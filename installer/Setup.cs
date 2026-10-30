using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using static OneMillionCoins5Mio.Installer.Texts;

namespace OneMillionCoins5Mio.Installer
{
    internal sealed class SetupException : Exception
    {
        internal SetupException(string message) : base(message) { }
    }

    internal enum Level { Ok, Note, Error }

    internal sealed class Finding
    {
        internal Level Level;
        internal string Text;
    }

    /// <summary>
    /// Installieren und Deinstallieren. Jede Änderung am Spielordner steht vorher im Protokoll
    /// (5Mio-Mod-Installation\installation.txt): angelegte Ordner, neue Dateien, überschriebene Dateien samt Sicherung.
    /// Die Deinstallation macht genau das rückgängig – auch nach einem Abbruch mitten in der Installation.
    /// </summary>
    internal sealed class Setup
    {
        internal const string RecordFolder = "5Mio-Mod-Installation";
        private const string BackupFolder = "sicherung";
        internal const string PluginFile = @"BepInEx\plugins\OneMillionCoins5Mio\OneMillionCoins5Mio.dll";
        private const string OldPluginFolder = @"BepInEx\plugins\OneMillionCoins100M";
        internal const string ConfigFile = @"BepInEx\config\" + ReleaseInfo.PluginGuid + ".cfg";
        internal const string BepInExVersion = "5.4.23.6";
        /// <summary>SHA-256 von BepInEx_win_x64_5.4.23.6.zip laut GitHub-Release (geprüft 30.10.2026).</summary>
        private const string BepInExZipSha256 = "6c57a714a892efb4e9e1fef63d608a3391f30ec984031c465404e4ee381a0755";

        internal enum Loader { Missing, BepInEx5, Other }

        internal sealed class Check
        {
            internal readonly List<Finding> Findings = new List<Finding>();
            internal bool CanInstall, CanUninstall, Installed;
        }

        internal readonly string Game;
        internal readonly string SaveFolder;
        private readonly Action<string> log;

        internal Setup(string game, string saveFolder, Action<string> log)
        {
            Game = Path.GetFullPath(game).TrimEnd('\\');
            if (!SteamGame.IsGameFolder(Game)) throw new SetupException(L("Kein Spielordner: ", "Not a game folder: ") + Game);
            SaveFolder = saveFolder;
            this.log = log;
        }

        internal static string DefaultSaveFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ReleaseInfo.SaveFolderName);

        private string RecordDir => Path.Combine(Game, RecordFolder);
        private string RecordFile => Path.Combine(RecordDir, "installation.txt");
        internal bool Installed => File.Exists(RecordFile);

        internal Loader DetectLoader()
        {
            string core = Path.Combine(Game, @"BepInEx\core\BepInEx.dll");
            if (File.Exists(core))
                return System.Diagnostics.FileVersionInfo.GetVersionInfo(core).FileMajorPart == 5 ? Loader.BepInEx5 : Loader.Other;
            // winhttp.dll oder ein BepInEx-Ordner ohne BepInEx 5: ein anderer Mod-Lader oder ein Rest davon. Den überschreiben
            // wir nicht – und löschen ihn beim Deinstallieren nicht mit.
            return File.Exists(Path.Combine(Game, "winhttp.dll")) || Directory.Exists(Path.Combine(Game, "BepInEx"))
                ? Loader.Other
                : Loader.Missing;
        }

        // ---------------------------------------------------------------- Vorprüfung

