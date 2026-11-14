// The HUD, built in code on a Canvas so the slice needs no hand-made scene. Kept to the 2D game's budget (game-polish
// rule 1): wave and Resonance top left, the two actions top right with their keys, one banner line, the fork choice.
// Style from art/STYLE.md: Lilita One for display, Nunito for body text, plum #4a2a4f panels with a brass #c9a24a rim,
// cream #fff4dc ink with a dark plum outline; readouts are plain text (game-polish rule 3), only pressables get a panel.
using UnityEngine;
using UnityEngine.UI;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public class Hud : MonoBehaviour
    {
        static Hud I;
        Font font, body; Text wave, res, rate, banner, upLbl, reLbl, gateTxt; Image gateBar; RectTransform forkBox;
        Pressable up, re, walk, work; Text walkLbl, workLbl; RectTransform workCard;

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
                if (!p.rt.gameObject.activeInHierarchy) { p.down = false; continue; }
                bool on = p.on(), inside = RectTransformUtility.RectangleContainsScreenPoint(p.rt, m, null);
                if (dn && inside && on) p.down = true;
                if (upNow) { if (p.down && inside && on) p.act(); p.down = false; }
                float target = !on ? 1f : p.down && inside ? 0.95f : inside ? 1.04f : 1f;
                p.s = Mathf.MoveTowards(p.s, target, Time.unscaledDeltaTime * 1.2f); p.rt.localScale = Vector3.one * p.s;
                if (p.img) p.img.color = on ? (inside ? Color.Lerp(p.col, Color.white, 0.12f) : p.col) : new Color(p.col.r * 0.6f, p.col.g * 0.6f, p.col.b * 0.6f, p.col.a * 0.8f);
            }
        }
        float bannerT;
        static readonly Color INK = new Color(0.16f, 0.08f, 0.19f, 0.95f), CREAM = new Color(1f, 0.957f, 0.863f),
            PLUM = new Color(0.29f, 0.165f, 0.31f, 0.96f), BRASS = new Color(0.79f, 0.635f, 0.29f), RES = new Color(1f, 0.84f, 0.42f);

        // the Resonance gem: a gold diamond with a light facet, drawn once (a glyph, like a font character)
        static Sprite Gem()
        {
            const int S = 48; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[S * S]; var gold = new Color(1f, 0.8f, 0.3f); var dark = new Color(0.55f, 0.32f, 0.12f); var lit = new Color(1f, 0.95f, 0.7f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float u = (x + 0.5f) / S * 2 - 1, v = (y + 0.5f) / S * 2 - 1, d = Mathf.Abs(u) / 0.78f + Mathf.Abs(v);   // a diamond
                    float a = Mathf.Clamp01((1f - d) * S * 0.5f);
                    var c = d > 0.8f ? dark : (u < 0 && v > 0 ? lit : gold);
                    c.a = a; px[y * S + x] = c;
                }
            t.SetPixels(px); t.Apply();
            return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
        }
        // a horizontal fade, for the soft shade behind the top-left readouts (game-polish rule 3: no pills, a gentle shade)
        static Sprite Fade()
        {
            var t = new Texture2D(64, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int x = 0; x < 64; x++) for (int y = 0; y < 4; y++) t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Pow(1 - x / 63f, 1.5f)));
            t.Apply(); return Sprite.Create(t, new Rect(0, 0, 64, 4), new Vector2(0, 1));
        }

        // a rounded panel with a rim and a soft top light, nine-sliced so it scales to any box
        static Sprite Panel(Color fill, Color rim, int radius = 14, int border = 3)
        {
            const int S = 64; var t = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[S * S];
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = Mathf.Max(radius - x - 0.5f, x + 0.5f - (S - radius), 0), dy = Mathf.Max(radius - y - 0.5f, y + 0.5f - (S - radius), 0);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;            // signed distance to the rounded edge (inside < 0)
                    float a = Mathf.Clamp01(0.5f - d);
                    var c = d > -border ? rim : Color.Lerp(fill, Color.Lerp(fill, Color.white, 0.12f), Mathf.Clamp01((y - S * 0.55f) / (S * 0.45f)));
                    c.a *= a; px[y * S + x] = c;
                }
            t.SetPixels(px); t.Apply();
            int b = radius + 2;
            return Sprite.Create(t, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        public void Init()
        {
            I = this;
            var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            font = Resources.Load<Font>("Fonts/LilitaOne-Regular") ?? fallback; body = Resources.Load<Font>("Fonts/Nunito-Variable") ?? fallback;
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var sc = gameObject.AddComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1280, 720); sc.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var pops = Box("Pops", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            gameObject.AddComponent<Fx>().Init(pops, font);

            var shade = Box("Shade", transform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(380, 130)); shade.pivot = new Vector2(0, 1);
            var si = shade.gameObject.AddComponent<Image>(); si.sprite = Fade(); si.color = new Color(0.16f, 0.08f, 0.19f, 0.55f); si.raycastTarget = false;
            wave = Label("Wave", transform, new Vector2(0, 1), new Vector2(22, -12), 28, TextAnchor.UpperLeft);
            var gem = Box("Gem", transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -52), new Vector2(34, 34)); gem.pivot = new Vector2(0, 1);
            var gi = gem.gameObject.AddComponent<Image>(); gi.sprite = Gem(); gi.raycastTarget = false;
            res = Label("Res", transform, new Vector2(0, 1), new Vector2(60, -46), 40, TextAnchor.UpperLeft); res.color = RES;
            rate = Label("Rate", transform, new Vector2(0, 1), new Vector2(24, -94), 18, TextAnchor.UpperLeft); rate.font = body;
            banner = Label("Banner", transform, new Vector2(0.5f, 1), new Vector2(0, -22), 34, TextAnchor.UpperCenter); banner.color = RES;

            // the gate's health, centred over the bottom of the view
            var bar = Box("GateBar", transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(360, 26));
            var frame = bar.gameObject.AddComponent<Image>(); frame.sprite = Panel(INK, BRASS, 10, 3); frame.type = Image.Type.Sliced;
            var fillBox = Box("FillBox", bar, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8, -8));
            gateBar = fillBox.gameObject.AddComponent<Image>();
            gateBar.color = new Color(0.52f, 0.85f, 0.36f); gateBar.type = Image.Type.Filled; gateBar.fillMethod = Image.FillMethod.Horizontal;
            gateBar.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
            gateTxt = Label("GateTxt", bar, new Vector2(0.5f, 0.5f), new Vector2(0, 1), 18, TextAnchor.MiddleCenter);

            // top right, over the far meadow: the city fills the bottom of the view (LayoutCheck keeps it clear)
            up = Btn("Upgrade", new Vector2(-16, -16), () => GameLoop.I.Upgrade(), () => GameLoop.I.CanUpgrade, out upLbl);
            re = Btn("Rebuild", new Vector2(-16, -78), () => GameLoop.I.Restore(), () => GameLoop.I.CanRestore, out reLbl);

            // the town walk (owner, Nov 13 2026): one button over the gate bar, between the inner lanes; while walking, a
            // card over it offers the work on the building in reach
            walk = BtnAt("Walk", new Vector2(0.5f, 0), new Vector2(0, 62), new Vector2(230, 50), () => Wizard.I?.Toggle(), null, out walkLbl);
            work = BtnAt("Work", new Vector2(0.5f, 0), new Vector2(0, 122), new Vector2(430, 78), () => { var w = Wizard.I; if (w && w.Near >= 0) GameLoop.I.WorkOn(w.Near); },
                () => Wizard.I && Wizard.I.Near >= 0 && GameLoop.I.CanWork(Wizard.I.Near), out workLbl);
            workCard = work.rt; workCard.gameObject.SetActive(false);
        }

        // true when the pointer is over a pressable control (the town walk ignores those taps)
        public static bool OnHud(Vector2 m)
        {
            if (!I) return false;
            foreach (var p in I.press) if (p.rt && p.rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(p.rt, m, null)) return true;
            return I.forkBox != null;
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
        Pressable BtnAt(string n, Vector2 anchor, Vector2 pos, Vector2 size, System.Action act, System.Func<bool> on, out Text lbl)
        {
            var r = Box(n, transform, anchor, anchor, pos, size); r.pivot = anchor;
            var img = r.gameObject.AddComponent<Image>(); img.sprite = Panel(PLUM, BRASS); img.type = Image.Type.Sliced; img.color = Color.white;
            lbl = Label("L", r, new Vector2(0.5f, 0.5f), Vector2.zero, 20, TextAnchor.MiddleCenter);
            Fit(lbl, r.sizeDelta - new Vector2(20, 8), 12);
            return Press(r, act, on);
        }
        Pressable Btn(string n, Vector2 pos, System.Action act, System.Func<bool> on, out Text lbl)
        {
            var r = Box(n, transform, new Vector2(1, 1), new Vector2(1, 1), pos, new Vector2(230, 56)); r.pivot = new Vector2(1, 1);   // slimmer (Nov 13 2026): the ring's outer towers stand under the corners
            var img = r.gameObject.AddComponent<Image>(); img.sprite = Panel(PLUM, BRASS); img.type = Image.Type.Sliced; img.color = Color.white;
            lbl = Label("L", r, new Vector2(0.5f, 0.5f), Vector2.zero, 20, TextAnchor.MiddleCenter);
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
            double gm = HG.GateMax(P, g.B); float gf = Mathf.Clamp01((float)(g.gateHp / gm)); gateBar.fillAmount = gf;
            gateBar.color = gf > 0.5f ? Color.Lerp(new Color(1f, 0.78f, 0.25f), new Color(0.5f, 0.85f, 0.35f), (gf - 0.5f) * 2) : Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(1f, 0.78f, 0.25f), gf * 2); gateTxt.text = $"Gate {HG.Fmt(System.Math.Max(0, g.gateHp))} / {HG.Fmt(gm)}";
            // keyboard keys on desktop only (game-polish rule 12); a phone or tablet shows just the action and its cost
            string keyU = Application.isMobilePlatform ? "" : "   <color=#D9C9A8>[U]</color>", keyR = Application.isMobilePlatform ? "" : "   <color=#D9C9A8>[R]</color>";
            upLbl.text = $"Upgrade gate\n<color=#FFD56B>{HG.Fmt(g.UpgradeCost)}</color>{keyU}";
            var nb = g.NextBuilding;
            reLbl.text = nb == null ? "City rebuilt" : $"Rebuild {HG.ById(nb).name}\n<color=#FFD56B>{HG.Fmt(g.RestoreCost)}</color>{keyR}";
            // the town walk
            var wz = Wizard.I; string keyT = Application.isMobilePlatform ? "" : "   <color=#D9C9A8>[T]</color>", keyE = Application.isMobilePlatform ? "" : "   <color=#D9C9A8>[E]</color>";
            walkLbl.text = (wz && wz.On ? "Back to the walls" : "Walk the town") + keyT;
            bool showWork = wz && wz.On && wz.Near >= 0; if (workCard.gameObject.activeSelf != showWork) workCard.gameObject.SetActive(showWork);
            if (showWork)
            {
                var id = HG.BUILDING_IDS[wz.Near]; var b = HG.ById(id);
                if (P.restored.Contains(id))
                {
                    int L = HG.LevelOf(P, id); double now = HG.BuildingRate(P, id, g.B), next = HG.BuildingRate(P, id, g.B, L + 1);
                    workLbl.text = $"Upgrade {b.name}  ·  level {L} to {L + 1}\n+{HG.Fmt(next - now)}/s   <color=#FFD56B>{HG.Fmt(g.LevelCost(wz.Near))}</color>{keyE}";
                }
                else if (HG.RestoreOffer(P).Contains(id)) workLbl.text = $"Rebuild {b.name}\n{b.line}   <color=#FFD56B>{HG.Fmt(g.RestoreCost)}</color>{keyE}";
                else workLbl.text = $"Ruined {b.name}\n<color=#D9C9A8>Rebuild the marked plot first</color>";
            }
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
            var bi = box.gameObject.AddComponent<Image>(); bi.sprite = Panel(INK, BRASS, 18, 4); bi.type = Image.Type.Sliced; I.forkBox = box;
            I.Label("T", box, new Vector2(0.5f, 1), new Vector2(0, -18), 28, TextAnchor.UpperCenter).text = "The gate grows. Choose one:";
            var opts = HG.FORKS[stage];
            for (int k = 0; k < opts.Length; k++)
            {
                var f = opts[k];
                var r = I.Box(f.id, box, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(k == 0 ? -150 : 150, 30), new Vector2(280, 150)); r.pivot = new Vector2(0.5f, 0);
                var ri = r.gameObject.AddComponent<Image>(); ri.sprite = Panel(PLUM, BRASS); ri.type = Image.Type.Sliced;
                I.Press(r, () => { GameLoop.I.Fork(stage, f.id); Destroy(box.gameObject); });
                I.Label("N", r, new Vector2(0.5f, 1), new Vector2(0, -14), 24, TextAnchor.UpperCenter).text = f.name;
                var n = r.GetChild(r.childCount - 1).GetComponent<Text>(); Fit(n, new Vector2(260, 34), 14); n.rectTransform.anchoredPosition = new Vector2(0, -30);
                var d = I.Label("D", r, new Vector2(0.5f, 0.5f), new Vector2(0, -16), 18, TextAnchor.MiddleCenter); d.text = f.line; d.color = Field.GOLD; d.font = I.body; Fit(d, new Vector2(256, 80), 12); d.rectTransform.anchoredPosition = new Vector2(0, -14);
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
