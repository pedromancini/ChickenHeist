using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class RuralTreeReview
{
    const string Output="output/tree-review";
    const string Folder="Assets/ChickenHeistGenerated/Nature/Trees";
    class TreeSource
    {
        public string name;
        public Vector3[] contacts;
        public Material[] materials;
    }
    static IEnumerable<GameObject> Sources()
    {
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            string name=Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if(!name.Contains("tree") || path.Contains("ChickenHeistGenerated") || path.Contains("HDRP") || path.Contains("URP"))continue;
            if(!path.Contains("LowPoly_ForestPack/") && !path.Contains("SimpleNaturePack/") && !path.Contains("Pandazole Farm Ranch Pack/"))continue;
            yield return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
        var plum=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Environment/Tree_RedPlum.fbx");
        if(plum!=null)yield return plum;
    }
    [MenuItem("Chicken Heist/Inspect Rural Trees")]
    public static void Inspect()
    {
        Directory.CreateDirectory(Output);
        var lines=new List<string>();
        foreach(var source in Sources())
        {
            lines.Add("SOURCE "+AssetDatabase.GetAssetPath(source));
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true))
            {
                var r=filter.GetComponent<Renderer>();
                lines.Add("  mesh "+filter.name+" | "+filter.sharedMesh.name+" | readable="+filter.sharedMesh.isReadable);
                foreach(var m in r.sharedMaterials)
                    lines.Add("    "+m.name+" | "+m.shader.name+" | "+m.color+" | tex="+(m.mainTexture!=null?m.mainTexture.name:"none"));
            }
        }
        File.WriteAllLines(Output+"/source-materials.txt",lines);
        Debug.Log("TREE SOURCES INSPECTED");
    }

    static void EnsureFolders()
    {
        if(!AssetDatabase.IsValidFolder("Assets/ChickenHeistGenerated/Nature"))AssetDatabase.CreateFolder("Assets/ChickenHeistGenerated","Nature");
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/ChickenHeistGenerated/Nature","Trees");
        Directory.CreateDirectory(Output);
    }

    static readonly Dictionary<Material,Material> materialCache=new Dictionary<Material,Material>();
    static Color SavedColor(Material material)
    {
        var colors=new SerializedObject(material).FindProperty("m_SavedProperties.m_Colors");
        Color color=Color.white;
        for(int i=0;i<colors.arraySize;i++)
        {
            var entry=colors.GetArrayElementAtIndex(i);
            string name=entry.FindPropertyRelative("first").stringValue;
            if(name=="_BaseColor")return entry.FindPropertyRelative("second").colorValue;
            if(name=="_Color")color=entry.FindPropertyRelative("second").colorValue;
        }
        return color;
    }

    static Material TreeMaterial(Material source,bool vertexColor)
    {
        if(materialCache.TryGetValue(source,out var cached))return cached;
        string label=source.name.ToLowerInvariant();
        Texture texture=null;
        foreach(string property in new[]{"_BaseMap","_MainTex","_BaseColorMap"})
            if(source.HasProperty(property) && source.GetTexture(property)!=null){texture=source.GetTexture(property);break;}
        if(texture==null && source.name=="ColorAtlas")
            texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Prefabs/Environment/FreePack/FBX/ColorAtlas.png");
        bool bark=label.Contains("wood") || label.Contains("bark") || label.Contains("trunk") || label.Contains("dead") || label.Contains("stump");
        var shader=Shader.Find(vertexColor && texture==null?"ChickenHeist/VertexColorLit":"Universal Render Pipeline/Lit");
        var material=new Material(shader){name=source.name+" - Rural 3D",enableInstancing=true};
        Color color=texture!=null || vertexColor?Color.white:SavedColor(source);
        if(bark)color=label.Contains("top")?new Color(.48f,.33f,.19f):new Color(.32f,.205f,.115f);
        color.a=1;
        material.SetColor("_BaseColor",color);
        if(material.HasProperty("_BaseMap"))material.SetTexture("_BaseMap",texture);
        if(material.HasProperty("_Smoothness"))material.SetFloat("_Smoothness",.08f);
        string id=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(source));
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source,out string guid,out long localId);
        string path=Folder+"/"+source.name+"-"+id+"-"+localId+".mat";
        var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(existing!=null){EditorUtility.CopySerialized(material,existing);Object.DestroyImmediate(material);material=existing;}
        else AssetDatabase.CreateAsset(material,path);
        materialCache[source]=material;return material;
    }

    static void Calibrate(GameObject instance)
    {
        var points=new List<Vector3>();
        foreach(var filter in instance.GetComponentsInChildren<MeshFilter>(true))
        {
            // Editor access is allowed even when runtime mesh Read/Write is disabled.
            var mesh=filter.sharedMesh;
            foreach(var vertex in mesh.vertices)points.Add(instance.transform.InverseTransformPoint(filter.transform.TransformPoint(vertex)));
            var renderer=filter.GetComponent<Renderer>();
            var materials=renderer.sharedMaterials;
            bool vertexColor=mesh.colors32.Length==mesh.vertexCount;
            for(int i=0;i<materials.Length;i++)materials[i]=TreeMaterial(materials[i],vertexColor);
            renderer.sharedMaterials=materials;
        }
        if(points.Count==0)throw new System.InvalidOperationException("No root mesh: "+instance.name);
        float minY=float.PositiveInfinity,maxY=float.NegativeInfinity;
        foreach(var p in points){minY=Mathf.Min(minY,p.y);maxY=Mathf.Max(maxY,p.y);}
        var roots=new List<Vector3>();
        float band=Mathf.Max(.005f,(maxY-minY)*.006f);
        var unique=new HashSet<Vector3>();
        foreach(var p in points)if(p.y<=minY+band && unique.Add(p))roots.Add(p);
        var metadata=instance.GetComponent<RuralTreeRoots>()??instance.AddComponent<RuralTreeRoots>();
        metadata.contacts=roots.ToArray();metadata.materialsCalibrated=true;
    }

    static GameObject Prepared(GameObject source)
    {
        if(source==null)return null;
        var instance=Object.Instantiate(source);
        try
        {
            instance.name=source.name;
            Calibrate(instance);
            string path=Folder+"/"+source.name+".prefab";
            return PrefabUtility.SaveAsPrefabAsset(instance,path);
        }
        finally{Object.DestroyImmediate(instance);}
    }

    public static void PrepareGeneratorTrees(ProceduralFarmGenerator generator)
    {
        EnsureFolders();materialCache.Clear();
        for(int i=0;i<generator.treePrefabs.Length;i++)generator.treePrefabs[i]=Prepared(generator.treePrefabs[i]);
        generator.orchardTreePrefab=Prepared(generator.orchardTreePrefab);
    }

    [MenuItem("Chicken Heist/Repair Rural Trees")]
    public static void Repair()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=RuralWorldReview.WorldScene)throw new System.InvalidOperationException("Open ChickenHeistRuralWorld first.");
        EnsureFolders();materialCache.Clear();
        EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BeforeTreeRepair.unity"),true);
        var meshes=new Dictionary<Mesh,TreeSource>();
        var prefabs=new List<GameObject>();
        foreach(var source in Sources())
        {
            var prefab=Prepared(source);prefabs.Add(prefab);
            var metadata=prefab.GetComponent<RuralTreeRoots>();
            foreach(var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                meshes[filter.sharedMesh]=new TreeSource{name=prefab.name,contacts=(Vector3[])metadata.contacts.Clone(),materials=filter.GetComponent<Renderer>().sharedMaterials};
        }
        var roots=new Dictionary<Transform,TreeSource>();
        foreach(var filter in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            if(filter.sharedMesh==null || !meshes.TryGetValue(filter.sharedMesh,out var source))continue;
            Transform root=filter.transform;
            while(root.parent!=null && root.name.Replace("(Clone)","")!=source.name)root=root.parent;
            if(root.name.Replace("(Clone)","")!=source.name)
            {
                Debug.LogWarning("Tree root not matched: "+filter.name);continue;
            }
            filter.GetComponent<Renderer>().sharedMaterials=source.materials;
            roots[root]=source;
        }
        Physics.SyncTransforms();var surfaces=RuralTreeRoots.TerrainSurfaces();
        if(surfaces.Length==0)throw new System.InvalidOperationException("No terrain colliders found.");
        var lines=new List<string>();int moved=0,failed=0;float largest=0;Transform example=null;
        foreach(var entry in roots)
        {
            var metadata=entry.Key.GetComponent<RuralTreeRoots>()??entry.Key.gameObject.AddComponent<RuralTreeRoots>();
            metadata.contacts=(Vector3[])entry.Value.contacts.Clone();metadata.materialsCalibrated=true;
            if(!metadata.Ground(surfaces,out float delta)){failed++;lines.Add("FAIL no ground: "+entry.Key.name);continue;}
            if(Mathf.Abs(delta)>.03f)moved++;
            if(Mathf.Abs(delta)>largest){largest=Mathf.Abs(delta);example=entry.Key;}
            float gap=float.NegativeInfinity;
            foreach(var contact in metadata.contacts)
            {
                Vector3 p=metadata.transform.TransformPoint(contact);
                RuralTreeRoots.SurfaceHeight(surfaces,p,out float y);gap=Mathf.Max(gap,p.y-y);
            }
            if(gap>.002f){failed++;lines.Add("FAIL floating contact: "+entry.Key.name+" gap="+gap);}
        }
        Physics.SyncTransforms();
        lines.Insert(0,"Trees/stumps reviewed: "+roots.Count+" | repositioned >3cm: "+moved+" | maximum movement: "+largest.ToString("F3")+"m | failures: "+failed);
        foreach(var prefab in prefabs)
            foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                foreach(var material in renderer.sharedMaterials)
                    lines.Add("MATERIAL "+prefab.name+" / "+renderer.name+" / "+material.name+" / "+material.shader.name+" / "+material.GetColor("_BaseColor"));
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllLines(Output+"/tree-audit.txt",lines);
        if(example!=null)Capture(example.gameObject,Output+"/forest-grounded.png",false);
        int sample=0;
        foreach(var prefab in prefabs)
        {
            if(prefab.name!="Tree_Summer" && prefab.name!="Dead_Tree" && prefab.name!="Tree_01" && prefab.name!="Env_Tree_01" && prefab.name!="Tree_RedPlum")continue;
            var instance=Object.Instantiate(prefab,new Vector3(4000,0,4000),Quaternion.identity);
            try{Capture(instance,Output+"/species-"+(sample++)+".png",true);}
            finally{Object.DestroyImmediate(instance);}
        }
        Debug.Log("TREES REPAIRED: "+lines[0]);
        var character=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/LowPolyHuman2.obj");
        if(character!=null)
        {
            var preview=Object.Instantiate(character,new Vector3(4000,0,4000),Quaternion.identity);
            try
            {
                Capture(preview,Output+"/character-source.png",true);
                var report=new List<string>();
                foreach(var filter in preview.GetComponentsInChildren<MeshFilter>())
                    report.Add(filter.name+" "+filter.sharedMesh.bounds+" vertices="+filter.sharedMesh.vertexCount);
                File.WriteAllLines(Output+"/character-source.txt",report);
            }
            finally{Object.DestroyImmediate(preview);}
        }
    }

    static void Capture(GameObject tree,string path,bool isolated)
    {
        Bounds b=ProceduralFarmGenerator.VisualBounds(tree);
        var go=new GameObject("Temporary tree review camera");var camera=go.AddComponent<Camera>();
        float distance=Mathf.Max(b.size.y*1.25f,b.size.x*1.6f);
        camera.transform.position=b.center+new Vector3(distance*.60f,b.size.y*.12f,-distance);
        camera.transform.LookAt(b.center);camera.fieldOfView=48;camera.nearClipPlane=.03f;camera.farClipPlane=isolated?100:500;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.40f,.48f,.53f);
        var rt=new RenderTexture(1000,900,24);var previous=RenderTexture.active;bool fog=RenderSettings.fog;
        var sun=RenderSettings.sun;float intensity=sun!=null?sun.intensity:0;
        Texture2D texture=null;
        try
        {
            RenderSettings.fog=false;if(sun!=null)sun.intensity=1.6f;
            camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            texture=new Texture2D(1000,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1000,900),0,0);texture.Apply();
            File.WriteAllBytes(path,texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture=null;RenderTexture.active=previous;RenderSettings.fog=fog;if(sun!=null)sun.intensity=intensity;
            if(texture!=null)Object.DestroyImmediate(texture);rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(go);
        }
    }
}
