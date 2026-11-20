using System;using System.IO;using System.Linq;using System.Collections;using System.Collections.Generic;using System.ComponentModel;using System.Reflection;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Media;using System.Windows.Media.Imaging;using CodexKit.Native;
namespace CodexKit {
internal static partial class NativeDesktopTests {
    static readonly BindingFlags UiPrivate=BindingFlags.Instance|BindingFlags.NonPublic;
    static object UiGet(MainWindow w,string n){return typeof(MainWindow).GetField(n,UiPrivate).GetValue(w);}
    static void UiSet(MainWindow w,string n,object v){typeof(MainWindow).GetField(n,UiPrivate).SetValue(w,v);}
    static object UiCall(MainWindow w,string n,params object[] values){return typeof(MainWindow).GetMethod(n,UiPrivate).Invoke(w,values);}
    static MainWindow TestWindow(){if(Application.Current==null)new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};return new MainWindow(true);}
    static void ExperienceChecks(){
        Check("完整代理地址继承与不支持地址保留",()=>{var folder=Path.Combine(root,"proxy-address");Directory.CreateDirectory(folder);var env=Path.Combine(folder,".env");foreach(var addr in new[]{"http://192.168.9.8:3067","http://[::1]:7897","http://127.0.0.1:54321"}){System.IO.File.WriteAllText(env,"HTTPS_PROXY='"+addr+"' # proxy\n");var saved=ProxyPortSettings.LoadAddress(folder);var expected=new Uri(addr);Assert(saved.Configured&&saved.Port==expected.Port&&System.Net.IPAddress.Parse(saved.Host).Equals(System.Net.IPAddress.Parse(expected.Host.Trim('[',']'))),"proxy address lost");}foreach(var addr in new[]{"https://127.0.0.1:3067","http://name:pass@127.0.0.1:3067","http://proxy.example:3067"}){var original="HTTP_PROXY="+addr+"\n";System.IO.File.WriteAllText(env,original);bool rejected=false;try{ProxyPortSettings.LoadAddress(folder);}catch(InvalidDataException){rejected=true;}Assert(rejected&&System.IO.File.ReadAllText(env)==original,"unsupported proxy changed");}});
        Check("启动窗口同时继承IP与端口",()=>{var before=Environment.GetEnvironmentVariable("CODEX_HOME");var home=Path.Combine(root,"proxy-window");Directory.CreateDirectory(home);System.IO.File.WriteAllText(Path.Combine(home,".env"),"HTTP_PROXY=http://192.168.9.8:3067\n");var w=TestWindow();try{Environment.SetEnvironmentVariable("CODEX_HOME",home);UiCall(w,"LoadCurrentProxyPort");Assert((string)UiCall(w,"ProxySummary")=="http://192.168.9.8:3067","startup address changed");}finally{Environment.SetEnvironmentVariable("CODEX_HOME",before);w.Close();}});
        Check("网络配置页与确认页使用同一远控转发选择",()=>{
            var w=TestWindow();
            try{
                UiCall(w,"Navigate","初始化");
                var relay=(CheckBox)UiGet(w,"remoteRelay");
                var save=(CheckBox)UiGet(w,"proxyEnabled");
                Assert(relay.Parent!=null && relay.IsEnabled && relay.IsChecked==true,"Relay should be selected by default");
                relay.ApplyTemplate();
                var icon=relay.Template.FindName("CheckIcon",relay) as System.Windows.Shapes.Path;
                Assert(icon!=null && icon.Data!=null && icon.Visibility==Visibility.Visible,"Relay vector icon missing");
                relay.IsChecked=false;
                UiCall(w,"Navigate","初始化");
                Assert(!Core.RemoteRelaySelected((Dictionary<string,object>)UiCall(w,"Options")),"Navigation reselected the relay");
                relay.IsChecked=true;
                ((ComboBox)UiGet(w,"proxyPortPreset")).SelectedIndex=2;
                ((TextBox)UiGet(w,"proxyHost")).Text="127.0.0.3";
                var selected=(Dictionary<string,object>)UiCall(w,"Options");
                Assert(Core.RemoteRelaySelected(selected) && ((string)UiCall(w,"RelaySummary")).Contains("127.0.0.3:3067"),"Confirmation lost selected proxy");
                UiSet(w,"wizard",1);UiCall(w,"Navigate","初始化");
                Assert(InitializationFlowTests.Descendants<TextBlock>(w.Content as DependencyObject).Any(x=>x.Text.Contains("127.0.0.3:3067") && x.Text.Contains("hosts")),"Relay activation missing from confirmation");
                UiSet(w,"wizard",0);UiCall(w,"Navigate","初始化");save.IsChecked=false;
                Assert(!relay.IsEnabled && relay.IsChecked==false && !Core.RemoteRelaySelected((Dictionary<string,object>)UiCall(w,"Options")),"Disabled proxy left relay selected");
            }finally{w.Close();}
        });
        Check("预检与执行使用同一设置快照",()=>{var w=TestWindow();try{((ComboBox)UiGet(w,"proxyPortPreset")).SelectedIndex=2;((TextBox)UiGet(w,"proxyHost")).Text="192.168.9.8";((CheckBox)UiGet(w,"guard")).IsChecked=false;var promise=new TaskCompletionSource<Dictionary<string,object>>();w.InitializationPreflight=()=>promise.Task;bool captured=false;w.StartOperation=(op,id,disk,port,protect,options)=>{captured=true;Assert(port==3067&&!protect&&Core.Value(options,"proxyHost","")=="192.168.9.8","confirmed snapshot mixed");throw new Win32Exception(1223);};var task=(Task)UiCall(w,"BeginInitialization");Assert(!((TextBox)UiGet(w,"port")).IsEnabled&&!((CheckBox)UiGet(w,"guard")).IsEnabled,"running settings still editable");UiCall(w,"Navigate","连接与修复");((ComboBox)UiGet(w,"proxyPortPreset")).SelectedIndex=0;((TextBox)UiGet(w,"proxyHost")).Text="192.168.9.9";((CheckBox)UiGet(w,"guard")).IsChecked=true;promise.SetResult(new Dictionary<string,object>{{"processes",new object[0]}});task.GetAwaiter().GetResult();Assert(captured&&!(bool)UiGet(w,"busy")&&((CheckBox)UiGet(w,"guard")).IsEnabled,"snapshot cancellation state");}finally{UiSet(w,"busy",false);w.Close();}});
        Check("离线维护不校验无关代理输入",()=>{var w=TestWindow();try{((ComboBox)UiGet(w,"proxyPortPreset")).SelectedIndex=8;((TextBox)UiGet(w,"port")).Text="";((TextBox)UiGet(w,"githubCustomMirror")).Text="invalid mirror";int calls=0;w.StartOperation=(op,id,disk,port,protect,opts)=>{Assert(port==0,"offline port");calls++;throw new Win32Exception(1223);};foreach(var op in new[]{"status","rollback-selected","rollback-delete-all","cua-check","config-check"})((Task)UiCall(w,"Run",op,"隔离维护",false,new Dictionary<string,object>{{"rollbackId","fixture"}},null)).GetAwaiter().GetResult();Assert(calls==5,"offline operation blocked by draft proxy");}finally{w.Close();}});
        Check("概览显示已保存值而非输入草稿",()=>{var w=TestWindow();try{UiSet(w,"status",new Dictionary<string,object>{{"runtimeMode","DesktopNative"},{"configModel","saved-model"},{"configEffort","high"},{"configWorkspace",@"C:\Saved\Conversations"},{"configProxy","http://192.168.0.2:3067"}});UiCall(w,"Navigate","概览");var all=InitializationFlowTests.Descendants<TextBlock>((DependencyObject)w.Content).Select(x=>x.Text).ToArray();Assert(all.Contains("http://192.168.0.2:3067")&&all.Contains(@"C:\Saved\Conversations")&&all.Any(x=>x.Contains("saved-model"))&&all.Any(x=>x.Contains("待应用设置")),"saved state replaced by draft");Assert(ManagedToml.SavedValue("model = 'custom'\n[desktop]\nprojectlessWorkspaceRoot = \"D:\\\\Saved\"\n","model")=="custom","saved TOML literal");}finally{w.Close();}});
        Check("初始化日志与后续操作独立",()=>{var w=TestWindow();try{string one=Path.Combine(root,"history-one.log"),two=Path.Combine(root,"history-two.log");System.IO.File.WriteAllText(one,"初始化专属日志\n");System.IO.File.WriteAllText(two,"连接检查专属日志\n");UiCall(w,"BeginRecord","one","初始化",one,true);UiSet(w,"lastLog",one);UiCall(w,"ReadLog");UiCall(w,"FinishRecord","complete","初始化完成");UiCall(w,"BeginRecord","two","连接检查",two,false);UiSet(w,"lastLog",two);UiCall(w,"ReadLog");UiSet(w,"wizard",2);UiCall(w,"Navigate","初始化");Assert(((TextBox)UiGet(w,"log")).Text.Contains("初始化专属日志")&&!((TextBox)UiGet(w,"log")).Text.Contains("连接检查专属日志"),"initialization lost log");UiCall(w,"Navigate","日志");Assert(((TextBox)UiGet(w,"log")).Text.Contains("连接检查专属日志"),"latest log missing");var picker=InitializationFlowTests.Descendants<ComboBox>((DependencyObject)w.Content).Single();picker.SelectedItem=picker.Items.Cast<OperationRecord>().Single(x=>x.id=="one");Assert(((TextBox)UiGet(w,"log")).Text.Contains("初始化专属日志")&&((string)UiCall(w,"FeedbackText")).Contains("初始化"),"history selection or feedback lost");}finally{w.Close();}});
        Check("操作记录持久化且旧日志仍可查看",()=>{var folder=Path.Combine(root,"operation-history");Directory.CreateDirectory(folder);var path=Path.Combine(folder,"operation.log");System.IO.File.WriteAllText(path,"raw fixture");var record=OperationRecord.Create("fixture","隔离测试",path);record.state="partial";record.Save();System.IO.File.WriteAllText(Path.Combine(folder,"legacy.log"),"legacy");var history=OperationRecord.History(folder);Assert(history.Count==2&&history.Single(x=>x.id=="fixture").state=="partial"&&history.Any(x=>x.title.StartsWith("历史日志")),"record compatibility");Assert(!System.IO.File.ReadAllText(Path.ChangeExtension(path,"operation.json")).Contains("proxyHost"),"options leaked to metadata");});
        Check("缺少最终摘要仍能重试未确认步骤",()=>{var w=TestWindow();try{var p=(InitializationProgress)UiGet(w,"initializationProgress");p.Reset(Settings(),true);p.Apply(new Dictionary<string,object>{{"stageId","preflight"},{"state","complete"}});p.Apply(new Dictionary<string,object>{{"stageId","desktop"},{"state","complete"}});p.Apply(new Dictionary<string,object>{{"stageId","base"},{"state","running"}});UiCall(w,"RecoverInitializationResult",1,"模拟后台中断",false);var retry=(string[])UiGet(w,"incompleteInitializationStages");Assert(retry.Contains("base")&&!retry.Contains("desktop")&&p.Stages.Single(x=>x.Id=="base").State=="uncertain","retry recovery missing");}finally{w.Close();}});
        Check("运行中的旧CLI缓存被保护",()=>{var f=new Fixture();var old=Path.Combine(f.P.Home,"packages","standalone","releases","old");MakePackage(old);f.Process.Entries.Add(new ProcessIdentity{Id=33,Started=700,Name="codex",Path=Path.Combine(old,"bin","codex.exe")});var entry=NativeDesktopState.Entries(f.P,f.Services).Single(x=>(string)x["path"]==old);Assert((bool)entry["protected"]&&((string)entry["detail"]).Contains("运行中"),"running CLI removable");});
        Check("缺失实际文件的初始化备份不可恢复",()=>{var f=new Fixture();var session=NativeSession.Create(f.P,f.Registry,true);NativeFile.Delete(Path.Combine(session.root,"Backups","config.toml"));var entry=NativeDesktopState.Entries(f.P,f.Services).Single(x=>(string)x["path"]==session.root);Assert(!(bool)entry["available"],"incomplete backup available");});

        Check("下载记录上翻暂停并保留页面位置",()=>{var w=TestWindow();try{
            var text=String.Join("\n",Enumerable.Range(1,160).Select(x=>"下载记录 "+x))+ "\n";
            UiSet(w,"githubLogText",text);UiCall(w,"Navigate","更新与下载");UiCall(w,"PumpLogLayout");
            var view=(TextBox)UiGet(w,"githubLogView");var viewport=(LogViewport)UiGet(w,"githubViewport");
            Assert(viewport.Following&&view.ExtentHeight-view.ViewportHeight-view.VerticalOffset<=2,"download not following");
            view.ScrollToVerticalOffset(100);UiCall(w,"PumpLogLayout");view.Select(20,8);view.ScrollToVerticalOffset(100);UiCall(w,"PumpLogLayout");double before=view.VerticalOffset;
            UiCall(w,"GitHubLog","新增下载记录");UiCall(w,"PumpLogLayout");
            Assert(!viewport.Following&&Math.Abs(view.VerticalOffset-before)<2&&view.SelectionLength==8,"download append moved reading");
            UiCall(w,"Navigate","概览");UiCall(w,"Navigate","更新与下载");UiCall(w,"PumpLogLayout");
            Assert(!viewport.Following&&Math.Abs(view.VerticalOffset-before)<2,"download navigation lost reading");
            viewport.Latest();UiCall(w,"PumpLogLayout");Assert(viewport.Following&&view.ExtentHeight-view.ViewportHeight-view.VerticalOffset<=2,"download latest failed");
        }finally{w.Close();}});
        Check("操作阅读位置与原文选择独立保存",()=>{var w=TestWindow();try{
            string one=Path.Combine(root,"reading-one.log"),two=Path.Combine(root,"reading-two.log");System.IO.File.WriteAllText(one,String.Join("\n",Enumerable.Range(1,180).Select(x=>"原始记录 "+x+new string('x',120)))+"\n");System.IO.File.WriteAllText(two,"第二项记录\n");
            UiCall(w,"BeginRecord","reading-one","第一项",one,true);UiSet(w,"lastLog",one);UiSet(w,"logInputComplete",true);UiCall(w,"ReadLog");UiCall(w,"Navigate","日志");UiSet(w,"showRawLog",true);UiCall(w,"UpdateLogView");UiCall(w,"PumpLogLayout");
            var view=(TextBox)UiGet(w,"log");view.Select(80,12);view.ScrollToVerticalOffset(140);view.ScrollToHorizontalOffset(50);UiCall(w,"PumpLogLayout");double top=view.VerticalOffset,left=view.HorizontalOffset;
            UiCall(w,"FinishRecord","complete","完成");UiCall(w,"BeginRecord","reading-two","第二项",two,false);UiSet(w,"lastLog",two);UiCall(w,"ReadLog");UiCall(w,"Navigate","日志");UiCall(w,"PumpLogLayout");
            var picker=InitializationFlowTests.Descendants<ComboBox>((DependencyObject)w.Content).Single();picker.SelectedItem=picker.Items.Cast<OperationRecord>().Single(x=>x.id=="reading-one");UiCall(w,"PumpLogLayout");
            Assert((bool)UiGet(w,"showRawLog")&&!((LogViewport)UiGet(w,"logViewport")).Following&&Math.Abs(view.VerticalOffset-top)<2&&Math.Abs(view.HorizontalOffset-left)<2&&view.SelectionStart==80&&view.SelectionLength==12,"operation reading lost");
        }finally{w.Close();}});
        Check("固定操作栏与多缩放离屏布局",()=>RenderExperience());
    }

