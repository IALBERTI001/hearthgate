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
PLOTS_GRID = [(-31.0, -20.16), (-29.0, -35.84), (2.5, -35.34), (15.5, -46.5), (-15.5, -20.16), (18.0, -32.84), (-15.5, -46.0), (0.0, -48.5), (30.0, -19.66), (34.0, -35.84)]
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

ROAD_HALF = 2.4   # the road to the gate (owner, Nov 14 2026: "the no road to gate part is a bit bad"), down the middle

def lane_clear(x, z, r):
    """True when a circle of radius r at (x, z) keeps off every lane, the road to the gate, and their 0.8 m verges."""
    if z > 1.0 and abs(x) < ROAD_HALF + 0.8 + r: return False
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


# ---------------------------------------------------------------------------------------------------------------------
# The ring town (owner, Nov 14 2026: "you can do the housing spaced inside circular facing the interior. makes the inside
# match a circular cobble floor matching the path of the gate"). The houses stand evenly spaced on an arc just inside
# the wall, each turned to face the town's centre; the middle of the ring is one cobbled square, laid in circles round
# the fountain, that the road through the gate runs straight into. The front arc (the gatehouse and its towers) stays
# open, so the wall camera never looks at the backs of houses. Grass and gardens fill the strip between the houses and
# the wall; villagers walk the square.
CENTRE = (0.0, -RZ)                    # the ring's centre: the fountain
PLAZA = CENTRE
HOUSE_A, HOUSE_B = RX - 7.0, RZ - 7.0  # the arc the houses' centres stand on (an 8 m deep house keeps 3 m from the wall)
ARC = (130.0, 410.0)                   # degrees, measured from +x with +z toward the gate: back and sides, the front open
ORDER = ['granary', 'tavern', 'frostSpire', 'forge', 'stormBell', 'bakery', 'library', 'watchtower', 'market', 'cottages']   # the first rebuilds at the back, fronts to the camera
SQUARE_A, SQUARE_B = HOUSE_A - 4.0 - 2.6, HOUSE_B - 4.0 - 2.6   # the cobbled square (an ellipse round the fountain)
GATE_HALF_ST = 2.2                     # the road through the gate, on into the square
PATH_HALF = 1.1                        # each door's path out to the square

FRONT_A, FRONT_B = HOUSE_A - 4.0, HOUSE_B - 4.0   # the line the house fronts are spaced along (gaps measured at the doors)

def _arc_points(n=4000):
    a0, a1 = (math.radians(v) for v in ARC); pts, s = [], [0.0]
    for k in range(n + 1):
        t = a0 + (a1 - a0) * k / n; pts.append((FRONT_A * math.cos(t), CENTRE[1] + FRONT_B * math.sin(t), t))
        if k: s.append(s[-1] + math.dist(pts[-1][:2], pts[-2][:2]))
    return pts, s

def place_ring():
    """PLOTS (centre x, z per IDS) and YAWS (degrees, Unity: the model's front faces (-sin y, -cos y)). The fronts are
    spaced evenly along FRONT (equal gaps at the doors, where the curve is tightest); each house turns to the arc's
    inward normal and stands back from its front point by half its depth."""
    pts, s = _arc_points(); widths = [SIZES[b][0] for b in ORDER]
    gap = (s[-1] - sum(widths)) / (len(ORDER) - 1)
    at, cur = {}, 0.0
    for b, w in zip(ORDER, widths):
        mid = cur + w / 2; k = min(range(len(s)), key=lambda j: abs(s[j] - mid))
        x, z, t = pts[k]; nx, nz = -x / FRONT_A ** 2, -(z - CENTRE[1]) / FRONT_B ** 2; L = math.hypot(nx, nz); fx, fz = nx / L, nz / L
        d = SIZES[b][1]
        at[b] = ((x - fx * d / 2, z - fz * d / 2), math.degrees(math.atan2(-fx, -fz)) % 360)   # yaw: the front faces (fx, fz)
        cur += w + gap
    return [at[b][0] for b in IDS], [at[b][1] for b in IDS], gap

PLOTS, YAWS, HOUSE_GAP = place_ring()

def frame(i):
    """(centre, right, front) of plot i on the ground: right is the model's +x, front the way its door faces."""
    (cx, cz), y = PLOTS[i], math.radians(YAWS[i])
    return (cx, cz), (math.cos(y), -math.sin(y)), (-math.sin(y), -math.cos(y))

def world(i, u, v):
    """A point in plot i's own frame (u along its width, v toward its door) on the ground."""
    (cx, cz), r, f = frame(i)
    return (cx + u * r[0] + v * f[0], cz + u * r[1] + v * f[1])

