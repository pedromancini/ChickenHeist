import bpy
import math
from pathlib import Path
from mathutils import Vector

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
folder = Path(__file__).resolve().parents[1] / 'Assets/ChickenHeistGenerated/Padlock'
folder.mkdir(parents=True, exist_ok=True)

def material(name, color, metallic):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = (*color, 1)
    mat.use_nodes = True
    shader = next((node for node in mat.node_tree.nodes if node.type == 'BSDF_PRINCIPLED'), None)
    if shader is None:
        shader = mat.node_tree.nodes.new('ShaderNodeBsdfPrincipled')
        output = mat.node_tree.nodes.new('ShaderNodeOutputMaterial')
        mat.node_tree.links.new(shader.outputs['BSDF'], output.inputs['Surface'])
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Metallic'].default_value = metallic
    shader.inputs['Roughness'].default_value = .34
    return mat

brass = material('AgedBrass', (.53, .34, .12), .72)
steel = material('Steel', (.46, .53, .58), .85)
dark = material('Keyway', (.018, .021, .022), .05)
wear = material('BrassWear', (.68, .49, .23), .65)

def box(name, location, dimensions, mat, bevel=0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=location)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    obj.data.materials.append(mat)
    if bevel:
        mod = obj.modifiers.new('Machined edges', 'BEVEL')
        mod.width = bevel
        mod.segments = 2
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return obj

box('Cast brass body', (0, 0, -.18), (.94, .78, .60), brass, .065)
box('Bottom steel seam', (0, 0, -.446), (.88, .71, .045), steel, .02)
box('Top collar', (0, 0, .091), (.83, .66, .055), wear, .022)

# Continuous U-shaped shackle, with a genuine empty opening above the body.
path = [Vector((-.285, 0, .08)), Vector((-.285, 0, .205))]
for step in range(1, 17):
    angle = math.pi - math.pi * step / 16
    path.append(Vector((.285 * math.cos(angle), 0, .205 + .285 * math.sin(angle))))
path.append(Vector((.285, 0, .08)))
vertices, faces = [], []
for index, point in enumerate(path):
    tangent = (path[min(index + 1, len(path) - 1)] - path[max(index - 1, 0)]).normalized()
    side = Vector((0, 1, 0))
    normal = tangent.cross(side).normalized()
    for segment in range(10):
        angle = segment * math.tau / 10
        vertices.append(point + .052 * (side * math.cos(angle) + normal * math.sin(angle)))
for index in range(len(path) - 1):
    for segment in range(10):
        a = index * 10 + segment
        b = index * 10 + (segment + 1) % 10
        faces.append((a, b, b + 10, a + 10))
faces += [tuple(reversed(range(10))), tuple(range((len(path)-1)*10, len(path)*10))]
mesh = bpy.data.meshes.new('ShackleMesh')
mesh.from_pydata(vertices, [], faces)
mesh.update()
arc = bpy.data.objects.new('Steel shackle', mesh)
bpy.context.collection.objects.link(arc)
arc.data.materials.append(steel)

def disc(name, x, y, z, radius, depth, mat):
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=radius, depth=depth, location=(x,y,z), rotation=(math.pi/2,0,0))
    obj = bpy.context.object
    obj.name = name
    obj.data.materials.append(mat)
    return obj

disc('Cylinder escutcheon', 0, -.396, -.175, .153, .018, steel)
disc('Dark keyhole', 0, -.409, -.145, .056, .013, dark)
box('Keyway slot', (0,-.417,-.208), (.070,.012,.105), dark, .006)
for x in [-.345, .345]:
    for z in [-.352, .005]:
        disc('Rivet', x, -.384, z, .032, .03, steel)
for x,z,length in [(-.20,-.09,.18),(.23,-.29,.15),(-.19,-.31,.12),(.21,-.04,.09)]:
    box('Surface wear', (x,-.393,z), (length,.006,.008), wear, .002)

bpy.ops.object.select_all(action='SELECT')
bpy.context.view_layer.objects.active = bpy.data.objects['Cast brass body']
bpy.ops.object.join()
obj=bpy.context.object
obj.name='CoopPadlock'
bpy.context.scene.cursor.location=(0,0,0)
bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
source_folder = Path(__file__).resolve().parent / 'SourceArt'
source_folder.mkdir(parents=True, exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(source_folder/'CoopPadlock.blend'))
bpy.ops.export_scene.fbx(filepath=str(folder/'CoopPadlock.fbx'), use_selection=True, add_leaf_bones=False, bake_anim=False, axis_forward='-Z', axis_up='Y', apply_unit_scale=True)
print('PADLOCK_EXPORTED', len(obj.data.polygons))
