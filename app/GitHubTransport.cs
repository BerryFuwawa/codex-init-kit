using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
    const int ProbeTimeout=8000;
    const int ProbeBytes=4096;
    static readonly string[] Mirrors={
        // Common community download routes.
        "https://ghfast.top/","https://gh-proxy.com/","https://ghproxy.net/","https://ghproxy.vip/","https://cors.isteed.cc/",
        // Additional routes selected by testing this project's raw and release assets.
        "https://github.cnxiaobai.com/","https://ghproxy.oscode.top/","https://git.820828.xyz/","https://github.zzrbk.xyz/","https://gh.noki.icu/"
    };
    internal static readonly string BuiltInMirrorNames=String.Join("、",Mirrors.Select(prefix=>new Uri(prefix).Host));
    readonly GitHubNetworkOptions options;
    readonly Action<string> log;
    public int ProxyPort {get;private set;}
    public string ProxyHost {get;private set;}
    internal Func<string,string> ReadText;
    internal Func<string,CancellationToken,string> ProbeText;
    internal Action<string,string,CancellationToken> ProbeFile;
    internal Action<string,string> ReadFile;
    public GitHubTransfer(GitHubNetworkOptions settings,int port,string host,Action<string> writeLog){
        options=settings??new GitHubNetworkOptions();ProxyPort=port;ProxyHost=host;log=writeLog;
        ReadText=url=>ReadTextRequest(url,12000,CancellationToken.None);
        ProbeText=(url,token)=>ReadTextRequest(url,ProbeTimeout,token);
        ProbeFile=ProbeFileRequest;ReadFile=ReadFileRequest;
    }
    public static bool IsGitHub(string url){Uri uri;return Uri.TryCreate(url,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.UserInfo.Length==0&&(uri.Host=="github.com"||uri.Host=="api.github.com"||uri.Host=="raw.githubusercontent.com");}
    string CustomMirrorUrl(string original){
        if(!options.githubMirrorEnabled||!IsGitHub(original))return null;
        var prefix=GitHubNetworkOptions.NormalizePrefix(options.customGithubMirror);
        return prefix.Length==0?null:prefix+original;
    }
    void Write(string message){if(log!=null)log(message);}
    sealed class ProbeResult<T> {
        public string Url;
        public TimeSpan Latency;
        public T Value;
        public Exception Error;
    }
    IList<ProbeResult<T>> ProbeMirrors<T>(string original,string custom,Func<string,CancellationToken,T> probe){
        if(!options.githubMirrorEnabled||!IsGitHub(original))return new ProbeResult<T>[0];
        var urls=Mirrors.Select(prefix=>prefix+original).Where(url=>!String.Equals(url,custom,StringComparison.OrdinalIgnoreCase)).ToArray();
        if(urls.Length==0)return new ProbeResult<T>[0];
        Write("并行检测 "+urls.Length+" 个内置 GitHub 镜像（最多 8 秒）。");
        using(var cancel=new CancellationTokenSource()){
            var token=cancel.Token;
            var tasks=urls.Select(url=>Task.Factory.StartNew(()=>{
                var result=new ProbeResult<T>{Url=url};
                var elapsed=Stopwatch.StartNew();
                try{result.Value=probe(url,token);result.Latency=elapsed.Elapsed;}
                catch(Exception ex){result.Error=ex;}
                return result;
            },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default)).ToArray();
            Task.WaitAll(tasks,ProbeTimeout);
            // Take the completed results before cancellation can change task state.
            var complete=new List<ProbeResult<T>>();
            for(int i=0;i<tasks.Length;i++){
                if(tasks[i].Status==TaskStatus.RanToCompletion)complete.Add(tasks[i].Result);
                else Write("镜像检测超时："+new Uri(urls[i]).Host);
            }
            cancel.Cancel();
            foreach(var result in complete.Where(x=>x.Error!=null))
                Write("镜像不可用："+new Uri(result.Url).Host+" · "+Failure(result.Error));
            return complete.Where(x=>x.Error==null&&x.Latency.TotalMilliseconds<=ProbeTimeout)
                .OrderBy(x=>x.Latency).ToArray();
        }
    }
    static string Failure(Exception ex){
        var web=ex as WebException;var response=web==null?null:web.Response as HttpWebResponse;
        if(response!=null){using(response)return "HTTP "+(int)response.StatusCode;}
        if(ex is TimeoutException||(web!=null&&(web.Status==WebExceptionStatus.Timeout||web.Status==WebExceptionStatus.RequestCanceled)))return "请求超时";
        if(web!=null){switch(web.Status){case WebExceptionStatus.NameResolutionFailure:return "无法解析站点地址";case WebExceptionStatus.ProxyNameResolutionFailure:return "无法解析所选代理地址";case WebExceptionStatus.ConnectFailure:return "无法连接站点或所选代理";case WebExceptionStatus.TrustFailure:case WebExceptionStatus.SecureChannelFailure:return "无法建立有效的 HTTPS 连接，请检查证书、网络和代理";case WebExceptionStatus.ReceiveFailure:case WebExceptionStatus.ConnectionClosed:return "服务器连接中断，文件未下载完整";}}
        if(ex is InvalidDataException)return ex.Message;
        return "连接或读取失败（"+ex.GetType().Name+"）："+ex.Message;
    }
    public string Fetch(string original,Func<string,bool> validate){
        string failure;HttpStatusCode? status;
        return FetchOfficial(original,validate,out failure,out status)??FetchMirrors(original,validate,failure);
    }
    internal string FetchOfficial(string original,Func<string,bool> validate,out string failure,out HttpStatusCode? status){
        failure=null;status=null;
        Write("GitHub 请求：官方地址");
        try{
            var value=ReadText(original);ValidateText(value,validate);
            Write("GitHub 请求完成："+new Uri(original).Host);return value;
        }catch(Exception ex){
            var web=ex as WebException;var response=web==null?null:web.Response as HttpWebResponse;
            if(response!=null)status=response.StatusCode;
            failure=Failure(ex);
            Write("官方请求未完成："+failure);
            return null;
        }
    }
    internal string FetchMirrors(string original,Func<string,bool> validate,string last){
        var custom=CustomMirrorUrl(original);
        if(custom!=null){
            var host=new Uri(custom).Host;
            Write("GitHub 请求：自定义镜像 "+host);
            try{
                var value=ReadText(custom);ValidateText(value,validate);
                Write("GitHub 请求完成："+host);return value;
            }catch(Exception ex){last=Failure(ex);Write("自定义镜像请求未完成："+last);}
        }
        foreach(var result in ProbeMirrors(original,custom,ProbeText)){
            try{ValidateText(result.Value,validate);}
            catch(Exception ex){last=Failure(ex);Write("镜像内容无效："+new Uri(result.Url).Host+" · "+last);continue;}
            Write("选用镜像："+new Uri(result.Url).Host+" · "+result.Latency.TotalMilliseconds.ToString("0")+" ms");
            return result.Value;
        }
        throw new IOException("所有 GitHub 请求通道均未完成。"+last);
    }
    static void ValidateText(string value,Func<string,bool> validate){
        if(String.IsNullOrWhiteSpace(value)||LooksLikeHtml(value)||!validate(value))
            throw new InvalidDataException("返回内容不是有效的 GitHub 数据。");
    }
    public void Download(string original,string path,Action<string> validate){
        Write("GitHub 下载：官方地址");
        var last=TryDownload(original,path,validate);
        if(last==null)return;
        var custom=CustomMirrorUrl(original);
        if(custom!=null){
            Write("GitHub 下载：自定义镜像 "+new Uri(custom).Host);
            last=TryDownload(custom,path,validate);
            if(last==null)return;
        }
        foreach(var result in ProbeMirrors(original,custom,(url,token)=>{ProbeFile(url,original,token);return true;})){
            Write("选用镜像："+new Uri(result.Url).Host+" · "+result.Latency.TotalMilliseconds.ToString("0")+" ms");
            last=TryDownload(result.Url,path,validate);
            if(last==null)return;
        }
        throw new IOException("所有 GitHub 下载通道均未完成。"+last);
    }
    string TryDownload(string url,string path,Action<string> validate){
        try{
            ReadFile(url,path);
            using(var reader=new StreamReader(path,Encoding.UTF8,true,256)){
                var prefix=new char[128];
                int count=reader.Read(prefix,0,prefix.Length);
                if(LooksLikeHtml(new string(prefix,0,count)))throw new InvalidDataException("下载返回了网页，未作为文件使用。");
            }
            validate(path);Write("GitHub 下载及校验完成："+new Uri(url).Host);return null;
        }catch(Exception ex){
            if(File.Exists(path))File.Delete(path);
            var failure=Failure(ex);
            Write("当前下载未完成："+new Uri(url).Host+" · "+failure+"。如有下一候选，将切换。");
            return failure;
        }
    }
    static bool LooksLikeHtml(string value){var text=value.TrimStart('\uFEFF',' ','\r','\n','\t');return text.StartsWith("<!doctype html",StringComparison.OrdinalIgnoreCase)||text.StartsWith("<html",StringComparison.OrdinalIgnoreCase);}
    HttpWebRequest Request(string url,int deadline,int idle){
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
        var request=(HttpWebRequest)WebRequest.Create(url);request.UserAgent="CodexInitKitDesktop";request.Timeout=deadline;request.ReadWriteTimeout=idle;
        request.UseDefaultCredentials=false;
        request.Proxy=ProxyPort>0?new WebProxy("http://"+(ProxyHost.Contains(":")?"["+ProxyHost+"]":ProxyHost)+":"+ProxyPort):null;
        return request;
    }
    HttpWebRequest ProbeRequest(string url,int timeout){
        var request=Request(url,timeout,timeout);
        // Mirrors can share a service point through the user's proxy.
        var servicePoint=request.ServicePoint;
        servicePoint.ConnectionLimit=Math.Max(servicePoint.ConnectionLimit,Mirrors.Length+1);
        return request;
    }
    string ReadTextRequest(string url,int timeout,CancellationToken token){
        var request=token.CanBeCanceled?ProbeRequest(url,timeout):Request(url,timeout,timeout);
        using(var registration=token.Register(request.Abort))
        using(var timer=new Timer(s=>request.Abort(),null,timeout,Timeout.Infinite))
        using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8))return reader.ReadToEnd();
    }
    void ProbeFileRequest(string url,string original,CancellationToken token){
        var request=ProbeRequest(url,ProbeTimeout);
        request.AddRange(0,ProbeBytes-1);
        using(var registration=token.Register(request.Abort))
        using(var response=(HttpWebResponse)request.GetResponse())
        using(var stream=response.GetResponseStream()){
            if(response.StatusCode==HttpStatusCode.PartialContent&&
                !(response.Headers[HttpResponseHeader.ContentRange]??"").StartsWith("bytes 0-",StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("镜像返回的文件分片起点不正确。");
            var bytes=new byte[ProbeBytes];
            int length=0,read;
            while(length<bytes.Length&&(read=stream.Read(bytes,length,bytes.Length-length))>0)length+=read;
            var extension=Path.GetExtension(new Uri(original).AbsolutePath);
            bool valid=length>0&&!LooksLikeHtml(Encoding.UTF8.GetString(bytes,0,length));
            if(extension.Equals(".exe",StringComparison.OrdinalIgnoreCase))valid=length>=2&&bytes[0]==0x4d&&bytes[1]==0x5a;
            if(extension.Equals(".zip",StringComparison.OrdinalIgnoreCase))valid=length>=4&&bytes[0]==0x50&&bytes[1]==0x4b&&bytes[2]==3&&bytes[3]==4;
            if(!valid)throw new InvalidDataException("镜像返回的内容不是所需安装包。");
        }
    }
    void ReadFileRequest(string url,string path){
        var request=Request(url,30000,30000);
        using(var timer=new Timer(s=>request.Abort(),null,300000,Timeout.Infinite))
        using(var response=request.GetResponse())using(var input=response.GetResponseStream())using(var output=new FileStream(path,FileMode.Create,FileAccess.Write,FileShare.None))input.CopyTo(output);
    }
}
}
