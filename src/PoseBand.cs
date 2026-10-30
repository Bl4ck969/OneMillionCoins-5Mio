using GoldDigger;
using HarmonyLib;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Münzen nur anordnen und zeichnen, wo man sie sehen kann: im Höhenband um den Spieler und überall ohne Decke darüber. Die Jobs des Spiels (GranularFlowSystem.Pose/Sides,
    /// Burst) gehen sonst je Bild über alle Schichten aller 25 Etagen (gemessen 26.10.2026: 16 ms; das Original hat ein
    /// Viertel der Schichten). Der Mod reicht ihnen dieselben Daten, aber je Säule nur die Schichten im Band, und sortiert
    /// das Ergebnis zurück. Schichten außerhalb liegen hinter Decken; sie bekommen keine Lagen und werden nicht gezeichnet.
    ///
    /// Je Säule steht vor den Band-Schichten eine Ersatzschicht mit allen Münzen darunter, weil die Jobs die Münzen einer
    /// Säule über die Summe der tieferen Schichten finden. Sie liegt weit unter dem Gebäude: ihre Lagen werden verworfen,
    /// sie öffnet keine Seitenwände, und die dabei geschriebenen Münzpositionen springen später auf ihren Platz zurück
    /// (der Pose-Job setzt jede Münze neu, die unter ihrer Schicht liegt).
    /// </summary>
    internal static unsafe class PoseBand
    {
        /// <summary>Band relativ zur Spielerhöhe in Metern; bei Abweichung über Shift wird neu angeordnet.</summary>
        internal const float Below = 20f, Above = 16f, Shift = 4f;
        private const float Parking = -100000f;

        private static bool _inside;
        private static float _center = float.NaN;
        private static float? _player;

        // Ersatz-Arrays (wachsen bei Bedarf, werden wiederverwendet).
        private static NativeArray<int> _starts, _counts, _sideStarts;
        private static NativeArray<float> _bases, _ceilings;
        private static NativeArray<GranularCoinPose> _poses;
        private static NativeList<GranularCoinPose> _sides;

        // Band je Säule in globalen Schichtnummern [first, end) und die Schichten, die zuletzt Lagen bekamen.
        private static int[] _first = new int[0], _end = new int[0], _top = new int[0], _localBand = new int[0], _localTop = new int[0];
        private static int[] _posedFirst = new int[0], _posedEnd = new int[0], _posedTop = new int[0];
        private static int _layerCount = -1;
        private static bool _allPosed = true;

        private static AccessTools.FieldRef<GranularPileModel, int> _posedRevision;

        internal static void Apply(Harmony harmony)
        {
            _posedRevision = AccessTools.FieldRefAccess<GranularPileModel, int>("_posedRevision");
            harmony.Patch(AccessTools.Method(typeof(GranularFlowSystem), "Pose"), prefix: new HarmonyMethod(typeof(PoseBand), nameof(PosePrefix)));
            harmony.Patch(AccessTools.Method(typeof(GranularFlowSystem), "Sides"), prefix: new HarmonyMethod(typeof(PoseBand), nameof(SidesPrefix)));
        }

        /// <summary>Spielerhöhe im Raum des Haufens, einmal je Bild von PilePatches gesetzt; null = Spiel rechnet alles.</summary>
        internal static void SetPlayer(GranularPileModel model, float? height)
        {
            _player = height;
            if (height.HasValue && !float.IsNaN(_center) && Mathf.Abs(height.Value - _center) > Shift)
                _posedRevision(model) = -1;   // Lagen beim nächsten Durchlauf neu, auch ohne neue Münzbewegung
        }

        /// <summary>Nur für den Bildvergleich im Leistungstest: alles anordnen wie das Spiel.</summary>
        internal static bool Bypass;

        /// <summary>Lagen beim nächsten Durchlauf neu berechnen.</summary>
        internal static void Repose(GranularPileModel model) => _posedRevision(model) = -1;

        private static bool Active => ModState.Active && !Diagnostics.KeepOriginal && _player.HasValue && !Bypass;

        private static bool PosePrefix(GranularFlowSystem __instance, NativeArray<Entity> cells, NativeArray<int> starts, NativeArray<int> layerCounts,
            NativeArray<float> bases, NativeArray<float> ceilings, NativeArray<GranularCoinPose> poses, NativeArray<float3> positions,
            NativeArray<byte> valid, int size, float pitch, float rise, float dt, int firstId)
        {
            if (_inside) return true;
            if (!Active) { _allPosed = true; return true; }
            int compact = Build(cells.Length, starts, layerCounts, bases, ceilings, rise);
            Ensure(ref _poses, compact * 5);
            _inside = true;
            try
            {
                __instance.Pose(cells, _starts, _counts, _bases, _ceilings, _poses, positions, valid, size, pitch, rise, dt, firstId);
            }
            finally { _inside = false; }

            var global = (GranularCoinPose*)poses.GetUnsafePtr();
            var local = (GranularCoinPose*)_poses.GetUnsafeReadOnlyPtr();
            var empty = new GranularCoinPose { Id = -1 };
            int columns = cells.Length;
            for (int c = 0; c < columns; c++)
            {
                int first = _first[c], end = _end[c], top = _top[c];
                // Was zuletzt Lagen hatte und jetzt nicht mehr dabei ist, wird geleert.
                int oldFirst = _allPosed ? starts[c] : _posedFirst[c], oldEnd = _allPosed ? starts[c + 1] : _posedEnd[c];
                for (int j = oldFirst; j < oldEnd; j++)
                    if ((j < first || j >= end) && j != top)
                        for (int k = 0; k < 5; k++) global[j * 5 + k] = empty;
                int oldTop = _allPosed ? -1 : _posedTop[c];
                if (oldTop >= 0 && (oldTop < first || oldTop >= end) && oldTop != top)
                    for (int k = 0; k < 5; k++) global[oldTop * 5 + k] = empty;
                if (end > first)
                    UnsafeUtility.MemCpy(global + first * 5, local + _localBand[c] * 5, (long)(end - first) * 5 * sizeof(GranularCoinPose));
                if (top >= 0)
                    UnsafeUtility.MemCpy(global + top * 5, local + _localTop[c] * 5, 5L * sizeof(GranularCoinPose));
                _posedFirst[c] = first;
                _posedEnd[c] = end;
                _posedTop[c] = top;
            }
            _allPosed = false;
            _center = _player.Value;
            return false;
        }

        private static bool SidesPrefix(GranularFlowSystem __instance, NativeArray<Entity> cells, NativeArray<int> starts, NativeArray<int> layerCounts,
            NativeArray<float> bases, NativeArray<float> ceilings, NativeList<GranularCoinPose> sides, NativeArray<int> sideStarts, int size, float pitch, float rise)
        {
            if (_inside) return true;
            if (!Active) return true;
            int compact = Build(cells.Length, starts, layerCounts, bases, ceilings, rise);
            Ensure(ref _sideStarts, compact + 1);
            if (!_sides.IsCreated) _sides = new NativeList<GranularCoinPose>(4096, Allocator.Persistent);
            _inside = true;
            try
            {
                __instance.Sides(cells, _starts, _counts, _bases, _ceilings, _sides, _sideStarts, size, pitch, rise);
            }
            finally { _inside = false; }

            sides.Clear();
            var global = (int*)sideStarts.GetUnsafePtr();
            var local = (int*)_sideStarts.GetUnsafeReadOnlyPtr();
            var localSides = (GranularCoinPose*)_sides.GetUnsafeReadOnlyPtr();
            int localSideCount = _sides.Length;
            int columns = cells.Length;
            for (int c = 0; c < columns; c++)
            {
                int first = _first[c], end = _end[c], top = _top[c];
                for (int j = starts[c]; j < first; j++) global[j] = sides.Length;
                if (end > first)
                {
                    int lb = _localBand[c];
                    int from = local[lb], to = LocalEnd(local, lb + (end - first), compact, localSideCount);
                    for (int j = first; j < end; j++) global[j] = sides.Length + local[lb + (j - first)] - from;
                    if (to > from) sides.AddRange(localSides + from, to - from);
                }
                for (int j = end; j < starts[c + 1]; j++)
                {
                    global[j] = sides.Length;
                    if (j != top) continue;
                    int lt = _localTop[c];
                    int from = local[lt], to = LocalEnd(local, lt + 1, compact, localSideCount);
                    if (to > from) sides.AddRange(localSides + from, to - from);
                }
            }
            global[sideStarts.Length - 1] = sides.Length;
            return false;
        }

        /// <summary>
        /// Ende der Seitenmünzen einer Ersatz-Schicht: Start der nächsten. Der Job setzt die Starts aller seiner Schichten
        /// und den letzten Eintrag des Arrays; hinter der letzten Schicht gilt deshalb die Länge der Liste.
        /// </summary>
        private static int LocalEnd(int* local, int next, int compact, int count) => next < compact ? local[next] : count;

        /// <summary>
        /// Baut die Ersatz-Arrays. Je Säule sichtbar: das Band um den Spieler (ab der ersten Schicht, deren Decke nicht unter
        /// dem Band liegt) und die oberste Schicht mit Münzen. Auf die blickt man von oben, auch durch Treppenlöcher mehrere
        /// Etagen tiefer (Bildvergleich 26.10.2026). Übersprungene Schichten mit Münzen stehen als Ersatzschicht davor.
        /// _top ist nur gesetzt, wenn die oberste Schicht über dem Band liegt; sonst gehört sie schon zum Band.
        /// </summary>
        private static int Build(int columns, NativeArray<int> starts, NativeArray<int> counts, NativeArray<float> bases, NativeArray<float> ceilings, float rise)
        {
            if (_first.Length != columns || _layerCount != counts.Length)
            {
                _first = new int[columns]; _end = new int[columns]; _top = new int[columns]; _localBand = new int[columns]; _localTop = new int[columns];
                _posedFirst = new int[columns]; _posedEnd = new int[columns]; _posedTop = new int[columns];
                _layerCount = counts.Length;
                _allPosed = true;
            }
            float low = _player.Value - Below, high = _player.Value + Above;
            Ensure(ref _starts, columns + 1);
            Ensure(ref _counts, counts.Length + 2 * columns);
            Ensure(ref _bases, counts.Length + 2 * columns);
            Ensure(ref _ceilings, counts.Length + 2 * columns);
            var gCounts = (int*)counts.GetUnsafeReadOnlyPtr();
            var gBases = (float*)bases.GetUnsafeReadOnlyPtr();
            var gCeilings = (float*)ceilings.GetUnsafeReadOnlyPtr();
            var lStarts = (int*)_starts.GetUnsafePtr();
            var lCounts = (int*)_counts.GetUnsafePtr();
            var lBases = (float*)_bases.GetUnsafePtr();
            var lCeilings = (float*)_ceilings.GetUnsafePtr();
            int n = 0;
            for (int c = 0; c < columns; c++)
            {
                int s0 = starts[c], s1 = starts[c + 1];
                int top = s1 - 1;
                while (top >= s0 && gCounts[top] == 0) top--;
                if (top < s0) top = -1;
                // Schichten einer Säule liegen aufsteigend, ihre Decken auch: Band und Oberste sind je ein Abschnitt.
                int first = s0;
                while (first < s1 && gCeilings[first] < low) first++;
                if (top >= 0 && top < first) first = top;
                int end = first;
                while (end < s1 && gBases[end] <= high) end++;
                if (top < end) top = -1;
                _first[c] = first;
                _end[c] = end;
                _top[c] = top;
                lStarts[c] = n;
                if (first > s0) Park(ref n, Sum(gCounts, s0, first), rise, lCounts, lBases, lCeilings);
                _localBand[c] = n;
                int band = end - first;
                if (band > 0)
                {
                    UnsafeUtility.MemCpy(lCounts + n, gCounts + first, band * sizeof(int));
                    UnsafeUtility.MemCpy(lBases + n, gBases + first, band * sizeof(float));
                    UnsafeUtility.MemCpy(lCeilings + n, gCeilings + first, band * sizeof(float));
                    n += band;
                }
                if (top >= 0)
                {
                    if (top > end) Park(ref n, Sum(gCounts, end, top), rise, lCounts, lBases, lCeilings);
                    _localTop[c] = n;
                    lCounts[n] = gCounts[top];
                    lBases[n] = gBases[top];
                    lCeilings[n] = gCeilings[top];
                    n++;
                }
            }
            lStarts[columns] = n;
            return n;
        }

        private static int Sum(int* counts, int from, int to)
        {
            int sum = 0;
            for (int j = from; j < to; j++) sum += counts[j];
            return sum;
        }

        /// <summary>Ersatzschicht weit unter dem Gebäude für übersprungene Münzen (siehe Klassenbeschreibung).</summary>
        private static void Park(ref int n, int count, float rise, int* counts, float* bases, float* ceilings)
        {
            counts[n] = count;
            bases[n] = Parking;
            ceilings[n] = Parking + count * rise;
            n++;
        }

        /// <summary>Ersatz-Array mindestens dieser Länge; die Jobs lesen nur über Starts, Überlänge stört nicht.</summary>
        private static void Ensure<T>(ref NativeArray<T> array, int length) where T : struct
        {
            if (array.IsCreated && array.Length >= length) return;
            if (array.IsCreated) array.Dispose();
            array = new NativeArray<T>(Mathf.Max(length, 1024) * 5 / 4, Allocator.Persistent);
        }
    }
}
