# Hearthgate 3D: HUD icons rendered from the game's own models (Nov 14 2026, owner: "Buttons are a mess, doesnt look
# polished"). One art style for the world and the HUD (art-consistency-check): each action button carries a small render
# of what it acts on, not a code-drawn symbol. Usage: blender -b -P hg_icons.py -- <models dir> <out dir>
# Writes <out>/icon_<name>.png, 192 x 192, transparent, three-quarter view from the front, the game's warm key light.
import bpy, sys, os, math
from mathutils import Vector

a = sys.argv[sys.argv.index('--') + 1:]
MODELS, OUT = a[0], a[1]
os.makedirs(OUT, exist_ok=True)
# icon name -> (model, yaw in degrees, how much of the frame the model fills)
ICONS = {
    'gate': ('gate', 200, 0.92),
    'rebuild': ('ruin_tavern', 200, 0.95),
    'upgrade': ('building_bakery', 200, 0.95),
    'walk': ('wizard', 205, 0.86),
    'tower': ('tower_base', 200, 0.9),
}

def render(name, model, yaw, fill):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, model + '.fbx'))
    objs = [o for o in sc.objects if o.type == 'MESH']
    for o in sc.objects:
        if o.parent is None: o.rotation_euler.rotate_axis('Z', math.radians(yaw))
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

for n, (m, yaw, fill) in ICONS.items(): render(n, m, yaw, fill)
