// Layout check on the real models (owner, Nov 13 2026: "why is that gate so terrible. Two towers on each side of the
// entrance ... where is the quality testing?"). The gate's drum towers stood inside two element towers and no check
// looked. This places every structure where the game places it, measures the models' bounds, and fails when:
//   - two structures' footprints overlap (gatehouse, towers, buildings and ruins on every plot, fountain), or one stands
//     on the road
//   - a structure or the enemy road leaves the game camera's view, at 16:9, 2.2:1 (phone landscape) and 1.2:1 (Fold)
//   - a structure or the road sits under the HUD (labels top left, buttons top right, gate bar bottom centre)
//   - a plot's roof line, the plaza or any stretch of the road is hidden behind the gatehouse, the wall or a tower
// Batch:  Unity -batchmode -quit -projectPath . -executeMethod Hearthgate.EditorTools.LayoutCheck.Run
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Hearthgate.Game;

namespace Hearthgate.EditorTools
{
    public static class LayoutCheck
    {
        struct Item { public string name; public Bounds b; public List<Vector3> pts; }

        static readonly List<Vector3> lastPts = new List<Vector3>();
        static Bounds Measure(GameObject prefab, Vector3 at, Quaternion rot)
        {
            var g = Object.Instantiate(prefab, at, rot * prefab.transform.rotation);
            var rs = g.GetComponentsInChildren<Renderer>();
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            lastPts.Clear();
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>())
            {
                var vs = mf.sharedMesh.vertices; int step = Mathf.Max(1, vs.Length / 600);   // a sample of the silhouette
                for (int i = 0; i < vs.Length; i += step) lastPts.Add(mf.transform.TransformPoint(vs[i]));
            }
            Object.DestroyImmediate(g);
            return b;
        }
        static List<Vector3> Pts() => new List<Vector3>(lastPts);

