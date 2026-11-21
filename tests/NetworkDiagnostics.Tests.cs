using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace CodexKit {
internal static class NetworkDiagnosticsTests {
    static void Require(bool passed,string message){if(!passed)throw new InvalidDataException(message);}
    internal static void Run(string directory){
        RemoteControlRelayTests.Run(directory);
        var home=Path.Combine(directory,"saved-network");Directory.CreateDirectory(home);var path=Path.Combine(home,".env");File.WriteAllText(path,"CUSTOM=preserve\nHTTPS_PROXY=http://127.0.0.1:6112\nNO_PROXY=localhost,127.0.0.1\n");var before=File.ReadAllBytes(path);
        var saved=SavedNetworkConfiguration.Read(home,key=>key=="HTTPS_PROXY"?"http://127.0.0.1:6113":null);Require(saved.Summary.Contains(":6112")&&!saved.Summary.Contains(":6113")&&saved.Summary.Contains(".env 已保存")&&File.ReadAllBytes(path).SequenceEqual(before),"Saved A was replaced by draft or inherited B");
        var probe=NetworkDiagnostics.ProbeStartInfo("fixture.exe");foreach(var key in new[]{"HTTP_PROXY","HTTPS_PROXY","ALL_PROXY","NODE_USE_ENV_PROXY","NO_PROXY","CODEX_CLI_PATH"})Require(probe.EnvironmentVariables[key]==Environment.GetEnvironmentVariable(key),"Probe injected or cleared "+key);Require(probe.WorkingDirectory==Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Probe inherited repository config scope");
        File.WriteAllText(path,"HTTPS_PROXY=\"http://user:secret@proxy.invalid:8080\"\nHTTP_PROXY=http://$PROXY_HOST:8080\n");saved=SavedNetworkConfiguration.Read(home,key=>key=="ALL_PROXY"?"socks5://127.0.0.1:1080":null);Require(saved.Summary.Contains("***@proxy.invalid:8080")&&!saved.Summary.Contains("secret")&&saved.Summary.Contains("地址未展开")&&saved.Summary.Contains("当前进程继承环境"),"Proxy credentials exposed or expression invented");
        File.WriteAllText(path,"HTTPS_PROXY=\nhttps_proxy=http://127.0.0.1:8000\n");Require(SavedNetworkConfiguration.Read(home,key=>null).Summary.Contains("复杂声明"),"Duplicate declarations presented as resolved");
        File.WriteAllText(path,"HTTPS_PROXY=\n");Require(SavedNetworkConfiguration.Read(home,key=>key=="HTTPS_PROXY"?"http://127.0.0.1:6113":null).Summary.Contains("空值"),"Empty saved value ignored");
        var report=Doctor("ok","HTTP 101 Switching Protocols",false,"disabled");Require((bool)NetworkDiagnostics.ParseModelReport(report)["passed"],"HTTP/101 success rejected");report=Doctor("ok","HTTP 403 Forbidden",false,"disabled");Require(!(bool)NetworkDiagnostics.ParseModelReport(report)["passed"],"403 accepted as WS success");report=Doctor("fail","",true,"disabled");var parsed=NetworkDiagnostics.ParseModelReport(report);Require(!(bool)parsed["passed"]&&Core.Value(parsed,"modelState","").Contains("未进行握手"),"Skipped WebSocket hid HTTP failure");report=Doctor("ok","",true,"enabled");parsed=NetworkDiagnostics.ParseModelReport(report);Require((bool)parsed["passed"]&&Core.Value(parsed,"proxyPolicy","").Contains("系统代理优先"),"System policy or skipped transport mislabeled");
        var failure=NetworkDiagnostics.FailedReport(new System.ComponentModel.Win32Exception(5,"fixture access denied"));Require(!(bool)failure["passed"]&&Core.Value(failure,"modelState","").Contains("Windows 错误码 5")&&Core.Value(failure,"modelState","").Contains("访问被拒绝"),"Probe startup failure retained success or lost cause");
        bool rejected=false;try{NetworkDiagnostics.ParseModelReport(new Dictionary<string,object>());}catch(InvalidDataException){rejected=true;}Require(rejected,"Missing checks accepted as success");
    }
    static Dictionary<string,object> Doctor(string http,string handshake,bool skipped,string system){
        var checks=new Dictionary<string,object>();checks["network.provider_reachability"]=new{status=http};checks["network.websocket_reachability"]=new{status="ok",details=new Dictionary<string,object>{{"handshake result",handshake},{"supports websockets",skipped?"false":"true"}}};checks["network.env"]=new{details=new Dictionary<string,object>{{"respect system proxy",system},{"managed proxy","not configured"}}};return Core.ReadJson(Core.Json.Serialize(new{checks=checks}));
    }
    static object Field(object target,string name){return target.GetType().GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(target);}
    internal static void VerifyWindow(string directory){
        var window=new MainWindow(true);var path=Path.Combine(Path.GetTempPath(),"CodexKit-network-ui-"+Guid.NewGuid().ToString("N")+".log");var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
        try{
            File.WriteAllText(path,"@@NETWORK_RESULT@@"+Core.Json.Serialize(new{kind="model",passed=false,modelState="隔离探针未通过",configurationPath="fixture/.env",savedConfiguration="HTTPS_PROXY：.env 已保存 · http://127.0.0.1:6112",proxyPolicy="官方 CLI 策略"})+"\n");
            window.GetType().GetField("lastLog",flags).SetValue(window,path);window.GetType().GetField("logInputComplete",flags).SetValue(window,true);window.GetType().GetMethod("ReadLog",flags).Invoke(window,null);window.GetType().GetMethod("Navigate",flags).Invoke(window,new object[]{"连接与修复"});
            var labels=InitializationFlowTests.Descendants<Button>(window.Content as DependencyObject).Select(x=>Convert.ToString(x.Content)).ToArray();Require(labels.Contains("开启远控转发"),"Remote relay switch missing");Require(labels.Contains("检查已保存的模型线路")&&!labels.Any(x=>x.Contains("启动 Codex")||x.Contains("保存 WS")),"Custom launch remains in connection page");var body=String.Join("\n",InitializationFlowTests.Descendants<TextBlock>(window.Content as DependencyObject).Select(x=>x.Text));Require(body.Contains(":6112")&&body.Contains("官方 CLI 策略")&&body.Contains("隔离探针未通过"),"Probe result or saved source lost in log UI");Require(window.GetType().GetField("wsProxyHost",flags)==null,"Independent WS override remains");
            VerifyProgressTravel(window,directory);
        }finally{window.Close();File.Delete(path);}
    }
    static int[] ProgressPixels(ProgressBar bar) {
        int width=(int)Math.Ceiling(bar.ActualWidth), height=(int)Math.Ceiling(bar.ActualHeight);
        var image=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
        var drawing=new DrawingVisual();
        using(var dc=drawing.RenderOpen())
            dc.DrawRectangle(new VisualBrush(bar),null,new Rect(0,0,width,height));
        image.Render(drawing);
        var row=new byte[width*4];
        image.CopyPixels(new Int32Rect(0,height/2,width,1),row,width*4,0);
        int first=-1,last=-1;
        for(int x=0;x<width;x++)if(row[x*4+2]>105){if(first<0)first=x;last=x;}
        return new[]{first,last,width};
    }
    static void VerifyProgressTravel(MainWindow window,string directory){
        var bar=(ProgressBar)Field(window,"progress");
        window.Left=-20000;window.Top=-20000;window.ShowInTaskbar=false;window.Show();
        var frames=new List<string>{"run,window,track,first,last"};
        foreach(string run in new[]{"narrow","wide","resize"}){
            bar.IsIndeterminate=false;
            window.Width=run=="wide"?1480:1000;
            window.UpdateLayout();Pump(35);
            bar.IsIndeterminate=true;
            bool blank=false,entered=false,crossed=false,leaving=false,exited=false,repeated=false,dragging=false;
            double previous=0;int initialWidth=0;
            var timer=System.Diagnostics.Stopwatch.StartNew();
            while(timer.ElapsedMilliseconds<4200 && !repeated){
                Pump(20);
                var p=ProgressPixels(bar);
                double start=(double)p[0]/p[2];
                frames.Add(String.Join(",",run,window.Width,p[2],p[0],p[1]));
                if(initialWidth==0)initialWidth=p[2];
                if(p[0]<0){
                    Require(!entered||leaving,"Bright segment disappeared before sliding off the right edge");
                    blank=true;
                    if(leaving)exited=true;
                    continue;
                }
                double fraction=(double)(p[1]-p[0]+1)/p[2];
                bool partialLeft=p[0]<4 && fraction<.085;
                if(exited){Require(partialLeft,"Next sweep did not emerge from the left");repeated=true;continue;}
                if(!entered){entered=blank&&partialLeft;if(!entered)continue;}
                Require(start+.025>=previous,"Animation restarted before reaching the current right edge");
                previous=start;
                if(start>.15 && start<.85){
                    Require(fraction>.08 && fraction<.12,"Bright segment width changed inside track");
                    crossed=true;
                }
                if(run=="resize"){
                    if(!dragging && start>.18 && start<.65)dragging=true;
                    if(dragging && window.Width<1479){
                        window.Width=Math.Min(1480,window.Width+120);window.UpdateLayout();continue;
                    }
                }
                leaving=crossed && p[1]>=p[2]-4 && fraction<.085;
                if(leaving && run=="resize")Require(dragging && p[2]>=initialWidth+450,
                    "Window did not widen during this sweep");
            }
            Require(repeated,"Progress did not enter, cross, exit and repeat: "+run);
        }
        bar.IsIndeterminate=false;
        File.WriteAllLines(Path.Combine(directory,"progress-travel.csv"),frames);
    }
    internal static void Visual(string directory){
        var indicator=new ActivityIndicator{State="running",Width=36,Height=36};var panel=new StackPanel{Background=MorandiTheme.Canvas,Margin=new Thickness(0),Width=390,Height=210};panel.Children.Add(new Image{Source=Brand.VectorLogo,Width=40,Height=40,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,12)});panel.Children.Add(indicator);panel.Children.Add(new TextBlock{Text="模型线路检查 · 正在处理",TextWrapping=TextWrapping.Wrap,Foreground=MorandiTheme.Text,FontSize=16,Margin=new Thickness(0,12,0,0)});var window=new Window{Content=panel,Width=390,Height=210,Left=-20000,Top=-20000,ShowInTaskbar=false,UseLayoutRounding=true};
        try{window.Show();window.UpdateLayout();Pump(100);Require(indicator.Animating==SystemParameters.ClientAreaAnimation,"Running indicator was not animated");double before=indicator.Angle;Pump(160);if(SystemParameters.ClientAreaAnimation)Require(Math.Abs(before-indicator.Angle)>1,"Spinner angle did not change");
            foreach(double scale in new[]{1,1.25,1.5,1.75,2,2.5,3}){var bitmap=new RenderTargetBitmap((int)(390*scale),(int)(210*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(panel);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(directory,"dpi-"+(scale*100).ToString("0")+".png")))encoder.Save(file);}
            for(int i=0;i<18;i++){Pump(70);var bitmap=new RenderTargetBitmap(390,210,96,96,PixelFormats.Pbgra32);bitmap.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(directory,"motion-"+i.ToString("00")+".png")))encoder.Save(file);}
            indicator.Visibility=Visibility.Collapsed;Pump(20);Require(!indicator.Animating,"Hidden indicator kept rotating");indicator.Visibility=Visibility.Visible;Pump(30);indicator.State="complete";Require(!indicator.Animating,"Completed indicator kept rotating");indicator.State="running";indicator.AllowMotion=false;Require(!indicator.Animating,"Reduced motion did not stop rotation");Require(Brand.VectorLogo is DrawingImage,"Sidebar logo is still a bitmap");
        }finally{window.Close();}
    }
    internal static void RecordInitialization(string directory){
        var window=new MainWindow(true){Width=1000,Height=740,Left=-20000,Top=-20000,ShowInTaskbar=false};
        try{var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;var progress=(InitializationProgress)Field(window,"initializationProgress");progress.Reset(Core.NormalizeOptions(new Dictionary<string,object>()),true);foreach(var stage in progress.Stages.Take(3))stage.State="complete";progress.Stages[3].State="running";progress.Stages[3].Detail="正在准备官方独立 CLI";window.GetType().GetField("busy",flags).SetValue(window,true);window.GetType().GetField("wizard",flags).SetValue(window,2);window.GetType().GetMethod("Navigate",flags).Invoke(window,new object[]{"初始化"});window.Show();window.UpdateLayout();foreach(var indicator in InitializationFlowTests.Descendants<ActivityIndicator>(window))indicator.AllowMotion=true;var bar=(ProgressBar)Field(window,"stageProgress");bar.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty,new System.Windows.Media.Animation.DoubleAnimation(37.5,50,TimeSpan.FromSeconds(1.4)){EasingFunction=new System.Windows.Media.Animation.CubicEase{EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut}});
            for(int i=0;i<30;i++){Pump(70);var view=(FrameworkElement)window.Content;var bitmap=new RenderTargetBitmap(1000,740,96,96,PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(directory,"initialization-motion-"+i.ToString("00")+".png")))encoder.Save(file);}
        }finally{window.GetType().GetField("busy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(window,false);window.Close();}
    }
    static void Pump(int milliseconds){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(milliseconds)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
}
}
