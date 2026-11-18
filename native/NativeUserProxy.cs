using System;using System.Linq;using System.Collections.Generic;
namespace CodexKit.Native {
public static class NativeUserProxy {
    public static readonly string[] Keys={"HTTP_PROXY","HTTPS_PROXY","NO_PROXY","NODE_USE_ENV_PROXY"};
    static RegistryValueState Text(string value){return new RegistryValueState{Exists=true,Kind="String",Value=value};}
    static bool Same(RegistryValueState left,RegistryValueState right){return left.Exists==right.Exists&&(!left.Exists||left.Kind==right.Kind&&Convert.ToString(left.Value)==Convert.ToString(right.Value));}
    public static bool Apply(INativeRegistry registry,NativeOptions options,Action backup){
        var values=new Dictionary<string,RegistryValueState>();
        foreach(var key in new[]{"HTTP_PROXY","HTTPS_PROXY"})values[key]=options.Proxy?Text(options.ProxyUrl):new RegistryValueState{Exists=false};
        values["NODE_USE_ENV_PROXY"]=options.Proxy?Text("1"):new RegistryValueState{Exists=false};
        if(options.Proxy){
            var previous=registry.Get("NO_PROXY");if(!previous.Exists)previous=registry.GetMachine("NO_PROXY");
            var exclusions=(Convert.ToString(previous.Value)??"").Split(',').Select(x=>x.Trim()).Where(x=>x.Length>0).Concat(new[]{"localhost","127.0.0.1","::1"}).Distinct(StringComparer.OrdinalIgnoreCase);
            values["NO_PROXY"]=new RegistryValueState{Exists=true,Kind=previous.Exists?previous.Kind:"String",Value=String.Join(",",exclusions)};
        }
        var changed=values.Where(x=>!Same(registry.Get(x.Key),x.Value)).ToArray();if(changed.Length==0)return false;
        backup();foreach(var entry in changed){registry.Set(entry.Key,entry.Value);if(!Same(registry.Get(entry.Key),entry.Value))throw new InvalidOperationException("当前用户代理保存后核对失败："+entry.Key);}
        return true;
    }
}
}
