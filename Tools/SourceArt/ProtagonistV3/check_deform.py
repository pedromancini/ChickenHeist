# Deformation check for generated/Elias_v3.blend: curls the fingers like ProtagonistFingers (Drive = 38 deg per
# phalanx, thumb 45 %), bends elbows/knees, and reports stretched edges per body region plus close-up renders.
import bpy, os, math
from mathutils import Vector, Quaternion

ROOT = os.path.dirname(os.path.abspath(__file__)); GEN = os.path.join(ROOT, 'generated')
bpy.ops.wm.open_mainfile(filepath=os.path.join(GEN, 'Elias_v3.blend'))
rig = bpy.data.objects['ProtagonistRig']; body = bpy.data.objects['ProtagonistBody']
FINGERS = ['Thumb', 'Index', 'Middle', 'Ring', 'Little']
dg = bpy.context.evaluated_depsgraph_get()
def coords():
    dg = bpy.context.evaluated_depsgraph_get(); e = body.evaluated_get(dg); m = e.to_mesh()
    c = [v.co.copy() for v in m.vertices]; e.to_mesh_clear(); return c
rest = coords()
pb = rig.pose.bones
CURL = float(os.environ.get('CURL', '38'))
for side in 'LR':
    for f in FINGERS:
        for k in (1, 2, 3):
            b = pb['%s%d%s' % (f, k, side)]; bone = b.bone
            along = (bone.tail_local - bone.head_local).normalized()
            axis_w = along.cross(Vector((0, 0, -1))).normalized()
            axis_l = bone.matrix_local.to_3x3().inverted() @ axis_w
            b.rotation_mode = 'QUATERNION'; b.rotation_quaternion = Quaternion(axis_l, math.radians(CURL * (.45 if f == 'Thumb' else 1)))
    for n, ang in (('Forearm' + side, 60), ('Shin' + side, 70)):
        b = pb[n]; bone = b.bone
        axis_l = bone.matrix_local.to_3x3().inverted() @ Vector((0, 0, 1) if n.startswith('Forearm') else (1, 0, 0))
        b.rotation_mode = 'QUATERNION'; b.rotation_quaternion = Quaternion(axis_l, math.radians(ang * (1 if side == 'L' else -1) if n.startswith('Forearm') else ang))
bpy.context.view_layer.update()
posed = coords()
gidx = {g.index: g.name for g in body.vertex_groups}
def region(vi):
    best = max(body.data.vertices[vi].groups, key=lambda g: g.weight, default=None)
    n = gidx[best.group] if best else 'Hips'
    if n.startswith(tuple(FINGERS)) or n.startswith('Hand'): return 'hand'
    if n.startswith(('Forearm', 'UpperArm')): return 'arm'
    if n.startswith(('Thigh', 'Shin', 'Foot', 'Toe')): return 'leg'
    return 'torso'
stats = {}
for e in body.data.edges:
    a, b = e.vertices; r0 = (rest[a] - rest[b]).length
    if r0 < 1e-5: continue
    ratio = (posed[a] - posed[b]).length / r0; reg = region(a)
    s = stats.setdefault(reg, [0, 0, 1.0])
    s[0] += 1
    if ratio > 1.6 or ratio < .45: s[1] += 1
    s[2] = max(s[2], ratio)
for reg, (n, bad, worst) in sorted(stats.items()):
    print('DEFORM %-6s edges %5d  stretched %4d (%.2f%%)  worst x%.2f' % (reg, n, bad, 100 * bad / n, worst))
sc = bpy.context.scene; cam = sc.camera or bpy.data.objects.new('cam', bpy.data.cameras.new('cam'))
if cam.name not in sc.collection.objects: sc.collection.objects.link(cam)
sc.camera = cam; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x, sc.render.resolution_y = 800, 600; cam.data.lens = 60
hp = rig.matrix_world @ pb['HandL'].tail
for name, off in (('deform_hand_side.png', (0, -.5, .02)), ('deform_hand_top.png', (.02, -.12, .45)), ('deform_full.png', None)):
    if off is None:
        cam.location = Vector((2.4, -3.6, 1.3)); look = Vector((0, 0, .95)); cam.data.lens = 50; sc.render.resolution_x, sc.render.resolution_y = 700, 900
    else:
        cam.location = hp + Vector(off); look = hp
    cam.rotation_euler = (look - cam.location).to_track_quat('-Z', 'Y').to_euler()
    sc.render.filepath = os.path.join(GEN, name); bpy.ops.render.render(write_still=True)
