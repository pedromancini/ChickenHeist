# Wrist test bench on generated/Elias_v4.blend: re-blends the left forearm/hand weights over different lengths and
# renders the wrist bent the way the game's grips bend it. Marks the wrist joint with a red dot.
# Run: blender -b generated/Elias_v4.blend --python wrist_lab.py
import bpy, os, math
from mathutils import Vector
GEN = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'generated')
rig = bpy.data.objects['ProtagonistRig']; body = bpy.data.objects['ProtagonistBody']; me = body.data
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x, sc.render.resolution_y = 420, 420
cam = bpy.data.objects.new('lab cam', bpy.data.cameras.new('lab cam')); sc.collection.objects.link(cam); sc.camera = cam; cam.data.lens = 35
el = rig.data.bones['ForearmL'].head_local; wr = rig.data.bones['HandL'].head_local; axis = (wr - el).normalized()
dot = bpy.data.objects.new('wrist joint', None)
bpy.ops.mesh.primitive_uv_sphere_add(radius=.006, location=wr); marker = bpy.context.active_object
red = bpy.data.materials.new('red'); red.diffuse_color = (1, 0, 0, 1); marker.data.materials.append(red)
fa, hd = body.vertex_groups['ForearmL'], body.vertex_groups['HandL']
original = {}
for v in me.vertices:
    ws = {body.vertex_groups[g.group].name: g.weight for g in v.groups if g.weight > 0}
    if ws and not (set(ws) - {'ForearmL', 'HandL'}): original[v.index] = ws
def blend(start, end):
    for i, ws in original.items():
        v = me.vertices[i]; t = (v.co - wr).dot(axis)
        if (v.co - (wr + axis * t)).length > .08 or not (start - .03 < t < end + .03):
            fa.add([i], ws.get('ForearmL', 0), 'REPLACE'); hd.add([i], ws.get('HandL', 0), 'REPLACE'); continue
        u = max(0.0, min(1.0, (t - start) / (end - start))); u = u * u * (3 - 2 * u)
        fa.add([i], 1 - u, 'REPLACE'); hd.add([i], u, 'REPLACE')
pb = rig.pose.bones['HandL']; pb.rotation_mode = 'XYZ'
# local X: up/down (extension/flexion), local Z: towards the thumb/little finger, local Y: twist
poses = [('rest', (0, 0, 0)), ('ext35', (math.radians(-35), 0, 0)), ('flex45', (math.radians(45), 0, 0)), ('ulnar40', (0, 0, math.radians(-40))),
         ('wheel', (math.radians(-25), math.radians(30), math.radians(-35)))]
blends = [('as built', None, None)] if os.environ.get('LAB_KEEP') == '1' else [('cur', -.10, .03), ('mid', -.05, .02), ('short', -.03, .015)]
views = [('top', Vector((0, -.15, 1))), ('side', Vector((0, -1, .1))), ('under', Vector((0, -.3, -1)))]
for bname, a, b in blends:
    if a is not None: blend(a, b)
    for pname, rot in poses:
        pb.rotation_euler = rot; bpy.context.view_layer.update()
        c = rig.matrix_world @ pb.head + (rig.matrix_world @ pb.tail - rig.matrix_world @ pb.head) * .3
        for vname, d in views:
            cam.location = c + d.normalized() * .42; cam.rotation_euler = (c - cam.location).to_track_quat('-Z', 'Y').to_euler()
            sc.render.filepath = os.path.join(GEN, 'lab_%s_%s_%s.png' % (bname, pname, vname)); bpy.ops.render.render(write_still=True)
pb.rotation_euler = (0, 0, 0)
print('LAB done')