        internal static Check Preflight(SteamGame game, string saveFolder)
        {
            var check = new Check();
            void Add(Level level, string text) => check.Findings.Add(new Finding { Level = level, Text = text });

            if (game == null || !SteamGame.IsGameFolder(game.Folder))
            {
                Add(Level.Error, game == null
                    ? L("Spiel nicht gefunden. Bitte den Spielordner von „One Million Coins“ wählen.",
                        "Game not found. Please choose the \"One Million Coins\" game folder.")
                    : L("Kein Spielordner (One Million Coins.exe fehlt): ", "Not a game folder (One Million Coins.exe missing): ") + game.Folder);
                return check;
            }
            Add(Level.Ok, L("Spielordner: ", "Game folder: ") + game.Folder);

            bool running = game.IsRunning();
            if (running) Add(Level.Error, L("Das Spiel läuft – bitte erst beenden.", "The game is running – please quit it first."));

            var build = ReleaseInfo.Identify(game.Folder);
            string steamBuild = game.BuildId != null ? L(" (Steam-Build ", " (Steam build ") + game.BuildId + ")" : "";
            if (build != null)
                Add(Level.Ok, L("Spielversion ", "Game version ") + build.Version + steamBuild + L(": geprüft", ": tested"));
            else
                Add(Level.Error, L("Diese Spielversion ist nicht geprüft", "This game version is not tested") + steamBuild +
                    L($". Die Mod läuft nur mit Version {ReleaseInfo.TestedVersions}.", $". The mod only runs with version {ReleaseInfo.TestedVersions}."));
            if (game.SteamBusy)
                Add(Level.Error, L("Steam hat ein Update für das Spiel offen – erst fertig laden lassen.",
                    "Steam has a pending update for the game – let it finish first."));

            var setup = new Setup(game.Folder, saveFolder, _ => { });
            var loader = setup.DetectLoader();
            check.Installed = setup.Installed;
            if (setup.Installed)
            {
                var record = new Record(setup.RecordFile);
                Add(Level.Ok, L("Mod installiert: Version ", "Mod installed: version ") + record.Value("mod") + L(" am ", " on ") + record.Value("datum") +
                    (record.Value("bepinex") == "installiert"
                        ? L(", mit BepInEx", ", with BepInEx")
                        : L(", BepInEx war schon vorhanden", ", BepInEx was already present")));
            }
            else
            {
                Add(Level.Note, L("Mod nicht installiert.", "Mod not installed."));
                if (loader == Loader.Missing)
                    Add(Level.Note, L($"BepInEx {BepInExVersion} (Mod-Lader) wird mitinstalliert.", $"BepInEx {BepInExVersion} (mod loader) will be installed as well."));
                else if (loader == Loader.BepInEx5)
                    Add(Level.Note, L("BepInEx 5 ist schon vorhanden und wird mitbenutzt; es bleibt beim Deinstallieren erhalten.",
                        "BepInEx 5 is already present and will be used; it stays when uninstalling."));
                if (File.Exists(Path.Combine(game.Folder, PluginFile)))
                    Add(Level.Note, L("Eine von Hand installierte Fassung der Mod liegt im Spielordner. Sie wird gesichert und beim Deinstallieren zurückgelegt.",
                        "A manually installed copy of the mod is in the game folder. It is backed up and put back when uninstalling."));
            }
            if (loader == Loader.Other)
                Add(Level.Error, L("Im Spielordner liegt ein anderer Mod-Lader oder ein Rest davon (winhttp.dll oder BepInEx-Ordner ohne BepInEx 5). Der Installer verändert ihn nicht.",
                    "The game folder contains a different mod loader or leftovers of one (winhttp.dll or a BepInEx folder without BepInEx 5). The installer does not touch it."));
            // Vor dem 30.10.2026 hieß die Mod „OneMillionCoins100M“; liegen beide Ordner da, laden zwei Fassungen der Mod.
            bool oldPlugin = Directory.Exists(Path.Combine(game.Folder, OldPluginFolder));
            if (oldPlugin)
                Add(Level.Error, L("Eine ältere Entwicklungsfassung der Mod liegt im Spielordner. Bitte erst diesen Ordner löschen: ",
                    "An older development copy of the mod is in the game folder. Please delete this folder first: ") + Path.Combine(game.Folder, OldPluginFolder));

            if (Directory.Exists(saveFolder))
                Add(Level.Note, L("Spielstände der Mod: ", "Mod saves: ") + saveFolder + " (" + Directory.GetFiles(saveFolder).Length + L(" Dateien)", " files)"));

            check.CanInstall = !running && build != null && !game.SteamBusy && loader != Loader.Other && !oldPlugin;
            check.CanUninstall = !running && setup.Installed;
            return check;
        }

        // ---------------------------------------------------------------- Installieren

