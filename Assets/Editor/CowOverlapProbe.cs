using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Diagnostic: cows that overlap. Edit-time pass: cow roots closer than 2 m and cows carrying more than one model.
// Play pass: 90 s of wandering, recording the closest pair per farm.
// Run: Unity.exe -batchmode -projectPath . -executeMethod CowOverlapProbe.Begin
[InitializeOnLoad]
public static class CowOverlapProbe
{
    const string Key="CowOverlapProbe",Out="output/cow-review/overlap.txt";
    static readonly List<string> log=new List<string>();static float until;static readonly Dictionary<string,float> closest=new Dictionary<string,float>();
    static CowOverlapProbe(){if(SessionState.GetBool(Key,false))EditorApplication.update+=Tick;}
    static List<Transform> Cows()=>Object.FindObjectsByType<SimpleAnimalWander>(FindObjectsInactive.Include,FindObjectsSortMode.None).Select(w=>w.transform).ToList();
    static string Path(Transform t){var s=t.name;for(var p=t.parent;p!=null;p=p.parent)s=p.name+"/"+s;return s;}
    public static void Begin()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var cows=Cows();log.Add("cows in scene: "+cows.Count+" (active "+cows.Count(c=>c.gameObject.activeInHierarchy)+")");
        var names=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(t=>t.name.StartsWith("Vaca")).ToList();
        log.Add("objects named Vaca*: "+names.Count+" ("+string.Join(", ",names.GroupBy(n=>n.name).Select(g=>g.Key+" x"+g.Count()))+")");
        foreach(var c in names.Where(n=>n.GetComponent<SimpleAnimalWander>()==null).Take(20))log.Add("  Vaca without wander: "+Path(c)+" at "+c.position.ToString("F2"));
        for(int i=0;i<cows.Count;i++)for(int j=i+1;j<cows.Count;j++)
        {
            float d=Vector2.Distance(new Vector2(cows[i].position.x,cows[i].position.z),new Vector2(cows[j].position.x,cows[j].position.z));
            if(d<2f)log.Add("EDIT OVERLAP "+d.ToString("F2")+" m: "+Path(cows[i])+" | "+Path(cows[j]));
        }
        foreach(var c in cows)
        {
            var skins=c.GetComponentsInChildren<Renderer>(true).Where(r=>r.GetComponent<SkinnedMeshRenderer>()!=null || r.bounds.size.magnitude>1.2f).ToList();
            var models=c.Cast<Transform>().Where(ch=>ch.GetComponentInChildren<Renderer>(true)!=null).Select(ch=>ch.name).ToList();
            if(models.Count>1)log.Add("MULTI MODEL "+Path(c)+": "+string.Join(", ",models));
        }
        foreach(var c in cows.Take(2))
        {
            log.Add("cow "+Path(c)+" lodgroup "+(c.GetComponentInChildren<LODGroup>(true)!=null));
            foreach(var r in c.GetComponentsInChildren<Renderer>(true))
            {
                var mesh=r is SkinnedMeshRenderer sk?sk.sharedMesh:r.GetComponent<MeshFilter>()?.sharedMesh;
                log.Add("  renderer "+Path(r.transform).Substring(Path(c).Length)+" enabled "+r.enabled+" active "+r.gameObject.activeInHierarchy+" mesh "+(mesh!=null?mesh.name+" v"+mesh.vertexCount:"none")+" size "+r.bounds.size.ToString("F2"));
            }
        }
        // Render one cow with both meshes and with each mesh alone.
        {
            var cow=cows[0];var rs=cow.GetComponentsInChildren<Renderer>(true);
            var go=new GameObject("probe cam");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.55f,.6f,.65f);
            var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);
            go.transform.position=b.center+cow.right*4.2f+Vector3.up*1.2f+cow.forward*1.5f;go.transform.LookAt(b.center);
            var light=new GameObject("probe light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(40,30,0);
            void Shot(string name){var rt=new RenderTexture(640,400,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;var t=new Texture2D(640,400);t.ReadPixels(new Rect(0,0,640,400),0,0);t.Apply();
                File.WriteAllBytes("output/cow-review/"+name+".png",t.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;rt.Release();}
            Directory.CreateDirectory("output/cow-review");
            Shot("both");
            for(int k=0;k<rs.Length;k++){for(int m=0;m<rs.Length;m++)rs[m].enabled=m==k;Shot("only-"+rs[k].name);}
            foreach(var r in rs)r.enabled=true;Object.DestroyImmediate(go);Object.DestroyImmediate(light.gameObject);
        }
        var groups=cows.Select(c=>c.GetComponentsInChildren<Renderer>(true).Count(r=>r.enabled && r.gameObject.activeInHierarchy)).GroupBy(n=>n).Select(g=>g.Key+" renderers x"+g.Count());
        log.Add("renderers per cow: "+string.Join(", ",groups));
        Directory.CreateDirectory("output/cow-review");File.WriteAllLines("output/cow-review/scene.txt",log);
        if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-sceneOnly")>=0){EditorApplication.Exit(0);return;}
        SessionState.SetBool(Key,true);EditorApplication.update+=Tick;EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying)return;
        if(until==0){until=(float)EditorApplication.timeSinceStartup+95;if(GameMenu.Instance!=null)GameMenu.Instance.Resume();return;}
        var cows=Cows().Where(c=>c.gameObject.activeInHierarchy).ToList();
        foreach(var g in cows.GroupBy(c=>c.parent!=null?c.parent.parent!=null?c.parent.parent.name:c.parent.name:"-"))
        {
            var list=g.ToList();
            for(int i=0;i<list.Count;i++)for(int j=i+1;j<list.Count;j++)
            {
                float d=Vector2.Distance(new Vector2(list[i].position.x,list[i].position.z),new Vector2(list[j].position.x,list[j].position.z));
                if(!closest.TryGetValue(g.Key,out var best) || d<best)closest[g.Key]=d;
            }
        }
        if(EditorApplication.timeSinceStartup<until)return;
        log.Add("PLAY closest pair per group over 90 s:");
        foreach(var kv in closest.OrderBy(k=>k.Value))log.Add("  "+kv.Value.ToString("F2")+" m  "+kv.Key);
        Directory.CreateDirectory("output/cow-review");File.WriteAllLines(Out,log);
        SessionState.SetBool(Key,false);EditorApplication.update-=Tick;EditorApplication.Exit(0);
    }
}
