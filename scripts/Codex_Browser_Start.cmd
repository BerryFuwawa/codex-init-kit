@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0Codex_Browser_Start.ps1" %*
set "CODEX_START_EXIT=%errorlevel%"
if not "%CODEX_START_EXIT%"=="0" pause
exit /b %CODEX_START_EXIT%
