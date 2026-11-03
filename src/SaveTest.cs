using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using GoldDigger;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Prüfmodus "spielstand": Mod-Lauf starten, speichern (Zeit, Größe), über den Pause-Weg des Spiels (ReturnToMenu)
    /// ins Hauptmenü, Menü mit beiden Tresor-Einstellungen aufnehmen, „Weiterspielen“ und prüfen, dass der Spielstand
    /// geladen wurde. Danach Spielende. Nutzt wie jeder Prüflauf den Test-Spielstand (spielstand.test.json).
    /// </summary>
    internal sealed class SaveTest : MonoBehaviour
    {
        internal static bool Requested => Plugin.CheckMode.Value == "spielstand";

        /// <summary>
        /// Prüfmodus „spielstand-laden“ (seit 03.11.2026): den vorhandenen Test-Spielstand über „Weiter“ laden – etwa eine Kopie
        /// eines Spielstands, den eine frühere Fassung der Mod geschrieben hat –, festhalten, was angekommen ist, und ohne
        /// Speichern zurück. Danach die echte Bestenliste öffnen (nur lesen). Der Prüflauf „spielstand“ lädt nur, was er selbst
        /// eben gespeichert hat; ob ältere Spielstände laden, sah er nie.
        /// </summary>
        internal static bool LoadRequested => Plugin.CheckMode.Value == "spielstand-laden";

        private static SaveTest _instance;
        private bool _loadOnly;

        internal static void Begin()
        {
            if (_instance != null) return;
            var go = new GameObject("Spielstand-Test");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SaveTest>();
        }

        /// <summary>Aus dem Hauptmenü: Der Ladetest startet den Lauf selbst über „Weiter“.</summary>
        internal static void BeginLoad()
        {
            if (_instance != null) return;
            var go = new GameObject("Spielstand-Ladetest");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SaveTest>();
            _instance._loadOnly = true;
        }

        private string Folder => Path.Combine(Plugin.CheckFolder.Value, _loadOnly ? "spielstand-laden" : "spielstand");

        private IEnumerator LoadExisting()
        {
            Directory.CreateDirectory(Folder);
            foreach (string old in Directory.GetFiles(Folder)) File.Delete(old);
            yield return new WaitForSecondsRealtime(3f);
            Plugin.Vault.Value = "5Mio";
            string file = OfflinePatches.SaveFile;
            string before = ReleaseInfo.Sha256(file);
            var lines = new List<string>
            {
                $"Ladetest: Datei {file}, {(File.Exists(file) ? new FileInfo(file).Length / 1024.0 / 1024.0 : -1):0.0} MB, " +
                $"Menü zeigt Weiterspielen {SaveManager.HasSavedGame}, Zusatz \"{SaveManager.SavedRulesetCaption}\"",
            };
            yield return Shot("menue.png");

            var menu = FindAnyObjectByType<MainMenuUI>();
            AccessTools.Method(typeof(MainMenuUI), "Play").Invoke(menu, new object[] { false });
            float until = Time.realtimeSinceStartup + 60f;
            while (SceneManager.GetActiveScene().path == GameNavigation.MenuScene && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSecondsRealtime(8f);
            var saves = FindAnyObjectByType<SaveManager>();
            var pile = FindAnyObjectByType<GranularCoinPile>();
            var progress = Traverse.Create(FindAnyObjectByType<GameSceneController>()).Field<GameProgressManager>("progress").Value;
            int learned = progress != null ? Traverse.Create(progress).Field<HashSet<SkillId>>("_learnedSkills").Value.Count : -1;
            var player = FindAnyObjectByType<FirstPersonController>();
            bool canSave = saves != null && Traverse.Create(saves).Field<bool>("_canSave").Value;
            lines.Add($"Ladetest: geladen, Mod aktiv {ModState.Active}, Haufen {(pile != null ? pile.Model.Remaining : -1):N0}, " +
                $"Durchgänge {progress?.CompletedVaults}, Münzen {progress?.CollectedCoins:N0}, Geld {progress?.Money:N0}, Fertigkeiten {learned}, " +
                $"Spielzeit {progress?.ActivePlayTimeSeconds:0.0} s, Spieler {(player != null ? player.transform.position.ToString("F1") : "fehlt")}, " +
                $"speicherbar {canSave}, Meldung \"{saves?.StatusMessage}\"");
            yield return Shot("geladen.png");

            var hud = FindAnyObjectByType<GoldDiggerHUD>();
            AccessTools.Method(typeof(GoldDiggerHUD), "ReturnToMenu").Invoke(hud, new object[] { false });
            until = Time.realtimeSinceStartup + 30f;
            while (SceneManager.GetActiveScene().path != GameNavigation.MenuScene && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSecondsRealtime(3f);
            lines.Add($"Ladetest: zurück ohne Speichern, Spielstand-Datei unverändert {ReleaseInfo.Sha256(file) == before}");
            FindAnyObjectByType<MainMenuUI>().OpenLeaderboards(null);
            yield return Shot("bestenliste.png");
            var entries = Leaderboard.Load();
            lines.Add($"Ladetest: echte Bestenliste geöffnet (nur gelesen), {entries.Count} Einträge: " +
                string.Join(" | ", entries.Select(e => $"{e.date} {e.seconds:0.0} s Durchgang {e.vault + 1}{(e.test ? " Test" : "")}").ToArray()));
            foreach (string line in lines) Plugin.Log.LogInfo(line);
            File.WriteAllLines(Path.Combine(Folder, "ergebnis.txt"), lines);
            // BepInEx schreibt sein Log gepuffert (03.11.2026).
            yield return new WaitForSecondsRealtime(2f);
            Application.Quit();
        }

        private IEnumerator Start()
        {
            if (_loadOnly)
            {
                yield return LoadExisting();
                yield break;
            }
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            Directory.CreateDirectory(Folder);
            yield return new WaitForSecondsRealtime(5f);

            // 1. Im Mod-Lauf speichern.
            var saves = FindAnyObjectByType<SaveManager>();
            var pile = FindAnyObjectByType<GranularCoinPile>();
            int remainingBefore = pile != null ? pile.Model.Remaining : -1;
            var watch = Stopwatch.StartNew();
            bool saved = saves != null && saves.Save();
            watch.Stop();
            string file = OfflinePatches.SaveFile;
            long size = File.Exists(file) ? new FileInfo(file).Length : -1;
            Plugin.Log.LogInfo($"Spielstandtest: gespeichert {saved} in {watch.ElapsedMilliseconds} ms, Datei {file}, {size / 1024.0 / 1024.0:0.0} MB, " +
                $"Haufen {remainingBefore:N0}, Meldung \"{saves?.StatusMessage}\"");

            // 2. Über den Pause-Weg des Spiels ins Hauptmenü.
            var hud = FindAnyObjectByType<GoldDiggerHUD>();
            watch.Restart();
            AccessTools.Method(typeof(GoldDiggerHUD), "ReturnToMenu").Invoke(hud, new object[] { true });
            float until = Time.realtimeSinceStartup + 30f;
            while (SceneManager.GetActiveScene().path != GameNavigation.MenuScene && Time.realtimeSinceStartup < until) yield return null;
            Plugin.Log.LogInfo($"Spielstandtest: Hauptmenü {(SceneManager.GetActiveScene().path == GameNavigation.MenuScene ? "erreicht" : "NICHT erreicht")} " +
                $"nach {watch.ElapsedMilliseconds} ms, Mod aktiv {ModState.Active}");
            yield return new WaitForSecondsRealtime(3f);

            // 3. Menü in beiden Einstellungen aufnehmen.
            Plugin.Vault.Value = "Original";
            yield return Shot("menue_original.png");
            Plugin.Vault.Value = "5Mio";
            yield return Shot("menue_5mio.png");
            ModText.ForceEnglish = true;
            yield return Shot("menue_5mio_en.png");
            ModText.ForceEnglish = false;
            Plugin.Log.LogInfo($"Spielstandtest: Menü zeigt Weiterspielen {SaveManager.HasSavedGame}, Zusatz \"{SaveManager.SavedRulesetCaption}\"");

            // 4. Weiterspielen.
            var menu = FindAnyObjectByType<MainMenuUI>();
            AccessTools.Method(typeof(MainMenuUI), "Play").Invoke(menu, new object[] { false });
            until = Time.realtimeSinceStartup + 60f;
            while (SceneManager.GetActiveScene().path == GameNavigation.MenuScene && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSecondsRealtime(5f);
            saves = FindAnyObjectByType<SaveManager>();
            pile = FindAnyObjectByType<GranularCoinPile>();
            bool canSave = saves != null && Traverse.Create(saves).Field<bool>("_canSave").Value;
            Plugin.Log.LogInfo($"Spielstandtest: weitergespielt, Mod aktiv {ModState.Active}, Haufen {(pile != null ? pile.Model.Remaining : -1):N0} " +
                $"(vorher {remainingBefore:N0}), speicherbar {canSave}, Meldung \"{saves?.StatusMessage}\"");
            yield return Shot("weitergespielt.png");

            // 5. Bestenliste: Eintrag erzeugen (Prüfdatei), ins Menü, Liste öffnen und aufnehmen.
            var progress = Traverse.Create(FindAnyObjectByType<GameSceneController>()).Field<GameProgressManager>("progress").Value;
            Leaderboard.Record(progress, saves);
            hud = FindAnyObjectByType<GoldDiggerHUD>();

            // 6. Abschlussdialog im Mod-Modus: Münzzahl des Haufens, kein roter Kasten, Status der Offline-Liste.
            // Der Test-Spielstand ist im Freispiel; für die Aufnahme kurz erster Durchgang, damit der Status der Offline-Liste erscheint.
            var vaults = Traverse.Create(progress).Property("CompletedVaults");
            int completed = vaults.GetValue<int>();
            vaults.SetValue(0);
            hud.ShowCompletionResults();
            yield return Shot("abschluss.png");
            ModText.ForceEnglish = true;
            yield return Shot("abschluss_en.png");
            ModText.ForceEnglish = false;
            vaults.SetValue(completed);
            yield return Shot("abschluss_freispiel.png");
            Plugin.Log.LogInfo($"Spielstandtest: Abschluss \"{CompletionPatches.CoinsLine(CompletionPatches.CoinsOriginal)}\" / \"{CompletionPatches.Status(null, hud)}\", " +
                $"roter Kasten {(CompletionPatches.Reason("Grund") == null ? "aus" : "AN")}");
            var sample = new List<Leaderboard.Entry>
            {
                new Leaderboard.Entry { run = "t", seconds = 50, test = true },
                new Leaderboard.Entry { run = "a", seconds = 200 },
                new Leaderboard.Entry { run = "b", seconds = 100 },
                new Leaderboard.Entry { run = "b", vault = 1, seconds = 10 },
            };
            Plugin.Log.LogInfo($"Spielstandtest: Platz b {Leaderboard.PlaceOf(sample, "b", 0)} (Soll 1), a {Leaderboard.PlaceOf(sample, "a", 0)} (Soll 2), " +
                $"Testlauf {Leaderboard.PlaceOf(sample, "t", 0)} (Soll 0), Durchgang 2 {Leaderboard.PlaceOf(sample, "b", 1)} (Soll 0)");
            Traverse.Create(hud).Field("_completionOpen").SetValue(false);

            AccessTools.Method(typeof(GoldDiggerHUD), "ReturnToMenu").Invoke(hud, new object[] { true });
            until = Time.realtimeSinceStartup + 30f;
            while (SceneManager.GetActiveScene().path != GameNavigation.MenuScene && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSecondsRealtime(3f);
            menu = FindAnyObjectByType<MainMenuUI>();
            menu.OpenLeaderboards(null);
            yield return Shot("bestenliste.png");
            ModText.ForceEnglish = true;
            yield return Shot("bestenliste_en.png");
            ModText.ForceEnglish = false;
            Plugin.Log.LogInfo($"Spielstandtest: Bestenliste geöffnet, {Leaderboard.Load().Count} Einträge in der Prüfdatei.");
            Application.Quit();
        }

        private IEnumerator Shot(string name)
        {
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Folder, name), tex.EncodeToPNG());
            Destroy(tex);
        }
    }
}
