using System;using System.Collections.Generic;using System.IO;using System.Linq;using System.Reflection;using System.Windows;using System.Windows.Controls;using System.Windows.Input;using System.Windows.Threading;
namespace CodexKit {
public static class ScrollingTests {
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Field(MainWindow view,string name){return typeof(MainWindow).GetField(name,Private).GetValue(view);}
    static object Call(MainWindow view,string name,params object[] args){return typeof(MainWindow).GetMethod(name,Private).Invoke(view,args);}
    static void Require(bool value,string message){if(!value)throw new InvalidDataException(message);}
    static void Pump(int ms){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(ms)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    static void Wheel(UIElement target,int delta){target.RaiseEvent(new MouseWheelEventArgs(Mouse.PrimaryDevice,Environment.TickCount,delta){RoutedEvent=Mouse.PreviewMouseWheelEvent});}
    public static void Run(){
        SmoothScrollViewer.Verify();
        var window=new MainWindow(true){Left=-20000,Top=-20000,Opacity=0,ShowInTaskbar=false};window.Show();Call(window,"Navigate","更新与下载");window.UpdateLayout();Pump(80);
        var scroll=(SmoothScrollViewer)Field(window,"pageScroll");var content=(StackPanel)Field(window,"content");var log=(TextBox)Field(window,"githubLogView");
        try{
            var samples=new List<double>();scroll.ScrollChanged+=(s,e)=>{if(e.OriginalSource==scroll&&e.VerticalChange!=0)samples.Add(scroll.VerticalOffset);};
            for(int i=0;i<4;i++){Wheel(scroll,-120);Pump(30);}Pump(700);
            Require(samples.Count>0,"Update page did not respond to wheel input");for(int i=1;i<samples.Count;i++)Require(samples[i]>=samples[i-1]-.5,"Downward wheel input snapped backward");
            scroll.StopAnimation();scroll.ScrollToVerticalOffset(40);Pump(50);scroll.AnimateTo(160);Pump(45);scroll.ScrollToVerticalOffset(20);Pump(260);Require(Math.Abs(scroll.VerticalOffset-20)<1,"An old wheel target overrode an external scroll request");
            scroll.StopAnimation();scroll.ScrollToVerticalOffset(90);Pump(50);var original=content.Children[0];var position=scroll.VerticalOffset;
            var version=new Version(Core.Version);typeof(MainWindow).GetField("update",Private).SetValue(window,new Dictionary<string,object>{{"version",new Version(version.Major,version.Minor,version.Build+1).ToString()},{"notes","测试更新说明"}});Call(window,"RefreshUpdatePage");window.UpdateLayout();Pump(40);
            Require(Object.ReferenceEquals(original,content.Children[0])&&Math.Abs(scroll.VerticalOffset-position)<1,"Update result rebuilt or moved the page");
            log.Text=String.Join("\n",Enumerable.Range(0,100).Select(i=>"下载记录 "+i));window.UpdateLayout();log.ApplyTemplate();var inner=(ScrollViewer)log.Template.FindName("PART_ContentHost",log);inner.ScrollToTop();Pump(50);position=scroll.VerticalOffset;Wheel(inner,-120);Pump(45);
            if(SystemParameters.ClientAreaAnimation)Require(inner.VerticalOffset<53,"Nested download log jumped a full wheel step");Pump(700);Require(inner.VerticalOffset>1&&Math.Abs(scroll.VerticalOffset-position)<1,"Nested log wheel moved the whole update page");
            inner.ScrollToBottom();Pump(50);scroll.ScrollToTop();Pump(50);Wheel(inner,-120);Pump(700);Require(scroll.VerticalOffset>1,"Log boundary did not pass wheel input to the page");
            scroll.StopAnimation();scroll.ScrollToVerticalOffset(90);Pump(50);position=scroll.VerticalOffset;Call(window,"GitHubLog","模拟新下载记录");Pump(80);Require(Math.Abs(scroll.VerticalOffset-position)<1,"Appending download output pulled the page away from the reader");
            scroll.AnimateTo(scroll.ScrollableHeight);Pump(40);Call(window,"Navigate","概览");Pump(300);Require(!scroll.IsMoving,"Page navigation retained an old wheel animation");
        }finally{scroll.StopAnimation();window.Close();}
    }
}
}
