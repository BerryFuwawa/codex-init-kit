# Codex Desktop Backend adapter regression tests. These tests never invoke a
# mutating init, CUA repair, Guard update, or process termination operation.
param(
    [string]$PayloadRoot,
    [string]$BackendPath
)
# Backend.ps1 has matching parameter names. Preserve the test harness values
# before dot-sourcing because its parameter binding would otherwise overwrite
# them in this scope.
$testPayloadRoot = $PayloadRoot
$testBackendPath = $BackendPath
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object Text.UTF8Encoding($false)
$OutputEncoding = [Console]::OutputEncoding
$project = Split-Path -Parent $PSScriptRoot
$backend = if ([string]::IsNullOrWhiteSpace($testBackendPath)) {
    Join-Path $project 'app\Backend.ps1'
} else {
    [IO.Path]::GetFullPath($testBackendPath)
}
. $backend

function Assert([bool]$Condition,[string]$Message) {
    if (-not $Condition) { throw $Message }
}
function HashFile([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($Path)
    try { return ([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','').ToLowerInvariant() }
    finally { $stream.Dispose(); $sha.Dispose() }
}

$scratch = Join-Path ([IO.Path]::GetTempPath()) ('codex-desktop-backend-' + [Guid]::NewGuid().ToString('N'))
$payload = Join-Path $scratch 'payload'
$codeHome = Join-Path $scratch 'home'
[void][IO.Directory]::CreateDirectory($payload)
[void][IO.Directory]::CreateDirectory($codeHome)

$oldCodeHome = $env:CODEX_HOME
try {
    $sourceRoot = if ([string]::IsNullOrWhiteSpace($testPayloadRoot)) {
        Join-Path $project 'scripts'
    } else {
        [IO.Path]::GetFullPath($testPayloadRoot)
    }
    $initSource = if ([string]::IsNullOrWhiteSpace($testPayloadRoot)) {
        Join-Path $sourceRoot '第1步-Codex初始化管理-V4.7.0-GPT5.6-LUNA-MAX.cmd'
    } else {
        Join-Path $sourceRoot 'init.cmd'
    }
    $guardSource = if ([string]::IsNullOrWhiteSpace($testPayloadRoot)) {
        Join-Path $sourceRoot '第2步-Codex启动保护与更新管理器-V5.1.2-GPT5.6-LUNA-MAX.cmd'
    } else {
        Join-Path $sourceRoot 'guard.cmd'
    }
    [IO.File]::Copy($initSource,(Join-Path $payload 'init.cmd'))
    [IO.File]::Copy($guardSource,(Join-Path $payload 'guard.cmd'))

    # Job-file parsing accepts the GUI protocol fields and normalizes aliases.
    $jobFile = Join-Path $scratch 'job.json'
    [IO.File]::WriteAllText($jobFile,'{"Operation":"init","PayloadRoot":"' + $payload.Replace('\','\\') + '","Drive":"D:","ProxyPort":12345,"InstallProtection":false}',[Text.UTF8Encoding]::new($false))
    $resolved = Resolve-BackendInput -RequestedOperation '' -RequestedPayloadRoot '' -RequestedDrive '' -RequestedProxyPort 10808 -RequestedInstallProtection:$false -RequestedJobFile $jobFile
    Assert ($resolved.Operation -eq 'initialize') 'Job operation alias was not normalized.'
    Assert ($resolved.Drive -eq 'D:') 'Job drive was not loaded.'
    Assert ($resolved.ProxyPort -eq 12345) 'Job proxy port was not loaded.'
    Assert (-not $resolved.InstallProtection) 'Job install flag was not loaded.'
    Assert ($resolved.Options.proxyHost -eq '127.0.0.1' -and -not $resolved.Options.subagents -and $resolved.Options.folderManagement) 'Default proxy or feature selections are wrong.'
    Assert ((Convert-BackendOptions @{proxyHost=''}).proxyHost -eq '127.0.0.1') 'Blank proxy IP did not use localhost.'
    Assert ((Convert-BackendOptions @{proxyHost='192.168.1.1'}).proxyHost -eq '192.168.1.1') 'Router proxy IP was not preserved.'
    Assert ((Get-ProxyUrl 7890 '2001:db8::1') -eq 'http://[2001:db8::1]:7890') 'IPv6 proxy URL is invalid.'
    try { Convert-BackendOptions @{proxyHost='192.168.1.1 & echo bad'} | Out-Null; throw 'Unsafe proxy IP was accepted.' } catch { if ($_.Exception.Message -eq 'Unsafe proxy IP was accepted.') { throw } }

    # Port zero is an explicit no-proxy mode, and the optional settings are
    # normalized without accepting shell-significant model characters.
    $optionJson = '{"folderManagement":false,"subagents":false,"cuaRepair":false,"parentModel":"vendor/parent:v2","childModel":"org/child_1","parentEffort":"medium","childEffort":"max"}'
    $optionB64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($optionJson))
    $resolvedOptions = Resolve-BackendInput -RequestedOperation 'initialize' -RequestedPayloadRoot $payload -RequestedDrive 'D' -RequestedProxyPort 0 -RequestedInstallProtection:$false -RequestedOptionsBase64 $optionB64
    Assert ($resolvedOptions.ProxyPort -eq 0) 'Proxy port zero was rejected.'
    Assert (-not $resolvedOptions.Options.folderManagement -and -not $resolvedOptions.Options.subagents -and -not $resolvedOptions.Options.cuaRepair) 'Disabled optional features were not preserved.'
    Assert ($resolvedOptions.Options.parentModel -eq 'vendor/parent:v2' -and $resolvedOptions.Options.childModel -eq 'org/child_1') 'Custom model options were not preserved.'
    try { Resolve-BackendInput -RequestedOperation 'initialize' -RequestedPayloadRoot $payload -RequestedDrive 'D' -RequestedProxyPort 0 -RequestedOptionsBase64 ([Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes('{"parentModel":"bad model"}'))) | Out-Null; throw 'Unsafe model id was accepted.' } catch { if ($_.Exception.Message -eq 'Unsafe model id was accepted.') { throw } }

    # Adapter patching is isolated to the extracted payload and preserves init.cmd.
    $originalHash = HashFile (Join-Path $payload 'init.cmd')
    $adapter = New-InitAdapter (Join-Path $payload 'init.cmd') $payload 'D' 10808
    $adapterText = [IO.File]::ReadAllText($adapter,[Text.Encoding]::GetEncoding(936))
    Assert ($adapterText.Contains('--codex-gui-action')) 'GUI init entry was not added.'
    Assert ($adapterText.Contains('GUI confirmation already supplied')) 'GUI confirmation override was not added.'
    Assert ($adapterText.Contains('@@PROGRESS@@{"step":2')) 'Stage progress marker was not added.'
    Assert ((HashFile (Join-Path $payload 'init.cmd')) -eq $originalHash) 'Payload init.cmd was modified.'
    Assert ($adapterText -notmatch '(?<!\r)\n') 'Generated batch adapter contains bare LF line endings; CALL return positions can become invalid.'
    $configVerifier = [regex]::Match($adapterText,'(?ms)^:VERIFY_CONFIG\r?\n.*?(?=^:VERIFY_FULL\r?$)').Value
    $configFixturePath = Join-Path $codeHome 'config-validation.toml'
    $configAgentsPath = Join-Path $codeHome 'config-validation-agents.md'
    $configFixtureText = "model = `"gpt-6.1-sol`"`r`nmodel_reasoning_effort = `"medium`"`r`nenabled-reasoning-efforts = [low, medium, high, xhigh, max, ultra]`r`ncontext_management.experimental_mode = true`r`nmulti_agent = false`r`n"
    [IO.File]::WriteAllText($configFixturePath,$configFixtureText,[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($configAgentsPath,"BEGIN CODEX LUNA PROMPT V1.3`r`nEND CODEX LUNA PROMPT V1.3`r`n",[Text.UTF8Encoding]::new($false))
    $configFlowPath = Join-Path $scratch 'config-validation.cmd'
    $configHeader = @('@echo off','setlocal EnableExtensions EnableDelayedExpansion','set "CONFIG_FILE=%CODEX_TEST_CONFIG%"','set "AGENTS_FILE=%CODEX_TEST_AGENTS%"','set "DEFAULT_PARENT_MODEL_ID=gpt-6.1-sol"','set "DEFAULT_PARENT_REASONING_EFFORT=medium"','set "LUNA_MAX_THREADS=6"','call :VERIFY_CONFIG','exit /b !errorlevel!') -join "`r`n"
    [IO.File]::WriteAllText($configFlowPath,$configHeader + "`r`n" + $configVerifier,[Text.Encoding]::GetEncoding(936))
    $configEnvironment = @{CODEX_TEST_CONFIG=$configFixturePath;CODEX_TEST_AGENTS=$configAgentsPath;CODEX_GUI_SUBAGENTS='0'}
    $configResult = Invoke-ChildProcess $configFlowPath @() $scratch $configEnvironment $script:InputEncoding
    Assert ($configResult.ExitCode -eq 0) 'Unchecked subagents failed config validation because old rules existed.'
    $configEnvironment.CODEX_GUI_SUBAGENTS = '1'
    $configResult = Invoke-ChildProcess $configFlowPath @() $scratch $configEnvironment $script:InputEncoding
    Assert ($configResult.ExitCode -ne 0) 'Enabled subagents passed config validation with multi_agent disabled.'

    # Exercise the actual embedded verifier against the marker names written
    # by the installer, rather than a mock verifier that always returns PASS.
    $verifySection = [regex]::Match($adapterText,'(?ms)^::AGENTS_VERIFY_PAYLOAD_BEGIN\r?\n(.*?)^::AGENTS_VERIFY_PAYLOAD_END')
    $verifyBase64 = ([regex]::Matches($verifySection.Groups[1].Value,'(?m)^::AVB64:([^\r\n]+)') | ForEach-Object { $_.Groups[1].Value }) -join ''
    $verifyCode = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($verifyBase64))
    $verifyPath = Join-Path $scratch 'verify-agents.ps1'
    [IO.File]::WriteAllText($verifyPath,$verifyCode,[Text.UTF8Encoding]::new($true))
    $markerFixture = "BEGIN CODEX LUNA PROMPT V1.3`nmodel = gpt-5.6-luna`nEND CODEX LUNA PROMPT V1.3`nBEGIN CODEX FOLDER MANAGEMENT PROMPT V1.0`nEND CODEX FOLDER MANAGEMENT PROMPT V1.0`n"
    $markerPath = Join-Path $codeHome 'AGENTS.md'
    [IO.File]::WriteAllText($markerPath,$markerFixture,[Text.UTF8Encoding]::new($false))
    $verifyEnvironment = @('CODEX_INIT_AGENTS_FILE','CODEX_GUI_SUBAGENTS','CODEX_GUI_FOLDER_MANAGEMENT')
    $savedVerifyEnvironment = @{}
    foreach ($name in $verifyEnvironment) { $savedVerifyEnvironment[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
    try {
        $env:CODEX_INIT_AGENTS_FILE = $markerPath
        $env:CODEX_GUI_SUBAGENTS = '1'; $env:CODEX_GUI_FOLDER_MANAGEMENT = '1'
        $actualVerification = @(& $PSHOME\powershell.exe -NoProfile -File $verifyPath)
        Assert ($LASTEXITCODE -eq 0 -and ($actualVerification -join '') -eq 'PASS|1') 'Installed managed markers were rejected by the actual AGENTS verifier.'
        [IO.File]::WriteAllText($markerPath,$markerFixture + "BEGIN CODEX LUNA PROMPT V1.3`nEND CODEX LUNA PROMPT V1.3`n",[Text.UTF8Encoding]::new($false))
        $actualVerification = @(& $PSHOME\powershell.exe -NoProfile -File $verifyPath)
        Assert ($LASTEXITCODE -ne 0 -and ($actualVerification -join '') -eq 'FAIL|2') 'Duplicate Luna blocks were accepted by the actual AGENTS verifier.'
        [IO.File]::WriteAllText($markerPath,'unrelated user instructions',[Text.UTF8Encoding]::new($false))
        $env:CODEX_GUI_SUBAGENTS = '0'; $env:CODEX_GUI_FOLDER_MANAGEMENT = '0'
        $actualVerification = @(& $PSHOME\powershell.exe -NoProfile -File $verifyPath)
        Assert ($LASTEXITCODE -eq 0 -and ($actualVerification -join '') -eq 'PASS|SKIPPED') 'Disabled optional blocks were still required by the actual AGENTS verifier.'
    } finally {
        foreach ($name in $verifyEnvironment) { [Environment]::SetEnvironmentVariable($name,$savedVerifyEnvironment[$name],'Process') }
    }
    # Preserve the real five-stage batch flow, its CALL/return boundaries and
    # final verifier. Replace only side-effecting routines with scratch stubs.
    $flow = [regex]::Match($adapterText,'(?ms)^:ONE_CLICK_INIT\r?\n.*?(?=^:SELECT_FOLDER_DRIVE\r?$)').Value
    $verifier = [regex]::Match($adapterText,'(?ms)^:VERIFY_AGENTS_CONTENT\r?\n.*?(?=^:VERIFY_FOLDER_BINDING\r?$)').Value
    $safeStages = @'
:FULL_INIT
echo CALLED BASE
set "RESULT=PASS"
exit /b 0
:INSTALL_FOLDER_MANAGEMENT
echo CALLED FOLDER
set "FOLDER_RESULT=PASS"
exit /b 0
:VERIFY_FOLDER_BINDING
echo CALLED BINDING
if "%CODEX_GUI_TEST_FAILURE%"=="binding" exit /b 1
exit /b 0
:RUN_CUA_REPAIR
echo CALLED CUA
if "%CODEX_GUI_TEST_FAILURE%"=="cua" exit /b 1
set "CUA_RESULT=PASS"
exit /b 0
:INSTALL_LUNA_PROMPT
echo CALLED LUNA
set "LUNA_PROMPT_RESULT=PASS"
set "LUNA_PROMPT_COUNT=1"
exit /b 0
:VERIFY_CONFIG
exit /b 0
:WRITE_REPORT
echo FINAL_REPORT !RESULT! !LUNA_PROMPT_RESULT! !LUNA_PROMPT_COUNT!
exit /b 0
:WRITE_ROLLBACK
exit /b 0
:ONE_CLICK_INIT_FAIL
exit /b 1
:SELECT_FOLDER_DRIVE
set "CODEX_FOLDER_DRIVE=%CODEX_GUI_DRIVE%"
exit /b 0
'@
    $flowPath = Join-Path $scratch 'real-flow.cmd'
    $flowHeader = @('@echo off','setlocal EnableExtensions EnableDelayedExpansion','chcp 936 >nul','set "SCRIPT_PATH=%~f0"','set "AGENTS_FILE=%CODEX_INIT_AGENTS_FILE%"','set "RTK_FILE=fixture"','set "REPORT_RESULT=PASS"','goto :ONE_CLICK_INIT') -join "`r`n"
    $flowSource = $flowHeader + "`r`n" + $flow + $safeStages + "`r`n" + $verifier + $verifySection.Value + "`r`n"
    [IO.File]::WriteAllText($flowPath,([regex]::Replace($flowSource,'\r\n|\r|\n',"`r`n")),[Text.Encoding]::GetEncoding(936))
    for ($flags = 0; $flags -lt 8; $flags++) {
        $folder = ($flags -band 1) -ne 0; $luna = ($flags -band 2) -ne 0; $cua = ($flags -band 4) -ne 0
        $selectedMarkers = if ($luna) { "BEGIN CODEX LUNA PROMPT V1.3`nEND CODEX LUNA PROMPT V1.3`n" } else { '' }
        if ($folder) { $selectedMarkers += "BEGIN CODEX FOLDER MANAGEMENT PROMPT V1.0`nEND CODEX FOLDER MANAGEMENT PROMPT V1.0`n" }
        [IO.File]::WriteAllText($markerPath,$selectedMarkers,[Text.UTF8Encoding]::new($false))
        $flowEnvironment = @{ CODEX_INIT_AGENTS_FILE=$markerPath; CODEX_GUI_DRIVE='D'; CODEX_GUI_FOLDER_MANAGEMENT=[int]$folder; CODEX_GUI_SUBAGENTS=[int]$luna; CODEX_GUI_CUA_REPAIR=[int]$cua }
        $flowResult = Invoke-ChildProcess $flowPath @() $scratch $flowEnvironment $script:InputEncoding
        Assert ($flowResult.ExitCode -eq 0 -and $flowResult.Output.Contains('RESULT = PASS')) ('Actual batch flow failed with feature flags ' + $flags)
        Assert ([string]::IsNullOrEmpty($flowResult.Error)) ('Actual batch flow emitted command errors with feature flags ' + $flags + ': ' + $flowResult.Error)
        Assert ($flowResult.Output.Contains('CALLED BASE')) 'Actual batch flow skipped full initialization.'
        Assert ($flowResult.Output.Contains('CALLED FOLDER') -eq $folder -and $flowResult.Output.Contains('CALLED BINDING') -eq $folder -and $flowResult.Output.Contains('CALLED CUA') -eq $cua -and $flowResult.Output.Contains('CALLED LUNA') -eq $luna) 'Actual batch flow executed the wrong feature stages.'
    }
    foreach ($failure in @('binding','cua')) {
        $flowEnvironment['CODEX_GUI_TEST_FAILURE'] = $failure
        $flowResult = Invoke-ChildProcess $flowPath @() $scratch $flowEnvironment $script:InputEncoding
        Assert ($flowResult.ExitCode -ne 0 -and -not $flowResult.Output.Contains('CALLED LUNA')) ('Actual batch flow ignored failure in ' + $failure)
    }
    # Redirected Windows PowerShell emits CLIXML unless its output format is
    # explicit. Exercise both embedded writers and a genuine stderr/exit code.
    $consoleFixture = '@echo off' + "`r`n" + 'chcp 936 >nul' + "`r`n" + 'set "CODEX_TEST_SELF=%~f0"' + "`r`n"
    foreach ($tag in @('PMB64','CUAB64')) {
        $writer = "[Console]::OutputEncoding = [Text.UTF8Encoding]::new(`$false)`nGet-AppxPackage -Name OpenAI.Codex | Out-Null; Write-Host '中文检查通过：文件夹与运行时'; Write-Progress -Activity '准备模块' -Status '测试' -PercentComplete 10; [Console]::Error.WriteLine('真实错误保留'); exit 7"
        $writerBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($writer))
        $loader = "`$lines=[IO.File]::ReadAllLines(`$env:CODEX_TEST_SELF,[Text.Encoding]::GetEncoding(936)); `$b=(`$lines | Where-Object {`$_ -like '::$tag*'} | ForEach-Object {`$_.Substring('$tag'.Length+3)}) -join ''; & ([scriptblock]::Create([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(`$b))))"
        $fixturePath = Join-Path $scratch ('console-' + $tag + '.cmd')
        $fixtureText = $consoleFixture + 'powershell.exe -NoProfile -EncodedCommand ' + [Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($loader)) + "`r`n" + 'exit /b %errorlevel%' + "`r`n" + '::' + $tag + ':' + $writerBase64 + "`r`n"
        $fixtureText = Set-InitConsoleProtocol $fixtureText (Join-Path $scratch ('console-scripts-' + $tag))
        [IO.File]::WriteAllText($fixturePath,$fixtureText,[Text.Encoding]::GetEncoding(936))
        $consoleResult = Invoke-ChildProcess $fixturePath @() $scratch @{} $script:InputEncoding
        Assert ($consoleResult.ExitCode -eq 7) 'Console protocol lost the real PowerShell failure code.'
        Assert ($consoleResult.Output.Contains('中文检查通过：文件夹与运行时') -and $consoleResult.Error.Contains('真实错误保留')) 'Console protocol corrupted Chinese stdout or stderr.'
        Assert (($consoleResult.Output + $consoleResult.Error) -notmatch 'CLIXML|<Objs|准备模块') 'Console protocol leaked serialized information or progress records.'
    }
    Remove-InitAdapter $adapter
    Assert (-not (Test-Path -LiteralPath ([IO.Path]::ChangeExtension($adapter,'.scripts')))) 'Private PowerShell wrappers were not removed with the adapter.'
    Remove-Item -LiteralPath (Join-Path $payload '.backend') -Recurse -Force -ErrorAction SilentlyContinue

    $enabledFeatureAdapter = New-InitAdapter (Join-Path $payload 'init.cmd') $payload 'D' 0 ([ordered]@{
        folderManagement = $true; subagents = $true; cuaRepair = $true
        parentModel = 'vendor/parent:v2'; childModel = 'org/child_1'; parentEffort = 'medium'; childEffort = 'max'
    })
    $enabledFeatureText = [IO.File]::ReadAllText($enabledFeatureAdapter,[Text.Encoding]::GetEncoding(936))
    $rulesPayloadPattern = '(?m)^set "CODEX_INIT_RULES_B64=([A-Za-z0-9+/=]+)"\r?$'
    $enabledRulesMatch = [regex]::Match($enabledFeatureText,$rulesPayloadPattern)
    Assert ($enabledRulesMatch.Success) 'Enabled-subagent adapter lost the base work-rules payload.'
    $enabledRules = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($enabledRulesMatch.Groups[1].Value))
    Assert ($enabledRules -match '(?m)^SUBAGENTS\r?$' -and $enabledRules.Contains('并行就并行')) 'Enabled-subagent adapter removed the SUBAGENTS rules section.'

    $featureAdapter = New-InitAdapter (Join-Path $payload 'init.cmd') $payload 'D' 0 ([ordered]@{
        folderManagement = $false; subagents = $false; cuaRepair = $false
        parentModel = 'vendor/parent:v2'; childModel = 'org/child_1'; parentEffort = 'medium'; childEffort = 'max'
    })
    $featureText = [IO.File]::ReadAllText($featureAdapter,[Text.Encoding]::GetEncoding(936))
    $disabledRulesMatch = [regex]::Match($featureText,$rulesPayloadPattern)
    Assert ($disabledRulesMatch.Success) 'Disabled-subagent adapter lost the base work-rules payload.'
    $disabledRules = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($disabledRulesMatch.Groups[1].Value))
    Assert ($disabledRules -notmatch '(?m)^SUBAGENTS\r?$' -and -not $disabledRules.Contains('并行就并行')) 'Disabled-subagent adapter still contained the SUBAGENTS rules section.'
    foreach ($requiredRulesHeader in @('<!-- BEGIN CODEX INIT WORK RULES -->','GOAL','AUTONOMY','PRIORITY','STYLE','DONE','APPROVAL','VERIFY')) {
        Assert ($disabledRules.Contains($requiredRulesHeader)) ('Disabled-subagent adapter dropped base work-rules header: ' + $requiredRulesHeader)
    }
    Assert ($featureText.Contains('if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" (') -and $featureText.Contains('call :SELECT_FOLDER_DRIVE')) 'Folder-management stage was not made optional.'
    Assert ($featureText.Contains('if /i "%CODEX_GUI_CUA_REPAIR%"=="1" call :RUN_CUA_REPAIR')) 'CUA stage was not made optional.'
    Assert ($featureText.Contains('if /i "%CODEX_GUI_SUBAGENTS%"=="1" call :INSTALL_LUNA_PROMPT')) 'Luna stage was not made optional.'
    Assert ($featureText.Contains('set "LUNA_MANAGER_INSTALLED=%CODEX_GUI_SUBAGENTS%"')) 'Base initialization still forced the Luna manager.'
    Assert ($featureText.Contains('echo multi_agent = false')) 'Disabled subagents did not explicitly disable the multi-agent feature.'
    Assert ($featureText.Contains('if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" call :VERIFY_FOLDER_BINDING')) 'Selected folder management lost binding verification.'
    Assert ($featureText.Contains('CODEX_GUI_PROXY_DISABLED')) 'Proxy-disabled initialization did not clear the default endpoint.'
    Remove-InitAdapter $enabledFeatureAdapter
    Assert (-not (Test-Path -LiteralPath ([IO.Path]::ChangeExtension($enabledFeatureAdapter,'.scripts')))) 'Enabled feature adapter wrappers were not removed.'
    Remove-InitAdapter $featureAdapter
    Assert (-not (Test-Path -LiteralPath ([IO.Path]::ChangeExtension($featureAdapter,'.scripts')))) 'Disabled feature adapter wrappers were not removed.'
    Remove-Item -LiteralPath (Join-Path $payload '.backend') -Recurse -Force -ErrorAction SilentlyContinue

    # Execute the patched GUI entry against a tiny mock batch implementation.
    # This verifies the non-interactive entry branch without touching Codex.
    $mockRoot = Join-Path $scratch 'mock-payload'
    [void][IO.Directory]::CreateDirectory($mockRoot)
    $mockLines = @(
        '@echo off','setlocal EnableExtensions EnableDelayedExpansion','set "PROXY_PORT=10808"',
        'if /i "%~1"=="--check-update" (','  exit /b 0',')',':RUN_ACTION',
        'if /i "%~1"=="one-click" goto :ONE_CLICK_INIT','echo ACTION %~1','exit /b 0',
        ':ONE_CLICK_INIT','call :SELECT_FOLDER_DRIVE','call :FULL_INIT','call :INSTALL_FOLDER_MANAGEMENT',
        'call :VERIFY_FOLDER_BINDING','call :RUN_CUA_REPAIR','call :INSTALL_LUNA_PROMPT','echo RESULT = PASS','exit /b 0',
        ':FULL_INIT','echo CALLED FULL_INIT','exit /b 0',':SELECT_FOLDER_DRIVE','echo CALLED SELECT','exit /b 0',
        ':INSTALL_FOLDER_MANAGEMENT','echo CALLED FOLDER','exit /b 0',':VERIFY_FOLDER_BINDING','echo CALLED VERIFY_FOLDER','exit /b 0',
        ':RUN_CUA_REPAIR','echo CALLED CUA','exit /b 0',':INSTALL_LUNA_PROMPT','echo CALLED LUNA','exit /b 0',
        ':CONFIRM','exit /b 0',':CHECK_GITHUB_UPDATE','exit /b 0'
    )
    $mockSource = Join-Path $mockRoot 'init.cmd'
    [IO.File]::WriteAllText($mockSource,($mockLines -join [Environment]::NewLine) + [Environment]::NewLine,[Text.Encoding]::GetEncoding(936))
    $mockAdapter = New-InitAdapter $mockSource $mockRoot 'D' 10808 ([ordered]@{
        folderManagement = $false; subagents = $true; cuaRepair = $false
        parentModel = 'gpt-6.1-sol'; childModel = 'gpt-5.6-luna'; parentEffort = 'medium'; childEffort = 'max'
    })
    $mockOutput = @(& $env:ComSpec /d /c $mockAdapter --codex-gui-action proxy)
    Assert ($LASTEXITCODE -eq 0) 'Patched GUI entry did not exit cleanly.'
    Assert (($mockOutput -join [Environment]::NewLine) -match 'ACTION proxy') 'Patched GUI entry did not call RUN_ACTION.'
    $oldGuiFlags = @($env:CODEX_GUI_FOLDER_MANAGEMENT,$env:CODEX_GUI_CUA_REPAIR,$env:CODEX_GUI_SUBAGENTS,$env:CODEX_GUI_DRIVE,$env:CODEX_GUI_PROXY_PORT)
    try {
        $env:CODEX_GUI_FOLDER_MANAGEMENT = '0'; $env:CODEX_GUI_CUA_REPAIR = '0'; $env:CODEX_GUI_SUBAGENTS = '0'; $env:CODEX_GUI_DRIVE = 'D'; $env:CODEX_GUI_PROXY_PORT = '0'
        $mockDisabledOutput = @(& $env:ComSpec /d /c $mockAdapter --codex-gui-action one-click)
        Assert ($LASTEXITCODE -eq 0) 'Disabled-feature one-click fixture did not exit cleanly.'
        $mockDisabledText = $mockDisabledOutput -join [Environment]::NewLine
        Assert ($mockDisabledText -match 'CALLED FULL_INIT') 'Disabled-feature one-click fixture skipped base initialization.'
        Assert ($mockDisabledText -notmatch 'CALLED (SELECT|FOLDER|VERIFY_FOLDER|CUA|LUNA)') 'Disabled-feature one-click fixture ran an optional stage.'
    } finally {
        $env:CODEX_GUI_FOLDER_MANAGEMENT = $oldGuiFlags[0]; $env:CODEX_GUI_CUA_REPAIR = $oldGuiFlags[1]; $env:CODEX_GUI_SUBAGENTS = $oldGuiFlags[2]; $env:CODEX_GUI_DRIVE = $oldGuiFlags[3]; $env:CODEX_GUI_PROXY_PORT = $oldGuiFlags[4]
    }
    Remove-Item -LiteralPath $mockRoot -Recurse -Force

    # Guard extraction imports function definitions into script scope without
    # running the Guard menu or creating its installation directories.
    $guardDocuments = Join-Path $scratch 'documents'
    [void][IO.Directory]::CreateDirectory($guardDocuments)
    $script:TestGuardDocuments = $guardDocuments
    Set-Item -Path Function:\script:Get-DocumentsPath -Value {
        return $script:TestGuardDocuments
    }
    $isolatedGuardRoot = Join-Path $guardDocuments 'Codex启动保护管理器'
    Initialize-GuardRuntime (Join-Path $payload 'guard.cmd')
    Assert ((Get-Command Install-Or-RepairProtection -ErrorAction SilentlyContinue).CommandType -eq 'Function') 'Guard operation was not imported into script scope.'
    Assert ((Get-Command Load-State -ErrorAction SilentlyContinue).CommandType -eq 'Function') 'Guard state helper was not imported into script scope.'
    Assert (-not (Test-Path -LiteralPath $isolatedGuardRoot)) 'Read-only Guard load created an install root.'
    $proxyFile = Join-Path (Get-CodexHomePath) '.env'
    [IO.File]::WriteAllText($proxyFile,"HTTP_PROXY=http://127.0.0.1:9999`r`nHTTPS_PROXY=http://127.0.0.1:9999`r`n",[Text.UTF8Encoding]::new($false))
    foreach ($selectedPort in @(0,23456)) {
        $proxySnapshot = Set-TransientProxyEnvironment $selectedPort
        try {
            Load-ProxyEnvironment
            if ($selectedPort -eq 0) {
                Assert ([string]::IsNullOrEmpty($env:HTTP_PROXY) -and [string]::IsNullOrEmpty($env:HTTPS_PROXY)) 'Guard reloaded an old proxy after disable.'
                Assert ($null -eq [Net.WebRequest]::DefaultWebProxy) 'Disabled worker retained the system web proxy.'
            } else { Assert ($env:HTTPS_PROXY -eq 'http://127.0.0.1:23456') 'Guard overwrote the selected GUI proxy port.' }
        } finally { Restore-TransientProxyEnvironment $proxySnapshot }
    }

    # Guard operations can be exercised with a tiny extracted payload fixture.
    # The fixture only writes scratch state, so the adapter's postcondition
    # check is covered without invoking the real installer or task scheduler.
    $mockGuardRoot = Join-Path $scratch 'mock-guard-payload'
    [void][IO.Directory]::CreateDirectory($mockGuardRoot)
    $mockGuardCode = @'
function Test-DesktopRunning { return $false }
function Load-State {
    $state = @{}
    if (Test-Path -LiteralPath $script:StateFile -PathType Leaf) {
        $object = Get-Content -LiteralPath $script:StateFile -Raw | ConvertFrom-Json
        foreach ($property in $object.PSObject.Properties) { $state[$property.Name] = $property.Value }
    }
    return $state
}
function Install-Or-RepairProtection {
    [void][IO.Directory]::CreateDirectory($script:StateDir)
    @{ Installed = $true; ActiveMode = 'MockProtected' } |
        ConvertTo-Json -Compress | Set-Content -LiteralPath $script:StateFile -Encoding UTF8
}
'@
    $mockGuardBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($mockGuardCode))
    $mockGuardText = '@echo off' + [Environment]::NewLine +
        '###CODEX_GUARD_PS_B64###' + [Environment]::NewLine + $mockGuardBase64 + [Environment]::NewLine +
        '###CODEX_GUARD_PS_B64_END###' + [Environment]::NewLine
    [IO.File]::WriteAllText((Join-Path $mockGuardRoot 'guard.cmd'),$mockGuardText,[Text.UTF8Encoding]::new($false))
    $guardResult = Invoke-GuardOperation 'guard-install' $mockGuardRoot
    Assert ($guardResult -eq 0) 'Mock Guard install did not return success.'
    $mockStatePath = Join-Path $isolatedGuardRoot 'State\guard-state.json'
    Assert (Test-Path -LiteralPath $mockStatePath -PathType Leaf) 'Mock Guard postcondition did not create state.'
    $mockState = Get-Content -LiteralPath $mockStatePath -Raw | ConvertFrom-Json
    Assert ([bool]$mockState.Installed) 'Mock Guard postcondition did not report installation.'

    # Status is a single machine-readable line and is read-only.
    $env:CODEX_HOME = $codeHome
    $config = Join-Path $codeHome 'config.toml'
    $configText = 'model = "gpt-6.1-sol"' + [Environment]::NewLine + 'model_reasoning_effort = "medium"' + [Environment]::NewLine
    [IO.File]::WriteAllText($config,$configText,[Text.UTF8Encoding]::new($false))
    $statusLines = @(& $PSHOME\powershell.exe -NoProfile -ExecutionPolicy Bypass -File $backend -Operation status -PayloadRoot $payload)
    Assert ($LASTEXITCODE -eq 0) 'Status worker returned a failure.'
    $status = @($statusLines | Where-Object { $_ -like '@@STATUS@*' })
    Assert ($status.Count -eq 1) 'Status did not emit exactly one @@STATUS@@ line.'
    $statusObject = ($status[0].Substring('@@STATUS@@'.Length) | ConvertFrom-Json)
    foreach ($name in @('desktopVersion','desktopHealth','desktopHealthy','configModel','configEffort','protectionInstalled','runtimeMode','runtimeModeDetail','currentCliPath','root','processRunning','backupPath')) {
        Assert ($null -ne $statusObject.PSObject.Properties[$name]) ('Status field missing: ' + $name)
    }
    Assert ($statusObject.configModel -eq 'gpt-6.1-sol') 'Status config model parser failed.'
    Assert ($statusObject.configEffort -eq 'medium') 'Status config effort parser failed.'
    Assert ($statusObject.root -eq [IO.Path]::GetFullPath($payload)) 'Status root field is wrong.'

    # Current persisted overrides take precedence over a stale Guard mode.
    # Protection installation alone never means the standalone CLI is active.
    $runtimeRoot = Join-Path $scratch 'runtime-mode'
    $nativeRoot = Join-Path $scratch 'desktop-mode'
    [void][IO.Directory]::CreateDirectory($runtimeRoot)
    [void][IO.Directory]::CreateDirectory($nativeRoot)
    $managedExe = Join-Path $runtimeRoot 'current\bin\codex.exe'
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($managedExe))
    $nativeExe = Join-Path $nativeRoot 'codex.exe'
    $externalExe = Join-Path $scratch 'external.exe'
    foreach ($exe in @($managedExe,$nativeExe,$externalExe)) { [IO.File]::WriteAllText($exe,'fixture') }
    $staleState = @{ActiveMode='OfficialCurrent';ActiveCliPath=$managedExe;Installed=$true}
    Assert ((Resolve-RuntimeModeInfo '' '' $staleState $runtimeRoot $nativeRoot).Mode -eq 'DesktopNative') 'Stale Guard state overrode native selection.'
    Assert ((Resolve-RuntimeModeInfo $managedExe '' @{ActiveMode='DesktopNativeProtected'} $runtimeRoot $nativeRoot).Mode -eq 'OfficialStandalone') 'A selected managed standalone CLI was reported as native.'
    Assert ((Resolve-RuntimeModeInfo $managedExe '' @{ActiveMode='Rollback';ActiveCliPath=$managedExe} $runtimeRoot $nativeRoot).Mode -eq 'OfficialStandalone') 'Standalone rollback mode was not recognized.'
    Assert ((Resolve-RuntimeModeInfo $nativeExe '' $staleState $runtimeRoot $nativeRoot).Mode -eq 'DesktopNative') 'Desktop component path was not recognized.'
    Assert ((Resolve-RuntimeModeInfo $externalExe '' $null $runtimeRoot $nativeRoot).Mode -eq 'Unknown') 'Unidentified external CLI was claimed to be official.'
    Assert ((Resolve-RuntimeModeInfo (Join-Path $scratch 'missing.exe') '' $staleState $runtimeRoot $nativeRoot).Mode -eq 'Unknown') 'Missing CLI path was claimed to be usable.'
    Assert ((Resolve-RuntimeModeInfo '' 'external-host.exe' $staleState $runtimeRoot $nativeRoot).Mode -eq 'Unknown') 'Host override was ignored.'

    # Proxy updates preserve unrelated .env entries while removing local proxy
    # endpoints when disabled.
    $envFile = Join-Path $codeHome '.env'
    $envBefore = "OPENAI_API_KEY=fixture-secret`r`nHTTP_PROXY=http://127.0.0.1:10808`r`nHTTPS_PROXY=http://127.0.0.1:10808`r`nCUSTOM_FLAG=keep`r`n"
    [IO.File]::WriteAllText($envFile,$envBefore,[Text.UTF8Encoding]::new($false))
    [void](Set-ProxyEnvironmentFile 0 $envBefore)
    $envDisabled = [IO.File]::ReadAllText($envFile)
    Assert ($envDisabled.Contains('OPENAI_API_KEY=fixture-secret') -and $envDisabled.Contains('CUSTOM_FLAG=keep')) 'Proxy disable did not preserve unrelated .env entries.'
    Assert ($envDisabled -notmatch '(?im)^HTTPS?_PROXY=') 'Proxy disable left HTTP(S)_PROXY entries.'
    Assert ($envDisabled -match '(?m)^NO_PROXY=localhost,127.0.0.1,::1\r?$') 'Proxy disable did not set NO_PROXY.'
    [void](Set-ProxyEnvironmentFile 23456 $envDisabled)
    $envEnabled = [IO.File]::ReadAllText($envFile)
    Assert ($envEnabled -match '(?m)^HTTP_PROXY=http://127\.0\.0\.1:23456\r?$' -and $envEnabled -match '(?m)^HTTPS_PROXY=http://127\.0\.0\.1:23456\r?$') 'Proxy enable did not write the requested endpoint.'
    Assert ($envEnabled.Contains('CUSTOM_FLAG=keep')) 'Proxy enable did not preserve unrelated .env entries.'
    [void](Set-ProxyEnvironmentFile 7890 $envEnabled '192.168.1.1')
    Assert ([IO.File]::ReadAllText($envFile).Contains('HTTPS_PROXY=http://192.168.1.1:7890')) 'Router proxy endpoint was not written to the config file.'
    $routerSnapshot = Set-TransientProxyEnvironment 7890 '192.168.1.1'
    try { Assert ($env:HTTPS_PROXY -eq 'http://192.168.1.1:7890') 'Router proxy was not passed to worker requests.' }
    finally { Restore-TransientProxyEnvironment $routerSnapshot }

    # Model replacement is structural: root config keys and the managed Luna
    # block change, while similarly named lines in unrelated sections/docs do not.
    $modelConfigFixture = @'
model = "old-parent"
model_reasoning_effort = "low"

[agents]
model = "section-model"
'@
    [IO.File]::WriteAllText($config,$modelConfigFixture,[Text.UTF8Encoding]::new($false))
    $agentsPath = Join-Path $codeHome 'AGENTS.md'
    $modelAgentsFixture = @'
outside model = old-outside; gpt-5.6-luna remains outside the managed block
BEGIN CODEX LUNA PROMPT V1.3
model = old-child
model_reasoning_effort = low
This is the GPT5.6 LUNA MAX child configuration. Do not substitute another child model or reasoning effort.
Do not create Sol, Terra, GPT-6, Astra, or any other non-Luna child under this protocol.
END CODEX LUNA PROMPT V1.3
'@
    [IO.File]::WriteAllText($agentsPath,$modelAgentsFixture,[Text.UTF8Encoding]::new($false))
    Set-ScopedModelSettings ([ordered]@{ parentModel='vendor/root:v3'; childModel='org/child_2'; parentEffort='high'; childEffort='max'; subagents=$true; folderManagement=$true; cuaRepair=$true })
    $configAfter = [IO.File]::ReadAllText($config)
    $agentsAfter = [IO.File]::ReadAllText($agentsPath)
    Assert ($configAfter -match '(?m)^model = "vendor/root:v3"\r?$' -and $configAfter -match '(?m)^model_reasoning_effort = "high"\r?$') 'Root model settings were not updated structurally.'
    Assert ($configAfter -match '(?m)^model = "section-model"\r?$') 'An unrelated config section was modified.'
    Assert ($agentsAfter -match '(?m)^model = org/child_2\r?$' -and $agentsAfter -match '(?m)^model_reasoning_effort = max\r?$') 'Managed child model settings were not updated.'
    Assert ($agentsAfter.Contains('outside model = old-outside; gpt-5.6-luna remains outside the managed block')) 'An unrelated AGENTS line was modified.'
    Assert ($agentsAfter -notmatch '(?ms)BEGIN CODEX LUNA PROMPT.*gpt-5\.6-luna|Do not create Sol, Terra, GPT-6, Astra') 'Managed child model prose still forbids the selected custom model.'

    # Full initialization rebuilds global AGENTS.md from the current embedded
    # baseline and RTK reference. Extract the original payload before the
    # adapter rewrites embedded PowerShell wrappers, then execute it against
    # isolated fixtures. Old managed blocks, user text, drive bindings, and
    # malformed markers must not affect the fresh output.
    $originalInitPath = Join-Path $payload 'init.cmd'
    $originalInitText = $script:InputEncoding.GetString([IO.File]::ReadAllBytes($originalInitPath))
    if ($originalInitText.Length -gt 0 -and $originalInitText[0] -eq [char]0xFEFF) { $originalInitText = $originalInitText.Substring(1) }
    $rebuildSection = [regex]::Match($originalInitText,'(?ms)^:REBUILD_AGENTS_FILE\r?\n.*?(?=^:VERIFY_AGENTS_CONTENT\r?$)').Value
    $rebuildCommandMatch = [regex]::Match($rebuildSection,'(?m)^powershell\.exe -NoProfile -ExecutionPolicy Bypass -EncodedCommand ([A-Za-z0-9+/=]+)')
    Assert ($rebuildCommandMatch.Success) 'Original init source did not contain the REBUILD_AGENTS_FILE payload.'
    $rebuildCode = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($rebuildCommandMatch.Groups[1].Value))
    $rulesMatch = [regex]::Match($originalInitText,'(?m)^set "CODEX_INIT_RULES_B64=([A-Za-z0-9+/=]+)"\r?$')
    Assert ($rulesMatch.Success) 'Original init source did not contain the embedded base rules payload.'
    $rulesB64 = $rulesMatch.Groups[1].Value
    $baselineRules = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($rulesB64))
    $baselineRules = $baselineRules.Replace('持续到请求的结果真正完真正完成。','持续到请求的结果真正完成。')
    $baselineRules = $baselineRules.Replace("`r`n","`n").Replace("`r","`n")
    $rtkFixturePath = Join-Path $scratch 'rtk-fixture.md'
    [IO.File]::WriteAllText($rtkFixturePath,'RTK fixture reference',[Text.UTF8Encoding]::new($false))
    $expectedRebuildText = '@' + $rtkFixturePath + "`n`n" + $baselineRules.TrimEnd([char[]]@([char]13,[char]10)) + "`n"
    $rebuildPath = Join-Path $scratch 'rebuild-agents.ps1'
    [IO.File]::WriteAllText($rebuildPath,$rebuildCode,[Text.UTF8Encoding]::new($true))
    $projectAgentsPath = Join-Path $scratch 'project\AGENTS.md'
    [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($projectAgentsPath))
    [IO.File]::WriteAllText($projectAgentsPath,'project-local instructions',[Text.UTF8Encoding]::new($true))
    $projectAgentsHash = HashFile $projectAgentsPath
    $legacyAgentsFixtures = @(
        ((@(
            'user-owned instruction must not leak into the rebuilt file',
            'Configured Codex workspace root: D:\Codex',
            'BEGIN CODEX LUNA PROMPT V1.3',
            'old Luna instructions',
            'END CODEX LUNA PROMPT V1.3',
            'BEGIN CODEX FOLDER MANAGEMENT PROMPT V1.0',
            'folderManagement = false',
            'END CODEX FOLDER MANAGEMENT PROMPT V1.0',
            'BEGIN LEMGE LUNA PROMPT V9.9',
            'incomplete old marker'
        ) -join "`r`n") + "`r`n"),
        ((@(
            'malformed legacy content should not block a fresh reset',
            'BEGIN CODEX LUNA PROMPT V2.0',
            'END CODEX FOLDER MANAGEMENT PROMPT V1.0',
            'user text after malformed markers'
        ) -join "`r`n") + "`r`n")
    )
    $rebuildEnvironmentNames = @('CODEX_INIT_AGENTS_FILE','CODEX_INIT_RTK_FILE','CODEX_INIT_RULES_B64')
    foreach ($legacyAgentsText in $legacyAgentsFixtures) {
        [IO.File]::WriteAllText($agentsPath,$legacyAgentsText,[Text.UTF8Encoding]::new($true))
        $savedRebuildEnvironment = @{}
        foreach ($name in $rebuildEnvironmentNames) { $savedRebuildEnvironment[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
        try {
            $env:CODEX_INIT_AGENTS_FILE = $agentsPath
            $env:CODEX_INIT_RTK_FILE = $rtkFixturePath
            $env:CODEX_INIT_RULES_B64 = $rulesB64
            $rebuildOutput = @(& $PSHOME\powershell.exe -NoProfile -ExecutionPolicy Bypass -File $rebuildPath 2>&1)
            $rebuildExitCode = $LASTEXITCODE
        } finally {
            foreach ($name in $rebuildEnvironmentNames) { [Environment]::SetEnvironmentVariable($name,$savedRebuildEnvironment[$name],'Process') }
        }
        Assert ($rebuildExitCode -eq 0) ('Fresh AGENTS rebuild failed: ' + ($rebuildOutput -join ' '))
        $rebuiltAgentsText = [IO.File]::ReadAllText($agentsPath,[Text.Encoding]::UTF8)
        Assert ([Convert]::ToBase64String([IO.File]::ReadAllBytes($agentsPath)) -eq [Convert]::ToBase64String([Text.UTF8Encoding]::new($false).GetBytes($expectedRebuildText))) 'Fresh AGENTS rebuild did not produce the embedded baseline and RTK reference exactly.'
        Assert ($rebuiltAgentsText.Contains('@' + $rtkFixturePath) -and $rebuiltAgentsText.Contains('<!-- BEGIN CODEX INIT WORK RULES -->') -and $rebuiltAgentsText.Contains('GOAL')) 'Fresh AGENTS rebuild omitted the RTK reference or base work rules.'
        Assert ($rebuiltAgentsText -notmatch 'user-owned instruction|old Luna instructions|BEGIN CODEX LUNA PROMPT|BEGIN CODEX FOLDER MANAGEMENT|BEGIN LEMGE|D:\\Codex|folderManagement = false') 'Fresh AGENTS rebuild retained old global rules or drive binding text.'
        Assert ((HashFile $projectAgentsPath) -eq $projectAgentsHash) 'Project AGENTS.md changed during the global AGENTS rebuild.'
    }

    # WRITE_AGENTS_RULES itself must perform only the base reset and base
    # validation. Optional Luna and folder blocks are installed by later
    # stages, even when their GUI selections are enabled.
    $writeAgentsSection = [regex]::Match($originalInitText,'(?ms)^:WRITE_AGENTS_RULES\r?\n.*?(?=^:REBUILD_AGENTS_FILE\r?$)').Value
    Assert ($writeAgentsSection.Length -gt 0) 'Original init source did not contain WRITE_AGENTS_RULES.'
    $writeFixtureRoot = Join-Path $scratch 'write-agents-fixture'
    [void][IO.Directory]::CreateDirectory($writeFixtureRoot)
    $writeAgentsPath = Join-Path $writeFixtureRoot 'AGENTS.md'
    $writeRtkPath = Join-Path $writeFixtureRoot 'RTK.md'
    $writeHomePath = Join-Path $writeFixtureRoot 'home'
    [void][IO.Directory]::CreateDirectory($writeHomePath)
    [IO.File]::WriteAllText($writeAgentsPath,"old global content`r`nBEGIN CODEX LUNA PROMPT V1.3`r`nold Luna block`r`nEND CODEX LUNA PROMPT V1.3`r`n",[Text.UTF8Encoding]::new($true))
    [IO.File]::WriteAllText($writeRtkPath,'RTK fixture',[Text.UTF8Encoding]::new($false))
    $writeFixturePath = Join-Path $writeFixtureRoot 'write-agents-flow.cmd'
    $writeHeader = @(
        '@echo off',
        'setlocal EnableExtensions EnableDelayedExpansion',
        'set "CODEX_HOME_DIR=%CODEX_TEST_HOME%"',
        'set "AGENTS_FILE=%CODEX_TEST_AGENTS%"',
        'set "RTK_FILE=%CODEX_TEST_RTK%"',
        'set "CODEX_GUI_FOLDER_MANAGEMENT=%CODEX_TEST_FOLDER%"',
        'set "CODEX_GUI_SUBAGENTS=%CODEX_TEST_SUBAGENTS%"',
        'call :WRITE_AGENTS_RULES',
        'exit /b %errorlevel%'
    ) -join "`r`n"
    $writeFixtureText = $writeHeader + "`r`n" + [regex]::Replace($writeAgentsSection,'\r\n|\r|\n',"`r`n") + [regex]::Replace($rebuildSection,'\r\n|\r|\n',"`r`n") + "`r`n"
    [IO.File]::WriteAllText($writeFixturePath,$writeFixtureText,[Text.Encoding]::GetEncoding(936))
    $writeEnvironment = @{
        CODEX_TEST_HOME = $writeHomePath; CODEX_TEST_AGENTS = $writeAgentsPath
        CODEX_TEST_RTK = $writeRtkPath; CODEX_TEST_FOLDER = '1'; CODEX_TEST_SUBAGENTS = '1'
    }
    $writeResult = Invoke-ChildProcess $writeFixturePath @() $writeFixtureRoot $writeEnvironment $script:InputEncoding
    Assert ($writeResult.ExitCode -eq 0) ('WRITE_AGENTS_RULES base-only fixture failed: ' + $writeResult.Error)
    $writeResultText = [IO.File]::ReadAllText($writeAgentsPath,[Text.Encoding]::UTF8)
    Assert ($writeResultText -eq ('@' + $writeRtkPath + "`n`n" + $baselineRules.TrimEnd([char[]]@([char]13,[char]10)) + "`n")) 'WRITE_AGENTS_RULES did not write the fresh base output.'
    Assert ($writeResultText -notmatch 'BEGIN CODEX LUNA PROMPT|BEGIN CODEX FOLDER MANAGEMENT PROMPT|old global content') 'WRITE_AGENTS_RULES installed or retained optional/legacy blocks during the base stage.'

    # The real backup label preserves exact bytes, including a UTF-8 BOM, and
    # the FULL_INIT gate stops before rebuilding when that backup fails.
    $backupSection = [regex]::Match($originalInitText,'(?ms)^:BACKUP_AGENTS_AND_RTK\r?\n.*?(?=^:CAPTURE_USER_PATH\r?$)').Value
    Assert ($backupSection.Length -gt 0) 'Original init source did not contain BACKUP_AGENTS_AND_RTK.'
    $backupFixtureRoot = Join-Path $scratch 'backup-fixture'
    [void][IO.Directory]::CreateDirectory($backupFixtureRoot)
    $backupAgentsPath = Join-Path $backupFixtureRoot 'AGENTS.md'
    $backupRtkPath = Join-Path $backupFixtureRoot 'RTK.md'
    $backupExePath = Join-Path $backupFixtureRoot 'rtk.exe'
    $backupDir = Join-Path $backupFixtureRoot 'Backups'
    [void][IO.Directory]::CreateDirectory($backupDir)
    [void][IO.Directory]::CreateDirectory((Join-Path $backupDir 'rtk'))
    $backupAgentsText = "原始 AGENTS 内容`r`nBEGIN CODEX LUNA PROMPT V1.3`r`nlegacy block`r`nEND CODEX LUNA PROMPT V1.3`r`n"
    $backupRtkText = "原始 RTK 内容`r`n"
    [IO.File]::WriteAllText($backupAgentsPath,$backupAgentsText,[Text.UTF8Encoding]::new($true))
    [IO.File]::WriteAllText($backupRtkPath,$backupRtkText,[Text.UTF8Encoding]::new($true))
    [IO.File]::WriteAllText($backupExePath,'fixture executable',[Text.UTF8Encoding]::new($false))
    $backupAgentsBytesB64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($backupAgentsPath))
    $backupRtkBytesB64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($backupRtkPath))
    $backupFixturePath = Join-Path $backupFixtureRoot 'backup-flow.cmd'
    $backupHeader = @(
        '@echo off',
        'setlocal EnableExtensions EnableDelayedExpansion',
        'set "AGENTS_FILE=%CODEX_TEST_AGENTS%"',
        'set "RTK_FILE=%CODEX_TEST_RTK%"',
        'set "RTK_EXE=%CODEX_TEST_RTK_EXE%"',
        'set "BACKUP_DIR=%CODEX_TEST_BACKUP%"',
        'set "CODEX_INIT_AGENTS_FILE=%CODEX_TEST_AGENTS%"',
        'set "CODEX_INIT_RTK_FILE=%CODEX_TEST_RTK%"',
        'set "CODEX_INIT_RULES_B64=%CODEX_TEST_RULES_B64%"',
        'call :FULL_INIT',
        'exit /b %errorlevel%',
        ':FULL_INIT',
        'call :BACKUP_AGENTS_AND_RTK',
        'if errorlevel 1 exit /b 1',
        'call :REBUILD_AGENTS_FILE',
        'if errorlevel 1 exit /b 1',
        'exit /b 0'
    ) -join "`r`n"
    $backupFixtureText = $backupHeader + "`r`n" + [regex]::Replace($backupSection,'\r\n|\r|\n',"`r`n") + "`r`n" + [regex]::Replace($rebuildSection,'\r\n|\r|\n',"`r`n") + "`r`n"
    [IO.File]::WriteAllText($backupFixturePath,$backupFixtureText,[Text.Encoding]::GetEncoding(936))
    $backupEnvironment = @{
        CODEX_TEST_AGENTS = $backupAgentsPath; CODEX_TEST_RTK = $backupRtkPath
        CODEX_TEST_RTK_EXE = $backupExePath; CODEX_TEST_BACKUP = $backupDir
        CODEX_TEST_RULES_B64 = $rulesB64
    }
    $backupResult = Invoke-ChildProcess $backupFixturePath @() $backupFixtureRoot $backupEnvironment $script:InputEncoding
    Assert ($backupResult.ExitCode -eq 0) ('Actual backup/rebuild fixture failed: ' + $backupResult.Error)
    Assert ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $backupDir 'AGENTS.md'))) -eq $backupAgentsBytesB64) 'BACKUP_AGENTS_AND_RTK did not preserve AGENTS.md bytes exactly.'
    Assert ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $backupDir 'RTK.md'))) -eq $backupRtkBytesB64) 'BACKUP_AGENTS_AND_RTK did not preserve RTK.md bytes exactly.'
    Assert ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $backupDir 'rtk\rtk.exe'))) -eq [Convert]::ToBase64String([IO.File]::ReadAllBytes($backupExePath))) 'BACKUP_AGENTS_AND_RTK did not preserve rtk.exe bytes exactly.'
    $expectedBackupRebuildText = '@' + $backupRtkPath + "`n`n" + $baselineRules.TrimEnd([char[]]@([char]13,[char]10)) + "`n"
    Assert ([IO.File]::ReadAllText($backupAgentsPath,[Text.Encoding]::UTF8) -eq $expectedBackupRebuildText) 'FULL_INIT did not rebuild AGENTS.md after a successful backup.'

    $failureRoot = Join-Path $scratch 'backup-failure-fixture'
    [void][IO.Directory]::CreateDirectory($failureRoot)
    $failureAgentsPath = Join-Path $failureRoot 'AGENTS.md'
    $failureRtkPath = Join-Path $failureRoot 'RTK.md'
    $failureExePath = Join-Path $failureRoot 'rtk.exe'
    $failureBackupBlocker = Join-Path $failureRoot 'backup-is-a-file'
    [IO.File]::WriteAllBytes($failureAgentsPath,[Convert]::FromBase64String($backupAgentsBytesB64))
    [IO.File]::WriteAllBytes($failureRtkPath,[Convert]::FromBase64String($backupRtkBytesB64))
    [IO.File]::WriteAllText($failureExePath,'fixture executable',[Text.UTF8Encoding]::new($false))
    [IO.File]::WriteAllText($failureBackupBlocker,'not a directory',[Text.UTF8Encoding]::new($false))
    $failureEnvironment = @{
        CODEX_TEST_AGENTS = $failureAgentsPath; CODEX_TEST_RTK = $failureRtkPath
        CODEX_TEST_RTK_EXE = $failureExePath; CODEX_TEST_BACKUP = $failureBackupBlocker
        CODEX_TEST_RULES_B64 = $rulesB64
    }
    $failureBeforeB64 = [Convert]::ToBase64String([IO.File]::ReadAllBytes($failureAgentsPath))
    $failureResult = Invoke-ChildProcess $backupFixturePath @() $failureRoot $failureEnvironment $script:InputEncoding
    Assert ($failureResult.ExitCode -ne 0) 'FULL_INIT continued after BACKUP_AGENTS_AND_RTK failed.'
    Assert ([Convert]::ToBase64String([IO.File]::ReadAllBytes($failureAgentsPath)) -eq $failureBeforeB64) 'Backup failure allowed AGENTS.md to be rebuilt.'

    # Critical input failures remain non-zero and do not fall through.
    $rc = Invoke-BackendMain -RequestedOperation 'unknown-operation' -RequestedPayloadRoot $payload
    Assert ($rc -eq 1) 'Unknown operation was swallowed.'
    $missingPayload = Join-Path $scratch 'missing-payload'
    [void][IO.Directory]::CreateDirectory($missingPayload)
    $rc = Invoke-BackendMain -RequestedOperation 'guard-install' -RequestedPayloadRoot $missingPayload
    Assert ($rc -eq 1) 'Missing Guard artifact did not fail.'
    Assert (-not (Test-Path -LiteralPath (Join-Path $missingPayload 'State'))) 'Failure path created a Guard state directory.'

    Write-Output 'PASS: backend protocol parsing, safe init adapter patching, script-scope Guard loading, read-only status, and critical failure propagation.'
} finally {
    if ($null -eq $oldCodeHome) { Remove-Item Env:CODEX_HOME -ErrorAction SilentlyContinue } else { $env:CODEX_HOME = $oldCodeHome }
    if (Test-Path -LiteralPath $scratch) { Remove-Item -LiteralPath $scratch -Recurse -Force }
}
