using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using static OneMillionCoins5Mio.Installer.Texts;

namespace OneMillionCoins5Mio.Installer
{
    internal static class Program
    {
        private const string Usage =
@"OMC-5Mio-Installer.exe                            Fenster öffnen / open window
  --pruefen                                       Spielordner und Version prüfen / check folder and version
  --spieldateien                                  Spielordner gegen Steams Dateiliste / verify against Steam's file list
  --installieren --einverstanden                  installieren, Hinweis akzeptiert / install, notice accepted
    [--ungeprueft]                                auch auf nicht geprüfter Spielversion, Mod dort eingeschaltet /
                                                  also on an untested game version, mod switched on there
  --deinstallieren [--spielstaende-loeschen]      entfernen, optional mit Mod-Spielständen / remove, optionally with mod saves
  --spielordner <Pfad>                            statt automatischer Suche / instead of automatic search
  --englisch                                      englische Texte / English texts
Rückgabe / exit code: 0 ok, 1 Fehler / error, 2 nicht möglich / not possible, 3 Hinweis nicht akzeptiert / notice not accepted";

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);

        /// <summary>Bildschirmfoto-Modus: Fenster außerhalb des Bildschirms und ohne Fokus – stört kein laufendes Spiel.</summary>
        internal static bool Snapshot;

        [STAThread]
        private static int Main(string[] args)
        {
            string Option(string name)
            {
                int i = Array.IndexOf(args, name);
                return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
            }
            bool Flag(string name) => args.Contains(name);

            if (Flag("--englisch")) German = false;
            string folder = Option("--spielordner");
            // Nur für Tests: Spielstände woanders als in %LOCALAPPDATA%, damit kein echter Spielstand angefasst wird.
            string saves = Option("--spielstand-ordner") ?? Setup.DefaultSaveFolder;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (Flag("--bildschirmfoto"))
            {
                Snapshot = true;
                MainForm.SaveSnapshots(Option("--bildschirmfoto"), folder, saves);
                return 0;
            }
            var commands = new[] { "--pruefen", "--spieldateien", "--installieren", "--deinstallieren", "--hilfe" };
            if (!args.Any(commands.Contains))
            {
                Application.Run(new MainForm(folder, saves));
                return 0;
            }

            if (Console.IsOutputRedirected)
                Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = true });
            else
                AttachConsole(-1);
            try
            {
                return Cli(Flag, folder, saves);
            }
            catch (Exception e) when (e is SetupException || e is IOException || e is UnauthorizedAccessException)
            {
                Console.WriteLine(L("FEHLER: ", "ERROR: ") + e.Message);
                return 1;
            }
            catch (Exception e)
            {
                Console.WriteLine(L("FEHLER: ", "ERROR: ") + e);
                return 1;
            }
        }

        private static int Cli(Func<string, bool> flag, string folder, string saves)
        {
            if (flag("--hilfe"))
            {
                Console.WriteLine(Usage);
                return 0;
            }
            var game = folder != null ? SteamGame.ForFolder(folder) : SteamGame.Locate();
            var check = Setup.Preflight(game, saves);
            Print(check.Findings);
            if (flag("--pruefen")) return check.CanInstall ? 0 : 2;
            if (flag("--spieldateien"))
            {
                if (game == null || !SteamGame.IsGameFolder(game.Folder)) return 2;
                Print(Setup.VerifyFiles(game, null));
                return 0;
            }
            if (flag("--installieren"))
            {
                if (!check.CanInstall) return 2;
                if (!flag("--einverstanden"))
                {
                    Console.WriteLine();
                    Console.WriteLine(string.Format(Disclaimer, ReleaseInfo.TestedVersions));
                    Console.WriteLine();
                    Console.WriteLine(L("Installiert wird nur mit --einverstanden.", "Installs only with --einverstanden (\"I agree\")."));
                    return 3;
                }
                if (check.Untested && !flag("--ungeprueft"))
                {
                    Console.WriteLine(L("Diese Spielversion ist nicht geprüft. Installieren und die Mod auf ihr einschalten – auf eigene Gefahr – mit --ungeprueft.",
                        "This game version is not tested. To install and switch the mod on for it – at your own risk – add --ungeprueft."));
                    return 2;
                }
                new Setup(game.Folder, saves, Console.WriteLine).Install(releaseUntested: check.Untested);
                return 0;
            }
            if (!check.CanUninstall) return 2;
            var problems = new Setup(game.Folder, saves, Console.WriteLine).Uninstall(flag("--spielstaende-loeschen"));
            foreach (string problem in problems) Console.WriteLine("PROBLEM: " + problem);
            Print(Setup.VerifyFiles(game, null));
            return problems.Count == 0 ? 0 : 1;
        }

        private static void Print(IEnumerable<Finding> findings)
        {
            foreach (var finding in findings)
                Console.WriteLine((finding.Level == Level.Ok ? "[OK]     " : finding.Level == Level.Note ? "[INFO]   " : L("[FEHLER] ", "[ERROR]  ")) + finding.Text);
        }
    }
}
