using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

namespace CodexKit {
public sealed class WebSocketSettings {
    public bool websocketRepair {get;set;}
    public string wsProxyHost {get;set;}
    public string wsProxyPort {get;set;}
    public int httpPort {get;set;}
    public string httpHost {get;set;}
    public WebSocketSettings(){websocketRepair=true;wsProxyHost="";wsProxyPort="";httpHost="127.0.0.1";}
    public static string SettingsPath {get{return Path.Combine(Core.Root,"websocket.json");}}
    internal static string StatePath {get{return Path.Combine(Core.Root,"websocket-launch.json");}}
    public static WebSocketSettings Load(string path){if(!File.Exists(path))return new WebSocketSettings();var result=new JavaScriptSerializer().Deserialize<WebSocketSettings>(File.ReadAllText(path));if(result==null)throw new InvalidDataException("WebSocket 代理设置无法读取。");result.wsProxyHost=(result.wsProxyHost??"").Trim();result.wsProxyPort=(result.wsProxyPort??"").Trim();return result;}
    public void Save(string path){Resolve();Directory.CreateDirectory(Path.GetDirectoryName(path));var temp=path+".new";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(this),new System.Text.UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,path+".previous");else File.Move(temp,path);}
    internal Uri HttpProxy {get{return httpPort>0?Address(httpHost,httpPort):null;}}
    internal Uri Resolve(){
        if(!websocketRepair)return null;
        string host=(wsProxyHost??"").Trim(),portText=(wsProxyPort??"").Trim();
        if(host.Length==0&&portText.Length==0&&httpPort==0)return null;
        if(httpPort==0&&(host.Length==0||portText.Length==0))throw new ArgumentException("HTTP 代理已关闭，请完整填写 WS 代理 IP 和端口。");
        int port=httpPort;if(portText.Length>0&&(!Int32.TryParse(portText,out port)||port<1||port>65535))throw new ArgumentException("WS 代理端口需为 1–65535 的整数。");
        return Address(host.Length==0?httpHost:host,port);
    }
    internal bool Independent {get{var ws=Resolve();return ws!=null&&(HttpProxy==null||ws!=HttpProxy);}}
    internal string Description {get{var proxy=Resolve();return proxy==null?"未配置代理；仍可检查当前直连线路":proxy.Authority+(Independent?" · 独立 WS 代理":" · 沿用 HTTP 代理");}}
    internal static Uri Address(string host,int port){IPAddress ip;if(!IPAddress.TryParse(String.IsNullOrWhiteSpace(host)?"127.0.0.1":host.Trim(),out ip))throw new ArgumentException("WS 代理 IP 必须是有效的 IPv4 或 IPv6 地址。");if(port<1||port>65535)throw new ArgumentException("代理端口需为 1–65535 的整数。");return new UriBuilder("http",ip.ToString(),port).Uri;}
    internal static WebSocketSettings FromOptions(Dictionary<string,object> options,int port){return new WebSocketSettings{websocketRepair=Convert.ToBoolean(options["websocketRepair"]),wsProxyHost=Core.Value(options,"wsProxyHost",""),wsProxyPort=Core.Value(options,"wsProxyPort",""),httpHost=Core.Value(options,"proxyHost","127.0.0.1"),httpPort=port};}
    internal static Dictionary<string,object> Options(WebSocketSettings settings){return new Dictionary<string,object>{{"websocketRepair",settings.websocketRepair},{"wsProxyHost",settings.wsProxyHost??""},{"wsProxyPort",settings.wsProxyPort??""},{"proxyHost",settings.httpHost??"127.0.0.1"}};}
}
}
