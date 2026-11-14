// The town's keepers and the time wizard's skills (owner, Nov 14 2026: "make the perks and store thing, as well as optional
// training or menus as a conversation with an npc. perhaps have the NPCs that have a quest or something for the player to
// interact with to have a traditional question mark or exclamation over their heads" / "the wizard is a time wizard ...
// Maybe even have an ability to freeze time ... Should be a skill trainer in here somewhere for future add-ons").
//  - Orrin the Star-Reader stands behind the Watchtower's star table (perks and stars), Mira behind the Market's Starshard
//    counter (the Shop), Elder Quill behind the Library's great book (quests, Feats, the Bestiary). Walk up and face one:
//    a conversation opens, and its choices lead to the old cards.
//  - Aurelian the Chronomancer comes to town at wave 5 and waits by the fountain: he teaches Time Stop the first time
//    and trains it after (the trainer is the home of every future skill).
//  - A gold '!' floats over a keeper with something new for the player, a '?' over one with something to hand in: over
//    the roof from outside (seen from the walls too), over the head inside.
//  - Time Stop (F, or its button): every enemy freezes for a few seconds while the towers keep firing; then it recharges.
using System.Linq;
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class GameLoop
    {
        public const int TRAINER = 99;
        public static readonly Vector3 TRAINER_AT = Field.PLAZA + new Vector3(4.3f, 0, 0.6f);
        readonly System.Collections.Generic.Dictionary<Transform, Quaternion> armRest = new System.Collections.Generic.Dictionary<Transform, Quaternion>();
        GameObject[] keepers; GameObject trainerObj; Transform[] marks; string[] markKind; float npcT;

        public static string KeeperOf(int i) => i == TRAINER ? "chronomancer" : HG.BUILDING_IDS[i] == "watchtower" ? "astronomer" : HG.BUILDING_IDS[i] == "market" ? "merchant" : HG.BUILDING_IDS[i] == "library" ? "librarian" : null;
        public bool TrainerHere => P.bestWave >= 5 || P.ascensions > 0;
        // where a keeper stands: behind the centrepiece, facing the door
        public static Vector3 TalkAt(int i)
        {
            if (i == TRAINER) return TRAINER_AT;
            var c = Field.CENTREPIECE[i]; float hz = Field.PlotSize(i).y / 2f;
            float v = Mathf.Max(c.y - (c.z + 0.65f), -(hz - 0.55f));
            return Field.World(i, c.x, v, Field.FLOOR);
        }
        public static float TalkReach(int i) => i == TRAINER ? 2.2f : 2f * Field.CENTREPIECE[i].z + 1.75f;

        void TickNpcs(float dt)
        {
            if (art == null) return;
            if (keepers == null) { keepers = new GameObject[HG.BUILDING_IDS.Length]; marks = new Transform[HG.BUILDING_IDS.Length + 1]; markKind = new string[marks.Length]; }
            npcT += dt;
            for (int i = 0; i < keepers.Length; i++)
            {
                bool on = CentreOn(i) && KeeperOf(i) != null && !P.fallen;
                if (on && !keepers[i]) keepers[i] = SpawnKeeper(KeeperOf(i), TalkAt(i), Field.Front(i));
                else if (!on && keepers[i]) { Destroy(keepers[i]); keepers[i] = null; }
                if (keepers[i]) Idle(keepers[i].transform, i);
            }
            bool tr = TrainerHere && !P.fallen;
            if (tr && !trainerObj)
            {
                trainerObj = SpawnKeeper("chronomancer", TRAINER_AT, new Vector2(0, 1));
                if (!told.Contains("trainer") && !P.told.Contains("trainer")) { told.Add("trainer"); P.told.Add("trainer"); Hud.Banner("An old Chronomancer has come to town. He waits by the fountain: walk the town (T) and talk to him"); }
            }
            else if (!tr && trainerObj) { Destroy(trainerObj); trainerObj = null; }
            if (trainerObj) Idle(trainerObj.transform, TRAINER);
            // the signs: '!' something new, '?' something to hand in
            for (int k = 0; k < marks.Length; k++)
            {
                int i = k == keepers.Length ? TRAINER : k; bool present = i == TRAINER ? trainerObj : keepers[k];
                string want = present ? Mark(i) : null;
                if (want != markKind[k] || (want != null && !marks[k]))
                {
                    if (marks[k]) Destroy(marks[k].gameObject);
                    marks[k] = null; markKind[k] = want;
                    var prefab = want == "!" ? art.markExcl : want == "?" ? art.markQuest : null;
                    if (prefab) { var g = Instantiate(prefab, world); foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; marks[k] = g.transform; }
                }
                if (!marks[k]) continue;
                var wz = Wizard.I; bool near = wz && wz.On && (i == TRAINER || wz.Inside == i);
                // over the head when the wizard is with them; over the roof (big enough to see from the walls) otherwise
                var at = near || i == TRAINER ? TalkAt(i) + Vector3.up * (2.5f + (i == TRAINER ? 0 : Field.FLOOR)) : Field.Plot(i) + Vector3.up * (Field.PlotHeight(i) + 1.6f);
                float sc = near ? 1.0f : wz && wz.On ? 1.7f : 2.4f;
                marks[k].position = at + Vector3.up * Mathf.Sin(npcT * 2.4f + k) * 0.15f * sc;
                var cam = Camera.main; if (cam) { var d = marks[k].position - cam.transform.position; d.y = 0; if (d.sqrMagnitude > 0.01f) marks[k].rotation = Quaternion.LookRotation(d); }
                marks[k].localScale = Vector3.one * sc * (1f + 0.05f * Mathf.Sin(npcT * 5f));
            }
        }
        GameObject SpawnKeeper(string kind, Vector3 at, Vector2 face)
        {
            var prefab = art.Npc(kind); if (prefab == null) return null;
            var g = Instantiate(prefab, world); g.name = "keeper " + kind;
            g.transform.position = at; g.transform.rotation = Quaternion.LookRotation(-new Vector3(face.x, 0, face.y));   // models face -Z
            g.transform.localScale = Vector3.one * 0.95f;
            Fx.I.Burst(at + Vector3.up * 1.2f, Field.GOLD, 10);
            return g;
        }
        // a keeper breathes, and gestures while the wizard talks to them
        void Idle(Transform t, int i)
        {
            float k = npcT + i * 0.7f; bool talking = Hud.TalkingTo == i;
            t.localScale = new Vector3(0.95f, 0.95f * (1f + Mathf.Sin(k * 2f) * 0.012f), 0.95f);
            foreach (Transform c in t)
            {
                if (c.name != "arm_R" && c.name != "arm_L") continue;
                if (!armRest.TryGetValue(c, out var rest)) armRest[c] = rest = c.localRotation;
                float a = c.name == "arm_R" ? (talking ? -25f + Mathf.Sin(k * 4f) * 18f : Mathf.Sin(k * 0.9f) * 4f) : (talking ? Mathf.Sin(k * 3.1f + 1f) * 10f : 0f);
                c.localRotation = Quaternion.Euler(a, 0, 0) * rest;
            }
        }
        // what a keeper has for the player: '!' new, '?' to hand in, null nothing
        public string Mark(int i)
        {
            switch (KeeperOf(i))
            {
                case "astronomer": return P.perkPts > 0 ? "!" : null;
                case "merchant": return P.shopOpen && HG.SHOP.Any(l => !Owns(l) && l.price > 0 && P.shards >= l.price) ? "!" : null;
                case "librarian": return P.quests.Any(q => q.n >= q.need && !q.claimed) ? "?" : Hud.FeatNews ? "!" : null;
                case "chronomancer": return CanTrain ? "!" : null;
            }
            return null;
        }

        // ---------- Time Stop ----------
        public int TimeStopLevel => P.skills.FirstOrDefault(s => s.k == "timestop")?.v ?? 0;
        public static readonly float[] TS_DUR = { 0, 4f, 5.5f, 7f }, TS_CD = { 0, 120f, 100f, 80f };
        public static readonly int[] TS_TRAIN = { 0, 60, 140 };   // Starshards to reach levels 2 and 3 (level 1 is his gift)
        public bool CanTrain => TrainerHere && (TimeStopLevel == 0 || (TimeStopLevel < 3 && P.shards >= TS_TRAIN[TimeStopLevel]));
        public float timeStop; float tsReadyAt, frozenAt;
        public float TimeStopLeft => Mathf.Max(0, tsReadyAt - Time.time);
        public bool TimeStopReady => TimeStopLevel > 0 && TimeStopLeft <= 0 && timeStop <= 0 && !P.fallen;
        public void Train()
        {
            int lv = TimeStopLevel; if (lv >= 3) return;
            if (lv > 0) { if (P.shards < TS_TRAIN[lv]) { Sound.Dud(); return; } P.shards -= TS_TRAIN[lv]; }
            P.skills.RemoveAll(s => s.k == "timestop"); P.skills.Add(new KV { k = "timestop", v = lv + 1 });
            Sound.Play("level_up", 0.5f); Fx.I.Burst(TRAINER_AT + Vector3.up * 2, new Color(0.6f, 0.85f, 1f), 30);
            Hud.Banner(lv == 0 ? "You learned Time Stop! Press F (or its button) when the walls are in trouble" : $"Time Stop {lv + 1}: {TS_DUR[lv + 1]:0.#} s, ready every {TS_CD[lv + 1]:0} s");
            Save();
        }
        public void UseTimeStop()
        {
            if (!TimeStopReady || Hud.Paused) { Sound.Dud(); return; }
            int lv = TimeStopLevel; timeStop = TS_DUR[lv]; tsReadyAt = Time.time + TS_DUR[lv] + TS_CD[lv]; frozenAt = Time.time;
            Sound.Play("surge", 0.6f); Sound.Play("ascension", 0.3f);
            StartCoroutine(Dial(new Vector3(0, 9f, Field.GATE_Z + 2f), TS_DUR[lv], 1.1f, true));
            foreach (var e in enemies) if (e.dying <= 0) Fx.I.Burst(e.Pos, new Color(0.7f, 0.85f, 1f), 4);
            if (!told.Contains("tsUsed")) { told.Add("tsUsed"); Hud.Banner("Time stands still. The towers fire on"); }
        }
        // the great clock: rises over (at), turns (forward for Time Stop, backward fast for a rewind), and fades
        System.Collections.IEnumerator Dial(Vector3 at, float hold, float scale, bool forward)
        {
            if (art == null || art.timeDial == null) yield break;
            var g = Instantiate(art.timeDial, world); var t = g.transform; t.position = at;
            foreach (var r in g.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Transform hm = null, hh = null; foreach (var c in g.GetComponentsInChildren<Transform>()) { if (c.name == "hand_m") hm = c; if (c.name == "hand_h") hh = c; }
            var cam = Camera.main; float life = hold + 0.9f;
            for (float u = 0; u < life; u += Time.unscaledDeltaTime)
            {
                if (cam) { var d = t.position - cam.transform.position; t.rotation = Quaternion.LookRotation(d); }   // its face (-Z) to the camera
                float k = u < 0.35f ? Mathf.SmoothStep(0, 1, u / 0.35f) : u > life - 0.5f ? Mathf.SmoothStep(0, 1, (life - u) / 0.5f) : 1f;
                t.localScale = Vector3.one * scale * k;
                float spin = forward ? (u < 0.5f ? 360f : 12f) : -720f;   // Time Stop: the hands lurch, then crawl; a rewind: they race backward
                if (hm) hm.Rotate(t.forward, spin * Time.unscaledDeltaTime, Space.World);
                if (hh) hh.Rotate(t.forward, spin / 12f * Time.unscaledDeltaTime, Space.World);
                Hud.SetTint(new Color(0.55f, 0.75f, 1f, 0.22f * k));
                yield return null;
            }
            Hud.SetTint(Color.clear); Destroy(g);
        }
    }
}
