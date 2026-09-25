# Low-poly pass for the protagonist so he matches the flat-shaded palette style of the NPCs.
# Run: blender -b Protagonist_Rigged.blend --python lowpoly_protagonist.py
# Output: generated/lowpoly_protagonist.json (+ preview renders). The .blend itself is not saved.
import bpy, bmesh, os, json, math
import numpy as np
from mathutils import Vector

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, 'generated'); os.makedirs(OUT, exist_ok=True)
TARGETS = {'ProtagonistBody': 5200, 'ProtagonistHead': 2600}
PALETTE_SIZE = 28
HAND_BONES = ('Hand', 'Index', 'Middle', 'Ring', 'Little', 'Thumb')

def texture_pixels(mat):
    for n in mat.node_tree.nodes:
        if n.type == 'TEX_IMAGE' and n.image and n.image.size[0] > 0:
            w, h = n.image.size
            return np.array(n.image.pixels[:], dtype=np.float32).reshape(h, w, 4)[:, :, :3]
    raise RuntimeError('no texture')

def sample(pixels, uv):
    h, w, _ = pixels.shape
    x = int((uv[0] % 1.0) * (w - 1)); y = int((uv[1] % 1.0) * (h - 1))
    return pixels[y, x]

rig = bpy.data.objects['ProtagonistRig']
result = {'meshes': {}}
all_colors = []; per_mesh = {}
for name, target in TARGETS.items():
    obj = bpy.data.objects[name]
    pixels = texture_pixels(obj.material_slots[0].material)
    # Protect hands and fingers: they carry the grip poses and were rebuilt by hand.
    guard = obj.vertex_groups.new(name='decimate_guard')
    hand_groups = {g.index for g in obj.vertex_groups if g.name.startswith(HAND_BONES)}
    for v in obj.data.vertices:
        w = sum(g.weight for g in v.groups if g.group in hand_groups)
        guard.add([v.index], 0.0 if w > 0.3 else 1.0, 'REPLACE')
    bpy.context.view_layer.objects.active = obj
    ratio = min(1.0, target / len(obj.data.polygons))
    mod = obj.modifiers.new('lowpoly', 'DECIMATE'); mod.decimate_type = 'COLLAPSE'; mod.ratio = ratio
    mod.vertex_group = guard.name; mod.vertex_group_factor = 1.0; mod.use_collapse_triangulate = True
    # Decimate must run before the armature so it works on the rest pose.
    while obj.modifiers[0].name != 'lowpoly':
        bpy.ops.object.modifier_move_up(modifier='lowpoly')
    bpy.ops.object.modifier_apply(modifier='lowpoly')
    bm = bmesh.new(); bm.from_mesh(obj.data); bmesh.ops.triangulate(bm, faces=bm.faces[:]); bm.to_mesh(obj.data); bm.free()
    mesh = obj.data; uv = mesh.uv_layers.active.data
    colors = []
    for poly in mesh.polygons:
        uvs = [Vector(uv[i].uv) for i in poly.loop_indices]
        c = (uvs[0] + uvs[1] + uvs[2]) / 3
        pts = [c] + [(c + u) / 2 for u in uvs]
        colors.append(np.mean([sample(pixels, p) for p in pts], axis=0))
    per_mesh[name] = (obj, np.array(colors))
    all_colors.append(np.array(colors))

# k-means palette shared by head and body.
data = np.concatenate(all_colors)
rng = np.random.default_rng(7)
centers = data[rng.choice(len(data), PALETTE_SIZE, replace=False)]
for _ in range(40):
    labels = np.argmin(((data[:, None, :] - centers[None]) ** 2).sum(-1), axis=1)
    for k in range(PALETTE_SIZE):
        members = data[labels == k]
        if len(members): centers[k] = members.mean(0)
result['palette'] = [[float(x) for x in c] for c in centers]  # linear RGB

for name, (obj, colors) in per_mesh.items():
    labels = np.argmin(((colors[:, None, :] - centers[None]) ** 2).sum(-1), axis=1)
    mesh = obj.data
    names = [g.name for g in obj.vertex_groups]
    verts = []
    for v in mesh.vertices:
        ws = sorted(((names[g.group], g.weight) for g in v.groups if names[g.group] in rig.data.bones and g.weight > 0), key=lambda x: -x[1])[:4]
        total = sum(w for _, w in ws) or 1
        verts.append({'p': list(obj.matrix_world @ v.co), 'w': [[b, w / total] for b, w in ws]})
    tris = [[list(p.vertices), int(labels[i])] for i, p in enumerate(mesh.polygons)]
    result['meshes'][name] = {'vertices': verts, 'triangles': tris}
    # Preview colours: flat per-face attribute.
    attr = mesh.color_attributes.new('flat', 'BYTE_COLOR', 'CORNER')
    for i, p in enumerate(mesh.polygons):
        c = centers[labels[i]]
        for li in p.loop_indices: attr.data[li].color = (*[float(x) for x in c], 1)
    print('LOWPOLY', name, 'tris', len(mesh.polygons), 'verts', len(mesh.vertices))

with open(os.path.join(OUT, 'lowpoly_protagonist.json'), 'w') as f: json.dump(result, f)

# Preview renders (workbench, flat shading, attribute colours).
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'; scene.display.shading.color_type = 'VERTEX'
scene.render.resolution_x = 700; scene.render.resolution_y = 1000
for o in bpy.data.objects:
    if o.type == 'MESH' and o.name not in TARGETS: o.hide_render = True
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); scene.collection.objects.link(cam); scene.camera = cam
cam.data.lens = 60
for label, angle, height, dist, look in [('front', 0, 1.0, 3.6, 0.9), ('threequarter', 35, 1.0, 3.6, 0.9), ('face', 20, 1.55, 1.0, 1.52), ('hands', 60, 1.2, 1.4, 1.1)]:
    a = math.radians(angle)
    cam.location = Vector((math.sin(a) * dist, -math.cos(a) * dist, height))
    direction = Vector((0, 0, look)) - cam.location
    cam.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = os.path.join(OUT, 'lowpoly_' + label + '.png')
    bpy.ops.render.render(write_still=True)
