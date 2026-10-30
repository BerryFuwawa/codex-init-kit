using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace CodexKit {
public static class DesktopLocalizationTests {
    static void Assert(bool value,string message){if(!value)throw new InvalidDataException(message);}
    public static void Run(string payload) {
        var cases=new[]{
            new[]{"[FAIL] Codex is running. Save work and fully exit before continuing.","保存工作并彻底退出"},
            new[]{"[FAIL] Unsupported desktop executable cache layout; no files were changed.","不支持当前桌面启动组件目录结构"},
            new[]{"[BROWSER INCOMPLETE] Bundled Node does not advertise environment proxy support.","内置 Node"},
            new[]{"[INCOMPLETE] 更新桌面版：Desktop update requires restart or is still pending; dependent repairs were paused.","需要重启或尚未完成"},
            new[]{"Runtime has third-party changes or a hash mismatch; refusing overwrite.","拒绝覆盖"},
            new[]{"[FAIL] Missing file: 文件夹\\中文 file.exe","中文 file.exe"},
            new[]{"Directory missing: D:\\My Project\\中文目录", "D:\\My Project\\中文目录"},
            new[]{"File count differs: expected 123, actual 12.","应为 123，实际为 12"},
            new[]{"XCOPY failed. Exit code: 4. Log: D:\\中文日志\\copy.log","退出码：4"},
            new[]{"init.cmd failed with exit code 17","退出码：17"},
            new[]{"No initialization backup session is available for rollback.","没有可用于回滚"},
            new[]{"Option parentModel contains an invalid model id.","parentModel 的模型 ID 无效"},
            new[]{"The operation has timed out","操作超时"},
            new[]{"The remote server returned an error: (403) Forbidden.","HTTP 状态码 403"}
        };
        foreach(var c in cases){var actual=DesktopLocalization.TranslateLine(c[0],true);Assert(actual.Contains(c[1]),"Chinese diagnostic lost its meaning: "+c[0]+" => "+actual);}
        var unknown="[FAIL] Unforeseen vendor crash with unfamiliar details 0x80070005 D:\\My Project\\用户日志.txt";
        var translated=DesktopLocalization.TranslateLine(unknown,true);
        Assert(translated.Contains("原因尚未识别")&&translated.Contains("0x80070005")&&translated.Contains("D:\\My Project\\用户日志.txt")&&!translated.Contains("Unforeseen"),"Unknown error was guessed or its context was lost");
        Assert(DesktopLocalization.TranslateLine("[FAIL] UnknownVendorError",true).Contains("原因尚未识别"),"Single-token foreign errors bypassed fallback");
        Assert(DesktopLocalization.TranslateLine("CategoryInfo : ObjectNotFound").Contains("原始日志")&&DesktopLocalization.TranslateLine("#< CLIXML").Contains("原始日志"),"PowerShell exception metadata leaked into the Chinese view");
        var chinese="当前模型：gpt-6.1-sol / high；文件：D:\\My Project\\中文.txt";
        Assert(DesktopLocalization.TranslateText(chinese)==chinese,"Chinese text or identifiers were modified");
        var protectedPath="D:\\My Project\\Invalid commit\\Unauthorized file.exe";
        Assert(DesktopLocalization.TranslateLine("Missing file: "+protectedPath,true).Contains(protectedPath),"Message translation modified a filename");
        Assert(DesktopLocalization.ExceptionText(new Win32Exception(5)).Contains("权限"),"Windows permission error was not localized");
        Assert(DesktopLocalization.ExceptionText(new Win32Exception(32)).Contains("占用"),"File sharing error was not localized");
        Assert(DesktopLocalization.ExceptionText(new WebException("foreign timeout",WebExceptionStatus.Timeout)).Contains("超时"),"Network error type was ignored");
        var raw=DiagnosticAdvice.AsciiFixture;
        Assert(DiagnosticAdvice.Build(null,1,raw).Contains("HTTPS 回退")&&DiagnosticAdvice.CompletedReport(raw),"Raw Doctor semantics were lost");
        var rawPath=Path.Combine(payload,"localization-fixture.log");File.WriteAllText(rawPath,unknown,new UTF8Encoding(false));
        DesktopLocalization.SaveChineseLog(rawPath,translated);
        Assert(File.ReadAllText(rawPath,Encoding.UTF8)==unknown&&File.ReadAllText(DesktopLocalization.ChineseLogPath(rawPath),Encoding.UTF8)==translated,"Localized logging modified the raw evidence");
        AuditOwnedErrors(payload);
    }
    static void AuditOwnedErrors(string root) {
        var sources=new List<string>();
        foreach(var name in new[]{"Backend.ps1","Backend220.ps1","DesktopCoreRecovery.ps1","BrowserStartup.ps1","cua.cmd"})sources.Add(File.ReadAllText(Path.Combine(root,name),Encoding.UTF8));
        var init=File.ReadAllText(Path.Combine(root,"init.cmd"),Encoding.GetEncoding(936));
        foreach(var prefix in new[]{"PMB64","KITB64"}){
            var b64=string.Join("",Regex.Matches(init,"(?m)^::"+prefix+":([A-Za-z0-9+/=]+)\\r?$").Cast<Match>().Select(m=>m.Groups[1].Value));
            if(b64.Length>0)sources.Add(Encoding.UTF8.GetString(Convert.FromBase64String(b64)));
        }
        var guard=File.ReadAllText(Path.Combine(root,"guard.cmd"),Encoding.UTF8);var match=Regex.Match(guard,@"###CODEX_GUARD_PS_B64###\s*([A-Za-z0-9+/=\s]+)###CODEX_GUARD_PS_B64_END###");
        if(match.Success)sources.Add(Encoding.UTF8.GetString(Convert.FromBase64String(Regex.Replace(match.Groups[1].Value,@"\s",""))));
        var uncovered=new SortedSet<string>();
        foreach(var source in sources)foreach(Match m in Regex.Matches(source,@"throw\s+(?:\(\s*)?(?:'([^'\r\n]+)'|""([^""\r\n]+)"")")){
            var value=m.Groups[1].Success?m.Groups[1].Value:m.Groups[2].Value;if(m.Groups[2].Success&&value.Contains("$"))continue;
            if(value=="Option "||!Regex.IsMatch(value,@"[A-Za-z]{2,} [A-Za-z]{2,}"))continue;
            var local=DesktopLocalization.TranslateLine(value,true);
            if(local.Contains("暂无中文说明")||local.Contains("原因尚未识别")||!Regex.IsMatch(local,@"[\u4e00-\u9fff]"))uncovered.Add(value);
        }
        Assert(uncovered.Count==0,"Missing specific Chinese messages:\n"+string.Join("\n",uncovered));
    }
}
}
