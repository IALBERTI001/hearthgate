# Hearthgate 3D, the front (Nov 13 2026, second pass after the fresh-eyes review): the gatehouse, the curtain wall, the
# element towers and their caps, the road, the ground, the fountain and the enemies, built to the art standard. Imported
# by hg_kit.py, which owns the helpers, the palette and the export.
#
# Coordinates are Unity's: x right as seen from the city (the camera stands north, so screen right is world -x), y in
# Blender = Unity z (north, toward the camera), z up. The camera looks at the +y faces, so those carry the detail.
# Layout numbers (gate at the origin, wall along y = 0, towers at x = -7.5, -13, -18.5, -24 with their platform at
# 5.0 m, road centre y = 5.4, plaza at (8.5, -6)) match Assets/Scripts/Game/Field.cs; LayoutCheck verifies them.
#
# Owner's and reviewer's rules this file answers:
#   - one gatehouse, the hero of the frame (raised keep, red roof, flag, crest); towers spaced on the wall, shorter
#   - each element tower reads by its own silhouette and colour from across the screen (oversized glowing caps)
#   - a value structure: mid-tone world, the brightest colours only on enemies, caps and effects
#   - nothing pasted on: plinths, moss, rubble and grass at every foot; props clustered, never sprinkled
import bpy, bmesh, math, random
from mathutils import Vector
import hg_kit as K

R = random.Random(13)
STONES = ('stone', 'stone2', 'stone3')

def M(name, glow=0.0): return K.mat(name, glow)

def block(loc, size, m, bevel=0.035, rot=0.0):
    o = K.cube('blk', size, loc, m, bevel)
    for md in o.modifiers:
        if md.type == 'BEVEL': md.segments = 1          # one segment: the chamfer reads at game size, half the triangles
    if rot: o.rotation_euler = (0, 0, rot)
    return o

def mesh_from(name, verts, faces, m, bevel=0.0, smooth=False):
    me = bpy.data.meshes.new(name); me.from_pydata(verts, [], faces); me.update()
    o = bpy.data.objects.new(name, me); bpy.context.collection.objects.link(o)
    K.finish(o, m, bevel, smooth)
    for md in o.modifiers:
        if md.type == 'BEVEL': md.segments = 1
    return o

# ---------------------------------------------------------------------------------------------------------------------
# masonry: rows of chamfered stones laid on a face, joints showing, in three tints
def masonry(u0, u1, z0, z1, place, course=0.52, skip=None, lmin=0.55, lmax=1.25):
    z, row = z0, 0
    while z < z1 - 0.05:
        h = min(course * R.uniform(0.85, 1.15), z1 - z)
        u = u0 - (R.uniform(0.2, 0.5) if row % 2 else 0)
        while u < u1 - 0.05:
            L = R.uniform(lmin, lmax)
            a, b = max(u, u0), min(u + L, u1)
            if b - a > 0.15 and not (skip and skip(a, b, z, z + h)):
                loc, size, rot = place((a + b) / 2, z + h / 2, b - a - 0.05, h - 0.05)
                block(loc, size, M(R.choice(STONES)), 0.035, rot)
            u += L
        z += h; row += 1

def face_y(y, sign, depth=0.1):
    return lambda u, z, L, h: ((u, y + sign * depth / 2, z), (L, depth, h), 0.0)
def face_x(x, sign, depth=0.1):
    return lambda u, z, L, h: ((x + sign * depth / 2, u, z), (depth, L, h), 0.0)

def rubble(cx, cy, n, spread):
    for _ in range(n):
        s = R.uniform(0.18, 0.4)
        o = block((cx + R.uniform(-spread[0], spread[0]), cy + R.uniform(-spread[1], spread[1]), s * 0.3), (s * 1.3, s, s * 0.7), M(R.choice(('stone_dk', 'rock_dk'))), 0.06)
        o.rotation_euler = (R.uniform(-0.2, 0.2), R.uniform(-0.2, 0.2), R.uniform(0, 3.1))

def tufts(cx, cy, n, spread, mats=('grass', 'grass2', 'grass3')):
    for _ in range(n):
        x, y = cx + R.uniform(-spread[0], spread[0]), cy + R.uniform(-spread[1], spread[1])
        for k in range(3):
            o = K.cone('tuft', R.uniform(0.07, 0.12), R.uniform(0.3, 0.55), (x + R.uniform(-0.12, 0.12), y + R.uniform(-0.12, 0.12), 0.18), M(R.choice(mats)), 4)
            o.rotation_euler = (R.uniform(-0.35, 0.35), R.uniform(-0.35, 0.35), 0)

def moss(x0, x1, y, z=0.05, n=6):   # low dark-green clumps along the foot of a wall
    for _ in range(n):
        x = R.uniform(x0, x1)
        K.sphere('moss', R.uniform(0.25, 0.5), (x, y, z), M(R.choice(('moss', 'grass2'))), (1.6, 0.7, 0.45), 1)

