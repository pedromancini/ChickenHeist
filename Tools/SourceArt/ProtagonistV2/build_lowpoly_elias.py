# Builds a low-poly Elias from the Medieval People pack (same family as every NPC) and fits it to
# the existing ProtagonistRig, so animations, first-person view and cutscenes keep working.
# Run: blender -b Protagonist_Rigged.blend --python build_lowpoly_elias.py
# Output: generated/lowpoly_elias.json and preview renders. The .blend is not saved.
import bpy, os, json, math
from mathutils import Vector, Matrix

ROOT = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(ROOT, 'generated'); os.makedirs(OUT, exist_ok=True)
MODEL = os.environ.get('ELIAS_MODEL', 'peasant_3')
FBX = r'E:\Jogo3D\ChickenHeist\Assets\ImportedMedievalPeople\fbx\people_unity' + '\\' + MODEL + '.fbx'
ATLAS = os.environ.get('ELIAS_ATLAS', r'E:\Jogo3D\ChickenHeist\Assets\ImportedMedievalPeople\texture\people_texture_map.png')

rig = bpy.data.objects['ProtagonistRig']
for o in bpy.data.objects:
    if o.type == 'MESH': o.hide_render = True

before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=FBX)
new = [o for o in bpy.data.objects if o not in before]
src_arm = next(o for o in new if o.type == 'ARMATURE')
src_meshes = [o for o in new if o.type == 'MESH']

def side(name):
    return 'L' if name.endswith('_L') else 'R'
# medieval bone -> (protagonist bone, how the segment is fitted)
MAP = {'Root': ('Hips', 'rigid'), 'Pelvis': ('Hips', 'rigid'), 'Spine_01': ('Spine', 'rigid'), 'Spine_02': ('Spine', 'rigid'),
       'Spine_03': ('Chest', 'rigid'), 'Neck_01': ('Neck', 'rigid'), 'Head': ('Head', 'rigid')}
for s in 'LR':
    MAP.update({'Clavicle_' + s: ('Shoulder' + s, 'rigid'), 'Upperarm_' + s: ('UpperArm' + s, 'stretch'),
                'Lowerarm_' + s: ('Forearm' + s, 'stretch'), 'Hand_' + s: ('Hand' + s, 'hand'),
                'Thigh_' + s: ('Thigh' + s, 'stretch'), 'Calf_' + s: ('Shin' + s, 'stretch'),
                'Foot_' + s: ('Foot' + s, 'rigid'), 'Ball_' + s: ('Foot' + s, 'rigid')})
    for f in ('Thumb', 'Index', 'Ring'):
        for j in (1, 2, 3): MAP[f'{f}_0{j}_{s}'] = ('Hand' + s, 'hand')

def world_bone(arm, name):
    b = arm.data.bones[name]
    return arm.matrix_world @ b.head_local, arm.matrix_world @ b.tail_local

# Global scale: fit the model's height to the rig (1.78 m) before per-segment fitting.
pts = [m.matrix_world @ v.co for m in src_meshes for v in m.data.vertices]
lo = min(p.z for p in pts); hi = max(p.z for p in pts)
scale = 1.78 / (hi - lo)

def facing(arm, foot, ball):
    f = world_bone(arm, ball)[0] - world_bone(arm, foot)[0]; f.z = 0
    return f.normalized()
SRC_FORWARD = facing(src_arm, 'Foot_L', 'Ball_L')
DST_FORWARD = Vector((0, -1, 0))

def frame(head, tail, forward):
    # Orthonormal frame from the bone axis and the character's facing, so vertical bones keep their roll.
    d = (tail - head).normalized()
    ref = forward if abs(d.dot(forward)) < .9 else Vector((0, 0, 1))
    side = d.cross(ref).normalized(); up = side.cross(d).normalized()
    return Matrix((d, side, up)).transposed().to_4x4()

transforms = {}
for mb, (pb, mode) in MAP.items():
    if mb not in src_arm.data.bones: continue
    mh, mt = world_bone(src_arm, mb); ph, pt = world_bone(rig, pb)
    if mode == 'hand':
        # Hand and fingers move rigidly with the medieval hand bone -> placed at the rig's hand.
        mh, mt = world_bone(src_arm, 'Hand_' + side(mb))
    if mode == 'stretch':
        rot_src = frame(mh, mt, SRC_FORWARD); rot_dst = frame(ph, pt, DST_FORWARD)
        rotation = rot_dst @ rot_src.inverted()
    else:
        # Torso, head, hands and feet stay upright: only the facing is aligned, never the bone tilt.
        rotation = SRC_FORWARD.rotation_difference(DST_FORWARD).to_matrix().to_4x4()
    axial = ((pt - ph).length / max(1e-5, (mt - mh).length * scale)) if mode == 'stretch' else 1.0
    # Scale along the source bone axis only, so limb thickness keeps the pack's proportions.
    d = (mt - mh).normalized()
    stretch = Matrix.Identity(3) + (axial - 1) * Matrix(((d.x * d.x, d.x * d.y, d.x * d.z), (d.y * d.x, d.y * d.y, d.y * d.z), (d.z * d.x, d.z * d.y, d.z * d.z)))
    M = Matrix.Translation(ph) @ rotation @ (stretch.to_4x4() * 1) @ Matrix.Diagonal((scale, scale, scale, 1)) @ Matrix.Translation(-mh)
    transforms[mb] = (M, pb)