        /// <summary>Installiert die Mod; ist sie schon installiert, wird sie vorher (ohne Spielstände) entfernt.</summary>
        internal void Install()
        {
            if (Installed)
            {
                log(L("Vorhandene Installation wird zuerst entfernt (Spielstände bleiben).", "Removing the existing installation first (saves are kept)."));
                var problems = Uninstall(deleteSaves: false);
                if (problems.Count > 0) throw new SetupException(string.Join(Environment.NewLine, problems));
            }
            var loader = DetectLoader();
            if (loader == Loader.Other) throw new SetupException(L("Anderer Mod-Lader im Spielordner – Installation abgebrochen.", "Different mod loader in the game folder – installation cancelled."));
            byte[] zip = Resource("BepInEx.zip");
            byte[] plugin = Resource("Plugin.dll");
            if (Sha256(zip) != BepInExZipSha256)
                throw new SetupException(L("Installer beschädigt: Das BepInEx-Paket hat die falsche Prüfsumme.", "Installer damaged: the BepInEx package has the wrong checksum."));

            Directory.CreateDirectory(RecordDir);
            var record = new Record(RecordFile);
            var build = ReleaseInfo.Identify(Game);
            record.Add("mod", ReleaseInfo.ModVersion);
            record.Add("spiel", build?.Version ?? "?", build?.SteamBuild ?? "?");
            record.Add("datum", DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            record.Add("bepinex", loader == Loader.Missing ? "installiert" : "vorhanden");
            if (File.Exists(Path.Combine(Game, ConfigFile))) record.Add("einstellungen", "vorhanden");
            try
            {
                if (loader == Loader.Missing)
                {
                    int count = 0;
                    using (var archive = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read))
                        foreach (var entry in archive.Entries)
                        {
                            // changelog.txt gehört nicht in den Spielordner; Ordner-Einträge legt Put selbst an.
                            if (entry.FullName.EndsWith("/") || entry.FullName == "changelog.txt") continue;
                            Put(record, "bepinex", entry.FullName.Replace('/', '\\'), ReadAll(entry));
                            count++;
                        }
                    log(L($"BepInEx {BepInExVersion} installiert ({count} Dateien).", $"BepInEx {BepInExVersion} installed ({count} files)."));
                }
                Put(record, "mod", PluginFile, plugin);
                log(L("Mod installiert: ", "Mod installed: ") + PluginFile);
            }
            catch (Exception)
            {
                log(L("Fehler – die Installation wird rückgängig gemacht.", "Error – rolling the installation back."));
                try
                {
                    foreach (string problem in Uninstall(deleteSaves: false)) log(problem);
                }
                catch (Exception rollback)
                {
                    log(rollback.Message);
                }
                throw;
            }

            // Nachprüfen am Ergebnis: jede geschriebene Datei mit der Prüfsumme, die geschrieben werden sollte.
            var written = record.All("neu").Select(l => (l[2], l[3])).Concat(record.All("gesichert").Select(l => (l[2], l[4]))).ToList();
            var wrong = written.Where(w => ReleaseInfo.Sha256(Path.Combine(Game, w.Item1)) != w.Item2).Select(w => w.Item1).ToList();
            if (wrong.Count > 0)
                throw new SetupException(L("Nach dem Schreiben abweichend: ", "Different after writing: ") + string.Join(", ", wrong));
            log(L($"Nachgeprüft: {written.Count} Dateien wie vorgesehen.", $"Verified: {written.Count} files as intended."));
        }

        private void Put(Record record, string part, string relative, byte[] content)
        {
            string target = Path.Combine(Game, relative);
            CreateFolders(record, Path.GetDirectoryName(target));
            string sha = Sha256(content);
            if (File.Exists(target))
            {
                // Flach und nummeriert statt im Originalpfad: sonst reißt ein langer Spielordner die 260-Zeichen-Grenze
                // (gemessen 30.10.2026 im Test). Wohin die Datei gehört, steht im Protokoll.
                string name = (record.All("gesichert").Count() + 1) + "_" + Path.GetFileName(relative);
                string backup = Path.Combine(RecordDir, BackupFolder, name);
                Directory.CreateDirectory(Path.GetDirectoryName(backup));
                File.Copy(target, backup, true);
                record.Add("gesichert", part, relative, ReleaseInfo.Sha256(backup), sha, name);
                log(L("Gesichert und ersetzt: ", "Backed up and replaced: ") + relative);
            }
            else
            {
                record.Add("neu", part, relative, sha);
            }
            File.WriteAllBytes(target, content);
        }

        private void CreateFolders(Record record, string folder)
        {
            var missing = new Stack<string>();
            for (string f = folder; f != null && f.Length > Game.Length && !Directory.Exists(f); f = Path.GetDirectoryName(f))
                missing.Push(f);
            while (missing.Count > 0)
            {
                string f = missing.Pop();
                record.Add("ordner", Relative(f));
                Directory.CreateDirectory(f);
            }
        }

        // ---------------------------------------------------------------- Deinstallieren

