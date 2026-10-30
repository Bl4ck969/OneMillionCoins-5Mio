using System.Collections.Generic;
using System.Linq;
using GoldDigger;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace OneMillionCoins5Mio
{
    /// <summary>
    /// Baut das 5-Mio.-Gebäude aus dem Teile-Inventar des Originals neu auf.
    ///
    /// Jede neue Etage entsteht aus dem Original-Baustein mit der nächstliegenden Höhe. Die sichtbare Geometrie
    /// wird je Etage und Material zu neuen Meshes zusammengesetzt, jeder Eckpunkt nach seiner Rolle verschoben
    /// (Boden, Decke, anteilig, gestreckt oder entlang der Stufenlinie). Kollider werden passend neu angelegt,
    /// Funktionsobjekte (Stationen, Taster, Lampen, Aufzugshalte) als Ganzes kopiert. Durchgehende Teile
    /// (Wände, Pfeiler, Stützen, Schacht) werden auf die neue Höhe gestreckt. Die Originale werden abgeschaltet.
    /// </summary>
    internal static class FloorBuilder
    {
        private const int OriginalFloors = 6;

        internal static float[] Heights;
        internal static float Lift;
        internal static readonly List<string> Distorted = new List<string>();

        /// <summary>Prüfhilfe: Punkte, die im Original fest sind, an ihrer neuen Lage (Etage, Teil, Punkt).</summary>
        internal static readonly List<(string where, string part, Vector3 point, string holder)> SolidChecks = new List<(string, string, Vector3, string)>();
        /// <summary>Je neuer Etage (von oben): aus welchem Original-Baustein und mit welchem Höhenfaktor gebaut.</summary>
        internal static readonly List<string> Plan = new List<string>();
        private static Scene _builtScene;

        /// <summary>Eine Etage des neuen Gebäudes.</summary>
        private sealed class Entry
        {
            internal int Index;      // von unten (Aufzug)
            internal int Label;      // Original-Baustein nach Schild
            internal int Copy;       // 0 = Original selbst, sonst Kopie
            internal float Y;        // Höhe des Etagenbodens
            internal float Factor;   // Höhenfaktor gegenüber dem Original-Baustein
            internal readonly Dictionary<Transform, Transform> Instances = new Dictionary<Transform, Transform>();
        }

        internal static void EnsureBuilt(Scene scene)
        {
            if (!ModState.Active || scene.path != GameNavigation.GameScene || scene == _builtScene) return;
            if (Diagnostics.KeepOriginal) return;   // Prüfaufnahme des unveränderten Gebäudes
            _builtScene = scene;
            Heights = null;
            Lift = 0f;
            Distorted.Clear();
            try
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                Build(scene);
                Plugin.Log.LogInfo($"Etagenbau in {watch.ElapsedMilliseconds} ms.");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("Etagenbau fehlgeschlagen: " + e);
            }
        }

        private static void Build(Scene scene)
        {
            var elevator = Object.FindObjectsByType<CargoElevator>(FindObjectsInactive.Include).FirstOrDefault();
            var original = elevator == null ? null : Traverse.Create(elevator).Field<float[]>("layerHeights").Value;
            if (original == null || original.Length != OriginalFloors)
            {
                Plugin.Log.LogError($"Unerwartete Etagenhöhen ({original?.Length}), Etagenbau abgebrochen.");
                return;
            }
            var y = original.Reverse().ToArray();   // nach Schild: [0] = oben ... [5] = Boden
            var entries = PlanFloors(y, out float growth);
            float top = entries[entries.Count - 1].Y;
            float lift = top - y[0];
            int floors = entries.Count;

            var inv = new Inventory(scene, y);
            var container = new GameObject("5 Mio. – Gebäude").transform;
            SceneManager.MoveGameObjectToScene(container.gameObject, scene);

            // 1. Sichtbare Geometrie (vor jeder Verschiebung, Grundlage sind die Original-Positionen).
            var cache = new Dictionary<MeshRenderer, MeshData>();
            foreach (var entry in entries)
            {
                var islands = inv.Islands.Where(i => i.Module == entry.Label).ToList();
                BuildMeshes(container, $"Etage {floors - 1 - entry.Index:00}", islands, cache, (i, p) => MapPoint(inv, y, entry, i.Anchor, i.Bounds, p));
            }
            BuildMeshes(container, "Durchgehend", inv.Islands.Where(i => i.Module == -1).ToList(), cache, (i, p) => MapContinuous(y, top, lift, p));
            BuildMeshes(container, "Basis", inv.Islands.Where(i => i.Module == -2).ToList(), cache, (i, p) => p);
            SolidChecks.Clear();
            if (Diagnostics.Requested)
            {
                foreach (var entry in entries)
                    foreach (var i in inv.Islands.Where(i => i.Module == entry.Label))
                        for (int k = 0; k < i.SolidSamples.Length; k++)
                            SolidChecks.Add(($"Etage {floors - 1 - entry.Index:00}", PartName(i), MapPoint(inv, y, entry, i.Anchor, i.Bounds, i.SolidSamples[k]), i.SolidHolders[k]));
                foreach (var i in inv.Islands.Where(i => i.Module == -1))
                    for (int k = 0; k < i.SolidSamples.Length; k++)
                        SolidChecks.Add(("Durchgehend", PartName(i), MapContinuous(y, top, lift, i.SolidSamples[k]), i.SolidHolders[k]));
            }

            // 2. Kollider vorbereiten (Lage aus den Original-Daten, Eltern erst nach dem Kopieren der Hüllen).
            var colliderPlans = new List<(StaticCollider source, Entry entry, Pose pose, Vector3 scale, Vector3 size, Vector3 center)>();
            foreach (var c in inv.Colliders)
            {
                if (c.Module == -1 || c.Module == -2)
                {
                    colliderPlans.Add(PlanCollider(c, null, p => c.Module == -2 ? p : MapContinuous(y, top, lift, p), c.Module == -1));
                    continue;
                }
                // Ein Kollider macht genau die Verschiebung seines sichtbaren Teils an der nächstgelegenen Stelle mit.
                var partner = c.Partner;
                foreach (var entry in entries.Where(e => e.Label == c.Module))
                {
                    if (c.Holder != null)
                    {
                        var holderDelta = RigidDelta(inv, y, entry, c.Holder);
                        colliderPlans.Add(PlanCollider(c, entry, p => p + holderDelta, false));
                        continue;
                    }
                    System.Func<Vector3, Vector3> map = partner == null || c.Fixed
                        ? (p => MapPoint(inv, y, entry, c.Anchor, c.Bounds, p))
                        : (p =>
                        {
                            var q = partner.Points[0];
                            float best = float.MaxValue;
                            foreach (var point in partner.Points)
                            {
                                float d = (point - p).sqrMagnitude;
                                if (d < best) { best = d; q = point; }
                            }
                            return p + (MapPoint(inv, y, entry, partner.Anchor, partner.Bounds, q) - q);
                        });
                    var anchor = c.Fixed ? c.Anchor : partner?.Anchor ?? c.Anchor;
                    // Treppen-Kollider (Handlaufstäbe, Stufen, Wangen) folgen der Stufenlinie wie ihr sichtbares Teil, und
                    // zwar an jedem ihrer Enden: Die Verschiebung des nächsten Partnerpunkts ließ lange Stäbe in alter
                    // Neigung stehen (Prüfung „feste Punkte“, 26.10.2026).
                    if (anchor == Anchor.Stair) map = p => MapPoint(inv, y, entry, Anchor.Stair, c.Bounds, p);
                    colliderPlans.Add(PlanCollider(c, entry, map, anchor == Anchor.Stretch));
                }
            }

            // 3. Funktionsobjekte und Hüllen: erst kopieren (von den Original-Positionen aus), dann Originale verschieben.
            var moveOriginal = new List<(Transform t, Vector3 delta)>();
            foreach (var entry in entries)
            {
                foreach (var r in inv.Rigids.Where(r => r.Module == entry.Label && !r.InShell))
                {
                    var delta = RigidDelta(inv, y, entry, r);
                    if (entry.Copy == 0)
                    {
                        entry.Instances[r.Root] = r.Root;
                        if (delta.sqrMagnitude > 0f) moveOriginal.Add((r.Root, delta));
                        continue;
                    }
                    var clone = Object.Instantiate(r.Root.gameObject, r.Root.parent);
                    clone.name = $"{r.Root.name} (+{entry.Copy})";
                    clone.transform.SetPositionAndRotation(r.Root.position + delta, r.Root.rotation);
                    clone.transform.localScale = r.Root.localScale;
                    DisableStatics(r.Root, clone.transform, inv);
                    entry.Instances[r.Root] = clone.transform;
                }
            }
            foreach (var (t, delta) in moveOriginal) t.position += delta;
            foreach (var r in inv.Rigids.Where(r => r.Shell || !r.InShell)) DisableStatics(r.Root, r.Root, inv);

            // 4. Kollider anlegen; Kollider in Aufzugshalten gehören in deren Hülle (der Haufen ignoriert sie dort).
            foreach (var plan in colliderPlans)
            {
                var shell = inv.Rigids.FirstOrDefault(r => r.Shell && plan.source.Collider.transform.IsChildOf(r.Root));
                Transform parent = container;
                if (shell != null && plan.entry != null && plan.entry.Instances.TryGetValue(shell.Root, out var instance)) parent = instance;
                CreateCollider(plan.source.Collider, parent, plan.pose, plan.scale, plan.size, plan.center);
            }

            // 5. Originale abschalten, Spieler nach oben, Aufzug und Nummern.
            foreach (var r in inv.SplitRenderers) r.enabled = false;
            foreach (var c in inv.StaticColliderSet) c.enabled = false;
            var player = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Player");
            if (player != null) player.transform.position += Vector3.up * lift;

            Heights = entries.Select(e => e.Y).ToArray();
            Traverse.Create(elevator).Field("layerHeights").SetValue((float[])Heights.Clone());
            var car = Traverse.Create(elevator).Field<Transform>("movingCar").Value;
            if (car != null) car.localPosition = new Vector3(car.localPosition.x, Heights[Heights.Length - 1], car.localPosition.z);
            Renumber(entries, floors);
            Plan.Clear();
            foreach (var e in entries.AsEnumerable().Reverse())
                Plan.Add($"Etage {floors - 1 - e.Index:00} (Boden {e.Y:0.00} m) <- Original {e.Label:00} (Boden {y[e.Label]:0.00} m), Höhenfaktor {e.Factor:0.00}");
            Physics.SyncTransforms();
            Lift = lift;

            Plugin.Log.LogInfo($"Etagen: {floors}, Wachstum {growth:0.000} je Etage, zusätzliche Höhe {lift:0.#} m. " +
                $"Inventar: {inv.Islands.Count} Teile aus {inv.SplitRenderers.Count} Meshes, {inv.Colliders.Count} Kollider, {inv.Rigids.Count} Funktionsobjekte. " +
                $"Von oben: {string.Join(" / ", entries.AsEnumerable().Reverse().Skip(1).Select(e => $"{(e.Label == 5 ? y[4] - y[5] : (y[e.Label - 1] - y[e.Label]) * e.Factor):0.0} m ({e.Label:00}x{e.Factor:0.00})"))}");
        }

        /// <summary>Zielhöhen: jede Etage um denselben Faktor höher als die darüber; darunter die unveränderte Bodenhalle.</summary>
        private static List<Entry> PlanFloors(float[] y, out float growth)
        {
            int intervals = Plugin.FloorCount.Value - 2;
            float topHeight = Plugin.TopFloorHeight.Value, deepHeight = Plugin.DeepFloorHeight.Value;
            growth = intervals > 1 ? Mathf.Pow(deepHeight / topHeight, 1f / (intervals - 1)) : 1f;
            var entries = new List<Entry> { new Entry { Label = 5, Y = y[5], Factor = 1f } };
            var copies = new int[OriginalFloors];
            float floorY = y[4];
            for (int k = intervals - 1; k >= 0; k--)
            {
                float target = topHeight * Mathf.Pow(growth, k);
                int label = 1;
                float best = float.MaxValue;
                for (int l = 1; l <= 4; l++)
                {
                    float error = Mathf.Abs(Mathf.Log(target / (y[l - 1] - y[l])));
                    if (error < best) { best = error; label = l; }
                }
                entries.Add(new Entry { Label = label, Copy = copies[label]++, Y = floorY, Factor = target / (y[label - 1] - y[label]) });
                floorY += target;
            }
            entries.Add(new Entry { Label = 0, Y = floorY, Factor = 1f });
            for (int i = 0; i < entries.Count; i++) entries[i].Index = i;
            return entries;
        }

        private static float AnchorReference(Bounds b) => b.min.y;

        /// <summary>Senkrechte Verschiebung eines Funktionsobjekts nach seiner Rolle, an einer Treppe nach der Stufenlinie an seiner Stelle.</summary>
        private static Vector3 RigidDelta(Inventory inv, float[] y, Entry entry, Rigid r)
        {
            var reference = new Vector3(r.Bounds.center.x, AnchorReference(r.Bounds), r.Bounds.center.z);
            return Vector3.up * (MapPoint(inv, y, entry, r.Anchor, r.Bounds, reference).y - reference.y);
        }

        private static string PartName(Island i) =>
            $"{i.Renderer.name} [{i.Anchor}{(i.StairOwned ? ", Treppe" : "")}] Original x {i.Bounds.center.x:0.0} y {i.Bounds.min.y:0.0}..{i.Bounds.max.y:0.0} z {i.Bounds.center.z:0.0}";

        /// <summary>
        /// Neue Lage eines Punktes eines Bausteins. Jedes Teil folgt genau einer Regel: Treppen-Teile der Stufenlinie
        /// (über Podesten deren Höhe), alle anderen ihrer Rolle. So kann ein Teil nicht in sich auseinandergezogen werden.
        /// </summary>
        private static Vector3 MapPoint(Inventory inv, float[] y, Entry entry, Anchor anchor, Bounds partBounds, Vector3 p)
        {
            int label = entry.Label;
            float offset = entry.Y - y[label];
            if (label == 0 || label == 5 || Mathf.Abs(entry.Factor - 1f) < 0.001f) return p + Vector3.up * offset;
            float floor = y[label], height = y[label - 1] - y[label], extra = entry.Factor - 1f;
            float delta;
            switch (anchor)
            {
                // Ob ein Treppenteil zum oberen Deck gehört, entscheidet seine Mitte: An der Kante zwischen oberster Stufe und
                // Deck gehört ein Eckpunkt zu beiden, je Punkt entschieden würde die Stufe gekippt oder die Bodenplatte schräg.
                case Anchor.Stair:
                    if (inv.OnUpperDeck(label, partBounds.center)) delta = height * extra;
                    else if (inv.OnLowerDeck(label, partBounds.center)) delta = 0f;
                    else delta = (inv.StairHeight(label, p) - floor) * extra;
                    break;
                case Anchor.Ceiling: delta = height * extra; break;
                case Anchor.Stretch: delta = (p.y - floor) * extra; break;
                case Anchor.Proportional: delta = (partBounds.center.y - floor) * extra; break;
                default: delta = 0f; break;
            }
            return p + Vector3.up * (offset + delta);
        }

        /// <summary>
        /// Durchgehende Teile: zwischen Boden und 1 m unter der alten Etage 00 gestreckt, darüber um die Gesamthöhe
        /// verschoben. So behalten die oberen Enden (Stützen 0,34 m unter Deck 00, Schacht, Wandkrone) ihren Abstand.
        /// </summary>
        private static Vector3 MapContinuous(float[] y, float top, float lift, Vector3 p)
        {
            float upper = y[0] - 1f;
            if (p.y <= y[5]) return p;
            if (p.y <= upper) return new Vector3(p.x, y[5] + (p.y - y[5]) * (upper + lift - y[5]) / (upper - y[5]), p.z);
            return p + Vector3.up * lift;
        }

        private sealed class MeshData
        {
            internal Vector3[] Positions, Normals;
            internal Vector4[] Tangents;
            internal Vector2[] Uv, Uv2;
            internal Color[] Colors;
        }

        private static MeshData DataOf(MeshRenderer renderer, Dictionary<MeshRenderer, MeshData> cache)
        {
            if (cache.TryGetValue(renderer, out var data)) return data;
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            var m = renderer.transform.localToWorldMatrix;
            var n = m.inverse.transpose;
            data = new MeshData
            {
                Positions = mesh.vertices.Select(v => m.MultiplyPoint3x4(v)).ToArray(),
                Normals = mesh.normals.Select(v => n.MultiplyVector(v).normalized).ToArray(),
                Tangents = mesh.tangents.Select(t => { var d = m.MultiplyVector(new Vector3(t.x, t.y, t.z)).normalized; return new Vector4(d.x, d.y, d.z, t.w); }).ToArray(),
                Uv = mesh.uv,
                Uv2 = mesh.uv2,
                Colors = mesh.colors,
            };
            if (renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 65534 && data.Uv2.Length > 0)
            {
                var so = renderer.lightmapScaleOffset;
                data.Uv2 = data.Uv2.Select(u => new Vector2(u.x * so.x + so.z, u.y * so.y + so.w)).ToArray();
            }
            cache[renderer] = data;
            return data;
        }

        /// <summary>Setzt Inseln je Material (und Schatten-/Lightmap-Einstellung) zu neuen Meshes zusammen.</summary>
        private static void BuildMeshes(Transform container, string name, List<Island> islands, Dictionary<MeshRenderer, MeshData> cache, System.Func<Island, Vector3, Vector3> map)
        {
            var groups = islands.GroupBy(i => (material: i.Renderer.sharedMaterials[Mathf.Min(i.SubMesh, i.Renderer.sharedMaterials.Length - 1)],
                shadows: i.Renderer.shadowCastingMode, receive: i.Renderer.receiveShadows, layer: i.Renderer.gameObject.layer, lightmap: i.Renderer.lightmapIndex));
            foreach (var group in groups)
            {
                var positions = new List<Vector3>();
                var normals = new List<Vector3>();
                var tangents = new List<Vector4>();
                var uv = new List<Vector2>();
                var uv2 = new List<Vector2>();
                var colors = new List<Color>();
                var triangles = new List<int>();
                bool hasNormals = true, hasTangents = true, hasUv = true, hasUv2 = true, hasColors = true;
                foreach (var island in group)
                {
                    var data = DataOf(island.Renderer, cache);
                    hasNormals &= data.Normals.Length > 0; hasTangents &= data.Tangents.Length > 0; hasUv &= data.Uv.Length > 0;
                    hasUv2 &= data.Uv2.Length > 0; hasColors &= data.Colors.Length > 0;
                }
                foreach (var island in group)
                {
                    var data = DataOf(island.Renderer, cache);
                    var remap = new Dictionary<int, int>();
                    float low = float.MaxValue, high = float.MinValue;
                    foreach (int idx in island.Triangles)
                    {
                        if (!remap.TryGetValue(idx, out int n))
                        {
                            remap[idx] = n = positions.Count;
                            var mapped = map(island, data.Positions[idx]);
                            low = Mathf.Min(low, mapped.y); high = Mathf.Max(high, mapped.y);
                            positions.Add(mapped);
                            if (hasNormals) normals.Add(data.Normals[idx]);
                            if (hasTangents) tangents.Add(data.Tangents[idx]);
                            if (hasUv) uv.Add(data.Uv[idx]);
                            if (hasUv2) uv2.Add(data.Uv2[idx]);
                            if (hasColors) colors.Add(data.Colors[idx]);
                        }
                        triangles.Add(n);
                    }
                    // Prüfhilfe: Teile, deren Höhe sich beim Umbau deutlich ändert, sind verdächtig.
                    float before = island.Bounds.size.y, after = high - low;
                    if (island.Module == -1 || Mathf.Abs(after - before) > 0.4f + 0.25f * before)
                        Distorted.Add($"{name}: {island.Renderer.name} Baustein {island.Module} Rolle {island.Anchor} Höhe {before:0.00} -> {after:0.00} m, " +
                            $"Original y {island.Bounds.min.y:0.00}..{island.Bounds.max.y:0.00}, x {island.Bounds.min.x:0.0}..{island.Bounds.max.x:0.0}, z {island.Bounds.min.z:0.0}..{island.Bounds.max.z:0.0}");
                }
                if (triangles.Count == 0) continue;
                var mesh = new Mesh { name = $"{name} {group.Key.material?.name}", indexFormat = IndexFormat.UInt32 };
                mesh.SetVertices(positions);
                if (hasNormals) mesh.SetNormals(normals);
                if (hasTangents) mesh.SetTangents(tangents);
                if (hasUv) mesh.SetUVs(0, uv);
                if (hasUv2) mesh.SetUVs(1, uv2);
                if (hasColors) mesh.SetColors(colors);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                var go = new GameObject(mesh.name) { layer = group.Key.layer };
                go.transform.SetParent(container, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = group.Key.material;
                renderer.shadowCastingMode = group.Key.shadows;
                renderer.receiveShadows = group.Key.receive;
                if (group.Key.lightmap >= 0 && group.Key.lightmap < 65534 && hasUv2)
                {
                    renderer.lightmapIndex = group.Key.lightmap;
                    renderer.lightmapScaleOffset = new Vector4(1f, 1f, 0f, 0f);
                }
            }
        }

        /// <summary>Lage und Maße eines neuen Kolliders. Schräge Kästen (Handläufe, Wangen) folgen der neuen Neigung.</summary>
        private static (StaticCollider, Entry, Pose, Vector3, Vector3, Vector3) PlanCollider(StaticCollider source, Entry entry, System.Func<Vector3, Vector3> map, bool stretch)
        {
            var c = source.Collider;
            var t = c.transform;
            var scale = t.lossyScale;
            if (!(c is BoxCollider box))
            {
                var center = c.bounds.center;
                var moved = map(center) - center;
                return (source, entry, new Pose(t.position + moved, t.rotation), scale, Vector3.zero, Vector3.zero);
            }
            var worldCenter = t.TransformPoint(box.center);
            var axes = new[] { t.right * (box.size.x * scale.x * 0.5f), t.up * (box.size.y * scale.y * 0.5f), t.forward * (box.size.z * scale.z * 0.5f) };
            int along = Enumerable.Range(0, 3).OrderByDescending(a => axes[a].sqrMagnitude).First();
            var half = axes[along];
            float slope = Mathf.Abs(half.normalized.y);
            var rotation = t.rotation;
            var size = box.size;
            Vector3 newCenter;
            if (slope > 0.05f && slope < 0.95f || stretch)
            {
                if (stretch && slope < 0.95f)
                {
                    // Senkrecht strecken: die am stärksten nach oben zeigende Achse nehmen.
                    along = Enumerable.Range(0, 3).OrderByDescending(a => Mathf.Abs(axes[a].normalized.y) * axes[a].magnitude).First();
                    half = axes[along];
                }
                var a = map(worldCenter + half);
                var b = map(worldCenter - half);
                newCenter = (a + b) * 0.5f;
                var newHalf = (a - b) * 0.5f;
                rotation = Quaternion.FromToRotation(half.normalized, newHalf.normalized) * rotation;
                size[along] *= newHalf.magnitude / half.magnitude;
            }
            else
            {
                newCenter = map(worldCenter);
            }
            // Pose so wählen, dass das lokale Zentrum des Kastens auf newCenter liegt.
            var position = newCenter - rotation * Vector3.Scale(box.center, scale);
            return (source, entry, new Pose(position, rotation), scale, size, box.center);
        }

        private static void CreateCollider(Collider source, Transform parent, Pose pose, Vector3 scale, Vector3 size, Vector3 center)
        {
            var go = new GameObject(source.name) { layer = source.gameObject.layer, tag = source.gameObject.tag };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(pose.position, pose.rotation);
            var parentScale = parent.lossyScale;
            go.transform.localScale = new Vector3(scale.x / parentScale.x, scale.y / parentScale.y, scale.z / parentScale.z);
            switch (source)
            {
                case BoxCollider box:
                    var nb = go.AddComponent<BoxCollider>();
                    nb.center = center;
                    nb.size = size;
                    nb.sharedMaterial = box.sharedMaterial;
                    break;
                case MeshCollider mesh:
                    var nm = go.AddComponent<MeshCollider>();
                    nm.sharedMesh = mesh.sharedMesh;
                    nm.convex = mesh.convex;
                    nm.sharedMaterial = mesh.sharedMaterial;
                    break;
                case CapsuleCollider capsule:
                    var nc = go.AddComponent<CapsuleCollider>();
                    nc.center = capsule.center; nc.radius = capsule.radius; nc.height = capsule.height; nc.direction = capsule.direction;
                    nc.sharedMaterial = capsule.sharedMaterial;
                    break;
                case SphereCollider sphere:
                    var ns = go.AddComponent<SphereCollider>();
                    ns.center = sphere.center; ns.radius = sphere.radius;
                    ns.sharedMaterial = sphere.sharedMaterial;
                    break;
            }
        }

        /// <summary>Statische Renderer und Kollider in einer Kopie (oder im Original) abschalten; sie werden neu aufgebaut.</summary>
        private static void DisableStatics(Transform source, Transform target, Inventory inv)
        {
            var sourceRenderers = source.GetComponentsInChildren<MeshRenderer>(true);
            var targetRenderers = target.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < sourceRenderers.Length && i < targetRenderers.Length; i++)
                if (inv.SplitRenderers.Contains(sourceRenderers[i])) targetRenderers[i].enabled = false;
            var sourceColliders = source.GetComponentsInChildren<Collider>(true);
            var targetColliders = target.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < sourceColliders.Length && i < targetColliders.Length; i++)
                if (inv.StaticColliderSet.Contains(sourceColliders[i])) targetColliders[i].enabled = false;
        }

        /// <summary>
        /// Stationsränge zählen von oben (0 = Etage 00), Aufzugsindizes von unten (0 = Boden).
        /// Kabinenknöpfe (keiner Etage zugeordnet) werden über ihre alte Etage auf deren neue Lage abgebildet.
        /// </summary>
        private static void Renumber(List<Entry> entries, int floors)
        {
            var handled = new HashSet<Object>();
            foreach (var entry in entries)
            {
                foreach (var root in entry.Instances.Values)
                {
                    foreach (var machine in root.GetComponentsInChildren<DepositMachine>(true))
                    {
                        Traverse.Create(machine).Field("requiredStationRank").SetValue(floors - 1 - entry.Index);
                        handled.Add(machine);
                    }
                    foreach (var gate in root.GetComponentsInChildren<ElevatorLandingGate>(true))
                    {
                        Traverse.Create(gate).Field("floor").SetValue(entry.Index);
                        handled.Add(gate);
                    }
                    foreach (var control in root.GetComponentsInChildren<ElevatorControl>(true))
                    {
                        Traverse.Create(control).Field("floor").SetValue(entry.Index);
                        handled.Add(control);
                    }
                }
            }
            foreach (var control in Object.FindObjectsByType<ElevatorControl>(FindObjectsInactive.Include))
            {
                if (handled.Contains(control)) continue;
                int oldLabel = 5 - Traverse.Create(control).Field<int>("floor").Value;
                var target = entries.FirstOrDefault(e => e.Label == oldLabel && e.Copy == 0);
                if (target != null) Traverse.Create(control).Field("floor").SetValue(target.Index);
            }
        }
    }
}
