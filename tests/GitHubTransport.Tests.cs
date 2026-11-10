using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CodexKit {
public static class GitHubTransportTests {
    static void Assert(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
    public static void Run(string directory){
        const string original="https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.5/CodexInitKit.exe";
        const string customPrefix="https://example.com/prefix/";
        const string api="https://api.github.com/repos/BerryFuwawa/codex-init-kit/commits/main";
        var settings=new GitHubNetworkOptions{customGithubMirror="https://example.com/prefix"};
        var calls=new List<string>();var messages=new List<string>();
        var transfer=new GitHubTransfer(settings,0,"127.0.0.1",messages.Add);
        var probes=new ConcurrentBag<string>();
        transfer.ReadText=url=>{calls.Add(url);if(url==customPrefix+api)return "<html>blocked</html>";throw new TimeoutException();};
        using(var parallel=new Barrier(10)){
            transfer.ProbeText=(url,token)=>{
                Assert(calls.SequenceEqual(new[]{api,customPrefix+api}),"Built-in probe ran before the custom request finished");
                probes.Add(url);
                if(!parallel.SignalAndWait(4000,token))throw new IOException("Probes did not overlap");
                if(url.StartsWith("https://ghfast.top/")){Thread.Sleep(100);return "valid-slower";}
                if(url.StartsWith("https://gh-proxy.com/")){Thread.Sleep(20);return "valid-fastest";}
                return "{}";
            };
            var text=transfer.Fetch(api,s=>s.StartsWith("valid-"));
            Assert(text=="valid-fastest"&&probes.Distinct().Count()==10&&probes.All(url=>!url.StartsWith(customPrefix)),"Parallel probes or fastest valid response selection failed");
        }
        int before=probes.Count;
        calls.Clear();transfer.ReadText=url=>{calls.Add(url);return "valid";};transfer.Fetch(original,s=>s=="valid");Assert(calls.Count==1&&calls[0]==original&&probes.Count==before,"Official success unnecessarily contacted mirrors");
        calls.Clear();transfer.ReadText=url=>{calls.Add(url);return url==original?"invalid":"valid";};
        Assert(transfer.Fetch(original,s=>s=="valid")=="valid"&&calls.SequenceEqual(new[]{original,customPrefix+original})&&probes.Count==before,"Custom success unnecessarily contacted built-in mirrors");
        settings.githubMirrorEnabled=false;calls.Clear();transfer.ReadText=url=>{calls.Add(url);throw new IOException("fixture");};bool rejected=false;try{transfer.Fetch(original,s=>true);}catch(IOException){rejected=true;}Assert(rejected&&calls.Count==1,"Disabled mirrors were used");
        settings.githubMirrorEnabled=true;calls.Clear();probes=new ConcurrentBag<string>();
        transfer.ProbeText=(url,token)=>{probes.Add(url);throw new IOException("fixture");};
        rejected=false;try{transfer.Fetch(original,s=>true);}catch(IOException){rejected=true;}
        Assert(rejected&&calls.SequenceEqual(new[]{original,customPrefix+original})&&probes.Distinct().Count()==10&&probes.Any(url=>url.StartsWith("https://github.zzrbk.xyz/")),"Exhaustion repeated or omitted a candidate");
        calls.Clear();probes=new ConcurrentBag<string>();settings.customGithubMirror="";
        try{transfer.Fetch(original,s=>true);}catch(IOException){}
        Assert(calls.SequenceEqual(new[]{original})&&probes.Count==10,"Empty custom mirror was not skipped");
        calls.Clear();probes=new ConcurrentBag<string>();settings.customGithubMirror="https://ghfast.top";
        try{transfer.Fetch(original,s=>true);}catch(IOException){}
        Assert(calls.SequenceEqual(new[]{original,"https://ghfast.top/"+original})&&probes.Count==9&&!probes.Any(url=>url.StartsWith("https://ghfast.top/")),"Built-in configured as custom was tried twice");
        settings.customGithubMirror=customPrefix;
        var file=Path.Combine(directory,"mirror-download.bin");calls.Clear();
        int fileProbes=0;
        transfer.ProbeFile=(url,official,token)=>Interlocked.Increment(ref fileProbes);
        transfer.ReadFile=(url,target)=>{calls.Add(url);File.WriteAllText(target,url==customPrefix+original?"good":"bad");};
        transfer.Download(original,file,p=>{if(File.ReadAllText(p)!="good")throw new InvalidDataException("fixture checksum mismatch");});
        Assert(calls.SequenceEqual(new[]{original,customPrefix+original})&&fileProbes==0,"Custom download did not precede built-in probes");
        calls.Clear();
        transfer.ProbeFile=(url,official,token)=>{
            if(url.StartsWith("https://ghfast.top/")){Thread.Sleep(20);return;}
            if(url.StartsWith("https://gh-proxy.com/")){Thread.Sleep(100);return;}
            throw new InvalidDataException("Invalid asset fixture");
        };
        transfer.ReadFile=(url,target)=>{calls.Add(url);File.WriteAllText(target,calls.Count<4?"bad":"good");};
        transfer.Download(original,file,p=>{if(File.ReadAllText(p)!="good")throw new InvalidDataException("fixture checksum mismatch");});
        Assert(calls.SequenceEqual(new[]{original,customPrefix+original,"https://ghfast.top/"+original,"https://gh-proxy.com/"+original})&&File.ReadAllText(file)=="good","Checksum failure did not retry the next fastest valid candidate");
        transfer.ReadFile=(url,target)=>{File.WriteAllText(target,"partial");throw new TimeoutException();};rejected=false;try{transfer.Download(original,file,p=>{});}catch(IOException){rejected=true;}Assert(rejected&&!File.Exists(file),"Exhausted file download left an incomplete candidate");
        Assert(messages.Any(s=>s.Contains("切换"))&&messages.Any(s=>s.Contains("官方")),"Mirror diagnostics lack routing details");
        calls.Clear();probes=new ConcurrentBag<string>();
        var manifest=Core.Json.Serialize(new { repository=Core.Repository,schema_version=1,version="2.2.10",url="https://github.com/"+Core.Repository+"/releases/download/v2.2.10/CodexInitKit.exe",sha256=new string('0',64) });
        transfer.ReadText=url=>{calls.Add(url);if(url.Contains("/commits/"))throw new IOException("fixture");return manifest;};
        var discovered=Core.CheckUpdate(transfer,messages.Add);
        Assert(Core.Value(discovered,"version","")=="2.2.10"&&calls.Count==3&&probes.Count==10&&calls[2].EndsWith("/main/desktop-update.json"),"Unavailable commit API did not fall back to the validated public main manifest");
        foreach(var status in new[]{"403 Forbidden","429 Too Many Requests"}){
            using(var server=new GitHubFixtureServer("blocked",status)){
                var denied=new GitHubTransfer(settings,0,"127.0.0.1",messages.Add);
                var http=denied.ReadText;
                calls.Clear();probes=new ConcurrentBag<string>();
                denied.ReadText=url=>{calls.Add(url);return url.Contains("/commits/")?http(server.Url+"api"):manifest;};
                denied.ProbeText=(url,token)=>{probes.Add(url);throw new IOException("Unexpected mirror");};
                Assert(Core.Value(Core.CheckUpdate(denied,messages.Add),"version","")=="2.2.10"&&calls.Count==2&&probes.IsEmpty,"A refused GitHub API did not try the official manifest before mirrors");
            }
        }
        using(var server=new GitHubFixtureServer("blocked","403 Forbidden")){
            var denied=new GitHubTransfer(settings,0,"127.0.0.1",messages.Add);
            var http=denied.ReadText;
            calls.Clear();
            denied.ReadText=url=>{calls.Add(url);if(url==api)return http(server.Url+"api");throw new IOException("Route unavailable");};
            denied.ProbeText=(url,token)=>{if(url.Contains("/commits/"))throw new IOException("Mirror API unavailable");return manifest;};
            Assert(Core.Value(Core.CheckUpdate(denied,messages.Add),"version","")=="2.2.10"&&calls.Count==4&&calls[1].StartsWith("https://raw.githubusercontent.com/")&&calls.Skip(2).All(url=>url.StartsWith(customPrefix)),"Raw mirror fallback repeated or skipped an official route");
        }
        using(var release=new ManualResetEventSlim())using(var ended=new ManualResetEventSlim()){
            transfer.ProbeText=(url,token)=>{
                if(url.StartsWith("https://ghfast.top/"))return "available";
                if(!url.StartsWith("https://gh-proxy.com/"))throw new IOException("fixture");
                using(token.Register(()=>release.Set()))release.Wait(15000);
                ended.Set();
                throw new OperationCanceledException();
            };
            var timer=Stopwatch.StartNew();
            Assert(transfer.Fetch(original,s=>s=="available")=="available"&&timer.ElapsedMilliseconds<12000&&ended.Wait(1000),"A slow mirror held the probe round open after cancellation");
        }
        foreach(var prefix in new[]{"http://example.com/","https://user:pass@example.com/","https://example.com/?token=secret","https://example.com/#fragment"}){rejected=false;try{GitHubNetworkOptions.NormalizePrefix(prefix);}catch(ArgumentException){rejected=true;}Assert(rejected,"Invalid mirror prefix accepted");}
        Assert(GitHubNetworkOptions.NormalizePrefix("https://example.com/path")=="https://example.com/path/","Custom path prefix was not preserved");
        var settingsFile=Path.Combine(directory,"network.json");settings.Save(settingsFile);var loaded=GitHubNetworkOptions.Load(settingsFile);Assert(loaded.githubMirrorEnabled&&loaded.customGithubMirror=="https://example.com/prefix/","Network settings did not survive reload");
        Assert(new GitHubNetworkOptions().githubMirrorEnabled&&GitHubNetworkOptions.Load(Path.Combine(directory,"absent.json")).customGithubMirror=="","Mirror defaults changed");
        Assert(transfer.ProxyPort==0&&transfer.ProxyHost=="127.0.0.1","Transport changed direct-connection settings");
        var proxied=new GitHubTransfer(settings,7890,"192.168.1.2",null);Assert(proxied.ProxyPort==7890&&proxied.ProxyHost=="192.168.1.2","Transport overwrote the selected proxy");
        using(var server=new GitHubFixtureServer("{\"ok\":true}")){
            var actual=new GitHubTransfer(new GitHubNetworkOptions(),0,"127.0.0.1",null);
            Assert(actual.Fetch(server.Url+"metadata",s=>s.Contains("ok")).Contains("ok"),"Real HTTP metadata transport failed");
            Assert(server.Headers.IndexOf("Authorization:",StringComparison.OrdinalIgnoreCase)<0,"Public transport sent credentials");
        }
        using(var server=new GitHubFixtureServer("downloaded file")){
            new GitHubTransfer(settings,0,"127.0.0.1",null).Download(server.Url+"asset",file,p=>Assert(File.ReadAllText(p)=="downloaded file","Real streamed file contents changed"));
        }
        using(var server=new GitHubFixtureServer("MZ"+new string('x',5000))){
            new GitHubTransfer(settings,0,"127.0.0.1",null).ProbeFile(server.Url+"asset",original,CancellationToken.None);
            Assert(server.Headers.IndexOf("Range: bytes=0-4095",StringComparison.OrdinalIgnoreCase)>=0,"Asset probe did not request only the first 4 KiB");
        }
        using(var server=new GitHubFixtureServer("<html>server error</html>")){
            rejected=false;try{new GitHubTransfer(settings,0,"127.0.0.1",null).Download(server.Url+"asset",file,p=>{});}catch(IOException){rejected=true;}
            Assert(rejected&&!File.Exists(file),"Real HTTP error page was accepted as an asset");
        }
    }
}
public sealed class GitHubFixtureServer:IDisposable {
    readonly TcpListener listener;
    readonly Task serve;
    public string Url {get;private set;}
    public string Headers {get;private set;}
    public GitHubFixtureServer(string body,string status="200 OK"){
        listener=new TcpListener(IPAddress.Loopback,0);listener.Start();Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/";
        serve=Task.Run(()=>{using(var client=listener.AcceptTcpClient())using(var stream=client.GetStream()){
            stream.ReadTimeout=5000;var header=new StringBuilder();int current;while((current=stream.ReadByte())>=0){header.Append((char)current);if(header.ToString().EndsWith("\r\n\r\n",StringComparison.Ordinal))break;}
            Headers=header.ToString();var bytes=Encoding.UTF8.GetBytes(body);var prefix=Encoding.ASCII.GetBytes("HTTP/1.1 "+status+"\r\nContent-Length: "+bytes.Length+"\r\nConnection: close\r\n\r\n");stream.Write(prefix,0,prefix.Length);stream.Write(bytes,0,bytes.Length);
        }});
    }
    public void Dispose(){listener.Stop();if(!serve.Wait(5000))throw new IOException("Fixture HTTP server did not finish");}
}
}
