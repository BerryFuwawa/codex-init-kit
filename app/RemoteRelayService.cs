using System;
using System.IO;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using CodexKit.Native;

namespace CodexKit {
[EventSource(Name="CodexInitKit-RemoteRelay")]
internal sealed class RemoteRelayEvents : EventSource {
    internal static readonly RemoteRelayEvents Log = new RemoteRelayEvents();
    [Event(1, Level=EventLevel.Warning)]
    public void ConnectionFailed(string detail) { WriteEvent(1, detail); }
}
internal sealed class RelayStatus {
    internal string Text="已关闭";
    internal bool Installed,CanRemove;
    internal bool CanEnable=true;
}

internal sealed class RemoteRelayService : ServiceBase {
    internal const string NameInWindows = "CodexInitKitRelay";
    internal const string Switch = "--remote-relay-service";
    readonly IPEndPoint proxy;
    RemoteControlRelay relay;

    RemoteRelayService(IPEndPoint endpoint) {
        ServiceName = NameInWindows;
        proxy = endpoint;
        AutoLog = false;
    }

    internal static int Dispatch(string[] args) {
        int port;
        IPAddress address;
        if (args.Length != 3 || !IPAddress.TryParse(args[1], out address) ||
                !Int32.TryParse(args[2], out port) || port < 1 || port > 65535) return 64;
        ServiceBase.Run(new RemoteRelayService(new IPEndPoint(address, port)));
        return 0;
    }

    protected override void OnStart(string[] args) {
        relay = new RemoteControlRelay(new IPEndPoint(RemoteControlRelay.Loopback, 443), CurrentTarget, RemoteRelayEvents.Log.ConnectionFailed);
        relay.Start();
        relay.Completion.ContinueWith(task => {
            RemoteRelayEvents.Log.ConnectionFailed(task.Exception.GetBaseException().Message);
            ExitCode = 1;
            Stop();
        }, TaskContinuationOptions.OnlyOnFaulted);
    }
    protected override void OnStop() { relay.Dispose(); }

    static string InstalledExe { get {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "Codex Init Kit", "RemoteRelay", "CodexInitKitRelay.exe");
    } }

