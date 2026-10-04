using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;
using System.Net.Sockets;

namespace CodexKit {
public static class Core {
    public const string Version = "2.1.0";
    public const string Repository = "BerryFuwawa/codex-init-kit";
    public static readonly string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexInitKit", "Desktop");
    public static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    public static readonly string[] Operations = {"status","initialize","proxy","init-rollback","cua-check","cua-repair","guard-install","guard-update","guard-native","guard-current","guard-rollback","guard-uninstall","doctor"};
    public static string Exe { get { return Assembly.GetExecutingAssembly().Location; } }
    public const string DefaultProxyHost = "127.0.0.1";
    const string DefaultParentModel = "gpt-6.1-sol";
    const string DefaultChildModel = "gpt-5.6-luna";
    const string DefaultParentEffort = "medium";
    const string DefaultChildEffort = "max";
    static readonly Regex ModelIdPattern = new Regex("^[A-Za-z0-9][A-Za-z0-9._/:+-]{0,127}$", RegexOptions.CultureInvariant);
    static readonly Regex EffortPattern = new Regex("^[A-Za-z][A-Za-z0-9_-]{0,31}$", RegexOptions.CultureInvariant);
    public static string Sid { get { return WindowsIdentity.GetCurrent().User.Value; } }
    public static bool Admin { get { return new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator); } }
    public static string Hash(string path) { using(var s=File.OpenRead(path)) using(var h=SHA256.Create()) return BitConverter.ToString(h.ComputeHash(s)).Replace("-", "").ToLowerInvariant(); }
    public static string Quote(string value) { if(value.Contains("\"") || value.Contains("\r") || value.Contains("\n")) throw new ArgumentException("Invalid argument"); return "\""+value+"\""; }
    public static string Logs { get { var path=Path.Combine(Root,"Logs"); Directory.CreateDirectory(path); return path; } }
    public static string Extract(string directory,bool privileged=false) {
        if(privileged) {
            var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);
            var admins=new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null);var system=new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null);
            acl.SetOwner(admins);
            foreach(var sid in new[]{admins,system})acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
            if(Directory.Exists(directory))throw new IOException("Job directory already exists");
            Directory.CreateDirectory(directory,acl);
        } else Directory.CreateDirectory(directory);
        var assembly=Assembly.GetExecutingAssembly();
        foreach(var name in new[]{"init.cmd","guard.cmd","cua.cmd","Backend.ps1","Backend.Tests.ps1"}) {
            using(var stream=assembly.GetManifestResourceStream("Payload."+name)) {
                if(stream==null) throw new InvalidOperationException("Missing resource: "+name);
                using(var file=new FileStream(Path.Combine(directory,name), FileMode.CreateNew,FileAccess.Write,FileShare.Read)) stream.CopyTo(file);
            }
        }
        return directory;
    }
    public static bool NeedsAdmin(string operation) { return operation!="status" && operation!="cua-check" && operation!="doctor"; }

    static Dictionary<string,object> DefaultOptions() {
        return new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase) {
            {"folderManagement", true}, {"subagents", false}, {"cuaRepair", true},
            {"parentModel", DefaultParentModel}, {"childModel", DefaultChildModel},
            {"parentEffort", DefaultParentEffort}, {"childEffort", DefaultChildEffort},
            {"proxyHost", DefaultProxyHost}
        };
    }
    static bool ReadOptionBoolean(Dictionary<string,object> source,string key,bool fallback) {
        object value;
        if(source==null || !source.TryGetValue(key,out value) || value==null) return fallback;
        if(value is bool) return (bool)value;
        throw new ArgumentException("Option "+key+" must be boolean");
    }
    static string ReadOptionString(Dictionary<string,object> source,string key,string fallback) {
        object value;
        if(source==null || !source.TryGetValue(key,out value) || value==null) return fallback;
        var text=value as string;
        if(text==null || String.IsNullOrWhiteSpace(text)) throw new ArgumentException("Option "+key+" must be a non-empty string");
        if((key=="parentModel" || key=="childModel") && !ModelIdPattern.IsMatch(text)) throw new ArgumentException("Option "+key+" contains an invalid model id");
        if((key=="parentEffort" || key=="childEffort") && !EffortPattern.IsMatch(text)) throw new ArgumentException("Option "+key+" contains an invalid effort");
        return text;
    }
    public static string NormalizeProxyHost(string value) {
        if(String.IsNullOrWhiteSpace(value)) return DefaultProxyHost;
        IPAddress address;
        var text=value.Trim();
        if(!IPAddress.TryParse(text,out address)) throw new ArgumentException("Option proxyHost must be a valid IPv4 or IPv6 address");
        return address.ToString();
    }
    static string ReadProxyHost(Dictionary<string,object> source) {
        object value;
        if(source==null || !source.TryGetValue("proxyHost",out value) || value==null) return DefaultProxyHost;
        var text=value as string;
        if(text==null) throw new ArgumentException("Option proxyHost must be a string");
        return NormalizeProxyHost(text);
    }
    public static Dictionary<string,object> NormalizeOptions(Dictionary<string,object> source) {
        var normalized=DefaultOptions();
        if(source==null) return normalized;
        normalized["folderManagement"]=ReadOptionBoolean(source,"folderManagement",true);
        normalized["subagents"]=ReadOptionBoolean(source,"subagents",false);
        normalized["cuaRepair"]=ReadOptionBoolean(source,"cuaRepair",true);
        normalized["parentModel"]=ReadOptionString(source,"parentModel",DefaultParentModel);
        normalized["childModel"]=ReadOptionString(source,"childModel",DefaultChildModel);
        normalized["parentEffort"]=ReadOptionString(source,"parentEffort",DefaultParentEffort);
        normalized["childEffort"]=ReadOptionString(source,"childEffort",DefaultChildEffort);
        normalized["proxyHost"]=ReadProxyHost(source);
        return normalized;
    }
    static Dictionary<string,object> DecodeOptions(string value) {
        if(String.IsNullOrWhiteSpace(value)) throw new ArgumentException("Worker options are empty");
        var bytes=Convert.FromBase64String(value);
        var json= new UTF8Encoding(false,true).GetString(bytes);
        return NormalizeOptions(Json.Deserialize<Dictionary<string,object>>(json));
    }
    static string EncodeOptions(Dictionary<string,object> options) {
        var json=Json.Serialize(NormalizeOptions(options));
        return Convert.ToBase64String(new UTF8Encoding(false).GetBytes(json));
    }
    public static int Worker(string[] args) {
        // All executable code comes from this EXE's embedded resources, never a supplied job/script path.
        if((args.Length!=7 && args.Length!=8) || args.Length<2 || !Operations.Contains(args[1])) return 64;
        Guid id; int port;
        Dictionary<string,object> options=null;
        if(!Guid.TryParseExact(args[2],"N",out id) || !int.TryParse(args[4],out port) || port<0 || port>65535) return 64;
        if(args[3].Length!=1 || args[3][0]<'A' || args[3][0]>'Z' || (args[5]!="0" && args[5]!="1")) return 64;
        if(args.Length==8) { try { options=DecodeOptions(args[7]); } catch { return 64; } }
        var log=Path.Combine(Logs,args[2]+".log");
        if(Sid!=args[6]) { File.WriteAllText(log,"管理员账户与原账户不同。操作已停止，请使用同一账户授权。\r\n",Encoding.UTF8); return 65; }
        if(NeedsAdmin(args[1]) && !Admin) return 66;
        var job=NeedsAdmin(args[1])?Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"CodexInitKit-privileged-"+Guid.NewGuid().ToString("N")):Path.Combine(Root,"Jobs",args[2]);
        try {
            var payload=Extract(job,NeedsAdmin(args[1]));
            using(var writer=new StreamWriter(log,false,new UTF8Encoding(false))) {
                writer.AutoFlush=true;
                object gate=new object();
                var psi=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe"));
                psi.UseShellExecute=false; psi.CreateNoWindow=true; psi.RedirectStandardOutput=true; psi.RedirectStandardError=true;
                psi.StandardOutputEncoding=Encoding.UTF8; psi.StandardErrorEncoding=Encoding.UTF8;
                var optionsArgument=options==null?"":" -OptionsBase64 "+Quote(EncodeOptions(options));
                psi.Arguments="-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "+Quote(Path.Combine(payload,"Backend.ps1"))+" -Operation "+Quote(args[1])+" -PayloadRoot "+Quote(payload)+" -Drive "+Quote(args[3])+" -ProxyPort "+port+(args[1]=="initialize"&&args[5]=="1"?" -InstallProtection":"")+optionsArgument;
                using(var process=new Process()) {
                    process.StartInfo=psi;
                    process.OutputDataReceived+=(s,e)=>{if(e.Data!=null) lock(gate) writer.WriteLine(e.Data);};
                    process.ErrorDataReceived+=(s,e)=>{if(e.Data!=null) lock(gate) writer.WriteLine(e.Data);};
                    process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine(); process.WaitForExit();
                    return process.ExitCode;
                }
            }
        } catch(Exception ex) { File.AppendAllText(log,"执行失败："+ex.Message+"\r\n",Encoding.UTF8); return 1; }
        finally { try { if(Directory.Exists(job) && (File.GetAttributes(job)&FileAttributes.ReparsePoint)==0) Directory.Delete(job,true); } catch { } }
    }
    public static Process StartWorker(string operation,string id,string drive,int port,bool guard) {
        return StartWorker(operation,id,drive,port,guard,null);
    }
    public static Process StartWorker(string operation,string id,string drive,int port,bool guard,Dictionary<string,object> options) {
        if(!Operations.Contains(operation)) throw new ArgumentException("Unsupported operation");
        if(port<0 || port>65535) throw new ArgumentOutOfRangeException("port");
        var args="--worker "+operation+" "+id+" "+drive+" "+port+" "+(guard?"1":"0")+" "+Sid;
        if(options!=null) args+=" "+EncodeOptions(options);
        var info=new ProcessStartInfo(Exe,args);
        info.UseShellExecute=true; info.WindowStyle=ProcessWindowStyle.Hidden;
        if(NeedsAdmin(operation) && !Admin) info.Verb="runas";
        return Process.Start(info);
    }
    public static Dictionary<string,object> ReadJson(string json) { return Json.Deserialize<Dictionary<string,object>>(json); }
    public static string Value(Dictionary<string,object> obj,string key,string fallback) { return obj!=null && obj.ContainsKey(key) && obj[key]!=null ? Convert.ToString(obj[key]) : fallback; }
    static string ProxyAddress(string proxyHost,int proxyPort) {
        var normalized=NormalizeProxyHost(proxyHost);
        IPAddress address;
        if(!IPAddress.TryParse(normalized,out address)) throw new ArgumentException("Proxy host must be a valid IPv4 or IPv6 address");
        var host=address.AddressFamily==AddressFamily.InterNetworkV6?"["+normalized+"]":normalized;
        return "http://"+host+":"+proxyPort;
    }
    public static string Fetch(string url,int proxyPort) {
        return Fetch(url,proxyPort,DefaultProxyHost);
    }
    public static string Fetch(string url,int proxyPort,string proxyHost) {
        ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
        var request=(HttpWebRequest)WebRequest.Create(url); request.UserAgent="CodexInitKitDesktop/"+Version;
        request.Timeout=12000; request.ReadWriteTimeout=12000;
        request.Proxy=proxyPort>0?new WebProxy(ProxyAddress(proxyHost,proxyPort)):null;
        using(var response=request.GetResponse()) using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)) return reader.ReadToEnd();
    }
    public static Dictionary<string,object> CheckUpdate(int port) {
        return CheckUpdate(port,DefaultProxyHost);
    }
    public static Dictionary<string,object> CheckUpdate(int port,string proxyHost) {
        var commit=ReadJson(Fetch("https://api.github.com/repos/"+Repository+"/commits/main",port,proxyHost));
        var sha=Value(commit,"sha",""); if(!System.Text.RegularExpressions.Regex.IsMatch(sha,"^[0-9a-f]{40}$")) throw new InvalidDataException("Invalid commit");
        return ValidateUpdate(Fetch("https://raw.githubusercontent.com/"+Repository+"/"+sha+"/desktop-update.json",port,proxyHost));
    }
    public static Dictionary<string,object> ValidateUpdate(string json) {
        var manifest=ReadJson(json);
        if(Value(manifest,"repository","")!=Repository || Value(manifest,"schema_version","")!="1") throw new InvalidDataException("更新清单来源不匹配");
        var version=new Version(Value(manifest,"version",""));
        var url=Value(manifest,"url",""); var uri=new Uri(url);
        if(uri.Scheme!="https" || uri.Host!="github.com" || !uri.AbsolutePath.StartsWith("/"+Repository+"/releases/download/",StringComparison.Ordinal)) throw new InvalidDataException("更新链接不属于本项目");
        if(!System.Text.RegularExpressions.Regex.IsMatch(Value(manifest,"sha256",""),"^[0-9a-f]{64}$")) throw new InvalidDataException("更新校验值不合法");
        return manifest;
    }
    public static Task<string> DownloadUpdate(Dictionary<string,object> manifest,int port) {
        return DownloadUpdate(manifest,port,DefaultProxyHost);
    }
    public static async Task<string> DownloadUpdate(Dictionary<string,object> manifest,int port,string proxyHost) {
        var stage=Path.Combine(Path.GetDirectoryName(Exe),".CodexInitKit-update-"+Guid.NewGuid().ToString("N")+".exe");
        using(var client=new WebClient()) {
            client.Headers[HttpRequestHeader.UserAgent]="CodexInitKitDesktop/"+Version;
            client.Proxy=port>0?new WebProxy(ProxyAddress(proxyHost,port)):null;
            try {
                await client.DownloadFileTaskAsync(new Uri(Value(manifest,"url","")),stage);
                if(Hash(stage)!=Value(manifest,"sha256","")) throw new InvalidDataException("下载校验失败，当前版本保持不变");
                var bytes=File.ReadAllBytes(stage);
                if(bytes.Length<2 || bytes[0]!=77 || bytes[1]!=90) throw new InvalidDataException("下载文件不是 Windows EXE");
                return stage;
            } catch { if(File.Exists(stage)) File.Delete(stage); throw; }
        }
    }
    static string PowerShellLiteral(string value) {
        if(value==null) throw new ArgumentNullException("value");
        return "'"+value.Replace("'","''")+"'";
    }
    public static string BuildUpdateScript(string stage,string target,int pid,string logPath,bool restart) {
        if(String.IsNullOrWhiteSpace(stage)) throw new ArgumentException("Update stage is required", "stage");
        if(String.IsNullOrWhiteSpace(target)) throw new ArgumentException("Update target is required", "target");
        if(String.IsNullOrWhiteSpace(logPath)) throw new ArgumentException("Update log path is required", "logPath");
        var stagePath=Path.GetFullPath(stage); var targetPath=Path.GetFullPath(target); var resultLog=Path.GetFullPath(logPath);
        if(String.Equals(stagePath,targetPath,StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Update stage and target must differ");
        var expectedStageHash=Hash(stagePath); var expectedTargetHash=Hash(targetPath);
        var backupPath=targetPath+".codex-update-backup-"+Guid.NewGuid().ToString("N")+".tmp";
        var rollbackPath=targetPath+".codex-update-rollback-"+Guid.NewGuid().ToString("N")+".tmp";
        var script=new StringBuilder();
        script.Append("$ErrorActionPreference='Stop';");
        script.Append("$stage=").Append(PowerShellLiteral(stagePath)).Append(";");
        script.Append("$target=").Append(PowerShellLiteral(targetPath)).Append(";");
        script.Append("$log=").Append(PowerShellLiteral(resultLog)).Append(";");
        script.Append("$backup=").Append(PowerShellLiteral(backupPath)).Append(";");
        script.Append("$rollback=").Append(PowerShellLiteral(rollbackPath)).Append(";");
        script.Append("$expectedStageHash=").Append(PowerShellLiteral(expectedStageHash)).Append(";");
        script.Append("$expectedTargetHash=").Append(PowerShellLiteral(expectedTargetHash)).Append(";");
        script.Append("$replaced=$false;$exitCode=1;");
        script.Append("function Get-Sha([string]$Path){if(-not [IO.File]::Exists($Path)){throw 'File not found: '+$Path};$stream=[IO.File]::OpenRead($Path);$hash=[Security.Cryptography.SHA256]::Create();try{[BitConverter]::ToString($hash.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}finally{$stream.Dispose();$hash.Dispose()}};");
        script.Append("function Test-Sha([string]$Path,[string]$Expected){try{if(-not [IO.File]::Exists($Path)){return $false};return ((Get-Sha $Path) -ceq $Expected)}catch{return $false}};");
        script.Append("function Write-UpdateLog([string]$Status,[string]$Message){try{$parent=[IO.Path]::GetDirectoryName($log);if($parent){[IO.Directory]::CreateDirectory($parent)|Out-Null};$line=(Get-Date -Format o)+' ['+$Status+'] '+$Message;[IO.File]::AppendAllText($log,$line+[Environment]::NewLine,(New-Object Text.UTF8Encoding($false)))}catch{}};");
        if(pid>0) script.Append("Wait-Process -Id ").Append(pid.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append(" -ErrorAction SilentlyContinue;");
        script.Append("try{");
        script.Append("if(-not (Test-Sha $stage $expectedStageHash)){throw 'Update stage checksum mismatch'};");
        script.Append("if(-not (Test-Sha $target $expectedTargetHash)){throw 'Current executable changed before update'};");
        script.Append("[IO.File]::Replace($stage,$target,$backup);$replaced=$true;");
        script.Append("if(-not (Test-Sha $target $expectedStageHash)){throw 'Updated executable checksum mismatch'};");
        if(restart) script.Append("Start-Process -FilePath $target -WorkingDirectory ([IO.Path]::GetDirectoryName($target)) | Out-Null;");
        script.Append("if([IO.File]::Exists($backup)){[IO.File]::Delete($backup)};");
        script.Append("Write-UpdateLog 'success' 'Update installed';$exitCode=0;");
        script.Append("}catch{$failure=$_.Exception.Message;if($replaced -and (Test-Sha $target $expectedStageHash) -and (Test-Sha $backup $expectedTargetHash)){try{[IO.File]::Replace($backup,$target,$rollback);if(-not (Test-Sha $target $expectedTargetHash)){throw 'Rollback checksum mismatch'};if([IO.File]::Exists($rollback)){[IO.File]::Delete($rollback)};$replaced=$false;$failure=$failure+'; rollback succeeded'}catch{$failure=$failure+'; rollback failed: '+$_.Exception.Message}};Write-UpdateLog 'failure' $failure;}");
        script.Append("finally{if([IO.File]::Exists($stage)){try{[IO.File]::Delete($stage)}catch{}};if($exitCode -eq 0 -or -not $replaced){if([IO.File]::Exists($backup)){try{[IO.File]::Delete($backup)}catch{}};if([IO.File]::Exists($rollback)){try{[IO.File]::Delete($rollback)}catch{}}}};exit $exitCode;");
        return script.ToString();
    }
    public static void ScheduleUpdate(string stage) {
        var target=Exe; var logPath=Path.Combine(Logs,"update.log");
        try {
            var script=BuildUpdateScript(stage,target,Process.GetCurrentProcess().Id,logPath,true);
            var powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"WindowsPowerShell","v1.0","powershell.exe");
            var encoded=Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var info=new ProcessStartInfo(powershell,"-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand "+encoded);
            info.UseShellExecute=false; info.CreateNoWindow=true; info.WindowStyle=ProcessWindowStyle.Hidden;
            using(var process=Process.Start(info)) { }
        } catch {
            try { if(File.Exists(stage)) File.Delete(stage); } catch { }
            throw;
        }
    }
}
}
