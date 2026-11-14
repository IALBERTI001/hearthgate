# Hearthgate 3D, the city (Nov 13 2026, after the fresh-eyes review: "the city reads as debris on dirt discs ... the houses
# are tiny cubes ... cylinders with circus hats"). Chunky storybook buildings with a stone ground floor, a jettied timber-
# framed upper floor, steep roofs in a shared palette (terracotta, red, slate), chimneys and warm lit windows; the three
# element buildings say their element; ruins read as burnt timber frames with charred walls and scaffolding, waiting to
# be rebuilt. Footprints stay within 2.3 m of the origin (Field / LayoutCheck); fronts face -y (Field.PLOT_FACING turns
# them to the camera). Imported by hg_kit.py.
import bpy, math, random
import hg_kit as K
import hg_front as F

R = random.Random(29)
M, block, mesh_from, window, tufts, rubble = F.M, F.block, F.mesh_from, F.window, F.tufts, F.rubble

def win(x, y, z, w=0.5, h=0.62, shutters=True):   # a lit window on a -y face, with a frame and shutters
    block((x, y - 0.03, z), (w + 0.16, 0.06, h + 0.16), M('timber'), 0.0)
    block((x, y - 0.07, z), (w, 0.03, h), M('window', 1.8), 0.0)
    block((x, y - 0.09, z), (0.05, 0.02, h), M('timber'), 0.0); block((x, y - 0.09, z), (w, 0.02, 0.05), M('timber'), 0.0)
    if shutters:
        for s in (-1, 1): block((x + s * (w / 2 + 0.17), y - 0.05, z), (0.26, 0.05, h + 0.06), M('roof_slate' if R.random() < 0.5 else 'wood'), 0.01)

def door(x, y, w=0.75, h=1.25, m='wood'):
    block((x, y - 0.04, h / 2 + 0.05), (w + 0.2, 0.08, h + 0.15), M('stone3'), 0.02)
    block((x, y - 0.08, h / 2), (w, 0.04, h), M(m), 0.01)
    for k in (0.25, 0.75): block((x, y - 0.11, h * k), (w, 0.02, 0.06), M('iron'), 0.0)
    K.sphere('knob', 0.04, (x + w * 0.3, y - 0.12, h * 0.48), M('gold'), sub=1)
    block((x, y - 0.3, 0.06), (w + 0.4, 0.45, 0.12), M('stone_dk'), 0.02)

def gable_roof(cx, cy, z0, hx, hy, rise, m, ridge_x=True, overhang=0.3):
    """A gable roof over a box of half sizes hx, hy, ridge along x (gables at the sides) or y (gable to the front)."""
    if ridge_x:
        a, b = hx + overhang, hy + overhang
        vs = [(cx - a, cy - b, z0), (cx + a, cy - b, z0), (cx + a, cy + b, z0), (cx - a, cy + b, z0), (cx - a, cy, z0 + rise), (cx + a, cy, z0 + rise)]
        fs = [(0, 1, 5, 4), (2, 3, 4, 5), (1, 2, 5), (3, 0, 4)]
    else:
        a, b = hx + overhang, hy + overhang
        vs = [(cx - a, cy - b, z0), (cx + a, cy - b, z0), (cx + a, cy + b, z0), (cx - a, cy + b, z0), (cx, cy - b, z0 + rise), (cx, cy + b, z0 + rise)]
        fs = [(1, 2, 5, 4), (3, 0, 4, 5), (0, 1, 4), (2, 3, 5)]
    o = mesh_from('roof', vs, fs, m, 0.0)
    sol = o.modifiers.new('thick', 'SOLIDIFY'); sol.thickness = 0.14; sol.offset = 1.0
    # ridge cap and tile courses on the slope facing the camera (-y)
    if ridge_x:
        block((cx, cy, z0 + rise + 0.05), (2 * a + 0.1, 0.2, 0.14), M('roof_red' if 'terra' in m.name else 'stone_dk'), 0.02)
        for k in range(1, 5):
            t = k / 5; o2 = block((cx, cy - b * (1 - t) - 0.06, z0 + rise * t + 0.08), (2 * a, 0.05, 0.07), M('wood_dk' if 'slate' in m.name else 'roof_red'), 0.0)
            o2.rotation_euler = (math.atan2(rise, b), 0, 0)
    return o

