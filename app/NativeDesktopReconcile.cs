using File=CodexKit.Native.NativeFile;
using Directory=CodexKit.Native.NativeDirectory;
using System;using System.IO;using System.Text;using System.Linq;using System.Globalization;using System.Collections.Generic;using System.Text.RegularExpressions;using CodexKit.Native;
namespace CodexKit {
// 保留原文，仅修改初始化工具负责的键；无法定位的写法在备份和修改前报告。
internal static class ManagedToml {
    sealed class Entry {internal int First,Last;internal string[] Path,Table;internal string Value;}
    sealed class Document {internal List<string> Lines;internal List<Entry> Entries=new List<Entry>();internal Dictionary<string,int> Ends=new Dictionary<string,int>();}
    sealed class ValueLocation {internal Entry Entry;internal int Offset;}
    static readonly string[] AgentsEnabledPath={"agents","enabled"};
    static readonly string[] LegacyMultiAgentPath={"features","multi_agent"};
    static readonly string[] V2Path={"features","multi_agent_v2"};
    static readonly string[] V2EnabledPath={"features","multi_agent_v2","enabled"};
    const string ConcurrencyKey="max_concurrent_threads_per_session";
    static readonly string[] AgentsConcurrencyPath={"agents",ConcurrencyKey};
    static readonly string[] LegacyConcurrencyPath={"agents","max_threads"};
    static readonly string[] V2ConcurrencyPath={"features","multi_agent_v2",ConcurrencyKey};
    static readonly string[] V2ModelOverridesPath={"features","multi_agent_v2","expose_spawn_agent_model_overrides"};
    static readonly Regex InlineTokens=new Regex(
        "\"\"\"(?:\\\\[\\s\\S]|\"(?!\"\")|[^\"\\\\])*\"{3,5}|'''[\\s\\S]*?'{3,5}|"+
        "\"(?:\\\\.|[^\"\\\\])*\"|'[^']*'|#[^\\r\\n]*|[{}\\[\\],=]");
    static readonly Regex BoolValue=new Regex(@"\G\s*(?<value>true|false)(?=\s*(?:[#},]|$))");
    static readonly Regex IntegerValue=new Regex(@"\G\s*(?<value>0x[0-9a-fA-F](?:_?[0-9a-fA-F])*|0o[0-7](?:_?[0-7])*|0b[01](?:_?[01])*|[+-]?(?:0|[1-9](?:_?[0-9])*))(?=\s*(?:[#},]|$))");
    static string Id(IEnumerable<string> path){return String.Join("\u001f",path);}
    static Document Read(string text){
        var doc=new Document{Lines=Regex.Matches(text,"[^\\r\\n]*(?:\\r\\n|\\n|\\r|$)").Cast<Match>().Where(x=>x.Length>0).Select(x=>x.Value).ToList()};
        string[] table=new string[0];string tableId="";doc.Ends[tableId]=doc.Lines.Count;char multi='\0';int depth=0;
        for(int i=0;i<doc.Lines.Count;i++){
            var line=doc.Lines[i].TrimEnd('\r','\n');var trimmed=line.Trim();if(trimmed.Length==0||trimmed.StartsWith("#"))continue;
            if(trimmed.StartsWith("[")){
                doc.Ends[tableId]=i;bool array=trimmed.StartsWith("[[");var header=array?trimmed.Substring(1,trimmed.LastIndexOf(']')-1):trimmed;
                table=ConfigCompatibility.TableParts(header);if(table==null)throw new InvalidDataException("配置表头无法识别，第 "+(i+1)+" 行；原文件未修改。");tableId=Id(table)+(array?"\u001f[]":"");doc.Ends[tableId]=doc.Lines.Count;continue;
            }
            int eq=ConfigCompatibility.EqualSign(line);var key=eq<0?null:ConfigCompatibility.KeyParts(line.Substring(0,eq));if(key==null)throw new InvalidDataException("配置键无法识别，第 "+(i+1)+" 行；原文件未修改。");
            int first=i;var value=new StringBuilder(line.Substring(eq+1));multi='\0';depth=0;
            if(!ConfigCompatibility.Advance(line.Substring(eq+1),ref multi,ref depth))throw new InvalidDataException("配置值无法识别，第 "+(i+1)+" 行。");
            while(multi!='\0'||depth>0){if(++i>=doc.Lines.Count)throw new InvalidDataException("配置包含未结束的多行值。");var next=doc.Lines[i].TrimEnd('\r','\n');value.Append('\n').Append(next);if(!ConfigCompatibility.Advance(next,ref multi,ref depth))throw new InvalidDataException("配置多行值无法识别。");}
            var path=table.Concat(key).ToArray();if(tableId.EndsWith("\u001f[]"))path=path.Concat(new[]{"[]"}).ToArray();
            if(doc.Entries.Any(x=>Id(x.Path)==Id(path))&&!tableId.EndsWith("\u001f[]"))throw new InvalidDataException("配置包含重复键，未执行修改。");
            doc.Entries.Add(new Entry{First=first,Last=i,Path=path,Table=table,Value=value.ToString()});
        }
        return doc;
    }
    internal static string SavedValue(string text,params string[] path){try{var entry=Read(text).Entries.FirstOrDefault(x=>x.Path.SequenceEqual(path));if(entry==null)return "未设置";var value=entry.Value.Trim();var m=Regex.Match(value,@"^(?:""(?<v>(?:\\.|[^""\\])*)""|'(?<v>[^']*)')\s*(?:#.*)?$");if(!m.Success)return "未能识别";return value.StartsWith("\"")?NativeData.Json.Deserialize<string>("\""+m.Groups["v"].Value+"\""):m.Groups["v"].Value;}catch(InvalidDataException){return "未能识别";}}
    static string Comparable(string value){var b=new StringBuilder();char quote='\0';bool escape=false;foreach(char c in value){if(quote!='\0'){b.Append(c);if(escape)escape=false;else if(c=='\\'&&quote=='"')escape=true;else if(c==quote)quote='\0';}else if(c=='#')break;else if(c=='"'||c=='\''){quote=c;b.Append(c);}else if(!Char.IsWhiteSpace(c))b.Append(c);}return b.ToString();}
    static int InlineMember(string value,int start,params string[] path){
        while(start>=0&&start<value.Length&&Char.IsWhiteSpace(value[start]))start++;
        if(start<0||start>=value.Length||value[start]!='{')return -1;
        int keyStart=start+1,depth=1;
        foreach(Match token in InlineTokens.Matches(value,keyStart)){
            switch(token.Value){
                case "{":case "[":depth++;break;
                case "}":case "]":if(--depth==0)return -1;break;
                case ",":if(depth==1)keyStart=token.Index+1;break;
                case "=":
                    if(depth==1){
                        var key=ConfigCompatibility.KeyParts(value.Substring(keyStart,token.Index-keyStart));
                        if(key!=null&&key.SequenceEqual(path))return token.Index+1;
                    }
                    break;
            }
        }
        return -1;
    }
    static ValueLocation FindValue(Document doc,string[] path){
        foreach(var entry in doc.Entries.Where(x=>path.Take(x.Path.Length).SequenceEqual(x.Path)).OrderByDescending(x=>x.Path.Length)){
            int offset=0,consumed=entry.Path.Length;
            while(consumed<path.Length){
                int member=-1;
                int count;
                for(count=path.Length-consumed;count>0;count--){
                    member=InlineMember(entry.Value,offset,path.Skip(consumed).Take(count).ToArray());
                    if(member>=0)break;
                }
                if(member<0)break;
                offset=member;
                consumed+=count;
            }
            if(consumed==path.Length)return new ValueLocation{Entry=entry,Offset=offset};
        }
        return null;
    }
    static Group Scalar(ValueLocation location,Regex pattern){
        return location==null?null:pattern.Match(location.Entry.Value,location.Offset).Groups["value"];
    }
    static long ReadPositiveInteger(ValueLocation location,string[] path){
        var error="子代理并发设置必须为正整数："+String.Join(".",path);
        var value=Scalar(location,IntegerValue);
        if(value!=null&&value.Success){
            var digits=value.Value.Replace("_","");
            try{
                long result;
                if(digits.StartsWith("0x"))result=Convert.ToInt64(digits.Substring(2),16);
                else if(digits.StartsWith("0o"))result=Convert.ToInt64(digits.Substring(2),8);
                else if(digits.StartsWith("0b"))result=Convert.ToInt64(digits.Substring(2),2);
                else result=Int64.Parse(digits,CultureInfo.InvariantCulture);
                if(result>0)return result;
            }catch(OverflowException ex){throw new InvalidDataException(error,ex);}
        }
        throw new InvalidDataException(error);
    }
    static long MaxConcurrentSubagents(Document doc){
        var v2=FindValue(doc,V2ConcurrencyPath);
        if(v2!=null)return ReadPositiveInteger(v2,V2ConcurrencyPath)-1;
        var configured=FindValue(doc,AgentsConcurrencyPath);
        if(configured!=null)return ReadPositiveInteger(configured,AgentsConcurrencyPath);
        var legacy=FindValue(doc,LegacyConcurrencyPath);
        return legacy==null?NativeOptions.DefaultMaxConcurrentSubagents:ReadPositiveInteger(legacy,LegacyConcurrencyPath);
    }
    static bool V2Enabled(Document doc){
        var flag=Scalar(FindValue(doc,V2EnabledPath)??FindValue(doc,V2Path),BoolValue);
        return flag!=null&&flag.Value=="true";
    }
    internal static bool V2Enabled(string text){return V2Enabled(Read(text));}
    internal static void ReadSubagentSettings(string text,out bool v2Enabled,out long maxConcurrentSubagents){
        var doc=Read(text);
        v2Enabled=V2Enabled(doc);
        maxConcurrentSubagents=MaxConcurrentSubagents(doc);
    }
    static void EditValue(Document doc,Entry entry,int offset,int length,string value){
        int line=entry.First+entry.Value.Take(offset).Count(c=>c=='\n');
        int column=line==entry.First?ConfigCompatibility.EqualSign(doc.Lines[line])+1+offset:offset-entry.Value.LastIndexOf('\n',offset-1)-1;
        doc.Lines[line]=doc.Lines[line].Remove(column,length).Insert(column,value);
        entry.Value=entry.Value.Remove(offset,length).Insert(offset,value);
    }
    static bool SetV2Member(Document doc,string[] path,Regex pattern,string value){
        var location=FindValue(doc,path);
        var scalar=Scalar(location,pattern);
        if(scalar!=null&&scalar.Success){
            if(scalar.Value!=value)EditValue(doc,location.Entry,scalar.Index,scalar.Length,value);
            return true;
        }
        if(location!=null)throw new InvalidDataException("无法识别子代理设置："+String.Join(".",path));
        var feature=FindValue(doc,V2Path);
        if(feature==null)return false;
        var flag=Scalar(feature,BoolValue);
        if(path.SequenceEqual(V2EnabledPath)&&flag.Success){
            if(flag.Value!=value)EditValue(doc,feature.Entry,flag.Index,flag.Length,value);
            return true;
        }
        var member=path.Last()+" = "+value;
        if(flag.Success){
            EditValue(doc,feature.Entry,flag.Index,flag.Length,"{ enabled = "+flag.Value+", "+member+" }");
            return true;
        }
        int brace=feature.Offset;
        while(brace<feature.Entry.Value.Length&&Char.IsWhiteSpace(feature.Entry.Value[brace]))brace++;
        if(brace==feature.Entry.Value.Length||feature.Entry.Value[brace]!='{')return false;
        bool empty=feature.Entry.Value.Substring(brace+1).TrimStart().StartsWith("}");
        EditValue(doc,feature.Entry,brace+1,0," "+member+(empty?" ":","));
        return true;
    }
    static void MergeSubagentSettings(Document doc,List<Entry> wanted,HashSet<string> owned){
        var agentsEnabled=wanted.FirstOrDefault(x=>x.Path.SequenceEqual(AgentsEnabledPath));
        if(agentsEnabled==null)return;
        bool enabled=Comparable(agentsEnabled.Value)=="true";
        owned.Add(Id(LegacyMultiAgentPath));
        var desiredV2=wanted.FirstOrDefault(x=>x.Path.SequenceEqual(V2EnabledPath));
        if(desiredV2!=null){
            if(SetV2Member(doc,V2EnabledPath,BoolValue,enabled?"true":"false"))wanted.Remove(desiredV2);
        }else if(V2Enabled(doc)){
            SetV2Member(doc,V2EnabledPath,BoolValue,"false");
        }
        owned.Remove(Id(V2EnabledPath));

        var concurrency=wanted.FirstOrDefault(x=>x.Path.SequenceEqual(AgentsConcurrencyPath));
        var dedicated=wanted.FirstOrDefault(x=>x.Path.SequenceEqual(V2ConcurrencyPath));
        if(concurrency!=null){
            owned.Add(Id(LegacyConcurrencyPath));
            if(FindValue(doc,V2ConcurrencyPath)!=null){
                long agents=Int64.Parse(concurrency.Value,CultureInfo.InvariantCulture);
                SetV2Member(doc,V2ConcurrencyPath,IntegerValue,(agents+1).ToString(CultureInfo.InvariantCulture));
            }
        }else if(dedicated!=null){
            owned.Remove(Id(AgentsConcurrencyPath));
            if(SetV2Member(doc,V2ConcurrencyPath,IntegerValue,Comparable(dedicated.Value)))wanted.Remove(dedicated);
        }
        if(enabled){
            var overrides=Scalar(FindValue(doc,V2ModelOverridesPath),BoolValue);
            if(overrides!=null&&overrides.Value=="false")SetV2Member(doc,V2ModelOverridesPath,BoolValue,"true");
        }
    }
    internal static string Merge(string current,string desired,string allOwned){
        var doc=Read(current);var wanted=Read(desired).Entries;var owned=new HashSet<string>(Read(allOwned).Entries.Select(x=>Id(x.Path)));var replacements=new Dictionary<int,string>();var removed=new HashSet<int>();var inserts=new Dictionary<int,List<string>>();var newTables=new Dictionary<string,List<string>>();string newline=current.Contains("\r\n")?"\r\n":"\n";
        MergeSubagentSettings(doc,wanted,owned);
        foreach(var entry in doc.Entries.Where(x=>owned.Contains(Id(x.Path)))){
            var target=wanted.SingleOrDefault(x=>Id(x.Path)==Id(entry.Path));if(target!=null&&Comparable(entry.Value)==Comparable(target.Value))continue;
            for(int n=entry.First;n<=entry.Last;n++)removed.Add(n);
            if(target!=null){var original=doc.Lines[entry.First];var eq=ConfigCompatibility.EqualSign(original);replacements[entry.First]=original.Substring(0,eq+1)+" "+target.Value.Trim()+newline;}
        }
        foreach(var entry in wanted.Where(x=>!doc.Entries.Any(e=>Id(e.Path)==Id(x.Path)))){
            if(doc.Entries.Any(e=>entry.Path.Length>e.Path.Length&&entry.Path.Take(e.Path.Length).SequenceEqual(e.Path)))throw new InvalidDataException("待修改配置使用内联表或冲突键："+String.Join(".",entry.Path)+"。请先整理该项；其他内容保留。");
            var table=entry.Table;var tableId=Id(table);
            if(!doc.Ends.ContainsKey(tableId)){var dotted=doc.Entries.FirstOrDefault(x=>x.Table.Length<table.Length&&x.Path.Length>table.Length&&x.Path.Take(table.Length).SequenceEqual(table));if(dotted!=null){table=dotted.Table;tableId=Id(table);}}
            int position;bool existing=doc.Ends.TryGetValue(tableId,out position);List<string> additions;
            if(existing){if(!inserts.TryGetValue(position,out additions))inserts[position]=additions=new List<string>();}
            else if(!newTables.TryGetValue(tableId,out additions))newTables[tableId]=additions=new List<string>{newline+"["+String.Join(".",table)+"]"+newline};
            additions.Add(String.Join(".",entry.Path.Skip(table.Length))+" = "+entry.Value.Trim()+newline);
        }
        var output=new StringBuilder();for(int i=0;i<=doc.Lines.Count;i++){List<string> added;if(inserts.TryGetValue(i,out added)){if(output.Length>0&&output[output.Length-1]!='\n')output.Append(newline);foreach(var part in added)output.Append(part);}if(i==doc.Lines.Count)break;string replace;if(replacements.TryGetValue(i,out replace))output.Append(replace);else if(!removed.Contains(i))output.Append(doc.Lines[i]);}foreach(var group in newTables.Values)foreach(var part in group)output.Append(part);return output.ToString();
    }
}
internal sealed class NativeDesktopReconcile {
    readonly NativePaths paths;readonly NativeServices services;readonly NativeOptions options;NativeSession session;readonly Dictionary<string,string> originals=new Dictionary<string,string>();
    internal readonly List<ConfigurationEffect> Changes=new List<ConfigurationEffect>();
    internal NativeDesktopReconcile(NativePaths p,NativeServices s,NativeOptions o){paths=p;services=s;options=o;}
    string Text(string path){return File.Exists(path)?NativeData.Read(path):"";}
    internal string Configuration(){var all=new NativeOptions{DesktopProfile=true,Folder=true,Subagents=true};return ManagedToml.Merge(Text(paths.Config),NativeInitializer.Config(paths,options),NativeInitializer.Config(paths,all));}
    internal static string EnvironmentText(string current,NativeOptions o){
        var lines=Regex.Matches(current,"[^\\r\\n]*(?:\\r\\n|\\n|\\r|$)").Cast<Match>().Where(x=>x.Length>0).Select(x=>x.Value).ToList();var managed=new[]{"HTTP_PROXY","HTTPS_PROXY","NO_PROXY"};var desired=NativeInitializer.ProxyText(o).Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).ToDictionary(x=>x.Split('=')[0],x=>x.Substring(x.IndexOf('=')+1),StringComparer.OrdinalIgnoreCase);var found=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var output=new StringBuilder();string nl=current.Contains("\r\n")?"\r\n":"\n";
        foreach(var line in lines){var m=Regex.Match(line,@"^\s*(?:export\s+)?(?<key>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*(?<value>[^\r\n]*)");var key=m.Groups["key"].Value;if(!m.Success||!managed.Contains(key,StringComparer.OrdinalIgnoreCase)){output.Append(line);continue;}if(!desired.ContainsKey(key)||!found.Add(key))continue;var old=m.Groups["value"].Value.Trim().Trim('"','\'');if(key.Equals("NO_PROXY",StringComparison.OrdinalIgnoreCase)){desired[key]=String.Join(",",old.Split(',').Select(x=>x.Trim()).Where(x=>x.Length>0).Concat(new[]{"localhost","127.0.0.1","::1"}).Distinct(StringComparer.OrdinalIgnoreCase));}output.Append(old==desired[key]?line:key+"="+desired[key]+nl);}
        foreach(var pair in desired.Where(x=>!found.Contains(x.Key))){if(output.Length>0&&output[output.Length-1]!='\n')output.Append(nl);output.Append(pair.Key).Append('=').Append(pair.Value).Append(nl);}return output.ToString();
    }
    internal static string RulesText(string current,string desired,NativePaths p){
        var text=NativeData.Normalize(current);var normalized=NativeData.Normalize(desired);if(text==normalized)return current;
        text=text.Replace(NativeData.Normalize(NativeData.Resource("Desktop.RtkRules.md")).TrimEnd(),"");
        text=Regex.Replace(text,@"(?m)^@"+Regex.Escape(p.RtkDoc)+@"\s*$","");
        foreach(var name in new[]{"INIT WORK","ANTI OVERDEFENSE","WORK JOURNAL","COMMAND LINE FIRST"})text=Regex.Replace(text,@"(?ms)^<!-- BEGIN CODEX "+name+@" RULES -->.*?^<!-- END CODEX "+name+@" RULES -->\s*","");
        foreach(var name in new[]{"FOLDER MANAGEMENT","LUNA"})text=Regex.Replace(text,@"(?ms)^BEGIN CODEX "+name+@" PROMPT V[^\r\n]*\r?\n.*?^END CODEX "+name+@" PROMPT V[^\r\n]*\s*","");
        var custom=text.Trim();var result=(custom.Length>0?custom+"\n\n":"")+normalized.TrimEnd()+"\n";return NativeData.Normalize(current)==result?current:result;
    }
    void Backup(){if(session==null)session=NativeSession.Create(paths,services.Registry,true,true);}
    bool Write(string path,string expected){string before;if(!originals.TryGetValue(path,out before))before=Text(path);if(Text(path)!=before)throw new IOException("配置在计划后发生变化，未覆盖："+Path.GetFileName(path));if(before==expected)return false;Backup();if(Text(path)!=before)throw new IOException("配置在检查后发生变化，尚未覆盖："+Path.GetFileName(path));var bytes=new UTF8Encoding(false).GetBytes(expected);if(File.Exists(path)){var old=File.ReadAllBytes(path);if(old.Length>=3&&old[0]==239&&old[1]==187&&old[2]==191)bytes=new byte[]{239,187,191}.Concat(bytes).ToArray();}NativeData.WriteBytes(path,bytes);ConfigurationEffect.Record(Changes,path==paths.Config?"config":path==paths.Env?"env":"agents",Path.GetFileName(path)+" 中本次选择对应的管理内容已更新；其他内容保留。");return true;}
    internal NativeResult Run(bool proxyOnly){
        var result=new NativeResult{state="failed"};if(!options.Confirm){result.state="cancelled";return result;}options.Validate(paths);
        // 先计算所有文本；解析失败不能留下半份新配置。
        foreach(var file in new[]{paths.Config,paths.Env,paths.Agents})originals[file]=Text(file);
        string config=proxyOnly?null:Configuration(),env=EnvironmentText(Text(paths.Env),options),rules=proxyOnly?null:RulesText(Text(paths.Agents),NativeInitializer.Rules(paths,options),paths);
        Action<string,Action> step=(name,body)=>{try{body();result.Add(name,"complete","检查与所需修改已完成。");}catch(Exception ex){result.Add(name,"failed",ex.Message);services.Log(name+"未完成："+ex.Message);}};
        step("配置与代理",()=>{bool changed=false;if(!proxyOnly)changed=Write(paths.Config,config);changed=Write(paths.Env,env)||changed;if(NativeUserProxy.Apply(services.Registry,options,Backup)){changed=true;ConfigurationEffect.Record(Changes,"env","已保存当前 Windows 用户代理与桌面 Node 代理开关；其他读取这些变量的程序也会采用该设置。关闭代理时移除用户代理和 Node 开关，保留直连排除项。");}services.Log(changed?"所选配置已准备；未管理配置、设备登记、凭据及连接数据保留。":"配置与代理一致，跳过写入；保留当前运行中的连接。");});
        if(!proxyOnly){
            step("RTK",()=>{var rtk=new NativeRtk(paths,services);var exe=rtk.Find();if(exe==null){Backup();exe=rtk.Install(session.root);ConfigurationEffect.Record(Changes,"rtk","已安装 RTK 组件。");}var version=services.Commands.Run(exe,new[]{"--version"},new Dictionary<string,string>{{"CODEX_HOME",paths.Home},{"RTK_TELEMETRY_DISABLED","1"}},15000);if(version.TimedOut||version.ExitCode!=0||!Regex.IsMatch(version.Output??"",@"(?im)^rtk(?:\.exe)?\s+\d+\.\d+"))throw new IOException("RTK 版本检查未通过，未执行集成。");bool disabled=Regex.IsMatch(Text(paths.RtkConfig),@"(?ms)^\s*\[telemetry\]\s*\r?\n(?:(?!^\s*\[).)*?^\s*enabled\s*=\s*false\b");if(!disabled){Backup();var telemetry=services.Commands.Run(exe,new[]{"telemetry","disable"},new Dictionary<string,string>{{"CODEX_HOME",paths.Home},{"RTK_TELEMETRY_DISABLED","1"}},15000);if(telemetry.TimedOut||telemetry.ExitCode!=0||!Regex.IsMatch(Text(paths.RtkConfig),@"(?ms)^\s*\[telemetry\]\s*\r?\n(?:(?!^\s*\[).)*?^\s*enabled\s*=\s*false\b"))throw new IOException("RTK 匿名统计关闭未通过检查。");ConfigurationEffect.Record(Changes,"rtk","已关闭匿名统计。");}var old=services.Registry.Get("Path");var path=Convert.ToString(old.Value)??"";if(String.Equals(exe,paths.Rtk,StringComparison.OrdinalIgnoreCase)&&!path.Split(';').Any(x=>String.Equals(x.TrimEnd('\\'),paths.UserTools.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))){Backup();services.Registry.Set("Path",new RegistryValueState{Exists=true,Kind=old.Exists?old.Kind:"ExpandString",Value=path.Length==0?paths.UserTools:path+";"+paths.UserTools});ConfigurationEffect.Record(Changes,"rtk","用户 PATH 已加入 RTK 目录。");}services.Log("RTK 可用；规则直接内嵌到 AGENTS.md，不重复调用 RTK 初始化或改写现有钩子。");});
            step("全局规则",()=>services.Log(Write(paths.Agents,rules)?"规则已按选择更新；保留未管理的自定义内容。":"全局规则一致，跳过写入。"));
            if(options.Folder)step("文件夹管理",()=>{foreach(var folder in new[]{"Conversations","Projects","OUT","Temp","Shared","Archive","Config","Docs"})if(!Directory.Exists(Path.Combine(paths.Workspace,folder))){Directory.CreateDirectory(Path.Combine(paths.Workspace,folder));ConfigurationEffect.Record(Changes,"folders",folder);}});
            if(options.Cua)step("CUA",()=>{var cua=new NativeCua(paths,services);var check=cua.Run(false,paths.BackupRoot);if(check.Passed){services.Log("CUA 现有组件完整，跳过修复，无需退出 Codex。");return;}if(services.Processes.List(paths,services.Desktop.Discover()).Count>0)throw new IOException("CUA 需要替换运行时文件；当前 Codex 正在运行，此项暂缓。保存其他对话后可单独重试并确认关闭。");Backup();if(!cua.Run(true,Path.Combine(session.root,"CUA")).Passed)throw new IOException("CUA 修复后检查未通过。");ConfigurationEffect.Record(Changes,"cua","已补齐当前桌面版本的 CUA 组件并检查文件完整性。");});
        }
        result.session=session==null?null:session.root;result.state=result.steps.Any(x=>x.state=="failed")?"partial":"complete";if(session!=null){NativeData.Write(Path.Combine(session.root,"Native_Result.json"),NativeData.Json.Serialize(result));NativeData.Write(Path.Combine(session.root,"Reset_Result.txt"),String.Join("\n",result.steps.Select(x=>x.name+"："+x.message)));}return result;
    }
}
}
