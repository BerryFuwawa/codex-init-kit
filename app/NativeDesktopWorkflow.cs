using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using CodexKit.Native;

namespace CodexKit {
internal interface IDesktopInitializationSteps {
    string DetectCli();
    void CaptureSelection();
    void RestoreSelection();
    void DesktopUpdate();
    bool DesktopSafe {get;}
    void OfficialCli(bool onlyIfMissing);
    void Protection();
    void SelectCli(string mode);
    void VerifyCli(string mode);
    void Browser();
    void WebSocket();
}
internal sealed class DesktopStageResult {public string stageId,name,state,message;}
internal sealed class NativeDesktopWorkflow {
    readonly NativePaths paths;readonly NativeServices services;readonly Dictionary<string,object> settings;readonly IDesktopInitializationSteps components;readonly Action<string> emit;
    internal NativeDesktopWorkflow(NativePaths p,NativeServices s,Dictionary<string,object> options,IDesktopInitializationSteps c,Action<string> log){paths=p;services=s;settings=Core.NormalizeOptions(options);components=c;emit=log;}
    internal static bool Handles(string op){return op=="initialize"||op=="initialize-preflight"||op=="status"||op=="proxy"||op=="cua-check"||op=="cua-repair"||op=="init-rollback"||op=="rollback-selected"||op=="rollback-delete-all"||op=="desktop-update"||op=="official-cli-update"||op=="guard-install"||op=="guard-update"||op=="guard-native"||op=="guard-current"||op=="browser-repair";}
    internal static NativeOptions Options(Dictionary<string,object> source,int port){var s=Core.NormalizeOptions(source);return new NativeOptions{Confirm=true,DesktopProfile=true,Folder=(bool)s["folderManagement"],Subagents=(bool)s["subagents"],Cua=(bool)s["cuaRepair"],AntiOverDefense=(bool)s["antiOverDefense"],WorkJournal=(bool)s["workJournal"],CommandLineFirst=(bool)s["commandLineFirst"],Memories=(bool)s["memories"],Proxy=port>0,ProxyPort=port,ProxyHost=Core.Value(s,"proxyHost",Core.DefaultProxyHost),ParentModel=(string)s["parentModel"],ParentEffort=(string)s["parentEffort"],ChildModel=(string)s["childModel"],ChildEffort=(string)s["childEffort"],AllowClose=(bool)s["allowCloseRunningApp"],ApprovedProcesses=Core.Value(s,"closeApprovedProcesses","").Split(new[]{','},StringSplitOptions.RemoveEmptyEntries)};}
    internal static object Preflight(NativePaths p,NativeServices s){OfficialDesktop app=null;try{app=s.Desktop.Discover();}catch(FileNotFoundException){}return new{processes=s.Processes.List(p,app).Select(x=>new{Id=x.Id,Started=x.Started,Name=x.Name,Path=x.Path}).ToArray()};}
    internal int Run(NativeOptions options,bool guard){
        var stages=new List<DesktopStageResult>();if((bool)settings["desktopUpdate"])stages.Add(Stage("desktop","更新桌面版（含原生 CLI）"));stages.Add(Stage("base","重建配置、目录及 CUA"));if((bool)settings["officialCliUpdate"])stages.Add(Stage("official","更新官方独立 CLI"));if(guard)stages.Add(Stage("guard","安装启动保护"));stages.Add(Stage("cli","应用所选 CLI"));if((bool)settings["browserRepair"])stages.Add(Stage("browser","浏览器连接修复"));if((bool)settings["websocketRepair"])stages.Add(Stage("websocket","WebSocket 线路修复"));
        var before=new Dictionary<string,RegistryValueState>();string mode="",session=null;bool basePassed=false,applied=false;int total=stages.Count+1;
        try{
            Progress("preflight","前置检查","running","正在核对应用状态与所选 CLI",1,total);
            OfficialDesktop app=null;try{app=services.Desktop.Discover();}catch(FileNotFoundException){}
            NativeProcessWorkflow.EnsureStopped(paths,app,services.Processes,options.AllowClose,options.ApprovedProcesses,emit);
            mode=Core.Value(settings,"cliMode","auto");if(mode=="auto"){mode=components.DetectCli();if(mode!="desktop-native"&&mode!="official-standalone")throw new InvalidOperationException("无法确认当前 CLI 类型，请在初始化中手动选择后再开始。");emit("初始化前检测到："+Label(mode)+"；本次自动沿用。");}
            foreach(var name in new[]{"CODEX_CLI_PATH","CODEX_CODE_MODE_HOST_PATH"})before[name]=services.Registry.Get(name);components.CaptureSelection();
            Progress("preflight","前置检查","complete","应用状态与 CLI 选择检查完成",1,total);
            for(int i=0;i<stages.Count;i++){
                var stage=stages[i];Progress(stage.stageId,stage.name,"running",stage.name,i+2,total);
                try{
                    switch(stage.stageId){
                    case "desktop":components.DesktopUpdate();break;
                    case "base":
                        var requestedCua=options.Cua;options.AllowClose=false;options.ApprovedProcesses=null;
                        if(!components.DesktopSafe&&options.Cua){options.Cua=false;emit("桌面更新待完成，CUA 修复已暂停；配置重建仍可继续。");}
                        var result=new NativeInitializer(paths,services).Run("init",options,null);session=result.session;
                        foreach(var item in result.steps)emit(item.name+"："+item.message);
                        if(result.state!="complete"&&result.state!="partial")throw new IOException("基础初始化未完成；"+String.Join("；",result.steps.Where(x=>x.state=="failed").Select(x=>x.message)));
                        basePassed=true;
                        if(result.state=="partial"||requestedCua&&!options.Cua)throw new IOException("基础配置已保存，但部分检查或 CUA 修复未完成，请查看日志。");
                        break;
                    case "official":components.OfficialCli(false);break;
                    case "guard":RequireSafe();components.Protection();break;
                    case "cli":
                        if(!basePassed)throw new InvalidOperationException("基础初始化未完成，未切换 CLI。");RequireSafe();if(mode=="official-standalone")components.OfficialCli(true);components.SelectCli(mode);components.VerifyCli(mode);applied=true;break;
                    case "browser":RequireSafe();components.Browser();break;
                    case "websocket":RequireSafe();if(!options.Proxy&&Core.Value(settings,"wsProxyHost","")==""&&Core.Value(settings,"wsProxyPort","")==""){stage.state="skipped";stage.message="未配置代理，保持直连；仍可检查 WebSocket 线路。";}else components.WebSocket();break;
                    }
                    if(stage.state!="skipped"){stage.state="complete";stage.message=stage.stageId=="cli"?"已选择并核验："+Label(mode):stage.stageId=="websocket"?"启动准备已保存；真实连接待启动后验证":"完成";}
                }catch(Exception ex){stage.state="incomplete";stage.message=ex.Message;if(stage.stageId=="cli")stage.message+=Restore(before);emit("【未完成】"+stage.name+"："+stage.message);}
                Progress(stage.stageId,stage.name,stage.state,stage.message,i+2,total);
            }
            if(applied)try{components.VerifyCli(mode);emit("最终 CLI 模式："+Label(mode));}catch(Exception ex){var stage=stages.First(x=>x.stageId=="cli");stage.state="incomplete";stage.message="最终 CLI 模式核验失败："+ex.Message+Restore(before);Progress(stage.stageId,stage.name,stage.state,stage.message,stages.IndexOf(stage)+2,total);}
            return Finish(stages,basePassed,session);
        }catch(Exception ex){Progress("preflight","前置检查","incomplete",ex.Message,1,total);stages.Insert(0,new DesktopStageResult{stageId="preflight",name="前置检查",state="incomplete",message=ex.Message});foreach(var stage in stages.Where(x=>x.state==null)){stage.state="skipped";stage.message="前置检查未通过，未执行。";}return Finish(stages,false,session);}
    }
    string Restore(Dictionary<string,RegistryValueState> before){try{foreach(var pair in before)services.Registry.Set(pair.Key,pair.Value);components.RestoreSelection();return "；已恢复初始化前的用户 CLI 路径设置。";}catch(Exception ex){return "；原 CLI 路径恢复未完成："+ex.Message;}}
    void RequireSafe(){if(!components.DesktopSafe)throw new InvalidOperationException("桌面版更新尚未完成或组件不完整，依赖修复已暂停，请检查商店更新和重启提示。");}
    int Finish(List<DesktopStageResult> stages,bool basePassed,string session){var state=!basePassed?"failed":stages.Any(x=>x.state!="complete"&&x.state!="skipped")?"partial":"complete";var summary=String.Join("\n",stages.Select(x=>x.name+"："+x.message));var result=new{state=state,summary=summary,steps=stages,session=session};if(session!=null)try{NativeData.Write(Path.Combine(session,"Desktop_Result.json"),NativeData.Json.Serialize(result));}catch(Exception ex){emit("初始化结果报告未能保存："+ex.Message);state=basePassed?"partial":"failed";summary+="\n结果报告："+ex.Message;}
        emit("@@INIT_RESULT@@"+Core.Json.Serialize(new{state=state,summary=summary,steps=stages,session=session}));return state=="complete"?0:state=="partial"?2:1;}
    void Progress(string id,string title,string state,string detail,int step,int total){emit("@@PROGRESS@@"+Core.Json.Serialize(new{stageId=id,title=title,state=state,detail=detail,step=step,total=total}));}
    static DesktopStageResult Stage(string id,string name){return new DesktopStageResult{stageId=id,name=name};}
    static string Label(string mode){return mode=="official-standalone"?"官方独立 CLI":"桌面版原生 CLI";}
}
}
