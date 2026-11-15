// What walks and what shoots. Plain classes driven by GameLoop each fixed step (fixed timestep: the same result at any
// refresh rate, crazygames-qa). Each holds the scene object that shows it.
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public class Enemy
    {
        public string role; public HG.Role R; public Transform t; public Transform body;
        // lane is which tower it walks to; z is progress out along that lane from the tower's foot (spawn down to stop), x is
        // lateral across it; Field.Lane maps them to the world
        public int lane; public double hp, max; public float speed, size, x, z, spawn, chill, burn, flash, hop, born, dying;
        public bool boss, atGate, shield, bigHit, faded, enraged, frozen; public float blinkT = Random.Range(0f, 2.4f), totemT, summonT, stun, stunCd, reshT; Quaternion yaw = Quaternion.identity;
        public Transform shieldT;   // the rings of force (the model's 'upper' part), gone when Storm breaks the shield
        public float stop = Field.FRONT + Random.Range(0f, Field.STOP_SPREAD);   // where it halts, a crowd at the tower's foot
        Renderer[] rends; Color[] baseCols; static readonly Color GHOST = new Color(0.82f, 0.9f, 1f);
        float s0; readonly System.Collections.Generic.List<Transform> legs = new System.Collections.Generic.List<Transform>();
        readonly System.Collections.Generic.List<Quaternion> legRest = new System.Collections.Generic.List<Quaternion>();
        Transform footL, footR, armL, armR; Quaternion rFL, rFR, rAL, rAR; Vector3 pFL, pFR;
        Quaternion baseRot; int lastStrike = -1; readonly float bornAt = Time.time;

        public Enemy(string role, int wave, int lane, float x, float z, Transform parent)
        {
            this.role = role; R = HG.ROLES[role]; boss = R.boss; this.lane = lane; this.x = x; this.z = spawn = z;
            hp = max = HG.EnemyHp(wave, role); speed = (float)R.speed * Field.PX * GameLoop.SPAWN_IN; size = Field.Size(R.size) * Random.Range(0.88f, 1.12f);
            // no two in lockstep (fresh-eyes review: "ten clones"); turned a little either way, so a cheek shows to the camera
            yaw = Quaternion.Euler(0, Random.Range(14f, 30f) * (Random.value < 0.5f ? -1 : 1), 0);
            var g = Art.Spawn(Art.Set ? Art.Set.Enemy(R.model) : null, R.model, Field.Lane(lane, z, x), parent);
            t = g.transform; body = t; s0 = size / 1.5f;
            t.localScale = Vector3.one * s0; baseRot = t.rotation;
            foreach (var c in g.GetComponentsInChildren<Transform>(true))   // the jointed parts (hg_front.Part): beetle legs, golem feet and arms
            {
                if (c.name.StartsWith("leg_") && c.name.Length == 6) { legs.Add(c); legRest.Add(c.localRotation); }
                else if (c.name == "foot_L") { footL = c; rFL = c.localRotation; pFL = c.localPosition; } else if (c.name == "foot_R") { footR = c; rFR = c.localRotation; pFR = c.localPosition; }
                else if (c.name == "arm_L") { armL = c; rAL = c.localRotation; } else if (c.name == "arm_R") { armR = c; rAR = c.localRotation; }
            } t.rotation = Field.Facing(lane) * yaw;   // models face Blender -Y; Facing turns them up the lane
            shield = R.shield; totemT = (float)R.totem; summonT = (float)R.summon; foreach (var c in g.GetComponentsInChildren<Transform>(true)) if (c.name == "upper") shieldT = c;
            rends = g.GetComponentsInChildren<Renderer>(); baseCols = new Color[rends.Length];
            for (int i = 0; i < rends.Length; i++) { var sm = rends[i].sharedMaterial ?? Art.Mat(Color.gray); rends[i].material = new Material(sm); baseCols[i] = rends[i].material.color; }
        }

        public void Grow(float k) { size *= k; s0 *= k; t.localScale *= k; }   // the Giants event
        public Vector3 Pos => Field.Lane(lane, z, x, size * 0.5f);

        // the look of the moment: legs that walk (beetles on alternating tripods, golems stomping foot to foot with their fists
        // swinging, slimes hopping), a squash on a hit, a frost tint while chilled, a white flash when struck
        public void Show(float time)
        {
            bool walking = !atGate && dying <= 0 && stun <= 0;
            float rate = (boss ? 3f : 7f) * (chill > 0 ? 0.5f : 1f), ph = (time + born) * rate;
            bool jointed = legs.Count > 0 || footL;
            bool glides = R.model == "boss_wraith";   // the Wraith King floats: a slow rise and fall, no hop
            float step = jointed ? 0f : glides ? 0.6f + 0.4f * Mathf.Sin(ph * 0.35f) : Mathf.Abs(Mathf.Sin(ph));   // a slime hops; a walker's body rides its legs
            if (legs.Count > 0)
            {   // six legs, two tripods (front and back on one side with the middle on the other) out of step: each sweeps
                // forward lifted, then back planted
                for (int i = 0; i < legs.Count; i++)
                {
                    var n = legs[i].name; float side = n[4] == 'L' ? -1f : 1f; int k = n[5] - '0';
                    float p = ph + ((k + (side < 0 ? 0 : 1)) % 2 == 0 ? 0 : Mathf.PI);
                    float sweep = walking ? 22f * Mathf.Sin(p) : Mathf.Sin(time * 9f + i) * 3f, lift = walking ? 20f * Mathf.Max(0, Mathf.Cos(p)) : 0f;
                    legs[i].localRotation = legRest[i] * Quaternion.Euler(0, sweep * side, lift * side);
                }
                step = walking ? Mathf.Abs(Mathf.Cos(ph)) * 0.25f : 0f;   // the shell rides a little on each tripod
            }
            if (footL && footR)
            {   // a golem stomps: one foot swings forward lifted, the other plants; fists swing against the feet; at the walls
                // both fists pound
                float a = walking ? 26f * Mathf.Sin(ph) : 0f;
                footL.localRotation = rFL * Quaternion.Euler(a, 0, 0); footR.localRotation = rFR * Quaternion.Euler(-a, 0, 0);
                footL.localPosition = pFL + Vector3.up * (walking ? 0.16f * Mathf.Max(0, Mathf.Cos(ph)) : 0f);
                footR.localPosition = pFR + Vector3.up * (walking ? 0.16f * Mathf.Max(0, -Mathf.Cos(ph)) : 0f);
                float pound = atGate && dying <= 0 ? 70f * Mathf.Pow(Mathf.Abs(Mathf.Sin(time * 2.6f + born)), 3f) : 0f;
                if (armL) armL.localRotation = rAL * Quaternion.Euler(-a * 0.8f + pound, 0, 0);
                if (armR) armR.localRotation = rAR * Quaternion.Euler(a * 0.8f + pound, 0, 0);
                step = walking ? Mathf.Abs(Mathf.Sin(ph)) * 0.3f : 0f;   // he rises as the feet pass
            }
            // at the wall it attacks (owner, Nov 14 2026: "Add an attack animation for the monsters to hit the wall"): it winds
            // back, lunges into the wall and recovers, about every half second for the small ones, slower for a boss; dust
            // flies and a thud sounds as it lands (a golem's fists pound as well)
            float atk = 0f;
            if (atGate && dying <= 0 && stun <= 0)
            {
                float a = (time + born) * (boss ? 0.9f : 1.7f), u = a - Mathf.Floor(a);
                atk = u < 0.6f ? -Mathf.SmoothStep(0, 1, u / 0.6f) : u < 0.72f ? Mathf.Lerp(-1f, 1f, (u - 0.6f) / 0.12f) : Mathf.Lerp(1f, 0f, (u - 0.72f) / 0.28f);
                int cyc = Mathf.FloorToInt(a);
                if (u >= 0.72f && cyc != lastStrike)
                {
                    lastStrike = cyc; var front = t.position + baseRot * Vector3.back * (size * 0.6f) + Vector3.up * size * 0.3f;
                    Fx.I.Burst(front, new Color(0.62f, 0.55f, 0.45f), boss ? 10 : 4); Sound.Knock();
                    if (boss) Fx.I.Shake(0.06f);
                }
            }
            bool slime = !jointed && !glides;
            float sq = (1f + flash * 0.15f) * (faded ? 0.92f : 1f);
            float roll = footL && walking ? Mathf.Sin(ph) * 5f : 0f;   // a golem rocks onto the planted foot
            float squash = slime ? 1f - 0.22f * Mathf.Max(0, -atk) + 0.12f * Mathf.Max(0, atk) : 1f;   // a slime gathers itself, then slams
            float grow = Mathf.SmoothStep(0.15f, 1f, Mathf.Clamp01((Time.time - bornAt) / 0.45f));   // stepping out of its rift
            t.localScale = grow * s0 * new Vector3(sq / Mathf.Sqrt(squash), (1f + (jointed ? 0f : step * 0.08f)) * squash / sq, sq / Mathf.Sqrt(squash));
            var lungeAt = baseRot * Vector3.back * (Mathf.Max(0, atk) * 0.35f * size);
            t.position = Field.Lane(lane, z, x, dying > 0 ? -dying * 2f : step * 0.12f * size) + lungeAt;
            baseRot = Quaternion.RotateTowards(baseRot, Field.Facing(lane) * (atGate ? Quaternion.identity : yaw) * Quaternion.Euler(0, 0, roll), 240f * Time.deltaTime);
            t.rotation = baseRot * Quaternion.Euler(slime ? 0f : -atk * 16f, 0, 0);   // leans back to wind up, forward into the blow (models face -Z)
            for (int i = 0; i < rends.Length; i++)
            {
                var c = baseCols[i];
                if (chill > 0) c = Color.Lerp(c, Field.FROST, 0.55f);
                if (burn > 0) c = Color.Lerp(c, Field.FIRE, 0.25f * (0.5f + 0.5f * Mathf.Sin(time * 20)));
                if (faded) c = Color.Lerp(c, GHOST, 0.75f);                          // a Phantom washed out: only Storm finds it
                else if (shield && shieldT == null) c = Color.Lerp(c, GHOST, 0.3f);   // a Totem's ward on a kind without rings
                if (frozen) c = Color.Lerp(new Color(c.grayscale, c.grayscale, c.grayscale), new Color(0.55f, 0.8f, 1f), 0.65f);   // Time Stop: drained of colour, cold
                if (enraged) c = Color.Lerp(c, new Color(1f, 0.16f, 0.08f), 0.22f + 0.14f * Mathf.Sin(time * 7f));   // a boss in a rage burns red
                if (flash > 0) c = Color.Lerp(c, Color.white, flash);
                rends[i].material.color = c;
            }
        }
    }

    public class Tower
    {
        public int n; public string el = "none"; public Transform t, top; public float cool, x, z, flash;   // n is also its lane
        public Vector3? muzzle; public bool beacon;   // the Watchtower's beacon: a fifth tower in the town (GameLoop.Town)
        public Vector3 Muzzle => muzzle ?? new Vector3(x, Field.TOWER_TOP + 0.8f, z);
    }

    public class Shot
    {
        public Vector3 p, from; public Enemy target; public string el; public float speed, k, dur, spin; public Transform t; public TrailRenderer trail; public bool beacon; public Vector3 last;   // k: 0..1 along the arc
    }
}