    static void SaveView(FrameworkElement view,string path,double scale){var png=new RenderTargetBitmap((int)Math.Ceiling(view.ActualWidth*scale),(int)Math.Ceiling(view.ActualHeight*scale),96*scale,96*scale,PixelFormats.Pbgra32);png.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(png));using(var stream=System.IO.File.Create(path))encoder.Save(stream);}
    static void RenderExperience(){
        var folder=Path.Combine(root,"experience-ui");Directory.CreateDirectory(folder);var metrics=new List<object>();var w=TestWindow();
        try{
            UiSet(w,"status",new Dictionary<string,object>{{"runtimeMode","DesktopNative"},{"configModel","gpt-6.1-sol"},{"configEffort","medium"},{"configWorkspace",@"D:\Codex\Conversations"},{"configProxy","http://127.0.0.1:10808"}});
            var options=Settings();UiSet(w,"lastInitializationOptions",options);UiSet(w,"lastInitializationPort",10808);var stages=(InitializationProgress)UiGet(w,"initializationProgress");stages.Reset(options,true);foreach(var stage in stages.Stages){stage.State=stage.Id=="guard"?"incomplete":"complete";stage.Detail=stage.Id=="guard"?"登录任务未完成，请单独重试此项。":"已检查，设置一致时跳过写入。";}stages.Effects.Add(ConfigurationEffect.Create("env","HTTP 代理地址"));stages.EffectsKnown=true;stages.Outcome="partial";UiSet(w,"initializationState","partial");UiSet(w,"incompleteInitializationStages",new[]{"guard"});UiSet(w,"initializationSummary","安装启动保护：未完成；其余所选项目已检查或准备。真实连接待验收。");
            foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0,2.5,3.0}){
                double width=Math.Min(1000,1920/scale-32),height=Math.Min(740,1080/scale-48);w.DialogArea=()=>new Rect(0,0,1920/scale,1080/scale);
                foreach(var name in new[]{"概览","初始化","初始化-功能","初始化-模型","初始化-确认","初始化-结果","连接与修复","备份与回滚","更新与下载","日志"}){
                    var page=name.StartsWith("初始化")?"初始化":name;UiSet(w,"wizard",name=="初始化-确认"?1:name=="初始化-结果"?2:0);UiSet(w,"configurationTab",name=="初始化-功能"?1:name=="初始化-模型"?2:0);UiCall(w,"Navigate",page);
                    var view=(FrameworkElement)w.Content;view.Width=width;view.Height=height;view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();UiCall(w,"PumpLogLayout");
                    if(page=="初始化"&&name!="初始化-结果"){var actions=(FrameworkElement)UiGet(w,"wizardActions");Assert(actions.ActualHeight>0&&actions.TranslatePoint(new Point(0,actions.ActualHeight),view).Y<=height,"wizard footer outside window");}
                    if(scale==1||scale==2||scale==3)SaveView(view,Path.Combine(folder,name+"-"+(scale*100)+".png"),scale);
                    metrics.Add(new{page=name,scale=scale,width=width,height=height});
                }
                UiSet(w,"initializationSummary",String.Join("\n",Enumerable.Repeat("安装启动保护：模拟未完成，请核对授权和本步骤的实际执行结果。",30)));
                var dialog=(Window)UiCall(w,"CreateInitializationResultDialog",2,"",false,true);
                try{var body=(FrameworkElement)dialog.Content;Assert(dialog.Resources.Contains(typeof(System.Windows.Controls.Primitives.ScrollBar)),"dialog theme missing");body.Measure(new Size(dialog.Width,dialog.MaxHeight));body.Arrange(new Rect(0,0,dialog.Width,Math.Min(body.DesiredSize.Height,dialog.MaxHeight)));body.UpdateLayout();Assert(dialog.MaxHeight<=1080/scale-24&&body.DesiredSize.Height<=dialog.MaxHeight,"dialog overflow");var button=InitializationFlowTests.Descendants<Button>(body).Single();Assert(button.TranslatePoint(new Point(0,button.ActualHeight),body).Y<=body.ActualHeight,"dialog button outside view");if(scale==1||scale==2||scale==3)SaveView(body,Path.Combine(folder,"结果弹窗-"+(scale*100)+".png"),scale);}finally{dialog.Close();}
            }
            System.IO.File.WriteAllText(Path.Combine(folder,"layout.json"),Core.Json.Serialize(metrics));
        }finally{w.Close();}
    }
}
}
