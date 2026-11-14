# Hearthgate 3D, the front (Nov 13 2026, second pass after the fresh-eyes review): the gatehouse, the curtain wall, the
# element towers and their caps, the road, the ground, the fountain and the enemies, built to the art standard. Imported
# by hg_kit.py, which owns the helpers, the palette and the export.
#
# Coordinates are Unity's: x right as seen from the city (the camera stands north, so screen right is world -x), y in
# Blender = Unity z (north, toward the camera), z up. The camera looks at the +y faces, so those carry the detail.
# Layout numbers (the ring wall, the gate at the origin on its front point, the towers, the lanes, the plaza and the
# plots) live in hg_layout.py, mirrored by Assets/Scripts/Game/Field.cs; LayoutCheck verifies them on the models.
#
# Owner's and reviewer's rules this file answers:
#   - one gatehouse, the hero of the frame (raised keep, red roof, flag, crest); towers spaced on the wall, shorter
#   - each element tower reads by its own silhouette and colour from across the screen (oversized glowing caps)
#   - a value structure: mid-tone world, the brightest colours only on enemies, caps and effects
#   - nothing pasted on: plinths, moss, rubble and grass at every foot; props clustered, never sprinkled
import bpy, bmesh, math, random
from mathutils import Vector
import hg_kit as K
import hg_layout as L

R = random.Random(13)
STONES = ('stone', 'stone2', 'stone3')

def M(name, glow=0.0): return K.mat(name, glow)

def block(loc, size, m, bevel=0.035, rot=0.0):   # a chamfered box, batched into one mesh per material (hg_kit.Blk)
    o = K.Blk(loc, size, m, bevel)
    if rot: o.rotation_euler = (0, 0, rot)
    K.BLOCKS.append(o)
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

