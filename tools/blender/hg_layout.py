# Hearthgate 3D layout, shared by the Blender kit (hg_front.py), the scene preview (hg_scene.py) and mirrored by
# Assets/Scripts/Game/Field.cs (LayoutCheck verifies the models against it).
#
# The owner's sketch (Nov 13 2026 18:44): "Make it so that monsters are walking up to the towers on a route in front of
# each tower. Close off the loop around town to make a protected town." The town sits inside an elliptical ring wall.
# The front arc, nearest the camera, carries the gatehouse in the middle and two element towers each side. Four lanes
# run up the screen from its bottom edge, one to the foot of each tower. Numbers from tools/layout/ring.py (a search
# over the frame, the HUD, sight lines into the town and footprints); since the human-scale town (Nov 13 2026) the plots,
# plaza, ring and camera come from tools/layout/town.py, and the town runs on beyond the top of the wall view.
#
# Coordinates are Unity's: x (screen LEFT is +x: the camera stands north and looks south), z north toward the camera,
# y up. In Blender, Unity z is Blender y.
import math

RX, RZ = 44.0, 28.0                    # ring half axes; centre (0, -RZ), so the ring's front point is the gate at (0, 0)
WALL_H = 3.4                           # wall walk; merlons to about 4.0
TOWER_X = (9.0, -9.0, 26.4, -26.4)     # tower 0 and 1 beside the gate, 2 and 3 further out (Field.TOWER_X, same order)
TOWER_TOP = 5.0
TOWER_FOOT = 2.6                       # from a tower's centre to the foot of its lane (base radius 2.15 plus a margin)
LANE_HALF = 1.8                        # each lane is 3.6 m wide; enemies keep within +-1.3 of its centre line
LANE_FAR_Z = 22.5                      # lanes begin just below the frame's bottom edge (ground z about 18 there)
PLAZA = (0.0, -10.0)
PLOTS = [(-31.0, -20.16), (-29.0, -35.84), (2.5, -35.34), (15.5, -46.5), (-15.5, -20.16), (18.0, -32.84), (-15.5, -46.0), (0.0, -48.5), (30.0, -19.66), (34.0, -35.84)]
# the buildings at human scale (owner, Nov 13 2026: "Wizard is as big as the town, some doors are insanely bigger than
# others. I want the wizard to be able to walk into the buildings"): footprint w (across the front) x d (deep), metres.
# Every door is 1.2 x 2.4 m, every storey 3.2 m (hg_city.py); the wizard is 1.8 m to his hat's tip.
SIZES = {'forge': (8.0, 7.0), 'frostSpire': (7.0, 7.0), 'stormBell': (6.0, 6.0), 'bakery': (8.0, 7.0), 'market': (10.0, 7.0),
         'tavern': (10.0, 8.0), 'granary': (8.0, 8.0), 'library': (10.0, 8.0), 'cottages': (10.0, 6.0), 'watchtower': (6.0, 6.0)}
DOOR_W, DOOR_H, STOREY, WALL_T, PLINTH = 1.2, 2.4, 3.2, 0.3, 0.3
IDS = ['forge', 'frostSpire', 'stormBell', 'bakery', 'market', 'tavern', 'granary', 'library', 'cottages', 'watchtower']

CAM_PITCH, CAM_FOV, CAM_DIST = 30.0, 40.0, 55.0
CAM_LOOK = (0.0, 0.0, -3.0)
CAM_POS = (0.0, CAM_DIST * math.sin(math.radians(CAM_PITCH)), CAM_LOOK[2] + CAM_DIST * math.cos(math.radians(CAM_PITCH)))

def ring_z(x):
    return -RZ + RZ * math.sqrt(max(0.0, 1 - (x / RX) ** 2))

def ring_normal(x, z):   # outward, on the ground plane
    nx, nz = x / RX ** 2, (z + RZ) / RZ ** 2
    L = math.hypot(nx, nz); return (nx / L, nz / L)

def inside(x, z, margin=0.0):
    return (x / (RX - margin)) ** 2 + ((z + RZ) / (RZ - margin)) ** 2 < 1

