using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using Unity.XR.CoreUtils;
using Object=UnityEngine.Object;

public static class ARPlanningBuilder
{
    const string Folder="Assets/ChickenHeistGenerated/AR/",Scene="Assets/Scenes/PlanejamentoRA.unity",Out="output/ar";
    public const string Apk="Builds/Android/ChickenHeist-PlanejamentoRA.apk";
    const float ModelSize=.6f;

    public static void BuildAll(){BuildModels();BuildScene();ConfigureAndroid();}

    public static void BuildModels()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);Physics.SyncTransforms();
        Directory.CreateDirectory(Folder+"Meshes");Directory.CreateDirectory(Folder+"Materials");Directory.CreateDirectory(Out);
        AssetDatabase.Refresh();
        var log=new List<string>();
        var farms=Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None).OrderBy(f=>f.name).ToArray();
        var allRenderers=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        var farmers=Object.FindObjectsByType<FarmerStateMachine>(FindObjectsSortMode.None);
        var cameras=Object.FindObjectsByType<SecurityCamera>(FindObjectsSortMode.None);
        var chickens=Object.FindObjectsByType<InteractableChicken>(FindObjectsSortMode.None);
        for(int f=0;f<farms.Length;f++)log.Add(BuildModel(farms[f],f,farms,allRenderers,farmers,cameras,chickens));
        AssetDatabase.SaveAssets();
        File.WriteAllLines(Out+"/models.txt",log);Debug.Log("AR MODELS\n"+string.Join("\n",log));
    }

    static string BuildModel(FarmLayoutInfo farm,int index,FarmLayoutInfo[] farms,MeshRenderer[] all,FarmerStateMachine[] farmers,SecurityCamera[] cams,InteractableChicken[] birds)
    {
        var lot=farm.lot;Rect area=new Rect(lot.xMin-4,lot.yMin-4,lot.width+8,lot.height+8);
        bool Inside(Vector3 p)=>area.Contains(new Vector2(p.x,p.z));
        float GroundAt(float x,float z)
        {
            float low=float.PositiveInfinity;
            foreach(var h in Physics.RaycastAll(new Vector3(x,400,z),Vector3.down,800,~0,QueryTriggerInteraction.Ignore))
                if(h.collider.GetComponentInParent<FarmLayoutInfo>()==null || h.collider.name.Contains("Terreno"))low=Mathf.Min(low,h.point.y);
            return float.IsPositiveInfinity(low)?0:low;
        }
        Vector3 centre=new Vector3(lot.center.x,0,lot.center.y);
        Vector3 Local(Vector3 world)=>world-centre;
        bool Moving(Component c)=>c.GetComponentInParent<InteractableChicken>()!=null || c.GetComponentInParent<FarmerStateMachine>()!=null || c.GetComponentInParent<Animation>()!=null
            || c.GetComponentInParent<Animator>()!=null || c.GetComponentInParent<SecurityCamera>()!=null || c.GetComponentInParent<HeistGameManager>()!=null;
        float diagonal=lot.size.magnitude;
        var parts=all.Where(r=>r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy && r.GetComponent<MeshFilter>()?.sharedMesh!=null
            && (r.transform.IsChildOf(farm.transform) || (Inside(r.bounds.center) && r.bounds.size.magnitude<diagonal*.5f && !farms.Any(o=>o!=farm && r.transform.IsChildOf(o.transform))))
            && !r.name.Contains("Terreno") && !Moving(r)).ToArray();
        var groups=new Dictionary<Material,List<CombineInstance>>();
        var toCentre=Matrix4x4.Translate(-centre);
        foreach(var r in parts)
        {
            var mesh=r.GetComponent<MeshFilter>().sharedMesh;var mats=r.sharedMaterials;
            for(int s=0;s<mesh.subMeshCount && s<mats.Length;s++)
            {
                if(mats[s]==null)continue;
                if(!groups.TryGetValue(mats[s],out var list))groups[mats[s]]=list=new List<CombineInstance>();
                list.Add(new CombineInstance{mesh=mesh,subMeshIndex=s,transform=toCentre*r.localToWorldMatrix});
            }
        }
        var merged=new List<CombineInstance>();var materials=new List<Material>();var temps=new List<Mesh>();
        foreach(var kv in groups)
        {
            var m=new Mesh{indexFormat=IndexFormat.UInt32};m.CombineMeshes(kv.Value.ToArray(),true,true);temps.Add(m);
            merged.Add(new CombineInstance{mesh=m,transform=Matrix4x4.identity});materials.Add(kv.Key);
        }
        var buildings=new Mesh{name="Maquete "+farm.identity,indexFormat=IndexFormat.UInt32};buildings.CombineMeshes(merged.ToArray(),false,false);
        buildings.RecalculateBounds();foreach(var t in temps)Object.DestroyImmediate(t);
        const int N=32;var heights=new float[N+1,N+1];float lowest=float.PositiveInfinity;
        for(int i=0;i<=N;i++)for(int j=0;j<=N;j++){float h=GroundAt(area.xMin+area.width*i/N,area.yMin+area.height*j/N);heights[i,j]=h;lowest=Mathf.Min(lowest,h);}
        float floor=lowest-1.5f;
        var slab=Slab(area,centre,heights,N,floor);slab.name="Chao "+farm.identity;
        string tag=(index+1).ToString("00");
        buildings=SaveMesh(buildings,Folder+"Meshes/Fazenda_"+tag+"_construcoes.asset");slab=SaveMesh(slab,Folder+"Meshes/Fazenda_"+tag+"_chao.asset");

        var root=new GameObject("Maquete - "+farm.identity);var model=root.AddComponent<ARFarmModel>();
        string[] id=farm.identity.Split(new[]{" - "},StringSplitOptions.None);
        model.farmName=id[0];model.kind=id.Length>1?id[1]:"";
        float scale=ModelSize/Mathf.Max(area.width,area.height);
        var scaled=new GameObject("Escala 1:"+Mathf.RoundToInt(1/scale)).transform;scaled.SetParent(root.transform,false);
        scaled.localScale=Vector3.one*scale;scaled.localPosition=new Vector3(0,-floor*scale,0);model.scaled=scaled;
        Renderer Piece(string name,Mesh mesh,Material[] mats,Transform parent)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterials=mats;mr.shadowCastingMode=ShadowCastingMode.On;return mr;
        }
        Piece("Chao",slab,new[]{Mat("Grama da maquete",new Color(.36f,.52f,.25f)),Mat("Terra da maquete",new Color(.42f,.30f,.20f))},scaled);
        Piece("Construcoes",buildings,materials.ToArray(),scaled);
        var farmer=farmers.FirstOrDefault(x=>Inside(x.transform.position));
        model.route=new GameObject("Ronda").transform;model.route.SetParent(scaled,false);
        if(farmer!=null && farmer.patrolPoints!=null && farmer.patrolPoints.Length>1)
        {
            var pts=farmer.patrolPoints.Where(p=>p!=null).Select(p=>{var w=p.position;w.y=GroundAt(w.x,w.z)+.35f;return Local(w);}).ToArray();
            model.patrol=pts;model.patrolSpeed=farmer.patrolSpeed;model.guards=1;
            for(int i=0;i<pts.Length;i++)model.patrolLength+=Vector3.Distance(pts[i],pts[(i+1)%pts.Length]);
            var ribbon=Ribbon(pts,1.3f,(x,z)=>GroundAt(x+centre.x,z+centre.z)+.35f);ribbon.name="Ronda "+farm.identity;ribbon=SaveMesh(ribbon,Folder+"Meshes/Fazenda_"+tag+"_ronda.asset");
            Piece("Caminho da ronda",ribbon,new[]{Mat("Ronda",new Color(.95f,.25f,.2f,.75f),true)},model.route).shadowCastingMode=ShadowCastingMode.Off;
            var token=new GameObject("Fazendeiro").transform;token.SetParent(model.route,false);token.localPosition=pts[0];
            var body=Primitive(PrimitiveType.Capsule,token,new Vector3(0,2.4f,0),new Vector3(2.2f,2.4f,2.2f),Mat("Fazendeiro",new Color(.85f,.18f,.14f)));
            Primitive(PrimitiveType.Sphere,token,new Vector3(0,5.4f,0),Vector3.one*1.8f,Mat("Fazendeiro",new Color(.85f,.18f,.14f)));
            Primitive(PrimitiveType.Cube,token,new Vector3(0,3f,1.4f),new Vector3(.6f,.6f,1.6f),Mat("Lanterna",new Color(1,.92f,.6f)));
            model.token=token;
        }
        model.cones=new GameObject("Cameras").transform;model.cones.SetParent(scaled,false);
        var sweeps=new List<ARFarmModel.Sweep>();var cone=SaveMesh(Cone(),Folder+"Meshes/Cone_de_visao.asset");
        foreach(var c in cams.Where(c=>Inside(c.transform.position)))
        {
            var mount=new GameObject("Camera de seguranca").transform;mount.SetParent(model.cones,false);mount.localPosition=Local(c.Eye);
            Primitive(PrimitiveType.Cube,mount,Vector3.zero,new Vector3(1.6f,1.2f,2.4f),Mat("Camera",new Color(.15f,.16f,.18f)));
            var head=new GameObject("Varredura").transform;head.SetParent(mount,false);
            var view=new GameObject("Cone de visao");view.transform.SetParent(head,false);view.transform.localScale=new Vector3(c.viewDistance,c.viewDistance,c.viewDistance);
            view.transform.localRotation=Quaternion.Euler(18,0,0);
            view.AddComponent<MeshFilter>().sharedMesh=cone;var vr=view.AddComponent<MeshRenderer>();vr.sharedMaterial=Mat("Visao da camera",new Color(1,.85f,.25f,.32f),true);vr.shadowCastingMode=ShadowCastingMode.Off;
            var fwd=(c.scanHead!=null?c.scanHead:c.transform).forward;fwd.y=0;
            float yaw=fwd.sqrMagnitude>1e-4f?Quaternion.LookRotation(fwd).eulerAngles.y:c.transform.eulerAngles.y;
            sweeps.Add(new ARFarmModel.Sweep{head=head,arc=c.rotationArc,speed=c.rotationSpeed,startYaw=yaw});
        }
        model.sweeps=sweeps.ToArray();model.cameras=sweeps.Count;
        model.birds=new GameObject("Galinhas").transform;model.birds.SetParent(scaled,false);
        foreach(var b in birds.Where(b=>Inside(b.transform.position)))
        {
            var p=b.transform.position;p.y=GroundAt(p.x,p.z);
            var marker=new GameObject("Galinha").transform;marker.SetParent(model.birds,false);marker.localPosition=Local(p);
            Primitive(PrimitiveType.Sphere,marker,new Vector3(0,1.3f,0),new Vector3(1.6f,1.5f,2f),Mat("Galinha",new Color(.98f,.93f,.82f)));
            Primitive(PrimitiveType.Cube,marker,new Vector3(0,2.2f,.7f),new Vector3(.4f,.6f,.5f),Mat("Crista",new Color(.86f,.14f,.12f)));
            model.chickens++;
        }
        model.gate=new GameObject("Entrada").transform;model.gate.SetParent(scaled,false);
        var gatePoint=farm.entrance;gatePoint.y=GroundAt(gatePoint.x,gatePoint.z);model.gate.localPosition=Local(gatePoint);
        var inward=centre-farm.entrance;inward.y=0;model.gate.localRotation=Quaternion.LookRotation(inward.sqrMagnitude>1e-4f?inward:Vector3.forward);
        var arrow=SaveMesh(Arrow(),Folder+"Meshes/Seta_de_entrada.asset");
        var ag=new GameObject("Seta");ag.transform.SetParent(model.gate,false);ag.transform.localPosition=new Vector3(0,.6f,-4);ag.transform.localScale=Vector3.one*4;
        ag.AddComponent<MeshFilter>().sharedMesh=arrow;ag.AddComponent<MeshRenderer>().sharedMaterial=Mat("Entrada",new Color(.35f,.85f,.4f));
        model.labels=new GameObject("Rotulos").transform;model.labels.SetParent(scaled,false);
        float top=parts.Length>0?parts.Max(r=>r.bounds.max.y):floor+10;
        model.labelTexts=new[]{model.farmName,"Entrada"};
        model.labelPositions=new[]{new Vector3(0,top+6,0),Local(gatePoint)+Vector3.up*9};
        model.labelSizes=new[]{7f,4.5f};
        model.labels.SetAsLastSibling();
        string path=Folder+"Fazenda_"+tag+".prefab";
        PrefabUtility.SaveAsPrefabAsset(root,path);Object.DestroyImmediate(root);
        return $"{tag} {farm.identity}: {parts.Length} pieces in {materials.Count} materials, {buildings.vertexCount} vertices, scale 1:{Mathf.RoundToInt(1/scale)}, patrol {(model.patrol?.Length??0)} points ({model.patrolLength:0} m), cameras {model.cameras}, chickens {model.chickens}";
    }

    static Mesh SaveMesh(Mesh mesh,string path)
    {
        var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}
        old.Clear();EditorUtility.CopySerialized(mesh,old);old.name=mesh.name;EditorUtility.SetDirty(old);Object.DestroyImmediate(mesh);return old;
    }
    static Material Mat(string name,Color color,bool transparent=false)
    {
        string path=Folder+"Materials/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find(transparent?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.15f);
        if(transparent)
        {
            m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);
            m.SetOverrideTag("RenderType","Transparent");m.renderQueue=(int)RenderQueue.Transparent;
            m.SetInt("_SrcBlend",(int)BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)BlendMode.OneMinusSrcAlpha);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        EditorUtility.SetDirty(m);return m;
    }
    static GameObject Primitive(PrimitiveType type,Transform parent,Vector3 position,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(type);Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;
    }
    static Mesh Slab(Rect area,Vector3 centre,float[,] h,int n,float floor)
    {
        var v=new List<Vector3>();var top=new List<int>();var sides=new List<int>();
        for(int i=0;i<=n;i++)for(int j=0;j<=n;j++)v.Add(new Vector3(area.xMin+area.width*i/n-centre.x,h[i,j],area.yMin+area.height*j/n-centre.z));
        for(int i=0;i<n;i++)for(int j=0;j<n;j++){int a=i*(n+1)+j,b=a+n+1;top.AddRange(new[]{a,a+1,b,b,a+1,b+1});}
        var ring=new List<int>();
        for(int j=0;j<=n;j++)ring.Add(j);for(int i=1;i<=n;i++)ring.Add(i*(n+1)+n);for(int j=n-1;j>=0;j--)ring.Add(n*(n+1)+j);for(int i=n-1;i>0;i--)ring.Add(i*(n+1));
        int baseStart=v.Count;foreach(int k in ring){var p=v[k];v.Add(new Vector3(p.x,floor,p.z));}
        for(int k=0;k<ring.Count;k++)
        {
            int a=ring[k],b=ring[(k+1)%ring.Count],c=baseStart+k,d=baseStart+(k+1)%ring.Count;
            sides.AddRange(new[]{a,c,b,b,c,d});
        }
        int centreBottom=v.Count;v.Add(new Vector3(area.center.x-centre.x,floor,area.center.y-centre.z));
        for(int k=0;k<ring.Count;k++)sides.AddRange(new[]{centreBottom,baseStart+(k+1)%ring.Count,baseStart+k});
        var mesh=new Mesh{indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.subMeshCount=2;mesh.SetTriangles(top,0);mesh.SetTriangles(sides,1);
        mesh.RecalculateNormals();
        if(mesh.normals[n/2*(n+1)+n/2].y<0){top.Reverse();sides.Reverse();mesh.SetTriangles(top,0);mesh.SetTriangles(sides,1);mesh.RecalculateNormals();}
        mesh.RecalculateBounds();return mesh;
    }
    static Mesh Ribbon(Vector3[] loop,float width,Func<float,float,float> ground)
    {
        var path=new List<Vector3>();
        for(int i=0;i<loop.Length;i++)
        {
            Vector3 a=loop[i],b=loop[(i+1)%loop.Length];int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(a,b)/2));
            for(int s=0;s<steps;s++){var p=Vector3.Lerp(a,b,(float)s/steps);p.y=ground(p.x,p.z);path.Add(p);}
        }
        var v=new List<Vector3>();var t=new List<int>();
        for(int i=0;i<path.Count;i++)
        {
            var dir=path[(i+1)%path.Count]-path[(i-1+path.Count)%path.Count];dir.y=0;var side=Vector3.Cross(Vector3.up,dir.normalized)*width*.5f;
            v.Add(path[i]-side);v.Add(path[i]+side);
        }
        for(int i=0;i<path.Count;i++){int a=i*2,b=((i+1)%path.Count)*2;t.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
        var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    static Mesh Cone()
    {
        var v=new List<Vector3>{Vector3.zero};var t=new List<int>();const int n=16;
        for(int i=0;i<=n;i++){float a=Mathf.Lerp(-21,21,(float)i/n)*Mathf.Deg2Rad;v.Add(new Vector3(Mathf.Sin(a),0,Mathf.Cos(a)));}
        for(int i=1;i<=n;i++)t.AddRange(new[]{0,i,i+1});
        var m=new Mesh{name="Cone de visao"};m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
    }
    static Mesh Arrow()
    {
        var v=new[]{new Vector3(-.25f,0,-1),new Vector3(.25f,0,-1),new Vector3(.25f,0,.2f),new Vector3(.6f,0,.2f),new Vector3(0,0,1),new Vector3(-.6f,0,.2f),new Vector3(-.25f,0,.2f)};
        var t=new[]{0,6,1,1,6,2,6,4,2,2,4,3,6,5,4};
        var m=new Mesh{name="Seta de entrada"};m.vertices=v;m.triangles=t;m.RecalculateNormals();if(m.normals[0].y<0){Array.Reverse(t);m.triangles=t;m.RecalculateNormals();}
        m.RecalculateBounds();return m;
    }

    public static void BuildScene()
    {
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var sun=new GameObject("Luz").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.15f;sun.shadows=LightShadows.Soft;sun.transform.rotation=Quaternion.Euler(55,-35,0);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.56f,.6f);
        var sessionGo=new GameObject("AR Session");var session=sessionGo.AddComponent<ARSession>();sessionGo.AddComponent<ARInputManager>();
        var originGo=new GameObject("XR Origin");var origin=originGo.AddComponent<XROrigin>();
        var offset=new GameObject("Camera Offset");offset.transform.SetParent(originGo.transform,false);
        var camGo=new GameObject("Main Camera");camGo.tag="MainCamera";camGo.transform.SetParent(offset.transform,false);
        var cam=camGo.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;cam.nearClipPlane=.03f;cam.farClipPlane=30;
        camGo.AddComponent<AudioListener>();camGo.AddComponent<ARCameraManager>();var background=camGo.AddComponent<ARCameraBackground>();
        var pose=camGo.AddComponent<TrackedPoseDriver>();
        var position=new InputAction("Position",binding:"<XRHMD>/centerEyePosition",expectedControlType:"Vector3");position.AddBinding("<HandheldARInputDevice>/devicePosition");
        var rotation=new InputAction("Rotation",binding:"<XRHMD>/centerEyeRotation",expectedControlType:"Quaternion");rotation.AddBinding("<HandheldARInputDevice>/deviceRotation");
        pose.positionInput=new InputActionProperty(position);pose.rotationInput=new InputActionProperty(rotation);
        origin.Camera=cam;origin.CameraFloorOffsetObject=offset;
        var planes=originGo.AddComponent<ARPlaneManager>();planes.requestedDetectionMode=PlaneDetectionMode.Horizontal;planes.planePrefab=PlanePrefab();
        var raycasts=originGo.AddComponent<ARRaycastManager>();
        var planner=new GameObject("Planejamento do roubo").AddComponent<ARHeistPlanner>();
        planner.session=session;planner.planes=planes;planner.raycasts=raycasts;planner.background=background;planner.view=cam;
        planner.font=AssetDatabase.LoadAssetAtPath<Font>("Assets/Resources/UIFonts/Barlow-SemiBold.ttf");
        planner.farms=AssetDatabase.FindAssets("Fazenda_ t:Prefab",new[]{Folder.TrimEnd('/')}).Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).Select(AssetDatabase.LoadAssetAtPath<GameObject>).ToArray();
        EditorSceneManager.SaveScene(scene,Scene);
        Debug.Log("AR SCENE "+Scene+" with "+planner.farms.Length+" farms");
    }
    static GameObject PlanePrefab()
    {
        string path=Folder+"Plano detectado.prefab";
        var go=new GameObject("Plano detectado",typeof(ARPlane),typeof(ARPlaneMeshVisualizer),typeof(MeshFilter),typeof(MeshRenderer));
        var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=Mat("Plano detectado",new Color(.95f,.75f,.35f,.18f),true);r.shadowCastingMode=ShadowCastingMode.Off;
        var prefab=PrefabUtility.SaveAsPrefabAsset(go,path);Object.DestroyImmediate(go);return prefab;
    }

    public static void ConfigureAndroid()
    {
        if(!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey,out XRGeneralSettingsPerBuildTarget perTarget) || perTarget==null)
        {
            Directory.CreateDirectory("Assets/XR");perTarget=ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(perTarget,"Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey,perTarget,true);
        }
        if(!perTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))perTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
        var general=perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);general.InitManagerOnStart=true;
        XRPackageMetadataStore.AssignLoader(general.Manager,"UnityEngine.XR.ARCore.ARCoreLoader",BuildTargetGroup.Android);
        var arcore=UnityEditor.XR.ARCore.ARCoreSettings.GetOrCreateSettings();arcore.requirement=UnityEditor.XR.ARCore.ARCoreSettings.Requirement.Required;arcore.depth=UnityEditor.XR.ARCore.ARCoreSettings.Requirement.Optional;EditorUtility.SetDirty(arcore);
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,"com.pedromancini.chickenheist.planejamento");
        PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.OpenGLES3});
        PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
        var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/Mobile_Renderer.asset");
        if(renderer!=null && !renderer.rendererFeatures.Any(f=>f is ARBackgroundRendererFeature))
        {
            var feature=ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();feature.name="ARBackgroundRendererFeature";
            AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);
            var so=new SerializedObject(renderer);var map=so.FindProperty("m_RendererFeatureMap");map.arraySize++;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature,out _,out long id);map.GetArrayElementAtIndex(map.arraySize-1).longValue=id;so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("AR ANDROID CONFIGURED");
    }

    public static void BuildAndroid()
    {
        ConfigureAndroid();
        string name=PlayerSettings.productName;var orientation=PlayerSettings.defaultInterfaceOrientation;
        PlayerSettings.productName="Chicken Heist - Planejamento RA";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Apk));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{Scene},locationPathName=Apk,target=BuildTarget.Android,options=BuildOptions.None});
            Directory.CreateDirectory(Out);
            File.WriteAllText(Out+"/android-build.txt",report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nSize: "+report.summary.totalSize/1048576+" MB\nTime: "+report.summary.totalTime);
            Debug.Log("AR ANDROID BUILD "+report.summary.result);
            if(Application.isBatchMode)EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:1);
        }
        finally{PlayerSettings.productName=name;PlayerSettings.defaultInterfaceOrientation=orientation;AssetDatabase.SaveAssets();}
    }

    public static void Preview()
    {
        EditorSceneManager.OpenScene(Scene);Directory.CreateDirectory(Out);
        var planner=Object.FindAnyObjectByType<ARHeistPlanner>();var cam=planner.view;
        cam.transform.SetParent(null,true);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.62f,.64f,.66f);
        var table=GameObject.CreatePrimitive(PrimitiveType.Cube);table.transform.position=new Vector3(0,-.02f,0);table.transform.localScale=new Vector3(1.6f,.04f,1.1f);
        table.GetComponent<Renderer>().sharedMaterial=Mat("Mesa (previa)",new Color(.45f,.33f,.24f));
        var rt=new RenderTexture(1280,800,24);cam.targetTexture=rt;cam.fieldOfView=50;
        var sheet=new Texture2D(1280*3,800*4,TextureFormat.RGB24,false);
        var warm=(GameObject)PrefabUtility.InstantiatePrefab(planner.farms[0]);cam.Render();Object.DestroyImmediate(warm);
        for(int i=0;i<planner.farms.Length && i<12;i++)
        {
            var model=((GameObject)PrefabUtility.InstantiatePrefab(planner.farms[i])).GetComponent<ARFarmModel>();
            model.transform.rotation=Quaternion.Euler(0,20,0);
            cam.transform.position=new Vector3(0,.48f,-.62f);cam.transform.LookAt(new Vector3(0,.04f,0));
            cam.Render();RenderTexture.active=rt;sheet.ReadPixels(new Rect(0,0,1280,800),(i%3)*1280,(3-i/3)*800);
            var one=new Texture2D(1280,800,TextureFormat.RGB24,false);one.ReadPixels(new Rect(0,0,1280,800),0,0);one.Apply();
            File.WriteAllBytes($"{Out}/fazenda-{i+1:00}.png",one.EncodeToPNG());Object.DestroyImmediate(one);RenderTexture.active=null;
            Object.DestroyImmediate(model.gameObject);
        }
        sheet.Apply();File.WriteAllBytes(Out+"/maquetes.png",sheet.EncodeToPNG());cam.targetTexture=null;
        Debug.Log("AR PREVIEW "+Out);
    }
}
