using GoldDigger;
using HarmonyLib;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Kollisionsnetz des Haufens nur im Höhenband um den Spieler. Das Spiel kocht bei jeder Änderung das Netz des ganzen
    /// Haufens neu (gemessen 26.10.2026: 32 ms bei 15 Etagen, 70 ms bei 25 Etagen, bis zu 12-mal je Sekunde) – mit
    /// 5 Mio. Münzen der Grund für 10 Bilder je Sekunde. Gebaut wird das Netz unverändert vom Spiel; der Mod reicht nur
    /// die Dreiecke im Band weiter. Weit entfernte Etagen brauchen keine Kollision, ihre Münzen bleiben im Modell.
    ///
    /// Zwei Wege, je nach Spielstand: Build 26290693 baut und kocht das Netz in RefreshCollision. Seit Build 26367772
    /// (03.11.2026) baut es das Netz in BuildCollisionMesh in ein zweites Netz und backt es im Hintergrund
    /// (RefreshCollisionInBackground); RefreshCollision läuft nur noch beim Laden und am Ende. Ein Prefix auf RefreshCollision
    /// griffe dort im laufenden Spiel nie – der ganze Haufen bekäme wieder ein Netz. Der Mod setzt deshalb dort an, wo das
    /// Netz entsteht, und lässt Backen und Tausch dem Spiel.
    /// </summary>
    internal static class PilePatches
    {
        /// <summary>Band relativ zur Spielerhöhe: tiefste Etage ist 16 m hoch, darüber genügt eine Etage Luft.</summary>
        private const float Below = 10f, Above = 8f, Shift = 3f;

        /// <summary>Spielstand mit Hintergrund-Backen (BuildCollisionMesh vorhanden).</summary>
        internal static bool Background;

        /// <summary>Altes Spiel: das zugewiesene Netz; neues Spiel: das Netz, in das gebaut und das danach gebacken wird.</summary>
        private static AccessTools.FieldRef<GranularCoinPile, Mesh> _mesh;
        private static AccessTools.FieldRef<GranularCoinPile, NativeList<int>> _triangles;
        private static AccessTools.FieldRef<GranularCoinPile, NativeList<Vector3>> _vertices;
        private static AccessTools.FieldRef<GranularCoinPile, NativeArray<int>> _coverage;
        private static AccessTools.FieldRef<GranularCoinPile, int> _revision;
        private static AccessTools.FieldRef<GranularCoinPile, float> _next;
        private static AccessTools.FieldRef<GranularCoinPile, int> _grid;
        private static AccessTools.FieldRef<GranularCoinPile, float> _pitch;

        /// <summary>Zeitmessung für den Leistungstest: Netz bauen, Dreiecke auswählen, an PhysX übergeben (ms, Aufrufe).</summary>
        internal static double JobMs, FilterMs, CookMs;
        internal static int Calls;

        private static Transform _player;
        private static float _bandCenter = float.NaN;
        // Verkürzte Schicht-Arrays nur mit den Schichten im Band (je Säule mindestens eine, weil der Job Nachbarn darüber findet).
        private static NativeArray<int> _starts, _counts, _columns;
        private static NativeArray<float> _bases, _ceilings;

        internal static void Apply(Harmony harmony)
        {
            var pile = typeof(GranularCoinPile);
            var build = AccessTools.Method(pile, "BuildCollisionMesh");
            Background = build != null;
            _mesh = AccessTools.FieldRefAccess<GranularCoinPile, Mesh>(Background ? "_pendingCollisionMesh" : "_collisionMesh");
            _triangles = AccessTools.FieldRefAccess<GranularCoinPile, NativeList<int>>("_surfaceTriangles");
            _vertices = AccessTools.FieldRefAccess<GranularCoinPile, NativeList<Vector3>>("_collisionVertices");
            _coverage = AccessTools.FieldRefAccess<GranularCoinPile, NativeArray<int>>("_triangleCoverage");
            _revision = AccessTools.FieldRefAccess<GranularCoinPile, int>("_collisionRevision");
            _next = AccessTools.FieldRefAccess<GranularCoinPile, float>("_nextCollision");
            _grid = AccessTools.FieldRefAccess<GranularCoinPile, int>("gridSize");
            _pitch = AccessTools.FieldRefAccess<GranularCoinPile, float>("pitch");
            if (Background)
                harmony.Patch(build, prefix: new HarmonyMethod(typeof(PilePatches), nameof(BuildCollisionMeshPrefix)));
            else
                harmony.Patch(AccessTools.Method(pile, "RefreshCollision"), prefix: new HarmonyMethod(typeof(PilePatches), nameof(RefreshCollisionPrefix)));
            harmony.Patch(AccessTools.Method(pile, "LateUpdate"), prefix: new HarmonyMethod(typeof(PilePatches), nameof(LateUpdatePrefix)));
            Plugin.Log.LogInfo(Background
                ? "Haufen-Kollision: Band um den Spieler, Netz gebaut in BuildCollisionMesh, gebacken vom Spiel im Hintergrund."
                : "Haufen-Kollision: Band um den Spieler, Netz gebaut und gekocht in RefreshCollision.");
        }

        /// <summary>
        /// Neues Spiel: nur das Band in das Zwischennetz bauen. Geprüft 03.11.2026 gegen „sofort backen wie bisher“ (Bergtest,
        /// je zwei Durchgänge abwechselnd): kein Unterschied – beide und die Kollision des Spiels selbst verschütten die Figur,
        /// wenn sie sich im 160-m-Berg senkrecht nach unten gräbt. Deshalb bleibt es beim Backen des Spiels (0,1 ms je Bild). Backen (im Hintergrund oder sofort) und Zuweisen macht das Spiel.
        /// Die Kochoptionen bleiben die des Spiels – gebacken wird nicht mehr im Bild, und Backen und Zuweisen müssen dieselben nutzen.
        /// </summary>
        private static bool BuildCollisionMeshPrefix(GranularCoinPile __instance)
        {
            if (!ModState.Active || Diagnostics.KeepOriginal || Bypass) return true;
            var model = __instance.Model;
            if (model == null || !model.IsCreated) return true;
            float? player = PlayerHeight(__instance);
            if (!player.HasValue) return true;

            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var vertices = _vertices(__instance);
            var triangles = _triangles(__instance);
            BuildBand(__instance, model, player.Value, vertices, triangles);
            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            var mesh = _mesh(__instance);
            mesh.Clear();
            mesh.SetVertices(vertices.AsArray());
            mesh.SetIndices(triangles.AsArray(), MeshTopology.Triangles, 0);
            // Mitte des Bands, für das dieses Netz gilt. Wandert der Spieler während des Backens weiter, baut LateUpdatePrefix
            // nach dem Tausch neu (das Spiel setzt die Revision beim Tausch auf den Stand beim Bauen).
            _bandCenter = player.Value;
            long t2 = System.Diagnostics.Stopwatch.GetTimestamp();
            double tick = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            JobMs += (t1 - t0) * tick; CookMs += (t2 - t1) * tick; Calls++;
            return false;
        }

        private static void BuildBand(GranularCoinPile pile, GranularPileModel model, float player, NativeList<Vector3> vertices, NativeList<int> triangles)
        {
            Compact(model, player - Below, player + Above);
            new BuildPileCollision
            {
                // Der Job läuft über die ganze Länge dieser Arrays: exakt zuschneiden.
                Starts = _starts.GetSubArray(0, model.ColumnLayerStarts.Length),
                Counts = _counts.GetSubArray(0, _layers),
                Columns = _columns.GetSubArray(0, _layers),
                Bases = _bases.GetSubArray(0, _layers),
                Ceilings = _ceilings.GetSubArray(0, _layers),
                Coverage = _coverage(pile),
                Vertices = vertices,
                Triangles = triangles,
                Size = _grid(pile),
                Pitch = _pitch(pile),
                Rise = model.PackedRise
            }.Schedule().Complete();
        }

        /// <summary>Wechselt der Spieler die Höhe um mehr als 4 m, wird das Netz beim nächsten Durchlauf neu gekocht.</summary>
        private static void LateUpdatePrefix(GranularCoinPile __instance)
        {
            if (!ModState.Active || Diagnostics.KeepOriginal || __instance.Model == null || !__instance.Model.IsCreated) return;
            float? y = PlayerHeight(__instance);
            PoseBand.SetPlayer(__instance.Model, y);
            if (y.HasValue && !float.IsNaN(_bandCenter) && Mathf.Abs(y.Value - _bandCenter) > Shift) _revision(__instance) = -1;
        }

        /// <summary>Nur für Tests: Kollision wieder wie das Spiel (ganzer Haufen).</summary>
        internal static bool Bypass;

        /// <summary>Netz beim nächsten Durchlauf neu bauen.</summary>
        internal static void Rebuild(GranularCoinPile pile) => _revision(pile) = -1;

        private static bool RefreshCollisionPrefix(GranularCoinPile __instance)
        {
            if (!ModState.Active || Diagnostics.KeepOriginal || Bypass) return true;
            var model = __instance.Model;
            if (model == null || !model.IsCreated) return true;
            float? player = PlayerHeight(__instance);
            if (!player.HasValue) return true;
            if (_revision(__instance) == model.Revision) return false;

            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            var vertices = _vertices(__instance);
            var triangles = _triangles(__instance);
            BuildBand(__instance, model, player.Value, vertices, triangles);

            long t1 = System.Diagnostics.Stopwatch.GetTimestamp();
            long t2 = t1;
            var mesh = _mesh(__instance);
            mesh.Clear();
            mesh.SetVertices(vertices.AsArray());
            mesh.SetIndices(triangles.AsArray(), MeshTopology.Triangles, 0);
            var collider = __instance.GetComponent<MeshCollider>();
            // Ohne „für schnellere Simulation kochen“: das Netz wird bis zu 12-mal je Sekunde neu übergeben.
            collider.cookingOptions = MeshColliderCookingOptions.UseFastMidphase;
            collider.sharedMesh = null;
            if (triangles.Length > 0) collider.sharedMesh = mesh;
            _revision(__instance) = model.Revision;
            _next(__instance) = Time.unscaledTime + 0.08f;
            _bandCenter = player.Value;
            long t3 = System.Diagnostics.Stopwatch.GetTimestamp();
            double tick = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            JobMs += (t1 - t0) * tick; FilterMs += (t2 - t1) * tick; CookMs += (t3 - t2) * tick; Calls++;
            return false;
        }

        private static unsafe void Compact(GranularPileModel model, float low, float high)
        {
            var starts = model.ColumnLayerStarts;
            int columns = starts.Length - 1, layers = model.LayerCount;
            Ensure(ref _starts, columns + 1);
            Ensure(ref _counts, layers);
            Ensure(ref _columns, layers);
            Ensure(ref _bases, layers);
            Ensure(ref _ceilings, layers);
            var gCounts = (int*)model.LayerCounts.GetUnsafeReadOnlyPtr();
            var gBases = (float*)model.LayerBases.GetUnsafeReadOnlyPtr();
            var gCeilings = (float*)model.LayerCeilings.GetUnsafeReadOnlyPtr();
            var lStarts = (int*)_starts.GetUnsafePtr();
            var lCounts = (int*)_counts.GetUnsafePtr();
            var lColumns = (int*)_columns.GetUnsafePtr();
            var lBases = (float*)_bases.GetUnsafePtr();
            var lCeilings = (float*)_ceilings.GetUnsafePtr();
            float rise = model.PackedRise;
            int n = 0;
            for (int c = 0; c < columns; c++)
            {
                int s0 = starts[c], s1 = starts[c + 1];
                int first = s0;
                while (first < s1 && gBases[first] + gCounts[first] * rise < low) first++;
                int end = first;
                while (end < s1 && gBases[end] <= high) end++;
                if (end == first)
                {
                    first = first > s0 ? first - 1 : s0;
                    end = first + 1;
                }
                lStarts[c] = n;
                int band = end - first;
                UnsafeUtility.MemCpy(lCounts + n, gCounts + first, band * sizeof(int));
                UnsafeUtility.MemCpy(lBases + n, gBases + first, band * sizeof(float));
                UnsafeUtility.MemCpy(lCeilings + n, gCeilings + first, band * sizeof(float));
                for (int j = 0; j < band; j++) lColumns[n + j] = c;
                n += band;
            }
            lStarts[columns] = n;
            _layers = n;
        }

        private static int _layers;

        private static void Ensure<T>(ref NativeArray<T> array, int length) where T : struct
        {
            if (array.IsCreated && array.Length >= length) return;
            if (array.IsCreated) array.Dispose();
            array = new NativeArray<T>(Mathf.Max(length, 1024), Allocator.Persistent);
        }

        /// <summary>Spielerhöhe im Raum des Haufens; null, solange es keinen Spieler gibt (dann rechnet das Spiel selbst).</summary>
        private static float? PlayerHeight(GranularCoinPile pile)
        {
            if (_player == null)
            {
                var controller = Object.FindAnyObjectByType<FirstPersonController>();
                if (controller == null) return null;
                _player = controller.transform;
            }
            return pile.transform.InverseTransformPoint(_player.position).y;
        }
    }
}
