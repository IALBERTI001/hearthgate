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
        public int Looking = -1;      // the room whose centrepiece he stands at and looks at (GameLoop.Halls opens its card)
        public Vector3 Position => pos;
        public const float SCALE = 0.66f, SPEED = 3.4f, RADIUS = 0.32f, REACH = 2.6f, FOUNTAIN_R = 3.4f;
        // in the square, left of the gatehouse's keep as seen from the walls (LayoutCheck: seen from the wall camera)
        public static readonly Vector3 HOME = new Vector3(-6f, 0, -8f);    // beside the gatehouse's shadow, so the walls camera sees him (LayoutCheck)
        // the walk camera, from his feet: on the gate side looking into the town, the way the wall view looks, so it sees
        // the fronts and doorways (from the town side it looked at the backs and lost him at every door: 159 of 692 spots)
        // lower and further than the first 8 m crane shot (owner, Nov 14 2026: "view of the player walking the town should be
        // improved because it is an important impression"): a third-person shot over his shoulder, the street ahead and the
        // rooftops against the sky, not the cobbles under his hat
        public static readonly Vector3 FOLLOW = new Vector3(0, 6.2f, 7.0f), LOOK = new Vector3(0, 1.3f, -1.2f);   // the wizard sits a little below the middle of the screen, above the dock and the walls bar (the bot's first look, Nov 15 2026: he stood under them)
        // the walk camera for a spot: behind him on the town side, pulled in and up when that would put it outside the ring
        // wall (from outside, the wall came between it and him near the back of the town)
        public static readonly Vector3 INDOORS = new Vector3(0, 11f, 3.2f);   // steeper over a room, to look past its front wall
        // the way the walk camera looks, on the ground: into the town from the gate at first; at a house it turns to look at
        // that house's front from the square (the houses face the square from all round the ring), and keeps that heading
        // when he walks on
        public static readonly Vector2 START_HEADING = new Vector2(-HOME.x, Field.PLAZA.z - HOME.z).normalized;   // from his spot to the fountain: the first walk shot looks up the street at the square
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
                var c = p + new Vector3(-h.x * off.z * k, off.y * (0.62f + 0.38f * k), -h.y * off.z * k);   // pulled in, it comes down too: an over-the-shoulder shot, not a crane looking at his hat
                if (Mathf.Abs(c.x) < 11f) c.y += Mathf.Clamp01((c.z + 5f) / 3f) * 2.6f;   // over the gatehouse and its towers when he is just inside the gate
                float ex = c.x / (Field.RX - 1.2f), ez = (c.z + Field.RZ) / (Field.RZ - 1.2f);
                if (ex * ex + ez * ez < 0.9f) return c;   // well inside: right against the ring the wall walk filled the bottom third of the first shot
            }
            return p + new Vector3(-h.x * 0.5f, off.y + 3f, -h.y * 0.5f);
        }

        Vector2 heading = START_HEADING; Transform model; Camera cam; Vector3 pos, vel; Vector3? target; float yaw = 180f, blend, step, idle, lift, indoors;
        GameObject goal;   // a little gold ring where a tap sent him
        // the player's own camera in the town (owner, Nov 14 2026: "Players should be able to rotate the camera when in town.
        // Gives them the ability to watch the fight ... from the ground floor level"): a right or middle drag turns it round
        // him and tilts it, the wheel raises it over the walls or lowers it to the street; C puts it back
        float orbit, tilt; Vector2 lastPtr, baseHeading = START_HEADING; bool dragging, toldCam;
        Gait gait;
        // the wall posts (GameLoop.Posts): climbing a stair, standing at a post, casting; and his mana
        public int Post = -1; int climbPost = -1, lastPost = 0, climbAfter = -1; bool climbUp; float climbS, climbLen, postBlend, castCd, dryT, fullT;
        Vector3[] climbPath; Vector3 wallPos;
        public bool OnWall => Post >= 0 || climbPost >= 0;
        public int StairNear { get; private set; } = -1;   // the stair whose foot he stands at, or -1
        public float Mana = MANA_MAX; public bool Refilling { get; private set; }
        public const float MANA_MAX = 100f, SPELL_COST = 8f, CAST_EVERY = 0.4f, CLIMB_SPEED = 2.6f, MANA_FOUNTAIN = 45f, MANA_TRICKLE = 0.5f;

        public void Init(ArtSet art)
        {
            I = this; cam = Camera.main; pos = HOME;
            model = Art.Spawn(art ? art.wizard : null, "wizard", pos, transform).transform;
            model.localScale = Vector3.one * SCALE;
            gait = model.gameObject.AddComponent<Gait>(); gait.LegLength = 0.76f * SCALE; gait.MaxCadence = 3.0f; gait.Step = Sound.Step;
            goal = Art.Spawn(art ? art.marker : null, "goal", pos, transform); goal.transform.localScale = Vector3.one * 0.25f; goal.SetActive(false);
        }

        // from the radial menu: into the town and off to a building's door (the ring's "Walk there")
        public void WalkTo(int i) { if (!On) Toggle(); var d = Field.Door(i, 1.6f); target = new Vector3(d.x, 0, d.z); goal.SetActive(true); goal.transform.position = target.Value + Vector3.up * 0.05f; }
        public void Teleport(Vector3 p) { Post = -1; climbPost = -1; pos = new Vector3(p.x, 0, p.z); Stop(); }   // test hooks only (GameLoop.Dev)
        public Vector3 Pos => pos;
        public bool Blending => blend > 0.001f;   // the camera still on its way between the walls and the town
        public Vector3 StaffTip => model ? model.position + model.rotation * new Vector3(0.45f * SCALE, 2.4f * SCALE, -0.25f * SCALE) : pos + Vector3.up * 1.6f;
        public void Stop() { target = null; goal.SetActive(false); vel = Vector3.zero; }
        // the keeper under the pointer, if any: the spot in front of them, a step short, where he stands to talk
        int talkTo = int.MinValue;
        bool KeeperAt(Vector2 sp, out Vector3 at, out int who)
        {
            at = default; who = -1; var g = GameLoop.I; if (g == null || cam == null) return false;
            float best = 60f * Screen.height / 720f;
            for (int ki = 0; ki < HG.BUILDING_IDS.Length + GameLoop.SQUARE_KEEPERS.Length; ki++)
            {
                int i = ki >= HG.BUILDING_IDS.Length ? GameLoop.SQUARE_KEEPERS[ki - HG.BUILDING_IDS.Length] : ki;
                if (GameLoop.InSquare(i) ? !g.KeeperHere(i) : (GameLoop.KeeperOf(i) == null || !g.CentreOn(i))) continue;
                var c = GameLoop.TalkAt(i); var s = cam.WorldToScreenPoint(c + Vector3.up * 1.2f); if (s.z <= 0) continue;
                var s2 = cam.WorldToScreenPoint(c + Vector3.up * 3.6f);   // the body or the sign over the head
                float d = Mathf.Min(Vector2.Distance(sp, s), s2.z > 0 ? Vector2.Distance(sp, s2) : 1e9f); if (d >= best) continue;
                var away = new Vector3(pos.x - c.x, 0, pos.z - c.z); if (away.sqrMagnitude < 0.01f) away = Vector3.back;
                at = c + away.normalized * Mathf.Min(1.4f, GameLoop.TalkReach(i) * 0.6f); at.y = 0; best = d; who = i;
            }
            return best < 60f * Screen.height / 720f;
        }

        public void Toggle()
        {
            if (OnWall) { int k = climbPost >= 0 ? climbPost : Post; pos = GameLoop.Posts[k].foot; pos.y = 0; Post = -1; climbPost = -1; }   // T on the wall: he is back in the street
            climbAfter = -1;
            On = !On; target = null; goal.SetActive(false); Guide.Done("walk");
            Fx.I.Burst(pos + Vector3.up * 1.0f, Field.GOLD, 14); Sound.Play("surge", 0.35f);
            if (On && GameLoop.I && !GameLoop.I.P.told.Contains("walkhint")) { GameLoop.I.P.told.Add("walkhint"); Hud.Banner("Click the ground or use WASD to walk. At a door, press E to raise the building"); }   // once (bot round 7)   // at once, on entering (a queued coach line arrived after the bot had left)   // the bot never learned how to move or E
            if (On) toldCam = true;
            if (!On && (Hud.Paused || (GameLoop.I && GameLoop.I.Fallen)))
            {   // sent back by the fall's card: the camera does not glide while a card is up, so it goes at once (it stayed on the wall post)
                blend = 0; postBlend = 0; Fx.I.camHome = Field.CAM_POS; cam.transform.rotation = Quaternion.LookRotation(Field.CAM_LOOK - Field.CAM_POS); cam.fieldOfView = Field.FovFor(cam.aspect);
            }
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
            bool onWall = OnWall;
            if (onWall) WallInput(dt);
            else if (Input.GetMouseButtonDown(0) && !Hud.OnHud(Hud.PointerPos))
            {
                if (!On)
                {   // a tap on the wizard from the walls is the same as the Walk button
                    var ray = cam.ScreenPointToRay(Hud.PointerPos); var c = pos + Vector3.up * 0.9f;
                    if (Vector3.Cross(ray.direction, c - ray.origin).magnitude < 1.6f) Toggle();
                }
                else if (KeeperAt(Hud.PointerPos, out var kat, out int ki0))
                {   // a click on a keeper (or the sign over them) talks to them: at once when he is close, else he walks up and then talks
                    // (the bots clicked Aurelian and his "?" and nothing happened)
                    if (new Vector2(kat.x - pos.x, kat.z - pos.z).magnitude < 7f) { Stop(); GameLoop.I.OpenHall(ki0); }
                    else { target = kat; talkTo = ki0; goal.SetActive(true); goal.transform.position = kat + Vector3.up * 0.05f; }
                }
                else if (StairAt(Hud.PointerPos, out int sk))
                {   // a click on a stair (or its brazier): up it, from its foot
                    if ((GameLoop.Posts[sk].foot - pos).magnitude < 2.4f) StartClimb(sk, true);
                    else { var f = GameLoop.Posts[sk].foot; target = new Vector3(f.x, 0, f.z); climbAfter = sk; talkTo = int.MinValue; goal.SetActive(true); goal.transform.position = target.Value + Vector3.up * 0.05f; }
                }
                else if (Ground(Hud.PointerPos, out var at))
                {   // a tap into a ruin stops him at its door: nothing to do in the rubble, and the camera stays out on the street
                    // (bot round 5: "walked into a ruin and the camera went top-down")
                    var gp = GameLoop.I ? GameLoop.I.P : null;
                    for (int ri = 0; gp != null && ri < HG.BUILDING_IDS.Length; ri++)
                        if (!gp.restored.Contains(HG.BUILDING_IDS[ri]) && InFootprint(ri, at, 0.2f)) { at = Field.Door(ri, 1.3f); at.y = 0; break; }
                    target = at; climbAfter = -1; goal.SetActive(true); goal.transform.position = at + Vector3.up * 0.05f; }
            }

            if (On)
            {
                if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) { dragging = true; lastPtr = Hud.PointerPos; }
                if (dragging && (Input.GetMouseButton(1) || Input.GetMouseButton(2)))
                {
                    var d = Hud.PointerPos - lastPtr; lastPtr = Hud.PointerPos; float px = 720f / Mathf.Max(1, Screen.height);
                    orbit = Mathf.Repeat(orbit + d.x * px * 0.35f + 180f, 360f) - 180f; tilt = Mathf.Clamp(tilt - d.y * px * 0.005f, -1f, 1f);
                }
                else dragging = false;
                if (Input.mouseScrollDelta.y != 0) tilt = Mathf.Clamp(tilt + Input.mouseScrollDelta.y * 0.12f, -1f, 1f);
                if (Input.GetKeyDown(KeyCode.C)) { orbit = 0; tilt = 0; Sound.Click(); }
            }
            else { orbit = Mathf.MoveTowardsAngle(orbit, 0, 180f * dt); tilt = Mathf.MoveTowards(tilt, 0, dt); }

            // walking: keys move him relative to the camera, a tap sends him
            Vector3 want = Vector3.zero;
            if (On && !onWall)
            {
                float h = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.Q) ? 1 : 0);
                float v = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.Z) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
                var fw = cam.transform.forward; fw.y = 0; fw = fw.sqrMagnitude < 0.01f ? Vector3.forward : fw.normalized; var rt = new Vector3(fw.z, 0, -fw.x);
                if (h != 0 || v != 0) { want = (rt * h + fw * v).normalized; target = null; talkTo = int.MinValue; climbAfter = -1; goal.SetActive(false); }
                else if (target.HasValue)
                {
                    var left = target.Value - pos; left.y = 0;
                    var to = Waypoint(target.Value) - pos; to.y = 0;
                    if (left.magnitude < 0.3f) { target = null; goal.SetActive(false); if (climbAfter >= 0) { int ca = climbAfter; climbAfter = -1; StartClimb(ca, true); } else if (talkTo != int.MinValue) { int tk = talkTo; talkTo = int.MinValue; GameLoop.I.OpenHall(tk); } }
                    else want = to.normalized * Mathf.Clamp01(left.magnitude / 1.0f + 0.3f);
                }
            }
            if (!onWall) { vel = Vector3.MoveTowards(vel, want * SPEED, SPEED * 6f * dt); pos += vel * dt; Collide(); } else vel = Vector3.zero;
            if (On && !onWall && want.sqrMagnitude > 0.01f && OnFlight(pos, out int fk, out _) >= 0 && Vector3.Dot(want, GameLoop.Posts[fk].outward) > 0.25f) { StartClimb(fk, true); onWall = true; }   // a stair is walked up, like a stair: walking into it, toward the wall
            float speed = climbPost >= 0 ? CLIMB_SPEED : vel.magnitude;
            if (onWall) { }   // turned by WallInput
            else if (speed > 0.25f) yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(-vel.x, -vel.z) * Mathf.Rad2Deg, 540f * dt);   // models face -Z
            else if (!On) yaw = Mathf.MoveTowardsAngle(yaw, 180f, 180f * dt);   // at rest by the gate he faces the walls
            gait.Tick(dt, speed);   // legs, arms and hat (Gait: the stance foot planted, the swing knee bent, the hips over the stance leg)
            if (!gait.HasLegs) { float was = step; step += dt * (speed > 0.25f ? 10f : 0f); if (Mathf.Floor(step / Mathf.PI) != Mathf.Floor(was / Mathf.PI)) Sound.Step(); }

            // indoors: which room he stands in (the floor is on a 0.34 m plinth: he steps up through the doorway)
            int wasIn = Inside; Inside = -1;
            for (int i = 0; i < HG.BUILDING_IDS.Length; i++) if (InFootprint(i, pos, -Field.WALL_T)) { Inside = i; break; }
            if (Inside >= 0 && wasIn < 0) Sound.Knock();   // the door
            bool onStep = false;
            for (int i = 0; i < HG.BUILDING_IDS.Length && !onStep; i++) onStep = InFootprint(i, pos, 0.2f);
            lift = Mathf.MoveTowards(lift, Inside >= 0 || onStep ? Field.FLOOR : 0f, dt * 2.5f);

            // the look: the hips over the stance leg, a lean into the walk and the body turning against the hips; a slow breath
            // when he stands (a model without legs keeps the old hop)
            float hop = gait.HasLegs ? gait.Drop : speed > 0.25f ? Mathf.Abs(Mathf.Sin(step)) * 0.09f : 0f, breath = Mathf.Sin(idle * 2f) * 0.012f;
            model.position = (onWall ? wallPos : pos + Vector3.up * lift) + Vector3.up * hop;
            model.rotation = Quaternion.Euler(Mathf.Clamp01(speed / SPEED) * 7f, yaw + (gait.HasLegs ? gait.Twist : 0f), gait.HasLegs ? 0f : Mathf.Sin(step) * 3f * Mathf.Clamp01(speed));
            model.localScale = new Vector3(SCALE, SCALE * (1 + breath), SCALE);
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
            // the keeper he talks to (GameLoop.Npcs): in their room (or by the fountain, the Chronomancer), within reach across
            // the centrepiece and turned to them (within 50 degrees)
            Looking = -1;
            if (On)
                for (int ki = 0; ki < HG.BUILDING_IDS.Length + GameLoop.SQUARE_KEEPERS.Length; ki++)
                {
                    int i = ki >= HG.BUILDING_IDS.Length ? GameLoop.SQUARE_KEEPERS[ki - HG.BUILDING_IDS.Length] : ki;
                    if (GameLoop.InSquare(i) ? !GameLoop.I.KeeperHere(i) : (!GameLoop.I.CentreOn(i) || !InFootprint(i, pos, 0.2f))) continue;
                    var c = GameLoop.TalkAt(i); var to = new Vector3(c.x - pos.x, 0, c.z - pos.z); float reach = GameLoop.TalkReach(i);
                    if (to.magnitude < reach && want.sqrMagnitude > 0.01f && Vector3.Angle(want, to) < 70f)   // pressing toward them: he turns to face them squarely
                        yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(-to.x, -to.z) * Mathf.Rad2Deg, 540f * dt);
                    var face = new Vector3(-Mathf.Sin(yaw * Mathf.Deg2Rad), 0, -Mathf.Cos(yaw * Mathf.Deg2Rad));
                    if (to.magnitude < reach && Vector3.Angle(face, to) < 50f) Looking = i;
                }
            // the stair at hand, and its mana: the fountain fills him, anywhere else only a trickle
            StairNear = -1;
            if (On && !onWall) StairNear = StairClose(pos, 2.4f);
            if (onWall) { Near = -1; Inside = -1; Looking = -1; }
            Refilling = On && !onWall && new Vector2(pos.x - Field.PLAZA.x, pos.z - Field.PLAZA.z).magnitude < FOUNTAIN_R + 2.6f;
            float was0 = Mana; Mana = Mathf.Min(MANA_MAX, Mana + (Refilling ? MANA_FOUNTAIN : MANA_TRICKLE) * dt);
            if (Refilling && Mana < MANA_MAX && Random.value < dt * 14f) Fx.I.Burst(StaffTip, new Color(0.55f, 0.8f, 1f), 1);
            if (Refilling && was0 < MANA_MAX && Mana >= MANA_MAX) { Sound.Play("claim", 0.3f); Fx.I.Pop(StaffTip + Vector3.up * 0.6f, "Mana full", new Color(0.6f, 0.85f, 1f), 26); }
            bool usedE = false;
            if (StairNear >= 0 && Input.GetKeyDown(KeyCode.E)) { usedE = true; StartClimb(StairNear, true); }
            int nk = int.MinValue;   // E beside the Chronomancer or the Steward talks to them (bot round 8: "E near Aurelian did nothing")
            if (On && !onWall && !usedE && Input.GetKeyDown(KeyCode.E) && GameLoop.I)
                foreach (int ki in GameLoop.SQUARE_KEEPERS)
                    if (GameLoop.I.KeeperHere(ki) && new Vector2(GameLoop.TalkAt(ki).x - pos.x, GameLoop.TalkAt(ki).z - pos.z).magnitude < 3f) nk = ki;
            if (nk != int.MinValue) { Stop(); GameLoop.I.OpenHall(nk); }
            else if (On && !onWall && !usedE && Near >= 0 && Looking < 0 && Input.GetKeyDown(KeyCode.E)) { if (GameLoop.I.CanWork(Near)) { Sound.Click(); GameLoop.I.WorkOn(Near); } else { Sound.Dud(); GameLoop.I.Need(GameLoop.I.BestCost(Near), Field.Door(Near, 1f, 2.5f)); } }

            // the camera: from the walls it glides down into the town and settles behind and above him, looking the way the
            // wall view looks (FollowFrom keeps it inside the ring); high enough to look over a room's walls when he is inside
            blend = Mathf.MoveTowards(blend, On ? 1 : 0, dt / 1.1f);
            float k = Mathf.SmoothStep(0, 1, blend);
            indoors = Mathf.MoveTowards(indoors, Inside >= 0 ? 1 : 0, dt / 0.5f);
            if (!On) { baseHeading = START_HEADING; stairCam = false; }
            else if (Mathf.Abs(orbit) < 1f)
            {   // by a wall stair the camera turns to look at it from the town side (owner, Nov 16 2026: "I cant" climb: the stairs were
                // behind the camera, which went top-down against the ring); walking away, it turns back toward the square
                int ns = StairClose(pos, 7f);
                if (ns >= 0 && !onWall) { var o = GameLoop.Posts[ns].outward; baseHeading = new Vector2(o.x, o.z).normalized; stairCam = true; }
                else { if (stairCam && !onWall) { stairCam = false; baseHeading = new Vector2(Field.PLAZA.x - pos.x, Field.PLAZA.z - pos.z).normalized; } baseHeading = HeadingAt(pos, baseHeading); }
            }   // the camera turns, gently, to the house he is at, until the player turns it (then it holds that way until C)
            Vector2 wantH = Quaternion.Euler(0, 0, -orbit) * baseHeading;   // and as far round as the player has turned it (kept apart, or the two fed each other)
            float ang = Vector2.SignedAngle(heading, wantH); heading = (Quaternion.Euler(0, 0, Mathf.MoveTowards(0, ang, (dragging ? 900f : 120f) * dt)) * heading); heading.Normalize();
            var lk = Vector3.Lerp(LOOK, new Vector3(0, 0.6f, -0.8f), indoors);   // ahead along the heading (-z of LOOK is "ahead")
            var followLook = pos + new Vector3(heading.x * -lk.z, lk.y, heading.y * -lk.z); var followPos = FollowFrom(pos, Mathf.SmoothStep(0, 1, indoors), heading);
            // the tilt: up over the rooftops and the walls to watch the fight, or down to the street looking up at it
            float tUp = Mathf.Max(0, tilt), tDown = Mathf.Max(0, -tilt);
            followPos += Vector3.up * (tUp * 9f - tDown * Mathf.Max(0, followPos.y - pos.y - 1.3f));
            followLook += new Vector3(heading.x, 0, heading.y) * (tUp * 22f) + Vector3.up * (tUp * 6.5f + tDown * 1.6f);   // raised, it looks out level over the walls to the field, not down at the cobbles
            var p = Vector3.Lerp(Field.CAM_POS, followPos, k);
            var r = Quaternion.Slerp(Quaternion.LookRotation(Field.CAM_LOOK - Field.CAM_POS), Quaternion.LookRotation(followLook - followPos), k);
            // at a post: behind and over him on the wall walk, looking out over the lanes (a glide up as he reaches the top)
            postBlend = Mathf.MoveTowards(postBlend, Post >= 0 || (climbPost >= 0 && climbUp && climbS > climbLen * 0.45f) ? 1f : 0f, dt / 0.9f);
            float fov = Mathf.Lerp(Field.FovFor(cam.aspect), Mathf.Max(55f, Field.FovFor(cam.aspect)), k);
            if (postBlend > 0.001f)
            {
                var ps = GameLoop.Posts[Post >= 0 ? Post : climbPost >= 0 ? climbPost : lastPost]; float pb = Mathf.SmoothStep(0, 1, postBlend);
                var pc = ps.stand - ps.outward * 1.6f - ps.along * 3.4f + Vector3.up * 6.8f; var pl = ps.look;   // high behind him over the wall itself, off to the side away from the tower, down onto its lane (pulled into the town it sat in the Cottages' roof by the outer towers)
                p = Vector3.Lerp(p, pc, pb); r = Quaternion.Slerp(r, Quaternion.LookRotation(pl - pc), pb); fov = Mathf.Lerp(fov, 58f, pb);
            }
            Fx.I.camHome = p; cam.transform.rotation = r;
            cam.fieldOfView = fov;
        }

        // ---------- the wall posts ----------
        public void DevPost(int k) { int s = GameLoop.Posts[k].stair ? k : k - 2; pos = GameLoop.Posts[s].foot; pos.y = 0; StartClimb(s, true); devTo = k; }
        int devTo = -1;   // test hook (GameLoop.Dev)
        void StartClimb(int k, bool up)
        {
            var ps = GameLoop.Posts[k]; Stop(); climbAfter = -1;
            float ft = OnFlight(pos, out int fk2, out var onLine);
            climbPath = up ? (fk2 == k ? new[] { onLine, ps.top, ps.landing, ps.stand } : new[] { new Vector3(pos.x, 0, pos.z), ps.foot, ps.bottom, ps.top, ps.landing, ps.stand })
                           : new[] { ps.stand, ps.landing, ps.top, ps.bottom, ps.foot };
            climbLen = 0; for (int i = 1; i < climbPath.Length; i++) climbLen += Vector3.Distance(climbPath[i - 1], climbPath[i]);
            climbPost = k; lastPost = k; climbUp = up; climbS = 0; Post = -1; wallPos = climbPath[0];
            if (up) Sound.Play("surge", 0.2f);
        }
        void ClimbStep(float dt)
        {
            climbS += CLIMB_SPEED * dt; float left = climbS;
            for (int i = 1; i < climbPath.Length; i++)
            {
                var a = climbPath[i - 1]; var b = climbPath[i]; float L = Vector3.Distance(a, b);
                if (left <= L || i == climbPath.Length - 1)
                {
                    wallPos = Vector3.Lerp(a, b, L > 0.001f ? Mathf.Clamp01(left / L) : 1f);
                    var d = b - a; d.y = 0; if (d.sqrMagnitude > 0.01f) yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(-d.x, -d.z) * Mathf.Rad2Deg, 540f * dt);
                    break;
                }
                left -= L;
            }
            pos = new Vector3(wallPos.x, 0, wallPos.z);
            if (climbS < climbLen) return;
            int k = climbPost; climbPost = -1;
            if (climbUp)
            {
                Post = k; wallPos = GameLoop.Posts[k].stand;
                if (devTo >= 0 && devTo != k) { Post = devTo; wallPos = GameLoop.Posts[devTo].stand; } devTo = -1;
                var gp = GameLoop.I ? GameLoop.I.P : null;
                if (gp != null && !gp.told.Contains("post1")) { gp.told.Add("post1"); Hud.Banner("On the wall! Click a monster to cast. A / D: the next post. S: climb down"); }
            }
            else { pos = GameLoop.Posts[k].foot; pos.y = 0; }
        }
        void WallInput(float dt)
        {
            castCd -= dt; dryT -= dt;
            if (climbPost >= 0) { ClimbStep(dt); return; }
            var ps = GameLoop.Posts[Post]; wallPos = ps.stand; pos = new Vector3(ps.stand.x, 0, ps.stand.z);
            if (castCd < CAST_EVERY - 0.25f) yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(ps.outward.x, ps.outward.z) * Mathf.Rad2Deg + 180f, 360f * dt);   // models face -Z: he looks out over the field
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.Q)) { Hop(-1); return; }
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) { Hop(1); return; }
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.E))
            {   // down the stair on this side of the gate (an outer post blinks back to the inner one first)
                int down = ps.stair ? Post : (Post == 2 ? 0 : 1);
                if (down != Post) { Fx.I.Burst(wallPos + Vector3.up, new Color(0.6f, 0.85f, 1f), 14); Post = down; wallPos = GameLoop.Posts[down].stand; }
                StartClimb(Post, false); return;
            }
            if (Input.GetMouseButton(0) && !Hud.OnHud(Hud.PointerPos) && castCd <= 0) Cast(Input.GetMouseButtonDown(0));
        }
        void Cast(bool pressed)
        {
            var g = GameLoop.I; if (g == null) return;
            var e = g.WizardTarget(Hud.PointerPos, cam);
            if (e == null) return;
            if (Mana < SPELL_COST)
            {
                if (pressed) { Sound.Dud(); if (dryT <= 0) { dryT = 6f; Hud.Banner("Out of mana! The fountain in the square refills it"); } }
                return;
            }
            Mana -= SPELL_COST; castCd = CAST_EVERY;
            var to = e.Pos - wallPos; yaw = Mathf.Atan2(-to.x, -to.z) * Mathf.Rad2Deg;   // he turns to it as he casts
            model.rotation = Quaternion.Euler(0, yaw, 0);
            g.WizardCast(e, StaffTip, g.PostElement(Post));
        }
        void Hop(int dir)
        {
            int i = System.Array.IndexOf(GameLoop.POST_ORDER, Post) + dir;
            if (i < 0 || i >= GameLoop.POST_ORDER.Length) { Sound.Dud(); return; }
            Fx.I.Burst(wallPos + Vector3.up * 1f, new Color(0.6f, 0.85f, 1f), 14);
            Post = GameLoop.POST_ORDER[i]; lastPost = Post; wallPos = GameLoop.Posts[Post].stand; pos = new Vector3(wallPos.x, 0, wallPos.z);
            Fx.I.Burst(wallPos + Vector3.up * 1f, new Color(0.6f, 0.85f, 1f), 14); Sound.Play("surge", 0.25f);   // a time wizard's blink
        }
        bool stairCam;
        // how far up a stair's flight a ground spot stands (0 at its bottom step, 1 at its top), or -1 when off every flight;
        // onLine: that point on the flight, at its height
        static float OnFlight(Vector3 p, out int k, out Vector3 onLine)
        {
            k = -1; onLine = Vector3.zero;
            for (int i = 0; i < 4; i++)
            {
                var ps = GameLoop.Posts[i]; if (!ps.stair) continue;
                var a = new Vector3(ps.bottom.x, 0, ps.bottom.z); var b = new Vector3(ps.top.x, 0, ps.top.z); var ab = b - a;
                float t = Vector3.Dot(new Vector3(p.x, 0, p.z) - a, ab) / ab.sqrMagnitude;
                if (t < -0.25f || t > 1.15f) continue;
                if ((a + ab * Mathf.Clamp01(t) - new Vector3(p.x, 0, p.z)).magnitude > 2.0f) continue;   // the walk keeps him 2.4 m inside the ring and the flight runs 1.65 m inside it, its side wall between (bot, Nov 16: stuck at that wall 1.2 m out)
                k = i; t = Mathf.Clamp01(t); onLine = a + ab * t + Vector3.up * (ps.top.y * t); return t;
            }
            return -1f;
        }
        // the stair within reach metres of a spot (its foot or its flight), or -1
        static int StairClose(Vector3 p, float reach)
        {
            for (int i = 0; i < 4; i++)
            {
                var ps = GameLoop.Posts[i]; if (!ps.stair) continue;
                var a = new Vector3(ps.bottom.x, 0, ps.bottom.z); var b = new Vector3(ps.top.x, 0, ps.top.z); var ab = b - a; var q = new Vector3(p.x, 0, p.z);
                float t = Mathf.Clamp01(Vector3.Dot(q - a, ab) / ab.sqrMagnitude);
                if ((a + ab * t - q).magnitude < reach || (ps.foot - q).magnitude < reach) return i;
            }
            return -1;
        }
        // a stair (its flight or its brazier) under the pointer
        bool StairAt(Vector2 sp, out int k)
        {
            k = -1; float best = 70f * Screen.height / 720f;
            for (int i = 0; i < 4; i++)
            {
                var ps = GameLoop.Posts[i]; if (!ps.stair) continue;
                foreach (var w in new[] { ps.foot + Vector3.up * 0.5f, (ps.bottom + ps.top) / 2f, ps.landing + Vector3.up * 1.4f })
                {
                    var s2 = cam.WorldToScreenPoint(w); if (s2.z <= 0) continue;
                    float d = Vector2.Distance(sp, s2); if (d < best) { best = d; k = i; }
                }
            }
            return k >= 0;
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
            if (GameLoop.I && GameLoop.I.TrainerHere) Push(GameLoop.TRAINER_AT, 0.45f + RADIUS);   // the Chronomancer stands his ground
            if (GameLoop.I && GameLoop.I.StewardHere) { Push(GameLoop.STEWARD_AT, 0.45f + RADIUS); var side = new Vector3(GameLoop.HUB_FACE.y, 0, -GameLoop.HUB_FACE.x) * 0.7f; Push(GameLoop.BOOTH_AT + side, 0.75f + RADIUS); Push(GameLoop.BOOTH_AT - side, 0.75f + RADIUS); }   // the booth's counter
            if (GameLoop.I && GameLoop.I.CaptainAtPost) Push(GameLoop.CAPTAIN_AT, 0.4f + RADIUS);
            foreach (var pr in Field.PROPS) Push(new Vector3(pr.x, 0, pr.y), pr.z + RADIUS);
            if (pos.z > -3.2f && Mathf.Abs(pos.x) < Field.GATE_HALF + 0.8f) pos.z = -3.2f;
            float m = 2.4f, ex = pos.x / (Field.RX - m), ez = (pos.z + Field.RZ) / (Field.RZ - m), e = Mathf.Sqrt(ex * ex + ez * ez);
            if (e > 1) { pos.x /= e; pos.z = (pos.z + Field.RZ) / e - Field.RZ; vel *= 0.5f; }
            for (int i = 0; i < HG.BUILDING_IDS.Length; i++)
            {
                if (!InFootprint(i, pos, RADIUS + 0.3f)) continue;
                if (GameLoop.I && GameLoop.I.CentreOn(i)) Push(Field.Centre(i), Field.CENTREPIECE[i].z + RADIUS);   // he walks up to the centrepiece, not through it
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
