# Inspect an AI-generated character source: objects, skeleton, triangle counts, materials, bounds; renders
# front, side and hand close-ups to generated/. Run: blender -b --factory-startup --python inspect_source.py -- <file>
import bpy, sys, os, math, mathutils
src = sys.argv[sys.argv.index("--") + 1]
here = os.path.dirname(os.path.abspath(__file__)); out = os.path.join(here, "generated"); os.makedirs(out, exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
if src.lower().endswith((".glb", ".gltf")): bpy.ops.import_scene.gltf(filepath=src)
else: bpy.ops.import_scene.fbx(filepath=src)
lines = []
for o in bpy.context.scene.objects:
    info = f"{o.type:9} {o.name}"
    if o.type == "MESH":
        m = o.data; tris = sum(len(p.vertices) - 2 for p in m.polygons)
        info += f" verts {len(m.vertices)} tris {tris} mats {[s.material.name if s.material else None for s in o.material_slots]} groups {len(o.vertex_groups)} mods {[x.type for x in o.modifiers]}"
    if o.type == "ARMATURE":
        info += f" bones {len(o.data.bones)}: " + ", ".join(b.name for b in o.data.bones)
    lines.append(info)
for img in bpy.data.images: lines.append(f"image {img.name} {tuple(img.size)}")
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
pts = [o.matrix_world @ v.co for o in meshes for v in o.data.vertices]
lo = mathutils.Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
hi = mathutils.Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
lines.append(f"bounds min {tuple(round(x,3) for x in lo)} max {tuple(round(x,3) for x in hi)} size {tuple(round(x,3) for x in hi-lo)}")
open(os.path.join(out, "inspect.txt"), "w").write("\n".join(lines)); print("\n".join(lines))
# renders
scene = bpy.context.scene; scene.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else "BLENDER_EEVEE"
scene.render.resolution_x, scene.render.resolution_y = 900, 1100
world = bpy.data.worlds.new("w"); scene.world = world; world.use_nodes = True; world.node_tree.nodes["Background"].inputs[0].default_value = (.32, .34, .38, 1); world.node_tree.nodes["Background"].inputs[1].default_value = 1.2
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); scene.collection.objects.link(sun); sun.rotation_euler = (math.radians(50), 0, math.radians(30)); sun.data.energy = 3
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scene.collection.objects.link(cam); scene.camera = cam; cam.data.type = "ORTHO"
center = (lo + hi) / 2; size = hi - lo
up_axis = 2 if size.z >= size.y else 1  # glTF imports Y-up converted to Z-up normally
def shot(name, direction, target, ortho):
    cam.data.ortho_scale = ortho
    cam.location = target + direction.normalized() * 6
    cam.rotation_euler = (target - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.render.filepath = os.path.join(out, name + ".png"); bpy.ops.render.render(write_still=True)
shot("src_front", mathutils.Vector((0, -1, 0)), center, max(size.x, size.z) * 1.08)
shot("src_side", mathutils.Vector((1, 0, 0)), center, max(size.y, size.z) * 1.08)
# hands: extreme vertices along x (T-pose)
right = max(pts, key=lambda p: p.x); left = min(pts, key=lambda p: p.x)
for nm, p in (("src_hand_right", right), ("src_hand_left", left)):
    t = mathutils.Vector((p.x - math.copysign(.08, p.x), p.y, p.z))
    shot(nm + "_top", mathutils.Vector((0, -.2, 1)), t, .32)
    shot(nm + "_front", mathutils.Vector((0, -1, 0)), t, .32)
