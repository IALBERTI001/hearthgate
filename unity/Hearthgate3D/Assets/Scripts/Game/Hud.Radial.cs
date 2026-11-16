// The radial menu: upgrades open on the thing they upgrade (owner, Nov 15 2026: "a radial menu would be best option for the
// upgrades. the buttons just dont seem to be in the right places"; chosen: "on the thing", Kingdom Rush style). A click on
// the gatehouse rings it with Upgrade defenses and Ascend; a click on a tower rings it with its elements and its designs; a
// click on a building rings it with its upgrade or rebuild. The ring stays open while the game runs, so a button can be
// pressed again and again; a click elsewhere, Escape, or a change of view closes it. Its buttons grow out of the thing they
// belong to (game-hud-ui-design rule 8: awareness) and move, never scale, so the words stay sharp.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class Hud
    {
        public class RadialItem
        {
            public string label, icon, key; public bool cost; public System.Func<string> line; public System.Func<bool> can, lit; public System.Action act; public bool closes;   // cost: the line is a price, drawn after the gem
        }
        RectTransform ring; Vector2 ringAt; string ringId; float ringT; Rect ringReach;
        bool ringHeld; float ringHeldT; int ringSel = -1;   // the press that opened it is still held: a drag picks, the release chooses
        readonly Dictionary<RadialItem, Image> ringGems = new Dictionary<RadialItem, Image>();
        readonly List<(RectTransform rt, Vector2 home, Text line, RadialItem it, Act a)> ringItems = new List<(RectTransform, Vector2, Text, RadialItem, Act)>();
        public static bool RingOpen => I && I.ring;
        public static string RingId => I ? I.ringId : null;

        public static void OpenRing(string id, Vector3 world, string title, List<RadialItem> items)   // world: unused now, kept for callers
        {
            if (!I) return;
            if (I.ring && I.ringId == id) { CloseRing(); return; }   // a second click on the same thing closes it
            CloseRing(); Sound.Click();
            I.ringId = id; I.ringAt = PointerPos; I.ringT = 0; I.ringHeld = Input.GetMouseButton(0); I.ringHeldT = 0; I.ringSel = -1;   // round the cursor, where the eye already is (owner, Nov 15 2026: "maybe we should do it on cursor with the options there")
            I.ring = I.Box("Ring", I.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(10, 10)); I.ring.pivot = new Vector2(0.5f, 0.5f);
            // the hub is the thing's name, plain words with a soft shade, not a button (it is not pressable)
            var hub = I.Box("Hub", I.ring, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(190, 30));
            var hi = hub.gameObject.AddComponent<Image>(); hi.sprite = Fade2(); hi.color = new Color(0.08f, 0.04f, 0.1f, 0.75f); hi.raycastTarget = false;
            var ht = I.Label("T", hub, new Vector2(0.5f, 0.5f), new Vector2(0, 1), 20, TextAnchor.MiddleCenter); ht.text = title; ht.color = RES;
            Fit(ht, new Vector2(186, 28), 13);
            int n = items.Count;
            // the same kit plates as every button (owner, Nov 15 2026: "WHY can we not be consistent"): picture, name and price inside
            // the plate, keycap on its corner; set round an ellipse so the wide plates never touch
            float RX = n <= 2 ? 150f : 175f, RY = n <= 2 ? 0f : n <= 4 ? 92f : 100f;
            for (int k = 0; k < n; k++)
            {
                var it = items[k];
                float a = n == 1 ? 90f : n == 2 ? (k == 0 ? 180f : 0f) : n == 3 ? 150f - k * 60f : 90f - k * 360f / n;
                var dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                var home = n == 1 ? new Vector2(0, 66) : new Vector2(dir.x * RX, dir.y * (RY > 0 ? RY : 0));
                var item = it; var act = I.Action("R" + k, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(176, 56), it.icon, it.key,
                    () => { if (item.can == null || item.can()) { item.act(); if (item.closes) CloseRing(); } }, it.can, false, I.ring);
                act.p.rt.pivot = new Vector2(0.5f, 0.5f); act.p.repeat = !it.closes;   // a price held down buys again and again
                I.ringItems.Add((act.p.rt, home, act.cost, it, act));
            }
            {   // how far the ring reaches round its centre (buttons, words under them, the hub), so it is kept on screen by its
                // real size: a two-button ring sits level with the thing it rings, even near the top of the screen
                var rr = new Rect(-95, -16, 190, 32);
                foreach (var (_, h, _, _, _) in I.ringItems) { rr.xMin = Mathf.Min(rr.xMin, h.x - 92); rr.xMax = Mathf.Max(rr.xMax, h.x + 92); rr.yMin = Mathf.Min(rr.yMin, h.y - 32); rr.yMax = Mathf.Max(rr.yMax, h.y + 34); }
                I.ringReach = rr;
            }
            I.TickRing();
        }
        Rect RingBox() => ring ? new Rect(ring.anchoredPosition + ringReach.min, ringReach.size) : Rect.zero;   // bottom-left frame, as BannerRect
        public static void CloseRing()
        {
            if (!I || !I.ring) return;
            Destroy(I.ring.gameObject); I.ring = null; I.ringId = null; I.ringItems.Clear(); I.ringGems.Clear();
        }
        void TickRing()
        {
            if (!ring) return;
            var g = GameLoop.I; var cam = Camera.main;
            bool walk = Wizard.I && Wizard.I.On;
            if (g == null || cam == null || walk != (ringId == "wiz") || g.Fallen || Paused || Input.GetKeyDown(KeyCode.Escape)) { CloseRing(); return; }
            if (ringHeld && HeldPick()) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, ringAt, null, out var lp);   // it stays where it opened
            var cv = ((RectTransform)transform).rect;
            // kept whole on the screen: the ring's centre stays far enough from every edge, and clear of the top bar
            var rr = ringReach; lp.x = Mathf.Clamp(lp.x, cv.xMin + 8 - rr.xMin, cv.xMax - 8 - rr.xMax); lp.y = Mathf.Clamp(lp.y, cv.yMin + 8 - rr.yMin, cv.yMax - 92 - rr.yMax);   // 92: the top bar
            ring.anchoredPosition = lp - cv.min;
            ringT += Time.unscaledDeltaTime; float u = Mathf.Clamp01(ringT / 0.14f), e = 1f - (1f - u) * (1f - u);
            foreach (var (rt, home, ln, it, a) in ringItems)
            {
                if (!rt) continue;
                rt.anchoredPosition = home * e;
                // the fade-in is the ring's own; the veil only dims (it skipped buttons that could not act, so a greyed Ascend
                // stayed at alpha 0 and the gatehouse ring showed one button: QA Nov 15 2026)
                var cg = rt.GetComponent<CanvasGroup>(); if (cg) cg.alpha = e;
                string line = it.line != null ? it.line() : null; bool can = it.can == null || it.can();
                bool price = it.cost && !string.IsNullOrEmpty(line) && char.IsDigit(line[0]);
                Show(a, it.label, string.IsNullOrEmpty(line) ? null : line, can || !price, Time.unscaledTime);
                if (a.gem) a.gem.enabled = price;
                if (!price && a.cost) { a.cost.color = new Color(0.9f, 0.84f, 0.92f); a.cost.rectTransform.anchoredPosition = new Vector2(2, 0); }
                else if (a.cost) a.cost.rectTransform.anchoredPosition = new Vector2(20, 0);
                bool picked = ringSel >= 0 && ringSel < ringItems.Count && ringItems[ringSel].rt == rt;
                if (a.glow) { var c = a.glow.color; c.a = picked ? 1f : it.lit != null && it.lit() ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f) : 0f; a.glow.color = c; }
                if (a.p != null && a.p.img) a.p.img.color = picked ? Color.Lerp(a.p.col, Color.white, 0.25f) : a.p.col;
            }
        }
        // the marking-menu gesture (owner, Nov 15 2026: "hold left click to open the menu, highlight option, let go of mouse button
        // to select"): while the press that opened the ring is held, the button the pointer leans toward is lit; letting go chooses
        // it. Let go near the centre after a quick click and the ring stays open to be clicked; anywhere else it closes.
        // Returns true when the ring was closed.
        bool HeldPick()
        {
            ringHeldT += Time.unscaledDeltaTime;
            var off = PointerPos - ringAt; float dead = 30f * Screen.height / 720f;
            ringSel = -1;
            if (off.magnitude > dead)
            {
                float best = ringItems.Count == 1 ? 181f : 70f;   // a lone button takes a lean in any direction
                for (int k = 0; k < ringItems.Count; k++) { float d = Vector2.Angle(off, ringItems[k].home); if (d < best) { best = d; ringSel = k; } }
            }
            if (Input.GetMouseButton(0)) return false;
            ringHeld = false;
            if (ringSel >= 0)
            {
                var it = ringItems[ringSel].it; ringSel = -1;
                if (it.can == null || it.can()) { Sound.Click(); it.act(); } else Sound.Dud();
                CloseRing(); return true;
            }
            if (off.magnitude <= dead) return false;   // let go where it was pressed (a click, or a hold that changed its mind): the ring stays for clicking
            CloseRing(); return true;
        }

        // a click that lands on the ring's buttons or its hub is the ring's
        static bool OnRing(Vector2 m)
        {
            if (!I || !I.ring) return false;
            foreach (var (rt, _, _, _, _) in I.ringItems) if (rt && RectTransformUtility.RectangleContainsScreenPoint(rt, m, null)) return true;
            var hub = I.ring.Find("Hub") as RectTransform; return hub && RectTransformUtility.RectangleContainsScreenPoint(hub, m, null);
        }

        // ---------- what each thing's ring holds ----------
        public static void RingGate()
        {
            var g = GameLoop.I; if (g == null) return;
            var items = new List<RadialItem>
            {
                new RadialItem { label = "Upgrade defenses", icon = "tower", key = "U", cost = true, line = () => HG.Fmt(g.UpgradeCost), can = () => g.CanUpgrade, act = () => g.Upgrade(), lit = () => Guide.Pressing == "up" },

            };
            // Ascend shows once the city is whole (review Nov 15 2026: a locked "rebuild the city" button in a new player's first ring)
            if (g.NextBuilding == null) items.Add(new RadialItem { label = "Ascend", icon = "ascend", key = g.NextBuilding == null ? "R" : null,
                    line = () => g.CanAscend ? "x1.5 for good" : g.NextBuilding != null ? "rebuild the city" : $"wave {System.Math.Max(g.P.runWave, g.P.wave)} / {HG.AscendWave(g.P)}",
                    can = () => g.CanAscend, act = () => ShowAscend(), closes = true, lit = () => g.CanAscend });
            AddLedger(items, g);
            OpenRing("gate", Field.GateTop, "The Gatehouse", items);
        }
        // on the town walk: hold anywhere and the wizard's own ring opens round the cursor (the gatehouse is far from the square)
        // the Town Ledger from any ring (owner, Nov 15 2026: "the radial menu should have the option to open the ledger too"),
        // once Tobin keeps it (the first building rebuilt)
        static void AddLedger(List<RadialItem> items, GameLoop g)
        {
            if (g.StewardHere) items.Add(new RadialItem { label = "Town ledger", icon = "ledger", line = () => g.LedgerNews ? "a doubling is near" : "every building", act = () => ShowLedger("town"), closes = true, lit = () => g.LedgerNews });
        }
        public static void RingWizard()
        {
            var g = GameLoop.I; if (g == null) return;
            var items = new List<RadialItem>();
            // at a building's door, its own upgrade comes first (the purple board that held it is retired)
            int near = Wizard.I ? Wizard.I.Near : -1;
            if (near >= 0)
            {
                int i = near; var id = HG.BUILDING_IDS[i]; bool built = g.P.restored.Contains(id);
                if (built || HG.RestoreOffer(g.P).Contains(id))
                    items.Add(new RadialItem { label = built ? $"Upgrade {HG.ById(id).name}" : $"Rebuild {HG.ById(id).name}", icon = built ? "upgrade" : "rebuild", key = "E",
                        cost = true, line = () => built ? HG.Fmt(g.LevelCost(i)) : HG.Fmt(g.RestoreCost), can = () => g.CanWork(i), act = () => g.WorkOn(i) });
            }
            items.AddRange(new List<RadialItem>
            {
                new RadialItem { label = "Upgrade defenses", icon = "tower", key = "U", cost = true, line = () => HG.Fmt(g.UpgradeCost), can = () => g.CanUpgrade, act = () => g.Upgrade(), lit = () => Guide.Pressing == "up" },
            });
            if (g.TimeStopLevel > 0)
                items.Add(new RadialItem { label = "Stop time", icon = "timestop", key = "F", line = () => g.TimeStopReady ? "ready" : $"in {Mathf.CeilToInt(g.TimeStopLeft)} s", can = () => g.TimeStopReady, act = () => g.UseTimeStop() });
            if (g.SurgeOpen && (g.SurgeReady || g.SurgeOn)) items.Add(new RadialItem { label = "Surge", icon = "fountain", key = "X", line = () => g.SurgeOn ? "flowing" : "x2 Resonance", can = () => g.SurgeReady, act = () => g.TapSurge(), lit = () => g.SurgeReady });   // the fountain from anywhere in town
            AddLedger(items, g);
            items.Add(new RadialItem { label = "Back to the walls", icon = "walk", key = "T", act = () => Wizard.I?.Toggle() });
            OpenRing("wiz", Vector3.zero, "The wizard", items);
        }
        public static void RingTower(int n)
        {
            var g = GameLoop.I; if (g == null) return;
            var items = new List<RadialItem>();
            var els = g.TowerElements;
            items.Add(new RadialItem { label = "Auto", icon = "tower", line = () => g.P.towerEl[n] == "" ? "in use" : "best element", act = () => g.SetTowerEl(n, "", false), lit = () => g.P.towerEl[n] == "" });
            foreach (var el in els)
            {
                var e = el;
                items.Add(new RadialItem { label = ElName(e), icon = "el_" + e, line = () => g.P.towerEl[n] == e ? "in use" : "use this", act = () => g.SetTowerEl(n, e, false), lit = () => g.P.towerEl[n] == e });
            }
            items.Add(new RadialItem { label = "Designs", icon = "shop", line = () => "change its look", act = () => ShowTower(n), closes = true });
            OpenRing("tower" + n, g.TowerMuzzle(n), TowerName(n), items);
        }
        public static void RingBuilding(int i)
        {
            var g = GameLoop.I; if (g == null) return; var P = g.P; var id = HG.BUILDING_IDS[i]; var b = HG.ById(id);
            var items = new List<RadialItem>();
            if (P.restored.Contains(id))
                items.Add(new RadialItem { label = "Upgrade", icon = "upgrade", key = null,
                    cost = true, line = () => HG.Fmt(g.LevelCost(i)), can = () => g.CanWork(i), act = () => g.WorkOn(i) });
            else
                items.Add(new RadialItem { label = "Rebuild", icon = "rebuild", key = HG.RestoreOffer(P).Contains(id) ? "R" : null,
                    cost = true, line = () => HG.RestoreOffer(P).Contains(id) ? HG.Fmt(g.RestoreCost) : g.NextBuilding != null ? "after the " + HG.ById(g.NextBuilding).name : "later",
                    can = () => g.CanWork(i), act = () => g.WorkOn(i), closes = true, lit = () => Guide.Pressing == "re" && HG.RestoreOffer(P).Contains(id) });
            items.Add(new RadialItem { label = "Walk there", icon = "walk", act = () => Wizard.I?.WalkTo(i), closes = true });
            AddLedger(items, g);
            OpenRing("b" + i, Field.Plot(i) + Vector3.up * (Field.PlotHeight(i) * 0.6f), b.name + (P.restored.Contains(id) ? $" · level {HG.LevelOf(P, id)}" : ""), items);
        }
    }
}
