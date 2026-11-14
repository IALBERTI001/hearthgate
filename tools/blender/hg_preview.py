# Render a contact sheet of the exported kit (Assets/Art/Models/*.fbx), each model imported back the way Unity reads it,
# on a grid with a 1 m reference cube, from the game camera's angle. Usage: blender -b -P hg_preview.py -- <models dir> <out.png>
import bpy, sys, os, math, glob
a = sys.argv[sys.argv.index('--') + 1:]
M, OUT = a[0], a[1]
bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene
files = sorted(glob.glob(os.path.join(M, '*.fbx')))
skip = {'ground', 'road', 'wall'}
files = [f for f in files if os.path.splitext(os.path.basename(f))[0] not in skip]
cols = 6; gap = 9.0
for i, f in enumerate(files):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=f)
    new = [o for o in bpy.data.objects if o not in before]
    x, y = (i % cols) * gap, -(i // cols) * gap
    for o in new:
        if o.parent is None: o.location.x += x; o.location.y += y
    bpy.ops.object.text_add(location=(x - 3.5, y - 3.8, 0))
    t = bpy.context.object; t.data.body = os.path.splitext(os.path.basename(f))[0]; t.data.size = 0.8
# floor and a 1 m cube for scale
bpy.ops.mesh.primitive_plane_add(size=200, location=(20, -15, -0.01))
bpy.ops.mesh.primitive_cube_add(size=1, location=(-5, 0, 0.5))
# the slime should face -Y in Blender = +Z... we mark Blender +Y (Unity forward after -Z export) with an arrow
bpy.ops.mesh.primitive_cone_add(radius1=0.4, depth=1.2, location=(-5, 2, 0.5), rotation=(-math.pi/2, 0, 0))
sun = bpy.data.lights.new('sun', 'SUN'); sun.energy = 3; so = bpy.data.objects.new('sun', sun); sc.collection.objects.link(so)
so.rotation_euler = (math.radians(50), 0, math.radians(30))
cam = bpy.data.cameras.new('cam'); cam.type = 'ORTHO'; cam.ortho_scale = 62
co = bpy.data.objects.new('cam', cam); sc.collection.objects.link(co); sc.camera = co
co.location = (22.5, -60, 45); co.rotation_euler = (math.radians(55), 0, 0)
w = bpy.data.worlds.new('w'); w.color = (0.5, 0.55, 0.6); sc.world = w
sc.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items] else 'BLENDER_EEVEE_NEXT'
sc.render.resolution_x, sc.render.resolution_y = 1800, 1300
sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print('[hg_preview] wrote', OUT, len(files), 'models')
