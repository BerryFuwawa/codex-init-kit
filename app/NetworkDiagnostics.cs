using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Collections.Generic;

namespace CodexKit {
internal sealed class SavedNetworkConfiguration {
    internal string Path,Summary;
    internal static readonly string[] Keys={"HTTPS_PROXY","HTTP_PROXY","ALL_PROXY","NO_PROXY"};
    internal static SavedNetworkConfiguration Read(string home,Func<string,string> inherited){
        var path=System.IO.Path.Combine(home,".env");var declarations=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var repeated=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(File.Exists(path))foreach(var line in File.ReadAllLines(path,Encoding.UTF8)){
            var m=Regex.Match(line,@"^\s*(?:export\s+)?(HTTPS_PROXY|HTTP_PROXY|ALL_PROXY|NO_PROXY)\s*=\s*(.*)$",RegexOptions.IgnoreCase);
            if(!m.Success)continue;var key=m.Groups[1].Value;if(declarations.ContainsKey(key))repeated.Add(key);declarations[key]=m.Groups[2].Value;
        }
        var rows=new List<string>();
        foreach(var key in Keys){string raw,value;bool saved=declarations.TryGetValue(key,out raw);
            if(saved){if(repeated.Contains(key)||!Literal(raw,out value)){rows.Add(key+"：.env 含表达式或复杂声明，由官方 CLI 解析；地址未展开");continue;}}
            else value=inherited(key);
            if(value==null)continue;
            rows.Add(key+"："+(saved?".env 已保存 · ":"当前进程继承环境 · ")+(value.Length==0?"空值":key=="NO_PROXY"?value:DisplayAddress(value)));
        }
        return new SavedNetworkConfiguration{Path=path,Summary=rows.Count==0?"未发现 .env 或继承环境中的代理声明；官方运行时仍可能使用系统代理或受管策略。":String.Join("\n",rows)};
    }
    static bool Literal(string raw,out string value){
        value="";var m=Regex.Match(raw,@"^(?:""(?<double>[^""\r\n]*)""|'(?<single>[^'\r\n]*)'|(?<plain>[^\s""']*))\s*(?:#.*)?$");if(!m.Success)return false;
        if(m.Groups["single"].Success){value=m.Groups["single"].Value;return true;}
        value=m.Groups["double"].Success?m.Groups["double"].Value:m.Groups["plain"].Value;
        return value.IndexOf('$')<0&&value.IndexOf('\\')<0;
    }
    static string DisplayAddress(string value){Uri uri;if(!Uri.TryCreate(value,UriKind.Absolute,out uri)||String.IsNullOrEmpty(uri.Host))return "地址由官方 CLI 解析";return uri.Scheme+"://"+(uri.UserInfo.Length>0?"***@":"")+uri.Authority.Substring(uri.Authority.LastIndexOf('@')+1);}
}
internal static class NetworkDiagnostics {
    internal static Dictionary<string,object> FailedReport(Exception error){var win=error as System.ComponentModel.Win32Exception;var message="模型线路检查未完成："+DesktopLocalization.ExceptionText(error)+(win==null?"":"（Windows 错误码 "+win.NativeErrorCode+"）");return new Dictionary<string,object>{{"kind","model"},{"passed",false},{"modelState",message},{"summary",message}};}
    internal static ProcessStartInfo ProbeStartInfo(string cli){return new ProcessStartInfo(cli,"doctor --network-only --json"){UseShellExecute=false,WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};}
    internal static Dictionary<string,object> ModelCheck(string cli,Action<string> log){
        if(String.IsNullOrWhiteSpace(cli)||!File.Exists(cli))throw new FileNotFoundException("尚未找到官方 CLI，请先刷新状态或准备官方独立 CLI。");
        var path=System.IO.Path.GetFullPath(cli);var home=CurrentModelSettings.CodexHome();var standalone=System.IO.Path.Combine(home,"packages","standalone")+System.IO.Path.DirectorySeparatorChar;var store=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WindowsApps")+System.IO.Path.DirectorySeparatorChar;
        if(!path.StartsWith(standalone,StringComparison.OrdinalIgnoreCase)&&!path.StartsWith(store,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("模型线路检查仅支持已识别的官方 CLI。");
        var saved=SavedNetworkConfiguration.Read(home,Environment.GetEnvironmentVariable);log("检查已保存的网络配置："+saved.Path);log(saved.Summary);log("探针使用官方 CLI 自行读取的配置；界面中尚未保存的代理不参与检查。");
        using(var process=Process.Start(ProbeStartInfo(path))){var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();if(!process.WaitForExit(45000)){process.Kill();process.WaitForExit();throw new IOException("模型线路诊断超时，已结束本次探针。");}process.WaitForExit();var text=output.GetAwaiter().GetResult();error.GetAwaiter().GetResult();Dictionary<string,object> data;
            try{data=Core.ReadJson(text);}catch(ArgumentException){throw new IOException("当前 CLI 未返回支持的网络诊断结果。退出码："+process.ExitCode);}catch(InvalidOperationException){throw new IOException("当前 CLI 未返回支持的网络诊断结果。退出码："+process.ExitCode);}
            var report=ParseModelReport(data);report["configurationPath"]=saved.Path;report["savedConfiguration"]=saved.Summary;log(Core.Value(report,"proxyPolicy",""));log(Core.Value(report,"modelState",""));log(Core.Value(report,"diagnosticDetail",""));return report;
        }
    }
    internal static Dictionary<string,object> Details(Dictionary<string,object> check){object value;return check!=null&&check.TryGetValue("details",out value)?value as Dictionary<string,object>:null;}
    internal static Dictionary<string,object> Check(Dictionary<string,object> data,string name){object checks,item;if(!data.TryGetValue("checks",out checks)||!(checks is Dictionary<string,object>)||!((Dictionary<string,object>)checks).TryGetValue(name,out item))return null;return item as Dictionary<string,object>;}
    internal static Dictionary<string,object> ParseModelReport(Dictionary<string,object> data){
        var ws=Check(data,"network.websocket_reachability");var http=Check(data,"network.provider_reachability");if(ws==null||http==null)throw new InvalidDataException("网络诊断缺少模型 HTTP 或 WebSocket 检查项，不能确认成功。");
        var details=Details(ws);bool skipped=Core.Value(details,"supports websockets","")=="false";bool handshake=Core.Value(ws,"status","")=="ok"&&Core.Value(details,"handshake result","").StartsWith("HTTP 101",StringComparison.Ordinal);bool httpOk=Core.Value(http,"status","")=="ok";
        var env=Details(Check(data,"network.env"));string system=Core.Value(env,"respect system proxy","未报告"),managed=Core.Value(env,"managed proxy","未报告");
        var policy=managed!="not configured"&&managed!="未报告"?"官方 CLI 报告受管代理策略；实际路由以该策略为准。":system=="enabled"?"官方 CLI 已启用系统代理优先策略（含 PAC）；.env 地址是环境回退来源，不能据此认定实际使用该地址。":system=="disabled"?"官方 CLI 未启用 respect_system_proxy；.env 覆盖同名继承变量。系统代理回退、NO_PROXY 和受管策略仍由官方运行时决定。":"官方 CLI 未报告完整代理策略；已列出保存的声明，实际路由未确认。";
        bool passed=httpOk&&(skipped||handshake);string state="HTTP："+(httpOk?"官方 CLI 探针通过":"官方 CLI 探针未通过")+"；模型 WebSocket："+(skipped?"当前提供方未启用，未进行握手":handshake?"独立探针握手通过":"独立探针握手未通过");
        return new Dictionary<string,object>{{"kind","model"},{"passed",passed},{"modelState",state},{"proxyPolicy",policy},{"diagnosticDetail","官方 HTTP 诊断："+Core.Value(http,"summary","未报告")+"；官方 WebSocket 诊断："+Core.Value(ws,"summary","未报告")+"；握手结果："+Core.Value(details,"handshake result","未执行或未报告")},{"summary",state+"。未创建对话；当前会话传输与 Work 在线状态需分别验收。"}};
    }
}
}
