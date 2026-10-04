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
                {"processRunning",false},{"backupPath","初始化完成后，可在此查看备份位置"}});
            var pages=new[]{"概览","维护恢复"};
            var files=new[]{"overview","maintenance"};
            for(int i=0;i<pages.Length;i++){navigate.Invoke(window,new object[]{pages[i]});Save(window,directory,files[i],i==0?1000:900,dpi);}
            navigate.Invoke(window,new object[]{"初始化"});Save(window,directory,"settings",1400,dpi);
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