        /// <summary>Stellt den Zustand vor der Installation her. Rückgabe: Probleme (leer = vollständig).</summary>
        internal List<string> Uninstall(bool deleteSaves)
        {
            if (!Installed)
                throw new SetupException(L("Keine Installation dieses Installers gefunden.", "No installation by this installer found."));
            var record = new Record(RecordFile);
            var problems = new List<string>();

            bool ownLoader = record.Value("bepinex") == "installiert";
            var foreign = ownLoader ? ForeignMods(record) : new List<string>();
            bool removeLoader = ownLoader && foreign.Count == 0;
            if (ownLoader && !removeLoader)
                log(L("Andere Mods gefunden, BepInEx bleibt deshalb: ", "Other mods found, so BepInEx stays: ") + string.Join(", ", foreign));

            // 1. Was der Installer neu angelegt hat.
            foreach (var line in record.All("neu"))
                if (line[1] == "mod" || removeLoader) Delete(line[2], problems);
            // 2. Was BepInEx und die Mod beim Spielen anlegen: Log, Cache, Einstellungen.
            if (removeLoader) DeleteFolder("BepInEx", problems);
            else if (record.Value("einstellungen") == null) Delete(ConfigFile, problems);
            // 3. Überschriebene Dateien zurück.
            foreach (var line in record.All("gesichert"))
            {
                string backup = Path.Combine(RecordDir, BackupFolder, line[5]);
                string target = Path.Combine(Game, line[2]);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(backup, target, true);
                    if (ReleaseInfo.Sha256(target) != line[3]) problems.Add(L("Sicherung weicht ab: ", "Backup differs: ") + line[2]);
                    else log(L("Zurückgelegt: ", "Restored: ") + line[2]);
                }
                catch (Exception e)
                {
                    problems.Add(line[2] + ": " + e.Message);
                }
            }
            // 4. Angelegte Ordner, sofern leer – die tiefsten zuerst.
            foreach (var line in record.All("ordner").Reverse())
            {
                string dir = Path.Combine(Game, line[1]);
                if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
            }
            // 5. Protokoll und Sicherungen – nur, wenn alles zurück ist; sonst bleiben sie für einen zweiten Versuch.
            if (problems.Count == 0) DeleteFolder(RecordFolder, problems);
            else problems.Add(L("Protokoll und Sicherungen bleiben in ", "Record and backups remain in ") + RecordDir);
            log(problems.Count == 0
                ? L("Mod entfernt.", "Mod removed.")
                : L("Mod nicht vollständig entfernt.", "Mod not completely removed."));

