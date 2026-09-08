using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class PlayerHomeBuilder
{
    private const string Pack="Assets/MarpaStudio/Built-In/Prefabs/";
    private const string Output="output/player-home";

    [MenuItem("Chicken Heist/Build Protagonist Home")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (GameObject.Find("Casa do Protagonista - Sitio do Recomeco")!=null)
        { Debug.LogWarning("The protagonist home already exists. Edit its prefab instead of creating a duplicate."); return; }
        var scene=SceneManager.GetActiveScene();
        var player=GameObject.FindGameObjectWithTag("Player");
        if (player==null) throw new System.InvalidOperationException("Open the rural world scene first.");
        Directory.CreateDirectory(Output);
        EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BeforePlayerHome.unity"),true);
        Vector3 origin=new Vector3(player.transform.position.x-24f,0,player.transform.position.z+23f);
        var lot=new Rect(origin.x-19f,origin.z-16f,38f,32f);
        foreach (var farm in Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None))
            if (farm.lot.Overlaps(lot)) throw new System.InvalidOperationException("Home area overlaps an existing farm.");
        FlattenHomePlot(lot);
        Transform forest=GameObject.Find("Floresta Low Poly")?.transform;
        if (forest!=null) ClearVegetation(forest,lot);
        var root=new GameObject("Casa do Protagonista - Sitio do Recomeco");
        root.transform.position=origin;
        var house=Place("House",new Vector3(-3,0,3),new Vector3(10,7,9),root.transform);
        house.name="Casa de madeira envelhecida";
        house.transform.RotateAround(ProceduralFarmGenerator.VisualBounds(house).center,Vector3.up,180f);
        Place("OldShed",new Vector3(10,0,7),new Vector3(5,3.6f,4),root.transform).name="Deposito de chapa enferrujada";
        Place("ChickenHouse",new Vector3(10,0,-4),new Vector3(3.2f,2.5f,3),root.transform).name="Pequeno galinheiro do jogador";
        Place("FoodTrough",new Vector3(10,0,-7),new Vector3(1.8f,.7f,.8f),root.transform).name="Comedouro quase vazio";
        Place("RockingChair",new Vector3(-8.7f,0,-2.5f),new Vector3(1,1.3f,1.2f),root.transform);
        Place("Bucket",new Vector3(1.7f,0,-2.4f),new Vector3(.5f,.55f,.5f),root.transform);
        Place("Barell",new Vector3(7.2f,0,7),new Vector3(.9f,1.2f,.9f),root.transform);
        Place("WheelCart",new Vector3(8,0,3.6f),new Vector3(1.6f,1.2f,2.1f),root.transform);
        Place("Shovel",new Vector3(12.5f,0,5),new Vector3(.6f,1.4f,.5f),root.transform);
        Place("Rake",new Vector3(13.4f,0,5),new Vector3(.65f,1.5f,.5f),root.transform);
        for (int i=0;i<3;i++)
            Place("Log",new Vector3(-10f+i*.7f,0,6),new Vector3(.65f,.7f,1.5f),root.transform);
        Place("Box",new Vector3(6.5f,0,9),new Vector3(.9f,.9f,.9f),root.transform);
        Place("Box",new Vector3(6.5f,.9f,9),new Vector3(.8f,.8f,.8f),root.transform);
        var wood=new Material(Shader.Find("Universal Render Pipeline/Lit"));
        wood.name="Tabuas de reparo - madeira antiga"; wood.color=new Color(.25f,.20f,.13f);
        for (int i=0;i<4;i++)
        {
            var board=GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name="Tabua guardada para conserto";
            board.transform.SetParent(root.transform,false);
            board.transform.localPosition=new Vector3(-10.5f+i*.3f,.12f+i*.06f,1.5f);
            board.transform.localScale=new Vector3(.22f,.09f,2.3f-i*.2f);
            board.transform.localRotation=Quaternion.Euler(0,i*4-6,0);
            board.GetComponent<Renderer>().sharedMaterial=wood;
        }
        // Fence pieces retain their imported orientation and are joined using measured bounds.
        for (int i=0;i<7;i++)
        {
            if (i==2) continue;
            var fence=Place("Fence",new Vector3(-15f+i*4.4f,0,-12),new Vector3(4.5f,1.3f,.35f),root.transform);
            fence.name="Cerca antiga do quintal";
        }
        var lampObject=new GameObject("Lampada fraca da varanda");
        lampObject.transform.SetParent(root.transform,false);
        lampObject.transform.localPosition=new Vector3(-3,2.7f,-2.3f);
        var lamp=lampObject.AddComponent<Light>(); lamp.type=LightType.Point;
        lamp.range=8; lamp.intensity=1.4f; lamp.color=new Color(1,.72f,.38f); lamp.shadows=LightShadows.None;
        AddHomePath(root.transform,new Vector3(-3,0,-2.5f),new Vector3(-3,0,-15),2.4f);
        AddHomePath(root.transform,new Vector3(-3,0,-8),new Vector3(10,0,-8),1.6f);
        AddHomePath(root.transform,new Vector3(10,0,-8),new Vector3(10,0,4),1.6f);
        Vector3 spawn=new Vector3(player.transform.position.x,0,player.transform.position.z);
        AddHomePath(root.transform,new Vector3(-3,0,-15),spawn-origin,2.6f);
        var roadRoot=GameObject.Find(RuralRoadSurface.RootName);
        if (roadRoot!=null) Object.DestroyImmediate(roadRoot);
        RuralRoadSurface.Build(Object.FindObjectsByType<RuralRoadSpan>(FindObjectsSortMode.None),
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/ground/Ground048_1K-JPG_Color.jpg"));
        RuralWorldReview.PersistGeneratedAssets(scene);
        string folder="Assets/ChickenHeistGenerated/PlayerHome";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/ChickenHeistGenerated","PlayerHome");
        PrefabUtility.SaveAsPrefabAsset(root,folder+"/ProtagonistHome.prefab");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Capture(root,Output+"/home-front.png",new Vector3(-22,12,-29),new Vector3(0,2,1));
        Capture(root,Output+"/home-yard.png",new Vector3(26,17,-25),new Vector3(1,1,1));
        File.WriteAllText(Output+"/home-notes.txt","Source: MarpaStudio House + OldShed. Exterior home set near the existing player spawn.\nOriginal farms preserved. No new downloads. Interior and economy interactions are not implemented.\nScene: "+scene.path+"\nHome position: "+origin+"\n");
        Selection.activeGameObject=root;
        SceneView.lastActiveSceneView?.LookAt(origin+Vector3.up*2,Quaternion.Euler(26,0,0),27f);
        Debug.Log("Protagonist home created and saved: "+scene.path);
    }

    [MenuItem("Chicken Heist/Finish Protagonist Home")]
    public static void Finish()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var root=GameObject.Find("Casa do Protagonista - Sitio do Recomeco");
        if (root==null) return;
        var scene=SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BeforeHomeFinish.unity"),true);
        var origin=root.transform.position;
        for (int i=root.transform.childCount-1;i>=0;i--)
        {
            var t=root.transform.GetChild(i);
            if (t.name=="Cerca antiga do quintal" || t.name=="Remendo da cerca antiga") Object.DestroyImmediate(t.gameObject);
        }
        BuildWornPerimeter(root.transform);
        ReplaceHomeReturnPoint(root.transform);
        BuildWornCoop(root.transform);
        var terrain=GameObject.Find("Terreno Ondulado Low Poly");
        var mf=terrain.GetComponent<MeshFilter>();
        var mesh=Object.Instantiate(mf.sharedMesh); mesh.name="Terreno com acesso nivelado da casa";
        var vertices=mesh.vertices;
        var paths=root.GetComponentsInChildren<RuralRoadSpan>();
        foreach (var span in paths)
        {
            if (Mathf.Abs(span.start.x-(origin.x-3))<.1f && Mathf.Abs(span.start.z-(origin.z-2.5f))<.1f)
            {
                Bounds h=ProceduralFarmGenerator.VisualBounds(root.transform.Find("Casa de madeira envelhecida").gameObject);
                span.start.z=h.min.z-.1f;
            }
        }
        for (int i=0;i<vertices.Length;i++)
        {
            Vector3 p=terrain.transform.TransformPoint(vertices[i]);
            float influence=0;
            foreach (var span in paths)
            {
                Vector2 a=new Vector2(span.start.x,span.start.z),b=new Vector2(span.end.x,span.end.z),point=new Vector2(p.x,p.z);
                Vector2 ab=b-a;
                float t=Mathf.Clamp01(Vector2.Dot(point-a,ab)/Mathf.Max(.01f,ab.sqrMagnitude));
                float distance=Vector2.Distance(point,a+ab*t);
                influence=Mathf.Max(influence,1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(span.width*.5f+4,span.width*.5f+12,distance)));
            }
            p.y*=1-influence; vertices[i]=terrain.transform.InverseTransformPoint(p);
        }
        mesh.vertices=vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mf.sharedMesh=mesh; terrain.GetComponent<MeshCollider>().sharedMesh=mesh;
        var roadRoot=GameObject.Find(RuralRoadSurface.RootName);
        if (roadRoot!=null) Object.DestroyImmediate(roadRoot);
        RuralRoadSurface.Build(Object.FindObjectsByType<RuralRoadSpan>(FindObjectsSortMode.None),
            AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/ground/Ground048_1K-JPG_Color.jpg"));
        RuralWorldReview.PersistGeneratedAssets(scene);
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/ChickenHeistGenerated/PlayerHome/ProtagonistHome.prefab");
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Capture(root,Output+"/home-front.png",new Vector3(-22,12,-29),new Vector3(0,2,1));
        Capture(root,Output+"/home-yard.png",new Vector3(26,17,-25),new Vector3(1,1,1));
        Capture(root,Output+"/worn-coop.png",new Vector3(21,5,-14),new Vector3(12,1,-3));
        int failures=0;
        if(GameObject.Find("Caminhonete de Fuga")!=null) failures++;
        var returnZone=root.GetComponentInChildren<ExtractionZone>();
        if(returnZone==null || returnZone.GetComponent<Renderer>()!=null || !returnZone.GetComponent<BoxCollider>().isTrigger) failures++;
        if(root.transform.Find("Galinheiro gasto - Ultimo Recurso")==null) failures++;
        int perimeterPanels=0;
        foreach(Transform child in root.transform) if(child.name=="Cerca antiga do quintal") perimeterPanels++;
        if(perimeterPanels<35) failures++;
        foreach (var r in root.GetComponentsInChildren<Renderer>())
            foreach(var m in r.sharedMaterials) if(m==null || m.shader==null || !AssetDatabase.Contains(m)) failures++;
        foreach(var p in paths)
        for(int i=0;i<=20;i++)
        {
            Vector3 pos=Vector3.Lerp(p.start,p.end,i/20f);
            if(terrain.GetComponent<MeshCollider>().Raycast(new Ray(pos+Vector3.up*30,Vector3.down),out var hit,60) && Mathf.Abs(hit.point.y)>.04f) failures++;
        }
        File.WriteAllText(Output+"/home-audit.txt","Materials, paths, worn coop, invisible return trigger and full perimeter: "+(failures==0 ? "PASS":"FAIL")+" | failures="+failures+" | fence panels="+perimeterPanels+"\n");
        File.WriteAllText(Output+"/home-notes.txt","Exterior do protagonista finalizado. Casa envelhecida, deposito de chapa, galinheiro deteriorado com ninhos vazios, remendos e pouca racao.\nAs fazendas-alvo foram preservadas. Sem downloads adicionais.\nInterior da casa e sistemas de alimentacao/conserto/economia ainda nao implementados.\n");
        Selection.activeGameObject=root;
        SceneView.lastActiveSceneView?.LookAt(origin+Vector3.up*2,Quaternion.Euler(26,0,0),27f);
        Debug.Log("Protagonist home finished. Validation failures="+failures);
    }

    private static void FenceRun(Transform parent,float min,float max)
    {
        int count=Mathf.CeilToInt((max-min)/3f);
        float length=(max-min)/count;
        for(int i=0;i<count;i++)
        {
            var fence=Place("Fence",new Vector3(min+(i+.5f)*length,0,-12),new Vector3(4.5f,1.3f,.35f),parent);
            var holder=new GameObject("Cerca antiga do quintal");holder.transform.SetParent(parent,false);
            Bounds b=ProceduralFarmGenerator.VisualBounds(fence);
            holder.transform.position=b.center;fence.transform.SetParent(holder.transform,true);
            holder.transform.localScale=new Vector3((length+.02f)/b.size.x,1.1f/b.size.y,1);
            b=ProceduralFarmGenerator.VisualBounds(holder);
            holder.transform.position+=Vector3.down*b.min.y;
        }
    }

    private static void AddHomePath(Transform root,Vector3 a,Vector3 b,float width)
    {
        var go=new GameObject("Acesso de terra da casa"); go.transform.SetParent(root,false);
        var span=go.AddComponent<RuralRoadSpan>(); span.start=root.TransformPoint(a); span.end=root.TransformPoint(b); span.width=width;
    }

    private static void FlattenHomePlot(Rect lot)
    {
        var terrain=GameObject.Find("Terreno Ondulado Low Poly");
        var mf=terrain.GetComponent<MeshFilter>(); var mesh=Object.Instantiate(mf.sharedMesh);
        mesh.name="Terreno rural com patio da casa";
        var vertices=mesh.vertices;
        for (int i=0;i<vertices.Length;i++)
        {
            Vector3 p=terrain.transform.TransformPoint(vertices[i]);
            float dx=Mathf.Max(lot.xMin-p.x,0,p.x-lot.xMax), dz=Mathf.Max(lot.yMin-p.z,0,p.z-lot.yMax);
            float fade=Mathf.SmoothStep(0,1,Mathf.Clamp01(Mathf.Sqrt(dx*dx+dz*dz)/12f));
            p.y*=fade; vertices[i]=terrain.transform.InverseTransformPoint(p);
        }
        mesh.vertices=vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
        mf.sharedMesh=mesh; terrain.GetComponent<MeshCollider>().sharedMesh=mesh;
    }

    private static void ClearVegetation(Transform parent,Rect lot)
    {
        for (int i=parent.childCount-1;i>=0;i--)
        {
            Transform child=parent.GetChild(i);
            if (child.GetComponentInChildren<Renderer>()==null) continue;
            if (child.GetComponent<Renderer>()==null) {ClearVegetation(child,lot);continue;}
            Bounds b=ProceduralFarmGenerator.VisualBounds(child.gameObject);
            if (lot.Overlaps(new Rect(b.min.x,b.min.z,b.size.x,b.size.z))) Object.DestroyImmediate(child.gameObject);
        }
    }

    public static void Preview()
    {
        Directory.CreateDirectory(Output);
        foreach (string name in new[]{"House","OldShed","ChickenHouse"})
        {
            var root=new GameObject("Temporary home preview");
            root.transform.position=new Vector3(3000,0,3000);
            try
            {
                Place(name,Vector3.zero,new Vector3(10,7,9),root.transform);
                Capture(root,Output+"/candidate-"+name+".png",new Vector3(13,8,-16),new Vector3(0,2,0));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }

    private static GameObject Place(string asset,Vector3 p,Vector3 limit,Transform parent)
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Pack+asset+".prefab");
        if (prefab==null) throw new System.InvalidOperationException("Missing home asset: "+asset);
        var obj=Object.Instantiate(prefab,parent);
        obj.name=asset;
        Bounds b=ProceduralFarmGenerator.VisualBounds(obj);
        float s=Mathf.Min(limit.x/Mathf.Max(.01f,b.size.x),limit.y/Mathf.Max(.01f,b.size.y),limit.z/Mathf.Max(.01f,b.size.z));
        obj.transform.localScale*=s;
        b=ProceduralFarmGenerator.VisualBounds(obj);
        Vector3 target=parent.TransformPoint(p);
        obj.transform.position+=new Vector3(target.x-b.center.x,target.y-b.min.y,target.z-b.center.z);
        foreach (var r in obj.GetComponentsInChildren<Renderer>())
        {
            var mats=r.sharedMaterials;
            for (int i=0;i<mats.Length;i++)
            {
                var source=mats[i];
                var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                m.name="Casa do protagonista - "+source.name;
                Texture tex=source.HasProperty("_MainTex") ? source.GetTexture("_MainTex"):null;
                if (tex==null && source.HasProperty("_BaseMap")) tex=source.GetTexture("_BaseMap");
                m.SetTexture("_BaseMap",tex);
                m.SetColor("_BaseColor",Color.white);
                m.SetFloat("_Smoothness",.1f);
                mats[i]=m;
            }
            r.sharedMaterials=mats;
        }
        foreach (var mf in obj.GetComponentsInChildren<MeshFilter>())
            if (mf.sharedMesh!=null && mf.GetComponent<Collider>()==null) mf.gameObject.AddComponent<MeshCollider>().sharedMesh=mf.sharedMesh;
        return obj;
    }

    private static void Capture(GameObject root,string path,Vector3 offset,Vector3 target,float fov=45)
    {
        var cameraObject=new GameObject("Home review camera");
        var camera=cameraObject.AddComponent<Camera>();
        camera.transform.position=root.transform.position+offset;
        camera.transform.LookAt(root.transform.position+target);
        camera.fieldOfView=fov; camera.nearClipPlane=.03f; camera.farClipPlane=150;
        camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.43f,.53f,.56f);
        var moon=GameObject.Find("Moon Light").GetComponent<Light>();
        Color oldColor=moon.color; float oldIntensity=moon.intensity;
        bool fog=RenderSettings.fog;
        var rt=new RenderTexture(1280,960,24);
        var old=RenderTexture.active;
        var image=new Texture2D(1280,960,TextureFormat.RGB24,false);
        try
        {
            moon.color=new Color(1,.95f,.86f); moon.intensity=1.5f; RenderSettings.fog=false;
            camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,1280,960),0,0); image.Apply();
            File.WriteAllBytes(path,image.EncodeToPNG());
        }
        finally
        {
            moon.color=oldColor; moon.intensity=oldIntensity; RenderSettings.fog=fog;
            RenderTexture.active=old; camera.targetTexture=null;
            rt.Release(); Object.DestroyImmediate(rt); Object.DestroyImmediate(image); Object.DestroyImmediate(cameraObject);
        }
    }
}
