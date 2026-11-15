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
            public string label, icon, key; public System.Func<string> line; public System.Func<bool> can, lit; public System.Action act; public bool closes;
        }
        RectTransform ring; Vector3 ringAt; string ringId; float ringT;
        readonly List<(RectTransform rt, Vector2 home, Text line, RadialItem it, Act a)> ringItems = new List<(RectTransform, Vector2, Text, RadialItem, Act)>();
        public static bool RingOpen => I && I.ring;
        public static string RingId => I ? I.ringId : null;

        public static void OpenRing(string id, Vector3 world, string title, List<RadialItem> items)
        {
            if (!I) return;
            if (I.ring && I.ringId == id) { CloseRing(); return; }   // a second click on the same thing closes it
            CloseRing(); Sound.Click();
            I.ringId = id; I.ringAt = world; I.ringT = 0;
            I.ring = I.Box("Ring", I.transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(10, 10)); I.ring.pivot = new Vector2(0.5f, 0.5f);
            var hub = I.Box("Hub", I.ring, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170, 34));
            var hi = hub.gameObject.AddComponent<Image>(); hi.sprite = Panel(INK, BRASS, 14, 2); hi.type = Image.Type.Sliced; hi.raycastTarget = false;
            var ht = I.Label("T", hub, new Vector2(0.5f, 0.5f), new Vector2(0, 1), 19, TextAnchor.MiddleCenter); ht.text = title;
            Fit(ht, new Vector2(160, 30), 12);
            int n = items.Count; float R = n <= 2 ? 132f : n <= 4 ? 140f : 152f;   // wide enough that the hub's title and each button's words never touch
            for (int k = 0; k < n; k++)
            {
                var it = items[k];
                // from the top, clockwise; two items sit left and right, three make a fan over the top
                float a = n == 1 ? 90f : n == 2 ? (k == 0 ? 180f : 0f) : 90f - k * 360f / n;
                if (n == 3) a = 150f - k * 60f;
                var home = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * R;
                var item = it; var act = I.Action("R" + k, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72, 72), it.icon, it.key,
                    () => { if (item.can == null || item.can()) { item.act(); if (item.closes) CloseRing(); } }, it.can, false, I.ring);
                act.p.rt.pivot = new Vector2(0.5f, 0.5f);
                var lab = I.Label("L" + k, act.p.rt, new Vector2(0.5f, 0), new Vector2(0, -6), 16, TextAnchor.UpperCenter); lab.text = it.label;
                lab.rectTransform.pivot = new Vector2(0.5f, 1); lab.rectTransform.sizeDelta = new Vector2(150, 20);
                var ln = I.Label("C" + k, act.p.rt, new Vector2(0.5f, 0), new Vector2(0, -26), 14, TextAnchor.UpperCenter); ln.font = I.body; ln.fontStyle = FontStyle.Bold; ln.supportRichText = true;
                ln.rectTransform.pivot = new Vector2(0.5f, 1); ln.rectTransform.sizeDelta = new Vector2(150, 20); ln.verticalOverflow = VerticalWrapMode.Overflow;
                I.ringItems.Add((act.p.rt, home, ln, it, act));
            }
            I.TickRing();
        }
        public static void CloseRing()
        {
            if (!I || !I.ring) return;
            Destroy(I.ring.gameObject); I.ring = null; I.ringId = null; I.ringItems.Clear();
        }
        void TickRing()
        {
            if (!ring) return;
            var g = GameLoop.I; var cam = Camera.main;
            bool walk = Wizard.I && Wizard.I.On;
            if (g == null || cam == null || walk || g.Fallen || Paused || Input.GetKeyDown(KeyCode.Escape)) { CloseRing(); return; }
            var sp = cam.WorldToScreenPoint(ringAt);
            if (sp.z <= 0) { CloseRing(); return; }
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, sp, null, out var lp);
            var cv = ((RectTransform)transform).rect;
            // kept whole on the screen: the ring's centre stays far enough from every edge, and clear of the top bar
            lp.x = Mathf.Clamp(lp.x, cv.xMin + 215, cv.xMax - 215); lp.y = Mathf.Clamp(lp.y, cv.yMin + 240, cv.yMax - 230);
            ring.anchoredPosition = lp - cv.min;
            ringT += Time.unscaledDeltaTime; float u = Mathf.Clamp01(ringT / 0.14f), e = 1f - (1f - u) * (1f - u);
            foreach (var (rt, home, ln, it, a) in ringItems)
            {
                if (!rt) continue;
                rt.anchoredPosition = home * e;
                // the fade-in is the ring's own; the veil only dims (it skipped buttons that could not act, so a greyed Ascend
                // stayed at alpha 0 and the gatehouse ring showed one button: QA Nov 15 2026)
                var cg = rt.GetComponent<CanvasGroup>(); if (cg) cg.alpha = e;
                string line = it.line != null ? it.line() : null; ln.text = line ?? "";
                bool can = it.can == null || it.can();
                ln.color = can ? RES : new Color(0.85f, 0.78f, 0.86f);
                if (a.glow) { var c = a.glow.color; c.a = it.lit != null && it.lit() ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 6f) : 0f; a.glow.color = c; }
            }
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
                new RadialItem { label = "Upgrade defenses", icon = "tower", key = "U", line = () => HG.Fmt(g.UpgradeCost), can = () => g.CanUpgrade, act = () => g.Upgrade(), lit = () => Guide.Pressing == "up" },
                new RadialItem { label = "Ascend", icon = "ascend", key = g.NextBuilding == null ? "R" : null,
                    line = () => g.CanAscend ? "x1.5 for good" : g.NextBuilding != null ? "rebuild the city" : $"wave {System.Math.Max(g.P.runWave, g.P.wave)} / {HG.AscendWave(g.P)}",
                    can = () => g.CanAscend, act = () => ShowAscend(), closes = true, lit = () => g.CanAscend },
            };
            OpenRing("gate", Field.GateTop, "The Gatehouse", items);
        }
        public static void RingTower(int n)
        {
            var g = GameLoop.I; if (g == null) return;
            var items = new List<RadialItem>();
            var els = g.TowerElements;
            items.Add(new RadialItem { label = "Auto", icon = "tower", line = () => g.P.towerEl[n] == "" ? "chosen" : null, act = () => g.SetTowerEl(n, "", false), lit = () => g.P.towerEl[n] == "" });
            foreach (var el in els)
            {
                var e = el;
                items.Add(new RadialItem { label = ElName(e), icon = "el_" + e, line = () => g.P.towerEl[n] == e ? "chosen" : null, act = () => g.SetTowerEl(n, e, false), lit = () => g.P.towerEl[n] == e });
            }
            items.Add(new RadialItem { label = "Designs", icon = "shop", line = () => "and all four", act = () => ShowTower(n), closes = true });
            OpenRing("tower" + n, g.TowerMuzzle(n), TowerName(n), items);
        }
        public static void RingBuilding(int i)
        {
            var g = GameLoop.I; if (g == null) return; var P = g.P; var id = HG.BUILDING_IDS[i]; var b = HG.ById(id);
            var items = new List<RadialItem>();
            if (P.restored.Contains(id))
                items.Add(new RadialItem { label = "Upgrade", icon = "upgrade", key = null,
                    line = () => { int L = HG.LevelOf(P, id); return $"level {L + 1} · {HG.Fmt(g.LevelCost(i))}"; }, can = () => g.CanWork(i), act = () => g.WorkOn(i) });
            else
                items.Add(new RadialItem { label = "Rebuild", icon = "rebuild", key = HG.RestoreOffer(P).Contains(id) ? "R" : null,
                    line = () => HG.RestoreOffer(P).Contains(id) ? HG.Fmt(g.RestoreCost) : g.NextBuilding != null ? "after the " + HG.ById(g.NextBuilding).name : "later",
                    can = () => g.CanWork(i), act = () => g.WorkOn(i), closes = true, lit = () => Guide.Pressing == "re" && HG.RestoreOffer(P).Contains(id) });
            items.Add(new RadialItem { label = "Walk there", icon = "walk", act = () => Wizard.I?.WalkTo(i), closes = true });
            OpenRing("b" + i, Field.Plot(i) + Vector3.up * (Field.PlotHeight(i) * 0.6f), b.name + (P.restored.Contains(id) ? $" · level {HG.LevelOf(P, id)}" : ""), items);
        }
    }
}
