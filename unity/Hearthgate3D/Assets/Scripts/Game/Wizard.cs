// The town walk (owner, Nov 13 2026: "Add a button that makes you into the wizard that can walk into the town and upgrade
// the building from inside. I think that would be an awesome addition."). The wizard waits by the fountain, in view from
// the walls. The Walk button (or T, or a tap on him) brings the camera down behind him; he walks with WASD or the arrow
// keys, or to wherever the player taps; next to a building a card offers its upgrade (or the rebuild, for a ruin the
// city can rebuild now), pressed with E or a tap. The fight goes on at the walls while he walks: the towers shoot by
// themselves. No physics (the WebGL build strips it): the ground is a plane and the buildings are circles.
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public class Wizard : MonoBehaviour
    {
        public static Wizard I;
        public bool On;               // walking the town (the camera follows him)
        public int Near = -1;         // the plot in reach, or -1
        public const float SPEED = 6f, REACH = 4.6f, PLOT_R = 2.9f, FOUNTAIN_R = 3.4f;
        public static readonly Vector3 HOME = Field.PLAZA + new Vector3(0.6f, 0, 3.9f);   // in front of the fountain, facing the walls
        public static readonly Vector3 FOLLOW = new Vector3(0, 9f, -7.5f), LOOK = new Vector3(0, 1.6f, 3.2f);   // the walk camera, from his feet

        Transform model; Camera cam; Vector3 pos, vel; Vector3? target; float yaw = 180f, blend, step, idle;
        GameObject goal;   // a little gold ring where a tap sent him

        public void Init(ArtSet art)
        {
            I = this; cam = Camera.main; pos = HOME;
            model = Art.Spawn(art ? art.wizard : null, "wizard", pos, transform).transform;
            model.localScale = Vector3.one * 1.15f;
            goal = Art.Spawn(art ? art.marker : null, "goal", pos, transform); goal.transform.localScale = Vector3.one * 0.32f; goal.SetActive(false);
        }

        public void Toggle()
        {
            On = !On; target = null; goal.SetActive(false);
            Fx.I.Burst(pos + Vector3.up * 1.4f, Field.GOLD, 14);
            if (On) Hud.Banner("Walk the town: rebuild and upgrade up close");
        }

        // the ground point under the pointer
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
                    var ray = cam.ScreenPointToRay(Input.mousePosition); var c = pos + Vector3.up * 1.3f;
                    if (Vector3.Cross(ray.direction, c - ray.origin).magnitude < 1.6f) Toggle();
                }
                else if (Ground(Input.mousePosition, out var at)) { target = at; goal.SetActive(true); goal.transform.position = at + Vector3.up * 0.05f; }
            }

            // walking: keys move him relative to the camera, a tap sends him
            Vector3 want = Vector3.zero;
            if (On)
            {
                float h = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
                float v = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.Z) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
                if (Input.GetKey(KeyCode.Q)) h -= 1;   // AZERTY: ZQSD
                var fw = cam.transform.forward; fw.y = 0; fw = fw.sqrMagnitude < 0.01f ? Vector3.forward : fw.normalized; var rt = new Vector3(fw.z, 0, -fw.x);
                if (h != 0 || v != 0) { want = (rt * h + fw * v).normalized; target = null; goal.SetActive(false); }
                else if (target.HasValue)
                {
                    var to = target.Value - pos; to.y = 0;
                    if (to.magnitude < 0.4f) { target = null; goal.SetActive(false); }
                    else want = to.normalized * Mathf.Clamp01(to.magnitude / 1.2f + 0.3f);
                }
            }
            vel = Vector3.MoveTowards(vel, want * SPEED, SPEED * 6f * dt);
            pos += vel * dt; Collide();
            float speed = vel.magnitude;
            if (speed > 0.3f) yaw = Mathf.MoveTowardsAngle(yaw, Mathf.Atan2(-vel.x, -vel.z) * Mathf.Rad2Deg, 540f * dt);   // models face -Z
            else if (!On) yaw = Mathf.MoveTowardsAngle(yaw, 180f, 180f * dt);   // back at rest, he faces the walls and the camera
            step += dt * (speed > 0.3f ? 9f : 0f);

            // the look: a hop and a forward lean as he walks, a slow breath when he stands
            float hop = speed > 0.3f ? Mathf.Abs(Mathf.Sin(step)) * 0.14f : 0f, breath = Mathf.Sin(idle * 2f) * 0.015f;
            model.position = pos + Vector3.up * hop;
            model.rotation = Quaternion.Euler(Mathf.Clamp(speed / SPEED, 0, 1) * 9f, yaw, Mathf.Sin(step) * 3f * Mathf.Clamp01(speed));
            model.localScale = new Vector3(1.15f, 1.15f * (1 + breath - hop * 0.25f), 1.15f);
            if (goal.activeSelf) goal.transform.Rotate(0, 90 * dt, 0);

            // the plot in reach (the nearest within REACH of his feet)
            Near = -1; float best = REACH;
            if (On) for (int i = 0; i < HG.BUILDING_IDS.Length; i++)
                {
                    float d = Vector3.Distance(new Vector3(pos.x, 0, pos.z), Field.Plot(i)) - PLOT_R + 1.2f;
                    if (d < best) { best = d; Near = i; }
                }
            if (On && Near >= 0 && Input.GetKeyDown(KeyCode.E)) GameLoop.I.WorkOn(Near);

            // the camera: from the walls it swings over into the town and settles behind him on the town side, looking out
            // toward the walls, so the wall and the towers are never between it and him (from the wall side the fire cap
            // filled the screen whenever he walked near the front: Nov 13 2026), and the fight shows beyond the wall
            blend = Mathf.MoveTowards(blend, On ? 1 : 0, dt / 1.1f);
            float k = Mathf.SmoothStep(0, 1, blend);
            var followLook = pos + LOOK;
            var followPos = pos + FOLLOW;
            var p = Vector3.Lerp(Field.CAM_POS, followPos, k);
            var r = Quaternion.Slerp(Quaternion.LookRotation(Field.CAM_LOOK - Field.CAM_POS), Quaternion.LookRotation(followLook - followPos), k);
            Fx.I.camHome = p; cam.transform.rotation = r;
            cam.fieldOfView = Mathf.Lerp(Field.FovFor(cam.aspect), Mathf.Max(52f, Field.FovFor(cam.aspect)), k);
        }

        // inside the ring, out of the buildings, the fountain and the gatehouse
        void Collide()
        {
            for (int i = 0; i < HG.BUILDING_IDS.Length; i++) Push(Field.Plot(i), PLOT_R);
            Push(Field.PLAZA, FOUNTAIN_R);
            if (pos.z > -3.2f && Mathf.Abs(pos.x) < Field.GATE_HALF + 0.8f) pos.z = -3.2f;
            float m = 2.4f, ex = pos.x / (Field.RX - m), ez = (pos.z + Field.RZ) / (Field.RZ - m), e = Mathf.Sqrt(ex * ex + ez * ez);
            if (e > 1) { pos.x /= e; pos.z = (pos.z + Field.RZ) / e - Field.RZ; vel *= 0.5f; }
        }
        void Push(Vector3 c, float r)
        {
            var d = new Vector3(pos.x - c.x, 0, pos.z - c.z); float L = d.magnitude;
            if (L < r) { pos = new Vector3(c.x, 0, c.z) + (L > 0.01f ? d / L : Vector3.forward) * r; vel -= Vector3.Project(vel, d) * 0.9f; }
        }

        public Vector3 Head => pos + Vector3.up * 2.9f;
    }
}
