# Renders the body and head meshes of generated/Elias_v4.blend separately (side and front), to check what the
# head split keeps on the body. Run: blender -b generated/Elias_v4.blend --python inspect_split.py
import bpy, os, math
from mathutils import Vector
GEN = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'generated')
sc = bpy.context.scene; sc.render.engine = 'BLENDER_WORKBENCH'; sc.display.shading.light = 'STUDIO'; sc.display.shading.color_type = 'TEXTURE'
sc.render.resolution_x, sc.render.resolution_y = 700, 700
body = bpy.data.objects['ProtagonistBody']; head = bpy.data.objects['ProtagonistHead']
rig = bpy.data.objects['ProtagonistRig']
for b in rig.pose.bones: b.rotation_mode = 'XYZ'; b.rotation_euler = (0, 0, 0); b.location = (0, 0, 0)
bpy.context.view_layer.update()
cam = next((o for o in bpy.data.objects if o.type == 'CAMERA'), None)
if cam is None: cam = bpy.data.objects.new('cam', bpy.data.cameras.new('cam')); sc.collection.objects.link(cam)
sc.camera = cam; cam.data.type = 'ORTHO'; cam.data.ortho_scale = .55
zc = max(v.co.z for v in head.data.vertices) - .2
for name, show in (('split_body', body), ('split_head', head)):
    for o in (body, head): o.hide_render = o is not show
    for tag, d in (('side', Vector((1, 0, 0))), ('front', Vector((0, -1, 0)))):
        cam.location = Vector((0, 0, zc)) + d * 3; cam.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(GEN, '%s_%s.png' % (name, tag)); bpy.ops.render.render(write_still=True)
print('SPLIT body top z', round(max(v.co.z for v in body.data.vertices), 3), 'head bottom z', round(min(v.co.z for v in head.data.vertices), 3))
