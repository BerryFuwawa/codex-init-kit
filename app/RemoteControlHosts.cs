using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CodexKit {
internal static class RemoteControlHosts {
    internal const string Entry = "127.0.0.2 chatgpt.com # CodexInitKit Remote Relay";
    const string Block = "\r\n" + Entry + "\r\n";
    static readonly Encoding Bytes = Encoding.GetEncoding(28591);
    internal static string SystemFile { get { return Path.Combine(Environment.SystemDirectory, @"drivers\etc\hosts"); } }

    internal static bool HasEntry(string text) {
        return Regex.IsMatch(text, @"(?m)^" + Regex.Escape(Entry) + @"\r?$");
    }

    internal static string Read(string path) {
        try { return Bytes.GetString(File.ReadAllBytes(path)); }
        catch (FileNotFoundException) { return ""; }
    }

    internal static string Edit(string current, bool enabled) {
        if (!enabled) {
            int block = current.IndexOf(Block, StringComparison.Ordinal);
            if (block >= 0) {
                var before = current.Substring(0, block);
                var after = current.Substring(block + Block.Length);
                var separator = before.Length > 0 && !before.EndsWith("\n") && after.Length > 0 ? "\r\n" : "";
                return before + separator + after;
            }
            return Regex.Replace(current, @"(?m)^" + Regex.Escape(Entry) + @"(?:\r?\n|$)", "");
        }
        foreach (var line in current.Split('\n')) {
            if (line.TrimEnd('\r') == Entry) continue;
            if (Regex.Split(line.Split('#')[0].Trim(), @"\s+").Skip(1)
                    .Any(name => name.TrimEnd('.').Equals("chatgpt.com", StringComparison.OrdinalIgnoreCase)))
                throw new IOException("hosts 已有 chatgpt.com 映射，请检查原有条目后再开启。");
        }
        return HasEntry(current) ? current : current + Block;
    }

    internal static void Set(string path, bool enabled) {
        using (var file = new FileStream(path, enabled ? FileMode.OpenOrCreate : FileMode.Open, FileAccess.ReadWrite, FileShare.Read)) {
            var old = new byte[checked((int)file.Length)];
            using (var reader = new BinaryReader(file, Bytes, true)) {
                if (reader.Read(old, 0, old.Length) != old.Length) throw new IOException("未能完整读取 hosts。");
            }
            var text = Bytes.GetString(old);
            var updated = Edit(text, enabled);
            if (updated == text) return;
            var data = Bytes.GetBytes(updated);
            // Keep the original file and its ACL. Append on enable; retain unrelated edits on disable.
            if (enabled) {
                file.Write(data, old.Length, data.Length - old.Length);
            } else {
                file.Position = 0;
                file.Write(data, 0, data.Length);
                file.SetLength(data.Length);
            }
            file.Flush(true);
        }
    }
}
}

