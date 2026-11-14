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
from mathutils import Vector, Euler
import hg_kit as K
import hg_layout as L

R = random.Random(13)
STONES = ('stone', 'stone2', 'stone3')

def M(name, glow=0.0): return K.mat(name, glow)

# batched primitives: spheres, cylinders and cones added straight into one bmesh per material (an operator per part made
# the ground with its gardens take minutes: each new object slows the next). export() flushes them into meshes.
from mathutils import Matrix
BATCH = {}
def _bm(m):
    key = m.name
    if key not in BATCH: BATCH[key] = (bmesh.new(), m)
    return BATCH[key][0]
def _place(loc, scale=(1, 1, 1), rot=None):
    mx = Matrix.Translation(loc)
    if rot: mx = mx @ Euler(rot).to_matrix().to_4x4()
    return mx @ Matrix.Diagonal((scale[0], scale[1], scale[2], 1.0))
def bsphere(name, r, loc, m, scale=(1, 1, 1), sub=2, rot=None):
    bmesh.ops.create_icosphere(_bm(m), subdivisions=sub, radius=r, matrix=_place(loc, scale, rot))
def bcyl(name, r, h, loc, m, verts=16, bevel=0.0, r2=None, rot=None):
    bmesh.ops.create_cone(_bm(m), cap_ends=True, cap_tris=False, segments=verts, radius1=r, radius2=r if r2 is None else r2, depth=h, matrix=_place(loc, rot=rot))
def bcone(name, r, h, loc, m, verts=16, rot=None): bcyl(name, r, h, loc, m, verts, 0.0, 0.0, rot)
def flush_batch():
    for key, (bm, m) in list(BATCH.items()):
        me = bpy.data.meshes.new('batch_' + key); bm.to_mesh(me); bm.free()
        o = bpy.data.objects.new('batch_' + key, me); bpy.context.collection.objects.link(o); K.finish(o, m)
    BATCH.clear()
def export(name): return K.export(name)
# every model's export flushes the batch first, and a fresh model starts with an empty one (hg_kit is shared by every
# builder: the gate, the wall and the towers grow tufts too)
_k_export, _k_clear = K.export, K.clear
def _export_flushing(name): flush_batch(); return _k_export(name)
def _clear_batch():
    for bm, _m in BATCH.values(): bm.free()
    BATCH.clear(); _k_clear()
