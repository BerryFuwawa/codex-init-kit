using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace CodexKit {
public partial class MainWindow {
    readonly StackPanel pageNavigation=new StackPanel();
    readonly SmoothScrollViewer pageScroll=new SmoothScrollViewer();
    readonly Dictionary<string,TextBlock> navigationSections=new Dictionary<string,TextBlock>();
    readonly InitializationProgress initializationProgress=new InitializationProgress();
    readonly Dictionary<string,ActivityIndicator> stageIndicators=new Dictionary<string,ActivityIndicator>();
    readonly Dictionary<string,TextBlock[]> stageLabels=new Dictionary<string,TextBlock[]>();
    System.Windows.Shapes.Path pageHeadingIcon;
    ProgressBar stageProgress;
    TextBlock stageHeading,stageCounter,stageElapsed;
    int configurationTab,repairTab=0,progressReadLine=-1;
    string initializationState="";
    DateTime initializationStarted;

    void BuildShell(){
        Title="Codex Init Kit "+OperationRecord.BuildVersion;Width=1000;Height=740;MinWidth=Math.Min(800,SystemParameters.WorkArea.Width);MinHeight=Math.Min(540,SystemParameters.WorkArea.Height);
        Width=Math.Min(Width,SystemParameters.WorkArea.Width);Height=Math.Min(Height,SystemParameters.WorkArea.Height);FontSize=13;
        var layout=new Grid{Background=MorandiTheme.Canvas};layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(164)});layout.ColumnDefinitions.Add(new ColumnDefinition());Content=layout;layout.SizeChanged+=(s,e)=>ApplyCompactLayout();
        var rail=new Grid{Background=MorandiTheme.Rail};rail.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});rail.RowDefinitions.Add(new RowDefinition());rail.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});layout.Children.Add(rail);
        var brand=new StackPanel{Margin=new Thickness(17,20,12,14)};var logoRow=new StackPanel{Orientation=Orientation.Horizontal};logoRow.Children.Add(new Image{Source=Brand.VectorLogo,Width=32,Height=32,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,8,0)});logoRow.Children.Add(new TextBlock{Text="Codex Init Kit",FontSize=13,FontWeight=FontWeights.SemiBold,Foreground=TextBrush,VerticalAlignment=VerticalAlignment.Center});brand.Children.Add(logoRow);rail.Children.Add(brand);
        var nav=new StackPanel{Margin=new Thickness(10,0,10,0)};var navScroll=new SmoothScrollViewer{Content=nav,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Grid.SetRow(navScroll,1);rail.Children.Add(navScroll);
        var groups=new[]{new[]{"开始","概览","初始化"},new[]{"维护","连接与修复","CLI 管理","备份与回滚"},new[]{"工具","更新与下载","日志"}};
        foreach(var group in groups){
            var section=new StackPanel{Margin=new Thickness(0,10,0,3)};
            var header=new Grid{Margin=new Thickness(7,0,5,5)};header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});header.ColumnDefinitions.Add(new ColumnDefinition());
            var caption=T(group[0],14,TextBrush);caption.FontWeight=FontWeights.SemiBold;caption.Margin=new Thickness(0);header.Children.Add(caption);
            var divider=new Border{Height=1,Background=MorandiTheme.Border,Margin=new Thickness(9,0,0,0),VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(divider,1);header.Children.Add(divider);section.Children.Add(header);
            var children=new StackPanel{Margin=new Thickness(5,0,0,0)};
            foreach(var item in group.Skip(1)){
                var name=item;var button=Button(name,async()=>{Navigate(name);if((name=="CLI 管理"||name=="备份与回滚")&&!preview&&!busy)await RefreshStatusQuietly();},false);
                button.Style=(Style)Resources["KitNavigation"];button.Tag=Geometry.Parse(NavigationIcon(name));button.FontSize=12.5;button.HorizontalAlignment=HorizontalAlignment.Stretch;button.HorizontalContentAlignment=HorizontalAlignment.Left;button.Padding=new Thickness(10,8,5,8);button.Margin=new Thickness(0,1,0,1);button.BorderThickness=new Thickness(0);button.BorderBrush=Brushes.Transparent;
                children.Children.Add(button);navigation.Add(button);navigationSections[name]=caption;
            }
            section.Children.Add(new Border{BorderBrush=MorandiTheme.Border,BorderThickness=new Thickness(1,0,0,0),Margin=new Thickness(11,0,0,0),Child=children});nav.Children.Add(section);
        }
        var foot=new StackPanel{Margin=new Thickness(17,10,10,13)};foot.Children.Add(T("v"+OperationRecord.BuildVersion,11,MutedBrush));foot.Children.Add(Link("GitHub ↗",()=>Open("https://github.com/"+Core.Repository)));foot.Children.Add(Link("关于与许可",ShowLicenses));Grid.SetRow(foot,2);rail.Children.Add(foot);
        var main=new Grid{Margin=new Thickness(19,15,19,12)};main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition());main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Grid.SetColumn(main,1);layout.Children.Add(main);
        var titles=new StackPanel();heading.FontSize=24;heading.FontWeight=FontWeights.SemiBold;var headingRow=new DockPanel();pageHeadingIcon=new System.Windows.Shapes.Path{Data=UiIcons.For("概览",true),Stroke=Accent,StrokeThickness=1.6,Width=22,Height=22,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,10,0),VerticalAlignment=VerticalAlignment.Center};headingRow.Children.Add(pageHeadingIcon);headingRow.Children.Add(heading);titles.Children.Add(headingRow);subtitle.Foreground=MutedBrush;subtitle.FontSize=12;subtitle.TextWrapping=TextWrapping.Wrap;subtitle.Margin=new Thickness(0,4,0,12);titles.Children.Add(subtitle);main.Children.Add(titles);
        Grid.SetRow(pageNavigation,1);main.Children.Add(pageNavigation);
        pageScroll.Content=content;pageScroll.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;pageScroll.HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled;Grid.SetRow(pageScroll,2);main.Children.Add(pageScroll);
        wizardActions.Margin=new Thickness(0,8,0,0);Grid.SetRow(wizardActions,3);main.Children.Add(wizardActions);
        var bottom=new StackPanel{Margin=new Thickness(0,9,0,0)};var line=new Grid();line.ColumnDefinitions.Add(new ColumnDefinition());line.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        banner.Text="准备就绪";banner.FontSize=11;banner.Foreground=MutedBrush;banner.TextWrapping=TextWrapping.Wrap;banner.Margin=new Thickness(0,0,8,0);banner.Cursor=System.Windows.Input.Cursors.Hand;banner.ToolTip="查看执行日志";banner.MouseLeftButtonUp+=(s,e)=>Navigate("日志");var statusLine=new DockPanel();operationActivity.Visibility=Visibility.Collapsed;statusLine.Children.Add(operationActivity);statusLine.Children.Add(banner);line.Children.Add(statusLine);
        var updates=new StackPanel{HorizontalAlignment=HorizontalAlignment.Right};updateNotice=Button("检测到更新，点击更新",async()=>await InstallUpdate(),false);updateNotice.Visibility=Visibility.Collapsed;updateNotice.Margin=new Thickness(0);updateNotice.Padding=new Thickness(8,3,8,3);updates.Children.Add(updateNotice);updateStatus.FontSize=11;updateStatus.Foreground=MutedBrush;updateStatus.Text="自动检查更新";updates.Children.Add(updateStatus);Grid.SetColumn(updates,1);line.Children.Add(updates);bottom.Children.Add(line);
        progress.Style=(Style)Resources["KitProgress"];progress.Height=3;progress.Maximum=100;progress.Margin=new Thickness(0,7,0,0);bottom.Children.Add(progress);Grid.SetRow(bottom,4);main.Children.Add(bottom);
    }
    static string NavigationIcon(string page){
        switch(page){
            case "概览":return "M3,10 L12,3 L21,10 M5,9 L5,21 L19,21 L19,9 M9,21 L9,14 L15,14 L15,21";
            case "初始化":return "M5,20 L18,7 M3,8 L7,8 M5,6 L5,10 M17,2 L17,6 M15,4 L19,4 M17,16 L21,16 M19,14 L19,18";
            case "连接与修复":return "M9,15 L15,9 M7,13 L5,15 C2,18 6,22 9,19 L12,16 M12,8 L15,5 C18,2 22,6 19,9 L17,11";
            case "CLI 管理":return "M3,4 L21,4 L21,20 L3,20 Z M6,9 L10,12 L6,15 M13,16 L18,16";
            case "备份与回滚":return "M4,8 C6,3 15,2 19,7 C25,15 15,24 7,19 M3,3 L3,9 L9,9 M12,7 L12,13 L16,15";
            case "更新与下载":return "M12,3 L12,16 M7,11 L12,16 L17,11 M4,17 L4,21 L20,21 L20,17";
            default:return "M5,3 L16,3 L20,7 L20,21 L5,21 Z M8,9 L16,9 M8,13 L16,13 M8,17 L14,17";
        }
    }
    void ShowLicenses(){
        var text="Codex Init Kit\nCopyright © 2026 BerryFuwawa\n\n项目源码： https://github.com/"+Core.Repository+"\n\n";
        using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.Project.LICENSE")){if(stream!=null)using(var reader=new StreamReader(stream))text+=reader.ReadToEnd();}
        using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.HERO-Anti-OverDefense.LICENSE.txt")){if(stream!=null)using(var reader=new StreamReader(stream))text+="\n\n第三方规则\n"+reader.ReadToEnd();}
        var dialog=new Window{Title="关于与许可",Width=590,MaxHeight=560,SizeToContent=SizeToContent.Height,Owner=this,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=BackgroundBrush,Foreground=TextBrush};var p=new StackPanel{Margin=new Thickness(22)};p.Children.Add(new ScrollViewer{Content=T(text,12,TextBrush),MaxHeight=410,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});p.Children.Add(Button("关闭",()=>Motion.Close(dialog,null,preview),true));dialog.Content=p;Motion.Dialog(dialog,preview);dialog.ShowDialog();
    }
    void Tabs(string[] labels,int selected,Action<int> select){
        var row=new WrapPanel{Margin=new Thickness(0,0,0,10)};for(int i=0;i<labels.Length;i++){int index=i;var button=Button(labels[i],()=>select(index),i==selected);button.Padding=new Thickness(14,7,14,7);button.Margin=new Thickness(0,0,5,0);row.Children.Add(button);}pageNavigation.Children.Add(row);
    }
    void Stages(){
        var row=new WrapPanel{Margin=new Thickness(0,0,0,9)};var selected=wizard==0?0:wizard==1?1:2;var names=new[]{"1  配置","2  确认","3  执行与结果"};for(int i=0;i<names.Length;i++){var text=T(names[i],12,i==selected?MorandiTheme.OnPrimary:MutedBrush);text.Margin=new Thickness(0);row.Children.Add(new Border{Background=i==selected?MorandiTheme.Primary:Brushes.Transparent,Padding=new Thickness(15,7,15,7),CornerRadius=new CornerRadius(8),Margin=new Thickness(0,0,8,0),Child=text});}pageNavigation.Children.Add(row);
    }
    void Pair(StackPanel panel,string label,string value){var row=new Grid{Margin=new Thickness(0,4,0,4)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(125)});row.ColumnDefinitions.Add(new ColumnDefinition());row.Children.Add(T(label,12,MutedBrush));var text=T(value,13,TextBrush);Grid.SetColumn(text,1);row.Children.Add(text);panel.Children.Add(row);}
    void Overview(){
        subtitle.Text="查看当前配置与组件状态";
        var hero=Card("让 Codex 准备就绪","配置、修复与日常维护");
        var actions=new Grid();actions.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});actions.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});actions.ColumnDefinitions.Add(new ColumnDefinition());
        actions.Children.Add(Button("开始初始化",()=>{if(activeOperation!="initialize"&&wizard==2)ResetCliChoice();wizard=activeOperation=="initialize"?2:0;Navigate("初始化");},true));var refresh=Button("刷新状态",async()=>await Run("status","读取当前状态",false),false);Grid.SetColumn(refresh,1);actions.Children.Add(refresh);
        recoveryGuide=Button("遇到了未知错误？尝试切换官方独立CLI",GuideOfficialCli,false);recoveryGuide.Background=B("#833F4C");recoveryGuide.Foreground=Brushes.White;recoveryGuide.MaxWidth=205;recoveryGuide.Padding=new Thickness(10,6,10,6);recoveryGuide.Content=new TextBlock{Text="遇到了未知错误？尝试切换官方独立CLI",TextWrapping=TextWrapping.Wrap,FontSize=11};recoveryGuide.Visibility=Core.Value(status,"runtimeMode","")=="DesktopNative"?Visibility.Visible:Visibility.Collapsed;Grid.SetColumn(recoveryGuide,2);actions.Children.Add(recoveryGuide);hero.Children.Add(actions);
        hero.Children.Add(new TextBlock{Name="DataPreservationNotice",Text="本工具不会初始化数据与对话",FontSize=12,HorizontalAlignment=HorizontalAlignment.Left,TextWrapping=TextWrapping.NoWrap,Foreground=MutedBrush,Margin=new Thickness(0,8,0,0),ToolTip="初始化会备份并重建配置，保留现有对话记录和项目文件。"});
        var current=Card("当前环境",preview?"设计预览 · 以下为示例，不代表真实检测":status==null?"正在读取状态…":"配置与文件检查 · 当前运行实例未核验");
        var mode=Core.Value(status,"runtimeMode","");Pair(current,"已保存 CLI",status==null?"未检测":RuntimeMode(mode));Pair(current,"保存的 CLI 路径",Core.Value(status,"currentCliPath","").Length==0&&mode=="DesktopNative"?"由桌面应用自动选择":Core.Value(status,"currentCliPath","未检测"));Pair(current,"已保存模型",SavedValue("configModel")+" · "+EffortLabels.Display(SavedValue("configEffort")));Pair(current,"无项目任务目录",SavedValue("configWorkspace"));Pair(current,"已保存代理",SavedValue("configProxy"));var draft=new StackPanel();AddDraftSummary(draft);current.Children.Add(new Expander{Header="查看待应用设置",Content=draft});
        var quick=Card("常用操作","");Row(quick,Button("检查浏览器连接  →",()=>{repairTab=1;Navigate("连接与修复");},false),Button("查看备份与回滚  →",()=>Navigate("备份与回滚"),false));
    }
    string ProxyDisplay(){try{return ProxySummary();}catch(InvalidOperationException){return "端口尚未填写或无效，请在初始化中设置";}}
    void Wizard(){
        subtitle.Text=wizard==0?"先选择配置，再确认执行":wizard==1?"检查变更范围后开始初始化":"查看当前步骤与执行结果";Stages();
        if(wizard==0){
            Tabs(new[]{"工作区与网络","功能选择","模型设置"},configurationTab,index=>{configurationTab=index;Navigate("初始化");});
            RefreshRelayChoice();
            var network=Card("工作目录与网络","已有项目文件保留在原位置。");network.Children.Add(T("工作盘",12,MutedBrush));network.Children.Add(drive);network.Children.Add(T("网络代理",15,TextBrush));network.Children.Add(proxyEnabled);network.Children.Add(ProxyPortRow());network.Children.Add(proxyPortNotice);network.Children.Add(T("代理 IP（留空为 127.0.0.1）",12,MutedBrush));network.Children.Add(proxyHost);Row(network,remoteRelay,UninstallRelayButton());network.Children.Add(T("沿用上次选择。勾选后，初始化将申请管理员权限、安装自启服务并修改系统 hosts。使用系统解析访问 chatgpt.com 的程序将经过上方代理。取消勾选只跳过安装，已安装的服务可单独卸载。",11,MutedBrush));network.Children.Add(T("此代理也供当前 Windows 用户下其他支持代理变量的程序使用。重新打开程序后采用。",11,MutedBrush));network.Children.Add(T("自动沿用已有端口，也可选择软件代填；修改过端口时请选择自定义代理端口。",11,MutedBrush));if(configurationTab==0)WizardActions(Button("返回概览",()=>Navigate("概览"),false),Button("下一步：功能选择 →",()=>{Port();configurationTab=1;Navigate("初始化");},true));((Border)network.Parent).Visibility=configurationTab==0?Visibility.Visible:Visibility.Collapsed;
            var features=Card("选择要启用的功能","按用途选择，确认后统一执行。");FeatureGroups(features);features.Children.Add(T("初始化后使用的 CLI",13,TextBrush));Row(features,cliNative,cliStandalone);features.Children.Add(cliDetectionHint);if(configurationTab==1)WizardActions(Button("上一步",()=>{configurationTab=0;Navigate("初始化");},false),Button("下一步：模型设置 →",()=>{configurationTab=2;Navigate("初始化");},true));((Border)features.Parent).Visibility=configurationTab==1?Visibility.Visible:Visibility.Collapsed;
            var models=Card("模型设置","沿用当前全局设置，也可直接更换。");ModelSelectionRow(models,"父模型",parentModel,parentEffort);ModelSelectionRow(models,"子模型",childModel,childEffort);models.Children.Add(modelCheck);if(configurationTab==2)WizardActions(Button("检查所选配置",CheckModels,false),Button("上一步",()=>{configurationTab=1;Navigate("初始化");},false),Button("查看变更 →",()=>{try{Port();Drive();RequireCliChoice();Options();wizard=1;Navigate("初始化");}catch(Exception ex){Error(ex);}},true));((Border)models.Parent).Visibility=configurationTab==2?Visibility.Visible:Visibility.Collapsed;
        }else if(wizard==1){
            var p=Card("配置确认","先检查差异，首次实际修改前备份。");AddChangeSummary(p);Pair(p,"工作目录",Drive()+":\\Codex");Pair(p,"代理",ProxySummary());Pair(p,"远控转发",RelaySummary());Pair(p,"父模型",parentModel.SelectedItem+" / "+EffortLabels.Display(Convert.ToString(parentEffort.SelectedItem)));Pair(p,"子代理",subagents.IsChecked==true?Convert.ToString(childModel.SelectedItem):"关闭");Pair(p,"保存的 CLI 选择",CliChoiceSummary());
            var full=new Expander{Header="查看完整变更摘要",Content=T(InitSummary(),12,MutedBrush),Margin=new Thickness(0,9,0,9)};p.Children.Add(full);p.Children.Add(T("对话、记忆文件和项目文件保留。未管理的自定义规则保留，原文件同时进入备份。",12,MorandiTheme.Accent));WizardActions(Button("返回修改",()=>{if(!busy){wizard=0;Navigate("初始化");}},false),Button("确认并开始初始化",async()=>{try{RequireCliChoice();await BeginInitialization();}catch(Exception ex){Error(ex);}},true));
        }else ExecutionPage();
    }
    void ExecutionPage(){
        stageLabels.Clear();stageIndicators.Clear();var completed=!(busy&&activeOperation=="initialize");var p=Card(completed?initializationState=="partial"?"部分完成，有项目待处理":initializationState=="complete"?"初始化已完成":initializationState=="failed"?"初始化未完成":initializationState=="cancelled"?"已取消初始化":"初始化结果":"正在初始化",completed?"完成情况与后续操作见下方。":"处理中，请保留窗口。");
        var nextActions=new StackPanel();if(completed)p.Children.Add(nextActions);stageHeading=T("正在准备初始化…",16,TextBrush);stageCounter=T("",12,MutedBrush);stageElapsed=T("",11,MutedBrush);p.Children.Add(stageHeading);stageProgress=new ProgressBar{Height=9,Maximum=100,Style=(Style)Resources["KitProgress"],Margin=new Thickness(0,7,0,8)};p.Children.Add(stageProgress);p.Children.Add(stageCounter);p.Children.Add(stageElapsed);
        foreach(var stage in initializationProgress.Stages){var row=new Grid{Margin=new Thickness(0,4,0,4)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(23)});row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(100)});var icon=new ActivityIndicator{Width=18,Height=18,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,1,0,0),AllowMotion=!preview};var title=T(stage.Title,13,TextBrush);var state=T("待执行",12,MutedBrush);Grid.SetColumn(title,1);Grid.SetColumn(state,2);row.Children.Add(icon);row.Children.Add(title);row.Children.Add(state);p.Children.Add(row);stageIndicators[stage.Id]=icon;stageLabels[stage.Id]=new[]{title,state};if(completed&&(stage.State=="incomplete"||stage.State=="uncertain"))p.Children.Add(T(stage.Detail,12,MorandiTheme.Warning));}
        if(completed&&!String.IsNullOrWhiteSpace(initializationSummary))p.Children.Add(new Expander{Header="完整执行摘要",Content=T(initializationSummary,12,MutedBrush)});
        if(completed){if(initializationProgress.Effects.Count>0){var effectsPanel=new StackPanel();foreach(var effect in initializationProgress.Effects){effectsPanel.Children.Add(T(effect.name,14,TextBrush));effectsPanel.Children.Add(T(effect.Detail,12,MutedBrush));}p.Children.Add(new Expander{Header="本次变更何时生效",Content=effectsPanel,IsExpanded=true});}else if(initializationProgress.EffectsKnown)p.Children.Add(T(initializationState=="complete"?"本次没有记录到配置或组件变更，无需为本次检查重启。已有连接是否正常仍以实际验收为准。":"本次没有记录到已保存的变更；未完成项目和生效状态请查看步骤详情。",12,MutedBrush));}
        log.Height=230;var details=new Expander{Header="执行详情",IsExpanded=!logViewport.Following,Content=log,Margin=new Thickness(0,9,0,7)};p.Children.Add(details);details.Expanded+=(s,e)=>{Dispatcher.BeginInvoke(new Action(()=>logViewport.Attach()));};
        if(completed&&initializationState=="complete")nextActions.Children.Add(T("配置已保存。下次使用原来的 Codex 图标打开应用；各连接的运行状态按实际结果验收。",12,Accent));
        if(completed&&incompleteInitializationStages.Length>0&&lastInitializationOptions!=null)Row(nextActions,Button("仅重试未完成项",async()=>await RetryIncompleteInitialization(),true));
        var reset=Button("重新设置",()=>{if(!busy){ResetCliChoice();configurationTab=0;wizard=0;Navigate("初始化");}},false);reset.IsEnabled=!busy;Row(p,LatestLogButton(),Button("查看全部日志",()=>Navigate("日志"),false),reset);if(completed)Row(p,Button("打开备份目录",()=>{var path=Core.Value(status,"backupPath","");if(Directory.Exists(path))Open(path);else Navigate("备份与回滚");},false),Button("返回概览",()=>Navigate("概览"),true));UpdateStageView();
    }
    void UpdateStageView(){
        if(page!="初始化"||wizard!=2||stageProgress==null)return;
        var active=initializationProgress.Stages.FirstOrDefault(x=>x.State=="running");
        stageHeading.Text=active==null?(busy&&activeOperation=="initialize"?"正在准备下一步…":initializationState=="partial"?"部分项目未完成，可查看原因后单独处理":"执行已结束"):(active.Detail.Length>0?active.Detail:active.Title);
        stageCounter.Text="已处理 "+initializationProgress.Finished+" / "+initializationProgress.Stages.Count+" 项 · 已完成 "+initializationProgress.Complete+" 项";
        var elapsed=(initializationProgress.Ended??DateTime.UtcNow)-initializationProgress.Started;stageElapsed.Text="已用时 "+elapsed.ToString(@"mm\:ss");stageProgress.IsIndeterminate=busy&&activeOperation=="initialize"&&initializationProgress.Stages.Count==0;
        if(Math.Abs(stageProgress.Value-initializationProgress.Value)>.01){if(!preview&&SystemParameters.ClientAreaAnimation)stageProgress.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty,new DoubleAnimation(initializationProgress.Value,TimeSpan.FromMilliseconds(220)){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}},HandoffBehavior.SnapshotAndReplace);else stageProgress.Value=initializationProgress.Value;}
        foreach(var stage in initializationProgress.Stages){TextBlock[] parts;if(!stageLabels.TryGetValue(stage.Id,out parts))continue;stageIndicators[stage.Id].State=stage.State;parts[1].Text=stage.State=="complete"?"完成":stage.State=="uncertain"?"待核对":stage.State=="incomplete"?"未完成":stage.State=="running"?"正在处理":stage.State=="skipped"?"已跳过":"待执行";parts[1].Foreground=(stage.State=="incomplete"||stage.State=="uncertain")?MorandiTheme.Warning:MutedBrush;parts[0].ToolTip=stage.Detail;}
    }
    void RepairPage(){
        Tabs(new[]{"连接状态","浏览器","电脑操作","桌面启动","配置检查"},repairTab,index=>{repairTab=index;Navigate("连接与修复");});
        if(repairTab==0){ConnectionOverview();return;}
        if(repairTab==1){BrowserConnection();return;}
        subtitle.Text="先检查，再按需修复。修改前会说明影响范围。";
        if(repairTab==0){var p=Card("诊断助手","检查 CLI 与桌面组件，给出后续建议。");p.Children.Add(T(diagnosticAdvice.Length>0?diagnosticAdvice:"诊断只读取状态，不自动执行修复。",13,MutedBrush));Row(p,Button("开始诊断",async()=>await Run("doctor","诊断助手",false),true),Button("查看详细日志",()=>Navigate("日志"),false));}
        else if(repairTab==2){var p=Card("电脑操作组件（CUA）","对照当前官方桌面版，检查浏览器自动化组件。");Row(p,Button("检查 CUA",async()=>await Run("cua-check","CUA 组件",false),true),Button("检查并修复",async()=>await Run("cua-repair","CUA 修复",true),false));p.Children.Add(T("按需备份并修复；不会重置模型或工作目录。",12,MutedBrush));}
        else if(repairTab==3){var p=Card("桌面启动修复","针对 EPERM / rename_staging 等组件复制问题。");p.Children.Add(T(desktopCoreAdvice.Length>0?desktopCoreAdvice:"先检查近期日志；修复前请保存工作并退出 Codex。",13,MutedBrush));Row(p,Button("检查启动问题",async()=>await Run("desktop-core-check","桌面启动检查",false),true),Button("修复桌面启动组件",async()=>await Run("desktop-core-repair","桌面启动修复",true),false));}
        else{var p=Card("配置兼容检查","先备份，再定向清理已失效的配置项。");p.Children.Add(T("项目目录（可选）",12,MutedBrush));var input=new TextBox{Text=compatibilityProjectPath,Style=(Style)Resources["KitInput"]};input.TextChanged+=(s,e)=>compatibilityProjectPath=input.Text;p.Children.Add(input);Row(p,Button("检查配置",async()=>await Run("config-check","配置兼容检查",false),true),Button("清理已废弃配置",async()=>await Run("config-cleanup","定向清理配置",true),false));p.Children.Add(T(compatibilityAdvice.Length>0?compatibilityAdvice:"会话参数警告需结合桌面版与 CLI 版本核对。",12,MutedBrush));}
    }
    void CliPage(){
        subtitle.Text="查看保存的 CLI 选择并维护组件；此页不代表当前运行实例。";var mode=Core.Value(status,"runtimeMode","");var grid=new Grid{Margin=new Thickness(0,0,0,11)};grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition());
        var native=new StackPanel{Margin=new Thickness(15)};native.Children.Add(T("桌面版原生 CLI",16,TextBrush));native.Children.Add(T(mode=="DesktopNative"?"✓ 已保存选择":"随桌面版一起更新",12,MorandiTheme.Accent));native.Children.Add(T("桌面版本："+Core.Value(status,"desktopVersion","未检测"),12,MutedBrush));
        var standalone=new StackPanel{Margin=new Thickness(15)};standalone.Children.Add(T("官方独立 CLI",16,TextBrush));standalone.Children.Add(T(mode=="OfficialStandalone"?"✓ 已保存选择":"独立准备与更新",12,MorandiTheme.Accent));standalone.Children.Add(T(Core.Value(status,"currentCliPath","尚未检测路径"),11,MutedBrush));
        var op=mode=="DesktopNative"?"guard-current":mode=="OfficialStandalone"?"guard-native":"status";var label=mode=="DesktopNative"?"切换官方独立 CLI":mode=="OfficialStandalone"?"切换桌面原生 CLI":"重新检测 CLI";officialSwitch=Button(label,async()=>await Run(op,label,op!="status"),true);if(mode=="DesktopNative")standalone.Children.Add(officialSwitch);else native.Children.Add(officialSwitch);
        var left=Surface(native);left.Margin=new Thickness(0,0,8,0);grid.Children.Add(left);var right=Surface(standalone);Grid.SetColumn(right,1);grid.Children.Add(right);content.Children.Add(grid);
        var updatePanel=Card("组件更新","更新组件与切换模式分别执行。");Row(updatePanel,Button("更新官方独立 CLI",async()=>await Run("official-cli-update","更新官方独立 CLI",true),true),Button("更新桌面版（含原生 CLI）",async()=>await Run("desktop-update","更新桌面版",true),false));
        var protect=Card("启动保护",Core.Value(status,"protectionInstalled","")=="True"?"已安装 · 登录时自动检查与维护":"未安装，或尚未读取状态");Row(protect,Button("安装 / 修复保护",async()=>await Run("guard-install","安装启动保护",true),true),Button("诊断助手",async()=>{repairTab=0;Navigate("连接与修复");await Run("doctor","诊断助手",false);},false),Button("卸载启动保护",async()=>await Run("guard-uninstall","卸载启动保护",true),false));protect.Children.Add(T("切换前请保存工作并退出 Codex。",12,MutedBrush));
    }
    Window DialogWindow(string title,double width){
        var dialog=new Window{Title=title,Width=width,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStyle=WindowStyle.None,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false,Background=BackgroundBrush,Foreground=TextBrush,FontFamily=FontFamily};
        LimitDialog(dialog);dialog.Resources.MergedDictionaries.Add(Resources);if(IsVisible)dialog.Owner=this;
        Motion.Dialog(dialog,preview);return dialog;
    }
    Window CreateMessageDialog(string title,string message,out TextBlock messageText,out WrapPanel actions){
        var area=AvailableDialogArea();var dialog=DialogWindow(title,540);
        var panel=new Grid{Margin=new Thickness(20)};panel.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});panel.RowDefinitions.Add(new RowDefinition{Height=new GridLength(1,GridUnitType.Star)});panel.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});var heading=T(title,20,TextBrush);panel.Children.Add(heading);
        messageText=T(message,13,MutedBrush);
        var body=new SmoothScrollViewer{Content=messageText,MaxHeight=Math.Max(70,Math.Min(380,area.Height-180)),VerticalScrollBarVisibility=ScrollBarVisibility.Auto};Grid.SetRow(body,1);panel.Children.Add(body);
        actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
        Grid.SetRow(actions,2);panel.Children.Add(actions);dialog.Content=Surface(panel);return dialog;
    }
    Window CreateActionDialog(string title,string message,bool confirmation){
        TextBlock text;WrapPanel actions;var dialog=CreateMessageDialog(title,message,out text,out actions);
        if(confirmation){var cancel=Button("取消",()=>Motion.Close(dialog,false,preview),false);cancel.IsDefault=true;cancel.IsCancel=true;actions.Children.Add(cancel);}
        var accept=Button(confirmation?"确认并继续":"知道了",()=>Motion.Close(dialog,true,preview),true);accept.IsDefault=!confirmation;actions.Children.Add(accept);return dialog;
    }
    Window CreatePreflightDialog(){
        const string title="正在准备初始化";
        var dialog=DialogWindow(title,450);
        var panel=new StackPanel{Margin=new Thickness(24)};
        var header=new DockPanel();
        header.Children.Add(new ActivityIndicator{State="running",Width=30,Height=30,Margin=new Thickness(0,0,12,0)});
        var heading=T(title,20,TextBrush);heading.FontWeight=FontWeights.SemiBold;heading.VerticalAlignment=VerticalAlignment.Center;heading.Margin=new Thickness(0);
        header.Children.Add(heading);panel.Children.Add(header);
        var note=T("正在检查 Codex 是否运行，以及本次选择的设置。检查完成后会自动继续。",14,MutedBrush);
        note.Margin=new Thickness(0,14,0,0);panel.Children.Add(note);
        dialog.Content=Surface(panel);return dialog;
    }
    DockPanel IconHeading(string text,int size,Brush color,bool strong){
        var row=new DockPanel{Margin=new Thickness(0,0,0,8)};var icon=new System.Windows.Shapes.Path{Data=UiIcons.For(text,true),Stroke=Accent,StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Width=size,Height=size,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center};row.Children.Add(icon);var label=T(text,size,color);label.Margin=new Thickness(0);if(strong)label.FontWeight=FontWeights.SemiBold;row.Children.Add(label);return row;
    }
    static Border Surface(UIElement child){return new Border{Child=child,Background=MorandiTheme.Panel,BorderBrush=MorandiTheme.Border,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12)};}
}
}
