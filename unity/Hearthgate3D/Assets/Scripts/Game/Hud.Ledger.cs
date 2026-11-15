// The town ledger and the wizard's skill list, kept by Tobin at his booth in the square (owner, Nov 14 2026: "we should make
// it easier to navigate the games mechanics. Specially the upgrading buildings. If a player wants to upgrade a building,
// the only option should not be to walk to the building ... a small management booth ... to get the whole picture of the
// town"; "the game should have a skill list and an easy to use upgrading system"). One row per building: its picture, name
// and level, what it makes, the next doubling, and one button that rebuilds or raises it. The Skills tab lists the wizard's
// skills, his rank in each and where he trains them.
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class Hud
    {
        public static void ShowLedger(string tab = "town")
        {
            if (!I || !GameLoop.I) return;
            var g = GameLoop.I; var P = g.P;
            var box = I.Modal("The Town Ledger", new Vector2(760, tab == "skills" ? 380 : CARD_H));   // the skills page is short: no empty half card TalkingTo = GameLoop.STEWARD;
            var tabs = new (string id, string label)[] { ("town", "The town"), ("skills", "Wizard's skills") };
            for (int k = 0; k < tabs.Length; k++)
            {
                var t = tabs[k];
                I.BtnAt("Tab" + k, new Vector2(0.5f, 1), new Vector2(-110 + k * 220, -66), new Vector2(210, 50), () => ShowLedger(t.id), null, out var tl, box, null, t.id == tab).rt.pivot = new Vector2(0.5f, 1);
                tl.text = t.label;
            }
            I.perkRefresh = null;
            if (tab == "skills") SkillsTab(box, g); else TownTab(box, g);
            I.Choice(box, "L_Close", new Vector2(0, 16), new Vector2(230, 52), "Back to the game", () => I.ShowSettings(false));
        }

        static void TownTab(RectTransform box, GameLoop g)
        {
            var P = g.P; var offer = HG.RestoreOffer(P);
            float y0 = -126, row = 41;
            var rates = new Text[HG.BUILDING_IDS.Length];
            for (int i = 0; i < HG.BUILDING_IDS.Length; i++)
            {
                int bi = i; var id = HG.BUILDING_IDS[i]; var b = HG.ById(id); float y = y0 - i * row;
                bool built = P.restored.Contains(id), next = !built && offer.Contains(id);
                var ic = I.Box("I" + i, box, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, y + 2), new Vector2(36, 36)); ic.pivot = new Vector2(0, 1);
                var im = ic.gameObject.AddComponent<Image>(); im.sprite = Icon("b_" + id) ?? Icon("upgrade"); im.preserveAspect = true; im.raycastTarget = false;
                if (!built) im.color = new Color(0.55f, 0.5f, 0.6f, 0.8f);
                int L = HG.LevelOf(P, id);
                var n = I.Label("N" + i, box, new Vector2(0, 1), new Vector2(78, y), 19, TextAnchor.UpperLeft);
                n.text = built ? $"{b.name}  <size=15><color=#CDBFD8>level {L}</color></size>" : b.name; n.supportRichText = true;
                n.color = built ? CREAM : new Color(0.7f, 0.66f, 0.74f);
                n.rectTransform.pivot = new Vector2(0, 1); n.rectTransform.sizeDelta = new Vector2(250, 24);
                var d = I.Label("D" + i, box, new Vector2(0, 1), new Vector2(78, y - 21), 14, TextAnchor.UpperLeft); d.font = I.body; d.supportRichText = true;
                d.rectTransform.pivot = new Vector2(0, 1); d.rectTransform.sizeDelta = new Vector2(300, 20); d.color = new Color(0.85f, 0.8f, 0.88f);
                if (built)
                {
                    double r = HG.BuildingRate(P, id, g.B); string rs = r < 10 ? r.ToString("0.##") : HG.Fmt(r);
                    int m = HG.MILESTONES.FirstOrDefault(x => x > L);
                    d.text = $"<color=#CFE9B0>+{rs}/s</color>" + (m > 0 ? $"   ·   level {m} doubles it" : "");
                }
                else d.text = next ? "In ruins: rebuild it next" : "In ruins";
                if (!built && !next) continue;
                string label; System.Func<bool> on;
                if (built)
                {
                    label = $"Level {L + 1} · {HG.Fmt(g.LevelCost(bi))}"; on = () => g.CanWork(bi);
                }
                else { label = $"Rebuild · {HG.Fmt(g.RestoreCost)}"; on = () => g.CanWork(bi); }
                I.BtnAt("U" + i, new Vector2(1, 1), new Vector2(-30, y + 2), new Vector2(230, 37), () => { g.WorkOn(bi); ShowLedger("town"); }, on, out var ul, box, null, false).rt.pivot = new Vector2(1, 1);
                ul.text = label;
            }
            I.Line(box, y0 - HG.BUILDING_IDS.Length * row - 4, $"The city makes <color=#CFE9B0>+{HG.Fmt(HG.CityRate(P, g.B))}/s</color>. Each building's output doubles at levels {string.Join(", ", HG.MILESTONES)}.", 15, new Color(0.85f, 0.8f, 0.88f));
        }

        static void SkillsTab(RectTransform box, GameLoop g)
        {
            var P = g.P; int lv = g.TimeStopLevel;
            var ic = I.Box("I0", box, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -136), new Vector2(72, 72)); ic.pivot = new Vector2(0, 1);
            var im = ic.gameObject.AddComponent<Image>(); im.sprite = Icon("timestop"); im.preserveAspect = true; im.raycastTarget = false;
            if (lv == 0) im.color = new Color(0.6f, 0.56f, 0.66f, 0.85f);
            var n = I.Label("N0", box, new Vector2(0, 1), new Vector2(130, -134), 24, TextAnchor.UpperLeft); n.supportRichText = true;
            n.text = lv > 0 ? $"Time Stop  <size=17><color=#9FD8FF>rank {lv} of {GameLoop.TS_DUR.Length - 1}</color></size>" : "Time Stop  <size=17><color=#CDBFD8>not learned</color></size>";
            n.rectTransform.pivot = new Vector2(0, 1); n.rectTransform.sizeDelta = new Vector2(560, 30);
            var d = I.Label("D0", box, new Vector2(0, 1), new Vector2(130, -168), 16, TextAnchor.UpperLeft); d.font = I.body; d.supportRichText = true; d.color = new Color(0.88f, 0.84f, 0.9f);
            d.horizontalOverflow = HorizontalWrapMode.Wrap; d.rectTransform.pivot = new Vector2(0, 1); d.rectTransform.sizeDelta = new Vector2(580, 70);
            d.text = lv > 0
                ? $"Freezes every enemy for <color=#9FD8FF>{GameLoop.TS_DUR[lv]:0.#} s</color>; ready again after {GameLoop.TS_CD[lv]:0} s. Press <color=#FFE68C>F</color> or the button over the walls' bar while you walk the town. " + (lv < GameLoop.TS_DUR.Length - 1 ? "Aurelian, by the fountain, trains the next rank." : "Fully trained.")
                : (g.TrainerHere ? "Aurelian the Chronomancer, by the fountain, will teach it." : "An old Chronomancer comes to town after wave 5. He teaches it.");
            I.Line(box, -244, "The wizard's skills are his own: they are ready when he walks the town. Aurelian teaches every one.", 16, new Color(0.8f, 0.76f, 0.84f));
        }
    }
}
