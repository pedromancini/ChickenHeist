# Low-poly hats for the Medieval People cast, mapped onto palette bands so each character's own
# atlas colours them: straw hat -> "yellow" band, cap -> "red" band. Origin = top of the skull,
# +Y forward in Unity (Blender -Y), sized for a 1.78 m character.
# Run: blender -b --factory-startup --python build_cast_accessories.py
import bpy, bmesh, math, os
from mathutils import Vector

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', 'ChickenHeistGenerated', 'Characters', 'Villagers', 'CastAccessories.fbx')
def band_v(top, bottom): return 1 - ((top + bottom) / 2) / 64
bpy.ops.wm.read_factory_settings(use_empty=True)

def finish(bm, name, v):
    me = bpy.data.meshes.new(name); bm.to_mesh(me); bm.free()
    uv = me.uv_layers.new()
    for l in uv.data: l.uv = (.5, v)
    for p in me.polygons: p.use_smooth = False
    obj = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(obj); return obj

# Straw hat: low crown with a dent, wide slightly drooping brim.
bm = bmesh.new(); seg = 12
crown_r, crown_h, brim_r = .105, .085, .215
rings = []
for z, r in [(-.035, crown_r * 1.02), (crown_h * .6, crown_r * .98), (crown_h, crown_r * .82)]:
    rings.append([bm.verts.new((math.cos(a) * r, math.sin(a) * r * .92, z)) for a in [i * 2 * math.pi / seg for i in range(seg)]])
top = bm.verts.new((0, 0, crown_h - .012))
for a, b in zip(rings, rings[1:]):
    for i in range(seg): bm.faces.new([a[i], a[(i + 1) % seg], b[(i + 1) % seg], b[i]])
for i in range(seg): bm.faces.new([rings[-1][i], rings[-1][(i + 1) % seg], top])
brim = [bm.verts.new((math.cos(a) * brim_r, math.sin(a) * brim_r * .95, -.035 - .025 * abs(math.sin(a)))) for a in [i * 2 * math.pi / seg for i in range(seg)]]
under = [bm.verts.new((v.co.x, v.co.y, v.co.z - .01)) for v in brim]
base = rings[0]
for i in range(seg):
    j = (i + 1) % seg
    bm.faces.new([base[i], brim[i], brim[j], base[j]])
    bm.faces.new([brim[i], under[i], under[j], brim[j]])
    bm.faces.new([under[i], base[i], base[j], under[j]])
finish(bm, 'StrawHat', band_v(40, 44))

# Cap: fitted dome and a curved visor pointing forward (-Y in Blender = Unity forward).
bm = bmesh.new(); r = .102
dome = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=6, radius=r)
for v in list(bm.verts):
    if v.co.z < -r * .05: bm.verts.remove(v)
for v in bm.verts: v.co.z = v.co.z * .72 - .02
arc = [math.radians(x) for x in range(-70, 71, 20)]
for thick in (0, -.008):
    rim = [bm.verts.new((math.sin(a) * r, -math.cos(a) * r, -.02 + thick)) for a in arc]
    tip = [bm.verts.new((math.sin(a) * r * .9, -math.cos(a) * r * 1.8, -.045 + thick)) for a in arc]
    for i in range(len(arc) - 1):
        f = [rim[i], rim[i + 1], tip[i + 1], tip[i]]
        bm.faces.new(f if thick == 0 else list(reversed(f)))
finish(bm, 'Cap', band_v(36, 40))

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=os.path.abspath(OUT), use_selection=True, object_types={'MESH'}, axis_forward='-Z', axis_up='Y', bake_anim=False)
print('ACCESSORIES exported', os.path.abspath(OUT))
