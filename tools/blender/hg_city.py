# Hearthgate 3D, the city at human scale (owner, Nov 13 2026: "we have to work on the proportions here. Wizard is as big
# as the town, some doors are insanely bigger than others. I want the wizard to be able to walk into the buildings
# inside. if we have to make the city bigger then so be it").
#
# One scale for every building (hg_layout.py): a storey is 3.2 m, every door is 1.2 x 2.4 m, every window 0.8 x 1.1 m
# with its sill 1.0 m above the floor, walls 0.3 m thick on a 0.3 m plinth; the wizard is 1.8 m to his hat's tip.
# Each building is a real ground-floor room the wizard walks into through an open doorway on its front (-y; the game turns
# fronts to the walls), furnished for its trade. Everything above the ground storey (the ceiling, the upper floor, the
# roof, spires and belfries) is tagged 'UP_' and exported as a child object named 'upper', which the game hides while
# the wizard is inside, so the room shows from above. Footprints (hg_layout.SIZES) are what the game collides against.
# Imported by hg_kit.py.
import bpy, math, random
import hg_kit as K
import hg_front as F
import hg_layout as LY

R = random.Random(29)
M, block, mesh_from, tufts, rubble = F.M, F.block, F.mesh_from, F.tufts, F.rubble
T, ST, PL = LY.WALL_T, LY.STOREY, LY.PLINTH
DW, DH = LY.DOOR_W, LY.DOOR_H
WW, WH, SILL = 0.8, 1.1, 1.0
FLOOR = PL + 0.04
z0 = FLOOR

class Up:   # everything made inside `with Up():` goes to the 'upper' part the game hides when the wizard is inside
    def __enter__(self): self.before = set(bpy.data.objects); self.nb = len(K.BLOCKS); return self
    def __exit__(self, *a):
        for o in set(bpy.data.objects) - self.before: o.name = 'UP_' + o.name
        for b in K.BLOCKS[self.nb:]: b.up = True   # batched boxes (hg_kit.Blk)

# ------------------------------------------------------------------------------------------------- walls and openings
def wall_x(x0, x1, y, zb, h, m, holes=()):
    """A wall along x from x0 to x1 at y, from zb up h, with holes (u, width, bottom, top) cut as pieces round them."""
    def piece(a, b, za, zt):
        if b - a > 0.02 and zt - za > 0.02: block(((a + b) / 2, y, (za + zt) / 2), (b - a, T, zt - za), M(m), 0.0)
    x = x0
    for (u, wd, hb, ht) in sorted(holes):
        a, b = u - wd / 2, u + wd / 2
        piece(x, a, zb, zb + h); piece(a, b, zb, zb + hb); piece(a, b, zb + ht, zb + h); x = b
    piece(x, x1, zb, zb + h)
def wall_y(y0, y1, x, zb, h, m, holes=()):
    def piece(a, b, za, zt):
        if b - a > 0.02 and zt - za > 0.02: block((x, (a + b) / 2, (za + zt) / 2), (T, b - a, zt - za), M(m), 0.0)
    y = y0
    for (u, wd, hb, ht) in sorted(holes):
        a, b = u - wd / 2, u + wd / 2
        piece(y, a, zb, zb + h); piece(a, b, zb, zb + hb); piece(a, b, zb + ht, zb + h); y = b
    piece(y, y1, zb, zb + h)

def pane_x(x, y, z, out):   # a lit window in a wall along x: a glowing pane in the hole, mullions, sill, lintel, shutters
    block((x, y, z + WH / 2), (WW - 0.02, 0.06, WH - 0.02), M('window', 1.6), 0.0)
    block((x, y, z + WH / 2), (0.06, 0.1, WH), M('wood_dk'), 0.0); block((x, y, z + WH * 0.6), (WW, 0.1, 0.06), M('wood_dk'), 0.0)
    yo = y + out * (T / 2 + 0.04)
    block((x, yo, z - 0.06), (WW + 0.3, 0.14, 0.1), M('stone3'), 0.01)
    block((x, yo, z + WH + 0.08), (WW + 0.3, 0.1, 0.16), M('timber'), 0.0)
    c = R.choice(('roof_slate', 'wood', 'awning'))
    for s in (-1, 1): block((x + s * (WW / 2 + 0.24), yo, z + WH / 2), (0.4, 0.05, WH), M(c), 0.01)
def pane_y(x, y, z, out):
    block((x, y, z + WH / 2), (0.06, WW - 0.02, WH - 0.02), M('window', 1.6), 0.0)
    block((x, y, z + WH / 2), (0.1, 0.06, WH), M('wood_dk'), 0.0); block((x, y, z + WH * 0.6), (0.1, WW, 0.06), M('wood_dk'), 0.0)
    xo = x + out * (T / 2 + 0.04)
    block((xo, y, z - 0.06), (0.14, WW + 0.3, 0.1), M('stone3'), 0.01)
    block((xo, y, z + WH + 0.08), (0.1, WW + 0.3, 0.16), M('timber'), 0.0)

def doorway(x, y):
    """The one door size (1.2 x 2.4 m): a stone frame, a step, and the plank door standing open inside against the wall."""
    yo = y - T / 2 - 0.05
    for s in (-1, 1): block((x + s * (DW / 2 + 0.12), yo, FLOOR + DH / 2), (0.24, 0.12, DH), M('stone3'), 0.02)
    block((x, yo, FLOOR + DH + 0.14), (DW + 0.6, 0.14, 0.28), M('stone3'), 0.02)
    block((x, y - T / 2 - 0.35, PL / 2), (DW + 0.5, 0.6, PL), M('stone_dk'), 0.02)   # the step up to the floor
    block((x - DW / 2 + 0.05, y + T / 2 + DW / 2, FLOOR + DH / 2 - 0.02), (0.06, DW - 0.04, DH - 0.06), M('wood'), 0.01)   # open, swung in
    for k in (0.25, 0.75): block((x - DW / 2 + 0.02, y + T / 2 + DW / 2, FLOOR + DH * k), (0.02, DW - 0.1, 0.07), M('iron'), 0.0)