def ring_points(step=2.0):
    """Points round the ring about `step` metres apart, starting at the gate (a = 90 degrees) and going round toward -x."""
    pts, a = [], math.pi / 2
    while a < math.pi / 2 + math.tau - 1e-6:
        pts.append((RX * math.cos(a), -RZ + RZ * math.sin(a)))
        dx, dz = -RX * math.sin(a), RZ * math.cos(a)
        a += step / math.hypot(dx, dz)
    return pts

TOWERS = [(x, ring_z(x)) for x in TOWER_X]

def project(p, aspect=16 / 9):
    """World point -> viewport (0..1, y up) from the game camera."""
    fx, fy, fz = (CAM_LOOK[i] - CAM_POS[i] for i in range(3)); L = math.sqrt(fx * fx + fy * fy + fz * fz); f = (fx / L, fy / L, fz / L)
    r = (f[2], 0.0, -f[0]); Lr = math.hypot(r[0], r[2]); r = (r[0] / Lr, 0.0, r[2] / Lr)          # cross(up, f)
    u = (f[1] * r[2] - f[2] * r[1], f[2] * r[0] - f[0] * r[2], f[0] * r[1] - f[1] * r[0])            # cross(f, r)
    d = [p[i] - CAM_POS[i] for i in range(3)]
    zc = sum(d[i] * f[i] for i in range(3)); xc = sum(d[i] * r[i] for i in range(3)); yc = sum(d[i] * u[i] for i in range(3))
    t = math.tan(math.radians(CAM_FOV / 2))
    return (0.5 + xc / (zc * t * aspect) / 2, 0.5 + yc / (zc * t) / 2)

def _ground_x_at(sx, z):   # the ground x at depth z whose screen x is sx
    lo, hi = -60.0, 60.0
    for _ in range(60):
        m = (lo + hi) / 2
        if project((m, 0, z))[0] < sx: hi = m   # +x is screen left
        else: lo = m
    return (lo + hi) / 2

def lane(i):
    """Lane i: (foot, start). The foot is in front of tower i on the ring's outward normal; the lane runs straight up the
    screen from below the frame (as in the owner's sketch), splayed a little outward."""
    tx, tz = TOWERS[i]; nx, nz = ring_normal(tx, tz)
    foot = (tx + nx * TOWER_FOOT, tz + nz * TOWER_FOOT)
    sx = project((foot[0], 0, foot[1]))[0]
    sp = 0.065 if i < 2 else 0.03                        # a slight outward splay; the inner pair clear of the gate bar
    sx += -sp if sx < 0.5 else sp
    start = (_ground_x_at(sx, LANE_FAR_Z), LANE_FAR_Z)
    return foot, start

LANES = [lane(i) for i in range(4)]

def lane_clear(x, z, r):
    """True when a circle of radius r at (x, z) keeps off every lane and its 0.8 m verge."""
    for (fx, fz), (sx, sz) in LANES:
        dx, dz = sx - fx, sz - fz; L2 = dx * dx + dz * dz
        t = max(0.0, min(1.0, ((x - fx) * dx + (z - fz) * dz) / L2))
        if math.hypot(x - (fx + t * dx), z - (fz + t * dz)) < LANE_HALF + 0.8 + r: return False
    return True

if __name__ == '__main__':
    print('towers', [(round(x, 2), round(z, 2)) for x, z in TOWERS])
    for i, (f, s) in enumerate(LANES):
        print('lane', i, 'foot', tuple(round(v, 2) for v in f), 'start', tuple(round(v, 2) for v in s), 'length', round(math.dist(f, s), 2),
              'screen', tuple(round(v, 3) for v in project((f[0], 0, f[1]))), tuple(round(v, 3) for v in project((s[0], 0, s[1]))))
    print('cam', CAM_POS)

# ground to the top of each building (hg_city.py), for the marker over a plot and the town search
HEIGHT = {'forge': 7.6, 'frostSpire': 14.8, 'stormBell': 12.9, 'bakery': 10.4, 'market': 6.6, 'tavern': 10.6,
          'granary': 8.8, 'library': 9.0, 'cottages': 6.8, 'watchtower': 13.2}
