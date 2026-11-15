// The buildings' boards, painted on their signposts (owner, Nov 15 2026: "the giant purple card on the buildings are not
// helping our game, the information i feel can be displayed on a building differently to make it feel more like its part of
// the game"). The signpost in each yard (hg_city.signpost) carries the building's emblem on the left; the game paints its
// name, level and what it makes on the right, in the game's lettering. When the wizard stands at its door the bottom line
// says what E does; a level within reach makes the level glow.
using UnityEngine;
using UnityEngine.UI;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class GameLoop
    {
        Text[] boardTxt; string[] boardShown;

        void TickBoards()
        {
            int n = HG.BUILDING_IDS.Length;
            if (boardTxt == null) { boardTxt = new Text[n]; boardShown = new string[n]; }
            var font = boardTxt[0] ? null : Resources.Load<Font>("Fonts/LilitaOne-Regular");
            var wz = Wizard.I;
            for (int i = 0; i < n; i++)
            {
                var id = HG.BUILDING_IDS[i]; bool built = P.restored.Contains(id);
                if (!boardTxt[i])
                {
                    var f = Field.Front(i); var front = new Vector3(f.x, 0, f.y);
                    var go = new GameObject("board words " + id, typeof(RectTransform)); go.transform.SetParent(world, false);
                    var cv = go.AddComponent<Canvas>(); cv.renderMode = RenderMode.WorldSpace; cv.sortingOrder = 1;
                    var rt = (RectTransform)go.transform; rt.sizeDelta = new Vector2(250, 190);   // 1.2 x 0.86 m at the scale below
                    // the board's right half: its centre 0.4 m right of the post, on its face (1.54 m out from the front wall)
                    rt.position = Field.World(i, Field.PlotSize(i).x / 2 - 0.7f + 0.42f, Field.PlotSize(i).y / 2 + 1.565f, 2.0f);
                    rt.rotation = Quaternion.LookRotation(-front); rt.localScale = Vector3.one * 0.0048f;
                    var t = new GameObject("text", typeof(RectTransform)).AddComponent<Text>(); t.transform.SetParent(rt, false);
                    var tr = t.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
                    t.font = font ?? boardTxt[0].font; t.alignment = TextAnchor.MiddleCenter; t.supportRichText = true; t.fontSize = 34;
                    t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.lineSpacing = 0.82f;
                    t.color = new Color(1f, 0.94f, 0.8f); t.raycastTarget = false;
                    var ol = t.gameObject.AddComponent<Outline>(); ol.effectColor = new Color(0.12f, 0.06f, 0.03f, 0.95f); ol.effectDistance = new Vector2(2f, -2f);
                    boardTxt[i] = t;
                }
                var bt = boardTxt[i];
                if (bt.gameObject.activeSelf != built) bt.gameObject.SetActive(built);   // a ruin's charred board stays bare
                if (!built) continue;
                var b = HG.ById(id); int L = HG.LevelOf(P, id); double r = HG.BuildingRate(P, id, B);
                bool can = CanWork(i), near = wz && wz.On && wz.Near == i;
                string rs = r < 10 ? r.ToString("0.#") : HG.Fmt(r);
                string bottom = near ? $"<size=40><color=#FFE68C>E</color> Lv {L + 1} · {HG.Fmt(LevelCost(i))}</size>" : $"<size=46><color=#CFE9B0>+{rs}/s</color></size>";
                string lv = can ? "#FFE68C" : "#E8C46A";
                string s = $"<size=48>{b.name}</size>\n<size=88><color={lv}>Lv {L}</color></size>\n{bottom}";
                if (s != boardShown[i]) { bt.text = s; boardShown[i] = s; }
                // a level within reach: the level breathes brighter (seen across the square)
                float glow = can ? 0.5f + 0.5f * Mathf.Sin(Time.time * 4f + i) : 0f;
                bt.color = Color.Lerp(new Color(1f, 0.94f, 0.8f), Color.white, glow * 0.6f);
            }
        }
    }
}
