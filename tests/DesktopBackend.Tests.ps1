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

    # Adapter patching is isolated to the extracted payload and preserves init.cmd.
    $originalHash = HashFile (Join-Path $payload 'init.cmd')
    $adapter = New-InitAdapter (Join-Path $payload 'init.cmd') $payload 'D' 10808
    $adapterText = [IO.File]::ReadAllText($adapter,[Text.Encoding]::GetEncoding(936))
    Assert ($adapterText.Contains('--codex-gui-action')) 'GUI init entry was not added.'
    Assert ($adapterText.Contains('GUI confirmation already supplied')) 'GUI confirmation override was not added.'
    Assert ($adapterText.Contains('@@PROGRESS@@{"step":2')) 'Stage progress marker was not added.'
    Assert ((HashFile (Join-Path $payload 'init.cmd')) -eq $originalHash) 'Payload init.cmd was modified.'
    Remove-Item -LiteralPath $adapter -Force
    Remove-Item -LiteralPath (Join-Path $payload '.backend') -Recurse -Force -ErrorAction SilentlyContinue

    # Execute the patched GUI entry against a tiny mock batch implementation.
    # This verifies the non-interactive entry branch without touching Codex.
    $mockRoot = Join-Path $scratch 'mock-payload'
    [void][IO.Directory]::CreateDirectory($mockRoot)
    $mockLines = @(
        '@echo off','setlocal EnableExtensions EnableDelayedExpansion','set "PROXY_PORT=10808"',
        'if /i "%~1"=="--check-update" (','  exit /b 0',')',':RUN_ACTION',
        'echo ACTION %~1','exit /b 0',':SELECT_FOLDER_DRIVE','exit /b 0',
        ':INSTALL_FOLDER_MANAGEMENT','exit /b 0',':CONFIRM','exit /b 0',':CHECK_GITHUB_UPDATE','exit /b 0'
    )
    $mockSource = Join-Path $mockRoot 'init.cmd'
    [IO.File]::WriteAllText($mockSource,($mockLines -join [Environment]::NewLine) + [Environment]::NewLine,[Text.Encoding]::GetEncoding(936))
    $mockAdapter = New-InitAdapter $mockSource $mockRoot 'D' 10808
    $mockOutput = @(& $env:ComSpec /d /c $mockAdapter --codex-gui-action proxy)
    Assert ($LASTEXITCODE -eq 0) 'Patched GUI entry did not exit cleanly.'
    Assert (($mockOutput -join [Environment]::NewLine) -match 'ACTION proxy') 'Patched GUI entry did not call RUN_ACTION.'
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
    foreach ($name in @('desktopVersion','desktopHealth','desktopHealthy','configModel','configEffort','protectionInstalled','root','processRunning','backupPath')) {
        Assert ($null -ne $statusObject.PSObject.Properties[$name]) ('Status field missing: ' + $name)
    }
    Assert ($statusObject.configModel -eq 'gpt-6.1-sol') 'Status config model parser failed.'
    Assert ($statusObject.configEffort -eq 'medium') 'Status config effort parser failed.'
    Assert ($statusObject.root -eq [IO.Path]::GetFullPath($payload)) 'Status root field is wrong.'

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
