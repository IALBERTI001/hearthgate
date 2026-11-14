# Hearthgate 3D, the front (Nov 13 2026): the gatehouse, the curtain wall, the element towers and their tops, the road,
# the ground, the fountain and the slime, built to the art standard rather than as block-outs. Imported by hg_kit.py,
# which owns the helpers, the palette and the export.
#
# Coordinates are Unity's: x right as seen from the city (the camera stands north, so screen right is world -x), y in
# Blender = Unity z (north, toward the camera), z up. The camera looks at the +y faces, so those carry the detail.
# Layout numbers (gate at the origin, wall along y = 0, towers at x = -7.5, -13, -18.5, -24, road centre y = 5.4,
# plaza at (0, -10)) match Assets/Scripts/Game/Field.cs; LayoutCheck verifies them on these models.
#
# Owner's rules this file answers: no towers crowding the entrance (one gatehouse, towers spaced on the wall); nothing
# looks pasted on (every structure sits on a plinth with rubble and grass at its foot); effects match each tower's element.
import bpy, bmesh, math, random
from mathutils import Vector
import hg_kit as K

R = random.Random(13)
STONES = ('stone', 'stone2', 'stone3')

def M(name, glow=0.0): return K.mat(name, glow)

def block(loc, size, m, bevel=0.035, rot=0.0):
    o = K.cube('blk', size, loc, m, bevel)
    if rot: o.rotation_euler = (0, 0, rot)
    return o

# ---------------------------------------------------------------------------------------------------------------------
# masonry: rows of bevelled stones laid on a face, with the joints showing, in three tints
def masonry(u0, u1, z0, z1, place, course=0.52, depth=0.1, skip=None, lmin=0.55, lmax=1.25):
    """place(u, z, length, height) -> (loc, size, rot) for one stone; skip(u0, u1, z0, z1) -> True to leave a gap."""
    z, row = z0, 0
    while z < z1 - 0.05:
        h = min(course, z1 - z)
        u = u0 - (R.uniform(0.2, 0.5) if row % 2 else 0)
        while u < u1 - 0.05:
            L = R.uniform(lmin, lmax)
            a, b = max(u, u0), min(u + L, u1)
            if b - a > 0.15 and not (skip and skip(a, b, z, z + h)):
                loc, size, rot = place((a + b) / 2, z + h / 2, b - a - 0.05, h - 0.05)
                block(loc, size, M(R.choice(STONES)), 0.035, rot)
            u += L
        z += h; row += 1

def face_y(y, sign, depth=0.1):   # stones on a face whose normal is +-y
    return lambda u, z, L, h: ((u, y + sign * depth / 2, z), (L, depth, h), 0.0)
def face_x(x, sign, depth=0.1):   # stones on a face whose normal is +-x
    return lambda u, z, L, h: ((x + sign * depth / 2, u, z), (depth, L, h), 0.0)

def rubble(cx, cy, n, spread, zmax=0.35):
    for _ in range(n):
        s = R.uniform(0.18, 0.42)
        o = block((cx + R.uniform(-spread[0], spread[0]), cy + R.uniform(-spread[1], spread[1]), s * 0.35), (s * 1.3, s, s * 0.7), M('stone_dk'), 0.06)
        o.rotation_euler = (R.uniform(-0.2, 0.2), R.uniform(-0.2, 0.2), R.uniform(0, 3.1))

def tufts(cx, cy, n, spread):
    for _ in range(n):
        x, y = cx + R.uniform(-spread[0], spread[0]), cy + R.uniform(-spread[1], spread[1])
        for k in range(3):
            o = K.cone('tuft', R.uniform(0.07, 0.12), R.uniform(0.35, 0.6), (x + R.uniform(-0.12, 0.12), y + R.uniform(-0.12, 0.12), 0.2), M(R.choice(('grass', 'grass2'))), 4)
            o.rotation_euler = (R.uniform(-0.35, 0.35), R.uniform(-0.35, 0.35), 0)