        // a placed copy of a model with mesh colliders, so sight lines are tested against the real shape, not its box
        static readonly List<GameObject> placed = new List<GameObject>();
        static void PlaceSolid(GameObject prefab, Vector3 at, Quaternion rot)
        {
            var g = Object.Instantiate(prefab, at, rot * prefab.transform.rotation); placed.Add(g);
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>()) mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }

        static bool Overlap(Bounds a, Bounds b, float slack = 0.05f) =>
            a.min.x + slack < b.max.x && b.min.x + slack < a.max.x && a.min.z + slack < b.max.z && b.min.z + slack < a.max.z;

        [MenuItem("Hearthgate/Check the layout")]
        public static void Run()
        {
            var art = AssetDatabase.LoadAssetAtPath<ArtSet>("Assets/Art/HearthgateArt.asset");
            var bad = new List<string>();
            if (art == null) { Fail(new List<string> { "no art set" }); return; }

            var items = new List<Item>();
            items.Add(new Item { name = "gatehouse", b = Measure(art.gate, new Vector3(0, 0, Field.GATE_Z), Quaternion.identity), pts = Pts() });
            for (int i = 0; i < 4; i++)
            {
                var at = new Vector3(Field.TOWER_X[i], 0, Field.TOWER_Z);
                var tb = Measure(art.towerBase, at, Quaternion.identity); var tp = Pts();
                foreach (var top in new[] { art.topStone, art.topFire, art.topFrost, art.topStorm })
                { tb.Encapsulate(Measure(top, new Vector3(at.x, Field.TOWER_TOP, at.z), Quaternion.identity)); tp.AddRange(lastPts); }
                items.Add(new Item { name = "tower " + i, b = tb, pts = tp });
            }
            items.Add(new Item { name = "fountain", b = Measure(art.fountain, Field.PLAZA, Quaternion.identity), pts = Pts() });
            for (int i = 0; i < art.buildings.Length; i++)
            {
                var at = Field.Plot(i);
                var rot = Field.PLOT_FACING;
                var b = Measure(art.buildings[i], at, rot); var pp = Pts(); b.Encapsulate(Measure(art.ruin, at, rot)); pp.AddRange(lastPts);
                items.Add(new Item { name = "plot " + Hearthgate.Core.HG.BUILDING_IDS[i], b = b, pts = pp });
            }
            // the road in front of the wall is open ground
            // (the strip next to the wall, where foot rubble and grass sit, is left out)
            var roadBox = new Bounds(new Vector3(-(Field.SPAWN_Z - 2) / 2, 0.5f, Field.ROAD_Z + 0.5f), new Vector3(Field.SPAWN_Z + 2, 1, 2 * Field.ROAD_HALF - 1f));

            // 1. footprints
            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i + 1; j < items.Count; j++)
                    if (Overlap(items[i].b, items[j].b)) bad.Add($"{items[i].name} overlaps {items[j].name} ({R(items[i].b)} / {R(items[j].b)})");
                if (Overlap(items[i].b, roadBox)) bad.Add($"{items[i].name} stands on the road");
            }
            var wall = Measure(art.wall, new Vector3(0, 0, Field.TOWER_Z), Quaternion.identity);
            foreach (var it in items.Where(x => x.name.StartsWith("plot") || x.name == "fountain"))
                if (Overlap(it.b, wall)) bad.Add($"{it.name} overlaps the wall");

            // 2 and 3. on screen and clear of the HUD, at three window shapes
            var road = new List<Vector3>();
            for (float z = Field.FRONT_Z - 3f; z <= Field.SPAWN_Z; z += 1.5f)
                foreach (float x in new[] { -Field.LANE, Field.LANE }) { road.Add(Field.Road(z, x)); road.Add(Field.Road(z, x, 1.8f)); }
            foreach (var (w, h) in new[] { (1280, 720), (1600, 727), (1080, 900) })
            {
                var cam = new GameObject("check cam").AddComponent<Camera>();
                cam.aspect = (float)w / h; cam.fieldOfView = Field.FovFor(cam.aspect);
                cam.transform.position = Field.CAM_POS; cam.transform.LookAt(Field.CAM_LOOK);
                var hud = Hud(w, h);
                foreach (var it in items)
                {
                    var c = it.pts.Select(p => (Vector2)cam.WorldToViewportPoint(p)).ToArray();   // the model's own vertices
                    Rect r = Rect.MinMaxRect(c.Min(p => p.x), c.Min(p => p.y), c.Max(p => p.x), c.Max(p => p.y));
                    if (r.xMin < 0.005f || r.yMin < 0.005f || r.xMax > 0.995f || r.yMax > 0.995f) bad.Add($"{w}x{h}: {it.name} leaves the view ({R(r)})");
                    foreach (var (hn, hr) in hud)
                    {
                        int n = c.Count(p => hr.Contains(p));
                        if (n > 0) bad.Add($"{w}x{h}: {it.name} is under the {hn} ({n} points)");
                    }
                }
                foreach (var p in road.Select(p => (Vector2)cam.WorldToViewportPoint(p)))
                {
                    if (p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1) { bad.Add($"{w}x{h}: the road leaves the view at {p}"); break; }
                    var hit = hud.FirstOrDefault(x => x.Item2.Contains(p));
                    if (hit.Item1 != null) { bad.Add($"{w}x{h}: the road runs under the {hit.Item1} at {p}"); break; }
                }
                Object.DestroyImmediate(cam.gameObject);
            }

            // 4. sight lines: every plot's roof line, the plaza and every stretch of the road are seen past the gatehouse,
            // the wall and the towers (the city shows over the wall; nothing stands between the camera and the fight)
            // solid copies of the gatehouse, the towers (with their tallest cap), the wall and every built plot
            PlaceSolid(art.gate, new Vector3(0, 0, Field.GATE_Z), Quaternion.identity);
            PlaceSolid(art.wall, new Vector3(0, 0, Field.TOWER_Z), Quaternion.identity);
            for (int i = 0; i < 4; i++)
            {
                PlaceSolid(art.towerBase, new Vector3(Field.TOWER_X[i], 0, Field.TOWER_Z), Quaternion.identity);
                PlaceSolid(art.topFrost, new Vector3(Field.TOWER_X[i], Field.TOWER_TOP, Field.TOWER_Z), Quaternion.identity);
            }
            for (int i = 0; i < art.buildings.Length; i++) PlaceSolid(art.buildings[i], Field.Plot(i), Field.PLOT_FACING);
            Physics.SyncTransforms();
            string blocker = null;
            bool Hidden(Vector3 target)
            {
                var o = Field.CAM_POS; var d = target - o; float L = d.magnitude;
                bool hit = Physics.Raycast(o, d / L, out var info, L - 0.4f);
                blocker = hit ? info.collider.transform.root.name : null; return hit;
            }
            foreach (var it in items.Where(x => x.name.StartsWith("plot")))
            {
                var b = it.b; float roof = b.min.y + Mathf.Min(3f, b.size.y * 0.7f);
                foreach (float fx in new[] { -0.3f, 0f, 0.3f })
                    if (Hidden(new Vector3(b.center.x + fx * b.size.x, roof, b.max.z + 0.3f))) { bad.Add($"{it.name}'s roof line is hidden behind {blocker} (at {fx:+0.0;-0.0;0} of its width)"); break; }
            }
            if (Hidden(Field.PLAZA + new Vector3(0, 1.2f, 1.5f))) bad.Add("the plaza is hidden behind the gatehouse or the wall");
            foreach (var p in road) if (p.y > 1f && Hidden(p)) { bad.Add($"the road is hidden at {p}"); break; }

            foreach (var g in placed) Object.DestroyImmediate(g); placed.Clear();
            if (bad.Count == 0) { Debug.Log($"[Hearthgate] layout check passed ({items.Count} structures, 3 window shapes)"); return; }
            Fail(bad);
        }

        // the HUD's fixed boxes in viewport space, from Hud.cs (reference 1280x720, CanvasScaler match 0.5)
        static List<(string, Rect)> Hud(int w, int h)
        {
            float s = Mathf.Sqrt(w / 1280f * (h / 720f));
            // pixel boxes measured from the screen corner they are anchored to
            Rect TopLeft(float x1, float y1) => Rect.MinMaxRect(0, 1 - y1 * s / h, x1 * s / w, 1);
            Rect TopRight(float x0, float y0, float x1, float y1) => Rect.MinMaxRect(1 - x1 * s / w, 1 - y1 * s / h, 1 - x0 * s / w, 1 - y0 * s / h);
            return new List<(string, Rect)> {
                ("labels", TopLeft(300, 140)),                       // wave, Resonance, rate
                ("buttons", TopRight(24, 24, 324, 172)),             // Upgrade and Rebuild
                ("gate bar", Rect.MinMaxRect(0.5f - 180 * s / w, 31 * s / h, 0.5f + 180 * s / w, 49 * s / h)),
            };
        }

        static IEnumerable<Vector3> Corners(Bounds b)
        {
            for (int i = 0; i < 8; i++) yield return new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
        }
        static string R(Bounds b) => $"x {b.min.x:0.0}..{b.max.x:0.0} z {b.min.z:0.0}..{b.max.z:0.0}";
        static string R(Rect r) => $"x {r.xMin:0.00}..{r.xMax:0.00} y {r.yMin:0.00}..{r.yMax:0.00}";

        static void Fail(List<string> bad)
        {
            foreach (var s in bad) Debug.LogError("[Hearthgate] layout: " + s);
            Debug.LogError($"[Hearthgate] layout check FAILED ({bad.Count} problems)");
            if (Application.isBatchMode) EditorApplication.Exit(1);
        }
    }
}
