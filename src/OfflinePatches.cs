using System;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Steamworks;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Hält den 5-Mio.-Modus offline: eigener Spielstand nur im Mod-Ordner, keine Lifetime-Coins, keine Bestenlisten,
    /// keine Achievements, keine Steam Cloud. Alle Sperren greifen nur, solange ModState.Active ist.
    /// Fehlt eine Pflicht-Sperre (z. B. nach einem Spiel-Update), bleibt der Modus gesperrt.
    /// </summary>
    internal static class OfflinePatches
    {
        /// <summary>
        /// Mod-Spielstände in AppData\Local – bewusst nicht im Spielordner unter LocalLow, den Steam Cloud vollständig
        /// hochlädt (remotecache.vdf, gemessen 26.10.2026).
        /// </summary>
        internal static readonly string ModFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ReleaseInfo.SaveFolderName);

        /// <summary>Prüfläufe (Testmodus mit allen Fertigkeiten) bekommen wie im Original einen eigenen Spielstand.</summary>
        internal static bool Testing => Plugin.TestLoadout;

        /// <summary>
        /// Der Ladetest „spielstand-laden“ läuft ohne Testmodus (sonst lehnt das Spiel einen echten Spielstand ab:
        /// testingLoadout muss passen) – auf einer eigenen Datei, damit er den echten Spielstand nie berührt (03.11.2026).
        /// </summary>
        internal static string SaveFile => Path.Combine(ModFolder,
            Testing ? "spielstand.test.json" : SaveTest.LoadRequested ? "spielstand.ladetest.json" : "spielstand.json");

        internal static bool HasModSave => File.Exists(SaveFile) || File.Exists(SaveFile + ".bak");

        internal static void Apply(Harmony harmony)
        {
            // Spielstand: Pfad in den Mod-Ordner umlenken. Jeder Datei-Zugriff des SaveManagers prüft vorher, dass er dort
            // landet; sonst wird er abgebrochen – der echte Spielstand bleibt so in jedem Fall unberührt.
            Patch(harmony, "GoldDigger.SaveManager", "Initialize", nameof(SaveInitializePrefix));
            foreach (var method in new[] { "Save", "StartNewGame", "TryLoadFile", "ContinueCompletedRun", "ResumeCompletedRun" })
                Patch(harmony, "GoldDigger.SaveManager", method, nameof(SavePathGuard));
            Patch(harmony, "GoldDigger.SaveManager", "Load", nameof(LoadPathGuard));
            Block(harmony, "GoldDigger.SaveManager", "FlushLifetimeCoins", true);
            Block(harmony, "GoldDigger.SaveManager", "SaveLifetimeCoins", true);
            Block(harmony, "GoldDigger.SaveManager", "PreserveLifetimeCoins", true);
            Block(harmony, "GoldDigger.SaveManager", "EnsureBankingProfile");

            // Achievements, Bestenlisten, Banking.
            Block(harmony, "GoldDigger.GameAchievementTracker", "Evaluate");
            Block(harmony, "GoldDigger.SteamService", "QueueAchievements");
            Block(harmony, "GoldDigger.SteamService", "ReportBankingIssue");
            Block(harmony, "GoldDigger.SteamService", "TrySyncBankingHistory");
            Block(harmony, "GoldDigger.SteamService", "SubmitLifetimeBankedCoins");
            Block(harmony, "GoldDigger.SteamService", "RefreshBankingForMenu");
            Block(harmony, "GoldDigger.SteamService", "FlushBankingForExit");
            Block(harmony, "GoldDigger.SteamService", "SubmitCompletedRun");
            Block(harmony, "GoldDigger.GameSceneController", "SubmitSpeedrun");
            Block(harmony, "GoldDigger.SteamScoreQueue", "Tick");
            Block(harmony, "GoldDigger.SteamCollectorQueue", "Tick");
            Block(harmony, "GoldDigger.SteamBankingCloud", "Write", false);

            // Letzte Sicherung direkt an Steamworks.NET.
            Block(harmony, "Steamworks.SteamUserStats", "UploadLeaderboardScore");
            Block(harmony, "Steamworks.SteamUserStats", "SetAchievement", false);
            Block(harmony, "Steamworks.SteamUserStats", "StoreStats", false);
            Block(harmony, "Steamworks.SteamUserStats", "SetStat", false, required: false);
            Block(harmony, "Steamworks.SteamRemoteStorage", "FileWrite", false);
            Block(harmony, "Steamworks.SteamRemoteStorage", "FileWriteAsync", required: false);
            Block(harmony, "Steamworks.SteamRemoteStorage", "FileDelete", false, required: false);

            // Moduswechsel am Spielstart.
            Patch(harmony, "GoldDigger.GameNavigation", "Play", nameof(PlayPrefix), nameof(PlayPostfix));
        }

        private static void Block(Harmony harmony, string typeName, string method, bool boolResult = false, bool required = true)
        {
            var type = AccessTools.TypeByName(typeName);
            var targets = type == null
                ? Array.Empty<MethodInfo>()
                : AccessTools.GetDeclaredMethods(type).Where(m => m.Name == method).ToArray();
            if (targets.Length == 0)
            {
                Fail($"{typeName}.{method} nicht gefunden", required);
                return;
            }
            foreach (var target in targets)
            {
                string prefix;
                if (target.ReturnType == typeof(void)) prefix = nameof(SkipVoid);
                else if (target.ReturnType == typeof(bool)) prefix = boolResult ? nameof(ReturnTrue) : nameof(ReturnFalse);
                else if (target.ReturnType == typeof(SteamAPICall_t)) prefix = nameof(ReturnInvalidCall);
                else
                {
                    Fail($"{typeName}.{method}: Rückgabetyp {target.ReturnType.Name} nicht vorgesehen", required);
                    continue;
                }
                try
                {
                    harmony.Patch(target, prefix: new HarmonyMethod(typeof(OfflinePatches), prefix));
                }
                catch (Exception e)
                {
                    Fail($"{typeName}.{method}: {e.Message}", required);
                }
            }
        }

        private static void Patch(Harmony harmony, string typeName, string method, string prefix, string postfix = null)
        {
            var target = AccessTools.Method(AccessTools.TypeByName(typeName), method);
            if (target == null)
            {
                Fail($"{typeName}.{method} nicht gefunden", true);
                return;
            }
            harmony.Patch(target,
                prefix: prefix == null ? null : new HarmonyMethod(typeof(OfflinePatches), prefix),
                postfix: postfix == null ? null : new HarmonyMethod(typeof(OfflinePatches), postfix));
        }

        private static void Fail(string message, bool required)
        {
            if (required)
            {
                ModState.OfflineGuardsReady = false;
                Plugin.Log.LogError("Offline-Sperre fehlt: " + message);
            }
            else
            {
                Plugin.Log.LogWarning("Optionale Sperre übersprungen: " + message);
            }
        }

        private static bool SkipVoid() => !ModState.Active;

        private static bool ReturnTrue(ref bool __result)
        {
            if (!ModState.Active) return true;
            __result = true;
            return false;
        }

        private static bool ReturnFalse(ref bool __result)
        {
            if (!ModState.Active) return true;
            __result = false;
            return false;
        }

        private static bool ReturnInvalidCall(ref SteamAPICall_t __result)
        {
            if (!ModState.Active) return true;
            __result = SteamAPICall_t.Invalid;
            return false;
        }

        private static void SaveInitializePrefix(object __instance, ref string savePath)
        {
            if (!ModState.Active) return;
            Directory.CreateDirectory(ModFolder);
            savePath = SaveFile;
            // Das Spiel speichert nur, wenn der Testmodus zum Spielstand passt; der Mod schaltet ihn erst nach dem Laden ein.
            Traverse.Create(__instance).Field("_testingSave").SetValue(Testing);
        }

        private static bool SavePathGuard(object __instance, MethodBase __originalMethod, ref bool __result)
        {
            if (InModFolder(__instance, __originalMethod)) return true;
            __result = false;
            return false;
        }

        private static bool LoadPathGuard(object __instance, MethodBase __originalMethod) => InModFolder(__instance, __originalMethod);

        /// <summary>Außerhalb des Mod-Modus immer frei; im Mod-Modus nur, wenn der Spielstand im Mod-Ordner liegt.</summary>
        private static bool InModFolder(object saveManager, MethodBase method)
        {
            if (!ModState.Active) return true;
            string path = Traverse.Create(saveManager).Field<string>("_savePath").Value;
            string full = path == null ? "" : Path.GetFullPath(path);
            if (full.StartsWith(Path.GetFullPath(ModFolder) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) return true;
            Plugin.Log.LogError($"Spielstand-Zugriff außerhalb des Mod-Ordners abgebrochen ({method.Name}: {path}).");
            return false;
        }

        private static void PlayPrefix()
        {
            ModState.Active = ModState.Launching && ModState.OfflineGuardsReady;
            ModState.Launching = false;
        }

        private static void PlayPostfix(bool __result)
        {
            if (!__result) ModState.Active = false;
            if (ModState.Active) Plugin.Log.LogInfo($"5-Mio.-Modus gestartet (offline, Spielstand {SaveFile}).");
        }
    }
}
