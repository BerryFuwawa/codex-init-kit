param([string]$PayloadRoot)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$backendPath=if($PayloadRoot){Join-Path $PayloadRoot 'Backend.ps1'}else{Join-Path $repo 'app\Backend.ps1'}
$startupPath=if($PayloadRoot){Join-Path $PayloadRoot 'BrowserStartup.ps1'}else{Join-Path $repo 'scripts\BrowserStartup.ps1'}
$cuaPath=if($PayloadRoot){Join-Path $PayloadRoot 'cua.cmd'}else{Join-Path $repo 'scripts\Codex_CUA_Repair.cmd'}
. $backendPath
. $startupPath
$scratch=Join-Path ([IO.Path]::GetTempPath()) ('CodexKit-startup-test-'+[guid]::NewGuid().ToString('N'))
$scratch=[IO.Path]::GetFullPath($scratch)
[void][IO.Directory]::CreateDirectory($scratch)
try {
    Copy-Item -LiteralPath $cuaPath -Destination (Join-Path $scratch 'cua.cmd')
    Copy-Item -LiteralPath $startupPath -Destination (Join-Path $scratch 'BrowserStartup.ps1')
    Import-CuaLibrary $scratch
    function Assert($Value,$Message){if(-not $Value){throw $Message}}
    $apps=@([pscustomobject]@{Id='App';Executable='app/ChatGPT.exe';EntryPoint='Windows.FullTrustApplication'},[pscustomobject]@{Id='Runner';Executable='app/resources/codex-command-runner.exe';EntryPoint='Windows.FullTrustApplication'})
    Assert ((Get-BrowserAppId $apps) -eq 'App') 'ChatGPT app not selected'
    $apps[0].Executable='app\Codex.exe';Assert ((Get-BrowserAppId $apps) -eq 'App') 'Codex backslash variant failed'
    foreach($bad in @(@($apps[1]),@($apps[0],$apps[0]))){$threw=$false;try{Get-BrowserAppId $bad}catch{$threw=$true};Assert $threw 'Unknown/ambiguous app accepted'}
    $source=Join-Path $scratch 'source';$target=Join-Path $scratch 'runtime\0123456789abcdef';$stateRoot=Join-Path $scratch 'state'
    [void][IO.Directory]::CreateDirectory($source);[void][IO.Directory]::CreateDirectory($target)
    $original=@'
#!/usr/bin/env node
import * as cua_repl from "@oai/cua-repl";

try {
  await cua_repl.launch();
} catch (error) {
  const error_message = error instanceof Error ? error.message : String(error);
  console.error(`cua_repl could not start: ${error_message}`);
  process.exitCode = 1;
}
'@
    $bytes=[Text.Encoding]::UTF8.GetBytes($original+"`n")
    $sourceLauncher=Join-Path $source 'cua-repl.mjs';$launcher=Join-Path $target 'cua-repl.mjs'
    [IO.File]::WriteAllBytes($sourceLauncher,$bytes);[IO.File]::WriteAllBytes($launcher,$bytes)
    [IO.File]::WriteAllText((Join-Path $source 'component'),'official');[IO.File]::WriteAllText((Join-Path $target 'component'),'official')
    $context=[pscustomobject]@{Source=$source;Target=$target;Id='0123456789abcdef';SourceLauncher=$sourceLauncher;Launcher=$launcher;Root=(Split-Path $target);Package=[pscustomobject]@{Version='26.1';PackageFullName='OpenAI.Codex_26.1.0.0_x64__2p2nqsd0c76g0';PackageFamilyName='OpenAI.Codex_2p2nqsd0c76g0';InstallLocation=$source}}
    function Get-BrowserStateRoot {return $stateRoot}
    function Get-OfficialDesktopPackage {return $context.Package}
    $running=$false
    function Confirm-CodexStopped {if($running){throw 'running'}}
    $nodeUnavailable=$false
    function Test-BrowserEnvironment {if($nodeUnavailable){throw 'fixture Node lacks environment proxy support'};return 'fixture environment verified'}
    function Test-BrowserProxy {return 'fixture proxy reachable'}
    $proxy='http://127.0.0.1:10808'
    Assert (-not (Import-LegacyBrowserPatch $context $proxy -CheckOnly)) 'Original reported legacy'
    [IO.File]::WriteAllBytes($launcher,(Get-LegacyBrowserBytes $bytes $proxy 'startup-v1'))
    $legacyHash=(Get-FileDigest $launcher).Hash
    Assert (Import-LegacyBrowserPatch $context $proxy -CheckOnly) 'Legacy not recognized'
    Assert (-not(Test-Path -LiteralPath $stateRoot)) 'Check wrote state'
    $running=$true;$threw=$false;try{Import-LegacyBrowserPatch $context $proxy}catch{$threw=$true};Assert $threw 'Active migration accepted';$running=$false
    $nodeUnavailable=$true;$threw=$false;try{Import-LegacyBrowserPatch $context $proxy}catch{$threw=$true};Assert ($threw -and (Get-FileDigest $launcher).Hash -eq $legacyHash -and -not(Test-Path -LiteralPath $stateRoot)) 'Unsupported Node allowed legacy migration';$nodeUnavailable=$false
    [IO.File]::WriteAllText((Join-Path $target 'component'),'tampered')
    $threw=$false;try{Import-LegacyBrowserPatch $context $proxy}catch{$threw=$true};Assert $threw 'Tampered component accepted'
    [IO.File]::WriteAllText((Join-Path $target 'component'),'official')
    Assert (Import-LegacyBrowserPatch $context $proxy) 'Migration failed'
    Assert ((Get-BrowserManagedPatch $source $target).Record.proxy -eq $proxy) 'Managed metadata invalid'
    Assert (Test-Path -LiteralPath (Join-Path $stateRoot ('0123456789abcdef-'+$legacyHash+'.legacy.bin'))) 'Legacy original not backed up'
    Assert (-not (Import-LegacyBrowserPatch $context $proxy)) 'Repeated migration changed files'
    Assert ((Compare-TreeInventory (Get-TreeInventory $source) $target).Passed) 'Migrated patch incompatible with CUA'
    # Startup orchestration: fixture actions only, no real app launch or update.
    [IO.File]::WriteAllText((Join-Path $source 'AppxManifest.xml'),'<Package><Applications><Application Id="App" Executable="app/ChatGPT.exe" EntryPoint="Windows.FullTrustApplication" /></Applications></Package>')
    Write-BrowserAtomic $launcher $bytes
    function Get-BrowserContext {return $context}
    function Invoke-BrowserOperation { param($Mode,$PayloadRoot,$Port,$Options) $script:repaired=$true;$script:lastPort=$Port;$script:lastHost=$Options.proxyHost;return $repairCode }
    function Start-Process {$script:launched=$true}
    $repairCode=0;$script:repaired=$false;$script:launched=$false
    Assert ((Invoke-BrowserStartup $scratch -PrepareOnly) -eq 0 -and $script:repaired -and -not $script:launched) 'PrepareOnly launched app'
    $script:repaired=$false;$script:launched=$false
    Assert ((Invoke-BrowserStartup $scratch -Port 0 -AllowDirect -PrepareOnly) -eq 0 -and $script:repaired -and $script:lastPort -eq 0 -and -not $script:launched) 'Proxy-off prepare did not use the repair path'
    $threw=$false;try{Invoke-BrowserStartup $scratch -Port 0 -PrepareOnly}catch{$threw=$true};Assert $threw 'Standalone direct mode changed unexpectedly'
    $repairCode=2;$script:launched=$false
    Assert ((Invoke-BrowserStartup $scratch) -eq 2 -and -not $script:launched) 'Failed repair launched app'
    $repairCode=0
    Assert ((Invoke-BrowserStartup $scratch) -eq 0 -and $script:launched) 'Successful repair did not launch app'
    Copy-Item -LiteralPath (Join-Path $source 'AppxManifest.xml') -Destination $target
    Write-BrowserAtomic $launcher (New-BrowserLauncherBytes $bytes $proxy)
    $beforeReady=(Get-Item -LiteralPath $launcher).LastWriteTimeUtc
    $script:repaired=$false
    Assert ((Invoke-BrowserStartup $scratch -PrepareOnly) -eq 0 -and -not $script:repaired -and (Get-Item -LiteralPath $launcher).LastWriteTimeUtc -eq $beforeReady) 'Ready entry unnecessarily rewritten'
    $nodeUnavailable=$true;$threw=$false;try{Invoke-BrowserStartup $scratch -PrepareOnly}catch{$threw=$true};Assert $threw 'Ready patch bypassed Node capability check';$nodeUnavailable=$false
    $script:launched=$false;$originalGetPackage=(Get-Item Function:Get-OfficialDesktopPackage).ScriptBlock
    function Get-OfficialDesktopPackage {return [pscustomobject]@{PackageFullName='different';PackageFamilyName=$context.Package.PackageFamilyName;InstallLocation=$source}}
    $threw=$false;try{Invoke-BrowserStartup $scratch -PrepareOnly}catch{$threw=$true};Assert ($threw -and -not $script:launched) 'App version race was not blocked'
    Set-Item Function:Get-OfficialDesktopPackage $originalGetPackage
    # GUI worker must return a validated target without starting the app itself.
    $originalWrite=(Get-Item Function:Write-BackendLine).ScriptBlock
    $script:records=[Collections.Generic.List[string]]::new()
    function Write-BackendLine {param($Message) [void]$script:records.Add([string]$Message)}
    function Assert-DesktopReady {}
    $script:launched=$false;$running=$false
    $workerRc=Invoke-BrowserStartWorker $scratch 10808 @{proxyHost='127.0.0.1'}
    Assert ($workerRc -eq 0 -and -not $script:launched -and @($script:records | Where-Object {$_ -like '@@BROWSER_START@@*'}).Count -eq 1) 'Prepared GUI worker did not return a launch target safely'
    $script:records.Clear();$verifyRc=Invoke-BrowserStartWorker $scratch 0 @{proxyHost='127.0.0.1'} -VerifyOnly
    Assert ($verifyRc -eq 0 -and @($script:records | Where-Object {$_ -like '@@BROWSER_START@@*'}).Count -eq 1) 'GUI launch verification did not return target'
    $script:records.Clear();$running=$true;$threw=$false;try{Invoke-BrowserStartWorker $scratch 10808 @{proxyHost='127.0.0.1'}}catch{$threw=$true};$running=$false
    Assert ($threw -and -not $script:launched -and @($script:records | Where-Object {$_ -like '@@BROWSER_START@@*'}).Count -eq 0) 'Running Codex did not block GUI preparation'
    Set-Item Function:Write-BackendLine $originalWrite
    Write-Output 'PASS: app IDs, legacy migration, check-only, process guard, inventory tampering, backups, CUA compatibility, prepare-only and launch gating.'
} finally {
    $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if([IO.Path]::GetFullPath($scratch).StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $scratch -Recurse -Force}
}
