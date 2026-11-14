// The HUD, built in code on a Canvas so the slice needs no hand-made scene. Kept to the 2D game's budget (game-polish
// rule 1): wave and Resonance top left, the two actions bottom right with their keys, one banner line, the fork choice.
using UnityEngine;
using UnityEngine.UI;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public class Hud : MonoBehaviour
    {
        static Hud I;
        Font font; Text wave, res, rate, banner, upLbl, reLbl, gateTxt; Image gateBar; RectTransform forkBox;
        Pressable up, re;

        // A control this HUD answers itself, from the raw pointer. uGUI's event system drops pointer input whenever the page
        // does not report focus to Unity (Windows hosts), which left the buttons dead in a browser pane while the keys
        // worked (Nov 13 2026); an embedded iframe can do the same. Hover grows it, press squeezes it (game-polish 17).
        class Pressable
        {
            public RectTransform rt; public Image img; public Color col; public System.Func<bool> on; public System.Action act;
            public bool down; public float s = 1;
        }
        readonly System.Collections.Generic.List<Pressable> press = new System.Collections.Generic.List<Pressable>();
        Pressable Press(RectTransform rt, System.Action act, System.Func<bool> on = null)
        {
            var img = rt.GetComponent<Image>();
            var p = new Pressable { rt = rt, img = img, col = img ? img.color : Color.white, on = on ?? (() => true), act = act };
            press.Add(p); return p;
        }
        void Pointer()
        {
            Vector2 m = Input.mousePosition; bool dn = Input.GetMouseButtonDown(0), upNow = Input.GetMouseButtonUp(0);
            press.RemoveAll(p => p.rt == null);
            foreach (var p in press.ToArray())
            {
                bool on = p.on(), inside = RectTransformUtility.RectangleContainsScreenPoint(p.rt, m, null);
                if (dn && inside && on) p.down = true;
                if (upNow) { if (p.down && inside && on) p.act(); p.down = false; }
                float target = !on ? 1f : p.down && inside ? 0.95f : inside ? 1.04f : 1f;
                p.s = Mathf.MoveTowards(p.s, target, Time.unscaledDeltaTime * 1.2f); p.rt.localScale = Vector3.one * p.s;
                if (p.img) p.img.color = on ? (inside ? Color.Lerp(p.col, Color.white, 0.12f) : p.col) : new Color(p.col.r * 0.6f, p.col.g * 0.6f, p.col.b * 0.6f, p.col.a * 0.8f);
            }
        }
        float bannerT;
        static readonly Color INK = new Color(0.12f, 0.07f, 0.1f, 0.92f), CREAM = new Color(1f, 0.96f, 0.86f);

        public void Init()
        {
            I = this;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var pops = Box("Pops", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            gameObject.AddComponent<Fx>().Init(pops, font);

            wave = Label("Wave", transform, new Vector2(0, 1), new Vector2(24, -18), 30, TextAnchor.UpperLeft);
            res = Label("Res", transform, new Vector2(0, 1), new Vector2(24, -58), 40, TextAnchor.UpperLeft); res.color = Field.GOLD;
            rate = Label("Rate", transform, new Vector2(0, 1), new Vector2(24, -104), 20, TextAnchor.UpperLeft);
            banner = Label("Banner", transform, new Vector2(0.5f, 1), new Vector2(0, -24), 30, TextAnchor.UpperCenter);

            // the gate's health, centred over the bottom of the view
            var bar = Box("GateBar", transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(360, 18));
            bar.gameObject.AddComponent<Image>().color = INK;
            gateBar = Box("Fill", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            gateBar.color = new Color(0.48f, 0.82f, 0.35f); gateBar.type = Image.Type.Filled; gateBar.fillMethod = Image.FillMethod.Horizontal;
            gateBar.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
            gateTxt = Label("GateTxt", bar, new Vector2(0.5f, 0.5f), Vector2.zero, 16, TextAnchor.MiddleCenter);

            up = Btn("Upgrade", new Vector2(-24, 104), () => GameLoop.I.Upgrade(), () => GameLoop.I.CanUpgrade, out upLbl);
            re = Btn("Rebuild", new Vector2(-24, 24), () => GameLoop.I.Restore(), () => GameLoop.I.CanRestore, out reLbl);
        }

        RectTransform Box(string n, Transform p, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var g = new GameObject(n, typeof(RectTransform)); g.transform.SetParent(p, false);
            var r = (RectTransform)g.transform; r.anchorMin = aMin; r.anchorMax = aMax; r.pivot = (aMin + aMax) / 2; r.anchoredPosition = pos; r.sizeDelta = size; return r;
        }
        Text Label(string n, Transform p, Vector2 anchor, Vector2 pos, int size, TextAnchor al)
        {
            var r = Box(n, p, anchor, anchor, pos, new Vector2(900, size * 1.4f)); r.pivot = anchor;
            var t = r.gameObject.AddComponent<Text>(); t.font = font; t.fontSize = size; t.fontStyle = FontStyle.Bold; t.color = CREAM; t.alignment = al;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.raycastTarget = false;
            var o = r.gameObject.AddComponent<Outline>(); o.effectColor = INK; o.effectDistance = new Vector2(2, -2);
            r.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(0, -4);
            return t;
        }
        // keep a label inside its box: wrap at the box width (game-polish rule 5: nothing ever clips). Not Unity's best fit:
        // when it judges the text cannot fit it draws nothing, and the button labels vanished at 800x450 (Nov 13 2026).
        // Instead the size steps down here until the wrapped text fits the box, never below min.
        static void Fit(Text t, Vector2 box, int min)
        {
            var r = t.rectTransform; r.pivot = new Vector2(0.5f, 0.5f); r.sizeDelta = box;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.resizeTextForBestFit = false;
            fits.Add((t, t.fontSize, min, box.y));
        }
        static readonly System.Collections.Generic.List<(Text t, int max, int min, float h)> fits = new System.Collections.Generic.List<(Text, int, int, float)>();
        static readonly System.Collections.Generic.Dictionary<Text, string> fitText = new System.Collections.Generic.Dictionary<Text, string>();
        static void Refit()
        {
            fits.RemoveAll(f => f.t == null);
            foreach (var f in fits)
            {
                if (fitText.TryGetValue(f.t, out var was) && was == f.t.text) continue;   // measure only when the words change
                fitText[f.t] = f.t.text;
                int size = f.max; f.t.fontSize = size;
                while (size > f.min && f.t.preferredHeight > f.h) f.t.fontSize = --size;
            }
        }
        Pressable Btn(string n, Vector2 pos, System.Action act, System.Func<bool> on, out Text lbl)
        {
            var r = Box(n, transform, new Vector2(1, 0), new Vector2(1, 0), pos, new Vector2(300, 68)); r.pivot = new Vector2(1, 0);
            var img = r.gameObject.AddComponent<Image>(); img.color = new Color(0.27f, 0.15f, 0.2f, 0.95f);
            lbl = Label("L", r, new Vector2(0.5f, 0.5f), Vector2.zero, 22, TextAnchor.MiddleCenter);
            Fit(lbl, r.sizeDelta - new Vector2(20, 8), 12);
            return Press(r, act, on);
        }

        void Update()
        {
            var g = GameLoop.I; if (g == null) return; var P = g.P;
            Pointer();
            wave.text = $"Wave {P.wave}";
            res.text = HG.Fmt(P.res);
            double r = HG.CityRate(P, g.B); rate.text = r > 0 ? $"+{HG.Fmt(r)}/s from the city" : "";
            double gm = HG.GateMax(P, g.B); gateBar.fillAmount = Mathf.Clamp01((float)(g.gateHp / gm)); gateTxt.text = $"Gate {HG.Fmt(System.Math.Max(0, g.gateHp))} / {HG.Fmt(gm)}";
            upLbl.text = $"Upgrade gate\n{HG.Fmt(g.UpgradeCost)}   [U]"; 
            var nb = g.NextBuilding;
            reLbl.text = nb == null ? "City rebuilt" : $"Rebuild {HG.ById(nb).name}\n{HG.Fmt(g.RestoreCost)}   [R]"; 
            Refit();
            if (bannerT > 0) { bannerT -= Time.deltaTime; var c = banner.color; c.a = Mathf.Clamp01(bannerT / 0.4f); banner.color = c; }
        }

        public static void Banner(string s) { if (!I) return; I.banner.text = s; I.bannerT = 2.6f; var c = I.banner.color; c.a = 1; I.banner.color = c; }
        public static void Toast(string s) => Banner(s);

        // a gate stage asks for one of two upgrades
        public static void ShowFork(int stage)
        {
            if (!I || I.forkBox) return;
            var box = I.Box("Fork", I.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 260));
            box.gameObject.AddComponent<Image>().color = new Color(0.16f, 0.09f, 0.12f, 0.96f); I.forkBox = box;
            I.Label("T", box, new Vector2(0.5f, 1), new Vector2(0, -18), 28, TextAnchor.UpperCenter).text = "The gate grows. Choose one:";
            var opts = HG.FORKS[stage];
            for (int k = 0; k < opts.Length; k++)
            {
                var f = opts[k];
                var r = I.Box(f.id, box, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(k == 0 ? -150 : 150, 30), new Vector2(280, 150)); r.pivot = new Vector2(0.5f, 0);
                r.gameObject.AddComponent<Image>().color = new Color(0.3f, 0.18f, 0.24f);
                I.Press(r, () => { GameLoop.I.Fork(stage, f.id); Destroy(box.gameObject); });
                I.Label("N", r, new Vector2(0.5f, 1), new Vector2(0, -14), 24, TextAnchor.UpperCenter).text = f.name;
                var n = r.GetChild(r.childCount - 1).GetComponent<Text>(); Fit(n, new Vector2(260, 34), 14); n.rectTransform.anchoredPosition = new Vector2(0, -30);
                var d = I.Label("D", r, new Vector2(0.5f, 0.5f), new Vector2(0, -16), 18, TextAnchor.MiddleCenter); d.text = f.line; d.color = Field.GOLD; Fit(d, new Vector2(256, 80), 12); d.rectTransform.anchoredPosition = new Vector2(0, -14);
            }
        }
    }

    // a storm chain: a jagged line of light between two enemies, gone in a fifth of a second
    public static class Bolt
    {
        public static void Arc(Vector3 a, Vector3 b)
        {
            var g = new GameObject("bolt"); var lr = g.AddComponent<LineRenderer>();
            lr.material = Art.Mat(Field.STORM, true); lr.widthMultiplier = 0.12f; lr.positionCount = 7;
            for (int i = 0; i < 7; i++) { float t = i / 6f; var p = Vector3.Lerp(a, b, t); if (i > 0 && i < 6) p += Random.insideUnitSphere * 0.35f; lr.SetPosition(i, p); }
            Object.Destroy(g, 0.2f);
        }
    }
}
