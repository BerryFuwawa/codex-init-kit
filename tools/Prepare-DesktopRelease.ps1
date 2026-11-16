[CmdletBinding()]
param(
    [string]$Executable,
    [string]$Notes='初始化按需更新，保留自定义配置；失败项单独重试，原资料与 Work 连接接着用。完善代理继承、操作日志和高缩放界面。'
)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(-not $Executable){$Executable=Join-Path $root 'build\desktop\CodexInitKit.exe'}
if(-not (Test-Path -LiteralPath $Executable -PathType Leaf)){throw 'Build the desktop EXE first.'}
$versionInfo=[Diagnostics.FileVersionInfo]::GetVersionInfo($Executable)
$version=([version]$versionInfo.FileVersion).ToString(3)
$stream=[IO.File]::OpenRead($Executable);$sha=[Security.Cryptography.SHA256]::Create()
try{$hash=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}finally{$stream.Dispose();$sha.Dispose()}
$manifest=[ordered]@{
    schema_version=1
    repository='BerryFuwawa/codex-init-kit'
    version=$version
    url=('https://github.com/BerryFuwawa/codex-init-kit/releases/download/v'+$version+'/CodexInitKit.exe')
    sha256=$hash
    notes=$Notes
}
[IO.File]::WriteAllText((Join-Path $root 'desktop-update.json'),($manifest|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path (Split-Path $Executable -Parent) 'SHA256SUMS.txt'),($hash+'  CodexInitKit.exe'+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))
Write-Output ('Prepared desktop update manifest for '+$version+' '+$hash)
