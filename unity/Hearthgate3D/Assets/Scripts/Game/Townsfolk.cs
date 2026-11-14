// The townsfolk (game-polish rule 9, a world that feels alive; owner, Nov 14 2026: "lets clean it up" and the fresh-eyes
// review: "the town interior is lifeless"). Villagers walk the square's paving on the walks in Field.WALKS (from
// tools/blender/hg_layout.py, checked to stay on the cobbles and clear of every stall, bench and lamp): round and round a
// loop, or there and back along a spoke to a door, pausing now and then to look about. A villager stops for the wizard
// rather than walk through him. Models from the Blender kit (three outfits); the step is a hop and a sway, like his.
using System.Collections.Generic;
using UnityEngine;

namespace Hearthgate.Game
{
    public class Townsfolk : MonoBehaviour
    {
        const float SCALE = 0.62f;   // the kit builds them at the wizard's units: about 1.65 m tall
        class Folk { public Transform t; public int walk; public float s, speed, dir = 1, pause, step, yaw, phase; }
        readonly List<Folk> folk = new List<Folk>();
        readonly List<float[]> lengths = new List<float[]>();

        public void Init(ArtSet art)
        {
            foreach (var w in Field.WALKS)
            {
                var c = new float[w.Length]; for (int k = 1; k < w.Length; k++) c[k] = c[k - 1] + Vector2.Distance(w[k - 1], w[k]);
                lengths.Add(c);
            }
            var rnd = new System.Random(14);
            // two on each loop round the square, one on each spoke
            var plan = new List<int>();
            for (int w = 0; w < Field.WALKS.Length; w++) { plan.Add(w); if (Loop(w)) plan.Add(w); }
            int n = 0;
            foreach (int w in plan)
            {
                GameObject prefab = null;
                if (art && art.villagers != null && art.villagers.Length > 0) prefab = art.villagers[n % art.villagers.Length];
                if (prefab == null && art) prefab = art.villager;
                var go = Art.Spawn(prefab, "villager", Vector3.zero, transform); go.transform.localScale = Vector3.one * SCALE;
                var L = lengths[w]; var f = new Folk { t = go.transform, walk = w, speed = 1.0f + (float)rnd.NextDouble() * 0.35f, phase = (float)rnd.NextDouble() * 6f };
                f.s = L[L.Length - 1] * (float)rnd.NextDouble(); f.dir = rnd.NextDouble() < 0.5 ? 1 : -1;
                folk.Add(f); n++;
            }
        }
        static bool Loop(int w) { var p = Field.WALKS[w]; return Vector2.Distance(p[0], p[p.Length - 1]) < 0.01f; }
        Vector2 At(int w, float s, out Vector2 dir)
        {
            var p = Field.WALKS[w]; var c = lengths[w];
            for (int k = 1; k < p.Length; k++)
                if (s <= c[k] || k == p.Length - 1)
                {
                    float u = Mathf.InverseLerp(c[k - 1], c[k], s); dir = (p[k] - p[k - 1]).normalized;
                    return Vector2.Lerp(p[k - 1], p[k], u);
                }
            dir = Vector2.up; return p[0];
        }

        void Update()
        {
            float dt = Time.deltaTime; var wz = Wizard.I;
            foreach (var f in folk)
            {
                var c = lengths[f.walk]; float total = c[c.Length - 1];
                var here = At(f.walk, f.s, out var d0);
                bool blocked = false;
                if (wz)
                {   // stop for the wizard when he is just ahead
                    var to = new Vector2(wz.Position.x, wz.Position.z) - here;
                    blocked = to.magnitude < 1.3f && Vector2.Dot(to, d0 * f.dir) > 0;
                }
                if (f.pause > 0) f.pause -= dt;
                else if (!blocked)
                {
                    f.s += f.speed * f.dir * dt;
                    if (Loop(f.walk)) f.s = Mathf.Repeat(f.s, total);
                    else if (f.s > total || f.s < 0) { f.s = Mathf.Clamp(f.s, 0, total); f.dir = -f.dir; f.pause = 1.5f + Random.value * 2.5f; }
                    else if (Random.value < dt * 0.04f) f.pause = 1.2f + Random.value * 2f;   // now and then: a look about
                }
                bool moving = f.pause <= 0 && !blocked;
                var p = At(f.walk, f.s, out var d);
                float want = Mathf.Atan2(-d.x * f.dir, -d.y * f.dir) * Mathf.Rad2Deg;   // models face -Z
                if (!moving && f.pause > 0) want += Mathf.Sin(Time.time * 0.8f + f.phase) * 50f;   // looking about
                f.yaw = Mathf.MoveTowardsAngle(f.yaw, want, 240f * dt);
                f.step += moving ? dt * f.speed * 8.5f : 0;
                float hop = moving ? Mathf.Abs(Mathf.Sin(f.step)) * 0.06f : 0;
                f.t.position = new Vector3(p.x, hop, p.y);
                f.t.rotation = Quaternion.Euler(moving ? 5f : 0, f.yaw, moving ? Mathf.Sin(f.step) * 3.5f : 0);
            }
        }
    }
}
