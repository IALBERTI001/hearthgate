// The watch's runner (owner, Nov 14 2026: "a Defense force or soldier NPC that runs to the wizard when monsters are too
// close to the wall. Like on them or just about to be on the wall to hit it. This way the player knows its time to focus on
// wall upgrades because the monsters are there"). When an enemy reaches the foot of the walls, a soldier of the watch runs
// out of the gatehouse to wherever the wizard is (on his walk, or at his post by the gate), stops, calls the warning and
// points at the Upgrade button, then runs back. He comes again only after a rest, so he is news, not noise.
using UnityEngine;

namespace Hearthgate.Game
{
    public partial class GameLoop
    {
        enum Run { None, Out, Report, Back }
        Run watchRun; Transform runner; Gait runnerGait; float watchT, watchRest;
        const float RUN_SPEED = 4.6f, WATCH_REST = 50f, NEAR_WALL = 2.5f;

        bool EnemyAtWalls()
        {
            foreach (var e in enemies) if (e.dying <= 0 && e.z <= e.stop + NEAR_WALL) return true;
            return false;
        }
        Vector3 WatchPost => new Vector3(4.5f, 0, Field.GATE_Z - 8.5f);   // in the gatehouse yard, beside its shadow: the walls camera sees him set out

        void TickWatch(float dt)
        {
            var wz = Wizard.I; watchRest -= dt;
            if (watchRun == Run.None)
            {
                if (watchRest > 0 || wz == null || Fallen || rewinding || Hud.Paused || !waveLive || !EnemyAtWalls()) return;
                var prefab = art ? art.Npc("soldier") : null; if (prefab == null) return;
                if (!runner)
                {
                    runner = Instantiate(prefab, world).transform; runner.name = "watch runner";
                    runner.localScale = Vector3.one * Townsfolk.SCALE;
                    runnerGait = runner.gameObject.AddComponent<Gait>(); runnerGait.LegLength = 0.6f * Townsfolk.SCALE; runnerGait.ArmSwing = 0.6f; runnerGait.StaffArm = 0.2f; runnerGait.MaxCadence = 3.4f;
                }
                runner.gameObject.SetActive(true); runner.position = WatchPost; watchRun = Run.Out;
                Sound.Play("bell", 0.5f);
                return;
            }
            if (!runner) { watchRun = Run.None; return; }
            // up the main street first, then across the square to him: a straight line from the gate would cut through houses
            var street = new Vector3(0, 0, -14f); bool far = wz.Pos.z < street.z;
            Vector3 to = watchRun == Run.Back ? (far && runner.position.z < street.z - 0.5f ? street : WatchPost)
                       : (far && runner.position.z > street.z + 0.5f ? street : wz.Pos);
            var d = to - runner.position; d.y = 0; float dist = d.magnitude;
            if (watchRun == Run.Report)
            {
                var face = wz.Pos - runner.position; face.y = 0;
                if (face.sqrMagnitude > 0.01f) runner.rotation = Quaternion.Slerp(runner.rotation, Quaternion.LookRotation(-face), 10f * dt);
                runnerGait.Tick(dt, 0f);
                if ((watchT -= dt) <= 0) watchRun = Run.Back;
                return;
            }
            bool last = watchRun == Run.Out ? to == wz.Pos : to == WatchPost;   // a waypoint is passed, not stopped at
            float stopAt = watchRun == Run.Out ? 1.5f : 0.2f;
            if ((last && dist <= stopAt) || Fallen)
            {
                if (watchRun == Run.Out && !Fallen)
                {
                    watchRun = Run.Report; watchT = 2.6f;
                    Guide.Shout(wz.On ? "The watch: They're at the walls! Upgrade them: press U, or hold the mouse!" : "The watch: They're at the walls! Upgrade them at the gatehouse!", "up");
                    Fx.I.Pop(runner.position + Vector3.up * 2.2f, "!", Field.GOLD, 40);
                }
                else { watchRun = Run.None; watchRest = WATCH_REST; runner.gameObject.SetActive(false); }
                return;
            }
            var step = d / dist * Mathf.Min(dist, RUN_SPEED * dt);
            runner.position += step;
            runner.rotation = Quaternion.Slerp(runner.rotation, Quaternion.LookRotation(-d), 12f * dt);   // models face -Z
            runnerGait.Tick(dt, RUN_SPEED);
        }
    }
}
