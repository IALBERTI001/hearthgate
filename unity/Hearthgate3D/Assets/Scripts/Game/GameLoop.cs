// The fight and the city, in 3D. Same rules as the 2D game (Core/HGCore.cs): waves walk the road to the gate, four
// towers on the wall shoot, kills pay Resonance, Upgrade raises the gate, Rebuild restores the next building (its element
// joins the towers), restored buildings hum out Resonance, and the gate falling sends the player back one wave.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class GameLoop : MonoBehaviour
    {
        public ArtSet art;
        public static GameLoop I;
        public Profile P; public HG.Bonus B;
        public double gateHp; public string lastCombo; public float comboT;
        public readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Tower> towers = new List<Tower>();
        readonly List<Shot> shots = new List<Shot>();
        readonly List<(string role, float at)> queue = new List<(string, float)>();
        readonly GameObject[] plots = new GameObject[10];
        readonly Transform[] uppers = new Transform[10];   // each building's upper storey and roof, hidden while the wizard is inside
        Transform world, unitsRoot, marker;
        float waveT, saveT, betweenT; bool waveLive; System.Random rnd = new System.Random();
        const string SAVE_KEY = "hearthgate.profile.v1";
        public const float DT = 1f / 60f;

        void Awake()
        {
            I = this; if (art == null) art = Art.Set; else Art.Set = art; Time.fixedDeltaTime = DT; Application.targetFrameRate = 60;
            P = Load(); B = HG.Bonuses(P); gateHp = HG.GateMax(P, B);
            world = new GameObject("World").transform; unitsRoot = new GameObject("Units").transform;
            BuildWorld();
            var off = HG.OfflineEarnings(P, B, Now());
            if (off.res > 0) { P.res += off.res; Hud.Toast($"While you were away: +{HG.Fmt(off.res)}"); }
            ApplyLooks(); if (P.shopOpen) HG.QuestsFor(P, Today());
            StartWave();
            if (P.ascensions == 0 && P.restored.Count == 0 && P.wave <= 2) Guide.Say("intro");
        }

        static long Now() => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        // ---------- the world ----------
        void BuildWorld()
        {
            Art.Spawn(art ? art.ground : null, "ground", Vector3.zero, world);
            Art.Spawn(art ? art.road : null, "road", Vector3.zero, world);
            Art.Spawn(art ? art.wall : null, "wall", Vector3.zero, world);   // the ring, closed round the town
            Art.Spawn(art ? art.gate : null, "gate", new Vector3(0, 0, Field.GATE_Z), world);
            Art.Spawn(art ? art.fountain : null, "fountain", Field.PLAZA, world);
            for (int i = 0; i < 4; i++)
            {
                var at = Field.Tower(i); var tw = new Tower { n = i, x = at.x, z = at.z };
                tw.t = Art.Spawn(art ? art.towerBase : null, "towerBase", new Vector3(tw.x, 0, tw.z), world).transform;
                towers.Add(tw);
            }
            RefreshTowers();
            for (int i = 0; i < HG.BUILDINGS.Length; i++) RefreshPlot(i);
            if (art && art.marker) marker = Art.Spawn(art.marker, "marker", Vector3.zero, world).transform;
            new GameObject("Wizard").AddComponent<Wizard>().Init(art);   // the town walk
            new GameObject("Townsfolk").AddComponent<Townsfolk>().Init(art);   // villagers walking the square
            sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional;
            // a warm sun from the upper left of the screen, in front (art/STYLE.md: "warm golden light from the upper left"):
            // the faces the camera sees are lit, shadows fall back and to the right; a cool sky fill keeps shadows coloured
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(-0.45f, -0.8f, -0.4f));
            sun.color = new Color(1f, 0.9f, 0.74f); sun.intensity = 1.3f; sun.shadows = LightShadows.Soft; sun.shadowStrength = 0.9f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.42f, 0.5f, 0.7f); RenderSettings.ambientEquatorColor = new Color(0.4f, 0.42f, 0.4f); RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.17f);
        }

        // each tower takes one element of those the city has (fire, frost, storm in restore order), like the 2D game
        string[] Elements() => HG.BUILDINGS.Where(b => b.element != null && P.restored.Contains(b.id)).Select(b => b.element).ToArray();
        void RefreshTowers()
        {
            var els = Elements();
            foreach (var tw in towers)
            {
                if (tw.beacon) continue;
                tw.el = els.Length > 0 ? els[tw.n % els.Length] : "none";
                if (tw.top) Destroy(tw.top.gameObject);
                tw.top = Art.Spawn(TopFor(tw.el), "top-" + (tw.el == "none" ? "stone" : tw.el), new Vector3(tw.x, Field.TOWER_TOP, tw.z), world).transform;
            }
            RefreshBeacon();
        }
        void RefreshPlot(int i)
        {
            if (plots[i]) Destroy(plots[i]);
            bool up = P.restored.Contains(HG.BUILDINGS[i].id);
            var prefab = art == null ? null : up ? art.buildings[i] : art.Ruin(i);
            plots[i] = Art.Spawn(prefab, up ? HG.BUILDINGS[i].id : "ruin", Field.Plot(i), world);
            plots[i].transform.rotation = Field.PlotRot(i);   // facing the square
            uppers[i] = null;
            foreach (var t in plots[i].GetComponentsInChildren<Transform>(true)) if (t.name == "upper") uppers[i] = t;
        }

        // ---------- waves ----------
        void StartWave()
        {
            queue.Clear(); waveT = 0; waveLive = true;
            ev = HG.EventFor(P, P.wave); bool evNew = ev != null && !P.seenEvents.Contains(ev.id); if (evNew) P.seenEvents.Add(ev.id);
            var mix = HG.EventMix(HG.WaveMix(P.wave, P.ascensions), ev); var list = new List<string>();
            foreach (var (role, n) in mix) for (int k = 0; k < n; k++) list.Add(role);
            list = list.OrderBy(_ => rnd.Next()).ToList();
            float gap = Mathf.Max(0.35f, 1.4f - P.wave * 0.02f) * (Ev("rush") ? 0.5f : 1f);
            for (int i = 0; i < list.Count; i++) queue.Add((list[i], 0.8f + i * gap));
            Sound.Boss(HG.IsBoss(P.wave)); if (HG.IsBoss(P.wave)) { Sound.Play("boss_horn", 0.5f); Guide.Say("boss"); }
            if (ev != null) { Hud.Banner($"{(evNew ? "NEW! " : "")}{ev.name}: {ev.line}"); Sound.Play(evNew ? "feat" : "surge", evNew ? 0.45f : 0.3f); }
            else Hud.Banner(HG.IsBoss(P.wave) ? $"Wave {P.wave}: {HG.ROLES[HG.BossOf(P.wave, P.ascensions)].label}!" : $"Wave {P.wave}");
        }

        void FixedUpdate()
        {
            float t = Time.time; waveT += DT;
            while (queue.Count > 0 && queue[0].at <= waveT)
            {
                var role = queue[0].role; queue.RemoveAt(0);
                int lane = rnd.Next(4);   // each walks the lane to one tower (owner's sketch)
                var en = new Enemy(role, P.wave, lane, (float)(rnd.NextDouble() * 2 - 1) * Field.LANE, Field.SpawnProgress(lane, Camera.main), unitsRoot) { born = (float)rnd.NextDouble() * 3 };
                EventSpawn(en); enemies.Add(en);
                if (!P.seenRoles.Contains(role)) { P.seenRoles.Add(role); if (en.R.tip != null) Hud.Banner("New: " + en.R.tip); }   // each kind's counter, said once
            }
            TickEnemies(); TickTowers(); TickShots();
            // the city hums
            P.res += HG.CityRate(P, B) * DT * SurgeX;
            if (waveLive && queue.Count == 0 && enemies.Count == 0) { waveLive = false; betweenT = 1.2f; WaveWon(); }
            if (!waveLive && (betweenT -= DT) <= 0) StartWave();
            if ((saveT += DT) > 5) { saveT = 0; Save(); }
            comboT = Mathf.Max(0, comboT - DT);
        }

        void TickEnemies()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                if (e.dying > 0) { e.dying += DT; if (e.dying > 0.5f) { Destroy(e.t.gameObject); enemies.RemoveAt(i); } continue; }
                if (e.R.heal > 0 && e.burn <= 0)
                {   // a Mender heals its friends nearby until it burns
                    foreach (var o in enemies) if (o != e && o.dying <= 0 && o.hp < o.max && (o.Pos - e.Pos).sqrMagnitude < 16f) { o.hp = System.Math.Min(o.max, o.hp + o.max * e.R.heal * DT); if (Random.value < DT * 2) Fx.I.Burst(o.Pos, new Color(0.5f, 0.95f, 0.5f), 1); }
                }
                if (e.R.blink) { e.blinkT = (e.blinkT + DT) % 2.4f; e.faded = e.blinkT > 1.4f; }   // Phantoms: 1.4 s seen, 1 s faded
                if (e.R.totem > 0 && (e.totemT -= DT) <= 0)
                {   // a Totem wards up to three friends near it
                    e.totemT = (float)e.R.totem; Fx.I.Burst(e.Pos + Vector3.up * e.size * 0.6f, new Color(0.75f, 0.9f, 1f), 14);
                    foreach (var o in enemies.Where(o => o != e && o.dying <= 0 && !o.shield && !o.boss && (o.Pos - e.Pos).sqrMagnitude < 25f).Take(3).ToList())
                    { o.shield = true; if (o.shieldT) o.shieldT.gameObject.SetActive(true); Fx.I.Burst(o.Pos, new Color(0.75f, 0.9f, 1f), 6); }
                }
                if (e.R.summon > 0 && e.atGate == false && (e.summonT -= DT) <= 0)
                {   // the Swarmlord calls runners; the Thief Tyrant sends thieves from well up the field so they can be stopped
                    e.summonT = (float)e.R.summon; string sr = e.R.summonRole ?? "runner";
                    for (int k = -1; k <= 1; k += 2)
                    {
                        var en = new Enemy(sr, P.wave, e.lane, Mathf.Clamp(e.x + k * 1.6f, -Field.LANE, Field.LANE), e.z + (e.R.summonRole != null ? 6f : 0.5f), unitsRoot);
                        EventSpawn(en); enemies.Add(en);
                    }
                }
                if (e.burn > 0) { e.burn -= DT; e.hp -= HG.TowerDamage(P, B) * (1 + B["fire"]) * 0.5 * DT * (e.R.weak == "fire" ? 2 : 1); }
                e.chill = Mathf.Max(0, e.chill - DT); e.flash = Mathf.Max(0, e.flash - DT * 6);
                float slow = e.chill > 0 ? (e.R.weak == "frost" ? 0.3f : 0.45f) : 1f;
                if (e.z > e.stop) e.z = Mathf.Max(e.stop, e.z - e.speed * slow * DT);
                else
                {
                    e.atGate = true;
                    if (e.R.thief)
                    {   // a Thief reaching the walls grabs Resonance and is gone
                        double amt = System.Math.Min(P.res * 0.5, (HG.FightRate(P, B) * 1.6 + HG.CityRate(P, B)) * 6); P.res -= amt; e.dying = 0.0001f; e.hp = 0;
                        Fx.I.Pop(e.Pos + Vector3.up * (e.size + 1f), $"Stolen -{HG.Fmt(amt)}", new Color(1f, 0.54f, 0.42f), 34, 1.3f); Sound.Dud(); continue;
                    }
                    if (e.R.bomb)
                    {   // a Bomber reaching the walls blows a piece out of them
                        gateHp -= HG.EnemyBite(P.wave, e.role) * 10; e.dying = 0.0001f; e.hp = 0; Fx.I.Burst(e.Pos, new Color(1f, 0.6f, 0.23f), 26); Fx.I.Shake(0.25f);
                        Fx.I.Pop(e.Pos + Vector3.up * (e.size + 1f), "BOOM!", new Color(1f, 0.6f, 0.23f), 44); Sound.Play("shatter_combo", 0.4f); continue;
                    }
                    gateHp -= HG.EnemyBite(P.wave, e.role) * DT;
                    e.x = Mathf.MoveTowards(e.x, Mathf.Clamp(e.x * 1.6f, -2.2f, 2.2f), e.speed * 0.3f * DT);   // spread round the tower's foot
                }
                if (e.hp <= 0) Kill(e);
            }
            if (B["regen"] > 0) gateHp = System.Math.Min(HG.GateMax(P, B), gateHp + HG.GateMax(P, B) * B["regen"] * DT);   // Iron Walls mend slowly
            if (gateHp <= 0) GateFell();
            else if (P.wave > 2 && gateHp < HG.GateMax(P, B) * 0.6) Guide.Say("gate");
        }

        void Kill(Enemy e)
        {
            e.dying = 0.0001f;
            double pay = HG.KillRes(P.wave, e.boss, B) * SurgeX * (Ev("golden") || Ev("giants") ? 2 : Ev("bloodmoon") ? 3 : 1); SendWisps(e.Pos, pay);   // paid when it reaches the fountain
            Fx.I.Burst(e.Pos, Field.GOLD, e.boss ? 30 : 8); Sound.Play(e.boss ? "ascension" : "golem_death", e.boss ? 0.55f : 0.28f);
            Fx.I.Pop(e.Pos + Vector3.up * e.size, "+" + HG.Fmt(pay), Field.GOLD, e.boss ? 44 : 26);
            Quest("kills");
            if (e.boss) { Fx.I.Shake(0.3f); Hud.Banner($"{e.R.label} falls!"); Quest("boss"); GiveShards(HG.SHARDS["boss"] + (int)B["bossPlus"], e.Pos + Vector3.up * (e.size + 2.5f)); }
            if (B["molten"] > 0 && e.burn > 0 && !e.boss)
            {   // the Molten Forge: a burning enemy bursts and sets its friends alight
                Fx.I.Burst(e.Pos, Field.FIRE, 18); Sound.Play("fire_contact_0", 0.3f);
                foreach (var o in enemies) if (o != e && o.dying <= 0 && (o.Pos - e.Pos).sqrMagnitude < 9f) { o.burn = 2.2f; o.hp -= HG.TowerDamage(P, B) * 0.6; o.flash = 1; }
            }
            if (e.R.chest)
            {   // an Elite's treasure chest
                double amt = HG.FightRate(P, B) * 1.6 * 5 * SurgeX * (1 + B["chestX"]); P.res += amt; Sound.Play("claim", 0.5f);
                Fx.I.Burst(e.Pos, Field.GOLD, 26); Fx.I.Pop(e.Pos + Vector3.up * (e.size + 1.5f), $"Treasure! +{HG.Fmt(amt)}", Field.GOLD, 40, 1.3f);
            }
            if (e.R.bomb)
            {   // a Bomber popped in the field takes its friends with it
                Fx.I.Burst(e.Pos, new Color(1f, 0.6f, 0.23f), 26); Fx.I.Shake(0.15f); Sound.Play("shatter_combo", 0.4f);
                Fx.I.Pop(e.Pos + Vector3.up * (e.size + 0.6f), "BOOM!", new Color(1f, 0.6f, 0.23f), 44);
                foreach (var o in enemies) if (o != e && o.dying <= 0 && (o.Pos - e.Pos).sqrMagnitude < 20f) { o.hp -= HG.TowerDamage(P, B) * 4; o.flash = 1; }
            }
            if (e.R.split > 0) for (int k = 0; k < e.R.split; k++) enemies.Add(new Enemy("swarm", P.wave, e.lane, Mathf.Clamp(e.x + Random.Range(-1.5f, 1.5f), -Field.LANE, Field.LANE), e.z + Random.Range(0, 1.5f), unitsRoot));
        }

        void GateFell()
        {
            foreach (var e in enemies) Destroy(e.t.gameObject);
            enemies.Clear(); queue.Clear(); waveLive = false; betweenT = 2f;
            P.wave = Mathf.Max(1, P.wave - 1); gateHp = HG.GateMax(P, B);
            Hud.Banner("The walls fell. Back a wave: upgrade the towers!"); Guide.Say("fell"); Fx.I.Shake(0.4f); Sound.Play("boss_horn", 0.4f);
        }

        void WaveWon()
        {
            P.wave++; P.bestWave = Mathf.Max(P.bestWave, P.wave); P.runWave = Mathf.Max(P.runWave, P.wave); Quest("waves");
            if (P.restored.Contains("cottages")) gateHp = HG.GateMax(P, B);
            Sound.Play("claim", 0.22f);
            if (B["harvest"] > 0)
            {   // Harvest Stores: every cleared wave pays seconds of income
                double amt = (HG.FightRate(P, B) * 1.6 + HG.CityRate(P, B)) * B["harvest"] * SurgeX; P.res += amt;
                int gi = System.Array.IndexOf(HG.BUILDING_IDS, "granary"); Fx.I.Pop(Field.Plot(gi) + Vector3.up * (Field.PlotHeight(gi) + 2f), $"Harvest +{HG.Fmt(amt)}", Field.GOLD, 32, 1.1f);
            }
            LightStars();
        }

        // ---------- towers and shots ----------
        void TickTowers()
        {
            foreach (var tw in towers)
            {
                if ((tw.cool -= DT) > 0) continue;
                if (tw.beacon) tw.el = BeaconElement();
                var tg = Pick(tw); if (tg == null) continue;
                tw.cool = (float)HG.FireInterval(B) * (0.9f + tw.n * 0.07f) * (cheerT > 0 ? 0.5f : 1f) * (Ev("frenzy") ? 0.7f : 1f) * (tw.beacon && B["beacon"] > 0 ? 0.5f : 1f); tw.flash = 1;   // the Tavern's Cheer: twice as fast
                var go = new GameObject("shot");
                var col = tw.el == "fire" || tw.el == "frost" || tw.el == "storm" ? Field.Of(tw.el) : new Color(0.66f, 0.58f, 0.47f);   // a stone tower throws stone, not a pale streak
                var mr = Art.Shape(PrimitiveType.Sphere, Art.Flat(col * 1.25f));   // a little over 1: a soft halo, not a white blaze
                // fat, glowing shots with long trails: the fight has to read across the screen (fresh-eyes review 2)
                mr.transform.SetParent(go.transform, false); mr.transform.localScale = Vector3.one * (tw.el == "frost" ? 0.8f : tw.el == "storm" ? 0.6f : 0.65f);
                var tr = go.AddComponent<TrailRenderer>(); tr.time = 0.22f; tr.startWidth = 0.45f; tr.endWidth = 0f;
                tr.material = Art.Flat(Color.Lerp(col, Color.black, 0.15f)); tr.numCapVertices = 3; StyleTrail(tr, col);
                tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; var smr = mr.GetComponent<Renderer>(); if (smr) smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;   // light casts no dark streak
                go.transform.position = tw.Muzzle; Sound.Play(Sfx(tw.el) + "_launch_0", 0.16f);
                shots.Add(new Shot { beacon = tw.beacon, p = tw.Muzzle, from = tw.Muzzle, target = tg, el = tw.el, speed = tw.el == "storm" ? 80f : tw.el == "frost" ? 26f : 20f, t = go.transform, trail = tr });
            }
        }
        // frost picks someone not yet chilled; fire, storm and stone prefer the chilled (that is how combos happen). A tower
        // shoots down its own side of the gate first (lanes on the same side), so its shots fly out over the field and not
        // across the gatehouse and the town (owner, Nov 14 2026: "the projectiles clipping the town is bad")
        Enemy Pick(Tower tw)
        {
            string el = tw.el; float side = Mathf.Sign(tw.x);
            var all = enemies.Where(e => e.dying <= 0 && e.z < e.spawn - 1.5f).OrderBy(e => e.z).ToList();   // on screen, nearest the wall first
            var live = all.Where(e => Mathf.Sign(Field.Lane(e.lane, 0, 0).x) == side).ToList();
            if (live.Count == 0) live = all;
            if (live.Count == 0) return null;
            if (el == "frost") return live.FirstOrDefault(e => e.chill <= 0.6f) ?? live[0];
            return live.FirstOrDefault(e => e.chill > 0.25f) ?? live[0];
        }
        void TickShots()
        {
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var s = shots[i];
                if (s.target == null || s.target.dying > 0) { Destroy(s.t.gameObject); shots.RemoveAt(i); continue; }
                // a shot flies on an arc from the tower top to its (moving) target: high enough to clear the gatehouse's keep
                // when it has to cross the middle, a gentle lob otherwise; it never passes through a wall or a roof
                var goal = s.target.Pos; float dist = Vector3.Distance(s.from, goal);
                if (s.dur <= 0) s.dur = Mathf.Max(0.12f, dist / s.speed);
                s.k = Mathf.Min(1f, s.k + DT / s.dur);
                bool crosses = !s.beacon && (Mathf.Sign(s.from.x) != Mathf.Sign(goal.x) || Mathf.Abs(goal.x) < Field.GATE_HALF + 1f);   // the beacon fires down from high in the town
                if (crosses)
                {   // across the middle: a curve that swings out in front of the gatehouse, over the road, never over the
                    // keep and the town behind it (a high lob over the keep still read as a shot into the town)
                    var ctrl = new Vector3(0f, Mathf.Max(s.from.y, 5f) + 1.5f, Mathf.Max(s.from.z, goal.z) + 7f);
                    float u = s.k, v = 1 - u; s.p = v * v * s.from + 2 * u * v * ctrl + u * u * goal;
                }
                else
                {
                    float apex = s.el == "storm" ? 0.6f : 1.2f + dist * 0.06f;
                    s.p = Vector3.Lerp(s.from, goal, s.k) + Vector3.up * (4f * apex * s.k * (1 - s.k));
                }
                s.t.position = s.p;
                if (s.k >= 1f) { Hit(s.target, s.el); Destroy(s.t.gameObject); shots.RemoveAt(i); continue; }
            }
        }

        void Hit(Enemy e, string el)
        {
            double d = HG.TowerDamage(P, B) * (1 + B[el]) * (e.boss ? 1 + B["bossDmg"] : 1);
            double cm = (1 + B["combo"]) * (Ev("eclipse") ? 2 : 1);
            d *= (Ev("thunder") && el == "storm") || (Ev("blizzard") && el == "frost") || (Ev("inferno") && el == "fire") ? 2 : (Ev("hail") && (el == "frost" || el == "storm")) ? 1.5 : 1;
            if (e.R.armour > 0 && el != "fire") d *= 1 - e.R.armour;
            if (e.faded && el != "storm") d *= 0.25;   // a faded Phantom: only Storm finds it
            if (e.shield)
            {   // a shield takes most of a hit; Storm breaks it
                if (el == "storm") { e.shield = false; if (e.shieldT) e.shieldT.gameObject.SetActive(false); Fx.I.Burst(e.Pos, Field.STORM, 16); Fx.I.Pop(e.Pos + Vector3.up * (e.size + 0.5f), "SHIELD BROKEN", new Color(0.75f, 0.9f, 1f), 30); Sound.Play("lightning_contact_0", 0.35f); }
                else d *= 0.2;
            }
            if (el == "fire") { if (e.chill > 0) { d *= 3 * cm; Combo("shatter", e, "SHATTER!", Field.FROST); e.bigHit = true; } e.burn = 2.2f; }
            if (el == "frost") { e.chill = B["deepFrost"] > 0 ? 3.3f : 2.2f; if (e.R.weak == "frost") d *= 1.3; }
            if (el == "storm")
            {
                int jumps = (e.chill > 0 ? 4 : 2) + (int)B["chainPlus"] + (Ev("stormfront") ? 3 : 0);
                if (e.chill > 0) { Combo("frostStorm", e, "SUPERCHAIN!", Field.STORM); e.bigHit = true; }
                if (e.burn > 0) Combo("fireStorm", e, "FIRESTORM!", Field.FIRE);
                double cd = d * 0.6 * (e.chill > 0 || e.burn > 0 ? cm : 1) * (e.R.weak == "storm" ? 2 : 1);
                foreach (var o in enemies.Where(x => x != e && x.dying <= 0).OrderBy(x => (x.Pos - e.Pos).sqrMagnitude).Take(jumps).ToList())
                { o.hp -= cd; o.flash = 1; if (e.burn > 0) o.burn = 2; Bolt.Arc(e.Pos, o.Pos); }
            }
            if (e.R.comboWeak > 0 && e.bigHit) { d *= e.R.comboWeak; Fx.I.Pop(e.Pos + Vector3.up * (e.size + 1.2f), "CRACK!", Field.STONE, 30); }   // Bulwarks
            e.bigHit = false;
            e.hp -= d; e.flash = 1; if (el != "none") Sound.Play(Sfx(el) + "_contact_0", 0.2f);
            HitBurst(e.Pos, Field.Of(el), el == "none" ? 4 : 7);
        }
        static string Sfx(string el) => el == "frost" ? "ice" : el == "storm" ? "lightning" : "fire";   // the 2D game's file names
        void Combo(string id, Enemy e, string word, Color c)
        {
            if (!P.combos.Contains(id)) { P.combos.Add(id); Hud.Banner($"New combo: {word.TrimEnd('!')}"); GiveShards(HG.SHARDS["combo"], e.Pos + Vector3.up * 3); }
            Quest("combo"); if (id == "shatter") Quest("shatter");
            lastCombo = word; comboT = 1.2f; FillCheer();
            Fx.I.Pop(e.Pos + Vector3.up * (e.size + 0.6f), word, c, 40, 1.1f); Fx.I.Burst(e.Pos, c, 20);
            Sound.Play(id == "shatter" ? "shatter_combo" : id == "frostStorm" ? "freeze_combo" : "conduction_combo", 0.4f);
        }

        // ---------- the player's actions ----------
        public double UpgradeCost => HG.UpgradeCost(P.gateLevel, B);
        public double RestoreCost => HG.RestoreCost(P.restored.Count);
        public bool CanUpgrade => P.res >= UpgradeCost;
        public string NextBuilding => HG.RestoreOffer(P).FirstOrDefault();
        public bool CanRestore => NextBuilding != null && P.res >= RestoreCost;

        public void Upgrade()
        {
            if (!CanUpgrade) return;
            P.res -= UpgradeCost; double before = HG.GateMax(P, B); P.gateLevel++; B = HG.Bonuses(P); Quest("upgrade");
            gateHp += HG.GateMax(P, B) - before;
            foreach (var tw in towers) Fx.I.Burst(tw.Muzzle, Field.GOLD, 6);
            Sound.Play("tier_up", 0.5f);
            int f = HG.PendingFork(P); if (f > 0) Hud.ShowFork(f);
        }
        public void Fork(int stage, string id) { Sound.Play("claim", 0.45f); P.SetFork(stage, id); B = HG.Bonuses(P); Save(); }
        public void Restore(string id = null)
        {
            id ??= NextBuilding; if (id == null || !CanRestore) return;
            P.res -= RestoreCost; P.restored.Add(id); B = HG.Bonuses(P);
            int i = System.Array.IndexOf(HG.BUILDING_IDS, id); RefreshPlot(i); RefreshTowers();
            Fx.I.Burst(Field.Plot(i) + Vector3.up * 2, Field.GOLD, 30); Fx.I.Shake(0.15f, true); Sound.Play("feat", 0.55f);
            Hud.Banner(P.ascensions >= 1 ? $"{HG.NameOf(P, id)}! {HG.LineOf(P, id)}" : $"{HG.ById(id).name} rebuilt! {HG.ById(id).line}"); Save();
            if (id == "market") OpenShop();
            Guide.Say(id); if (HG.CityWhole(P)) Guide.Say("whole", HG.AscendWave(P).ToString());
        }

        // the town walk: the wizard works on the plot he stands at. A restored building goes up a level (its output grows,
        // doubling at levels 10, 25, 50 and 100); a ruin the city can rebuild now is rebuilt.
        public double LevelCost(int i) => HG.LevelCost(P, HG.BUILDING_IDS[i], B);
        public bool CanWork(int i)
        {
            var id = HG.BUILDING_IDS[i];
            if (P.restored.Contains(id)) return P.res >= LevelCost(i);
            return HG.RestoreOffer(P).Contains(id) && P.res >= RestoreCost;
        }
        // the building stays still when worked on (owner, Nov 13 2026: "There shouldnt be a hop since people can be in the
        // building"): gold sparks rise from its walls and sign instead
        void Glow(int i, int n)
        {
            var s = Field.PlotSize(i);
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * Mathf.PI * 2;
                Fx.I.Burst(Field.World(i, Mathf.Cos(a) * s.x * 0.5f, Mathf.Sin(a) * s.y * 0.5f, 1.2f + (k % 2) * 1.4f), Field.GOLD, Mathf.Max(3, n / 6));
            }
            Fx.I.Burst(Field.SignPoint(i), Field.GOLD, n / 2);
        }
        public void WorkOn(int i)
        {
            var id = HG.BUILDING_IDS[i]; var b = HG.ById(id);
            if (!P.restored.Contains(id)) { if (HG.RestoreOffer(P).Contains(id) && P.res >= RestoreCost) { Restore(id); Glow(i, 30); } return; }
            double cost = LevelCost(i); if (P.res < cost) return;
            P.res -= cost; int L = HG.LevelOf(P, id) + 1; P.SetLevel(id, L); B = HG.Bonuses(P); Quest("level");
            bool big = System.Array.IndexOf(HG.MILESTONES, L) >= 0; Sound.Play(big ? "tier_up" : "level_up", big ? 0.5f : 0.42f);
            Glow(i, big ? 28 : 10);
            Fx.I.Pop(Field.Door(i, 0f, 3.8f), $"Level {L}", Field.GOLD, 34);   // over the door, in view inside and out
            if (System.Array.IndexOf(HG.MILESTONES, L) >= 0) { Hud.Banner($"{b.name} reaches level {L}: its output doubles!"); Fx.I.Shake(0.15f, true); }
            Save();
        }

        void Update()
        {
            var wz = Wizard.I;
            TickTown(Time.deltaTime); TapWorld(); CheckFeats();
            if (NextBuilding == "forge" && CanRestore) Guide.Say("restore");
            if (CanAscend) Guide.Say("ascend");
            if (SurgeReady && SurgeOpen && !Guide.Busy) Guide.Say("surge");
            if (!Hud.Paused && Input.GetKeyDown(KeyCode.X)) TapSurge();
            for (int i = 0; i < plots.Length; i++) if (uppers[i]) uppers[i].gameObject.SetActive(!(wz && wz.Inside == i));
            // the next plot to rebuild, marked in the world: a gold ring and arrow that bob, brighter when affordable
            if (marker)
            {
                var nb = NextBuilding; marker.gameObject.SetActive(nb != null);
                if (nb != null)
                {
                    int i = System.Array.IndexOf(HG.BUILDING_IDS, nb); float t = Time.time;
                    // in the yard before its door (the buildings are rooms now: a ring at their centre would sit inside)
                    marker.position = Field.Door(i, 1.8f, 0.15f + Mathf.Sin(t * 2.4f) * 0.15f);
                    marker.rotation = Quaternion.Euler(0, t * 25f, 0);
                    marker.localScale = Vector3.one * (CanRestore ? 1f + Mathf.Sin(t * 5f) * 0.05f : 0.8f);
                }
            }
            if (!Hud.Paused && Input.GetKeyDown(KeyCode.U)) { if (CanUpgrade) { Sound.Click(); Upgrade(); } else Sound.Dud(); }
            if (!Hud.Paused && Input.GetKeyDown(KeyCode.R))
            {
                if (NextBuilding != null) { if (CanRestore) { Sound.Click(); Restore(); } else Sound.Dud(); }
                else if (CanAscend) { Sound.Click(); Hud.ShowAscend(); } else Sound.Dud();
            }
            foreach (var e in enemies) e.Show(Time.time);
        }

        // ---------- save ----------
        Profile Load()
        {
            Profile p = null;
            try { var s = PlayerPrefs.GetString(SAVE_KEY, ""); if (s.Length > 0) p = JsonUtility.FromJson<Profile>(s); } catch { p = null; }
            p ??= new Profile(); p.Sanitize(); return p;
        }
        public void Save() { P.lastSeen = Now(); P.firstSession = false; PlayerPrefs.SetString(SAVE_KEY, JsonUtility.ToJson(P)); PlayerPrefs.Save(); }
        void OnApplicationPause(bool paused) { if (paused) { FlushWisps(); Save(); } }
        void OnApplicationQuit() { FlushWisps(); Save(); }
    }
}
