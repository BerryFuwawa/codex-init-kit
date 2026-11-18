using System.Reflection;
using System.Runtime.Versioning;
[assembly:TargetFramework(".NETFramework,Version=v4.8")]
[assembly:AssemblyTitle("Codex Init Kit Native CLI")]
[assembly:AssemblyProduct("Codex Init Kit Native CLI")]
[assembly:AssemblyVersion(CodexKit.Native.NativeData.Version)]
[assembly:AssemblyFileVersion(CodexKit.Native.NativeData.Version)]
namespace CodexKit.Native { class NativeCliMain { static int Main(string[] args){return NativeCli.Run(args);} } }
