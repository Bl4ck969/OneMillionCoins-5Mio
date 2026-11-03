using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using GoldDigger;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneMillionCoins5Mio
{
    [BepInPlugin(Guid, "One Million Coins - 5 Mio.", ReleaseInfo.ModVersion)]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Guid = ReleaseInfo.PluginGuid;

        internal static ManualLogSource Log;
        internal static ConfigEntry<int> FloorCount;
        internal static ConfigEntry<float> TopFloorHeight;
        internal static ConfigEntry<float> DeepFloorHeight;
        internal static ConfigEntry<int> CoinTotal;
        internal static ConfigEntry<string> StationGroups;
        internal static ConfigEntry<bool> PerformanceAlways;
        internal static ConfigEntry<string> CheckMode;
        internal static ConfigEntry<string> CheckFolder;
        internal static ConfigEntry<string> Vault;
        internal static ConfigEntry<float> LiftSpeed;
        internal static ConfigEntry<float> LiftRamp;
        internal static ConfigEntry<bool> AllowUntested;
        internal static ConfigEntry<string> ReleasedBuild;

        /// <summary>Kennung des laufenden Spielstands (ReleaseInfo.Fingerprint); im Prüfmodus „versionsschutz“ eine erfundene.</summary>
        internal static string CurrentFingerprint;
        /// <summary>Spielversion und Steam-Build zum Anzeigen, z. B. „1.2.2 (Build 26367772)“.</summary>
        internal static string CurrentBuildLabel;
        /// <summary>Datei der Mod-Einstellungen (für die Prüfung, ob eine Freigabe wirklich auf der Platte steht).</summary>
        internal static string ConfigPath;

        /// <summary>Kennung, die der Prüfmodus „versionsschutz“ vorspielt – trifft nie einen echten Spielstand.</summary>
        internal const string SimulatedFingerprint = "pruefung-versionsschutz";

        /// <summary>Zustand für den Hinweis im Hauptmenü; null = geprüfter Stand, kein Hinweis.</summary>
        private static VersionNotice.Kind? notice;

        private void Awake()
        {
            Log = Logger;
            // Ohne BepInEx-Konsole gehen alle Logzeilen auch ins Unity-Log Player.log –
            // das lädt Steam Cloud mit dem ganzen LocalLow-Ordner hoch (gemessen 26.10.2026). Mod-Zeilen bleiben in
            // BepInEx\LogOutput.log im Spielordner.
            // Ohne Konsole hängt BepInEx einen UnityLogListener an, der ins Unity-Log (Player.log) schreibt. Nur dann wird er entfernt.
            var stdout = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(BepInEx.Logging.Logger.Listeners,
                l => (l is UnityLogListener || l is ConsoleLogListener) && !ConsoleActive()));
            foreach (var listener in stdout) BepInEx.Logging.Logger.Listeners.Remove(listener);
            int removed = stdout.Count;
            Log.LogInfo($"Standardausgabe-Logger entfernt: {removed} (Mod-Zeilen nicht mehr in Player.log).");
            FloorCount = Config.Bind("Etagen", "Anzahl", 25,
                new ConfigDescription("Etagen insgesamt, von 00 (oben) bis zur 16-m-Bodenhalle.", new AcceptableValueRange<int>(6, 40)));
            TopFloorHeight = Config.Bind("Etagen", "HoeheOben", 4.2f,
                new ConfigDescription("Höhe der obersten Etage in Metern.", new AcceptableValueRange<float>(3.5f, 16f)));
            // 14,12 m: bei 25 Etagen ab 4,2 m fasst das Gebäude genau 5 Mio. Münzen in der Füllhöhe des Originals (26.10.2026).
            DeepFloorHeight = Config.Bind("Etagen", "HoeheUnten", 14.12f,
                new ConfigDescription("Höhe der tiefsten Etage über der Bodenhalle in Metern; dazwischen wächst jede Etage um denselben Faktor.",
                    new AcceptableValueRange<float>(3.5f, 16f)));
            CoinTotal = Config.Bind("Etagen", "Muenzen", 5000000,
                new ConfigDescription("Münzen im Haufen. Wie im Original liegt die letzte davon am Ende in der Kiste (gesammelt werden alle bis auf eine).",
                    new AcceptableValueRange<int>(1000000, 20000000)));
            // Bl4ck969, 30.10.2026: „der erste Skillpunkt Etage 1 und 2, der zweite 3 bis 5 usw.“
            StationGroups = Config.Bind("Etagen", "Automatengruppen", "2,5,9,15,24",
                "Letzte Etage, deren Einzahlautomat der 1. bis 5. Skill „Unterer Trichter“ freischaltet (5 aufsteigende Zahlen). " +
                "Der 5. reicht immer bis zur untersten Etage. Etage 00 ist immer offen.");
            Vault = Config.Bind("Menue", "Tresor", "Original",
                new ConfigDescription("Auswahl im Hauptmenü: \"Original\" = 1 Million, \"5Mio\" = Mod. Wird im Menü umgeschaltet.",
                    new AcceptableValueList<string>("Original", "5Mio")));
            // Das Spiel fährt jede Strecke als eine S-Kurve mit 7 m/s Spitze; bei 200 m dauerten Anfahren und Bremsen je fast
            // eine halbe Minute (Bl4ck969, 29.10.2026). Kurze Fahrten bis Anfahr- plus Bremsweg bleiben wie im Original.
            LiftSpeed = Config.Bind("Aufzug", "Geschwindigkeit", 7f,
                new ConfigDescription("Höchstgeschwindigkeit des Aufzugs in m/s (im Original die Spitze der Fahrt: 7).", new AcceptableValueRange<float>(2f, 30f)));
            LiftRamp = Config.Bind("Aufzug", "Anfahrzeit", 1.5f,
                new ConfigDescription("Sekunden bis zur Höchstgeschwindigkeit; ebenso lang wird gebremst.", new AcceptableValueRange<float>(0.3f, 5f)));
            CheckMode = Config.Bind("Pruefung", "Modus", "aus",
                new ConfigDescription("Nur für die Entwicklung und für Prüfläufe nach einem Spiel-Update: startet automatisch eine Prüfaufnahme, " +
                    "steuert dabei die Spielfigur und beendet das Spiel danach. " +
                    "\"original\" = unverändertes Gebäude, \"mod\" = 5-Mio.-Gebäude, \"inventar\" = Teile-Inventar des Originals, " +
                    "\"leistung\" = 60 s Abgraben simulieren und Bildrate messen, \"leistung-original\" = dasselbe im unveränderten Gebäude, " +
                    "\"spielstand\" = speichern, ins Menü, weiterspielen, \"spielstand-laden\" = vorhandenen Test-Spielstand laden (z. B. aus einer " +
                    "früheren Fassung), ohne Speichern zurück, echte Bestenliste ansehen, \"fertigkeiten\" = geänderte Fertigkeiten und Taschenbank-Rückruf messen, " +
                    "\"versionsschutz\" = so tun, als wäre die Spielversion nicht geprüft, Hauptmenü fotografieren, Einschalten und Ausschalten prüfen, " +
                    "\"versionsschutz-fehler\" = dasselbe mit einem vorgetäuschten Fehler beim Patchen, " +
                    "\"bilder\" = Aufnahmen für README und Release. Alles offline.",
                    new AcceptableValueList<string>("aus", "original", "mod", "inventar", "leistung", "leistung-original", "spielstand", "spielstand-laden", "fertigkeiten",
                        "versionsschutz", "versionsschutz-fehler", "bilder")));
            // Gespeicherte Werte bleiben; die Voreinstellungen gelten für neue Installationen (30.10.2026, vor der Veröffentlichung).
            CheckFolder = Config.Bind("Pruefung", "Ordner", System.IO.Path.Combine(Paths.BepInExRootPath, "pruefung"), "Zielordner der Prüfaufnahmen.");
            PerformanceAlways = Config.Bind("Prototyp", "LeistungAuchImNormalspiel", false,
                "Schreibt die Leistungswerte auch im normalen Spiel ins Log (nur zum Vergleich, ändert nichts am Spiel).");
            AllowUntested = Config.Bind("Kompatibilitaet", "UngepruefteVersion", false,
                "Mod auf jeder nicht geprüften Spielversion laden, ohne zu fragen. Für Prüfläufe nach einem Spiel-Update; " +
                "als Spieler genügt FreigegebeneVersion (Knopf im Hauptmenü).");
            ReleasedBuild = Config.Bind("Kompatibilitaet", "FreigegebeneVersion", "",
                "Kennung eines nicht geprüften Spielstands, auf dem die Mod trotzdem läuft – auf eigene Gefahr. Wird im Hauptmenü " +
                "(\"Trotzdem einschalten …\") oder vom Installer eingetragen, leer = keiner. Nach dem nächsten Spiel-Update passt sie nicht mehr: " +
                "Dann bleibt die Mod wieder aus und fragt neu.");
            ConfigPath = Config.ConfigFilePath;

            // Nach einem Steam-Update kann ein Patch an anderer Stelle greifen als gedacht – auch im Original mit Bestenlisten.
            // Auf einem nicht geprüften Stand bleibt das Spiel deshalb zunächst unverändert (30.10.2026). Seit 03.11.2026 kann
            // der Spieler genau diesen Stand im Hauptmenü freigeben (Bl4ck969: „nicht so radikal … dass man es trotzdem zum
            // Laufen bekommt“). Prüfmodus „versionsschutz“ spielt einen unbekannten Stand mit erfundener Kennung vor.
            bool simulate = CheckMode.Value == "versionsschutz" || CheckMode.Value == "versionsschutz-fehler";
            var build = simulate ? null : ReleaseInfo.Identify(Paths.GameRootPath);
            CurrentFingerprint = simulate ? SimulatedFingerprint : ReleaseInfo.Fingerprint(Paths.GameRootPath);
            string steamBuild = ReleaseInfo.SteamBuildOf(Paths.GameRootPath);
            CurrentBuildLabel = Application.version + (steamBuild != null ? " (Build " + steamBuild + ")" : "");
            if (build == null)
            {
                if (!Released)
                {
                    Log.LogWarning($"Spielversion {CurrentBuildLabel} ist nicht geprüft (geprüft: {ReleaseInfo.TestedVersions}). " +
                        "Mod aus, das Spiel läuft unverändert; einschalten über das Hauptmenü." + (simulate ? " (Prüfmodus " + CheckMode.Value + ")" : ""));
                    ShowNotice(VersionNotice.Kind.Off);
                    return;
                }
                Log.LogWarning($"Spielversion {CurrentBuildLabel} ist NICHT geprüft – vom Spieler freigegeben ({(AllowUntested.Value && !simulate ? "UngepruefteVersion" : "FreigegebeneVersion")}), Mod lädt trotzdem.");
                notice = VersionNotice.Kind.Untested;
            }
            else
            {
                Log.LogInfo($"Spielversion {build.Version} (Build {build.SteamBuild}): geprüft.");
            }

            // Fehlt auf einem fremden Stand eine Methode oder ein Feld, wirft ein Modul. Dann alles zurücknehmen statt halb gepatcht
            // weiterzulaufen – das Spiel ist danach wieder das Original. Was ein Modul außerhalb von Harmony gesetzt hat (z. B. die
            // HUD-Felder des Asts Bewegung), bleibt; im Original ohne Wirkung.
            var harmony = new Harmony(Guid);
            try
            {
                OfflinePatches.Apply(harmony);
                WorldPatches.Apply(harmony);
                GoalPatches.Apply(harmony);
                HudPatches.Apply(harmony);
                ElevatorPatches.Apply(harmony);
                PilePatches.Apply(harmony);
                PoseBand.Apply(harmony);
                MenuPatches.Apply(harmony);
                Leaderboard.Apply(harmony);
                CompletionPatches.Apply(harmony);
                SkillPatches.Apply(harmony);
                MovementBranch.Apply(harmony);
                if (CheckMode.Value == "versionsschutz-fehler")
                    throw new System.InvalidOperationException("Prüfmodus versionsschutz-fehler: vorgetäuschter Fehler nach allen Modulen.");
            }
            catch (System.Exception e)
            {
                int applied = System.Linq.Enumerable.Count(harmony.GetPatchedMethods());
                harmony.UnpatchSelf();
                Log.LogError($"Mod konnte nicht geladen werden, {applied} Patches zurückgenommen – das Spiel läuft unverändert: {e}");
                ShowNotice(VersionNotice.Kind.Failed);
                return;
            }
            Log.LogInfo($"Gepatchte Methoden: {System.Linq.Enumerable.Count(harmony.GetPatchedMethods())}.");
            if (notice.HasValue) ShowNotice(notice.Value);

            SceneManager.activeSceneChanged += OnSceneChanged;
            Log.LogInfo(ModState.OfflineGuardsReady
                ? "Bereit. Offline-Sperren gesetzt."
                : "Offline-Sperren unvollständig: Der 5-Mio.-Modus bleibt gesperrt.");
        }

        /// <summary>
        /// Testmodus des Spiels mit allen Fertigkeiten: nur noch in Prüfläufen (eigener Spielstand). Die Schalter
        /// „AlleFertigkeiten“/„TestPunkte“ für das normale Spiel sind seit 29.10.2026 entfernt (Bl4ck969: Start bei null).
        /// </summary>
        internal static bool TestLoadout => CheckMode.Value != "aus" && CheckMode.Value != "spielstand-laden";

        /// <summary>
        /// Hinweis erst nach dem Laden einer Szene anlegen. Der Chainloader läuft in Application..cctor; eine Komponente mit
        /// Start/OnGUI, dort angelegt, bringt Unity 6000.6 nativ zum Absturz – als AddComponent aus Awake und als
        /// Start() am Plugin selbst (beides im Prüfmodus versionsschutz, 30.10.2026). Das Plugin bekommt deshalb keine
        /// Unity-Ereignismethoden.
        /// </summary>
        private static void ShowVersionNotice(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= ShowVersionNotice;
            VersionNotice.Show(notice.Value);
        }

        private static void ShowNotice(VersionNotice.Kind kind)
        {
            notice = kind;
            SceneManager.sceneLoaded += ShowVersionNotice;
        }

        /// <summary>Hat der Spieler den laufenden, nicht geprüften Stand freigegeben? Der Prüfmodus beachtet nur seine eigene Kennung.</summary>
        internal static bool Released =>
            (AllowUntested.Value && CurrentFingerprint != SimulatedFingerprint) ||
            (CurrentFingerprint != null && ReleasedBuild.Value.Trim() == CurrentFingerprint);

        /// <summary>BepInEx.ConsoleManager ist intern: per Reflexion fragen, ob ein Konsolenfenster offen ist.</summary>
        private static bool ConsoleActive()
        {
            var property = AccessTools.Property(AccessTools.TypeByName("BepInEx.ConsoleManager"), "ConsoleActive");
            return property != null && (bool)property.GetValue(null);
        }

        private static void OnSceneChanged(Scene from, Scene to)
        {
            if (to.path == GameNavigation.MenuScene && ModState.Active)
            {
                ModState.Active = false;
                Log.LogInfo("Zurück im Hauptmenü: 5-Mio.-Modus beendet.");
            }
        }

        /// <summary>Mod-Lauf direkt starten (Prüfmodus); im Spiel geht der Start über die Tresor-Auswahl im Menü.</summary>
        internal static void Launch()
        {
            ModState.Launching = true;
            if (!GameNavigation.Play(fresh: true))
            {
                ModState.Launching = false;
                Log.LogWarning("Spielstart abgelehnt.");
            }
        }
    }
}
