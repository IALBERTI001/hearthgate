// The town walk (owner, Nov 13 2026: "Add a button that makes you into the wizard that can walk into the town and upgrade
// the building from inside", then "Wizard is as big as the town ... I want the wizard to be able to walk into the
// buildings inside"). The wizard is 1.8 m to his hat's tip, the scale of every door (1.2 x 2.4 m) and storey (3.2 m).
// He waits in the square, in view from the walls. Walk the town (T, or a tap on him) swings the camera into the town
// behind him; he walks with WASD / arrows / ZQSD or to wherever the player taps. He walks into any building through its
// doorway: the walls stop him everywhere else, he steps up onto its floor, and its upper storey and roof are hidden so the
// room shows. Near a building or inside it, a card offers its next level (or the rebuild of a ruin the city can rebuild
// now), pressed with E or a tap. The fight goes on at the walls while he walks. No physics engine (the WebGL build strips
// it): the ground is a plane and the buildings are wall segments with a gap for the door.
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public class Wizard : MonoBehaviour
    {
        public static Wizard I;
        public bool On;               // walking the town (the camera follows him)
        public int Near = -1;         // the plot he can work on, or -1
        public int Inside = -1;       // the building he is standing in, or -1
        public const float SCALE = 0.66f, SPEED = 4.2f, RADIUS = 0.32f, REACH = 2.6f, FOUNTAIN_R = 3.4f;
        // in the square, left of the gatehouse's keep as seen from the walls (LayoutCheck: seen from the wall camera)
        public static readonly Vector3 HOME = new Vector3(-6f, 0, -8f);
        public static readonly Vector3 FOLLOW = new Vector3(0, 8f, -6.5f), LOOK = new Vector3(0, 1.0f, 2.6f);   // the walk camera, from his feet

        Transform model; Camera cam; Vector3 pos, vel; Vector3? target; float yaw = 180f, blend, step, idle, lift;
        GameObject goal;   // a little gold ring where a tap sent him

        public void Init(ArtSet art)
        {
            I = this; cam = Camera.main; pos = HOME;
            model = Art.Spawn(art ? art.wizard : null, "wizard", pos, transform).transform;
            model.localScale = Vector3.one * SCALE;
            goal = Art.Spawn(art ? art.marker : null, "goal", pos, transform); goal.transform.localScale = Vector3.one * 0.25f; goal.SetActive(false);
        }

        public void Toggle()
        {
            On = !On; target = null; goal.SetActive(false);
            Fx.I.Burst(pos + Vector3.up * 1.0f, Field.GOLD, 14);
            if (On) Hud.Banner("Walk the town: step inside to rebuild and upgrade");
        }

        // a plot's footprint in world space: the building faces +z (Field.PLOT_FACING), so its door is on its +z side
        static void Rect(int i, out Vector3 c, out float hx, out float hz) { c = Field.Plot(i); var s = Field.PlotSize(i); hx = s.x / 2; hz = s.y / 2; }
        public static bool InFootprint(int i, Vector3 p, float margin)
        {
            Rect(i, out var c, out float hx, out float hz);
            return Mathf.Abs(p.x - c.x) < hx + margin && Mathf.Abs(p.z - c.z) < hz + margin;
        }

        bool Ground(Vector2 screen, out Vector3 at)
        {
            var ray = cam.ScreenPointToRay(screen); at = default;
            if (!new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float d)) return false;
            at = ray.GetPoint(d); return true;
        }

        void Update()
        {
            if (cam == null) return;
            float dt = Time.deltaTime; idle += dt;
            if (Input.GetKeyDown(KeyCode.T)) Toggle();
            if (Input.GetMouseButtonDown(0) && !Hud.OnHud(Input.mousePosition))
            {
                if (!On)
                {   // a tap on the wizard from the walls is the same as the Walk button
                    var ray = cam.ScreenPointToRay(Input.mousePosition); var c = pos + Vector3.up * 0.9f;
                    if (Vector3.Cross(ray.direction, c - ray.origin).magnitude < 1.6f) Toggle();
                }
                else if (Ground(Input.mousePosition, out var at)) { target = at; goal.SetActive(true); goal.transform.position = at + Vector3.up * 0.05f; }
            }

            // walking: keys move him relative to the camera, a tap sends him
            Vector3 want = Vector3.zero;
            if (On)
            {
                float h = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.Q) ? 1 : 0);
                float v = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.Z) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
                var fw = cam.transform.forward; fw.y = 0; fw = fw.sqrMagnitude < 0.01f ? Vector3.forward : fw.normalized; var rt = new Vector3(fw.z, 0, -fw.x);
                if (h != 0 || v != 0) { want = (rt * h + fw * v).normalized; target = null; goal.SetActive(false); }
                else if (target.HasValue)
                {
                    var left = target.Value - pos; left.y = 0;
                    var to = Waypoint(target.Value) - pos; to.y = 0;
                    if (left.magnitude < 0.3f) { target = null; goal.SetActive(false); }
                    else want = to.normalized * Mathf.Clamp01(left.magnitude / 1.0f + 0.3f);
                }
            }
            vel = Vector3.MoveTowards(vel, want * SPEED, SPEED * 6f * dt);
            pos += vel * dt; Collide();
            float speed = vel.magnitude;
            if (speed > 0.25f) yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(-vel.x, -vel.z) * Mathf.Rad2Deg, 540f * dt);   // models face -Z
            else if (!On) yaw = Mathf.MoveTowardsAngle(yaw, 180f, 180f * dt);   // at rest in the square he faces the walls
            step += dt * (speed > 0.25f ? 10f : 0f);

            // indoors: which room he stands in (the floor is on a 0.34 m plinth: he steps up through the doorway)
            Inside = -1;
            for (int i = 0; i < HG.BUILDING_IDS.Length; i++) if (InFootprint(i, pos, -Field.WALL_T)) { Inside = i; break; }
            bool onStep = false;
            for (int i = 0; i < HG.BUILDING_IDS.Length && !onStep; i++) onStep = InFootprint(i, pos, 0.2f);
            lift = Mathf.MoveTowards(lift, Inside >= 0 || onStep ? Field.FLOOR : 0f, dt * 2.5f);

            // the look: a hop and a forward lean as he walks, a slow breath when he stands
            float hop = speed > 0.25f ? Mathf.Abs(Mathf.Sin(step)) * 0.09f : 0f, breath = Mathf.Sin(idle * 2f) * 0.015f;
            model.position = pos + Vector3.up * (lift + hop);
            model.rotation = Quaternion.Euler(Mathf.Clamp01(speed / SPEED) * 9f, yaw, Mathf.Sin(step) * 3f * Mathf.Clamp01(speed));
            model.localScale = new Vector3(SCALE, SCALE * (1 + breath - hop * 0.3f), SCALE);
            if (goal.activeSelf) goal.transform.Rotate(0, 90 * dt, 0);

            // the plot he can work on: the room he is in, else the nearest building within reach of its walls
            Near = Inside;
            if (On && Near < 0)
            {
                float best = REACH;
                for (int i = 0; i < HG.BUILDING_IDS.Length; i++)
                {
                    Rect(i, out var c, out float hx, out float hz);
                    float dx = Mathf.Max(0, Mathf.Abs(pos.x - c.x) - hx), dz = Mathf.Max(0, Mathf.Abs(pos.z - c.z) - hz), d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < best) { best = d; Near = i; }
                }
            }
            if (!On) Near = -1;
            if (On && Near >= 0 && Input.GetKeyDown(KeyCode.E)) GameLoop.I.WorkOn(Near);

            // the camera: from the walls it swings over into the town and settles behind him on the town side, looking out
            // toward the walls, so the wall and the towers are never between it and him and the fight shows beyond the wall;
            // high enough to look over a room's walls when he is inside
            blend = Mathf.MoveTowards(blend, On ? 1 : 0, dt / 1.1f);
            float k = Mathf.SmoothStep(0, 1, blend);
            var followLook = pos + LOOK; var followPos = pos + FOLLOW;
            var p = Vector3.Lerp(Field.CAM_POS, followPos, k);
            var r = Quaternion.Slerp(Quaternion.LookRotation(Field.CAM_LOOK - Field.CAM_POS), Quaternion.LookRotation(followLook - followPos), k);
            Fx.I.camHome = p; cam.transform.rotation = r;
            cam.fieldOfView = Mathf.Lerp(Field.FovFor(cam.aspect), Mathf.Max(55f, Field.FovFor(cam.aspect)), k);
        }

        // a tap into (or out of) a building walks him round to its doorway first, then through it
        Vector3 Waypoint(Vector3 goalAt)
        {
            for (int i = 0; i < HG.BUILDING_IDS.Length; i++)
            {
                if (HG.BUILDING_IDS[i] == "market") continue;
                bool goalIn = InFootprint(i, goalAt, 0f), meIn = InFootprint(i, pos, 0f);
                if (goalIn == meIn) continue;
                Rect(i, out var c, out float hx, out float hz);
                var outside = new Vector3(c.x, 0, c.z + hz + 0.9f); var inside = new Vector3(c.x, 0, c.z + hz - 1.0f);
                if (Mathf.Abs(pos.x - c.x) < 0.15f && pos.z > c.z + hz - 1.2f && pos.z < c.z + hz + 1.1f) return goalIn ? inside : outside;   // in the doorway: straight through
                return goalIn ? outside : inside;
            }
            return goalAt;
        }

        // inside the ring; out of the fountain and the gatehouse; buildings stop him at their walls but let him through
        // their doorway (centred on the +z wall, 1.2 m wide)
        void Collide()
        {
            Push(Field.PLAZA, FOUNTAIN_R);
            if (pos.z > -3.2f && Mathf.Abs(pos.x) < Field.GATE_HALF + 0.8f) pos.z = -3.2f;
            float m = 2.4f, ex = pos.x / (Field.RX - m), ez = (pos.z + Field.RZ) / (Field.RZ - m), e = Mathf.Sqrt(ex * ex + ez * ez);
            if (e > 1) { pos.x /= e; pos.z = (pos.z + Field.RZ) / e - Field.RZ; vel *= 0.5f; }
            for (int i = 0; i < HG.BUILDING_IDS.Length; i++)
            {
                Rect(i, out var c, out float hx, out float hz);
                if (!InFootprint(i, pos, RADIUS + 0.3f)) continue;
                if (HG.BUILDING_IDS[i] == "market")
                {   // an open hall: only its posts stop him
                    foreach (float fx in new[] { -1f, -1f / 3, 1f / 3, 1f })
                        foreach (float fz in new[] { -1f, 1f }) Push(new Vector3(c.x + fx * (hx - 0.3f), 0, c.z + fz * (hz - 0.3f)), 0.15f + RADIUS);
                    continue;
                }
                float t = Field.WALL_T / 2 + RADIUS, door = Field.DOOR_W / 2 - RADIUS + 0.05f;
                Segment(new Vector3(c.x - hx, 0, c.z - hz + Field.WALL_T / 2), new Vector3(c.x + hx, 0, c.z - hz + Field.WALL_T / 2), t);   // back
                Segment(new Vector3(c.x - hx + Field.WALL_T / 2, 0, c.z - hz), new Vector3(c.x - hx + Field.WALL_T / 2, 0, c.z + hz), t);   // sides
                Segment(new Vector3(c.x + hx - Field.WALL_T / 2, 0, c.z - hz), new Vector3(c.x + hx - Field.WALL_T / 2, 0, c.z + hz), t);
                float fz2 = c.z + hz - Field.WALL_T / 2;   // the front, either side of the doorway
                if (Mathf.Abs(pos.z - fz2) < 0.9f && Mathf.Abs(pos.x - c.x) < 0.9f) pos.x = Mathf.MoveTowards(pos.x, c.x, 2.5f * Time.deltaTime);   // keys: eased into the doorway
                Segment(new Vector3(c.x - hx, 0, fz2), new Vector3(c.x - door - RADIUS, 0, fz2), t);
                Segment(new Vector3(c.x + door + RADIUS, 0, fz2), new Vector3(c.x + hx, 0, fz2), t);
            }
        }
        void Push(Vector3 c, float r)
        {
            var d = new Vector3(pos.x - c.x, 0, pos.z - c.z); float L = d.magnitude;
            if (L < r) { pos = new Vector3(c.x, 0, c.z) + (L > 0.01f ? d / L : Vector3.forward) * r; vel -= Vector3.Project(vel, d) * 0.9f; }
        }
        void Segment(Vector3 a, Vector3 b, float r)
        {
            var ab = b - a; float t = Mathf.Clamp01(Vector3.Dot(pos - a, ab) / ab.sqrMagnitude);
            Push(a + ab * t, r);
        }
    }
}
