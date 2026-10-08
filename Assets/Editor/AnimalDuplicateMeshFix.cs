using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// The animal library ships some animals as two full alternative meshes under one node (e.g. BrownCow: alt578 and
// alt579, two coat patterns). Both were drawn on top of each other and FarmAnimalMotion only bends the first mesh,
// so a walking cow showed a second, rigid cow inside it. Keeps one mesh per animal (alternating the variant between
// animals for variety) in the generated prefabs and in the world scene.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod AnimalDuplicateMeshFix.Run
public static class AnimalDuplicateMeshFix
{
    const string Folder="Assets/ChickenHeistGenerated/Animals";
    static readonly List<string> log=new List<string>();
    // Renderers whose bounds nearly coincide are alternative copies of the same animal, not separate parts.
    static List<List<Renderer>> Duplicates(GameObject animal)
    {
        var rs=animal.GetComponentsInChildren<Renderer>(true).Where(r=>r.GetComponent<MeshFilter>()!=null || r is SkinnedMeshRenderer).ToList();
        var groups=new List<List<Renderer>>();
        foreach(var r in rs)
        {
            var g=groups.FirstOrDefault(x=>Same(x[0].bounds,r.bounds));
            if(g==null)groups.Add(new List<Renderer>{r});else g.Add(r);
        }
        return groups.Where(g=>g.Count>1).ToList();
    }
    static bool Same(Bounds a,Bounds b)=>Vector3.Distance(a.center,b.center)<.05f*a.size.magnitude && Vector3.Distance(a.size,b.size)<.08f*a.size.magnitude;
    static int Fix(GameObject animal,int variant)
    {
        int removed=0;
        foreach(var g in Duplicates(animal))
        {
            var keep=g[variant%g.Count];
            foreach(var r in g.Where(r=>r!=keep)){Object.DestroyImmediate(r.gameObject,true);removed++;}
        }
        return removed;
    }
    public static void Run()
    {
        foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{Folder}).Select(AssetDatabase.GUIDToAssetPath))
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try{int n=Fix(root,0);if(n>0){PrefabUtility.SaveAsPrefabAsset(root,path);log.Add("prefab "+path+": removed "+n+" duplicate mesh(es)");}}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        var scene=EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var animals=Object.FindObjectsByType<FarmAnimalMotion>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(m=>m.gameObject)
            .Concat(Object.FindObjectsByType<SimpleAnimalWander>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(w=>w.gameObject))
            .Concat(Object.FindObjectsByType<InteractableChicken>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(c=>c.gameObject))
            .Distinct().OrderBy(g=>g.transform.position.x).ThenBy(g=>g.transform.position.z).ToList();
        int total=0,fixedAnimals=0;
        for(int i=0;i<animals.Count;i++)
        {
            if(PrefabUtility.IsPartOfPrefabInstance(animals[i]))PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(animals[i]),PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            int n=Fix(animals[i],i);if(n>0){total+=n;fixedAnimals++;}
        }
        log.Add("scene: "+animals.Count+" animals checked, "+fixedAnimals+" fixed, "+total+" duplicate meshes removed");
        int left=animals.Count(a=>a!=null && Duplicates(a).Count>0);
        log.Add("animals still carrying duplicate meshes: "+left);
        // FarmAnimalMotion bends a readable rest copy from Resources/AnimalMotion; the second variants had none.
        foreach(var filter in animals.Where(a=>a!=null && a.GetComponent<FarmAnimalMotion>()!=null).Select(a=>a.GetComponentInChildren<MeshFilter>()).Where(f=>f!=null))
        {
            var source=filter.sharedMesh;string path="Assets/Resources/AnimalMotion/"+source.name+".asset";
            if(AssetDatabase.LoadAssetAtPath<Mesh>(path)!=null)continue;
            var mesh=new Mesh{name=source.name,vertices=source.vertices,normals=source.normals,uv=source.uv,colors=source.colors,subMeshCount=source.subMeshCount};
            for(int i=0;i<source.subMeshCount;i++)mesh.SetTriangles(source.GetTriangles(i),i);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
            log.Add("baked motion rest mesh "+path);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("output/cow-review");File.WriteAllLines("output/cow-review/fix.txt",log);
        Debug.Log("ANIMAL DUPLICATE MESH FIX\n"+string.Join("\n",log));
    }
}
