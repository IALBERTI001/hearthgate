# Hearthgate 3D kit, block-out pass (Nov 13 2026): builds every model the Unity slice needs, in Blender, and exports one
# FBX per model into the Unity project (Assets/Art/Models). One model per file, origin at the base centre, metres,
# +Y up in Unity. Shapes are chunky and readable from the high three-quarter camera; colours follow the 2D game's
# palette. This pass proves the pipeline and the scale; the art pass replaces each model under the same file name.
#
# Run headless:  blender -b --factory-startup -P hg_kit.py -- <output folder>
# or from the Blender MCP: exec(open(path).read(), {'__name__': '__main__', 'OUT': r'...\Assets\Art\Models'})
import bpy, bmesh, math, os, sys, random
from mathutils import Vector

OUT = globals().get('OUT') or (sys.argv[sys.argv.index('--') + 1] if '--' in sys.argv else os.path.join(os.path.dirname(__file__), 'out'))
os.makedirs(OUT, exist_ok=True)
random.seed(7)

PAL = {
    'stone': (0.86, 0.79, 0.64), 'stone_dk': (0.6, 0.54, 0.45), 'gold': (1.0, 0.78, 0.3), 'roof_red': (0.78, 0.27, 0.2),
    'roof_blue': (0.25, 0.42, 0.75), 'roof_purple': (0.5, 0.3, 0.7), 'wood': (0.55, 0.36, 0.2), 'grass': (0.42, 0.68, 0.26),
    'road': (0.8, 0.68, 0.5), 'slime': (0.42, 0.86, 0.38), 'beetle': (0.32, 0.26, 0.55), 'golem': (0.55, 0.5, 0.46),
    'eye': (1, 1, 1), 'pupil': (0.08, 0.05, 0.08), 'fire': (1.0, 0.5, 0.15), 'frost': (0.55, 0.88, 1.0), 'storm': (0.75, 0.5, 1.0),
    'plaster': (0.96, 0.9, 0.78), 'water': (0.35, 0.7, 0.95), 'skin': (0.96, 0.75, 0.6), 'cloth': (0.3, 0.5, 0.8),
}
_mats = {}
def mat(name, glow=0.0):
    key = (name, glow)
    if key in _mats: return _mats[key]
    m = bpy.data.materials.new(f'HG_{name}' + ('_glow' if glow else ''))
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get('Principled BSDF')
    c = PAL[name] + (1.0,)
    bsdf.inputs['Base Color'].default_value = c
    bsdf.inputs['Roughness'].default_value = 0.8
    if glow:
        bsdf.inputs['Emission Color'].default_value = c
        bsdf.inputs['Emission Strength'].default_value = glow
    m.diffuse_color = c
    _mats[key] = m
    return m

def clear():
    bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete()
    for block in (bpy.data.meshes, bpy.data.curves):
        for x in list(block):
            if x.users == 0: block.remove(x)

def finish(o, m, bevel=0.0, smooth=True):
    o.data.materials.clear(); o.data.materials.append(m)
    if bevel > 0:
        b = o.modifiers.new('bevel', 'BEVEL'); b.width = bevel; b.segments = 2; b.limit_method = 'ANGLE'
    if smooth:
        for p in o.data.polygons: p.use_smooth = True
        if hasattr(o.data, 'set_sharp_from_angle'): o.data.set_sharp_from_angle(angle=math.radians(40))
    return o

def cube(name, size, loc, m, bevel=0.04):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc); o = bpy.context.object; o.name = name; o.scale = size
    bpy.ops.object.transform_apply(scale=True); return finish(o, m, bevel)
def cyl(name, r, h, loc, m, verts=16, bevel=0.03, r2=None):
    if r2 is None: bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=h, location=loc)
    else: bpy.ops.mesh.primitive_cone_add(vertices=verts, radius1=r, radius2=r2, depth=h, location=loc)
    o = bpy.context.object; o.name = name; return finish(o, m, bevel)
def cone(name, r, h, loc, m, verts=16): return cyl(name, r, h, loc, m, verts, 0.0, r2=0.0)
def sphere(name, r, loc, m, scale=(1, 1, 1), sub=3):
    bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=sub, radius=r, location=loc); o = bpy.context.object; o.name = name; o.scale = scale
    bpy.ops.object.transform_apply(scale=True); return finish(o, m)

