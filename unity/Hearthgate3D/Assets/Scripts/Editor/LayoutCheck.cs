// Layout check on the real models (owner, Nov 13 2026: "why is that gate so terrible. Two towers on each side of the
// entrance ... where is the quality testing?"). The gate's drum towers stood inside two element towers and no check
// looked. This places every structure where the game places it, measures the models' bounds, and fails when:
//   - two structures' footprints overlap (gatehouse, towers, buildings and ruins on every plot, fountain), or one stands
//     on a lane, or a plot or the fountain is not inside the ring wall with room to spare (owner, Nov 13 2026: "Close off
//     the loop around town to make a protected town")
//   - a structure or a lane leaves the game camera's view, at 16:9, 2.2:1 (phone landscape) and 1.2:1 (Fold)
//   - a structure or a lane sits under the HUD (labels top left, buttons top right, gate bar bottom centre)
//   - a plot's roof line, the plaza or any stretch of a lane is hidden behind the gatehouse, the wall or a tower
//   - a lane does not end at the foot of its own tower (owner: "monsters are walking up to the towers on a route in front
//     of each tower")
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
                var at = Field.Tower(i);
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
                var b = Measure(art.buildings[i], at, rot); var pp = Pts(); b.Encapsulate(Measure(art.Ruin(i), at, rot)); pp.AddRange(lastPts);
                items.Add(new Item { name = "plot " + Hearthgate.Core.HG.BUILDING_IDS[i], b = b, pts = pp });
            }
            // the lanes are open ground: centre-line points from just past the tower's foot to 9 m past the 16:9 start
            var laneGround = new List<(int, Vector3)>();
            for (int k = 0; k < 4; k++)
                for (float pr = 3f; pr <= Field.LaneLen(k) + 8f; pr += 1f) laneGround.Add((k, Field.Lane(k, pr, 0)));
            bool OnLane(Bounds b) => laneGround.Any(q => q.Item2.x > b.min.x - 1.8f && q.Item2.x < b.max.x + 1.8f && q.Item2.z > b.min.z - 1.8f && q.Item2.z < b.max.z + 1.8f);
            bool InRing(Vector3 p, float margin) => Mathf.Pow(p.x / (Field.RX - margin), 2) + Mathf.Pow((p.z + Field.RZ) / (Field.RZ - margin), 2) < 1f;

            // 1. footprints
            for (int i = 0; i < items.Count; i++)
            {
                for (int j = i + 1; j < items.Count; j++)
                    if (Overlap(items[i].b, items[j].b)) bad.Add($"{items[i].name} overlaps {items[j].name} ({R(items[i].b)} / {R(items[j].b)})");
                if (OnLane(items[i].b)) bad.Add($"{items[i].name} stands on a lane ({R(items[i].b)})");
            }
            foreach (var it in items.Where(x => x.name.StartsWith("plot") || x.name == "fountain"))
                if (Corners(it.b).Any(c => !InRing(c, 1.4f))) bad.Add($"{it.name} is not inside the ring wall ({R(it.b)})");
            // each lane ends at the foot of its own tower, in front of it (outside the ring)
            for (int k = 0; k < 4; k++)
            {
                var foot = Field.Lane(k, 0, 0); var tw = Field.Tower(k);
                float d = Vector2.Distance(new Vector2(foot.x, foot.z), new Vector2(tw.x, tw.z));
                if (d > 3.2f || InRing(foot, 0f)) bad.Add($"lane {k} does not end in front of tower {k} ({d:0.0} m from it)");
                for (int j = 0; j < 4; j++) if (j != k && Vector3.Distance(Field.Lane(k, 0, 0), Field.Tower(j)) < Vector3.Distance(foot, tw)) bad.Add($"lane {k} ends nearer tower {j}");
            }

            // 2 and 3. on screen and clear of the HUD, at three window shapes
            var road = new List<Vector3>();
            for (int k = 0; k < 4; k++)   // the part of each lane in view at 16:9 (it begins 1.5 m below the frame)
                for (float pr = 0f; pr <= Field.LaneLen(k) - 3f; pr += 1.5f)
                    foreach (float x in new[] { -Field.LANE, Field.LANE }) { road.Add(Field.Lane(k, pr, x)); road.Add(Field.Lane(k, pr, x, 1.8f)); }
            foreach (var (w, h) in new[] { (1280, 720), (1600, 727), (1080, 900) })
            {
                var cam = new GameObject("check cam").AddComponent<Camera>();
                cam.aspect = (float)w / h; cam.fieldOfView = Field.FovFor(cam.aspect);
                cam.transform.position = Field.CAM_POS; cam.transform.LookAt(Field.CAM_LOOK);
                var hud = Hud(w, h);
                foreach (var it in items.Where(x => !x.name.StartsWith("plot")))   // the town runs on beyond the top of the view (owner's sketch, human-scale town)
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
                    if (p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1) { bad.Add($"{w}x{h}: a lane leaves the view at {p}"); break; }
                    var hit = hud.FirstOrDefault(x => x.Item2.Contains(p));
                    if (hit.Item1 != null) { bad.Add($"{w}x{h}: a lane runs under the {hit.Item1} at {p}"); break; }
                }
                Object.DestroyImmediate(cam.gameObject);
            }

            // 4. sight lines: every plot's roof line, the plaza and every stretch of the road are seen past the gatehouse,
            // the wall and the towers (the city shows over the wall; nothing stands between the camera and the fight)
            // solid copies of the gatehouse, the towers (with their tallest cap), the wall and every built plot
            PlaceSolid(art.gate, new Vector3(0, 0, Field.GATE_Z), Quaternion.identity);
            PlaceSolid(art.wall, Vector3.zero, Quaternion.identity);
            for (int i = 0; i < 4; i++)
            {
                PlaceSolid(art.towerBase, Field.Tower(i), Quaternion.identity);
                PlaceSolid(art.topFrost, Field.Tower(i) + Vector3.up * Field.TOWER_TOP, Quaternion.identity);
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
            // the wizard waiting in the square is seen from the walls (a tap on him starts the town walk)
            if (Hidden(Wizard.HOME + Vector3.up * 1.2f)) bad.Add($"the wizard's spot in the square is hidden behind {blocker}");
            foreach (var p in road) if (p.y > 1f && Hidden(p)) { bad.Add($"a lane is hidden behind {blocker} at {p}"); break; }

            // 5. the town walk: wherever the wizard can stand, the walk camera sees him past the wall, the gatehouse and the
            // towers (from the wall side the fire cap filled the screen near the front, Nov 13 2026); buildings may hide him
            // for a step and are only counted
            int spots = 0, byBuilding = 0; string wallBlock = null;
            for (float x = -Field.RX + 2.5f; x <= Field.RX - 2.5f; x += 1.5f)
                for (float z = -2 * Field.RZ + 2.5f; z <= -3.2f; z += 1.5f)
                {
                    var p = new Vector3(x, 0, z);
                    if (!InRing(p, 2.4f) || Enumerable.Range(0, 10).Any(i => Wizard.InFootprint(i, p, 0.4f)) || Vector3.Distance(p, Field.PLAZA) < Wizard.FOUNTAIN_R) continue;
                    spots++;
                    var from = p + Wizard.FOLLOW; var to = p + Vector3.up * 1.3f; var d = to - from;
                    if (Physics.Raycast(from, d.normalized, out var hitInfo, d.magnitude - 0.5f))
                    {
                        var who = hitInfo.collider.transform.root.name;
                        if (who.StartsWith("building")) byBuilding++; else if (wallBlock == null) wallBlock = $"{who} at {p}";
                    }
                }
            if (wallBlock != null) bad.Add($"town walk: the camera loses the wizard behind {wallBlock}");
            Debug.Log($"[Hearthgate] town walk: {spots} spots, {byBuilding} briefly behind a building");

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
                ("labels", TopLeft(250, 118)),                       // wave, Resonance, rate
                ("buttons", TopRight(16, 16, 246, 134)),             // Upgrade and Rebuild (Hud.Btn: 230 x 56, 6 apart)
                ("gate bar", Rect.MinMaxRect(0.5f - 180 * s / w, 27 * s / h, 0.5f + 180 * s / w, 53 * s / h)),
                ("walk button", Rect.MinMaxRect(0.5f - 115 * s / w, 62 * s / h, 0.5f + 115 * s / w, 112 * s / h)),   // Hud: the town walk
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
