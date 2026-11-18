using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using CodexKit.Native;

namespace CodexKit {
internal static partial class NativeDesktopTests {
    static void InitializationCompatibilityChecks(){
        Check("资料目录对工具不可见不阻塞正常初始化",()=>{
            var f=new Fixture();var profile=Path.Combine(f.P.RoamingData,"Codex","web","Codex");f.AddDesktop(81,501,profile);var settings=Settings();
            var report=Core.ReadJson(Core.Json.Serialize(NativeDesktopWorkflow.Preflight(f.P,f.Services,NativeDesktopWorkflow.Options(settings,3067),settings)));
            Assert(!System.IO.Directory.Exists(profile)&&Convert.ToBoolean(report["requiresClose"])&&((System.Collections.IList)report["processes"]).Count==2&&f.Process.Requested==0,"unreadable profile blocked preflight or closed desktop");
            settings["allowCloseRunningApp"]=true;settings["closeApprovedProcesses"]=String.Join(",",f.Process.Entries.Select(x=>x.Key));
            Assert(f.Run(settings,false,3067)==0&&f.Process.Requested==2&&!System.IO.Directory.Exists(profile),"initialization required or created a desktop profile");
        });
        Check("旧资料观测记录损坏不阻塞也不覆盖",()=>{
            var f=new Fixture();var record=Path.Combine(f.P.LocalData,"CodexInitKit","Desktop","desktop-profile.json");NativeData.Write(record,"legacy-observation");f.AddDesktop(81,501);
            var settings=Settings();NativeDesktopWorkflow.Preflight(f.P,f.Services,NativeDesktopWorkflow.Options(settings,3067),settings);
            Assert(NativeData.Read(record)=="legacy-observation"&&f.Process.Requested==0,"legacy record read, replaced or closed desktop");
        });
        Check("初始化保留登录与官方桌面资料",()=>{
            var f=new Fixture();var profile=Path.Combine(f.P.RoamingData,"Codex","web","Codex");var files=new[]{Path.Combine(f.P.Home,"auth.json"),Path.Combine(f.P.Home,"state_5.sqlite"),Path.Combine(profile,"Preferences"),Path.Combine(profile,".codex-tpp-local-executor-installation-id")};
            foreach(var file in files)NativeData.Write(file,"existing-user-data");var before=files.ToDictionary(x=>x,x=>new{bytes=NativeFile.ReadAllBytes(x),modified=System.IO.File.GetLastWriteTimeUtc(NativePath.Io(x))});
            Assert(f.Run(Settings(),false)==0,"initialization failed");Assert(files.All(file=>NativeFile.ReadAllBytes(file).SequenceEqual(before[file].bytes)&&System.IO.File.GetLastWriteTimeUtc(NativePath.Io(file))==before[file].modified),"login or desktop data changed");
        });
        Check("连接界面不再提供专用启动与身份恢复",()=>{
            var window=TestWindow();try{
                UiCall(window,"Navigate","连接与修复");var buttons=InitializationFlowTests.Descendants<System.Windows.Controls.Button>((System.Windows.DependencyObject)window.Content).Select(x=>Convert.ToString(x.Content)).ToArray();
                Assert(!buttons.Contains("恢复原来的 Codex")&&!buttons.Contains("沿用当前 Codex")&&!buttons.Any(x=>x.Contains("启动 Codex")),"identity recovery or launcher controls remain");
            }finally{window.Close();}
        });
    }
}
}