def gable_wall(cx, y, z0, hx, rise, m):   # the triangle closing a gable on a -y face (ridge along y)
    mesh_from('gable', [(cx - hx, y, z0), (cx + hx, y, z0), (cx, y, z0 + rise)], [(0, 1, 2)], m, 0.0)

def framing(x0, x1, y, z0, z1):   # timber framing on a -y plaster face: posts, rails and braces
    for x in [x0 + 0.06] + [x0 + (x1 - x0) * k / 3 for k in (1, 2)] + [x1 - 0.06]:
        block((x, y - 0.03, (z0 + z1) / 2), (0.12, 0.06, z1 - z0), M('timber'), 0.0)
    for z in (z0 + 0.06, z1 - 0.06): block(((x0 + x1) / 2, y - 0.03, z), (x1 - x0, 0.06, 0.12), M('timber'), 0.0)
    L = math.hypot((x1 - x0) / 3, z1 - z0)
    for (xa, s) in ((x0, 1), (x1, -1)):
        o = block((xa + s * (x1 - x0) / 6, y - 0.04, (z0 + z1) / 2), (0.1, 0.05, L), M('timber'), 0.0)
        o.rotation_euler = (0, s * math.atan2((x1 - x0) / 3, z1 - z0), 0)

def chimney(x, y, ztop, glow=False):
    block((x, y, ztop - 0.9), (0.55, 0.55, 1.8), M('stone2'), 0.03)
    block((x, y, ztop + 0.05), (0.7, 0.7, 0.14), M('stone_dk'), 0.02)
    if glow: K.sphere('ember', 0.2, (x, y, ztop + 0.25), M('fire', 3.0), sub=2)

def house(w=3.6, d=3.2, floors=2, roof='roof_terra', ridge_x=True, plaster='plaster', chim=True, x0=0.0, y0=0.0, scale=1.0):
    hx, hy = w / 2 * scale, d / 2 * scale
    g = 1.5 * scale; u = 1.35 * scale
    block((x0, y0, 0.12), (2 * hx + 0.3, 2 * hy + 0.3, 0.24), M('stone_dk'), 0.03)
    K.cube('ground', (2 * hx, 2 * hy, g), (x0, y0, 0.24 + g / 2), M('stone2'), 0.04)
    for s in (-1, 1):   # quoins at the corners of the stone floor
        for k in range(3):
            block((x0 + s * (hx - 0.12), y0 - hy - 0.02, 0.4 + k * 0.45), (0.3 if k % 2 else 0.22, 0.08, 0.36), M('stone3'), 0.02)
    door(x0 + (0.0 if w < 3 else -0.6 * scale), y0 - hy)
    if w >= 3: win(x0 + 0.85 * scale, y0 - hy, 0.24 + g * 0.55, 0.5 * scale, 0.55 * scale, False)
    top = 0.24 + g
    if floors >= 2:
        K.cube('upper', (2 * hx + 0.3, 2 * hy + 0.3, u), (x0, y0, top + u / 2), M(plaster), 0.03)   # jettied
        block((x0, y0 - hy - 0.16, top + 0.05), (2 * hx + 0.4, 0.2, 0.14), M('timber'), 0.0)
        framing(x0 - hx - 0.15, x0 + hx + 0.15, y0 - hy - 0.15, top, top + u)
        for s in (-1, 1): win(x0 + s * 0.85 * scale, y0 - hy - 0.15, top + u * 0.52, 0.48 * scale, 0.58 * scale)
        top += u; ehx, ehy = hx + 0.15, hy + 0.15
    else: ehx, ehy = hx, hy
    rise = 1.7 * scale if ridge_x else 1.9 * scale
    gable_roof(x0, y0, top, ehx, ehy, rise, M(roof), ridge_x)
    if not ridge_x:
        gable_wall(x0, y0 - ehy - 0.01, top, ehx, rise, M(plaster))
        win(x0, y0 - ehy - 0.02, top + rise * 0.4, 0.36 * scale, 0.42 * scale, False)
    if chim: chimney(x0 + ehx * 0.55, y0 + ehy * 0.3, top + rise * 0.85)
    return top + rise