K.export, K.clear = _export_flushing, _clear_batch

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
            bcone('tuft', R.uniform(0.07, 0.12), R.uniform(0.3, 0.55), (x + R.uniform(-0.12, 0.12), y + R.uniform(-0.12, 0.12), 0.18), M(R.choice(mats)), 4,
                  rot=(R.uniform(-0.35, 0.35), R.uniform(-0.35, 0.35), 0))

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
        # trampled grass, not a dug road (owner, Nov 14 2026: "fix the paths of dirt. Not sure if they are really
        # required"; scuffed earth patches read as stepping stones): a faint lighter strip with ragged edges where the
        # horde has flattened the meadow, bare earth only at the tower's foot where they mass
        left, right = [], []
        for k in range(n + 1):
            u = u0 + (u1 - u0) * k / n
            hl, hr = 1.25 + R.uniform(-0.25, 0.25), 1.25 + R.uniform(-0.25, 0.25)
            left.append((fx + dx * u + px * hl, fz + dz * u + pz * hl, 0.026)); right.append((fx + dx * u - px * hr, fz + dz * u - pz * hr, 0.026))
        N = n + 1
        mesh_from('trodden', left + right, [(N + k, N + k + 1, k + 1, k) for k in range(n)], M('grass3'), 0.0)
        for _ in range(5):   # a little bare earth just before the tower
            u = R.uniform(1.5, 5.0); off = R.uniform(-0.8, 0.8); cx, cy = fx + dx * u + px * off, fz + dz * u + pz * off
            rr = R.uniform(0.4, 0.75); n_ = 7
            vs = [(cx + math.cos(k / n_ * math.tau) * rr * R.uniform(0.75, 1.15), cy + math.sin(k / n_ * math.tau) * rr * R.uniform(0.75, 1.15), 0.032) for k in range(n_)]
            mesh_from('scuff', vs, [tuple(range(n_))], M('dirt'), 0.0)
        # the apron at the tower's foot: trampled earth in a rough half disc, rubble from the fighting
        vs = [(fx, fz, 0.035)]
        for k in range(13):
            ang = math.atan2(dz, dx) - math.pi / 2 - 0.35 + (math.pi + 0.7) * k / 12
            r = 2.4 * R.uniform(0.8, 1.1); vs.append((fx + math.cos(ang) * r, fz + math.sin(ang) * r, 0.035))
        mesh_from('apron', vs, [(0, k + 1, k + 2) for k in range(12)], M('dirt'), 0.0)
        rubble(fx + px * 2.2, fz + pz * 2.2, 3, (0.5, 0.5)); rubble(fx - px * 2.2, fz - pz * 2.2, 3, (0.5, 0.5))
        for _ in range(int(L0 * 0.5)):   # a few stones kicked aside
            u = R.uniform(2.5, u1 - 1); side = R.choice((-1, 1)); off = side * R.uniform(0.9, 1.6)
            for k in range(R.randint(1, 3)):
                sz_ = R.uniform(0.14, 0.28)
                o = block((fx + dx * u + px * off + R.uniform(-0.3, 0.3), fz + dz * u + pz * off + R.uniform(-0.3, 0.3), 0.07), (sz_ * 1.3, sz_, sz_ * 0.5), M(R.choice(('cobble2', 'rock', 'rock_dk'))), 0.03)
                o.rotation_euler = (0, 0, R.uniform(0, 3.1))
        for _ in range(int(L0 * 0.7)):
            u = R.uniform(3, u1 - 1); side = R.choice((-1, 1)); off = side * (L.LANE_HALF + 0.1)
            tufts(fx + dx * u + px * off, fz + dz * u + pz * off, 2, (0.4, 0.4))
    # the road to the gate: a cobbled way straight up the middle from beyond the frame to the gatehouse's step, kerbed,
    # worn smoother down its middle, with milestones; traders' road, not a lane (the monsters keep to their four)
    z0, z1 = 2.2, 34.0; n = 30
    left, right = [], []   # the bed: dark mortar showing between the stones
    for k in range(n + 1):
        zz = z0 + (z1 - z0) * k / n
        left.append((L.ROAD_HALF + R.uniform(-0.06, 0.06), zz, 0.03)); right.append((-L.ROAD_HALF + R.uniform(-0.06, 0.06), zz, 0.03))
    N = n + 1; mesh_from('road', left + right, [(k, k + 1, N + k + 1, N + k) for k in range(n)], M('mortar'), 0.0)
    # the setts: courses 0.42 m deep laid across the road, each stone a raised top with its camera-facing edge, joints
    # 6 cm, every course offset like brickwork; four tones, lighter down the worn middle (owner, Nov 14 2026: the first
    # road read as a flat grey strip)
    faces = {m: ([], []) for m in ('cobble', 'cobble2', 'stone2', 'stone3', 'stone_dk')}
    zz, row = z0 + 0.05, 0
    while zz < z1 - 0.3:
        d = R.uniform(0.36, 0.46); x = -L.ROAD_HALF + 0.04 + (0.25 if row % 2 else 0.0) * R.uniform(0.6, 1.0)
        while x < L.ROAD_HALF - 0.12:
            w = min(R.uniform(0.38, 0.62), L.ROAD_HALF - 0.04 - x)
            a_, b_, c_, e_ = x, x + w - 0.06, zz, zz + d - 0.06
            mid = abs(x + w / 2) < 1.0
            m = R.choice(('cobble', 'cobble', 'cobble', 'cobble2', 'stone2') if mid else ('cobble', 'cobble2', 'cobble2', 'cobble2', 'stone_dk'))   # close tones: light stones in the middle read as a checkerboard
            top = 0.03 + R.uniform(0.05, 0.09); vs, fs = faces[m]; i = len(vs)
            j = R.uniform(-0.015, 0.015)   # a hand-laid stone is never quite square
            vs += [(b_ + j, c_, top), (b_, e_ + j, top), (a_ - j, e_, top), (a_, c_ - j, top), (a_ - j, e_, 0.03), (b_, e_ + j, 0.03)]
            fs += [(i, i + 1, i + 2, i + 3), (i + 2, i + 1, i + 5, i + 4)]
            x += w
        zz += d; row += 1
    for m, (vs, fs) in faces.items():
        if vs: mesh_from('setts', vs, fs, M(m), 0.0)
    for s in (-1, 1):   # kerbs, a stone every 0.9 m
        zz = z0 + 0.3
        while zz < z1:
            L0 = R.uniform(0.6, 0.85)
            block((s * (L.ROAD_HALF + 0.1), zz + L0 / 2, 0.1), (0.26, L0 - 0.05, 0.2), M(R.choice(('stone2', 'stone3', 'stone_dk'))), 0.03)
            zz += L0
        for zz in (9.0, 20.0):   # milestones
            block((s * (L.ROAD_HALF + 0.7), zz, 0.45), (0.36, 0.26, 0.9), M('stone3'), 0.06)
            K.sphere('cap', 0.2, (s * (L.ROAD_HALF + 0.7), zz, 0.95), M('stone2'), (1, 0.8, 0.6), 1)
    for k in range(10): tufts(R.choice((-1, 1)) * (L.ROAD_HALF + 0.45), R.uniform(3, 30), 2, (0.2, 0.4))
    return export('road')
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
        bcyl('trunk', 0.18 * s, 1.2 * s, (x, y, z + 0.6 * s), M('wood_dk'), 6, 0.0)
        for k, (dz, r, h) in enumerate(((1.4, 1.25, 1.9), (2.4, 0.95, 1.6), (3.3, 0.65, 1.3))):
            bcone('needles', r * s, h * s, (x, y, z + dz * s), M('tree_far' if far else 'pine'), 8)
    else:
        bcyl('trunk', 0.22 * s, 1.6 * s, (x, y, z + 0.8 * s), M('wood_dk'), 8, 0.0, r2=0.15 * s)
        for (dz, r, dx, dy) in ((1.9, 1.15, 0, 0), (2.6, 0.9, 0.35, 0.1), (2.55, 0.85, -0.4, -0.15), (3.25, 0.65, 0.05, 0.05)):
            bsphere('crown', r * s, (x + dx * s, y + dy * s, z + dz * s), M('tree_far' if far else R.choice(('tree', 'tree2'))), (1, 1, 0.88), 2)