def merlons(u0, u1, z, place, w=0.62, gap=0.42, h=0.6):
    n = int((u1 - u0 + gap) // (w + gap)); start = u0 + ((u1 - u0) - (n * w + (n - 1) * gap)) / 2
    for i in range(n):
        u = start + i * (w + gap) + w / 2
        loc, size = place(u, z + h / 2, w, h)
        block(loc, size, M(R.choice(STONES)), 0.045)

def banner(x, y, ztop, zbot, w, cloth='banner', trim='gold'):
    """A hanging banner on a +y face: a rod, a cloth with a gentle wave and a swallowtail, a gold hearth sigil."""
    K.cyl('rod', 0.05, w + 0.35, (x, y + 0.12, ztop + 0.05), M('iron'), 8, 0.0).rotation_euler = (0, math.pi / 2, 0)
    for s in (-1, 1): K.sphere('knob', 0.08, (x + s * (w / 2 + 0.18), y + 0.12, ztop + 0.05), M(trim), sub=2)
    bm = bmesh.new(); nx, nz = 6, 14
    verts = []
    for j in range(nz + 1):
        t = j / nz; z = ztop - t * (ztop - zbot)
        row = []
        for i in range(nx + 1):
            u = i / nx; xx = x - w / 2 + u * w
            zz = z
            if j == nz: zz = zbot + abs(u - 0.5) * 0.9 * w   # swallowtail cut
            yy = y + 0.1 + 0.06 * math.sin(t * 5.5 + u * 1.3) * t
            row.append(bm.verts.new((xx, yy, zz)))
        verts.append(row)
    for j in range(nz):
        for i in range(nx):
            bm.faces.new((verts[j][i], verts[j][i + 1], verts[j + 1][i + 1], verts[j + 1][i]))
    me = bpy.data.meshes.new('banner'); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new('banner', me); bpy.context.collection.objects.link(o)
    sol = o.modifiers.new('thick', 'SOLIDIFY'); sol.thickness = 0.04
    K.finish(o, M(cloth), 0.0)
    # gold trim along the sides and a diamond sigil with a flame
    for s in (-1, 1): block((x + s * (w / 2 - 0.05), y + 0.17, (ztop + zbot) / 2 + 0.15), (0.08, 0.04, ztop - zbot - 0.4), M(trim), 0.0)
    d = block((x, y + 0.2, ztop - (ztop - zbot) * 0.38), (0.42, 0.04, 0.42), M(trim), 0.01); d.rotation_euler = (0, math.pi / 4, 0)
    K.sphere('sigil', 0.12, (x, y + 0.24, ztop - (ztop - zbot) * 0.38 - 0.02), M('fire', 1.5), (1, 0.5, 1.4), 2)

def torch(x, y, z):
    block((x, y + 0.08, z - 0.15), (0.16, 0.16, 0.34), M('iron'), 0.02)
    K.cyl('cup', 0.17, 0.2, (x, y + 0.22, z + 0.08), M('iron'), 10, 0.02, r2=0.1)
    K.cone('flame', 0.13, 0.45, (x, y + 0.22, z + 0.38), M('fire', 4.0), 10)
    K.cone('flame_in', 0.07, 0.3, (x, y + 0.24, z + 0.34), M('gold', 5.0), 8)

# ---------------------------------------------------------------------------------------------------------------------
# the gatehouse: one mass over the road's end, an arched passage with a half-raised portcullis and closed doors behind,
# voussoirs and quoins, a corbel band, a parapet with merlons, the hearth crest, banners and torches
GX, GY = 4.0, 2.1          # half width, half depth
AW, ASPR = 1.5, 2.4        # arch half width and springing height
BODY, PARA = 4.8, 5.45     # top of the body, top of the parapet wall

def arch_top(x): return ASPR + math.sqrt(max(0.0, AW * AW - x * x))

def gate():
    # plinth with a batter, then the body with the passage cut through it
    K.cube('plinth', (2 * GX + 0.5, 2 * GY + 0.5, 0.6), (0, 0, 0.3), M('stone_dk'), 0.06)
    body = K.cube('body', (2 * GX, 2 * GY, BODY - 0.6), (0, 0, 0.6 + (BODY - 0.6) / 2), M('mortar'), 0.0)
    cut = K.cube('cut', (2 * AW, 2 * GY + 2, ASPR + 0.1), (0, 0, ASPR / 2 - 0.05), M('mortar'), 0.0)
    bpy.ops.mesh.primitive_cylinder_add(vertices=40, radius=AW, depth=2 * GY + 2, location=(0, 0, ASPR), rotation=(math.pi / 2, 0, 0))
    cyl = bpy.context.object
    for c in (cut, cyl):
        bo = body.modifiers.new('arch', 'BOOLEAN'); bo.operation = 'DIFFERENCE'; bo.object = c; bo.solver = 'EXACT'
        bpy.context.view_layer.objects.active = body; bpy.ops.object.modifier_apply(modifier=bo.name)
    for c in (cut, cyl): bpy.data.objects.remove(c, do_unlink=True)
    # the passage floor and the dark doors at its far end
    K.cube('floor', (2 * AW, 2 * GY, 0.62), (0, 0, 0.31), M('cobble'), 0.0)
    for s in (-1, 1):
        for k in range(4):   # planks of each door leaf
            x = s * (AW - 0.19 - k * 0.37)
            h = arch_top(abs(x) + 0.18) - 0.65
            block((x, -0.9, 0.62 + h / 2), (0.35, 0.16, h), M('wood_dk' if k % 2 else 'wood'), 0.02)
    for zb in (1.1, 2.1, 3.0):   # iron bands and studs
        w = 2 * math.sqrt(max(0.0, AW * AW - max(0.0, zb - ASPR) ** 2)) - 0.1 if zb > ASPR else 2 * AW - 0.1
        block((0, -0.8, zb), (w, 0.05, 0.12), M('iron'), 0.0)
    # portcullis, raised to 1.35 m, in its groove just behind the front face
    yp = GY - 0.45
    for i in range(8):
        x = -AW + 0.2 + i * (2 * AW - 0.4) / 7
        top = arch_top(abs(x)) - 0.05
        block((x, yp, (1.35 + top) / 2), (0.09, 0.09, top - 1.35), M('iron'), 0.0)
        K.cone('spike', 0.07, 0.28, (x, yp, 1.22), M('iron'), 6).rotation_euler = (math.pi, 0, 0)
    for zb in (1.5, 2.05, 2.6, 3.15, 3.65):
        if zb >= arch_top(0): continue
        hw = math.sqrt(max(0.0, AW * AW - max(0.0, zb - ASPR) ** 2)) if zb > ASPR else AW
        block((0, yp, zb), (2 * hw - 0.12, 0.07, 0.07), M('iron'), 0.0)
    # masonry on the front (+y) and the two outer sides, leaving the arch and its stone ring clear
    def skip_front(a, b, z0, z1):
        if b > -2.15 and a < 2.15 and z0 < ASPR + 2.1: # inside the voussoir ring or the jambs
            for x in (a, b, (a + b) / 2):
                ax = abs(x)
                if ax < 2.1 and z0 < ASPR + math.sqrt(max(0.0, 2.1 ** 2 - min(ax, 2.1) ** 2)): return True
        return False
    masonry(-GX, GX, 0.6, BODY, face_y(GY, 1), skip=skip_front)
    for s in (-1, 1): masonry(-GY, GY, 0.6, BODY, face_x(s * GX, s))
    masonry(-GX, GX, 0.6, BODY, face_y(-GY, -1), skip=skip_front, lmin=0.8, lmax=1.6)   # the city side, plainer
    # voussoirs: a ring of wedge stones round the arch, the keystone proud and gold-capped
    n = 11
    for i in range(n):
        a0, a1 = math.pi * i / n, math.pi * (i + 1) / n
        am = (a0 + a1) / 2
        r0, r1 = AW + 0.02, AW + (0.68 if i == n // 2 else 0.55)
        bm = bmesh.new()
        pts = []
        for (r, a) in ((r0, a0 + 0.012), (r1, a0 + 0.012), (r1, a1 - 0.012), (r0, a1 - 0.012)):
            pts.append((math.cos(a) * r, math.sin(a) * r + ASPR))
        depth = 0.18 if i == n // 2 else 0.13
        vs = [bm.verts.new((x, GY + yy, z)) for yy in (0.0, depth) for (x, z) in pts]
        for f in ((0, 1, 2, 3), (7, 6, 5, 4), (0, 4, 5, 1), (1, 5, 6, 2), (2, 6, 7, 3), (3, 7, 4, 0)):
            bm.faces.new([vs[k] for k in f])
        me = bpy.data.meshes.new('vous'); bm.to_mesh(me); bm.free()
        o = bpy.data.objects.new('vous', me); bpy.context.collection.objects.link(o)
        K.finish(o, M('stone3' if i == n // 2 else R.choice(STONES)), 0.025, smooth=False)
    # quoins: alternating long and short stones up both jambs
    for s in (-1, 1):
        z, k = 0.6, 0
        while z < ASPR - 0.05:
            L = 0.62 if k % 2 else 0.42; h = min(0.45, ASPR - z)
            block((s * (AW + L / 2 + 0.02), GY + 0.07, z + h / 2), (L - 0.04, 0.14, h - 0.04), M('stone3'), 0.03)
            z += h; k += 1
    # the passage walls inside the arch, so the opening reads deep
    for s in (-1, 1): block((s * (AW + 0.05), 0, 0.62 + (ASPR - 0.62) / 2), (0.1, 2 * GY - 0.2, ASPR - 0.62), M('stone_dk'), 0.0)
    # corbel band and the overhanging parapet with merlons on front and sides
    for x in [-GX + 0.3 + i * 0.62 for i in range(13)]:
        block((x, GY + 0.14, BODY - 0.12), (0.34, 0.28, 0.24), M('stone_dk'), 0.03)
        block((x, GY + 0.08, BODY - 0.32), (0.26, 0.16, 0.18), M('stone_dk'), 0.03)
    K.cube('parapet', (2 * GX + 0.36, 2 * GY + 0.36, PARA - BODY), (0, 0, BODY + (PARA - BODY) / 2), M('stone'), 0.05)
    masonry(-GX - 0.18, GX + 0.18, BODY, PARA, face_y(GY + 0.18, 1, 0.06), course=0.33, lmin=0.4, lmax=0.8)
    merlons(-GX - 0.1, GX + 0.1, PARA, lambda u, z, w, h: ((u, GY + 0.0, z), (w, 0.38, h)))
    merlons(-GX - 0.1, GX + 0.1, PARA, lambda u, z, w, h: ((u, -GY - 0.0, z), (w, 0.38, h)))
    for s in (-1, 1): merlons(-GY + 0.4, GY - 0.4, PARA, lambda u, z, w, h, s=s: ((s * (GX + 0.0), u, z), (0.38, w, h)))
    # the hearth crest over the arch: a gold disc with a flame, on a raised stone
    block((0, GY + 0.05, PARA + 0.35), (1.3, 0.5, 0.75), M('stone3'), 0.05)
    K.cyl('crest', 0.5, 0.1, (0, GY + 0.33, PARA + 0.45), M('gold'), 24, 0.02).rotation_euler = (math.pi / 2, 0, 0)
    K.cone('crest_flame', 0.24, 0.62, (0, GY + 0.42, PARA + 0.47), M('fire', 2.5), 10)
    K.cone('crest_core', 0.12, 0.38, (0, GY + 0.47, PARA + 0.42), M('gold', 3.0), 8)
    # banners over the piers and torches beside the arch
    for s in (-1, 1):
        banner(s * 2.95, GY + 0.1, BODY - 0.45, 2.0, 0.92)
        torch(s * 2.08, GY + 0.1, 2.75)
    # feet: rubble and grass where the plinth meets the ground (front and outer corners)
    for s in (-1, 1):
        rubble(s * (GX + 0.2), GY + 0.25, 6, (0.35, 0.3)); tufts(s * (GX + 0.35), GY + 0.4, 4, (0.3, 0.25))
        rubble(s * (AW + 0.5), GY + 0.35, 3, (0.25, 0.15))
    return K.export('gate')

# ---------------------------------------------------------------------------------------------------------------------
# the curtain wall: either side of the gatehouse, off both edges of the view; stones on the front, merlons, a batter
WX0, WX1, WH = -40.0, 30.0, 3.4
def wall():
    for (a, b) in ((WX0, -GX + 0.02), (GX - 0.02, WX1)):
        K.cube('core', (b - a, 1.8, WH), ((a + b) / 2, 0, WH / 2), M('mortar'), 0.0)
        K.cube('batter', (b - a, 2.2, 0.55), ((a + b) / 2, 0.05, 0.27), M('stone_dk'), 0.05)
        masonry(a, b, 0.55, WH, face_y(0.9, 1), lmin=0.7, lmax=1.5)
        K.cube('walk', (b - a, 1.9, 0.22), ((a + b) / 2, 0, WH + 0.11), M('stone2'), 0.03)
        merlons(a + 0.2, b - 0.2, WH + 0.22, lambda u, z, w, h: ((u, 0.72, z), (w, 0.42, h)), w=0.7, gap=0.48, h=0.62)
        merlons(a + 0.2, b - 0.2, WH + 0.22, lambda u, z, w, h: ((u, -0.72, z), (w, 0.32, h * 0.7)), w=0.7, gap=0.48, h=0.62)
        # buttresses and drainage spouts at intervals, rubble and grass along the foot
        x = a + 2.5
        while x < b - 2:
            if all(abs(x - tx) > 2.6 for tx in (-7.5, -13, -18.5, -24)):
                block((x, 1.12, 1.0), (0.7, 0.45, 2.0), M(R.choice(STONES)), 0.05)
                block((x, 1.02, 2.1), (0.62, 0.3, 0.35), M('stone_dk'), 0.05)
            if R.random() < 0.6: rubble(x + R.uniform(-1, 1), 1.35, 2, (0.5, 0.15))
            if R.random() < 0.7: tufts(x + R.uniform(-1.5, 1.5), 1.45, 2, (0.6, 0.15))
            x += R.uniform(4.0, 5.5)
    return K.export('wall')

# ---------------------------------------------------------------------------------------------------------------------
# the element tower: a round tower astride the wall, battered foot, stone courses, arrow slits, corbels, a crenellated
# platform at 6.3 m where the element top stands (Field.TOWER_TOP)
TR, TTOP = 1.7, 6.3
def tower_base():
    K.cyl('foot', TR + 0.35, 1.0, (0, 0, 0.5), M('stone_dk'), 28, 0.05, r2=TR + 0.05)
    K.cyl('core', TR - 0.05, TTOP - 0.9, (0, 0, 0.9 + (TTOP - 0.9) / 2), M('mortar'), 28, 0.0)
    z, row, n = 1.0, 0, 15
    while z < TTOP - 0.75:
        h = 0.5
        for i in range(n):
            a = (i + (0.5 if row % 2 else 0)) / n * math.tau
            L = math.tau * TR / n - 0.06
            o = block((math.cos(a) * (TR + 0.02), math.sin(a) * (TR + 0.02), z + h / 2), (0.12, L, h - 0.05), M(R.choice(STONES)), 0.03)
            o.rotation_euler = (0, 0, a)
        z += h; row += 1
    for k, a in enumerate((math.pi / 2 - 0.6, math.pi / 2 + 0.6, math.pi / 2)):   # arrow slits facing the road
        zc = 2.4 if k < 2 else 4.2
        o = block((math.cos(a) * (TR + 0.08), math.sin(a) * (TR + 0.08), zc), (0.06, 0.16, 0.75), M('pupil'), 0.0); o.rotation_euler = (0, 0, a)
        o = block((math.cos(a) * (TR + 0.1), math.sin(a) * (TR + 0.1), zc - 0.42), (0.1, 0.34, 0.1), M('stone3'), 0.02); o.rotation_euler = (0, 0, a)
    # corbel ring and the crenellated platform
    for i in range(18):
        a = i / 18 * math.tau
        o = block((math.cos(a) * (TR + 0.12), math.sin(a) * (TR + 0.12), TTOP - 0.62), (0.3, 0.26, 0.26), M('stone_dk'), 0.03); o.rotation_euler = (0, 0, a)
    K.cyl('ring', TR + 0.32, 0.5, (0, 0, TTOP - 0.25), M('stone'), 32, 0.04)
    K.cyl('floor', TR + 0.1, 0.12, (0, 0, TTOP + 0.0), M('stone2'), 28, 0.0)
    for i in range(10):
        a = (i + 0.5) / 10 * math.tau
        o = block((math.cos(a) * (TR + 0.12), math.sin(a) * (TR + 0.12), TTOP + 0.3), (0.32, 0.62, 0.6), M(R.choice(STONES)), 0.04); o.rotation_euler = (0, 0, a)
    # a pennant on the back rim, and rubble and grass at the foot on the road side
    K.cyl('pole', 0.04, 2.0, (0.9, -1.25, TTOP + 1.0), M('wood'), 6, 0.0)
    flag = block((0.9 + 0.35, -1.25, TTOP + 1.75), (0.7, 0.03, 0.4), M('banner'), 0.0)
    rubble(0, TR + 0.45, 4, (1.0, 0.2)); tufts(0, TR + 0.55, 4, (1.1, 0.2))
    return K.export('tower_base')

# ---------------------------------------------------------------------------------------------------------------------
# element tops: each says its element from across the screen. They sit on the platform (origin at its floor).
def top(kind):
    if kind == 'stone':   # no element yet: a slate cone roof on a short drum, a pennant
        K.cyl('drum', 1.1, 0.7, (0, 0, 0.35), M('stone2'), 20, 0.04)
        K.cyl('roof', 1.55, 1.9, (0, 0, 1.65), M('roof_slate'), 20, 0.0, r2=0.06)
        K.cyl('eave', 1.6, 0.12, (0, 0, 0.72), M('wood_dk'), 20, 0.02)
        K.cyl('spire', 0.05, 0.9, (0, 0, 2.95), M('gold'), 6, 0.0)
        block((0.3, 0, 3.15), (0.55, 0.03, 0.32), M('banner'), 0.0)
    elif kind == 'fire':   # an iron brazier on three legs, coals, a three-tongue flame and sparks
        for i in range(3):
            a = i / 3 * math.tau
            o = block((math.cos(a) * 0.6, math.sin(a) * 0.6, 0.45), (0.12, 0.12, 0.9), M('iron'), 0.02); o.rotation_euler = (math.sin(a) * 0.25, -math.cos(a) * 0.25, 0)
        K.cyl('bowl', 1.0, 0.55, (0, 0, 1.05), M('iron'), 20, 0.04, r2=0.62)
        K.cyl('rim', 1.05, 0.1, (0, 0, 1.33), M('gold'), 20, 0.02)
        for i in range(9):
            a = i / 9 * math.tau; r = 0.35 + 0.3 * (i % 2)
            K.sphere('coal', R.uniform(0.18, 0.26), (math.cos(a) * r, math.sin(a) * r, 1.35), M('fire', 3.0), (1, 1, 0.7), 2)
        K.cone('flame', 0.75, 2.0, (0, 0, 2.3), M('fire', 3.5), 12)
        for i in range(3):
            a = i / 3 * math.tau + 0.5
            K.cone('tongue', 0.35, 1.3, (math.cos(a) * 0.35, math.sin(a) * 0.35, 2.0), M('fire', 3.5), 10)
        K.cone('core', 0.4, 1.3, (0, 0, 1.95), M('gold', 5.0), 10)
        for i in range(5): K.sphere('spark', 0.07, (R.uniform(-0.6, 0.6), R.uniform(-0.6, 0.6), R.uniform(2.6, 3.4)), M('gold', 5.0), sub=1)
    elif kind == 'frost':   # a cluster of glowing ice crystals in a stone ring, rime round the base
        K.cyl('ring', 1.05, 0.35, (0, 0, 0.18), M('stone3'), 20, 0.04)
        spec = [(0.38, 2.4, 0, 0, 0), (0.26, 1.6, 0.55, 0.4, 0.25), (0.24, 1.4, -0.5, 0.35, -0.3), (0.22, 1.2, 0.1, -0.55, 0.2), (0.2, 1.0, -0.45, -0.4, -0.25), (0.18, 0.9, 0.6, -0.3, 0.3)]
        for (r, h, x, y, tilt) in spec:
            o = K.cyl('crystal', r, h, (x, y, 0.3 + h / 2), M('frost', 1.6), 6, 0.0)
            tip = K.cone('tip', r, r * 2.0, (x, y, 0.3 + h + r), M('frost', 1.6), 6)
            for q in (o, tip):
                q.rotation_euler = (tilt * (y / (abs(y) + 1e-3)), -tilt * (x / (abs(x) + 1e-3)), R.uniform(0, 1))
        for i in range(10):
            a = i / 10 * math.tau
            K.sphere('rime', 0.14, (math.cos(a) * 0.9, math.sin(a) * 0.9, 0.38), M('snow'), (1, 1, 0.6), 1)
    elif kind == 'storm':   # a bronze bell in a frame under a lightning rod with coils and a crackling orb
        for s in (-1, 1):
            block((s * 0.75, 0, 1.2), (0.2, 0.2, 2.4), M('wood_dk'), 0.03)
            block((s * 0.75, 0, 0.12), (0.5, 0.5, 0.24), M('stone3'), 0.03)
        block((0, 0, 2.45), (1.9, 0.26, 0.26), M('wood_dk'), 0.03)
        K.cyl('bell', 0.55, 0.9, (0, 0, 1.75), M('gold'), 20, 0.03, r2=0.28)
        K.cyl('lip', 0.6, 0.1, (0, 0, 1.33), M('gold'), 20, 0.02)
        K.sphere('clapper', 0.12, (0, 0, 1.25), M('iron'), sub=2)
        K.cyl('rod', 0.06, 1.6, (0, 0, 3.35), M('gold'), 8, 0.0)
        for k in range(3):
            bpy.ops.mesh.primitive_torus_add(major_radius=0.22 - k * 0.04, minor_radius=0.04, location=(0, 0, 2.8 + k * 0.35))
            K.finish(bpy.context.object, M('gold'), 0.0)
        K.sphere('orb', 0.32, (0, 0, 4.35), M('storm', 3.5), sub=3)
        K.sphere('halo', 0.45, (0, 0, 4.35), M('storm', 1.2), (1, 1, 1), 1)
    return K.export('top_' + kind)

# ---------------------------------------------------------------------------------------------------------------------
# the slime: a glossy teardrop blob, big eyes with catchlights, a smile, a painted highlight, drips at its foot.
# 1.5 m tall, faces -y (Field.WALK turns it along the road, three-quarters to the camera).
def slime():
    bpy.ops.mesh.primitive_uv_sphere_add(segments=40, ring_count=24, radius=0.82, location=(0, 0, 0.7))
    o = bpy.context.object
    for v in o.data.vertices:
        x, y, z = v.co.x, v.co.y, v.co.z
        if z > 0: z *= 1.0 + 0.28 * (z / 0.82) ** 3                  # a soft peak on top
        else: z *= 0.62; x *= 1.08 + 0.1 * (-z / 0.5); y *= 1.08 + 0.1 * (-z / 0.5)   # a heavy, spreading base
        v.co = Vector((x, y, z))
    o.data.update(); K.finish(o, M('slime_gloss'))
    o.modifiers.new('sub', 'SUBSURF').levels = 1
    K.sphere('belly', 0.6, (0, -0.25, 0.52), M('slime_dk'), (1.05, 0.7, 0.75), 3)          # a darker core showing through
    K.sphere('shine', 0.22, (-0.28, -0.42, 1.28), M('white_gloss', 0.4), (1.2, 0.6, 0.8), 2)   # painted highlight
    K.sphere('shine2', 0.08, (-0.05, -0.58, 1.38), M('white_gloss', 0.4), sub=1)
    for s in (-1, 1):
        K.sphere('eye', 0.2, (s * 0.27, -0.66, 0.98), M('eye'), (1, 0.75, 1.15), 3)
        K.sphere('pupil', 0.12, (s * 0.25, -0.79, 0.95), M('pupil'), (1, 0.6, 1.2), 2)
        K.sphere('catch', 0.04, (s * 0.21, -0.86, 1.02), M('white_gloss', 0.6), sub=1)
        K.sphere('cheek', 0.1, (s * 0.5, -0.6, 0.72), M('blush'), (1, 0.4, 0.6), 2)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.13, minor_radius=0.028, location=(0, -0.79, 0.72), rotation=(math.pi / 2, 0, 0))
    sm = bpy.context.object
    bm = bmesh.new(); bm.from_mesh(sm.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if v.co.z > -0.02], context='VERTS')   # keep the lower half: a smile
    bm.to_mesh(sm.data); bm.free(); K.finish(sm, M('pupil'))
    for (x, y, s) in ((0.62, 0.3, 0.2), (-0.58, 0.35, 0.16), (0.2, -0.75, 0.13), (-0.4, -0.55, 0.11)):
        K.sphere('drip', s, (x, y, s * 0.4), M('slime_gloss'), (1.2, 1.2, 0.55), 2)
    return K.export('slime')

# ---------------------------------------------------------------------------------------------------------------------
# the road: a packed-earth way along the front of the wall, flagstones on the forecourt before the gate, kerbs, cobbles
ROADY, RHALF = 5.4, 3.0
def road():
    K.cube('way', (60, 2 * RHALF, 0.06), (-28 + 3, ROADY, 0.03), M('dirt'), 0.0)
    for i in range(260):   # cobbles pressed into the way, denser near its middle
        x = R.uniform(-55, 2); y = ROADY + R.gauss(0, 1.2)
        if abs(y - ROADY) > RHALF - 0.25: continue
        s = R.uniform(0.25, 0.5)
        o = block((x, y, 0.07), (s * 1.2, s, 0.08), M(R.choice(('cobble', 'cobble2'))), 0.03); o.rotation_euler = (0, 0, R.uniform(0, 3.1))
    # forecourt: flagstones in rows in front of the gatehouse
    y = GY + 0.15
    while y < ROADY + RHALF - 0.1:
        x = -GX - 1.3 + R.uniform(0, 0.4)
        while x < GX + 1.5:
            L = R.uniform(0.6, 1.1)
            block((x + L / 2, y + 0.3, 0.08), (L - 0.06, 0.56, 0.09), M(R.choice(('cobble', 'cobble2', 'stone2'))), 0.025)
            x += L
        y += 0.62
    for x in [-55 + i * 1.6 for i in range(36)]:   # kerb along the road's outer edge
        if x > -GX - 1.3: break
        block((x, ROADY + RHALF + 0.12, 0.1), (1.5, 0.28, 0.2), M('stone_dk'), 0.04)
    # a low split-rail fence along the meadow side: reads as a road, never hides an enemy (0.7 m)
    x = -52
    while x < -GX - 3:
        K.cyl('post', 0.07, 0.75, (x, ROADY + RHALF + 0.6, 0.37), M('wood_dk'), 6, 0.0)
        if x + 2.4 < -GX - 3:
            for zz in (0.32, 0.62):
                o = K.cyl('rail', 0.045, 2.45, (x + 1.2, ROADY + RHALF + 0.6, zz), M('wood'), 6, 0.0); o.rotation_euler = (0, math.pi / 2, R.uniform(-0.04, 0.04))
        x += 2.4
    return K.export('road')

# ---------------------------------------------------------------------------------------------------------------------
# the ground: meadow in front of the wall, the city's ground behind it, hills closing the view, edges dressed
def ground():
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=60, y_subdivisions=50, size=1, location=(0, 0, 0))
    g = bpy.context.object; g.scale = (120, 100, 1); bpy.ops.object.transform_apply(scale=True)
    g.location = (-10, -10, 0); bpy.ops.object.transform_apply(location=True)
    for v in g.data.vertices:
        x, y = v.co.x, v.co.y
        h = 0.0
        if y < -22: h += (min(-22 - y, 18) / 18) ** 1.6 * 9.0 + 0.6 * math.sin(x * 0.21) * math.cos(y * 0.17)   # hills behind the city
        if y > 12: h += 0.25 * math.sin(x * 0.3) * math.cos(y * 0.35)                                                   # rolling meadow
        if x > 22 or x < -42: h += min(abs(x) - (22 if x > 0 else 42), 10) * 0.25                                     # rising sides
        v.co.z = h - 0.02
    K.finish(g, M('grass'))
    # meadow dressing in front of the wall: clover patches, flowers, rocks, bushes; trees only well clear of the road
    for _ in range(90):
        x, y = R.uniform(-48, 22), R.uniform(9.6, 30)
        K.cyl('patch', R.uniform(0.8, 2.2), 0.03, (x, y, 0.02), M(R.choice(('grass2', 'grass3'))), 10, 0.0).scale = (1, R.uniform(0.5, 1), 1)
    for _ in range(140):
        x, y = R.uniform(-48, 22), R.uniform(9.4, 28)
        K.sphere('flower', 0.06, (x, y, 0.12), M(R.choice(('flower_y', 'flower_w', 'flower_p'))), sub=1)
    for _ in range(18):
        x, y = R.uniform(-46, 20), R.uniform(10.5, 26)
        s = R.uniform(0.3, 0.7)
        o = K.sphere('rock', s, (x, y, s * 0.25), M(R.choice(('stone_dk', 'stone2'))), (1.3, 1.0, 0.65), 1); o.rotation_euler = (0, 0, R.uniform(0, 3))
    for _ in range(26):
        x, y = R.uniform(-46, 20), R.uniform(11, 28)
        for k in range(3): K.sphere('bush', R.uniform(0.45, 0.8), (x + R.uniform(-0.6, 0.6), y + R.uniform(-0.4, 0.4), 0.4), M(R.choice(('grass2', 'bush'))), (1, 1, 0.8), 2)
    for (x, y) in [(R.uniform(-46, 20), R.uniform(19, 30)) for _ in range(7)] + [(R.uniform(20, 30), R.uniform(-30, 10)) for _ in range(6)] + [(R.uniform(-48, -38), R.uniform(-30, 10)) for _ in range(6)]:
        tree(x, y, R.uniform(0.9, 1.3))
    for (x, y) in [(R.uniform(-40, 25), R.uniform(-30, -24)) for _ in range(16)]:   # a wooded ridge behind the city
        tree(x, y, R.uniform(1.0, 1.5), z=(min(-22 - y, 18) / 18) ** 1.6 * 9.0)
    # the city's ground: a cobbled plaza round the fountain and lanes from the gate and to the plots
    K.cyl('plaza', 4.6, 0.05, (0, -10, 0.025), M('cobble'), 32, 0.0)
    K.cyl('plaza_rim', 4.8, 0.04, (0, -10, 0.015), M('stone_dk'), 32, 0.0)
    block((0, -6.0, 0.02), (3.0, 8.2, 0.05), M('cobble2'), 0.0)
    for (px, py) in ((7, -6), (13, -6), (17.5, -10), (-18.5, -10.5), (-12.5, -10.5), (-6.5, -10.5), (10, -11), (5, -14), (-9.5, -15.5), (-3.5, -15.5)):
        d = Vector((px - 0, py + 10, 0)); L = d.length
        o = block(((px) / 2, (py - 10) / 2, 0.015), (1.4, L, 0.04), M('dirt'), 0.0); o.rotation_euler = (0, 0, math.atan2(d.x, d.y) * -1)
        K.cyl('yard', 2.8, 0.04, (px, py, 0.02), M('dirt'), 20, 0.0)
    return K.export('ground')

def tree(x, y, s, z=0.0):
    K.cyl('trunk', 0.22 * s, 1.6 * s, (x, y, z + 0.8 * s), M('wood_dk'), 8, 0.0, r2=0.15 * s)
    for k, (dz, r) in enumerate(((1.9, 1.2), (2.7, 0.95), (3.35, 0.65))):
        K.sphere('crown', r * s, (x + R.uniform(-0.15, 0.15), y + R.uniform(-0.15, 0.15), z + dz * s), M(R.choice(('tree', 'tree2'))), (1, 1, 0.85), 2)

# ---------------------------------------------------------------------------------------------------------------------
# the fountain: an octagonal basin with a stone rim, water, a two-tier column and a gold hearth flame on top
def fountain():
    K.cyl('step', 2.5, 0.25, (0, 0, 0.12), M('stone_dk'), 8, 0.04)
    K.cyl('basin', 2.15, 0.6, (0, 0, 0.55), M('stone'), 8, 0.05)
    K.cyl('rim', 2.25, 0.14, (0, 0, 0.88), M('stone3'), 8, 0.03)
    K.cyl('water', 1.95, 0.06, (0, 0, 0.8), M('water_gloss', 0.3), 32, 0.0)
    K.cyl('column', 0.32, 1.5, (0, 0, 1.55), M('stone2'), 12, 0.03)
    K.cyl('bowl', 0.95, 0.32, (0, 0, 2.25), M('stone'), 16, 0.03, r2=0.5)
    K.cyl('bowl_water', 0.82, 0.05, (0, 0, 2.4), M('water_gloss', 0.3), 16, 0.0)
    K.cyl('neck', 0.16, 0.6, (0, 0, 2.7), M('stone2'), 10, 0.02)
    K.cyl('cup', 0.3, 0.2, (0, 0, 3.05), M('gold'), 12, 0.02, r2=0.18)
    K.cone('flame', 0.2, 0.6, (0, 0, 3.45), M('fire', 3.0), 10)
    for i in range(8):   # water falling from the upper bowl
        a = i / 8 * math.tau
        K.cyl('fall', 0.05, 1.0, (math.cos(a) * 0.9, math.sin(a) * 0.9, 1.85), M('water_gloss', 0.5), 6, 0.0)
    return K.export('fountain')