def roundtower(x0, y0, r, h, roof, roof_h, crown=None):
    F.prism('rt_foot', r + 0.25, r + 0.05, 0, 0.6, M('stone_dk'), n=12)
    F.prism('rt_body', r, r * 0.94, 0.5, h, M('stone2'), n=12)
    for k in range(int(h / 0.5)):   # string courses every other course
        if k % 2: F.prism('course', r * (1 - 0.06 * k / h * 0.5) + 0.04, r * (1 - 0.06 * k / h * 0.5) + 0.04, 0.5 + k * 0.5, 0.58 + k * 0.5, M('stone3'), n=12, bevel=0.0)
    for z in (1.4, 2.9):
        win(x0, y0 - r * 0.97, z, 0.4, 0.62, False)
    door(x0, y0 - r * 0.98, 0.65, 1.1)
    K.cyl('eave', r + 0.4, 0.12, (x0, y0, h + 0.06), M('wood_dk'), 12, 0.02)
    K.cyl('roof', r + 0.45, roof_h, (x0, y0, h + 0.12 + roof_h / 2), M(roof), 12, 0.0, r2=0.08)
    return h + 0.12 + roof_h

def barrel(x, y, s=1.0):
    K.cyl('barrel', 0.3 * s, 0.7 * s, (x, y, 0.35 * s), M('wood'), 10, 0.02)
    for z in (0.15, 0.55): K.cyl('hoop', 0.31 * s, 0.05, (x, y, z * s), M('iron'), 10, 0.0)
def crate(x, y, s=1.0):
    o = block((x, y, 0.28 * s), (0.56 * s, 0.56 * s, 0.56 * s), M('wood'), 0.03); o.rotation_euler = (0, 0, R.uniform(-0.4, 0.4))

def sign(x, y, z, icon_mat):   # a hanging sign on an iron bracket, with a glowing icon
    block((x, y - 0.35, z + 0.35), (0.06, 0.7, 0.06), M('iron'), 0.0)
    block((x, y - 0.62, z), (0.06, 0.5, 0.42), M('wood'), 0.01)
    K.sphere('icon', 0.12, (x - 0.05, y - 0.62, z), icon_mat, (0.5, 1, 1), 2)

# ------------------------------------------------------------------------------------------------- the ten buildings
def b_forge():
    house(3.4, 3.0, 1, 'roof_red', ridge_x=True, chim=False)
    chimney(1.2, 0.6, 3.9, glow=True); K.sphere('smoke', 0.3, (1.2, 0.6, 4.6), M('rock'), (1, 1, 0.8), 1)
    block((-2.0, -0.4, 1.3), (1.0, 2.0, 0.1), M('roof_red'), 0.0).rotation_euler = (0, 0.35, 0)   # lean-to shed
    for y in (-1.2, 0.4): block((-2.35, y, 0.6), (0.12, 0.12, 1.2), M('timber'), 0.0)
    block((-2.0, -0.4, 0.35), (0.8, 1.2, 0.7), M('stone_dk'), 0.03); K.sphere('hearth', 0.3, (-2.0, -0.9, 0.62), M('fire', 3.0), (1.2, 0.7, 0.7), 2)
    block((-1.6, -1.6, 0.45), (0.5, 0.25, 0.25), M('iron'), 0.02); block((-1.6, -1.6, 0.2), (0.2, 0.2, 0.4), M('wood_dk'), 0.0)   # anvil
def b_frostSpire():
    top = roundtower(0, 0, 1.45, 4.6, 'roof_blue', 2.3)
    for (r, h, x, y) in ((0.18, 1.0, 0, 0), (0.1, 0.6, 0.18, 0.08), (0.1, 0.55, -0.16, -0.08)):
        K.cyl('ice', r, h, (x, y, top + h / 2 - 0.1), M('frost', 1.8), 6, 0.0); K.cone('ice_tip', r, r * 2, (x, y, top + h + r - 0.1), M('frost', 1.8), 6)
    house(2.0, 1.8, 1, 'roof_slate', ridge_x=True, chim=False, x0=-1.3, y0=0.8, scale=0.85)
