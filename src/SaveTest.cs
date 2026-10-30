using System.Collections;
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

        private static SaveTest _instance;

        internal static void Begin()
        {
            if (_instance != null) return;
            var go = new GameObject("Spielstand-Test");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SaveTest>();
        }

        private string Folder => Path.Combine(Plugin.CheckFolder.Value, "spielstand");

        private IEnumerator Start()
        {
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
