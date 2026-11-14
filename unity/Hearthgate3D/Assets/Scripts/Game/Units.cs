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
        public bool boss, atGate; Quaternion yaw = Quaternion.identity;
        public float stop = Field.FRONT + Random.Range(0f, Field.STOP_SPREAD);   // where it halts, a crowd at the tower's foot
        Renderer[] rends; Color[] baseCols;

        public Enemy(string role, int wave, int lane, float x, float z, Transform parent)
        {
            this.role = role; R = HG.ROLES[role]; boss = R.boss; this.lane = lane; this.x = x; this.z = spawn = z;
            hp = max = HG.EnemyHp(wave, role); speed = (float)R.speed * Field.PX; size = Field.Size(R.size) * Random.Range(0.88f, 1.12f);
            // no two in lockstep (fresh-eyes review: "ten clones"); turned a little either way, so a cheek shows to the camera
            yaw = Quaternion.Euler(0, Random.Range(14f, 30f) * (Random.value < 0.5f ? -1 : 1), 0);
            var g = Art.Spawn(Art.Set ? Art.Set.Enemy(R.model) : null, R.model, Field.Lane(lane, z, x), parent);
            t = g.transform; body = t.childCount > 0 ? t.GetChild(0) : t;
            t.localScale = Vector3.one * (size / 1.5f); t.rotation = Field.Facing(lane) * yaw;   // models face Blender -Y; Facing turns them up the lane
            rends = g.GetComponentsInChildren<Renderer>(); baseCols = new Color[rends.Length];
            for (int i = 0; i < rends.Length; i++) { var sm = rends[i].sharedMaterial ?? Art.Mat(Color.gray); rends[i].material = new Material(sm); baseCols[i] = rends[i].material.color; }
        }

        public Vector3 Pos => Field.Lane(lane, z, x, size * 0.5f);

        // the look of the moment: a hop as it walks, a squash on a hit, a frost tint while chilled, a white flash when struck
        public void Show(float time)
        {
            float step = Mathf.Abs(Mathf.Sin((time + born) * (boss ? 3f : 7f)));
            float sq = 1f + flash * 0.15f;
            body.localScale = new Vector3(sq, (1f + step * 0.08f) / sq, sq);
            t.position = Field.Lane(lane, z, x, dying > 0 ? -dying * 2f : step * 0.12f * size);
            t.rotation = Quaternion.RotateTowards(t.rotation, Field.Facing(lane) * (atGate ? Quaternion.identity : yaw), 240f * Time.deltaTime);
            for (int i = 0; i < rends.Length; i++)
            {
                var c = baseCols[i];
                if (chill > 0) c = Color.Lerp(c, Field.FROST, 0.55f);
                if (burn > 0) c = Color.Lerp(c, Field.FIRE, 0.25f * (0.5f + 0.5f * Mathf.Sin(time * 20)));
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
        public Vector3 p, from; public Enemy target; public string el; public float speed, k, dur; public Transform t; public TrailRenderer trail; public bool beacon;   // k: 0..1 along the arc
    }
}
