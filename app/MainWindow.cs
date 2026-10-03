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

namespace CodexKit {
public class MainWindow : Window {
    static readonly Brush BackgroundBrush=B("#10141E"), PanelBrush=B("#1B2230"), TextBrush=B("#EDF2FC"), MutedBrush=B("#A4B0C5"), Accent=B("#9EBCFF");
    readonly StackPanel content=new StackPanel();
    readonly TextBlock heading=new TextBlock(), subtitle=new TextBlock(), banner=new TextBlock();
    readonly TextBox log=new TextBox();
    readonly ProgressBar progress=new ProgressBar();
    readonly List<Button> navigation=new List<Button>();
    readonly ComboBox drive=new ComboBox();
    readonly TextBox port=new TextBox();
    readonly CheckBox guard=new CheckBox();
    Dictionary<string,object> status, update;
    string page="概览", lastLog="", operationTitle="";
    bool busy, preview;
    int wizard;
    public void VerifyPages() {
        foreach(var name in new[]{"概览","初始化","维护恢复","更新","日志","初始化"}) { Navigate(name); Measure(new Size(1120,800)); Arrange(new Rect(0,0,1120,800)); UpdateLayout(); }
        wizard=1;Navigate("初始化");UpdateLayout(); wizard=2;Navigate("初始化");UpdateLayout();Navigate("日志");UpdateLayout();
    }
    public void RenderPages(string directory) {
        Directory.CreateDirectory(directory);
        foreach(var name in new[]{"概览","初始化","维护恢复","更新","日志"}) {
            Navigate(name);var view=(FrameworkElement)Content;view.Measure(new Size(1120,800));view.Arrange(new Rect(0,0,1120,800));view.UpdateLayout();
            var bitmap=new RenderTargetBitmap(1120,800,96,96,PixelFormats.Pbgra32);bitmap.Render(view);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(Path.Combine(directory,name+".png")))encoder.Save(file);
        }
    }
    public MainWindow(bool isPreview) {
        preview=isPreview; Title="Codex Init Kit"; Width=1120; Height=800; MinWidth=940; MinHeight=720; WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Background=BackgroundBrush; Foreground=TextBrush; FontFamily=new FontFamily("Microsoft YaHei UI"); FontSize=14;
        var layout=new Grid{Background=BackgroundBrush}; layout.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(220)}); layout.ColumnDefinitions.Add(new ColumnDefinition()); Content=layout;
        var rail=new Grid{Background=B("#151B27")}; rail.RowDefinitions.Add(new RowDefinition{Height=new GridLength(130)}); rail.RowDefinitions.Add(new RowDefinition()); rail.RowDefinitions.Add(new RowDefinition{Height=new GridLength(90)}); layout.Children.Add(rail);
        var brand=new StackPanel{Margin=new Thickness(25,25,15,10)}; brand.Children.Add(T("C /",30,Accent)); brand.Children.Add(T("CODEX INIT KIT",16,TextBrush)); brand.Children.Add(T("Windows 初始化与维护",11,MutedBrush)); rail.Children.Add(brand);
        var nav=new StackPanel{Margin=new Thickness(14,12,14,0)}; Grid.SetRow(nav,1); rail.Children.Add(nav);
        foreach(var name in new[]{"概览","初始化","维护恢复","更新","日志"}) { var button=Button(name,()=>Navigate(name),false); button.HorizontalContentAlignment=HorizontalAlignment.Left; button.Margin=new Thickness(0,0,0,8); button.Padding=new Thickness(17,14,12,14); nav.Children.Add(button); navigation.Add(button); }
        var foot=new StackPanel{Margin=new Thickness(25,10,15,15)}; foot.Children.Add(T("v"+Core.Version,12,MutedBrush)); foot.Children.Add(Link("GitHub 项目 ↗",()=>Open("https://github.com/"+Core.Repository))); Grid.SetRow(foot,2); rail.Children.Add(foot);
        var main=new Grid{Margin=new Thickness(32,25,32,20)}; main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto}); main.RowDefinitions.Add(new RowDefinition()); main.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto}); Grid.SetColumn(main,1); layout.Children.Add(main);
        var title=new StackPanel(); heading.FontSize=28; heading.FontWeight=FontWeights.SemiBold; subtitle.Foreground=MutedBrush; subtitle.Margin=new Thickness(0,8,0,22); subtitle.TextWrapping=TextWrapping.Wrap; title.Children.Add(heading); title.Children.Add(subtitle); main.Children.Add(title);
        var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=content}; Grid.SetRow(scroll,1); main.Children.Add(scroll);
        var bottom=new StackPanel{Margin=new Thickness(0,15,0,0)}; banner.Foreground=MutedBrush; banner.TextWrapping=TextWrapping.Wrap; banner.Text="准备就绪 · 所有更改都会先展示确认摘要"; banner.Cursor=System.Windows.Input.Cursors.Hand;banner.ToolTip="点击查看执行日志";banner.MouseLeftButtonUp+=(s,e)=>Navigate("日志"); progress.Height=3; progress.Margin=new Thickness(0,10,0,0); progress.Maximum=100; bottom.Children.Add(banner); bottom.Children.Add(progress); Grid.SetRow(bottom,2); main.Children.Add(bottom);
        log.IsReadOnly=true; log.AcceptsReturn=true; log.TextWrapping=TextWrapping.NoWrap; log.VerticalScrollBarVisibility=ScrollBarVisibility.Auto; log.HorizontalScrollBarVisibility=ScrollBarVisibility.Auto; log.FontFamily=new FontFamily("Consolas"); log.FontSize=12; log.Background=B("#101620"); log.Foreground=TextBrush; log.BorderThickness=new Thickness(0); log.Padding=new Thickness(14); log.Height=350;
        drive.ItemsSource=DriveInfo.GetDrives().Where(x=>x.DriveType==DriveType.Fixed && x.IsReady).Select(x=>x.Name.Substring(0,1)).ToArray(); drive.SelectedItem="D"; if(drive.SelectedIndex<0) drive.SelectedIndex=0;
        drive.Height=38; drive.FontSize=15; port.Text="10808"; port.Height=38; port.Padding=new Thickness(10,6,10,6);
        guard.Content="同时安装启动保护与自动维护"; guard.Foreground=TextBrush; guard.Margin=new Thickness(0,16,0,16);
        Closing+=(s,e)=>{if(busy){e.Cancel=true; MessageBox.Show(this,"操作正在执行，请等待结果后再关闭窗口。","正在执行",MessageBoxButton.OK,MessageBoxImage.Information);}};
        Navigate("概览"); Loaded+=async(s,e)=>{if(!preview) { await Run("status","读取当前状态",false); await CheckUpdates(true); }};
    }
    static Brush B(string hex) { return (Brush)new BrushConverter().ConvertFromString(hex); }
    static TextBlock T(string text,int size,Brush color) { return new TextBlock{Text=text,FontSize=size,Foreground=color,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,8)}; }
    Button Button(string label,Action action,bool primary) {
        var b=new Button{Content=label,Background=primary?Accent:B("#293348"),Foreground=primary?B("#101B31"):TextBrush,BorderThickness=new Thickness(0),Padding=new Thickness(18,10,18,10),Cursor=System.Windows.Input.Cursors.Hand,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,4,10,4)};
        var factory=new FrameworkElementFactory(typeof(Border)); factory.SetValue(Border.CornerRadiusProperty,new CornerRadius(8)); factory.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));
        var presenter=new FrameworkElementFactory(typeof(ContentPresenter)); presenter.SetValue(ContentPresenter.MarginProperty,new TemplateBindingExtension(Control.PaddingProperty)); presenter.SetValue(ContentPresenter.HorizontalAlignmentProperty,new TemplateBindingExtension(Control.HorizontalContentAlignmentProperty)); factory.AppendChild(presenter); b.Template=new ControlTemplate(typeof(Button)){VisualTree=factory};
        b.MouseEnter+=(s,e)=>b.Opacity=.8; b.MouseLeave+=(s,e)=>b.Opacity=1; b.Click+=(s,e)=>action(); return b;
    }
    Button Link(string label,Action action) { var b=Button(label,action,false); b.Background=Brushes.Transparent; b.Foreground=Accent; b.Padding=new Thickness(0,6,0,6); b.FontSize=12; return b; }
    StackPanel Card(string title,string description) { var p=new StackPanel{Margin=new Thickness(22,18,22,18)}; p.Children.Add(T(title,18,TextBrush)); if(description.Length>0)p.Children.Add(T(description,13,MutedBrush)); var box=new Border{Background=PanelBrush,CornerRadius=new CornerRadius(12),Margin=new Thickness(0,0,0,14),Child=p}; content.Children.Add(box); return p; }
    void Row(StackPanel parent,params UIElement[] items) { var row=new WrapPanel(); foreach(var item in items)row.Children.Add(item); parent.Children.Add(row); }
    void Navigate(string name) { foreach(var control in new FrameworkElement[]{drive,port,guard,log}) { var parent=control.Parent as Panel; if(parent!=null)parent.Children.Remove(control); } page=name; content.Children.Clear(); heading.Text=name; foreach(var b in navigation){b.Background=Convert.ToString(b.Content)==name?B("#2E4063"):Brushes.Transparent; b.Foreground=Convert.ToString(b.Content)==name?Accent:MutedBrush;}
        if(name=="概览")Overview(); else if(name=="初始化")Wizard(); else if(name=="维护恢复")Maintenance(); else if(name=="更新")Updates(); else LogsPage();
    }
    void Overview() {
        subtitle.Text="把工作区、模型设置、CUA 修复与启动保护放在一起。";
        var hero=Card("让 Codex 准备好开始工作","按步骤配置工作区，完成文件夹管理后执行 CUA 检查与修复。每一步都有日志，关键更改先确认。");
        Row(hero,Button("开始初始化  →",()=>{wizard=0;Navigate("初始化");},true),Button("刷新状态",async()=>await Run("status","读取当前状态",false),false));
        var p=Card("当前环境",status==null?(preview?"界面预览 · 未执行检测":"正在读取，或点击上方刷新状态。"):"最近一次检测结果");
        if(status!=null) {
            var labels=new Dictionary<string,string>{{"desktopVersion","桌面版本"},{"desktopHealth","组件状态"},{"configModel","当前模型"},{"configEffort","当前推理强度"},{"protectionInstalled","启动保护已安装"},{"processRunning","Codex 正在运行"},{"backupPath","初始化备份"}};
            foreach(var pair in labels) p.Children.Add(T(pair.Value+"   "+Format(status.ContainsKey(pair.Key)?status[pair.Key]:null),13,MutedBrush));
        }
        var model=Card("默认模型","父模型  GPT6.1 SOL · 中等推理\n子代理  GPT5.6 LUNA · MAX · 最多 6 个，单层调用");
        model.Children.Add(T("gpt-6.1-sol / medium    ·    gpt-5.6-luna / max",12,Accent));
    }
    static string Format(object value) { if(value==null)return "未检测"; if(value is bool)return (bool)value?"是":"否"; var text=Convert.ToString(value);if(text=="healthy")return "完整";if(text=="missing")return "未找到";if(text=="incomplete")return "组件不完整";return text; }
    int Port() { int v; if(!int.TryParse(port.Text,out v)||v<1||v>65535)throw new InvalidOperationException("代理端口需为 1–65535 的整数。"); return v; }
    string Drive() { if(drive.SelectedItem==null)throw new InvalidOperationException("请选择可用的工作盘。"); return Convert.ToString(drive.SelectedItem); }
    void Wizard() {
        subtitle.Text="1  选择工作区     →     2  确认变更     →     3  执行与结果";
        if(wizard==0) {
            var p=Card("01   选择工作盘与代理","工作区会创建在所选盘的 Codex 文件夹。已有工作文件不会自动搬迁。");
            p.Children.Add(T("工作盘",13,TextBrush)); p.Children.Add(drive); p.Children.Add(T("\n本地 HTTP 代理端口",13,TextBrush)); p.Children.Add(port); p.Children.Add(guard);
            p.Children.Add(Button("下一步：查看变更  →",()=>{try{Port();Drive();wizard=1;Navigate("初始化");}catch(Exception ex){Error(ex);}},true));
            Card("按顺序完成","备份并重置初始化配置  →  创建工作目录并绑定  →  检查 / 修复 CUA  →  写入 Luna 子代理规则\n如勾选启动保护，最后安装保护器和登录自动维护。");
        } else if(wizard==1) {
            var p=Card("02   确认初始化","将更新当前 Windows 用户的 Codex 配置。执行时会请求管理员授权。");
            p.Children.Add(T(InitSummary(),14,TextBrush));
            Row(p,Button("返回修改",()=>{wizard=0;Navigate("初始化");},false),Button("确认并开始初始化",async()=>{if(Confirm("开始初始化",InitSummary()+"\n\n将停止正在运行的 Codex 进程。请先保存其他窗口的工作。")){wizard=2;Navigate("初始化");await Run("initialize","初始化",false);}},true));
        } else { var p=Card("03   执行与结果",busy?"正在执行，请保留窗口。":"完整结果与备份位置见下方日志。"); p.Children.Add(log); Row(p,Button("查看全部日志",()=>Navigate("日志"),false),Button("重新设置",()=>{if(!busy){wizard=0;Navigate("初始化");}},false)); }
    }
    string InitSummary() { return "工作区："+Drive()+":\\Codex\n父模型：GPT6.1 SOL / medium\n子代理：GPT5.6 LUNA / max\n代理：http://127.0.0.1:"+Port()+"\n创建 8 个工作子目录，更新配置、规则与用户 PATH\n文件夹管理完成后检查 / 修复 CUA\n启动保护："+(guard.IsChecked==true?"安装，包含登录自动维护":"此次不安装")+"\n初始化和 CUA 分别备份，回滚范围见维护恢复页面。"; }
    void ActionCard(string title,string description,string label,string op,bool danger) {
        var p=Card(title,description); p.Children.Add(Button(label,async()=>await Run(op,title,true),!danger));
    }
    void Maintenance() {
        subtitle.Text="独立检查、修复和恢复。修改操作执行前会说明影响范围。";
        ActionCard("CUA 组件","检查官方桌面应用中的浏览器自动化文件，并在需要时修复。修复会保留独立备份。","检查 CUA","cua-check",false);
        var cua=Card("修复 CUA","仅在校验异常时修复。修复前备份原文件，不重置模型或工作区。"); cua.Children.Add(Button("检查并修复",async()=>await Run("cua-repair","CUA 修复",true),true));
        var g=Card("启动保护","管理独立 CLI、桌面原生模式与登录自动维护。更新或切换运行时前请关闭 Codex 桌面应用。");
        Row(g,Button("安装 / 修复保护",async()=>await Run("guard-install","安装启动保护",true),true),Button("更新官方 CLI",async()=>await Run("guard-update","更新官方 CLI",true),false),Button("诊断",async()=>await Run("doctor","环境诊断",false),false));
        Row(g,Button("恢复桌面原生",async()=>await Run("guard-native","恢复桌面原生模式",true),false),Button("切换官方独立 CLI",async()=>await Run("guard-current","切换官方独立 CLI",true),false));
        var r=Card("恢复与回滚","初始化回滚只恢复初始化备份，不恢复 CUA 文件，也不删除已创建的工作目录。保护器回滚使用独立的运行时备份。");
        Row(r,Button("回滚最新初始化",async()=>await Run("init-rollback","回滚最新初始化",true),false),Button("回滚保护器",async()=>await Run("guard-rollback","回滚保护器",true),false),Button("卸载启动保护",async()=>await Run("guard-uninstall","卸载启动保护",true),false));
        var proxy=Card("代理设置","单独写入当前用户 Codex 的本地代理设置。"); proxy.Children.Add(T("端口在初始化页面填写，当前："+port.Text,13,MutedBrush)); proxy.Children.Add(Button("更新代理",async()=>await Run("proxy","更新代理",true),false));
    }
    void Updates() {
        subtitle.Text="从项目 GitHub 发现新版本，确认后下载、校验并替换。";
        var p=Card("当前版本  v"+Core.Version,update==null?"尚未获得更新清单。应用启动时会自动检查，可手动重试。":"GitHub 最新版本  v"+Core.Value(update,"version","未知"));
        Row(p,Button("检查更新",async()=>await CheckUpdates(false),true),Button("打开版本发布页 ↗",()=>Open("https://github.com/"+Core.Repository+"/releases"),false));
        if(update!=null && new Version(Core.Value(update,"version","0.0.0"))>new Version(Core.Version)) { p.Children.Add(T(Core.Value(update,"notes","有新版本可用。"),14,Accent)); p.Children.Add(Button("下载并更新",async()=>await InstallUpdate(),true)); }
        Card("更新如何进行","使用固定 GitHub 提交中的更新清单，校验仓库来源和 SHA-256。确认后下载到当前 EXE 旁；退出后替换并重启，旧版本会留作备份。\n检查失败不影响本地功能。网络请求使用初始化页面中的本地代理端口。");
    }
    void LogsPage() { subtitle.Text="查看真实执行输出、失败原因和备份位置。"; var p=Card(operationTitle.Length>0?operationTitle:"执行日志","日志保存在当前用户 LocalAppData\\CodexInitKit\\Desktop\\Logs。"); p.Children.Add(log); Row(p,Button("打开日志目录",()=>Open(Core.Logs),false),Button("复制日志",()=>Clipboard.SetText(log.Text.Length>0?log.Text:"尚无日志"),false)); }
    bool Confirm(string title,string message) { return MessageBox.Show(this,message+"\n\n是否继续？",title,MessageBoxButton.YesNo,MessageBoxImage.Question,MessageBoxResult.No)==MessageBoxResult.Yes; }
    void Error(Exception ex) { banner.Text=ex.Message; MessageBox.Show(this,ex.Message,"操作未完成",MessageBoxButton.OK,MessageBoxImage.Error); }
    void Open(string target) { try{Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}catch(Exception ex){Error(ex);} }
    string Impact(string op) {
        switch(op) {
            case "cua-repair":return "检查并修复官方桌面应用的 CUA 组件，修复前单独备份。";
            case "proxy":return "写入当前用户的 Codex 代理端口："+Port()+"。";
            case "init-rollback":return "恢复最新初始化备份，可能停止 Codex 进程。CUA 和已创建的工作目录不在此回滚范围内。";
            case "guard-uninstall":return "移除保护器管理的登录任务、启动入口与配置。";
            case "guard-native":return "恢复桌面应用自带 CLI，并清理保护器拥有的覆盖配置。请先关闭 Codex 桌面应用。";
            case "guard-rollback":return "将保护器管理的 CLI 恢复到备份版本。请先关闭 Codex 桌面应用。";
            case "guard-current":return "切换到保护器管理的官方独立 CLI。请先关闭 Codex 桌面应用。";
            case "guard-update":return "从官方渠道下载并更新保护器管理的 CLI。请先关闭 Codex 桌面应用。";
            default:return "安装或修复当前用户的启动保护与登录自动维护任务。";
        }
    }
    async Task Run(string op,string title,bool confirm) {
        if(busy){banner.Text="请等待当前操作完成。";return;}
        try {
            int proxy=(op=="initialize"||op=="proxy")?Port():10808; string disk=op=="initialize"?Drive():Environment.SystemDirectory.Substring(0,1);
            if(confirm && !Confirm(title,Impact(op)))return;
            busy=true; operationTitle=title; banner.Text=title+" · 正在启动"; progress.IsIndeterminate=true;
            var id=Guid.NewGuid().ToString("N"); lastLog=Path.Combine(Core.Logs,id+".log"); log.Text="";
            using(var process=Core.StartWorker(op,id,disk,proxy,op=="initialize"&&guard.IsChecked==true)) {
                while(!process.HasExited) { await Task.Delay(300); ReadLog(); }
                ReadLog(); progress.IsIndeterminate=false;
                if(process.ExitCode!=0) { banner.Text=title+"未完成 · 退出码 "+process.ExitCode+" · 查看日志了解原因"; if(op!="status")MessageBox.Show(this,banner.Text,title,MessageBoxButton.OK,MessageBoxImage.Warning); }
                else {progress.Value=100;banner.Text=title+"完成 · 日志已保存"; if(op=="status" && page=="概览")Navigate("概览");}
            }
        } catch(System.ComponentModel.Win32Exception ex) { if(ex.NativeErrorCode==1223)banner.Text="已取消管理员授权，未开始执行。";else Error(ex); }
          catch(Exception ex) { Error(ex); }
        finally {busy=false;progress.IsIndeterminate=false;}
    }
    void ReadLog() {
        if(!File.Exists(lastLog))return;
        string text; using(var fs=new FileStream(lastLog,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))using(var reader=new StreamReader(fs,Encoding.UTF8))text=reader.ReadToEnd();
        var display=new StringBuilder(); foreach(var line in text.Split('\n')) {
            if(line.StartsWith("@@STATUS@@")) {try{status=Core.ReadJson(line.Substring(10).Trim());}catch(Exception ex){display.AppendLine("状态输出格式无效："+ex.Message);} }
            else if(line.StartsWith("@@PROGRESS@@")) {try{var p=Core.ReadJson(line.Substring(12).Trim());banner.Text=ProgressTitle(Core.Value(p,"title",operationTitle));int step,total;if(int.TryParse(Core.Value(p,"step","0"),out step)&&int.TryParse(Core.Value(p,"total","0"),out total)&&total>1){progress.IsIndeterminate=false;progress.Value=100.0*Math.Max(0,step-1)/total;}display.AppendLine(banner.Text);}catch(Exception ex){display.AppendLine("阶段输出格式无效："+ex.Message);} }
            else display.AppendLine(line.TrimEnd('\r'));
        }
        log.Text=display.ToString();log.ScrollToEnd();
    }
    static string ProgressTitle(string value) {
        var titles=new Dictionary<string,string>{{"Confirm folder-management drive","确认工作盘"},{"Run full initialization and backup","备份并完成初始化配置"},{"Install folder management and binding","安装文件夹管理并绑定工作区"},{"Check and repair CUA runtime","检查并按需修复 CUA"},{"Install and verify Luna configuration","安装并校验 Luna 配置"},{"Rebuild proxy configuration","重建代理配置"},{"Rollback latest initialization backup","回滚最新初始化备份"},{"Check CUA runtime","检查 CUA"},{"Repair CUA runtime","修复 CUA"}};
        return titles.ContainsKey(value)?titles[value]:value;
    }
    async Task CheckUpdates(bool silent) {
        if(busy)return;
        try {busy=true;banner.Text="正在检查 GitHub 更新…"; var proxy=Port(); update=await Task.Run(()=>Core.CheckUpdate(proxy)); var newer=new Version(Core.Value(update,"version","0.0.0"))>new Version(Core.Version); banner.Text=newer?"发现新版本 v"+Core.Value(update,"version","")+" · 在更新页面确认下载":"当前已是最新版本";if(page=="更新")Navigate("更新");}
        catch(Exception ex){banner.Text="更新检查未完成 · "+ex.Message;if(!silent && page=="更新")Navigate("更新");}
        finally {busy=false;}
    }
    async Task InstallUpdate() {
        if(busy || update==null)return;
        if(!Confirm("更新到 v"+Core.Value(update,"version",""),"下载并校验后，应用将退出、替换当前 EXE 并自动重启。原版本保留为备份。"))return;
        try{busy=true;progress.IsIndeterminate=true;banner.Text="正在下载并校验更新…";var stage=await Core.DownloadUpdate(update,Port());Core.ScheduleUpdate(stage);busy=false;Application.Current.Shutdown();}
        catch(Exception ex){Error(ex);}finally{busy=false;progress.IsIndeterminate=false;}
    }
}
}
