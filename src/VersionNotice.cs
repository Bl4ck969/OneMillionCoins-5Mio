using System.Collections;
using System.IO;
using GoldDigger;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Hinweis im Hauptmenü, solange die Mod auf einem nicht geprüften Spielstand läuft oder aus ist – mit Knopf zum Ein- bzw.
    /// Ausschalten (seit 03.11.2026; vorher blieb sie bis zu einer neuen Mod-Fassung aus). Die Wahl gilt ab dem nächsten Start:
    /// Patches greifen beim Laden des Plugins, und manche Felder des Spiels lassen sich nur vor dessen erstem Lauf tauschen.
    /// Eigenes Objekt (wie die Prüfläufe) und nur Unity-Bedienelemente, damit nichts vom Spielcode des fremden Stands abhängt.
    /// Texte nach der Windows-Sprache. In den Prüfmodi „versionsschutz“ und „versionsschutz-fehler“ prüft er sich selbst.
    /// </summary>
    internal sealed class VersionNotice : MonoBehaviour
    {
        internal enum Kind { Off, Untested, Failed }

        private Kind _kind;
        /// <summary>Freigabe beim Start; die Knöpfe ändern nur die Einstellung, nicht den laufenden Zustand.</summary>
        private bool _releasedAtStart;
        private bool _confirming;
        private GUIStyle _label, _button, _box;

        internal static void Show(Kind kind)
        {
            var go = new GameObject("5-Mio.-Versionshinweis");
            DontDestroyOnLoad(go);
            var notice = go.AddComponent<VersionNotice>();
            notice._kind = kind;
            notice._releasedAtStart = Plugin.Released;
        }

        private static bool German => Application.systemLanguage == SystemLanguage.German;
        private static string T(string german, string english) => German ? german : english;

        private bool ReleasedNow => Plugin.Released;
        private bool Changed => ReleasedNow != _releasedAtStart;

        /// <summary>Das Spiel zählt die Versionsnummer nicht bei jedem Update hoch (03.11.2026: weiter „1.2.2“) – dann sagt der Build, was neu ist.</summary>
        private static string Build => Plugin.CurrentBuildLabel;

        private string Line
        {
            get
            {
                if (Changed)
                    return ReleasedNow
                        ? T($"Mod „5 Mio. Münzen“ für Spielversion {Build} eingeschaltet – wirkt beim nächsten Start des Spiels.",
                            $"Mod \"5 Million Coins\" switched on for game version {Build} – takes effect the next time the game starts.")
                        : T("Mod „5 Mio. Münzen“ ausgeschaltet – wirkt beim nächsten Start des Spiels.",
                            "Mod \"5 Million Coins\" switched off – takes effect the next time the game starts.");
                switch (_kind)
                {
                    case Kind.Off:
                        return T($"Mod „5 Mio. Münzen“ aus: Spielversion {Build} ist nicht geprüft (geprüft: {ReleaseInfo.TestedVersions}). Das Spiel läuft unverändert.",
                            $"Mod \"5 Million Coins\" off: game version {Build} is not tested (tested: {ReleaseInfo.TestedVersions}). The game runs unmodified.");
                    case Kind.Untested:
                        return T($"Mod „5 Mio. Münzen“ läuft auf der nicht geprüften Spielversion {Build} – von dir eingeschaltet, auf eigene Gefahr.",
                            $"Mod \"5 Million Coins\" is running on the untested game version {Build} – switched on by you, at your own risk.");
                    default:
                        return T($"Mod „5 Mio. Münzen“ ließ sich auf Spielversion {Build} nicht laden und ist aus. Das Spiel läuft unverändert.",
                            $"Mod \"5 Million Coins\" could not be loaded on game version {Build} and is off. The game runs unmodified.");
                }
            }
        }

        private static string Warning => T(
            $"Die Mod ist mit Spielversion {Build} nicht geprüft (geprüft: {ReleaseInfo.TestedVersions}). Sie greift an Stellen im Spielcode ein, " +
            "die sich mit einem Update geändert haben können. Möglich sind Fehler im Gebäude, ein Absturz oder ein unbrauchbarer Spielstand im " +
            "5-Mio.-Modus – im schlimmsten Fall auch Fehler im normalen Spiel. Der 5-Mio.-Modus bleibt offline, dein normaler Spielstand " +
            "wird nicht angefasst.\n\nDie Mod wird ab dem nächsten Start geladen. Nach dem nächsten Spiel-Update fragt sie erneut. " +
            "Ausschalten geht hier jederzeit wieder.",
            $"The mod is not tested with game version {Build} (tested: {ReleaseInfo.TestedVersions}). It hooks into places in the game's code " +
            "that an update may have changed. Possible results are glitches in the building, a crash or an unusable save in 5 million " +
            "mode – in the worst case also glitches in the normal game. 5 million mode stays offline, and your normal save is not touched." +
            "\n\nThe mod will load from the next start. After the next game update it asks again. You can switch it off here at any time.");

        private void OnGUI()
        {
            if (SceneManager.GetActiveScene().path != GameNavigation.MenuScene) return;
            GUI.depth = -1000;
            int size = Mathf.Max(14, Screen.height / 60);
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = false };
                _button = new GUIStyle(GUI.skin.button) { fontSize = size, padding = new RectOffset(size / 2 + 4, size / 2 + 4, 3, 3) };
                _box = new GUIStyle(GUI.skin.box) { fontSize = size, wordWrap = true, alignment = TextAnchor.UpperLeft, padding = new RectOffset(size, size, size, size) };
            }
            float y = Screen.height - size * 2.6f, height = size * 1.9f;
            var line = new GUIContent(Line);
            float width = Mathf.Min(_label.CalcSize(line).x, Screen.width - 32f);
            // Schatten, damit der Hinweis auf jedem Hintergrund lesbar ist.
            _label.normal.textColor = Color.black;
            GUI.Label(new Rect(17f, y + 1f, width, height), line, _label);
            _label.normal.textColor = Color.white;
            GUI.Label(new Rect(16f, y, width, height), line, _label);

            float x = 16f + width + size;
            if (Changed)
            {
                if (Button(ref x, y, height, T("Spiel beenden", "Quit game"))) Application.Quit();
                if (Button(ref x, y, height, T("Rückgängig", "Undo"))) SetReleased(_releasedAtStart);
            }
            else if (_kind == Kind.Off && Plugin.CurrentFingerprint != null && !_confirming)
            {
                if (Button(ref x, y, height, T("Trotzdem einschalten …", "Switch on anyway …"))) _confirming = true;
            }
            else if (_kind != Kind.Off && ReleasedNow)
            {
                // Nach einem Fehler ist die Mod schon aus; der Knopf verhindert nur den nächsten Versuch.
                string label = _kind == Kind.Failed ? T("Nicht mehr laden", "Don't load again") : T("Ausschalten", "Switch off");
                if (Button(ref x, y, height, label)) SetReleased(false);
            }
            if (_confirming && !Changed) DrawConfirmation(size);
        }

        private bool Button(ref float x, float y, float height, string text)
        {
            var content = new GUIContent(text);
            float width = _button.CalcSize(content).x;
            bool pressed = GUI.Button(new Rect(x, y - 2f, width, height), content, _button);
            x += width + 8f;
            return pressed;
        }

        private void DrawConfirmation(int size)
        {
            // Über dem Münzhaufen in der Bildmitte: links stehen die Knöpfe des Spiels, rechts seine Steam-Karte (03.11.2026: die
            // erste Fassung lag durchscheinend über „Optionen“ und „Spiel beenden“). Deckender Hintergrund, eigener Stil.
            if (_panel == null)
            {
                var background = new Texture2D(1, 1) { hideFlags = HideFlags.DontSave };
                background.SetPixel(0, 0, new Color(0.08f, 0.09f, 0.10f, 0.97f));
                background.Apply();
                _panel = new GUIStyle(_box) { normal = { background = background, textColor = Color.white } };
            }
            float left = Mathf.Max(16f, Screen.width * 0.33f);
            float width = Mathf.Min(size * 44f, Screen.width - left - 16f);
            if (width < size * 20f) { left = 16f; width = Screen.width - 32f; }
            var text = new GUIContent(Warning);
            float textHeight = _panel.CalcHeight(text, width);
            float buttons = size * 2.4f;
            var panel = new Rect(left, Screen.height - size * 3.6f - textHeight - buttons, width, textHeight + buttons);
            GUI.Box(panel, text, _panel);
            float x = panel.x + size, y = panel.yMax - buttons + size * 0.1f;
            if (Button(ref x, y, size * 1.9f, T("Einschalten", "Switch on"))) { SetReleased(true); _confirming = false; }
            if (Button(ref x, y, size * 1.9f, T("Abbrechen", "Cancel"))) _confirming = false;
            // Klicks auf das Feld nicht an das Menü des Spiels darunter weiterreichen.
            var e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseUp) && panel.Contains(e.mousePosition)) e.Use();
        }

        private GUIStyle _panel;

        /// <summary>Freigabe setzen oder löschen; BepInEx schreibt die Einstellung sofort in die Datei.</summary>
        private static void SetReleased(bool on)
        {
            Plugin.ReleasedBuild.Value = on ? Plugin.CurrentFingerprint : "";
            if (!on && Plugin.CurrentFingerprint != Plugin.SimulatedFingerprint) Plugin.AllowUntested.Value = false;
            Plugin.Log.LogInfo(on
                ? $"Spielversion {Build} vom Spieler freigegeben (FreigegebeneVersion = {Plugin.CurrentFingerprint}), wirkt ab dem nächsten Start."
                : $"Freigabe für Spielversion {Build} zurückgenommen, wirkt ab dem nächsten Start.");
        }

        // ---------------------------------------------------------------- Prüfmodus

        /// <summary>
        /// Drei Läufe nacheinander (werkzeuge\pruefung.ps1): 1. „versionsschutz“ ohne Freigabe → Mod aus, 0 Patches, einschalten;
        /// 2. „versionsschutz“ mit der Freigabe aus Lauf 1 → Mod läuft; 3. „versionsschutz-fehler“ → Fehler beim Patchen, alles
        /// zurückgenommen, ausschalten. Jeder Lauf schließt aus dem Zustand, den er vorfindet, welcher er ist.
        /// </summary>
        private IEnumerator Start()
        {
            string mode = Plugin.CheckMode.Value;
            if (mode != "versionsschutz" && mode != "versionsschutz-fehler") yield break;
            string step = _kind == Kind.Off ? "1-aus" : _kind == Kind.Untested ? "2-ungeprueft" : "3-fehler";
            string folder = Path.Combine(Plugin.CheckFolder.Value, "versionsschutz");
            // Nur die Dateien dieses Schritts leeren: den Ordner selbst hält Windows fest, solange ein anderer Prozess darin steht (30.10.2026).
            Directory.CreateDirectory(folder);
            foreach (string file in Directory.GetFiles(folder, step + "*")) File.Delete(file);
            float until = Time.realtimeSinceStartup + 60f;
            while (SceneManager.GetActiveScene().path != GameNavigation.MenuScene && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSecondsRealtime(5f);
            yield return Snap(folder, step + "_hauptmenue.png");

            // Harmony zählt alle Patches im Prozess, auch die von BepInEx selbst – deshalb je Methode mit Besitzer.
            // Zurückgenommene Methoden führt Harmony weiter, mit leerer Besitzerliste – die zählen als „ohne Patch“.
            var patched = System.Linq.Enumerable.ToList(Harmony.GetAllPatchedMethods());
            int ours = patched.FindAll(m => System.Linq.Enumerable.Contains(Harmony.GetPatchInfo(m).Owners, Plugin.Guid)).Count;
            int unpatched = patched.FindAll(m => Harmony.GetPatchInfo(m).Owners.Count == 0).Count;
            var others = patched.FindAll(m => Harmony.GetPatchInfo(m).Owners.Count > 0 && !System.Linq.Enumerable.Contains(Harmony.GetPatchInfo(m).Owners, Plugin.Guid))
                .ConvertAll(m => $"{m.DeclaringType?.Name}.{m.Name} ({string.Join("/", Harmony.GetPatchInfo(m).Owners)})");
            string result = $"Versionsschutz-Test {step}: Hauptmenü {(SceneManager.GetActiveScene().path == GameNavigation.MenuScene ? "erreicht" : "NICHT erreicht")}, " +
                $"Zustand {_kind}, Freigabe beim Start {_releasedAtStart}, Patches der Mod {ours}, zurückgenommen (ohne Patch) {unpatched}, " +
                $"fremde Patches {others.Count}: {string.Join(", ", others.ToArray())}; Hinweis: {Line}";

            if (_kind == Kind.Off)
            {
                _confirming = true;
                yield return Snap(folder, step + "_bestaetigen.png");
                SetReleased(true);
                _confirming = false;
                yield return Snap(folder, step + "_eingeschaltet.png");
                result += $"; eingeschaltet, in der Datei: {SavedValue()}";
            }
            else if (_kind == Kind.Failed)
            {
                SetReleased(false);
                yield return Snap(folder, step + "_ausgeschaltet.png");
                result += $"; ausgeschaltet, in der Datei: „{SavedValue()}“";
            }
            Plugin.Log.LogInfo(result);
            File.WriteAllText(Path.Combine(folder, step + "_ergebnis.txt"), result);
            // BepInEx schreibt sein Log gepuffert: Ohne Pause fehlten am 03.11.2026 die letzten Zeilen eines Laufs.
            yield return new WaitForSecondsRealtime(2f);
            Application.Quit();
        }

        /// <summary>Wert von FreigegebeneVersion, wie er in der Einstellungsdatei steht – gemessen an der Datei, nicht am Speicher.</summary>
        private static string SavedValue()
        {
            foreach (string line in File.ReadAllLines(Plugin.ConfigPath))
                if (line.StartsWith("FreigegebeneVersion =")) return line.Substring("FreigegebeneVersion =".Length).Trim();
            return "(fehlt)";
        }

        private static IEnumerator Snap(string folder, string name)
        {
            yield return null;
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(folder, name), tex.EncodeToPNG());
            Destroy(tex);
        }
    }
}
