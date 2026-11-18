using System;
using System.IO;
using System.Collections.Generic;
using CodexKit.Native;
namespace CodexKit {
internal sealed class NativeDesktopBrowser {
    readonly NativePaths paths;readonly NativeServices services;readonly Action<string> log;
    internal bool Changed;
    internal NativeDesktopBrowser(NativePaths p,NativeServices s,Action<string> emit){paths=p;services=s;log=emit;}
    internal bool RequiresChange(){return !new NativeCua(paths,services).Run(false,paths.BackupRoot).Passed;}
    internal bool Check(){return Run(false);}
    internal void Repair(){Run(true);}
    bool Run(bool repair){
        Changed=false;var root=Path.Combine(paths.LocalData,"CodexInitKit","Desktop","BrowserConnection");var app=services.Desktop.Discover();
        var result=new NativeCua(paths,services).Run(repair,root);Changed=result.Changed;
        var report=new Dictionary<string,object>{{"desktopVersion",app.Version},{"runtime",result.Target},{"integrity",result.Passed?"组件与当前官方源一致":"组件需要修复"},{"connection","真实浏览器连接待单独验收"},{"checkedAt",DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}};
        log("@@BROWSER@@"+Core.Json.Serialize(report));log(result.Passed?"浏览器电脑操作组件完整；真实连接待单独验收。":"浏览器电脑操作组件需要修复。");return result.Passed;
    }
}
}
