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
    proxyHost = '127.0.0.1'
    folderManagement = $true
    antiOverDefense = $true
    workJournal = $true
    commandLineFirst = $true
    allowCloseRunningApp = $false
    memories = $true
    githubMirrorEnabled = $true
    customGithubMirror = ''
    subagents = $false
    cuaRepair = $true
    browserRepair = $true
    officialCliUpdate = $true
    desktopUpdate = $true
    cliMode = 'auto'
    parentModel = 'gpt-6.1-sol'
    childModel = 'gpt-6.1-sol'
    parentEffort = 'medium'
    childEffort = 'medium'
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
    if ($script:InitializationProgressActive) { return }
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
        proxyHost = Convert-ProxyHost (& $get 'proxyHost')
        folderManagement = Convert-BackendOptionBoolean $options.folderManagement 'folderManagement' $script:DefaultOptions.folderManagement
        subagents = Convert-BackendOptionBoolean $options.subagents 'subagents' $script:DefaultOptions.subagents
        cuaRepair = Convert-BackendOptionBoolean $options.cuaRepair 'cuaRepair' $script:DefaultOptions.cuaRepair
        parentModel = Convert-BackendOptionString $options.parentModel 'parentModel' $script:DefaultOptions.parentModel -Model
        childModel = Convert-BackendOptionString $options.childModel 'childModel' $script:DefaultOptions.childModel -Model
        parentEffort = Convert-BackendOptionString $options.parentEffort 'parentEffort' $script:DefaultOptions.parentEffort
        childEffort = Convert-BackendOptionString $options.childEffort 'childEffort' $script:DefaultOptions.childEffort
    }
    foreach ($name in @('browserRepair','officialCliUpdate','desktopUpdate','antiOverDefense','workJournal','commandLineFirst','memories')) { $normalized[$name] = Convert-BackendOptionBoolean (& $get $name) $name $true }
    $normalized.allowCloseRunningApp=Convert-BackendOptionBoolean (& $get 'allowCloseRunningApp') 'allowCloseRunningApp' $false
    $approvedProcesses=& $get 'closeApprovedProcesses'
    if($null -ne $approvedProcesses){
        if($approvedProcesses -isnot [string] -or $approvedProcesses -cnotmatch '\A[0-9]+:[0-9]+(?:,[0-9]+:[0-9]+)*\z'){throw '本次关闭的进程标识无效，请重新确认。'}
        $normalized.closeApprovedProcesses=$approvedProcesses
    }
    $cliMode=& $get 'cliMode'
    if($null -eq $cliMode){$cliMode=$script:DefaultOptions.cliMode}
    if($cliMode -isnot [string] -or $cliMode -cnotin @('auto','desktop-native','official-standalone')){throw '请选择有效的 CLI 模式。'}
    $normalized.cliMode=$cliMode
    $normalized.githubMirrorEnabled=Convert-BackendOptionBoolean (& $get 'githubMirrorEnabled') 'githubMirrorEnabled' $true
    $customMirror=& $get 'customGithubMirror'
    if($null -ne $customMirror -and $customMirror -isnot [string]){throw '自定义 GitHub 镜像需为文本。'}
    $normalized.customGithubMirror=if($null -eq $customMirror -or -not $normalized.githubMirrorEnabled){''}else{$customMirror.Trim()}
    if($normalized.customGithubMirror){
        $mirrorUri=$null
        if(-not [Uri]::TryCreate($normalized.customGithubMirror,[UriKind]::Absolute,[ref]$mirrorUri) -or $mirrorUri.Scheme -ne 'https' -or $mirrorUri.UserInfo -or $mirrorUri.Query -or $mirrorUri.Fragment){throw '自定义镜像需填写 HTTPS 前缀，不能包含账号密码、查询参数或片段。'}
        $normalized.customGithubMirror=$mirrorUri.AbsoluteUri.TrimEnd('/')+'/'
    }
    $projectPath = & $get 'projectPath'
    if ($null -ne $projectPath -and $projectPath -isnot [string]) { throw 'Option projectPath must be a string.' }
    $normalized.projectPath = if ($null -eq $projectPath) { '' } else { $projectPath.Trim() }
    $rollbackId = & $get 'rollbackId'
    if ($null -ne $rollbackId) {
        if ($rollbackId -isnot [string] -or $rollbackId -cnotmatch '\A[a-f0-9]{64}\z') { throw '回滚目标标识无效，请刷新备份列表。' }
        $normalized.rollbackId = $rollbackId
    }
    return $normalized
}
function Convert-ProxyHost {
    param([AllowNull()][object]$Value)
    if ($null -eq $Value -or ($Value -is [string] -and [string]::IsNullOrWhiteSpace($Value))) { return '127.0.0.1' }
    if ($Value -isnot [string]) { throw 'Proxy IP must be a string.' }
    $address = $null
    if (-not [Net.IPAddress]::TryParse($Value.Trim(),[ref]$address)) { throw 'Proxy IP must be a valid IPv4 or IPv6 address.' }
    return $address.ToString()
}
function Get-ProxyUrl {
    param([int]$Port,[AllowNull()][string]$HostValue)
    if ($Port -eq 0) { return '' }
    $hostText = Convert-ProxyHost $HostValue
    if ($hostText.Contains(':')) { $hostText = '[' + $hostText + ']' }
    return 'http://' + $hostText + ':' + $Port
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
function New-RollbackEntryId {
    param([string]$Kind,[IO.DirectoryInfo]$Directory,[string]$Marker)
    $bytes = [Text.Encoding]::UTF8.GetBytes($Kind+'|'+$Directory.FullName.ToUpperInvariant()+'|'+$Directory.CreationTimeUtc.Ticks)
    if (Test-Path -LiteralPath $Marker -PathType Leaf) { $bytes += [IO.File]::ReadAllBytes($Marker) }
    return (Get-RollbackHash $bytes)
}
function Get-RollbackHash {
    param([byte[]]$Bytes)
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash($Bytes)).Replace('-','').ToLowerInvariant() } finally { $sha.Dispose() }
}
function Get-RollbackCliReferences {
    param([string]$StandaloneRoot)
    $current=Get-ChildItem -LiteralPath $StandaloneRoot -Directory -Force -ErrorAction Stop | Where-Object Name -eq 'current'
    if($current -and ($current.Attributes -band [IO.FileAttributes]::ReparsePoint)){
        if(-not $current.Target){throw '无法确认独立 CLI 当前版本链接。'}
        foreach($target in $current.Target){
            if(-not [IO.Path]::IsPathRooted($target)){$target=Join-Path $StandaloneRoot $target}
            [IO.Path]::GetFullPath($target)
        }
    }
    $statePath=Join-Path (Get-GuardInstallRootPath) 'State\guard-state.json'
    if(Test-Path -LiteralPath $statePath -PathType Leaf){
        $state=[IO.File]::ReadAllText($statePath,[Text.Encoding]::UTF8)|ConvertFrom-Json -ErrorAction Stop
        foreach($name in @('ActiveCliPath','OriginalCliPath')){
            $path=Get-ObjectPropertyValue $state $name
            if($path){[IO.Path]::GetFullPath($path)}
        }
    }
    foreach($scope in @('Process','User','Machine')){
        foreach($name in @('CODEX_CLI_PATH','CODEX_CODE_MODE_HOST_PATH')){
            $path=[Environment]::GetEnvironmentVariable($name,$scope)
            if($path){[IO.Path]::GetFullPath($path)}
        }
    }
}
function Test-RollbackCliReference {
    param([string]$Directory,[string[]]$References)
    $prefix=$Directory.TrimEnd('\')+'\'
    foreach($path in $References){
        if($path.TrimEnd('\') -ieq $Directory -or $path.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){return $true}
    }
    return $false
}
function Get-RollbackEntries {
    $entries = [Collections.Generic.List[object]]::new()
    foreach ($kind in @('init','cli')) {
        $root = if ($kind -eq 'init') { Get-BackupRootPath } else { Join-Path (Get-CodexHomePath) 'packages\standalone\releases' }
        if (-not (Test-Path -LiteralPath $root -PathType Container)) { continue }
        $unknownCli=$false;$cliReferences=@()
        if($kind -eq 'cli'){
            try{$cliReferences=@(Get-RollbackCliReferences (Split-Path $root -Parent))}
            catch{$unknownCli=$true}
        }
        foreach ($directory in @(Get-ChildItem -LiteralPath $root -Directory -ErrorAction Stop)) {
            if (($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { continue }
            if ($kind -eq 'init' -and $directory.Name -notmatch '^Codex_Full_Reset_[A-Za-z0-9_.-]+$') { continue }
            $marker = Join-Path $directory.FullName $(if($kind -eq 'init'){'session.meta'}else{'codex-package.json'})
            $available = Test-Path -LiteralPath $marker -PathType Leaf
            $version = '版本未记录'; $detail = ''; $timestamp = $directory.CreationTime
            if ($kind -eq 'init') {
                $versionFile = Join-Path $directory.FullName 'desktop-version.txt'
                if (Test-Path -LiteralPath $versionFile -PathType Leaf) {
                    $recorded = ([IO.File]::ReadAllText($versionFile)).Trim()
                    if ($recorded -match '^\d+\.\d+\.\d+$') { $version = '备份工具 v'+$recorded }
                }
                $stamp = [datetime]::MinValue
                if ([datetime]::TryParseExact(($directory.Name -replace '^Codex_Full_Reset_',''),'yyyyMMdd-HHmmss',[Globalization.CultureInfo]::InvariantCulture,[Globalization.DateTimeStyles]::None,[ref]$stamp)) { $timestamp=$stamp }
                $available = $available -and (Test-Path -LiteralPath (Join-Path $directory.FullName 'Backups') -PathType Container)
                $configPath = Join-Path $directory.FullName 'Backups\config.toml'
                if (Test-Path -LiteralPath $configPath -PathType Leaf) { $model=Get-ConfigValue ([IO.File]::ReadAllText($configPath)) 'model';if($model){$detail='备份模型：'+$model} }
            } else {
                if ($available) {
                    try { $package=[IO.File]::ReadAllText($marker)|ConvertFrom-Json; $v=Get-ObjectPropertyValue $package 'version';if($v){$version=[string]$v} }
                    catch { $available=$false }
                }
                if ($version -eq '版本未记录' -and $directory.Name -match '(\d+\.\d+\.\d+(?:[-+][A-Za-z0-9.-]+)?)') { $version=$Matches[1] }
                foreach ($file in @('bin\codex.exe','bin\codex-code-mode-host.exe','codex-path\rg.exe','codex-resources\codex-command-runner.exe','codex-resources\codex-windows-sandbox-setup.exe')) {
                    if (-not (Test-Path -LiteralPath (Join-Path $directory.FullName $file) -PathType Leaf)) { $available=$false }
                }
                $detail='本地独立 CLI 缓存，执行前检查官方签名、完整性和实际版本。'
            }
            if([IO.File]::Exists((Join-Path $directory.FullName '.rollback-cleanup-pending'))){
                $available=$false;$detail='上次删除未完成，可再次清理。'
            }elseif(-not $available){$detail='备份或组件不完整，无法回滚。'}
            $protected=$kind -eq 'cli' -and ($unknownCli -or (Test-RollbackCliReference $directory.FullName $cliReferences))
            if($protected){$detail+=if($unknownCli){' 当前 CLI 引用无法确认，清理时保留。'}else{' 当前或受保护的 CLI，清理时保留。'}}
            $entries.Add([ordered]@{id=New-RollbackEntryId $kind $directory $marker;kind=$kind;version=$version;time=$timestamp.ToString('yyyy-MM-dd HH:mm:ss');path=$directory.FullName;available=[bool]$available;protected=[bool]$protected;detail=$detail})
        }
    }
    return @($entries | Sort-Object { $_.time } -Descending)
}
function Get-RollbackDeletionPlan {
    param([object[]]$Entries)
    $targets=@($Entries|Where-Object {-not $_.protected})
    $id=if($targets.Count){Get-RollbackHash ([Text.Encoding]::UTF8.GetBytes(($targets.id|Sort-Object) -join ','))}else{''}
    return @{entries=$targets;id=$id}
}
function Remove-RollbackDirectory {
    param([IO.DirectoryInfo]$Directory,[string]$PendingMarker)
    foreach($item in $Directory.GetFileSystemInfos()){
        if($item.FullName -ieq $PendingMarker){continue}
        if($item -is [IO.DirectoryInfo] -and -not ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)){
            Remove-RollbackDirectory $item
        }else{
            # Delete a directory link itself, without entering the target.
            $item.Delete()
        }
    }
    if($PendingMarker){[IO.File]::Delete($PendingMarker)}
    $Directory.Delete()
}
function Remove-RollbackEntries {
    param([string]$PayloadRoot,[string]$Id)
    $plan=Get-RollbackDeletionPlan @(Get-RollbackEntries)
    if(-not $plan.id -or $plan.id -cne $Id){throw '可删除的备份列表已变化，请刷新后重新确认。'}
    if(@($plan.entries|Where-Object kind -eq 'cli').Count){Assert-InitializationAppsStopped $PayloadRoot}
    $failed=0;$deleted=0
    foreach($entry in $plan.entries){
        try{
            $root=if($entry.kind -eq 'init'){Get-BackupRootPath}else{Join-Path (Get-CodexHomePath) 'packages\standalone\releases'}
            $root=(Get-Item -LiteralPath $root -Force -ErrorAction Stop)
            $target=(Get-Item -LiteralPath $entry.path -Force -ErrorAction Stop)
            if(!$target.PSIsContainer -or $target.Parent.FullName -ine $root.FullName -or
                (($root.Attributes -bor $target.Attributes) -band [IO.FileAttributes]::ReparsePoint)){
                throw '目录不在已确认的备份范围内。'
            }
            $marker=Join-Path $target.FullName $(if($entry.kind -eq 'init'){'session.meta'}else{'codex-package.json'})
            if((New-RollbackEntryId $entry.kind $target $marker) -cne $entry.id){throw '备份已变化，请刷新列表。'}
            if($entry.kind -eq 'cli' -and (Test-RollbackCliReference $target.FullName @(Get-RollbackCliReferences $root.Parent.FullName))){
                throw '该 CLI 缓存已被引用，保留。'
            }
            $pendingMarker=Join-Path $target.FullName '.rollback-cleanup-pending'
            if(-not [IO.File]::Exists($pendingMarker)){[IO.File]::Open($pendingMarker,[IO.FileMode]::CreateNew).Dispose()}
            Remove-RollbackDirectory $target $pendingMarker
            $deleted++
            Write-BackendLine ('已删除回滚项：'+$target.FullName)
        }catch{
            $failed++
            Write-BackendLine ('未能删除回滚项：'+$entry.path+'。'+$_.Exception.Message)
        }
    }
    Write-BackendLine ('回滚清理：已删除 '+$deleted+' 项，未完成 '+$failed+' 项。')
    if($failed){return 1}
    return 0
}
function Resolve-SelectedRollback {
    param([string]$Id)
    if ($Id -cnotmatch '\A[a-f0-9]{64}\z') { throw '请先选择一个可用的回滚项。' }
    $entry = @(Get-RollbackEntries | Where-Object { $_.id -ceq $Id }) | Select-Object -First 1
    if (-not $entry) { throw '所选备份已变化或被移除，请刷新列表后重新选择。' }
    if (-not $entry.available) { throw '所选备份或组件不完整，未执行回滚。' }
    return $entry
}
function Invoke-SelectedRollback {
    param([string]$PayloadRoot,[string]$Id,[int]$ProxyPortValue=0)
    $entry = Resolve-SelectedRollback $Id
    [void](Assert-DesktopStopped $PayloadRoot -AllowOtherChannel)
    if ($entry.kind -eq 'init') {
        Write-BackendLine ('正在恢复初始化备份：'+$entry.time+' · '+$entry.version+' · '+$entry.path)
        return (Invoke-InitRollback $PayloadRoot $ProxyPortValue $entry.path)
    }
    Initialize-GuardRuntime (Assert-PayloadFile $PayloadRoot 'guard.cmd') -ForModification
    if (Test-DesktopRunning) { throw 'Codex is running. Save work and fully exit before continuing.' }
    $package = Test-CanonicalPackage $entry.path
    if (-not $package.Healthy) { throw '所选 CLI 的官方签名、完整性或版本校验失败，未切换运行时。' }
    if ($entry.version -ne '版本未记录' -and $package.Version -ne $entry.version) { throw '所选 CLI 的实际版本与列表不符，请刷新后重试。' }
    $state=Load-State
    Set-ActiveCli $package.Cli $state 'Rollback'
    $after=Load-State
    if ($after.ActiveMode -ne 'Rollback' -or $after.ActiveCliPath -ne $package.Cli) { throw 'Protection rollback did not complete.' }
    Write-BackendLine ('已回滚保护器 CLI：'+$package.Version+' · 缓存时间：'+$entry.time)
    return 0
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
    param([string]$SourceText,[string]$ScriptDirectory,[string]$WorkRules,[string]$PayloadRoot)
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
        if($tag -eq 'PMB64' -and $PayloadRoot){
            $assertions=[ordered]@{
                'This restriction applies only to independent-task APIs.'='上述限制只适用于创建独立任务的接口。'
                'the parent should proactively identify opportunities for in-task parallelism'='主任务应主动识别任务内可并行的工作'
                'two or more genuinely independent subtasks'='两个或更多确实独立的子任务'
                'largest number of useful, non-overlapping subagents supported by the task, up to 6.'='充分使用任务可支持的子代理，最多 6 个。'
                'The parent may use any available main model'='主任务可使用任何可用的主模型'
                'waiting for all required child results'='等待所有必要的子代理结果'
                'performing final verification'='执行最终验证'
                'Configured Codex workspace root: {0}:\Codex'='Codex 工作区根目录：{0}:\Codex'
                'At the beginning of each task, proactively classify the work,'='开始任务时，主动判断工作类型'
                'Do not broadly reorganize historical files automatically.'='历史文件的整理需要明确授权。'
                'User-specified paths, repositories, and project locations take priority.'='优先使用用户指定的路径、仓库和项目位置。'
                'Before finalizing a task, verify that active work and final outputs are'='交付前，确认进行中的工作和最终产物都放在对应目录'
            }
            foreach($old in $assertions.Keys){$code=$code.Replace($old,$assertions[$old])}
            foreach($template in @{prompt='SubagentRules.md';folderPromptTemplate='FolderRules.md'}.GetEnumerator()){
                $assignment='$'+$template.Key+" = @'"
                $pattern='(?ms)^'+[regex]::Escape($assignment)+"\r?\n.*?^'@(?=\r?$)"
                if($code -notmatch $pattern){throw ('找不到桌面提示词模板：'+$template.Key)}
                $body=[IO.File]::ReadAllText((Assert-PayloadFile $PayloadRoot $template.Value),[Text.Encoding]::UTF8).TrimEnd()
                $replacement=$assignment+"`n"+$body+"`n'@"
                $code=[regex]::Replace($code,$pattern,[Text.RegularExpressions.MatchEvaluator]{param($m) $replacement})
            }
        }
        $code = [regex]::Replace($code,'(?m)^([ \t]*)\[Console\]::OutputEncoding\s*=[^\r\n]*', '$1[Console]::OutputEncoding = [Text.Encoding]::GetEncoding(936)')
        $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($code))
        $chunks = for ($i = 0; $i -lt $encoded.Length; $i += 180) { '::' + $tag + ':' + $encoded.Substring($i,[Math]::Min(180,$encoded.Length-$i)) }
        $SourceText = $SourceText.Remove($match.Index,$match.Length).Insert($match.Index,($chunks -join "`r`n") + "`r`n")
    }
    $prefix = "`$ProgressPreference='SilentlyContinue'; [Console]::OutputEncoding=[Text.Encoding]::GetEncoding(936); `$OutputEncoding=[Console]::OutputEncoding;`n"
    $rulesReader = '[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($env:CODEX_INIT_RULES_B64))'
    $rulesFileReader = '[IO.File]::ReadAllText((Join-Path $PSScriptRoot ''work-rules.md''),[Text.Encoding]::UTF8)'
    $oldPrefix='''@''+$rtk+"`n`n"+'
    $oldCheck=@'
if($text.IndexOf('@'+$env:CODEX_INIT_RTK_FILE,[StringComparison]::OrdinalIgnoreCase) -lt 0){exit 1}
'@
    $newCheck=@'
if(-not $text.StartsWith('# RTK'+[char]10,[StringComparison]::Ordinal)){exit 1}
'@
    $writeLicense=@'
    $license = Join-Path $PSScriptRoot 'HERO-Anti-OverDefense.LICENSE.txt'
    if ([IO.File]::Exists($license)) {
        [IO.File]::Copy($license,(Join-Path ([IO.Path]::GetDirectoryName($path)) 'HERO-Anti-OverDefense.LICENSE.txt'),$true)
    }
'@
    $rulesWriter = @{Found=$false;Check=$false}
    $evaluator = [Text.RegularExpressions.MatchEvaluator]{
        param($match)
        $flags = $match.Groups[1].Value
        if ($flags -notmatch '(?i)-InputFormat\s+Text') { $flags += '-InputFormat Text ' }
        if ($flags -notmatch '(?i)-OutputFormat\s+Text') { $flags += '-OutputFormat Text ' }
        $code = [Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($match.Groups[2].Value))
        if ($WorkRules -and $code.Contains($rulesReader)) {
            if(-not $code.Contains($oldPrefix)){throw '找不到旧 RTK 引用写入步骤。'}
            $code = $code.Replace($rulesReader,$rulesFileReader).Replace($oldPrefix,'')
            $code = $code.Replace('    [IO.File]::WriteAllText($path,$newText,',$writeLicense+"`n"+'    [IO.File]::WriteAllText($path,$newText,')
            $rulesWriter.Found = $true
        }
        if($code.Contains($oldCheck)){
            $code=$code.Replace($oldCheck,$newCheck)
            $rulesWriter.Check=$true
        }
        # EncodedCommand serializes the information stream to CLIXML on
        # redirected stderr even with OutputFormat Text. A BOM-marked script
        # file uses ordinary text streams and avoids cmd.exe's 8191-byte limit.
        $path = Join-Path $ScriptDirectory ('console-' + [Guid]::NewGuid().ToString('N') + '.ps1')
        [IO.File]::WriteAllText($path,$prefix + $code,[Text.UTF8Encoding]::new($true))
        return $flags + '-File "' + $path + '"'
    }
    $SourceText = [regex]::Replace($SourceText,'(?i)(powershell\.exe\s+[^\r\n]*?)-EncodedCommand\s+([A-Za-z0-9+/=]+)',$evaluator)
    if ($WorkRules) {
        if (-not $rulesWriter.Found -or -not $rulesWriter.Check) { throw '未找到桌面初始化的规则读写校验步骤，请重新构建桌面工具。' }
        [IO.File]::WriteAllText((Join-Path $ScriptDirectory 'work-rules.md'),$WorkRules,$script:Utf8NoBom)
    }
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
    $psi.RedirectStandardInput = $true
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
        # GUI operations never accept hidden terminal prompts; pass EOF immediately.
        $process.StandardInput.Close()
        # Drain stderr asynchronously so a noisy child cannot deadlock while
        # stdout is streamed line by line to the GUI.
        $stderrTask = $process.StandardError.ReadToEndAsync()
        $stdoutBuilder = New-Object System.Text.StringBuilder
        while (($line = $process.StandardOutput.ReadLine()) -ne $null) {
            [void]$stdoutBuilder.AppendLine($line)
            if (-not ($script:InitializationProgressActive -and $line.StartsWith('@@PROGRESS@@'))) { Write-BackendLine $line }
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
    $rulesMatch = [regex]::Match($sourceText,'(?m)^set "CODEX_INIT_RULES_B64=([A-Za-z0-9+/=]+)"\r?$')
    if (-not $rulesMatch.Success) { throw '找不到基础工作规则。' }
    $rules = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($rulesMatch.Groups[1].Value))
    if (-not $subagentsEnabled) { $rules = [regex]::Replace($rules,'(?ms)^SUBAGENTS\r?\n.*?(?=^<!-- END CODEX INIT WORK RULES -->)','') }
    $headings=[ordered]@{GOAL='目标';AUTONOMY='自主推进';PRIORITY='指令优先级';STYLE='表达风格';DONE='完成条件';APPROVAL='授权';VERIFY='验证';SUBAGENTS='子代理'}
    foreach($key in $headings.Keys){$rules=[regex]::Replace($rules,('(?m)^'+$key+'(?=\r?$)'),$headings[$key])}
    $rtkRules=[IO.File]::ReadAllText((Assert-PayloadFile $PayloadRoot 'RtkRules.md'),[Text.Encoding]::UTF8).Replace("`r`n","`n").TrimEnd()
    $rules=$rtkRules+"`n`n"+$rules
    $optionalRules = [ordered]@{
        antiOverDefense = 'AntiOverDefense.md'
        workJournal = 'WorkJournal.md'
        commandLineFirst = 'CommandLineFirst.md'
    }
    foreach ($name in $optionalRules.Keys) {
        if ($resolvedOptions[$name]) {
            $section = [IO.File]::ReadAllText((Assert-PayloadFile $PayloadRoot $optionalRules[$name]),[Text.Encoding]::UTF8)
            $rules = $rules.TrimEnd([char[]]"`r`n") + "`n`n" + $section
        }
    }
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
    $sourceText = $sourceText.Replace(') > "%SESSION_DIR%\session.meta"', ') > "%SESSION_DIR%\session.meta"' + [Environment]::NewLine + '> "%SESSION_DIR%\desktop-version.txt" echo 2.2.12')
    $folderRuleCheck=@'
$text=[IO.File]::ReadAllText($env:AGENTS_FILE,[Text.Encoding]::UTF8)
if($text.IndexOf($env:EXPECTED_CODEX_ROOT,[StringComparison]::OrdinalIgnoreCase) -lt 0){
    [Console]::Error.WriteLine('文件夹管理校验失败：规则中未找到所选工作目录。')
    exit 1
}
exit 0
'@
    $folderRuleEncoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($folderRuleCheck))
    $folderRuleLine='powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand '+$folderRuleEncoded+' || exit /b 1'
    $sourceText=$sourceText.Replace('findstr /l /c:"Configured Codex workspace root: !EXPECTED_CODEX_ROOT!" "%AGENTS_FILE%" >nul || exit /b 1',$folderRuleLine)
    $proxySetup = 'set "PROXY_PORT=%CODEX_GUI_PROXY_PORT%"' + [Environment]::NewLine +
        ('if not defined PROXY_PORT set "PROXY_PORT=' + $defaultProxyPort + '"')
    $sourceText = $sourceText.Replace('set "PROXY_URL=http://127.0.0.1:%PROXY_PORT%"', 'if "%CODEX_GUI_PROXY_DISABLED%"=="1" (set "PROXY_URL=") else (set "PROXY_URL=%CODEX_GUI_PROXY_URL%")')
    $sourceText = $sourceText.Replace('set "PROXY_PORT=10808"', $proxySetup)
    $sourceText = $sourceText.Replace('set "DEFAULT_PARENT_MODEL_ID=gpt-6.1-sol"', 'set "DEFAULT_PARENT_MODEL_ID=%CODEX_GUI_PARENT_MODEL%"' + [Environment]::NewLine + 'if not defined DEFAULT_PARENT_MODEL_ID set "DEFAULT_PARENT_MODEL_ID=gpt-6.1-sol"')
    $sourceText = $sourceText.Replace('set "DEFAULT_PARENT_REASONING_EFFORT=high"', 'set "DEFAULT_PARENT_REASONING_EFFORT=%CODEX_GUI_PARENT_EFFORT%"' + [Environment]::NewLine + 'if not defined DEFAULT_PARENT_REASONING_EFFORT set "DEFAULT_PARENT_REASONING_EFFORT=medium"')
    $sourceText = $sourceText.Replace('set "LUNA_MANAGER_INSTALLED=1"', 'set "LUNA_MANAGER_INSTALLED=%CODEX_GUI_SUBAGENTS%"' + [Environment]::NewLine + 'if not defined LUNA_MANAGER_INSTALLED set "LUNA_MANAGER_INSTALLED=1"')
    $sourceText = $sourceText.Replace('    echo [desktop]', "    echo [memories]`r`n    echo generate_memories = %CODEX_GUI_MEMORIES%`r`n    echo use_memories = %CODEX_GUI_MEMORIES%`r`n    echo.`r`n    echo [desktop]")
    $sourceText = $sourceText.Replace('    echo [features]', "    echo [features]`r`n    echo memories = %CODEX_GUI_MEMORIES%")
    $sourceText = $sourceText.Replace('        echo [agents]', "        echo [agents]`r`n        echo default_subagent_model = `"%CODEX_GUI_CHILD_MODEL%`"`r`n        echo default_subagent_reasoning_effort = `"%CODEX_GUI_CHILD_EFFORT%`"")
    $sourceText = $sourceText.Replace('        echo max_depth = 1' + [Environment]::NewLine + '    )', '        echo max_depth = 1' + [Environment]::NewLine + '    )' + [Environment]::NewLine + '    if "!LUNA_MANAGER_INSTALLED!"=="0" (' + [Environment]::NewLine + '        echo [agents]' + [Environment]::NewLine + '        echo default_subagent_model = "%CODEX_GUI_CHILD_MODEL%"' + [Environment]::NewLine + '        echo default_subagent_reasoning_effort = "%CODEX_GUI_CHILD_EFFORT%"' + [Environment]::NewLine + '    )')
    $sourceText = $sourceText.Replace('if "!LUNA_MANAGER_INSTALLED!"=="1" echo multi_agent = true', 'if "!LUNA_MANAGER_INSTALLED!"=="1" echo multi_agent = true' + [Environment]::NewLine + '    if "!LUNA_MANAGER_INSTALLED!"=="0" echo multi_agent = false')
    # Global AGENTS is rebuilt from scratch during the base stage. Old markers
    # must not force an unchecked feature to be enabled.
    $sourceText = [regex]::Replace($sourceText,'(?m)^if exist "%AGENTS_FILE%" \(\r?\n(?=    findstr /x /l /c:"BEGIN CODEX LUNA PROMPT V1.3")','if /i "%CODEX_GUI_SUBAGENTS%"=="1" if exist "%AGENTS_FILE%" (' + [Environment]::NewLine)
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
        '$1' + [Environment]::NewLine + 'if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" call :INSTALL_FOLDER_MANAGEMENT' + [Environment]::NewLine + 'if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" if errorlevel 1 (' + [Environment]::NewLine + '    echo 文件夹管理安装失败。' + [Environment]::NewLine + '    set "FOLDER_RESULT=FAIL"' + [Environment]::NewLine + '    set "RESULT=FAIL"' + [Environment]::NewLine + '    call :WRITE_REPORT "一键初始化 - 文件夹管理安装失败"' + [Environment]::NewLine + '    call :WRITE_ROLLBACK' + [Environment]::NewLine + '    goto :ONE_CLICK_INIT_FAIL' + [Environment]::NewLine + ')' + [Environment]::NewLine + 'if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" call :VERIFY_FOLDER_BINDING' + [Environment]::NewLine + 'if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" if errorlevel 1 (' + [Environment]::NewLine + '    echo 文件夹管理校验失败，请检查工作目录规则和无项目任务目录。' + [Environment]::NewLine + '    set "FOLDER_RESULT=FAIL"' + [Environment]::NewLine + '    set "RESULT=FAIL"' + [Environment]::NewLine + '    call :WRITE_REPORT "一键初始化 - 文件夹管理校验失败"' + [Environment]::NewLine + '    call :WRITE_ROLLBACK' + [Environment]::NewLine + '    goto :ONE_CLICK_INIT_FAIL' + [Environment]::NewLine + ')' + [Environment]::NewLine)
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
    $optionalStageMessages = @(
        @('CODEX_GUI_FOLDER_MANAGEMENT','echo 阶段 1/5：确认文件夹管理盘符。','echo 阶段 1/5：跳过工作盘确认（文件夹管理未勾选）。'),
        @('CODEX_GUI_FOLDER_MANAGEMENT','echo 阶段 3/5：安装文件夹管理并创建标准目录。','echo 阶段 3/5：跳过文件夹管理（未勾选）。'),
        @('CODEX_GUI_CUA_REPAIR','echo 阶段 4/5：检查并按需修复 Codex CUA 运行时。','echo 阶段 4/5：跳过 CUA 检查与修复（未勾选）。'),
        @('CODEX_GUI_SUBAGENTS','echo 阶段 5/5：安装 GPT6.1 SOL HIGH 多线程提示词。','echo 阶段 5/5：跳过子代理规则安装（未勾选）。')
    )
    foreach ($stage in $optionalStageMessages) {
        $sourceText = $sourceText.Replace($stage[1],('if /i "%'+$stage[0]+'%"=="1" '+$stage[1])+[Environment]::NewLine+('if /i "%'+$stage[0]+'%"=="0" '+$stage[2]))
    }
    $sourceText = $sourceText.Replace('echo 一键初始化完成：完整初始化、文件夹管理、CUA 检查修复和 Luna 多线程均已通过。','echo 一键初始化完成：基础配置和全局规则已重建，所选功能均已通过。')
    $selectedResult = @'
set "RESULT=!FULL_INIT_RESULT!"
if not "!CONFIG_RESULT!"=="PASS" set "RESULT=FAIL"
if not "!AGENTS_RESULT!"=="PASS" set "RESULT=FAIL"
if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="1" if not "!FOLDER_RESULT!"=="PASS" set "RESULT=FAIL"
if /i "%CODEX_GUI_CUA_REPAIR%"=="1" if not "!CUA_RESULT!"=="PASS" set "RESULT=FAIL"
if /i "%CODEX_GUI_SUBAGENTS%"=="1" if not "!LUNA_PROMPT_RESULT!"=="PASS" set "RESULT=FAIL"
if /i "%CODEX_GUI_FOLDER_MANAGEMENT%"=="0" set "FOLDER_RESULT=SKIPPED"
if /i "%CODEX_GUI_CUA_REPAIR%"=="0" set "CUA_RESULT=SKIPPED"
if /i "%CODEX_GUI_SUBAGENTS%"=="0" set "LUNA_PROMPT_RESULT=SKIPPED"
'@
    $sourceText=[regex]::Replace($sourceText,'(?m)^if "!CONFIG_RESULT!"=="PASS" if "!AGENTS_RESULT!"=="PASS" if "!FOLDER_RESULT!"=="PASS" if "!CUA_RESULT!"=="PASS" if "!LUNA_PROMPT_RESULT!"=="PASS" set "RESULT=!FULL_INIT_RESULT!"\r?$',$selectedResult.TrimEnd())
    $workRoot = Join-Path $PayloadRoot '.backend'
    [void][IO.Directory]::CreateDirectory($workRoot)
    $adapterPath = Join-Path $workRoot ('init-adapter-' + [Guid]::NewGuid().ToString('N') + '.cmd')
    # Only the private desktop adapter changes downloads; standalone scripts
    # retain their existing behavior. The ZIP is validated before it is used.
    $downloadHelper=Join-Path $PayloadRoot 'GitHubDownload.ps1'
    $downloadBlock=@"
:DOWNLOAD_FILE
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File "$downloadHelper" -Url "%~1" -Path "%~2" -ProxyPort "%CODEX_GUI_PROXY_PORT%" -ProxyHost "%CODEX_GUI_PROXY_HOST%" -OptionsBase64 "%CODEX_GUI_NETWORK_OPTIONS%"
exit /b %errorlevel%

"@
    $sourceText=[regex]::Replace($sourceText,'(?ms)^:DOWNLOAD_FILE\r?\n.*?(?=^:[A-Z_]+\r?$)',$downloadBlock)
    $sourceText=[regex]::Replace($sourceText,'(?ms)(^call :DOWNLOAD_FILE "%RTK_RELEASE_URL%" "%RTK_ZIP%"\r?\nif errorlevel 1 \(.*?^\)\r?\n)(\r?\n)?(call :DOWNLOAD_FILE "%RTK_CHECKSUM_URL%" "%RTK_CHECKSUMS%"\r?\nif errorlevel 1 \(.*?^\)\r?\n)','$3$2$1')
    $sourceText=$sourceText.Replace('call :VERIFY_RTK_ZIP','ver >nul')
    # The desktop writer embeds RTK instructions before this final check.
    # Keep the standalone script's reference-based verification intact.
    if($sourceText -match '(?m)^:VERIFY_FULL\r?$'){
        $rtkDocumentPattern='(?ms)^set "RTK_DOC_RESULT=FAIL"\r?\n(?=if exist "%RTK_FILE%" if exist "%AGENTS_FILE%").*?(?=^rem Optional rules)'
        if($sourceText -notmatch $rtkDocumentPattern){throw '找不到初始化末尾的 RTK 文档校验。'}
        $check=@'
$expected=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('__RTK_RULES__'))
$text=[IO.File]::ReadAllText($env:CODEX_INIT_AGENTS_FILE,[Text.Encoding]::UTF8).Replace("`r`n","`n")
if($text.StartsWith($expected+"`n`n",[StringComparison]::Ordinal)){exit 0}
exit 1
'@
        $check=$check.Replace('__RTK_RULES__',[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($rtkRules)))
        $encodedCheck=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($check))
        $rtkDocumentBlock=@"
set "RTK_DOC_RESULT=FAIL"
if exist "%RTK_FILE%" if exist "%AGENTS_FILE%" (
    set "CODEX_INIT_AGENTS_FILE=%AGENTS_FILE%"
    powershell.exe -NoProfile -ExecutionPolicy Bypass -EncodedCommand $encodedCheck
    if not errorlevel 1 set "RTK_DOC_RESULT=PASS"
    set "CODEX_INIT_AGENTS_FILE="
)
if not "!RTK_DOC_RESULT!"=="PASS" echo RTK 文档校验失败：RTK.md 不存在或 AGENTS.md 中的内联规则不完整。
"@
        $sourceText=[regex]::Replace($sourceText,$rtkDocumentPattern,[Text.RegularExpressions.MatchEvaluator]{param($m) $rtkDocumentBlock+"`r`n"})
    }
    # Use the same managed-patch-aware CUA library in the GUI's original
    # five-stage script. The standalone legacy init component stays unchanged.
    if ($sourceText -match '(?m)^::CUAB64:') {
        $cuaText=[IO.File]::ReadAllText((Assert-PayloadFile $PayloadRoot 'cua.cmd'),[Text.Encoding]::UTF8)
        $cuaMarker='#==POWERSHELL=='
        $cuaCode=$cuaText.Substring($cuaText.LastIndexOf($cuaMarker)+$cuaMarker.Length)
        $cuaEncoded=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($cuaCode))
        $cuaLines=for($i=0;$i -lt $cuaEncoded.Length;$i+=180){'::CUAB64:'+$cuaEncoded.Substring($i,[Math]::Min(180,$cuaEncoded.Length-$i))}
        $replacement=($cuaLines -join "`r`n")+"`r`n"
        $sourceText=[regex]::Replace($sourceText,'(?m)^::CUAB64:[A-Za-z0-9+/=]+\r?\n(?:^::CUAB64:[A-Za-z0-9+/=]+\r?\n)*',[Text.RegularExpressions.MatchEvaluator]{param($m) $replacement})
    }
    # GUI initialization never forcibly terminates a user's reopened task.
    # Exit the private cmd worker itself if Codex reappears after preflight.
    if ($sourceText -match '(?m)^:STOP_CODEX\r?$') {
        $stopCode=@'
$ErrorActionPreference='Stop'
try {
    . '__BACKEND__'
    Assert-InitializationAppsStopped '__PAYLOAD__'
} catch { [Console]::Error.WriteLine($_.Exception.Message); exit 1 }
'@
        $stopCode=$stopCode.Replace('__BACKEND__',(Join-Path $PSScriptRoot 'Backend.ps1').Replace("'","''")).Replace('__PAYLOAD__',$PayloadRoot.Replace("'","''"))
        $stopEncoded=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($stopCode))
        $stopBlock=":STOP_CODEX`r`npowershell.exe -NoLogo -NoProfile -NonInteractive -EncodedCommand $stopEncoded`r`nif errorlevel 1 exit 1`r`nexit /b 0`r`n`r`n"
        $sourceText=[regex]::Replace($sourceText,'(?ms)^:STOP_CODEX\r?\n.*?(?=^:[A-Z_]+\r?$)',[Text.RegularExpressions.MatchEvaluator]{param($m) $stopBlock})
        if($sourceText -match '(?i)taskkill\s+/f'){throw 'Unrecognized process termination in init adapter; stopped.'}
    }
    $scriptDirectory=[IO.Path]::ChangeExtension($adapterPath,'.scripts')
    $sourceText = Set-InitConsoleProtocol $sourceText $scriptDirectory -WorkRules $rules -PayloadRoot $PayloadRoot
    if($resolvedOptions.antiOverDefense){
        $licenseName='HERO-Anti-OverDefense.LICENSE.txt'
        [IO.File]::Copy((Assert-PayloadFile $PayloadRoot $licenseName),(Join-Path $scriptDirectory $licenseName))
        $backupLicense=@'
:BACKUP_AGENTS_AND_RTK
if exist "%CODEX_HOME_DIR%\HERO-Anti-OverDefense.LICENSE.txt" (
    copy /y "%CODEX_HOME_DIR%\HERO-Anti-OverDefense.LICENSE.txt" "%BACKUP_DIR%\HERO-Anti-OverDefense.LICENSE.txt" >nul
    if errorlevel 1 exit /b 1
)
> "%BACKUP_DIR%\hero-license-managed.txt" echo 1
if errorlevel 1 exit /b 1
'@
        $sourceText=$sourceText.Replace(":BACKUP_AGENTS_AND_RTK`r`n",$backupLicense.TrimEnd()+"`r`n")
    }
    $restoreLicense=@'
if exist "%SESSION_DIR%\Backups\hero-license-managed.txt" (
    if exist "%SESSION_DIR%\Backups\HERO-Anti-OverDefense.LICENSE.txt" (
        copy /y "%SESSION_DIR%\Backups\HERO-Anti-OverDefense.LICENSE.txt" "%CODEX_HOME_DIR%\HERO-Anti-OverDefense.LICENSE.txt" >nul
        if errorlevel 1 exit /b 1
    ) else if exist "%CODEX_HOME_DIR%\HERO-Anti-OverDefense.LICENSE.txt" (
        del /q "%CODEX_HOME_DIR%\HERO-Anti-OverDefense.LICENSE.txt" >nul
        if errorlevel 1 exit /b 1
    )
)
'@
    $sourceText=$sourceText.Replace('if "!RESTORE_RTK!"=="1" (',$restoreLicense.TrimEnd()+"`r`n"+'if "!RESTORE_RTK!"=="1" (')
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
    param([Parameter(Mandatory=$true)][int]$ProxyPortValue,[AllowNull()][string]$OriginalText,[string]$ProxyHostValue='127.0.0.1')
    $path = Join-Path (Get-CodexHomePath) '.env'
    $current = if ($null -ne $OriginalText) { $OriginalText } else { Read-Utf8FileSafe $path }
    $lines = if ($null -eq $current) { @() } else { @($current -split "\r?\n") }
    $kept = @($lines | Where-Object { $_ -notmatch '(?i)^\s*(HTTP_PROXY|HTTPS_PROXY|ALL_PROXY|NO_PROXY)\s*=' })
    if ($ProxyPortValue -gt 0) {
        $proxy = Get-ProxyUrl $ProxyPortValue $ProxyHostValue
        $kept += 'HTTP_PROXY=' + $proxy
        $kept += 'HTTPS_PROXY=' + $proxy
    }
    $kept += 'NO_PROXY=localhost,127.0.0.1,::1'
    $text = (($kept | Where-Object { $null -ne $_ }) -join [Environment]::NewLine).TrimEnd() + [Environment]::NewLine
    Write-Utf8NoBomFile $path $text
    return $path
}
function Set-TransientProxyEnvironment {
    param([Parameter(Mandatory=$true)][int]$ProxyPortValue,[string]$ProxyHostValue='127.0.0.1')
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
        $proxy = Get-ProxyUrl $ProxyPortValue $ProxyHostValue
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
            $pattern = '(?ms)^[ \t]*BEGIN CODEX LUNA PROMPT V[^\r\n]*\r?\n.*?^[ \t]*END CODEX LUNA PROMPT V[^\r\n]*\r?$'
            $evaluator = [System.Text.RegularExpressions.MatchEvaluator]{
                param($match)
                $block = $match.Value
                $block = [regex]::Replace($block,'(?m)^([ \t]*model\s*=\s*).*$',[System.Text.RegularExpressions.MatchEvaluator]{ param($m) $m.Groups[1].Value + [string]$resolved.childModel })
                $block = [regex]::Replace($block,'(?m)^([ \t]*model_reasoning_effort\s*=\s*).*$',[System.Text.RegularExpressions.MatchEvaluator]{ param($m) $m.Groups[1].Value + [string]$resolved.childEffort })
                $block = $block.Replace('This is the GPT5.6 LUNA MAX child configuration. Do not substitute another child model or reasoning effort.', 'Use the configured child model and reasoning effort above for temporary child agents.')
                $block = $block.Replace('This is the GPT6.1 TERRA MAX child configuration. Do not substitute another child model or reasoning effort.', 'Use the configured child model and reasoning effort above for temporary child agents.')
                $block = $block.Replace('Do not create Sol, Terra, GPT-6, Astra, or any other non-Luna child under this protocol.', 'Use the configured child model above; do not substitute another child model.')
                $block = $block.Replace('gpt-5.6-luna',[string]$resolved.childModel)
                $block = $block.Replace('gpt-6.1-terra',[string]$resolved.childModel)
                $block = $block.Replace('gpt-6.1-sol',[string]$resolved.childModel)
                $block = $block.Replace('GPT5.6 LUNA MAX',([string]$resolved.childModel + ' ' + [string]$resolved.childEffort))
                $block = $block.Replace('GPT6.1 TERRA MAX',([string]$resolved.childModel + ' ' + [string]$resolved.childEffort))
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
            CODEX_GUI_MEMORIES=if ([bool]$resolvedOptions.memories) { 'true' } else { 'false' }
            CODEX_GUI_PROXY_HOST=[string]$resolvedOptions.proxyHost
            CODEX_GUI_NETWORK_OPTIONS=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes((@{githubMirrorEnabled=[bool]$resolvedOptions.githubMirrorEnabled;customGithubMirror=[string]$resolvedOptions.customGithubMirror}|ConvertTo-Json -Compress)))
            CODEX_GUI_CUA_REPAIR=if ([bool]$resolvedOptions.cuaRepair) { '1' } else { '0' }
            CODEX_GUI_PARENT_MODEL=[string]$resolvedOptions.parentModel; CODEX_GUI_CHILD_MODEL=[string]$resolvedOptions.childModel
            CODEX_GUI_PARENT_EFFORT=[string]$resolvedOptions.parentEffort; CODEX_GUI_CHILD_EFFORT=[string]$resolvedOptions.childEffort
            CODEX_GUI_ENV_FILE=$envFile; CODEX_GUI_PROXY_URL=Get-ProxyUrl $ProxyPortValue $resolvedOptions.proxyHost
        }
        $result = Invoke-ChildProcess $adapter @('--codex-gui-action',$Action) $PayloadRoot $childEnvironment $script:InputEncoding -ClearProxyEnvironment:($ProxyPortValue -eq 0)
        if ($result.ExitCode -ne 0) { throw ('init.cmd failed with exit code ' + $result.ExitCode) }
        if ($result.Output -match '(?im)RESULT\s*=\s*FAIL|FAIL') { throw 'init.cmd reported a failed result.' }
        if ($Action -eq 'proxy' -or $Action -eq 'one-click') { [void](Set-ProxyEnvironmentFile $ProxyPortValue $originalEnv $resolvedOptions.proxyHost) }
        if ($Action -eq 'one-click') {
            Set-ScopedModelSettings $resolvedOptions
            Write-BackendLine $(if($resolvedOptions.memories){'Codex 记忆：已开启记忆生成与使用；现有记忆文件保留。'}else{'Codex 记忆：已关闭记忆生成与使用；现有记忆文件保留。'})
            Write-BackendLine $(if($resolvedOptions.antiOverDefense){'防止过度防御：已写入 HERO 中文完整规则及实现、交付规则。'}else{'防止过度防御：未勾选，已跳过规则写入。'})
        }
        if ($Action -eq 'proxy') {
            $envFile = Join-Path (Get-CodexHomePath) '.env'
            if (-not (Test-Path -LiteralPath $envFile -PathType Leaf)) { throw '.env was not written by proxy operation.' }
            $envText = [IO.File]::ReadAllText($envFile)
            $proxy = Get-ProxyUrl $ProxyPortValue $resolvedOptions.proxyHost
            if ($ProxyPortValue -eq 0) {
                if ($envText -match '(?im)^\s*(HTTP_PROXY|HTTPS_PROXY|ALL_PROXY)\s*=') { throw 'Proxy operation did not remove proxy environment entries.' }
            } elseif ($envText -notmatch ('(?m)^HTTP_PROXY=' + [regex]::Escape($proxy) + '\r?$') -or
                $envText -notmatch ('(?m)^HTTPS_PROXY=' + [regex]::Escape($proxy) + '\r?$')) { throw 'Proxy operation did not write the requested proxy endpoint.' }
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
    param([Parameter(Mandatory=$true)][string]$PayloadRoot,[int]$ProxyPortValue=0,[string]$SelectedPath)
    $init = Assert-PayloadFile $PayloadRoot 'init.cmd'
    $rollback = if ($SelectedPath) { $SelectedPath } else { Resolve-InitRollbackPath }
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
function Convert-DesktopGuardRuntime {
    param([Parameter(Mandatory=$true)][string]$Code)
    $text=$Code -replace "`r`n","`n"
    if($text.Contains('# CODEX INIT KIT: preserve explicitly selected standalone')){return $text}
    $applyAnchor=@'
    $desktop = Get-DesktopInfo
    if ($desktop.Found -and $desktop.BundleHealthy) {
        return (Set-DesktopNativeMode $State "Desktop 内置运行时完整；standalone 仅作为后备")
'@
    $applyPrefix=@'
    # CODEX INIT KIT: preserve explicitly selected standalone
    $active = Get-UserEnv "CODEX_CLI_PATH"
    if ($State["ActiveMode"] -eq "OfficialCurrent" -and $active -and $State["ActiveCliPath"] -and $active -ieq $State["ActiveCliPath"] -and $Candidate -and $Candidate.Healthy -and $Candidate.Cli) {
        Set-ActiveCli $Candidate.Cli $State "OfficialCurrent"
        return "OfficialCurrent"
    }
'@
    $autoAnchor=@'
        if ($desktop.Found -and $desktop.BundleHealthy) {
            Set-DesktopNativeMode $state "登录自动维护：Desktop 内置运行时完整" | Out-Null
'@
    $autoReplacement=@'
        $active = Get-UserEnv "CODEX_CLI_PATH"
        if ($state["ActiveMode"] -eq "OfficialCurrent" -and $active -and $state["ActiveCliPath"] -and $active -ieq $state["ActiveCliPath"] -and $managed.Healthy) {
            Set-ActiveCli $managed.Cli $state "OfficialCurrent"
        } elseif ($desktop.Found -and $desktop.BundleHealthy) {
            Set-DesktopNativeMode $state "登录自动维护：Desktop 内置运行时完整" | Out-Null
'@
    if(-not $text.Contains($applyAnchor) -or -not $text.Contains($autoAnchor)){throw '启动保护自动维护结构不匹配，无法安全保留 CLI 选择。'}
    return $text.Replace($applyAnchor,($applyPrefix+"`n"+$applyAnchor)).Replace($autoAnchor,$autoReplacement)
}
function Update-DesktopGuardAutoScript {
    $state=Load-State
    if(-not (Convert-ToBoolean $state['Installed'])){return}
    if(-not (Test-Path -LiteralPath $script:AutoScript -PathType Leaf)){throw '启动保护自动维护脚本缺失，无法保证 CLI 选择在登录后保持。'}
    $old=[IO.File]::ReadAllText($script:AutoScript,[Text.Encoding]::UTF8)
    $updated=Convert-DesktopGuardRuntime $old
    if($updated -eq $old){return}
    $tmp=$script:AutoScript+'.'+[guid]::NewGuid().ToString('N')+'.tmp';$backup=$tmp+'.bak'
    try{[IO.File]::WriteAllText($tmp,$updated,[Text.UTF8Encoding]::new($true));[IO.File]::Replace($tmp,$script:AutoScript,$backup)}finally{if(Test-Path -LiteralPath $tmp){Remove-Item -LiteralPath $tmp -Force};if(Test-Path -LiteralPath $backup){Remove-Item -LiteralPath $backup -Force}}
}
function Initialize-GuardRuntime {
    param([Parameter(Mandatory=$true)][string]$GuardPath,[switch]$ForModification)
    $code = Convert-DesktopGuardRuntime (Get-GuardPayloadText $GuardPath)
    $tokens=$null; $errors=$null
    $ast = [Management.Automation.Language.Parser]::ParseInput($code,[ref]$tokens,[ref]$errors)
    if ($errors.Count -gt 0) { throw 'guard.cmd embedded PowerShell payload has syntax errors.' }
    $documents = Get-DocumentsPath
    $script:ErrorActionPreference='Stop'; $script:ProgressPreference='SilentlyContinue'
    $script:ToolVersion='V5.2.4'; $script:SourceCmd=$GuardPath; $script:SourceDir=Split-Path -Parent $GuardPath
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
        try {
            $diagnosticPaths = Get-ManagedPaths
            $diagnosticPackage = Test-CanonicalPackage $diagnosticPaths.Root
            $diagnosticDesktop = Get-DesktopInfo
            Write-BackendLine ('@@DIAGNOSIS@@' + ([ordered]@{
                standaloneHealthy = [bool]$diagnosticPackage.Healthy
                desktopFound = [bool]$diagnosticDesktop.Found
                desktopHealthy = [bool]$diagnosticDesktop.BundleHealthy
            } | ConvertTo-Json -Compress))
        } catch { Write-BackendLog 'Component details unavailable; diagnosis will use the execution result.' }
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
        'guard-current' { [void](Invoke-OfficialCliUpdate $PayloadRoot -OnlyIfMissing); Initialize-GuardRuntime $guard -ForModification; Switch-ToOfficialCurrent | ForEach-Object { Write-BackendLine $_ } | Out-Null }
        'guard-rollback' { Rollback-Protection | ForEach-Object { Write-BackendLine $_ } | Out-Null }
        'guard-uninstall' { Remove-Protection | ForEach-Object { Write-BackendLine $_ } | Out-Null }
        'doctor' { Run-Doctor | ForEach-Object { Write-BackendLine $_ } | Out-Null }
    }
    if($Action -in @('guard-install','guard-current','guard-update')){Update-DesktopGuardAutoScript}
    if ($Action -eq 'doctor') {
        $doctorExitCode = if ($null -eq $global:LASTEXITCODE) { 0 } else { [int]$global:LASTEXITCODE }
        $newDoctorLogs = @(Get-ChildItem -LiteralPath $script:LogDir -Filter 'doctor_*.txt' -File -ErrorAction SilentlyContinue |
            Where-Object { $_.LastWriteTimeUtc -ge $doctorStartedUtc })
        if ($newDoctorLogs.Count -eq 0) { throw 'Codex doctor did not produce a new diagnostic log.' }
        if ($doctorExitCode -ne 0) {
            $newestDoctorLog = $newDoctorLogs | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
            $doctorReport = [IO.File]::ReadAllText($newestDoctorLog.FullName)
            if ($doctorExitCode -eq 1 -and $doctorReport -match '(?im)^\s*\d+\s+ok\s*\|[^\r\n]*\d+\s+fail\b') {
                Write-BackendLog 'Doctor 已完成检查，报告中存在失败项；请查看诊断建议。'
                return 1
            }
            throw ('codex doctor failed with native exit code ' + $doctorExitCode + '.')
        }
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
    $rootText=($text -split '(?m)^\s*\[',2)[0]
    return [pscustomobject]@{Path=$path;Exists=(Test-Path -LiteralPath $path -PathType Leaf);Model=(Get-ConfigValue $rootText 'model');Effort=(Get-ConfigValue $rootText 'model_reasoning_effort')}
}
function Get-ReadOnlyProtectionInstalled {
    $statePath=Join-Path (Get-GuardInstallRootPath) 'State\guard-state.json'
    if(-not(Test-Path -LiteralPath $statePath -PathType Leaf)){return $false}
    try{$state=Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json;return (Convert-ToBoolean (Get-ObjectPropertyValue $state 'Installed'))}catch{return $false}
}
function Get-PersistedRuntimeOverride {
    param([string]$Name)
    foreach ($scope in @('User','Machine')) {
        $value = [Environment]::GetEnvironmentVariable($Name,$scope)
        if (-not [string]::IsNullOrWhiteSpace($value)) { return [string]$value }
    }
    return ''
}
function Resolve-RuntimeModeInfo {
    param([AllowNull()][string]$CliPath,[AllowNull()][string]$HostPath,[AllowNull()][object]$State,[string]$StandaloneRoot,[AllowNull()][string]$DesktopInstallLocation)
    $info = [ordered]@{ Mode='Unknown'; Detail='尚未确认启动模式'; CliPath=$CliPath }
    if (-not [string]::IsNullOrWhiteSpace($HostPath)) {
        $info.Detail = '存在 Code Mode Host 路径覆盖，请先检查启动诊断。'
        return $info
    }
    if ([string]::IsNullOrWhiteSpace($CliPath)) {
        $info.Mode = 'DesktopNative'
        $info.Detail = '使用 Codex 桌面应用自带的 CLI。'
        if ([string]::IsNullOrWhiteSpace($DesktopInstallLocation)) { $info.Detail += ' 当前未检测到桌面应用。' }
        return $info
    }
    if (-not (Test-Path -LiteralPath $CliPath -PathType Leaf)) {
        $info.Detail = '配置的 CLI 路径不存在，请在维护恢复中检查或修复。'
        return $info
    }
    try {
        $path = [IO.Path]::GetFullPath($CliPath)
        if ($DesktopInstallLocation -and $path.StartsWith([IO.Path]::GetFullPath($DesktopInstallLocation).TrimEnd('\') + '\',[StringComparison]::OrdinalIgnoreCase)) {
            $info.Mode = 'DesktopNative'; $info.Detail = '当前 CLI 路径指向桌面应用自带组件。'
            return $info
        }
        $mode = [string](Get-ObjectPropertyValue $State 'ActiveMode')
        $active = [string](Get-ObjectPropertyValue $State 'ActiveCliPath')
        $stateMatches = $active -and ([IO.Path]::GetFullPath($active) -eq $path) -and $mode -match '^(OfficialCurrent(?:Fallback)?|Rollback(?:Fallback)?)$'
        $rootPrefix = [IO.Path]::GetFullPath($StandaloneRoot).TrimEnd('\') + '\'
        $managedPath = $path.StartsWith($rootPrefix,[StringComparison]::OrdinalIgnoreCase) -and $path.Substring($rootPrefix.Length) -match '^(?:current|releases\\[^\\]+)\\bin\\codex\.exe$'
        if ($managedPath) {
            $info.Mode = 'OfficialStandalone'
            $info.Detail = if ($mode -match '^Rollback' -and $stateMatches) { '使用官方独立 CLI 的回滚版本。' } else { '使用启动保护管理的官方独立 CLI。' }
        } else { $info.Detail = '检测到其他 CLI 路径覆盖，无法确认是否为官方独立 CLI。' }
    } catch { $info.Detail = 'CLI 路径或启动记录异常，请检查启动诊断。' }
    return $info
}
function Get-ReadOnlyRuntimeModeInfo {
    param([string]$ProtectionRoot,[AllowNull()][string]$DesktopInstallLocation)
    $state = $null
    $path = Join-Path $ProtectionRoot 'State\guard-state.json'
    if (Test-Path -LiteralPath $path -PathType Leaf) {
        try { $state = [IO.File]::ReadAllText($path,[Text.Encoding]::UTF8) | ConvertFrom-Json } catch {}
    }
    $standaloneRoot = Join-Path (Get-CodexHomePath) 'packages\standalone'
    return Resolve-RuntimeModeInfo (Get-PersistedRuntimeOverride 'CODEX_CLI_PATH') (Get-PersistedRuntimeOverride 'CODEX_CODE_MODE_HOST_PATH') $state $standaloneRoot $DesktopInstallLocation
}
function Get-ReadOnlyStatus {
    param([string]$PayloadRoot)
    $desktop=Get-ReadOnlyDesktopInfo; $config=Get-ReadOnlyConfigInfo; $backupRoot=Get-BackupRootPath; $latestBackup=Get-LatestBackupPath
    $running=$false; try{$running=[bool](Get-Process -ErrorAction SilentlyContinue | Where-Object{$_.ProcessName -match '^(Codex|ChatGPT)$'})}catch{}
    $protectionRoot=Get-GuardInstallRootPath; $protectionInstalled=Get-ReadOnlyProtectionInstalled
    $runtime = Get-ReadOnlyRuntimeModeInfo $protectionRoot $desktop.InstallLocation
    $rollbackEntries=@();$rollbackError='';$rollbackDeleteId='';$rollbackDeleteCount=0
    try {
        $rollbackEntries=@(Get-RollbackEntries)
        $deletion=Get-RollbackDeletionPlan $rollbackEntries
        $rollbackDeleteId=$deletion.id;$rollbackDeleteCount=$deletion.entries.Count
    } catch { $rollbackError='备份列表读取失败：'+$_.Exception.Message }
    return [ordered]@{
        backendVersion=$script:BackendVersion; desktopVersion=$desktop.Version; desktopHealth=$desktop.Health; desktopHealthy=$desktop.Healthy; desktopFound=$desktop.Found
        desktopInstallLocation=$desktop.InstallLocation; configModel=$config.Model; configEffort=$config.Effort
        config=[ordered]@{model=$config.Model;effort=$config.Effort;path=$config.Path;exists=$config.Exists}
        protectionInstalled=$protectionInstalled; protectionRoot=$protectionRoot; root=$PayloadRoot; processRunning=$running
        runtimeMode=$runtime.Mode; runtimeModeDetail=$runtime.Detail; currentCliPath=$runtime.CliPath
        backupPath=if($latestBackup){$latestBackup}else{$backupRoot}; backupRoot=$backupRoot; latestBackupPath=$latestBackup
        statePath=Join-Path $protectionRoot 'State\guard-state.json'
        rollbackEntries=$rollbackEntries; rollbackError=$rollbackError; rollbackDeleteId=$rollbackDeleteId; rollbackDeleteCount=$rollbackDeleteCount
    }
}
function Initialize-ConfigCompatibility {
    if(-not ('CodexKit.ConfigCompatibility' -as [type])){Add-Type -Path (Join-Path $PSScriptRoot 'ConfigCompatibility.cs')}
}
function Invoke-ConfigCompatibility {
    param([switch]$Clean,[string]$ProjectPath)
    Initialize-ConfigCompatibility
    $report = [CodexKit.ConfigCompatibility]::Scan((Get-CodexHomePath),$ProjectPath)
    if ($Clean) {
        [CodexKit.ConfigCompatibility]::Clean($report,(Get-BackupRootPath))
        $after = [CodexKit.ConfigCompatibility]::Scan((Get-CodexHomePath),$ProjectPath)
        $after.Removed = $report.Removed; $after.BackupPath = $report.BackupPath
        $report = $after
    }
    $summary = $report.Summary()
    foreach ($line in ($summary -split '\r?\n')) { Write-BackendLine $line }
    Write-BackendLine ('@@CONFIG_COMPAT@@' + (@{summary=$summary;count=$report.Count;removed=$report.Removed;backupPath=$report.BackupPath;hasErrors=$report.HasErrors} | ConvertTo-Json -Compress))
    if ($report.HasErrors -or @($report.Files | ForEach-Object { $_.Findings } | Where-Object { -not $_.CanClean }).Count -gt 0) { return 1 }
    return 0
}
function Invoke-BackendMain {
    param([string]$RequestedOperation,[string]$RequestedPayloadRoot,[string]$RequestedDrive,[int]$RequestedProxyPort=10808,[switch]$RequestedInstallProtection,[string]$RequestedJobFile,[string]$RequestedOptionsBase64)
    Set-BackendOutputEncoding
    $proxySnapshot = $null
    try {
        $input=Resolve-BackendInput -RequestedOperation $RequestedOperation -RequestedPayloadRoot $RequestedPayloadRoot -RequestedDrive $RequestedDrive -RequestedProxyPort $RequestedProxyPort -RequestedInstallProtection:$RequestedInstallProtection.IsPresent -RequestedJobFile $RequestedJobFile -RequestedOptionsBase64 $RequestedOptionsBase64
        if ($input.Operation -notin @('status','initialize-preflight','config-check','config-cleanup','rollback-delete-all')) { $proxySnapshot = Set-TransientProxyEnvironment $input.ProxyPort $input.Options.proxyHost }
        switch($input.Operation){
            'status' { Write-BackendStatus (Get-ReadOnlyStatus $input.PayloadRoot); return 0 }
            'initialize-preflight' { Write-BackendLine ('@@INIT_PREFLIGHT@@'+((Get-InitializationPreflight $input.PayloadRoot)|ConvertTo-Json -Compress -Depth 8)); return 0 }
            'config-check' { return (Invoke-ConfigCompatibility -ProjectPath $input.Options.projectPath) }
            'config-cleanup' { return (Invoke-ConfigCompatibility -Clean -ProjectPath $input.Options.projectPath) }
            'initialize' { return (Invoke-Initialize220 $input) }
            'desktop-update' { [void](Invoke-DesktopUpdate $input.PayloadRoot); return 0 }
            'official-cli-update' { return (Invoke-OfficialCliUpdate $input.PayloadRoot) }
            'browser-check' { return (Invoke-BrowserOperation 'check' $input.PayloadRoot $input.ProxyPort $input.Options) }
            'browser-repair' { return (Invoke-BrowserOperation 'repair' $input.PayloadRoot $input.ProxyPort $input.Options) }
            'browser-rollback' { return (Invoke-BrowserOperation 'rollback' $input.PayloadRoot $input.ProxyPort $input.Options) }
            'browser-start-prepare' { return (Invoke-BrowserStartWorker $input.PayloadRoot $input.ProxyPort $input.Options) }
            'browser-start-verify' { return (Invoke-BrowserStartWorker $input.PayloadRoot 0 $input.Options -VerifyOnly) }
            'desktop-core-check' { return (Invoke-DesktopCoreOperation 'check' $input.PayloadRoot) }
            'desktop-core-repair' { return (Invoke-DesktopCoreOperation 'repair' $input.PayloadRoot) }
            'proxy' { return (Invoke-InitOperation 'proxy' $input.PayloadRoot $null $input.ProxyPort $input.Options) }
            'init-rollback' { return (Invoke-InitRollback $input.PayloadRoot $input.ProxyPort) }
            'rollback-selected' { return (Invoke-SelectedRollback $input.PayloadRoot $input.Options.rollbackId $input.ProxyPort) }
            'rollback-delete-all' { return (Remove-RollbackEntries $input.PayloadRoot $input.Options.rollbackId) }
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
. (Join-Path $PSScriptRoot 'Backend220.ps1')
. (Join-Path $PSScriptRoot 'InitializationPreflight.ps1')
. (Join-Path $PSScriptRoot 'DesktopCoreRecovery.ps1')

# Dot-sourcing exposes helpers for adapter tests without starting a worker.
if ($MyInvocation.InvocationName -ne '.') {
    exit (Invoke-BackendMain -RequestedOperation $Operation -RequestedPayloadRoot $PayloadRoot -RequestedDrive $Drive -RequestedProxyPort $ProxyPort -RequestedInstallProtection:$InstallProtection.IsPresent -RequestedJobFile $JobFile -RequestedOptionsBase64 $OptionsBase64)
}
