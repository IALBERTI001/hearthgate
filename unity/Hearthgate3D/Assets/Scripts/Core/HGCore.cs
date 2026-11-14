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
                perks = p.perks.Select(x => new KV { k = x.k, v = x.v }).ToList(), perkPts = p.perkPts + PerkPtsFor(p), told = new List<string>(p.told), seenEvents = new List<string>(p.seenEvents), seenRoles = new List<string>(p.seenRoles)
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
        public class Role { public string id, model, label, weak, tip; public double hp, speed, size, armour, heal, comboWeak; public bool boss, chest, shield; public int split; }
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
            R("boss", "golem", 90, 28, 190, "Golem King", null, 0.3, true),
            R("queen", "slime", 70, 30, 190, "Slime Queen", null, 0, true, 6),
            R("lord", "beetle", 60, 40, 170, "Swarmlord", null, 0, true),
        }.ToDictionary(r => r.id);
        static Role Tip(Role r, string tip, bool chest = false, bool shield = false, double heal = 0, double comboWeak = 0)
        { r.tip = tip; r.chest = chest; r.shield = shield; r.heal = heal; r.comboWeak = comboWeak; return r; }
        public static readonly Dictionary<string, int> VARIANT_FROM = new Dictionary<string, int> { { "elite", 12 }, { "shielded", 14 }, { "splitter", 16 }, { "mender", 19 }, { "ironhide", 26 }, { "sprinter", 33 }, { "bulwark", 37 } };
        public static bool IsBoss(int w) => w % 10 == 0;
        public static string BossOf(int w) { var list = new[] { "boss", "queen", "lord" }; return list[((w / 10) - 1) % list.Length]; }

        // the wave's make-up as (role, count): runners from wave 3, armoured from 6, swarm from 12, a boss every 10
        public static List<(string role, int n)> WaveMix(int w)
        {
            var mix = new List<(string, int)>();
            if (IsBoss(w)) { mix.Add((BossOf(w), 1)); mix.Add(("slime", Math.Min(8, 2 + w / 10))); return mix; }
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
        public List<string> seenRoles = new List<string>();    // enemy kinds met (each one's tip shows once)   // surgeUntil: the fountain's Surge runs until then, then recharges (GameLoop.Town)
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
            perks = (perks ?? new List<KV>()).Where(e => HG.PERKS.Any(k => k.id == e.k) && e.v > 0).Select(e => new KV { k = e.k, v = Math.Min(e.v, HG.PERKS.First(k => k.id == e.k).max) }).ToList();
        }
    }
}
