using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using GoldDigger;
using HarmonyLib;
using UnityEngine;

namespace OneMillionCoins5Mio
{
    /// <summary>Haken für Gebäude und Haufen: Etagen einsetzen, bevor der Haufen die Böden abtastet.</summary>
    internal static class WorldPatches
    {
        /// <summary>Neue Obergrenze statt der festen 1.000.000 im Konstruktor von GranularPileModel.</summary>
        private const int LiftedCoinLimit = 0x3FFFFFFF;

        internal static bool PileLimitLifted;

        internal static void Apply(Harmony harmony)
        {
            var ctor = AccessTools.GetDeclaredConstructors(typeof(GranularPileModel))
                .FirstOrDefault(c => c.GetParameters().Length == 7);
            if (ctor != null)
            {
                harmony.Patch(ctor, transpiler: new HarmonyMethod(typeof(WorldPatches), nameof(LiftCoinLimit)));
            }
            if (!PileLimitLifted)
            {
                Plugin.Log.LogError("Münzgrenze im Haufen-Modell nicht gefunden: Der Haufen bleibt bei 1 Mio.");
            }

            harmony.Patch(AccessTools.Method(typeof(GranularCoinPile), "Initialize"),
                prefix: new HarmonyMethod(typeof(WorldPatches), nameof(PileInitializePrefix)));
            harmony.Patch(AccessTools.Method(typeof(GameSceneController), "InitializeScene"),
                prefix: new HarmonyMethod(typeof(WorldPatches), nameof(SceneInitializePrefix)),
                postfix: new HarmonyMethod(typeof(WorldPatches), nameof(SceneInitializePostfix)));
            harmony.Patch(AccessTools.Method(typeof(VaultLayerLayout), "CreateHeights"),
                postfix: new HarmonyMethod(typeof(WorldPatches), nameof(HeightsPostfix)));
            // VaultLayerLayout.CreateHeights wird vom JIT eingebettet; die Höhen deshalb direkt an den Wandrutschen übergeben.
            harmony.Patch(AccessTools.Method(typeof(PassiveWallChutes), "Initialize"),
                prefix: new HarmonyMethod(typeof(WorldPatches), nameof(ChutesHeightsPrefix)),
                postfix: new HarmonyMethod(typeof(WorldPatches), nameof(ChutesLogPostfix)));
            harmony.Patch(AccessTools.Method(typeof(WallChutePlacement), "TryPlace"),
                prefix: new HarmonyMethod(typeof(WorldPatches), nameof(ChuteColumnPrefix)));
        }

        /// <summary>Seitlicher Abstand der Rutschen-Reihe von der Wandmitte (rechts, wie die Mehrzahl im Original).</summary>
        private const float ChuteColumnOffset = 9.8f;

        /// <summary>
        /// Wandrutschen im Mod-Modus in einer senkrechten Reihe bei x = 9,8 (Entscheidung Bl4ck969, 26.10.2026):
        /// gleiche Seitenlage auf jeder Etage, bei Hindernis nur in 0,25-m-Schritten tiefer. Findet sich dort kein Platz,
        /// gilt die Regel des Spiels. Höhengrenzen wie im Original.
        /// </summary>
        private static bool ChuteColumnPrefix(WallChutePlacement __instance, Vector3 wall, Quaternion rotation, float floor, float ceiling,
            float initialSurface, float halfWidth, ref Vector3 position, ref bool __result)
        {
            if (!ModState.Active || Diagnostics.KeepOriginal || floor + 0.8f > initialSurface) return true;
            if (ChuteColumnOffset + 0.7f > halfWidth) return true;
            float start = float.IsPositiveInfinity(ceiling) ? floor + 0.8f : Mathf.Min(ceiling - 1.15f, initialSurface - 0.5f);
            for (float height = start; height - 0.48f > floor; height -= 0.25f)
            {
                if (height + 0.47f >= ceiling || height > initialSurface) continue;
                var candidate = wall + rotation * (Vector3.right * ChuteColumnOffset - Vector3.forward * 0.04f);
                candidate.y = height;
                if (!__instance.IsClear(candidate, rotation)) continue;
                position = candidate;
                __result = true;
                return false;
            }
            return true;
        }

        private static IEnumerable<CodeInstruction> LiftCoinLimit(IEnumerable<CodeInstruction> instructions)
        {
            var list = instructions.ToList();
            int replaced = 0;
            foreach (var instruction in list)
            {
                if (instruction.opcode == OpCodes.Ldc_I4 && instruction.operand is int value && value == GranularPileModel.Goal)
                {
                    instruction.operand = LiftedCoinLimit;
                    replaced++;
                }
            }
            // Erwartet: "initialCoins > 1000000" und "firstId > 1000000 - initialCoins".
            PileLimitLifted = replaced == 2;
            Plugin.Log.LogInfo($"Haufen-Grenze: {replaced} Stellen ersetzt.");
            return list;
        }

        private static void SceneInitializePrefix(GameSceneController __instance)
        {
            if (ModState.Active) FloorBuilder.EnsureBuilt(__instance.gameObject.scene);
        }

