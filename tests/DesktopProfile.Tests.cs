using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CodexKit.Native;

namespace CodexKit {
internal static partial class NativeDesktopTests {
    static DesktopProfileRecord Profile(Fixture f){return DesktopProfile.Capture(f.Process.Entries,f.Services.Desktop.Discover(),DateTime.UtcNow);}
    static void ProfileFails(Action action,string message){bool failed=false;try{action();}catch(InvalidDataException){failed=true;}catch(DirectoryNotFoundException){failed=true;}Assert(failed,message);}
    static void ProfileChecks(){
        ProductContinuityChecks();
        Check("进程状态输出不包含原始命令行",()=>{
            var f=new Fixture();f.AddDesktop(81,501);f.Process.Entries[0].CommandLine="fixture-private-argument";
            Assert(!Core.Json.Serialize(new NativeInitializer(f.P,f.Services).Preflight()).Contains("fixture-private-argument"),"raw command line exposed in process report");
        });
        Check("实际子进程目录覆盖别名推断",()=>{
            var f=new Fixture();var folder=Path.Combine(f.P.RoamingData,"Codex","web","Codex");f.AddDesktop(81,501,folder);
            f.Process.Entries[0].CommandLine="chatgpt-desktop.exe";
            var identity=Path.Combine(folder,DesktopProfile.IdentityFile);System.IO.File.WriteAllText(identity,"existing-identity");
            var before=System.IO.File.ReadAllBytes(identity);var modified=System.IO.File.GetLastWriteTimeUtc(identity);
            var record=Profile(f);Assert(record.path==folder&&record.processes.SequenceEqual(new[]{"81:501"})&&record.identityPresent,"alias selected wrong profile");
            DesktopProfile.Validate(record,DateTime.UtcNow);Assert(System.IO.File.ReadAllBytes(identity).SequenceEqual(before)&&System.IO.File.GetLastWriteTimeUtc(identity)==modified&&!System.IO.File.Exists(DesktopProfile.RecordPath(f.P)),"read-only capture wrote");
        });
        Check("多桌面实例使用同一目录可识别",()=>{
            var f=new Fixture();f.AddDesktop(81,501);f.AddDesktop(82,502);
            Assert(Profile(f).processes.Length==2,"same-profile instances lost");
        });
        Check("多资料目录在关闭前停止且不覆盖旧记录",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var record=Profile(f);DesktopProfile.Save(f.P,record);var before=System.IO.File.ReadAllText(DesktopProfile.RecordPath(f.P));
            f.AddDesktop(82,502,Path.Combine(f.P.RoamingData,"other"));
            var settings=Settings();settings["allowCloseRunningApp"]=true;settings["closeApprovedProcesses"]=String.Join(",",f.Process.Entries.Select(p=>p.Key));settings["desktopProfileRecord"]=Core.Json.Serialize(record);
            Assert(new NativeDesktopWorkflow(f.P,f.Services,settings,f,f.Log.Add).Run(NativeDesktopWorkflow.Options(settings,3067),false)==1&&f.Process.Requested==0&&System.IO.File.ReadAllText(DesktopProfile.RecordPath(f.P))==before,"ambiguous profiles closed or overwritten");
        });
        Check("同产品旧版本实例也参与目录判断",()=>{
            var f=new Fixture();f.AddDesktop(81,501);f.AddDesktop(82,502,Path.Combine(f.P.RoamingData,"old-profile"));
            var old=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"WindowsApps","OpenAI.Codex_25.1.0.0_x64__2p2nqsd0c76g0","app","ChatGPT.exe");
            foreach(var process in f.Process.Entries.Skip(2))process.Path=old;
            Assert(WindowsNativeProcesses.IsDesktopExecutable(old),"old version excluded");ProfileFails(()=>Profile(f),"old-profile instance ignored");
        });
        Check("只有主进程请求参数不能充当实际目录",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var folder=Profile(f).path;f.Process.Entries.RemoveAt(1);f.Process.Entries[0].CommandLine="app.exe --user-data-dir="+NativeData.Quoted(folder);
            ProfileFails(()=>Profile(f),"unobserved request treated as actual");
        });
        Check("启动请求与运行时目录不同则停止",()=>{
            var f=new Fixture();f.AddDesktop(81,501);f.Process.Entries[0].CommandLine="app.exe --user-data-dir="+NativeData.Quoted(f.P.Home);
            ProfileFails(()=>Profile(f),"requested directory overrode actual");
        });
        Check("子进程无可读参数时不选择默认目录",()=>{
            var f=new Fixture();f.AddDesktop(81,501);f.Process.Entries[1].CommandLine="";
            ProfileFails(()=>Profile(f),"missing process evidence guessed");
        });
        Check("无桌面进程不扫描目录猜选",()=>{
            var f=new Fixture();System.IO.Directory.CreateDirectory(Path.Combine(f.P.RoamingData,"Codex","web","Codex"));System.IO.Directory.CreateDirectory(Path.Combine(f.P.RoamingData,"Codex","web","chatgpt-desktop"));
            Assert(Profile(f)==null,"invented running profile");ProfileFails(()=>DesktopProfile.Load(f.P,DateTime.UtcNow),"directory timestamps selected profile");
            Assert(f.Run(Settings(),false)==0,"no-process initialization blocked");
        });
        Check("无进程自动沿用有效记录且不显示目录选择",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var profile=Profile(f);DesktopProfile.Save(f.P,profile);f.Process.Entries.Clear();
            var info=Core.ReadJson(Core.Json.Serialize(NativeDesktopWorkflow.WebSocketInfo(f.P,f.Services,NativeDesktopWorkflow.Options(Settings(),3067))));
            var restored=DesktopProfile.FromReport(info);Assert(restored.path==profile.path&&((System.Collections.IList)info["processes"]).Count==0,"saved profile missing");
            var description=Core.Value(info,"profileDescription","");Assert(!description.Contains(profile.path)&&!description.Contains("请确认")&&description.Contains("继续使用之前的 Codex"),"technical selection remained in normal flow");
        });
        Check("未变化的旧观测不因时间经过失效",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var profile=Profile(f);profile.capturedUtc=DateTime.UtcNow.AddHours(-25).ToString("o");DesktopProfile.Save(f.P,profile);
            Assert(DesktopProfile.Load(f.P,DateTime.UtcNow).path==profile.path,"unchanged old observation expired");
        });
        Check("损坏记录明确停止且不猜选目录",()=>{
            var f=new Fixture();var file=DesktopProfile.RecordPath(f.P);System.IO.Directory.CreateDirectory(Path.GetDirectoryName(file));System.IO.File.WriteAllText(file,"incomplete-json");
            ProfileFails(()=>DesktopProfile.Load(f.P,DateTime.UtcNow),"invalid record accepted");
        });
        Check("资料目录缺失时不创建替代目录",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var profile=Profile(f);profile.path=Path.Combine(f.P.RoamingData,"missing-profile");DesktopProfile.Save(f.P,profile);
            ProfileFails(()=>DesktopProfile.Load(f.P,DateTime.UtcNow),"missing path accepted");Assert(!System.IO.Directory.Exists(profile.path),"missing profile created");
        });
        Check("身份文件变化使保存记录失效",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var identity=Path.Combine(Profile(f).path,DesktopProfile.IdentityFile);System.IO.File.WriteAllText(identity,"existing-identity");var profile=Profile(f);DesktopProfile.Save(f.P,profile);
            System.IO.File.SetLastWriteTimeUtc(identity,DateTime.UtcNow.AddMinutes(-3));ProfileFails(()=>DesktopProfile.Load(f.P,DateTime.UtcNow),"changed identity accepted");
            System.IO.File.Delete(identity);ProfileFails(()=>DesktopProfile.Load(f.P,DateTime.UtcNow),"missing identity accepted");
        });
        Check("保存记录不包含身份文件内容",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var folder=Profile(f).path;System.IO.File.WriteAllText(Path.Combine(folder,DesktopProfile.IdentityFile),"private-identity-value");DesktopProfile.Save(f.P,Profile(f));
            Assert(!System.IO.File.ReadAllText(DesktopProfile.RecordPath(f.P)).Contains("private-identity-value"),"identity copied into record");
        });
        Check("未经目录确认不能关闭桌面",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var settings=Settings();settings["allowCloseRunningApp"]=true;settings["closeApprovedProcesses"]=String.Join(",",f.Process.Entries.Select(p=>p.Key));
            Assert(new NativeDesktopWorkflow(f.P,f.Services,settings,f,f.Log.Add).Run(NativeDesktopWorkflow.Options(settings,3067),false)==1&&f.Process.Requested==0&&!System.IO.File.Exists(DesktopProfile.RecordPath(f.P)),"missing profile approval closed desktop");
        });
        Check("关闭前保存目录且身份文件保持原样",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var profile=Profile(f);var identity=Path.Combine(profile.path,DesktopProfile.IdentityFile);System.IO.File.WriteAllText(identity,"original-identity");profile=Profile(f);
            var options=NativeDesktopWorkflow.Options(Settings(),3067);options.AllowClose=true;options.ApprovedProcesses=f.Process.Entries.Select(p=>p.Key).ToArray();options.SavedDesktopProfile=Core.Json.Serialize(profile);
            f.Process.BeforeExit=()=>Assert(System.IO.File.Exists(DesktopProfile.RecordPath(f.P))&&DesktopProfile.Load(f.P,DateTime.UtcNow).path==profile.path,"closed before preserving profile");
            var result=Core.ReadJson(Core.Json.Serialize(NativeDesktopWorkflow.WebSocketInfo(f.P,f.Services,options)));
            Assert(f.Process.Requested==2&&DesktopProfile.FromReport(result).path==profile.path&&System.IO.File.ReadAllText(identity)=="original-identity","close or identity preservation");
        });
        Check("确认后桌面已退出仍保留已观测记录",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var options=NativeDesktopWorkflow.Options(Settings(),3067);options.AllowClose=true;options.ApprovedProcesses=f.Process.Entries.Select(p=>p.Key).ToArray();options.SavedDesktopProfile=Core.Json.Serialize(Profile(f));f.Process.Entries.Clear();
            var result=Core.ReadJson(Core.Json.Serialize(NativeDesktopWorkflow.WebSocketInfo(f.P,f.Services,options)));Assert(f.Process.Requested==0&&DesktopProfile.FromReport(result)!=null,"closed externally lost confirmed profile");
        });
        Check("确认后进程资料目录变化不关闭",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var options=NativeDesktopWorkflow.Options(Settings(),3067);options.AllowClose=true;options.ApprovedProcesses=f.Process.Entries.Select(p=>p.Key).ToArray();options.SavedDesktopProfile=Core.Json.Serialize(Profile(f));
            var other=Path.Combine(f.P.RoamingData,"other");System.IO.Directory.CreateDirectory(other);f.Process.Entries[1].CommandLine="app.exe --user-data-dir="+NativeData.Quoted(other);
            ProfileFails(()=>NativeDesktopWorkflow.WebSocketInfo(f.P,f.Services,options),"changed directory closed");Assert(f.Process.Requested==0,"closed changed profile");
        });
        Check("带空格中文目录和代理CLI共同传入启动",()=>{
            var f=new Fixture();f.AddDesktop(81,501,Path.Combine(f.P.RoamingData,"桌面 资料"));var profile=Profile(f);var start=WebSocketStartup.StartInfo(Core.Exe,new Uri("http://127.0.0.1:3067"));
            WebSocketStartup.ApplySavedCliEnvironment(start,key=>key=="CODEX_CLI_PATH"?"saved-cli":null);DesktopProfile.Apply(start,profile);
            Assert(DesktopProfile.DirectoryArgument(NativeData.Quoted(Core.Exe)+" "+start.Arguments)==profile.path&&start.EnvironmentVariables["CODEX_ELECTRON_USER_DATA_PATH"]==profile.path&&start.EnvironmentVariables["HTTPS_PROXY"]=="http://127.0.0.1:3067/"&&start.EnvironmentVariables["NODE_USE_ENV_PROXY"]=="1"&&start.EnvironmentVariables["CODEX_CLI_PATH"]=="saved-cli","startup environment lost");
        });
        Check("隔离子进程实际收到目录代理与CLI",()=>{
            var f=new Fixture();f.AddDesktop(81,501,Path.Combine(f.P.RoamingData,"中文 资料"));var profile=Profile(f);var output=Path.Combine(f.P.LocalData,"profile-child.json");
            var start=WebSocketStartup.StartInfo(Core.Exe,new Uri("http://127.0.0.1:3067"));DesktopProfile.Apply(start,profile);WebSocketStartup.ApplySavedCliEnvironment(start,key=>key=="CODEX_CLI_PATH"?"selected-cli":null);
            System.IO.Directory.CreateDirectory(f.P.LocalData);start.Arguments+=" --profile-fixture "+NativeData.Quoted(output);start.CreateNoWindow=true;start.WindowStyle=System.Diagnostics.ProcessWindowStyle.Hidden;
            using(var child=System.Diagnostics.Process.Start(start)){Assert(child.WaitForExit(10000)&&child.ExitCode==0,"isolated child did not finish");}
            var received=Core.ReadJson(System.IO.File.ReadAllText(output));Assert(Core.Value(received,"argument","")=="--user-data-dir="+profile.path&&Core.Value(received,"profile","")==profile.path&&Core.Value(received,"proxy","")=="http://127.0.0.1:3067/"&&Core.Value(received,"websocket","")=="1"&&Core.Value(received,"cli","")=="selected-cli","child arguments or environment changed");
        });
        Check("实际启动目录不匹配不报告成功",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var expected=Profile(f);f.Process.Entries.Clear();f.AddDesktop(82,502,Path.Combine(f.P.RoamingData,"different"));
            ProfileFails(()=>DesktopProfile.ObserveStarted(expected,()=>Profile(f),1).GetAwaiter().GetResult(),"mismatched start succeeded");
        });
        Check("新进程沿用原目录与身份可验收",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var expected=Profile(f);f.Process.Entries.Clear();f.AddDesktop(82,502,expected.path);
            var actual=DesktopProfile.ObserveStarted(expected,()=>Profile(f),1).GetAwaiter().GetResult();Assert(actual.processes.SequenceEqual(new[]{"82:502"})&&actual.path==expected.path,"process renewal rejected");
        });
        Check("已观测目录首次产生执行器身份仍保持目录",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var expected=Profile(f);Assert(!expected.identityPresent,"fixture already registered");
            System.IO.File.WriteAllText(Path.Combine(expected.path,DesktopProfile.IdentityFile),"fixture-official-first-registration");
            var actual=DesktopProfile.ObserveStarted(expected,()=>Profile(f),1).GetAwaiter().GetResult();Assert(actual.path==expected.path&&actual.identityPresent,"first registration in confirmed directory rejected");
        });
        Check("启动失败保留记录且不自动回退",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var expected=Profile(f);DesktopProfile.Save(f.P,expected);var before=System.IO.File.ReadAllText(DesktopProfile.RecordPath(f.P));int attempts=0;bool failed=false;
            try{DesktopProfile.ObserveStarted(expected,()=>{attempts++;return null;},1).GetAwaiter().GetResult();}catch(InvalidDataException){failed=true;}
            Assert(failed&&attempts==1&&System.IO.File.ReadAllText(DesktopProfile.RecordPath(f.P))==before&&f.Process.Requested==0,"failed startup changed state");
        });
        Check("资料目录证据跨工作进程传递",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var settings=Settings();var preflight=Core.ReadJson(Core.Json.Serialize(NativeDesktopWorkflow.Preflight(f.P,f.Services,NativeDesktopWorkflow.Options(settings,3067),settings)));
            settings["desktopProfileRecord"]=Core.Json.Serialize(DesktopProfile.FromReport(preflight));var options=NativeDesktopWorkflow.Options(Core.NormalizeOptions(settings),3067);
            DesktopProfile.RequireSame(Profile(f),DesktopProfile.Parse(options.SavedDesktopProfile),true);Assert(!System.IO.File.Exists(DesktopProfile.RecordPath(f.P)),"preflight persisted on cancel");
        });
        Check("失败项重试不覆盖已保存目录",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var settings=Settings();settings["allowCloseRunningApp"]=true;settings["closeApprovedProcesses"]=String.Join(",",f.Process.Entries.Select(p=>p.Key));f.Fail="guard";Assert(f.Run(settings,true)==2,"expected partial initialization");
            var before=System.IO.File.ReadAllText(DesktopProfile.RecordPath(f.P));settings=Settings();settings["retryStages"]="guard";f.Fail="";Assert(f.Run(settings,true)==0&&System.IO.File.ReadAllText(DesktopProfile.RecordPath(f.P))==before,"retry changed saved profile");
        });
    }
    static NativeOptions RecoveryOptions(Fixture f,bool close){
        var settings=Settings();settings["restoreDesktopProfile"]=true;var options=NativeDesktopWorkflow.Options(settings,3067);
        var report=Core.ReadJson(Core.Json.Serialize(NativeDesktopWorkflow.WebSocketInfo(f.P,f.Services,options)));
        options.SavedDesktopProfile=Core.Json.Serialize(report["desktopProfile"]);if(report["currentDesktopProfile"]!=null)options.ObservedDesktopProfile=Core.Json.Serialize(report["currentDesktopProfile"]);
        options.AllowClose=close;options.ApprovedProcesses=f.Process.Entries.Select(p=>p.Key).ToArray();return options;
    }
    static void ProductContinuityChecks(){
        Check("重复初始化持续使用相同资料和身份",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var path=Profile(f).path;var identity=Path.Combine(path,DesktopProfile.IdentityFile);System.IO.File.WriteAllText(identity,"stable-original");var before=System.IO.File.ReadAllBytes(identity);
            for(int run=0;run<2;run++){if(run>0)f.AddDesktop(82,502,path);var settings=Settings();settings["allowCloseRunningApp"]=true;settings["closeApprovedProcesses"]=String.Join(",",f.Process.Entries.Select(p=>p.Key));Assert(f.Run(settings,false)==0,"repeat initialization failed");Assert(DesktopProfile.Load(f.P,DateTime.UtcNow).path==path&&System.IO.File.ReadAllBytes(identity).SequenceEqual(before),"original identity drifted");}
        });
        Check("另一套运行实例不会覆盖可靠原记录",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var original=Profile(f);DesktopProfile.Save(f.P,original);var before=System.IO.File.ReadAllText(DesktopProfile.RecordPath(f.P));f.Process.Entries.Clear();f.AddDesktop(82,502,Path.Combine(f.P.RoamingData,"different"));
            ProfileFails(()=>NativeDesktopWorkflow.WebSocketInfo(f.P,f.Services,NativeDesktopWorkflow.Options(Settings(),3067)),"new profile silently accepted");
            var settings=Settings();ProfileFails(()=>NativeDesktopWorkflow.Preflight(f.P,f.Services,NativeDesktopWorkflow.Options(settings,3067),settings),"initialization replaced baseline");
            Assert(f.Process.Requested==0&&System.IO.File.ReadAllText(DesktopProfile.RecordPath(f.P))==before,"baseline overwritten or desktop closed");
        });
        Check("双连接有可靠旧观测时自动确定恢复目标",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var original=Profile(f);DesktopProfile.Save(f.P,original);f.Process.Entries.Clear();f.AddDesktop(82,502,Path.Combine(f.P.RoamingData,"different"));
            var options=RecoveryOptions(f,false);Assert(DesktopProfile.Parse(options.SavedDesktopProfile).path==original.path&&DesktopProfile.Parse(options.ObservedDesktopProfile).path!=original.path&&f.Process.Requested==0,"recovery target guessed or closed early");
        });
        Check("恢复只关闭已确认实例且保留原记录",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var original=Profile(f);DesktopProfile.Save(f.P,original);var before=System.IO.File.ReadAllText(DesktopProfile.RecordPath(f.P));f.Process.Entries.Clear();f.AddDesktop(82,502,Path.Combine(f.P.RoamingData,"different"));var options=RecoveryOptions(f,true);
            var result=Core.ReadJson(Core.Json.Serialize(NativeDesktopWorkflow.WebSocketInfo(f.P,f.Services,options)));
            Assert(f.Process.Requested==2&&f.Process.Entries.Count==0&&DesktopProfile.FromReport(result).path==original.path&&System.IO.File.ReadAllText(DesktopProfile.RecordPath(f.P))==before,"recovery switched target or overwrote original");
        });
        Check("恢复目标在确认后变化则不关闭",()=>{
            var f=new Fixture();f.AddDesktop(81,501);DesktopProfile.Save(f.P,Profile(f));f.Process.Entries.Clear();f.AddDesktop(82,502,Path.Combine(f.P.RoamingData,"different"));var options=RecoveryOptions(f,true);DesktopProfile.Save(f.P,Profile(f));
            ProfileFails(()=>NativeDesktopWorkflow.WebSocketInfo(f.P,f.Services,options),"changed recovery target accepted");Assert(f.Process.Requested==0,"closed before target recheck");
        });
        Check("恢复时当前实例变化则不关闭",()=>{
            var f=new Fixture();f.AddDesktop(81,501);DesktopProfile.Save(f.P,Profile(f));f.Process.Entries.Clear();f.AddDesktop(82,502,Path.Combine(f.P.RoamingData,"different"));var options=RecoveryOptions(f,true);f.Process.Entries[0].Started++;
            bool blocked=false;try{NativeDesktopWorkflow.WebSocketInfo(f.P,f.Services,options);}catch(InvalidOperationException){blocked=true;}Assert(blocked&&f.Process.Requested==0,"changed source instance closed");
        });
        Check("缺少原观测时恢复不采用当前错误实例",()=>{
            var f=new Fixture();f.AddDesktop(82,502,Path.Combine(f.P.RoamingData,"different"));try{RecoveryOptions(f,false);Assert(false,"recovery invented original");}catch(InvalidDataException ex){Assert(ex.Message.Contains("确认原连接上线")&&ex.Message.Contains("沿用当前 Codex"),"missing actionable recovery route");}Assert(f.Process.Requested==0&&!System.IO.File.Exists(DesktopProfile.RecordPath(f.P)),"recovery wrote guessed original");
        });
        Check("已使用原Codex时恢复检查不触发关闭",()=>{
            var f=new Fixture();f.AddDesktop(81,501);DesktopProfile.Save(f.P,Profile(f));var settings=Settings();settings["restoreDesktopProfile"]=true;
            var report=Core.ReadJson(Core.Json.Serialize(NativeDesktopWorkflow.WebSocketInfo(f.P,f.Services,NativeDesktopWorkflow.Options(settings,3067))));Assert(Convert.ToBoolean(report["alreadyOriginal"])&&f.Process.Requested==0,"unneeded recovery requested");
        });
        Check("失效记录可由可靠当前实例重新识别",()=>{
            var f=new Fixture();f.AddDesktop(81,501);var file=DesktopProfile.RecordPath(f.P);System.IO.Directory.CreateDirectory(Path.GetDirectoryName(file));System.IO.File.WriteAllText(file,"invalid-json");
            var selected=DesktopProfile.Select(f.P,Profile(f),false,f.Log.Add);Assert(selected.path==Profile(f).path&&System.IO.File.ReadAllText(file)=="invalid-json"&&f.Process.Requested==0,"inspection overwrote record");
            ProfileFails(()=>DesktopProfile.Select(f.P,null,false,f.Log.Add),"invalid record without running evidence accepted");
        });
        Check("多资料实例恢复仍需先减少歧义",()=>{
            var f=new Fixture();f.AddDesktop(81,501);DesktopProfile.Save(f.P,Profile(f));f.AddDesktop(82,502,Path.Combine(f.P.RoamingData,"other"));
            ProfileFails(()=>RecoveryOptions(f,false),"multiple profiles silently chosen");Assert(f.Process.Requested==0,"multiple instances closed");
        });
        Check("普通提示隐藏技术目录但日志保留证据",()=>{
            var error=DesktopProfile.Issue("请从平时使用的图标打开 Codex 后重试。","private/profile/path");var lines=new List<string>();DesktopProfile.ReportFailure(error,lines.Add);var message=DesktopProfile.FailureMessage(String.Join("\n",lines),"fallback");
            Assert(message==error.Message&&!message.Contains("private")&&lines.Any(x=>x.Contains("private/profile/path")),"technical path leaked or evidence lost");
        });
        Check("正常无实例启动不增加确认且恢复保留影响确认",()=>{
            var w=TestWindow();try{
                Assert(UiCall(w,"CreateStartupConfirmation","启动 Codex",0,false)==null,"normal launch added confirmation");
                foreach(var count in new[]{0,2}){var dialog=(System.Windows.Window)UiCall(w,"CreateStartupConfirmation","恢复原来的 Codex",count,true);var text=String.Join("\n",InitializationFlowTests.Descendants<System.Windows.Controls.TextBlock>((System.Windows.DependencyObject)dialog.Content).Select(x=>x.Text));var buttons=InitializationFlowTests.Descendants<System.Windows.Controls.Button>((System.Windows.DependencyObject)dialog.Content).ToArray();Assert(text.Contains("远程请求")&&text.Contains("继续执行")&&!text.Contains("资料目录")&&!text.Contains("installation")&&buttons.Single(x=>Convert.ToString(x.Content)=="取消").IsDefault,"recovery impact or simple language lost");if(count>0)Assert(text.Contains("全部正在进行的对话"),"close impact missing");dialog.Close();}
                UiCall(w,"Navigate","连接与修复");Assert(InitializationFlowTests.Descendants<System.Windows.Controls.Button>((System.Windows.DependencyObject)w.Content).Any(x=>Convert.ToString(x.Content)=="恢复原来的 Codex")&&InitializationFlowTests.Descendants<System.Windows.Controls.Button>((System.Windows.DependencyObject)w.Content).Any(x=>Convert.ToString(x.Content)=="沿用当前 Codex"),"recovery action missing");
            }finally{w.Close();}
        });
    }
}
}
