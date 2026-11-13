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
    internal string Outcome="",Summary="";
    internal DateTime Started=DateTime.UtcNow;
    internal void Reset(Dictionary<string,object> options,bool protection){
        Stages.Clear();Outcome="";Summary="";Started=DateTime.UtcNow;
        Stages.Add(new InitializationStage("preflight","前置检查"));
        if(Convert.ToBoolean(options["desktopUpdate"]))Stages.Add(new InitializationStage("desktop","更新桌面版"));
        Stages.Add(new InitializationStage("base","配置、目录与 CUA"));
        if(Convert.ToBoolean(options["officialCliUpdate"]))Stages.Add(new InitializationStage("official","更新官方独立 CLI"));
        if(protection)Stages.Add(new InitializationStage("guard","安装启动保护"));
        Stages.Add(new InitializationStage("cli","应用所选 CLI"));
        if(Convert.ToBoolean(options["browserRepair"]))Stages.Add(new InitializationStage("browser","浏览器连接修复"));
    }
    internal void Apply(Dictionary<string,object> record){
        var id=Core.Value(record,"stageId","");var stage=Stages.FirstOrDefault(x=>x.Id==id);
        if(stage==null)return;
        stage.State=Core.Value(record,"state","running");stage.Detail=DesktopLocalization.TranslateText(Core.Value(record,"detail",""));
    }
    internal void Finish(Dictionary<string,object> record){
        Outcome=Core.Value(record,"state","failed");Summary=DesktopLocalization.TranslateText(Core.Value(record,"summary",""),true);
        object entries;
        if(record.TryGetValue("steps",out entries)&&entries is System.Collections.IEnumerable)foreach(var item in (System.Collections.IEnumerable)entries){
            var entry=item as Dictionary<string,object>;if(entry==null)continue;
            var id=Core.Value(entry,"stageId","");var stage=Stages.FirstOrDefault(x=>x.Id==id);
            if(stage==null)continue;stage.State=Core.Value(entry,"state","incomplete");stage.Detail=DesktopLocalization.TranslateText(Core.Value(entry,"message",""),true);
        }
    }
    internal void Stopped(int code,string error,bool cancelled){
        if(Outcome.Length>0)return;
        Outcome=cancelled?"cancelled":code==0?"complete":code==2?"partial":"failed";
        var active=Stages.FirstOrDefault(x=>x.State=="running")??Stages.FirstOrDefault(x=>x.State=="pending");
        if(active!=null&&!cancelled&&code!=0){active.State="incomplete";active.Detail=String.IsNullOrWhiteSpace(error)?"本步骤未完成，请查看日志。":error;}
        foreach(var stage in Stages.Where(x=>x.State=="pending"||x.State=="running")){stage.State="skipped";stage.Detail=cancelled?"已取消，未执行":"未收到此步骤的完成结果，请查看日志。";}
    }
    internal int Finished {get{return Stages.Count(x=>x.State=="complete"||x.State=="incomplete"||x.State=="skipped");}}
    internal int Complete {get{return Stages.Count(x=>x.State=="complete");}}
    internal double Value {get{return Stages.Count==0?0:100.0*Finished/Stages.Count;}}
}
}