def room(w, d, h=ST, wall='stone2', floor='wood', door=True, front_win=True, side_win=True, back_win=True):
    """The ground storey: plinth, plank floor, four walls with the doorway centred on the front, windows round it."""
    hx, hy = w / 2, d / 2
    block((0, 0, PL / 2), (w + 0.4, d + 0.4, PL), M('stone_dk'), 0.03)
    block((0, 0, PL + 0.02), (w - 2 * T, d - 2 * T, 0.04), M(floor), 0.0)
    if floor == 'wood':
        for k in range(int((w - 2 * T) / 0.5)):   # floor boards
            block((-hx + T + 0.25 + k * 0.5, 0, PL + 0.045), (0.02, d - 2 * T, 0.01), M('wood_dk'), 0.0)
    wx = hx - 1.6 if w >= 6.5 else None
    fh = [(0.0, DW, 0.0, DH)] if door else []
    if front_win and wx: fh += [(-wx, WW, SILL, SILL + WH), (wx, WW, SILL, SILL + WH)]
    wall_x(-hx, hx, -hy + T / 2, FLOOR, h, wall, fh)
    bh = ([(-wx, WW, SILL, SILL + WH), (wx, WW, SILL, SILL + WH)] if wx else [(0.0, WW, SILL, SILL + WH)]) if back_win else []
    wall_x(-hx, hx, hy - T / 2, FLOOR, h, wall, bh)
    sh = [(0.0, WW, SILL, SILL + WH)] if side_win else []
    for s in (-1, 1): wall_y(-hy + T, hy - T, s * (hx - T / 2), FLOOR, h, wall, sh)
    if door: doorway(0.0, -hy + T / 2)
    if front_win and wx:
        for s in (-1, 1): pane_x(s * wx, -hy + T / 2, FLOOR + SILL, -1)
    for (u, _, _, _) in bh: pane_x(u, hy - T / 2, FLOOR + SILL, 1)
    if side_win:
        for s in (-1, 1): pane_y(s * (hx - T / 2), 0.0, FLOOR + SILL, s)
    for (sx, sy) in ((-1, -1), (1, -1), (-1, 1), (1, 1)):   # quoins
        for k in range(int(h / 0.6)):
            block((sx * (hx - 0.05), sy * (hy - 0.05), FLOOR + 0.3 + k * 0.6), (0.42 if k % 2 else 0.3, 0.3 if k % 2 else 0.42, 0.5), M('stone3'), 0.02)
    return FLOOR + h

def top_rail(w, d, z, m='timber'):   # the wall plate round the top of the ground storey: the cutaway's clean edge
    for y in (-d / 2 + T / 2, d / 2 - T / 2): block((0, y, z + 0.08), (w, T + 0.06, 0.16), M(m), 0.0)
    for s in (-1, 1): block((s * (w / 2 - T / 2), 0, z + 0.08), (T + 0.06, d, 0.16), M(m), 0.0)

def ceiling(w, d, z):   # the floor of the storey above (part of the upper)
    block((0, 0, z + 0.26), (w - 0.02, d - 0.02, 0.2), M('timber'), 0.0)

def upper_floor(w, d, z, h=3.0, plaster='plaster'):
    """A jettied, timber-framed plaster storey with windows and flower boxes on its long faces, a window on each end."""
    hx, hy = w / 2 + 0.25, d / 2 + 0.25
    K.cube('upper', (2 * hx, 2 * hy, h), (0, 0, z + h / 2), M(plaster), 0.03)
    block((0, -hy - 0.02, z + 0.08), (2 * hx + 0.1, 0.18, 0.16), M('timber'), 0.0)
    for (y, sg) in ((-hy, -1), (hy, 1)):
        for x in [-hx + 0.08, hx - 0.08] + [-hx + 2 * hx * k / 4 for k in (1, 2, 3)]:
            block((x, y + sg * 0.03, z + h / 2), (0.16, 0.06, h), M('timber'), 0.0)
        for zz in (z + 0.08, z + h - 0.08): block((0, y + sg * 0.03, zz), (2 * hx, 0.06, 0.16), M('timber'), 0.0)
        for x in (-hx * 0.5, hx * 0.5):
            block((x, y + sg * 0.04, z + 1.0 + WH / 2), (WW, 0.04, WH), M('window', 1.6), 0.0)
            block((x, y + sg * 0.07, z + 1.0 + WH / 2), (0.06, 0.03, WH), M('wood_dk'), 0.0)
            K.cube('flowers', (WW, 0.22, 0.2), (x, y + sg * 0.16, z + 0.86), M('wood'), 0.02)
            for k in range(4): K.sphere('bloom', 0.09, (x - 0.3 + k * 0.2, y + sg * 0.2, z + 1.0), M(R.choice(('flower_p', 'flower_y', 'blush'))), sub=1)
    for s in (-1, 1): block((s * hx, 0, z + 1.0 + WH / 2), (0.04, WW, WH), M('window', 1.6), 0.0)
    return z + h, hx, hy

def gable_roof(zb, hx, hy, rise, m, ridge_x=True, overhang=0.45, gable_m='plaster'):
    a, b = hx + overhang, hy + overhang
    if ridge_x:
        vs = [(-a, -b, zb), (a, -b, zb), (a, b, zb), (-a, b, zb), (-a, 0, zb + rise), (a, 0, zb + rise)]
        fs = [(0, 1, 5, 4), (2, 3, 4, 5), (1, 2, 5), (3, 0, 4)]
    else:
        vs = [(-a, -b, zb), (a, -b, zb), (a, b, zb), (-a, b, zb), (0, -b, zb + rise), (0, b, zb + rise)]
        fs = [(1, 2, 5, 4), (3, 0, 4, 5), (0, 1, 4), (2, 3, 5)]
    o = mesh_from('roof', vs, fs, M(m), 0.0)
    sol = o.modifiers.new('thick', 'SOLIDIFY'); sol.thickness = 0.2; sol.offset = 1.0
    edge = 'roof_red' if 'terra' in m else 'stone_dk'
    if ridge_x:
        block((0, 0, zb + rise + 0.08), (2 * a + 0.1, 0.28, 0.2), M(edge), 0.02)
        for sg in (-1, 1):
            for k in range(1, 6):
                t = k / 6; o2 = block((0, sg * (b * (1 - t) + 0.08), zb + rise * t + 0.12), (2 * a, 0.06, 0.08), M('wood_dk' if 'slate' in m else 'roof_red'), 0.0)
                o2.rotation_euler = (-sg * math.atan2(rise, b), 0, 0)
    else:
        block((0, 0, zb + rise + 0.08), (0.28, 2 * b + 0.1, 0.2), M(edge), 0.02)
        for sg in (-1, 1): mesh_from('gable', [(-hx, sg * hy, zb), (hx, sg * hy, zb), (0, sg * hy, zb + rise * hx / a)], [(0, 1, 2) if sg < 0 else (2, 1, 0)], M(gable_m), 0.0)
    return zb + rise