def outcrop(x, y, n, spread, scale=1.0):   # a cluster of big angular rocks
    for _ in range(n):
        r = R.uniform(0.8, 1.9) * scale
        bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1, radius=r, location=(x + R.uniform(-spread, spread), y + R.uniform(-spread, spread), r * 0.35))
        b = bpy.context.object; b.scale = (R.uniform(0.9, 1.4), R.uniform(0.8, 1.2), R.uniform(0.6, 1.0)); b.rotation_euler = (0, 0, R.uniform(0, 3))
        for v in b.data.vertices: v.co *= R.uniform(0.85, 1.12)
        K.finish(b, M(R.choice(('rock', 'rock_dk', 'stone2'))), 0.0, smooth=False)

# ---------------------------------------------------------------------------------------------------------------------
# the town (owner, Nov 14 2026: "you can do the housing spaced inside circular facing the interior. makes the inside match
# a circular cobble floor matching the path of the gate"): grass inside the ring, a cobbled square laid in rings round the
# fountain and kerbed, the road through the gate running on into it, a short path from each door, and gardens between
SETT_MATS = ('cobble', 'cobble', 'cobble', 'cobble2', 'cobble2', 'stone2')   # close tones; a few warm stones

def _sett_bag(): return {m: ([], []) for m in SETT_MATS + ('stone_dk', 'stone3')}
def _flush(bag, name='setts'):
    for m, (vs, fs) in bag.items():
        if vs: mesh_from(name, vs, fs, M(m), 0.0)

def setts_rect(bag, org, eu, ev, u0, u1, v0, v1, size=(0.52, 0.44), keep=None):
    """Setts in courses across u, laid from v0 to v1 in the frame (org, eu, ev) on the ground; a stone's camera-facing
    edge (+y) shows its thickness. keep(x, y) can drop a stone (where another floor already lies)."""
    v, row = v0 + 0.03, 0
    while v < v1 - 0.2:
        d = min(size[1] * R.uniform(0.9, 1.1), v1 - v); u = u0 + 0.03 + (size[0] * 0.5 * R.uniform(0.6, 1.0) if row % 2 else 0)
        while u < u1 - 0.12:
            w = min(size[0] * R.uniform(0.8, 1.2), u1 - 0.03 - u)
            a, b, c, e = u, u + w - 0.06, v, v + d - 0.06
            P = lambda uu, vv: (org[0] + eu[0] * uu + ev[0] * vv, org[1] + eu[1] * uu + ev[1] * vv)
            cx, cy = P((a + b) / 2, (c + e) / 2)
            if keep is None or keep(cx, cy):
                m = R.choice(SETT_MATS); top = 0.03 + R.uniform(0.05, 0.08); j = R.uniform(-0.012, 0.012)
                q = [P(a + j, c), P(b, c - j), P(b - j, e), P(a, e + j)]   # counter-clockwise from above when eu x ev points up
                vs, fs = bag[m]; i = len(vs)
                vs += [(x, y, top) for x, y in q]; fs.append((i, i + 1, i + 2, i + 3))
                k = max(range(4), key=lambda n: (q[n][1] + q[(n + 1) % 4][1]))   # the edge nearest the camera (+y)
                (x0, y0), (x1, y1) = q[k], q[(k + 1) % 4]
                vs += [(x0, y0, 0.03), (x1, y1, 0.03)]; fs.append((i + k, i + 4, i + 5, i + (k + 1) % 4))
            u += w
        v += d; row += 1

