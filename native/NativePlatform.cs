using File=CodexKit.Native.NativeFile;
using Directory=CodexKit.Native.NativeDirectory;
using FileInfo=CodexKit.Native.NativeFileInfo;
using System;using System.IO;using System.Linq;using System.Text;using System.Collections.Generic;using System.Diagnostics;using System.Threading;using System.Runtime.InteropServices;using System.ComponentModel;using System.Security.Principal;using System.Management;using Microsoft.Win32;
namespace CodexKit.Native {
public sealed class WindowsRegistryStore:INativeRegistry {
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr SendMessageTimeout(IntPtr window,uint message,IntPtr parameter,string data,uint flags,uint timeout,out IntPtr result);
    static RegistryValueState Read(RegistryKey key,string name){if(key==null||!key.GetValueNames().Contains(name,StringComparer.OrdinalIgnoreCase))return new RegistryValueState{Exists=false};return new RegistryValueState{Exists=true,Kind=key.GetValueKind(name).ToString(),Value=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames)};}
    public RegistryValueState Get(string name){using(var key=Registry.CurrentUser.OpenSubKey("Environment",false))return Read(key,name);}
    public RegistryValueState GetMachine(string name){using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Environment",false))return Read(key,name);}
    public void Set(string name,RegistryValueState value){if(!new[]{"Path","CODEX_CLI_PATH","CODEX_CODE_MODE_HOST_PATH"}.Contains(name))throw new ArgumentException("该操作不管理此环境变量。");using(var key=Registry.CurrentUser.CreateSubKey("Environment")){if(!value.Exists)key.DeleteValue(name,false);else{var kind=(RegistryValueKind)Enum.Parse(typeof(RegistryValueKind),value.Kind);object data=value.Value;if(kind==RegistryValueKind.String||kind==RegistryValueKind.ExpandString)data=Convert.ToString(data);key.SetValue(name,data,kind);}}Environment.SetEnvironmentVariable(name,value.Exists?Convert.ToString(value.Value):null,EnvironmentVariableTarget.Process);IntPtr broadcast;SendMessageTimeout(new IntPtr(0xffff),0x001a,IntPtr.Zero,"Environment",2,1000,out broadcast);}
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
    internal Func<ManagementObject,ProcessIdentity> ReadIdentity=Entry;
    static readonly HashSet<string> ChildNames=new HashSet<string>(new[]{"ChatGPT","Codex","codex-latest","node","node_repl","cua_node","cua-helper","codex-computer-use-swift","codex-code-mode-host","codex-command-runner","codex-windows-sandbox-setup"},StringComparer.OrdinalIgnoreCase);
    static ProcessIdentity Entry(ManagementObject p){var owner=p.InvokeMethod("GetOwnerSid",null,null);if(Convert.ToUInt32(owner["ReturnValue"])!=0)throw new UnauthorizedAccessException("无法核验进程所属用户。");return new ProcessIdentity{Id=Convert.ToInt32(p["ProcessId"]),ParentId=Convert.ToInt32(p["ParentProcessId"]),SessionId=Convert.ToInt32(p["SessionId"]),Path=Convert.ToString(p["ExecutablePath"]),Name=Path.GetFileNameWithoutExtension(Convert.ToString(p["Name"])),Owner=Convert.ToString(owner["Sid"]),Started=ManagementDateTimeConverter.ToDateTime(Convert.ToString(p["CreationDate"])).ToUniversalTime().Ticks};}
    static bool Gone(int id){try{using(var p=Process.GetProcessById(id))return p.HasExited;}catch(ArgumentException){return true;}}
    public List<ProcessIdentity> List(NativePaths paths,OfficialDesktop desktop){
        var roots=new[]{desktop==null?null:desktop.Install,paths.RuntimeRoot,Path.Combine(paths.Home,"packages","standalone"),paths.LegacyCli}.Where(x=>!String.IsNullOrEmpty(x)).ToArray();var own=WindowsIdentity.GetCurrent().User.Value;int session=Process.GetCurrentProcess().SessionId;
        var result=new List<ProcessIdentity>();var candidates=new List<ManagementObject>();
        using(var query=new ManagementObjectSearcher("SELECT * FROM Win32_Process WHERE SessionId="+session))using(var values=query.Get())foreach(ManagementObject p in values)candidates.Add(p);
        try{
            var ids=new HashSet<int>();bool changed;
            do{changed=false;foreach(var p in candidates){int id=Convert.ToInt32(p["ProcessId"]);if(ids.Contains(id))continue;var path=Convert.ToString(p["ExecutablePath"]);var name=Path.GetFileNameWithoutExtension(Convert.ToString(p["Name"]));bool pathMatch=roots.Any(root=>!String.IsNullOrEmpty(path)&&NativeData.Inside(path,root));bool child=ids.Contains(Convert.ToInt32(p["ParentProcessId"]))&&ChildNames.Contains(name);
                if(!pathMatch&&!child){if(String.IsNullOrEmpty(path)&&(name.Equals("ChatGPT",StringComparison.OrdinalIgnoreCase)||name.Equals("Codex",StringComparison.OrdinalIgnoreCase))&&!Gone(id))throw new UnauthorizedAccessException("无法读取正在运行的 "+name+"（PID "+id+"）路径，初始化未开始。");continue;}
                ProcessIdentity entry;try{entry=ReadIdentity(p);}catch{if(Gone(id))continue;throw;}
                if(entry.Owner==own){ids.Add(id);result.Add(entry);changed=true;}
            }}while(changed);return result;
        }finally{foreach(var p in candidates)p.Dispose();}
    }
    static bool ConfirmExited(Process process){try{return process.WaitForExit(500);}catch(Win32Exception){return false;}catch(InvalidOperationException){return false;}}
    static InvalidOperationException CloseFailure(ProcessIdentity process,bool force,Exception error){var wmi=error as ManagementException;var windows=error as Win32Exception;var detail=wmi!=null?"WMI 错误 "+wmi.ErrorCode+"（0x"+unchecked((uint)(int)wmi.ErrorCode).ToString("X8")+"）":windows!=null?"Windows 错误码 "+windows.NativeErrorCode:error.GetType().Name;return new InvalidOperationException((force?"结束残留进程":"请求正常退出")+"未完成："+process.Name+"（PID "+process.Id+"）；"+detail+"："+error.Message.Trim()+"。尚未确认该进程退出，初始化已停止。",error);}
    public void RequestExit(ProcessIdentity expected,bool force){
        Process p;try{p=Process.GetProcessById(expected.Id);}catch(ArgumentException){return;}
        using(p)try{
            if(p.HasExited)return;var handle=p.Handle;
            using(var query=new ManagementObjectSearcher("SELECT * FROM Win32_Process WHERE ProcessId="+expected.Id))using(var values=query.Get()){
                if(p.HasExited)return;var rows=values.Cast<ManagementObject>().ToArray();
                if(rows.Length!=1){foreach(var row in rows)row.Dispose();if(ConfirmExited(p))return;throw new InvalidOperationException("仍在运行，无法核验进程身份："+expected.Name+"（PID "+expected.Id+"）。初始化已停止。");}
                using(var row=rows[0]){var current=ReadIdentity(row);if(p.HasExited)return;if(current.Started!=expected.Started||current.SessionId!=expected.SessionId||current.Owner!=expected.Owner||!String.Equals(current.Path,expected.Path,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("进程已变化，停止关闭："+expected.Name+"（PID "+expected.Id+"）。");}
            }
            if(p.HasExited)return;if(force)p.Kill();else p.CloseMainWindow();
        }catch(ManagementException ex){if(!ConfirmExited(p))throw CloseFailure(expected,force,ex);}
        catch(Win32Exception ex){if(!ConfirmExited(p))throw CloseFailure(expected,force,ex);}
    }
    public List<ProcessIdentity> Wait(NativePaths p,OfficialDesktop app,int timeoutMs){var timer=Stopwatch.StartNew();do{var current=List(p,app);if(current.Count==0||timer.ElapsedMilliseconds>=timeoutMs)return current;Thread.Sleep(100);}while(true);}
}
public static class NativeProcessWorkflow {
    public static void EnsureStopped(NativePaths paths,OfficialDesktop app,INativeProcesses processes,bool allowed,IEnumerable<string> approved,Action<string> log){
        var original=processes.List(paths,app);if(original.Count==0)return;if(!allowed)throw new InvalidOperationException("检测到 ChatGPT／Codex 仍在运行；未获得关闭授权，尚未重建配置。");var authorized=new HashSet<string>(approved??Enumerable.Empty<string>());AssertAuthorized(original,authorized);
        foreach(var process in original){log("请求正常退出："+process.Name+"（PID "+process.Id+"）");processes.RequestExit(process,false);}
        var remaining=processes.Wait(paths,app,5000);AssertAuthorized(remaining,authorized);foreach(var process in remaining){log("结束已授权的残留进程："+process.Name+"（PID "+process.Id+"）");processes.RequestExit(process,true);}
        if(remaining.Count>0){remaining=processes.Wait(paths,app,10000);AssertAuthorized(remaining,authorized);if(remaining.Count>0)throw new TimeoutException("等待关闭超时；初始化未开始。");}
    }
    public static void AssertAuthorized(IEnumerable<ProcessIdentity> current,HashSet<string> authorized){if(current.Any(p=>!authorized.Contains(p.Key)))throw new InvalidOperationException("应用重新启动或出现新关联进程；停止初始化，请重新确认。");}
}
}
