using System;
using System.IO;
using System.Linq;
using CodexKit.Native;

namespace CodexKit {
internal sealed class RelayPreference {
    public bool selected { get; set; }
    public bool installed { get; set; }

    static string PathFor(string root){return Path.Combine(root,"remote-relay.json");}

    internal static void Save(string root,bool selected,bool installed){
        var value=new RelayPreference{selected=selected,installed=installed};
        NativeData.Write(PathFor(root),Core.Json.Serialize(value));
    }

    internal static bool Load(string root,bool installed){
        string path=PathFor(root);
        if(File.Exists(path)){
            var saved=Core.Json.Deserialize<RelayPreference>(File.ReadAllText(path));
            if(saved==null)throw new InvalidDataException("远控转发设置为空。");
            // An install or removal outside this window takes precedence over its old selection.
            return installed==saved.installed ? saved.selected : installed;
        }
        if(installed)return true;
        var history=OperationRecord.History(Path.Combine(root,"Logs"));
        return !history.Any(op=>(op.title=="初始化" && (op.state=="complete" || op.state=="partial")) ||
            (op.state=="complete" && (op.title=="开启远控转发" || op.title=="关闭远控转发" || op.title=="卸载远控转发服务")));
    }
}
}
