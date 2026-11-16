using File=CodexKit.Native.NativeFile;
using Directory=CodexKit.Native.NativeDirectory;
using FileInfo=CodexKit.Native.NativeFileInfo;
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text.RegularExpressions;using System.IO.Compression;
namespace CodexKit.Native {
public sealed class NativeRtk {
    public const string ZipName="rtk-x86_64-pc-windows-msvc.zip";
    const string Release="https://github.com/rtk-ai/rtk/releases/latest/download/";
    readonly NativePaths paths;readonly NativeServices services;
    public int TimeoutMs=60000;
    public NativeRtk(NativePaths p,NativeServices s){paths=p;services=s;}
    Dictionary<string,string> EnvironmentForRun(){return new Dictionary<string,string>{{"CODEX_HOME",paths.Home},{"RTK_TELEMETRY_DISABLED","1"}};}
    public static void VerifyZip(string zip,string checksums){string expected=null;foreach(var line in checksums.Split('\n')){var m=Regex.Match(line,@"^([0-9a-fA-F]{64})\s+\*?"+Regex.Escape(ZipName)+@"\s*$");if(m.Success){expected=m.Groups[1].Value;break;}}if(expected==null)throw new InvalidDataException("RTK 校验清单缺少目标 ZIP。");if(!NativeCua.Hash(zip).Equals(expected,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("RTK ZIP SHA-256 不一致，未安装。");}
    public string Find(){var candidates=new[]{paths.Rtk}.Concat((paths.SearchPath??"").Split(';').Where(d=>!String.IsNullOrWhiteSpace(d)).Select(d=>Path.Combine(d.Trim('"'),"rtk.exe"))).Distinct(StringComparer.OrdinalIgnoreCase);return candidates.FirstOrDefault(File.Exists);}
    NativeCommandResult Run(string exe,string[] args,string log){var r=services.Commands.Run(exe,args,EnvironmentForRun(),TimeoutMs);NativeData.Write(log,(r.Output??"")+"\n"+(r.Error??""));if(r.TimedOut)throw new TimeoutException("RTK "+String.Join(" ",args)+" 超时，已结束本次组件进程。");if(r.ExitCode!=0)throw new IOException("RTK "+String.Join(" ",args)+" 未完成，退出码："+r.ExitCode+"；日志："+log);return r;}
    public string Install(string session){
        var stage=Path.Combine(session,"RtkDownload");Directory.CreateDirectory(stage);var sums=services.Downloads.Text(Release+"checksums.txt",s=>s.Contains(ZipName));var zip=Path.Combine(stage,ZipName);services.Downloads.File(Release+ZipName,zip,p=>VerifyZip(p,sums));VerifyZip(zip,sums);
        var extract=Path.Combine(stage,"extracted");Directory.CreateDirectory(extract);var candidate=Path.Combine(extract,"rtk.exe");
        using(var stream=File.OpenRead(zip))using(var archive=new ZipArchive(stream,ZipArchiveMode.Read)){var entries=archive.Entries.Where(e=>String.Equals(Path.GetFileName(e.FullName.Replace('/',Path.DirectorySeparatorChar)),"rtk.exe",StringComparison.OrdinalIgnoreCase)&&!e.FullName.EndsWith("/")).ToArray();if(entries.Length!=1)throw new InvalidDataException("RTK ZIP 中必须只有一个 rtk.exe。");using(var input=entries[0].Open())using(var output=new System.IO.FileStream(NativePath.Io(candidate),FileMode.CreateNew,FileAccess.Write,FileShare.None))input.CopyTo(output);}

        var version=Run(candidate,new[]{"--version"},Path.Combine(session,"rtk-candidate-version.log"));if(!Regex.IsMatch(version.Output??"",@"(?im)^rtk(?:\.exe)?\s+\d+\.\d+"))throw new InvalidDataException("RTK 安装包未返回可识别版本，未替换原程序。");NativeData.WriteBytes(paths.Rtk,File.ReadAllBytes(candidate));return paths.Rtk;
    }
    public static string[] PlannedFiles(string output,NativePaths paths,string inspection=null){
        var planned=new List<string>();foreach(Match m in Regex.Matches(NativeData.Normalize(output??""),@"(?m)^\[dry-run\] would .+: (?<path>[^\r\n]+)$")){var path=Path.GetFullPath(m.Groups["path"].Value.Trim());if(!new[]{paths.RtkDoc,paths.Agents,paths.Hooks}.Any(p=>String.Equals(Path.GetFullPath(p),path,StringComparison.OrdinalIgnoreCase)))throw new InvalidDataException("RTK 计划写入未纳入备份的文件，已停止集成："+path);planned.Add(path);}
        foreach(Match m in Regex.Matches(output??"",@"would add @(?<doc>.+?) reference to AGENTS\.md:"))if(!String.Equals(Path.GetFullPath(m.Groups["doc"].Value),Path.GetFullPath(paths.RtkDoc),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("RTK 计划引用其他位置的规则文档，尚未执行集成。");
        var show=NativeData.Normalize(inspection??"");var global=Regex.Match(show,@"(?m)^\[ok\] Global RTK\.md:\s*(?<path>[^\r\n]+)$");bool scope=global.Success&&String.Equals(Path.GetFullPath(global.Groups["path"].Value.Trim()),Path.GetFullPath(paths.RtkDoc),StringComparison.OrdinalIgnoreCase);
        if(!planned.Contains(paths.RtkDoc,StringComparer.OrdinalIgnoreCase)&&scope&&File.Exists(paths.RtkDoc)&&!String.IsNullOrWhiteSpace(NativeData.Read(paths.RtkDoc)))planned.Add(paths.RtkDoc);
        if(!planned.Contains(paths.Agents,StringComparer.OrdinalIgnoreCase)&&scope&&Regex.IsMatch(show,@"(?m)^\[ok\] Global AGENTS\.md(?:\s*:|\s*$)")&&File.Exists(paths.Agents)&&!String.IsNullOrWhiteSpace(NativeData.Read(paths.Agents)))planned.Add(paths.Agents);
        if(!planned.Contains(paths.RtkDoc,StringComparer.OrdinalIgnoreCase)||!planned.Contains(paths.Agents,StringComparer.OrdinalIgnoreCase))throw new InvalidDataException("RTK 预检无法确认本次 CODEX_HOME 的规则目标；已有文档须通过全局位置检查，尚未执行集成。");return planned.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public string Prepare(string session){
        var exe=Find();if(exe==null)exe=Install(session);
        var version=Run(exe,new[]{"--version"},Path.Combine(session,"rtk-version.log"));if(!Regex.IsMatch(version.Output??"",@"(?im)^rtk(?:\.exe)?\s+\d+\.\d+"))throw new InvalidDataException("RTK 未返回可识别版本；可能未成功执行，已停止集成。");
        if(String.Equals(exe,paths.Rtk,StringComparison.OrdinalIgnoreCase)){var previous=services.Registry.Get("Path");var old=previous.Exists?Convert.ToString(previous.Value):"";if(!old.Split(';').Any(x=>String.Equals(x.TrimEnd('\\','/'),paths.UserTools.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase)))services.Registry.Set("Path",new RegistryValueState{Exists=true,Kind=previous.Exists?previous.Kind:"ExpandString",Value=old.Length==0?paths.UserTools:old+";"+paths.UserTools});}
        Run(exe,new[]{"telemetry","disable"},Path.Combine(session,"rtk-telemetry.log"));
        var dryRun=Run(exe,new[]{"init","--dry-run","--global","--codex"},Path.Combine(session,"rtk-init-dry-run.log"));
        var existing=Run(exe,new[]{"init","--show","--codex"},Path.Combine(session,"rtk-init-before.log"));
        var planned=PlannedFiles(dryRun.Output,paths,existing.Output);
        Run(exe,new[]{"init","--global","--codex"},Path.Combine(session,"rtk-init.log"));
        var missing=planned.Where(path=>!File.Exists(path)).ToArray();if(missing.Length>0)throw new InvalidDataException("RTK 未生成预检声明的文件："+String.Join("、",missing.Select(Path.GetFileName))+"；不能判为集成成功。");
        if(!File.Exists(paths.RtkDoc)||String.IsNullOrWhiteSpace(NativeData.Read(paths.RtkDoc)))throw new InvalidDataException("RTK 未生成实际 RTK.md，不能判为集成成功。");
        var show=Run(exe,new[]{"init","--show","--codex"},Path.Combine(session,"rtk-init-show.log"));if((show.Output??"").IndexOf(paths.RtkDoc,StringComparison.OrdinalIgnoreCase)<0)throw new InvalidDataException("RTK 集成位置未指向本次 CODEX_HOME，停止后续写入。");services.Log("RTK 已验证并完成无交互集成；本次统计已关闭。");return exe;
    }
}
}
