import bpy, math, random
from pathlib import Path
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Assets/ChickenHeistGenerated/Truck'
OUT.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
random.seed(19)
def V(p): return Vector((p[0],-p[2],p[1]))
groups={}
def mat(name,color,metal=0,rough=.65):
    m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True
    n=next(n for n in m.node_tree.nodes if n.type=='BSDF_PRINCIPLED')
    n.inputs['Base Color'].default_value=(*color,1);n.inputs['Metallic'].default_value=metal;n.inputs['Roughness'].default_value=rough
    return m
paint=mat('FadedSage',(.29,.39,.32),.25)
paint2=mat('OldPrimer',(.43,.45,.34),.12)
rust=mat('Oxide',(.29,.10,.032))
rust2=mat('RustEdge',(.43,.20,.067))
steel=mat('WornSteel',(.32,.34,.32),.75,.38)
dark=mat('Rubber',(.024,.028,.026))
trim=mat('BlackTrim',(.052,.059,.055))
seat=mat('WornVinyl',(.15,.105,.065))
foam=mat('SeatFoam',(.47,.39,.22))
glass=mat('WindowGlass',(.14,.23,.24),.2,.18)
lamp=mat('Headlamp',(.72,.74,.57),.35,.23)
red=mat('TailLamp',(.39,.038,.018),.15)
amber=mat('Indicator',(.66,.27,.035),.1)
wood=mat('BedTimber',(.28,.21,.12))
def add(o,m,group='Body'):
    o.data.materials.append(m);groups.setdefault(group,[]).append(o);return o
def box(name,p,s,m,bevel=.015,group='Body'):
    bpy.ops.mesh.primitive_cube_add(size=1,location=V(p));o=bpy.context.object;o.name=name;o.dimensions=(s[0],s[2],s[1])
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        b=o.modifiers.new('Pressed metal edges','BEVEL');b.width=bevel;b.segments=2
        bpy.ops.object.modifier_apply(modifier=b.name)
    return add(o,m,group)
def mesh(name,verts,faces,m,group='Body',bevel=0):
    data=bpy.data.meshes.new(name);data.from_pydata([V(v) for v in verts],[],faces);data.update()
    o=bpy.data.objects.new(name,data);bpy.context.collection.objects.link(o);add(o,m,group)
    if bevel:
        bpy.context.view_layer.objects.active=o;b=o.modifiers.new('Edge lip','BEVEL');b.width=bevel;b.segments=2
        bpy.ops.object.modifier_apply(modifier=b.name)
    return o
def beam(name,a,b,width,m,group='Body'):
    a,b=V(a),V(b);mid=(a+b)/2
    bpy.ops.mesh.primitive_cylinder_add(vertices=8,radius=width/2,depth=(b-a).length,location=mid)
    o=bpy.context.object;o.name=name;o.rotation_mode='QUATERNION';o.rotation_quaternion=Vector((0,0,1)).rotation_difference(b-a)
    return add(o,m,group)
def panel(name,side,profile,m,x=.865,thick=.045):
    verts=[(side*(x+t),y,z) for t in [-thick/2,thick/2] for z,y in profile];n=len(profile)
    faces=[tuple(reversed(range(n))),tuple(range(n,n*2))]
    faces += [(i,(i+1)%n,(i+1)%n+n,i+n) for i in range(n)]
    return mesh(name,verts,faces,m,bevel=.008)
def wheel_panel(side,zmin,zmax,zc,top):
    profile=[(zmin,top),(zmax,top),(zmax,.43),(zc+.47,.43)]
    for i in range(13):
        a=i*math.pi/12;profile.append((zc+.47*math.cos(a),.39+.47*math.sin(a)))
    profile.append((zmin,.43))
    panel('Stamped fender with open wheel arch',side,profile,paint)
    points=[(side*.904,.39+.49*math.sin(i*math.pi/16),zc+.49*math.cos(i*math.pi/16)) for i in range(17)]
    for a,b in zip(points,points[1:]):beam('Wheel arch rolled lip',a,b,.025,paint2)

