using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using CodexKit.Native;

namespace CodexKit {
internal sealed class DesktopProfileRecord {
    public string path,capturedUtc,packageFamily;
    public string[] processes;
    public bool identityPresent;
    public long identityCreated,identityModified;
}

internal static class DesktopProfile {
    internal const string IdentityFile=".codex-tpp-local-executor-installation-id";
    internal const string PackageFamily="OpenAI.Codex_2p2nqsd0c76g0";
    internal static string RecordPath(NativePaths paths){return Path.Combine(paths.LocalData,"CodexInitKit","Desktop","desktop-profile.json");}
    internal static InvalidDataException Issue(string message,string detail=""){
        var error=new InvalidDataException(message);error.Data["profileMessage"]=message;error.Data["profileDetail"]=detail;return error;
    }
    internal static void ReportFailure(Exception error,Action<string> log){
        if(!error.Data.Contains("profileMessage"))return;
        log("资料识别详情："+Convert.ToString(error.Data["profileDetail"]));
        log("@@PROFILE_ERROR@@"+Core.Json.Serialize(new{message=Convert.ToString(error.Data["profileMessage"])}));
    }
    internal static string FailureMessage(string log,string fallback){
        var line=log.Split('\n').LastOrDefault(x=>x.StartsWith("@@PROFILE_ERROR@@"));
        return line==null?fallback:Core.Value(Core.ReadJson(line.Substring(17).Trim()),"message",fallback);
    }
    internal static bool SameIdentity(DesktopProfileRecord a,DesktopProfileRecord b){
        return a!=null&&b!=null&&String.Equals(a.path,b.path,StringComparison.OrdinalIgnoreCase)&&a.identityPresent==b.identityPresent&&a.identityCreated==b.identityCreated&&a.identityModified==b.identityModified;
    }
    internal static DesktopProfileRecord Select(NativePaths paths,DesktopProfileRecord current,bool restoreOriginal,Action<string> log){
        if(restoreOriginal){
            try{return Load(paths,DateTime.UtcNow);}
            catch(InvalidDataException ex){throw Issue("还没有可确认的原连接记录。请保存工作后，从平时使用的图标重新打开 Codex；确认原连接上线，再点“沿用当前 Codex”。",ex.Message);}
        }
        if(current==null)return Load(paths,DateTime.UtcNow);
        DesktopProfileRecord previous=null;
        if(File.Exists(RecordPath(paths))){
            try{previous=Load(paths,DateTime.UtcNow);}
            catch(InvalidDataException ex){log("旧观测不可用，依据当前运行实例继续："+ex.Message);}
        }
        if(previous!=null&&!SameIdentity(previous,current))
            throw Issue("当前打开的 Codex 与上次使用的不一致。请在“连接与修复”点击“恢复原来的 Codex”。","saved="+previous.path+"; current="+current.path);
        return current;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CommandLineToArgvW(string line,out int count);
    [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr memory);

    internal static string DirectoryArgument(string line){
        if(String.IsNullOrWhiteSpace(line))return null;
        int count;var memory=CommandLineToArgvW(line,out count);
        if(memory==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"无法读取桌面资料目录参数。");
        try{
            var paths=new List<string>();
            for(int i=1;i<count;i++){
                var value=Marshal.PtrToStringUni(Marshal.ReadIntPtr(memory,i*IntPtr.Size));
                if(value=="--user-data-dir"){if(++i>=count)throw Issue("暂时无法识别当前 Codex。请等窗口加载完成后重试。","user-data-dir missing");value=Marshal.PtrToStringUni(Marshal.ReadIntPtr(memory,i*IntPtr.Size));}
                else if(value.StartsWith("--user-data-dir=",StringComparison.Ordinal))value=value.Substring(16);
                else continue;
                if(String.IsNullOrWhiteSpace(value)||!Path.IsPathRooted(value))throw Issue("暂时无法识别当前 Codex。请等窗口加载完成后重试。","user-data-dir is not absolute");
                var full=Path.GetFullPath(value);
                paths.Add(full==Path.GetPathRoot(full)?full:full.TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));
            }
            var distinct=paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(distinct.Length>1)throw Issue("暂时无法识别当前 Codex。请等窗口加载完成后重试。","conflicting process arguments");
            return distinct.SingleOrDefault();
        }finally{LocalFree(memory);}
    }
    static bool IsDesktop(ProcessIdentity process,OfficialDesktop app){
        if(app==null)return false;
        return WindowsNativeProcesses.IsDesktopExecutable(process.Path)||new[]{"ChatGPT","Codex"}.Any(name=>String.Equals(process.Name,name,StringComparison.OrdinalIgnoreCase)&&
            String.Equals(process.Path,Path.Combine(app.Install,"app",name+".exe"),StringComparison.OrdinalIgnoreCase));
    }
    internal static DesktopProfileRecord Capture(IEnumerable<ProcessIdentity> running,OfficialDesktop app,DateTime now){
        var desktops=running.Where(p=>IsDesktop(p,app)).ToDictionary(p=>p.Id);
        if(desktops.Count==0){
            if(running.Any(p=>String.Equals(p.Name,"ChatGPT",StringComparison.OrdinalIgnoreCase)))
                throw Issue("暂时无法识别当前 Codex。请等窗口加载完成后重试。","unrecognized desktop executable");
            return null;
        }
        var roots=desktops.Values.Where(p=>!desktops.ContainsKey(p.ParentId)).ToArray();
        var directories=new List<string>();
        foreach(var root in roots){
            var instance=desktops.Values.Where(p=>{
                var current=p;var visited=new HashSet<int>();
                while(visited.Add(current.Id)){if(current.Id==root.Id)return true;ProcessIdentity parent;if(!desktops.TryGetValue(current.ParentId,out parent))return false;current=parent;}
                return false;
            }).ToArray();
            // 子进程参数反映运行时采用的目录；主进程参数只是启动请求。
            var actual=instance.Where(p=>p.Id!=root.Id).Select(p=>DirectoryArgument(p.CommandLine)).Where(p=>p!=null).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if(actual.Length!=1)throw Issue("暂时无法识别当前 Codex。请等窗口加载完成后重试。","PID "+root.Id+" has "+actual.Length+" observed profiles");
            var requested=DirectoryArgument(root.CommandLine);
            if(requested!=null&&!String.Equals(requested,actual[0],StringComparison.OrdinalIgnoreCase))throw Issue("Codex 的启动状态与预期不一致。请保存工作，从平时使用的图标重新打开后重试。","requested="+requested+"; actual="+actual[0]);
            directories.Add(actual[0]);
        }
        var unique=directories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if(unique.Length!=1)throw Issue("检测到多组独立运行的 Codex。请先保存工作，只保留平时使用的那一组，再重试。",String.Join("；",unique));
        if(!Directory.Exists(unique[0]))throw Issue("当前 Codex 的资料暂时不可用。请从平时使用的图标打开后重试。",unique[0]);
        var identity=new FileInfo(Path.Combine(unique[0],IdentityFile));
        return new DesktopProfileRecord{path=unique[0],capturedUtc=now.ToUniversalTime().ToString("o"),packageFamily=PackageFamily,processes=roots.Select(p=>p.Key).OrderBy(p=>p).ToArray(),identityPresent=identity.Exists,identityCreated=identity.Exists?identity.CreationTimeUtc.Ticks:0,identityModified=identity.Exists?identity.LastWriteTimeUtc.Ticks:0};
    }
    internal static DesktopProfileRecord Parse(string json){
        if(String.IsNullOrWhiteSpace(json))return null;
        try{return Core.Json.Deserialize<DesktopProfileRecord>(json);}
        catch(ArgumentException ex){throw Issue("无法确认之前使用的 Codex。请从平时使用的图标打开，再回到工具重试。",ex.Message);}
        catch(InvalidOperationException ex){throw Issue("无法确认之前使用的 Codex。请从平时使用的图标打开，再回到工具重试。",ex.Message);}
    }
    internal static DesktopProfileRecord FromReport(Dictionary<string,object> report){
        object value;return report.TryGetValue("desktopProfile",out value)&&value!=null?Parse(Core.Json.Serialize(value)):null;
    }
    internal static void Validate(DesktopProfileRecord record,DateTime now){
        DateTime captured;
        if(record==null||record.packageFamily!=PackageFamily||String.IsNullOrWhiteSpace(record.path)||!Path.IsPathRooted(record.path)||record.processes==null||record.processes.Length==0||
            !DateTime.TryParse(record.capturedUtc,null,System.Globalization.DateTimeStyles.RoundtripKind,out captured))
            throw Issue("无法确认之前使用的 Codex。请从平时使用的图标打开，再回到工具重试。","invalid observation");
        var age=now.ToUniversalTime()-captured.ToUniversalTime();
        if(age<TimeSpan.Zero)throw Issue("无法确认之前使用的 Codex。请从平时使用的图标打开，再回到工具重试。","observation is in the future");
        if(!Directory.Exists(record.path))throw Issue("之前使用的 Codex 资料暂时不可用。请从平时使用的图标打开后重试。",record.path);
        var identity=new FileInfo(Path.Combine(record.path,IdentityFile));
        if(identity.Exists!=record.identityPresent||identity.Exists&&(identity.CreationTimeUtc.Ticks!=record.identityCreated||identity.LastWriteTimeUtc.Ticks!=record.identityModified))
            throw Issue("之前的连接状态已有变化。请从平时使用的 Codex 图标打开，再回到工具重试。","executor identity metadata changed: "+record.path);
    }
    internal static DesktopProfileRecord Load(NativePaths paths,DateTime now){
        var file=RecordPath(paths);
        if(!File.Exists(file))throw Issue("还无法确认之前使用的 Codex。请从平时使用的图标打开，再回到工具重试。","no saved observation");
        var record=Parse(File.ReadAllText(file,Encoding.UTF8));Validate(record,now);return record;
    }
    internal static void Save(NativePaths paths,DesktopProfileRecord record){
        var file=RecordPath(paths);Directory.CreateDirectory(Path.GetDirectoryName(file));
        File.WriteAllText(file,Core.Json.Serialize(record),new UTF8Encoding(false));
    }
    internal static void RequireSame(DesktopProfileRecord expected,DesktopProfileRecord actual,bool sameProcesses){
        if(expected==null||actual==null||!String.Equals(expected.path,actual.path,StringComparison.OrdinalIgnoreCase)||
            expected.identityPresent!=actual.identityPresent||expected.identityCreated!=actual.identityCreated||expected.identityModified!=actual.identityModified||
            sameProcesses&&!expected.processes.SequenceEqual(actual.processes))
            throw Issue("Codex 的运行状态已变化。请点击重试，重新检查。","profile or process differs from confirmation");
    }
    internal static DesktopProfileRecord PreserveBeforeClose(NativePaths paths,NativeServices services,OfficialDesktop app,NativeOptions options,Action<string> log){
        var running=services.Processes.List(paths,app);
        NativeProcessWorkflow.AssertAuthorized(running,new HashSet<string>(options.ApprovedProcesses??new string[0]));
        var actual=Capture(running,app,DateTime.UtcNow);var expected=Parse(options.SavedDesktopProfile);
        if(options.RestoreDesktopProfile){
            var previous=Load(paths,DateTime.UtcNow);RequireSame(expected,previous,false);
            if(actual!=null){var source=Parse(options.ObservedDesktopProfile);Validate(source,DateTime.UtcNow);RequireSame(source,actual,true);}
            log("恢复已观测的原资料目录："+previous.path);return previous;
        }
        if(actual!=null)Select(paths,actual,false,log);
        if(actual!=null){Validate(expected,DateTime.UtcNow);RequireSame(expected,actual,true);}
        else if(expected!=null){Validate(expected,DateTime.UtcNow);actual=expected;}
        if(actual!=null){Save(paths,actual);log("已保存当前桌面资料目录："+actual.path+"；后续启动沿用此目录。");}
        return actual;
    }
    internal static string Description(DesktopProfileRecord record,bool running){
        return running?"已识别正在使用的 Codex，本次将继续沿用现有资料和连接设置。":"将继续使用之前的 Codex，资料和连接设置保持一致。";
    }
    internal static void Apply(ProcessStartInfo start,DesktopProfileRecord record){
        Validate(record,DateTime.UtcNow);
        start.Arguments="--user-data-dir="+NativeData.Quoted(record.path);
        start.EnvironmentVariables["CODEX_ELECTRON_USER_DATA_PATH"]=record.path;
    }
    internal static async System.Threading.Tasks.Task<DesktopProfileRecord> ObserveStarted(DesktopProfileRecord expected,Func<DesktopProfileRecord> observe,int attempts=60){
        string detail="尚未取得桌面子进程的资料目录参数。";
        for(int attempt=0;attempt<attempts;attempt++){
            DesktopProfileRecord actual=null;
            try{actual=observe();}catch(InvalidDataException ex){detail=ex.Message;}
            if(actual!=null){
                if(!String.Equals(expected.path,actual.path,StringComparison.OrdinalIgnoreCase)||expected.identityPresent&&
                    (!actual.identityPresent||expected.identityCreated!=actual.identityCreated||expected.identityModified!=actual.identityModified))
                    throw Issue("Codex 已打开，但未能确认它继续使用原来的资料和连接。请先保留现状，查看日志后再处理。","expected="+expected.path+"; actual="+actual.path);
                return actual;
            }
            await System.Threading.Tasks.Task.Delay(250).ConfigureAwait(false);
        }
        throw Issue("Codex 的启动结果还无法确认。请查看窗口是否已经打开，先不要重复点击启动。",detail);
    }
}
}
