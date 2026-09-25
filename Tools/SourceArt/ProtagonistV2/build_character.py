import bpy, bmesh, math, os, json, array
from mathutils import Vector, Matrix, Quaternion
ROOT=os.path.dirname(os.path.abspath(__file__))
OUT=os.path.join(ROOT,'generated');os.makedirs(OUT,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=r'E:\Jogo3D\ChickenHeist\Assets\69f0dedb-960e-44f9-8794-a2019f4656e6.glb')
mesh=next(o for o in bpy.context.scene.objects if o.type=='MESH')
bpy.context.view_layer.objects.active=mesh
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
lo=min(v.co.z for v in mesh.data.vertices);hi=max(v.co.z for v in mesh.data.vertices)
for v in mesh.data.vertices:v.co=Vector((v.co.x,v.co.y,v.co.z-lo))*(1.78/(hi-lo))
mesh.location=(0,0,0);mesh.name='ProtagonistBody'
material=mesh.data.materials[0]
node=next(n for n in material.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
texture=node.inputs['Base Color'].links[0].from_node.image
pixels=array.array('f',[0])*len(texture.pixels);texture.pixels.foreach_get(pixels)
paint=bpy.data.images.new('ProtagonistBaseColor',width=texture.size[0],height=texture.size[1],alpha=True)
paint.colorspace_settings.name=texture.colorspace_settings.name;paint.pixels.foreach_set(pixels)
paint.filepath_raw=os.path.join(OUT,'Protagonist_BaseColor.png');paint.file_format='PNG';paint.save()
node.inputs['Base Color'].links[0].from_node.image=paint
bpy.ops.object.select_all(action='DESELECT');bpy.ops.object.armature_add()
rig=bpy.context.object;rig.name='ProtagonistRig'
bpy.ops.object.mode_set(mode='EDIT');rig.data.edit_bones.remove(rig.data.edit_bones[0])
spec=[('Hips',None,(0,0,.79),(0,0,.94)),('Spine','Hips',(0,0,.94),(0,0,1.10)),
      ('Chest','Spine',(0,0,1.10),(0,0,1.26)),('Neck','Chest',(0,0,1.26),(0,0,1.34)),('Head','Neck',(0,0,1.34),(0,0,1.66))]
mapping={'Spine':'B-spine','Chest':'B-chest','Neck':'B-neck','Head':'B-head'}
for side,s in [('L',1),('R',-1)]:
    spec += [('Shoulder'+side,'Chest',(s*.065,0,1.225),(s*.205,0,1.23)),
             ('UpperArm'+side,'Shoulder'+side,(s*.205,0,1.23),(s*.465,0,1.19)),
             ('Forearm'+side,'UpperArm'+side,(s*.465,0,1.19),(s*.69,-.005,1.18)),
             ('Hand'+side,'Forearm'+side,(s*.69,-.005,1.18),(s*.775,-.005,1.17)),
             ('Thigh'+side,'Hips',(s*.115,0,.79),(s*.14,-.015,.43)),
             ('Shin'+side,'Thigh'+side,(s*.14,-.015,.43),(s*.17,0,.105)),
             ('Foot'+side,'Shin'+side,(s*.17,0,.105),(s*.17,-.145,.055))]
    for a,b in [('Shoulder','shoulder'),('UpperArm','upperArm'),('Forearm','forearm'),('Hand','hand'),('Thigh','thigh'),('Shin','shin'),('Foot','foot')]:mapping[a+side]='B-'+b+'.'+side
    for finger,y,length in [('Index',-.045,.096),('Middle',-.013,.105),('Ring',.018,.097),('Little',.045,.078),('Thumb',-.071,.069)]:
        start=Vector((s*(.735 if finger=='Thumb' else .776),-.045 if finger=='Thumb' else y,1.163 if finger=='Thumb' else 1.175))
        for j in range(3):
            end=start+Vector((s*length/3, -.014 if finger=='Thumb' else 0,-.012 if j else -.001))
            name=finger+str(j+1)+side;parent='Hand'+side if j==0 else finger+str(j)+side
            spec.append((name,parent,tuple(start),tuple(end)));start=end
            source={'Index':'indexFinger','Middle':'middleFinger','Ring':'ringFinger','Little':'pinky','Thumb':'thumb'}[finger]
            mapping[name]='B-'+source+f'{j+1:02d}.'+side
for name,parent,head,tail in spec:
    b=rig.data.edit_bones.new(name);b.head=head;b.tail=tail
    if parent:b.parent=rig.data.edit_bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
bpy.ops.object.parent_set(type='ARMATURE_NAME')
def distance(p,b):
    axis=b.tail_local-b.head_local;t=max(0,min(1,(p-b.head_local).dot(axis)/axis.length_squared))
    return (p-b.head_local-axis*t).length
for v in mesh.data.vertices:
    p=v.co;s='L' if p.x>=0 else 'R'
    if p.z>1.36:names=['Head']
    elif abs(p.x)>.745 and p.z>1.05:
        fingers=[n for n in mapping if n.endswith(s) and any(n.startswith(f) for f in ['Index','Middle','Ring','Little','Thumb'])]
        closest=min(fingers,key=lambda n:distance(p,rig.data.bones[n]));family=closest[:-2]
        names=['Hand'+s]+[n for n in fingers if n.startswith(family)]
    elif abs(p.x)>.29 and p.z>.94:names=['UpperArm'+s,'Forearm'+s,'Hand'+s]
    elif p.z<.76:names=['Hips','Thigh'+s,'Shin'+s,'Foot'+s]
    else:names=['Hips','Spine','Chest','Neck','Head','Shoulder'+s,'UpperArm'+s,'Thigh'+s]
    ranked=sorted((distance(p,rig.data.bones[n]),n) for n in names)[:3]
    weights=[1/(d+.012)**5 for d,n in ranked];total=sum(weights)
    for (_,n),w in zip(ranked,weights):mesh.vertex_groups[n].add([v.index],w/total,'REPLACE')
# Retopologize the generated hands: regular joint loops prevent torn fingers.
# Sample the original skin paint so the replacement retains the character palette.
uv=mesh.data.uv_layers.active.data;swatch=None;score=1e9
for poly in mesh.data.polygons:
    for li in poly.loop_indices:
        p=mesh.data.vertices[mesh.data.loops[li].vertex_index].co
        if abs(p.x)<.70 or p.z<1.08:continue
        u,v=uv[li].uv;pixel=(min(paint.size[1]-1,int(v*paint.size[1]))*paint.size[0]+min(paint.size[0]-1,int(u*paint.size[0])))*4
        r,g,b=pixels[pixel:pixel+3]
        candidate=abs(r-.68)+abs(g-.44)+abs(b-.31)
        if candidate<score:score=candidate;swatch=(u,v)
bm=bmesh.new();bm.from_mesh(mesh.data)
bmesh.ops.delete(bm,geom=[f for f in bm.faces if any(abs(v.co.x)>.51 and v.co.z>1.03 for v in f.verts)],context='FACES')
bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS');bm.to_mesh(mesh.data);bm.free()
verts=[];faces=[];weights=[]
def ring(center,tangent,r1,r2,groups):
    axis=Vector(tangent).normalized();across=Vector((0,1,0));across=(across-axis*across.dot(axis)).normalized();up=axis.cross(across).normalized()
    ids=[]
    for j in range(12):
        angle=j*math.tau/12;ids.append(len(verts));verts.append(Vector(center)+across*math.cos(angle)*r1+up*math.sin(angle)*r2);weights.append(groups)
    return ids
def bridge(a,b):
    for j in range(12):faces.append((a[j],a[(j+1)%12],b[(j+1)%12],b[j]))
for side,s in [('L',1),('R',-1)]:
    previous=None
    for x,width,depth in [(.42,.055,.046),(.445,.052,.044),(.465,.050,.042),(.49,.046,.039),(.53,.044,.037),(.57,.039,.033),(.61,.034,.029),(.652,.03,.025),(.69,.029,.024),(.72,.044,.025),(.75,.057,.024),(.776,.056,.021)]:
        w=max(0,min(1,(x-.652)/.038))
        upper=max(0,min(1,(.49-x)/.07))
        current=ring((s*x,-.005,1.18-(x-.696)*.06),(s,0,0),width,depth,{'Hand'+side:w,'Forearm'+side:(1-w)*(1-upper),'UpperArm'+side:upper})
        if previous:bridge(previous,current)
        else:faces.append(tuple(current))
        previous=current
    faces.append(tuple(reversed(previous)))
    for finger in ['Index','Middle','Ring','Little','Thumb']:
        previous=None
        radius=.0135 if finger=='Thumb' else .0105 if finger=='Little' else .012
        for joint in range(1,4):
            bone=rig.data.bones[finger+str(joint)+side];axis=bone.tail_local-bone.head_local
            for part in range(3):
                t=part/3;point=bone.head_local+axis*t
                parent='Hand'+side if joint==1 else finger+str(joint-1)+side
                mix=.5*(1-t) if joint>1 else .35*(1-t)
                current=ring(point,axis,radius*(1-(joint-1+t)*.09),radius*.85*(1-(joint-1+t)*.10),{bone.name:1-mix,parent:mix})
                if previous:bridge(previous,current)
                previous=current
        current=ring(bone.tail_local,axis,.004,.004,{bone.name:1});bridge(previous,current);faces.append(tuple(reversed(current)))
data=bpy.data.meshes.new('Clean articulated hands');data.from_pydata(verts,[],faces);data.update()
hands=bpy.data.objects.new('Retopologized hands',data);bpy.context.scene.collection.objects.link(hands);data.materials.append(material)
uv=data.uv_layers.new(name='UVMap')
for v in uv.data:v.uv=swatch
for name,_,_,_ in spec:hands.vertex_groups.new(name=name)
for i,groups in enumerate(weights):
    for name,w in groups.items():hands.vertex_groups[name].add([i],w,'REPLACE')
bm=bmesh.new();bm.from_mesh(data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(data);bm.free()
for p in data.polygons:p.use_smooth=True
bpy.ops.object.select_all(action='DESELECT');mesh.select_set(True);hands.select_set(True);bpy.context.view_layer.objects.active=mesh;bpy.ops.object.join()
head=mesh.copy();head.data=mesh.data.copy();bpy.context.scene.collection.objects.link(head);head.name='ProtagonistHead'
for obj,keep in [(mesh,False),(head,True)]:
    bm=bmesh.new();bm.from_mesh(obj.data)
    remove=[f for f in bm.faces if any(v.co.z>1.29 and abs(v.co.x)<.29 for v in f.verts)!=keep]
    bmesh.ops.delete(bm,geom=remove,context='FACES')
    bmesh.ops.delete(bm,geom=[v for v in bm.verts if not v.link_faces],context='VERTS')
    bm.to_mesh(obj.data);bm.free()
for p in rig.pose.bones:p.rotation_mode='QUATERNION'
rig.animation_data_create();bpy.context.scene.render.fps=30
def reset():
    for b in rig.pose.bones:b.matrix_basis=Matrix.Identity(4)
def orient(name,target):
    b=rig.pose.bones[name];q=(b.tail-b.head).rotation_difference(Vector(target)-b.head)
    b.matrix=Matrix.Translation(b.head)@(q@b.matrix.to_quaternion()).to_matrix().to_4x4()
    bpy.context.view_layer.update()
def leg(side,target):
    u=rig.pose.bones['Thigh'+side];l=rig.pose.bones['Shin'+side]
    axis=Vector(target)-u.head;d=max(.01,min(axis.length,u.length+l.length-.001));axis.normalize()
    a=(u.length**2-l.length**2+d*d)/(2*d);forward=Vector((0,-1,0));bend=(forward-axis*forward.dot(axis)).normalized()
    orient('Thigh'+side,u.head+axis*a+bend*math.sqrt(max(0,u.length*u.length-a*a)))
    orient('Shin'+side,target);f=rig.pose.bones['Foot'+side];orient('Foot'+side,f.head+Vector((0,-.145,-.05)))
def key(frame):
    for b in rig.pose.bones:
        b.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=b.name)
        b.keyframe_insert(data_path='location',frame=frame,group=b.name)
sources={'Idle':'Idle01','Walk':'Walk01_Forward','Run':'Sprint01_Forward','Jump':'Jump01 - Begin','Fall':'Fall01','Trade':'Talk01'}
for mode,src in [('Walk','Walk01'),('Run','Run01')]:
    for direction in ['Backward','Left','Right','ForwardLeft','ForwardRight','BackwardLeft','BackwardRight']:sources[mode+direction]=src+'_'+direction
durations={}
before=set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT,'package','HumanM_Model.fbx'))
added=set(bpy.data.objects)-before;reference=next(o for o in added if o.type=='ARMATURE')
rest={n:(reference.matrix_world@reference.data.bones[n].matrix_local).to_quaternion() for n in mapping.values()}
for o in added:bpy.data.objects.remove(o,do_unlink=True)
for state,file in sources.items():
    before=set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=os.path.join(ROOT,'package','HumanM@'+file+'.fbx'))
    added=set(bpy.data.objects)-before;source=next(o for o in added if o.type=='ARMATURE')
    action=source.animation_data.action;start,end=map(int,action.frame_range)
    anim=bpy.data.actions.new(state);anim.use_fake_user=True;rig.animation_data.action=anim
    # World-space rotation deltas preserve the new character's bone lengths.
    sourceBase=None
    for f in range(start,end+1):
        bpy.context.scene.frame_set(f);reset();bpy.context.view_layer.update()
        sourceHip=source.matrix_world.translation
        if sourceBase is None:sourceBase=sourceHip.copy()
        rig.pose.bones['Hips'].location=rig.data.bones['Hips'].matrix_local.to_quaternion().inverted()@((sourceHip-sourceBase)*.86)
        for b in rig.pose.bones:
            if b.name not in mapping:continue
            n=mapping[b.name];rotation=(source.matrix_world@source.pose.bones[n].matrix).to_quaternion()@rest[n].inverted()@b.bone.matrix_local.to_quaternion()
            b.matrix=Matrix.Translation(b.head)@rotation.to_matrix().to_4x4();bpy.context.view_layer.update()
        key(f-start+1)
    durations[state]=(end-start)/30
    for o in added:bpy.data.objects.remove(o,do_unlink=True)
    print('RETARGETED',state,durations[state],flush=True)
