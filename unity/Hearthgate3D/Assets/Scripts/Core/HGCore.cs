// Hearthgate core rules, ported from js/core.js (the 2D build, Nov 13 2026). Pure logic: no Unity scene objects, so the
// numbers stay identical to the game players already tuned. Every balance number lives here.
// Ported so far: numbers, buildings and restore order, gate stages and forks, bonuses, costs, damage, the city's income,
// the four first enemy roles and the first bosses, the wave mix before Ascension, offline earnings, the save profile.
// Still in js/core.js only: Ascension unlocks, constellations, relics, omens, perks, events, quests, feats, shop.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Hearthgate.Core
{
    public static class HG
    {
        // ---------- numbers: 999, 1K, 1.04K, 12.5K, 3.2M ... never capped ----------
        static readonly string[] UNITS = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };
        public static string Fmt(double n)
        {
            if (double.IsNaN(n) || double.IsInfinity(n)) return "0";
            bool neg = n < 0; n = Math.Abs(n);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (n < 1000) return (neg ? "-" : "") + Math.Floor(n).ToString("0", inv);
            int i = 0; while (n >= 1000 && i < 700) { n /= 1000; i++; }
            if (n >= 999.5) { n /= 1000; i++; }
            string s = n >= 100 ? n.ToString("0", inv) : n >= 10 ? n.ToString("0.0", inv) : n.ToString("0.00", inv);
            if (s.Contains(".")) s = s.TrimEnd('0').TrimEnd('.');
            return (neg ? "-" : "") + s + Unit(i);
        }
        static string Unit(int i)
        {
            if (i < UNITS.Length) return UNITS[i];
            int j = i - UNITS.Length; const string a = "abcdefghijklmnopqrstuvwxyz";
            return a[(j / 26) % 26].ToString() + a[j % 26];
        }

        // ---------- buildings ----------
        public class Building { public string id, name, element, ability, line; public Dictionary<string, double> bonus = new Dictionary<string, double>(); }
        static Building B(string id, string name, string element, string ability, string line, params (string, double)[] bonus)
        { var b = new Building { id = id, name = name, element = element, ability = ability, line = line }; foreach (var (k, v) in bonus) b.bonus[k] = v; return b; }
        public static readonly Building[] BUILDINGS =
        {
            B("forge", "Forge", "fire", null, "Towers gain Fire. Hits burn."),
            B("frostSpire", "Frost Spire", "frost", null, "Towers gain Frost. Hits slow."),
            B("stormBell", "Storm Bell", "storm", null, "Towers gain Storm. Hits chain."),
            B("bakery", "Bakery", null, "wisps", "Golden Wisps come twice as often. +15% Resonance.", ("res", 0.15)),
            B("market", "Market", null, "shop", "Home of the Shop. Upgrades cost 10% less.", ("upgradeDiscount", 0.10)),
            B("tavern", "Tavern", null, "cheer", "Combos fill the Cheer meter: when full, towers fire twice as fast.", ("fireRate", 0.10)),
            B("granary", "Granary", null, "store", "Stores 50% more while you are away, and 4 more hours.", ("offline", 0.5)),
            B("library", "Library", null, "quests", "Home of Quests and Feats. Combos hit 25% harder.", ("combo", 0.25)),
            B("cottages", "Cottages", null, "repair", "Villagers repair the walls after every wave.", ("gateHp", 0.25)),
            B("watchtower", "Watchtower", null, "tower5", "Its beacon joins the fight as a fifth tower. Home of the Perks.", ("damage", 0.15)),
        };
        public static readonly string[] BUILDING_IDS = BUILDINGS.Select(b => b.id).ToArray();
        public static Building ById(string id) => BUILDINGS.FirstOrDefault(b => b.id == id);
        public const int FIXED_ORDER = 3;

        // the first three in fixed order; then two unrestored ones, the same two until one is picked
        public static List<string> RestoreOffer(Profile p)
        {
            var left = BUILDING_IDS.Where(id => !p.restored.Contains(id)).ToList();
            if (left.Count == 0) return new List<string>();
            if (p.restored.Count < FIXED_ORDER) return new List<string> { BUILDING_IDS[p.restored.Count] };
            if (left.Count == 1) return left;
            int seed = (p.restored.Count * 7 + p.ascensions * 13 + 3) % left.Count;
            var o = new List<string> { left[seed], left[(seed + 1 + (p.ascensions % (left.Count - 1))) % left.Count] };
            return o.Distinct().ToList();
        }

        // ---------- Ascended buildings (js/core.js RANK2): after the first Ascension every building comes back with a second
        // ability, so each restore in a new run brings something new ----------
        public class Rank2 { public string name, line; public Dictionary<string, double> bonus = new Dictionary<string, double>(); }
        static Rank2 R2(string name, string line, params (string, double)[] bonus) { var r = new Rank2 { name = name, line = line }; foreach (var (k, v) in bonus) r.bonus[k] = v; return r; }
        public static readonly Dictionary<string, Rank2> RANK2 = new Dictionary<string, Rank2>
        {
            { "forge", R2("Molten Forge", "Burning enemies explode when they fall, setting their friends alight", ("fire", 0.25), ("molten", 1)) },
            { "frostSpire", R2("Deep Frost", "Frost lingers half again as long and hits 25% harder", ("frost", 0.25), ("deepFrost", 1)) },
            { "stormBell", R2("Thunder Bell", "Storm chains jump to two more enemies", ("storm", 0.25), ("chainPlus", 2)) },
            { "bakery", R2("Golden Ovens", "Golden Wisps pay double", ("wispX", 1)) },
            { "market", R2("Grand Bazaar", "Treasure chests pay double. Upgrades cost 5% less", ("chestX", 1), ("upgradeDiscount", 0.05)) },
            { "tavern", R2("Feast Hall", "Cheer lasts 10 seconds instead of 6", ("cheerLong", 1)) },
            { "granary", R2("Harvest Stores", "Every cleared wave pays 5 seconds of income", ("harvest", 5)) },
            { "library", R2("Grand Archive", "Combos hit another 25% harder", ("combo", 0.25)) },
            { "cottages", R2("Masons", "The walls are 25% stronger", ("gateHp", 0.25)) },
            { "watchtower", R2("Bright Beacon", "The Watchtower beacon fires twice as fast", ("beacon", 1)) },
        };
        public static string NameOf(Profile p, string id) => p.ascensions >= 1 && RANK2.ContainsKey(id) ? RANK2[id].name : ById(id).name;
        public static string LineOf(Profile p, string id) => p.ascensions >= 1 && RANK2.ContainsKey(id) ? RANK2[id].line : ById(id).line;

        // ---------- constellations (js/core.js STARS): each of the first ten Ascension runs has five stars, lit at fixed waves
        // on the way to that run's Ascend wave; each is a named, lasting boon ----------
        public class Star { public string id, name, line; public int asc, k; public Dictionary<string, double> bonus = new Dictionary<string, double>(); }
        public static readonly string[] CONSTELLATIONS = { "Anvil", "Lantern", "Hound", "Crown", "Serpent", "Harp", "Tower", "Phoenix", "Owl", "Dragon" };
        static readonly (string part, string key, double v, string what)[][] STAR_KINDS =
        {
            new[] { ("Eye", "damage", 0.04, "damage"), ("Heart", "gateHp", 0.08, "wall health"), ("Spark", "fireRate", 0.03, "fire rate"), ("Crown", "res", 0.05, "Resonance"), ("Tail", "combo", 0.08, "combo damage") },
            new[] { ("Ember", "fire", 0.08, "Fire damage"), ("Frost", "frost", 0.08, "Frost damage"), ("Bolt", "storm", 0.08, "Storm damage"), ("Hearth", "city", 0.06, "city output"), ("Wing", "bossDmg", 0.1, "boss damage") },
        };
        public static readonly Star[] STARS = CONSTELLATIONS.SelectMany((c, i) => STAR_KINDS[i % 2].Select((sk, k) =>
        { var st = new Star { id = c.ToLower() + k, asc = i + 1, k = k, name = $"{c}'s {sk.part}", line = $"+{Math.Round(sk.v * 100)}% {sk.what}" }; st.bonus[sk.key] = sk.v; return st; })).ToArray();
        public static int StarWave(Profile p, int k) => (int)Math.Round(AscendWave(p) * (k + 1) / 6.0);   // five stars spread before the Ascend wave
        public static List<Star> StarsDue(Profile p) => STARS.Where(st => st.asc == p.ascensions && !p.stars.Contains(st.id) && Math.Max(p.runWave, p.wave) >= StarWave(p, st.k)).ToList();

        // ---------- gate stages and forks ----------
        public static readonly int[] STAGES = { 1, 15, 40, 60, 85 };
        public class Fork { public string id, name, line; public Dictionary<string, double> bonus = new Dictionary<string, double>(); }
        static Fork F(string id, string name, string line, params (string, double)[] bonus)
        { var f = new Fork { id = id, name = name, line = line }; foreach (var (k, v) in bonus) f.bonus[k] = v; return f; }
        public static readonly Dictionary<int, Fork[]> FORKS = new Dictionary<int, Fork[]>
        {
            { 2, new[] { F("ballistae", "Heavy Bolts", "+40% tower damage", ("damage", 0.40)), F("crews", "Swift Crews", "+35% fire rate", ("fireRate", 0.35)) } },
            { 3, new[] { F("focus", "Elemental Focus", "Combos hit 50% harder", ("combo", 0.50)), F("ironGate", "Iron Walls", "Double wall health, slow repair", ("gateHp", 1.0), ("regen", 0.02)) } },
            { 4, new[] { F("volleys", "Rapid Volleys", "Towers fire 50% faster", ("fireRate", 0.5)), F("cores", "Searing Cores", "+60% tower damage", ("damage", 0.6)) } },
            { 5, new[] { F("resonant", "Resonant Strikes", "+50% Resonance from enemies", ("res", 0.5)), F("kingslayer", "Kingslayer", "Bosses take double damage", ("bossDmg", 1)) } },
        };
        public static readonly int[] FORK_STAGES = { 2, 3, 4, 5 };
        public static int StageOf(int lvl) { int s = 1; for (int i = 1; i < STAGES.Length; i++) if (lvl >= STAGES[i]) s = i + 1; return s; }
        public static int PendingFork(Profile p) { foreach (var s in FORK_STAGES) if (StageOf(p.gateLevel) >= s && p.ForkAt(s) == null) return s; return 0; }

        // ---------- bonuses, summed ----------
        public class Bonus
        {
            readonly Dictionary<string, double> v = new Dictionary<string, double>();
            public double this[string k] { get => v.TryGetValue(k, out var x) ? x : 0; set => v[k] = value; }
            public void Add(Dictionary<string, double> o) { foreach (var kv in o) this[kv.Key] += kv.Value; }
            public double ascend = 1;
        }
        public const double ASCEND_MULT = 1.5;
        public static Bonus Bonuses(Profile p)
        {
            var b = new Bonus();
            foreach (var id in p.restored) { if (id == "granary") b["offlineH"] += 4; var d = ById(id); if (d != null) b.Add(d.bonus); }
            foreach (var s in FORK_STAGES) { var fid = p.ForkAt(s); var f = fid == null ? null : FORKS[s].FirstOrDefault(x => x.id == fid); if (f != null) b.Add(f.bonus); }
            foreach (var k in PERKS) { int n = p.PerkAt(k.id); if (n > 0) foreach (var kv in k.per) b[kv.Key] += kv.Value * n; }
            if (p.ascensions >= 1) foreach (var id in p.restored) if (RANK2.TryGetValue(id, out var r2)) b.Add(r2.bonus);
            foreach (var id in p.stars) { var st = STARS.FirstOrDefault(x => x.id == id); if (st != null) b.Add(st.bonus); }
            b.ascend = Math.Pow(ASCEND_MULT, p.ascensions);
            return b;
        }

        // ---------- Ascension (js/core.js): at 10/10 and a deep enough run, the city returns to ruins and the waves restart;
        // towers and Resonance x1.5 for good, stacking; perk points to spend at the Watchtower. Each Ascension asks for a
        // deeper run (wave 30, 35, 40 ... up to 90), or the x1.5 made every loop shorter.
        public const int ETERNAL_AT = 10;
        public static int AscendWave(Profile p) => Math.Min(90, 30 + 5 * p.ascensions);
        public static bool CityWhole(Profile p) => p.restored.Count >= BUILDINGS.Length;
        public static bool CanAscend(Profile p) => CityWhole(p) && Math.Max(p.runWave, p.wave) >= AscendWave(p);
        public static int PerkPtsFor(Profile p) => 1 + Math.Max(p.runWave, p.wave) / 25;   // wave 50 pays 3, wave 100 pays 5
        public static Profile Ascend(Profile p)
        {
            var q = new Profile
            {
                ascensions = p.ascensions + 1, bestWave = p.bestWave, combos = new List<string>(p.combos), firstSession = false, lastSeen = p.lastSeen,
                music = p.music, sfx = p.sfx, reduced = p.reduced, surgeUntil = p.surgeUntil,
                perks = p.perks.Select(x => new KV { k = x.k, v = x.v }).ToList(), perkPts = p.perkPts + PerkPtsFor(p), told = new List<string>(p.told), seenEvents = new List<string>(p.seenEvents), seenRoles = new List<string>(p.seenRoles),
                shards = p.shards + (p.shopOpen ? SHARDS["ascend"] : 0), shopOpen = p.shopOpen, owned = new List<string>(p.owned), equip = p.equip.Select(e => new KS { k = e.k, s = e.s }).ToList(),
                stats = p.stats.Select(x => new KV { k = x.k, v = x.v }).ToList(), feats = new List<string>(p.feats), stars = new List<string>(p.stars),
                questDay = p.questDay, questBonus = p.questBonus, quests = p.quests.Select(q => new QuestState { id = q.id, n = q.n, need = q.need, claimed = q.claimed }).ToList()
            };
            int st = q.PerkAt("start") * 5; if (st > 0) { q.wave = 1 + st; q.gateLevel = 1 + st; q.runWave = q.wave; }   // Head Start
            return q;
        }

        // ---------- perks: bought with the points each Ascension pays; each level costs one more than the last ----------
        public class Perk { public string id, name, line; public int max, cost; public Dictionary<string, double> per = new Dictionary<string, double>(); }
        static Perk K(string id, string name, string line, int max, int cost, params (string, double)[] per)
        { var k = new Perk { id = id, name = name, line = line, max = max, cost = cost }; foreach (var (a, v) in per) k.per[a] = v; return k; }
        public static readonly Perk[] PERKS =
        {
            K("sharp", "Keen Sights", "Towers hit 15% harder", 5, 0, ("damage", 0.15)),
            K("hum", "Deep Hum", "The city makes 25% more", 4, 0, ("city", 0.25)),
            K("rich", "Rich Veins", "+15% Resonance from enemies", 4, 0, ("res", 0.15)),
            K("night", "Night Watch", "Offline earnings last 2 hours longer", 4, 0, ("offlineH", 2)),
            K("surge", "Quick Surge", "The Surge recharges 3 minutes sooner", 3, 0, ("surgeCd", 3)),
            K("start", "Head Start", "Each new city starts 5 waves and 5 tower levels ahead", 3, 0, ("start", 5)),
            K("flame", "Eternal Flame", "Towers hit 10% harder and enemies pay 10% more", int.MaxValue, 3, ("damage", 0.1), ("res", 0.1)),
        };
        public static int PerkCost(Profile p, string id) { var k = PERKS.First(x => x.id == id); return k.cost > 0 ? k.cost : p.PerkAt(id) + 1; }
        public static bool CanBuyPerk(Profile p, string id) { var k = PERKS.First(x => x.id == id); return p.PerkAt(id) < k.max && p.perkPts >= PerkCost(p, id); }
        public static bool BuyPerk(Profile p, string id)
        {
            if (!CanBuyPerk(p, id)) return false;
            p.perkPts -= PerkCost(p, id); p.SetPerk(id, p.PerkAt(id) + 1); return true;
        }

        public static double UpgradeCost(int lvl, Bonus b) => Math.Floor(5 * Math.Pow(1.27, lvl - 1) * (1 - (b == null ? 0 : b["upgradeDiscount"])));
        public static double RestoreCost(int n) => n < FIXED_ORDER ? new double[] { 30, 80, 220 }[n] : Math.Floor(600 * Math.Pow(2.7, n - 3));
        public static double TowerDamage(Profile p, Bonus b) => 3 * Math.Pow(1.235, p.gateLevel - 1) * (1 + b["damage"]) * b.ascend;
        public static double FireInterval(Bonus b) => 0.55 / (1 + b["fireRate"]);
        public static double GateMax(Profile p, Bonus b) => Math.Floor(100 * Math.Pow(1.11, p.gateLevel - 1) * (1 + b["gateHp"]));
        public static double KillRes(int wave, bool boss, Bonus b) => Math.Ceiling(4 * Math.Pow(1.12, wave - 1) * (boss ? 25 : 1) * (1 + b["res"]) * b.ascend);

        // ---------- the city makes Resonance ----------
        public static readonly int[] MILESTONES = { 10, 25, 50, 100 };
        public const double CITY_BASE = 0.09, LEVEL_COST = 0.4, OFFLINE_CAP_H = 8;
        public static int LevelOf(Profile p, string id) => p.restored.Contains(id) ? Math.Max(1, p.LevelAt(id)) : 0;
        public static double BuildingRate(Profile p, string id, Bonus b, int lvl = 0)
        {
            int idx = p.restored.IndexOf(id); if (idx < 0) return 0;
            int L = lvl > 0 ? lvl : LevelOf(p, id), dbl = MILESTONES.Count(m => L >= m);
            return CITY_BASE * Math.Pow(2.7, idx) * L * Math.Pow(2, dbl) * b.ascend * (1 + b["city"]) * (p.ascensions >= 2 ? 1.25 : 1);
        }
        public static double CityRate(Profile p, Bonus b) => p.restored.Sum(id => BuildingRate(p, id, b));
        public static double LevelCost(Profile p, string id, Bonus b)
        {
            int idx = p.restored.IndexOf(id); if (idx < 0) return double.PositiveInfinity;
            return Math.Floor(RestoreCost(idx) * LEVEL_COST * Math.Pow(1.15, LevelOf(p, id) - 1) * (1 - b["upgradeDiscount"]));
        }

        // ---------- enemy roles ----------
        public class Role { public string id, model, label, weak, tip, summonRole; public double hp, speed, size, armour, heal, comboWeak, totem, summon; public bool boss, chest, shield, blink, thief, bomb; public int split; }
        static Role R(string id, string model, double hp, double speed, double size, string label, string weak = null, double armour = 0, bool boss = false, int split = 0)
            => new Role { id = id, model = model, hp = hp, speed = speed, size = size, label = label, weak = weak, armour = armour, boss = boss, split = split };
        // speed and size are in the 2D stage's px (the field was 414 px deep); the 3D field converts them (Field.cs)
        public static readonly Dictionary<string, Role> ROLES = new[]
        {
            R("slime", "slime", 1, 46, 66, "Slimes"),
            R("runner", "beetle", 0.7, 84, 60, "Runners", "frost"),
            R("armoured", "golem", 2.6, 36, 76, "Armoured", "fire", 0.5),
            R("swarm", "slime", 0.35, 54, 44, "Swarm", "storm"),
            // the variants (js/core.js VARIANT_FROM): one new kind every few waves, each with a counter to learn
            Tip(R("elite", "elite", 3, 60, 70, "Elite"), "Elites drop a treasure chest", chest: true),
            Tip(R("shielded", "shielded", 1, 70, 62, "Shielded", "storm"), "Shields block most damage: Storm breaks them", shield: true),
            Tip(R("splitter", "splitter", 1.6, 40, 76, "Splitters", "fire", 0, false, 2), "Splitters burst into two small slimes"),
            Tip(R("mender", "mender", 1.2, 42, 64, "Menders", "fire"), "Menders heal their friends: burning stops them", heal: 0.05),
            Tip(R("ironhide", "ironhide", 3.4, 32, 80, "Ironhide", "storm", 0.5), "Ironhide: Storm breaks the shield, then Fire burns the armour", shield: true),
            Tip(R("sprinter", "sprinter", 0.8, 128, 56, "Sprinters", "frost"), "Sprinters dash for the walls: Frost slows them"),
            Tip(R("bulwark", "bulwark", 6, 26, 86, "Bulwarks"), "Bulwarks shrug off hits: combos crack them for double", comboWeak: 2),
            // the Ascension enemies (js/core.js ROLES): each later loop meets new kinds, with a counter to learn
            Asc(R("phantom", "phantom", 1.2, 66, 62, "Phantoms", "storm"), "Phantoms fade in and out: hits land only when they show", blink: true),
            Asc(R("thief", "thief", 1, 110, 54, "Thieves", "frost"), "Thieves grab Resonance at the walls: stop them first", thief: true),
            Asc(R("bomber", "bomber", 2, 34, 78, "Bombers", "fire"), "Bombers blow up: pop them in a crowd, never at the walls", bomb: true),
            Asc(R("totem", "totem", 4, 24, 84, "Totems", "fire", 0.3), "Totems shield their friends: burn them down first", totem: 4),
            R("boss", "golem", 90, 28, 190, "Golem King", null, 0.3, true),
            R("queen", "slime", 70, 30, 190, "Slime Queen", null, 0, true, 6),
            Asc(R("lord", "beetle", 60, 40, 170, "Swarmlord", null, 0, true), "The Swarmlord calls runners as it walks", summon: 3.5),
            // the Ascension bosses join the rotation (2nd, 3rd and 4th Ascension), each first met at wave 20 of that run
            Asc(R("wraith", "phantom", 70, 34, 180, "Wraith King", null, 0, true), "The Wraith King fades: Storm still finds him", blink: true),
            Asc(R("titan", "totem", 110, 22, 200, "Totem Titan", null, 0.3, true), "The Totem Titan shields his escort: burn them first", totem: 3),
            Asc(R("tyrant", "thief", 80, 30, 185, "Thief Tyrant", null, 0, true), "The Thief Tyrant sends thieves for your Resonance", summon: 3, summonRole: "thief"),
        }.ToDictionary(r => r.id);
        static Role Asc(Role r, string tip, bool blink = false, bool thief = false, bool bomb = false, double totem = 0, double summon = 0, string summonRole = null)
        { r.tip = tip; r.blink = blink; r.thief = thief; r.bomb = bomb; r.totem = totem; r.summon = summon; r.summonRole = summonRole; return r; }
        static Role Tip(Role r, string tip, bool chest = false, bool shield = false, double heal = 0, double comboWeak = 0)
        { r.tip = tip; r.chest = chest; r.shield = shield; r.heal = heal; r.comboWeak = comboWeak; return r; }
        public static readonly Dictionary<string, int> VARIANT_FROM = new Dictionary<string, int> { { "elite", 12 }, { "shielded", 14 }, { "splitter", 16 }, { "mender", 19 }, { "ironhide", 26 }, { "sprinter", 33 }, { "bulwark", 37 } };
        public static bool IsBoss(int w) => w % 10 == 0;
        // the newest unlocked Ascension boss takes wave 20, the others follow the first three in the rotation
        static readonly (string role, int asc)[] ASC_BOSSES = { ("wraith", 2), ("titan", 3), ("tyrant", 4) };
        public static string BossOf(int w, int asc = 0)
        {
            var extra = ASC_BOSSES.Where(b => asc >= b.asc).Select(b => b.role).Reverse().ToList();
            var list = new List<string> { "boss" }; list.AddRange(extra.Take(1)); list.Add("queen"); list.Add("lord"); list.AddRange(extra.Skip(1));
            return list[((w / 10) - 1) % list.Count];
        }
        // Ascension enemies: (Ascensions needed, first wave in that run)
        public static readonly Dictionary<string, (int asc, int from)> ASC_ROLES = new Dictionary<string, (int, int)> { { "phantom", (1, 15) }, { "thief", (2, 8) }, { "bomber", (2, 26) }, { "totem", (3, 16) } };

        // the wave's make-up as (role, count): runners from wave 3, armoured from 6, swarm from 12, a boss every 10
        public static List<(string role, int n)> WaveMix(int w, int asc = 0)
        {
            var mix = BaseMix(w, asc); if (IsBoss(w)) return mix;
            foreach (var kv in ASC_ROLES)
            {
                var (a, from) = kv.Value;
                if (asc >= a && w >= from && (w == from || w % 3 == from % 3)) mix.Add((kv.Key, kv.Key == "totem" ? 1 : 2 + (w - from) / 15));
            }
            return mix;
        }
        static List<(string role, int n)> BaseMix(int w, int asc)
        {
            var mix = new List<(string, int)>();
            if (IsBoss(w)) { mix.Add((BossOf(w, asc), 1)); mix.Add(("slime", Math.Min(8, 2 + w / 10))); if (w >= 40) mix.Add(("mender", 2)); return mix; }
            int n = 6 + Math.Min(14, w * 2);
            int runners = w >= 3 ? (int)Math.Floor(n * (w % 3 == 0 ? 0.5 : 0.25)) : 0;
            int armoured = w >= 6 ? Math.Max(1, (int)Math.Floor(n * (w % 4 == 2 ? 0.3 : 0.12))) : 0;
            int swarm = w >= 12 && w % 5 == 2 ? n : 0;
            var v = new List<(string, int)>(); int F(string r) => VARIANT_FROM[r];
            if (w >= F("elite")) v.Add(("elite", w == F("elite") ? 1 : (w % 3 == 0 ? 2 : 1)));
            if (w >= F("shielded")) { int k = Math.Max(2, runners / 2); v.Add(("shielded", k)); runners -= runners / 2; }
            if (w >= F("splitter") && (w % 2 == 0 || w == F("splitter"))) v.Add(("splitter", 2 + (w - 16) / 8));
            if (w >= F("mender") && (w % 3 != 2 || w == F("mender"))) v.Add(("mender", 1 + (w - 19) / 12));
            if (w >= F("ironhide") && w % 2 == 0) { int k = Math.Max(1, armoured / 2); v.Add(("ironhide", k)); armoured = Math.Max(0, armoured - k); }
            if (w >= F("sprinter") && w % 2 == 1) { int k = Math.Max(2, runners / 2); v.Add(("sprinter", k)); runners -= runners / 2; }
            if (w >= F("bulwark") && (w % 3 == 1 || w == F("bulwark"))) v.Add(("bulwark", 1 + (w - 37) / 15));
            int slimes = Math.Max(0, n - runners - armoured - v.Sum(x => x.Item2));
            if (slimes > 0) mix.Add(("slime", slimes));
            mix.AddRange(v);
            if (runners > 0) mix.Add(("runner", runners));
            if (armoured > 0) mix.Add(("armoured", armoured));
            if (swarm > 0) mix.Add(("swarm", swarm));
            return mix;
        }
        // ---------- named waves (js/core.js EVENTS): a twist the first time it can happen, then one wave in four brings a
        // known one back. The ones the 3D game's enemies and mechanics can carry today; the rest need their enemies first.
        public class Ev { public string id, name, line; public int from, asc; }
        static Ev E(string id, string name, string line, int from, int asc = 0) => new Ev { id = id, name = name, line = line, from = from, asc = asc };
        public static readonly Ev[] EVENTS =
        {
            E("stampede", "Stampede", "Runners only, and faster", 22), E("golden", "Golden Wave", "Every enemy pays double", 25),
            E("horde", "Horde", "Half again as many enemies", 29), E("thunder", "Thunderstorm", "Storm hits twice as hard", 44),
            E("blizzard", "Blizzard", "Frost hits twice as hard, enemies slow down", 49), E("inferno", "Inferno", "Fire hits twice as hard", 54),
            E("eclipse", "Eclipse", "Enemies are faster, combos pay double", 6, 1), E("harvest", "Harvest Moon", "Golden Wisps every few seconds", 12, 1),
            E("frenzy", "Frenzy", "Everyone moves and fires faster", 32, 1), E("giants", "Giants", "Bigger, tougher enemies that pay double", 18, 1),
            E("rush", "Rush", "The wave comes twice as fast", 27, 1), E("hail", "Hailstorm", "Frost and Storm hit 50% harder", 11, 2),
            E("winter", "Deep Winter", "Every enemy arrives frozen: SHATTER them", 29, 2), E("swarmnight", "Swarm Night", "A sky full of Swarm", 42, 2),
            E("bloodmoon", "Blood Moon", "Tougher enemies that pay triple", 20, 3), E("feast", "Feast Day", "Combos fill the Cheer meter twice as fast", 12, 3),
            E("embers", "Ember Rain", "Every enemy arrives burning", 31, 3), E("stormfront", "Stormfront", "Storm chains reach three more enemies", 38, 3),
            E("goldrush", "Gold Rush", "Golden Wisps every few seconds, paying triple", 31, 4),
        };
        public static Ev EventFor(Profile p, int w)
        {
            if (IsBoss(w) || w < 6) return null;
            var open = EVENTS.Where(e => p.ascensions >= e.asc && w >= e.from).ToList();
            var fresh = open.FirstOrDefault(e => !p.seenEvents.Contains(e.id));
            if (fresh != null) return fresh;
            if (w % 4 != 1 || open.Count == 0) return null;
            return open[(w * 7 + p.ascensions * 3) % open.Count];
        }
        public static List<(string role, int n)> EventMix(List<(string role, int n)> mix, Ev ev)
        {
            if (ev == null) return mix; int n = mix.Sum(x => x.n);
            switch (ev.id)
            {
                case "stampede": return new List<(string, int)> { ("runner", n) };
                case "horde": return mix.Select(x => (x.role, (int)Math.Ceiling(x.n * 1.5))).ToList();
                case "swarmnight": return mix.Concat(new[] { ("swarm", n) }).ToList();
                default: return mix;
            }
        }
        // ---------- the Shop: looks bought with Starshards (js/core.js SHOP). Cosmetic only. Starshards come from the Market
        // opening, bosses, new combos, quests and Ascension; never from Resonance.
        public class Look { public string id, cat, el, name, line; public int price; }
        static Look Lk(string id, string cat, string name, string line, int price, string el = null) => new Look { id = id, cat = cat, el = el, name = name, line = line, price = price };
        public static readonly Look[] SHOP =
        {
            Lk("fire-classic", "tower", "Forge Fire", "The first flame", 0, "fire"), Lk("fire-obsidian", "tower", "Obsidian", "Black glass and magma", 20, "fire"), Lk("fire-ember", "tower", "Ember Gold", "Gilded and white-hot", 20, "fire"),
            Lk("frost-classic", "tower", "Frost Spire", "The first ice", 0, "frost"), Lk("frost-night", "tower", "Glacier Night", "Deep-sea ice, cold light", 20, "frost"), Lk("frost-aurora", "tower", "Aurora", "Ice that holds the northern lights", 20, "frost"),
            Lk("storm-classic", "tower", "Storm Bell", "The first thunder", 0, "storm"), Lk("storm-void", "tower", "Voidcaller", "Night-purple and humming", 20, "storm"), Lk("storm-steel", "tower", "Thunder Steel", "Silver iron, white sparks", 20, "storm"),
            Lk("trail-classic", "trail", "Orbs", "A string of glowing beads", 0), Lk("trail-comet", "trail", "Comet", "One long burning tail", 12), Lk("trail-sparkle", "trail", "Stardust", "Twinkling sparks behind every shot", 12),
            Lk("hit-classic", "hit", "Sparks", "A spray of sparks", 0), Lk("hit-star", "hit", "Starburst", "A bright white flash", 12), Lk("hit-shards", "hit", "Crystals", "Glittering splinters of crystal", 15),
            Lk("wisp-gold", "wisp", "Sunlit Gold", "Wisps like falling coins", 0), Lk("wisp-rose", "wisp", "Rose", "The city's own colour", 8), Lk("wisp-star", "wisp", "Starlight", "White wisps, blue light", 8), Lk("wisp-ruby", "wisp", "Ruby", "Deep red and bright", 8),
            Lk("town-spring", "town", "Spring Day", "The city as it was built", 0), Lk("town-golden", "town", "Golden Hour", "Late sun on every roof", 30), Lk("town-autumn", "town", "Autumn", "Amber light on the fields", 30), Lk("town-winter", "town", "First Snow", "Ice-blue light", 30),
        };
        public static readonly Dictionary<string, int> SHARDS = new Dictionary<string, int> { { "welcome", 10 }, { "boss", 3 }, { "combo", 2 }, { "ascend", 15 }, { "quest", 4 }, { "questBonus", 5 } };
        // the slot a look fills: a tower look is per element
        public static string Slot(Look l) => l.cat == "tower" ? "tower:" + l.el : l.cat;
        public static string Equipped(Profile p, string slot)
        {
            var e = p.equip.Find(x => x.k == slot); if (e != null) return e.s;
            return SHOP.First(x => Slot(x) == slot && x.price == 0).id;
        }

        // ---------- daily quests (js/core.js QUESTS): three a day from the Library once the Shop is open, Starshards each ----------
        public class QuestDef { public string id, text; public int need; }
        public static readonly QuestDef[] QUESTS =
        {
            new QuestDef { id = "kills", text = "Defeat {n} enemies", need = 150 }, new QuestDef { id = "waves", text = "Clear {n} waves", need = 8 },
            new QuestDef { id = "upgrade", text = "Upgrade the towers {n} times", need = 8 }, new QuestDef { id = "combo", text = "Land {n} combos", need = 40 },
            new QuestDef { id = "shatter", text = "Trigger SHATTER {n} times", need = 15 }, new QuestDef { id = "level", text = "Level up buildings {n} times", need = 6 },
            new QuestDef { id = "boss", text = "Defeat a boss", need = 1 }, new QuestDef { id = "surge", text = "Start a Surge", need = 1 },
        };
        static bool QuestOk(Profile p, string id) => id == "combo" ? p.combos.Count > 0 : id == "shatter" ? p.combos.Contains("shatter") : id == "level" ? p.restored.Count > 0 : id == "boss" ? p.bestWave >= 10 : true;
        public static void QuestsFor(Profile p, string today)
        {
            if (p.questDay == today && p.quests.Count > 0) return;
            var pool = QUESTS.Where(q => QuestOk(p, q.id)).ToList(); uint seed = 7; foreach (var c in today) seed = seed * 31 + c;
            var pick = new List<QuestDef>();
            for (int i = 0; pick.Count < 3 && i < pool.Count * 3; i++) { var q = pool[(int)((seed + (uint)i * 5) % (uint)pool.Count)]; if (!pick.Contains(q)) pick.Add(q); }
            p.questDay = today; p.questBonus = false; p.quests = pick.Select(q => new QuestState { id = q.id, need = q.need }).ToList();
        }
        public static string QuestText(QuestState q) => QUESTS.First(x => x.id == q.id).text.Replace("{n}", q.need.ToString());
        // ---------- Feats (js/core.js FEATS): a long collection from lifetime stats that survive Ascension, each paying
        // Starshards once. Tier 1 bronze, 2 silver, 3 gold. (Stars, butterflies and the daily streak join when those systems do.)
        public static readonly string[] STATS = { "kills", "bosses", "combos", "shatter", "upgrades", "levels", "surges", "quests" };
        public class Feat { public string id, name, text; public int tier; public double need; public Func<Profile, double> get; }
        public static readonly int[] FEAT_PAY = { 0, 2, 4, 8 };
        public static readonly List<Feat> FEATS = MakeFeats();
        static List<Feat> MakeFeats()
        {
            var L = new List<Feat>();
            void F(string id, string name, int tier, string text, Func<Profile, double> get, double need) => L.Add(new Feat { id = id, name = name, tier = tier, text = text, get = get, need = need });
            foreach (var (n, nm, t) in new[] { (10, "Holdfast", 1), (25, "Stalwart", 1), (50, "Bulwark", 2), (100, "Unbroken", 2), (200, "Everwall", 3) }) F("wave" + n, nm, t, $"Reach wave {n}", p => p.bestWave, n);
            foreach (var (n, nm, t) in new[] { (100, "First Blood", 1), (1000, "Monster Bane", 1), (10000, "Horde Breaker", 2), (100000, "Legend of the Gate", 3) }) F("kills" + n, nm, t, $"Defeat {Fmt(n)} enemies", p => p.Stat("kills"), n);
            foreach (var (n, nm, t) in new[] { (1, "Giant Slayer", 1), (5, "Boss Hunter", 2), (25, "Kingbreaker", 3) }) F("boss" + n, nm, t, n == 1 ? "Defeat a boss" : $"Defeat {n} bosses", p => p.Stat("bosses"), n);
            foreach (var (n, nm, t) in new[] { (10, "Cracked Ice", 1), (100, "Shatterer", 2), (1000, "Glass Storm", 3) }) F("shatter" + n, nm, t, $"Trigger SHATTER {Fmt(n)} times", p => p.Stat("shatter"), n);
            F("allcombos", "Elementalist", 2, "Discover all three combos", p => p.combos.Count, 3);
            foreach (var (n, nm, t) in new[] { (500, "Chain Reaction", 1), (5000, "Combo Master", 2) }) F("combos" + n, nm, t, $"Land {Fmt(n)} combos", p => p.Stat("combos"), n);
            foreach (var (n, nm, t) in new[] { (5, "Rebuilder", 1), (10, "Hearth Restored", 2) }) F("city" + n, nm, t, $"Rebuild {n} buildings in one city", p => p.restored.Count, n);
            foreach (var (n, nm, t) in new[] { (1, "Reborn", 2), (3, "Thrice Risen", 2), (10, "Eternal City", 3) }) F("ascend" + n, nm, t, n == 1 ? "Ascend once" : $"Ascend {n} times", p => p.ascensions, n);
            foreach (var (n, nm, t) in new[] { (5, "First Light", 1), (20, "Stargazer", 2), (40, "Full Sky", 3) }) F("stars" + n, nm, t, $"Light {n} constellation stars", p => p.stars.Count, n);
            F("bestiary", "Monster Scholar", 2, "Meet every kind of enemy", p => p.seenRoles.Count(r => ROLES.ContainsKey(r)), ROLES.Count);
            foreach (var (n, nm, t) in new[] { (25, "Iron Ramparts", 1), (50, "Steel Ramparts", 2), (100, "Starforged Ramparts", 3) }) F("gate" + n, nm, t, $"Upgrade the towers to level {n}", p => p.gateLevel, n);
            foreach (var (n, nm, t) in new[] { (10, "Humming Halls", 1), (25, "Resonant", 2), (50, "Heart of the City", 3) }) F("blvl" + n, nm, t, $"Get one building to level {n}", p => p.restored.Count == 0 ? 0 : p.restored.Max(id => p.LevelAt(id)), n);
            int buyable = SHOP.Count(l => l.price > 0);
            foreach (var (n, nm, t) in new[] { (3, "New Look", 1), (10, "Collector", 2), (buyable, "Grand Wardrobe", 3) }) F("looks" + n, nm, t, n == buyable ? "Buy every look in the Shop" : $"Buy {n} looks", p => p.owned.Count, n);
            foreach (var (n, nm, t) in new[] { (10, "Errand Runner", 1), (50, "Questkeeper", 2) }) F("quests" + n, nm, t, $"Finish {n} daily quests", p => p.Stat("quests"), n);
            foreach (var (n, nm, t) in new[] { (5, "Surge Rider", 1), (25, "Storm of Light", 2) }) F("surges" + n, nm, t, $"Start {n} Surges", p => p.Stat("surges"), n);
            return L;
        }
        public static double FeatProgress(Profile p, Feat f) => Math.Min(f.need, Math.Max(0, f.get(p)));
        // newly earned Feats: marked, paid and returned so the game can celebrate them
        public static List<Feat> FeatsEarned(Profile p)
        {
            var got = FEATS.Where(f => !p.feats.Contains(f.id) && FeatProgress(p, f) >= f.need).ToList();
            foreach (var f in got) { p.feats.Add(f.id); p.shards += FEAT_PAY[f.tier]; }
            return got;
        }
        // the Bestiary: every kind, in the order a player meets them
        public static readonly string[] BESTIARY = { "slime", "runner", "armoured", "swarm", "boss", "elite", "shielded", "splitter", "queen", "mender", "lord", "ironhide", "sprinter", "bulwark",
            "phantom", "thief", "wraith", "bomber", "totem", "titan", "tyrant" };
        public static string About(Role r) => r.tip ?? (r.id == "slime" ? "The common foe: anything beats them" : r.id == "runner" ? "Fast on their feet: Frost slows them"
            : r.id == "armoured" ? "Half damage from most hits: Fire melts armour" : r.id == "swarm" ? "Many and small: Storm chains through them"
            : r.id == "boss" ? "Every few boss waves: a slow giant of stone" : r.id == "queen" ? "Bursts into a brood of slimes when she falls" : r.id == "lord" ? "Calls beetles to its side" : "");
        public static double EnemyBite(int w, string role) =>
            (ROLES[role].boss ? 16 : role == "armoured" ? 7 : role == "swarm" ? 2 : 4) * Math.Pow(1.12, w - 1);
        public static double EnemyHp(int w, string role) => 10 * Math.Pow(1.235, w - 1) * ROLES[role].hp;

        // ---------- idle ----------
        public static double FightRate(Profile p, Bonus b) => 4 * Math.Pow(1.12, p.wave - 1) * 1.2 * (1 + b["res"]) * b.ascend;
        public static (double secs, double res) OfflineEarnings(Profile p, Bonus b, long nowMs)
        {
            if (p.lastSeen <= 0 || nowMs <= p.lastSeen) return (0, 0);
            double secs = Math.Min((OFFLINE_CAP_H + b["offlineH"]) * 3600, (nowMs - p.lastSeen) / 1000.0);
            if (secs < 60) return (secs, 0);
            return (secs, Math.Floor(secs * (FightRate(p, b) + CityRate(p, b)) * (1 + b["offline"])));
        }
    }

    // ---------- the save profile (JsonUtility-friendly: lists, no dictionaries) ----------
    [Serializable] public class KV { public string k; public int v; }
    [Serializable] public class KS { public string k, s; }
    [Serializable] public class QuestState { public string id; public int n, need; public bool claimed; }
    [Serializable]
    public class Profile
    {
        public int v = 3;
        public double res;
        public int wave = 1, bestWave = 1, runWave = 1, gateLevel = 1, ascensions;
        public List<string> restored = new List<string>();
        public List<KV> levels = new List<KV>();
        public List<KV> forks = new List<KV>();   // k = fork id, v = stage
        public List<string> combos = new List<string>();
        public long lastSeen, surgeUntil;
        public int perkPts;
        public List<KV> perks = new List<KV>();
        public List<string> told = new List<string>();   // one-time hints already shown
        public List<string> seenEvents = new List<string>();   // named waves met at least once
        public List<string> seenRoles = new List<string>();    // enemy kinds met (each one's tip shows once)
        public int shards; public bool shopOpen;               // Starshards, and whether the Market has opened the Shop
        public List<string> owned = new List<string>();        // looks bought
        public List<KS> equip = new List<KS>();                // slot -> look
        public string questDay = ""; public bool questBonus; public List<QuestState> quests = new List<QuestState>();   // surgeUntil: the fountain's Surge runs until then, then recharges (GameLoop.Town)
        public List<KV> stats = new List<KV>(); public List<string> feats = new List<string>(), stars = new List<string>();   // lifetime counts and Feats earned (kept by Ascension)
        public int Stat(string k) { var e = stats.Find(x => x.k == k); return e == null ? 0 : e.v; }
        public void AddStat(string k, int n) { var e = stats.Find(x => x.k == k); if (e == null) stats.Add(new KV { k = k, v = n }); else e.v = (int)Math.Min(int.MaxValue, (long)e.v + n); }
        public bool firstSession = true;
        public bool music = true, sfx = true, reduced;

        public int LevelAt(string id) { var e = levels.Find(x => x.k == id); return e == null ? 1 : e.v; }
        public void SetLevel(string id, int L) { var e = levels.Find(x => x.k == id); if (e == null) levels.Add(new KV { k = id, v = L }); else e.v = L; }
        public int PerkAt(string id) { var e = perks.Find(x => x.k == id); return e == null ? 0 : e.v; }
        public void SetPerk(string id, int n) { var e = perks.Find(x => x.k == id); if (e == null) perks.Add(new KV { k = id, v = n }); else e.v = n; }
        public string ForkAt(int stage) { var e = forks.Find(x => x.v == stage); return e?.k; }
        public void SetFork(int stage, string id) { forks.RemoveAll(x => x.v == stage); forks.Add(new KV { k = id, v = stage }); }

        // fill missing fields, clamp numbers, drop unknown buildings; runs on every load
        public void Sanitize()
        {
            if (double.IsNaN(res) || double.IsInfinity(res) || res < 0) res = 0;
            wave = Math.Max(1, wave); bestWave = Math.Max(wave, bestWave); runWave = Math.Max(wave, runWave);
            gateLevel = Math.Max(1, gateLevel); ascensions = Math.Max(0, ascensions);
            restored = (restored ?? new List<string>()).Where(id => HG.BUILDING_IDS.Contains(id)).Distinct().ToList();
            levels = (levels ?? new List<KV>()).Where(e => restored.Contains(e.k) && e.v > 1).ToList();
            forks = (forks ?? new List<KV>()).Where(e => HG.FORKS.ContainsKey(e.v) && HG.FORKS[e.v].Any(f => f.id == e.k) && HG.StageOf(gateLevel) >= e.v).ToList();
            combos = (combos ?? new List<string>()).Distinct().ToList();
            perkPts = Math.Max(0, perkPts); told = (told ?? new List<string>()).Distinct().ToList();
            seenEvents = (seenEvents ?? new List<string>()).Where(id => HG.EVENTS.Any(e => e.id == id)).Distinct().ToList();
            seenRoles = (seenRoles ?? new List<string>()).Distinct().ToList();
            shards = Math.Max(0, shards); owned = (owned ?? new List<string>()).Where(id => HG.SHOP.Any(l => l.id == id)).Distinct().ToList();
            equip = (equip ?? new List<KS>()).Where(e => HG.SHOP.Any(l => l.id == e.s && HG.Slot(l) == e.k)).ToList();
            stats = (stats ?? new List<KV>()).Where(e => HG.STATS.Contains(e.k) && e.v > 0).GroupBy(e => e.k).Select(g => g.First()).ToList();
            stars = (stars ?? new List<string>()).Where(id => HG.STARS.Any(x => x.id == id)).Distinct().ToList();
            foreach (var st in HG.STARS) if (st.asc < ascensions && !stars.Contains(st.id)) stars.Add(st.id);   // a finished run's constellation is whole
            feats = (feats ?? new List<string>()).Where(id => HG.FEATS.Any(f => f.id == id)).Distinct().ToList();
            quests = (quests ?? new List<QuestState>()).Where(q => HG.QUESTS.Any(d => d.id == q.id)).ToList(); questDay = questDay ?? "";
            perks = (perks ?? new List<KV>()).Where(e => HG.PERKS.Any(k => k.id == e.k) && e.v > 0).Select(e => new KV { k = e.k, v = Math.Min(e.v, HG.PERKS.First(k => k.id == e.k).max) }).ToList();
        }
    }
}
