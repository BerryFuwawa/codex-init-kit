# Initialization preflight and explicitly approved app exit.
function Select-InitializationProcesses {
    param([object[]]$Processes,[string]$InstallLocation,[string[]]$RuntimeRoots,[string]$OwnerSid,[int]$SessionId,[object[]]$Tracked=@())
    $roots=@($InstallLocation)+@($RuntimeRoots)|Where-Object {$_}|ForEach-Object { [IO.Path]::GetFullPath($_).TrimEnd('\')+'\' }
    $candidates=@($Processes|Where-Object {$_.SessionId -eq $SessionId -and $_.OwnerSid -eq $OwnerSid})
    $selected=[Collections.Generic.HashSet[int]]::new()
    foreach($entry in $candidates){
        if(@($Tracked|Where-Object {$_.Id -eq $entry.Id -and $_.Started -eq $entry.Started}).Count){[void]$selected.Add($entry.Id)}
    }
    foreach($entry in $candidates){foreach($root in $roots){if($entry.Path -and $entry.Path.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)){[void]$selected.Add($entry.Id);break}}}
    do {
        $changed=$false
        foreach($entry in $candidates){
            if($selected.Contains($entry.ParentId) -and $entry.Name -in @('ChatGPT','codex','codex-latest','node','cua_node','node_repl','cua-helper','codex-code-mode-host','codex-command-runner','codex-windows-sandbox-setup')){
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
    $inventory=@(Get-CimInstance Win32_Process -Filter ('SessionId = '+$sessionId) -ErrorAction Stop)
    # Find plausible application processes before asking Windows about ownership.
    $candidates=foreach($process in $inventory){
        [pscustomobject]@{
            Id=[int]$process.ProcessId;ParentId=[int]$process.ParentProcessId;Path=[string]$process.ExecutablePath
            OwnerSid=$owner;SessionId=[int]$process.SessionId
            Started=if($process.CreationDate){[string]$process.CreationDate.ToUniversalTime().Ticks}else{''}
            Name=[IO.Path]::GetFileNameWithoutExtension($process.Name);CimProcess=$process
        }
    }
    $entries=foreach($entry in (Select-InitializationProcesses @($candidates) $desktop.InstallLocation $runtimeRoots $owner $sessionId $Tracked)){
        $process=$entry.CimProcess
        $target=$process.Name+'（PID '+$process.ProcessId+'）'
        try{
            $sid=Invoke-CimMethod -InputObject $process -MethodName GetOwnerSid -ErrorAction Stop
            if($sid.ReturnValue -in @(2,3)){throw [UnauthorizedAccessException]::new('无法读取进程所属用户。')}
            if($sid.ReturnValue -ne 0){throw ('无法读取进程所属用户（返回码 '+$sid.ReturnValue+'）。')}
            if($sid.Sid -ne $owner){continue}
            if(-not $entry.Started){throw '无法读取进程启动时间。'}
        }catch{
            $failure=$_.Exception
            if(Test-InitializationProcessGone $process.ProcessId){continue}
            throw (New-InitializationProcessError '检查运行进程' $target $failure)
        }
        [pscustomobject]@{Id=$entry.Id;ParentId=$entry.ParentId;Path=$entry.Path;OwnerSid=[string]$sid.Sid;SessionId=$entry.SessionId;Started=$entry.Started;Name=$entry.Name}
    }
    return @(Select-InitializationProcesses @($entries) $desktop.InstallLocation $runtimeRoots $owner $sessionId $Tracked)
}
function Test-InitializationProcessGone {
    param([int]$Id)
    try{$process=[Diagnostics.Process]::GetProcessById($Id)}
    catch [ArgumentException]{return $true}
    try{return $process.HasExited}
    catch [ComponentModel.Win32Exception]{if($_.Exception.NativeErrorCode -eq 5){return $false};throw}
    finally{$process.Dispose()}
}
function New-InitializationProcessError {
    param([string]$Stage,[string]$Target,[Exception]$Failure)
    $cause=$Failure
    while($cause){
        if($cause -is [UnauthorizedAccessException] -or
            ($cause -is [ComponentModel.Win32Exception] -and $cause.NativeErrorCode -eq 5)){
            return [InvalidOperationException]::new("$Stage：$Target 权限不足，初始化未开始。", $Failure)
        }
        $cause=$cause.InnerException
    }
    return [InvalidOperationException]::new("$Stage：$Target $($Failure.Message)", $Failure)
}
function Get-InitializationProcess {
    param($Entry)
    try{$process=Get-Process -Id $Entry.Id -ErrorAction Stop}
    catch{if($_.FullyQualifiedErrorId -like 'NoProcessFoundForGivenId,*'){return};throw}
    $keep=$false
    try{
        if($process.HasExited){return}
        # Hold this instance while checking the same CIM identity used by discovery.
        [void]$process.SafeHandle
        $current=Get-CimInstance Win32_Process -Filter ('ProcessId = '+$Entry.Id) -ErrorAction Stop
        if($process.HasExited){return}
        if(!$current -or !$current.CreationDate -or !$current.ExecutablePath -or !$Entry.Path){
            throw '仍在运行，无法核验进程身份。请手动退出后重试。'
        }
        if([string]$current.CreationDate.ToUniversalTime().Ticks -ne $Entry.Started -or
            $current.SessionId -ne $Entry.SessionId -or
            -not [string]::Equals($current.ExecutablePath,$Entry.Path,[StringComparison]::OrdinalIgnoreCase)){
            throw '应用已重新启动或被其他进程替换，初始化未开始。请退出后重新确认。'
        }
        $keep=$true
        return $process
    }finally{
        if(!$keep){$process.Dispose()}
    }
}
function Invoke-InitializationProcessExit {
    param($Entry,[switch]$Force)
    $stage=if($Force){'结束残留进程'}else{'请求正常退出'}
    $target=$Entry.Name+'（PID '+$Entry.Id+'）'
    $process=$null
    try{
        $process=Get-InitializationProcess $Entry
        if(!$process){Write-BackendLine ("${stage}：$target 已退出。");return}
        if($Force){$process.Kill()}else{[void]$process.CloseMainWindow()}
    }catch{
        $failure=$_.Exception
        if(($process -and $process.HasExited) -or (Test-InitializationProcessGone $Entry.Id)){Write-BackendLine ("${stage}：$target 已退出。");return}
        throw (New-InitializationProcessError $stage $target $failure)
    }finally{if($process){$process.Dispose()}}
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
function Assert-InitializationTargetsUnchanged {
    param([object[]]$Remaining,[string[]]$Authorized)
    foreach($entry in $Remaining){
        if(($entry.Id.ToString()+':'+$entry.Started) -cnotin $Authorized){
            throw ('ChatGPT／Codex 已重新启动或出现新关联进程：'+$entry.Name+'（PID '+$entry.Id+'）。初始化未开始，请退出后重新确认。')
        }
    }
}
function Assert-InitializationAppsStopped {
    param([string]$PayloadRoot)
    $running=@(Get-InitializationProcesses $PayloadRoot)
    if($running.Count){
        $targets=($running|ForEach-Object {$_.Name+'（PID '+$_.Id+'）'}) -join '、'
        throw ("应用在修改配置前重新启动或仍在运行：$targets。请保存工作并退出后重新确认。")
    }
}
function Stop-InitializationApps {
    param([string]$PayloadRoot,[bool]$Allowed,[string]$ApprovedProcesses)
    $running=@(Get-InitializationProcesses $PayloadRoot)
    if($running.Count -eq 0){return}
    if(-not $Allowed){throw '检测到 ChatGPT／Codex 仍在运行。请返回确认是否允许关闭，或保存工作后手动退出。'}
    $authorized=$ApprovedProcesses -split ','
    Assert-InitializationTargetsUnchanged $running $authorized
    Write-BackendLine '已获得本次关闭授权，正在请求 ChatGPT／Codex 正常退出。'
    foreach($entry in $running){Invoke-InitializationProcessExit $entry}
    $remaining=@(Wait-InitializationExit 5 $PayloadRoot $running)
    if($remaining.Count){
        Assert-InitializationTargetsUnchanged $remaining $authorized
        Write-BackendLine '正在结束已授权关闭的残留进程。'
        foreach($entry in $remaining){Invoke-InitializationProcessExit $entry -Force}
        $remaining=@(Wait-InitializationExit 10 $PayloadRoot $running)
        if($remaining.Count){
            Assert-InitializationTargetsUnchanged $remaining $authorized
            $targets=($remaining|ForEach-Object {$_.Name+'（PID '+$_.Id+'）'}) -join '、'
            throw ("等待退出超过 10 秒：$targets。初始化未开始，请手动退出后重试。")
        }
    }
    Write-BackendLine 'ChatGPT／Codex 已退出，继续初始化。'
}
function Get-InitializationPreflight {
    param([string]$PayloadRoot)
    return @{processes=@(Get-InitializationProcesses $PayloadRoot)}
}
