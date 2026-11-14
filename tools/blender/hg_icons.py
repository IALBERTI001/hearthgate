# Hearthgate 3D: HUD icons rendered from the game's own models (Nov 14 2026, owner: "Buttons are a mess, doesnt look
# polished"). One art style for the world and the HUD (art-consistency-check): each action button carries a small render
# of what it acts on, not a code-drawn symbol. Usage: blender -b -P hg_icons.py -- <models dir> <out dir>
# Writes <out>/icon_<name>.png, 192 x 192, transparent, three-quarter view from the front, the game's warm key light.
import bpy, sys, os, math
from mathutils import Vector, Matrix

a = sys.argv[sys.argv.index('--') + 1:]
MODELS, OUT = a[0], a[1]
ONLY = a[2].split(',') if len(a) > 2 else None   # optional: just these icons, e.g. for a yaw test
os.makedirs(OUT, exist_ok=True)
# icon name -> (model, yaw in degrees, how much of the frame the model fills)
ICONS = {
    'gate': ('gate', 200, 0.92),
    'rebuild': ('ruin_tavern', 200, 0.95),
    'upgrade': ('building_bakery', 205, 0.95),
    'walk': ('wizard', 205, 0.86),
    'tower': ('tower_base', 200, 0.9),
    'gear': (None, 0, 0.9),   # the settings cog, modelled here: a brass wheel with eight teeth and a dark hub
}

def gear():
    import bmesh
    me = bpy.data.meshes.new('gear'); bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=48, radius1=0.78, radius2=0.78, depth=0.22)
    for k in range(8):
        a = k * math.tau / 8; m = bmesh.ops.create_cube(bm, size=1.0)['verts']
        bmesh.ops.scale(bm, vec=(0.34, 0.36, 0.22), verts=m)
        bmesh.ops.translate(bm, vec=(0, 0.86, 0), verts=m); bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=__import__('mathutils').Matrix.Rotation(a, 3, 'Z'), verts=m)
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.025, segments=2, affect='EDGES')
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new('gear', me); bpy.context.scene.collection.objects.link(o)
    mat = bpy.data.materials.new('brass'); mat.use_nodes = True; b = mat.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (0.82, 0.6, 0.22, 1); b.inputs['Metallic'].default_value = 0.6; b.inputs['Roughness'].default_value = 0.4
    me.materials.append(mat)
    hub = bpy.data.meshes.new('hub'); bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=32, radius1=0.3, radius2=0.3, depth=0.3); bm.to_mesh(hub); bm.free()
    h = bpy.data.objects.new('hub', hub); bpy.context.scene.collection.objects.link(h)
    hm = bpy.data.materials.new('hub'); hm.use_nodes = True; hm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.16, 0.08, 0.19, 1); hub.materials.append(hm)
    for ob in (o, h): ob.rotation_euler = (math.radians(68), 0, 0)   # turned toward the camera, a little tilted

def render(name, model, yaw, fill):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    if model: bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, model + '.fbx'))
    else: gear()
    objs = [o for o in sc.objects if o.type == 'MESH']
    bpy.context.view_layer.update()
    turn = Matrix.Rotation(math.radians(yaw), 4, 'Z')   # about the world's up axis (the FBX roots carry a Y-up correction)
    for o in sc.objects:
        if o.parent is None: o.matrix_world = turn @ o.matrix_world
    bpy.context.view_layer.update()
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts))); hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    centre = (lo + hi) / 2; size = (hi - lo).length
    cam = bpy.data.cameras.new('cam'); cam.type = 'ORTHO'; cam.ortho_scale = size * 0.78 / fill
    co = bpy.data.objects.new('cam', cam); sc.collection.objects.link(co); sc.camera = co
    d = Vector((0.0, -1.0, 0.62)).normalized()   # from the front, a little above
    co.location = centre + d * size * 2; co.rotation_euler = (centre - co.location).to_track_quat('-Z', 'Y').to_euler()
    sun = bpy.data.lights.new('sun', 'SUN'); sun.energy = 3.4; sun.color = (1.0, 0.93, 0.82)
    so = bpy.data.objects.new('sun', sun); sc.collection.objects.link(so)
    so.rotation_euler = Vector((-0.5, 0.45, -0.75)).normalized().to_track_quat('-Z', 'Y').to_euler()
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (0.75, 0.72, 0.85, 1); bg.inputs['Strength'].default_value = 0.8
    eng = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
    sc.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in eng else 'BLENDER_EEVEE_NEXT'
    sc.render.resolution_x = sc.render.resolution_y = 192; sc.render.film_transparent = True
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'
    try: sc.view_settings.view_transform = 'Standard'
    except Exception: pass
    sc.render.filepath = os.path.join(OUT, 'icon_' + name + '.png')
    bpy.ops.render.render(write_still=True)
    print('[hg_icons] wrote', sc.render.filepath)

for n, (m, yaw, fill) in ICONS.items():
    if ONLY is None or n in ONLY:
        if os.environ.get('HG_YAW'):   # a yaw sweep: icon_<name>_<yaw>.png for each
            for y in os.environ['HG_YAW'].split(','): render(n + '_' + y, m, float(y), fill)
        else: render(n, m, yaw, fill)