        // Prüfläufe: der eingebaute Testmodus des Spiels (1.000 Fertigkeitspunkte, nicht wertbar), gleich mit allen
        // Fertigkeiten gelernt. Im normalen Spiel nicht (seit 29.10.2026, Start bei null).
        private static void SceneInitializePostfix(GameSceneController __instance)
        {
            if (ModState.Active || Plugin.PerformanceAlways.Value) __instance.gameObject.AddComponent<PerformanceLog>();
            if (!ModState.Active) return;
            // Die Spielkamera sieht 100 m weit; das reicht für die 48 m des Originals, aber nicht für über 200 m. Dahinter
            // zeichnet sie den Himmel (Bl4ck969 sah ihn am 26.10.2026 vom Boden aus). Sichtweite = Gebäudehöhe plus Reserve.
            var camera = Traverse.Create(__instance).Field<Camera>("playerCamera").Value;
            if (camera != null && FloorBuilder.Heights != null && FloorBuilder.Heights.Length > 0 && !Diagnostics.KeepOriginal)
            {
                float needed = FloorBuilder.Heights.Max() - FloorBuilder.Heights.Min() + 80f;
                if (camera.farClipPlane < needed)
                {
                    Plugin.Log.LogInfo($"Sichtweite der Spielkamera: {camera.farClipPlane:0} -> {needed:0} m.");
                    camera.farClipPlane = needed;
                }
            }
            if (Diagnostics.Requested) __instance.gameObject.AddComponent<Diagnostics>();
            if (LoadTest.Requested) __instance.gameObject.AddComponent<LoadTest>();
            if (SaveTest.Requested) SaveTest.Begin();
            if (SkillTest.Requested) SkillTest.Begin();
            var scene = Traverse.Create(__instance);
            var runProgress = scene.Field<GameProgressManager>("progress").Value;
            var runSaves = scene.Field<SaveManager>("saveManager").Value;
            if (runProgress != null) runProgress.GameCompleted += () => Leaderboard.Record(runProgress, runSaves);
            if (!Plugin.TestLoadout) return;
            var progress = Traverse.Create(__instance).Field<GameProgressManager>("progress").Value;
            if (progress == null) return;
            progress.SetTestingLoadout(true);
            // Mehrere Durchgänge, weil Voraussetzungen erst nach ihren Vorgängern lernbar sind.
            int learned = 0;
            for (bool more = true; more;)
            {
                more = false;
                foreach (var skill in progress.SkillTree.All)
                {
                    if (!progress.HasSkill(skill.Id) && progress.TryLearnSkill(skill.Id))
                    {
                        learned++;
                        more = true;
                    }
                }
            }
            Plugin.Log.LogInfo($"Testmodus: {learned} Fertigkeiten gelernt, {progress.AvailableSkillPoints} Punkte übrig.");
        }

        private static void PileInitializePrefix(GranularCoinPile __instance)
        {
            if (!ModState.Active) return;
            FloorBuilder.EnsureBuilt(__instance.gameObject.scene);
            if (__instance.Model != null && __instance.Model.IsCreated) return;

            var pile = Traverse.Create(__instance);
            if (pile.Field("_presentationGeometry").GetValue() != null) return;   // Abschluss-Animation übernimmt die Werte des Haufens
            int initial = pile.Field<int>("initialCoins").Value;
            if (initial != GranularPileModel.Goal || !PileLimitLifted || FloorBuilder.Lift <= 0f) return;

            int grid = pile.Field<int>("gridSize").Value;
            float rise = pile.Field<float>("coinRise").Value;
            // Feste Münzzahl (Bl4ck969: genau 5 Mio., die letzte in der Kiste). Die Etagenhöhen sind so gewählt, dass die
            // Füllung wie im Original nur um die eingefügte Höhe höher steht; die Abweichung davon steht im Log.
            int scaled = Plugin.CoinTotal.Value;
            float planned = initial + FloorBuilder.Lift * grid * grid / rise;
            pile.Field("initialCoins").SetValue(scaled);
            GoalPatches.ModTotal = scaled;
            Plugin.Log.LogInfo($"Haufen: {initial:N0} -> {scaled:N0} Münzen ({grid}x{grid} Säulen, {rise:0.####} m je Münze), " +
                $"Füllhöhe {(scaled - planned) * rise / (grid * grid) * 100f:+0;-0} cm gegenüber der Etagenplanung.");
        }

        private static void HeightsPostfix(ref float[] __result)
        {
            if (ModState.Active && FloorBuilder.Heights != null) __result = (float[])FloorBuilder.Heights.Clone();
        }

        private static void ChutesHeightsPrefix(ref float[] floors)
        {
            if (ModState.Active && FloorBuilder.Heights != null) floors = (float[])FloorBuilder.Heights.Clone();
        }

        private static void ChutesLogPostfix(PassiveWallChutes __instance)
        {
            var mounts = Traverse.Create(__instance).Field<Transform[]>("_mounts").Value;
            if (mounts == null) return;
            Plugin.Log.LogInfo($"Wandrutschen ({(ModState.Active ? "5 Mio." : "Original")}): " +
                string.Join(" | ", mounts.Select(m => $"{m.name.Replace("Wall chute - floor ", "")}: x {m.position.x:0.0} y {m.position.y:0.0}")));
        }

        // Prototyp: alle Stationen offen. Die Freischaltung über den erweiterten Baum folgt später.
    }
}
