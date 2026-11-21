using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace CodexKit {
public static class CurrentModelSettingsTests {
    static void Assert(bool value,string message){if(!value)throw new InvalidDataException(message);}
    static void Write(string path,string text){File.WriteAllText(path,text,new UTF8Encoding(false));}
    public static void Run(string root) {
        var home=Path.Combine(root,"model-settings");Directory.CreateDirectory(home);
        var config=Path.Combine(home,"config.toml");var agents=Path.Combine(home,"AGENTS.md");
        var empty=CurrentModelSettings.Load(home);
        Assert(empty.AutoAssign&&!empty.Subagents&&empty.ParentModel=="gpt-6.2-sol"&&empty.ParentEffort=="medium"&&empty.ChildModel=="gpt-6.1-sol"&&empty.ChildEffort=="medium"&&empty.MaxConcurrentSubagents==6,"Missing model settings did not use defaults");
        Write(config,"[features]\nmulti_agent = true\n");
        Assert(CurrentModelSettings.Load(home).Subagents,"Legacy enabled setting was not inherited");
        Write(config,"features.multi_agent = true\nagents.enabled = false\n");
        Assert(!CurrentModelSettings.Load(home).Subagents,"Modern disabled setting did not take precedence");
        Write(config,"[agents]\nenabled = false\nmax_concurrent_threads_per_session = 3\n[features.multi_agent_v2]\nenabled = true\nmax_concurrent_threads_per_session = 8\n");
        var forcedV2=CurrentModelSettings.Load(home);
        Assert(forcedV2.Subagents&&forcedV2.MaxConcurrentSubagents==7,"V2's total thread limit must include the main thread and take precedence over the general limit");
        Write(config,"features.multi_agent_v2.enabled = false\nfeatures.multi_agent_v2.max_concurrent_threads_per_session = 1\nagents.max_concurrent_threads_per_session = 3\n");
        var mainOnly=CurrentModelSettings.Load(home);
        Assert(!mainOnly.Subagents&&mainOnly.MaxConcurrentSubagents==0,"An inherited V2 total of one leaves room only for the main thread");
        Write(config,"model = 'gpt-6-astra' # parent\nmodel_reasoning_effort = \"max\"\n[agents]\ndefault_subagent_model = \"gpt-6-luna\"\ndefault_subagent_reasoning_effort = 'high'\n[profiles.other]\nmodel = \"wrong\"\n");
        Write(agents,"unrelated model = no\nBEGIN CODEX LUNA PROMPT V1.3\nmodel = gpt-5.5\nmodel_reasoning_effort = low\nEND CODEX LUNA PROMPT V1.3\n");
        var current=CurrentModelSettings.Load(home);
        Assert(current.ParentModel=="gpt-6-astra"&&current.ParentEffort=="max"&&!current.AutoAssign&&current.ChildModel=="gpt-6-luna"&&current.ChildEffort=="high","Global parent or configured child settings were not inherited");
        Write(config,"model_reasoning_effort = \"high\"\n[profiles.other]\nmodel = \"not-global\"\n");
        current=CurrentModelSettings.Load(home);
        Assert(current.ParentModel=="gpt-6.2-sol"&&current.ParentEffort=="high"&&!current.AutoAssign&&current.ChildModel=="gpt-5.5"&&current.ChildEffort=="low","Partial defaults or managed child settings were not inherited");
        Write(config,"agents.default_subagent_model = \"future/model\"\nagents.default_subagent_reasoning_effort = \"xhigh\"\n");
        File.Delete(agents);current=CurrentModelSettings.Load(home);
        Assert(current.ChildModel=="future/model"&&current.ChildEffort=="xhigh","Dotted and table child settings were not inherited");
        var catalog=ModelCatalog.Preset();catalog.IncludeCurrent(current.ChildModel,current.ChildEffort);
        Assert(catalog.Check(current.ChildModel,current.ChildEffort).Contains("尚未验证"),"An inherited unknown model was lost or falsely verified");
        catalog.IncludeCurrent("gpt-6-luna","ultra");Assert(catalog.Check("gpt-6-luna","ultra").Contains("尚未验证"),"An inherited unknown effort was lost or falsely verified");
        catalog.IncludeCurrent("gpt-5.5","max");Assert(!catalog.Models.ContainsKey("gpt-5.5"),"A retired preset returned through saved settings");
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
                var limitField=type.GetField("maxConcurrentSubagentsInput",flags);
                var limit=(TextBox)limitField.GetValue(window);
                var optionsMethod=type.GetMethod("Options",flags);
                var summaryMethod=type.GetMethod("InitSummary",flags);
                Assert(Convert.ToString(combo("parentModel").SelectedItem)=="gpt-6-astra"&&Convert.ToString(combo("parentEffort").SelectedItem)=="max"&&Convert.ToString(combo("childModel").SelectedItem)=="gpt-6-luna"&&Convert.ToString(combo("childEffort").SelectedItem)=="high","Window did not load current settings at startup");
                Assert(!combo("childModel").IsEnabled&&!limit.IsEnabled&&limit.Text=="6"&&File.ReadAllText(config)==before&&!File.Exists(agents),"Opening the window changed the default limit, modified configuration, or enabled subagents");

                var expectedEfforts=new[]{"low","medium","high","xhigh","max","ultra"};
                var parentEffort=combo("parentEffort");
                combo("parentModel").SelectedItem="gpt-6.2-sol";
                Assert(parentEffort.Items.Cast<string>().SequenceEqual(expectedEfforts),"GPT-6.2 Sol did not expose its six reasoning efforts");
                parentEffort.SelectedItem="ultra";

                var subagents=(CheckBox)type.GetField("subagents",flags).GetValue(window);
                subagents.IsChecked=true;
                var childEffort=combo("childEffort");
                combo("childModel").SelectedItem="gpt-6.2-astra";
                Assert(childEffort.IsEnabled&&childEffort.Items.Cast<string>().SequenceEqual(expectedEfforts),"GPT-6.2 Astra did not expose its six reasoning efforts");
                childEffort.SelectedItem="xhigh";
                Assert(limit.IsEnabled,"Manual assignment does not allow editing the concurrent subagent limit");
                limit.Text="4";

                var selected=(Dictionary<string,object>)optionsMethod.Invoke(window,null);
                var selectedPort=(int)type.GetMethod("Port",flags).Invoke(window,null);
                var paths=new Native.NativePaths{Workspace=root,Home=home};
                var manualOptions=NativeDesktopWorkflow.Options(selected,selectedPort);
                var generated=Native.NativeInitializer.Config(paths,manualOptions);
                Assert(ManagedToml.SavedValue(generated,"model")=="gpt-6.2-sol"&&ManagedToml.SavedValue(generated,"model_reasoning_effort")=="ultra","Parent selection did not reach the generated configuration");
                Assert(ManagedToml.SavedValue(generated,"agents","default_subagent_model")=="gpt-6.2-astra"&&ManagedToml.SavedValue(generated,"agents","default_subagent_reasoning_effort")=="xhigh","Child selection did not reach the generated configuration");
                Assert(manualOptions.MaxConcurrentSubagents==4&&generated.Contains("\nmax_concurrent_threads_per_session = 4\n")&&ManagedToml.V2Enabled(generated),"Manual assignment did not enable V2 with the selected concurrent limit");
                Write(config,generated);
                var manual=CurrentModelSettings.Load(home);
                Assert(manual.Subagents&&!manual.AutoAssign&&manual.ChildModel=="gpt-6.2-astra"&&manual.ChildEffort=="xhigh"&&manual.MaxConcurrentSubagents==4,"Saved manual selection did not round trip");
                Write(config,before);
                combo("childModel").SelectedItem="自动指派";
                Assert(!childEffort.IsEnabled&&Equals(childEffort.SelectedItem,"由父模型决定")&&limit.IsEnabled&&limit.Text=="4","Automatic assignment changed the limit or unlocked its effort");
                const string resetLabel="恢复默认模型设置";
                foreach(var tab in new[]{0,1}){
                    type.GetField("configurationTab",flags).SetValue(window,tab);
                    type.GetMethod("Navigate",flags).Invoke(window,new object[]{"初始化"});
                    Assert(!InitializationFlowTests.Descendants<Button>((DependencyObject)window.Content).Any(button=>Equals(button.Content,resetLabel)),"Model reset appeared outside model settings");
                }
                type.GetField("configurationTab",flags).SetValue(window,2);
                type.GetMethod("Navigate",flags).Invoke(window,new object[]{"初始化"});
                var review=InitializationFlowTests.Descendants<Button>((DependencyObject)window.Content).Single(button=>Equals(button.Content,"查看变更 →"));
                var limitError=(TextBlock)type.GetField("maxConcurrentSubagentsError",flags).GetValue(window);
                limit.Text="2.5";
                bool unexpectedDialog=false;
                var dismiss=window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>{
                    foreach(var dialog in window.OwnedWindows.Cast<Window>().Where(candidate=>candidate.Title=="操作未完成").ToArray()){
                        unexpectedDialog=true;
                        dialog.Close();
                    }
                }));
                try{review.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}finally{dismiss.Abort();}
                Assert(!unexpectedDialog&&(int)type.GetField("wizard",flags).GetValue(window)==0&&limitError.Visibility==Visibility.Visible,"Invalid concurrent limit advanced beyond editable settings");
                foreach(var invalid in new[]{"","2.5","0","-1",long.MaxValue.ToString()}){
                    limit.Text=invalid;
                    bool rejected=false;
                    try{summaryMethod.Invoke(window,null);}catch(TargetInvocationException ex){if(!(ex.InnerException is InvalidOperationException))throw;rejected=true;}
                    Assert(rejected&&File.ReadAllText(config)==before,"Invalid concurrent limit reached initialization confirmation: "+invalid);
                }
                limit.Text="9";
                Assert(limitError.Visibility==Visibility.Collapsed,"Correcting the concurrent limit left the validation message visible");
                selected=(Dictionary<string,object>)optionsMethod.Invoke(window,null);
                var auto=NativeDesktopWorkflow.Options(selected,selectedPort);
                var desired=Native.NativeInitializer.Config(paths,auto);
                var merged=ManagedToml.Merge(generated,desired,generated);
                var manualRules=Native.NativeInitializer.Rules(paths,manualOptions);
                var rules=NativeDesktopReconcile.RulesText(manualRules,Native.NativeInitializer.Rules(paths,auto),paths);
                Assert(auto.ChildAutoAssign&&auto.Subagents&&!merged.Contains("default_subagent_")&&rules.Contains("## 自动指派")&&!rules.Contains("model_reasoning_effort ="),"Automatic assignment retained fixed model or effort");
                Assert(auto.MaxConcurrentSubagents==9&&merged.Contains("\nmax_concurrent_threads_per_session = 9\n")&&manualRules.Contains("最多 4 个")&&rules.Contains("最多 9 个"),"The selected concurrent limits did not reach configuration and rules");
                Assert(rules.Contains("显式传入 model 和 reasoning_effort")&&rules.Contains("fork_turns=\"none\""),"Automatic assignment must request per-task model and effort overrides");
                Assert(!rules.Contains("gpt-6-sol")&&!rules.Contains("GPT-6-astra"),"Automatic assignment names example models");
                Assert(File.ReadAllText(config)==before,"Changing the model controls saved settings before confirmation");
                Write(config,merged);Write(agents,rules);
                var reopened=new MainWindow(false);
                try{
                    Assert(((CheckBox)type.GetField("subagents",flags).GetValue(reopened)).IsChecked==true&&Equals(((ComboBox)type.GetField("childModel",flags).GetValue(reopened)).SelectedItem,"自动指派")&&!((ComboBox)type.GetField("childEffort",flags).GetValue(reopened)).IsEnabled,"Automatic assignment did not survive reopening");
                    var reopenedLimit=(TextBox)limitField.GetValue(reopened);
                    Assert(reopenedLimit.IsEnabled&&reopenedLimit.Text=="9","Reopening did not inherit the saved concurrent limit");
                    ((CheckBox)type.GetField("subagents",flags).GetValue(reopened)).IsChecked=false;
                    Assert(!reopenedLimit.IsEnabled&&reopenedLimit.Text=="9","Disabling subagents cleared the saved preference or left it editable");
                    var offOptions=NativeDesktopWorkflow.Options((Dictionary<string,object>)optionsMethod.Invoke(reopened,null),selectedPort);
                    Write(config,ManagedToml.Merge(merged,Native.NativeInitializer.Config(paths,offOptions),desired));
                    Write(agents,Native.NativeInitializer.Rules(paths,offOptions));
                }finally{reopened.Close();}
                var off=CurrentModelSettings.Load(home);Assert(!off.Subagents&&off.AutoAssign&&off.MaxConcurrentSubagents==9,"Disabled automatic mode lost the concurrent limit");

                var reset=InitializationFlowTests.Descendants<Button>((DependencyObject)window.Content)
                    .Single(button=>Equals(button.Content,resetLabel));
                var modelNote=(TextBlock)type.GetField("modelCheck",flags).GetValue(window);
                Assert(reset.Parent==modelNote.Parent&&Grid.GetColumn(reset)>Grid.GetColumn(modelNote)&&reset.HorizontalAlignment==HorizontalAlignment.Right,"Model reset is not beside the note at the right edge of model settings");
                var savedConfig=File.ReadAllText(config);
                var savedRules=File.ReadAllText(agents);
                combo("parentModel").SelectedItem="gpt-6.2-astra";
                parentEffort.SelectedItem="max";
                combo("childModel").SelectedItem="gpt-6.2-sol";
                childEffort.SelectedItem="ultra";
                ((CheckBox)type.GetField("antiOverDefense",flags).GetValue(window)).IsChecked=false;
                var beforeReset=(Dictionary<string,object>)optionsMethod.Invoke(window,null);
                limit.Text="2.5";
                reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var defaults=(Dictionary<string,object>)optionsMethod.Invoke(window,null);
                Assert(Equals(defaults["parentModel"],"gpt-6.2-sol")&&Equals(defaults["parentEffort"],"medium")
                    &&Equals(defaults["childAutoAssign"],true)&&Equals(childEffort.SelectedItem,"由父模型决定")&&!childEffort.IsEnabled,
                    "Reset did not select the default parent model and effort and automatic child assignment");
                Assert(limit.IsEnabled&&limit.Text=="6"&&Equals(defaults["maxConcurrentSubagents"],6L)&&limitError.Visibility==Visibility.Collapsed,
                    "Reset did not restore the default concurrent limit and clear its invalid draft");
                var resetKeys=new[]{"parentModel","parentEffort","childAutoAssign","childModel","childEffort","maxConcurrentSubagents"};
                Assert(beforeReset.Where(entry=>!resetKeys.Contains(entry.Key)).All(entry=>Equals(entry.Value,defaults[entry.Key])),
                    "Reset changed options outside model settings");

                parentEffort.SelectedItem="high";
                combo("childModel").SelectedItem="gpt-6.2-astra";
                childEffort.SelectedItem="xhigh";
                limit.Text="11";
                subagents.IsChecked=false;
                reset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                defaults=(Dictionary<string,object>)optionsMethod.Invoke(window,null);
                Assert(Equals(defaults["subagents"],false)&&!combo("childModel").IsEnabled&&!childEffort.IsEnabled&&!limit.IsEnabled
                    &&Equals(defaults["childAutoAssign"],true)&&Equals(combo("childModel").SelectedItem,"自动指派")
                    &&Equals(childEffort.SelectedItem,"由父模型决定")&&Equals(defaults["parentEffort"],"medium")
                    &&limit.Text=="6"&&Equals(defaults["maxConcurrentSubagents"],6L),
                    "Reset did not restore the default settings while keeping subagents disabled");
                var disabledDefaults=NativeDesktopWorkflow.Options(defaults,selectedPort);
                var disabledConfig=ManagedToml.Merge(generated,Native.NativeInitializer.Config(paths,disabledDefaults),generated);
                var disabledRules=NativeDesktopReconcile.RulesText(manualRules,Native.NativeInitializer.Rules(paths,disabledDefaults),paths);
                var disabledHome=Path.Combine(root,"disabled-model-defaults");
                Directory.CreateDirectory(disabledHome);
                Write(Path.Combine(disabledHome,"config.toml"),disabledConfig);
                Write(Path.Combine(disabledHome,"AGENTS.md"),disabledRules);
                var savedDefaults=CurrentModelSettings.Load(disabledHome);
                Assert(!savedDefaults.Subagents&&!ManagedToml.V2Enabled(disabledConfig)&&savedDefaults.AutoAssign&&savedDefaults.MaxConcurrentSubagents==6
                    &&!disabledConfig.Contains("default_subagent_")&&!disabledRules.Contains("BEGIN CODEX LUNA PROMPT"),
                    "Saving disabled reset defaults would enable subagents, keep manual assignment, or install LUNA rules");
                Assert((int)type.GetField("wizard",flags).GetValue(window)==0&&File.ReadAllText(config)==savedConfig&&File.ReadAllText(agents)==savedRules,
                    "Reset navigated away from editable settings or saved configuration before confirmation");
            } finally {window.Close();}
            Write(config,"model = 'gpt-5.5'\n[agents]\ndefault_subagent_model = 'gpt-6-luna'\n");
            var cache=Path.Combine(home,"models_cache.json");
            Write(cache,"{\"models\":[{\"slug\":\"gpt-6.2-astra\",\"visibility\":\"list\",\"default_reasoning_level\":\"low\",\"supported_reasoning_levels\":[{\"effort\":\"low\"}]},{\"slug\":\"gpt-6-luna\",\"visibility\":\"hide\",\"supported_reasoning_levels\":[{\"effort\":\"high\"}]}]}");
            try{
                var filtered=new MainWindow(false);
                try{
                    var flags=BindingFlags.Instance|BindingFlags.NonPublic;
                    Func<string,ComboBox> box=name=>(ComboBox)filtered.GetType().GetField(name,flags).GetValue(filtered);
                    Assert(box("parentModel").Items.Cast<string>().SequenceEqual(new[]{"gpt-6.2-astra"})&&box("childModel").Items.Cast<string>().SequenceEqual(new[]{"自动指派","gpt-6.2-astra"}),"Unavailable models returned through the preset or saved configuration");
                    filtered.GetType().GetMethod("ResetModelSettings",flags).Invoke(filtered,null);
                    Assert(Equals(box("parentModel").SelectedItem,"gpt-6.2-astra")&&Equals(box("parentEffort").SelectedItem,"low")&&Equals(box("childModel").SelectedItem,"自动指派"),"Reset selected an unavailable model or effort");
                    Assert(File.ReadAllText(config).Contains("gpt-5.5"),"Filtering changed saved settings before initialization");
                }finally{filtered.Close();}
            }finally{File.Delete(cache);}
        } finally {Environment.SetEnvironmentVariable("CODEX_HOME",previous,EnvironmentVariableTarget.Process);}
    }
}
}
