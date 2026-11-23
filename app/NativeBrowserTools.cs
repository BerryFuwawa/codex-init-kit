using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CodexKit.Native;
using Microsoft.Win32;

namespace CodexKit {
internal enum BrowserToolInstallation { None, Managed, External }
internal sealed class NativeBrowserTools {
    internal const string PlaywrightStage="playwright",ChromeDevToolsStage="chrome-devtools";
    internal const string RemovePlaywrightOperation="uninstall-playwright",RemoveChromeDevToolsOperation="uninstall-chrome-devtools";
    internal const string InstallFirefoxOperation="install-playwright-firefox";
    const string PlaywrightVersion="0.1.25",ChromeDevToolsVersion="1.13.0",NodeVersion="24.22.3";
    const string PlaywrightPackage="@playwright/cli",ChromePackage="chrome-devtools-mcp";
    const string PlaywrightBin="playwright-cli.js",ChromeBin=@"out\Default\gen\src\bin\chrome-devtools-mcp.js";
    const string NodeArchive="node-v"+NodeVersion+"-win-x64";
    const string SkillMarker="<!-- Managed by Codex Init Kit -->";
    const string NoFirefoxShortcut="pref(\"browser.privacySegmentation.createdShortcut\", true);";
    readonly NativePaths paths;
    readonly NativeServices services;
    readonly string root,proxy;
    internal bool IntegrationChanged{get;private set;}

    internal NativeBrowserTools(NativePaths paths,NativeServices services,string proxy){
        this.paths=paths;
        this.services=services;
        this.proxy=proxy;
        root=Path.Combine(paths.Home,"browser-tools");
    }

    string NodeDirectory{get{return Path.Combine(root,NodeArchive);}}
    string Node{get{return Path.Combine(NodeDirectory,"node.exe");}}
    string Npm{get{return Path.Combine(NodeDirectory,"node_modules","npm","bin","npm-cli.js");}}
    string BrowserCache{get{return Path.Combine(root,"playwright-browsers");}}
    string PlaywrightLauncher{get{return Path.Combine(root,"playwright-launcher.cjs");}}