def merlons(u0, u1, z, place, w=0.62, gap=0.42, h=0.6, vary=0.0, rot=0.0):
    n = int((u1 - u0 + gap) // (w + gap)); start = u0 + ((u1 - u0) - (n * w + (n - 1) * gap)) / 2
    for i in range(n):
        u = start + i * (w + gap) + w / 2
        hh = h * (1 + R.uniform(-vary, vary))
        loc, size = place(u, z + hh / 2, w, hh)
        block(loc, size, M(R.choice(STONES)), 0.045, rot)

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
# the ring wall (owner, Nov 13 2026: "Close off the loop around town to make a protected town"): an ellipse round the
# town (hg_layout.RX, RZ), closed at the gatehouse, built as straight runs about 3 m long. Each run: a mortar core, a
# battered foot, coursed stones on the face the camera sees (the outside of the front arc, the inside of the back arc),
# a coping course, the wall walk, tall merlons outside and low ones inside, buttresses, and moss, rubble and grass at
# the outer foot
WH = L.WALL_H
def wall():
    cam = (L.CAM_POS[0], L.CAM_POS[2])
    a0 = math.acos(-(GX - 0.05) / L.RX); a1 = math.acos((GX - 0.05) / L.RX) + math.tau   # from the gate's left side round to its right
    angles, a = [a0], a0
    while True:
        dx, dz = -L.RX * math.sin(a), L.RZ * math.cos(a)
        a += 3.0 / math.hypot(dx, dz)
        if a >= a1 - 0.02: angles.append(a1); break
        angles.append(a)
    pts = [(L.RX * math.cos(a), -L.RZ + L.RZ * math.sin(a)) for a in angles]
    for (x0, z0), (x1, z1) in zip(pts, pts[1:]):
        Lr = math.hypot(x1 - x0, z1 - z0); tx, tz = (x1 - x0) / Lr, (z1 - z0) / Lr
        mx, mz = (x0 + x1) / 2, (z0 + z1) / 2
        rot = math.atan2(tz, tx); ny = (-tz, tx)                     # the run's local +y, across the wall
        nx, nz = L.ring_normal(mx, mz); out = 1 if ny[0] * nx + ny[1] * nz > 0 else -1
        see = 1 if ny[0] * (cam[0] - mx) + ny[1] * (cam[1] - mz) > 0 else -1
        def at(u, v, z): return (mx + tx * u + ny[0] * v, mz + tz * u + ny[1] * v, z)
        Ld = Lr + 0.35                                                 # runs overlap a little at the bends
        block(at(0, 0, WH / 2), (Ld, 1.8, WH), M('mortar'), 0.0, rot)
        block(at(0, out * 0.05, 0.27), (Ld, 2.2, 0.55), M('stone_dk'), 0.05, rot)
        masonry(-Ld / 2, Ld / 2, 0.55, WH - 0.25, lambda u, z, Lb, h: (at(u, see * 0.95, z), (Lb, 0.1, h), rot), lmin=0.7, lmax=1.5)
        block(at(0, 0, WH - 0.12), (Ld, 2.05, 0.25), M('stone3'), 0.04, rot)
        block(at(0, 0, WH + 0.09), (Ld, 1.9, 0.18), M('stone2'), 0.03, rot)
        merlons(-Lr / 2 + 0.1, Lr / 2 - 0.1, WH + 0.18, lambda u, z, w, h: (at(u, out * 0.72, z), (w, 0.42, h)), w=0.7, gap=0.48, h=0.62, vary=0.12, rot=rot)
        merlons(-Lr / 2 + 0.1, Lr / 2 - 0.1, WH + 0.18, lambda u, z, w, h: (at(u, -out * 0.72, z), (w, 0.32, h)), w=0.7, gap=0.48, h=0.42, rot=rot)
        if all(math.hypot(mx - tx_, mz - tz_) > 3.2 for tx_, tz_ in L.TOWERS) and abs(mx) > GX + 2 and R.random() < 0.6:
            block(at(0, out * 1.12, 1.0), (0.75, 0.48, 2.0), M(R.choice(STONES)), 0.05, rot)
            block(at(0, out * 1.02, 2.12), (0.66, 0.32, 0.35), M('stone_dk'), 0.05, rot)
        fx, fz = mx + nx * 1.4, mz + nz * 1.4
        K.sphere('moss', R.uniform(0.25, 0.5), (fx, fz, 0.05), M(R.choice(('moss', 'grass2'))), (1.6, 0.7, 0.45), 1).rotation_euler = (0, 0, rot)
        if R.random() < 0.5: rubble(fx, fz, 2, (0.4, 0.4))
        tufts(mx + nx * 1.5 + tx * R.uniform(-1, 1), mz + nz * 1.5 + tz * R.uniform(-1, 1), 2, (0.3, 0.3))
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
            o = block((math.cos(a) * 1.93, math.sin(a) * 1.93, 0.18), (0.06, 0.32, 0.22), M('fire_red', 3.0), 0.0); o.rotation_euler = (0, 0, a)
        for i in range(6):   # iron prongs holding the bowl
            a = i / 6 * math.tau
            o = block((math.cos(a) * 1.35, math.sin(a) * 1.35, 0.95), (0.16, 0.16, 0.9), M('iron'), 0.02)
            o.rotation_euler = (-math.sin(a) * 0.35, math.cos(a) * 0.35, 0)
        K.cyl('bowl', 1.5, 0.7, (0, 0, 1.2), M('iron'), 20, 0.04, r2=0.95)
        K.cyl('rim', 1.55, 0.12, (0, 0, 1.55), M('gold'), 20, 0.02)
        for i in range(10):
            a = i / 10 * math.tau; r = 0.5 + 0.45 * (i % 2)
            K.sphere('coal', R.uniform(0.22, 0.32), (math.cos(a) * r, math.sin(a) * r, 1.58), M('fire', 3.0), (1, 1, 0.7), 2)
        K.cone('flame', 1.15, 2.9, (0, 0, 3.0), M('fire_red', 3.5), 14)   # a red flame with orange tongues and core (owner: "red and orange tops")
        for i in range(5):
            a = i / 5 * math.tau + 0.4
            K.cone('tongue', 0.45, R.uniform(1.4, 2.0), (math.cos(a) * 0.65, math.sin(a) * 0.65, 2.3), M('fire_core', 3.0), 10)
        K.cone('core', 0.38, 1.3, (0, 0, 2.25), M('fire_core', 3.0), 12)
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
        K.sphere('orb', 0.62, (0, 0, 4.45), M('storm', 3.5), sub=3)
        K.sphere('orb_core', 0.32, (0, 0, 4.45), M('storm', 3.0), sub=2)
        for k in range(3):
            bpy.ops.mesh.primitive_torus_add(major_radius=0.92, minor_radius=0.05, location=(0, 0, 4.45), rotation=(k * 1.05, k * 0.7, 0))
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
# the lanes (owner's sketch, Nov 13 2026: "monsters are walking up to the towers on a route in front of each tower"):
# four trodden earth paths running up the screen from below the frame to the foot of each tower, a darker worn middle,
# pebbles and grass along the edges, a trampled apron with rubble where the enemies gather under the tower. They run on
# past the frame's bottom edge so a taller window never shows their end.
def lanes():
    for (fx, fz), (sx, sz) in L.LANES:
        L0 = math.hypot(sx - fx, sz - fz); dx, dz = (sx - fx) / L0, (sz - fz) / L0; px, pz = -dz, dx
        u0, u1 = 1.0, L0 + 9.0
        n = int((u1 - u0) / 1.5) + 1
        for (half, m, z, jit) in ((L.LANE_HALF, 'dirt', 0.03, 0.18), (0.75, 'dirt_dk', 0.045, 0.22)):
            left, right = [], []
            for k in range(n + 1):
                u = u0 + (u1 - u0) * k / n
                hl, hr = half + R.uniform(-jit, jit), half + R.uniform(-jit, jit)
                left.append((fx + dx * u + px * hl, fz + dz * u + pz * hl, z)); right.append((fx + dx * u - px * hr, fz + dz * u - pz * hr, z))
            vs = left + right; N = n + 1
            mesh_from('lane', vs, [(N + k, N + k + 1, k + 1, k) for k in range(n)], M(m), 0.0)
        # the apron at the tower's foot: trampled earth in a rough half disc, rubble from the fighting
        vs = [(fx, fz, 0.035)]
        for k in range(13):
            ang = math.atan2(dz, dx) - math.pi / 2 - 0.35 + (math.pi + 0.7) * k / 12
            r = 3.0 * R.uniform(0.85, 1.1); vs.append((fx + math.cos(ang) * r, fz + math.sin(ang) * r, 0.035))
        mesh_from('apron', vs, [(0, k + 1, k + 2) for k in range(12)], M('dirt'), 0.0)
        rubble(fx + px * 2.2, fz + pz * 2.2, 3, (0.5, 0.5)); rubble(fx - px * 2.2, fz - pz * 2.2, 3, (0.5, 0.5))
        for _ in range(int(L0 * 1.6)):   # pebbles in clusters along both edges, grass bleeding in
            u = R.uniform(2.5, u1 - 1); side = R.choice((-1, 1)); off = side * (L.LANE_HALF - R.uniform(0.0, 0.4))
            for k in range(R.randint(1, 3)):
                sz_ = R.uniform(0.14, 0.28)
                o = block((fx + dx * u + px * off + R.uniform(-0.3, 0.3), fz + dz * u + pz * off + R.uniform(-0.3, 0.3), 0.07), (sz_ * 1.3, sz_, sz_ * 0.5), M(R.choice(('cobble2', 'rock', 'rock_dk'))), 0.03)
                o.rotation_euler = (0, 0, R.uniform(0, 3.1))
        for _ in range(int(L0 * 0.7)):
            u = R.uniform(3, u1 - 1); side = R.choice((-1, 1)); off = side * (L.LANE_HALF + 0.1)
            tufts(fx + dx * u + px * off, fz + dz * u + pz * off, 2, (0.4, 0.4))
    return K.export('road')
road = lanes   # exported as 'road' (ArtSet.road)

def spot(x, z, r):
    """The nearest place to (x, z) where something of radius r keeps off every lane (props never stand on a route)."""
    for k in range(80):
        for sx in (1, -1):
            xx = x + sx * k * 0.25
            if L.lane_clear(xx, z, r) and not L.inside(xx, z, -2.5 - r): return xx, z
    raise RuntimeError(f'no clear spot near {x}, {z}')

def props():
    """Foreground life on the meadow outside the walls, clear of the lanes: a supply cart, a camp before the gate with a
    tent and a campfire, haystacks (fresh-eyes review 2: the bottom third was empty)."""
    def cart(x, y, rot):
        block((x, y, 0.75), (1.8, 1.0, 0.5), M('wood'), 0.03)
        for s in (-1, 1):
            w = K.cyl('wheel', 0.42, 0.12, (x - 0.4, y + s * 0.58, 0.42), M('wood_dk'), 12, 0.01); w.rotation_euler = (math.pi / 2, 0, 0)
            K.cyl('hub', 0.1, 0.16, (x - 0.4, y + s * 0.6, 0.42), M('iron'), 8, 0.0).rotation_euler = (math.pi / 2, 0, 0)
        o = block((x + 1.3, y, 0.62), (1.1, 0.08, 0.08), M('wood_dk'), 0.0); o.rotation_euler = (0, 0.2, 0)
        for k in range(3): K.sphere('sack', 0.28, (x - 0.5 + k * 0.5, y + R.uniform(-0.2, 0.2), 1.15), M('canvas'), (1, 0.8, 0.8), 1)
        K.cyl('barrel', 0.28, 0.6, (x + 0.55, y + 0.15, 1.3), M('wood'), 10, 0.02)
    cart(*spot(-12.0, 9.0, 1.4), 0)
    # the camp before the gate: a canvas tent, a campfire with stones round it, a log to sit on
    tx, ty = spot(2.6, 11.0, 1.7)
    # a ridge tent of striped canvas on poles (a four-sided cone read as an untextured pyramid: fresh-eyes review 4)
    hl, hw, hh = 1.5, 1.25, 1.7
    vs = [(tx - hl, ty - hw, 0.02), (tx + hl, ty - hw, 0.02), (tx + hl, ty, hh), (tx - hl, ty, hh), (tx - hl, ty + hw, 0.02), (tx + hl, ty + hw, 0.02)]
    mesh_from('tent_s', vs[:4], [(0, 1, 2, 3)], M('canvas'), 0.0); mesh_from('tent_n', [vs[3], vs[2], vs[5], vs[4]], [(0, 1, 2, 3)], M('canvas'), 0.0)
    for k in range(3):   # red stripes across the slope the camera sees
        u0 = -hl + 0.35 + k * 1.0
        mesh_from('stripe', [(tx + u0, ty + 0.02, hh + 0.02), (tx + u0 + 0.3, ty + 0.02, hh + 0.02), (tx + u0 + 0.3, ty + hw + 0.01, 0.04), (tx + u0, ty + hw + 0.01, 0.04)], [(0, 1, 2, 3)], M('banner'), 0.0)
    for sx in (-1, 1):
        mesh_from('gable', [(tx + sx * hl, ty - hw, 0.02), (tx + sx * hl, ty + hw, 0.02), (tx + sx * hl, ty, hh)], [(0, 1, 2) if sx > 0 else (2, 1, 0)], M('canvas'), 0.0)
        K.cyl('pole', 0.05, hh + 0.5, (tx + sx * (hl + 0.05), ty, (hh + 0.5) / 2), M('wood_dk'), 6, 0.0)
    mesh_from('flap', [(tx - hl - 0.01, ty - 0.5, 0.03), (tx - hl - 0.01, ty + 0.5, 0.03), (tx - hl - 0.01, ty, 1.2)], [(2, 1, 0)], M('cave'), 0.0)
    K.cyl('ridge', 0.05, 2 * hl + 0.3, (tx, ty, hh + 0.02), M('wood_dk'), 6, 0.0).rotation_euler = (0, math.pi / 2, 0)
    fx, fy = tx - 2.8, ty + 0.6
    for k in range(7):
        a = k / 7 * math.tau
        K.sphere('ring', 0.18, (fx + math.cos(a) * 0.55, fy + math.sin(a) * 0.55, 0.1), M('stone_dk'), (1, 1, 0.7), 1)
    K.cone('fire', 0.35, 0.8, (fx, fy, 0.45), M('fire', 3.0), 8); K.cone('fire_in', 0.18, 0.5, (fx, fy, 0.35), M('fire_core', 3.0), 6)
    o = K.cyl('log', 0.22, 1.6, (fx, fy + 1.1, 0.22), M('wood'), 8, 0.0); o.rotation_euler = (0, math.pi / 2, 0.3)
    for (x, y) in ((tx + 2.6, ty + 0.4), (tx + 2.9, ty + 1.9), (tx - 4.6, ty - 1.0)):   # hay with the camp, never at the frame's edges
        x, y = spot(x, y, 0.9)
        K.cyl('hay', 0.75, 1.0, (x, y, 0.5), M('hay'), 14, 0.04).rotation_euler = (0, math.pi / 2, R.uniform(0, 3))

# ---------------------------------------------------------------------------------------------------------------------
# the ground: a valley. Meadow outside the ring wall with the four lanes; the town's packed earth filling the ring;
# a wooded ridge behind the town; trees and rocks on the slopes either side; dark tree clumps framing the bottom corners
PLAZA = L.PLAZA
PLOTS = L.PLOTS

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
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=76, y_subdivisions=76, size=1, location=(0, 0, 0))
    g = bpy.context.object; g.scale = (190, 180, 1); bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    g.location = (0, -35, 0); bpy.ops.object.transform_apply(location=True, rotation=False, scale=False)
    def height(x, y):   # flat where the lanes run (a rolling meadow poked through them: fresh-eyes review 4)
        h = 0.0
        if y < -2 * L.RZ - 4: h += (min(-2 * L.RZ - 4 - y, 40) / 18) ** 1.4 * 9.0 + 0.5 * math.sin(x * 0.21) * math.cos(y * 0.17)   # the ridge climbs on past the top of a tall window
        if abs(x) > L.RX + 6: h += (min(abs(x) - L.RX - 6, 14) / 14) ** 1.4 * 7.0
        return h - 0.02
    for v in g.data.vertices: v.co.z = height(v.co.x, v.co.y)
    K.finish(g, M('grass'))
    def free(x, y, r): return not L.inside(x, y, -1.8 - r) and L.lane_clear(x, y, r)   # outside the ring and off the lanes
    # the town's ground: packed earth filling the ring, its rim ragged under the wall, a cobbled plaza and yards
    pts = []
    for i in range(48):
        a = i / 48 * math.tau; k = R.uniform(0.93, 0.99)
        pts.append((L.RX * k * math.cos(a), -L.RZ + L.RZ * k * math.sin(a)))
    mesh_from('town', [(x, y, 0.02) for x, y in pts], [tuple(range(len(pts)))], M('dirt'), 0.0)
    def patch(cx, cy, r, m, n=9, z=0.035):
        vs = [(cx + math.cos(a) * r * R.uniform(0.8, 1.15), cy + math.sin(a) * r * R.uniform(0.8, 1.15), z) for a in [i / n * math.tau for i in range(n)]]
        mesh_from('patch', vs, [tuple(range(n))], m, 0.0)
    patch(PLAZA[0], PLAZA[1], 5.6, M('cobble'), 14)
    mesh_from('street', [(-2.2, -1.6, 0.03), (2.2, -1.6, 0.03), (2.2, PLAZA[1] + 4.5, 0.03), (-2.2, PLAZA[1] + 4.5, 0.03)], [(3, 2, 1, 0)], M('cobble2'), 0.0)   # gate to plaza
    for (px, py), bid in zip(PLOTS, L.IDS):   # a cobbled yard before each door (fronts face the gate, +y)
        w, d = L.SIZES[bid]; y0 = py + d / 2 + 0.2
        mesh_from('yard', [(px - w / 2, y0, 0.03), (px + w / 2, y0, 0.03), (px + w / 2 - 0.5, y0 + 2.6, 0.03), (px - w / 2 + 0.5, y0 + 2.6, 0.03)], [(0, 1, 2, 3)], M('cobble2'), 0.0)
    def on_plot(x, y, m):
        return any(abs(x - px) < L.SIZES[b][0] / 2 + m and abs(y - py) < L.SIZES[b][1] / 2 + m for (px, py), b in zip(PLOTS, L.IDS))
    n = 0
    while n < 40:   # grass creeping over the town's ground, clear of the yards
        x, y = R.uniform(-L.RX, L.RX), R.uniform(-2 * L.RZ, 0)
        if L.inside(x, y, 2.5) and not on_plot(x, y, 2.0) and math.hypot(x - PLAZA[0], y - PLAZA[1]) > 6.5 and abs(x) > 2.6:
            patch(x, y, R.uniform(0.6, 1.6), M(R.choice(('grass2', 'grass'))), 7, 0.03); n += 1
            if n % 2: tufts(x, y, 2, (0.5, 0.5))
    # the slopes either side of the ring and the wooded ridge behind it: a darker, bluer back row for depth
    for (x, y) in ((L.RX + 5, -12), (L.RX + 6, -L.RZ - 4), (-L.RX - 5, -12), (-L.RX - 6, -L.RZ - 4), (L.RX + 3, 2), (-L.RX - 3, 2)): outcrop(x, y, 4, 1.8, 1.2)
    for _ in range(12):
        x, y = R.choice((1, -1)) * R.uniform(L.RX + 4, L.RX + 18), R.uniform(-2 * L.RZ, 6); tree(x, y, R.uniform(1.0, 1.4), height(x, y))
    for (x, y) in [(R.uniform(-L.RX - 16, L.RX + 16), R.uniform(-2 * L.RZ - 16, -2 * L.RZ - 5)) for _ in range(40)] + [(R.uniform(-L.RX - 28, L.RX + 28), R.uniform(-2 * L.RZ - 45, -2 * L.RZ - 16)) for _ in range(45)]:
        tree(x, y, R.uniform(1.1, 1.8), height(x, y), far=True)
    for _ in range(14):   # trees behind the ring's shoulders, between the wall and the ridge
        x, y = R.choice((1, -1)) * R.uniform(L.RX * 0.7, L.RX + 4), R.uniform(-2 * L.RZ - 4, -1.6 * L.RZ)
        if free(x, y, 1.5): tree(x, y, R.uniform(1.0, 1.4), height(x, y))
    # the meadow: clustered flowers, rocks with tufts, bush clumps, broad swathes of darker and lighter grass
    def scatter(n, r, box, make):
        k = 0
        while k < n:
            x, y = R.uniform(*box[0]), R.uniform(*box[1])
            if free(x, y, r): make(x, y); k += 1
    box = ((-L.RX - 8, L.RX + 8), (-2, 30))
    def flowers(cx, cy):
        c = R.choice(('flower_y', 'flower_w', 'flower_p'))
        for k in range(R.randint(5, 10)): K.sphere('flower', 0.07, (cx + R.uniform(-0.8, 0.8), cy + R.uniform(-0.6, 0.6), 0.12), M(c), sub=1)
    scatter(30, 1.0, box, flowers)
    scatter(8, 1.0, box, lambda x, y: (outcrop(x, y, 2, 0.6, 0.35), tufts(x, y, 3, (0.9, 0.7))))
    def bushes(cx, cy):
        for k in range(R.randint(3, 6)): K.sphere('bush', R.uniform(0.45, 0.95), (cx + R.uniform(-1.0, 1.0), cy + R.uniform(-0.6, 0.6), 0.35), M(R.choice(('bush', 'grass2', 'tree'))), (1, 1, 0.8), 2)
    scatter(22, 1.6, box, bushes)
    def swathe(cx, cy):
        n = 9; rr = R.uniform(1.5, 3.0)
        vs = [(cx + math.cos(i / n * math.tau) * rr * R.uniform(0.7, 1.2), cy + math.sin(i / n * math.tau) * rr * R.uniform(0.5, 0.9), 0.025) for i in range(n)]
        mesh_from('swathe', vs, [tuple(range(n))], M(R.choice(('grass2', 'grass3'))), 0.0)
    scatter(40, 3.2, ((-L.RX - 14, L.RX + 14), (-2 * L.RZ - 2, 30)), swathe)
    scatter(6, 1.8, ((-30, 30), (20, 30)), lambda x, y: tree(x, y, R.uniform(1.0, 1.3)))
    props()
    # dark framing clumps at the bottom corners of the view
    for (cx, cy) in ((24, 17), (27, 21), (-24, 17), (-27, 21)):
        for k in range(3):
            x, y = cx + R.uniform(-2, 2), cy + R.uniform(-1.5, 1.5)
            if free(x, y, 1.6): tree(x, y, R.uniform(1.4, 1.8), 0, 'round', far=True)
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