def local(i, x, z):
    (cx, cz), r, f = frame(i); dx, dz = x - cx, z - cz
    return (dx * r[0] + dz * r[1], dx * f[0] + dz * f[1])

def corners(i, m=0.0):
    w, d = SIZES[IDS[i]]; return [world(i, su * (w / 2 + m), sv * (d / 2 + m)) for su, sv in ((-1, -1), (1, -1), (1, 1), (-1, 1))]

def on_plot(x, z, m):
    return any(abs(u) < SIZES[b][0] / 2 + m and abs(v) < SIZES[b][1] / 2 + m for b, (u, v) in zip(IDS, (local(i, x, z) for i in range(len(IDS)))))

def door(i): return world(i, 0.0, SIZES[IDS[i]][1] / 2)
def sign_point(i): w, d = SIZES[IDS[i]]; return world(i, w / 2 - 0.7, d / 2 + 1.5)

def in_square(x, z, m=0.0):
    return (x / (SQUARE_A + m)) ** 2 + ((z - CENTRE[1]) / (SQUARE_B + m)) ** 2 < 1

def door_path(i):
    """The path from the door straight out to the square's edge: (length) along the plot's front direction."""
    d = SIZES[IDS[i]][1] / 2; L_ = 0.0
    while L_ < 12 and not in_square(*world(i, 0, d + L_), -0.4): L_ += 0.1
    return L_ + 0.4

def paved(x, z, m=0.0):
    if in_square(x, z, m): return True
    if abs(x) < GATE_HALF_ST + m and CENTRE[1] + SQUARE_B - 1 < z < 0.5: return True   # the gate road
    for i in range(len(IDS)):
        u, v = local(i, x, z); d = SIZES[IDS[i]][1] / 2
        if abs(u) < PATH_HALF + m and d - 0.2 < v < d + door_path(i) + m: return True
    return False

WIZARD_HOME = (-6.0, -8.0)

def _seg_dist(p, a, b):
    ax, az = a; bx, bz = b; dx, dz = bx - ax, bz - az; L2 = dx * dx + dz * dz or 1e-9
    t = max(0.0, min(1.0, ((p[0] - ax) * dx + (p[1] - az) * dz) / L2)); return math.hypot(p[0] - ax - t * dx, p[1] - az - t * dz)

def _loop(a, b, n=28): return [(a * math.cos(k / n * math.tau), CENTRE[1] + b * math.sin(k / n * math.tau)) for k in range(n + 1)]
# the villagers' walks along the paving: a loop round the square, the gate road to the fountain, spokes out to doors.
# A walk whose last point is its first goes round and round; any other walks there and back.
WALKS = [_loop(SQUARE_A * 0.84, SQUARE_B * 0.8), _loop(SQUARE_A * 0.42, SQUARE_B * 0.55, 20), [(0.0, -3.0), (0.0, CENTRE[1] + 4.5)]]
for i in (IDS.index('forge'), IDS.index('library'), IDS.index('tavern'), IDS.index('cottages'), IDS.index('stormBell')):
    d = SIZES[IDS[i]][1] / 2; ex, ez = world(i, 0, d + 0.8)
    t = math.atan2((ez - CENTRE[1]) / SQUARE_B, ex / SQUARE_A)
    WALKS.append([(ex, ez), world(i, 0, d + door_path(i) + 1.0), (0.42 * SQUARE_A * math.cos(t), CENTRE[1] + 0.55 * SQUARE_B * math.sin(t))])