def setts_rings(bag, cx, cy, A, B, course=0.46, length=0.56):
    """The square: setts in rings round its centre, out to the ellipse A x B; only whole stones inside it."""
    r = 1.0
    while r < max(A, B):
        n = max(6, int(math.tau * r / length)); ph = R.uniform(0, math.tau)
        for k in range(n):
            t0, t1 = ph + k / n * math.tau, ph + (k + 1) / n * math.tau - 0.055 / r
            r0, r1 = r, r + course - 0.055
            q = [(cx + rr * math.cos(t), cy + rr * math.sin(t)) for rr, t in ((r0, t0), (r1, t0), (r1, t1), (r0, t1))]   # counter-clockwise from above
            if any(((x - cx) / A) ** 2 + ((y - cy) / B) ** 2 > 0.985 for x, y in q): continue
            m = R.choice(SETT_MATS); top = 0.03 + R.uniform(0.05, 0.08); vs, fs = bag[m]; i = len(vs)
            vs += [(x, y, top) for x, y in q]; fs.append((i, i + 1, i + 2, i + 3))
        r += course

def ellipse_pts(cx, cy, A, B, n=96): return [(cx + A * math.cos(k / n * math.tau), cy + B * math.sin(k / n * math.tau)) for k in range(n)]

def lamp(x, y):
    bcyl('lamp_base', 0.16, 0.3, (x, y, 0.15), M('stone_dk'), 8, 0.02)
    bcyl('lamp_post', 0.06, 2.6, (x, y, 1.45), M('iron'), 8, 0.0)
    block((x, y, 2.85), (0.34, 0.34, 0.42), M('window', 1.6), 0.03)
    bcone('lamp_cap', 0.3, 0.25, (x, y, 3.18), M('iron'), 4, rot=(0, 0, math.pi / 4))
    for sx, sy in ((1, 1), (1, -1), (-1, 1), (-1, -1)): block((x + sx * 0.17, y + sy * 0.17, 2.85), (0.04, 0.04, 0.44), M('iron'), 0.0)

def bench(x, y, f):
    r = (f[1], -f[0]); P = lambda u, v, z: (x + r[0] * u + f[0] * v, y + r[1] * u + f[1] * v, z)
    rot = math.atan2(r[1], r[0])
    block(P(0, 0, 0.45), (1.5, 0.44, 0.07), M('wood'), 0.02, rot)
    block(P(0, -0.2, 0.75), (1.5, 0.06, 0.32), M('wood'), 0.02, rot)
    for u in (-0.6, 0.6):
        block(P(u, 0, 0.22), (0.08, 0.4, 0.44), M('iron'), 0.0, rot); block(P(u, -0.2, 0.6), (0.06, 0.06, 0.4), M('iron'), 0.0, rot)

