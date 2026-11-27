using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace CodexKit {
public partial class MainWindow {
    const int NetworkStep=0, FeaturesStep=1, ModelsStep=2, WorkspaceStep=3;
    const string WorkRulePreferencesKey=@"HKEY_CURRENT_USER\Software\CodexInitKit\Desktop\WorkRules";
    readonly bool initializationDesignPreview;
    readonly string initializationPreviewPreferences;
    readonly CheckBox credentialManagement=new CheckBox();
    readonly TextBlock credentialManagementHint=new TextBlock();
    bool credentialManagementPreference=true;
    TextBlock workspaceDrivePlaceholder;
    Button workspaceContinue,featuresContinue,workspaceTab,modelTab;
    Border workspaceTabSlot;
    double workspaceTabWidth;

    void GoToConfigurationStep(int step){
        configurationTab=step;
        Navigate("初始化");
    }

    Button ConfigurationTab(string caption,int step){
        var button=Button(caption,()=>GoToConfigurationStep(step),configurationTab==step);
        button.Padding=new Thickness(14,7,14,7);
        button.Margin=new Thickness(0,0,5,0);
        return button;
    }

    void ConfigurationTabs(){
        if(configurationTab==WorkspaceStep&&folderManagement.IsChecked!=true)configurationTab=ModelsStep;
        var tabs=new WrapPanel{Margin=new Thickness(0,0,0,10)};
        tabs.Children.Add(ConfigurationTab("1  网络与转发",NetworkStep));
        tabs.Children.Add(ConfigurationTab("2  功能选择",FeaturesStep));
        workspaceTab=ConfigurationTab("3  工作区与目录",WorkspaceStep);
        workspaceTab.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));
        workspaceTabWidth=workspaceTab.DesiredSize.Width;
        var tabCanvas=new Canvas{Height=workspaceTab.DesiredSize.Height};
        tabCanvas.Children.Add(workspaceTab);
        workspaceTabSlot=new Border{ClipToBounds=true,Child=tabCanvas};
        tabs.Children.Add(workspaceTabSlot);
        modelTab=ConfigurationTab("4  模型设置",ModelsStep);
        tabs.Children.Add(modelTab);
        pageNavigation.Children.Add(tabs);
        UpdateConfigurationNavigation(false);
    }

    void UpdateConfigurationNavigation(bool animate){
        if(page!="初始化"||wizard!=0||workspaceTabSlot==null)return;
        bool enabled=folderManagement.IsChecked==true;
        workspaceTab.IsEnabled=enabled;
        workspaceTabSlot.IsHitTestVisible=enabled;
        modelTab.Content=enabled?"4  模型设置":"3  模型设置";
        if(featuresContinue!=null)featuresContinue.Content=enabled?"下一步：工作区与目录":"下一步：模型设置";
        double end=enabled?workspaceTabWidth:0;
        double start=workspaceTabSlot.Width;
        if(!Double.IsNaN(start)&&Math.Abs(start-end)<.1)return;
        workspaceTabSlot.BeginAnimation(WidthProperty,null);
        workspaceTabSlot.Width=end;
        if(!animate||Double.IsNaN(start)||!SystemParameters.ClientAreaAnimation)return;

        var slide=new DoubleAnimationUsingKeyFrames{FillBehavior=FillBehavior.Stop};
        slide.KeyFrames.Add(new LinearDoubleKeyFrame(start,KeyTime.FromTimeSpan(TimeSpan.Zero)));
        if(enabled)slide.KeyFrames.Add(new EasingDoubleKeyFrame(end+7,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(235)),new CubicEase{EasingMode=EasingMode.EaseOut}));
        slide.KeyFrames.Add(new EasingDoubleKeyFrame(end,KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(enabled?330:220)),new CubicEase{EasingMode=EasingMode.EaseOut}));
        workspaceTabSlot.BeginAnimation(WidthProperty,slide);
    }

    void FolderManagementChanged(){
        UpdateWorkspaceDriveHint();
        UpdateCredentialManagement();
        UpdateConfigurationNavigation(true);
    }

    void AddWorkspaceDrivePicker(StackPanel panel){
        panel.Children.Add(T("允许 Codex 完全管理的文件夹工作盘",14,TextBrush));
        var picker=new Grid{HorizontalAlignment=HorizontalAlignment.Left};
        picker.Children.Add(drive);
        workspaceDrivePlaceholder=new TextBlock{Text="请选择工作盘",Foreground=MutedBrush,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(12,0,0,0),IsHitTestVisible=false};
        picker.Children.Add(workspaceDrivePlaceholder);
        panel.Children.Add(picker);
        panel.Children.Add(workspaceDriveHint);
        UpdateWorkspaceDriveHint();
    }

    void ConfigureCredentialManagement(string keyPath){
        credentialManagement.Content="本机凭证管理";
        credentialManagement.Style=(Style)Resources["KitCheck"];
        SetFeatureHint(credentialManagement,"写入凭证管理规则。保存或登录时，Codex 需要本机已有的凭证索引和助手。");
        credentialManagementHint.FontSize=11;
        credentialManagementHint.Foreground=MutedBrush;
        credentialManagementHint.TextWrapping=TextWrapping.Wrap;
        credentialManagementHint.Margin=new Thickness(26,0,0,5);
        if(keyPath!=null){
            try{
                var saved=Registry.GetValue(keyPath,"credentialManagement",null);
                if(saved is int)credentialManagementPreference=(int)saved!=0;
            }catch(Exception ex){banner.Text="凭证管理选项未能读取："+DesktopLocalization.ExceptionText(ex);}
        }
        credentialManagement.Click+=(s,e)=>{
            credentialManagementPreference=credentialManagement.IsChecked==true;
            if(keyPath==null)return;
            try{Registry.SetValue(keyPath,"credentialManagement",credentialManagementPreference?1:0,RegistryValueKind.DWord);}
            catch(Exception ex){Error(ex);}
        };
        UpdateCredentialManagement();
    }

    void BindInitializationPreviewPreferences(){
        if(initializationPreviewPreferences==null)return;
        var savedFolder=Registry.GetValue(initializationPreviewPreferences,"folderManagement",1);
        folderManagement.IsChecked=!(savedFolder is int)||(int)savedFolder!=0;
        drive.SelectedItem=Registry.GetValue(initializationPreviewPreferences,"workspaceDrive","");
        folderManagement.Checked+=SavePreviewFolderPreference;
        folderManagement.Unchecked+=SavePreviewFolderPreference;
        drive.SelectionChanged+=(s,e)=>SaveInitializationPreviewPreference("workspaceDrive",Convert.ToString(drive.SelectedItem),RegistryValueKind.String);
    }

    void SavePreviewFolderPreference(object sender,RoutedEventArgs args){
        SaveInitializationPreviewPreference("folderManagement",folderManagement.IsChecked==true?1:0,RegistryValueKind.DWord);
    }

    void SaveInitializationPreviewPreference(string name,object value,RegistryValueKind kind){
        if(initializationPreviewPreferences!=null)Registry.SetValue(initializationPreviewPreferences,name,value,kind);
    }

    void UpdateCredentialManagement(){
        bool enabled=folderManagement.IsChecked==true;
        credentialManagement.IsEnabled=enabled&&folderManagement.IsEnabled;
        credentialManagement.IsChecked=enabled&&credentialManagementPreference;
        credentialManagementHint.Text=enabled?"引导 Codex 使用本机凭证索引和 Windows 凭据管理器。":"开启文件夹管理功能后可用。";
    }

    internal void RenderInitializationDesignPreview(string directory){
        Directory.CreateDirectory(directory);
        GoToConfigurationStep(NetworkStep);RenderView(directory,"01-network",1120,750);
        GoToConfigurationStep(FeaturesStep);RenderView(directory,"02-features-on",1120,750);
        folderManagement.IsChecked=false;
        UpdateConfigurationNavigation(false);
        workspaceTabSlot.BeginAnimation(WidthProperty,null);
        RenderView(directory,"03-features-off",1120,750);
        folderManagement.IsChecked=true;
        GoToConfigurationStep(WorkspaceStep);RenderView(directory,"04-workspace",1120,750);
    }
}
}
