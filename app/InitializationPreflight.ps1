# Initialization-only permission preservation and explicitly approved app exit.
function Import-PermissionSettings {
    param([string]$PayloadRoot)
    if(-not ('CodexKit.PermissionSettings' -as [type])){
        Add-Type -Path @((Join-Path $PayloadRoot 'ConfigCompatibility.cs'),(Join-Path $PayloadRoot 'PermissionSettings.cs'))
    }
}
function Get-InitializationPermissions {
    param([string]$PayloadRoot)
    Import-PermissionSettings $PayloadRoot
    return [CodexKit.PermissionSettings]::Load((Get-CodexHomePath))
}
function Select-InitializationProcesses {
    param([object[]]$Processes,[string]$InstallLocation,[string[]]$RuntimeRoots,[string]$OwnerSid,[int]$SessionId,[object[]]$Tracked=@())
    $roots=@($InstallLocation)+@($RuntimeRoots)|Where-Object {$_}|ForEach-Object { [IO.Path]::GetFullPath($_).TrimEnd('\')+'\' }
    $candidates=@($Processes|Where-Object {$_.SessionId -eq $SessionId -and $_.OwnerSid -eq $OwnerSid})
    $selected=[Collections.Generic.HashSet[int]]::new()
    foreach($entry in $candidates){
        if(@($Tracked|Where-Object {$_.Id -eq $entry.Id -and $_.Started -eq $entry.Started -and $_.Path -eq $entry.Path}).Count){[void]$selected.Add($entry.Id)}
    }
    foreach($entry in $candidates){foreach($root in $roots){if($entry.Path -and $entry.Path.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){[void]$selected.Add($entry.Id);break}}}
    do {
        $changed=$false
        foreach($entry in $candidates){
            if($selected.Contains($entry.ParentId) -and $entry.Name -in @('ChatGPT','codex','node','cua_node','node_repl','cua-helper','codex-code-mode-host','codex-command-runner','codex-windows-sandbox-setup')){
                if($selected.Add($entry.Id)){$changed=$true}
            }
        }
    }while($changed)
    return @($candidates|Where-Object {$selected.Contains($_.Id)})
}
function Get-InitializationProcesses {
    param([string]$PayloadRoot,[object[]]$Tracked=@())
    $desktop=Get-ReadOnlyDesktopInfo
    $sessionId=[Diagnostics.Process]::GetCurrentProcess().SessionId
    $owner=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $runtimeRoots=@((Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\runtimes\cua_node'),(Join-Path (Get-CodexHomePath) 'packages\standalone'))
    $knownRoots=@($desktop.InstallLocation)+$runtimeRoots|Where-Object {$_}|ForEach-Object {$_.TrimEnd('\')+'\'}
    $inventory=@(Get-CimInstance Win32_Process -Filter ('SessionId = '+$sessionId) -ErrorAction Stop)
    # Query owners only for app files and known runtime names.
    $entries=foreach($process in $inventory){
        $name=[IO.Path]::GetFileNameWithoutExtension($process.Name)
        $inAppPath=@($knownRoots|Where-Object {$process.ExecutablePath -and $process.ExecutablePath.StartsWith($_,[StringComparison]::OrdinalIgnoreCase)}).Count -gt 0
        if(-not $inAppPath -and $name -notin @('ChatGPT','codex','node','cua_node','node_repl','cua-helper','codex-code-mode-host','codex-command-runner','codex-windows-sandbox-setup')){continue}
        $sid=Invoke-CimMethod -InputObject $process -MethodName GetOwnerSid -ErrorAction Stop
        if($sid.ReturnValue -ne 0){throw ('无法确认进程所属用户：'+$process.Name+'（PID '+$process.ProcessId+'）。请手动退出后重试。')}
        [pscustomobject]@{Id=[int]$process.ProcessId;ParentId=[int]$process.ParentProcessId;Path=[string]$process.ExecutablePath;OwnerSid=[string]$sid.Sid;SessionId=[int]$process.SessionId;Started=[string]$process.CreationDate.ToUniversalTime().Ticks;Name=$name}
    }
    return @(Select-InitializationProcesses @($entries) $desktop.InstallLocation $runtimeRoots $owner $sessionId $Tracked)
}
function Get-InitializationProcess {
    param($Entry)
    $process=Get-Process -Id $Entry.Id -ErrorAction SilentlyContinue
    if(-not $process){return $null}
    # Win32_Process timestamps carry microseconds; Process.StartTime has 100 ns ticks.
    $started=$process.StartTime.ToUniversalTime().Ticks
    $started-=$started%10
    if([string]$started -ne $Entry.Started -or $process.SessionId -ne $Entry.SessionId -or -not [string]::Equals($process.Path,$Entry.Path,[StringComparison]::OrdinalIgnoreCase)){
        throw '应用进程已变化，初始化未开始。请重新确认后再试。'
    }
    return $process
}
function Request-InitializationExit {
    param($Entry)
    $process=Get-InitializationProcess $Entry
    if($process){[void]$process.CloseMainWindow()}
}
function Stop-InitializationProcess {
    param($Entry)
    $process=Get-InitializationProcess $Entry
    if($process){Stop-Process -InputObject $process -Force -ErrorAction Stop}
}
function Wait-InitializationExit {
    param([int]$Seconds,[string]$PayloadRoot,[object[]]$Tracked)
    $watch=[Diagnostics.Stopwatch]::StartNew()
    do {
        $remaining=@(Get-InitializationProcesses $PayloadRoot $Tracked)
        if($remaining.Count -eq 0){return @()}
        if($watch.Elapsed.TotalSeconds -ge $Seconds){return $remaining}
        Start-Sleep -Milliseconds 250
    }while($true)
}
function Stop-InitializationApps {
    param([string]$PayloadRoot,[bool]$Allowed)
    $running=@(Get-InitializationProcesses $PayloadRoot)
    if($running.Count -eq 0){return}
    if(-not $Allowed){throw '检测到 ChatGPT／Codex 仍在运行。请返回确认是否允许关闭，或保存工作后手动退出。'}
    Write-BackendLine '已获得本次关闭授权，正在请求 ChatGPT／Codex 正常退出。'
    foreach($entry in $running){Request-InitializationExit $entry}
    $remaining=@(Wait-InitializationExit 5 $PayloadRoot $running)
    if($remaining.Count){
        foreach($entry in $remaining){
            if(-not @($running|Where-Object {$_.Id -eq $entry.Id -and $_.Started -eq $entry.Started}).Count){throw 'ChatGPT／Codex 已重新启动，初始化未开始。请退出应用后重新确认。'}
        }
        Write-BackendLine '正在结束已授权关闭的残留进程。'
        foreach($entry in $remaining){Stop-InitializationProcess $entry}
        if(@(Wait-InitializationExit 10 $PayloadRoot $running).Count){throw '无法彻底关闭 ChatGPT／Codex，初始化未开始。请手动退出后重试。'}
    }
    Write-BackendLine 'ChatGPT／Codex 已退出，正在重新读取初始化配置。'
}
function Get-InitializationPreflight {
    param([string]$PayloadRoot,$Options)
    $permissions=if($Options.inheritPermissions){Get-InitializationPermissions $PayloadRoot}else{$null}
    return @{processes=@(Get-InitializationProcesses $PayloadRoot);permissionSummary=if($permissions){$permissions.Summary}else{'未启用权限继承。'};permissionSource=Join-Path (Get-CodexHomePath) 'config.toml'}
}
