using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using Ellipse = System.Windows.Shapes.Ellipse;
using System.Windows.Media.Effects;
using System.Windows.Media.Animation;

namespace CodexKit {
public partial class MainWindow : Window {
    static readonly Brush BackgroundBrush=B("#10141E"), PanelBrush=MorandiTheme.Panel, TextBrush=B("#EDF2FC"), MutedBrush=B("#A4B0C5"), Accent=MorandiTheme.Accent;
    readonly StackPanel content=new StackPanel();
    readonly TextBlock heading=new TextBlock(), subtitle=new TextBlock(), banner=new TextBlock();
    readonly TextBlock updateStatus=new TextBlock();
    Button updateNotice;
    readonly TextBox log=new TextBox();
    readonly LogViewport logViewport;
    ListBox rollbackList;
    Button rollbackButton,rollbackFolderButton,rollbackDeleteButton;
    string selectedRollbackId="";
    readonly ProgressBar progress=new ProgressBar();
    readonly List<Button> navigation=new List<Button>();
    readonly ComboBox drive=new ComboBox();
    readonly TextBox port=new TextBox();
    readonly ComboBox proxyPortPreset=new ComboBox();
    readonly TextBlock proxyPortNotice=new TextBlock();
    string customProxyPort="";
    readonly TextBox proxyHost=new TextBox();
    readonly CheckBox guard=new CheckBox();
    readonly CheckBox proxyEnabled=new CheckBox(),folderManagement=new CheckBox(),subagents=new CheckBox(),cuaRepair=new CheckBox();
    readonly CheckBox browserRepair=new CheckBox(),officialCliUpdate=new CheckBox(),desktopUpdate=new CheckBox();
    readonly CheckBox antiOverDefense=new CheckBox(),workJournal=new CheckBox(),commandLineFirst=new CheckBox(),memories=new CheckBox(),githubMirrors=new CheckBox();
    readonly RadioButton cliNative=new RadioButton(),cliStandalone=new RadioButton();
    readonly TextBlock cliDetectionHint=new TextBlock();
    readonly TextBox githubCustomMirror=new TextBox(),githubLogView=new TextBox();
    string githubLogText="",networkSettingsError="",modelSettingsError="";
    Dictionary<string,object> browserReport,browserStartTarget;
    string initializationSummary="";
    Button recoveryGuide,officialSwitch;
    readonly ComboBox parentModel=new ComboBox(),childModel=new ComboBox();
    readonly ComboBox parentEffort=new ComboBox(),childEffort=new ComboBox();
    readonly TextBlock modelCheck=new TextBlock();
    ModelCatalog modelCatalog;
    Dictionary<string,object> status, update,diagnosisContext;
    string diagnosticAdvice="";
    string compatibilityAdvice="", compatibilityProjectPath="";
    string desktopCoreAdvice="";
    string page="概览", lastLog="", operationTitle="", activeOperation="";
    string rawLogText="", rawDiagnosticText="", chineseLogText="";
    readonly Dictionary<string,string> localizedLines=new Dictionary<string,string>(StringComparer.Ordinal);
    bool showRawLog;
    bool logInputComplete,renderedComplete;
    bool busy, preview, checkingUpdate, cliChoiceManual, applyingCliDetection, cliDetectionComplete;
    string detectedCliMode="";
    int wizard;
    internal Func<string,string,string,int,bool,Dictionary<string,object>,Process> StartOperation=Core.StartWorker;
    internal Func<string,string> OperationLogPath=id=>Path.Combine(Core.Logs,id+".log");
    internal Action<Window> PresentInitializationDialog;
    internal Func<Task<Dictionary<string,object>>> InitializationPreflight;
    internal Func<Window,bool?> PresentAppCloseDialog;
    public void VerifyPages() {
        if(proxyEnabled.IsChecked!=true||folderManagement.IsChecked!=true||subagents.IsChecked==true||guard.IsChecked!=true||cuaRepair.IsChecked!=true||browserRepair.IsChecked!=true||officialCliUpdate.IsChecked!=true||desktopUpdate.IsChecked!=true||Port()!=10808)throw new InvalidOperationException("Default setup options changed");
        if(proxyHost.Text.Length!=0||ProxyHost()!=Core.DefaultProxyHost)throw new InvalidOperationException("Default proxy host changed");
        proxyHost.Text="192.168.1.20";var settings=Options();if(Convert.ToString(settings["proxyHost"])!="192.168.1.20")throw new InvalidOperationException("Custom proxy host was not preserved");
        proxyHost.Text="not-an-ip";var rejected=false;try{Options();}catch{rejected=true;}if(!rejected)throw new InvalidOperationException("Unsafe proxy host accepted");
        proxyEnabled.IsChecked=false;port.Text="invalid";proxyHost.Text="not-an-ip";settings=Options();if(Port()!=0||port.IsEnabled||proxyHost.IsEnabled||Convert.ToString(settings["proxyHost"])!="")throw new InvalidOperationException("Disabled proxy did not ignore its fields");proxyEnabled.IsChecked=true;port.Text="10808";proxyHost.Text="";
        var defaults=Options();if(Convert.ToString(defaults["parentModel"])!="gpt-6.1-sol"||Convert.ToString(defaults["parentEffort"])!="medium"||!parentModel.IsEnabled||!parentEffort.IsEnabled||childModel.IsEnabled||childEffort.IsEnabled)throw new InvalidOperationException("Default parent model must be directly editable while subagents stay disabled");
        if(!parentModel.IsEnabled||!parentEffort.IsEnabled||childModel.IsEnabled||childEffort.IsEnabled)throw new InvalidOperationException("Disabled subagents allowed child selection");
        subagents.IsChecked=true;if(!childModel.IsEnabled||!childEffort.IsEnabled)throw new InvalidOperationException("Enabled subagents did not allow child selection");parentModel.SelectedItem="gpt-6-astra";childModel.SelectedItem="gpt-6-luna";parentEffort.SelectedItem="ultra";childEffort.SelectedItem="high";settings=Options();if(Convert.ToString(settings["parentModel"])!="gpt-6-astra"||Convert.ToString(settings["childModel"])!="gpt-6-luna"||Convert.ToString(settings["parentEffort"])!="ultra"||Convert.ToString(settings["childEffort"])!="high"||parentModel.IsEditable||childModel.IsEditable||!InitSummary().Contains("gpt-6-luna / 高（high）"))throw new InvalidOperationException("Preset model selection failed");
        if(parentModel.Items.Count!=9||childModel.Items.Count!=9||childEffort.Items.Contains("ultra"))throw new InvalidOperationException("Preset dropdown choices were lost");
        childModel.SelectedItem="gpt-5.5";if(!childEffort.Items.Cast<string>().SequenceEqual(new[]{"low","medium","high","xhigh"}))throw new InvalidOperationException("Effort choices did not follow model selection");
        childEffort.ItemsSource=new[]{"ultra"};childEffort.SelectedItem="ultra";rejected=false;try{Options();}catch{rejected=true;}if(!rejected)throw new InvalidOperationException("Unsupported UI effort accepted");RefreshEfforts(childEffort,"gpt-5.5","high");
        parentModel.SelectedItem=null;parentModel.Text="vendor/custom";rejected=false;try{Options();}catch{rejected=true;}if(!rejected)throw new InvalidOperationException("Unlisted model accepted");parentModel.SelectedItem="gpt-6.1-sol";
        folderManagement.IsChecked=false;subagents.IsChecked=false;cuaRepair.IsChecked=false;guard.IsChecked=false;settings=Options();if((bool)settings["folderManagement"]||(bool)settings["subagents"]||(bool)settings["cuaRepair"]||childModel.IsEnabled||childEffort.IsEnabled||!parentModel.IsEnabled||!parentEffort.IsEnabled||Convert.ToString(settings["childEffort"])!="high")throw new InvalidOperationException("Optional setup switches failed");
        subagents.IsChecked=true;if(!childModel.IsEnabled||!childEffort.IsEnabled||Convert.ToString(childModel.SelectedItem)!="gpt-5.5")throw new InvalidOperationException("Re-enabling subagents lost custom selection");
        parentModel.SelectedItem="gpt-6.1-sol";childModel.SelectedItem="gpt-6.1-sol";parentEffort.SelectedItem="medium";childEffort.SelectedItem="medium";folderManagement.IsChecked=true;subagents.IsChecked=false;cuaRepair.IsChecked=true;guard.IsChecked=true;proxyHost.Text="";
        var completionDialog=CreateInitializationCompletedDialog();var completionRoot=completionDialog.Content as Border;var completionPanel=completionRoot==null?null:completionRoot.Child as StackPanel;if((IsVisible&&completionDialog.Owner!=this)||completionDialog.Title!="已完成初始化"||completionPanel==null||completionPanel.Children.OfType<Button>().Count()!=1||!completionPanel.Children.OfType<TextBlock>().Any(x=>x.Text=="已完成初始化"))throw new InvalidOperationException("Initialization completion dialog is not themed or single-action");
        status=new Dictionary<string,object>{{"runtimeMode","DesktopNative"},{"runtimeModeDetail","桌面应用自带 CLI"},{"currentCliPath",""},{"protectionInstalled",false}};Navigate("概览");UpdateLayout();VerifyRuntimeDisplay("DesktopNative","由桌面应用自动选择");status["runtimeMode"]="OfficialStandalone";status["runtimeModeDetail"]="官方独立 CLI";status["currentCliPath"]="C:\\Users\\Test\\.codex\\bin\\codex.exe";Navigate("概览");UpdateLayout();VerifyRuntimeDisplay("OfficialStandalone","C:\\Users\\Test\\.codex\\bin\\codex.exe");status["runtimeMode"]="Unknown";Navigate("概览");UpdateLayout();VerifyRuntimeDisplay("Unknown","C:\\Users\\Test\\.codex\\bin\\codex.exe");status=null;
        foreach(var mode in new[]{"DesktopNative","OfficialStandalone","Unknown",""}){status=new Dictionary<string,object>{{"runtimeMode",mode}};Navigate("概览");if((recoveryGuide.Visibility==Visibility.Visible)!=(mode=="DesktopNative"))throw new InvalidOperationException("Recovery guide visibility failed");}
        status=new Dictionary<string,object>{{"runtimeMode","DesktopNative"}};GuideOfficialCli();HighlightOfficialSwitch();if(page!="CLI 管理"||Convert.ToString(officialSwitch.Content)!="切换官方独立 CLI"||busy||officialSwitch.Background!=Accent)throw new InvalidOperationException("Recovery navigation or reduced-motion highlight failed");
        var newDefaults=Options();foreach(var key in new[]{"browserRepair","officialCliUpdate","desktopUpdate","antiOverDefense","workJournal","commandLineFirst"})if(!(bool)newDefaults[key])throw new InvalidOperationException("New default disabled: "+key);
        antiOverDefense.IsChecked=false;if((bool)Options()["antiOverDefense"]||!InitSummary().Contains("防止过度防御：跳过"))throw new InvalidOperationException("Anti-over-defense selection or confirmation was not preserved");antiOverDefense.IsChecked=true;if(!InitSummary().Contains("HERO 中文完整规则"))throw new InvalidOperationException("Anti-over-defense confirmation was missing");
        workJournal.IsChecked=false;if((bool)Options()["workJournal"]||!InitSummary().Contains("按需笔记与文档管理：跳过"))throw new InvalidOperationException("Work journal selection or confirmation was not preserved");workJournal.IsChecked=true;
        commandLineFirst.IsChecked=false;
        if((bool)Options()["commandLineFirst"]||!(bool)Options()["workJournal"]||!InitSummary().Contains("优先使用命令行：跳过"))
            throw new InvalidOperationException("Command-line preference was not independent or missing from confirmation");
        commandLineFirst.IsChecked=true;
        Navigate("初始化");var featurePanel=(StackPanel)((Border)content.Children[1]).Child;var featureChoices=InitializationFlowTests.Descendants<CheckBox>(featurePanel).ToArray();if(featureChoices[0]!=folderManagement||featureChoices[1]!=antiOverDefense||featureChoices[2]!=workJournal||featureChoices[3]!=commandLineFirst||featureChoices[4]!=memories||workJournal.IsChecked!=true||commandLineFirst.IsChecked!=true||memories.IsChecked!=true||featureChoices[featureChoices.Length-2]!=desktopUpdate||featureChoices.Last()!=subagents||antiOverDefense.IsChecked!=true||subagents.IsChecked!=false)throw new InvalidOperationException("Initialization checkbox order or defaults changed");
        var cliChoices=featurePanel.Children.OfType<WrapPanel>().SelectMany(x=>x.Children.OfType<RadioButton>()).ToArray();if(!cliChoices.SequenceEqual(new[]{cliNative,cliStandalone})||cliNative.IsChecked!=true||cliStandalone.IsChecked==true||Convert.ToString(Options()["cliMode"])!="auto"||!InitSummary().Contains("初始化后使用的 CLI：桌面版原生 CLI"))throw new InvalidDataException("Detected native CLI choice or confirmation is incorrect");
        ApplyCliDetection(new Dictionary<string,object>{{"runtimeMode","OfficialStandalone"}});if(cliStandalone.IsChecked!=true||cliNative.IsChecked==true||Convert.ToString(Options()["cliMode"])!="auto"||!InitSummary().Contains("初始化后使用的 CLI：官方独立 CLI"))throw new InvalidDataException("Detected standalone CLI was not selected automatically");
        cliNative.IsChecked=true;ApplyCliDetection(new Dictionary<string,object>{{"runtimeMode","OfficialStandalone"}});if(cliNative.IsChecked!=true||Convert.ToString(Options()["cliMode"])!="desktop-native")throw new InvalidDataException("Status refresh replaced the manual CLI choice");Navigate("概览");Navigate("初始化");if(cliNative.IsChecked!=true)throw new InvalidDataException("Navigation lost the selected CLI");
        cliChoiceManual=false;ApplyCliDetection(new Dictionary<string,object>{{"runtimeMode","Unknown"}});rejected=false;try{RequireCliChoice();}catch(InvalidOperationException){rejected=true;}if(!rejected||cliNative.IsChecked==true||cliStandalone.IsChecked==true)throw new InvalidDataException("Unknown CLI mode silently selected a default");cliStandalone.IsChecked=true;RequireCliChoice();if(Convert.ToString(Options()["cliMode"])!="official-standalone")throw new InvalidDataException("Manual selection after unknown detection failed");ResetCliChoice();
        Navigate("连接与修复");var browserSettings=(StackPanel)((Border)content.Children[1]).Child;var browserButtons=browserSettings.Children.OfType<WrapPanel>().SelectMany(x=>x.Children.OfType<Button>()).Select(x=>Convert.ToString(x.Content)).ToArray();if(!browserButtons.Contains("修复并启动 Codex")||!browserSettings.IsAncestorOf(port)||!browserSettings.IsAncestorOf(proxyPortPreset))throw new InvalidOperationException("Browser startup entry or proxy inputs are missing");proxyHost.Text="192.168.5.8";proxyPortPreset.SelectedIndex=5;if(ProxyHost()!="192.168.5.8"||Port()!=7890||!port.IsReadOnly)throw new InvalidOperationException("Browser proxy settings did not synchronize");proxyEnabled.IsChecked=false;if(port.IsEnabled||proxyPortPreset.IsEnabled)throw new InvalidOperationException("Browser proxy switch did not disable its inputs");proxyEnabled.IsChecked=true;proxyHost.Text="";proxyPortPreset.SelectedIndex=0;
        VerifyMaintenanceModes();
        memories.IsChecked=false;if((bool)Options()["memories"]||!InitSummary().Contains("关闭，保留已有记忆文件"))throw new InvalidDataException("Memory selection was not preserved");memories.IsChecked=true;
        if(!githubMirrors.IsChecked.Value||NetworkOptions().customGithubMirror.Length!=0)throw new InvalidDataException("GitHub mirror defaults changed");
        githubCustomMirror.Text="invalid mirror";githubMirrors.IsChecked=false;if(NetworkOptions().customGithubMirror.Length!=0||githubCustomMirror.IsEnabled)throw new InvalidDataException("Disabled mirrors validated an ignored prefix");githubCustomMirror.Text="";githubMirrors.IsChecked=true;
        Navigate("更新与下载");
        var mirrorNotes=((StackPanel)((Border)content.Children[1]).Child).Children.OfType<TextBlock>().ToArray();
        if(!mirrorNotes.Any(note=>note.Text=="顺序：官方 → 自定义 → 内置 10 个镜像")||
           !mirrorNotes.Any(note=>note.FontSize==12&&note.Text=="内置站点："+GitHubTransfer.BuiltInMirrorNames)||
           GitHubTransfer.BuiltInMirrorNames.Split('、').Length!=10)throw new InvalidDataException("GitHub mirror order or built-in site note is missing");
        parentEffort.ItemTemplate.Seal();var effortLabel=(TextBlock)parentEffort.ItemTemplate.LoadContent();effortLabel.DataContext="medium";effortLabel.Measure(new Size(200,40));effortLabel.Arrange(new Rect(0,0,200,40));effortLabel.UpdateLayout();effortLabel.GetBindingExpression(TextBlock.TextProperty).UpdateTarget();if(effortLabel.Text!="中（medium）"||Convert.ToString(Options()["parentEffort"])!="medium"||!InitSummary().Contains("gpt-6.1-sol / 中（medium）"))throw new InvalidDataException("Effort labels changed the underlying config value");
        foreach(var effort in new[]{"none","minimal","low","medium","high","xhigh","max","ultra"})if(!EffortLabels.Display(effort).EndsWith("（"+effort+"）"))throw new InvalidDataException("Effort label did not include its original identifier: "+effort);
        VerifyChineseLogs();
        VerifyLogScrolling();
        VerifyRollbackSelection();
        foreach(var name in new[]{"概览","初始化","连接与修复","CLI 管理","更新与下载","日志","初始化"}) { Navigate(name); Measure(new Size(1120,800)); Arrange(new Rect(0,0,1120,800)); UpdateLayout(); }
        wizard=1;Navigate("初始化");UpdateLayout(); wizard=2;Navigate("初始化");UpdateLayout();Navigate("日志");UpdateLayout();
        update=new Dictionary<string,object>{{"version",Core.Version}};ApplyUpdateState();if(updateNotice.Visibility!=Visibility.Collapsed)throw new InvalidOperationException("Update notice shown for current version");
        var current=new Version(Core.Version);update["version"]=new Version(current.Major,current.Minor,current.Build+1).ToString();ApplyUpdateState();Navigate("概览");UpdateLayout();if(updateNotice.Visibility!=Visibility.Visible||!updateStatus.Text.StartsWith("检测到更新"))throw new InvalidOperationException("Update notice missing");update=null;ApplyUpdateState();
        ApplyCurrentModelSettings(new CurrentModelSettings{ParentModel="gpt-6-astra",ParentEffort="max",ChildModel="gpt-6-luna",ChildEffort="high"});settings=Options();
        if((bool)settings["subagents"]||Convert.ToString(settings["childModel"])!="gpt-6-luna"||Convert.ToString(settings["childEffort"])!="high"||!InitSummary().Contains("保留模型设置 gpt-6-luna / 高（high）"))throw new InvalidDataException("Disabled subagents lost inherited model preferences");
        subagents.IsChecked=true;settings=Options();
        if(Convert.ToString(settings["parentModel"])!="gpt-6-astra"||Convert.ToString(settings["parentEffort"])!="max"||Convert.ToString(settings["childModel"])!="gpt-6-luna"||Convert.ToString(settings["childEffort"])!="high")throw new InvalidDataException("Current model selections were not applied");
        parentModel.SelectedItem="gpt-6.1-terra";parentEffort.SelectedItem="ultra";subagents.IsChecked=false;subagents.IsChecked=true;Navigate("概览");Navigate("初始化");CheckModels();settings=Options();
        if(Convert.ToString(settings["parentModel"])!="gpt-6.1-terra"||Convert.ToString(settings["parentEffort"])!="ultra"||Convert.ToString(settings["childModel"])!="gpt-6-luna"||Convert.ToString(settings["childEffort"])!="high")throw new InvalidDataException("Navigation or model check changed a user selection");
        ApplyCurrentModelSettings(new CurrentModelSettings{ParentModel="future/model",ParentEffort="high",ChildModel="gpt-5.5",ChildEffort="max"});CheckModels();settings=Options();
        if(Convert.ToString(settings["parentModel"])!="future/model"||Convert.ToString(settings["childEffort"])!="max"||!modelCheck.Text.Contains("尚未验证"))throw new InvalidDataException("An inherited model or effort was not retained");
    }
    public void RenderPages(string directory){
        Directory.CreateDirectory(directory);Width=900;Height=660;
        status=new Dictionary<string,object>{{"runtimeMode","DesktopNative"},{"desktopVersion","26.1103.7773.0"},{"protectionInstalled",true}};
        foreach(var name in new[]{"概览","连接与修复","CLI 管理","备份与回滚","更新与下载","日志"}){if(name=="备份与回滚"){status["rollbackEntries"]=new object[]{new Dictionary<string,object>{{"id","preview-config"},{"kind","init"},{"version","2.2.12"},{"time","2026-11-12 16:33"},{"detail","配置、代理与全局规则"},{"available",true},{"path",@"C:\Users\示例\Documents\Codex初始化备份"}},new Dictionary<string,object>{{"id","preview-cli"},{"kind","cli"},{"version","0.167.0"},{"time","2026-11-10 16:46"},{"detail","官方独立 CLI 缓存"},{"available",true},{"path",@"C:\Users\示例\.codex\packages\standalone"}}};selectedRollbackId="preview-config";status["rollbackDeleteId"]="preview-history";status["rollbackDeleteCount"]=2;}Navigate(name);RenderView(directory,name,900,660);}
        wizard=0;for(int index=0;index<3;index++){configurationTab=index;Navigate("初始化");RenderView(directory,"初始化-"+new[]{"工作区与网络","功能选择","模型设置"}[index],900,660);}
        wizard=1;Navigate("初始化");RenderView(directory,"初始化-确认",900,660);
        initializationProgress.Reset(Options(),true);foreach(var stage in initializationProgress.Stages.Take(3))stage.State="complete";initializationProgress.Stages[3].State="running";initializationProgress.Stages[3].Detail="正在准备官方独立 CLI";busy=true;wizard=2;Navigate("初始化");RenderView(directory,"初始化-执行",900,660);
        busy=false;initializationState="partial";foreach(var stage in initializationProgress.Stages){stage.State="complete";}initializationProgress.Stages[1].State="incomplete";initializationSummary="桌面版更新未完成，可稍后在微软商店或 Codex 内手动更新。其余所选项目已完成。";Navigate("初始化");RenderView(directory,"初始化-结果",900,660);
        wizard=0;configurationTab=0;Navigate("初始化");RenderView(directory,"最小窗口",800,540);RenderView(directory,"双倍缩放",900,660,192);
        var modal=CreateActionDialog("确认修改", "工具将先备份当前配置，再执行本次选择的操作。对话与项目文件保留。请核对目标后继续。",true);var modalView=(FrameworkElement)modal.Content;modalView.Measure(new Size(520,Double.PositiveInfinity));modalView.Arrange(new Rect(new Point(),modalView.DesiredSize));modalView.UpdateLayout();var modalBitmap=new RenderTargetBitmap(520,(int)Math.Ceiling(modalView.ActualHeight),96,96,PixelFormats.Pbgra32);modalBitmap.Render(modalView);var modalEncoder=new PngBitmapEncoder();modalEncoder.Frames.Add(BitmapFrame.Create(modalBitmap));using(var file=File.Create(Path.Combine(directory,"操作确认弹窗.png")))modalEncoder.Save(file);
        status=null;busy=false;wizard=0;
    }
    void RenderView(string directory,string name,int width,int height,double dpi=96){
        var view=(FrameworkElement)Content;view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();PumpLogLayout();
        var bitmap=new RenderTargetBitmap((int)(width*dpi/96),(int)(height*dpi/96),dpi,dpi,PixelFormats.Pbgra32);bitmap.Render(view);File.WriteAllText(Path.Combine(directory,name+".layout.json"),Core.Json.Serialize(new {width=view.ActualWidth,height=view.ActualHeight,desiredWidth=view.DesiredSize.Width,desiredHeight=view.DesiredSize.Height,windowWidth=ActualWidth,windowHeight=ActualHeight,contentWidth=content.ActualWidth,contentHeight=content.ActualHeight}));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(directory,name+".png")))encoder.Save(file);
    }
    public MainWindow(bool isPreview) {
        logViewport=new LogViewport(log);
        preview=isPreview;if(preview)StartOperation=(op,id,disk,proxy,protect,options)=>{throw new InvalidOperationException("设计预览不会执行初始化或维护操作。");};modelCatalog=ModelCatalog.Preset(); Title="Codex Init Kit"; Width=1120; Height=800; MinWidth=940; MinHeight=720; WindowStartupLocation=WindowStartupLocation.CenterScreen;
        SourceInitialized+=(s,e)=>MorandiTheme.StyleWindow(this);
        Icon=Brand.Logo();
        Background=BackgroundBrush; Foreground=TextBrush; FontFamily=new FontFamily("Microsoft YaHei UI"); FontSize=14;
        using(var styles=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("UI.Controls.xaml"))Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Load(styles));
        BuildShell();
        log.IsReadOnly=true; log.AcceptsReturn=true; log.TextWrapping=TextWrapping.NoWrap; log.VerticalScrollBarVisibility=ScrollBarVisibility.Auto; log.HorizontalScrollBarVisibility=ScrollBarVisibility.Auto; log.FontFamily=new FontFamily("Consolas"); log.FontSize=12; log.Background=B("#101620"); log.Foreground=TextBrush; log.BorderThickness=new Thickness(0); log.Padding=new Thickness(14); log.Height=350;
        drive.ItemsSource=DriveInfo.GetDrives().Where(x=>x.DriveType==DriveType.Fixed && x.IsReady).Select(x=>x.Name.Substring(0,1)).ToArray(); drive.SelectedItem="D"; if(drive.SelectedIndex<0) drive.SelectedIndex=0;
        drive.Style=(Style)Resources["KitCombo"];drive.FontSize=13;drive.Width=280;drive.HorizontalAlignment=HorizontalAlignment.Left;
        foreach(var input in new[]{port,proxyHost})input.Style=(Style)Resources["KitInput"];
        port.Width=90;port.Height=36;port.MaxLength=5;port.HorizontalAlignment=HorizontalAlignment.Left;
        proxyPortPreset.Style=(Style)Resources["KitCombo"];proxyPortPreset.Width=205;
        proxyPortPreset.ItemsSource=ProxyPortSettings.Presets;
        proxyPortNotice.Foreground=B("#E9C779");proxyPortNotice.FontSize=12;proxyPortNotice.TextWrapping=TextWrapping.Wrap;proxyPortNotice.Margin=new Thickness(0,0,0,8);proxyPortNotice.Visibility=Visibility.Collapsed;
        proxyPortPreset.SelectionChanged+=(s,e)=>{
            var selected=(ProxyPortPreset)proxyPortPreset.SelectedItem;
            port.IsReadOnly=selected.Port!=0;
            port.Text=selected.Port==0?customProxyPort:selected.Port.ToString();
            port.Background=port.IsReadOnly?B("#202938"):B("#101620");port.Foreground=port.IsReadOnly?MutedBrush:TextBrush;
            proxyPortNotice.Visibility=Visibility.Collapsed;
        };
        port.TextChanged+=(s,e)=>{if(!port.IsReadOnly){customProxyPort=port.Text;proxyPortNotice.Visibility=Visibility.Collapsed;}};
        proxyPortPreset.SelectedIndex=0;
        proxyHost.Text="";proxyHost.Width=280;proxyHost.HorizontalAlignment=HorizontalAlignment.Left;proxyHost.ToolTip="留空使用 127.0.0.1。软路由在局域网其他设备时，请填写可访问的路由器 IP 地址；本工具不会启动代理服务。";
        var checks=new[]{proxyEnabled,folderManagement,antiOverDefense,workJournal,commandLineFirst,memories,guard,cuaRepair,browserRepair,officialCliUpdate,desktopUpdate,subagents};var labels=new[]{"启用 HTTP 代理","文件夹管理功能","防止过度防御","按需笔记与文档管理","优先使用命令行","启用 Codex 记忆","CLI 启动保护与修复","CUA 检查与修复","浏览器链接修复","更新官方CLI","更新桌面版（含原生CLI）","子代理多线程"};for(int i=0;i<checks.Length;i++){checks[i].Style=(Style)Resources["KitCheck"];checks[i].Content=labels[i];checks[i].IsChecked=checks[i]!=subagents;}
        cliNative.Content="桌面版原生 CLI";cliStandalone.Content="官方独立 CLI";foreach(var choice in new[]{cliNative,cliStandalone}){choice.GroupName="InitializationCliMode";choice.Style=(Style)Resources["KitCliChoice"];choice.Checked+=(s,e)=>{if(!applyingCliDetection){cliChoiceManual=true;UpdateCliDetectionHint();}};choice.Click+=(s,e)=>{cliChoiceManual=true;UpdateCliDetectionHint();};}
        cliDetectionHint.FontSize=12;cliDetectionHint.Foreground=MutedBrush;cliDetectionHint.TextWrapping=TextWrapping.Wrap;cliDetectionHint.Margin=new Thickness(0,4,0,8);
        if(preview)ApplyCliDetection(new Dictionary<string,object>{{"runtimeMode","DesktopNative"}});else UpdateCliDetectionHint();
        antiOverDefense.ToolTip="在全局 AGENTS.md 中写入 HERO 中文完整规则，以及实现、防御和自主交付规则。";
        workJournal.ToolTip="按需维护 Markdown 笔记、文档和索引，记录可复用的排查经验。";
        commandLineFirst.ToolTip="有可行命令时优先使用专用 CLI 或 PowerShell，由 Codex 直接执行并核验。";
        memories.ToolTip="初始化时开启本地 Codex 记忆生成与使用；取消后关闭，但不删除已有记忆文件。";
        githubMirrors.Content="自动使用 GitHub 镜像";githubMirrors.Style=(Style)Resources["KitCheck"];
        githubCustomMirror.Style=(Style)Resources["KitInput"];githubCustomMirror.ToolTip="可选 HTTPS 镜像前缀，例如 https://example.com/proxy/";
        var network=new GitHubNetworkOptions();if(!preview)try{network=GitHubNetworkOptions.Load(Core.NetworkSettingsPath);}catch(Exception ex){networkSettingsError="下载设置未能读取，请重新保存："+ex.Message;}
        githubMirrors.IsChecked=network.githubMirrorEnabled;githubCustomMirror.Text=network.customGithubMirror;githubCustomMirror.IsEnabled=network.githubMirrorEnabled;
        githubMirrors.Checked+=(a,b)=>githubCustomMirror.IsEnabled=true;githubMirrors.Unchecked+=(a,b)=>githubCustomMirror.IsEnabled=false;
        githubLogView.IsReadOnly=true;githubLogView.TextWrapping=TextWrapping.Wrap;githubLogView.Height=150;githubLogView.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;githubLogView.Background=B("#101620");githubLogView.Foreground=TextBrush;githubLogView.BorderThickness=new Thickness(0);githubLogView.Padding=new Thickness(10);
        proxyEnabled.Checked+=(s,e)=>{proxyPortPreset.IsEnabled=port.IsEnabled=proxyHost.IsEnabled=true;};proxyEnabled.Unchecked+=(s,e)=>{proxyPortPreset.IsEnabled=port.IsEnabled=proxyHost.IsEnabled=false;};
        parentEffort.Style=(Style)Resources["KitCombo"];childEffort.Style=(Style)Resources["KitCombo"];foreach(var input in new[]{parentModel,childModel,parentEffort,childEffort})input.Margin=new Thickness(0,0,0,10);
        var effortText=new FrameworkElementFactory(typeof(TextBlock));effortText.SetBinding(TextBlock.TextProperty,new System.Windows.Data.Binding{Converter=new EffortLabels()});var effortTemplate=new DataTemplate{VisualTree=effortText};parentEffort.ItemTemplate=effortTemplate;childEffort.ItemTemplate=effortTemplate;
        parentEffort.ItemsSource=ModelCatalog.CommonEfforts;parentEffort.SelectedItem="medium";childEffort.ItemsSource=ModelCatalog.CommonEfforts;childEffort.SelectedItem="medium";
        parentModel.Text="gpt-6.1-sol";childModel.Text="gpt-6.1-sol";
        foreach(var input in new[]{parentModel,childModel}){input.Style=(Style)Resources["KitCombo"];input.IsEditable=false;input.IsTextSearchEnabled=true;input.FontSize=14;input.ToolTip="可选择内置模型或已配置的模型。";}
        RefreshModelChoices();
        var modelTextProperty=System.ComponentModel.DependencyPropertyDescriptor.FromProperty(ComboBox.TextProperty,typeof(ComboBox));modelTextProperty.AddValueChanged(parentModel,(s,e)=>RefreshEfforts(parentEffort,parentModel.Text,"high"));modelTextProperty.AddValueChanged(childModel,(s,e)=>RefreshEfforts(childEffort,childModel.Text,"high"));
        parentModel.SelectionChanged+=(s,e)=>{if(parentModel.SelectedItem!=null)RefreshEfforts(parentEffort,Convert.ToString(parentModel.SelectedItem),"medium");};childModel.SelectionChanged+=(s,e)=>{if(childModel.SelectedItem!=null)RefreshEfforts(childEffort,Convert.ToString(childModel.SelectedItem),"medium");};
        modelCheck.Foreground=MutedBrush;modelCheck.TextWrapping=TextWrapping.Wrap;modelCheck.Margin=new Thickness(0,8,0,12);
        subagents.Checked+=(s,e)=>UpdateModelInputs();subagents.Unchecked+=(s,e)=>UpdateModelInputs();
        if(!preview)try{ApplyCurrentModelSettings(CurrentModelSettings.Load(CurrentModelSettings.CodexHome()));}catch(Exception ex){modelSettingsError="无法读取当前模型设置："+ex.Message+"。请处理后重新打开工具。";}
        if(!preview)LoadCurrentProxyPort();
        foreach(var choice in new[]{drive,proxyPortPreset,parentModel,childModel,parentEffort,childEffort})choice.DropDownOpened+=(s,e)=>{var combo=(ComboBox)s;var popup=combo.Template.FindName("PART_Popup",combo) as System.Windows.Controls.Primitives.Popup;if(popup!=null){popup.PopupAnimation=System.Windows.Controls.Primitives.PopupAnimation.None;var child=popup.Child as FrameworkElement;if(child!=null)Motion.Enter(child,preview,180);}};
        UpdateModelInputs();
        Closing+=(s,e)=>{if(busy){e.Cancel=true; MessageBox.Show(this,"操作正在执行，请等待结果后再关闭窗口。","正在执行",MessageBoxButton.OK,MessageBoxImage.Information);}};
        Navigate("概览"); Loaded+=async(s,e)=>{if(!preview) await Task.WhenAll(Run("status","读取当前状态",false),CheckUpdates(true));};
    }
    static Brush B(string hex) { return MorandiTheme.Legacy(hex); }
    void LoadCurrentProxyPort() {
        try {
            var saved=ProxyPortSettings.Load(CurrentModelSettings.CodexHome());
            var preset=ProxyPortSettings.Presets.FirstOrDefault(item=>item.Port==saved);
            if(preset==null){customProxyPort=saved.ToString();preset=ProxyPortSettings.Presets.Last();}
            proxyPortPreset.SelectedItem=preset;
        } catch(Exception ex) {
            proxyPortPreset.SelectedItem=ProxyPortSettings.Presets.Last();
            proxyPortNotice.Text="无法读取已有代理端口："+DesktopLocalization.ExceptionText(ex)+" 请重新选择或填写端口。";
            proxyPortNotice.Visibility=Visibility.Visible;
        }
    }
    Grid ProxyPortRow(){
        var row=new Grid{HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,6,0,8)};
        foreach(var width in new[]{108.0,90.0,213.0,120.0})row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(width)});
        row.Children.Add(new TextBlock{Text="阁下的代理端口：",FontSize=12,Foreground=TextBrush,VerticalAlignment=VerticalAlignment.Center});
        port.Margin=new Thickness(0);Grid.SetColumn(port,1);row.Children.Add(port);
        proxyPortPreset.Margin=new Thickness(8,0,0,0);Grid.SetColumn(proxyPortPreset,2);row.Children.Add(proxyPortPreset);
        var hintContent=new StackPanel{Orientation=Orientation.Horizontal};hintContent.Children.Add(new System.Windows.Shapes.Path{Data=Geometry.Parse("M21,12 A9,9 0 1 1 3,12 A9,9 0 1 1 21,12 M12,10 L12,17 M12,6.5 L12,7"),Stroke=Accent,StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,Width=12,Height=12,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,5,0)});hintContent.Children.Add(new TextBlock{Text="常用端口预设",FontSize=11,Foreground=Accent,VerticalAlignment=VerticalAlignment.Center});
        var hint=new Border{Name="ProxyPresetHint",Child=hintContent,Background=MorandiTheme.Group,BorderBrush=MorandiTheme.Border,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(7,4,7,4),Margin=new Thickness(8,0,0,0),HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,ToolTip="选择代理软件后会自动填写常用端口；如果软件中改过端口，请选择自定义代理端口。"};Grid.SetColumn(hint,3);row.Children.Add(hint);return row;
    }
    static TextBlock T(string text,int size,Brush color) { return new TextBlock{Text=text,FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)}; }
    Button Button(string label,Action action,bool primary) {
        var button=new Button{Content=label,Tag=UiIcons.For(label),Style=(Style)Resources["KitButton"],Background=primary?MorandiTheme.Primary:MorandiTheme.Panel,Foreground=primary?MorandiTheme.OnPrimary:TextBrush,FontSize=13,Padding=new Thickness(14,8,14,8),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,4,8,4)};
        button.Click+=(s,e)=>action();Motion.Button(button,preview);return button;
    }
    Button Link(string label,Action action) { var b=Button(label,action,false); b.Background=Brushes.Transparent;b.BorderThickness=new Thickness(0); b.Foreground=Accent; b.Padding=new Thickness(0,6,0,6); b.FontSize=12; return b; }
    StackPanel Card(string title,string description){var panel=new StackPanel{Margin=new Thickness(16,14,16,14)};panel.Children.Add(IconHeading(title,17,TextBrush,false));if(description.Length>0)panel.Children.Add(T(description,12,MutedBrush));var border=Surface(panel);border.Margin=new Thickness(0,0,0,12);content.Children.Add(border);return panel;}
    Border FeatureGroup(string title,params CheckBox[] choices) {
        var panel=new StackPanel();
        var label=IconHeading(title,13,Accent,true);label.Margin=new Thickness(0,0,0,6);panel.Children.Add(label);
        foreach(var choice in choices)panel.Children.Add(choice);
        return new Border{Background=MorandiTheme.Group,BorderBrush=MorandiTheme.Border,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),Padding=new Thickness(14,12,14,12),Margin=new Thickness(0,0,0,10),Child=panel};
    }
    void FeatureGroups(StackPanel parent) {
        var grid=new Grid{Margin=new Thickness(0,8,0,0)};
        grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var groups=new[]{FeatureGroup("工作与规则",folderManagement,antiOverDefense,workJournal,commandLineFirst),FeatureGroup("记忆",memories),FeatureGroup("修复与连接",guard,cuaRepair,browserRepair),FeatureGroup("组件更新",officialCliUpdate,desktopUpdate)};
        for(int index=0;index<groups.Length;index++){
            var group=groups[index];Grid.SetRow(group,index/2);Grid.SetColumn(group,index%2);
            if(index%2==0)group.Margin=new Thickness(0,0,10,10);grid.Children.Add(group);
        }
        parent.Children.Add(grid);
        var optional=new StackPanel();optional.Children.Add(subagents);optional.Children.Add(T("默认关闭，启用后可设置子模型与思考强度。",12,MutedBrush));
        parent.Children.Add(new Border{Background=B("#151C28"),CornerRadius=new CornerRadius(9),Padding=new Thickness(14,8,14,10),Child=optional});
    }
    void Row(StackPanel parent,params UIElement[] items) { var row=new WrapPanel(); foreach(var item in items)row.Children.Add(item); parent.Children.Add(row); }
    void Navigate(string name) { pageScroll.StopAnimation();logViewport.Detach();foreach(var control in new FrameworkElement[]{drive,port,proxyPortPreset,proxyPortNotice,proxyHost,guard,log,githubMirrors,githubCustomMirror,githubLogView,proxyEnabled,folderManagement,antiOverDefense,workJournal,commandLineFirst,memories,subagents,cuaRepair,browserRepair,officialCliUpdate,desktopUpdate,cliNative,cliStandalone,cliDetectionHint,parentModel,childModel,parentEffort,childEffort,modelCheck}) { var parent=control.Parent as Panel; if(parent!=null)parent.Children.Remove(control);else{var holder=control.Parent as ContentControl;if(holder!=null&&holder.Content==control)holder.Content=null;} } page=name;content.Children.Clear();pageNavigation.Children.Clear();stageProgress=null;heading.Text=name;if(pageHeadingIcon!=null)pageHeadingIcon.Data=UiIcons.For(name,true);foreach(var b in navigation){var active=Convert.ToString(b.Content)==name;b.Background=active?MorandiTheme.Selection:Brushes.Transparent;b.Foreground=active?TextBrush:MutedBrush;b.BorderBrush=active?MorandiTheme.Accent:Brushes.Transparent;b.FontWeight=active?FontWeights.SemiBold:FontWeights.Normal;}
        foreach(var section in navigationSections.Values.Distinct())section.Foreground=navigationSections.ContainsKey(name)&&navigationSections[name]==section?Accent:TextBrush;
        if(name=="概览")Overview();else if(name=="初始化")Wizard();else if(name=="连接与修复")RepairPage();else if(name=="CLI 管理")CliPage();else if(name=="备份与回滚")BackupPage();else if(name=="更新与下载")Updates();else LogsPage();
        if(log.Parent!=null)logViewport.Attach();Motion.Enter(content,preview,220);
    }
    static string Format(object value) { if(value==null)return "未检测"; if(value is bool)return (bool)value?"是":"否"; var text=Convert.ToString(value);if(text=="healthy")return "完整";if(text=="missing")return "未找到";if(text=="incomplete")return "组件不完整";return text; }
    static string RuntimeMode(object value) { var text=Convert.ToString(value);if(text=="DesktopNative")return "桌面原生";if(text=="OfficialStandalone")return "官方独立 CLI";return "需要检查"; }
    void ApplyCliDetection(Dictionary<string,object> current) {
        detectedCliMode=Core.Value(current,"runtimeMode","");cliDetectionComplete=true;
        if(!cliChoiceManual){
            applyingCliDetection=true;
            try{cliNative.IsChecked=detectedCliMode=="DesktopNative";cliStandalone.IsChecked=detectedCliMode=="OfficialStandalone";}
            finally{applyingCliDetection=false;}
        }
        UpdateCliDetectionHint();
    }
    void UpdateCliDetectionHint() {
        if(cliChoiceManual)cliDetectionHint.Text="已手动选择 CLI，本次初始化将按所选模式执行。";
        else if(detectedCliMode=="DesktopNative"||detectedCliMode=="OfficialStandalone")cliDetectionHint.Text="已检测到当前模式；初始化开始前将再次检测并自动沿用，也可手动切换。";
        else cliDetectionHint.Text=cliDetectionComplete?"未能识别当前 CLI 模式，请手动选择，或到维护恢复检查。":"正在检测当前 CLI 模式；也可手动选择。";
    }
    void ResetCliChoice() {
        cliChoiceManual=false;
        if(status!=null)ApplyCliDetection(status);
        else if(preview)ApplyCliDetection(new Dictionary<string,object>{{"runtimeMode","DesktopNative"}});
        else{detectedCliMode="";cliDetectionComplete=false;applyingCliDetection=true;try{cliNative.IsChecked=false;cliStandalone.IsChecked=false;}finally{applyingCliDetection=false;}UpdateCliDetectionHint();}
    }
    void RequireCliChoice() {
        if(busy&&activeOperation=="status")throw new InvalidOperationException("正在读取当前状态，请稍候再继续。");
        if(!cliChoiceManual&&detectedCliMode!="DesktopNative"&&detectedCliMode!="OfficialStandalone")throw new InvalidOperationException("尚未识别当前 CLI 模式，请手动选择，或到维护恢复检查后重试。");
    }
    string CliChoiceSummary() {
        var name=cliStandalone.IsChecked==true?"官方独立 CLI":cliNative.IsChecked==true?"桌面版原生 CLI":"尚未识别";
        return name+(cliChoiceManual?"（手动选择）":"（初始化开始前再次检测并自动沿用）")+(cliStandalone.IsChecked==true?"；缺失时会准备组件":"");
    }
    void VerifyRuntimeDisplay(string mode,string path) { var card=content.Children.Count>1?content.Children[1] as Border:null;var panel=card==null?null:card.Child as StackPanel;var texts=panel==null?new string[0]:InitializationFlowTests.Descendants<TextBlock>(panel).Select(x=>x.Text).ToArray();if(!texts.Any(x=>x.Contains(RuntimeMode(mode)))||!texts.Any(x=>x.Contains(path)))throw new InvalidOperationException("Runtime mode status was not displayed"); }
    int Port() { if(proxyEnabled.IsChecked!=true)return 0;int v; if(!int.TryParse(port.Text,out v)||v<1||v>65535)throw new InvalidOperationException("代理端口需为 1–65535 的整数。"); return v; }
    void ApplyCurrentModelSettings(CurrentModelSettings current){modelCatalog.IncludeCurrent(current.ParentModel,current.ParentEffort);modelCatalog.IncludeCurrent(current.ChildModel,current.ChildEffort);RefreshModelChoices();parentModel.SelectedItem=current.ParentModel;childModel.SelectedItem=current.ChildModel;RefreshEfforts(parentEffort,current.ParentModel,"medium");RefreshEfforts(childEffort,current.ChildModel,"medium");parentEffort.SelectedItem=current.ParentEffort;childEffort.SelectedItem=current.ChildEffort;}
    void UpdateModelInputs(){parentModel.IsEnabled=true;parentEffort.IsEnabled=true;childModel.IsEnabled=subagents.IsChecked==true;childEffort.IsEnabled=subagents.IsChecked==true;RefreshEfforts(parentEffort,Convert.ToString(parentModel.SelectedItem),"medium");RefreshEfforts(childEffort,Convert.ToString(childModel.SelectedItem),"medium");modelCheck.Text=modelSettingsError.Length>0?modelSettingsError:modelCatalog.Source+"\n"+(subagents.IsChecked==true?"子代理已开启：将写入所选子代理模型和思考强度。":"子代理未开启：子模型与思考强度已禁用，勾选子代理多线程后可设置。");}
    void RefreshEfforts(ComboBox combo,string model,string fallback){CatalogModel entry;var values=modelCatalog.Models.TryGetValue((model??"").Trim(),out entry)?entry.Efforts:new string[0];var current=Convert.ToString(combo.SelectedItem);combo.ItemsSource=values;combo.SelectedItem=values.Contains(current)?current:values.Contains(fallback)?fallback:entry!=null&&values.Contains(entry.DefaultEffort)?entry.DefaultEffort:values.FirstOrDefault();}
    string Effort(ComboBox combo,string fallback){return Convert.ToString(combo.SelectedItem);}
    void CheckModels(){try{if(modelSettingsError.Length>0)throw new InvalidOperationException(modelSettingsError);RefreshModelChoices();RefreshEfforts(parentEffort,parentModel.Text,"high");RefreshEfforts(childEffort,childModel.Text,"high");var p=Model(parentModel,"gpt-6.1-sol");var c=Model(childModel,"gpt-6.1-sol");modelCheck.Text=modelCatalog.Source+"\n"+EffortLabels.InText(modelCatalog.Check(p,Effort(parentEffort,"medium")))+"\n"+(subagents.IsChecked==true?EffortLabels.InText(modelCatalog.Check(c,Effort(childEffort,"medium"))):"子代理未开启：跳过子模型检查。")+"\n预设列表不代表当前账号一定有调用权限。";}catch(Exception ex){modelCheck.Text=ex.Message;}}
    void RefreshModelChoices(){var values=modelCatalog.Models.Keys.ToArray();foreach(var combo in new[]{parentModel,childModel}){var current=Convert.ToString(combo.SelectedItem);if(current.Length==0)current=combo==parentModel?"gpt-6.1-sol":"gpt-6.1-sol";combo.ItemsSource=values;combo.SelectedItem=values.Contains(current)?current:values.FirstOrDefault();}}
    string Model(ComboBox input,string fallback){var value=Convert.ToString(input.SelectedItem);if(!modelCatalog.Models.ContainsKey(value))throw new InvalidOperationException("请从下拉框选择模型。");return value;}
    string ProxyHost() { if(proxyEnabled.IsChecked!=true)return "";var text=(proxyHost.Text??"").Trim();if(text.Length==0)return Core.DefaultProxyHost;System.Net.IPAddress address;if(!System.Net.IPAddress.TryParse(text,out address))throw new InvalidOperationException("代理 IP 必须是有效的 IPv4 或 IPv6 地址。");return address.ToString(); }
    Dictionary<string,object> Options(){if(modelSettingsError.Length>0)throw new InvalidOperationException(modelSettingsError);var p=Model(parentModel,"gpt-6.1-sol");var pe=Effort(parentEffort,"medium");var c=Model(childModel,"gpt-6.1-sol");var ce=Effort(childEffort,"medium");modelCatalog.Check(p,pe);if(subagents.IsChecked==true)modelCatalog.Check(c,ce);return new Dictionary<string,object>{{"proxyHost",ProxyHost()},{"folderManagement",folderManagement.IsChecked==true},{"antiOverDefense",antiOverDefense.IsChecked==true},{"workJournal",workJournal.IsChecked==true},{"commandLineFirst",commandLineFirst.IsChecked==true},{"memories",memories.IsChecked==true},{"githubMirrorEnabled",githubMirrors.IsChecked==true},{"customGithubMirror",NetworkOptions().customGithubMirror},{"subagents",subagents.IsChecked==true},{"cuaRepair",cuaRepair.IsChecked==true},{"browserRepair",browserRepair.IsChecked==true},{"officialCliUpdate",officialCliUpdate.IsChecked==true},{"desktopUpdate",desktopUpdate.IsChecked==true},{"cliMode",cliChoiceManual?(cliStandalone.IsChecked==true?"official-standalone":"desktop-native"):"auto"},{"parentModel",p},{"parentEffort",pe},{"childModel",c},{"childEffort",ce}};}
    Dictionary<string,object> ProxyOptions(){return new Dictionary<string,object>{{"proxyHost",ProxyHost()},{"githubMirrorEnabled",githubMirrors.IsChecked==true},{"customGithubMirror",NetworkOptions().customGithubMirror}};}
    GitHubNetworkOptions NetworkOptions(){return new GitHubNetworkOptions{githubMirrorEnabled=githubMirrors.IsChecked==true,customGithubMirror=githubMirrors.IsChecked==true?GitHubNetworkOptions.NormalizePrefix(githubCustomMirror.Text):""};}
    string ModelChecks(Dictionary<string,object> options){return EffortLabels.InText(modelCatalog.Source+"\n"+modelCatalog.Check(Convert.ToString(options["parentModel"]),Convert.ToString(options["parentEffort"]))+(subagents.IsChecked==true?"\n"+modelCatalog.Check(Convert.ToString(options["childModel"]),Convert.ToString(options["childEffort"])):"")+"\n核对结果不代表账号实时调用权限。");}
    string ProxySummary(){var value=Port();if(value==0)return "关闭（不写入本地 HTTP 代理）";var host=ProxyHost();if(host.Contains(":")&&!host.StartsWith("["))host="["+host+"]";return "http://"+host+":"+value;}
    string Drive() { if(drive.SelectedItem==null)throw new InvalidOperationException("请选择可用的工作盘。"); return Convert.ToString(drive.SelectedItem); }
    string InitSummary() {var options=Options();return "工作盘："+Drive()+":\n父模型："+options["parentModel"]+" / "+EffortLabels.Display(Convert.ToString(options["parentEffort"]))+"\n子代理："+(subagents.IsChecked==true?options["childModel"]+" / "+EffortLabels.Display(Convert.ToString(options["childEffort"])):"关闭，不写入子代理规则；保留模型设置 "+options["childModel"]+" / "+EffortLabels.Display(Convert.ToString(options["childEffort"])))+"\n初始化后使用的 CLI："+CliChoiceSummary()+"\n代理："+ProxySummary()+"\n文件夹管理："+(folderManagement.IsChecked==true?"创建并绑定标准目录":"跳过")+"\n防止过度防御："+(antiOverDefense.IsChecked==true?"写入 HERO 中文完整规则及实现、交付规则":"跳过")+"\n按需笔记与文档管理："+(workJournal.IsChecked==true?"写入 Markdown 笔记、文档与索引规则":"跳过")+"\n优先使用命令行："+(commandLineFirst.IsChecked==true?"写入 CLI 与 PowerShell 优先规则":"跳过")+"\nCodex 记忆："+(memories.IsChecked==true?"开启生成和使用":"关闭，保留已有记忆文件")+"\nGitHub 下载："+(githubMirrors.IsChecked==true?"官方 → 自定义 → 内置 10 个镜像":"仅使用官方地址")+"\nCUA 检查与修复："+(cuaRepair.IsChecked==true?"按需执行":"跳过")+"\nCLI 启动保护与修复："+(guard.IsChecked==true?"安装，包含登录自动维护":"跳过")+"\n浏览器链接修复："+(browserRepair.IsChecked==true?"最后执行，真实连接需重启后验证":"跳过")+"\n更新官方CLI："+(officialCliUpdate.IsChecked==true?"更新独立组件":"跳过更新；如所选组件缺失仍会准备")+"\n更新桌面版（含原生CLI）："+(desktopUpdate.IsChecked==true?"先通过 Microsoft Store 精确更新":"跳过")+"\n执行顺序：前置检查 → 桌面更新 → 配置、目录及 CUA → 独立 CLI 更新 → 可选保护 → CLI 选择 → 浏览器修复。未完成项目可单独重试。\n"+ModelChecks(options)+"\n基础配置及全局 AGENTS.md 将完整备份并重建。审批与沙箱使用 Codex 默认设置。原自定义指令只保存在备份中。项目内的 AGENTS.md 不变。更新用户 PATH。初始化和 CUA 分别备份。";}
    void ActionCard(string title,string description,string label,string op,bool danger) {
        var p=Card(title,description); p.Children.Add(Button(label,async()=>await Run(op,title,true),!danger));
    }
    async Task<Dictionary<string,object>> ReadInitializationPreflight(Dictionary<string,object> options) {
        var id=Guid.NewGuid().ToString("N");var path=OperationLogPath(id);
        using(var process=StartOperation("initialize-preflight",id,Environment.SystemDirectory.Substring(0,1),0,false,options)){
            while(!process.HasExited)await Task.Delay(100);
            var lines=File.Exists(path)?File.ReadAllLines(path,Encoding.UTF8):new string[0];
            if(process.ExitCode!=0)throw new InvalidDataException("初始化前检查未完成。\n"+DesktopLocalization.TranslateText(String.Join("\n",lines)));
            var line=lines.LastOrDefault(x=>x.StartsWith("@@INIT_PREFLIGHT@@"));
            if(line==null)throw new InvalidDataException("未收到初始化前检查结果，请重试。");
            return Core.ReadJson(line.Substring("@@INIT_PREFLIGHT@@".Length));
        }
    }
    Window CreateAppCloseDialog(int count) {
        var dialog=new Window{Title="关闭 ChatGPT／Codex",Width=560,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Owner=this,Background=BackgroundBrush,Foreground=TextBrush,ShowInTaskbar=false};
        var panel=new StackPanel{Margin=new Thickness(25)};
        panel.Children.Add(T("是否允许关闭 ChatGPT／Codex 并继续初始化？",18,TextBrush));
        panel.Children.Add(T("检测到桌面应用或关联运行时仍在运行（"+count+" 个进程）。\n\n请先保存工作。关闭会中断正在执行的任务。程序将先请求正常退出，再结束仍存在的相关进程。",14,TextBrush));
        var buttons=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};
        var allow=Button("关闭并继续",()=>Motion.Close(dialog,true,preview),true);allow.IsDefault=false;
        var cancel=Button("取消",()=>Motion.Close(dialog,false,preview),false);cancel.IsDefault=true;cancel.IsCancel=true;
        buttons.Children.Add(allow);buttons.Children.Add(cancel);panel.Children.Add(buttons);dialog.Content=panel;Motion.Dialog(dialog,preview);return dialog;
    }
    async Task BeginInitialization() {
        if(busy)return;
        busy=true;activeOperation="initialize";banner.Text="正在检查初始化条件…";
        bool proceed=false,allowClose=false;
        Dictionary<string,object> options=null;
        try {
            options=Options();initializationSummary="";initializationState="";initializationProgress.Reset(options,guard.IsChecked==true);initializationProgress.Apply(new Dictionary<string,object>{{"stageId","preflight"},{"state","running"},{"detail","正在检查应用状态"}});
            var report=InitializationPreflight==null?await ReadInitializationPreflight(options):await InitializationPreflight();
            object value;if(!report.TryGetValue("processes",out value)||!(value is System.Collections.IList))throw new InvalidDataException("初始化进程检查结果无效，请重试。");
            int count=((System.Collections.IList)value).Count;
            if(count>0){
                var dialog=CreateAppCloseDialog(count);
                allowClose=(PresentAppCloseDialog==null?dialog.ShowDialog():PresentAppCloseDialog(dialog))==true;
                if(allowClose){
                    var entries=((System.Collections.IList)value).Cast<Dictionary<string,object>>();
                    options["closeApprovedProcesses"]=String.Join(",",entries.Select(entry=>Core.Value(entry,"Id","")+":"+Core.Value(entry,"Started","")));
                }
            }
            proceed=count==0||allowClose;
            if(!proceed)banner.Text="已取消本次初始化，配置尚未修改。";
        } catch(Exception ex){if(preview){banner.Text=DesktopLocalization.ExceptionText(ex);}else Error(ex);}
        finally {busy=false;activeOperation="";}
        if(!proceed){wizard=1;Navigate("初始化");return;}
        wizard=2;Navigate("初始化");
        options["allowCloseRunningApp"]=allowClose;
        await Run("initialize","初始化",false,options);
    }
    void BackupPage(){
        subtitle.Text="选择已有备份，查看位置或确认回滚。";
        var r=Card("恢复与回滚","选择本地初始化备份或保护器 CLI 历史缓存，可以打开所在文件夹或回滚。时间为本机备份 / 缓存时间。此处的回滚范围为初始化配置与保护器 CLI。");
        rollbackList=new ListBox{ItemContainerStyle=(Style)Resources["KitRollbackItem"],Background=B("#101620"),Foreground=TextBrush,BorderThickness=new Thickness(0),MaxHeight=280,MinHeight=70,HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(6),Margin=new Thickness(0,2,0,10)};
        ScrollViewer.SetHorizontalScrollBarVisibility(rollbackList,ScrollBarVisibility.Disabled);
        var entries=status!=null&&status.ContainsKey("rollbackEntries")?status["rollbackEntries"] as System.Collections.IEnumerable:null;
        if(entries!=null)foreach(var value in entries){var entry=value as Dictionary<string,object>;if(entry==null)continue;var row=new StackPanel{Margin=new Thickness(8,5,8,5)};row.Children.Add(T((Core.Value(entry,"kind","")=="init"?"初始化配置":"保护器 CLI")+"  ·  "+Core.Value(entry,"version","版本未记录"),14,CanRollback(entry)?TextBrush:MutedBrush));row.Children.Add(T(Core.Value(entry,"time","时间未记录")+"  ·  "+Core.Value(entry,"detail",""),12,MutedBrush));var item=new ListBoxItem{Tag=entry,Content=row,ToolTip=Core.Value(entry,"path",""),HorizontalContentAlignment=HorizontalAlignment.Stretch};rollbackList.Items.Add(item);if(Core.Value(entry,"id","")==selectedRollbackId)rollbackList.SelectedItem=item;}
        if(rollbackList.Items.Count==0)r.Children.Add(T(Core.Value(status,"rollbackError","").Length>0?Core.Value(status,"rollbackError",""):status==null?"正在读取可回滚备份，请刷新列表。":"暂未发现初始化备份或独立 CLI 历史缓存。",13,MutedBrush));
        r.Children.Add(rollbackList);
        rollbackButton=Button("回滚所选项",async()=>{var entry=SelectedRollbackEntry();if(CanRollback(entry))await Run("rollback-selected","回滚所选项",true,new Dictionary<string,object>{{"rollbackId",Core.Value(entry,"id","")}},RollbackSummary(entry));},false);
        rollbackFolderButton=Button("打开所在文件夹",()=>OpenSelectedRollbackFolder(Open),false);
        rollbackDeleteButton=Button("删除所有回滚",async()=>{
            var id=Core.Value(status,"rollbackDeleteId","");
            if(id.Length==0)return;
            var count=Core.Value(status,"rollbackDeleteCount","0");
            await Run("rollback-delete-all","删除所有回滚",true,new Dictionary<string,object>{{"rollbackId",id}},
                "将永久删除当前列表中 "+count+" 项可清理的初始化备份或独立 CLI 历史缓存。保留当前及被引用的 CLI、当前配置、对话和项目文件。删除后无法从这些备份回滚。涉及 CLI 缓存时请先退出 Codex。");
        },false);
        rollbackList.SelectionChanged+=(s,e)=>{selectedRollbackId=Core.Value(SelectedRollbackEntry(),"id","");UpdateRollbackActions();};
        UpdateRollbackActions();
        Row(r,rollbackButton,rollbackFolderButton,Button("刷新备份列表",async()=>{if(!busy){await RefreshStatusQuietly();Navigate("备份与回滚");}},false),rollbackDeleteButton);
    }
    Dictionary<string,object> SelectedRollbackEntry(){
        var item=rollbackList==null?null:rollbackList.SelectedItem as ListBoxItem;
        return item==null?null:item.Tag as Dictionary<string,object>;
    }
    static bool CanRollback(Dictionary<string,object> entry){
        object available;
        return entry!=null&&entry.TryGetValue("available",out available)&&available is bool&&(bool)available;
    }
    void UpdateRollbackActions(){
        var entry=SelectedRollbackEntry();
        if(rollbackButton!=null)rollbackButton.IsEnabled=!busy&&CanRollback(entry);
        if(rollbackFolderButton!=null){
            var canOpen=Core.Value(entry,"path","").Length>0;
            rollbackFolderButton.IsEnabled=canOpen;
            rollbackFolderButton.Background=B(canOpen?"#293348":"#383838");
            rollbackFolderButton.Foreground=canOpen?TextBrush:B("#A6A6A6");
        }
        if(rollbackDeleteButton!=null){
            var canDelete=!busy&&Core.Value(status,"rollbackDeleteId","").Length>0;
            rollbackDeleteButton.IsEnabled=canDelete;
            rollbackDeleteButton.Background=B(canDelete?"#963E4A":"#383838");
            rollbackDeleteButton.Foreground=canDelete?TextBrush:B("#A6A6A6");
        }
    }
    void OpenSelectedRollbackFolder(Action<string> open){
        var path=Core.Value(SelectedRollbackEntry(),"path","");
        if(!Directory.Exists(path)){banner.Text="所选备份文件夹已移动或删除，请刷新备份列表。";return;}
        open(path);
    }
    static string RollbackSummary(Dictionary<string,object> entry){return "目标："+(Core.Value(entry,"kind","")=="init"?"初始化配置":"保护器 CLI")+"\n版本："+Core.Value(entry,"version","版本未记录")+"\n备份 / 缓存时间："+Core.Value(entry,"time","未记录")+"\n位置："+Core.Value(entry,"path","")+"\n\n"+(Core.Value(entry,"kind","")=="init"?"将覆盖当前配置、代理、全局规则及该备份记录的运行时 / PATH 设置；不恢复 CUA，不删除对话或已创建的工作目录。":"将切换到所选本地独立 CLI，并更改桌面应用的 CLI 覆盖路径；不会降级 Microsoft Store 桌面应用。")+"\n请先保存工作并退出 Codex；检测到仍在运行时停止，不强制结束进程。";}
    void GuideOfficialCli(){
        Navigate("CLI 管理");
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(HighlightOfficialSwitch));
    }
    void HighlightOfficialSwitch(){
            if(officialSwitch==null)return;officialSwitch.BringIntoView();officialSwitch.Focus();
            if(SystemParameters.ClientAreaAnimation&&!preview){var animation=new DoubleAnimation(1,.45,TimeSpan.FromMilliseconds(450)){AutoReverse=true,RepeatBehavior=new RepeatBehavior(3)};officialSwitch.BeginAnimation(UIElement.OpacityProperty,animation);}
            else {officialSwitch.Background=Accent;officialSwitch.Foreground=B("#101B31");officialSwitch.ToolTip="请点击此按钮检查并切换官方独立 CLI";}
    }
    void BrowserConnection(){
        subtitle.Text="检查浏览器运行时与代理，准备修复，再重启验证真实连接。";
        if(browserReport==null&&!preview){try{var file=Path.Combine(Core.Root,"BrowserConnection","last-check.json");if(File.Exists(file))browserReport=Core.ReadJson(File.ReadAllText(file,Encoding.UTF8));}catch{}}
        var environment=Card("浏览器连接状态","");
        Pair(environment,"桌面版",Core.Value(status,"desktopVersion","未检测"));Pair(environment,"补丁状态",DesktopLocalization.TranslateText(Core.Value(browserReport,"patch","尚未检查")));
        if(browserReport!=null&&status!=null&&Core.Value(browserReport,"desktopVersion","")!=Core.Value(status,"desktopVersion",""))environment.Children.Add(T("桌面版本已变化，请重新检查。",12,B("#E9C779")));
        var detail=new StackPanel();foreach(var item in new[]{new[]{"runtime","控制运行时"},new[]{"proxy","检查代理"},new[]{"checkedAt","检查时间"},new[]{"integrity","组件完整性"},new[]{"reachability","代理可达性"},new[]{"environment","环境继承"},new[]{"connection","真实连接"}})Pair(detail,item[1],DesktopLocalization.TranslateText(Core.Value(browserReport,item[0],item[0]=="connection"?"待重启后验证":"未检查")));
        detail.Children.Add(T("HTTP 403 仅表示服务可达，真实连接需重启后运行测试。桌面版升级后请重新检查。",12,MutedBrush));environment.Children.Add(new Expander{Header="分项验证与检查详情",Content=detail});
        var settings=Card("代理设置","与初始化页面同步。使用代理软件提供的 HTTP／混合端口；关闭代理后，修复操作会恢复本工具管理的入口为直连。");
        settings.Children.Add(proxyEnabled);settings.Children.Add(ProxyPortRow());settings.Children.Add(proxyPortNotice);
        settings.Children.Add(T("自动沿用已有端口，也可选择软件代填；修改过端口时请选择自定义代理端口。",12,MutedBrush));
        settings.Children.Add(T("代理 IP（留空为 127.0.0.1）",12,MutedBrush));settings.Children.Add(proxyHost);
        Row(settings,Button("修复并启动 Codex",async()=>await Run("browser-start-prepare","修复并启动 Codex",true),true),Button("检查连接",async()=>await Run("browser-check","检查浏览器连接",false),false),Button("修复连接",async()=>await Run("browser-repair","修复浏览器连接",true),false),Button("恢复原始文件",async()=>await Run("browser-rollback","恢复浏览器入口",true),false),Button("复制连接测试指令",()=>{Clipboard.SetText("请使用浏览器控制工具，新建独立测试标签页访问 https://example.com，读取页面标题和 DOM；成功后关闭本次新建的测试标签。保留我的其他 Chrome 标签页。请分别报告连接、页面读取和关闭结果，不要仅用 HTTP 状态判断连接成功。");banner.Text="已复制 · 重启 Codex 后在新对话中运行测试";},false));
        Row(settings,Button("仅保存代理配置",async()=>await Run("proxy","更新代理",true),false),Link("微软商店 ↗",()=>Open("ms-windows-store://pdp/?ProductId=9PLM9XGG6VKS")));
        settings.Children.Add(T("通过此按钮启动时会先检查并按需修复；原来的 Codex 图标不会经过此检查。",12,MutedBrush));
        settings.Children.Add(T("修复前请保存工作并退出 Codex。保留 Chrome 和用户标签页，修复后重启 Codex 验证。",12,Accent));
    }
    void ModelSelectionRow(StackPanel panel,string label,ComboBox model,ComboBox effort){
        var heading=T(label,16,TextBrush);heading.FontWeight=FontWeights.Bold;heading.Margin=new Thickness(0,12,0,8);panel.Children.Add(heading);var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(2,GridUnitType.Star)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        var id=T("模型 ID",13,MutedBrush);id.VerticalAlignment=VerticalAlignment.Center;id.Margin=new Thickness(0,0,12,10);row.Children.Add(id);Grid.SetColumn(model,1);row.Children.Add(model);var strength=T("思考强度",13,MutedBrush);strength.VerticalAlignment=VerticalAlignment.Center;strength.Margin=new Thickness(18,0,12,10);Grid.SetColumn(strength,2);row.Children.Add(strength);Grid.SetColumn(effort,3);row.Children.Add(effort);panel.Children.Add(row);
    }
    void VerifyMaintenanceModes(){
        foreach(var mode in new[]{"DesktopNative","OfficialStandalone","Unknown"}){
            status=new Dictionary<string,object>{{"runtimeMode",mode}};Navigate("CLI 管理");UpdateLayout();var expected=mode=="DesktopNative"?"切换官方独立 CLI":mode=="OfficialStandalone"?"切换桌面原生 CLI":"重新检测 CLI";
            if(Convert.ToString(officialSwitch.Content)!=expected)throw new InvalidDataException("CLI switch label missing");
            var buttons=InitializationFlowTests.Descendants<Button>(content).Select(x=>Convert.ToString(x.Content)).ToArray();foreach(var label in new[]{expected,"安装 / 修复保护","更新官方独立 CLI","更新桌面版（含原生 CLI）","诊断助手","卸载启动保护"})if(!buttons.Contains(label))throw new InvalidDataException("CLI action missing: "+label);
            repairTab=4;Navigate("连接与修复");if(!InitializationFlowTests.Descendants<Button>(content).Any(x=>Convert.ToString(x.Content)=="清理已废弃配置"))throw new InvalidDataException("Config cleanup missing");
            repairTab=3;Navigate("连接与修复");if(!InitializationFlowTests.Descendants<Button>(content).Any(x=>Convert.ToString(x.Content)=="修复桌面启动组件"))throw new InvalidDataException("Startup repair missing");
        }
        diagnosticAdvice=DiagnosticAdvice.Build(null,1,"[ERROR] proxy connection refused");repairTab=0;Navigate("连接与修复");if(!InitializationFlowTests.Descendants<TextBlock>(content).Any(x=>x.Text.Contains("建议怎么做")))throw new InvalidDataException("Diagnostic advice missing");diagnosticAdvice="";repairTab=1;status=null;wizard=0;configurationTab=2;Navigate("初始化");
        if(parentModel.Parent!=parentEffort.Parent||childModel.Parent!=childEffort.Parent||parentModel.Parent==childModel.Parent)throw new InvalidDataException("Model controls no longer grouped");
        parentModel.ApplyTemplate();var popup=(System.Windows.Controls.Primitives.Popup)parentModel.Template.FindName("PART_Popup",parentModel);var popupScroll=((Border)popup.Child).Child as SmoothScrollViewer;if(popupScroll==null)throw new InvalidOperationException("Model dropdown scrolling is not smooth");double itemsHeight=10;foreach(var id in ModelCatalog.Preset().Models.Keys){var item=new ComboBoxItem{Content=id,Style=(Style)Resources["KitComboItem"]};item.Measure(new Size(320,Double.PositiveInfinity));itemsHeight+=item.DesiredSize.Height;if(item.FontSize!=12.5)throw new InvalidOperationException("Dropdown font was not compact");}if(itemsHeight<=0||popupScroll.MaxHeight<=0)throw new InvalidOperationException("Preset dropdown scrolling is unavailable");
    }
    TextBlock updatePageDescription,updatePageNotes;
    Button updatePageDownload;
    void RefreshUpdatePage(){
        if(page!="更新与下载"||updatePageDescription==null)return;
        updatePageDescription.Text=update==null?"尚未获得更新清单，可手动重试。":"GitHub 最新版本  v"+Core.Value(update,"version","未知");
        var newer=update!=null&&new Version(Core.Value(update,"version","0.0.0"))>new Version(Core.Version);updatePageNotes.Text=newer?Core.Value(update,"notes","有新版本可用。"):"";updatePageNotes.Visibility=updatePageDownload.Visibility=newer?Visibility.Visible:Visibility.Collapsed;
    }
    void Updates() {
        subtitle.Text="从项目 GitHub 发现新版本，确认后下载、校验并替换。";
        var p=Card("当前版本  v"+Core.Version,update==null?"尚未获得更新清单。应用启动时会自动检查，可手动重试。":"GitHub 最新版本  v"+Core.Value(update,"version","未知"));
        Row(p,Button("检查更新",async()=>await CheckUpdates(false),true),Button("打开版本发布页 ↗",()=>Open("https://github.com/"+Core.Repository+"/releases"),false));
        updatePageDescription=p.Children.OfType<TextBlock>().First();updatePageNotes=T("",14,Accent);updatePageDownload=Button("下载并更新",async()=>await InstallUpdate(),true);p.Children.Add(updatePageNotes);p.Children.Add(updatePageDownload);RefreshUpdatePage();
        var networkCard=Card("GitHub 下载加速","自定义镜像留空时跳过。内置镜像并行测速，优先使用最快的有效站点。请求沿用当前 HTTP 代理设置。");
        networkCard.Children.Add(githubMirrors);networkCard.Children.Add(T("自定义镜像前缀（可选，HTTPS）",12,MutedBrush));networkCard.Children.Add(githubCustomMirror);
        networkCard.Children.Add(T("顺序：官方 → 自定义 → 内置 10 个镜像",12,MutedBrush));
        networkCard.Children.Add(T("内置站点："+GitHubTransfer.BuiltInMirrorNames,12,MutedBrush));
        Row(networkCard,Button("保存下载设置",()=>{try{if(preview){banner.Text="设计预览不会保存下载设置。";return;}NetworkOptions().Save(Core.NetworkSettingsPath);networkSettingsError="";banner.Text="GitHub 下载设置已保存";}catch(Exception ex){Error(ex);}},true));
        if(networkSettingsError.Length>0)networkCard.Children.Add(T(networkSettingsError,12,MutedBrush));
        githubLogView.Text=githubLogText;networkCard.Children.Add(githubLogView);
        Card("更新如何进行","每次启动自动检查 GitHub 新版，右下角显示检查状态，发现更新时显示更新入口。点击后下载并校验，退出后替换同目录、同文件名的 EXE 并重启；更新成功后自动删除旧版本。\n检查失败不影响本地功能。网络请求遵循初始化页面中的代理开关和端口。");
    }
    void LogsPage() { subtitle.Text="默认跟随底部最新日志；上翻暂停，回到底部恢复。"; var p=Card(operationTitle.Length>0?operationTitle:"执行日志","中文和原始日志保存在当前用户 LocalAppData\\CodexInitKit\\Desktop\\Logs。"); p.Children.Add(log); Row(p,LatestLogButton(),Button(showRawLog?"查看中文日志":"查看原始日志",()=>{showRawLog=!showRawLog;UpdateLogView();Navigate("日志");},false),Button("打开日志目录",()=>Open(Core.Logs),false),Button("复制日志",()=>Clipboard.SetText(LogToCopy()),false)); }
    Button LatestLogButton(){return Button("回到最新日志",()=>{var expander=log.Parent as Expander;if(expander!=null)expander.IsExpanded=true;logViewport.Latest();},false);}
    string LogToCopy(){return log.Text.Length>0?log.Text:"尚无日志";}
    void UpdateLogView(){logViewport.SetText(showRawLog?rawLogText:chineseLogText);}
    string TranslateLogLine(string value){string translated;if(!localizedLines.TryGetValue(value,out translated)){translated=DesktopLocalization.TranslateLine(value);localizedLines[value]=translated;}return translated;}
    bool Confirm(string title,string message) { return CreateActionDialog(title,message,true).ShowDialog()==true; }
    void Error(Exception ex) { var message=DesktopLocalization.ExceptionText(ex);banner.Text=message;RecordUiError(ex,true);CreateActionDialog("操作未完成",message,false).ShowDialog(); }
    void RecordUiError(Exception ex,bool select) {
        try{var path=Path.Combine(Core.Logs,"ui-"+Guid.NewGuid().ToString("N")+".log");File.WriteAllText(path,ex.ToString(),new UTF8Encoding(false));DesktopLocalization.SaveChineseLog(path,DesktopLocalization.ExceptionText(ex));if(select){lastLog=path;rawLogText=File.ReadAllText(path,Encoding.UTF8);rawDiagnosticText=rawLogText;chineseLogText=DesktopLocalization.ExceptionText(ex);showRawLog=false;operationTitle="操作错误";UpdateLogView();}}catch{}
    }
    void Open(string target) { try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}catch(Exception ex){Error(ex);} }
    string Impact(string op) {
        switch(op) {
            case "cua-repair":return "检查并修复官方桌面应用的 CUA 组件，修复前单独备份。";
            case "proxy":return "更新当前用户 Codex 的代理配置："+ProxySummary()+"。";
            case "config-cleanup":return "先备份用户配置、profile 及所选项目配置，再仅移除 features.code_mode_tool_description_first 和 features.guardianv2.thread_context 的可靠布尔赋值。复杂写法保留并提示。不会重置配置或关闭 Codex；session-flags 警告需单独处理。";
            case "init-rollback":return "恢复最新初始化备份，可能停止 Codex 进程。CUA 和已创建的工作目录不在此回滚范围内。";
            case "guard-uninstall":return "移除保护器管理的登录任务、启动入口与配置。";
            case "guard-native":return "恢复桌面应用自带 CLI，并清理保护器拥有的覆盖配置。请先关闭 Codex 桌面应用。";
            case "guard-rollback":return "将保护器管理的 CLI 恢复到备份版本。请先关闭 Codex 桌面应用。";
            case "guard-current":return "先检查官方独立 CLI，缺失或不完整时从官方渠道准备，再切换。请先退出 Codex。";
            case "official-cli-update":return "更新官方独立 CLI 组件，保留当前 CLI 模式；不安装启动保护或登录任务。请先退出 Codex。";
            case "desktop-update":return "通过 winget 更新官方 Microsoft Store 桌面版（含原生 CLI），最长等待 20 分钟；不自动重启。请先退出 Codex。";
            case "desktop-core-repair":return "请先保存工作并彻底退出 Codex。将核对当前官方桌面版，若当前版本的用户目录缺失，则复制并逐项校验四个启动组件。若目录已有异常文件、版本变化或无法识别，操作会停止，不会强制结束任务或覆盖已有目录。完成后仍需启动 Codex 验证。";
            case "browser-check":return "只读校验 CUA、代理端口及 Node 环境继承探针。真实 Chrome 连接需重启后测试。";
            case "browser-start-prepare":return "请先保存工作并彻底退出 Codex。将检查当前版本和代理设置（"+ProxySummary()+"），必要时备份并修复浏览器入口，然后请求启动官方桌面应用。未知修改或校验失败时停止，不会强制结束任务或关闭 Chrome。";
            case "browser-repair":return "使用当前代理设置："+ProxySummary()+"。请先退出 Codex；校验官方入口后备份并应用补丁。未知结构或哈希异常时保留原文件。不会关闭 Chrome。";
            case "browser-rollback":return "请先退出 Codex。核对当前版本及前后哈希后，恢复本工具备份的原始入口；版本变化时拒绝覆盖。不会关闭 Chrome。";
            case "guard-update":return "从官方渠道下载并更新保护器管理的 CLI。请先关闭 Codex 桌面应用。";
            default:return "安装或修复当前用户的启动保护与登录自动维护任务。";
        }
    }
    async Task Run(string op,string title,bool confirm,Dictionary<string,object> extraOptions=null,string confirmationText=null) {
        if(busy){banner.Text="请等待当前操作完成。";return;}
        var refreshStatus=false;var initialize=op=="initialize";var showInitializationResult=false;var initCancelled=false;var initBrowserRepair=false;var initExitCode=1;var initError="";
        if(initialize){initializationSummary="";initializationState="";progressReadLine=-1;}
        try {
            var configOperation=op=="config-check"||op=="config-cleanup";var coreOperation=op=="desktop-core-check"||op=="desktop-core-repair";
            var localOperation=op=="status"||op=="rollback-delete-all"||coreOperation;
            int proxy=localOperation||configOperation?0:Port(); string disk=op=="initialize"?Drive():Environment.SystemDirectory.Substring(0,1);var options=configOperation?new Dictionary<string,object>{{"projectPath",compatibilityProjectPath},{"proxyHost",Core.DefaultProxyHost}}:op=="initialize"?Options():(localOperation?new Dictionary<string,object>{{"proxyHost",Core.DefaultProxyHost}}:ProxyOptions());
            if(extraOptions!=null)foreach(var pair in extraOptions)options[pair.Key]=pair.Value;
            if(op=="browser-start-prepare" && Core.Admin)throw new InvalidOperationException("请以普通用户权限打开本工具，再使用修复并启动功能。");
            if(confirm && !Confirm(title,confirmationText??Impact(op)))return;
            if(configOperation)compatibilityAdvice="";
            if(coreOperation)desktopCoreAdvice="";
            if(initialize){initBrowserRepair=Convert.ToBoolean(options["browserRepair"]);initializationProgress.Reset(options,guard.IsChecked==true);initializationStarted=DateTime.UtcNow;}
            busy=true; activeOperation=op; operationTitle=title; banner.Text=title+" · 正在启动"; progress.IsIndeterminate=Motion.Enabled(preview);
            if(initialize&&page=="初始化"&&wizard==2)Navigate("初始化");
            UpdateRollbackActions();
            if(op=="browser-start-prepare")browserStartTarget=null;
            if(op=="doctor"){diagnosisContext=null;diagnosticAdvice="";}
            var id=Guid.NewGuid().ToString("N"); lastLog=OperationLogPath(id);rawLogText="";rawDiagnosticText="";chineseLogText="";localizedLines.Clear();showRawLog=false;logInputComplete=false;renderedComplete=false;logViewport.Reset();
            using(var process=StartOperation(op,id,disk,proxy,op=="initialize"&&guard.IsChecked==true,options)) {
                while(!process.HasExited) { await Task.Delay(300); ReadLog();UpdateStageView(); }
                logInputComplete=true;ReadLog(); progress.IsIndeterminate=false;
                if(configOperation){if(compatibilityAdvice.Length==0)compatibilityAdvice=chineseLogText;banner.Text=process.ExitCode==0?"配置检查完成 · 文件结果已显示；会话参数警告需单独验证":"配置处理未全部完成 · 请查看结果和日志";progress.Value=100;repairTab=4;Navigate("连接与修复");}
                else if(coreOperation){if(desktopCoreAdvice.Length==0)desktopCoreAdvice=chineseLogText;banner.Text=process.ExitCode==0?"桌面启动组件检查完成 · 请查看结果":"桌面启动检查或修复未完成 · 请查看原因";progress.Value=100;repairTab=3;Navigate("连接与修复");if(process.ExitCode!=0&&!preview)MessageBox.Show(this,desktopCoreAdvice,title,MessageBoxButton.OK,MessageBoxImage.Warning);}
                else if(op=="doctor"){diagnosticAdvice=DiagnosticAdvice.Build(diagnosisContext,process.ExitCode,rawDiagnosticText);var completed=process.ExitCode==0||DiagnosticAdvice.CompletedReport(rawDiagnosticText);banner.Text=completed?"诊断完成 · 已生成操作建议":"诊断未完成 · 已生成排查建议";if(completed)progress.Value=100;repairTab=0;Navigate("连接与修复");if(!preview)CreateDiagnosticDialog().ShowDialog();}
                else if(op=="browser-start-prepare"){
                    if(process.ExitCode!=0){banner.Text="浏览器准备未完成 · 请查看日志中的原因";progress.Value=100;Navigate("连接与修复");if(!preview)MessageBox.Show(this,banner.Text+"\n\n"+chineseLogText,title,MessageBoxButton.OK,MessageBoxImage.Warning);}
                    else {
                        if(browserStartTarget==null)throw new InvalidDataException("未收到经过校验的桌面应用身份。");
                        var current=await VerifyBrowserStartTarget();
                        var uri=Core.BrowserLaunchUri(browserStartTarget,current);
                        var launch=new ProcessStartInfo("explorer.exe",uri){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden};Process.Start(launch);
                        var reach=Core.Value(browserStartTarget,"reachability","");banner.Text=reach.Contains("不可达")?"已请求启动 Codex · 代理端口不可达，请检查代理":"已请求启动 Codex · 真实浏览器连接仍需验证";progress.Value=100;Navigate("连接与修复");
                    }
                }
                else if(op.StartsWith("browser-")){banner.Text=process.ExitCode==0?"浏览器检查或修复已准备 · 真实连接需重启后验证":"浏览器操作未完成 · 可查看原因后单独重试";progress.Value=100;Navigate("连接与修复");}
                else if(initialize){initExitCode=process.ExitCode;showInitializationResult=true;banner.Text=initExitCode==0?"初始化完成 · 日志已保存":initExitCode==2?"初始化部分完成 · 请重试未完成项目":"初始化未完成 · 请查看日志";if(initExitCode==0||initExitCode==2)progress.Value=100;refreshStatus=true;}
                else if(process.ExitCode!=0) { banner.Text=title+"未完成 · 退出码 "+process.ExitCode+" · 查看日志了解原因"; if(op!="status")MessageBox.Show(this,banner.Text,title,MessageBoxButton.OK,MessageBoxImage.Warning); }
                else {progress.Value=100;banner.Text=title+"完成 · 日志已保存"; if(op=="status" && (page=="概览"||page=="CLI 管理"))Navigate(page); if(!preview&&(op.StartsWith("guard-")||op=="desktop-update"||op=="official-cli-update"))refreshStatus=true;}
            }
        } catch(System.ComponentModel.Win32Exception ex) {
            if(ex.NativeErrorCode==1223){banner.Text="已取消管理员授权，未开始执行。";if(initialize){initCancelled=true;showInitializationResult=true;}}
            else if(initialize){initError=DesktopLocalization.ExceptionText(ex);banner.Text="初始化未完成 · 请查看日志";RecordUiError(ex,false);showInitializationResult=true;}
            else Error(ex);
        } catch(Exception ex) {
            if(initialize){initError=DesktopLocalization.ExceptionText(ex);banner.Text="初始化未完成 · 请查看日志";RecordUiError(ex,false);showInitializationResult=true;}
            else Error(ex);
        }
        finally {busy=false;activeOperation="";progress.IsIndeterminate=false;if(op=="status"&&status==null){cliDetectionComplete=true;UpdateCliDetectionHint();}UpdateRollbackActions();}
        if(showInitializationResult){initializationProgress.Stopped(initExitCode,initError,initCancelled);initializationState=initCancelled?"cancelled":initExitCode==0?"complete":initExitCode==2?"partial":"failed";wizard=2;Navigate("初始化");ShowInitializationResult(initExitCode,initError,initCancelled,initBrowserRepair);}
        if(refreshStatus && !preview) await RefreshStatusQuietly();
        if((op=="rollback-selected"||op=="rollback-delete-all")&&!preview){await RefreshStatusQuietly();if(page=="备份与回滚")Navigate(page);}
    }
    void ReadLog() {
        if(!File.Exists(lastLog))return;
        string text; using(var fs=new FileStream(lastLog,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var reader=new StreamReader(fs,Encoding.UTF8))text=reader.ReadToEnd();
        if(text==rawLogText && chineseLogText.Length>0 && renderedComplete==logInputComplete)return;
        rawLogText=text;renderedComplete=logInputComplete;var display=new StringBuilder();var diagnostic=new StringBuilder();var lines=text.Split('\n');for(int index=0;index<lines.Length;index++) {var line=lines[index];if(!logInputComplete&&index==lines.Length-1&&!text.EndsWith("\n"))break;
            if(line.StartsWith("@@BROWSER_START@@")){try{browserStartTarget=Core.ReadJson(line.Substring(17).Trim());}catch{display.AppendLine("启动身份无法读取。");}}
            else if(line.StartsWith("@@BROWSER@@")){try{browserReport=Core.ReadJson(line.Substring(11).Trim());foreach(var item in new[]{new[]{"patch","浏览器修复状态"},new[]{"integrity","组件完整性"},new[]{"reachability","代理检查"},new[]{"environment","环境检查"}})display.AppendLine(item[1]+"："+DesktopLocalization.TranslateText(Core.Value(browserReport,item[0],"未检查")));}catch{display.AppendLine("浏览器结果无法读取。");}}
            else if(line.StartsWith("@@INIT_RESULT@@")){try{var result=Core.ReadJson(line.Substring(15).Trim());initializationSummary=DesktopLocalization.TranslateText(Core.Value(result,"summary",""),true);if(index>progressReadLine){initializationProgress.Finish(result);initializationState=Core.Value(result,"state","failed");progressReadLine=index;}display.AppendLine(initializationSummary);}catch{display.AppendLine("初始化结果无法读取。");}}
            else if(line.StartsWith("@@CONFIG_COMPAT@@")){try{compatibilityAdvice=DesktopLocalization.TranslateText(Core.Value(Core.ReadJson(line.Substring(17).Trim()),"summary",""),true);}catch{display.AppendLine("配置检查结果无法读取，请查看详细日志。");}}
            else if(line.StartsWith("@@DESKTOP_CORE@@")){try{var report=Core.ReadJson(line.Substring("@@DESKTOP_CORE@@".Length).Trim());desktopCoreAdvice=DesktopLocalization.TranslateText(Core.Value(report,"summary",""),true)+"\n桌面版本："+Core.Value(report,"version","未检测")+"\n近期日志："+DesktopLocalization.TranslateText(Core.Value(report,"signal","未检查"))+"\n组件目录："+Core.Value(report,"target","未检测");display.AppendLine(desktopCoreAdvice);}catch{display.AppendLine("桌面启动检查结果无法读取，请查看详细日志。");}}
            else if(line.StartsWith("@@STATUS@@")) {try{status=Core.ReadJson(line.Substring(10).Trim());ApplyCliDetection(status);}catch{display.AppendLine("状态输出格式无效，请查看原始日志。");} }
            else if(line.StartsWith("@@DIAGNOSIS@@")){try{diagnosisContext=Core.ReadJson(line.Substring(13).Trim());}catch{display.AppendLine("未能读取组件检查结果；建议将依据诊断输出生成。");}}
            else if(line.StartsWith("@@PROGRESS@@")) {try{var p=Core.ReadJson(line.Substring(12).Trim());if(index>progressReadLine){initializationProgress.Apply(p);progressReadLine=index;}banner.Text=ProgressTitle(Core.Value(p,"title",operationTitle));int step,total;if(int.TryParse(Core.Value(p,"step","0"),out step)&&int.TryParse(Core.Value(p,"total","0"),out total)&&total>1){progress.IsIndeterminate=false;progress.Value=100.0*Math.Max(0,step-1)/total;}display.AppendLine(banner.Text);}catch{display.AppendLine("阶段输出格式无效，请查看原始日志。");} }
            else {diagnostic.AppendLine(line.TrimEnd('\r'));display.AppendLine(TranslateLogLine(line.TrimEnd('\r')));}
        }
        rawDiagnosticText=diagnostic.ToString();chineseLogText=display.ToString();
        try{DesktopLocalization.SaveChineseLog(lastLog,chineseLogText);}catch{if(!preview)banner.Text="中文日志暂时无法保存；原始日志仍可查看。";}
        UpdateLogView();UpdateStageView();
    }
    void VerifyChineseLogs(){
        var directory=Path.Combine(Path.GetTempPath(),"CodexKit-log-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
        try{
            lastLog=Path.Combine(directory,"fixture.log");rawLogText="";chineseLogText="";showRawLog=false;logInputComplete=false;
            var original="[FAIL] Codex is running. Save work and fully exit before continuing.\n"+DiagnosticAdvice.AsciiFixture+"\n@@INIT_RESULT@@{\"state\":\"partial\",\"summary\":\"更新桌面版：Desktop update requires restart or is still pending; dependent repairs were paused.\"}\n";
            File.WriteAllText(lastLog,original,new UTF8Encoding(false));ReadLog();
            if(!log.Text.Contains("保存工作并彻底退出")||!initializationSummary.Contains("依赖该版本的修复已暂停")||!rawDiagnosticText.Contains("HTTPS fallback"))throw new InvalidDataException("Localized UI or raw diagnostic input was lost");
            if(File.ReadAllText(lastLog,Encoding.UTF8)!=original||File.ReadAllText(DesktopLocalization.ChineseLogPath(lastLog),Encoding.UTF8)!=chineseLogText)throw new InvalidDataException("Chinese log persistence changed the raw log");
            Navigate("日志");var panel=(StackPanel)((Border)content.Children[0]).Child;var toggle=panel.Children.OfType<WrapPanel>().SelectMany(x=>x.Children.OfType<Button>()).First(x=>Convert.ToString(x.Content)=="查看原始日志");
            toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));if(log.Text!=original||LogToCopy()!=original)throw new InvalidDataException("Raw log view or copy is not exact");
            panel=(StackPanel)((Border)content.Children[0]).Child;toggle=panel.Children.OfType<WrapPanel>().SelectMany(x=>x.Children.OfType<Button>()).First(x=>Convert.ToString(x.Content)=="查看中文日志");toggle.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));if(LogToCopy()!=chineseLogText||showRawLog)throw new InvalidDataException("Chinese view did not restore");
            File.WriteAllText(lastLog,original+"@@DESKTOP_CORE@@{",new UTF8Encoding(false));ReadLog();if(chineseLogText.Contains("桌面启动检查结果无法读取"))throw new InvalidDataException("Partial streaming record was treated as failure");
            File.WriteAllText(lastLog,"[FAIL] Missing file: 中文目录\\codex.exe",new UTF8Encoding(false));logInputComplete=true;ReadLog();if(!chineseLogText.Contains("缺少文件")||!chineseLogText.Contains("中文目录\\codex.exe"))throw new InvalidDataException("Final non-newline message was omitted");
        }finally{lastLog="";rawLogText="";rawDiagnosticText="";chineseLogText="";localizedLines.Clear();showRawLog=false;logInputComplete=false;renderedComplete=false;initializationSummary="";Directory.Delete(directory,true);UpdateLogView();}
    }
    void PumpLogLayout(){
        var root=(FrameworkElement)Content;var width=root.ActualWidth>0?root.ActualWidth:Width;var height=root.ActualHeight>0?root.ActualHeight:Height;root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
        var frame=new DispatcherFrame();
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);root.UpdateLayout();
    }
    void VerifyRollbackSelection(){
        var folder=Directory.CreateDirectory(Path.Combine(Path.GetTempPath(),"CodexKit-rollback-"+Guid.NewGuid().ToString("N"))).FullName;
        var entries=new object[]{new Dictionary<string,object>{{"id",new string('a',64)},{"kind","init"},{"version","备份工具 v2.2.4"},{"time","2026-11-01 14:00:00"},{"available",true},{"path",folder}},new Dictionary<string,object>{{"id",new string('b',64)},{"kind","cli"},{"version","0.165.0"},{"time","2026-10-30 12:00:00"},{"available",true},{"path",Path.Combine(folder,"missing")}},new Dictionary<string,object>{{"id",new string('c',64)},{"kind","cli"},{"available",false},{"path",folder}}};
        try{
            status=new Dictionary<string,object>{{"rollbackEntries",entries},{"rollbackDeleteId",new string('a',64)},{"rollbackDeleteCount",3}};selectedRollbackId="";Navigate("备份与回滚");
            if(rollbackList.Items.Count!=3||rollbackButton.IsEnabled||rollbackFolderButton.IsEnabled||!rollbackDeleteButton.IsEnabled||
               ((SolidColorBrush)rollbackFolderButton.Background).Color!=((SolidColorBrush)MorandiTheme.Disabled).Color)throw new InvalidDataException("Rollback actions did not reflect the empty selection");
            rollbackList.SelectedIndex=0;if(!rollbackButton.IsEnabled||!rollbackFolderButton.IsEnabled||selectedRollbackId!=new string('a',64)||
               ((SolidColorBrush)rollbackFolderButton.Background).Color==((SolidColorBrush)MorandiTheme.Disabled).Color)throw new InvalidDataException("Rollback selection was not retained");
            string opened=null;OpenSelectedRollbackFolder(path=>opened=path);if(opened!=folder)throw new InvalidDataException("Open backup folder did not resolve the selected path");
            var summary=RollbackSummary((Dictionary<string,object>)entries[0]);if(!summary.Contains("2026-11-01 14:00:00")||!summary.Contains("2.2.4")||!summary.Contains("不删除对话"))throw new InvalidDataException("Rollback confirmation lacks its exact target or impact");
            Navigate("备份与回滚");if(rollbackList.SelectedIndex!=0||!rollbackButton.IsEnabled)throw new InvalidDataException("Returning to maintenance lost the rollback selection");
            rollbackList.SelectedIndex=1;summary=RollbackSummary((Dictionary<string,object>)entries[1]);if(!summary.Contains("0.165.0")||!summary.Contains("不会降级 Microsoft Store"))throw new InvalidDataException("CLI rollback confirmation misstates its scope");
            OpenSelectedRollbackFolder(path=>{throw new InvalidDataException("Missing backup directory was opened");});
            if(!banner.Text.Contains("请刷新备份列表"))throw new InvalidDataException("Missing backup folder was not reported");
            rollbackList.SelectedIndex=2;if(rollbackButton.IsEnabled||!rollbackFolderButton.IsEnabled)throw new InvalidDataException("Incomplete backup could not be inspected independently of rollback");
            status=new Dictionary<string,object>();Navigate("备份与回滚");if(rollbackList.Items.Count!=0||rollbackButton.IsEnabled||rollbackFolderButton.IsEnabled||rollbackDeleteButton.IsEnabled)throw new InvalidDataException("Empty backup list enabled actions");
            foreach(var mode in new[]{"DesktopNative","OfficialStandalone"})foreach(int width in new[]{940,1120}){
                status=new Dictionary<string,object>{{"runtimeMode",mode}};Navigate("概览");var root=(FrameworkElement)Content;root.Measure(new Size(width,720));root.Arrange(new Rect(0,0,width,720));root.UpdateLayout();
                var hero=(StackPanel)((Border)content.Children[0]).Child;var notice=hero.Children.OfType<TextBlock>().FirstOrDefault(x=>x.Name=="DataPreservationNotice");var label=notice;var actions=hero.Children.OfType<Grid>().First();var start=actions.Children.OfType<Button>().First();
                if(label==null||label.Text!="本工具不会初始化数据与对话"||label.Foreground!=MutedBrush||Convert.ToString(notice.ToolTip)!="初始化会备份并重建配置，保留现有对话记录和项目文件。")throw new InvalidDataException("Overview data-preservation notice or tooltip is missing");
                if(notice.ActualWidth>=hero.ActualWidth||notice.ActualHeight>20||notice.ActualWidth<label.DesiredSize.Width-label.Margin.Left-label.Margin.Right-1||label.ActualHeight<label.DesiredSize.Height-label.Margin.Top-label.Margin.Bottom-1)throw new InvalidDataException("Overview notice stretched or clipped at width "+width);
                if(Math.Abs(notice.TranslatePoint(new Point(),hero).X-start.TranslatePoint(new Point(),hero).X)>1||notice.TranslatePoint(new Point(),hero).Y<actions.TranslatePoint(new Point(0,actions.ActualHeight),hero).Y+7)throw new InvalidDataException("Overview notice is not below and left-aligned with initialization");
            }
        }finally{status=null;selectedRollbackId="";Directory.Delete(folder);}
    }
    void VerifyLogScrolling(){
        try{
            logViewport.Reset();showRawLog=false;
            chineseLogText=String.Join("\n",Enumerable.Range(1,240).Select(i=>"中文日志 "+i+" "+new string('x',180)))+"\n";rawLogText=chineseLogText.Replace("中文日志","RAW LOG");
            Navigate("日志");UpdateLogView();PumpLogLayout();
            if(log.ExtentHeight<=log.ViewportHeight*3||!logViewport.Following||log.ExtentHeight-log.ViewportHeight-log.VerticalOffset>2)throw new InvalidDataException("Initial log view did not reach the latest line after layout: extent="+log.ExtentHeight+", viewport="+log.ViewportHeight+", offset="+log.VerticalOffset+", following="+logViewport.Following);
            for(int i=0;i<8;i++){chineseLogText+="追加日志 "+i+"\n";UpdateLogView();}PumpLogLayout();
            if(log.ExtentHeight-log.ViewportHeight-log.VerticalOffset>2)throw new InvalidDataException("Rapid log appends stopped following");
            log.ApplyTemplate();var host=(ScrollViewer)log.Template.FindName("PART_ContentHost",log);
            // Route wheel input through the real nested scroll viewer.
            chineseLogText+="滚动回调尚未执行时上翻\n";UpdateLogView();
            host.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=System.Windows.Input.Mouse.PreviewMouseWheelEvent});
            host.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,Environment.TickCount,120){RoutedEvent=System.Windows.Input.Mouse.MouseWheelEvent});PumpLogLayout();
            if(logViewport.Following)throw new InvalidDataException("Mouse wheel up did not pause log following");
            // The scrollbar thumb ultimately requests the same ScrollToVerticalOffset.
            log.ScrollToVerticalOffset(160);log.ScrollToHorizontalOffset(90);PumpLogLayout();
            log.Select(80,12);log.ScrollToVerticalOffset(160);log.ScrollToHorizontalOffset(90);PumpLogLayout();
            double top=log.VerticalOffset,left=log.HorizontalOffset;int start=log.SelectionStart,length=log.SelectionLength;
            chineseLogText+="阅读历史时追加\n";UpdateLogView();PumpLogLayout();
            if(logViewport.Following||Math.Abs(log.VerticalOffset-top)>2||Math.Abs(log.HorizontalOffset-left)>2||log.SelectionStart!=start||log.SelectionLength!=length)throw new InvalidDataException("Append moved the paused viewport or selection");
            // PageUp command uses the actual ScrollViewer command path used by keyboard scrolling.
            System.Windows.Controls.Primitives.ScrollBar.PageUpCommand.Execute(null,host);PumpLogLayout();
            top=log.VerticalOffset;if(logViewport.Following)throw new InvalidDataException("PageUp resumed following");
            chineseLogText+="待处理滚动请求\n";UpdateLogView();Navigate("概览");PumpLogLayout();Navigate("日志");PumpLogLayout();
            if(logViewport.Following||Math.Abs(log.VerticalOffset-top)>2||Math.Abs(log.HorizontalOffset-left)>2)throw new InvalidDataException("Returning to logs lost the reader's position");
            var panel=(StackPanel)((Border)content.Children[0]).Child;
            panel.Children.OfType<WrapPanel>().SelectMany(x=>x.Children.OfType<Button>()).First(x=>Convert.ToString(x.Content)=="查看原始日志").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));PumpLogLayout();
            if(logViewport.Following||Math.Abs(log.VerticalOffset-top)>2||log.Text!=rawLogText)throw new InvalidDataException("Raw log switch lost the paused position");
            panel=(StackPanel)((Border)content.Children[0]).Child;
            panel.Children.OfType<WrapPanel>().SelectMany(x=>x.Children.OfType<Button>()).First(x=>Convert.ToString(x.Content)=="回到最新日志").RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));PumpLogLayout();
            if(!logViewport.Following||log.ExtentHeight-log.ViewportHeight-log.VerticalOffset>2)throw new InvalidDataException("Latest log button did not restore following");
            showRawLog=false;UpdateLogView();Navigate("日志");PumpLogLayout();
            if(!logViewport.Following||log.ExtentHeight-log.ViewportHeight-log.VerticalOffset>2)throw new InvalidDataException("Language switch did not follow the latest log");
            log.ScrollToVerticalOffset(80);PumpLogLayout();log.ScrollToEnd();PumpLogLayout();
            if(!logViewport.Following)throw new InvalidDataException("Manually returning to the bottom did not resume following");
            chineseLogText+="操作完成\n";UpdateLogView();wizard=2;Navigate("初始化");var detail=log.Parent as Expander;if(detail!=null)detail.IsExpanded=true;PumpLogLayout();
            var actions=((StackPanel)((Border)content.Children[0]).Child).Children.OfType<WrapPanel>().SelectMany(x=>x.Children.OfType<Button>());
            if(!actions.Any(x=>Convert.ToString(x.Content)=="回到最新日志")||!logViewport.Following||log.ExtentHeight-log.ViewportHeight-log.VerticalOffset>2)throw new InvalidDataException("Initialization result log did not share bottom following");
            log.ScrollToVerticalOffset(100);PumpLogLayout();logViewport.Reset();chineseLogText="新操作\n";UpdateLogView();PumpLogLayout();
            if(!logViewport.Following||log.VerticalOffset>2||log.HorizontalOffset>2)throw new InvalidDataException("New operation did not reset log following");
        }finally{wizard=0;rawLogText="";chineseLogText="";showRawLog=false;logViewport.Reset();Navigate("日志");}
    }
    async Task RefreshStatusQuietly() {
        try {
            var id=Guid.NewGuid().ToString("N");var statusLog=Path.Combine(Core.Logs,id+".log");
            using(var process=Core.StartWorker("status",id,Environment.SystemDirectory.Substring(0,1),0,false,null)) { while(!process.HasExited) await Task.Delay(100); }
            if(!File.Exists(statusLog))return;
            string text;using(var fs=new FileStream(statusLog,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var reader=new StreamReader(fs,Encoding.UTF8))text=reader.ReadToEnd();
            Dictionary<string,object> refreshed=null;foreach(var line in text.Split('\n'))if(line.StartsWith("@@STATUS@@")){try{refreshed=Core.ReadJson(line.Substring(10).Trim());}catch{} }
            if(refreshed!=null){status=refreshed;ApplyCliDetection(status);if(page=="概览"||page=="CLI 管理")Navigate(page);}
        } catch { }
    }
    async Task<Dictionary<string,object>> VerifyBrowserStartTarget() {
        var id=Guid.NewGuid().ToString("N");var path=Path.Combine(Core.Logs,id+".log");
        using(var process=Core.StartWorker("browser-start-verify",id,Environment.SystemDirectory.Substring(0,1),0,false,null)){
            while(!process.HasExited)await Task.Delay(100);
            if(process.ExitCode!=0)throw new InvalidDataException("重新检查桌面应用失败，请查看日志后重试。");
        }
        var lines=File.ReadAllLines(path,Encoding.UTF8);var line=lines.LastOrDefault(x=>x.StartsWith("@@BROWSER_START@@"));
        if(line==null)throw new InvalidDataException("未收到桌面应用复核结果。");
        return Core.ReadJson(line.Substring(17).Trim());
    }
    static string ProgressTitle(string value) {
        var titles=new Dictionary<string,string>{{"Confirm folder-management drive","确认工作盘"},{"Run full initialization and backup","备份并完成初始化配置"},{"Install folder management and binding","安装文件夹管理并绑定工作区"},{"Check and repair CUA runtime","检查并按需修复 CUA"},{"Install and verify Luna configuration","安装并校验 Luna 配置"},{"Rebuild proxy configuration","重建代理配置"},{"Rollback latest initialization backup","回滚最新初始化备份"},{"Check CUA runtime","检查 CUA"},{"Repair CUA runtime","修复 CUA"}};
        return titles.ContainsKey(value)?titles[value]:DesktopLocalization.TranslateLine(value);
    }
    Window CreateInitializationCompletedDialog(){return CreateInitializationResultDialog(0,"",false,browserRepair.IsChecked==true);}
    Window CreateInitializationResultDialog(int exitCode,string error,bool cancelled,bool browserSelected) {
        var title=cancelled?"已取消初始化":exitCode==0?"已完成初始化":exitCode==2?"初始化部分完成":"初始化未完成";
        var detail=cancelled?"已取消管理员授权，未开始执行初始化。":exitCode==0?"基础配置和所选功能已完成。"+(browserSelected?"浏览器修复已准备，真实连接仍需重启 Codex 后验证。":""):(exitCode==2?"部分项目未完成，请查看下方日志并重试。":"初始化未完成，请查看下方日志了解原因。");
        var summary=String.IsNullOrWhiteSpace(error)?initializationSummary:error;
        if(!cancelled&&exitCode!=0&&!String.IsNullOrWhiteSpace(summary))detail+="\n\n"+summary;
        var dialog=new Window{Title=title,Width=520,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,WindowStyle=WindowStyle.None,ShowInTaskbar=false,Background=BackgroundBrush,Foreground=TextBrush,FontFamily=FontFamily};if(IsVisible)dialog.Owner=this;
        var panel=new StackPanel{Margin=new Thickness(30)};panel.Children.Add(T(title,24,TextBrush));panel.Children.Add(new ScrollViewer{Content=T(detail,14,MutedBrush),MaxHeight=320,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var confirm=Button("确认",()=>dialog.DialogResult=true,true);confirm.IsDefault=true;confirm.HorizontalAlignment=HorizontalAlignment.Right;confirm.Margin=new Thickness(0,16,0,0);panel.Children.Add(confirm);
        dialog.Content=new Border{Background=PanelBrush,BorderBrush=B("#39465D"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Child=panel};Motion.Dialog(dialog,preview);return dialog;
    }
    Window CreateDiagnosticDialog(){
        var dialog=new Window{Title="诊断建议",Width=660,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false,Background=BackgroundBrush,Foreground=TextBrush,FontFamily=FontFamily,FontSize=14};if(IsVisible)dialog.Owner=this;foreach(var dictionary in Resources.MergedDictionaries)dialog.Resources.MergedDictionaries.Add(dictionary);
        var panel=new StackPanel{Margin=new Thickness(26)};panel.Children.Add(T("诊断助手",24,TextBrush));panel.Children.Add(new SmoothScrollViewer{Content=T(diagnosticAdvice,14,TextBrush),MaxHeight=380,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});Row(panel,Button("查看详细日志",()=>{dialog.Close();Navigate("日志");},false),Button("知道了",()=>dialog.Close(),true));dialog.Content=new Border{Background=PanelBrush,Child=panel};return dialog;
    }
    void ShowInitializationResult(int exitCode,string error,bool cancelled,bool browserSelected) {
        if(preview&&PresentInitializationDialog==null)return;
        var dialog=CreateInitializationResultDialog(exitCode,error,cancelled,browserSelected);
        if(PresentInitializationDialog!=null)PresentInitializationDialog(dialog);else dialog.ShowDialog();
    }
    async Task CheckUpdates(bool silent) {
        if(preview){updateStatus.Text="设计预览不检查在线更新";return;}
        if(checkingUpdate)return;
        try {checkingUpdate=true;updateStatus.Text="自动检查更新中…"; var proxy=Port();var host=ProxyHost(); var network=NetworkOptions();ResetGitHubLog();update=await Task.Run(()=>Core.CheckUpdate(proxy,host,network,GitHubLog));ApplyUpdateState();RefreshUpdatePage();}
        catch(Exception ex){updateStatus.Text="更新检查未完成";updateStatus.ToolTip=DesktopLocalization.ExceptionText(ex);RecordUiError(ex,false);if(page=="更新与下载"&&updatePageDescription!=null)updatePageDescription.Text="检查未完成，可在下载记录中查看原因后重试。";}
        finally {checkingUpdate=false;}
    }
    void ResetGitHubLog(){githubLogText="";githubLogView.Text="";}
    void GitHubLog(string message){Dispatcher.BeginInvoke(new Action(()=>{githubLogText+=message+Environment.NewLine;githubLogView.AppendText(message+Environment.NewLine);githubLogView.ScrollToEnd();}));}
    void ApplyUpdateState() {
        var newer=update!=null && new Version(Core.Value(update,"version","0.0.0"))>new Version(Core.Version);
        updateNotice.Visibility=newer?Visibility.Visible:Visibility.Collapsed;updateNotice.ToolTip=newer?"更新到 v"+Core.Value(update,"version",""):null;
        updateStatus.Text=newer?"检测到更新 v"+Core.Value(update,"version",""):"已是最新版本";updateStatus.ToolTip=null;
    }
    async Task InstallUpdate() {
        if(preview){banner.Text="设计预览不执行下载更新。";return;}
        if(busy){banner.Text="请等待当前操作完成后再更新。";return;}
        if(update==null || new Version(Core.Value(update,"version","0.0.0"))<=new Version(Core.Version))return;
        try{busy=true;updateNotice.IsEnabled=false;progress.IsIndeterminate=true;updateStatus.Text="正在下载并校验更新…";var stage=await Core.DownloadUpdate(update,Port(),ProxyHost(),NetworkOptions(),GitHubLog);Core.ScheduleUpdate(stage);busy=false;Application.Current.Shutdown();}
        catch(Exception ex){updateStatus.Text="更新未完成，可点击重试";Error(ex);}finally{busy=false;updateNotice.IsEnabled=true;progress.IsIndeterminate=false;}
    }
}
}