# Ladder chassis and a genuinely open cargo bed.
for x in [-.55,.55]:box('Chassis rail',(x,.37,-.05),(.09,.13,4.25),rust)
for z in [-1.72,-.35,.55,1.72]:box('Chassis crossmember',(0,.39,z),(1.25,.08,.07),steel)
box('Bed floor',(0,.71,-1.22),(1.64,.08,1.87),paint)
for x in [-.64,-.43,-.21,0,.21,.43,.64]:box('Bed pressed ridge',(x,.758,-1.22),(.028,.023,1.80),paint2,.005)
box('Cab floor',(0,.58,.41),(1.63,.1,1.22),paint)
for side in [-1,1]:
    wheel_panel(side,-2.18,-.27,-1.25,1.10)
    wheel_panel(side,.84,2.16,1.40,1.055)
    panel('Hood side closure',side,[(.91,.98),(2.17,.96),(2.14,1.065),(1.05,1.15),(.91,1.15)],paint,x=.828,thick=.06)
    box('Bed top rail',(side*.86,1.13,-1.22),(.12,.065,1.98),paint2)
    panel('Door skin',side,[(-.24,.45),(.83,.45),(.94,1.12),(-.24,1.14)],paint)
    panel('Inner door lining',side,[(-.25,.53),(.91,.53),(1.01,1.18),(-.25,1.20)],trim,x=.824,thick=.035)
    box('Door sill',(side*.82,.54,.34),(.13,.12,1.25),paint)
    box('Bed inner wall',(side*.79,.94,-1.22),(.055,.38,1.89),paint)
    # Door outlines and belt line are fine seams, not separate oversized blocks.
    for a,b in [((-.22,.50),(.81,.50)),((.81,.50),(.93,1.12)),((-.22,.50),(-.22,1.80))]:
        beam('Door seam',(side*.892,a[1],a[0]),(side*.892,b[1],b[0]),.009,trim)
    beam('Belt trim',(side*.899,1.14,-.21),(side*.899,1.12,.95),.024,trim)
    box('Door handle recess',(side*.899,1.015,-.045),(.013,.055,.19),trim,.008)
    box('Door handle',(side*.913,1.022,-.04),(.019,.022,.14),steel,.009)
    # Closed cabin frame, raked windshield and inset side windows.
    panel('Rear cabin pillar',side,[(-.27,1.10),(-.13,1.10),(-.11,1.89),(-.26,1.88)],paint,x=.82)
    beam('A pillar',(side*.82,1.12,1.035),(side*.755,1.87,.77),.075,paint)
    beam('Roof side rail',(side*.755,1.88,-.20),(side*.755,1.89,.78),.067,paint)
    window=[(side*.822,1.15,-.16),(side*.819,1.15,1.01),(side*.746,1.875,.78),(side*.749,1.875,-.16)]
    mesh('Side window',window,[(0,1,2,3)],glass,'Glass')
    for i in range(4):beam('Side window rubber seal',window[i],window[(i+1)%4],.035,trim)
    beam('Mirror stalk',(side*.87,1.25,.87),(side*1.00,1.36,.96),.025,steel)
    box('Mirror housing',(side*1.065,1.41,.96),(.17,.17,.075),trim,.028)
    box('Mirror surface',(side*1.065,1.41,.916),(.145,.14,.008),steel,.02)
    box('Mud flap',(side*.81,.25,-1.68),(.30,.29,.025),dark)
    # Irregular oxide spots, concentrated below door seams and bed corners.
    for n in range(28):
        z=random.uniform(-2.08,2.08);y=random.uniform(.50,1.08)
        if min(abs(z+1.25),abs(z-1.40))<.49 and y<.91:continue
        x=side*.901;r=random.uniform(.018,.075)
        verts=[(x,y+r*random.uniform(.5,1)*math.sin(k*math.tau/7),z+r*1.8*random.uniform(.5,1)*math.cos(k*math.tau/7)) for k in range(7)]
        mesh('Irregular chipped paint',verts,[tuple(range(7))],rust if n%3 else rust2)

