@echo off
setlocal DisableDelayedExpansion
chcp 65001 >nul
rem Codex CUA recovery. Save work and fully exit Codex before using.
rem Default: verify and repair if needed. /check: verification only.
rem Backups stay beside the runtime; reports go under D:\Codex\Temp.
set "CODEX_CUA_FIX_SELF=%~f0"
set "CODEX_CUA_FIX_MODE=repair"
set "CODEX_CUA_FIX_NO_PAUSE="
if not "%~1"=="" if /i not "%~1"=="/check" if /i not "%~1"=="/nopause" goto usage
if not "%~2"=="" if /i not "%~2"=="/check" if /i not "%~2"=="/nopause" goto usage
if not "%~3"=="" goto usage
if /i "%~1"=="/check" set "CODEX_CUA_FIX_MODE=check"
if /i "%~2"=="/check" set "CODEX_CUA_FIX_MODE=check"
if /i "%~1"=="/nopause" set "CODEX_CUA_FIX_NO_PAUSE=1"
if /i "%~2"=="/nopause" set "CODEX_CUA_FIX_NO_PAUSE=1"
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "$text=[IO.File]::ReadAllText($env:CODEX_CUA_FIX_SELF,[Text.Encoding]::UTF8);$marker='#==POWERSHELL==';$at=$text.LastIndexOf($marker);if($at -lt 0){exit 90};& ([scriptblock]::Create($text.Substring($at+$marker.Length)))"
set "CODEX_CUA_FIX_EXIT=%errorlevel%"
if defined CODEX_CUA_FIX_NO_PAUSE goto finished
echo.
pause
:finished
exit /b %CODEX_CUA_FIX_EXIT%
:usage
echo Usage: Codex_CUA_Repair.cmd [/check] [/nopause]
exit /b 64
#==POWERSHELL==
param([switch]$LibraryOnly)

$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
Set-StrictMode -Version 2.0
$script:LogFile = $null

function Write-RepairMessage {
    param([string]$Message, [ConsoleColor]$Color = 'Gray')
    Write-Host $Message -ForegroundColor $Color
    if ($script:LogFile) {
        try {
            Add-Content -LiteralPath $script:LogFile -Value ('[{0}] {1}' -f (Get-Date -Format 'HH:mm:ss'), $Message) -Encoding UTF8
        } catch {
            Write-Host ('日志暂时无法写入：' + $_.Exception.Message) -ForegroundColor Yellow
        }
    }
}

function Get-FileDigest {
    param([string]$Path)
    $stream = $null
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $stream = [IO.File]::Open($Path, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::Read)
        [long]$length = $stream.Length
        $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant()
        return [pscustomobject]@{ Hash = $hash; Length = $length }
    } finally {
        if ($stream) { $stream.Dispose() }
        $sha.Dispose()
    }
}

function Get-Sha256 {
    param([string]$Path)
    return (Get-FileDigest $Path).Hash
}

function Get-FullDirectoryPath {
    param([string]$Path)
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'Empty path is forbidden.' }
    $full = [IO.Path]::GetFullPath($Path)
    $volumeRoot = [IO.Path]::GetPathRoot($full)
    $trimmed = $full.TrimEnd([char[]]'\/')
    if ($trimmed.Length -lt $volumeRoot.Length) { return $volumeRoot }
    return $trimmed
}

function Assert-ContainedPath {
    param([string]$Path, [string]$Parent)
    $full = Get-FullDirectoryPath $Path
    $root = Get-FullDirectoryPath $Parent
    $prefix = $root.TrimEnd([char[]]'\/') + [IO.Path]::DirectorySeparatorChar
    if (-not $full.StartsWith($prefix, [StringComparison]::OrdinalIgnoreCase) -or $full.Equals($root, [StringComparison]::OrdinalIgnoreCase)) {
        throw ('Unsafe path outside the runtime root: ' + $full)
    }
    return $full
}

function Assert-NoReparsePoint {
    param([string]$Path, [switch]$Tree)
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $item = Get-Item -LiteralPath $Path -Force
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw ('Refusing a link/junction/reparse point: ' + $item.FullName)
    }
    if ($Tree -and $item.PSIsContainer) {
        foreach ($entry in @(Get-ChildItem -LiteralPath $Path -Recurse -Force)) {
            if (($entry.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw ('Refusing a link/junction/reparse point: ' + $entry.FullName)
            }
        }
    }
}

