// The Shop (the Market) and the daily quests (the Library), on the one button kit. Each opens from a board over its
// building once the Market has opened the Shop; the fight waits while it is open.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class Hud
    {
        Act shopAct, questAct;
        static readonly Color ROSE_TXT = new Color(1f, 0.72f, 0.9f);
        void InitHalls()
        {
            shopAct = Action("ShopTag", Vector2.zero, Vector2.zero, new Vector2(230, 56), "shop", null, () => ShowShop("tower"), null, false);
            shopAct.p.rt.pivot = new Vector2(0.5f, 0); shopAct.p.rt.SetSiblingIndex(1); shopAct.p.rt.gameObject.SetActive(false);
            questAct = Action("QuestTag", Vector2.zero, Vector2.zero, new Vector2(230, 56), "quests", null, () => ShowQuests(), null, false);
            questAct.p.rt.pivot = new Vector2(0.5f, 0); questAct.p.rt.SetSiblingIndex(1); questAct.p.rt.gameObject.SetActive(false);
        }
        void HallTags(GameLoop g, Profile P)
        {
            int mi = System.Array.IndexOf(HG.BUILDING_IDS, "market"), li = System.Array.IndexOf(HG.BUILDING_IDS, "library");
            if (P.shopOpen && Pin(shopAct.p.rt, Field.Plot(mi) + Vector3.up * (Field.PlotHeight(mi) + 1.2f)))
            { Show(shopAct, "Shop", $"{P.shards} Starshards", true, 0); shopAct.gem.enabled = false; shopAct.cost.color = ROSE_TXT; }
            else if (!P.shopOpen && shopAct.p.rt.gameObject.activeSelf) shopAct.p.rt.gameObject.SetActive(false);
            int claim = g.QuestsToClaim, done = P.quests.Count(q => q.n >= q.need);
            if (P.shopOpen && Pin(questAct.p.rt, Field.Plot(li) + Vector3.up * (Field.PlotHeight(li) + 1.2f)))
            { Show(questAct, "Library", claim > 0 ? $"{claim} to claim" : FeatNews ? "New Feat!" : $"Quests {done} / {P.quests.Count}", true, 0); questAct.gem.enabled = false; questAct.cost.color = claim > 0 || FeatNews ? RES : CREAM; }
            else if (!P.shopOpen && questAct.p.rt.gameObject.activeSelf) questAct.p.rt.gameObject.SetActive(false);
        }

        static readonly (string cat, string label)[] TABS = { ("tower", "Towers"), ("trail", "Shots"), ("hit", "Hits"), ("wisp", "Wisps"), ("town", "Town") };
        public static void ShowShop(string cat)
        {
            if (!I || !GameLoop.I) return;
            var g = GameLoop.I; var items = HG.SHOP.Where(l => l.cat == cat).ToArray();
            var box = I.Modal("Shop", new Vector2(720, 236 + items.Length * 62));
            var bal = I.Line(box, -62, "", 19, ROSE_TXT);
            for (int k = 0; k < TABS.Length; k++)
            {
                var t = TABS[k];
                I.BtnAt("Tab" + k, new Vector2(0.5f, 1), new Vector2(-264 + k * 132, -98), new Vector2(124, 56), () => ShowShop(t.cat), null, out var tl, box, null, t.cat == cat).rt.pivot = new Vector2(0.5f, 1);
                tl.text = t.label;
            }
            var rows = new List<(HG.Look l, Text b)>();
            for (int i = 0; i < items.Length; i++)
            {
                var l = items[i]; float y = -170 - i * 62;
                var name = I.Label("N" + i, box, new Vector2(0, 1), new Vector2(32, y), 19, TextAnchor.UpperLeft); name.text = l.name + (l.el != null ? $"  <size=14><color=#D9C9A8>{(l.el == "fire" ? "Fire" : l.el == "frost" ? "Frost" : "Storm")} towers</color></size>" : "");
                name.rectTransform.pivot = new Vector2(0, 1); name.rectTransform.sizeDelta = new Vector2(420, 24);
                var line = I.Label("D" + i, box, new Vector2(0, 1), new Vector2(32, y - 24), 15, TextAnchor.UpperLeft); line.font = I.body; line.text = l.line; line.color = new Color(0.85f, 0.8f, 0.88f);
                line.rectTransform.pivot = new Vector2(0, 1); line.rectTransform.sizeDelta = new Vector2(440, 22);
                var lk = l;
                I.BtnAt("B_" + l.id, new Vector2(1, 1), new Vector2(-24, y + 4), new Vector2(180, 56), () => g.BuyOrEquip(lk), () => HG.Equipped(g.P, HG.Slot(lk)) != lk.id && (g.Owns(lk) || g.P.shards >= lk.price), out var bt, box, g.Owns(l) ? null : "shard");
                rows.Add((l, bt));
            }
            I.Choice(box, "S_Close", new Vector2(0, 18), new Vector2(230, 56), "Back to the game", () => I.ShowSettings(false));
            I.perkRefresh = () =>
            {
                bal.text = $"{g.P.shards} Starshards  ·  looks only: nothing in the fight changes";
                foreach (var (l, b) in rows) b.text = HG.Equipped(g.P, HG.Slot(l)) == l.id ? "Equipped" : g.Owns(l) ? "Equip" : $"Buy · {l.price}";
            };
            I.perkRefresh();
        }

        public static bool FeatNews;   // a Feat was earned since the Feats tab was last opened
        static readonly Color[] TIER = { Color.white, new Color(0.85f, 0.58f, 0.35f), new Color(0.86f, 0.88f, 0.92f), new Color(1f, 0.84f, 0.42f) };
        const int PAGE = 6;
        // the Library: today's quests, the Feats and the Bestiary, one card with three tabs (js/game.js qTabs). Every card
        // is the same size so the tabs never jump; long lists turn pages.
        public static void ShowQuests(string tab = "quests", int page = 0)
        {
            if (!I || !GameLoop.I) return;
            var g = GameLoop.I; var P = g.P; HG.QuestsFor(P, GameLoop.Today());
            var box = I.Modal("The Library", new Vector2(720, 680));
            var tabs = new (string id, string label)[] { ("quests", "Quests"), ("feats", $"Feats {P.feats.Count}/{HG.FEATS.Count}"), ("beasts", $"Beasts {HG.BESTIARY.Count(r => P.seenRoles.Contains(r))}/{HG.BESTIARY.Length}"), ("garden", $"Garden {P.flies.Count}/{HG.BUTTERFLIES.Length}") };
            for (int k = 0; k < tabs.Length; k++)
            {
                var t = tabs[k];
                I.BtnAt("Tab" + k, new Vector2(0.5f, 1), new Vector2(-246 + k * 164, -70), new Vector2(158, 56), () => ShowQuests(t.id), null, out var tl, box, null, t.id == tab).rt.pivot = new Vector2(0.5f, 1);
                tl.text = t.label;
            }
            I.perkRefresh = null;
            if (tab == "feats") { FeatNews = false; FeatsTab(box, P, page); }
            else if (tab == "beasts") BeastsTab(box, P, page);
            else if (tab == "garden") GardenTab(box, P, page);
            else QuestsTab(box, g);
            I.Choice(box, "Q_Close", new Vector2(0, 18), new Vector2(230, 56), "Back to the game", () => I.ShowSettings(false));
        }
        // one row: a name, a line under it, and a short note on the right
        void Row(RectTransform box, int i, string name, Color nameCol, string line, string right, Color rightCol)
        {
            float y = -176 - i * 64;
            var n = Label("N" + i, box, new Vector2(0, 1), new Vector2(36, y), 20, TextAnchor.UpperLeft); n.text = name; n.color = nameCol;
            n.rectTransform.pivot = new Vector2(0, 1); n.rectTransform.sizeDelta = new Vector2(470, 26);
            var d = Label("D" + i, box, new Vector2(0, 1), new Vector2(36, y - 27), 15, TextAnchor.UpperLeft); d.font = body; d.text = line; d.color = new Color(0.85f, 0.8f, 0.88f);
            d.rectTransform.pivot = new Vector2(0, 1); d.rectTransform.sizeDelta = new Vector2(500, 22);
            if (right == null) return;
            var r = Label("R" + i, box, new Vector2(1, 1), new Vector2(-36, y - 6), 18, TextAnchor.UpperRight); r.text = right; r.color = rightCol;
            r.rectTransform.pivot = new Vector2(1, 1); r.rectTransform.sizeDelta = new Vector2(150, 48);
        }
        void Pager(RectTransform box, string tab, int page, int pages)
        {
            if (pages <= 1) return;
            var pg = Label("Pg", box, new Vector2(0.5f, 0), new Vector2(0, 92), 16, TextAnchor.MiddleCenter); pg.font = body; pg.text = $"Page {page + 1} of {pages}"; pg.color = new Color(0.85f, 0.8f, 0.88f);
            Choice(box, "Prev", new Vector2(-250, 18), new Vector2(150, 56), "Previous", () => ShowQuests(tab, page - 1), () => page > 0);
            Choice(box, "Next", new Vector2(250, 18), new Vector2(150, 56), "Next", () => ShowQuests(tab, page + 1), () => page < pages - 1);
        }
        static void FeatsTab(RectTransform box, Profile P, int page)
        {
            var rows = HG.FEATS.Select(f => (f, done: P.feats.Contains(f.id), n: HG.FeatProgress(P, f)))
                .OrderBy(x => x.done).ThenByDescending(x => x.n / x.f.need).ToList();   // closest unfinished first, finished last
            int pages = (rows.Count + PAGE - 1) / PAGE; page = Mathf.Clamp(page, 0, pages - 1);
            I.Line(box, -136, $"Each Feat pays Starshards once: bronze {HG.FEAT_PAY[1]}, silver {HG.FEAT_PAY[2]}, gold {HG.FEAT_PAY[3]}", 16, new Color(0.85f, 0.8f, 0.88f));
            for (int i = 0; i < PAGE && page * PAGE + i < rows.Count; i++)
            {
                var (f, done, n) = rows[page * PAGE + i];
                I.Row(box, i, f.name, done ? TIER[f.tier] : CREAM, f.text, done ? "Done" : $"{HG.Fmt(n)} / {HG.Fmt(f.need)}", done ? TIER[f.tier] : RES);
            }
            I.Pager(box, "feats", page, pages);
        }
        static void BeastsTab(RectTransform box, Profile P, int page)
        {
            var list = HG.BESTIARY; int pages = (list.Length + PAGE - 1) / PAGE; page = Mathf.Clamp(page, 0, pages - 1);
            I.Line(box, -136, "Every kind you meet, and what beats it", 16, new Color(0.85f, 0.8f, 0.88f));
            for (int i = 0; i < PAGE && page * PAGE + i < list.Length; i++)
            {
                var R = HG.ROLES[list[page * PAGE + i]]; bool seen = P.seenRoles.Contains(R.id);
                string weak = R.weak == null ? null : "Weak to " + char.ToUpper(R.weak[0]) + R.weak.Substring(1);
                I.Row(box, i, seen ? R.label + (R.boss ? "  <size=14><color=#FF9A7A>boss</color></size>" : "") : "???", seen ? CREAM : new Color(0.6f, 0.55f, 0.62f),
                    seen ? HG.About(R) : "Not met yet", seen ? weak : null, R.weak == "fire" ? new Color(1f, 0.6f, 0.35f) : R.weak == "frost" ? new Color(0.55f, 0.85f, 1f) : new Color(0.85f, 0.7f, 1f));
            }
            I.Pager(box, "beasts", page, pages);
        }
        static void GardenTab(RectTransform box, Profile P, int page)
        {
            var list = HG.BUTTERFLIES; int pages = (list.Length + PAGE - 1) / PAGE; page = Mathf.Clamp(page, 0, pages - 1);
            I.Line(box, -136, $"Caught {P.Stat("flies")}  ·  every kind you catch flutters round the fountain", 16, new Color(0.85f, 0.8f, 0.88f));
            for (int i = 0; i < PAGE && page * PAGE + i < list.Length; i++)
            {
                var k = list[page * PAGE + i]; bool got = P.flies.Contains(k.id), open = HG.FlyOpen(P, k);
                I.Row(box, i, got ? k.name : "???", got ? new Color(k.r, k.g, k.b) : new Color(0.6f, 0.55f, 0.62f),
                    got ? k.line : open && !k.rare ? "On the meadow now: tap one to catch it" : k.line, got ? "Caught" : null, RES);
            }
            I.Pager(box, "garden", page, pages);
        }
        static void QuestsTab(RectTransform box, GameLoop g)
        {
            var om = g.P.omen != null ? HG.OMENS.First(o => o.id == g.P.omen) : null;
            I.Line(box, -136, (om != null ? $"<color=#FFD56B>Today's omen: {om.name}</color> · {om.line}\n" : "") + $"{HG.SHARDS["quest"]} Starshards each, {HG.SHARDS["questBonus"]} more for all three. New quests tomorrow.", 16, new Color(0.85f, 0.8f, 0.88f));
            var rows = new List<(int i, Text prog, Text b)>();
            for (int i = 0; i < g.P.quests.Count; i++)
            {
                var q = g.P.quests[i]; float y = -176 - i * 70;
                var t = I.Label("Q" + i, box, new Vector2(0, 1), new Vector2(36, y), 20, TextAnchor.UpperLeft); t.text = HG.QuestText(q);
                t.rectTransform.pivot = new Vector2(0, 1); t.rectTransform.sizeDelta = new Vector2(440, 26);
                var pr = I.Label("P" + i, box, new Vector2(0, 1), new Vector2(36, y - 27), 15, TextAnchor.UpperLeft); pr.font = I.body; pr.color = RES;
                pr.rectTransform.pivot = new Vector2(0, 1); pr.rectTransform.sizeDelta = new Vector2(300, 22);
                int qi = i;
                I.BtnAt("C" + i, new Vector2(1, 1), new Vector2(-28, y + 4), new Vector2(180, 56), () => g.ClaimQuest(qi), () => g.P.quests[qi].n >= g.P.quests[qi].need && !g.P.quests[qi].claimed, out var bt, box, "shard");
                rows.Add((i, pr, bt));
            }
            I.BtnAt("Bonus", new Vector2(0.5f, 1), new Vector2(0, -176 - 3 * 70 - 10), new Vector2(320, 56), () => g.ClaimBonus(), () => g.BonusReady, out var bonus, box, "shard", true).rt.pivot = new Vector2(0.5f, 1);
            I.perkRefresh = () =>
            {
                foreach (var (i, pr, b) in rows)
                {
                    var q = g.P.quests[i];
                    pr.text = q.claimed ? "Claimed" : $"{q.n} / {q.need}"; b.text = q.claimed ? "Claimed" : q.n >= q.need ? $"Claim · {HG.SHARDS["quest"]}" : "Not yet";
                }
                bonus.text = g.P.questBonus ? "Bonus claimed" : $"All three: +{HG.SHARDS["questBonus"]}";
            };
            I.perkRefresh();
        }
    }
}