# Hood is a tapered stamped shell with a sloping nose and central crease.
verts=[(-.83,1.08,1.04),(.83,1.08,1.04),(.82,.98,2.17),(-.82,.98,2.17),
       (-.80,1.15,1.05),(.80,1.15,1.05),(.78,1.065,2.14),(-.78,1.065,2.14)]
mesh('Tapered hood',verts,[(0,3,2,1),(4,5,6,7),(0,1,5,4),(1,2,6,5),(2,3,7,6),(3,0,4,7)],paint,bevel=.035)
for x in [-.44,.44]:beam('Hood pressing',(x,1.153,1.16),(x,1.082,2.03),.014,paint2)
box('Cab back lower',(0,.95,-.25),(1.62,.53,.07),paint)
box('Cab firewall',(0,.88,1.01),(1.65,.55,.10),trim)
for side in [-1,1]:box('Rear window side surround',(side*.755,1.54,-.24),(.14,.67,.09),paint)
box('Cab rear roof header',(0,1.86,-.225),(1.53,.095,.09),paint)
mesh('Rear glass',[(-.71,1.29,-.262),(.71,1.29,-.262),(.70,1.78,-.232),(-.70,1.78,-.232)],[(0,1,2,3)],glass,'Glass')
box('Rear window lower frame',(0,1.245,-.245),(1.54,.08,.08),paint)
box('Roof',(0,1.92,.29),(1.60,.095,1.14),paint,bevel=.055)
beam('Front windshield upper frame',(-.77,1.86,.78),(.77,1.86,.78),.064,paint)
beam('Front windshield lower frame',(-.83,1.15,1.03),(.83,1.15,1.03),.055,trim)
windshield=[(-.81,1.17,1.03),(.81,1.17,1.03),(.755,1.86,.78),(-.755,1.86,.78)]
mesh('Windshield',windshield,[(0,1,2,3)],glass,'Glass')
for i in range(4):beam('Windshield rubber seal',windshield[i],windshield[(i+1)%4],.032,trim)
for x in [-.35,.33]:
    beam('Wiper arm',(x,1.175,1.055),(x-.10,1.37,.997),.014,trim)
    beam('Wiper rubber',(x-.26,1.38,.998),(x+.10,1.38,.998),.018,trim)

# Front fascia: inset grille, two circular lenses, indicators and a bent bumper.
box('Front fascia',(0,.865,2.15),(1.73,.35,.10),paint)
box('Recessed grille',(0,.865,2.214),(1.04,.24,.024),trim)
for x in [-.43,-.31,-.19,-.07,.07,.19,.31,.43]:box('Grille vertical',(x,.865,2.232),(.018,.215,.016),steel,.004)
for y in [.79,.855,.92]:box('Grille crossbar',(0,y,2.245),(1.01,.012,.01),steel,.003)
def disc(name,p,r,depth,m,group='Body',normal=(0,0,1),segments=24):
    bpy.ops.mesh.primitive_cylinder_add(vertices=segments,radius=r,depth=depth,location=V(p));o=bpy.context.object;o.name=name
    o.rotation_mode='QUATERNION';o.rotation_quaternion=Vector((0,0,1)).rotation_difference(V(normal))
    return add(o,m,group)
for side in [-1,1]:
    disc('Headlight bezel',(side*.65,.887,2.219),.142,.035,trim)
    disc('Chrome headlight ring',(side*.65,.887,2.240),.126,.018,steel)
    disc('Fluted headlight lens',(side*.65,.887,2.253),.108,.012,lamp)
    for n in [-2,-1,0,1,2]:beam('Lens fluting',(side*.65+n*.03,.80,2.263),(side*.65+n*.03,.974,2.263),.004,steel)
    box('Front indicator',(side*.71,.69,2.224),(.22,.06,.023),amber,.008)
    box('Rear combination lamp',(side*.79,.96,-2.216),(.12,.23,.035),trim,.016)
    box('Brake lens',(side*.79,1.01,-2.241),(.095,.11,.011),red,.008)
    box('Reverse lens',(side*.79,.90,-2.241),(.095,.045,.011),lamp,.004)