def chimney(x, y, zb, ztop, glow=False):
    block((x, y, (zb + ztop) / 2), (0.8, 0.8, ztop - zb), M('stone2'), 0.03)
    block((x, y, ztop + 0.08), (1.0, 1.0, 0.18), M('stone_dk'), 0.02)
    if glow: K.sphere('ember', 0.28, (x, y, ztop + 0.3), M('fire', 3.0), sub=2)

def sign(x, y, z, icon_mat):   # a hanging sign on an iron bracket off the front wall, with a glowing icon
    block((x, y - 0.5, z + 0.5), (0.07, 1.0, 0.07), M('iron'), 0.0)
    block((x, y - 0.85, z), (0.07, 0.7, 0.55), M('wood'), 0.01)
    K.sphere('icon', 0.16, (x - 0.06, y - 0.85, z), icon_mat, (0.5, 1, 1), 2)
def lantern(x, y, z):
    block((x, y - 0.25, z + 0.3), (0.05, 0.5, 0.05), M('iron'), 0.0)
    K.cyl('lamp', 0.12, 0.3, (x, y - 0.45, z), M('window', 2.0), 6, 0.0); K.cone('lamp_top', 0.16, 0.14, (x, y - 0.45, z + 0.22), M('iron'), 6)

# ------------------------------------------------------------------------------------------------- furniture (on the floor)
def barrel(x, y, s=1.0):
    K.cyl('barrel', 0.32 * s, 0.8 * s, (x, y, z0 + 0.4 * s), M('wood'), 10, 0.02)
    for z in (0.15, 0.65): K.cyl('hoop', 0.33 * s, 0.05, (x, y, z0 + z * s), M('iron'), 10, 0.0)
def crate(x, y, s=1.0):
    o = block((x, y, z0 + 0.3 * s), (0.6 * s, 0.6 * s, 0.6 * s), M('wood'), 0.03); o.rotation_euler = (0, 0, R.uniform(-0.4, 0.4))
def table(x, y, w=1.6, d=0.9, goods=()):
    block((x, y, z0 + 0.76), (w, d, 0.08), M('wood'), 0.02)
    for (a, b) in ((-1, -1), (1, -1), (-1, 1), (1, 1)): block((x + a * (w / 2 - 0.1), y + b * (d / 2 - 0.1), z0 + 0.37), (0.08, 0.08, 0.74), M('wood_dk'), 0.0)
    for k, g in enumerate(goods): K.sphere('goods', 0.12, (x - w / 3 + k * (w / 3), y, z0 + 0.88), M(g), (1, 1, 0.7), 1)
def stool(x, y):
    K.cyl('stool', 0.2, 0.06, (x, y, z0 + 0.46), M('wood'), 8, 0.0)
    for k in range(3): block((x + math.cos(k * 2.1) * 0.12, y + math.sin(k * 2.1) * 0.12, z0 + 0.22), (0.05, 0.05, 0.44), M('wood_dk'), 0.0)
def shelf(x, y, w, along_x=True, goods=('wood', 'roof_red', 'roof_blue', 'flower_y', 'grass3')):   # tall shelves against a wall
    for z in (0.4, 1.0, 1.6, 2.2): block((x, y, z0 + z), (w, 0.4, 0.05) if along_x else (0.4, w, 0.05), M('wood'), 0.0)
    for s in (-1, 1): block((x + s * w / 2, y, z0 + 1.2) if along_x else (x, y + s * w / 2, z0 + 1.2), (0.06, 0.4, 2.4) if along_x else (0.4, 0.06, 2.4), M('wood_dk'), 0.0)
    for z in (0.4, 1.0, 1.6, 2.2):
        for k in range(int(w / 0.18)):
            if R.random() < 0.8:
                u = -w / 2 + 0.1 + k * 0.18; hh = R.uniform(0.25, 0.42)
                block((x + u, y, z0 + z + hh / 2 + 0.03) if along_x else (x, y + u, z0 + z + hh / 2 + 0.03), (0.12, 0.28, hh) if along_x else (0.28, 0.12, hh), M(R.choice(goods)), 0.0)
def hearth(x, y, along_x=True, big=False):   # a stone fireplace against a wall, with its fire
    w = 2.0 if big else 1.5
    block((x, y, z0 + 0.6), (w, 0.7, 1.2) if along_x else (0.7, w, 1.2), M('stone3'), 0.03)
    block((x, y, z0 + 1.3), (w + 0.3, 0.9, 0.2) if along_x else (0.9, w + 0.3, 0.2), M('stone_dk'), 0.02)
    K.cone('flame', 0.3 if big else 0.22, 0.7, (x, y, z0 + 0.55), M('fire', 3.0), 8)
    K.cone('flame_in', 0.15, 0.5, (x, y, z0 + 0.45), M('fire_core', 3.0), 6)
def bed(x, y):
    block((x, y, z0 + 0.3), (1.0, 2.0, 0.4), M('wood'), 0.02)
    block((x, y, z0 + 0.55), (0.9, 1.9, 0.16), M(R.choice(('cloth', 'awning', 'roof_blue'))), 0.04)
    K.sphere('pillow', 0.22, (x, y + 0.7, z0 + 0.7), M('plaster'), (1.4, 0.8, 0.5), 1)
