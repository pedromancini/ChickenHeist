using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CastSourceAudit
{
    public const string Pack="Assets/ImportedMedievalPeople/fbx/people_unity/";
    public static readonly string[] Banned={"king","queen","rich_citizzens_3","rich_citizzens_4"};
    public static void Run()
    {
        var scene=EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var lines=new System.Collections.Generic.List<string>();int broken=0;
        foreach(var skin in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None).OrderBy(s=>Path(s.transform)))
        {
            var mesh=skin.sharedMesh;string source=mesh!=null?AssetDatabase.GetAssetPath(mesh):"(none)";
            string model=System.IO.Path.GetFileNameWithoutExtension(source);
            bool elias=source.Contains("EliasNative") || source.Contains("Protagonist"),visitor=source.Contains("HoodedVisitor");
            bool ok=elias || visitor || source.StartsWith(Pack) && !Banned.Contains(model);
            if(!ok)broken++;
            lines.Add((ok?"ok   ":"BAD  ")+Path(skin.transform)+"  <-  "+source);
        }
        foreach(var path in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Resources"}).Select(AssetDatabase.GUIDToAssetPath))
            foreach(var dep in AssetDatabase.GetDependencies(path,true).Where(d=>d.EndsWith(".fbx") && (d.Contains("ImportedMedievalPeople") || d.Contains("ImportedVillagerNPC"))))
            {
                string model=System.IO.Path.GetFileNameWithoutExtension(dep);bool ok=dep.StartsWith(Pack) && !Banned.Contains(model);
                if(!ok)broken++;lines.Add((ok?"ok   ":"BAD  ")+path+"  <-  "+dep);
            }
        lines.Insert(0,(broken==0?"PASS":"FAIL")+" cast: "+broken+" characters outside the allowed set");
        Directory.CreateDirectory("output/cast-audit");File.WriteAllLines("output/cast-audit/cast.txt",lines);
        if(Application.isBatchMode)EditorApplication.Exit(broken==0?0:1);
    }
    static string Path(Transform t){var s=t.name;while(t.parent!=null){t=t.parent;s=t.name+"/"+s;}return s;}
}