for state,duration in {'CrouchIdle':2.8,'Crouch':1.2,'Pickup':.85,'Drive':3,'Lockpick':2,'Ignite':1.4,'Carry':2.4}.items():
    anim=bpy.data.actions.new(state);anim.use_fake_user=True;rig.animation_data.action=anim;frames=round(duration*30)
    for frame in range(frames+1):
        reset();t=frame/frames;p=t*math.tau
        crouch=state.startswith('Crouch');drive=state in ('Drive','Ignite');reach=math.sin(t*math.pi) if state=='Pickup' else 0
        drop=-.32 if crouch else -.32 if drive else -.15*reach if state=='Pickup' else -.02
        hips=rig.pose.bones['Hips'];hips.location=hips.bone.matrix_local.to_quaternion().inverted()@Vector((0,0,drop))
        rig.pose.bones['Spine'].rotation_quaternion=Quaternion((1,0,0),math.radians(18 if crouch else 23*reach if state=='Pickup' else 2))
        bpy.context.view_layer.update()
        for side,s,offset in [('L',1,0),('R',-1,math.pi)]:
            q=p+offset;moving=state=='Crouch'
            leg(side,(s*.17, -.31 if drive else -.12*math.cos(q) if moving else 0,.105+(max(0,math.sin(q))*.045 if moving else 0)))
            arm=rig.pose.bones['UpperArm'+side]
            raised=state in ('Drive','Ignite','Lockpick','Carry')
            elbow=arm.head+Vector((s*.045,-.17 if raised else -.12*reach,-.22))
            orient('UpperArm'+side,elbow);fore=rig.pose.bones['Forearm'+side]
            orient('Forearm'+side,fore.head+Vector((-s*.07 if raised else s*.015,-.21 if raised else -.10-reach*.13,.01 if raised else -.19+reach*.10)))
            hand=rig.pose.bones['Hand'+side];orient('Hand'+side,hand.head+Vector((0,-.09,-.02)))
            for finger in ['Index','Middle','Ring','Little','Thumb']:
                for j in range(1,4):
                    b=rig.pose.bones[finger+str(j)+side]
                    # Finger bend axis is computed in bind space toward the palm.
                    axis=b.bone.matrix_local.to_quaternion().inverted()@Vector((0,s,0))
                    amount=(18 if state=='Carry' else 32 if raised else 4)+(math.sin(p)*2 if raised else 0)
                    b.rotation_quaternion=Quaternion(axis,math.radians(amount))
        key(frame+1)
    durations[state]=duration
    print('AUTHORED',state,flush=True)
rig.animation_data.action=bpy.data.actions['Idle'];bpy.context.scene.frame_set(1)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'Protagonist_Rigged.blend'))
bpy.ops.object.select_all(action='DESELECT')
for o in (rig,mesh,head):o.select_set(True)
bpy.context.view_layer.objects.active=rig
# Export only target actions, excluding imported source actions with other slots.
for action in list(bpy.data.actions):
    if action.name not in durations:bpy.data.actions.remove(action)
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'Protagonist_Rigged.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,axis_forward='-Z',axis_up='Y')
with open(os.path.join(OUT,'rig-report.json'),'w') as f:json.dump({'source':'69f0dedb-960e-44f9-8794-a2019f4656e6.glb','bones':len(rig.data.bones),'fingerBones':30,'clips':durations,'packageClips':sources,'unweightedVertices':sum(1 for o in (mesh,head) for v in o.data.vertices if not v.groups)},f,indent=2)
print('EXPORT_OK',flush=True)

