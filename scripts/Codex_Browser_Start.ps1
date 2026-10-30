param([int]$Port=10808,[string]$ProxyHost='127.0.0.1',[switch]$CheckOnly,[switch]$PrepareOnly)
$ErrorActionPreference='Stop'
$requested=@{Port=$Port;ProxyHost=$ProxyHost;CheckOnly=$CheckOnly;PrepareOnly=$PrepareOnly}
$repo=Split-Path $PSScriptRoot -Parent
$payload=Join-Path ([IO.Path]::GetTempPath()) ('CodexKit-startup-'+[guid]::NewGuid().ToString('N'))
$mutex=$null;$locked=$false
try {
    . (Join-Path $repo 'app\Backend.ps1')
    . (Join-Path $PSScriptRoot 'BrowserStartup.ps1')
    [void][IO.Directory]::CreateDirectory($payload)
    Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Codex_CUA_Repair.cmd') -Destination (Join-Path $payload 'cua.cmd')
    Import-CuaLibrary $payload
    $script:LogFile=$null
    $sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $mutex=[Threading.Mutex]::new($false,('Local\CodexCUARepair-'+$sid))
    try{$locked=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$locked=$true}
    if(-not $locked){throw 'Another CUA/browser operation is running.'}
    # Invoke-BrowserOperation uses the same reentrant mutex on this thread.
    $rc=Invoke-BrowserStartup -PayloadRoot $payload @requested
    exit $rc
}catch{Write-Host ('STOP: '+$_.Exception.Message) -ForegroundColor Red;exit 1}
finally{
    if($locked -and $mutex){$mutex.ReleaseMutex()};if($mutex){$mutex.Dispose()}
    $safeRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\')+'\'
    if([IO.Path]::GetFullPath($payload).StartsWith($safeRoot,[StringComparison]::OrdinalIgnoreCase) -and (Test-Path -LiteralPath $payload)){Remove-Item -LiteralPath $payload -Recurse -Force}
}
