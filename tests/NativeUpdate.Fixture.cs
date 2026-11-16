using System;using System.Threading;using System.Reflection;using System.IO;using System.Text;
[assembly:AssemblyProduct("Codex Init Kit Native CLI")]
[assembly:AssemblyVersion("2.3.1.2")]
[assembly:AssemblyFileVersion("2.3.1.2")]
class UpdateFixture {static int Main(string[] args){var echo=Environment.GetEnvironmentVariable("CODEX_NATIVE_TEST_ARGS");if(!String.IsNullOrEmpty(echo)){File.WriteAllLines(echo,args,new UTF8Encoding(false));return 0;}if(args.Length>0&&args[0]=="sleep-short"){Thread.Sleep(500);return 0;}if(args.Length>0&&args[0]=="sleep"){Thread.Sleep(30000);return 0;}Console.WriteLine("Codex Init Kit Native CLI 2.3.1.2 测试组件");return 0;}}
