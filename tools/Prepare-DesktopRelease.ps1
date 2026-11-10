[CmdletBinding()]
param([string]$Executable)
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
    notes='修复中文工作目录规则的校验，让初始化继续执行 CUA 检查；文件夹管理失败时写入准确的失败报告。'
}
[IO.File]::WriteAllText((Join-Path $root 'desktop-update.json'),($manifest|ConvertTo-Json),[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path (Split-Path $Executable -Parent) 'SHA256SUMS.txt'),($hash+'  CodexInitKit.exe'+[Environment]::NewLine),[Text.UTF8Encoding]::new($false))
Write-Output ('Prepared desktop update manifest for '+$version+' '+$hash)
