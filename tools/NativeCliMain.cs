using System.Reflection;
using System.Runtime.Versioning;
[assembly:TargetFramework(".NETFramework,Version=v4.8")]
[assembly:AssemblyTitle("Codex Init Kit Native CLI")]
[assembly:AssemblyProduct("Codex Init Kit Native CLI")]
[assembly:AssemblyVersion("2.3.1.0")]
[assembly:AssemblyFileVersion("2.3.1")]
namespace CodexKit.Native { class NativeCliMain { static int Main(string[] args){return NativeCli.Run(args);} } }
