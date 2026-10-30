using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using GoldDigger;
using HarmonyLib;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Abschlussdialog im 5-Mio.-Modus (Bl4ck969, 30.10.2026: „der Abschlussscreenshot ist falsch, ich habe ja 5 Mio
    /// eingesammelt“). Das Spiel schreibt dort fest „1,000,000 coins collected“ und zeigt rot „Nicht für die Bestenlisten
    /// eingereicht“, weil seine Steam-Prüfung genau 1.000.000 Münzen verlangt. Im Mod-Modus stehen stattdessen die
    /// Münzzahl des Haufens und der Platz in der Offline-Bestenliste da. Knöpfe und Weiterspielen bleiben die des Spiels.
    /// </summary>
    internal static class CompletionPatches
    {
        internal const string CoinsOriginal = "1,000,000 coins collected. Every one delivered.";
        private const string SteamUnavailable = "Steam is unavailable. This run has not been submitted.";

        // Die Zahl in der übersetzten Zeile mit ihrem Tausendertrenner („1,000,000“, „1.000.000“, „1 000 000“). Benannte
        // Gruppe, weil .NET „\1000“ nicht als Rückverweis + 000 liest, sondern als Oktalzeichen (30.10.2026 im Prüflauf).
        private static readonly Regex Number = new Regex(@"1(?<sep>[^0-9]?)000\k<sep>000");

        private static AccessTools.FieldRef<GoldDiggerHUD, GameProgressManager> _progress;
        private static AccessTools.FieldRef<GoldDiggerHUD, SaveManager> _saves;
        private static int _replaced;

        internal static void Apply(Harmony harmony)
        {
            _progress = AccessTools.FieldRefAccess<GoldDiggerHUD, GameProgressManager>("_progress");
            _saves = AccessTools.FieldRefAccess<GoldDiggerHUD, SaveManager>("_saveManager");
            harmony.Patch(AccessTools.Method(typeof(GoldDiggerHUD), "DrawCompletion"), transpiler: new HarmonyMethod(typeof(CompletionPatches), nameof(Transpiler)));
            // Soll: Münzzeile 1, Sperrgrund 2 (Abfrage und Anzeige), Steam-Status 1, „Steam nicht erreichbar“ 1.
            if (_replaced == 5) Plugin.Log.LogInfo("Abschlussdialog: 5 Stellen angepasst.");
            else Plugin.Log.LogWarning($"Abschlussdialog: {_replaced} von 5 Stellen angepasst – zeigt im 5-Mio.-Modus teilweise die Texte des Spiels.");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var reason = AccessTools.Field(typeof(GoldDiggerHUD), "_completionLeaderboardReason");
            foreach (var instruction in instructions)
            {
                yield return instruction;
                if (instruction.opcode == OpCodes.Ldstr && (string)instruction.operand == CoinsOriginal)
                {
                    _replaced++;
                    yield return CodeInstruction.Call(typeof(CompletionPatches), nameof(CoinsLine));
                }
                else if (instruction.LoadsField(reason))
                {
                    _replaced++;
                    yield return CodeInstruction.Call(typeof(CompletionPatches), nameof(Reason));
                }
                else if ((instruction.operand is MethodInfo method && method.Name == "CompletionUploadStatus")
                    || (instruction.opcode == OpCodes.Ldstr && (string)instruction.operand == SteamUnavailable))
                {
                    _replaced++;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return CodeInstruction.Call(typeof(CompletionPatches), nameof(Status));
                }
            }
        }

        private static string _coinsSource, _coinsText;
        private static int _coinsTotal;

        /// <summary>Übersetzte Zeile des Spiels mit der Münzzahl des Haufens; ohne Zahl in der Übersetzung englisch.</summary>
        internal static string CoinsLine(string original)
        {
            if (!ModState.Active) return original;
            string translated = GameLocalization.Text(original);
            if (translated != _coinsSource || GoalPatches.ModTotal != _coinsTotal)
            {
                _coinsSource = translated;
                _coinsTotal = GoalPatches.ModTotal;
                string total = _coinsTotal.ToString("N0", CultureInfo.InvariantCulture);
                var match = Number.Match(translated);
                _coinsText = match.Success
                    ? translated.Substring(0, match.Index) + total.Replace(",", match.Groups["sep"].Value) + translated.Substring(match.Index + match.Length)
                    : total + " coins collected. Every one delivered.";
            }
            return _coinsText;
        }

        /// <summary>Kein roter Kasten: Die Steam-Prüfung gilt dem Original, der Mod-Lauf geht nie an Steam.</summary>
        internal static string Reason(string reason) => ModState.Active ? null : reason;

        /// <summary>Statt des Steam-Status: Platz in der Bestenliste auf diesem PC.</summary>
        internal static string Status(string original, GoldDiggerHUD hud)
        {
            if (!ModState.Active) return original;
            var progress = _progress(hud);
            if (progress != null && progress.TestingLoadoutActive)
                return ModText.Of("5 million mode · test run · not ranked.", "5-Mio.-Modus · Testlauf · nicht gewertet.");
            int place = progress != null ? Leaderboard.Place(progress, _saves(hud)) : 0;
            return place > 0
                ? ModText.Of($"5 million mode · offline · rank {place} in your leaderboard on this PC.", $"5-Mio.-Modus · offline · Platz {place} in deiner Bestenliste auf diesem PC.")
                : ModText.Of("5 million mode · offline · nothing goes to Steam.", "5-Mio.-Modus · offline · nichts geht an Steam.");
        }
    }
}
