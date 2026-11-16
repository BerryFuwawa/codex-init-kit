using File=CodexKit.Native.NativeFile;
using Directory=CodexKit.Native.NativeDirectory;
using FileInfo=CodexKit.Native.NativeFileInfo;
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace CodexKit.Native {
public static class NativeRuntime { public static void Configure(){AppContext.SetSwitch("Switch.System.IO.UseLegacyPathHandling",false);AppContext.SetSwitch("Switch.System.IO.BlockLongPaths",false);} }
public static class NativeData {
    public const string Version="2.3.1.1";
    public static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
    public static readonly JavaScriptSerializer Json=new JavaScriptSerializer{MaxJsonLength=32*1024*1024};
    public static string Resource(string name){using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("Native."+name)){if(s==null)throw new FileNotFoundException("缺少内嵌资源："+name);using(var r=new StreamReader(s,Utf8,true))return r.ReadToEnd();}}
    public static string Read(string file){return File.ReadAllText(file,Utf8);}
    public static void Write(string file,string text){WriteBytes(file,Utf8.GetBytes(text));}
    public static void WriteBytes(string file,byte[] bytes){var target=Path.GetFullPath(file);Directory.CreateDirectory(Path.GetDirectoryName(target));var temp=target+".native-write-"+Guid.NewGuid().ToString("N")+".tmp";try{File.WriteAllBytes(temp,bytes);if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);}finally{if(File.Exists(temp))File.Delete(temp);}}
    public static bool Inside(string path,string root){return Path.GetFullPath(path).StartsWith(Path.GetFullPath(root).TrimEnd('\\','/')+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase);}
    public static string Normalize(string text){return text.Replace("\r\n","\n").Replace("\r","\n");}
    public static void NoLinks(string path){var p=Path.GetFullPath(path);while(!String.IsNullOrEmpty(p)){if((Directory.Exists(p)||File.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("目标路径包含链接，未修改："+p);p=Path.GetDirectoryName(p);}}
    public static string Quoted(string value){if(value==null)value="";if(value.IndexOf('\0')>=0||value.IndexOf('\r')>=0||value.IndexOf('\n')>=0)throw new ArgumentException("命令参数包含换行或空字符。");var b=new StringBuilder("\"");int slashes=0;foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='\"'){b.Append('\\',slashes*2+1);b.Append(c);}else{b.Append('\\',slashes);b.Append(c);}slashes=0;}b.Append('\\',slashes*2);b.Append('"');return b.ToString();}
}
public sealed class NativePaths {
    public string Home,LocalData,RoamingData,Documents,UserTools,Workspace,Self,SearchPath,EntryPrefix;
    public string Config{get{return Path.Combine(Home,"config.toml");}}
    public string Env{get{return Path.Combine(Home,".env");}}
    public string Agents{get{return Path.Combine(Home,"AGENTS.md");}}
    public string RtkDoc{get{return Path.Combine(Home,"RTK.md");}}
    public string Hooks{get{return Path.Combine(Home,"hooks.json");}}
    public string Rtk{get{return Path.Combine(UserTools,"rtk.exe");}}
    public string RtkConfig{get{return Path.Combine(RoamingData,"rtk","config.toml");}}
    public string LegacyCli{get{return Path.Combine(LocalData,"OpenAI","Codex","cli");}}
    public string RuntimeRoot{get{return Path.Combine(LocalData,"OpenAI","Codex","runtimes","cua_node");}}
    public string BackupRoot{get{return Path.Combine(Documents,"Codex初始化备份");}}
    public static NativePaths Current(){var profile=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);return new NativePaths{Home=Environment.GetEnvironmentVariable("CODEX_HOME")??Path.Combine(profile,".codex"),LocalData=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),RoamingData=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),Documents=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),UserTools=Path.Combine(profile,".local","bin"),Workspace=null,Self=Assembly.GetEntryAssembly().Location,SearchPath=Environment.GetEnvironmentVariable("PATH")};}
    public void Validate(){foreach(var p in new[]{Home,LocalData,RoamingData,Documents,UserTools})if(String.IsNullOrWhiteSpace(p)||!Path.IsPathRooted(p))throw new ArgumentException("初始化目录必须为明确的绝对路径。");if(!Directory.Exists(Documents))throw new DirectoryNotFoundException("Documents 目录不可用："+Documents);}
}
public sealed class NativeOptions {
    public bool Confirm,AllowClose,Folder=true,Subagents=true,Cua=true,Proxy=true;
    public string ProxyHost="127.0.0.1",ParentModel="gpt-6.1-sol",ChildModel="gpt-6.1-sol",ParentEffort="high",ChildEffort="high";
    public int ProxyPort=10808;
    public string[] ApprovedProcesses;
    public string ProxyUrl {get{if(!Proxy)return null;var host=ProxyHost.Trim().Trim('[',']');return "http://"+(host.Contains(":")?"["+host+"]":host)+":"+ProxyPort;}}
    public void Validate(NativePaths paths){paths.Validate();if(Folder&&(String.IsNullOrWhiteSpace(paths.Workspace)||!Path.IsPathRooted(paths.Workspace)))throw new ArgumentException("请明确选择工作目录或工作盘。");if(Folder&&!Directory.Exists(Path.GetPathRoot(paths.Workspace)))throw new DirectoryNotFoundException("所选工作盘不可用。");if(Proxy&&(ProxyPort<1||ProxyPort>65535))throw new ArgumentException("代理端口须为 1–65535。");if(Proxy){var h=ProxyHost.Trim().Trim('[',']');if(String.IsNullOrEmpty(h)||Uri.CheckHostName(h)==UriHostNameType.Unknown||h.Contains("/"))throw new ArgumentException("代理 IP／主机名无效。");}foreach(var model in new[]{ParentModel,ChildModel})if(!Regex.IsMatch(model??"",@"^[A-Za-z0-9][A-Za-z0-9._/:+-]{0,127}$"))throw new ArgumentException("模型 ID 无效。");foreach(var effort in new[]{ParentEffort,ChildEffort})if(!new[]{"low","medium","high","xhigh","max","ultra"}.Contains(effort))throw new ArgumentException("思考强度无效。");}
}
public sealed class NativeStep {public string name,state,message;}
public sealed class NativeResult {
    public string state,session;
    public List<NativeStep> steps=new List<NativeStep>();
    public int ExitCode {get{return state=="complete"?0:state=="cancelled"?2:state=="partial"?3:1;}}
    public void Add(string name,string state,string message){steps.Add(new NativeStep{name=name,state=state,message=message});}
}
public sealed class RegistryValueState {public bool Exists;public string Kind;public object Value;}
public interface INativeRegistry {RegistryValueState Get(string name);void Set(string name,RegistryValueState value);RegistryValueState GetMachine(string name);}
public sealed class NativeCommandResult {public int ExitCode;public bool TimedOut;public string Output,Error;}
public interface INativeCommands {NativeCommandResult Run(string file,string[] arguments,Dictionary<string,string> environment,int timeoutMs);}
public interface INativeDownloads {string Text(string url,Func<string,bool> validate);void File(string url,string target,Action<string> validate);}
public sealed class OfficialDesktop {public string FullName,Version,Install,Source,StartupCode;}
public interface INativeDesktop {OfficialDesktop Discover();}
public sealed class ProcessIdentity {public int Id,ParentId,SessionId;public long Started;public string Name,Path,Owner;public string Key{get{return Id+":"+Started;}}}
public interface INativeProcesses {List<ProcessIdentity> List(NativePaths paths,OfficialDesktop desktop);void RequestExit(ProcessIdentity process,bool force);List<ProcessIdentity> Wait(NativePaths paths,OfficialDesktop desktop,int timeoutMs);}
public sealed class NativeServices {public INativeRegistry Registry;public INativeCommands Commands;public INativeDownloads Downloads;public INativeDesktop Desktop;public INativeProcesses Processes;public Action<string> Log=delegate{};}
}
