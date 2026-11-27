using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CodexKit {
public partial class MainWindow {
    const string PlaywrightDescription="微软官方开源工具，为 Codex 提供网页操作工具，可点击、填表和读取结果。";
    const string ChromeDevToolsDescription="Google Chrome 团队官方开源工具。为 Codex 提供 Chrome 调试工具，可查看网页报错、网络请求和加载性能。";
    const string PlaywrightExtensionStore="https://chromewebstore.google.com/detail/playwright-extension/mmlmfjhmonkocbjadbfplnigmagldckm";
    const string PlaywrightExtensionTitle="设置 Playwright 浏览器（可选）";
    const string FirefoxDownloadTitle="下载 Playwright 专用 Firefox";
    readonly CheckBox playwrightCli=new CheckBox(),chromeDevToolsMcp=new CheckBox();
    BrowserToolInstallation playwrightInstallation,chromeInstallation;
    internal Func<NativeBrowserTools> InspectBrowserTools;
    bool browserToolsPreview;
    bool previewFirefoxReady;

    void ConfigureFeatureHints(){
        foreach(var choice in new[]{playwrightCli,chromeDevToolsMcp}){
            choice.Style=(Style)Resources["KitCheck"];
            choice.IsChecked=false;
        }
        playwrightCli.Content="Playwright CLI";
        chromeDevToolsMcp.Content="Chrome DevTools MCP";
        SetFeatureHint(folderManagement,"创建标准工作目录，并将 Codex 工作区绑定到所选盘符。");
        SetFeatureHint(antiOverDefense,"为 Codex 写入工作规则，避免为实际用不到的情况增加复杂度。");
        SetFeatureHint(workJournal,"让 Codex 主动记录关键进度、证据、等待项和下一步，换对话后也能接着做。");
        SetFeatureHint(commandLineFirst,"有可靠命令行工具时，让 Codex 优先直接执行和核验。");
        SetFeatureHint(openSourceReference,"开发新功能时，Codex 会参考 GitHub 上的类似做法，再决定使用现成工具、借鉴思路或自行实现。");
        SetFeatureHint(memories,"允许 Codex 生成和使用本地记忆。关闭时保留已有记忆文件。");
        SetFeatureHint(guard,"检查和修复 Codex CLI 的启动入口，并安装登录时的维护任务。");
        SetFeatureHint(cuaRepair,"检查电脑操作组件，并在组件缺失或损坏时修复。");
        SetFeatureHint(browserRepair,"检查现有 Codex 浏览器组件，必要时恢复官方文件。");
        SetFeatureHint(officialCliUpdate,"检查并更新官方独立 Codex CLI。");
        SetFeatureHint(desktopUpdate,"通过 Microsoft Store 更新 Codex 桌面版及自带的 CLI。");
        SetFeatureHint(subagents,"允许父模型把可并行的任务分给子代理。可自动指派，也可指定子模型与思考强度。");
        SetFeatureHint(playwrightCli,"适合点击、填表和读取页面。可独立启动 Chrome、Edge 或 Firefox，也可通过扩展接入已有的 Chrome 或 Edge 标签页。");
        SetFeatureHint(chromeDevToolsMcp,"适合排查 Chrome 网页报错、网络请求和加载性能。按需打开独立 Chrome 窗口，无需安装浏览器扩展。");
    }

    void SetFeatureHint(FrameworkElement choice,string detail){
        choice.ToolTip=new ToolTip{
            Style=(Style)Resources["KitFeatureHint"],
            Content=new TextBlock{
                Text=detail,FontFamily=FontFamily,FontSize=12,Foreground=TextBrush,
                MaxWidth=300,TextWrapping=TextWrapping.Wrap,LineHeight=19
            }
        };
        ToolTipService.SetInitialShowDelay(choice,350);
        ToolTipService.SetShowDuration(choice,14000);
        System.Windows.Automation.AutomationProperties.SetHelpText(choice,detail);
    }

    static void AddFeatureChoice(StackPanel panel,CheckBox choice,string description,bool separator=false){
        var prior=choice.Parent as Panel;
        if(prior!=null)prior.Children.Remove(choice);
        if(separator)panel.Children.Add(new Border{Height=1,Background=MorandiTheme.Border,Margin=new Thickness(0,2,0,3)});
        panel.Children.Add(choice);
        var note=T(description,12,MutedBrush);
        note.Margin=new Thickness(28,0,0,8);
        panel.Children.Add(note);
    }

    void ReadBrowserToolInstallations(){
        if(preview&&InspectBrowserTools==null)return;
        var tools=BrowserToolInspector();
        playwrightInstallation=ReadInstallation(tools.PlaywrightInstallation,"Playwright CLI");
        chromeInstallation=ReadInstallation(tools.ChromeInstallation,"Chrome DevTools MCP");
    }

    BrowserToolInstallation ReadInstallation(Func<BrowserToolInstallation> read,string name){
        try{return read();}
        catch(Exception ex){
            if(page=="初始化"&&configurationTab==1)banner.Text=name+" 安装状态未能读取："+DesktopLocalization.ExceptionText(ex);
            return BrowserToolInstallation.None;
        }
    }

    void AddBrowserToolChoice(StackPanel panel,CheckBox choice,string description,BrowserToolInstallation installation,bool playwright){
        if(installation==BrowserToolInstallation.None){
            AddFeatureChoice(panel,choice,description,true);
            return;
        }
        choice.IsChecked=false;
        panel.Children.Add(new Border{Height=1,Background=MorandiTheme.Border,Margin=new Thickness(0,2,0,3)});
        var row=new Grid{Margin=new Thickness(0,3,0,6)};
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
        var main=new StackPanel();
        var header=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,1,0,5),Background=Brushes.Transparent};
        var state=T("已安装",11,B("#BEE3D2"));state.Margin=new Thickness(0);
        header.Children.Add(new Border{Child=state,Background=B("#32574D"),CornerRadius=new CornerRadius(6),Padding=new Thickness(6,3,6,3),
            Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center});
        var label=T(Convert.ToString(choice.Content),13,TextBrush);
        label.Margin=new Thickness(0);
        label.VerticalAlignment=VerticalAlignment.Center;
        header.Children.Add(label);
        SetFeatureHint(header,System.Windows.Automation.AutomationProperties.GetHelpText(choice));
        main.Children.Add(header);
        var note=T(description,12,MutedBrush);note.Margin=new Thickness(0,0,10,0);
        main.Children.Add(note);
        row.Children.Add(main);

        var button=Button("卸载",async()=>await RemoveBrowserTool(playwright),false);
        button.Name=playwright?"RemovePlaywrightCli":"RemoveChromeDevToolsMcp";
        button.HorizontalAlignment=HorizontalAlignment.Right;
        button.VerticalAlignment=VerticalAlignment.Center;
        button.Margin=new Thickness(9,0,0,0);
        button.Background=MorandiTheme.Danger;
        button.Foreground=Brushes.White;
        button.IsEnabled=CanRemoveBrowserTool(button.Name);
        System.Windows.Automation.AutomationProperties.SetName(button,"卸载 "+label.Text);
        if(playwright&&installation==BrowserToolInstallation.External)
            button.ToolTip="由其他方式安装，请使用原安装方式卸载。";
        ToolTipService.SetShowOnDisabled(button,true);
        var rowActions=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
        if(playwright&&installation==BrowserToolInstallation.Managed){
            var setup=Button("浏览器设置",()=>{if(!busy)CreatePlaywrightExtensionDialog(false).ShowDialog();},false);
            setup.Tag=UiIcons.For("浏览器");
            setup.Name="SetUpPlaywrightBrowsers";
            setup.Margin=new Thickness(0);
            rowActions.Children.Add(setup);
        }
        rowActions.Children.Add(button);
        Grid.SetColumn(rowActions,1);
        row.Children.Add(rowActions);
        panel.Children.Add(row);
    }

    bool CanRemoveBrowserTool(string buttonName){
        return !busy&&(buttonName=="RemovePlaywrightCli"?playwrightInstallation==BrowserToolInstallation.Managed:chromeInstallation!=BrowserToolInstallation.None);
    }

    async Task RemoveBrowserTool(bool playwright){
        if(busy)return;
        var title=playwright?"卸载 Playwright CLI":"卸载 Chrome DevTools MCP";
        bool external=!playwright&&chromeInstallation==BrowserToolInstallation.External;
        var impact=playwright
            ?"移除 Init Kit 安装的 Playwright CLI、Codex 技能和已下载的专用浏览器。日常浏览器、扩展和登录状态会保留。"
            :external?"从 Codex 移除 Chrome DevTools MCP 配置。外部安装的组件和 Chrome 会保留。"
            :"移除 Init Kit 安装的 Chrome DevTools MCP 组件和 Codex 配置。Chrome 和其他 MCP 服务会保留。";
        if(browserToolsPreview){
            if(Confirm(title,impact)){
                if(playwright){playwrightInstallation=BrowserToolInstallation.None;previewFirefoxReady=false;}
                else chromeInstallation=BrowserToolInstallation.None;
                banner.Text=title+" · 预览完成";
            }
        }else await Run(playwright?NativeBrowserTools.RemovePlaywrightOperation:NativeBrowserTools.RemoveChromeDevToolsOperation,title,true,null,impact);
        if(page=="初始化"&&wizard==0)Navigate("初始化");
    }

    internal void OpenBrowserToolsPreview(){
        if(!preview)throw new InvalidOperationException("浏览器工具预览需要在预览模式下运行。");
        browserToolsPreview=true;
        Title="Codex Init Kit · 浏览器工具预览";
        Width=1120;
        Height=960;
        remoteRelay.IsChecked=false;
        status=new Dictionary<string,object>{
            {"configProxy","未配置 HTTP 代理"},{"configModel",Core.DefaultParentModel},{"configEffort",Core.DefaultParentEffort}
        };
        LaunchCodexDesktop=()=>{banner.Text="浏览器工具预览 · 启动 Codex 按钮演示完成";};
        configurationTab=1;
        banner.Text="浏览器工具预览 · 不会修改 Codex 配置";
        updateStatus.Text="本地预览";
        Navigate("初始化");
    }

    string BrowserToolsSelection(){
        var selected=new List<string>();
        foreach(var tool in new[]{new{Choice=playwrightCli,Installation=playwrightInstallation},new{Choice=chromeDevToolsMcp,Installation=chromeInstallation}}){
            if(tool.Installation!=BrowserToolInstallation.None)selected.Add(tool.Choice.Content+"（已安装）");
            else if(tool.Choice.IsChecked==true)selected.Add(Convert.ToString(tool.Choice.Content));
        }
        var labels=String.Join("、",selected);
        return labels.Length>0?labels:"跳过";
    }

    void ResetBrowserToolPreviewStages(){
        initializationProgress.Reset(Options(),guard.IsChecked==true);
        initializationState="";
        initializationSummary="";
        wizard=2;
    }

    async Task PreviewBrowserToolInitialization(){
        busy=true;
        activeOperation="initialize";
        Window checking=null;
        bool checkingFinished=false;
        try{
            ResetBrowserToolPreviewStages();
            if(IsVisible){
                checking=CreatePreflightDialog();
                checking.Closing+=(s,e)=>e.Cancel=!checkingFinished;
                checking.Show();
                IsEnabled=false;
            }
            await Task.Delay(750);
            checkingFinished=true;
            if(checking!=null){
                checking.Close();
                checking=null;
                IsEnabled=true;
            }
            Navigate("初始化");
            foreach(var stage in initializationProgress.Stages){
                stage.State="running";
                UpdateStageView();
                await Task.Delay(115);
                stage.State="complete";
            }
            initializationProgress.Ended=DateTime.UtcNow;
            initializationState="complete";
        }finally{
            checkingFinished=true;
            if(checking!=null)checking.Close();
            IsEnabled=true;
            busy=false;
            activeOperation="";
        }
        Navigate("初始化");
        ShowInitializationResult(0,"",false,browserRepair.IsChecked==true);
        if(playwrightCli.IsChecked==true)playwrightInstallation=BrowserToolInstallation.Managed;
        if(chromeDevToolsMcp.IsChecked==true)chromeInstallation=BrowserToolInstallation.Managed;
    }

    Window CreatePlaywrightExtensionDialog(bool duringInitialization=true){
        TextBlock note;
        WrapPanel actions;
        var dialog=CreateMessageDialog(PlaywrightExtensionTitle,"Edge 和 Chrome 可通过扩展操作已打开的网页。需要独立使用 Firefox 时，可以下载 Playwright 专用版本。",out note,out actions);
        bool downloading=false;
        dialog.Closing+=(s,e)=>e.Cancel=downloading;
        foreach(var browser in new[]{"Edge","Chrome"}){
            var button=Button(browser,()=>OpenPlaywrightExtensionStore(browser,note),false);
            button.Tag=UiIcons.For("浏览器");
            button.MinWidth=86;
            actions.Children.Add(button);
        }
        var firefox=Button("Firefox",async()=>{
            if(downloading)return;
            var tools=BrowserToolInspector();
            bool alreadyDownloaded=preview?previewFirefoxReady:tools.PlaywrightFirefoxDownloaded();
            if(preview?previewFirefoxReady:tools.PlaywrightFirefoxReady()){
                note.Text="Playwright 专用 Firefox 已就绪。可在 Codex 中选择 Firefox 使用。";
                note.Foreground=MutedBrush;
                return;
            }
            if(!alreadyDownloaded&&CreateFirefoxDownloadConfirmation(dialog).ShowDialog()!=true)return;
            downloading=true;
            actions.IsEnabled=false;
            note.Foreground=MutedBrush;
            note.Text=alreadyDownloaded?"正在核对 Playwright 专用 Firefox…":"正在下载 Playwright 专用 Firefox…";
            try{
                if(preview){await Task.Delay(200);previewFirefoxReady=true;}
                else await Run(NativeBrowserTools.InstallFirefoxOperation,FirefoxDownloadTitle,false);
                if(preview?previewFirefoxReady:tools.PlaywrightFirefoxReady()){
                    note.Text="Playwright 专用 Firefox 已就绪。可在 Codex 中选择 Firefox 使用。";
                }else{
                    note.Text="Firefox 未准备完成。可查看执行日志后重试，或直接继续。";
                    note.Foreground=MorandiTheme.Warning;
                }
            }finally{
                downloading=false;
                actions.IsEnabled=true;
            }
        },false);
        firefox.Tag=UiIcons.For("浏览器");
        firefox.MinWidth=86;
        actions.Children.Add(firefox);
        var next=Button(duringInitialization?"下一步":"关闭",()=>Motion.Close(dialog,true,preview),true);
        next.IsDefault=true;
        next.IsCancel=true;
        next.Margin=new Thickness(8,4,0,4);
        actions.Children.Add(next);
        return dialog;
    }

    NativeBrowserTools BrowserToolInspector(){
        return InspectBrowserTools==null?new NativeBrowserTools(CodexKit.Native.NativePaths.Current(),null,null):InspectBrowserTools();
    }

    Window CreateFirefoxDownloadConfirmation(Window owner){
        TextBlock note;
        WrapPanel actions;
        var dialog=CreateMessageDialog(FirefoxDownloadTitle,"Playwright 会下载一份专用的 Firefox 到自己的目录，用于独立打开网页。它不会改动系统默认浏览器。是否确认下载？",out note,out actions);
        if(owner!=null&&owner.IsVisible)dialog.Owner=owner;
        var cancel=Button("取消",()=>Motion.Close(dialog,false,preview),false);
        cancel.IsDefault=true;
        cancel.IsCancel=true;
        actions.Children.Add(cancel);
        actions.Children.Add(Button("确认下载",()=>Motion.Close(dialog,true,preview),true));
        return dialog;
    }

    static void OpenPlaywrightExtensionStore(string browser,TextBlock note){
        var pathToBrowser=NativeBrowserTools.FindInstalledBrowser(browser);
        if(pathToBrowser==null){
            note.Text="这台电脑未找到 "+browser+"。你可以选另一个浏览器，或点击「下一步」跳过。";
            note.Foreground=MorandiTheme.Warning;
            return;
        }
        try{
            using(var process=Process.Start(new ProcessStartInfo(pathToBrowser,PlaywrightExtensionStore){UseShellExecute=false})){
                if(process==null)throw new InvalidOperationException("浏览器没有启动。");
            }
            note.Text="已在 "+browser+" 中打开微软官方扩展页面。"+(browser=="Edge"?"如有提示，请在 Edge 中允许来自其他应用商店的扩展。":"")+"确认添加后，回到这里点击「下一步」。";
            note.Foreground=MutedBrush;
        }catch(Exception ex){
            note.Text="无法打开 "+browser+"。"+DesktopLocalization.ExceptionText(ex)+" 仍可点击「下一步」。";
            note.Foreground=MorandiTheme.Warning;
        }
    }

    internal void RenderBrowserToolsPreview(string directory){
        Directory.CreateDirectory(directory);
        OpenBrowserToolsPreview();
        if(playwrightCli.IsChecked!=false||chromeDevToolsMcp.IsChecked!=false)
            throw new InvalidOperationException("浏览器工具必须默认关闭。");
        SaveBrowserToolView(directory,"01-功能选择",null,null);
        SaveBrowserToolView(directory,"02-Playwright-说明气泡",playwrightCli,null);
        SaveBrowserToolView(directory,"03-Chrome-DevTools-说明气泡",chromeDevToolsMcp,null);
        playwrightCli.IsChecked=chromeDevToolsMcp.IsChecked=true;
        wizard=1;
        Navigate("初始化");
        SaveBrowserToolView(directory,"04-配置确认",null,null);
        SaveBrowserToolView(directory,"05-前置检查弹窗",null,CreatePreflightDialog());
        ResetBrowserToolPreviewStages();
        busy=true;
        activeOperation="initialize";
        initializationProgress.Stages[0].State="running";
        Navigate("初始化");
        if(stageLabels["playwright"][0].ToolTip!=null)
            throw new InvalidOperationException("待执行的步骤仍存在空白提示。");
        SaveBrowserToolView(directory,"06-初始化中",null,null);
        busy=false;
        activeOperation="";
        initializationState="complete";
        foreach(var stage in initializationProgress.Stages)stage.State="complete";
        initializationProgress.Ended=DateTime.UtcNow;
        Navigate("初始化");
        var titles=new List<string>();
        PresentInitializationDialog=dialog=>{
            titles.Add(dialog.Title);
            var file=titles.Count==1?"07-安装扩展":"08-启动或关闭";
            SaveBrowserToolView(directory,file,null,dialog);
        };
        ShowInitializationResult(0,"",false,true);
        if(!titles.SequenceEqual(new[]{PlaywrightExtensionTitle,"已完成初始化"}))
            throw new InvalidOperationException("Playwright CLI 成功后未按顺序显示两个弹窗。");
        VerifyExtensionNext();
        titles.Clear();
        PresentInitializationDialog=dialog=>{titles.Add(dialog.Title);dialog.Close();};
        playwrightCli.IsChecked=false;
        ResetBrowserToolPreviewStages();
        foreach(var stage in initializationProgress.Stages)stage.State="complete";
        ShowInitializationResult(0,"",false,true);
        if(!titles.SequenceEqual(new[]{"已完成初始化"}))
            throw new InvalidOperationException("只选 Chrome DevTools MCP 时不应弹出扩展安装。");
        titles.Clear();
        playwrightCli.IsChecked=true;
        ShowInitializationResult(2,"",false,true);
        if(!titles.SequenceEqual(new[]{"初始化部分完成"}))
            throw new InvalidOperationException("初始化未完成时不应弹出扩展安装。");
        PresentInitializationDialog=null;
        wizard=0;
        configurationTab=1;
        playwrightInstallation=chromeInstallation=BrowserToolInstallation.Managed;
        Navigate("初始化");
        SaveBrowserToolView(directory,"09-浏览器工具已安装",null,null);
        SaveBrowserToolView(directory,"10-Firefox下载确认",null,CreateFirefoxDownloadConfirmation(null));
        VerifyFirefoxConfirmation();
    }

    void VerifyFirefoxConfirmation(){
        foreach(bool accept in new[]{false,true}){
            var dialog=CreateFirefoxDownloadConfirmation(null);
            var buttons=InitializationFlowTests.Descendants<Button>((DependencyObject)dialog.Content).ToArray();
            if(!buttons.Select(button=>Convert.ToString(button.Content)).SequenceEqual(new[]{"取消","确认下载"}))
                throw new InvalidOperationException("Firefox 下载确认缺少所需操作。");
            dialog.ShowActivated=false;
            dialog.WindowStartupLocation=WindowStartupLocation.Manual;
            dialog.Left=-20000;
            dialog.Top=-20000;
            dialog.ContentRendered+=(s,e)=>buttons[accept?1:0].RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
            if((dialog.ShowDialog()==true)!=accept)throw new InvalidOperationException("Firefox 下载未按确认结果处理。");
        }
    }

    void VerifyExtensionNext(){
        var dialog=CreatePlaywrightExtensionDialog();
        var buttons=InitializationFlowTests.Descendants<Button>((DependencyObject)dialog.Content).ToArray();
        if(!buttons.Select(button=>Convert.ToString(button.Content)).SequenceEqual(new[]{"Edge","Chrome","Firefox","下一步"}))
            throw new InvalidOperationException("扩展弹窗缺少浏览器按钮或下一步。");
        dialog.ShowActivated=false;
        dialog.WindowStartupLocation=WindowStartupLocation.Manual;
        dialog.Left=-20000;
        dialog.Top=-20000;
        dialog.ContentRendered+=(s,e)=>buttons.Last().RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        if(dialog.ShowDialog()!=true)throw new InvalidOperationException("下一步按钮没有完成扩展弹窗。");
    }

    void SaveBrowserToolView(string directory,string name,CheckBox hover,Window dialog){
        const int width=1120,height=960;
        var view=(FrameworkElement)Content;
        view.Measure(new Size(width,height));
        view.Arrange(new Rect(0,0,width,height));
        view.UpdateLayout();
        PumpLogLayout();
        var baseImage=CaptureBrowserToolVisual(view,width,height);
        var composite=new DrawingVisual();
        using(var drawing=composite.RenderOpen()){
            drawing.DrawImage(baseImage,new Rect(0,0,width,height));
            if(hover!=null){
                var hint=(ToolTip)hover.ToolTip;
                hint.Measure(new Size(370,130));
                Size size=hint.DesiredSize;
                hint.Arrange(new Rect(new Point(),size));
                hint.UpdateLayout();
                var anchor=hover.TranslatePoint(new Point(26,hover.ActualHeight+2),view);
                drawing.DrawImage(CaptureBrowserToolVisual(hint,size.Width,size.Height),new Rect(anchor,size));
            }
            if(dialog!=null){
                dialog.ShowActivated=false;
                dialog.WindowStartupLocation=WindowStartupLocation.Manual;
                dialog.Left=-20000;
                dialog.Top=-20000;
                dialog.Show();
                dialog.UpdateLayout();
                PumpLogLayout();
                var size=new Size(dialog.ActualWidth,dialog.ActualHeight);
                var popup=CaptureBrowserToolVisual(dialog,size.Width,size.Height);
                drawing.DrawImage(popup,new Rect((width-size.Width)/2,(height-size.Height)/2,size.Width,size.Height));
                dialog.Close();
            }
        }
        var encoder=new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(CaptureBrowserToolVisual(composite,width,height)));
        using(var stream=File.Create(Path.Combine(directory,name+".png")))encoder.Save(stream);
    }

    static RenderTargetBitmap CaptureBrowserToolVisual(Visual visual,double width,double height){
        const double scale=1.5;
        var image=new RenderTargetBitmap((int)Math.Ceiling(width*scale),(int)Math.Ceiling(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);
        image.Render(visual);
        return image;
    }
}
}
