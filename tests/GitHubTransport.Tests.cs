using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace CodexKit {
public static class GitHubTransportTests {
    static void Assert(bool condition,string message){if(!condition)throw new InvalidDataException(message);}
    public static void Run(string directory){
        const string original="https://github.com/BerryFuwawa/codex-init-kit/releases/download/v2.2.5/CodexInitKit.exe";
        var settings=new GitHubNetworkOptions{customGithubMirror="https://example.com/prefix"};
        var calls=new List<string>();var messages=new List<string>();
        var transfer=new GitHubTransfer(settings,0,"127.0.0.1",messages.Add);
        transfer.ReadText=url=>{calls.Add(url);if(calls.Count==1)throw new TimeoutException();return calls.Count==2?"<html>blocked</html>":"{\"ok\":true}";};
        var text=transfer.Fetch("https://api.github.com/repos/BerryFuwawa/codex-init-kit/commits/main",s=>s.Contains("ok"));
        Assert(text.Contains("ok")&&calls.Count==3&&calls[1].StartsWith("https://example.com/prefix/https://api.github.com/")&&calls[2].StartsWith("https://ghfast.top/"),"Mirror sequence or invalid-response fallback failed");
        calls.Clear();transfer.ReadText=url=>{calls.Add(url);return "valid";};transfer.Fetch(original,s=>s=="valid");Assert(calls.Count==1&&calls[0]==original,"Official success unnecessarily contacted mirrors");
        settings.githubMirrorEnabled=false;calls.Clear();transfer.ReadText=url=>{calls.Add(url);throw new IOException("fixture");};bool rejected=false;try{transfer.Fetch(original,s=>true);}catch(IOException){rejected=true;}Assert(rejected&&calls.Count==1,"Disabled mirrors were used");
        settings.githubMirrorEnabled=true;calls.Clear();transfer.ReadText=url=>{calls.Add(url);throw new IOException("fixture");};rejected=false;try{transfer.Fetch(original,s=>true);}catch(IOException){rejected=true;}
        Assert(rejected&&calls.Count==7&&calls.Distinct().Count()==7&&calls.Last().StartsWith("https://ghproxy.homeboyc.cn/"),"Exhaustion repeated or omitted a candidate");
        var file=Path.Combine(directory,"mirror-download.bin");calls.Clear();
        transfer.ReadFile=(url,target)=>{calls.Add(url);File.WriteAllText(target,calls.Count<3?"bad":"good");};
        transfer.Download(original,file,p=>{if(File.ReadAllText(p)!="good")throw new InvalidDataException("fixture checksum mismatch");});
        Assert(calls.Count==3&&File.ReadAllText(file)=="good","Checksum failure did not retry the next candidate");
        transfer.ReadFile=(url,target)=>{File.WriteAllText(target,"partial");throw new TimeoutException();};rejected=false;try{transfer.Download(original,file,p=>{});}catch(IOException){rejected=true;}Assert(rejected&&!File.Exists(file),"Exhausted file download left an incomplete candidate");
        Assert(messages.Any(s=>s.Contains("切换"))&&messages.Any(s=>s.Contains("官方")),"Mirror diagnostics lack routing details");
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
    public GitHubFixtureServer(string body){
        listener=new TcpListener(IPAddress.Loopback,0);listener.Start();Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/";
        serve=Task.Run(()=>{using(var client=listener.AcceptTcpClient())using(var stream=client.GetStream()){
            stream.ReadTimeout=5000;var header=new StringBuilder();int current;while((current=stream.ReadByte())>=0){header.Append((char)current);if(header.ToString().EndsWith("\r\n\r\n",StringComparison.Ordinal))break;}
            Headers=header.ToString();var bytes=Encoding.UTF8.GetBytes(body);var prefix=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: "+bytes.Length+"\r\nConnection: close\r\n\r\n");stream.Write(prefix,0,prefix.Length);stream.Write(bytes,0,bytes.Length);
        }});
    }
    public void Dispose(){listener.Stop();if(!serve.Wait(5000))throw new IOException("Fixture HTTP server did not finish");}
}
}