def export(name):
    objs = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    for o in objs:
        for md in list(o.modifiers): bpy.context.view_layer.objects.active = o; bpy.ops.object.modifier_apply(modifier=md.name)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    o = bpy.context.object; o.name = name
    bpy.context.scene.cursor.location = (0, 0, 0); bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    path = os.path.join(OUT, name + '.fbx')
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
                             bake_space_transform=True, object_types={'MESH'}, mesh_smooth_type='FACE', path_mode='COPY', embed_textures=False)
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    print(f'[hg_kit] {name}.fbx  {tris} tris')
    clear()
    return tris

def crenels(r, z, n, m, size=0.45):
    for i in range(n):
        a = i / n * math.tau
        cube('cren', (size, size, size * 1.1), (math.cos(a) * r, math.sin(a) * r, z + size * 0.55), m, 0.03)

# Blender is Z up; exported with Y up for Unity. Footprints below are in Blender X (right) and Y (toward the waves).

def ground():
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=40, y_subdivisions=48, size=1, location=(0, 0, 0))
    o = bpy.context.object; o.scale = (70, 84, 1); bpy.ops.object.transform_apply(scale=True)
    o.location.y = 12
    for v in o.data.vertices:
        x, y = v.co.x, v.co.y + 12
        if abs(x) > 9 or y < -2: v.co.z = 0.25 * math.sin(x * 0.35) * math.cos(y * 0.27) + (0.4 if abs(x) > 14 else 0)
    finish(o, mat('grass'))
    for i in range(70):   # clumps of shrubs at the edges of the meadow, never on the road
        x = random.choice([-1, 1]) * random.uniform(10, 30); y = random.uniform(-14, 42)
        sphere('shrub', random.uniform(0.6, 1.3), (x, y, 0.3), mat('grass'), (1, 1, 0.7), 2)
    return export('ground')

