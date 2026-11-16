using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace CodexKit {
public partial class MainWindow {
    readonly CheckBox websocketRepair=new CheckBox(),websocketEnabled=new CheckBox();
    readonly TextBox wsProxyHost=new TextBox(),wsProxyPort=new TextBox();
    readonly TextBlock wsEffective=new TextBlock();
    readonly ActivityIndicator operationActivity=new ActivityIndicator{Width=15,Height=15,Margin=new Thickness(0,0,7,0)};
    Dictionary<string,object> websocketReport,modelNetworkReport;
    bool loadingWebSocketSettings;
    void InitializeWebSocketControls(){
        loadingWebSocketSettings=true;foreach(var item in new[]{websocketRepair,websocketEnabled}){item.Content="WebSocket 线路修复";item.Style=(Style)Resources["KitCheck"];item.IsChecked=true;}
        foreach(var item in new[]{wsProxyHost,wsProxyPort}){item.Style=(Style)Resources["KitInput"];item.HorizontalAlignment=HorizontalAlignment.Stretch;}
        wsProxyPort.MaxLength=5;wsProxyHost.ToolTip="留空则沿用 HTTP 代理 IP；HTTP 关闭时需填写。";wsProxyPort.ToolTip="留空则沿用 HTTP 代理端口；HTTP 关闭时需填写。请使用 HTTP／混合代理入口。";
        wsEffective.Foreground=Accent;wsEffective.FontSize=12;wsEffective.TextWrapping=TextWrapping.Wrap;wsEffective.Margin=new Thickness(0,7,0,4);
        if(!preview)try{var saved=WebSocketSettings.Load(WebSocketSettings.SettingsPath);websocketRepair.IsChecked=websocketEnabled.IsChecked=saved.websocketRepair;wsProxyHost.Text=saved.wsProxyHost;wsProxyPort.Text=saved.wsProxyPort;}catch(Exception ex){networkSettingsError="WS 设置无法读取："+DesktopLocalization.ExceptionText(ex);}
        websocketRepair.Checked+=(s,e)=>{websocketEnabled.IsChecked=true;UpdateWebSocketControls();};websocketRepair.Unchecked+=(s,e)=>{websocketEnabled.IsChecked=false;UpdateWebSocketControls();};websocketEnabled.Checked+=(s,e)=>{websocketRepair.IsChecked=true;UpdateWebSocketControls();};websocketEnabled.Unchecked+=(s,e)=>{websocketRepair.IsChecked=false;UpdateWebSocketControls();};
        wsProxyHost.TextChanged+=(s,e)=>UpdateWebSocketControls();wsProxyPort.TextChanged+=(s,e)=>UpdateWebSocketControls();port.TextChanged+=(s,e)=>UpdateWebSocketControls();proxyHost.TextChanged+=(s,e)=>UpdateWebSocketControls();proxyEnabled.Checked+=(s,e)=>UpdateWebSocketControls();proxyEnabled.Unchecked+=(s,e)=>UpdateWebSocketControls();loadingWebSocketSettings=false;UpdateWebSocketControls();
    }
    WebSocketSettings CurrentWebSocketSettings(){return new WebSocketSettings{websocketRepair=websocketRepair.IsChecked==true,wsProxyHost=wsProxyHost.Text.Trim(),wsProxyPort=wsProxyPort.Text.Trim(),httpHost=ProxyHost(),httpPort=Port()};}
    Dictionary<string,object> WebSocketOptions(){return WebSocketSettings.Options(CurrentWebSocketSettings());}
    void UpdateWebSocketControls(){if(loadingWebSocketSettings)return;bool independent=wsProxyHost.Text.Trim().Length>0&&wsProxyPort.Text.Trim().Length>0;websocketRepair.IsEnabled=websocketEnabled.IsEnabled=proxyEnabled.IsChecked==true||independent;try{var settings=CurrentWebSocketSettings();wsEffective.Text=settings.websocketRepair?"本次拟用线路："+settings.Description:"WebSocket 线路修复已关闭；仍可检查连接";wsEffective.Foreground=Accent;}catch(Exception ex){wsEffective.Text=ex.Message;wsEffective.Foreground=MorandiTheme.Warning;}}
    void AddWebSocketSettings(StackPanel panel){
        panel.Children.Add(websocketEnabled);panel.Children.Add(T("帮助 Dot 等远程连接使用代理。默认沿用 HTTP 地址，也可单独设置。",12,MutedBrush));
        var advanced=new StackPanel();
        var grid=new Grid{Margin=new Thickness(0,5,0,3)};grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(2,GridUnitType.Star)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(12)});grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        var hostPanel=new StackPanel();hostPanel.Children.Add(T("WS 代理 IP（可留空）",12,MutedBrush));hostPanel.Children.Add(wsProxyHost);grid.Children.Add(hostPanel);var portPanel=new StackPanel();portPanel.Children.Add(T("WS 代理端口（可留空）",12,MutedBrush));portPanel.Children.Add(wsProxyPort);Grid.SetColumn(portPanel,2);grid.Children.Add(portPanel);advanced.Children.Add(T("两项留空时，分别沿用 HTTP 的 IP 和端口；关闭 HTTP 后需完整填写。",12,MutedBrush));advanced.Children.Add(grid);panel.Children.Add(wsEffective);panel.Children.Add(new Expander{Header="独立 WS 代理（可选）",Content=advanced,IsExpanded=wsProxyHost.Text.Length>0||wsProxyPort.Text.Length>0,Margin=new Thickness(0,5,0,7)});
        panel.Children.Add(T("独立入口仅用于 Dot 远程线路；模型回复保持原代理设置。支持 HTTP／混合入口，原 Codex 图标保持原有行为。",11,MutedBrush));UpdateWebSocketControls();
    }
    void ConnectionOverview(){
        subtitle.Text=preview?"设计预览 · 检查哪里连不上，再按需修复。":"检查哪里连不上，再按需修复。";
        var proxy=Card("当前代理","浏览器与远程连接可共用代理，也可为 Dot 单独填写。");proxy.Children.Add(proxyEnabled);proxy.Children.Add(ProxyPortRow());proxy.Children.Add(proxyPortNotice);proxy.Children.Add(T("代理 IP（留空为 127.0.0.1）",12,MutedBrush));proxy.Children.Add(proxyHost);
        var states=Card("连接状态","按需检查浏览器、模型回复和 Dot 远程连接。电脑名称和允许控制可在 Codex/Work 中设置。");
        CompactConnectionRow(states,"浏览器连接",ConnectionTag("browser",browserReport),Core.Value(browserReport,"patch","尚未验证真实浏览器连接"),()=>{repairTab=1;Navigate("连接与修复");});
        CompactConnectionRow(states,"模型回复",ConnectionTag("model",modelNetworkReport),Core.Value(modelNetworkReport,"modelState","尚未验证实际模型线路"),async()=>await CheckModelNetwork());
        CompactConnectionRow(states,"Dot 远程连接",ConnectionTag("dot",websocketReport),Core.Value(websocketReport,"dotState",Core.Value(websocketReport,"summary","尚未验证真实远程连接")),async()=>await Run("ws-start-verify","WebSocket 实际连接核验",false,WebSocketOptions()));
        Row(states,Button("检查 WebSocket 线路",async()=>await Run("ws-check","WebSocket 线路检查",false,WebSocketOptions()),true),Button("修复并启动 Codex",async()=>await StartWebSocketRepair(),false),Button("验证重启后的连接",async()=>await Run("ws-start-verify","WebSocket 实际连接核验",false,WebSocketOptions()),false));
        var ws=Card("WebSocket 线路修复设置","");AddWebSocketSettings(ws);

        Row(ws,Button("保存 WS 设置",()=>{try{if(preview){banner.Text="设计预览不会保存设置。";return;}CurrentWebSocketSettings().Save(WebSocketSettings.SettingsPath);banner.Text="WS 设置已保存；通过本工具启动后生效";}catch(Exception ex){Error(ex);}},false),Button("撤销 WebSocket 线路修复",async()=>await Run("ws-rollback","撤销 WebSocket 线路修复",true,WebSocketOptions(),"请先退出由本工具启动的 Codex。撤销工具保存的启动准备；保留 HTTP 配置、对话、项目和官方文件。"),false));
        if(websocketReport!=null){var result=Card("最近检查结果","");Pair(result,"代理",Core.Value(websocketReport,"proxyState","未检查"));Pair(result,"WebSocket",Core.Value(websocketReport,"websocketState",Core.Value(websocketReport,"summary","未检查")));Row(result,Button("查看检查日志",()=>Navigate("日志"),false));}
        var diagnostic=Card("诊断助手","CLI 或其他组件异常时，可进一步查看诊断建议。");diagnostic.Children.Add(T(diagnosticAdvice.Length>0?diagnosticAdvice:"诊断只读取状态，不自动执行修复。",12,MutedBrush));Row(diagnostic,Button("开始诊断",async()=>await Run("doctor","诊断助手",false),false));
    }
    async Task CheckModelNetwork(){var options=WebSocketOptions();var cli=Core.Value(status,"currentCliPath","");if(String.IsNullOrWhiteSpace(cli)){var install=Core.Value(status,"desktopInstallLocation","");if(install.Length>0)cli=Path.Combine(install,"app","resources","codex.exe");}options["modelCliPath"]=cli;await Run("ws-model-check","模型 WebSocket 线路检查",false,options);}
    static string ConnectionTag(string kind,Dictionary<string,object> report){if(report==null)return "未检查";if(kind=="model")return Core.Value(report,"passed","").Equals("True",StringComparison.OrdinalIgnoreCase)?"独立探针通过":"检查未通过";if(kind=="browser")return "入口检查 · 待验收";var state=Core.Value(report,"state","");return state=="connected"?"连接已有证据":state=="prepared"||state=="launched"?"启动已准备":state=="stopped"||state=="disconnected"?"尚未连接":"真实连接待验收";}
    void CompactConnectionRow(StackPanel parent,string title,string tag,string detail,Action action){
        var row=new Grid{Margin=new Thickness(0,7,0,7)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(120)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var label=T(title,13,TextBrush);label.FontWeight=FontWeights.SemiBold;row.Children.Add(label);var info=new StackPanel();var badge=new TextBlock{Text=tag,FontSize=11,Foreground=Accent};info.Children.Add(new Border{Child=badge,Background=MorandiTheme.Group,CornerRadius=new CornerRadius(5),Padding=new Thickness(6,3,6,3),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,4)});info.Children.Add(T(detail,12,MutedBrush));Grid.SetColumn(info,1);row.Children.Add(info);var button=Button("检查",action,false);button.Padding=new Thickness(10,3,10,3);button.Margin=new Thickness(6,0,0,0);button.VerticalAlignment=VerticalAlignment.Top;button.IsEnabled=!busy;Grid.SetColumn(button,2);row.Children.Add(button);parent.Children.Add(row);
    }
    async Task<Dictionary<string,object>> ReadWebSocketAppInfo(Dictionary<string,object> options){var id=Guid.NewGuid().ToString("N");var path=OperationLogPath(id);using(var process=StartOperation("ws-start-info",id,Environment.SystemDirectory.Substring(0,1),0,false,options)){while(!process.HasExited)await Task.Delay(100);var text=File.Exists(path)?File.ReadAllText(path):"";if(process.ExitCode!=0)throw new IOException("官方 Codex 启动检查未完成。\n"+DesktopLocalization.TranslateText(text));var record=text.Split('\n').LastOrDefault(x=>x.StartsWith("@@WS_APP@@"));if(record==null)throw new IOException("未收到官方桌面应用身份，请刷新状态后重试。");return Core.ReadJson(record.Substring("@@WS_APP@@".Length).Trim());}}
    Window WebSocketConfirmation(WebSocketSettings settings,int processCount){var dialog=CreateActionDialog("WebSocket 线路修复", "将使用 "+settings.Description+"，通过本工具重新启动官方 Codex。\n\n保留对话、项目和模型设置，不修改系统代理或官方安装文件。\n\n"+(processCount>0?"检测到 "+processCount+" 个相关进程。关闭会中断正在进行的任务，请先保存工作。\n\n":"")+"尚未结束的远程请求，请先确认处理；恢复连接后旧请求可能继续执行。",true);var buttons=InitializationFlowTests.Descendants<Button>(dialog).ToArray();var accept=buttons.FirstOrDefault(x=>Convert.ToString(x.Content)=="确认并继续");if(accept!=null)accept.Content=processCount>0?"关闭并修复":"修复并启动";return dialog;}
    static string StartupMessage(int processCount){return "将使用本次代理与 CLI 选择启动官方 Codex。"+(processCount>0?"\n\n需要关闭当前 Codex，将中断其中全部正在进行的对话和 Work 连接。请先保存工作。":"")+"\n\n未结束的远程请求可能在恢复连接后继续执行。";}
    Window CreateStartupConfirmation(string title,int processCount){return processCount==0?null:CreateActionDialog(title,StartupMessage(processCount),true);}
    Task StartWebSocketRepair(WebSocketSettings preparedSettings=null){return StartCodex(preparedSettings,false,true);}
    async Task StartCodex(WebSocketSettings preparedSettings,bool repairBrowser,bool requireWs){
        if(busy){banner.Text="请等待当前操作完成。";return;}if(preview){banner.Text="设计预览不会启动或关闭 Codex。";return;}if(Core.Admin){Error(new InvalidOperationException("请以普通用户权限打开工具，再使用修复并启动。"));return;}
        Window resultDialog=null;OperationRecord startRecord=null;bool returnToInitialization=page=="初始化"&&wizard==2;string title=repairBrowser?"浏览器修复并启动 Codex":requireWs?"WebSocket 修复并启动 Codex":"启动 Codex";
        try{
            var settings=preparedSettings??CurrentWebSocketSettings();var target=settings.Resolve();if(requireWs&&target==null)throw new InvalidOperationException("请启用 HTTP 代理，或完整填写独立 WS 代理地址。");
            busy=true;activeOperation="codex-start";operationTitle=title;UpdateBusyControls();banner.Text="正在检查官方应用与本次启动设置…";operationActivity.Visibility=Visibility.Visible;operationActivity.State="running";progress.IsIndeterminate=Motion.Enabled(preview);
            var id=Guid.NewGuid().ToString("N");lastLog=OperationLogPath(id);BeginRecord(id,title,lastLog,false);startRecord=activeRecord;rawLogText=chineseLogText="";localizedLines.Clear();showRawLog=false;logViewport.Reset();logInputComplete=false;File.WriteAllText(lastLog,"正在检查官方应用与本次启动设置…"+Environment.NewLine,new UTF8Encoding(false));
            var options=WebSocketSettings.Options(settings);var info=await ReadWebSocketAppInfo(options);string install=Core.Value(info,"installLocation","");await Task.Run(()=>WebSocketApp.Inspect(install,target!=null));var processes=(IList)info["processes"];
            var confirm=CreateStartupConfirmation(title,processes.Count);if(confirm!=null){if(confirm.ShowDialog()!=true){banner.Text="已取消，当前 Codex 保持不变。";File.AppendAllText(lastLog,banner.Text+Environment.NewLine);logInputComplete=true;ReadLog();FinishRecord("cancelled",banner.Text);return;}}
            if(processes.Count>0){options["allowCloseRunningApp"]=true;options["closeApprovedProcesses"]=String.Join(",",processes.Cast<Dictionary<string,object>>().Select(p=>Core.Value(p,"Id","")+":"+Core.Value(p,"Started","")));info=await ReadWebSocketAppInfo(options);if(!String.Equals(Core.Value(info,"installLocation",""),install,StringComparison.OrdinalIgnoreCase))throw new IOException("桌面应用在确认后更新，请重新检查。");}
            if(repairBrowser){using(var worker=StartOperation("browser-repair",id,Environment.SystemDirectory.Substring(0,1),settings.httpPort,false,options)){while(!worker.HasExited){await Task.Delay(300);ReadLog();}ReadLog();if(worker.ExitCode!=0)throw new IOException("浏览器入口准备未完成，未启动 Codex；请查看本次日志。");}}
            var gate=new object();Action<string> write=line=>{lock(gate)File.AppendAllText(lastLog,line+Environment.NewLine,new UTF8Encoding(false));};
            if(target!=null)await Task.Run(()=>WebSocketStartup.Prepare(settings,install,write));
            var checkOptions=WebSocketSettings.Options(settings);var current=await ReadWebSocketAppInfo(checkOptions);if(!String.Equals(Core.Value(current,"installLocation",""),install,StringComparison.OrdinalIgnoreCase))throw new IOException("桌面应用已更新，请重新检查后启动。");
            if(((IList)current["processes"]).Count>0)throw new IOException("Codex 已重新运行，请重新检查并确认关闭；本次未启动新的桌面。");
            websocketReport=await WebSocketStartup.Launch(settings,install);write(Core.Value(websocketReport,"summary",""));write("电脑名称和允许控制可在 Codex/Work 中自行设置。");logInputComplete=true;ReadLog();FinishRecord("complete",Core.Value(websocketReport,"summary",""));banner.Text="Codex 已启动 · 连接状态待验证";
        }catch(Exception ex){if(startRecord!=null&&activeRecord==startRecord&&activeRecord.state=="running"){if(File.Exists(lastLog))File.AppendAllText(lastLog,"\n"+DesktopLocalization.ExceptionText(ex)+"\n");logInputComplete=true;ReadLog();FinishRecord("failed",DesktopLocalization.ExceptionText(ex));}RecordUiError(ex,false);banner.Text=DesktopLocalization.ExceptionText(ex);resultDialog=CreateActionDialog("修复并启动未完成",DesktopLocalization.ExceptionText(ex),false);}
        finally{busy=false;activeOperation="";operationActivity.State="pending";operationActivity.Visibility=Visibility.Collapsed;progress.IsIndeterminate=false;progress.Value=0;repairTab=0;Navigate(returnToInitialization?"初始化":"连接与修复");}if(resultDialog!=null)resultDialog.ShowDialog();
    }

}
}