def stall(x, y, f):
    r = (f[1], -f[0]); P = lambda u, v, z: (x + r[0] * u + f[0] * v, y + r[1] * u + f[1] * v, z)
    rot = math.atan2(r[1], r[0])
    block(P(0, 0.1, 0.48), (2.2, 0.9, 0.9), M('wood'), 0.03, rot)          # the counter
    block(P(0, 0.1, 0.95), (2.3, 1.0, 0.06), M('wood_dk'), 0.02, rot)
    for k, m in enumerate(('flower_y', 'roof_red', 'hay', 'tree2')):        # goods in baskets
        u = -0.8 + k * 0.53; bcyl('basket', 0.2, 0.16, P(u, 0.25, 1.06), M('wood'), 10, 0.01)
        for _ in range(3): bsphere('goods', 0.08, P(u + R.uniform(-0.08, 0.08), 0.25 + R.uniform(-0.08, 0.08), 1.16), M(m), sub=1)
    for u in (-1.05, 1.05):
        for v in (-0.35, 0.55): bcyl('pole', 0.04, 2.2, P(u, v, 1.1), M('wood_dk'), 6, 0.0)
    for k in range(5):   # a striped awning sloping down toward the customers
        u0 = -1.2 + k * 0.48; m = 'awning' if k % 2 == 0 else 'canvas'
        vs = [P(u0, -0.45, 2.25), P(u0 + 0.48, -0.45, 2.25), P(u0 + 0.48, 0.85, 1.95), P(u0, 0.85, 1.95)]
        mesh_from('awning', vs, [(0, 1, 2, 3), (3, 2, 1, 0)], M(m), 0.0)   # seen from above and below

def barrels(x, y):
    for dx, dy, h in ((0, 0, 0), (0.45, 0.1, 0), (0.2, -0.38, 0)):
        bcyl('barrel', 0.24, 0.62, (x + dx, y + dy, 0.31), M('wood'), 12, 0.02)
        bcyl('hoop', 0.25, 0.05, (x + dx, y + dy, 0.5), M('iron'), 12, 0.0)
    block((x - 0.4, y + 0.3, 0.2), (0.4, 0.4, 0.4), M('wood'), 0.03, R.uniform(0, 1))

def flower_bed(x, y, r):
    n = 12
    mesh_from('soil', [(x + math.cos(k / n * math.tau) * r, y + math.sin(k / n * math.tau) * r, 0.1) for k in range(n)], [tuple(range(n))], M('soil'), 0.0)
    for k in range(n): bsphere('kerb', 0.13, (x + math.cos(k / n * math.tau) * r, y + math.sin(k / n * math.tau) * r, 0.08), M('stone2'), (1.3, 1, 0.7), 1)
    c = R.choice(('flower_y', 'flower_w', 'flower_p'))
    for _ in range(int(r * 18)):
        a, d = R.uniform(0, math.tau), R.uniform(0, r * 0.8)
        bsphere('flower', 0.08, (x + math.cos(a) * d, y + math.sin(a) * d, 0.22), M(c if R.random() < 0.7 else 'flower_w'), sub=1)
    for _ in range(4): bsphere('leaves', 0.16, (x + R.uniform(-r, r) * 0.6, y + R.uniform(-r, r) * 0.6, 0.15), M('bush'), (1, 1, 0.6), 1)

