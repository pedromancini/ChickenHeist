# Open boundary loops of the meshes in generated/Elias_v4.blend: size and position of each loop.
# Run: blender -b generated/Elias_v4.blend --python inspect_holes.py
import bpy, bmesh
for ob in [o for o in bpy.data.objects if o.type == 'MESH']:
    bm = bmesh.new(); bm.from_mesh(ob.data)
    bnd = {e for e in bm.edges if e.is_boundary}; seen = set(); loops = []
    for e in bnd:
        if e in seen: continue
        stack = [e]; comp = []
        while stack:
            x = stack.pop()
            if x in seen: continue
            seen.add(x); comp.append(x)
            for v in x.verts:
                for y in v.link_edges:
                    if y in bnd and y not in seen: stack.append(y)
        c = sum((v.co for x in comp for v in x.verts), comp[0].verts[0].co * 0) / (2 * len(comp))
        loops.append((len(comp), c))
    nm = sum(1 for e in bm.edges if len(e.link_faces) > 2)
    print('HOLES', ob.name, 'boundary edges', len(bnd), 'loops', len(loops), 'non-manifold edges', nm)
    for n, c in sorted(loops, key=lambda l: -l[0])[:40]:
        print('HOLES   %4d edges at (%.3f, %.3f, %.3f)' % (n, c.x, c.y, c.z))
    bm.free()