def merlons(u0, u1, z, place, w=0.62, gap=0.42, h=0.6, vary=0.0):
    n = int((u1 - u0 + gap) // (w + gap)); start = u0 + ((u1 - u0) - (n * w + (n - 1) * gap)) / 2
    for i in range(n):
        u = start + i * (w + gap) + w / 2
        hh = h * (1 + R.uniform(-vary, vary))
        loc, size = place(u, z + hh / 2, w, hh)
        block(loc, size, M(R.choice(STONES)), 0.045)

def cloth(name, x0, x1, ztop, zbot, y, m, wave=0.08, tail=True, axis='x', nx=6, nz=12):
    """A hanging (axis 'x') or flying (axis 'y' for a flag along +x from a pole) cloth with a gentle wave."""
    vs, fs = [], []
    for j in range(nz + 1):
        t = j / nz
        for i in range(nx + 1):
            u = i / nx
            if axis == 'x':
                xx = x0 + u * (x1 - x0); zz = ztop - t * (ztop - zbot)
                if tail and j == nz: zz = zbot + abs(u - 0.5) * 0.9 * (x1 - x0)
                yy = y + wave * math.sin(t * 5.5 + u * 1.3) * t
            else:   # a flag: u runs along +x away from the pole, t down its height
                xx = x0 + u * (x1 - x0); zz = ztop - t * (ztop - zbot) - 0.12 * u * u
                yy = y + wave * math.sin(u * 6.0 + t) * u
            vs.append((xx, yy, zz))
    for j in range(nz):
        for i in range(nx):
            a = j * (nx + 1) + i
            fs.append((a, a + 1, a + nx + 2, a + nx + 1))
    o = mesh_from(name, vs, fs, m, 0.0, smooth=True)
    sol = o.modifiers.new('thick', 'SOLIDIFY'); sol.thickness = 0.05
    return o

def banner(x, y, ztop, zbot, w):
    K.cyl('rod', 0.05, w + 0.35, (x, y + 0.12, ztop + 0.05), M('iron'), 8, 0.0).rotation_euler = (0, math.pi / 2, 0)
    for s in (-1, 1): K.sphere('knob', 0.08, (x + s * (w / 2 + 0.18), y + 0.12, ztop + 0.05), M('gold'), sub=2)
    cloth('banner', x - w / 2, x + w / 2, ztop, zbot, y + 0.1, M('banner'))
    for s in (-1, 1): block((x + s * (w / 2 - 0.06), y + 0.2, (ztop + zbot) / 2 + 0.2), (0.08, 0.04, ztop - zbot - 0.5), M('gold'), 0.0)
    d = block((x, y + 0.22, ztop - (ztop - zbot) * 0.36), (0.44, 0.04, 0.44), M('gold'), 0.0); d.rotation_euler = (0, math.pi / 4, 0)
    K.cone('sigil', 0.11, 0.3, (x, y + 0.26, ztop - (ztop - zbot) * 0.36 + 0.02), M('fire', 2.0), 8).rotation_euler = (0, 0, 0)

def torch(x, y, z):
    block((x, y + 0.08, z - 0.15), (0.16, 0.16, 0.34), M('iron'), 0.02)
    K.cyl('cup', 0.18, 0.2, (x, y + 0.22, z + 0.08), M('iron'), 10, 0.02, r2=0.1)
    K.cone('flame', 0.15, 0.55, (x, y + 0.22, z + 0.43), M('fire', 4.0), 10)
    K.cone('flame_in', 0.08, 0.35, (x, y + 0.25, z + 0.36), M('gold', 5.0), 8)

def window(x, y, z, w=0.55, h=0.8, arched=True):   # a warm lit window set in a stone frame (+y face)
    block((x, y + 0.03, z), (w + 0.2, 0.08, h + 0.2), M('stone3'), 0.02)
    block((x, y + 0.07, z - 0.05), (w, 0.04, h - 0.1), M('window', 1.6), 0.0)
    if arched: K.cyl('arch', w / 2, 0.04, (x, y + 0.07, z + h / 2 - 0.1), M('window', 1.6), 12, 0.0).rotation_euler = (math.pi / 2, 0, 0)
    block((x, y + 0.1, z), (0.05, 0.03, h), M('wood_dk'), 0.0)
    block((x, y + 0.1, z), (w, 0.03, 0.05), M('wood_dk'), 0.0)

def hip_roof(cx, cy, z0, hx, hy, rise, ridge, m, battens=True):
    """A hipped roof: base rectangle (half sizes hx, hy) at z0, ridge along x of half length 'ridge'."""
    vs = [(cx - hx, cy - hy, z0), (cx + hx, cy - hy, z0), (cx + hx, cy + hy, z0), (cx - hx, cy + hy, z0),
          (cx - ridge, cy, z0 + rise), (cx + ridge, cy, z0 + rise)]
    fs = [(0, 1, 5, 4), (2, 3, 4, 5), (1, 2, 5), (3, 0, 4), (3, 2, 1, 0)]
    o = mesh_from('roof', vs, fs, m, 0.03)
    if battens:   # tile courses on the front slope: thin strips parallel to the eaves
        for k in range(1, 6):
            t = k / 6
            z = z0 + rise * t; yy = cy + hy * (1 - t) + 0.03; half = hx - (hx - ridge) * t
            block((cx, yy, z), (2 * half, 0.06, 0.06), M('roof_red' if m.name.endswith('terra') else 'wood_dk'), 0.0).rotation_euler = (-math.atan2(rise, hy), 0, 0)
    return o

# ---------------------------------------------------------------------------------------------------------------------
# the gatehouse: the hero. An arched passage with a half-raised portcullis and closed doors, voussoirs and quoins, a
# corbel band and crenellated parapet, a raised keep with lit windows, a steep red hipped roof, a flag and the hearth crest
GX, GY = 4.0, 2.1          # half width, half depth
AW, ASPR = 1.5, 2.4        # arch half width and springing height
BODY, PARA = 4.8, 5.45     # top of the body, top of the parapet
KX, KY, KTOP = 2.6, 1.45, 7.5   # the keep: half width, half depth, top of its walls

def arch_top(x): return ASPR + math.sqrt(max(0.0, AW * AW - x * x))

def gate():
    K.cube('plinth', (2 * GX + 0.5, 2 * GY + 0.5, 0.6), (0, 0, 0.3), M('stone_dk'), 0.06)
    body = K.cube('body', (2 * GX, 2 * GY, BODY - 0.6), (0, 0, 0.6 + (BODY - 0.6) / 2), M('mortar'), 0.0)
    cut = K.cube('cut', (2 * AW, 2 * GY + 2, ASPR + 0.1), (0, 0, ASPR / 2 - 0.05), M('mortar'), 0.0)
    bpy.ops.mesh.primitive_cylinder_add(vertices=40, radius=AW, depth=2 * GY + 2, location=(0, 0, ASPR), rotation=(math.pi / 2, 0, 0))
    cy = bpy.context.object
    for c in (cut, cy):
        bo = body.modifiers.new('arch', 'BOOLEAN'); bo.operation = 'DIFFERENCE'; bo.object = c; bo.solver = 'EXACT'
        bpy.context.view_layer.objects.active = body; bpy.ops.object.modifier_apply(modifier=bo.name)
    for c in (cut, cy): bpy.data.objects.remove(c, do_unlink=True)
    K.cube('floor', (2 * AW, 2 * GY, 0.62), (0, 0, 0.31), M('cobble2'), 0.0)
    for s in (-1, 1):
        for k in range(4):
            x = s * (AW - 0.19 - k * 0.37)
            h = arch_top(abs(x) + 0.18) - 0.65
            block((x, -0.9, 0.62 + h / 2), (0.35, 0.16, h), M('wood_dk' if k % 2 else 'wood'), 0.02)
    for zb in (1.1, 2.1, 3.0):
        w = 2 * math.sqrt(max(0.0, AW * AW - max(0.0, zb - ASPR) ** 2)) - 0.1 if zb > ASPR else 2 * AW - 0.1
        block((0, -0.8, zb), (w, 0.05, 0.12), M('iron'), 0.0)
    yp = GY - 0.45
    for i in range(8):
        x = -AW + 0.2 + i * (2 * AW - 0.4) / 7
        top = arch_top(abs(x)) - 0.05
        block((x, yp, (1.35 + top) / 2), (0.1, 0.1, top - 1.35), M('iron'), 0.0)
        K.cone('spike', 0.08, 0.3, (x, yp, 1.2), M('iron'), 6).rotation_euler = (math.pi, 0, 0)
    for zb in (1.5, 2.05, 2.6, 3.15, 3.65):
        if zb >= arch_top(0): continue
        hw = math.sqrt(max(0.0, AW * AW - max(0.0, zb - ASPR) ** 2)) if zb > ASPR else AW
        block((0, yp, zb), (2 * hw - 0.12, 0.08, 0.08), M('iron'), 0.0)
    def skip_front(a, b, z0, z1):
        for x in (a, b, (a + b) / 2):
            ax = abs(x)
            if ax < 2.1 and z0 < ASPR + math.sqrt(max(0.0, 2.1 ** 2 - min(ax, 2.1) ** 2)): return True
        return False
    masonry(-GX, GX, 0.6, BODY, face_y(GY, 1), skip=skip_front)
    for s in (-1, 1): masonry(-GY, GY, 0.6, BODY, face_x(s * GX, s))
    n = 11
    for i in range(n):
        a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
        r0, r1 = AW + 0.02, AW + (0.7 if i == n // 2 else 0.56)
        pts = [(math.cos(a) * r, math.sin(a) * r + ASPR) for (r, a) in ((r0, a0 + 0.012), (r1, a0 + 0.012), (r1, a1 - 0.012), (r0, a1 - 0.012))]
        depth = 0.2 if i == n // 2 else 0.14
        vs = [(x, GY + yy, z) for yy in (0.0, depth) for (x, z) in pts]
        fs = [(0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)]
        mesh_from('vous', vs, fs, M('stone3' if i == n // 2 else R.choice(STONES)), 0.025)
    for s in (-1, 1):
        z, k = 0.6, 0
        while z < ASPR - 0.05:
            L = 0.62 if k % 2 else 0.42; h = min(0.45, ASPR - z)
            block((s * (AW + L / 2 + 0.02), GY + 0.07, z + h / 2), (L - 0.04, 0.14, h - 0.04), M('stone3'), 0.03)
            z += h; k += 1
    for s in (-1, 1): block((s * (AW + 0.05), 0, 0.62 + (ASPR - 0.62) / 2), (0.1, 2 * GY - 0.2, ASPR - 0.62), M('stone_dk'), 0.0)
    # corbel band, parapet and merlons (varied a little)
    for x in [-GX + 0.3 + i * 0.62 for i in range(13)]:
        block((x, GY + 0.14, BODY - 0.12), (0.34, 0.28, 0.24), M('stone_dk'), 0.03)
        block((x, GY + 0.08, BODY - 0.32), (0.26, 0.16, 0.18), M('stone_dk'), 0.03)
    K.cube('parapet', (2 * GX + 0.36, 2 * GY + 0.36, PARA - BODY), (0, 0, BODY + (PARA - BODY) / 2), M('stone'), 0.05)
    masonry(-GX - 0.18, GX + 0.18, BODY, PARA, face_y(GY + 0.18, 1, 0.06), course=0.33, lmin=0.4, lmax=0.8)
    K.cube('walk', (2 * GX + 0.2, 2 * GY + 0.2, 0.08), (0, 0, PARA + 0.04), M('cobble2'), 0.0)
    merlons(-GX - 0.1, GX + 0.1, PARA, lambda u, z, w, h: ((u, GY + 0.0, z), (w, 0.38, h)), vary=0.08)
    merlons(-GX - 0.1, GX + 0.1, PARA, lambda u, z, w, h: ((u, -GY, z), (w, 0.38, h)), vary=0.08)
    for s in (-1, 1): merlons(-GY + 0.4, GY - 0.4, PARA, lambda u, z, w, h, s=s: ((s * GX, u, z), (0.38, w, h)), vary=0.08)
    # the keep: masonry walls, two lit arched windows and the hearth crest on its front, corbels under a red hipped roof
    K.cube('keep', (2 * KX, 2 * KY, KTOP - PARA), (0, 0, PARA + (KTOP - PARA) / 2), M('mortar'), 0.0)
    masonry(-KX, KX, PARA, KTOP, face_y(KY, 1), course=0.45, lmin=0.5, lmax=1.0,
            skip=lambda a, b, z0, z1: (z1 > 5.75 and z0 < 7.05 and ((a < -0.75 and b > -1.85) or (a < 1.85 and b > 0.75) or (a < 0.6 and b > -0.6))))
    for s in (-1, 1): masonry(-KY, KY, PARA, KTOP, face_x(s * KX, s), course=0.45)
    for s in (-1, 1): window(s * 1.3, KY + 0.1, 6.4, 0.6, 0.95)
    K.cyl('crest_back', 0.6, 0.12, (0, KY + 0.12, 6.45), M('stone3'), 24, 0.02).rotation_euler = (math.pi / 2, 0, 0)
    K.cyl('crest', 0.5, 0.1, (0, KY + 0.2, 6.45), M('gold'), 24, 0.02).rotation_euler = (math.pi / 2, 0, 0)
    K.cone('crest_flame', 0.26, 0.66, (0, KY + 0.3, 6.5), M('fire', 2.5), 10)
    K.cone('crest_core', 0.13, 0.4, (0, KY + 0.34, 6.44), M('gold', 3.0), 8)
    for x in [-KX + 0.25 + i * 0.58 for i in range(10)]:
        block((x, KY + 0.12, KTOP - 0.1), (0.3, 0.24, 0.2), M('stone_dk'), 0.03)
    block((0, 0, KTOP + 0.06), (2 * KX + 0.5, 2 * KY + 0.5, 0.12), M('wood_dk'), 0.02)   # eaves board
    hip_roof(0, 0, KTOP + 0.12, KX + 0.45, KY + 0.45, 1.8, 1.3, M('roof_terra'))
    for s in (-1, 1): K.cone('finial', 0.1, 0.55, (s * 1.3, 0, KTOP + 2.2), M('gold'), 8)
    # the flag on the ridge, big enough to read at game size
    K.cyl('pole', 0.06, 2.4, (0, 0, KTOP + 3.3), M('wood_dk'), 8, 0.0)
    K.sphere('pole_knob', 0.12, (0, 0, KTOP + 4.52), M('gold'), sub=2)
    cloth('flag', 0.06, 1.9, KTOP + 4.3, KTOP + 3.25, 0.0, M('banner'), wave=0.16, tail=False, axis='y', nx=8, nz=4)
    # banners on the piers, torches beside the arch
    for s in (-1, 1):
        banner(s * 2.95, GY + 0.1, BODY - 0.45, 2.0, 0.92)
        torch(s * 2.1, GY + 0.1, 2.75)
    for s in (-1, 1):
        rubble(s * (GX + 0.25), GY + 0.3, 5, (0.35, 0.3)); tufts(s * (GX + 0.4), GY + 0.45, 4, (0.3, 0.25))
        moss(s * GX - 0.6 * s, s * GX + 0.2 * s, GY + 0.3, n=3)
    return K.export('gate')

# ---------------------------------------------------------------------------------------------------------------------
# the curtain wall: from rocky hills on the left to rocky hills on the right; stones, a coping course, varied merlons,
# buttresses between the towers, moss and grass at the foot
WX0, WX1, WH = -31.0, 21.0, 3.4
TOWERS = (-7.5, -13.0, -18.5, -24.0)
def wall():
    for (a, b) in ((WX0, -GX + 0.02), (GX - 0.02, WX1)):
        K.cube('core', (b - a, 1.8, WH), ((a + b) / 2, 0, WH / 2), M('mortar'), 0.0)
        K.cube('batter', (b - a, 2.2, 0.55), ((a + b) / 2, 0.05, 0.27), M('stone_dk'), 0.05)
        masonry(a, b, 0.55, WH - 0.25, face_y(0.9, 1), lmin=0.7, lmax=1.5)
        K.cube('coping', (b - a, 2.05, 0.25), ((a + b) / 2, 0.04, WH - 0.12), M('stone3'), 0.04)
        K.cube('walk', (b - a, 1.9, 0.18), ((a + b) / 2, 0, WH + 0.09), M('stone2'), 0.03)
        merlons(a + 0.2, b - 0.2, WH + 0.18, lambda u, z, w, h: ((u, 0.72, z), (w, 0.42, h)), w=0.7, gap=0.48, h=0.62, vary=0.12)
        merlons(a + 0.2, b - 0.2, WH + 0.18, lambda u, z, w, h: ((u, -0.72, z), (w, 0.32, h)), w=0.7, gap=0.48, h=0.42)
        x = a + 2.2
        while x < b - 1.5:
            if all(abs(x - tx) > 2.7 for tx in TOWERS):
                block((x, 1.12, 1.0), (0.75, 0.48, 2.0), M(R.choice(STONES)), 0.05)
                block((x, 1.02, 2.12), (0.66, 0.32, 0.35), M('stone_dk'), 0.05)
            x += R.uniform(4.2, 5.6)
        moss(a, b, 1.25, n=int((b - a) / 2.5))
        for _ in range(int((b - a) / 4)):
            xx = R.uniform(a + 1, b - 1)
            if R.random() < 0.5: rubble(xx, 1.35, 2, (0.6, 0.15))
            tufts(xx + R.uniform(-1, 1), 1.45, 2, (0.6, 0.15))
    return K.export('wall')

# ---------------------------------------------------------------------------------------------------------------------
# the element tower: twelve-sided, battered foot, coursed stone on each face, arrow slits to the road, a corbel ring and a
# crenellated platform at 5.0 m (Field.TOWER_TOP) where the element cap stands
TR, TTOP, TN = 1.75, 5.0, 12
def ring_pts(r, n=TN, off=0.0): return [(math.cos((i + off) / n * math.tau) * r, math.sin((i + off) / n * math.tau) * r) for i in range(n)]
def prism(name, r0, r1, z0, z1, m, n=TN, bevel=0.03):
    p0, p1 = ring_pts(r0, n, 0.5), ring_pts(r1, n, 0.5)
    vs = [(x, y, z0) for x, y in p0] + [(x, y, z1) for x, y in p1]
    fs = [tuple(range(n - 1, -1, -1)), tuple(range(n, 2 * n))] + [(i, (i + 1) % n, n + (i + 1) % n, n + i) for i in range(n)]
    return mesh_from(name, vs, fs, m, bevel)
def tower_base():
    prism('foot', TR + 0.4, TR + 0.08, 0, 1.0, M('stone_dk'))
    prism('core', TR, TR, 0.9, TTOP - 0.55, M('mortar'), bevel=0.0)
    side = 2 * TR * math.sin(math.pi / TN)
    for i in range(TN):
        a = (i + 0.5) / TN * math.tau + math.pi / TN   # the face's middle direction
        rm = TR * math.cos(math.pi / TN)
        cx, cy = math.cos(a) * rm, math.sin(a) * rm
        tx, ty = -math.sin(a), math.cos(a)
        def place(u, z, L, h, cx=cx, cy=cy, tx=tx, ty=ty, a=a):
            return ((cx + tx * u + math.cos(a) * 0.05, cy + ty * u + math.sin(a) * 0.05, z), (0.1, L, h), a)
        masonry(-side / 2, side / 2, 1.0, TTOP - 0.6, place, course=0.48, lmin=0.3, lmax=0.62)
    for k, a in enumerate((math.pi / 2 - 0.55, math.pi / 2 + 0.55, math.pi / 2)):
        zc = 2.2 if k < 2 else 3.7
        o = block((math.cos(a) * (TR + 0.08), math.sin(a) * (TR + 0.08), zc), (0.06, 0.16, 0.75), M('pupil'), 0.0); o.rotation_euler = (0, 0, a)
        o = block((math.cos(a) * (TR + 0.1), math.sin(a) * (TR + 0.1), zc - 0.42), (0.1, 0.36, 0.1), M('stone3'), 0.02); o.rotation_euler = (0, 0, a)
    for i in range(TN * 2):
        a = (i + 0.5) / (TN * 2) * math.tau
        o = block((math.cos(a) * (TR + 0.12), math.sin(a) * (TR + 0.12), TTOP - 0.62), (0.3, 0.26, 0.26), M('stone_dk'), 0.03); o.rotation_euler = (0, 0, a)
    prism('ring', TR + 0.32, TR + 0.32, TTOP - 0.5, TTOP, M('stone'))
    prism('floor', TR + 0.1, TR + 0.1, TTOP - 0.02, TTOP + 0.06, M('cobble2'), bevel=0.0)
    for i in range(TN):
        a = (i + 0.5) / TN * math.tau + math.pi / TN
        o = block((math.cos(a) * (TR + 0.14), math.sin(a) * (TR + 0.14), TTOP + 0.3), (0.32, 0.55, 0.6 * R.uniform(0.92, 1.08)), M(R.choice(STONES)), 0.04); o.rotation_euler = (0, 0, a)
    rubble(0, TR + 0.5, 4, (1.1, 0.2)); tufts(0, TR + 0.6, 4, (1.2, 0.2)); moss(-1.2, 1.2, TR + 0.35, n=3)
    return K.export('tower_base')

# ---------------------------------------------------------------------------------------------------------------------
# element caps: oversized, each its own silhouette and colour, a glowing core. Origin at the platform floor.
#   stone: a slate cone roof (no element yet)  fire: a squat iron furnace crown with a roaring flame
#   frost: a tall spire of glowing ice           storm: a needle of bronze coils under a crackling violet orb
def top(kind):
    if kind == 'stone':
        prism('drum', 1.35, 1.35, 0.0, 0.6, M('stone2'))
        # a warm shingle roof, lower than the gatehouse's, with a brass ring and an empty socket that glows faintly:
        # this tower is waiting for an element (fresh-eyes review 2: four blue cones outranked the gatehouse)
        K.cyl('eave', 2.1, 0.16, (0, 0, 0.66), M('gold'), TN, 0.02)
        K.cyl('roof', 2.05, 2.1, (0, 0, 1.79), M('shingle'), TN, 0.0, r2=0.1)
        for k in range(1, 4):
            r = 2.05 * (1 - k / 4) + 0.1 * k / 4
            K.cyl('course', r + 0.04, 0.08, (0, 0, 0.74 + 2.1 * k / 4), M('shingle2'), TN, 0.0)
        K.sphere('socket', 0.26, (0, 0, 2.95), M('fire_core', 0.8), sub=2)
        K.cyl('spire', 0.06, 0.9, (0, 0, 3.35), M('gold'), 6, 0.0)
        cloth('pennant', 0.06, 1.0, 3.75, 3.35, 0.0, M('banner'), wave=0.1, tail=False, axis='y', nx=5, nz=2)
    elif kind == 'fire':
        prism('crown', 2.0, 1.85, -0.2, 0.55, M('iron'))
        for i in range(TN):   # glowing vents round the crown
            a = (i + 0.5) / TN * math.tau + math.pi / TN
            o = block((math.cos(a) * 1.93, math.sin(a) * 1.93, 0.18), (0.06, 0.32, 0.22), M('fire', 3.0), 0.0); o.rotation_euler = (0, 0, a)
        for i in range(6):   # iron prongs holding the bowl
            a = i / 6 * math.tau
            o = block((math.cos(a) * 1.35, math.sin(a) * 1.35, 0.95), (0.16, 0.16, 0.9), M('iron'), 0.02)
            o.rotation_euler = (-math.sin(a) * 0.35, math.cos(a) * 0.35, 0)
        K.cyl('bowl', 1.5, 0.7, (0, 0, 1.2), M('iron'), 20, 0.04, r2=0.95)
        K.cyl('rim', 1.55, 0.12, (0, 0, 1.55), M('gold'), 20, 0.02)
        for i in range(10):
            a = i / 10 * math.tau; r = 0.5 + 0.45 * (i % 2)
            K.sphere('coal', R.uniform(0.22, 0.32), (math.cos(a) * r, math.sin(a) * r, 1.58), M('fire', 3.0), (1, 1, 0.7), 2)
        K.cone('flame', 1.15, 2.9, (0, 0, 3.0), M('fire', 3.5), 14)
        for i in range(5):
            a = i / 5 * math.tau + 0.4
            K.cone('tongue', 0.45, R.uniform(1.4, 2.0), (math.cos(a) * 0.65, math.sin(a) * 0.65, 2.3), M('fire', 3.5), 10)
        K.cone('core', 0.6, 2.0, (0, 0, 2.55), M('fire_core', 3.0), 12)
        for i in range(7): K.sphere('spark', R.uniform(0.07, 0.12), (R.uniform(-0.9, 0.9), R.uniform(-0.9, 0.9), R.uniform(3.6, 4.8)), M('fire_core', 3.0), sub=1)
    elif kind == 'frost':
        prism('ring', 1.55, 1.45, -0.1, 0.45, M('stone3'))
        for i in range(TN):
            a = (i + 0.5) / TN * math.tau
            K.sphere('rime', 0.2, (math.cos(a) * 1.45, math.sin(a) * 1.45, 0.5), M('snow'), (1, 1, 0.55), 1)
        spec = [(0.5, 2.6, 0, 0, 0), (0.32, 1.9, 0.75, 0.3, 0.3), (0.3, 1.75, -0.7, 0.35, -0.32), (0.28, 1.5, 0.2, -0.75, 0.25),
                (0.26, 1.3, -0.55, -0.55, -0.28), (0.22, 1.1, 0.85, -0.35, 0.38), (0.2, 0.95, -0.95, 0.0, -0.4)]
        for (r, h, x, y, tilt) in spec:
            rx = tilt * (1 if y >= 0 else -1) * min(1.0, abs(y) * 2); ry = -tilt * (1 if x >= 0 else -1) * min(1.0, abs(x) * 2)
            for (o_r, o_h, off, m) in ((r, h, 0.0, M('frost', 1.4)), (r * 0.45, h * 0.9, 0.02, M('frost_dk', 1.0))):
                o = K.cyl('crystal', o_r, o_h, (x, y, 0.4 + o_h / 2), m, 6, 0.0)
                tip = K.cone('tip', o_r, o_r * 2.2, (x, y, 0.4 + o_h + o_r * 1.1), m, 6)
                for q in (o, tip): q.rotation_euler = (rx, ry, 0.3)
        for i in range(6): K.sphere('mote', 0.08, (R.uniform(-1, 1), R.uniform(-1, 1), R.uniform(2.6, 4.2)), M('white_gloss', 2.0), sub=1)
    elif kind == 'storm':
        prism('base', 1.4, 1.2, -0.1, 0.5, M('stone3'))
        K.cyl('needle', 0.32, 3.6, (0, 0, 2.3), M('gold'), 10, 0.0, r2=0.06)
        for k in range(5):
            bpy.ops.mesh.primitive_torus_add(major_radius=0.55 - k * 0.08, minor_radius=0.06, location=(0, 0, 1.1 + k * 0.62))
            K.finish(bpy.context.object, M('gold'), 0.0)
        for s in (-1, 1):   # two arms carrying small orbs
            o = block((s * 0.55, 0, 2.2), (1.1, 0.1, 0.1), M('gold'), 0.0); o.rotation_euler = (0, s * -0.4, 0)
            K.sphere('arm_orb', 0.16, (s * 1.08, 0, 2.45), M('storm', 3.0), sub=2)
        K.sphere('orb', 0.72, (0, 0, 4.6), M('storm', 3.5), sub=3)
        K.sphere('orb_core', 0.36, (0, 0, 4.6), M('storm', 3.0), sub=2)
        for k in range(3):
            bpy.ops.mesh.primitive_torus_add(major_radius=1.05, minor_radius=0.05, location=(0, 0, 4.6), rotation=(k * 1.05, k * 0.7, 0))
            K.finish(bpy.context.object, M('storm', 2.5), 0.0)
    return K.export('top_' + kind)

# ---------------------------------------------------------------------------------------------------------------------
# enemies. Each faces -y; Field.WALK turns them along the road, three-quarters to the camera.
def face(y, z, spread, r=0.2, brows=True, mouth=0.13, body='slime_dk'):
    # big close-set eyes with large pupils and one catchlight, brows that sit on the body, an open smile: reads as a
    # character at 30 px (fresh-eyes review: "googly eyes ... white extra eyes read as noise")
    for s in (-1, 1):
        K.sphere('eye', r, (s * spread, y, z), M('eye'), (1, 0.62, 1.12), 3)
        K.sphere('pupil', r * 0.74, (s * spread * 0.94, y - r * 0.5, z - r * 0.06), M('pupil'), (1, 0.55, 1.15), 2)
        K.sphere('catch', r * 0.22, (s * spread * 0.82, y - r * 0.9, z + r * 0.3), M('white_gloss', 0.5), sub=1)
        if brows:
            o = block((s * spread, y + r * 0.15, z + r * 1.12), (r * 1.7, r * 0.6, r * 0.42), M(body), 0.04)
            o.rotation_euler = (0, -s * 0.38, 0)   # inner ends down: determined, not worried
    if mouth:
        K.sphere('mouth', mouth, (0, y + 0.02, z - r * 1.75), M('pupil'), (1.5, 0.55, 0.95), 2)
        K.sphere('tongue', mouth * 0.6, (0, y - 0.04, z - r * 1.95), M('blush'), (1.3, 0.5, 0.55), 1)

def slime():   # 1.5 m: a glossy teal teardrop with a darker puddle foot, an angry-cute face
    bpy.ops.mesh.primitive_uv_sphere_add(segments=32, ring_count=18, radius=0.8, location=(0, 0, 0.72))
    o = bpy.context.object
    for v in o.data.vertices:
        x, y, z = v.co.x, v.co.y, v.co.z
        if z > 0: z *= 1.0 + 0.22 * (z / 0.8) ** 3
        else: z *= 0.7; k = 1.06 + 0.12 * (-z / 0.56); x *= k; y *= k
        v.co = Vector((x, y, z))
    o.data.update(); K.finish(o, M('slime_gloss'))
    K.cyl('puddle', 1.0, 0.12, (0, 0, 0.06), M('slime_dk'), 24, 0.0, r2=0.9)
    for (x, y, s) in ((0.78, 0.25, 0.2), (-0.72, 0.4, 0.17), (0.35, -0.82, 0.14), (-0.5, -0.66, 0.12)):
        K.sphere('drip', s, (x, y, s * 0.35), M('slime_gloss'), (1.25, 1.25, 0.5), 2)
    face(-0.66, 0.98, 0.22, 0.2, mouth=0.12)
    return K.export('slime')

def beetle():   # a shiny armoured beetle: domed split shell, horned head, thick jointed legs
    bpy.ops.mesh.primitive_uv_sphere_add(segments=28, ring_count=14, radius=0.75, location=(0, 0.15, 0.65))
    sh = bpy.context.object; sh.scale = (0.95, 1.25, 0.7); bpy.ops.object.transform_apply(scale=True); K.finish(sh, M('beetle_gloss'))
    block((0, 0.15, 1.17), (0.06, 1.6, 0.05), M('pupil'), 0.0)
    K.sphere('head', 0.42, (0, -0.85, 0.6), M('iron'), (1, 0.9, 0.85), 2)
    K.cone('horn', 0.16, 0.75, (0, -1.15, 0.98), M('gold'), 8).rotation_euler = (-0.6, 0, 0)
    for s in (-1, 1):
        o = K.cone('mandible', 0.08, 0.4, (s * 0.2, -1.2, 0.45), M('iron'), 6); o.rotation_euler = (-1.4, 0, s * 0.4)
        for k in range(3):
            y = -0.45 + k * 0.5
            o = block((s * 0.85, y, 0.42), (0.55, 0.18, 0.18), M('iron'), 0.04); o.rotation_euler = (0, s * -0.5, 0)
            block((s * 1.12, y, 0.18), (0.18, 0.2, 0.42), M('iron'), 0.04)
    face(-1.08, 0.72, 0.2, 0.13, brows=True, mouth=0)
    return K.export('beetle')

def golem():   # a boss: lumpy boulders bound by glowing cracks, big fists, a small glowing-eyed head
    def boulder(loc, r, sc):
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=2, radius=r, location=loc)
        b = bpy.context.object; b.scale = sc
        for v in b.data.vertices: v.co *= R.uniform(0.86, 1.12)
        b.data.update(); bpy.ops.object.transform_apply(scale=True); K.finish(b, M(R.choice(('golem', 'rock', 'rock_dk'))), 0.0, smooth=False)
        return b
    boulder((0, 0, 1.15), 0.8, (1.15, 0.9, 1.0))            # chest
    boulder((0, 0.05, 0.45), 0.55, (1.1, 0.85, 0.7))         # hips
    boulder((0, -0.15, 1.95), 0.38, (1.0, 0.95, 0.85))       # head
    for s in (-1, 1):
        boulder((s * 1.0, 0, 1.35), 0.42, (1, 1, 1))         # shoulder
        boulder((s * 1.15, -0.1, 0.8), 0.36, (0.9, 0.9, 1.2))  # arm
        boulder((s * 1.2, -0.2, 0.32), 0.42, (1.1, 1.1, 0.9))  # fist
        boulder((s * 0.42, 0, 0.2), 0.32, (1, 1.1, 0.75))      # foot
        K.sphere('eye', 0.08, (s * 0.15, -0.5, 2.0), M('storm', 4.0), sub=1)
    for (x, z, rot) in ((0.0, 1.2, 0.3), (-0.35, 1.0, -0.6), (0.4, 1.35, 1.0), (0.1, 0.5, 0.0)):   # glowing cracks on the front
        o = block((x, -0.8, z), (0.5, 0.05, 0.07), M('storm', 3.5), 0.0); o.rotation_euler = (0, rot, 0)
    K.sphere('core', 0.18, (0, -0.86, 1.15), M('storm', 4.0), sub=2)
    return K.export('golem')

# ---------------------------------------------------------------------------------------------------------------------
# the road: worn earth along the front of the wall with darker ruts, pebbles and grass along its edges, a cobbled
# forecourt centred on the gate, a low fence on the meadow side (0.7 m: never hides an enemy)
ROADY, RHALF = 5.4, 3.0
CAVE_X = 31.0    # the cave mouth the waves come out of, world x = -CAVE_X (Field.SPAWN_Z)
def road():
    K.cube('way', (CAVE_X + 2.5, 2 * RHALF, 0.06), (-(CAVE_X + 2.5) / 2 + 2, ROADY, 0.03), M('dirt'), 0.0)
    for dy in (-1.05, 1.05):
        K.cube('rut', (CAVE_X + 2.5, 0.55, 0.02), (-(CAVE_X + 2.5) / 2 + 2, ROADY + dy, 0.065), M('dirt_dk'), 0.0)
    # setts thinning out from the forecourt into the earth road, so the two meet without a seam
    for _ in range(60):
        x = -GX - 1.6 - abs(R.gauss(0, 1.6)); y = ROADY + R.uniform(-RHALF + 0.3, RHALF - 0.3)
        o = block((x, y, 0.075), (R.uniform(0.28, 0.45), 0.27, 0.08), M(R.choice(('cobble', 'cobble2', 'stone2'))), 0.04); o.rotation_euler = (0, 0, R.uniform(-0.25, 0.25))
    for _ in range(110):   # pebbles in clusters along both edges, grass bleeding in
        side = R.choice((-1, 1)); x = R.uniform(-CAVE_X + 1, -GX - 1.0)
        for k in range(R.randint(1, 3)):
            s = R.uniform(0.14, 0.3)
            o = block((x + R.uniform(-0.4, 0.4), ROADY + side * (RHALF - R.uniform(0.05, 0.45)), 0.07), (s * 1.3, s, s * 0.5), M(R.choice(('cobble2', 'rock', 'rock_dk'))), 0.03)
            o.rotation_euler = (0, 0, R.uniform(0, 3.1))
    for _ in range(26):
        tufts(R.uniform(-CAVE_X + 1, -GX - 1.5), ROADY + R.choice((-1, 1)) * (RHALF - 0.1), 2, (0.6, 0.12))
    cave()
    # forecourt: rounded cobbles in rows, darker toward its edges, a kerb of dark stones round it
    y = GY + 0.3
    while y < ROADY + RHALF - 0.15:   # small rounded setts, warm and dark toward the edges (the big grey slabs read as a car park)
        x = -GX - 1.6 + R.uniform(0, 0.2)
        while x < GX + 1.6:
            L = R.uniform(0.3, 0.52)
            edge = abs(x + L / 2) > GX + 0.6 or y > ROADY + RHALF - 0.7
            m = M('cobble2' if edge or R.random() < 0.35 else R.choice(('cobble', 'dirt', 'stone2')))
            block((x + L / 2, y + 0.15, 0.08), (L - 0.06, 0.28, 0.09), m, 0.045)
            x += L
        y += 0.33
    for x in [-GX - 1.75 + i * 0.5 for i in range(int((2 * GX + 3.5) / 0.5) + 1)]:
        block((x, ROADY + RHALF - 0.05, 0.1), (0.46, 0.22, 0.14), M('stone_dk'), 0.03)
    x = -CAVE_X + 2.5
    while x < -GX - 3.5:
        K.cyl('post', 0.08, 0.75, (x, ROADY + RHALF + 0.7, 0.37), M('wood_dk'), 6, 0.0)
        if x + 2.4 < -GX - 3.5:
            for zz in (0.32, 0.6):
                o = K.cyl('rail', 0.05, 2.45, (x + 1.2, ROADY + RHALF + 0.7, zz), M('wood'), 6, 0.0); o.rotation_euler = (0, math.pi / 2, R.uniform(-0.05, 0.05))
        x += 2.4
    return K.export('road')

def cave():
    """The cave the waves come out of: a dark mouth in a rocky hillside where the road begins, framed by big rocks and
    a broken arch of stones, a faint violet glow inside (fresh-eyes review 2: the road stopped at the frame's edge)."""
    cx = -CAVE_X - 0.6
    mesh_from('mouth', [(cx, ROADY - 2.6, 0.0), (cx, ROADY + 2.6, 0.0), (cx, ROADY + 2.2, 3.4), (cx, ROADY - 2.2, 3.4)], [(0, 1, 2, 3)], M('cave'), 0.0)
    K.sphere('dark', 2.6, (cx - 1.4, ROADY, 1.4), M('cave'), (1.0, 1.25, 0.8), 2)
    K.sphere('glow', 0.9, (cx - 1.2, ROADY, 1.0), M('storm', 1.2), (0.6, 1.4, 0.7), 2)
    for (dy, h, r) in ((-3.1, 4.2, 1.5), (3.2, 4.6, 1.6), (-4.6, 2.6, 1.8), (4.8, 3.0, 1.9)):   # the hill's flanks
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r, location=(cx + R.uniform(-0.4, 0.6), ROADY + dy, h * 0.42))
        b = bpy.context.object; b.scale = (1.0, 0.9, h / (2 * r) * 1.3)
        for v in b.data.vertices: v.co *= R.uniform(0.85, 1.12)
        K.finish(b, M(R.choice(('rock', 'rock_dk'))), 0.0, smooth=False)
    for k in range(5):   # the arch: stones along a curve over the mouth
        a = math.pi * (k + 0.5) / 5
        o = block((cx + 0.3, ROADY + math.cos(a) * 2.6, 2.4 + math.sin(a) * 1.4), (0.9, 0.85, 0.75), M(R.choice(('rock', 'stone_dk'))), 0.06)
        o.rotation_euler = (math.cos(a) * 0.5, 0, 0)
    outcrop(cx + 1.5, ROADY - 4.2, 3, 1.0, 0.7); outcrop(cx + 1.5, ROADY + 4.3, 3, 1.0, 0.7)
    for k in range(4): K.cone('spike', 0.12, 0.5, (cx + 0.5, ROADY - 1.8 + k * 1.2, 3.35), M('rock_dk'), 5).rotation_euler = (math.pi, 0, 0)

def props():
    """Foreground life on the meadow, clear of the road (fresh-eyes review 2: the bottom third was empty): a supply cart,
    a little camp with a tent and a campfire, haystacks, a signpost pointing at the gate."""
    def cart(x, y, rot):
        parts = []
        parts.append(block((x, y, 0.75), (1.8, 1.0, 0.5), M('wood'), 0.03))
        for s in (-1, 1):
            w = K.cyl('wheel', 0.42, 0.12, (x - 0.4, y + s * 0.58, 0.42), M('wood_dk'), 12, 0.01); w.rotation_euler = (math.pi / 2, 0, 0)
            K.cyl('hub', 0.1, 0.16, (x - 0.4, y + s * 0.6, 0.42), M('iron'), 8, 0.0).rotation_euler = (math.pi / 2, 0, 0)
        o = block((x + 1.3, y, 0.62), (1.1, 0.08, 0.08), M('wood_dk'), 0.0); o.rotation_euler = (0, 0.2, 0)
        for k in range(3): K.sphere('sack', 0.28, (x - 0.5 + k * 0.5, y + R.uniform(-0.2, 0.2), 1.15), M('canvas'), (1, 0.8, 0.8), 1)
        K.cyl('barrel', 0.28, 0.6, (x + 0.55, y + 0.15, 1.3), M('wood'), 10, 0.02)
    cart(-15.0, ROADY + RHALF + 2.2, 0)
    # the camp: a canvas tent, a campfire with stones round it, a log to sit on
    tx, ty = 9.5, 13.0
    o = K.cone('tent', 1.6, 2.2, (tx, ty, 1.1), M('canvas'), 4); o.rotation_euler = (0, 0, math.pi / 4)
    mesh_from('flap', [(tx - 0.5, ty - 1.14, 0.02), (tx + 0.5, ty - 1.14, 0.02), (tx, ty - 0.9, 1.3)], [(0, 1, 2)], M('cave'), 0.0)
    for k in range(7):
        a = k / 7 * math.tau
        K.sphere('ring', 0.18, (tx - 2.6 + math.cos(a) * 0.55, ty - 0.6 + math.sin(a) * 0.55, 0.1), M('stone_dk'), (1, 1, 0.7), 1)
    K.cone('fire', 0.35, 0.8, (tx - 2.6, ty - 0.6, 0.45), M('fire', 3.0), 8); K.cone('fire_in', 0.18, 0.5, (tx - 2.6, ty - 0.6, 0.35), M('fire_core', 3.0), 6)
    o = K.cyl('log', 0.22, 1.6, (tx - 2.6, ty + 0.6, 0.22), M('wood'), 8, 0.0); o.rotation_euler = (0, math.pi / 2, 0.3)
    for (x, y) in ((-3.5, 14.0), (-2.2, 14.6), (-24.0, 12.8)):
        K.cyl('hay', 0.75, 1.0, (x, y, 0.5), M('hay'), 14, 0.04).rotation_euler = (0, math.pi / 2, R.uniform(0, 3))
    # a signpost at the road's edge pointing to the gate
    K.cyl('post', 0.08, 1.8, (-7.0, ROADY + RHALF + 0.9, 0.9), M('wood_dk'), 6, 0.0)
    block((-6.5, ROADY + RHALF + 0.9, 1.55), (0.9, 0.08, 0.28), M('wood'), 0.02)
    K.cone('arrow', 0.18, 0.3, (-6.0, ROADY + RHALF + 0.9, 1.55), M('wood'), 3).rotation_euler = (0, -math.pi / 2, 0)

# ---------------------------------------------------------------------------------------------------------------------
# the ground: a valley. Meadow in front of the wall; rocky hills where the wall ends on both sides; a wooded ridge
# behind the city; the town's packed earth inside the walls; dark tree clumps framing the bottom corners
PLAZA = (7.0, -8.0)
PLOTS = [(-4.5, -9.0), (13.5, -6.0), (-7.0, -14.5), (-11.5, -9.5), (-17.0, -12.0), (3.5, -14.5), (-21.0, -19.0), (-12.0, -19.0), (7.5, -20.5), (-1.5, -14.5)]

def tree(x, y, s, z=0.0, kind=None, far=False):
    kind = kind or R.choice(('round', 'round', 'pine'))
    if kind == 'pine':
        K.cyl('trunk', 0.18 * s, 1.2 * s, (x, y, z + 0.6 * s), M('wood_dk'), 6, 0.0)
        for k, (dz, r, h) in enumerate(((1.4, 1.25, 1.9), (2.4, 0.95, 1.6), (3.3, 0.65, 1.3))):
            K.cone('needles', r * s, h * s, (x, y, z + dz * s), M('tree_far' if far else 'pine'), 8)
    else:
        K.cyl('trunk', 0.22 * s, 1.6 * s, (x, y, z + 0.8 * s), M('wood_dk'), 8, 0.0, r2=0.15 * s)
        for (dz, r, dx, dy) in ((1.9, 1.15, 0, 0), (2.6, 0.9, 0.35, 0.1), (2.55, 0.85, -0.4, -0.15), (3.25, 0.65, 0.05, 0.05)):
            K.sphere('crown', r * s, (x + dx * s, y + dy * s, z + dz * s), M('tree_far' if far else R.choice(('tree', 'tree2'))), (1, 1, 0.88), 2)

def outcrop(x, y, n, spread, scale=1.0):   # a cluster of big angular rocks
    for _ in range(n):
        r = R.uniform(0.8, 1.9) * scale
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r, location=(x + R.uniform(-spread, spread), y + R.uniform(-spread, spread), r * 0.35))
        b = bpy.context.object; b.scale = (R.uniform(0.9, 1.4), R.uniform(0.8, 1.2), R.uniform(0.6, 1.0)); b.rotation_euler = (0, 0, R.uniform(0, 3))
        for v in b.data.vertices: v.co *= R.uniform(0.85, 1.12)
        K.finish(b, M(R.choice(('rock', 'rock_dk', 'stone2'))), 0.0, smooth=False)

def ground():
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=70, y_subdivisions=56, size=1, location=(0, 0, 0))
    g = bpy.context.object; g.scale = (130, 105, 1); bpy.ops.object.transform_apply(scale=True)
    g.location = (-10, -10, 0); bpy.ops.object.transform_apply(location=True)
    def height(x, y):
        h = 0.0
        if y < -23: h += (min(-23 - y, 18) / 18) ** 1.5 * 9.0 + 0.5 * math.sin(x * 0.21) * math.cos(y * 0.17)
        if y > 11: h += 0.22 * math.sin(x * 0.3) * math.cos(y * 0.35)
        if x > 21: h += (min(x - 21, 14) / 14) ** 1.4 * 7.0
        if x < -31: h += (min(-31 - x, 14) / 14) ** 1.4 * 7.0
        return h - 0.02
    for v in g.data.vertices: v.co.z = height(v.co.x, v.co.y)
    K.finish(g, M('grass'))
    # the town's ground inside the walls: packed earth with cobbled yards and a cobbled plaza
    # an irregular blob hugging the buildings (the review: hard-edged ground read as a stage), with grass eating its rim
    centre = (-3.0, -12.5); pts = []
    for i in range(36):
        a = i / 36 * math.tau
        rx, ry = 22.0, 10.5
        r = R.uniform(0.82, 1.0)
        pts.append((centre[0] + math.cos(a) * rx * r, max(-1.6, centre[1] + math.sin(a) * ry * r)))
    mesh_from('town', [(x, y, 0.02) for x, y in pts], [tuple(range(len(pts)))], M('dirt'), 0.0)
    for (x, y) in pts[::2]: tufts(x, y, 2, (0.8, 0.6))
    def patch(cx, cy, r, m, n=9, z=0.035):
        vs = [(cx + math.cos(a) * r * R.uniform(0.8, 1.15), cy + math.sin(a) * r * R.uniform(0.8, 1.15), z) for a in [i / n * math.tau for i in range(n)]]
        mesh_from('patch', vs, [tuple(range(n))], m, 0.0)
    patch(PLAZA[0], PLAZA[1], 4.4, M('cobble'), 12)
    for (px, py) in PLOTS: patch(px, py, 3.0, M('cobble2'), 9, 0.03)
    for _ in range(40):   # grass creeping over the town's ground and tufts at its edges
        patch(R.uniform(-28, 19), R.uniform(-23, -2), R.uniform(0.6, 1.6), M(R.choice(('grass2', 'grass'))), 7, 0.03)
    for _ in range(24): tufts(R.uniform(-28, 19), R.uniform(-23, -2), 2, (0.5, 0.5))
    # rocky hills where the wall ends, rocks and trees on them
    for (x, y) in ((22.5, 0.5), (24, -5), (23, 5), (-33, 0.5), (-34.5, 5), (-33.5, -5)): outcrop(x, y, 4, 1.8, 1.2)
    for (x, y) in [(R.uniform(23, 34), R.uniform(-28, 18)) for _ in range(10)]: tree(x, y, R.uniform(1.0, 1.4), height(x, y))
    for (x, y) in [(R.uniform(-46, -33), R.uniform(-28, 18)) for _ in range(10)]: tree(x, y, R.uniform(1.0, 1.4), height(x, y))
    # the wooded ridge behind the town: a darker, bluer back row for depth
    for (x, y) in [(R.uniform(-40, 30), R.uniform(-34, -25)) for _ in range(30)]: tree(x, y, R.uniform(1.0, 1.6), height(x, y), far=True)
    # the meadow: clustered flowers, rocks with tufts, bush clumps; trees only away from the road
    for _ in range(26):
        cx, cy = R.uniform(-48, 20), R.uniform(9.8, 28); c = R.choice(('flower_y', 'flower_w', 'flower_p'))
        for k in range(R.randint(5, 10)): K.sphere('flower', 0.07, (cx + R.uniform(-0.8, 0.8), cy + R.uniform(-0.6, 0.6), 0.12), M(c), sub=1)
    for _ in range(8):
        cx, cy = R.uniform(-46, 18), R.uniform(11, 26); outcrop(cx, cy, 2, 0.6, 0.35); tufts(cx, cy, 3, (0.9, 0.7))
    for _ in range(22):
        cx, cy = R.uniform(-46, 18), R.uniform(12, 26)
        for k in range(R.randint(3, 6)): K.sphere('bush', R.uniform(0.45, 0.95), (cx + R.uniform(-1.0, 1.0), cy + R.uniform(-0.6, 0.6), 0.35), M(R.choice(('bush', 'grass2', 'tree'))), (1, 1, 0.8), 2)
    for _ in range(30):   # broad, low-contrast swathes of darker and lighter grass, so the meadow is not one flat colour
        cx, cy = R.uniform(-48, 20), R.uniform(10, 30); n = 9; rr = R.uniform(1.5, 4.0)
        vs = [(cx + math.cos(i / n * math.tau) * rr * R.uniform(0.7, 1.2), cy + math.sin(i / n * math.tau) * rr * R.uniform(0.5, 0.9), 0.025) for i in range(n)]
        mesh_from('swathe', vs, [tuple(range(n))], M(R.choice(('grass2', 'grass3'))), 0.0)
    for (x, y) in [(R.uniform(-44, 16), R.uniform(20, 30)) for _ in range(7)]: tree(x, y, R.uniform(1.0, 1.3))
    props()
    # dark framing clumps at the bottom corners of the view
    for (cx, cy) in ((17, 17), (19, 21), (-36, 17), (-39, 21)):
        for k in range(3): tree(cx + R.uniform(-2, 2), cy + R.uniform(-1.5, 1.5), R.uniform(1.4, 1.8), 0, 'round', far=True)
    return K.export('ground')

