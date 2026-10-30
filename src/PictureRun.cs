using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Linq;
using GoldDigger;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Prüfmodus "bilder": Aufnahmen für README und Release (Bl4ck969, 30.10.2026: „Wir brauchen noch Screenshots“).
    /// Läuft wie jeder Prüflauf auf dem Test-Spielstand und speichert nicht. Für die Aufnahmen ist der Testmodus des Spiels
    /// aus (keine Testknöpfe im Baum, kein „Testlauf“), der Baum voll ausgebaut wie am Ende eines Laufs und der Haufen bis
    /// Etage 12 abgegraben. Abschlussdialog und Bestenliste zeigen den besten echten Lauf aus bestenliste.tsv (nur gelesen);
    /// die Steam-Karte im Hauptmenü bleibt weg. Menü und Bestenliste werden vor dem Lauf aufgenommen: Nach der Rückkehr
    /// ohne Speichern stand oben links über 30 s „Bankverlauf wird geladen …“ (30.10.2026), im normalen Spiel nicht.
    /// </summary>
    internal sealed class PictureRun : MonoBehaviour
    {
        internal static bool Requested => Plugin.CheckMode.Value == "bilder";

        private const int DugFloor = 12;

        private static PictureRun _instance;

        internal static void Begin()
        {
            if (_instance != null) return;
            var go = new GameObject("Bilder");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PictureRun>();
        }

        private string Folder => Path.Combine(Plugin.CheckFolder.Value, "bilder");

        private IEnumerator Start()
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            Directory.CreateDirectory(Folder);

            // Hauptmenü, sobald das Spiel den Bankverlauf oben links geladen hat, dann die Bestenliste.
            float since = Time.realtimeSinceStartup, until = since + 30f;
            while (SteamService.Instance != null && SteamService.Instance.CheckingBankingHistory && Time.realtimeSinceStartup < until) yield return null;
            Plugin.Log.LogInfo($"Bilder: Bankverlauf im Menü {(SteamService.Instance != null && SteamService.Instance.CheckingBankingHistory ? "nach 30 s noch nicht geladen" : $"nach {Time.realtimeSinceStartup - since:0.0} s geladen")}.");
            yield return new WaitForSecondsRealtime(2f);
            Plugin.Vault.Value = "5Mio";
            yield return Shot("menue.png");
            var menu = FindAnyObjectByType<MainMenuUI>();
            menu.OpenLeaderboards(null);
            yield return Shot("bestenliste.png");
            Traverse.Create(menu).Field("_leaderboardsOpen").SetValue(false);
            yield return null;

            Plugin.Launch();
            until = Time.realtimeSinceStartup + 60f;
            while ((!ModState.Active || FindAnyObjectByType<GameSceneController>() == null) && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSecondsRealtime(5f);

            var scene = Traverse.Create(FindAnyObjectByType<GameSceneController>());
            var progress = scene.Field<GameProgressManager>("progress").Value;
            var saves = scene.Field<SaveManager>("saveManager").Value;
            var hud = FindAnyObjectByType<GoldDiggerHUD>();
            var player = FindAnyObjectByType<FirstPersonController>();
            var pile = FindAnyObjectByType<GranularCoinPile>();
            var elevator = FindAnyObjectByType<CargoElevator>();

            // Ende eines Laufs: voller Baum, alle Punkte ausgegeben, kein Testmodus. Ohne Testmodus passt der Lauf nicht mehr
            // zum Test-Spielstand – das Spiel speichert ihn dann gar nicht. Der Test-Spielstand steht im 2. Durchgang; die
            // Aufnahmen zeigen den ersten. Die verdienten Punkte rechnet das Spiel bei jeder Münze neu, deshalb wird die
            // Ausgabe erst direkt vor der Aufnahme angeglichen, und der Mauszeiger geht aus dem Baum (sonst ein Tooltip).
            var run = Traverse.Create(progress);
            run.Property("TestingLoadoutActive").SetValue(false);
            run.Property("CompletedVaults").SetValue(0);
            hud.OpenUpgradeTree();
            SetCursorPos(8, 8);
            yield return new WaitForSecondsRealtime(1.5f);
            run.Property("SpentSkillPoints").SetValue(progress.EarnedSkillPoints);
            yield return Shot("fertigkeiten.png");
            AccessTools.Method(typeof(GoldDiggerHUD), "CloseUpgradeTree").Invoke(hud, null);

            // Bis Etage 12 abgraben, Etage für Etage wie im Aufzugstest.
            var floors = Enumerable.Range(0, elevator.LayerCount).OrderByDescending(i => elevator.FloorPosition(i).y).Select(i => elevator.FloorPosition(i).y).ToArray();
            int start = pile.Model.Remaining;
            for (int floor = 0; floor <= DugFloor; floor++) Diagnostics.DigTo(pile, floors[floor]);
            Plugin.Log.LogInfo($"Bilder: bis Etage {DugFloor} abgegraben, {start - pile.Model.Remaining:N0} Münzen entfernt, Rest {pile.Model.Remaining:N0}.");
            yield return new WaitForSecondsRealtime(2f);

            // Vom Geländer der obersten Etage in den Schacht schauen, mit den Händen statt des Magneten im Bild.
            var center = pile.transform.position;
            var controller = player.GetComponent<CharacterController>();
            var pitch = Traverse.Create(player).Field<float>("_pitch");
            FindAnyObjectByType<ToolController>().SelectShortcut(1);
            var toCenter = new Vector3(center.x - player.transform.position.x, 0f, center.z - player.transform.position.z).normalized;
            player.transform.rotation = Quaternion.LookRotation(toCenter);
            if (Physics.Raycast(player.transform.position + Vector3.up, toCenter, out var railing, 20f, ~0, QueryTriggerInteraction.Ignore))
                Place(player, controller, new Vector3(railing.point.x, player.transform.position.y, railing.point.z) - toCenter * 0.7f);
            Plugin.Log.LogInfo($"Bilder: Geländer {(railing.collider != null ? $"{railing.collider.name} in {railing.distance:0.0} m" : "nicht gefunden")}.");
            foreach (int angle in new[] { 35, 50, 65 })
            {
                pitch.Value = angle;
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot($"schacht_{angle}.png");
            }

            Traverse.Create(hud).Field("_elevatorOpen").SetValue(true);
            yield return Shot("aufzug.png");
            Traverse.Create(hud).Field("_elevatorOpen").SetValue(false);

            // Abschlussdialog mit dem besten gewerteten Lauf von diesem PC (Zeit und Kennung; Platz kommt aus der Liste).
            var best = Leaderboard.Ordered(Leaderboard.Load()).FirstOrDefault(e => !e.test && e.vault == 0);
            if (best != null) Traverse.Create(saves).Field("_statisticsRunId").SetValue(best.run);
            // Uhr anhalten, sonst läuft die Zeit bei den langsamen Bildern des großen Haufens bis zur Aufnahme weiter.
            hud.ShowCompletionResults();
            Time.timeScale = 0f;
            if (best != null) run.Property("ExactPlayTimeSeconds").SetValue(best.seconds + progress.UnstuckPenaltySeconds);
            yield return Shot("abschluss.png");
            Time.timeScale = 1f;
            Plugin.Log.LogInfo($"Bilder: Abschluss {(best != null ? Leaderboard.Time(best.seconds) : "ohne Lauf in der Bestenliste")}, " +
                $"\"{CompletionPatches.CoinsLine(CompletionPatches.CoinsOriginal)}\" / \"{CompletionPatches.Status(null, hud)}\"");
            Traverse.Create(hud).Field("_completionOpen").SetValue(false);
            yield return new WaitForSecondsRealtime(0.5f);

            // Mit dem Jetpack über dem Schacht, dann auf dem abgegrabenen Haufen: geradeaus und nach oben.
            MovementBranch.HoldJumpForTest = true;
            foreach (var (floor, angle) in new[] { (2, 70f), (7, 55f) })
            {
                Place(player, controller, new Vector3(center.x, floors[floor] + 1f, center.z));
                pitch.Value = angle;
                yield return new WaitForSecondsRealtime(0.4f);
                yield return Shot($"flug_{floor:00}.png");
            }
            MovementBranch.HoldJumpForTest = false;
            Place(player, controller, new Vector3(center.x, floors[DugFloor] + 1.5f, center.z));
            yield return new WaitForSecondsRealtime(1.5f);
            foreach (int angle in new[] { 10, -60, -80 })
            {
                pitch.Value = angle;
                yield return new WaitForSecondsRealtime(0.3f);
                yield return Shot($"haufen_{(angle < 0 ? "hoch" : "")}{System.Math.Abs(angle)}.png");
            }

            // Ohne Speichern ins Hauptmenü, dann Ende.
            AccessTools.Method(typeof(GoldDiggerHUD), "ReturnToMenu").Invoke(hud, new object[] { false });
            until = Time.realtimeSinceStartup + 30f;
            while (SceneManager.GetActiveScene().path != GameNavigation.MenuScene && Time.realtimeSinceStartup < until) yield return null;
            Plugin.Log.LogInfo($"Bilder: fertig, {Directory.GetFiles(Folder).Length} Aufnahmen in {Folder}.");
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        private static void Place(FirstPersonController player, CharacterController controller, Vector3 position)
        {
            controller.enabled = false;
            player.transform.position = position;
            controller.enabled = true;
            Traverse.Create(player).Field("_verticalVelocity").SetValue(0f);
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