def town():
    cx, cy = L.CENTRE
    # the square: a mortar bed, setts in rings round the fountain, a kerb of dark stones round its edge
    mesh_from('square_bed', [(x, y, 0.025) for x, y in ellipse_pts(cx, cy, L.SQUARE_A, L.SQUARE_B)], [tuple(range(96))], M('mortar'), 0.0)
    bag = _sett_bag(); setts_rings(bag, cx, cy, L.SQUARE_A - 0.25, L.SQUARE_B - 0.25)
    pts = ellipse_pts(cx, cy, L.SQUARE_A, L.SQUARE_B, 260)
    for k in range(0, 260, 1):
        (x0, y0), (x1, y1) = pts[k], pts[(k + 1) % 260]
        if abs((x0 + x1) / 2) < L.GATE_HALF_ST and (y0 + y1) / 2 > cy: continue   # the gate road runs in here
        block(((x0 + x1) / 2, (y0 + y1) / 2, 0.06), (math.dist((x0, y0), (x1, y1)) - 0.05, 0.22, 0.12), M('stone_dk'), 0.02, math.atan2(y1 - y0, x1 - x0))
    # the road through the gate, on to the square (setts like the road outside, courses across it)
    g0, g1 = cy + L.SQUARE_B - 1.2, -1.2
    mesh_from('gate_bed', [(-L.GATE_HALF_ST, g0, 0.025), (L.GATE_HALF_ST, g0, 0.025), (L.GATE_HALF_ST, g1, 0.025), (-L.GATE_HALF_ST, g1, 0.025)], [(0, 1, 2, 3)], M('mortar'), 0.0)
    setts_rect(bag, (0, 0), (1, 0), (0, 1), -L.GATE_HALF_ST, L.GATE_HALF_ST, g0, g1, keep=lambda x, y: not L.in_square(x, y, -0.3))
    for sx in (-1, 1):   # its kerbs
        y = g0
        while y < g1 - 0.3:
            Ln = R.uniform(0.6, 0.85); block((sx * (L.GATE_HALF_ST + 0.08), y + Ln / 2, 0.08), (0.2, Ln - 0.05, 0.16), M(R.choice(('stone2', 'stone_dk'))), 0.02); y += Ln
    # a path from each door to the square, in the house's own frame
    for i, bid in enumerate(L.IDS):
        d = L.SIZES[bid][1] / 2; Ln = L.door_path(i) + 0.4
        (ox, oy), r, f = L.frame(i)
        org = L.world(i, 0, d)
        corners = [L.world(i, u, v) for u, v in ((-L.PATH_HALF, d), (L.PATH_HALF, d), (L.PATH_HALF, d + Ln), (-L.PATH_HALF, d + Ln))]
        if (r[0] * f[1] - r[1] * f[0]) < 0: corners.reverse()
        mesh_from('path_bed', [(x, y, 0.024) for x, y in corners], [(0, 1, 2, 3)], M('mortar'), 0.0)
        eu, ev = (r, f) if (r[0] * f[1] - r[1] * f[0]) > 0 else ((-r[0], -r[1]), f)
        setts_rect(bag, org, eu, ev, -L.PATH_HALF, L.PATH_HALF, 0.0, Ln, keep=lambda x, y: not L.in_square(x, y, -0.3))
    _flush(bag)
    # the gardens and the square's furniture (hg_layout.TOWN_PROPS: the wizard walks round each one)
    for kind, x, y, rad, yaw in L.TOWN_PROPS:
        f = ((cx - x), (cy - y)); Lf = math.hypot(*f) or 1; f = (f[0] / Lf, f[1] / Lf)   # facing the fountain
        if kind == 'lamp': lamp(x, y)
        elif kind == 'bench': bench(x, y, f)
        elif kind == 'stall': stall(x, y, f)
        elif kind == 'barrels': barrels(x, y)
        elif kind == 'bed': flower_bed(x, y, rad)
        elif kind == 'bush':
            for k in range(R.randint(3, 5)): bsphere('bush', R.uniform(0.35, 0.6), (x + R.uniform(-0.4, 0.4), y + R.uniform(-0.4, 0.4), 0.3), M(R.choice(('bush', 'grass2', 'tree'))), (1, 1, 0.8), 2)
        elif kind == 'tree': tree(x, y, rad * R.uniform(1.0, 1.15), 0.0, 'round')
    # tufts along the house walls and the lawn edges
    for i in range(len(L.IDS)):
        for c in L.corners(i, 0.2): tufts(c[0], c[1], 2, (0.3, 0.3))

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
    town()   # the square, the gate road, the door paths and the town's gardens (owner, Nov 14 2026)
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
        for k in range(R.randint(5, 10)): bsphere('flower', 0.07, (cx + R.uniform(-0.8, 0.8), cy + R.uniform(-0.6, 0.6), 0.12), M(c), sub=1)
    scatter(30, 1.0, box, flowers)
    scatter(8, 1.0, box, lambda x, y: (outcrop(x, y, 2, 0.6, 0.35), tufts(x, y, 3, (0.9, 0.7))))
    def bushes(cx, cy):
        for k in range(R.randint(3, 6)): bsphere('bush', R.uniform(0.45, 0.95), (cx + R.uniform(-1.0, 1.0), cy + R.uniform(-0.6, 0.6), 0.35), M(R.choice(('bush', 'grass2', 'tree'))), (1, 1, 0.8), 2)
    scatter(22, 1.6, box, bushes)
    def swathe(cx, cy):
        n = 9; rr = R.uniform(1.5, 3.0)
        vs = [(cx + math.cos(i / n * math.tau) * rr * R.uniform(0.7, 1.2), cy + math.sin(i / n * math.tau) * rr * R.uniform(0.5, 0.9), 0.025) for i in range(n)]
        mesh_from('swathe', vs, [tuple(range(n))], M(R.choice(('grass2', 'grass3'))), 0.0)
    scatter(40, 3.2, ((-L.RX - 14, L.RX + 14), (-2 * L.RZ - 2, 30)), swathe)
    # no trees in the near meadow: in a tall window they stood between the camera and the lanes (Nov 13 2026)
    props()
    # dark framing clumps at the bottom corners of the view
    for (cx, cy) in ((24, 17), (27, 21), (-24, 17), (-27, 21)):
        for k in range(3):
            x, y = cx + R.uniform(-2, 2), cy + R.uniform(-1.5, 1.5)
            if free(x, y, 1.6): tree(x, y, R.uniform(1.4, 1.8), 0, 'round', far=True)
    return export('ground')

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

