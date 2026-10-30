using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace OneMillionCoins5Mio.Installer
{
    /// <summary>Das Spiel und was Steam darüber weiß: Registry → libraryfolders.vdf → appmanifest_5258340.acf.</summary>
    internal sealed class SteamGame
    {
        internal const string AppId = "5258340";
        internal const string ExeName = "One Million Coins.exe";
        internal const string ProcessName = "One Million Coins";

        internal string Folder;
        /// <summary>Steam-Hauptordner; null, wenn Steam nicht gefunden wurde.</summary>
        internal string SteamPath;
        /// <summary>Angaben aus dem appmanifest; null, wenn der Ordner von Hand gewählt wurde und kein Manifest dazu passt.</summary>
        internal string BuildId, StateFlags, DepotId, ManifestId, LibraryFolder;

        internal static bool IsGameFolder(string folder) =>
            File.Exists(Path.Combine(folder, ExeName)) && File.Exists(ReleaseInfo.CodeFile(folder));

        /// <summary>Steam fertig mit dem Spiel? StateFlags 4 = vollständig installiert, alles andere = Update ausstehend/läuft.</summary>
        internal bool SteamBusy => StateFlags != null && StateFlags != "4";

        internal static SteamGame Locate()
        {
            string steam = FindSteam();
            if (steam == null) return null;
            foreach (string library in Libraries(steam))
            {
                string acf = Path.Combine(library, "steamapps", "appmanifest_" + AppId + ".acf");
                if (!File.Exists(acf)) continue;
                var game = FromManifest(acf, steam);
                if (game != null && IsGameFolder(game.Folder)) return game;
            }
            return null;
        }

        /// <summary>Von Hand gewählter Ordner: Steam-Angaben trotzdem nutzen, wenn er in einer Steam-Bibliothek liegt.</summary>
        internal static SteamGame ForFolder(string folder)
        {
            folder = Path.GetFullPath(folder).TrimEnd('\\');
            string steam = FindSteam();
            // <Bibliothek>\steamapps\common\<Spiel> → <Bibliothek>\steamapps\appmanifest_5258340.acf
            var steamapps = Directory.GetParent(folder)?.Parent;
            string acf = steamapps == null ? null : Path.Combine(steamapps.FullName, "appmanifest_" + AppId + ".acf");
            var game = acf != null && File.Exists(acf) ? FromManifest(acf, steam) : null;
            if (game == null || !string.Equals(game.Folder, folder, StringComparison.OrdinalIgnoreCase))
                game = new SteamGame { Folder = folder, SteamPath = steam };
            return game;
        }

        internal static string FindSteam()
        {
            var places = new[]
            {
                (Registry.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
                (Registry.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"),
            };
            foreach (var (hive, key, name) in places)
            {
                using (var k = hive.OpenSubKey(key))
                    if (k?.GetValue(name) is string path && Directory.Exists(path)) return Path.GetFullPath(path);
            }
            return null;
        }

        internal static List<string> Libraries(string steam)
        {
            var result = new List<string> { steam };
            string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), @"""path""\s+""([^""]+)"""))
                    result.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static SteamGame FromManifest(string acf, string steam)
        {
            string text = File.ReadAllText(acf);
            string Value(string key)
            {
                var m = Regex.Match(text, @"""" + key + @"""\s+""([^""]*)""", RegexOptions.IgnoreCase);
                return m.Success ? m.Groups[1].Value : null;
            }
            string installdir = Value("installdir");
            if (string.IsNullOrEmpty(installdir)) return null;
            string steamapps = Path.GetDirectoryName(acf);
            var depot = Regex.Match(text, @"""InstalledDepots""\s*\{\s*""(\d+)""\s*\{\s*""manifest""\s+""(\d+)""", RegexOptions.IgnoreCase);
            return new SteamGame
            {
                Folder = Path.Combine(steamapps, "common", installdir),
                SteamPath = steam,
                LibraryFolder = Path.GetDirectoryName(steamapps),
                BuildId = Value("buildid"),
                StateFlags = Value("StateFlags"),
                DepotId = depot.Success ? depot.Groups[1].Value : null,
                ManifestId = depot.Success ? depot.Groups[2].Value : null,
            };
        }

        /// <summary>Läuft das Spiel aus genau diesem Ordner? Ist das nicht feststellbar, gilt es als laufend.</summary>
        internal bool IsRunning()
        {
            foreach (var process in Process.GetProcessesByName(ProcessName))
            {
                try
                {
                    string exe = process.MainModule?.FileName;
                    if (exe == null || string.Equals(Path.GetDirectoryName(exe), Folder, StringComparison.OrdinalIgnoreCase)) return true;
                }
                catch (Exception)
                {
                    return true;
                }
                finally
                {
                    process.Dispose();
                }
            }
            return false;
        }
    }
}