    static string ImagePath() {
        using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + NameInWindows)) {
            return key == null ? null : Convert.ToString(key.GetValue("ImagePath"));
        }
    }
    static string CommandPrefix { get { return Core.Quote(InstalledExe) + " " + Switch + " "; } }
    internal static string CommandFor(IPEndPoint target) {
        return CommandPrefix + target.Address + " " + target.Port;
    }

    internal static void SyncSavedProxy(string home, Action<string> log) {
        SyncSavedProxy(home, ImagePath(), target => {
            var id = Guid.NewGuid().ToString("N");
            var options = new System.Collections.Generic.Dictionary<string,object> {
                {"proxyHost", target == null ? Core.DefaultProxyHost : target.Address.ToString()}
            };
            using (var worker = Core.StartWorker("relay-sync", id, Environment.SystemDirectory.Substring(0,1),
                    target == null ? 0 : target.Port, false, options)) {
                worker.WaitForExit();
                var file = Path.Combine(Core.Logs, id+".log");
                if (File.Exists(file)) log(File.ReadAllText(file).Trim());
                if (worker.ExitCode != 0) throw new IOException("远控转发同步未完成，请检查代理后再次保存。");
            }
        });
    }

    internal static void SyncSavedProxy(string home, string existing, Action<IPEndPoint> apply) {
        if (existing == null || !existing.StartsWith(CommandPrefix, StringComparison.OrdinalIgnoreCase)) return;
        var saved = ProxyPortSettings.LoadAddress(home);
        var target = saved.Configured ? new IPEndPoint(IPAddress.Parse(saved.Host), saved.Port) : null;
        if (target != null && String.Equals(CommandFor(target),existing,StringComparison.OrdinalIgnoreCase)) return;
        apply(target);
    }

    IPEndPoint CurrentTarget() {
        var path = ImagePath();
        return path == null ? proxy : ReadTarget(path);
    }

    internal static IPEndPoint ReadTarget(string path) {
        if(!path.StartsWith(CommandPrefix,StringComparison.OrdinalIgnoreCase))throw new IOException("服务配置已更改。");
        var parts=path.Substring(CommandPrefix.Length).Split(' ');
        int port;IPAddress address;
        if(parts.Length!=2 || !int.TryParse(parts[1],out port) || port<1 || port>65535 || !IPAddress.TryParse(parts[0],out address))
            throw new InvalidDataException("转发服务的上游代理地址无效。");
        return new IPEndPoint(address,port);
    }

    // Validate the same TLS destination as Codex after the hosts entry is active.
    static void CheckRoute() {
        using (var client = new TcpClient())
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12)))
        using (deadline.Token.Register(client.Close)) {
            try {
                client.Connect(RemoteControlRelay.Loopback, 443);
                using (var tls = new SslStream(client.GetStream(), false))
                    tls.AuthenticateAsClient("chatgpt.com");
            } catch (Exception ex) {
                throw new IOException("转发未能通过代理与 chatgpt.com 建立 TLS 连接。请检查已保存的代理及域名分流。", ex);
            }
        }
    }

    internal static bool OwnedInstallation {
        get { string path=ImagePath();return path!=null && path.StartsWith(CommandPrefix,StringComparison.OrdinalIgnoreCase); }
    }

    internal static RelayStatus ReadStatus() {
        string path = ImagePath();
        bool mapped = RemoteControlHosts.HasEntry(RemoteControlHosts.Read(RemoteControlHosts.SystemFile));
        bool leftover = File.Exists(InstalledExe);
        if(path==null){
            if(mapped || leftover)return new RelayStatus{Text="有残留文件或映射，可卸载清理",CanRemove=true,CanEnable=false};
            return new RelayStatus();
        }
        if (!path.StartsWith(CommandPrefix, StringComparison.OrdinalIgnoreCase))
            return new RelayStatus{Text="同名服务使用了其他程序",CanEnable=false};
        using (var service = new ServiceController(NameInWindows)) {
            var endpoint = path.Substring(CommandPrefix.Length).Split(' ');
            IPAddress address;
            int port;
            var target = endpoint.Length == 2 && IPAddress.TryParse(endpoint[0], out address) &&
                Int32.TryParse(endpoint[1], out port) && port > 0 && port <= 65535
                ? new IPEndPoint(address, port).ToString() : "未知代理";
            bool running=service.Status==ServiceControllerStatus.Running;
            string text=running && mapped ? "运行中 · "+target : (mapped ? "已设置映射" : "缺少 hosts 映射") +
                " · 服务"+(running ? "正在运行" : "尚未就绪");
            return new RelayStatus{Text=text,Installed=true,CanRemove=true,CanEnable=false};
        }
    }

    internal static bool Configure(IPEndPoint target, bool sync) {
        using (var gate = new Mutex(false, @"Global\CodexInitKit.RemoteRelay.Configuration")) {
            try { if (!gate.WaitOne(TimeSpan.FromSeconds(15))) throw new IOException("另一个远控转发操作尚未完成。"); }
            catch (AbandonedMutexException) { /* The previous configuration process has exited. */ }
            try {
                string existing = ImagePath();
                if (existing != null && !existing.StartsWith(CommandPrefix, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Windows 中已有同名服务，服务程序路径与本工具不同。");
                if (sync && existing == null) return false;
                if (target==null) {
                    Remove(existing != null);
                    SaveAppliedChoice(false);
                    return true;
                }
                if (target.Equals(new IPEndPoint(RemoteControlRelay.Loopback, 443)))
                    throw new IOException("上游代理不能是转发监听地址。");
                // Check conflicts before creating a service or changing an existing mapping.
                var originalHosts = RemoteControlHosts.Read(RemoteControlHosts.SystemFile);
                bool previouslyMapped = RemoteControlHosts.HasEntry(originalHosts);
                RemoteControlHosts.Edit(originalHosts, true);
                string command = CommandFor(target);
                bool changeTarget = existing != null && !String.Equals(existing, command, StringComparison.OrdinalIgnoreCase);
                bool created = false;
                bool changed = false;
                try {
                    if (existing == null) {
                        Directory.CreateDirectory(Path.GetDirectoryName(InstalledExe));
                        File.Copy(Core.Exe, InstalledExe, true);
                        Sc("create", NameInWindows, "binPath=", command, "start=", "auto",
                            "obj=", @"NT AUTHORITY\LocalService", "DisplayName=", "Codex Init Kit 远控转发");
                        created = true;
                    }
                    if (changeTarget) {
                        Sc("config", NameInWindows, "binPath=", command);
                        changed=true;
                    }
                    using (var service = new ServiceController(NameInWindows)) {
                        if (service.Status == ServiceControllerStatus.Stopped) service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(12));
                    }
                    if (!previouslyMapped) RemoteControlHosts.Set(RemoteControlHosts.SystemFile, true);
                    CheckRoute();
                } catch (Exception ex) {
                    if(changed)Sc("config",NameInWindows,"binPath=",existing);
                    if (created) {
                        try { Remove(true); }
                        catch (Exception cleanup) { throw new IOException(ex.Message + "；清理未完成：" + cleanup.Message, ex); }
                    } else if (existing == null) DeleteHelper(InstalledExe);
                    else if (!previouslyMapped) RemoteControlHosts.Set(RemoteControlHosts.SystemFile, false);
                    throw;
                }
                if(!sync)SaveAppliedChoice(true);
                return true;
            } finally { gate.ReleaseMutex(); }
        }
    }

    static void SaveAppliedChoice(bool installed){
        try { RelayPreference.Save(Core.Root,installed,installed); }
        catch(Exception ex){throw new IOException((installed?"服务已开启":"服务已卸载")+"，但初始化选项未保存。",ex);}
    }

    static void Remove(bool registered) {
        Remove(RemoteControlHosts.SystemFile,InstalledExe,registered ? (Action)Unregister : null);
    }

    static void Unregister(){
        using(var service=new ServiceController(NameInWindows)){
            if(service.Status!=ServiceControllerStatus.Stopped){
                if(service.Status!=ServiceControllerStatus.StopPending)service.Stop();
                service.WaitForStatus(ServiceControllerStatus.Stopped,TimeSpan.FromSeconds(12));
            }
        }
        Sc("delete",NameInWindows);
    }

    internal static void Remove(string hosts,string executable,Action unregister){
        if(RemoteControlHosts.HasEntry(RemoteControlHosts.Read(hosts)))RemoteControlHosts.Set(hosts,false);
        if(unregister!=null)unregister();
        DeleteHelper(executable);
    }

    static void DeleteHelper(string executable) {
        for (int attempt = 0; File.Exists(executable); attempt++) {
            try { File.Delete(executable); break; }
            catch (IOException ex) {
                // SCM can report Stopped shortly before the service process releases its executable.
                int code = ex.HResult & 0xffff;
                if (attempt >= 20 || (code != 32 && code != 33)) throw;
                Thread.Sleep(100);
            }
            catch (UnauthorizedAccessException) {
                if (attempt >= 20) throw;
                Thread.Sleep(100);
            }
        }
        string dir = Path.GetDirectoryName(executable);
        if (Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length == 0) Directory.Delete(dir);
    }
    static void Sc(params string[] args) {
        var result = new NativeCommandRunner().Run(Path.Combine(Environment.SystemDirectory,"sc.exe"), args, null, 15000);
        if (result.TimedOut || result.ExitCode != 0)
            throw new IOException("服务配置失败：" + (result.TimedOut ? "操作超时" : (result.Output + result.Error).Trim()));
    }
}
}

