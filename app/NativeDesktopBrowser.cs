using File=CodexKit.Native.NativeFile;
using Directory=CodexKit.Native.NativeDirectory;
using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.Net.Sockets;using CodexKit.Native;
namespace CodexKit {
internal sealed class NativeDesktopBrowser {
    readonly NativePaths paths;readonly NativeServices services;readonly int port;readonly string host;readonly Action<string> log;
    internal Func<string,int,string> ProxyCheck=Reachability;
    internal NativeDesktopBrowser(NativePaths p,NativeServices s,int proxyPort,string proxyHost,Action<string> emit){paths=p;services=s;port=proxyPort;host=proxyHost;log=emit;}
    internal bool RequiresChange(){var app=services.Desktop.Discover();var check=new NativeCua(paths,services).Run(false,paths.BackupRoot);if(!check.Passed)return true;var launchers=Directory.GetFiles(app.Source,"cua-repl.mjs",SearchOption.AllDirectories);if(launchers.Length!=1)throw new InvalidDataException("无法唯一识别浏览器入口。");var source=launchers[0];var launcher=Path.Combine(check.Target,source.Substring(app.Source.TrimEnd('\\').Length+1));NativeBrowserPatch.Load(paths,app,check.Target);var original=File.ReadAllBytes(source);var replacement=port>0?NativeBrowserPatch.Generate(original,new NativeOptions{Proxy=true,ProxyPort=port,ProxyHost=host}.ProxyUrl):original;return !File.ReadAllBytes(launcher).SequenceEqual(replacement);}
    internal void Repair(){using(var mutex=new System.Threading.Mutex(false,"Local\\CodexCUARepair-"+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value)){bool owned=false;try{try{owned=mutex.WaitOne(0);}catch(System.Threading.AbandonedMutexException){owned=true;}if(!owned)throw new IOException("另一个 CUA 或浏览器操作正在运行，请等待完成。");RepairCore();}finally{if(owned)mutex.ReleaseMutex();}}}
    void RepairCore(){
        var report=new Dictionary<string,object>{{"desktopVersion","未检测"},{"runtime","未检测"},{"patch","未检查"},{"integrity","未检查"},{"reachability","未检查"},{"environment","未检查"},{"connection","重启后真实浏览器连接：待验证"},{"checkedAt",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")},{"error",""}};
        var root=Path.Combine(paths.LocalData,"CodexInitKit","Desktop","BrowserConnection");
        try{
            var app=services.Desktop.Discover();
            var check=new NativeCua(paths,services).Run(false,root);if(!check.Passed)throw new IOException("当前 CUA 组件不完整，请先检查并修复电脑操作组件。");var target=check.Target;var id=check.RuntimeId;report["desktopVersion"]=app.Version;report["runtime"]=target;report["integrity"]="文件、目录、长度和 SHA-256 均一致。";
            var launchers=Directory.GetFiles(app.Source,"cua-repl.mjs",SearchOption.AllDirectories);if(launchers.Length!=1)throw new InvalidDataException("无法唯一识别浏览器入口，未覆盖文件。");var source=launchers[0];var relative=source.Substring(app.Source.TrimEnd('\\').Length+1);var launcher=Path.Combine(target,relative);var original=File.ReadAllBytes(source);string originalHash=NativeCua.HashBytes(original);var managed=NativeBrowserPatch.Load(paths,app,target);var currentHash=NativeCua.Hash(launcher);if(managed==null&&currentHash!=originalHash)throw new InvalidDataException("浏览器入口有未知修改，未覆盖文件。");
            var options=new NativeOptions{Proxy=port>0,ProxyHost=host,ProxyPort=port};string proxy=options.ProxyUrl;report["proxy"]=proxy;
            byte[] replacement=original;
            if(port>0){report["reachability"]=ProxyCheck(host,port);report["environment"]=Probe(target,proxy);replacement=NativeBrowserPatch.Generate(original,proxy);}
            if(currentHash==NativeCua.HashBytes(replacement)){report["patch"]="现有浏览器配置一致，跳过文件替换；真实连接仍需单独验收";log("浏览器入口一致，跳过修复，无需退出 Codex。");return;}
            var latest=services.Desktop.Discover();if(latest.FullName!=app.FullName||latest.Install!=app.Install||NativeCua.Hash(source)!=originalHash||NativeCua.Hash(launcher)!=currentHash)throw new IOException("桌面版本或浏览器入口在检查后变化，请重新检查。");if(services.Processes.List(paths,latest).Count>0)throw new IOException("应用重新启动，未写入浏览器入口。");NativeData.NoLinks(root);Directory.CreateDirectory(root);
            if(port>0){var backup=Path.Combine(root,id+"-"+originalHash+".bin");if(File.Exists(backup)){if(NativeCua.Hash(backup)!=originalHash)throw new IOException("浏览器原始备份校验不符。");}else NativeData.WriteBytes(backup,original);
                NativeData.Write(Path.Combine(root,id+".json"),NativeData.Json.Serialize(new{schema=1,runtimeId=id,appVersion=app.Version,packageFullName=app.FullName,source=app.Source,target=target,proxy=proxy,originalHash=originalHash,patchedHash=NativeCua.HashBytes(replacement),preparedAt=DateTime.UtcNow.ToString("o")}));}
            if(currentHash!=NativeCua.HashBytes(replacement))NativeData.WriteBytes(launcher,replacement);if(port>0)NativeBrowserPatch.Load(paths,app,target);report["patch"]=port>0?"修复已准备；请重新启动 Codex 并运行连接测试":"已恢复原始直连入口（或原本未修改）";
        }catch(Exception ex){report["error"]=ex.Message;report["patch"]="未完成："+ex.Message;throw;}
        finally{log("@@BROWSER@@"+Core.Json.Serialize(report));Directory.CreateDirectory(root);NativeData.Write(Path.Combine(root,"last-check.json"),Core.Json.Serialize(report));}
    }
    internal static string Reachability(string host,int port){using(var client=new TcpClient())try{var task=client.ConnectAsync(host.Trim('[',']'),port);if(!task.Wait(3000))return "代理不可达（连接超时）；尚未验证 Chrome。";task.GetAwaiter().GetResult();return "代理端口可达（TCP）；尚未验证 Chrome 或代理认证。";}catch(Exception ex){return "代理不可达："+ex.GetBaseException().Message;}}
    string Probe(string target,string proxy){var node=Path.Combine(target,"bin","node.exe");var capability=services.Commands.Run(node,new[]{"-p","JSON.stringify({proxy:process.allowedNodeEnvironmentFlags.has('--use-env-proxy')})"},null,15000);if(capability.TimedOut||capability.ExitCode!=0||!(capability.Output??"").Contains("\"proxy\":true"))throw new IOException("当前 Node 未通过环境代理能力检查。");var env=new Dictionary<string,string>{{"HTTP_PROXY",proxy},{"HTTPS_PROXY",proxy},{"NO_PROXY","localhost,127.0.0.1,::1"},{"NODE_USE_ENV_PROXY","1"},{"NODE_REPL_UNTRUSTED_ENV_ALLOWLIST","HTTP_PROXY,HTTPS_PROXY,NO_PROXY,NODE_USE_ENV_PROXY"}};
        const string code="const {spawnSync}=require('node:child_process');const keys=process.env.NODE_REPL_UNTRUSTED_ENV_ALLOWLIST.split(',');const env=Object.fromEntries(keys.map(k=>[k,process.env[k]]));const r=spawnSync(process.execPath,['-p','JSON.stringify(process.env)'],{env,encoding:'utf8'});if(r.status!==0)process.exit(1);const e=JSON.parse(r.stdout);for(const k of keys)if(e[k]!==process.env[k])process.exit(2);console.log('ENV_INHERITANCE_PASS');";var result=services.Commands.Run(node,new[]{"-e",code},env,15000);if(result.TimedOut||result.ExitCode!=0||!(result.Output??"").Contains("ENV_INHERITANCE_PASS"))throw new IOException("Node 隔离子进程环境继承未通过。");return "Node 代理能力及隔离子进程变量继承通过；真实浏览器连接待重启验证。";
    }
}
}
