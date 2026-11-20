using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace CodexKit {
internal sealed class RemoteControlRelay : IDisposable {
    internal const string Destination = "chatgpt.com:443";
    internal static readonly IPAddress Loopback = IPAddress.Parse("127.0.0.2");
    readonly TcpListener listener;
    readonly Func<IPEndPoint> getProxy;
    readonly HashSet<TcpClient> connections = new HashSet<TcpClient>();
    readonly Action<string> report;
    bool stopping;
    internal Task Completion { get; private set; }
    internal IPEndPoint Endpoint { get { return (IPEndPoint)listener.LocalEndpoint; } }

    internal RemoteControlRelay(IPEndPoint listen, Func<IPEndPoint> upstream, Action<string> reportError) {
        listener = new TcpListener(listen);
        listener.Server.ExclusiveAddressUse = true;
        getProxy = upstream;
        report = reportError;
    }

    internal void Start() {
        listener.Start();
        Completion = AcceptConnections();
    }

    async Task AcceptConnections() {
        for (;;) {
            TcpClient client;
            try { client = await listener.AcceptTcpClientAsync().ConfigureAwait(false); }
            catch (ObjectDisposedException) { lock (connections) { if (stopping) return; } throw; }
            catch (SocketException ex) {
                lock (connections) { if (stopping) return; }
                if (ex.SocketErrorCode == SocketError.ConnectionReset || ex.SocketErrorCode == SocketError.ConnectionAborted) continue;
                throw;
            }
            lock (connections) {
                if (stopping) { client.Close(); return; }
                // A local proxy using a DIRECT rule can resolve the hosts entry back to this listener.
                if (connections.Count >= 128) { client.Close(); continue; }
                connections.Add(client);
            }
            Forward(client);
        }
    }

    async void Forward(TcpClient client) {
        TcpClient upstream=null;
        bool opened = false;
        try {
            var proxy=getProxy();
            if(proxy.Equals(Endpoint))throw new IOException("代理地址与转发入口相同。");
            upstream=new TcpClient(proxy.AddressFamily);
            lock(connections){if(stopping)return;connections.Add(upstream);}
            using (var timeout = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(10)))
            using (timeout.Token.Register(upstream.Close)) {
                await upstream.ConnectAsync(proxy.Address, proxy.Port).ConfigureAwait(false);
                await OpenTunnel(upstream.GetStream()).ConfigureAwait(false);
                opened = true;
            }
            var outgoing = client.GetStream().CopyToAsync(upstream.GetStream());
            var incoming = upstream.GetStream().CopyToAsync(client.GetStream());
            await Task.WhenAny(outgoing, incoming).ConfigureAwait(false);
            client.Close();
            upstream.Close();
            await Task.WhenAll(outgoing, incoming).ConfigureAwait(false);
        } catch (Exception ex) {
            // Closing a peer interrupts pending reads. Setup failures are reported by the service.
            lock (connections) { if (!stopping && !opened && report != null) report(ex.GetBaseException().Message); }
        } finally {
            client.Close();
            if(upstream!=null)upstream.Close();
            lock (connections) { connections.Remove(client); connections.Remove(upstream); }
        }
    }

    // Read through the CONNECT response only. The caller owns all subsequent TLS bytes.
    internal static async Task OpenTunnel(NetworkStream stream) {
        var request = Encoding.ASCII.GetBytes("CONNECT " + Destination + " HTTP/1.1\r\nHost: " + Destination + "\r\n\r\n");
        await stream.WriteAsync(request, 0, request.Length).ConfigureAwait(false);
        var header = new List<byte>();
        var one = new byte[1];
        while (header.Count < 16384) {
            if (await stream.ReadAsync(one, 0, 1).ConfigureAwait(false) == 0)
                throw new IOException("代理在返回 CONNECT 响应前关闭了连接。");
            header.Add(one[0]);
            int end = header.Count;
            if (end >= 4 && header[end-4] == 13 && header[end-3] == 10 && header[end-2] == 13 && header[end-1] == 10) {
                var firstLine = Encoding.ASCII.GetString(header.ToArray()).Split('\r')[0];
                var fields = firstLine.Split(' ');
                if (fields.Length < 2 || !fields[0].StartsWith("HTTP/") || fields[1] != "200")
                    throw new IOException("代理拒绝了远控连接：" + firstLine);
                return;
            }
        }
        throw new IOException("代理返回的 CONNECT 响应头过长。");
    }

    public void Dispose() {
        lock (connections) {
            if (stopping) return;
            stopping = true;
            listener.Stop();
            foreach (var socket in new List<TcpClient>(connections)) socket.Close();
        }
    }
}
}

