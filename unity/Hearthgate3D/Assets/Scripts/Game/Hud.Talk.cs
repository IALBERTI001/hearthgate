// Conversations with the town's keepers (GameLoop.Npcs), the Chronomancer's training, the time tint over the screen and
// the Time Stop button. A conversation is a card on the one kit: the keeper's portrait (rendered from their Blender model),
// their name and calling, what they say now, and choices that lead on.
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class Hud
    {
        public static int TalkingTo = -1;   // the keeper whose card is open (they gesture while it is)
        public static string KeeperName(string kind) => kind == "astronomer" ? "Orrin the Star-Reader" : kind == "merchant" ? "Mira of the Bazaar" : kind == "librarian" ? "Elder Quill" : "Aurelian the Chronomancer";
        static string Calling(string kind) => kind == "astronomer" ? "Keeper of the Watchtower's stars" : kind == "merchant" ? "Starshard seller of the Market" : kind == "librarian" ? "Keeper of the Library" : "Master of hours, teacher of the time wizard";

        public static void ShowTalk(int who)
        {
            if (!I || !GameLoop.I) return;
            var g = GameLoop.I; var P = g.P; string kind = GameLoop.KeeperOf(who); if (kind == null) return;
            var box = I.Modal(KeeperName(kind), new Vector2(720, 400)); TalkingTo = who;
            var med = I.Box("Portrait", box, new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -70), new Vector2(150, 150)); med.pivot = new Vector2(0, 1);
            var mi = med.gameObject.AddComponent<Image>(); mi.sprite = Panel(INK, BRASS, 18, 3); mi.type = Image.Type.Sliced; mi.raycastTarget = false;
            var pic = I.Box("Pic", med, Vector2.zero, Vector2.one, new Vector2(0, -4), new Vector2(-14, -14)); var pi = pic.gameObject.AddComponent<Image>(); pi.sprite = Icon("npc_" + kind); pi.preserveAspect = true; pi.raycastTarget = false; if (!pi.sprite) pi.enabled = false;
            var who2 = I.Label("Calling", box, new Vector2(0, 1), new Vector2(206, -72), 16, TextAnchor.UpperLeft); who2.font = I.body; who2.text = Calling(kind); who2.color = new Color(0.85f, 0.78f, 0.6f);
            who2.rectTransform.pivot = new Vector2(0, 1); who2.rectTransform.sizeDelta = new Vector2(480, 24);
            var say = I.Label("Say", box, new Vector2(0, 1), new Vector2(206, -102), 19, TextAnchor.UpperLeft); say.font = I.body; say.fontStyle = FontStyle.Normal;
            say.rectTransform.pivot = new Vector2(0, 1); say.rectTransform.sizeDelta = new Vector2(480, 140); say.horizontalOverflow = HorizontalWrapMode.Wrap; say.verticalOverflow = VerticalWrapMode.Overflow;
            var opts = new System.Collections.Generic.List<(string label, string icon, System.Action act)>();
            switch (kind)
            {
                case "astronomer":
                    say.text = P.perkPts > 0 ? $"\"The sky has turned for you, wizard. You have <color=#FF8CD9>{P.perkPts} perk point{(P.perkPts == 1 ? "" : "s")}</color> to spend, and the stars are listening.\""
                                             : "\"Every Ascension the stars remember you a little more. Come back when the sky has turned again.\"";
                    opts.Add(("Show me the stars", "perks", () => { ShowPerks(); }));
                    break;
                case "merchant":
                    say.text = P.shopOpen ? $"\"Starshards for wonders! A new design dresses any tower you like. You carry <color=#FFE68C>{P.shards} Starshards</color>.\""
                                          : "\"My stall is bare until the Market stands again. Rebuild it, and I'll have wonders for you.\"";
                    if (P.shopOpen) opts.Add(("Show me your wares", "shop", () => ShowShop("tower")));
                    break;
                case "librarian":
                    int claim = g.QuestsToClaim;
                    say.text = claim > 0 ? $"\"You've done what the book asked! <color=#FFD56B>{claim} quest{(claim == 1 ? "" : "s")}</color> to claim, and Starshards for each.\""
                                         : FeatNews ? "\"A new Feat is written in the great book in your name. Come and read it.\"" : "\"Today's quests are in the great book. The Feats and the Bestiary keep the rest.\"";
                    opts.Add((claim > 0 ? "Claim my quests" : "What needs doing?", "quests", () => ShowQuests()));
                    opts.Add(("The Feats and beasts", "quests", () => ShowQuests("feats")));
                    break;
                default:   // the Chronomancer
                    int lv = g.TimeStopLevel;
                    say.text = lv == 0 ? "\"So you are the wizard who keeps this gate. Time is a river, and you have only learned to swim in it. Let me teach you to <color=#9FD8FF>stop it</color>, when the walls need a breath.\""
                                       : $"\"Each Ascension is a mark in time. If the walls fall you can rewind to the moment before, for Starshards, or back to your last mark. Your Time Stop holds <color=#9FD8FF>{GameLoop.TS_DUR[lv]:0.#} seconds</color>.\"";
                    opts.Add((lv == 0 ? "Teach me" : "Train with me", "timestop", () => { if (lv == 0) { g.Train(); ShowTalk(who); } else ShowTraining(); }));
                    break;
            }
            opts.Add(("Farewell", null, () => I.ShowSettings(false)));
            float w = opts.Count >= 3 ? 210 : 240, gap = 10, x0 = -(opts.Count - 1) * (w + gap) / 2;
            for (int k = 0; k < opts.Count; k++)
            {
                var o = opts[k];
                var b = I.BtnAt("O" + k, new Vector2(0.5f, 0), new Vector2(x0 + k * (w + gap), 22), new Vector2(w, 56), o.act, null, out var ol, box, o.icon, k == 0 && opts.Count > 1);
                b.rt.pivot = new Vector2(0.5f, 0); ol.text = o.label;
            }
            Sound.Click();
        }

        public static void ShowTraining()
        {
            if (!I || !GameLoop.I) return;
            var g = GameLoop.I; var P = g.P; int lv = g.TimeStopLevel;
            var box = I.Modal("Training with Aurelian", new Vector2(720, 400)); TalkingTo = GameLoop.TRAINER;
            I.Line(box, -66, "The time wizard's skills. Each grows stronger as you train it.", 18, new Color(0.88f, 0.84f, 0.9f));
            var name = I.Label("Skill", box, new Vector2(0, 1), new Vector2(40, -112), 24, TextAnchor.UpperLeft); name.text = $"Time Stop  ·  level {lv}/3";
            name.rectTransform.pivot = new Vector2(0, 1); name.rectTransform.sizeDelta = new Vector2(420, 30);
            var line = I.Label("SkillLine", box, new Vector2(0, 1), new Vector2(40, -148), 17, TextAnchor.UpperLeft); line.font = I.body; line.fontStyle = FontStyle.Normal;
            line.rectTransform.pivot = new Vector2(0, 1); line.rectTransform.sizeDelta = new Vector2(420, 60); line.horizontalOverflow = HorizontalWrapMode.Wrap;
            line.text = $"Every enemy freezes for {GameLoop.TS_DUR[Mathf.Max(1, lv)]:0.#} s while the towers fire on. Ready again {GameLoop.TS_CD[Mathf.Max(1, lv)]:0} s later. Key F."
                        + (lv < 3 ? $"\n<color=#9FD8FF>Next: {GameLoop.TS_DUR[lv + 1]:0.#} s, every {GameLoop.TS_CD[lv + 1]:0} s</color>" : "");
            if (lv < 3)
            {
                int cost = lv == 0 ? 0 : GameLoop.TS_TRAIN[lv];
                var b = I.BtnAt("Train", new Vector2(1, 1), new Vector2(-30, -112), new Vector2(210, 56), () => { g.Train(); ShowTraining(); }, () => lv == 0 || P.shards >= cost, out var tl, box, lv == 0 ? "timestop" : "starshard", true);
                b.rt.pivot = new Vector2(1, 1); tl.text = lv == 0 ? "Learn" : $"Train · {cost}";
            }
            else I.Line(box, -240, "Mastered. Aurelian will have more to teach in time.", 18, new Color(0.62f, 0.85f, 1f));
            I.Choice(box, "T_Back", new Vector2(-130, 22), new Vector2(230, 56), "Back", () => ShowTalk(GameLoop.TRAINER));
            I.Choice(box, "T_Close", new Vector2(130, 22), new Vector2(230, 56), "Farewell", () => I.ShowSettings(false));
        }

        // a colour washed over the whole screen (the time wizard's magic), under the HUD
        Image tint;
        public static void SetTint(Color c)
        {
            if (!I) return;
            if (!I.tint)
            {
                var r = I.Box("TimeTint", I.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); r.SetAsFirstSibling();
                I.tint = r.gameObject.AddComponent<Image>(); I.tint.raycastTarget = false;
            }
            I.tint.color = c; I.tint.enabled = c.a > 0.001f;
        }

        // the Time Stop button: a round skill button with the hourglass and its key, in the free space right of the readouts
        // (the lanes run under every spot along the bottom); while it recharges a dark sweep closes over it and the seconds
        // count down under it
        Act tsAct; Image tsSweep; Text tsTxt;
        void TickTimeStop(GameLoop g)
        {
            if (tsAct == null)
            {
                tsAct = Action("TimeStop", new Vector2(0, 1), new Vector2(LABELS_W + 24, -16), new Vector2(60, 60), "timestop", "F", () => g.UseTimeStop(), () => g.TimeStopReady, false);
                var sw = Box("Sweep", tsAct.p.rt, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8, -8));
                tsSweep = sw.gameObject.AddComponent<Image>(); tsSweep.sprite = Panel(new Color(0.05f, 0.03f, 0.08f, 0.7f), new Color(0, 0, 0, 0), 26, 1);
                tsSweep.type = Image.Type.Filled; tsSweep.fillMethod = Image.FillMethod.Radial360; tsSweep.fillOrigin = 2; tsSweep.fillClockwise = false; tsSweep.raycastTarget = false;
                tsTxt = Label("TsTxt", transform, new Vector2(0, 1), new Vector2(LABELS_W + 54, -80), 15, TextAnchor.UpperCenter); tsTxt.font = body;
                tsTxt.rectTransform.pivot = new Vector2(0.5f, 1); tsTxt.rectTransform.sizeDelta = new Vector2(120, 20);
            }
            bool on = g.TimeStopLevel > 0 && !(Wizard.I && Wizard.I.On);
            if (tsAct.p.rt.gameObject.activeSelf != on) { tsAct.p.rt.gameObject.SetActive(on); tsTxt.gameObject.SetActive(on); }
            if (!on) return;
            float left = g.TimeStopLeft, total = GameLoop.TS_CD[g.TimeStopLevel] + GameLoop.TS_DUR[g.TimeStopLevel];
            tsSweep.fillAmount = g.timeStop > 0 ? 0f : Mathf.Clamp01(left / total);
            tsTxt.text = g.timeStop > 0 ? $"{g.timeStop:0.0} s" : left > 0 ? $"{left:0} s" : "Stop time";
            tsTxt.color = g.TimeStopReady ? new Color(0.72f, 0.9f, 1f) : new Color(0.8f, 0.76f, 0.84f);
            if (tsAct.glow) { var c = tsAct.glow.color; c.a = g.TimeStopReady ? 0.45f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f) : 0f; tsAct.glow.color = c; }
        }
    }
}
