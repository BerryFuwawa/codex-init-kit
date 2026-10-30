# Recovery of the Store application's user-local executable cache. No action on import.
function Assert-KnownDesktopCoreAlgorithm {
    param([string]$Code,[ValidateSet('direct','streamed')][string]$Format='direct')
    # Verify the same digest algorithm and file-content hashing used by CUA, then
    # bind it to the desktop executable's four-file relocation implementation.
    Assert-KnownRuntimeAlgorithm $Code -Format $Format
    $q='[\x60\x22\x27]'
    $siblings='(?<names>[$\w]+)=\['+$q+'codex-code-mode-host\.exe'+$q+','+$q+'codex-windows-sandbox-setup\.exe'+$q+','+$q+'codex-command-runner\.exe'+$q+'\]'
    $m=[regex]::Match($Code,$siblings)
    if(-not $m.Success){throw 'Unsupported desktop executable list; no files were changed.'}
    $name=[regex]::Escape($m.Groups['names'].Value)
    $binding='windowsSiblingExecutableNames:'+$name
    $layout='\['+$q+'OpenAI'+$q+','+$q+'Codex'+$q+','+$q+'bin'+$q+','+$q+'codex\.exe'+$q+'\]'
    $hashAlgorithm='function\s+(?<name>[$\w]+)\((?<list>[$\w]+)\)\{let\s+(?<hash>[$\w]+)=\(0,[$\w]+\.createHash\)\('+$q+'sha256'+$q+'\);for\(let\s+(?<item>[$\w]+)\s+of\s+\k<list>\)\k<hash>\.update\(\k<item>\.executableName\),\k<hash>\.update\('+$q+'\\0'+$q+'\),\k<hash>\.update\(\k<item>\.digest\),\k<hash>\.update\('+$q+'\\0'+$q+'\);return\s+\k<hash>\.digest\('+$q+'hex'+$q+'\)\}'
    $hash=[regex]::Match($Code,$hashAlgorithm)
    if(-not $hash.Success){throw 'Unsupported desktop executable digest; no files were changed.'}
    $hashName=[regex]::Escape($hash.Groups['name'].Value)
    $relocate='function\s+[$\w]+\(\{executableName:(?<main>[$\w]+),sourcePath:[$\w]+,stableDestinationPath:[$\w]+,windowsSiblingExecutableNames:(?<siblings>[$\w]+)\}\)\{let\s+[$\w]+=\(0,[$\w]+\.dirname\)\([\s\S]{1,100}?(?<entries>[$\w]+)=\[\k<main>,\.\.\.\k<siblings>\]\.map\((?<item>[$\w]+)=>(?<collector>[$\w]+)\(\{destinationPath:[$\w]+,executableName:\k<item>,sourcePath:\(0,[$\w]+\.join\)\([\s\S]{1,40}?,\k<item>\)\}\)\)[\s\S]{0,100}?='+$hashName+'\(\k<entries>\)\.slice\(0,16\)'
    $relocation=[regex]::Match($Code,$relocate)
    if(-not [regex]::IsMatch($Code,$binding) -or -not [regex]::IsMatch($Code,$layout) -or -not $relocation.Success){throw 'Unsupported desktop executable cache layout; no files were changed.'}
    $collector=[regex]::Escape($relocation.Groups['collector'].Value)
    $cuaList='\['+$q+'manifest\.json'+$q+','+$q+'bin/node\.exe'+$q+','+$q+'bin/node_repl\.exe'+$q+'\]\.map\((?<item>[$\w]+)=>'+$collector+'\(\{destinationPath:[$\w]+,executableName:\k<item>,sourcePath:\(0,[$\w]+\.join\)\([\s\S]{1,40}?,\k<item>\)\}\)\)'
    $source='function\s+'+$collector+'\(\{destinationPath:(?<dest>[$\w]+),executableName:(?<exe>[$\w]+),sourcePath:(?<path>[$\w]+)\}\)\{[\s\S]{0,1800}?return\{digest:[$\w]+\(\{destinationPath:\k<dest>,executableName:\k<exe>,filePath:\k<path>,operation:'+$q+'hash_source'+$q+',sourcePath:\k<path>\}\),executableName:\k<exe>,'
    if(-not [regex]::IsMatch($Code,$cuaList) -or -not [regex]::IsMatch($Code,$source)){throw 'Desktop source hashing is not tied to the verified runtime implementation; no files were changed.'}
}
function Get-DesktopCoreFiles {
    @('codex.exe','codex-code-mode-host.exe','codex-windows-sandbox-setup.exe','codex-command-runner.exe')
}
function Get-DesktopCoreId {
    param($Files)
    $combined=[Text.StringBuilder]::new()
    foreach($file in $Files){[void]$combined.Append($file.Name);[void]$combined.Append([char]0);[void]$combined.Append($file.Hash);[void]$combined.Append([char]0)}
    $sha=[Security.Cryptography.SHA256]::Create()
    try{return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($combined.ToString()))).Replace('-','').ToLowerInvariant().Substring(0,16)}finally{$sha.Dispose()}
}
function Assert-SafeDesktopCorePath {
    param([string]$Path,[string]$LocalData)
    $local=Get-FullDirectoryPath $LocalData
    $root=Get-FullDirectoryPath (Join-Path $local 'OpenAI\Codex\bin')
    $full=Get-FullDirectoryPath $Path
    if(-not $full.Equals($root,[StringComparison]::OrdinalIgnoreCase)){[void](Assert-ContainedPath $full $root)}
    $cursor=$full
    while($cursor.StartsWith($local+'\',[StringComparison]::OrdinalIgnoreCase)){Assert-NoReparsePoint $cursor;$cursor=Split-Path -Parent $cursor}
    Assert-NoReparsePoint $local
}
function Get-DesktopCoreContext {
    param([string]$PayloadRoot)
    Import-CuaLibrary $PayloadRoot
    $package=Get-OfficialDesktopPackage
    Assert-DesktopReady $package
    $source=Join-Path $package.InstallLocation 'app\resources'
    foreach($path in @($package.InstallLocation,(Join-Path $package.InstallLocation 'app'),$source)){Assert-NoReparsePoint $path}
    $implementation=Get-AppStartupImplementation $package.InstallLocation
    $format=if($implementation.Name -match '^application-network-startup-'){'direct'}else{'streamed'}
    Assert-KnownDesktopCoreAlgorithm $implementation.Code -Format $format
    $files=@(foreach($name in (Get-DesktopCoreFiles)){$file=Join-Path $source $name;Assert-NoReparsePoint $file;$digest=Get-FileDigest $file;[pscustomobject]@{Name=$name;Source=$file;Hash=$digest.Hash;Length=$digest.Length}})
    $id=Get-DesktopCoreId $files
    $root=Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\bin'
    $target=Assert-ContainedPath (Join-Path $root $id) $root
    Assert-SafeDesktopCorePath $target $env:LOCALAPPDATA
    return [pscustomobject]@{Package=$package;Source=$source;Files=$files;Id=$id;Root=$root;Target=$target}
}
function Test-DesktopCoreTarget {
    param($Context)
    Assert-SafeDesktopCorePath $Context.Target $env:LOCALAPPDATA
    if(-not(Test-Path -LiteralPath $Context.Target)){return 'missing'}
    if(-not(Test-Path -LiteralPath $Context.Target -PathType Container)){return 'conflict'}
    Assert-NoReparsePoint $Context.Target -Tree
    foreach($file in $Context.Files){
        $target=Join-Path $Context.Target $file.Name
        if(-not(Test-Path -LiteralPath $target -PathType Leaf)){return 'conflict'}
        $digest=Get-FileDigest $target
        if($digest.Hash -ne $file.Hash -or $digest.Length -ne $file.Length){return 'conflict'}
    }
    return 'ready'
}
function New-DesktopCoreDirectory {
    param([string]$Path)
    if(-not ('CodexKitExclusiveDirectory' -as [type])){
        Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class CodexKitExclusiveDirectory {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    public static extern bool CreateDirectory(string path, IntPtr attributes);
}
'@
    }
    if(-not [CodexKitExclusiveDirectory]::CreateDirectory($Path,[IntPtr]::Zero)){throw (New-Object ComponentModel.Win32Exception([Runtime.InteropServices.Marshal]::GetLastWin32Error()))}
}
function Get-DesktopCoreLogSignal {
    # Scan only a bounded recent tail; never include raw logs or their paths in reports.
    $roots=@((Join-Path $env:LOCALAPPDATA 'Codex\Logs'),(Join-Path $env:LOCALAPPDATA 'Packages\OpenAI.Codex_2p2nqsd0c76g0\LocalCache\Local\Codex\Logs'))
    $files=@()
    foreach($root in $roots){
        for($day=0;$day -lt 7;$day++){
            $directory=Join-Path $root ((Get-Date).AddDays(-$day).ToString('yyyy\MM\dd'))
            try{Assert-NoReparsePoint $directory;if(Test-Path -LiteralPath $directory -PathType Container){$files+=@(Get-ChildItem -LiteralPath $directory -Filter 'codex-desktop*.log' -File -ErrorAction SilentlyContinue)}}catch{}
        }
    }
    foreach($file in @($files|Sort-Object LastWriteTime -Descending|Select-Object -First 5)){
        $stream=$null;$reader=$null
        try{
            Assert-NoReparsePoint $file.FullName
            $stream=[IO.File]::Open($file.FullName,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::ReadWrite)
            $stream.Position=[Math]::Max(0,$stream.Length-4194304)
            $reader=[IO.StreamReader]::new($stream,[Text.Encoding]::UTF8,$true)
            while(-not $reader.EndOfStream){$line=$reader.ReadLine();if($line -match 'bundled_executable_relocation_failed' -and $line -match 'rename_staging' -and $line -match 'EPERM' -and $line -match 'codex\.exe'){return $true}}
        }catch{}finally{if($reader){$reader.Dispose()}elseif($stream){$stream.Dispose()}}
    }
    return $false
}
function Invoke-DesktopCoreOperation {
    param([ValidateSet('check','repair')][string]$Mode,[string]$PayloadRoot)
    $report=[ordered]@{summary='未完成';status='incomplete';version='未检测';target='';signal='未检查';error=''}
    $mutex=$null;$acquired=$false
    try{
        $sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        $mutex=[Threading.Mutex]::new($false,('Local\CodexCoreRecovery-'+$sid))
        try{$acquired=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$acquired=$true}
        if(-not $acquired){throw 'Another desktop startup check or repair is running.'}
        if($Mode -eq 'repair'){
            Import-CuaLibrary $PayloadRoot
            $before=Get-OfficialDesktopPackage
            Confirm-CodexStopped $before.InstallLocation (Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\bin')
        }
        $context=Get-DesktopCoreContext $PayloadRoot
        $report.version=[string]$context.Package.Version;$report.target=$context.Target
        $report.signal=if(Get-DesktopCoreLogSignal){'近期日志出现 EPERM / rename_staging；具体原因仍需确认。'}else{'近期日志未发现该特征；其他启动故障需另外排查。'}
        $state=Test-DesktopCoreTarget $context
        if($state -eq 'conflict'){throw 'The target already exists but its files are incomplete or differ from the official package. No overwrite was attempted.'}
        if($Mode -eq 'repair' -and $state -eq 'missing'){
            Confirm-CodexStopped $context.Package.InstallLocation $context.Root
            $current=Get-OfficialDesktopPackage;Assert-DesktopReady $current
            if($current.PackageFullName -ne $context.Package.PackageFullName){throw 'Desktop package changed during inspection; recheck the new version.'}
            Assert-SafeDesktopCorePath $context.Target $env:LOCALAPPDATA
            [void][IO.Directory]::CreateDirectory($context.Root)
            Assert-SafeDesktopCorePath $context.Target $env:LOCALAPPDATA
            New-DesktopCoreDirectory $context.Target
            foreach($file in $context.Files){
                Confirm-CodexStopped $context.Package.InstallLocation $context.Root
                $input=$null;$output=$null
                try{
                    $input=[IO.File]::Open($file.Source,[IO.FileMode]::Open,[IO.FileAccess]::Read,[IO.FileShare]::Read)
                    $output=[IO.File]::Open((Join-Path $context.Target $file.Name),[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
                    $input.CopyTo($output)
                }finally{if($output){$output.Dispose()};if($input){$input.Dispose()}}
                $copied=Get-FileDigest (Join-Path $context.Target $file.Name)
                if($copied.Hash -ne $file.Hash -or $copied.Length -ne $file.Length){throw ('Copied file verification failed: '+$file.Name)}
            }
            $current=Get-OfficialDesktopPackage;Assert-DesktopReady $current
            if($current.PackageFullName -ne $context.Package.PackageFullName){throw 'Desktop package changed during recovery; recheck the new version.'}
            if((Test-DesktopCoreTarget $context) -ne 'ready'){throw 'Final file verification failed.'}
            $state='ready'
        }
        $report.status=$state
        $override=@('User','Machine')|Where-Object {[Environment]::GetEnvironmentVariable('CODEX_CLI_PATH',$_)}
        $extra=if(@($override).Count){' 检测到 CLI 路径覆盖；如仍无法启动，请检查启动保护设置。'}else{''}
        $report.summary=if($state -eq 'ready'){'当前版本的四个启动组件已核对。请启动 Codex 验证；组件完整不代表启动一定成功。'+$extra}else{'当前版本的启动组件目录缺失。退出 Codex 后可尝试修复；这不是所有启动问题的通用解决方案。'+$extra}
        return 0
    }catch{
        $report.error=$_.Exception.Message
        $report.summary='桌面启动检查或修复未完成：'+$report.error+'。已有文件未被覆盖；若中途复制失败，请检查该目录，勿反复重试。'
        return 2
    }finally{
        if($acquired -and $mutex){$mutex.ReleaseMutex()};if($mutex){$mutex.Dispose()}
        Write-BackendLine ('@@DESKTOP_CORE@@'+($report|ConvertTo-Json -Compress -Depth 5))
    }
}
