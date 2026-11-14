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
        public Vector3 Position => pos;
        public const float SCALE = 0.66f, SPEED = 4.2f, RADIUS = 0.32f, REACH = 2.6f, FOUNTAIN_R = 3.4f;
        // in the square, left of the gatehouse's keep as seen from the walls (LayoutCheck: seen from the wall camera)
        public static readonly Vector3 HOME = new Vector3(-6f, 0, -8f);
        // the walk camera, from his feet: on the gate side looking into the town, the way the wall view looks, so it sees
        // the fronts and doorways (from the town side it looked at the backs and lost him at every door: 159 of 692 spots)
        public static readonly Vector3 FOLLOW = new Vector3(0, 8f, 6.5f), LOOK = new Vector3(0, 1.0f, -2.6f);
        // the walk camera for a spot: behind him on the town side, pulled in and up when that would put it outside the ring
        // wall (from outside, the wall came between it and him near the back of the town)
        public static readonly Vector3 INDOORS = new Vector3(0, 11f, 3.2f);   // steeper over a room, to look past its front wall
        // the way the walk camera looks, on the ground: into the town from the gate at first; at a house it turns to look at
        // that house's front from the square (the houses face the square from all round the ring), and keeps that heading
        // when he walks on
        public static readonly Vector2 START_HEADING = new Vector2(0, -1);
        public static Vector2 HeadingAt(Vector3 p, Vector2 keep)
        {
            int best = -1; float bd = REACH;
            for (int i = 0; i < HG.BUILDING_IDS.Length; i++)
            {
                Rect(i, out float hx, out float hz); var l = Field.Local(i, p);
                float dx = Mathf.Max(0, Mathf.Abs(l.x) - hx), dz = Mathf.Max(0, Mathf.Abs(l.y) - hz), d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < bd) { bd = d; best = i; }
            }
            return best >= 0 ? -Field.Front(best) : keep;
        }
        public static Vector3 FollowFrom(Vector3 p, float indoors = 0f, Vector2? heading = null)
        {
            var off = Vector3.Lerp(FOLLOW, INDOORS, indoors); var h = (heading ?? START_HEADING).normalized;
            for (float k = 1f; k > 0.05f; k -= 0.1f)
            {
                var c = p + new Vector3(-h.x * off.z * k, off.y + (1 - k) * 3f, -h.y * off.z * k);
                float ex = c.x / (Field.RX - 1.2f), ez = (c.z + Field.RZ) / (Field.RZ - 1.2f);
                if (ex * ex + ez * ez < 1) return c;
            }
            return p + new Vector3(-h.x * 0.5f, off.y + 3f, -h.y * 0.5f);
        }

        Vector2 heading = START_HEADING; Transform model; Camera cam; Vector3 pos, vel; Vector3? target; float yaw = 180f, blend, step, idle, lift, indoors;
        GameObject goal;   // a little gold ring where a tap sent him
        int log;

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
            Fx.I.Burst(pos + Vector3.up * 1.0f, Field.GOLD, 14); Sound.Play("surge", 0.35f);
            if (On) Hud.Banner("Walk into a building to work on it");
        }

        // a house's footprint, in its own frame (Field.Local: u across its front, v toward its door); the houses face the
        // square from all round the ring (owner, Nov 14 2026)
        static void Rect(int i, out float hx, out float hz) { var s = Field.PlotSize(i); hx = s.x / 2; hz = s.y / 2; }
        public static bool InFootprint(int i, Vector3 p, float margin)
        {
            Rect(i, out float hx, out float hz); var l = Field.Local(i, p);
            return Mathf.Abs(l.x) < hx + margin && Mathf.Abs(l.y) < hz + margin;
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
            if (Hud.Paused) return;
            if (Input.GetKeyDown(KeyCode.T)) { Sound.Click(); Toggle(); }
            if (Input.GetMouseButtonDown(0) && Debug.isDebugBuild == false && Application.isEditor == false && log < 12)
            {   // QA trace of taps (Nov 13 2026: taps in the browser pane sent him the wrong way); the first dozen only
                log++; Ground(Hud.PointerPos, out var g0);
                Debug.Log($"[Hearthgate] tap {Hud.PointerPos} screen {Screen.width}x{Screen.height} onHud {Hud.OnHud(Hud.PointerPos)} ground {g0} wizard {pos} cam {cam.transform.position}");
            }
            if (Input.GetMouseButtonDown(0) && !Hud.OnHud(Hud.PointerPos))
            {
                if (!On)
                {   // a tap on the wizard from the walls is the same as the Walk button
                    var ray = cam.ScreenPointToRay(Hud.PointerPos); var c = pos + Vector3.up * 0.9f;
                    if (Vector3.Cross(ray.direction, c - ray.origin).magnitude < 1.6f) Toggle();
                }
                else if (Ground(Hud.PointerPos, out var at)) { target = at; goal.SetActive(true); goal.transform.position = at + Vector3.up * 0.05f; }
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
            else if (!On) yaw = Mathf.MoveTowardsAngle(yaw, 180f, 180f * dt);   // at rest by the gate he faces the walls
            float was = step; step += dt * (speed > 0.25f ? 10f : 0f);
            if (Mathf.Floor(step / Mathf.PI) != Mathf.Floor(was / Mathf.PI)) Sound.Step();   // a footfall at each low point of the hop

            // indoors: which room he stands in (the floor is on a 0.34 m plinth: he steps up through the doorway)
            int wasIn = Inside; Inside = -1;
            for (int i = 0; i < HG.BUILDING_IDS.Length; i++) if (InFootprint(i, pos, -Field.WALL_T)) { Inside = i; break; }
            if (Inside >= 0 && wasIn < 0) Sound.Knock();   // the door
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
                    Rect(i, out float hx, out float hz); var l = Field.Local(i, pos);
                    float dx = Mathf.Max(0, Mathf.Abs(l.x) - hx), dz = Mathf.Max(0, Mathf.Abs(l.y) - hz), d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < best) { best = d; Near = i; }
                }
            }
            if (!On) Near = -1;
            if (On && Near >= 0 && Input.GetKeyDown(KeyCode.E)) { if (GameLoop.I.CanWork(Near)) { Sound.Click(); GameLoop.I.WorkOn(Near); } else Sound.Dud(); }

            // the camera: from the walls it glides down into the town and settles behind and above him, looking the way the
            // wall view looks (FollowFrom keeps it inside the ring); high enough to look over a room's walls when he is inside
            blend = Mathf.MoveTowards(blend, On ? 1 : 0, dt / 1.1f);
            float k = Mathf.SmoothStep(0, 1, blend);
            indoors = Mathf.MoveTowards(indoors, Inside >= 0 ? 1 : 0, dt / 0.5f);
            var want = On ? HeadingAt(pos, heading) : START_HEADING;   // the camera turns, gently, to the house he is at
            float ang = Vector2.SignedAngle(heading, want); heading = (Quaternion.Euler(0, 0, Mathf.MoveTowards(0, ang, 120f * dt)) * heading); heading.Normalize();
            var lk = Vector3.Lerp(LOOK, new Vector3(0, 0.6f, -0.8f), indoors);   // ahead along the heading (-z of LOOK is "ahead")
            var followLook = pos + new Vector3(heading.x * -lk.z, lk.y, heading.y * -lk.z); var followPos = FollowFrom(pos, Mathf.SmoothStep(0, 1, indoors), heading);
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
                Rect(i, out float hx, out float hz); var l = Field.Local(i, pos);
                var outside = Field.World(i, 0, hz + 0.9f); var inside = Field.World(i, 0, hz - 1.0f);
                if (Mathf.Abs(l.x) < 0.15f && l.y > hz - 1.2f && l.y < hz + 1.1f) return goalIn ? inside : outside;   // in the doorway: straight through
                return goalIn ? outside : inside;
            }
            return goalAt;
        }

        // inside the ring; out of the fountain, the gatehouse and the town's props; houses stop him at their walls but let
        // him through their doorway (centred on the front wall, 1.2 m wide). The walls are worked in each house's own frame.
        void Collide()
        {
            Push(Field.PLAZA, FOUNTAIN_R);
            foreach (var pr in Field.PROPS) Push(new Vector3(pr.x, 0, pr.y), pr.z + RADIUS);
            if (pos.z > -3.2f && Mathf.Abs(pos.x) < Field.GATE_HALF + 0.8f) pos.z = -3.2f;
            float m = 2.4f, ex = pos.x / (Field.RX - m), ez = (pos.z + Field.RZ) / (Field.RZ - m), e = Mathf.Sqrt(ex * ex + ez * ez);
            if (e > 1) { pos.x /= e; pos.z = (pos.z + Field.RZ) / e - Field.RZ; vel *= 0.5f; }
            for (int i = 0; i < HG.BUILDING_IDS.Length; i++)
            {
                if (!InFootprint(i, pos, RADIUS + 0.3f)) continue;
                Rect(i, out float hx, out float hz); house = i;
                if (HG.BUILDING_IDS[i] == "market")
                {   // an open hall: only its posts stop him
                    foreach (float fx in new[] { -1f, -1f / 3, 1f / 3, 1f })
                        foreach (float fz in new[] { -1f, 1f }) Push(Field.World(i, fx * (hx - 0.3f), fz * (hz - 0.3f)), 0.15f + RADIUS);
                    continue;
                }
                float t = Field.WALL_T / 2 + RADIUS, door = Field.DOOR_W / 2 - RADIUS + 0.05f, wt = Field.WALL_T / 2;
                Wall(-hx, -hz + wt, hx, -hz + wt, t);   // back
                Wall(-hx + wt, -hz, -hx + wt, hz, t);   // sides
                Wall(hx - wt, -hz, hx - wt, hz, t);
                float fz2 = hz - wt; var l = Field.Local(i, pos);   // the front, either side of the doorway
                if (Mathf.Abs(l.y - fz2) < 0.9f && Mathf.Abs(l.x) < 0.9f)   // keys: eased into the doorway
                {
                    var c = Field.World(i, 0, l.y); pos = Vector3.MoveTowards(pos, new Vector3(c.x, 0, c.z), 2.5f * Time.deltaTime);
                }
                Wall(-hx, fz2, -door - RADIUS, fz2, t);
                Wall(door + RADIUS, fz2, hx, fz2, t);
            }
        }
        int house;
        void Wall(float u0, float v0, float u1, float v1, float r) => Segment(Field.World(house, u0, v0), Field.World(house, u1, v1), r);
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
