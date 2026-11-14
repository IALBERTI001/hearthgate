// The 3D field: where things stand. One world unit is one metre.
//
// Composition (Nov 13 2026, the owner's sketch: "Make it so that monsters are walking up to the towers on a route in front
// of each tower. Close off the loop around town to make a protected town."): the town stands inside a closed ring wall
// (an ellipse, RX x RZ, its front point the gatehouse at the origin). The camera stands outside the front of the ring,
// north of it, looking south. Two element towers stand on the front arc each side of the gatehouse. Four lanes run up the
// screen from below its bottom edge, one to the foot of each tower; the enemies walk their lane and attack the wall there.
//
// Enemies keep three lane coordinates: the lane, progress (metres from the tower's foot, out along the lane) and lateral
// (across it, within LANE). Lane() turns them into a world position. The 2D game's field was 414 stage px deep, so its
// speeds convert by DEPTH / 414 and the wave timing stays about the same.
//
// The numbers come from tools/blender/hg_layout.py (shared with the Blender kit and preview) and tools/layout/ring.py (a
// search over the frame, the HUD, sight lines into the town and footprints); Editor/LayoutCheck measures the same rules
// on the real models and fails the build otherwise.
using UnityEngine;

namespace Hearthgate.Game
{
    public static class Field
    {
        public const float DEPTH = 20f;                       // a typical lane's walk, in metres (the 2D field's 414 px)
        public const float LANE = 1.3f;                       // enemies keep within this of the lane's centre (the path is 3.6 m wide)
        public const float FRONT = 1.0f, STOP_SPREAD = 2.4f;  // where they halt: this far out from the tower's foot, plus up to the spread
        public const float PX = DEPTH / 414f;                 // one 2D stage px of travel, in metres
        public static float Size(double px) => (float)px / 66f * 2.2f;   // a slime (66 px) is 2.2 m tall: the fight reads first (review 2)

        public const float RX = 44f, RZ = 28f;                // the ring wall's half axes; its centre is (0, 0, -RZ)
        public const float GATE_Z = 0f, GATE_HALF = 4f;

        // the four towers on the ring's front arc: 0 and 1 beside the gatehouse, 2 and 3 further out (screen left is +x)
        static readonly Vector2[] TOWERS = { new Vector2(9.000f, -0.592f), new Vector2(-9.000f, -0.592f), new Vector2(26.400f, -5.600f), new Vector2(-26.400f, -5.600f) };
        public const float TOWER_TOP = 5.0f;   // the platform; the gatehouse (keep and roof to 9.7 m) outranks the towers
        public static Vector3 Tower(int i) => new Vector3(TOWERS[i].x, 0, TOWERS[i].y);

        // each lane: from the foot of its tower (on the ring's outward normal) to its start below the frame at 16:9
        static readonly Vector2[] FEET = { new Vector2(9.343f, 1.985f), new Vector2(-9.343f, 1.985f), new Vector2(27.520f, -3.254f), new Vector2(-27.520f, -3.254f) };
        static readonly Vector2[] STARTS = { new Vector2(8.837f, 22.500f), new Vector2(-8.837f, 22.500f), new Vector2(17.683f, 22.500f), new Vector2(-17.683f, 22.500f) };
        public static float LaneLen(int k) => Vector2.Distance(FEET[k], STARTS[k]);
        static Vector2 Dir(int k) => (STARTS[k] - FEET[k]).normalized;            // out along the lane, away from the wall
        // a point on lane k: progress metres out from the tower's foot, lateral across the lane (+ is the lane's left)
        public static Vector3 Lane(int k, float progress, float lateral, float y = 0)
        {
            var d = Dir(k); var p = FEET[k] + d * progress + new Vector2(-d.y, d.x) * lateral;
            return new Vector3(p.x, y, p.y);
        }
        // facing along the lane toward the wall (the models face -Z at no rotation)
        public static Quaternion Facing(int k) { var d = Dir(k); return Quaternion.Euler(0, Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg, 0); }
        // where an enemy enters lane k: just below the bottom edge of the view, however tall the window is
        public static float SpawnProgress(int k, Camera cam)
        {
            float far = LaneLen(k);
            if (cam == null) return far;
            var s = cam.WorldToViewportPoint(Lane(k, far, 0));
            var ray = cam.ViewportPointToRay(new Vector3(s.x, -0.02f, 0));
            if (ray.direction.y >= -0.01f) return far;
            var g = ray.origin + ray.direction * (-ray.origin.y / ray.direction.y);
            var d = Dir(k); float prog = Vector2.Dot(new Vector2(g.x, g.z) - FEET[k], d) + 1.5f;
            return Mathf.Clamp(Mathf.Max(far, prog), far, far + 8f);   // the lane art runs 9 m past its 16:9 start
        }
        public static readonly Vector3 PLAZA = new Vector3(0.000f, 0, -28.000f);   // the ring's centre: the fountain in the square
        public const float SQUARE_A = 29.40f, SQUARE_B = 14.40f;   // the cobbled square, an ellipse round the fountain

