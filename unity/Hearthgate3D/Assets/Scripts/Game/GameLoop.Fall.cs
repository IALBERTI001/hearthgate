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
        GameObject gateObj, breachObj; Quaternion gateRot; Vector3 gatePos;
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
            if (gateObj)
            {
                if (on) { gateRot = gateObj.transform.rotation; gatePos = gateObj.transform.position; gateObj.transform.SetPositionAndRotation(gatePos + new Vector3(0.3f, -0.9f, 0.4f), gateRot * Quaternion.Euler(-14f, 0, 7f)); }
                else gateObj.transform.SetPositionAndRotation(gatePos, gateRot);
            }
            if (beaconFlame) beaconFlame.gameObject.SetActive(!on);
            if (Debug.isDebugBuild) Debug.Log($"[fall] shambles {on}: gate {(gateObj ? gateObj.transform.position + " " + gateObj.transform.eulerAngles : "none")}, breach {(breachObj ? "yes" : "no")}");
        }

        // smoke and embers drift up over the ruins while the card is up
        void TickFall(float dt)
        {
            if (adWaiting && (adT += dt) > 90f) AdDone("0");   // never wait on an ad for good
            if (!P.fallen) return;
            if (!Hud.Paused) Hud.ShowFallen();   // the card stays until a choice is made (P, the menu, a reload)
            if ((smokeT -= dt) > 0) return;
            smokeT = 0.18f;
            int i = Random.Range(0, HG.BUILDINGS.Length + 1);
            var at = i == HG.BUILDINGS.Length ? new Vector3(Random.Range(-3f, 3f), 2.5f, Field.GATE_Z) : Field.Plot(i) + new Vector3(Random.Range(-1.5f, 1.5f), 1.5f, Random.Range(-1.5f, 1.5f));
            Fx.I.Burst(at, Random.value < 0.75f ? new Color(0.36f, 0.34f, 0.34f) : Field.FIRE, 3);
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
