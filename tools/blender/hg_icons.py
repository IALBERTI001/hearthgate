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
    'ascend': (None, 0, 0.9),   # Ascend: a rising double chevron over a halo, modelled here
    'starshard': (None, 0, 0.9),   # Starshards: a gold star (owner, Nov 14 2026: "starshards should be the perks logo. Its a star after all")
    'shard': (None, 0, 0.85),   # perk points: the rose crystal (owner: "perks can be the starshards logo")
    'shop': ('building_market', 205, 0.95),   # the Market keeps the Shop
    'quests': ('building_library', 205, 0.95),   # the Library keeps the quests
    'fountain': ('fountain', 200, 0.92),      # the Surge board
    'el_fire': ('top_fire', 200, 0.9), 'el_frost': ('top_frost', 200, 0.9), 'el_storm': ('top_storm', 200, 0.9), 'el_earth': ('top_earth', 200, 0.9),   # the tower card's elements
    'look_fire_obsidian': ('top_fire_obsidian', 200, 0.9), 'look_fire_ember': ('top_fire_ember', 200, 0.9), 'look_frost_night': ('top_frost_night', 200, 0.9),
    'look_frost_aurora': ('top_frost_aurora', 200, 0.9), 'look_storm_void': ('top_storm_void', 200, 0.9), 'look_storm_steel': ('top_storm_steel', 200, 0.9),   # the Market's tower designs
    'timestop': ('hourglass', 200, 0.9),   # Time Stop (the time wizard's skill)
    'npc_astronomer': ('npc_astronomer', 200, 0.95), 'npc_merchant': ('npc_merchant', 200, 0.95), 'npc_librarian': ('npc_librarian', 200, 0.95), 'npc_chronomancer': ('npc_chronomancer', 200, 0.95),   # portraits in the conversation card
    'npc_steward': ('npc_steward', 200, 0.95), 'ledger': ('steward_booth', 205, 0.95),   # the steward and his ledger (Nov 14 2026)
    **{'b_' + b: ('building_' + b, 205, 0.95) for b in ('forge', 'frostSpire', 'stormBell', 'bakery', 'market', 'tavern', 'granary', 'library', 'cottages', 'watchtower')},   # the ledger's rows
    'perks': ('building_watchtower', 205, 0.95),   # the Watchtower keeps the perks  # Ascend: a five-pointed gold star rising from a ring, modelled here
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

def shard():
    import bmesh
    me = bpy.data.meshes.new('shard'); bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=0.42, radius2=0.0, depth=0.9, matrix=__import__('mathutils').Matrix.Translation((0, 0, 0.45)))
    bmesh.ops.create_cone(bm, cap_ends=True, segments=6, radius1=0.42, radius2=0.0, depth=0.5, matrix=__import__('mathutils').Matrix.Translation((0, 0, -0.25)) @ __import__('mathutils').Matrix.Rotation(math.pi, 4, 'X'))
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new('shard', me); bpy.context.scene.collection.objects.link(o)
    mat = bpy.data.materials.new('rose'); mat.use_nodes = True; b = mat.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (1.0, 0.45, 0.8, 1); b.inputs['Roughness'].default_value = 0.15
    em = b.inputs.get('Emission Color') or b.inputs.get('Emission')
    if em: em.default_value = (1.0, 0.4, 0.75, 1)
    if b.inputs.get('Emission Strength'): b.inputs['Emission Strength'].default_value = 0.5
    me.materials.append(mat); o.rotation_euler = (0.25, 0.35, 0.5)

def star():
    import bmesh
    from mathutils import Matrix
    me = bpy.data.meshes.new('star'); bm = bmesh.new()
    pts = []
    for k in range(10):   # a fat five-pointed star, extruded and bevelled so it catches the light
        a = math.pi / 2 + k * math.pi / 5; r = 0.95 if k % 2 == 0 else 0.42
        pts.append(bm.verts.new((r * math.cos(a), r * math.sin(a), 0)))
    f = bm.faces.new(pts); ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    bmesh.ops.translate(bm, vec=(0, 0, 0.26), verts=[v for v in ext['geom'] if isinstance(v, bmesh.types.BMVert)])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.04, segments=2, affect='EDGES')
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new('star', me); bpy.context.scene.collection.objects.link(o)
    mat = bpy.data.materials.new('gold'); mat.use_nodes = True; b = mat.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (1.0, 0.72, 0.18, 1); b.inputs['Metallic'].default_value = 0.7; b.inputs['Roughness'].default_value = 0.3
    em = b.inputs.get('Emission Color') or b.inputs.get('Emission')
    if em: em.default_value = (1.0, 0.6, 0.1, 1)
    if b.inputs.get('Emission Strength'): b.inputs['Emission Strength'].default_value = 0.4
    me.materials.append(mat)
    o.rotation_euler = (math.radians(70), 0, 0)

