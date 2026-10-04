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
    [string]$JobFile,
    [string]$OptionsBase64
)

$script:BackendVersion = '1.0.0'
$script:Utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$script:InputEncoding = [System.Text.Encoding]::GetEncoding(936)
$script:ProxyPortWasBound = $PSBoundParameters.ContainsKey('ProxyPort')
$script:InstallProtectionWasBound = $PSBoundParameters.ContainsKey('InstallProtection')
$script:JobWasBound = $PSBoundParameters.ContainsKey('JobFile')
$script:OptionsWasBound = $PSBoundParameters.ContainsKey('OptionsBase64')
$script:DefaultOptions = [ordered]@{
    folderManagement = $true
    subagents = $true
    cuaRepair = $true
    parentModel = 'gpt-6.1-sol'
    childModel = 'gpt-5.6-luna'
    parentEffort = 'medium'
    childEffort = 'max'
}

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
    if ($Object -is [System.Collections.IDictionary]) {
        foreach ($key in $Object.Keys) {
            if ([string]$key -ieq $Name) { return $Object[$key] }
        }
    }
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
function Convert-BackendOptionBoolean {
    param([AllowNull()][object]$Value,[Parameter(Mandatory=$true)][string]$Name,[bool]$Default=$true)
    if ($null -eq $Value) { return $Default }
    if ($Value -is [bool]) { return [bool]$Value }
    throw ('Option ' + $Name + ' must be boolean.')
}
function Convert-BackendOptionString {
    param([AllowNull()][object]$Value,[Parameter(Mandatory=$true)][string]$Name,[Parameter(Mandatory=$true)][string]$Default,[switch]$Model)
    if ($null -eq $Value) { return $Default }
    if ($Value -isnot [string] -or [string]::IsNullOrWhiteSpace([string]$Value)) { throw ('Option ' + $Name + ' must be a non-empty string.') }
    $text = [string]$Value
    if ($Model -and $text -notmatch '^[A-Za-z0-9][A-Za-z0-9._/:+-]{0,127}$') { throw ('Option ' + $Name + ' contains an invalid model id.') }
    if (-not $Model -and $text -notmatch '^[A-Za-z][A-Za-z0-9_-]{0,31}$') { throw ('Option ' + $Name + ' contains an invalid effort.') }
    return $text
}
function Convert-BackendOptions {
    param([AllowNull()][object]$Value,[string]$Base64)
    $source = $Value
    if (-not [string]::IsNullOrWhiteSpace($Base64)) {
        try {
            $bytes = [Convert]::FromBase64String($Base64)
            $source = [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json -ErrorAction Stop
        } catch { throw ('OptionsBase64 is not valid UTF-8 JSON: ' + $_.Exception.Message) }
    }
    $get = { param([string]$Name) Get-ObjectPropertyValue $source $Name }
    $options = [ordered]@{}
    $options.folderManagement = & $get 'folderManagement'
    $options.subagents = & $get 'subagents'
    $options.cuaRepair = & $get 'cuaRepair'
    $options.parentModel = & $get 'parentModel'
    $options.childModel = & $get 'childModel'
    $options.parentEffort = & $get 'parentEffort'
    $options.childEffort = & $get 'childEffort'
    $normalized = [ordered]@{
        folderManagement = Convert-BackendOptionBoolean $options.folderManagement 'folderManagement' $script:DefaultOptions.folderManagement
        subagents = Convert-BackendOptionBoolean $options.subagents 'subagents' $script:DefaultOptions.subagents
        cuaRepair = Convert-BackendOptionBoolean $options.cuaRepair 'cuaRepair' $script:DefaultOptions.cuaRepair
        parentModel = Convert-BackendOptionString $options.parentModel 'parentModel' $script:DefaultOptions.parentModel -Model
        childModel = Convert-BackendOptionString $options.childModel 'childModel' $script:DefaultOptions.childModel -Model
        parentEffort = Convert-BackendOptionString $options.parentEffort 'parentEffort' $script:DefaultOptions.parentEffort
        childEffort = Convert-BackendOptionString $options.childEffort 'childEffort' $script:DefaultOptions.childEffort
    }
    return $normalized
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
        [int]$RequestedProxyPort,[switch]$RequestedInstallProtection,[string]$RequestedJobFile,
        [string]$RequestedOptionsBase64
    )
    $operation = $RequestedOperation
    $payloadRoot = $RequestedPayloadRoot
    $drive = $RequestedDrive
    $proxyPort = $RequestedProxyPort
    $installProtection = [bool]$RequestedInstallProtection
    $optionsBase64 = $RequestedOptionsBase64
    $optionsValue = $null
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
        if (-not $script:OptionsWasBound) {
            $jobOptionsBase64 = Get-ObjectPropertyValue $job 'OptionsBase64'
            if ($null -ne $jobOptionsBase64) { $optionsBase64 = [string]$jobOptionsBase64 }
            $optionsValue = Get-ObjectPropertyValue $job 'Options'
        }
    }
    if ([string]::IsNullOrWhiteSpace($operation)) { throw 'Operation is required.' }
    $operation = $operation.Trim().ToLowerInvariant()
    $aliases = @{
        'init' = 'initialize'; 'one-click' = 'initialize'; 'oneclick' = 'initialize'
        'init-rollback-latest' = 'init-rollback'; 'cua' = 'cua-repair'; 'guard' = 'guard-install'
    }
    if ($aliases.ContainsKey($operation)) { $operation = $aliases[$operation] }
    if ($proxyPort -lt 0 -or $proxyPort -gt 65535) { throw 'ProxyPort must be between 0 and 65535.' }
    if (-not [string]::IsNullOrWhiteSpace($payloadRoot)) {
        try { $payloadRoot = [IO.Path]::GetFullPath($payloadRoot) } catch { throw ('Invalid PayloadRoot: ' + $payloadRoot) }
    }
    return [pscustomobject]@{
        Operation=$operation; PayloadRoot=$payloadRoot; Drive=$drive; ProxyPort=$proxyPort
        InstallProtection=$installProtection; JobFile=$RequestedJobFile; Options=(Convert-BackendOptions $optionsValue $optionsBase64)
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
function Set-InitConsoleProtocol {
    param([Parameter(Mandatory=$true)][string]$SourceText,[Parameter(Mandatory=$true)][string]$ScriptDirectory)
    [void][IO.Directory]::CreateDirectory($ScriptDirectory)
    # Keep the GBK batch source and every nested PowerShell writer on the same
    # byte protocol. Embedded scripts still read/write their data files as UTF-8.
    foreach ($tag in @('PMB64','CUAB64')) {
        $pattern = '(?m)^::' + $tag + ':[A-Za-z0-9+/=]+\r?\n(?:^::' + $tag + ':[A-Za-z0-9+/=]+\r?\n)*'
        $match = [regex]::Match($SourceText,$pattern)
        if (-not $match.Success) { continue }
        $parts = [regex]::Matches($match.Value, '(?m)^::' + $tag + ':([^\r\n]+)')
        $encoded = ($parts | ForEach-Object { $_.Groups[1].Value }) -join ''
        $code = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($encoded))
        $code = [regex]::Replace($code,'(?m)^([ \t]*)\[Console\]::OutputEncoding\s*=[^\r\n]*', '$1[Console]::OutputEncoding = [Text.Encoding]::GetEncoding(936)')
        $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($code))
        $chunks = for ($i = 0; $i -lt $encoded.Length; $i += 180) { '::' + $tag + ':' + $encoded.Substring($i,[Math]::Min(180,$encoded.Length-$i)) }
        $SourceText = $SourceText.Remove($match.Index,$match.Length).Insert($match.Index,($chunks -join "`r`n") + "`r`n")
    }
    $prefix = "`$ProgressPreference='SilentlyContinue'; [Console]::OutputEncoding=[Text.Encoding]::GetEncoding(936); `$OutputEncoding=[Console]::OutputEncoding;`n"
    $evaluator = [Text.RegularExpressions.MatchEvaluator]{
        param($match)
        $flags = $match.Groups[1].Value
        if ($flags -notmatch '(?i)-InputFormat\s+Text') { $flags += '-InputFormat Text ' }
        if ($flags -notmatch '(?i)-OutputFormat\s+Text') { $flags += '-OutputFormat Text ' }
        $code = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($match.Groups[2].Value))
        # EncodedCommand serializes the information stream to CLIXML on
        # redirected stderr even with OutputFormat Text. A BOM-marked script
        # file uses ordinary text streams and avoids cmd.exe's 8191-byte limit.
        $path = Join-Path $ScriptDirectory ('console-' + [Guid]::NewGuid().ToString('N') + '.ps1')
        [IO.File]::WriteAllText($path,$prefix + $code,[Text.UTF8Encoding]::new($true))
        return $flags + '-File "' + $path + '"'
    }
    $SourceText = [regex]::Replace($SourceText,'(?i)(powershell\.exe\s+[^\r\n]*?)-EncodedCommand\s+([A-Za-z0-9+/=]+)',$evaluator)
    return $SourceText.Replace('chcp 65001 >nul','chcp 936 >nul')
}
function Invoke-ChildProcess {
    param(
        [Parameter(Mandatory=$true)][string]$FilePath,[string[]]$Arguments=@(),
        [string]$WorkingDirectory,[hashtable]$Environment=@{},
        [System.Text.Encoding]$OutputEncoding=$script:Utf8NoBom,[switch]$ClearProxyEnvironment
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
    if ($ClearProxyEnvironment) {
        foreach ($key in @('HTTP_PROXY','HTTPS_PROXY','ALL_PROXY','http_proxy','https_proxy','all_proxy')) {
            [void]$psi.EnvironmentVariables.Remove($key)
        }
        $psi.EnvironmentVariables['NO_PROXY'] = 'localhost,127.0.0.1,::1'
        $psi.EnvironmentVariables['no_proxy'] = 'localhost,127.0.0.1,::1'
    }
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
        [AllowNull()][string]$DriveLetter,[Parameter(Mandatory=$true)][int]$ProxyPortValue,
        [AllowNull()][object]$Options
    )
    $rawSource = [IO.File]::ReadAllBytes($SourcePath)
    $sourceText = $script:InputEncoding.GetString($rawSource)
    if ($sourceText.Length -gt 0 -and $sourceText[0] -eq [char]0xFEFF) { $sourceText = $sourceText.Substring(1) }
    if ($sourceText.Contains([char]0xFFFD)) { throw 'init.cmd is not valid GBK text.' }
    $resolvedOptions = Convert-BackendOptions $Options
    $folderEnabled = [bool]$resolvedOptions.folderManagement
    $subagentsEnabled = [bool]$resolvedOptions.subagents
    $cuaEnabled = [bool]$resolvedOptions.cuaRepair
    $proxyDisabled = ($ProxyPortValue -eq 0)
    $defaultProxyPort = if ($proxyDisabled) { '0' } else { '10808' }
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
    $proxySetup = 'set "PROXY_PORT=%CODEX_GUI_PROXY_PORT%"' + [Environment]::NewLine +
        ('if not defined PROXY_PORT set "PROXY_PORT=' + $defaultProxyPort + '"')
    $sourceText = $sourceText.Replace('set "PROXY_URL=http://127.0.0.1:%PROXY_PORT%"', 'if "%CODEX_GUI_PROXY_DISABLED%"=="1" (set "PROXY_URL=") else (set "PROXY_URL=http://127.0.0.1:%PROXY_PORT%")')
    $sourceText = $sourceText.Replace('set "PROXY_PORT=10808"', $proxySetup)
    $sourceText = $sourceText.Replace('set "DEFAULT_PARENT_MODEL_ID=gpt-6.1-sol"', 'set "DEFAULT_PARENT_MODEL_ID=%CODEX_GUI_PARENT_MODEL%"' + [Environment]::NewLine + 'if not defined DEFAULT_PARENT_MODEL_ID set "DEFAULT_PARENT_MODEL_ID=gpt-6.1-sol"')
    $sourceText = $sourceText.Replace('set "DEFAULT_PARENT_REASONING_EFFORT=medium"', 'set "DEFAULT_PARENT_REASONING_EFFORT=%CODEX_GUI_PARENT_EFFORT%"' + [Environment]::NewLine + 'if not defined DEFAULT_PARENT_REASONING_EFFORT set "DEFAULT_PARENT_REASONING_EFFORT=medium"')
    $sourceText = $sourceText.Replace('set "LUNA_MANAGER_INSTALLED=1"', 'set "LUNA_MANAGER_INSTALLED=%CODEX_GUI_SUBAGENTS%"' + [Environment]::NewLine + 'if not defined LUNA_MANAGER_INSTALLED set "LUNA_MANAGER_INSTALLED=1"')
    $sourceText = $sourceText.Replace('if "!LUNA_MANAGER_INSTALLED!"=="1" echo multi_agent = true', 'if "!LUNA_MANAGER_INSTALLED!"=="1" echo multi_agent = true' + [Environment]::NewLine + '    if "!LUNA_MANAGER_INSTALLED!"=="0" echo multi_agent = false')
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
        @('(?m)^call :FULL_INIT\r?$', 'echo @@PROGRESS@@{"step":2,"total":5,"title":"Run full initialization and backup"}' + [Environment]::NewLine + 'call :FULL_INIT'),
        @('(?m)^call :INSTALL_FOLDER_MANAGEMENT\r?$', 'if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" echo @@PROGRESS@@{"step":3,"total":5,"title":"Install folder management and binding"}' + [Environment]::NewLine + 'call :INSTALL_FOLDER_MANAGEMENT'),
        @('(?m)^call :RUN_CUA_REPAIR\r?$', 'if /i "%CODEX_GUI_CUA_REPAIR%"=="1" echo @@PROGRESS@@{"step":4,"total":5,"title":"Check and repair CUA runtime"}' + [Environment]::NewLine + 'call :RUN_CUA_REPAIR'),
        @('(?m)^call :INSTALL_LUNA_PROMPT\r?$', 'if /i "%CODEX_GUI_SUBAGENTS%"=="1" echo @@PROGRESS@@{"step":5,"total":5,"title":"Install and verify Luna configuration"}' + [Environment]::NewLine + 'call :INSTALL_LUNA_PROMPT')
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
    $oneClickDefaults = @"
:ONE_CLICK_INIT
set "FOLDER_RESULT=PASS"
set "CUA_RESULT=PASS"
set "LUNA_PROMPT_RESULT=PASS"
set "LUNA_PROMPT_COUNT=SKIPPED"
set "CODEX_FOLDER_DRIVE=%CODEX_GUI_DRIVE%"
"@
    $sourceText = [regex]::Replace($sourceText, '(?m)^:ONE_CLICK_INIT\r?$', $oneClickDefaults.TrimEnd())
    $sourceText = [regex]::Replace($sourceText, '(?m)^call :SELECT_FOLDER_DRIVE\r?$', 'if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" call :SELECT_FOLDER_DRIVE')
    $sourceText = [regex]::Replace($sourceText, '(?m)^call :INSTALL_FOLDER_MANAGEMENT\r?$', 'if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" call :INSTALL_FOLDER_MANAGEMENT')
    $sourceText = [regex]::Replace($sourceText, '(?m)^call :VERIFY_FOLDER_BINDING\r?$', 'if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" call :VERIFY_FOLDER_BINDING')
    $sourceText = [regex]::Replace($sourceText, '(?m)^call :RUN_CUA_REPAIR\r?$', 'if /i "%CODEX_GUI_CUA_REPAIR%"=="1" call :RUN_CUA_REPAIR')
    $sourceText = [regex]::Replace($sourceText, '(?m)^call :INSTALL_LUNA_PROMPT\r?$', 'if /i "%CODEX_GUI_SUBAGENTS%"=="1" call :INSTALL_LUNA_PROMPT')
    $sourceText = [regex]::Replace($sourceText,
        '(?ms)^if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" call :SELECT_FOLDER_DRIVE\r?\nif errorlevel 1 goto :ONE_CLICK_INIT_FAIL',
        @'
if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" (
    call :SELECT_FOLDER_DRIVE
    if errorlevel 1 goto :ONE_CLICK_INIT_FAIL
) else (
    set "SELECT_DRIVE_RC=0"
)
'@.TrimEnd())
    $sourceText = [regex]::Replace($sourceText,
        '(?ms)^if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" call :INSTALL_FOLDER_MANAGEMENT\r?\nif errorlevel 1 \(.*?^\)\r?\nif /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" call :VERIFY_FOLDER_BINDING\r?\nif errorlevel 1 \(.*?^\)',
        @'
if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" (
    call :INSTALL_FOLDER_MANAGEMENT
    if errorlevel 1 (
        echo 文件夹管理安装失败，已停止 CUA 修复和多线程安装。
        goto :ONE_CLICK_INIT_FAIL
    )
    call :VERIFY_FOLDER_BINDING
    if errorlevel 1 (
        set "FOLDER_RESULT=FAIL"
        echo Folder-management and projectless-task-folder binding verification failed.
        goto :ONE_CLICK_INIT_FAIL
    )
)
'@.TrimEnd())
    $sourceText = [regex]::Replace($sourceText,
        '(?ms)^if /i "%CODEX_GUI_CUA_REPAIR%"=="1" call :RUN_CUA_REPAIR\r?\nif errorlevel 1 \(.*?^\)\r?\n\r?\necho 阶段 5/5',
        @'
if /i "%CODEX_GUI_CUA_REPAIR%"=="1" (
    call :RUN_CUA_REPAIR
    if errorlevel 1 (
        echo CUA 运行时检查或修复失败，已停止后续安装。
        set "RESULT=FAIL"
        call :WRITE_REPORT "一键初始化 - CUA 检查或修复失败"
        call :WRITE_ROLLBACK
        goto :ONE_CLICK_INIT_FAIL
    )
)

echo 阶段 5/5
'@.TrimEnd())
    $sourceText = [regex]::Replace($sourceText,
        '(?ms)^if /i "%CODEX_GUI_SUBAGENTS%"=="1" call :INSTALL_LUNA_PROMPT\r?\nif errorlevel 1 \(.*?^\)\r?\n\r?\nset "CONFIG_RESULT=FAIL"',
        @'
if /i "%CODEX_GUI_SUBAGENTS%"=="1" (
    call :INSTALL_LUNA_PROMPT
    if errorlevel 1 (
        echo Luna 多线程提示词安装失败。
        goto :ONE_CLICK_INIT_FAIL
    )
)

set "CONFIG_RESULT=FAIL"
'@.TrimEnd())
    # Keep the stage calls and their failure checks together after all marker
    # edits.  This also makes a skipped stage independent of a stale errorlevel.
    $sourceText = [regex]::Replace($sourceText,
        '(?ms)^(if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" echo @@PROGRESS@@\{"step":3.*?\})\r?\n.*?(?=^echo 阶段 4/5)',
        '$1' + [Environment]::NewLine + 'if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" call :INSTALL_FOLDER_MANAGEMENT' + [Environment]::NewLine + 'if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" if errorlevel 1 (' + [Environment]::NewLine + '    echo Folder-management stage failed.' + [Environment]::NewLine + '    goto :ONE_CLICK_INIT_FAIL' + [Environment]::NewLine + ')' + [Environment]::NewLine + 'if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" call :VERIFY_FOLDER_BINDING' + [Environment]::NewLine + 'if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" if errorlevel 1 goto :ONE_CLICK_INIT_FAIL' + [Environment]::NewLine)
    $sourceText = [regex]::Replace($sourceText,
        '(?ms)^(if /i "%CODEX_GUI_CUA_REPAIR%"=="1" echo @@PROGRESS@@\{"step":4.*?\})\r?\n.*?(?=^echo 阶段 5/5)',
        '$1' + [Environment]::NewLine + 'if /i "%CODEX_GUI_CUA_REPAIR%"=="1" call :RUN_CUA_REPAIR' + [Environment]::NewLine + 'if /i "%CODEX_GUI_CUA_REPAIR%"=="1" if errorlevel 1 (' + [Environment]::NewLine + '    echo CUA 运行时检查或修复失败，已停止后续安装。' + [Environment]::NewLine + '    set "RESULT=FAIL"' + [Environment]::NewLine + '    call :WRITE_REPORT "一键初始化 - CUA 检查或修复失败"' + [Environment]::NewLine + '    call :WRITE_ROLLBACK' + [Environment]::NewLine + '    goto :ONE_CLICK_INIT_FAIL' + [Environment]::NewLine + ')' + [Environment]::NewLine)
    $sourceText = [regex]::Replace($sourceText,
        '(?ms)^(if /i "%CODEX_GUI_SUBAGENTS%"=="1" echo @@PROGRESS@@\{"step":5.*?\})\r?\n.*?(?=^set "CONFIG_RESULT=FAIL")',
        '$1' + [Environment]::NewLine + 'if /i "%CODEX_GUI_SUBAGENTS%"=="1" call :INSTALL_LUNA_PROMPT' + [Environment]::NewLine + 'if /i "%CODEX_GUI_SUBAGENTS%"=="1" if errorlevel 1 (' + [Environment]::NewLine + '    echo Luna 多线程提示词安装失败.' + [Environment]::NewLine + '    goto :ONE_CLICK_INIT_FAIL' + [Environment]::NewLine + ')' + [Environment]::NewLine)
    $sourceText = [regex]::Replace($sourceText,
        '(?m)^(echo @@PROGRESS@@\{"step":2.*?\})\r?\n(?!call :FULL_INIT)',
        '$1' + [Environment]::NewLine + 'call :FULL_INIT' + [Environment]::NewLine)
    if ($sourceText -match '(?m)^:WRITE_ENV\r?$') {
        $sourceText = $sourceText.Replace('echo 已写入：%ENV_FILE%', 'if "%CODEX_GUI_PROXY_DISABLED%"=="1" echo 已清理代理：%ENV_FILE%' + [Environment]::NewLine + 'if not "%CODEX_GUI_PROXY_DISABLED%"=="1" echo 已写入：%ENV_FILE%')
    }
    $workRoot = Join-Path $PayloadRoot '.backend'
    [void][IO.Directory]::CreateDirectory($workRoot)
    $adapterPath = Join-Path $workRoot ('init-adapter-' + [Guid]::NewGuid().ToString('N') + '.cmd')
    $sourceText = Set-InitConsoleProtocol $sourceText ([IO.Path]::ChangeExtension($adapterPath,'.scripts'))
    # cmd.exe stores byte offsets when CALL enters a label. Mixed LF/CRLF
    # introduced by PowerShell here-strings can corrupt the return position.
    $sourceText = [regex]::Replace($sourceText, '\r\n|\r|\n', "`r`n")
    [IO.File]::WriteAllText($adapterPath, $sourceText, $script:InputEncoding)
    return $adapterPath
}
function Read-Utf8FileSafe {
    param([Parameter(Mandatory=$true)][string]$Path)
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $null }
    try { $text = [IO.File]::ReadAllText($Path,(New-Object Text.UTF8Encoding($false,$true))) }
    catch { $text = [IO.File]::ReadAllText($Path) }
    if ($text.Length -gt 0 -and $text[0] -eq [char]0xFEFF) { return $text.Substring(1) }
    return $text
}
function Remove-InitAdapter {
    param([AllowNull()][string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { return }
    $fullPath = [IO.Path]::GetFullPath($Path)
    $parent = [IO.Path]::GetDirectoryName($fullPath)
    if ([IO.Path]::GetFileName($parent) -ne '.backend' -or [IO.Path]::GetFileName($fullPath) -notmatch '^init-adapter-[a-f0-9]{32}\.cmd$') { throw 'Unexpected initialization adapter cleanup path.' }
    $scriptsPath = [IO.Path]::ChangeExtension($fullPath,'.scripts')
    if (Test-Path -LiteralPath $scriptsPath -PathType Container) { Remove-Item -LiteralPath $scriptsPath -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $fullPath -PathType Leaf) { Remove-Item -LiteralPath $fullPath -Force -ErrorAction SilentlyContinue }
}
function Write-Utf8NoBomFile {
    param([Parameter(Mandatory=$true)][string]$Path,[Parameter(Mandatory=$true)][string]$Text)
    $parent = Split-Path -Parent $Path
    if ($parent) { [void][IO.Directory]::CreateDirectory($parent) }
    [IO.File]::WriteAllText($Path,$Text,$script:Utf8NoBom)
}
function Set-ProxyEnvironmentFile {
    param([Parameter(Mandatory=$true)][int]$ProxyPortValue,[AllowNull()][string]$OriginalText)
    $path = Join-Path (Get-CodexHomePath) '.env'
    $current = if ($null -ne $OriginalText) { $OriginalText } else { Read-Utf8FileSafe $path }
    $lines = if ($null -eq $current) { @() } else { @($current -split "\r?\n") }
    $kept = @($lines | Where-Object { $_ -notmatch '(?i)^\s*(HTTP_PROXY|HTTPS_PROXY|ALL_PROXY|NO_PROXY)\s*=' })
    if ($ProxyPortValue -gt 0) {
        $proxy = 'http://127.0.0.1:' + $ProxyPortValue
        $kept += 'HTTP_PROXY=' + $proxy
        $kept += 'HTTPS_PROXY=' + $proxy
    }
    $kept += 'NO_PROXY=localhost,127.0.0.1,::1'
    $text = (($kept | Where-Object { $null -ne $_ }) -join [Environment]::NewLine).TrimEnd() + [Environment]::NewLine
    Write-Utf8NoBomFile $path $text
    return $path
}
function Set-TransientProxyEnvironment {
    param([Parameter(Mandatory=$true)][int]$ProxyPortValue)
    $names = @('HTTP_PROXY','HTTPS_PROXY','ALL_PROXY','NO_PROXY','http_proxy','https_proxy','all_proxy','no_proxy')
    $snapshot = [ordered]@{}
    $snapshot['__DefaultWebProxy'] = [Net.WebRequest]::DefaultWebProxy
    if ($ProxyPortValue -eq 0) { [Net.WebRequest]::DefaultWebProxy = $null }
    foreach ($name in $names) { $snapshot[$name] = [Environment]::GetEnvironmentVariable($name,'Process') }
    foreach ($name in @('HTTP_PROXY','HTTPS_PROXY','ALL_PROXY','http_proxy','https_proxy','all_proxy')) {
        [Environment]::SetEnvironmentVariable($name,$null,'Process')
    }
    if ($ProxyPortValue -eq 0) {
        [Environment]::SetEnvironmentVariable('NO_PROXY','localhost,127.0.0.1,::1','Process')
        [Environment]::SetEnvironmentVariable('no_proxy','localhost,127.0.0.1,::1','Process')
    } else {
        $proxy = 'http://127.0.0.1:' + $ProxyPortValue
        [Environment]::SetEnvironmentVariable('HTTP_PROXY',$proxy,'Process')
        [Environment]::SetEnvironmentVariable('HTTPS_PROXY',$proxy,'Process')
        [Environment]::SetEnvironmentVariable('ALL_PROXY',$proxy,'Process')
    }
    return $snapshot
}
function Restore-TransientProxyEnvironment {
    param([Parameter(Mandatory=$true)][System.Collections.IDictionary]$Snapshot)
    foreach ($name in $Snapshot.Keys) { if ($name -eq '__DefaultWebProxy') { [Net.WebRequest]::DefaultWebProxy = $Snapshot[$name] } else { [Environment]::SetEnvironmentVariable([string]$name,$Snapshot[$name],'Process') } }
}
function Set-ScopedModelSettings {
    param([AllowNull()][object]$Options)
    $resolved = Convert-BackendOptions $Options
    $codeHome = Get-CodexHomePath
    $configPath = Join-Path $codeHome 'config.toml'
    if (Test-Path -LiteralPath $configPath -PathType Leaf) {
        $config = Read-Utf8FileSafe $configPath
        if ($null -ne $config) {
            $lines = @($config -split "\r?\n")
            $section = ''
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -match '^\s*\[([^\]]+)\]\s*$') { $section = $Matches[1]; continue }
                if ([string]::IsNullOrEmpty($section) -and $lines[$i] -match '^(\s*model\s*=\s*)(.*?)(\s*(?:#.*)?)$') {
                    $lines[$i] = $Matches[1] + '"' + [string]$resolved.parentModel + '"' + $Matches[3]
                } elseif ([string]::IsNullOrEmpty($section) -and $lines[$i] -match '^(\s*model_reasoning_effort\s*=\s*)(.*?)(\s*(?:#.*)?)$') {
                    $lines[$i] = $Matches[1] + '"' + [string]$resolved.parentEffort + '"' + $Matches[3]
                }
            }
            Write-Utf8NoBomFile $configPath (($lines -join [Environment]::NewLine).TrimEnd() + [Environment]::NewLine)
        }
    }
    if ([bool]$resolved.subagents) {
        $agentsPath = Join-Path $codeHome 'AGENTS.md'
        $agents = Read-Utf8FileSafe $agentsPath
        if ($null -ne $agents) {
            $pattern = '(?ms)^[ \t]*BEGIN CODEX LUNA PROMPT V[^\r\n]*\r?\n.*?^[ \t]*END CODEX LUNA PROMPT V[^\r\n]*$'
            $evaluator = [System.Text.RegularExpressions.MatchEvaluator]{
                param($match)
                $block = $match.Value
                $block = [regex]::Replace($block,'(?m)^([ \t]*model\s*=\s*).*$',[System.Text.RegularExpressions.MatchEvaluator]{ param($m) $m.Groups[1].Value + [string]$resolved.childModel })
                $block = [regex]::Replace($block,'(?m)^([ \t]*model_reasoning_effort\s*=\s*).*$',[System.Text.RegularExpressions.MatchEvaluator]{ param($m) $m.Groups[1].Value + [string]$resolved.childEffort })
                $block = $block.Replace('This is the GPT5.6 LUNA MAX child configuration. Do not substitute another child model or reasoning effort.', 'Use the configured child model and reasoning effort above for temporary child agents.')
                $block = $block.Replace('Do not create Sol, Terra, GPT-6, Astra, or any other non-Luna child under this protocol.', 'Use the configured child model above; do not substitute another child model.')
                $block = $block.Replace('gpt-5.6-luna',[string]$resolved.childModel)
                $block = $block.Replace('GPT5.6 LUNA MAX',([string]$resolved.childModel + ' ' + [string]$resolved.childEffort))
                $block = $block.Replace('GPT5.6 LUNA',[string]$resolved.childModel)
                return $block
            }
            $updated = [regex]::Replace($agents,$pattern,$evaluator)
            if ($updated -ne $agents) { Write-Utf8NoBomFile $agentsPath $updated }
        }
    }
}
function Invoke-InitOperation {
    param([Parameter(Mandatory=$true)][string]$Action,[Parameter(Mandatory=$true)][string]$PayloadRoot,
          [string]$DriveLetter,[int]$ProxyPortValue=10808,[AllowNull()][object]$Options)
    $init = Assert-PayloadFile $PayloadRoot 'init.cmd'
    $drive = $null
    if ($Action -eq 'one-click') { $drive = Normalize-DriveLetter $DriveLetter }
    $resolvedOptions = Convert-BackendOptions $Options
    $envFile = Join-Path (Get-CodexHomePath) '.env'
    $originalEnv = Read-Utf8FileSafe $envFile
    $adapter = $null
    try {
        $adapter = New-InitAdapter $init $PayloadRoot $drive $ProxyPortValue $resolvedOptions
        if ($Action -eq 'one-click') {
            if ([bool]$resolvedOptions.folderManagement) { Write-BackendProgress 1 5 'Confirm folder-management drive' }
            else { Write-BackendProgress 1 5 'Skip folder-management drive confirmation' }
        } else { Write-BackendProgress 1 1 'Rebuild proxy configuration' }
        $childEnvironment = @{
            CODEX_GUI_DRIVE=$drive; CODEX_GUI_PROXY_PORT=[string]$ProxyPortValue; CODEX_GUI_NO_PAUSE='1'
            CODEX_GUI_PROXY_DISABLED=if ($ProxyPortValue -eq 0) { '1' } else { '0' }
            CODEX_GUI_FOLDER_MANAGEMENT=if ([bool]$resolvedOptions.folderManagement) { '1' } else { '0' }
            CODEX_GUI_SUBAGENTS=if ([bool]$resolvedOptions.subagents) { '1' } else { '0' }
            CODEX_GUI_CUA_REPAIR=if ([bool]$resolvedOptions.cuaRepair) { '1' } else { '0' }
            CODEX_GUI_PARENT_MODEL=[string]$resolvedOptions.parentModel; CODEX_GUI_CHILD_MODEL=[string]$resolvedOptions.childModel
            CODEX_GUI_PARENT_EFFORT=[string]$resolvedOptions.parentEffort; CODEX_GUI_CHILD_EFFORT=[string]$resolvedOptions.childEffort
            CODEX_GUI_ENV_FILE=$envFile; CODEX_GUI_PROXY_URL=if ($ProxyPortValue -gt 0) { 'http://127.0.0.1:' + $ProxyPortValue } else { '' }
        }
        $result = Invoke-ChildProcess $adapter @('--codex-gui-action',$Action) $PayloadRoot $childEnvironment $script:InputEncoding -ClearProxyEnvironment:($ProxyPortValue -eq 0)
        if ($result.ExitCode -ne 0) { throw ('init.cmd failed with exit code ' + $result.ExitCode) }
        if ($result.Output -match '(?im)RESULT\s*=\s*FAIL|FAIL') { throw 'init.cmd reported a failed result.' }
        if ($Action -eq 'proxy' -or $Action -eq 'one-click') { [void](Set-ProxyEnvironmentFile $ProxyPortValue $originalEnv) }
        if ($Action -eq 'one-click') { Set-ScopedModelSettings $resolvedOptions }
        if ($Action -eq 'proxy') {
            $envFile = Join-Path (Get-CodexHomePath) '.env'
            if (-not (Test-Path -LiteralPath $envFile -PathType Leaf)) { throw '.env was not written by proxy operation.' }
            $envText = [IO.File]::ReadAllText($envFile)
            $proxy = 'http://127.0.0.1:' + $ProxyPortValue
            if ($ProxyPortValue -eq 0) {
                if ($envText -match '(?im)^\s*(HTTP_PROXY|HTTPS_PROXY|ALL_PROXY)\s*=') { throw 'Proxy operation did not remove proxy environment entries.' }
            } elseif ($envText -notmatch ('(?m)^HTTP_PROXY=' + [regex]::Escape($proxy) + '$') -or
                $envText -notmatch ('(?m)^HTTPS_PROXY=' + [regex]::Escape($proxy) + '$')) { throw 'Proxy operation did not write the requested proxy endpoint.' }
        }
        return 0
    } finally {
        Remove-InitAdapter $adapter
    }
}
function Resolve-InitRollbackPath {
    $latest = Get-LatestBackupPath
    if (-not $latest) { throw 'No initialization backup session is available for rollback.' }
    return $latest
}
function Invoke-InitRollback {
    param([Parameter(Mandatory=$true)][string]$PayloadRoot,[int]$ProxyPortValue=0)
    $init = Assert-PayloadFile $PayloadRoot 'init.cmd'
    $rollback = Resolve-InitRollbackPath
    $adapter = $null
    try {
        $adapter = New-InitAdapter $init $PayloadRoot '' $ProxyPortValue
        Write-BackendProgress 1 1 'Rollback latest initialization backup'
        $result = Invoke-ChildProcess $adapter @('--codex-gui-rollback') $PayloadRoot @{
            CODEX_GUI_ROLLBACK_PATH=$rollback; CODEX_GUI_NO_PAUSE='1'; CODEX_GUI_PROXY_PORT=[string]$ProxyPortValue
            CODEX_GUI_PROXY_DISABLED=if ($ProxyPortValue -eq 0) { '1' } else { '0' }
        } $script:InputEncoding -ClearProxyEnvironment:($ProxyPortValue -eq 0)
        if ($result.ExitCode -ne 0) { throw ('init rollback failed with exit code ' + $result.ExitCode) }
        if ($result.Output -match '(?im)FAIL|rollback.*failed') { throw 'init rollback reported a failure.' }
        return 0
    } finally {
        Remove-InitAdapter $adapter
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
    # GUI workers already receive the selected proxy through their process
    # environment. Do not reload an older .env value over the GUI selection.
    Set-Item -Path 'Function:\script:Load-ProxyEnvironment' -Value { }
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
    param([string]$RequestedOperation,[string]$RequestedPayloadRoot,[string]$RequestedDrive,[int]$RequestedProxyPort=10808,[switch]$RequestedInstallProtection,[string]$RequestedJobFile,[string]$RequestedOptionsBase64)
    Set-BackendOutputEncoding
    $proxySnapshot = $null
    try {
        $input=Resolve-BackendInput -RequestedOperation $RequestedOperation -RequestedPayloadRoot $RequestedPayloadRoot -RequestedDrive $RequestedDrive -RequestedProxyPort $RequestedProxyPort -RequestedInstallProtection:$RequestedInstallProtection.IsPresent -RequestedJobFile $RequestedJobFile -RequestedOptionsBase64 $RequestedOptionsBase64
        if ($input.Operation -ne 'status') { $proxySnapshot = Set-TransientProxyEnvironment $input.ProxyPort }
        switch($input.Operation){
            'status' { Write-BackendStatus (Get-ReadOnlyStatus $input.PayloadRoot); return 0 }
            'initialize' { [void](Invoke-InitOperation 'one-click' $input.PayloadRoot $input.Drive $input.ProxyPort $input.Options); if($input.InstallProtection){[void](Invoke-GuardOperation 'guard-install' $input.PayloadRoot)}; return 0 }
            'proxy' { return (Invoke-InitOperation 'proxy' $input.PayloadRoot $null $input.ProxyPort $input.Options) }
            'init-rollback' { return (Invoke-InitRollback $input.PayloadRoot $input.ProxyPort) }
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
    } finally {
        if ($null -ne $proxySnapshot) { Restore-TransientProxyEnvironment $proxySnapshot }
    }
}
# Dot-sourcing exposes helpers for adapter tests without starting a worker.
if ($MyInvocation.InvocationName -ne '.') {
    exit (Invoke-BackendMain -RequestedOperation $Operation -RequestedPayloadRoot $PayloadRoot -RequestedDrive $Drive -RequestedProxyPort $ProxyPort -RequestedInstallProtection:$InstallProtection.IsPresent -RequestedJobFile $JobFile -RequestedOptionsBase64 $OptionsBase64)
}
