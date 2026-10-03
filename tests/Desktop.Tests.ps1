[CmdletBinding()]
param([string]$Executable,[string]$ReportDirectory,[switch]$CheckLiveUpdate)
$ErrorActionPreference='Stop'
if(-not $Executable){$Executable=Join-Path (Split-Path $PSScriptRoot -Parent) 'build\desktop\CodexInitKit.exe'}
if(-not $ReportDirectory){$ReportDirectory=Join-Path $env:TEMP ('CodexKit-tests-'+[Guid]::NewGuid().ToString('N'))}
[void][IO.Directory]::CreateDirectory($ReportDirectory)
function Invoke-DesktopCli([string]$Action,[string]$Report) {
    $info=New-Object Diagnostics.ProcessStartInfo
    $info.FileName=$Executable;$info.Arguments=$Action+' "'+$Report+'"'
    $info.UseShellExecute=$false;$info.CreateNoWindow=$true
    $process=New-Object Diagnostics.Process
    $process.StartInfo=$info
    try {
        [void]$process.Start()
        if(-not $process.WaitForExit(60000)){throw ('CLI timed out: '+$Action)}
        if($process.ExitCode -ne 0){throw ('CLI failed: '+$Action+' ('+$process.ExitCode+'); report '+$Report)}
    } finally {$process.Dispose()}
}
$verify=Join-Path $ReportDirectory 'verify.json'
Invoke-DesktopCli '--verify' $verify
$result=[IO.File]::ReadAllText($verify,[Text.Encoding]::UTF8)|ConvertFrom-Json
if(-not $result.passed){throw 'Built-in verification reported failure.'}
$status=Join-Path $ReportDirectory 'status.json'
Invoke-DesktopCli '--status-json' $status
$state=[IO.File]::ReadAllText($status,[Text.Encoding]::UTF8)|ConvertFrom-Json
if($state.exit_code -ne 0 -or $null -eq $state.status){throw 'No valid status report.'}
if($CheckLiveUpdate){Invoke-DesktopCli '--check-update-json' (Join-Path $ReportDirectory 'update.json')}
Write-Output ('PASS: headless resources, PowerShell syntax, page navigation, worker input, update validation, atomic backup and read-only status. Reports: '+$ReportDirectory)