    internal BrowserToolInstallation PlaywrightInstallation(){
        if(OwnPlaywrightSkill()&&File.Exists(Node)&&File.Exists(PlaywrightLauncher)
            &&File.Exists(PackageEntry(PlaywrightPackage,PlaywrightVersion,PlaywrightBin)))
            return BrowserToolInstallation.Managed;
        var search=(paths.SearchPath??"").Split(';').Select(path=>Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
        if(search.Where(Path.IsPathRooted).Any(path=>File.Exists(Path.Combine(path,"playwright-cli.cmd"))
            &&File.Exists(Path.Combine(path,"node_modules","@playwright","cli",PlaywrightBin))))
            return BrowserToolInstallation.External;
        return BrowserToolInstallation.None;
    }

    internal BrowserToolInstallation ChromeInstallation(){
        return ChromeInstallation(ReadConfig());
    }

    BrowserToolInstallation ChromeInstallation(string config){
        if(!ManagedToml.ContainsMcpServer(config,ChromeDevToolsStage))return BrowserToolInstallation.None;
        string command=ManagedToml.ReadTableString(config,"mcp_servers",ChromeDevToolsStage,"command");
        if(!OwnChromeCommand(command))return BrowserToolInstallation.External;
        return File.Exists(command)&&PackageFolders(ChromePackage).Any(folder=>File.Exists(Path.Combine(folder,"node_modules",ChromePackage,ChromeBin)))
            ?BrowserToolInstallation.Managed:BrowserToolInstallation.None;
    }

    bool OwnPlaywrightSkill(){
        return File.Exists(paths.PlaywrightSkill)&&NativeData.Read(paths.PlaywrightSkill).Contains(SkillMarker);
    }

    bool OwnChromeCommand(string command){
        return !String.IsNullOrWhiteSpace(command)&&Path.IsPathRooted(command)
            &&Path.GetFileName(command).Equals("node.exe",StringComparison.OrdinalIgnoreCase)&&NativeData.Inside(command,root);
    }

    internal string InstallPlaywright(Action beforeWrite){
        if(PlaywrightInstallation()!=BrowserToolInstallation.None)return "Playwright CLI 已安装，跳过。";
        var entry=InstallPackage(PlaywrightPackage,PlaywrightVersion,PlaywrightBin);
        var defaultBrowser=FindInstalledBrowser("Chrome")!=null?"chrome":FindInstalledBrowser("Edge")!=null?"msedge":"firefox";
        var launcher="const path = require('node:path');\n"+
            "process.env.PLAYWRIGHT_BROWSERS_PATH = path.join(__dirname, 'playwright-browsers');\n"+
            "process.env.PLAYWRIGHT_MCP_BROWSER ||= "+NativeData.Json.Serialize(defaultBrowser)+";\n"+
            "require("+NativeData.Json.Serialize(entry)+");\n";
        if(!File.Exists(PlaywrightLauncher)||NativeData.Read(PlaywrightLauncher)!=launcher)
            NativeData.Write(PlaywrightLauncher,launcher);
        var environment=DownloadEnvironment();
        RunNode("Playwright CLI 版本检查",new[]{PlaywrightLauncher,"--version"},environment,20000);
        var skill=File.Exists(paths.PlaywrightSkill)?NativeData.Read(paths.PlaywrightSkill):null;
        if(skill!=null&&!skill.Contains(SkillMarker))return "Playwright CLI 已准备，保留已有同名技能。";
        var expected=PlaywrightSkill();
        if(skill!=expected){
            beforeWrite();
            NativeData.Write(paths.PlaywrightSkill,expected);
            IntegrationChanged=true;
        }
        return "Playwright CLI "+PlaywrightVersion+" 已准备。可直接使用已安装的 Chrome、Edge，也可选下载专用 Firefox。";
    }

    IEnumerable<string> PlaywrightFirefoxFolders(){
        if(!Directory.Exists(BrowserCache))return Enumerable.Empty<string>();
        return Directory.EnumerateDirectories(BrowserCache,"firefox-*")
            .Where(folder=>File.Exists(Path.Combine(folder,"INSTALLATION_COMPLETE"))
                &&File.Exists(Path.Combine(folder,"firefox","firefox.exe")));
    }

    internal bool PlaywrightFirefoxDownloaded(){return PlaywrightFirefoxFolders().Any();}
    internal bool PlaywrightFirefoxReady(){
        return PlaywrightFirefoxFolders().Any(folder=>{
            var config=Path.Combine(folder,"firefox","playwright.cfg");
            return File.Exists(config)&&NativeData.Read(config).Contains(NoFirefoxShortcut);
        });
    }

    internal void PreparePlaywrightFirefox(){
        if(PlaywrightInstallation()!=BrowserToolInstallation.Managed)
            throw new InvalidOperationException("请先初始化 Playwright CLI。");
        if(!PlaywrightFirefoxDownloaded()){
            services.Log("已确认下载 Playwright 专用 Firefox。");
            RunNode("Playwright Firefox 下载",new[]{PlaywrightLauncher,"install-browser","firefox"},DownloadEnvironment(),600000);
        }
        var folders=PlaywrightFirefoxFolders().ToArray();
        if(folders.Length==0)throw new IOException("下载后未找到完整的 Playwright 专用 Firefox。");
        foreach(var folder in folders){
            var config=Path.Combine(folder,"firefox","playwright.cfg");
            var text=NativeData.Read(config);
            if(!text.Contains(NoFirefoxShortcut))NativeData.Write(config,text+"\n"+NoFirefoxShortcut+"\n");
        }
        services.Log("Playwright 专用 Firefox 已准备。");
    }

    internal string InstallChromeDevTools(Action beforeWrite){
        var original=ReadConfig();
        const string name=ChromeDevToolsStage;
        if(ChromeInstallation(original)!=BrowserToolInstallation.None)return "已存在 Chrome DevTools MCP 配置，保留现有设置。";
        var chrome=FindInstalledBrowser("Chrome");
        if(chrome==null)throw new FileNotFoundException("未找到 Chrome。请先安装 Google Chrome，再重试此项目。");
        var entry=InstallPackage(ChromePackage,ChromeDevToolsVersion,ChromeBin);
        RunNode("Chrome DevTools MCP 版本检查",new[]{entry,"--version"},DownloadEnvironment(),20000);
        var hasRegistration=ManagedToml.ContainsMcpServer(original,name);
        if(hasRegistration&&ChromeInstallation(original)==BrowserToolInstallation.Managed)return "Chrome DevTools MCP 组件已补齐，保留现有设置。";
        var arguments=new[]{entry,"--executablePath",chrome,"--isolated","--no-usage-statistics","--no-performance-crux"};
        var desired=ManagedToml.AddMcpServer(hasRegistration?ManagedToml.RemoveMcpServer(original,name):original,name,new Dictionary<string,string>{
            {"command",NativeData.Json.Serialize(Node)},
            {"args",NativeData.Json.Serialize(arguments)},
            {"startup_timeout_sec","25"},
            {"env_vars",NativeData.Json.Serialize(new[]{"SystemRoot","ProgramFiles","ProgramFiles(x86)","LOCALAPPDATA","TEMP","TMP","USERPROFILE"})}
        });
        beforeWrite();
        WriteConfig(original,desired);
        IntegrationChanged=true;
        return "已添加 Chrome DevTools MCP "+ChromeDevToolsVersion+"，新会话可使用。";
    }

    string ReadConfig(){return File.Exists(paths.Config)?NativeData.Read(paths.Config):"";}

    void WriteConfig(string original,string desired){
        if(ReadConfig()!=original)throw new IOException("操作期间 Codex 配置发生变化，已保留修改。请重试。");
        var bytes=new UTF8Encoding(false).GetBytes(desired);
        if(File.Exists(paths.Config)){
            var previous=File.ReadAllBytes(paths.Config);
            if(previous.Length>=3&&previous[0]==239&&previous[1]==187&&previous[2]==191)
                bytes=new byte[]{239,187,191}.Concat(bytes).ToArray();
        }
        NativeData.WriteBytes(paths.Config,bytes);
    }

    internal static bool IsRemoval(string operation){
        return operation==RemovePlaywrightOperation||operation==RemoveChromeDevToolsOperation;
    }

    internal void UninstallPlaywright(){
        if(!OwnPlaywrightSkill()){
            if(PlaywrightInstallation()==BrowserToolInstallation.External)
                throw new InvalidOperationException("Playwright CLI 由其他方式安装，请使用原安装方式卸载。");
            services.Log("未找到 Init Kit 管理的 Playwright CLI。");
            return;
        }
        var assets=PackageFolders(PlaywrightPackage).Concat(new[]{PlaywrightLauncher,BrowserCache});
        RemoveAssets(assets,()=>{
            BackupRemoval(paths.PlaywrightSkill);
            File.Delete(paths.PlaywrightSkill);
        });
        services.Log("已卸载 Playwright CLI 和 Codex 技能，并清理专用浏览器文件。");
    }

    internal void UninstallChromeDevTools(){
        var original=ReadConfig();
        var desired=ManagedToml.RemoveMcpServer(original,ChromeDevToolsStage);
        if(desired==original){
            services.Log("未找到 Chrome DevTools MCP 配置。");
            return;
        }
        var command=ManagedToml.ReadTableString(original,"mcp_servers",ChromeDevToolsStage,"command");
        var assets=OwnChromeCommand(command)?PackageFolders(ChromePackage):Enumerable.Empty<string>();
        RemoveAssets(assets,()=>{
            BackupRemoval(paths.Config);
            WriteConfig(original,desired);
        });
        services.Log("Chrome DevTools MCP 已从 Codex 移除。请重新打开 Codex 后使用更新后的工具列表。");
    }

    void BackupRemoval(string path){
        NativeData.NoLinks(path);
        var directory=Path.Combine(paths.BackupRoot,"BrowserTools",DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));
        Directory.CreateDirectory(directory);
        var target=Path.Combine(directory,Path.GetFileName(path));
        File.Copy(path,target);
        services.Log("卸载前的文件备份："+target);
    }

