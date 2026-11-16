using System;using System.IO;using System.Linq;using System.Text;
namespace CodexKit.Native {
public static class NativePath {
 public static string Display(string path){if(path.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase))return @"\\"+path.Substring(8);return path.StartsWith(@"\\?\",StringComparison.Ordinal)?path.Substring(4):path;}
 public static string Io(string path){var full=Path.GetFullPath(path);if(full.StartsWith(@"\\?\",StringComparison.Ordinal))return full;return full.StartsWith(@"\\",StringComparison.Ordinal)?@"\\?\UNC\"+full.Substring(2):@"\\?\"+full;}
}
public static class NativeFile {
 public static bool Exists(string p){return System.IO.File.Exists(NativePath.Io(p));}
 public static byte[] ReadAllBytes(string p){return System.IO.File.ReadAllBytes(NativePath.Io(p));}
 public static string ReadAllText(string p){return System.IO.File.ReadAllText(NativePath.Io(p));}
 public static string ReadAllText(string p,Encoding e){return System.IO.File.ReadAllText(NativePath.Io(p),e);}
 public static string[] ReadAllLines(string p,Encoding e){return System.IO.File.ReadAllLines(NativePath.Io(p),e);}
 public static void WriteAllBytes(string p,byte[] b){System.IO.File.WriteAllBytes(NativePath.Io(p),b);}
 public static void WriteAllText(string p,string t){System.IO.File.WriteAllText(NativePath.Io(p),t);}
 public static void WriteAllText(string p,string t,Encoding e){System.IO.File.WriteAllText(NativePath.Io(p),t,e);}
 public static void AppendAllText(string p,string t){System.IO.File.AppendAllText(NativePath.Io(p),t);}
 public static void Copy(string a,string b){System.IO.File.Copy(NativePath.Io(a),NativePath.Io(b));}
 public static void Copy(string a,string b,bool overwrite){System.IO.File.Copy(NativePath.Io(a),NativePath.Io(b),overwrite);}
 public static void Move(string a,string b){System.IO.File.Move(NativePath.Io(a),NativePath.Io(b));}
 public static void Replace(string a,string b,string backup){System.IO.File.Replace(NativePath.Io(a),NativePath.Io(b),backup==null?null:NativePath.Io(backup));}
 public static void Delete(string p){System.IO.File.Delete(NativePath.Io(p));}
 public static FileAttributes GetAttributes(string p){return System.IO.File.GetAttributes(NativePath.Io(p));}
 public static FileStream OpenRead(string p){return System.IO.File.OpenRead(NativePath.Io(p));}
}
public static class NativeDirectory {
 public static bool Exists(string p){return System.IO.Directory.Exists(NativePath.Io(p));}
 public static DirectoryInfo CreateDirectory(string p){return System.IO.Directory.CreateDirectory(NativePath.Io(p));}
 public static void Delete(string p){System.IO.Directory.Delete(NativePath.Io(p));}
 public static void Delete(string p,bool recursive){System.IO.Directory.Delete(NativePath.Io(p),recursive);}
 public static void Move(string a,string b){System.IO.Directory.Move(NativePath.Io(a),NativePath.Io(b));}
 public static string[] GetFiles(string p,string mask,SearchOption option){return System.IO.Directory.GetFiles(NativePath.Io(p),mask,option).Select(NativePath.Display).ToArray();}
 public static string[] GetFiles(string p,string mask){return GetFiles(p,mask,SearchOption.TopDirectoryOnly);}
 public static string[] GetDirectories(string p,string mask){return System.IO.Directory.GetDirectories(NativePath.Io(p),mask).Select(NativePath.Display).ToArray();}
 public static string[] GetFileSystemEntries(string p){return System.IO.Directory.GetFileSystemEntries(NativePath.Io(p)).Select(NativePath.Display).ToArray();}
 public static DateTime GetCreationTimeUtc(string p){return System.IO.Directory.GetCreationTimeUtc(NativePath.Io(p));}
}
public sealed class NativeFileInfo {readonly System.IO.FileInfo info;public NativeFileInfo(string p){info=new System.IO.FileInfo(NativePath.Io(p));}public long Length{get{return info.Length;}}}
}
