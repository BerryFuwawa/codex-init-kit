param([string]$KitRoot = (Split-Path $PSScriptRoot -Parent))
$ErrorActionPreference = 'Stop'
. (Join-Path $KitRoot 'app\Backend.ps1')
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('CodexKit-runtime-test-' + [guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
try {
    Copy-Item -LiteralPath (Join-Path $KitRoot 'scripts\Codex_CUA_Repair.cmd') -Destination (Join-Path $scratch 'cua.cmd')
    Import-CuaLibrary $scratch
    $source = Join-Path $scratch 'source'
    [void][IO.Directory]::CreateDirectory((Join-Path $source 'bin'))
    [IO.File]::WriteAllText((Join-Path $source 'manifest.json'), 'manifest-test')
    [IO.File]::WriteAllText((Join-Path $source 'bin\node.exe'), 'node-test')
    [IO.File]::WriteAllText((Join-Path $source 'bin\node_repl.exe'), 'repl-test')
    $knownCode = @'
const files=["manifest.json","bin/node.exe","bin/node_repl.exe"];
function M(e){let t=(0,n.createHash)("sha256");for(let r of e)t.update(r.executableName),t.update("\0"),t.update(r.digest),t.update("\0");return t.digest("hex")}
M(i).slice(0,16);(0,n.createHash)("sha256").update((0,s.readFileSync)(r)).digest("hex");
'@
    # Current desktop bundles hash file bytes in bounded chunks rather than readFileSync.
    $streamedCode = @'
let i=["manifest.json","bin/node.exe","bin/node_repl.exe"].map(e=>L({destinationPath:r,executableName:e,sourcePath:(0,o.join)(t,e)})),a=M(i).slice(0,16);
function L({destinationPath:e,executableName:t,sourcePath:r}){return{digest:D({destinationPath:e,executableName:t,filePath:r,operation:"hash_source",sourcePath:r}),executableName:t,size:1,sourcePath:r}}
function M(e){let t=(0,n.createHash)("sha256");for(let r of e)t.update(r.executableName),t.update("\0"),t.update(r.digest),t.update("\0");return t.digest("hex")}
function D({destinationPath:e,executableName:t,operation:r,sourcePath:s,filePath:i}){return W({destinationPath:e,executableName:t,operation:r,sourcePath:s},()=>{let e=(0,n.createHash)("sha256");return j(i,t=>{e.update(t)}),e.digest("hex")})}
function j(e,t){let r=(0,s.openSync)(e,"r");try{let e=(0,s.fstatSync)(r).size,n=Buffer.allocUnsafe(Math.min(Math.max(1,e),262144));for(;;){let e=(0,s.readSync)(r,n);if(0===e)return;t(n.subarray(0,e))}}finally{(0,s.closeSync)(r)}}
'@
    function New-AsarFixture($Name, $Files) {
        $install = Join-Path $scratch $Name
        $resources = Join-Path $install 'app\resources'
        [void][IO.Directory]::CreateDirectory($resources)
        $entries = [ordered]@{}
        $contents = [Collections.Generic.List[byte[]]]::new()
        [long]$offset = 0
        foreach ($name in $Files.Keys) {
            $bytes = [Text.Encoding]::UTF8.GetBytes($Files[$name])
            $entries[$name] = @{size=$bytes.Length;offset=[string]$offset}
            $contents.Add($bytes)
            $offset += $bytes.Length
        }
        $header = @{files=@{'.vite'=@{files=@{build=@{files=$entries}}}}} | ConvertTo-Json -Depth 10 -Compress
        $json = [Text.Encoding]::UTF8.GetBytes($header)
        $stream = [IO.File]::Create((Join-Path $resources 'app.asar'))
        $writer = [IO.BinaryWriter]::new($stream)
        try {
            $writer.Write([uint32]4); $writer.Write([uint32]($json.Length + 8))
            $writer.Write([uint32]($json.Length + 4)); $writer.Write([uint32]$json.Length)
            $writer.Write($json)
            foreach ($bytes in $contents) { $writer.Write($bytes) }
        } finally { $writer.Dispose() }
        return $install
    }
    function Assert-RuntimeId($Install, $Message) {
        $actual = Get-RequiredRuntimeId $source $Install
        if ($actual -ne 'b5b98a31756046fb') { throw ($Message + ': unexpected runtime ID ' + $actual) }
    }
    function Assert-Rejected($Install, $Message) {
        $rejected = $false
        try { [void](Get-RequiredRuntimeId $source $Install) } catch { $rejected = $true }
        if (-not $rejected) { throw $Message }
    }
    $old = New-AsarFixture 'old' ([ordered]@{'application-network-startup-old.js'=$knownCode})
    Assert-RuntimeId $old 'Legacy bundle failed'
    $new = New-AsarFixture 'new' ([ordered]@{'early-bootstrap-491-test.js'='const unrelated = true;';'early-bootstrap-824-test.js'=$streamedCode})
    Assert-RuntimeId $new 'Renamed bundle failed'
    $streamed = New-AsarFixture 'streamed' ([ordered]@{'early-bootstrap-824-test.js'=$streamedCode})
    Assert-RuntimeId $streamed 'Chunked file hashing failed'
    $duplicate = New-AsarFixture 'duplicate' ([ordered]@{'early-bootstrap-a.js'=$knownCode;'early-bootstrap-b.js'=$knownCode})
    Assert-Rejected $duplicate 'Ambiguous bundle accepted'
    $changed = New-AsarFixture 'changed' ([ordered]@{'early-bootstrap-a.js'=$knownCode.Replace('sha256','sha512')})
    Assert-Rejected $changed 'Changed runtime algorithm accepted'
    $changedStream = New-AsarFixture 'changed-stream' ([ordered]@{'early-bootstrap-a.js'=$streamedCode.Replace('subarray(0,e)','subarray(1,e)')})
    Assert-Rejected $changedStream 'Incomplete file hashing accepted'
    $decoy = New-AsarFixture 'decoy' ([ordered]@{'early-bootstrap-a.js'=($streamedCode.Replace('subarray(0,e)','subarray(1,e)') + $knownCode)})
    Assert-Rejected $decoy 'Unrelated direct hashing masked an incomplete chunked reader'
    $wrongFile = New-AsarFixture 'wrong-file' ([ordered]@{'early-bootstrap-a.js'=$streamedCode.Replace('filePath:r','filePath:t')})
    Assert-Rejected $wrongFile 'Changed source file hashing accepted'
    $unknown = New-AsarFixture 'unknown' ([ordered]@{'other.js'=$knownCode})
    Assert-Rejected $unknown 'Unknown bundle accepted'
    Write-Output 'PASS: old and renamed runtime bundles, unique validated implementation, changed algorithm and unknown bundle.'
} finally {
    $tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
    if ([IO.Path]::GetFullPath($scratch).StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase)) { Remove-Item -LiteralPath $scratch -Recurse -Force }
}
