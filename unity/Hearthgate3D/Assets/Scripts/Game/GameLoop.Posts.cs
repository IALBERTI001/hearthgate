// The wizard on the walls (owner, Nov 16 2026: "I would love to see some form of ability for the wizard to watch for
// monsters from the wall to shoot at monsters with an element ... Something to tie the town and the outside even more. Like
// a quest giver that rewards players that manually eliminate a certain amount of monsters ... just walking around town does
// not seem like it should be the only thing the wizard can do"; picks approved the same day: wall posts, a Watch Captain's
// bounties, mana that refills at the fountain).
//  - A post beside each tower: a stone stair up the inside of the ring wall (hg_front.wall_stair) to a brazier on the wall
//    walk. On his walk the wizard climbs it (E at its foot, or a click on it); from the post the camera looks out over the
//    lanes and a click on a monster casts that tower's element at it (Storm where the tower has none). A / D blink him to the
//    next post, S takes him back down. His spells are his alone, so they are on the walk only (owner, Nov 14 2026).
//  - Mana: each spell costs some; it refills at the fountain (and only a trickle anywhere else), so he goes back and forth
//    between the walls and the square, past the town's keepers and shops.
//  - The Captain of the Watch (the soldier who runs to warn the wizard) stands by the gate between his runs. His '?' is a
//    bounty: slay so many monsters by your own hand; his '!' pays Starshards. Each bounty asks a little more.
using System.Linq;
using UnityEngine;
using Hearthgate.Core;

namespace Hearthgate.Game
{
    public partial class GameLoop
    {
        public const int CAPTAIN = 97;
        public const double WIZ_DAMAGE = 3.0;    // a spell strikes like three tower shots
        public const float HAND_WINDOW = 2.5f;   // a kill this soon after his spell struck (a burn, a chain) is his
        public const float WALK_Y = Field_WALL_H + 0.18f, Field_WALL_H = 3.4f;   // the wall walk (hg_layout.WALL_H, hg_front.wall)

        // ---------- the posts: one by each tower, its stair running along the wall away from the gate ----------
        public struct PostSpot { public Vector3 stand, foot, bottom, top, landing, outward, along, look; public int tower; public bool stair; }
        static PostSpot[] posts;
        public static PostSpot[] Posts { get { if (posts == null) posts = Enumerable.Range(0, 4).Select(MakePost).ToArray(); return posts; } }
        // the ring as the wall is built (hg_front.wall): x = RX cos a, z = -RZ + RZ sin a
        static Vector3 Ring(float a) => new Vector3(Field.RX * Mathf.Cos(a), 0, -Field.RZ + Field.RZ * Mathf.Sin(a));
        static Vector3 Out(float a) => new Vector3(Mathf.Cos(a) / Field.RX, 0, Mathf.Sin(a) / Field.RZ).normalized;
        static float Along(float a, float metres, float sgn)
        {   // step round the ring by arc length
            for (int k = 0; k < 20; k++) { float sp = new Vector2(Field.RX * Mathf.Sin(a), Field.RZ * Mathf.Cos(a)).magnitude; a += sgn * metres / 20f / sp; }
            return a;
        }
        static PostSpot MakePost(int i)
        {
            // the stairs: one each side, between the inner and the outer tower (behind the outer towers the Cottages and the Tavern
            // leave no room, and the walk camera sat in their roofs); the outer posts stand on that same stretch of wall, toward the
            // gate, and he blinks along the wall to them (A / D)
            bool inner = i < 2;
            var t = Field.Tower(i); float a0 = Mathf.Atan2((t.z + Field.RZ) / Field.RZ, t.x / Field.RX), sgn = (t.x > 0 ? -1f : 1f) * (inner ? 1f : -1f);   // inner: away from the gate; outer: toward it
            float aPost = Along(a0, 3.7f, sgn), aTop = Along(a0, 3.45f, sgn), aFoot = Along(a0, 8.05f, sgn), aWay = Along(a0, 8.5f, sgn);
            var n = Out(aPost); var ps = new PostSpot { tower = i, outward = n, stair = inner };
            ps.stand = Ring(aPost) - n * 0.25f + Vector3.up * WALK_Y;
            ps.landing = (Ring(aTop) - Out(aTop) * 0.9f + Ring(aPost) - Out(aPost) * 0.25f) * 0.5f + Vector3.up * WALK_Y;   // off the top step onto the walk, clear of the brazier
            ps.top = Ring(aTop) - Out(aTop) * 1.65f + Vector3.up * WALK_Y;
            ps.bottom = Ring(aFoot) - Out(aFoot) * 1.65f;
            ps.foot = Ring(aWay) - Out(aWay) * 3.2f;   // where he starts up: inside the street he walks
            ps.along = (ps.top - ps.bottom); ps.along.y = 0; ps.along.Normalize();
            ps.look = Field.Lane(i, 4f, 0) + Vector3.down * 1.5f;   // near enough that he stands above the HUD's bars, his lane running up the middle   // the post camera watches the tower's own lane
            return ps;
        }
        // the order along the wall as the post camera sees it (looking out, +x is to the right)
        public static readonly int[] POST_ORDER = { 3, 1, 0, 2 };

