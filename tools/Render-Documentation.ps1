[CmdletBinding()]
param([string]$Executable,[string]$OutputDirectory,[int]$Dpi=192)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $Executable){$Executable=Join-Path $root 'build\desktop\CodexInitKit.exe'}
if(-not $OutputDirectory){$OutputDirectory=Join-Path $root 'assets\screenshots'}
if($Dpi -lt 96 -or $Dpi -gt 384){throw 'DPI must be between 96 and 384.'}
$Executable=[IO.Path]::GetFullPath($Executable)
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(-not (Test-Path -LiteralPath $Executable -PathType Leaf)){throw 'Desktop EXE missing.'}
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$scratch=Join-Path $root 'build\documentation-renderer'
[void][IO.Directory]::CreateDirectory($scratch)
$source=@'
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
class DocumentationRenderer {
    [STAThread] static int Main(string[] args) {
        try {
            var assembly=Assembly.LoadFrom(args[0]);var type=assembly.GetType("CodexKit.MainWindow",true);
            var app=new Application();var window=(Window)Activator.CreateInstance(type,new object[]{true});
            var navigate=type.GetMethod("Navigate",BindingFlags.Instance|BindingFlags.NonPublic);
            var directory=args[1];var dpi=Int32.Parse(args[2]);Directory.CreateDirectory(directory);
            type.GetField("status",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,new Dictionary<string,object>{
                {"desktopVersion","示例"},{"desktopHealth","healthy"},{"runtimeMode","DesktopNative"},
                {"runtimeModeDetail","使用 Codex 桌面应用自带的 CLI。"},{"currentCliPath",null},
                {"configModel","gpt-6.1-sol"},{"configEffort","medium"},{"protectionInstalled",true},
                {"processRunning",false},{"backupPath","初始化完成后，可在此查看备份位置"},
                {"rollbackEntries",new object[]{
                    new Dictionary<string,object>{{"id",new string('a',64)},{"kind","init"},{"version","备份工具 v2.2.4"},{"time","2026-11-01 14:00:00"},{"available",true},{"detail","备份模型：gpt-6.1-sol"}},
                    new Dictionary<string,object>{{"id",new string('b',64)},{"kind","cli"},{"version","0.165.0"},{"time","2026-10-30 12:00:00"},{"available",true},{"detail","本地独立 CLI 缓存，执行前校验。"}}
                }}});
            var pages=new[]{"概览","维护恢复","浏览器连接","更新"};
            var files=new[]{"overview","maintenance","browser-connection","updates"};
            for(int i=0;i<pages.Length;i++){navigate.Invoke(window,new object[]{pages[i]});Save(window,directory,files[i],i==0?1100:i==1?1900:1200,dpi);}
            var sampleRoot=Path.Combine(Path.GetTempPath(),"CodexKit-doc-log-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(sampleRoot);
            try{
                var sample=Path.Combine(sampleRoot,"example.log");var sampleText=new System.Text.StringBuilder();for(int line=1;line<=40;line++)sampleText.AppendLine("示例检查记录 "+line+"：读取组件状态。");sampleText.AppendLine("Config written: C:\\Users\\示例用户\\.codex\\config.toml");sampleText.AppendLine("[INCOMPLETE] 更新桌面版：Desktop update failed. Exit code -2147012867. Microsoft Store: ms-windows-store://pdp/?ProductId=9PLM9XGG6VKS");sampleText.AppendLine("提示：自动更新未完成，无需担心；你可以自行在微软商店或 Codex 内手动更新。");sampleText.AppendLine("日志已保存；当前视图跟随底部最新内容。");File.WriteAllText(sample,sampleText.ToString());
                type.GetField("lastLog",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,sample);type.GetField("rawLogText",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,"");type.GetField("logInputComplete",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,true);type.GetField("operationTitle",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,"示例：执行结果与错误说明");type.GetMethod("ReadLog",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);navigate.Invoke(window,new object[]{"日志"});Save(window,directory,"logs",1000,dpi);
            }finally{type.GetField("lastLog",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,"");Directory.Delete(sampleRoot,true);}
            var adviceType=assembly.GetType("CodexKit.DiagnosticAdvice",true);var example=(string)adviceType.GetMethod("Build").Invoke(null,new object[]{new Dictionary<string,object>{{"standaloneHealthy",true},{"desktopFound",true},{"desktopHealthy",true}},1,adviceType.GetField("AsciiFixture").GetRawConstantValue()});type.GetField("diagnosticAdvice",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,example);
            var diagnosis=(Window)type.GetMethod("CreateDiagnosticDialog",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);var adviceView=(FrameworkElement)diagnosis.Content;adviceView.Measure(new Size(660,Double.PositiveInfinity));SaveView(adviceView,directory,"diagnosis",660,(int)Math.Ceiling(adviceView.DesiredSize.Height),dpi);diagnosis.Close();type.GetField("diagnosticAdvice",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(window,"");
            navigate.Invoke(window,new object[]{"初始化"});Save(window,directory,"settings-scroll",900,dpi);Save(window,directory,"settings",1800,dpi);
            var completion=(Window)type.GetMethod("CreateInitializationCompletedDialog",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(window,null);
            var popup=(FrameworkElement)completion.Content;popup.Measure(new Size(520,Double.PositiveInfinity));
            SaveView(popup,directory,"completed",520,(int)Math.Ceiling(popup.DesiredSize.Height),dpi);completion.Close();
            window.Close();return 0;
        }catch(Exception ex){Console.Error.WriteLine(ex);return 1;}
    }
    static void Save(Window window,string directory,string name,int height,int dpi) {
        SaveView((FrameworkElement)window.Content,directory,name,1120,height,dpi);
    }
    static void SaveView(FrameworkElement view,string directory,string name,int width,int height,int dpi) {
        view.Measure(new Size(width,height));view.Arrange(new Rect(0,0,width,height));view.UpdateLayout();
        var frame=new System.Windows.Threading.DispatcherFrame();view.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));System.Windows.Threading.Dispatcher.PushFrame(frame);view.UpdateLayout();
        var scale=dpi/96.0;var bitmap=new RenderTargetBitmap((int)Math.Round(width*scale),(int)Math.Round(height*scale),dpi,dpi,PixelFormats.Pbgra32);
        bitmap.Render(view);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
        using(var stream=File.Create(Path.Combine(directory,name+".png")))png.Save(stream);
    }
}
'@
$sourceFile=Join-Path $scratch 'DocumentationRenderer.cs'
[IO.File]::WriteAllText($sourceFile,$source,[Text.UTF8Encoding]::new($true))
$renderer=Join-Path $scratch 'DocumentationRenderer.exe'
$compile=@('/nologo','/target:exe','/platform:x64','/utf8output',('/out:'+$renderer))
foreach($ref in @('System.dll','System.Core.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll')){$compile+=('/reference:'+(Join-Path $framework $ref))}
$compile+=$sourceFile
& (Join-Path $framework 'csc.exe') @compile
if($LASTEXITCODE -ne 0){throw 'Documentation renderer compile failed.'}
& $renderer $Executable $OutputDirectory $Dpi
if($LASTEXITCODE -ne 0){throw 'Documentation render failed.'}
Write-Output ('Rendered desktop previews at '+$Dpi+' DPI ('+($Dpi/96.0*100)+'%).')
