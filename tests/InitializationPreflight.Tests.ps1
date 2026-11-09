param([string]$PayloadRoot)
$ErrorActionPreference='Stop'
. (Join-Path $PayloadRoot 'Backend.ps1')
function Check($Value,[string]$Message){if(-not $Value){throw $Message}}
$desktop='C:\Official\Codex';$runtime='C:\User\cua';$owner='S-1-5-21-fixture'
function Entry($Id,$Parent,$Path,$Sid=$owner,$Session=7){return [pscustomobject]@{Id=$Id;ParentId=$Parent;Path=$Path;OwnerSid=$Sid;SessionId=$Session;Started=[string]$Id;Name=[IO.Path]::GetFileNameWithoutExtension($Path)}}
$inventory=@((Entry 1 0 "$desktop\ChatGPT.exe"),(Entry 2 1 'C:\External\node.exe'),(Entry 3 1 'C:\Chrome\chrome.exe'),(Entry 4 0 'C:\Other\ChatGPT.exe'),(Entry 5 0 "$runtime\node.exe"),(Entry 6 0 "$desktop\ChatGPT.exe" 'other-user'),(Entry 7 0 "$desktop\ChatGPT.exe" $owner 8),(Entry 8 0 'C:\Other\node.exe'))
$selected=@(Select-InitializationProcesses $inventory $desktop @($runtime) $owner 7)
Check (($selected.Id -join ',') -eq '1,2,5') 'Process selection included an unrelated browser, user, session or same-named program.'
$orphaned=@(Select-InitializationProcesses @($inventory|Where-Object {$_.Id -ne 1}) $desktop @($runtime) $owner 7 $selected)
Check (($orphaned.Id -join ',') -eq '2,5') 'An authorized runtime was forgotten after its desktop parent exited.'
$script:processFixture=[pscustomobject]@{StartTime=[datetime]::new(639298120645079236,[DateTimeKind]::Utc);SessionId=7;Path="$desktop\ChatGPT.exe"}
$identity=Entry 1 0 "$desktop\ChatGPT.exe";$identity.Started='639298120645079230'
try {
    function Get-Process {return $script:processFixture}
    Check ($null -ne (Get-InitializationProcess $identity)) 'CIM microsecond precision rejected the original process.'
    $script:processFixture.StartTime=$script:processFixture.StartTime.AddTicks(10);$reused=$false
    try{[void](Get-InitializationProcess $identity)}catch{$reused=$true}
    Check $reused 'A reused process identity was accepted.'
} finally {Remove-Item Function:Get-Process}
$saved=@{};foreach($name in @('Get-InitializationProcesses','Request-InitializationExit','Stop-InitializationProcess','Wait-InitializationExit','Write-BackendLine')){$saved[$name]=(Get-Item ('Function:'+$name)).ScriptBlock}
try {
    $script:entries=@($selected);$script:normal=0;$script:forced=0;$script:waitStage=0;$script:scenario='graceful'
    function Get-InitializationProcesses {return $script:entries}
    function Request-InitializationExit {param($Entry) $script:normal++}
    function Stop-InitializationProcess {param($Entry) $script:forced++;if($script:scenario -eq 'denied'){throw 'Access denied fixture'}}
    function Wait-InitializationExit {param($Seconds) $script:waitStage++;if($script:scenario -eq 'graceful' -or ($script:scenario -eq 'forced' -and $Seconds -eq 10)){return @()};if($script:scenario -eq 'reopened'){return @((Entry 99 0 "$desktop\ChatGPT.exe"))};return $script:entries}
    function Write-BackendLine {}
    $rejected=$false;try{Stop-InitializationApps $PayloadRoot $false}catch{$rejected=$_.Exception.Message.Contains('关闭')}
    Check ($rejected -and $normal -eq 0 -and $forced -eq 0) 'Missing consent closed the running application.'
    Stop-InitializationApps $PayloadRoot $true
    Check ($normal -eq 3 -and $forced -eq 0) 'Graceful exit forced processes unnecessarily.'
    $script:normal=0;$script:forced=0;$script:scenario='forced';Stop-InitializationApps $PayloadRoot $true
    Check ($normal -eq 3 -and $forced -eq 3) 'Confirmed remaining processes were not closed.'
    foreach($case in @('denied','stuck','reopened')){
        $script:scenario=$case;$script:forced=0;$rejected=$false
        try{Stop-InitializationApps $PayloadRoot $true}catch{$rejected=$true}
        Check $rejected ('Initialization continued after '+$case)
        if($case -eq 'reopened'){Check ($forced -eq 0) 'A reopened app was silently force-closed.'}
    }
    $script:entries=@();$script:normal=0;Stop-InitializationApps $PayloadRoot $false
    Check ($normal -eq 0) 'No-running-app preflight attempted closure.'
    Write-Output 'PASS initialization preflight selection, consent, graceful exit, force, denied, timeout and reopen fixtures.'
} finally {foreach($name in $saved.Keys){Set-Item ('Function:'+$name) $saved[$name]}}
