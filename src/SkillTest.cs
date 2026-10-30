using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GoldDigger;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Prüfmodus "fertigkeiten": misst im Mod-Lauf die geänderten Fertigkeiten (Goldfluss, Filialleiter, Bankradius,
    /// deutsche Texte) und holt eine abgestellte Taschenbank mit [4] zurück. Danach über den Pause-Weg ins Hauptmenü und
    /// prüfen, dass die Definitionen wieder die des Originals sind. Läuft wie jeder Prüflauf im Testmodus (alle Fertigkeiten
    /// gelernt, Spielstand spielstand.test.json) und speichert nicht.
    /// </summary>
    internal sealed class SkillTest : MonoBehaviour
    {
        internal static bool Requested => Plugin.CheckMode.Value == "fertigkeiten";

        private static SkillTest _instance;

        internal static void Begin()
        {
            if (_instance != null) return;
            var go = new GameObject("Fertigkeiten-Test");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SkillTest>();
        }

        private string Folder => Path.Combine(Plugin.CheckFolder.Value, "fertigkeiten");

        private IEnumerator Start()
        {
            if (Directory.Exists(Folder)) Directory.Delete(Folder, true);
            Directory.CreateDirectory(Folder);
            yield return new WaitForSecondsRealtime(5f);

            var progress = Traverse.Create(FindAnyObjectByType<GameSceneController>()).Field<GameProgressManager>("progress").Value;
            var tools = FindAnyObjectByType<ToolController>();
            Report("Mod", progress);

            // [4] bei abgestellter Bank: 8 m vor dem Spieler abstellen (weiter als die 3 m für [F]), 2 s sammeln lassen, [4].
            // Die Figur bleibt stehen; am 29.10.2026 hatte ein Versetzen um 10 m sie durch die Außenwand geschoben.
            var player = tools.transform;
            tools.SelectShortcut(4);
            yield return null;
            var held = tools.CurrentTool.id;
            bool placed = progress.PlacePocketBank(player.position + player.forward * 8f);
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("bank_abgestellt.png");
            int before = progress.CollectedCoins;
            float distance = Vector3.Distance(player.position, progress.StationPosition);
            tools.SelectShortcut(4);
            yield return null;
            Plugin.Log.LogInfo($"Fertigkeitentest: Bank abgestellt {placed}, Werkzeug davor {held}, gesammelt {before:N0}; " +
                $"[4] aus {distance:0.0} m -> abgestellt {progress.StationDeployed}, in der Hand {tools.CurrentTool.id}");
            yield return Shot("bank_zurueck.png");
            tools.SelectShortcut(4);
            yield return null;
            Plugin.Log.LogInfo($"Fertigkeitentest: zweites [4] mit Bank in der Hand -> abgestellt {progress.StationDeployed}, in der Hand {tools.CurrentTool.id}");

            yield return MovementChecks(progress);
            StationChecks(progress);

            // Zurück ins Hauptmenü, ohne zu speichern (sonst überschreibt der Test den Test-Spielstand, mit dem Bl4ck969 spielt):
            // die Definitionen müssen danach wieder die des Originals sein.
            var hud = FindAnyObjectByType<GoldDiggerHUD>();
            AccessTools.Method(typeof(GoldDiggerHUD), "ReturnToMenu").Invoke(hud, new object[] { false });
            float until = Time.realtimeSinceStartup + 30f;
            while (SceneManager.GetActiveScene().path != GameNavigation.MenuScene && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSecondsRealtime(2f);
            SkillPatches.Sync();
            Report("Hauptmenü", null);
            Plugin.Log.LogInfo($"Fertigkeitentest Hauptmenü: Ast Bewegung im Katalog {MovementBranch.Injected}, Skill 1700 {SkillCatalog.Get((SkillId)1700) != null}, " +
                $"Baum {LevelCatalog.Vault.skillTree.All.Length} Skills, Spalten {string.Join(",", SkillTreeLayout.For(LevelCatalog.Vault.skillTree).BranchOrder)}, " +
                $"Punkte bei 20 Mio. Münzen {SkillCatalog.PointsAt(20000000L, out _, out _)}");
            Application.Quit();
        }

        /// <summary>
        /// Einzahlautomaten: Gruppe je Etage, Texte (deutsch), und je Freischalt-Skill: Skill kurz aus der gelernten Menge
        /// nehmen und zählen, welche Etagen dann gesperrt sind.
        /// </summary>
        private static void StationChecks(GameProgressManager progress)
        {
            int lowest = Plugin.FloorCount.Value - 1;
            var groups = Enumerable.Range(1, lowest).GroupBy(SkillPatches.StationGroup)
                .Select(g => $"{(int)SkillPatches.StationSkill(g.First())}: {g.First():00}–{g.Last():00}");
            Plugin.Log.LogInfo($"Fertigkeitentest Automaten: Gruppen {string.Join(", ", groups)}; offen mit allen Skills " +
                $"{Enumerable.Range(0, lowest + 1).Count(progress.IsDepositStationUnlocked)}/{lowest + 1}");
            var learned = Traverse.Create(progress).Field<HashSet<SkillId>>("_learnedSkills").Value;
            foreach (int id in new[] { 1211, 1213, 1214, 1215, 1216 })
            {
                var skill = SkillCatalog.Get((SkillId)id);
                learned.Remove((SkillId)id);
                var locked = Enumerable.Range(0, lowest + 1).Where(r => !progress.IsDepositStationUnlocked(r)).ToList();
                learned.Add((SkillId)id);
                Plugin.Log.LogInfo($"Fertigkeitentest Automaten: ohne {id} gesperrt {(locked.Count == 0 ? "keine" : $"{locked.First():00}–{locked.Last():00} ({locked.Count})")}; " +
                    $"\"{skill.Name}\" | de \"{Translate(skill.Name)}\" | \"{skill.Description.Replace("\n", " / ")}\" | de \"{Translate(skill.Description).Replace("\n", " / ")}\"");
            }
        }

        /// <summary>Ast Bewegung: Katalog, Baumkarte, Boni, Jetpack-Steigen, Bilder von Baum, Browser und Leiste unten.</summary>
        private IEnumerator MovementChecks(GameProgressManager progress)
        {
            var tree = progress.SkillTree;
            var map = SkillTreeLayout.For(tree);
            int learned = 0;
            for (int id = 1700; id <= 1712; id++) if (progress.HasSkill((SkillId)id)) learned++;
            Plugin.Log.LogInfo($"Fertigkeitentest Bewegung: im Katalog {MovementBranch.Injected}, Baum {tree.All.Length} Skills, gelernt {learned}/13, " +
                $"Lauftempo x{progress.MovementSpeedMultiplier:0.##}, Sprunghöhe x{progress.JumpHeightMultiplier:0.##}; Spalten {string.Join(",", map.BranchOrder)}, " +
                $"Breite {map.TotalWidth:0}, Lage Wurzel {map.Position((SkillId)1700)}, Jetpack {map.Position((SkillId)1712)}, Rutschen {map.Position((SkillId)1600)}, " +
                $"Route {tree.DevelopmentRoute(7, 1).Length}; Punkte bei 20 Mio. Münzen {SkillCatalog.PointsAt(20000000L, out _, out _)}, " +
                $"voller Baum kostet {tree.TotalPointCost}, Testmodus {progress.TestingLoadoutActive}, Spielstand {OfflinePatches.SaveFile}");

            // Baum mit dem neuen Ast.
            var hud = FindAnyObjectByType<GoldDiggerHUD>();
            hud.OpenUpgradeTree();
            yield return new WaitForSecondsRealtime(1.5f);
            yield return Shot("baum.png");
            AccessTools.Method(typeof(GoldDiggerHUD), "CloseUpgradeTree").Invoke(hud, null);
            yield return new WaitForSecondsRealtime(0.5f);

            // Jetpack: wie nach einem Sprung (4 m/s aufwärts), dann 0,6 s „Sprungtaste gehalten“.
            var player = FindAnyObjectByType<FirstPersonController>();
            var velocity = Traverse.Create(player).Field<float>("_verticalVelocity");
            float startY = player.transform.position.y, peak = 0f;
            velocity.Value = 4f;
            MovementBranch.HoldJumpForTest = true;
            float until = Time.realtimeSinceStartup + 0.6f;
            bool shot = false;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                peak = Mathf.Max(peak, velocity.Value);
                if (!shot && Time.realtimeSinceStartup > until - 0.3f)
                {
                    shot = true;
                    yield return Shot("jetpack_feld.png");
                }
            }
            float risen = player.transform.position.y - startY;
            MovementBranch.HoldJumpForTest = false;
            yield return new WaitForSecondsRealtime(3f);
            var controller = player.GetComponent<CharacterController>();
            Plugin.Log.LogInfo($"Fertigkeitentest Jetpack: Steiggeschwindigkeit bis {peak:0.0} m/s, gestiegen {risen:0.00} m in 0,6 s; " +
                $"3 s nach dem Loslassen am Boden {controller.isGrounded}, Höhe zum Start {player.transform.position.y - startY:0.00} m");
        }

        private static void Report(string where, GameProgressManager progress)
        {
            foreach (int id in new[] { 30602, 30222, 1222 })
            {
                var skill = SkillCatalog.Get((SkillId)id);
                string german = Translate(skill.Description);
                Plugin.Log.LogInfo($"Fertigkeitentest {where}: {id} {skill.Name}: Rutsche {Mathf.Exp(skill.Bonuses.ChuteGrowth) - 1f:P0}, " +
                    $"Taschenbank {Mathf.Exp(skill.Bonuses.PocketBankGrowth) - 1f:P0}, Text \"{skill.Description}\" | de \"{german}\"");
            }
            if (progress == null) return;
            Plugin.Log.LogInfo($"Fertigkeitentest {where}: gelernt Goldfluss {progress.HasSkill((SkillId)30602)}, Filialleiter {progress.HasSkill((SkillId)30222)}; " +
                $"Bankradius {progress.PocketBankRange:0.#} m, Sammelrate {progress.PocketBankBaseCollectionRate}/s, Rutschen {progress.PassiveChuteRate:0.##}/s, " +
                $"Sprache {GameLocalization.Language}");
        }

        private static string Translate(string english)
        {
            foreach (var language in GameLocalization.SupportedLanguages)
            {
                if (language.Id != "de") continue;
                var catalog = Traverse.Create(language).Property<TranslationCatalog>("Catalog").Value;
                return catalog?.Translate(english) ?? "(kein Katalog)";
            }
            return "(kein Deutsch)";
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
