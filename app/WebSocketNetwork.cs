using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CodexKit {
internal static class WebSocketNetwork {
    internal static TcpClient Connect(string host,int port,int timeout){host=host.Trim('[',']');IPAddress numeric;var client=new TcpClient(IPAddress.TryParse(host,out numeric)?numeric.AddressFamily:AddressFamily.InterNetwork);try{var pending=client.BeginConnect(host,port,null,null);using(pending.AsyncWaitHandle){if(!pending.AsyncWaitHandle.WaitOne(timeout))throw new IOException("连接超时，请检查代理端口或网络。");client.EndConnect(pending);}client.ReceiveTimeout=timeout;client.SendTimeout=timeout;client.NoDelay=true;return client;}catch{client.Close();throw;}}
    internal static string ReadHeader(Stream stream){var bytes=new List<byte>();int current;while((current=stream.ReadByte())>=0){bytes.Add((byte)current);int n=bytes.Count;if(n>=4&&bytes[n-4]==13&&bytes[n-3]==10&&bytes[n-2]==13&&bytes[n-1]==10)return Encoding.ASCII.GetString(bytes.ToArray());if(n>16384)throw new IOException("代理或服务返回的响应头过大。");}throw new IOException("连接在响应完成前关闭。");}
    internal static void Write(Stream stream,string value){var bytes=Encoding.ASCII.GetBytes(value);stream.Write(bytes,0,bytes.Length);stream.Flush();}
    internal static int Status(string header){var first=header.Split('\n')[0].Trim().Split(' ');int status;if(first.Length<2||!Int32.TryParse(first[1],out status))throw new IOException("端口没有返回有效 HTTP 响应；请确认使用 HTTP／混合代理端口。");return status;}
    internal static TcpClient Tunnel(Uri proxy,string host,int port){var client=Connect(proxy==null?host:proxy.Host,proxy==null?port:proxy.Port,8000);try{if(proxy!=null){Write(client.GetStream(),"CONNECT "+Authority(host,port)+" HTTP/1.1\r\nHost: "+Authority(host,port)+"\r\n\r\n");int status=Status(ReadHeader(client.GetStream()));if(status!=200)throw new IOException(status==407?"代理要求身份验证（HTTP 407），首版仅支持无需认证的本机代理入口。":"代理隧道未建立，HTTP "+status+"；请检查端口类型、节点及分流设置。");}return client;}catch{client.Close();throw;}}
    internal static string Authority(string host,int port){host=host.Trim('[',']');return (host.Contains(":")?"["+host+"]":host)+":"+port;}
    static byte[] ReadBytes(Stream stream,int count){var value=new byte[count];int total=0;while(total<count){int n=stream.Read(value,total,count-total);if(n==0)throw new IOException("WebSocket 连接已关闭。");total+=n;}return value;}
    internal static void SendFrame(Stream stream,int opcode,byte[] payload){if(payload.Length>125)throw new ArgumentException("探针载荷过大。");var mask=new byte[4];using(var random=RandomNumberGenerator.Create())random.GetBytes(mask);stream.WriteByte((byte)(0x80|opcode));stream.WriteByte((byte)(0x80|payload.Length));stream.Write(mask,0,4);for(int i=0;i<payload.Length;i++)stream.WriteByte((byte)(payload[i]^mask[i%4]));stream.Flush();}
    internal static byte[] ReadDataFrame(Stream stream){for(int frame=0;frame<12;frame++){var header=ReadBytes(stream,2);int opcode=header[0]&15;long length=header[1]&127;if(length==126){var n=ReadBytes(stream,2);length=n[0]*256+n[1];}else if(length==127)throw new IOException("探针收到了不支持的大帧。");if(length>65536)throw new IOException("探针响应过大。");var mask=(header[1]&128)!=0?ReadBytes(stream,4):null;var data=ReadBytes(stream,(int)length);if(mask!=null)for(int i=0;i<data.Length;i++)data[i]^=mask[i%4];if(opcode==8)throw new IOException("WebSocket 服务主动关闭了连接。");if(opcode==9){SendFrame(stream,10,data);continue;}if(opcode==1||opcode==2)return data;}throw new IOException("未收到预期的 WebSocket 回显。");}
    internal static void Echo(Uri endpoint,Uri proxy,Action<string> log){
        using(var client=Tunnel(proxy,endpoint.Host,endpoint.Port))using(var tls=new SslStream(client.GetStream(),false)){
            tls.ReadTimeout=8000;tls.WriteTimeout=8000;tls.AuthenticateAsClient(endpoint.Host,null,System.Security.Authentication.SslProtocols.Tls12,true);
            byte[] nonce=new byte[16];using(var random=RandomNumberGenerator.Create())random.GetBytes(nonce);string key=Convert.ToBase64String(nonce);
            Write(tls,"GET "+endpoint.PathAndQuery+" HTTP/1.1\r\nHost: "+endpoint.Authority+"\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Key: "+key+"\r\nSec-WebSocket-Version: 13\r\nUser-Agent: CodexInitKit/"+Core.Version+"\r\n\r\n");
            var response=ReadHeader(tls);int status=Status(response);if(status!=101)throw new IOException("WebSocket 握手未完成，HTTP "+status+"；此结果不能视为连接成功。");
            string expected;using(var sha=SHA1.Create())expected=Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key+"258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            bool accepted=false;foreach(var line in response.Split('\n'))if(line.StartsWith("Sec-WebSocket-Accept:",StringComparison.OrdinalIgnoreCase)&&line.Substring(line.IndexOf(':')+1).Trim()==expected)accepted=true;if(!accepted)throw new IOException("WebSocket 握手校验未通过。");
            if(log!=null)log("WebSocket 握手通过（HTTP 101），正在验证持续通信。");
            for(int i=0;i<3;i++){var message="CodexInitKit-probe-"+Guid.NewGuid().ToString("N");SendFrame(tls,1,Encoding.UTF8.GetBytes(message));bool matched=false;for(int attempt=0;attempt<4;attempt++)if(Encoding.UTF8.GetString(ReadDataFrame(tls))==message){matched=true;break;}if(!matched)throw new IOException("WebSocket 已握手，但回显内容不匹配。");if(log!=null)log("持续通信 "+(i+1)+"/3 通过。");if(i<2)Thread.Sleep(1000);}
            SendFrame(tls,8,new byte[]{3,232});
        }
    }
}
internal sealed class WebSocketRelay : IDisposable {
    readonly TcpListener listener=new TcpListener(IPAddress.Loopback,0);
    readonly Uri http,websocket;readonly string dotHost;readonly Action<string> log;
    readonly object gate=new object();readonly List<TcpClient> clients=new List<TcpClient>();
    bool stopped;
    internal int Port {get{return ((IPEndPoint)listener.LocalEndpoint).Port;}}
    internal WebSocketRelay(Uri httpProxy,Uri websocketProxy,string target,Action<string> logger){http=httpProxy;websocket=websocketProxy;dotHost=target;log=logger;}
    internal void Start(){listener.Start(32);Task.Run((Action)Accept);}
    void Accept(){while(!stopped){try{var client=listener.AcceptTcpClient();lock(gate)clients.Add(client);Task.Run(()=>Handle(client));}catch(SocketException){if(!stopped)throw;}catch(ObjectDisposedException){return;}}}
    void Handle(TcpClient incoming){TcpClient upstream=null;try{incoming.ReceiveTimeout=10000;incoming.SendTimeout=10000;var stream=incoming.GetStream();var header=WebSocketNetwork.ReadHeader(stream);var first=header.Split('\n')[0].Trim().Split(' ');Uri target;bool tunnel=first.Length==3&&first[0]=="CONNECT";if(first.Length!=3||!Uri.TryCreate(tunnel?"http://"+first[1]:first[1],UriKind.Absolute,out target)||target.UserInfo.Length!=0||(tunnel?target.AbsolutePath!="/":target.Scheme!="http"))throw new IOException("本机线路要求有效的 HTTP 请求或 HTTPS CONNECT。");var selected=String.Equals(target.Host,dotHost,StringComparison.OrdinalIgnoreCase)?websocket:http;
            if(tunnel){upstream=WebSocketNetwork.Tunnel(selected,target.DnsSafeHost,target.Port);WebSocketNetwork.Write(stream,"HTTP/1.1 200 Connection Established\r\n\r\n");}
            else{upstream=WebSocketNetwork.Connect(selected==null?target.DnsSafeHost:selected.DnsSafeHost,selected==null?target.Port:selected.Port,8000);var request=header;if(selected==null)request=first[0]+" "+target.PathAndQuery+" "+first[2]+"\r\n"+header.Substring(header.IndexOf("\r\n")+2);WebSocketNetwork.Write(upstream.GetStream(),request);}
            lock(gate)clients.Add(upstream);incoming.ReceiveTimeout=0;upstream.ReceiveTimeout=0;if(log!=null)log(String.Equals(target.Host,dotHost,StringComparison.OrdinalIgnoreCase)?"Dot 远程目标已进入独立 WS 代理线路。":"其他目标保持原 HTTP 代理路线。");var one=stream.CopyToAsync(upstream.GetStream());var two=upstream.GetStream().CopyToAsync(stream);Task.WaitAny(one,two);if(one.IsFaulted)throw one.Exception.GetBaseException();if(two.IsFaulted)throw two.Exception.GetBaseException();}
        catch(Exception ex){if(log!=null&&!stopped)log("本机线路连接未完成："+ex.Message);try{WebSocketNetwork.Write(incoming.GetStream(),"HTTP/1.1 502 Bad Gateway\r\nConnection: close\r\nContent-Length: 0\r\n\r\n");}catch(IOException){}catch(ObjectDisposedException){}catch(SocketException){}}
        finally{incoming.Close();if(upstream!=null)upstream.Close();lock(gate){clients.Remove(incoming);if(upstream!=null)clients.Remove(upstream);}}
    }
    public void Dispose(){stopped=true;listener.Stop();lock(gate){foreach(var client in clients)client.Close();clients.Clear();}}
}
}
