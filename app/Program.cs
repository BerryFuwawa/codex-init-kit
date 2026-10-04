using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexKit {
public static class Program {
    [STAThread] public static int Main(string[] args) {
        if(args.Length>0 && args[0]=="--worker") return Core.Worker(args);
        if(args.Length==2 && args[0]=="--verify") return Verify(args[1]);
        if(args.Length==2 && args[0]=="--status-json") return Status(args[1]);
        if(args.Length==2 && args[0]=="--render-preview") {var previewApp=new Application();var previewWindow=new MainWindow(true);previewWindow.RenderPages(Path.GetFullPath(args[1]));previewWindow.Close();return 0;}
        if(args.Length==2 && args[0]=="--check-update-json") {
            try { File.WriteAllText(Path.GetFullPath(args[1]),Core.Json.Serialize(Core.CheckUpdate(10808)),new UTF8Encoding(false));return 0; }
            catch(Exception ex) {File.WriteAllText(Path.GetFullPath(args[1]),Core.Json.Serialize(new {error=ex.Message}),new UTF8Encoding(false));return 1;}
        }
        if(args.Length>0 && args[0]=="--self-test") {
            var dir=Path.Combine(Path.GetTempPath(),"CodexKit-test-"+Guid.NewGuid().ToString("N"));
            try { Core.Extract(dir); foreach(var n in new[]{"init.cmd","guard.cmd","cua.cmd","Backend.ps1"}) if(new FileInfo(Path.Combine(dir,n)).Length<100) return 2; if(Core.Operations.Distinct().Count()!=Core.Operations.Length) return 3; return 0; }
            finally { if(Directory.Exists(dir)) Directory.Delete(dir,true); }
        }
        if(args.Length>0 && args[0]!="--preview")return 64;
        var app=new Application();
        app.DispatcherUnhandledException+=(s,e)=>{MessageBox.Show(e.Exception.Message,"Codex Init Kit",MessageBoxButton.OK,MessageBoxImage.Error); e.Handled=true;};
        var window=new MainWindow(args.Contains("--preview"));
        return app.Run(window);
    }
    static int Status(string reportPath) {
        var id=Guid.NewGuid().ToString("N");
        var code=Core.Worker(new[]{"--worker","status",id,Environment.SystemDirectory.Substring(0,1),"10808","0",Core.Sid});
        var lines=File.ReadAllLines(Path.Combine(Core.Logs,id+".log"),Encoding.UTF8);
        var line=lines.LastOrDefault(x=>x.StartsWith("@@STATUS@@"));
        object state=null; string error=null;try{if(line==null)throw new InvalidDataException("Missing status output");state=Core.ReadJson(line.Substring(10));}catch(Exception ex){code=1;error=ex.Message;}
        var report=new Dictionary<string,object>{{"exit_code",code},{"status",state},{"error",error},{"log",Path.Combine(Core.Logs,id+".log")}};
        File.WriteAllText(Path.GetFullPath(reportPath),Core.Json.Serialize(report),new UTF8Encoding(false)); return code;
    }
    static int Verify(string reportPath) {
        var checks=new Dictionary<string,object>(); var errors=new System.Collections.Generic.List<string>();
        var dir=Path.Combine(Path.GetTempPath(),"CodexKit-verify-"+Guid.NewGuid().ToString("N"));
        try {
            Core.Extract(dir);
            var logo=Brand.Logo();if(logo.PixelWidth!=512||logo.PixelHeight!=512)throw new InvalidDataException("Invalid logo resource");checks["logo_resource"]=true;
            foreach(var name in new[]{"init.cmd","guard.cmd","cua.cmd","Backend.ps1"}){if(new FileInfo(Path.Combine(dir,name)).Length<100)throw new InvalidDataException("Empty payload: "+name);checks[name]=new{bytes=new FileInfo(Path.Combine(dir,name)).Length,sha256=Core.Hash(Path.Combine(dir,name))};}
            var ps=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
            var script="$e=$null;$t=$null;[System.Management.Automation.Language.Parser]::ParseFile('"+Path.Combine(dir,"Backend.ps1").Replace("'","''")+"',[ref]$t,[ref]$e)|Out-Null;if($e.Count){$e|Out-String|Write-Output;exit 1};exit 0";
            var info=new ProcessStartInfo(ps,"-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script))){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            info.StandardOutputEncoding=Encoding.UTF8;info.StandardErrorEncoding=Encoding.UTF8;
            using(var proc=Process.Start(info)){var output=proc.StandardOutput.ReadToEnd()+proc.StandardError.ReadToEnd();proc.WaitForExit();checks["backend_syntax"]=proc.ExitCode==0;if(proc.ExitCode!=0)errors.Add(output);}
            var testScript="$ErrorActionPreference='Stop';try { & '"+Path.Combine(dir,"Backend.Tests.ps1").Replace("'","''")+"' -PayloadRoot '"+dir.Replace("'","''")+"' -BackendPath '"+Path.Combine(dir,"Backend.ps1").Replace("'","''")+"';exit 0 } catch { $_|Out-String|Write-Output;exit 1 }";
            info.Arguments="-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(testScript));
            using(var proc=Process.Start(info)){var output=proc.StandardOutput.ReadToEnd()+proc.StandardError.ReadToEnd();proc.WaitForExit();checks["backend_regressions"]=proc.ExitCode==0;if(proc.ExitCode!=0)errors.Add(output);}
            var catalog=ModelCatalog.Parse("{\"models\":[{\"slug\":\"fixture-model\",\"default_reasoning_level\":\"high\",\"supported_reasoning_levels\":[{\"effort\":\"high\"},{\"effort\":\"low\"}]}]}");
            if(!catalog.Check("fixture-model","high").Contains("核对通过")||catalog.Models["fixture-model"].Efforts[0]!="high")throw new InvalidDataException("Model catalog validation or ordering failed");
            foreach(var bad in new[]{"display name","bad;model","fixture-model\n"})if(ModelCatalog.ValidId(bad))throw new InvalidDataException("Unsafe model catalog ID accepted");
            bool unsupported=false;try{catalog.Check("fixture-model","ultra");}catch(InvalidOperationException){unsupported=true;}if(!unsupported)throw new InvalidDataException("Unsupported model effort accepted");unsupported=false;try{catalog.Check("vendor/custom:v1","max");}catch(InvalidOperationException){unsupported=true;}if(!unsupported)throw new InvalidDataException("Unlisted model accepted");var preset=ModelCatalog.Preset();if(preset.Models.Count!=8||preset.Models["gpt-5.6-luna"].Efforts.Contains("ultra"))throw new InvalidDataException("Preset model choices changed");checks["model_catalog_validation"]=true;
            var app=new Application(); var window=new MainWindow(true);window.VerifyPages(); checks["pages_and_navigation"]=true;SmoothScrollViewer.Verify();checks["smooth_scrolling"]=true; window.Close();
            checks["worker_rejects_invalid_operation"]=Core.Worker(new[]{"--worker","invalid",Guid.NewGuid().ToString("N"),"D","10808","0",Core.Sid})==64;
            checks["worker_rejects_invalid_drive"]=Core.Worker(new[]{"--worker","status",Guid.NewGuid().ToString("N"),"&","10808","0",Core.Sid})==64;
            var options=Core.NormalizeOptions(new Dictionary<string,object>{{"folderManagement",false},{"subagents",false},{"cuaRepair",false},{"parentModel","custom-parent"},{"childModel","custom-child"}});if((bool)options["folderManagement"]||(bool)options["subagents"]||(bool)options["cuaRepair"]||Convert.ToString(options["parentModel"])!="custom-parent")throw new InvalidDataException("Setup options were not preserved");checks["setup_options"]=true;
            var defaults=Core.NormalizeOptions(null);if(!(bool)defaults["folderManagement"]||(bool)defaults["subagents"]||!(bool)defaults["cuaRepair"]||Convert.ToString(defaults["proxyHost"])!=Core.DefaultProxyHost)throw new InvalidDataException("Setup defaults changed");checks["setup_option_defaults"]=true;
            var customProxy=Core.NormalizeOptions(new Dictionary<string,object>{{"proxyHost","192.168.50.10"}});if(Convert.ToString(customProxy["proxyHost"])!="192.168.50.10")throw new InvalidDataException("Custom IPv4 proxy host was not preserved");
            var customIpv6=Core.NormalizeOptions(new Dictionary<string,object>{{"proxyHost","2001:db8::7"}});if(Convert.ToString(customIpv6["proxyHost"])!="2001:db8::7")throw new InvalidDataException("Custom IPv6 proxy host was not preserved");
            var blankProxy=Core.NormalizeOptions(new Dictionary<string,object>{{"proxyHost","   "}});if(Convert.ToString(blankProxy["proxyHost"])!=Core.DefaultProxyHost)throw new InvalidDataException("Blank proxy host was not normalized");checks["proxy_host_normalization"]=true;
            foreach(var badHost in new[]{"localhost","127.0.0.1;bad","http://127.0.0.1"}) { bool rejected=false;try{Core.NormalizeProxyHost(badHost);}catch{rejected=true;}if(!rejected)throw new InvalidDataException("Unsafe proxy host accepted: "+badHost); }checks["proxy_host_rejects_unsafe_input"]=true;
            var unsafeOptions=Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"parentModel\":\"bad & model\"}"));if(Core.Worker(new[]{"--worker","status",Guid.NewGuid().ToString("N"),"D","0","0",Core.Sid,unsafeOptions})!=64)throw new InvalidDataException("Unsafe worker model accepted");checks["worker_rejects_unsafe_model"]=true;
            var unsafeProxyOptions=Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"proxyHost\":\"127.0.0.1;bad\"}"));if(Core.Worker(new[]{"--worker","status",Guid.NewGuid().ToString("N"),"D","0","0",Core.Sid,unsafeProxyOptions})!=64)throw new InvalidDataException("Unsafe worker proxy host accepted");checks["worker_rejects_unsafe_proxy_host"]=true;
            checks["operation_count"]=Core.Operations.Length;
            var valid="{\"schema_version\":1,\"repository\":\""+Core.Repository+"\",\"version\":\"2.0.1\",\"url\":\"https://github.com/"+Core.Repository+"/releases/download/v2.0.1/CodexInitKit.exe\",\"sha256\":\""+new string('a',64)+"\"}";
            Core.ValidateUpdate(valid);checks["update_manifest_valid"]=true;
            foreach(var bad in new[]{valid.Replace(Core.Repository,"other/repo"),valid.Replace("https://github.com/","http://github.com/"),valid.Replace(new string('a',64),"bad")}) { bool rejected=false;try{Core.ValidateUpdate(bad);}catch{rejected=true;}if(!rejected)throw new InvalidDataException("Unsafe update manifest accepted"); }checks["update_manifest_rejects_unsafe"]=true;
            foreach(var scenario in new[]{"success","stage_changed","target_changed","restart_failed"}) {
                var fixture=Path.Combine(dir,scenario);Directory.CreateDirectory(fixture);var current=Path.Combine(fixture,"app.exe");var next=Path.Combine(fixture,"next.exe");var updateLog=Path.Combine(fixture,"update.log");File.WriteAllText(current,"old");File.WriteAllText(next,"new");
                var replacement=Core.BuildUpdateScript(next,current,0,updateLog,scenario=="restart_failed");
                if(scenario=="stage_changed")File.WriteAllText(next,"corrupt");
                if(scenario=="target_changed")File.WriteAllText(current,"changed");
                if(scenario=="restart_failed")replacement=replacement.Replace("Start-Process -FilePath $target -WorkingDirectory ([IO.Path]::GetDirectoryName($target)) | Out-Null;","throw 'Simulated restart failure';");
                info.Arguments="-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(replacement));
                using(var proc=Process.Start(info)){var output=proc.StandardOutput.ReadToEnd()+proc.StandardError.ReadToEnd();proc.WaitForExit();if((proc.ExitCode==0)!=(scenario=="success"))throw new IOException("Updater exit status: "+scenario+" "+output);}
                var expected=scenario=="success"?"new":scenario=="target_changed"?"changed":"old";
                if(File.ReadAllText(current)!=expected||File.Exists(next)||Directory.GetFiles(fixture,"*.tmp").Length!=0||!File.Exists(updateLog))throw new IOException("Updater regression: "+scenario);
                if(scenario=="restart_failed"&&!File.ReadAllText(updateLog).Contains("rollback succeeded"))throw new IOException("Updater rollback missing");
                checks["updater_"+scenario]=true;
            }
        }catch(Exception ex){errors.Add(ex.ToString());}
        finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
        var report=new Dictionary<string,object>{{"version",Core.Version},{"passed",errors.Count==0},{"checks",checks},{"errors",errors.ToArray()}};
        File.WriteAllText(Path.GetFullPath(reportPath),Core.Json.Serialize(report),new UTF8Encoding(false));return errors.Count==0?0:1;
    }
}
}
