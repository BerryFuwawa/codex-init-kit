# Codex Desktop GUI backend adapter.
#
# The desktop host extracts init.cmd, guard.cmd, and cua.cmd into a private
# PayloadRoot and invokes this file as a non-interactive worker.  This adapter
# keeps the original scripts as the implementation source of truth while
# providing a deterministic process protocol for the GUI.

[CmdletBinding()]
param(
    [string]$Operation,
    [string]$PayloadRoot,
    [string]$Drive,
    [int]$ProxyPort = 10808,
    [switch]$InstallProtection,
    [string]$JobFile
)

$script:BackendVersion = '1.0.0'
$script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$script:InputEncoding = [System.Text.Encoding]::GetEncoding(936)
$script:ProxyPortWasBound = $PSBoundParameters.ContainsKey('ProxyPort')
$script:InstallProtectionWasBound = $PSBoundParameters.ContainsKey('InstallProtection')
$script:JobWasBound = $PSBoundParameters.ContainsKey('JobFile')

function Set-BackendOutputEncoding {
    try { [Console]::OutputEncoding = $script:Utf8NoBom } catch {}
    $script:OutputEncoding = $script:Utf8NoBom
}
function Write-BackendLine {
    param([AllowNull()][object]$Message)
    $text = if ($null -eq $Message) { '' } else { [string]$Message }
    try { [Console]::Out.WriteLine($text) } catch { Write-Output $text }
}
function Write-BackendErrorLine {
    param([AllowNull()][object]$Message)
    $text = if ($null -eq $Message) { '' } else { [string]$Message }
    try { [Console]::Error.WriteLine($text) } catch { Write-Error $text }
}
function Write-BackendLog { param([string]$Message) Write-BackendLine $Message }
function Write-BackendProgress {
    param([int]$Step,[int]$Total,[Parameter(Mandatory=$true)][string]$Title)
    $payload = [ordered]@{ step = $Step; total = $Total; title = $Title }
    Write-BackendLine ('@@PROGRESS@@' + ($payload | ConvertTo-Json -Compress))
}
function Write-BackendStatus {
    param([Parameter(Mandatory=$true)][object]$Status)
    Write-BackendLine ('@@STATUS@@' + ($Status | ConvertTo-Json -Compress -Depth 8))
}
function Get-ObjectPropertyValue {
    param([AllowNull()][object]$Object,[Parameter(Mandatory=$true)][string]$Name)
    if ($null -eq $Object) { return $null }
    $property = $Object.PSObject.Properties | Where-Object { $_.Name -ieq $Name } | Select-Object -First 1
    if ($property) { return $property.Value }
    return $null
}
function Convert-ToBoolean {
    param([AllowNull()][object]$Value)
    if ($null -eq $Value) { return $false }
    if ($Value -is [bool]) { return [bool]$Value }
    return (([string]$Value).Trim() -match '^(?i:true|yes|y|1|on)$')
}
function Get-DocumentsPath {
    $documents = $null
    try { $documents = [Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments) } catch {}
    if ([string]::IsNullOrWhiteSpace($documents)) {
        $documents = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)) 'Documents'
    }
    return $documents
}
function Get-CodexHomePath {
    $value = [Environment]::GetEnvironmentVariable('CODEX_HOME', 'Process')
    if ([string]::IsNullOrWhiteSpace($value)) { $value = [Environment]::GetEnvironmentVariable('CODEX_HOME', 'User') }
    if ([string]::IsNullOrWhiteSpace($value)) {
        $value = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::UserProfile)) '.codex'
    }
    return [IO.Path]::GetFullPath($value)
}
function Get-BackupRootPath { return (Join-Path (Get-DocumentsPath) 'Codex初始化备份') }
function Get-GuardInstallRootPath { return (Join-Path (Get-DocumentsPath) 'Codex启动保护管理器') }
function Get-LatestBackupPath {
    $root = Get-BackupRootPath
    if (-not (Test-Path -LiteralPath $root -PathType Container)) { return $null }
    $latest = Get-ChildItem -LiteralPath $root -Directory -Filter 'Codex_Full_Reset_*' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($latest) { return $latest.FullName }
    return $null
}
function Normalize-DriveLetter {
    param([string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value)) { throw 'Drive is required for initialize.' }
    $text = $Value.Trim()
    if ($text -notmatch '^(?i:[a-z])(?::)?$') { throw ('Invalid drive value: ' + $Value) }
    return $text.Substring(0, 1).ToUpperInvariant()
}
function Resolve-BackendInput {
    param(
        [string]$RequestedOperation,[string]$RequestedPayloadRoot,[string]$RequestedDrive,
        [int]$RequestedProxyPort,[switch]$RequestedInstallProtection,[string]$RequestedJobFile
    )
    $operation = $RequestedOperation
    $payloadRoot = $RequestedPayloadRoot
    $drive = $RequestedDrive
    $proxyPort = $RequestedProxyPort
    $installProtection = [bool]$RequestedInstallProtection
    if (-not [string]::IsNullOrWhiteSpace($RequestedJobFile)) {
        if (-not (Test-Path -LiteralPath $RequestedJobFile -PathType Leaf)) { throw ('JobFile was not found: ' + $RequestedJobFile) }
        try { $job = Get-Content -LiteralPath $RequestedJobFile -Raw -ErrorAction Stop | ConvertFrom-Json -ErrorAction Stop }
        catch { throw ('JobFile is not valid JSON: ' + $_.Exception.Message) }
        $jobOperation = Get-ObjectPropertyValue $job 'Operation'
        if ([string]::IsNullOrWhiteSpace($operation) -and $null -ne $jobOperation) { $operation = [string]$jobOperation }
        if ([string]::IsNullOrWhiteSpace($payloadRoot)) {
            $jobPayloadRoot = Get-ObjectPropertyValue $job 'PayloadRoot'
            if ($null -ne $jobPayloadRoot) { $payloadRoot = [string]$jobPayloadRoot }
        }
        if ([string]::IsNullOrWhiteSpace($drive)) {
            $jobDrive = Get-ObjectPropertyValue $job 'Drive'
            if ($null -ne $jobDrive) { $drive = [string]$jobDrive }
        }
        if (-not $script:ProxyPortWasBound) {
            $jobPort = Get-ObjectPropertyValue $job 'ProxyPort'
            if ($null -ne $jobPort -and [string]$jobPort -ne '') {
                try { $proxyPort = [int]$jobPort } catch { throw 'JobFile ProxyPort must be an integer.' }
            }
        }
        if (-not $script:InstallProtectionWasBound) {
            $jobInstall = Get-ObjectPropertyValue $job 'InstallProtection'
            if ($null -ne $jobInstall) { $installProtection = Convert-ToBoolean $jobInstall }
        }
    }
    if ([string]::IsNullOrWhiteSpace($operation)) { throw 'Operation is required.' }
    $operation = $operation.Trim().ToLowerInvariant()
    $aliases = @{
        'init' = 'initialize'; 'one-click' = 'initialize'; 'oneclick' = 'initialize'
        'init-rollback-latest' = 'init-rollback'; 'cua' = 'cua-repair'; 'guard' = 'guard-install'
    }
    if ($aliases.ContainsKey($operation)) { $operation = $aliases[$operation] }
    if ($proxyPort -lt 1 -or $proxyPort -gt 65535) { throw 'ProxyPort must be between 1 and 65535.' }
    if (-not [string]::IsNullOrWhiteSpace($payloadRoot)) {
        try { $payloadRoot = [IO.Path]::GetFullPath($payloadRoot) } catch { throw ('Invalid PayloadRoot: ' + $payloadRoot) }
    }
    return [pscustomobject]@{
        Operation=$operation; PayloadRoot=$payloadRoot; Drive=$drive; ProxyPort=$proxyPort
        InstallProtection=$installProtection; JobFile=$RequestedJobFile
    }
}
function Assert-PayloadFile {
    param([Parameter(Mandatory=$true)][string]$Root,[Parameter(Mandatory=$true)][string]$Name)
    if ([string]::IsNullOrWhiteSpace($Root)) { throw 'PayloadRoot is required for this operation.' }
    if (-not (Test-Path -LiteralPath $Root -PathType Container)) { throw ('PayloadRoot was not found: ' + $Root) }
    $path = Join-Path $Root $Name
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw ('Payload artifact is missing: ' + $path) }
    return [IO.Path]::GetFullPath($path)
}
function Get-CmdArgumentText {
    param([string[]]$Arguments)
    $parts = @()
    foreach ($argument in @($Arguments)) {
        if ($null -eq $argument) { continue }
        $parts += ('"' + ([string]$argument).Replace('"','\\"') + '"')
    }
    return ($parts -join ' ')
}
function Emit-ChildText {
    param([AllowNull()][string]$Text)
    if ($null -eq $Text) { return }
    foreach ($line in ($Text -split "\r?\n")) { if ($line.Length -gt 0) { Write-BackendLine $line } }
}
function Invoke-ChildProcess {
    param(
        [Parameter(Mandatory=$true)][string]$FilePath,[string[]]$Arguments=@(),
        [string]$WorkingDirectory,[hashtable]$Environment=@{},
        [System.Text.Encoding]$OutputEncoding=$script:Utf8NoBom
    )
    if (-not (Test-Path -LiteralPath $FilePath -PathType Leaf)) { throw ('Executable was not found: ' + $FilePath) }
    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $env:ComSpec
    if ([string]::IsNullOrWhiteSpace($psi.FileName)) { $psi.FileName = Join-Path $env:SystemRoot 'System32\cmd.exe' }
    $psi.Arguments = '/d /c ""' + $FilePath + '" ' + (Get-CmdArgumentText $Arguments) + '"'
    if (-not [string]::IsNullOrWhiteSpace($WorkingDirectory)) { $psi.WorkingDirectory = $WorkingDirectory }
    $psi.UseShellExecute = $false; $psi.CreateNoWindow = $true
    $psi.RedirectStandardOutput = $true; $psi.RedirectStandardError = $true
    try { $psi.StandardOutputEncoding = $OutputEncoding } catch {}
    try { $psi.StandardErrorEncoding = $OutputEncoding } catch {}
    foreach ($key in $Environment.Keys) {
        if ($null -ne $Environment[$key]) { $psi.EnvironmentVariables[$key] = [string]$Environment[$key] }
    }
    $process = New-Object System.Diagnostics.Process
    $process.StartInfo = $psi
    try {
        if (-not $process.Start()) { throw ('Unable to start process: ' + $FilePath) }
        # Drain stderr asynchronously so a noisy child cannot deadlock while
        # stdout is streamed line by line to the GUI.
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $stdoutBuilder = New-Object System.Text.StringBuilder
        while (($line = $process.StandardOutput.ReadLine()) -ne $null) {
            [void]$stdoutBuilder.AppendLine($line)
            Write-BackendLine $line
        }
        $process.WaitForExit()
        $stdout = $stdoutBuilder.ToString(); $stderr = $stderrTask.Result
        if (-not [string]::IsNullOrWhiteSpace($stderr)) {
            foreach ($line in ($stderr -split "\r?\n")) { if ($line.Length -gt 0) { Write-BackendErrorLine $line } }
        }
        return [pscustomobject]@{ ExitCode=$process.ExitCode; Output=$stdout; Error=$stderr }
    } finally { $process.Dispose() }
}
function New-InitAdapter {
    param(
        [Parameter(Mandatory=$true)][string]$SourcePath,[Parameter(Mandatory=$true)][string]$PayloadRoot,
        [AllowNull()][string]$DriveLetter,[Parameter(Mandatory=$true)][int]$ProxyPortValue
    )
    $sourceText = [IO.File]::ReadAllText($SourcePath, $script:InputEncoding)
    $marker = 'if /i "%~1"=="--check-update" ('
    if (-not $sourceText.Contains($marker)) { throw 'init.cmd entry marker is missing.' }
    $guiEntry = @'
if /i "%~1"=="--codex-gui-action" (
    call :RUN_ACTION "%~2"
    set "CODEX_GUI_RC=!errorlevel!"
    exit /b !CODEX_GUI_RC!
)
if /i "%~1"=="--codex-gui-rollback" (
    call :ROLLBACK_SESSION "%CODEX_GUI_ROLLBACK_PATH%"
    set "CODEX_GUI_RC=!errorlevel!"
    exit /b !CODEX_GUI_RC!
)
'@
    $sourceText = $sourceText.Replace($marker, $guiEntry + [Environment]::NewLine + $marker)
    $sourceText = $sourceText.Replace('set "PROXY_PORT=10808"', 'set "PROXY_PORT=%CODEX_GUI_PROXY_PORT%"' + [Environment]::NewLine + 'if not defined PROXY_PORT set "PROXY_PORT=10808"')
    $driveBlock = @'
:SELECT_FOLDER_DRIVE
set "FOLDER_MANAGEMENT_DRIVE=%CODEX_GUI_DRIVE%"
set "CODEX_FOLDER_DRIVE=%CODEX_GUI_DRIVE%"
echo GUI supplied folder management drive: !CODEX_FOLDER_DRIVE!:
exit /b 0

'@
    $drivePattern = '(?ms)^:SELECT_FOLDER_DRIVE\r?\n.*?(?=^:INSTALL_FOLDER_MANAGEMENT\r?$)'
    $sourceText = [regex]::Replace($sourceText, $drivePattern, $driveBlock)
    if ($sourceText -notmatch '(?m)^:SELECT_FOLDER_DRIVE\r?$') { throw 'Unable to patch init.cmd drive selector.' }
    # The original one-click routine owns the actual five-stage sequencing.
    # Add protocol markers at the existing call sites so progress is emitted
    # only when the corresponding batch stage is entered.
    $stageMarkers = @(
        @('(?m)^call :FULL_INIT\r?$', 'echo @@PROGRESS@@{"step":2,"total":5,"title":"Run full initialization and backup"}' + [Environment]::NewLine + '$0'),
        @('(?m)^call :INSTALL_FOLDER_MANAGEMENT\r?$', 'echo @@PROGRESS@@{"step":3,"total":5,"title":"Install folder management and binding"}' + [Environment]::NewLine + '$0'),
        @('(?m)^call :RUN_CUA_REPAIR\r?$', 'echo @@PROGRESS@@{"step":4,"total":5,"title":"Check and repair CUA runtime"}' + [Environment]::NewLine + '$0'),
        @('(?m)^call :INSTALL_LUNA_PROMPT\r?$', 'echo @@PROGRESS@@{"step":5,"total":5,"title":"Install and verify Luna configuration"}' + [Environment]::NewLine + '$0')
    )
    foreach ($markerEntry in $stageMarkers) {
        # Replace every matching call site.  The one-click flow contains the
        # real stage-2 call (the full-init menu action has a separate call),
        # and both are safe because the GUI entry only dispatches one-click.
        $sourceText = [regex]::Replace($sourceText, $markerEntry[0], $markerEntry[1])
    }
    $confirmBlock = @'
:CONFIRM
echo GUI confirmation already supplied for this operation.
exit /b 0

'@
    $confirmPattern = '(?ms)^:CONFIRM\r?\n.*?(?=^:CHECK_GITHUB_UPDATE\r?$)'
    $sourceText = [regex]::Replace($sourceText, $confirmPattern, $confirmBlock)
    if ($sourceText -notmatch '(?m)^:CONFIRM\r?$') { throw 'Unable to patch init.cmd confirmation block.' }
    $workRoot = Join-Path $PayloadRoot '.backend'
    [void][IO.Directory]::CreateDirectory($workRoot)
    $adapterPath = Join-Path $workRoot ('init-adapter-' + [Guid]::NewGuid().ToString('N') + '.cmd')
    [IO.File]::WriteAllText($adapterPath, $sourceText, $script:InputEncoding)
    return $adapterPath
}
function Invoke-InitOperation {
    param([Parameter(Mandatory=$true)][string]$Action,[Parameter(Mandatory=$true)][string]$PayloadRoot,
          [string]$DriveLetter,[int]$ProxyPortValue=10808)
    $init = Assert-PayloadFile $PayloadRoot 'init.cmd'
    $drive = $null
    if ($Action -eq 'one-click') { $drive = Normalize-DriveLetter $DriveLetter }
    $adapter = $null
    try {
        $adapter = New-InitAdapter $init $PayloadRoot $drive $ProxyPortValue
        if ($Action -eq 'one-click') {
            Write-BackendProgress 1 5 'Confirm folder-management drive'
        } else { Write-BackendProgress 1 1 'Rebuild proxy configuration' }
        $result = Invoke-ChildProcess $adapter @('--codex-gui-action',$Action) $PayloadRoot @{
            CODEX_GUI_DRIVE=$drive; CODEX_GUI_PROXY_PORT=[string]$ProxyPortValue; CODEX_GUI_NO_PAUSE='1'
        } $script:InputEncoding
        if ($result.ExitCode -ne 0) { throw ('init.cmd failed with exit code ' + $result.ExitCode) }
        if ($result.Output -match '(?im)RESULT\s*=\s*FAIL|FAIL') { throw 'init.cmd reported a failed result.' }
        if ($Action -eq 'proxy') {
            $envFile = Join-Path (Get-CodexHomePath) '.env'
            if (-not (Test-Path -LiteralPath $envFile -PathType Leaf)) { throw '.env was not written by proxy operation.' }
            $envText = [IO.File]::ReadAllText($envFile)
            $proxy = 'http://127.0.0.1:' + $ProxyPortValue
            if ($envText -notmatch ('(?m)^HTTP_PROXY=' + [regex]::Escape($proxy) + '$') -or
                $envText -notmatch ('(?m)^HTTPS_PROXY=' + [regex]::Escape($proxy) + '$')) { throw 'Proxy operation did not write the requested proxy endpoint.' }
        }
        return 0
    } finally {
        if ($adapter -and (Test-Path -LiteralPath $adapter)) { Remove-Item -LiteralPath $adapter -Force -ErrorAction SilentlyContinue }
    }
}
function Resolve-InitRollbackPath {
    $latest = Get-LatestBackupPath
    if (-not $latest) { throw 'No initialization backup session is available for rollback.' }
    return $latest
}
function Invoke-InitRollback {
    param([Parameter(Mandatory=$true)][string]$PayloadRoot)
    $init = Assert-PayloadFile $PayloadRoot 'init.cmd'
    $rollback = Resolve-InitRollbackPath
    $adapter = $null
    try {
        $adapter = New-InitAdapter $init $PayloadRoot '' 10808
        Write-BackendProgress 1 1 'Rollback latest initialization backup'
        $result = Invoke-ChildProcess $adapter @('--codex-gui-rollback') $PayloadRoot @{
            CODEX_GUI_ROLLBACK_PATH=$rollback; CODEX_GUI_NO_PAUSE='1'; CODEX_GUI_PROXY_PORT='10808'
        } $script:InputEncoding
        if ($result.ExitCode -ne 0) { throw ('init rollback failed with exit code ' + $result.ExitCode) }
        if ($result.Output -match '(?im)FAIL|rollback.*failed') { throw 'init rollback reported a failure.' }
        return 0
    } finally {
        if ($adapter -and (Test-Path -LiteralPath $adapter)) { Remove-Item -LiteralPath $adapter -Force -ErrorAction SilentlyContinue }
    }
}
function Invoke-CuaOperation {
    param([Parameter(Mandatory=$true)][ValidateSet('check','repair')][string]$Mode,[Parameter(Mandatory=$true)][string]$PayloadRoot)
    $cua = Assert-PayloadFile $PayloadRoot 'cua.cmd'
    $args = if ($Mode -eq 'check') { @('/check','/nopause') } else { @('/nopause') }
    Write-BackendProgress 1 1 $(if ($Mode -eq 'check') { 'Check CUA runtime' } else { 'Repair CUA runtime' })
    $reportRoot = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::LocalApplicationData)) 'OpenAI\Codex\logs\CUA'
    [void][IO.Directory]::CreateDirectory($reportRoot)
    $result = Invoke-ChildProcess $cua $args $PayloadRoot @{
        CODEX_CUA_FIX_NO_PAUSE='1'; CODEX_CUA_FIX_REPORT_ROOT=$reportRoot
    } $script:Utf8NoBom
    if ($result.ExitCode -eq 0) { return 0 }
    if ($Mode -eq 'check' -and $result.ExitCode -eq 2) { Write-BackendLog 'CUA check found a repairable difference.'; return 2 }
    throw ('cua.cmd failed with exit code ' + $result.ExitCode)
}
function Get-GuardPayloadText {
    param([Parameter(Mandatory=$true)][string]$GuardPath)
    $raw = [IO.File]::ReadAllText($GuardPath,[Text.Encoding]::UTF8)
    $begin = '###CODEX_GUARD_PS_B64###'; $end = '###CODEX_GUARD_PS_B64_END###'
    $start = $raw.IndexOf($begin,[StringComparison]::Ordinal); $finish = $raw.IndexOf($end,[StringComparison]::Ordinal)
    if ($start -lt 0 -or $finish -lt ($start+$begin.Length)) { throw 'guard.cmd PowerShell payload markers are missing.' }
    $encoded = $raw.Substring($start+$begin.Length,$finish-($start+$begin.Length)) -replace '\s',''
    try { return [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($encoded)) } catch { throw 'guard.cmd PowerShell payload is invalid base64.' }
}
function Initialize-GuardRuntime {
    param([Parameter(Mandatory=$true)][string]$GuardPath,[switch]$ForModification)
    $code = Get-GuardPayloadText $GuardPath
    $tokens=$null; $errors=$null
    $ast = [Management.Automation.Language.Parser]::ParseInput($code,[ref]$tokens,[ref]$errors)
    if ($errors.Count -gt 0) { throw 'guard.cmd embedded PowerShell payload has syntax errors.' }
    $documents = Get-DocumentsPath
    $script:ErrorActionPreference='Stop'; $script:ProgressPreference='SilentlyContinue'
    $script:ToolVersion='V5.2.1'; $script:SourceCmd=$GuardPath; $script:SourceDir=Split-Path -Parent $GuardPath
    $script:InstallerUrl='https://chatgpt.com/codex/install.ps1'; $script:UpdateIntervalHours=24
    $script:DefaultParentModelId='gpt-6.1-sol'; $script:DefaultParentReasoningEffort='medium'; $script:LunaMaxThreads=6
    $script:Documents=$documents; $script:InstallRoot=Join-Path $documents 'Codex启动保护管理器'
    $script:StateDir=Join-Path $script:InstallRoot 'State'; $script:StateFile=Join-Path $script:StateDir 'guard-state.json'
    $script:StartupDir=[Environment]::GetFolderPath([Environment+SpecialFolder]::Startup)
    $script:StartupShortcut=Join-Path $script:StartupDir 'Codex启动保护自动维护.lnk'; $script:AutoTaskName='CodexGuardAuto'
    $script:InstalledCmd=Join-Path $script:InstallRoot 'Codex_启动保护与更新管理器.cmd'
    $script:AutoScript=Join-Path $script:InstallRoot 'Codex_Guard_Auto.ps1'; $script:AutoLauncher=Join-Path $script:InstallRoot 'Codex_Guard_Auto.vbs'
    $script:LogDir=Join-Path $documents 'Codex启动保护管理器\Logs'
    $script:LogFile=Join-Path $script:LogDir ('CodexGuard_'+(Get-Date -Format 'yyyyMMdd')+'.log')
    foreach ($functionAst in @($ast.FindAll({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst]},$false))) {
        # Function definitions are imported into script scope deliberately.
        # Dot-sourcing here would leave them in this initializer's local
        # scope, so Invoke-GuardOperation could not see them afterwards.
        Set-Item -Path ('Function:\script:' + $functionAst.Name) -Value $functionAst.Body.GetScriptBlock()
    }
    if ($ForModification) {
        [void][IO.Directory]::CreateDirectory($script:InstallRoot); [void][IO.Directory]::CreateDirectory($script:StateDir); [void][IO.Directory]::CreateDirectory($script:LogDir)
        Set-Item -Path 'Function:\script:Read-Host' -Value {
            param([string]$Prompt)
            Write-BackendLog ('GUI confirmation accepted: ' + $Prompt)
            return 'Y'
        }
    }
}
function Invoke-GuardOperation {
    param([Parameter(Mandatory=$true)][ValidateSet('guard-install','guard-update','guard-native','guard-current','guard-rollback','guard-uninstall','doctor')][string]$Action,[Parameter(Mandatory=$true)][string]$PayloadRoot)
    $guard = Assert-PayloadFile $PayloadRoot 'guard.cmd'
    Initialize-GuardRuntime $guard -ForModification
    if ($Action -ne 'doctor' -and (Test-DesktopRunning)) { throw 'Codex is running. Close Codex before changing startup protection.' }
    Write-BackendProgress 1 1 ('执行 ' + $Action)
    $beforeState = Load-State
    $beforeCheck = if ($beforeState.ContainsKey('LastUpdateCheckAt')) {[string]$beforeState['LastUpdateCheckAt']} else {''}
    $beforeInstalled = if ($beforeState.ContainsKey('Installed')) { $beforeState['Installed'] } else { $false }
    $doctorStartedUtc = $null
    if ($Action -eq 'doctor') {
        # The Guard script's doctor pipeline can inherit a stale native exit
        # code and an old doctor_*.txt file. Reset and timestamp before it is
        # invoked so the postcondition belongs to this request.
        $doctorStartedUtc = [DateTime]::UtcNow
        $global:LASTEXITCODE = 0
    }
    switch ($Action) {
        'guard-install' { Install-Or-RepairProtection | ForEach-Object { Write-BackendLine $_ } | Out-Null }
        'guard-update' { if (-not (Convert-ToBoolean $beforeInstalled)) { throw 'Startup protection is not installed.' }; Update-ProtectedRuntime | ForEach-Object { Write-BackendLine $_ } | Out-Null }
        'guard-native' { Restore-DesktopNative | ForEach-Object { Write-BackendLine $_ } | Out-Null }
        'guard-current' { Switch-ToOfficialCurrent | ForEach-Object { Write-BackendLine $_ } | Out-Null }
        'guard-rollback' { Rollback-Protection | ForEach-Object { Write-BackendLine $_ } | Out-Null }
        'guard-uninstall' { Remove-Protection | ForEach-Object { Write-BackendLine $_ } | Out-Null }
        'doctor' { Run-Doctor | ForEach-Object { Write-BackendLine $_ } | Out-Null }
    }
    if ($Action -eq 'doctor') {
        $doctorExitCode = if ($null -eq $global:LASTEXITCODE) { 0 } else { [int]$global:LASTEXITCODE }
        if ($doctorExitCode -ne 0) { throw ('codex doctor failed with native exit code ' + $doctorExitCode + '.') }
        $newDoctorLogs = @(Get-ChildItem -LiteralPath $script:LogDir -Filter 'doctor_*.txt' -File -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTimeUtc -ge $doctorStartedUtc })
        if ($newDoctorLogs.Count -eq 0) { throw 'Codex doctor did not produce a new diagnostic log.' }
    }
    $afterState = Load-State
    $afterInstalled = if ($afterState.ContainsKey('Installed')) { $afterState['Installed'] } else { $false }
    switch ($Action) {
        'guard-install' { if (-not (Convert-ToBoolean $afterInstalled)) { throw 'Startup protection install did not complete.' } }
        'guard-update' {
            $afterCheck = if ($afterState.ContainsKey('LastUpdateCheckAt')) {[string]$afterState['LastUpdateCheckAt']} else {''}
            if ([string]::IsNullOrWhiteSpace($afterCheck) -or $afterCheck -eq $beforeCheck) { throw 'Startup protection update did not complete.' }
        }
        'guard-native' { if ([string]$afterState['ActiveMode'] -ne 'DesktopNativeProtected') { throw 'Desktop native mode was not restored.' } }
        'guard-current' { if ([string]$afterState['ActiveMode'] -ne 'OfficialCurrent') { throw 'Official Current was not selected.' } }
        'guard-rollback' { if ([string]$afterState['ActiveMode'] -ne 'Rollback') { throw 'Protection rollback did not complete.' } }
        'guard-uninstall' { if (Convert-ToBoolean $afterInstalled) { throw 'Startup protection uninstall did not complete.' } }
        'doctor' {
            $doctorLogs=@(Get-ChildItem -LiteralPath $script:LogDir -Filter 'doctor_*.txt' -File -ErrorAction SilentlyContinue |
                Where-Object { $_.LastWriteTimeUtc -ge $doctorStartedUtc })
            if ($doctorLogs.Count -eq 0) { throw 'Codex doctor did not produce a new diagnostic log.' }
            $latest=$doctorLogs | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
            if ((Get-Content -LiteralPath $latest.FullName -Raw -ErrorAction SilentlyContinue) -match '(?i)doctor.*(failed|失败)') { throw 'Codex doctor reported a failure.' }
        }
    }
    return 0
}
function Get-ConfigValue {
    param([string]$Text,[string]$Name)
    if ([string]::IsNullOrWhiteSpace($Text)) { return $null }
    $pattern='(?im)^\s*'+[regex]::Escape($Name)+'\s*=\s*(["'']?)([^#\r\n]*?)\1\s*(?:#.*)?$'
    $match=[regex]::Match($Text,$pattern)
    if ($match.Success) { return $match.Groups[2].Value.Trim() }
    return $null
}
function Get-ReadOnlyDesktopInfo {
    try { $package=Get-AppxPackage -Name 'OpenAI.Codex' -ErrorAction SilentlyContinue | Sort-Object Version -Descending | Select-Object -First 1 } catch { $package=$null }
    if (-not $package) { return [pscustomobject]@{Found=$false;Version=$null;Healthy=$false;Health='missing';InstallLocation=$null} }
    $resources=Join-Path $package.InstallLocation 'app\resources'
    $required=@('codex.exe','codex-code-mode-host.exe','codex-command-runner.exe','codex-windows-sandbox-setup.exe')
    $healthy=$true; foreach($name in $required){if(-not(Test-Path -LiteralPath (Join-Path $resources $name) -PathType Leaf)){$healthy=$false}}
    return [pscustomobject]@{Found=$true;Version=$package.Version.ToString();Healthy=$healthy;Health=if($healthy){'healthy'}else{'incomplete'};InstallLocation=$package.InstallLocation}
}
function Get-ReadOnlyConfigInfo {
    $path=Join-Path (Get-CodexHomePath) 'config.toml'; $text=''
    if(Test-Path -LiteralPath $path -PathType Leaf){try{$text=[IO.File]::ReadAllText($path,[Text.UTF8Encoding]::new($false,$true))}catch{$text=[IO.File]::ReadAllText($path)}}
    return [pscustomobject]@{Path=$path;Exists=(Test-Path -LiteralPath $path -PathType Leaf);Model=(Get-ConfigValue $text 'model');Effort=(Get-ConfigValue $text 'model_reasoning_effort')}
}
function Get-ReadOnlyProtectionInstalled {
    $statePath=Join-Path (Get-GuardInstallRootPath) 'State\guard-state.json'
    if(-not(Test-Path -LiteralPath $statePath -PathType Leaf)){return $false}
    try{$state=Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json;return (Convert-ToBoolean (Get-ObjectPropertyValue $state 'Installed'))}catch{return $false}
}
function Get-ReadOnlyStatus {
    param([string]$PayloadRoot)
    $desktop=Get-ReadOnlyDesktopInfo; $config=Get-ReadOnlyConfigInfo; $backupRoot=Get-BackupRootPath; $latestBackup=Get-LatestBackupPath
    $running=$false; try{$running=[bool](Get-Process -ErrorAction SilentlyContinue | Where-Object{$_.ProcessName -match '^(Codex|ChatGPT)$'})}catch{}
    $protectionRoot=Get-GuardInstallRootPath; $protectionInstalled=Get-ReadOnlyProtectionInstalled
    return [ordered]@{
        backendVersion=$script:BackendVersion; desktopVersion=$desktop.Version; desktopHealth=$desktop.Health; desktopHealthy=$desktop.Healthy; desktopFound=$desktop.Found
        desktopInstallLocation=$desktop.InstallLocation; configModel=$config.Model; configEffort=$config.Effort
        config=[ordered]@{model=$config.Model;effort=$config.Effort;path=$config.Path;exists=$config.Exists}
        protectionInstalled=$protectionInstalled; protectionRoot=$protectionRoot; root=$PayloadRoot; processRunning=$running
        backupPath=if($latestBackup){$latestBackup}else{$backupRoot}; backupRoot=$backupRoot; latestBackupPath=$latestBackup
        statePath=Join-Path $protectionRoot 'State\guard-state.json'
    }
}
function Invoke-BackendMain {
    param([string]$RequestedOperation,[string]$RequestedPayloadRoot,[string]$RequestedDrive,[int]$RequestedProxyPort=10808,[switch]$RequestedInstallProtection,[string]$RequestedJobFile)
    Set-BackendOutputEncoding
    try {
        $input=Resolve-BackendInput -RequestedOperation $RequestedOperation -RequestedPayloadRoot $RequestedPayloadRoot -RequestedDrive $RequestedDrive -RequestedProxyPort $RequestedProxyPort -RequestedInstallProtection:$RequestedInstallProtection.IsPresent -RequestedJobFile $RequestedJobFile
        switch($input.Operation){
            'status' { Write-BackendStatus (Get-ReadOnlyStatus $input.PayloadRoot); return 0 }
            'initialize' { [void](Invoke-InitOperation 'one-click' $input.PayloadRoot $input.Drive $input.ProxyPort); if($input.InstallProtection){[void](Invoke-GuardOperation 'guard-install' $input.PayloadRoot)}; return 0 }
            'proxy' { return (Invoke-InitOperation 'proxy' $input.PayloadRoot $null $input.ProxyPort) }
            'init-rollback' { return (Invoke-InitRollback $input.PayloadRoot) }
            'cua-check' { return (Invoke-CuaOperation 'check' $input.PayloadRoot) }
            'cua-repair' { return (Invoke-CuaOperation 'repair' $input.PayloadRoot) }
            'guard-install' { return (Invoke-GuardOperation 'guard-install' $input.PayloadRoot) }
            'guard-update' { return (Invoke-GuardOperation 'guard-update' $input.PayloadRoot) }
            'guard-native' { return (Invoke-GuardOperation 'guard-native' $input.PayloadRoot) }
            'guard-current' { return (Invoke-GuardOperation 'guard-current' $input.PayloadRoot) }
            'guard-rollback' { return (Invoke-GuardOperation 'guard-rollback' $input.PayloadRoot) }
            'guard-uninstall' { return (Invoke-GuardOperation 'guard-uninstall' $input.PayloadRoot) }
            'doctor' { return (Invoke-GuardOperation 'doctor' $input.PayloadRoot) }
            default { throw ('Unknown operation: ' + $input.Operation) }
        }
    } catch {
        Write-BackendLine ('[FAIL] ' + $_.Exception.Message)
        return 1
    }
}
# Dot-sourcing exposes helpers for adapter tests without starting a worker.
if ($MyInvocation.InvocationName -ne '.') {
    exit (Invoke-BackendMain -RequestedOperation $Operation -RequestedPayloadRoot $PayloadRoot -RequestedDrive $Drive -RequestedProxyPort $ProxyPort -RequestedInstallProtection:$InstallProtection.IsPresent -RequestedJobFile $JobFile)
}