function Assert-SafeRuntimeRoot {
    param([string]$RuntimeRoot, [string]$LocalData)
    $expected = Get-FullDirectoryPath (Join-Path $LocalData 'OpenAI\Codex\runtimes\cua_node')
    $actual = Get-FullDirectoryPath $RuntimeRoot
    if (-not $actual.Equals($expected, [StringComparison]::OrdinalIgnoreCase)) { throw 'Runtime root does not match the expected cache path.' }
    $cursor = $actual
    $localRoot = Get-FullDirectoryPath $LocalData
    while ($cursor.StartsWith($localRoot + '\', [StringComparison]::OrdinalIgnoreCase)) {
        Assert-NoReparsePoint $cursor
        $cursor = Split-Path -Parent $cursor
    }
    Assert-NoReparsePoint $localRoot
}

function Get-TreeInventory {
    param([string]$Root)
    $rootPath = Get-FullDirectoryPath $Root
    if (-not (Test-Path -LiteralPath $rootPath -PathType Container)) { throw ('Directory missing: ' + $rootPath) }
    Assert-NoReparsePoint $rootPath -Tree
    $files = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::OrdinalIgnoreCase)
    $directories = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    [long]$bytes = 0
    foreach ($entry in @(Get-ChildItem -LiteralPath $rootPath -Recurse -Force)) {
        $relative = $entry.FullName.Substring($rootPath.Length + 1)
        if ($entry.PSIsContainer) {
            [void]$directories.Add($relative)
        } else {
            $digest = Get-FileDigest $entry.FullName
            $files.Add($relative, $digest)
            $bytes += $digest.Length
        }
    }
    return [pscustomobject]@{ Root = $rootPath; Files = $files; Directories = $directories; Count = $files.Count; Bytes = $bytes }
}

function Compare-TreeInventory {
    param($Expected, [string]$Target)
    if (-not (Test-Path -LiteralPath $Target -PathType Container)) {
        return [pscustomobject]@{ Passed = $false; Reason = 'Target directory is missing.' }
    }
    $actual = Get-TreeInventory $Target
    if ($actual.Count -ne $Expected.Count) {
        return [pscustomobject]@{ Passed = $false; Reason = ('File count differs: expected {0}, actual {1}.' -f $Expected.Count, $actual.Count) }
    }
    if (-not $actual.Directories.SetEquals($Expected.Directories)) {
        return [pscustomobject]@{ Passed = $false; Reason = 'Directory set differs.' }
    }
    foreach ($relative in $Expected.Files.Keys) {
        if (-not $actual.Files.ContainsKey($relative)) {
            return [pscustomobject]@{ Passed = $false; Reason = ('Missing file: ' + $relative) }
        }
        $a = $actual.Files[$relative]
        $e = $Expected.Files[$relative]
        if ($a.Length -ne $e.Length -or $a.Hash -ne $e.Hash) {
            return [pscustomobject]@{ Passed = $false; Reason = ('File content differs: ' + $relative) }
        }
    }
    return [pscustomobject]@{ Passed = $true; Reason = 'All file paths, directory paths, lengths and SHA256 hashes match.' }
}

