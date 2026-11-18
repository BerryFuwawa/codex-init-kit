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
foreach($name in @('Get-BrowserStateRoot','Get-BrowserContext','Test-BrowserProxy','Test-BrowserEnvironment','Assert-DesktopStopped','Get-OfficialDesktopPackage','Get-ReadOnlyDesktopInfo','Get-ReadOnlyRuntimeModeInfo','Assert-DesktopReady','Invoke-BoundedProcess','Invoke-DesktopUpdate','Invoke-OfficialCliUpdate','Invoke-InitOperation','Invoke-GuardOperation','Invoke-BrowserOperation','Get-InitializationCliSnapshot','Restore-InitializationCliSnapshot','Set-InitializationCliMode','Assert-InitializationCliMode','Get-Command','Stop-InitializationApps','Write-BackendLine')){
    if(Test-Path ('Function:'+ $name)){$functions[$name]=(Get-Item ('Function:'+$name)).ScriptBlock}
}
try {
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
        $exit=if($scenario -eq 'network'){-2147012867}elseif($scenario -eq 'restart'){3010}else{0}
        $rejected=$false;try{Invoke-DesktopUpdate $PayloadRoot}catch{$rejected=$true;if($scenario -eq 'network'){Assert220 ($_.Exception.Message.Contains('0x80072EFD') -and $_.Exception.Message.Contains('手动更新')) 'Store network failure lost its explanation.'}}
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
    # Snapshot and restore use an isolated registry key, never the live Codex values.
    $fixtureKey='Software\CodexInitKit-Test-'+[guid]::NewGuid().ToString('N')
    $fixtureStatePath=Join-Path $scratch 'guard-state.json'
    $key=[Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($fixtureKey)
    try{$key.SetValue('CODEX_CLI_PATH','%USERPROFILE%\old-codex.exe',[Microsoft.Win32.RegistryValueKind]::ExpandString)}finally{$key.Dispose()}
    [IO.File]::WriteAllText($fixtureStatePath,'{"ActiveMode":"OfficialCurrent","ActiveCliPath":"old-cli","LastModeReason":"old"}')
    $snapshot=Get-InitializationCliSnapshot -EnvironmentKey $fixtureKey -StatePath $fixtureStatePath
    $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($fixtureKey,$true)
    try{$key.SetValue('CODEX_CLI_PATH','changed');$key.SetValue('CODEX_CODE_MODE_HOST_PATH','changed-host')}finally{$key.Dispose()}
    $script:fixtureGuardState=@{Installed=$true;ActiveMode='DesktopNativeProtected';ActiveCliPath='new-cli';LastModeReason='new'}
    function Load-State {return $script:fixtureGuardState}
    function Save-State {param($State) $script:fixtureGuardState=$State}
    function Broadcast-Environment {}
    Restore-InitializationCliSnapshot $snapshot $PayloadRoot
    $key=[Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($fixtureKey,$false)
    try{
        Assert220 ($key.GetValue('CODEX_CLI_PATH',$null,[Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames) -eq '%USERPROFILE%\old-codex.exe' -and $key.GetValueKind('CODEX_CLI_PATH') -eq [Microsoft.Win32.RegistryValueKind]::ExpandString -and $null -eq $key.GetValue('CODEX_CODE_MODE_HOST_PATH')) 'CLI registry snapshot did not restore its value, type or absence.'
    }finally{$key.Dispose();[Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree($fixtureKey)}
    Assert220 ($script:fixtureGuardState.Installed -and $script:fixtureGuardState.ActiveMode -eq 'OfficialCurrent' -and $script:fixtureGuardState.ActiveCliPath -eq 'old-cli') 'Restoration lost guard installation or prior mode state.'
    # The real standalone switch prepares components only if missing and never installs a login task.
    $script:fixtureGuardState=@{Installed=$false;ActiveMode='DesktopNativeProtected'};$script:preparedOnlyIfMissing=$false
    function Test-DesktopRunning {return $false}
    function Switch-ToOfficialCurrent {$script:fixtureGuardState.ActiveMode='OfficialCurrent'}
    function Invoke-OfficialCliUpdate {param($PayloadRoot,[switch]$OnlyIfMissing) $script:preparedOnlyIfMissing=$OnlyIfMissing.IsPresent;return 0}
    Assert220 ((Invoke-GuardOperation 'guard-current' $PayloadRoot) -eq 0 -and $script:preparedOnlyIfMissing -and $script:fixtureGuardState.ActiveMode -eq 'OfficialCurrent' -and -not $script:fixtureGuardState.Installed) 'Standalone switch did not prepare the component independently of protection.'
    # Existing automatic maintenance must respect an explicitly selected standalone CLI.
    $guardText=Get-GuardPayloadText (Join-Path $PayloadRoot 'guard.cmd')
    $desktopGuard=Convert-DesktopGuardRuntime $guardText
    Assert220 ((Convert-DesktopGuardRuntime $desktopGuard) -eq $desktopGuard) 'Desktop guard adaptation is not idempotent.'
    $rejected=$false;try{Convert-DesktopGuardRuntime 'unknown guard structure'}catch{$rejected=$true};Assert220 $rejected 'Unknown guard structure was overwritten.'
    $script:AutoScript=Join-Path $scratch 'guard-auto.ps1';[IO.File]::WriteAllText($script:AutoScript,$guardText,[Text.UTF8Encoding]::new($true))
    $script:fixtureGuardState=@{Installed=$true;ActiveMode='OfficialCurrent';ActiveCliPath='fixture-cli'}
    Update-DesktopGuardAutoScript
    Assert220 ([IO.File]::ReadAllText($script:AutoScript).Contains('# CODEX INIT KIT: preserve explicitly selected standalone')) 'Existing automatic-maintenance script was not updated.'
    $tokens=$null;$errors=$null;$guardAst=[Management.Automation.Language.Parser]::ParseInput($desktopGuard,[ref]$tokens,[ref]$errors)
    Assert220 ($errors.Count -eq 0) 'Adapted guard runtime did not parse.'
    foreach($name in @('Apply-SafeRuntime','Auto-Maintain')){
        $definition=@($guardAst.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst]},$false)|Where-Object Name -eq $name)[0]
        Set-Item ('Function:'+ $name) $definition.Body.GetScriptBlock()
    }
    $script:activeFixture='fixture-cli';$script:appliedMode='';$script:fixtureManagedHealthy=$true
    function Get-UserEnv {return $script:activeFixture}
    function Get-DesktopInfo {return [pscustomobject]@{Found=$true;BundleHealthy=$true}}
    function Test-CanonicalPackage {return [pscustomobject]@{Healthy=$script:fixtureManagedHealthy;Cli='fixture-cli';Version='1.0';Problems=@()}}
    function Set-ActiveCli {param($CliPath,$State,$Mode) $script:appliedMode=$Mode;$State['ActiveMode']=$Mode;$State['ActiveCliPath']=$CliPath}
    function Set-DesktopNativeMode {param($State,$Reason) $script:appliedMode='native';$State['ActiveMode']='DesktopNativeProtected';return 'DesktopNativeProtected'}
    function Update-ProtectedRuntime {}
    function Write-GuardLog {}
    Auto-Maintain
    Assert220 ($script:appliedMode -eq 'OfficialCurrent') 'Login maintenance changed explicitly selected standalone CLI.'
    $script:appliedMode='';$candidate=[pscustomobject]@{Healthy=$true;Cli='fixture-cli'}
    Assert220 ((Apply-SafeRuntime $script:fixtureGuardState $candidate 'OfficialCurrentFallback') -eq 'OfficialCurrent' -and $script:appliedMode -eq 'OfficialCurrent') 'Guard update changed explicitly selected standalone CLI.'
    $script:fixtureGuardState.ActiveMode='DesktopNativeProtected';$script:appliedMode='';Auto-Maintain
    Assert220 ($script:appliedMode -eq 'native') 'Native selection was not retained by login maintenance.'
    $script:fixtureGuardState.ActiveMode='OfficialCurrent';$script:fixtureManagedHealthy=$false;$script:appliedMode='';Auto-Maintain
    Assert220 ($script:appliedMode -eq 'native') 'Missing standalone component did not fall back to healthy native CLI.'
    Set-Item Function:Initialize-GuardRuntime $realGuardInitializer
    # Initialization orchestration uses only fixture actions; no installs, process stops or live updates.
    $calls=[Collections.Generic.List[string]]::new();$desktopFails=$false;$switchFails=$false;$finalFails=$false;$baseFails=$false;$script:fixtureMode='DesktopNative'
    function Stop-InitializationApps {$calls.Add('preflight')}
    function Assert-DesktopStopped {return $before}
    function Get-ReadOnlyDesktopInfo {return [pscustomobject]@{InstallLocation='fixture-desktop'}}
    function Get-ReadOnlyRuntimeModeInfo {$calls.Add('detect');return [pscustomobject]@{Mode=$script:fixtureMode;Detail='fixture unrecognized'}}
    function Invoke-DesktopUpdate {$calls.Add('desktop');if($desktopFails){throw 'network failed'}}
    function Assert-DesktopReady {}
    function Invoke-InitOperation {param($Action,$PayloadRoot,$Drive,$Port,$Options) $calls.Add('base');if($baseFails){throw 'base failed'};return 0}
    function Invoke-OfficialCliUpdate {$calls.Add('official');return 0}
    function Invoke-GuardOperation {$calls.Add('guard');return 0}
    function Invoke-BrowserOperation {$calls.Add('browser');return 0}
    function Get-InitializationCliSnapshot {$calls.Add('snapshot');return @{fixture=$true}}
    function Restore-InitializationCliSnapshot {$calls.Add('restore')}
    function Set-InitializationCliMode {param($Mode,$PayloadRoot) $calls.Add('cli:'+ $Mode);if($switchFails){throw 'fixture CLI unavailable'};return $Mode}
    function Assert-InitializationCliMode {param($Mode) $calls.Add('verify:'+ $Mode);if($finalFails){throw 'fixture mode changed'};return $Mode}
    $inputData=[pscustomobject]@{PayloadRoot=$PayloadRoot;Drive='D';ProxyPort=10808;Options=(Convert-BackendOptions @{});InstallProtection=$true}
    Assert220 ((Invoke-Initialize220 $inputData) -eq 0 -and ($calls -join ',') -eq 'preflight,detect,snapshot,desktop,base,official,guard,cli:desktop-native,verify:desktop-native') 'Detected native CLI was not applied and verified after guard.'
    $calls.Clear();$script:fixtureMode='OfficialStandalone'
    Assert220 ((Invoke-Initialize220 $inputData) -eq 0 -and ($calls -join ',') -eq 'preflight,detect,snapshot,desktop,base,official,guard,cli:official-standalone,verify:official-standalone') 'Detected standalone CLI was not retained after guard.'
    $calls.Clear();$script:fixtureMode='Unknown';$rejected=$false
    try{[void](Invoke-Initialize220 $inputData)}catch{$rejected=$_.Exception.Message.Contains('无法识别初始化前的 CLI 模式')}
    Assert220 ($rejected -and ($calls -join ',') -eq 'preflight,detect') 'Unknown CLI mode did not stop before initialization changes.'
    $script:fixtureMode='DesktopNative'
    $calls.Clear();$desktopFails=$true;$after.Status='Ok'
    Assert220 ((Invoke-Initialize220 $inputData) -eq 2 -and ($calls -join ',') -eq 'preflight,detect,snapshot,desktop,base,official,guard,cli:desktop-native,verify:desktop-native') 'Failed optional update did not continue as partial.'
    $calls.Clear();function Invoke-DesktopUpdate {$calls.Add('desktop');throw (New-DesktopUnsafeError 'restart pending')}
    Assert220 ((Invoke-Initialize220 $inputData) -eq 2 -and ($calls -join ',') -eq 'preflight,detect,snapshot,desktop,base,official,restore') 'Pending desktop did not pause dependent repairs and restore CLI.'
    $calls.Clear();$inputData.Options.desktopUpdate=$false;$inputData.Options.officialCliUpdate=$false;$inputData.Options.browserRepair=$false;$inputData.Options.cliMode='desktop-native';$inputData.InstallProtection=$false
    Assert220 ((Invoke-Initialize220 $inputData) -eq 0 -and ($calls -join ',') -eq 'preflight,snapshot,base,cli:desktop-native,verify:desktop-native') 'Native selection required unchecked optional steps.'
    $calls.Clear();$inputData.Options.cliMode='official-standalone'
    Assert220 ((Invoke-Initialize220 $inputData) -eq 0 -and ($calls -join ',') -eq 'preflight,snapshot,base,cli:official-standalone,verify:official-standalone') 'Standalone selection required an optional update or guard.'
    $calls.Clear();$switchFails=$true;$inputData.Options.browserRepair=$true
    Assert220 ((Invoke-Initialize220 $inputData) -eq 2 -and ($calls -join ',') -eq 'preflight,snapshot,base,cli:official-standalone,restore') 'Failed CLI selection did not restore the snapshot or continue independent work.'
    $calls.Clear();$switchFails=$false;$finalFails=$true
    Assert220 ((Invoke-Initialize220 $inputData) -eq 2 -and ($calls -join ',') -eq 'preflight,snapshot,base,cli:official-standalone,verify:official-standalone,restore') 'Changed final CLI mode was not reported and restored.'
    $calls.Clear();$finalFails=$false;$baseFails=$true
    Assert220 ((Invoke-Initialize220 $inputData) -eq 1 -and ($calls -join ',') -eq 'preflight,snapshot,base,restore') 'Failed base initialization did not restore CLI or report failure.'

} finally {
    foreach($name in $functions.Keys){Set-Item ('Function:'+$name) $functions[$name]}
    if(Test-Path -LiteralPath $scratch){Remove-Item -LiteralPath $scratch -Recurse -Force}
}
