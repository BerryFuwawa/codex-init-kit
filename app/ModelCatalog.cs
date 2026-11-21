using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CodexKit {
public sealed class CatalogModel {
    public string Id, DefaultEffort;
    public string[] Efforts;
    public readonly HashSet<string> UnverifiedEfforts=new HashSet<string>(StringComparer.Ordinal);
}
public sealed class ModelCatalog {
    public readonly Dictionary<string,CatalogModel> Models=new Dictionary<string,CatalogModel>(StringComparer.Ordinal);
    readonly HashSet<string> unavailable=new HashSet<string>(StringComparer.Ordinal){"gpt-5.5","gpt-reserve","codex-auto-review"};
    public string Source="尚未读取本机模型列表";
    public static readonly string[] CommonEfforts={"none","minimal","low","medium","high","xhigh","max","ultra"};
    public static bool ValidId(string value){return value!=null&&Regex.IsMatch(value,"^[A-Za-z0-9][A-Za-z0-9._/:+-]{0,127}\\z");}
    public static ModelCatalog Preset(){
        var result=new ModelCatalog{Source="内置模型列表 · 2026-11-22（可用性取决于账号和客户端）"};
        foreach(var id in new[]{"gpt-6.2-sol","gpt-6.2-astra","gpt-6.1-terra","gpt-6.1-sol","gpt-6-astra","gpt-6-sol","gpt-6-luna","gpt-5.6-sol","gpt-5.6-terra","gpt-5.6-luna"}){
            var efforts=id.EndsWith("-luna")?new[]{"low","medium","high","xhigh","max"}:new[]{"low","medium","high","xhigh","max","ultra"};
            var defaultEffort=id.StartsWith("gpt-6.2-",StringComparison.Ordinal)?"low":"medium";
            result.Models[id]=new CatalogModel{Id=id,DefaultEffort=defaultEffort,Efforts=efforts};
        }
        return result;
    }
    public static ModelCatalog Load(string home){
        var preset=Preset();
        var cache=Path.Combine(home,"models_cache.json");
        if(!File.Exists(cache))return preset;
        try{
            var catalog=Parse(File.ReadAllText(cache,Encoding.UTF8));
            catalog.unavailable.UnionWith(preset.Models.Keys.Except(catalog.Models.Keys));
            return catalog;
        }catch(IOException){return preset;}
        catch(UnauthorizedAccessException){return preset;}
        catch(InvalidDataException){return preset;}
        catch(ArgumentException){return preset;}
        catch(InvalidOperationException){return preset;}
    }
    public static ModelCatalog Parse(string json) {
        var result=new ModelCatalog();var parser=new JavaScriptSerializer{MaxJsonLength=4*1024*1024};
        var root=parser.DeserializeObject(json) as Dictionary<string,object>;
        if(root==null||!root.ContainsKey("models"))throw new InvalidDataException("模型列表格式无法识别。");
        var models=root["models"] as IEnumerable;if(models==null)throw new InvalidDataException("模型列表为空。");
        foreach(var item in models) {
            var row=item as Dictionary<string,object>;if(row==null)continue;
            var id=Get(row,"slug");if(!ValidId(id))continue;
            var visibility=Get(row,"visibility");
            if(visibility.Length>0&&visibility!="list"){result.unavailable.Add(id);continue;}
            if(result.unavailable.Contains(id))continue;
            var levels=new List<string>();object raw;
            if(row.TryGetValue("supported_reasoning_levels",out raw)&&raw is IEnumerable)foreach(var level in (IEnumerable)raw){var entry=level as Dictionary<string,object>;if(entry==null)continue;var effort=Get(entry,"effort");if(Regex.IsMatch(effort,"^[A-Za-z][A-Za-z0-9_-]{0,31}$")&&!levels.Contains(effort))levels.Add(effort);}
            if(levels.Count>0)result.Models[id]=new CatalogModel{Id=id,DefaultEffort=Get(row,"default_reasoning_level"),Efforts=levels.ToArray()};
        }
        if(result.Models.Count==0)throw new InvalidDataException("本机没有可供核对的模型记录。");
        DateTimeOffset fetched;var timestamp=Get(root,"fetched_at");
        result.Source="本机 Codex 模型缓存";
        if(DateTimeOffset.TryParse(timestamp,out fetched)){result.Source+=" · "+fetched.ToLocalTime().ToString("yyyy-MM-dd HH:mm");if(DateTimeOffset.UtcNow-fetched>TimeSpan.FromHours(24))result.Source+="（超过 24 小时）";}
        return result;
    }
    static string Get(Dictionary<string,object> row,string key){object value;return row.TryGetValue(key,out value)?Convert.ToString(value):"";}
    public void IncludeCurrent(string model,string effort) {
        if(unavailable.Contains(model))return;
        CatalogModel entry;
        if(!Models.TryGetValue(model,out entry)){entry=new CatalogModel{Id=model,DefaultEffort=effort,Efforts=new string[0]};Models[model]=entry;}
        if(!entry.Efforts.Contains(effort)){entry.Efforts=entry.Efforts.Concat(new[]{effort}).ToArray();entry.UnverifiedEfforts.Add(effort);}
    }
    public string Check(string model,string effort) {
        if(!ValidId(model))throw new InvalidOperationException("模型 ID 格式不正确，例如 gpt-6.1-sol；不能填写展示名称或空格。");
        if(!Regex.IsMatch(effort??"","^[A-Za-z][A-Za-z0-9_-]{0,31}$"))throw new InvalidOperationException("请选择有效的思考强度。");
        CatalogModel entry;
        if(!Models.TryGetValue(model,out entry))throw new InvalidOperationException("模型不在当前列表中，请重新检查并从下拉框选择。");
        var modelLabel=ModelLabels.Display(model);
        var effortLabel=EffortLabels.Display(effort);
        if(entry.Efforts.Length==0)return "已找到 "+modelLabel+"，但列表未提供思考强度；该强度尚未验证。";
        if(!entry.Efforts.Contains(effort))throw new InvalidOperationException(modelLabel+" 的本机记录不支持 "+effortLabel+"；支持："+string.Join("、",entry.Efforts.Select(EffortLabels.Display)));
        if(entry.UnverifiedEfforts.Contains(effort))return "保留当前配置："+modelLabel+" / "+effortLabel+"（尚未验证）。";
        return "配置核对通过："+modelLabel+" / "+effortLabel+"。";
    }
}
}
