using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class PalmFarmBuilder
{
    const string Folder="Assets/ChickenHeistGenerated/PalmFarms/",Prefabs="Assets/Resources/PalmFarms/";
    const int Rings=9,Segments=44;
    const float Marker=1.7f;

    [MenuItem("Chicken Heist/Build Palm Farm Models")]
    public static void Build()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);Physics.SyncTransforms();
        Directory.CreateDirectory(Folder+"Meshes");Directory.CreateDirectory(Prefabs);Directory.CreateDirectory("output/palm-view");
        AssetDatabase.Refresh();
        var farms=Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None).OrderBy(f=>f.identity,StringComparer.Ordinal).ToArray();
        var all=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);
        var farmers=Object.FindObjectsByType<FarmerStateMachine>(FindObjectsSortMode.None);
        var cams=Object.FindObjectsByType<SecurityCamera>(FindObjectsSortMode.None);
        var birds=Object.FindObjectsByType<InteractableChicken>(FindObjectsSortMode.None);
        var shadow=ShadowMaterial();
        var log=new List<string>();
        for(int i=0;i<farms.Length;i++)log.Add(BuildFarm(farms[i],i,farms,all,farmers,cams,birds,shadow));
        AssetDatabase.SaveAssets();
        File.WriteAllLines("output/palm-view/models.txt",log);Debug.Log("PALM MODELS\n"+string.Join("\n",log));
    }

    static string BuildFarm(FarmLayoutInfo farm,int index,FarmLayoutInfo[] farms,MeshRenderer[] all,FarmerStateMachine[] farmers,SecurityCamera[] cams,InteractableChicken[] birds,Material shadow)
    {
        var lot=farm.lot;var centre=new Vector3(lot.center.x,0,lot.center.y);
        float radius=lot.size.magnitude*.5f+2;
        bool Inside(Vector3 p,float margin=0)=>new Vector2(p.x-centre.x,p.z-centre.z).magnitude<radius-margin;
        bool Neighbour(Transform t)=>farms.Any(o=>o!=farm && t.IsChildOf(o.transform));
        float GroundAt(float x,float z)
        {
            float low=float.PositiveInfinity;
            foreach(var h in Physics.RaycastAll(new Vector3(x,400,z),Vector3.down,800,~0,QueryTriggerInteraction.Ignore))
                if(h.collider.GetComponentInParent<FarmLayoutInfo>()==null || h.collider.name.Contains("Terreno"))low=Mathf.Min(low,h.point.y);
            return float.IsPositiveInfinity(low)?0:low;
        }
        Vector3 Local(Vector3 world)=>world-centre;
        bool Moving(Component c)=>c.GetComponentInParent<InteractableChicken>()!=null || c.GetComponentInParent<FarmerStateMachine>()!=null || c.GetComponentInParent<Animation>()!=null
            || c.GetComponentInParent<Animator>()!=null || c.GetComponentInParent<SecurityCamera>()!=null || c.GetComponentInParent<TrapSystem>()!=null || c.GetComponentInParent<HeistGameManager>()!=null;
        var parts=all.Where(r=>r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy && r.GetComponent<MeshFilter>()?.sharedMesh!=null
            && Inside(r.bounds.center,1) && r.bounds.size.magnitude<radius && r.bounds.size.magnitude>.9f && !Neighbour(r.transform)
            && !r.name.Contains("Terreno") && !r.name.StartsWith("Solo continuo") && !Moving(r)).ToArray();
        var groups=new Dictionary<Material,List<CombineInstance>>();var toCentre=Matrix4x4.Translate(-centre);
        var cube=Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        void Add(Material material,Mesh mesh,int sub,Matrix4x4 matrix)
        {
            if(!groups.TryGetValue(material,out var list))groups[material]=list=new List<CombineInstance>();
            list.Add(new CombineInstance{mesh=mesh,subMeshIndex=sub,transform=toCentre*matrix});
        }
        void Box(Material material,Renderer r,Vector3 middle,Vector3 size)=>Add(material,cube,0,r.localToWorldMatrix*Matrix4x4.TRS(middle,Quaternion.identity,size));
        var crop=Mat("Lavoura da maquete",new Color(.40f,.58f,.26f));var grain=Mat("Trigo da maquete",new Color(.80f,.68f,.33f));var fence=Mat("Cerca da maquete",new Color(.47f,.35f,.23f));
        foreach(var r in parts)
        {
            var mesh=r.GetComponent<MeshFilter>().sharedMesh;var b=mesh.bounds;
            if(mesh.name.StartsWith("Solo continuo"))continue;
            if(mesh.name.StartsWith("Crop_") || mesh.name.Contains("Wheat") || mesh.name.Contains("Corn"))
            {
                bool dry=mesh.name.Contains("Wheat") || mesh.name.Contains("Corn");
                Box(dry?grain:crop,r,b.center,new Vector3(b.size.x*.8f,b.size.y*.7f,b.size.z*.8f));continue;
            }
            if(mesh.name.Contains("Fence"))
            {
                bool alongX=b.size.x>=b.size.z;float length=alongX?b.size.x:b.size.z,h=b.size.y,t=Mathf.Max(.05f*h,.04f);
                var rail=alongX?new Vector3(length,t,t):new Vector3(t,t,length);var axis=alongX?Vector3.right:Vector3.forward;
                foreach(float k in new[]{.45f,.85f})Box(fence,r,new Vector3(b.center.x,b.min.y+h*k,b.center.z),rail);
                foreach(float k in new[]{-.5f,.5f})Box(fence,r,new Vector3(b.center.x,b.center.y,b.center.z)+axis*length*k*.94f,new Vector3(t*1.6f,h,t*1.6f));
                continue;
            }
            var mats=r.sharedMaterials;
            for(int s=0;s<mesh.subMeshCount && s<mats.Length;s++)if(mats[s]!=null)Add(mats[s],mesh,s,r.localToWorldMatrix);
        }
        var merged=new List<CombineInstance>();var materials=new List<Material>();var temps=new List<Mesh>();
        foreach(var kv in groups)
        {
            var m=new Mesh{indexFormat=IndexFormat.UInt32};m.CombineMeshes(kv.Value.ToArray(),true,true);temps.Add(m);
            merged.Add(new CombineInstance{mesh=m,transform=Matrix4x4.identity});materials.Add(kv.Key);
        }
        var buildings=new Mesh{name="Maquete de palma "+farm.identity,indexFormat=IndexFormat.UInt32};buildings.CombineMeshes(merged.ToArray(),false,false);
        buildings.tangents=null;buildings.uv2=null;buildings.uv3=null;buildings.uv4=null;
        buildings.RecalculateBounds();foreach(var t in temps)Object.DestroyImmediate(t);

        var heights=new float[Rings+1,Segments];float lowest=float.PositiveInfinity;
        for(int r=0;r<=Rings;r++)for(int s=0;s<Segments;s++)
        {
            var p=Polar(r,s,radius);float h=GroundAt(centre.x+p.x,centre.z+p.y);heights[r,s]=h;lowest=Mathf.Min(lowest,h);
            if(r==0)break;
        }
        float floor=lowest-radius*.16f;
        string tag=(index+1).ToString("00");
        var island=SaveMesh(Island(heights,radius,floor),Folder+"Meshes/Fazenda_"+tag+"_ilha.asset");
        buildings=SaveMesh(buildings,Folder+"Meshes/Fazenda_"+tag+"_construcoes.asset");

        var root=new GameObject("Maquete de palma - "+farm.identity);var model=root.AddComponent<ARFarmModel>();
        string[] id=farm.identity.Split(new[]{" - "},StringSplitOptions.None);
        model.farmName=id[0];model.kind=id.Length>1?id[1]:"";model.origin=centre;
        float scale=1/(radius*2);
        var scaled=new GameObject("Escala").transform;scaled.SetParent(root.transform,false);
        scaled.localScale=Vector3.one*scale;scaled.localPosition=new Vector3(0,-floor*scale,0);model.scaled=scaled;
        Renderer Piece(string name,Mesh mesh,Material[] mats,Transform parent)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var mr=go.AddComponent<MeshRenderer>();mr.sharedMaterials=mats;mr.shadowCastingMode=ShadowCastingMode.On;return mr;
        }
        Piece("Ilha",island,new[]{Mat("Grama da maquete",new Color(.36f,.52f,.25f)),Mat("Terra da maquete",new Color(.42f,.30f,.20f)),Mat("Base da maquete",new Color(.27f,.19f,.13f))},scaled);
        Piece("Construcoes",buildings,materials.ToArray(),scaled);
        var contact=GameObject.CreatePrimitive(PrimitiveType.Quad);Object.DestroyImmediate(contact.GetComponent<Collider>());contact.name="Sombra na mao";
        contact.transform.SetParent(root.transform,false);contact.transform.localRotation=Quaternion.Euler(90,0,0);contact.transform.localScale=Vector3.one*1.32f;contact.transform.localPosition=Vector3.up*.002f;
        var cr=contact.GetComponent<MeshRenderer>();cr.sharedMaterial=shadow;cr.shadowCastingMode=ShadowCastingMode.Off;cr.receiveShadows=false;

        var farmer=farmers.FirstOrDefault(x=>Inside(x.transform.position));
        model.route=new GameObject("Ronda").transform;model.route.SetParent(scaled,false);
        if(farmer!=null && farmer.patrolPoints!=null && farmer.patrolPoints.Length>1)
        {
            var pts=farmer.patrolPoints.Where(p=>p!=null).Select(p=>{var w=p.position;w.y=GroundAt(w.x,w.z)+.35f;return Local(w);}).ToArray();
            model.patrol=pts;model.patrolSpeed=farmer.patrolSpeed;model.guards=1;
            for(int i=0;i<pts.Length;i++)model.patrolLength+=Vector3.Distance(pts[i],pts[(i+1)%pts.Length]);
            var ribbon=Ribbon(pts,1.6f,(x,z)=>GroundAt(x+centre.x,z+centre.z)+.35f);ribbon.name="Ronda "+farm.identity;ribbon=SaveMesh(ribbon,Folder+"Meshes/Fazenda_"+tag+"_ronda.asset");
            Piece("Caminho da ronda",ribbon,new[]{Mat("Ronda",new Color(.95f,.25f,.2f,.75f),true)},model.route).shadowCastingMode=ShadowCastingMode.Off;
            var token=new GameObject("Fazendeiro").transform;token.SetParent(model.route,false);token.localPosition=pts[0];token.localScale=Vector3.one*Marker;
            Primitive(PrimitiveType.Capsule,token,new Vector3(0,2.4f,0),new Vector3(2.2f,2.4f,2.2f),Mat("Fazendeiro",new Color(.85f,.18f,.14f)));
            Primitive(PrimitiveType.Sphere,token,new Vector3(0,5.4f,0),Vector3.one*1.8f,Mat("Fazendeiro",new Color(.85f,.18f,.14f)));
            Primitive(PrimitiveType.Cube,token,new Vector3(0,3f,1.4f),new Vector3(.6f,.6f,1.6f),Mat("Lanterna",new Color(1,.92f,.6f)));
            model.token=token;
        }
        model.cones=new GameObject("Seguranca").transform;model.cones.SetParent(scaled,false);
        var sweeps=new List<ARFarmModel.Sweep>();var cone=SaveMesh(Cone(),Folder+"Meshes/Cone_de_visao.asset");
        foreach(var c in cams.Where(c=>Inside(c.transform.position)))
        {
            var mount=new GameObject("Camera de seguranca").transform;mount.SetParent(model.cones,false);mount.localPosition=Local(c.Eye);
            Primitive(PrimitiveType.Cube,mount,Vector3.zero,new Vector3(1.6f,1.2f,2.4f)*Marker*.8f,Mat("Camera",new Color(.15f,.16f,.18f)));
            var head=new GameObject("Varredura").transform;head.SetParent(mount,false);
            var view=new GameObject("Cone de visao");view.transform.SetParent(head,false);view.transform.localScale=Vector3.one*c.viewDistance;
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
            var marker=new GameObject("Galinha").transform;marker.SetParent(model.birds,false);marker.localPosition=Local(p);marker.localScale=Vector3.one*Marker;
            Primitive(PrimitiveType.Sphere,marker,new Vector3(0,1.3f,0),new Vector3(1.6f,1.5f,2f),Mat("Galinha",new Color(.98f,.93f,.82f)));
            Primitive(PrimitiveType.Cube,marker,new Vector3(0,2.2f,.7f),new Vector3(.4f,.6f,.5f),Mat("Crista",new Color(.86f,.14f,.12f)));
            model.chickens++;
        }
        model.gate=new GameObject("Entrada").transform;model.gate.SetParent(scaled,false);
        var gatePoint=farm.entrance;gatePoint.y=GroundAt(gatePoint.x,gatePoint.z);model.gate.localPosition=Local(gatePoint);
        var inward=centre-farm.entrance;inward.y=0;model.gate.localRotation=Quaternion.LookRotation(inward.sqrMagnitude>1e-4f?inward:Vector3.forward);
        var ag=new GameObject("Seta");ag.transform.SetParent(model.gate,false);ag.transform.localPosition=new Vector3(0,.8f,-5);ag.transform.localScale=Vector3.one*5.5f;
        ag.AddComponent<MeshFilter>().sharedMesh=SaveMesh(Arrow(),Folder+"Meshes/Seta_de_entrada.asset");ag.AddComponent<MeshRenderer>().sharedMaterial=Mat("Entrada",new Color(.35f,.85f,.4f));
        PrefabUtility.SaveAsPrefabAsset(root,Prefabs+"Fazenda_"+tag+".prefab");Object.DestroyImmediate(root);
        return $"{tag} {farm.identity}: {parts.Length} pieces, {buildings.vertexCount} vertices, radius {radius:0} m, cameras {model.cameras}, chickens {model.chickens}, patrol {(model.patrol?.Length??0)}";
    }

    static Vector2 Polar(int ring,int segment,float radius)
    {
        float r=radius*ring/Rings,a=(segment+(ring%2)*.5f)*Mathf.PI*2/Segments;
        return new Vector2(Mathf.Cos(a)*r,Mathf.Sin(a)*r);
    }

    static Mesh Island(float[,] h,float radius,float floor)
    {
        var v=new List<Vector3>();var top=new List<int>();var side=new List<int>();var bottom=new List<int>();
        Vector3 P(int r,int s){s=(s%Segments+Segments)%Segments;var p=Polar(r,s,radius);return new Vector3(p.x,r==0?h[0,0]:h[r,s],p.y);}
        void Tri(List<int> list,Vector3 a,Vector3 b,Vector3 c){list.Add(v.Count);v.Add(a);list.Add(v.Count);v.Add(b);list.Add(v.Count);v.Add(c);}
        for(int s=0;s<Segments;s++)Tri(top,P(0,0),P(1,s+1),P(1,s));
        for(int r=1;r<Rings;r++)for(int s=0;s<Segments;s++)
        {
            int o=r%2;
            Tri(top,P(r,s),P(r,s+1),P(r+1,s+o));
            Tri(top,P(r,s+1),P(r+1,s+1+o),P(r+1,s+o));
        }
        float lip=floor+(h[Rings,0]-floor)*.55f;
        for(int s=0;s<Segments;s++)
        {
            Vector3 a=P(Rings,s),b=P(Rings,s+1);
            Vector3 am=new Vector3(a.x*1.015f,lip,a.z*1.015f),bm=new Vector3(b.x*1.015f,lip,b.z*1.015f);
            Vector3 ab=new Vector3(a.x*.94f,floor,a.z*.94f),bb=new Vector3(b.x*.94f,floor,b.z*.94f);
            Tri(side,a,b,am);Tri(side,b,bm,am);
            Tri(side,am,bm,ab);Tri(side,bm,bb,ab);
            Tri(bottom,new Vector3(0,floor,0),ab,bb);
        }
        var mesh=new Mesh{name="Ilha da maquete",indexFormat=IndexFormat.UInt32};mesh.SetVertices(v);mesh.subMeshCount=3;
        mesh.SetTriangles(top,0);mesh.SetTriangles(side,1);mesh.SetTriangles(bottom,2);mesh.RecalculateNormals();
        if(mesh.normals[top[0]].y<0)
        {
            top.Reverse();side.Reverse();bottom.Reverse();mesh.SetTriangles(top,0);mesh.SetTriangles(side,1);mesh.SetTriangles(bottom,2);mesh.RecalculateNormals();
        }
        mesh.RecalculateBounds();return mesh;
    }

    static Material ShadowMaterial()
    {
        string png=Folder+"Sombra_na_mao.png";
        if(!File.Exists(png))
        {
            const int n=128;var t=new Texture2D(n,n,TextureFormat.RGBA32,false);
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float d=new Vector2(x+.5f-n*.5f,y+.5f-n*.5f).magnitude/(n*.5f);
                float a=Mathf.Clamp01(1-Mathf.SmoothStep(.55f,1,d))*.55f;
                t.SetPixel(x,y,new Color(.05f,.03f,.02f,a));
            }
            File.WriteAllBytes(png,t.EncodeToPNG());Object.DestroyImmediate(t);AssetDatabase.ImportAsset(png);
            var importer=(TextureImporter)AssetImporter.GetAtPath(png);importer.alphaIsTransparency=true;importer.mipmapEnabled=false;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
        }
        var m=Mat("Sombra na mao",Color.white,true);m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(png));m.SetFloat("_Cull",2);
        EditorUtility.SetDirty(m);return m;
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
            var dir=path[(i+1)%path.Count]-path[(i-1+path.Count)%path.Count];dir.y=0;var across=Vector3.Cross(Vector3.up,dir.normalized)*width*.5f;
            v.Add(path[i]-across);v.Add(path[i]+across);
        }
        for(int i=0;i<path.Count;i++){int a=i*2,b=((i+1)%path.Count)*2;t.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
        var m=new Mesh();m.SetVertices(v);m.SetTriangles(t,0);m.RecalculateNormals();m.RecalculateBounds();return m;
    }

    static Mesh SaveMesh(Mesh mesh,string path){MeshUtility.SetMeshCompression(mesh,ModelImporterMeshCompression.Medium);return ARPlanningBuilder.SaveMesh(mesh,path);}
    static Material Mat(string name,Color color,bool transparent=false)=>ARPlanningBuilder.Mat(name,color,transparent);
    static GameObject Primitive(PrimitiveType type,Transform parent,Vector3 position,Vector3 size,Material material)=>ARPlanningBuilder.Primitive(type,parent,position,size,material);
    static Mesh Cone()=>ARPlanningBuilder.Cone();
    static Mesh Arrow()=>ARPlanningBuilder.Arrow();
}
