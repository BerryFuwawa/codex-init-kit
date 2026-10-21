param([string]$PayloadRoot)
$ErrorActionPreference='Stop'
$testPayloadRoot=$PayloadRoot
. (Join-Path $testPayloadRoot 'Backend.ps1')
$PayloadRoot=$testPayloadRoot
Import-CuaLibrary $PayloadRoot
function Assert220($Condition,[string]$Message){if(-not $Condition){throw $Message}}
$scratch=Join-Path $env:TEMP ('CodexKit-220-tests-'+[guid]::NewGuid().ToString('N'))
$scratch=[IO.Path]::GetFullPath($scratch)
[void][IO.Directory]::CreateDirectory($scratch)
$functions=@{}
foreach($name in @('Get-BrowserStateRoot','Get-BrowserContext','Test-BrowserProxy','Test-BrowserEnvironment','Assert-DesktopStopped','Get-OfficialDesktopPackage','Assert-DesktopReady','Invoke-BoundedProcess','Invoke-DesktopUpdate','Invoke-OfficialCliUpdate','Invoke-InitOperation','Invoke-GuardOperation','Invoke-BrowserOperation','Get-Command')){
    if(Test-Path ('Function:'+ $name)){$functions[$name]=(Get-Item ('Function:'+$name)).ScriptBlock}
}
try {
    $source=Join-Path $scratch 'official';$target=Join-Path $scratch 'runtimes\0123456789abcdef';$stateRoot=Join-Path $scratch 'state'
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
    $original+="`n"
    $launcher=Join-Path $target 'cua-repl.mjs';$sourceLauncher=Join-Path $source 'cua-repl.mjs'
    [IO.File]::WriteAllText($sourceLauncher,$original,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($launcher,$original,[Text.UTF8Encoding]::new($false))
    foreach($folder in @($source,$target)){[IO.File]::WriteAllText((Join-Path $folder 'component.txt'),'official')}
    function Get-BrowserStateRoot {return $stateRoot}
    $context=[pscustomobject]@{Package=[pscustomobject]@{Version='26.1.0.0';PackageFullName='fixture26';InstallLocation=$source};Source=$source;Target=$target;Root=(Split-Path $target);Id='0123456789abcdef';SourceLauncher=$sourceLauncher;Launcher=$launcher}
    function Get-BrowserContext {return $context}
    function Confirm-CodexStopped {}
    function Test-BrowserProxy {return 'fixture TCP reachable'}
    function Test-BrowserEnvironment {return 'fixture env passed'}
    $opts=Convert-BackendOptions @{proxyHost='192.168.50.4'}
    foreach($key in @('browserRepair','officialCliUpdate','desktopUpdate')){Assert220 $opts[$key] ('New option default: '+$key)}
    $originalHash=(Get-FileDigest $launcher).Hash
    $rc=Invoke-BrowserOperation 'repair' $PayloadRoot 10808 $opts
    Assert220 ($rc -eq 0) 'First browser patch failed.'
    $patchedHash=(Get-FileDigest $launcher).Hash
    Assert220 ($patchedHash -ne $originalHash) 'Browser launcher unchanged.'
    $text=[IO.File]::ReadAllText($launcher)
    Assert220 ($text.Contains('http://192.168.50.4:10808') -and $text.Contains('NODE_USE_ENV_PROXY') -and $text.Contains('NODE_REPL_UNTRUSTED_ENV_ALLOWLIST')) 'Proxy settings missing.'
    Assert220 ((Compare-TreeInventory (Get-TreeInventory $source) $target).Passed) 'CUA rejected a validated managed patch.'
    Assert220 ((Invoke-BrowserOperation 'repair' $PayloadRoot 10808 $opts) -eq 0 -and (Get-FileDigest $launcher).Hash -eq $patchedHash) 'Repeated browser patch was not idempotent.'
    # Execute the generated ES module using a local fixture launch implementation.
    $node=(Get-Command node.exe -ErrorAction SilentlyContinue)
    if(-not $node){throw 'Node is required for the environment regression fixture.'}
    $module=Join-Path $target 'node_modules\@oai\cua-repl'
    [void][IO.Directory]::CreateDirectory($module)
    [IO.File]::WriteAllText((Join-Path $module 'package.json'),'{"type":"module","exports":"./index.mjs"}')
    [IO.File]::WriteAllText((Join-Path $module 'index.mjs'),'export async function launch(){ console.log(JSON.stringify({http:process.env.HTTP_PROXY,https:process.env.HTTPS_PROXY,no:process.env.NO_PROXY,flag:process.env.NODE_USE_ENV_PROXY,list:process.env.NODE_REPL_UNTRUSTED_ENV_ALLOWLIST})); }')
    $run=Invoke-BoundedProcess $node.Source @($launcher) 15 @{NODE_REPL_UNTRUSTED_ENV_ALLOWLIST='EXISTING,EXISTING'}
    $envReport=$run.Output.Trim()|ConvertFrom-Json
    Assert220 ($run.ExitCode -eq 0 -and $envReport.http -eq 'http://192.168.50.4:10808' -and $envReport.https -eq $envReport.http -and $envReport.flag -eq '1' -and $envReport.no -eq 'localhost,127.0.0.1,::1') 'Injected variables did not reach launch.'
    $allowed=@($envReport.list.Split(','))
    Assert220 ($allowed.Count -eq 5 -and $allowed[0] -eq 'EXISTING') 'Allowlist was lost or duplicated.'
    Remove-Item -LiteralPath (Join-Path $target 'node_modules') -Recurse -Force
    # Actual subprocess inheritance probe through Node, without executing the live Codex runtime.
    $bounded=$functions['Invoke-BoundedProcess'];$nodeExe=$node.Source
    $slowCode=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes('Write-Output fixture-before-timeout; Start-Sleep -Seconds 30'))
    $watch=[Diagnostics.Stopwatch]::StartNew()
    $timed=& $bounded (Join-Path $env:WINDIR 'System32\WindowsPowerShell\v1.0\powershell.exe') @('-NoProfile','-NonInteractive','-EncodedCommand',$slowCode) 1
    Assert220 ($timed.TimedOut -and $watch.Elapsed.TotalSeconds -lt 15 -and $timed.Output -match 'fixture-before-timeout') 'Bounded process did not terminate and retain output.'
    function Invoke-BoundedProcess {param($FilePath,$Arguments,$TimeoutSeconds,$Environment=@{}) & $bounded $nodeExe $Arguments $TimeoutSeconds $Environment}
    $envProbe=& $functions['Test-BrowserEnvironment'] $context 'http://127.0.0.1:10808'
    Assert220 ($envProbe -match '继承通过') 'Node inheritance probe failed.'
    Set-Item Function:Invoke-BoundedProcess $bounded
    # CUA canonical repair must reapply only an already validated patch.
    [IO.File]::WriteAllText((Join-Path $target 'component.txt'),'broken')
    $repair=Invoke-RuntimeRepair -SourceInventory (Get-TreeInventory $source) -Target $target -RuntimeRoot (Split-Path $target) -RunDirectory $scratch -CopyAction {
        param($from,$to,$copyLog)
        [void][IO.Directory]::CreateDirectory($to)
        Get-ChildItem -LiteralPath $from | Copy-Item -Destination $to -Recurse
    }
    Assert220 ($repair.Changed -and (Get-FileDigest $launcher).Hash -eq $patchedHash -and (Compare-TreeInventory (Get-TreeInventory $source) $target).Passed) 'CUA repair failed to retain the managed patch.'
    Assert220 ((Invoke-BrowserOperation 'repair' $PayloadRoot 0 $opts) -eq 0 -and (Get-FileDigest $launcher).Hash -eq $originalHash) 'Proxy off did not restore direct mode.'
    Assert220 ((Invoke-BrowserOperation 'repair' $PayloadRoot 10808 $opts) -eq 0) 'Reapplying after direct mode failed.'
    Assert220 ((Invoke-BrowserOperation 'rollback' $PayloadRoot 10808 $opts) -eq 0 -and (Get-FileDigest $launcher).Hash -eq $originalHash) 'Exact byte rollback failed.'
    # Rollback must not depend on a working proxy or a successful Node probe.
    Assert220 ((Invoke-BrowserOperation 'repair' $PayloadRoot 10808 $opts) -eq 0) 'Patch preparation before rollback test failed.'
    function Test-BrowserEnvironment {throw 'fixture Node unavailable'}
    Assert220 ((Invoke-BrowserOperation 'rollback' $PayloadRoot 10808 $opts) -eq 0 -and (Get-FileDigest $launcher).Hash -eq $originalHash) 'Rollback incorrectly depended on a Node probe.'
    function Test-BrowserEnvironment {return 'fixture env passed'}
    $backupFile=Get-ChildItem -LiteralPath $stateRoot -Filter '*.bin' | Select-Object -First 1
    $backupBytes=[IO.File]::ReadAllBytes($backupFile.FullName)
    [IO.File]::WriteAllText($backupFile.FullName,'bad backup')
    Assert220 ((Invoke-BrowserOperation 'repair' $PayloadRoot 10808 $opts) -eq 2 -and (Get-FileDigest $launcher).Hash -eq $originalHash) 'Corrupt backup was accepted.'
    [IO.File]::WriteAllBytes($backupFile.FullName,$backupBytes)
    [IO.File]::AppendAllText($launcher,'// third party')
    $thirdHash=(Get-FileDigest $launcher).Hash
    Assert220 ((Invoke-BrowserOperation 'repair' $PayloadRoot 10808 $opts) -eq 2 -and (Get-FileDigest $launcher).Hash -eq $thirdHash) 'Third-party edit was overwritten.'
    [IO.File]::WriteAllText($launcher,$original,[Text.UTF8Encoding]::new($false))
    $recordPath=Join-Path $stateRoot '0123456789abcdef.json'
    $record=[IO.File]::ReadAllText($recordPath)|ConvertFrom-Json;$record.patchedHash='bad';[IO.File]::WriteAllText($recordPath,($record|ConvertTo-Json))
    Assert220 ((Invoke-BrowserOperation 'rollback' $PayloadRoot 10808 $opts) -eq 2 -and (Get-FileDigest $launcher).Hash -eq $originalHash) 'Metadata hash mismatch was accepted.'
    $record.patchedHash=$patchedHash;$record.source='another-version';[IO.File]::WriteAllText($recordPath,($record|ConvertTo-Json))
    Assert220 ((Invoke-BrowserOperation 'repair' $PayloadRoot 10808 $opts) -eq 2) 'Different app version was accepted.'
    $rejected=$false;try{New-BrowserLauncherBytes ([Text.Encoding]::UTF8.GetBytes($original+'// new structure')) 'http://127.0.0.1:10808'}catch{$rejected=$true}
    Assert220 $rejected 'Unknown launcher structure was accepted.'
    $before=[pscustomobject]@{Version='1.0.0.0';InstallLocation='fixture';Status='Ok'}
    $after=[pscustomobject]@{Version='2.0.0.0';InstallLocation='fixture';Status='Ok'}
    function Assert-DesktopStopped {return $before}
    function Test-DesktopCli { $script:cliProbed=$true }
    function Get-OfficialDesktopPackage {return $after}
    function Assert-DesktopReady {param($Package) if($Package.Status -ne 'Ok'){throw 'pending desktop'}}
    function Get-Command {param($Name,$ErrorAction) if($Name -eq 'winget.exe'){if($wingetMissing){return $null};return [pscustomobject]@{Source='fixture-winget'}};Microsoft.PowerShell.Core\Get-Command $Name}
    $wingetMissing=$false;$exit=0;$timeout=$false;$capturedArguments=$null
    function Invoke-BoundedProcess {param($FilePath,$Arguments,$TimeoutSeconds) $script:capturedArguments=$Arguments;Assert220 ($TimeoutSeconds -eq 1200) 'Desktop timeout changed.';return [pscustomobject]@{ExitCode=$exit;TimedOut=$timeout;Output='fixture';Error=''}}
    Assert220 ((Invoke-DesktopUpdate $PayloadRoot) -eq 'updated') 'Desktop version change not detected.'
    Assert220 $script:cliProbed 'Updated native CLI was not probed.'
    Assert220 (($script:capturedArguments -join ' ') -eq 'upgrade --id 9PLM9XGG6VKS --exact --source msstore --silent --accept-source-agreements --accept-package-agreements --disable-interactivity') 'Unsafe winget arguments.'
    $after.Version=$before.Version;$exit=-1978335189 # APPINSTALLER_CLI_ERROR_UPDATE_NOT_APPLICABLE, 0x8A15002B
    Assert220 ((Invoke-DesktopUpdate $PayloadRoot) -eq 'latest') 'Already latest not recognized.'
    foreach($scenario in @('missing','network','timeout','restart','unchanged','pending')){
        $wingetMissing=$scenario -eq 'missing';$timeout=$scenario -eq 'timeout';$after.Status=if($scenario -eq 'pending'){'DeploymentInProgress'}else{'Ok'}
        $exit=if($scenario -eq 'network'){-1}elseif($scenario -eq 'restart'){3010}else{0}
        $rejected=$false;try{Invoke-DesktopUpdate $PayloadRoot}catch{$rejected=$true}
        Assert220 $rejected ('Desktop failure swallowed: '+$scenario)
    }
    # Independent update requires neither guard installation nor mode selection.
    $realGuardInitializer=(Get-Item Function:Initialize-GuardRuntime).ScriptBlock
    function Initialize-GuardRuntime {}
    function Get-ManagedPaths {return [pscustomobject]@{Root=(Join-Path $scratch 'standalone')}}
    function Test-CanonicalPackage {return [pscustomobject]@{Healthy=$true;Version='fixture-cli';Problems=@()}}
    $installerCalled=$false;$installerSucceeds=$true
    function Invoke-OfficialInstaller {$script:installerCalled=$true;return $installerSucceeds}
    function Ensure-AutoStart {throw 'Standalone update attempted to install a login task.'}
    function Set-ActiveCli {throw 'Standalone update attempted to switch runtime mode.'}
    $priorCli=[Environment]::GetEnvironmentVariable('CODEX_CLI_PATH','User')
    Assert220 ((Invoke-OfficialCliUpdate $PayloadRoot) -eq 0 -and $script:installerCalled -and [Environment]::GetEnvironmentVariable('CODEX_CLI_PATH','User') -eq $priorCli) 'Independent CLI update changed mode or required guard.'
    $script:installerCalled=$false
    Assert220 ((Invoke-OfficialCliUpdate $PayloadRoot -OnlyIfMissing) -eq 0 -and -not $script:installerCalled) 'Healthy switch target unnecessarily reinstalled.'
    $installerSucceeds=$false;$rejected=$false;try{Invoke-OfficialCliUpdate $PayloadRoot}catch{$rejected=$true}
    Assert220 $rejected 'Independent installer failure was swallowed.'
    Set-Item Function:Initialize-GuardRuntime $realGuardInitializer
    # Initialization orchestration uses only fixture actions; no installs, process stops or live updates.
    $calls=[Collections.Generic.List[string]]::new();$desktopFails=$false
    function Assert-DesktopStopped {$calls.Add('preflight');return $before}
    function Invoke-DesktopUpdate {$calls.Add('desktop');if($desktopFails){throw 'network failed'}}
    function Assert-DesktopReady {}
    function Invoke-InitOperation {param($Action,$PayloadRoot,$Drive,$Port,$Options) $calls.Add('base');return 0}
    function Invoke-OfficialCliUpdate {$calls.Add('official');return 0}
    function Invoke-GuardOperation {$calls.Add('guard');return 0}
    function Invoke-BrowserOperation {$calls.Add('browser');return 0}
    $inputData=[pscustomobject]@{PayloadRoot=$PayloadRoot;Drive='D';ProxyPort=10808;Options=(Convert-BackendOptions $null);InstallProtection=$true}
    Assert220 ((Invoke-Initialize220 $inputData) -eq 0 -and ($calls -join ',') -eq 'preflight,desktop,base,official,guard,browser') 'Initialization step order changed.'
    $calls.Clear();$desktopFails=$true;$after.Status='Ok'
    Assert220 ((Invoke-Initialize220 $inputData) -eq 2 -and ($calls -join ',') -eq 'preflight,desktop,base,official,guard,browser') 'Failed optional update did not continue as partial.'
    $calls.Clear();function Invoke-DesktopUpdate {$calls.Add('desktop');throw 'restart pending'}
    Assert220 ((Invoke-Initialize220 $inputData) -eq 2 -and ($calls -join ',') -eq 'preflight,desktop,base,official') 'Pending desktop did not pause dependent repairs.'
    $calls.Clear();$inputData.Options.desktopUpdate=$false;$inputData.Options.officialCliUpdate=$false;$inputData.Options.browserRepair=$false;$inputData.InstallProtection=$false
    Assert220 ((Invoke-Initialize220 $inputData) -eq 0 -and ($calls -join ',') -eq 'preflight,base') 'Unchecked optional steps still executed.'
    Write-Output 'PASS: browser variables, allowlist, inheritance, idempotency, proxy off, exact rollback, unknown structure, hashes, app identity, CUA compatibility, winget results, initialization order and partial continuation. No live updates performed.'
} finally {
    foreach($name in $functions.Keys){Set-Item ('Function:'+$name) $functions[$name]}
    if(Test-Path -LiteralPath $scratch){Remove-Item -LiteralPath $scratch -Recurse -Force}
}
