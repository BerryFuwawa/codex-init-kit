using System;using System.Reflection;using System.Runtime.Versioning;
[assembly:TargetFramework(".NETFramework,Version=v4.8")]
[assembly:AssemblyTitle("Codex Init Kit Native Updater")]
[assembly:AssemblyProduct("Codex Init Kit Native Updater")]
[assembly:AssemblyVersion("2.3.1.1")]
[assembly:AssemblyFileVersion("2.3.1.1")]
namespace CodexKit.Native {class NativeUpdaterMain {static int Main(string[] args){if(args.Length==0||args[0]!="--apply-update"){Console.Error.WriteLine("此组件仅供原生 CLI 执行已确认的更新。");return 64;}return NativeCli.Run(args);}}}
