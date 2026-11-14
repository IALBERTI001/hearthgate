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
            { Show(questAct, "Quests", claim > 0 ? $"{claim} to claim" : $"{done} / {P.quests.Count} done", true, 0); questAct.gem.enabled = false; questAct.cost.color = claim > 0 ? RES : CREAM; }
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
                I.BtnAt("Tab" + k, new Vector2(0.5f, 1), new Vector2(-264 + k * 132, -98), new Vector2(124, 56), () => ShowShop(t.cat), () => t.cat != cat, out var tl, box).rt.pivot = new Vector2(0.5f, 1);
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

        public static void ShowQuests()
        {
            if (!I || !GameLoop.I) return;
            var g = GameLoop.I; HG.QuestsFor(g.P, GameLoop.Today());
            var box = I.Modal("Today's quests", new Vector2(680, 430));
            I.Line(box, -62, $"{HG.SHARDS["quest"]} Starshards each, {HG.SHARDS["questBonus"]} more for all three. New quests tomorrow.", 17, new Color(0.85f, 0.8f, 0.88f));
            var rows = new List<(int i, Text prog, Text b)>();
            for (int i = 0; i < g.P.quests.Count; i++)
            {
                var q = g.P.quests[i]; float y = -110 - i * 66;
                var t = I.Label("Q" + i, box, new Vector2(0, 1), new Vector2(32, y), 19, TextAnchor.UpperLeft); t.text = HG.QuestText(q);
                t.rectTransform.pivot = new Vector2(0, 1); t.rectTransform.sizeDelta = new Vector2(400, 24);
                var pr = I.Label("P" + i, box, new Vector2(0, 1), new Vector2(32, y - 26), 15, TextAnchor.UpperLeft); pr.font = I.body; pr.color = RES;
                pr.rectTransform.pivot = new Vector2(0, 1); pr.rectTransform.sizeDelta = new Vector2(300, 22);
                int qi = i;
                I.BtnAt("C" + i, new Vector2(1, 1), new Vector2(-24, y + 4), new Vector2(180, 56), () => g.ClaimQuest(qi), () => g.P.quests[qi].n >= g.P.quests[qi].need && !g.P.quests[qi].claimed, out var bt, box, "shard");
                rows.Add((i, pr, bt));
            }
            I.BtnAt("Bonus", new Vector2(0.5f, 0), new Vector2(0, 86), new Vector2(300, 56), () => g.ClaimBonus(), () => g.BonusReady, out var bonus, box, "shard", true);
            I.Choice(box, "Q_Close", new Vector2(0, 18), new Vector2(230, 56), "Back to the game", () => I.ShowSettings(false));
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
