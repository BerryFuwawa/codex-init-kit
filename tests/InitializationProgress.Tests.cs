using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Windows;using System.Windows.Media;using System.Windows.Threading;
namespace CodexKit {
public static class InitializationProgressTests {
    static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
    public static void Run(){
        var model=new InitializationProgress();var options=new Dictionary<string,object>{{"desktopUpdate",true},{"officialCliUpdate",true},{"browserRepair",true}};model.Reset(options,true);
        Require(model.Stages.Select(x=>x.Id).SequenceEqual(new[]{"preflight","desktop","base","official","guard","cli","browser"}),"Timeline order differs from actual initialization order");
        model.Apply(new Dictionary<string,object>{{"stageId","desktop"},{"state","running"},{"detail","等待商店完成"}});Require(model.Value==0&&model.Stages[1].Detail=="等待商店完成","Running stage fabricated completed progress");
        model.Apply(new Dictionary<string,object>{{"stageId","preflight"},{"state","complete"}});Require(Math.Abs(model.Value-100.0/7)<.01,"Progress did not use actual completed stages");
        model.Finish(new Dictionary<string,object>{{"state","partial"},{"summary","桌面版：未完成"},{"steps",new object[]{new Dictionary<string,object>{{"stageId","desktop"},{"state","incomplete"},{"message","网络连接失败"}},new Dictionary<string,object>{{"stageId","base"},{"state","complete"}}}}});Require(model.Outcome=="partial"&&model.Stages[1].State=="incomplete"&&model.Stages[1].Detail.Contains("网络"),"Final failure was replaced by a success state");
        options["desktopUpdate"]=false;options["officialCliUpdate"]=false;options["browserRepair"]=false;model.Reset(options,false);Require(model.Stages.Count==3&&model.Value==0&&model.Outcome=="","New run retained old steps or progress");
        model.Stopped(1,"未收到后台响应",false);Require(model.Stages[0].State=="incomplete"&&model.Stages.Skip(1).All(x=>x.State=="skipped"),"Early failure left stages permanently running");model.Reset(options,false);model.Stopped(1,"",true);Require(model.Stages.All(x=>x.State=="skipped"),"Authorization cancellation reported work as complete");
        var element=new FrameworkElement();Motion.Value(element,UIElement.OpacityProperty,.65,120,false);Require(Math.Abs(element.Opacity-.65)<.01&&!element.HasAnimatedProperties,"Reduced-motion path left an animation running");
        var transform=new TranslateTransform();Motion.Value(transform,TranslateTransform.XProperty,30,180,true);Pump(55);var current=transform.X;Require(current>0&&current<30,"Nonlinear motion did not pass through intermediate values");Motion.Value(transform,TranslateTransform.XProperty,0,120,true);Require(Math.Abs(transform.X-current)<.5,"Retargeting motion jumped");Pump(170);Require(Math.Abs(transform.X)<.1,"Retargeted motion did not settle");
    }
    static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
}
}
