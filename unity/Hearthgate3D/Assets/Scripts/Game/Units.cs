// What walks and what shoots. Plain classes driven by GameLoop each fixed step (fixed timestep: the same result at any
// refresh rate, crazygames-qa). Each holds the scene object that shows it.
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public class Enemy
    {
        public string role; public HG.Role R; public Transform t; public Transform body;
        public double hp, max; public float speed, size, x, z, chill, burn, flash, hop, born, dying;
        public bool boss, atGate;
        Renderer[] rends; Color[] baseCols;

        public Enemy(string role, int wave, float x, float z, Transform parent)
        {
            this.role = role; R = HG.ROLES[role]; boss = R.boss; this.x = x; this.z = z;
            hp = max = HG.EnemyHp(wave, role); speed = (float)R.speed * Field.PX; size = Field.Size(R.size);
            var g = Art.Spawn(Art.Set ? Art.Set.Enemy(R.model) : null, R.model, new Vector3(x, 0, z), parent);
            t = g.transform; body = t.childCount > 0 ? t.GetChild(0) : t;
            t.localScale = Vector3.one * (size / 1.5f); t.rotation = Quaternion.identity;   // models face Blender -Y = Unity -Z, toward the gate
            rends = g.GetComponentsInChildren<Renderer>(); baseCols = new Color[rends.Length];
            for (int i = 0; i < rends.Length; i++) { var sm = rends[i].sharedMaterial ?? Art.Mat(Color.gray); rends[i].material = new Material(sm); baseCols[i] = rends[i].material.color; }
        }

        public Vector3 Pos => new Vector3(x, size * 0.5f, z);

        // the look of the moment: a hop as it walks, a squash on a hit, a frost tint while chilled, a white flash when struck
        public void Show(float time)
        {
            float step = Mathf.Abs(Mathf.Sin((time + born) * (boss ? 3f : 7f)));
            float sq = 1f + flash * 0.15f;
            body.localScale = new Vector3(sq, (1f + step * 0.08f) / sq, sq);
            t.position = new Vector3(x, dying > 0 ? -dying * 2f : step * 0.12f * size, z);
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
        public int n; public string el = "none"; public Transform t, top; public float cool, x, z;
        public Vector3 Muzzle => new Vector3(x, Field.TOWER_TOP + 0.8f, z);
    }

    public class Shot
    {
        public Vector3 p; public Enemy target; public string el; public float speed; public Transform t; public TrailRenderer trail;
    }
}
