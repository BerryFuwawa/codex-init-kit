using File=CodexKit.Native.NativeFile;
using Directory=CodexKit.Native.NativeDirectory;
using FileInfo=CodexKit.Native.NativeFileInfo;
using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.Diagnostics;using System.Threading;using System.Runtime.InteropServices;using System.ComponentModel;using System.Security.Principal;using System.Management;using Microsoft.Win32;using System.Text.RegularExpressions;
namespace CodexKit.Native {
public sealed class WindowsRegistryStore:INativeRegistry {
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr parameter,string data,uint flags,uint timeout,out IntPtr result);
    static RegistryValueState Read(RegistryKey key,string name){if(key==null||!key.GetValueNames().Contains(name,StringComparer.OrdinalIgnoreCase))return new RegistryValueState{Exists=false};return new RegistryValueState{Exists=true,Kind=key.GetValueKind(name).ToString(),Value=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames)};}
    public RegistryValueState Get(string name){using(var key=Registry.CurrentUser.OpenSubKey("Environment",false))return Read(key,name);}
    public RegistryValueState GetMachine(string name){using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment",false))return Read(key,name);}
    public void Set(string name,RegistryValueState value){if(!new[]{"Path","CODEX_CLI_PATH","CODEX_CODE_MODE_HOST_PATH"}.Concat(NativeUserProxy.Keys).Contains(name))throw new ArgumentException("该操作不管理此环境变量。");using(var key=Registry.CurrentUser.CreateSubKey("Environment")){if(!value.Exists)key.DeleteValue(name,false);else{var kind=(RegistryValueKind)Enum.Parse(typeof(RegistryValueKind),value.Kind);object data=value.Value;if(kind==RegistryValueKind.String||kind==RegistryValueKind.ExpandString)data=Convert.ToString(data);key.SetValue(name,data,kind);}}Environment.SetEnvironmentVariable(name,value.Exists?Convert.ToString(value.Value):null,EnvironmentVariableTarget.Process);IntPtr broadcast;SendMessageTimeout(new IntPtr(0xffff),0x001a,IntPtr.Zero,"Environment",2,1000,out broadcast);}
}
public sealed class NativeCommandRunner:INativeCommands {
    [DllImport("kernel32.dll")]static extern uint GetOEMCP();
    public static string DecodeOutput(byte[] bytes,int fallbackCodePage=0){if(bytes.Length>=2&&bytes[0]==255&&bytes[1]==254)return Encoding.Unicode.GetString(bytes,2,bytes.Length-2);if(bytes.Length>=2&&bytes[0]==254&&bytes[1]==255)return Encoding.BigEndianUnicode.GetString(bytes,2,bytes.Length-2);int offset=bytes.Length>=3&&bytes[0]==239&&bytes[1]==187&&bytes[2]==191?3:0;try{return new UTF8Encoding(false,true).GetString(bytes,offset,bytes.Length-offset);}catch(DecoderFallbackException){return Encoding.GetEncoding(fallbackCodePage==0?(int)GetOEMCP():fallbackCodePage).GetString(bytes);}}
    public NativeCommandResult Run(string file,string[] args,Dictionary<string,string> environment,int timeoutMs){
        var info=new ProcessStartInfo(NativePath.Display(Path.GetFullPath(file)),String.Join(" ",args.Select(NativeData.Quoted))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8};
        if(environment!=null)foreach(var item in environment)info.EnvironmentVariables[item.Key]=item.Value;
        using(var process=new Process{StartInfo=info})using(var job=new NativeChildJob()){
            if(!process.Start())throw new IOException("无法启动组件："+file);
            try{job.Assign(process);}catch{if(!process.HasExited)process.Kill();throw;}
            process.StandardInput.Close();using(var outputBytes=new MemoryStream())using(var errorBytes=new MemoryStream()){var output=process.StandardOutput.BaseStream.CopyToAsync(outputBytes);var error=process.StandardError.BaseStream.CopyToAsync(errorBytes);bool exited=process.WaitForExit(timeoutMs);
            if(!exited){job.Terminate();process.WaitForExit(5000);}
            if(!output.Wait(5000)||!error.Wait(5000))throw new IOException("组件输出未关闭，请检查本次进程日志。");
            return new NativeCommandResult{ExitCode=exited?process.ExitCode:-1,TimedOut=!exited,Output=DecodeOutput(outputBytes.ToArray()),Error=DecodeOutput(errorBytes.ToArray())};}
        }
    }
}
sealed class NativeChildJob:IDisposable {
    IntPtr handle;
    [StructLayout(LayoutKind.Sequential)]struct Basic{public long ProcessTime,JobTime;public uint Flags;public UIntPtr Minimum,Maximum;public uint Active;public UIntPtr Affinity;public uint Priority,Scheduling;}
    [StructLayout(LayoutKind.Sequential)]struct IO {public ulong ReadOps,WriteOps,OtherOps,ReadBytes,WriteBytes,OtherBytes;}
    [StructLayout(LayoutKind.Sequential)]struct Extended {public Basic Basic;public IO Io;public UIntPtr ProcessMemory,JobMemory,PeakProcess,PeakJob;}
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr CreateJobObject(IntPtr a,string name);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool SetInformationJobObject(IntPtr h,int type,ref Extended info,uint size);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool AssignProcessToJobObject(IntPtr h,IntPtr process);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool TerminateJobObject(IntPtr h,uint code);
    [DllImport("kernel32.dll")]static extern bool CloseHandle(IntPtr h);
    public NativeChildJob(){handle=CreateJobObject(IntPtr.Zero,null);if(handle==IntPtr.Zero)throw new Win32Exception(Marshal.GetLastWin32Error(),"无法建立本次组件进程组。");var limits=new Extended();limits.Basic.Flags=0x2000;if(!SetInformationJobObject(handle,9,ref limits,(uint)Marshal.SizeOf(limits))){var error=Marshal.GetLastWin32Error();Dispose();throw new Win32Exception(error,"无法设置本次组件退出清理。");}}
    public void Assign(Process p){if(!AssignProcessToJobObject(handle,p.Handle)&&!p.HasExited)throw new Win32Exception(Marshal.GetLastWin32Error(),"无法管理本次组件进程，已停止。");}
    public void Terminate(){if(!TerminateJobObject(handle,1))throw new Win32Exception(Marshal.GetLastWin32Error(),"结束本次超时组件失败。");}
    public void Dispose(){if(handle!=IntPtr.Zero){CloseHandle(handle);handle=IntPtr.Zero;}}
}
public sealed class NativeGitHubDownloads:INativeDownloads {
    readonly GitHubTransfer transfer;
    public NativeGitHubDownloads(GitHubNetworkOptions network,int port,string host,Action<string> log){transfer=new GitHubTransfer(network,port,host,log);}
    public string Text(string url,Func<string,bool> validate){return transfer.Fetch(url,validate);}
    public void File(string url,string target,Action<string> validate){transfer.Download(url,target,validate);}
}
public sealed class WindowsNativeProcesses:INativeProcesses {
    public static bool IsDesktopExecutable(string path){return !String.IsNullOrWhiteSpace(path)&&Regex.IsMatch(path,"^"+Regex.Escape(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WindowsApps")+Path.DirectorySeparatorChar)+@"OpenAI\.Codex_[0-9.]+_x64__2p2nqsd0c76g0[\\/]app[\\/](ChatGPT|Codex)\.exe$",RegexOptions.IgnoreCase);}
    internal Func<ManagementObject,ProcessIdentity> ReadIdentity=Entry;
    static readonly HashSet<string> ChildNames=new HashSet<string>(new[]{"ChatGPT","Codex","codex-latest","node","node_repl","cua_node","cua-helper","codex-computer-use-swift","codex-code-mode-host","codex-command-runner","codex-windows-sandbox-setup"},StringComparer.OrdinalIgnoreCase);
    static ProcessIdentity Entry(ManagementObject p){var owner=p.InvokeMethod("GetOwnerSid",null,null);if(Convert.ToUInt32(owner["ReturnValue"])!=0)throw new UnauthorizedAccessException("无法核验进程所属用户。");var entry=Basic(p);entry.Owner=Convert.ToString(owner["Sid"]);if(entry.Started==0||String.IsNullOrEmpty(entry.Path))throw new InvalidOperationException("无法读取目标进程的路径或启动时间。");return entry;}
    static ProcessIdentity Basic(ManagementObject p){return new ProcessIdentity{Id=Convert.ToInt32(p["ProcessId"]),ParentId=Convert.ToInt32(p["ParentProcessId"]),SessionId=Convert.ToInt32(p["SessionId"]),Path=Convert.ToString(p["ExecutablePath"]),Name=Path.GetFileNameWithoutExtension(Convert.ToString(p["Name"])),CommandLine=Convert.ToString(p["CommandLine"]),Started=p["CreationDate"]==null?0:ManagementDateTimeConverter.ToDateTime(Convert.ToString(p["CreationDate"])).ToUniversalTime().Ticks};}
    static bool Gone(int id){try{using(var p=Process.GetProcessById(id))return p.HasExited;}catch(ArgumentException){return true;}}
    static string[] RuntimeRoots(NativePaths paths,OfficialDesktop desktop){return new[]{desktop==null?null:desktop.Install,paths.RuntimeRoot,Path.Combine(paths.LocalData,"OpenAI","Codex","bin"),Path.Combine(paths.Home,"packages","standalone"),paths.LegacyCli}.Where(x=>!String.IsNullOrEmpty(x)).ToArray();}
    internal static bool SameIdentity(ProcessIdentity expected,ProcessIdentity current){return expected.Id==current.Id&&expected.Started==current.Started&&expected.SessionId==current.SessionId&&expected.Owner==current.Owner&&String.Equals(expected.Path,current.Path,StringComparison.OrdinalIgnoreCase);}
    static bool DesktopRoot(ProcessIdentity p,OfficialDesktop app){return IsDesktopExecutable(p.Path)||app!=null&&new[]{"ChatGPT.exe","Codex.exe"}.Any(name=>String.Equals(p.Path,Path.Combine(app.Install,"app",name),StringComparison.OrdinalIgnoreCase));}
    internal static List<ProcessIdentity> SelectTargets(IEnumerable<ProcessIdentity> source,NativePaths paths,OfficialDesktop desktop,string owner,int session,IEnumerable<ProcessIdentity> tracked){
        var all=source.ToArray();var retained=(tracked??Enumerable.Empty<ProcessIdentity>()).ToArray();
        foreach(var p in all){var expected=retained.FirstOrDefault(x=>x.Key==p.Key);if(expected!=null&&!SameIdentity(expected,p))throw new InvalidOperationException("已授权进程身份无法核验："+p.Name+"（PID "+p.Id+"）。初始化已停止。");}
        var candidates=all.Where(p=>p.Owner==owner&&p.SessionId==session).ToArray();var roots=RuntimeRoots(paths,desktop);var ids=new HashSet<int>(candidates.Where(p=>DesktopRoot(p,desktop)||retained.Any(x=>SameIdentity(x,p))).Select(p=>p.Id));bool changed;
        do{changed=false;foreach(var p in candidates)if(!ids.Contains(p.Id)&&ids.Contains(p.ParentId)&&ChildNames.Contains(p.Name??"")&&roots.Any(root=>!String.IsNullOrEmpty(p.Path)&&NativeData.Inside(p.Path,root))){ids.Add(p.Id);changed=true;}}while(changed);
        return candidates.Where(p=>ids.Contains(p.Id)).ToList();
    }
    List<ProcessIdentity> Scan(NativePaths paths,OfficialDesktop desktop,IEnumerable<ProcessIdentity> tracked,bool runtimeUsers){
        var owner=WindowsIdentity.GetCurrent().User.Value;int session=Process.GetCurrentProcess().SessionId;var candidates=new Dictionary<int,ManagementObject>();
        using(var query=new ManagementObjectSearcher("SELECT * FROM Win32_Process WHERE SessionId="+session))using(var values=query.Get())foreach(ManagementObject p in values)candidates.Add(Convert.ToInt32(p["ProcessId"]),p);
        try{
            var basic=candidates.Values.Select(p=>{var entry=Basic(p);entry.Owner=owner;return entry;}).ToList();
            foreach(var p in basic)if(String.IsNullOrEmpty(p.Path)&&(p.Name.Equals("ChatGPT",StringComparison.OrdinalIgnoreCase)||p.Name.Equals("Codex",StringComparison.OrdinalIgnoreCase))&&!Gone(p.Id))throw new UnauthorizedAccessException("无法读取候选 "+p.Name+"（PID "+p.Id+"）的路径，未执行强制结束。");
            var roots=RuntimeRoots(paths,desktop);var selected=runtimeUsers?basic.Where(p=>IsDesktopExecutable(p.Path)||roots.Any(root=>!String.IsNullOrEmpty(p.Path)&&NativeData.Inside(p.Path,root))).ToList():SelectTargets(basic,paths,desktop,owner,session,tracked);
            var verified=new List<ProcessIdentity>();foreach(var p in selected){ProcessIdentity entry;try{entry=ReadIdentity(candidates[p.Id]);}catch(Exception ex){if(Gone(p.Id))continue;throw TerminationFailure(p,"核验目标进程",ex);}if(entry.Owner==owner&&entry.SessionId==session)verified.Add(entry);}
            return runtimeUsers?verified:SelectTargets(verified,paths,desktop,owner,session,tracked);
        }finally{foreach(var p in candidates.Values)p.Dispose();}
    }
    public List<ProcessIdentity> List(NativePaths paths,OfficialDesktop desktop){return Scan(paths,desktop,null,false);}
    public List<ProcessIdentity> RuntimeUsers(NativePaths paths,OfficialDesktop desktop){return Scan(paths,desktop,null,true);}
    static bool ConfirmExited(Process process){try{return process.WaitForExit(500);}catch(Win32Exception){return false;}catch(InvalidOperationException){return false;}}
    internal static InvalidOperationException TerminationFailure(ProcessIdentity process,string stage,Exception error){var wmi=error as ManagementException;var windows=error as Win32Exception;var detail=wmi!=null?"WMI 错误 "+wmi.ErrorCode+"（0x"+unchecked((uint)(int)wmi.ErrorCode).ToString("X8")+"）":windows!=null?"Windows 错误码 "+windows.NativeErrorCode:error is UnauthorizedAccessException?"权限不足":error.GetType().Name;return new InvalidOperationException(stage+"未完成："+process.Name+"（PID "+process.Id+"）；"+detail+"："+error.Message.Trim()+"。尚未确认目标退出，初始化已停止。",error);}
    public void Terminate(ProcessIdentity expected){
        Process p;try{p=Process.GetProcessById(expected.Id);}catch(ArgumentException){return;}
        using(p)try{
            if(p.HasExited)return;var handle=p.Handle;
            using(var query=new ManagementObjectSearcher("SELECT * FROM Win32_Process WHERE ProcessId="+expected.Id))using(var values=query.Get()){
                if(p.HasExited)return;var rows=values.Cast<ManagementObject>().ToArray();
                if(rows.Length!=1){foreach(var row in rows)row.Dispose();if(ConfirmExited(p))return;throw new InvalidOperationException("仍在运行，无法核验进程身份："+expected.Name+"（PID "+expected.Id+"）。初始化已停止。");}
                using(var row=rows[0]){var current=ReadIdentity(row);if(p.HasExited)return;if(!SameIdentity(expected,current))throw new InvalidOperationException("进程已变化，停止强制结束："+expected.Name+"（PID "+expected.Id+"）。");}
            }
            if(!p.HasExited)p.Kill();
        }catch(ManagementException ex){if(!ConfirmExited(p))throw TerminationFailure(expected,"强制结束",ex);}
        catch(Win32Exception ex){if(!ConfirmExited(p))throw TerminationFailure(expected,"强制结束",ex);}
    }
    public List<ProcessIdentity> Wait(NativePaths paths,OfficialDesktop app,IEnumerable<ProcessIdentity> tracked,int timeoutMs){var targets=tracked.ToArray();var authorized=new HashSet<string>(targets.Select(p=>p.Key));var timer=Stopwatch.StartNew();do{var current=Scan(paths,app,targets,false);if(current.Count==0||current.Any(p=>!authorized.Contains(p.Key))||timer.ElapsedMilliseconds>=timeoutMs)return current;Thread.Sleep(100);}while(true);}
}
public static class NativeProcessWorkflow {
    public static void EnsureStopped(NativePaths paths,OfficialDesktop app,INativeProcesses processes,bool allowed,IEnumerable<string> approved,Action<string> log){
        var original=processes.List(paths,app);if(original.Count==0)return;if(!allowed)throw new InvalidOperationException("检测到 ChatGPT／Codex 仍在运行；未获得关闭授权，尚未重建配置。");var authorized=new HashSet<string>(approved??Enumerable.Empty<string>());AssertAuthorized(original,authorized);
        var ids=new HashSet<int>(original.Select(p=>p.Id));foreach(var process in original.OrderBy(p=>ids.Contains(p.ParentId)?1:0)){log("强制结束已授权目标："+process.Name+"（PID "+process.Id+"）");processes.Terminate(process);}
        var remaining=processes.Wait(paths,app,original,10000);AssertAuthorized(remaining,authorized);if(remaining.Count>0)throw new TimeoutException("强制结束后等待退出超过 10 秒："+String.Join("、",remaining.Select(p=>p.Name+"（PID "+p.Id+"）"))+"。目标仍在运行，初始化未开始。");
        log("已确认全部授权目标退出，继续初始化。");
    }
    public static void AssertAuthorized(IEnumerable<ProcessIdentity> current,HashSet<string> authorized){var unexpected=current.Where(p=>!authorized.Contains(p.Key)).ToArray();if(unexpected.Length>0)throw new InvalidOperationException("应用重新启动或出现未授权关联进程："+String.Join("、",unexpected.Select(p=>p.Name+"（PID "+p.Id+"）"))+"；未结束这些进程，初始化已停止，请重新确认。");}
}
}
