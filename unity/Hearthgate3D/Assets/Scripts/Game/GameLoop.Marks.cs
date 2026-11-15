// The timeMarks in time: one golden hourglass circles above the fountain for every Ascension (owner, Nov 14 2026: "we are not
// highlighting the ascension levels in any way in the game ... we should be making the ascension level a highlight to show
// progress"). The ring grows with the run's lasting progress and is seen from the walls and from the square; a new
// Ascension adds its hourglass with a flash.
using UnityEngine;
using System.Collections.Generic;

namespace Hearthgate.Game
{
    public partial class GameLoop
    {
        readonly List<Transform> timeMarks = new List<Transform>();
        float markUnit;
        const int MARKS_MAX = 12;
        const float MARK_R = 2.6f, MARK_H = 6.2f, MARK_SIZE = 0.75f;

        void TickMarks()
        {
            if (!art || !art.hourglass) return;
            int want = Mathf.Min(P.ascensions, MARKS_MAX);
            while (timeMarks.Count < want)
            {
                var g = Instantiate(art.hourglass, world); g.name = "mark in time";
                if (markUnit <= 0)
                {
                    float h = 0f; foreach (var r in g.GetComponentsInChildren<Renderer>()) h = Mathf.Max(h, r.bounds.size.y);   // sized from the mesh as placed
                    markUnit = MARK_SIZE / Mathf.Max(0.01f, h);
                }
                g.transform.localScale = g.transform.localScale * markUnit;
                foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                if (timeMarks.Count > 0 && Time.timeSinceLevelLoad > 3f && Fx.I) Fx.I.Pop(Field.PLAZA + Vector3.up * (MARK_H + 1f), "+1 mark in time", Field.GOLD, 30);
                timeMarks.Add(g.transform);
            }
            while (timeMarks.Count > want) { var t = timeMarks[timeMarks.Count - 1]; timeMarks.RemoveAt(timeMarks.Count - 1); if (t) Destroy(t.gameObject); }
            float now = Time.time;
            for (int k = 0; k < timeMarks.Count; k++)
            {
                if (!timeMarks[k]) continue;
                float a = now * 0.35f + k * Mathf.PI * 2f / Mathf.Max(1, timeMarks.Count);
                timeMarks[k].position = Field.PLAZA + new Vector3(Mathf.Cos(a) * MARK_R, MARK_H + 0.25f * Mathf.Sin(now * 1.3f + k), Mathf.Sin(a) * MARK_R);
                timeMarks[k].rotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + now * 40f, 0) * art.hourglass.transform.rotation;
            }
        }
    }
}
