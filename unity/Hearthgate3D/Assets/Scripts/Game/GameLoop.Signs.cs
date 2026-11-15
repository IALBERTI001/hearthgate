// The keepers' shop signs (owner, Nov 15 2026: "the buildings that you interact with NPCs for things should have a sign on
// the Building that says things like Upgrade Perks! Skins and More! for market! etc etc... Give some life and directions for
// the building. Right now they feel like just placeholders"). A walnut board on iron brackets (hg_front.shop_sign) hangs on
// each keeper's building once it is rebuilt, saying in a few words what is inside; it swings a little in the breeze.
using UnityEngine;
using UnityEngine.UI;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class GameLoop
    {
        // the building, its words, where on its front the brackets meet the wall (across from the door's centre, and height):
        // over the door where the wall has room; beside it on the Library, whose rose window fills the space above
        static readonly (string id, string words, float u, float y, float size)[] SHOP_SIGNS =
        {
            ("market", "Skins & More!", 0f, 4.15f, 1.3f),   // the open hall is wide: a bigger board
            ("watchtower", "Upgrade Perks!", 0f, 3.7f, 1f),   // the owner's words
            ("library", "Quests & Feats!", -1.65f, 2.7f, 0.85f),   // between the door and the window
        };
        const float SIGN_OUT = 0.42f, SIGN_DROP = 0.59f;   // the board's face from the wall, its centre under the brackets (hg_front)
        readonly Transform[] shopSigns = new Transform[3], shopBoards = new Transform[3]; readonly Quaternion[] boardRest = new Quaternion[3];

        void TickShopSigns()
        {
            if (!art || !art.shopSign) return;
            for (int k = 0; k < SHOP_SIGNS.Length; k++)
            {
                var s = SHOP_SIGNS[k]; int i = System.Array.IndexOf(HG.BUILDING_IDS, s.id); if (i < 0) continue;
                bool want = P.restored.Contains(s.id);
                if (want && !shopSigns[k]) shopSigns[k] = MakeSign(k, i, s.words, s.u, s.y, s.size);
                if (!shopSigns[k]) continue;
                if (shopSigns[k].gameObject.activeSelf != want) shopSigns[k].gameObject.SetActive(want);
                if (!want || !shopBoards[k]) continue;
                // the breeze: a slow swing about the wall's line, now and then a gust
                var r = Field.Right(i); float t = Time.time + k * 2.3f;
                float a = 2.2f * Mathf.Sin(t * 1.15f) + 1.6f * Mathf.Max(0, Mathf.Sin(t * 0.23f)) * Mathf.Sin(t * 3.1f);
                shopBoards[k].rotation = Quaternion.AngleAxis(a, new Vector3(r.x, 0, r.y)) * boardRest[k];
            }
        }

        Transform MakeSign(int k, int i, string words, float u, float y, float size)
        {
            var f = Field.Front(i); var front = new Vector3(f.x, 0, f.y);
            var mount = Field.World(i, u, Field.PlotSize(i).y / 2f + 0.02f, y);
            var g = Instantiate(art.shopSign, world); g.name = "shop sign " + HG.BUILDING_IDS[i];
            g.transform.localScale = art.shopSign.transform.localScale * size; g.transform.position = mount; g.transform.rotation = Quaternion.LookRotation(-front) * art.shopSign.transform.rotation;   // models face -Z: its face to the street
            foreach (var c in g.GetComponentsInChildren<Transform>()) if (c.name == "board") shopBoards[k] = c;
            if (shopBoards[k]) boardRest[k] = shopBoards[k].rotation;
            // the words, painted on the board's face: cream letters with a dark edge, fitted to the board
            var go = new GameObject("words", typeof(RectTransform)); go.transform.SetParent(world, false);
            var cv = go.AddComponent<Canvas>(); cv.renderMode = RenderMode.WorldSpace; cv.sortingOrder = 1;
            var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(370, 104);
            rt.position = mount + front * ((SIGN_OUT + 0.06f) * size) + Vector3.down * (SIGN_DROP * size);
            rt.rotation = Quaternion.LookRotation(-front); rt.localScale = Vector3.one * 0.0052f * size;
            var tx = new GameObject("text", typeof(RectTransform)).AddComponent<Text>(); tx.transform.SetParent(rt, false);
            var tr = tx.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            tx.font = Resources.Load<Font>("Fonts/LilitaOne-Regular"); tx.alignment = TextAnchor.MiddleCenter; tx.text = words;
            tx.resizeTextForBestFit = true; tx.resizeTextMinSize = 28; tx.resizeTextMaxSize = 66; tx.fontSize = 66;
            tx.color = new Color(1f, 0.93f, 0.74f); tx.raycastTarget = false;
            var ol = tx.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0.12f, 0.06f, 0.03f, 0.95f); ol.effectDistance = new Vector2(2.5f, -2.5f);
            if (shopBoards[k]) rt.SetParent(shopBoards[k], true); else rt.SetParent(g.transform, true);   // the words swing with the board
            return g.transform;
        }
    }
}
