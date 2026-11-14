// The walls fall (owner, Nov 14 2026: "if the player walls get destroyed, we do a loss screen of the walls and town in
// shambles and charge to restore the town (star shards); if they do not have enough star shards then we can give them
// the option of watching an ad to restore or start over at level 1"). The fight stops; every standing building shows as
// its ruin, the gate leans broken, the tower tops are gone and smoke and embers rise over the town. A card at the foot of
// the screen (the shambles stay in view above it) offers:
//  - Restore the town for Starshards (3 + 1 per 10 waves): all of it stands again and the same wave is fought again;
//  - without enough Starshards, a rewarded ad restores it instead (only where the CrazyGames SDK offers one: Basic Launch
//    has ads off, so the button is not shown there);
//  - Start over at wave 1 (asked twice): the town is rebuilt as it was, the waves begin again from the first.
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
        float smokeT;

        public int FallCost => 3 + P.wave / 10;
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
            if (!Hud.Paused) Hud.ShowFallen();   // the card stays until a choice is made (P, the menu, a reload)
            // smoke: dark puffs (hg_front.puff) rise from the ruins and the gate, swell, drift with the wind and sink away
            for (int k = puffs.Count - 1; k >= 0; k--)
            {
                var p = puffs[k]; p.age += dt;
                if (!p.t || p.age > p.life) { if (p.t) Destroy(p.t.gameObject); puffs.RemoveAt(k); continue; }
                float u = p.age / p.life;
                p.t.position += (Vector3.up * 1.6f + p.drift) * dt;
                p.t.localScale = Vector3.one * p.size * Mathf.Sin(Mathf.Min(1f, u * 1.4f) * Mathf.PI * 0.5f) * (1f - Mathf.SmoothStep(0.7f, 1f, u));
                p.t.Rotate(0, 20f * dt, 0); puffs[k] = p;
            }
            if ((smokeT -= dt) > 0) return;
            smokeT = 0.12f;
            var standing = new System.Collections.Generic.List<int>();
            for (int b = 0; b < HG.BUILDINGS.Length; b++) if (P.restored.Contains(HG.BUILDINGS[b].id)) standing.Add(b);
            int i = Random.Range(-1, standing.Count);
            var at = i < 0 ? new Vector3(Random.Range(-2f, 2f), 3.5f, Field.GATE_Z) : Field.Plot(standing[i]) + new Vector3(Random.Range(-1.2f, 1.2f), 1.2f, Random.Range(-1.2f, 1.2f));
            if (art && art.puff && puffs.Count < 70)
            {
                var g = Instantiate(art.puff, world); g.transform.position = at; g.transform.localScale = Vector3.zero;
                foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                puffs.Add((g.transform, 0f, Random.Range(3.2f, 4.5f), Random.Range(1.3f, 2.2f), new Vector3(0.5f, 0, Random.Range(-0.2f, 0.2f))));
            }
            if (Random.value < 0.3f) Fx.I.Burst(at, Field.FIRE, 2);   // embers
        }

        public void RestoreFallen(bool paid)
        {
            if (!P.fallen) return;
            if (paid) { if (!CanPayRestore) { Sound.Dud(); return; } P.shards -= FallCost; }
            Rise($"The town stands again! Wave {P.wave}, once more");
        }
        public void StartOver()
        {
            if (!P.fallen) return;
            P.wave = 1;
            Rise("The town is rebuilt. The waves begin again from wave 1");
        }
        void Rise(string line)
        {
            P.fallen = false; gateHp = HG.GateMax(P, B); betweenT = 2.5f;
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
