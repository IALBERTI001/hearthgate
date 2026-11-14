// Butterflies (js/game.js, owner Nov 6 2026: "add some interaction for the user with the butterflies"): once the city has
// begun, one flutters over the meadow every half minute or so. A tap catches it for a little Resonance (the Golden Monarch
// pays Starshards); every kind caught joins the Garden in the Library and flutters round the fountain from then on.
// The butterfly is a Blender model (hg_front.butterfly) with white wings, tinted to its kind; it flaps by folding its width.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class GameLoop
    {
        class Flier { public HG.Fly kind; public Transform tr; public Vector3 home, vel; public float t, life, phase; }
        readonly List<Flier> meadow = new List<Flier>(), garden = new List<Flier>();
        float flyT = 12f;
        readonly System.Random flyRnd = new System.Random();

        Transform MakeFly(HG.Fly k, float scale)
        {
            GameObject go;
            if (art != null && art.butterfly) go = Instantiate(art.butterfly);
            else return null;   // no stand-in shapes: without the model there are no butterflies
            go.name = "butterfly " + k.id;
            var col = new Color(k.r, k.g, k.b);
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var ms = r.materials;
                for (int i = 0; i < ms.Length; i++) if (ms[i].name.Contains("flower_w")) ms[i].color = col * (k.rare ? 1.25f : 1f);
                r.materials = ms;
            }
            go.transform.SetParent(world, false); go.transform.localScale = Vector3.one * scale;
            return go.transform;
        }

        void TickFlies(float dt)
        {
            // the meadow: a new one every 25 to 45 s once a building stands, two at most
            if (P.restored.Count > 0 && (flyT -= dt) <= 0 && meadow.Count < 2 && !(Wizard.I && Wizard.I.On))
            {
                flyT = Random.Range(25f, 45f);
                var k = HG.FlyPick(P, flyRnd); var tr = MakeFly(k, 3.6f);   // big enough to see and tap from the walls (1.3 read as 8 px)
                if (tr != null)
                {
                    var home = new Vector3(Random.Range(-16f, 16f), Random.Range(2.2f, 3.6f), Random.Range(6f, 16f));   // over the meadow before the walls
                    var f = new Flier { kind = k, tr = tr, home = home, life = 20f, phase = Random.value * 10 };
                    tr.position = home + new Vector3(Random.value < 0.5f ? -25 : 25, 2, 0); meadow.Add(f);
                    if (!told.Contains("fly")) { told.Add("fly"); Hud.Banner("A butterfly! Tap it to catch it"); }
                }
            }
            for (int i = meadow.Count - 1; i >= 0; i--)
            {
                var f = meadow[i]; f.t += dt;
                var goal = f.t < f.life ? f.home + Wander(f, 3f) : f.home + new Vector3(0, 12, -30);   // then off over the hills
                Steer(f, goal, dt, f.t < f.life ? 3.2f : 7f);
                if (f.t > f.life + 5f) { Destroy(f.tr.gameObject); meadow.RemoveAt(i); }
            }
            // the Garden: every kind caught circles the fountain
            var want = HG.BUTTERFLIES.Where(k => P.flies.Contains(k.id)).ToList();
            if (garden.Count != want.Count)
            {
                foreach (var g in garden) if (g.tr) Destroy(g.tr.gameObject);
                garden.Clear();
                foreach (var k in want) { var tr = MakeFly(k, 1.8f); if (tr == null) break; garden.Add(new Flier { kind = k, tr = tr, phase = Random.value * 10, home = Field.PLAZA + Vector3.up * 2.6f }); tr.position = Field.PLAZA + Vector3.up * 3f; }
            }
            for (int i = 0; i < garden.Count; i++)
            {
                var f = garden[i]; f.t += dt; float a = f.t * 0.45f + i * Mathf.PI * 2 / garden.Count;
                var goal = f.home + new Vector3(Mathf.Cos(a) * 3.6f, Mathf.Sin(f.t * 1.3f + f.phase) * 0.5f, Mathf.Sin(a) * 3.6f);
                Steer(f, goal, dt, 2.5f);
            }
        }
        Vector3 Wander(Flier f, float r) => new Vector3(Mathf.Sin(f.t * 0.7f + f.phase) * r, Mathf.Sin(f.t * 1.9f + f.phase) * 0.6f, Mathf.Cos(f.t * 0.53f + f.phase * 2) * r * 0.6f);
        // a butterfly's flight: eased toward its goal with a bob, facing where it goes, wings folding and opening
        void Steer(Flier f, Vector3 goal, float dt, float speed)
        {
            var p = f.tr.position; var d = goal - p;
            f.vel = Vector3.Lerp(f.vel, Vector3.ClampMagnitude(d, speed), dt * 2f);
            p += f.vel * dt; p.y += Mathf.Sin(f.t * 9f + f.phase) * dt * 0.8f; f.tr.position = p;
            var flat = new Vector3(f.vel.x, 0, f.vel.z);
            if (flat.sqrMagnitude > 0.01f) f.tr.rotation = Quaternion.Slerp(f.tr.rotation, Quaternion.LookRotation(-flat.normalized) , dt * 4f);   // the model's head is -y in Blender, -z here
            float s = f.tr.localScale.y, flap = 0.25f + 0.75f * Mathf.Abs(Mathf.Sin(f.t * 11f + f.phase));
            f.tr.localScale = new Vector3(s * flap, s, s);
        }

        bool TapFly(Vector2 screen)
        {
            var cam = Camera.main; if (cam == null) return false;
            foreach (var f in meadow)
            {
                var s = cam.WorldToScreenPoint(f.tr.position);
                if (s.z < 0 || Vector2.Distance(screen, s) > 70f * Screen.height / 720f) continue;
                CatchFly(f); return true;
            }
            return false;
        }
        void CatchFly(Flier f)
        {
            var at = f.tr.position; meadow.Remove(f); Destroy(f.tr.gameObject);
            bool fresh = !P.flies.Contains(f.kind.id); if (fresh) P.flies.Add(f.kind.id);
            P.AddStat("flies", 1);
            var col = new Color(f.kind.r, f.kind.g, f.kind.b);
            Fx.I.Burst(at, col, 18); Sound.Play("claim", 0.5f);
            if (f.kind.rare && P.shopOpen) GiveShards(2, at + Vector3.up);
            else { double amt = HG.FightRate(P, B) * 1.6 * 3 * SurgeX * (f.kind.rare ? 5 : 1); P.res += amt; Fx.I.Pop(at + Vector3.up, "+" + HG.Fmt(amt), Field.GOLD, 34, 1.1f); }
            if (fresh) Hud.Banner($"New in the Garden: {f.kind.name}! {P.flies.Count}/{HG.BUTTERFLIES.Length}");
            Save();
        }
    }
}