def marker():
    """Where to rebuild next (fresh-eyes review 2: lead with one plot): a ring of gold with glowing runes on the ground and
    a fat gold arrow above it. The game bobs the arrow and pulses the ring; origin on the ground at the ring's centre."""
    bpy.ops.mesh.primitive_torus_add(major_radius=2.5, minor_radius=0.12, location=(0, 0, 0.12))
    K.finish(bpy.context.object, M('gold', 1.2), 0.0)
    for i in range(10):
        a = i / 10 * math.tau
        o = block((math.cos(a) * 2.5, math.sin(a) * 2.5, 0.2), (0.22, 0.42, 0.06), M('fire_core', 2.0), 0.0); o.rotation_euler = (0, 0, a)
    K.cyl('stem', 0.22, 0.9, (0, 0, 6.9), M('gold', 1.0), 10, 0.02)
    K.cone('head', 0.6, 0.85, (0, 0, 6.05), M('gold', 1.0), 12).rotation_euler = (math.pi, 0, 0)
    return K.export('marker')

# ---------------------------------------------------------------------------------------------------------------------
# the fountain: the town's centrepiece. An octagonal basin on steps, water, a two-tier column, a gold hearth flame.
def fountain():
    K.cyl('step', 2.95, 0.22, (0, 0, 0.11), M('stone_dk'), 8, 0.04)
    K.cyl('step2', 2.7, 0.2, (0, 0, 0.3), M('stone2'), 8, 0.04)
    K.cyl('basin', 2.4, 0.55, (0, 0, 0.66), M('stone'), 8, 0.05)
    K.cyl('rim', 2.5, 0.14, (0, 0, 0.98), M('stone3'), 8, 0.03)
    K.cyl('water', 2.2, 0.06, (0, 0, 0.88), M('water_gloss', 0.3), 32, 0.0)
    prism('column', 0.38, 0.3, 0.9, 2.3, M('stone2'), n=8)
    K.cyl('bowl', 1.05, 0.36, (0, 0, 2.45), M('stone'), 16, 0.03, r2=0.55)
    K.cyl('bowl_water', 0.92, 0.05, (0, 0, 2.62), M('water_gloss', 0.3), 16, 0.0)
    K.cyl('neck', 0.18, 0.6, (0, 0, 2.95), M('stone2'), 10, 0.02)
    K.cyl('cup', 0.34, 0.22, (0, 0, 3.32), M('gold'), 12, 0.02, r2=0.2)
    K.cone('flame', 0.24, 0.75, (0, 0, 3.8), M('fire', 3.0), 10)
    K.cone('flame_core', 0.12, 0.45, (0, 0, 3.7), M('gold', 4.0), 8)
    for i in range(8):
        a = i / 8 * math.tau
        K.cyl('fall', 0.05, 1.4, (math.cos(a) * 1.0, math.sin(a) * 1.0, 1.85), M('water_gloss', 0.5), 6, 0.0)
    return K.export('fountain')
