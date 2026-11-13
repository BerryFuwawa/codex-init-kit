using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace CodexKit {
public static class InitializationFlowTests {
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly Type WindowType=typeof(MainWindow);
    static object Field(MainWindow window,string name){return WindowType.GetField(name,Private).GetValue(window);}
    static void Set(MainWindow window,string name,object value){WindowType.GetField(name,Private).SetValue(window,value);}
    static object Call(MainWindow window,string name,params object[] args){return WindowType.GetMethod(name,Private).Invoke(window,args);}
    static void Assert(bool value,string message){if(!value)throw new InvalidDataException(message);}
    static void Navigate(MainWindow window,string page){Call(window,"Navigate",page);}
    static void Click(Button button){button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    static Button OverviewStart(MainWindow window){
        var content=(StackPanel)Field(window,"content");var hero=(StackPanel)((Border)content.Children[0]).Child;
        return hero.Children.OfType<Grid>().First().Children.OfType<Button>().First();
    }
    sealed class Scenario:IDisposable {
        internal readonly MainWindow Window;internal readonly Task Task;internal readonly string Root,Log;
        internal Process Worker;
        readonly SynchronizationContext prior;
        internal Scenario(int exitCode){
            prior=SynchronizationContext.Current;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            Root=Path.Combine(Path.GetTempPath(),"CodexKit-init-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Root);Log=Path.Combine(Root,"operation.log");
            Window=new MainWindow(true){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000,Opacity=0,ShowInTaskbar=false};Window.Show();Window.OperationLogPath=id=>Log;
            Window.StartOperation=(op,id,drive,port,guard,options)=>{
                var info=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"cmd.exe"),"/d /c \"set /p codex_test_signal= & exit /b "+exitCode+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
                Worker=Process.Start(info);return Worker;
            };
            Set(Window,"wizard",2);Navigate(Window,"初始化");
            Task=(Task)Call(Window,"Run","initialize","初始化",false,null,null);
            Assert((bool)Field(Window,"busy")&&Worker!=null,"Simulated initialization did not start");
        }
        internal void Complete(string logText){File.WriteAllText(Log,logText,new UTF8Encoding(false));Worker.StandardInput.WriteLine("continue");Worker.StandardInput.Close();Wait();}
        internal void Wait(){
            var frame=new DispatcherFrame();var timeout=new DispatcherTimer{Interval=TimeSpan.FromSeconds(15)};timeout.Tick+=(s,e)=>{timeout.Stop();frame.Continue=false;};timeout.Start();
            Task.ContinueWith(t=>Dispatcher.CurrentDispatcher.BeginInvoke(new Action(()=>frame.Continue=false)),TaskScheduler.FromCurrentSynchronizationContext());
            if(!Task.IsCompleted)Dispatcher.PushFrame(frame);timeout.Stop();
            if(!Task.IsCompleted)throw new TimeoutException("Simulated initialization did not finish");
            Task.GetAwaiter().GetResult();
        }
        public void Dispose(){
            if(Worker!=null&&!Task.IsCompleted){try{Worker.StandardInput.WriteLine("continue");Worker.StandardInput.Close();Wait();}catch{if(!Worker.HasExited)Worker.Kill();}}
            Set(Window,"busy",false);Window.Close();SynchronizationContext.SetSynchronizationContext(prior);Directory.Delete(Root,true);
        }
    }
    public static void OverviewDuringInitialization(){
        using(var s=new Scenario(0)){
            Navigate(s.Window,"概览");Click(OverviewStart(s.Window));
            Assert((int)Field(s.Window,"wizard")==2,"Overview start reset the running initialization");
        }
    }
    public static void ReturnAfterInitialization(){
        using(var s=new Scenario(0)){
            Navigate(s.Window,"概览");s.Complete("模拟运行日志\r\n@@INIT_RESULT@@{\"state\":\"complete\",\"summary\":\"重建配置：完成\"}\r\n");
            Assert(Convert.ToString(Field(s.Window,"page"))=="初始化"&&(int)Field(s.Window,"wizard")==2,"Finished initialization did not return to its results page");
        }
    }
    static void PresentAndConfirm(MainWindow window,Window dialog,string expectedTitle,string expectedBody){
        Assert(!(bool)Field(window,"busy")&&Convert.ToString(Field(window,"page"))=="初始化"&&(int)Field(window,"wizard")==2,"Result dialog opened before the operation state or page was restored");
        Assert(dialog.Owner==window,"Result dialog lost its owner");
        Exception error=null;bool loaded=false;
        dialog.WindowStartupLocation=WindowStartupLocation.Manual;dialog.Left=-20000;dialog.Top=-20000;dialog.Opacity=0;
        dialog.Loaded+=(s,e)=>dialog.Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(()=>{
            try{
                dialog.UpdateLayout();var panel=(StackPanel)((Border)dialog.Content).Child;
                var title=panel.Children.OfType<TextBlock>().First();var body=(TextBlock)panel.Children.OfType<ScrollViewer>().Single().Content;
                var button=panel.Children.OfType<Button>().Single();
                Assert(title.Text==expectedTitle&&dialog.Title==expectedTitle&&title.IsVisible&&title.ActualHeight>0,"Result dialog title was not visible");
                var ink=body.Foreground as SolidColorBrush;var background=((((Border)dialog.Content).Background is SolidColorBrush)?((Border)dialog.Content).Background:SilverTheme.Dark) as SolidColorBrush;
                Assert(body.Text.Contains(expectedBody)&&body.IsVisible&&body.ActualHeight>0&&body.ActualWidth>0&&ink!=null&&ink.Color.A>0&&background!=null&&ink.Color!=background.Color,"Result details for "+expectedTitle+" were empty or hidden: expected="+expectedBody+", text="+body.Text+", visible="+body.IsVisible+", height="+body.ActualHeight);
                Assert(button.IsVisible&&button.IsDefault&&Convert.ToString(button.Content)=="确认","Result confirmation button was unavailable");
                loaded=true;Click(button);
            }catch(Exception ex){error=ex;dialog.Close();}
        }));
        var accepted=dialog.ShowDialog();if(error!=null)throw error;
        Assert(loaded&&accepted==true,"Result dialog did not accept confirmation");
    }
    static void Sidebar(MainWindow window,string page){
        var buttons=(List<Button>)Field(window,"navigation");Click(buttons.Single(b=>Convert.ToString(b.Content)==page));
        Assert(Convert.ToString(Field(window,"page"))==page,"Sidebar navigation failed: "+page);
    }
    static string Fixture(string state,string summary){
        var record=new Dictionary<string,object>{{"state",state},{"summary",summary}};
        return "模拟运行日志\r\n@@INIT_RESULT@@"+Core.Json.Serialize(record)+"\r\n";
    }
    static void VerifyOutcome(int exitCode,string finalPage,string logText,string expectedTitle,string expectedBody){
        using(var s=new Scenario(exitCode)){
            bool presented=false;
            s.Window.PresentInitializationDialog=dialog=>{presented=true;PresentAndConfirm(s.Window,dialog,expectedTitle,expectedBody);};
            foreach(var page in new[]{"概览","连接与修复","CLI 管理","备份与回滚","更新与下载","日志","初始化"}){
                Sidebar(s.Window,page);Assert((bool)Field(s.Window,"busy"),"Navigation ended the active operation");
                if(page=="初始化")Assert((int)Field(s.Window,"wizard")==2,"Navigation lost the running results page");
            }
            Sidebar(s.Window,finalPage);s.Complete(logText);
            Assert(presented&&!(bool)Field(s.Window,"busy"),"Result dialog was not presented after the operation finished");
            Assert(Convert.ToString(Field(s.Window,"page"))=="初始化"&&(int)Field(s.Window,"wizard")==2,"Result did not return to initialization");
            Assert(Convert.ToString(Field(s.Window,"lastLog"))==s.Log&&File.ReadAllText(s.Log,Encoding.UTF8)==logText,"Navigation or result dialog replaced the initialization log");
            foreach(var page in new[]{"概览","日志","初始化"})Sidebar(s.Window,page);
            var content=(StackPanel)Field(s.Window,"content");var panel=(StackPanel)((Border)content.Children[0]).Child;
            var reset=panel.Children.OfType<WrapPanel>().SelectMany(x=>x.Children.OfType<Button>()).Single(x=>Convert.ToString(x.Content)=="重新设置");Click(reset);
            Assert((int)Field(s.Window,"wizard")==0,"Initialization settings could not be reopened");
        }
    }
    static void VerifyReadingPosition(){
        using(var s=new Scenario(2)){
            var lines=String.Join("\r\n",Enumerable.Range(1,240).Select(i=>"日志 "+i+" "+new string('x',180)))+"\r\n";
            File.WriteAllText(s.Log,lines,new UTF8Encoding(false));Call(s.Window,"ReadLog");Sidebar(s.Window,"日志");Call(s.Window,"PumpLogLayout");
            var log=(TextBox)Field(s.Window,"log");log.ScrollToVerticalOffset(160);log.ScrollToHorizontalOffset(90);Call(s.Window,"PumpLogLayout");
            log.Select(80,12);log.ScrollToVerticalOffset(160);log.ScrollToHorizontalOffset(90);Call(s.Window,"PumpLogLayout");
            var viewport=Field(s.Window,"logViewport");var following=viewport.GetType().GetProperty("Following",Private);double top=log.VerticalOffset,left=log.HorizontalOffset;int start=log.SelectionStart,length=log.SelectionLength;
            Assert(!(bool)following.GetValue(viewport,null)&&top>0&&left>0,"Log fixture did not pause at a readable position");
            Sidebar(s.Window,"概览");s.Window.PresentInitializationDialog=dialog=>PresentAndConfirm(s.Window,dialog,"初始化部分完成","更新桌面版：未完成");
            s.Complete(lines+"@@INIT_RESULT@@"+Core.Json.Serialize(new Dictionary<string,object>{{"state","partial"},{"summary","更新桌面版：未完成"}})+"\r\n");
            Call(s.Window,"PumpLogLayout");
            Assert(!(bool)following.GetValue(viewport,null)&&Math.Abs(log.VerticalOffset-top)<2&&Math.Abs(log.HorizontalOffset-left)<2&&log.SelectionStart==start&&log.SelectionLength==length,"Result navigation changed the reader's log position or selection");
        }
    }
    static void VerifyAuthorizationCancellation(){
        var window=new MainWindow(true){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000,Opacity=0,ShowInTaskbar=false};window.Show();
        var prior=SynchronizationContext.Current;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        try{
            var logPath=Path.Combine(Path.GetTempPath(),"CodexKit-cancel-"+Guid.NewGuid().ToString("N")+".log");window.OperationLogPath=id=>logPath;
            window.StartOperation=(op,id,drive,port,guard,options)=>{throw new System.ComponentModel.Win32Exception(1223);};
            bool presented=false;window.PresentInitializationDialog=dialog=>{presented=true;PresentAndConfirm(window,dialog,"已取消初始化","已取消管理员授权");};
            Set(window,"wizard",2);Navigate(window,"初始化");var task=(Task)Call(window,"Run","initialize","初始化",false,null,null);task.GetAwaiter().GetResult();
            Assert(presented&&!(bool)Field(window,"busy")&&!File.Exists(logPath),"Authorization cancellation was not reported safely");
        }finally{Set(window,"busy",false);window.Close();SynchronizationContext.SetSynchronizationContext(prior);}
    }
    static void VerifyCliDetectionFromStatus(){
        var window=new MainWindow(true){WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000,Opacity=0,ShowInTaskbar=false};window.Show();
        var root=Path.Combine(Path.GetTempPath(),"CodexKit-cli-status-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var path=Path.Combine(root,"status.log");
        try{
            Set(window,"lastLog",path);Set(window,"logInputComplete",true);Navigate(window,"初始化");
            File.WriteAllText(path,"@@STATUS@@{\"runtimeMode\":\"OfficialStandalone\"}\r\n",new UTF8Encoding(false));Call(window,"ReadLog");
            var native=(RadioButton)Field(window,"cliNative");var standalone=(RadioButton)Field(window,"cliStandalone");
            Assert(standalone.IsChecked==true&&native.IsChecked!=true&&Convert.ToString(((Dictionary<string,object>)Call(window,"Options"))["cliMode"])=="auto","Status output did not select the existing standalone CLI");
            native.IsChecked=true;File.WriteAllText(path,"@@STATUS@@{\"runtimeMode\":\"DesktopNative\"}\r\n",new UTF8Encoding(false));Call(window,"ReadLog");
            File.WriteAllText(path,"@@STATUS@@{\"runtimeMode\":\"OfficialStandalone\"}\r\n",new UTF8Encoding(false));Call(window,"ReadLog");
            Assert(native.IsChecked==true&&Convert.ToString(((Dictionary<string,object>)Call(window,"Options"))["cliMode"])=="desktop-native","A late status result replaced the manual CLI choice");
            File.WriteAllText(path,"@@STATUS@@{\"runtimeMode\":\"Unknown\"}\r\n",new UTF8Encoding(false));Call(window,"ReadLog");Call(window,"ResetCliChoice");
            Assert(native.IsChecked!=true&&standalone.IsChecked!=true,"Unknown CLI mode selected a default");
            bool blocked=false;try{Call(window,"RequireCliChoice");}catch(TargetInvocationException ex){blocked=ex.InnerException is InvalidOperationException;}
            Assert(blocked,"Unknown CLI mode allowed initialization to proceed automatically");
        }finally{Set(window,"busy",false);window.Close();Directory.Delete(root,true);}
    }
    static void Pump(Task task) {
        var watch=Stopwatch.StartNew();
        while(!task.IsCompleted){
            if(watch.Elapsed.TotalSeconds>15)throw new TimeoutException("Preflight UI did not complete");
            var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);Thread.Sleep(10);
        }
        task.GetAwaiter().GetResult();
    }
    static bool? AppCloseDialog(Window dialog,bool accept) {
        dialog.WindowStartupLocation=WindowStartupLocation.Manual;dialog.Left=-20000;dialog.Top=-20000;dialog.Opacity=0;
        Exception error=null;
        dialog.Loaded+=(s,e)=>dialog.Dispatcher.BeginInvoke(new Action(()=>{
            try {
                var panel=(StackPanel)dialog.Content;var buttons=panel.Children.OfType<WrapPanel>().Single().Children.OfType<Button>().ToArray();
                var cancel=buttons.Single(b=>Convert.ToString(b.Content)=="取消");
                Assert(cancel.IsDefault&&cancel.IsCancel,"Application closure did not default to cancellation");
                Assert(panel.Children.OfType<TextBlock>().Any(t=>t.Text.Contains("请先保存工作")&&t.ActualHeight>0),"Application closure did not show the interruption warning");
                Click(accept?buttons.Single(b=>Convert.ToString(b.Content)=="关闭并继续"):cancel);
            }catch(Exception ex){error=ex;dialog.Close();}
        }));
        var result=dialog.ShowDialog();if(error!=null)throw error;return result;
    }
    internal static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject {
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()){
            var match=child as T;if(match!=null)yield return match;
            foreach(var nested in Descendants<T>(child))yield return nested;
        }
    }
    static void VerifyInitializationGroups() {
        var window=new MainWindow(true){Left=-20000,Top=-20000,Opacity=0,ShowInTaskbar=false};window.Show();
        try {
            Set(window,"configurationTab",1);Navigate(window,"初始化");window.UpdateLayout();
            var content=(StackPanel)Field(window,"content");var features=(StackPanel)((Border)content.Children[1]).Child;
            var labels=Descendants<TextBlock>(features).Select(t=>t.Text).ToArray();
            foreach(var title in new[]{"工作与规则","记忆","修复与连接","组件更新"})Assert(labels.Contains(title),"Initialization feature category missing: "+title);
            var choices=Descendants<CheckBox>(features).ToArray();
            Assert(choices.Length==11&&choices.Distinct().Count()==11&&choices.Last()==Field(window,"subagents"),"Feature grouping duplicated, lost or misplaced a checkbox");
            var journal=(CheckBox)Field(window,"workJournal");journal.IsChecked=false;Navigate(window,"概览");Navigate(window,"初始化");
            Assert(journal.IsChecked==false&&(bool)((Dictionary<string,object>)Call(window,"Options"))["memories"],"Grouping or navigation reset feature choices");
            foreach(var width in new[]{1000.0,1120.0}){window.Width=width;window.UpdateLayout();Assert(choices.All(c=>c.ActualWidth>0&&c.ActualHeight>0),"Grouped feature control was hidden at supported window width");}
        }finally{window.Close();}
    }
    static void VerifyPreflight(bool running,bool accept,bool fail) {
        var previous=SynchronizationContext.Current;SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var window=new MainWindow(true){Left=-20000,Top=-20000,Opacity=0,ShowInTaskbar=false};window.Show();
        var root=Path.Combine(Path.GetTempPath(),"CodexKit-preflight-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            Assert(Convert.ToString(Call(window,"InitSummary")).Contains("审批与沙箱使用 Codex 默认设置"),"Confirmation did not explain the default permissions");
            var response=new TaskCompletionSource<Dictionary<string,object>>();int checks=0,dialogs=0,workers=0;
            window.InitializationPreflight=()=>{checks++;return response.Task;};
            window.PresentAppCloseDialog=dialog=>{dialogs++;return AppCloseDialog(dialog,accept);};
            window.OperationLogPath=id=>Path.Combine(root,"init.log");
            window.StartOperation=(op,id,drive,port,guard,options)=>{
                Assert(op=="initialize"&&(bool)options["allowCloseRunningApp"]==(running&&accept),"One-time close authorization was missing or persisted incorrectly");workers++;
                Assert(running&&accept?Core.Value(options,"closeApprovedProcesses","")=="123:639298120645079230":!options.ContainsKey("closeApprovedProcesses"),"Close approval did not name the detected process instance");
                return Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"cmd.exe"),"/d /c exit 0"){UseShellExecute=false,CreateNoWindow=true});
            };
            Set(window,"wizard",1);Navigate(window,"初始化");
            var task=(Task)Call(window,"BeginInitialization");Pump((Task)Call(window,"BeginInitialization"));
            Assert(checks==1&&workers==0,"Repeated clicks bypassed the pending preflight");Navigate(window,"概览");
            if(fail)response.SetException(new InvalidDataException("fixture permission read failure"));
            else response.SetResult(new Dictionary<string,object>{{"processes",running?new object[]{new Dictionary<string,object>{{"Id",123},{"Started","639298120645079230"}}}:new object[0]}});
            Pump(task);
            bool started=!fail&&(!running||accept);
            Assert(workers==(started?1:0)&&dialogs==(!fail&&running?1:0),"Preflight cancellation or failure started initialization");
            Assert(!(bool)Field(window,"busy")&&Convert.ToString(Field(window,"page"))=="初始化"&&(int)Field(window,"wizard")== (started?2:1),"Preflight did not restore the correct initialization page");
            Assert(!((Dictionary<string,object>)Call(window,"Options")).ContainsKey("allowCloseRunningApp"),"Preflight kept closure consent");
        }finally{Set(window,"busy",false);window.Close();Directory.Delete(root,true);SynchronizationContext.SetSynchronizationContext(previous);}
    }
    public static void Run(){
        OverviewDuringInitialization();ReturnAfterInitialization();
        VerifyOutcome(0,"概览",Fixture("complete","重建配置：完成"),"已完成初始化","基础配置和所选功能已完成");
        VerifyOutcome(2,"连接与修复",Fixture("partial","更新桌面版：未完成"),"初始化部分完成","更新桌面版：未完成");
        VerifyOutcome(2,"更新与下载","模拟运行日志\r\n","初始化部分完成","请查看下方日志");
        VerifyOutcome(1,"CLI 管理",Fixture("failed","重建配置：未完成"),"初始化未完成","重建配置：未完成");
        VerifyOutcome(1,"日志","模拟运行日志\r\n","初始化未完成","请查看下方日志");
        VerifyReadingPosition();VerifyAuthorizationCancellation();VerifyCliDetectionFromStatus();
        VerifyPreflight(false,false,false);VerifyPreflight(true,false,false);VerifyPreflight(true,true,false);VerifyPreflight(true,false,true);
        VerifyInitializationGroups();
    }
}
}
