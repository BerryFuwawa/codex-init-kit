using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace CodexKit {
public static class ProxyPortSettingsTests {
    const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    static void Assert(bool value,string message){if(!value)throw new InvalidDataException(message);}
    static void Write(string path,string text){File.WriteAllText(path,text,new UTF8Encoding(false));}
    static T Field<T>(MainWindow window,string name){return (T)typeof(MainWindow).GetField(name,Private).GetValue(window);}
    static object Call(MainWindow window,string name,params object[] args){return typeof(MainWindow).GetMethod(name,Private).Invoke(window,args);}

    public static void Run(string root) {
        var home=Path.Combine(root,"proxy-port-settings");Directory.CreateDirectory(home);
        var path=Path.Combine(home,".env");
        Assert(ProxyPortSettings.Load(home)==10808,"Missing proxy config did not use v2rayN defaults.");
        foreach(var preset in ProxyPortSettings.Presets.Where(item=>item.Port>0)){
            Write(path,"HTTP_PROXY=http://127.0.0.1:"+preset.Port+"\n");
            Assert(ProxyPortSettings.Load(home)==preset.Port,"A preset port could not be inherited.");
        }
        foreach(var entry in new[]{
            new[]{"OTHER=value\n","10808"},
            new[]{"HTTP_PROXY=\nHTTPS_PROXY='http://127.0.0.1:3067' # rule\n","3067"},
            new[]{"HTTP_PROXY=\"\"\nHTTPS_PROXY=\"http://[::1]:7897\"\n","7897"},
            new[]{"http_proxy = http://127.0.0.1:3066 # global\n","3066"},
            new[]{"export HTTP_PROXY='http://127.0.0.1:54321/'\n","54321"},
            new[]{"HTTP_PROXY=http://127.0.0.1:1\nHTTPS_PROXY=invalid\n","1"},
            new[]{"HTTPS_PROXY=http://127.0.0.1:65535\n","65535"},
            new[]{"HTTP_PROXY=http://127.0.0.1:10808\nHTTP_PROXY=http://127.0.0.1:2080\n","2080"}
        }){
            Write(path,entry[0]);var before=File.ReadAllBytes(path);
            Assert(ProxyPortSettings.Load(home)==Int32.Parse(entry[1]),"Quoted, empty or alternate proxy config was not parsed correctly.");
            Assert(before.SequenceEqual(File.ReadAllBytes(path)),"Reading proxy config changed it.");
        }
        foreach(var value in new[]{"invalid","http://127.0.0.1","http://127.0.0.1:0","http://127.0.0.1:65536","http://127.0.0.1:7897/path","'unterminated"}){
            Write(path,"HTTP_PROXY="+value+"\nHTTPS_PROXY=http://127.0.0.1:3067\n");
            bool rejected=false;try{ProxyPortSettings.Load(home);}catch(InvalidDataException){rejected=true;}
            Assert(rejected,"Invalid saved proxy silently became a different port.");
        }
        File.Delete(path);Directory.CreateDirectory(path);
        bool unreadable=false;try{ProxyPortSettings.Load(home);}catch(IOException){unreadable=true;}catch(UnauthorizedAccessException){unreadable=true;}
        Assert(unreadable,"Unreadable saved proxy config silently used defaults.");
        Directory.Delete(path);
    }

    public static void VerifyWindow(string root) {
        var home=Path.Combine(root,"proxy-port-settings");var path=Path.Combine(home,".env");
        var previous=Environment.GetEnvironmentVariable("CODEX_HOME",EnvironmentVariableTarget.Process);
        try {
            Environment.SetEnvironmentVariable("CODEX_HOME",home,EnvironmentVariableTarget.Process);
            foreach(var preset in ProxyPortSettings.Presets.Where(item=>item.Port>0)){
                Write(path,"HTTP_PROXY=http://127.0.0.1:"+preset.Port+"\n");
                var window=new MainWindow(false);
                try{
                    Assert(!Field<ComboBox>(window,"proxyPortPreset").IsDropDownOpen,"First launch unexpectedly opened the preset dropdown.");
                    Assert(Field<ComboBox>(window,"proxyPortPreset").SelectedItem==preset&&Field<TextBox>(window,"port").IsReadOnly,"Startup did not reverse-map a saved preset.");
                    Assert((int)Call(window,"Port")==preset.Port,"Startup update requests would use a stale port.");
                }finally{window.Close();}
            }
            Write(path,"HTTP_PROXY=http://127.0.0.1:54321\n");
            var inherited=new MainWindow(false);
            try{Assert(Field<ComboBox>(inherited,"proxyPortPreset").SelectedIndex==8&&Field<TextBox>(inherited,"port").Text=="54321"&&!Field<TextBox>(inherited,"port").IsReadOnly,"Unknown saved port did not become an editable custom value.");}
            finally{inherited.Close();}
            Write(path,"HTTP_PROXY=invalid\n");
            var invalid=new MainWindow(false);
            try{Assert(Field<ComboBox>(invalid,"proxyPortPreset").SelectedIndex==8&&Field<TextBox>(invalid,"port").Text==""&&Field<TextBlock>(invalid,"proxyPortNotice").Visibility==Visibility.Visible,"Invalid saved proxy did not show an empty custom field and warning.");}
            finally{invalid.Close();}
        }finally{Environment.SetEnvironmentVariable("CODEX_HOME",previous,EnvironmentVariableTarget.Process);}

        var preview=new MainWindow(true){Left=-20000,Top=-20000,Opacity=0,ShowInTaskbar=false};
        preview.Show();
        try{
            var combo=Field<ComboBox>(preview,"proxyPortPreset");var port=Field<TextBox>(preview,"port");var enabled=Field<CheckBox>(preview,"proxyEnabled");
            Assert(combo.SelectedIndex==0&&port.Text=="10808"&&port.IsReadOnly,"Fresh window did not default to v2rayN.");
            combo.SelectedIndex=8;Assert(port.Text==""&&!port.IsReadOnly,"First custom selection was not empty and editable.");
            port.Text="12345";combo.SelectedIndex=2;
            Assert(port.Text=="3067"&&port.IsReadOnly,"Karing selection did not fill and lock the port.");
            Call(preview,"Navigate","连接与修复");
            Assert(combo.SelectedIndex==2&&port.IsReadOnly&&port.Text=="3067","Browser navigation lost the proxy choice.");
            combo.SelectedIndex=8;Assert(port.Text=="12345"&&!port.IsReadOnly,"Returning to custom lost the previous value.");
            enabled.IsChecked=false;Assert(!combo.IsEnabled&&!port.IsEnabled&&!Field<TextBox>(preview,"proxyHost").IsEnabled&&(int)Call(preview,"Port")==0,"Disabled proxy did not disable and ignore all inputs.");
            enabled.IsChecked=true;Assert(combo.IsEnabled&&port.IsEnabled&&!port.IsReadOnly,"Enabling proxy lost the custom input state.");
            foreach(var value in new[]{"","0","65536","abc"}){
                port.Text=value;bool rejected=false;try{Call(preview,"Port");}catch(TargetInvocationException ex){rejected=ex.InnerException is InvalidOperationException;}
                Assert(rejected,"Invalid custom proxy port was accepted.");
            }
            combo.SelectedIndex=5;Call(preview,"Navigate","初始化");
            Assert(port.Text=="7890"&&port.IsReadOnly&&Convert.ToString(Call(preview,"InitSummary")).Contains("127.0.0.1:7890"),"Confirmation lost the selected proxy port.");
            foreach(var page in new[]{"初始化","连接与修复"}){
                Call(preview,"Navigate",page);
                foreach(var width in new[]{800.0,900.0}){
                    preview.Width=width;
                    var view=(FrameworkElement)preview.Content;
                    view.Measure(new Size(width,1200));view.Arrange(new Rect(0,0,width,1200));view.UpdateLayout();
                    var row=(Grid)combo.Parent;var hint=row.Children.OfType<Border>().SingleOrDefault(x=>x.Name=="ProxyPresetHint");Assert(row.Children.OfType<TextBlock>().Any(x=>x.Text=="阁下的代理端口：")&&hint!=null&&InitializationFlowTests.Descendants<TextBlock>(hint).Any(x=>x.Text=="常用端口预设")&&Convert.ToString(hint.ToolTip).Contains("自定义代理端口")&&Grid.GetColumn(port)==1&&Grid.GetColumn(combo)==2,"Proxy row labels or control order changed.");var content=(FrameworkElement)Field<StackPanel>(preview,"content");
                    Assert(row.ActualWidth<=content.ActualWidth-32&&Math.Abs(combo.ActualHeight-port.ActualHeight)<1&&Math.Abs(port.ActualWidth-90)<1,"Proxy row layout at "+width+": row "+row.ActualWidth+", content "+content.ActualWidth+", heights "+combo.ActualHeight+"/"+port.ActualHeight+", port "+port.ActualWidth);
                }
            }
        }finally{preview.Close();}
    }
}
}
