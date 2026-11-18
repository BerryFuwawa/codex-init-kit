param([string]$PayloadRoot)
$ErrorActionPreference='Stop'
. (Join-Path $PayloadRoot 'Backend.ps1')
function Check($Value,[string]$Message){if(-not $Value){throw $Message}}
$desktop='C:\Official\Codex';$runtime='C:\User\cua';$relocated='C:\User\bin';$standalone='C:\User\standalone';$owner='S-1-5-21-fixture'
function Entry($Id,$Parent,$Path,$Sid=$owner,$Session=7){[pscustomobject]@{Id=$Id;ParentId=$Parent;Path=$Path;OwnerSid=$Sid;SessionId=$Session;Started=[string]$Id;Name=[IO.Path]::GetFileNameWithoutExtension($Path)}}
$inventory=@((Entry 1 0 "$desktop\app\ChatGPT.exe"),(Entry 2 1 "$runtime\node.exe"),(Entry 3 1 "$relocated\codex.exe"),(Entry 4 0 'C:\Other\ChatGPT.exe'),(Entry 5 0 "$runtime\node.exe"),(Entry 6 1 'C:\Other\node.exe'),(Entry 7 0 "$desktop\app\ChatGPT.exe" 'other-user'),(Entry 8 0 "$desktop\app\ChatGPT.exe" $owner 8),(Entry 9 0 "$standalone\codex.exe"),(Entry 10 1 "$standalone\codex.exe"))
$roots=@($runtime,$relocated,$standalone)
$selected=@(Select-InitializationProcesses $inventory $desktop $roots $owner 7)
Check (($selected.Id -join ',') -eq '1,2,3,10') 'Unrelated process or independent CLI selected.'
$orphaned=@(Select-InitializationProcesses @($inventory|Where-Object {$_.Id -ne 1}) $desktop $roots $owner 7 $selected)
Check (($orphaned.Id -join ',') -eq '2,3,10') 'Approved orphaned children were forgotten.'
$script:processFixture=[pscustomobject]@{HasExited=$false;SafeHandle=1}
$script:forceCalls=0
$script:processFixture|Add-Member ScriptMethod Dispose {}
$script:processFixture|Add-Member ScriptMethod Kill {$script:forceCalls++;$this.HasExited=$true}
$script:cimFixture=[pscustomobject]@{CreationDate=[datetime]::new(639298120645079230,[DateTimeKind]::Utc);SessionId=7;ExecutablePath="$desktop\app\ChatGPT.exe"}
$identity=Entry 1 0 "$desktop\app\ChatGPT.exe";$identity.Started='639298120645079230'
try {
    function Get-Process {return $script:processFixture}
    function Get-CimInstance {return $script:cimFixture}
    function Invoke-CimMethod {return [pscustomobject]@{ReturnValue=0;Sid=$owner}}
    function Write-BackendLine {}
    Check ($null -ne (Get-InitializationProcess $identity)) 'Original process rejected.'
    Invoke-InitializationProcessExit $identity
    Check ($forceCalls -eq 1 -and $processFixture.HasExited) 'Direct force was not used.'
    Invoke-InitializationProcessExit $identity
    Check ($forceCalls -eq 1) 'Already-exited target was forced again.'
    $script:processFixture.HasExited=$false
    $script:cimFixture.CreationDate=$script:cimFixture.CreationDate.AddTicks(10);$rejected=$false
    try{[void](Get-InitializationProcess $identity)}catch{$rejected=$_.Exception.Message.Contains('重新启动')}
    Check $rejected 'Reused process accepted.'
    $script:cimFixture.CreationDate=$script:cimFixture.CreationDate.AddTicks(-10)
    $script:cimFixture.ExecutablePath=$null;$rejected=$false
    try{[void](Get-InitializationProcess $identity)}catch{$rejected=$_.Exception.Message.Contains('无法核验')}
    Check $rejected 'Missing target path accepted.'
    $script:cimFixture.ExecutablePath=$identity.Path
    function Invoke-CimMethod {return [pscustomobject]@{ReturnValue=0;Sid='other-user'}}
    $rejected=$false;try{[void](Get-InitializationProcess $identity)}catch{$rejected=$_.Exception.Message.Contains('所属用户')}
    Check $rejected 'Changed owner accepted.'
    function Invoke-CimMethod {return [pscustomobject]@{ReturnValue=2;Sid=$null}}
    $rejected=$false;try{[void](Get-InitializationProcess $identity)}catch{$rejected=$_.Exception.Message.Contains('所属用户')}
    Check $rejected 'Ownership permission failure accepted.'
    function Get-CimInstance {$script:processFixture.HasExited=$true;return $script:cimFixture}
    Check ($null -eq (Get-InitializationProcess $identity)) 'Natural exit during verification rejected.'
}finally{Remove-Item Function:Get-Process,Function:Get-CimInstance,Function:Invoke-CimMethod}
$denied=New-InitializationProcessError '强制结束' 'ChatGPT（PID 1）' ([ComponentModel.Win32Exception]::new(5))
Check ($denied.Message.Contains('PID 1') -and $denied.Message.Contains('Windows 错误码 5')) 'Permission failure lost its target or code.'
$saved=@{};foreach($name in @('Get-InitializationProcesses','Invoke-InitializationProcessExit','Wait-InitializationExit','Write-BackendLine')){$saved[$name]=(Get-Item ('Function:'+$name)).ScriptBlock}
try {
    $script:entries=@($selected);$script:forced=0;$script:scenario='complete';$script:trackedCount=0
    $approved=($selected|ForEach-Object {$_.Id.ToString()+':'+$_.Started}) -join ','
    function Get-InitializationProcesses {return $script:entries}
    function Invoke-InitializationProcessExit {param($Entry) $script:forced++;if($scenario -eq 'denied'){throw $denied}}
    function Wait-InitializationExit {param($Seconds,$PayloadRoot,$Tracked) $script:trackedCount=@($Tracked).Count;Check ($Seconds -eq 10) 'Wrong force confirmation timeout.';if($scenario -eq 'complete'){return @()};if($scenario -eq 'reopened'){return @((Entry 99 0 "$desktop\app\ChatGPT.exe"))};return $script:entries}
    function Write-BackendLine {}
    $rejected=$false;try{Stop-InitializationApps $PayloadRoot $false}catch{$rejected=$_.Exception.Message.Contains('关闭')}
    Check ($rejected -and $forced -eq 0) 'Missing consent terminated an app.'
    $rejected=$false;try{Stop-InitializationApps $PayloadRoot $true '1:old'}catch{$rejected=$_.Exception.Message.Contains('重新启动')}
    Check ($rejected -and $forced -eq 0) 'Changed approval terminated an app.'
    Stop-InitializationApps $PayloadRoot $true $approved
    Check ($forced -eq 4 -and $trackedCount -eq 4) 'Force or tracked confirmation skipped a target.'
    foreach($case in @('denied','stuck','reopened')){
        $script:scenario=$case;$script:forced=0;$rejected=$false
        try{Stop-InitializationApps $PayloadRoot $true $approved}catch{$expected=switch($case){'denied'{'权限不足'}'stuck'{'超过 10 秒'}'reopened'{'重新启动'}};$rejected=$_.Exception.Message.Contains($expected)}
        Check $rejected ('Initialization continued after '+$case)
        if($case -eq 'reopened'){Check ($forced -eq 4) 'A new instance was silently killed.'}
    }
    $script:entries=@();$script:forced=0;Stop-InitializationApps $PayloadRoot $false
    Check ($forced -eq 0) 'Empty target list caused force.'
    Write-Output 'PASS exact targets, retained children, direct force, consent, owner, gone, denied, timeout and reopen fixtures.'
}finally{foreach($name in $saved.Keys){Set-Item ('Function:'+$name) $saved[$name]}}
