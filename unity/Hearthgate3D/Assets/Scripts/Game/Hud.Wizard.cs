// The wizard's own readouts on his walk (GameLoop.Posts, owner Nov 16 2026): his mana beside the walls' health (blue, the
// fountain's colour), the Captain's bounty on the other side while one is taken, and one plain line over the bar that says
// what the keys do where he stands (at a stair's foot, or at a post on the wall). Readouts in the body face with a thin dark
// rim and no drop shadow (owner, Nov 15 2026: "Shadows under text creates an issue").
using UnityEngine;
using UnityEngine.UI;

namespace Hearthgate.Game
{
    public partial class Hud
    {
        RectTransform manaBox; Image manaFill; Text manaTxt, bountyTxt, wallHint; float manaShown = -1, manaFlash;
        static readonly Color MANA = new Color(0.36f, 0.66f, 1f);
        Text Readout(string n, Transform p, Vector2 anchor, Vector2 pos, int size, TextAnchor al)
        {
            var t = Label(n, p, anchor, pos, size, al); t.font = body; t.fontStyle = FontStyle.Bold;
            foreach (var e in t.GetComponents<Shadow>()) { if (e is Outline) { e.effectDistance = new Vector2(1.5f, -1.5f); e.effectColor = INK; } else e.enabled = false; }
            return t;
        }
        void TickWizardHud(GameLoop g)
        {
            var wz = Wizard.I; var P = g.P;
            if (!manaBox)
            {
                manaBox = Box("ManaBar", transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(286, WALLS_Y + 13), new Vector2(200, 26));
                var fr = manaBox.gameObject.AddComponent<Image>(); fr.sprite = Panel(INK, new Color(0.45f, 0.62f, 0.95f), 10, 3); fr.type = Image.Type.Sliced; fr.raycastTarget = false;
                var fb = Box("FillBox", manaBox, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8, -8));
                manaFill = fb.gameObject.AddComponent<Image>(); manaFill.color = MANA; manaFill.type = Image.Type.Filled; manaFill.fillMethod = Image.FillMethod.Horizontal; manaFill.raycastTarget = false;
                manaFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
                manaTxt = Readout("ManaTxt", manaBox, new Vector2(0.5f, 0.5f), new Vector2(0, 1), 17, TextAnchor.MiddleCenter);
                bountyTxt = Readout("BountyTxt", transform, new Vector2(0.5f, 0), new Vector2(-196, WALLS_Y + 2), 18, TextAnchor.MiddleRight);
                bountyTxt.rectTransform.pivot = new Vector2(1f, 0f); bountyTxt.rectTransform.sizeDelta = new Vector2(240, 26); bountyTxt.color = new Color(0.78f, 0.9f, 1f);
                wallHint = Readout("WallHint", transform, new Vector2(0.5f, 0), new Vector2(0, WALLS_Y + 34), 19, TextAnchor.LowerCenter);
                wallHint.rectTransform.pivot = new Vector2(0.5f, 0f); wallHint.rectTransform.sizeDelta = new Vector2(900, 28);
            }
            bool walk = wz && wz.On && !g.Fallen && !Paused;
            if (manaBox.gameObject.activeSelf != walk) manaBox.gameObject.SetActive(walk);
            if (walk)
            {
                if (manaShown >= 0 && wz.Mana > manaShown + 0.5f && wz.Refilling) manaFlash = 0.4f;
                manaShown = wz.Mana; manaFlash = Mathf.Max(0, manaFlash - Time.unscaledDeltaTime);
                manaFill.fillAmount = wz.Mana / Wizard.MANA_MAX;
                bool dry = wz.Mana < Wizard.SPELL_COST;
                manaFill.color = dry ? new Color(0.4f, 0.45f, 0.6f) : Color.Lerp(MANA, Color.white, manaFlash);
                manaTxt.text = wz.Refilling && wz.Mana < Wizard.MANA_MAX ? $"Mana {wz.Mana:0}  filling" : $"Mana {wz.Mana:0} / {Wizard.MANA_MAX:0}";
            }
            bool bounty = P.bountyOn && !g.Fallen;
            if (bountyTxt.gameObject.activeSelf != bounty) bountyTxt.gameObject.SetActive(bounty);
            if (bounty) bountyTxt.text = g.BountyDone ? "Bounty done: see the Captain" : $"Bounty {P.bountyN} / {g.BountyNeed}";
            string hint = null;
            if (walk && wz.Post >= 0) hint = $"Click a monster: cast {Cap(g.PostElement(wz.Post))}     A / D: next post     S: climb down";
            else if (walk && wz.StairNear >= 0) hint = "E  climb to the wall post";
            if (wallHint.gameObject.activeSelf != (hint != null)) wallHint.gameObject.SetActive(hint != null);
            if (hint != null) { wallHint.text = hint; wallHint.rectTransform.anchoredPosition = new Vector2(0, g.BossNow != null ? BOSS_Y + 40 : WALLS_Y + 34); }
        }
        static string Cap(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
    }
}
