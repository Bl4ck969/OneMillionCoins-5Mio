using System.Linq;
using GoldDigger;
using UnityEngine;

namespace OneMillionCoins5Mio
{
    /// <summary>Schreibt im Mod-Modus alle 10 s Bildrate, Haufen-Rechenzeit und gezeichnete Münzen ins Log.</summary>
    internal sealed class PerformanceLog : MonoBehaviour
    {
        private const float Interval = 10f;

        private GranularCoinPile _pile;
        private float _elapsed;
        private int _frames;
        private float _worstFrame;
        private float _pileMs;
        private float _worstPileMs;

        private void Start()
        {
            _pile = FindObjectsByType<GranularCoinPile>(FindObjectsInactive.Exclude).FirstOrDefault();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _elapsed += dt;
            _frames++;
            _worstFrame = Mathf.Max(_worstFrame, dt);
            if (_pile != null)
            {
                _pileMs += _pile.LastUpdateMilliseconds;
                _worstPileMs = Mathf.Max(_worstPileMs, _pile.LastUpdateMilliseconds);
            }
            if (_elapsed < Interval) return;

            string pile = _pile == null ? "kein Haufen" :
                $"Haufen {_pileMs / _frames:0.0} ms (max {_worstPileMs:0.0}), {_pile.RenderedInstances:N0} Münzen in {_pile.RenderedBatches} Paketen, Rest {_pile.Model.Remaining:N0}";
            Plugin.Log.LogInfo($"Leistung ({(ModState.Active ? "5 Mio." : "Original")}): {_frames / _elapsed:0} FPS (langsamstes Bild {_worstFrame * 1000f:0} ms), {pile}");
            _elapsed = 0f;
            _frames = 0;
            _worstFrame = 0f;
            _pileMs = 0f;
            _worstPileMs = 0f;
        }
    }

    /// <summary>
    /// Leistungstest (Prüfmodus "leistung"): gräbt 60 s lang wie ein Spieler mit vollem Baum von oben ab – je Bild an
    /// vier zufälligen Säulen die oberste Münzschicht –, damit Fließen und Darstellung arbeiten. Danach Spielende.
    /// Nimmt Münzen nur aus dem Haufen, nichts wird gutgeschrieben oder gespeichert.
    /// </summary>
    internal sealed class LoadTest : MonoBehaviour
    {
        internal static bool Requested => Plugin.CheckMode.Value == "leistung" || Plugin.CheckMode.Value == "leistung-original";

        // Zeit je Teil des Haufens (Fließen, Lagen, Kollision, Darstellung), per Harmony gemessen, nur in diesem Test.
        private static readonly (System.Type type, string name)[] Parts =
        {
            (typeof(GranularPileModel), "TickInventory"), (typeof(GranularPileModel), "UpdatePoses"), (typeof(GranularCoinPile), "RefreshCollision"),
            (typeof(GranularCoinPile), "RefreshInstances"), (typeof(GranularFlowSystem), "Pose"), (typeof(GranularFlowSystem), "Sides"),
        };
        private static readonly double[] PartMs = new double[6];
        private static readonly int[] PartCalls = new int[6];
        private static HarmonyLib.Harmony _probe;

        private static void Measure()
        {
            _probe = new HarmonyLib.Harmony("de.bl4ck969.onemillioncoins.5mio.leistung");
            for (int i = 0; i < Parts.Length; i++)
            {
                var method = HarmonyLib.AccessTools.Method(Parts[i].type, PartName(i));
                _probe.Patch(method, prefix: new HarmonyLib.HarmonyMethod(typeof(LoadTest), nameof(Before)),
                    postfix: new HarmonyLib.HarmonyMethod(typeof(LoadTest), "After" + i));
            }
        }

        /// <summary>Seit Build 26367772 läuft die Kollision im Spiel über RefreshCollisionInBackground (PilePatches).</summary>
        private static string PartName(int i) =>
            Parts[i].name == "RefreshCollision" && PilePatches.Background ? "RefreshCollisionInBackground" : Parts[i].name;

