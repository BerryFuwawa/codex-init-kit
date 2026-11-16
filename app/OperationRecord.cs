using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text;
namespace CodexKit {
internal sealed class InitializationRequest {
    internal readonly string Drive;internal readonly int Port;internal readonly bool Guard;
    readonly Dictionary<string,object> options;
    internal InitializationRequest(string drive,int port,bool guard,Dictionary<string,object> values){Drive=drive;Port=port;Guard=guard;options=Core.ReadJson(Core.Json.Serialize(values));}
    internal Dictionary<string,object> Options(){return Core.ReadJson(Core.Json.Serialize(options));}
}
internal sealed class OperationRecord {
    public string id,title,version,started,finished,state,rawPath,summary;
    public object[] steps;public ConfigurationEffect[] effects;
    internal string Raw,Chinese;
    internal LogReadingPosition Reading=new LogReadingPosition();internal bool ShowRaw;
    internal static string BuildVersion{get{return System.Diagnostics.FileVersionInfo.GetVersionInfo(typeof(OperationRecord).Assembly.Location).FileVersion;}}
    internal string Label{get{return DateTime.Parse(started).ToLocalTime().ToString("MM-dd HH:mm:ss")+" · "+title+" · "+StateLabel;}}
    internal string StateLabel{get{return state=="running"?"执行中":state=="complete"?"已完成":state=="partial"?"部分完成":state=="failed"?"未完成":state=="cancelled"?"已取消":"历史记录";}}
    internal void Save(){Directory.CreateDirectory(Path.GetDirectoryName(rawPath));File.WriteAllText(Path.ChangeExtension(rawPath,"operation.json"),Core.Json.Serialize(this),new UTF8Encoding(false));}
    internal static OperationRecord Create(string id,string title,string path){return new OperationRecord{id=id,title=title,rawPath=path,version=BuildVersion,started=DateTime.Now.ToString("o"),state="running",summary=""};}
    internal static List<OperationRecord> History(string directory){var list=new List<OperationRecord>();if(!Directory.Exists(directory))return list;foreach(var path in Directory.GetFiles(directory,"*.log").Where(x=>!x.EndsWith(".zh-CN.log",StringComparison.OrdinalIgnoreCase))){OperationRecord r=null;var meta=Path.ChangeExtension(path,"operation.json");if(File.Exists(meta))try{r=Core.Json.Deserialize<OperationRecord>(File.ReadAllText(meta));}catch(ArgumentException){}catch(InvalidOperationException){}if(r==null)r=new OperationRecord{id=Path.GetFileNameWithoutExtension(path),title="历史日志 · "+Path.GetFileName(path),started=File.GetLastWriteTime(path).ToString("o"),state="history",version="未记录"};r.rawPath=path;DateTime time;if(!DateTime.TryParse(r.started,out time))r.started=File.GetLastWriteTime(path).ToString("o");if(r.state=="running")r.state="history";list.Add(r);}return list.OrderByDescending(x=>DateTime.Parse(x.started)).ToList();}
    internal void LoadText(){if(Raw==null)Raw=Read(rawPath);if(Chinese==null){var translated=DesktopLocalization.ChineseLogPath(rawPath);Chinese=File.Exists(translated)?Read(translated):DesktopLocalization.TranslateText(Raw);}}
    static string Read(string path){if(!File.Exists(path))return "尚无日志";using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream,Encoding.UTF8))return reader.ReadToEnd();}
    public override string ToString(){return Label;}
}
}
