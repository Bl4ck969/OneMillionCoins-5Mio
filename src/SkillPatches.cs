using System;
using System.Collections.Generic;
using System.Linq;
using GoldDigger;
using HarmonyLib;
using UnityEngine;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Fertigkeiten im 5-Mio.-Modus (Bl4ck969, 29.10.2026): Goldfluss und Filialleiter +300 % statt +100 %, Bankradius
    /// mit Filialleiter 15 statt 12 m, und [4] holt die abgestellte Taschenbank von überall in die Hand zurück.
    /// Seit 30.10.2026 schalten die fünf „Unterer Trichter“-Skills die Einzahlautomaten in wachsenden Gruppen frei
    /// (01–02, 03–05, 06–09, 10–15, 16–24; [Etagen] Automatengruppen); vorher waren im Mod alle Automaten von Anfang an offen.
    /// Im Original bleibt alles wie im Spiel – die Werte stehen in den gemeinsamen Skill-Definitionen und werden beim
    /// Wechsel des Modus gesetzt bzw. zurückgesetzt.
    /// </summary>
    internal static class SkillPatches
    {
        private const int GoldRiver = 30602, BranchManager = 30222, PocketBank = 1222;
        private const float BranchManagerRange = 15f;

        /// <summary>Die Skills, die im Spiel die Automaten auf Etage 01–05 freischalten (GameProgressManager.IsDepositStationUnlocked).</summary>
        private static readonly int[] StationSkills = { 1211, 1213, 1214, 1215, 1216 };

        private static readonly Dictionary<int, (SkillBonuses Bonuses, string Description, string Name)> Originals =
            new Dictionary<int, (SkillBonuses, string, string)>();

        /// <summary>Texte der Freischalt-Skills: alt (Spiel) und neu (Mod), dazu die Etagen der Gruppe.</summary>
        private static (int Id, string Floor, string Range, string OldName, string NewName, string OldText, string NewText)[] _stations =
            Array.Empty<(int, string, string, string, string, string, string)>();

        private static System.Reflection.FieldInfo _bonuses, _description, _name;
        private static bool _modValues;

        private static AccessTools.FieldRef<ToolController, GameProgressManager> _toolProgress;
        private static AccessTools.FieldRef<ToolController, GameObject> _bankVisual;
        private static AccessTools.FieldRef<ToolController, float> _packTime;

        // Englisch wie im Spiel, Deutsch im Wortlaut der Spielübersetzung.
        private const string GoldRiverText = "Chute drainage +300%.";
        private const string BranchManagerText = "Pocket Bank's automatic collection rate +300%, including its maximum rate. Bank radius: 9 → 15 m. Bag capacity +33.11%.";
        private const string PocketBankText = "Equip Pocket Bank with {Tool4}. Hold and release {Primary} to place it; press {Tool4} again to recall it to your hand from anywhere. Automatically banks visible nearby gold. A bigger bag improves its rate.";

        private static readonly (string English, string German)[] German =
        {
            (GoldRiverText, "Rutschenabfluss +300%."),
            (BranchManagerText, "Automatische Sammelrate der Taschenbank +300%, einschließlich ihrer Höchstrate. Bankradius: 9 → 15 m. Taschengröße +33,11%."),
            (PocketBankText, "Rüste die Taschenbank mit {Tool4} aus. Halte {Primary} gedrückt und lasse los, um sie zu platzieren; drücke erneut {Tool4}, um sie von überall in deine Hand zurückzuholen. Zahlt sichtbares Gold in der Nähe automatisch ein. Eine größere Tasche erhöht die Sammelrate."),
        };

        internal static void Apply(Harmony harmony)
        {
            try
            {
                _bonuses = AccessTools.Field(typeof(SkillDefinition), nameof(SkillDefinition.Bonuses));
                _description = AccessTools.Field(typeof(SkillDefinition), nameof(SkillDefinition.Description));
                _name = AccessTools.Field(typeof(SkillDefinition), nameof(SkillDefinition.Name));
                ReadStationGroups();
                BuildStationTexts();
                _toolProgress = AccessTools.FieldRefAccess<ToolController, GameProgressManager>("_progress");
                _bankVisual = AccessTools.FieldRefAccess<ToolController, GameObject>("_bankVisual");
                _packTime = AccessTools.FieldRefAccess<ToolController, float>("_packTime");
                var progress = typeof(GameProgressManager);
                // Jede Neuberechnung der Boni läuft über eine dieser beiden Methoden; vorher die Werte zum Modus passend setzen.
                harmony.Patch(AccessTools.Method(progress, "RestoreSkills"), prefix: new HarmonyMethod(typeof(SkillPatches), nameof(SyncPrefix)));
                harmony.Patch(AccessTools.Method(progress, "ApplyLearnedReward"), prefix: new HarmonyMethod(typeof(SkillPatches), nameof(SyncPrefix)));
                harmony.Patch(AccessTools.PropertyGetter(progress, nameof(GameProgressManager.PocketBankRange)),
                    postfix: new HarmonyMethod(typeof(SkillPatches), nameof(RangePostfix)));
                harmony.Patch(AccessTools.Method(progress, "CaptureTooltipBuild"), postfix: new HarmonyMethod(typeof(SkillPatches), nameof(TooltipPostfix)));
                harmony.Patch(AccessTools.Method(progress, nameof(GameProgressManager.IsDepositStationUnlocked)),
                    prefix: new HarmonyMethod(typeof(SkillPatches), nameof(StationUnlockedPrefix)));
                harmony.Patch(AccessTools.Method(typeof(ToolController), nameof(ToolController.SelectShortcut)),
                    prefix: new HarmonyMethod(typeof(SkillPatches), nameof(ShortcutPrefix)));
                harmony.Patch(AccessTools.Method(typeof(TranslationCatalog), nameof(TranslationCatalog.FromJson)),
                    postfix: new HarmonyMethod(typeof(SkillPatches), nameof(CatalogPostfix)));
                // Ein schon geladener deutscher Katalog bekommt die Zeilen nachträglich.
                foreach (var language in GameLocalization.SupportedLanguages)
                {
                    if (language.Id == "de") AddGerman(Traverse.Create(language).Field<TranslationCatalog>("_catalog").Value);
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Fertigkeiten nicht angepasst (Werte wie im Spiel): " + e.Message);
            }
        }

        private static void SyncPrefix() => Sync();

        /// <summary>Setzt die Mod-Werte im 5-Mio.-Modus und stellt außerhalb die Originale wieder her.</summary>
        internal static void Sync()
        {
            if (_modValues == ModState.Active || _bonuses == null) return;
            _modValues = ModState.Active;
            Change(GoldRiver, (ref SkillBonuses b) => b.ChuteGrowth = Mathf.Log(4f), GoldRiverText);
            Change(BranchManager, (ref SkillBonuses b) => b.PocketBankGrowth = Mathf.Log(4f), BranchManagerText);
            Change(PocketBank, null, PocketBankText);
            foreach (var station in _stations) Change(station.Id, null, station.NewText, station.NewName);
            MovementBranch.Sync(_modValues);
            Plugin.Log.LogInfo(_modValues
                ? "Fertigkeiten: Goldfluss und Filialleiter +300 %, Bankradius 15 m, [4] holt die Taschenbank zurück, Ast Bewegung, " +
                  "Einzahlautomaten " + string.Join(", ", _stations.Select(s => s.Range)) + "."
                : "Fertigkeiten: Werte des Originals, ohne Ast Bewegung.");
        }

        private delegate void BonusChange(ref SkillBonuses bonuses);

        private static void Change(int id, BonusChange change, string text, string name = null)
        {
            var skill = SkillCatalog.Get((SkillId)id);
            if (skill == null)
            {
                Plugin.Log.LogWarning($"Fertigkeit {id} nicht gefunden.");
                return;
            }
            if (!Originals.ContainsKey(id)) Originals[id] = (skill.Bonuses, skill.Description, skill.Name);
            var original = Originals[id];
            if (!_modValues)
            {
                _bonuses.SetValue(skill, original.Bonuses);
                _description.SetValue(skill, original.Description);
                _name.SetValue(skill, original.Name);
                return;
            }
            var bonuses = original.Bonuses;
            change?.Invoke(ref bonuses);
            _bonuses.SetValue(skill, bonuses);
            _description.SetValue(skill, text);
            if (name != null) _name.SetValue(skill, name);
        }

        // ---- Einzahlautomaten ----

        /// <summary>Höchster Stationsrang = unterste Etage (Rang = Etage von oben, gesetzt in FloorBuilder.Renumber).</summary>
        private static int LowestStation => Diagnostics.KeepOriginal ? 5 : Plugin.FloorCount.Value - 1;

        /// <summary>Letzte Etage je Gruppe aus [Etagen] Automatengruppen; Standard 2, 5, 9, 15, 24.</summary>
        private static int[] _groupEnds = { 2, 5, 9, 15, 24 };

        /// <summary>
        /// Letzte Etage der Gruppe k (1–5). Wachsende Gruppen nach Bl4ck969 (30.10.2026): „der erste Skillpunkt Etage 1 und 2,
        /// der zweite 3 bis 5 usw.“ – fortgesetzt mit 06–09, 10–15, 16–24. Die letzte Gruppe reicht immer bis ganz unten.
        /// </summary>
        private static int GroupEnd(int group) =>
            group >= StationSkills.Length ? LowestStation : Mathf.Min(_groupEnds[group - 1], LowestStation);

        private static void ReadStationGroups()
        {
            var parts = Plugin.StationGroups.Value.Split(',');
            var ends = new int[StationSkills.Length];
            bool valid = parts.Length == ends.Length;
            for (int i = 0; valid && i < ends.Length; i++)
                valid = int.TryParse(parts[i].Trim(), out ends[i]) && ends[i] >= 1 && (i == 0 || ends[i] > ends[i - 1]);
            if (valid) _groupEnds = ends;
            else Plugin.Log.LogWarning($"[Etagen] Automatengruppen \"{Plugin.StationGroups.Value}\" ungültig (5 aufsteigende Zahlen); es gilt 2,5,9,15,24.");
        }

        /// <summary>Gruppe (1–5), deren Skill den Automaten auf dieser Etage freischaltet.</summary>
        internal static int StationGroup(int rank)
        {
            for (int group = 1; group < StationSkills.Length; group++)
                if (rank <= GroupEnd(group)) return group;
            return StationSkills.Length;
        }

        internal static SkillId StationSkill(int rank) => (SkillId)StationSkills[StationGroup(rank) - 1];

        /// <summary>
        /// Im Spiel schaltet jeder der fünf Skills genau eine Etage (01–05) frei; die Etagen darunter blieben im Mod bisher
        /// ohne Regel und waren deshalb alle offen (Bl4ck969, 30.10.2026). Jetzt schaltet jeder Skill eine Gruppe frei.
        /// </summary>
        private static bool StationUnlockedPrefix(GameProgressManager __instance, int rank, ref bool __result)
        {
            if (!ModState.Active) return true;
            __result = rank <= 0 || __instance.HasSkill(StationSkill(rank));
            return false;
        }

        /// <summary>Namen und Texte der fünf Skills: „Ebene 03“ wird „Ebenen 11–15“, aus dem Wortlaut des Spiels abgeleitet.</summary>
        private static void BuildStationTexts()
        {
            var list = new List<(int, string, string, string, string, string, string)>();
            for (int i = 0; i < StationSkills.Length; i++)
            {
                var skill = SkillCatalog.Get((SkillId)StationSkills[i]);
                if (skill == null) continue;
                int low = i == 0 ? 1 : GroupEnd(i) + 1, high = GroupEnd(i + 1);
                string floor = $"{i + 1:00}", range = $"{low:00}–{high:00}";
                string name = skill.Name.Replace("Lower hopper " + floor, "Lower hoppers " + range);
                string text = skill.Description.Replace("the deposit station on floor " + floor, "the deposit stations on floors " + range);
                if (text == skill.Description) Plugin.Log.LogWarning($"Fertigkeit {skill.Id}: Etagen-Text nicht gefunden, bleibt \"{skill.Description}\".");
                list.Add((StationSkills[i], floor, range, skill.Name, name, skill.Description, text));
            }
            _stations = list.ToArray();
        }

        private static void RangePostfix(GameProgressManager __instance, ref float __result)
        {
            if (ModState.Active && __instance.UsesV12Balance && __instance.HasSkill((SkillId)BranchManager)) __result = BranchManagerRange;
        }

        /// <summary>Vergleichswerte im Tooltip: nur Filialleiter ergibt im Spiel 12 m.</summary>
        private static void TooltipPostfix(Dictionary<string, float> __result)
        {
            if (ModState.Active && __result != null && __result.TryGetValue("bankRange", out float range) && range == 12f)
                __result["bankRange"] = BranchManagerRange;
        }

        /// <summary>
        /// [4] bei abgestellter Taschenbank: einpacken wie nach 1 s [F] daneben (Spielablauf in TickPocketBank) und in die
        /// Hand nehmen – ohne den Weg zurück zur Bank.
        /// </summary>
        private static bool ShortcutPrefix(ToolController __instance, int number)
        {
            if (!ModState.Active || number != 4) return true;
            var progress = _toolProgress(__instance);
            if (progress == null || !progress.StationDeployed || !progress.IsToolUnlocked(ToolId.Cyclone)) return true;
            progress.PackPocketBank();
            MenuAudio.Equip();
            var visual = _bankVisual(__instance);
            if (visual != null) visual.SetActive(false);
            _packTime(__instance) = 0f;
            __instance.Select(ToolId.Cyclone);
            return false;
        }

        private static void CatalogPostfix(TranslationCatalog __result, string languageId)
        {
            if (languageId == "de") AddGerman(__result);
        }

        private static void AddGerman(TranslationCatalog catalog)
        {
            if (catalog == null) return;
            var exact = Traverse.Create(catalog).Field<Dictionary<string, string>>("_exact").Value;
            if (exact == null) return;
            foreach (var (english, german) in German.Concat(MovementBranch.German()))
            {
                if (!exact.ContainsKey(english)) exact.Add(english, german);
            }
            // Freischalt-Skills: die deutsche Fassung des Spiels mit den Etagen der Gruppe.
            foreach (var station in _stations)
            {
                if (exact.TryGetValue(station.OldName, out var name) && !exact.ContainsKey(station.NewName))
                    exact.Add(station.NewName, name.Replace("Unterer Trichter " + station.Floor, "Untere Trichter " + station.Range));
                if (exact.TryGetValue(station.OldText, out var text) && !exact.ContainsKey(station.NewText))
                    exact.Add(station.NewText, text.Replace("die Einzahlstation auf Ebene " + station.Floor, "die Einzahlstationen auf den Ebenen " + station.Range));
            }
        }
    }
}