        // the ten houses round the inside of the wall, in the order of HG.BUILDINGS, each turned to face the square (owner,
        // Nov 14 2026: "the housing spaced inside circular facing the interior"; tools/blender/hg_layout.py place_ring)
        static readonly Vector2[] PLOTS = {
            new Vector2(-18.732f, -45.510f), new Vector2(-29.560f, -39.542f), new Vector2(-7.655f, -47.531f), new Vector2(3.469f, -48.405f), new Vector2(35.493f, -28.424f),
            new Vector2(-35.774f, -25.577f), new Vector2(-26.190f, -13.406f), new Vector2(16.819f, -46.654f), new Vector2(26.620f, -14.863f), new Vector2(27.906f, -40.231f) };
        static readonly float[] YAWS = { 199.251f, 220.867f, 187.081f, 176.868f, 91.938f, 280.623f, 328.668f, 163.380f, 33.520f, 143.149f };
        public static Vector3 Plot(int i) => new Vector3(PLOTS[i].x, 0, PLOTS[i].y);
        public static Quaternion PlotRot(int i) => Quaternion.Euler(0, YAWS[i], 0);
        // a house's own frame on the ground: u along its width (the model's +x), v toward its door (its front)
        public static Vector2 Right(int i) { float y = YAWS[i] * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(y), -Mathf.Sin(y)); }
        public static Vector2 Front(int i) { float y = YAWS[i] * Mathf.Deg2Rad; return new Vector2(-Mathf.Sin(y), -Mathf.Cos(y)); }
        public static Vector2 Local(int i, Vector3 p) { var d = new Vector2(p.x - PLOTS[i].x, p.z - PLOTS[i].y); return new Vector2(Vector2.Dot(d, Right(i)), Vector2.Dot(d, Front(i))); }
        public static Vector3 World(int i, float u, float v, float y = 0f) { var w = PLOTS[i] + Right(i) * u + Front(i) * v; return new Vector3(w.x, y, w.y); }
        // each building's footprint (across its front x deep) and height, at human scale (tools/blender/hg_layout.py):
        // a room with its doorway centred on the front
        static readonly Vector2[] SIZES = { new Vector2(8.000f, 7.000f), new Vector2(7.000f, 7.000f), new Vector2(6.000f, 6.000f), new Vector2(8.000f, 7.000f), new Vector2(10.000f, 7.000f), new Vector2(10.000f, 8.000f), new Vector2(8.000f, 8.000f), new Vector2(10.000f, 8.000f), new Vector2(10.000f, 6.000f), new Vector2(6.000f, 6.000f) };
        static readonly float[] HEIGHTS = { 7.600f, 14.800f, 12.900f, 10.400f, 6.600f, 10.600f, 8.800f, 9.000f, 6.800f, 13.200f };
        public static Vector2 PlotSize(int i) => SIZES[i];
        public static float PlotHeight(int i) => HEIGHTS[i];
        public static Vector3 Door(int i, float out_ = 0f, float y = 0f) => World(i, 0, SIZES[i].y / 2 + out_, y);
        // the board of a building's sign (hg_city.signpost): in the yard, left of the door as you face the house
        public static Vector3 SignPoint(int i) => World(i, SIZES[i].x / 2 - 0.7f, SIZES[i].y / 2 + 1.5f, 2.0f);
        public const float DOOR_W = 1.2f, WALL_T = 0.3f, FLOOR = 0.34f;   // every door 1.2 x 2.4 m; the room floor on its plinth

