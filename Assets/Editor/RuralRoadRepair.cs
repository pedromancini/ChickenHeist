using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class RuralRoadRepair
{
    [MenuItem("Chicken Heist/Repair Dirt Roads (Keep Farms)")]
    public static void Repair()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var scene=SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BeforeRoadRepair.unity"),true);
        var spans=new List<RuralRoadSpan>(Object.FindObjectsByType<RuralRoadSpan>(FindObjectsSortMode.None));
        foreach (var mf in Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
        {
            string n=mf.name;
            bool road=n.StartsWith("Caminho Rural Principal") || n.StartsWith("Trilha de Terra Secundaria") ||
                n=="Acesso ao patio" || n=="Via de servico" || n=="Caminho entre setores" || n=="Entrada do setor";
            if (!road || mf.GetComponent<RuralRoadSpan>()!=null) continue;
            Vector3[] v=mf.sharedMesh.vertices;
            if (v.Length<16 || v.Length%8!=0) throw new System.InvalidOperationException("Unknown legacy road mesh: "+n);
            Vector3 a=mf.transform.TransformPoint((v[3]+v[4])*0.5f);
            Vector3 b=mf.transform.TransformPoint((v[v.Length-5]+v[v.Length-4])*0.5f);
            float width=Vector3.Distance(mf.transform.TransformPoint(v[1]),mf.transform.TransformPoint(v[6]))/0.8f;
            var span=mf.gameObject.AddComponent<RuralRoadSpan>();
            span.start=a; span.end=b; span.width=width;
            spans.Add(span);
            Object.DestroyImmediate(mf.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(mf);
        }
        GameObject previous=GameObject.Find(RuralRoadSurface.RootName);
        if (previous!=null) Object.DestroyImmediate(previous);
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/ground/Ground048_1K-JPG_Color.jpg");
        RuralRoadSurface.Build(spans,texture);
        RuralWorldReview.PersistGeneratedAssets(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Verify(spans);
        RuralWorldReview.AuditAndCapture();
    }

    private static void Verify(List<RuralRoadSpan> spans)
    {
        var root=GameObject.Find(RuralRoadSurface.RootName);
        int duplicate=0,degenerate=0,triangleCount=0;
        var seen=new HashSet<string>();
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
        {
            var mesh=mf.sharedMesh; var v=mesh.vertices; var t=mesh.triangles;
            for (int i=0;i<t.Length;i+=3)
            {
                triangleCount++;
                Vector3 a=v[t[i]],b=v[t[i+1]],c=v[t[i+2]];
                if (Vector3.Cross(b-a,c-a).sqrMagnitude<0.000000001f) degenerate++;
                var points=new[] {a.ToString("F4"),b.ToString("F4"),c.ToString("F4")};
                System.Array.Sort(points,System.StringComparer.Ordinal);
                if (!seen.Add(string.Join("|",points))) duplicate++;
            }
        }
        Directory.CreateDirectory("output/world-review");
        File.WriteAllLines("output/world-review/road-audit.txt",new[] {
            "Spans="+spans.Count+" | triangles="+triangleCount+" | duplicate triangles="+duplicate+" | degenerate triangles="+degenerate,
            "Single clipped cell tessellation; no overlapping segment renderers.",
            duplicate==0 && degenerate==0 && triangleCount>0 ? "PASS":"FAIL"
        });
    }
}
