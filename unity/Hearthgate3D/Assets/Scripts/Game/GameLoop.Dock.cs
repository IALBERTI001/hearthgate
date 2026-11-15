// The upgrade dock's actions (owner, Nov 15 2026: "the radial menu is not pulling its weight for the amount of upgrades we
// have to do in a moments notice"): the defenses and the best building, bought one, ten or as many as Resonance allows at a
// press, and again and again while held (game-polish 12), from the keyboard as well as the dock.
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class GameLoop
    {
        public static readonly int[] MULTS = { 1, 10, 0 };   // 0: as many as can be bought
        public int MultIx { get => Mathf.Clamp(PlayerPrefs.GetInt("hg_mult", 0), 0, 2); set { PlayerPrefs.SetInt("hg_mult", value); } }
        public int Mult => MULTS[MultIx];
        public string MultLabel => Mult == 0 ? "Max" : "x" + Mult;
        public void CycleMult() { MultIx = (MultIx + 1) % MULTS.Length; Sound.Click(); }

        // how many levels of the defenses the multiplier buys now, and what they cost together
        public int GateBuyCount(out double total)
        {
            int want = Mult == 0 ? 500 : Mult; total = 0; double res = P.res; int n = 0;
            for (; n < want; n++) { double c = HG.UpgradeCost(P.gateLevel + n, B); if (total + c > res) break; total += c; }
            if (n == 0) total = UpgradeCost;
            return n;
        }
        public void UpgradeBulk()
        {
            int want = Mult == 0 ? 500 : Mult, bought = 0;
            for (int k = 0; k < want && CanUpgrade && HG.PendingFork(P) == 0; k++) { Upgrade(bought > 0); bought++; }
            if (bought == 0) Sound.Dud();
            else if (bought > 1) Fx.I.Pop(Field.GateTop + Vector3.up * 2f, $"+{bought} levels", Field.GOLD, 34);
        }

        // the building whose next level pays itself back soonest; the ruin next in line comes first while it waits
        public int BestBuilding()
        {
            var nb = NextBuilding; if (nb != null) return System.Array.IndexOf(HG.BUILDING_IDS, nb);
            int best = -1; double bv = double.MaxValue;
            for (int i = 0; i < HG.BUILDING_IDS.Length; i++)
            {
                var id = HG.BUILDING_IDS[i]; if (!P.restored.Contains(id)) continue;
                int L = HG.LevelOf(P, id); double gain = HG.BuildingRate(P, id, B, L + 1) - HG.BuildingRate(P, id, B);
                double v = LevelCost(i) / System.Math.Max(1e-9, gain); if (v < bv) { bv = v; best = i; }
            }
            return best;
        }
        public double BestCost(int i) => i < 0 ? 0 : P.restored.Contains(HG.BUILDING_IDS[i]) ? LevelCost(i) : RestoreCost;
        public void BuyBest()
        {
            int want = Mult == 0 ? 500 : Mult, bought = 0;
            for (int k = 0; k < want; k++)
            {
                int i = BestBuilding(); if (i < 0 || !CanWork(i)) break;
                bool rebuild = !P.restored.Contains(HG.BUILDING_IDS[i]);
                WorkOn(i, bought > 0); bought++;
                if (rebuild) break;   // a rebuild is its own moment: one at a time
            }
            if (bought == 0) Sound.Dud();
        }

        float uHeld, uNext;
        void TickDockKeys()
        {
            if (Hud.Paused) return;
            if (Input.GetKeyDown(KeyCode.U)) { if (CanUpgrade) { Sound.Click(); UpgradeBulk(); } else Sound.Dud(); uHeld = 0; uNext = 0.35f; }
            else if (Input.GetKey(KeyCode.U))
            {   // held: again and again, faster the longer it is held
                uHeld += Time.unscaledDeltaTime;
                if (uHeld >= uNext && CanUpgrade) { UpgradeBulk(); Sound.Click(); uNext = uHeld + Mathf.Lerp(0.16f, 0.05f, Mathf.Clamp01((uHeld - 0.35f) / 2f)); }
            }
            if (Input.GetKeyDown(KeyCode.B)) BuyBest();
            if (Input.GetKeyDown(KeyCode.L) && StewardHere) Hud.ShowLedger("town");
            if (Input.GetKeyDown(KeyCode.V)) CycleMult();   // M is sound, Q is AZERTY movement
        }
    }
}
