// The 3D field: where things stand. One world unit is one metre. The road runs north (+Z) from the spawn to the gate at
// z = 0; the city lies south of the gate. The 2D game's field was 414 stage px deep, so its speeds convert by DEPTH / 414.
using UnityEngine;

namespace Hearthgate.Game
{
    public static class Field
    {
        public const float SPAWN_Z = 40f, GATE_Z = 0f, FRONT_Z = 2.2f, HALF_W = 7f, DEPTH = SPAWN_Z - FRONT_Z;
        public const float LANE = 2.1f;                     // enemies keep to the road (the road model is 5.2 m wide)
        public const float PX = DEPTH / 414f;               // one 2D stage px of travel, in metres
        public static float Size(double px) => (float)px / 66f * 1.5f;   // a slime (66 px) is 1.5 m tall
        public static readonly float[] TOWER_X = { -6f, -2.2f, 2.2f, 6f };
        public const float TOWER_Z = -0.6f, TOWER_TOP = 4.2f;
        public static readonly Vector3 PLAZA = new Vector3(0, 0, -8.5f);

        // the ten building plots round the plaza, in the order of HG.BUILDINGS
        public static Vector3 Plot(int i)
        {
            float a = Mathf.Deg2Rad * (200f + i * 36f);   // a ring, the gap of it facing the gate
            return PLAZA + new Vector3(Mathf.Cos(a) * 11f, 0, Mathf.Sin(a) * 4f);
        }

        public static readonly Color FIRE = new Color(1f, 0.55f, 0.2f), FROST = new Color(0.55f, 0.88f, 1f),
            STORM = new Color(0.78f, 0.55f, 1f), STONE = new Color(0.93f, 0.86f, 0.7f), GOLD = new Color(1f, 0.84f, 0.42f);
        public static Color Of(string el) => el == "fire" ? FIRE : el == "frost" ? FROST : el == "storm" ? STORM : STONE;
    }
}