def b_stormBell():
    F.prism('belfry', 1.35, 1.3, 0, 3.8, M('stone2'), n=4)
    door(0, -0.95, 0.7, 1.2); win(0, -0.95, 2.6, 0.42, 0.6, False)
    for (x, y) in ((-0.85, -0.85), (0.85, -0.85), (-0.85, 0.85), (0.85, 0.85)): block((x, y, 4.5), (0.24, 0.24, 1.4), M('timber'), 0.0)
    block((0, 0, 5.25), (2.0, 2.0, 0.14), M('timber'), 0.0)
    K.cyl('bell', 0.42, 0.7, (0, 0, 4.6), M('gold'), 16, 0.02, r2=0.22)
    K.sphere('glow', 0.2, (0, 0, 4.25), M('storm', 3.0), sub=2)
    o = K.cone('roof', 1.55, 1.7, (0, 0, 6.15), M('roof_purple'), 4); o.rotation_euler = (0, 0, math.pi / 4)
    K.cyl('rod', 0.05, 0.9, (0, 0, 7.4), M('gold'), 6, 0.0); K.sphere('orb', 0.2, (0, 0, 7.9), M('storm', 3.5), sub=2)
def b_bakery():
    house(3.2, 3.0, 2, 'roof_terra', ridge_x=False)
    K.sphere('oven', 0.85, (1.55, 0.3, 0.3), M('stone3'), (1, 1, 0.85), 2)
    K.sphere('oven_mouth', 0.25, (1.55, -0.45, 0.45), M('fire', 2.5), (1.2, 0.5, 0.8), 2)
    sign(-1.85, -1.6, 2.3, M('gold', 1.5))
def b_market():
    block((0, 0, 0.1), (4.4, 3.8, 0.2), M('cobble'), 0.02)
    for (x, y) in ((-1.9, -1.6), (1.9, -1.6), (-1.9, 1.6), (1.9, 1.6), (0, -1.6), (0, 1.6)): block((x, y, 1.35), (0.18, 0.18, 2.5), M('timber'), 0.0)
    gable_roof(0, 0, 2.62, 2.1, 1.9, 0.9, M('awning'), ridge_x=True, overhang=0.15)
    for x in (-1.3, 0.0, 1.3):
        block((x, -0.9, 0.55), (1.0, 0.7, 0.7), M('wood'), 0.02)
        for k in range(3): K.sphere('goods', 0.14, (x - 0.3 + k * 0.3, -0.9, 0.98), M(R.choice(('fire', 'flower_y', 'grass3', 'blush'))), sub=1)
    barrel(-1.7, -2.0); crate(1.8, -1.9); crate(1.4, -2.1, 0.8)
def b_tavern():
    house(4.0, 3.3, 2, 'roof_red', ridge_x=True, plaster='plaster2')
    sign(-2.15, -1.7, 2.4, M('gold', 1.5))
    barrel(1.7, -2.0); barrel(2.0, -1.5, 0.9); crate(-1.6, -2.0, 0.8)
def b_granary():
    block((0, 0, 0.15), (3.0, 3.4, 0.3), M('stone_dk'), 0.03)
    K.cube('barn', (2.8, 3.2, 2.2), (-0.3, 0, 1.4), M('wood'), 0.03)
    for x in [-1.6 + k * 0.4 for k in range(7)]: block((x, -1.62, 1.4), (0.06, 0.04, 2.2), M('wood_dk'), 0.0)
    block((-0.3, -1.66, 1.0), (1.3, 0.06, 1.6), M('wood_dk'), 0.0); block((-0.3, -1.7, 1.0), (1.3, 0.04, 0.1), M('iron'), 0.0)
    gable_roof(-0.3, 0, 2.5, 1.4, 1.6, 1.4, M('roof_red'), ridge_x=False, overhang=0.25)
    gable_wall(-0.3, -1.86, 2.5, 1.65, 1.4, M('wood'))
    K.cyl('silo', 0.75, 3.0, (1.55, 0.6, 1.5), M('stone3'), 12, 0.03)
    K.cyl('silo_roof', 0.95, 1.0, (1.55, 0.6, 3.5), M('roof_terra'), 12, 0.0, r2=0.05)
    for k in range(3): K.sphere('sack', 0.22, (-1.3 + k * 0.4, -2.0, 0.22), M('plaster2'), (1, 0.8, 1), 1)
