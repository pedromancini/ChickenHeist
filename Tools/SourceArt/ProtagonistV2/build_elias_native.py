# Elias on the native Medieval People skeleton (same family as every NPC), with his own identity:
# faded cap, stubble, red flannel sleeves under a worn olive shirt, dark work trousers.
# Bones are renamed to the ProtagonistRig convention so gameplay code (hands, fingers, head,
# camera, chicken carry, cinematics) finds them unchanged.
# Run: blender -b --factory-startup --python build_elias_native.py
# Output: Assets/ChickenHeistGenerated/Characters/EliasNative/Elias_Rigged.fbx (+ preview renders)
import bpy, bmesh, os, math
from mathutils import Vector, Matrix

ROOT = os.path.dirname(os.path.abspath(__file__))
PROJECT = os.path.abspath(os.path.join(ROOT, '..', '..', '..'))
SOURCE = os.path.join(PROJECT, 'Assets', 'ImportedMedievalPeople', 'fbx', 'people_unity', 'peasant_3.fbx')
OUT_DIR = os.path.join(PROJECT, 'Assets', 'ChickenHeistGenerated', 'Characters', 'EliasNative')
PREVIEW = os.path.join(ROOT, 'generated'); os.makedirs(OUT_DIR, exist_ok=True); os.makedirs(PREVIEW, exist_ok=True)
ATLAS = os.path.join(PROJECT, 'Assets', 'ChickenHeistGenerated', 'Characters', 'Villagers', 'MedievalAtlas-eliasNative.png')

def band_v(top, bottom):  # atlas rows counted from the top of a 64 px texture -> UV v at band centre
    return 1 - ((top + bottom) / 2) / 64
CAP_V = band_v(36, 40)       # "red" band, repainted as a faded cap colour for Elias
STUBBLE_V = band_v(19, 23)   # "brownGray" band, repainted as stubble

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=SOURCE)
arm = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
mesh = next(o for o in bpy.context.scene.objects if o.type == 'MESH')

# Normalise: 1.78 m tall, feet on the ground, facing -Y like ProtagonistRig.
bpy.context.view_layer.update()
pts = [mesh.matrix_world @ v.co for v in mesh.data.vertices]
lo = min(p.z for p in pts); hi = max(p.z for p in pts)
foot = arm.matrix_world @ arm.data.bones['Foot_L'].head_local; ball = arm.matrix_world @ arm.data.bones['Ball_L'].head_local
fwd = (ball - foot); fwd.z = 0; fwd.normalize()
yaw = math.atan2(fwd.x, -fwd.y)  # rotate so forward becomes -Y
s = 1.78 / (hi - lo)
for o in [arm] + [o for o in bpy.context.scene.objects if o.parent == arm and o.type == 'MESH']:
    pass
arm.matrix_world = Matrix.Rotation(-yaw, 4, 'Z') @ Matrix.Diagonal((s, s, s, 1)) @ Matrix.Translation((0, 0, -lo)) @ arm.matrix_world
bpy.ops.object.select_all(action='SELECT'); bpy.context.view_layer.objects.active = arm
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

# Bone names: ProtagonistRig convention.
rename = {'Pelvis': 'Hips', 'Spine_01': 'Spine', 'Spine_02': 'Spine2', 'Spine_03': 'Chest', 'Neck_01': 'Neck', 'Head': 'Head'}
for side in 'LR':
    rename.update({f'Clavicle_{side}': f'Shoulder{side}', f'Upperarm_{side}': f'UpperArm{side}', f'Lowerarm_{side}': f'Forearm{side}',
                   f'Hand_{side}': f'Hand{side}', f'Thigh_{side}': f'Thigh{side}', f'Calf_{side}': f'Shin{side}',
                   f'Foot_{side}': f'Foot{side}', f'Ball_{side}': f'Toe{side}'})
    for f in ('Thumb', 'Index', 'Ring'):
        for j in (1, 2, 3): rename[f'{f}_0{j}_{side}'] = f'{f}{j}{side}'
for b in arm.data.bones:
    if b.name in rename: b.name = rename[b.name]   # vertex groups follow bone renames
arm.name = 'ProtagonistRig'; arm.data.name = 'ProtagonistRig'

# --- identity: stubble on chin and jaw (front, below the mouth), recoloured through the atlas
me = mesh.data; uv = me.uv_layers.active.data
head_group = mesh.vertex_groups['Head'].index
def head_w(v): return sum(g.weight for g in me.vertices[v].groups if g.group == head_group)
head_pts = [me.vertices[i].co for i in range(len(me.vertices)) if head_w(i) > .5]
hz0 = min(p.z for p in head_pts); hz1 = max(p.z for p in head_pts); hy = sum(p.y for p in head_pts) / len(head_pts)
for poly in me.polygons:
    vs = list(poly.vertices)
    if min(head_w(v) for v in vs) < .5: continue
    c = sum((me.vertices[v].co for v in vs), Vector()) / len(vs)
    vband = sum(uv[l].uv.y for l in poly.loop_indices) / poly.loop_total
    skin = 1 - 19 / 64 <= vband <= 1 - 11 / 64
    if skin and c.z < hz0 + (hz1 - hz0) * .30 and c.y < hy:
        for l in poly.loop_indices: uv[l].uv = (uv[l].uv.x, STUBBLE_V)

