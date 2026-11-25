using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace CodexKit {
public sealed class CurrentModelSettings {
    public string ParentModel=Core.DefaultParentModel, ParentEffort=Core.DefaultParentEffort, ChildModel=Core.DefaultChildModel, ChildEffort=Core.DefaultChildEffort;

    public bool Subagents, AutoAssign, HasSavedParent;
    public long MaxConcurrentSubagents=Native.NativeOptions.DefaultMaxConcurrentSubagents;

    public static string CodexHome() {
        return Native.NativePaths.CodexHome();
    }
    static string ReadOptional(string path) {
        try{return File.ReadAllText(path,Encoding.UTF8);}
        catch(FileNotFoundException){return "";}
        catch(DirectoryNotFoundException){return "";}
    }
    static Dictionary<string,string> Values(string text,bool config) {
        var result=new Dictionary<string,string>(StringComparer.Ordinal);var section="";
        foreach(var line in Regex.Split(text??"",@"\r?\n")) {
            var trimmed=line.TrimStart();
            if(config&&trimmed.StartsWith("[")){var header=Regex.Match(trimmed,@"^\[([^\[\]]+)\]\s*(?:#.*)?$");section=header.Success?header.Groups[1].Value.Trim():"(other)";continue;}
            var assignment=Regex.Match(line,@"^\s*([A-Za-z0-9_.-]+)\s*=\s*(.*?)\s*$");if(!assignment.Success)continue;
            var key=assignment.Groups[1].Value;
            if(config){if(section=="agents"||section=="features")key=section+"."+key;else if(section!="")continue;}
            if(key!="agents.enabled"&&key!="features.multi_agent"&&key!="model"&&key!="model_reasoning_effort"&&key!="agents.default_subagent_model"&&key!="agents.default_subagent_reasoning_effort")continue;
            var value=Regex.Match(assignment.Groups[2].Value,@"^(?:""([^""\r\n]*)""|'([^'\r\n]*)'|([A-Za-z0-9._/:+\-]+))\s*(?:#.*)?$");
            if(!value.Success)throw new InvalidDataException("模型设置格式无法识别："+key);
            result[key]=value.Groups[1].Success?value.Groups[1].Value:value.Groups[2].Success?value.Groups[2].Value:value.Groups[3].Value;
        }
        return result;
    }
    static string Get(Dictionary<string,string> values,string key,string fallback){string value;return values.TryGetValue(key,out value)?value:fallback;}
    public static CurrentModelSettings Load(string home) {
        var configText=ReadOptional(Path.Combine(home,"config.toml"));
        var config=Values(configText,true);
        var agents=ReadOptional(Path.Combine(home,"AGENTS.md"));
        var block=Regex.Match(agents,@"(?ms)^[ \t]*BEGIN CODEX LUNA PROMPT V[^\r\n]*\r?\n.*?^[ \t]*END CODEX LUNA PROMPT V[^\r\n]*\r?$");
        var managed=Values(block.Success?block.Value:"",false);
        var result=new CurrentModelSettings();
        result.HasSavedParent=config.ContainsKey("model");
        var savedSwitch=Get(config,"agents.enabled",Get(config,"features.multi_agent",block.Success?"true":"false"));
        bool v2Enabled;
        ManagedToml.ReadSubagentSettings(configText,out v2Enabled,out result.MaxConcurrentSubagents);
        result.Subagents=savedSwitch=="true"||v2Enabled;
        result.AutoAssign=!config.ContainsKey("agents.default_subagent_model")&&!config.ContainsKey("agents.default_subagent_reasoning_effort")&&!managed.ContainsKey("model")&&!managed.ContainsKey("model_reasoning_effort");
        result.ParentModel=Get(config,"model",result.ParentModel);result.ParentEffort=Get(config,"model_reasoning_effort",result.ParentEffort);
        result.ChildModel=Get(config,"agents.default_subagent_model",Get(managed,"model",result.ChildModel));
        result.ChildEffort=Get(config,"agents.default_subagent_reasoning_effort",Get(managed,"model_reasoning_effort",result.ChildEffort));
        if(!ModelCatalog.ValidId(result.ParentModel)||!ModelCatalog.ValidId(result.ChildModel)||!ValidEffort(result.ParentEffort)||!ValidEffort(result.ChildEffort))throw new InvalidDataException("现有模型或思考强度格式无效，请先修正配置后重新打开工具。");
        return result;
    }
    static bool ValidEffort(string value){return value!=null&&Regex.IsMatch(value,@"^[A-Za-z][A-Za-z0-9_-]{0,31}\z");}
}
}
