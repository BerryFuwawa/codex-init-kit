using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using CodexKit.Native;

namespace CodexKit {
internal interface IDesktopInitializationSteps {
    IEnumerable<ConfigurationEffect> Changes {get;}
    string DetectCli();
    void CaptureSelection();
    void RestoreSelection();
    void EnableRelay(System.Net.IPEndPoint target);
    void DesktopUpdate();
    bool DesktopSafe {get;}
    void OfficialCli(bool onlyIfMissing);
    void Protection();
    void SelectCli(string mode);
    void VerifyCli(string mode);
    void Browser();
    string Playwright(Action beforeWrite);
    string ChromeDevTools(Action beforeWrite);
}
internal sealed class DesktopStageResult {public string stageId,name,state,message;}
internal sealed class NativeDesktopWorkflow {
    readonly List<ConfigurationEffect> changes=new List<ConfigurationEffect>();
    readonly NativePaths paths;readonly NativeServices services;readonly Dictionary<string,object> settings;readonly IDesktopInitializationSteps components;readonly Action<string> emit;
    internal NativeDesktopWorkflow(NativePaths p,NativeServices s,Dictionary<string,object> options,IDesktopInitializationSteps c,Action<string> log){paths=p;services=s;settings=Core.NormalizeOptions(options);components=c;emit=log;}
    internal static bool Handles(string op){return op=="initialize"||op=="initialize-preflight"||op=="status"||op=="proxy"||op=="cua-check"||op=="cua-repair"||op=="init-rollback"||op=="rollback-selected"||op=="rollback-delete-all"||op=="desktop-update"||op=="official-cli-update"||op=="guard-install"||op=="guard-update"||op=="guard-native"||op=="guard-current"||op=="browser-repair"||op=="browser-check"||NativeBrowserTools.IsRemoval(op)||op==NativeBrowserTools.InstallFirefoxOperation;}
    internal static NativeOptions Options(Dictionary<string,object> source,int port){var s=Core.NormalizeOptions(source);if((bool)s["remoteRelay"] && port==0)throw new ArgumentException("开启远控转发需要先选择代理端口。");return new NativeOptions{Confirm=true,DesktopProfile=true,ChildAutoAssign=(bool)s["childAutoAssign"],Folder=(bool)s["folderManagement"],Subagents=(bool)s["subagents"],MaxConcurrentSubagents=(long)s["maxConcurrentSubagents"],Cua=(bool)s["cuaRepair"],AntiOverDefense=(bool)s["antiOverDefense"],WorkJournal=(bool)s["workJournal"],CommandLineFirst=(bool)s["commandLineFirst"],Memories=(bool)s["memories"],Proxy=port>0,ProxyPort=port,ProxyHost=Core.Value(s,"proxyHost",Core.DefaultProxyHost),ParentModel=(string)s["parentModel"],ParentEffort=(string)s["parentEffort"],ChildModel=(string)s["childModel"],ChildEffort=(string)s["childEffort"],AllowClose=(bool)s["allowCloseRunningApp"],ApprovedProcesses=Core.Value(s,"closeApprovedProcesses","").Split(new[]{','},StringSplitOptions.RemoveEmptyEntries)};}
    internal static object Preflight(NativePaths p,NativeServices s,NativeOptions options=null,Dictionary<string,object> settings=null){OfficialDesktop app=null;try{app=s.Desktop.Discover();}catch(FileNotFoundException){}var running=s.Processes.List(p,app);var reasons=new List<string>();if(options!=null&&app!=null){var retry=settings==null?"":Core.Value(settings,"retryStages","");Func<string,bool> selected=id=>retry.Length==0||retry.Split(',').Contains(id);if(retry.Length>0&&running.Count>0){var updates=new NativeDesktopComponents(p,s,settings,options.Proxy?options.ProxyPort:0,s.Log);Action<string,Func<bool>,string> checkUpdate=(name,check,reason)=>{try{if(check())reasons.Add(reason);}catch(Exception ex){s.Log(name+"更新预检未完成："+ex.Message+"；该项将在执行阶段报告结果，其他项目可继续。");}};if(selected("desktop")&&(bool)settings["desktopUpdate"])checkUpdate("桌面版",updates.DesktopUpdateAvailable,"发现桌面更新，需要退出整个 Codex 才能更新应用包；会中断其他对话。");if(selected("official")&&(bool)settings["officialCliUpdate"])checkUpdate("独立 CLI",()=>updates.OfficialUpdateRequired(false),"独立 CLI 需要替换当前入口；退出 Codex 后才能完成更新。");if(selected("guard"))checkUpdate("启动保护",()=>updates.OfficialUpdateRequired(true),"启动保护缺少可用的独立 CLI；需要先退出 Codex 准备组件。");}if(selected("base"))new NativeDesktopReconcile(p,s,options).Configuration();if(selected("base")&&options.Cua&&!new NativeCua(p,s).Run(false,p.BackupRoot).Passed)reasons.Add("CUA 组件需要替换；所有正在运行的 Codex 对话会被中断。");if(selected("browser")&&settings!=null&&(bool)settings["browserRepair"]&&new NativeDesktopBrowser(p,s,s.Log).RequiresChange())reasons.Add("浏览器入口需要替换；必须退出 Codex 才能安全写入。");if(selected("cli")&&settings!=null&&Core.Value(settings,"cliMode","auto")!="auto"){var c=new NativeDesktopComponents(p,s,settings,options.Proxy?options.ProxyPort:0,s.Log);if(c.DetectCli()!=Core.Value(settings,"cliMode","auto"))reasons.Add("CLI 模式需要切换；退出后才能核对新模式。");}}if(options!=null&&settings!=null&&Core.Value(settings,"retryStages","").Length==0&&running.Count>0)reasons.Insert(0,"完整初始化将先关闭 Codex，再检查并准备配置；会中断此桌面实例中的其他对话，请先保存工作。");return new{processes=(reasons.Count>0?running:new List<ProcessIdentity>()).Select(x=>new{Id=x.Id,Started=x.Started,Name=x.Name,Path=x.Path}).ToArray(),runningCount=running.Count,requiresClose=reasons.Count>0,reasons=reasons.ToArray()};}
    internal int Run(NativeOptions options,bool guard,Action proxySaved=null){
        var stages=new List<DesktopStageResult>();if((bool)settings["desktopUpdate"])stages.Add(Stage("desktop","更新桌面版（含原生 CLI）"));stages.Add(Stage("base","检查配置、目录及 CUA"));if((bool)settings["remoteRelay"])stages.Add(Stage("relay","开启远控转发"));if((bool)settings["officialCliUpdate"])stages.Add(Stage("official","更新官方独立 CLI"));if(guard)stages.Add(Stage("guard","安装启动保护"));stages.Add(Stage("cli","应用所选 CLI"));if((bool)settings["browserRepair"])stages.Add(Stage("browser","浏览器组件检查"));
        if(Core.BrowserToolSelected(settings,"playwrightCli"))stages.Add(Stage(NativeBrowserTools.PlaywrightStage,"准备 Playwright CLI"));
        if(Core.BrowserToolSelected(settings,"chromeDevToolsMcp"))stages.Add(Stage(NativeBrowserTools.ChromeDevToolsStage,"配置 Chrome DevTools MCP"));
        var retry=Core.Value(settings,"retryStages","");if(retry.Length>0){var ids=retry.Split(',');if(ids.Any(x=>!stages.Any(s=>s.stageId==x)))throw new ArgumentException("重试项目无效，请返回结果页重新选择。");stages=stages.Where(x=>ids.Contains(x.stageId)).ToList();}
        var before=new Dictionary<string,RegistryValueState>();string mode="",session=null;bool basePassed=Core.Value(settings,"retryStages","").Length>0&&!Core.Value(settings,"retryStages","").Split(',').Contains("base"),applied=false;int total=stages.Count+1;
        Func<NativeSession> browserToolsBackup=()=>{
            if(session!=null)return NativeSession.Load(session,paths);
            var created=NativeSession.Create(paths,services.Registry,true,true);
            session=created.root;
            return created;
        };
        try{
            Progress("preflight","前置检查","running","正在核对应用状态与所选 CLI",1,total);
            OfficialDesktop app=null;try{app=services.Desktop.Discover();}catch(FileNotFoundException){}
            if(options.AllowClose)NativeProcessWorkflow.EnsureStopped(paths,app,services.Processes,true,options.ApprovedProcesses,emit);
            if(retry.Length==0&&services.Processes.List(paths,app).Count>0)throw new InvalidOperationException("完整初始化开始前需要确认关闭 Codex；应用仍在运行或已重新启动，配置尚未修改。请返回确认页重试。");
            mode=Core.Value(settings,"cliMode","auto");if(mode=="auto"&&(retry.Length==0||stages.Any(x=>x.stageId=="cli"))){mode=components.DetectCli();if(mode!="desktop-native"&&mode!="official-standalone")throw new InvalidOperationException("无法确认当前 CLI 类型，请在初始化中手动选择后再开始。");emit("初始化前保存的 CLI 选择："+Label(mode)+"；本次自动沿用，当前实例未核验。");}
            foreach(var name in new[]{"CODEX_CLI_PATH","CODEX_CODE_MODE_HOST_PATH"})before[name]=services.Registry.Get(name);components.CaptureSelection();
            Progress("preflight","前置检查","complete","应用状态与 CLI 选择检查完成",1,total);
            for(int i=0;i<stages.Count;i++){
                var stage=stages[i];Progress(stage.stageId,stage.name,"running",stage.name,i+2,total);
                bool selectionAttempted=false;try{
                    switch(stage.stageId){
                    case "desktop":components.DesktopUpdate();break;
                    case "base":
                        var requestedCua=options.Cua;options.AllowClose=false;options.ApprovedProcesses=null;
                        if(!components.DesktopSafe&&options.Cua){options.Cua=false;emit("桌面更新待完成，CUA 修复已暂停；配置重建仍可继续。");}
                        var reconcile=new NativeDesktopReconcile(paths,services,options);NativeResult result;try{result=reconcile.Run(false);}finally{changes.AddRange(reconcile.Changes);}session=result.session;
                        foreach(var item in result.steps)emit(item.name+"："+item.message);
                        if(result.state!="complete"&&result.state!="partial"||result.steps.Any(x=>x.name=="配置与代理"&&x.state=="failed"))throw new IOException("基础初始化未完成；"+String.Join("；",result.steps.Where(x=>x.state=="failed").Select(x=>x.message)));
                        basePassed=true;
                        if(proxySaved!=null && !stages.Any(x=>x.stageId=="relay"))proxySaved();
                        if(result.state=="partial"||requestedCua&&!options.Cua)throw new IOException("基础配置已保存，但部分检查或 CUA 修复未完成，请查看日志。");
                        break;
                    case "relay":
                        if(!basePassed)throw new InvalidOperationException("代理配置未保存，请先完成基础配置。");
                        var relayTarget=new System.Net.IPEndPoint(System.Net.IPAddress.Parse(options.ProxyHost),options.ProxyPort);
                        components.EnableRelay(relayTarget);
                        stage.message="远控转发已开启，上游代理 "+relayTarget;
                        break;
                    case "official":components.OfficialCli(false);break;
                    case "guard":RequireSafe();components.Protection();break;
                    case "cli":
                        if(!basePassed)throw new InvalidOperationException("基础初始化未完成，未切换 CLI。");RequireSafe();if(mode=="official-standalone")components.OfficialCli(true);selectionAttempted=true;components.SelectCli(mode);components.VerifyCli(mode);applied=true;break;
                    case "browser":RequireSafe();components.Browser();break;
                    case NativeBrowserTools.PlaywrightStage:
                        stage.message=components.Playwright(()=>browserToolsBackup().Include(paths.PlaywrightSkill,"playwright-skill.md"));
                        break;
                    case NativeBrowserTools.ChromeDevToolsStage:
                        if(!basePassed)throw new InvalidOperationException("基础配置尚未保存，请先重试基础初始化。");
                        stage.message=components.ChromeDevTools(()=>{browserToolsBackup();});
                        break;
                    }
                    if(stage.state!="skipped"){stage.state="complete";if(stage.message==null)stage.message=stage.stageId=="cli"?"已核对保存的 CLI 选择："+Label(mode)+"；当前运行实例未核验":stage.stageId=="browser"?"浏览器组件已检查；真实连接待验收":stage.stageId=="base"?"所选配置与组件检查完成；真实环境运行状态待验收":"完成";}
                }catch(Exception ex){stage.state="incomplete";stage.message=ex.Message;if(stage.stageId=="cli")stage.message+=selectionAttempted?Restore(before):"；现有 CLI 设置保留。";emit("【未完成】"+stage.name+"："+stage.message);}
                Progress(stage.stageId,stage.name,stage.state,stage.message,i+2,total);
            }
            if(applied)try{components.VerifyCli(mode);emit("保存的 CLI 选择："+Label(mode)+"；已核对路径配置，未验证当前运行实例");}catch(Exception ex){var stage=stages.First(x=>x.stageId=="cli");stage.state="incomplete";stage.message="保存的 CLI 路径核验失败："+ex.Message+Restore(before);Progress(stage.stageId,stage.name,stage.state,stage.message,stages.IndexOf(stage)+2,total);}
            return Finish(stages,basePassed,session);
        }catch(Exception ex){Progress("preflight","前置检查","incomplete",ex.Message,1,total);stages.Insert(0,new DesktopStageResult{stageId="preflight",name="前置检查",state="incomplete",message=ex.Message});foreach(var stage in stages.Where(x=>x.state==null)){stage.state="skipped";stage.message="前置检查未通过，未执行。";}return Finish(stages,false,session);}
    }
    string Restore(Dictionary<string,RegistryValueState> before){try{foreach(var pair in before)services.Registry.Set(pair.Key,pair.Value);components.RestoreSelection();return "；已恢复初始化前的用户 CLI 路径设置。";}catch(Exception ex){return "；原 CLI 路径恢复未完成："+ex.Message;}}
    void RequireSafe(){if(!components.DesktopSafe)throw new InvalidOperationException("桌面版更新尚未完成或组件不完整，依赖修复已暂停，请检查商店更新和重启提示。");}
    int Finish(List<DesktopStageResult> stages,bool basePassed,string session){var state=!basePassed?"failed":stages.Any(x=>x.state!="complete"&&x.state!="skipped")?"partial":"complete";var summary=String.Join("\n",stages.Select(x=>x.name+"："+x.message));var effects=changes.Concat(components.Changes).ToArray();foreach(var effect in effects)emit("【配置生效】"+effect.name+"\n"+effect.Detail);var result=new{state=state,summary=summary,steps=stages,session=session,effects=effects};if(session!=null)try{NativeData.Write(Path.Combine(session,"Desktop_Result.json"),NativeData.Json.Serialize(result));}catch(Exception ex){emit("初始化结果报告未能保存："+ex.Message);state=basePassed?"partial":"failed";summary+="\n结果报告："+ex.Message;}
        emit("@@INIT_RESULT@@"+Core.Json.Serialize(new{state=state,summary=summary,steps=stages,session=session,effects=effects}));return state=="complete"?0:state=="partial"?2:1;}
    void Progress(string id,string title,string state,string detail,int step,int total){emit("@@PROGRESS@@"+Core.Json.Serialize(new{stageId=id,title=title,state=state,detail=detail,step=step,total=total}));}
    static DesktopStageResult Stage(string id,string name){return new DesktopStageResult{stageId=id,name=name};}
    static string Label(string mode){return mode=="official-standalone"?"官方独立 CLI":"桌面版原生 CLI";}
}
}