def sacks(x, y, n=4):
    for k in range(n): K.sphere('sack', 0.3, (x + (k % 2) * 0.55, y + (k // 2 % 2) * 0.5, z0 + 0.28 + (0.4 if k > 3 else 0)), M('canvas'), (1, 0.85, 1.05), 1)

def rug(x, y, w, d, m='awning'):   # a woven rug with a border
    block((x, y, z0 + 0.012), (w, d, 0.02), M('wood_dk'), 0.0)
    block((x, y, z0 + 0.022), (w - 0.2, d - 0.2, 0.02), M(m), 0.0)
    block((x, y, z0 + 0.03), (w - 0.6, d - 0.6, 0.02), M('hay'), 0.0)
def sconce(x, y, z, face):   # a candle on an iron bracket on an inner wall; face = direction into the room (dx, dy)
    fx, fy = face
    block((x - fx * 0.02, y - fy * 0.02, z), (0.12 if fx == 0 else 0.04, 0.04 if fx == 0 else 0.12, 0.2), M('iron'), 0.0)
    K.cyl('candle', 0.04, 0.16, (x + fx * 0.12, y + fy * 0.12, z + 0.12), M('plaster'), 6, 0.0)
    K.cone('wick', 0.035, 0.1, (x + fx * 0.12, y + fy * 0.12, z + 0.25), M('fire_core', 3.0), 6)
def hanging(x, y, z, w, m='banner', face='x'):   # a cloth hanging on an inner wall, with a gold rod
    block((x, y, z), (w, 0.03, 1.4) if face == 'x' else (0.03, w, 1.4), M(m), 0.0)
    block((x, y, z + 0.72), (w + 0.2, 0.05, 0.05) if face == 'x' else (0.05, w + 0.2, 0.05), M('gold'), 0.0)
def pots(x, y, n=3):
    for k in range(n): K.cyl('pot', 0.16 + 0.04 * (k % 2), 0.3 + 0.08 * (k % 3), (x + k * 0.36, y, z0 + 0.18), M(R.choice(('roof_terra', 'stone3', 'roof_blue'))), 10, 0.02, r2=0.1)
def inner_walls(w, d, h=ST):   # a plaster skin and a timber dado on the inside faces, so a room reads warm, not like the outside
    a0, a1 = -w / 2 + T, w / 2 - T
    for (y, sg) in ((-d / 2 + T + 0.01, 1), (d / 2 - T - 0.01, -1)):
        runs = [(a0, -DW / 2), (DW / 2, a1)] if sg > 0 else [(a0, a1)]   # the front wall leaves the doorway clear
        for (u0, u1) in runs:
            block(((u0 + u1) / 2, y, z0 + 0.45), (u1 - u0, 0.02, 0.9), M('wood'), 0.0); block(((u0 + u1) / 2, y + sg * 0.012, z0 + 0.92), (u1 - u0, 0.04, 0.06), M('wood_dk'), 0.0)
    for (x, sg) in ((-w / 2 + T + 0.01, 1), (w / 2 - T - 0.01, -1)):
        block((x, 0, z0 + 0.45), (0.02, d - 2 * T, 0.9), M('wood'), 0.0); block((x + sg * 0.012, 0, z0 + 0.92), (0.04, d - 2 * T, 0.06), M('wood_dk'), 0.0)

# ------------------------------------------------------------------------------------------------- the ten buildings
def b_forge():
    w, d = LY.SIZES['forge']; top = room(w, d, 3.4, 'stone2', 'cobble2'); top_rail(w, d, top)
    hearth(-0.6, d / 2 - T - 0.36, big=True)                                    # the forge fire against the back wall
    block((-0.6, d / 2 - 0.75, z0 + 2.0), (2.4, 1.0, 0.25), M('iron'), 0.02)    # its hood
    block((0.6, 0.3, z0 + 0.65), (0.9, 0.4, 0.3), M('iron'), 0.02); block((0.6, 0.3, z0 + 0.3), (0.35, 0.35, 0.6), M('wood_dk'), 0.0)   # anvil
    barrel(2.6, 2.2); K.cyl('quench', 0.3, 0.02, (2.6, 2.2, z0 + 0.81), M('water_gloss', 0.3), 10, 0.0)
    block((-w / 2 + T + 0.08, 0.5, z0 + 1.5), (0.06, 2.4, 1.2), M('wood'), 0.0)   # tool rack
    for k in range(5): block((-w / 2 + T + 0.16, -0.4 + k * 0.45, z0 + 1.5), (0.05, 0.08, 0.9), M('iron'), 0.0)
    sign(-2.4, -d / 2, 2.9, M('fire', 1.5))
    inner_walls(w, d); sconce(-w / 2 + T, -1.6, z0 + 1.9, (1, 0)); sconce(w / 2 - T, -1.6, z0 + 1.9, (-1, 0)); pots(1.6, -2.6, 2)
    with Up():
        ceiling(w, d, top); gable_roof(top + 0.36, w / 2, d / 2, 3.0, 'roof_red', True)
        chimney(-0.6, d / 2 - 0.75, top + 0.36, top + 4.6, glow=True)
def b_frostSpire():
    w, d = LY.SIZES['frostSpire']; top = room(w, d, 3.4, 'stone3', 'cobble'); top_rail(w, d, top)
    K.cyl('plinth', 0.7, 0.6, (0, 0.8, z0 + 0.3), M('stone2'), 8, 0.03)
    for (r, h, x, y) in ((0.28, 1.4, 0, 0.8), (0.16, 0.9, 0.3, 0.95), (0.16, 0.8, -0.28, 0.7)):
        K.cyl('ice', r, h, (x, y, z0 + 0.6 + h / 2), M('frost', 1.8), 6, 0.0); K.cone('ice_tip', r, r * 2.2, (x, y, z0 + 0.6 + h + r * 1.1), M('frost', 1.8), 6)
    shelf(0, d / 2 - T - 0.25, 3.0, True, ('frost', 'frost_dk', 'roof_blue', 'snow'))
    lantern(-2.0, -d / 2, 2.6); lantern(2.0, -d / 2, 2.6)
    inner_walls(w, d); rug(0, -0.6, 3.0, 2.0, 'roof_blue'); hanging(-w / 2 + T + 0.03, -0.2, z0 + 1.9, 1.2, 'roof_blue', 'y'); sconce(w / 2 - T, -1.4, z0 + 1.9, (-1, 0))
    with Up():
        ceiling(w, d, top); block((0, 0, top + 0.4), (w + 0.4, d + 0.4, 0.2), M('roof_slate'), 0.02)
        zb = top + 0.5
        F.prism('spire', 2.7, 2.4, zb, zb + 4.5, M('stone3'), n=12)
        for k in range(1, 4, 2): F.prism('course', 2.74 - k * 0.04, 2.74 - k * 0.04, zb + k, zb + k + 0.12, M('stone2'), n=12, bevel=0.0)
        block((0, -2.55, zb + 2.0), (WW, 0.08, WH), M('window', 1.6), 0.0)
        K.cyl('eave', 3.0, 0.16, (0, 0, zb + 4.6), M('wood_dk'), 12, 0.02)
        K.cyl('roof', 3.1, 3.6, (0, 0, zb + 6.5), M('roof_blue'), 12, 0.0, r2=0.1)
        for (r, h, x, y) in ((0.3, 1.6, 0, 0), (0.18, 1.0, 0.3, 0.12), (0.18, 0.9, -0.28, -0.1)):
            K.cyl('ice', r, h, (x, y, zb + 8.1 + h / 2), M('frost', 1.8), 6, 0.0); K.cone('ice_tip', r, r * 2.2, (x, y, zb + 8.1 + h + r * 1.1), M('frost', 1.8), 6)
def b_stormBell():
    w, d = LY.SIZES['stormBell']; top = room(w, d, 3.4, 'stone2', 'cobble2'); top_rail(w, d, top)
    K.cyl('rope', 0.04, 3.0, (0, 0.5, z0 + 1.9), M('canvas'), 6, 0.0)
    block((-1.1, d / 2 - T - 0.4, z0 + 0.4), (1.8, 0.5, 0.8), M('wood'), 0.02)
    inner_walls(w, d); rug(0, -0.3, 2.4, 1.8, 'roof_purple'); hanging(w / 2 - T - 0.03, -0.2, z0 + 1.9, 1.0, 'roof_purple', 'y'); sconce(-w / 2 + T, -1.2, z0 + 1.9, (1, 0)); pots(1.0, d / 2 - T - 0.35, 2)
    for k in range(3):   # jars of caught lightning on the bench
        K.cyl('jar', 0.16, 0.36, (-1.6 + k * 0.5, d / 2 - T - 0.4, z0 + 0.98), M('water_gloss', 0.2), 10, 0.0)
        K.sphere('spark', 0.08, (-1.6 + k * 0.5, d / 2 - T - 0.4, z0 + 0.98), M('storm', 3.0), sub=1)
    with Up():
        ceiling(w, d, top); zb = top + 0.36
        F.prism('belfry', 2.3, 2.2, zb, zb + 2.2, M('stone2'), n=4)
        for (x, y) in ((-1.4, -1.4), (1.4, -1.4), (-1.4, 1.4), (1.4, 1.4)): block((x, y, zb + 3.4), (0.32, 0.32, 2.4), M('timber'), 0.0)
        block((0, 0, zb + 4.7), (3.4, 3.4, 0.2), M('timber'), 0.0)
        K.cyl('bell', 0.75, 1.2, (0, 0, zb + 3.4), M('gold'), 16, 0.02, r2=0.38)
        K.sphere('glow', 0.32, (0, 0, zb + 2.8), M('storm', 3.0), sub=2)
        o = K.cone('roof', 2.7, 2.4, (0, 0, zb + 6.0), M('roof_purple'), 4); o.rotation_euler = (0, 0, math.pi / 4)
        K.cyl('rod', 0.06, 1.0, (0, 0, zb + 7.7), M('gold'), 6, 0.0); K.sphere('orb', 0.3, (0, 0, zb + 8.3), M('storm', 3.5), sub=2)
def b_bakery():
    w, d = LY.SIZES['bakery']; top = room(w, d, ST, 'stone2', 'wood'); top_rail(w, d, top)
    K.sphere('oven', 1.2, (1.7, d / 2 - 1.5, z0 + 0.2), M('stone3'), (1, 0.9, 0.85), 2)
    K.sphere('oven_mouth', 0.32, (1.7, d / 2 - 2.55, z0 + 0.55), M('fire', 2.5), (1.2, 0.5, 0.8), 2)
    block((-1.6, -0.6, z0 + 0.5), (2.6, 0.7, 1.0), M('wood'), 0.02)   # the counter, loaves on it
    for k in range(5): K.sphere('loaf', 0.16, (-2.6 + k * 0.5, -0.6, z0 + 1.08), M('hay'), (1.4, 0.8, 0.6), 1)
    sacks(-3.0, 1.6, 4)
    sign(-2.4, -d / 2, 2.7, M('gold', 1.5))
    inner_walls(w, d); rug(-1.4, -2.0, 2.2, 1.2, 'awning'); sconce(-w / 2 + T, -1.6, z0 + 1.9, (1, 0)); pots(-0.4, d / 2 - T - 0.35, 3)
    with Up():
        ceiling(w, d, top); zt, hx, hy = upper_floor(w, d, top + 0.36)
        gable_roof(zt, hx, hy, 3.4, 'roof_terra', ridge_x=False); chimney(1.7, d / 2 - 1.5, zt - 1, zt + 3.2)
def b_market():
    w, d = LY.SIZES['market']; hx, hy = w / 2, d / 2
    block((0, 0, PL / 2), (w + 0.4, d + 0.4, PL), M('cobble'), 0.02)
    for x in (-hx + 0.3, -hx / 3, hx / 3, hx - 0.3):
        for y in (-hy + 0.3, hy - 0.3): block((x, y, FLOOR + 1.9), (0.3, 0.3, 3.8), M('timber'), 0.0)
    for (x, c) in ((-2.8, 'fire'), (0.0, 'flower_y'), (2.8, 'grass3')):   # three stalls with goods, crates along the back
        table(x, -0.6, 2.0, 1.0, (c, c, 'blush'))
        block((x, -0.6, z0 + 2.3), (2.4, 1.4, 0.06), M(R.choice(('awning', 'roof_blue', 'canvas'))), 0.0)
        for s in (-1, 1): block((x + s * 1.1, -0.6, z0 + 1.55), (0.06, 0.06, 1.5), M('wood_dk'), 0.0)
    for k in range(5): crate(-3.6 + k * 1.8, hy - 1.0)
    barrel(hx - 1.0, 0.8); barrel(hx - 1.6, 1.4, 0.9); sacks(-hx + 0.8, 0.6, 3)
    with Up():
        block((0, 0, FLOOR + 3.9), (w + 0.2, d + 0.2, 0.2), M('timber'), 0.0)
        gable_roof(FLOOR + 4.0, hx, hy, 2.2, 'awning', ridge_x=True, overhang=0.4)
def b_tavern():
    w, d = LY.SIZES['tavern']; top = room(w, d, ST, 'stone2', 'wood'); top_rail(w, d, top)
    block((2.2, d / 2 - 1.6, z0 + 0.55), (3.6, 0.6, 1.1), M('wood_dk'), 0.02)   # the bar, barrels behind it
    for k in range(4): barrel(0.9 + k * 0.9, d / 2 - 0.75, 0.9)
    for (x, y) in ((-2.4, -1.0), (-2.4, 1.4), (1.0, -0.8)):
        table(x, y, 1.6, 0.9, ('hay', 'gold')); stool(x - 1.0, y); stool(x + 1.0, y)
    hearth(-w / 2 + T + 0.36, 1.4, along_x=False, big=True)
    inner_walls(w, d); rug(-2.4, 0.2, 3.0, 4.0, 'awning'); hanging(0.0, d / 2 - T - 0.03, z0 + 2.0, 1.4, 'banner')
    for (x, y, f) in ((-w / 2 + T, -2.2, (1, 0)), (w / 2 - T, -2.2, (-1, 0)), (w / 2 - T, 1.6, (-1, 0))): sconce(x, y, z0 + 1.9, f)
    sign(-3.4, -d / 2, 2.7, M('gold', 1.5)); lantern(1.6, -d / 2, 2.7)
    with Up():
        ceiling(w, d, top); zt, hx, hy = upper_floor(w, d, top + 0.36, plaster='plaster2')
        gable_roof(zt, hx, hy, 3.6, 'roof_red', ridge_x=True); chimney(-w / 2 + 1.0, 1.4, zt - 1, zt + 4.0)
def b_granary():
    w, d = LY.SIZES['granary']; top = room(w, d, 4.0, 'wood', 'wood', side_win=False); top_rail(w, d, top, 'wood_dk')
    for k in range(int(w / 0.5)):   # boards on the outside of the front and back
        x = -w / 2 + 0.25 + k * 0.5
        if abs(x) < DW / 2 + 0.3: continue
        for y in (-d / 2 - 0.01, d / 2 + 0.01): block((x, y, FLOOR + 2.0), (0.05, 0.04, 4.0), M('wood_dk'), 0.0)
    for (x, y) in ((-2.4, 2.0), (-1.1, 2.0), (-2.4, 0.8)): K.cyl('bale', 0.55, 1.1, (x, y, z0 + 0.55), M('hay'), 12, 0.03).rotation_euler = (0, math.pi / 2, 0)
    sacks(1.4, 1.4, 6)
    for k in range(6): block((2.8, -1.0, z0 + 0.3 + k * 0.55), (0.6, 0.06, 0.06), M('wood'), 0.0)   # ladder to the loft
    for s in (-1, 1): block((2.8 + s * 0.3, -1.0, z0 + 1.6), (0.06, 0.06, 3.2), M('wood'), 0.0)
    for k in range(5): K.sphere('straw', 0.5, (R.uniform(-3, 1), R.uniform(-2.5, 0.5), z0 + 0.05), M('hay'), (1.4, 1.1, 0.2), 1)   # loose straw
    barrel(-3.1, -2.6); crate(-2.3, -2.8)
    with Up():
        ceiling(w, d, top); gable_roof(top + 0.36, w / 2, d / 2, 4.0, 'roof_red', ridge_x=False, gable_m='wood')
        block((0, -d / 2 - 0.05, top + 1.4), (1.4, 0.1, 1.6), M('wood_dk'), 0.0)   # the hayloft door
def b_library():
    w, d = LY.SIZES['library']; top = room(w, d, 4.6, 'stone3', 'cobble'); top_rail(w, d, top, 'stone2')
    for s in (-1, 1): shelf(s * (w / 2 - T - 0.25), 2.0, 2.8, False)
    shelf(0, d / 2 - T - 0.25, 4.0, True)
    table(0, 0.4, 2.4, 1.1, ('plaster', 'roof_red')); stool(-0.8, -0.5); stool(0.8, -0.5)
    K.sphere('globe', 0.35, (2.3, -1.6, z0 + 1.15), M('roof_blue'), sub=2); block((2.3, -1.6, z0 + 0.4), (0.12, 0.12, 0.8), M('wood_dk'), 0.0)
    inner_walls(w, d); rug(0, 0.2, 4.0, 3.0, 'banner')
    for (x, y, f) in ((-w / 2 + T, -1.2, (1, 0)), (w / 2 - T, -1.2, (-1, 0))): sconce(x, y, z0 + 2.2, f)
    for k in range(3): K.cyl('candle', 0.05, 0.22, (-0.6 + k * 0.4, 0.4, z0 + 0.92), M('plaster'), 6, 0.0); K.cone('wick', 0.04, 0.1, (-0.6 + k * 0.4, 0.4, z0 + 1.07), M('fire_core', 3.0), 6)
    K.cyl('rose_frame', 0.8, 0.12, (0, -d / 2 - 0.02, FLOOR + 3.6), M('stone2'), 20, 0.02).rotation_euler = (math.pi / 2, 0, 0)
    K.cyl('rose', 0.66, 0.08, (0, -d / 2 - 0.05, FLOOR + 3.6), M('window', 2.0), 20, 0.0).rotation_euler = (math.pi / 2, 0, 0)
    with Up():
        ceiling(w, d, top); gable_roof(top + 0.36, w / 2, d / 2, 3.6, 'roof_slate', ridge_x=True); chimney(-2.8, 1.6, top + 0.36, top + 4.8)
def b_cottages():
    w, d = LY.SIZES['cottages']; top = room(w, d, 3.0, 'stone2', 'wood'); top_rail(w, d, top)
    block((0, 0.9, z0 + 1.5), (0.16, d - 2 * T - 1.8, 3.0), M('timber'), 0.0)   # the party wall between the two homes, open at the front
    bed(-3.4, 1.0); bed(3.4, 1.0); table(-2.0, -0.9, 1.2, 0.8); table(2.0, -0.9, 1.2, 0.8); stool(-2.0, -1.6); stool(2.0, -1.6)
    hearth(-1.2, d / 2 - T - 0.36); hearth(1.2, d / 2 - T - 0.36)
    inner_walls(w, d, 3.0); rug(-2.6, -0.4, 1.8, 1.4, 'roof_blue'); rug(2.6, -0.4, 1.8, 1.4, 'awning'); pots(-4.3, -2.2, 2); pots(3.6, -2.2, 2)
    for k in range(4): F.cloth('wash', -1.2 + k * 0.6, -0.8 + k * 0.6, 2.2, 1.6, -d / 2 - 1.2, M(R.choice(('plaster', 'cloth', 'blush'))), 0.02, False, 'x', 2, 2)
    for s in (-1, 1): block((s * 1.6, -d / 2 - 1.2, 1.15), (0.06, 0.06, 2.3), M('wood'), 0.0)
    block((0, -d / 2 - 1.2, 2.25), (3.3, 0.02, 0.02), M('canvas'), 0.0)
    with Up():
        ceiling(w, d, top)
        for s, roof in ((-1, 'roof_terra'), (1, 'roof_red')):   # two homes under two roofs, gables to the front
            cx = s * w / 4
            vs = [(cx - w / 4 - 0.3, -d / 2 - 0.4, top + 0.36), (cx + w / 4 + 0.3, -d / 2 - 0.4, top + 0.36), (cx + w / 4 + 0.3, d / 2 + 0.4, top + 0.36),
                  (cx - w / 4 - 0.3, d / 2 + 0.4, top + 0.36), (cx, -d / 2 - 0.4, top + 3.2), (cx, d / 2 + 0.4, top + 3.2)]
            o = mesh_from('roof', vs, [(1, 2, 5, 4), (3, 0, 4, 5), (0, 1, 4), (2, 3, 5)], M(roof), 0.0)
            o.modifiers.new('thick', 'SOLIDIFY').thickness = 0.2
            mesh_from('gable', [(cx - w / 4, -d / 2, top + 0.36), (cx + w / 4, -d / 2, top + 0.36), (cx, -d / 2, top + 2.9)], [(0, 1, 2)], M('plaster'), 0.0)
            chimney(cx + s * 1.0, d / 2 - T - 0.36, top + 0.36, top + 3.9)
def b_watchtower():
    w, d = LY.SIZES['watchtower']; top = room(w, d, 3.4, 'stone2', 'cobble2'); top_rail(w, d, top)
    for k in range(7): block((w / 2 - T - 0.5, -1.6 + k * 0.45, z0 + 0.2 + k * 0.4), (0.9, 0.45, 0.4), M('stone3'), 0.02)   # stairs up
    block((-w / 2 + T + 0.08, 0, z0 + 1.4), (0.06, 2.4, 1.6), M('wood'), 0.0)   # a weapon rack with spears
    for k in range(5): block((-w / 2 + T + 0.2, -0.9 + k * 0.45, z0 + 1.2), (0.05, 0.05, 2.2), M('wood_dk'), 0.0)
    inner_walls(w, d); hanging(0.0, d / 2 - T - 0.03, z0 + 1.9, 1.2, 'banner'); barrel(-1.8, 2.0); crate(-1.0, 2.2); sconce(-w / 2 + T, 1.6, z0 + 1.9, (1, 0))
    with Up():
        ceiling(w, d, top); zb = top + 0.36
        F.prism('wt_body', 2.4, 2.1, zb, zb + 4.5, M('stone2'), n=4)
        block((0, -1.65, zb + 2.2), (0.5, 0.08, 0.9), M('window', 1.6), 0.0)
        block((0, 0, zb + 4.7), (4.6, 4.6, 0.4), M('timber'), 0.02)
        for (x, y) in ((-2.0, -2.0), (2.0, -2.0), (-2.0, 2.0), (2.0, 2.0)): block((x, y, zb + 5.8), (0.28, 0.28, 1.9), M('timber'), 0.0)
        o = K.cone('roof', 3.3, 2.2, (0, 0, zb + 7.8), M('roof_terra'), 4); o.rotation_euler = (0, 0, math.pi / 4)
        K.cyl('brazier', 0.55, 0.45, (0, 0, zb + 5.15), M('iron'), 10, 0.02, r2=0.4)
        K.cone('beacon', 0.45, 1.1, (0, 0, zb + 5.9), M('gold', 4.0), 10)

BUILDERS = {'forge': b_forge, 'frostSpire': b_frostSpire, 'stormBell': b_stormBell, 'bakery': b_bakery, 'market': b_market,
            'tavern': b_tavern, 'granary': b_granary, 'library': b_library, 'cottages': b_cottages, 'watchtower': b_watchtower}

def signpost(w, d, ruined=False):
    """The building's sign by its yard (owner, Nov 13 2026: "a data sign or something that shows the level and stats of the
    buildings"): a post and a board facing the street; the game writes the name, level and output over the board."""
    x, y = w / 2 - 0.7, -d / 2 - 1.4
    block((x, y, 1.1), (0.14, 0.14, 2.2), M('char' if ruined else 'wood_dk'), 0.0)
    o = block((x, y - 0.1, 2.0), (1.5, 0.08, 0.8), M('wood' if not ruined else 'char'), 0.02)
    if ruined: o.rotation_euler = (0, 0.18, 0)
    block((x, y - 0.15, 2.42), (1.6, 0.06, 0.08), M('gold' if not ruined else 'iron'), 0.0)
    rubble(x, y, 2, (0.2, 0.2)); tufts(x + 0.3, y + 0.1, 2, (0.2, 0.2))

def building(bid):
    BUILDERS[bid]()
    w, d = LY.SIZES[bid]
    signpost(w, d)
    tufts(-w / 2 - 0.2, -d / 2 - 0.3, 2, (0.3, 0.2)); tufts(w / 2 + 0.2, -d / 2 - 0.2, 2, (0.3, 0.2)); tufts(w / 2 + 0.2, d / 2 + 0.2, 2, (0.3, 0.2))
    return K.export('building_' + bid)

# ------------------------------------------------------------------------------------------------- the ruins
# Each plot's ruin is the burnt shell of its own building at its own footprint: charred walls of uneven height with the
# doorway still open (the wizard walks in to rebuild), fallen beams, embers in the rubble, smoke, scaffolding and stacked
# fresh timber waiting outside.
SHELLS = {bid: LY.SIZES[bid] for bid in LY.IDS}

def broken(x0, x1, y, axis, hmax, gap=None):
    x, k = x0, 0
    while x < x1 - 0.1:
        L = min(R.uniform(0.5, 0.9), x1 - x)
        if gap and x + L > gap[0] and x < gap[1]: x = gap[1]; continue
        h = max(0.3, hmax * R.uniform(0.3, 1.0) * (0.6 + 0.4 * math.sin(k * 0.8 + R.random())))
        loc = (x + L / 2, y, FLOOR + h / 2) if axis == 'x' else (y, x + L / 2, FLOOR + h / 2)
        size = (L - 0.04, T, h) if axis == 'x' else (T, L - 0.04, h)
        block(loc, size, M(R.choice(('stone2', 'stone_dk', 'stone2'))), 0.04)
        if h > 0.8: block((loc[0], loc[1], FLOOR + h + 0.03), (size[0], size[1], 0.06), M('char'), 0.0)
        x += L; k += 1

def ruin_of(bid):
    w, d = LY.SIZES[bid]; hx, hy = w / 2, d / 2
    block((0, 0, PL / 2), (w + 0.4, d + 0.4, PL), M('stone_dk'), 0.03)
    block((0, 0, PL + 0.02), (w - 2 * T, d - 2 * T, 0.04), M('char'), 0.0)
    if bid != 'market':
        broken(-hx, hx, hy - T / 2, 'x', 2.6)
        for s in (-1, 1): broken(-hy + T, hy - T, s * (hx - T / 2), 'y', 2.2)
        broken(-hx, hx, -hy + T / 2, 'x', 1.8, gap=(-DW / 2 - 0.24, DW / 2 + 0.24))
        for s in (-1, 1): block((s * (DW / 2 + 0.12), -hy + T / 2 - 0.05, FLOOR + DH / 2), (0.24, 0.14, DH), M('stone3'), 0.02)   # the door frame survived
        block((0, -hy + T / 2 - 0.05, FLOOR + DH + 0.14), (DW + 0.6, 0.14, 0.28), M('char'), 0.02)
    else:   # the market: charred posts and a torn awning
        for (x, y) in ((-hx + 0.3, -hy + 0.3), (hx - 0.3, -hy + 0.3), (-hx + 0.3, hy - 0.3), (hx / 3, hy - 0.3)):
            o = block((x, y, FLOOR + 1.4), (0.3, 0.3, 2.8), M('char'), 0.0); o.rotation_euler = (R.uniform(-0.2, 0.2), R.uniform(-0.2, 0.2), 0)
        o = block((0, 0.6, FLOOR + 1.0), (6.0, 3.6, 0.06), M('awning'), 0.0); o.rotation_euler = (0.3, 0.15, 0)
        for k in range(5): crate(R.uniform(-hx + 1, hx - 1), R.uniform(-hy + 1, 0.5), 0.9)
    for k in range(4):   # fallen, charred beams
        o = block((R.uniform(-w / 3, w / 3), R.uniform(0.3, d / 3), FLOOR + 0.5), (0.22, 0.22, R.uniform(2.4, 3.6)), M('char'), 0.0)
        o.rotation_euler = (R.uniform(0.9, 1.3), 0, R.uniform(0, 3))
    rubble(0.6, 1.0, 10, (w / 3, d / 4))
    for k in range(6): K.sphere('ember', 0.08, (R.uniform(-w / 3, w / 3), R.uniform(0.3, d / 3), FLOOR + 0.12), M('fire', 2.5), sub=1)
    for k, (dz, r) in enumerate(((2.2, 0.45), (3.2, 0.6), (4.4, 0.72))):
        K.sphere('smoke', r, (0.4 + k * 0.35, 1.0, dz), M('rock'), (1, 1, 0.85), 1)
    for (x, y) in ((-hx + 0.2, -hy - 0.5), (-hx + 2.2, -hy - 0.5)): block((x, y, 1.6), (0.1, 0.1, 3.2), M('wood'), 0.0)   # scaffolding
    for z in (1.2, 2.6): block((-hx + 1.2, -hy - 0.5, z), (2.2, 0.1, 0.1), M('wood'), 0.0)
    block((-hx + 1.2, -hy - 0.32, 2.65), (2.2, 0.45, 0.08), M('wood'), 0.0)
    for k in range(4): block((hx - 2.6, -hy - 1.0, 0.14 + k * 0.16), (1.6, 0.18, 0.15), M('wood'), 0.0)   # fresh timber
    tufts(-hx, -hy - 0.4, 2, (0.3, 0.2)); tufts(hx, hy, 2, (0.3, 0.2))
    signpost(w, d, ruined=True)
    return K.export('ruin_' + bid)

def ruin():   # the generic ruin, a stand-in if a plot's own shell were missing
    w, d = 6.0, 5.0; hx, hy = w / 2, d / 2
    block((0, 0, PL / 2), (w + 0.4, d + 0.4, PL), M('stone_dk'), 0.03)
    broken(-hx, hx, hy - T / 2, 'x', 2.2); broken(-hx, hx, -hy + T / 2, 'x', 1.6, gap=(-0.84, 0.84))
    for s in (-1, 1): broken(-hy + T, hy - T, s * (hx - T / 2), 'y', 1.8)
    rubble(0.3, 0.3, 6, (1.6, 1.2))
    return K.export('ruin')