def b_library():
    block((0, 0, 0.12), (4.2, 3.6, 0.24), M('stone_dk'), 0.03)
    K.cube('hall', (3.9, 3.3, 2.9), (0, 0, 1.69), M('stone3'), 0.04)
    for s in (-1, 1):
        block((s * 1.95, -1.66, 1.69), (0.3, 0.12, 2.9), M('stone2'), 0.02)
        win(s * 1.15, -1.65, 1.5, 0.45, 1.0, False)
    K.cyl('rose_frame', 0.62, 0.1, (0, -1.68, 2.35), M('stone2'), 20, 0.02).rotation_euler = (math.pi / 2, 0, 0)
    K.cyl('rose', 0.5, 0.06, (0, -1.72, 2.35), M('window', 2.0), 20, 0.0).rotation_euler = (math.pi / 2, 0, 0)
    door(0, -1.65, 0.8, 1.3, 'wood_dk')
    gable_roof(0, 0, 3.14, 1.95, 1.65, 1.6, M('roof_slate'), ridge_x=True)
    chimney(-1.1, 0.5, 4.5)
def b_cottages():
    house(2.4, 2.4, 1, 'roof_terra', ridge_x=False, x0=-1.05, y0=-0.35, scale=0.92)
    house(2.4, 2.4, 2, 'roof_red', ridge_x=True, x0=1.15, y0=0.55, scale=0.82, plaster='plaster2')
    for k in range(4): F.cloth('wash', -0.4 + k * 0.25, -0.2 + k * 0.25, 1.5, 1.1, -1.7, M(R.choice(('plaster', 'cloth', 'blush'))), 0.02, False, 'x', 2, 2)
def b_watchtower():
    F.prism('wt_body', 1.25, 1.1, 0, 5.2, M('stone2'), n=4)
    door(0, -0.9, 0.65, 1.15); win(0, -0.85, 3.2, 0.36, 0.55, False)
    block((0, 0, 5.35), (2.6, 2.6, 0.3), M('timber'), 0.02)
    for (x, y) in ((-1.15, -1.15), (1.15, -1.15), (-1.15, 1.15), (1.15, 1.15)): block((x, y, 6.0), (0.18, 0.18, 1.0), M('timber'), 0.0)
    o = K.cone('roof', 1.85, 1.3, (0, 0, 7.15), M('roof_terra'), 4); o.rotation_euler = (0, 0, math.pi / 4)
    K.cyl('brazier', 0.35, 0.3, (0, 0, 5.65), M('iron'), 10, 0.02, r2=0.25)
    K.cone('beacon', 0.3, 0.7, (0, 0, 6.1), M('gold', 4.0), 10)

BUILDERS = {'forge': b_forge, 'frostSpire': b_frostSpire, 'stormBell': b_stormBell, 'bakery': b_bakery, 'market': b_market,
            'tavern': b_tavern, 'granary': b_granary, 'library': b_library, 'cottages': b_cottages, 'watchtower': b_watchtower}

def building(bid):
    BUILDERS[bid]()
    tufts(-2.0, -1.9, 2, (0.3, 0.2)); tufts(2.0, -1.8, 2, (0.3, 0.2))
    return K.export('building_' + bid)

# ------------------------------------------------------------------------------------------------- the ruins
# Each plot's ruin is the burnt shell of its own building (fresh-eyes review 2: "12 identical grey lumps in rows ...
# procedural"): its footprint, its shape (round tower, belfry, barn, open market), charred walls of uneven height, fallen
# beams, embers glowing in the rubble, a wisp of smoke, and, on some, scaffolding and fresh timber waiting for the rebuild.
SHELLS = {   # footprint (w, d), shape, wall height
    'forge': (3.4, 3.0, 'box', 1.6), 'frostSpire': (2.9, 2.9, 'round', 2.6), 'stormBell': (2.6, 2.6, 'square', 2.8),
    'bakery': (3.2, 3.0, 'box', 1.8), 'market': (4.2, 3.6, 'stalls', 0.0), 'tavern': (4.0, 3.3, 'box', 2.0),
    'granary': (3.0, 3.4, 'barn', 1.4), 'library': (3.9, 3.3, 'box', 2.3), 'cottages': (2.2, 2.2, 'pair', 1.3),
    'watchtower': (2.4, 2.4, 'square', 3.0)}

