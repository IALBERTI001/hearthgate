// The 3D field: where things stand. One world unit is one metre.
//
// Composition (Nov 13 2026, after the owner's "why is that gate so terrible. Two towers on each side of the entrance"):
// the camera stands outside the walls, north of them, looking south at the fortress front. The road runs across the
// screen in front of the wall, from the spawn on the right (world -X) to the gatehouse left of centre (world x = 0), the
// genre's convention (defenders left, enemies from the right). The four element towers stand on the curtain wall over
// the road; the city lies behind the wall (world -Z) and shows over it. From the city side, as first built, anything
// taller than 2.5 m at the gate hid the enemies attacking it.
//
// Enemies keep two road coordinates: progress (how far from the gate, SPAWN_Z down to FRONT_Z) and lateral (across the
// road, within LANE). Road() turns them into a world position. The 2D game's field was 414 stage px deep, so its speeds
// convert by DEPTH / 414 and the wave timing stays the same.
//
// The plot spots, the plaza and the camera came from tools/layout/frame.py (a search over sight lines, overlaps, frame
// and HUD); Editor/LayoutCheck measures the same rules on the real models and fails the build otherwise.
using UnityEngine;

namespace Hearthgate.Game
{
    public static class Field
    {
        public const float SPAWN_Z = 27f, FRONT_Z = 1.2f, DEPTH = SPAWN_Z - FRONT_Z;   // road progress, in metres
        public const float LANE = 2.3f;                       // enemies keep to the road (the road model is 6 m wide)
        public const float ROAD_Z = 5.4f, ROAD_HALF = 3f;     // road centre line and half width, world z
        public const float GATE_Z = 0f, GATE_HALF = 4f, GATE_FRONT = 2.1f, HALF_W = 7f;
        public const float PX = DEPTH / 414f;                 // one 2D stage px of travel, in metres
        public static float Size(double px) => (float)px / 66f * 1.8f;   // a slime (66 px) is 1.8 m tall

        // a point on the road: progress from the gate (world -X), lateral across it (world Z)
        public static Vector3 Road(float progress, float lateral, float y = 0) => new Vector3(-progress, y, ROAD_Z + lateral);
        public static readonly Quaternion WALK = Quaternion.Euler(0, 240, 0);   // toward the gate, three-quarters to the camera
        public static readonly Quaternion BITE = Quaternion.Euler(0, 200, 0);   // at the gate, still showing a cheek

        public static readonly float[] TOWER_X = { -7.5f, -13f, -18.5f, -24f };
        public const float TOWER_Z = 0f, TOWER_TOP = 5.0f;   // the platform; the gatehouse (keep and roof to 9.7 m) outranks the towers
        public static readonly Vector3 PLAZA = new Vector3(7f, 0, -8f);   // beside the gatehouse: its keep would hide a plaza behind it

        // the ten building plots, in the order of HG.BUILDINGS
        // the tall ones (frost spire, storm bell, watchtower) stand where nothing behind them is hidden (tools/layout/frame.py)
        static readonly Vector2[] PLOTS = {
            new Vector2(-4.5f, -9f), new Vector2(13.5f, -6f), new Vector2(-7f, -14.5f), new Vector2(-11.5f, -9.5f),
            new Vector2(-17f, -12f), new Vector2(3.5f, -14.5f), new Vector2(-21f, -19f), new Vector2(-12f, -19f), new Vector2(7.5f, -20.5f), new Vector2(-1.5f, -14.5f) };
        public static Vector3 Plot(int i) => new Vector3(PLOTS[i].x, 0, PLOTS[i].y);
        public static readonly Quaternion PLOT_FACING = Quaternion.Euler(0, 180, 0);   // fronts toward the wall and the camera

        // the one steady camera (owner's rule: "one steady art and working area")
        public static readonly Vector3 CAM_LOOK = new Vector3(-7.5f, 0, -3.5f);
        public static readonly Vector3 CAM_POS = CAM_LOOK + new Vector3(0, 46f * 0.6428f, 46f * 0.7660f);   // 46 m away, 40 degrees down
        public const float CAM_FOV = 38f, CAM_ASPECT = 16f / 9f;
        // on a narrower screen the camera widens its view so the sides of the field never crop
        public static float FovFor(float aspect)
        {
            if (aspect >= CAM_ASPECT) return CAM_FOV;
            float h = Mathf.Atan(Mathf.Tan(CAM_FOV * Mathf.Deg2Rad / 2) * CAM_ASPECT / aspect);
            return h * 2 * Mathf.Rad2Deg;
        }

        public static readonly Color FIRE = new Color(1f, 0.55f, 0.2f), FROST = new Color(0.55f, 0.88f, 1f),
            STORM = new Color(0.78f, 0.55f, 1f), STONE = new Color(0.93f, 0.86f, 0.7f), GOLD = new Color(1f, 0.84f, 0.42f);
        public static Color Of(string el) => el == "fire" ? FIRE : el == "frost" ? FROST : el == "storm" ? STORM : STONE;
    }
}