        GameObject[] stairs;
        void TickPosts()
        {
            if (art == null || !art.wallStair) return;
            if (stairs == null)
            {
                stairs = new GameObject[4];
                for (int i = 0; i < 4; i++)
                {
                    if (!Posts[i].stair) continue;
                    var ps = Posts[i]; var g = Instantiate(art.wallStair, world); g.name = "wall stair " + i; stairs[i] = g;
                    g.transform.position = ps.bottom;
                    // the model rises along its +x with +z toward the wall: look along the outward normal; mirrored where the
                    // flight has to run the other way
                    var flat = ps.outward; flat.y = 0;
                    g.transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up) * art.wallStair.transform.rotation;
                    var right = new Vector3(flat.z, 0, -flat.x);
                    if (Vector3.Dot(right, ps.along) < 0) { var s = g.transform.localScale; s.x = -s.x; g.transform.localScale = s; }
                }
            }
        }

        // the tower's element at a post (the wizard casts what the tower beside him throws); Storm where it has none
        public string PostElement(int i)
        {
            var tw = towers.FirstOrDefault(t => t.n == i && !t.beacon);
            return tw == null || tw.el == "none" ? "storm" : tw.el;
        }
        // the monster under the pointer: the nearest on screen to it, else the nearest on the ground to where it points
        public Enemy WizardTarget(Vector2 screen, Camera cam)
        {
            Enemy best = null; float bd = 110f * Screen.height / 720f;
            foreach (var e in enemies)
            {
                if (e.dying > 0 || e.hp <= 0) continue;
                var s = cam.WorldToScreenPoint(e.Pos + Vector3.up * e.size * 0.3f); if (s.z <= 0) continue;
                float d = Vector2.Distance(screen, s); if (d < bd) { bd = d; best = e; }
            }
            if (best != null) return best;
            var ray = cam.ScreenPointToRay(screen);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float t))
            {
                var g = ray.GetPoint(t); float gd = 6f;
                foreach (var e in enemies) { if (e.dying > 0) continue; float d = Vector3.Distance(new Vector3(e.Pos.x, 0, e.Pos.z), g); if (d < gd) { gd = d; best = e; } }
            }
            return best;
        }
        public void WizardCast(Enemy e, Vector3 from, string el)
        {
            Launch(el, from, e, false, true);
            Fx.I.Burst(from, Field.Of(el), 6);
            if (!P.told.Contains("cast1")) { P.told.Add("cast1"); Hud.Banner("Your spell strikes like three tower shots. Mana refills at the fountain"); }
        }

        // ---------- the bounties ----------
        public int BountyNeed => 10 + 5 * Mathf.Min(P.bountyLv, 10);
        public int BountyPay => 3 + Mathf.Min(P.bountyLv, 6);
        public bool BountyDone => P.bountyOn && P.bountyN >= BountyNeed;
        void HandKill(Enemy e)
        {
            if (!P.bountyOn || P.bountyN >= BountyNeed) return;
            P.bountyN++;
            Fx.I.Pop(e.Pos + Vector3.up * (e.size + 1.6f), $"Bounty {P.bountyN}/{BountyNeed}", new Color(0.72f, 0.9f, 1f), 26);
            if (P.bountyN >= BountyNeed) { Sound.Play("feat", 0.45f); Hud.Banner("Bounty done! The Captain of the Watch has your Starshards by the gate"); Save(); }
        }
        public void TakeBounty()
        {
            P.bountyOn = true; P.bountyN = 0; Sound.Play("claim", 0.4f); Save();
            if (!P.told.Contains("bounty1")) { P.told.Add("bounty1"); Hud.Banner("Climb a wall stair (one each side of the gate, between the towers): E at its foot, then click the monsters"); }
        }
        public void ClaimBounty()
        {
            if (!BountyDone) return;
            int pay = BountyPay; P.bountyOn = false; P.bountyN = 0; P.bountyLv++;
            GiveShards(pay, CAPTAIN_AT + Vector3.up * 2.4f); Sound.Play("level_up", 0.45f); Save();
        }

        // ---------- the Captain at his post (he is the watch's runner: GameLoop.Watch) ----------
        public static readonly Vector3 CAPTAIN_AT = new Vector3(4.5f, 0, Field.GATE_Z - 8.5f);
        public bool CaptainHere => StewardHere;
        public bool CaptainAtPost => CaptainHere && runner && runner.gameObject.activeSelf && watchRun == Run.None && !P.fallen;
        // the keepers who stand in the square rather than in a room
        public static readonly int[] SQUARE_KEEPERS = { TRAINER, STEWARD, CAPTAIN };
        public bool KeeperHere(int k) => k == TRAINER ? TrainerHere : k == STEWARD ? StewardHere : k == CAPTAIN ? CaptainAtPost : false;
        bool toldCaptain;
        void TickCaptain(float dt)
        {
            if (!CaptainHere || P.fallen) { if (runner && watchRun == Run.None) runner.gameObject.SetActive(false); return; }
            if (!EnsureRunner()) return;
            if (watchRun != Run.None) return;
            if (!runner.gameObject.activeSelf) { runner.gameObject.SetActive(true); runner.position = CAPTAIN_AT; Fx.I.Burst(CAPTAIN_AT + Vector3.up * 1.2f, Field.GOLD, 10); }
            runner.position = CAPTAIN_AT;
            var wz = Wizard.I; var look = wz && wz.On && !wz.OnWall && (wz.Pos - CAPTAIN_AT).magnitude < 8f ? wz.Pos - CAPTAIN_AT : Field.PLAZA - CAPTAIN_AT; look.y = 0;
            runner.rotation = Quaternion.Slerp(runner.rotation, Quaternion.LookRotation(-look.normalized), 6f * dt);   // models face -Z
            runnerGait.Tick(dt, 0f);
            if (!toldCaptain && !P.told.Contains("captain")) { toldCaptain = true; P.told.Add("captain"); Hud.Banner("The Captain of the Watch stands by the gate. Walk the town (T) and talk to him: he has a bounty for you"); }
        }
        bool EnsureRunner()
        {
            if (runner) return true;
            var prefab = art ? art.Npc("soldier") : null; if (prefab == null) return false;
            runner = Instantiate(prefab, world).transform; runner.name = "captain of the watch";
            runner.localScale = Vector3.one * Townsfolk.SCALE;
            runnerGait = runner.gameObject.AddComponent<Gait>(); runnerGait.LegLength = 0.6f * Townsfolk.SCALE; runnerGait.ArmSwing = 0.6f; runnerGait.StaffArm = 0.2f; runnerGait.MaxCadence = 3.4f;
            runner.position = CAPTAIN_AT; return true;
        }
    }
}
