using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace CodexKit {
public static class DiagnosticAdvice {
    static bool Is(Dictionary<string,object> context,string key,bool expected){object value;return context!=null&&context.TryGetValue(key,out value)&&value is bool&&(bool)value==expected;}
    public static string Build(Dictionary<string,object> context,int exitCode,string output){
        var suggestions=new List<string>();var findings=new List<string>();
        if(Is(context,"standaloneHealthy",false)){findings.Add("官方独立 CLI 的组件缺失、校验异常或无法运行，诊断助手可能无法启动。");suggestions.Add("先退出 Codex，点击“安装 / 修复保护”，完成后再运行诊断助手。");}
        if(Is(context,"desktopFound",false)){findings.Add("未找到当前用户安装的 Codex 桌面应用。");suggestions.Add("确认已安装 Codex 桌面应用，并使用安装它的 Windows 账号运行本工具。");}
        else if(Is(context,"desktopHealthy",false)){findings.Add("Codex 桌面应用自带的 CLI 组件不完整。");suggestions.Add("先点击“安装 / 修复保护”准备备用组件；若仍需独立 CLI，可点击“切换官方独立 CLI”。");}
        var warnings=Regex.Matches(output??"","(?im)^\\s*(?:\\[(?:fail|error|warn|warning)\\]|(?:fail|error|warn|warning)\\s*[:：]|[✗×❌⚠])\\s*(.+)$").Cast<Match>().Select(x=>x.Groups[1].Value.Trim()).Distinct().Take(8).ToArray();
        if(warnings.Length>0){findings.Add("诊断输出中有 "+warnings.Length+" 项需要留意的提示。");var evidence=string.Join("\n",warnings);
            if(Regex.IsMatch(evidence,"proxy|connection refused|connect.*timed out|network.*(?:fail|error)|代理|连接.*(?:失败|超时|拒绝)",RegexOptions.IgnoreCase))suggestions.Add("检查代理软件或软路由是否开启，以及代理端口和 IP 是否正确。到初始化页面修改后，在维护恢复中点击“更新代理”。");
            if(Regex.IsMatch(evidence,"unauthorized|not authenticated|login required|authentication.*(?:fail|expired)|登录.*(?:失效|过期)|未登录",RegexOptions.IgnoreCase))suggestions.Add("打开 Codex，重新登录账号后再试；切换 CLI 不会替你完成账号登录。");
            if(Regex.IsMatch(evidence,"model.*(?:not available|not found|not supported|access denied)|模型.*(?:不可用|无权限|不支持)",RegexOptions.IgnoreCase))suggestions.Add("换用账号可以使用的模型。预设模型列表不代表账号都有权限，重装 CLI 不会增加模型权限。");
            if(Regex.IsMatch(evidence,"access is denied|permission denied|权限不足|拒绝访问",RegexOptions.IgnoreCase))suggestions.Add("检查提示涉及的文件夹是否可访问，以及安全软件是否拦截了程序；不要直接删除配置或备份。");
            if(Regex.IsMatch(evidence,"disk.*full|no space left|磁盘.*(?:满|不足)",RegexOptions.IgnoreCase))suggestions.Add("清理对应磁盘的空间后重试，保留 Codex 配置和初始化备份。");
            if(Regex.IsMatch(evidence,"cua|browser.*(?:missing|corrupt)",RegexOptions.IgnoreCase))suggestions.Add("在下方 CUA 组件栏点击“检查 CUA”，发现异常后再点击“检查并修复”。");
        }
        if(exitCode!=0){findings.Add("本次诊断未正常完成（退出码 "+exitCode+"）。");if(suggestions.Count==0)suggestions.Add("先查看详细日志的最后几行。原因尚未确认，建议复制日志用于继续排查，避免反复初始化。");}
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
    }
}
}