            // 6. Spielstände der Mod, nur auf ausdrücklichen Wunsch.
            if (deleteSaves && Directory.Exists(SaveFolder))
            {
                int files = Directory.GetFiles(SaveFolder, "*", SearchOption.AllDirectories).Length;
                Directory.Delete(SaveFolder, true);
                log(L($"Spielstände der Mod gelöscht ({files} Dateien): ", $"Mod saves deleted ({files} files): ") + SaveFolder);
            }
            else if (Directory.Exists(SaveFolder))
            {
                log(L("Spielstände der Mod bleiben: ", "Mod saves kept: ") + SaveFolder);
            }
            return problems;
        }

        /// <summary>Dateien anderer Mods in BepInEx\plugins und \patchers – dann bleibt BepInEx beim Deinstallieren.</summary>
        private List<string> ForeignMods(Record record)
        {
            var ours = new HashSet<string>(record.All("neu").Concat(record.All("gesichert")).Select(l => l[2]), StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            foreach (string sub in new[] { @"BepInEx\plugins", @"BepInEx\patchers" })
            {
                string dir = Path.Combine(Game, sub);
                if (!Directory.Exists(dir)) continue;
                foreach (string file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                    if (!ours.Contains(Relative(file))) result.Add(Relative(file));
            }
            return result;
        }

        private void Delete(string relative, List<string> problems)
        {
            string path = Path.Combine(Game, relative);
            try
            {
                if (!File.Exists(path)) return;
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
            catch (Exception e)
            {
                problems.Add(relative + ": " + e.Message);
            }
        }

        /// <summary>Löscht einen Ordner direkt im Spielordner samt Inhalt (nur BepInEx und das Installationsprotokoll).</summary>
        private void DeleteFolder(string name, List<string> problems)
        {
            string path = Path.Combine(Game, name);
            try
            {
                if (!Directory.Exists(path)) return;
                foreach (string file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                    File.SetAttributes(file, FileAttributes.Normal);
                Directory.Delete(path, true);
            }
            catch (Exception e)
            {
                problems.Add(name + ": " + e.Message);
            }
        }

        // ---------------------------------------------------------------- Originalzustand nachweisen

        /// <summary>Spielordner gegen Steams Dateiliste: fehlt, weicht ab, liegt zusätzlich herum?</summary>
        internal static List<Finding> VerifyFiles(SteamGame game, Action<long, long> progress)
        {
            var result = new List<Finding>();
            void Add(Level level, string text) => result.Add(new Finding { Level = level, Text = text });
            string file = game == null ? null : DepotManifest.Find(game);
            var entries = file == null ? null : DepotManifest.Read(file);
            if (entries == null)
            {
                Add(Level.Note, L("Steams Dateiliste ist hier nicht lesbar. Prüfen geht auch in Steam: Spiel → Eigenschaften → Installierte Dateien → Dateien auf Fehler überprüfen.",
                    "Steam's file list is not readable here. You can also check in Steam: game → Properties → Installed Files → Verify integrity of game files."));
                return result;
            }
            var check = DepotManifest.Verify(game.Folder, entries, progress);
            bool allThere = check.Missing.Count == 0 && check.Changed.Count == 0;
            Add(allThere ? Level.Ok : Level.Error,
                L($"Originaldateien laut Steam: {check.Unchanged} von {entries.Count} unverändert.", $"Original files according to Steam: {check.Unchanged} of {entries.Count} unchanged."));
            if (check.Missing.Count > 0) Add(Level.Error, L("Fehlen: ", "Missing: ") + Short(check.Missing));
            if (check.Changed.Count > 0) Add(Level.Error, L("Verändert: ", "Changed: ") + Short(check.Changed));
            if (check.Extra.Count == 0)
            {
                if (allThere) Add(Level.Ok, L("Keine zusätzlichen Dateien: Der Spielordner ist im Originalzustand.", "No additional files: the game folder is in its original state."));
            }
            else
            {
                var groups = check.Extra.GroupBy(p => p.Split('\\')[0], StringComparer.OrdinalIgnoreCase)
                    .Select(g => g.Count() == 1 ? g.Key : g.Key + " (" + g.Count() + ")");
                Add(Level.Note, L($"Zusätzlich im Spielordner ({check.Extra.Count} Dateien): ", $"Additional in the game folder ({check.Extra.Count} files): ") + string.Join(", ", groups));
            }
            if (check.Missing.Count > 0 || check.Changed.Count > 0)
                Add(Level.Note, L("Steam kann das reparieren: Spiel → Eigenschaften → Installierte Dateien → Dateien auf Fehler überprüfen.",
                    "Steam can repair this: game → Properties → Installed Files → Verify integrity of game files."));
            return result;
        }

        private static string Short(List<string> items) =>
            string.Join(", ", items.Take(8)) + (items.Count > 8 ? L($" und {items.Count - 8} weitere", $" and {items.Count - 8} more") : "");

        // ---------------------------------------------------------------- Hilfen

        private string Relative(string path) => path.Substring(Game.Length).TrimStart('\\');

        private static byte[] Resource(string name)
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (stream == null) throw new SetupException(L("Installer beschädigt: ", "Installer damaged: ") + name + L(" fehlt.", " missing."));
                var memory = new MemoryStream();
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        internal static string ResourceText(string name) => System.Text.Encoding.UTF8.GetString(Resource(name));

        private static byte[] ReadAll(ZipArchiveEntry entry)
        {
            using (var stream = entry.Open())
            {
                var memory = new MemoryStream();
                stream.CopyTo(memory);
                return memory.ToArray();
            }
        }

        private static string Sha256(byte[] data)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }

        /// <summary>Protokoll als Textdatei, eine Zeile je Schritt, Felder durch Tabulator getrennt; jede Zeile sofort auf der Platte.</summary>
        private sealed class Record
        {
            private readonly string file;
            private readonly List<string[]> lines = new List<string[]>();

            internal Record(string file)
            {
                this.file = file;
                if (File.Exists(file))
                    foreach (string line in File.ReadAllLines(file))
                        if (line.Length > 0 && line[0] != '#') lines.Add(line.Split('\t'));
            }

            internal void Add(params string[] fields)
            {
                if (!File.Exists(file))
                    File.WriteAllText(file, L("# Installationsprotokoll der Mod „5 Mio. Münzen“. Nicht von Hand ändern – die Deinstallation liest es.",
                        "# Installation record of the \"5 Million Coins\" mod. Do not edit – uninstalling reads it.") + Environment.NewLine);
                lines.Add(fields);
                File.AppendAllText(file, string.Join("\t", fields) + Environment.NewLine);
            }

            internal string Value(string key) => lines.FirstOrDefault(l => l[0] == key && l.Length > 1)?[1];

            internal IEnumerable<string[]> All(string key) => lines.Where(l => l[0] == key);
        }
    }
}
