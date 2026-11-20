using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CodexKit {
internal static class RemoteControlRelayTests {
    static void Check(bool condition, string message) {
        if (!condition) throw new InvalidDataException(message);
    }

    internal static void Run(string directory) {
        Directory.CreateDirectory(directory);
        foreach (string operation in new[] {"relay-enable", "relay-disable", "relay-sync"}) {
            var start = Core.WorkerStartInfo(operation, Guid.NewGuid().ToString("N"), "C", 0, false, null, false);
            Check(start.Verb == "runas" && start.Arguments.StartsWith("--worker " + operation + " "),
                "Relay setting did not launch the privileged worker");
        }
        var install=Path.Combine(directory,"relay-installed");
        Directory.CreateDirectory(install);
        var helper=Path.Combine(install,"CodexInitKitRelay.exe");
        var hosts=Path.Combine(directory,"uninstall-hosts");
        const string originalHosts="127.0.0.1 localhost\r\n";
        File.WriteAllText(hosts,originalHosts);RemoteControlHosts.Set(hosts,true);File.WriteAllText(helper,"fixture");
        bool called=false;
        RemoteRelayService.Remove(hosts,helper,()=>{
            Check(File.ReadAllText(hosts)==originalHosts && File.Exists(helper),"Uninstall order or unrelated hosts changed");
            called=true;
        });
        Check(called && !File.Exists(helper) && !Directory.Exists(install),"Service helper was not removed");
        Directory.CreateDirectory(install);File.WriteAllText(helper,"fixture");RemoteControlHosts.Set(hosts,true);
        RemoteRelayService.Remove(hosts,helper,null);
        Check(File.ReadAllText(hosts)==originalHosts && !File.Exists(helper),"Leftover cleanup failed without registration");
        var path = Path.Combine(directory, "relay-hosts");
        foreach (var text in new[] { "", "# user", "127.0.0.1 localhost\r\n", "# 中文注释\n" }) {
            var original = Encoding.UTF8.GetBytes(text);
            File.WriteAllBytes(path, original);
            RemoteControlHosts.Set(path, true);
            var enabled = File.ReadAllBytes(path);
            RemoteControlHosts.Set(path, true);
            Check(enabled.SequenceEqual(File.ReadAllBytes(path)), "Enabling twice changed hosts");
            RemoteControlHosts.Set(path, false);
            Check(original.SequenceEqual(File.ReadAllBytes(path)), "Hosts did not round-trip");
            RemoteControlHosts.Set(path, true);
            File.AppendAllText(path, "# user edit\r\n", new UTF8Encoding(false));
            RemoteControlHosts.Set(path, false);
            var remaining = File.ReadAllBytes(path);
            var separator = text.Length > 0 && !text.EndsWith("\n") ? "\r\n" : "";
            Check(original.Concat(Encoding.ASCII.GetBytes(separator + "# user edit\r\n")).SequenceEqual(remaining),
                "Disabling changed unrelated hosts bytes");
            RemoteControlHosts.Set(path, false);
            Check(remaining.SequenceEqual(File.ReadAllBytes(path)), "Disabling twice changed hosts");
        }
        File.WriteAllText(path, "192.0.2.4 example.com CHATGPT.COM. # custom\r\n");
        try { RemoteControlHosts.Set(path, true); throw new Exception("Existing mapping was replaced"); }
        catch (IOException) { Check(!File.ReadAllText(path).Contains(RemoteControlHosts.Entry), "Conflict modified hosts"); }
        IPEndPoint selected=null;
        var test = RoundTrip(target=>selected=target,()=>selected);
        if (!test.Wait(TimeSpan.FromSeconds(8))) throw new TimeoutException("Relay did not deliver bytes or close active sockets.");
        test.GetAwaiter().GetResult();
        using (var occupied = new RemoteControlRelay(new IPEndPoint(IPAddress.Loopback, 0), ()=>new IPEndPoint(IPAddress.Loopback, 1), null)) {
            occupied.Start();
            using (var second = new RemoteControlRelay(occupied.Endpoint, ()=>new IPEndPoint(IPAddress.Loopback, 1), null)) {
                try { second.Start(); throw new InvalidDataException("An occupied port was reported as available"); }
                catch (SocketException ex) { Check(ex.SocketErrorCode == SocketError.AddressAlreadyInUse || ex.SocketErrorCode == SocketError.AccessDenied, ex.Message); }
            }
            var error = new TaskCompletionSource<string>();
            using (var closedPort = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)) {
                closedPort.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                using (var unavailable = new RemoteControlRelay(new IPEndPoint(IPAddress.Loopback, 0),
                           ()=>(IPEndPoint)closedPort.LocalEndPoint, message => error.TrySetResult(message)))
                using (var caller = new TcpClient()) {
                    unavailable.Start();
                    caller.Connect(unavailable.Endpoint);
                    Check(error.Task.Wait(3000), "Unreachable upstream did not close the connection");
                }
            }
        }
    }

    internal static async Task RoundTrip(Action<IPEndPoint> select, Func<IPEndPoint> current) {
        var fake = new TcpListener(IPAddress.Loopback, 0);
        var switched = new TcpListener(IPAddress.Parse("127.0.0.3"), 0);
        fake.Start();switched.Start();
        try {
            select((IPEndPoint)fake.LocalEndpoint);
            using (var relay = new RemoteControlRelay(new IPEndPoint(IPAddress.Loopback, 0),
                       current, null))
            using (var client = new TcpClient()) {
                relay.Start();
                await client.ConnectAsync(relay.Endpoint.Address, relay.Endpoint.Port).ConfigureAwait(false);
                using (var upstream = await fake.AcceptTcpClientAsync().ConfigureAwait(false)) {
                    var network = upstream.GetStream();
                    var bytes = new byte[1];
                    var header = new StringBuilder();
                    while (!header.ToString().EndsWith("\r\n\r\n")) {
                        if (await network.ReadAsync(bytes, 0, 1).ConfigureAwait(false) == 0) throw new EndOfStreamException();
                        header.Append((char)bytes[0]);
                    }
                    Check(header.ToString().StartsWith("CONNECT chatgpt.com:443 HTTP/1.1\r\n"), "Wrong CONNECT target");
                    var reply = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\nB");
                    await network.WriteAsync(reply, 0, reply.Length).ConfigureAwait(false);
                    var downstream = client.GetStream();
                    Check(await downstream.ReadAsync(bytes, 0, 1).ConfigureAwait(false) == 1 && bytes[0] == 'B',
                        "Relay consumed bytes after the CONNECT response");
                    select((IPEndPoint)switched.LocalEndpoint);
                    using(var secondClient=new TcpClient()){
                        await secondClient.ConnectAsync(relay.Endpoint.Address,relay.Endpoint.Port).ConfigureAwait(false);
                        using(var newUpstream=await switched.AcceptTcpClientAsync().ConfigureAwait(false)){
                            var next=newUpstream.GetStream();
                            var connect=new StringBuilder();
                            while(!connect.ToString().EndsWith("\r\n\r\n")){
                                if(await next.ReadAsync(bytes,0,1).ConfigureAwait(false)==0)throw new EndOfStreamException();
                                connect.Append((char)bytes[0]);
                            }
                            Check(connect.ToString()==header.ToString(),"New connection lost its CONNECT destination");
                            var accepted=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n\r\nN");
                            await next.WriteAsync(accepted,0,accepted.Length).ConfigureAwait(false);
                            Check(await secondClient.GetStream().ReadAsync(bytes,0,1).ConfigureAwait(false)==1 && bytes[0]=='N',
                                "New connection failed through the saved proxy");
                        }
                    }
                    bytes[0] = 0xfa;
                    await downstream.WriteAsync(bytes, 0, 1).ConfigureAwait(false);
                    Check(await network.ReadAsync(bytes, 0, 1).ConfigureAwait(false) == 1 && bytes[0] == 0xfa,
                        "Relay did not forward binary payload");
                    relay.Dispose();
                    try { Check(await downstream.ReadAsync(bytes, 0, 1).ConfigureAwait(false) == 0, "Session survived stop"); }
                    catch (IOException) { /* Windows can reset an active TCP connection on close. */ }
                    await relay.Completion.ConfigureAwait(false);
                }
            }
        } finally { fake.Stop();switched.Stop(); }
    }
}
}