        private static void Before(out long __state) => __state = System.Diagnostics.Stopwatch.GetTimestamp();
        private static void Done(int i, long start)
        {
            PartMs[i] += (System.Diagnostics.Stopwatch.GetTimestamp() - start) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            PartCalls[i]++;
        }
        private static void After0(long __state) => Done(0, __state);
        private static void After1(long __state) => Done(1, __state);
        private static void After2(long __state) => Done(2, __state);
        private static void After3(long __state) => Done(3, __state);
        private static void After4(long __state) => Done(4, __state);
        private static void After5(long __state) => Done(5, __state);

        private readonly System.Collections.Generic.List<GranularCoinPose> _feedback = new System.Collections.Generic.List<GranularCoinPose>();

        private System.Collections.IEnumerator Start()
        {
            yield return new WaitForSecondsRealtime(5f);
            var pile = FindObjectsByType<GranularCoinPile>(FindObjectsInactive.Exclude).FirstOrDefault();
            if (pile == null) { Application.Quit(); yield break; }
            var model = pile.Model;
            int start = model.Remaining;
            Measure();
            var player = FindAnyObjectByType<FirstPersonController>()?.transform;
            int frames = 0;
            yield return CompareBand(pile, player, "start");
            float until = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < until)
            {
                for (int k = 0; k < 4; k++)
                {
                    int column = Random.Range(0, model.Counts.Length);
                    int top = -1;
                    for (int layer = model.LayerStart(column); layer < model.LayerEnd(column); layer++)
                        if (model.LayerCounts[layer] > 0) top = layer;
                    if (top < 0) continue;
                    var c = model.LayerCenter(top);
                    _feedback.Clear();
                    pile.Extract(pile.transform.TransformPoint(new Vector3(c.x, c.y, c.z)), 1.5f, 60, _feedback);
                }
                // Der Spieler geht mit nach unten: alle 2 s auf die höchste Münzoberfläche, wie beim echten Abgraben.
                // Ohne Münzen gibt es keine Oberfläche: Spieler dann stehen lassen (minus unendlich ließ PhysX abstürzen, 26.10.2026).
                if (frames % 120 == 0 && player != null && model.Remaining > 0)
                {
                    float highest = float.NegativeInfinity;
                    for (int layer = 0; layer < model.LayerCount; layer++)
                        if (model.LayerCounts[layer] > 0) highest = Mathf.Max(highest, model.LayerCenter(layer).y);
                    if (float.IsInfinity(highest)) highest = 0f;
                    var controller = player.GetComponent<CharacterController>();
                    if (controller != null) controller.enabled = false;
                    var p = player.position;
                    player.position = new Vector3(p.x, pile.transform.TransformPoint(0f, highest, 0f).y + 1f, p.z);
                    if (controller != null) controller.enabled = true;
                }
                frames++;
                yield return null;
            }
            for (int i = 0; i < Parts.Length; i++)
                Plugin.Log.LogInfo($"Leistungstest {PartName(i)}: {PartMs[i] / frames:0.0} ms je Bild, {PartCalls[i] / (float)frames:0.00} Aufrufe je Bild, {PartMs[i] / Mathf.Max(1, PartCalls[i]):0.0} ms je Aufruf");
            _probe.UnpatchSelf();
            Plugin.Log.LogInfo($"Leistungstest Kollision (Band): {PilePatches.Calls / (float)frames:0.00} Aufrufe je Bild, je Aufruf " +
                $"Netz {PilePatches.JobMs / Mathf.Max(1, PilePatches.Calls):0.0} ms, Auswahl {PilePatches.FilterMs / Mathf.Max(1, PilePatches.Calls):0.0} ms, " +
                (PilePatches.Background ? "Übergabe ans Netz (gebacken wird im Hintergrund) " : "PhysX ") + $"{PilePatches.CookMs / Mathf.Max(1, PilePatches.Calls):0.0} ms");
            yield return CompareBand(pile, player, "ende");
            yield return StandOnMountain(pile, player, false);
            yield return StandOnMountain(pile, player, true);
            Plugin.Log.LogInfo($"Leistungstest fertig: {start - model.Remaining:N0} Münzen in 60 s abgegraben, Rest {model.Remaining:N0}.");
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }

