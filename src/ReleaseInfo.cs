using System;
using System.IO;
using System.Security.Cryptography;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Mod-Version und die Spielstände, auf denen die Mod geprüft ist. Plugin (beim Laden) und Installer (vor der
    /// Installation) fragen beide hier – eine Liste an einer Stelle. Nach einem Spiel-Update: Prüfläufe mit
    /// [Kompatibilitaet] UngepruefteVersion = true, dann den neuen Stand hier eintragen (ERKENNTNISSE.md, Abschnitt 9).
    /// Auf einem Stand, der hier fehlt, bleibt die Mod aus, bis der Spieler ihn freigibt (Fingerprint, seit 03.11.2026).
    /// </summary>
    internal static class ReleaseInfo
    {
        internal const string ModVersion = "1.1.0";
        internal const string PluginGuid = "de.bl4ck969.onemillioncoins.5mio";

        /// <summary>Mod-Spielstände unter %LOCALAPPDATA% (nicht LocalLow: das lädt Steam Cloud hoch).</summary>
        internal const string SaveFolderName = "OneMillionCoins-5Mio";

        internal sealed class GameBuild
        {
            /// <summary>Application.version, wie das Hauptmenü sie zeigt.</summary>
            internal string Version;
            /// <summary>buildid aus appmanifest_5258340.acf, nur zur Anzeige.</summary>
            internal string SteamBuild;
            /// <summary>SHA-256 von Assembly-CSharp.dll: der Code, an dem die Patches ansetzen.</summary>
            internal string Code;
            /// <summary>SHA-256 von globalgamemanagers: enthält die Versionsnummer, ändert sich mit jedem Build.</summary>
            internal string Settings;
        }

        /// <summary>
        /// Das Spiel zählt seine Versionsnummer nicht bei jedem Update hoch: Der Stand vom 03.11.2026 meldet weiterhin
        /// „1.2.2“. Unterscheiden lassen sich die Stände nur am Steam-Build und an den Prüfsummen.
        /// </summary>
        internal static readonly GameBuild[] Tested =
        {
            // Geprüft 30.10.2026 mit Mod 1.0.0: Stand nach dem Steam-Update vom 29.10.2026, 18:28. Steam liefert ihn nicht
            // mehr aus; Mod 1.1.0 behält für ihn den alten Weg der Haufen-Kollision (PilePatches), sonst ist nichts versionsabhängig.
            new GameBuild
            {
                Version = "1.2.2", SteamBuild = "26290693",
                Code = "6c77d043119ffdd855df637240e799fed6e8ab6c808fb495f762b3010380b8b8",
                Settings = "bc103b390e95a26d7155f3312c9b438ae33f4964a915d05de0a7b9e6ec422558",
            },
            // Geprüft 03.11.2026 mit Mod 1.1.0: Steam-Update vom 03.11.2026, 18:56 (Kollision des Haufens im Hintergrund,
            // Sprache Português BR, Schriftanpassung).
            new GameBuild
            {
                Version = "1.2.2", SteamBuild = "26367772",
                Code = "721beeb5361b4a6723253c8292887aabef291850804c44d9aaaf3713aa1490db",
                Settings = "8fca7e72108ef1078b7841bbb2e4d69512a270396763252b375b5b5e5ca1ba19",
            },
        };

        /// <summary>Geprüfte Stände zum Anzeigen, z. B. „1.2.2 (Build 26290693, 26367772)“.</summary>
        internal static string TestedVersions
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                foreach (var build in Tested)
                {
                    int at = parts.FindIndex(p => p.StartsWith(build.Version + " (Build "));
                    if (at < 0) parts.Add(build.Version + " (Build " + build.SteamBuild + ")");
                    else parts[at] = parts[at].TrimEnd(')') + ", " + build.SteamBuild + ")";
                }
                return string.Join(", ", parts.ToArray());
            }
        }

        /// <summary>
        /// Kurze Kennung genau dieses Spielstands (je 16 Zeichen der beiden Prüfsummen). Gibt der Spieler einen nicht
        /// geprüften Stand frei, steht sie in [Kompatibilitaet] FreigegebeneVersion; nach dem nächsten Update passt sie nicht
        /// mehr, und die Mod fragt neu. Null, wenn eine der beiden Dateien fehlt.
        /// </summary>
        internal static string Fingerprint(string gameRoot)
        {
            string code = Sha256(CodeFile(gameRoot)), settings = Sha256(SettingsFile(gameRoot));
            return code == null || settings == null ? null : code.Substring(0, 16) + "-" + settings.Substring(0, 16);
        }

        /// <summary>buildid aus Steams appmanifest neben dem Spielordner, nur zur Anzeige; null, wenn nicht lesbar.</summary>
        internal static string SteamBuildOf(string gameRoot)
        {
            try
            {
                string manifest = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(gameRoot.TrimEnd('\\', '/'))), "appmanifest_5258340.acf");
                if (!File.Exists(manifest)) return null;
                var match = System.Text.RegularExpressions.Regex.Match(File.ReadAllText(manifest), "\"buildid\"\\s+\"([0-9]+)\"");
                return match.Success ? match.Groups[1].Value : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        internal static string CodeFile(string gameRoot) =>
            Path.Combine(gameRoot, "One Million Coins_Data", "Managed", "Assembly-CSharp.dll");

        internal static string SettingsFile(string gameRoot) =>
            Path.Combine(gameRoot, "One Million Coins_Data", "globalgamemanagers");

        /// <summary>Der geprüfte Stand im Spielordner, oder null, wenn Code oder Einstellungen zu keinem passen.</summary>
        internal static GameBuild Identify(string gameRoot)
        {
            string code = Sha256(CodeFile(gameRoot));
            string settings = Sha256(SettingsFile(gameRoot));
            foreach (var build in Tested)
                if (code == build.Code && settings == build.Settings) return build;
            return null;
        }

        /// <summary>SHA-256 in Kleinbuchstaben; null, wenn die Datei fehlt. Liest auch Dateien, die das Spiel offen hält.</summary>
        internal static string Sha256(string path)
        {
            if (!File.Exists(path)) return null;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
