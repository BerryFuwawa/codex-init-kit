using System;
using System.IO;
using System.Text;

namespace CodexKit {
public static class PermissionSettingsTests {
    static void Assert(bool value,string message){if(!value)throw new InvalidDataException(message);}
    public static void Run(string root) {
        var home=Path.Combine(root,"permissions");Directory.CreateDirectory(home);
        var path=Path.Combine(home,"config.toml");
        var empty=PermissionSettings.Load(home);
        Assert(empty.Apply("model = 'new'\n[features]\nmemories = true\n")=="model = 'new'\n[features]\nmemories = true\n","Missing permissions changed generated config");
        var original="model = 'old'\napproval_policy = { granular = { sandbox_approval = true, rules = false } } # keep\napprovals_reviewer = 'auto_review'\nsandbox_mode = \"workspace-write\"\n[sandbox_workspace_write]\nnetwork_access = true\nwritable_roots = [\n  'D:\\工作', # first\n  \"C:\\\\Temp\",\n]\n[unrelated]\nnote = \"\"\"\n[permissions.fake]\ntext = 'not a table'\n\"\"\"\n";
        File.WriteAllText(path,original,new UTF8Encoding(false));
        var current=PermissionSettings.Load(home);
        var generated="model = 'new'\n[features]\nmemories = true\n";
        var merged=current.Apply(generated);
        Assert(merged.Contains("approval_policy = { granular = { sandbox_approval = true, rules = false } } # keep")&&merged.Contains("approvals_reviewer = 'auto_review'")&&merged.Contains("sandbox_mode = \"workspace-write\""),"Approval or sandbox values were lost");
        Assert(merged.Contains("writable_roots = [\n  'D:\\工作', # first\n  \"C:\\\\Temp\",\n]")&&!merged.Contains("permissions.fake")&&!merged.Contains("old"),"Multi-line permissions or unrelated config were copied incorrectly");
        Assert(current.Apply(merged)==merged&&File.ReadAllText(path)==original,"Merging duplicated permissions or changed the source");
        File.WriteAllText(path,"default_permissions = 'work'\napproval_policy = 'on-request'\n[permissions.work.filesystem]\n'D:\\工作' = 'write'\n[permissions.work.network]\nenabled = true\nallowed_domains = ['example.com']\n",new UTF8Encoding(false));
        current=PermissionSettings.Load(home);merged=current.Apply(generated);
        Assert(merged.Contains("default_permissions = 'work'")&&merged.Contains("[permissions.work.network]")&&merged.Contains("allowed_domains = ['example.com']")&&!merged.Contains("sandbox_mode"),"Named permission profile was not preserved");
        Assert(current.Summary.Contains("work")&&current.Summary.Contains("on-request"),"Confirmation did not describe inherited permissions");
        var complete=PermissionSettings.Parse("developer_instructions = \"\"\"Say \"yes\"\"\"\"\ntime = 07:32:00\n[[skills.config]]\npath = 'first'\n[skills.config.options]\nenabled = true\n[[skills.config]]\npath = 'second'\n[skills.config.options]\nenabled = false\napproval = 'unrelated'\n");
        Assert(!complete.Apply(generated).Contains("developer_instructions"),"Unrelated multiline settings leaked into inherited permissions");
        foreach(var invalid in new[]{"sandbox_mode = \"unfinished\n","[permissions.work]\nallowed = [true\n","approval_policy = { granular = { rules = true }\n","approval_policy = never\n","sandbox_mode = 'read-only'\nsandbox_mode = 'danger-full-access'\n","approval_policy = 'never'\n[permissions.work]\nnetwork = false\n[permissions.work.network]\nenabled = true\n","permissions.work = { network = false }\n[permissions.work.network]\nenabled = true\n","[permissions.work.network]\nenabled = true\n[permissions]\nwork = false\n","[permissions.work]\ninvalid = 0b9\n"}){
            File.WriteAllText(path,invalid);bool rejected=false;try{PermissionSettings.Load(home);}catch(InvalidDataException){rejected=true;}
            Assert(rejected,"Malformed permissions were silently discarded: "+invalid);
        }
        Assert(File.ReadAllText(path).Contains("0b9"),"Parsing failure changed configuration");
        File.Delete(path);Directory.CreateDirectory(path);bool unreadable=false;try{PermissionSettings.Load(home);}catch(IOException){unreadable=true;}catch(UnauthorizedAccessException){unreadable=true;}
        Assert(unreadable,"Unreadable config silently became default permissions");Directory.Delete(path);
    }
}
}
