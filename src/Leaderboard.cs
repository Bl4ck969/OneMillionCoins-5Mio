using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GoldDigger;
using HarmonyLib;
using UnityEngine;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Offline-Bestenliste des 5-Mio.-Modus (Bl4ck969, 26.10.2026: „darf nicht online gehen, lokal angezeigt“). Jeder
    /// abgeschlossene Lauf wird in AppData\Local\OneMillionCoins-5Mio\bestenliste.json eingetragen. Ist im Menü
    /// „5 Millionen“ gewählt, zeigt der Knopf „Bestenlisten“ diese Liste statt der Steam-Liste und fragt Steam nicht.
    /// </summary>
    internal static class Leaderboard
    {
        internal sealed class Entry
        {
            public string run;
            public int vault;
            public string date;
            public double seconds;
            public bool test;
            public string ruleset;
            public int unstuck;
            public int coins;
        }

        /// <summary>
        /// Prüfläufe schreiben in eine eigene Datei, damit die echte Liste frei von Testeinträgen bleibt. Der Prüfmodus „bilder“
        /// zeigt die echte Liste und trägt nichts ein.
        /// </summary>
        private static string FilePath => Path.Combine(OfflinePatches.ModFolder,
            Plugin.CheckMode.Value == "aus" || PictureRun.Requested ? "bestenliste.tsv" : "bestenliste.pruefung.tsv");

        // Eine Zeile je Lauf, Tabulator-getrennt. JsonUtility schreibt Klassen aus dem Mod-Assembly nur als "{}" (26.10.2026).
        private static string Line(Entry e) => string.Join("\t", e.run, e.vault.ToString(CultureInfo.InvariantCulture), e.date,
            e.seconds.ToString("R", CultureInfo.InvariantCulture), e.test ? "1" : "0", e.ruleset ?? "", e.unstuck.ToString(CultureInfo.InvariantCulture),
            e.coins.ToString(CultureInfo.InvariantCulture));

        private static Entry Parse(string line)
        {
            var f = line.Split('\t');
            if (f.Length < 8) return null;
            return new Entry
            {
                run = f[0],
                vault = int.Parse(f[1], CultureInfo.InvariantCulture),
                date = f[2],
                seconds = double.Parse(f[3], CultureInfo.InvariantCulture),
                test = f[4] == "1",
                ruleset = f[5],
                unstuck = int.Parse(f[6], CultureInfo.InvariantCulture),
                coins = int.Parse(f[7], CultureInfo.InvariantCulture),
            };
        }

        private static List<Entry> _shown = new List<Entry>();
        private static Vector2 _scroll;

        internal static void Apply(Harmony harmony)
        {
            harmony.Patch(AccessTools.Method(typeof(MainMenuUI), "OpenLeaderboards"), prefix: new HarmonyMethod(typeof(Leaderboard), nameof(OpenPrefix)));
            harmony.Patch(AccessTools.Method(typeof(MainMenuUI), "DrawLeaderboards"), prefix: new HarmonyMethod(typeof(Leaderboard), nameof(DrawPrefix)));
        }

        internal static List<Entry> Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    return File.ReadAllLines(FilePath).Where(l => l.Length > 0 && l[0] != '#').Select(Parse).Where(e => e != null).ToList();
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Bestenliste nicht lesbar: " + e.Message);
            }
            return new List<Entry>();
        }

        /// <summary>Beim Abschluss eines Mod-Laufs eintragen; ein Lauf (Kennung + Durchgang) steht nur einmal in der Liste.</summary>
        internal static void Record(GameProgressManager progress, SaveManager saves)
        {
            if (!ModState.Active || progress == null || PictureRun.Requested) return;
            try
            {
                string run = RunId(saves);
                var entries = Load();
                if (run != null && entries.Any(e => e.run == run && e.vault == progress.CompletedVaults)) return;
                entries.Add(new Entry
                {
                    run = run ?? Guid.NewGuid().ToString("N"),
                    vault = progress.CompletedVaults,
                    date = DateTime.Now.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
                    seconds = progress.ActivePlayTimeSeconds,
                    test = progress.TestingLoadoutActive,
                    ruleset = progress.VaultRulesetId,
                    unstuck = progress.UnstuckUses,
                    coins = GoalPatches.ModTotal,
                });
                Directory.CreateDirectory(OfflinePatches.ModFolder);
                string temp = FilePath + ".tmp";
                File.WriteAllLines(temp, new[] { "# Lauf\tDurchgang\tDatum\tSekunden\tTest\tFertigkeitsbaum\tBefreit\tMünzen" }.Concat(entries.Select(Line)));
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak");
                else File.Move(temp, FilePath);
                _placeKey = null;
                Plugin.Log.LogInfo($"Bestenliste: Lauf eingetragen ({Time(progress.ActivePlayTimeSeconds)}, Durchgang {progress.CompletedVaults + 1}{(progress.TestingLoadoutActive ? ", Test" : "")}).");
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Bestenliste nicht gespeichert: " + e.Message);
            }
        }

        /// <summary>Wertung wie im Original: erster Durchgang ohne Testmodus; Schnellste zuerst, alles andere danach.</summary>
        internal static List<Entry> Ordered(List<Entry> entries) =>
            entries.OrderBy(e => e.test || e.vault > 0 ? 1 : 0).ThenBy(e => e.seconds).ToList();

        private static string RunId(SaveManager saves) => saves != null ? Traverse.Create(saves).Field<string>("_statisticsRunId").Value : null;

        /// <summary>Platz wie in der Liste (nur gewertete Läufe zählen); 0 = nicht gewertet oder nicht eingetragen.</summary>
        internal static int PlaceOf(List<Entry> entries, string run, int vault)
        {
            var ranked = Ordered(entries).Where(e => !e.test && e.vault == 0).ToList();
            return ranked.FindIndex(e => e.run == run && e.vault == vault) + 1;
        }

        private static string _placeKey;
        private static int _place;

        /// <summary>Platz des laufenden Durchgangs für den Abschlussdialog; liest die Datei nur, wenn sich Lauf oder Liste ändern.</summary>
        internal static int Place(GameProgressManager progress, SaveManager saves)
        {
            string run = RunId(saves);
            if (run == null) return 0;
            string key = run + "/" + progress.CompletedVaults;
            if (key != _placeKey)
            {
                _place = PlaceOf(Load(), run, progress.CompletedVaults);
                _placeKey = key;
            }
            return _place;
        }

        private static bool OpenPrefix(MainMenuUI __instance)
        {
            if (!MenuPatches.ModSelected) return true;
            _shown = Ordered(Load());
            _scroll = Vector2.zero;
            Traverse.Create(__instance).Field("_leaderboardsOpen").SetValue(true);
            return false;
        }

        /// <summary>Statt der Steam-Liste im selben Fenster die lokale Liste; „Zurück“ und Esc schließen wie im Spiel.</summary>
        private static bool DrawPrefix(MainMenuUI __instance, float width, float height)
        {
            if (!MenuPatches.ModSelected) return true;
            var theme = Traverse.Create(__instance).Field<VaultTheme>("_theme").Value;
            var rect = new Rect((width - 900f) * 0.5f, (height - 620f) * 0.5f, 900f, 620f);
            theme.Panel(rect, 0f);
            LocalizedGUI.Label(new Rect(rect.x + 30f, rect.y + 15f, 840f, 46f), ModText.Of("5 million · leaderboard", "5 Millionen · Bestenliste"), theme.Title);
            LocalizedGUI.Label(new Rect(rect.x + 32f, rect.y + 62f, 840f, 24f),
                ModText.Of("Stored on this PC only – nothing goes online.", "Nur auf diesem PC gespeichert – nichts geht online."), theme.Caption);

            float[] columns = { 32f, 110f, 290f, 480f, 680f };
            string[] heads = ModText.German
                ? new[] { "PLATZ", "ZEIT", "DATUM", "FERTIGKEITSBAUM", "HINWEIS" }
                : new[] { "RANK", "TIME", "DATE", "SKILL TREE", "NOTE" };
            for (int i = 0; i < heads.Length; i++)
                LocalizedGUI.Label(new Rect(rect.x + columns[i], rect.y + 100f, 200f, 22f), heads[i], theme.Caption);

            var view = new Rect(rect.x + 20f, rect.y + 128f, rect.width - 40f, 400f);
            if (_shown.Count == 0)
            {
                LocalizedGUI.Label(new Rect(view.x + 12f, view.y + 10f, view.width - 24f, 60f),
                    ModText.Of("No run completed yet. A run counts once the last coin has been taken out of the chest.",
                        "Noch kein Lauf abgeschlossen. Ein Lauf zählt, wenn die letzte Münze aus der Kiste geholt ist."), theme.Body);
            }
            else
            {
                _scroll = GUI.BeginScrollView(view, _scroll, new Rect(0f, 0f, view.width - 20f, _shown.Count * 36f));
                int place = 0;
                for (int i = 0; i < _shown.Count; i++)
                {
                    var e = _shown[i];
                    bool ranked = !e.test && e.vault == 0;
                    if (ranked) place++;
                    float y = i * 36f;
                    var style = ranked ? theme.Body : theme.Small;
                    LocalizedGUI.Label(new Rect(columns[0] - 20f, y, 80f, 32f), ranked ? place + "." : "–", style);
                    LocalizedGUI.Label(new Rect(columns[1] - 20f, y, 180f, 32f), Time(e.seconds), style);
                    LocalizedGUI.Label(new Rect(columns[2] - 20f, y, 190f, 32f), e.date, style);
                    LocalizedGUI.Label(new Rect(columns[3] - 20f, y, 200f, 32f), string.IsNullOrEmpty(e.ruleset) ? "–" : VaultRulesets.Label(e.ruleset), style);
                    var notes = new List<string>();
                    if (e.test) notes.Add("Test");
                    if (e.vault > 0) notes.Add(ModText.Of($"Vault {e.vault + 1}", $"Durchgang {e.vault + 1}"));
                    if (e.unstuck > 0) notes.Add(ModText.Of($"{e.unstuck}× unstuck", $"{e.unstuck}× befreit"));
                    LocalizedGUI.Label(new Rect(columns[4] - 20f, y, 200f, 32f), notes.Count > 0 ? string.Join(", ", notes) : "", style);
                }
                GUI.EndScrollView();
            }
            if (VaultUI.ActionButton(new Rect(rect.xMax - 180f, rect.yMax - 58f, 152f, 36f), "Back  [Esc]", theme.Compact))
                Traverse.Create(__instance).Field("_leaderboardsOpen").SetValue(false);
            return false;
        }

        internal static string Time(double seconds)
        {
            var t = TimeSpan.FromSeconds(Math.Max(0.0, seconds));
            return t.TotalHours >= 1.0 ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 10:00}" : $"{t.Minutes}:{t.Seconds:00}.{t.Milliseconds / 10:00}";
        }
    }
}
