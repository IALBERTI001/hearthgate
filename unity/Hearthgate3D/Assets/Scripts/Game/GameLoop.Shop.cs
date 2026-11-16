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
            // always earned and always shown (owner, Nov 14 2026: "It shouldnt be a surprise to see I earned stars"); the
            // first time, one line says what they are for
            if (n <= 0) return;
            P.shards += n; Fx.I.Pop(at, $"+{n} Starshard{(n == 1 ? "" : "s")}", new Color(1f, 0.9f, 0.55f), 32, 1.2f);   // the star's gold, not the perk crystal's rose (bot round 9) Hud.ShardsIn(n); TellShards();
        }
        public void TellShards()
        {
            if (P.told.Contains("shards")) return; P.told.Add("shards");
            Hud.Banner(P.shopOpen ? "Starshards! Spend them at the Market's Starshard counter" : "Starshards! Save them: the Market's Shop sells new looks once you rebuild it");
        }
        void OpenShop()
        {
            if (P.shopOpen) return;
            P.shopOpen = true; P.shards += HG.SHARDS["welcome"]; HG.QuestsFor(P, Today());
            Hud.Banner($"The Market opens the Shop! +{HG.SHARDS["welcome"]} Starshards. Walk in to its counter; the Library's great book keeps your quests");
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
            foreach (var q in P.quests) if (q.id == id && q.n < q.need) { q.n = Mathf.Min(q.need, q.n + n); if (q.n == q.need) Hud.Banner(P.restored.Contains("library") ? $"Quest done: {HG.QuestText(q)}. Claim it at the Library" : $"Quest done: {HG.QuestText(q)}. Rebuild the Library to claim it"); }   // never send a player to a ruin (bot round 10)
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
            string paid = $" +{pay} Starshards"; Hud.ShardsIn(pay);
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
        // ---------- each tower's own element and design (owner, Nov 14 2026: "I should be able to customize the tower with
        // purchased designs from the market. As well as which tower element i want to place and where") ----------
        public string TowerLook(int n, string el)
        {
            var mine = n >= 0 && n < P.towerLook.Count ? P.towerLook[n] : "";
            var l = HG.SHOP.FirstOrDefault(x => x.id == mine);
            return l != null && l.el == el && Owns(l) ? l.id : Look("tower:" + el);   // its own design if it suits the element, else the Shop's pick
        }
        public string TowerEl(int n) => towers.Count > n ? towers[n].el : "none";
        public void SetTowerEl(int n, string el, bool all = false)
        {
            for (int i = 0; i < 4; i++) if (i == n || all) P.towerEl[i] = el;
            RefreshTowers(); Sound.Play("tier_up", 0.35f);
            foreach (var tw in towers) if (!tw.beacon && (tw.n == n || all)) Fx.I.Burst(tw.Muzzle, Field.Of(tw.el), 16);
            Save();
        }
        public void SetTowerLook(int n, string id)
        {
            var l = HG.SHOP.FirstOrDefault(x => x.id == id); if (l != null && !Owns(l)) { Sound.Dud(); return; }
            P.towerLook[n] = id; RefreshTowers(); Sound.Play("buy", 0.4f);
            foreach (var tw in towers) if (tw.n == n) Fx.I.Burst(tw.Muzzle, Field.GOLD, 14);
            Save();
        }
        // the tower cap for an element in its equipped look (hg_front.cap_look models)
        GameObject TopFor(string el, int n = -1)
        {
            if (art == null) return null;
            if (el == "fire" || el == "frost" || el == "storm")
            {
                var id = TowerLook(n, el);
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
                case "trail-comet": tr.time = 0.5f; tr.startWidth = 0.6f * Fx.TrailScale; break;
                case "trail-sparkle": tr.time = 0.3f; tr.startWidth = 0.3f * Fx.TrailScale; tr.material = Art.Flat(Color.Lerp(col, Color.white, 0.5f)); break;
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
