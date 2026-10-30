using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using GoldDigger;
using HarmonyLib;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Ersetzt die festen 1.000.000 / 999.999 der Spiellogik durch die tatsächliche Münzzahl des Haufens.
    /// Außerhalb des Mod-Modus liefern die Ersatzwerte genau die Originalzahlen.
    /// </summary>
    internal static class GoalPatches
    {
        private const int Original = GranularPileModel.Goal;

        /// <summary>Münzen im Haufen des Mod-Modus (vom Haufen-Start gesetzt).</summary>
        internal static int ModTotal = Original;

        internal static int Goal() => ModState.Active ? ModTotal : Original;
        internal static int Ordinary() => Goal() - 1;
        internal static long GoalLong() => Goal();
        internal static float GoalFloat() => Goal();
        internal static double OrdinaryDouble() => Ordinary();

        private static int _replaced;
        private static bool _firstOnly;

        internal static void Apply(Harmony harmony)
        {
            int methods = 0;
            foreach (var method in AccessTools.GetDeclaredMethods(typeof(GameProgressManager)))
            {
                if (method.IsAbstract || method.ContainsGenericParameters || !UsesGoalConstant(method)) continue;
                _firstOnly = false;
                harmony.Patch(method, transpiler: new HarmonyMethod(typeof(GoalPatches), nameof(Transpiler)));
                methods++;
            }
            // Laden eines Mod-Spielstands: Plausibilitätsgrenzen und erwarteter Rest des Haufens.
            foreach (var method in new[] { AccessTools.Method(typeof(SaveManager), "TryLoadFile"), AccessTools.Method(typeof(CoinFieldManager), "TryDecodeDensity") })
            {
                if (method == null || !UsesGoalConstant(method)) { Plugin.Log.LogError("Münzziel beim Laden nicht umgestellt: Methode fehlt."); continue; }
                _firstOnly = false;
                harmony.Patch(method, transpiler: new HarmonyMethod(typeof(GoalPatches), nameof(Transpiler)));
                methods++;
            }
            // Das Zählwerk an der Station: nur die Kappung anheben, nicht den Stellenteiler (7 Stellen bleiben).
            var counter = AccessTools.Method(typeof(DepositMachineVisual), "SetCounter");
            if (counter != null)
            {
                _firstOnly = true;
                harmony.Patch(counter, transpiler: new HarmonyMethod(typeof(GoalPatches), nameof(Transpiler)));
                methods++;
            }
            Plugin.Log.LogInfo($"Münzziel: {_replaced} Stellen in {methods} Methoden umgestellt.");
        }

        private static bool UsesGoalConstant(MethodInfo method)
        {
            byte[] il;
            try { il = method.GetMethodBody()?.GetILAsByteArray(); }
            catch { return false; }
            if (il == null) return false;
            for (int i = 0; i + 4 < il.Length; i++)
            {
                if ((il[i] == 0x20 || il[i] == 0x21) && (BitConverter.ToInt32(il, i + 1) == Original || BitConverter.ToInt32(il, i + 1) == Original - 1)) return true;
                if (il[i] == 0x22 && (BitConverter.ToSingle(il, i + 1) == 1000000f || BitConverter.ToSingle(il, i + 1) == 999999f)) return true;
                if (il[i] == 0x23 && i + 8 < il.Length && (BitConverter.ToDouble(il, i + 1) == 1000000.0 || BitConverter.ToDouble(il, i + 1) == 999999.0)) return true;
            }
            return false;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool done = false;
            foreach (var instruction in instructions)
            {
                string replacement = done ? null : Replacement(instruction);
                if (replacement != null)
                {
                    var call = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(GoalPatches), replacement));
                    call.labels.AddRange(instruction.labels);
                    call.blocks.AddRange(instruction.blocks);
                    _replaced++;
                    done = _firstOnly;
                    yield return call;
                    continue;
                }
                yield return instruction;
            }
        }

        private static string Replacement(CodeInstruction instruction)
        {
            var op = instruction.opcode;
            var value = instruction.operand;
            if (op == OpCodes.Ldc_I4 && value is int i)
            {
                if (i == Original) return nameof(Goal);
                if (i == Original - 1) return nameof(Ordinary);
            }
            if (op == OpCodes.Ldc_I8 && value is long l && l == Original) return nameof(GoalLong);
            if (op == OpCodes.Ldc_R4 && value is float f && f == 1000000f) return nameof(GoalFloat);
            if (op == OpCodes.Ldc_R8 && value is double d && d == 999999.0) return nameof(OrdinaryDouble);
            return null;
        }
    }
}