verts, tris, uvs = [], [], []
for m in src_meshes:
    names = [g.name for g in m.vertex_groups]
    base = len(verts)
    for v in m.data.vertices:
        p = m.matrix_world @ v.co
        acc = Vector((0, 0, 0)); total = 0; weights = {}
        for g in v.groups:
            n = names[g.group]
            if n not in transforms or g.weight <= 0: continue
            M, pb = transforms[n]
            acc += (M @ p) * g.weight; total += g.weight
            weights[pb] = weights.get(pb, 0) + g.weight
        if total == 0:
            M, pb = transforms['Pelvis']; acc = M @ p; total = 1; weights = {'Hips': 1}
        top = sorted(weights.items(), key=lambda x: -x[1])[:4]; s = sum(w for _, w in top)
        verts.append({'p': list(acc / total), 'w': [[b, w / s] for b, w in top]})
    uv = m.data.uv_layers.active.data
    for poly in m.data.polygons:
        loops = list(poly.loop_indices)
        for k in range(1, len(loops) - 1):
            tri = [loops[0], loops[k], loops[k + 1]]
            tris.append([base + m.data.loops[l].vertex_index for l in tri])
            uvs.append([list(uv[l].uv) for l in tri])

# Flat, JsonUtility-friendly layout for the Unity installer (UniformCastUpgrade).
export = {'model': MODEL,
          'vertices': [{'p': v['p'], 'bones': [b for b, _ in v['w']], 'weights': [w for _, w in v['w']]} for v in verts],
          'triangles': [i for t in tris for i in t],
          'uvs': [c for t in uvs for uv in t for c in uv],
          'bones': [{'name': b.name, 'head': list(rig.matrix_world @ b.head_local)} for b in rig.data.bones]}
with open(os.path.join(OUT, 'lowpoly_elias.json'), 'w') as f:
    json.dump(export, f)
print('ELIAS verts', len(verts), 'tris', len(tris))

# Preview: rebuild the fitted mesh, skin it to the rig and render rest pose and a walk frame.
mesh = bpy.data.meshes.new('EliasLowPoly')
mesh.from_pydata([Vector(v['p']) for v in verts], [], tris)
uvl = mesh.uv_layers.new()
for i, poly in enumerate(mesh.polygons):
    for k, li in enumerate(poly.loop_indices): uvl.data[li].uv = uvs[i][k]
obj = bpy.data.objects.new('EliasLowPoly', mesh); bpy.context.scene.collection.objects.link(obj)
for i, v in enumerate(verts):
    for b, w in v['w']:
        g = obj.vertex_groups.get(b) or obj.vertex_groups.new(name=b); g.add([i], w, 'REPLACE')
obj.parent = rig; mod = obj.modifiers.new('arm', 'ARMATURE'); mod.object = rig
mat = bpy.data.materials.new('atlas'); mat.use_nodes = True
tex = mat.node_tree.nodes.new('ShaderNodeTexImage'); tex.image = bpy.data.images.load(ATLAS); tex.interpolation = 'Closest'
mat.node_tree.links.new(tex.outputs[0], next(n for n in mat.node_tree.nodes if n.type=='BSDF_PRINCIPLED').inputs[0]); mesh.materials.append(mat)
for o in new: o.hide_render = True
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x = 1400; sc.render.resolution_y = 1000
cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam); sc.camera = cam
cam.data.lens = 50
def shot(label, action=None, frame=1, angle=25):
    rig.animation_data_create(); rig.animation_data.action = bpy.data.actions.get(action) if action else None
    if not action:
        for pb in rig.pose.bones: pb.matrix_basis = Matrix.Identity(4)
    sc.frame_set(frame)
    a = math.radians(angle); cam.location = Vector((math.sin(a) * 4.2, -math.cos(a) * 4.2, 1.0))
    cam.rotation_euler = (Vector((0, 0, .9)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(OUT, 'elias_' + label + '.png'); bpy.ops.render.render(write_still=True)
shot('rest'); shot('walk', 'Walk', 8); shot('carry', 'Carry', 10, 60)
cam.data.lens = 85; rig.animation_data.action = None
for pb in rig.pose.bones: pb.matrix_basis = Matrix.Identity(4)
sc.frame_set(1); cam.location = Vector((0.5, -1.6, 1.55)); cam.rotation_euler = (Vector((0, 0, 1.5)) - cam.location).to_track_quat('-Z', 'Y').to_euler()
sc.render.filepath = os.path.join(OUT, 'elias_face.png'); bpy.ops.render.render(write_still=True)
