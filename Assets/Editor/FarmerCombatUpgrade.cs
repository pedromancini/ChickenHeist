using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

public static class FarmerCombatUpgrade
{
    const string Folder="Assets/ChickenHeistGenerated/FarmerCombat";
    public static void Install()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var notes=new List<string>();
        foreach(var farm in Object.FindObjectsByType<FarmLayoutInfo>().OrderBy(f=>f.layoutIndex))
        {
            var farmer=farm.GetComponentInChildren<FarmerStateMachine>();
            var house=farm.transform.Cast<Transform>().First(t=>t.name.StartsWith("Casa -"));
            var residence=farmer.GetComponent<FarmerResidence>();
            if(residence==null)
            {
                Bounds b=ProceduralFarmGenerator.VisualBounds(house.gameObject);
                Vector3 entrance=new Vector3(b.center.x,.1f,b.min.z+.1f);
                residence=farmer.gameObject.AddComponent<FarmerResidence>();
                BuildRoom(house,residence,entrance,farm.layoutIndex);
                farmer.transform.SetPositionAndRotation(residence.bedPosition.position,Quaternion.Euler(0,180,0));
                farmer.sleepSystem.startingSleep=Mathf.Min(farmer.sleepSystem.startingSleep,24);
                var cc=farmer.GetComponent<CharacterController>();cc.radius=.32f;cc.height=1.8f;cc.center=Vector3.up*.9f;
                var old=farmer.GetComponent<Renderer>();if(old!=null)old.enabled=false;
            }
            BakeFarm(farm,residence);
            notes.Add(farm.identity+" bed="+residence.bedPosition.position+" door="+residence.insideDoor.position+" exit="+residence.outsideDoor.position);
        }
        var game=Object.FindAnyObjectByType<HeistGameManager>();if(game.player.GetComponent<PlayerHealth>()==null)game.player.gameObject.AddComponent<PlayerHealth>();
        RuralWorldReview.PersistGeneratedAssets(EditorSceneManager.GetActiveScene());
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        Directory.CreateDirectory("output/farmer-combat-review");File.WriteAllLines("output/farmer-combat-review/installation.txt",notes);
        FarmerCombatReview.Begin();
    }
    static GameObject Part(Transform root,string name,Vector3 p,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root,false);go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go;
    }
    static Transform Point(Transform root,string name,Vector3 p)
    {var t=new GameObject(name).transform;t.SetParent(root,false);t.localPosition=p;return t;}
    static void BuildRoom(Transform house,FarmerResidence residence,Vector3 entrance,int index)
    {
        var cut=new Bounds(entrance+new Vector3(0,1.2f,2.5f),new Vector3(3.2f,3.4f,6.1f));
        int i=0;
        foreach(var filter in house.GetComponentsInChildren<MeshFilter>())
        {
            var renderer=filter.GetComponent<Renderer>();if(renderer==null || !renderer.bounds.Intersects(cut))continue;
            var mesh=SubtractBox(filter,cut);
            string path=Folder+"/House-"+index+"-"+(i++)+".asset";AssetDatabase.CreateAsset(mesh,path);
            filter.sharedMesh=mesh;var collider=filter.GetComponent<MeshCollider>();if(collider!=null)collider.sharedMesh=mesh;
            foreach(var box in filter.GetComponents<BoxCollider>())Object.DestroyImmediate(box);
            if(collider==null)filter.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        var room=new GameObject("Quarto do fazendeiro").transform;room.SetParent(house,false);room.SetPositionAndRotation(entrance,Quaternion.identity);room.localScale=new Vector3(1/house.lossyScale.x,1/house.lossyScale.y,1/house.lossyScale.z);
        var wood=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Quarto - madeira antiga",color=new Color(.28f,.20f,.13f)};
        var wall=new Material(wood){name="Quarto - reboco",color=new Color(.54f,.50f,.39f)};
        var cloth=new Material(wood){name="Quarto - cobertor",color=new Color(.24f,.29f,.21f)};
        Part(room,"Piso do quarto",new Vector3(0,-.055f,2.5f),new Vector3(3.2f,.1f,6.1f),wood);
        Part(room,"Parede interna esquerda",new Vector3(-1.53f,1.4f,2.5f),new Vector3(.12f,2.8f,6),wall);
        Part(room,"Parede interna direita",new Vector3(1.53f,1.4f,2.5f),new Vector3(.12f,2.8f,6),wall);
        Part(room,"Parede interna fundo",new Vector3(0,1.4f,5.48f),new Vector3(3.2f,2.8f,.12f),wall);
        Part(room,"Forro do quarto",new Vector3(0,2.78f,2.5f),new Vector3(3.2f,.1f,6),wood);
        Part(room,"Frente esquerda",new Vector3(-1.22f,1.4f,0),new Vector3(.61f,2.8f,.15f),wood);
        Part(room,"Frente direita",new Vector3(1.22f,1.4f,0),new Vector3(.61f,2.8f,.15f),wood);
        Part(room,"Verga da entrada",new Vector3(0,2.58f,0),new Vector3(1.84f,.44f,.15f),wood);
        residence.doorHinge=Point(room,"Dobradiça da porta",new Vector3(-.87f,0,0));
        var door=Part(residence.doorHinge,"Porta da casa - fazendeiro",new Vector3(.86f,1.15f,0),new Vector3(1.72f,2.3f,.08f),wood);residence.doorCollider=door.GetComponent<Collider>();
        Part(residence.doorHinge,"Macaneta",new Vector3(1.57f,1.08f,-.09f),new Vector3(.08f,.06f,.12f),wood);
        Part(room,"Cama de madeira",new Vector3(0,.32f,4.25f),new Vector3(.95f,.48f,1.85f),wood);
        Part(room,"Colchao e cobertor",new Vector3(0,.58f,4.25f),new Vector3(.91f,.12f,1.8f),cloth);
        Part(room,"Travesseiro",new Vector3(0,.68f,4.87f),new Vector3(.75f,.12f,.36f),wall);
        residence.bedPosition=Point(room,"Posicao ao pe da cama",new Vector3(0,.02f,2.9f));
        residence.insideDoor=Point(room,"Passagem interna",new Vector3(0,.02f,.85f));
        residence.outsideDoor=Point(room,"Saida da casa",new Vector3(0,.02f,-1.4f));
        var lamp=Point(room,"Luz fraca do quarto",new Vector3(1.15f,2.2f,3.2f)).gameObject.AddComponent<Light>();lamp.type=LightType.Point;lamp.range=6;lamp.intensity=.55f;lamp.color=new Color(1,.73f,.44f);
    }
    struct Vertex
    {
        public Vector3 p,n;public Vector2 uv;
        public static Vertex Mix(Vertex a,Vertex b,float t)=>new Vertex{p=Vector3.Lerp(a.p,b.p,t),n=Vector3.Lerp(a.n,b.n,t).normalized,uv=Vector2.Lerp(a.uv,b.uv,t)};
    }
    static Mesh SubtractBox(MeshFilter filter,Bounds box)
    {
        var source=filter.sharedMesh;var p=source.vertices;var n=source.normals;var uv=source.uv;
        var output=new List<Vector3>();var normals=new List<Vector3>();var uvs=new List<Vector2>();var submeshes=new List<int[]>();
        for(int sub=0;sub<source.subMeshCount;sub++)
        {
            var indices=new List<int>();var triangles=source.GetTriangles(sub);
            for(int tri=0;tri<triangles.Length;tri+=3)
            {
                var polygon=new List<Vertex>();
                for(int j=0;j<3;j++){int v=triangles[tri+j];polygon.Add(new Vertex{p=filter.transform.TransformPoint(p[v]),n=n.Length==p.Length?n[v]:Vector3.up,uv=uv.Length==p.Length?uv[v]:Vector2.zero});}
                for(int plane=0;plane<6 && polygon.Count>=3;plane++)
                {
                    int axis=plane/2;bool minimum=plane%2==0;float boundary=minimum?box.min[axis]:box.max[axis];
                    var inside=new List<Vertex>();var outside=new List<Vertex>();
                    for(int j=0;j<polygon.Count;j++)
                    {
                        var a=polygon[j];var b=polygon[(j+1)%polygon.Count];
                        float da=(a.p[axis]-boundary)*(minimum?1:-1),db=(b.p[axis]-boundary)*(minimum?1:-1);
                        (da>=0?inside:outside).Add(a);
                        if((da>=0)!=(db>=0)){var split=Vertex.Mix(a,b,da/(da-db));inside.Add(split);outside.Add(split);}
                    }
                    if(outside.Count>=3)
                    {
                        int start=output.Count;
                        foreach(var vertex in outside){output.Add(filter.transform.InverseTransformPoint(vertex.p));normals.Add(vertex.n);uvs.Add(vertex.uv);}
                        for(int j=1;j<outside.Count-1;j++)indices.AddRange(new[]{start,start+j,start+j+1});
                    }
                    polygon=inside;
                }
            }
            submeshes.Add(indices.ToArray());
        }
        var mesh=new Mesh{name=source.name+" - passagem habitavel",indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.SetVertices(output);mesh.SetNormals(normals);mesh.SetUVs(0,uvs);mesh.subMeshCount=submeshes.Count;
        for(int sub=0;sub<submeshes.Count;sub++)mesh.SetTriangles(submeshes[sub],sub);mesh.RecalculateBounds();mesh.RecalculateTangents();return mesh;
    }
    static void BakeFarm(FarmLayoutInfo farm,FarmerResidence home)
    {
        var sources=new List<NavMeshBuildSource>();var bounds=new Bounds(new Vector3(farm.lot.center.x,3,farm.lot.center.y),new Vector3(farm.lot.width+36,25,farm.lot.height+36));
        NavMeshBuilder.CollectSources(bounds,~0,NavMeshCollectGeometry.PhysicsColliders,0,new List<NavMeshBuildMarkup>(),sources);
        sources.RemoveAll(s=>s.component!=null && (s.component.GetComponentInParent<FarmerStateMachine>()!=null || s.component.GetComponentInParent<PlayerMovement>()!=null ||
            s.component.GetComponentInParent<SimpleAnimalWander>()!=null || s.component.GetComponentInParent<InteractableChicken>()!=null || s.component.GetComponentInParent<OldPickupTruck>()!=null || s.component.transform.IsChildOf(home.doorHinge)));
        var settings=NavMesh.GetSettingsByIndex(0);settings.agentRadius=.36f;settings.agentHeight=1.8f;settings.agentClimb=.3f;settings.overrideVoxelSize=true;settings.voxelSize=.09f;
        var data=NavMeshBuilder.BuildNavMeshData(settings,sources,bounds,Vector3.zero,Quaternion.identity);data.name="Navegacao - "+farm.identity;
        string path=Folder+"/Navigation-"+farm.layoutIndex+".asset";var existing=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
        if(existing==null)AssetDatabase.CreateAsset(data,path);else{EditorUtility.CopySerialized(data,existing);Object.DestroyImmediate(data);data=existing;}
        var surface=farm.GetComponent<FarmNavigationSurface>()??farm.gameObject.AddComponent<FarmNavigationSurface>();surface.data=data;
    }
}