        /// <summary>
        /// Bl4ck969s Fall (26.10.2026): mitten auf dem Münzberg im Lichthof stehen und unter den Füßen abbauen. 15 s lang
        /// wird unter dem Spieler gegraben; protokolliert werden Sprünge nach oben. Einmal mit Kollisionsband, einmal wie das Spiel.
        /// </summary>
        private System.Collections.IEnumerator StandOnMountain(GranularCoinPile pile, Transform player, bool original)
        {
            if (player == null) yield break;
            var model = pile.Model;
            PilePatches.Bypass = original;
            PilePatches.Rebuild(pile);
            // Offene Säule (ohne Decke) mit Münzen, die der Gebäudemitte am nächsten liegt – nicht die höchste. Der zweite
            // Durchgang beginnt deshalb meist unten im Schacht des ersten, und dessen Wände rutschen nach (03.11.2026).
            int best = -1;
            float bestScore = float.MaxValue;
            for (int layer = 0; layer < model.LayerCount; layer++)
            {
                if (model.LayerCounts[layer] < 20 || !float.IsPositiveInfinity(model.LayerCeiling(layer))) continue;
                var c = model.LayerCenter(layer);
                var w = pile.transform.TransformPoint(new Vector3(c.x, c.y, c.z));
                float score = w.x * w.x + w.z * w.z;
                if (score < bestScore) { bestScore = score; best = layer; }
            }
            if (best < 0) { Plugin.Log.LogInfo("Bergtest: keine offene Säule gefunden."); yield break; }
            var top = model.LayerCenter(best);
            var spot = pile.transform.TransformPoint(new Vector3(top.x, top.y, top.z));
            var controller = player.GetComponent<CharacterController>();
            controller.enabled = false;
            player.position = spot + Vector3.up * 1.2f;
            controller.enabled = true;
            yield return new WaitForSecondsRealtime(1f);
            float startY = player.position.y, last = startY, maxJump = 0f;
            int jumps = 0, sunk = 0, frames = 0, sunkTop = 0, overfull = 0, grounded = 0;
            float deepest = 0f, deepestTop = 0f, fastestFall = 0f;
            var jumpLines = new System.Collections.Generic.List<string>();
            float until = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < until)
            {
                var feet = player.position + Vector3.down * 1.2f;
                _feedback.Clear();
                pile.Extract(feet + Vector3.down * 0.4f, 2.2f, 150, _feedback);
                yield return null;
                float y = player.position.y;
                // Versunken: Füße mehr als 0,5 m unter der Münzoberfläche der Säule darunter.
                var local = pile.transform.InverseTransformPoint(player.position);
                if (model.TryCell(local, out int cell))
                {
                    float sole = pile.transform.InverseTransformPoint(controller.bounds.min).y;
                    for (int layer = model.LayerStart(cell); layer < model.LayerEnd(cell); layer++)
                    {
                        if (model.LayerCounts[layer] == 0 || model.LayerBase(layer) > sole) continue;
                        float surface = model.LayerBase(layer) + model.LayerCounts[layer] * model.PackedRise;
                        if (surface - sole > 0.5f) { sunk++; deepest = Mathf.Max(deepest, surface - sole); }
                        // Schicht mit rechnerischer Oberfläche über ihrer Decke: zählt oben mit, ohne dass Münzen über der Figur liegen.
                        if (surface > model.LayerCeiling(layer) + 0.5f) overfull++;
                    }
                    // Physikalisch entscheidend ist die oberste Schicht, auf der die Figur steht: liegt ihre Oberfläche über der Sohle?
                    int topLayer = model.LayerEnd(cell) - 1;
                    if (topLayer >= model.LayerStart(cell) && model.LayerCounts[topLayer] > 0 && model.LayerBase(topLayer) <= sole)
                    {
                        float surface = model.LayerBase(topLayer) + model.LayerCounts[topLayer] * model.PackedRise;
                        if (surface - sole > 0.5f) { sunkTop++; deepestTop = Mathf.Max(deepestTop, surface - sole); }
                    }
                }
                if (controller.isGrounded) grounded++;
                fastestFall = Mathf.Min(fastestFall, (y - last) / Mathf.Max(Time.unscaledDeltaTime, 0.0001f));
                if (y - last > 0.5f)
                {
                    jumps++;
                    maxJump = Mathf.Max(maxJump, y - last);
                    if (jumpLines.Count < 8) jumpLines.Add($"{last:0.0}->{y:0.0}");
                }
                last = y;
                frames++;
            }
            Plugin.Log.LogInfo($"Bergtest ({(original ? "Kollision wie Spiel" : "Kollisionsband")}): Start y {startY:0.0} bei x {spot.x:0.0} z {spot.z:0.0}, Ende y {last:0.0}, " +
                $"{jumps} Sprünge nach oben (größter {maxJump:0.0} m) {string.Join(" ", jumpLines)}, {sunk} von {frames} Bildern im Haufen versunken (tiefstens {deepest:0.0} m); " +
                $"unter der obersten Schicht {sunkTop} (tiefstens {deepestTop:0.0} m), Schichten über ihrer Decke {overfull}, " +
                $"auf Boden {grounded} von {frames} Bildern, schnellster Fall {-fastestFall:0.0} m/s");
            PilePatches.Bypass = false;
            PilePatches.Rebuild(pile);
        }

        /// <summary>
        /// Bildvergleich: Aus Spielersicht in sechs Richtungen einmal mit Band (PoseBand) und einmal mit allen Schichten
        /// aufnehmen und abweichende Pixel zählen. Fehlen sichtbare Münzen, unterscheiden sich die Bilder.
        /// </summary>
        private System.Collections.IEnumerator CompareBand(GranularCoinPile pile, Transform player, string moment)
        {
            if (player == null) yield break;
            string folder = System.IO.Path.Combine(Plugin.CheckFolder.Value, "leistung");
            System.IO.Directory.CreateDirectory(folder);
            float scale = Time.timeScale;
            Time.timeScale = 0f;
            var main = Camera.main;
            var go = new GameObject("Vergleichskamera");
            var cam = go.AddComponent<Camera>();
            if (main != null) { cam.CopyFrom(main); cam.depth = main.depth + 10f; }
            var looks = new[] { (0f, 0f), (0f, 90f), (0f, 180f), (0f, 270f), (50f, 45f), (50f, 225f) };
            int worst = 0;
            var lines = new System.Collections.Generic.List<string>();
            foreach (var (pitch, yaw) in looks)
            {
                cam.transform.SetPositionAndRotation(player.position + Vector3.up * 1.6f, Quaternion.Euler(pitch, yaw, 0f));
                var shots = new Texture2D[2];
                for (int pass = 0; pass < 2; pass++)
                {
                    PoseBand.Bypass = pass == 1;
                    PoseBand.Repose(pile.Model);
                    pile.RefreshInstances();
                    yield return null;
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    shots[pass] = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                    shots[pass].ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                    shots[pass].Apply();
                }
                var a = shots[0].GetPixels32();
                var b = shots[1].GetPixels32();
                int differ = 0;
                for (int i = 0; i < a.Length; i++)
                    if (Mathf.Abs(a[i].r - b[i].r) > 24 || Mathf.Abs(a[i].g - b[i].g) > 24 || Mathf.Abs(a[i].b - b[i].b) > 24) differ++;
                string name = $"{moment}_{pitch:0}_{yaw:0}";
                lines.Add($"{name}: {differ} Pixel");
                worst = Mathf.Max(worst, differ);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + "_band.png"), shots[0].EncodeToPNG());
                if (differ > 0) System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, name + "_alle.png"), shots[1].EncodeToPNG());
                Destroy(shots[0]);
                Destroy(shots[1]);
            }
            PoseBand.Bypass = false;
            PoseBand.Repose(pile.Model);
            Destroy(go);
            Time.timeScale = scale;
            Plugin.Log.LogInfo($"Bandvergleich {moment} (Spieler y {player.position.y:0.0}, {Screen.width}x{Screen.height}): höchstens {worst} abweichende Pixel – " + string.Join(", ", lines));
        }
    }
}