    void RemoveAssets(IEnumerable<string> assets,Action removeConfiguration){
        var selected=assets.Where(path=>File.Exists(path)||Directory.Exists(path)).ToArray();
        if(selected.Length==0){removeConfiguration();return;}
        NativeData.NoLinks(root);
        var staging=Path.Combine(root,".removed-"+Guid.NewGuid().ToString("N"));
        var moved=new List<KeyValuePair<string,string>>();
        Directory.CreateDirectory(staging);
        try{
            foreach(var source in selected){
                if(!NativeData.Inside(source,root))throw new IOException("浏览器工具文件超出卸载目录。");
                NativeData.NoLinks(source);
                var target=Path.Combine(staging,Path.GetFileName(source));
                MoveAsset(source,target);
                moved.Add(new KeyValuePair<string,string>(source,target));
            }
            removeConfiguration();
        }catch{
            foreach(var item in moved.AsEnumerable().Reverse())MoveAsset(item.Value,item.Key);
            NativeDirectory.Delete(staging,false);
            throw;
        }
        try{NativeDirectory.Delete(staging,true);}
        catch(IOException ex){services.Log("卸载已完成，部分临时文件稍后可清理："+staging+"。"+ex.Message);}
        catch(UnauthorizedAccessException ex){services.Log("卸载已完成，部分临时文件稍后可清理："+staging+"。"+ex.Message);}
    }

