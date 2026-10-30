# v2.2 operations. Imported by Backend.ps1; no action executes on import.
function Import-CuaLibrary {
    param([string]$PayloadRoot)
    $code=[IO.File]::ReadAllText((Assert-PayloadFile $PayloadRoot 'cua.cmd'),[Text.Encoding]::UTF8)
    $marker='#==POWERSHELL=='
    $code=$code.Substring($code.LastIndexOf($marker)+$marker.Length)
    $tokens=$null;$errors=$null
    $ast=[Management.Automation.Language.Parser]::ParseInput($code,[ref]$tokens,[ref]$errors)
    if($errors.Count){throw 'CUA library syntax is invalid.'}
    foreach($fn in $ast.FindAll({param($n)$n -is [Management.Automation.Language.FunctionDefinitionAst]},$false)){
        Set-Item ('Function:\script:'+$fn.Name) $fn.Body.GetScriptBlock()
    }
}
function Get-OfficialDesktopPackage {
    $packages=@(Get-AppxPackage -Name OpenAI.Codex -ErrorAction Stop)
    if($packages.Count -ne 1){throw 'Expected exactly one installed official desktop package; no cross-channel installation attempted.'}
    $package=$packages[0]
    if($package.PackageFamilyName -ne 'OpenAI.Codex_2p2nqsd0c76g0' -or [string]$package.SignatureKind -ne 'Store' -or -not $package.InstallLocation){throw 'Installed package is not the supported official Store channel.'}
    return $package
}
function Assert-DesktopReady {
    param($Package)
    if([string]$Package.Status -ne 'Ok'){throw ('Desktop package is pending or unhealthy: '+$Package.Status)}
    $info=Get-ReadOnlyDesktopInfo
    if(-not $info.Found -or -not $info.Healthy -or $info.InstallLocation -ne $Package.InstallLocation){throw 'Desktop components are incomplete or the app changed during inspection.'}
}
function Assert-DesktopStopped {
    param([string]$PayloadRoot,[switch]$AllowOtherChannel)
    Import-CuaLibrary $PayloadRoot
    if($AllowOtherChannel){
        $package=Get-ReadOnlyDesktopInfo
        if(-not $package.Found){
            if(@(Get-Process -Name codex,ChatGPT -ErrorAction SilentlyContinue).Count){throw 'Codex is running. Save work and fully exit before continuing.'}
            return $package
        }
    }else{$package=Get-OfficialDesktopPackage}
    $runtimeRoot=Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\runtimes\cua_node'
    Confirm-CodexStopped $package.InstallLocation $runtimeRoot
    return $package
}
function Test-DesktopCli {
    param($Package,[string]$PayloadRoot)
    Initialize-GuardRuntime (Assert-PayloadFile $PayloadRoot 'guard.cmd')
    $cli=Join-Path $Package.InstallLocation 'app\resources\codex.exe'
    if(-not(Test-OpenAISignature $cli)){throw 'Updated native CLI failed its OpenAI signature check.'}
    $probe=Invoke-BoundedProcess $cli @('--version') 30
    if($probe.TimedOut -or $probe.ExitCode -ne 0 -or $probe.Output -notmatch 'codex[^\r\n]*\d+\.\d+'){throw 'Updated native CLI failed its version probe.'}
    Write-BackendLine ('Native CLI verified: '+$probe.Output.Trim())
}
function Invoke-BoundedProcess {
    param([string]$FilePath,[string[]]$Arguments,[int]$TimeoutSeconds=1200,[hashtable]$Environment=@{})
    $psi=[Diagnostics.ProcessStartInfo]::new()
    $psi.FileName=$FilePath
    $psi.Arguments=($Arguments|ForEach-Object {if($_ -match '["\r\n]'){throw 'Unsafe process argument.'};'"'+$_+'"'}) -join ' '
    $psi.UseShellExecute=$false;$psi.CreateNoWindow=$true
    $psi.RedirectStandardInput=$true;$psi.RedirectStandardOutput=$true;$psi.RedirectStandardError=$true
    $psi.StandardOutputEncoding=[Text.Encoding]::UTF8;$psi.StandardErrorEncoding=[Text.Encoding]::UTF8
    foreach($key in $Environment.Keys){$psi.EnvironmentVariables[$key]=[string]$Environment[$key]}
    $process=[Diagnostics.Process]::new();$process.StartInfo=$psi
    try {
        [void]$process.Start();$process.StandardInput.Close()
        $stdout=$process.StandardOutput.ReadToEndAsync();$stderr=$process.StandardError.ReadToEndAsync()
        $watch=[Diagnostics.Stopwatch]::StartNew();$timedOut=$false
        while(-not $process.WaitForExit(1000)){
            if($watch.Elapsed.TotalSeconds -ge $TimeoutSeconds){$timedOut=$true;try{$process.Kill()}catch{};break}
            if([int]$watch.Elapsed.TotalSeconds % 30 -eq 0){Write-BackendLine ('Waiting for process completion ('+[int]$watch.Elapsed.TotalSeconds+'s)...')}
        }
        if(-not $process.WaitForExit(5000)){throw 'Process did not stop; completion is still pending.'}
        # A Store installer may outlive winget and retain its output handles.
        # Never let those handles extend the command's deadline indefinitely.
        $output=if($stdout.Wait(5000)){$stdout.GetAwaiter().GetResult()}else{'Output pipe still open; installer completion pending.'}
        $errorOutput=if($stderr.Wait(5000)){$stderr.GetAwaiter().GetResult()}else{'Error pipe still open; installer completion pending.'}
        if(-not $stdout.IsCompleted -or -not $stderr.IsCompleted){$timedOut=$true}
        Write-BackendLine $output;Write-BackendLine $errorOutput
        return [pscustomobject]@{ExitCode=$process.ExitCode;Output=$output;Error=$errorOutput;TimedOut=$timedOut}
    } finally {$process.Dispose()}
}
function Invoke-DesktopUpdate {
    param([string]$PayloadRoot)
    $before=Assert-DesktopStopped $PayloadRoot
    Assert-DesktopReady $before
    $winget=Get-Command winget.exe -ErrorAction SilentlyContinue
    if(-not $winget){throw 'winget is missing. Update in Microsoft Store: ms-windows-store://pdp/?ProductId=9PLM9XGG6VKS'}
    $result=Invoke-BoundedProcess $winget.Source @('upgrade','--id','9PLM9XGG6VKS','--exact','--source','msstore','--silent','--accept-source-agreements','--accept-package-agreements','--disable-interactivity') 1200
    $after=Get-OfficialDesktopPackage
    Assert-DesktopReady $after
    if($result.TimedOut){throw 'Desktop update timed out; Store may still be completing the update. Recheck before repairing dependent components.'}
    $unsigned=[long]$result.ExitCode -band 0xffffffffL
    if($unsigned -in @(3010,1641,0x8a150109L)){throw 'Desktop update requires restart or is still pending; dependent repairs were paused.'}
    if($result.ExitCode -eq 0 -and [version]$after.Version -gt [version]$before.Version){Test-DesktopCli $after $PayloadRoot;Write-BackendLine ('Desktop updated: '+$before.Version+' -> '+$after.Version);return 'updated'}
    if($unsigned -eq 0x8a15002bL -and [version]$after.Version -eq [version]$before.Version){Test-DesktopCli $after $PayloadRoot;Write-BackendLine ('Desktop already up to date: '+$after.Version);return 'latest'}
    if($result.ExitCode -eq 0){throw 'winget completed but the installed version has not changed; completion is not verified. Recheck Store.'}
    throw ('Desktop update failed. Exit code '+$result.ExitCode+'. Microsoft Store: ms-windows-store://pdp/?ProductId=9PLM9XGG6VKS')
}
function Invoke-OfficialCliUpdate {
    param([string]$PayloadRoot,[switch]$OnlyIfMissing)
    [void](Assert-DesktopStopped $PayloadRoot -AllowOtherChannel)
    Initialize-GuardRuntime (Assert-PayloadFile $PayloadRoot 'guard.cmd')
    # Import only; do not install guard files, login tasks, or select a CLI mode.
    $paths=Get-ManagedPaths
    $package=Test-CanonicalPackage $paths.Root
    if($OnlyIfMissing -and $package.Healthy){return 0}
    $before=@{}
    foreach($scope in @('User','Machine')){foreach($name in @('CODEX_CLI_PATH','CODEX_CODE_MODE_HOST_PATH')){$before[$scope+':'+$name]=[Environment]::GetEnvironmentVariable($name,$scope)}}
    try {
        $installed=@(Invoke-OfficialInstaller)
        if(-not($installed|Where-Object {$_ -is [bool] -and $_})){throw 'Official standalone CLI installer did not complete.'}
        $package=Test-CanonicalPackage $paths.Root
        if(-not $package.Healthy){throw ('Official standalone CLI is incomplete: '+($package.Problems -join ', '))}
        Write-BackendLine ('Official standalone CLI ready: '+$package.Version)
        return 0
    } finally {
        foreach($scope in @('User','Machine')){foreach($name in @('CODEX_CLI_PATH','CODEX_CODE_MODE_HOST_PATH')){
            if([Environment]::GetEnvironmentVariable($name,$scope) -ne $before[$scope+':'+$name]){[Environment]::SetEnvironmentVariable($name,$before[$scope+':'+$name],$scope)}
        }}
    }
}
function Get-BrowserContext {
    param([string]$PayloadRoot)
    Import-CuaLibrary $PayloadRoot
    $package=Get-OfficialDesktopPackage
    Assert-DesktopReady $package
    $source=Get-FullDirectoryPath (Join-Path $package.InstallLocation 'app\resources\cua_node')
    Assert-NoReparsePoint $source -Tree
    $id=Get-RequiredRuntimeId $source $package.InstallLocation
    $root=Join-Path $env:LOCALAPPDATA 'OpenAI\Codex\runtimes\cua_node'
    $target=Assert-ContainedPath (Join-Path $root $id) $root
    Assert-NoReparsePoint $target -Tree
    $launcher=Get-BrowserLauncher $source
    $relative=$launcher.Substring($source.Length+1)
    return [pscustomobject]@{Package=$package;Source=$source;Target=$target;Root=$root;Id=$id;SourceLauncher=$launcher;Launcher=(Join-Path $target $relative)}
}
function Test-BrowserProxy {
    param([string]$Proxy)
    if(-not $Proxy){return '直连（代理关闭）'}
    $uri=[uri]$Proxy;$client=[Net.Sockets.TcpClient]::new()
    try {
        $task=$client.ConnectAsync($uri.DnsSafeHost.Trim('[',']'),$uri.Port)
        if(-not $task.Wait(3000)) {return '代理不可达（连接超时）'}
        [void]$task.GetAwaiter().GetResult()
        return '代理端口可达（TCP）；尚未验证 Chrome 或代理认证'
    } catch {return ('代理不可达：'+$_.Exception.Message)} finally {$client.Dispose()}
}
function Test-BrowserEnvironment {
    param($Context,[string]$Proxy)
    # Execute the verified cache copy: WindowsApps may prohibit launching
    # bundled executables directly outside the packaged application.
    $node=Join-Path $Context.Target 'bin\node.exe'
    $capability=Invoke-BoundedProcess $node @('-p',"JSON.stringify({proxy:process.allowedNodeEnvironmentFlags.has('--use-env-proxy')})") 15
    if($capability.ExitCode -ne 0 -or $capability.Output -notmatch '"proxy":true'){throw 'Bundled Node does not advertise environment proxy support.'}
    $values=@{HTTP_PROXY=$Proxy;HTTPS_PROXY=$Proxy;NO_PROXY='localhost,127.0.0.1,::1';NODE_USE_ENV_PROXY='1';NODE_REPL_UNTRUSTED_ENV_ALLOWLIST='HTTP_PROXY,HTTPS_PROXY,NO_PROXY,NODE_USE_ENV_PROXY'}
    $script='const {spawnSync}=require("node:child_process");const keys=process.env.NODE_REPL_UNTRUSTED_ENV_ALLOWLIST.split(",");const env=Object.fromEntries(keys.map(k=>[k,process.env[k]]));const r=spawnSync(process.execPath,["-p","JSON.stringify(process.env)"],{env,encoding:"utf8"});if(r.status!==0)process.exit(1);const e=JSON.parse(r.stdout);for(const k of keys)if(e[k]!==process.env[k])process.exit(2);console.log("ENV_INHERITANCE_PASS");'
    $probeRoot=Join-Path ([IO.Path]::GetTempPath()) ('CodexKit-browser-probe-'+[guid]::NewGuid().ToString('N'))
    [void][IO.Directory]::CreateDirectory($probeRoot)
    $probeFile=Join-Path $probeRoot 'probe.cjs'
    try {
        [IO.File]::WriteAllText($probeFile,$script,[Text.UTF8Encoding]::new($false))
        $probe=Invoke-BoundedProcess $node @($probeFile) 15 $values
        if($probe.ExitCode -ne 0 -or $probe.Output -notmatch 'ENV_INHERITANCE_PASS'){throw 'Environment inheritance probe failed.'}
        return 'Node 代理能力及隔离子进程变量继承通过；CUA 沙箱真实连接待重启验证'
    } finally {Remove-Item -LiteralPath $probeFile -Force -ErrorAction SilentlyContinue;[IO.Directory]::Delete($probeRoot)}
}
function Invoke-BrowserOperation {
    param([ValidateSet('check','repair','rollback')][string]$Mode,[string]$PayloadRoot,[int]$Port,[object]$Options)
    $report=[ordered]@{desktopVersion='未检测';runtime='未检测';proxy=(Get-ProxyUrl $Port $Options.proxyHost);patch='未检查';integrity='未检查';reachability='未检查';environment='未检查';connection='重启后真实浏览器连接：待验证';checkedAt=(Get-Date -Format 'yyyy-MM-dd HH:mm:ss');error=''}
    $mutex=$null;$acquired=$false
    try {
        $sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        $mutex=[Threading.Mutex]::new($false,('Local\CodexCUARepair-'+$sid))
        try{$acquired=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$acquired=$true}
        if(-not $acquired){throw 'Another CUA or browser check/repair is running. Wait for it to finish.'}
        $context=Get-BrowserContext $PayloadRoot
        $report.desktopVersion=[string]$context.Package.Version;$report.runtime=$context.Target
        if($Mode -ne 'check'){Confirm-CodexStopped $context.Package.InstallLocation $context.Root}
        $patch=Get-BrowserManagedPatch $context.Source $context.Target -AllowOriginal
        if($patch -and $patch.Record.packageFullName -ne $context.Package.PackageFullName){throw 'Managed patch app version differs; rollback refused.'}
        $original=[IO.File]::ReadAllBytes($context.SourceLauncher)
        $originalHash=Get-BrowserBytesHash $original
        $currentHash=(Get-FileDigest $context.Launcher).Hash
        if(-not $patch -and $currentHash -ne $originalHash){throw 'Launcher has unrecognized changes; no overwrite attempted. Inspect CUA components first.'}
        $inventory=Get-TreeInventory $context.Source
        $check=Compare-TreeInventory $inventory $context.Target
        $report.integrity=$check.Reason
        if(-not $check.Passed){throw ('Component integrity check failed: '+$check.Reason)}
        $report.patch=if($patch -and $currentHash -eq $patch.PatchedHash){'受管理补丁已准备；真实连接待重启验证'}else{'原始直连入口'}
        if($Mode -ne 'rollback' -and -not($Mode -eq 'repair' -and -not $report.proxy)){
            $report.reachability=Test-BrowserProxy $report.proxy
            $report.environment=Test-BrowserEnvironment $context $report.proxy
        }
        if($Mode -eq 'rollback' -or ($Mode -eq 'repair' -and -not $report.proxy)){
            if($patch -and $currentHash -eq $patch.PatchedHash){
                $latest=Get-BrowserContext $PayloadRoot
                if($latest.Package.PackageFullName -ne $context.Package.PackageFullName){throw 'App version changed before rollback.'}
                $patch=Get-BrowserManagedPatch $context.Source $context.Target
                Confirm-CodexStopped $context.Package.InstallLocation $context.Root
                Write-BrowserAtomic $context.Launcher $patch.Original
            }
            $report.patch='已恢复原始直连入口（或原本未修改）'
        } elseif($Mode -eq 'repair') {
            $bytes=New-BrowserLauncherBytes $original $report.proxy
            $stateRoot=Get-BrowserStateRoot
            [void][IO.Directory]::CreateDirectory($stateRoot)
            Assert-NoReparsePoint $stateRoot -Tree
            $backup=Join-Path $stateRoot ($context.Id+'-'+$originalHash+'.bin')
            if(Test-Path -LiteralPath $backup){if((Get-FileDigest $backup).Hash -ne $originalHash){throw 'Existing original backup failed hash verification.'}}
            else {Write-BrowserAtomic $backup $original}
            $record=[ordered]@{schema=1;runtimeId=$context.Id;appVersion=[string]$context.Package.Version;packageFullName=$context.Package.PackageFullName;source=$context.Source;target=$context.Target;proxy=$report.proxy;originalHash=$originalHash;patchedHash=(Get-BrowserBytesHash $bytes);preparedAt=[DateTime]::UtcNow.ToString('o')}
            $latest=Get-BrowserContext $PayloadRoot
            if($latest.Package.PackageFullName -ne $context.Package.PackageFullName -or (Get-FileDigest $context.SourceLauncher).Hash -ne $originalHash){throw 'Official app changed before patch; stopped.'}
            if((Get-FileDigest $context.Launcher).Hash -ne $currentHash){throw 'Launcher changed before patch; stopped.'}
            Confirm-CodexStopped $context.Package.InstallLocation $context.Root
            # Commit journal first: AllowOriginal permits a safe retry after interruption.
            Write-BrowserAtomic (Join-Path $stateRoot ($context.Id+'.json')) ([Text.Encoding]::UTF8.GetBytes(($record|ConvertTo-Json -Depth 5)))
            Write-BrowserAtomic $context.Launcher $bytes
            [void](Get-BrowserManagedPatch $context.Source $context.Target)
            $report.patch='修复已准备；请重新启动 Codex 并运行连接测试'
        }
        return 0
    } catch {$report.error=$_.Exception.Message;$report.patch='未完成：'+$report.error;Write-BackendLine ('[BROWSER INCOMPLETE] '+$report.error);return 2}
    finally {
        if($acquired -and $mutex){$mutex.ReleaseMutex()};if($mutex){$mutex.Dispose()}
        Write-BackendLine ('@@BROWSER@@'+($report|ConvertTo-Json -Compress -Depth 5))
        $root=Get-BrowserStateRoot
        [void][IO.Directory]::CreateDirectory($root)
        Write-BrowserAtomic (Join-Path $root 'last-check.json') ([Text.Encoding]::UTF8.GetBytes(($report|ConvertTo-Json -Depth 5)))
    }
}
function Invoke-BrowserStartWorker {
    param([string]$PayloadRoot,[int]$Port,[object]$Options,[switch]$VerifyOnly)
    . (Assert-PayloadFile $PayloadRoot 'BrowserStartup.ps1')
    if($VerifyOnly){
        $package=Get-OfficialDesktopPackage
        Assert-DesktopReady $package
        $target=Get-BrowserLaunchTarget $package
        Write-BackendLine ('@@BROWSER_START@@'+($target|ConvertTo-Json -Compress))
        return 0
    }
    $mutex=$null;$acquired=$false
    try {
        $sid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
        $mutex=[Threading.Mutex]::new($false,('Local\CodexCUARepair-'+$sid))
        try{$acquired=$mutex.WaitOne(0)}catch [Threading.AbandonedMutexException]{$acquired=$true}
        if(-not $acquired){throw 'Another CUA or browser operation is running.'}
        return (Invoke-BrowserStartup -PayloadRoot $PayloadRoot -Port $Port -ProxyHost $Options.proxyHost -AllowDirect -PrepareOnly -EmitLaunchInfo)
    } finally {
        if($acquired -and $mutex){$mutex.ReleaseMutex()};if($mutex){$mutex.Dispose()}
    }
}
function Invoke-Initialize220 {
    param($InputData)
    [void](Assert-DesktopStopped $InputData.PayloadRoot -AllowOtherChannel)
    $opts=$InputData.Options
    $steps=[Collections.Generic.List[object]]::new()
    if($opts.desktopUpdate){$steps.Add(@{Name='更新桌面版（含原生 CLI）';Kind='desktop'})}
    $steps.Add(@{Name='重建配置、目录及 CUA';Kind='base'})
    if($opts.officialCliUpdate){$steps.Add(@{Name='更新官方独立 CLI';Kind='official'})}
    if($InputData.InstallProtection){$steps.Add(@{Name='安装启动保护';Kind='guard'})}
    if($opts.browserRepair){$steps.Add(@{Name='浏览器连接修复';Kind='browser'})}
    $results=[Collections.Generic.List[object]]::new();$index=0;$desktopSafe=$true;$basePassed=$false
    $script:InitializationProgressActive=$true
    try {
        foreach($step in $steps){
            $index++;Write-BackendLine ('@@PROGRESS@@'+(@{step=$index;total=$steps.Count;title=$step.Name}|ConvertTo-Json -Compress))
            try {
                switch($step.Kind){
                    'desktop' {
                        try {[void](Invoke-DesktopUpdate $InputData.PayloadRoot)}
                        catch {
                            # A failed network/source lookup can still leave a usable desktop.
                            # Pending/restart/timeout is conservatively treated as unsafe.
                            $message=$_.Exception.Message
                            if($message -match '(?i)pending|restart|timed out|not changed|still completing'){$desktopSafe=$false}
                            else {try {$remaining=Get-OfficialDesktopPackage;Assert-DesktopReady $remaining;Test-DesktopCli $remaining $InputData.PayloadRoot}catch{$desktopSafe=$false}}
                            throw
                        }
                    }
                    'base' {
                        $baseOpts=@{};foreach($key in $opts.Keys){$baseOpts[$key]=$opts[$key]}
                        if(-not $desktopSafe -and $baseOpts.cuaRepair){$baseOpts.cuaRepair=$false;$results.Add(@{name='CUA 检查与修复';state='incomplete';message='桌面更新待完成，已暂停依赖修复'})}
                        [void](Invoke-InitOperation 'one-click' $InputData.PayloadRoot $InputData.Drive $InputData.ProxyPort $baseOpts)
                        $basePassed=$true
                    }
                    'official' {[void](Invoke-OfficialCliUpdate $InputData.PayloadRoot)}
                    'guard' {if(-not $desktopSafe){throw 'Desktop update pending; startup protection paused.'};[void](Invoke-GuardOperation 'guard-install' $InputData.PayloadRoot)}
                    'browser' {if(-not $desktopSafe){throw 'Desktop update pending; browser repair paused.'};if((Invoke-BrowserOperation 'repair' $InputData.PayloadRoot $InputData.ProxyPort $opts) -ne 0){throw '浏览器修复未完成，可在浏览器连接页单独重试。'}}
                }
                $results.Add(@{name=$step.Name;state='complete';message='完成'})
            } catch {$results.Add(@{name=$step.Name;state='incomplete';message=$_.Exception.Message});Write-BackendLine ('[INCOMPLETE] '+$step.Name+': '+$_.Exception.Message)}
        }
    } finally {$script:InitializationProgressActive=$false}
    $incomplete=@($results|Where-Object {$_.state -ne 'complete'})
    $state=if(-not $basePassed){'failed'}elseif($incomplete.Count){'partial'}else{'complete'}
    $summary=($results|ForEach-Object {$_.name+'：'+$_.message}) -join "`n"
    Write-BackendLine ('@@INIT_RESULT@@'+(@{state=$state;summary=$summary;steps=@($results.ToArray())}|ConvertTo-Json -Compress -Depth 6))
    if($state -eq 'failed'){return 1};if($state -eq 'partial'){return 2};return 0
}