def shell_walls(cx, cy, w, d, hmax, broken=0.6):
    def run(x0, x1, y, axis):
        x, k = x0, 0
        while x < x1 - 0.1:
            L = min(R.uniform(0.4, 0.75), x1 - x)
            h = max(0.25, hmax * (1 - broken * R.random()) * (0.55 + 0.45 * math.sin(k * 0.9 + R.random())))
            loc = (x + L / 2, y, h / 2 + 0.16) if axis == 'x' else (y, x + L / 2, h / 2 + 0.16)
            size = (L - 0.04, 0.32, h) if axis == 'x' else (0.32, L - 0.04, h)
            block(loc, size, M(R.choice(('stone2', 'stone_dk', 'stone2'))), 0.04)
            if h > 0.6: block((loc[0], loc[1], h + 0.16), (size[0], size[1], 0.06), M('char'), 0.0)   # scorched tops
            x += L; k += 1
    run(cx - w / 2, cx + w / 2, cy + d / 2, 'x')      # back wall, the tallest remains
    run(cy - d / 2, cy + d / 2, cx - w / 2, 'y'); run(cy - d / 2 + 0.6, cy + d / 2, cx + w / 2, 'y')
    run(cx - w / 2, cx - w / 2 + w * 0.4, cy - d / 2, 'x')   # a stub of the front wall

def ruin_of(bid):
    w, d, shape, hw = SHELLS[bid]
    block((0, 0, 0.08), (w + 0.3, d + 0.3, 0.16), M('char'), 0.02)
    if shape in ('box', 'barn'): shell_walls(0, 0, w, d, hw)
    elif shape == 'pair':
        shell_walls(-1.05, -0.35, w, d, hw); shell_walls(1.15, 0.55, w * 0.9, d * 0.9, hw * 0.8)
    elif shape == 'round':   # a round tower's broken stump
        for i in range(12):
            a = i / 12 * math.tau; h = hw * (0.35 + 0.65 * abs(math.sin(i * 0.7 + 0.4)))
            o = block((math.cos(a) * 1.35, math.sin(a) * 1.35, h / 2 + 0.16), (0.32, 0.72, h), M(R.choice(('stone2', 'stone_dk'))), 0.04); o.rotation_euler = (0, 0, a)
    elif shape == 'square':   # a belfry or watchtower's stump with a jagged top
        for (a, b) in ((-1, -1), (1, -1), (-1, 1), (1, 1)):
            h = hw * R.uniform(0.6, 1.0)
            block((a * (w / 2 - 0.2), b * (d / 2 - 0.2), h / 2 + 0.16), (0.42, 0.42, h), M('stone2'), 0.04)
        shell_walls(0, 0, w, d, hw * 0.55)
    elif shape == 'stalls':   # the market's collapsed stalls and torn awning
        for (x, y) in ((-1.6, -1.3), (1.6, -1.3), (-1.6, 1.3), (0.2, 1.3)):
            o = block((x, y, 0.9), (0.16, 0.16, 1.8), M('char'), 0.0); o.rotation_euler = (R.uniform(-0.3, 0.3), R.uniform(-0.3, 0.3), 0)
        o = block((0, 0.2, 0.9), (3.6, 2.4, 0.06), M('awning'), 0.0); o.rotation_euler = (0.35, 0.2, 0)
        for k in range(4): crate(R.uniform(-1.6, 1.6), R.uniform(-1.6, 0.4), 0.8)
    # fallen, charred beams; embers in the rubble; a wisp of smoke
    for k in range(3):
        o = block((R.uniform(-w / 3, w / 3), R.uniform(-d / 3, d / 3), 0.5), (0.16, 0.16, R.uniform(1.4, 2.4)), M('char'), 0.0)
        o.rotation_euler = (R.uniform(0.6, 1.2), 0, R.uniform(0, 3))
    rubble(0.2, 0.0, 6, (w / 3, d / 3))
    for k in range(4): K.sphere('ember', 0.07, (R.uniform(-w / 3, w / 3), R.uniform(-d / 3, d / 3), 0.25), M('fire', 2.5), sub=1)
    for k, (dz, r) in enumerate(((1.6, 0.32), (2.3, 0.42), (3.1, 0.5))):
        K.sphere('smoke', r, (0.3 + k * 0.25, 0.3, dz + hw * 0.4), M('rock'), (1, 1, 0.85), 1)
    if bid in ('forge', 'tavern', 'library', 'granary', 'watchtower', 'frostSpire'):   # scaffolding: work has begun
        for (x, y) in ((w / 2 - 0.1, -d / 2 - 0.4), (w / 2 - 1.6, -d / 2 - 0.4)): block((x, y, 1.2), (0.08, 0.08, 2.4), M('wood'), 0.0)
        for z in (0.9, 1.9): block((w / 2 - 0.85, -d / 2 - 0.4, z), (1.6, 0.08, 0.08), M('wood'), 0.0)
        block((w / 2 - 0.85, -d / 2 - 0.25, 1.95), (1.6, 0.35, 0.06), M('wood'), 0.0)
    for k in range(3): block((-w / 2 + 0.8, -d / 2 - 0.5, 0.12 + k * 0.13), (1.3, 0.14, 0.12), M('wood'), 0.0)   # fresh timber
    tufts(-w / 2, -d / 2 - 0.3, 2, (0.3, 0.2)); tufts(w / 2, d / 2, 2, (0.3, 0.2))
    return K.export('ruin_' + bid)

