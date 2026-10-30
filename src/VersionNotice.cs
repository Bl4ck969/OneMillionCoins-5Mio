using System.Collections;
using System.IO;
using GoldDigger;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Hinweis im Hauptmenü, solange die Mod wegen einer nicht geprüften Spielversion nichts patcht. Eigenes Objekt (wie die
    /// Prüfläufe), damit er nicht davon abhängt, dass das Spiel das BepInEx-Objekt leben lässt. Im Prüfmodus
    /// „versionsschutz“ fotografiert er das Hauptmenü, zählt die Patches und beendet das Spiel.
    /// </summary>
    internal sealed class VersionNotice : MonoBehaviour
    {
        private string _version;
        private GUIStyle _style;

        internal static void Show(string version)
        {
            var go = new GameObject("5-Mio.-Versionshinweis");
            DontDestroyOnLoad(go);
            go.AddComponent<VersionNotice>()._version = version;
        }

        private string Text => Application.systemLanguage == SystemLanguage.German
            ? $"Mod „5 Mio. Münzen“ inaktiv: Spielversion {_version} ist nicht geprüft (geprüft: {ReleaseInfo.TestedVersions}). Das Spiel läuft unverändert."
            : $"Mod \"5 Million Coins\" inactive: game version {_version} is not tested (tested: {ReleaseInfo.TestedVersions}). The game runs unmodified.";

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().path != GameNavigation.MenuScene) return;
            GUI.depth = -1000;
            if (_style == null) _style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(14, Screen.height / 60), wordWrap = false };
            var area = new Rect(16f, Screen.height - _style.fontSize * 2.6f, Screen.width - 300f, _style.fontSize * 2f);
            // Schatten, damit der Hinweis auf jedem Hintergrund lesbar ist.
            _style.normal.textColor = Color.black;
            GUI.Label(new Rect(area.x + 1f, area.y + 1f, area.width, area.height), Text, _style);
            _style.normal.textColor = Color.white;
            GUI.Label(area, Text, _style);
        }

        private IEnumerator Start()
        {
            if (Plugin.CheckMode.Value != "versionsschutz") yield break;
            string folder = Path.Combine(Plugin.CheckFolder.Value, "versionsschutz");
            // Nur die Dateien leeren: den Ordner selbst hält Windows fest, solange ein anderer Prozess darin steht (30.10.2026).
            Directory.CreateDirectory(folder);
            foreach (string file in Directory.GetFiles(folder)) File.Delete(file);
            float until = Time.realtimeSinceStartup + 60f;
            while (SceneManager.GetActiveScene().path != GameNavigation.MenuScene && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSecondsRealtime(5f);
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(folder, "hauptmenue.png"), tex.EncodeToPNG());
            Destroy(tex);
            // Harmony zählt alle Patches im Prozess, auch die von BepInEx selbst – deshalb je Methode mit Besitzer.
            var patched = System.Linq.Enumerable.ToList(Harmony.GetAllPatchedMethods());
            var owners = patched.ConvertAll(m => $"{m.DeclaringType?.Name}.{m.Name} ({string.Join("/", Harmony.GetPatchInfo(m).Owners)})");
            int ours = patched.FindAll(m => System.Linq.Enumerable.Contains(Harmony.GetPatchInfo(m).Owners, Plugin.Guid)).Count;
            Plugin.Log.LogInfo($"Versionsschutz-Test: Hauptmenü {(SceneManager.GetActiveScene().path == GameNavigation.MenuScene ? "erreicht" : "NICHT erreicht")}, " +
                $"Patches der Mod {ours}, alle Patches {patched.Count}: {string.Join(", ", owners)}; Hinweis: {Text}");
            Application.Quit();
        }
    }
}
