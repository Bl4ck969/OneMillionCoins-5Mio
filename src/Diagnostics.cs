using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using GoldDigger;
using HarmonyLib;
using UnityEngine;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Prüfmodus: nimmt das Gebäude ohne Haufen aus festen Blickwinkeln auf (Schnitte und Etagenansichten),
    /// schreibt eine Bauteilliste und eine Aufzugs-/Stationsprüfung und beendet das Spiel danach.
    /// Läuft nur im Mod-Modus (offline). "original" lässt das Gebäude unverändert und dient als Vergleich.
    /// </summary>
    internal sealed class Diagnostics : MonoBehaviour
    {
        private string _folder;
        private readonly StringBuilder _report = new StringBuilder();

        internal static bool Requested => Plugin.CheckMode.Value == "original" || Plugin.CheckMode.Value == "mod" || Plugin.CheckMode.Value == "inventar";

        /// <summary>Gebäude bleibt unverändert (Vergleichsaufnahme oder Inventar).</summary>
        internal static bool KeepOriginal => Plugin.CheckMode.Value == "original" || Plugin.CheckMode.Value == "inventar" || Plugin.CheckMode.Value == "leistung-original";

        private IEnumerator Start()
        {
            _folder = Path.Combine(Plugin.CheckFolder.Value, Plugin.CheckMode.Value);
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
            Directory.CreateDirectory(_folder);

            yield return new WaitForSecondsRealtime(4f);
            var pile = FindObjectsByType<GranularCoinPile>(FindObjectsInactive.Exclude).FirstOrDefault();
            if (pile != null) pile.ManualRendering = true;
            var hud = FindObjectsByType<GoldDiggerHUD>(FindObjectsInactive.Exclude).FirstOrDefault();
            if (hud != null) hud.enabled = false;
            yield return null;

            try
            {
                if (Plugin.CheckMode.Value == "inventar")
                {
                    RunInventory();
                }
                else
                {
                    WriteParts();
                    File.WriteAllLines(Path.Combine(_folder, "verzerrt.txt"), FloorBuilder.Distorted, new UTF8Encoding(false));
                    _report.AppendLine($"Verzerrte Teile: {FloorBuilder.Distorted.Count}");
                    CheckElevatorAndStations();
                    var elevatorForCheck = FindObjectsByType<CargoElevator>(FindObjectsInactive.Include).FirstOrDefault();
                    CheckInvisibleColliders(Traverse.Create(elevatorForCheck).Field<float[]>("layerHeights").Value);
                    CheckStairs(Traverse.Create(elevatorForCheck).Field<float[]>("layerHeights").Value);
                    foreach (var line in FloorBuilder.Plan) _report.AppendLine(line);
                    CheckSolid();
                    CheckShaftColumns();
                    Capture();
                    CheckBlockReasons();
                    CheckElevatorEmpty();
                    CheckElevatorRides();
                    CheckCabinPanel();
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("Prüfmodus: " + e);
                _report.AppendLine("FEHLER: " + e);
            }
            // Aufzugsmenü des HUD öffnen und den Bildschirm aufnehmen (Haufen ist nach dem Aufzugstest leer, alle Etagen frei).
            if (hud != null && Plugin.CheckMode.Value != "inventar")
            {
                hud.enabled = true;
                Traverse.Create(hud).Field("_elevatorOpen").SetValue(true);
                yield return null;
                yield return null;
                yield return new WaitForEndOfFrame();
                var shot = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
                shot.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
                shot.Apply();
                File.WriteAllBytes(Path.Combine(_folder, "aufzugsmenue.png"), shot.EncodeToPNG());
                Destroy(shot);
                _report.AppendLine($"Aufzugsmenü aufgenommen ({Screen.width}x{Screen.height})");
            }
            File.WriteAllText(Path.Combine(_folder, "pruefbericht.txt"), _report.ToString(), new UTF8Encoding(false));
            Plugin.Log.LogInfo($"Prüfmodus fertig: {_folder}");
            yield return new WaitForSecondsRealtime(1f);
            Application.Quit();
        }

        private static string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        private static string PathOf(Transform t)
        {
            var names = new List<string>();
            for (; t != null; t = t.parent) names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
        }

        /// <summary>Alle sichtbaren Renderer und festen Collider mit Welt-Grenzen (CSV).</summary>
        private void WriteParts()
        {
            var csv = new StringBuilder("art;pfad;minx;miny;minz;maxx;maxy;maxz;rotation\n");
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (!r.enabled || r.GetComponentInParent<GranularCoinPile>() != null || r.GetComponentInParent<FirstPersonController>() != null) continue;
                var b = r.bounds;
                csv.Append($"R;{PathOf(r.transform)};{F(b.min.x)};{F(b.min.y)};{F(b.min.z)};{F(b.max.x)};{F(b.max.y)};{F(b.max.z)};{r.transform.rotation.eulerAngles}\n");
            }
            foreach (var c in FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            {
                if (!c.enabled || c.isTrigger || c.GetComponentInParent<GranularCoinPile>() != null || c.GetComponentInParent<FirstPersonController>() != null) continue;
                var b = c.bounds;
                csv.Append($"C;{PathOf(c.transform)};{F(b.min.x)};{F(b.min.y)};{F(b.min.z)};{F(b.max.x)};{F(b.max.y)};{F(b.max.z)};{c.transform.rotation.eulerAngles}\n");
            }
            File.WriteAllText(Path.Combine(_folder, "bauteile.csv"), csv.ToString(), new UTF8Encoding(false));
        }

        /// <summary>Passen Ruftaster, Landetore und Stationen zu den Etagenhöhen des Aufzugs?</summary>
        private void CheckElevatorAndStations()
        {
            var elevator = FindObjectsByType<CargoElevator>(FindObjectsInactive.Include).FirstOrDefault();
            var heights = elevator == null ? new float[0] : Traverse.Create(elevator).Field<float[]>("layerHeights").Value;
            _report.AppendLine($"Etagenhöhen (Index von unten): {string.Join(" / ", heights.Select(F))}");
            var car = elevator == null ? null : Traverse.Create(elevator).Field<Transform>("movingCar").Value;
            _report.AppendLine($"Aufzug: aktuelle Etage {elevator?.CurrentFloor}, Kabine y {F(car != null ? car.position.y : 0f)}");

            int Nearest(float y)
            {
                int best = 0;
                for (int i = 1; i < heights.Length; i++) if (Mathf.Abs(heights[i] - y) < Mathf.Abs(heights[best] - y)) best = i;
                return best;
            }

            foreach (var control in FindObjectsByType<ElevatorControl>(FindObjectsInactive.Include).OrderBy(c => c.transform.position.y))
            {
                int floor = Traverse.Create(control).Field<int>("floor").Value;
                bool call = Traverse.Create(control).Field<bool>("landingCall").Value;
                float y = control.transform.position.y;
                string where = call ? $"steht auf Etage {Nearest(y - 1.5f)}" : "in der Kabine";
                string flag = call && Nearest(y - 1.5f) != floor ? "  <-- FALSCH" : "";
                _report.AppendLine($"Taster {(call ? "Ruf " : "Kabine")} Index {floor} ({elevator?.GetLayerName(floor)}) y {F(y)} {where}{flag}  [{PathOf(control.transform)}]");
            }
            foreach (var gate in FindObjectsByType<ElevatorLandingGate>(FindObjectsInactive.Include).OrderBy(g => g.transform.position.y))
            {
                int floor = Traverse.Create(gate).Field<int>("floor").Value;
                float y = gate.transform.position.y;
                string flag = Nearest(y) != floor ? "  <-- FALSCH" : "";
                _report.AppendLine($"Landetor Index {floor} y {F(y)} Etage nach Höhe {Nearest(y)}{flag}");
            }
            foreach (var machine in FindObjectsByType<DepositMachine>(FindObjectsInactive.Include).OrderByDescending(m => m.transform.position.y))
            {
                float y = machine.transform.position.y;
                int fromTop = heights.Length - 1 - Nearest(y - 0.9f);
                string flag = fromTop != machine.RequiredStationRank ? "  <-- FALSCH" : "";
                _report.AppendLine($"Station Rang {machine.RequiredStationRank} y {F(y)} Etage von oben {fromTop}{flag}");
            }
        }

        private void Capture()
        {
            var main = Camera.main;
            var go = new GameObject("Prüfkamera");
            var cam = go.AddComponent<Camera>();
            if (main != null) cam.CopyFrom(main);
            cam.enabled = false;

            // Gebäudegrenzen aus den vier Außenwänden.
            var walls = BuildingWalls();
            var box = walls.Count > 0 ? walls.Aggregate((a, b) => { a.Encapsulate(b); return a; }) : new Bounds(new Vector3(0, 25, 0), new Vector3(30, 50, 30));
            _report.AppendLine($"Gebäude: x {F(box.min.x)}..{F(box.max.x)}  y {F(box.min.y)}..{F(box.max.y)}  z {F(box.min.z)}..{F(box.max.z)}");
            var elevator = FindObjectsByType<CargoElevator>(FindObjectsInactive.Include).FirstOrDefault();
            var heights = elevator == null ? new float[0] : Traverse.Create(elevator).Field<float[]>("layerHeights").Value;

            // Schnitte: orthografisch von außen, die nahe Wand und alles davor weggeschnitten.
            float h = box.size.y;
            var dirs = new (string name, Vector3 forward)[] { ("nord", Vector3.forward), ("ost", Vector3.right), ("sued", Vector3.back), ("west", Vector3.left) };
            foreach (var (name, forward) in dirs)
            {
                float halfDepth = Mathf.Abs(Vector3.Dot(box.extents, forward)) ;
                float halfWidth = Mathf.Abs(Vector3.Dot(box.extents, Vector3.Cross(Vector3.up, forward)));
                // Schnittebene relativ zur Mitte: -0,85 = kurz hinter der nahen Wand, 0 = Mitte, 0,5 = hintere Hälfte.
                foreach (var (cut, tag) in new[] { (-0.85f, "vorn"), (0f, "mitte"), (0.5f, "hinten") })
                {
                    var position = box.center - forward * (halfDepth + 20f);
                    cam.orthographic = true;
                    cam.orthographicSize = h * 0.5f + 1f;
                    cam.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward));
                    cam.nearClipPlane = 20f + halfDepth * (1f + cut);
                    cam.farClipPlane = 20f + halfDepth * 2f + 5f;
                    int width = 900;
                    int height = Mathf.Clamp(Mathf.RoundToInt(width * (h + 2f) / (halfWidth * 2f + 2f)), 600, 3600);
                    Render(cam, width, height, $"schnitt_{name}_{tag}.png");
                }
            }

            // Etagenansichten: aus der Schachtmitte, 1,7 m über jedem Boden, in alle vier Richtungen.
            cam.orthographic = false;
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 300f;
            for (int i = heights.Length - 1; i >= 0; i--)
            {
                string label = (heights.Length - 1 - i).ToString("00");
                foreach (var (name, forward) in dirs)
                {
                    cam.transform.SetPositionAndRotation(new Vector3(box.center.x, heights[i] + 1.7f, box.center.z), Quaternion.LookRotation(forward));
                    Render(cam, 1100, 620, $"etage_{label}_{name}.png");
                }
            }
            // Blick nach oben vom Boden und nach unten unter der Decke.
            cam.fieldOfView = 100f;
            cam.transform.SetPositionAndRotation(new Vector3(box.center.x, heights.FirstOrDefault() + 1.7f, box.center.z), Quaternion.LookRotation(Vector3.up, Vector3.forward));
            Render(cam, 1000, 1000, "blick_nach_oben.png");
            cam.transform.SetPositionAndRotation(new Vector3(box.center.x, box.max.y - 3f, box.center.z), Quaternion.LookRotation(Vector3.down, Vector3.forward));
            Render(cam, 1000, 1000, "blick_nach_unten.png");

            // Löcher nach draußen: Hintergrund magenta statt Himmel, ohne Nebel. Jedes Magenta-Pixel aus dem Inneren ist ein Loch.
            var marker = new Color32(255, 0, 255, 255);
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = marker;
            cam.fieldOfView = 100f;
            // Sichtweite der Spielkamera: Was dahinter liegt, zeichnet sie als Himmel (Deckenloch 26.10.2026).
            cam.farClipPlane = main != null ? main.farClipPlane : 1000f;
            _report.AppendLine($"Sichtweite der Spielkamera: {F(cam.farClipPlane)} m");
            var looks = new (string name, Quaternion rotation)[] { ("nord", Quaternion.LookRotation(Vector3.forward)), ("ost", Quaternion.LookRotation(Vector3.right)),
                ("sued", Quaternion.LookRotation(Vector3.back)), ("west", Quaternion.LookRotation(Vector3.left)),
                ("oben", Quaternion.LookRotation(Vector3.up, Vector3.forward)), ("unten", Quaternion.LookRotation(Vector3.down, Vector3.forward)) };
            var spots = new (string name, Vector2 offset)[] { ("mitte", Vector2.zero), ("nw", new Vector2(-0.6f, 0.6f)), ("no", new Vector2(0.6f, 0.6f)), ("sw", new Vector2(-0.6f, -0.6f)), ("so", new Vector2(0.6f, -0.6f)) };
            int total = 0;
            var found = new List<string>();
            for (int i = heights.Length - 1; i >= 0; i--)
            {
                string label = (heights.Length - 1 - i).ToString("00");
                foreach (var (spot, offset) in spots)
                    foreach (var (look, rotation) in looks)
                    {
                        var position = new Vector3(box.center.x + offset.x * box.extents.x, heights[i] + 1.7f, box.center.z + offset.y * box.extents.z);
                        cam.transform.SetPositionAndRotation(position, rotation);
                        int hits = Render(cam, 256, 256, $"loch_etage_{label}_{spot}_{look}.png", marker);
                        if (hits == 0) continue;
                        total += hits;
                        found.Add($"{label} {spot} {look}: {hits}");
                    }
            }
            // Gegenprobe: über dem Dach in den Himmel muss fast alles Magenta sein, sonst greift der Test nicht.
            cam.transform.SetPositionAndRotation(new Vector3(box.center.x, box.max.y + 60f, box.center.z), Quaternion.LookRotation(Vector3.up, Vector3.forward));
            int control = Render(cam, 256, 256, "loch_gegenprobe.png", marker);
            RenderSettings.fog = fog;
            _report.AppendLine($"Loch-Test Gegenprobe (Himmel über dem Dach): {control} von {256 * 256} Pixel Magenta");
            _report.AppendLine($"Löcher nach draußen: {total} Pixel in {found.Count} Ansichten" + (found.Count > 0 ? " (" + string.Join("; ", found) + ")" : ""));
            Destroy(go);
        }

        /// <summary>
        /// Unsichtbare Hindernisse: feste Kollider, in deren Raum (3 cm Toleranz) kein einziger sichtbarer Eckpunkt liegt.
        /// Läuft für Original und Mod; im Original gibt es solche Kollider absichtlich (Geländer-Schutz), der Vergleich zählt.
        /// </summary>
        private void CheckInvisibleColliders(float[] heights)
        {
            const float cell = 0.5f;
            var grid = new Dictionary<(int, int, int), List<Vector3>>();
            void Add(Vector3 p)
            {
                var key = (Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell), Mathf.FloorToInt(p.z / cell));
                if (!grid.TryGetValue(key, out var list)) grid[key] = list = new List<Vector3>();
                list.Add(p);
            }
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            {
                if (!r.enabled || r.GetComponentInParent<GranularCoinPile>() != null || r.GetComponentInParent<FirstPersonController>() != null) continue;
                var mesh = r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh != null && mesh.isReadable)
                {
                    var m = r.transform.localToWorldMatrix;
                    foreach (var v in mesh.vertices) Add(m.MultiplyPoint3x4(v));
                }
                else
                {
                    var b = r.bounds;
                    Add(b.center);
                    for (int i = 0; i < 8; i++) Add(new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z));
                }
            }
            var csv = new StringBuilder("etage;pfad;minx;miny;minz;maxx;maxy;maxz\n");
            var perFloor = new SortedDictionary<string, int>();
            int total = 0;
            foreach (var c in FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
            {
                if (!c.enabled || c.isTrigger || c.GetComponentInParent<GranularCoinPile>() != null || c.GetComponentInParent<FirstPersonController>() != null || c is CharacterController) continue;
                var b = c.bounds;
                b.Expand(0.06f);
                bool visible = false;
                for (int x = Mathf.FloorToInt(b.min.x / cell); x <= Mathf.FloorToInt(b.max.x / cell) && !visible; x++)
                    for (int yy = Mathf.FloorToInt(b.min.y / cell); yy <= Mathf.FloorToInt(b.max.y / cell) && !visible; yy++)
                        for (int z = Mathf.FloorToInt(b.min.z / cell); z <= Mathf.FloorToInt(b.max.z / cell) && !visible; z++)
                            if (grid.TryGetValue((x, yy, z), out var list))
                                foreach (var p in list) if (b.Contains(p)) { visible = true; break; }
                if (visible) continue;
                total++;
                int below = 0;
                for (int i = 0; i < heights.Length; i++) if (heights[i] <= b.center.y + 0.3f) below = i;
                string floor = (heights.Length - 1 - below).ToString("00");
                perFloor[floor] = perFloor.TryGetValue(floor, out int n) ? n + 1 : 1;
                csv.Append($"{floor};{PathOf(c.transform)};{F(b.min.x)};{F(b.min.y)};{F(b.min.z)};{F(b.max.x)};{F(b.max.y)};{F(b.max.z)}\n");
            }
            File.WriteAllText(Path.Combine(_folder, "unsichtbare_kollider.csv"), csv.ToString(), new UTF8Encoding(false));
            _report.AppendLine($"Unsichtbare Kollider: {total} ({string.Join(", ", perFloor.Select(p => $"{p.Key}: {p.Value}"))})");
        }

        /// <summary>
        /// Begehbarkeit: Je Etage die Oberkanten aller Treppenstufen und Podeste (Kollider). Unterste Stufe und oberste
        /// müssen an die Decks anschließen, kein Schritt darf höher sein als die Steighöhe der Spielfigur.
        /// Dazu je Deck die Pooltreppe: oberste Stufe direkt unter dem Deck.
        /// </summary>
        private void CheckStairs(float[] heights)
        {
            var controller = FindObjectsByType<CharacterController>(FindObjectsInactive.Include).FirstOrDefault();
            float stepOffset = controller != null ? controller.stepOffset : 0.3f;
            var colliders = FindObjectsByType<BoxCollider>(FindObjectsInactive.Exclude).Where(c => c.enabled && !c.isTrigger).ToList();
            var treads = colliders.Where(c => c.name.StartsWith("Tread") || c.name.StartsWith("Rest landing")).Select(c => c.bounds.max.y).ToList();
            var pool = colliders.Where(c => c.name.StartsWith("Pool entry tread")).Select(c => c.bounds.max.y).ToList();
            _report.AppendLine($"Treppen (Steighöhe der Spielfigur {F(stepOffset)} m):");
            int problems = 0;
            for (int i = 0; i + 1 < heights.Length; i++)
            {
                float floor = heights[i], next = heights[i + 1];
                var tops = treads.Where(t => t > floor - 0.5f && t <= next + 0.05f).OrderBy(t => t).ToList();
                string label = (heights.Length - 1 - i).ToString("00");
                if (tops.Count == 0) { _report.AppendLine($"  Etage {label}: KEINE STUFEN"); problems++; continue; }
                var steps = new List<float> { tops[0] - floor };
                for (int k = 1; k < tops.Count; k++) steps.Add(tops[k] - tops[k - 1]);
                steps.Add(next - tops[tops.Count - 1]);
                float worst = steps.Max();
                bool bad = worst > stepOffset + 0.01f;
                if (bad) problems++;
                _report.AppendLine($"  Etage {label}: {tops.Count} Stufen/Podeste, unterste {F(tops[0] - floor)} m, oberste {F(tops[tops.Count - 1] - next)} m zum nächsten Deck, größter Schritt {F(worst)} m{(bad ? "  <-- ZU HOCH" : "")}");
            }
            for (int i = 1; i < heights.Length; i++)
            {
                var below = pool.Where(t => t <= heights[i] + 0.05f && t > heights[i] - 3f).OrderByDescending(t => t).ToList();
                if (below.Count == 0) continue;
                string label = (heights.Length - 1 - i).ToString("00");
                _report.AppendLine($"  Pooltreppe unter Deck {label}: {below.Count} Stufen, oberste {F(below[0] - heights[i])} m, unterste {F(below[below.Count - 1] - heights[i])} m");
            }
            _report.AppendLine($"Treppen mit Problemen: {problems}");
        }

        /// <summary>
        /// Aufzug beim Abgraben: Etage für Etage von oben alle Münzen ab dieser Etage aufwärts per Code entfernen (nur im
        /// Prüfmodus, ohne Wertung), dann prüfen, ob der Aufzug die Etage anfahren darf. Bei Sperre die Münzschichten nennen,
        /// die Schacht oder Landeplatz belegen, genau nach der Regel von GranularCoinPile.IsVolumeClear.
        /// </summary>
        private static readonly System.Reflection.MethodInfo Tick = AccessTools.Method(typeof(GranularPileModel), "TickInventory");

        /// <summary>
        /// Wie beim Spielen bis zu einem Boden abgraben: die Etage ganz absaugen, Durchgehendes (Schacht, Landeplatz) bis 0,3 m
        /// unter den Boden ("excavate its shaft and landing"), und die Münzen dazwischen nachrutschen lassen, bis nichts mehr fließt.
        /// </summary>
        internal static int DigTo(GranularCoinPile pile, float floorY)
        {
            var model = pile.Model;
            float floorLocal = pile.transform.InverseTransformPoint(new Vector3(0f, floorY, 0f)).y;
            int rounds = 0;
            for (bool busy = true; busy && rounds < 4000; rounds++)
            {
                busy = false;
                for (int layer = 0; layer < model.LayerCount; layer++)
                {
                    bool open = float.IsPositiveInfinity(model.LayerCeiling(layer));
                    if (!open && model.LayerBase(layer) >= floorLocal - 0.5f)
                        while (model.ExtractLayerTop(layer, out _)) busy = true;
                    else if (open)
                        while (model.LayerCounts[layer] > 0 && model.LayerCenter(layer).y > floorLocal - 0.3f && model.ExtractLayerTop(layer, out _)) busy = true;
                }
                Tick.Invoke(model, null);
                if (model.LastMoved > 0) busy = true;
            }
            return rounds;
        }

        private void CheckElevatorEmpty()
        {
            var elevator = FindObjectsByType<CargoElevator>(FindObjectsInactive.Include).FirstOrDefault();
            var pile = FindObjectsByType<GranularCoinPile>(FindObjectsInactive.Exclude).FirstOrDefault();
            if (elevator == null || pile == null) return;
            var model = pile.Model;
            var car = Traverse.Create(elevator).Field<Transform>("movingCar").Value;
            _report.AppendLine($"Aufzugstest beim Abgraben (Kabine auf y {F(car.position.y)}):");
            var order = Enumerable.Range(0, elevator.LayerCount).OrderByDescending(i => elevator.FloorPosition(i).y).ToList();
            int blocked = 0;
            foreach (int i in order)
            {
                float floorY = elevator.FloorPosition(i).y;
                int rounds = DigTo(pile, floorY);
                bool accessible = elevator.IsLayerAccessible(i);
                bool landingClear = elevator.IsLandingClear(i);
                // Was steht körperlich im Schacht über dieser Etage (ohne Aufzug selbst)? Im Original nur Schachtrahmen.
                var inShaft = Physics.OverlapBox(new Vector3(car.position.x, floorY + 1.4f, car.position.z), new Vector3(1.6f, 1.2f, 1.6f))
                    .Where(c => !c.isTrigger && c.GetComponentInParent<CargoElevator>() == null && c.GetComponentInParent<GranularCoinPile>() == null && c.GetComponentInParent<FirstPersonController>() == null)
                    .Select(c => c.name).Distinct().OrderBy(n => n).ToList();
                if (inShaft.Count > 0) _report.AppendLine($"    Im Schacht über Etage {elevator.GetLayerName(i)}: {string.Join(", ", inShaft)}");
                if (accessible)
                {
                    _report.AppendLine($"  Etage {elevator.GetLayerName(i)} (y {F(floorY)}): erreichbar ({rounds} Runden, Rest {model.Remaining:N0})");
                    continue;
                }
                blocked++;
                float bottom = Mathf.Min(floorY, car.position.y) - 0.24f, top = Mathf.Max(floorY, car.position.y) + 3.4f;
                var volume = new Bounds(new Vector3(car.position.x, (bottom + top) * 0.5f, car.position.z), new Vector3(3.65f, top - bottom, 3.65f));
                volume.SetMinMax(new Vector3(volume.min.x, Mathf.Max(pile.transform.position.y + 0.14f, volume.min.y), volume.min.z), volume.max);
                _report.AppendLine($"  Etage {elevator.GetLayerName(i)} (y {F(floorY)}): GESPERRT nach {rounds} Runden, Landeplatz frei {landingClear}, Schacht frei {pile.IsVolumeClear(volume)}, Rest {model.Remaining:N0} Münzen");
                foreach (var (name, box) in new[] { ("Schacht", volume), ("Landeplatz", elevator.LandingClearance(i)) })
                {
                    var lo = pile.transform.InverseTransformPoint(box.min);
                    var hi = pile.transform.InverseTransformPoint(box.max);
                    if (lo.x < -model.Width * 0.5f || hi.x > model.Width * 0.5f || lo.z < -model.Depth * 0.5f || hi.z > model.Depth * 0.5f)
                        _report.AppendLine($"    {name}: liegt außerhalb des Haufens ({F(lo.x)}..{F(hi.x)} / {F(lo.z)}..{F(hi.z)}, Haufen ±{F(model.Width * 0.5f)} / ±{F(model.Depth * 0.5f)})");
                    int shown = 0;
                    for (int layer = 0; layer < model.LayerCount && shown < 6; layer++)
                    {
                        if (model.LayerCounts[layer] == 0 || model.LayerBase(layer) >= hi.y) continue;
                        Vector3 c = model.LayerCenter(layer);
                        if (c.x < lo.x - model.Pitch || c.x > hi.x + model.Pitch || c.z < lo.z - model.RowPitch || c.z > hi.z + model.RowPitch || c.y + 0.015f <= lo.y) continue;
                        shown++;
                        var w = pile.transform.TransformPoint(c);
                        _report.AppendLine($"    {name}: Schicht {layer} bei {F(w.x)} / {F(w.y)} / {F(w.z)}, Boden y {F(pile.transform.TransformPoint(0f, model.LayerBase(layer), 0f).y)}, Decke y {F(pile.transform.TransformPoint(0f, model.LayerCeiling(layer), 0f).y)}, {model.LayerCounts[layer]} Münzen, Oberfläche {F(w.y - floorY)} m über dem Etagenboden");
                    }
                }
            }
            _report.AppendLine($"Aufzug: {elevator.LayerCount - blocked} von {elevator.LayerCount} Etagen beim Abgraben erreichbar");
        }

        /// <summary>
        /// Durchlaufstellen: Punkte, die im Original in einem festen Kollider liegen, müssen es an ihrer neuen Lage auch tun.
        /// Der Haufen zählt dabei nicht (er ist im Prüfmodus voll und würde Lücken verdecken).
        /// </summary>
        private void CheckSolid()
        {
            if (FloorBuilder.SolidChecks.Count == 0) return;
            var pile = FindObjectsByType<GranularCoinPile>(FindObjectsInactive.Exclude).FirstOrDefault();
            var pileCollider = pile != null ? pile.GetComponent<MeshCollider>() : null;
            bool pileWasOn = pileCollider != null && pileCollider.enabled;
            if (pileCollider != null) pileCollider.enabled = false;
            Physics.SyncTransforms();
            var groups = new Dictionary<(string, string), (int open, int total, Vector3 sample, string holder)>();
            var byHolder = new Dictionary<string, int>();
            int open = 0;
            foreach (var (where, part, point, holder) in FloorBuilder.SolidChecks)
            {
                bool solid = Physics.OverlapSphere(point, 0.06f, ~0, QueryTriggerInteraction.Ignore).Any(c => !(c is CharacterController));
                groups.TryGetValue((where, part), out var g);
                g.total++;
                if (!solid)
                {
                    g.open++; g.sample = point; g.holder = holder; open++;
                    byHolder.TryGetValue(holder, out int n);
                    byHolder[holder] = n + 1;
                }
                groups[(where, part)] = g;
            }
            if (pileCollider != null) pileCollider.enabled = pileWasOn;
            var bad = groups.Where(kv => kv.Value.open >= 2 && kv.Value.open * 4 >= kv.Value.total).OrderByDescending(kv => kv.Value.open).ToList();
            _report.AppendLine($"Feste Punkte ohne Kollider: {open} von {FloorBuilder.SolidChecks.Count}; Teile mit Durchlaufstellen: {bad.Count}");
            var lines = bad.Select(kv => $"{kv.Key.Item1}\t{kv.Value.open}/{kv.Value.total}\tbei {F(kv.Value.sample.x)} / {F(kv.Value.sample.y)} / {F(kv.Value.sample.z)}\t{kv.Key.Item2}\tim Original gehalten von {kv.Value.holder}").ToList();
            File.WriteAllLines(Path.Combine(_folder, "durchlauf_nach_kollider.txt"),
                byHolder.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Value}\t{kv.Key}"), new UTF8Encoding(false));
            File.WriteAllLines(Path.Combine(_folder, "durchlaufstellen.txt"), lines, new UTF8Encoding(false));
            foreach (var line in lines.Take(25)) _report.AppendLine("  " + line);
            // Maße der Handlauf-Kästen (Original abgeschaltet, Mod-Kopien aktiv), zum Vergleich.
            var rails = FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(c => c.name == "Continuous handrail")
                .OrderBy(c => c.enabled).ThenBy(c => c.bounds.min.y).ToList();
            File.WriteAllLines(Path.Combine(_folder, "handlauf_kaesten.txt"), rails.Select(c =>
                $"{(c.enabled ? "Mod" : "Original")}\tGröße {F(c.size.x * c.transform.lossyScale.x)} x {F(c.size.y * c.transform.lossyScale.y)} x {F(c.size.z * c.transform.lossyScale.z)}" +
                $"\tMitte {F(c.bounds.center.x)} / {F(c.bounds.center.y)} / {F(c.bounds.center.z)}\tHülle y {F(c.bounds.min.y)}..{F(c.bounds.max.y)} x {F(c.bounds.min.x)}..{F(c.bounds.max.x)} z {F(c.bounds.min.z)}..{F(c.bounds.max.z)}" +
                $"\tDrehung {F(c.transform.eulerAngles.x)} / {F(c.transform.eulerAngles.y)} / {F(c.transform.eulerAngles.z)}"), new UTF8Encoding(false));
        }

        /// <summary>Sperrgründe im Aufzugsmenü bei vollem Haufen: Zusatz je gesperrter Etage, dazu das erste volle Schachtstück Richtung Boden.</summary>
        private void CheckBlockReasons()
        {
            var elevator = FindObjectsByType<CargoElevator>(FindObjectsInactive.Include).FirstOrDefault();
            if (elevator == null) return;
            int shaft = ElevatorPatches.BlockedShaftFloor(elevator, 0);
            _report.AppendLine($"Sperrgründe bei vollem Haufen (Kabine auf {elevator.GetLayerName(elevator.CurrentFloor)}, erstes volles Schachtstück Richtung Boden bei {(shaft >= 0 ? elevator.GetLayerName(shaft) : "keinem")}):");
            var blocked = Enumerable.Range(0, elevator.LayerCount).Reverse().Where(i => i != elevator.CurrentFloor && !elevator.IsLayerAccessible(i));
            _report.AppendLine("  " + string.Join(" | ", blocked.Select(i => elevator.GetLayerName(i) + ElevatorPatches.BlockReason(elevator, i))));
        }

        /// <summary>
        /// Aufzugsfahrten, mit dem Spiel selbst durchgerechnet (Tick in 20-ms-Schritten; der Haufen ist nach dem Aufzugstest leer):
        /// Strecke, Fahrzeit ohne Tore, Spitzengeschwindigkeit und Zeit, bis 90 % davon erreicht sind.
        /// </summary>
        private void CheckElevatorRides()
        {
            var elevator = FindObjectsByType<CargoElevator>(FindObjectsInactive.Include).FirstOrDefault();
            if (elevator == null || elevator.Car == null) return;
            const float step = 0.02f;
            int top = elevator.LayerCount - 1;
            _report.AppendLine($"Aufzugsfahrten (Höchstgeschwindigkeit {F(Plugin.LiftSpeed.Value)} m/s, Anfahrzeit {F(Plugin.LiftRamp.Value)} s):");
            foreach (int target in new[] { top, top - 1, top / 2, 0 })
            {
                int from = elevator.CurrentFloor;
                if (target == from) continue;
                string ride = $"{elevator.GetLayerName(from)} -> {elevator.GetLayerName(target)}";
                float distance = Mathf.Abs(elevator.GetLayerHeight(target) - elevator.GetLayerHeight(from));
                if (!elevator.CallTo(target))
                {
                    _report.AppendLine($"  {ride}: nicht gestartet");
                    continue;
                }
                var speeds = new List<float>();
                float previous = elevator.Car.position.y;
                for (int i = 0; elevator.IsMoving && i < 60000; i++)
                {
                    elevator.Tick(step);
                    float y = elevator.Car.position.y;
                    speeds.Add(Mathf.Abs(y - previous) / step);
                    previous = y;
                }
                float peak = speeds.DefaultIfEmpty(0f).Max();
                int start = speeds.FindIndex(v => v > 0.001f);
                float moving = speeds.Count(v => v > 0.001f) * step;
                float rampUp = (speeds.FindIndex(v => v >= 0.9f * peak) - start + 1) * step;
                _report.AppendLine($"  {ride}: {F(distance)} m in {F(moving)} s, Spitze {F(peak)} m/s, 90 % davon nach {F(rampUp)} s");
            }
        }

        /// <summary>Kabinentasten auf einer mittleren Etage: Beschriftung und Hinweis je Platz in Leserichtung, dazu ein Bild der Tafel.</summary>
        private void CheckCabinPanel()
        {
            var elevator = FindObjectsByType<CargoElevator>(FindObjectsInactive.Include).FirstOrDefault();
            if (elevator == null || elevator.Car == null) return;
            int middle = elevator.LayerCount / 2;
            if (elevator.CurrentFloor != middle && elevator.CallTo(middle))
                for (int i = 0; elevator.IsMoving && i < 60000; i++) elevator.Tick(0.02f);
            var cabin = ElevatorPatches.CabinInReadingOrder(elevator);
            foreach (var c in cabin) c.Refresh();
            _report.AppendLine($"Kabinentasten auf Etage {elevator.GetLayerName(elevator.CurrentFloor)} (Leserichtung): " +
                string.Join(" | ", cabin.Select(c => $"{Traverse.Create(c).Field<TextMesh>("label").Value?.text} \"{c.Prompt}\"")));
            if (cabin.Count == 0) return;
            var center = cabin.Aggregate(Vector3.zero, (sum, c) => sum + c.transform.position) / cabin.Count;
            var toCar = elevator.Car.position - center;
            toCar.y = 0f;
            var go = new GameObject("Prüfkamera");
            var cam = go.AddComponent<Camera>();
            if (Camera.main != null) cam.CopyFrom(Camera.main);
            cam.enabled = false;
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.05f;
            go.transform.position = center + (toCar.sqrMagnitude > 0.01f ? toCar.normalized : Vector3.forward) * 2.2f;
            go.transform.LookAt(center);
            Render(cam, 1280, 720, "kabine.png");
            Destroy(go);
            // Menütaste drücken: Das Aufzugsmenü des HUD muss sich öffnen (danach wieder zu).
            var hud = FindObjectsByType<GoldDiggerHUD>(FindObjectsInactive.Include).FirstOrDefault();
            if (hud == null || cabin.Count != 6) return;
            var open = Traverse.Create(hud).Field<bool>("_elevatorOpen");
            open.Value = false;
            cabin[1].Interact();
            _report.AppendLine($"Menütaste öffnet das Aufzugsmenü: {open.Value}");
            open.Value = false;
        }

        /// <summary>
        /// Münzschichten in den Säulen von Schacht und Landeplatz (je Bereich: Boden/Decke und Zahl der Säulen). Im Original
        /// sind dort nur durchgehende Säulen; Absätze dazwischen kann der Spieler vom Landeplatz aus nicht leeren.
        /// </summary>
        private void CheckShaftColumns()
        {
            var elevator = FindObjectsByType<CargoElevator>(FindObjectsInactive.Include).FirstOrDefault();
            var pile = FindObjectsByType<GranularCoinPile>(FindObjectsInactive.Exclude).FirstOrDefault();
            if (elevator == null || pile == null) return;
            var model = pile.Model;
            var car = Traverse.Create(elevator).Field<Transform>("movingCar").Value;
            var landing = elevator.LandingClearance(0);
            var regions = new[] { ("Schacht", new Bounds(car.position, new Vector3(3.65f, 1f, 3.65f))), ("Landeplatz", landing) };
            foreach (var (name, region) in regions)
            {
                var keys = new SortedDictionary<string, int>();
                for (int c = 0; c < model.Counts.Length; c++)
                {
                    var center = pile.transform.TransformPoint(model.Center(c));
                    if (center.x < region.min.x - model.Pitch || center.x > region.max.x + model.Pitch || center.z < region.min.z - model.RowPitch || center.z > region.max.z + model.RowPitch) continue;
                    for (int layer = model.LayerStart(c); layer < model.LayerEnd(c); layer++)
                    {
                        float bottom = pile.transform.TransformPoint(0f, model.LayerBase(layer), 0f).y;
                        float ceiling = model.LayerCeiling(layer);
                        string top = float.IsPositiveInfinity(ceiling) ? "offen" : F(pile.transform.TransformPoint(0f, ceiling, 0f).y);
                        string key = $"Boden {bottom,7:0.00}  Decke {top}";
                        keys.TryGetValue(key, out int n);
                        keys[key] = n + 1;
                    }
                }
                _report.AppendLine($"Münzschichten im Bereich {name} (Säulen je Schicht):");
                foreach (var kv in keys) _report.AppendLine($"  {kv.Key}: {kv.Value}");
            }
        }

        /// <summary>Teile-Inventar des Originals: Liste, Zusammenfassung und Schnitte eingefärbt nach Baustein und Rolle.</summary>
        private void RunInventory()
        {
            var elevator = FindObjectsByType<CargoElevator>(FindObjectsInactive.Include).First();
            var heights = Traverse.Create(elevator).Field<float[]>("layerHeights").Value;
            var floorY = heights.Reverse().ToArray();
            var car = Traverse.Create(elevator).Field<Transform>("movingCar").Value;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var inv = new Inventory(gameObject.scene, floorY);
            _report.AppendLine($"Inventar in {watch.ElapsedMilliseconds} ms: {inv.SplitRenderers.Count} Meshes -> {inv.Islands.Count} Inseln, {inv.Colliders.Count} Kollider, {inv.Rigids.Count} Funktionsobjekte, Stufenpunkte {string.Join(" ", inv.StairPoints.OrderBy(p => p.Key).Select(p => $"B{p.Key}:{p.Value.Count}"))}");

            string ModuleName(int m) => m == -1 ? "durchgehend" : m == -2 ? "basis" : m.ToString("00");
            var csv = new StringBuilder("art;quelle;material;baustein;rolle;dreiecke;minx;miny;minz;maxx;maxy;maxz\n");
            foreach (var i in inv.Islands)
            {
                var b = i.Bounds;
                var mat = i.Renderer.sharedMaterials.Length > i.SubMesh ? i.Renderer.sharedMaterials[i.SubMesh]?.name : "";
                csv.Append($"{(i.ModuleFixed ? "IG" : "I")};{PathOf(i.Renderer.transform)};{mat};{ModuleName(i.Module)};{i.Anchor};{i.Triangles.Length / 3};{F(b.min.x)};{F(b.min.y)};{F(b.min.z)};{F(b.max.x)};{F(b.max.y)};{F(b.max.z)}\n");
            }
            foreach (var c in inv.Colliders)
            {
                var b = c.Bounds;
                csv.Append($"C;{PathOf(c.Collider.transform)};;{ModuleName(c.Module)};{c.Anchor};0;{F(b.min.x)};{F(b.min.y)};{F(b.min.z)};{F(b.max.x)};{F(b.max.y)};{F(b.max.z)}\n");
            }
            foreach (var r in inv.Rigids)
            {
                var b = r.Bounds;
                csv.Append($"F;{PathOf(r.Root)};;{ModuleName(r.Module)};{r.Anchor};0;{F(b.min.x)};{F(b.min.y)};{F(b.min.z)};{F(b.max.x)};{F(b.max.y)};{F(b.max.z)}\n");
            }
            File.WriteAllText(Path.Combine(_folder, "inventar.csv"), csv.ToString(), new UTF8Encoding(false));

            // Zusammenfassung: Anzahl je Baustein und Rolle.
            foreach (var group in inv.Islands.GroupBy(i => i.Module).OrderBy(g => g.Key))
            {
                _report.AppendLine($"Baustein {ModuleName(group.Key)}: Inseln {group.Count()} ({string.Join(", ", group.GroupBy(i => i.Anchor).Select(a => $"{a.Key} {a.Count()}"))}), " +
                    $"Kollider {inv.Colliders.Count(c => c.Module == group.Key)}, Funktionsobjekte {inv.Rigids.Count(r => r.Module == group.Key)}");
            }
            _report.AppendLine("Durchgehend: " + string.Join("; ", inv.Islands.Where(i => i.Module == -1).Select(i => $"{i.Renderer.name}[{F(i.Bounds.min.y)}..{F(i.Bounds.max.y)}]").Take(60)));

            // Eingefärbte Schnitte.
            var palette = new[] { Color.red, new Color(1f, 0.55f, 0f), Color.yellow, Color.green, Color.cyan, new Color(0.3f, 0.4f, 1f) };
            Color ByModule(Island i) => i.Module >= 0 ? palette[i.Module] : i.Module == -1 ? Color.white : Color.gray;
            Color ByAnchor(Island i) => i.Anchor switch
            {
                Anchor.Floor => new Color(0.3f, 0.4f, 1f), Anchor.Ceiling => Color.red, Anchor.Proportional => Color.yellow,
                Anchor.Stretch => Color.green, _ => new Color(1f, 0.3f, 1f),
            };
            foreach (var r in inv.SplitRenderers) r.enabled = false;
            RenderColored(inv, ByModule, "baustein");
            RenderColored(inv, ByAnchor, "rolle");
            foreach (var r in inv.SplitRenderers) r.enabled = true;
        }

        private void RenderColored(Inventory inv, System.Func<Island, Color> color, string prefix)
        {
            var template = inv.SplitRenderers.First().sharedMaterial;
            var objects = new List<GameObject>();
            foreach (var group in inv.Islands.GroupBy(color))
            {
                var vertices = new List<Vector3>();
                var triangles = new List<int>();
                var cache = new Dictionary<MeshRenderer, Vector3[]>();
                foreach (var island in group)
                {
                    if (!cache.TryGetValue(island.Renderer, out var world))
                    {
                        var mesh = island.Renderer.GetComponent<MeshFilter>().sharedMesh;
                        var m = island.Renderer.transform.localToWorldMatrix;
                        world = mesh.vertices.Select(v => m.MultiplyPoint3x4(v)).ToArray();
                        cache[island.Renderer] = world;
                    }
                    var map = new Dictionary<int, int>();
                    foreach (int idx in island.Triangles)
                    {
                        if (!map.TryGetValue(idx, out int n)) { map[idx] = n = vertices.Count; vertices.Add(world[idx]); }
                        triangles.Add(n);
                    }
                }
                var colored = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                colored.SetVertices(vertices);
                colored.SetTriangles(triangles, 0);
                colored.RecalculateNormals();
                var go = new GameObject("Inventar " + prefix);
                go.AddComponent<MeshFilter>().sharedMesh = colored;
                var material = new Material(template);
                foreach (var tex in new[] { "_BaseMap", "_MainTex", "_BumpMap", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap" })
                    if (material.HasProperty(tex)) material.SetTexture(tex, null);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", group.Key);
                if (material.HasProperty("_Color")) material.SetColor("_Color", group.Key);
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                objects.Add(go);
            }
            RenderSections(prefix);
            foreach (var go in objects) Destroy(go);
        }

        private void RenderSections(string prefix)
        {
            var go = new GameObject("Prüfkamera");
            var cam = go.AddComponent<Camera>();
            if (Camera.main != null) cam.CopyFrom(Camera.main);
            cam.enabled = false;
            // Im Inventar-Modus sind die Original-Mauern für die Einfärbung abgeschaltet; dann die Maße des Originals.
            var walls = BuildingWalls();
            var box = walls.Count > 0 ? walls.Aggregate((a, b) => { a.Encapsulate(b); return a; }) : new Bounds(new Vector3(0, 25, 0), new Vector3(30, 50, 30));
            float h = box.size.y;
            foreach (var (name, forward) in new[] { ("nord", Vector3.forward), ("ost", Vector3.right), ("sued", Vector3.back), ("west", Vector3.left) })
            {
                float halfDepth = Mathf.Abs(Vector3.Dot(box.extents, forward));
                float halfWidth = Mathf.Abs(Vector3.Dot(box.extents, Vector3.Cross(Vector3.up, forward)));
                foreach (var (cut, tag) in new[] { (-0.85f, "vorn"), (0f, "mitte") })
                {
                    cam.orthographic = true;
                    cam.orthographicSize = h * 0.5f + 1f;
                    cam.transform.SetPositionAndRotation(box.center - forward * (halfDepth + 20f), Quaternion.LookRotation(forward));
                    cam.nearClipPlane = 20f + halfDepth * (1f + cut);
                    cam.farClipPlane = 20f + halfDepth * 2f + 5f;
                    int width = 900;
                    int height = Mathf.Clamp(Mathf.RoundToInt(width * (h + 2f) / (halfWidth * 2f + 2f)), 600, 3600);
                    Render(cam, width, height, $"{prefix}_{name}_{tag}.png");
                }
            }
            Destroy(go);
        }

        /// <summary>Außenwände: im Original die vier "masonry"-Objekte, im Mod die neu aufgebauten durchgehenden Meshes.</summary>
        private static List<Bounds> BuildingWalls() =>
            FindObjectsByType<Renderer>(FindObjectsInactive.Exclude)
                .Where(r => r.enabled && (r.name.EndsWith("masonry") || r.name.StartsWith("Durchgehend")))
                .Select(r => r.bounds).ToList();

        private int Render(Camera cam, int width, int height, string file) => Render(cam, width, height, file, null);

        /// <summary>Rendert in eine PNG-Datei; mit Markenfarbe werden deren Pixel gezählt und nur bei Treffern gespeichert.</summary>
        private int Render(Camera cam, int width, int height, string file, Color32? marker)
        {
            var rt = new RenderTexture(width, height, 24);
            cam.targetTexture = rt;
            cam.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            cam.targetTexture = null;
            int hits = 0;
            if (marker is Color32 m)
                foreach (var px in tex.GetPixels32())
                    if (px.r == m.r && px.g == m.g && px.b == m.b) hits++;
            if (marker == null || hits > 0) File.WriteAllBytes(Path.Combine(_folder, file), tex.EncodeToPNG());
            Destroy(tex);
            rt.Release();
            Destroy(rt);
            return hits;
        }
    }
}
