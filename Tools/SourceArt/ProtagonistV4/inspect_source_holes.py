# Open boundary loops of a source model (after welding coincident vertices): size, perimeter and position, and
# the non-manifold edges. Run: blender -b --factory-startup --python inspect_source_holes.py -- <file>
import bpy, bmesh, sys
from collections import Counter
src = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True); bpy.ops.import_scene.gltf(filepath=src)
ob = next(o for o in bpy.data.objects if o.type == 'MESH'); ob.data.transform(ob.matrix_world)
bm = bmesh.new(); bm.from_mesh(ob.data); bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=5e-5)
z0 = min(v.co.z for v in bm.verts); z1 = max(v.co.z for v in bm.verts); H = z1 - z0
bnd = {e for e in bm.edges if e.is_boundary}; seen = set(); loops = []
for e in bnd:
    if e in seen: continue
    stack = [e]; comp = []
    while stack:
        x = stack.pop()
        if x in seen: continue
        seen.add(x); comp.append(x); stack.extend(y for v in x.verts for y in v.link_edges if y in bnd and y not in seen)
    per = sum(x.calc_length() for x in comp) / H * 1.74
    vs = list({v for x in comp for v in x.verts}); c = sum((v.co for v in vs), vs[0].co * 0) / len(vs)
    loops.append((per, len(comp), c))
loops.sort(key=lambda l: -l[0])
print('SRCHOLES loops', len(loops), 'boundary edges', len(bnd))
print('SRCHOLES perimeter histogram (cm at 1.74 m):', dict(Counter(min(50, int(p * 100 // 2 * 2)) for p, _, _ in loops)))
for per, n, c in loops[:25]:
    print('SRCHOLES   perimeter %.1f cm, %d edges, at height %.2f, x %.3f y %.3f' % (per * 100, n, (c.z - z0) / H, c.x, c.y))
nm = [e for e in bm.edges if len(e.link_faces) > 2]
print('SRCHOLES non-manifold edges', len(nm), 'heights', dict(Counter(round((sum((v.co.z for v in e.verts)) / 2 - z0) / H, 1) for e in nm)))
