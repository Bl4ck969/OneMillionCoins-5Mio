using System;
using System.IO;
using System.Security.Cryptography;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Mod-Version und die Spielstände, auf denen die Mod geprüft ist. Plugin (beim Laden) und Installer (vor der
    /// Installation) fragen beide hier – eine Liste an einer Stelle. Nach einem Spiel-Update: Prüfläufe mit
    /// [Kompatibilitaet] UngepruefteVersion = true, dann den neuen Stand hier eintragen (ERKENNTNISSE.md, Abschnitt 9).
    /// </summary>
    internal static class ReleaseInfo
    {
        internal const string ModVersion = "1.0.0";
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

        /// <summary>Geprüft 30.10.2026: Stand nach dem Steam-Update vom 29.10.2026, 18:28.</summary>
        internal static readonly GameBuild[] Tested =
        {
            new GameBuild
            {
                Version = "1.2.2", SteamBuild = "26290693",
                Code = "6c77d043119ffdd855df637240e799fed6e8ab6c808fb495f762b3010380b8b8",
                Settings = "bc103b390e95a26d7155f3312c9b438ae33f4964a915d05de0a7b9e6ec422558",
            },
        };

        internal static string TestedVersions => string.Join(", ", Array.ConvertAll(Tested, b => b.Version));

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
