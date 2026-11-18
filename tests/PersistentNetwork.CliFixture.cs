using System;using System.IO;using System.Text;using System.Reflection;using System.Diagnostics;using System.Collections.Generic;using System.Web.Script.Serialization;
internal static class PersistentNetworkCliFixture {
    static readonly BindingFlags Static=BindingFlags.Static|BindingFlags.NonPublic;
    static int Main(string[] args){
        var assembly=Assembly.LoadFrom(args[0]);var json=new JavaScriptSerializer();var home=Environment.GetEnvironmentVariable("CODEX_HOME");
        if(args[1]=="prepare"){
            var type=assembly.GetType("CodexKit.Native.NativeOptions");var options=Activator.CreateInstance(type);type.GetField("Proxy").SetValue(options,true);type.GetField("ProxyHost").SetValue(options,"127.0.0.1");type.GetField("ProxyPort").SetValue(options,Int32.Parse(args[2]));
            var text=(string)assembly.GetType("CodexKit.NativeDesktopReconcile").GetMethod("EnvironmentText",Static).Invoke(null,new object[]{"CUSTOM=preserve\n",options});File.WriteAllText(Path.Combine(home,".env"),text,new UTF8Encoding(false));return 0;
        }
        var snapshotType=assembly.GetType("CodexKit.SavedNetworkConfiguration");var snapshot=snapshotType.GetMethod("Read",Static).Invoke(null,new object[]{home,new Func<string,string>(Environment.GetEnvironmentVariable)});var summary=(string)snapshotType.GetField("Summary",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(snapshot);
        var typeProbe=assembly.GetType("CodexKit.NetworkDiagnostics");var info=(ProcessStartInfo)typeProbe.GetMethod("ProbeStartInfo",Static).Invoke(null,new object[]{args[1]});
        using(var process=Process.Start(info)){
            var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();if(!process.WaitForExit(45000)){process.Kill();process.WaitForExit();throw new IOException("isolated CLI timeout");}var data=json.Deserialize<Dictionary<string,object>>(output.Result);var parsed=typeProbe.GetMethod("ParseModelReport",Static).Invoke(null,new object[]{data});File.WriteAllText(args[2],json.Serialize(new{savedConfiguration=summary,doctor=data,parsed=parsed,exitCode=process.ExitCode}),new UTF8Encoding(false));error.GetAwaiter().GetResult();return 0;
        }
    }
}
