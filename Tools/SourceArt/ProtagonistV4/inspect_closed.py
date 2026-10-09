# Open and non-manifold edges of a source model after welding coincident vertices.
# Run: blender -b --factory-startup --python inspect_closed.py -- <file>
import bpy, bmesh, sys
src = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src)
ob = next(o for o in bpy.data.objects if o.type == 'MESH'); bm = bmesh.new(); bm.from_mesh(ob.data)
for dist in (1e-6, 5e-5, 2e-4):
    b2 = bm.copy(); bmesh.ops.remove_doubles(b2, verts=b2.verts, dist=dist)
    print('CLOSED weld %g: verts %d boundary edges %d non-manifold %d' % (dist, len(b2.verts), sum(1 for e in b2.edges if e.is_boundary), sum(1 for e in b2.edges if len(e.link_faces) > 2)))
    b2.free()