def town_props():
    """[(kind, x, z, r, yaw)]: r is the footprint the wizard walks round. A fixed seed: the kit and the game agree."""
    import random
    rng = random.Random(1114); out = []
    def clear(x, z, r, pm=0.5):
        return (inside(x, z, 2.6 + r) and not paved(x, z, pm + r) and not on_plot(x, z, 1.0 + r)
                and all(math.hypot(x - sx, z - sz) > 0.9 + r for sx, sz in map(sign_point, range(len(IDS))))
                and math.hypot(x - WIZARD_HOME[0], z - WIZARD_HOME[1]) > 1.6 + r
                and all(math.hypot(x - o[1], z - o[2]) > r + o[3] + 0.7 for o in out))
    # market stalls on the square, between the fountain and the houses, facing the fountain
    for a in range(190, 360, 6):   # the first free spot every 40 degrees or so, clear of every walk
        t = math.radians(a); x, z = 0.64 * SQUARE_A * math.cos(t), CENTRE[1] + 0.66 * SQUARE_B * math.sin(t)
        if all(_seg_dist((x, z), p, q) > 2.0 for w in WALKS for p, q in zip(w, w[1:])) and all(math.hypot(x - o[1], z - o[2]) > 9 for o in out):
            out.append(('stall', x, z, 1.3, math.degrees(math.atan2(x, z - CENTRE[1])) % 360))
    # benches in a ring round the fountain, facing it
    for a in (30, 90, 150, 210, 270, 330):
        t = math.radians(a + 15); x, z = 5.2 * math.cos(t), CENTRE[1] + 5.2 * math.sin(t)
        if all(math.hypot(x - o[1], z - o[2]) > 2.4 for o in out): out.append(('bench', x, z, 0.7, math.degrees(math.atan2(x, z - CENTRE[1])) % 360))
    # lamps round the square's rim, every 9 m, clear of the door paths and the gate road
    k = 0
    while k < 360:
        t = math.radians(k); x, z = (SQUARE_A + 0.5) * math.cos(t), CENTRE[1] + (SQUARE_B + 0.5) * math.sin(t)
        near_path = any(abs(local(i, x, z)[0]) < PATH_HALF + 0.8 and local(i, x, z)[1] > 0 for i in range(len(IDS))) or (abs(x) < GATE_HALF_ST + 0.8 and z > CENTRE[1])
        if not near_path and not on_plot(x, z, 0.6): out.append(('lamp', x, z, 0.2, 0.0)); k += 13
        else: k += 2
    # barrels by each door, on the side away from the sign
    for i, b in enumerate(IDS):
        w, d = SIZES[b]; x, z = world(i, -(w / 2 - 0.7), d / 2 + 0.9)
        if not paved(x, z, 0.7) and not on_plot(x, z, 0.5): out.append(('barrels', x, z, 0.5, YAWS[i]))   # against the front wall
    # gardens: trees between and behind the houses, flower beds and bushes in the front lawns (no trees in front of the
    # square: they would stand between the wall camera and the town)
    for kind, r, n, ok in (('tree', 1.1, 18, lambda x, z: z < CENTRE[1] + SQUARE_B - 2), ('tree', 0.8, 10, lambda x, z: z < CENTRE[1] + SQUARE_B - 2),
                           ('bed', 0.9, 12, lambda x, z: True), ('bush', 0.8, 10, lambda x, z: True)):
        k = tries = 0
        while k < n and tries < 6000:
            tries += 1; x, z = rng.uniform(-RX, RX), rng.uniform(-2 * RZ, -3)
            if ok(x, z) and clear(x, z, r): out.append((kind, x, z, r, rng.uniform(0, 360))); k += 1
    return out

TOWN_PROPS = town_props()

def check_town():
    """Raises on houses too close or under the wall, the square over a house, a prop on paving or a house, or a walk off the
    paving or through a prop. Returns (house gap, the closest two houses, prop count)."""
    n = len(IDS); worst = 99.0
    for i in range(n):
        for x, z in corners(i):
            if not inside(x, z, 2.0): raise ValueError(f'{IDS[i]} corner {x:.1f},{z:.1f} under the wall')
            if in_square(x, z, 1.2): raise ValueError(f'{IDS[i]} corner {x:.1f},{z:.1f} on the square')
        for j in range(i + 1, n):
            A, B = corners(i), corners(j)
            dmin = min(min(_seg_dist(p, B[k], B[(k + 1) % 4]) for k in range(4)) for p in A)
            dmin = min(dmin, min(min(_seg_dist(p, A[k], A[(k + 1) % 4]) for k in range(4)) for p in B))
            if on_plot(*A[0], 0) and False: pass
            worst = min(worst, dmin)
            if dmin < 3.0: raise ValueError(f'{IDS[i]} and {IDS[j]} only {dmin:.1f} m apart')
    for kind, x, z, r, _ in TOWN_PROPS:
        if on_plot(x, z, r): raise ValueError(f'{kind} at {x:.1f},{z:.1f} on a house')
        if kind not in ('stall', 'bench', 'lamp') and paved(x, z, r * 0.5): raise ValueError(f'{kind} at {x:.1f},{z:.1f} on the paving')
    for w in WALKS:
        for a, b in zip(w, w[1:]):
            for t in (0.0, 0.25, 0.5, 0.75, 1.0):
                x, z = a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t
                if not paved(x, z, 0.3): raise ValueError(f'walk point {x:.1f},{z:.1f} off the paving')
            for kind, px, pz, r, _ in TOWN_PROPS:
                if _seg_dist((px, pz), a, b) < r + 0.35: raise ValueError(f'walk {a}->{b} through a {kind}')
    return round(HOUSE_GAP, 2), round(worst, 2), len(TOWN_PROPS)
