@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 936 >nul
title Codex 初始化与维护 v4.8.1

rem ============================================================================
rem Codex 初始化与维护 v4.8.1
rem
rem 便携配置：启动时读取 Windows 的真实 Documents；读取失败时才使用用户目录作为备用路径。
rem ============================================================================
if not defined USERPROFILE (
    echo 未找到 USERPROFILE，无法确定 Windows 用户目录。
    pause
    exit /b 1
)

rem 读取 Windows 的真实 Documents 已知文件夹，兼容已迁移到其他盘符的情况。
set "DOCUMENTS_DIR="
for /f "delims=" %%D in ('powershell.exe -NoProfile -Command "[Environment]::GetFolderPath([Environment+SpecialFolder]::MyDocuments)" 2^>nul') do if not defined DOCUMENTS_DIR set "DOCUMENTS_DIR=%%D"
if not defined DOCUMENTS_DIR set "DOCUMENTS_DIR=%USERPROFILE%\Documents"
if not exist "%DOCUMENTS_DIR%" (
    echo Windows Documents 路径不可用：%DOCUMENTS_DIR%
    pause
    exit /b 1
)
set "BACKUP_ROOT=%DOCUMENTS_DIR%\Codex初始化备份"
set "PROXY_PORT=10808"
set "PROXY_URL=http://127.0.0.1:%PROXY_PORT%"

if defined CODEX_HOME (
    set "CODEX_HOME_DIR=%CODEX_HOME%"
) else (
    set "CODEX_HOME_DIR=%USERPROFILE%\.codex"
)

set "CODEX_ROOT=%LOCALAPPDATA%\OpenAI\Codex"
set "CONFIG_FILE=%CODEX_HOME_DIR%\config.toml"
set "ENV_FILE=%CODEX_HOME_DIR%\.env"
set "AGENTS_FILE=%CODEX_HOME_DIR%\AGENTS.md"
set "RTK_FILE=%CODEX_HOME_DIR%\RTK.md"
set "RTK_INSTALL_DIR=%USERPROFILE%\.local\bin"
set "RTK_EXE=%RTK_INSTALL_DIR%\rtk.exe"
set "RTK_RELEASE_URL=https://github.com/rtk-ai/rtk/releases/latest/download/rtk-x86_64-pc-windows-msvc.zip"
set "RTK_CHECKSUM_URL=https://github.com/rtk-ai/rtk/releases/latest/download/checksums.txt"
set "CLI_DIR=%CODEX_ROOT%\cli"
set "SCRIPT_PATH=%~f0"
set "CODEX_KIT_REPOSITORY=BerryFuwawa/codex-init-kit"
set "CODEX_KIT_COMPONENT=init"
set "CODEX_KIT_VERSION=4.8.1"
set "LUNA_PROMPT_RESULT=NOT_CHECKED"
set "CUA_RESULT=NOT_RUN"
set "CUA_REPAIR_RC="
set "CUA_REPORT_ROOT="
set "LUNA_PROMPT_COUNT=NOT_CHECKED"
set "DEFAULT_PARENT_MODEL_ID=gpt-6.1-sol"
set "DEFAULT_PARENT_REASONING_EFFORT=medium"
set "LUNA_MAX_THREADS=6"

if defined CODEX_INIT_ELEVATION_PROFILE (
    if /i not "%USERPROFILE%"=="%CODEX_INIT_ELEVATION_PROFILE%" (
        echo.
        echo 提权后的 Windows 用户目录与原用户不一致，已停止，避免写入错误的配置。
        echo 原用户目录：%CODEX_INIT_ELEVATION_PROFILE%
        echo 当前用户目录：%USERPROFILE%
        pause
        exit /b 1
    )
    set "CODEX_INIT_ELEVATION_PROFILE="
)

if /i "%~1"=="--check-update" (
    call :CHECK_GITHUB_UPDATE "manual"
    exit /b 0
)
if "%~1"=="" (
    call :CHECK_GITHUB_UPDATE "auto"
    if errorlevel 20 if not errorlevel 21 exit /b 0
)
if /i "%~1"=="--action" (
    call :RUN_ACTION "%~2"
    set "CODEX_INIT_ACTION_RC=!errorlevel!"
    if "!CODEX_INIT_ACTION_RC!"=="2" exit /b 0
    echo.
echo 办于已完成完，行部上文改数集，自原多线程。
    call :SHOW_REPORT
    pause
    set "CODEX_INIT_ACTION_RC="
    goto :MENU
)
if /i "%~1"=="--rollback" (
    call :RUN_ROLLBACK_REQUEST "%~2"
    set "CODEX_INIT_ACTION_RC=!errorlevel!"
    if "!CODEX_INIT_ACTION_RC!"=="2" exit /b 0
    echo.
    echo Rollback completed. Review the result above; this console will remain open.
    call :SHOW_REPORT
    pause
    set "CODEX_INIT_ACTION_RC="
    goto :MENU
)

:MENU
cls
call :RENDER_MENU_V2
choice /c 123450 /n /m "Input [1/2/3/4/5/0]: "
if errorlevel 6 goto :END
if errorlevel 5 (
    call :CHECK_GITHUB_UPDATE "manual"
    if errorlevel 20 if not errorlevel 21 exit /b 0
    goto :AFTER_ACTION
)
if errorlevel 4 (
    call :RUN_ACTION "rollback-latest"
    if errorlevel 2 exit /b 0
    goto :AFTER_ACTION
)
if errorlevel 3 (
    call :RUN_ACTION "proxy"
    if errorlevel 2 exit /b 0
    goto :AFTER_ACTION
)
if errorlevel 2 (
    call :SHOW_STATUS
    goto :AFTER_ACTION
)
if errorlevel 1 (
    call :RUN_ACTION "one-click"
    if errorlevel 2 exit /b 0
    goto :AFTER_ACTION
)
goto :MENU
:AFTER_ACTION
echo.
echo 操作结束。按任意键返回主菜单；按 Ctrl+C 可退出。
pause >nul
goto :MENU
:RENDER_MENU_V2
powershell.exe -NoLogo -NoProfile -EncodedCommand JABFAHIAcgBvAHIAQQBjAHQAaQBvAG4AUAByAGUAZgBlAHIAZQBuAGMAZQAgAD0AIAAnAFMAaQBsAGUAbgB0AGwAeQBDAG8AbgB0AGkAbgB1AGUAJwAKAFcAcgBpAHQAZQAtAEgAbwBzAHQAIAAnAD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9AD0APQA9ACcAIAAtAEYAbwByAGUAZwByAG8AdQBuAGQAQwBvAGwAbwByACAAQwB5AGEAbgAKAFcAcgBpAHQAZQAtAEgAbwBzAHQAIAAnACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAQwBvAGQAZQB4ACAAHVLLWRZTDk70fqRiIAB2ADQALgA4AC4AMQAnACAALQBGAG8AcgBlAGcAcgBvAHUAbgBkAEMAbwBsAG8AcgAgAEMAeQBhAG4ACgBXAHIAaQB0AGUALQBIAG8AcwB0ACAAJwAnAAoAVwByAGkAdABlAC0ASABvAHMAdAAgACcAU19NUq9zg1gnACAALQBGAG8AcgBlAGcAcgBvAHUAbgBkAEMAbwBsAG8AcgAgAEQAYQByAGsARwByAGEAeQAKAFcAcgBpAHQAZQAtAEgAbwBzAHQAIAAoACcAIAAgAEQAbwBjAHUAbQBlAG4AdABzACAAIAA6ACAAJwAgACsAIAAkAGUAbgB2ADoARABPAEMAVQBNAEUATgBUAFMAXwBEAEkAUgApACAALQBGAG8AcgBlAGcAcgBvAHUAbgBkAEMAbwBsAG8AcgAgAEQAYQByAGsARwByAGEAeQAKAFcAcgBpAHQAZQAtAEgAbwBzAHQAIAAoACcAIAAgAEMATwBEAEUAWABfAEgATwBNAEUAIAA6ACAAJwAgACsAIAAkAGUAbgB2ADoAQwBPAEQARQBYAF8ASABPAE0ARQBfAEQASQBSACkAIAAtAEYAbwByAGUAZwByAG8AdQBuAGQAQwBvAGwAbwByACAARABhAHIAawBHAHIAYQB5AAoAVwByAGkAdABlAC0ASABvAHMAdAAgACgAJwAgACAA404GdCAAIAAgACAAIAAgACAAOgAgACcAIAArACAAJABlAG4AdgA6AFAAUgBPAFgAWQBfAFUAUgBMACkAIAAtAEYAbwByAGUAZwByAG8AdQBuAGQAQwBvAGwAbwByACAARABhAHIAawBHAHIAYQB5AAoAVwByAGkAdABlAC0ASABvAHMAdAAgACcAJwAKAFcAcgBpAHQAZQAtAEgAbwBzAHQAIAAnAO52B2hNkW5/JwAgAC0ARgBvAHIAZQBnAHIAbwB1AG4AZABDAG8AbABvAHIAIABEAGEAcgBrAEMAeQBhAG4ACgBXAHIAaQB0AGUALQBIAG8AcwB0ACAAKAAnACAAIADYnqSLNnIhaotXIAA6ACAAJwAgACsAIAAkAGUAbgB2ADoARABFAEYAQQBVAEwAVABfAFAAQQBSAEUATgBUAF8ATQBPAEQARQBMAF8ASQBEACAAKwAgACcAIAAtACAARwBQAFQANgAuADEAIABTAE8ATAAnACkAIAAtAEYAbwByAGUAZwByAG8AdQBuAGQAQwBvAGwAbwByACAARABhAHIAawBDAHkAYQBuAAoAVwByAGkAdABlAC0ASABvAHMAdAAgACgAJwAgACAAqGMGdDpfpl4gACAAIAA6ACAAJwAgACsAIAAkAGUAbgB2ADoARABFAEYAQQBVAEwAVABfAFAAQQBSAEUATgBUAF8AUgBFAEEAUwBPAE4ASQBOAEcAXwBFAEYARgBPAFIAVAApACAALQBGAG8AcgBlAGcAcgBvAHUAbgBkAEMAbwBsAG8AcgAgAEQAYQByAGsAQwB5AGEAbgAKAFcAcgBpAHQAZQAtAEgAbwBzAHQAIAAnACAAIABQW+NOBnQgACAAIAAgACAAOgAgAGcAcAB0AC0ANQAuADYALQBsAHUAbgBhACAALwAgAG0AYQB4ACcAIAAtAEYAbwByAGUAZwByAG8AdQBuAGQAQwBvAGwAbwByACAARABhAHIAawBDAHkAYQBuAAoAVwByAGkAdABlAC0ASABvAHMAdAAgACgAJwAgACAAGlm/fgt6Ck5QliAAOgAgACcAIAArACAAJABlAG4AdgA6AEwAVQBOAEEAXwBNAEEAWABfAFQASABSAEUAQQBEAFMAIAArACAAJwAgAO+NJwApACAALQBGAG8AcgBlAGcAcgBvAHUAbgBkAEMAbwBsAG8AcgAgAEQAYQByAGsAQwB5AGEAbgAKAFcAcgBpAHQAZQAtAEgAbwBzAHQAIAAnACcACgBXAHIAaQB0AGUALQBIAG8AcwB0ACAAJwCJW8WIDk5NkW5/JwAgAC0ARgBvAHIAZQBnAHIAbwB1AG4AZABDAG8AbABvAHIAIABHAHIAZQBlAG4ACgBXAHIAaQB0AGUALQBIAG8AcwB0ACAAJwAgACAAWwAxAF0AIAAATi6VHVLLWRZTIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgANh2JntueKSLATCMW3RlHVLLWRZTATCHZfZOOVmhewZ0ATBDAFUAQQAgAO5PDVkBMEwAdQBuAGEAIAAaWb9+C3onACAALQBGAG8AcgBlAGcAcgBvAHUAbgBkAEMAbwBsAG8AcgAgAEcAcgBlAGUAbgAKAFcAcgBpAHQAZQAtAEgAbwBzAHQAIAAnAMqLrWUOTvR+pGInACAALQBGAG8AcgBlAGcAcgBvAHUAbgBkAEMAbwBsAG8AcgAgAEMAeQBhAG4ACgBXAHIAaQB0AGUALQBIAG8AcwB0ACAAJwAgACAAWwAyAF0AIADlZwt3tnIBYA5OyoutZSAAIAAgACAAIAAgACAAIAAgACAA6lP7iwz/DU7uTzllh2X2TicAIAAtAEYAbwByAGUAZwByAG8AdQBuAGQAQwBvAGwAbwByACAAQwB5AGEAbgAKAFcAcgBpAHQAZQAtAEgAbwBzAHQAIAAnACAAIABbADMAXQAgAO5PDVkvAM2R+l7jTgZ0TZFufyAAIAAgACAAIAAgACAAGk8HWf1Odl6GidZ2IAAuAGUAbgB2ACcAIAAtAEYAbwByAGUAZwByAG8AdQBuAGQAQwBvAGwAbwByACAAQwB5AGEAbgAKAFcAcgBpAHQAZQAtAEgAbwBzAHQAIAAnAGJgDVkOTgCQ+lEnACAALQBGAG8AcgBlAGcAcgBvAHUAbgBkAEMAbwBsAG8AcgAgAFkAZQBsAGwAbwB3AAoAVwByAGkAdABlAC0ASABvAHMAdAAgACcAIAAgAFsANABdACAA3lbabgBn0Y8ATiFrHVLLWRZTIAAgACAAIAAgACAAf08odQBn0Y8ATiFr71ModQdZ/U4nACAALQBGAG8AcgBlAGcAcgBvAHUAbgBkAEMAbwBsAG8AcgAgAFkAZQBsAGwAbwB3AAoAVwByAGkAdABlAC0ASABvAHMAdAAgACcAIAAgAFsANQBdACAAwGjlZyAARwBpAHQASAB1AGIAIAAagSxn9GawZSAAIAAgACAAbnikiw5UC059jwEwIWiMmgEwB1n9TnZe/2ZiYycAIAAtAEYAbwByAGUAZwByAG8AdQBuAGQAQwBvAGwAbwByACAAQwB5AGEAbgAKAFcAcgBpAHQAZQAtAEgAbwBzAHQAIAAnACAAIABbADAAXQAgAACQ+lEnACAALQBGAG8AcgBlAGcAcgBvAHUAbgBkAEMAbwBsAG8AcgAgAEQAYQByAGsARwByAGEAeQAKAFcAcgBpAHQAZQAtAEgAbwBzAHQAIAAnACcACgBXAHIAaQB0AGUALQBIAG8AcwB0ACAAJwCcmHKC9IsOZhr//35ygj0AAE4ulYlbxYgb/1KXcoI9AMqLrWUvAPR+pGIb/8SecoI9AGJgDVkb/3BwcoI9AACQ+lECMCcAIAAtAEYAbwByAGUAZwByAG8AdQBuAGQAQwBvAGwAbwByACAARABhAHIAawBHAHIAYQB5AA0ACgBXAHIAaQB0AGUALQBIAG8AcwB0ACAAJwDQYzp5Gv8JkOliIABbADEAXQAgAA5U6lMAl254pIsATiFr2HYme4xU537tfgz/j5YOVOqBqFKMWxBilE4qTjaWtWsCMCcAIAAtAEYAbwByAGUAZwByAG8AdQBuAGQAQwBvAGwAbwByACAARABhAHIAawBHAHIAYQB5AA==
exit /b 0

:PROMPT_MANAGER
set "CODEX_INIT_SCRIPT_PATH=%SCRIPT_PATH%"
set "CODEX_PROMPT_MANAGER_ACTION=%~1"
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -EncodedCommand JABFAHIAcgBvAHIAQQBjAHQAaQBvAG4AUAByAGUAZgBlAHIAZQBuAGMAZQAgAD0AIAAnAFMAdABvAHAAJwAKAHQAcgB5ACAAewAKACAAIAAgACAAJABzAGMAcgBpAHAAdABQAGEAdABoACAAPQAgACQAZQBuAHYAOgBDAE8ARABFAFgAXwBJAE4ASQBUAF8AUwBDAFIASQBQAFQAXwBQAEEAVABIAAoAIAAgACAAIABpAGYAIAAoAFsAcwB0AHIAaQBuAGcAXQA6ADoASQBzAE4AdQBsAGwATwByAFcAaABpAHQAZQBTAHAAYQBjAGUAKAAkAHMAYwByAGkAcAB0AFAAYQB0AGgAKQAgAC0AbwByACAALQBuAG8AdAAgACgAVABlAHMAdAAtAFAAYQB0AGgAIAAtAEwAaQB0AGUAcgBhAGwAUABhAHQAaAAgACQAcwBjAHIAaQBwAHQAUABhAHQAaAAgAC0AUABhAHQAaABUAHkAcABlACAATABlAGEAZgApACkAIAB7ACAAdABoAHIAbwB3ACAAJwBUAGgAZQAgAGkAbgBpAHQAaQBhAGwAaQB6AGUAcgAgAHMAYwByAGkAcAB0ACAAcABhAHQAaAAgAGkAcwAgAHUAbgBhAHYAYQBpAGwAYQBiAGwAZQAuACcAIAB9AAoAIAAgACAAIAAkAGwAaQBuAGUAcwAgAD0AIABbAFMAeQBzAHQAZQBtAC4ASQBPAC4ARgBpAGwAZQBdADoAOgBSAGUAYQBkAEEAbABsAEwAaQBuAGUAcwAoACQAcwBjAHIAaQBwAHQAUABhAHQAaAAsACAAWwBTAHkAcwB0AGUAbQAuAFQAZQB4AHQALgBFAG4AYwBvAGQAaQBuAGcAXQA6ADoARABlAGYAYQB1AGwAdAApAAoAIAAgACAAIAAkAHMAdABhAHIAdAAgAD0AIAAtADEACgAgACAAIAAgACQAZgBpAG4AaQBzAGgAIAA9ACAALQAxAAoAIAAgACAAIABmAG8AcgAgACgAJABpACAAPQAgADAAOwAgACQAaQAgAC0AbAB0ACAAJABsAGkAbgBlAHMALgBMAGUAbgBnAHQAaAA7ACAAJABpACsAKwApACAAewAKACAAIAAgACAAIAAgACAAIABpAGYAIAAoACQAbABpAG4AZQBzAFsAJABpAF0AIAAtAGMAZQBxACAAJwA6ADoAUABSAE8ATQBQAFQAXwBNAEEATgBBAEcARQBSAF8AUABBAFkATABPAEEARABfAEIARQBHAEkATgAnACkAIAB7ACAAJABzAHQAYQByAHQAIAA9ACAAJABpADsAIABiAHIAZQBhAGsAIAB9AAoAIAAgACAAIAB9AAoAIAAgACAAIABpAGYAIAAoACQAcwB0AGEAcgB0ACAALQBsAHQAIAAwACkAIAB7ACAAdABoAHIAbwB3ACAAJwBFAG0AYgBlAGQAZABlAGQAIABtAGEAbgBhAGcAZQByACAAcABhAHkAbABvAGEAZAAgAHMAdABhAHIAdAAgAG0AYQByAGsAZQByACAAaQBzACAAbQBpAHMAcwBpAG4AZwAuACcAIAB9AAoAIAAgACAAIABmAG8AcgAgACgAJABpACAAPQAgACQAcwB0AGEAcgB0ACAAKwAgADEAOwAgACQAaQAgAC0AbAB0ACAAJABsAGkAbgBlAHMALgBMAGUAbgBnAHQAaAA7ACAAJABpACsAKwApACAAewAKACAAIAAgACAAIAAgACAAIABpAGYAIAAoACQAbABpAG4AZQBzAFsAJABpAF0AIAAtAGMAZQBxACAAJwA6ADoAUABSAE8ATQBQAFQAXwBNAEEATgBBAEcARQBSAF8AUABBAFkATABPAEEARABfAEUATgBEACcAKQAgAHsAIAAkAGYAaQBuAGkAcwBoACAAPQAgACQAaQA7ACAAYgByAGUAYQBrACAAfQAKACAAIAAgACAAfQAKACAAIAAgACAAaQBmACAAKAAkAGYAaQBuAGkAcwBoACAALQBsAGUAIAAkAHMAdABhAHIAdAApACAAewAgAHQAaAByAG8AdwAgACcARQBtAGIAZQBkAGQAZQBkACAAbQBhAG4AYQBnAGUAcgAgAHAAYQB5AGwAbwBhAGQAIABlAG4AZAAgAG0AYQByAGsAZQByACAAaQBzACAAbQBpAHMAcwBpAG4AZwAuACcAIAB9AAoAIAAgACAAIAAkAGIAdQBpAGwAZABlAHIAIAA9ACAATgBlAHcALQBPAGIAagBlAGMAdAAgAFMAeQBzAHQAZQBtAC4AVABlAHgAdAAuAFMAdAByAGkAbgBnAEIAdQBpAGwAZABlAHIACgAgACAAIAAgAGYAbwByACAAKAAkAGkAIAA9ACAAJABzAHQAYQByAHQAIAArACAAMQA7ACAAJABpACAALQBsAHQAIAAkAGYAaQBuAGkAcwBoADsAIAAkAGkAKwArACkAIAB7AAoAIAAgACAAIAAgACAAIAAgAGkAZgAgACgAJABsAGkAbgBlAHMAWwAkAGkAXQAgAC0AbgBvAHQAbQBhAHQAYwBoACAAJwBeADoAOgBQAE0AQgA2ADQAOgAoAFsAQQAtAFoAYQAtAHoAMAAtADkAKwAvAD0AXQArACkAJAAnACkAIAB7ACAAdABoAHIAbwB3ACAAJwBFAG0AYgBlAGQAZABlAGQAIABtAGEAbgBhAGcAZQByACAAcABhAHkAbABvAGEAZAAgAGMAbwBuAHQAYQBpAG4AcwAgAGEAbgAgAGkAbgB2AGEAbABpAGQAIABsAGkAbgBlAC4AJwAgAH0ACgAgACAAIAAgACAAIAAgACAAWwB2AG8AaQBkAF0AJABiAHUAaQBsAGQAZQByAC4AQQBwAHAAZQBuAGQAKAAkAE0AYQB0AGMAaABlAHMAWwAxAF0AKQAKACAAIAAgACAAfQAKACAAIAAgACAAJABwAGEAeQBsAG8AYQBkAEIAeQB0AGUAcwAgAD0AIABbAEMAbwBuAHYAZQByAHQAXQA6ADoARgByAG8AbQBCAGEAcwBlADYANABTAHQAcgBpAG4AZwAoACQAYgB1AGkAbABkAGUAcgAuAFQAbwBTAHQAcgBpAG4AZwAoACkAKQAKACAAIAAgACAAJABwAGEAeQBsAG8AYQBkACAAPQAgAFsAVABlAHgAdAAuAEUAbgBjAG8AZABpAG4AZwBdADoAOgBVAFQARgA4AC4ARwBlAHQAUwB0AHIAaQBuAGcAKAAkAHAAYQB5AGwAbwBhAGQAQgB5AHQAZQBzACkACgAgACAAIAAgACYAIAAoAFsAcwBjAHIAaQBwAHQAYgBsAG8AYwBrAF0AOgA6AEMAcgBlAGEAdABlACgAJABwAGEAeQBsAG8AYQBkACkAKQAKACAAIAAgACAAZQB4AGkAdAAgADAACgB9ACAAYwBhAHQAYwBoACAAewAKACAAIAAgACAAWwBDAG8AbgBzAG8AbABlAF0AOgA6AEUAcgByAG8AcgAuAFcAcgBpAHQAZQBMAGkAbgBlACgAJwBbAEYAQQBJAEwAXQAgAFAAcgBvAG0AcAB0ACAAbQBhAG4AYQBnAGUAcgA6ACAAJwAgACsAIAAkAF8ALgBFAHgAYwBlAHAAdABpAG8AbgAuAE0AZQBzAHMAYQBnAGUAKQAKACAAIAAgACAAZQB4AGkAdAAgADEACgB9AA==
set "PROMPT_MANAGER_RC=!errorlevel!"
chcp 936 >nul
set "CODEX_INIT_SCRIPT_PATH="
set "CODEX_PROMPT_MANAGER_ACTION="
exit /b !PROMPT_MANAGER_RC!
:RUN_ACTION
call :ENSURE_ADMIN "--action" "%~1"
if errorlevel 2 exit /b 2
if errorlevel 1 exit /b 1
if /i "%~1"=="full" (
    call :FULL_INIT
    exit /b !errorlevel!
)
if /i "%~1"=="one-click" (
    call :ONE_CLICK_INIT
    exit /b !errorlevel!
)
if /i "%~1"=="proxy" (
    call :PROXY_ONLY
    exit /b !errorlevel!
)
if /i "%~1"=="rollback-latest" (
    call :ROLLBACK_LATEST
    exit /b !errorlevel!
)
echo 未知的操作：%~1
exit /b 1

:RUN_ROLLBACK_REQUEST
call :ENSURE_ADMIN "--rollback" "%~1"
if errorlevel 1 if not errorlevel 2 exit /b 1
if errorlevel 2 exit /b 0
call :ROLLBACK_SESSION "%~1"
exit /b !errorlevel!

:ENSURE_ADMIN
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$id=[Security.Principal.WindowsIdentity]::GetCurrent(); $principal=New-Object Security.Principal.WindowsPrincipal($id); if($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)){exit 0}else{exit 1}"
if not errorlevel 1 exit /b 0

echo.
echo 此操作需要管理员权限，正在弹出 UAC 确认窗口...
set "CODEX_INIT_ELEVATION_SCRIPT=%SCRIPT_PATH%"
set "CODEX_INIT_ELEVATION_ARG1=%~1"
set "CODEX_INIT_ELEVATION_ARG2=%~2"
set "CODEX_INIT_ELEVATION_PROFILE=%USERPROFILE%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$script=$env:CODEX_INIT_ELEVATION_SCRIPT; $a1=$env:CODEX_INIT_ELEVATION_ARG1; $a2=$env:CODEX_INIT_ELEVATION_ARG2; $argText=$a1+' "'+$a2+'"'; try{Start-Process -FilePath $script -ArgumentList $argText -WorkingDirectory ([IO.Path]::GetDirectoryName($script)) -Verb RunAs -ErrorAction Stop; exit 0}catch{exit 1}"
if errorlevel 1 (
    echo 未获得管理员权限，操作已取消。
    set "CODEX_INIT_ELEVATION_SCRIPT="
    set "CODEX_INIT_ELEVATION_ARG1="
    set "CODEX_INIT_ELEVATION_ARG2="
    set "CODEX_INIT_ELEVATION_PROFILE="
    exit /b 1
)
set "CODEX_INIT_ELEVATION_SCRIPT="
set "CODEX_INIT_ELEVATION_ARG1="
set "CODEX_INIT_ELEVATION_ARG2="
set "CODEX_INIT_ELEVATION_PROFILE="
exit /b 2

:FULL_INIT
call :CONFIRM "完整初始化将关闭 Codex，清理用户级 Runtime 覆盖，并重建配置、代理和全局 AGENTS.md。是否继续？"
if errorlevel 1 (
    set "FULL_INIT_CANCELLED=1"
    set "RESULT=CANCELLED"
    exit /b 2
)
set "FULL_INIT_CANCELLED=0"

if not exist "%CODEX_ROOT%" (
    echo.
    echo 未找到 Codex Desktop 本地目录：%CODEX_ROOT%
    echo 本次未执行清理。
    exit /b 1
)

call :START_SESSION
if errorlevel 1 exit /b 1

call :BACKUP_CONFIG
if errorlevel 1 (
    echo config.toml 备份失败，已停止初始化。
    exit /b 1
)
call :BACKUP_ENV
if errorlevel 1 (
    echo .env 备份失败，已停止初始化。
    exit /b 1
)
call :CAPTURE_RUNTIME
if errorlevel 1 (
    echo Runtime 状态备份失败，已停止初始化。
    exit /b 1
)
call :BACKUP_CUSTOM_FILES
if errorlevel 1 (
    echo 自定义 Runtime 备份失败，已停止初始化。
    exit /b 1
)
call :BACKUP_AGENTS_AND_RTK
if errorlevel 1 (
    echo AGENTS / RTK 备份失败，已停止初始化。
    exit /b 1
)
call :CAPTURE_USER_PATH
if errorlevel 1 (
    echo 无法记录用户 PATH，已停止初始化。
    exit /b 1
)
call :WRITE_SESSION_META
call :STOP_CODEX
call :CLEAR_USER_RUNTIME
call :REMOVE_CUSTOM_FILES
call :WRITE_CONFIG
call :WRITE_ENV
call :INSTALL_AND_INTEGRATE_RTK
call :WRITE_AGENTS_RULES
call :WRITE_SESSION_META
call :VERIFY_FULL
set "CODEX_INIT_RULES_B64="
call :WRITE_REPORT "完整初始化"
call :WRITE_ROLLBACK

echo.
echo ============================================================
if /i "!RESULT!"=="PASS" (
    echo RESULT = PASS
    echo Codex 初始化完成，默认父模型为 GPT6.1 SOL / medium，子代理为 GPT5.6 LUNA MAX；多线程上限 !LUNA_MAX_THREADS!。
) else if /i "!RESULT!"=="NEEDS_REVIEW_MACHINE_OVERRIDE" (
    echo RESULT = NEEDS_REVIEW_MACHINE_OVERRIDE
    echo 用户级配置已完成，但检测到 Machine 级 Runtime 覆盖。
) else if /i "!RESULT!"=="NEEDS_REVIEW_AGENTS_OVERRIDE" (
    echo RESULT = NEEDS_REVIEW_AGENTS_OVERRIDE
    echo 配置已完成，但 AGENTS.override.md 可能覆盖新写入的 AGENTS.md 规则。
) else (
    echo RESULT = FAIL
    echo 初始化校验失败，请查看报告。
)
if /i "!REPORT_RESULT!"=="PASS" echo 报告   : !REPORT_FILE!
if /i not "!REPORT_RESULT!"=="PASS" echo 报告   : WRITE_FAILED
echo 备份   : !BACKUP_DIR!
echo 回滚   : !SESSION_DIR!\Rollback.cmd
echo ============================================================
if /i "!RESULT!"=="FAIL" exit /b 1
exit /b 0

:ONE_CLICK_INIT
echo 正在准备一键初始化，未执行部署。
set "CODEX_ONE_CLICK_MODE=1"
echo 阶段 1/5：确认文件夹管理盘符。
call :SELECT_FOLDER_DRIVE
if errorlevel 1 goto :ONE_CLICK_INIT_FAIL

echo 阶段 2/5：执行完整初始化与备份。
call :FULL_INIT
set "FULL_INIT_RC=!errorlevel!"
if "!FULL_INIT_RC!"=="2" (
    echo One-click initialization cancelled; later installation was not executed.
    goto :ONE_CLICK_INIT_FAIL
)
if "!FULL_INIT_RC!"=="1" (
    echo Full initialization failed; later installation stopped.
    goto :ONE_CLICK_INIT_FAIL
)
set "FULL_INIT_RESULT=!RESULT!"
if /i "!FULL_INIT_RESULT!"=="FAIL" (
    echo 完整初始化未通过，已停止后续安装。
    goto :ONE_CLICK_INIT_FAIL
)

echo 阶段 3/5：安装文件夹管理并创建标准目录。
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

echo 阶段 4/5：检查并按需修复 Codex CUA 运行时。
call :RUN_CUA_REPAIR
if errorlevel 1 (
    echo CUA 运行时检查或修复失败，已停止后续安装。
    set "RESULT=FAIL"
    call :WRITE_REPORT "一键初始化 - CUA 检查或修复失败"
    call :WRITE_ROLLBACK
    goto :ONE_CLICK_INIT_FAIL
)

echo 阶段 5/5：安装 GPT5.6 LUNA MAX 多线程提示词。
call :INSTALL_LUNA_PROMPT
if errorlevel 1 (
    echo Luna 多线程提示词安装失败。
    goto :ONE_CLICK_INIT_FAIL
)

set "CONFIG_RESULT=FAIL"
call :VERIFY_CONFIG
if not errorlevel 1 set "CONFIG_RESULT=PASS"
call :VERIFY_AGENTS_CONTENT
set "RESULT=FAIL"
if "!CONFIG_RESULT!"=="PASS" if "!AGENTS_RESULT!"=="PASS" if "!FOLDER_RESULT!"=="PASS" if "!CUA_RESULT!"=="PASS" if "!LUNA_PROMPT_RESULT!"=="PASS" set "RESULT=!FULL_INIT_RESULT!"
call :WRITE_REPORT "一键初始化（完整初始化 + 文件夹管理 + CUA 修复 + Luna 多线程）"
call :WRITE_ROLLBACK

echo.
echo ============================================================
if /i "!RESULT!"=="PASS" (
    echo RESULT = PASS
    echo 一键初始化完成：完整初始化、文件夹管理、CUA 检查修复和 Luna 多线程均已通过。
) else if /i "!RESULT!"=="NEEDS_REVIEW_MACHINE_OVERRIDE" (
    echo RESULT = NEEDS_REVIEW_MACHINE_OVERRIDE
    echo 初始化完成，但检测到 Machine 级 Runtime 覆盖。
) else if /i "!RESULT!"=="NEEDS_REVIEW_AGENTS_OVERRIDE" (
    echo RESULT = NEEDS_REVIEW_AGENTS_OVERRIDE
    echo 初始化完成，但 AGENTS.override.md 可能覆盖新规则。
) else (
    echo RESULT = FAIL
    echo 一键初始化失败，请查看报告。
)
if /i "!REPORT_RESULT!"=="PASS" echo 报告   : !REPORT_FILE!
if /i not "!REPORT_RESULT!"=="PASS" echo 报告   : WRITE_FAILED
echo 备份   : !BACKUP_DIR!
echo 回滚   : !SESSION_DIR!\Rollback.cmd
echo ============================================================
set "CODEX_ONE_CLICK_MODE="
set "CODEX_FOLDER_DRIVE="
if /i "!RESULT!"=="FAIL" exit /b 1
exit /b 0

:SELECT_FOLDER_DRIVE
set "FOLDER_SELECTION_FILE=%TEMP%\CodexFolderDrive_!RANDOM!.txt"
del /f /q "!FOLDER_SELECTION_FILE!" >nul 2>&1
set "CODEX_FOLDER_SELECTION_FILE=!FOLDER_SELECTION_FILE!"
call :PROMPT_MANAGER "select-drive"
set "SELECT_DRIVE_RC=!errorlevel!"
set "FOLDER_MANAGEMENT_DRIVE="
if exist "!FOLDER_SELECTION_FILE!" for /f "usebackq delims=" %%D in ("!FOLDER_SELECTION_FILE!") do if not defined FOLDER_MANAGEMENT_DRIVE set "FOLDER_MANAGEMENT_DRIVE=%%D"
del /f /q "!FOLDER_SELECTION_FILE!" >nul 2>&1
set "CODEX_FOLDER_SELECTION_FILE="
set "CODEX_FOLDER_DRIVE=!FOLDER_MANAGEMENT_DRIVE!"
if not "!SELECT_DRIVE_RC!"=="0" goto :ONE_CLICK_INIT_FAIL
if not defined FOLDER_MANAGEMENT_DRIVE (
    echo 未确认文件夹管理盘符，操作已取消。
    goto :ONE_CLICK_INIT_FAIL
)
echo 已确认文件夹管理盘符：!FOLDER_MANAGEMENT_DRIVE!:
exit /b 0

:INSTALL_FOLDER_MANAGEMENT
set "FOLDER_RESULT=FAIL"
call :PROMPT_MANAGER "install-folder"
if not errorlevel 1 set "FOLDER_RESULT=PASS"
exit /b !errorlevel!

:RUN_CUA_REPAIR
set "CUA_RESULT=FAIL"
set "CUA_REPORT_ROOT=%SESSION_DIR%\CUA"
if defined CODEX_FOLDER_DRIVE set "CUA_REPORT_ROOT=%CODEX_FOLDER_DRIVE%:\Codex\Temp\codex-cua-recovery"
setlocal DisableDelayedExpansion
set "CODEX_CUA_FIX_SELF=%SCRIPT_PATH%"
set "CODEX_CUA_FIX_MODE=repair"
set "CODEX_CUA_FIX_REPORT_ROOT=%CUA_REPORT_ROOT%"
chcp 65001 >nul
powershell.exe -NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand JABFAHIAcgBvAHIAQQBjAHQAaQBvAG4AUAByAGUAZgBlAHIAZQBuAGMAZQAgAD0AIAAnAFMAdABvAHAAJwAKAHQAcgB5ACAAewAKACAAIAAgACAAJABsAGkAbgBlAHMAIAA9ACAAWwBJAE8ALgBGAGkAbABlAF0AOgA6AFIAZQBhAGQAQQBsAGwATABpAG4AZQBzACgAJABlAG4AdgA6AEMATwBEAEUAWABfAEMAVQBBAF8ARgBJAFgAXwBTAEUATABGACwAIABbAFQAZQB4AHQALgBFAG4AYwBvAGQAaQBuAGcAXQA6ADoARwBlAHQARQBuAGMAbwBkAGkAbgBnACgAOQAzADYAKQApAAoAIAAgACAAIAAkAHMAdABhAHIAdAAgAD0AIABbAEEAcgByAGEAeQBdADoAOgBJAG4AZABlAHgATwBmACgAJABsAGkAbgBlAHMALAAgACcAOgA6AEMAVQBBAF8AUgBFAFAAQQBJAFIAXwBQAEEAWQBMAE8AQQBEAF8AQgBFAEcASQBOACcAKQAKACAAIAAgACAAJABmAGkAbgBpAHMAaAAgAD0AIABbAEEAcgByAGEAeQBdADoAOgBJAG4AZABlAHgATwBmACgAJABsAGkAbgBlAHMALAAgACcAOgA6AEMAVQBBAF8AUgBFAFAAQQBJAFIAXwBQAEEAWQBMAE8AQQBEAF8ARQBOAEQAJwApAAoAIAAgACAAIABpAGYAIAAoACQAcwB0AGEAcgB0ACAALQBsAHQAIAAwACAALQBvAHIAIAAkAGYAaQBuAGkAcwBoACAALQBsAGUAIAAkAHMAdABhAHIAdAApACAAewAgAHQAaAByAG8AdwAgACcARQBtAGIAZQBkAGQAZQBkACAAQwBVAEEAIAByAGUAcABhAGkAcgAgAHAAYQB5AGwAbwBhAGQAIABtAGEAcgBrAGUAcgBzACAAYQByAGUAIABtAGkAcwBzAGkAbgBnAC4AJwAgAH0ACgAgACAAIAAgACQAYgB1AGkAbABkAGUAcgAgAD0AIABOAGUAdwAtAE8AYgBqAGUAYwB0ACAAVABlAHgAdAAuAFMAdAByAGkAbgBnAEIAdQBpAGwAZABlAHIACgAgACAAIAAgAGYAbwByACAAKAAkAGkAIAA9ACAAJABzAHQAYQByAHQAIAArACAAMQA7ACAAJABpACAALQBsAHQAIAAkAGYAaQBuAGkAcwBoADsAIAAkAGkAKwArACkAIAB7AAoAIAAgACAAIAAgACAAIAAgAGkAZgAgACgAJABsAGkAbgBlAHMAWwAkAGkAXQAgAC0AbgBvAHQAbQBhAHQAYwBoACAAJwBeADoAOgBDAFUAQQBCADYANAA6ACgAWwBBAC0AWgBhAC0AegAwAC0AOQArAC8APQBdACsAKQAkACcAKQAgAHsAIAB0AGgAcgBvAHcAIAAnAEkAbgB2AGEAbABpAGQAIABlAG0AYgBlAGQAZABlAGQAIABDAFUAQQAgAHIAZQBwAGEAaQByACAAcABhAHkAbABvAGEAZAAgAGwAaQBuAGUALgAnACAAfQAKACAAIAAgACAAIAAgACAAIABbAHYAbwBpAGQAXQAkAGIAdQBpAGwAZABlAHIALgBBAHAAcABlAG4AZAAoACQATQBhAHQAYwBoAGUAcwBbADEAXQApAAoAIAAgACAAIAB9AAoAIAAgACAAIAAkAHAAYQB5AGwAbwBhAGQAIAA9ACAAWwBUAGUAeAB0AC4ARQBuAGMAbwBkAGkAbgBnAF0AOgA6AFUAVABGADgALgBHAGUAdABTAHQAcgBpAG4AZwAoAFsAQwBvAG4AdgBlAHIAdABdADoAOgBGAHIAbwBtAEIAYQBzAGUANgA0AFMAdAByAGkAbgBnACgAJABiAHUAaQBsAGQAZQByAC4AVABvAFMAdAByAGkAbgBnACgAKQApACkACgAgACAAIAAgACYAIAAoAFsAcwBjAHIAaQBwAHQAYgBsAG8AYwBrAF0AOgA6AEMAcgBlAGEAdABlACgAJABwAGEAeQBsAG8AYQBkACkAKQAKACAAIAAgACAAZQB4AGkAdAAgADAACgB9ACAAYwBhAHQAYwBoACAAewAKACAAIAAgACAAWwBDAG8AbgBzAG8AbABlAF0AOgA6AEUAcgByAG8AcgAuAFcAcgBpAHQAZQBMAGkAbgBlACgAJwBbAEYAQQBJAEwAXQAgAEMAVQBBACAAcgBlAHAAYQBpAHIAOgAgACcAIAArACAAJABfAC4ARQB4AGMAZQBwAHQAaQBvAG4ALgBNAGUAcwBzAGEAZwBlACkACgAgACAAIAAgAGUAeABpAHQAIAAxAAoAfQA=
set "CUA_REPAIR_RC=%errorlevel%"
chcp 936 >nul
endlocal & set "CUA_REPAIR_RC=%CUA_REPAIR_RC%"
if "%CUA_REPAIR_RC%"=="0" set "CUA_RESULT=PASS"
exit /b %CUA_REPAIR_RC%
:INSTALL_LUNA_PROMPT
set "LUNA_PROMPT_RESULT=FAIL"
set "LUNA_PROMPT_COUNT=0"
call :PROMPT_MANAGER "install-luna"
set "LUNA_INSTALL_RC=!errorlevel!"
if "!LUNA_INSTALL_RC!"=="0" (
    set "LUNA_PROMPT_RESULT=PASS"
    set "LUNA_PROMPT_COUNT=1"
)
exit /b !LUNA_INSTALL_RC!
:ONE_CLICK_INIT_FAIL
set "CODEX_ONE_CLICK_MODE="
set "CODEX_FOLDER_DRIVE="
exit /b 1
:PROXY_ONLY
call :CONFIRM "仅重建 V2Ray 代理配置将关闭 Codex 并覆盖 .env。是否继续？"
if errorlevel 1 exit /b 0

call :START_SESSION
if errorlevel 1 exit /b 1
call :BACKUP_ENV
if errorlevel 1 (
    echo .env 备份失败，已停止写入。
    exit /b 1
)
call :WRITE_SESSION_META
call :STOP_CODEX
call :WRITE_ENV

set "ENV_RESULT=FAIL"
if exist "%ENV_FILE%" (
    findstr /l /c:"HTTP_PROXY=%PROXY_URL%" "%ENV_FILE%" >nul && findstr /l /c:"HTTPS_PROXY=%PROXY_URL%" "%ENV_FILE%" >nul && findstr /l /c:"NO_PROXY=localhost,127.0.0.1,::1" "%ENV_FILE%" >nul && set "ENV_RESULT=PASS"
)
set "RESULT=!ENV_RESULT!"

call :WRITE_REPORT "仅重建 V2Ray 代理配置"
call :WRITE_ROLLBACK

echo.
echo 代理操作结果：!RESULT!
echo 代理文件：!ENV_FILE!
if /i "!REPORT_RESULT!"=="PASS" echo 报告：!REPORT_FILE!
if /i not "!REPORT_RESULT!"=="PASS" echo 报告：WRITE_FAILED
echo 回滚：!SESSION_DIR!\Rollback.cmd
exit /b 0

:SHOW_STATUS
echo.
echo -------------------- 当前状态 --------------------
echo Documents  : %DOCUMENTS_DIR%
echo 备份根目录 : %BACKUP_ROOT%
echo CODEX_HOME : %CODEX_HOME_DIR%
echo AGENTS.md  : %AGENTS_FILE%
echo RTK.md     : %RTK_FILE%
echo RTK 程序   : %RTK_EXE%
echo 配置文件   : %CONFIG_FILE%
if exist "%CONFIG_FILE%" (
    echo config.toml：存在
    echo.
    findstr /n /l /c:"model" /c:"model_reasoning_effort" /c:"enabled-reasoning-efforts" /c:"context_management" "%CONFIG_FILE%"
) else (
    echo config.toml：不存在
)
echo.
echo 代理文件   : %ENV_FILE%
if exist "%ENV_FILE%" (
    echo .env：存在
    findstr /n /l /c:"HTTP_PROXY" /c:"HTTPS_PROXY" /c:"NO_PROXY" "%ENV_FILE%"
) else (
    echo .env：不存在
)
echo.
echo RTK 状态：
if exist "%RTK_EXE%" (
    "%RTK_EXE%" --version
    "%RTK_EXE%" gain >nul 2>&1
    if errorlevel 1 (echo rtk gain：失败) else (echo rtk gain：通过)
) else (
    echo rtk.exe：不存在
)
echo.
echo AGENTS 规则状态：
call :VERIFY_AGENTS_CONTENT
echo AGENTS.md 目标内容：!AGENTS_RESULT!
echo GPT5.6 LUNA 提示词：!LUNA_PROMPT_RESULT!（数量 !LUNA_PROMPT_COUNT!）
if exist "%CODEX_HOME_DIR%\AGENTS.override.md" echo 提示：发现 AGENTS.override.md，可能覆盖 AGENTS.md
echo.
echo User CODEX_CLI_PATH：
reg query "HKCU\Environment" /v CODEX_CLI_PATH 2>nul
if errorlevel 1 echo 未设置
echo User CODEX_CODE_MODE_HOST_PATH：
reg query "HKCU\Environment" /v CODEX_CODE_MODE_HOST_PATH 2>nul
if errorlevel 1 echo 未设置
echo.
echo Machine 级覆盖检测：
reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v CODEX_CLI_PATH 2>nul
if errorlevel 1 echo Machine CODEX_CLI_PATH 未设置
reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v CODEX_CODE_MODE_HOST_PATH 2>nul
if errorlevel 1 echo Machine CODEX_CODE_MODE_HOST_PATH 未设置
echo ----------------------------------------------------
exit /b 0

:ROLLBACK_LATEST
if not exist "%BACKUP_ROOT%" (
    echo 未找到备份根目录：%BACKUP_ROOT%
    exit /b 1
)

set "LATEST_SESSION="
for /f "delims=" %%D in ('dir /b /ad /o-d "%BACKUP_ROOT%\Codex_Full_Reset_*" 2^>nul') do if not defined LATEST_SESSION set "LATEST_SESSION=%BACKUP_ROOT%\%%D"

if not defined LATEST_SESSION (
    echo 未找到可回滚的初始化目录。
    exit /b 1
)
if not exist "!LATEST_SESSION!\Rollback.cmd" (
    echo 最近目录没有 Rollback.cmd：!LATEST_SESSION!
    exit /b 1
)

echo 最近一次备份：!LATEST_SESSION!
call :CONFIRM "将从该目录恢复配置、代理和必要的 Runtime 状态。是否继续？"
if errorlevel 1 exit /b 0
call "!LATEST_SESSION!\Rollback.cmd"
exit /b !errorlevel!

:ROLLBACK_SESSION
set "SESSION_DIR=%~1"
if not defined SESSION_DIR (
    echo 未提供回滚目录。
    exit /b 1
)
if not exist "%SESSION_DIR%\session.meta" (
    echo 找不到回滚元数据：%SESSION_DIR%\session.meta
    exit /b 1
)

for /f "usebackq tokens=1,* delims==" %%A in ("%SESSION_DIR%\session.meta") do set "%%A=%%B"

call :STOP_CODEX

if "!RESTORE_CONFIG!"=="1" (
    if exist "%SESSION_DIR%\Backups\config.toml" (
        if not exist "%CODEX_HOME_DIR%" md "%CODEX_HOME_DIR%" >nul 2>&1
        copy /y "%SESSION_DIR%\Backups\config.toml" "%CONFIG_FILE%" >nul
        echo 已恢复 config.toml
    ) else if exist "%CONFIG_FILE%" (
        del /f /q "%CONFIG_FILE%" >nul
        echo 已删除初始化生成的 config.toml
    )
)

if "!RESTORE_ENV!"=="1" (
    if exist "%SESSION_DIR%\Backups\.env" (
        if not exist "%CODEX_HOME_DIR%" md "%CODEX_HOME_DIR%" >nul 2>&1
        copy /y "%SESSION_DIR%\Backups\.env" "%ENV_FILE%" >nul
        echo 已恢复 .env
    ) else if exist "%ENV_FILE%" (
        del /f /q "%ENV_FILE%" >nul
        echo 已删除初始化生成的 .env
    )
)

if "!RESTORE_AGENTS!"=="1" (
    if exist "%SESSION_DIR%\Backups\AGENTS.md" (
        if not exist "%CODEX_HOME_DIR%" md "%CODEX_HOME_DIR%" >nul 2>&1
        copy /y "%SESSION_DIR%\Backups\AGENTS.md" "%AGENTS_FILE%" >nul
        echo 已恢复 AGENTS.md
    ) else if exist "%AGENTS_FILE%" (
        del /f /q "%AGENTS_FILE%" >nul
        echo 已删除初始化生成的 AGENTS.md
    )
)

if "!RESTORE_RTK!"=="1" (
    if exist "%SESSION_DIR%\Backups\RTK.md" (
        if not exist "%CODEX_HOME_DIR%" md "%CODEX_HOME_DIR%" >nul 2>&1
        copy /y "%SESSION_DIR%\Backups\RTK.md" "%RTK_FILE%" >nul
        echo 已恢复 RTK.md
    ) else if exist "%RTK_FILE%" (
        del /f /q "%RTK_FILE%" >nul
        echo 已删除初始化生成的 RTK.md
    )
)

if "!RESTORE_RTK_EXE!"=="1" (
    if exist "%SESSION_DIR%\Backups\rtk\rtk.exe" (
        if not exist "%RTK_INSTALL_DIR%" md "%RTK_INSTALL_DIR%" >nul 2>&1
        copy /y "%SESSION_DIR%\Backups\rtk\rtk.exe" "%RTK_EXE%" >nul
        echo 已恢复 rtk.exe
    ) else if exist "%RTK_EXE%" (
        del /f /q "%RTK_EXE%" >nul
        echo 已删除初始化生成的 rtk.exe
    )
)

if "!RESTORE_USER_PATH!"=="1" call :RESTORE_USER_PATH

if "!RESTORE_CLI!"=="1" (
    if not exist "%CLI_DIR%" md "%CLI_DIR%" >nul 2>&1
    for %%F in (codex-latest.exe codex-code-mode-host.exe codex-windows-sandbox-setup.exe codex-command-runner.exe) do if exist "%SESSION_DIR%\Backups\cli\%%F" copy /y "%SESSION_DIR%\Backups\cli\%%F" "%CLI_DIR%\%%F" >nul
    echo 已恢复自定义 Runtime 备份文件
)

if "!RESTORE_RUNTIME!"=="1" (
    if "!OLD_CLI_PRESENT!"=="1" (
        reg add "HKCU\Environment" /v CODEX_CLI_PATH /t "!OLD_CLI_TYPE!" /d "!OLD_CLI_VALUE!" /f >nul
    ) else (
        reg delete "HKCU\Environment" /v CODEX_CLI_PATH /f >nul 2>&1
    )
    if "!OLD_HOST_PRESENT!"=="1" (
        reg add "HKCU\Environment" /v CODEX_CODE_MODE_HOST_PATH /t "!OLD_HOST_TYPE!" /d "!OLD_HOST_VALUE!" /f >nul
    ) else (
        reg delete "HKCU\Environment" /v CODEX_CODE_MODE_HOST_PATH /f >nul 2>&1
    )
    echo 已恢复用户级 Runtime 环境变量
)

echo.
echo 回滚完成：%SESSION_DIR%
exit /b 0

:START_SESSION
call :SET_TIMESTAMP
set "SESSION_DIR=%BACKUP_ROOT%\Codex_Full_Reset_!STAMP!"
set "BACKUP_DIR=!SESSION_DIR!\Backups"
set "RESTORE_CONFIG=0"
set "RESTORE_ENV=0"
set "RESTORE_CLI=0"
set "RESTORE_RUNTIME=0"
set "RESTORE_AGENTS=0"
set "RESTORE_RTK=0"
set "RESTORE_RTK_EXE=0"
set "RESTORE_USER_PATH=0"
set "USER_CLI_PRESENT=0"
set "USER_HOST_PRESENT=0"
set "USER_CLI_TYPE=REG_SZ"
set "USER_HOST_TYPE=REG_SZ"
set "USER_CLI_VALUE="
set "USER_HOST_VALUE="
set "MACHINE_CLI="
set "MACHINE_HOST="
set "CONFIG_RESULT=NOT_RUN"
set "ENV_RESULT=NOT_RUN"
set "BUNDLED_RESULT=NOT_RUN"
set "CUA_RESULT=NOT_RUN"
set "CUA_REPAIR_RC="
set "CUA_REPORT_ROOT="
set "BUNDLED_RUNTIME="
set "RTK_RESULT=NOT_RUN"
set "RTK_VERSION="
set "RTK_ACTIVE_PATH="
set "AGENTS_RESULT=NOT_RUN"
set "RTK_DOC_RESULT=NOT_RUN"
set "USER_PATH_RESULT=NOT_RUN"
set "AGENTS_OVERRIDE_RESULT=NOT_CHECKED"
set "RESULT=FAIL"

if not exist "%BACKUP_ROOT%" md "%BACKUP_ROOT%" >nul 2>&1
if not exist "%SESSION_DIR%" md "%SESSION_DIR%" >nul 2>&1
if not exist "%BACKUP_DIR%" md "%BACKUP_DIR%" >nul 2>&1
if not exist "%BACKUP_DIR%\cli" md "%BACKUP_DIR%\cli" >nul 2>&1
if not exist "%BACKUP_DIR%\rtk" md "%BACKUP_DIR%\rtk" >nul 2>&1

if not exist "%SESSION_DIR%" (
    echo 无法创建备份目录：%SESSION_DIR%
    exit /b 1
)
if not exist "%BACKUP_DIR%" (
    echo 无法创建备份子目录：%BACKUP_DIR%
    exit /b 1
)
exit /b 0

:SET_TIMESTAMP
set "STAMP="
for /f "delims=" %%I in ('powershell.exe -NoProfile -Command "Get-Date -Format yyyyMMdd-HHmmss" 2^>nul') do if not defined STAMP set "STAMP=%%I"
if defined STAMP exit /b 0
set "STAMP=%DATE:/=-%_%TIME::=-%"
set "STAMP=!STAMP: =0!"
set "STAMP=!STAMP:.=-!"
set "STAMP=!STAMP:/=-!"
exit /b 0

:BACKUP_CONFIG
set "RESTORE_CONFIG=1"
if exist "%CONFIG_FILE%" (
    copy /y "%CONFIG_FILE%" "%BACKUP_DIR%\config.toml" >nul
    if errorlevel 1 (
        echo config.toml 备份复制失败
        exit /b 1
    )
    echo 已备份 config.toml
) else (
    echo 原 config.toml 不存在，回滚时将删除初始化生成的文件
)
exit /b 0

:BACKUP_ENV
set "RESTORE_ENV=1"
if exist "%ENV_FILE%" (
    copy /y "%ENV_FILE%" "%BACKUP_DIR%\.env" >nul
    if errorlevel 1 (
        echo .env 备份复制失败
        exit /b 1
    )
    echo 已备份 .env
) else (
    echo 原 .env 不存在，回滚时将删除初始化生成的文件
)
exit /b 0

:CAPTURE_RUNTIME
set "RESTORE_RUNTIME=1"
for /f "tokens=2,*" %%A in ('reg query "HKCU\Environment" /v CODEX_CLI_PATH 2^>nul ^| findstr /i /c:"CODEX_CLI_PATH"') do (
    set "USER_CLI_PRESENT=1"
    set "USER_CLI_TYPE=%%A"
    set "USER_CLI_VALUE=%%B"
)
for /f "tokens=2,*" %%A in ('reg query "HKCU\Environment" /v CODEX_CODE_MODE_HOST_PATH 2^>nul ^| findstr /i /c:"CODEX_CODE_MODE_HOST_PATH"') do (
    set "USER_HOST_PRESENT=1"
    set "USER_HOST_TYPE=%%A"
    set "USER_HOST_VALUE=%%B"
)

for /f "tokens=2,*" %%A in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v CODEX_CLI_PATH 2^>nul ^| findstr /i /c:"CODEX_CLI_PATH"') do set "MACHINE_CLI=%%B"
for /f "tokens=2,*" %%A in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v CODEX_CODE_MODE_HOST_PATH 2^>nul ^| findstr /i /c:"CODEX_CODE_MODE_HOST_PATH"') do set "MACHINE_HOST=%%B"

(
    echo Time: %DATE% %TIME%
    echo.
    echo USER CODEX_CLI_PATH:
    if "!USER_CLI_PRESENT!"=="1" echo !USER_CLI_TYPE! !USER_CLI_VALUE!
    if "!USER_CLI_PRESENT!"=="0" echo NOT_SET
    echo.
    echo USER CODEX_CODE_MODE_HOST_PATH:
    if "!USER_HOST_PRESENT!"=="1" echo !USER_HOST_TYPE! !USER_HOST_VALUE!
    if "!USER_HOST_PRESENT!"=="0" echo NOT_SET
    echo.
    echo MACHINE CODEX_CLI_PATH:
    if defined MACHINE_CLI echo !MACHINE_CLI!
    if not defined MACHINE_CLI echo NOT_SET
    echo.
    echo MACHINE CODEX_CODE_MODE_HOST_PATH:
    if defined MACHINE_HOST echo !MACHINE_HOST!
    if not defined MACHINE_HOST echo NOT_SET
) > "%BACKUP_DIR%\runtime-overrides.before.txt"
if not exist "%BACKUP_DIR%\runtime-overrides.before.txt" exit /b 1
exit /b 0

:BACKUP_CUSTOM_FILES
set "RESTORE_CLI=1"
for %%F in (codex-latest.exe codex-code-mode-host.exe codex-windows-sandbox-setup.exe codex-command-runner.exe) do if exist "%CLI_DIR%\%%F" (
    copy /y "%CLI_DIR%\%%F" "%BACKUP_DIR%\cli\%%F" >nul
    if errorlevel 1 exit /b 1
    echo 已备份 %%F
)
exit /b 0

:BACKUP_AGENTS_AND_RTK
set "RESTORE_AGENTS=1"
set "RESTORE_RTK=1"
set "RESTORE_RTK_EXE=1"
if exist "%AGENTS_FILE%" (
    copy /y "%AGENTS_FILE%" "%BACKUP_DIR%\AGENTS.md" >nul
    if errorlevel 1 (
        echo AGENTS.md 备份复制失败
        exit /b 1
    )
    echo 已备份 AGENTS.md
) else (
    echo 原 AGENTS.md 不存在，回滚时将删除初始化生成的文件
)
if exist "%RTK_FILE%" (
    copy /y "%RTK_FILE%" "%BACKUP_DIR%\RTK.md" >nul
    if errorlevel 1 (
        echo RTK.md 备份复制失败
        exit /b 1
    )
    echo 已备份 RTK.md
) else (
    echo 原 RTK.md 不存在，回滚时将删除初始化生成的文件
)
if exist "%RTK_EXE%" (
    copy /y "%RTK_EXE%" "%BACKUP_DIR%\rtk\rtk.exe" >nul
    if errorlevel 1 (
        echo rtk.exe 备份复制失败
        exit /b 1
    )
    echo 已备份 rtk.exe
) else (
    echo 原 rtk.exe 不存在，回滚时将删除初始化生成的文件
)
exit /b 0

:CAPTURE_USER_PATH
set "RESTORE_USER_PATH=1"
set "USER_PATH_RESULT=FAIL"
set "CODEX_INIT_PATH_STATE=%BACKUP_DIR%\user-path.before.txt"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$p=[Environment]::GetEnvironmentVariable('Path','User'); $f=$env:CODEX_INIT_PATH_STATE; if($null -eq $p){[IO.File]::WriteAllText($f,'__CODEX_INIT_PATH_ABSENT__',(New-Object System.Text.UTF8Encoding -ArgumentList $false))}else{[IO.File]::WriteAllText($f,$p,(New-Object System.Text.UTF8Encoding -ArgumentList $false))}"
set "CODEX_INIT_PATH_STATE="
if exist "%BACKUP_DIR%\user-path.before.txt" (
    echo 已记录用户 PATH
    set "USER_PATH_RESULT=PASS"
) else (
    echo 用户 PATH 记录失败
    exit /b 1
)
exit /b 0

:RESTORE_USER_PATH
set "CODEX_INIT_PATH_STATE=%SESSION_DIR%\Backups\user-path.before.txt"
if not exist "%CODEX_INIT_PATH_STATE%" (
    echo 未找到用户 PATH 备份，跳过恢复
    set "USER_PATH_RESULT=FAIL"
    set "CODEX_INIT_PATH_STATE="
    exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$f=$env:CODEX_INIT_PATH_STATE; $v=[IO.File]::ReadAllText($f,(New-Object System.Text.UTF8Encoding -ArgumentList $false)); if($v -eq '__CODEX_INIT_PATH_ABSENT__'){[Environment]::SetEnvironmentVariable('Path',$null,'User')}else{[Environment]::SetEnvironmentVariable('Path',$v,'User')}"
if errorlevel 1 (
    echo 用户 PATH 恢复失败
    set "USER_PATH_RESULT=FAIL"
) else (
    echo 已恢复用户 PATH
    set "USER_PATH_RESULT=PASS"
)
set "CODEX_INIT_PATH_STATE="
exit /b 0

:WRITE_SESSION_META
(
    echo RESTORE_CONFIG=!RESTORE_CONFIG!
    echo RESTORE_ENV=!RESTORE_ENV!
    echo RESTORE_CLI=!RESTORE_CLI!
    echo RESTORE_RUNTIME=!RESTORE_RUNTIME!
    echo RESTORE_AGENTS=!RESTORE_AGENTS!
    echo RESTORE_RTK=!RESTORE_RTK!
    echo RESTORE_RTK_EXE=!RESTORE_RTK_EXE!
    echo RESTORE_USER_PATH=!RESTORE_USER_PATH!
    echo OLD_CLI_PRESENT=!USER_CLI_PRESENT!
    echo OLD_CLI_TYPE=!USER_CLI_TYPE!
    echo OLD_CLI_VALUE=!USER_CLI_VALUE!
    echo OLD_HOST_PRESENT=!USER_HOST_PRESENT!
    echo OLD_HOST_TYPE=!USER_HOST_TYPE!
    echo OLD_HOST_VALUE=!USER_HOST_VALUE!
) > "%SESSION_DIR%\session.meta"
exit /b 0

:STOP_CODEX
echo 正在关闭 ChatGPT / Codex 相关进程...
for %%P in (ChatGPT.exe codex.exe codex-latest.exe codex-code-mode-host.exe codex-command-runner.exe) do taskkill /f /im "%%P" >nul 2>&1
timeout /t 2 /nobreak >nul
exit /b 0

:CLEAR_USER_RUNTIME
reg delete "HKCU\Environment" /v CODEX_CLI_PATH /f >nul 2>&1
reg delete "HKCU\Environment" /v CODEX_CODE_MODE_HOST_PATH /f >nul 2>&1
set "CODEX_CLI_PATH="
set "CODEX_CODE_MODE_HOST_PATH="
echo 已清除 User CODEX_CLI_PATH
echo 已清除 User CODEX_CODE_MODE_HOST_PATH
if defined MACHINE_CLI echo 检测到 Machine CODEX_CLI_PATH：!MACHINE_CLI!
if defined MACHINE_HOST echo 检测到 Machine CODEX_CODE_MODE_HOST_PATH：!MACHINE_HOST!
exit /b 0

:REMOVE_CUSTOM_FILES
for %%F in (codex-latest.exe codex-code-mode-host.exe codex-windows-sandbox-setup.exe codex-command-runner.exe) do if exist "%CLI_DIR%\%%F" (
    del /f /q "%CLI_DIR%\%%F" >nul
    echo 已移除自定义副本：%%F
)
exit /b 0

:WRITE_CONFIG
if not exist "%CODEX_HOME_DIR%" md "%CODEX_HOME_DIR%" >nul 2>&1
set "LUNA_MANAGER_INSTALLED=1"
(
    echo # Codex initialization defaults
    echo model = "!DEFAULT_PARENT_MODEL_ID!"
    echo model_reasoning_effort = "!DEFAULT_PARENT_REASONING_EFFORT!"
    echo.
    echo [desktop]
    echo enabled-reasoning-efforts = ["low", "medium", "high", "xhigh", "max", "ultra"]
    echo.
    echo [features]
    echo context_management.experimental_mode = true
    if "!LUNA_MANAGER_INSTALLED!"=="1" echo multi_agent = true
    echo.
    if "!LUNA_MANAGER_INSTALLED!"=="1" (
        echo [features.multi_agent_v2]
        echo enabled = false
        echo.
        echo [agents]
        echo enabled = true
        echo max_concurrent_threads_per_session = !LUNA_MAX_THREADS!
        echo max_depth = 1
    )
) > "%CONFIG_FILE%"
echo Config written: %CONFIG_FILE%
exit /b 0

:WRITE_ENV
if not exist "%CODEX_HOME_DIR%" md "%CODEX_HOME_DIR%" >nul 2>&1
(
    echo HTTP_PROXY=%PROXY_URL%
    echo HTTPS_PROXY=%PROXY_URL%
    echo NO_PROXY=localhost,127.0.0.1,::1
) > "%ENV_FILE%"
echo 已写入：%ENV_FILE%
exit /b 0

:INSTALL_AND_INTEGRATE_RTK
set "RTK_RESULT=FAIL"
set "RTK_DOC_RESULT=FAIL"
set "USER_PATH_RESULT=FAIL"
set "RTK_ACTIVE_PATH="
set "RTK_VERSION="

call :FIND_VALID_RTK
if errorlevel 1 (
    call :DOWNLOAD_AND_INSTALL_RTK
    if errorlevel 1 exit /b 1
    call :FIND_VALID_RTK
    if errorlevel 1 exit /b 1
)

if exist "%RTK_EXE%" (
    call :ENSURE_USER_PATH
) else (
    set "USER_PATH_RESULT=PASS"
)

set "CODEX_HOME=%CODEX_HOME_DIR%"
call "!RTK_ACTIVE_PATH!" init --dry-run --global --codex > "%SESSION_DIR%\rtk-init-dry-run.log" 2>&1
if errorlevel 1 (
    echo RTK Codex 集成预检失败，请查看 rtk-init-dry-run.log
    exit /b 1
)

call "!RTK_ACTIVE_PATH!" init --global --codex > "%SESSION_DIR%\rtk-init.log" 2>&1
if errorlevel 1 (
    echo RTK Codex 集成失败，请查看 rtk-init.log
    exit /b 1
)
if not exist "%RTK_FILE%" (
    echo RTK.md 未生成，集成失败
    exit /b 1
)
call "!RTK_ACTIVE_PATH!" init --show --codex > "%SESSION_DIR%\rtk-init-show.log" 2>&1
if errorlevel 1 (
    echo RTK Codex 集成状态检查失败，请查看 rtk-init-show.log
    exit /b 1
)
set "CODEX_INIT_RTK_LOG=%SESSION_DIR%\rtk-init-show.log"
set "CODEX_INIT_RTK_EXPECTED=%RTK_FILE%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:CODEX_INIT_RTK_LOG; $needle=$env:CODEX_INIT_RTK_EXPECTED; if(-not(Test-Path -LiteralPath $p)){exit 2}; try{$text=[IO.File]::ReadAllText($p,(New-Object System.Text.UTF8Encoding -ArgumentList $false,$true))}catch{$text=[IO.File]::ReadAllText($p,[Text.Encoding]::Default)}; if($text.IndexOf($needle,[System.StringComparison]::OrdinalIgnoreCase) -lt 0){exit 1}; exit 0"
if errorlevel 1 (
    echo RTK 集成状态的目标路径不正确，已停止
    set "CODEX_INIT_RTK_LOG="
    set "CODEX_INIT_RTK_EXPECTED="
    exit /b 1
)
set "CODEX_INIT_RTK_LOG="
set "CODEX_INIT_RTK_EXPECTED="
call "!RTK_ACTIVE_PATH!" --version >nul 2>&1
if errorlevel 1 exit /b 1
for /f "delims=" %%V in ('"!RTK_ACTIVE_PATH!" --version 2^>nul') do if not defined RTK_VERSION set "RTK_VERSION=%%V"
set "RTK_RESULT=PASS"
set "RTK_DOC_RESULT=PASS"
exit /b 0

:FIND_VALID_RTK
set "RTK_ACTIVE_PATH="
set "RTK_VERSION="
if exist "%RTK_EXE%" (
    call "%RTK_EXE%" --version >nul 2>&1
    if not errorlevel 1 call "%RTK_EXE%" gain >nul 2>&1
    if not errorlevel 1 set "RTK_ACTIVE_PATH=%RTK_EXE%"
)
if not defined RTK_ACTIVE_PATH for /f "delims=" %%P in ('where rtk.exe 2^>nul') do if not defined RTK_ACTIVE_PATH set "RTK_ACTIVE_PATH=%%P"
if not defined RTK_ACTIVE_PATH exit /b 1
call "!RTK_ACTIVE_PATH!" --version >nul 2>&1
if errorlevel 1 (
    set "RTK_ACTIVE_PATH="
    exit /b 1
)
call "!RTK_ACTIVE_PATH!" gain >nul 2>&1
if errorlevel 1 (
    set "RTK_ACTIVE_PATH="
    exit /b 1
)
for /f "delims=" %%V in ('"!RTK_ACTIVE_PATH!" --version 2^>nul') do if not defined RTK_VERSION set "RTK_VERSION=%%V"
exit /b 0

:DOWNLOAD_AND_INSTALL_RTK
set "RTK_TMP_DIR=%TEMP%\CodexRtkInstall_!STAMP!"
set "RTK_ZIP=%RTK_TMP_DIR%\rtk-x86_64-pc-windows-msvc.zip"
set "RTK_CHECKSUMS=%RTK_TMP_DIR%\checksums.txt"
set "RTK_EXTRACT_DIR=%RTK_TMP_DIR%\extracted"
if exist "%RTK_TMP_DIR%" rmdir /s /q "%RTK_TMP_DIR%" >nul 2>&1
md "%RTK_TMP_DIR%" >nul 2>&1
md "%RTK_EXTRACT_DIR%" >nul 2>&1
if not exist "%RTK_EXTRACT_DIR%" exit /b 1

call :DOWNLOAD_FILE "%RTK_RELEASE_URL%" "%RTK_ZIP%"
if errorlevel 1 (
    echo RTK ZIP 下载失败
    rmdir /s /q "%RTK_TMP_DIR%" >nul 2>&1
    exit /b 1
)
call :DOWNLOAD_FILE "%RTK_CHECKSUM_URL%" "%RTK_CHECKSUMS%"
if errorlevel 1 (
    echo RTK 校验文件下载失败
    rmdir /s /q "%RTK_TMP_DIR%" >nul 2>&1
    exit /b 1
)
call :VERIFY_RTK_ZIP
if errorlevel 1 (
    echo RTK ZIP SHA-256 校验失败
    rmdir /s /q "%RTK_TMP_DIR%" >nul 2>&1
    exit /b 1
)

set "CODEX_INIT_RTK_ZIP=%RTK_ZIP%"
set "CODEX_INIT_RTK_EXTRACT=%RTK_EXTRACT_DIR%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Add-Type -AssemblyName System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::ExtractToDirectory($env:CODEX_INIT_RTK_ZIP,$env:CODEX_INIT_RTK_EXTRACT)"
set "CODEX_INIT_RTK_ZIP="
set "CODEX_INIT_RTK_EXTRACT="
if errorlevel 1 (
    echo RTK ZIP 解压失败
    rmdir /s /q "%RTK_TMP_DIR%" >nul 2>&1
    exit /b 1
)
set "RTK_EXTRACTED_EXE="
for /f "delims=" %%F in ('dir /b /s /a-d "%RTK_EXTRACT_DIR%\rtk.exe" 2^>nul') do if not defined RTK_EXTRACTED_EXE set "RTK_EXTRACTED_EXE=%%F"
if not defined RTK_EXTRACTED_EXE (
    echo ZIP 中未找到 rtk.exe
    rmdir /s /q "%RTK_TMP_DIR%" >nul 2>&1
    exit /b 1
)
if not exist "%RTK_INSTALL_DIR%" md "%RTK_INSTALL_DIR%" >nul 2>&1
if not exist "%RTK_INSTALL_DIR%" (
    echo 无法创建 RTK 安装目录：%RTK_INSTALL_DIR%
    rmdir /s /q "%RTK_TMP_DIR%" >nul 2>&1
    exit /b 1
)
copy /y "%RTK_EXTRACTED_EXE%" "%RTK_EXE%.new" >nul
if errorlevel 1 (
    echo 无法复制临时 rtk.exe
    rmdir /s /q "%RTK_TMP_DIR%" >nul 2>&1
    exit /b 1
)
move /y "%RTK_EXE%.new" "%RTK_EXE%" >nul
if errorlevel 1 (
    del /f /q "%RTK_EXE%.new" >nul 2>&1
    echo 无法替换 rtk.exe，可能被其他进程占用
    rmdir /s /q "%RTK_TMP_DIR%" >nul 2>&1
    exit /b 1
)
call "%RTK_EXE%" --version >nul 2>&1
if errorlevel 1 (
    echo 新安装的 rtk.exe 无法运行
    rmdir /s /q "%RTK_TMP_DIR%" >nul 2>&1
    exit /b 1
)
rmdir /s /q "%RTK_TMP_DIR%" >nul 2>&1
exit /b 0

:DOWNLOAD_FILE
set "CODEX_INIT_DOWNLOAD_URL=%~1"
set "CODEX_INIT_DOWNLOAD_TARGET=%~2"
where curl.exe >nul 2>&1
if not errorlevel 1 (
    curl.exe -fL --retry 3 --silent --show-error -o "%CODEX_INIT_DOWNLOAD_TARGET%" "%CODEX_INIT_DOWNLOAD_URL%"
) else (
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$ProgressPreference='SilentlyContinue'; [Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12; Invoke-WebRequest -UseBasicParsing -Uri $env:CODEX_INIT_DOWNLOAD_URL -OutFile $env:CODEX_INIT_DOWNLOAD_TARGET"
)
set "DOWNLOAD_RESULT=%errorlevel%"
set "CODEX_INIT_DOWNLOAD_URL="
set "CODEX_INIT_DOWNLOAD_TARGET="
if not "%DOWNLOAD_RESULT%"=="0" exit /b 1
if not exist "%~2" exit /b 1
exit /b 0

:VERIFY_RTK_ZIP
set "CODEX_INIT_RTK_ZIP=%RTK_ZIP%"
set "CODEX_INIT_RTK_CHECKSUMS=%RTK_CHECKSUMS%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$zip=$env:CODEX_INIT_RTK_ZIP; $sum=$env:CODEX_INIT_RTK_CHECKSUMS; $expected=$null; foreach($line in [IO.File]::ReadAllLines($sum)){if($line -match '([0-9A-Fa-f]{64})\s+\*?rtk-x86_64-pc-windows-msvc\.zip\s*$'){$expected=$Matches[1];break}}; if([string]::IsNullOrEmpty($expected)){exit 2}; $sha=[Security.Cryptography.SHA256]::Create(); $stream=[IO.File]::OpenRead($zip); try{$actual=([BitConverter]::ToString($sha.ComputeHash($stream))).Replace('-','').ToLowerInvariant()}finally{$stream.Dispose();$sha.Dispose()}; if($actual -ne $expected.ToLowerInvariant()){exit 3}; exit 0"
set "CODEX_INIT_RTK_ZIP="
set "CODEX_INIT_RTK_CHECKSUMS="
set "RTK_HASH_RESULT=%errorlevel%"
exit /b %RTK_HASH_RESULT%

:ENSURE_USER_PATH
set "USER_PATH_RESULT=FAIL"
set "CODEX_INIT_RTK_PATH=%RTK_INSTALL_DIR%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$entry=$env:CODEX_INIT_RTK_PATH; $old=[Environment]::GetEnvironmentVariable('Path','User'); $parts=@(); if($null -ne $old -and $old.Length -gt 0){$parts=$old -split ';'}; $found=$false; foreach($part in $parts){if($part.TrimEnd([char[]]('/\')) -ieq $entry.TrimEnd([char[]]('/\'))){$found=$true;break}}; if(-not $found){if([string]::IsNullOrEmpty($old)){[Environment]::SetEnvironmentVariable('Path',$entry,'User')}else{[Environment]::SetEnvironmentVariable('Path',($old+';'+$entry),'User')}}"
if errorlevel 1 (
    set "CODEX_INIT_RTK_PATH="
    exit /b 1
)
set "PATH=%RTK_INSTALL_DIR%;%PATH%"
set "USER_PATH_RESULT=PASS"
set "CODEX_INIT_RTK_PATH="
exit /b 0

:WRITE_AGENTS_RULES
set "AGENTS_RESULT=FAIL"
if not exist "%CODEX_HOME_DIR%" md "%CODEX_HOME_DIR%" >nul 2>&1
set "CODEX_INIT_AGENTS_FILE=%AGENTS_FILE%"
set "CODEX_INIT_RTK_FILE=%RTK_FILE%"
set "CODEX_INIT_RULES_B64=PCEtLSBCRUdJTiBDT0RFWCBJTklUIFdPUksgUlVMRVMgLS0+DQpHT0FMDQrmnIDnu4jlj6/pqozmlLbnu5PmnpzmmK/ku4DkuYjvvIzkuqTnu5nmiJHml7blv4Xpobvmu6HotrPku4DkuYjjgIINCg0KQVVUT05PTVkNCuW4uOinhOe8uuWPo+iHquihjOWBh+iuvuW5tue7p+e7reOAgg0K5Y+q5pyJ57y65aSx5L+h5oGv5Lya5a6e6LSo5pS55Y+Y57uT5p6c5pe25omN5o+Q6Zeu44CCDQrmj5Dpl67ml7blkIzml7bmjqjov5vkuI3kvp3otZbor6XnrZTmoYjnmoTpg6jliIbjgIINCg0KUFJJT1JJVFkNCuaIkeeahOaYvuW8j+aMh+S7pCA+IOW9k+WJjeWvueivneihpeWFhSA+IHNraWxscyAvIEFHRU5UUy5tZCDnmoTkuIDoiKzmjIflr7zjgIINCuWGsueqgeaXtuaMh+WHuuaWh+S7tui3r+W+hOWSjOWOn+WPpeOAgg0KDQpTVFlMRQ0K5YWI57uT6K6677yM55+t5q616JC977yM56aB5aWX6K+d44CCDQoNCkRPTkUNCuWBmuW3peS9nOacrOi6q++8jOS4jeWPque7meiuoeWIkuOAgg0K5oyB57ut5Yiw6K+35rGC55qE57uT5p6c55yf5q2j5a6M55yf5q2j5a6M5oiQ44CCDQoNCkFQUFJPVkFMDQrlj6ror7sgLyDlj6/pgIbvvJrnm7TmjqXlgZrjgIINCuS4jeWPr+mAhuWklumDqOWKqOS9nO+8muWFiOS6pOWHuuWPr+WuoemYhee7k+aenO+8jOWGjemXruacgOWQjuS4gOatpeOAgg0KDQpWRVJJRlkNCua1i+ivleS4juaUueWKqOaIkOavlOS+i+OAguW/heimgeajgOafpemAmui/h+WNs+WBnOOAgg0KDQpTVUJBR0VOVFMNCueLrOeri+WtkOS7u+WKoeWPr+W5tuihjOWwseW5tuihjOOAguS9oOi0n+i0o+aVtOWQiOS4juijgeWGs+OAgg0KPCEtLSBFTkQgQ09ERVggSU5JVCBXT1JLIFJVTEVTIC0tPg0K"
call :REBUILD_AGENTS_FILE
if errorlevel 1 (
    set "CODEX_INIT_AGENTS_FILE="
    set "CODEX_INIT_RTK_FILE="
    set "CODEX_INIT_RULES_B64="
    exit /b 1
)
call :VERIFY_AGENTS_CONTENT
if errorlevel 1 (
    set "CODEX_INIT_AGENTS_FILE="
    set "CODEX_INIT_RTK_FILE="
    set "CODEX_INIT_RULES_B64="
    exit /b 1
)
if exist "%CODEX_HOME_DIR%\AGENTS.override.md" (set "AGENTS_OVERRIDE_RESULT=PRESENT") else (set "AGENTS_OVERRIDE_RESULT=ABSENT")
set "AGENTS_RESULT=PASS"
set "CODEX_INIT_AGENTS_FILE="
set "CODEX_INIT_RTK_FILE="
set "CODEX_INIT_RULES_B64="
exit /b 0


:REBUILD_AGENTS_FILE
powershell.exe -NoProfile -ExecutionPolicy Bypass -EncodedCommand JABFAHIAcgBvAHIAQQBjAHQAaQBvAG4AUAByAGUAZgBlAHIAZQBuAGMAZQAgAD0AIAAnAFMAdABvAHAAJwAKAHQAcgB5ACAAewAKACAAIAAgACAAJABwAGEAdABoACAAPQAgACQAZQBuAHYAOgBDAE8ARABFAFgAXwBJAE4ASQBUAF8AQQBHAEUATgBUAFMAXwBGAEkATABFAAoAIAAgACAAIAAkAHIAdABrACAAPQAgACQAZQBuAHYAOgBDAE8ARABFAFgAXwBJAE4ASQBUAF8AUgBUAEsAXwBGAEkATABFAAoAIAAgACAAIAAkAHIAdQBsAGUAcwAgAD0AIABbAFQAZQB4AHQALgBFAG4AYwBvAGQAaQBuAGcAXQA6ADoAVQBUAEYAOAAuAEcAZQB0AFMAdAByAGkAbgBnACgAWwBDAG8AbgB2AGUAcgB0AF0AOgA6AEYAcgBvAG0AQgBhAHMAZQA2ADQAUwB0AHIAaQBuAGcAKAAkAGUAbgB2ADoAQwBPAEQARQBYAF8ASQBOAEkAVABfAFIAVQBMAEUAUwBfAEIANgA0ACkAKQAKACAAIAAgACAAJAByAHUAbABlAHMAPQAkAHIAdQBsAGUAcwAuAFIAZQBwAGwAYQBjAGUAKAAnAAFj7X4wUveLQmyEdtN+nGcfd2NrjFsfd2NrjFsQYgIwJwAsACcAAWPtfjBS94tCbIR2036cZx93Y2uMWxBiAjAnACkAOwAKACAAIAAgACAAJAByAHUAbABlAHMAIAA9ACAAJAByAHUAbABlAHMALgBSAGUAcABsAGEAYwBlACgAKABbAHMAdAByAGkAbgBnAF0AWwBjAGgAYQByAF0AMQAzACkAIAArACAAKABbAHMAdAByAGkAbgBnAF0AWwBjAGgAYQByAF0AMQAwACkALAAgAFsAcwB0AHIAaQBuAGcAXQBbAGMAaABhAHIAXQAxADAAKQAuAFIAZQBwAGwAYQBjAGUAKABbAHMAdAByAGkAbgBnAF0AWwBjAGgAYQByAF0AMQAzACwAIABbAHMAdAByAGkAbgBnAF0AWwBjAGgAYQByAF0AMQAwACkACgAgACAAIAAgACQAbwBsAGQAVABlAHgAdAAgAD0AIAAnACcACgAgACAAIAAgAGkAZgAgACgAVABlAHMAdAAtAFAAYQB0AGgAIAAtAEwAaQB0AGUAcgBhAGwAUABhAHQAaAAgACQAcABhAHQAaAApACAAewAgACQAbwBsAGQAVABlAHgAdAAgAD0AIABbAEkATwAuAEYAaQBsAGUAXQA6ADoAUgBlAGEAZABBAGwAbABUAGUAeAB0ACgAJABwAGEAdABoACwAIABbAFQAZQB4AHQALgBFAG4AYwBvAGQAaQBuAGcAXQA6ADoAVQBUAEYAOAApACAAfQAKACAAIAAgACAAJABvAGwAZABUAGUAeAB0ACAAPQAgACQAbwBsAGQAVABlAHgAdAAuAFIAZQBwAGwAYQBjAGUAKAAoAFsAcwB0AHIAaQBuAGcAXQBbAGMAaABhAHIAXQAxADMAKQAgACsAIAAoAFsAcwB0AHIAaQBuAGcAXQBbAGMAaABhAHIAXQAxADAAKQAsACAAWwBzAHQAcgBpAG4AZwBdAFsAYwBoAGEAcgBdADEAMAApAC4AUgBlAHAAbABhAGMAZQAoAFsAcwB0AHIAaQBuAGcAXQBbAGMAaABhAHIAXQAxADMALAAgAFsAcwB0AHIAaQBuAGcAXQBbAGMAaABhAHIAXQAxADAAKQAKACAAIAAgACAAZgB1AG4AYwB0AGkAbwBuACAARwBlAHQALQBNAGEAbgBhAGcAZQBkAEIAbABvAGMAawBzACgAWwBzAHQAcgBpAG4AZwBdACQAVABlAHgAdAAsACAAWwBzAHQAcgBpAG4AZwBdACQAUAByAGUAZgBpAHgAKQAgAHsACgAgACAAIAAgACAAIAAgACAAJABwAGEAdAB0AGUAcgBuACAAPQAgACcAKAA/AG0AKQBeAFsAIABcAHQAXQAqACgAPwA8AGsAaQBuAGQAPgBCAEUARwBJAE4AfABFAE4ARAApACAAJwAgACsAIABbAHIAZQBnAGUAeABdADoAOgBFAHMAYwBhAHAAZQAoACQAUAByAGUAZgBpAHgAKQAgACsAIAAnACAAKAA/ADwAdgBlAHIAcwBpAG8AbgA+AFYAXABkACsAKAA/ADoAXAAuAFwAZAArACkAKgApAFsAIABcAHQAXQAqACQAJwAKACAAIAAgACAAIAAgACAAIAAkAG8AcABlAG4AIAA9ACAAJABuAHUAbABsAAoAIAAgACAAIAAgACAAIAAgACQAYgBsAG8AYwBrAHMAIAA9ACAATgBlAHcALQBPAGIAagBlAGMAdAAgACcAUwB5AHMAdABlAG0ALgBDAG8AbABsAGUAYwB0AGkAbwBuAHMALgBHAGUAbgBlAHIAaQBjAC4ATABpAHMAdABbAHMAdAByAGkAbgBnAF0AJwAKACAAIAAgACAAIAAgACAAIABmAG8AcgBlAGEAYwBoACAAKAAkAG0AYQB0AGMAaAAgAGkAbgAgAFsAcgBlAGcAZQB4AF0AOgA6AE0AYQB0AGMAaABlAHMAKAAkAFQAZQB4AHQALAAgACQAcABhAHQAdABlAHIAbgApACkAIAB7AAoAIAAgACAAIAAgACAAIAAgACAAIAAgACAAaQBmACAAKAAkAG0AYQB0AGMAaAAuAEcAcgBvAHUAcABzAFsAJwBrAGkAbgBkACcAXQAuAFYAYQBsAHUAZQAgAC0AZQBxACAAJwBCAEUARwBJAE4AJwApACAAewAKACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAaQBmACAAKAAkAG4AdQBsAGwAIAAtAG4AZQAgACQAbwBwAGUAbgApACAAewAgAHQAaAByAG8AdwAgACIATgBlAHMAdABlAGQAIABvAHIAIAB1AG4AYwBsAG8AcwBlAGQAIAAkAFAAcgBlAGYAaQB4ACAAYgBsAG8AYwBrAC4AIgAgAH0ACgAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACQAbwBwAGUAbgAgAD0AIAAkAG0AYQB0AGMAaAAKACAAIAAgACAAIAAgACAAIAAgACAAIAAgAH0AIABlAGwAcwBlACAAewAKACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAaQBmACAAKAAkAG4AdQBsAGwAIAAtAGUAcQAgACQAbwBwAGUAbgApACAAewAgAHQAaAByAG8AdwAgACIAJABQAHIAZQBmAGkAeAAgAEUATgBEACAAbQBhAHIAawBlAHIAIABoAGEAcwAgAG4AbwAgAG0AYQB0AGMAaABpAG4AZwAgAEIARQBHAEkATgAgAG0AYQByAGsAZQByAC4AIgAgAH0ACgAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgAGkAZgAgACgAJABvAHAAZQBuAC4ARwByAG8AdQBwAHMAWwAnAHYAZQByAHMAaQBvAG4AJwBdAC4AVgBhAGwAdQBlACAALQBuAGUAIAAkAG0AYQB0AGMAaAAuAEcAcgBvAHUAcABzAFsAJwB2AGUAcgBzAGkAbwBuACcAXQAuAFYAYQBsAHUAZQApACAAewAgAHQAaAByAG8AdwAgACIAJABQAHIAZQBmAGkAeAAgAG0AYQByAGsAZQByACAAdgBlAHIAcwBpAG8AbgBzACAAZABvACAAbgBvAHQAIABtAGEAdABjAGgALgAiACAAfQAKACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAJABsAGUAbgBnAHQAaAAgAD0AIAAkAG0AYQB0AGMAaAAuAEkAbgBkAGUAeAAgACsAIAAkAG0AYQB0AGMAaAAuAEwAZQBuAGcAdABoACAALQAgACQAbwBwAGUAbgAuAEkAbgBkAGUAeAAKACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAWwB2AG8AaQBkAF0AJABiAGwAbwBjAGsAcwAuAEEAZABkACgAJABUAGUAeAB0AC4AUwB1AGIAcwB0AHIAaQBuAGcAKAAkAG8AcABlAG4ALgBJAG4AZABlAHgALAAgACQAbABlAG4AZwB0AGgAKQApAAoAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAkAG8AcABlAG4AIAA9ACAAJABuAHUAbABsAAoAIAAgACAAIAAgACAAIAAgACAAIAAgACAAfQAKACAAIAAgACAAIAAgACAAIAB9AAoAIAAgACAAIAAgACAAIAAgAGkAZgAgACgAJABuAHUAbABsACAALQBuAGUAIAAkAG8AcABlAG4AKQAgAHsAIAB0AGgAcgBvAHcAIAAiACQAUAByAGUAZgBpAHgAIABCAEUARwBJAE4AIABtAGEAcgBrAGUAcgAgAGgAYQBzACAAbgBvACAAbQBhAHQAYwBoAGkAbgBnACAARQBOAEQAIABtAGEAcgBrAGUAcgAuACIAIAB9AAoAIAAgACAAIAAgACAAIAAgAHIAZQB0AHUAcgBuACAAJABiAGwAbwBjAGsAcwAuAFQAbwBBAHIAcgBhAHkAKAApAAoAIAAgACAAIAB9AAoAIAAgACAAIAAkAG0AYQBuAGEAZwBlAGQAIAA9ACAATgBlAHcALQBPAGIAagBlAGMAdAAgACcAUwB5AHMAdABlAG0ALgBDAG8AbABsAGUAYwB0AGkAbwBuAHMALgBHAGUAbgBlAHIAaQBjAC4ATABpAHMAdABbAHMAdAByAGkAbgBnAF0AJwAKACAAIAAgACAAJABtAGEAbgBhAGcAZQBkAFMAcABlAGMAcwAgAD0AIABAACgACgAgACAAIAAgACAAIAAgACAAWwBwAHMAYwB1AHMAdABvAG0AbwBiAGoAZQBjAHQAXQBAAHsAIABQAHIAZQBmAGkAeAAgAD0AIAAnAEMATwBEAEUAWAAgAEwAVQBOAEEAIABQAFIATwBNAFAAVAAnADsAIABWAGUAcgBzAGkAbwBuACAAPQAgACcAVgAxAC4AMwAnACAAfQAsAAoAIAAgACAAIAAgACAAIAAgAFsAcABzAGMAdQBzAHQAbwBtAG8AYgBqAGUAYwB0AF0AQAB7ACAAUAByAGUAZgBpAHgAIAA9ACAAJwBDAE8ARABFAFgAIABGAE8ATABEAEUAUgAgAE0AQQBOAEEARwBFAE0ARQBOAFQAIABQAFIATwBNAFAAVAAnADsAIABWAGUAcgBzAGkAbwBuACAAPQAgACcAVgAxAC4AMAAnACAAfQAKACAAIAAgACAAKQAKACAAIAAgACAAZgBvAHIAZQBhAGMAaAAgACgAJABzAHAAZQBjACAAaQBuACAAJABtAGEAbgBhAGcAZQBkAFMAcABlAGMAcwApACAAewAKACAAIAAgACAAIAAgACAAIABmAG8AcgBlAGEAYwBoACAAKAAkAGIAbABvAGMAawAgAGkAbgAgACgARwBlAHQALQBNAGEAbgBhAGcAZQBkAEIAbABvAGMAawBzACAAJABvAGwAZABUAGUAeAB0ACAAJABzAHAAZQBjAC4AUAByAGUAZgBpAHgAKQApACAAewAKACAAIAAgACAAIAAgACAAIAAgACAAIAAgAGkAZgAgACgAJABiAGwAbwBjAGsAIAAtAG0AYQB0AGMAaAAgACgAJwAoAD8AbQApAF4AQgBFAEcASQBOACAAJwAgACsAIABbAHIAZQBnAGUAeABdADoAOgBFAHMAYwBhAHAAZQAoACQAcwBwAGUAYwAuAFAAcgBlAGYAaQB4ACkAIAArACAAJwAgACgAPwA8AHYAZQByAHMAaQBvAG4APgBWAFwAZAArACgAPwA6AFwALgBcAGQAKwApACoAKQBbACAAXAB0AF0AKgAkACcAKQAgAC0AYQBuAGQAIAAkAE0AYQB0AGMAaABlAHMAWwAnAHYAZQByAHMAaQBvAG4AJwBdACAALQBlAHEAIAAkAHMAcABlAGMALgBWAGUAcgBzAGkAbwBuACkAIAB7AAoAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIAAgACAAIABbAHYAbwBpAGQAXQAkAG0AYQBuAGEAZwBlAGQALgBBAGQAZAAoACQAYgBsAG8AYwBrACkACgAgACAAIAAgACAAIAAgACAAIAAgACAAIAB9AAoAIAAgACAAIAAgACAAIAAgAH0ACgAgACAAIAAgAH0ACgAgACAAIAAgACQAcgBlAGYAZQByAGUAbgBjAGUAIAA9ACAAJwBAACcAIAArACAAJAByAHQAawAKACAAIAAgACAAJABuAGUAdwBUAGUAeAB0ACAAPQAgACQAcgBlAGYAZQByAGUAbgBjAGUAIAArACAAKABbAHMAdAByAGkAbgBnAF0AWwBjAGgAYQByAF0AMQAwACkAIAArACAAKABbAHMAdAByAGkAbgBnAF0AWwBjAGgAYQByAF0AMQAwACkAIAArACAAJAByAHUAbABlAHMALgBUAHIAaQBtAEUAbgBkACgAWwBjAGgAYQByAFsAXQBdAEAAKABbAGMAaABhAHIAXQAxADMALAAgAFsAYwBoAGEAcgBdADEAMAApACkAIAArACAAKABbAHMAdAByAGkAbgBnAF0AWwBjAGgAYQByAF0AMQAwACkACgAgACAAIAAgAGkAZgAgACgAJABtAGEAbgBhAGcAZQBkAC4AQwBvAHUAbgB0ACAALQBnAHQAIAAwACkAIAB7ACAAJABuAGUAdwBUAGUAeAB0ACAAKwA9ACAAKABbAHMAdAByAGkAbgBnAF0AWwBjAGgAYQByAF0AMQAwACkAIAArACAAKAAkAG0AYQBuAGEAZwBlAGQAIAAtAGoAbwBpAG4AIAAoACgAWwBzAHQAcgBpAG4AZwBdAFsAYwBoAGEAcgBdADEAMAApACAAKwAgACgAWwBzAHQAcgBpAG4AZwBdAFsAYwBoAGEAcgBdADEAMAApACkAKQAgACsAIAAoAFsAcwB0AHIAaQBuAGcAXQBbAGMAaABhAHIAXQAxADAAKQAgAH0ACgAgACAAIAAgAFsASQBPAC4ARgBpAGwAZQBdADoAOgBXAHIAaQB0AGUAQQBsAGwAVABlAHgAdAAoACQAcABhAHQAaAAsACAAJABuAGUAdwBUAGUAeAB0ACwAIAAoAE4AZQB3AC0ATwBiAGoAZQBjAHQAIABTAHkAcwB0AGUAbQAuAFQAZQB4AHQALgBVAFQARgA4AEUAbgBjAG8AZABpAG4AZwAgAC0AQQByAGcAdQBtAGUAbgB0AEwAaQBzAHQAIAAkAGYAYQBsAHMAZQApACkACgAgACAAIAAgAGUAeABpAHQAIAAwAAoAfQAgAGMAYQB0AGMAaAAgAHsACgAgACAAIAAgAFsAQwBvAG4AcwBvAGwAZQBdADoAOgBFAHIAcgBvAHIALgBXAHIAaQB0AGUATABpAG4AZQAoACQAXwAuAEUAeABjAGUAcAB0AGkAbwBuAC4ATQBlAHMAcwBhAGcAZQApAAoAIAAgACAAIABlAHgAaQB0ACAAMQAKAH0A
if errorlevel 1 exit /b 1
exit /b 0
:VERIFY_AGENTS_CONTENT
set "AGENTS_RESULT=FAIL"
set "LUNA_PROMPT_RESULT=FAIL"
set "LUNA_PROMPT_COUNT=UNKNOWN"
set "AGENTS_CHECK="
set "CODEX_INIT_AGENTS_FILE=%AGENTS_FILE%"
set "CODEX_INIT_RTK_FILE=%RTK_FILE%"
set "CODEX_INIT_SCRIPT_PATH=%SCRIPT_PATH%"
for /f "tokens=1,2 delims=|" %%A in ('powershell.exe -NoProfile -ExecutionPolicy Bypass -EncodedCommand JABFAHIAcgBvAHIAQQBjAHQAaQBvAG4AUAByAGUAZgBlAHIAZQBuAGMAZQAgAD0AIAAnAFMAdABvAHAAJwAKAHQAcgB5ACAAewAKACAAIAAgACAAJABzAGMAcgBpAHAAdABQAGEAdABoACAAPQAgACQAZQBuAHYAOgBDAE8ARABFAFgAXwBJAE4ASQBUAF8AUwBDAFIASQBQAFQAXwBQAEEAVABIAAoAIAAgACAAIABpAGYAIAAoAFsAcwB0AHIAaQBuAGcAXQA6ADoASQBzAE4AdQBsAGwATwByAFcAaABpAHQAZQBTAHAAYQBjAGUAKAAkAHMAYwByAGkAcAB0AFAAYQB0AGgAKQAgAC0AbwByACAALQBuAG8AdAAgACgAVABlAHMAdAAtAFAAYQB0AGgAIAAtAEwAaQB0AGUAcgBhAGwAUABhAHQAaAAgACQAcwBjAHIAaQBwAHQAUABhAHQAaAAgAC0AUABhAHQAaABUAHkAcABlACAATABlAGEAZgApACkAIAB7ACAAdABoAHIAbwB3ACAAJwBUAGgAZQAgAGkAbgBpAHQAaQBhAGwAaQB6AGUAcgAgAHMAYwByAGkAcAB0ACAAcABhAHQAaAAgAGkAcwAgAHUAbgBhAHYAYQBpAGwAYQBiAGwAZQAuACcAIAB9AAoAIAAgACAAIAAkAHAAYQByAHQAcwAgAD0AIABOAGUAdwAtAE8AYgBqAGUAYwB0ACAAJwBTAHkAcwB0AGUAbQAuAEMAbwBsAGwAZQBjAHQAaQBvAG4AcwAuAEcAZQBuAGUAcgBpAGMALgBMAGkAcwB0AFsAcwB0AHIAaQBuAGcAXQAnAAoAIAAgACAAIAAkAGkAbgBzAGkAZABlACAAPQAgACQAZgBhAGwAcwBlAAoAIAAgACAAIABmAG8AcgBlAGEAYwBoACAAKAAkAGwAaQBuAGUAIABpAG4AIABbAEkATwAuAEYAaQBsAGUAXQA6ADoAUgBlAGEAZABBAGwAbABMAGkAbgBlAHMAKAAkAHMAYwByAGkAcAB0AFAAYQB0AGgALAAgAFsAVABlAHgAdAAuAEUAbgBjAG8AZABpAG4AZwBdADoAOgBEAGUAZgBhAHUAbAB0ACkAKQAgAHsACgAgACAAIAAgACAAIAAgACAAaQBmACAAKAAkAGwAaQBuAGUAIAAtAGMAZQBxACAAJwA6ADoAQQBHAEUATgBUAFMAXwBWAEUAUgBJAEYAWQBfAFAAQQBZAEwATwBBAEQAXwBCAEUARwBJAE4AJwApACAAewAgACQAaQBuAHMAaQBkAGUAIAA9ACAAJAB0AHIAdQBlADsAIABjAG8AbgB0AGkAbgB1AGUAIAB9AAoAIAAgACAAIAAgACAAIAAgAGkAZgAgACgAJABsAGkAbgBlACAALQBjAGUAcQAgACcAOgA6AEEARwBFAE4AVABTAF8AVgBFAFIASQBGAFkAXwBQAEEAWQBMAE8AQQBEAF8ARQBOAEQAJwApACAAewAgAGIAcgBlAGEAawAgAH0ACgAgACAAIAAgACAAIAAgACAAaQBmACAAKAAkAGkAbgBzAGkAZABlACAALQBhAG4AZAAgACQAbABpAG4AZQAgAC0AbQBhAHQAYwBoACAAJwBeADoAOgBBAFYAQgA2ADQAOgAoAFsAQQAtAFoAYQAtAHoAMAAtADkAKwAvAD0AXQArACkAJAAnACkAIAB7ACAAWwB2AG8AaQBkAF0AJABwAGEAcgB0AHMALgBBAGQAZAAoACQATQBhAHQAYwBoAGUAcwBbADEAXQApACAAfQAKACAAIAAgACAAfQAKACAAIAAgACAAaQBmACAAKAAkAHAAYQByAHQAcwAuAEMAbwB1AG4AdAAgAC0AZQBxACAAMAApACAAewAgAHQAaAByAG8AdwAgACcARQBtAGIAZQBkAGQAZQBkACAAQQBHAEUATgBUAFMAIAB2AGUAcgBpAGYAaQBjAGEAdABpAG8AbgAgAHAAYQB5AGwAbwBhAGQAIABpAHMAIABtAGkAcwBzAGkAbgBnAC4AJwAgAH0ACgAgACAAIAAgACQAYwBvAGQAZQAgAD0AIABbAFQAZQB4AHQALgBFAG4AYwBvAGQAaQBuAGcAXQA6ADoAVQBUAEYAOAAuAEcAZQB0AFMAdAByAGkAbgBnACgAWwBDAG8AbgB2AGUAcgB0AF0AOgA6AEYAcgBvAG0AQgBhAHMAZQA2ADQAUwB0AHIAaQBuAGcAKAAoACQAcABhAHIAdABzACAALQBqAG8AaQBuACAAJwAnACkAKQApAAoAIAAgACAAIAAmACAAKABbAHMAYwByAGkAcAB0AGIAbABvAGMAawBdADoAOgBDAHIAZQBhAHQAZQAoACQAYwBvAGQAZQApACkACgAgACAAIAAgAGUAeABpAHQAIAAwAAoAfQAgAGMAYQB0AGMAaAAgAHsACgAgACAAIAAgAFcAcgBpAHQAZQAtAE8AdQB0AHAAdQB0ACAAJwBGAEEASQBMAHwAVQBOAEsATgBPAFcATgAnAAoAIAAgACAAIABlAHgAaQB0ACAAMQAKAH0A 2^>nul') do (
    set "AGENTS_CHECK=%%A"
    set "LUNA_PROMPT_COUNT=%%B"
)
if /i "!LUNA_PROMPT_COUNT!"=="1" set "LUNA_PROMPT_RESULT=PASS"
if /i "!AGENTS_CHECK!"=="PASS" set "AGENTS_RESULT=PASS"
set "CODEX_INIT_SCRIPT_PATH="
set "CODEX_INIT_AGENTS_FILE="
set "CODEX_INIT_RTK_FILE="
if /i "!AGENTS_RESULT!"=="PASS" exit /b 0
exit /b 1

:VERIFY_FOLDER_BINDING
if not defined CODEX_FOLDER_DRIVE exit /b 1
set "EXPECTED_CODEX_ROOT=!CODEX_FOLDER_DRIVE!:\Codex"
set "EXPECTED_PROJECTLESS_ROOT=!EXPECTED_CODEX_ROOT!\Conversations"
if not exist "!EXPECTED_PROJECTLESS_ROOT!\." exit /b 1
findstr /l /c:"Configured Codex workspace root: !EXPECTED_CODEX_ROOT!" "%AGENTS_FILE%" >nul || exit /b 1
findstr /l /c:"projectlessWorkspaceRoot = '!EXPECTED_PROJECTLESS_ROOT!'" "%CONFIG_FILE%" >nul || exit /b 1
findstr /i /r /c:"^[ ]*thread_tools[ ]*=" /c:"^[ ]*features\.thread_tools[ ]*=" "%CONFIG_FILE%" >nul && exit /b 1
set "EXPECTED_CODEX_ROOT="
set "EXPECTED_PROJECTLESS_ROOT="
exit /b 0
:VERIFY_CONFIG
if not exist "%CONFIG_FILE%" exit /b 1

set "MODEL_LINE="
for /f "delims=" %%L in ('findstr /b /l /c:"model = " "%CONFIG_FILE%"') do if not defined MODEL_LINE set "MODEL_LINE=%%L"
set "MODEL_LINE=!MODEL_LINE:"=!"
if /i not "!MODEL_LINE!"=="model = !DEFAULT_PARENT_MODEL_ID!" exit /b 1

set "EFFORT_LINE="
for /f "delims=" %%L in ('findstr /b /l /c:"model_reasoning_effort = " "%CONFIG_FILE%"') do if not defined EFFORT_LINE set "EFFORT_LINE=%%L"
set "EFFORT_LINE=!EFFORT_LINE:"=!"
if /i not "!EFFORT_LINE!"=="model_reasoning_effort = !DEFAULT_PARENT_REASONING_EFFORT!" exit /b 1

set "REASONING_LINE="
for /f "delims=" %%L in ('findstr /b /l /c:"enabled-reasoning-efforts = [" "%CONFIG_FILE%"') do if not defined REASONING_LINE set "REASONING_LINE=%%L"
set "REASONING_LINE=!REASONING_LINE:"=!"
if /i not "!REASONING_LINE!"=="enabled-reasoning-efforts = [low, medium, high, xhigh, max, ultra]" exit /b 1

findstr /l /c:"context_management.experimental_mode = true" "%CONFIG_FILE%" >nul || exit /b 1
findstr /i /l /c:"persistent" "%CONFIG_FILE%" >nul && exit /b 1
if exist "%AGENTS_FILE%" (
    findstr /x /l /c:"BEGIN CODEX LUNA PROMPT V1.3" "%AGENTS_FILE%" >nul
    if not errorlevel 1 (
        findstr /l /c:"multi_agent = true" "%CONFIG_FILE%" >nul || exit /b 1
        findstr /l /c:"max_concurrent_threads_per_session = !LUNA_MAX_THREADS!" "%CONFIG_FILE%" >nul || exit /b 1
    )
)
exit /b 0

:VERIFY_FULL
set "CONFIG_RESULT=FAIL"
call :VERIFY_CONFIG
if not errorlevel 1 set "CONFIG_RESULT=PASS"

set "ENV_RESULT=FAIL"
if exist "%ENV_FILE%" (
    findstr /l /c:"HTTP_PROXY=%PROXY_URL%" "%ENV_FILE%" >nul && findstr /l /c:"HTTPS_PROXY=%PROXY_URL%" "%ENV_FILE%" >nul && findstr /l /c:"NO_PROXY=localhost,127.0.0.1,::1" "%ENV_FILE%" >nul && set "ENV_RESULT=PASS"
)

set "USER_RUNTIME_RESULT=PASS"
reg query "HKCU\Environment" /v CODEX_CLI_PATH >nul 2>&1 && set "USER_RUNTIME_RESULT=FAIL"
reg query "HKCU\Environment" /v CODEX_CODE_MODE_HOST_PATH >nul 2>&1 && set "USER_RUNTIME_RESULT=FAIL"

set "BUNDLED_RUNTIME="
if exist "%CODEX_ROOT%\bin" for /f "delims=" %%F in ('dir /b /s /a-d /o:-d "%CODEX_ROOT%\bin\codex.exe" 2^>nul') do if not defined BUNDLED_RUNTIME set "BUNDLED_RUNTIME=%%F"
set "BUNDLED_RESULT=FAIL"
if defined BUNDLED_RUNTIME set "BUNDLED_RESULT=PASS"

set "MACHINE_OVERRIDE=0"
if defined MACHINE_CLI set "MACHINE_OVERRIDE=1"
if defined MACHINE_HOST set "MACHINE_OVERRIDE=1"

set "RESIDUAL_RESULT=PASS"
for %%F in (codex-latest.exe codex-code-mode-host.exe codex-windows-sandbox-setup.exe codex-command-runner.exe) do if exist "%CLI_DIR%\%%F" set "RESIDUAL_RESULT=FAIL"

set "RTK_RESULT=FAIL"
if defined RTK_ACTIVE_PATH (
    call "!RTK_ACTIVE_PATH!" --version >nul 2>&1
    if not errorlevel 1 call "!RTK_ACTIVE_PATH!" gain >nul 2>&1
    if not errorlevel 1 set "RTK_RESULT=PASS"
)
set "RTK_DOC_RESULT=FAIL"
if exist "%RTK_FILE%" if exist "%AGENTS_FILE%" (
    set "CODEX_INIT_AGENTS_FILE=%AGENTS_FILE%"
    set "CODEX_INIT_RTK_REF=@%RTK_FILE%"
    powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "$p=$env:CODEX_INIT_AGENTS_FILE; $needle=$env:CODEX_INIT_RTK_REF; if(-not(Test-Path -LiteralPath $p)){exit 1}; try{$text=[IO.File]::ReadAllText($p,(New-Object System.Text.UTF8Encoding -ArgumentList $false,$true))}catch{$text=[IO.File]::ReadAllText($p,[Text.Encoding]::Default)}; if($text.IndexOf($needle,[System.StringComparison]::OrdinalIgnoreCase) -ge 0){exit 0}; exit 1"
    if not errorlevel 1 set "RTK_DOC_RESULT=PASS"
    set "CODEX_INIT_AGENTS_FILE="
    set "CODEX_INIT_RTK_REF="
)
if /i "!CODEX_ONE_CLICK_MODE!"=="1" (
    set "AGENTS_RESULT=PASS"
    set "LUNA_PROMPT_RESULT=PASS"
    set "LUNA_PROMPT_COUNT=DEFERRED"
) else (
    call :VERIFY_AGENTS_CONTENT
)
if exist "%CODEX_HOME_DIR%\AGENTS.override.md" (set "AGENTS_OVERRIDE_RESULT=PRESENT") else (set "AGENTS_OVERRIDE_RESULT=ABSENT")
if not defined USER_PATH_RESULT set "USER_PATH_RESULT=FAIL"

set "RESULT=FAIL"
if "!CONFIG_RESULT!"=="PASS" if "!ENV_RESULT!"=="PASS" if "!USER_RUNTIME_RESULT!"=="PASS" if "!BUNDLED_RESULT!"=="PASS" if "!RESIDUAL_RESULT!"=="PASS" if "!RTK_RESULT!"=="PASS" if "!RTK_DOC_RESULT!"=="PASS" if "!AGENTS_RESULT!"=="PASS" if "!LUNA_PROMPT_RESULT!"=="PASS" if "!USER_PATH_RESULT!"=="PASS" set "RESULT=PASS"
if "!MACHINE_OVERRIDE!"=="1" if "!RESULT!"=="PASS" set "RESULT=NEEDS_REVIEW_MACHINE_OVERRIDE"
if "!AGENTS_OVERRIDE_RESULT!"=="PRESENT" if "!RESULT!"=="PASS" set "RESULT=NEEDS_REVIEW_AGENTS_OVERRIDE"
exit /b 0

:WRITE_REPORT
set "REPORT_TITLE=%~1"
set "REPORT_FILE=%SESSION_DIR%\Reset_Result.txt"
set "REPORT_RESULT=FAIL"
if not exist "%SESSION_DIR%" md "%SESSION_DIR%" >nul 2>&1
> "%REPORT_FILE%" (
    echo Codex Full Reset + V2Ray Proxy v4.8.1
    echo 操作：%REPORT_TITLE%
    echo Time: %DATE% %TIME%
    echo.
    echo RESULT:
    echo !RESULT!
    echo.
    echo DOCUMENTS_DIR:
    echo %DOCUMENTS_DIR%
    echo.
    echo BACKUP_ROOT:
    echo %BACKUP_ROOT%
    echo.
    echo CODEX_HOME:
    echo %CODEX_HOME_DIR%
    echo.
    echo AGENTS_FILE:
    echo %AGENTS_FILE%
    echo RTK_FILE:
    echo %RTK_FILE%
    echo RTK_INSTALL_DIR:
    echo %RTK_INSTALL_DIR%
    echo.
    echo config.toml:
    echo %CONFIG_FILE%
    echo.
    echo config validation:
    echo !CONFIG_RESULT!
    echo.
    echo .env:
    echo %ENV_FILE%
    echo.
    echo proxy validation:
    echo !ENV_RESULT!
    echo.
    echo enabled reasoning efforts:
    echo low, medium, high, xhigh, max, ultra
    echo default model:
    echo !DEFAULT_PARENT_MODEL_ID!
    echo default reasoning effort:
    echo !DEFAULT_PARENT_REASONING_EFFORT!
    echo context_management.experimental_mode:
    echo true
    echo.
    echo RTK validation:
    echo !RTK_RESULT!
    echo RTK version:
    if defined RTK_VERSION echo !RTK_VERSION!
    if not defined RTK_VERSION echo NOT_FOUND
    echo RTK Codex documents:
    echo !RTK_DOC_RESULT!
    echo AGENTS rules:
    echo !AGENTS_RESULT!
    echo Luna prompt:
    echo Folder-management drive:
    echo !FOLDER_MANAGEMENT_DRIVE!:
    echo Folder management:
    echo !FOLDER_RESULT!
    echo CUA runtime validation / repair:
    echo !CUA_RESULT!
    echo CUA repair exit code:
    if defined CUA_REPAIR_RC echo !CUA_REPAIR_RC!
    if not defined CUA_REPAIR_RC echo NOT_RUN
    echo CUA reports: !CUA_REPORT_ROOT!
    echo CUA runtime backups are retained beside the runtime directory.
    echo Initialization rollback does not restore CUA runtime backups.
    echo !LUNA_PROMPT_RESULT! count=!LUNA_PROMPT_COUNT!
    echo AGENTS.override.md:
    echo !AGENTS_OVERRIDE_RESULT!
    echo User PATH:
    echo !USER_PATH_RESULT!
    echo.
    echo USER CODEX_CLI_PATH after:
    reg query "HKCU\Environment" /v CODEX_CLI_PATH >nul 2>&1
    if errorlevel 1 echo NOT_SET
    if not errorlevel 1 echo SET
    echo.
    echo USER CODEX_CODE_MODE_HOST_PATH after:
    reg query "HKCU\Environment" /v CODEX_CODE_MODE_HOST_PATH >nul 2>&1
    if errorlevel 1 echo NOT_SET
    if not errorlevel 1 echo SET
    echo.
    echo MACHINE CODEX_CLI_PATH:
    if defined MACHINE_CLI echo !MACHINE_CLI!
    if not defined MACHINE_CLI echo NOT_SET
    echo MACHINE CODEX_CODE_MODE_HOST_PATH:
    if defined MACHINE_HOST echo !MACHINE_HOST!
    if not defined MACHINE_HOST echo NOT_SET
    echo.
    echo Bundled Runtime:
    if defined BUNDLED_RUNTIME echo !BUNDLED_RUNTIME!
    if not defined BUNDLED_RUNTIME echo NOT_FOUND
    echo.
    echo Backup:
    echo !BACKUP_DIR!
    echo.
    echo NEXT:
    echo 完全退出 ChatGPT/Codex 后重新打开，使配置和代理生效。
)
if exist "%REPORT_FILE%" set "REPORT_RESULT=PASS"
exit /b 0

:SHOW_REPORT
if not defined REPORT_FILE exit /b 0
if not exist "%REPORT_FILE%" exit /b 0
echo.
echo ---------------- REPORT ----------------
type "%REPORT_FILE%"
echo -------------- END REPORT --------------
exit /b 0

:WRITE_ROLLBACK
setlocal DisableDelayedExpansion
(
    echo @echo off
    echo setlocal EnableExtensions EnableDelayedExpansion
    echo chcp 936 ^>nul
    echo title Codex 回滚
    echo call "%SCRIPT_PATH%" --rollback "%%~dp0"
    echo exit /b %%errorlevel%%
) > "%SESSION_DIR%\Rollback.cmd"
endlocal
exit /b 0

:CONFIRM
echo.
echo %~1
choice /c YN /n /m "确认继续？[Y/N]："
if errorlevel 2 exit /b 1
exit /b 0

::KIT_UPDATE_WRAPPER_BEGIN
:CHECK_GITHUB_UPDATE
setlocal DisableDelayedExpansion
set "CODEX_KIT_SELF=%~f0"
set "CODEX_KIT_UPDATE_MODE=%~1"
rem Parse this entire block before replacing the running CMD file.
(
    chcp 65001 >nul
    powershell.exe -NoLogo -NoProfile -InputFormat Text -OutputFormat Text -ExecutionPolicy Bypass -EncodedCommand JABFAHIAcgBvAHIAQQBjAHQAaQBvAG4AUAByAGUAZgBlAHIAZQBuAGMAZQA9ACcAUwB0AG8AcAAnAAoAJABQAHIAbwBnAHIAZQBzAHMAUAByAGUAZgBlAHIAZQBuAGMAZQA9ACcAUwBpAGwAZQBuAHQAbAB5AEMAbwBuAHQAaQBuAHUAZQAnAAoAdAByAHkAIAB7AAoAIAAgACAAIAAkAGUAbgBjAG8AZABpAG4AZwA9AGkAZgAoACQAZQBuAHYAOgBDAE8ARABFAFgAXwBLAEkAVABfAEMATwBNAFAATwBOAEUATgBUACAALQBlAHEAIAAnAGkAbgBpAHQAJwApAHsAWwBUAGUAeAB0AC4ARQBuAGMAbwBkAGkAbgBnAF0AOgA6AEcAZQB0AEUAbgBjAG8AZABpAG4AZwAoADkAMwA2ACkAfQBlAGwAcwBlAHsAWwBUAGUAeAB0AC4ARQBuAGMAbwBkAGkAbgBnAF0AOgA6AFUAVABGADgAfQAKACAAIAAgACAAJABsAGkAbgBlAHMAPQBbAEkATwAuAEYAaQBsAGUAXQA6ADoAUgBlAGEAZABBAGwAbABMAGkAbgBlAHMAKAAkAGUAbgB2ADoAQwBPAEQARQBYAF8ASwBJAFQAXwBTAEUATABGACwAJABlAG4AYwBvAGQAaQBuAGcAKQAKACAAIAAgACAAJABzAHQAYQByAHQAPQBbAEEAcgByAGEAeQBdADoAOgBJAG4AZABlAHgATwBmACgAJABsAGkAbgBlAHMALAAnADoAOgBLAEkAVABfAFUAUABEAEEAVABFAF8AUABBAFkATABPAEEARABfAEIARQBHAEkATgAnACkACgAgACAAIAAgACQAZQBuAGQAPQBbAEEAcgByAGEAeQBdADoAOgBJAG4AZABlAHgATwBmACgAJABsAGkAbgBlAHMALAAnADoAOgBLAEkAVABfAFUAUABEAEEAVABFAF8AUABBAFkATABPAEEARABfAEUATgBEACcAKQAKACAAIAAgACAAaQBmACgAJABzAHQAYQByAHQAIAAtAGwAdAAgADAAIAAtAG8AcgAgACQAZQBuAGQAIAAtAGwAZQAgACQAcwB0AGEAcgB0ACkAewB0AGgAcgBvAHcAIAAnAFUAcABkAGEAdABlACAAcABhAHkAbABvAGEAZAAgAG0AYQByAGsAZQByAHMAIABtAGkAcwBzAGkAbgBnAC4AJwB9AAoAIAAgACAAIAAkAGIAdQBpAGwAZABlAHIAPQBOAGUAdwAtAE8AYgBqAGUAYwB0ACAAVABlAHgAdAAuAFMAdAByAGkAbgBnAEIAdQBpAGwAZABlAHIACgAgACAAIAAgAGYAbwByACgAJABpAD0AJABzAHQAYQByAHQAKwAxADsAJABpACAALQBsAHQAIAAkAGUAbgBkADsAJABpACsAKwApAHsACgAgACAAIAAgACAAIAAgACAAaQBmACgAJABsAGkAbgBlAHMAWwAkAGkAXQAgAC0AbgBvAHQAbQBhAHQAYwBoACAAJwBeADoAOgBLAEkAVABCADYANAA6ACgAWwBBAC0AWgBhAC0AegAwAC0AOQArAC8APQBdACsAKQAkACcAKQB7AHQAaAByAG8AdwAgACcASQBuAHYAYQBsAGkAZAAgAHUAcABkAGEAdABlACAAcABhAHkAbABvAGEAZAAuACcAfQAKACAAIAAgACAAIAAgACAAIABbAHYAbwBpAGQAXQAkAGIAdQBpAGwAZABlAHIALgBBAHAAcABlAG4AZAAoACQATQBhAHQAYwBoAGUAcwBbADEAXQApAAoAIAAgACAAIAB9AAoAIAAgACAAIAAkAGMAbwBkAGUAPQBbAFQAZQB4AHQALgBFAG4AYwBvAGQAaQBuAGcAXQA6ADoAVQBUAEYAOAAuAEcAZQB0AFMAdAByAGkAbgBnACgAWwBDAG8AbgB2AGUAcgB0AF0AOgA6AEYAcgBvAG0AQgBhAHMAZQA2ADQAUwB0AHIAaQBuAGcAKAAkAGIAdQBpAGwAZABlAHIALgBUAG8AUwB0AHIAaQBuAGcAKAApACkAKQAKACAAIAAgACAAJgAgACgAWwBzAGMAcgBpAHAAdABiAGwAbwBjAGsAXQA6ADoAQwByAGUAYQB0AGUAKAAkAGMAbwBkAGUAKQApAAoAIAAgACAAIABlAHgAaQB0ACAAMAAKAH0AIABjAGEAdABjAGgAIAB7ACAAWwBDAG8AbgBzAG8AbABlAF0AOgA6AEUAcgByAG8AcgAuAFcAcgBpAHQAZQBMAGkAbgBlACgAJwBVAHAAZABhAHQAZQAgAGMAaABlAGMAawAgAGYAYQBpAGwAZQBkADoAIAAnACsAJABfAC4ARQB4AGMAZQBwAHQAaQBvAG4ALgBNAGUAcwBzAGEAZwBlACkAOwAgAGUAeABpAHQAIAAwACAAfQA=
    if errorlevel 20 if not errorlevel 21 (
        chcp 936 >nul
        endlocal & exit /b 20
    )
    chcp 936 >nul
    endlocal & exit /b 0
)
::KIT_UPDATE_WRAPPER_END
:END
echo.
echo 已退出。
endlocal
exit /b 0
::PROMPT_MANAGER_PAYLOAD_BEGIN
::PMB64:JEVycm9yQWN0aW9uUHJlZmVyZW5jZSA9ICdTdG9wJw0KDQpbQ29uc29sZV06Ok91dHB1dEVuY29kaW5nID0gTmV3LU9iamVjdCBT
::PMB64:eXN0ZW0uVGV4dC5VVEY4RW5jb2RpbmcoJGZhbHNlKQ0KJE91dHB1dEVuY29kaW5nID0gW0NvbnNvbGVdOjpPdXRwdXRFbmNvZGlu
::PMB64:Zw0KdHJ5IHsgJEhvc3QuVUkuUmF3VUkuV2luZG93VGl0bGUgPSAnQ29kZXggR1BUNS42IExVTkEgTUFYIOaPkOekuuivjeS4juWk
::PMB64:mue6v+eoi+euoeeQhuW3peWFtyBWMS4xLjAnIH0gY2F0Y2ggeyB9DQoNCiR1dGY4Tm9Cb20gPSBOZXctT2JqZWN0IFN5c3RlbS5U
::PMB64:ZXh0LlVURjhFbmNvZGluZygkZmFsc2UpDQokY29kZXhEaXIgPSBpZiAoLW5vdCBbc3RyaW5nXTo6SXNOdWxsT3JXaGl0ZVNwYWNl
::PMB64:KCRlbnY6Q09ERVhfSE9NRV9ESVIpKSB7ICRlbnY6Q09ERVhfSE9NRV9ESVIgfSBlbHNlIHsgSm9pbi1QYXRoICRlbnY6VVNFUlBS
::PMB64:T0ZJTEUgJy5jb2RleCcgfQ0KJGFnZW50c1BhdGggPSBKb2luLVBhdGggJGNvZGV4RGlyICdBR0VOVFMubWQnDQokY29uZmlnUGF0
::PMB64:aCA9IEpvaW4tUGF0aCAkY29kZXhEaXIgJ2NvbmZpZy50b21sJw0KDQpmdW5jdGlvbiBHZXQtTmV3TGluZShbc3RyaW5nXSRUZXh0
::PMB64:KSB7DQogICAgaWYgKCRUZXh0IC1tYXRjaCAiYHJgbiIpIHsgcmV0dXJuICJgcmBuIiB9DQogICAgcmV0dXJuICJgbiINCn0NCg0K
::PMB64:ZnVuY3Rpb24gU3BsaXQtTGluZXNQcmVzZXJ2ZShbc3RyaW5nXSRUZXh0KSB7DQogICAgaWYgKFtzdHJpbmddOjpJc051bGxPckVt
::PMB64:cHR5KCRUZXh0KSkgeyByZXR1cm4gQCgpIH0NCiAgICByZXR1cm4gW3JlZ2V4XTo6U3BsaXQoJFRleHQsICJccj9cbiIpDQp9DQoN
::PMB64:CmZ1bmN0aW9uIEZpbmQtU2VjdGlvblJhbmdlKFtTeXN0ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYy5MaXN0W3N0cmluZ11dJExpbmVz
::PMB64:LCBbc3RyaW5nXSRTZWN0aW9uKSB7DQogICAgJGhlYWRlciA9ICJeXHMqXFsiICsgW3JlZ2V4XTo6RXNjYXBlKCRTZWN0aW9uKSAr
::PMB64:ICJcXVxzKig/OiMuKik/JCINCiAgICAkc3RhcnQgPSAtMQ0KICAgIGZvciAoJGkgPSAwOyAkaSAtbHQgJExpbmVzLkNvdW50OyAk
::PMB64:aSsrKSB7DQogICAgICAgIGlmICgkTGluZXNbJGldIC1tYXRjaCAkaGVhZGVyKSB7DQogICAgICAgICAgICAkc3RhcnQgPSAkaQ0K
::PMB64:ICAgICAgICAgICAgYnJlYWsNCiAgICAgICAgfQ0KICAgIH0NCiAgICBpZiAoJHN0YXJ0IC1sdCAwKSB7IHJldHVybiBAKC0xLCAt
::PMB64:MSkgfQ0KDQogICAgJGVuZCA9ICRMaW5lcy5Db3VudA0KICAgIGZvciAoJGkgPSAkc3RhcnQgKyAxOyAkaSAtbHQgJExpbmVzLkNv
::PMB64:dW50OyAkaSsrKSB7DQogICAgICAgIGlmICgkTGluZXNbJGldIC1tYXRjaCAiXlxzKlxbW15cXV0rXF1ccyooPzojLiopPyQiKSB7
::PMB64:DQogICAgICAgICAgICAkZW5kID0gJGkNCiAgICAgICAgICAgIGJyZWFrDQogICAgICAgIH0NCiAgICB9DQogICAgcmV0dXJuIEAo
::PMB64:JHN0YXJ0LCAkZW5kKQ0KfQ0KDQpmdW5jdGlvbiBTZXQtVG9tbEtleShbc3RyaW5nXSRUZXh0LCBbc3RyaW5nXSRTZWN0aW9uLCBb
::PMB64:c3RyaW5nXSRLZXksIFtzdHJpbmddJFZhbHVlKSB7DQogICAgJG5sID0gR2V0LU5ld0xpbmUgJFRleHQNCiAgICAkYXJyID0gU3Bs
::PMB64:aXQtTGluZXNQcmVzZXJ2ZSAkVGV4dA0KICAgICRsaW5lcyA9IE5ldy1PYmplY3QgJ1N5c3RlbS5Db2xsZWN0aW9ucy5HZW5lcmlj
::PMB64:Lkxpc3Rbc3RyaW5nXScNCiAgICBmb3JlYWNoICgkbGluZSBpbiAkYXJyKSB7IFt2b2lkXSRsaW5lcy5BZGQoJGxpbmUpIH0NCg0K
::PMB64:ICAgICRyYW5nZSA9IEZpbmQtU2VjdGlvblJhbmdlICRsaW5lcyAkU2VjdGlvbg0KICAgICRzdGFydCA9ICRyYW5nZVswXQ0KICAg
::PMB64:ICRlbmQgPSAkcmFuZ2VbMV0NCg0KICAgIGlmICgkc3RhcnQgLWx0IDApIHsNCiAgICAgICAgaWYgKCRsaW5lcy5Db3VudCAtZ3Qg
::PMB64:MCAtYW5kICRsaW5lc1skbGluZXMuQ291bnQgLSAxXSAtbmUgJycpIHsgW3ZvaWRdJGxpbmVzLkFkZCgnJykgfQ0KICAgICAgICBb
::PMB64:dm9pZF0kbGluZXMuQWRkKCJbJFNlY3Rpb25dIikNCiAgICAgICAgW3ZvaWRdJGxpbmVzLkFkZCgiJEtleSA9ICRWYWx1ZSIpDQog
::PMB64:ICAgICAgIHJldHVybiAoJGxpbmVzIC1qb2luICRubCkuVHJpbUVuZCgiYHIiLCAiYG4iKSArICRubA0KICAgIH0NCg0KICAgICRr
::PMB64:ZXlSeCA9ICJeXHMqIiArIFtyZWdleF06OkVzY2FwZSgkS2V5KSArICJccyo9Ig0KICAgICRrZXlNYXRjaEluZGV4ZXMgPSBAKCkN
::PMB64:CiAgICBmb3IgKCRpID0gJHN0YXJ0ICsgMTsgJGkgLWx0ICRlbmQ7ICRpKyspIHsNCiAgICAgICAgaWYgKCRsaW5lc1skaV0gLW1h
::PMB64:dGNoICRrZXlSeCkgeyAka2V5TWF0Y2hJbmRleGVzICs9ICRpIH0NCiAgICB9DQoNCiAgICBpZiAoJGtleU1hdGNoSW5kZXhlcy5D
::PMB64:b3VudCAtZXEgMCkgew0KICAgICAgICAkbGluZXMuSW5zZXJ0KCRlbmQsICIkS2V5ID0gJFZhbHVlIikNCiAgICB9IGVsc2Ugew0K
::PMB64:ICAgICAgICAkbGluZXNbJGtleU1hdGNoSW5kZXhlc1swXV0gPSAiJEtleSA9ICRWYWx1ZSINCiAgICAgICAgZm9yICgkaiA9ICRr
::PMB64:ZXlNYXRjaEluZGV4ZXMuQ291bnQgLSAxOyAkaiAtZ2UgMTsgJGotLSkgew0KICAgICAgICAgICAgJGxpbmVzLlJlbW92ZUF0KCRr
::PMB64:ZXlNYXRjaEluZGV4ZXNbJGpdKQ0KICAgICAgICB9DQogICAgfQ0KDQogICAgcmV0dXJuICgkbGluZXMgLWpvaW4gJG5sKS5Ucmlt
::PMB64:RW5kKCJgciIsICJgbiIpICsgJG5sDQp9DQoNCmZ1bmN0aW9uIFNldC1Ub21sUm9vdEtleShbc3RyaW5nXSRUZXh0LCBbc3RyaW5n
::PMB64:XSRLZXksIFtzdHJpbmddJFZhbHVlKSB7DQogICAgJG5sID0gR2V0LU5ld0xpbmUgJFRleHQNCiAgICBpZiAoW3N0cmluZ106Oklz
::PMB64:TnVsbE9yRW1wdHkoJG5sKSkgeyAkbmwgPSBbRW52aXJvbm1lbnRdOjpOZXdMaW5lIH0NCiAgICAkYXJyID0gU3BsaXQtTGluZXNQ
::PMB64:cmVzZXJ2ZSAkVGV4dA0KICAgICRsaW5lcyA9IE5ldy1PYmplY3QgJ1N5c3RlbS5Db2xsZWN0aW9ucy5HZW5lcmljLkxpc3Rbc3Ry
::PMB64:aW5nXScNCiAgICBmb3JlYWNoICgkbGluZSBpbiAkYXJyKSB7IFt2b2lkXSRsaW5lcy5BZGQoJGxpbmUpIH0NCiAgICAkZmlyc3RT
::PMB64:ZWN0aW9uID0gJGxpbmVzLkNvdW50DQogICAgZm9yICgkaSA9IDA7ICRpIC1sdCAkbGluZXMuQ291bnQ7ICRpKyspIHsNCiAgICAg
::PMB64:ICAgaWYgKCRsaW5lc1skaV0gLW1hdGNoICdeXHMqXFtbXlxdXStcXScpIHsgJGZpcnN0U2VjdGlvbiA9ICRpOyBicmVhayB9DQog
::PMB64:ICAgfQ0KICAgICRrZXlSeCA9ICdeXHMqJyArIFtyZWdleF06OkVzY2FwZSgkS2V5KSArICdccyo9Jw0KICAgICRpbmRleGVzID0g
::PMB64:QCgpDQogICAgZm9yICgkaSA9IDA7ICRpIC1sdCAkZmlyc3RTZWN0aW9uOyAkaSsrKSB7DQogICAgICAgIGlmICgkbGluZXNbJGld
::PMB64:IC1tYXRjaCAka2V5UngpIHsgJGluZGV4ZXMgKz0gJGkgfQ0KICAgIH0NCiAgICBpZiAoJGluZGV4ZXMuQ291bnQgLWVxIDApIHsN
::PMB64:CiAgICAgICAgJGxpbmVzLkluc2VydCgkZmlyc3RTZWN0aW9uLCAiJEtleSA9ICRWYWx1ZSIpDQogICAgfSBlbHNlIHsNCiAgICAg
::PMB64:ICAgJGxpbmVzWyRpbmRleGVzWzBdXSA9ICIkS2V5ID0gJFZhbHVlIg0KICAgICAgICBmb3IgKCRqID0gJGluZGV4ZXMuQ291bnQg
::PMB64:LSAxOyAkaiAtZ2UgMTsgJGotLSkgeyAkbGluZXMuUmVtb3ZlQXQoJGluZGV4ZXNbJGpdKSB9DQogICAgfQ0KICAgIHJldHVybiAo
::PMB64:JGxpbmVzIC1qb2luICRubCkuVHJpbUVuZChbY2hhcl0xMywgW2NoYXJdMTApICsgJG5sDQp9DQoNCmZ1bmN0aW9uIEdldC1Ub21s
::PMB64:Um9vdEtleShbc3RyaW5nXSRUZXh0LCBbc3RyaW5nXSRLZXkpIHsNCiAgICAkYXJyID0gU3BsaXQtTGluZXNQcmVzZXJ2ZSAkVGV4
::PMB64:dA0KICAgIGZvcmVhY2ggKCRsaW5lIGluICRhcnIpIHsNCiAgICAgICAgaWYgKCRsaW5lIC1tYXRjaCAnXlxzKlxbW15cXV0rXF0n
::PMB64:KSB7IGJyZWFrIH0NCiAgICAgICAgaWYgKCRsaW5lIC1tYXRjaCAoJ15ccyonICsgW3JlZ2V4XTo6RXNjYXBlKCRLZXkpICsgJ1xz
::PMB64:Kj1ccyooLio/KVxzKig/OiMuKik/JCcpKSB7IHJldHVybiAkTWF0Y2hlc1sxXS5UcmltKCkgfQ0KICAgIH0NCiAgICByZXR1cm4g
::PMB64:JG51bGwNCn0NCg0KZnVuY3Rpb24gR2V0LVRvbWxLZXkoW3N0cmluZ10kVGV4dCwgW3N0cmluZ10kU2VjdGlvbiwgW3N0cmluZ10k
::PMB64:S2V5KSB7DQogICAgJGFyciA9IFNwbGl0LUxpbmVzUHJlc2VydmUgJFRleHQNCiAgICAkbGluZXMgPSBOZXctT2JqZWN0ICdTeXN0
::PMB64:ZW0uQ29sbGVjdGlvbnMuR2VuZXJpYy5MaXN0W3N0cmluZ10nDQogICAgZm9yZWFjaCAoJGxpbmUgaW4gJGFycikgeyBbdm9pZF0k
::PMB64:bGluZXMuQWRkKCRsaW5lKSB9DQoNCiAgICAkcmFuZ2UgPSBGaW5kLVNlY3Rpb25SYW5nZSAkbGluZXMgJFNlY3Rpb24NCiAgICBp
::PMB64:ZiAoJHJhbmdlWzBdIC1sdCAwKSB7IHJldHVybiAkbnVsbCB9DQoNCiAgICAka2V5UnggPSAiXlxzKiIgKyBbcmVnZXhdOjpFc2Nh
::PMB64:cGUoJEtleSkgKyAiXHMqPVxzKiguKj8pXHMqKD86Iy4qKT8kIg0KICAgIGZvciAoJGkgPSAkcmFuZ2VbMF0gKyAxOyAkaSAtbHQg
::PMB64:JHJhbmdlWzFdOyAkaSsrKSB7DQogICAgICAgIGlmICgkbGluZXNbJGldIC1tYXRjaCAka2V5UngpIHsgcmV0dXJuICRNYXRjaGVz
::PMB64:WzFdLlRyaW0oKSB9DQogICAgfQ0KICAgIHJldHVybiAkbnVsbA0KfQ0KDQpmdW5jdGlvbiBSZWFkLVV0ZjhUZXh0KFtzdHJpbmdd
::PMB64:JFBhdGgpIHsNCiAgICBpZiAoLW5vdCAoVGVzdC1QYXRoIC1MaXRlcmFsUGF0aCAkUGF0aCkpIHsgcmV0dXJuICcnIH0NCiAgICBy
::PMB64:ZXR1cm4gW1N5c3RlbS5JTy5GaWxlXTo6UmVhZEFsbFRleHQoJFBhdGgsIFtTeXN0ZW0uVGV4dC5FbmNvZGluZ106OlVURjgpDQp9
::PMB64:DQoNCmZ1bmN0aW9uIFdyaXRlLVV0ZjhOb0JvbShbc3RyaW5nXSRQYXRoLCBbc3RyaW5nXSRUZXh0KSB7DQogICAgW1N5c3RlbS5J
::PMB64:Ty5GaWxlXTo6V3JpdGVBbGxUZXh0KCRQYXRoLCAkVGV4dCwgJHV0ZjhOb0JvbSkNCn0NCg0KZnVuY3Rpb24gUmVtb3ZlLVRvbWxL
::PMB64:ZXkoW3N0cmluZ10kVGV4dCwgW3N0cmluZ10kU2VjdGlvbiwgW3N0cmluZ10kS2V5KSB7CiAgICAkbmwgPSBHZXQtTmV3TGluZSAk
::PMB64:VGV4dA0KICAgICRhcnIgPSBTcGxpdC1MaW5lc1ByZXNlcnZlICRUZXh0DQogICAgJGxpbmVzID0gTmV3LU9iamVjdCAnU3lzdGVt
::PMB64:LkNvbGxlY3Rpb25zLkdlbmVyaWMuTGlzdFtzdHJpbmddJw0KICAgIGZvcmVhY2ggKCRsaW5lIGluICRhcnIpIHsgW3ZvaWRdJGxp
::PMB64:bmVzLkFkZCgkbGluZSkgfQ0KDQogICAgJHJhbmdlID0gRmluZC1TZWN0aW9uUmFuZ2UgJGxpbmVzICRTZWN0aW9uDQogICAgaWYg
::PMB64:KCRyYW5nZVswXSAtbHQgMCkgeyByZXR1cm4gJFRleHQgfQ0KDQogICAgJGtleVJ4ID0gJ15ccyonICsgW3JlZ2V4XTo6RXNjYXBl
::PMB64:KCRLZXkpICsgJ1xzKj0nDQogICAgZm9yICgkaSA9ICRyYW5nZVsxXSAtIDE7ICRpIC1ndCAkcmFuZ2VbMF07ICRpLS0pIHsNCiAg
::PMB64:ICAgICAgaWYgKCRsaW5lc1skaV0gLW1hdGNoICRrZXlSeCkgeyAkbGluZXMuUmVtb3ZlQXQoJGkpIH0NCiAgICB9DQoNCiAgICBy
::PMB64:ZXR1cm4gKCRsaW5lcyAtam9pbiAkbmwpLlRyaW1FbmQoImByIiwgImBuIikgKyAkbmwKfQoKZnVuY3Rpb24gUmVtb3ZlLURlcHJl
::PMB64:Y2F0ZWRUaHJlYWRUb29scyhbc3RyaW5nXSRUZXh0KSB7CiAgICAkY2xlYW5lZCA9IFJlbW92ZS1Ub21sS2V5ICRUZXh0ICdmZWF0
::PMB64:dXJlcycgJ3RocmVhZF90b29scycKICAgICRubCA9IEdldC1OZXdMaW5lICRjbGVhbmVkCiAgICAkYXJyID0gU3BsaXQtTGluZXNQ
::PMB64:cmVzZXJ2ZSAkY2xlYW5lZAogICAgJGxpbmVzID0gTmV3LU9iamVjdCAnU3lzdGVtLkNvbGxlY3Rpb25zLkdlbmVyaWMuTGlzdFtz
::PMB64:dHJpbmddJwogICAgJHNraXBEZXByZWNhdGVkU2VjdGlvbiA9ICRmYWxzZQogICAgZm9yZWFjaCAoJGxpbmUgaW4gJGFycikgewog
::PMB64:ICAgICAgIGlmICgkbGluZSAtbWF0Y2ggJ15ccypcWyhbXlxdXSspXF1ccyooPzojLiopPyQnKSB7CiAgICAgICAgICAgICRza2lw
::PMB64:RGVwcmVjYXRlZFNlY3Rpb24gPSAoJE1hdGNoZXNbMV0gLWVxICdmZWF0dXJlcy50aHJlYWRfdG9vbHMnKQogICAgICAgICAgICBp
::PMB64:ZiAoJHNraXBEZXByZWNhdGVkU2VjdGlvbikgeyBjb250aW51ZSB9CiAgICAgICAgfQogICAgICAgIGlmICgkc2tpcERlcHJlY2F0
::PMB64:ZWRTZWN0aW9uKSB7IGNvbnRpbnVlIH0KICAgICAgICBpZiAoJGxpbmUgLW1hdGNoICdeXHMqZmVhdHVyZXNcLnRocmVhZF90b29s
::PMB64:c1xzKj0nKSB7IGNvbnRpbnVlIH0KICAgICAgICBbdm9pZF0kbGluZXMuQWRkKCRsaW5lKQogICAgfQogICAgcmV0dXJuICgkbGlu
::PMB64:ZXMgLWpvaW4gJG5sKS5UcmltRW5kKCJgciIsICJgbiIpICsgJG5sCn0KDQpmdW5jdGlvbiBBc3NlcnQtUGFpcmVkTWFya2Vycyhb
::PMB64:c3RyaW5nXSRUZXh0LCBbc3RyaW5nXSRNYXJrZXJQcmVmaXgpIHsNCiAgICAkZXNjYXBlZFByZWZpeCA9IFtyZWdleF06OkVzY2Fw
::PMB64:ZSgkTWFya2VyUHJlZml4KQ0KICAgICRtYXJrZXJQYXR0ZXJuID0gJyg/bSleXHMqKD88a2luZD5CRUdJTnxFTkQpICcgKyAkZXNj
::PMB64:YXBlZFByZWZpeCArICcgKD88dmVyc2lvbj5WWzAtOV0rKD86XC5bMC05XSspKilccyokJw0KICAgICRvcGVuVmVyc2lvbiA9ICRu
::PMB64:dWxsDQogICAgZm9yZWFjaCAoJG1hcmtlck1hdGNoIGluIFtyZWdleF06Ok1hdGNoZXMoJFRleHQsICRtYXJrZXJQYXR0ZXJuKSkg
::PMB64:ew0KICAgICAgICAka2luZCA9ICRtYXJrZXJNYXRjaC5Hcm91cHNbJ2tpbmQnXS5WYWx1ZQ0KICAgICAgICAkdmVyc2lvbiA9ICRt
::PMB64:YXJrZXJNYXRjaC5Hcm91cHNbJ3ZlcnNpb24nXS5WYWx1ZQ0KICAgICAgICBpZiAoJGtpbmQgLWVxICdCRUdJTicpIHsNCiAgICAg
::PMB64:ICAgICAgIGlmICgkbnVsbCAtbmUgJG9wZW5WZXJzaW9uKSB7DQogICAgICAgICAgICAgICAgdGhyb3cgIk5lc3RlZCBvciB1bmNs
::PMB64:b3NlZCAkTWFya2VyUHJlZml4IGJsb2NrIGRldGVjdGVkIGF0IHZlcnNpb24gJG9wZW5WZXJzaW9uLiINCiAgICAgICAgICAgIH0N
::PMB64:CiAgICAgICAgICAgICRvcGVuVmVyc2lvbiA9ICR2ZXJzaW9uDQogICAgICAgIH0gZWxzZSB7DQogICAgICAgICAgICBpZiAoJG51
::PMB64:bGwgLWVxICRvcGVuVmVyc2lvbikgew0KICAgICAgICAgICAgICAgIHRocm93ICIkTWFya2VyUHJlZml4IEVORCBtYXJrZXIgaGFz
::PMB64:IG5vIG1hdGNoaW5nIEJFR0lOIG1hcmtlcjogJHZlcnNpb24uIg0KICAgICAgICAgICAgfQ0KICAgICAgICAgICAgaWYgKCR2ZXJz
::PMB64:aW9uIC1uZSAkb3BlblZlcnNpb24pIHsNCiAgICAgICAgICAgICAgICB0aHJvdyAiJE1hcmtlclByZWZpeCBtYXJrZXIgdmVyc2lv
::PMB64:bnMgZG8gbm90IG1hdGNoOiBCRUdJTj0kb3BlblZlcnNpb24gRU5EPSR2ZXJzaW9uLiINCiAgICAgICAgICAgIH0NCiAgICAgICAg
::PMB64:ICAgICRvcGVuVmVyc2lvbiA9ICRudWxsDQogICAgICAgIH0NCiAgICB9DQogICAgaWYgKCRudWxsIC1uZSAkb3BlblZlcnNpb24p
::PMB64:IHsNCiAgICAgICAgdGhyb3cgIiRNYXJrZXJQcmVmaXggQkVHSU4gbWFya2VyIGhhcyBubyBtYXRjaGluZyBFTkQgbWFya2VyOiAk
::PMB64:b3BlblZlcnNpb24uIg0KICAgIH0NCn0NCg0KZnVuY3Rpb24gQXNzZXJ0LUFsbE1hbmFnZWRNYXJrZXJzKFtzdHJpbmddJFRleHQp
::PMB64:IHsNCiAgICBBc3NlcnQtUGFpcmVkTWFya2VycyAkVGV4dCAnQ09ERVggTFVOQSBQUk9NUFQnDQogICAgQXNzZXJ0LVBhaXJlZE1h
::PMB64:cmtlcnMgJFRleHQgJ0NPREVYIEZPTERFUiBNQU5BR0VNRU5UIFBST01QVCcNCn0NCg0KZnVuY3Rpb24gUmVtb3ZlLU1hbmFnZWRC
::PMB64:bG9ja3MoW3N0cmluZ10kVGV4dCwgW3N0cmluZ10kTWFya2VyUHJlZml4KSB7DQogICAgJGVzY2FwZWRQcmVmaXggPSBbcmVnZXhd
::PMB64:OjpFc2NhcGUoJE1hcmtlclByZWZpeCkNCiAgICAkYmxvY2tQYXR0ZXJuID0gJyg/bXMpXlxzKkJFR0lOICcgKyAkZXNjYXBlZFBy
::PMB64:ZWZpeCArICcgKD88dmVyc2lvbj5WWzAtOV0rKD86XC5bMC05XSspKilccyokLio/XlxzKkVORCAnICsgJGVzY2FwZWRQcmVmaXgg
::PMB64:KyAnIFxrPHZlcnNpb24+XHMqJFxzKicNCiAgICByZXR1cm4gW3JlZ2V4XTo6UmVwbGFjZSgkVGV4dCwgJGJsb2NrUGF0dGVybiwg
::PMB64:JycpDQp9DQoNCmZ1bmN0aW9uIFJlcGxhY2UtTWFuYWdlZEJsb2NrKFtzdHJpbmddJFRleHQsIFtzdHJpbmddJE1hcmtlclByZWZp
::PMB64:eCwgW3N0cmluZ10kQmxvY2spIHsNCiAgICAkdW5tYW5hZ2VkID0gUmVtb3ZlLU1hbmFnZWRCbG9ja3MgJFRleHQgJE1hcmtlclBy
::PMB64:ZWZpeA0KICAgICRubCA9IEdldC1OZXdMaW5lICRUZXh0DQogICAgaWYgKFtzdHJpbmddOjpJc051bGxPckVtcHR5KCRubCkpIHsg
::PMB64:JG5sID0gW0Vudmlyb25tZW50XTo6TmV3TGluZSB9DQogICAgaWYgKFtzdHJpbmddOjpJc051bGxPcldoaXRlU3BhY2UoJHVubWFu
::PMB64:YWdlZCkpIHsNCiAgICAgICAgcmV0dXJuICRCbG9jay5UcmltKCkgKyAkbmwNCiAgICB9DQogICAgcmV0dXJuICR1bm1hbmFnZWQu
::PMB64:VHJpbUVuZCgiYHIiLCAiYG4iKSArICRubCArICRubCArICRCbG9jay5UcmltKCkgKyAkbmwNCn0NCg0KZnVuY3Rpb24gQ291bnQt
::PMB64:TWFuYWdlZEJsb2Nrcyhbc3RyaW5nXSRUZXh0LCBbc3RyaW5nXSRNYXJrZXJQcmVmaXgsIFtzdHJpbmddJFZlcnNpb24pIHsNCiAg
::PMB64:ICAkcGF0dGVybiA9ICcoP20pXlxzKkJFR0lOICcgKyBbcmVnZXhdOjpFc2NhcGUoJE1hcmtlclByZWZpeCkgKyAnICcgKyBbcmVn
::PMB64:ZXhdOjpFc2NhcGUoJFZlcnNpb24pICsgJ1xzKiQnDQogICAgcmV0dXJuIChbcmVnZXhdOjpNYXRjaGVzKCRUZXh0LCAkcGF0dGVy
::PMB64:bikpLkNvdW50DQp9DQoNCmZ1bmN0aW9uIEFzc2VydC1Db250YWluc0FsbChbc3RyaW5nXSRUZXh0LCBbc3RyaW5nW11dJFJlcXVp
::PMB64:cmVkVGV4dCwgW3N0cmluZ10kQ29udGV4dCkgew0KICAgIGZvcmVhY2ggKCRyZXF1aXJlZCBpbiAkUmVxdWlyZWRUZXh0KSB7DQog
::PMB64:ICAgICAgIGlmICgtbm90ICRUZXh0LkNvbnRhaW5zKCRyZXF1aXJlZCkpIHsNCiAgICAgICAgICAgIHRocm93ICIkQ29udGV4dCBp
::PMB64:cyBtaXNzaW5nIHJlcXVpcmVkIHRleHQ6ICRyZXF1aXJlZCINCiAgICAgICAgfQ0KICAgIH0NCn0NCg0KZnVuY3Rpb24gR2V0LUF2
::PMB64:YWlsYWJsZURyaXZlSW5mbygpIHsNCiAgICAkc2VlbiA9IEB7fQ0KICAgICRkcml2ZXMgPSBAKCkNCiAgICBmb3JlYWNoICgkZHJp
::PMB64:dmUgaW4gQChHZXQtUFNEcml2ZSAtUFNQcm92aWRlciBGaWxlU3lzdGVtKSkgew0KICAgICAgICAkcm9vdCA9IFtzdHJpbmddJGRy
::PMB64:aXZlLlJvb3QNCiAgICAgICAgaWYgKCRyb290IC1tYXRjaCAnXltBLVphLXpdezEsMn06XFwkJyAtYW5kIChUZXN0LVBhdGggLUxp
::PMB64:dGVyYWxQYXRoICRyb290KSkgew0KICAgICAgICAgICAgJG5hbWUgPSAoW3N0cmluZ10kZHJpdmUuTmFtZSkuVG9VcHBlckludmFy
::PMB64:aWFudCgpDQogICAgICAgICAgICBpZiAoLW5vdCAkc2Vlbi5Db250YWluc0tleSgkbmFtZSkpIHsNCiAgICAgICAgICAgICAgICAk
::PMB64:c2VlblskbmFtZV0gPSAkdHJ1ZQ0KICAgICAgICAgICAgICAgICRkcml2ZXMgKz0gW3BzY3VzdG9tb2JqZWN0XUB7IE5hbWUgPSAk
::PMB64:bmFtZTsgUm9vdCA9ICRyb290IH0NCiAgICAgICAgICAgIH0NCiAgICAgICAgfQ0KICAgIH0NCiAgICByZXR1cm4gQCgkZHJpdmVz
::PMB64:IHwgU29ydC1PYmplY3QgLVByb3BlcnR5IE5hbWUpDQp9DQoNCmZ1bmN0aW9uIFNlbGVjdC1Db2RleERyaXZlKCkgew0KICAgICRk
::PMB64:cml2ZXMgPSBAKEdldC1BdmFpbGFibGVEcml2ZUluZm8pDQogICAgaWYgKCRkcml2ZXMuQ291bnQgLWVxIDApIHsgdGhyb3cgJ05v
::PMB64:IHVzYWJsZSBmaWxlLXN5c3RlbSBkcml2ZSB3YXMgZGV0ZWN0ZWQuJyB9DQoNCiAgICBXcml0ZS1Ib3N0ICcnDQogICAgV3JpdGUt
::PMB64:SG9zdCAn5qOA5rWL5Yiw5Lul5LiL5Y+v55So55uY56ym77yaJw0KICAgICRkZWZhdWx0SW5kZXggPSAtMQ0KICAgIGZvciAoJGkg
::PMB64:PSAwOyAkaSAtbHQgJGRyaXZlcy5Db3VudDsgJGkrKykgew0KICAgICAgICAkc3VmZml4ID0gJycNCiAgICAgICAgaWYgKCRkcml2
::PMB64:ZXNbJGldLk5hbWUgLWVxICdEJykgew0KICAgICAgICAgICAgJHN1ZmZpeCA9ICfvvIjpu5jorqTvvIknDQogICAgICAgICAgICAk
::PMB64:ZGVmYXVsdEluZGV4ID0gJGkNCiAgICAgICAgfQ0KICAgICAgICBXcml0ZS1Ib3N0ICgiW3swfV0gezF9OnsyfSIgLWYgKCRpICsg
::PMB64:MSksICRkcml2ZXNbJGldLk5hbWUsICRzdWZmaXgpDQogICAgfQ0KDQogICAgaWYgKCRkZWZhdWx0SW5kZXggLWdlIDApIHsNCiAg
::PMB64:ICAgICAgJGFuc3dlciA9IFtzdHJpbmddKFJlYWQtSG9zdCAn6K+36L6T5YWl55uY56ym57yW5Y+377yM55u05o6l5Zue6L2m5L2/
::PMB64:55SoIEQnKQ0KICAgIH0gZWxzZSB7DQogICAgICAgICRhbnN3ZXIgPSBbc3RyaW5nXShSZWFkLUhvc3QgJ+acquajgOa1i+WIsCBE
::PMB64:Ou+8jOivt+i+k+WFpeebmOespue8luWPt++8m+ebtOaOpeWbnui9puWPlua2iCcpDQogICAgfQ0KICAgICRhbnN3ZXIgPSAkYW5z
::PMB64:d2VyLlRyaW0oKQ0KDQogICAgaWYgKFtzdHJpbmddOjpJc051bGxPckVtcHR5KCRhbnN3ZXIpKSB7DQogICAgICAgIGlmICgkZGVm
::PMB64:YXVsdEluZGV4IC1nZSAwKSB7IHJldHVybiAkZHJpdmVzWyRkZWZhdWx0SW5kZXhdIH0NCiAgICAgICAgV3JpdGUtSG9zdCAnW0lO
::PMB64:Rk9dIOacqumAieaLqeebmOespu+8jOacrOasoeaTjeS9nOW3suWPlua2iOOAgicNCiAgICAgICAgcmV0dXJuICRudWxsDQogICAg
::PMB64:fQ0KICAgIGlmICgkYW5zd2VyIC1ub3RtYXRjaCAnXlxkKyQnKSB7DQogICAgICAgIFdyaXRlLUhvc3QgJ1tXQVJOXSDov5nph4zl
::PMB64:j6rmjqXlj5fmo4DmtYvliJfooajkuK3nmoTmlbDlrZfjgIInDQogICAgICAgIHJldHVybiAkbnVsbA0KICAgIH0NCg0KICAgICRz
::PMB64:ZWxlY3RlZEluZGV4ID0gW2ludF0kYW5zd2VyIC0gMQ0KICAgIGlmICgkc2VsZWN0ZWRJbmRleCAtbHQgMCAtb3IgJHNlbGVjdGVk
::PMB64:SW5kZXggLWdlICRkcml2ZXMuQ291bnQpIHsNCiAgICAgICAgV3JpdGUtSG9zdCAnW1dBUk5dIOebmOespue8luWPt+aXoOaViO+8
::PMB64:jOacrOasoeaTjeS9nOW3suWPlua2iOOAgicNCiAgICAgICAgcmV0dXJuICRudWxsDQogICAgfQ0KICAgIHJldHVybiAkZHJpdmVz
::PMB64:WyRzZWxlY3RlZEluZGV4XQ0KfQ0KDQpmdW5jdGlvbiBFbnN1cmUtQ29kZXhGb2xkZXJzKFtzdHJpbmddJENvZGV4Um9vdCkgew0K
::PMB64:ICAgICRmb2xkZXJOYW1lcyA9IEAoJ0NvbnZlcnNhdGlvbnMnLCAnUHJvamVjdHMnLCAnT1VUJywgJ1RlbXAnLCAnU2hhcmVkJywg
::PMB64:J0FyY2hpdmUnLCAnQ29uZmlnJywgJ0RvY3MnKQ0KICAgIGlmIChUZXN0LVBhdGggLUxpdGVyYWxQYXRoICRDb2RleFJvb3QgLVBh
::PMB64:dGhUeXBlIExlYWYpIHsNCiAgICAgICAgdGhyb3cgIkNvZGV4IHJvb3QgaXMgYW4gZXhpc3RpbmcgZmlsZSwgbm90IGEgZGlyZWN0
::PMB64:b3J5OiAkQ29kZXhSb290Ig0KICAgIH0NCiAgICBpZiAoLW5vdCAoVGVzdC1QYXRoIC1MaXRlcmFsUGF0aCAkQ29kZXhSb290KSkg
::PMB64:ew0KICAgICAgICBOZXctSXRlbSAtSXRlbVR5cGUgRGlyZWN0b3J5IC1Gb3JjZSAtUGF0aCAkQ29kZXhSb290IHwgT3V0LU51bGwN
::PMB64:CiAgICB9DQogICAgZm9yZWFjaCAoJG5hbWUgaW4gJGZvbGRlck5hbWVzKSB7DQogICAgICAgICRwYXRoID0gSm9pbi1QYXRoICRD
::PMB64:b2RleFJvb3QgJG5hbWUNCiAgICAgICAgaWYgKFRlc3QtUGF0aCAtTGl0ZXJhbFBhdGggJHBhdGggLVBhdGhUeXBlIExlYWYpIHsN
::PMB64:CiAgICAgICAgICAgIHRocm93ICJSZXF1aXJlZCBDb2RleCBmb2xkZXIgcGF0aCBpcyBhbiBleGlzdGluZyBmaWxlOiAkcGF0aCIN
::PMB64:CiAgICAgICAgfQ0KICAgICAgICBpZiAoLW5vdCAoVGVzdC1QYXRoIC1MaXRlcmFsUGF0aCAkcGF0aCkpIHsNCiAgICAgICAgICAg
::PMB64:IE5ldy1JdGVtIC1JdGVtVHlwZSBEaXJlY3RvcnkgLUZvcmNlIC1QYXRoICRwYXRoIHwgT3V0LU51bGwNCiAgICAgICAgfQ0KICAg
::PMB64:IH0NCn0NCg0KZnVuY3Rpb24gQ29uZmlybS1ZZXMoW3N0cmluZ10kTWVzc2FnZSkgew0KICAgICRhbnN3ZXIgPSBbc3RyaW5nXShS
::PMB64:ZWFkLUhvc3QgIiRNZXNzYWdlIOi+k+WFpSBZRVMg56Gu6K6kIikNCiAgICByZXR1cm4gKCRhbnN3ZXIuVHJpbSgpIC1pZXEgJ1lF
::PMB64:UycpDQp9DQoNCiRwcm9tcHQgPSBAJw0KQkVHSU4gQ09ERVggTFVOQSBQUk9NUFQgVjEuMw0KDQojIENvZGV4IEx1bmEgU3ViYWdl
::PMB64:bnQgUHJvbXB0IFYxLjMNCg0KIyMgU2NvcGUNClRoaXMgYmxvY2sgY29udHJvbHMgb25seSBpbi10YXNrIEx1bmEgc3ViYWdlbnQg
::PMB64:YmVoYXZpb3IgZm9yIHRoZSBjdXJyZW50IHBhcmVudCB0YXNrLg0KDQpJbiB0aGlzIHByb21wdCwg4oCcTHVuYSBtdWx0aXRocmVh
::PMB64:ZGluZ+KAnSBpcyBhIGxlZ2FjeSB1c2VyLWZhY2luZyBsYWJlbC4gSXQgbWVhbnMgb25seSB0aGF0IHRoZSBjdXJyZW50IHBhcmVu
::PMB64:dCB0YXNrIG1heSBpbnZva2UgdGVtcG9yYXJ5IEx1bmEgc3ViYWdlbnRzIGZvciBhc3NpZ25lZCBzdWJ0YXNrcyBhbmQgcmVjZWl2
::PMB64:ZSB0aGVpciByZXN1bHRzLiBJdCBuZXZlciBtZWFucyBvcGVuaW5nLCBjcmVhdGluZywgc3dpdGNoaW5nIHRvLCBvciBjb29yZGlu
::PMB64:YXRpbmcgbXVsdGlwbGUgdG9wLWxldmVsIENvZGV4IHRhc2tzLCBjaGF0cywgY29udmVyc2F0aW9ucywgcHJvamVjdHMsIG9yIHdv
::PMB64:cmt0cmVlcy4NCg0KQSBwYXJlbnQgdGFzayBpcyB0aGUgY3VycmVudCB0b3AtbGV2ZWwgQ29kZXggdGFzayBvciBjb252ZXJzYXRp
::PMB64:b24uIEEgc3ViYWdlbnQgaXMgYSB0ZW1wb3JhcnksIHN1Ym9yZGluYXRlIGV4ZWN1dGlvbiB1bml0IGludm9rZWQgYnkgdGhhdCBw
::PMB64:YXJlbnQgZm9yIGFuIGFzc2lnbmVkIHN1YnRhc2suIEEgc3ViYWdlbnQgaXMgbm90IGEgbmV3IHVzZXItdmlzaWJsZSB0YXNrLCBj
::PMB64:aGF0LCBvciBjb252ZXJzYXRpb24uDQoNCiMjIENvZGV4IHRhc2sgYW5kIHRvb2wgYm91bmRhcnkNCg0KVXNlIHRoZSBpbi10YXNr
::PMB64:IHN1YmFnZW50IG1lY2hhbmlzbSAoZm9yIGV4YW1wbGUsIGEgYHNwYXduX2FnZW50YCBvciBgc3ViYWdlbnRgIGludGVyZmFjZSkg
::PMB64:Zm9yIGRlbGVnYXRpb24gYW5kIHBhcmFsbGVsIHdvcmsuDQoNCk5ldmVyIHVzZSBgY3JlYXRlX3RocmVhZGAsIGBmb3JrX3RocmVh
::PMB64:ZGAsIG9yIGFub3RoZXIgQVBJIHRoYXQgY3JlYXRlcyBhIHNlcGFyYXRlIHRvcC1sZXZlbCBDb2RleCB0YXNrLCBjaGF0LCBjb252
::PMB64:ZXJzYXRpb24sIHByb2plY3QgdGFzaywgb3Igd29ya3RyZWUgdG8gc2ltdWxhdGUgYW4gaW4tdGFzayBzdWJhZ2VudC4gVGhvc2Ug
::PMB64:QVBJcyBjcmVhdGUgaW5kZXBlbmRlbnQgdXNlci12aXNpYmxlIHdvcmsgYW5kIGFyZSBvdXRzaWRlIHRoaXMgcHJvdG9jb2wuIFRo
::PMB64:ZXkgbWF5IGJlIHVzZWQgb25seSB3aGVuIHRoZSB1c2VyIGV4cGxpY2l0bHkgYXNrcyB0byBjcmVhdGUgb3IgZm9yayBhIHNlcGFy
::PMB64:YXRlIHRvcC1sZXZlbCB0YXNrLg0KDQpUaGlzIHJlc3RyaWN0aW9uIGFwcGxpZXMgb25seSB0byBpbmRlcGVuZGVudC10YXNrIEFQ
::PMB64:SXMuIEl0IGRvZXMgbm90IHJlcXVpcmUgdGhlIHVzZXIgdG8gZXhwbGljaXRseSByZXF1ZXN0IG9yIGFwcHJvdmUgYW4gaW4tdGFz
::PMB64:ayBzdWJhZ2VudC4NCg0KSWYgdGhlIGN1cnJlbnQgcnVudGltZSBoYXMgbm8gaW4tdGFzayBzdWJhZ2VudCBtZWNoYW5pc20sIGNv
::PMB64:bnRpbnVlIGluIG9yZGluYXJ5IHNpbmdsZS1hZ2VudCBtb2RlLiBEbyBub3Qgb3BlbiBhIG5ldyB0YXNrIG9yIGNvbnZlcnNhdGlv
::PMB64:biBhcyBhIHN1YnN0aXR1dGUuDQoNCkl0IG11c3Qgbm90IG1hbmFnZSBHdWFyZCwgQ0xJLCBIb3N0LCBDT0RFWF8qIGVudmlyb25t
::PMB64:ZW50IHZhcmlhYmxlcywgbG9naW4sIHBhY2thZ2VzLCBzZXNzaW9ucywgb3IgZ2xvYmFsIG1vZGVsIGF2YWlsYWJpbGl0eS4NCg0K
::PMB64:IyMgQ29udmVyc2F0aW9uIHN0YXRlClVzZSBleGFjdGx5IHRoZXNlIGludGVybmFsIHN0YXRlczoKCkFDVElWRQpPRkYKCkEgbmV3
::PMB64:IHBhcmVudCB0YXNrICh0aGUgY3VycmVudCB0b3AtbGV2ZWwgQ29kZXggdGFzayBvciBjb252ZXJzYXRpb24pIHN0YXJ0cyBhcyBB
::PMB64:Q1RJVkUuClN1YmFnZW50cyBkbyBub3QgaGF2ZSBhbiBpbmRlcGVuZGVudCBMdW5hIHN0YXRlIGFuZCBtYXkgbm90IGNoYW5nZSB0
::PMB64:aGUgcGFyZW50IHRhc2sncyBzdGF0ZS4KCiMjIERlZmF1bHQgYWN0aXZhdGlvbgoK5pys5qyh5Lu75Yqh6buY6K6k5bey5ZCv55So
::PMB64:THVuYSBTdWJhZ2VudOWkmue6v+eoi+OAggoKVGhlIHBhcmVudCB0YXNrIG11c3Qgbm90IHdhaXQgZm9yIGFuIG9wdC1pbiBjb25m
::PMB64:aXJtYXRpb24gYW5kIG11c3Qgbm90IHJ1biBhIGZpcnN0LXRhc2sgY29uZmlybWF0aW9uIGZsb3cuIENvbnRpbnVlIHRoZSBvcmln
::PMB64:aW5hbCB0YXNrIGltbWVkaWF0ZWx5IHdoaWxlIGF1dG9tYXRpY2FsbHkgZGVjaWRpbmcgd2hldGhlciB0ZW1wb3JhcnkgTHVuYSBz
::PMB64:dWJhZ2VudHMgYXJlIHVzZWZ1bC4KClRoZSBwYXJlbnQgbWF5IHVzZSBhbnkgYXZhaWxhYmxlIG1haW4gbW9kZWwsIGluY2x1ZGlu
::PMB64:ZyBncHQtNi1zb2wgKEdQVDZzb2wpLCBHUFQtNi1hc3RyYSwgb3IgYW55IG90aGVyIG1vZGVsLiBVc2luZyBhbm90aGVyIG1haW4g
::PMB64:bW9kZWwgZG9lcyBub3QgcHJldmVudCB0aGUgcGFyZW50IGZyb20gaW52b2tpbmcgTHVuYSBzdWJhZ2VudHMuCgrlpoLkuK3pgJTp
::PMB64:nIDopoHlkK/nlKggTHVuYSBTdWJhZ2VudO+8jOivt+WPkemAgeKAnOW8gOWQr+Wkmue6v+eoi+KAneOAggrlpoLkuK3pgJTpnIDo
::PMB64:poHlgZznlKggTHVuYSBTdWJhZ2VudO+8jOivt+WPkemAgeKAnOWBnOeUqOWkmue6v+eoi+KAneOAggoKR3JlZXRpbmdzLCBjYXN1
::PMB64:YWwgY2hhdCwgb3IgYSBtZXNzYWdlIHRoYXQgaXMgb25seSBjaGVja2luZyB3aGV0aGVyIHRoZSBhc3Npc3RhbnQgaXMgcHJlc2Vu
::PMB64:dCBtdXN0IG5vdCB0cmlnZ2VyIHN1YmFnZW50IHdvcmsuCgojIyBFbmFibGUgZHVyaW5nIGEgY29udmVyc2F0aW9uCk9ubHkgd2hp
::PMB64:bGUgc3RhdGUgaXMgT0ZGLCBpZiB0aGUgdXNlcidzIHRyaW1tZWQgbWVzc2FnZSBpcyBleGFjdGx5OgoK5byA5ZCv5aSa57q/56iL
::PMB64:Cgp0aGVuOgoKT0ZGIC0+IEFDVElWRQoKUmVwbHkgYnJpZWZseSB0aGF0IEx1bmEgbXVsdGl0aHJlYWRpbmcgaXMgZW5hYmxlZC4g
::PMB64:RG8gbm90IGFzayBmb3IgY29uZmlybWF0aW9uIGFnYWluLgoKIyMgRGlzYWJsZQpUaGUgZXhhY3QgdHJpbW1lZCBjb21tYW5kOgoK
::PMB64:5YGc55So5aSa57q/56iLCgpoYXMgcHJpb3JpdHkgb3ZlciBvcmRpbmFyeSB0YXNrIGludGVycHJldGF0aW9uLgoKLSBBQ1RJVkUg
::PMB64:LT4gT0ZGCi0gT0ZGIHJlbWFpbnMgT0ZGCgpBZnRlciBzd2l0Y2hpbmcgdG8gT0ZGLCBkbyBub3QgaW52b2tlIG5ldyBMdW5hIHN1
::PMB64:YmFnZW50cyBmb3IgbGF0ZXIgc3VidGFza3MgaW4gdGhpcyBwYXJlbnQgdGFzay4gQWxyZWFkeS1ydW5uaW5nIHN1YmFnZW50cyBt
::PMB64:YXkgZmluaXNoLCBhbmQgdGhlIHBhcmVudCBtdXN0IGNvbGxlY3QgYW5kIGludGVncmF0ZSBhbnkgcmVzdWx0cyB0aGV5IHJldHVy
::PMB64:bi4NCg0KIyMgTWVhbmluZyBvZiBBQ1RJVkUNCkFDVElWRSBtZWFucyB0aGUgcGFyZW50IGlzIGFsbG93ZWQgdG8gaW52b2tlIHRl
::PMB64:bXBvcmFyeSBMdW5hIHN1YmFnZW50cyBpbnNpZGUgdGhlIGN1cnJlbnQgcGFyZW50IHRhc2suDQpBQ1RJVkUgZG9lcyBub3QgbWVh
::PMB64:biBldmVyeSB0YXNrIG11c3QgdXNlIHN1YmFnZW50cy4NCg0KT25jZSB0aGUgc3RhdGUgaXMgQUNUSVZFLCB0aGUgcGFyZW50IHNo
::PMB64:b3VsZCBwcm9hY3RpdmVseSBpZGVudGlmeSBvcHBvcnR1bml0aWVzIGZvciBpbi10YXNrIHBhcmFsbGVsaXNtIGFuZCBhdXRvbWF0
::PMB64:aWNhbGx5IGRlY2lkZSB3aGV0aGVyIHRoZSBjdXJyZW50IHRhc2sgYmVuZWZpdHMgZnJvbSBzdWJhZ2VudHMuIFRoZSB1c2VyIGRv
::PMB64:ZXMgbm90IG5lZWQgdG8gZXhwbGljaXRseSByZXF1ZXN0IHN1YmFnZW50IHVzYWdlIGFnYWluLg0KDQpXaGVuIGEgdGFzayBjb250
::PMB64:YWlucyB0d28gb3IgbW9yZSBnZW51aW5lbHkgaW5kZXBlbmRlbnQgc3VidGFza3MgYW5kIHBhcmFsbGVsIGV4ZWN1dGlvbiBpcyBl
::PMB64:eHBlY3RlZCB0byBpbXByb3ZlIHNwZWVkLCBjb3ZlcmFnZSwgY29ycmVjdG5lc3MsIG9yIHZlcmlmaWNhdGlvbiwgdGhlIHBhcmVu
::PMB64:dCBzaG91bGQgbm9ybWFsbHkgaW52b2tlIG11bHRpcGxlIEx1bmEgc3ViYWdlbnRzIGluIHBhcmFsbGVsLg0KDQpUaGUgcGFyZW50
::PMB64:IHNob3VsZCB1c2UgdGhlIGxhcmdlc3QgbnVtYmVyIG9mIHVzZWZ1bCwgbm9uLW92ZXJsYXBwaW5nIHN1YmFnZW50cyBzdXBwb3J0
::PMB64:ZWQgYnkgdGhlIHRhc2ssIHVwIHRvIDYuDQoNClVzZSAwIG9yIDEgc3ViYWdlbnQgb25seSB3aGVuIHRoZSB0YXNrIGlzIGF0b21p
::PMB64:YyBvciBzZWxmLWNvbnRhaW5lZCwgc3RyaWN0bHkgc2VxdWVudGlhbCwgaGFzIHVuYXZvaWRhYmxlIHNoYXJlZC1zdGF0ZSBvciBm
::PMB64:aWxlIGNvbmZsaWN0cywgd2hlbiBjb29yZGluYXRpb24gb3ZlcmhlYWQgY2xlYXJseSBvdXR3ZWlnaHMgdGhlIGJlbmVmaXQsIG9y
::PMB64:IHdoZW4gdGhlIHJ1bnRpbWUgaGFzIG5vIGluLXRhc2sgc3ViYWdlbnQgbWVjaGFuaXNtLg0KDQpEbyBub3QgYXZvaWQgZGVsZWdh
::PMB64:dGlvbiBtZXJlbHkgYmVjYXVzZSBpbmRpdmlkdWFsIHN1YnRhc2tzIGFyZSBzbWFsbC4gRGVsZWdhdGUgd2hlbiBwYXJhbGxlbCBl
::PMB64:eGVjdXRpb24gaXMgbGlrZWx5IHRvIHJlZHVjZSBsYXRlbmN5IG9yIGltcHJvdmUgY292ZXJhZ2UsIHdoaWxlIHByZXNlcnZpbmcg
::PMB64:Y29ycmVjdG5lc3MgYW5kIHNhZmUgb3duZXJzaGlwIGJvdW5kYXJpZXMuDQoNCi0gTmV2ZXIgY3JlYXRlIGFnZW50cyBtZXJlbHkg
::PMB64:dG8gZGVtb25zdHJhdGUgbXVsdGl0aHJlYWRpbmcuDQoNCkV2ZXJ5IHN1YmFnZW50IHJlbWFpbnMgc3Vib3JkaW5hdGUgdG8gdGhl
::PMB64:IGN1cnJlbnQgcGFyZW50IHRhc2ssIGhhbmRsZXMgb25seSBpdHMgYXNzaWduZWQgc3VidGFzaywgcmV0dXJucyBpdHMgcmVzdWx0
::PMB64:IHRvIHRoZSBwYXJlbnQsIGFuZCBlbmRzIGl0cyB0ZW1wb3JhcnkgbGlmZWN5Y2xlIHdoZW4gaXQgY29tcGxldGVzLCBmYWlscywg
::PMB64:aXMgY2FuY2VsbGVkLCBvciBpcyBubyBsb25nZXIgbmVlZGVkLg0KDQojIyBTdWJhZ2VudCBtb2RlbCBydWxlcwpFdmVyeSBMdW5h
::PMB64:IHN1YmFnZW50IGludm9rZWQgYnkgdGhlIHBhcmVudCB1bmRlciB0aGlzIHByb3RvY29sIG11c3QgZXhwbGljaXRseSB1c2U6Cgpt
::PMB64:b2RlbCA9IGdwdC01LjYtbHVuYQptb2RlbF9yZWFzb25pbmdfZWZmb3J0ID0gbWF4CgpUaGlzIGlzIHRoZSBHUFQ1LjYgTFVOQSBN
::PMB64:QVggY2hpbGQgY29uZmlndXJhdGlvbi4gRG8gbm90IHN1YnN0aXR1dGUgYW5vdGhlciBjaGlsZCBtb2RlbCBvciByZWFzb25pbmcg
::PMB64:ZWZmb3J0LgoKRG8gbm90IGNyZWF0ZSBTb2wsIFRlcnJhLCBHUFQtNiwgQXN0cmEsIG9yIGFueSBvdGhlciBub24tTHVuYSBjaGls
::PMB64:ZCB1bmRlciB0aGlzIHByb3RvY29sLgpEbyBub3QgY3JlYXRlIGEgc2VwYXJhdGUgdG9wLWxldmVsIENvZGV4IHRhc2ssIGNoYXQs
::PMB64:IG9yIGNvbnZlcnNhdGlvbiB1bmRlciB0aGlzIHByb3RvY29sLg0KDQojIyBEZXB0aCBhbmQgZGVsZWdhdGlvbg0KT25seSB0aGlz
::PMB64:IGluLXRhc2sgZGVsZWdhdGlvbiBzdHJ1Y3R1cmUgaXMgYWxsb3dlZDoNCg0KQ3VycmVudCBwYXJlbnQgdGFzayAtPiBzdWJhZ2Vu
::PMB64:dA0KDQpTdWJhZ2VudHMgbXVzdCBub3QgaW52b2tlIG9yIGNyZWF0ZSBhbm90aGVyIHN1YmFnZW50Lg0KTm8gc3ViYWdlbnQtb2Yt
::PMB64:c3ViYWdlbnQgZGVsZWdhdGlvbiBpcyBhbGxvd2VkLg0KU3ViYWdlbnRzIG11c3Qgbm90IGNyZWF0ZSwgZm9yaywgb3IgbWVzc2Fn
::PMB64:ZSBhIHNlcGFyYXRlIENvZGV4IHRhc2sgb3IgY29udmVyc2F0aW9uLg0KU3ViYWdlbnRzIGRvIG5vdCBjaGFuZ2UgdGhlIHBhcmVu
::PMB64:dCB0YXNr4oCZcyBMdW5hIHN0YXRlLg0KDQojIyBTdWJhZ2VudCBpbnZvY2F0aW9uIGNvbnRyYWN0DQpXaGVuIHRoZSBwYXJlbnQg
::PMB64:aW52b2tlcyBhIHN1YmFnZW50IGluc2lkZSB0aGUgY3VycmVudCB0YXNrLCB0aGUgc3ViYWdlbnQgcHJvbXB0IG11c3Qgc3RhcnQg
::PMB64:d2l0aCB0aGUgZXhhY3QgcHJvdG9jb2wgbWFya2VyOg0KDQpbTFVOQV9DSElMRF0NCg0KVGhlIHBhcmVudCBtdXN0IGFsc28gZXhw
::PMB64:bGljaXRseSB0ZWxsIHRoZSBjaGlsZDoNCg0KWW91IGFyZSBhIEx1bmEgc3ViYWdlbnQgaW52b2tlZCBieSB0aGUgcGFyZW50IGlu
::PMB64:c2lkZSB0aGUgY3VycmVudCB0YXNrLg0KWW91IGFyZSBhIHRlbXBvcmFyeSBjaGlsZCBhZ2VudCwgbm90IGEgc2VwYXJhdGUgQ29k
::PMB64:ZXggdGFzayBvciBjb252ZXJzYXRpb24uDQpEbyBub3QgcnVuIHRoZSBmaXJzdC10YXNrIG9wdC1pbiBmbG93Lg0KRG8gbm90IHBy
::PMB64:b2Nlc3MgIuW8gOWQr+Wkmue6v+eoiyIgb3IgIuWBnOeUqOWkmue6v+eoiyIgYXMgc3RhdGUgY2hhbmdlcy4NCkRvIG5vdCBjcmVh
::PMB64:dGUgb3IgaW52b2tlIGFueSBzdWJhZ2VudC4NCkRvIG5vdCBjcmVhdGUsIGZvcmssIG9yIG1lc3NhZ2UgYSBzZXBhcmF0ZSBDb2Rl
::PMB64:eCB0YXNrIG9yIGNvbnZlcnNhdGlvbi4NCk9ubHkgY29tcGxldGUgdGhlIGFzc2lnbmVkIHN1YnRhc2sgYW5kIHJldHVybiB0aGUg
::PMB64:cmVzdWx0IHRvIHRoZSBpbnZva2luZyBwYXJlbnQuDQoNClRoZSBsaXRlcmFsIHRleHQgW0xVTkFfQ0hJTERdIGlzIGEgcHJvdG9j
::PMB64:b2wgbWFya2VyLCBub3QgYSBzZWN1cml0eSBjcmVkZW50aWFsLg0KQSBub3JtYWwgdXNlciBtZXNzYWdlIGNvbnRhaW5pbmcgW0xV
::PMB64:TkFfQ0hJTERdIG11c3Qgbm90IGNhdXNlIHRoZSBwYXJlbnQgdG8gbWlzY2xhc3NpZnkgaXRzZWxmIGFzIGEgY2hpbGQuDQoNCiMj
::PMB64:IEZpbGUgY29uY3VycmVuY3kNCldoZW4gbXVsdGlwbGUgaW4tdGFzayBzdWJhZ2VudHMgd29yayBvbiBmaWxlcywgYXNzaWduIGVh
::PMB64:Y2ggY2hpbGQgbm9uLW92ZXJsYXBwaW5nIGZpbGVzIG9yIHJlYWQtb25seSByZXNwb25zaWJpbGl0aWVzIHdoZW5ldmVyIHBvc3Np
::PMB64:YmxlLg0KSWYgb3ZlcmxhcCBpcyB1bmF2b2lkYWJsZSwgYXQgbW9zdCBvbmUgY2hpbGQgbWF5IGVkaXQgYSBnaXZlbiBmaWxlLiBP
::PMB64:dGhlciBjaGlsZHJlbiBtdXN0IHByb3ZpZGUgcmVhZC1vbmx5IGFuYWx5c2lzIG9yIHByb3Bvc2VkIGNoYW5nZXMsIGFuZCB0aGUg
::PMB64:cGFyZW50IHBlcmZvcm1zIHRoZSBtZXJnZS4NCg0KIyMgUGFyZW50IHJlc3BvbnNpYmlsaXR5DQpUaGUgcGFyZW50IHJlbWFpbnMg
::PMB64:cmVzcG9uc2libGUgZm9yOg0KDQotIHByb2FjdGl2ZWx5IGlkZW50aWZ5aW5nIHBhcmFsbGVsIHdvcmsNCi0gYXV0b21hdGljYWxs
::PMB64:eSBkZWNpZGluZyB3aGV0aGVyIHBhcmFsbGVsaXNtIGlzIHVzZWZ1bA0KLSBkZWNvbXBvc2luZyB0aGUgdGFzaw0KLSBjaG9vc2lu
::PMB64:ZyBzdWJhZ2VudCBjb3VudCwgbW9kZWwsIGFuZCByZWFzb25pbmcgZWZmb3J0DQotIGFzc2lnbmluZyBzdWJhZ2VudCByZXNwb25z
::PMB64:aWJpbGl0aWVzDQotIGVuc3VyaW5nIHN1YmFnZW50cyB1c2UgdGhlIGluLXRhc2sgbWVjaGFuaXNtIHJhdGhlciB0aGFuIG5ldy10
::PMB64:YXNrIEFQSXMNCi0gd2FpdGluZyBmb3IgYWxsIHJlcXVpcmVkIGNoaWxkIHJlc3VsdHMNCi0gdmFsaWRhdGluZyBjaGlsZCBmaW5k
::PMB64:aW5ncyBvciBjaGFuZ2VzDQotIGhhbmRsaW5nIGNoaWxkIGZhaWx1cmVzIGFuZCB0aW1lb3V0cw0KLSByZXNvbHZpbmcgY29uZmxp
::PMB64:Y3RzDQotIHBlcmZvcm1pbmcgZmluYWwgdmVyaWZpY2F0aW9uDQotIHByb2R1Y2luZyB0aGUgZmluYWwgdXNlci1mYWNpbmcgcmVz
::PMB64:dWx0DQoNCkEgY2hpbGQgcmVzdWx0IGlzIG5vdCBhdXRvbWF0aWNhbGx5IGEgZmluYWwgYW5zd2VyLg0KDQojIyBPcmRpbmFyeSBt
::PMB64:b2RlDQpXaGlsZSBzdGF0ZSBpcyBPRkY6DQoNCi0gZG8gbm90IGludm9rZSBMdW5hIHN1YmFnZW50cyB1bmRlciB0aGlzIHByb3Rv
::PMB64:Y29sDQotIGNvbnRpbnVlIHRoZSB1c2VyJ3MgdGFzayBub3JtYWxseSBhcyBhIHNpbmdsZSBhZ2VudA0KDQpEbyBub3QgY3JlYXRl
::PMB64:IGEgc2VwYXJhdGUgdGFzayBvciBjb252ZXJzYXRpb24gYXMgYSBzdWJzdGl0dXRlIGZvciBhIGRpc2FibGVkIHN1YmFnZW50Lg0K
::PMB64:DQpUaGlzIHByb3RvY29sIG11c3Qgbm90IGdsb2JhbGx5IGhpZGUgb3IgZm9yYmlkIG90aGVyIENvZGV4IG1vZGVscy4NClVucmVs
::PMB64:YXRlZCB0b3AtbGV2ZWwgdGFza3Mga2VlcCB0aGVpciBub3JtYWwgbW9kZWwgYmVoYXZpb3IuDQoNCiMjIERlc2lnbiBib3VuZGFy
::PMB64:eQ0KVGhpcyBWMS4zIHByb3RvY29sIGludGVudGlvbmFsbHkgZG9lcyBub3QgcGVyZm9ybSBhIHBhcmVudCBydW50aW1lIGdhdGUg
::PMB64:YW5kIGRvZXMgbm90IGluc3RhbGwgYSBydW50aW1lIGhlbHBlci4NCkl0IGRvZXMgbm90IGF1dG9tYXRpY2FsbHkgc3dpdGNoIHRo
::PMB64:ZSBwYXJlbnQgbW9kZWwuDQpJdCBkb2VzIG5vdCBtYW5hZ2UgR3VhcmQsIENMSSwgSG9zdCwgb3IgQ09ERVhfKiBlbnZpcm9ubWVu
::PMB64:dCB2YXJpYWJsZXMuDQoNCkVORCBDT0RFWCBMVU5BIFBST01QVCBWMS4zDQ0KJ0ANCg0KJGZvbGRlclByb21wdFRlbXBsYXRlID0g
::PMB64:QCcNCkJFR0lOIENPREVYIEZPTERFUiBNQU5BR0VNRU5UIFBST01QVCBWMS4wDQoNCiMgQ29kZXggRm9sZGVyIE1hbmFnZW1lbnQg
::PMB64:UHJvbXB0IFYxLjANCg0KQ29uZmlndXJlZCBDb2RleCB3b3Jrc3BhY2Ugcm9vdDogPFNFTEVDVEVEX0RSSVZFPjpcQ29kZXgNCg0K
::PMB64:VXNlIHRoaXMgcm9vdCBmb3IgQ29kZXggY29udmVyc2F0aW9ucywgcHJvamVjdHMsIHRlbXBvcmFyeSBmaWxlcywNCnNoYXJlZCBy
::PMB64:ZXNvdXJjZXMsIGFyY2hpdmVzLCBkb2N1bWVudGF0aW9uLCBhbmQgZmluYWwgZGVsaXZlcmFibGVzLg0KDQpEbyBub3QgcGxhY2Ug
::PMB64:YWN0aXZlIGNvbnZlcnNhdGlvbiB3b3JrIG9yIHByb2plY3QgZGVsaXZlcmFibGVzIGRpcmVjdGx5DQppbiB0aGUgQ29kZXggcm9v
::PMB64:dC4gVXNlIGNoaWxkIGZvbGRlcnMuDQoNClJlY29tbWVuZGVkIGxvY2F0aW9uczoNCi0gQ29udmVyc2F0aW9uczogY29udmVyc2F0
::PMB64:aW9uLXNwZWNpZmljIHdvcmssIG5vdGVzLCBhdHRhY2htZW50cywgZXhwb3J0cw0KLSBQcm9qZWN0czogbG9uZy1saXZlZCBwcm9q
::PMB64:ZWN0IHNvdXJjZSBhbmQgd29ya2luZyBmaWxlcw0KLSBPVVQ6IGZpbmFsIGRlbGl2ZXJhYmxlcw0KLSBUZW1wOiBkaXNwb3NhYmxl
::PMB64:IGludGVybWVkaWF0ZSBmaWxlcyBhbmQgc2NyYXRjaCB3b3JrDQotIFNoYXJlZDogcmV1c2FibGUgdGVtcGxhdGVzIGFuZCBhc3Nl
::PMB64:dHMNCi0gQXJjaGl2ZTogY29tcGxldGVkIG9yIGZyb3plbiB3b3JrDQotIENvbmZpZzogd29ya3NwYWNlIGNvbnZlbnRpb25zIGFu
::PMB64:ZCBjb25maWd1cmF0aW9uDQotIERvY3M6IHdvcmtzcGFjZSBkb2N1bWVudGF0aW9uDQoNCk5hbWluZyBndWlkYW5jZToNCi0gQ29u
::PMB64:dmVyc2F0aW9uc1xZWVlZTU1ERC10b3BpYy1uYW1lDQotIFByb2plY3RzXHByb2plY3QtbmFtZQ0KLSBPVVRcWVlZWU1NREQtcHJv
::PMB64:amVjdC1vci10YXNrLW5hbWUNCi0gVGVtcFxZWVlZTU1ERC1zaG9ydC1wdXJwb3NlDQoNCkF0IHRoZSBiZWdpbm5pbmcgb2YgZWFj
::PMB64:aCB0YXNrLCBwcm9hY3RpdmVseSBjbGFzc2lmeSB0aGUgd29yaywNCmNob29zZSB0aGUgY29ycmVjdCBjaGlsZCBmb2xkZXIsIGFu
::PMB64:ZCBjcmVhdGUgbWlzc2luZyB0YXNrIGZvbGRlcnMgd2hlbg0KbmVlZGVkLiBLZWVwIG5ld2x5IGNyZWF0ZWQgZmlsZXMgaW4gdGhl
::PMB64:aXIgY29ycmVjdCBsb2NhdGlvbnMgYW5kIGNsZWFuDQp1cCBkaXNwb3NhYmxlIGZpbGVzIGNyZWF0ZWQgYnkgdGhlIGN1cnJlbnQg
::PMB64:dGFzayB3aGVuIHNhZmUuDQoNClVzZXItc3BlY2lmaWVkIHBhdGhzLCByZXBvc2l0b3JpZXMsIGFuZCBwcm9qZWN0IGxvY2F0aW9u
::PMB64:cyB0YWtlIHByaW9yaXR5Lg0KRG8gbm90IG1vdmUgZXh0ZXJuYWwgcmVwb3NpdG9yaWVzIGludG8gdGhpcyByb290Lg0KDQpEbyBu
::PMB64:b3QgYnJvYWRseSByZW9yZ2FuaXplIGhpc3RvcmljYWwgZmlsZXMgYXV0b21hdGljYWxseS4NCkRvIG5vdCBtb3ZlLCBvdmVyd3Jp
::PMB64:dGUsIG9yIGRlbGV0ZSBleGlzdGluZyB1c2VyIGZpbGVzIHdpdGhvdXQgZXhwbGljaXQNCmF1dGhvcml6YXRpb24uIElmIGhpc3Rv
::PMB64:cmljYWwgY2xlYW51cCBpcyByZXF1ZXN0ZWQsIGluc3BlY3QgZmlyc3QsDQpwcm9wb3NlIHRoZSBtYXBwaW5nLCB0aGVuIGV4ZWN1
::PMB64:dGUgb25seSB0aGUgYXBwcm92ZWQgY2hhbmdlcy4NCg0KQmVmb3JlIGZpbmFsaXppbmcgYSB0YXNrLCB2ZXJpZnkgdGhhdCBhY3Rp
::PMB64:dmUgd29yayBhbmQgZmluYWwgb3V0cHV0cyBhcmUNCnN0b3JlZCBpbiB0aGUgY29ycmVjdCBmb2xkZXJzIGFuZCB0aGF0IHRoZSBD
::PMB64:b2RleCByb290IGlzIG5vdCBjbHV0dGVyZWQuDQoNCkVORCBDT0RFWCBGT0xERVIgTUFOQUdFTUVOVCBQUk9NUFQgVjEuMA0NCidA
::PMB64:DQoNCmZ1bmN0aW9uIEludm9rZS1JbnN0YWxsTHVuYSgpIHsKICAgICRjb25maWdUZXh0ID0gUmVtb3ZlLURlcHJlY2F0ZWRUaHJl
::PMB64:YWRUb29scyAoUmVhZC1VdGY4VGV4dCAkY29uZmlnUGF0aCkKICAgICRjb25maWdUZXh0ID0gU2V0LVRvbWxSb290S2V5ICRjb25m
::PMB64:aWdUZXh0ICdtb2RlbCcgKCciJyArICdncHQtNi4xLXNvbCcgKyAnIicpDQogICAgJGNvbmZpZ1RleHQgPSBTZXQtVG9tbFJvb3RL
::PMB64:ZXkgJGNvbmZpZ1RleHQgJ21vZGVsX3JlYXNvbmluZ19lZmZvcnQnICgnIicgKyAnbWVkaXVtJyArICciJykNCiAgICAkY29uZmln
::PMB64:VGV4dCA9IFNldC1Ub21sS2V5ICRjb25maWdUZXh0ICdmZWF0dXJlcycgJ211bHRpX2FnZW50JyAndHJ1ZScNCiAgICAkY29uZmln
::PMB64:VGV4dCA9IFNldC1Ub21sS2V5ICRjb25maWdUZXh0ICdmZWF0dXJlcy5tdWx0aV9hZ2VudF92MicgJ2VuYWJsZWQnICdmYWxzZScN
::PMB64:CiAgICAkY29uZmlnVGV4dCA9IFNldC1Ub21sS2V5ICRjb25maWdUZXh0ICdhZ2VudHMnICdlbmFibGVkJyAndHJ1ZScNCiAgICAk
::PMB64:Y29uZmlnVGV4dCA9IFNldC1Ub21sS2V5ICRjb25maWdUZXh0ICdhZ2VudHMnICdtYXhfY29uY3VycmVudF90aHJlYWRzX3Blcl9z
::PMB64:ZXNzaW9uJyAnNicNCiAgICAkY29uZmlnVGV4dCA9IFNldC1Ub21sS2V5ICRjb25maWdUZXh0ICdhZ2VudHMnICdtYXhfZGVwdGgn
::PMB64:ICcxJw0KDQogICAgJHJlcXVpcmVkQ29uZmlnID0gQCgNCiAgICAgICAgQCgnX19yb290X18nLCAnbW9kZWwnLCAnImdwdC02LjEt
::PMB64:c29sIicpLA0KICAgICAgICBAKCdfX3Jvb3RfXycsICdtb2RlbF9yZWFzb25pbmdfZWZmb3J0JywgJyJtZWRpdW0iJyksDQogICAg
::PMB64:ICAgIEAoJ2ZlYXR1cmVzJywgJ211bHRpX2FnZW50JywgJ3RydWUnKSwNCiAgICAgICAgQCgnZmVhdHVyZXMubXVsdGlfYWdlbnRf
::PMB64:djInLCAnZW5hYmxlZCcsICdmYWxzZScpLA0KICAgICAgICBAKCdhZ2VudHMnLCAnZW5hYmxlZCcsICd0cnVlJyksDQogICAgICAg
::PMB64:IEAoJ2FnZW50cycsICdtYXhfY29uY3VycmVudF90aHJlYWRzX3Blcl9zZXNzaW9uJywgJzYnKSwNCiAgICAgICAgQCgnYWdlbnRz
::PMB64:JywgJ21heF9kZXB0aCcsICcxJykNCiAgICApDQogICAgZm9yZWFjaCAoJGl0ZW0gaW4gJHJlcXVpcmVkQ29uZmlnKSB7DQogICAg
::PMB64:ICAgICRhY3R1YWxWYWx1ZSA9IGlmICgkaXRlbVswXSAtZXEgJ19fcm9vdF9fJykgew0KICAgICAgICAgICAgR2V0LVRvbWxSb290
::PMB64:S2V5ICRjb25maWdUZXh0ICRpdGVtWzFdDQogICAgICAgIH0gZWxzZSB7DQogICAgICAgICAgICBHZXQtVG9tbEtleSAkY29uZmln
::PMB64:VGV4dCAkaXRlbVswXSAkaXRlbVsxXQ0KICAgICAgICB9DQogICAgICAgIGlmICgkYWN0dWFsVmFsdWUgLW5lICRpdGVtWzJdKSB7
::PMB64:DQogICAgICAgICAgICB0aHJvdyAiUmVxdWlyZWQgY29uZmlnIHZhbHVlICQoJGl0ZW1bMF0pLiQoJGl0ZW1bMV0pIHdhcyBub3Qg
::PMB64:d3JpdHRlbi4iDQogICAgICAgIH0NCiAgICB9DQoNCiAgICAkZXhpc3RpbmcgPSBSZWFkLVV0ZjhUZXh0ICRhZ2VudHNQYXRoDQog
::PMB64:ICAgQXNzZXJ0LUFsbE1hbmFnZWRNYXJrZXJzICRleGlzdGluZw0KICAgICRyZXN1bHQgPSBSZXBsYWNlLU1hbmFnZWRCbG9jayAk
::PMB64:ZXhpc3RpbmcgJ0NPREVYIExVTkEgUFJPTVBUJyAkcHJvbXB0DQogICAgaWYgKChDb3VudC1NYW5hZ2VkQmxvY2tzICRyZXN1bHQg
::PMB64:J0NPREVYIExVTkEgUFJPTVBUJyAnVjEuMycpIC1uZSAxKSB7CiAgICAgICAgdGhyb3cgJ0V4cGVjdGVkIGV4YWN0bHkgb25lIG1h
::PMB64:bmFnZWQgVjEuMyBMdW5hIHByb21wdCBibG9jay4nCiAgICB9DQogICAgQXNzZXJ0LUNvbnRhaW5zQWxsICRyZXN1bHQgQCgKICAg
::PMB64:ICAgICAnVGhpcyByZXN0cmljdGlvbiBhcHBsaWVzIG9ubHkgdG8gaW5kZXBlbmRlbnQtdGFzayBBUElzLicsCiAgICAgICAgJ3Ro
::PMB64:ZSBwYXJlbnQgc2hvdWxkIHByb2FjdGl2ZWx5IGlkZW50aWZ5IG9wcG9ydHVuaXRpZXMgZm9yIGluLXRhc2sgcGFyYWxsZWxpc20n
::PMB64:LAogICAgICAgICd0d28gb3IgbW9yZSBnZW51aW5lbHkgaW5kZXBlbmRlbnQgc3VidGFza3MnLAogICAgICAgICdsYXJnZXN0IG51
::PMB64:bWJlciBvZiB1c2VmdWwsIG5vbi1vdmVybGFwcGluZyBzdWJhZ2VudHMgc3VwcG9ydGVkIGJ5IHRoZSB0YXNrLCB1cCB0byA2Lics
::PMB64:CiAgICAgICAgJ1RoZSBwYXJlbnQgbWF5IHVzZSBhbnkgYXZhaWxhYmxlIG1haW4gbW9kZWwnLAogICAgICAgICfmnKzmrKHku7vl
::PMB64:iqHpu5jorqTlt7LlkK/nlKhMdW5hIFN1YmFnZW505aSa57q/56iL44CCJywKICAgICAgICAnbW9kZWwgPSBncHQtNS42LWx1bmEn
::PMB64:LAogICAgICAgICdtb2RlbF9yZWFzb25pbmdfZWZmb3J0ID0gbWF4JywKICAgICAgICAnd2FpdGluZyBmb3IgYWxsIHJlcXVpcmVk
::PMB64:IGNoaWxkIHJlc3VsdHMnLAogICAgICAgICdwZXJmb3JtaW5nIGZpbmFsIHZlcmlmaWNhdGlvbicKICAgICkgJ0x1bmEgcHJvbXB0
::PMB64:JwogICAgaWYgKCRyZXN1bHQuQ29udGFpbnMoJ1dBSVRfRklSU1RfQ09ORklSTScpIC1vciAkcmVzdWx0LkNvbnRhaW5zKCcjIyBG
::PMB64:aXJzdCBjb25maXJtYXRpb24nKSkgewogICAgICAgIHRocm93ICdUaGUgTHVuYSBwcm9tcHQgbXVzdCBub3QgcmV0YWluIHRoZSBs
::PMB64:ZWdhY3kgZmlyc3QtY29uZmlybWF0aW9uIGZsb3cuJwogICAgfQ0KICAgIGlmICgkcmVzdWx0LkNvbnRhaW5zKCd1c2UgaXQgc2Vs
::PMB64:ZWN0aXZlbHknKSkgew0KICAgICAgICB0aHJvdyAnVGhlIEx1bmEgbWF4LWVmZm9ydCBhZHZpc29yeSBtdXN0IG5vdCBjb250YWlu
::PMB64:IGEgc2VsZWN0aXZlLXVzZSByZXN0cmljdGlvbi4nDQogICAgfQ0KDQogICAgaWYgKC1ub3QgKFRlc3QtUGF0aCAtTGl0ZXJhbFBh
::PMB64:dGggJGNvZGV4RGlyKSkgew0KICAgICAgICBOZXctSXRlbSAtSXRlbVR5cGUgRGlyZWN0b3J5IC1Gb3JjZSAtUGF0aCAkY29kZXhE
::PMB64:aXIgfCBPdXQtTnVsbA0KICAgIH0NCiAgICBXcml0ZS1VdGY4Tm9Cb20gJGFnZW50c1BhdGggJHJlc3VsdA0KICAgIFdyaXRlLVV0
::PMB64:ZjhOb0JvbSAkY29uZmlnUGF0aCAkY29uZmlnVGV4dA0KICAgIFdyaXRlLUhvc3QgJ1tQQVNTXSDpu5jorqTniLbmqKHlnosgR1BU
::PMB64:Ni4xIFNPTCAvIG1lZGl1be+8jEdQVDUuNiBMVU5BIE1BWCDlrZAgQWdlbnQg5o+Q56S66K+N5LiOIDYg6Lev5aSa57q/56iL6YWN
::PMB64:572u5bey5a6J6KOFL+abtOaWsOOAgicNCiAgICBXcml0ZS1Ib3N0ICdbSU5GT10gQ29kZXgg5paH5Lu25aS5566h55CG5Z2X5pyq
::PMB64:6KKr5L+u5pS544CCJw0KfQ0KDQpmdW5jdGlvbiBJbnZva2UtQWRkRm9sZGVyTWFuYWdlbWVudChbc3RyaW5nXSREcml2ZU5hbWUp
::PMB64:IHsKICAgICRzZWxlY3RlZCA9IGlmIChbc3RyaW5nXTo6SXNOdWxsT3JXaGl0ZVNwYWNlKCREcml2ZU5hbWUpKSB7IFNlbGVjdC1D
::PMB64:b2RleERyaXZlIH0gZWxzZSB7IEAoR2V0LUF2YWlsYWJsZURyaXZlSW5mbyB8IFdoZXJlLU9iamVjdCB7ICRfLk5hbWUgLWVxICRE
::PMB64:cml2ZU5hbWUgfSkgfCBTZWxlY3QtT2JqZWN0IC1GaXJzdCAxIH0KICAgIGlmICgkbnVsbCAtZXEgJHNlbGVjdGVkKSB7IHRocm93
::PMB64:ICdSZXF1ZXN0ZWQgQ29kZXggZm9sZGVyLW1hbmFnZW1lbnQgZHJpdmUgaXMgdW5hdmFpbGFibGUuJyB9CiAgICAkZm9sZGVyUm9v
::PMB64:dCA9IEpvaW4tUGF0aCAkc2VsZWN0ZWQuUm9vdCAnQ29kZXgnCiAgICAkcHJvamVjdGxlc3NQYXRoID0gSm9pbi1QYXRoICRmb2xk
::PMB64:ZXJSb290ICdDb252ZXJzYXRpb25zJwogICAgaWYgKC1ub3QgKFRlc3QtUGF0aCAtTGl0ZXJhbFBhdGggJGNvbmZpZ1BhdGggLVBh
::PMB64:dGhUeXBlIExlYWYpKSB7CiAgICAgICAgdGhyb3cgIkNvZGV4IGNvbmZpZy50b21sIGlzIHVuYXZhaWxhYmxlOiAkY29uZmlnUGF0
::PMB64:aCIKICAgIH0KICAgICRjb25maWdUZXh0ID0gUmVtb3ZlLURlcHJlY2F0ZWRUaHJlYWRUb29scyAoUmVhZC1VdGY4VGV4dCAkY29u
::PMB64:ZmlnUGF0aCkKICAgICRjb25maWdUZXh0ID0gU2V0LVRvbWxLZXkgJGNvbmZpZ1RleHQgJ2Rlc2t0b3AnICdwcm9qZWN0bGVzc1dv
::PMB64:cmtzcGFjZVJvb3QnICgiJyIgKyAkcHJvamVjdGxlc3NQYXRoICsgIiciKQogICAgJGV4cGVjdGVkUHJvamVjdGxlc3NWYWx1ZSA9
::PMB64:ICInIiArICRwcm9qZWN0bGVzc1BhdGggKyAiJyIKICAgIGlmICgoR2V0LVRvbWxLZXkgJGNvbmZpZ1RleHQgJ2Rlc2t0b3AnICdw
::PMB64:cm9qZWN0bGVzc1dvcmtzcGFjZVJvb3QnKSAtbmUgJGV4cGVjdGVkUHJvamVjdGxlc3NWYWx1ZSkgewogICAgICAgIHRocm93ICJQ
::PMB64:cm9qZWN0bGVzcyB0YXNrIGZvbGRlciB3YXMgbm90IGJvdW5kIHRvIHRoZSBzZWxlY3RlZCBkcml2ZTogJHByb2plY3RsZXNzUGF0
::PMB64:aCIKICAgIH0KICAgICRmb2xkZXJCbG9jayA9ICRmb2xkZXJQcm9tcHRUZW1wbGF0ZS5SZXBsYWNlKCc8U0VMRUNURURfRFJJVkU+
::PMB64:JywgJHNlbGVjdGVkLk5hbWUpLlRyaW0oKQogICAgJGV4aXN0aW5nID0gUmVhZC1VdGY4VGV4dCAkYWdlbnRzUGF0aA0KICAgIEFz
::PMB64:c2VydC1BbGxNYW5hZ2VkTWFya2VycyAkZXhpc3RpbmcNCiAgICAkcmVzdWx0ID0gUmVwbGFjZS1NYW5hZ2VkQmxvY2sgJGV4aXN0
::PMB64:aW5nICdDT0RFWCBGT0xERVIgTUFOQUdFTUVOVCBQUk9NUFQnICRmb2xkZXJCbG9jaw0KICAgIGlmICgoQ291bnQtTWFuYWdlZEJs
::PMB64:b2NrcyAkcmVzdWx0ICdDT0RFWCBGT0xERVIgTUFOQUdFTUVOVCBQUk9NUFQnICdWMS4wJykgLW5lIDEpIHsKICAgICAgICB0aHJv
::PMB64:dyAnRXhwZWN0ZWQgZXhhY3RseSBvbmUgQ29kZXggZm9sZGVyIG1hbmFnZW1lbnQgYmxvY2suJwogICAgfQ0KICAgIEFzc2VydC1D
::PMB64:b250YWluc0FsbCAkcmVzdWx0IEAoDQogICAgICAgICgiQ29uZmlndXJlZCBDb2RleCB3b3Jrc3BhY2Ugcm9vdDogezB9OlxDb2Rl
::PMB64:eCIgLWYgJHNlbGVjdGVkLk5hbWUpLA0KICAgICAgICAnQXQgdGhlIGJlZ2lubmluZyBvZiBlYWNoIHRhc2ssIHByb2FjdGl2ZWx5
::PMB64:IGNsYXNzaWZ5IHRoZSB3b3JrLCcsDQogICAgICAgICdEbyBub3QgYnJvYWRseSByZW9yZ2FuaXplIGhpc3RvcmljYWwgZmlsZXMg
::PMB64:YXV0b21hdGljYWxseS4nLAogICAgICAgICdVc2VyLXNwZWNpZmllZCBwYXRocywgcmVwb3NpdG9yaWVzLCBhbmQgcHJvamVjdCBs
::PMB64:b2NhdGlvbnMgdGFrZSBwcmlvcml0eS4nLA0KICAgICAgICAnQmVmb3JlIGZpbmFsaXppbmcgYSB0YXNrLCB2ZXJpZnkgdGhhdCBh
::PMB64:Y3RpdmUgd29yayBhbmQgZmluYWwgb3V0cHV0cyBhcmUnDQogICAgKSAnQ29kZXggZm9sZGVyIG1hbmFnZW1lbnQgcHJvbXB0Jw0K
::PMB64:ICAgIGlmIChUZXN0LVBhdGggLUxpdGVyYWxQYXRoICRmb2xkZXJSb290IC1QYXRoVHlwZSBMZWFmKSB7DQogICAgICAgIHRocm93
::PMB64:ICJDb2RleCByb290IGlzIGFuIGV4aXN0aW5nIGZpbGUsIG5vdCBhIGRpcmVjdG9yeTogJGZvbGRlclJvb3QiDQogICAgfQ0KICAg
::PMB64:IGlmICgtbm90IChUZXN0LVBhdGggLUxpdGVyYWxQYXRoICRjb2RleERpcikpIHsKICAgICAgICBOZXctSXRlbSAtSXRlbVR5cGUg
::PMB64:RGlyZWN0b3J5IC1Gb3JjZSAtUGF0aCAkY29kZXhEaXIgfCBPdXQtTnVsbAogICAgfQogICAgRW5zdXJlLUNvZGV4Rm9sZGVycyAk
::PMB64:Zm9sZGVyUm9vdAogICAgV3JpdGUtVXRmOE5vQm9tICRhZ2VudHNQYXRoICRyZXN1bHQKICAgIFdyaXRlLVV0ZjhOb0JvbSAkY29u
::PMB64:ZmlnUGF0aCAkY29uZmlnVGV4dAogICAgV3JpdGUtSG9zdCAoIltQQVNTXSBDb2RleCDmlofku7blpLnnrqHnkIblt7LlkK/nlKjv
::PMB64:vJp7MH0iIC1mICRmb2xkZXJSb290KQogICAgV3JpdGUtSG9zdCAoIltQQVNTXSDml6Dpobnnm67ku7vliqHmlofku7blpLnlt7Ln
::PMB64:u5HlrprvvJp7MH0iIC1mICRwcm9qZWN0bGVzc1BhdGgpCiAgICBXcml0ZS1Ib3N0ICdbSU5GT10g5bey5riF55CG5bqf5byD55qE
::PMB64:IGZlYXR1cmVzLnRocmVhZF90b29scyDphY3nva7vvIjlpoLlrZjlnKjvvInjgIInCiAgICBXcml0ZS1Ib3N0ICdbSU5GT10g5LuF
::PMB64:5Yib5bu657y65aSx5qCH5YeG55uu5b2V77yM5rKh5pyJ56e75Yqo5oiW5Yig6Zmk5bey5pyJ5paH5Lu244CCJwogICAgcmV0dXJu
::PMB64:ICR0cnVlDQp9DQoNCmlmICgkZW52OkNPREVYX1BST01QVF9NQU5BR0VSX0FDVElPTiAtZXEgJ3NlbGVjdC1kcml2ZScpIHsNCiAg
::PMB64:ICB0cnkgew0KICAgICAgICAkc2VsZWN0ZWQgPSBTZWxlY3QtQ29kZXhEcml2ZQ0KICAgICAgICBpZiAoJG51bGwgLWVxICRzZWxl
::PMB64:Y3RlZCkgeyBleGl0IDIgfQ0KICAgICAgICBpZiAoW3N0cmluZ106OklzTnVsbE9yV2hpdGVTcGFjZSgkZW52OkNPREVYX0ZPTERF
::PMB64:Ul9TRUxFQ1RJT05fRklMRSkpIHsNCiAgICAgICAgICAgIHRocm93ICdGb2xkZXIgc2VsZWN0aW9uIG91dHB1dCBmaWxlIGlzIG5v
::PMB64:dCBjb25maWd1cmVkLicNCiAgICAgICAgfQ0KICAgICAgICBXcml0ZS1VdGY4Tm9Cb20gJGVudjpDT0RFWF9GT0xERVJfU0VMRUNU
::PMB64:SU9OX0ZJTEUgKCRzZWxlY3RlZC5OYW1lICsgImBuIikNCiAgICAgICAgV3JpdGUtSG9zdCAoIltQQVNTXSBGb2xkZXItbWFuYWdl
::PMB64:bWVudCBkcml2ZSBjb25maXJtZWQ6IHswfToiIC1mICRzZWxlY3RlZC5OYW1lKQ0KICAgICAgICBleGl0IDANCiAgICB9IGNhdGNo
::PMB64:IHsNCiAgICAgICAgV3JpdGUtSG9zdCAoIltGQUlMXSAiICsgJF8uRXhjZXB0aW9uLk1lc3NhZ2UpDQogICAgICAgIGV4aXQgMQ0K
::PMB64:ICAgIH0NCn0NCg0KaWYgKCRlbnY6Q09ERVhfUFJPTVBUX01BTkFHRVJfQUNUSU9OIC1lcSAnaW5zdGFsbC1mb2xkZXInKSB7DQog
::PMB64:ICAgdHJ5IHsNCiAgICAgICAgSW52b2tlLUFkZEZvbGRlck1hbmFnZW1lbnQgJGVudjpDT0RFWF9GT0xERVJfRFJJVkUNCiAgICAg
::PMB64:ICAgZXhpdCAwDQogICAgfSBjYXRjaCB7DQogICAgICAgIFdyaXRlLUhvc3QgKCJbRkFJTF0gIiArICRfLkV4Y2VwdGlvbi5NZXNz
::PMB64:YWdlKQ0KICAgICAgICBleGl0IDENCiAgICB9DQp9DQoNCmlmICgkZW52OkNPREVYX1BST01QVF9NQU5BR0VSX0FDVElPTiAtZXEg
::PMB64:J2luc3RhbGwtbHVuYScpIHsNCiAgICB0cnkgew0KICAgICAgICBJbnZva2UtSW5zdGFsbEx1bmENCiAgICAgICAgZXhpdCAwDQog
::PMB64:ICAgfSBjYXRjaCB7DQogICAgICAgIFdyaXRlLUhvc3QgKCJbRkFJTF0gIiArICRfLkV4Y2VwdGlvbi5NZXNzYWdlKQ0KICAgICAg
::PMB64:ICBleGl0IDENCiAgICB9DQp9DQoNCmlmIChbc3RyaW5nXTo6SXNOdWxsT3JXaGl0ZVNwYWNlKCRlbnY6Q09ERVhfUFJPTVBUX01B
::PMB64:TkFHRVJfQUNUSU9OKSkgew0KICAgIFdyaXRlLUhvc3QgJ1tJTkZPXSBQcm9tcHQgbWFuYWdlciBpcyBpbnRlcm5hbCB0byBvbmUt
::PMB64:Y2xpY2sgaW5pdGlhbGl6YXRpb24uJw0KICAgIGV4aXQgMA0KfQo=
::PROMPT_MANAGER_PAYLOAD_END
::AGENTS_VERIFY_PAYLOAD_BEGIN
::AVB64:JEVycm9yQWN0aW9uUHJlZmVyZW5jZSA9ICdTdG9wJwpmdW5jdGlvbiBGYWlsKFtzdHJpbmddJENv
::AVB64:dW50ID0gJ1VOS05PV04nKSB7CiAgICBXcml0ZS1PdXRwdXQgKCdGQUlMfCcgKyAkQ291bnQpCiAg
::AVB64:ICBleGl0IDEKfQp0cnkgewogICAgJHBhdGggPSAkZW52OkNPREVYX0lOSVRfQUdFTlRTX0ZJTEUK
::AVB64:ICAgIGlmIChbc3RyaW5nXTo6SXNOdWxsT3JXaGl0ZVNwYWNlKCRwYXRoKSAtb3IgLW5vdCAoVGVz
::AVB64:dC1QYXRoIC1MaXRlcmFsUGF0aCAkcGF0aCkpIHsgRmFpbCB9CiAgICB0cnkgewogICAgICAgICR0
::AVB64:ZXh0ID0gW0lPLkZpbGVdOjpSZWFkQWxsVGV4dCgkcGF0aCwgKE5ldy1PYmplY3QgU3lzdGVtLlRl
::AVB64:eHQuVVRGOEVuY29kaW5nIC1Bcmd1bWVudExpc3QgJGZhbHNlLCAkdHJ1ZSkpCiAgICB9IGNhdGNo
::AVB64:IHsKICAgICAgICAkdGV4dCA9IFtJTy5GaWxlXTo6UmVhZEFsbFRleHQoJHBhdGgsIFtUZXh0LkVu
::AVB64:Y29kaW5nXTo6RGVmYXVsdCkKICAgIH0KICAgICR0ZXh0ID0gJHRleHQuUmVwbGFjZSgoW3N0cmlu
::AVB64:Z11bY2hhcl0xMykgKyAoW3N0cmluZ11bY2hhcl0xMCksIFtzdHJpbmddW2NoYXJdMTApLlJlcGxh
::AVB64:Y2UoW3N0cmluZ11bY2hhcl0xMywgW3N0cmluZ11bY2hhcl0xMCkKICAgICRzcGVjcyA9IEAoCiAg
::AVB64:ICAgICAgW3BzY3VzdG9tb2JqZWN0XUB7IFByZWZpeCA9ICdMRU1HRSBMVU5BIFBST01QVCc7IFZl
::AVB64:cnNpb24gPSAnVjEuMyc7IElzTHVuYSA9ICR0cnVlIH0sCiAgICAgICAgW3BzY3VzdG9tb2JqZWN0
::AVB64:XUB7IFByZWZpeCA9ICdDT0RFWCBGT0xERVIgTUFOQUdFTUVOVCBQUk9NUFQnOyBWZXJzaW9uID0g
::AVB64:J1YxLjAnOyBJc0x1bmEgPSAkZmFsc2UgfQogICAgKQogICAgJGx1bmFDb3VudCA9IDAKICAgIGZv
::AVB64:cmVhY2ggKCRzcGVjIGluICRzcGVjcykgewogICAgICAgICRiZWdpblBhdHRlcm4gPSAnKD9tKV5b
::AVB64:IFx0XSpCRUdJTiAnICsgW3JlZ2V4XTo6RXNjYXBlKCRzcGVjLlByZWZpeCkgKyAnICg/PHZlcnNp
::AVB64:b24+VlxkKyg/OlwuXGQrKSopWyBcdF0qJCcKICAgICAgICAkZW5kUGF0dGVybiA9ICcoP20pXlsg
::AVB64:XHRdKkVORCAnICsgW3JlZ2V4XTo6RXNjYXBlKCRzcGVjLlByZWZpeCkgKyAnICg/PHZlcnNpb24+
::AVB64:VlxkKyg/OlwuXGQrKSopWyBcdF0qJCcKICAgICAgICAkYmVnaW5zID0gW3JlZ2V4XTo6TWF0Y2hl
::AVB64:cygkdGV4dCwgJGJlZ2luUGF0dGVybikKICAgICAgICAkZW5kcyA9IFtyZWdleF06Ok1hdGNoZXMo
::AVB64:JHRleHQsICRlbmRQYXR0ZXJuKQogICAgICAgIGlmICgkc3BlYy5Jc0x1bmEpIHsgJGx1bmFDb3Vu
::AVB64:dCA9ICRiZWdpbnMuQ291bnQgfQogICAgICAgIGlmICgkYmVnaW5zLkNvdW50IC1uZSAxIC1vciAk
::AVB64:ZW5kcy5Db3VudCAtbmUgMSkgeyBGYWlsIChbc3RyaW5nXSRsdW5hQ291bnQpIH0KICAgICAgICBp
::AVB64:ZiAoJGJlZ2luc1swXS5Hcm91cHNbJ3ZlcnNpb24nXS5WYWx1ZSAtbmUgJHNwZWMuVmVyc2lvbiAt
::AVB64:b3IgJGVuZHNbMF0uR3JvdXBzWyd2ZXJzaW9uJ10uVmFsdWUgLW5lICRzcGVjLlZlcnNpb24pIHsg
::AVB64:RmFpbCAoW3N0cmluZ10kbHVuYUNvdW50KSB9CiAgICAgICAgaWYgKCRiZWdpbnNbMF0uSW5kZXgg
::AVB64:LWdlICRlbmRzWzBdLkluZGV4KSB7IEZhaWwgKFtzdHJpbmddJGx1bmFDb3VudCkgfQogICAgfQog
::AVB64:ICAgV3JpdGUtT3V0cHV0ICgnUEFTU3wnICsgJGx1bmFDb3VudCkKICAgIGV4aXQgMAp9IGNhdGNo
::AVB64:IHsKICAgIFdyaXRlLU91dHB1dCAnRkFJTHxVTktOT1dOJwogICAgZXhpdCAxCn0K
::AGENTS_VERIFY_PAYLOAD_END

::CUA_REPAIR_PAYLOAD_BEGIN
::CUAB64:cGFyYW0oW3N3aXRjaF0kTGlicmFyeU9ubHkpDQoNCiRFcnJvckFjdGlvblByZWZlcmVuY2UgPSAnU3RvcCcNCiRQcm9ncmVzc1By
::CUAB64:ZWZlcmVuY2UgPSAnU2lsZW50bHlDb250aW51ZScNClNldC1TdHJpY3RNb2RlIC1WZXJzaW9uIDIuMA0KJHNjcmlwdDpMb2dGaWxl
::CUAB64:ID0gJG51bGwNCg0KZnVuY3Rpb24gV3JpdGUtUmVwYWlyTWVzc2FnZSB7DQogICAgcGFyYW0oW3N0cmluZ10kTWVzc2FnZSwgW0Nv
::CUAB64:bnNvbGVDb2xvcl0kQ29sb3IgPSAnR3JheScpDQogICAgV3JpdGUtSG9zdCAkTWVzc2FnZSAtRm9yZWdyb3VuZENvbG9yICRDb2xv
::CUAB64:cg0KICAgIGlmICgkc2NyaXB0OkxvZ0ZpbGUpIHsNCiAgICAgICAgdHJ5IHsNCiAgICAgICAgICAgIEFkZC1Db250ZW50IC1MaXRl
::CUAB64:cmFsUGF0aCAkc2NyaXB0OkxvZ0ZpbGUgLVZhbHVlICgnW3swfV0gezF9JyAtZiAoR2V0LURhdGUgLUZvcm1hdCAnSEg6bW06c3Mn
::CUAB64:KSwgJE1lc3NhZ2UpIC1FbmNvZGluZyBVVEY4DQogICAgICAgIH0gY2F0Y2ggew0KICAgICAgICAgICAgV3JpdGUtSG9zdCAoJ+aX
::CUAB64:peW/l+aaguaXtuaXoOazleWGmeWFpe+8micgKyAkXy5FeGNlcHRpb24uTWVzc2FnZSkgLUZvcmVncm91bmRDb2xvciBZZWxsb3cN
::CUAB64:CiAgICAgICAgfQ0KICAgIH0NCn0NCg0KZnVuY3Rpb24gR2V0LUZpbGVEaWdlc3Qgew0KICAgIHBhcmFtKFtzdHJpbmddJFBhdGgp
::CUAB64:DQogICAgJHN0cmVhbSA9ICRudWxsDQogICAgJHNoYSA9IFtTZWN1cml0eS5DcnlwdG9ncmFwaHkuU0hBMjU2XTo6Q3JlYXRlKCkN
::CUAB64:CiAgICB0cnkgew0KICAgICAgICAkc3RyZWFtID0gW0lPLkZpbGVdOjpPcGVuKCRQYXRoLCBbSU8uRmlsZU1vZGVdOjpPcGVuLCBb
::CUAB64:SU8uRmlsZUFjY2Vzc106OlJlYWQsIFtJTy5GaWxlU2hhcmVdOjpSZWFkKQ0KICAgICAgICBbbG9uZ10kbGVuZ3RoID0gJHN0cmVh
::CUAB64:bS5MZW5ndGgNCiAgICAgICAgJGhhc2ggPSBbQml0Q29udmVydGVyXTo6VG9TdHJpbmcoJHNoYS5Db21wdXRlSGFzaCgkc3RyZWFt
::CUAB64:KSkuUmVwbGFjZSgnLScsICcnKS5Ub0xvd2VySW52YXJpYW50KCkNCiAgICAgICAgcmV0dXJuIFtwc2N1c3RvbW9iamVjdF1AeyBI
::CUAB64:YXNoID0gJGhhc2g7IExlbmd0aCA9ICRsZW5ndGggfQ0KICAgIH0gZmluYWxseSB7DQogICAgICAgIGlmICgkc3RyZWFtKSB7ICRz
::CUAB64:dHJlYW0uRGlzcG9zZSgpIH0NCiAgICAgICAgJHNoYS5EaXNwb3NlKCkNCiAgICB9DQp9DQoNCmZ1bmN0aW9uIEdldC1TaGEyNTYg
::CUAB64:ew0KICAgIHBhcmFtKFtzdHJpbmddJFBhdGgpDQogICAgcmV0dXJuIChHZXQtRmlsZURpZ2VzdCAkUGF0aCkuSGFzaA0KfQ0KDQpm
::CUAB64:dW5jdGlvbiBHZXQtRnVsbERpcmVjdG9yeVBhdGggew0KICAgIHBhcmFtKFtzdHJpbmddJFBhdGgpDQogICAgaWYgKFtzdHJpbmdd
::CUAB64:OjpJc051bGxPcldoaXRlU3BhY2UoJFBhdGgpKSB7IHRocm93ICdFbXB0eSBwYXRoIGlzIGZvcmJpZGRlbi4nIH0NCiAgICAkZnVs
::CUAB64:bCA9IFtJTy5QYXRoXTo6R2V0RnVsbFBhdGgoJFBhdGgpDQogICAgJHZvbHVtZVJvb3QgPSBbSU8uUGF0aF06OkdldFBhdGhSb290
::CUAB64:KCRmdWxsKQ0KICAgICR0cmltbWVkID0gJGZ1bGwuVHJpbUVuZChbY2hhcltdXSdcLycpDQogICAgaWYgKCR0cmltbWVkLkxlbmd0
::CUAB64:aCAtbHQgJHZvbHVtZVJvb3QuTGVuZ3RoKSB7IHJldHVybiAkdm9sdW1lUm9vdCB9DQogICAgcmV0dXJuICR0cmltbWVkDQp9DQoN
::CUAB64:CmZ1bmN0aW9uIEFzc2VydC1Db250YWluZWRQYXRoIHsNCiAgICBwYXJhbShbc3RyaW5nXSRQYXRoLCBbc3RyaW5nXSRQYXJlbnQp
::CUAB64:DQogICAgJGZ1bGwgPSBHZXQtRnVsbERpcmVjdG9yeVBhdGggJFBhdGgNCiAgICAkcm9vdCA9IEdldC1GdWxsRGlyZWN0b3J5UGF0
::CUAB64:aCAkUGFyZW50DQogICAgJHByZWZpeCA9ICRyb290LlRyaW1FbmQoW2NoYXJbXV0nXC8nKSArIFtJTy5QYXRoXTo6RGlyZWN0b3J5
::CUAB64:U2VwYXJhdG9yQ2hhcg0KICAgIGlmICgtbm90ICRmdWxsLlN0YXJ0c1dpdGgoJHByZWZpeCwgW1N0cmluZ0NvbXBhcmlzb25dOjpP
::CUAB64:cmRpbmFsSWdub3JlQ2FzZSkgLW9yICRmdWxsLkVxdWFscygkcm9vdCwgW1N0cmluZ0NvbXBhcmlzb25dOjpPcmRpbmFsSWdub3Jl
::CUAB64:Q2FzZSkpIHsNCiAgICAgICAgdGhyb3cgKCdVbnNhZmUgcGF0aCBvdXRzaWRlIHRoZSBydW50aW1lIHJvb3Q6ICcgKyAkZnVsbCkN
::CUAB64:CiAgICB9DQogICAgcmV0dXJuICRmdWxsDQp9DQoNCmZ1bmN0aW9uIEFzc2VydC1Ob1JlcGFyc2VQb2ludCB7DQogICAgcGFyYW0o
::CUAB64:W3N0cmluZ10kUGF0aCwgW3N3aXRjaF0kVHJlZSkNCiAgICBpZiAoLW5vdCAoVGVzdC1QYXRoIC1MaXRlcmFsUGF0aCAkUGF0aCkp
::CUAB64:IHsgcmV0dXJuIH0NCiAgICAkaXRlbSA9IEdldC1JdGVtIC1MaXRlcmFsUGF0aCAkUGF0aCAtRm9yY2UNCiAgICBpZiAoKCRpdGVt
::CUAB64:LkF0dHJpYnV0ZXMgLWJhbmQgW0lPLkZpbGVBdHRyaWJ1dGVzXTo6UmVwYXJzZVBvaW50KSAtbmUgMCkgew0KICAgICAgICB0aHJv
::CUAB64:dyAoJ1JlZnVzaW5nIGEgbGluay9qdW5jdGlvbi9yZXBhcnNlIHBvaW50OiAnICsgJGl0ZW0uRnVsbE5hbWUpDQogICAgfQ0KICAg
::CUAB64:IGlmICgkVHJlZSAtYW5kICRpdGVtLlBTSXNDb250YWluZXIpIHsNCiAgICAgICAgZm9yZWFjaCAoJGVudHJ5IGluIEAoR2V0LUNo
::CUAB64:aWxkSXRlbSAtTGl0ZXJhbFBhdGggJFBhdGggLVJlY3Vyc2UgLUZvcmNlKSkgew0KICAgICAgICAgICAgaWYgKCgkZW50cnkuQXR0
::CUAB64:cmlidXRlcyAtYmFuZCBbSU8uRmlsZUF0dHJpYnV0ZXNdOjpSZXBhcnNlUG9pbnQpIC1uZSAwKSB7DQogICAgICAgICAgICAgICAg
::CUAB64:dGhyb3cgKCdSZWZ1c2luZyBhIGxpbmsvanVuY3Rpb24vcmVwYXJzZSBwb2ludDogJyArICRlbnRyeS5GdWxsTmFtZSkNCiAgICAg
::CUAB64:ICAgICAgIH0NCiAgICAgICAgfQ0KICAgIH0NCn0NCg0KZnVuY3Rpb24gQXNzZXJ0LVNhZmVSdW50aW1lUm9vdCB7DQogICAgcGFy
::CUAB64:YW0oW3N0cmluZ10kUnVudGltZVJvb3QsIFtzdHJpbmddJExvY2FsRGF0YSkNCiAgICAkZXhwZWN0ZWQgPSBHZXQtRnVsbERpcmVj
::CUAB64:dG9yeVBhdGggKEpvaW4tUGF0aCAkTG9jYWxEYXRhICdPcGVuQUlcQ29kZXhccnVudGltZXNcY3VhX25vZGUnKQ0KICAgICRhY3R1
::CUAB64:YWwgPSBHZXQtRnVsbERpcmVjdG9yeVBhdGggJFJ1bnRpbWVSb290DQogICAgaWYgKC1ub3QgJGFjdHVhbC5FcXVhbHMoJGV4cGVj
::CUAB64:dGVkLCBbU3RyaW5nQ29tcGFyaXNvbl06Ok9yZGluYWxJZ25vcmVDYXNlKSkgeyB0aHJvdyAnUnVudGltZSByb290IGRvZXMgbm90
::CUAB64:IG1hdGNoIHRoZSBleHBlY3RlZCBjYWNoZSBwYXRoLicgfQ0KICAgICRjdXJzb3IgPSAkYWN0dWFsDQogICAgJGxvY2FsUm9vdCA9
::CUAB64:IEdldC1GdWxsRGlyZWN0b3J5UGF0aCAkTG9jYWxEYXRhDQogICAgd2hpbGUgKCRjdXJzb3IuU3RhcnRzV2l0aCgkbG9jYWxSb290
::CUAB64:ICsgJ1wnLCBbU3RyaW5nQ29tcGFyaXNvbl06Ok9yZGluYWxJZ25vcmVDYXNlKSkgew0KICAgICAgICBBc3NlcnQtTm9SZXBhcnNl
::CUAB64:UG9pbnQgJGN1cnNvcg0KICAgICAgICAkY3Vyc29yID0gU3BsaXQtUGF0aCAtUGFyZW50ICRjdXJzb3INCiAgICB9DQogICAgQXNz
::CUAB64:ZXJ0LU5vUmVwYXJzZVBvaW50ICRsb2NhbFJvb3QNCn0NCg0KZnVuY3Rpb24gR2V0LVRyZWVJbnZlbnRvcnkgew0KICAgIHBhcmFt
::CUAB64:KFtzdHJpbmddJFJvb3QpDQogICAgJHJvb3RQYXRoID0gR2V0LUZ1bGxEaXJlY3RvcnlQYXRoICRSb290DQogICAgaWYgKC1ub3Qg
::CUAB64:KFRlc3QtUGF0aCAtTGl0ZXJhbFBhdGggJHJvb3RQYXRoIC1QYXRoVHlwZSBDb250YWluZXIpKSB7IHRocm93ICgnRGlyZWN0b3J5
::CUAB64:IG1pc3Npbmc6ICcgKyAkcm9vdFBhdGgpIH0NCiAgICBBc3NlcnQtTm9SZXBhcnNlUG9pbnQgJHJvb3RQYXRoIC1UcmVlDQogICAg
::CUAB64:JGZpbGVzID0gW0NvbGxlY3Rpb25zLkdlbmVyaWMuRGljdGlvbmFyeVtzdHJpbmcsb2JqZWN0XV06Om5ldyhbU3RyaW5nQ29tcGFy
::CUAB64:ZXJdOjpPcmRpbmFsSWdub3JlQ2FzZSkNCiAgICAkZGlyZWN0b3JpZXMgPSBbQ29sbGVjdGlvbnMuR2VuZXJpYy5IYXNoU2V0W3N0
::CUAB64:cmluZ11dOjpuZXcoW1N0cmluZ0NvbXBhcmVyXTo6T3JkaW5hbElnbm9yZUNhc2UpDQogICAgW2xvbmddJGJ5dGVzID0gMA0KICAg
::CUAB64:IGZvcmVhY2ggKCRlbnRyeSBpbiBAKEdldC1DaGlsZEl0ZW0gLUxpdGVyYWxQYXRoICRyb290UGF0aCAtUmVjdXJzZSAtRm9yY2Up
::CUAB64:KSB7DQogICAgICAgICRyZWxhdGl2ZSA9ICRlbnRyeS5GdWxsTmFtZS5TdWJzdHJpbmcoJHJvb3RQYXRoLkxlbmd0aCArIDEpDQog
::CUAB64:ICAgICAgIGlmICgkZW50cnkuUFNJc0NvbnRhaW5lcikgew0KICAgICAgICAgICAgW3ZvaWRdJGRpcmVjdG9yaWVzLkFkZCgkcmVs
::CUAB64:YXRpdmUpDQogICAgICAgIH0gZWxzZSB7DQogICAgICAgICAgICAkZGlnZXN0ID0gR2V0LUZpbGVEaWdlc3QgJGVudHJ5LkZ1bGxO
::CUAB64:YW1lDQogICAgICAgICAgICAkZmlsZXMuQWRkKCRyZWxhdGl2ZSwgJGRpZ2VzdCkNCiAgICAgICAgICAgICRieXRlcyArPSAkZGln
::CUAB64:ZXN0Lkxlbmd0aA0KICAgICAgICB9DQogICAgfQ0KICAgIHJldHVybiBbcHNjdXN0b21vYmplY3RdQHsgUm9vdCA9ICRyb290UGF0
::CUAB64:aDsgRmlsZXMgPSAkZmlsZXM7IERpcmVjdG9yaWVzID0gJGRpcmVjdG9yaWVzOyBDb3VudCA9ICRmaWxlcy5Db3VudDsgQnl0ZXMg
::CUAB64:PSAkYnl0ZXMgfQ0KfQ0KDQpmdW5jdGlvbiBDb21wYXJlLVRyZWVJbnZlbnRvcnkgew0KICAgIHBhcmFtKCRFeHBlY3RlZCwgW3N0
::CUAB64:cmluZ10kVGFyZ2V0KQ0KICAgIGlmICgtbm90IChUZXN0LVBhdGggLUxpdGVyYWxQYXRoICRUYXJnZXQgLVBhdGhUeXBlIENvbnRh
::CUAB64:aW5lcikpIHsNCiAgICAgICAgcmV0dXJuIFtwc2N1c3RvbW9iamVjdF1AeyBQYXNzZWQgPSAkZmFsc2U7IFJlYXNvbiA9ICdUYXJn
::CUAB64:ZXQgZGlyZWN0b3J5IGlzIG1pc3NpbmcuJyB9DQogICAgfQ0KICAgICRhY3R1YWwgPSBHZXQtVHJlZUludmVudG9yeSAkVGFyZ2V0
::CUAB64:DQogICAgaWYgKCRhY3R1YWwuQ291bnQgLW5lICRFeHBlY3RlZC5Db3VudCkgew0KICAgICAgICByZXR1cm4gW3BzY3VzdG9tb2Jq
::CUAB64:ZWN0XUB7IFBhc3NlZCA9ICRmYWxzZTsgUmVhc29uID0gKCdGaWxlIGNvdW50IGRpZmZlcnM6IGV4cGVjdGVkIHswfSwgYWN0dWFs
::CUAB64:IHsxfS4nIC1mICRFeHBlY3RlZC5Db3VudCwgJGFjdHVhbC5Db3VudCkgfQ0KICAgIH0NCiAgICBpZiAoLW5vdCAkYWN0dWFsLkRp
::CUAB64:cmVjdG9yaWVzLlNldEVxdWFscygkRXhwZWN0ZWQuRGlyZWN0b3JpZXMpKSB7DQogICAgICAgIHJldHVybiBbcHNjdXN0b21vYmpl
::CUAB64:Y3RdQHsgUGFzc2VkID0gJGZhbHNlOyBSZWFzb24gPSAnRGlyZWN0b3J5IHNldCBkaWZmZXJzLicgfQ0KICAgIH0NCiAgICBmb3Jl
::CUAB64:YWNoICgkcmVsYXRpdmUgaW4gJEV4cGVjdGVkLkZpbGVzLktleXMpIHsNCiAgICAgICAgaWYgKC1ub3QgJGFjdHVhbC5GaWxlcy5D
::CUAB64:b250YWluc0tleSgkcmVsYXRpdmUpKSB7DQogICAgICAgICAgICByZXR1cm4gW3BzY3VzdG9tb2JqZWN0XUB7IFBhc3NlZCA9ICRm
::CUAB64:YWxzZTsgUmVhc29uID0gKCdNaXNzaW5nIGZpbGU6ICcgKyAkcmVsYXRpdmUpIH0NCiAgICAgICAgfQ0KICAgICAgICAkYSA9ICRh
::CUAB64:Y3R1YWwuRmlsZXNbJHJlbGF0aXZlXQ0KICAgICAgICAkZSA9ICRFeHBlY3RlZC5GaWxlc1skcmVsYXRpdmVdDQogICAgICAgIGlm
::CUAB64:ICgkYS5MZW5ndGggLW5lICRlLkxlbmd0aCAtb3IgJGEuSGFzaCAtbmUgJGUuSGFzaCkgew0KICAgICAgICAgICAgcmV0dXJuIFtw
::CUAB64:c2N1c3RvbW9iamVjdF1AeyBQYXNzZWQgPSAkZmFsc2U7IFJlYXNvbiA9ICgnRmlsZSBjb250ZW50IGRpZmZlcnM6ICcgKyAkcmVs
::CUAB64:YXRpdmUpIH0NCiAgICAgICAgfQ0KICAgIH0NCiAgICByZXR1cm4gW3BzY3VzdG9tb2JqZWN0XUB7IFBhc3NlZCA9ICR0cnVlOyBS
::CUAB64:ZWFzb24gPSAnQWxsIGZpbGUgcGF0aHMsIGRpcmVjdG9yeSBwYXRocywgbGVuZ3RocyBhbmQgU0hBMjU2IGhhc2hlcyBtYXRjaC4n
::CUAB64:IH0NCn0NCg0KZnVuY3Rpb24gQ29weS1SdW50aW1lVHJlZSB7DQogICAgcGFyYW0oW3N0cmluZ10kU291cmNlLCBbc3RyaW5nXSRU
::CUAB64:YXJnZXQsIFtzdHJpbmddJENvcHlMb2cpDQogICAgW3ZvaWRdW0lPLkRpcmVjdG9yeV06OkNyZWF0ZURpcmVjdG9yeSgkVGFyZ2V0
::CUAB64:KQ0KICAgICR4Y29weSA9IEpvaW4tUGF0aCAkZW52OlN5c3RlbVJvb3QgJ1N5c3RlbTMyXHhjb3B5LmV4ZScNCiAgICBpZiAoLW5v
::CUAB64:dCAoVGVzdC1QYXRoIC1MaXRlcmFsUGF0aCAkeGNvcHkgLVBhdGhUeXBlIExlYWYpKSB7IHRocm93ICdTeXN0ZW0gWENPUFkgaXMg
::CUAB64:dW5hdmFpbGFibGUuJyB9DQogICAgJGFyZ3VtZW50cyA9IEAoKEpvaW4tUGF0aCAkU291cmNlICcqJyksICgkVGFyZ2V0LlRyaW1F
::CUAB64:bmQoJ1wnKSArICdcJyksICcvRScsICcvSScsICcvSCcsICcvWScsICcvUicsICcvRycsICcvUScpDQogICAgJG9sZFByZWZlcmVu
::CUAB64:Y2UgPSAkRXJyb3JBY3Rpb25QcmVmZXJlbmNlDQogICAgJGNvcHlFeGl0ID0gLTENCiAgICB0cnkgew0KICAgICAgICAkRXJyb3JB
::CUAB64:Y3Rpb25QcmVmZXJlbmNlID0gJ0NvbnRpbnVlJw0KICAgICAgICAkb3V0cHV0ID0gQCgmICR4Y29weSBAYXJndW1lbnRzIDI+JjEp
::CUAB64:DQogICAgICAgICRjb3B5RXhpdCA9ICRMQVNURVhJVENPREUNCiAgICB9IGZpbmFsbHkgew0KICAgICAgICAkRXJyb3JBY3Rpb25Q
::CUAB64:cmVmZXJlbmNlID0gJG9sZFByZWZlcmVuY2UNCiAgICB9DQogICAgdHJ5IHsgJG91dHB1dCB8IE91dC1GaWxlIC1MaXRlcmFsUGF0
::CUAB64:aCAkQ29weUxvZyAtRW5jb2RpbmcgVVRGOCB9IGNhdGNoIHsgV3JpdGUtUmVwYWlyTWVzc2FnZSAoJ1hDT1BZIOaXpeW/l+aXoOaz
::CUAB64:leS/neWtmO+8micgKyAkXy5FeGNlcHRpb24uTWVzc2FnZSkgJ1llbGxvdycgfQ0KICAgIFdyaXRlLVJlcGFpck1lc3NhZ2UgKCdY
::CUAB64:Q09QWSBleGl0IGNvZGU6ICcgKyAkY29weUV4aXQpDQogICAgaWYgKCRjb3B5RXhpdCAtbmUgMCkgeyB0aHJvdyAoJ1hDT1BZIGZh
::CUAB64:aWxlZC4gRXhpdCBjb2RlOiB7MH0uIExvZzogezF9JyAtZiAkY29weUV4aXQsICRDb3B5TG9nKSB9DQp9DQoNCmZ1bmN0aW9uIE1v
::CUAB64:dmUtUnVudGltZVBhdGggew0KICAgIHBhcmFtKFtzdHJpbmddJFNvdXJjZSwgW3N0cmluZ10kRGVzdGluYXRpb24sIFtzdHJpbmdd
::CUAB64:JFJ1bnRpbWVSb290KQ0KICAgICRzb3VyY2VQYXRoID0gQXNzZXJ0LUNvbnRhaW5lZFBhdGggJFNvdXJjZSAkUnVudGltZVJvb3QN
::CUAB64:CiAgICAkZGVzdGluYXRpb25QYXRoID0gQXNzZXJ0LUNvbnRhaW5lZFBhdGggJERlc3RpbmF0aW9uICRSdW50aW1lUm9vdA0KICAg
::CUAB64:IEFzc2VydC1Ob1JlcGFyc2VQb2ludCAkUnVudGltZVJvb3QNCiAgICBBc3NlcnQtTm9SZXBhcnNlUG9pbnQgJHNvdXJjZVBhdGgg
::CUAB64:LVRyZWUNCiAgICBpZiAoVGVzdC1QYXRoIC1MaXRlcmFsUGF0aCAkZGVzdGluYXRpb25QYXRoKSB7IHRocm93ICgnTW92ZSBkZXN0
::CUAB64:aW5hdGlvbiBhbHJlYWR5IGV4aXN0czogJyArICRkZXN0aW5hdGlvblBhdGgpIH0NCiAgICAkc291cmNlV2FzRGlyZWN0b3J5ID0g
::CUAB64:KEdldC1JdGVtIC1MaXRlcmFsUGF0aCAkc291cmNlUGF0aCAtRm9yY2UpLlBTSXNDb250YWluZXINCiAgICBNb3ZlLUl0ZW0gLUxp
::CUAB64:dGVyYWxQYXRoICRzb3VyY2VQYXRoIC1EZXN0aW5hdGlvbiAkZGVzdGluYXRpb25QYXRoDQogICAgaWYgKChUZXN0LVBhdGggLUxp
::CUAB64:dGVyYWxQYXRoICRzb3VyY2VQYXRoKSAtb3IgLW5vdCAoVGVzdC1QYXRoIC1MaXRlcmFsUGF0aCAkZGVzdGluYXRpb25QYXRoKSkg
::CUAB64:eyB0aHJvdyAnTW92ZSBwb3N0Y29uZGl0aW9uIGZhaWxlZC4gUGxlYXNlIHJldGFpbiBib3RoIHBhdGhzIGZvciBtYW51YWwgcmVj
::CUAB64:b3ZlcnkuJyB9DQogICAgaWYgKChHZXQtSXRlbSAtTGl0ZXJhbFBhdGggJGRlc3RpbmF0aW9uUGF0aCAtRm9yY2UpLlBTSXNDb250
::CUAB64:YWluZXIgLW5lICRzb3VyY2VXYXNEaXJlY3RvcnkpIHsgdGhyb3cgJ01vdmUgZGVzdGluYXRpb24gaGFzIGFuIHVuZXhwZWN0ZWQg
::CUAB64:aXRlbSB0eXBlLicgfQ0KfQ0KDQpmdW5jdGlvbiBJbnZva2UtUnVudGltZVJlcGFpciB7DQogICAgcGFyYW0oJFNvdXJjZUludmVu
::CUAB64:dG9yeSwgW3N0cmluZ10kVGFyZ2V0LCBbc3RyaW5nXSRSdW50aW1lUm9vdCwgW3N0cmluZ10kUnVuRGlyZWN0b3J5LCBbc2NyaXB0
::CUAB64:YmxvY2tdJENvcHlBY3Rpb24sIFtzdHJpbmddJEluc3RhbGxMb2NhdGlvbikNCiAgICAkdGFyZ2V0UGF0aCA9IEFzc2VydC1Db250
::CUAB64:YWluZWRQYXRoICRUYXJnZXQgJFJ1bnRpbWVSb290DQogICAgaWYgKChTcGxpdC1QYXRoIC1MZWFmICR0YXJnZXRQYXRoKSAtbm90
::CUAB64:bWF0Y2ggJ15bMC05YS1mXXsxNn0kJykgeyB0aHJvdyAnSW52YWxpZCBydW50aW1lIElELicgfQ0KICAgIEFzc2VydC1Ob1JlcGFy
::CUAB64:c2VQb2ludCAkUnVudGltZVJvb3QNCiAgICBBc3NlcnQtTm9SZXBhcnNlUG9pbnQgJHRhcmdldFBhdGggLVRyZWUNCiAgICBpZiAo
::CUAB64:JHRhcmdldFBhdGguRXF1YWxzKCRTb3VyY2VJbnZlbnRvcnkuUm9vdCwgW1N0cmluZ0NvbXBhcmlzb25dOjpPcmRpbmFsSWdub3Jl
::CUAB64:Q2FzZSkpIHsgdGhyb3cgJ1NvdXJjZSBhbmQgdGFyZ2V0IG11c3QgZGlmZmVyLicgfQ0KICAgIGlmICgkdGFyZ2V0UGF0aC5TdGFy
::CUAB64:dHNXaXRoKCRTb3VyY2VJbnZlbnRvcnkuUm9vdCArICdcJywgW1N0cmluZ0NvbXBhcmlzb25dOjpPcmRpbmFsSWdub3JlQ2FzZSkg
::CUAB64:LW9yICRTb3VyY2VJbnZlbnRvcnkuUm9vdC5TdGFydHNXaXRoKCR0YXJnZXRQYXRoICsgJ1wnLCBbU3RyaW5nQ29tcGFyaXNvbl06
::CUAB64:Ok9yZGluYWxJZ25vcmVDYXNlKSkgeyB0aHJvdyAnU291cmNlIGFuZCB0YXJnZXQgbXVzdCBub3QgY29udGFpbiBlYWNoIG90aGVy
::CUAB64:LicgfQ0KICAgICRpbml0aWFsID0gQ29tcGFyZS1UcmVlSW52ZW50b3J5ICRTb3VyY2VJbnZlbnRvcnkgJHRhcmdldFBhdGgNCiAg
::CUAB64:ICBpZiAoJGluaXRpYWwuUGFzc2VkKSB7IHJldHVybiBbcHNjdXN0b21vYmplY3RdQHsgQ2hhbmdlZCA9ICRmYWxzZTsgQmFja3Vw
::CUAB64:ID0gJG51bGw7IFF1YXJhbnRpbmUgPSAkbnVsbCB9IH0NCiAgICBpZiAoJEluc3RhbGxMb2NhdGlvbikgeyBDb25maXJtLUNvZGV4
::CUAB64:U3RvcHBlZCAkSW5zdGFsbExvY2F0aW9uICRSdW50aW1lUm9vdCB9DQogICAgW3ZvaWRdW0lPLkRpcmVjdG9yeV06OkNyZWF0ZURp
::CUAB64:cmVjdG9yeSgkUnVudGltZVJvb3QpDQogICAgJHN1ZmZpeCA9IChHZXQtRGF0ZSAtRm9ybWF0ICd5eXl5TU1kZC1ISG1tc3MnKSAr
::CUAB64:ICctJyArIFtHdWlkXTo6TmV3R3VpZCgpLlRvU3RyaW5nKCdOJykuU3Vic3RyaW5nKDAsIDgpDQogICAgJGlkID0gU3BsaXQtUGF0
::CUAB64:aCAtTGVhZiAkdGFyZ2V0UGF0aA0KICAgICRiYWNrdXAgPSAkbnVsbA0KICAgICRxdWFyYW50aW5lID0gJG51bGwNCiAgICBpZiAo
::CUAB64:VGVzdC1QYXRoIC1MaXRlcmFsUGF0aCAkdGFyZ2V0UGF0aCkgew0KICAgICAgICAkYmFja3VwID0gQXNzZXJ0LUNvbnRhaW5lZFBh
::CUAB64:dGggKEpvaW4tUGF0aCAkUnVudGltZVJvb3QgKCcuYmFja3VwLScgKyAkaWQgKyAnLScgKyAkc3VmZml4KSkgJFJ1bnRpbWVSb290
::CUAB64:DQogICAgICAgIGlmIChUZXN0LVBhdGggLUxpdGVyYWxQYXRoICRiYWNrdXApIHsgdGhyb3cgJ0JhY2t1cCBwYXRoIGFscmVhZHkg
::CUAB64:ZXhpc3RzLicgfQ0KICAgICAgICBXcml0ZS1SZXBhaXJNZXNzYWdlICgn5aSH5Lu95Y6f6L+Q6KGM5pe277yaJyArICRiYWNrdXAp
::CUAB64:DQogICAgICAgIE1vdmUtUnVudGltZVBhdGggJHRhcmdldFBhdGggJGJhY2t1cCAkUnVudGltZVJvb3QNCiAgICB9DQogICAgdHJ5
::CUAB64:IHsNCiAgICAgICAgaWYgKCRJbnN0YWxsTG9jYXRpb24pIHsgQ29uZmlybS1Db2RleFN0b3BwZWQgJEluc3RhbGxMb2NhdGlvbiAk
::CUAB64:UnVudGltZVJvb3QgfQ0KICAgICAgICAkY29weUxvZyA9IEpvaW4tUGF0aCAkUnVuRGlyZWN0b3J5ICdYQ09QWS5sb2cnDQogICAg
::CUAB64:ICAgIGlmICgkQ29weUFjdGlvbikgeyAmICRDb3B5QWN0aW9uICRTb3VyY2VJbnZlbnRvcnkuUm9vdCAkdGFyZ2V0UGF0aCAkY29w
::CUAB64:eUxvZyB9IGVsc2UgeyBDb3B5LVJ1bnRpbWVUcmVlICRTb3VyY2VJbnZlbnRvcnkuUm9vdCAkdGFyZ2V0UGF0aCAkY29weUxvZyB9
::CUAB64:DQogICAgICAgICR2ZXJpZmllZCA9IENvbXBhcmUtVHJlZUludmVudG9yeSAkU291cmNlSW52ZW50b3J5ICR0YXJnZXRQYXRoDQog
::CUAB64:ICAgICAgIGlmICgtbm90ICR2ZXJpZmllZC5QYXNzZWQpIHsgdGhyb3cgKCdWZXJpZmljYXRpb24gZmFpbGVkOiAnICsgJHZlcmlm
::CUAB64:aWVkLlJlYXNvbikgfQ0KICAgICAgICAkc291cmNlTm93ID0gQ29tcGFyZS1UcmVlSW52ZW50b3J5ICRTb3VyY2VJbnZlbnRvcnkg
::CUAB64:JFNvdXJjZUludmVudG9yeS5Sb290DQogICAgICAgIGlmICgtbm90ICRzb3VyY2VOb3cuUGFzc2VkKSB7IHRocm93ICdPZmZpY2lh
::CUAB64:bCBzb3VyY2UgY2hhbmdlZCBkdXJpbmcgcmVwYWlyLiBQbGVhc2UgcnVuIHRoZSB0b29sIGFnYWluIGFmdGVyIHRoZSBhcHAgdXBk
::CUAB64:YXRlIGZpbmlzaGVzLicgfQ0KICAgICAgICByZXR1cm4gW3BzY3VzdG9tb2JqZWN0XUB7IENoYW5nZWQgPSAkdHJ1ZTsgQmFja3Vw
::CUAB64:ID0gJGJhY2t1cDsgUXVhcmFudGluZSA9ICRudWxsIH0NCiAgICB9IGNhdGNoIHsNCiAgICAgICAgJG9yaWdpbmFsRXJyb3IgPSAk
::CUAB64:Xy5FeGNlcHRpb24uTWVzc2FnZQ0KICAgICAgICBXcml0ZS1SZXBhaXJNZXNzYWdlICgn5L+u5aSN5aSx6LSl77yaJyArICRvcmln
::CUAB64:aW5hbEVycm9yKSAnUmVkJw0KICAgICAgICB0cnkgew0KICAgICAgICAgICAgaWYgKFRlc3QtUGF0aCAtTGl0ZXJhbFBhdGggJHRh
::CUAB64:cmdldFBhdGgpIHsNCiAgICAgICAgICAgICAgICAkcXVhcmFudGluZSA9IEFzc2VydC1Db250YWluZWRQYXRoIChKb2luLVBhdGgg
::CUAB64:JFJ1bnRpbWVSb290ICgnLmZhaWxlZC0nICsgJGlkICsgJy0nICsgJHN1ZmZpeCkpICRSdW50aW1lUm9vdA0KICAgICAgICAgICAg
::CUAB64:ICAgIGlmIChUZXN0LVBhdGggLUxpdGVyYWxQYXRoICRxdWFyYW50aW5lKSB7IHRocm93ICdRdWFyYW50aW5lIHBhdGggYWxyZWFk
::CUAB64:eSBleGlzdHMuJyB9DQogICAgICAgICAgICAgICAgQXNzZXJ0LU5vUmVwYXJzZVBvaW50ICR0YXJnZXRQYXRoIC1UcmVlDQogICAg
::CUAB64:ICAgICAgICAgICAgTW92ZS1SdW50aW1lUGF0aCAkdGFyZ2V0UGF0aCAkcXVhcmFudGluZSAkUnVudGltZVJvb3QNCiAgICAgICAg
::CUAB64:ICAgICAgICBXcml0ZS1SZXBhaXJNZXNzYWdlICgn5L+d55WZ5pyq5a6M5oiQ5paH5Lu277yaJyArICRxdWFyYW50aW5lKQ0KICAg
::CUAB64:ICAgICAgICAgfQ0KICAgICAgICAgICAgaWYgKCRiYWNrdXApIHsNCiAgICAgICAgICAgICAgICBBc3NlcnQtTm9SZXBhcnNlUG9p
::CUAB64:bnQgJGJhY2t1cCAtVHJlZQ0KICAgICAgICAgICAgICAgIFt2b2lkXShBc3NlcnQtQ29udGFpbmVkUGF0aCAkYmFja3VwICRSdW50
::CUAB64:aW1lUm9vdCkNCiAgICAgICAgICAgICAgICBNb3ZlLVJ1bnRpbWVQYXRoICRiYWNrdXAgJHRhcmdldFBhdGggJFJ1bnRpbWVSb290
::CUAB64:DQogICAgICAgICAgICAgICAgV3JpdGUtUmVwYWlyTWVzc2FnZSAn5Y6f6L+Q6KGM5pe25bey5oGi5aSN44CCJyAnWWVsbG93Jw0K
::CUAB64:ICAgICAgICAgICAgfQ0KICAgICAgICB9IGNhdGNoIHsNCiAgICAgICAgICAgIHRocm93ICgnUkVQQUlSX0FORF9ST0xMQkFDS19G
::CUAB64:QUlMRUQ6IHswfTsgcm9sbGJhY2s6IHsxfTsgYmFja3VwOiB7Mn07IGluY29tcGxldGUgdGFyZ2V0OiB7M30nIC1mICRvcmlnaW5h
::CUAB64:bEVycm9yLCAkXy5FeGNlcHRpb24uTWVzc2FnZSwgJGJhY2t1cCwgJHRhcmdldFBhdGgpDQogICAgICAgIH0NCiAgICAgICAgdGhy
::CUAB64:b3cgKCdSRVBBSVJfRkFJTEVEOiAnICsgJG9yaWdpbmFsRXJyb3IpDQogICAgfQ0KfQ0KDQpmdW5jdGlvbiBHZXQtQ29kZXhQcm9j
::CUAB64:ZXNzZXMgew0KICAgIHBhcmFtKFtzdHJpbmddJEluc3RhbGxMb2NhdGlvbiwgW3N0cmluZ10kUnVudGltZVJvb3QpDQogICAgJHBh
::CUAB64:Y2thZ2VSb290ID0gR2V0LUZ1bGxEaXJlY3RvcnlQYXRoICRJbnN0YWxsTG9jYXRpb24NCiAgICAkY2FjaGVSb290ID0gR2V0LUZ1
::CUAB64:bGxEaXJlY3RvcnlQYXRoICRSdW50aW1lUm9vdA0KICAgIGZvcmVhY2ggKCRwcm9jZXNzIGluIEAoR2V0LVByb2Nlc3MgLU5hbWUg
::CUAB64:Q2hhdEdQVCxjb2RleCxjdWFfbm9kZSxub2RlLG5vZGVfcmVwbCxjdWEtaGVscGVyIC1FcnJvckFjdGlvbiBTaWxlbnRseUNvbnRp
::CUAB64:bnVlKSkgew0KICAgICAgICAkcGF0aCA9ICRudWxsDQogICAgICAgIHRyeSB7ICRwYXRoID0gJHByb2Nlc3MuUGF0aCB9IGNhdGNo
::CUAB64:IHsgfQ0KICAgICAgICBpZiAoJHBhdGgpIHsNCiAgICAgICAgICAgIGlmICgkcGF0aC5TdGFydHNXaXRoKCRwYWNrYWdlUm9vdCAr
::CUAB64:ICdcJywgW1N0cmluZ0NvbXBhcmlzb25dOjpPcmRpbmFsSWdub3JlQ2FzZSkgLW9yICRwYXRoLlN0YXJ0c1dpdGgoJGNhY2hlUm9v
::CUAB64:dCArICdcJywgW1N0cmluZ0NvbXBhcmlzb25dOjpPcmRpbmFsSWdub3JlQ2FzZSkgLW9yICRwYXRoIC1tYXRjaCAnXFxXaW5kb3dz
::CUAB64:QXBwc1xcT3BlbkFJXC5Db2RleF9bXlxcXStcXCcpIHsNCiAgICAgICAgICAgICAgICAkcHJvY2Vzcw0KICAgICAgICAgICAgfQ0K
::CUAB64:ICAgICAgICB9IGVsc2Ugew0KICAgICAgICAgICAgJHByb2Nlc3MNCiAgICAgICAgfQ0KICAgIH0NCn0NCg0KZnVuY3Rpb24gQ29u
::CUAB64:ZmlybS1Db2RleFN0b3BwZWQgew0KICAgIHBhcmFtKFtzdHJpbmddJEluc3RhbGxMb2NhdGlvbiwgW3N0cmluZ10kUnVudGltZVJv
::CUAB64:b3QpDQogICAgaWYgKEAoR2V0LUNvZGV4UHJvY2Vzc2VzICRJbnN0YWxsTG9jYXRpb24gJFJ1bnRpbWVSb290KS5Db3VudCAtZ3Qg
::CUAB64:MCkgeyB0aHJvdyAnQ09ERVhfU1RJTExfUlVOTklORzogQ29kZXggcmVvcGVuZWQgb3IgYSBydW50aW1lIHByb2Nlc3MgaXMgc3Rp
::CUAB64:bGwgcnVubmluZy4gRXhpdCBpdCBhbmQgcnVuIHRoZSB0b29sIGFnYWluLicgfQ0KfQ0KDQpmdW5jdGlvbiBXYWl0LUNvZGV4RXhp
::CUAB64:dCB7DQogICAgcGFyYW0oW3N0cmluZ10kSW5zdGFsbExvY2F0aW9uLCBbc3RyaW5nXSRSdW50aW1lUm9vdCwgW2ludF0kVGltZW91
::CUAB64:dFNlY29uZHMgPSAxODApDQogICAgJHJ1bm5pbmcgPSBAKEdldC1Db2RleFByb2Nlc3NlcyAkSW5zdGFsbExvY2F0aW9uICRSdW50
::CUAB64:aW1lUm9vdCkNCiAgICBpZiAoJHJ1bm5pbmcuQ291bnQgLWVxIDApIHsgcmV0dXJuICRmYWxzZSB9DQogICAgV3JpdGUtUmVwYWly
::CUAB64:TWVzc2FnZSAoJ+ajgOa1i+WIsOato+WcqOi/kOihjOeahCBDb2RleCDnm7jlhbPov5vnqIvvvJonICsgKCgkcnVubmluZyB8IEZv
::CUAB64:ckVhY2gtT2JqZWN0IHsgJ3swfSh7MX0pJyAtZiAkXy5Qcm9jZXNzTmFtZSwgJF8uSWQgfSkgLWpvaW4gJywgJykpICdZZWxsb3cn
::CUAB64:DQogICAgV3JpdGUtUmVwYWlyTWVzc2FnZSAn6K+35L+d5a2Y5pyq5a6M5oiQ5bel5L2c77yM54S25ZCO5LuO57O757uf5omY55uY
::CUAB64:5b275bqV6YCA5Ye6IENvZGV444CC6YCA5Ye65ZCO5pys56qX5Y+j5Lya6Ieq5Yqo57un57ut44CCJyAnWWVsbG93Jw0KICAgICR3
::CUAB64:YXRjaCA9IFtEaWFnbm9zdGljcy5TdG9wd2F0Y2hdOjpTdGFydE5ldygpDQogICAgd2hpbGUgKEAoR2V0LUNvZGV4UHJvY2Vzc2Vz
::CUAB64:ICRJbnN0YWxsTG9jYXRpb24gJFJ1bnRpbWVSb290KS5Db3VudCAtZ3QgMCkgew0KICAgICAgICBpZiAoJHdhdGNoLkVsYXBzZWQu
::CUAB64:VG90YWxTZWNvbmRzIC1nZSAkVGltZW91dFNlY29uZHMpIHsgdGhyb3cgJ0NPREVYX1NUSUxMX1JVTk5JTkc6IFdhaXRlZCAxODAg
::CUAB64:c2Vjb25kcy4gRXhpdCBDb2RleCBhbmQgZG91YmxlLWNsaWNrIHRoaXMgZmlsZSBhZ2Fpbi4nIH0NCiAgICAgICAgU3RhcnQtU2xl
::CUAB64:ZXAgLVNlY29uZHMgMQ0KICAgIH0NCiAgICBXcml0ZS1SZXBhaXJNZXNzYWdlICdDb2RleCDlt7LpgIDlh7rvvIznu6fnu63kv67l
::CUAB64:pI3jgIInICdHcmVlbicNCiAgICByZXR1cm4gJHRydWUNCn0NCg0KZnVuY3Rpb24gR2V0LUN1cnJlbnRQYWNrYWdlIHsNCiAgICAk
::CUAB64:cGFja2FnZXMgPSBAKEdldC1BcHB4UGFja2FnZSAtTmFtZSBPcGVuQUkuQ29kZXgpDQogICAgaWYgKCRwYWNrYWdlcy5Db3VudCAt
::CUAB64:bmUgMSkgeyB0aHJvdyAoJ0V4cGVjdGVkIG9uZSByZWdpc3RlcmVkIE9wZW5BSS5Db2RleCBwYWNrYWdlOyBmb3VuZCAnICsgJHBh
::CUAB64:Y2thZ2VzLkNvdW50KSB9DQogICAgaWYgKC1ub3QgJHBhY2thZ2VzWzBdLkluc3RhbGxMb2NhdGlvbikgeyB0aHJvdyAnVGhlIHJl
::CUAB64:Z2lzdGVyZWQgcGFja2FnZSBoYXMgbm8gaW5zdGFsbGF0aW9uIGRpcmVjdG9yeS4nIH0NCiAgICByZXR1cm4gJHBhY2thZ2VzWzBd
::CUAB64:DQp9DQoNCmZ1bmN0aW9uIEdldC1BcHBTdGFydHVwQ29kZSB7DQogICAgcGFyYW0oW3N0cmluZ10kSW5zdGFsbExvY2F0aW9uKQ0K
::CUAB64:ICAgICRhc2FyUGF0aCA9IEpvaW4tUGF0aCAkSW5zdGFsbExvY2F0aW9uICdhcHBccmVzb3VyY2VzXGFwcC5hc2FyJw0KICAgICRz
::CUAB64:dHJlYW0gPSAkbnVsbA0KICAgICRyZWFkZXIgPSAkbnVsbA0KICAgIHRyeSB7DQogICAgICAgICRzdHJlYW0gPSBbSU8uRmlsZV06
::CUAB64:Ok9wZW5SZWFkKCRhc2FyUGF0aCkNCiAgICAgICAgJHJlYWRlciA9IFtJTy5CaW5hcnlSZWFkZXJdOjpuZXcoJHN0cmVhbSkNCiAg
::CUAB64:ICAgICAgaWYgKCRyZWFkZXIuUmVhZFVJbnQzMigpIC1uZSA0KSB7IHRocm93ICdVbnN1cHBvcnRlZCBBU0FSIGhlYWRlci4nIH0N
::CUAB64:CiAgICAgICAgW2xvbmddJGhlYWRlclNpemUgPSAkcmVhZGVyLlJlYWRVSW50MzIoKQ0KICAgICAgICBbdm9pZF0kcmVhZGVyLlJl
::CUAB64:YWRVSW50MzIoKQ0KICAgICAgICBbaW50XSRqc29uU2l6ZSA9ICRyZWFkZXIuUmVhZFVJbnQzMigpDQogICAgICAgIGlmICgkaGVh
::CUAB64:ZGVyU2l6ZSAtbHQgOCAtb3IgJGhlYWRlclNpemUgLWd0IDE2Nzc3MjE2IC1vciAkanNvblNpemUgLWxlIDAgLW9yICRqc29uU2l6
::CUAB64:ZSAtZ3QgKCRoZWFkZXJTaXplIC0gOCkpIHsgdGhyb3cgJ0ludmFsaWQgQVNBUiBoZWFkZXIgc2l6ZS4nIH0NCiAgICAgICAgJGpz
::CUAB64:b25CeXRlcyA9ICRyZWFkZXIuUmVhZEJ5dGVzKCRqc29uU2l6ZSkNCiAgICAgICAgaWYgKCRqc29uQnl0ZXMuTGVuZ3RoIC1uZSAk
::CUAB64:anNvblNpemUpIHsgdGhyb3cgJ1RydW5jYXRlZCBBU0FSIGhlYWRlci4nIH0NCiAgICAgICAgJGhlYWRlciA9IFtUZXh0LkVuY29k
::CUAB64:aW5nXTo6VVRGOC5HZXRTdHJpbmcoJGpzb25CeXRlcykgfCBDb252ZXJ0RnJvbS1Kc29uDQogICAgICAgICR2aXRlID0gJGhlYWRl
::CUAB64:ci5maWxlcy5QU09iamVjdC5Qcm9wZXJ0aWVzWycudml0ZSddLlZhbHVlDQogICAgICAgICRidWlsZCA9ICR2aXRlLmZpbGVzLlBT
::CUAB64:T2JqZWN0LlByb3BlcnRpZXNbJ2J1aWxkJ10uVmFsdWUNCiAgICAgICAgJGVudHJpZXMgPSBAKCRidWlsZC5maWxlcy5QU09iamVj
::CUAB64:dC5Qcm9wZXJ0aWVzIHwgV2hlcmUtT2JqZWN0IHsgJF8uTmFtZSAtbWF0Y2ggJ15hcHBsaWNhdGlvbi1uZXR3b3JrLXN0YXJ0dXAt
::CUAB64:LipcLmpzJCcgfSkNCiAgICAgICAgaWYgKCRlbnRyaWVzLkNvdW50IC1uZSAxKSB7IHRocm93ICdDYW5ub3QgdW5pcXVlbHkgaWRl
::CUAB64:bnRpZnkgdGhlIGN1cnJlbnQgcnVudGltZSBpbXBsZW1lbnRhdGlvbi4nIH0NCiAgICAgICAgJGVudHJ5ID0gJGVudHJpZXNbMF0u
::CUAB64:VmFsdWUNCiAgICAgICAgW2xvbmddJHNpemUgPSAkZW50cnkuc2l6ZQ0KICAgICAgICBpZiAoJHNpemUgLWxlIDAgLW9yICRzaXpl
::CUAB64:IC1ndCA4Mzg4NjA4KSB7IHRocm93ICdVbnN1cHBvcnRlZCBydW50aW1lIGltcGxlbWVudGF0aW9uIHNpemUuJyB9DQogICAgICAg
::CUAB64:ICR1bnBhY2tlZFByb3BlcnR5ID0gJGVudHJ5LlBTT2JqZWN0LlByb3BlcnRpZXNbJ3VucGFja2VkJ10NCiAgICAgICAgaWYgKCR1
::CUAB64:bnBhY2tlZFByb3BlcnR5IC1hbmQgJHVucGFja2VkUHJvcGVydHkuVmFsdWUpIHsNCiAgICAgICAgICAgICR1bnBhY2tlZFJvb3Qg
::CUAB64:PSAkYXNhclBhdGggKyAnLnVucGFja2VkJw0KICAgICAgICAgICAgJHVucGFja2VkRmlsZSA9IEFzc2VydC1Db250YWluZWRQYXRo
::CUAB64:IChKb2luLVBhdGggJHVucGFja2VkUm9vdCAoJy52aXRlXGJ1aWxkXCcgKyAkZW50cmllc1swXS5OYW1lKSkgJHVucGFja2VkUm9v
::CUAB64:dA0KICAgICAgICAgICAgcmV0dXJuIFtJTy5GaWxlXTo6UmVhZEFsbFRleHQoJHVucGFja2VkRmlsZSwgW1RleHQuRW5jb2Rpbmdd
::CUAB64:OjpVVEY4KQ0KICAgICAgICB9DQogICAgICAgIGlmIChbc3RyaW5nXSRlbnRyeS5vZmZzZXQgLW5vdG1hdGNoICdeXGQrJCcpIHsg
::CUAB64:dGhyb3cgJ0ludmFsaWQgQVNBUiBlbnRyeSBvZmZzZXQuJyB9DQogICAgICAgIFtsb25nXSRvZmZzZXQgPSA4ICsgJGhlYWRlclNp
::CUAB64:emUgKyBbbG9uZ10kZW50cnkub2Zmc2V0DQogICAgICAgIGlmICgkb2Zmc2V0IC1sdCAwIC1vciAoJG9mZnNldCArICRzaXplKSAt
::CUAB64:Z3QgJHN0cmVhbS5MZW5ndGgpIHsgdGhyb3cgJ0FTQVIgZW50cnkgaXMgb3V0c2lkZSB0aGUgYXJjaGl2ZS4nIH0NCiAgICAgICAg
::CUAB64:JHN0cmVhbS5Qb3NpdGlvbiA9ICRvZmZzZXQNCiAgICAgICAgJGJ5dGVzID0gJHJlYWRlci5SZWFkQnl0ZXMoW2ludF0kc2l6ZSkN
::CUAB64:CiAgICAgICAgaWYgKCRieXRlcy5MZW5ndGggLW5lICRzaXplKSB7IHRocm93ICdUcnVuY2F0ZWQgcnVudGltZSBpbXBsZW1lbnRh
::CUAB64:dGlvbi4nIH0NCiAgICAgICAgcmV0dXJuIFtUZXh0LkVuY29kaW5nXTo6VVRGOC5HZXRTdHJpbmcoJGJ5dGVzKQ0KICAgIH0gZmlu
::CUAB64:YWxseSB7DQogICAgICAgIGlmICgkcmVhZGVyKSB7ICRyZWFkZXIuRGlzcG9zZSgpIH0gZWxzZWlmICgkc3RyZWFtKSB7ICRzdHJl
::CUAB64:YW0uRGlzcG9zZSgpIH0NCiAgICB9DQp9DQoNCmZ1bmN0aW9uIEFzc2VydC1Lbm93blJ1bnRpbWVBbGdvcml0aG0gew0KICAgIHBh
::CUAB64:cmFtKFtzdHJpbmddJENvZGUpDQogICAgJHF1b3RlID0gJ1tceDYwXHgyMlx4MjddJw0KICAgICRtYXJrZXJzID0gJ1xbXHMqJyAr
::CUAB64:ICRxdW90ZSArICdtYW5pZmVzdFwuanNvbicgKyAkcXVvdGUgKyAnXHMqLFxzKicgKyAkcXVvdGUgKyAnYmluL25vZGVcLmV4ZScg
::CUAB64:KyAkcXVvdGUgKyAnXHMqLFxzKicgKyAkcXVvdGUgKyAnYmluL25vZGVfcmVwbFwuZXhlJyArICRxdW90ZSArICdccypcXScNCiAg
::CUAB64:ICAkaGFzaEFsZ29yaXRobSA9ICdmdW5jdGlvblxzKyg/PG5hbWU+WyRcd10rKVwoKD88bGlzdD5bJFx3XSspXClce2xldFxzKyg/
::CUAB64:PGhhc2g+WyRcd10rKT1cKDAsWyRcd10rXC5jcmVhdGVIYXNoXClcKCcgKyAkcXVvdGUgKyAnc2hhMjU2JyArICRxdW90ZSArICdc
::CUAB64:KTtmb3JcKGxldFxzKyg/PGl0ZW0+WyRcd10rKVxzK29mXHMrXGs8bGlzdD5cKVxrPGhhc2g+XC51cGRhdGVcKFxrPGl0ZW0+XC5l
::CUAB64:eGVjdXRhYmxlTmFtZVwpLFxrPGhhc2g+XC51cGRhdGVcKCcgKyAkcXVvdGUgKyAnXFwwJyArICRxdW90ZSArICdcKSxcazxoYXNo
::CUAB64:PlwudXBkYXRlXChcazxpdGVtPlwuZGlnZXN0XCksXGs8aGFzaD5cLnVwZGF0ZVwoJyArICRxdW90ZSArICdcXDAnICsgJHF1b3Rl
::CUAB64:ICsgJ1wpO3JldHVyblxzK1xrPGhhc2g+XC5kaWdlc3RcKCcgKyAkcXVvdGUgKyAnaGV4JyArICRxdW90ZSArICdcKVx9Jw0KICAg
::CUAB64:ICRtYXRjaCA9IFtyZWdleF06Ok1hdGNoKCRDb2RlLCAkaGFzaEFsZ29yaXRobSkNCiAgICBpZiAoLW5vdCBbcmVnZXhdOjpJc01h
::CUAB64:dGNoKCRDb2RlLCAkbWFya2VycykgLW9yIC1ub3QgJG1hdGNoLlN1Y2Nlc3MpIHsgdGhyb3cgJ1RoaXMgYXBwIHZlcnNpb24gY2hh
::CUAB64:bmdlZCBpdHMgcnVudGltZSBpZGVudGl0eSBhbGdvcml0aG0uIFRoZSB0b29sIHN0b3BwZWQgd2l0aG91dCBtb2RpZnlpbmcgdGhl
::CUAB64:IHJ1bnRpbWUuJyB9DQogICAgJHNsaWNlID0gW3JlZ2V4XTo6RXNjYXBlKCRtYXRjaC5Hcm91cHNbJ25hbWUnXS5WYWx1ZSkgKyAn
::CUAB64:XChbJFx3XStcKVwuc2xpY2VcKDAsMTZcKScNCiAgICAkZmlsZUhhc2ggPSAnY3JlYXRlSGFzaFwpXCgnICsgJHF1b3RlICsgJ3No
::CUAB64:YTI1NicgKyAkcXVvdGUgKyAnXClcLnVwZGF0ZVwoW1xzXFNdezAsNDAwfT9yZWFkRmlsZVN5bmNcKVtcc1xTXXswLDEwMH0/XC5k
::CUAB64:aWdlc3RcKCcgKyAkcXVvdGUgKyAnaGV4JyArICRxdW90ZSArICdcKScNCiAgICBpZiAoLW5vdCBbcmVnZXhdOjpJc01hdGNoKCRD
::CUAB64:b2RlLCAkc2xpY2UpIC1vciAtbm90IFtyZWdleF06OklzTWF0Y2goJENvZGUsICRmaWxlSGFzaCkpIHsgdGhyb3cgJ1VucmVjb2du
::CUAB64:aXplZCBydW50aW1lIGhhc2ggY2FsY3VsYXRpb24uIFRoZSB0b29sIHN0b3BwZWQgd2l0aG91dCBtb2RpZnlpbmcgdGhlIHJ1bnRp
::CUAB64:bWUuJyB9DQp9DQoNCmZ1bmN0aW9uIEdldC1SdW50aW1lSWRGcm9tU291cmNlIHsNCiAgICBwYXJhbShbc3RyaW5nXSRTb3VyY2Up
::CUAB64:DQogICAgJGNvbWJpbmVkID0gW1RleHQuU3RyaW5nQnVpbGRlcl06Om5ldygpDQogICAgZm9yZWFjaCAoJHJlbGF0aXZlIGluIEAo
::CUAB64:J21hbmlmZXN0Lmpzb24nLCAnYmluL25vZGUuZXhlJywgJ2Jpbi9ub2RlX3JlcGwuZXhlJykpIHsNCiAgICAgICAgJGZpbGVQYXRo
::CUAB64:ID0gSm9pbi1QYXRoICRTb3VyY2UgJHJlbGF0aXZlDQogICAgICAgIFt2b2lkXSRjb21iaW5lZC5BcHBlbmQoJHJlbGF0aXZlKQ0K
::CUAB64:ICAgICAgICBbdm9pZF0kY29tYmluZWQuQXBwZW5kKFtjaGFyXTApDQogICAgICAgIFt2b2lkXSRjb21iaW5lZC5BcHBlbmQoKEdl
::CUAB64:dC1TaGEyNTYgJGZpbGVQYXRoKSkNCiAgICAgICAgW3ZvaWRdJGNvbWJpbmVkLkFwcGVuZChbY2hhcl0wKQ0KICAgIH0NCiAgICAk
::CUAB64:c2hhID0gW1NlY3VyaXR5LkNyeXB0b2dyYXBoeS5TSEEyNTZdOjpDcmVhdGUoKQ0KICAgIHRyeSB7DQogICAgICAgICRkaWdlc3Qg
::CUAB64:PSAkc2hhLkNvbXB1dGVIYXNoKFtUZXh0LkVuY29kaW5nXTo6VVRGOC5HZXRCeXRlcygkY29tYmluZWQuVG9TdHJpbmcoKSkpDQog
::CUAB64:ICAgICAgIHJldHVybiBbQml0Q29udmVydGVyXTo6VG9TdHJpbmcoJGRpZ2VzdCkuUmVwbGFjZSgnLScsICcnKS5Ub0xvd2VySW52
::CUAB64:YXJpYW50KCkuU3Vic3RyaW5nKDAsIDE2KQ0KICAgIH0gZmluYWxseSB7ICRzaGEuRGlzcG9zZSgpIH0NCn0NCg0KZnVuY3Rpb24g
::CUAB64:R2V0LVJlcXVpcmVkUnVudGltZUlkIHsNCiAgICBwYXJhbShbc3RyaW5nXSRTb3VyY2UsIFtzdHJpbmddJEluc3RhbGxMb2NhdGlv
::CUAB64:bikNCiAgICBBc3NlcnQtS25vd25SdW50aW1lQWxnb3JpdGhtIChHZXQtQXBwU3RhcnR1cENvZGUgJEluc3RhbGxMb2NhdGlvbikN
::CUAB64:CiAgICByZXR1cm4gR2V0LVJ1bnRpbWVJZEZyb21Tb3VyY2UgJFNvdXJjZQ0KfQ0KDQpmdW5jdGlvbiBJbnZva2UtUmVwYWlyTWFp
::CUAB64:biB7DQogICAgJG11dGV4ID0gJG51bGwNCiAgICAkYWNxdWlyZWQgPSAkZmFsc2UNCiAgICB0cnkgew0KICAgICAgICBbQ29uc29s
::CUAB64:ZV06Ok91dHB1dEVuY29kaW5nID0gW1RleHQuVVRGOEVuY29kaW5nXTo6bmV3KCRmYWxzZSkNCiAgICAgICAgJHNpZCA9IFtTZWN1
::CUAB64:cml0eS5QcmluY2lwYWwuV2luZG93c0lkZW50aXR5XTo6R2V0Q3VycmVudCgpLlVzZXIuVmFsdWUNCiAgICAgICAgJG11dGV4ID0g
::CUAB64:W1RocmVhZGluZy5NdXRleF06Om5ldygkZmFsc2UsICgnTG9jYWxcQ29kZXhDVUFSZXBhaXItJyArICRzaWQpKQ0KICAgICAgICB0
::CUAB64:cnkgeyAkYWNxdWlyZWQgPSAkbXV0ZXguV2FpdE9uZSgwKSB9IGNhdGNoIFtUaHJlYWRpbmcuQWJhbmRvbmVkTXV0ZXhFeGNlcHRp
::CUAB64:b25dIHsgJGFjcXVpcmVkID0gJHRydWUgfQ0KICAgICAgICBpZiAoLW5vdCAkYWNxdWlyZWQpIHsgV3JpdGUtSG9zdCAn5bey5pyJ
::CUAB64:5Y+m5LiA5Liq5L+u5aSN56qX5Y+j5Zyo6L+Q6KGM77yM6K+35L2/55So6YKj5Liq56qX5Y+j44CCJzsgcmV0dXJuIDEwIH0NCiAg
::CUAB64:ICAgICAgJHJ1bk5hbWUgPSAoR2V0LURhdGUgLUZvcm1hdCAneXl5eU1NZGQtSEhtbXNzJykgKyAnLScgKyBbR3VpZF06Ok5ld0d1
::CUAB64:aWQoKS5Ub1N0cmluZygnTicpLlN1YnN0cmluZygwLCA4KQ0KICAgICAgICAkcmVwb3J0Um9vdCA9IGlmIChbc3RyaW5nXTo6SXNO
::CUAB64:dWxsT3JXaGl0ZVNwYWNlKCRlbnY6Q09ERVhfQ1VBX0ZJWF9SRVBPUlRfUk9PVCkpIHsgJ0Q6XENvZGV4XFRlbXBcY29kZXgtY3Vh
::CUAB64:LXJlY292ZXJ5JyB9IGVsc2UgeyAkZW52OkNPREVYX0NVQV9GSVhfUkVQT1JUX1JPT1QgfQ0KICAgICAgICAkcnVuRGlyZWN0b3J5
::CUAB64:ID0gSm9pbi1QYXRoICRyZXBvcnRSb290ICRydW5OYW1lDQogICAgICAgIFt2b2lkXVtJTy5EaXJlY3RvcnldOjpDcmVhdGVEaXJl
::CUAB64:Y3RvcnkoJHJ1bkRpcmVjdG9yeSkNCiAgICAgICAgJHNjcmlwdDpMb2dGaWxlID0gSm9pbi1QYXRoICRydW5EaXJlY3RvcnkgJ1Jl
::CUAB64:cGFpcl9SZXBvcnQudHh0Jw0KICAgICAgICBXcml0ZS1SZXBhaXJNZXNzYWdlICdDb2RleCBDVUEg6L+Q6KGM5pe25qOA5p+lIC8g
::CUAB64:5L+u5aSN5bel5YW3JyAnQ3lhbicNCiAgICAgICAgV3JpdGUtUmVwYWlyTWVzc2FnZSAoJ+aXpeW/l+ebruW9le+8micgKyAkcnVu
::CUAB64:RGlyZWN0b3J5KQ0KICAgICAgICAkcGFja2FnZSA9IEdldC1DdXJyZW50UGFja2FnZQ0KICAgICAgICAkaW5zdGFsbCA9IEdldC1G
::CUAB64:dWxsRGlyZWN0b3J5UGF0aCAkcGFja2FnZS5JbnN0YWxsTG9jYXRpb24NCiAgICAgICAgJHNvdXJjZSA9IEFzc2VydC1Db250YWlu
::CUAB64:ZWRQYXRoIChKb2luLVBhdGggJGluc3RhbGwgJ2FwcFxyZXNvdXJjZXNcY3VhX25vZGUnKSAkaW5zdGFsbA0KICAgICAgICBpZiAo
::CUAB64:LW5vdCAoVGVzdC1QYXRoIC1MaXRlcmFsUGF0aCAkc291cmNlIC1QYXRoVHlwZSBDb250YWluZXIpKSB7IHRocm93ICdUaGUgb2Zm
::CUAB64:aWNpYWwgY3VhX25vZGUgc291cmNlIGlzIG1pc3NpbmcuIFRoaXMgYXBwIHZlcnNpb24gaXMgbm90IHN1cHBvcnRlZCBieSB0aGlz
::CUAB64:IHRvb2wuJyB9DQogICAgICAgIFdyaXRlLVJlcGFpck1lc3NhZ2UgKCdDb2RleCB2ZXJzaW9uOiAnICsgJHBhY2thZ2UuVmVyc2lv
::CUAB64:bikNCiAgICAgICAgV3JpdGUtUmVwYWlyTWVzc2FnZSAoJ1NvdXJjZTogJyArICRzb3VyY2UpDQogICAgICAgICRydW50aW1lSWQg
::CUAB64:PSBHZXQtUmVxdWlyZWRSdW50aW1lSWQgJHNvdXJjZSAkaW5zdGFsbA0KICAgICAgICBpZiAoJHJ1bnRpbWVJZCAtbm90bWF0Y2gg
::CUAB64:J15bMC05YS1mXXsxNn0kJykgeyB0aHJvdyAnQ2Fubm90IGlkZW50aWZ5IHRoZSBjdXJyZW50IHJlcXVpcmVkIHJ1bnRpbWUgSUQg
::CUAB64:c2FmZWx5LicgfQ0KICAgICAgICAkcnVudGltZVJvb3QgPSBKb2luLVBhdGggJGVudjpMT0NBTEFQUERBVEEgJ09wZW5BSVxDb2Rl
::CUAB64:eFxydW50aW1lc1xjdWFfbm9kZScNCiAgICAgICAgQXNzZXJ0LVNhZmVSdW50aW1lUm9vdCAkcnVudGltZVJvb3QgJGVudjpMT0NB
::CUAB64:TEFQUERBVEENCiAgICAgICAgJHRhcmdldCA9IEFzc2VydC1Db250YWluZWRQYXRoIChKb2luLVBhdGggJHJ1bnRpbWVSb290ICRy
::CUAB64:dW50aW1lSWQpICRydW50aW1lUm9vdA0KICAgICAgICBXcml0ZS1SZXBhaXJNZXNzYWdlICgnUnVudGltZSBJRDogJyArICRydW50
::CUAB64:aW1lSWQpDQogICAgICAgIFdyaXRlLVJlcGFpck1lc3NhZ2UgKCdUYXJnZXQ6ICcgKyAkdGFyZ2V0KQ0KICAgICAgICBmb3JlYWNo
::CUAB64:ICgkcmVxdWlyZWQgaW4gQCgnbWFuaWZlc3QuanNvbicsICdiaW5cbm9kZS5leGUnLCAnYmluXG5vZGVfcmVwbC5leGUnKSkgew0K
::CUAB64:ICAgICAgICAgICAgaWYgKC1ub3QgKFRlc3QtUGF0aCAtTGl0ZXJhbFBhdGggKEpvaW4tUGF0aCAkc291cmNlICRyZXF1aXJlZCkg
::CUAB64:LVBhdGhUeXBlIExlYWYpKSB7IHRocm93ICgnT2ZmaWNpYWwgc291cmNlIGxhY2tzIHJlcXVpcmVkIGZpbGU6ICcgKyAkcmVxdWly
::CUAB64:ZWQpIH0NCiAgICAgICAgfQ0KICAgICAgICBXcml0ZS1SZXBhaXJNZXNzYWdlICfmraPlnKjpgJDmlofku7borqHnrpcgU0hBLTI1
::CUAB64:Nu+8jOajgOafpeWumOaWuea6kOWSjOeOsOaciei/kOihjOaXtu+8jOivt+eojeWAmeKApuKApicgJ0N5YW4nDQogICAgICAgICRp
::CUAB64:bnZlbnRvcnkgPSBHZXQtVHJlZUludmVudG9yeSAkc291cmNlDQogICAgICAgIFdyaXRlLVJlcGFpck1lc3NhZ2UgKCdTb3VyY2Ug
::CUAB64:ZmlsZXM6IHswfTsgU291cmNlIGJ5dGVzOiB7MX0nIC1mICRpbnZlbnRvcnkuQ291bnQsICRpbnZlbnRvcnkuQnl0ZXMpDQogICAg
::CUAB64:ICAgICRzdGF0ZSA9IENvbXBhcmUtVHJlZUludmVudG9yeSAkaW52ZW50b3J5ICR0YXJnZXQNCiAgICAgICAgaWYgKCRzdGF0ZS5Q
::CUAB64:YXNzZWQpIHsNCiAgICAgICAgICAgIFdyaXRlLVJlcGFpck1lc3NhZ2UgJ1JFU1VMVDogUEFTUyDigJQg5b2T5YmN6L+Q6KGM5pe2
::CUAB64:5a6M5pW077yM5peg6ZyA5L+u5aSN44CCJyAnR3JlZW4nDQogICAgICAgICAgICByZXR1cm4gMA0KICAgICAgICB9DQogICAgICAg
::CUAB64:IFdyaXRlLVJlcGFpck1lc3NhZ2UgKCfpnIDopoHkv67lpI3vvJonICsgJHN0YXRlLlJlYXNvbikgJ1llbGxvdycNCiAgICAgICAg
::CUAB64:aWYgKCRlbnY6Q09ERVhfQ1VBX0ZJWF9NT0RFIC1lcSAnY2hlY2snKSB7IFdyaXRlLVJlcGFpck1lc3NhZ2UgJ0NIRUNLIE9OTFnv
::CUAB64:vJrmnKrkv67mlLnov5DooYzml7bjgIInOyByZXR1cm4gMiB9DQogICAgICAgICR3YWl0ZWQgPSBXYWl0LUNvZGV4RXhpdCAkaW5z
::CUAB64:dGFsbCAkcnVudGltZVJvb3QNCiAgICAgICAgJHBhY2thZ2VOb3cgPSBHZXQtQ3VycmVudFBhY2thZ2UNCiAgICAgICAgaWYgKCRw
::CUAB64:YWNrYWdlTm93LlBhY2thZ2VGdWxsTmFtZSAtbmUgJHBhY2thZ2UuUGFja2FnZUZ1bGxOYW1lIC1vciAkcGFja2FnZU5vdy5JbnN0
::CUAB64:YWxsTG9jYXRpb24gLW5lICRwYWNrYWdlLkluc3RhbGxMb2NhdGlvbikgeyB0aHJvdyAnVGhlIGFwcCB3YXMgdXBkYXRlZCBkdXJp
::CUAB64:bmcgdGhpcyBydW4uIFBsZWFzZSBydW4gdGhpcyBmaWxlIGFnYWluLicgfQ0KICAgICAgICBpZiAoKEdldC1SZXF1aXJlZFJ1bnRp
::CUAB64:bWVJZCAkc291cmNlICRpbnN0YWxsKSAtbmUgJHJ1bnRpbWVJZCkgeyB0aHJvdyAnUnVudGltZSBpZGVudGl0eSBjaGFuZ2VkIGR1
::CUAB64:cmluZyB0aGlzIHJ1bi4gUGxlYXNlIHJ1biB0aGlzIGZpbGUgYWdhaW4uJyB9DQogICAgICAgIGlmICgkd2FpdGVkKSB7DQogICAg
::CUAB64:ICAgICAgICBXcml0ZS1SZXBhaXJNZXNzYWdlICfmraPlnKjph43mlrDmoKHpqozpgIDlh7rlkI7nmoTlrpjmlrnov5DooYzml7bi
::CUAB64:gKbigKYnICdDeWFuJw0KICAgICAgICAgICAgJGludmVudG9yeSA9IEdldC1UcmVlSW52ZW50b3J5ICRzb3VyY2UNCiAgICAgICAg
::CUAB64:fQ0KICAgICAgICBDb25maXJtLUNvZGV4U3RvcHBlZCAkaW5zdGFsbCAkcnVudGltZVJvb3QNCiAgICAgICAgV3JpdGUtUmVwYWly
::CUAB64:TWVzc2FnZSAn5q2j5Zyo55u05o6l6KGl6b2Q5q2j5byP6L+Q6KGM5pe255uu5b2V4oCm4oCmJyAnQ3lhbicNCiAgICAgICAgJHJl
::CUAB64:c3VsdCA9IEludm9rZS1SdW50aW1lUmVwYWlyIC1Tb3VyY2VJbnZlbnRvcnkgJGludmVudG9yeSAtVGFyZ2V0ICR0YXJnZXQgLVJ1
::CUAB64:bnRpbWVSb290ICRydW50aW1lUm9vdCAtUnVuRGlyZWN0b3J5ICRydW5EaXJlY3RvcnkgLUluc3RhbGxMb2NhdGlvbiAkaW5zdGFs
::CUAB64:bA0KICAgICAgICBpZiAoJHJlc3VsdC5CYWNrdXApIHsgV3JpdGUtUmVwYWlyTWVzc2FnZSAoJ+WOn+ebruW9leWkh+S7veS/neeV
::CUAB64:meS6ju+8micgKyAkcmVzdWx0LkJhY2t1cCkgfQ0KICAgICAgICBXcml0ZS1SZXBhaXJNZXNzYWdlICdSRVNVTFQ6IFBBU1Mg4oCU
::CUAB64:IOaWh+S7tui3r+W+hOOAgeaWh+S7tuaVsOOAgeWtl+iKguaVsOWSjOmAkOaWh+S7tiBTSEEtMjU2IOWFqOmDqOmAmui/h+OAgicg
::CUAB64:J0dyZWVuJw0KICAgICAgICBXcml0ZS1SZXBhaXJNZXNzYWdlICfkv67lpI3lrozmiJDjgILnjrDlnKjlj6/ku6Xph43mlrDmiZPl
::CUAB64:vIAgQ29kZXjjgIInICdHcmVlbicNCiAgICAgICAgcmV0dXJuIDANCiAgICB9IGNhdGNoIHsNCiAgICAgICAgV3JpdGUtUmVwYWly
::CUAB64:TWVzc2FnZSAoJ1JFU1VMVDogRkFJTCDigJQgJyArICRfLkV4Y2VwdGlvbi5NZXNzYWdlKSAnUmVkJw0KICAgICAgICBpZiAoJHNj
::CUAB64:cmlwdDpMb2dGaWxlKSB7IFdyaXRlLUhvc3QgKCfor7fkv53nlZnov5nku73miqXlkYrvvJonICsgJHNjcmlwdDpMb2dGaWxlKSAt
::CUAB64:Rm9yZWdyb3VuZENvbG9yIFllbGxvdyB9DQogICAgICAgIHJldHVybiAxDQogICAgfSBmaW5hbGx5IHsNCiAgICAgICAgaWYgKCRh
::CUAB64:Y3F1aXJlZCAtYW5kICRtdXRleCkgeyAkbXV0ZXguUmVsZWFzZU11dGV4KCkgfQ0KICAgICAgICBpZiAoJG11dGV4KSB7ICRtdXRl
::CUAB64:eC5EaXNwb3NlKCkgfQ0KICAgIH0NCn0NCg0KaWYgKCRMaWJyYXJ5T25seSkgeyByZXR1cm4gfQ0KZXhpdCAoSW52b2tlLVJlcGFp
::CUAB64:ck1haW4pDQo=
::CUA_REPAIR_PAYLOAD_END
::KIT_UPDATE_PAYLOAD_BEGIN
::KITB64:cGFyYW0oW3N3aXRjaF0kTGlicmFyeU9ubHkpCiRFcnJvckFjdGlvblByZWZlcmVuY2UgPSAnU3RvcCcKJFByb2dyZXNzUHJlZmVy
::KITB64:ZW5jZSA9ICdTaWxlbnRseUNvbnRpbnVlJwoKZnVuY3Rpb24gR2V0LUtpdEhhc2goW3N0cmluZ10kUGF0aCkgewogICAgJHNoYSA9
::KITB64:IFtTZWN1cml0eS5DcnlwdG9ncmFwaHkuU0hBMjU2XTo6Q3JlYXRlKCkKICAgICRzdHJlYW0gPSBbSU8uRmlsZV06Ok9wZW5SZWFk
::KITB64:KCRQYXRoKQogICAgdHJ5IHsgcmV0dXJuIFtCaXRDb252ZXJ0ZXJdOjpUb1N0cmluZygkc2hhLkNvbXB1dGVIYXNoKCRzdHJlYW0p
::KITB64:KS5SZXBsYWNlKCctJywgJycpLlRvTG93ZXJJbnZhcmlhbnQoKSB9CiAgICBmaW5hbGx5IHsgJHN0cmVhbS5EaXNwb3NlKCk7ICRz
::KITB64:aGEuRGlzcG9zZSgpIH0KfQoKZnVuY3Rpb24gR2V0LUtpdFZlcnNpb24oW3N0cmluZ10kVmFsdWUpIHsKICAgIGlmICgkVmFsdWUg
::KITB64:LW5vdG1hdGNoICdeXGQrXC5cZCtcLlxkKyg/OlwuXGQrKT8kJykgeyB0aHJvdyAnSW52YWxpZCBjb21wb25lbnQgdmVyc2lvbi4n
::KITB64:IH0KICAgIHJldHVybiBbdmVyc2lvbl0kVmFsdWUKfQoKZnVuY3Rpb24gUmVxdWVzdC1LaXRKc29uKFtzdHJpbmddJFVybCkgewog
::KITB64:ICAgW05ldC5TZXJ2aWNlUG9pbnRNYW5hZ2VyXTo6U2VjdXJpdHlQcm90b2NvbCA9IFtOZXQuU2VjdXJpdHlQcm90b2NvbFR5cGVd
::KITB64:OjpUbHMxMgogICAgJG9wdGlvbnM9QHtVcmk9JFVybDsgSGVhZGVycz1AeyAnVXNlci1BZ2VudCc9J2NvZGV4LWluaXQta2l0Jzsg
::KITB64:QWNjZXB0PSdhcHBsaWNhdGlvbi92bmQuZ2l0aHViK2pzb24nIH07IFRpbWVvdXRTZWM9OH0KICAgIGlmICgtbm90IFtzdHJpbmdd
::KITB64:OjpJc051bGxPcldoaXRlU3BhY2UoJGVudjpIVFRQU19QUk9YWSkpIHsgJG9wdGlvbnMuUHJveHk9JGVudjpIVFRQU19QUk9YWSB9
::KITB64:CiAgICBlbHNlaWYgKC1ub3QgW3N0cmluZ106OklzTnVsbE9yV2hpdGVTcGFjZSgkZW52OlBST1hZX1VSTCkpIHsgJG9wdGlvbnMu
::KITB64:UHJveHk9JGVudjpQUk9YWV9VUkwgfQogICAgcmV0dXJuIEludm9rZS1SZXN0TWV0aG9kIEBvcHRpb25zCn0KCmZ1bmN0aW9uIFJl
::KITB64:cXVlc3QtS2l0RmlsZShbc3RyaW5nXSRVcmwsIFtzdHJpbmddJFBhdGgpIHsKICAgIFtOZXQuU2VydmljZVBvaW50TWFuYWdlcl06
::KITB64:OlNlY3VyaXR5UHJvdG9jb2wgPSBbTmV0LlNlY3VyaXR5UHJvdG9jb2xUeXBlXTo6VGxzMTIKICAgICRvcHRpb25zPUB7VXJpPSRV
::KITB64:cmw7IE91dEZpbGU9JFBhdGg7IEhlYWRlcnM9QHsnVXNlci1BZ2VudCc9J2NvZGV4LWluaXQta2l0J307IFRpbWVvdXRTZWM9MzA7
::KITB64:IFVzZUJhc2ljUGFyc2luZz0kdHJ1ZX0KICAgIGlmICgtbm90IFtzdHJpbmddOjpJc051bGxPcldoaXRlU3BhY2UoJGVudjpIVFRQ
::KITB64:U19QUk9YWSkpIHsgJG9wdGlvbnMuUHJveHk9JGVudjpIVFRQU19QUk9YWSB9CiAgICBlbHNlaWYgKC1ub3QgW3N0cmluZ106Oklz
::KITB64:TnVsbE9yV2hpdGVTcGFjZSgkZW52OlBST1hZX1VSTCkpIHsgJG9wdGlvbnMuUHJveHk9JGVudjpQUk9YWV9VUkwgfQogICAgSW52
::KITB64:b2tlLVdlYlJlcXVlc3QgQG9wdGlvbnMKfQoKZnVuY3Rpb24gR2V0LUtpdFJlbW90ZShbc3RyaW5nXSRSZXBvc2l0b3J5LCBbc3Ry
::KITB64:aW5nXSRDb21wb25lbnQpIHsKICAgIGlmICgkUmVwb3NpdG9yeSAtbm90bWF0Y2ggJ15bQS1aYS16MC05XVtBLVphLXowLTktXSov
::KITB64:W0EtWmEtejAtOV8uLV0rJCcpIHsgdGhyb3cgJ0ludmFsaWQgR2l0SHViIHJlcG9zaXRvcnkuJyB9CiAgICAkY29tbWl0ID0gUmVx
::KITB64:dWVzdC1LaXRKc29uICgnaHR0cHM6Ly9hcGkuZ2l0aHViLmNvbS9yZXBvcy8nICsgJFJlcG9zaXRvcnkgKyAnL2NvbW1pdHMvbWFp
::KITB64:bicpCiAgICAkcmV2aXNpb24gPSBbc3RyaW5nXSRjb21taXQuc2hhCiAgICBpZiAoJHJldmlzaW9uIC1ub3RtYXRjaCAnXlswLTlh
::KITB64:LWZdezQwfSQnKSB7IHRocm93ICdJbnZhbGlkIEdpdEh1YiBjb21taXQgSUQuJyB9CiAgICAkYmFzZSA9ICdodHRwczovL3Jhdy5n
::KITB64:aXRodWJ1c2VyY29udGVudC5jb20vJyArICRSZXBvc2l0b3J5ICsgJy8nICsgJHJldmlzaW9uICsgJy8nCiAgICAkbWFuaWZlc3Qg
::KITB64:PSBSZXF1ZXN0LUtpdEpzb24gKCRiYXNlICsgJ3VwZGF0ZS1tYW5pZmVzdC5qc29uJykKICAgIGlmICgkbWFuaWZlc3Quc2NoZW1h
::KITB64:X3ZlcnNpb24gLW5lIDEgLW9yICRtYW5pZmVzdC5yZXBvc2l0b3J5IC1jbmUgJFJlcG9zaXRvcnkpIHsgdGhyb3cgJ1VwZGF0ZSBt
::KITB64:YW5pZmVzdCByZXBvc2l0b3J5L3NjaGVtYSBtaXNtYXRjaC4nIH0KICAgICRwcm9wZXJ0eSA9ICRtYW5pZmVzdC5jb21wb25lbnRz
::KITB64:LlBTT2JqZWN0LlByb3BlcnRpZXNbJENvbXBvbmVudF0KICAgIGlmICgtbm90ICRwcm9wZXJ0eSkgeyB0aHJvdyAnQ29tcG9uZW50
::KITB64:IG1pc3NpbmcgZnJvbSB1cGRhdGUgbWFuaWZlc3QuJyB9CiAgICAkaXRlbSA9ICRwcm9wZXJ0eS5WYWx1ZQogICAgW3ZvaWRdKEdl
::KITB64:dC1LaXRWZXJzaW9uIChbc3RyaW5nXSRpdGVtLnZlcnNpb24pKQogICAgJHJlbGF0aXZlID0gW3N0cmluZ10kaXRlbS5wYXRoCiAg
::KITB64:ICBpZiAoJHJlbGF0aXZlIC1ub3RtYXRjaCAnXnNjcmlwdHMvW14vXFxdK1wuY21kJCcgLW9yICRyZWxhdGl2ZS5Db250YWlucygn
::KITB64:Li4nKSkgeyB0aHJvdyAnSW52YWxpZCBzY3JpcHQgZG93bmxvYWQgcGF0aC4nIH0KICAgIGlmIChbc3RyaW5nXSRpdGVtLnNoYTI1
::KITB64:NiAtbm90bWF0Y2ggJ15bMC05YS1mXXs2NH0kJykgeyB0aHJvdyAnSW52YWxpZCBzY3JpcHQgY2hlY2tzdW0uJyB9CiAgICByZXR1
::KITB64:cm4gW3BzY3VzdG9tb2JqZWN0XUB7CiAgICAgICAgVmVyc2lvbj1bc3RyaW5nXSRpdGVtLnZlcnNpb247IEhhc2g9W3N0cmluZ10k
::KITB64:aXRlbS5zaGEyNTY7IEVuY29kaW5nPVtzdHJpbmddJGl0ZW0uZW5jb2RpbmcKICAgICAgICBVcmw9JGJhc2UgKyAoKCRyZWxhdGl2
::KITB64:ZSAtc3BsaXQgJy8nIHwgRm9yRWFjaC1PYmplY3QgeyBbVXJpXTo6RXNjYXBlRGF0YVN0cmluZygkXykgfSkgLWpvaW4gJy8nKQog
::KITB64:ICAgICAgIFBhZ2U9J2h0dHBzOi8vZ2l0aHViLmNvbS8nICsgJFJlcG9zaXRvcnkgKyAnL2NvbW1pdC8nICsgJHJldmlzaW9uCiAg
::KITB64:ICB9Cn0KCmZ1bmN0aW9uIENvbmZpcm0tS2l0VXBkYXRlKFtzdHJpbmddJFZlcnNpb24pIHsKICAgIHJldHVybiAoUmVhZC1Ib3N0
::KITB64:ICgn5Y+R546w54mI5pysICcgKyAkVmVyc2lvbiArICfvvIzkuIvovb3lubbmm7/mjaLlvZPliY3ohJrmnKzvvJ9beS9OXScpKSAt
::KITB64:bWF0Y2ggJ14oP2k6eXx5ZXN85pivfOehruiupCkkJwp9CgpmdW5jdGlvbiBJbnN0YWxsLUtpdFVwZGF0ZSgkUmVtb3RlLCBbc3Ry
::KITB64:aW5nXSRUYXJnZXQsIFtzdHJpbmddJENvbXBvbmVudCwgW3N0cmluZ10kRXhwZWN0ZWRPcmlnaW5hbEhhc2gpIHsKICAgICR0YXJn
::KITB64:ZXRQYXRoID0gW0lPLlBhdGhdOjpHZXRGdWxsUGF0aCgkVGFyZ2V0KQogICAgJGZpbGUgPSBHZXQtSXRlbSAtTGl0ZXJhbFBhdGgg
::KITB64:JHRhcmdldFBhdGggLUZvcmNlCiAgICBpZiAoJGZpbGUuUFNJc0NvbnRhaW5lciAtb3IgKCRmaWxlLkF0dHJpYnV0ZXMgLWJhbmQg
::KITB64:W0lPLkZpbGVBdHRyaWJ1dGVzXTo6UmVwYXJzZVBvaW50KSkgeyB0aHJvdyAnUmVmdXNpbmcgdG8gcmVwbGFjZSBhIGRpcmVjdG9y
::KITB64:eSBvciBsaW5rLicgfQogICAgJGRpcmVjdG9yeSA9IFNwbGl0LVBhdGggLVBhcmVudCAkdGFyZ2V0UGF0aAogICAgJHN0YWdlID0g
::KITB64:Sm9pbi1QYXRoICRkaXJlY3RvcnkgKCcuY29kZXgtdXBkYXRlLScgKyBbR3VpZF06Ok5ld0d1aWQoKS5Ub1N0cmluZygnTicpICsg
::KITB64:Jy50bXAnKQogICAgJGJhY2t1cCA9ICR0YXJnZXRQYXRoICsgJy5iZWZvcmUtdXBkYXRlLScgKyAoR2V0LURhdGUgLUZvcm1hdCAn
::KITB64:eXl5eU1NZGQtSEhtbXNzJykgKyAnLScgKyBbR3VpZF06Ok5ld0d1aWQoKS5Ub1N0cmluZygnTicpLlN1YnN0cmluZygwLDgpICsg
::KITB64:Jy5iYWsnCiAgICB0cnkgewogICAgICAgIFJlcXVlc3QtS2l0RmlsZSAkUmVtb3RlLlVybCAkc3RhZ2UKICAgICAgICBpZiAoKEdl
::KITB64:dC1LaXRIYXNoICRzdGFnZSkgLWNuZSAkUmVtb3RlLkhhc2gpIHsgdGhyb3cgJ1NIQS0yNTYgbWlzbWF0Y2g7IG9yaWdpbmFsIHNj
::KITB64:cmlwdCB3YXMgbm90IHJlcGxhY2VkLicgfQogICAgICAgICRlbmNvZGluZyA9IHN3aXRjaCAoJFJlbW90ZS5FbmNvZGluZykgewog
::KITB64:ICAgICAgICAgICAnZ2JrJyB7IFtUZXh0LkVuY29kaW5nXTo6R2V0RW5jb2RpbmcoOTM2KSB9CiAgICAgICAgICAgICd1dGYtOCcg
::KITB64:eyBbVGV4dC5FbmNvZGluZ106OlVURjggfQogICAgICAgICAgICBkZWZhdWx0IHsgdGhyb3cgJ1Vuc3VwcG9ydGVkIHNjcmlwdCBl
::KITB64:bmNvZGluZy4nIH0KICAgICAgICB9CiAgICAgICAgJGNhbmRpZGF0ZSA9IFtJTy5GaWxlXTo6UmVhZEFsbFRleHQoJHN0YWdlLCAk
::KITB64:ZW5jb2RpbmcpCiAgICAgICAgaWYgKC1ub3QgJGNhbmRpZGF0ZS5TdGFydHNXaXRoKCdAZWNobyBvZmYnKSAtb3IKICAgICAgICAg
::KITB64:ICAgJGNhbmRpZGF0ZSAtbm90bWF0Y2ggKCcoP20pXnNldCAiQ09ERVhfS0lUX0NPTVBPTkVOVD0nICsgW3JlZ2V4XTo6RXNjYXBl
::KITB64:KCRDb21wb25lbnQpICsgJyJccj8kJykgLW9yCiAgICAgICAgICAgICRjYW5kaWRhdGUgLW5vdG1hdGNoICgnKD9tKV5zZXQgIkNP
::KITB64:REVYX0tJVF9WRVJTSU9OPScgKyBbcmVnZXhdOjpFc2NhcGUoJFJlbW90ZS5WZXJzaW9uKSArICciXHI/JCcpKSB7CiAgICAgICAg
::KITB64:ICAgIHRocm93ICdEb3dubG9hZGVkIGZpbGUgZG9lcyBub3QgbWF0Y2ggdGhlIGV4cGVjdGVkIHNjcmlwdCBjb21wb25lbnQvdmVy
::KITB64:c2lvbi4nCiAgICAgICAgfQogICAgICAgIGlmICgoR2V0LUtpdEhhc2ggJHRhcmdldFBhdGgpIC1jbmUgJEV4cGVjdGVkT3JpZ2lu
::KITB64:YWxIYXNoKSB7IHRocm93ICdTY3JpcHQgY2hhbmdlZCBkdXJpbmcgZG93bmxvYWQ7IHVwZGF0ZSBjYW5jZWxsZWQuJyB9CiAgICAg
::KITB64:ICAgW0lPLkZpbGVdOjpSZXBsYWNlKCRzdGFnZSwgJHRhcmdldFBhdGgsICRiYWNrdXApCiAgICAgICAgV3JpdGUtSG9zdCAoJ+ab
::KITB64:tOaWsOWujOaIkOOAguaXp+iEmuacrOWkh+S7ve+8micgKyAkYmFja3VwKSAtRm9yZWdyb3VuZENvbG9yIEdyZWVuCiAgICAgICAg
::KITB64:V3JpdGUtSG9zdCAn6K+36YeN5paw6L+Q6KGM6ISa5pys77yM5Lul5L2/55So5paw54mI5pys44CCJyAtRm9yZWdyb3VuZENvbG9y
::KITB64:IEdyZWVuCiAgICAgICAgcmV0dXJuIDIwCiAgICB9IGZpbmFsbHkgewogICAgICAgIGlmIChUZXN0LVBhdGggLUxpdGVyYWxQYXRo
::KITB64:ICRzdGFnZSkgeyBSZW1vdmUtSXRlbSAtTGl0ZXJhbFBhdGggJHN0YWdlIC1Gb3JjZSB9CiAgICB9Cn0KCmZ1bmN0aW9uIEludm9r
::KITB64:ZS1LaXRVcGRhdGUoW3N0cmluZ10kUmVwb3NpdG9yeSwgW3N0cmluZ10kQ29tcG9uZW50LCBbc3RyaW5nXSRDdXJyZW50VmVyc2lv
::KITB64:biwgW3N0cmluZ10kVGFyZ2V0LCBbc3RyaW5nXSRNb2RlID0gJ2F1dG8nKSB7CiAgICAkbXV0ZXggPSAkbnVsbDsgJGFjcXVpcmVk
::KITB64:ID0gJGZhbHNlCiAgICB0cnkgewogICAgICAgICRjdXJyZW50ID0gR2V0LUtpdFZlcnNpb24gJEN1cnJlbnRWZXJzaW9uCiAgICAg
::KITB64:ICAgJHRhcmdldFBhdGggPSBbSU8uUGF0aF06OkdldEZ1bGxQYXRoKCRUYXJnZXQpCiAgICAgICAgaWYgKC1ub3QgKFRlc3QtUGF0
::KITB64:aCAtTGl0ZXJhbFBhdGggJHRhcmdldFBhdGggLVBhdGhUeXBlIExlYWYpKSB7IHRocm93ICdDdXJyZW50IHNjcmlwdCBpcyB1bmF2
::KITB64:YWlsYWJsZS4nIH0KICAgICAgICAkb3JpZ2luYWxIYXNoID0gR2V0LUtpdEhhc2ggJHRhcmdldFBhdGgKICAgICAgICAka2V5U2hh
::KITB64:ID0gW1NlY3VyaXR5LkNyeXB0b2dyYXBoeS5TSEEyNTZdOjpDcmVhdGUoKQogICAgICAgIHRyeSB7ICRrZXkgPSBbQml0Q29udmVy
::KITB64:dGVyXTo6VG9TdHJpbmcoJGtleVNoYS5Db21wdXRlSGFzaChbVGV4dC5FbmNvZGluZ106OlVURjguR2V0Qnl0ZXMoJFJlcG9zaXRv
::KITB64:cnkgKyAnOicgKyAkQ29tcG9uZW50ICsgJzonICsgJHRhcmdldFBhdGguVG9Mb3dlckludmFyaWFudCgpKSkpLlJlcGxhY2UoJy0n
::KITB64:LCcnKSB9CiAgICAgICAgZmluYWxseSB7ICRrZXlTaGEuRGlzcG9zZSgpIH0KICAgICAgICAkbXV0ZXggPSBbVGhyZWFkaW5nLk11
::KITB64:dGV4XTo6bmV3KCRmYWxzZSwgKCdMb2NhbFxDb2RleEtpdFVwZGF0ZS0nICsgJGtleSkpCiAgICAgICAgdHJ5IHsgJGFjcXVpcmVk
::KITB64:ID0gJG11dGV4LldhaXRPbmUoMCkgfSBjYXRjaCBbVGhyZWFkaW5nLkFiYW5kb25lZE11dGV4RXhjZXB0aW9uXSB7ICRhY3F1aXJl
::KITB64:ZCA9ICR0cnVlIH0KICAgICAgICBpZiAoLW5vdCAkYWNxdWlyZWQpIHsgcmV0dXJuIDAgfQogICAgICAgICRjYWNoZVJvb3QgPSBK
::KITB64:b2luLVBhdGggJGVudjpMT0NBTEFQUERBVEEgJ0NvZGV4SW5pdEtpdFx1cGRhdGUtY2hlY2tzJwogICAgICAgICRjYWNoZVBhdGgg
::KITB64:PSBKb2luLVBhdGggJGNhY2hlUm9vdCAoJGtleSArICcuanNvbicpCiAgICAgICAgaWYgKCRNb2RlIC1pbiBAKCdhdXRvJywgJ2Jh
::KITB64:Y2tncm91bmQnKSAtYW5kIChUZXN0LVBhdGggLUxpdGVyYWxQYXRoICRjYWNoZVBhdGgpKSB7CiAgICAgICAgICAgIHRyeSB7CiAg
::KITB64:ICAgICAgICAgICAgICAkY2FjaGVkID0gW0lPLkZpbGVdOjpSZWFkQWxsVGV4dCgkY2FjaGVQYXRoKSB8IENvbnZlcnRGcm9tLUpz
::KITB64:b24KICAgICAgICAgICAgICAgICRsYXN0Q2hlY2sgPSBbZGF0ZXRpbWVdOjpQYXJzZSgkY2FjaGVkLmNoZWNrZWRfYXQpLlRvVW5p
::KITB64:dmVyc2FsVGltZSgpCiAgICAgICAgICAgICAgICBpZiAoJGNhY2hlZC5jdXJyZW50X3ZlcnNpb24gLWVxICRDdXJyZW50VmVyc2lv
::KITB64:biAtYW5kICRsYXN0Q2hlY2sgLWxlIFtkYXRldGltZV06OlV0Y05vdyAtYW5kIChbZGF0ZXRpbWVdOjpVdGNOb3cgLSAkbGFzdENo
::KITB64:ZWNrKS5Ub3RhbEhvdXJzIC1sdCAyNCkgeyByZXR1cm4gMCB9CiAgICAgICAgICAgIH0gY2F0Y2ggeyB9CiAgICAgICAgfQogICAg
::KITB64:ICAgICRyZW1vdGUgPSBHZXQtS2l0UmVtb3RlICRSZXBvc2l0b3J5ICRDb21wb25lbnQKICAgICAgICB0cnkgewogICAgICAgICAg
::KITB64:ICBbdm9pZF1bSU8uRGlyZWN0b3J5XTo6Q3JlYXRlRGlyZWN0b3J5KCRjYWNoZVJvb3QpCiAgICAgICAgICAgICRzdGF0ZSA9IEB7
::KITB64:IGNoZWNrZWRfYXQ9W2RhdGV0aW1lXTo6VXRjTm93LlRvU3RyaW5nKCdvJyk7IGN1cnJlbnRfdmVyc2lvbj0kQ3VycmVudFZlcnNp
::KITB64:b247IGxhdGVzdF92ZXJzaW9uPSRyZW1vdGUuVmVyc2lvbjsgc291cmNlPSRyZW1vdGUuUGFnZSB9CiAgICAgICAgICAgIFtJTy5G
::KITB64:aWxlXTo6V3JpdGVBbGxUZXh0KCRjYWNoZVBhdGgsICgkc3RhdGUgfCBDb252ZXJ0VG8tSnNvbiksIFtUZXh0LlVURjhFbmNvZGlu
::KITB64:Z106Om5ldygkZmFsc2UpKQogICAgICAgIH0gY2F0Y2ggeyB9CiAgICAgICAgaWYgKChHZXQtS2l0VmVyc2lvbiAkcmVtb3RlLlZl
::KITB64:cnNpb24pIC1sZSAkY3VycmVudCkgewogICAgICAgICAgICBpZiAoJE1vZGUgLWVxICdtYW51YWwnKSB7IFdyaXRlLUhvc3QgKCfl
::KITB64:vZPliY3ohJrmnKzlt7LmmK/mnIDmlrDniYjmnKzvvJonICsgJEN1cnJlbnRWZXJzaW9uKSAtRm9yZWdyb3VuZENvbG9yIEdyZWVu
::KITB64:IH0KICAgICAgICAgICAgcmV0dXJuIDAKICAgICAgICB9CiAgICAgICAgV3JpdGUtSG9zdCAoJ0dpdEh1YiDlj5HnjrDmlrDniYjm
::KITB64:nKzvvJonICsgJEN1cnJlbnRWZXJzaW9uICsgJyAtPiAnICsgJHJlbW90ZS5WZXJzaW9uKSAtRm9yZWdyb3VuZENvbG9yIEN5YW4K
::KITB64:ICAgICAgICBXcml0ZS1Ib3N0ICgn5p2l5rqQ77yaJyArICRyZW1vdGUuUGFnZSkKICAgICAgICBpZiAoJE1vZGUgLWluIEAoJ2No
::KITB64:ZWNrJywgJ2JhY2tncm91bmQnKSkgeyByZXR1cm4gMCB9CiAgICAgICAgaWYgKC1ub3QgKENvbmZpcm0tS2l0VXBkYXRlICRyZW1v
::KITB64:dGUuVmVyc2lvbikpIHsgV3JpdGUtSG9zdCAn5bey5L+d55WZ5b2T5YmN6ISa5pys44CCJzsgcmV0dXJuIDAgfQogICAgICAgIHJl
::KITB64:dHVybiBJbnN0YWxsLUtpdFVwZGF0ZSAkcmVtb3RlICR0YXJnZXRQYXRoICRDb21wb25lbnQgJG9yaWdpbmFsSGFzaAogICAgfSBj
::KITB64:YXRjaCB7CiAgICAgICAgV3JpdGUtSG9zdCAoJ0dpdEh1YiDmm7TmlrDmo4Dmn6XmnKrlrozmiJDvvJonICsgJF8uRXhjZXB0aW9u
::KITB64:Lk1lc3NhZ2UgKyAn77yb5b2T5YmN6ISa5pys5Yqf6IO95Y+v57un57ut5L2/55So44CCJykgLUZvcmVncm91bmRDb2xvciBZZWxs
::KITB64:b3cKICAgICAgICByZXR1cm4gMAogICAgfSBmaW5hbGx5IHsKICAgICAgICBpZiAoJGFjcXVpcmVkIC1hbmQgJG11dGV4KSB7ICRt
::KITB64:dXRleC5SZWxlYXNlTXV0ZXgoKSB9CiAgICAgICAgaWYgKCRtdXRleCkgeyAkbXV0ZXguRGlzcG9zZSgpIH0KICAgIH0KfQoKaWYg
::KITB64:KCRMaWJyYXJ5T25seSkgeyByZXR1cm4gfQpbQ29uc29sZV06Ok91dHB1dEVuY29kaW5nID0gW1RleHQuVVRGOEVuY29kaW5nXTo6
::KITB64:bmV3KCRmYWxzZSkKZXhpdCAoSW52b2tlLUtpdFVwZGF0ZSAkZW52OkNPREVYX0tJVF9SRVBPU0lUT1JZICRlbnY6Q09ERVhfS0lU
::KITB64:X0NPTVBPTkVOVCAkZW52OkNPREVYX0tJVF9WRVJTSU9OICRlbnY6Q09ERVhfS0lUX1NFTEYgJGVudjpDT0RFWF9LSVRfVVBEQVRF
::KITB64:X01PREUpCg==
::KIT_UPDATE_PAYLOAD_END
