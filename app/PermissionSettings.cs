using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CodexKit {
// Keep the original TOML statements, including quoted paths and multi-line values.
public sealed class PermissionSettings {
    static readonly HashSet<string> Roots=new HashSet<string>(new[]{"approval_policy","approvals_reviewer","sandbox_mode","sandbox_workspace_write","default_permissions","permissions"},StringComparer.Ordinal);
    sealed class Statement {
        internal string Header, Text;
        internal string[] Path;
    }
    readonly List<Statement> selected=new List<Statement>();
    public string SourcePath {get;private set;}
    public string Summary {get;private set;}
    public string Payload {get{return Render();}}

    public static PermissionSettings Load(string home) {
        var path=System.IO.Path.Combine(home,"config.toml");string text;
        try {text=File.ReadAllText(path,new UTF8Encoding(false,true));}
        catch(FileNotFoundException){text="";}
        catch(DirectoryNotFoundException){text="";}
        var result=Parse(text);result.SourcePath=path;return result;
    }
    static bool Inherited(string[] path){return path.Length>0&&Roots.Contains(path[0]);}
    static string Identity(string[] path){return String.Join("\0",path);}
    static bool HasValueParent(HashSet<string> keys,string[] path) {
        for(int count=1;count<=path.Length;count++)if(keys.Contains(Identity(path.Take(count).ToArray())))return true;
        return false;
    }
    static List<Statement> Read(string text) {
        var result=new List<Statement>();var lines=Regex.Split(text,"\r\n|\n|\r");var section=new string[0];string header="";
        var keys=new HashSet<string>(StringComparer.Ordinal);var tables=new HashSet<string>(StringComparer.Ordinal);bool arrayTable=false;
        for(int index=0;index<lines.Length;index++) {
            var line=lines[index];var trimmed=line.Trim();
            if(trimmed.Length==0||trimmed.StartsWith("#"))continue;
            if(trimmed.StartsWith("[")) {
                arrayTable=trimmed.StartsWith("[[");
                var candidate=trimmed;
                if(arrayTable){var end=candidate.IndexOf("]]",StringComparison.Ordinal);if(end<0)throw Invalid(index);candidate="["+candidate.Substring(2,end-2)+"]"+candidate.Substring(end+2);}
                section=ConfigCompatibility.TableParts(candidate);if(section==null)throw Invalid(index);
                if(HasValueParent(keys,section))throw Invalid(index);
                if(!arrayTable&&!tables.Add(Identity(section)))throw Invalid(index);
                if(arrayTable){keys.RemoveWhere(k=>k.StartsWith(Identity(section)+"\0",StringComparison.Ordinal));tables.RemoveWhere(k=>k.StartsWith(Identity(section)+"\0",StringComparison.Ordinal));}
                header=line;result.Add(new Statement{Header=header,Path=section,Text=""});continue;
            }
            int equals=ConfigCompatibility.EqualSign(line);if(equals<0)throw Invalid(index);
            var key=ConfigCompatibility.KeyParts(line.Substring(0,equals));if(key==null)throw Invalid(index);
            var path=section.Concat(key).ToArray();var identity=Identity(path);
            if(HasValueParent(keys,path)||tables.Any(t=>t==identity||t.StartsWith(identity+"\0",StringComparison.Ordinal))||keys.Any(k=>k.StartsWith(identity+"\0",StringComparison.Ordinal)))throw Invalid(index);
            keys.Add(identity);
            var raw=new StringBuilder(line);var value=new StringBuilder(line.Substring(equals+1));char multi='\0';int depth=0;
            if(!ConfigCompatibility.Advance(value.ToString(),ref multi,ref depth))throw Invalid(index);
            while(multi!='\0'||depth>0){
                if(++index>=lines.Length)throw Invalid(index);
                raw.Append('\n').Append(lines[index]);value.Append('\n').Append(lines[index]);
                if(!ConfigCompatibility.Advance(lines[index],ref multi,ref depth))throw Invalid(index);
            }
            var reader=new ValueReader(value.ToString());reader.Value();reader.Space();if(!reader.End)throw Invalid(index);
            result.Add(new Statement{Header=header,Path=path,Text=raw.ToString()});
        }
        return result;
    }
    static InvalidDataException Invalid(int index){return new InvalidDataException("权限继承无法解析 config.toml，第 "+(index+1)+" 行附近存在无效或不完整的 TOML。请修正后重试。");}
    public static PermissionSettings Parse(string text) {
        var result=new PermissionSettings();var all=Read(text);result.selected.AddRange(all.Where(s=>Inherited(s.Path)));
        var labels=new List<string>();
        foreach(var item in all.Where(s=>s.Header.Length==0&&s.Text.Length>0&&s.Path.Length==1&&Inherited(s.Path))){
            var value=item.Text.Substring(ConfigCompatibility.EqualSign(item.Text)+1).Trim();
            string label;
            switch(item.Path[0]){case "approval_policy":label="审批策略";break;case "approvals_reviewer":label="审批审核";break;case "sandbox_mode":label="沙箱模式";break;case "default_permissions":label="权限配置";break;default:continue;}
            labels.Add(label+"："+(value.StartsWith("{")?"granular／自定义":value.Split('\n')[0].Trim()));
        }
        result.Summary=result.selected.Count==0?"未设置全局权限，将使用 Codex 默认行为。":(labels.Count==0?"继承已有文件与网络权限。":String.Join("；",labels))+" 文件与网络权限按原配置保留。";
        return result;
    }
    string Render() {
        var text=new StringBuilder();string current="";
        foreach(var item in selected){
            if(item.Header!=current){text.AppendLine(item.Header);current=item.Header;}
            if(item.Text.Length>0)text.AppendLine(item.Text);
        }
        return text.ToString().Replace("\r\n","\n");
    }
    public string Apply(string generated) {
        // Generated defaults have no permission keys. Remove any existing copy so
        // repeated application replaces the selected settings instead of duplicating them.
        if(selected.Count==0)return generated;
        var retained=Read(generated).Where(s=>!Inherited(s.Path)).ToArray();
        var roots=selected.Where(s=>s.Header.Length==0&&s.Text.Length>0).Select(s=>s.Text);
        var tables=selected.Where(s=>s.Header.Length>0).ToArray();
        var text=new StringBuilder();foreach(var root in roots)text.AppendLine(root);
        string current="";
        foreach(var item in retained.Concat(tables)){
            if(item.Header!=current){text.AppendLine(item.Header);current=item.Header;}
            if(item.Text.Length>0)text.AppendLine(item.Text);
        }
        return text.ToString().Replace("\r\n","\n");
    }
    sealed class ValueReader {
        readonly string text;int index;
        internal ValueReader(string value){text=value;}
        internal bool End{get{return index>=text.Length;}}
        internal void Space(){while(!End){if(Char.IsWhiteSpace(text[index])){index++;continue;}if(text[index]=='#'){while(!End&&text[index]!='\n')index++;continue;}break;}}
        void Fail(){throw new InvalidDataException("权限继承遇到无效的 TOML 值，请检查 config.toml。");}
        bool Take(char value){if(!End&&text[index]==value){index++;return true;}return false;}
        void String() {
            char quote=text[index++];bool multi=index+1<text.Length&&text[index]==quote&&text[index+1]==quote;
            if(multi)index+=2;
            while(!End){
                char c=text[index++];
                if(c==quote){
                    if(!multi)return;
                    if(index+1<text.Length&&text[index]==quote&&text[index+1]==quote){index+=2;while(!End&&text[index]==quote)index++;return;}
                }
                if(!multi&&(c=='\r'||c=='\n'))Fail();
                if(c=='\\'&&quote=='"'){
                    if(End)Fail();char escape=text[index++];
                    if(multi&&Char.IsWhiteSpace(escape)){while(!End&&Char.IsWhiteSpace(text[index]))index++;continue;}
                    if("btnfr\"\\".IndexOf(escape)>=0)continue;
                    int digits=escape=='u'?4:escape=='U'?8:0;if(digits==0||index+digits>text.Length)Fail();
                    for(int i=0;i<digits;i++)if(!Uri.IsHexDigit(text[index++]))Fail();
                }
            }
            Fail();
        }
        internal void Value() {
            Space();if(End)Fail();char c=text[index];
            if(c=='\''||c=='"'){String();return;}
            if(Take('[')){
                Space();if(Take(']'))return;
                while(true){Value();Space();if(Take(']'))return;if(!Take(','))Fail();Space();if(Take(']'))return;}
            }
            if(Take('{')){
                var keys=new HashSet<string>(StringComparer.Ordinal);Space();if(Take('}'))return;
                while(true){
                    int start=index;char quote='\0';bool escaped=false;
                    while(!End){c=text[index];if(quote!='\0'){index++;if(escaped){escaped=false;continue;}if(c=='\\'&&quote=='"'){escaped=true;continue;}if(c==quote)quote='\0';continue;}if(c=='\''||c=='"'){quote=c;index++;continue;}if(c=='=')break;if(c=='}'||c==',')Fail();index++;}
                    var key=ConfigCompatibility.KeyParts(text.Substring(start,index-start));if(key==null||!keys.Add(Identity(key))||!Take('='))Fail();
                    Value();Space();if(Take('}'))return;if(!Take(','))Fail();Space();
                }
            }
            int begin=index;while(!End&&!Char.IsWhiteSpace(text[index])&&",]}#".IndexOf(text[index])<0)index++;
            var token=text.Substring(begin,index-begin);
            if(token=="true"||token=="false"||Regex.IsMatch(token,@"^(?:[+-]?(?:inf|nan|(?:0|[1-9][0-9]*(?:_[0-9]+)*)(?:\.[0-9]+(?:_[0-9]+)*)?(?:[eE][+-]?[0-9]+(?:_[0-9]+)*)?)|0x[0-9A-Fa-f]+(?:_[0-9A-Fa-f]+)*|0o[0-7]+(?:_[0-7]+)*|0b[01]+(?:_[01]+)*)$")||Regex.IsMatch(token,@"^(?:\d{4}-\d\d-\d\d(?:[Tt]\d\d:\d\d:\d\d(?:\.\d+)?(?:[Zz]|[+-]\d\d:\d\d)?)?|\d\d:\d\d:\d\d(?:\.\d+)?)$"))return;
            Fail();
        }
    }
}
}
