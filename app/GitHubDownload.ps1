param([string]$Url,[string]$Path,[int]$ProxyPort=0,[string]$ProxyHost='127.0.0.1',[string]$OptionsBase64)
$ErrorActionPreference='Stop'
function Initialize-GitHubTransport {
    if(-not ('CodexKit.GitHubTransfer' -as [type])){Add-Type -Path (Join-Path $PSScriptRoot 'GitHubTransport.cs') -ReferencedAssemblies System.Web.Extensions.dll}
}
function Get-RtkChecksum {
    param([string]$Text)
    $match=[regex]::Match($Text,'(?im)^([a-f0-9]{64})\s+\*?(?:\./)?rtk-x86_64-pc-windows-msvc\.zip\s*$')
    if(-not $match.Success){throw 'RTK 校验文件不包含所需 Windows ZIP 的 SHA-256。'}
    return $match.Groups[1].Value.ToLowerInvariant()
}
function Invoke-GuiGitHubDownload {
    param([string]$Url,[string]$Path,[int]$ProxyPort,[string]$ProxyHost,[string]$OptionsBase64)
    Initialize-GitHubTransport
    $settings=[CodexKit.GitHubNetworkOptions]::new()
    if($OptionsBase64){$value=[Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($OptionsBase64))|ConvertFrom-Json;$settings.githubMirrorEnabled=$value.githubMirrorEnabled;$settings.customGithubMirror=$value.customGithubMirror}
    $transfer=[CodexKit.GitHubTransfer]::new($settings,$ProxyPort,$ProxyHost,[Action[string]]{param($line)[Console]::Out.WriteLine($line)})
    $expected=''
    if($Url -match '/rtk-x86_64-pc-windows-msvc\.zip$'){
        $checksums=Join-Path (Split-Path $Path -Parent) 'checksums.txt'
        $expected=Get-RtkChecksum ([IO.File]::ReadAllText($checksums))
    }
    $validate=[Action[string]]{
        param($candidate)
        if($expected){
            $sha=[Security.Cryptography.SHA256]::Create();$stream=[IO.File]::OpenRead($candidate)
            try{$actual=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','').ToLowerInvariant()}finally{$stream.Dispose();$sha.Dispose()}
            if($actual -ne $expected){throw [IO.InvalidDataException]::new('RTK ZIP SHA-256 不一致，切换下载通道。')}
        }elseif($Url -match '/checksums\.txt$'){[void](Get-RtkChecksum ([IO.File]::ReadAllText($candidate)))}
    }
    $transfer.Download($Url,$Path,$validate)
}
if($MyInvocation.InvocationName -ne '.'){
    try{Invoke-GuiGitHubDownload $Url $Path $ProxyPort $ProxyHost $OptionsBase64;exit 0}
    catch{[Console]::Error.WriteLine(('GitHub 组件下载未完成：'+$_.Exception.Message));exit 1}
}