function Copy-RuntimeTree {
    param([string]$Source, [string]$Target, [string]$CopyLog)
    [void][IO.Directory]::CreateDirectory($Target)
    $xcopy = Join-Path $env:SystemRoot 'System32\xcopy.exe'
    if (-not (Test-Path -LiteralPath $xcopy -PathType Leaf)) { throw 'System XCOPY is unavailable.' }
    $arguments = @((Join-Path $Source '*'), ($Target.TrimEnd('\') + '\'), '/E', '/I', '/H', '/Y', '/R', '/G', '/Q')
    $oldPreference = $ErrorActionPreference
    $copyExit = -1
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& $xcopy @arguments 2>&1)
        $copyExit = $LASTEXITCODE
    } finally {
        $ErrorActionPreference = $oldPreference
    }
    try { $output | Out-File -LiteralPath $CopyLog -Encoding UTF8 } catch { Write-RepairMessage ('XCOPY 日志无法保存：' + $_.Exception.Message) 'Yellow' }
    Write-RepairMessage ('XCOPY exit code: ' + $copyExit)
    if ($copyExit -ne 0) { throw ('XCOPY failed. Exit code: {0}. Log: {1}' -f $copyExit, $CopyLog) }
}

function Move-RuntimePath {
    param([string]$Source, [string]$Destination, [string]$RuntimeRoot)
    $sourcePath = Assert-ContainedPath $Source $RuntimeRoot
    $destinationPath = Assert-ContainedPath $Destination $RuntimeRoot
    Assert-NoReparsePoint $RuntimeRoot
    Assert-NoReparsePoint $sourcePath -Tree
    if (Test-Path -LiteralPath $destinationPath) { throw ('Move destination already exists: ' + $destinationPath) }
    $sourceWasDirectory = (Get-Item -LiteralPath $sourcePath -Force).PSIsContainer
    Move-Item -LiteralPath $sourcePath -Destination $destinationPath
    if ((Test-Path -LiteralPath $sourcePath) -or -not (Test-Path -LiteralPath $destinationPath)) { throw 'Move postcondition failed. Please retain both paths for manual recovery.' }
    if ((Get-Item -LiteralPath $destinationPath -Force).PSIsContainer -ne $sourceWasDirectory) { throw 'Move destination has an unexpected item type.' }
}

function Invoke-RuntimeRepair {
    param($SourceInventory, [string]$Target, [string]$RuntimeRoot, [string]$RunDirectory, [scriptblock]$CopyAction, [string]$InstallLocation)
    $targetPath = Assert-ContainedPath $Target $RuntimeRoot
    if ((Split-Path -Leaf $targetPath) -notmatch '^[0-9a-f]{16}$') { throw 'Invalid runtime ID.' }
    Assert-NoReparsePoint $RuntimeRoot
    Assert-NoReparsePoint $targetPath -Tree
    if ($targetPath.Equals($SourceInventory.Root, [StringComparison]::OrdinalIgnoreCase)) { throw 'Source and target must differ.' }
    if ($targetPath.StartsWith($SourceInventory.Root + '\', [StringComparison]::OrdinalIgnoreCase) -or $SourceInventory.Root.StartsWith($targetPath + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Source and target must not contain each other.' }
    $initial = Compare-TreeInventory $SourceInventory $targetPath
    if ($initial.Passed) { return [pscustomobject]@{ Changed = $false; Backup = $null; Quarantine = $null } }
    if ($InstallLocation) { Confirm-CodexStopped $InstallLocation $RuntimeRoot }
    [void][IO.Directory]::CreateDirectory($RuntimeRoot)
    $suffix = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $id = Split-Path -Leaf $targetPath
    $backup = $null
    $quarantine = $null
    if (Test-Path -LiteralPath $targetPath) {
        $backup = Assert-ContainedPath (Join-Path $RuntimeRoot ('.backup-' + $id + '-' + $suffix)) $RuntimeRoot
        if (Test-Path -LiteralPath $backup) { throw 'Backup path already exists.' }
        Write-RepairMessage ('备份原运行时：' + $backup)
        Move-RuntimePath $targetPath $backup $RuntimeRoot
    }
    try {
        if ($InstallLocation) { Confirm-CodexStopped $InstallLocation $RuntimeRoot }
        $copyLog = Join-Path $RunDirectory 'XCOPY.log'
        if ($CopyAction) { & $CopyAction $SourceInventory.Root $targetPath $copyLog } else { Copy-RuntimeTree $SourceInventory.Root $targetPath $copyLog }
        $verified = Compare-TreeInventory $SourceInventory $targetPath
        if (-not $verified.Passed) { throw ('Verification failed: ' + $verified.Reason) }
        $sourceNow = Compare-TreeInventory $SourceInventory $SourceInventory.Root
        if (-not $sourceNow.Passed) { throw 'Official source changed during repair. Please run the tool again after the app update finishes.' }
        return [pscustomobject]@{ Changed = $true; Backup = $backup; Quarantine = $null }
    } catch {
        $originalError = $_.Exception.Message
        Write-RepairMessage ('修复失败：' + $originalError) 'Red'
        try {
            if (Test-Path -LiteralPath $targetPath) {
                $quarantine = Assert-ContainedPath (Join-Path $RuntimeRoot ('.failed-' + $id + '-' + $suffix)) $RuntimeRoot
                if (Test-Path -LiteralPath $quarantine) { throw 'Quarantine path already exists.' }
                Assert-NoReparsePoint $targetPath -Tree
                Move-RuntimePath $targetPath $quarantine $RuntimeRoot
                Write-RepairMessage ('保留未完成文件：' + $quarantine)
            }
            if ($backup) {
                Assert-NoReparsePoint $backup -Tree
                [void](Assert-ContainedPath $backup $RuntimeRoot)
                Move-RuntimePath $backup $targetPath $RuntimeRoot
                Write-RepairMessage '原运行时已恢复。' 'Yellow'
            }
        } catch {
            throw ('REPAIR_AND_ROLLBACK_FAILED: {0}; rollback: {1}; backup: {2}; incomplete target: {3}' -f $originalError, $_.Exception.Message, $backup, $targetPath)
        }
        throw ('REPAIR_FAILED: ' + $originalError)
    }
}

function Get-CodexProcesses {
    param([string]$InstallLocation, [string]$RuntimeRoot)
    $packageRoot = Get-FullDirectoryPath $InstallLocation
    $cacheRoot = Get-FullDirectoryPath $RuntimeRoot
    foreach ($process in @(Get-Process -Name ChatGPT,codex,cua_node,node,node_repl,cua-helper -ErrorAction SilentlyContinue)) {
        $path = $null
        try { $path = $process.Path } catch { }
        if ($path) {
            if ($path.StartsWith($packageRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or $path.StartsWith($cacheRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or $path -match '\\WindowsApps\\OpenAI\.Codex_[^\\]+\\') {
                $process
            }
        } else {
            $process
        }
    }
}

function Confirm-CodexStopped {
    param([string]$InstallLocation, [string]$RuntimeRoot)
    if (@(Get-CodexProcesses $InstallLocation $RuntimeRoot).Count -gt 0) { throw 'CODEX_STILL_RUNNING: Codex reopened or a runtime process is still running. Exit it and run the tool again.' }
}

function Wait-CodexExit {
    param([string]$InstallLocation, [string]$RuntimeRoot, [int]$TimeoutSeconds = 180)
    $running = @(Get-CodexProcesses $InstallLocation $RuntimeRoot)
    if ($running.Count -eq 0) { return $false }
    Write-RepairMessage ('检测到正在运行的 Codex 相关进程：' + (($running | ForEach-Object { '{0}({1})' -f $_.ProcessName, $_.Id }) -join ', ')) 'Yellow'
    Write-RepairMessage '请保存未完成工作，然后从系统托盘彻底退出 Codex。退出后本窗口会自动继续。' 'Yellow'
    $watch = [Diagnostics.Stopwatch]::StartNew()
    while (@(Get-CodexProcesses $InstallLocation $RuntimeRoot).Count -gt 0) {
        if ($watch.Elapsed.TotalSeconds -ge $TimeoutSeconds) { throw 'CODEX_STILL_RUNNING: Waited 180 seconds. Exit Codex and double-click this file again.' }
        Start-Sleep -Seconds 1
    }
    Write-RepairMessage 'Codex 已退出，继续修复。' 'Green'
    return $true
}

function Get-CurrentPackage {
    $packages = @(Get-AppxPackage -Name OpenAI.Codex)
    if ($packages.Count -ne 1) { throw ('Expected one registered OpenAI.Codex package; found ' + $packages.Count) }
    if (-not $packages[0].InstallLocation) { throw 'The registered package has no installation directory.' }
    return $packages[0]
}

function Get-AppStartupCode {
    param([string]$InstallLocation)
    $asarPath = Join-Path $InstallLocation 'app\resources\app.asar'
    $stream = $null
    $reader = $null
    try {
        $stream = [IO.File]::OpenRead($asarPath)
        $reader = [IO.BinaryReader]::new($stream)
        if ($reader.ReadUInt32() -ne 4) { throw 'Unsupported ASAR header.' }
        [long]$headerSize = $reader.ReadUInt32()
        [void]$reader.ReadUInt32()
        [int]$jsonSize = $reader.ReadUInt32()
        if ($headerSize -lt 8 -or $headerSize -gt 16777216 -or $jsonSize -le 0 -or $jsonSize -gt ($headerSize - 8)) { throw 'Invalid ASAR header size.' }
        $jsonBytes = $reader.ReadBytes($jsonSize)
        if ($jsonBytes.Length -ne $jsonSize) { throw 'Truncated ASAR header.' }
        $header = [Text.Encoding]::UTF8.GetString($jsonBytes) | ConvertFrom-Json
        $vite = $header.files.PSObject.Properties['.vite'].Value
        $build = $vite.files.PSObject.Properties['build'].Value
        $entries = @($build.files.PSObject.Properties | Where-Object { $_.Name -match '^application-network-startup-.*\.js$' })
        if ($entries.Count -ne 1) { throw 'Cannot uniquely identify the current runtime implementation.' }
        $entry = $entries[0].Value
        [long]$size = $entry.size
        if ($size -le 0 -or $size -gt 8388608) { throw 'Unsupported runtime implementation size.' }
        $unpackedProperty = $entry.PSObject.Properties['unpacked']
        if ($unpackedProperty -and $unpackedProperty.Value) {
            $unpackedRoot = $asarPath + '.unpacked'
            $unpackedFile = Assert-ContainedPath (Join-Path $unpackedRoot ('.vite\build\' + $entries[0].Name)) $unpackedRoot
            return [IO.File]::ReadAllText($unpackedFile, [Text.Encoding]::UTF8)
        }
        if ([string]$entry.offset -notmatch '^\d+$') { throw 'Invalid ASAR entry offset.' }
        [long]$offset = 8 + $headerSize + [long]$entry.offset
        if ($offset -lt 0 -or ($offset + $size) -gt $stream.Length) { throw 'ASAR entry is outside the archive.' }
        $stream.Position = $offset
        $bytes = $reader.ReadBytes([int]$size)
        if ($bytes.Length -ne $size) { throw 'Truncated runtime implementation.' }
        return [Text.Encoding]::UTF8.GetString($bytes)
    } finally {
        if ($reader) { $reader.Dispose() } elseif ($stream) { $stream.Dispose() }
    }
}

function Assert-KnownRuntimeAlgorithm {
    param([string]$Code)
    $quote = '[\x60\x22\x27]'
    $markers = '\[\s*' + $quote + 'manifest\.json' + $quote + '\s*,\s*' + $quote + 'bin/node\.exe' + $quote + '\s*,\s*' + $quote + 'bin/node_repl\.exe' + $quote + '\s*\]'
    $hashAlgorithm = 'function\s+(?<name>[$\w]+)\((?<list>[$\w]+)\)\{let\s+(?<hash>[$\w]+)=\(0,[$\w]+\.createHash\)\(' + $quote + 'sha256' + $quote + '\);for\(let\s+(?<item>[$\w]+)\s+of\s+\k<list>\)\k<hash>\.update\(\k<item>\.executableName\),\k<hash>\.update\(' + $quote + '\\0' + $quote + '\),\k<hash>\.update\(\k<item>\.digest\),\k<hash>\.update\(' + $quote + '\\0' + $quote + '\);return\s+\k<hash>\.digest\(' + $quote + 'hex' + $quote + '\)\}'
    $match = [regex]::Match($Code, $hashAlgorithm)
    if (-not [regex]::IsMatch($Code, $markers) -or -not $match.Success) { throw 'This app version changed its runtime identity algorithm. The tool stopped without modifying the runtime.' }
    $slice = [regex]::Escape($match.Groups['name'].Value) + '\([$\w]+\)\.slice\(0,16\)'
    $fileHash = 'createHash\)\(' + $quote + 'sha256' + $quote + '\)\.update\([\s\S]{0,400}?readFileSync\)[\s\S]{0,100}?\.digest\(' + $quote + 'hex' + $quote + '\)'
    if (-not [regex]::IsMatch($Code, $slice) -or -not [regex]::IsMatch($Code, $fileHash)) { throw 'Unrecognized runtime hash calculation. The tool stopped without modifying the runtime.' }
}

function Get-RuntimeIdFromSource {
    param([string]$Source)
    $combined = [Text.StringBuilder]::new()
    foreach ($relative in @('manifest.json', 'bin/node.exe', 'bin/node_repl.exe')) {
        $filePath = Join-Path $Source $relative
        [void]$combined.Append($relative)
        [void]$combined.Append([char]0)
        [void]$combined.Append((Get-Sha256 $filePath))
        [void]$combined.Append([char]0)
    }
    $sha = [Security.Cryptography.SHA256]::Create()
    try {
        $digest = $sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($combined.ToString()))
        return [BitConverter]::ToString($digest).Replace('-', '').ToLowerInvariant().Substring(0, 16)
    } finally { $sha.Dispose() }
}

function Get-RequiredRuntimeId {
    param([string]$Source, [string]$InstallLocation)
    Assert-KnownRuntimeAlgorithm (Get-AppStartupCode $InstallLocation)
    return Get-RuntimeIdFromSource $Source
}

function Invoke-RepairMain {
    $mutex = $null
    $acquired = $false
    try {
        [Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
        $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        $mutex = [Threading.Mutex]::new($false, ('Local\CodexCUARepair-' + $sid))
        try { $acquired = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $acquired = $true }
        if (-not $acquired) { Write-Host '已有另一个修复窗口在运行，请使用那个窗口。'; return 10 }
        $runName = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
        $runDirectory = Join-Path 'D:\Codex\Temp\codex-cua-recovery' $runName
        [void][IO.Directory]::CreateDirectory($runDirectory)
        $script:LogFile = Join-Path $runDirectory 'Repair_Report.txt'
        Write-RepairMessage 'Codex CUA 运行时检查 / 修复工具' 'Cyan'
        Write-RepairMessage ('日志目录：' + $runDirectory)
        $package = Get-CurrentPackage
        $install = Get-FullDirectoryPath $package.InstallLocation
        $source = Assert-ContainedPath (Join-Path $install 'app\resources\cua_node') $install
        if (-not (Test-Path -LiteralPath $source -PathType Container)) { throw 'The official cua_node source is missing. This app version is not supported by this tool.' }
        Write-RepairMessage ('Codex version: ' + $package.Version)
        Write-RepairMessage ('Source: ' + $source)
        $runtimeId = Get-RequiredRuntimeId $source $install
        if ($runtimeId -notmatch '^[0-9a-f]{16}$') { throw 'Cannot identify the current required runtime ID safely.' }
        $runtimeRoot = Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\runtimes\cua_node'
        Assert-SafeRuntimeRoot $runtimeRoot $env:LOCALAPPDATA
        $target = Assert-ContainedPath (Join-Path $runtimeRoot $runtimeId) $runtimeRoot
        Write-RepairMessage ('Runtime ID: ' + $runtimeId)
        Write-RepairMessage ('Target: ' + $target)
        foreach ($required in @('manifest.json', 'bin\node.exe', 'bin\node_repl.exe')) {
            if (-not (Test-Path -LiteralPath (Join-Path $source $required) -PathType Leaf)) { throw ('Official source lacks required file: ' + $required) }
        }
        Write-RepairMessage '正在逐文件计算 SHA-256，检查官方源和现有运行时，请稍候……' 'Cyan'
        $inventory = Get-TreeInventory $source
        Write-RepairMessage ('Source files: {0}; Source bytes: {1}' -f $inventory.Count, $inventory.Bytes)
        $state = Compare-TreeInventory $inventory $target
        if ($state.Passed) {
            Write-RepairMessage 'RESULT: PASS — 当前运行时完整，无需修复。' 'Green'
            return 0
        }
        Write-RepairMessage ('需要修复：' + $state.Reason) 'Yellow'
        if ($env:CODEX_CUA_FIX_MODE -eq 'check') { Write-RepairMessage 'CHECK ONLY：未修改运行时。'; return 2 }
        $waited = Wait-CodexExit $install $runtimeRoot
        $packageNow = Get-CurrentPackage
        if ($packageNow.PackageFullName -ne $package.PackageFullName -or $packageNow.InstallLocation -ne $package.InstallLocation) { throw 'The app was updated during this run. Please run this file again.' }
        if ((Get-RequiredRuntimeId $source $install) -ne $runtimeId) { throw 'Runtime identity changed during this run. Please run this file again.' }
        if ($waited) {
            Write-RepairMessage '正在重新校验退出后的官方运行时……' 'Cyan'
            $inventory = Get-TreeInventory $source
        }
        Confirm-CodexStopped $install $runtimeRoot
        Write-RepairMessage '正在直接补齐正式运行时目录……' 'Cyan'
        $result = Invoke-RuntimeRepair -SourceInventory $inventory -Target $target -RuntimeRoot $runtimeRoot -RunDirectory $runDirectory -InstallLocation $install
        if ($result.Backup) { Write-RepairMessage ('原目录备份保留于：' + $result.Backup) }
        Write-RepairMessage 'RESULT: PASS — 文件路径、文件数、字节数和逐文件 SHA-256 全部通过。' 'Green'
        Write-RepairMessage '修复完成。现在可以重新打开 Codex。' 'Green'
        return 0
    } catch {
        Write-RepairMessage ('RESULT: FAIL — ' + $_.Exception.Message) 'Red'
        if ($script:LogFile) { Write-Host ('请保留这份报告：' + $script:LogFile) -ForegroundColor Yellow }
        return 1
    } finally {
        if ($acquired -and $mutex) { $mutex.ReleaseMutex() }
        if ($mutex) { $mutex.Dispose() }
    }
}

if ($LibraryOnly) { return }
exit (Invoke-RepairMain)
