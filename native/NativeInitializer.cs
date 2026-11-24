using File=CodexKit.Native.NativeFile;
using Directory=CodexKit.Native.NativeDirectory;
using FileInfo=CodexKit.Native.NativeFileInfo;
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Globalization;using System.Text;using System.Threading;
namespace CodexKit.Native {
public sealed class NativeInitializer {
    readonly NativePaths paths;readonly NativeServices services;
    public NativeInitializer(NativePaths p,NativeServices s){paths=p;services=s;}
    OfficialDesktop DesktopOptional(){try{return services.Desktop.Discover();}catch(FileNotFoundException){return null;}}
    public object Status(){paths.Validate();var app=DesktopOptional();return new{version=NativeData.Version,home=paths.Home,config=File.Exists(paths.Config),proxy=File.Exists(paths.Env)?NativeData.Read(paths.Env):null,agents=File.Exists(paths.Agents),rtk=new NativeRtk(paths,services).Find(),desktop=app==null?null:app.FullName,processes=services.Processes.List(paths,app),userCli=services.Registry.Get("CODEX_CLI_PATH"),userHost=services.Registry.Get("CODEX_CODE_MODE_HOST_PATH"),machineCli=services.Registry.GetMachine("CODEX_CLI_PATH"),machineHost=services.Registry.GetMachine("CODEX_CODE_MODE_HOST_PATH")};}
    public object Preflight(){paths.Validate();var app=DesktopOptional();return new{home=paths.Home,desktop=app==null?null:app.FullName,processes=services.Processes.List(paths,app)};}
    public static string Config(NativePaths p,NativeOptions o){
        var lines=new List<string>{
            "# Codex initialization defaults",
            "model = "+NativeData.Json.Serialize(o.ParentModel),
            "model_reasoning_effort = "+NativeData.Json.Serialize(o.ParentEffort),
            "","[desktop]","enabled-reasoning-efforts = [\"low\", \"medium\", \"high\", \"xhigh\", \"max\", \"ultra\"]"
        };
        if(o.Folder)lines.Add("projectlessWorkspaceRoot = "+NativeData.Json.Serialize(Path.Combine(p.Workspace,"Conversations")));
        lines.AddRange(new[]{"","[features]","context_management.experimental_mode = true"});
        if(o.DesktopProfile)lines.Add("memories = "+o.Memories.ToString().ToLowerInvariant());
        var concurrency=o.DesktopProfile?o.MaxConcurrentSubagents:NativeOptions.DefaultMaxConcurrentSubagents;
        if(o.Subagents||concurrency==0){
            lines.AddRange(new[]{"","[features.multi_agent_v2]","enabled = "+o.Subagents.ToString().ToLowerInvariant()});
            if(concurrency==0)lines.Add("max_concurrent_threads_per_session = 1");
        }
        lines.AddRange(new[]{"","[agents]","enabled = "+o.Subagents.ToString().ToLowerInvariant()});
        if((o.Subagents||o.DesktopProfile)&&concurrency>0)lines.Add("max_concurrent_threads_per_session = "+concurrency.ToString(CultureInfo.InvariantCulture));
        if(o.Subagents)lines.Add("max_depth = 1");
        if(o.DesktopProfile){
            if(!o.ChildAutoAssign)lines.AddRange(new[]{"default_subagent_model = "+NativeData.Json.Serialize(o.ChildModel),"default_subagent_reasoning_effort = "+NativeData.Json.Serialize(o.ChildEffort)});
            lines.AddRange(new[]{"","[memories]","generate_memories = "+o.Memories.ToString().ToLowerInvariant(),"use_memories = "+o.Memories.ToString().ToLowerInvariant()});
        }
        return String.Join("\n",lines)+"\n";
    }
    public static string Rules(NativePaths p,NativeOptions o){if(o.DesktopProfile)return DesktopRules(p,o);var baseRules=NativeData.Normalize(NativeData.Resource("BaseRules.md")).Replace("持续到请求的结果真正完真正完成。","持续到请求的结果真正完成。").TrimEnd();var result="@"+p.RtkDoc+"\n\n"+baseRules+"\n";if(o.Folder){var folder=NativeData.Normalize(NativeData.Resource("FolderRules.md")).Trim();var original="<SELECTED_DRIVE>:\\Codex";if(!folder.Contains(original))throw new InvalidDataException("目录规则模板无法识别。");result+="\n"+folder.Replace(original,p.Workspace.TrimEnd('\\','/'))+"\n";}if(o.Subagents)result+="\n"+NativeData.Normalize(NativeData.Resource("SubagentRules.md")).Trim().Replace("model = gpt-6.1-sol","model = "+o.ChildModel).Replace("model_reasoning_effort = high","model_reasoning_effort = "+o.ChildEffort)+"\n";return result;}
    static string DesktopRules(NativePaths p,NativeOptions o){
        var rules=NativeData.Normalize(NativeData.Resource("BaseRules.md")).Replace("持续到请求的结果真正完真正完成。","持续到请求的结果真正完成。");if(!o.Subagents)rules=System.Text.RegularExpressions.Regex.Replace(rules,@"(?ms)^SUBAGENTS\n.*?(?=^<!-- END CODEX INIT WORK RULES -->)","");
        var headings=new Dictionary<string,string>{{"GOAL","目标"},{"AUTONOMY","自主推进"},{"PRIORITY","指令优先级"},{"STYLE","表达风格"},{"DONE","完成条件"},{"APPROVAL","授权"},{"VERIFY","验证"},{"SUBAGENTS","子代理"}};foreach(var pair in headings)rules=System.Text.RegularExpressions.Regex.Replace(rules,@"(?m)^"+pair.Key+"$",pair.Value);
        rules=NativeData.Resource("Desktop.RtkRules.md").TrimEnd()+"\n\n"+rules.TrimEnd();foreach(var section in new[]{new{Enabled=o.AntiOverDefense,Name="AntiOverDefense.md"},new{Enabled=o.WorkJournal,Name="WorkJournal.md"},new{Enabled=o.CommandLineFirst,Name="CommandLineFirst.md"},new{Enabled=o.OpenSourceReference,Name="OpenSourceReference.md"}})if(section.Enabled)rules+="\n\n"+NativeData.Resource("Desktop."+section.Name).Trim();
        if(o.Folder)rules+="\n\n"+NativeData.Resource("Desktop.FolderRules.md").Trim().Replace("<SELECTED_DRIVE>:\\Codex",p.Workspace.TrimEnd('\\','/'));if(o.Subagents)rules+="\n\n"+DesktopSubagentRules(o);return NativeData.Normalize(rules)+"\n";
    }
    static string DesktopSubagentRules(NativeOptions options){
        var rules=NativeData.Normalize(NativeData.Resource("Desktop.SubagentRules.md")).Trim()
            .Replace("{{MAX_CONCURRENT_SUBAGENTS}}",options.MaxConcurrentSubagents.ToString(CultureInfo.InvariantCulture));
        if(!options.ChildAutoAssign)return rules.Replace("model = gpt-6.1-sol","model = "+options.ChildModel).Replace("model_reasoning_effort = high","model_reasoning_effort = "+options.ChildEffort);
        var start=rules.IndexOf("## 子代理模型",StringComparison.Ordinal);
        var end=rules.IndexOf("## 委派深度",StringComparison.Ordinal);
        const string automatic = "## 自动指派\n\n"
            + "每次委派前，参考当前工具列出的模型能力说明和支持的思考强度，按子任务需要选择模型与档位。调用 spawn_agent 时显式传入 model 和 reasoning_effort，即使选择与父模型相同，也写明这两个参数。\n\n"
            + "如果工具要求使用独立上下文才能指定档位，传入 fork_turns=\"none\"，并向子代理交代任务目标、所需路径或材料，以及完成标准。使用其他委派工具时，填写它支持的对应参数。\n\n";
        return rules.Substring(0,start)+automatic+rules.Substring(end);
    }
    public static string ProxyText(NativeOptions o){return o.Proxy?"HTTP_PROXY="+o.ProxyUrl+"\nHTTPS_PROXY="+o.ProxyUrl+"\nNO_PROXY=localhost,127.0.0.1,::1\n":"NO_PROXY=localhost,127.0.0.1,::1\n";}
    void EnsureNoRunning(OfficialDesktop app){if(services.Processes.List(paths,app).Count>0)throw new InvalidOperationException("修改前应用重新启动或仍在运行，已停止。");}
    public NativeResult Run(string action,NativeOptions options,string sessionRoot){
        var result=new NativeResult{state="failed"};if(!options.Confirm){result.state="cancelled";result.Add("确认","cancelled","未确认，不修改配置。");return result;}
        if(action!="init"){options.Folder=false;options.Subagents=false;options.Cua=false;}
        if(action=="rollback")paths.Validate();else options.Validate(paths);bool initialized=false;NativeSession session=null;OfficialDesktop app=null;
        string mutexName="Local\\CodexInitNative-"+System.Security.Principal.WindowsIdentity.GetCurrent().User.Value;
        using(var mutex=new Mutex(false,mutexName)){bool locked=false;try{
            try{locked=mutex.WaitOne(0);}catch(AbandonedMutexException){locked=true;}if(!locked)throw new InvalidOperationException("另一个原生初始化操作正在运行。");
            app=DesktopOptional();NativeProcessWorkflow.EnsureStopped(paths,app,services.Processes,options.AllowClose,options.ApprovedProcesses,services.Log);EnsureNoRunning(app);result.Add("前置检查","complete","应用已退出，初始化目标已确认。");
            if(action=="rollback"){
                var root=sessionRoot??NativeSession.Latest(paths);session=NativeSession.Load(root,paths);EnsureNoRunning(app);session.Restore(services.Registry);result.session=root;result.Add("回滚","complete","原文件字节、不存在状态与已记录用户环境已恢复；CUA 和已创建目录不在此回滚范围。");result.state="complete";WriteResult(result);return result;
            }
            bool full=action!="proxy";if(action!="init"&&action!="reset"&&action!="proxy")throw new ArgumentException("不支持的初始化操作。");
            if(full&&app==null)throw new FileNotFoundException("未找到官方 Codex 桌面版；尚未重建配置。");
            session=NativeSession.Create(paths,services.Registry,full);result.session=session.root;result.Add("完整备份","complete","备份已保存："+session.root);EnsureNoRunning(app);
            if(!full){NativeData.Write(paths.Env,ProxyText(options));if(NativeData.Read(paths.Env)!=ProxyText(options))throw new IOException("代理文件写入后校验失败。");result.Add("代理配置","complete","仅重建 .env；其他配置保留。");initialized=true;}
            else {
                foreach(var key in new[]{"CODEX_CLI_PATH","CODEX_CODE_MODE_HOST_PATH"})services.Registry.Set(key,new RegistryValueState{Exists=false});foreach(var name in NativeSession.LegacyNames){var file=Path.Combine(paths.LegacyCli,name);if(File.Exists(file))File.Delete(file);}
                NativeData.Write(paths.Config,Config(paths,options));NativeData.Write(paths.Env,ProxyText(options));result.Add("配置重建","complete","配置、代理与用户运行时覆盖已处理。");
                new NativeRtk(paths,services).Prepare(session.root);result.Add("RTK","complete","组件、匿名统计关闭和实际集成产物已验证。");EnsureNoRunning(app);
                NativeData.Write(paths.Agents,Rules(paths,options));result.Add("全局规则","complete","全局规则已重建；原始自定义内容保留于备份。");
                if(options.Folder){foreach(var folder in new[]{"Conversations","Projects","OUT","Temp","Shared","Archive","Config","Docs"})Directory.CreateDirectory(Path.Combine(paths.Workspace,folder));result.Add("文件夹管理","complete","仅补齐缺失目录，已绑定无项目任务目录。");}else result.Add("文件夹管理","skipped","未选择，不写目录规则。");
                result.Add("子代理",options.Subagents?"complete":"skipped",options.Subagents?"子代理规则与 6 路线程配置已写入。":"未选择，多线程保持关闭。");
                if(NativeData.Read(paths.Config)!=Config(paths,options)||NativeData.Read(paths.Agents)!=Rules(paths,options)||NativeData.Read(paths.Env)!=ProxyText(options))throw new IOException("初始化生成文件与所选设置不一致。");if(services.Registry.Get("CODEX_CLI_PATH").Exists||services.Registry.Get("CODEX_CODE_MODE_HOST_PATH").Exists)throw new IOException("用户运行时覆盖未清理。");initialized=true;
                if(options.Cua){var cua=new NativeCua(paths,services).Run(true,Path.Combine(session.root,"CUA"));if(!cua.Passed)throw new IOException("CUA 校验未通过。");result.Add("CUA","complete",cua.Changed?"已修复并校验，原目录备份保留。":"现有组件完整，无需修复。");}else result.Add("CUA","skipped","未选择，不作为失败。");
                if(File.Exists(Path.Combine(paths.Home,"AGENTS.override.md")))result.Add("规则覆盖提示","warning","检测到 AGENTS.override.md，可能覆盖全局规则；文件已保留。");
                if(services.Registry.GetMachine("CODEX_CLI_PATH").Exists||services.Registry.GetMachine("CODEX_CODE_MODE_HOST_PATH").Exists)result.Add("机器级运行时覆盖","warning","机器级覆盖仍存在，未越权修改，请核对实际 CLI。");
            }
            result.state=result.steps.Any(x=>x.state=="warning")?"partial":"complete";
        }catch(Exception ex){result.state=initialized?"partial":"failed";result.Add("未完成","failed",ex.GetType().Name+"："+ex.Message);services.Log("操作未完成："+ex.Message);}
        finally{if(locked)mutex.ReleaseMutex();}}
        WriteResult(result);return result;
    }
    static void WriteResult(NativeResult result){if(String.IsNullOrWhiteSpace(result.session))return;try{NativeData.Write(Path.Combine(result.session,"Native_Result.json"),NativeData.Json.Serialize(result));NativeData.Write(Path.Combine(result.session,"Reset_Result.txt"),"结果："+result.state+"\n"+String.Join("\n",result.steps.Select(x=>x.name+"："+x.state+" — "+x.message))+"\n");}catch(Exception ex){result.state="failed";result.Add("报告","failed","报告保存失败："+ex.Message);}}
}
}
