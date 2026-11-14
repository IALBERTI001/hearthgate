// The town's powers, from the 2D game (js/game.js; owner, Nov 14 2026: "we are still not there yet with game core"):
// every building does what its line says, and the city answers the fight where the player can see it.
//  - Resonance wisps: a kill's Resonance flies over the wall into the fountain and is paid when it lands (the 2D game's
//    wisps), so income is something you watch come home. In a Surge each kill sends two, rose-haloed.
//  - The Surge lives in the fountain: ready, it glows rose and a board over it says so; a tap (or X) makes everything pay
//    double for 3 minutes; it recharges 15 minutes after it ends (Basic Launch: no ads). Real-time, saved.
//  - Golden Wisps: once three buildings stand, one drifts across the field every 30 to 60 s (15 to 30 s with the Bakery,
//    whose wisps rise from its chimney first). Tap it: a big purse, or double pay for 15 s.
//  - The Tavern's Cheer: combos fill its meter; full, the towers fire twice as fast for 6 s.
//  - The Watchtower's beacon: a fifth tower, a flame in its lantern room that fires over the wall in every element.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class GameLoop
    {
        public const long SURGE_MS = 3 * 60 * 1000, SURGE_CD_MS = 15 * 60 * 1000;
        public static readonly Color ROSE = new Color(1f, 0.55f, 0.85f);
        long goldSurgeUntil;   // a Golden Wisp's 15 s of double pay (not saved)
        public float SurgeX => SurgeOn || Now() < goldSurgeUntil ? 2f : 1f;
        public bool SurgeOn => Now() < P.surgeUntil;
        public bool SurgeReady => !SurgeOn && Now() >= P.surgeUntil + SURGE_CD_MS;
        public long SurgeLeftMs => System.Math.Max(0, P.surgeUntil - Now());
        public long SurgeWaitMs => System.Math.Max(0, P.surgeUntil + SURGE_CD_MS - Now());
        public static readonly Vector3 FOUNTAIN_TOP = new Vector3(0, 2.6f, 0);

        // ---------- Resonance wisps ----------
        class Wisp { public Vector3 a, c, b; public float t, dur; public double amt; public Transform tr; }
        readonly List<Wisp> wisps = new List<Wisp>();
        void SendWisps(Vector3 from, double amt)
        {
            int n = SurgeX > 1 ? 2 : 1;
            if (wisps.Count > 40) { P.res += amt; return; }   // a huge swarm: pay at once rather than flood the sky
            var to = Field.PLAZA + FOUNTAIN_TOP;
            for (int k = 0; k < n; k++)
            {
                var go = new GameObject("wisp"); var tr = go.transform; tr.position = from + Vector3.up;
                var ball = Art.Shape(PrimitiveType.Sphere, Art.Flat((n > 1 ? ROSE : Field.GOLD) * 1.3f)); ball.transform.SetParent(tr, false); ball.transform.localScale = Vector3.one * 0.38f;
                var r = ball.GetComponent<Renderer>(); if (r) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var trail = go.AddComponent<TrailRenderer>(); trail.time = 0.35f; trail.startWidth = 0.28f; trail.endWidth = 0; trail.material = Art.Flat(n > 1 ? ROSE : Field.GOLD);
                trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var mid = (from + to) * 0.5f; float side = (k == 0 ? 1 : -1) * Random.Range(3f, 7f);
                var c = new Vector3(mid.x + side, Mathf.Max(from.y, to.y) + 13f + Random.Range(0f, 4f), mid.z);   // high over the wall
                wisps.Add(new Wisp { a = from + Vector3.up, c = c, b = to, t = 0, dur = 1.0f + Random.Range(0f, 0.35f) + k * 0.15f, amt = amt / n, tr = tr });
            }
        }
        void TickWisps(float dt)
        {
            for (int i = wisps.Count - 1; i >= 0; i--)
            {
                var w = wisps[i]; w.t += dt / w.dur; float u = Mathf.Min(1, w.t), e = u * u * (3 - 2 * u), v = 1 - e;
                w.tr.position = v * v * w.a + 2 * v * e * w.c + e * e * w.b;
                if (u >= 1)
                {
                    P.res += w.amt; fountainGlow = 1f; Destroy(w.tr.gameObject); wisps.RemoveAt(i);
                    Fx.I.Burst(Field.PLAZA + FOUNTAIN_TOP, SurgeX > 1 ? ROSE : Field.GOLD, 2); Sound.Play("claim", 0.06f);
                }
            }
        }
        public void FlushWisps() { foreach (var w in wisps) { P.res += w.amt; Destroy(w.tr.gameObject); } wisps.Clear(); }
        float fountainGlow;

        // ---------- the Surge ----------
        public void TapSurge()
        {
            if (SurgeOn) { Hud.Toast($"Surge: double pay for {Mmss(SurgeLeftMs)} more"); return; }
            if (!SurgeReady) { Sound.Dud(); Hud.Toast($"The fountain refills in {Mmss(SurgeWaitMs)}"); return; }
            P.surgeUntil = Now() + SURGE_MS; Sound.Play("surge", 0.6f); Fx.I.Shake(0.12f, true);
            Fx.I.Burst(Field.PLAZA + FOUNTAIN_TOP, ROSE, 36); Fx.I.Pop(Field.PLAZA + FOUNTAIN_TOP + Vector3.up * 2, "SURGE! x2", ROSE, 48, 1.4f);
            Hud.Banner("Surge! Everything pays double for 3 minutes"); Save();
        }
        public static string Mmss(long ms) { long s = (ms + 999) / 1000; return $"{s / 60}:{s % 60:00}"; }
        float surgeFxT;
        void TickFountain(float dt)
        {
            fountainGlow = Mathf.Max(0, fountainGlow - dt * 2f);
            if ((surgeFxT -= dt) > 0) return;
            if (SurgeOn) { surgeFxT = 0.12f; Fx.I.Burst(Field.PLAZA + FOUNTAIN_TOP + Random.insideUnitSphere * 0.8f, ROSE, 3); }        // it sprays rose light
            else if (SurgeReady && !P.firstSession) { surgeFxT = 0.45f; Fx.I.Burst(Field.PLAZA + FOUNTAIN_TOP + Random.insideUnitSphere * 1.2f, ROSE, 1); }   // ready: motes rise
            else surgeFxT = 0.5f;
        }

        // ---------- Golden Wisps ----------
        class Gold { public Transform tr; public Vector3 rise0; public float riseT, t, life = 9f, vx, y0, z; }
        Gold gold; float goldT = 8f;
        bool Has(string id) => P.restored.Contains(id);
        void TickGold(float dt)
        {
            if (P.restored.Count < 3) return;
            if (gold != null)
            {
                var g = gold; var p = g.tr.position;
                if (g.riseT > 0)
                {   // the Bakery's: up out of its chimney, then off over the wall
                    g.riseT = Mathf.Max(0, g.riseT - dt); float u = 1 - g.riseT / 2.2f, e = u * u * (3 - 2 * u);
                    var field = new Vector3(g.rise0.x * 0.6f, g.y0, g.z);
                    g.tr.position = Vector3.Lerp(g.rise0, field, e) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 6f;
                }
                else
                {
                    g.t += dt; p.x += g.vx * dt; p.y = g.y0 + Mathf.Sin(g.t * 2.4f) * 0.8f; p.z = Mathf.MoveTowards(p.z, g.z, dt * 4f); g.tr.position = p;
                    if (g.t > g.life || Mathf.Abs(p.x) > 40) { Destroy(g.tr.gameObject); gold = null; }
                }
                if (gold != null && Random.value < dt * 8) Fx.I.Burst(g.tr.position, Field.GOLD, 1);
                return;
            }
            if ((goldT -= dt) > 0) return;
            goldT = Random.Range(30f, 60f) * (Has("bakery") ? 0.5f : 1f);
            bool fromLeft = Random.value < 0.5f;   // +x is the screen's left
            var go = new GameObject("golden wisp"); var tr = go.transform;
            var ball = Art.Shape(PrimitiveType.Sphere, Art.Flat(new Color(1f, 0.86f, 0.35f) * 1.6f)); ball.transform.SetParent(tr, false); ball.transform.localScale = Vector3.one * 0.9f;
            var halo = Art.Shape(PrimitiveType.Sphere, Art.Flat(new Color(1f, 0.7f, 0.2f))); halo.transform.SetParent(tr, false); halo.transform.localScale = Vector3.one * 0.5f; halo.transform.localPosition = new Vector3(0, 0, 0.35f);
            foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var trail = go.AddComponent<TrailRenderer>(); trail.time = 0.6f; trail.startWidth = 0.5f; trail.endWidth = 0; trail.material = Art.Flat(new Color(1f, 0.8f, 0.3f));
            gold = new Gold { tr = tr, vx = (fromLeft ? -1 : 1) * Random.Range(6.5f, 8.5f), y0 = Random.Range(5.5f, 8f), z = Random.Range(5f, 13f) };
            tr.position = new Vector3(fromLeft ? 38 : -38, gold.y0, gold.z);
            if (Has("bakery") && Random.value < 0.6f)
            {
                int i = System.Array.IndexOf(HG.BUILDING_IDS, "bakery");
                gold.rise0 = Field.Plot(i) + Vector3.up * (Field.PlotHeight(i) + 0.5f); gold.riseT = 2.2f; tr.position = gold.rise0;
                gold.vx = Mathf.Abs(gold.vx) * (gold.rise0.x > 0 ? -1 : 1);
            }
            if (!told.Contains("gold")) { told.Add("gold"); Hud.Banner("A Golden Wisp! Tap it before it flies away"); }
        }
        readonly HashSet<string> told = new HashSet<string>();
        bool TapGold(Vector2 screen)
        {
            if (gold == null || Camera.main == null) return false;
            var s = Camera.main.WorldToScreenPoint(gold.tr.position);
            if (s.z < 0 || Vector2.Distance(screen, s) > 80f * Screen.height / 720f) return false;
            var at = gold.tr.position; Destroy(gold.tr.gameObject); gold = null;
            Sound.Play("claim", 0.6f); Fx.I.Burst(at, Field.GOLD, 26);
            if (Random.value < 0.3f) { goldSurgeUntil = Now() + 15000; Fx.I.Pop(at + Vector3.up, "x2 for 15 s!", ROSE, 42, 1.4f); }
            else { double amt = HG.FightRate(P, B) * 1.6 * 10 * SurgeX; P.res += amt; Fx.I.Pop(at + Vector3.up, "+" + HG.Fmt(amt), Field.GOLD, 46, 1.4f); }
            Save(); return true;
        }

        // ---------- the Tavern's Cheer ----------
        public float cheer, cheerT; const float CHEER_S = 6f;
        void FillCheer()
        {
            if (!Has("tavern") || cheerT > 0) return;
            cheer = Mathf.Min(1, cheer + 0.06f);
            if (cheer >= 1)
            {
                cheerT = CHEER_S; Sound.Play("feat", 0.5f); Hud.Banner("CHEER! Towers fire twice as fast");
                int i = System.Array.IndexOf(HG.BUILDING_IDS, "tavern"); Fx.I.Burst(Field.Door(i, 1f, 2f), Field.GOLD, 24);
            }
        }
        void TickCheer(float dt)
        {
            if (cheerT > 0 && (cheerT -= dt) <= 0) { cheerT = 0; cheer = 0; }
            if (cheerT > 0 && Random.value < dt * 10) foreach (var tw in towers) Fx.I.Burst(tw.Muzzle, Field.GOLD, 1);   // the towers glow gold
        }
        public float CheerShown => cheerT > 0 ? cheerT / CHEER_S : cheer;

        // ---------- the Watchtower's beacon ----------
        Tower beacon; Transform beaconFlame; int beaconShots;
        void RefreshBeacon()
        {
            bool on = Has("watchtower");
            if (!on) { if (beacon != null) { towers.Remove(beacon); if (beaconFlame) Destroy(beaconFlame.gameObject); beacon = null; } return; }
            if (beacon != null) return;
            int i = System.Array.IndexOf(HG.BUILDING_IDS, "watchtower"); var top = Field.Plot(i) + Vector3.up * (Field.PlotHeight(i) - 2.2f);
            beacon = new Tower { n = 4, x = top.x, z = top.z, muzzle = top, beacon = true };
            towers.Add(beacon);
            var fl = Art.Shape(PrimitiveType.Sphere, Art.Flat(Field.GOLD * 1.5f)); fl.name = "beacon"; fl.transform.SetParent(world, false);
            fl.transform.position = top; fl.transform.localScale = Vector3.one * 0.7f; beaconFlame = fl.transform;
            var r = fl.GetComponent<Renderer>(); if (r) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        // the beacon's element turns with every shot through the elements the city has
        string BeaconElement() { var els = Elements(); return els.Length == 0 ? "none" : els[beaconShots++ % els.Length]; }
        void TickBeacon(float dt)
        {
            if (beacon == null || !beaconFlame) return;
            float f = 0.7f + Mathf.Sin(Time.time * 9f) * 0.06f + Mathf.Max(0, beacon.flash) * 0.5f; beacon.flash = Mathf.Max(0, beacon.flash - dt * 4);
            beaconFlame.localScale = Vector3.one * f;
        }

        // ---------- the tap: the Golden Wisp first, then the fountain (from the walls, or the board over it) ----------
        void TapWorld()
        {
            if (!Input.GetMouseButtonDown(0) || Hud.OnHud(Hud.PointerPos) || Hud.Paused) return;
            var pt = Hud.PointerPos;
            if (TapGold(pt)) return;
            var cam = Camera.main; if (cam == null || (Wizard.I && Wizard.I.On)) return;
            var s = cam.WorldToScreenPoint(Field.PLAZA + Vector3.up * 1.2f);
            if (s.z > 0 && Vector2.Distance(pt, s) < 46f * Screen.height / 720f) TapSurge();
        }

        void TickTown(float dt)
        {
            TickWisps(dt); TickFountain(dt); TickGold(dt); TickCheer(dt); TickBeacon(dt);
        }
    }
}
