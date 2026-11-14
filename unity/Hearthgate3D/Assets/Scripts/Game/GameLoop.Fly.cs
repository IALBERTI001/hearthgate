// Butterflies (js/game.js; owner Nov 6 2026: "add some interaction for the user with the butterflies", Nov 14: "Butterflies
// can be polished better"). Once the city has begun, one flutters over the meadow every half minute or so: it flaps in
// bursts and glides, drifts on a wandering line, and now and then settles on the grass, its wings slowly opening and
// closing (the easy moment to tap it). A tap catches it: a sparkle, a little Resonance (the Golden Monarch pays
// Starshards), and it flies home over the walls to the fountain, where every kind caught flutters round from then on and
// sometimes rests on the rim. The model (hg_front.butterfly) has its wings hinged at the body and white so each kind is
// tinted; dark veins, rim and pale eye-spots stay.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class GameLoop
    {
        class Flier
        {
            public HG.Fly kind; public Transform tr, wL, wR; public Quaternion rL, rR;
            public Vector3 home, vel, perchAt; public float t, life, phase, flap, scale, perch, nextPerch, glide;
            public bool caught, garden;
        }
        readonly List<Flier> meadow = new List<Flier>(), garden = new List<Flier>();
        float flyT = 12f;
        readonly System.Random flyRnd = new System.Random();
        const float MEADOW_SCALE = 3.0f, GARDEN_SCALE = 0.75f;   // the Garden is seen close from the town walk: 1.6 filled the camera (owner, Nov 14 2026)

        Flier MakeFly(HG.Fly k, float scale)
        {
            if (art == null || !art.butterfly) return null;   // no stand-in shapes: without the model there are no butterflies
            var go = Instantiate(art.butterfly); go.name = "butterfly " + k.id;
            var col = new Color(k.r, k.g, k.b) * (k.rare ? 1.2f : 1f);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var ms = r.materials;
                for (int i = 0; i < ms.Length; i++) if (ms[i].name.Contains("flower_w")) ms[i].color = col;
                r.materials = ms;
            }
            go.transform.SetParent(world, false); go.transform.localScale = Vector3.one * scale;
            var f = new Flier { kind = k, tr = go.transform, scale = scale, phase = Random.value * 10, nextPerch = Random.Range(4f, 8f) };
            foreach (var c in go.GetComponentsInChildren<Transform>(true))
            {
                if (c.name == "wing_L") { f.wL = c; f.rL = c.localRotation; }
                if (c.name == "wing_R") { f.wR = c; f.rR = c.localRotation; }
            }
            return f;
        }

        void TickFlies(float dt)
        {
            // the meadow: a new one every 25 to 45 s once a building stands, two at most
            if (P.restored.Count > 0 && !P.fallen && (flyT -= dt) <= 0 && meadow.Count(m => !m.caught) < 2 && !(Wizard.I && Wizard.I.On))
            {
                flyT = Random.Range(25f, 45f);
                var f = MakeFly(HG.FlyPick(P, flyRnd), MEADOW_SCALE);
                if (f != null)
                {
                    f.home = new Vector3(Random.Range(-15f, 15f), Random.Range(2.4f, 3.6f), Random.Range(6f, 15f));   // over the meadow before the walls
                    f.life = 24f; f.tr.position = f.home + new Vector3(Random.value < 0.5f ? -26 : 26, 3, 4); meadow.Add(f);
                    if (!told.Contains("fly")) { told.Add("fly"); Hud.Banner("A butterfly! Tap it to catch it"); }
                }
            }
            for (int i = meadow.Count - 1; i >= 0; i--)
            {
                var f = meadow[i]; f.t += dt;
                if (f.caught)
                {   // home over the walls to the fountain, sparkling; then it joins the Garden there
                    var to = Field.PLAZA + Vector3.up * 3f;
                    Steer(f, to, dt, 9f);
                    if (Random.value < dt * 10f) Fx.I.Burst(f.tr.position, Field.GOLD, 1);
                    if ((f.tr.position - to).magnitude < 1.5f || f.t > 6f) { Destroy(f.tr.gameObject); meadow.RemoveAt(i); }
                    continue;
                }
                if (f.t > f.life)
                {   // off over the hills
                    Steer(f, f.home + new Vector3(0, 14, -34), dt, 7f);
                    if (f.t > f.life + 6f) { Destroy(f.tr.gameObject); meadow.RemoveAt(i); }
                    continue;
                }
                if (f.perch > 0)
                {   // resting on the grass: wings slowly opening and closing
                    f.perch -= dt; f.tr.position = Vector3.Lerp(f.tr.position, f.perchAt, dt * 4f); f.vel = Vector3.zero;
                    Wings(f, 10f + 60f * (0.5f + 0.5f * Mathf.Sin(f.t * 1.6f)));
                    if (f.perch <= 0) { f.nextPerch = f.t + Random.Range(5f, 9f); f.home = f.perchAt + new Vector3(Random.Range(-5f, 5f), 3f, Random.Range(-2f, 3f)); f.perchAt = default; }
                    continue;
                }
                if (f.t > f.nextPerch && f.perchAt == default)
                {   // pick a spot on the meadow ahead and drop to it
                    f.perchAt = new Vector3(Mathf.Clamp(f.tr.position.x + Random.Range(-4f, 4f), -18f, 18f), 0.15f * f.scale, Mathf.Clamp(f.tr.position.z + Random.Range(-2f, 3f), 4f, 16f));
                }
                if (f.perchAt != default)
                {
                    Steer(f, f.perchAt + Vector3.up * 0.3f, dt, 3.5f);
                    if ((f.tr.position - f.perchAt).magnitude < 0.6f) f.perch = Random.Range(2.5f, 4.5f);
                    continue;
                }
                Steer(f, f.home + Wander(f, 3.5f), dt, 3.2f);
            }
            // the Garden: every kind caught circles the fountain, and now and then rests on its rim
            var want = HG.BUTTERFLIES.Where(k => P.flies.Contains(k.id) && !meadow.Any(m => m.caught && m.kind == k)).ToList();   // one still flying home joins when it lands
            if (garden.Count != want.Count)
            {
                foreach (var g in garden) if (g.tr) Destroy(g.tr.gameObject);
                garden.Clear();
                foreach (var k in want) { var f = MakeFly(k, GARDEN_SCALE); if (f == null) break; f.garden = true; f.home = Field.PLAZA + Vector3.up * 2.6f; f.tr.position = Field.PLAZA + Vector3.up * 3f; garden.Add(f); }
            }
            for (int i = 0; i < garden.Count; i++)
            {
                var f = garden[i]; f.t += dt;
                if (f.perch > 0)
                {
                    f.perch -= dt; f.tr.position = Vector3.Lerp(f.tr.position, f.perchAt, dt * 4f); f.vel = Vector3.zero;
                    Wings(f, 10f + 60f * (0.5f + 0.5f * Mathf.Sin(f.t * 1.6f)));
                    if (f.perch <= 0) { f.perchAt = default; f.nextPerch = f.t + Random.Range(8f, 16f); }
                    continue;
                }
                if (f.t > f.nextPerch && f.perchAt == default)
                {   // the fountain's rim
                    float a = Random.value * Mathf.PI * 2; f.perchAt = Field.PLAZA + new Vector3(Mathf.Cos(a) * 2.3f, 1.27f, Mathf.Sin(a) * 2.3f);   // on the rim's coping (hg_front.fountain)
                }
                if (f.perchAt != default)
                {
                    Steer(f, f.perchAt, dt, 2.5f);
                    if ((f.tr.position - f.perchAt).magnitude < 0.3f) f.perch = Random.Range(3f, 6f);
                    continue;
                }
                float ang = f.t * 0.45f + i * Mathf.PI * 2 / garden.Count;
                Steer(f, f.home + new Vector3(Mathf.Cos(ang) * 3.8f, Mathf.Sin(f.t * 1.3f + f.phase) * 0.6f, Mathf.Sin(ang) * 3.8f), dt, 2.6f);
            }
        }
        Vector3 Wander(Flier f, float r) => new Vector3(Mathf.Sin(f.t * 0.7f + f.phase) * r, Mathf.Sin(f.t * 1.9f + f.phase) * 0.7f, Mathf.Cos(f.t * 0.53f + f.phase * 2) * r * 0.6f);

        // the wings: the angle each lifts above flat (0) at its hinge; + is up
        static void Wings(Flier f, float up)
        {
            if (f.wL) f.wL.localRotation = f.rL * Quaternion.Euler(0, 0, -up);
            if (f.wR) f.wR.localRotation = f.rR * Quaternion.Euler(0, 0, up);
            if (!f.wL) f.tr.localScale = new Vector3(f.scale * (0.25f + 0.75f * Mathf.Cos(up * Mathf.Deg2Rad)), f.scale, f.scale);   // an old model: fold its width
        }
        // flight: eased toward the goal with a flutter bob; the wings beat in bursts and hold open in short glides; the body
        // faces where it goes and banks into turns
        void Steer(Flier f, Vector3 goal, float dt, float speed)
        {
            var p = f.tr.position; var d = goal - p;
            var was = f.vel; f.vel = Vector3.Lerp(f.vel, Vector3.ClampMagnitude(d, speed), dt * 2.2f);
            if ((f.glide -= dt) < -Random.Range(1.2f, 2.4f)) f.glide = Random.Range(0.3f, 0.7f);   // a short glide every second or two
            bool gliding = f.glide > 0;
            f.flap += dt * (gliding ? 0f : 13f);
            float beat = Mathf.Sin(f.flap);
            p += f.vel * dt; p.y += (gliding ? -0.25f : beat * 0.9f) * dt; f.tr.position = p;   // each beat lifts it, a glide sinks
            Wings(f, gliding ? 28f : 25f + 50f * beat);
            var flat = new Vector3(f.vel.x, 0, f.vel.z);
            if (flat.sqrMagnitude > 0.01f)
            {
                float turn = Vector3.SignedAngle(new Vector3(was.x, 0, was.z), flat, Vector3.up) / Mathf.Max(dt, 0.001f);
                var look = Quaternion.LookRotation(-flat.normalized) * Quaternion.Euler(0, 0, Mathf.Clamp(-turn * 0.15f, -30f, 30f));   // models face -Z
                f.tr.rotation = Quaternion.Slerp(f.tr.rotation, look, dt * 4f);
            }
        }

        bool TapFly(Vector2 screen)
        {
            var cam = Camera.main; if (cam == null) return false;
            foreach (var f in meadow)
            {
                if (f.caught) continue;
                var s = cam.WorldToScreenPoint(f.tr.position);
                if (s.z < 0 || Vector2.Distance(screen, s) > 75f * Screen.height / 720f) continue;
                CatchFly(f); return true;
            }
            return false;
        }
        void CatchFly(Flier f)
        {
            var at = f.tr.position; f.caught = true; f.t = 0; f.perch = 0;
            bool fresh = !P.flies.Contains(f.kind.id);
            P.AddStat("flies", 1);
            var col = new Color(f.kind.r, f.kind.g, f.kind.b);
            Fx.I.Burst(at, col, 22); Fx.I.Burst(at, Field.GOLD, 10); Sound.Play("claim", 0.5f);
            if (f.kind.rare) GiveShards(2, at + Vector3.up);
            else { double amt = HG.FightRate(P, B) * 1.6 * 3 * SurgeX * (f.kind.rare ? 5 : 1); P.res += amt; Fx.I.Pop(at + Vector3.up, "+" + HG.Fmt(amt), Field.GOLD, 34, 1.1f); }
            if (fresh) { P.flies.Add(f.kind.id); Hud.Banner($"New in the Garden: {f.kind.name}! {P.flies.Count}/{HG.BUTTERFLIES.Length}"); }
            Save();
        }
    }
}
