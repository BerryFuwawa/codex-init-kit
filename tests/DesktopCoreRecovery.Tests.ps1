param([string]$PayloadRoot)
$ErrorActionPreference='Stop'
$testPayloadRoot=$PayloadRoot
. (Join-Path $testPayloadRoot 'Backend.ps1')
$PayloadRoot=$testPayloadRoot
Import-CuaLibrary $PayloadRoot
function AssertCore($Condition,[string]$Message){if(-not $Condition){throw $Message}}
$scratch=[IO.Path]::GetFullPath((Join-Path $env:TEMP ('CodexKit-core-tests-'+[guid]::NewGuid().ToString('N'))))
$previousLocalData=$env:LOCALAPPDATA
[void][IO.Directory]::CreateDirectory($scratch)
try {
    # A representative implementation fixture: the same source format as the
    # official package, without reading or changing the installed application.
    $known=@'
var c=[`manifest.json`,`bin/node.exe`,`bin/node_repl.exe`].map(e=>Ma({destinationPath:n,executableName:e,sourcePath:(0,l.join)(t,e)})),ca=[`codex-code-mode-host.exe`,`codex-windows-sandbox-setup.exe`,`codex-command-runner.exe`];
function Na(e){let t=(0,a.createHash)(`sha256`);for(let n of e)t.update(n.executableName),t.update(`\0`),t.update(n.digest),t.update(`\0`);return t.digest(`hex`)}
function Ia(e){return(0,a.createHash)(`sha256`).update((0,c.readFileSync)(e)).digest(`hex`)}
function Ma({destinationPath:e,executableName:t,sourcePath:n}){return{digest:Ia({destinationPath:e,executableName:t,filePath:n,operation:`hash_source`,sourcePath:n}),executableName:t,size:1,sourcePath:n}}
function ja({executableName:e,sourcePath:t,stableDestinationPath:n,windowsSiblingExecutableNames:r}){let i=(0,l.dirname)(t),a=[e,...r].map(e=>Ma({destinationPath:n,executableName:e,sourcePath:(0,l.join)(i,e)})),o=Na(a).slice(0,16)}
var path=[`OpenAI`,`Codex`,`bin`,`codex.exe`];ga({windowsSiblingExecutableNames:ca})
'@
    Assert-KnownDesktopCoreAlgorithm $known
    foreach($unknown in @($known.Replace('codex-command-runner.exe','other.exe'),$known.Replace('o=Na(a).slice','o=Unknown(a).slice'),$known.Replace('t.update(n.digest)','t.update(n.sourcePath)'),$known.Replace('map(e=>Ma({destinationPath:n,executableName:e,sourcePath:(0,l.join)(i,e)})','map(e=>Other({destinationPath:n,executableName:e,sourcePath:(0,l.join)(i,e)})'))){
        $rejected=$false;try{Assert-KnownDesktopCoreAlgorithm $unknown}catch{$rejected=$true};AssertCore $rejected 'Unknown desktop layout or hash was accepted.'
    }
    $streamed=@'
var ca=[`codex-code-mode-host.exe`,`codex-windows-sandbox-setup.exe`,`codex-command-runner.exe`];
let i=[`manifest.json`,`bin/node.exe`,`bin/node_repl.exe`].map(e=>L({destinationPath:r,executableName:e,sourcePath:(0,o.join)(t,e)})),a=M(i).slice(0,16);
function L({destinationPath:e,executableName:t,sourcePath:r}){return{digest:D({destinationPath:e,executableName:t,filePath:r,operation:`hash_source`,sourcePath:r}),executableName:t,size:1,sourcePath:r}}
function M(e){let t=(0,n.createHash)(`sha256`);for(let r of e)t.update(r.executableName),t.update(`\0`),t.update(r.digest),t.update(`\0`);return t.digest(`hex`)}
function D({destinationPath:e,executableName:t,operation:r,sourcePath:s,filePath:i}){return W({destinationPath:e,executableName:t,operation:r,sourcePath:s},()=>{let e=(0,n.createHash)(`sha256`);return j(i,t=>{e.update(t)}),e.digest(`hex`)})}
function j(e,t){let r=(0,s.openSync)(e,`r`);try{let e=(0,s.fstatSync)(r).size,n=Buffer.allocUnsafe(Math.min(Math.max(1,e),262144));for(;;){let e=(0,s.readSync)(r,n);if(0===e)return;t(n.subarray(0,e))}}finally{(0,s.closeSync)(r)}}
function ja({executableName:e,sourcePath:t,stableDestinationPath:n,windowsSiblingExecutableNames:r}){let i=(0,l.dirname)(t),a=[e,...r].map(e=>L({destinationPath:n,executableName:e,sourcePath:(0,l.join)(i,e)})),o=M(a).slice(0,16)}
var path=[`OpenAI`,`Codex`,`bin`,`codex.exe`];ga({windowsSiblingExecutableNames:ca})
'@
    Assert-KnownDesktopCoreAlgorithm $streamed -Format streamed
    $rejected=$false;try{Assert-KnownDesktopCoreAlgorithm $streamed.Replace('subarray(0,e)','subarray(1,e)') -Format streamed}catch{$rejected=$true};AssertCore $rejected 'Incomplete streamed hashing was accepted.'
    $env:LOCALAPPDATA=Join-Path $scratch 'local'
    $source=Join-Path $scratch 'official'
    [void][IO.Directory]::CreateDirectory($source);[void][IO.Directory]::CreateDirectory($env:LOCALAPPDATA)
    $files=@(foreach($name in (Get-DesktopCoreFiles)){
        $path=Join-Path $source $name;[IO.File]::WriteAllText($path,('official fixture '+$name))
        $digest=Get-FileDigest $path;[pscustomobject]@{Name=$name;Source=$path;Hash=$digest.Hash;Length=$digest.Length}
    })
    $id=Get-DesktopCoreId $files
    AssertCore ($id -match '^[0-9a-f]{16}$' -and $id -ne (Get-DesktopCoreId @($files[1],$files[0],$files[2],$files[3]))) 'Runtime identity or file ordering was ignored.'
    $root=Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\bin';$target=Join-Path $root $id
    $package=[pscustomobject]@{Version='26.1.0.0';PackageFullName='fixture-package';InstallLocation=$source}
    $fixtureContext=[pscustomobject]@{Package=$package;Source=$source;Files=$files;Id=$id;Root=$root;Target=$target}
    $script:fakePackage=$package;$script:running=$false;$script:stopAfter=0;$script:stopChecks=0;$script:lastReport=''
    function Import-CuaLibrary {}
    function Get-DesktopCoreContext {return $fixtureContext}
    function Get-OfficialDesktopPackage {return $script:fakePackage}
    function Assert-DesktopReady {}
    function Get-DesktopCoreLogSignal {return $true}
    function Write-BackendLine {param($Line) $script:lastReport=[string]$Line}
    function Confirm-CodexStopped {$script:stopChecks++;if($script:running -or ($script:stopAfter -gt 0 -and $script:stopChecks -ge $script:stopAfter)){throw 'CODEX_STILL_RUNNING'}}
    $rc=Invoke-DesktopCoreOperation 'check' $PayloadRoot
    AssertCore ($rc -eq 0 -and $script:lastReport -match 'rename_staging' -and -not(Test-Path -LiteralPath $target)) 'Read-only diagnosis changed the target or omitted the signal.'
    $script:running=$true
    AssertCore ((Invoke-DesktopCoreOperation 'repair' $PayloadRoot) -eq 2 -and -not(Test-Path -LiteralPath $target)) 'Running Codex did not block repair.'
    $script:running=$false;$script:fakePackage=[pscustomobject]@{PackageFullName='new-version';InstallLocation=$source}
    AssertCore ((Invoke-DesktopCoreOperation 'repair' $PayloadRoot) -eq 2 -and -not(Test-Path -LiteralPath $target)) 'Version change did not block repair.'
    $script:fakePackage=$package
    AssertCore ((Invoke-DesktopCoreOperation 'repair' $PayloadRoot) -eq 0 -and (Test-DesktopCoreTarget $fixtureContext) -eq 'ready') 'Missing target was not safely prepared.'
    AssertCore ((Invoke-DesktopCoreOperation 'repair' $PayloadRoot) -eq 0 -and (Test-DesktopCoreTarget $fixtureContext) -eq 'ready') 'Repeated repair was not idempotent.'
    $changed=Join-Path $target $files[0].Name;[IO.File]::WriteAllText($changed,'user-changed')
    AssertCore ((Invoke-DesktopCoreOperation 'repair' $PayloadRoot) -eq 2 -and [IO.File]::ReadAllText($changed) -eq 'user-changed') 'Existing modified target was overwritten.'
    $newTarget=Join-Path $root '1234567890abcdef';$fixtureContext.Target=$newTarget
    $script:stopChecks=0;$script:stopAfter=4
    AssertCore ((Invoke-DesktopCoreOperation 'repair' $PayloadRoot) -eq 2 -and (Test-Path -LiteralPath $newTarget)) 'Mid-copy interruption was not reported.'
    $script:stopAfter=0
    AssertCore ((Invoke-DesktopCoreOperation 'repair' $PayloadRoot) -eq 2 -and @(Get-ChildItem -LiteralPath $newTarget).Count -eq 1) 'Incomplete target was overwritten or removed.'
    Write-Output 'Desktop core recovery tests passed.'
} finally {
    $env:LOCALAPPDATA=$previousLocalData
    $temp=[IO.Path]::GetFullPath($env:TEMP).TrimEnd('\')+'\'
    if($scratch.StartsWith($temp,[StringComparison]::OrdinalIgnoreCase) -and (Split-Path -Leaf $scratch) -match '^CodexKit-core-tests-[0-9a-f]{32}$'){Remove-Item -LiteralPath $scratch -Recurse -Force}
}
