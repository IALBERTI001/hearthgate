# Hearthgate 3D: the game's view rendered in Blender, for fast art reviews between Unity builds (Nov 13 2026).
# Imports the exported models (Assets/Art/Models/*.fbx) and places them where the game does (numbers from
# Assets/Scripts/Game/Field.cs), then renders from the game camera at 1280x720. The QA loop reviews these frames, then
# the real WebGL build. Usage: blender -b -P hg_scene.py -- <models dir> <out.png> [hud] [state]
#   hud: 1 draws the HUD's boxes over a second copy (<out>-hud.png) for the layout pass; never in review frames.
#   state: 'mid' (some buildings rebuilt, three elements, a wave on the road) or 'fresh' (all ruins, stone tops).
import bpy, sys, os, math, glob
from mathutils import Vector, Euler

a = sys.argv[sys.argv.index('--') + 1:]
MODELS, OUT = a[0], a[1]
HUD = len(a) > 2 and a[2] == '1'
STATE = a[3] if len(a) > 3 else 'mid'

# ---- Field.cs, mirrored (Unity x, z) -> Blender (x, y)
TOWER_X = [-7.5, -13.0, -18.5, -24.0]; TOWER_TOP = 6.3
PLAZA = (0.0, -10.0)
PLOTS = [(7, -6), (13, -6), (17.5, -10), (-18.5, -10.5), (-12.5, -10.5), (-6.5, -10.5), (10, -11), (5, -14), (-9.5, -15.5), (-3.5, -15.5)]
IDS = ['forge', 'frostSpire', 'stormBell', 'bakery', 'market', 'tavern', 'granary', 'library', 'cottages', 'watchtower']
ROAD_Z = 5.4
CAM_LOOK = Vector((-7.5, -3.5, 0.0)); CAM_POS = CAM_LOOK + Vector((0, 46 * 0.7660, 46 * 0.6428)); CAM_FOV = 38.0

bpy.ops.wm.read_factory_settings(use_empty=True)
sc = bpy.context.scene

def place(name, loc, yaw_deg=0.0, scale=1.0):
    path = os.path.join(MODELS, name + '.fbx')
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    new = [o for o in bpy.data.objects if o not in before]
    for o in new:
        if o.parent is None:
            # the export turned models 180 degrees for Unity; the importer undoes the axes, so turn them back here
            o.rotation_euler.rotate(Euler((0, 0, math.pi + math.radians(-yaw_deg))))
            o.location = Vector(loc); o.scale = o.scale * scale   # keep whatever unit scale the importer set
    return new

place('ground', (0, 0, 0)); place('road', (0, 0, 0)); place('wall', (0, 0, 0)); place('gate', (0, 0, 0))
place('fountain', (PLAZA[0], PLAZA[1], 0))
els = ['fire', 'frost', 'storm', 'fire'] if STATE == 'mid' else ['stone'] * 4
for x, el in zip(TOWER_X, els):
    place('tower_base', (x, 0, 0)); place('top_' + el, (x, 0, TOWER_TOP))
built = {'forge', 'frostSpire', 'stormBell', 'bakery', 'cottages'} if STATE == 'mid' else set()
for (x, y), bid in zip(PLOTS, IDS):
    place('building_' + bid if bid in built else 'ruin', (x, y, 0), 180)   # Field.PLOT_FACING: fronts to the camera
if STATE == 'mid':
    # a wave on the road: slimes walking toward the gate three-quarters to the camera (Field.WALK = 240 deg), a few biting
    for i, p in enumerate((3.0, 6.5, 9.0, 12.5, 16.0, 19.5, 23.0)):
        lat = (-1.6, 1.2, -0.4, 1.8, -1.9, 0.6, -0.8)[i]
        place('slime', (-p, ROAD_Z + lat, 0), 240, 1.2)
    for p, lat in ((-0.5, -2.3), (1.2, -2.2)):
        place('slime', (-p, ROAD_Z + lat, 0), 200, 1.2)
    place('beetle', (-14.5, ROAD_Z + 0.5, 0), 240, 1.1); place('golem', (-21.0, ROAD_Z - 0.6, 0), 240, 1.2)

# ---- light like the game: a warm sun from the camera's front right (world -x, +y, high), a soft blue sky
sun = bpy.data.lights.new('sun', 'SUN'); sun.energy = 3.2; sun.angle = math.radians(8); sun.color = (1.0, 0.93, 0.82)
so = bpy.data.objects.new('sun', sun); sc.collection.objects.link(so)
d = Vector((0.45, -0.4, -0.8)).normalized()                    # the direction the light travels (Blender axes)
so.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
w = bpy.data.worlds.new('sky'); sc.world = w; w.use_nodes = True
bg = w.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (0.55, 0.7, 0.92, 1); bg.inputs['Strength'].default_value = 0.9

cam = bpy.data.cameras.new('cam'); cam.sensor_fit = 'VERTICAL'; cam.angle_y = math.radians(CAM_FOV); cam.clip_end = 300
co = bpy.data.objects.new('cam', cam); sc.collection.objects.link(co); sc.camera = co
co.location = CAM_POS; co.rotation_euler = (CAM_LOOK - CAM_POS).to_track_quat('-Z', 'Y').to_euler()
if os.environ.get('HG_TOP'):   # a plan view of the whole layout, for finding stray geometry (north up)
    cam.type = 'ORTHO'; cam.ortho_scale = 90; co.location = (-8, -2, 80); co.rotation_euler = (0, 0, math.pi)

eng = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
sc.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in eng else 'BLENDER_EEVEE_NEXT'
sc.render.resolution_x, sc.render.resolution_y = 1280, 720
sc.view_settings.view_transform = 'Standard'
sc.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print('[hg_scene] wrote', OUT)

if HUD:   # the HUD's fixed boxes (Hud.cs at 1280x720), drawn over a copy for the layout pass only
    import numpy as np
    img = bpy.data.images.load(OUT); W, H = img.size
    px = np.array(img.pixels[:]).reshape(H, W, 4)
    def box(x0, y0, x1, y1, col):   # pixel box from the top-left
        r0, r1 = H - y1, H - y0
        px[r0:r1, x0:x1, :3] = px[r0:r1, x0:x1, :3] * 0.5 + np.array(col) * 0.5
    box(0, 0, 300, 140, (1, 0, 0)); box(W - 324, 24, W - 24, 172, (1, 0, 0)); box(W // 2 - 180, H - 49, W // 2 + 180, H - 31, (1, 0, 0))
    img.pixels[:] = px.ravel(); img.filepath_raw = OUT.replace('.png', '-hud.png'); img.file_format = 'PNG'; img.save()
    print('[hg_scene] wrote', img.filepath_raw)
