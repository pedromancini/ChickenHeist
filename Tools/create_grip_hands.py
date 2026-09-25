import bpy, math
from pathlib import Path
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/ChickenHeistGenerated/Hands';OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
# Canonical hand space: X across knuckles, Y wrist to fingers, Z out of palm.
def V(p):return Vector((p[0],-p[2],p[1]))
verts=[];faces=[];poses={'Wheel':[],'Phone':[]}
def tube(paths,radii,sides=8):
 start=len(verts)
 for k,p in enumerate(paths['Open']):
  for s in range(sides):
   a=2*math.pi*s/sides
   for name,dst in [('Open',verts),*poses.items()]:
    path=paths[name];direction=Vector(path[min(k+1,len(path)-1)])-Vector(path[max(0,k-1)])
    direction.normalize();right=Vector((1,0,0));normal=right.cross(direction).normalized()
    dst.append(V(Vector(path[k])+right*math.cos(a)*radii[k]+normal*math.sin(a)*radii[k]))
 for k in range(len(radii)-1):
  for s in range(sides):faces.append((start+k*sides+s,start+k*sides+(s+1)%sides,start+(k+1)*sides+(s+1)%sides,start+(k+1)*sides+s))
 faces.append(tuple(start+s for s in reversed(range(sides))))
 faces.append(tuple(start+(len(radii)-1)*sides+s for s in range(sides)))
# Elliptical palm and wrist with enough rings for rounded silhouette.
for y,w,d in [(-.022,.023,.014),(0,.026,.015),(.025,.037,.018),(.052,.040,.018),(.075,.034,.014)]:
 for s in range(12):
  a=2*math.pi*s/12;p=V((math.cos(a)*w,y,math.sin(a)*d))
  verts.append(p)
  for dst in poses.values():dst.append(p.copy())
for k in range(4):
 for s in range(12):faces.append((k*12+s,k*12+(s+1)%12,(k+1)*12+(s+1)%12,(k+1)*12+s))
faces.extend([tuple(reversed(range(12))),tuple(48+s for s in range(12))])
for i,x in enumerate([-.029,-.010,.010,.029]):
 length=[.061,.069,.064,.050][i]
 openpath=[(x,.073,0),(x,.073+length*.37,.002),(x,.073+length*.70,.006),(x,.073+length*.94,.012),(x,.073+length,.014)]
 wheel=[(x,.073,0),(x,.093,.010),(x,.099,.033),(x,.080,.053),(x,.062,.038)]
 phone=[(x,.073,0),(x,.073+length*.4,.002),(x,.073+length*.75,.004),(x,.073+length*.97,.008),(x,.073+length,.015)]
 tube({'Open':openpath,'Wheel':wheel,'Phone':phone},[.010,.0095,.0085,.007,.0045])
thumb=[(.031,.023,0),(.050,.037,.006),(.057,.055,.011),(.052,.071,.015)]
tube({'Open':thumb,'Wheel':[(.031,.023,0),(.052,.035,.025),(.048,.059,.044),(.028,.075,.045)],'Phone':[(.031,.023,0),(.055,.035,.019),(.061,.052,.036),(.049,.075,.042)]},[.014,.012,.010,.007])
mesh=bpy.data.meshes.new('ArticulatedGrip');mesh.from_pydata(verts,[],faces);mesh.update()
o=bpy.data.objects.new('GripHand',mesh);bpy.context.collection.objects.link(o);bpy.context.view_layer.objects.active=o;o.select_set(True)
o.shape_key_add(name='Basis')
for name,points in poses.items():
 key=o.shape_key_add(name=name)
 for v,p in zip(key.data,points):v.co=p
for p in mesh.polygons:p.use_smooth=True
mat=bpy.data.materials.new('WorkGlove');mat.diffuse_color=(.24,.17,.10,1);o.data.materials.append(mat)
for name,pos in [('Wrist',(0,0,0)),('Across',(1,0,0)),('Fingers',(0,1,0)),('Palm',(0,0,1))]:
 marker=bpy.data.objects.new(name,None);bpy.context.collection.objects.link(marker);marker.location=V(pos);marker.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT/'GripHand.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=False,axis_forward='-Z',axis_up='Y')
(ROOT/'Tools/SourceArt').mkdir(exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Tools/SourceArt/GripHands.blend'))
print('HAND_POSES_EXPORTED',len(verts),len(faces))
