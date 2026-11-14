// The HUD, built in code on a Canvas so the slice needs no hand-made scene. Kept to the 2D game's budget (game-polish
// rule 1): wave and Resonance top left, the two actions top right with their keys, one banner line, the fork choice.
// Style from art/STYLE.md: Lilita One for display, Nunito for body text, plum #4a2a4f panels with a brass #c9a24a rim,
// cream #fff4dc ink with a dark plum outline; readouts are plain text (game-polish rule 3), only pressables get a panel.
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public class Hud : MonoBehaviour
    {
        static Hud I;
        Font font, body; Text wave, res, rate, banner, gateTxt; Image gateBar; RectTransform forkBox;
        Act up, re, walk, work; RectTransform workCard; double shownRes = -1;
        Sprite gemSprite; static Sprite Icon(string n) => Resources.Load<Sprite>("Icons/icon_" + n);
        readonly RectTransform[] signs = new RectTransform[10]; readonly Text[] signTxt = new Text[10];   // the buildings' signs, written over their boards

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
            Vector2 m = PointerPos; bool dn = Input.GetMouseButtonDown(0), upNow = Input.GetMouseButtonUp(0);
            press.RemoveAll(p => p.rt == null);
            foreach (var p in press.ToArray())
            {
                if (!p.rt.gameObject.activeInHierarchy || settingsBox && !p.rt.IsChildOf(settingsBox)) { p.down = false; continue; }   // an open menu takes every press
                bool on = p.on(), inside = RectTransformUtility.RectangleContainsScreenPoint(p.rt, m, null);
                if (dn && inside) { if (on) p.down = true; else Sound.Dud(); }   // a soft low thud on a button that cannot act yet
                if (upNow) { if (p.down && inside && on) { Sound.Click(); p.act(); } p.down = false; }
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
            gemSprite = Gem(); var gi = gem.gameObject.AddComponent<Image>(); gi.sprite = gemSprite; gi.raycastTarget = false;
            res = Label("Res", transform, new Vector2(0, 1), new Vector2(60, -46), 40, TextAnchor.UpperLeft); res.color = RES;
            rate = Label("Rate", transform, new Vector2(0, 1), new Vector2(24, -94), 18, TextAnchor.UpperLeft); rate.font = body;
            // the banner sits under the top row of the HUD, never across it (QA, Nov 14 2026: in a portrait window the walk
            // banner ran under the cog and the Upgrade button), and wraps to the window's width
            banner = Label("Banner", transform, new Vector2(0.5f, 1), new Vector2(0, -136), 34, TextAnchor.UpperCenter); banner.color = RES;
            banner.horizontalOverflow = HorizontalWrapMode.Wrap; banner.verticalOverflow = VerticalWrapMode.Overflow;

            // the gate's health, centred over the bottom of the view
            var bar = Box("GateBar", transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 40), new Vector2(360, 26));
            var frame = bar.gameObject.AddComponent<Image>(); frame.sprite = Panel(INK, BRASS, 10, 3); frame.type = Image.Type.Sliced;
            var fillBox = Box("FillBox", bar, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8, -8));
            gateBar = fillBox.gameObject.AddComponent<Image>();
            gateBar.color = new Color(0.52f, 0.85f, 0.36f); gateBar.type = Image.Type.Filled; gateBar.fillMethod = Image.FillMethod.Horizontal;
            gateBar.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
            gateTxt = Label("GateTxt", bar, new Vector2(0.5f, 0.5f), new Vector2(0, 1), 18, TextAnchor.MiddleCenter);

            // top right, over the far meadow: the city fills the bottom of the view (LayoutCheck keeps it clear)
            // (owner, Nov 14 2026: "Buttons are a mess, doesnt look polished"): one action kit for every button (game-polish
            // rule 7): an icon rendered from the game's own model in a brass medallion, the action in the display font, its
            // cost with the gem, the key as a small keycap on desktop. Upgrade gate is the primary action (rule 2): a
            // brighter rim that breathes when it is affordable. A finished goal leaves (rule 4): no "City rebuilt" button.
            var tr = new Vector2(1, 1);
            up = Action("Upgrade", tr, new Vector2(-16, -16), new Vector2(230, 56), "tower", "U", () => GameLoop.I.Upgrade(), () => GameLoop.I.CanUpgrade, true);
            re = Action("Rebuild", tr, new Vector2(-16, -78), new Vector2(230, 56), "rebuild", "R", () => GameLoop.I.Restore(), () => GameLoop.I.CanRestore, false);

            // the town walk and, while walking, the work on the building in reach: in the same group as the other actions
            // (owner, Nov 14 2026: "the walk the town button should be with the other buttons. No reason to separate
            // actions"); LayoutActions places the group every frame
            walk = Action("Walk", tr, new Vector2(-16, -140), new Vector2(230, 56), "walk", "T", () => Wizard.I?.Toggle(), null, false);
            work = Action("Work", tr, new Vector2(-16, -202), new Vector2(280, 56), "upgrade", "E", () => { var w = Wizard.I; if (w && w.Near >= 0) GameLoop.I.WorkOn(w.Near); },
                () => Wizard.I && Wizard.I.Near >= 0 && GameLoop.I.CanWork(Wizard.I.Near), true);
            workCard = work.p.rt; workCard.gameObject.SetActive(false);

            // settings (game-polish rule 19: the controls inside the game, the music and sound switches): a small brass
            // medallion with a cog rendered in Blender, inside the top-left label block, right of the wave
            var gear = Box("Settings", transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(200, -14), new Vector2(44, 44)); gear.pivot = new Vector2(0, 1);
            var gimg = gear.gameObject.AddComponent<Image>(); gimg.sprite = Panel(INK, BRASS, 21, 3); gimg.type = Image.Type.Sliced;
            var gic = Box("Icon", gear, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8, -8));
            var gii = gic.gameObject.AddComponent<Image>(); gii.sprite = Icon("gear"); gii.preserveAspect = true; gii.raycastTarget = false; if (!gii.sprite) gii.enabled = false;
            Press(gear, () => ShowSettings(true));

            // each building's sign (owner, Nov 13 2026: "a data sign or something that shows the level and stats of the
            // buildings"): a small plum board with its name, level and output, pinned over the signpost in its yard while
            // the wizard walks the town; behind the HUD controls, so the cards and buttons stay on top
            for (int i = 0; i < signs.Length; i++)
            {
                var r = Box("Sign" + i, transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(240, 66)); r.pivot = new Vector2(0.5f, 0.5f);
                var im = r.gameObject.AddComponent<Image>(); im.sprite = Panel(PLUM, BRASS, 10, 3); im.type = Image.Type.Sliced; im.raycastTarget = false;
                signTxt[i] = Label("T", r, new Vector2(0.5f, 0.5f), Vector2.zero, 19, TextAnchor.MiddleCenter);
                signTxt[i].font = body; Fit(signTxt[i], new Vector2(226, 60), 14);
                r.SetSiblingIndex(1); r.gameObject.SetActive(false); signs[i] = r;
            }

            // the fountain's board (game-polish rule 6, in the world): ready, it says so and a tap starts the Surge; on, it
            // counts down. Hidden while the fountain refills.
            surgeTag = Box("SurgeTag", transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(190, 44)); surgeTag.pivot = new Vector2(0.5f, 0);
            var sti = surgeTag.gameObject.AddComponent<Image>(); sti.sprite = Panel(PLUM, new Color(1f, 0.62f, 0.86f), 12, 3); sti.type = Image.Type.Sliced;
            surgeTxt = Label("T", surgeTag, new Vector2(0.5f, 0.5f), new Vector2(0, 1), 19, TextAnchor.MiddleCenter); surgeTxt.color = new Color(1f, 0.8f, 0.94f);
            surgeTxt.rectTransform.pivot = new Vector2(0.5f, 0.5f); surgeTxt.rectTransform.sizeDelta = new Vector2(180, 40);
            Press(surgeTag, () => GameLoop.I.TapSurge()); surgeTag.SetSiblingIndex(1); surgeTag.gameObject.SetActive(false);
            // the Tavern's Cheer meter, over its roof
            cheerTag = Box("CheerTag", transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(120, 30)); cheerTag.pivot = new Vector2(0.5f, 0);
            var cbg = cheerTag.gameObject.AddComponent<Image>(); cbg.sprite = Panel(INK, BRASS, 8, 2); cbg.type = Image.Type.Sliced; cbg.raycastTarget = false;
            var cf = Box("Fill", cheerTag, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8, -8));
            cheerFill = cf.gameObject.AddComponent<Image>(); cheerFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
            cheerFill.type = Image.Type.Filled; cheerFill.fillMethod = Image.FillMethod.Horizontal; cheerFill.raycastTarget = false;
            var ct = Label("T", cheerTag, new Vector2(0.5f, 0.5f), new Vector2(0, 1), 15, TextAnchor.MiddleCenter); ct.text = "Cheer"; ct.font = body;
            ct.rectTransform.pivot = new Vector2(0.5f, 0.5f); ct.rectTransform.sizeDelta = new Vector2(110, 26);
            cheerTag.SetSiblingIndex(1); cheerTag.gameObject.SetActive(false);
        }
        RectTransform surgeTag, cheerTag; Text surgeTxt; Image cheerFill;
        // a HUD element pinned over a point in the world; false when that point is off screen
        bool Pin(RectTransform r, Vector3 at, float near = 9999f)
        {
            var cam = Camera.main; var canvas = (RectTransform)transform; if (!cam) return false;
            var sp = cam.WorldToScreenPoint(at); float dist = Vector3.Distance(cam.transform.position, at);
            bool show = sp.z > 0 && dist < near && sp.x > 0 && sp.x < Screen.width && sp.y > 0 && sp.y < Screen.height;
            if (r.gameObject.activeSelf != show) r.gameObject.SetActive(show);
            if (!show) return false;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, sp, null, out var lp);
            r.anchoredPosition = lp + canvas.rect.size * 0.5f; return true;
        }
        void TownTags(GameLoop g, Profile P)
        {
            bool tagOn = !P.firstSession && (g.SurgeOn || g.SurgeReady);
            if (tagOn && Pin(surgeTag, Field.PLAZA + new Vector3(0, 4.2f, 0)))
                surgeTxt.text = g.SurgeOn ? $"x2  ·  {GameLoop.Mmss(g.SurgeLeftMs)}" : (Application.isMobilePlatform ? "Surge ready: tap" : "Surge ready  [X]");
            else if (!tagOn && surgeTag.gameObject.activeSelf) surgeTag.gameObject.SetActive(false);
            int ti = System.Array.IndexOf(HG.BUILDING_IDS, "tavern");
            if (P.restored.Contains("tavern") && Pin(cheerTag, Field.Plot(ti) + Vector3.up * (Field.PlotHeight(ti) + 0.8f)))
            {
                cheerFill.fillAmount = g.CheerShown;
                cheerFill.color = g.cheerT > 0 ? new Color(1f, 0.84f, 0.42f) : new Color(0.9f, 0.6f, 0.23f);
            }
            else if (!P.restored.Contains("tavern") && cheerTag.gameObject.activeSelf) cheerTag.gameObject.SetActive(false);
        }

        // where the pointer is: a touch's own position when there is one (the mouse position Unity simulates from a touch can
        // lag a frame), else the mouse (QA, Nov 13 2026: a click with no mouse move before it reported (0, 0))
        public static Vector2 PointerPos => Input.touchCount > 0 ? Input.GetTouch(0).position : (Vector2)Input.mousePosition;
        void Signs(GameLoop g, Profile P)
        {
            var wz = Wizard.I; var cam = Camera.main; var canvas = (RectTransform)transform;
            for (int i = 0; i < signs.Length; i++)
            {
                var at = Field.SignPoint(i); bool show = wz && wz.On && cam;
                Vector3 sp = default; float dist = 0;
                if (show)
                {
                    dist = Vector3.Distance(cam.transform.position, at); sp = cam.WorldToScreenPoint(at);
                    show = sp.z > 0 && dist < 32f && sp.x > 0 && sp.x < Screen.width && sp.y > 0 && sp.y < Screen.height;
                }
                if (signs[i].gameObject.activeSelf != show) signs[i].gameObject.SetActive(show);
                if (!show) continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, sp, null, out var lp);
                signs[i].anchoredPosition = lp + canvas.rect.size * 0.5f;
                signs[i].localScale = Vector3.one * Mathf.Clamp(18f / dist, 0.85f, 1.1f);   // never below about 16 px text (fresh-eyes review, Nov 14 2026: signs unreadable)
                var id = HG.BUILDING_IDS[i]; var b = HG.ById(id);
                if (P.restored.Contains(id))
                {
                    double r = HG.BuildingRate(P, id, g.B); string rs = r < 10 ? r.ToString("0.##") : HG.Fmt(r);
                    signTxt[i].text = $"<b>{b.name}</b>  ·  level {HG.LevelOf(P, id)}\n<color=#FFD56B>+{rs}/s</color>";
                }
                else signTxt[i].text = HG.RestoreOffer(P).Contains(id) ? $"<b>{b.name}</b>  ·  ruined\n<color=#FFD56B>rebuild {HG.Fmt(g.RestoreCost)}</color>" : $"<b>{b.name}</b>  ·  ruined\n<color=#D9C9A8>{(g.NextBuilding != null ? "after the " + HG.ById(g.NextBuilding).name : "rebuilt later")}</color>";   // says what comes first, not a bare "not yet"
            }
        }

        // true when the pointer is over a pressable control (the town walk ignores those taps)
        public static bool OnHud(Vector2 m)
        {
            if (!I) return false;
            foreach (var p in I.press) if (p.rt && p.rt.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(p.rt, m, null)) return true;
            return I.forkBox != null || I.settingsBox != null;
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
        // the actions as one group in the top-right corner: a row along the top when the window is wide enough (the sky and
        // the far meadow), else a column down the right side (portrait: the sky above the town). Upgrade gate, the primary
        // action, sits in the corner; a finished or idle action leaves and the rest close up. LayoutCheck mirrors this.
        public const float GAP = 8f, EDGE = 16f, LABELS_W = 266f;
        void LayoutActions()
        {
            // Upgrade in the corner, Walk always right beside it (owner, Nov 14 2026: "make the walk town and back to the
            // walls button stay there"), then Rebuild and the work card, which come and go
            var list = new System.Collections.Generic.List<RectTransform> { up.p.rt, walk.p.rt };
            if (re.p.rt.gameObject.activeSelf) list.Add(re.p.rt);
            if (work.p.rt.gameObject.activeSelf) list.Add(work.p.rt);
            float width = ((RectTransform)transform).rect.width, row = list.Sum(r => r.sizeDelta.x) + GAP * (list.Count - 1);
            bool asRow = width - LABELS_W - EDGE >= row;
            float x = -EDGE, y = -EDGE;
            foreach (var r in list)
            {
                r.anchoredPosition = new Vector2(x, y);
                if (asRow) x -= r.sizeDelta.x + GAP; else y -= r.sizeDelta.y + GAP;
            }
            // the banner sits below the group (under the top row, or under the column, never beside it)
            var bp = banner.rectTransform.anchoredPosition; bp.y = asRow ? -136f : Mathf.Min(-136f, y - 12f); banner.rectTransform.anchoredPosition = bp;
        }

        // ---------- the action kit ----------
        class Act { public Pressable p; public Text title, cost; public Image icon, gem, glow; public RectTransform cap, costRow; public bool primary; }
        static readonly Color PLUM_DIM = new Color(0.2f, 0.14f, 0.22f, 0.94f), BRASS_HI = new Color(1f, 0.82f, 0.42f), SHORT = new Color(0.93f, 0.62f, 0.55f);
        Act Action(string n, Vector2 anchor, Vector2 pos, Vector2 size, string icon, string key, System.Action act, System.Func<bool> on, bool primary)
        {
            var a = new Act { primary = primary };
            var r = Box(n, transform, anchor, anchor, pos, size); r.pivot = anchor;
            if (primary)
            {   // the breathing gold glow behind the primary action
                var g = Box("Glow", r, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(14, 14));
                a.glow = g.gameObject.AddComponent<Image>(); a.glow.sprite = Panel(new Color(1f, 0.8f, 0.35f, 0.0f), new Color(1f, 0.8f, 0.35f, 0.55f), 18, 7);
                a.glow.type = Image.Type.Sliced; a.glow.raycastTarget = false;
            }
            var img = r.gameObject.AddComponent<Image>(); img.sprite = Panel(PLUM, primary ? BRASS_HI : BRASS, 14, primary ? 4 : 3); img.type = Image.Type.Sliced;
            float h = size.y, m = h - 12;
            var med = Box("Medal", r, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6 + m / 2, 0), new Vector2(m, m));
            var mi = med.gameObject.AddComponent<Image>(); mi.sprite = Panel(INK, BRASS, (int)(m / 2) - 1, 3); mi.type = Image.Type.Sliced; mi.raycastTarget = false;
            var ic = Box("Icon", med, Vector2.zero, Vector2.one, new Vector2(0, 1), new Vector2(2, 2));
            a.icon = ic.gameObject.AddComponent<Image>(); a.icon.sprite = Icon(icon); a.icon.preserveAspect = true; a.icon.raycastTarget = false;
            if (!a.icon.sprite) a.icon.enabled = false;
            float left = m + 14, w = size.x - left - 10;
            a.title = Label("Title", r, new Vector2(0, 0.5f), Vector2.zero, h >= 60 ? 22 : 20, TextAnchor.MiddleLeft);
            var tr = a.title.rectTransform; tr.pivot = new Vector2(0, 0.5f); tr.sizeDelta = new Vector2(w, h * 0.5f); tr.anchoredPosition = new Vector2(left, h * 0.18f);
            a.title.horizontalOverflow = HorizontalWrapMode.Overflow;
            a.costRow = Box("Cost", r, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(left, -h * 0.2f), new Vector2(w, h * 0.4f)); a.costRow.pivot = new Vector2(0, 0.5f);
            var gm = Box("Gem", a.costRow, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(17, 17));
            a.gem = gm.gameObject.AddComponent<Image>(); a.gem.sprite = gemSprite; a.gem.raycastTarget = false;
            a.cost = Label("C", a.costRow, new Vector2(0, 0.5f), new Vector2(20, 0), 17, TextAnchor.MiddleLeft); a.cost.font = body;
            a.cost.rectTransform.pivot = new Vector2(0, 0.5f); a.cost.rectTransform.sizeDelta = new Vector2(w - 20, h * 0.4f);
            // a line taller than its box draws nothing in uGUI (Truncate): the cost row was blank in the browser (Nov 14 2026)
            a.cost.verticalOverflow = a.title.verticalOverflow = VerticalWrapMode.Overflow;
            if (!Application.isMobilePlatform)
            {   // the key, as a keycap on the corner (desktop only)
                a.cap = Box("Key", r, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -4), new Vector2(24, 21)); a.cap.pivot = new Vector2(1, 1);
                var ci = a.cap.gameObject.AddComponent<Image>(); ci.sprite = Panel(INK, new Color(0.62f, 0.5f, 0.3f), 6, 2); ci.type = Image.Type.Sliced; ci.raycastTarget = false;
                var kt = Label("K", a.cap, new Vector2(0.5f, 0.5f), new Vector2(0, 1), 13, TextAnchor.MiddleCenter); kt.font = body; kt.text = key;
                kt.rectTransform.pivot = new Vector2(0.5f, 0.5f); kt.rectTransform.sizeDelta = new Vector2(24, 21); kt.color = new Color(0.93f, 0.86f, 0.72f);
                Destroy(kt.GetComponent<Shadow>());
            }
            a.p = Press(r, act, on);
            return a;
        }
        // one action's look for this frame: its words, its cost (or none), and whether it can be pressed
        void Show(Act a, string title, string cost, bool can, float t)
        {
            a.title.text = title;
            // the title never runs under the keycap or off the button (game-polish 5; QA Nov 14 2026: "Rebuild Frost Spi")
            float room = a.p.rt.sizeDelta.x - a.title.rectTransform.anchoredPosition.x - (a.cap ? 32 : 12);
            int fs = a.p.rt.sizeDelta.y >= 60 ? 22 : 20; a.title.fontSize = fs;
            while (fs > 13 && a.title.preferredWidth > room) a.title.fontSize = --fs;
            bool hasCost = !string.IsNullOrEmpty(cost); a.costRow.gameObject.SetActive(hasCost);
            var tr = a.title.rectTransform; float h = a.p.rt.sizeDelta.y;
            tr.anchoredPosition = new Vector2(tr.anchoredPosition.x, hasCost ? h * 0.18f : 0);
            if (hasCost) { a.cost.text = cost; a.cost.color = can ? RES : SHORT; }
            if (a.icon) a.icon.color = can || !hasCost ? Color.white : new Color(0.75f, 0.72f, 0.78f, 0.85f);
            if (a.glow) { var c = a.glow.color; c.a = can && hasCost ? 0.55f + 0.45f * Mathf.Sin(t * 3.2f) : 0f; a.glow.color = c; }
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
            shownRes = shownRes < 0 ? P.res : shownRes + (P.res - shownRes) * Mathf.Min(1f, Time.deltaTime * 9f);   // the number counts toward the truth (game-polish 14)
            if (System.Math.Abs(P.res - shownRes) < 1) shownRes = P.res;
            res.text = HG.Fmt(shownRes);
            double r = HG.CityRate(P, g.B); rate.text = r >= 0.05 ? $"+{(r < 10 ? r.ToString("0.#") : HG.Fmt(r))}/s from the city" : "";   // never "+0/s"
            double gm = HG.GateMax(P, g.B); float gf = Mathf.Clamp01((float)(g.gateHp / gm)); gateBar.fillAmount = gf;
            gateBar.color = gf > 0.5f ? Color.Lerp(new Color(1f, 0.78f, 0.25f), new Color(0.5f, 0.85f, 0.35f), (gf - 0.5f) * 2) : Color.Lerp(new Color(0.9f, 0.25f, 0.2f), new Color(1f, 0.78f, 0.25f), gf * 2); gateTxt.text = $"Walls {HG.Fmt(System.Math.Max(0, g.gateHp))} / {HG.Fmt(gm)}";
            float now = Time.unscaledTime;
            Show(up, "Upgrade towers", HG.Fmt(g.UpgradeCost), g.CanUpgrade, now);
            var nb = g.NextBuilding; if (re.p.rt.gameObject.activeSelf != (nb != null)) re.p.rt.gameObject.SetActive(nb != null);
            if (nb != null) Show(re, $"Rebuild {HG.ById(nb).name}", HG.Fmt(g.RestoreCost), g.CanRestore, now);
            Signs(g, P); TownTags(g, P);
            // the town walk
            var wz = Wizard.I;
            Show(walk, wz && wz.On ? "Back to the walls" : "Walk the town", null, true, now);
            bool showWork = wz && wz.On && wz.Near >= 0; if (workCard.gameObject.activeSelf != showWork) workCard.gameObject.SetActive(showWork);
            if (showWork)
            {
                var id = HG.BUILDING_IDS[wz.Near]; var b = HG.ById(id);
                bool can = g.CanWork(wz.Near);
                if (P.restored.Contains(id))
                {
                    int L = HG.LevelOf(P, id); double cur = HG.BuildingRate(P, id, g.B), next = HG.BuildingRate(P, id, g.B, L + 1); double gain = next - cur;
                    work.icon.sprite = Icon("upgrade") ?? work.icon.sprite;
                    Show(work, $"{b.name}: level {L + 1}", $"{HG.Fmt(g.LevelCost(wz.Near))}     <color=#CFE9B0>+{(gain < 10 ? gain.ToString("0.##") : HG.Fmt(gain))}/s</color>", can, now);
                }
                else if (HG.RestoreOffer(P).Contains(id)) { work.icon.sprite = Icon("rebuild") ?? work.icon.sprite; Show(work, $"Rebuild the {b.name}", HG.Fmt(g.RestoreCost), can, now); }
                else { work.icon.sprite = Icon("rebuild") ?? work.icon.sprite; Show(work, $"The {b.name} lies in ruins", null, false, now); }
            }
            LayoutActions();
            Refit();
            if (Input.GetKeyDown(KeyCode.P)) ShowSettings(settingsBox == null);
            if (Input.GetKeyDown(KeyCode.M)) { bool on = !(Sound.MusicOn || Sound.SfxOn); Sound.SetMusic(on); Sound.SetSfx(on); Banner(on ? "Sound on" : "Sound off"); RefreshSettings(); }
            if (bannerT > 0) { bannerT -= Time.deltaTime; var c = banner.color; c.a = Mathf.Clamp01(bannerT / 0.4f); banner.color = c; }
        }

        public static void Banner(string s)
        {
            if (!I) return; var b = I.banner; b.text = s; I.bannerT = 2.6f; var c = b.color; c.a = 1; b.color = c;
            float w = Mathf.Min(900f, ((RectTransform)I.transform).rect.width - 48f); b.rectTransform.sizeDelta = new Vector2(w, 90);
            int size = 34; b.fontSize = size;
            while (size > 18 && b.preferredHeight > 84) b.fontSize = --size;   // two lines at most, then smaller
        }
        public static void Toast(string s) => Banner(s);

        // ---------- settings and controls ----------
        RectTransform settingsBox; Text musicLbl, sfxLbl;
        public static bool Paused => I && I.settingsBox != null;
        void ShowSettings(bool open)
        {
            if (!open) { if (settingsBox) { Destroy(settingsBox.gameObject); settingsBox = null; } Time.timeScale = 1; return; }
            if (settingsBox) return;
            Time.timeScale = 0;   // the fight waits while the menu is open
            var dim = Box("SettingsDim", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var di = dim.gameObject.AddComponent<Image>(); di.color = new Color(0.08f, 0.04f, 0.1f, 0.55f); settingsBox = dim;
            var box = Box("Panel", dim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 550));   // tall enough for every control row above the stamp
            var bi = box.gameObject.AddComponent<Image>(); bi.sprite = Panel(INK, BRASS, 18, 4); bi.type = Image.Type.Sliced;
            Label("T", box, new Vector2(0.5f, 1), new Vector2(0, -18), 32, TextAnchor.UpperCenter).text = "Settings";
            BtnAt("S_Music", new Vector2(0.5f, 1), new Vector2(-140, -76), new Vector2(250, 50), () => { Sound.SetMusic(!Sound.MusicOn); RefreshSettings(); }, null, out musicLbl).rt.SetParent(box, false);
            BtnAt("S_Sfx", new Vector2(0.5f, 1), new Vector2(140, -76), new Vector2(250, 50), () => { Sound.SetSfx(!Sound.SfxOn); RefreshSettings(); }, null, out sfxLbl).rt.SetParent(box, false);
            var ct = Label("Controls", box, new Vector2(0.5f, 1), new Vector2(0, -148), 22, TextAnchor.UpperCenter); ct.text = "Controls";
            string[,] rows = {
                { "U", "Upgrade the towers" }, { "R", "Rebuild the next building" }, { "T", "Walk the town / back to the walls" },
                { "WASD, arrows", "Walk (ZQSD on AZERTY)" }, { "Tap or click", "Walk to that spot, or press a button" }, { "E", "Work on the building you stand in" },
                { "X", "Start a Surge at the fountain" }, { "M", "Sound on or off" }, { "P", "This menu" } };
            for (int k = 0; k < rows.GetLength(0); k++)
            {
                float y = -186 - k * 26;
                var key = Label("K" + k, box, new Vector2(0.5f, 1), new Vector2(-30, y), 17, TextAnchor.UpperRight); key.font = body; key.color = RES; key.text = rows[k, 0];
                key.rectTransform.pivot = new Vector2(1, 1); key.rectTransform.sizeDelta = new Vector2(200, 24);
                var what = Label("W" + k, box, new Vector2(0.5f, 1), new Vector2(-10, y), 17, TextAnchor.UpperLeft); what.font = body; what.text = rows[k, 1];
                what.rectTransform.pivot = new Vector2(0, 1); what.rectTransform.sizeDelta = new Vector2(300, 24);
            }
            var stamp = Label("Stamp", box, new Vector2(0.5f, 0), new Vector2(0, 74), 13, TextAnchor.LowerCenter); stamp.font = body; stamp.color = new Color(0.8f, 0.74f, 0.82f);
            stamp.text = "Hearthgate 3D · build " + Application.version;
            var close = BtnAt("S_Close", new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(200, 48), () => ShowSettings(false), null, out var cl);
            close.rt.SetParent(box, false); cl.text = "Back to the game";
            RefreshSettings();
        }
        void RefreshSettings()
        {
            if (!settingsBox) return;
            musicLbl.text = "Music: " + (Sound.MusicOn ? "on" : "off"); sfxLbl.text = "Sound effects: " + (Sound.SfxOn ? "on" : "off");
        }

        // a gate stage asks for one of two upgrades
        public static void ShowFork(int stage)
        {
            if (!I || I.forkBox) return;
            var box = I.Box("Fork", I.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 260));
            var bi = box.gameObject.AddComponent<Image>(); bi.sprite = Panel(INK, BRASS, 18, 4); bi.type = Image.Type.Sliced; I.forkBox = box;
            I.Label("T", box, new Vector2(0.5f, 1), new Vector2(0, -18), 28, TextAnchor.UpperCenter).text = "The towers grow. Choose one:";
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