# --- identity: faded cap, weighted to the head
bpy.context.view_layer.update()
top = hz1; cx = sum(p.x for p in head_pts) / len(head_pts)
radius = max(max(p.x for p in head_pts) - min(p.x for p in head_pts), max(p.y for p in head_pts) - min(p.y for p in head_pts)) * .56
bm = bmesh.new()
bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=6, radius=radius)
for v in list(bm.verts):
    if v.co.z < -radius * .05: bm.verts.remove(v)           # dome only
for v in bm.verts: v.co.z *= .78; v.co += Vector((cx, hy + radius * .05, top - radius * .52))
# visor: thin half-moon brim attached to the front rim of the dome, drooping slightly
centre = Vector((cx, hy + radius * .05, top - radius * .52))
rim = [centre + Vector((math.sin(a) * radius, -math.cos(a) * radius, 0)) for a in [math.radians(x) for x in range(-80, 81, 20)]]
tips = [centre + Vector((math.sin(a) * radius * .95, -math.cos(a) * radius * 1.75, -radius * .16)) for a in [math.radians(x) for x in range(-80, 81, 20)]]
for thickness in (0, -.012):
    ring = [bm.verts.new(p + Vector((0, 0, thickness))) for p in rim]
    edge = [bm.verts.new(p + Vector((0, 0, thickness))) for p in tips]
    for i in range(len(ring) - 1):
        face = [ring[i], ring[i + 1], edge[i + 1], edge[i]]
        bm.faces.new(face if thickness == 0 else list(reversed(face)))
cap_me = bpy.data.meshes.new('Cap'); bm.to_mesh(cap_me); bm.free()
cap = bpy.data.objects.new('Cap', cap_me); bpy.context.scene.collection.objects.link(cap)
cap_uv = cap_me.uv_layers.new()
for l in cap_uv.data: l.uv = (.5, CAP_V)
cap.data.materials.append(me.materials[0])
cap.vertex_groups.new(name='Head').add(list(range(len(cap_me.vertices))), 1.0, 'REPLACE')
# join the cap into the body mesh so it shares the skin and material
bpy.ops.object.select_all(action='DESELECT'); cap.select_set(True); mesh.select_set(True); bpy.context.view_layer.objects.active = mesh
bpy.ops.object.join()

# Split head (hidden in first person) from body.
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.select_all(action='DESELECT'); bpy.ops.object.mode_set(mode='OBJECT')
head_group = mesh.vertex_groups['Head'].index
for poly in mesh.data.polygons:
    poly.select = all(sum(g.weight for g in mesh.data.vertices[v].groups if g.group == head_group) > .5 for v in poly.vertices)
bpy.ops.object.mode_set(mode='EDIT'); bpy.ops.mesh.separate(type='SELECTED'); bpy.ops.object.mode_set(mode='OBJECT')
parts = [o for o in bpy.context.scene.objects if o.type == 'MESH']
head = max(parts, key=lambda o: min((o.matrix_world @ v.co).z for v in o.data.vertices))
body = next(o for o in parts if o != head)
head.name = head.data.name = 'ProtagonistHead'; body.name = body.data.name = 'ProtagonistBody'
for o in (head, body):
    for p in o.data.polygons: p.use_smooth = False

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT_DIR, 'Elias_Rigged.fbx'), use_selection=True, object_types={'MESH', 'ARMATURE'},
                         add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y')
print('ELIAS NATIVE exported', len(body.data.vertices), len(head.data.vertices))

# Preview with the Elias atlas (if already generated by CastPalette).
if os.path.exists(ATLAS):
    mat = bpy.data.materials.new('preview'); mat.use_nodes = True
    tex = mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(ATLAS); tex.interpolation = 'Closest'
    mat.node_tree.links.new(tex.outputs[0], next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED').inputs[0])
    for o in (head, body): o.data.materials.clear(); o.data.materials.append(mat)
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x = 900; sc.render.resolution_y = 1100
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
for label, angle, h, d, look, lens in [('native_full', 25, 1.0, 4.4, .92, 50), ('native_face', 20, 1.62, 1.1, 1.58, 70)]:
    a = math.radians(angle); cam.data.lens = lens
    cam.location = Vector((math.sin(a) * d, -math.cos(a) * d, h))
    cam.rotation_euler = (Vector((0, 0, look)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(PREVIEW, 'elias_' + label + '.png'); bpy.ops.render.render(write_still=True)
