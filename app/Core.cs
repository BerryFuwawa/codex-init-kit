using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;
using System.Net.Sockets;

namespace CodexKit {
public static class Core {
    public const string Version = "2.2.13";
    public const string Repository = "BerryFuwawa/codex-init-kit";
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexInitKit", "Desktop");
    public static string NetworkSettingsPath {get{return Path.Combine(Root,"network.json");}}
    public static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    public static readonly string[] Operations = {"status","initialize-preflight","initialize","proxy","init-rollback","rollback-selected","rollback-delete-all","cua-check","cua-repair","guard-install","guard-update","guard-native","guard-current","guard-rollback","guard-uninstall","doctor","config-check","config-cleanup","browser-check","browser-repair","browser-rollback","browser-start-prepare","browser-start-verify","desktop-update","official-cli-update","desktop-core-check","desktop-core-repair"};
    public static string Exe { get { return Assembly.GetExecutingAssembly().Location; } }
    public const string DefaultProxyHost = "127.0.0.1";
    const string DefaultParentModel = "gpt-6.1-sol";
    const string DefaultChildModel = "gpt-6.1-sol";
    const string DefaultParentEffort = "medium";
    const string DefaultChildEffort = "medium";
    static readonly Regex ModelIdPattern = new Regex("^[A-Za-z0-9][A-Za-z0-9._/:+-]{0,127}$", RegexOptions.CultureInvariant);
    static readonly Regex EffortPattern = new Regex("^[A-Za-z][A-Za-z0-9_-]{0,31}$", RegexOptions.CultureInvariant);
    public static string Sid { get { return WindowsIdentity.GetCurrent().User.Value; } }
    public static bool Admin { get { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); } }
    public static string Hash(string path) { using(var s=File.OpenRead(path)) using(var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(s)).Replace("-", "").ToLowerInvariant(); }
    public static string Quote(string value) { if(value.Contains("\"") || value.Contains("\r") || value.Contains("\n")) throw new ArgumentException("参数中包含不允许的引号或换行。"); return "\""+value+"\""; }
    public static string Logs { get { var path=Path.Combine(Root,"Logs"); Directory.CreateDirectory(path); return path; } }
    public static string Extract(string directory,bool privileged=false) {
        if(privileged) {
            var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);
            var admins=new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null);var system=new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null);
            acl.SetOwner(admins);
            foreach(var sid in new[]{admins,system})acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            if(Directory.Exists(directory))throw new IOException("任务目录已存在，操作已停止。");
            Directory.CreateDirectory(directory,acl);
        } else Directory.CreateDirectory(directory);
        var assembly=Assembly.GetExecutingAssembly();
        foreach(var name in new[]{"init.cmd","guard.cmd","cua.cmd","Backend.ps1","Backend.Tests.ps1","ConfigCompatibility.cs","InitializationPreflight.ps1","InitializationPreflight.Tests.ps1","InitConfigAdapter.Tests.ps1","Backend220.ps1","Backend220.Tests.ps1","BrowserStartup.ps1","BrowserStartup.Tests.ps1","DesktopCoreRecovery.ps1","DesktopCoreRecovery.Tests.ps1","RuntimeDiscovery.Tests.ps1","AntiOverDefense.md","RtkRules.md","FolderRules.md","SubagentRules.md","HERO-Anti-OverDefense.LICENSE.txt","WorkJournal.md","CommandLineFirst.md","GitHubTransport.cs","GitHubDownload.ps1","DesktopUpdate225.Tests.ps1"}) {
            using(var stream=assembly.GetManifestResourceStream("Payload."+name)) {
                if(stream==null) throw new InvalidOperationException("缺少内置资源："+name);
                using(var file=new FileStream(Path.Combine(directory,name), FileMode.CreateNew,FileAccess.Write,FileShare.Read)) stream.CopyTo(file);
            }
        }
        return directory;
    }
    public static bool NeedsAdmin(string operation) { return operation!="initialize-preflight" && operation!="desktop-core-check" && operation!="desktop-core-repair" && operation!="browser-start-verify" && operation!="browser-check" && operation!="status" && operation!="cua-check" && operation!="doctor" && operation!="config-check" && operation!="config-cleanup" && operation!="rollback-delete-all"; }

    public static string BrowserLaunchUri(Dictionary<string,object> prepared,Dictionary<string,object> current) {
        const string family="OpenAI.Codex_2p2nqsd0c76g0";
        var package=Value(prepared,"packageFullName","");var app=Value(prepared,"appId","");
        if(Value(prepared,"packageFamilyName","")!=family || !Regex.IsMatch(package,@"^OpenAI\.Codex_[A-Za-z0-9._-]+_2p2nqsd0c76g0\z") || !Regex.IsMatch(app,@"^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}\z"))throw new InvalidDataException("官方桌面应用身份无效，启动已停止。");
        foreach(var key in new[]{"packageFullName","packageFamilyName","appId"})if(!String.Equals(Value(prepared,key,""),Value(current,key,""),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("桌面应用已更新，请重新执行浏览器修复后启动。");
        return @"shell:AppsFolder\"+family+"!"+app;
    }

    static Dictionary<string,object> DefaultOptions() {
        return new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase) {
            {"folderManagement", true}, {"antiOverDefense", true}, {"workJournal",true}, {"commandLineFirst",true}, {"allowCloseRunningApp",false}, {"memories",true}, {"githubMirrorEnabled",true}, {"customGithubMirror",""}, {"subagents", false}, {"cuaRepair", true}, {"browserRepair", true}, {"officialCliUpdate", true}, {"desktopUpdate", true},
            {"parentModel", DefaultParentModel}, {"childModel", DefaultChildModel},
            {"parentEffort", DefaultParentEffort}, {"childEffort", DefaultChildEffort},
            {"proxyHost", DefaultProxyHost}, {"cliMode","auto"}
        };
    }
    static bool ReadOptionBoolean(Dictionary<string,object> source,string key,bool fallback) {
        object value;
        if(source==null || !source.TryGetValue(key,out value) || value==null) return fallback;
        if(value is bool) return (bool)value;
        throw new ArgumentException("选项 "+key+" 必须为布尔值。");
    }
    static string ReadOptionString(Dictionary<string,object> source,string key,string fallback) {
        object value;
        if(source==null || !source.TryGetValue(key,out value) || value==null) return fallback;
        var text=value as string;
        if(text==null || String.IsNullOrWhiteSpace(text)) throw new ArgumentException("选项 "+key+" 必须为有效文本。");
        if((key=="parentModel" || key=="childModel") && !ModelIdPattern.IsMatch(text)) throw new ArgumentException("选项 "+key+" 的模型 ID 无效。");
        if((key=="parentEffort" || key=="childEffort") && !EffortPattern.IsMatch(text)) throw new ArgumentException("选项 "+key+" 的思考强度无效。");
        return text;
    }
    public static string NormalizeProxyHost(string value) {
        if(String.IsNullOrWhiteSpace(value)) return DefaultProxyHost;
        IPAddress address;
        var text=value.Trim();
        if(!IPAddress.TryParse(text,out address)) throw new ArgumentException("代理 IP 必须是有效的 IPv4 或 IPv6 地址。");
        return address.ToString();
    }
    static string ReadProxyHost(Dictionary<string,object> source) {
        object value;
        if(source==null || !source.TryGetValue("proxyHost",out value) || value==null) return DefaultProxyHost;
        var text=value as string;
        if(text==null) throw new ArgumentException("代理 IP 必须为文本。");
        return NormalizeProxyHost(text);
    }
    public static Dictionary<string,object> NormalizeOptions(Dictionary<string,object> source) {
        var normalized=DefaultOptions();
        if(source==null) return normalized;
        normalized["folderManagement"]=ReadOptionBoolean(source,"folderManagement",true);
        normalized["antiOverDefense"]=ReadOptionBoolean(source,"antiOverDefense",true);
        normalized["workJournal"]=ReadOptionBoolean(source,"workJournal",true);
        normalized["commandLineFirst"]=ReadOptionBoolean(source,"commandLineFirst",true);
        normalized["allowCloseRunningApp"]=ReadOptionBoolean(source,"allowCloseRunningApp",false);
        if(source.ContainsKey("closeApprovedProcesses"))
            normalized["closeApprovedProcesses"]=ReadOptionString(source,"closeApprovedProcesses","");
        normalized["memories"]=ReadOptionBoolean(source,"memories",true);
        normalized["githubMirrorEnabled"]=ReadOptionBoolean(source,"githubMirrorEnabled",true);
        object customMirror;
        if(source.TryGetValue("customGithubMirror",out customMirror)){
            if(customMirror!=null&&!(customMirror is string))throw new ArgumentException("自定义 GitHub 镜像需为文本。");
            normalized["customGithubMirror"]=(bool)normalized["githubMirrorEnabled"]?GitHubNetworkOptions.NormalizePrefix(customMirror as string):"";
        }
        normalized["subagents"]=ReadOptionBoolean(source,"subagents",false);
        normalized["cuaRepair"]=ReadOptionBoolean(source,"cuaRepair",true);
        foreach(var key in new[]{"browserRepair","officialCliUpdate","desktopUpdate"})normalized[key]=ReadOptionBoolean(source,key,true);
        var cliMode=ReadOptionString(source,"cliMode","auto");
        if(cliMode!="auto"&&cliMode!="desktop-native"&&cliMode!="official-standalone")throw new ArgumentException("请选择有效的 CLI 模式。");
        normalized["cliMode"]=cliMode;
        normalized["parentModel"]=ReadOptionString(source,"parentModel",DefaultParentModel);
        normalized["childModel"]=ReadOptionString(source,"childModel",DefaultChildModel);
        normalized["parentEffort"]=ReadOptionString(source,"parentEffort",DefaultParentEffort);
        normalized["childEffort"]=ReadOptionString(source,"childEffort",DefaultChildEffort);
        normalized["proxyHost"]=ReadProxyHost(source);
        object projectPath;
        if(source.TryGetValue("projectPath",out projectPath)) {
            if(projectPath!=null && !(projectPath is string))throw new ArgumentException("项目路径必须为文本。");
            normalized["projectPath"]=projectPath==null?"":((string)projectPath).Trim();
        }
        object rollbackId;
        if(source.TryGetValue("rollbackId",out rollbackId)) {
            if(!(rollbackId is string)||!Regex.IsMatch((string)rollbackId,@"\A[a-f0-9]{64}\z"))throw new ArgumentException("回滚目标标识无效，请刷新备份列表。");
            normalized["rollbackId"]=rollbackId;
        }
        return normalized;
    }
    static Dictionary<string,object> DecodeOptions(string value) {
        if(String.IsNullOrWhiteSpace(value)) throw new ArgumentException("后台任务选项为空。");
        var bytes=Convert.FromBase64String(value);
        var json= new UTF8Encoding(false,true).GetString(bytes);
        return NormalizeOptions(Json.Deserialize<Dictionary<string,object>>(json));
    }
    static string EncodeOptions(Dictionary<string,object> options) {
        var json=Json.Serialize(NormalizeOptions(options));
        return Convert.ToBase64String(new UTF8Encoding(false).GetBytes(json));
    }
    public static int Worker(string[] args) {
        // All executable code comes from this EXE's embedded resources, never a supplied job/script path.
        if((args.Length!=7 && args.Length!=8) || args.Length<2 || !Operations.Contains(args[1])) return 64;
        Guid id; int port;
        Dictionary<string,object> options=null;
        if(!Guid.TryParseExact(args[2],"N",out id) || !int.TryParse(args[4],out port) || port<0 || port>65535) return 64;
        if(args[3].Length!=1 || args[3][0]<'A' || args[3][0]>'Z' || (args[5]!="0" && args[5]!="1")) return 64;
        if(args.Length==8) { try { options=DecodeOptions(args[7]); } catch { return 64; } }
        var log=Path.Combine(Logs,args[2]+".log");
        if(Sid!=args[6]) { File.WriteAllText(log,"管理员账户与原账户不同。操作已停止，请使用同一账户授权。\r\n",Encoding.UTF8); return 65; }
        if(NeedsAdmin(args[1]) && !Admin) return 66;
        var job=NeedsAdmin(args[1])?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"CodexInitKit-privileged-"+Guid.NewGuid().ToString("N")):Path.Combine(Root,"Jobs",args[2]);
        try {
            var payload=Extract(job,NeedsAdmin(args[1]));
            using(var writer=new StreamWriter(log,false,new UTF8Encoding(false))) {
                writer.AutoFlush=true;
                object gate=new object();
                var psi=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe"));
                psi.UseShellExecute=false; psi.CreateNoWindow=true; psi.RedirectStandardOutput=true; psi.RedirectStandardError=true;
                psi.StandardOutputEncoding=Encoding.UTF8; psi.StandardErrorEncoding=Encoding.UTF8;
                var optionsArgument=options==null?"":" -OptionsBase64 "+Quote(EncodeOptions(options));
                psi.Arguments="-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+Quote(Path.Combine(payload,"Backend.ps1"))+" -Operation "+Quote(args[1])+" -PayloadRoot "+Quote(payload)+" -Drive "+Quote(args[3])+" -ProxyPort "+port+(args[1]=="initialize"&&args[5]=="1"?" -InstallProtection":"")+optionsArgument;
                using(var process=new Process()) {
                    process.StartInfo=psi;
                    process.OutputDataReceived+=(s,e)=>{if(e.Data!=null) lock(gate) writer.WriteLine(e.Data);};
                    process.ErrorDataReceived+=(s,e)=>{if(e.Data!=null) lock(gate) writer.WriteLine(e.Data);};
                    process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine(); process.WaitForExit();
                    return process.ExitCode;
                }
            }
        } catch(Exception ex) { File.AppendAllText(log,"执行失败："+ex.Message+"\r\n",Encoding.UTF8); return 1; }
        finally { try { if(Directory.Exists(job) && (File.GetAttributes(job)&FileAttributes.ReparsePoint)==0) Directory.Delete(job,true); } catch { } }
    }
    public static Process StartWorker(string operation,string id,string drive,int port,bool guard) {
        return StartWorker(operation,id,drive,port,guard,null);
    }
    public static Process StartWorker(string operation,string id,string drive,int port,bool guard,Dictionary<string,object> options) {
        if(!Operations.Contains(operation)) throw new ArgumentException("不支持此操作。");
        if(port<0 || port>65535) throw new ArgumentOutOfRangeException("port");
        var args="--worker "+operation+" "+id+" "+drive+" "+port+" "+(guard?"1":"0")+" "+Sid;
        if(options!=null) args+=" "+EncodeOptions(options);
        var info=new ProcessStartInfo(Exe,args);
        info.UseShellExecute=true; info.WindowStyle=ProcessWindowStyle.Hidden;
        if(NeedsAdmin(operation) && !Admin) info.Verb="runas";
        return Process.Start(info);
    }
    public static Dictionary<string,object> ReadJson(string json) { return Json.Deserialize<Dictionary<string,object>>(json); }
    public static string Value(Dictionary<string,object> obj,string key,string fallback) { return obj!=null && obj.ContainsKey(key) && obj[key]!=null ? Convert.ToString(obj[key]) : fallback; }
    static string ProxyAddress(string proxyHost,int proxyPort) {
        var normalized=NormalizeProxyHost(proxyHost);
        IPAddress address;
        if(!IPAddress.TryParse(normalized,out address)) throw new ArgumentException("代理 IP 必须是有效的 IPv4 或 IPv6 地址。");
        var host=address.AddressFamily==AddressFamily.InterNetworkV6?"["+normalized+"]":normalized;
        return "http://"+host+":"+proxyPort;
    }
    public static string Fetch(string url,int proxyPort) {
        return Fetch(url,proxyPort,DefaultProxyHost);
    }
    public static string Fetch(string url,int proxyPort,string proxyHost) {
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
        var request=(HttpWebRequest)WebRequest.Create(url); request.UserAgent="CodexInitKitDesktop/"+Version;
        request.Timeout=12000; request.ReadWriteTimeout=12000;
        request.Proxy=proxyPort>0?new WebProxy(ProxyAddress(proxyHost,proxyPort)):null;
        using(var response=request.GetResponse()) using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)) return reader.ReadToEnd();
    }
    public static Dictionary<string,object> CheckUpdate(int port) {
        return CheckUpdate(port,DefaultProxyHost);
    }
    public static Dictionary<string,object> CheckUpdate(int port,string proxyHost) {
        return CheckUpdate(port,proxyHost,GitHubNetworkOptions.Load(NetworkSettingsPath),null);
    }
    public static Dictionary<string,object> CheckUpdate(int port,string proxyHost,GitHubNetworkOptions settings,Action<string> writeLog) {
        return CheckUpdate(new GitHubTransfer(settings,port,proxyHost,writeLog),writeLog);
    }
    internal static Dictionary<string,object> CheckUpdate(GitHubTransfer transfer,Action<string> writeLog) {
        var commitUrl="https://api.github.com/repos/"+Repository+"/commits/main";
        var rawRoot="https://raw.githubusercontent.com/"+Repository+"/";
        var mainUrl=rawRoot+"main/desktop-update.json";
        Func<string,bool> validCommit=s=>Regex.IsMatch(Value(ReadJson(s),"sha",""),"^[0-9a-f]{40}$");
        Dictionary<string,object> manifest=null;
        Func<string,bool> validManifest=s=>{manifest=ValidateUpdate(s);return true;};
        string commitFailure,mainFailure=null;
        HttpStatusCode? status,mainStatus;
        var commit=transfer.FetchOfficial(commitUrl,validCommit,out commitFailure,out status);
        bool tryMainFirst=status==HttpStatusCode.Forbidden||(int?)status==429;
        if(tryMainFirst){
            if(writeLog!=null)writeLog("GitHub 提交接口返回 HTTP "+(int)status+"，先读取官方更新清单。");
            if(transfer.FetchOfficial(mainUrl,validManifest,out mainFailure,out mainStatus)!=null)return manifest;
        }
        if(commit==null){
            try{commit=transfer.FetchMirrors(commitUrl,validCommit,commitFailure);}
            catch(IOException){
                if(writeLog!=null)writeLog("GitHub 提交查询未完成，改用本项目 main 的公开更新清单。");
                if(tryMainFirst)transfer.FetchMirrors(mainUrl,validManifest,mainFailure);
                else transfer.Fetch(mainUrl,validManifest);
                return manifest;
            }
        }
        var reference=Value(ReadJson(commit),"sha","");
        transfer.Fetch(rawRoot+reference+"/desktop-update.json",validManifest);
        return manifest;
    }
    public static Dictionary<string,object> ValidateUpdate(string json) {
        var manifest=ReadJson(json);
        if(Value(manifest,"repository","")!=Repository || Value(manifest,"schema_version","")!="1") throw new InvalidDataException("更新清单来源不匹配");
        var version=Value(manifest,"version","");
        new Version(version);
        var url=Value(manifest,"url",""); var uri=new Uri(url);
        if(uri.Scheme!="https" || uri.Host!="github.com" || uri.UserInfo.Length!=0 ||
            !uri.AbsolutePath.StartsWith("/"+Repository+"/releases/download/v"+version+"/",StringComparison.Ordinal))
            throw new InvalidDataException("更新链接不属于本项目的对应版本");
        if(!System.Text.RegularExpressions.Regex.IsMatch(Value(manifest,"sha256",""),"^[0-9a-f]{64}$")) throw new InvalidDataException("更新校验值不合法");
        return manifest;
    }
    public static Task<string> DownloadUpdate(Dictionary<string,object> manifest,int port) {
        return DownloadUpdate(manifest,port,DefaultProxyHost);
    }
    public static Task<string> DownloadUpdate(Dictionary<string,object> manifest,int port,string proxyHost) {
        return DownloadUpdate(manifest,port,proxyHost,GitHubNetworkOptions.Load(NetworkSettingsPath),null);
    }
    public static Task<string> DownloadUpdate(Dictionary<string,object> manifest,int port,string proxyHost,GitHubNetworkOptions settings,Action<string> writeLog) {
        return Task.Run(()=>{
            ValidateUpdate(Json.Serialize(manifest));
            var stage=Path.Combine(Path.GetDirectoryName(Exe),".CodexInitKit-update-"+Guid.NewGuid().ToString("N")+".exe");
            new GitHubTransfer(settings,port,proxyHost,writeLog).Download(Value(manifest,"url",""),stage,path=>{
                if(Hash(path)!=Value(manifest,"sha256",""))throw new InvalidDataException("下载校验失败，当前版本保持不变。");
                using(var stream=File.OpenRead(path))if(stream.ReadByte()!=77||stream.ReadByte()!=90)throw new InvalidDataException("下载文件不是 Windows EXE。");
            });return stage;
        });
    }
    static string PowerShellLiteral(string value) {
        if(value==null) throw new ArgumentNullException("value");
        return "'"+value.Replace("'","''")+"'";
    }
    internal static string UpdateWorkRoot(string target) {
        return Path.Combine(Path.GetDirectoryName(target),"."+Path.GetFileName(target)+".update");
    }
    internal static void CleanupCompletedUpdates(string target) {
        var root=UpdateWorkRoot(target);
        if(!Directory.Exists(root))return;
        try{
            foreach(var directory in Directory.GetDirectories(root)){
                Guid id;
                if(!Guid.TryParseExact(Path.GetFileName(directory),"N",out id))continue;
                var marker=Path.Combine(directory,"cleanup-ready");
                if(!File.Exists(marker))continue;
                try{
                    File.Delete(Path.Combine(directory,"previous.tmp"));
                    File.Delete(Path.Combine(directory,"replaced.tmp"));
                    File.Delete(marker);
                    Directory.Delete(directory);
                }catch(IOException){}catch(UnauthorizedAccessException){}
            }
            if(Directory.GetFileSystemEntries(root).Length==0)Directory.Delete(root);
        }catch(IOException){}catch(UnauthorizedAccessException){}
    }
    public static string BuildUpdateScript(string stage,string target,int pid,string logPath,bool restart) {
        if(String.IsNullOrWhiteSpace(stage)) throw new ArgumentException("未指定更新文件。", "stage");
        if(String.IsNullOrWhiteSpace(target)) throw new ArgumentException("未指定更新目标。", "target");
        if(String.IsNullOrWhiteSpace(logPath)) throw new ArgumentException("未指定更新日志路径。", "logPath");
        var stagePath=Path.GetFullPath(stage); var targetPath=Path.GetFullPath(target); var resultLog=Path.GetFullPath(logPath);
        if(String.Equals(stagePath,targetPath,StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("更新文件和当前程序不能使用同一路径。");
        var expectedStageHash=Hash(stagePath); var expectedTargetHash=Hash(targetPath);
        var workRoot=UpdateWorkRoot(targetPath);
        var transaction=Path.Combine(workRoot,Guid.NewGuid().ToString("N"));
        var script=new StringBuilder();
        script.Append("$ErrorActionPreference='Stop';");
        script.Append("$stage=").Append(PowerShellLiteral(stagePath)).Append(";");
        script.Append("$target=").Append(PowerShellLiteral(targetPath)).Append(";");
        script.Append("$log=").Append(PowerShellLiteral(resultLog)).Append(";");
        script.Append("$chineseLog=").Append(PowerShellLiteral(DesktopLocalization.ChineseLogPath(resultLog))).Append(";");
        script.Append("$workRoot=").Append(PowerShellLiteral(workRoot)).Append(";");
        script.Append("$transaction=").Append(PowerShellLiteral(transaction)).Append(";");
        script.Append("$backup=Join-Path $transaction 'previous.tmp';$rollback=Join-Path $transaction 'replaced.tmp';$marker=Join-Path $transaction 'cleanup-ready';");
        script.Append("$expectedStageHash=").Append(PowerShellLiteral(expectedStageHash)).Append(";");
        script.Append("$expectedTargetHash=").Append(PowerShellLiteral(expectedTargetHash)).Append(";");
        script.Append("$replaced=$false;$exitCode=1;");
        script.Append("function Get-Sha([string]$Path){if(-not [IO.File]::Exists($Path)){throw '找不到文件：'+$Path};$stream=[IO.File]::OpenRead($Path);$hash=[Security.Cryptography.SHA256]::Create();try{[BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}finally{$stream.Dispose();$hash.Dispose()}};");
        script.Append("function Test-Sha([string]$Path,[string]$Expected){try{if(-not [IO.File]::Exists($Path)){return $false};return ((Get-Sha $Path) -ceq $Expected)}catch{return $false}};");
        script.Append("function Write-UpdateLog([string]$Status,[string]$Message){try{$parent=[IO.Path]::GetDirectoryName($log);if($parent){[IO.Directory]::CreateDirectory($parent)|Out-Null};$time=Get-Date -Format o;$line=$time+' ['+$Status+'] '+$Message;[IO.File]::AppendAllText($log,$line+[Environment]::NewLine,(New-Object Text.UTF8Encoding($false)));$label=switch($Status){'success'{'成功'}'cleanup'{'清理'}default{'失败'}};$translated=$Message;if($Status -eq 'failure' -and $Message -match '[A-Za-z]{2,} [A-Za-z]{2,}'){$translated='更新操作未完成，原因尚未识别；请查看原始 update.log。';if($Message -match '(HRESULT: -?[0-9]+|0x[0-9a-fA-F]{4,8})'){$translated+=' 错误码：'+$Matches[1]}};[IO.File]::AppendAllText($chineseLog,$time+' 【'+$label+'】 '+$translated+[Environment]::NewLine,(New-Object Text.UTF8Encoding($false)))}catch{}};");
        script.AppendLine(@"
function Remove-UpdateFile([string]$Path) {
    for($attempt=0;;$attempt++){
        try{[IO.File]::Delete($Path);return}
        catch{if($attempt -ge 3){throw};Start-Sleep -Milliseconds 300}
    }
}
");
        if(pid>0) script.Append("Wait-Process -Id ").Append(pid.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(" -ErrorAction SilentlyContinue;");
        script.Append("try{");
        script.Append("if(-not (Test-Sha $stage $expectedStageHash)){throw '更新文件校验失败'};");
        script.Append("if(-not (Test-Sha $target $expectedTargetHash)){throw '更新前当前程序已变化'};");
        script.Append("[void][IO.Directory]::CreateDirectory($transaction);");
        script.Append("[IO.File]::Replace($stage,$target,$backup);$replaced=$true;");
        script.Append("if(-not (Test-Sha $target $expectedStageHash)){throw '更新后程序校验失败'};");
        if(restart) script.Append("Start-Process -FilePath $target -WorkingDirectory ([IO.Path]::GetDirectoryName($target)) | Out-Null;");
        script.Append("Write-UpdateLog 'success' '更新已安装。';$exitCode=0;");
        script.Append("}catch{$failure=$_.Exception.Message+' (HRESULT: '+$_.Exception.HResult+')';if($replaced -and (Test-Sha $target $expectedStageHash) -and (Test-Sha $backup $expectedTargetHash)){try{[IO.File]::Replace($backup,$target,$rollback);if(-not (Test-Sha $target $expectedTargetHash)){throw '回滚文件校验失败'};$replaced=$false;$failure=$failure+'；回滚成功'}catch{$failure=$failure+'；回滚失败：'+$_.Exception.Message}};Write-UpdateLog 'failure' $failure;}");
        script.AppendLine(@"
finally {
    try{Remove-UpdateFile $stage}catch{Write-UpdateLog 'cleanup' ('下载临时文件暂未清理：'+$stage)}
    if([IO.Directory]::Exists($transaction)){
        if($exitCode -eq 0 -or (!$replaced -and (Test-Sha $target $expectedTargetHash))){
            try {
                [IO.File]::WriteAllText($marker,'')
                Remove-UpdateFile $backup
                Remove-UpdateFile $rollback
                Remove-UpdateFile $marker
                if([IO.Directory]::Exists($transaction)){[IO.Directory]::Delete($transaction)}
                if([IO.Directory]::Exists($workRoot) -and [IO.Directory]::GetFileSystemEntries($workRoot).Length -eq 0){[IO.Directory]::Delete($workRoot)}
            } catch {Write-UpdateLog 'cleanup' ('更新备份暂未清理：'+$transaction+'；'+$_.Exception.Message)}
        } else {Write-UpdateLog 'cleanup' ('已保留更新恢复文件：'+$transaction)}
    }
}
exit $exitCode
");
        return script.ToString();
    }
    public static void ScheduleUpdate(string stage) {
        var target=Exe; var logPath=Path.Combine(Logs,"update.log");
        try {
            var script=BuildUpdateScript(stage,target,Process.GetCurrentProcess().Id,logPath,true);
            var powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
            var encoded=Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var info=new ProcessStartInfo(powershell,"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand "+encoded);
            info.UseShellExecute=false; info.CreateNoWindow=true; info.WindowStyle=ProcessWindowStyle.Hidden;
            using(var process=Process.Start(info)) { }
        } catch {
            try { if(File.Exists(stage)) File.Delete(stage); } catch { }
            throw;
        }
    }
}
}
