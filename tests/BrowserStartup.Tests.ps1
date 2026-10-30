$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
. (Join-Path $repo 'app\Backend.ps1')
. (Join-Path $repo 'scripts\BrowserStartup.ps1')
$scratch=Join-Path ([IO.Path]::GetTempPath()) ('CodexKit-startup-test-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
try {
    Copy-Item -LiteralPath (Join-Path $repo 'scripts\Codex_CUA_Repair.cmd') -Destination (Join-Path $scratch 'cua.cmd')
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
    $context=[pscustomobject]@{Source=$source;Target=$target;Id='0123456789abcdef';SourceLauncher=$sourceLauncher;Launcher=$launcher;Root=(Split-Path $target);Package=[pscustomobject]@{Version='26.1';PackageFullName='fixture';PackageFamilyName='fixture';InstallLocation=$source}}
    function Get-BrowserStateRoot {return $stateRoot}
    function Get-OfficialDesktopPackage {return $context.Package}
    $running=$false
    function Confirm-CodexStopped {if($running){throw 'running'}}
    $proxy='http://127.0.0.1:10808'
    Assert (-not (Import-LegacyBrowserPatch $context $proxy -CheckOnly)) 'Original reported legacy'
    [IO.File]::WriteAllBytes($launcher,(Get-LegacyBrowserBytes $bytes $proxy 'startup-v1'))
    $legacyHash=(Get-FileDigest $launcher).Hash
    Assert (Import-LegacyBrowserPatch $context $proxy -CheckOnly) 'Legacy not recognized'
    Assert (-not(Test-Path -LiteralPath $stateRoot)) 'Check wrote state'
    $running=$true;$threw=$false;try{Import-LegacyBrowserPatch $context $proxy}catch{$threw=$true};Assert $threw 'Active migration accepted';$running=$false
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
    function Invoke-BrowserOperation { $script:repaired=$true;return $repairCode }
    function Start-Process {$script:launched=$true}
    $repairCode=0;$script:repaired=$false;$script:launched=$false
    Assert ((Invoke-BrowserStartup $scratch -PrepareOnly) -eq 0 -and $script:repaired -and -not $script:launched) 'PrepareOnly launched app'
    $repairCode=2;$script:launched=$false
    Assert ((Invoke-BrowserStartup $scratch) -eq 2 -and -not $script:launched) 'Failed repair launched app'
    $repairCode=0
    Assert ((Invoke-BrowserStartup $scratch) -eq 0 -and $script:launched) 'Successful repair did not launch app'
    Copy-Item -LiteralPath (Join-Path $source 'AppxManifest.xml') -Destination $target
    Write-BrowserAtomic $launcher (New-BrowserLauncherBytes $bytes $proxy)
    $beforeReady=(Get-Item -LiteralPath $launcher).LastWriteTimeUtc
    $script:repaired=$false
    Assert ((Invoke-BrowserStartup $scratch -PrepareOnly) -eq 0 -and -not $script:repaired -and (Get-Item -LiteralPath $launcher).LastWriteTimeUtc -eq $beforeReady) 'Ready entry unnecessarily rewritten'
    Write-Output 'PASS: app IDs, legacy migration, check-only, process guard, inventory tampering, backups, CUA compatibility, prepare-only and launch gating.'
} finally {
    $tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if([IO.Path]::GetFullPath($scratch).StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase)){Remove-Item -LiteralPath $scratch -Recurse -Force}
}