# ---------------------------------------------------------------------------------------------------------------------
# the enemy variants (the 2D game's VARIANT_FROM, Nov 14 2026: "we are still not there yet with game core"): each one a
# base enemy recoloured with one tell the player learns. A shield bubble is an 'upper' part (UP_), so the game can pop it
# when Storm breaks the shield.
def _remap(remap):
    for o in bpy.context.scene.objects:
        if o.type != 'MESH': continue
        for k, m in enumerate(o.data.materials):
            if m is None: continue
            base = m.name[3:].replace('_glow', '') if m.name.startswith('HG_') else m.name
            if base in remap: o.data.materials[k] = M(*remap[base]) if isinstance(remap[base], tuple) else M(remap[base])

def _shield(r, z):   # two glowing rings of force round it (the materials are opaque: a bubble would hide the enemy)
    for k, tilt in enumerate((0.35, -0.35)):
        bpy.ops.mesh.primitive_torus_add(major_radius=r, minor_radius=0.05, major_segments=40, minor_segments=8, location=(0, 0, z + k * 0.15))
        o = bpy.context.object; o.name = 'UP_shield%d' % k; o.rotation_euler = (tilt, 0, k * 1.2)
        bpy.ops.object.transform_apply(location=False, rotation=True, scale=False)   # baked: a joined part keeps no turn of its own
        K.finish(o, M('water_gloss', 1.6))

def variant(kind):
    base = {'elite': beetle, 'shielded': beetle, 'sprinter': beetle, 'splitter': slime, 'mender': slime, 'ironhide': golem, 'bulwark': golem}[kind]
    real = K.export; K.export = lambda name: 0
    try: base()
    finally: K.export = real
    K.flush_blocks(); flush_batch()
    if kind == 'elite':      # gold shell and a little crown: it drops a treasure chest
        _remap({'beetle_gloss': 'gold'})
        bcyl('crown', 0.32, 0.22, (0, -0.55, 1.42), M('gold', 0.6), 8, 0.0, r2=0.38)
        for k in range(5): bcone('point', 0.07, 0.2, (math.cos(k * 1.257) * 0.32, -0.55 + math.sin(k * 1.257) * 0.32, 1.6), M('gold', 0.6), 6)
    elif kind == 'shielded':  # a blue bubble: Storm breaks it
        _remap({'beetle_gloss': ('cloth',)}); _shield(1.25, 0.75)
    elif kind == 'sprinter':  # pale and lean: it dashes for the walls
        _remap({'beetle_gloss': 'stone3'})
    elif kind == 'splitter':  # violet with a dark seam round its middle: it bursts into two
        _remap({'slime_gloss': 'roof_purple', 'slime_dk': 'robe_dk'})   # violet, far from the plain slime's pink
        bcyl('seam', 0.86, 0.1, (0, 0, 0.72), M('robe_dk'), 32, 0.0)
    elif kind == 'mender':    # green, a glowing ring over its head: it heals its friends until it burns
        _remap({'slime_gloss': 'bush', 'slime_dk': 'grass2'})
        bpy.ops.mesh.primitive_torus_add(major_radius=0.42, minor_radius=0.06, location=(0, 0, 1.95)); K.finish(bpy.context.object, M('tree2', 1.5))
    elif kind == 'ironhide':  # dark iron plates and a bubble: Storm breaks the shield, then Fire the armour
        _remap({'golem': 'iron', 'rock': 'iron', 'rock_dk': 'pupil', 'storm': ('water_gloss', 3.0)}); _shield(1.9, 1.3)   # dark iron, blue light in its cracks
    elif kind == 'bulwark':   # grey stone with a great slab carried in front: combos crack it for double
        _remap({'golem': 'stone3', 'rock': 'stone2', 'rock_dk': 'stone_dk', 'storm': ('fire_core', 2.0)})   # pale stone, ember cracks
        block((0, -1.05, 1.3), (1.6, 0.25, 1.9), M('stone3'), 0.06); block((0, -1.2, 1.3), (0.3, 0.1, 1.2), M('iron'), 0.02)
    return K.export(kind)

