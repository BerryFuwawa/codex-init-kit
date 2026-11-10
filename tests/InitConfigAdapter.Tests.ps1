param([string]$PayloadRoot)
$ErrorActionPreference='Stop'
$testPayload=$PayloadRoot
. (Join-Path $testPayload 'Backend.ps1')
function Require($Value,[string]$Message){if(-not $Value){throw $Message}}
$scratch=Join-Path $env:TEMP ('CodexKit-config-adapter-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
$adapter=$null
try {
    $config=Join-Path $scratch 'config.toml';$session=Join-Path $scratch 'session';$backups=Join-Path $session 'Backups';[void][IO.Directory]::CreateDirectory($backups)
    $original="model = 'old'`napproval_policy = 'never'`nsandbox_mode = 'workspace-write'`n[sandbox_workspace_write]`nnetwork_access = true`nwritable_roots = ['D:\工作']`n"
    [IO.File]::WriteAllText($config,$original,[Text.UTF8Encoding]::new($true))
    $originalBytes=[Convert]::ToBase64String([IO.File]::ReadAllBytes($config))
    $source=Join-Path $testPayload 'init.cmd';$raw=[IO.File]::ReadAllText($source,$script:InputEncoding)
    $backup=[regex]::Match($raw,'(?ms)^:BACKUP_CONFIG\r?\n.*?(?=^:BACKUP_ENV\r?$)').Value
    $adapter=New-InitAdapter $source $testPayload 'C' 0 @{inheritPermissions=$true}
    $text=[IO.File]::ReadAllText($adapter,$script:InputEncoding)
    $write=[regex]::Match($text,'(?ms)^:WRITE_CONFIG\r?\n.*?(?=^:WRITE_ENV\r?$)').Value
    $header="@echo off`r`nsetlocal EnableExtensions EnableDelayedExpansion`r`nset `"CONFIG_FILE=%CODEX_TEST_CONFIG%`"`r`nset `"BACKUP_DIR=%CODEX_TEST_BACKUP%`"`r`nset `"CODEX_HOME_DIR=%CODEX_TEST_HOME%`"`r`nset `"DEFAULT_PARENT_MODEL_ID=gpt-6.1-sol`"`r`nset `"DEFAULT_PARENT_REASONING_EFFORT=medium`"`r`nset `"CODEX_GUI_SUBAGENTS=0`"`r`nset `"CODEX_GUI_MEMORIES=true`"`r`ncall :BACKUP_CONFIG`r`nif errorlevel 1 exit /b 1`r`ncall :WRITE_CONFIG`r`nexit /b %errorlevel%`r`n"
    $fixture=Join-Path $scratch 'config-flow.cmd';[IO.File]::WriteAllText($fixture,$header+$backup+$write,$script:InputEncoding)
    $result=Invoke-ChildProcess $fixture @() $scratch @{CODEX_TEST_CONFIG=$config;CODEX_TEST_BACKUP=$backups;CODEX_TEST_HOME=$scratch} $script:InputEncoding
    Require ($result.ExitCode -eq 0) ('Config writer failed: '+$result.Error)
    $actual=[IO.File]::ReadAllText($config,[Text.Encoding]::UTF8)
    Require ($actual.Contains('model = "gpt-6.1-sol"')) 'The selected model was not written.'
    Require ($actual -notmatch '(?m)^(approval_policy|approvals_reviewer|sandbox_mode|default_permissions)\s*=|^\[(permissions|sandbox_workspace_write)(\.|\])') 'Rebuilt config should use Codex permission defaults even when an old caller supplies the legacy option.'
    Require ([Convert]::ToBase64String([IO.File]::ReadAllBytes((Join-Path $backups 'config.toml'))) -eq $originalBytes) 'Config backup did not preserve the original permissions.'
    $meta="RESTORE_CONFIG=1`r`nRESTORE_ENV=0`r`nRESTORE_AGENTS=0`r`nRESTORE_RTK=0`r`nRESTORE_RTK_EXE=0`r`nRESTORE_USER_PATH=0`r`nRESTORE_CLI=0`r`nRESTORE_RUNTIME=0`r`n"
    [IO.File]::WriteAllText((Join-Path $session 'session.meta'),$meta,[Text.Encoding]::ASCII)
    $rollback=[regex]::Match($raw,'(?ms)^:ROLLBACK_SESSION\r?\n.*?(?=^:START_SESSION\r?$)').Value
    $fixture=Join-Path $scratch 'rollback.cmd'
    $header="@echo off`r`nsetlocal EnableExtensions EnableDelayedExpansion`r`nset `"CONFIG_FILE=%CODEX_TEST_CONFIG%`"`r`nset `"CODEX_HOME_DIR=%CODEX_TEST_HOME%`"`r`ncall :ROLLBACK_SESSION `"%~1`"`r`nexit /b %errorlevel%`r`n"
    [IO.File]::WriteAllText($fixture,$header+$rollback+"`r`n:STOP_CODEX`r`nexit /b 0`r`n",$script:InputEncoding)
    $result=Invoke-ChildProcess $fixture @($session) $scratch @{CODEX_TEST_CONFIG=$config;CODEX_TEST_HOME=$scratch} $script:InputEncoding
    Require ($result.ExitCode -eq 0 -and [Convert]::ToBase64String([IO.File]::ReadAllBytes($config)) -eq $originalBytes) 'Original permission config did not roll back exactly.'
    Write-Output 'PASS config writer uses permission defaults with exact backup and rollback in isolation.'
} finally {
    if($adapter){Remove-InitAdapter $adapter}
    if(Test-Path -LiteralPath $scratch){Remove-Item -LiteralPath $scratch -Recurse -Force}
}