def ruin():   # burnt: charred floor, broken stone walls, a scorched timber frame, scaffolding and stacked new timber
    block((0, 0, 0.08), (4.0, 3.4, 0.16), M('char'), 0.02)
    def broken_wall(x0, x1, y, hmax, axis='x'):
        x, k = x0, 0
        while x < x1 - 0.1:
            L = min(R.uniform(0.45, 0.8), x1 - x); h = max(0.3, hmax * R.uniform(0.35, 1.0) * (1 - 0.6 * k / 8))
            loc = (x + L / 2, y, h / 2 + 0.16) if axis == 'x' else (y, x + L / 2, h / 2 + 0.16)
            size = (L - 0.04, 0.35, h) if axis == 'x' else (0.35, L - 0.04, h)
            block(loc, size, M(R.choice(('stone2', 'stone_dk'))), 0.04); x += L; k += 1
    broken_wall(-1.8, 1.8, 1.5, 1.8); broken_wall(-1.4, 1.2, -1.8, 1.4, axis='y'); broken_wall(-1.6, -0.2, -1.5, 0.9)
    for (x, y, h, tilt) in ((-1.5, -1.2, 2.2, 0.05), (1.5, -1.2, 1.6, -0.12), (1.5, 1.2, 2.4, 0.0), (-0.2, 1.3, 1.2, 0.2)):
        o = block((x, y, h / 2 + 0.16), (0.18, 0.18, h), M('char'), 0.0); o.rotation_euler = (tilt, 0, 0)
    o = block((0.0, -1.2, 1.6), (3.2, 0.16, 0.16), M('char'), 0.0); o.rotation_euler = (0, 0.3, 0)
    for (x, y) in ((0.6, -1.9), (1.9, -1.9)): block((x, y, 1.2), (0.08, 0.08, 2.4), M('wood'), 0.0)    # scaffolding
    for z in (0.9, 1.9): block((1.25, -1.9, z), (1.5, 0.08, 0.08), M('wood'), 0.0)
    block((1.25, -1.75, 1.95), (1.5, 0.35, 0.06), M('wood'), 0.0)
    for k in range(4): block((-0.9, -2.0, 0.12 + k * 0.13), (1.5, 0.14, 0.12), M('wood'), 0.0)       # fresh timber stacked
    rubble(0.3, 0.0, 6, (1.2, 0.9)); tufts(-1.8, -1.9, 2, (0.3, 0.2)); tufts(1.9, 1.7, 2, (0.3, 0.2))
    return K.export('ruin')
