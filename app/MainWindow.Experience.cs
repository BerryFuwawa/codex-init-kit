using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;using System.Windows;using System.Windows.Controls;using System.Windows.Media;
namespace CodexKit {
public partial class MainWindow {
    InitializationRequest initializationRequest;
    OperationRecord activeRecord,initializationRecord,selectedLogRecord,viewedLogRecord;
    readonly Dictionary<string,OperationRecord> operationRecords=new Dictionary<string,OperationRecord>();
    readonly StackPanel wizardActions=new StackPanel();
    LogViewport githubViewport;
    void UpdateBusyControls(){
        bool locked=busy&&activeOperation=="initialize";
        foreach(var control in new Control[]{drive,proxyEnabled,folderManagement,antiOverDefense,workJournal,commandLineFirst,memories,subagents,cuaRepair,browserRepair,officialCliUpdate,desktopUpdate,guard,cliNative,cliStandalone,parentModel,parentEffort,githubMirrors})control.IsEnabled=!locked;
        proxyPortPreset.IsEnabled=port.IsEnabled=proxyHost.IsEnabled=!locked&&proxyEnabled.IsChecked==true;
        remoteRelay.IsEnabled=!busy && proxyEnabled.IsChecked==true && (relayStatus.Installed || relayStatus.CanEnable);
        childModel.IsEnabled=!locked&&subagents.IsChecked==true;
        childEffort.IsEnabled=childModel.IsEnabled&&!AutoAssign;
        maxConcurrentSubagentsInput.IsEnabled=childModel.IsEnabled;
        githubCustomMirror.IsEnabled=!locked&&githubMirrors.IsChecked==true;
        foreach(var b in InitializationFlowTests.Descendants<Button>((DependencyObject)Content)){var label=Convert.ToString(b.Content);if(label==UninstallRelayLabel){b.IsEnabled=!busy && relayStatus.CanRemove;continue;}bool operation=new[]{"开启远控转发","关闭远控转发","确认并开始","保存代理","检查已保存","检查组件","修复组件","检查更新","下载并更新","更新官方","更新桌面","仅保存","保存 WS","保存下载","修复并启动","修复连接","检查连接","检查 WebSocket","验证重启","检查 CUA","检查并修复","开始诊断","安装 /","卸载启动","恢复原始","撤销 WebSocket","清理已","仅重试","切换官方","切换桌面","启动 Codex"}.Any(x=>label.StartsWith(x,StringComparison.Ordinal));if(operation){b.IsEnabled=!busy;b.ToolTip=busy?"当前操作正在执行，请等待结果后再操作。":null;}}
        UpdateRollbackActions();
    }
    void ApplyCompactLayout(){var root=Content as FrameworkElement;bool compact=root!=null&&root.ActualHeight>0&&root.ActualHeight<500;subtitle.Visibility=compact?Visibility.Collapsed:Visibility.Visible;heading.FontSize=compact?20:24;if(page=="初始化"&&wizard==0&&pageNavigation.Children.Count>0)pageNavigation.Children[0].Visibility=compact?Visibility.Collapsed:Visibility.Visible;}
    void WizardActions(params UIElement[] controls){Row(wizardActions,controls);wizardActions.Visibility=Visibility.Visible;}
    static bool LocalOperation(string op){return new[]{"relay-disable","doctor","model-network-check","browser-check","browser-repair","status","rollback-selected","rollback-delete-all","init-rollback","config-check","config-cleanup","cua-check","cua-repair","desktop-core-check","desktop-core-repair","guard-uninstall"}.Contains(op);}
    void RememberReading(){if(viewedLogRecord!=null){viewedLogRecord.Reading=logViewport.Capture();viewedLogRecord.ShowRaw=showRawLog;}}
    OperationRecord VisibleRecord(){return page=="初始化"&&wizard==2?initializationRecord:page=="日志"?(selectedLogRecord??activeRecord):activeRecord;}
    void BeginRecord(string id,string title,string path,bool initialization){RememberReading();activeRecord=OperationRecord.Create(id,title,path);operationRecords[path]=activeRecord;selectedLogRecord=null;if(initialization)initializationRecord=activeRecord;viewedLogRecord=null;showRawLog=false;PersistRecord(activeRecord);}
    void PersistRecord(OperationRecord record){if(record==null||preview)return;try{record.Save();}catch(Exception ex){banner.Text="操作记录暂未保存，原始日志保留："+DesktopLocalization.ExceptionText(ex);}}
    void FinishRecord(string state,string summary){if(activeRecord==null)return;activeRecord.state=state;activeRecord.summary=summary;activeRecord.finished=DateTime.Now.ToString("o");if(activeRecord==initializationRecord){activeRecord.steps=initializationProgress.Stages.Select(x=>(object)new Dictionary<string,object>{{"id",x.Id},{"title",x.Title},{"state",x.State},{"detail",x.Detail}}).ToArray();activeRecord.effects=initializationProgress.Effects.ToArray();}PersistRecord(activeRecord);}
    void UpdateRecordText(){if(activeRecord!=null&&activeRecord.rawPath==lastLog){activeRecord.Raw=rawLogText;activeRecord.Chinese=chineseLogText;}}
    void DisplayOperationLog(){var record=VisibleRecord();if(record==null){logViewport.SetText(showRawLog?rawLogText:chineseLogText);return;}if(record!=viewedLogRecord){RememberReading();viewedLogRecord=record;showRawLog=record.ShowRaw;record.LoadText();logViewport.SetText(showRawLog?record.Raw:record.Chinese);logViewport.Load(record.Reading);}else{record.LoadText();logViewport.SetText(showRawLog?record.Raw:record.Chinese);}}
    List<OperationRecord> LogHistory(){var records=preview?new List<OperationRecord>():OperationRecord.History(Core.Logs);foreach(var record in records)if(!operationRecords.ContainsKey(record.rawPath))operationRecords[record.rawPath]=record;return operationRecords.Values.OrderByDescending(x=>DateTime.Parse(x.started)).ToList();}
    void AddLogHistory(StackPanel panel){var records=LogHistory();if(records.Count==0)return;var picker=new ComboBox{ItemsSource=records,Style=(Style)Resources["KitCombo"],HorizontalAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(0,0,0,10),ToolTip="按时间选择操作。历史记录只读，不自动重新执行。"};picker.SelectedItem=VisibleRecord();picker.SelectionChanged+=(s,e)=>{RememberReading();selectedLogRecord=picker.SelectedItem as OperationRecord;Navigate("日志");};panel.Children.Add(picker);}
    string FeedbackText(){var record=VisibleRecord();return "Codex Init Kit "+(record==null?OperationRecord.BuildVersion:record.version)+"\n操作："+(record==null?operationTitle:record.title)+"\n时间："+(record==null?"未记录":record.started)+"\n结果："+(record==null?"未记录":record.StateLabel)+"\n摘要："+(record==null||String.IsNullOrWhiteSpace(record.summary)?"未提供":record.summary)+"\n\n"+LogToCopy();}
    void RecoverInitializationResult(int code,string error,bool cancelled){initializationProgress.Stopped(code,error,cancelled);if(!cancelled){if(code!=0)foreach(var stage in initializationProgress.Stages.Where(x=>x.State=="running")){stage.State="uncertain";stage.Detail="未收到此步骤的完成结果；重试前会先检查。";}incompleteInitializationStages=initializationProgress.Stages.Where(x=>x.State=="incomplete"||x.State=="uncertain").Select(x=>x.Id).ToArray();if(code!=0&&incompleteInitializationStages.Length==0)incompleteInitializationStages=new[]{"preflight"};}else incompleteInitializationStages=new string[0];}
    string SavedValue(string key){return Core.Value(status,key,status==null?"正在读取…":"未设置");}
    void AddDraftSummary(StackPanel parent){
        var changed=new List<string>();
        Action<string,string,string,Func<string,string>> diff=(name,saved,wanted,display)=>{
            if(saved!=wanted)changed.Add(name+"："+display(saved)+" → "+display(wanted));
        };
        diff("代理",SavedValue("configProxy"),ProxyDisplay(),value=>value);
        diff(ParentModelLabel,SavedValue("configModel"),Convert.ToString(parentModel.SelectedItem),ModelLabels.Display);
        diff("思考强度",SavedValue("configEffort"),Convert.ToString(parentEffort.SelectedItem),EffortLabels.Display);
        if(folderManagement.IsChecked==true)diff("无项目任务目录",SavedValue("configWorkspace"),Convert.ToString(drive.SelectedItem)+@":\Codex\Conversations",value=>value);
        parent.Children.Add(T(changed.Count==0?"模型、强度与代理等本页对比项未发现变化；其他所选功能执行时检查。":"待应用设置（尚未写入）\n"+String.Join("\n",changed),12,MutedBrush));
    }
    void AddChangeSummary(StackPanel panel){AddDraftSummary(panel);panel.Children.Add(T("配置和组件会先检查：相同内容跳过；首次实际变更前备份。完整初始化开始时若 Codex 正在运行，会询问关闭；这会中断该桌面实例中的其他对话。",12,MutedBrush));}
    void LimitDialog(Window dialog){var area=AvailableDialogArea();dialog.Width=Math.Min(dialog.Width,Math.Max(280,area.Width-24));dialog.MaxHeight=Math.Max(200,area.Height-24);dialog.UseLayoutRounding=true;dialog.SnapsToDevicePixels=true;}
    internal Func<Rect> DialogArea;
    Rect AvailableDialogArea(){
        if(DialogArea!=null)return DialogArea();
        var handle=new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var source=PresentationSource.FromVisual(this);
        var info=new MonitorArea.Info{size=System.Runtime.InteropServices.Marshal.SizeOf(typeof(MonitorArea.Info))};
        if(handle==IntPtr.Zero||source==null||source.CompositionTarget==null||!MonitorArea.GetMonitorInfo(MonitorArea.MonitorFromWindow(handle,2),ref info))return SystemParameters.WorkArea;
        var matrix=source.CompositionTarget.TransformFromDevice;
        return new Rect(matrix.Transform(new Point(info.work.left,info.work.top)),matrix.Transform(new Point(info.work.right,info.work.bottom)));
    }
    static class MonitorArea {
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]internal struct Bounds{internal int left,top,right,bottom;}
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]internal struct Info{internal int size;internal Bounds monitor,work;internal uint flags;}
        [System.Runtime.InteropServices.DllImport("user32.dll")]internal static extern IntPtr MonitorFromWindow(IntPtr window,uint flags);
        [System.Runtime.InteropServices.DllImport("user32.dll",CharSet=System.Runtime.InteropServices.CharSet.Auto)]internal static extern bool GetMonitorInfo(IntPtr monitor,ref Info info);
    }
}
}
