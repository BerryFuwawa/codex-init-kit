param([switch]$LibraryOnly)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

function Get-KitHash([string]$Path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($Path)
    try { return [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-', '').ToLowerInvariant() }
    finally { $stream.Dispose(); $sha.Dispose() }
}

function Get-KitVersion([string]$Value) {
    if ($Value -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') { throw 'Invalid component version.' }
    return [version]$Value
}

function Request-KitJson([string]$Url) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $options=@{Uri=$Url; Headers=@{ 'User-Agent'='codex-init-kit'; Accept='application/vnd.github+json' }; TimeoutSec=8}
    if (-not [string]::IsNullOrWhiteSpace($env:HTTPS_PROXY)) { $options.Proxy=$env:HTTPS_PROXY }
    elseif (-not [string]::IsNullOrWhiteSpace($env:PROXY_URL)) { $options.Proxy=$env:PROXY_URL }
    return Invoke-RestMethod @options
}

function Request-KitFile([string]$Url, [string]$Path) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $options=@{Uri=$Url; OutFile=$Path; Headers=@{'User-Agent'='codex-init-kit'}; TimeoutSec=30; UseBasicParsing=$true}
    if (-not [string]::IsNullOrWhiteSpace($env:HTTPS_PROXY)) { $options.Proxy=$env:HTTPS_PROXY }
    elseif (-not [string]::IsNullOrWhiteSpace($env:PROXY_URL)) { $options.Proxy=$env:PROXY_URL }
    Invoke-WebRequest @options
}

function Get-KitRemote([string]$Repository, [string]$Component) {
    if ($Repository -notmatch '^[A-Za-z0-9][A-Za-z0-9-]*/[A-Za-z0-9_.-]+$') { throw 'Invalid GitHub repository.' }
    $commit = Request-KitJson ('https://api.github.com/repos/' + $Repository + '/commits/main')
    $revision = [string]$commit.sha
    if ($revision -notmatch '^[0-9a-f]{40}$') { throw 'Invalid GitHub commit ID.' }
    $base = 'https://raw.githubusercontent.com/' + $Repository + '/' + $revision + '/'
    $manifest = Request-KitJson ($base + 'update-manifest.json')
    if ($manifest.schema_version -ne 1 -or $manifest.repository -cne $Repository) { throw 'Update manifest repository/schema mismatch.' }
    $property = $manifest.components.PSObject.Properties[$Component]
    if (-not $property) { throw 'Component missing from update manifest.' }
    $item = $property.Value
    [void](Get-KitVersion ([string]$item.version))
    $relative = [string]$item.path
    if ($relative -notmatch '^scripts/[^/\\]+\.cmd$' -or $relative.Contains('..')) { throw 'Invalid script download path.' }
    if ([string]$item.sha256 -notmatch '^[0-9a-f]{64}$') { throw 'Invalid script checksum.' }
    return [pscustomobject]@{
        Version=[string]$item.version; Hash=[string]$item.sha256; Encoding=[string]$item.encoding
        Url=$base + (($relative -split '/' | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/')
        Page='https://github.com/' + $Repository + '/commit/' + $revision
    }
}

function Confirm-KitUpdate([string]$Version) {
    return (Read-Host ('发现版本 ' + $Version + '，下载并替换当前脚本？[y/N]')) -match '^(?i:y|yes|是|确认)$'
}

function Install-KitUpdate($Remote, [string]$Target, [string]$Component, [string]$ExpectedOriginalHash) {
    $targetPath = [IO.Path]::GetFullPath($Target)
    $file = Get-Item -LiteralPath $targetPath -Force
    if ($file.PSIsContainer -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw 'Refusing to replace a directory or link.' }
    $directory = Split-Path -Parent $targetPath
    $stage = Join-Path $directory ('.codex-update-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    $backup = $targetPath + '.before-update-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8) + '.bak'
    try {
        Request-KitFile $Remote.Url $stage
        if ((Get-KitHash $stage) -cne $Remote.Hash) { throw 'SHA-256 mismatch; original script was not replaced.' }
        $encoding = switch ($Remote.Encoding) {
            'gbk' { [Text.Encoding]::GetEncoding(936) }
            'utf-8' { [Text.Encoding]::UTF8 }
            default { throw 'Unsupported script encoding.' }
        }
        $candidate = [IO.File]::ReadAllText($stage, $encoding)
        if (-not $candidate.StartsWith('@echo off') -or
            $candidate -notmatch ('(?m)^set "CODEX_KIT_COMPONENT=' + [regex]::Escape($Component) + '"\r?$') -or
            $candidate -notmatch ('(?m)^set "CODEX_KIT_VERSION=' + [regex]::Escape($Remote.Version) + '"\r?$')) {
            throw 'Downloaded file does not match the expected script component/version.'
        }
        if ((Get-KitHash $targetPath) -cne $ExpectedOriginalHash) { throw 'Script changed during download; update cancelled.' }
        [IO.File]::Replace($stage, $targetPath, $backup)
        Write-Host ('更新完成。旧脚本备份：' + $backup) -ForegroundColor Green
        Write-Host '请重新运行脚本，以使用新版本。' -ForegroundColor Green
        return 20
    } finally {
        if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Force }
    }
}

function Invoke-KitUpdate([string]$Repository, [string]$Component, [string]$CurrentVersion, [string]$Target, [string]$Mode = 'auto') {
    $mutex = $null; $acquired = $false
    try {
        $current = Get-KitVersion $CurrentVersion
        $targetPath = [IO.Path]::GetFullPath($Target)
        if (-not (Test-Path -LiteralPath $targetPath -PathType Leaf)) { throw 'Current script is unavailable.' }
        $originalHash = Get-KitHash $targetPath
        $keySha = [Security.Cryptography.SHA256]::Create()
        try { $key = [BitConverter]::ToString($keySha.ComputeHash([Text.Encoding]::UTF8.GetBytes($Repository + ':' + $Component + ':' + $targetPath.ToLowerInvariant()))).Replace('-','') }
        finally { $keySha.Dispose() }
        $mutex = [Threading.Mutex]::new($false, ('Local\CodexKitUpdate-' + $key))
        try { $acquired = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $acquired = $true }
        if (-not $acquired) { return 0 }
        $cacheRoot = Join-Path $env:LOCALAPPDATA 'CodexInitKit\update-checks'
        $cachePath = Join-Path $cacheRoot ($key + '.json')
        if ($Mode -in @('auto', 'background') -and (Test-Path -LiteralPath $cachePath)) {
            try {
                $cached = [IO.File]::ReadAllText($cachePath) | ConvertFrom-Json
                $lastCheck = [datetime]::Parse($cached.checked_at).ToUniversalTime()
                if ($cached.current_version -eq $CurrentVersion -and $lastCheck -le [datetime]::UtcNow -and ([datetime]::UtcNow - $lastCheck).TotalHours -lt 24) { return 0 }
            } catch { }
        }
        $remote = Get-KitRemote $Repository $Component
        try {
            [void][IO.Directory]::CreateDirectory($cacheRoot)
            $state = @{ checked_at=[datetime]::UtcNow.ToString('o'); current_version=$CurrentVersion; latest_version=$remote.Version; source=$remote.Page }
            [IO.File]::WriteAllText($cachePath, ($state | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
        } catch { }
        if ((Get-KitVersion $remote.Version) -le $current) {
            if ($Mode -eq 'manual') { Write-Host ('当前脚本已是最新版本：' + $CurrentVersion) -ForegroundColor Green }
            return 0
        }
        Write-Host ('GitHub 发现新版本：' + $CurrentVersion + ' -> ' + $remote.Version) -ForegroundColor Cyan
        Write-Host ('来源：' + $remote.Page)
        if ($Mode -in @('check', 'background')) { return 0 }
        if (-not (Confirm-KitUpdate $remote.Version)) { Write-Host '已保留当前脚本。'; return 0 }
        return Install-KitUpdate $remote $targetPath $Component $originalHash
    } catch {
        Write-Host ('GitHub 更新检查未完成：' + $_.Exception.Message + '；当前脚本功能可继续使用。') -ForegroundColor Yellow
        return 0
    } finally {
        if ($acquired -and $mutex) { $mutex.ReleaseMutex() }
        if ($mutex) { $mutex.Dispose() }
    }
}

if ($LibraryOnly) { return }
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
exit (Invoke-KitUpdate $env:CODEX_KIT_REPOSITORY $env:CODEX_KIT_COMPONENT $env:CODEX_KIT_VERSION $env:CODEX_KIT_SELF $env:CODEX_KIT_UPDATE_MODE)