        // the town's props the wizard walks round: x, z, radius (lamps, benches, stalls, barrels, trees, beds, bushes)
        public static readonly Vector3[] PROPS = {
            new Vector3(-18.530f, -29.650f, 1.300f), new Vector3(12.095f, -35.280f, 1.300f), new Vector3(18.805f, -28.332f, 1.300f), new Vector3(3.677f, -24.323f, 0.700f),
            new Vector3(-1.346f, -22.977f, 0.700f), new Vector3(-5.023f, -26.654f, 0.700f), new Vector3(-3.677f, -31.677f, 0.700f), new Vector3(1.346f, -33.023f, 0.700f),
            new Vector3(5.023f, -29.346f, 0.700f), new Vector3(29.609f, -25.926f, 0.200f), new Vector3(27.914f, -22.660f, 0.200f), new Vector3(22.220f, -18.030f, 0.200f),
            new Vector3(17.150f, -15.795f, 0.200f), new Vector3(11.201f, -14.185f, 0.200f), new Vector3(4.677f, -13.283f, 0.200f), new Vector3(-10.226f, -13.999f, 0.200f),
            new Vector3(-16.285f, -15.504f, 0.200f), new Vector3(-24.788f, -19.668f, 0.200f), new Vector3(-27.914f, -22.660f, 0.200f), new Vector3(-29.827f, -29.039f, 0.200f),
            new Vector3(-28.594f, -32.356f, 0.200f), new Vector3(-24.190f, -36.758f, 0.200f), new Vector3(-19.616f, -39.245f, 0.200f), new Vector3(-14.037f, -41.156f, 0.200f),
            new Vector3(-4.677f, -42.717f, 0.200f), new Vector3(5.192f, -42.674f, 0.200f), new Vector3(11.683f, -41.716f, 0.200f), new Vector3(17.575f, -40.054f, 0.200f),
            new Vector3(26.641f, -34.764f, 0.200f), new Vector3(29.012f, -31.605f, 0.200f), new Vector3(-14.166f, -42.444f, 0.500f), new Vector3(-24.564f, -38.047f, 0.500f),
            new Vector3(-4.892f, -43.944f, 0.500f), new Vector3(6.524f, -43.832f, 0.500f), new Vector3(31.241f, -23.978f, 0.500f), new Vector3(-31.750f, -30.707f, 0.500f),
            new Vector3(-26.461f, -19.307f, 0.500f), new Vector3(19.538f, -40.729f, 0.500f), new Vector3(20.881f, -15.740f, 0.500f), new Vector3(27.407f, -35.731f, 0.500f),
            new Vector3(38.531f, -21.107f, 1.100f), new Vector3(-34.894f, -36.561f, 1.100f), new Vector3(-36.000f, -17.318f, 1.100f), new Vector3(34.649f, -19.270f, 1.100f),
            new Vector3(33.013f, -35.647f, 1.100f), new Vector3(-38.020f, -32.749f, 1.100f), new Vector3(33.910f, -39.368f, 1.100f), new Vector3(24.069f, -44.844f, 1.100f),
            new Vector3(-31.692f, -17.538f, 1.100f), new Vector3(35.097f, -16.166f, 1.100f), new Vector3(-13.022f, -49.015f, 1.100f), new Vector3(-25.177f, -45.278f, 1.100f),
            new Vector3(36.018f, -36.940f, 1.100f), new Vector3(-33.114f, -34.160f, 1.100f), new Vector3(9.747f, -49.202f, 1.100f), new Vector3(-2.613f, -51.327f, 1.100f),
            new Vector3(30.569f, -21.319f, 0.800f), new Vector3(29.280f, -33.940f, 0.800f), new Vector3(-38.402f, -35.475f, 0.800f), new Vector3(38.634f, -35.504f, 0.800f),
            new Vector3(9.220f, -45.397f, 0.800f), new Vector3(-25.131f, -42.567f, 0.800f), new Vector3(-29.792f, -19.452f, 0.800f), new Vector3(-23.338f, -41.003f, 0.800f),
            new Vector3(24.344f, -47.637f, 0.800f), new Vector3(5.932f, -10.502f, 0.900f), new Vector3(19.192f, -13.319f, 0.900f), new Vector3(-10.425f, -7.332f, 0.900f),
            new Vector3(-14.789f, -10.677f, 0.900f), new Vector3(21.017f, -9.603f, 0.900f), new Vector3(-21.107f, -7.148f, 0.900f), new Vector3(9.518f, -9.925f, 0.900f),
            new Vector3(-5.887f, -10.937f, 0.900f), new Vector3(-17.312f, -11.713f, 0.900f), new Vector3(-17.072f, -7.881f, 0.900f), new Vector3(3.605f, -8.713f, 0.900f),
            new Vector3(12.320f, -8.064f, 0.900f), new Vector3(15.943f, -14.410f, 0.800f), new Vector3(15.806f, -6.143f, 0.800f), new Vector3(5.789f, -5.345f, 0.800f),
            new Vector3(-8.309f, -10.663f, 0.800f), new Vector3(16.420f, -8.363f, 0.800f), new Vector3(22.955f, -7.914f, 0.800f), new Vector3(-2.626f, -46.136f, 0.800f),
            new Vector3(-12.912f, -7.034f, 0.800f), new Vector3(10.300f, -6.346f, 0.800f), new Vector3(16.747f, -11.786f, 0.800f) };
        // the villagers' walks along the paving; a walk that ends where it starts goes round and round
        public static readonly Vector2[][] WALKS = {
            new[] { new Vector2(24.696f, -28.000f), new Vector2(24.077f, -25.437f), new Vector2(22.250f, -23.002f), new Vector2(19.308f, -20.817f), new Vector2(15.398f, -18.993f), new Vector2(10.715f, -17.621f), new Vector2(5.495f, -16.769f), new Vector2(0.000f, -16.480f), new Vector2(-5.495f, -16.769f), new Vector2(-10.715f, -17.621f), new Vector2(-15.398f, -18.993f), new Vector2(-19.308f, -20.817f), new Vector2(-22.250f, -23.002f), new Vector2(-24.077f, -25.437f), new Vector2(-24.696f, -28.000f), new Vector2(-24.077f, -30.563f), new Vector2(-22.250f, -32.998f), new Vector2(-19.308f, -35.183f), new Vector2(-15.398f, -37.007f), new Vector2(-10.715f, -38.379f), new Vector2(-5.495f, -39.231f), new Vector2(-0.000f, -39.520f), new Vector2(5.495f, -39.231f), new Vector2(10.715f, -38.379f), new Vector2(15.398f, -37.007f), new Vector2(19.308f, -35.183f), new Vector2(22.250f, -32.998f), new Vector2(24.077f, -30.563f), new Vector2(24.696f, -28.000f) },
            new[] { new Vector2(12.348f, -28.000f), new Vector2(11.744f, -25.553f), new Vector2(9.990f, -23.345f), new Vector2(7.258f, -21.593f), new Vector2(3.816f, -20.468f), new Vector2(0.000f, -20.080f), new Vector2(-3.816f, -20.468f), new Vector2(-7.258f, -21.593f), new Vector2(-9.990f, -23.345f), new Vector2(-11.744f, -25.553f), new Vector2(-12.348f, -28.000f), new Vector2(-11.744f, -30.447f), new Vector2(-9.990f, -32.655f), new Vector2(-7.258f, -34.407f), new Vector2(-3.816f, -35.532f), new Vector2(-0.000f, -35.920f), new Vector2(3.816f, -35.532f), new Vector2(7.258f, -34.407f), new Vector2(9.990f, -32.655f), new Vector2(11.744f, -30.447f), new Vector2(12.348f, -28.000f) },
            new[] { new Vector2(0.000f, -3.000f), new Vector2(0.000f, -23.500f) },
            new[] { new Vector2(-17.314f, -41.450f), new Vector2(-16.160f, -38.146f), new Vector2(-6.586f, -34.700f) },
            new[] { new Vector2(15.446f, -42.054f), new Vector2(14.416f, -38.605f), new Vector2(5.853f, -34.974f) },
            new[] { new Vector2(-31.056f, -26.462f), new Vector2(-27.518f, -27.126f), new Vector2(-12.285f, -27.203f) },
            new[] { new Vector2(24.522f, -18.031f), new Vector2(22.589f, -20.949f), new Vector2(9.501f, -22.942f) },
            new[] { new Vector2(-7.187f, -43.760f), new Vector2(-6.743f, -40.187f), new Vector2(-2.692f, -35.730f) } };

