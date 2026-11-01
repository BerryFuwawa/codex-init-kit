using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Controls;

namespace CodexKit {
public static class CurrentModelSettingsTests {
    static void Assert(bool value,string message){if(!value)throw new InvalidDataException(message);}
    static void Write(string path,string text){File.WriteAllText(path,text,new UTF8Encoding(false));}
    public static void Run(string root) {
        var home=Path.Combine(root,"model-settings");Directory.CreateDirectory(home);
        var config=Path.Combine(home,"config.toml");var agents=Path.Combine(home,"AGENTS.md");
        var empty=CurrentModelSettings.Load(home);
        Assert(empty.ParentModel=="gpt-6.1-sol"&&empty.ParentEffort=="medium"&&empty.ChildModel=="gpt-6.1-sol"&&empty.ChildEffort=="medium","Missing model settings did not use defaults");
        Write(config,"model = 'gpt-6-astra' # parent\nmodel_reasoning_effort = \"max\"\n[agents]\ndefault_subagent_model = \"gpt-6-luna\"\ndefault_subagent_reasoning_effort = 'high'\n[profiles.other]\nmodel = \"wrong\"\n");
        Write(agents,"unrelated model = no\nBEGIN CODEX LUNA PROMPT V1.3\nmodel = gpt-5.5\nmodel_reasoning_effort = low\nEND CODEX LUNA PROMPT V1.3\n");
        var current=CurrentModelSettings.Load(home);
        Assert(current.ParentModel=="gpt-6-astra"&&current.ParentEffort=="max"&&current.ChildModel=="gpt-6-luna"&&current.ChildEffort=="high","Global parent or configured child settings were not inherited");
        Write(config,"model_reasoning_effort = \"high\"\n[profiles.other]\nmodel = \"not-global\"\n");
        current=CurrentModelSettings.Load(home);
        Assert(current.ParentModel=="gpt-6.1-sol"&&current.ParentEffort=="high"&&current.ChildModel=="gpt-5.5"&&current.ChildEffort=="low","Partial defaults or managed child settings were not inherited");
        Write(config,"agents.default_subagent_model = \"future/model\"\nagents.default_subagent_reasoning_effort = \"xhigh\"\n");
        File.Delete(agents);current=CurrentModelSettings.Load(home);
        Assert(current.ChildModel=="future/model"&&current.ChildEffort=="xhigh","Dotted and table child settings were not inherited");
        var catalog=ModelCatalog.Preset();catalog.IncludeCurrent(current.ChildModel,current.ChildEffort);
        Assert(catalog.Check(current.ChildModel,current.ChildEffort).Contains("尚未验证"),"An inherited unknown model was lost or falsely verified");
        catalog.IncludeCurrent("gpt-5.5","max");Assert(catalog.Check("gpt-5.5","max").Contains("尚未验证"),"An inherited unknown effort was lost or falsely verified");
        Write(config,"model = \"bad model\"\n");bool rejected=false;try{CurrentModelSettings.Load(home);}catch(InvalidDataException){rejected=true;}
        Assert(rejected,"Invalid current model silently became a default");
    }
    public static void VerifyWindow(string root) {
        var home=Path.Combine(root,"model-settings");
        var config=Path.Combine(home,"config.toml");var agents=Path.Combine(home,"AGENTS.md");
        Write(config,"model = \"gpt-6-astra\"\nmodel_reasoning_effort = \"max\"\n[agents]\ndefault_subagent_model = \"gpt-6-luna\"\ndefault_subagent_reasoning_effort = \"high\"\n");
        var before=File.ReadAllText(config);var previous=Environment.GetEnvironmentVariable("CODEX_HOME",EnvironmentVariableTarget.Process);
        try {
            Environment.SetEnvironmentVariable("CODEX_HOME",home,EnvironmentVariableTarget.Process);
            var window=new MainWindow(false);
            try {
                var flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=window.GetType();
                Func<string,ComboBox> combo=name=>(ComboBox)type.GetField(name,flags).GetValue(window);
                Assert(Convert.ToString(combo("parentModel").SelectedItem)=="gpt-6-astra"&&Convert.ToString(combo("parentEffort").SelectedItem)=="max"&&Convert.ToString(combo("childModel").SelectedItem)=="gpt-6-luna"&&Convert.ToString(combo("childEffort").SelectedItem)=="high","Window did not load current settings at startup");
                Assert(!combo("childModel").IsEnabled&&File.ReadAllText(config)==before&&!File.Exists(agents),"Opening the window modified configuration or enabled subagents");
            } finally {window.Close();}
        } finally {Environment.SetEnvironmentVariable("CODEX_HOME",previous,EnvironmentVariableTarget.Process);}
    }
}
}