# ---------------------------------------------------------------------------------------------------------------------
# the wizard (owner, Nov 13 2026: "a button that makes you into the wizard that can walk into the town and upgrade the
# building from inside"): a chunky chibi in an indigo robe with gold trim, a big floppy pointed hat with a brass band
# and a star, a long white beard, rosy nose, and a gnarled staff topped with a glowing hearth-orb. About 2.3 m to the
# hat's tip so he reads beside the 6 m buildings. Front toward Blender -Y, origin between the feet. The game bobs and
# leans him as he walks.
def wizard():
    # robe: a flared bell with a hem band and a gold trim strip down the front
    K.cyl('robe', 0.62, 1.05, (0, 0, 0.55), M('robe'), 20, 0.03, r2=0.36)
    K.cyl('hem', 0.64, 0.1, (0, 0, 0.07), M('gold'), 20, 0.02)
    block((0, -0.47, 0.55), (0.12, 0.08, 0.95), M('gold'), 0.02).rotation_euler = (-0.27, 0, 0)
    K.cyl('belt', 0.42, 0.1, (0, 0, 0.88), M('wood_dk'), 16, 0.02)
    block((0, -0.42, 0.88), (0.16, 0.06, 0.13), M('gold'), 0.02)
    K.sphere('chest', 0.42, (0, 0, 1.1), M('robe'), (1, 0.9, 0.85), 3)
    # sleeves and hands; the right one holds the staff
    from mathutils import Euler
    for s in (-1, 1):   # bell sleeves hanging down and out from the shoulders, a gold cuff at the open end, the hand in it
        c = Vector((s * 0.48, -0.1, 1.04)); rot = Euler((-0.35, -s * 0.55, 0))
        def along(d): v = Vector((0, 0, d)); v.rotate(rot); return c + v
        o = K.cyl('sleeve', 0.23, 0.55, tuple(c), M('robe'), 12, 0.02, r2=0.15); o.rotation_euler = rot
        o = K.cyl('cuff', 0.24, 0.07, tuple(along(-0.27)), M('gold'), 12, 0.0); o.rotation_euler = rot
        K.sphere('hand', 0.12, tuple(along(-0.36)), M('skin'), sub=2)
    # head: a round face, rosy nose, little eyes under bushy brows, and a long tapering beard
    K.sphere('head', 0.33, (0, -0.02, 1.52), M('skin'), sub=3)
    K.sphere('nose', 0.08, (0, -0.33, 1.5), M('blush'), sub=2)
    for s in (-1, 1):
        K.sphere('eye', 0.06, (s * 0.12, -0.29, 1.6), M('pupil'), (1, 0.6, 1.2), 2)
        K.sphere('catch', 0.018, (s * 0.11, -0.34, 1.63), M('white_gloss', 0.5), sub=1)
        o = block((s * 0.13, -0.29, 1.69), (0.17, 0.08, 0.06), M('beard'), 0.02); o.rotation_euler = (0, -s * 0.25, 0)
        K.sphere('tuft', 0.12, (s * 0.3, -0.12, 1.47), M('beard'), (0.8, 0.9, 1.1), 2)
    K.sphere('moustache', 0.13, (0, -0.3, 1.43), M('beard'), (1.6, 0.6, 0.55), 2)
    o = K.cone('beard', 0.27, 0.75, (0, -0.24, 1.08), M('beard'), 12); o.rotation_euler = (math.pi + 0.12, 0, 0)
    # the hat: a wide brim, a brass band with a gold star, a tall cone flopping forward at the tip
    K.cyl('brim', 0.62, 0.06, (0, 0, 1.76), M('robe_dk'), 24, 0.02)
    K.cyl('band', 0.37, 0.12, (0, 0, 1.84), M('gold'), 20, 0.02)
    K.cyl('crown', 0.36, 0.42, (0, 0, 2.07), M('robe'), 20, 0.0, r2=0.22)
    o = K.cone('tip', 0.22, 0.55, (0, 0.06, 2.48), M('robe'), 16); o.rotation_euler = (-0.45, 0, 0)
    K.sphere('pom', 0.07, (0, 0.18, 2.72), M('gold', 1.0), sub=2)
    o = K.cone('star', 0.1, 0.03, (0, -0.37, 1.85), M('gold', 1.2), 5); o.rotation_euler = (math.pi / 2, 0, 0)
    # the staff: a gnarled shaft in the right hand, a claw holding a glowing hearth-orb above the hat brim
    o = K.cyl('staff', 0.05, 2.2, (0.66, -0.26, 1.05), M('wood_dk'), 8, 0.0, r2=0.04); o.rotation_euler = (0, 0.04, 0)
    for k in range(3):
        a = k / 3 * math.tau
        o = K.cone('claw', 0.04, 0.3, (0.7 + math.cos(a) * 0.1, -0.26 + math.sin(a) * 0.1, 2.18), M('wood_dk'), 6); o.rotation_euler = (-math.sin(a) * 0.5, math.cos(a) * 0.5, 0)
    K.sphere('orb', 0.15, (0.7, -0.26, 2.3), M('fire_core', 2.5), sub=3)
    K.sphere('orb_core', 0.08, (0.7, -0.26, 2.3), M('gold', 3.0), sub=2)
    for s in (-1, 1): K.sphere('boot', 0.13, (s * 0.2, -0.22, 0.07), M('wood_dk'), (1, 1.5, 0.6), 2)
    return K.export('wizard')
