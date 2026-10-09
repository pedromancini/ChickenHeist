# Winding consistency of the meshes in generated/Elias_v4.blend: signed volume and edges whose two faces disagree.
import bpy, bmesh
for name in ('ProtagonistBody', 'ProtagonistHead'):
    ob = bpy.data.objects[name]; bm = bmesh.new(); bm.from_mesh(ob.data); bm.normal_update()
    vol = sum(f.verts[0].co.dot(f.verts[1].co.cross(f.verts[2].co)) for f in bm.faces if len(f.verts) == 3) / 6
    bad = 0
    for e in bm.edges:
        if len(e.link_faces) != 2: continue
        f1, f2 = e.link_faces; v1, v2 = e.verts
        # consistent winding: the shared edge runs in opposite directions in the two faces
        def runs(f):
            vs = list(f.verts); i = vs.index(v1); return vs[(i + 1) % len(vs)] == v2
        if runs(f1) == runs(f2): bad += 1
    print('WIND %s faces %d signed volume %.5f m3, edges with inconsistent winding %d, matrix identity %s' % (name, len(bm.faces), vol, bad, ob.matrix_world == ob.matrix_world.Identity(4)))
