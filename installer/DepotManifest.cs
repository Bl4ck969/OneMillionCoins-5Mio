using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OneMillionCoins5Mio.Installer
{
    /// <summary>
    /// Steams Dateiliste des Spiels (depotcache\{Depot}_{Manifest}.manifest): Name, Größe und SHA-1 jeder Datei. Damit lässt
    /// sich nachweisen, dass alle Originaldateien da und unverändert sind und was sonst im Ordner liegt (30.10.2026: 229 Dateien).
    /// </summary>
    internal static class DepotManifest
    {
        private const uint PayloadMagic = 0x71F617D0;
        private const uint MetadataMagic = 0x1F4812BE;
        private const uint DirectoryFlag = 64;

        internal sealed class Entry
        {
            internal string Path;
            internal long Size;
            internal string Sha1;
        }

        internal sealed class Result
        {
            internal int Unchanged;
            internal readonly List<string> Missing = new List<string>();
            internal readonly List<string> Changed = new List<string>();
            internal readonly List<string> Extra = new List<string>();
            internal bool Original => Missing.Count == 0 && Changed.Count == 0 && Extra.Count == 0;
        }

        /// <summary>Die Manifest-Datei zum installierten Stand, oder null (Steam nicht gefunden, Ordner von Hand gewählt …).</summary>
        internal static string Find(SteamGame game)
        {
            if (game.DepotId == null || game.ManifestId == null) return null;
            string name = game.DepotId + "_" + game.ManifestId + ".manifest";
            foreach (string root in new[] { game.SteamPath, game.LibraryFolder })
            {
                if (root == null) continue;
                string path = System.IO.Path.Combine(root, "depotcache", name);
                if (File.Exists(path)) return path;
                path = System.IO.Path.Combine(root, "steamapps", "depotcache", name);
                if (File.Exists(path)) return path;
            }
            return null;
        }

        /// <summary>Dateien des Manifests (ohne Ordner); null, wenn Steam die Namen verschlüsselt abgelegt hat.</summary>
        internal static List<Entry> Read(string file)
        {
            byte[] b = File.ReadAllBytes(file);
            var entries = new List<Entry>();
            bool encrypted = false;
            int pos = 0;
            while (pos + 8 <= b.Length)
            {
                uint magic = BitConverter.ToUInt32(b, pos);
                int length = BitConverter.ToInt32(b, pos + 4);
                pos += 8;
                if (magic == PayloadMagic)
                {
                    Fields(b, pos, pos + length, (field, _, start, size) =>
                    {
                        if (field != 1) return;
                        var entry = new Entry();
                        ulong flags = 0;
                        Fields(b, start, start + size, (f, number, s, n) =>
                        {
                            if (f == 1) entry.Path = Encoding.UTF8.GetString(b, s, n).Replace('/', '\\');
                            else if (f == 2) entry.Size = (long)number;
                            else if (f == 3) flags = number;
                            else if (f == 5) entry.Sha1 = Hex(b, s, n);
                        });
                        if ((flags & DirectoryFlag) == 0 && entry.Path != null) entries.Add(entry);
                    });
                }
                else if (magic == MetadataMagic)
                {
                    Fields(b, pos, pos + length, (field, number, _, __) => { if (field == 4 && number != 0) encrypted = true; });
                }
                else break;  // Signatur und Endmarke werden nicht gebraucht
                pos += length;
            }
            return encrypted ? null : entries;
        }

        internal static Result Verify(string folder, List<Entry> entries, Action<long, long> progress)
        {
            var result = new Result();
            long total = entries.Sum(e => e.Size), done = 0;
            foreach (var entry in entries)
            {
                string path = System.IO.Path.Combine(folder, entry.Path);
                if (!File.Exists(path)) result.Missing.Add(entry.Path);
                else if (new FileInfo(path).Length != entry.Size || Sha1(path) != entry.Sha1) result.Changed.Add(entry.Path);
                else result.Unchanged++;
                done += entry.Size;
                progress?.Invoke(done, total);
            }
            var known = new HashSet<string>(entries.Select(e => e.Path), StringComparer.OrdinalIgnoreCase);
            foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                string relative = file.Substring(folder.Length).TrimStart('\\');
                if (!known.Contains(relative)) result.Extra.Add(relative);
            }
            return result;
        }

        /// <summary>Protobuf-Felder eines Abschnitts: Feldnummer, Zahlenwert (Typ 0) oder Lage der Bytes (Typ 2).</summary>
        private static void Fields(byte[] b, int pos, int end, Action<int, ulong, int, int> visit)
        {
            while (pos < end)
            {
                ulong key = Varint(b, ref pos);
                int field = (int)(key >> 3), type = (int)(key & 7);
                switch (type)
                {
                    case 0:
                        visit(field, Varint(b, ref pos), 0, 0);
                        break;
                    case 2:
                        int n = (int)Varint(b, ref pos);
                        visit(field, 0, pos, n);
                        pos += n;
                        break;
                    case 1:
                        pos += 8;
                        break;
                    case 5:
                        pos += 4;
                        break;
                    default:
                        throw new InvalidDataException("Steam-Manifest: unbekannter Feldtyp " + type);
                }
            }
        }

        private static ulong Varint(byte[] b, ref int pos)
        {
            ulong value = 0;
            for (int shift = 0; ; shift += 7)
            {
                byte x = b[pos++];
                value |= (ulong)(x & 0x7F) << shift;
                if (x < 0x80) return value;
            }
        }

        private static string Hex(byte[] b, int start, int count) =>
            BitConverter.ToString(b, start, count).Replace("-", "").ToLowerInvariant();

        private static string Sha1(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sha = SHA1.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
