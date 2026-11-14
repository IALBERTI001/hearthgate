// Starshards, the Shop and the daily quests (js/core.js): the Market opens the Shop (looks only, nothing in the fight
// changes); the Library keeps three quests a day. Starshards come from the Market opening, bosses, new combos, quests and
// Ascension, never from Resonance.
using System.Linq;
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class GameLoop
    {
        public static string Today() => System.DateTime.Now.ToString("yyyy-MM-dd");
        Light sun;

        public void GiveShards(int n, Vector3 at)
        {
            if (!P.shopOpen || n <= 0) return;
            P.shards += n; Fx.I.Pop(at, $"+{n} Starshard{(n == 1 ? "" : "s")}", ROSE, 32, 1.2f);
        }
        void OpenShop()
        {
            if (P.shopOpen) return;
            P.shopOpen = true; P.shards += HG.SHARDS["welcome"]; HG.QuestsFor(P, Today());
            Hud.Banner($"The Market opens the Shop! +{HG.SHARDS["welcome"]} Starshards. The Library keeps your quests");
        }

        // ---------- quests ----------
        static readonly System.Collections.Generic.Dictionary<string, string> STAT_OF = new System.Collections.Generic.Dictionary<string, string>
        { { "kills", "kills" }, { "boss", "bosses" }, { "combo", "combos" }, { "shatter", "shatter" }, { "upgrade", "upgrades" }, { "level", "levels" }, { "surge", "surges" } };
        // every counted event feeds the lifetime stats (Feats) and, once the Shop is open, today's quests
        public void Quest(string id, int n = 1)
        {
            if (STAT_OF.TryGetValue(id, out var st)) P.AddStat(st, n);
            if (!P.shopOpen) return;
            HG.QuestsFor(P, Today());
            foreach (var q in P.quests) if (q.id == id && q.n < q.need) { q.n = Mathf.Min(q.need, q.n + n); if (q.n == q.need) Hud.Banner($"Quest done: {HG.QuestText(q)}. Claim it at the Library"); }
        }
        public int QuestsToClaim => !P.shopOpen ? 0 : P.quests.Count(q => q.n >= q.need && !q.claimed) + (BonusReady ? 1 : 0);
        public bool BonusReady => P.quests.Count > 0 && !P.questBonus && P.quests.All(q => q.claimed);
        public void ClaimQuest(int i)
        {
            if (i < 0 || i >= P.quests.Count) return; var q = P.quests[i];
            if (q.claimed || q.n < q.need) { Sound.Dud(); return; }
            q.claimed = true; P.AddStat("quests", 1); P.shards += HG.SHARDS["quest"]; Sound.Play("claim", 0.6f); Save();
        }
        public void ClaimBonus() { if (!BonusReady) { Sound.Dud(); return; } P.questBonus = true; P.shards += HG.SHARDS["questBonus"]; Sound.Play("feat", 0.6f); Save(); }

        // ---------- Feats: checked twice a second; each is a gold-ringed moment and Starshards ----------
        float featT;
        void CheckFeats()
        {
            if ((featT += Time.deltaTime) < 0.5f) return; featT = 0;
            var got = HG.FeatsEarned(P); if (got.Count == 0) return;
            int pay = got.Sum(f => HG.FEAT_PAY[f.tier]); Save();
            string paid = P.shopOpen ? $" +{pay} Starshards" : "";
            Hud.Banner(got.Count == 1 ? $"Feat: {got[0].name}!{paid}" : $"{got.Count} Feats earned!{paid}");
            Sound.Play("feat", 0.6f); Hud.FeatNews = true;
        }

        // ---------- the Shop ----------
        public bool Owns(HG.Look l) => l.price == 0 || P.owned.Contains(l.id);
        public void BuyOrEquip(HG.Look l)
        {
            if (!Owns(l))
            {
                if (P.shards < l.price) { Sound.Dud(); return; }
                P.shards -= l.price; P.owned.Add(l.id); Sound.Play("buy", 0.6f);
            }
            var slot = HG.Slot(l); P.equip.RemoveAll(e => e.k == slot); P.equip.Add(new KS { k = slot, s = l.id });
            Sound.Click(); ApplyLooks(); Save();
        }
        string Look(string slot) => HG.Equipped(P, slot);
        // the tower cap for an element in its equipped look (hg_front.cap_look models)
        GameObject TopFor(string el)
        {
            if (art == null) return null;
            if (el == "fire" || el == "frost" || el == "storm")
            {
                var id = Look("tower:" + el);
                if (!id.EndsWith("-classic")) foreach (var c in art.capLooks) if (c && c.name == "top_" + id.Replace('-', '_')) return c;
            }
            return art.Top(el);
        }
        Color WispColor => Look("wisp") switch { "wisp-rose" => ROSE, "wisp-star" => new Color(0.85f, 0.93f, 1f), "wisp-ruby" => new Color(0.95f, 0.15f, 0.25f), _ => Field.GOLD };
        void ApplyLooks()
        {
            RefreshTowers();
            if (!sun) return;
            switch (Look("town"))
            {   // the city's light: the same sun, another hour or season
                case "town-golden": sun.color = new Color(1f, 0.72f, 0.42f); sun.intensity = 1.45f; RenderSettings.ambientSkyColor = new Color(0.55f, 0.45f, 0.5f); break;
                case "town-autumn": sun.color = new Color(1f, 0.78f, 0.5f); sun.intensity = 1.25f; RenderSettings.ambientSkyColor = new Color(0.5f, 0.42f, 0.35f); break;
                case "town-winter": sun.color = new Color(0.85f, 0.92f, 1f); sun.intensity = 1.35f; RenderSettings.ambientSkyColor = new Color(0.55f, 0.65f, 0.85f); break;
                default: sun.color = new Color(1f, 0.9f, 0.74f); sun.intensity = 1.3f; RenderSettings.ambientSkyColor = new Color(0.42f, 0.5f, 0.7f); break;
            }
        }
        // a shot's trail and a hit's burst in the equipped look
        void StyleTrail(TrailRenderer tr, Color col)
        {
            switch (Look("trail"))
            {
                case "trail-comet": tr.time = 0.5f; tr.startWidth = 0.6f; break;
                case "trail-sparkle": tr.time = 0.3f; tr.startWidth = 0.3f; tr.material = Art.Flat(Color.Lerp(col, Color.white, 0.5f)); break;
            }
        }
        void HitBurst(Vector3 at, Color c, int n)
        {
            switch (Look("hit"))
            {
                case "hit-star": Fx.I.Burst(at, Color.Lerp(c, Color.white, 0.7f), n + 4); break;
                case "hit-shards": Fx.I.Burst(at, new Color(0.75f, 0.95f, 1f), n); Fx.I.Burst(at, c, n / 2); break;
                default: Fx.I.Burst(at, c, n); break;
            }
        }
    }
}