        // the one steady camera (owner's rule: "one steady art and working area")
        public static readonly Vector3 CAM_LOOK = new Vector3(0.000f, 0, -3.000f);
        public static readonly Vector3 CAM_POS = CAM_LOOK + new Vector3(0, 27.500f, 47.631f);   // 55 m away, 30 degrees down
        public const float CAM_FOV = 40f, CAM_ASPECT = 16f / 9f;
        // on a narrower screen the camera widens its view so the sides of the field never crop
        public static float FovFor(float aspect)
        {
            if (aspect >= CAM_ASPECT) return CAM_FOV;
            float h = Mathf.Atan(Mathf.Tan(CAM_FOV * Mathf.Deg2Rad / 2) * CAM_ASPECT / aspect);
            return h * 2 * Mathf.Rad2Deg;
        }

        public static readonly Color FIRE = new Color(1f, 0.32f, 0.1f), FROST = new Color(0.55f, 0.88f, 1f),
            STORM = new Color(0.78f, 0.55f, 1f), STONE = new Color(0.93f, 0.86f, 0.7f), GOLD = new Color(1f, 0.84f, 0.42f);
        public static Color Of(string el) => el == "fire" ? FIRE : el == "frost" ? FROST : el == "storm" ? STORM : STONE;
    }
}
