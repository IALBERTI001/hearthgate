// The fight and the city, in 3D. Same rules as the 2D game (Core/HGCore.cs): waves walk the road to the gate, four
// towers on the wall shoot, kills pay Resonance, Upgrade raises the gate, Rebuild restores the next building (its element
// joins the towers), restored buildings hum out Resonance, and the gate falling sends the player back one wave.
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public class GameLoop : MonoBehaviour
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
            StartWave();
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
            var sun = new GameObject("Sun").AddComponent<Light>(); sun.type = LightType.Directional;
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
                tw.el = els.Length > 0 ? els[tw.n % els.Length] : "none";
                if (tw.top) Destroy(tw.top.gameObject);
                tw.top = Art.Spawn(art ? art.Top(tw.el) : null, "top-" + (tw.el == "none" ? "stone" : tw.el), new Vector3(tw.x, Field.TOWER_TOP, tw.z), world).transform;
            }
        }
        void RefreshPlot(int i)
        {
            if (plots[i]) Destroy(plots[i]);
            bool up = P.restored.Contains(HG.BUILDINGS[i].id);
            var prefab = art == null ? null : up ? art.buildings[i] : art.Ruin(i);
            plots[i] = Art.Spawn(prefab, up ? HG.BUILDINGS[i].id : "ruin", Field.Plot(i), world);
            plots[i].transform.rotation = Field.PLOT_FACING;
        }

        // ---------- waves ----------
        void StartWave()
        {
            queue.Clear(); waveT = 0; waveLive = true;
            var mix = HG.WaveMix(P.wave); var list = new List<string>();
            foreach (var (role, n) in mix) for (int k = 0; k < n; k++) list.Add(role);
            list = list.OrderBy(_ => rnd.Next()).ToList();
            float gap = Mathf.Max(0.35f, 1.4f - P.wave * 0.02f);
            for (int i = 0; i < list.Count; i++) queue.Add((list[i], 0.8f + i * gap));
            Hud.Banner(HG.IsBoss(P.wave) ? $"Wave {P.wave}: {HG.ROLES[HG.BossOf(P.wave)].label}!" : $"Wave {P.wave}");
        }

        void FixedUpdate()
        {
            float t = Time.time; waveT += DT;
            while (queue.Count > 0 && queue[0].at <= waveT)
            {
                var role = queue[0].role; queue.RemoveAt(0);
                int lane = rnd.Next(4);   // each walks the lane to one tower (owner's sketch)
                enemies.Add(new Enemy(role, P.wave, lane, (float)(rnd.NextDouble() * 2 - 1) * Field.LANE, Field.SpawnProgress(lane, Camera.main), unitsRoot) { born = (float)rnd.NextDouble() * 3 });
            }
            TickEnemies(); TickTowers(); TickShots();
            // the city hums
            P.res += HG.CityRate(P, B) * DT;
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
                if (e.burn > 0) { e.burn -= DT; e.hp -= HG.TowerDamage(P, B) * (1 + B["fire"]) * 0.5 * DT * (e.R.weak == "fire" ? 2 : 1); }
                e.chill = Mathf.Max(0, e.chill - DT); e.flash = Mathf.Max(0, e.flash - DT * 6);
                float slow = e.chill > 0 ? (e.R.weak == "frost" ? 0.3f : 0.45f) : 1f;
                if (e.z > e.stop) e.z = Mathf.Max(e.stop, e.z - e.speed * slow * DT);
                else
                {
                    e.atGate = true; gateHp -= HG.EnemyBite(P.wave, e.role) * DT;
                    e.x = Mathf.MoveTowards(e.x, Mathf.Clamp(e.x * 1.6f, -2.2f, 2.2f), e.speed * 0.3f * DT);   // spread round the tower's foot
                }
                if (e.hp <= 0) Kill(e);
            }
            if (gateHp <= 0) GateFell();
        }

        void Kill(Enemy e)
        {
            e.dying = 0.0001f;
            double pay = HG.KillRes(P.wave, e.boss, B); P.res += pay;
            Fx.I.Burst(e.Pos, Field.GOLD, e.boss ? 30 : 8);
            Fx.I.Pop(e.Pos + Vector3.up * e.size, "+" + HG.Fmt(pay), Field.GOLD, e.boss ? 44 : 26);
            if (e.boss) { Fx.I.Shake(0.3f); Hud.Banner($"{e.R.label} falls!"); }
            if (e.R.split > 0) for (int k = 0; k < e.R.split; k++) enemies.Add(new Enemy("swarm", P.wave, e.lane, Mathf.Clamp(e.x + Random.Range(-1.5f, 1.5f), -Field.LANE, Field.LANE), e.z + Random.Range(0, 1.5f), unitsRoot));
        }

        void GateFell()
        {
            foreach (var e in enemies) Destroy(e.t.gameObject);
            enemies.Clear(); queue.Clear(); waveLive = false; betweenT = 2f;
            P.wave = Mathf.Max(1, P.wave - 1); gateHp = HG.GateMax(P, B);
            Hud.Banner("The gate held you back a wave. Upgrade it!"); Fx.I.Shake(0.4f);
        }

        void WaveWon()
        {
            P.wave++; P.bestWave = Mathf.Max(P.bestWave, P.wave); P.runWave = Mathf.Max(P.runWave, P.wave);
            if (P.restored.Contains("cottages")) gateHp = HG.GateMax(P, B);
        }

        // ---------- towers and shots ----------
        void TickTowers()
        {
            foreach (var tw in towers)
            {
                if ((tw.cool -= DT) > 0) continue;
                var tg = Pick(tw.el); if (tg == null) continue;
                tw.cool = (float)HG.FireInterval(B) * (0.9f + tw.n * 0.07f);
                var go = new GameObject("shot");
                var mr = Art.Shape(PrimitiveType.Sphere, Art.Mat(Field.Of(tw.el), true));
                // fat, glowing shots with long trails: the fight has to read across the screen (fresh-eyes review 2)
                mr.transform.SetParent(go.transform, false); mr.transform.localScale = Vector3.one * (tw.el == "frost" ? 0.8f : tw.el == "storm" ? 0.6f : 0.65f);
                var tr = go.AddComponent<TrailRenderer>(); tr.time = 0.3f; tr.startWidth = 0.55f; tr.endWidth = 0f;
                tr.material = Art.Mat(Field.Of(tw.el), true);
                go.transform.position = tw.Muzzle;
                shots.Add(new Shot { p = tw.Muzzle, target = tg, el = tw.el, speed = tw.el == "storm" ? 80f : tw.el == "frost" ? 26f : 20f, t = go.transform, trail = tr });
            }
        }
        // frost picks someone not yet chilled; fire, storm and stone prefer the chilled (that is how combos happen)
        Enemy Pick(string el)
        {
            var live = enemies.Where(e => e.dying <= 0 && e.z < e.spawn - 1.5f).OrderBy(e => e.z).ToList();   // on screen, nearest the wall first
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
                var to = s.target.Pos - s.p; float step = s.speed * DT;
                if (to.magnitude <= step) { Hit(s.target, s.el); Destroy(s.t.gameObject); shots.RemoveAt(i); continue; }
                s.p += to.normalized * step; s.t.position = s.p;
            }
        }

        void Hit(Enemy e, string el)
        {
            double d = HG.TowerDamage(P, B) * (1 + B[el]) * (e.boss ? 1 + B["bossDmg"] : 1);
            double cm = 1 + B["combo"];
            if (e.R.armour > 0 && el != "fire") d *= 1 - e.R.armour;
            if (el == "fire") { if (e.chill > 0) { d *= 3 * cm; Combo("shatter", e, "SHATTER!", Field.FROST); } e.burn = 2.2f; }
            if (el == "frost") { e.chill = 2.2f; if (e.R.weak == "frost") d *= 1.3; }
            if (el == "storm")
            {
                int jumps = (e.chill > 0 ? 4 : 2) + (int)B["chainPlus"];
                if (e.chill > 0) Combo("frostStorm", e, "SUPERCHAIN!", Field.STORM);
                if (e.burn > 0) Combo("fireStorm", e, "FIRESTORM!", Field.FIRE);
                double cd = d * 0.6 * (e.chill > 0 || e.burn > 0 ? cm : 1) * (e.R.weak == "storm" ? 2 : 1);
                foreach (var o in enemies.Where(x => x != e && x.dying <= 0).OrderBy(x => (x.Pos - e.Pos).sqrMagnitude).Take(jumps).ToList())
                { o.hp -= cd; o.flash = 1; if (e.burn > 0) o.burn = 2; Bolt.Arc(e.Pos, o.Pos); }
            }
            e.hp -= d; e.flash = 1;
            Fx.I.Burst(e.Pos, Field.Of(el), el == "none" ? 4 : 7);
        }
        void Combo(string id, Enemy e, string word, Color c)
        {
            if (!P.combos.Contains(id)) { P.combos.Add(id); Hud.Banner($"New combo: {word.TrimEnd('!')}"); }
            lastCombo = word; comboT = 1.2f;
            Fx.I.Pop(e.Pos + Vector3.up * (e.size + 0.6f), word, c, 40, 1.1f); Fx.I.Burst(e.Pos, c, 20);
            if (id == "shatter") Fx.I.Shake(0.12f);
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
            P.res -= UpgradeCost; double before = HG.GateMax(P, B); P.gateLevel++; B = HG.Bonuses(P);
            gateHp += HG.GateMax(P, B) - before;
            foreach (var tw in towers) Fx.I.Burst(tw.Muzzle, Field.GOLD, 6);
            int f = HG.PendingFork(P); if (f > 0) Hud.ShowFork(f);
        }
        public void Fork(int stage, string id) { P.SetFork(stage, id); B = HG.Bonuses(P); Save(); }
        public void Restore(string id = null)
        {
            id ??= NextBuilding; if (id == null || !CanRestore) return;
            P.res -= RestoreCost; P.restored.Add(id); B = HG.Bonuses(P);
            int i = System.Array.IndexOf(HG.BUILDING_IDS, id); RefreshPlot(i); RefreshTowers();
            Fx.I.Burst(Field.Plot(i) + Vector3.up * 2, Field.GOLD, 30); Fx.I.Shake(0.15f);
            Hud.Banner($"{HG.ById(id).name} rebuilt! {HG.ById(id).line}"); Save();
        }

        void Update()
        {
            // the next plot to rebuild, marked in the world: a gold ring and arrow that bob, brighter when affordable
            if (marker)
            {
                var nb = NextBuilding; marker.gameObject.SetActive(nb != null);
                if (nb != null)
                {
                    int i = System.Array.IndexOf(HG.BUILDING_IDS, nb); float t = Time.time;
                    marker.position = Field.Plot(i) + Vector3.up * (0.15f + Mathf.Sin(t * 2.4f) * 0.15f);
                    marker.rotation = Quaternion.Euler(0, t * 25f, 0);
                    marker.localScale = Vector3.one * (CanRestore ? 1f + Mathf.Sin(t * 5f) * 0.05f : 0.8f);
                }
            }
            if (Input.GetKeyDown(KeyCode.U)) Upgrade();
            if (Input.GetKeyDown(KeyCode.R)) Restore();
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
        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() { Save(); }
    }
}
