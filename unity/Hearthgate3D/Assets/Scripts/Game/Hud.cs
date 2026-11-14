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
    public partial class Hud : MonoBehaviour
    {
        static Hud I;
        Font font, body; Text wave, res, rate, evTxt, banner, gateTxt; Image gateBar; RectTransform forkBox;
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

            var shade = Box("Shade", transform, new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(380, 150)); shade.pivot = new Vector2(0, 1);
            var si = shade.gameObject.AddComponent<Image>(); si.sprite = Fade(); si.color = new Color(0.16f, 0.08f, 0.19f, 0.55f); si.raycastTarget = false;
            wave = Label("Wave", transform, new Vector2(0, 1), new Vector2(22, -12), 28, TextAnchor.UpperLeft);
            var gem = Box("Gem", transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -52), new Vector2(34, 34)); gem.pivot = new Vector2(0, 1);
            gemSprite = Gem(); var gi = gem.gameObject.AddComponent<Image>(); gi.sprite = gemSprite; gi.raycastTarget = false;
            res = Label("Res", transform, new Vector2(0, 1), new Vector2(60, -46), 40, TextAnchor.UpperLeft); res.color = RES;
            rate = Label("Rate", transform, new Vector2(0, 1), new Vector2(24, -94), 18, TextAnchor.UpperLeft); rate.font = body;
            evTxt = Label("Event", transform, new Vector2(0, 1), new Vector2(24, -116), 18, TextAnchor.UpperLeft); evTxt.color = new Color(1f, 0.72f, 0.9f);   // the named wave
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
            // Rebuild the next building; once the city is whole the same button is Ascend (the 2D game's rule)
            re = Action("Rebuild", tr, new Vector2(-16, -78), new Vector2(230, 56), "rebuild", "R",
                () => { if (GameLoop.I.NextBuilding != null) GameLoop.I.Restore(); else ShowAscend(); },
                () => GameLoop.I.NextBuilding != null ? GameLoop.I.CanRestore : GameLoop.I.CanAscend, false);

            // the town walk and, while walking, the work on the building in reach: in the same group as the other actions
            // (owner, Nov 14 2026: "the walk the town button should be with the other buttons. No reason to separate
            // actions"); LayoutActions places the group every frame
            walk = Action("Walk", tr, new Vector2(-16, -140), new Vector2(230, 56), "walk", "T", () => Wizard.I?.Toggle(), null, false);
            work = Action("Work", tr, new Vector2(-16, -202), new Vector2(280, 56), "upgrade", "E", () => { var w = Wizard.I; if (w && w.Near >= 0) GameLoop.I.WorkOn(w.Near); },
                () => Wizard.I && Wizard.I.Near >= 0 && GameLoop.I.CanWork(Wizard.I.Near), true);
            workCard = work.p.rt; workCard.gameObject.SetActive(false);
            // Magister Bram's card, bottom left, his portrait in the medallion (the wizard); a tap skips the line
            guide = Action("Guide", new Vector2(0, 0), new Vector2(16, 70), new Vector2(430, 72), "walk", null, () => Guide.Skip(), null, false);
            guide.title.fontSize = 22; guide.p.rt.gameObject.SetActive(false);

            // settings (game-polish rule 19: the controls inside the game, the music and sound switches): a small brass
            // medallion with a cog rendered in Blender, inside the top-left label block, right of the wave
            var gear = Action("Settings", new Vector2(0, 1), new Vector2(196, -12), new Vector2(56, 56), "gear", null, () => ShowSettings(true), null, false);

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
            surgeAct = Action("SurgeTag", Vector2.zero, Vector2.zero, new Vector2(230, 56), "fountain", "X", () => GameLoop.I.TapSurge(), null, true);
            surgeTag = surgeAct.p.rt; surgeTag.pivot = new Vector2(0.5f, 0); surgeTag.SetSiblingIndex(1); surgeTag.gameObject.SetActive(false);
            // the Tavern's Cheer meter, over its roof
            cheerTag = Box("CheerTag", transform, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(120, 30)); cheerTag.pivot = new Vector2(0.5f, 0);
            var cbg = cheerTag.gameObject.AddComponent<Image>(); cbg.sprite = Panel(INK, BRASS, 8, 2); cbg.type = Image.Type.Sliced; cbg.raycastTarget = false;
            var cf = Box("Fill", cheerTag, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-8, -8));
            cheerFill = cf.gameObject.AddComponent<Image>(); cheerFill.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), Vector2.zero);
            cheerFill.type = Image.Type.Filled; cheerFill.fillMethod = Image.FillMethod.Horizontal; cheerFill.raycastTarget = false;
            var ct = Label("T", cheerTag, new Vector2(0.5f, 0.5f), new Vector2(0, 1), 15, TextAnchor.MiddleCenter); ct.text = "Cheer"; ct.font = body;
            ct.rectTransform.pivot = new Vector2(0.5f, 0.5f); ct.rectTransform.sizeDelta = new Vector2(110, 26);
            cheerTag.SetSiblingIndex(1); cheerTag.gameObject.SetActive(false);
            perksAct = Action("PerksTag", Vector2.zero, Vector2.zero, new Vector2(230, 56), "perks", null, () => ShowPerks(), null, false);
            perksTag = perksAct.p.rt; perksTag.pivot = new Vector2(0.5f, 0); perksTag.SetSiblingIndex(1); perksTag.gameObject.SetActive(false);
            InitHalls();
        }
        RectTransform perksTag; Act surgeAct, perksAct;
        RectTransform surgeTag, cheerTag; Image cheerFill; Act guide;
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
            bool tagOn = g.SurgeOpen && (g.SurgeOn || g.SurgeReady);
            if (tagOn && Pin(surgeTag, Field.PLAZA + new Vector3(0, 4.2f, 0)))
            { Show(surgeAct, g.SurgeOn ? "Surge x2" : "Surge ready", g.SurgeOn ? $"{GameLoop.Mmss(g.SurgeLeftMs)} left" : "x2 for 3 min", !g.SurgeOn, Time.unscaledTime); surgeAct.gem.enabled = false; }
            else if (!tagOn && surgeTag.gameObject.activeSelf) surgeTag.gameObject.SetActive(false);
            int ti = System.Array.IndexOf(HG.BUILDING_IDS, "tavern");
            if (P.restored.Contains("tavern") && Pin(cheerTag, Field.Plot(ti) + Vector3.up * (Field.PlotHeight(ti) + 0.8f)))
            {
                cheerFill.fillAmount = g.CheerShown;
                cheerFill.color = g.cheerT > 0 ? new Color(1f, 0.84f, 0.42f) : new Color(0.9f, 0.6f, 0.23f);
            }
            else if (!P.restored.Contains("tavern") && cheerTag.gameObject.activeSelf) cheerTag.gameObject.SetActive(false);
            // the Watchtower keeps the perks: a board over it once there is anything to spend (it works even in ruins)
            int wi = System.Array.IndexOf(HG.BUILDING_IDS, "watchtower");
            bool perksOn = P.ascensions > 0;
            if (perksOn && Pin(perksTag, Field.Plot(wi) + Vector3.up * (Field.PlotHeight(wi) + 1.2f)))
            { Show(perksAct, "Perks", P.perkPts > 0 ? $"{P.perkPts} to spend" : "all spent", P.perkPts > 0, Time.unscaledTime); perksAct.gem.enabled = false; }
            else if (!perksOn && perksTag.gameObject.activeSelf) perksTag.gameObject.SetActive(false);
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
                    signTxt[i].text = $"<b>{HG.NameOf(P, id)}</b>  ·  level {HG.LevelOf(P, id)}\n<color=#FFD56B>+{rs}/s</color>";
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
        // EVERY pressable thing in the game is made here (owner, Nov 14 2026: "the buttons always have to look the same"):
        // the plum panel and brass rim, the display font, the icon medallion when there is an icon, the keycap on desktop,
        // the gold rim on the one primary action, the same press and hover. Menus, cards, the perks and the boards over the
        // fountain and the Watchtower included.
        Act Action(string n, Vector2 anchor, Vector2 pos, Vector2 size, string icon, string key, System.Action act, System.Func<bool> on, bool primary, Transform parent = null)
        {
            var a = new Act { primary = primary };
            var r = Box(n, parent ?? transform, anchor, anchor, pos, size); r.pivot = anchor;
            {   // the breathing gold glow: behind the primary action, or the one the Magister points at
                var g = Box("Glow", r, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(14, 14));
                a.glow = g.gameObject.AddComponent<Image>(); a.glow.sprite = Panel(new Color(1f, 0.8f, 0.35f, 0.0f), new Color(1f, 0.8f, 0.35f, 0.55f), 18, 7);
                a.glow.type = Image.Type.Sliced; a.glow.raycastTarget = false; a.glow.color = new Color(1, 1, 1, primary ? 1 : 0);   // dark until lit
            }
            var img = r.gameObject.AddComponent<Image>(); img.sprite = Panel(PLUM, primary ? BRASS_HI : BRASS, 14, primary ? 4 : 3); img.type = Image.Type.Sliced;
            float h = size.y, m = h - 12;
            bool hasIcon = icon != null && size.x > m + 24;
            var med = Box("Medal", r, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(6 + m / 2, 0), new Vector2(m, m));
            if (icon != null && size.x <= m + 24) { med.anchorMin = med.anchorMax = med.pivot = new Vector2(0.5f, 0.5f); med.anchoredPosition = Vector2.zero; }   // an icon-only button: the medallion centred
            var mi = med.gameObject.AddComponent<Image>(); mi.sprite = Panel(INK, BRASS, (int)(m / 2) - 1, 3); mi.type = Image.Type.Sliced; mi.raycastTarget = false;
            var ic = Box("Icon", med, Vector2.zero, Vector2.one, new Vector2(0, 1), new Vector2(2, 2));
            a.icon = ic.gameObject.AddComponent<Image>(); a.icon.sprite = Icon(icon); a.icon.preserveAspect = true; a.icon.raycastTarget = false;
            if (!a.icon.sprite) a.icon.enabled = false;
            if (icon == null) med.gameObject.SetActive(false);
            float left = hasIcon ? m + 14 : 12, w = size.x - left - (key != null && !Application.isMobilePlatform ? 30 : 12);
            a.title = Label("Title", r, new Vector2(0, 0.5f), Vector2.zero, h >= 60 ? 22 : 20, TextAnchor.MiddleLeft);
            var tr = a.title.rectTransform; tr.pivot = new Vector2(0, 0.5f); tr.sizeDelta = new Vector2(w, h * 0.5f); tr.anchoredPosition = new Vector2(left, h * 0.18f);
            a.title.horizontalOverflow = HorizontalWrapMode.Overflow;
            if (!hasIcon) { a.title.alignment = TextAnchor.MiddleCenter; tr.sizeDelta = new Vector2(w, h * 0.5f); }
            if (icon != null && !hasIcon) a.title.gameObject.SetActive(false);
            a.costRow = Box("Cost", r, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(left, -h * 0.2f), new Vector2(w, h * 0.4f)); a.costRow.pivot = new Vector2(0, 0.5f);
            var gm = Box("Gem", a.costRow, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(17, 17));
            a.gem = gm.gameObject.AddComponent<Image>(); a.gem.sprite = gemSprite; a.gem.raycastTarget = false;
            a.cost = Label("C", a.costRow, new Vector2(0, 0.5f), new Vector2(20, 0), 17, TextAnchor.MiddleLeft); a.cost.font = body;
            a.cost.rectTransform.pivot = new Vector2(0, 0.5f); a.cost.rectTransform.sizeDelta = new Vector2(w - 20, h * 0.4f);
            // a line taller than its box draws nothing in uGUI (Truncate): the cost row was blank in the browser (Nov 14 2026)
            a.cost.verticalOverflow = a.title.verticalOverflow = VerticalWrapMode.Overflow;
            if (!hasIcon) { a.cost.alignment = TextAnchor.MiddleCenter; a.costRow.anchorMin = a.costRow.anchorMax = new Vector2(0.5f, 0.5f); a.costRow.pivot = new Vector2(0.5f, 0.5f); a.costRow.anchoredPosition = new Vector2(0, -h * 0.2f); a.cost.rectTransform.anchorMin = a.cost.rectTransform.anchorMax = a.cost.rectTransform.pivot = new Vector2(0.5f, 0.5f); a.cost.rectTransform.anchoredPosition = Vector2.zero; }
            a.costRow.gameObject.SetActive(false);
            if (key != null && !Application.isMobilePlatform)
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
            if (a.glow) { var c = a.glow.color; c.a = a.primary && can && hasCost ? 0.55f + 0.45f * Mathf.Sin(t * 3.2f) : 0f; a.glow.color = c; }
        }
        Pressable BtnAt(string n, Vector2 anchor, Vector2 pos, Vector2 size, System.Action act, System.Func<bool> on, out Text lbl, Transform parent = null, string icon = null, bool primary = false)
        {
            var a = Action(n, anchor, pos, size, icon, null, act, on, primary, parent);
            a.title.rectTransform.anchoredPosition = new Vector2(a.title.rectTransform.anchoredPosition.x, 0); lbl = a.title;
            Fit(lbl, new Vector2(a.title.rectTransform.sizeDelta.x, size.y - 10), 13);
            acts.Add(a); return a.p;
        }
        readonly System.Collections.Generic.List<Act> acts = new System.Collections.Generic.List<Act>();   // every kit button, for the primary rim's breath

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
            evTxt.text = g.ev != null ? g.ev.name : "";
            float now = Time.unscaledTime;
            Show(up, "Upgrade towers", HG.Fmt(g.UpgradeCost), g.CanUpgrade, now);
            var nb = g.NextBuilding;
            if (nb != null) { re.icon.sprite = Icon("rebuild") ?? re.icon.sprite; Show(re, $"Rebuild {HG.NameOf(g.P, nb)}", HG.Fmt(g.RestoreCost), g.CanRestore, now); }
            else
            {   // the city is whole: Ascend, once the run is deep enough
                re.icon.sprite = Icon("ascend") ?? re.icon.sprite; int need = HG.AscendWave(P), got = System.Math.Max(P.runWave, P.wave);
                Show(re, "Ascend", g.CanAscend ? "x1.5 for good" : $"wave {got} / {need}", g.CanAscend, now);
                re.gem.enabled = false;
            }
            if (nb != null) re.gem.enabled = true;
            Signs(g, P); TownTags(g, P); HallTags(g, P); perkRefresh?.Invoke();
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
            foreach (var a in acts) if (a.glow && a.p.rt) { var c = a.glow.color; c.a = a.primary ? 0.4f + 0.35f * Mathf.Sin(Time.unscaledTime * 3.2f) : 0f; a.glow.color = c; }
            // the Magister: his line on the card, and the button he names lit up bright
            Guide.Tick(Time.unscaledDeltaTime, txt => { guide.title.text = txt; guide.p.rt.gameObject.SetActive(true); }, () => guide.p.rt.gameObject.SetActive(false));
            var pt = Guide.Pressing == "up" ? up : Guide.Pressing == "re" ? re : Guide.Pressing == "walk" ? walk : null;
            if (pt != null && pt.glow) { var c = pt.glow.color; c.a = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 7f); pt.glow.color = c; }
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
        // a modal card: dims the game, pauses the fight, takes every press until closed (settings, Ascend, perks)
        RectTransform Modal(string title, Vector2 size)
        {
            if (settingsBox) ShowSettings(false);
            Time.timeScale = 0;
            var dim = Box("ModalDim", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var di = dim.gameObject.AddComponent<Image>(); di.color = new Color(0.08f, 0.04f, 0.1f, 0.55f); settingsBox = dim;
            var box = Box("Panel", dim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var bi = box.gameObject.AddComponent<Image>(); bi.sprite = Panel(INK, BRASS, 18, 4); bi.type = Image.Type.Sliced;
            Label("T", box, new Vector2(0.5f, 1), new Vector2(0, -18), 32, TextAnchor.UpperCenter).text = title;
            var cv = ((RectTransform)transform).rect.size;   // a tall card shrinks to fit a short screen, never runs off it
            box.localScale = Vector3.one * Mathf.Min(1f, (cv.y - 24f) / size.y, (cv.x - 24f) / size.x);
            return box;
        }
        Text Line(RectTransform box, float y, string text, int size = 18, Color? col = null)
        {
            var t = Label("L", box, new Vector2(0.5f, 1), new Vector2(0, y), size, TextAnchor.UpperCenter); t.font = body; t.text = text; t.supportRichText = true;
            if (col.HasValue) t.color = col.Value;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.rectTransform.sizeDelta = new Vector2(box.sizeDelta.x - 60, size * 3); return t;
        }
        Pressable Choice(RectTransform box, string n, Vector2 pos, Vector2 size, string text, System.Action act, System.Func<bool> on = null, bool primary = false)
        {
            var p = BtnAt(n, new Vector2(0.5f, 0), pos, size, act, on, out var l, box, null, primary); l.text = text;
            return p;
        }

        // Ascend: the city goes back to ruins, so the card says plainly what is lost and what is kept (it cannot be undone)
        public static void ShowAscend()
        {
            if (!I || !GameLoop.I || !GameLoop.I.CanAscend) return;
            var g = GameLoop.I; var P = g.P; int n = P.ascensions + 1;
            var box = I.Modal($"Ascend to Ascension {n}?", new Vector2(620, 380));
            I.Line(box, -74, "The city returns to ruins and the waves start again from wave 1.", 19);
            I.Line(box, -128, $"<color=#FFD56B>For good:</color> towers and Resonance x{System.Math.Pow(HG.ASCEND_MULT, n):0.##} (now x{g.B.ascend:0.##})", 19);
            I.Line(box, -168, $"<color=#FFD56B>+{HG.PerkPtsFor(P)} perk points</color> to spend at the Watchtower", 19);
            I.Line(box, -208, "Kept: your best wave, the combos you found, perks and settings.", 17, new Color(0.85f, 0.8f, 0.88f));
            I.Choice(box, "A_No", new Vector2(-130, 26), new Vector2(230, 56), "Not yet", () => I.ShowSettings(false));
            I.Choice(box, "A_Yes", new Vector2(130, 26), new Vector2(230, 56), "Ascend", () => { I.ShowSettings(false); g.Ascend(); }, null, true);
        }

        // the Watchtower's perks (2D: "Home of the Perks"): one row each, its level, its cost in points, Buy
        public static void ShowPerks(string tab = "perks")
        {
            if (!I || !GameLoop.I) return;
            var g = GameLoop.I; var P = g.P;
            var box = I.Modal("The Watchtower", new Vector2(700, 236 + HG.PERKS.Length * 64));   // room for the close button under the last row
            var tabs = new (string id, string label)[] { ("perks", "Perks"), ("stars", $"Stars {P.stars.Count}/{HG.STARS.Length}") };
            for (int k = 0; k < tabs.Length; k++)
            {
                var t = tabs[k];
                I.BtnAt("Tab" + k, new Vector2(0.5f, 1), new Vector2(-115 + k * 230, -70), new Vector2(220, 56), () => ShowPerks(t.id), () => t.id != tab, out var tl, box).rt.pivot = new Vector2(0.5f, 1);
                tl.text = t.label;
            }
            I.perkRefresh = null;
            if (tab == "stars") StarsTab(box, P); else PerksTab(box, g);
            I.Choice(box, "P_Close", new Vector2(0, 18), new Vector2(230, 56), "Back to the game", () => I.ShowSettings(false));
        }
        static void PerksTab(RectTransform box, GameLoop g)
        {
            var pts = I.Line(box, -136, "", 18, RES);
            var rows = new System.Collections.Generic.List<(HG.Perk k, Text lv, Text cost)>();
            for (int i = 0; i < HG.PERKS.Length; i++)
            {
                var k = HG.PERKS[i]; float y = -172 - i * 64;
                var name = I.Label("N" + i, box, new Vector2(0, 1), new Vector2(32, y), 19, TextAnchor.UpperLeft); name.text = k.name;
                name.rectTransform.pivot = new Vector2(0, 1); name.rectTransform.sizeDelta = new Vector2(360, 24);
                var line = I.Label("D" + i, box, new Vector2(0, 1), new Vector2(32, y - 24), 15, TextAnchor.UpperLeft); line.font = I.body; line.text = k.line; line.color = new Color(0.85f, 0.8f, 0.88f);
                line.rectTransform.pivot = new Vector2(0, 1); line.rectTransform.sizeDelta = new Vector2(400, 22);
                var lv = I.Label("V" + i, box, new Vector2(1, 1), new Vector2(-170, y - 8), 17, TextAnchor.UpperRight); lv.font = I.body; lv.rectTransform.pivot = new Vector2(1, 1); lv.rectTransform.sizeDelta = new Vector2(90, 24);
                var id = k.id;
                I.BtnAt("P_" + id, new Vector2(1, 1), new Vector2(-24, y + 2), new Vector2(160, 56), () => { g.BuyPerk(id); }, () => HG.CanBuyPerk(g.P, id), out var cost, box);
                rows.Add((k, lv, cost));
            }
            I.perkRefresh = () =>
            {
                pts.text = $"{g.P.perkPts} perk point{(g.P.perkPts == 1 ? "" : "s")}  ·  each Ascension pays more";
                foreach (var (k, lv, cost) in rows)
                {
                    int L = g.P.PerkAt(k.id); bool max = L >= k.max;
                    lv.text = k.max == int.MaxValue ? $"level {L}" : $"{L} / {k.max}";
                    cost.text = max ? "Maxed" : $"Buy · {HG.PerkCost(g.P, k.id)}";
                }
            };
            I.perkRefresh();
        }
        // the constellation of this run: five stars lit at fixed waves on the way to the Ascend wave; earlier runs' skies whole
        static void StarsTab(RectTransform box, Profile P)
        {
            int run = Mathf.Clamp(P.ascensions, 1, HG.CONSTELLATIONS.Length);
            if (P.ascensions == 0) { I.Line(box, -150, "Each city after an Ascension has its own constellation of five stars. They light as the waves climb, and each one is a lasting boon.", 18); return; }
            string c = HG.CONSTELLATIONS[run - 1];
            I.Line(box, -136, P.ascensions > HG.CONSTELLATIONS.Length ? "Every constellation is lit" : $"This city's sky: the {c}", 18, RES);
            var mine = HG.STARS.Where(x => x.asc == run).ToArray();
            for (int i = 0; i < mine.Length; i++)
            {
                var st = mine[i]; bool lit = P.stars.Contains(st.id);
                I.Row(box, i, st.name, lit ? RES : new Color(0.6f, 0.55f, 0.62f), st.line, lit ? "Lit" : $"wave {HG.StarWave(P, st.k)}", lit ? RES : CREAM);
            }
            var done = HG.CONSTELLATIONS.Take(run - 1).ToArray();
            if (done.Length > 0) I.Line(box, -176 - 5 * 64 - 6, $"Whole: {string.Join(", ", done)}", 16, new Color(0.85f, 0.8f, 0.88f));
        }
        System.Action perkRefresh;

        void ShowSettings(bool open)
        {
            if (!open) { if (settingsBox) { Destroy(settingsBox.gameObject); settingsBox = null; } perkRefresh = null; Time.timeScale = 1; return; }
            if (settingsBox) return;
            Time.timeScale = 0;   // the fight waits while the menu is open
            var dim = Box("SettingsDim", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var di = dim.gameObject.AddComponent<Image>(); di.color = new Color(0.08f, 0.04f, 0.1f, 0.55f); settingsBox = dim;
            var box = Box("Panel", dim, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(600, 550));   // tall enough for every control row above the stamp
            var bi = box.gameObject.AddComponent<Image>(); bi.sprite = Panel(INK, BRASS, 18, 4); bi.type = Image.Type.Sliced;
            Label("T", box, new Vector2(0.5f, 1), new Vector2(0, -18), 32, TextAnchor.UpperCenter).text = "Settings";
            BtnAt("S_Music", new Vector2(0.5f, 1), new Vector2(-140, -76), new Vector2(250, 56), () => { Sound.SetMusic(!Sound.MusicOn); RefreshSettings(); }, null, out musicLbl, box);
            BtnAt("S_Sfx", new Vector2(0.5f, 1), new Vector2(140, -76), new Vector2(250, 56), () => { Sound.SetSfx(!Sound.SfxOn); RefreshSettings(); }, null, out sfxLbl, box);
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
            var close = BtnAt("S_Close", new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(230, 56), () => ShowSettings(false), null, out var cl, box);
            cl.text = "Back to the game";
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
                var a = I.Action(f.id, new Vector2(0.5f, 0), new Vector2(k == 0 ? -152 : 152, 36), new Vector2(290, 64), "tower", null, () => { GameLoop.I.Fork(stage, f.id); Destroy(box.gameObject); }, null, false, box);
                I.Show(a, f.name, f.line, true, 0); a.gem.enabled = false; a.cost.color = Field.GOLD; Fit(a.cost, new Vector2(a.cost.rectTransform.sizeDelta.x, 26), 11);
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
