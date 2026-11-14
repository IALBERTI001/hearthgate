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
        public static readonly Vector3 PLAZA = new Vector3(0.000f, 0, -10.000f);   // straight behind the gatehouse: the town's square

        // the ten building plots inside the ring, in the order of HG.BUILDINGS (tools/layout/ring.py: every roof line seen
        // over the front wall, the gatehouse and the towers; the tall ones where they hide nothing behind them)
        static readonly Vector2[] PLOTS = {
            new Vector2(-31.000f, -20.160f), new Vector2(-29.000f, -35.840f), new Vector2(2.500f, -35.340f), new Vector2(15.500f, -46.500f), new Vector2(-15.500f, -20.160f),
            new Vector2(18.000f, -32.840f), new Vector2(-15.500f, -46.000f), new Vector2(0.000f, -48.500f), new Vector2(30.000f, -19.660f), new Vector2(34.000f, -35.840f) };
        public static Vector3 Plot(int i) => new Vector3(PLOTS[i].x, 0, PLOTS[i].y);
        // each building's footprint (across its front x deep) and height, at human scale (tools/blender/hg_layout.py):
        // a room with its doorway centred on the front, which faces the gate (+z)
        static readonly Vector2[] SIZES = { new Vector2(8.000f, 7.000f), new Vector2(7.000f, 7.000f), new Vector2(6.000f, 6.000f), new Vector2(8.000f, 7.000f), new Vector2(10.000f, 7.000f), new Vector2(10.000f, 8.000f), new Vector2(8.000f, 8.000f), new Vector2(10.000f, 8.000f), new Vector2(10.000f, 6.000f), new Vector2(6.000f, 6.000f) };
        static readonly float[] HEIGHTS = { 7.600f, 14.800f, 12.900f, 10.400f, 6.600f, 10.600f, 8.800f, 9.000f, 6.800f, 13.200f };
        public static Vector2 PlotSize(int i) => SIZES[i];
        public static float PlotHeight(int i) => HEIGHTS[i];
        // the board of a building's sign (hg_city.signpost): by the yard, right of the door as seen from the gate side
        public static Vector3 SignPoint(int i) => Plot(i) + new Vector3(-(SIZES[i].x / 2 - 0.7f), 2.0f, SIZES[i].y / 2 + 1.5f);
        public const float DOOR_W = 1.2f, WALL_T = 0.3f, FLOOR = 0.34f;   // every door 1.2 x 2.4 m; the room floor on its plinth
        public static readonly Quaternion PLOT_FACING = Quaternion.Euler(0, 180, 0);   // fronts toward the wall and the camera

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
