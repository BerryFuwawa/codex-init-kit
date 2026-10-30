function Get-BrowserAppId($Applications) {
    $apps=@($Applications|Where-Object {$_.Executable -match '^(?i:app[\\/](ChatGPT|Codex)\.exe)$' -and $_.EntryPoint -eq 'Windows.FullTrustApplication'})
    if($apps.Count -ne 1 -or -not $apps[0].Id){throw 'Cannot uniquely identify official app launch ID.'}
    return [string]$apps[0].Id
}
function Get-BrowserLaunchTarget {
    param($Package)
    if(-not $Package){$Package=Get-OfficialDesktopPackage}
    [xml]$manifest=[IO.File]::ReadAllText((Join-Path $Package.InstallLocation 'AppxManifest.xml'))
    $appId=Get-BrowserAppId @($manifest.Package.Applications.Application)
    if($Package.PackageFamilyName -ne 'OpenAI.Codex_2p2nqsd0c76g0' -or $appId -notmatch '^[A-Za-z0-9][A-Za-z0-9_.-]{0,63}$'){throw 'Unsupported official application identity.'}
    return [pscustomobject]@{packageFullName=[string]$Package.PackageFullName;packageFamilyName=[string]$Package.PackageFamilyName;appId=$appId}
}
function Get-LegacyBrowserBytes {
    param([byte[]]$Original,[string]$Proxy,[string]$Date)
    # Regenerate the entire known entry, never trust a marker or execute its contents.
    [void](New-BrowserLauncherBytes $Original $Proxy)
    $values=[ordered]@{HTTP_PROXY=$Proxy;HTTPS_PROXY=$Proxy;NO_PROXY='localhost,127.0.0.1,::1';NODE_USE_ENV_PROXY='1'}
    if($Date -eq 'startup-v1'){
        $prefix='// Codex local startup proxy v1'+"`nconst localBrowserProxyEnv = "+($values|ConvertTo-Json -Compress)+";`n"
        $suffix="`n`n"
    }else{
        $json="{`n"+(($values.Keys|ForEach-Object {'  "'+$_+'": "'+$values[$_]+'"'}) -join ",`n")+"`n}"
        $prefix='// BEGIN LOCAL CODEX BROWSER PROXY '+$Date+"`nconst localBrowserProxyEnv = "+$json+";`n"
        $suffix="`n// END LOCAL CODEX BROWSER PROXY "+$Date+"`n`n"
    }
    $block=$prefix+"Object.assign(process.env, localBrowserProxyEnv);`n"+'process.env.NODE_REPL_UNTRUSTED_ENV_ALLOWLIST = [...new Set((process.env.NODE_REPL_UNTRUSTED_ENV_ALLOWLIST || "").split(",").filter(Boolean).concat(Object.keys(localBrowserProxyEnv)))].join(",");'+$suffix
    return ,([Text.Encoding]::UTF8.GetBytes([Text.Encoding]::UTF8.GetString($Original).Replace('try {',$block+'try {')))
}
function Import-LegacyBrowserPatch {
    param($Context,[string]$Proxy,[switch]$CheckOnly)
    if(Get-BrowserManagedPatch $Context.Source $Context.Target -AllowOriginal){return $false}
    $original=[IO.File]::ReadAllBytes($Context.SourceLauncher)
    $current=[IO.File]::ReadAllBytes($Context.Launcher)
    if((Get-BrowserBytesHash $original) -eq (Get-BrowserBytesHash $current)){return $false}
    $known=$false
    foreach($date in @('20261020','20261023','20261026','20261028','startup-v1')){
        if((Get-BrowserBytesHash (Get-LegacyBrowserBytes $original $Proxy $date)) -eq (Get-BrowserBytesHash $current)){$known=$true;break}
    }
    if(-not $known){throw 'Unknown launcher changes; no overwrite attempted.'}
    # Every other component must be canonical before adopting a foreign patch.
    $expected=Get-TreeInventory $Context.Source;$actual=Get-TreeInventory $Context.Target
    $relative=$Context.Launcher.Substring($Context.Target.TrimEnd('\').Length+1)
    $actual.Files[$relative]=$expected.Files[$relative]
    if($actual.Count -ne $expected.Count -or -not $actual.Directories.SetEquals($expected.Directories)){throw 'Component inventory differs; migration stopped.'}
    foreach($key in $expected.Files.Keys){if(-not $actual.Files.ContainsKey($key) -or $actual.Files[$key].Hash -ne $expected.Files[$key].Hash){throw 'Component hash differs; migration stopped.'}}
    if($CheckOnly){return $true}
    Confirm-CodexStopped $Context.Package.InstallLocation $Context.Root
    # Verify bundled Node capability before adopting or writing a legacy patch.
    [void](Test-BrowserEnvironment $Context $Proxy)
    $root=Get-BrowserStateRoot
    [void][IO.Directory]::CreateDirectory($root);Assert-NoReparsePoint $root -Tree
    $originalHash=Get-BrowserBytesHash $original
    $backup=Join-Path $root ($Context.Id+'-'+$originalHash+'.bin')
    if(Test-Path -LiteralPath $backup){if((Get-FileDigest $backup).Hash -ne $originalHash){throw 'Original backup hash mismatch.'}}else{Write-BrowserAtomic $backup $original}
    $prior=Join-Path $root ($Context.Id+'-'+(Get-BrowserBytesHash $current)+'.legacy.bin')
    if(Test-Path -LiteralPath $prior){if((Get-FileDigest $prior).Hash -ne (Get-BrowserBytesHash $current)){throw 'Legacy backup hash mismatch.'}}else{Write-BrowserAtomic $prior $current}
    $patched=New-BrowserLauncherBytes $original $Proxy
    $record=[ordered]@{schema=1;runtimeId=$Context.Id;appVersion=[string]$Context.Package.Version;packageFullName=$Context.Package.PackageFullName;source=$Context.Source;target=$Context.Target;proxy=$Proxy;originalHash=$originalHash;patchedHash=(Get-BrowserBytesHash $patched);preparedAt=[DateTime]::UtcNow.ToString('o')}
    if((Get-OfficialDesktopPackage).PackageFullName -ne $Context.Package.PackageFullName -or (Get-FileDigest $Context.Launcher).Hash -ne (Get-BrowserBytesHash $current)){throw 'App or entry changed before migration.'}
    # If the write is interrupted, retain both originals and do not silently adopt unknown bytes.
    Write-BrowserAtomic (Join-Path $root ($Context.Id+'.json')) ([Text.Encoding]::UTF8.GetBytes(($record|ConvertTo-Json)))
    Write-BrowserAtomic $Context.Launcher $patched
    [void](Get-BrowserManagedPatch $Context.Source $Context.Target)
    return $true
}
function Invoke-BrowserStartup {
    param([string]$PayloadRoot,[int]$Port=10808,[string]$ProxyHost='127.0.0.1',[switch]$CheckOnly,[switch]$PrepareOnly,[switch]$AllowDirect,[switch]$EmitLaunchInfo)
    $context=Get-BrowserContext $PayloadRoot
    $target=Get-BrowserLaunchTarget $context.Package
    $proxy=Get-ProxyUrl $Port $ProxyHost
    if(-not $proxy -and -not $AllowDirect){throw 'Startup repair requires an HTTP/mixed proxy; use browser rollback for direct mode.'}
    if($CheckOnly -and -not $proxy){throw 'CheckOnly requires an HTTP/mixed proxy.'}
    Write-BackendLine ('Codex '+$context.Package.Version+' / runtime '+$context.Id)
    if($CheckOnly){
        if(-not(Test-Path -LiteralPath $context.Launcher)){Write-BackendLine 'Runtime missing; repair required.';return 2}
        $legacy=Import-LegacyBrowserPatch $context $proxy -CheckOnly
        if($legacy){Write-BackendLine 'Known legacy patch; migration required.';return 2}
        $patch=Get-BrowserManagedPatch $context.Source $context.Target -AllowOriginal
        if(-not $patch -or $patch.CurrentHash -ne $patch.PatchedHash -or $patch.Record.proxy -ne $proxy){Write-BackendLine 'Proxy repair required.';return 2}
        $inventory=Compare-TreeInventory (Get-TreeInventory $context.Source) $context.Target
        if(-not $inventory.Passed){throw $inventory.Reason}
        Write-BackendLine 'Managed patch ready; real Chrome connection not verified.';return 0
    }
    Confirm-CodexStopped $context.Package.InstallLocation $context.Root
    if(-not(Test-Path -LiteralPath $context.Target)){
        $run=Join-Path (Get-BrowserStateRoot) ('startup-'+[guid]::NewGuid().ToString('N'))
        [void][IO.Directory]::CreateDirectory($run)
        [void](Invoke-RuntimeRepair -SourceInventory (Get-TreeInventory $context.Source) -Target $context.Target -RuntimeRoot $context.Root -RunDirectory $run -InstallLocation $context.Package.InstallLocation)
    }
    $reachability='Direct mode; no proxy injected.'
    if(-not $proxy){
        # The common repair path restores only our verified managed patch.
        $rc=Invoke-BrowserOperation 'repair' $PayloadRoot 0 @{proxyHost=$ProxyHost}
    }else{
        [void](Import-LegacyBrowserPatch $context $proxy)
        $managed=Get-BrowserManagedPatch $context.Source $context.Target -AllowOriginal
        if($managed -and $managed.CurrentHash -eq $managed.PatchedHash -and $managed.Record.proxy -eq $proxy){
            $verified=Compare-TreeInventory (Get-TreeInventory $context.Source) $context.Target
            if(-not $verified.Passed){throw $verified.Reason}
            [void](Test-BrowserEnvironment $context $proxy)
            Write-BackendLine 'Managed patch already ready; no entry write required.'
            $rc=0
        }else{$rc=Invoke-BrowserOperation 'repair' $PayloadRoot $Port @{proxyHost=$ProxyHost}}
        $reachability=Test-BrowserProxy $proxy
        Write-BackendLine $reachability
    }
    if($rc -ne 0){return $rc}
    $current=Get-OfficialDesktopPackage
    if($current.PackageFullName -ne $context.Package.PackageFullName){throw 'App updated before launch; run again.'}
    Confirm-CodexStopped $context.Package.InstallLocation $context.Root
    if($EmitLaunchInfo){Write-BackendLine ('@@BROWSER_START@@'+(@{packageFullName=$target.packageFullName;packageFamilyName=$target.packageFamilyName;appId=$target.appId;reachability=$reachability}|ConvertTo-Json -Compress))}
    if(-not $PrepareOnly){
        Start-Process -FilePath explorer.exe -ArgumentList ('shell:AppsFolder\'+$target.packageFamilyName+'!'+$target.appId) -WindowStyle Hidden
        Write-BackendLine 'Official app launch requested. Actual browser connection still needs verification.'
    }
    return 0
}
