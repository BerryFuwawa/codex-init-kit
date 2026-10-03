$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
. ([scriptblock]::Create([IO.File]::ReadAllText((Join-Path $project 'src\GitHubUpdate.ps1'),[Text.Encoding]::UTF8))) -LibraryOnly
function Assert([bool]$Ok,[string]$Message){if(-not $Ok){throw $Message}}
$scratch=Join-Path $env:TEMP ('codex-kit-tests-'+[Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
$oldLocalData=$env:LOCALAPPDATA
$env:LOCALAPPDATA=$scratch
$script:confirm=$true; $script:requests=0; $script:fail=$false
$target=Join-Path $scratch 'target.cmd'
$candidate=Join-Path $scratch 'candidate.cmd'
$utf8=[Text.UTF8Encoding]::new($false)
[IO.File]::WriteAllText($target,"@echo off`r`nset `"CODEX_KIT_COMPONENT=init`"`r`nset `"CODEX_KIT_VERSION=4.8.0`"`r`n",$utf8)
[IO.File]::WriteAllText($candidate,"@echo off`r`nset `"CODEX_KIT_COMPONENT=init`"`r`nset `"CODEX_KIT_VERSION=4.9.0`"`r`n",$utf8)
$initial=Get-KitHash $target
$script:manifest=[pscustomobject]@{schema_version=1; repository='BerryFuwawa/codex-init-kit'; components=[pscustomobject]@{init=[pscustomobject]@{version='4.9.0'; path='scripts/init.cmd'; encoding='utf-8'; sha256=(Get-KitHash $candidate)}}}
function Request-KitJson([string]$Url){
    $script:requests++
    if($script:fail){throw 'Simulated timeout/404/rate limit'}
    if($Url -match '/commits/main$'){return [pscustomobject]@{sha=('a'*40)}}
    return $script:manifest
}
function Request-KitFile([string]$Url,[string]$Path){[IO.File]::WriteAllBytes($Path,[IO.File]::ReadAllBytes($candidate))}
function Confirm-KitUpdate([string]$Version){return $script:confirm}
try {
    $script:confirm=$false
    Assert ((Invoke-KitUpdate 'BerryFuwawa/codex-init-kit' 'init' '4.8.0' $target 'manual') -eq 0) 'Decline failed'
    Assert ((Get-KitHash $target) -eq $initial) 'Decline replaced target'
    $count=$script:requests
    [void](Invoke-KitUpdate 'BerryFuwawa/codex-init-kit' 'init' '4.8.0' $target 'auto')
    Assert ($script:requests -eq $count) '24h cache failed'
    $script:confirm=$true
    [void](Invoke-KitUpdate 'BerryFuwawa/codex-init-kit' 'init' '4.8.0' $target 'check')
    Assert ((Get-KitHash $target) -eq $initial) 'Read-only check replaced target'
    $script:manifest.components.init.sha256='0'*64
    [void](Invoke-KitUpdate 'BerryFuwawa/codex-init-kit' 'init' '4.8.0' $target 'manual')
    Assert ((Get-KitHash $target) -eq $initial) 'Bad checksum replaced target'
    Assert (@(Get-ChildItem -LiteralPath $scratch -Filter '.codex-update-*').Count -eq 0) 'Failed staging was not cleaned'
    $script:manifest.components.init.sha256=Get-KitHash $candidate
    $script:manifest.components.init.version='4.8.0'
    [void](Invoke-KitUpdate 'BerryFuwawa/codex-init-kit' 'init' '4.8.0' $target 'manual')
    Assert ((Get-KitHash $target) -eq $initial) 'Same version replaced target'
    $script:manifest.components.init.version='4.7.0'
    [void](Invoke-KitUpdate 'BerryFuwawa/codex-init-kit' 'init' '4.8.0' $target 'manual')
    Assert ((Get-KitHash $target) -eq $initial) 'Downgrade replaced target'
    $script:manifest.components.init.version='4.9.0'
    $script:manifest.components.init.path='../init.cmd'
    [void](Invoke-KitUpdate 'BerryFuwawa/codex-init-kit' 'init' '4.8.0' $target 'manual')
    Assert ((Get-KitHash $target) -eq $initial) 'Unsafe path replaced target'
    $script:manifest.components.init.path='scripts/init.cmd'
    $script:fail=$true
    [void](Invoke-KitUpdate 'BerryFuwawa/codex-init-kit' 'init' '4.8.0' $target 'manual')
    Assert ((Get-KitHash $target) -eq $initial) 'Offline failure replaced target'
    $script:fail=$false
    $result=Invoke-KitUpdate 'BerryFuwawa/codex-init-kit' 'init' '4.8.0' $target 'manual'
    Assert ($result -eq 20) 'Confirmed update did not return restart signal'
    Assert ((Get-KitHash $target) -eq (Get-KitHash $candidate)) 'Confirmed update bytes mismatch'
    $backups=@(Get-ChildItem -LiteralPath $scratch -Filter '*.bak')
    Assert ($backups.Count -eq 1 -and (Get-KitHash $backups[0].FullName) -eq $initial) 'Backup mismatch'
    $manifest=[IO.File]::ReadAllText((Join-Path $project 'update-manifest.json')) | ConvertFrom-Json
    foreach($property in $manifest.components.PSObject.Properties){
        $item=$property.Value; $path=Join-Path $project $item.path
        Assert ((Get-KitHash $path) -eq $item.sha256) 'Published manifest checksum mismatch'
        $encoding=if($item.encoding -eq 'gbk'){[Text.Encoding]::GetEncoding(936)}else{[Text.Encoding]::UTF8}
        $cmd=[IO.File]::ReadAllText($path,$encoding)
        Assert ($cmd -notmatch '(?<!\r)\n') 'Script CRLF mismatch'
        $b64=([regex]::Matches($cmd,'(?m)^::KITB64:([A-Za-z0-9+/=]+)') | ForEach-Object {$_.Groups[1].Value}) -join ''
        $embedded=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($b64))
        Assert ($embedded -ceq [IO.File]::ReadAllText((Join-Path $project 'src\GitHubUpdate.ps1'),[Text.Encoding]::UTF8)) 'Updater embedding mismatch'
        $tokens=$null; $errors=$null
        [void][Management.Automation.Language.Parser]::ParseInput($embedded,[ref]$tokens,[ref]$errors)
        Assert ($errors.Count -eq 0) 'Embedded updater syntax failed'
        # Load harmless stand-in payload through the real CMD wrapper. No init/repair runs.
        $loaderMatch=[regex]::Match($cmd,'(?ms)^::KIT_UPDATE_WRAPPER_BEGIN\r?\n(.*?)^::KIT_UPDATE_WRAPPER_END')
        foreach($rc in @(0,20)){
            $stub=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes(('exit '+$rc)))
            $harness="@echo off`r`nsetlocal`r`n(`r`ncall :CHECK_GITHUB_UPDATE manual`r`nif errorlevel $rc if not errorlevel $($rc+1) exit /b 0`r`nexit /b 91`r`n)`r`n"+$loaderMatch.Groups[1].Value+"`r`n::KIT_UPDATE_PAYLOAD_BEGIN`r`n::KITB64:$stub`r`n::KIT_UPDATE_PAYLOAD_END`r`n"
            $harnessPath=Join-Path $scratch ('wrapper-'+$property.Name+'-'+$rc+'.cmd')
            [IO.File]::WriteAllBytes($harnessPath,$encoding.GetBytes($harness))
            & $env:ComSpec /d /c $harnessPath
            Assert ($LASTEXITCODE -eq 0) 'Batch wrapper returned wrong status'
        }
        # Replace a currently executing CMD with a much shorter file. The parsed
        # wrapper and caller must finish without executing the new file's tail.
        $selfPath=Join-Path $scratch ('self-replace-'+$property.Name+'.cmd')
        $shortPath=Join-Path $scratch ('new-'+$property.Name+'.cmd')
        $short="@echo off`r`nset `"CODEX_KIT_COMPONENT=init`"`r`nset `"CODEX_KIT_VERSION=4.9.0`"`r`nexit /b 73`r`n"
        [IO.File]::WriteAllBytes($shortPath,[Text.Encoding]::UTF8.GetBytes($short))
        $library=[IO.File]::ReadAllText((Join-Path $project 'src\GitHubUpdate.ps1'),[Text.Encoding]::UTF8)
        $library=$library.Substring(0,$library.LastIndexOf('if ($LibraryOnly)'))
        $testCode=$library+"`nfunction Request-KitFile([string]`$Url,[string]`$Path) { [IO.File]::WriteAllBytes(`$Path,[IO.File]::ReadAllBytes(`$env:KIT_TEST_CANDIDATE)) }`n`$remote=[pscustomobject]@{Url='https://example.invalid/unused'; Hash=(Get-KitHash `$env:KIT_TEST_CANDIDATE); Encoding='utf-8'; Version='4.9.0'}`nexit (Install-KitUpdate `$remote `$env:CODEX_KIT_SELF 'init' (Get-KitHash `$env:CODEX_KIT_SELF))"
        $stub=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($testCode))
        $harness="@echo off`r`nsetlocal`r`n(`r`ncall :CHECK_GITHUB_UPDATE manual`r`nif errorlevel 20 if not errorlevel 21 exit /b 0`r`nexit /b 91`r`n)`r`n"+$loaderMatch.Groups[1].Value+"`r`n::KIT_UPDATE_PAYLOAD_BEGIN`r`n"
        for($i=0;$i -lt $stub.Length;$i+=100){$harness+='::KITB64:'+$stub.Substring($i,[Math]::Min(100,$stub.Length-$i))+"`r`n"}
        $harness+="::KIT_UPDATE_PAYLOAD_END`r`n"
        [IO.File]::WriteAllBytes($selfPath,$encoding.GetBytes($harness))
        $env:KIT_TEST_CANDIDATE=$shortPath
        & $env:ComSpec /d /c $selfPath
        Assert ($LASTEXITCODE -eq 0) 'Running CMD replacement did not exit safely'
        Assert ((Get-KitHash $selfPath) -eq (Get-KitHash $shortPath)) 'Running CMD replacement bytes mismatch'
        $env:KIT_TEST_CANDIDATE=$null
        if($property.Name -eq 'guard'){
            $begin='###CODEX_'+'GUARD_PS_B64###'; $end='###CODEX_'+'GUARD_PS_B64_END###'
            $at=$cmd.IndexOf($begin); $finish=$cmd.IndexOf($end)
            $guardCode=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(($cmd.Substring($at+$begin.Length,$finish-$at-$begin.Length) -replace '\s','')))
            $tokens=$null; $errors=$null
            $guardAst=[Management.Automation.Language.Parser]::ParseInput($guardCode,[ref]$tokens,[ref]$errors)
            Assert ($errors.Count -eq 0) 'Guard payload syntax failed'
            $hook=$guardAst.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Check-GuardKitUpdate'},$false)
            Assert ($null -ne $hook) 'Guard background hook missing'
            . ([scriptblock]::Create($hook.Extent.Text))
            function Write-GuardLog([string]$Message,[string]$Level){$script:hookFailure=$Message}
            $script:hookCalls=0; $script:hookFailure=$null
            $fakeModule="param([switch]`$LibraryOnly)`nfunction Invoke-KitUpdate(`$Repository,`$Component,`$Version,`$Target,`$Mode){`$script:hookCalls++; if(`$Mode -ne 'background' -or `$Version -ne '5.9.0'){throw 'Background arguments incorrect'}; return 0}`n"
            $fakeB64=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($fakeModule))
            $SourceCmd=Join-Path $scratch 'guard-background.cmd'
            $fixture="@echo off`r`nset `"CODEX_KIT_VERSION=5.9.0`"`r`n::KITB64:outside-marker-ignored`r`n::KIT_UPDATE_PAYLOAD_BEGIN`r`n::KITB64:$fakeB64`r`n::KIT_UPDATE_PAYLOAD_END`r`n"
            [IO.File]::WriteAllText($SourceCmd,$fixture,$utf8)
            Check-GuardKitUpdate
            Assert ($script:hookCalls -eq 1 -and $null -eq $script:hookFailure) 'Background hook failed or used wrong version'
            $Auto=$true; $script:maintenanceCalls=0
            function Auto-Maintain{$script:maintenanceCalls++}
            $autoBranch=$guardAst.Find({param($node) $node -is [Management.Automation.Language.IfStatementAst] -and $node.Clauses[0].Item1.Extent.Text -eq '$Auto'},$false)
            Assert ($null -ne $autoBranch) 'Guard Auto branch missing'
            foreach($statement in $autoBranch.Clauses[0].Item2.Statements){
                if($statement -is [Management.Automation.Language.ExitStatementAst]){continue}
                . ([scriptblock]::Create($statement.Extent.Text))
            }
            Assert ($script:hookCalls -eq 2 -and $script:maintenanceCalls -eq 1) 'Auto branch did not continue maintenance'
        }
    }
    Write-Output 'PASS: declined/read-only updates, cache, checksum failure, staging cleanup, same/older versions, unsafe path, offline continuation, confirmed replacement, exact backup, manifest/encoding/payload and both wrappers.'
} finally {
    $env:LOCALAPPDATA=$oldLocalData
    $resolved=[IO.Path]::GetFullPath($scratch)
    $tempRoot=[IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')+'\'
    if($resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -and (Split-Path $resolved -Leaf).StartsWith('codex-kit-tests-')){Remove-Item -LiteralPath $resolved -Recurse -Force}
}
