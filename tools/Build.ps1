param([string]$Repository = 'BerryFuwawa/codex-init-kit')
$ErrorActionPreference='Stop'
$project=Split-Path -Parent $PSScriptRoot
$gbk=[Text.Encoding]::GetEncoding(936)
$utf8=[Text.UTF8Encoding]::new($false)
$module=[IO.File]::ReadAllText((Join-Path $project 'src\GitHubUpdate.ps1'),[Text.Encoding]::UTF8)
$tokens=$null; $errors=$null
[void][Management.Automation.Language.Parser]::ParseInput($module,[ref]$tokens,[ref]$errors)
if($errors.Count){throw ($errors | Out-String)}
$payload=[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($module))
$payloadBlock="::KIT_UPDATE_PAYLOAD_BEGIN`r`n"
for($i=0;$i -lt $payload.Length;$i+=100){$payloadBlock+='::KITB64:'+$payload.Substring($i,[Math]::Min(100,$payload.Length-$i))+"`r`n"}
$payloadBlock+="::KIT_UPDATE_PAYLOAD_END`r`n"
$loader=@'
$ErrorActionPreference='Stop'
$ProgressPreference='SilentlyContinue'
try {
    $encoding=if($env:CODEX_KIT_COMPONENT -eq 'init'){[Text.Encoding]::GetEncoding(936)}else{[Text.Encoding]::UTF8}
    $lines=[IO.File]::ReadAllLines($env:CODEX_KIT_SELF,$encoding)
    $start=[Array]::IndexOf($lines,'::KIT_UPDATE_PAYLOAD_BEGIN')
    $end=[Array]::IndexOf($lines,'::KIT_UPDATE_PAYLOAD_END')
    if($start -lt 0 -or $end -le $start){throw 'Update payload markers missing.'}
    $builder=New-Object Text.StringBuilder
    for($i=$start+1;$i -lt $end;$i++){
        if($lines[$i] -notmatch '^::KITB64:([A-Za-z0-9+/=]+)$'){throw 'Invalid update payload.'}
        [void]$builder.Append($Matches[1])
    }
    $code=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($builder.ToString()))
    & ([scriptblock]::Create($code))
    exit 0
} catch { [Console]::Error.WriteLine('Update check failed: '+$_.Exception.Message); exit 0 }
'@
$encodedLoader=[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($loader))
$wrapper=@'
::KIT_UPDATE_WRAPPER_BEGIN
:CHECK_GITHUB_UPDATE
setlocal DisableDelayedExpansion
set "CODEX_KIT_SELF=%~f0"
set "CODEX_KIT_UPDATE_MODE=%~1"
rem Parse this entire block before replacing the running CMD file.
(
    chcp 65001 >nul
    powershell.exe -NoLogo -NoProfile -InputFormat Text -OutputFormat Text -ExecutionPolicy Bypass -EncodedCommand __LOADER__
    if errorlevel 20 if not errorlevel 21 (
        chcp 936 >nul
        endlocal & exit /b 20
    )
    chcp 936 >nul
    endlocal & exit /b 0
)
::KIT_UPDATE_WRAPPER_END
'@
$wrapper=$wrapper.Replace('__LOADER__',$encodedLoader).Replace("`r`n","`n").Replace("`n","`r`n")
$components=@(
    @{Id='init'; Version='4.8.3'; File='第1步-Codex初始化管理-V4.7.0-GPT5.6-LUNA-MAX.cmd'; Encoding='gbk'},
    @{Id='guard'; Version='5.2.1'; File='第2步-Codex启动保护与更新管理器-V5.1.2-GPT5.6-LUNA-MAX.cmd'; Encoding='utf-8'}
)
$manifest=[ordered]@{schema_version=1; repository=$Repository; channel='stable'; components=[ordered]@{}}
foreach($component in $components){
    $encoding=if($component.Encoding -eq 'gbk'){$gbk}else{$utf8}
    $path=Join-Path (Join-Path $project 'scripts') $component.File
    $s=[IO.File]::ReadAllText($path,$encoding)
    $already=$s.Contains('::KIT_UPDATE_PAYLOAD_BEGIN')
    $s=[regex]::Replace($s,'(?ms)^::KIT_UPDATE_PAYLOAD_BEGIN\r?\n.*?^::KIT_UPDATE_PAYLOAD_END\r?\n?','')
    $s=[regex]::Replace($s,'(?ms)^::KIT_UPDATE_WRAPPER_BEGIN\r?\n.*?^::KIT_UPDATE_WRAPPER_END\r?\n?','')
    $s=[regex]::Replace($s,'(?m)^set "CODEX_KIT_(?:REPOSITORY|COMPONENT|VERSION)=[^\r\n]*"\r?\n','')
    $metadata='set "CODEX_KIT_REPOSITORY='+$Repository+'"'+"`r`n"+'set "CODEX_KIT_COMPONENT='+$component.Id+'"'+"`r`n"+'set "CODEX_KIT_VERSION='+$component.Version+'"'+"`r`n"
    if($component.Id -eq 'init'){
        $anchor='set "SCRIPT_PATH=%~f0"'+"`r`n"
        $s=$s.Replace($anchor,$anchor+$metadata)
        $s=$s.Replace('v4.7.0','v'+$component.Version).Replace('v4.8.0','v'+$component.Version)
        if(-not $already){
            $startup=@'
if /i "%~1"=="--check-update" (
    call :CHECK_GITHUB_UPDATE "manual"
    exit /b 0
)
if "%~1"=="" (
    call :CHECK_GITHUB_UPDATE "auto"
    if errorlevel 20 if not errorlevel 21 exit /b 0
)

'@
            $startup=$startup.Replace("`r`n","`n").Replace("`n","`r`n")
            $s=$s.Replace('if /i "%~1"=="--action" (',$startup+'if /i "%~1"=="--action" (')
            $s=$s.Replace('choice /c 12340 /n /m "Input [1/2/3/4/0]: "','choice /c 123450 /n /m "Input [1/2/3/4/5/0]: "')
            $manual="if errorlevel 6 goto :END`r`nif errorlevel 5 (`r`n    call :CHECK_GITHUB_UPDATE `"manual`"`r`n    if errorlevel 20 if not errorlevel 21 exit /b 0`r`n    goto :AFTER_ACTION`r`n)"
            $s=$s.Replace('if errorlevel 5 goto :END',$manual)
        }
        $match=[regex]::Match($s,'(?m)^powershell\.exe -NoLogo -NoProfile -EncodedCommand ([A-Za-z0-9+/=]+)')
        $menu=[Text.Encoding]::Unicode.GetString([Convert]::FromBase64String($match.Groups[1].Value))
        $menu=$menu.Replace('v4.7.0','v'+$component.Version).Replace('v4.8.0','v'+$component.Version)
        if(-not $menu.Contains('[5]')){$menu=$menu.Replace("Write-Host '  [0] 退出'", "Write-Host '  [5] 检查 GitHub 脚本更新    确认后下载、校验、备份并替换' -ForegroundColor Cyan`nWrite-Host '  [0] 退出'")}
        $s=$s.Replace($match.Groups[1].Value,[Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($menu)))
        $s=$s.Replace("`r`n:END`r`n","`r`n"+$wrapper+"`r`n:END`r`n")
        $s=$s.TrimEnd([char[]]"`r`n")+"`r`n"+$payloadBlock
    } else {
        $anchor='set "CODEX_GUARD_AUTO=0"'+"`r`n"
        $s=$s.Replace($anchor,$metadata+$anchor)
        if(-not $already){
            $startup=@'
if /I "%~1"=="--check-update" (
    call :CHECK_GITHUB_UPDATE "manual"
    exit /b 0
)
if "%CODEX_GUARD_AUTO%"=="1" (
    call :CHECK_GITHUB_UPDATE "background"
) else (
    call :CHECK_GITHUB_UPDATE "auto"
    if errorlevel 20 if not errorlevel 21 exit /b 0
)

'@
            $startup=$startup.Replace("`r`n","`n").Replace("`n","`r`n")
            $anchor='if /I "%~1"=="--auto" set "CODEX_GUARD_AUTO=1"'+"`r`n"
            $s=$s.Replace($anchor,$anchor+$startup)
        }
        $begin='###CODEX_'+'GUARD_PS_B64###'; $end='###CODEX_'+'GUARD_PS_B64_END###'
        $i=$s.IndexOf($begin); $j=$s.IndexOf($end)
        $guard=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String(($s.Substring($i+$begin.Length,$j-$i-$begin.Length) -replace '\s','')))
        $guard=$guard.Replace('V5.1.2','V5.2.0').Replace('$LunaModelId','$DefaultParentModelId').Replace('$LunaReasoningEffort','$DefaultParentReasoningEffort')
        $guard=[regex]::Replace($guard,'(?m)^\$ToolVersion = "V[0-9.]+"',('$ToolVersion = "V'+$component.Version+'"'))
        $guard=$guard.Replace('$DefaultParentModelId = "gpt-5.6-luna"','$DefaultParentModelId = "gpt-6.1-sol"').Replace('$DefaultParentReasoningEffort = "max"','$DefaultParentReasoningEffort = "medium"')
        $guard=$guard.Replace('模型配置状态：GPT5.6 LUNA MAX','模型配置状态：父模型 GPT6.1 SOL / medium；子代理 GPT5.6 LUNA MAX').Replace('未匹配 GPT5.6 LUNA MAX','未匹配 GPT6.1 SOL / medium')
        $guard=[regex]::Replace($guard,'(?ms)^function Check-GuardKitUpdate \{.*?^}\r?\n','')
        $guard=$guard.Replace("if (`$Auto) {`r`n    Check-GuardKitUpdate`r`n    Auto-Maintain", "if (`$Auto) {`r`n    Auto-Maintain")
        if($guard -notmatch '(?m)^if \(\$Auto\) \{'){
            $guard=$guard.Replace('function Confirm-MenuAction {', "if (`$Auto) {`r`n    Auto-Maintain`r`n    exit`r`n}`r`n`r`nfunction Confirm-MenuAction {")
        }
        $background=@'
function Check-GuardKitUpdate {
    try {
        $raw=[IO.File]::ReadAllText($SourceCmd,[Text.Encoding]::UTF8)
        if($raw -notmatch '(?m)^set "CODEX_KIT_VERSION=([0-9.]+)"\r?$'){throw 'Script version metadata missing.'}
        $kitVersion=$Matches[1]
        $lines=$raw -split '\r?\n'
        $start=[Array]::IndexOf($lines,'::KIT_UPDATE_PAYLOAD_BEGIN')
        $end=[Array]::IndexOf($lines,'::KIT_UPDATE_PAYLOAD_END')
        if($start -lt 0 -or $end -le $start){throw 'Update payload markers missing.'}
        $builder=New-Object Text.StringBuilder
        for($i=$start+1;$i -lt $end;$i++){
            if($lines[$i] -notmatch '^::KITB64:([A-Za-z0-9+/=]+)$'){throw 'Invalid update payload line.'}
            [void]$builder.Append($Matches[1])
        }
        $base64=$builder.ToString()
        $code=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($base64))
        . ([scriptblock]::Create($code)) -LibraryOnly
        [void](Invoke-KitUpdate '__REPOSITORY__' 'guard' $kitVersion $SourceCmd 'background')
    } catch { Write-GuardLog ('GitHub script update check: '+$_.Exception.Message) 'WARN' }
}

'@
        $background=$background.Replace('__REPOSITORY__',$Repository).Replace("`r`n","`n").Replace("`n","`r`n")
        $guard=$guard.Replace("if (`$Auto) {`r`n    Auto-Maintain",$background+"if (`$Auto) {`r`n    Check-GuardKitUpdate`r`n    Auto-Maintain")
        $s=$s.Substring(0,$i)+$wrapper+"`r`n"+$begin+[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($guard))+"`r`n"+$end+"`r`n"+$payloadBlock
        $s=$s.Replace('rem Codex Guard V5.1.2 / GPT5.6 LUNA MAX','rem Codex Guard V5.2.0 / GPT6.1 SOL medium / GPT5.6 LUNA MAX children')
    }
    $s=$s.Replace("`r`n","`n").Replace("`n","`r`n")
    [IO.File]::WriteAllBytes($path,$encoding.GetBytes($s))
    $sha=[Security.Cryptography.SHA256]::Create()
    try{$hash=[BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($path))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
    $manifest.components[$component.Id]=[ordered]@{version=$component.Version; path='scripts/'+$component.File; encoding=$component.Encoding; sha256=$hash}
}
[IO.File]::WriteAllText((Join-Path $project 'update-manifest.json'),($manifest | ConvertTo-Json -Depth 6)+"`n",$utf8)
Write-Output 'Built self-contained init/guard scripts and update-manifest.json.'
