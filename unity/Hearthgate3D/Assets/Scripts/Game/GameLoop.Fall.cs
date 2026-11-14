// The walls fall (owner, Nov 14 2026: "if the player walls get destroyed, we do a loss screen of the walls and town in
// shambles and charge to restore the town (star shards); if they do not have enough star shards then we can give them
// the option of watching an ad to restore or start over at level 1"). The fight stops; every standing building shows as
// its ruin, the gate leans broken, the tower tops are gone and smoke and embers rise over the town. A card at the foot of
// the screen (the shambles stay in view above it) offers:
//  - Restore the town for Starshards (3 + 1 per 10 waves): all of it stands again and the same wave is fought again;
//  - without enough Starshards, a rewarded ad restores it instead (only where the CrazyGames SDK offers one: Basic Launch
//    has ads off, so the button is not shown there);
//  - Start over (asked twice): the run is lost as if this Ascension had just begun: towers, town and Resonance gone,
//    wave 1 (owner, Nov 14 2026: "progress locks after ascending"). Perks, stars, Starshards, Feats and looks stay.
// Nothing is lost while the card is up: a reload comes back to the same choice (P.fallen is saved).
using System.Runtime.InteropServices;
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class GameLoop
    {
        public bool Fallen => P.fallen;
        GameObject gateObj, breachObj; readonly System.Collections.Generic.List<GameObject> heaps = new System.Collections.Generic.List<GameObject>();
        readonly System.Collections.Generic.List<(Transform t, float age, float life, float size, Vector3 drift)> puffs = new System.Collections.Generic.List<(Transform, float, float, float, Vector3)>();
        float smokeT; Vector3 puffUnit;

        // a do-or-die price (owner, Nov 14 2026: "Rebuilding the city should be more expensive, possibly in the hundred or
        // hundreds of star shards"): 100, and 4 more for every wave reached
        public int FallCost => 100 + 4 * P.wave;
        bool rewinding;
        public bool CanPayRestore => P.shards >= FallCost;

        void GateFell()
        {
            if (Wizard.I && Wizard.I.On) Wizard.I.Toggle();   // back to the walls: the loss is seen from there
            foreach (var e in enemies) { Fx.I.Burst(e.t.position + Vector3.up, new Color(0.5f, 0.45f, 0.42f), 4); Destroy(e.t.gameObject); }
            enemies.Clear(); queue.Clear(); waveLive = false; betweenT = 2f;
            foreach (var s in shots) Destroy(s.t.gameObject); shots.Clear();
            P.fallen = true; gateHp = 0; Save();
            Fx.I.Shake(0.5f); Sound.Play("boss_horn", 0.45f);
            ShowShambles(true);
            Hud.ShowFallen();
        }

        // the town in shambles, or standing again
        void ShowShambles(bool on)
        {
            for (int i = 0; i < HG.BUILDINGS.Length; i++)
            {
                bool up = P.restored.Contains(HG.BUILDINGS[i].id);
                if (!up) continue;
                if (on)
                {
                    if (plots[i]) Destroy(plots[i]);
                    var prefab = art == null ? null : art.Ruin(i);
                    plots[i] = Art.Spawn(prefab, "ruin", Field.Plot(i), world); plots[i].transform.rotation = Field.PlotRot(i); uppers[i] = null;
                    Fx.I.Burst(Field.Plot(i) + Vector3.up * 2f, new Color(0.45f, 0.42f, 0.4f), 18);
                }
                else RefreshPlot(i);
            }
            foreach (var tw in towers) if (tw.top) tw.top.gameObject.SetActive(!on);
            if (on && !breachObj && art && art.breach) { breachObj = Instantiate(art.breach, world); breachObj.transform.position = new Vector3(0, 0, Field.GATE_Z); }
            else if (!on && breachObj) { Destroy(breachObj); breachObj = null; }
            // fallen blocks heaped at the foot of the wall beside every tower, spilling outward
            foreach (var h in heaps) if (h) Destroy(h);
            heaps.Clear();
            if (on && art && art.rubbleHeap)
                foreach (var tw in towers)
                {
                    var at = new Vector3(tw.x, 0, tw.z); var outward = at - Field.PLAZA; outward.y = 0; outward.Normalize();
                    var side = Vector3.Cross(Vector3.up, outward);
                    foreach (float sx in new[] { -2.4f, 2.6f })
                    {
                        var h = Instantiate(art.rubbleHeap, world); h.transform.position = at + outward * 1.6f + side * sx;
                        h.transform.rotation = Quaternion.LookRotation(-outward) * art.rubbleHeap.transform.rotation; heaps.Add(h);   // models face -Z: its +y spill goes outward
                    }
                }
            if (!on) { foreach (var p in puffs) if (p.t) Destroy(p.t.gameObject); puffs.Clear(); }
            if (beaconFlame) beaconFlame.gameObject.SetActive(!on);
        }

        // smoke and embers drift up over the ruins while the card is up
        void TickFall(float dt)
        {
            if (adWaiting && (adT += dt) > 90f) AdDone("0");   // never wait on an ad for good
            if (!P.fallen) return;
            if (rewinding)
            {   // the rewind runs the smoke backward: every puff sinks and shrinks back into the ruins
                for (int k = puffs.Count - 1; k >= 0; k--)
                {
                    var p = puffs[k]; p.age -= dt * 2.5f;
                    if (!p.t || p.age <= 0) { if (p.t) Destroy(p.t.gameObject); puffs.RemoveAt(k); continue; }
                    float u = p.age / p.life; p.t.position -= (Vector3.up * 2.4f + p.drift) * dt * 2.5f;
                    p.t.localScale = puffUnit * p.size * Mathf.Sin(Mathf.Min(1f, u * 1.4f) * Mathf.PI * 0.5f); p.t.Rotate(0, -60f * dt, 0); puffs[k] = p;
                }
                return;
            }
            if (!Hud.Paused) Hud.ShowFallen();   // the card stays until a choice is made (P, the menu, a reload)
            // smoke: dark puffs (hg_front.puff) rise from the ruins and the gate, swell, drift with the wind and sink away
            for (int k = puffs.Count - 1; k >= 0; k--)
            {
                var p = puffs[k]; p.age += dt;
                if (!p.t || p.age > p.life) { if (p.t) Destroy(p.t.gameObject); puffs.RemoveAt(k); continue; }
                float u = p.age / p.life;
                p.t.position += (Vector3.up * 2.4f + p.drift) * dt;
                p.t.localScale = puffUnit * p.size * Mathf.Sin(Mathf.Min(1f, u * 1.4f) * Mathf.PI * 0.5f) * (1f - Mathf.SmoothStep(0f, 1f, (u - 0.7f) / 0.3f));   // Unity's SmoothStep(a, b, t) blends a to b; it is not GLSL's smoothstep(edge0, edge1, x);
                p.t.Rotate(0, 20f * dt, 0); puffs[k] = p;
            }
            if ((smokeT -= dt) > 0) return;
            smokeT = 0.12f;
            var standing = new System.Collections.Generic.List<int>();
            for (int b = 0; b < HG.BUILDINGS.Length; b++) if (P.restored.Contains(HG.BUILDINGS[b].id)) standing.Add(b);
            int i = Random.Range(-1, standing.Count);
            var at = i < 0 ? new Vector3(Random.Range(-2f, 2f), 3.5f, Field.GATE_Z) : Field.Plot(standing[i]) + new Vector3(Random.Range(-1.2f, 1.2f), 1.2f, Random.Range(-1.2f, 1.2f));
            if (art && art.puff && puffs.Count < 80)
            {
                var g = Instantiate(art.puff, world); g.transform.position = at;
                if (puffUnit == Vector3.zero)
                {   // a scale for one metre across, from the mesh's own size (an imported root may carry any scale)
                    var mf = g.GetComponentInChildren<MeshFilter>(); float w = mf && mf.sharedMesh ? Mathf.Max(0.001f, mf.sharedMesh.bounds.size.x) : 1.27f;
                    puffUnit = Vector3.one / w;
                    if (Debug.isDebugBuild) Debug.Log($"[fall] puff mesh {w:0.000} wide, root scale {g.transform.localScale}");
                }
                g.transform.localScale = Vector3.zero;
                foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                puffs.Add((g.transform, 0f, Random.Range(4f, 5.5f), Random.Range(4.5f, 7f), new Vector3(0.9f, 0, Random.Range(-0.3f, 0.3f))));   // seen from 55 m: a puff must be metres across to read
            }
            if (Random.value < 0.3f) Fx.I.Burst(at, Field.FIRE, 2);   // embers
        }

        public void RestoreFallen(bool paid)
        {
            if (!P.fallen) return;
            if (rewinding) return;
            if (paid) { if (!CanPayRestore) { Sound.Dud(); return; } P.shards -= FallCost; Save(); }
            StartCoroutine(Rewind(false));
        }
        // the gamble lost: the run is gone, back to the start of this Ascension (HG.Collapse). Ascending locks progress in;
        // anything past the last Ascension is at stake every time the walls fall.
        public void StartOver()
        {
            if (!P.fallen || rewinding) return;
            StartCoroutine(Rewind(true));
        }
        // the time wizard rewinds (owner, Nov 14 2026: "Rebuilding the town should match wizard lore with an animation to
        // show the power of the wizard ... when town falls he rewinds to his last ascension"): his staff throws light into a
        // great clock over the town whose hands race backward; the smoke sinks back into the ruins, each building rises
        // again one after another (or, rewinding to the last Ascension, the rubble and the breach lift away from the walls
        // as they stood then), and the world washes blue until the moment holds.
        System.Collections.IEnumerator Rewind(bool toAscension)
        {
            rewinding = true; Hud.CloseCard(); if (Wizard.I && Wizard.I.On) Wizard.I.Toggle();
            var dialAt = new Vector3(0, 13f, (Field.PLAZA.z + Field.GATE_Z) * 0.5f);
            Sound.Play("surge", 0.6f); Sound.Play("ascension", 0.5f);
            StartCoroutine(Dial(dialAt, 2.4f, 1.7f, false));
            var order = new System.Collections.Generic.List<int>();
            if (!toAscension) for (int i = HG.BUILDINGS.Length - 1; i >= 0; i--) if (P.restored.Contains(HG.BUILDINGS[i].id)) order.Add(i);
            float t = 0, boltT = 0; int next = 0;
            while (t < 2.6f)
            {
                float dt = Time.unscaledDeltaTime; t += dt;
                if ((boltT -= dt) <= 0) { boltT = 0.11f; var staff = Wizard.I ? Wizard.I.StaffTip : new Vector3(0, 3, -6); Bolt.Arc(staff, dialAt + Random.insideUnitSphere * 1.2f); Fx.I.Burst(staff, new Color(0.6f, 0.85f, 1f), 2); }
                while (next < order.Count && t > 0.55f + next * (1.3f / Mathf.Max(1, order.Count)))
                {   // a building rises again, in reverse order of its fall
                    int i = order[next++]; RefreshPlot(i);
                    Fx.I.Burst(Field.Plot(i) + Vector3.up * 2f, new Color(0.62f, 0.86f, 1f), 22); Fx.I.Burst(Field.Plot(i) + Vector3.up * 3.5f, Field.GOLD, 8); Sound.Play("claim", 0.25f);
                }
                yield return null;
            }
            if (toAscension)
            {
                FlushWisps(); ShowShambles(false);
                P = HG.Collapse(P); Refresh(); gateHp = HG.GateMax(P, B); cheer = cheerT = 0; betweenT = 3f;
                for (int i = 0; i < HG.BUILDINGS.Length; i++) RefreshPlot(i);
                RefreshTowers();
                Hud.Banner(P.ascensions > 0 ? $"Time rewinds to your last mark: Ascension {P.ascensions}, wave {P.wave}" : $"Time rewinds to the beginning: wave {P.wave}");
                Save();
            }
            else Rise($"Time rewinds to before the walls fell. Wave {P.wave}, once more");
            Fx.I.Burst(new Vector3(0, 3f, Field.GATE_Z), new Color(0.62f, 0.86f, 1f), 40); Fx.I.Shake(0.2f, true);
            rewinding = false;
        }
        void Rise(string line)
        {
            P.fallen = false; gateHp = HG.GateMax(P, B); betweenT = 2.5f; Hud.CloseCard();   // whatever closed the loss (a button, an ad, a test hook)
            ShowShambles(false); RefreshTowers();
            Sound.Play("tier_up", 0.5f); Fx.I.Shake(0.25f, true);
            Fx.I.Burst(new Vector3(0, 3f, Field.GATE_Z), Field.GOLD, 40);
            for (int i = 0; i < HG.BUILDINGS.Length; i++) if (P.restored.Contains(HG.BUILDINGS[i].id)) Fx.I.Burst(Field.Plot(i) + Vector3.up * 3f, Field.GOLD, 14);
            Hud.Banner(line); Save();
        }

        // ---------- the rewarded ad (Plugins/WebGL/HgAds.jslib -> the CrazyGames SDK when the page has it) ----------
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern int HG_AdReady();
        [DllImport("__Internal")] static extern void HG_AdRewarded();
#else
        static int HG_AdReady() => 0;
        static void HG_AdRewarded() { }
#endif
        public static bool AdReady { get { try { return HG_AdReady() == 1; } catch { return false; } } }
        bool adWaiting; float adT;
        public void WatchAdToRestore()
        {
            if (!P.fallen || adWaiting || !AdReady) { Sound.Dud(); return; }
            adWaiting = true; adT = 0; AudioListener.pause = true;
            try { HG_AdRewarded(); } catch { AdDone("0"); }
        }
        // called by the page (SendMessage 'Game' 'AdDone'): "1" when the ad was watched; no reward on an error
        public void AdDone(string ok)
        {
            adWaiting = false; AudioListener.pause = false;
            if (ok == "1") RestoreFallen(false);
            else Hud.Banner("No ad could be shown just now");
        }
    }
}