    static void MoveAsset(string source,string target){
        if(Directory.Exists(source))NativeDirectory.Move(source,target);
        else NativeFile.Move(source,target);
    }

    static string PackageName(string package){return package.Substring(package.LastIndexOf('/')+1);}
    string PackageEntry(string package,string version,string relativeEntry){
        return Path.Combine(root,PackageName(package)+"-"+version,"node_modules",package.Replace('/',Path.DirectorySeparatorChar),relativeEntry);
    }
    IEnumerable<string> PackageFolders(string package){
        if(!Directory.Exists(root))return Enumerable.Empty<string>();
        string name=PackageName(package);
        var pattern=new Regex("^"+Regex.Escape(name)+@"-\d+\.\d+\.\d+$");
        return Directory.EnumerateDirectories(root,name+"-*").Where(folder=>pattern.IsMatch(Path.GetFileName(folder)));
    }

    string InstallPackage(string package,string version,string relativeEntry){
        var destination=Path.Combine(root,PackageName(package)+"-"+version);
        var entry=PackageEntry(package,version,relativeEntry);
        var relative=entry.Substring(destination.Length+1);
        EnsureNode();
        if(File.Exists(entry))return entry;
        services.Log("从 npm 官方源准备 "+package+" "+version+"。");
        WithStaging(staging=>{
            RunNode("安装 "+package,new[]{
                Npm,"install",package+"@"+version,"--prefix",staging,"--no-save","--ignore-scripts",
                "--no-audit","--no-fund","--registry=https://registry.npmjs.org","--cache",Path.Combine(root,"npm-cache")
            },DownloadEnvironment(),300000);
            if(!File.Exists(Path.Combine(staging,relative)))throw new IOException("安装后未找到 "+package+" 的命令入口。");
            Directory.Move(staging,destination);
        });
        return entry;
    }

