using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CodexKit {
public sealed class ProxyPortPreset {
    public readonly string Name;
    public readonly int Port;
    public ProxyPortPreset(string name,int port){Name=name;Port=port;}
    public override string ToString(){return Name;}
}

public static class ProxyPortSettings {
    public static readonly ProxyPortPreset[] Presets={
        new ProxyPortPreset("v2rayN 新版",10808),
        new ProxyPortPreset("v2rayN 旧版",10809),
        new ProxyPortPreset("Karing",3067),
        new ProxyPortPreset("Karing 全代理入口",3066),
        new ProxyPortPreset("Clash Verge Rev",7897),
        new ProxyPortPreset("Clash／Mihomo 常见配置",7890),
        new ProxyPortPreset("Hiddify",12334),
        new ProxyPortPreset("NekoRay",2080),
        new ProxyPortPreset("自定义代理端口",0)
    };

    public static int Load(string home) {
        string text;
        try{text=File.ReadAllText(Path.Combine(home,".env"),new UTF8Encoding(false,true));}
        catch(FileNotFoundException){return 10808;}
        catch(DirectoryNotFoundException){return 10808;}
        var values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var line in Regex.Split(text,@"\r\n|\n|\r")){
            var entry=Regex.Match(line,@"^\s*(?:export\s+)?(HTTP_PROXY|HTTPS_PROXY)\s*=\s*(.*)$",RegexOptions.IgnoreCase);
            if(entry.Success)values[entry.Groups[1].Value]=entry.Groups[2].Value;
        }
        var address=ReadValue(values,"HTTP_PROXY");
        if(address.Length==0)address=ReadValue(values,"HTTPS_PROXY");
        if(address.Length==0)return 10808;
        var explicitPort=Regex.Match(address,@"^https?://(?:[^/?#@\s]+@)?(?:\[[^\]\s]+\]|[^:/?#\s]+):(?<port>[0-9]{1,5})/?$",RegexOptions.IgnoreCase);
        Uri uri;int port;
        if(!explicitPort.Success||!Uri.TryCreate(address,UriKind.Absolute,out uri)||
           String.IsNullOrEmpty(uri.Host)||!Int32.TryParse(explicitPort.Groups["port"].Value,out port)||port<1||port>65535)
            throw new InvalidDataException("已有代理地址的端口无效，请选择预设或填写 1–65535 的自定义端口。");
        return port;
    }

    static string ReadValue(Dictionary<string,string> values,string key) {
        string raw;
        if(!values.TryGetValue(key,out raw))return "";
        var match=Regex.Match(raw,@"^(?:""(?<value>[^""\r\n]*)""|'(?<value>[^'\r\n]*)'|(?<value>[^\s""']*))\s*(?:#.*)?$");
        if(!match.Success)throw new InvalidDataException(".env 中的 "+key+" 格式无法识别，请重新选择代理端口。");
        return match.Groups["value"].Value.Trim();
    }
}
}