def road():
    bpy.ops.mesh.primitive_grid_add(x_subdivisions=2, y_subdivisions=20, size=1, location=(0, 21, 0.03))
    o = bpy.context.object; o.scale = (5.2, 42, 1); bpy.ops.object.transform_apply(scale=True); finish(o, mat('road'), smooth=False)
    for i in range(24):
        cube('kerb', (0.5, 1.6, 0.18), (2.75 * (1 if i % 2 else -1), 1 + (i // 2) * 3.4, 0.08), mat('stone_dk'), 0.05)
    return export('road')

def wall():
    cube('wall', (18, 1.2, 2.8), (0, 0, 1.4), mat('stone'), 0.06)
    for i in range(-8, 9):
        if abs(i) < 2: continue
        cube('merlon', (0.6, 1.3, 0.6), (i * 1.0, 0, 3.1), mat('stone'), 0.04)
    return export('wall')

def gate():
    for s in (-1, 1):   # two drum towers with gold domes
        cyl('drum', 1.4, 6, (s * 2.4, 0, 3), mat('stone'), 20, 0.05)
        crenels(1.4, 6, 10, mat('stone'))
        sphere('dome', 1.15, (s * 2.4, 0, 6.9), mat('gold'), (1, 1, 0.9), 3)
        cone('finial', 0.18, 0.9, (s * 2.4, 0, 8.3), mat('gold'), 8)
    cube('lintel', (3.6, 1.6, 2.2), (0, 0, 4.6), mat('stone'), 0.06)
    cube('door_l', (1.5, 0.25, 3.4), (-0.78, -0.55, 1.7), mat('wood'), 0.04)
    cube('door_r', (1.5, 0.25, 3.4), (0.78, -0.55, 1.7), mat('wood'), 0.04)
    for k in range(3): cube('band', (3.2, 0.3, 0.16), (0, -0.7, 0.7 + k * 1.1), mat('gold'), 0.02)
    sphere('gem', 0.32, (0, -0.85, 4.8), mat('storm', 2.0), (1, 0.6, 1.2), 2)
    return export('gate')

def tower_base():
    cyl('tower', 1.25, 4.2, (0, 0, 2.1), mat('stone'), 18, 0.05)
    cyl('lip', 1.45, 0.35, (0, 0, 4.1), mat('stone_dk'), 18, 0.04)
    for k in range(4): cube('slit', (0.18, 0.1, 0.6), (math.cos(k * 1.57) * 1.25, math.sin(k * 1.57) * 1.25, 2.6), mat('pupil'), 0.0)
    return export('tower_base')

def top(kind):
    cyl('plinth', 1.1, 0.4, (0, 0, 0.2), mat('stone_dk'), 16, 0.04)
    if kind == 'stone':
        crenels(1.0, 0.4, 8, mat('stone'))
    elif kind == 'fire':   # a brazier of hot coals and a tongue of flame
        cyl('bowl', 0.95, 0.6, (0, 0, 0.7), mat('wood'), 16, 0.05, r2=0.6)
        for i in range(5): sphere('coal', 0.3, (math.cos(i) * 0.4, math.sin(i) * 0.4, 1.05), mat('fire', 3.0), (1, 1, 0.7), 2)
        cone('flame', 0.55, 1.5, (0, 0, 1.8), mat('fire', 4.0), 12)
    elif kind == 'frost':   # a cluster of ice crystals
        for i, (r, h, a) in enumerate([(0.35, 2.0, 0), (0.25, 1.4, 1.3), (0.25, 1.2, 2.6), (0.22, 1.1, 4.0), (0.2, 0.9, 5.2)]):
            o = cyl('crystal', r, h, (math.cos(a) * (0 if i == 0 else 0.45), math.sin(a) * (0 if i == 0 else 0.45), 0.4 + h / 2), mat('frost', 1.5), 6, 0.0)
            tip = cone('tip', r, r * 1.6, (o.location.x, o.location.y, 0.4 + h + r * 0.8), mat('frost', 1.5), 6)
            if i: o.rotation_euler = (math.cos(a) * 0.3, math.sin(a) * 0.3, 0); tip.rotation_euler = o.rotation_euler
    elif kind == 'storm':   # a bell on a frame, a rod crackling above it
        for s in (-1, 1): cube('post', (0.2, 0.2, 1.8), (s * 0.7, 0, 1.3), mat('wood'), 0.03)
        cube('beam', (1.7, 0.25, 0.25), (0, 0, 2.2), mat('wood'), 0.03)
        cyl('bell', 0.5, 0.8, (0, 0, 1.6), mat('gold'), 16, 0.03, r2=0.3)
        cyl('rod', 0.06, 1.2, (0, 0, 2.9), mat('gold'), 6, 0.0)
        sphere('orb', 0.28, (0, 0, 3.6), mat('storm', 3.0), sub=2)
    return export('top_' + kind)

def eyes(z, y, spread, r=0.17):
    for s in (-1, 1):
        sphere('eye', r, (s * spread, -y, z), mat('eye'), sub=2)
        sphere('pupil', r * 0.55, (s * spread, -y - r * 0.7, z), mat('pupil'), sub=2)

def slime():   # 1.5 m tall, a happy blob; faces -Y (toward the gate in Unity once turned)
    sphere('body', 0.8, (0, 0, 0.65), mat('slime'), (1.1, 1.1, 0.95), 4)
    sphere('drip', 0.25, (0.5, -0.3, 0.2), mat('slime'), (1, 1, 0.6), 2)
    eyes(0.95, 0.78, 0.28); return export('slime')

def beetle():
    sphere('shell', 0.7, (0, 0.1, 0.6), mat('beetle'), (0.9, 1.3, 0.75), 3)
    sphere('head', 0.4, (0, -0.85, 0.55), mat('pupil'), sub=2)
    cone('horn', 0.14, 0.6, (0, -1.15, 0.85), mat('gold'), 8)
    for s in (-1, 1):
        for k in range(3): cube('leg', (0.12, 0.12, 0.6), (s * 0.7, -0.4 + k * 0.45, 0.25), mat('pupil'), 0.02)
    eyes(0.65, 1.12, 0.17, 0.11); return export('beetle')

def golem():
    cube('torso', (1.2, 0.9, 1.0), (0, 0, 0.95), mat('golem'), 0.12)
    cube('head', (0.6, 0.55, 0.45), (0, -0.1, 1.65), mat('golem'), 0.08)
    for s in (-1, 1):
        cube('arm', (0.38, 0.4, 0.95), (s * 0.85, 0, 0.75), mat('golem'), 0.1)
        cube('leg', (0.4, 0.45, 0.5), (s * 0.32, 0, 0.25), mat('stone_dk'), 0.08)
    sphere('core', 0.15, (0, -0.47, 1.0), mat('storm', 3.0), sub=2)
    eyes(1.7, 0.36, 0.14, 0.09); return export('golem')

def ruin():
    cube('slab', (2.6, 2.2, 0.25), (0, 0, 0.12), mat('stone_dk'), 0.04)
    cube('wall_a', (2.2, 0.3, 1.1), (0, 0.9, 0.55), mat('stone'), 0.05)
    cube('wall_b', (0.3, 1.2, 0.7), (-1.0, 0.2, 0.35), mat('stone'), 0.05)
    for i in range(5): cube('rubble', (0.4, 0.35, 0.3), (random.uniform(-0.9, 0.9), random.uniform(-0.8, 0.4), 0.3), mat('stone_dk'), 0.06)
    for i in range(3): cube('beam', (0.15, 1.4, 0.15), (random.uniform(-0.6, 0.6), random.uniform(-0.5, 0.5), 0.35), mat('wood'), 0.02).rotation_euler = (0, random.uniform(-0.5, 0.5), random.uniform(0, 3))
    return export('ruin')

ROOF = {'forge': 'roof_red', 'frostSpire': 'roof_blue', 'stormBell': 'roof_purple', 'bakery': 'roof_red', 'market': 'roof_red',
        'tavern': 'roof_red', 'granary': 'wood', 'library': 'roof_blue', 'cottages': 'roof_blue', 'watchtower': 'roof_purple'}
def building(bid):
    tall = bid in ('watchtower', 'frostSpire', 'stormBell')
    cube('base', (2.6, 2.2, 0.3), (0, 0, 0.15), mat('stone_dk'), 0.04)
    if tall:
        cyl('tower', 0.95, 3.6, (0, 0, 2.1), mat('plaster'), 16, 0.04)
        cone('roof', 1.25, 1.8, (0, 0, 4.8), mat(ROOF[bid]), 16)
    else:
        cube('house', (2.3, 1.9, 1.7), (0, 0, 1.15), mat('plaster'), 0.05)
        bpy.ops.mesh.primitive_cone_add(vertices=4, radius1=1.85, depth=1.3, location=(0, 0, 2.65)); r = bpy.context.object
        r.rotation_euler = (0, 0, math.pi / 4); r.scale = (1, 0.82, 1); finish(r, mat(ROOF[bid]), 0.03, smooth=False)
        cube('door', (0.55, 0.12, 0.9), (0, -0.98, 0.75), mat('wood'), 0.02)
        for s in (-1, 1): cube('window', (0.4, 0.1, 0.4), (s * 0.7, -0.97, 1.4), mat('gold', 0.8), 0.02)
    sign = {'forge': 'fire', 'frostSpire': 'frost', 'stormBell': 'storm'}.get(bid)
    if bid == 'forge': cyl('chimney', 0.25, 1.2, (0.7, 0.4, 3.1), mat('stone_dk'), 8, 0.02); sphere('ember', 0.2, (0.7, 0.4, 3.8), mat('fire', 3), sub=2)
    if sign and bid != 'forge': sphere('glow', 0.3, (0, 0, 5.9 if tall else 3.5), mat(sign, 3.0), sub=2)
    if bid == 'watchtower': sphere('beacon', 0.35, (0, 0, 5.9), mat('gold', 3.0), sub=2)
    if bid == 'market':
        for s in (-1, 1): cube('stall', (0.8, 0.6, 0.6), (s * 1.6, -0.6, 0.45), mat('wood'), 0.03)
    if bid == 'granary': cyl('silo', 0.6, 2.4, (1.4, 0.3, 1.2), mat('wood'), 12, 0.03); cone('silo_roof', 0.75, 0.7, (1.4, 0.3, 2.75), mat('roof_red'), 12)
    return export('building_' + bid)

def fountain():
    cyl('basin', 1.6, 0.5, (0, 0, 0.25), mat('stone'), 24, 0.05)
    cyl('water', 1.4, 0.1, (0, 0, 0.45), mat('water', 0.4), 24, 0.0)
    cyl('column', 0.25, 1.4, (0, 0, 0.9), mat('stone'), 12, 0.03)
    cyl('bowl', 0.7, 0.3, (0, 0, 1.6), mat('stone'), 16, 0.03, r2=0.4)
    sphere('spout', 0.22, (0, 0, 2.0), mat('water', 0.8), sub=2)
    return export('fountain')

def villager():   # a 0.9 m chibi; the walk is a rig in the art pass
    sphere('body', 0.28, (0, 0, 0.42), mat('cloth'), (1, 0.9, 1.2), 3)
    sphere('head', 0.24, (0, 0, 0.85), mat('skin'), sub=3)
    for s in (-1, 1): cube('boot', (0.12, 0.18, 0.14), (s * 0.12, -0.03, 0.07), mat('wood'), 0.03)
    return export('villager')

if __name__ == '__main__':
    clear()
    report = {}
    for fn in (ground, road, wall, gate, tower_base, slime, beetle, golem, ruin, fountain, villager):
        report[fn.__name__] = fn()
    for k in ('stone', 'fire', 'frost', 'storm'): report['top_' + k] = top(k)
    for bid in ROOF: report['building_' + bid] = building(bid)
    print('[hg_kit] done', len(report), 'models,', sum(report.values()), 'tris total, into', OUT)