def villager(v):
    """The townsfolk (owner, Nov 14 2026: a town that is lived in), in the wizard's style and units (the game scales them to
    about 1.6 m): a: the baker (cream tunic, white apron and cap), b: the farmer (green tunic, straw hat), c: a townswoman
    (red dress, apron, headscarf, a basket on her arm). Faces -y like the wizard."""
    from mathutils import Euler
    top = {'a': 'canvas', 'b': 'tunic_g', 'c': 'dress'}[v]
    if v == 'c':   # a long skirt to the ground
        K.cyl('skirt', 0.5, 0.95, (0, 0, 0.5), M('dress'), 18, 0.03, r2=0.32)
        K.cyl('hem', 0.51, 0.06, (0, 0, 0.05), M('apron'), 18, 0.0)
    else:
        for sx in (-1, 1): K.cyl('leg', 0.13, 0.5, (sx * 0.14, 0, 0.3), M('trousers'), 10, 0.02)
        K.cyl('tunic', 0.42, 0.55, (0, 0, 0.78), M(top), 18, 0.03, r2=0.33)
    for sx in (-1, 1): K.sphere('boot', 0.13, (sx * 0.15, -0.08, 0.07), M('wood_dk'), (1, 1.5, 0.6), 2)
    K.cyl('belt', 0.36, 0.08, (0, 0, 0.98), M('wood_dk'), 16, 0.02)
    K.sphere('chest', 0.36, (0, 0, 1.12), M(top), (1, 0.85, 0.85), 3)
    if v in 'ac':   # an apron over the front
        block((0, -0.36, 0.72), (0.5, 0.05, 0.62 if v == 'a' else 0.75), M('apron'), 0.02).rotation_euler = (-0.12, 0, 0)
    for sx in (-1, 1):   # sleeves hanging from the shoulders, hands at the cuffs
        c = Vector((sx * 0.4, -0.04, 1.15)); rot = Euler((-0.2, -sx * 0.32, 0))
        def along(d): w = Vector((0, 0, d)); w.rotate(rot); return c + w
        o = K.cyl('sleeve', 0.13, 0.48, tuple(along(-0.2)), M(top), 10, 0.02, r2=0.11); o.rotation_euler = rot
        K.sphere('hand', 0.1, tuple(along(-0.47)), M('skin'), sub=2)
    if v == 'c':   # a basket on her right arm
        K.cyl('basket', 0.17, 0.18, (0.5, -0.12, 0.72), M('wood'), 12, 0.01)
        for k in range(3): K.sphere('bread', 0.07, (0.46 + k * 0.05, -0.12, 0.84), M('hay'), sub=1)
    # the head: round face, little eyes, a rosy nose, hair
    K.sphere('head', 0.3, (0, -0.02, 1.55), M('skin'), sub=3)
    K.sphere('nose', 0.06, (0, -0.31, 1.53), M('blush'), sub=2)
    for sx in (-1, 1):
        K.sphere('eye', 0.05, (sx * 0.11, -0.27, 1.62), M('pupil'), (1, 0.6, 1.2), 2)
        K.sphere('catch', 0.015, (sx * 0.1, -0.31, 1.64), M('white_gloss', 0.5), sub=1)
    hair = 'hair2' if v == 'b' else 'hair'
    K.sphere('hair', 0.31, (0, 0.05, 1.62), M(hair), (1, 1, 0.9), 3)
    if v == 'a':   # the baker's cap
        K.cyl('cap', 0.3, 0.22, (0, 0.02, 1.85), M('apron'), 16, 0.03, r2=0.34)
    elif v == 'b':   # a wide straw hat
        K.cyl('brim', 0.55, 0.05, (0, 0, 1.78), M('hay'), 24, 0.02)
        K.cyl('crown', 0.3, 0.24, (0, 0, 1.92), M('hay'), 18, 0.02, r2=0.24)
        K.cyl('band', 0.31, 0.06, (0, 0, 1.84), M('wood_dk'), 18, 0.0)
    else:   # a headscarf, knotted at the back
        K.sphere('scarf', 0.33, (0, 0.06, 1.66), M('canvas'), (1, 1, 0.85), 3)
        K.sphere('knot', 0.09, (0, 0.34, 1.52), M('canvas'), sub=2)
    return K.export('villager_' + v)
