using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CodexKit {
public static class DiagnosticAdvice {
    public const string AsciiFixture="Codex Doctor v0.160.0 · windows-x86_64\nNotes\n [!!] rollouts 176 active files · 1.97 GB on disk\n [!!] updates update configuration is locally consistent\n [!!] websocket Responses WebSocket failed; HTTPS fallback may still work - Check proxy, VPN, firewall, DNS, custom CA, and WebSocket policy support.\n [!!] mcp MCP configuration has optional issues - Set the missing MCP env vars or disable the affected server.\n [!!] threads 2 issues - rollout files are missing from the state DB; duplicate thread inventory entries found\n [XX] reachability desktop update and runtime CDN is unreachable\nEnvironment\n [ok] runtime standalone\n [ok] install consistent\n [ok] state databases healthy\n [!!] threads 2 issues - rollout files are missing from the state DB; duplicate thread inventory entries found\n [!!] mcp MCP configuration has optional issues - Set the missing MCP env vars or disable the affected server.\n [--] app-server not running (ephemeral mode)\n19 ok | 1 idle | 6 notes | 4 warn | 1 fail failed\n[FAIL] codex doctor failed with native exit code 1.";
    public static bool CompletedReport(string output){return Regex.IsMatch(output??"","(?im)^\\s*\\d+\\s+ok\\s*\\|[^\\r\\n]*\\d+\\s+fail\\b");}
    static bool Is(Dictionary<string,object> context,string key,bool expected){object value;return context!=null&&context.TryGetValue(key,out value)&&value is bool&&(bool)value==expected;}
    public static string Build(Dictionary<string,object> context,int exitCode,string output){
        var suggestions=new List<string>();var findings=new List<string>();
        if(Is(context,"standaloneHealthy",false)){findings.Add("官方独立 CLI 的组件缺失、校验异常或无法运行，诊断助手可能无法启动。");suggestions.Add("先退出 Codex，点击“安装 / 修复保护”，完成后再运行诊断助手。");}
        if(Is(context,"desktopFound",false)){findings.Add("未找到当前用户安装的 Codex 桌面应用。");suggestions.Add("确认已安装 Codex 桌面应用，并使用安装它的 Windows 账号运行本工具。");}
        else if(Is(context,"desktopHealthy",false)){findings.Add("Codex 桌面应用自带的 CLI 组件不完整。");suggestions.Add("先点击“安装 / 修复保护”准备备用组件；若仍需独立 CLI，可点击“切换官方独立 CLI”。");}
        var cleanOutput=Regex.Replace(output??"","\x1B\\[[0-?]*[ -/]*[@-~]","");
        var rows=Regex.Matches(cleanOutput,"(?im)^\\s*(?:\\[(?:!!|XX|fail|error|warn|warning)\\]|(?:fail|error|warn|warning)\\s*[:：]|[✗×❌⚠])\\s*(.+)$").Cast<Match>().Select(x=>x.Groups[1].Value.Trim()).Where(x=>!Regex.IsMatch(x,"^codex doctor failed with native exit code",RegexOptions.IgnoreCase)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var warnings=rows.Where(x=>!Regex.IsMatch(x,"^updates\\s+update configuration is locally consistent|^rollouts\\s+\\d+ active files",RegexOptions.IgnoreCase)).Take(20).ToArray();
        if(warnings.Length>0){var evidence=string.Join("\n",warnings);var recognized=false;
            if(Regex.IsMatch(evidence,"(?im)^reachability\\s+.*(?:unreachable|failed)")){findings.Add("Codex 的更新服务器或运行时下载 CDN 无法访问，可能影响更新和下载组件。");suggestions.Add("优先检查代理 / VPN 节点、防火墙、DNS 和路由是否允许访问更新与下载服务器。代理端口或 IP 改动后，点击“更新代理”；切换 CLI 本身不能修复网络。");recognized=true;}
            if(Regex.IsMatch(evidence,"(?im)^websocket\\s+.*(?:failed|unreachable)")){findings.Add("WebSocket 连接失败，但 HTTPS 回退可能仍可用；不能据此认定 Codex 完全无法联网。");suggestions.Add("确认代理或网关支持 WebSocket。可换一个节点后重跑诊断，检查更新服务器和 WebSocket 是否都能连通。");recognized=true;}
            if(Regex.IsMatch(evidence,"(?im)^mcp\\s+.*(?:issues|missing|failed)")){findings.Add("部分 MCP 工具配置有问题；这份报告提示需要补充环境变量或停用相关服务。");suggestions.Add("补齐相关 MCP 服务要求的环境变量，或停用不使用的服务。摘要没有给出服务名时，需要完整 Doctor 报告定位具体项目。");recognized=true;}
            if(Regex.IsMatch(evidence,"(?im)^threads\\s+.*(?:issues|missing|duplicate)")){findings.Add("会话记录与数据库索引的对应关系有异常，存在缺失记录或重复条目；这不等于整个数据库损坏。");suggestions.Add("先备份会话记录和数据库，再查看完整 Doctor 报告中的具体条目。不要直接删除整个 sessions 目录或数据库，也无需仅因此重新初始化。");recognized=true;}
            if(!recognized)findings.Add("诊断输出中有 "+warnings.Length+" 项需要留意的提示。");
            if(!Regex.IsMatch(evidence,"(?im)^(reachability|websocket)\\s+")&&Regex.IsMatch(evidence,"proxy|connection refused|connect.*timed out|network.*(?:fail|error)|代理|连接.*(?:失败|超时|拒绝)",RegexOptions.IgnoreCase))suggestions.Add("检查代理软件或软路由是否开启，以及代理端口和 IP 是否正确。到初始化页面修改后，在维护恢复中点击“更新代理”。");
            if(Regex.IsMatch(evidence,"unauthorized|not authenticated|login required|authentication.*(?:fail|expired)|登录.*(?:失效|过期)|未登录",RegexOptions.IgnoreCase))suggestions.Add("打开 Codex，重新登录账号后再试；切换 CLI 不会替你完成账号登录。");
            if(Regex.IsMatch(evidence,"model.*(?:not available|not found|not supported|access denied)|模型.*(?:不可用|无权限|不支持)",RegexOptions.IgnoreCase))suggestions.Add("换用账号可以使用的模型。预设模型列表不代表账号都有权限，重装 CLI 不会增加模型权限。");
            if(Regex.IsMatch(evidence,"access is denied|permission denied|权限不足|拒绝访问",RegexOptions.IgnoreCase))suggestions.Add("检查提示涉及的文件夹是否可访问，以及安全软件是否拦截了程序；不要直接删除配置或备份。");
            if(Regex.IsMatch(evidence,"disk.*full|no space left|磁盘.*(?:满|不足)",RegexOptions.IgnoreCase))suggestions.Add("清理对应磁盘的空间后重试，保留 Codex 配置和初始化备份。");
            if(Regex.IsMatch(evidence,"cua|browser.*(?:missing|corrupt)",RegexOptions.IgnoreCase))suggestions.Add("在下方 CUA 组件栏点击“检查 CUA”，发现异常后再点击“检查并修复”。");
        }
        if(rows.Any(x=>Regex.IsMatch(x,"^updates\\s+update configuration is locally consistent",RegexOptions.IgnoreCase)))findings.Add("更新配置在本地是一致的；这与更新服务器是否能连通是两项不同检查。");
        if(rows.Any(x=>Regex.IsMatch(x,"^rollouts\\s+\\d+ active files",RegexOptions.IgnoreCase)))findings.Add("会话记录的文件数和容量属于统计提示，不能单凭这项判断文件损坏。");
        if(exitCode!=0){findings.Add(CompletedReport(cleanOutput)?"Doctor 已完成检查并报告失败项（退出码 "+exitCode+"），不是诊断助手启动失败。":"本次诊断未正常完成（退出码 "+exitCode+"）。");if(suggestions.Count==0)suggestions.Add("先查看详细日志的最后几行。原因尚未确认，建议复制日志用于继续排查，避免反复初始化。");}
        if(findings.Count==0)findings.Add("诊断已运行完成，没有识别到明确的故障提示。此结果不代表所有功能都正常。");
        if(suggestions.Count==0)suggestions.Add(warnings.Length>0?"有提示，但暂时无法可靠判断原因。打开详细日志查看具体项目，可复制日志继续排查。":"如果仍然无法启动或使用，请打开详细日志并复制具体错误，按实际问题继续排查。");
        return "检查结果\n"+string.Join("\n",findings.Select(x=>"• "+x))+"\n\n建议怎么做\n"+string.Join("\n",suggestions.Distinct().Select((x,i)=>(i+1)+". "+x));
    }
    public static void Verify(){
        var missing=Build(new Dictionary<string,object>{{"standaloneHealthy",false}},1,"");if(!missing.Contains("安装 / 修复保护"))throw new InvalidOperationException("Missing CLI advice was lost");
        var network=Build(null,0,"[WARN] proxy connection refused");if(!network.Contains("更新代理"))throw new InvalidOperationException("Network advice was lost");
        var clean=Build(null,0,"No errors. Proxy is healthy. Model access is available.");if(clean.Contains("更新代理")||clean.Contains("换用账号"))throw new InvalidOperationException("Healthy diagnostics were classified as errors");
        var failed=Build(null,1,"unrecognized failure");if(!failed.Contains("未正常完成")||failed.Contains("没有识别到明确"))throw new InvalidOperationException("Failed diagnostics were described as passed");
        if(!Build(null,0,"[WARN] model not available").Contains("模型权限"))throw new InvalidOperationException("Model access advice was lost");
        var ascii=Build(null,1,AsciiFixture);foreach(var required in new[]{"CDN","HTTPS 回退","MCP","数据库索引","本地是一致","统计提示","已完成检查"})if(!ascii.Contains(required))throw new InvalidOperationException("Doctor ASCII finding was lost: "+required);if(ascii.Contains("本次诊断未正常完成")||ascii.Split(new[]{"数据库索引"},StringSplitOptions.None).Length!=2)throw new InvalidOperationException("Completed diagnostics or repeated rows were misclassified");
        var notes=Build(null,0,"[!!] updates update configuration is locally consistent\n[!!] rollouts 176 active files · 1.97 GB on disk\n[--] app-server not running (ephemeral mode)");if(notes.Contains("更新代理")||notes.Contains("重新登录")||notes.Contains("无法可靠判断原因"))throw new InvalidOperationException("Informational Doctor rows were classified as faults");
    }
}
}