box('Chrome front bumper',(0,.555,2.26),(1.87,.13,.16),steel,.035)
box('Rear bumper',(0,.50,-2.24),(1.83,.12,.15),rust,.025)
box('Tailgate',(0,.95,-2.19),(1.6,.36,.06),paint,.018)
box('Tailgate recess',(0,.97,-2.228),(1.19,.17,.008),paint2,.008)
box('Tailgate handle',(0,1.09,-2.247),(.18,.04,.028),trim,.008)
for x in [-.54,.54]:box('Tailgate hinge',(x,.766,-2.235),(.14,.04,.04),steel,.006)
box('License plate',(0,.61,-2.329),(.34,.105,.013),paint2,.006)
for x in [-.09,-.045,0,.045,.09]:box('Faded plate mark',(x,.61,-2.338),(.021,.055,.004),trim,.002)

# Interior, seen through the windows and from the driver's seat.
box('Dashboard',(0,1.10,.86),(1.47,.20,.30),trim,.045)
box('Gauge binnacle',(-.39,1.21,.83),(.43,.09,.20),dark,.025)
for x in [-.49,-.31]:
    disc('Analog instrument',(x,1.195,.720),.065,.008,steel,normal=(0,0,-1))
    disc('Instrument face',(x,1.195,.712),.052,.008,trim,normal=(0,0,-1))
    beam('Gauge needle',(x,1.195,.704),(x+.018,1.223,.704),.004,lamp)
for x in [.25,.45]:box('Dashboard vent',(x,1.13,.697),(.14,.065,.018),dark,.006)
box('Bench cushion',(0,.78,.27),(1.43,.19,.61),seat,.07)
box('Bench back',(0,1.03,-.035),(1.43,.53,.17),seat,.07)
for x in [-.55,-.3,0,.3,.55]:beam('Vinyl stitched seam',(x,.879,.05),(x,.879,.49),.006,foam)
box('Exposed foam',(-.47,1.04,.057),(.16,.12,.012),foam,.015)
beam('Gear lever',(.05,.59,.50),(.05,.96,.39),.021,steel)
disc('Gear knob',(.05,.98,.39),.038,.06,trim,normal=(0,1,0),segments=12)
beam('Steering column',(-.40,.86,.86),(-.40,1.14,.61),.04,trim)
# A real ring, spokes and horn button; its origin is the steering pivot.
steerCenter=Vector((-.40,1.14,.61))
for i in range(32):
    a=i*math.tau/32;b=(i+1)*math.tau/32
    p=steerCenter+Vector((math.cos(a)*.175,math.sin(a)*.145,math.sin(a)*.10))
    q=steerCenter+Vector((math.cos(b)*.175,math.sin(b)*.145,math.sin(b)*.10))
    beam('Steering rim',p,q,.022,trim,'Steering')
for a in [math.pi/6,math.pi*5/6,math.pi*1.5]:
    beam('Steering spoke',steerCenter,steerCenter+Vector((math.cos(a)*.163,math.sin(a)*.135,math.sin(a)*.094)),.022,steel,'Steering')
disc('Horn',steerCenter,.046,.024,trim,'Steering',normal=(0,.55,-.8),segments=16)

