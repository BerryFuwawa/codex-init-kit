using File=CodexKit.Native.NativeFile;
using Directory=CodexKit.Native.NativeDirectory;
using FileInfo=CodexKit.Native.NativeFileInfo;
using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Text;
namespace CodexKit.Native {
public sealed class SavedFile {public string Target,Backup;public bool Existed;}
public sealed class NativeSession {
    public int schema=1;public string root,home;public List<SavedFile> files=new List<SavedFile>();public Dictionary<string,RegistryValueState> registry=new Dictionary<string,RegistryValueState>();
    public static NativeSession Create(NativePaths p,INativeRegistry registry,bool full,bool userProxy=false){
        var s=new NativeSession{root=Path.Combine(p.BackupRoot,"Codex_Full_Reset_"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8)),home=p.Home};Directory.CreateDirectory(Path.Combine(s.root,"Backups"));
        try{
            s.Save(p.Env,".env");
            if(full){s.Save(p.Config,"config.toml");s.Save(p.Agents,"AGENTS.md");s.Save(p.RtkDoc,"RTK.md");s.Save(p.Hooks,"hooks.json");s.Save(p.Rtk,Path.Combine("rtk","rtk.exe"));s.Save(p.RtkConfig,Path.Combine("rtk","config.toml"));foreach(string name in LegacyNames)s.Save(Path.Combine(p.LegacyCli,name),Path.Combine("cli",name));foreach(string key in new[]{"Path","CODEX_CLI_PATH","CODEX_CODE_MODE_HOST_PATH"})s.registry.Add(key,registry.Get(key));}
            if(userProxy)foreach(var key in NativeUserProxy.Keys)s.registry.Add(key,registry.Get(key));
            s.Persist(p);return s;
        }catch(Exception ex){NativeData.Write(Path.Combine(s.root,"Backup_Failed.txt"),"备份未完成，尚未重建配置："+ex.Message);throw;}
    }
    public static readonly string[] LegacyNames={"codex-latest.exe","codex-code-mode-host.exe","codex-windows-sandbox-setup.exe","codex-command-runner.exe"};
    void Save(string target,string relative){NativeData.NoLinks(target);var entry=new SavedFile{Target=Path.GetFullPath(target),Backup=relative,Existed=File.Exists(target)};if(Directory.Exists(target))throw new IOException("预期文件被目录占用："+target);if(entry.Existed){var dest=Path.Combine(root,"Backups",relative);Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(target,dest,false);}files.Add(entry);}
    internal void Include(string target,string relative){
        if(files.Any(file=>String.Equals(file.Target,target,StringComparison.OrdinalIgnoreCase)))return;
        Save(target,relative);
        NativeData.Write(Path.Combine(root,"session.json"),NativeData.Json.Serialize(this));
    }
    void Persist(NativePaths p){
        NativeData.Write(Path.Combine(root,"session.json"),NativeData.Json.Serialize(this));
        var lines=new List<string>{"RESTORE_CONFIG="+(files.Any(x=>x.Target.Equals(p.Config,StringComparison.OrdinalIgnoreCase))?"1":"0"),"RESTORE_ENV=1","RESTORE_AGENTS="+(files.Any(x=>x.Target.Equals(p.Agents,StringComparison.OrdinalIgnoreCase))?"1":"0"),"RESTORE_RTK="+(files.Any(x=>x.Target.Equals(p.RtkDoc,StringComparison.OrdinalIgnoreCase))?"1":"0"),"RESTORE_RTK_EXE="+(files.Any(x=>x.Target.Equals(p.Rtk,StringComparison.OrdinalIgnoreCase))?"1":"0"),"RESTORE_CLI="+(registry.ContainsKey("CODEX_CLI_PATH")?"1":"0"),"RESTORE_USER_PATH="+(registry.ContainsKey("Path")?"1":"0"),"RESTORE_RUNTIME="+(registry.ContainsKey("CODEX_CLI_PATH")?"1":"0"),"RESTORE_RTK_CONFIG="+(files.Any(x=>x.Target.Equals(p.RtkConfig,StringComparison.OrdinalIgnoreCase))?"1":"0"),"RESTORE_HOOKS="+(files.Any(x=>x.Target.Equals(p.Hooks,StringComparison.OrdinalIgnoreCase))?"1":"0")};
        if(NativeUserProxy.Keys.Any(registry.ContainsKey))NativeData.Write(Path.Combine(root,"Backups","user-proxy.before.json"),NativeData.Json.Serialize(registry.Where(x=>NativeUserProxy.Keys.Contains(x.Key)).ToDictionary(x=>x.Key,x=>x.Value)));
        foreach(var item in new[]{new[]{"OLD_CLI","CODEX_CLI_PATH"},new[]{"OLD_HOST","CODEX_CODE_MODE_HOST_PATH"}}){RegistryValueState v;registry.TryGetValue(item[1],out v);lines.Add(item[0]+"_PRESENT="+(v!=null&&v.Exists?"1":"0"));lines.Add(item[0]+"_TYPE="+(v!=null&&v.Kind=="ExpandString"?"REG_EXPAND_SZ":"REG_SZ"));lines.Add(item[0]+"_VALUE="+(v==null?"":Convert.ToString(v.Value)));}
        RegistryValueState oldPath;if(registry.TryGetValue("Path",out oldPath))NativeData.Write(Path.Combine(root,"Backups","user-path.before.txt"),oldPath.Exists?Convert.ToString(oldPath.Value):"__CODEX_INIT_PATH_ABSENT__");
        NativeData.Write(Path.Combine(root,"session.meta"),String.Join("\r\n",lines)+"\r\n");
        if(p.Self.IndexOfAny(new[]{'%','\r','\n'})>=0)throw new ArgumentException("程序路径不能写入旧式回滚入口；请使用原生命令回滚。");
        NativeData.Write(Path.Combine(root,"Rollback.cmd"),"@echo off\r\nchcp 65001 >nul\r\n\""+p.Self+"\" "+(p.EntryPrefix??"")+"rollback --session \"%~dp0.\" --confirm\r\nexit /b %errorlevel%\r\n");
    }
    public static NativeSession Load(string root,NativePaths paths){
        var native=Path.Combine(root,"session.json");if(File.Exists(native)){var s=NativeData.Json.Deserialize<NativeSession>(NativeData.Read(native));if(s==null||s.schema!=1)throw new InvalidDataException("不支持的回滚元数据。");s.root=Path.GetFullPath(root);return s;}
        var meta=Path.Combine(root,"session.meta");if(!File.Exists(meta))throw new FileNotFoundException("找不到初始化回滚元数据。",meta);
        var values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(var line in File.ReadAllLines(meta,Encoding.Default)){var index=line.IndexOf('=');if(index>0)values[line.Substring(0,index)]=line.Substring(index+1);}
        var legacy=new NativeSession{root=Path.GetFullPath(root),home=paths.Home};Action<string,string,string> add=(flag,target,backup)=>{string val;if(values.TryGetValue(flag,out val)&&val=="1")legacy.files.Add(new SavedFile{Target=target,Backup=backup,Existed=File.Exists(Path.Combine(root,"Backups",backup))});};
        add("RESTORE_CONFIG",paths.Config,"config.toml");add("RESTORE_ENV",paths.Env,".env");add("RESTORE_AGENTS",paths.Agents,"AGENTS.md");add("RESTORE_RTK",paths.RtkDoc,"RTK.md");add("RESTORE_HOOKS",paths.Hooks,"hooks.json");add("RESTORE_RTK_EXE",paths.Rtk,Path.Combine("rtk","rtk.exe"));add("RESTORE_RTK_CONFIG",paths.RtkConfig,Path.Combine("rtk","config.toml"));foreach(var name in LegacyNames)if(File.Exists(Path.Combine(root,"Backups","cli",name)))add("RESTORE_CLI",Path.Combine(paths.LegacyCli,name),Path.Combine("cli",name));
        string enabled;if(values.TryGetValue("RESTORE_USER_PATH",out enabled)&&enabled=="1"){var pathFile=Path.Combine(root,"Backups","user-path.before.txt");if(!File.Exists(pathFile))throw new FileNotFoundException("旧回滚缺少 PATH 备份。",pathFile);var old=NativeData.Read(pathFile);legacy.registry["Path"]=new RegistryValueState{Exists=old!="__CODEX_INIT_PATH_ABSENT__",Kind="ExpandString",Value=old};}
        if(values.TryGetValue("RESTORE_RUNTIME",out enabled)&&enabled=="1")foreach(var pair in new[]{new[]{"OLD_CLI","CODEX_CLI_PATH"},new[]{"OLD_HOST","CODEX_CODE_MODE_HOST_PATH"}}){string present,kind,value;values.TryGetValue(pair[0]+"_PRESENT",out present);values.TryGetValue(pair[0]+"_TYPE",out kind);values.TryGetValue(pair[0]+"_VALUE",out value);legacy.registry[pair[1]]=new RegistryValueState{Exists=present=="1",Kind=kind=="REG_EXPAND_SZ"?"ExpandString":"String",Value=value??""};}
        var proxyBackup=Path.Combine(root,"Backups","user-proxy.before.json");if(File.Exists(proxyBackup))foreach(var entry in NativeData.Json.Deserialize<Dictionary<string,RegistryValueState>>(NativeData.Read(proxyBackup))){if(!NativeUserProxy.Keys.Contains(entry.Key))throw new InvalidDataException("代理备份包含未管理的环境变量。");legacy.registry[entry.Key]=entry.Value;}
        return legacy;
    }
    public void Restore(INativeRegistry store){
        foreach(var entry in files){if(!Path.IsPathRooted(entry.Target)||String.IsNullOrWhiteSpace(entry.Backup)||!NativeData.Inside(Path.Combine(root,"Backups",entry.Backup),Path.Combine(root,"Backups")))throw new InvalidDataException("回滚文件位置无效。");NativeData.NoLinks(entry.Target);if(entry.Existed&&!File.Exists(Path.Combine(root,"Backups",entry.Backup)))throw new FileNotFoundException("备份文件缺失，停止回滚。",entry.Backup);}
        foreach(var entry in files){if(entry.Existed)NativeData.WriteBytes(entry.Target,File.ReadAllBytes(Path.Combine(root,"Backups",entry.Backup)));else if(File.Exists(entry.Target))File.Delete(entry.Target);}
        foreach(var entry in registry)store.Set(entry.Key,entry.Value);
    }
    public static string Latest(NativePaths paths){if(!Directory.Exists(paths.BackupRoot))throw new DirectoryNotFoundException("没有初始化备份。");var result=Directory.GetDirectories(paths.BackupRoot,"Codex_Full_Reset_*").Where(x=>File.Exists(Path.Combine(x,"session.json"))||File.Exists(Path.Combine(x,"session.meta"))).OrderByDescending(x=>Directory.GetCreationTimeUtc(x)).ThenByDescending(x=>Path.GetFileName(x),StringComparer.OrdinalIgnoreCase).FirstOrDefault();if(result==null)throw new FileNotFoundException("没有可回滚的初始化备份。");return result;}
}
}
