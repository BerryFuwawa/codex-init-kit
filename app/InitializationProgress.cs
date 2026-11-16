using System;
using System.Collections.Generic;
using System.Linq;

namespace CodexKit {
internal sealed class InitializationStage {
    internal string Id,Title,State="pending",Detail="";
    internal InitializationStage(string id,string title){Id=id;Title=title;}
}
internal sealed class InitializationProgress {
    internal readonly List<InitializationStage> Stages=new List<InitializationStage>();
    internal readonly List<ConfigurationEffect> Effects=new List<ConfigurationEffect>();
    internal bool EffectsKnown;
    internal string Outcome="",Summary="";
    internal DateTime Started=DateTime.UtcNow;internal DateTime? Ended;
    internal void Reset(Dictionary<string,object> options,bool protection){
        var previous=Stages.ToDictionary(x=>x.Id,x=>new InitializationStage(x.Id,x.Title){State=x.State,Detail=x.Detail});
        if(Core.Value(options,"retryStages","").Length==0){Effects.Clear();EffectsKnown=false;}
        Stages.Clear();Outcome="";Summary="";Started=DateTime.UtcNow;Ended=null;
        Stages.Add(new InitializationStage("preflight","前置检查"));
        if(Convert.ToBoolean(options["desktopUpdate"]))Stages.Add(new InitializationStage("desktop","更新桌面版"));
        Stages.Add(new InitializationStage("base","配置、目录与 CUA"));
        if(Convert.ToBoolean(options["officialCliUpdate"]))Stages.Add(new InitializationStage("official","更新官方独立 CLI"));
        if(protection)Stages.Add(new InitializationStage("guard","安装启动保护"));
        Stages.Add(new InitializationStage("cli","应用所选 CLI"));
        if(Convert.ToBoolean(options["browserRepair"]))Stages.Add(new InitializationStage("browser","浏览器连接修复"));
        if(options.ContainsKey("websocketRepair")&&Convert.ToBoolean(options["websocketRepair"]))Stages.Add(new InitializationStage("websocket","WebSocket 线路修复"));
        var retry=Core.Value(options,"retryStages","");if(retry.Length>0)foreach(var stage in Stages.Where(x=>x.Id!="preflight"&&!retry.Split(',').Contains(x.Id))){InitializationStage old;if(previous.TryGetValue(stage.Id,out old)){stage.State=old.State;stage.Detail=old.Detail;}else{stage.State="skipped";stage.Detail="本次仅重试所选项目，此项未执行。";}}
    }
    internal void Apply(Dictionary<string,object> record){
        var id=Core.Value(record,"stageId","");var stage=Stages.FirstOrDefault(x=>x.Id==id);
        if(stage==null)return;
        stage.State=Core.Value(record,"state","running");stage.Detail=DesktopLocalization.TranslateText(Core.Value(record,"detail",""));
    }
    internal void Finish(Dictionary<string,object> record){
        Ended=DateTime.UtcNow;
        Outcome=Core.Value(record,"state","failed");Summary=DesktopLocalization.TranslateText(Core.Value(record,"summary",""),true);
        object entries;
        if(record.TryGetValue("effects",out entries)&&entries is System.Collections.IEnumerable){EffectsKnown=true;foreach(var item in (System.Collections.IEnumerable)entries){var entry=item as Dictionary<string,object>;if(entry==null)continue;var effect=ConfigurationEffect.Read(entry);if(effect.id.Length==0)continue;Effects.RemoveAll(x=>x.id==effect.id);Effects.Add(effect);}}
        if(record.TryGetValue("steps",out entries)&&entries is System.Collections.IEnumerable)foreach(var item in (System.Collections.IEnumerable)entries){
            var entry=item as Dictionary<string,object>;if(entry==null)continue;
            var id=Core.Value(entry,"stageId","");var stage=Stages.FirstOrDefault(x=>x.Id==id);
            if(stage==null)continue;stage.State=Core.Value(entry,"state","incomplete");stage.Detail=DesktopLocalization.TranslateText(Core.Value(entry,"message",""),true);
        }
    }
    internal void Stopped(int code,string error,bool cancelled){
        if(!Ended.HasValue)Ended=DateTime.UtcNow;
        bool missing=Outcome.Length==0;
        if(!missing)return;
        Outcome=cancelled?"cancelled":code==0||code==2?"partial":"failed";
        foreach(var stage in Stages.Where(x=>x.State=="pending"||x.State=="running")){
            stage.State=cancelled?"skipped":"uncertain";
            stage.Detail=cancelled?"已取消，未执行":String.IsNullOrWhiteSpace(error)?"未收到完成结果；重试前会先检查已保存内容。":error;
        }
    }
    internal int Finished {get{return Stages.Count(x=>x.State=="complete"||x.State=="incomplete"||x.State=="uncertain"||x.State=="skipped");}}
    internal int Complete {get{return Stages.Count(x=>x.State=="complete");}}
    internal double Value {get{return Stages.Count==0?0:100.0*Finished/Stages.Count;}}
}
}