# Four separately rigged wheels: rounded tire shoulders, inset stamped rims,
# sidewall rings, tread blocks, central hubs and five lug nuts.
wheel_centers={}
for side in [-1,1]:
    for front,zc in [(True,1.40),(False,-1.25)]:
        name='Wheel_'+('F' if front else 'R')+('L' if side<0 else 'R');center=(side*.835,.38,zc);wheel_centers[name]=center
        rings=[(-.135,.275),(-.145,.315),(-.11,.366),(-.07,.38),(.07,.38),(.11,.366),(.145,.315),(.135,.275)]
        vs=[(center[0]+x,center[1]+r*math.sin(i*math.tau/32),center[2]+r*math.cos(i*math.tau/32)) for x,r in rings for i in range(32)]
        fs=[]
        for j in range(len(rings)-1):
            for i in range(32):fs.append((j*32+i,j*32+(i+1)%32,(j+1)*32+(i+1)%32,(j+1)*32+i))
        mesh('Tire carcass',vs,fs,dark,name)
        outer=center[0]+side*.141
        disc('Stamped wheel rim',(outer,center[1],zc),.262,.020,steel,name,normal=(1,0,0))
        disc('Rim recess',(outer+side*.014,center[1],zc),.204,.013,trim,name,normal=(1,0,0))
        disc('Steel hub',(outer+side*.024,center[1],zc),.139,.023,steel,name,normal=(1,0,0),segments=16)
        for i in range(5):
            a=i*math.tau/5;disc('Lug nut',(outer+side*.04,center[1]+math.sin(a)*.087,zc+math.cos(a)*.087),.015,.023,rust2,name,normal=(1,0,0),segments=6)
        for i in range(32):
            a=i*math.tau/32
            p=(center[0]-.065,center[1]+math.sin(a)*.381,zc+math.cos(a)*.381)
            q=(center[0]+.065,center[1]+math.sin(a+.05)*.381,zc+math.cos(a+.05)*.381)
            beam('Tread ridge',p,q,.012,trim,name)
        # Axles are attached to the chassis, not to the spinning wheel.
for z in [-1.25,1.40]:
    beam('Axle',(-.78,.37,z),(.78,.37,z),.09,steel)
    for x in [-.53,.53]:beam('Leaf spring',(x,.32,z-.37),(x,.31,z+.37),.035,rust)
beam('Exhaust pipe',(.58,.30,.90),(.66,.30,-2.12),.048,steel)

for group,objects in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects:o.select_set(True)
    bpy.context.view_layer.objects.active=objects[0];bpy.ops.object.join()
    o=bpy.context.object;o.name=group
    bpy.context.scene.cursor.location=V(wheel_centers.get(group,steerCenter if group=='Steering' else (0,0,0)))
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
    # Consistent face normals on concave wheel arches and closed stampings.
    bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.mesh.normals_make_consistent(inside=False);bpy.ops.object.mode_set(mode='OBJECT')

bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=str(OUT/'FarmPickup.fbx'),use_selection=True,add_leaf_bones=False,bake_anim=False,
    axis_forward='-Z',axis_up='Y',apply_unit_scale=True,apply_scale_options='FBX_SCALE_ALL')
(ROOT/'Tools/SourceArt').mkdir(exist_ok=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT/'Tools/SourceArt/FarmPickup.blend'))

# Neutral studio render to inspect the shape before installing it in the game.
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24
scene.world=bpy.data.worlds.new('StudioWorld');scene.world.color=(.22,.22,.22)
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,0));floor=bpy.context.object;floor.name='Studio floor'
floor.data.materials.append(mat('StudioGray',(.16,.18,.19)))
for name,pos,power,size in [('Key',(-3,6,4),1100,5),('Fill',(4,4,1),850,4),('Rim',(0,5,-5),1200,3)]:
    bpy.ops.object.light_add(type='AREA',location=V(pos));o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size
    o.rotation_euler=(V((0,1,0))-o.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(location=V((-5.9,3.2,6.8)));camera=bpy.context.object;camera.rotation_euler=(V((0,.92,0))-camera.location).to_track_quat('-Z','Y').to_euler()
camera.data.type='ORTHO';camera.data.ortho_scale=6.2;scene.camera=camera
scene.render.resolution_x=1400;scene.render.resolution_y=1000;scene.render.resolution_percentage=100
scene.render.filepath=str(ROOT/'output/menu-review/truck-studio.png')
bpy.ops.render.render(write_still=True)
print('PICKUP_MODEL_COMPLETE')
