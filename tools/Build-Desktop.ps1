[CmdletBinding()]
param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if (-not $OutputDirectory) { $OutputDirectory=Join-Path $root 'build\desktop' }
[IO.Directory]::CreateDirectory($OutputDirectory) | Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler=Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x C# compiler missing.' }
$scriptRoot=Join-Path $root 'scripts'
$init=Join-Path $scriptRoot '第1步-Codex初始化管理-V4.7.0-GPT5.6-LUNA-MAX.cmd'
$guard=Join-Path $scriptRoot '第2步-Codex启动保护与更新管理器-V5.1.2-GPT5.6-LUNA-MAX.cmd'
$out=Join-Path $OutputDirectory 'CodexInitKit.exe'
$argsList=@('/nologo','/target:winexe','/platform:x64','/optimize+','/utf8output',"/out:$out",('/win32manifest:'+(Join-Path $root 'app\app.manifest')))
$argsList+=('/win32icon:'+(Join-Path $root 'assets\app.ico'))
$argsList+=('/resource:'+(Join-Path $root 'assets\logo.png')+',Brand.logo.png')
$argsList+=('/resource:'+(Join-Path $root 'app\Controls.xaml')+',UI.Controls.xaml')
foreach($ref in @('System.dll','System.Core.dll','System.Web.Extensions.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll')) { $argsList+="/reference:$(Join-Path $framework $ref)" }
$argsList+="/resource:$init,Payload.init.cmd"
$argsList+="/resource:$guard,Payload.guard.cmd"
$argsList+="/resource:$(Join-Path $scriptRoot 'Codex_CUA_Repair.cmd'),Payload.cua.cmd"
$argsList+="/resource:$(Join-Path $root 'app\Backend.ps1'),Payload.Backend.ps1"
$argsList+="/resource:$(Join-Path $root 'tests\DesktopBackend.Tests.ps1'),Payload.Backend.Tests.ps1"
$argsList+=@(Get-ChildItem -LiteralPath (Join-Path $root 'app') -Filter '*.cs' | ForEach-Object FullName)
& $compiler @argsList
if($LASTEXITCODE -ne 0) { throw "Compile failed: $LASTEXITCODE" }
Write-Output $out
