using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GoldDigger;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OneMillionCoins5Mio
{
    /// <summary>Wie ein Teil bei einer höheren oder niedrigeren Etage mitgeht.</summary>
    internal enum Anchor
    {
        Floor,          // sitzt am Etagenboden (Deck, Geländer, Stationen): bleibt
        Ceiling,        // hängt am nächsten Deck (Träger, Pooltreppe von oben): wandert ganz mit
        Proportional,   // dazwischen (Lampen, Bänder): wandert anteilig
        Stretch,        // reicht vom Boden bis unter die Decke (Schienen, Rahmen): wird gestreckt
        Stair,          // an einer Treppe: folgt der Stufenlinie, Form bleibt
    }

    /// <summary>Ein zusammenhängendes Stück sichtbarer Geometrie aus einem (Sammel-)Mesh.</summary>
    internal sealed class Island
    {
        internal MeshRenderer Renderer;
        internal int SubMesh;
        internal int[] Triangles;      // Indizes in das Original-Mesh
        internal Bounds Bounds;        // Welt
        internal int Module = -1;      // Etage nach Schild (0 = oben ... 5 = Boden); -1 = durchgehend, -2 = Basis
        internal bool ModuleFixed;     // beim Trennen an einer Etagengrenze vergeben, nicht neu berechnen
        internal Anchor Anchor;
        internal Vector3[] Points;     // Welt-Eckpunkte (für Kollider-Partner und deren Verschiebung)
        internal bool StairOwned;      // gehört zu einer Treppe (Stufen, Wangen, Handläufe, Podeste) und folgt deren Stufenlinie
        internal Vector3[] SolidSamples = new Vector3[0];   // Prüfhilfe: Eckpunkte, die im Original in einem festen Kollider liegen
        internal string[] SolidHolders = new string[0];     // Prüfhilfe: der Kollider, der den Punkt im Original hält
    }

    /// <summary>Fester Kollisionskörper ohne Funktion.</summary>
    internal sealed class StaticCollider
    {
        internal Collider Collider;
        internal Bounds Bounds;
        internal int Module = -1;
        internal Anchor Anchor;
        internal Island Partner;       // sichtbares Teil am selben Ort; der Kollider geht mit dessen Regel mit
        internal bool Fixed;           // Baustein und Rolle stehen fest (Treppenteil, Deckplatte, Teil eines Aufzugshalts), der Partner ändert nichts
        internal Rigid Holder;         // Lampenhalter: geht genau wie seine Lampe mit
    }

    /// <summary>
    /// Objekt mit Spiel-Logik (Station, Taster, Werkbank, Lampe ...): wird als Ganzes kopiert und verschoben.
    /// Eine Hülle (Aufzugshalt mit Landetor) wird ebenfalls als Ganzes kopiert, ihre statischen Teile
    /// werden aber wie das übrige Gebäude neu aufgebaut und in der Hülle abgeschaltet.
    /// </summary>
    internal sealed class Rigid
    {
        internal Transform Root;
        internal Bounds Bounds;
        internal int Module = -1;
        internal Anchor Anchor;
        internal bool Shell;
        internal bool InShell;         // liegt in einer Hülle und geht mit ihr mit
    }

    /// <summary>
    /// Teile-Inventar des Original-Gebäudes. Zerlegt alle statischen Meshes in zusammenhängende Inseln,
    /// erfasst Kollider und Funktionsobjekte und ordnet alles einer Original-Etage ("Baustein") und einer Rolle zu.
    /// Baustein N reicht vom Deck N bis einschließlich Unterseite und Pooltreppe des Decks darüber.
    /// </summary>
    internal sealed class Inventory
    {
        internal const string Building = "Excavation Bunker";

        internal readonly float[] FloorY;   // nach Schild: [0] = oben ... [5] = Boden
        internal readonly List<Island> Islands = new List<Island>();
        internal readonly List<StaticCollider> Colliders = new List<StaticCollider>();
        internal readonly List<Rigid> Rigids = new List<Rigid>();
        internal readonly Dictionary<int, List<Vector3>> StairPoints = new Dictionary<int, List<Vector3>>();
        internal readonly Dictionary<int, List<Bounds>> StairBoxes = new Dictionary<int, List<Bounds>>();   // je Stufenpunkt dessen Kasten
        internal readonly Dictionary<int, List<Bounds>> Landings = new Dictionary<int, List<Bounds>>();
        internal readonly Dictionary<int, List<Bounds>> DeckSlabs = new Dictionary<int, List<Bounds>>();   // je Deck (Schild) seine Platten
        internal readonly HashSet<MeshRenderer> SplitRenderers = new HashSet<MeshRenderer>();
        internal readonly HashSet<Collider> StaticColliderSet = new HashSet<Collider>();

        private static readonly HashSet<string> SkipRoots = new HashSet<string> { "HUD", "Game Systems", "Player", "Atlas Cargo Lift" };
        private static readonly Regex StairGroup = new Regex(@"^Stair connection (\d) to (\d)$");
        private static readonly Regex DeckSlab = new Regex(@"^Joined deck (\d\d)$");
        private const float StairReach = 1.5f;     // horizontaler Abstand zur nächsten Stufe, ab dem ein Punkt zur Treppe gehört
        private const float StairEdge = 0.25f;     // ... oder zum Rand ihrer Trittfläche (Ecken breiter Stufen liegen weiter als 1,5 m von der Mitte)
        private readonly Dictionary<Transform, int> _shellModule = new Dictionary<Transform, int>();   // Aufzugshalt -> Baustein

        internal Inventory(Scene scene, float[] floorYFromTop)
        {
            FloorY = floorYFromTop;
            var roots = scene.GetRootGameObjects()
                .Where(r => !SkipRoots.Contains(r.name) && r.GetComponentInChildren<GranularCoinPile>(true) == null).ToList();

            // 1. Funktionsobjekte. Alles außerhalb des Gebäudes (Stationen, Werkbänke, Tresortür, Shop, Deko) bleibt ganz.
            //    Im Gebäude: Spiel-Logik, Lichter, die Schranke der Landetore. Landetor-Halter sind Hüllen.
            var rigidRoots = new HashSet<Transform>();
            var shells = new HashSet<Transform>();
            foreach (var root in roots)
            {
                if (root.name != Building)
                {
                    if (root.GetComponentsInChildren<Renderer>(true).Length > 0 || root.GetComponentsInChildren<Light>(true).Length > 0)
                        rigidRoots.Add(root.transform);
                    continue;
                }
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null || mb is BunkerLayoutManifest) continue;
                    if (mb is ElevatorLandingGate gate)
                    {
                        shells.Add(gate.transform);
                        var barrier = Traverse.Create(gate).Field<Transform>("barrier").Value;
                        if (barrier != null) rigidRoots.Add(barrier);
                        continue;
                    }
                    if (mb.GetType().Namespace == "GoldDigger") rigidRoots.Add(mb.transform);
                }
                foreach (var light in root.GetComponentsInChildren<Light>(true)) rigidRoots.Add(LampOf(light.transform));
            }
            var outer = rigidRoots.Where(t => !rigidRoots.Any(o => o != t && t.IsChildOf(o))).ToList();
            foreach (var t in outer)
            {
                Rigids.Add(new Rigid { Root = t, Bounds = TreeBounds(t), InShell = shells.Any(s => t.IsChildOf(s)) });
            }
            foreach (var s in shells)
            {
                Rigids.Add(new Rigid { Root = s, Bounds = TreeBounds(s), Shell = true });
                _shellModule[s] = RigidModuleOf(TreeBounds(s));
            }
            bool InRigid(Transform t) => outer.Any(t.IsChildOf);

            // 2. Stufenpunkte je Baustein aus den Treppen-Kollidern ("Stair connection i+1 to i" gehört zu Baustein 5-i).
            foreach (var c in roots.SelectMany(r => r.GetComponentsInChildren<BoxCollider>(true)))
            {
                int module = StairModuleOf(c);
                if (module < 0 || !(c.name.StartsWith("Tread") || c.name.StartsWith("Rest landing"))) continue;
                if (!StairPoints.TryGetValue(module, out var list))
                {
                    StairPoints[module] = list = new List<Vector3>();
                    StairBoxes[module] = new List<Bounds>();
                }
                var center = c.transform.TransformPoint(c.center);
                list.Add(new Vector3(center.x, c.bounds.max.y, center.z));
                StairBoxes[module].Add(c.bounds);
                if (c.name.StartsWith("Rest landing"))
                {
                    if (!Landings.TryGetValue(module, out var landings)) Landings[module] = landings = new List<Bounds>();
                    landings.Add(c.bounds);
                }
            }

            // 3. Statische Meshes in Inseln zerlegen.
            foreach (var renderer in roots.SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(true)))
            {
                if (InRigid(renderer.transform)) continue;
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter != null ? filter.sharedMesh : null;
                if (mesh == null || !mesh.isReadable || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                SplitRenderers.Add(renderer);
                Split(renderer, mesh);
            }

            // 4. Statische Kollider.
            foreach (var c in roots.SelectMany(r => r.GetComponentsInChildren<Collider>(true)))
            {
                if (c.isTrigger || !c.enabled || !c.gameObject.activeInHierarchy || InRigid(c.transform)) continue;
                Colliders.Add(new StaticCollider { Collider = c, Bounds = c.bounds });
                StaticColliderSet.Add(c);
            }

            foreach (var i in Islands)
            {
                if (!i.ModuleFixed) i.Module = ModuleOf(i.Bounds);
                int shellModule = -1;
                bool shellPart = !i.StairOwned && InShell(i.Renderer.transform, out shellModule);
                if (shellPart) i.Module = shellModule;
                i.Anchor = i.StairOwned ? Anchor.Stair : AnchorOf(i.Bounds, i.Module, shellPart);
            }
            foreach (var c in Colliders)
            {
                c.Module = ModuleOf(c.Bounds);
                if (c.Module == -1 && !IsContinuousShape(c.Bounds, c.Collider.name)) c.Module = ModuleOf(new Bounds(c.Bounds.center, Vector3.zero));
                c.Anchor = AnchorOf(c.Bounds, c.Module);
                // Ein Aufzugshalt ist eine Baugruppe seiner Etage. Über den Partner hing z. B. ein Pfosten an Baustein 5 und fehlte
                // auf den Etagen 14–22 (Prüfung „feste Punkte“, 29.10.2026).
                if (InShell(c.Collider.transform, out int shellModule))
                {
                    c.Fixed = true;
                    c.Module = shellModule;
                    c.Anchor = AnchorOf(c.Bounds, c.Module, true);
                }
                // Stufen, Podeste, Wangen, Pfosten und Handläufe gehören fest zu ihrer Treppe. Über den Partner bekam z. B. die
                // oberste Stufe unter Deck 00 den Baustein 0 und fehlte auf Etage 02 (Prüfung „feste Punkte“, 29.10.2026).
                // Lampenhalter an der Treppenwand nicht: die gehen mit ihrer Lampe.
                int stair = StairModuleOf(c.Collider);
                if (stair >= 1 && !c.Collider.name.Contains("light bracket"))
                {
                    c.Fixed = true;
                    c.Module = stair;
                    c.Anchor = Anchor.Stair;
                }
                // Deckplatten gehören zum Boden ihres Decks, damit jede Etage ihr ganzes Deck hat. Über den Partner gingen einzelne
                // Platten mit einer Treppe; auf Etage 01 fehlte so der Boden an der Treppenankunft (29.10.2026).
                var deck = DeckSlab.Match(c.Collider.name);
                if (deck.Success)
                {
                    c.Fixed = true;
                    c.Module = int.Parse(deck.Groups[1].Value);
                    c.Anchor = Anchor.Floor;
                    if (!DeckSlabs.TryGetValue(c.Module, out var slabs)) DeckSlabs[c.Module] = slabs = new List<Bounds>();
                    slabs.Add(c.Bounds);
                }
            }
            // Partner: das sichtbare Teil, dessen Eckpunkte dem Kollider am nächsten liegen (im Kollider oder höchstens
            // 10 cm davon). Der Kollider gehört dann zum selben Baustein und geht genau wie sein Partner mit.
            // Trägt er ein Teil einer Treppe (Schutzgitter an der Ankunft), geht er mit dieser Treppe; vorher entschied die zuletzt
            // gefundene Insel, und auf Etage 01 hatte das Gitter an der Treppenankunft keine Kollision (29.10.2026).
            foreach (var c in Colliders)
            {
                var box = c.Bounds;
                box.Expand(0.2f);
                float best = 0.1f * 0.1f;
                Island stairPartner = null;
                int stairHeld = 0;
                foreach (var i in Islands)
                {
                    if (!box.Intersects(i.Bounds)) continue;
                    int held = 0;
                    foreach (var p in i.Points)
                    {
                        float d = c.Bounds.SqrDistance(p);
                        if (d <= 0.02f * 0.02f) held++;
                        if (d <= best) { best = d; c.Partner = i; }
                    }
                    if (i.StairOwned && held > stairHeld) { stairHeld = held; stairPartner = i; }
                }
                if (stairPartner != null) c.Partner = stairPartner;
                if (c.Fixed) continue;
                // Durchgehende Kollider (Mauern, Säulen, Schachtrahmen) bleiben durchgehend, auch wenn ihr nächstes sichtbares
                // Teil zu einer Etage gehört – sonst gab es sie nur auf dieser Etage (Prüfung „feste Punkte“, 26.10.2026).
                if (c.Partner != null && c.Partner.Module != -1 && c.Module != -1) c.Module = c.Partner.Module;
            }
            // Was ein Kollider des Aufzugshalts hält, gehört zu dessen Baugruppe, auch wenn es in einem Sammel-Mesh steckt
            // (sichtbarer Sturz, Seitenträger, Lampe): seine Mitte oder mehr als die Hälfte seiner Eckpunkte liegt höchstens
            // 8 cm vom Kollider (die Zierleisten unter dem Sturz liegen 5–7 cm darunter, die Lampe ist breiter als ihr Halter).
            var shellParts = Colliders.Where(c => c.Fixed && c.Anchor == Anchor.Floor && InShell(c.Collider.transform, out _)).ToList();
            foreach (var c in shellParts)
            {
                var box = c.Bounds;
                box.Expand(0.16f);
                foreach (var i in Islands)
                {
                    if (i.StairOwned || i.Anchor == Anchor.Floor || i.Module != c.Module || !box.Intersects(i.Bounds)) continue;
                    if (box.Contains(i.Bounds.center) || i.Points.Count(box.Contains) * 2 > i.Points.Length) i.Anchor = Anchor.Floor;
                }
            }
            foreach (var r in Rigids)
            {
                r.Module = RigidModuleOf(r.Bounds);
                r.Anchor = RigidAnchor(r.Bounds, r.Module);
                if (r.Shell || r.InShell) continue;
                // Die Lampe am Sturz hängt bis 10 cm vor ihrem Halter und geht mit der Baugruppe des Aufzugshalts; sonst
                // leuchtete sie auf den gestreckten Etagen von oben statt nach unten (Etage 23, 29.10.2026).
                bool atShell = false;
                foreach (var c in shellParts)
                {
                    var near = c.Bounds;
                    near.Expand(0.6f);
                    if (c.Module == r.Module && near.Contains(r.Bounds.center)) { atShell = true; break; }
                }
                if (atShell) r.Anchor = Anchor.Floor;
                // Lampen an Treppen und über Podesten gehen mit der Treppe, wie deren Geländer.
                else if (r.Anchor != Anchor.Floor && StairModuleAt(r.Bounds.center) == r.Module) r.Anchor = Anchor.Stair;
            }
            // Lampenhalter (unsichtbare Kollider in den Treppen- und Etagengruppen) gehen genau wie ihre Lampe mit.
            foreach (var c in Colliders.Where(c => !c.Fixed && c.Collider.name.Contains("light bracket")))
            {
                foreach (var r in Rigids)
                {
                    if (r.Shell || r.InShell) continue;
                    var near = r.Bounds;
                    near.Expand(0.6f);
                    if (!near.Contains(c.Bounds.center)) continue;
                    c.Fixed = true;
                    c.Holder = r;
                    c.Module = r.Module;
                    c.Anchor = r.Anchor;
                    break;
                }
            }
            // Prüfhilfe (nur im Prüfmodus): Welche Punkte jedes Teils sind im Original fest? Im Mod müssen sie es auch sein,
            // sonst läuft man dort hindurch (Geländer ohne Kollision, Bl4ck969 26.10.2026).
            if (Diagnostics.Requested)
            {
                var owner = Colliders.ToDictionary(c => c.Collider);
                foreach (var i in Islands) SolidSamples(i, owner);
            }
        }

        private void SolidSamples(Island island, Dictionary<Collider, StaticCollider> owner)
        {
            var points = new List<Vector3>();
            var holders = new List<string>();
            int step = Mathf.Max(1, island.Points.Length / 16);
            for (int k = 0; k < island.Points.Length; k += step)
            {
                var p = island.Points[k];
                foreach (var hit in Physics.OverlapSphere(p, 0.05f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!StaticColliderSet.Contains(hit)) continue;
                    points.Add(p);
                    owner.TryGetValue(hit, out var c);
                    holders.Add($"{hit.GetType().Name} {hit.name} (Baustein {c?.Module}, Partner {(c?.Partner == null ? "-" : c.Partner.Renderer.name + " " + c.Partner.Anchor)})");
                    break;
                }
            }
            island.SolidSamples = points.ToArray();
            island.SolidHolders = holders.ToArray();
        }

        /// <summary>
        /// Eine Lampe ist eine Baugruppe: Lichter und Gehäuse unter einer gemeinsamen Gruppe von höchstens 2 m. Vorher gingen
        /// Gehäuseteile, Licht und Halter nach verschiedenen Regeln mit und hingen auf gestreckten Etagen bis 0,85 m
        /// auseinander (Bl4ck969, Etage 22, 29.10.2026).
        /// </summary>
        private static Transform LampOf(Transform light)
        {
            var group = light.parent;
            if (group == null || group.GetComponentsInChildren<MeshRenderer>(true).Length == 0) return light;
            var b = TreeBounds(group);
            return Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) <= 2f ? group : light;
        }

        private static Bounds TreeBounds(Transform t)
        {
            bool has = false;
            var b = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (has) b.Encapsulate(r.bounds); else { b = r.bounds; has = true; }
            }
            return b;
        }

        /// <summary>Durchgehendes Bauteil: Wände, Pfeiler, Stützen, Schacht (hoch und schmal oder Außenwand).</summary>
        internal static bool IsContinuousShape(Bounds b, string name) =>
            b.size.y >= 12f && (Mathf.Min(b.size.x, b.size.z) <= 2f || name.EndsWith("masonry"));

        /// <summary>
        /// Zerlegt ein Mesh in Inseln (Dreiecke, die über gemeinsame Eckpunkte zusammenhängen; gleiche Position zählt als gemeinsam).
        /// Inseln, die über eine Etagengrenze hinweg verschweißt sind, aber kein durchgehendes Bauteil sind
        /// (z. B. Handlauf von Pooltreppe und Deck), werden an der Grenze nach dem Schwerpunkt der Dreiecke getrennt.
        /// </summary>
        private void Split(MeshRenderer renderer, Mesh mesh)
        {
            var local = mesh.vertices;
            var world = new Vector3[local.Length];
            var matrix = renderer.transform.localToWorldMatrix;
            for (int v = 0; v < local.Length; v++) world[v] = matrix.MultiplyPoint3x4(local[v]);

            var weld = new int[world.Length];
            var ids = new Dictionary<(int, int, int), int>();
            for (int v = 0; v < world.Length; v++)
            {
                var key = (Mathf.RoundToInt(world[v].x * 2000f), Mathf.RoundToInt(world[v].y * 2000f), Mathf.RoundToInt(world[v].z * 2000f));
                if (!ids.TryGetValue(key, out int id)) ids[key] = id = ids.Count;
                weld[v] = id;
            }
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                var tris = mesh.GetTriangles(s);
                foreach (var whole in Connected(tris, weld))
                {
                    // Dreiecke an einer Treppe gehören zu deren Baustein, egal in welcher Höhe (Handlauf-Enden ragen über das Deck).
                    var rest = new List<int>();
                    if (IsContinuousShape(BoundsOf(whole, world), renderer.name))
                    {
                        rest = whole;
                    }
                    else
                    {
                        var owned = new Dictionary<int, List<int>>();
                        for (int t = 0; t < whole.Count; t += 3)
                        {
                            var centroid = (world[whole[t]] + world[whole[t + 1]] + world[whole[t + 2]]) / 3f;
                            int stair = StairModuleAt(centroid);
                            var target = rest;
                            if (stair >= 1 && !owned.TryGetValue(stair, out target)) owned[stair] = target = new List<int>();
                            target.Add(whole[t]); target.Add(whole[t + 1]); target.Add(whole[t + 2]);
                        }
                        foreach (var kv in owned)
                            foreach (var piece in Connected(kv.Value.ToArray(), weld))
                                Islands.Add(new Island { Renderer = renderer, SubMesh = s, Triangles = piece.ToArray(), Bounds = BoundsOf(piece, world), Points = PointsOf(piece, world), Module = kv.Key, ModuleFixed = true, StairOwned = true });
                    }
                    if (rest.Count == 0) continue;
                    var groupsLeft = ReferenceEquals(rest, whole) ? new List<List<int>> { whole } : Connected(rest.ToArray(), weld).ToList();
                    foreach (var group in groupsLeft)
                {
                    var b = BoundsOf(group, world);
                    int module = ModuleOf(b);
                    if (module != -1 || IsContinuousShape(b, renderer.name))
                    {
                        Islands.Add(new Island { Renderer = renderer, SubMesh = s, Triangles = group.ToArray(), Bounds = b, Points = PointsOf(group, world) });
                        continue;
                    }
                    // Nach Etage des Dreiecks-Schwerpunkts trennen und darin wieder zusammenhängend gruppieren.
                    var band = new Dictionary<int, int>();
                    for (int t = 0; t < group.Count; t += 3)
                    {
                        float cy = (world[group[t]].y + world[group[t + 1]].y + world[group[t + 2]].y) / 3f;
                        band[t] = ModuleOf(new Bounds(new Vector3(0f, cy, 0f), Vector3.zero));
                    }
                    foreach (var part in band.Values.Distinct())
                    {
                        var subset = new List<int>();
                        for (int t = 0; t < group.Count; t += 3)
                            if (band[t] == part) { subset.Add(group[t]); subset.Add(group[t + 1]); subset.Add(group[t + 2]); }
                        foreach (var piece in Connected(subset.ToArray(), weld))
                            Islands.Add(new Island { Renderer = renderer, SubMesh = s, Triangles = piece.ToArray(), Bounds = BoundsOf(piece, world), Points = PointsOf(piece, world), Module = part, ModuleFixed = true });
                    }
                }
                }
            }
        }

        private static Vector3[] PointsOf(List<int> indices, Vector3[] world) => indices.Distinct().Select(i => world[i]).ToArray();

        private static Bounds BoundsOf(List<int> indices, Vector3[] world)
        {
            var b = new Bounds(world[indices[0]], Vector3.zero);
            foreach (int idx in indices) b.Encapsulate(world[idx]);
            return b;
        }

        /// <summary>Gruppiert Dreiecke (Indexliste) nach Zusammenhang über verschweißte Eckpunkte.</summary>
        private static IEnumerable<List<int>> Connected(int[] tris, int[] weld)
        {
            var parent = new Dictionary<int, int>();
            int Find(int x)
            {
                if (!parent.ContainsKey(x)) { parent[x] = x; return x; }
                int root = x;
                while (parent[root] != root) root = parent[root];
                while (parent[x] != root) { int next = parent[x]; parent[x] = root; x = next; }
                return root;
            }
            for (int t = 0; t < tris.Length; t += 3)
            {
                int a = Find(weld[tris[t]]), b = Find(weld[tris[t + 1]]);
                if (a != b) parent[b] = a;
                int c = Find(weld[tris[t + 2]]);
                a = Find(a);
                if (a != c) parent[c] = a;
            }
            var groups = new Dictionary<int, List<int>>();
            for (int t = 0; t < tris.Length; t += 3)
            {
                int root = Find(weld[tris[t]]);
                if (!groups.TryGetValue(root, out var g)) groups[root] = g = new List<int>();
                g.Add(tris[t]); g.Add(tris[t + 1]); g.Add(tris[t + 2]);
            }
            return groups.Values;
        }

        /// <summary>Baustein nach Unterkante; -1 = über mehrere Etagen durchgehend, -2 = unterhalb des Bodens.</summary>
        internal int ModuleOf(Bounds b)
        {
            float min = b.min.y, max = b.max.y;
            if (min < FloorY[5] - 0.35f) return max > FloorY[5] + 1.5f ? -1 : -2;
            if (min >= FloorY[0] - 0.35f) return 0;
            for (int label = 1; label <= 5; label++)
            {
                if (min >= FloorY[label] - 0.35f && min < FloorY[label - 1] - 0.35f)
                    return max <= FloorY[label - 1] + 1.5f ? label : -1;
            }
            return -1;
        }

        internal Anchor AnchorOf(Bounds b, int module, bool shellPart = false)
        {
            if (module <= 0) return Anchor.Floor;
            float floor = FloorY[module], ceiling = FloorY[module - 1];
            bool touchesFloor = b.min.y <= floor + 0.5f;
            bool touchesCeiling = b.max.y >= ceiling - 0.7f;
            // Ein Aufzugshalt (Tor, Pfosten, Sturz, Halter, Schild) ist eine Baugruppe am Boden, wie seine Funktionsobjekte;
            // gestreckt wird nur, was vom Boden bis an die Decke reicht (Führungsschiene). Vorher schwebte bei gestreckten
            // Etagen der Sturz über den Pfosten (Etage 23, 29.10.2026).
            if (shellPart) return touchesFloor && b.max.y >= ceiling - 0.1f ? Anchor.Stretch : Anchor.Floor;
            // Alles in den oberen 2,6 m hängt am nächsten Deck (Pooltreppe 2,26 m tief, Träger, Deckenlampen).
            bool hangsFromCeiling = b.min.y >= ceiling - 2.6f && !touchesFloor;
            if (touchesFloor && touchesCeiling) return Anchor.Stretch;
            if (hangsFromCeiling || touchesCeiling) return Anchor.Ceiling;
            // Was ganz in den unteren 1,5 m liegt (Geländerstangen, Sockel), steht am Boden; als „anteilig“ schwebte z. B. eine
            // Stange des Schutzgitters an der Pooltreppe auf den unteren Etagen über ihren Pfosten.
            if (touchesFloor || b.max.y <= floor + 1.5f) return Anchor.Floor;
            return Anchor.Proportional;
        }

        /// <summary>Liegt das Teil in einem Aufzugshalt? Dann dessen Baustein.</summary>
        private bool InShell(Transform t, out int module)
        {
            foreach (var kv in _shellModule)
                if (t.IsChildOf(kv.Key)) { module = kv.Value; return true; }
            module = -1;
            return false;
        }

        /// <summary>Baustein eines Funktionsobjekts: nach seinen Grenzen, bei mehreren Etagen nach seiner Unterkante.</summary>
        private int RigidModuleOf(Bounds b)
        {
            int module = ModuleOf(b);
            return module == -1 ? ModuleOf(new Bounds(new Vector3(0f, b.min.y, 0f), Vector3.zero)) : module;
        }

        /// <summary>Baustein der Treppe, zu deren Gruppe ein Kollider gehört („Stair connection i+1 to i“ = Baustein 5-i); sonst -1.</summary>
        private static int StairModuleOf(Collider c)
        {
            var m = StairGroup.Match(c.transform.parent != null ? c.transform.parent.name : "");
            return m.Success ? 5 - int.Parse(m.Groups[2].Value) : -1;
        }

        /// <summary>Funktionsobjekte bis 2 m über dem Boden stehen am Boden (Taster, Stationen, Werkbänke).</summary>
        private Anchor RigidAnchor(Bounds b, int module)
        {
            if (module <= 0) return Anchor.Floor;
            float floor = FloorY[module], ceiling = FloorY[module - 1];
            if (b.min.y <= floor + 2f) return Anchor.Floor;
            if (b.max.y >= ceiling - 0.7f) return Anchor.Ceiling;
            return Anchor.Proportional;
        }

        /// <summary>Baustein der Treppe, an der ein Punkt liegt (über einem Podest, oder 0,8 m unter bis 2,5 m über einer Stufe
        /// und waagerecht ≤ 1,5 m von ihrer Mitte bzw. ≤ 0,25 m von ihrer Trittfläche); sonst -9.</summary>
        internal int StairModuleAt(Vector3 p)
        {
            foreach (var kv in Landings)
                foreach (var l in kv.Value)
                    if (OverLanding(l, p)) return kv.Key;
            int best = -9;
            float bestDistance = float.MaxValue;
            foreach (var kv in StairPoints)
            {
                var boxes = StairBoxes[kv.Key];
                for (int k = 0; k < kv.Value.Count; k++)
                {
                    var q = kv.Value[k];
                    float dy = p.y - q.y;
                    if (dy < -0.8f || dy > 2.5f) continue;
                    float dx = q.x - p.x, dz = q.z - p.z, d = dx * dx + dz * dz;
                    // Die Ecken 2,5 m breiter Stufen liegen weiter als 1,5 m von der Mitte; Kantenleisten und ein Messing-Handlauf
                    // fielen deshalb aus der Treppe und wanderten mit der Decke (Prüfung „feste Punkte“, 29.10.2026).
                    if (d > StairReach * StairReach && EdgeDistanceSqr(boxes[k], p) > StairEdge * StairEdge) continue;
                    if (d < bestDistance) { bestDistance = d; best = kv.Key; }
                }
            }
            return best;
        }

        /// <summary>
        /// Liegt die Mitte eines Treppenteils am oberen Deck – über dessen Platten, nicht über einer Stufe oder einem Podest,
        /// höchstens 0,8 m darunter? Dann geht das Teil mit dem Deck. Mit der Höhe der obersten Stufe (0,2 m unter dem Deck)
        /// lag die Bodenfläche an der Treppenankunft bei Faktor 1,34 knapp 7 cm tiefer als der Rest; an der Dreieckskante
        /// sah man durch den Boden (Bl4ck969, 29.10.2026).
        /// </summary>
        internal bool OnUpperDeck(int module, Vector3 p) =>
            module >= 1 && p.y >= FloorY[module - 1] - 0.8f && OverDeckNotStair(module - 1, module, p);

        /// <summary>Spiegelbild am Treppenanfang: Mitte bis 0,8 m unter dem eigenen Deck, über dessen Platten, nicht über einer Stufe.</summary>
        internal bool OnLowerDeck(int module, Vector3 p) =>
            p.y <= FloorY[module] + 0.05f && p.y >= FloorY[module] - 0.8f && OverDeckNotStair(module, module, p);

        private bool OverDeckNotStair(int deck, int module, Vector3 p)
        {
            if (!DeckSlabs.TryGetValue(deck, out var slabs)) return false;
            bool overSlab = false;
            foreach (var s in slabs)
                if (p.x >= s.min.x - 0.05f && p.x <= s.max.x + 0.05f && p.z >= s.min.z - 0.05f && p.z <= s.max.z + 0.05f) { overSlab = true; break; }
            if (!overSlab) return false;
            if (StairBoxes.TryGetValue(module, out var boxes))
                foreach (var b in boxes)
                    if (EdgeDistanceSqr(b, p) == 0f) return false;
            return true;
        }

        /// <summary>Waagerechter Abstand (zum Quadrat) eines Punktes zur Grundfläche eines Kastens; 0 darüber oder darunter.</summary>
        private static float EdgeDistanceSqr(Bounds b, Vector3 p)
        {
            float dx = Mathf.Max(0f, Mathf.Max(b.min.x - p.x, p.x - b.max.x));
            float dz = Mathf.Max(0f, Mathf.Max(b.min.z - p.z, p.z - b.max.z));
            return dx * dx + dz * dz;
        }

        private static bool OverLanding(Bounds landing, Vector3 p)
        {
            float dy = p.y - landing.max.y;
            return dy >= -0.8f && dy <= 2.5f && p.x >= landing.min.x - 0.35f && p.x <= landing.max.x + 0.35f
                && p.z >= landing.min.z - 0.35f && p.z <= landing.max.z + 0.35f;
        }

        /// <summary>
        /// Höhe der Stufenlinie unter einem Punkt eines Treppen-Teils: über einem Podest dessen Höhe, sonst die waagerecht
        /// nächste Stufe, die 0,8 m über bis 2,5 m unter dem Punkt liegt; notfalls die nächste Stufe überhaupt.
        /// </summary>
        internal float StairHeight(int module, Vector3 p)
        {
            if (Landings.TryGetValue(module, out var landings))
                foreach (var l in landings)
                    if (OverLanding(l, p)) return l.max.y;
            if (!StairPoints.TryGetValue(module, out var points)) return p.y;
            float height = p.y, best = float.MaxValue, fallback = float.MaxValue, fallbackHeight = p.y;
            foreach (var q in points)
            {
                float dx = q.x - p.x, dz = q.z - p.z, dy = p.y - q.y, d = dx * dx + dz * dz;
                if (dy >= -0.8f && dy <= 2.5f)
                {
                    if (d < best) { best = d; height = q.y; }
                }
                else if (d + dy * dy < fallback) { fallback = d + dy * dy; fallbackHeight = q.y; }
            }
            return best < float.MaxValue ? height : fallbackHeight;
        }
    }
}