    void EnsureNode(){
        if(File.Exists(Node)&&File.Exists(Npm))return;
        services.Log("准备浏览器工具专用的 Node.js "+NodeVersion+"。");
        WithStaging(staging=>{
            var zip=Path.Combine(staging,NodeArchive+".zip");
            services.Downloads.File("https://nodejs.org/dist/v"+NodeVersion+"/"+NodeArchive+".zip",zip,file=>{
                using(var archive=ZipFile.OpenRead(file)){
                    if(archive.GetEntry(NodeArchive+"/node.exe")==null||
                       archive.GetEntry(NodeArchive+"/node_modules/npm/bin/npm-cli.js")==null)
                        throw new InvalidDataException("Node.js 下载包缺少运行文件。");
                }
            });
            ZipFile.ExtractToDirectory(zip,staging);
            Directory.Move(Path.Combine(staging,NodeArchive),NodeDirectory);
        });
    }

    void WithStaging(Action<string> action){
        var staging=Path.Combine(root,".install-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        try{action(staging);}
        finally{if(NativeData.Inside(staging,root)&&Directory.Exists(staging))Directory.Delete(staging,true);}
    }

    Dictionary<string,string> DownloadEnvironment(){
        var environment=new Dictionary<string,string>{
            {"HTTP_PROXY",proxy??""},{"HTTPS_PROXY",proxy??""},{"ALL_PROXY",""},{"NO_PROXY",""},
            {"npm_config_proxy",proxy??""},{"npm_config_https_proxy",proxy??""},
            {"npm_config_userconfig",Path.Combine(root,"npmrc")},
            {"PLAYWRIGHT_BROWSERS_PATH",BrowserCache}
        };
        return environment;
    }

    void RunNode(string step,string[] arguments,Dictionary<string,string> environment,int timeout){
        var result=services.Commands.Run(Node,arguments,environment,timeout);
        if(result.TimedOut)throw new TimeoutException(step+"超时。请检查当前代理并重试。");
        if(result.ExitCode!=0)throw new IOException(step+"未完成（"+result.ExitCode+"）。"+(result.Error??"").Trim());
    }

    string PlaywrightSkill(){
        return "---\nname: init-kit-playwright-cli\n"+
            "description: Use Playwright CLI to open and operate web pages, click controls, fill forms, and read results in Chrome, Edge, or Firefox.\n"+
            "---\n\n"+SkillMarker+"\n\n"+
            "Run Playwright CLI from PowerShell by using this command followed by the Playwright CLI arguments:\n\n"+
            "    & '"+Node.Replace("'","''")+"' '"+PlaywrightLauncher.Replace("'","''")+"'\n\n"+
            "Run with --help to discover commands. Use an isolated browser session by default. "+
            "Use open --browser=chrome or open --browser=msedge for the installed browser channels. "+
            "To use Firefox, first confirm the optional download under Browser Setup in Codex Init Kit, then run open --browser=firefox. "+
            "Use attach --extension=chrome or attach --extension=msedge when the user wants to operate existing tabs and has installed the Playwright extension. "+
            "Read the page snapshot before selecting elements, and close only sessions opened for the current task.\n";
    }

    internal static string FindInstalledBrowser(string browser){
        var executable=browser=="Edge"?"msedge.exe":"chrome.exe";
        var registration=@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\"+executable;
        var registered=new[]{Registry.CurrentUser,Registry.LocalMachine}.Select(registry=>{
            using(var key=registry.OpenSubKey(registration))return key==null?null:key.GetValue("") as string;
        });
        var vendorPath=browser=="Edge"?@"Microsoft\Edge\Application":@"Google\Chrome\Application";
        var installed=new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}
            .Where(path=>path.Length>0).Select(path=>Path.Combine(path,vendorPath,executable));
        return registered.Concat(installed).FirstOrDefault(File.Exists);
    }
}
}