def rising():   # Ascend: two gold chevrons rising out of a glowing halo
    import bmesh
    from mathutils import Matrix
    me = bpy.data.meshes.new('rise'); bm = bmesh.new()
    for k, z in enumerate((-0.15, 0.42)):
        for sx in (-1, 1):   # each chevron: two bevelled bars meeting at a point
            m = bmesh.ops.create_cube(bm, size=1.0)['verts']
            bmesh.ops.scale(bm, vec=(0.72, 0.2, 0.18), verts=m)
            bmesh.ops.rotate(bm, cent=(0, 0, 0), matrix=Matrix.Rotation(sx * math.radians(38), 3, 'Y'), verts=m)
            bmesh.ops.translate(bm, vec=(sx * 0.27, 0, z), verts=m)
    bmesh.ops.bevel(bm, geom=list(bm.edges), offset=0.03, segments=2, affect='EDGES')
    bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new('rise', me); bpy.context.scene.collection.objects.link(o)
    mat = bpy.data.materials.new('gold'); mat.use_nodes = True; b = mat.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (1.0, 0.72, 0.18, 1); b.inputs['Metallic'].default_value = 0.7; b.inputs['Roughness'].default_value = 0.3
    em = b.inputs.get('Emission Color') or b.inputs.get('Emission')
    if em: em.default_value = (1.0, 0.6, 0.1, 1)
    if b.inputs.get('Emission Strength'): b.inputs['Emission Strength'].default_value = 0.4
    me.materials.append(mat)
    bpy.ops.mesh.primitive_torus_add(major_radius=0.62, minor_radius=0.07, location=(0, 0, -0.55))
    t = bpy.context.object; t.scale = (1, 1, 0.5)
    hm = bpy.data.materials.new('halo'); hm.use_nodes = True; hb = hm.node_tree.nodes['Principled BSDF']
    hb.inputs['Base Color'].default_value = (1.0, 0.9, 0.55, 1)
    em = hb.inputs.get('Emission Color') or hb.inputs.get('Emission')
    if em: em.default_value = (1.0, 0.85, 0.4, 1)
    if hb.inputs.get('Emission Strength'): hb.inputs['Emission Strength'].default_value = 1.5
    t.data.materials.append(hm)
    for ob in (o,): ob.rotation_euler = (math.radians(90), 0, 0)

def render(name, model, yaw, fill):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    sc = bpy.context.scene
    if model: bpy.ops.import_scene.fbx(filepath=os.path.join(MODELS, model + '.fbx'))
    elif name.startswith('ascend'): rising()
    elif name.startswith('starshard'): star()
    elif name.startswith('shard'): shard()
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
    cam = bpy.data.cameras.new('cam'); cam.type = 'ORTHO'; cam.ortho_scale = size * 0.78 / fill * 1.12
    co = bpy.data.objects.new('cam', cam); sc.collection.objects.link(co); sc.camera = co
    d = Vector((0.0, -1.0, 0.45)).normalized()   # from the front, a little above (flatter: a clearer silhouette for a glyph)
    co.location = centre + d * size * 2; co.rotation_euler = (centre - co.location).to_track_quat('-Z', 'Y').to_euler()
    sun = bpy.data.lights.new('sun', 'SUN'); sun.energy = 2.6; sun.color = (1.0, 0.93, 0.82)
    so = bpy.data.objects.new('sun', sun); sc.collection.objects.link(so)
    so.rotation_euler = Vector((-0.5, 0.45, -0.75)).normalized().to_track_quat('-Z', 'Y').to_euler()
    w = bpy.data.worlds.new('w'); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (0.75, 0.72, 0.85, 1); bg.inputs['Strength'].default_value = 1.35
    eng = [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items]
    sc.render.engine = 'BLENDER_EEVEE' if 'BLENDER_EEVEE' in eng else 'BLENDER_EEVEE_NEXT'
    sc.render.resolution_x = sc.render.resolution_y = 192; sc.render.film_transparent = True
    sc.render.image_settings.file_format = 'PNG'; sc.render.image_settings.color_mode = 'RGBA'
    try: sc.view_settings.view_transform = 'Standard'
    except Exception: pass
    sc.render.filepath = os.path.join(OUT, 'icon_' + name + '.png')
    for o in objs:   # the painted-glyph look (owner, Nov 15 2026: the icons looked like "pasted pictures"): no shine, no metal
        for ms in o.material_slots:
            m = ms.material
            if m and m.use_nodes:
                for nd in m.node_tree.nodes:
                    if nd.type == 'BSDF_PRINCIPLED':
                        nd.inputs['Roughness'].default_value = 1.0; nd.inputs['Metallic'].default_value = 0.0
                        if nd.inputs.get('Specular IOR Level'): nd.inputs['Specular IOR Level'].default_value = 0.0
    bpy.ops.render.render(write_still=True)
    sticker(sc.render.filepath)
    print('[hg_icons] wrote', sc.render.filepath)

def sticker(path):
    # every icon gets the same thick dark-plum outline round its silhouette and a soft drop below it, so the whole set reads
    # as one family of painted glyphs on the button plates (one outline weight, one light), not cut-out renders
    import numpy as np
    img = bpy.data.images.load(path); W, H = img.size
    px = np.array(img.pixels[:], dtype=np.float32).reshape(H, W, 4)
    a = px[..., 3]
    def dil(m, r):
        out = m.copy()
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if dx * dx + dy * dy <= r * r: out = np.maximum(out, np.roll(np.roll(m, dy, 0), dx, 1))
        return out
    ring = dil(a, 7)
    shadow = np.roll(ring, -5, 0) * 0.45
    ink = np.array([0.105, 0.05, 0.13])
    base = np.zeros_like(px)
    base[..., :3] = ink; base[..., 3] = np.maximum(ring, shadow * (1 - ring))
    base[..., :3] = np.where((ring > 0.01)[..., None], ink, np.array([0.0, 0.0, 0.0]))
    out = base.copy()
    out[..., :3] = px[..., :3] * a[..., None] + base[..., :3] * (1 - a[..., None])
    out[..., 3] = a + base[..., 3] * (1 - a)
    img.pixels[:] = out.ravel(); img.filepath_raw = path; img.file_format = 'PNG'; img.save()

for n, (m, yaw, fill) in ICONS.items():
    if ONLY is None or n in ONLY:
        if os.environ.get('HG_YAW'):   # a yaw sweep: icon_<name>_<yaw>.png for each
            for y in os.environ['HG_YAW'].split(','): render(n + '_' + y, m, float(y), fill)
        else: render(n, m, yaw, fill)
