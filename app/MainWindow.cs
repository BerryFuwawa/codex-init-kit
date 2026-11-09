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
public class MainWindow : Window {
    static readonly Brush BackgroundBrush=B("#10141E"), PanelBrush=B("#1B2230"), TextBrush=B("#EDF2FC"), MutedBrush=B("#A4B0C5"), Accent=B("#9EBCFF");
    readonly StackPanel content=new StackPanel();
    readonly TextBlock heading=new TextBlock(), subtitle=new TextBlock(), banner=new TextBlock();
    readonly TextBlock updateStatus=new TextBlock();
    Button updateNotice;
    readonly TextBox log=new TextBox();
    readonly LogViewport logViewport;
    ListBox rollbackList;
    Button rollbackButton;
    string selectedRollbackId="";
    readonly ProgressBar progress=new ProgressBar();
    readonly List<Button> navigation=new List<Button>();
    readonly ComboBox drive=new ComboBox();
    readonly TextBox port=new TextBox();
    readonly TextBox proxyHost=new TextBox();
    readonly CheckBox guard=new CheckBox();
    readonly CheckBox proxyEnabled=new CheckBox(),folderManagement=new CheckBox(),subagents=new CheckBox(),cuaRepair=new CheckBox();
    readonly CheckBox browserRepair=new CheckBox(),officialCliUpdate=new CheckBox(),desktopUpdate=new CheckBox();
    readonly CheckBox antiOverDefense=new CheckBox(),workJournal=new CheckBox(),commandLineFirst=new CheckBox(),inheritPermissions=new CheckBox(),memories=new CheckBox(),githubMirrors=new CheckBox();
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
        status=new Dictionary<string,object>{{"runtimeMode","DesktopNative"}};GuideOfficialCli();HighlightOfficialSwitch();if(page!="维护恢复"||Convert.ToString(officialSwitch.Content)!="切换官方独立 CLI"||busy||officialSwitch.Background!=Accent)throw new InvalidOperationException("Recovery navigation or reduced-motion highlight failed");
        var newDefaults=Options();foreach(var key in new[]{"browserRepair","officialCliUpdate","desktopUpdate","antiOverDefense","workJournal","commandLineFirst"})if(!(bool)newDefaults[key])throw new InvalidOperationException("New default disabled: "+key);
        antiOverDefense.IsChecked=false;if((bool)Options()["antiOverDefense"]||!InitSummary().Contains("防止过度防御：跳过"))throw new InvalidOperationException("Anti-over-defense selection or confirmation was not preserved");antiOverDefense.IsChecked=true;if(!InitSummary().Contains("HERO 中文精简规则"))throw new InvalidOperationException("Anti-over-defense confirmation was missing");
        workJournal.IsChecked=false;if((bool)Options()["workJournal"]||!InitSummary().Contains("按需笔记与文档管理：跳过"))throw new InvalidOperationException("Work journal selection or confirmation was not preserved");workJournal.IsChecked=true;
        commandLineFirst.IsChecked=false;
        if((bool)Options()["commandLineFirst"]||!(bool)Options()["workJournal"]||!InitSummary().Contains("优先使用命令行：跳过"))
            throw new InvalidOperationException("Command-line preference was not independent or missing from confirmation");
        commandLineFirst.IsChecked=true;
        Navigate("初始化");var featurePanel=(StackPanel)((Border)content.Children[1]).Child;var featureChoices=InitializationFlowTests.Descendants<CheckBox>(featurePanel).ToArray();if(featureChoices[0]!=folderManagement||featureChoices[1]!=antiOverDefense||featureChoices[2]!=workJournal||featureChoices[3]!=commandLineFirst||featureChoices[4]!=inheritPermissions||featureChoices[5]!=memories||inheritPermissions.IsChecked!=true||workJournal.IsChecked!=true||commandLineFirst.IsChecked!=true||memories.IsChecked!=true||featureChoices[featureChoices.Length-2]!=desktopUpdate||featureChoices.Last()!=subagents||antiOverDefense.IsChecked!=true||subagents.IsChecked!=false)throw new InvalidOperationException("Initialization checkbox order or defaults changed");
        var cliChoices=featurePanel.Children.OfType<WrapPanel>().SelectMany(x=>x.Children.OfType<RadioButton>()).ToArray();if(!cliChoices.SequenceEqual(new[]{cliNative,cliStandalone})||cliNative.IsChecked!=true||cliStandalone.IsChecked==true||Convert.ToString(Options()["cliMode"])!="auto"||!InitSummary().Contains("初始化后使用的 CLI：桌面版原生 CLI"))throw new InvalidDataException("Detected native CLI choice or confirmation is incorrect");
        ApplyCliDetection(new Dictionary<string,object>{{"runtimeMode","OfficialStandalone"}});if(cliStandalone.IsChecked!=true||cliNative.IsChecked==true||Convert.ToString(Options()["cliMode"])!="auto"||!InitSummary().Contains("初始化后使用的 CLI：官方独立 CLI"))throw new InvalidDataException("Detected standalone CLI was not selected automatically");
        cliNative.IsChecked=true;ApplyCliDetection(new Dictionary<string,object>{{"runtimeMode","OfficialStandalone"}});if(cliNative.IsChecked!=true||Convert.ToString(Options()["cliMode"])!="desktop-native")throw new InvalidDataException("Status refresh replaced the manual CLI choice");Navigate("概览");Navigate("初始化");if(cliNative.IsChecked!=true)throw new InvalidDataException("Navigation lost the selected CLI");
        cliChoiceManual=false;ApplyCliDetection(new Dictionary<string,object>{{"runtimeMode","Unknown"}});rejected=false;try{RequireCliChoice();}catch(InvalidOperationException){rejected=true;}if(!rejected||cliNative.IsChecked==true||cliStandalone.IsChecked==true)throw new InvalidDataException("Unknown CLI mode silently selected a default");cliStandalone.IsChecked=true;RequireCliChoice();if(Convert.ToString(Options()["cliMode"])!="official-standalone")throw new InvalidDataException("Manual selection after unknown detection failed");ResetCliChoice();
        Navigate("浏览器连接");var browserSettings=(StackPanel)((Border)content.Children[1]).Child;var browserInputs=browserSettings.Children.OfType<TextBox>().ToArray();var browserToggle=browserSettings.Children.OfType<CheckBox>().First();var browserButtons=browserSettings.Children.OfType<WrapPanel>().SelectMany(x=>x.Children.OfType<Button>()).Select(x=>Convert.ToString(x.Content)).ToArray();if(!browserButtons.Contains("修复并启动 Codex")||browserInputs.Length!=2)throw new InvalidOperationException("Browser startup entry is missing");browserInputs[0].Text="192.168.5.8";browserInputs[1].Text="7890";if(ProxyHost()!="192.168.5.8"||Port()!=7890)throw new InvalidOperationException("Browser proxy settings did not synchronize");browserToggle.IsChecked=false;if(proxyEnabled.IsChecked!=false)throw new InvalidOperationException("Browser proxy switch did not synchronize");browserToggle.IsChecked=true;proxyHost.Text="";port.Text="10808";
        VerifyMaintenanceModes();
        memories.IsChecked=false;if((bool)Options()["memories"]||!InitSummary().Contains("关闭，保留已有记忆文件"))throw new InvalidDataException("Memory selection was not preserved");memories.IsChecked=true;
        if(!githubMirrors.IsChecked.Value||NetworkOptions().customGithubMirror.Length!=0)throw new InvalidDataException("GitHub mirror defaults changed");
        githubCustomMirror.Text="invalid mirror";githubMirrors.IsChecked=false;if(NetworkOptions().customGithubMirror.Length!=0||githubCustomMirror.IsEnabled)throw new InvalidDataException("Disabled mirrors validated an ignored prefix");githubCustomMirror.Text="";githubMirrors.IsChecked=true;
        parentEffort.ItemTemplate.Seal();var effortLabel=(TextBlock)parentEffort.ItemTemplate.LoadContent();effortLabel.DataContext="medium";effortLabel.Measure(new Size(200,40));effortLabel.Arrange(new Rect(0,0,200,40));effortLabel.UpdateLayout();effortLabel.GetBindingExpression(TextBlock.TextProperty).UpdateTarget();if(effortLabel.Text!="中（medium）"||Convert.ToString(Options()["parentEffort"])!="medium"||!InitSummary().Contains("gpt-6.1-sol / 中（medium）"))throw new InvalidDataException("Effort labels changed the underlying config value");
        foreach(var effort in new[]{"none","minimal","low","medium","high","xhigh","max","ultra"})if(!EffortLabels.Display(effort).EndsWith("（"+effort+"）"))throw new InvalidDataException("Effort label did not include its original identifier: "+effort);
        VerifyChineseLogs();
        VerifyLogScrolling();
        VerifyRollbackSelection();
        foreach(var name in new[]{"概览","初始化","浏览器连接","维护恢复","更新","日志","初始化"}) { Navigate(name); Measure(new Size(1120,800)); Arrange(new Rect(0,0,1120,800)); UpdateLayout(); }
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
    public void RenderPages(string directory) {
        Directory.CreateDirectory(directory);
        foreach(var name in new[]{"概览","初始化","浏览器连接","维护恢复","更新","日志"}) {
            Navigate(name);var view=(FrameworkElement)Content;view.Measure(new Size(1120,800));view.Arrange(new Rect(0,0,1120,800));view.UpdateLayout();
            var bitmap=new RenderTargetBitmap(1120,800,96,96,PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(directory,name+".png")))encoder.Save(file);
        }
        wizard=0;Navigate("初始化");var settingsView=(FrameworkElement)Content;settingsView.Measure(new Size(1120,1400));settingsView.Arrange(new Rect(0,0,1120,1400));settingsView.UpdateLayout();var settingsBitmap=new RenderTargetBitmap(1120,1400,96,96,PixelFormats.Pbgra32);settingsBitmap.Render(settingsView);var settingsEncoder=new PngBitmapEncoder();settingsEncoder.Frames.Add(BitmapFrame.Create(settingsBitmap));using(var file=File.Create(Path.Combine(directory,"完整设置.png")))settingsEncoder.Save(file);
        update=new Dictionary<string,object>{{"version",new Version(new Version(Core.Version).Major,new Version(Core.Version).Minor,new Version(Core.Version).Build+1).ToString()}};ApplyUpdateState();Navigate("概览");var updateView=(FrameworkElement)Content;updateView.Measure(new Size(1120,800));updateView.Arrange(new Rect(0,0,1120,800));updateView.UpdateLayout();var updateBitmap=new RenderTargetBitmap(1120,800,96,96,PixelFormats.Pbgra32);updateBitmap.Render(updateView);var updateEncoder=new PngBitmapEncoder();updateEncoder.Frames.Add(BitmapFrame.Create(updateBitmap));using(var file=File.Create(Path.Combine(directory,"检测到更新.png")))updateEncoder.Save(file);update=null;ApplyUpdateState();
    }
    public MainWindow(bool isPreview) {
        logViewport=new LogViewport(log);
        preview=isPreview;modelCatalog=ModelCatalog.Preset(); Title="Codex Init Kit"; Width=1120; Height=800; MinWidth=940; MinHeight=720; WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Icon=Brand.Logo();
        Background=BackgroundBrush; Foreground=TextBrush; FontFamily=new FontFamily("Microsoft YaHei UI"); FontSize=14;
        using(var styles=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("UI.Controls.xaml"))Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Load(styles));
        var layout=new Grid{Background=BackgroundBrush}; layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(220)}); layout.ColumnDefinitions.Add(new ColumnDefinition()); Content=layout;
        var rail=new Grid{Background=B("#151B27")}; rail.RowDefinitions.Add(new RowDefinition{Height=new GridLength(130)}); rail.RowDefinitions.Add(new RowDefinition()); rail.RowDefinitions.Add(new RowDefinition{Height=new GridLength(130)}); layout.Children.Add(rail);
        var brand=new StackPanel{Margin=new Thickness(25,18,15,10)}; brand.Children.Add(new Image{Source=Brand.Logo(),Width=48,Height=48,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,8)}); brand.Children.Add(T("CODEX INIT KIT",16,TextBrush)); brand.Children.Add(T("Windows 初始化与维护",11,MutedBrush)); rail.Children.Add(brand);
        var nav=new StackPanel{Margin=new Thickness(14,12,14,0)}; Grid.SetRow(nav,1); rail.Children.Add(nav);
        foreach(var name in new[]{"概览","初始化","浏览器连接","维护恢复","更新","日志"}) { var button=Button(name,async()=>{Navigate(name);if(name=="维护恢复"&&!preview&&!busy)await RefreshStatusQuietly();},false); button.HorizontalContentAlignment=HorizontalAlignment.Left; button.Margin=new Thickness(0,0,0,8); button.Padding=new Thickness(17,14,12,14); nav.Children.Add(button); navigation.Add(button); }
        var foot=new StackPanel{Margin=new Thickness(20,10,12,15)};
        updateNotice=Button("",async()=>await InstallUpdate(),false);updateNotice.Background=Brushes.Transparent;updateNotice.Padding=new Thickness(0,6,0,8);updateNotice.Visibility=Visibility.Collapsed;
        var noticeContent=new StackPanel{Orientation=Orientation.Horizontal};noticeContent.Children.Add(new Ellipse{Width=8,Height=8,Fill=B("#FF526C"),Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center,Effect=new DropShadowEffect{Color=Color.FromRgb(255,65,95),BlurRadius=10,ShadowDepth=0,Opacity=.95}});noticeContent.Children.Add(T("检测到更新，点击更新",12,TextBrush));updateNotice.Content=noticeContent;
        foot.Children.Add(T("v"+Core.Version,12,MutedBrush)); foot.Children.Add(Link("GitHub 项目 ↗",()=>Open("https://github.com/"+Core.Repository))); Grid.SetRow(foot,2); rail.Children.Add(foot);
        var main=new Grid{Margin=new Thickness(32,25,32,20)}; main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto}); main.RowDefinitions.Add(new RowDefinition()); main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto}); Grid.SetColumn(main,1); layout.Children.Add(main);
        var title=new StackPanel(); heading.FontSize=28; heading.FontWeight=FontWeights.SemiBold; subtitle.Foreground=MutedBrush; subtitle.Margin=new Thickness(0,8,0,22); subtitle.TextWrapping=TextWrapping.Wrap; title.Children.Add(heading); title.Children.Add(subtitle); main.Children.Add(title);
        var scroll=new SmoothScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=content}; Grid.SetRow(scroll,1); main.Children.Add(scroll);
        var bottom=new StackPanel{Margin=new Thickness(0,15,0,0)}; banner.Foreground=MutedBrush; banner.TextWrapping=TextWrapping.Wrap; banner.Text="准备就绪 · 所有更改都会先展示确认摘要"; banner.Cursor=System.Windows.Input.Cursors.Hand;banner.ToolTip="点击查看执行日志";banner.MouseLeftButtonUp+=(s,e)=>Navigate("日志");
        var footerLine=new Grid();footerLine.ColumnDefinitions.Add(new ColumnDefinition());footerLine.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});banner.Margin=new Thickness(0,0,16,0);footerLine.Children.Add(banner);updateStatus.Foreground=MutedBrush;updateStatus.FontSize=12;updateStatus.Text="自动检查更新";updateStatus.VerticalAlignment=VerticalAlignment.Bottom;var updateArea=new StackPanel{HorizontalAlignment=HorizontalAlignment.Right};updateNotice.HorizontalAlignment=HorizontalAlignment.Right;updateNotice.Margin=new Thickness(0);updateArea.Children.Add(updateNotice);updateArea.Children.Add(updateStatus);Grid.SetColumn(updateArea,1);footerLine.Children.Add(updateArea);
        progress.Height=3; progress.Margin=new Thickness(0,10,0,0); progress.Maximum=100; bottom.Children.Add(footerLine); bottom.Children.Add(progress); Grid.SetRow(bottom,2); main.Children.Add(bottom);
        log.IsReadOnly=true; log.AcceptsReturn=true; log.TextWrapping=TextWrapping.NoWrap; log.VerticalScrollBarVisibility=ScrollBarVisibility.Auto; log.HorizontalScrollBarVisibility=ScrollBarVisibility.Auto; log.FontFamily=new FontFamily("Consolas"); log.FontSize=12; log.Background=B("#101620"); log.Foreground=TextBrush; log.BorderThickness=new Thickness(0); log.Padding=new Thickness(14); log.Height=350;
        drive.ItemsSource=DriveInfo.GetDrives().Where(x=>x.DriveType==DriveType.Fixed && x.IsReady).Select(x=>x.Name.Substring(0,1)).ToArray(); drive.SelectedItem="D"; if(drive.SelectedIndex<0) drive.SelectedIndex=0;
        drive.Style=(Style)Resources["KitCombo"];drive.FontSize=15;drive.Width=320;drive.HorizontalAlignment=HorizontalAlignment.Left;
        foreach(var input in new[]{port,proxyHost})input.Style=(Style)Resources["KitInput"];
        port.Text="10808";port.Width=320;port.HorizontalAlignment=HorizontalAlignment.Left;
        proxyHost.Text="";proxyHost.Width=320;proxyHost.HorizontalAlignment=HorizontalAlignment.Left;proxyHost.ToolTip="留空使用 127.0.0.1。软路由在局域网其他设备时，请填写可访问的路由器 IP 地址；本工具不会启动代理服务。";
        inheritPermissions.ToolTip="初始化时保留全局配置中的审批、文件与网络权限；未设置时使用 Codex 默认行为。";
        var checks=new[]{proxyEnabled,folderManagement,antiOverDefense,workJournal,commandLineFirst,inheritPermissions,memories,guard,cuaRepair,browserRepair,officialCliUpdate,desktopUpdate,subagents};var labels=new[]{"启用 HTTP 代理","文件夹管理功能","防止过度防御","按需笔记与文档管理","优先使用命令行","操作权限继承","启用 Codex 记忆","CLI 启动保护与修复","CUA 检查与修复","浏览器链接修复","更新官方CLI","更新桌面版（含原生CLI）","子代理多线程"};for(int i=0;i<checks.Length;i++){checks[i].Style=(Style)Resources["KitCheck"];checks[i].Content=labels[i];checks[i].IsChecked=checks[i]!=subagents;}
        cliNative.Content="桌面版原生 CLI";cliStandalone.Content="官方独立 CLI";foreach(var choice in new[]{cliNative,cliStandalone}){choice.GroupName="InitializationCliMode";choice.Style=(Style)Resources["KitCliChoice"];choice.Checked+=(s,e)=>{if(!applyingCliDetection){cliChoiceManual=true;UpdateCliDetectionHint();}};choice.Click+=(s,e)=>{cliChoiceManual=true;UpdateCliDetectionHint();};}
        cliDetectionHint.FontSize=12;cliDetectionHint.Foreground=MutedBrush;cliDetectionHint.TextWrapping=TextWrapping.Wrap;cliDetectionHint.Margin=new Thickness(0,4,0,8);
        if(preview)ApplyCliDetection(new Dictionary<string,object>{{"runtimeMode","DesktopNative"}});else UpdateCliDetectionHint();
        antiOverDefense.ToolTip="在全局 AGENTS.md 中写入 HERO 中文精简规则，以及实现、防御和自主交付规则。";
        workJournal.ToolTip="按需维护 Markdown 笔记、文档和索引，记录可复用的排查经验。";
        commandLineFirst.ToolTip="有可行命令时优先使用专用 CLI 或 PowerShell，由 Codex 直接执行并核验。";
        memories.ToolTip="初始化时开启本地 Codex 记忆生成与使用；取消后关闭，但不删除已有记忆文件。";
        githubMirrors.Content="自动使用 GitHub 镜像（官方优先，失败后串行切换）";githubMirrors.Style=(Style)Resources["KitCheck"];
        githubCustomMirror.Style=(Style)Resources["KitInput"];githubCustomMirror.ToolTip="可选 HTTPS 镜像前缀，例如 https://example.com/proxy/";
        var network=new GitHubNetworkOptions();if(!preview)try{network=GitHubNetworkOptions.Load(Core.NetworkSettingsPath);}catch(Exception ex){networkSettingsError="下载设置未能读取，请重新保存："+ex.Message;}
        githubMirrors.IsChecked=network.githubMirrorEnabled;githubCustomMirror.Text=network.customGithubMirror;githubCustomMirror.IsEnabled=network.githubMirrorEnabled;
        githubMirrors.Checked+=(a,b)=>githubCustomMirror.IsEnabled=true;githubMirrors.Unchecked+=(a,b)=>githubCustomMirror.IsEnabled=false;
        githubLogView.IsReadOnly=true;githubLogView.TextWrapping=TextWrapping.Wrap;githubLogView.Height=150;githubLogView.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;githubLogView.Background=B("#101620");githubLogView.Foreground=TextBrush;githubLogView.BorderThickness=new Thickness(0);githubLogView.Padding=new Thickness(10);
        proxyEnabled.Checked+=(s,e)=>{port.IsEnabled=true;proxyHost.IsEnabled=true;};proxyEnabled.Unchecked+=(s,e)=>{port.IsEnabled=false;proxyHost.IsEnabled=false;};
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
        UpdateModelInputs();
        Closing+=(s,e)=>{if(busy){e.Cancel=true; MessageBox.Show(this,"操作正在执行，请等待结果后再关闭窗口。","正在执行",MessageBoxButton.OK,MessageBoxImage.Information);}};
        Navigate("概览"); Loaded+=async(s,e)=>{if(!preview) await Task.WhenAll(Run("status","读取当前状态",false),CheckUpdates(true));};
    }
    static Brush B(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
    static TextBlock T(string text,int size,Brush color) { return new TextBlock{Text=text,FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)}; }
    Button Button(string label,Action action,bool primary) {
        var b=new Button{Content=label,Background=primary?Accent:B("#293348"),Foreground=primary?B("#101B31"):TextBrush,BorderThickness=new Thickness(0),Padding=new Thickness(18,10,18,10),Cursor=System.Windows.Input.Cursors.Hand,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,4,10,4)};
        var factory=new FrameworkElementFactory(typeof(Border));factory.Name="ButtonSurface";factory.SetValue(Border.BorderThicknessProperty,new Thickness(1));factory.SetValue(Border.BorderBrushProperty,Brushes.Transparent); factory.SetValue(Border.CornerRadiusProperty,new CornerRadius(8)); factory.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.MarginProperty,new TemplateBindingExtension(Control.PaddingProperty)); presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty)); factory.AppendChild(presenter);var template=new ControlTemplate(typeof(Button)){VisualTree=factory};
        var focus=new Trigger{Property=UIElement.IsKeyboardFocusedProperty,Value=true};focus.Setters.Add(new Setter(Border.BorderBrushProperty,B("#70DFC0"),"ButtonSurface"));template.Triggers.Add(focus);b.Template=template;
        b.MouseEnter+=(s,e)=>b.Opacity=.8; b.MouseLeave+=(s,e)=>b.Opacity=1; b.Click+=(s,e)=>action(); return b;
    }
    Button Link(string label,Action action) { var b=Button(label,action,false); b.Background=Brushes.Transparent; b.Foreground=Accent; b.Padding=new Thickness(0,6,0,6); b.FontSize=12; return b; }
    StackPanel Card(string title,string description) { var p=new StackPanel{Margin=new Thickness(22,18,22,18)}; p.Children.Add(T(title,18,TextBrush)); if(description.Length>0)p.Children.Add(T(description,13,MutedBrush)); var box=new Border{Background=PanelBrush,CornerRadius=new CornerRadius(12),Margin=new Thickness(0,0,0,14),Child=p}; content.Children.Add(box); return p; }
    Border FeatureGroup(string title,params CheckBox[] choices) {
        var panel=new StackPanel();
        var label=T(title,13,Accent);label.FontWeight=FontWeights.SemiBold;label.Margin=new Thickness(0,0,0,6);panel.Children.Add(label);
        foreach(var choice in choices)panel.Children.Add(choice);
        return new Border{Background=B("#151C28"),BorderBrush=B("#2D394D"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(9),Padding=new Thickness(14,12,14,12),Margin=new Thickness(0,0,0,10),Child=panel};
    }
    void FeatureGroups(StackPanel parent) {
        var grid=new Grid{Margin=new Thickness(0,8,0,0)};
        grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
        var groups=new[]{FeatureGroup("工作与规则",folderManagement,antiOverDefense,workJournal,commandLineFirst),FeatureGroup("权限与记忆",inheritPermissions,memories),FeatureGroup("修复与连接",guard,cuaRepair,browserRepair),FeatureGroup("组件更新",officialCliUpdate,desktopUpdate)};
        for(int index=0;index<groups.Length;index++){
            var group=groups[index];Grid.SetRow(group,index/2);Grid.SetColumn(group,index%2);
            if(index%2==0)group.Margin=new Thickness(0,0,10,10);grid.Children.Add(group);
        }
        parent.Children.Add(grid);
        var optional=new StackPanel();optional.Children.Add(subagents);optional.Children.Add(T("默认关闭，启用后可设置子模型与思考强度。",12,MutedBrush));
        parent.Children.Add(new Border{Background=B("#151C28"),CornerRadius=new CornerRadius(9),Padding=new Thickness(14,8,14,10),Child=optional});
    }
    void Row(StackPanel parent,params UIElement[] items) { var row=new WrapPanel(); foreach(var item in items)row.Children.Add(item); parent.Children.Add(row); }
    void Navigate(string name) { logViewport.Detach();foreach(var control in new FrameworkElement[]{drive,port,proxyHost,guard,log,githubMirrors,githubCustomMirror,githubLogView,proxyEnabled,folderManagement,antiOverDefense,workJournal,commandLineFirst,inheritPermissions,memories,subagents,cuaRepair,browserRepair,officialCliUpdate,desktopUpdate,cliNative,cliStandalone,cliDetectionHint,parentModel,childModel,parentEffort,childEffort,modelCheck}) { var parent=control.Parent as Panel; if(parent!=null)parent.Children.Remove(control); } page=name; content.Children.Clear(); heading.Text=name; foreach(var b in navigation){b.Background=Convert.ToString(b.Content)==name?B("#2E4063"):Brushes.Transparent; b.Foreground=Convert.ToString(b.Content)==name?Accent:MutedBrush;}
        if(name=="概览")Overview(); else if(name=="初始化")Wizard(); else if(name=="浏览器连接")BrowserConnection(); else if(name=="维护恢复")Maintenance(); else if(name=="更新")Updates(); else LogsPage();
        if(log.Parent!=null)logViewport.Attach();
    }
    void Overview() {
        subtitle.Text="把工作区、模型设置、CUA 修复与启动保护放在一起。";
        var hero=Card("让 Codex 准备好开始工作","按步骤配置工作区，完成文件夹管理后执行 CUA 检查与修复。每一步都有日志，关键更改先确认。");
        var actions=new Grid();for(int i=0;i<3;i++)actions.ColumnDefinitions.Add(new ColumnDefinition{Width=i<2?GridLength.Auto:new GridLength(1,GridUnitType.Star)});
        var start=Button("开始初始化  →",()=>{if(activeOperation!="initialize"&&wizard==2)ResetCliChoice();wizard=activeOperation=="initialize"?2:0;Navigate("初始化");},true);actions.Children.Add(start);
        var refresh=Button("刷新状态",async()=>await Run("status","读取当前状态",false),false);Grid.SetColumn(refresh,1);actions.Children.Add(refresh);
        recoveryGuide=Button("遇到了未知错误？尝试切换官方独立CLI",()=>GuideOfficialCli(),false);recoveryGuide.Background=B("#AA344B");recoveryGuide.Foreground=Brushes.White;recoveryGuide.HorizontalAlignment=HorizontalAlignment.Right;recoveryGuide.MaxWidth=300;recoveryGuide.Padding=new Thickness(12,10,12,10);recoveryGuide.Content=new TextBlock{Text="遇到了未知错误？尝试切换官方独立CLI",TextWrapping=TextWrapping.Wrap,FontSize=12};recoveryGuide.Visibility=Core.Value(status,"runtimeMode","")=="DesktopNative"?Visibility.Visible:Visibility.Collapsed;Grid.SetColumn(recoveryGuide,2);actions.Children.Add(recoveryGuide);hero.Children.Add(actions);
        var noticeText=new TextBlock{Name="DataPreservationNotice",Text="本工具不会初始化数据与对话",FontSize=12,FontWeight=FontWeights.Normal,Foreground=Brushes.White,TextWrapping=TextWrapping.NoWrap,Margin=new Thickness(0,8,0,0),HorizontalAlignment=HorizontalAlignment.Left,ToolTip="初始化会备份并重建配置，保留现有对话记录和项目文件。"};
        hero.Children.Add(noticeText);
        var p=Card("当前环境",status==null?(preview?"界面预览 · 未执行检测":"正在读取，或点击上方刷新状态。"):preview?"界面预览 · 示例状态（非真实检测）":"最近一次检测结果");
        if(status!=null) {
            var labels=new Dictionary<string,string>{{"desktopVersion","桌面版本"},{"desktopHealth","组件状态"},{"runtimeMode","当前 CLI 模式"},{"runtimeModeDetail","运行时说明"},{"currentCliPath","当前 CLI 路径"},{"configModel","当前模型"},{"configEffort","当前推理强度"},{"protectionInstalled","启动保护已安装"},{"processRunning","Codex 正在运行"},{"backupPath","初始化备份"}};
            foreach(var pair in labels) { var value=status.ContainsKey(pair.Key)?status[pair.Key]:null;var display=pair.Key=="configEffort"?EffortLabels.Display(Convert.ToString(value)):pair.Key=="runtimeMode"?RuntimeMode(value):pair.Key=="currentCliPath"&&String.IsNullOrWhiteSpace(Convert.ToString(value))&&Convert.ToString(status.ContainsKey("runtimeMode")?status["runtimeMode"]:null)=="DesktopNative"?"由桌面应用自动选择":Format(value);var row=T(pair.Value+"   "+display,pair.Key=="runtimeMode"?16:13,pair.Key=="runtimeMode"?Accent:MutedBrush);if(pair.Key=="runtimeMode")row.FontWeight=FontWeights.SemiBold;p.Children.Add(row); }
        }
        var model=Card("初始化模型","默认读取现有全局模型设置，未设置的项目使用默认值；子代理默认关闭。");
        model.Children.Add(T(modelSettingsError.Length>0?modelSettingsError:"父模型  "+parentModel.SelectedItem+" / "+EffortLabels.Display(Convert.ToString(parentEffort.SelectedItem))+"    ·    子代理  "+childModel.SelectedItem+" / "+EffortLabels.Display(Convert.ToString(childEffort.SelectedItem)),12,Accent));
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
    void VerifyRuntimeDisplay(string mode,string path) { var card=content.Children.Count>1?content.Children[1] as Border:null;var panel=card==null?null:card.Child as StackPanel;var texts=panel==null?new string[0]:panel.Children.OfType<TextBlock>().Select(x=>x.Text).ToArray();if(!texts.Any(x=>x.Contains(RuntimeMode(mode)))||!texts.Any(x=>x.Contains(path)))throw new InvalidOperationException("Runtime mode status was not displayed"); }
    int Port() { if(proxyEnabled.IsChecked!=true)return 0;int v; if(!int.TryParse(port.Text,out v)||v<1||v>65535)throw new InvalidOperationException("代理端口需为 1–65535 的整数。"); return v; }
    void ApplyCurrentModelSettings(CurrentModelSettings current){modelCatalog.IncludeCurrent(current.ParentModel,current.ParentEffort);modelCatalog.IncludeCurrent(current.ChildModel,current.ChildEffort);RefreshModelChoices();parentModel.SelectedItem=current.ParentModel;childModel.SelectedItem=current.ChildModel;RefreshEfforts(parentEffort,current.ParentModel,"medium");RefreshEfforts(childEffort,current.ChildModel,"medium");parentEffort.SelectedItem=current.ParentEffort;childEffort.SelectedItem=current.ChildEffort;}
    void UpdateModelInputs(){parentModel.IsEnabled=true;parentEffort.IsEnabled=true;childModel.IsEnabled=subagents.IsChecked==true;childEffort.IsEnabled=subagents.IsChecked==true;RefreshEfforts(parentEffort,Convert.ToString(parentModel.SelectedItem),"medium");RefreshEfforts(childEffort,Convert.ToString(childModel.SelectedItem),"medium");modelCheck.Text=modelSettingsError.Length>0?modelSettingsError:modelCatalog.Source+"\n"+(subagents.IsChecked==true?"子代理已开启：将写入所选子代理模型和思考强度。":"子代理未开启：子模型与思考强度已禁用，勾选子代理多线程后可设置。");}
    void RefreshEfforts(ComboBox combo,string model,string fallback){CatalogModel entry;var values=modelCatalog.Models.TryGetValue((model??"").Trim(),out entry)?entry.Efforts:new string[0];var current=Convert.ToString(combo.SelectedItem);combo.ItemsSource=values;combo.SelectedItem=values.Contains(current)?current:values.Contains(fallback)?fallback:entry!=null&&values.Contains(entry.DefaultEffort)?entry.DefaultEffort:values.FirstOrDefault();}
    string Effort(ComboBox combo,string fallback){return Convert.ToString(combo.SelectedItem);}
    void CheckModels(){try{if(modelSettingsError.Length>0)throw new InvalidOperationException(modelSettingsError);RefreshModelChoices();RefreshEfforts(parentEffort,parentModel.Text,"high");RefreshEfforts(childEffort,childModel.Text,"high");var p=Model(parentModel,"gpt-6.1-sol");var c=Model(childModel,"gpt-6.1-sol");modelCheck.Text=modelCatalog.Source+"\n"+EffortLabels.InText(modelCatalog.Check(p,Effort(parentEffort,"medium")))+"\n"+(subagents.IsChecked==true?EffortLabels.InText(modelCatalog.Check(c,Effort(childEffort,"medium"))):"子代理未开启：跳过子模型检查。")+"\n预设列表不代表当前账号一定有调用权限。";}catch(Exception ex){modelCheck.Text=ex.Message;}}
    void RefreshModelChoices(){var values=modelCatalog.Models.Keys.ToArray();foreach(var combo in new[]{parentModel,childModel}){var current=Convert.ToString(combo.SelectedItem);if(current.Length==0)current=combo==parentModel?"gpt-6.1-sol":"gpt-6.1-sol";combo.ItemsSource=values;combo.SelectedItem=values.Contains(current)?current:values.FirstOrDefault();}}
    string Model(ComboBox input,string fallback){var value=Convert.ToString(input.SelectedItem);if(!modelCatalog.Models.ContainsKey(value))throw new InvalidOperationException("请从下拉框选择模型。");return value;}
    string ProxyHost() { if(proxyEnabled.IsChecked!=true)return "";var text=(proxyHost.Text??"").Trim();if(text.Length==0)return Core.DefaultProxyHost;System.Net.IPAddress address;if(!System.Net.IPAddress.TryParse(text,out address))throw new InvalidOperationException("代理 IP 必须是有效的 IPv4 或 IPv6 地址。");return address.ToString(); }
    Dictionary<string,object> Options(){if(modelSettingsError.Length>0)throw new InvalidOperationException(modelSettingsError);var p=Model(parentModel,"gpt-6.1-sol");var pe=Effort(parentEffort,"medium");var c=Model(childModel,"gpt-6.1-sol");var ce=Effort(childEffort,"medium");modelCatalog.Check(p,pe);if(subagents.IsChecked==true)modelCatalog.Check(c,ce);return new Dictionary<string,object>{{"proxyHost",ProxyHost()},{"folderManagement",folderManagement.IsChecked==true},{"antiOverDefense",antiOverDefense.IsChecked==true},{"workJournal",workJournal.IsChecked==true},{"commandLineFirst",commandLineFirst.IsChecked==true},{"inheritPermissions",inheritPermissions.IsChecked==true},{"memories",memories.IsChecked==true},{"githubMirrorEnabled",githubMirrors.IsChecked==true},{"customGithubMirror",NetworkOptions().customGithubMirror},{"subagents",subagents.IsChecked==true},{"cuaRepair",cuaRepair.IsChecked==true},{"browserRepair",browserRepair.IsChecked==true},{"officialCliUpdate",officialCliUpdate.IsChecked==true},{"desktopUpdate",desktopUpdate.IsChecked==true},{"cliMode",cliChoiceManual?(cliStandalone.IsChecked==true?"official-standalone":"desktop-native"):"auto"},{"parentModel",p},{"parentEffort",pe},{"childModel",c},{"childEffort",ce}};}
    Dictionary<string,object> ProxyOptions(){return new Dictionary<string,object>{{"proxyHost",ProxyHost()},{"githubMirrorEnabled",githubMirrors.IsChecked==true},{"customGithubMirror",NetworkOptions().customGithubMirror}};}
    GitHubNetworkOptions NetworkOptions(){return new GitHubNetworkOptions{githubMirrorEnabled=githubMirrors.IsChecked==true,customGithubMirror=githubMirrors.IsChecked==true?GitHubNetworkOptions.NormalizePrefix(githubCustomMirror.Text):""};}
    string ModelChecks(Dictionary<string,object> options){return EffortLabels.InText(modelCatalog.Source+"\n"+modelCatalog.Check(Convert.ToString(options["parentModel"]),Convert.ToString(options["parentEffort"]))+(subagents.IsChecked==true?"\n"+modelCatalog.Check(Convert.ToString(options["childModel"]),Convert.ToString(options["childEffort"])):"")+"\n核对结果不代表账号实时调用权限。");}
    string ProxySummary(){var value=Port();if(value==0)return "关闭（不写入本地 HTTP 代理）";var host=ProxyHost();if(host.Contains(":")&&!host.StartsWith("["))host="["+host+"]";return "http://"+host+":"+value;}
    string Drive() { if(drive.SelectedItem==null)throw new InvalidOperationException("请选择可用的工作盘。"); return Convert.ToString(drive.SelectedItem); }
    void Wizard() {
        subtitle.Text="1  选择工作区     →     2  确认变更     →     3  执行与结果";
        if(wizard==0) {
            var p=Card("01   选择工作盘与代理","工作区会创建在所选盘的 Codex 文件夹。已有工作文件不会自动搬迁。");
            p.Children.Add(T("工作盘",13,TextBrush));p.Children.Add(drive);p.Children.Add(T("\n网络连接",13,TextBrush));p.Children.Add(proxyEnabled);
            var proxyRows=new StackPanel{Margin=new Thickness(0,6,0,0)};
            var portRow=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,8)};portRow.Children.Add(new TextBlock{Text="HTTP 代理端口",Width=180,Foreground=TextBrush,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,8)});portRow.Children.Add(port);proxyRows.Children.Add(portRow);
            var hostRow=new StackPanel{Orientation=Orientation.Horizontal};hostRow.Children.Add(new TextBlock{Text="代理 IP（默认 127.0.0.1）",Width=180,Foreground=TextBrush,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,8)});hostRow.Children.Add(proxyHost);proxyRows.Children.Add(hostRow);p.Children.Add(proxyRows);
            p.Children.Add(T("填写 V2RayN、Clash、软路由等提供的 HTTP／混合代理端口。端口默认按 V2RayN 的 10808 填写，请以你的实际设置为准。IP 留空使用 127.0.0.1；软路由在局域网其他设备时，请填写可访问的路由器 IP 地址。本工具不会启动代理服务。关闭后会忽略端口和 IP。",12,MutedBrush));
            var features=Card("02   选择要启用的功能","按类别选择所需功能。配置与全局规则将在备份后按本次选择重建，子代理多线程默认关闭。");FeatureGroups(features);
            var cliLabel=T("初始化后使用的 CLI",13,TextBrush);cliLabel.Margin=new Thickness(0,16,0,4);features.Children.Add(cliLabel);Row(features,cliNative,cliStandalone);features.Children.Add(cliDetectionHint);features.Children.Add(T("独立 CLI 缺失时会先准备组件；启动保护与更新选项可单独选择。",12,MutedBrush));
            var models=Card("03   模型设置","默认读取当前全局模型设置，未设置的项目使用默认值；可通过下拉框更换。思考强度随模型切换，实际调用权限取决于账号。");ModelSelectionRow(models,"父模型",parentModel,parentEffort);ModelSelectionRow(models,"子模型",childModel,childEffort);models.Children.Add(modelCheck);models.Children.Add(Button("检查所选配置",()=>CheckModels(),false));
            models.Children.Add(Button("下一步：查看变更  →",()=>{try{Port();Drive();RequireCliChoice();Options();wizard=1;Navigate("初始化");}catch(Exception ex){Error(ex);}},true));
        } else if(wizard==1) {
            var p=Card("02   确认初始化","将更新当前 Windows 用户的 Codex 配置。执行时会请求管理员授权。");
            p.Children.Add(T(InitSummary(),14,TextBrush));
            Row(p,Button("返回修改",()=>{if(!busy){wizard=0;Navigate("初始化");}},false),Button("确认并开始初始化",async()=>{
                try { RequireCliChoice(); }
                catch(InvalidOperationException ex) { Error(ex); return; }
                await BeginInitialization();
            },true));
        } else { var p=Card("03   执行与结果",busy?"正在执行，请保留窗口。":"完整结果与备份位置见下方日志。"); p.Children.Add(log); Row(p,LatestLogButton(),Button("查看全部日志",()=>Navigate("日志"),false),Button("重新设置",()=>{if(!busy){ResetCliChoice();wizard=0;Navigate("初始化");}},false)); }
    }
    string InitSummary() {var options=Options();return "工作盘："+Drive()+":\n父模型："+options["parentModel"]+" / "+EffortLabels.Display(Convert.ToString(options["parentEffort"]))+"\n子代理："+(subagents.IsChecked==true?options["childModel"]+" / "+EffortLabels.Display(Convert.ToString(options["childEffort"])):"关闭，不写入子代理规则；保留模型设置 "+options["childModel"]+" / "+EffortLabels.Display(Convert.ToString(options["childEffort"])))+"\n初始化后使用的 CLI："+CliChoiceSummary()+"\n代理："+ProxySummary()+"\n文件夹管理："+(folderManagement.IsChecked==true?"创建并绑定标准目录":"跳过")+"\n防止过度防御："+(antiOverDefense.IsChecked==true?"写入 HERO 中文精简规则及实现、交付规则":"跳过")+"\n按需笔记与文档管理："+(workJournal.IsChecked==true?"写入 Markdown 笔记、文档与索引规则":"跳过")+"\n优先使用命令行："+(commandLineFirst.IsChecked==true?"写入 CLI 与 PowerShell 优先规则":"跳过")+"\n操作权限继承："+PermissionSummary()+"\nCodex 记忆："+(memories.IsChecked==true?"开启生成和使用":"关闭，保留已有记忆文件")+"\nGitHub 下载："+(githubMirrors.IsChecked==true?"官方优先，失败串行尝试自定义及五个镜像":"仅使用官方地址")+"\nCUA 检查与修复："+(cuaRepair.IsChecked==true?"按需执行":"跳过")+"\nCLI 启动保护与修复："+(guard.IsChecked==true?"安装，包含登录自动维护":"跳过")+"\n浏览器链接修复："+(browserRepair.IsChecked==true?"最后执行，真实连接需重启后验证":"跳过")+"\n更新官方CLI："+(officialCliUpdate.IsChecked==true?"更新独立组件":"跳过更新；如所选组件缺失仍会准备")+"\n更新桌面版（含原生CLI）："+(desktopUpdate.IsChecked==true?"先通过 Microsoft Store 精确更新":"跳过")+"\n执行顺序：前置检查 → 桌面更新 → 配置、目录及 CUA → 独立 CLI 更新 → 可选保护 → CLI 选择 → 浏览器修复。未完成项目可单独重试。\n"+ModelChecks(options)+"\n基础配置及全局 AGENTS.md 将完整备份并重建；原自定义指令只保存在备份中。项目内的 AGENTS.md 不变。更新用户 PATH。初始化和 CUA 分别备份。";}
    void ActionCard(string title,string description,string label,string op,bool danger) {
        var p=Card(title,description); p.Children.Add(Button(label,async()=>await Run(op,title,true),!danger));
    }
    string PermissionSummary() {
        if(inheritPermissions.IsChecked!=true)return "未启用，使用初始化模板。";
        if(preview)return "来源：全局 config.toml；执行前读取并继承审批、文件与网络权限。";
        try {var settings=PermissionSettings.Load(CurrentModelSettings.CodexHome());return "来源："+settings.SourcePath+"\n"+settings.Summary;}
        catch(Exception ex){return "读取未完成："+DesktopLocalization.ExceptionText(ex)+" 初始化将在配置重建前停止。";}
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
        var allow=Button("关闭并继续",()=>dialog.DialogResult=true,true);allow.IsDefault=false;
        var cancel=Button("取消",()=>dialog.DialogResult=false,false);cancel.IsDefault=true;cancel.IsCancel=true;
        buttons.Children.Add(allow);buttons.Children.Add(cancel);panel.Children.Add(buttons);dialog.Content=panel;return dialog;
    }
    async Task BeginInitialization() {
        if(busy)return;
        busy=true;activeOperation="initialize";banner.Text="正在检查初始化条件…";
        bool proceed=false,allowClose=false;
        Dictionary<string,object> options=null;
        try {
            options=Options();
            var report=InitializationPreflight==null?await ReadInitializationPreflight(options):await InitializationPreflight();
            object value;if(!report.TryGetValue("processes",out value)||!(value is System.Collections.IList))throw new InvalidDataException("初始化进程检查结果无效，请重试。");
            int count=((System.Collections.IList)value).Count;
            if(count>0){var dialog=CreateAppCloseDialog(count);allowClose=(PresentAppCloseDialog==null?dialog.ShowDialog():PresentAppCloseDialog(dialog))==true;}
            proceed=count==0||allowClose;
            if(!proceed)banner.Text="已取消本次初始化，配置尚未修改。";
        } catch(Exception ex){if(preview){banner.Text=DesktopLocalization.ExceptionText(ex);}else Error(ex);}
        finally {busy=false;activeOperation="";}
        if(!proceed){wizard=1;Navigate("初始化");return;}
        wizard=2;Navigate("初始化");
        options["allowCloseRunningApp"]=allowClose;
        await Run("initialize","初始化",false,options);
    }
    void Maintenance() {
        subtitle.Text="独立检查、修复和恢复。修改操作执行前会说明影响范围。";
        var g=Card("启动保护","管理独立 CLI、桌面原生模式与登录自动维护。更新或切换运行时前请关闭 Codex 桌面应用。");
        var mode=Core.Value(status,"runtimeMode","");var header=new Grid();header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});var title=g.Children[0];g.Children.RemoveAt(0);header.Children.Add(title);var badge=T(mode=="DesktopNative"?"当前：桌面原生 CLI":mode=="OfficialStandalone"?"当前：官方独立 CLI":status==null?"当前：正在读取状态":"当前：CLI 模式需检查",12,mode=="DesktopNative"?B("#70DFC0"):mode=="OfficialStandalone"?Accent:B("#E9C779"));badge.VerticalAlignment=VerticalAlignment.Center;badge.Margin=new Thickness(15,0,0,8);Grid.SetColumn(badge,1);header.Children.Add(badge);g.Children.Insert(0,header);
        var switchOp=mode=="DesktopNative"?"guard-current":mode=="OfficialStandalone"?"guard-native":"status";var switchLabel=mode=="DesktopNative"?"切换官方独立 CLI":mode=="OfficialStandalone"?"切换桌面原生 CLI":"重新检测 CLI";
        var switchButton=Button(switchLabel,async()=>await Run(switchOp,switchLabel,switchOp!="status"),false);if(switchOp!="status"){switchButton.Background=B("#9BAF9F");switchButton.Foreground=B("#14231C");}
        officialSwitch=switchButton;
        Row(g,Button("安装 / 修复保护",async()=>await Run("guard-install","安装启动保护",true),true),switchButton,Button("更新官方独立 CLI",async()=>await Run("official-cli-update","更新官方独立 CLI",true),false),Button("更新桌面版（含原生 CLI）",async()=>await Run("desktop-update","更新桌面版",true),false),Button("诊断助手",async()=>await Run("doctor","诊断助手",false),false));
        if(diagnosticAdvice.Length>0){g.Children.Add(T("\n最近一次诊断建议",16,TextBrush));g.Children.Add(T(diagnosticAdvice,13,MutedBrush));g.Children.Add(Link("查看详细日志",()=>Navigate("日志")));}
        var core=Card("桌面启动修复","无法启动或提示无法加载组织设置时，可检查桌面版的启动组件。适用于运行文件复制出现 EPERM / rename_staging 的情况；其他原因需要分别排查。");
        Row(core,Button("检查启动问题",async()=>await Run("desktop-core-check","检查桌面启动问题",false),true),Button("修复桌面启动组件",async()=>await Run("desktop-core-repair","修复桌面启动组件",true),false));
        core.Children.Add(T(desktopCoreAdvice.Length>0?desktopCoreAdvice:"先检查当前版本和近期日志；修复前请保存工作并退出 Codex。不会强制结束任务，也不会覆盖已有的异常目录。",12,MutedBrush));
        var compatibility=Card("配置兼容检查","检查用户配置、profile 和下方明确指定的项目。清理只移除两项已失效设置，先备份并生成恢复脚本；不会重置配置、关闭 Codex 或切换 CLI。");
        compatibility.Children.Add(T("项目目录（可选，仅检查该项目的 .codex/config.toml）",12,MutedBrush));
        var projectInput=new TextBox{Text=compatibilityProjectPath,Style=(Style)Resources["KitInput"]};projectInput.TextChanged+=(s,e)=>compatibilityProjectPath=projectInput.Text;compatibility.Children.Add(projectInput);
        Row(compatibility,Button("检查配置",async()=>await Run("config-check","配置兼容检查",false),true),Button("清理已废弃配置",async()=>await Run("config-cleanup","定向清理配置",true),false));
        compatibility.Children.Add(T(compatibilityAdvice.Length>0?compatibilityAdvice:"session-flags 来自会话参数时，清理文件无法消除警告；请检查桌面版与 CLI 的版本兼容性。",12,MutedBrush));
        var cua=Card("CUA 组件","检查官方桌面应用中的浏览器自动化文件，仅在校验异常时修复。修复前备份原文件，不重置模型或工作区。");
        Row(cua,Button("检查 CUA",async()=>await Run("cua-check","CUA 组件",true),true),Button("检查并修复",async()=>await Run("cua-repair","CUA 修复",true),true));
        var r=Card("恢复与回滚","选择本地初始化备份或保护器 CLI 历史缓存，再点击下方回滚按钮。时间为本机备份 / 缓存时间。初始化快照不是桌面应用版本；Microsoft Store 桌面版不在此回滚。");
        rollbackList=new ListBox{Background=B("#101620"),Foreground=TextBrush,BorderThickness=new Thickness(0),MaxHeight=280,MinHeight=70,HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(6),Margin=new Thickness(0,2,0,10)};
        ScrollViewer.SetHorizontalScrollBarVisibility(rollbackList,ScrollBarVisibility.Disabled);
        var entries=status!=null&&status.ContainsKey("rollbackEntries")?status["rollbackEntries"] as System.Collections.IEnumerable:null;
        if(entries!=null)foreach(var value in entries){var entry=value as Dictionary<string,object>;if(entry==null)continue;bool available=entry.ContainsKey("available")&&entry["available"] is bool&&(bool)entry["available"];var row=new StackPanel{Margin=new Thickness(8,5,8,5)};row.Children.Add(T((Core.Value(entry,"kind","")=="init"?"初始化配置":"保护器 CLI")+"  ·  "+Core.Value(entry,"version","版本未记录"),14,TextBrush));row.Children.Add(T(Core.Value(entry,"time","时间未记录")+"  ·  "+Core.Value(entry,"detail",""),12,MutedBrush));var item=new ListBoxItem{Tag=entry,Content=row,IsEnabled=available,ToolTip=Core.Value(entry,"path",""),HorizontalContentAlignment=HorizontalAlignment.Stretch};rollbackList.Items.Add(item);if(Core.Value(entry,"id","")==selectedRollbackId&&available)rollbackList.SelectedItem=item;}
        if(rollbackList.Items.Count==0)r.Children.Add(T(Core.Value(status,"rollbackError","").Length>0?Core.Value(status,"rollbackError",""):status==null?"正在读取可回滚备份，请刷新列表。":"暂未发现初始化备份或独立 CLI 历史缓存。",13,MutedBrush));
        r.Children.Add(rollbackList);
        rollbackButton=Button("回滚所选项",async()=>{var item=rollbackList.SelectedItem as ListBoxItem;var entry=item==null?null:item.Tag as Dictionary<string,object>;if(entry!=null)await Run("rollback-selected","回滚所选项",true,new Dictionary<string,object>{{"rollbackId",Core.Value(entry,"id","")}},RollbackSummary(entry));},false);rollbackButton.IsEnabled=rollbackList.SelectedItem!=null&&!busy;
        rollbackList.SelectionChanged+=(s,e)=>{var item=rollbackList.SelectedItem as ListBoxItem;var entry=item==null?null:item.Tag as Dictionary<string,object>;selectedRollbackId=Core.Value(entry,"id","");rollbackButton.IsEnabled=entry!=null&&!busy;};
        Row(r,rollbackButton,Button("刷新备份列表",async()=>{if(!busy){await RefreshStatusQuietly();Navigate("维护恢复");}},false),Button("卸载启动保护",async()=>await Run("guard-uninstall","卸载启动保护",true),false));
        var proxy=Card("代理设置","开关和端口在初始化页面设置。关闭后移除 Codex 的代理配置。");proxy.Children.Add(T("当前："+ProxySummary(),13,MutedBrush));proxy.Children.Add(Button("更新代理",async()=>await Run("proxy","更新代理",true),false));
    }
    static string RollbackSummary(Dictionary<string,object> entry){return "目标："+(Core.Value(entry,"kind","")=="init"?"初始化配置":"保护器 CLI")+"\n版本："+Core.Value(entry,"version","版本未记录")+"\n备份 / 缓存时间："+Core.Value(entry,"time","未记录")+"\n位置："+Core.Value(entry,"path","")+"\n\n"+(Core.Value(entry,"kind","")=="init"?"将覆盖当前配置、代理、全局规则及该备份记录的运行时 / PATH 设置；不恢复 CUA，不删除对话或已创建的工作目录。":"将切换到所选本地独立 CLI，并更改桌面应用的 CLI 覆盖路径；不会降级 Microsoft Store 桌面应用。")+"\n请先保存工作并退出 Codex；检测到仍在运行时停止，不强制结束进程。";}
    void GuideOfficialCli(){
        Navigate("维护恢复");
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
        var environment=Card("浏览器连接状态","补丁只作用于当前已校验的官方运行时。桌面版升级后请重新检查；旧版本的结果不能代表当前版本。");
        environment.Children.Add(T("当前桌面版："+Core.Value(status,"desktopVersion","未检测"),13,TextBrush));
        if(browserReport!=null&&status!=null&&Core.Value(browserReport,"desktopVersion","")!=Core.Value(status,"desktopVersion",""))environment.Children.Add(T("应用版本已变化：下方旧检查结果已失效，请重新检查浏览器连接。",13,B("#E9C779")));
        foreach(var item in new[]{new[]{"desktopVersion","最近检查的桌面版"},new[]{"runtime","浏览器控制运行时"},new[]{"proxy","最近检查代理"},new[]{"patch","补丁状态"},new[]{"checkedAt","最近检查"}})environment.Children.Add(T(item[1]+"："+DesktopLocalization.TranslateText(Core.Value(browserReport,item[0],"未检查")),13,MutedBrush));
        var settings=Card("代理设置","与初始化页面同步。使用代理软件提供的 HTTP／混合端口；关闭代理后，修复操作会恢复本工具管理的入口为直连。");
        var enabled=new CheckBox{Content="启用 HTTP 代理",IsChecked=proxyEnabled.IsChecked,Style=(Style)Resources["KitCheck"]};
        var hostInput=new TextBox{Text=proxyHost.Text,Style=(Style)Resources["KitInput"]};var portInput=new TextBox{Text=port.Text,Style=(Style)Resources["KitInput"]};
        hostInput.IsEnabled=portInput.IsEnabled=enabled.IsChecked==true;
        enabled.Checked+=(a,b)=>{proxyEnabled.IsChecked=true;hostInput.IsEnabled=portInput.IsEnabled=true;};enabled.Unchecked+=(a,b)=>{proxyEnabled.IsChecked=false;hostInput.IsEnabled=portInput.IsEnabled=false;};
        hostInput.TextChanged+=(a,b)=>proxyHost.Text=hostInput.Text;portInput.TextChanged+=(a,b)=>port.Text=portInput.Text;
        settings.Children.Add(enabled);settings.Children.Add(T("代理 IP（留空为 127.0.0.1）",12,MutedBrush));settings.Children.Add(hostInput);settings.Children.Add(T("HTTP 代理端口",12,MutedBrush));settings.Children.Add(portInput);
        Row(settings,Button("修复并启动 Codex",async()=>await Run("browser-start-prepare","修复并启动 Codex",true),true),Button("检查连接",async()=>await Run("browser-check","检查浏览器连接",false),false),Button("修复连接",async()=>await Run("browser-repair","修复浏览器连接",true),false),Button("恢复原始文件",async()=>await Run("browser-rollback","恢复浏览器入口",true),false),Button("复制连接测试指令",()=>{Clipboard.SetText("请使用浏览器控制工具，新建独立测试标签页访问 https://example.com，读取页面标题和 DOM；成功后关闭本次新建的测试标签。保留我的其他 Chrome 标签页。请分别报告连接、页面读取和关闭结果，不要仅用 HTTP 状态判断连接成功。");banner.Text="已复制 · 重启 Codex 后在新对话中运行测试";},false));
        settings.Children.Add(T("通过此按钮启动时会先检查并按需修复；原来的 Codex 图标不会经过此检查。",12,MutedBrush));
        var checks=Card("分项验证","HTTP 403 只表示服务可达，不表示 Chrome 已连接。环境探针验证 Node 与子进程继承，真实 CUA 连接仍需重启后测试。");
        foreach(var item in new[]{new[]{"integrity","组件完整性"},new[]{"reachability","代理可达性"},new[]{"environment","环境继承探针"},new[]{"connection","真实连接验证"}})checks.Children.Add(T(item[1]+"："+DesktopLocalization.TranslateText(Core.Value(browserReport,item[0],item[0]=="connection"?"待重启后验证":"未检查")),13,MutedBrush));
        checks.Children.Add(T("修改前请保存工作并彻底退出 Codex，再从独立 EXE 执行修复。本工具不会关闭 Chrome 或用户标签页。修复后重新启动 Codex，复制上方指令完成验收。",12,Accent));
        checks.Children.Add(Link("在 Microsoft Store 中更新桌面版 ↗",()=>Open("ms-windows-store://pdp/?ProductId=9PLM9XGG6VKS")));
    }
    void ModelSelectionRow(StackPanel panel,string label,ComboBox model,ComboBox effort){
        var heading=T(label,16,TextBrush);heading.FontWeight=FontWeights.Bold;heading.Margin=new Thickness(0,12,0,8);panel.Children.Add(heading);var row=new Grid();row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(2,GridUnitType.Star)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
        var id=T("模型 ID",13,MutedBrush);id.VerticalAlignment=VerticalAlignment.Center;id.Margin=new Thickness(0,0,12,10);row.Children.Add(id);Grid.SetColumn(model,1);row.Children.Add(model);var strength=T("思考强度",13,MutedBrush);strength.VerticalAlignment=VerticalAlignment.Center;strength.Margin=new Thickness(18,0,12,10);Grid.SetColumn(strength,2);row.Children.Add(strength);Grid.SetColumn(effort,3);row.Children.Add(effort);panel.Children.Add(row);
    }
    void VerifyMaintenanceModes(){
        foreach(var mode in new[]{"DesktopNative","OfficialStandalone","Unknown"}){
            status=new Dictionary<string,object>{{"runtimeMode",mode}};Navigate("维护恢复");UpdateLayout();var panel=(StackPanel)((Border)content.Children[0]).Child;var buttons=panel.Children.OfType<WrapPanel>().First().Children.OfType<Button>().Select(x=>Convert.ToString(x.Content)).ToArray();var expected=mode=="DesktopNative"?"切换官方独立 CLI":mode=="OfficialStandalone"?"切换桌面原生 CLI":"重新检测 CLI";
            if(!buttons.SequenceEqual(new[]{"安装 / 修复保护",expected,"更新官方独立 CLI","更新桌面版（含原生 CLI）","诊断助手"}))throw new InvalidOperationException("Maintenance switch labels or order were wrong");var texts=((Grid)panel.Children[0]).Children.OfType<TextBlock>().Select(x=>x.Text).ToArray();if(!texts.Any(x=>x.Contains(mode=="DesktopNative"?"桌面原生 CLI":mode=="OfficialStandalone"?"官方独立 CLI":"需检查")))throw new InvalidOperationException("Maintenance status badge was missing");
            if(mode!="Unknown"&&((SolidColorBrush)panel.Children.OfType<WrapPanel>().First().Children.OfType<Button>().ElementAt(1).Background).Color!=Color.FromRgb(155,175,159))throw new InvalidOperationException("CLI switch highlight was lost");
            var configPanel=content.Children.OfType<Border>().Select(x=>x.Child as StackPanel).FirstOrDefault(x=>x!=null&&x.Children.OfType<TextBlock>().Any(t=>t.Text=="配置兼容检查"));if(configPanel==null||!configPanel.Children.OfType<WrapPanel>().SelectMany(x=>x.Children.OfType<Button>()).Select(x=>Convert.ToString(x.Content)).SequenceEqual(new[]{"检查配置","清理已废弃配置"}))throw new InvalidOperationException("Config compatibility controls missing");
            var corePanel=content.Children.OfType<Border>().Select(x=>x.Child as StackPanel).FirstOrDefault(x=>x!=null&&x.Children.OfType<TextBlock>().Any(t=>t.Text=="桌面启动修复"));if(corePanel==null||!corePanel.Children.OfType<WrapPanel>().SelectMany(x=>x.Children.OfType<Button>()).Select(x=>Convert.ToString(x.Content)).SequenceEqual(new[]{"检查启动问题","修复桌面启动组件"}))throw new InvalidOperationException("Desktop startup recovery controls missing");
        }
        diagnosticAdvice=DiagnosticAdvice.Build(null,1,"[ERROR] proxy connection refused");Navigate("维护恢复");var guardPanel=(StackPanel)((Border)content.Children[0]).Child;if(!guardPanel.Children.OfType<TextBlock>().Any(x=>x.Text.Contains("建议怎么做")))throw new InvalidOperationException("Diagnostic advice was not retained in maintenance");var dialog=CreateDiagnosticDialog();if(dialog.Title!="诊断建议")throw new InvalidOperationException("Diagnostic dialog missing");dialog.Close();diagnosticAdvice="";status=null;wizard=0;Navigate("初始化");if(!(parentModel.Parent is Grid)||parentModel.Parent!=parentEffort.Parent||childModel.Parent!=childEffort.Parent||parentModel.Parent==childModel.Parent)throw new InvalidOperationException("Model and effort controls were not paired by row");
        parentModel.ApplyTemplate();var popup=(System.Windows.Controls.Primitives.Popup)parentModel.Template.FindName("PART_Popup",parentModel);var popupScroll=((Border)popup.Child).Child as SmoothScrollViewer;if(popupScroll==null)throw new InvalidOperationException("Model dropdown scrolling is not smooth");double itemsHeight=10;foreach(var id in ModelCatalog.Preset().Models.Keys){var item=new ComboBoxItem{Content=id,Style=(Style)Resources["KitComboItem"]};item.Measure(new Size(320,Double.PositiveInfinity));itemsHeight+=item.DesiredSize.Height;if(item.FontSize!=12.5)throw new InvalidOperationException("Dropdown font was not compact");}if(itemsHeight<=0||popupScroll.MaxHeight<=0)throw new InvalidOperationException("Preset dropdown scrolling is unavailable");
    }
    void Updates() {
        subtitle.Text="从项目 GitHub 发现新版本，确认后下载、校验并替换。";
        var p=Card("当前版本  v"+Core.Version,update==null?"尚未获得更新清单。应用启动时会自动检查，可手动重试。":"GitHub 最新版本  v"+Core.Value(update,"version","未知"));
        Row(p,Button("检查更新",async()=>await CheckUpdates(false),true),Button("打开版本发布页 ↗",()=>Open("https://github.com/"+Core.Repository+"/releases"),false));
        if(update!=null && new Version(Core.Value(update,"version","0.0.0"))>new Version(Core.Version)) { p.Children.Add(T(Core.Value(update,"notes","有新版本可用。"),14,Accent)); p.Children.Add(Button("下载并更新",async()=>await InstallUpdate(),true)); }
        var networkCard=Card("GitHub 下载加速","官方地址优先，失败后依次尝试自定义前缀及五个内置镜像；保持当前 HTTP 代理设置。仅用于公开 GitHub 资源，现有校验继续执行。");
        networkCard.Children.Add(githubMirrors);networkCard.Children.Add(T("自定义镜像前缀（可选，HTTPS）",12,MutedBrush));networkCard.Children.Add(githubCustomMirror);
        networkCard.Children.Add(T("顺序：官方 → 自定义（如填写）→ ghfast.top → gh-proxy.com → ghproxy.net → gh.llkk.cc → ghproxy.homeboyc.cn。",12,MutedBrush));
        Row(networkCard,Button("保存下载设置",()=>{try{NetworkOptions().Save(Core.NetworkSettingsPath);networkSettingsError="";banner.Text="GitHub 下载设置已保存";}catch(Exception ex){Error(ex);}},true));
        if(networkSettingsError.Length>0)networkCard.Children.Add(T(networkSettingsError,12,MutedBrush));
        githubLogView.Text=githubLogText;networkCard.Children.Add(githubLogView);
        Card("更新如何进行","每次启动自动检查 GitHub 新版，右下角显示检查状态，发现更新时右下角出现红点入口。点击后下载并校验，退出后替换同目录、同文件名的 EXE 并重启；更新成功后自动删除旧版本。\n检查失败不影响本地功能。网络请求遵循初始化页面中的代理开关和端口。");
    }
    void LogsPage() { subtitle.Text="默认跟随底部最新日志；上翻暂停，回到底部恢复。"; var p=Card(operationTitle.Length>0?operationTitle:"执行日志","中文和原始日志保存在当前用户 LocalAppData\\CodexInitKit\\Desktop\\Logs。"); p.Children.Add(log); Row(p,LatestLogButton(),Button(showRawLog?"查看中文日志":"查看原始日志",()=>{showRawLog=!showRawLog;UpdateLogView();Navigate("日志");},false),Button("打开日志目录",()=>Open(Core.Logs),false),Button("复制日志",()=>Clipboard.SetText(LogToCopy()),false)); }
    Button LatestLogButton(){return Button("回到最新日志",()=>logViewport.Latest(),false);}
    string LogToCopy(){return log.Text.Length>0?log.Text:"尚无日志";}
    void UpdateLogView(){logViewport.SetText(showRawLog?rawLogText:chineseLogText);}
    string TranslateLogLine(string value){string translated;if(!localizedLines.TryGetValue(value,out translated)){translated=DesktopLocalization.TranslateLine(value);localizedLines[value]=translated;}return translated;}
    bool Confirm(string title,string message) { return MessageBox.Show(this,message+"\n\n是否继续？",title,MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes; }
    void Error(Exception ex) { var message=DesktopLocalization.ExceptionText(ex);banner.Text=message;RecordUiError(ex,true);MessageBox.Show(this,message,"操作未完成",MessageBoxButton.OK,MessageBoxImage.Error); }
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
        if(initialize)initializationSummary="";
        try {
            var configOperation=op=="config-check"||op=="config-cleanup";var coreOperation=op=="desktop-core-check"||op=="desktop-core-repair";
            int proxy=op=="status"||configOperation||coreOperation?0:Port(); string disk=op=="initialize"?Drive():Environment.SystemDirectory.Substring(0,1);var options=configOperation?new Dictionary<string,object>{{"projectPath",compatibilityProjectPath},{"proxyHost",Core.DefaultProxyHost}}:op=="initialize"?Options():(op=="status"||coreOperation?new Dictionary<string,object>{{"proxyHost",Core.DefaultProxyHost}}:ProxyOptions());
            if(extraOptions!=null)foreach(var pair in extraOptions)options[pair.Key]=pair.Value;
            if(op=="browser-start-prepare" && Core.Admin)throw new InvalidOperationException("请以普通用户权限打开本工具，再使用修复并启动功能。");
            if(confirm && !Confirm(title,confirmationText??Impact(op)))return;
            if(configOperation)compatibilityAdvice="";
            if(coreOperation)desktopCoreAdvice="";
            if(initialize)initBrowserRepair=Convert.ToBoolean(options["browserRepair"]);
            busy=true; activeOperation=op; operationTitle=title; banner.Text=title+" · 正在启动"; progress.IsIndeterminate=true;
            if(initialize&&page=="初始化"&&wizard==2)Navigate("初始化");
            if(rollbackButton!=null)rollbackButton.IsEnabled=false;
            if(op=="browser-start-prepare")browserStartTarget=null;
            if(op=="doctor"){diagnosisContext=null;diagnosticAdvice="";}
            var id=Guid.NewGuid().ToString("N"); lastLog=OperationLogPath(id);rawLogText="";rawDiagnosticText="";chineseLogText="";localizedLines.Clear();showRawLog=false;logInputComplete=false;renderedComplete=false;logViewport.Reset();
            using(var process=StartOperation(op,id,disk,proxy,op=="initialize"&&guard.IsChecked==true,options)) {
                while(!process.HasExited) { await Task.Delay(300); ReadLog(); }
                logInputComplete=true;ReadLog(); progress.IsIndeterminate=false;
                if(configOperation){if(compatibilityAdvice.Length==0)compatibilityAdvice=chineseLogText;banner.Text=process.ExitCode==0?"配置检查完成 · 文件结果已显示；会话参数警告需单独验证":"配置处理未全部完成 · 请查看结果和日志";progress.Value=100;Navigate("维护恢复");}
                else if(coreOperation){if(desktopCoreAdvice.Length==0)desktopCoreAdvice=chineseLogText;banner.Text=process.ExitCode==0?"桌面启动组件检查完成 · 请查看结果":"桌面启动检查或修复未完成 · 请查看原因";progress.Value=100;Navigate("维护恢复");if(process.ExitCode!=0&&!preview)MessageBox.Show(this,desktopCoreAdvice,title,MessageBoxButton.OK,MessageBoxImage.Warning);}
                else if(op=="doctor"){diagnosticAdvice=DiagnosticAdvice.Build(diagnosisContext,process.ExitCode,rawDiagnosticText);var completed=process.ExitCode==0||DiagnosticAdvice.CompletedReport(rawDiagnosticText);banner.Text=completed?"诊断完成 · 已生成操作建议":"诊断未完成 · 已生成排查建议";if(completed)progress.Value=100;Navigate("维护恢复");if(!preview)CreateDiagnosticDialog().ShowDialog();}
                else if(op=="browser-start-prepare"){
                    if(process.ExitCode!=0){banner.Text="浏览器准备未完成 · 请查看日志中的原因";progress.Value=100;Navigate("浏览器连接");if(!preview)MessageBox.Show(this,banner.Text+"\n\n"+chineseLogText,title,MessageBoxButton.OK,MessageBoxImage.Warning);}
                    else {
                        if(browserStartTarget==null)throw new InvalidDataException("未收到经过校验的桌面应用身份。");
                        var current=await VerifyBrowserStartTarget();
                        var uri=Core.BrowserLaunchUri(browserStartTarget,current);
                        var launch=new ProcessStartInfo("explorer.exe",uri){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden};Process.Start(launch);
                        var reach=Core.Value(browserStartTarget,"reachability","");banner.Text=reach.Contains("不可达")?"已请求启动 Codex · 代理端口不可达，请检查代理":"已请求启动 Codex · 真实浏览器连接仍需验证";progress.Value=100;Navigate("浏览器连接");
                    }
                }
                else if(op.StartsWith("browser-")){banner.Text=process.ExitCode==0?"浏览器检查或修复已准备 · 真实连接需重启后验证":"浏览器操作未完成 · 可查看原因后单独重试";progress.Value=100;Navigate("浏览器连接");}
                else if(initialize){initExitCode=process.ExitCode;showInitializationResult=true;banner.Text=initExitCode==0?"初始化完成 · 日志已保存":initExitCode==2?"初始化部分完成 · 请重试未完成项目":"初始化未完成 · 请查看日志";if(initExitCode==0||initExitCode==2)progress.Value=100;refreshStatus=true;}
                else if(process.ExitCode!=0) { banner.Text=title+"未完成 · 退出码 "+process.ExitCode+" · 查看日志了解原因"; if(op!="status")MessageBox.Show(this,banner.Text,title,MessageBoxButton.OK,MessageBoxImage.Warning); }
                else {progress.Value=100;banner.Text=title+"完成 · 日志已保存"; if(op=="status" && (page=="概览"||page=="维护恢复"))Navigate(page); if(!preview&&(op.StartsWith("guard-")||op=="desktop-update"||op=="official-cli-update"))refreshStatus=true;}
            }
        } catch(System.ComponentModel.Win32Exception ex) {
            if(ex.NativeErrorCode==1223){banner.Text="已取消管理员授权，未开始执行。";if(initialize){initCancelled=true;showInitializationResult=true;}}
            else if(initialize){initError=DesktopLocalization.ExceptionText(ex);banner.Text="初始化未完成 · 请查看日志";RecordUiError(ex,false);showInitializationResult=true;}
            else Error(ex);
        } catch(Exception ex) {
            if(initialize){initError=DesktopLocalization.ExceptionText(ex);banner.Text="初始化未完成 · 请查看日志";RecordUiError(ex,false);showInitializationResult=true;}
            else Error(ex);
        }
        finally {busy=false;activeOperation="";progress.IsIndeterminate=false;if(op=="status"&&status==null){cliDetectionComplete=true;UpdateCliDetectionHint();}if(rollbackButton!=null)rollbackButton.IsEnabled=rollbackList!=null&&rollbackList.SelectedItem!=null;}
        if(showInitializationResult){wizard=2;Navigate("初始化");ShowInitializationResult(initExitCode,initError,initCancelled,initBrowserRepair);}
        if(refreshStatus && !preview) await RefreshStatusQuietly();
        if(op=="rollback-selected"&&!preview){await RefreshStatusQuietly();if(page=="维护恢复")Navigate(page);}
    }
    void ReadLog() {
        if(!File.Exists(lastLog))return;
        string text; using(var fs=new FileStream(lastLog,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var reader=new StreamReader(fs,Encoding.UTF8))text=reader.ReadToEnd();
        if(text==rawLogText && chineseLogText.Length>0 && renderedComplete==logInputComplete)return;
        rawLogText=text;renderedComplete=logInputComplete;var display=new StringBuilder();var diagnostic=new StringBuilder();var lines=text.Split('\n');for(int index=0;index<lines.Length;index++) {var line=lines[index];if(!logInputComplete&&index==lines.Length-1&&!text.EndsWith("\n"))break;
            if(line.StartsWith("@@BROWSER_START@@")){try{browserStartTarget=Core.ReadJson(line.Substring(17).Trim());}catch{display.AppendLine("启动身份无法读取。");}}
            else if(line.StartsWith("@@BROWSER@@")){try{browserReport=Core.ReadJson(line.Substring(11).Trim());foreach(var item in new[]{new[]{"patch","浏览器修复状态"},new[]{"integrity","组件完整性"},new[]{"reachability","代理检查"},new[]{"environment","环境检查"}})display.AppendLine(item[1]+"："+DesktopLocalization.TranslateText(Core.Value(browserReport,item[0],"未检查")));}catch{display.AppendLine("浏览器结果无法读取。");}}
            else if(line.StartsWith("@@INIT_RESULT@@")){try{initializationSummary=DesktopLocalization.TranslateText(Core.Value(Core.ReadJson(line.Substring(15).Trim()),"summary",""),true);display.AppendLine(initializationSummary);}catch{display.AppendLine("初始化结果无法读取。");}}
            else if(line.StartsWith("@@CONFIG_COMPAT@@")){try{compatibilityAdvice=DesktopLocalization.TranslateText(Core.Value(Core.ReadJson(line.Substring(17).Trim()),"summary",""),true);}catch{display.AppendLine("配置检查结果无法读取，请查看详细日志。");}}
            else if(line.StartsWith("@@DESKTOP_CORE@@")){try{var report=Core.ReadJson(line.Substring("@@DESKTOP_CORE@@".Length).Trim());desktopCoreAdvice=DesktopLocalization.TranslateText(Core.Value(report,"summary",""),true)+"\n桌面版本："+Core.Value(report,"version","未检测")+"\n近期日志："+DesktopLocalization.TranslateText(Core.Value(report,"signal","未检查"))+"\n组件目录："+Core.Value(report,"target","未检测");display.AppendLine(desktopCoreAdvice);}catch{display.AppendLine("桌面启动检查结果无法读取，请查看详细日志。");}}
            else if(line.StartsWith("@@STATUS@@")) {try{status=Core.ReadJson(line.Substring(10).Trim());ApplyCliDetection(status);}catch{display.AppendLine("状态输出格式无效，请查看原始日志。");} }
            else if(line.StartsWith("@@DIAGNOSIS@@")){try{diagnosisContext=Core.ReadJson(line.Substring(13).Trim());}catch{display.AppendLine("未能读取组件检查结果；建议将依据诊断输出生成。");}}
            else if(line.StartsWith("@@PROGRESS@@")) {try{var p=Core.ReadJson(line.Substring(12).Trim());banner.Text=ProgressTitle(Core.Value(p,"title",operationTitle));int step,total;if(int.TryParse(Core.Value(p,"step","0"),out step)&&int.TryParse(Core.Value(p,"total","0"),out total)&&total>1){progress.IsIndeterminate=false;progress.Value=100.0*Math.Max(0,step-1)/total;}display.AppendLine(banner.Text);}catch{display.AppendLine("阶段输出格式无效，请查看原始日志。");} }
            else {diagnostic.AppendLine(line.TrimEnd('\r'));display.AppendLine(TranslateLogLine(line.TrimEnd('\r')));}
        }
        rawDiagnosticText=diagnostic.ToString();chineseLogText=display.ToString();
        try{DesktopLocalization.SaveChineseLog(lastLog,chineseLogText);}catch{if(!preview)banner.Text="中文日志暂时无法保存；原始日志仍可查看。";}
        UpdateLogView();
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
        var root=(FrameworkElement)Content;root.Measure(new Size(1120,800));root.Arrange(new Rect(0,0,1120,800));root.UpdateLayout();
        var frame=new DispatcherFrame();
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);root.UpdateLayout();
    }
    void VerifyRollbackSelection(){
        var entries=new object[]{new Dictionary<string,object>{{"id",new string('a',64)},{"kind","init"},{"version","备份工具 v2.2.4"},{"time","2026-11-01 14:00:00"},{"available",true},{"path",@"C:\fixture\snapshot"}},new Dictionary<string,object>{{"id",new string('b',64)},{"kind","cli"},{"version","0.165.0"},{"time","2026-10-30 12:00:00"},{"available",true},{"path",@"C:\fixture\release"}},new Dictionary<string,object>{{"id",new string('c',64)},{"kind","cli"},{"available",false}}};
        try{
            status=new Dictionary<string,object>{{"rollbackEntries",entries}};selectedRollbackId="";Navigate("维护恢复");
            if(rollbackList.Items.Count!=3||rollbackButton.IsEnabled||((ListBoxItem)rollbackList.Items[2]).IsEnabled)throw new InvalidDataException("Rollback list defaults or unavailable rows failed");
            rollbackList.SelectedIndex=0;if(!rollbackButton.IsEnabled||selectedRollbackId!=new string('a',64))throw new InvalidDataException("Rollback selection was not retained");
            var summary=RollbackSummary((Dictionary<string,object>)entries[0]);if(!summary.Contains("2026-11-01 14:00:00")||!summary.Contains("2.2.4")||!summary.Contains("不删除对话"))throw new InvalidDataException("Rollback confirmation lacks its exact target or impact");
            Navigate("维护恢复");if(rollbackList.SelectedIndex!=0||!rollbackButton.IsEnabled)throw new InvalidDataException("Returning to maintenance lost the rollback selection");
            rollbackList.SelectedIndex=1;summary=RollbackSummary((Dictionary<string,object>)entries[1]);if(!summary.Contains("0.165.0")||!summary.Contains("不会降级 Microsoft Store"))throw new InvalidDataException("CLI rollback confirmation misstates its scope");
            status=new Dictionary<string,object>();Navigate("维护恢复");if(rollbackList.Items.Count!=0||rollbackButton.IsEnabled)throw new InvalidDataException("Empty backup list enabled rollback");
            foreach(var mode in new[]{"DesktopNative","OfficialStandalone"})foreach(int width in new[]{940,1120}){
                status=new Dictionary<string,object>{{"runtimeMode",mode}};Navigate("概览");var root=(FrameworkElement)Content;root.Measure(new Size(width,720));root.Arrange(new Rect(0,0,width,720));root.UpdateLayout();
                var hero=(StackPanel)((Border)content.Children[0]).Child;var notice=hero.Children.OfType<TextBlock>().FirstOrDefault(x=>x.Name=="DataPreservationNotice");var label=notice;var actions=hero.Children.OfType<Grid>().First();var start=actions.Children.OfType<Button>().First();
                if(label==null||label.Text!="本工具不会初始化数据与对话"||label.Foreground!=Brushes.White||Convert.ToString(notice.ToolTip)!="初始化会备份并重建配置，保留现有对话记录和项目文件。")throw new InvalidDataException("Overview data-preservation notice or tooltip is missing");
                if(notice.ActualWidth>=hero.ActualWidth||notice.ActualHeight>20||notice.ActualWidth<label.DesiredSize.Width-label.Margin.Left-label.Margin.Right-1||label.ActualHeight<label.DesiredSize.Height-label.Margin.Top-label.Margin.Bottom-1)throw new InvalidDataException("Overview notice stretched or clipped at width "+width);
                if(Math.Abs(notice.TranslatePoint(new Point(),hero).X-start.TranslatePoint(new Point(),hero).X)>1||notice.TranslatePoint(new Point(),hero).Y<actions.TranslatePoint(new Point(0,actions.ActualHeight),hero).Y+7)throw new InvalidDataException("Overview notice is not below and left-aligned with initialization");
            }
        }finally{status=null;selectedRollbackId="";}
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
            chineseLogText+="操作完成\n";UpdateLogView();wizard=2;Navigate("初始化");PumpLogLayout();
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
            if(refreshed!=null){status=refreshed;ApplyCliDetection(status);if(page=="概览"||page=="维护恢复")Navigate(page);}
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
        dialog.Content=new Border{Background=PanelBrush,BorderBrush=B("#39465D"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Child=panel};return dialog;
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
        if(checkingUpdate)return;
        try {checkingUpdate=true;updateStatus.Text="自动检查更新中…"; var proxy=Port();var host=ProxyHost(); var network=NetworkOptions();ResetGitHubLog();update=await Task.Run(()=>Core.CheckUpdate(proxy,host,network,GitHubLog));ApplyUpdateState();if(page=="更新")Navigate("更新");}
        catch(Exception ex){updateStatus.Text="更新检查未完成";updateStatus.ToolTip=DesktopLocalization.ExceptionText(ex);RecordUiError(ex,false);if(!silent && page=="更新")Navigate("更新");}
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
        if(busy){banner.Text="请等待当前操作完成后再更新。";return;}
        if(update==null || new Version(Core.Value(update,"version","0.0.0"))<=new Version(Core.Version))return;
        try{busy=true;updateNotice.IsEnabled=false;progress.IsIndeterminate=true;updateStatus.Text="正在下载并校验更新…";var stage=await Core.DownloadUpdate(update,Port(),ProxyHost(),NetworkOptions(),GitHubLog);Core.ScheduleUpdate(stage);busy=false;Application.Current.Shutdown();}
        catch(Exception ex){updateStatus.Text="更新未完成，可点击重试";Error(ex);}finally{busy=false;updateNotice.IsEnabled=true;progress.IsIndeterminate=false;}
    }
}
}
