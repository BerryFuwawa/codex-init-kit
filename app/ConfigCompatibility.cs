using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CodexKit {
// A deliberately conservative editor: only standalone boolean assignments are
// removable. Ambiguous TOML is reported for manual review, never rewritten.
public static class ConfigCompatibility {
    public class Finding {
        public int Line;
        public string Key;
        public bool CanClean;
    }
    public class ConfigFile {
        public string Path, Error;
        public byte[] Original;
        public string[] Lines;
        public bool Bom;
        public List<Finding> Findings=new List<Finding>();
    }
    public class Report {
        public List<ConfigFile> Files=new List<ConfigFile>();
        public string BackupPath;
        public int Removed;
        public int Count { get { return Files.Sum(x=>x.Findings.Count); } }
        public bool HasErrors { get { return Files.Any(x=>!String.IsNullOrEmpty(x.Error)); } }
        public string Summary() {
            var text=new StringBuilder();
            text.AppendLine("已检查 "+Files.Count+" 个配置文件；发现 "+Count+" 项失效设置。");
            foreach(var file in Files) {
                text.AppendLine(file.Path);
                if(!String.IsNullOrEmpty(file.Error))text.AppendLine("  需手工检查："+file.Error);
                foreach(var item in file.Findings)text.AppendLine("  第 "+item.Line+" 行 · "+item.Key+" · "+(item.CanClean?"可定向清理":"复杂写法，需手工处理"));
            }
            if(Removed>0)text.AppendLine("已移除 "+Removed+" 项。备份："+BackupPath+"；恢复方法见 Restore.ps1。");
            if(Count==0&&!HasErrors&&Removed==0)text.AppendLine("文件中无需清理。若设置页面仍提示 session-flags，警告来自会话参数或其他配置层，不能据此报告全部修复。");
            text.AppendLine("code_mode_tool_description_first 若来自 session-flags，应检查 Codex 桌面版与 CLI 的版本兼容性；文件清理无法移除客户端传入的参数。");
            text.Append("guardianv2.thread_context 已被忽略；只移除该子项，保留 Guardian 其他设置。重新启动 Codex 后验证提示；本工具不会关闭 Codex 或切换 CLI。");
            return text.ToString();
        }
    }
    static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
    static string[] KeyParts(string text) {
        var parts=new List<string>();int i=0;
        while(i<text.Length) {
            while(i<text.Length&&Char.IsWhiteSpace(text[i]))i++;
            if(i==text.Length)return null;
            string part;
            if(text[i]=='\''||text[i]=='"') {
                char quote=text[i++];var decoded=new StringBuilder();
                while(i<text.Length&&text[i]!=quote){
                    char c=text[i++];
                    if(c=='\\'&&quote=='"'){
                        if(i==text.Length)return null;char escaped=text[i++];
                        if(escaped=='\\'||escaped=='"')decoded.Append(escaped);
                        else if(escaped=='b')decoded.Append('\b');else if(escaped=='t')decoded.Append('\t');else if(escaped=='n')decoded.Append('\n');else if(escaped=='f')decoded.Append('\f');else if(escaped=='r')decoded.Append('\r');
                        else if(escaped=='u'||escaped=='U'){
                            int length=escaped=='u'?4:8;if(i+length>text.Length)return null;
                            try{decoded.Append(Char.ConvertFromUtf32(Convert.ToInt32(text.Substring(i,length),16)));}catch{return null;}i+=length;
                        }else return null;
                    }else decoded.Append(c);
                }
                if(i==text.Length)return null;
                part=decoded.ToString();i++;
            } else {
                int start=i;while(i<text.Length&&((text[i]<128&&Char.IsLetterOrDigit(text[i]))||text[i]=='_'||text[i]=='-'))i++;
                if(i==start)return null;part=text.Substring(start,i-start);
            }
            parts.Add(part);while(i<text.Length&&Char.IsWhiteSpace(text[i]))i++;
            if(i==text.Length)break;if(text[i++]!='.')return null;
            if(i==text.Length)return null;
        }
        return parts.ToArray();
    }
    static int EqualSign(string line) {
        char quote='\0';bool escape=false;
        for(int i=0;i<line.Length;i++) {
            char c=line[i];
            if(quote!='\0'){if(escape){escape=false;continue;}if(c=='\\'&&quote=='"'){escape=true;continue;}if(c==quote)quote='\0';continue;}
            if(c=='\''||c=='"'){quote=c;continue;}if(c=='#')break;if(c=='=')return i;
        }
        return -1;
    }
    static string[] TableParts(string line) {
        if(line.StartsWith("[["))return null;
        char quote='\0';bool escape=false;
        for(int i=1;i<line.Length;i++){
            char c=line[i];
            if(quote!='\0'){if(escape){escape=false;continue;}if(c=='\\'&&quote=='"'){escape=true;continue;}if(c==quote)quote='\0';continue;}
            if(c=='\''||c=='"'){quote=c;continue;}
            if(c==']'){
                var tail=line.Substring(i+1).Trim();if(tail.Length>0&&!tail.StartsWith("#"))return null;
                return KeyParts(line.Substring(1,i-1));
            }
        }
        return null;
    }
    static string Target(string[] path) {
        if(path.Length>=2&&path[0]=="profiles")path=path.Skip(2).ToArray();
        if(path.SequenceEqual(new[]{"features","code_mode_tool_description_first"}))return "features.code_mode_tool_description_first";
        if(path.SequenceEqual(new[]{"features","guardianv2","thread_context"}))return "features.guardianv2.thread_context";
        return null;
    }
    static bool Advance(string line,ref char multi,ref int depth) {
        char quote='\0';bool escape=false;
        for(int i=0;i<line.Length;i++) {
            char c=line[i];bool triple=i+2<line.Length&&line[i+1]==c&&line[i+2]==c;
            if(multi!='\0') {
                if(c=='\\'&&multi=='"'){i++;continue;}
                if(c==multi&&triple){multi='\0';i+=2;}continue;
            }
            if(quote!='\0') {if(escape){escape=false;continue;}if(c=='\\'&&quote=='"'){escape=true;continue;}if(c==quote)quote='\0';continue;}
            if(c=='#')break;
            if(c=='\''||c=='"'){if(triple){multi=c;i+=2;}else quote=c;continue;}
            if(c=='['||c=='{')depth++;if(c==']'||c=='}')depth--;
            if(depth<0)return false;
        }
        return quote=='\0';
    }
    public static ConfigFile Inspect(string path) {
        var file=new ConfigFile{Path=System.IO.Path.GetFullPath(path)};
        try {
            if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("链接文件不自动修改。");
            for(var parent=Directory.GetParent(file.Path);parent!=null;parent=parent.Parent)if((parent.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("配置位于链接目录下，请直接选择实际目录。");
            file.Original=File.ReadAllBytes(path);file.Bom=file.Original.Length>=3&&file.Original[0]==239&&file.Original[1]==187&&file.Original[2]==191;
            var text=Utf8.GetString(file.Original,file.Bom?3:0,file.Original.Length-(file.Bom?3:0));
            file.Lines=Regex.Matches(text,"[^\r\n]*(?:\r\n|\n|\r|$)").Cast<Match>().Where(x=>x.Length>0).Select(x=>x.Value).ToArray();
            string[] section=new string[0];char multi='\0';int depth=0;bool safe=true;
            for(int i=0;i<file.Lines.Length;i++) {
                var line=file.Lines[i].TrimEnd('\r','\n');var trimmed=line.Trim();
                if(multi=='\0'&&depth==0) {
                    if(trimmed.Length==0||trimmed.StartsWith("#"))continue;
                    if(trimmed.StartsWith("[")) {
                        var parsed=TableParts(trimmed);
                        if(parsed==null){safe=false;section=new string[0];}else section=parsed;
                        continue;
                    }
                    int equals=EqualSign(line);
                    if(equals<0){safe=false;continue;}
                    var key=KeyParts(line.Substring(0,equals));
                    if(key==null){safe=false;continue;}
                    var full=section.Concat(key).ToArray();var target=Target(full);var value=line.Substring(equals+1);
                    if(target!=null)file.Findings.Add(new Finding{Line=i+1,Key=target,CanClean=Regex.IsMatch(value,"^\\s*(?:true|false)\\s*(?:#.*)?$")});
                    // Inline tables containing an obsolete key require manual editing.
                    var normalized=full.Length>=2&&full[0]=="profiles"?full.Skip(2).ToArray():full;
                    if(value.TrimStart().StartsWith("{")&&((normalized.SequenceEqual(new[]{"features","guardianv2"})&&Regex.IsMatch(value,"(?:^|[,{\\s])(?:thread_context|[\"']thread_context[\"'])\\s*="))||(normalized.SequenceEqual(new[]{"features"})&&Regex.IsMatch(value,"code_mode_tool_description_first|thread_context"))))
                        file.Findings.Add(new Finding{Line=i+1,Key=String.Join(".",normalized)+" (inline table)",CanClean=false});
                    if(!Advance(value,ref multi,ref depth))safe=false;
                } else if(!Advance(line,ref multi,ref depth))safe=false;
            }
            if(multi!='\0'||depth!=0)safe=false;
            if(!safe){file.Error="存在不支持或不完整的 TOML 写法；为保护原文件，不自动修改。";foreach(var finding in file.Findings)finding.CanClean=false;}
        } catch(Exception ex){file.Error="无法可靠读取配置："+ex.Message;foreach(var finding in file.Findings)finding.CanClean=false;}
        return file;
    }
    public static Report Scan(string home,string project) {
        var paths=new List<string>();
        home=Path.GetFullPath(home);
        if(File.Exists(Path.Combine(home,"config.toml")))paths.Add(Path.Combine(home,"config.toml"));
        if(Directory.Exists(home))paths.AddRange(Directory.GetFiles(home,"*.config.toml",SearchOption.TopDirectoryOnly));
        if(!String.IsNullOrWhiteSpace(project)) {
            project=Path.GetFullPath(project);
            if(!Directory.Exists(project))throw new DirectoryNotFoundException("项目目录不存在。");
            var config=Path.Combine(project,".codex","config.toml");
            if(File.Exists(config))paths.Add(config);
        }
        var report=new Report();foreach(var path in paths.Distinct(StringComparer.OrdinalIgnoreCase))report.Files.Add(Inspect(path));return report;
    }
    static bool Same(byte[] a,byte[] b){return a.SequenceEqual(b);}
    static string PsQuote(string text){return "'"+text.Replace("'","''")+"'";}
    public static void Clean(Report report,string backupRoot) {
        var files=report.Files.Where(x=>String.IsNullOrEmpty(x.Error)&&x.Findings.Any(f=>f.CanClean)).ToArray();
        if(files.Length==0)return;
        report.BackupPath=Path.Combine(backupRoot,"ConfigCompatibility-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(report.BackupPath);
        var restore=new StringBuilder("# Restore exact original configuration files. Close Codex first.\r\n$ErrorActionPreference='Stop'\r\n");
        for(int i=0;i<files.Length;i++) {
            var file=files[i];if(!Same(File.ReadAllBytes(file.Path),file.Original))throw new IOException("配置已被其他程序修改，请重新检查。备份目录："+report.BackupPath);
            string backup=Path.Combine(report.BackupPath,i.ToString("D3")+"-"+Path.GetFileName(file.Path));
            File.WriteAllBytes(backup,file.Original);
            restore.AppendLine("Copy-Item -LiteralPath (Join-Path $PSScriptRoot "+PsQuote(Path.GetFileName(backup))+") -Destination "+PsQuote(file.Path)+" -Force");
        }
        File.WriteAllText(Path.Combine(report.BackupPath,"Restore.ps1"),restore.ToString(),new UTF8Encoding(true));
        try {
            foreach(var file in files) {
                var remove=new HashSet<int>(file.Findings.Where(x=>x.CanClean).Select(x=>x.Line));
                var cleaned=String.Concat(file.Lines.Where((x,i)=>!remove.Contains(i+1)));
                string temp=Path.Combine(Path.GetDirectoryName(file.Path),".codex-kit-"+Guid.NewGuid().ToString("N")+".tmp");
                try {
                    File.WriteAllText(temp,cleaned,new UTF8Encoding(file.Bom,true));
                    if(!Same(File.ReadAllBytes(file.Path),file.Original))throw new IOException("配置已被其他程序修改，请重新检查。");
                    File.Replace(temp,file.Path,null);
                    report.Removed+=remove.Count;
                } finally {if(File.Exists(temp))File.Delete(temp);}
            }
        } catch(Exception ex){throw new IOException("配置清理未全部完成；原文件备份及 Restore.ps1 位于 "+report.BackupPath+"。原因："+ex.Message,ex);}
    }
}
}
