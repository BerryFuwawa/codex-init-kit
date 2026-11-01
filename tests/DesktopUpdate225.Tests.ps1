param([string]$PayloadRoot)
$testPayloadRoot=$PayloadRoot
$ErrorActionPreference='Stop'
if($PayloadRoot){. (Join-Path $PayloadRoot 'Backend.ps1')}else{. (Join-Path (Split-Path $PSScriptRoot -Parent) 'app\Backend.ps1')}
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$scratch=Join-Path ([IO.Path]::GetTempPath()) ('CodexKit-probe-'+[guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory((Join-Path $scratch 'app\resources'))
try{
    foreach($name in @('codex.exe','codex-code-mode-host.exe','codex-command-runner.exe','codex-windows-sandbox-setup.exe')){[IO.File]::WriteAllText((Join-Path $scratch ('app\resources\'+$name)),'fixture')}
    function Assert-PayloadFile {return 'fixture'}
    function Initialize-GuardRuntime {}
    function Test-OpenAISignature {return $signatureValid}
    function Invoke-BoundedProcess {throw [ComponentModel.Win32Exception]::new(5)}
    $signatureValid=$true;$package=[pscustomobject]@{InstallLocation=$scratch;Status='Ok';Version='26.0.0.0'}
    if((Test-DesktopCli $package $scratch) -ne 'unavailable'){throw 'Access-denied CLI probe was not treated as unavailable.'}
    $signatureValid=$false;$rejected=$false
    try{Test-DesktopCli $package $scratch}catch{$rejected=$true;if(-not $_.Exception.Data['DesktopUnsafe']){throw 'Signature failure did not pause dependent repairs.'}}
    if(-not $rejected){throw 'Invalid signature was treated as a usable desktop.'}
    $signatureValid=$true
    function Assert-DesktopStopped {return $package}
    function Assert-DesktopReady {}
    function Get-OfficialDesktopPackage {return $package}
    function Get-Command {param($Name,$ErrorAction) return [pscustomobject]@{Source='fixture-winget'}}
    function Invoke-BoundedProcess {param($FilePath,$Arguments,$TimeoutSeconds) if($FilePath -eq 'fixture-winget'){return [pscustomobject]@{ExitCode=-1978335189;TimedOut=$false}};throw [ComponentModel.Win32Exception]::new(5)}
    if((Invoke-DesktopUpdate $scratch) -ne 'latest'){throw 'No update plus denied probe did not retain the no-update outcome.'}
    function Invoke-BoundedProcess {return [pscustomobject]@{ExitCode=-2147012867;TimedOut=$false}}
    $rejected=$false;try{Invoke-DesktopUpdate $scratch}catch{$rejected=$true;if($_.Exception.Data['DesktopUnsafe']){throw 'A network-only failure blocked a statically healthy desktop.'}}
    if(-not $rejected){throw 'Network update failure was reported as success.'}
    function Invoke-BoundedProcess {throw (New-DesktopUnsafeError 'Process did not stop; completion is still pending.')}
    $rejected=$false;try{Invoke-DesktopUpdate $scratch}catch{$rejected=$true;if(-not $_.Exception.Data['DesktopUnsafe']){throw 'A still-running installer allowed dependent repairs.'}}
    if(-not $rejected){throw 'A still-running installer was reported complete.'}
    # Real loopback HTTP exercises the PowerShell/C# validation delegate used
    # by the private CMD downloader, without downloading or running any tool.
    $helper=if($testPayloadRoot){Join-Path $testPayloadRoot 'GitHubDownload.ps1'}else{Join-Path (Split-Path $PSScriptRoot -Parent) 'app\GitHubDownload.ps1'}
    . $helper
    Initialize-GitHubTransport
    Add-Type -TypeDefinition @'
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
public class RtkHttpFixture:IDisposable {
    readonly TcpListener listener;readonly Task task;
    public string Url;
    public RtkHttpFixture(string body){listener=new TcpListener(IPAddress.Loopback,0);listener.Start();Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/";
        task=Task.Run(()=>{using(var socket=listener.AcceptTcpClient())using(var stream=socket.GetStream()){
            stream.ReadTimeout=5000;var header=new StringBuilder();int b;while((b=stream.ReadByte())>=0){header.Append((char)b);if(header.ToString().EndsWith("\r\n\r\n"))break;}
            var bytes=Encoding.UTF8.GetBytes(body);var prefix=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: "+bytes.Length+"\r\nConnection: close\r\n\r\n");stream.Write(prefix,0,prefix.Length);stream.Write(bytes,0,bytes.Length);
        }});
    }
    public void Dispose(){listener.Stop();if(!task.Wait(5000))throw new Exception("Fixture HTTP server did not stop.");}
}
'@
    $zipPath=Join-Path $scratch 'rtk-x86_64-pc-windows-msvc.zip';$checksumPath=Join-Path $scratch 'checksums.txt'
    $body='test-only ZIP bytes';$sha=[Security.Cryptography.SHA256]::Create()
    try{$expected=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($body))).Replace('-','').ToLowerInvariant()}finally{$sha.Dispose()}
    foreach($hash in @(('0'*64),$expected)){
        [IO.File]::WriteAllText($checksumPath,($hash+'  rtk-x86_64-pc-windows-msvc.zip'))
        $server=[RtkHttpFixture]::new($body)
        try{
            $failed=$false;try{Invoke-GuiGitHubDownload ($server.Url+'rtk-x86_64-pc-windows-msvc.zip') $zipPath 0 '127.0.0.1' ''}catch{$failed=$true}
            if($hash -eq $expected){if($failed -or [IO.File]::ReadAllText($zipPath) -ne $body){throw 'Actual RTK download did not accept the expected checksum.'}}
            elseif(-not $failed -or (Test-Path -LiteralPath $zipPath)){throw 'RTK checksum mismatch left an installable file.'}
        }finally{$server.Dispose()}
    }
    Write-Output 'PASS: access-denied runtime probe, invalid signatures and no-update outcome.'
}finally{Remove-Item -LiteralPath $scratch -Recurse -Force}
