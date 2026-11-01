using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace CodexKit {
public sealed class GitHubNetworkOptions {
    public bool githubMirrorEnabled {get;set;}
    public string customGithubMirror {get;set;}
    public GitHubNetworkOptions(){githubMirrorEnabled=true;customGithubMirror="";}
    public static string NormalizePrefix(string value){
        if(String.IsNullOrWhiteSpace(value))return "";
        Uri uri;
        if(!Uri.TryCreate(value.Trim(),UriKind.Absolute,out uri)||uri.Scheme!="https"||uri.UserInfo.Length!=0||uri.Query.Length!=0||uri.Fragment.Length!=0)throw new ArgumentException("自定义镜像需填写 HTTPS 前缀，不能包含账号密码、查询参数或片段。");
        return uri.AbsoluteUri.TrimEnd('/')+"/";
    }
    public static GitHubNetworkOptions Load(string path){
        if(!File.Exists(path))return new GitHubNetworkOptions();
        var value=new JavaScriptSerializer().Deserialize<GitHubNetworkOptions>(File.ReadAllText(path,Encoding.UTF8));
        if(value==null)throw new InvalidDataException("GitHub 下载设置无法读取，请在更新页重新保存。");
        value.customGithubMirror=NormalizePrefix(value.customGithubMirror);return value;
    }
    public void Save(string path){customGithubMirror=NormalizePrefix(customGithubMirror);Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));File.WriteAllText(path,new JavaScriptSerializer().Serialize(this),new UTF8Encoding(false));}
}
public sealed class GitHubTransfer {
    static readonly string[] Mirrors={"https://ghfast.top/","https://gh-proxy.com/","https://ghproxy.net/","https://gh.llkk.cc/","https://ghproxy.homeboyc.cn/"};
    readonly GitHubNetworkOptions options;
    readonly Action<string> log;
    public int ProxyPort {get;private set;}
    public string ProxyHost {get;private set;}
    internal Func<string,string> ReadText;
    internal Action<string,string> ReadFile;
    public GitHubTransfer(GitHubNetworkOptions settings,int port,string host,Action<string> writeLog){options=settings??new GitHubNetworkOptions();ProxyPort=port;ProxyHost=host;log=writeLog;ReadText=ReadTextRequest;ReadFile=ReadFileRequest;}
    public static bool IsGitHub(string url){Uri uri;return Uri.TryCreate(url,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.UserInfo.Length==0&&(uri.Host=="github.com"||uri.Host=="api.github.com"||uri.Host=="raw.githubusercontent.com");}
    IEnumerable<string> Candidates(string original){
        yield return original;
        if(!options.githubMirrorEnabled||!IsGitHub(original))yield break;
        var prefixes=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var custom=GitHubNetworkOptions.NormalizePrefix(options.customGithubMirror);
        if(custom.Length>0&&prefixes.Add(custom))yield return custom+original;
        foreach(var mirror in Mirrors)if(prefixes.Add(mirror))yield return mirror+original;
    }
    void Write(string message){if(log!=null)log(message);}
    static string Failure(Exception ex){
        var web=ex as WebException;var response=web==null?null:web.Response as HttpWebResponse;
        if(response!=null)return "HTTP "+(int)response.StatusCode;
        if(ex is TimeoutException||(web!=null&&(web.Status==WebExceptionStatus.Timeout||web.Status==WebExceptionStatus.RequestCanceled)))return "请求超时";
        if(web!=null){switch(web.Status){case WebExceptionStatus.NameResolutionFailure:return "无法解析站点地址";case WebExceptionStatus.ProxyNameResolutionFailure:return "无法解析所选代理地址";case WebExceptionStatus.ConnectFailure:return "无法连接站点或所选代理";case WebExceptionStatus.TrustFailure:case WebExceptionStatus.SecureChannelFailure:return "无法建立有效的 HTTPS 连接，请检查证书、网络和代理";case WebExceptionStatus.ReceiveFailure:case WebExceptionStatus.ConnectionClosed:return "服务器连接中断，文件未下载完整";}}
        if(ex is InvalidDataException)return ex.Message;
        return "连接或读取失败（"+ex.GetType().Name+"）："+ex.Message;
    }
    public string Fetch(string original,Func<string,bool> validate){
        Exception last=null;
        foreach(var url in Candidates(original)){
            Write("GitHub 请求："+(url==original?"官方地址":new Uri(url).Host));
            try{var value=ReadText(url);if(LooksLikeHtml(value)||!validate(value))throw new InvalidDataException("返回内容不是有效的更新数据。");Write("GitHub 请求完成："+new Uri(url).Host);return value;}
            catch(Exception ex){last=ex;Write("当前通道未完成："+Failure(ex)+"；如有后续通道，将串行切换。");}
        }
        throw new IOException("所有 GitHub 请求通道均未完成。"+(last==null?"":Failure(last)),last);
    }
    public void Download(string original,string path,Action<string> validate){
        Exception last=null;
        foreach(var url in Candidates(original)){
            Write("GitHub 下载："+(url==original?"官方地址":new Uri(url).Host));
            try{ReadFile(url,path);using(var reader=new StreamReader(path,Encoding.UTF8,true,256)){var prefix=new char[128];int count=reader.Read(prefix,0,prefix.Length);if(LooksLikeHtml(new string(prefix,0,count)))throw new InvalidDataException("下载返回了网页，未作为文件使用。");}validate(path);Write("GitHub 下载及校验完成："+new Uri(url).Host);return;}
            catch(Exception ex){last=ex;if(File.Exists(path))File.Delete(path);Write("当前通道未完成："+Failure(ex)+"；如有后续通道，将串行切换。");}
        }
        throw new IOException("所有 GitHub 下载通道均未完成。"+(last==null?"":Failure(last)),last);
    }
    static bool LooksLikeHtml(string value){var text=value.TrimStart('\uFEFF',' ','\r','\n','\t');return text.StartsWith("<!doctype html",StringComparison.OrdinalIgnoreCase)||text.StartsWith("<html",StringComparison.OrdinalIgnoreCase);}
    HttpWebRequest Request(string url,int deadline,int idle){
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
        var request=(HttpWebRequest)WebRequest.Create(url);request.UserAgent="CodexInitKitDesktop";request.Timeout=deadline;request.ReadWriteTimeout=idle;
        request.UseDefaultCredentials=false;
        request.Proxy=ProxyPort>0?new WebProxy("http://"+(ProxyHost.Contains(":")?"["+ProxyHost+"]":ProxyHost)+":"+ProxyPort):null;
        return request;
    }
    string ReadTextRequest(string url){
        var request=Request(url,12000,12000);
        using(var timer=new Timer(s=>request.Abort(),null,12000,Timeout.Infinite))
        using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))return reader.ReadToEnd();
    }
    void ReadFileRequest(string url,string path){
        var request=Request(url,30000,30000);
        using(var timer=new Timer(s=>request.Abort(),null,300000,Timeout.Infinite))
        using(var response=request.GetResponse())using(var input=response.GetResponseStream())using(var output=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None))input.CopyTo(output);
    }
}
}
