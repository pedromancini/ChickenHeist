using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Diagnostic: foot contact measured on the baked skinned mesh (lowest real vertex), not on renderer bounds,
// for the player body, the merchant and every walker, sampled over a few seconds of their own animation.
// Run: Unity.exe -batchmode -projectPath . -executeMethod CharacterGroundAudit.Begin
[InitializeOnLoad]
public static class CharacterGroundAudit
{
    const string Key="CharacterGroundAudit",Folder="output/character-ground-audit";
    static readonly List<string> report=new List<string>();
    static readonly Dictionary<string,List<float>> samples=new Dictionary<string,List<float>>();
    static int phase,taken;static float next;

    static CharacterGroundAudit(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void Begin()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/ground-audit-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount{introSeen=true});SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;taken=0;report.Clear();samples.Clear();next=Time.realtimeSinceStartup+4;Application.runInBackground=true;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}
    }
    static IEnumerable<(string name,Transform root)> Subjects()
    {
        var player=HeistGameManager.Instance.player;
        var body=player.GetComponentsInChildren<Animation>().FirstOrDefault(a=>a.transform.Find("Protagonist_Rigged(Clone)")!=null);
        if(body!=null)yield return ("Elias (player)",body.transform);
        foreach(var w in Object.FindObjectsByType<RoadsideWalker>())yield return (w.name,w.transform);
        var merchant=Object.FindObjectsByType<Transform>().FirstOrDefault(t=>t.name.StartsWith("Seu Anselmo"));
        if(merchant!=null)yield return (merchant.name,merchant);
    }
    static float Lowest(Transform root)
    {
        float low=float.MaxValue;var mesh=new Mesh();
        foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(s=>s.name!="Corpo em primeira pessoa" && s.enabled))
        {
            skin.BakeMesh(mesh,true);
            foreach(var v in mesh.vertices)low=Mathf.Min(low,skin.transform.TransformPoint(v).y);
        }
        Object.DestroyImmediate(mesh);return low;
    }
    static float Ground(Transform root,Vector3 at)
    {
        var hits=Physics.RaycastAll(at+Vector3.up*1.5f,Vector3.down,4f).Where(h=>!h.collider.transform.IsChildOf(root) && !h.collider.isTrigger &&
            h.collider.GetComponentInParent<CharacterController>()==null).OrderBy(h=>h.distance).ToArray();
        return hits.Length>0?hits[0].point.y:float.NaN;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try
        {
            if(phase==0){GameMenu.Instance.Resume();next=Time.realtimeSinceStartup+5;phase++;return;}
            foreach(var (name,root) in Subjects())
            {
                var feet=root.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Foot") || t.name.StartsWith("Toe") || t.name.StartsWith("Ball")).ToArray();
                var at=feet.Length>0?feet.Aggregate(Vector3.zero,(a,t)=>a+t.position)/feet.Length:root.position;
                float g=Ground(root,at);if(float.IsNaN(g))continue;
                if(!samples.ContainsKey(name))samples[name]=new List<float>();samples[name].Add(Lowest(root)-g);
            }
            if(++taken<8){next=Time.realtimeSinceStartup+.37f;return;}
            var anselmo=Subjects().FirstOrDefault(x=>x.name.StartsWith("Seu Anselmo")).root;
            if(anselmo!=null)
            {
                var foot=anselmo.GetComponentsInChildren<Transform>().First(t=>t.name.StartsWith("Foot"));
                foreach(var h in Physics.RaycastAll(foot.position+Vector3.up*1.5f,Vector3.down,4f).OrderBy(h=>h.distance))
                    report.Add("INFO Anselmo ray hit "+h.collider.name+" at "+h.point.y.ToString("0.000")+" (foot bone "+foot.position.y.ToString("0.000")+")");
                foreach(var r in Object.FindObjectsByType<MeshRenderer>().Where(r=>r.bounds.Contains(foot.position+Vector3.down*.03f)))
                    report.Add("INFO renderer around Anselmo's feet: "+r.name+" top "+r.bounds.max.y.ToString("0.000"));
                var go=new GameObject("feet cam");var cam=go.AddComponent<Camera>();var f=anselmo.forward;
                cam.transform.position=foot.position+f*1.1f+Vector3.up*.35f;cam.transform.LookAt(foot.position);cam.nearClipPlane=.02f;cam.fieldOfView=50;
                var tex=new RenderTexture(800,600,24);cam.targetTexture=tex;cam.Render();RenderTexture.active=tex;
                var png=new Texture2D(800,600,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,800,600),0,0);png.Apply();
                File.WriteAllBytes(Folder+"/anselmo-feet.png",png.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;Object.DestroyImmediate(png);tex.Release();Object.DestroyImmediate(go);
            }
            foreach(var kv in samples.OrderBy(k=>k.Key))
            {
                float min=kv.Value.Min(),max=kv.Value.Max(),mean=kv.Value.Average();
                string flag=min>.03f?"FLOATING":min< -.05f?"SINKS":"ok";
                report.Add(flag.PadRight(9)+kv.Key+" | lowest vertex above ground: min "+min.ToString("0.000")+" mean "+mean.ToString("0.000")+" max "+max.ToString("0.000")+" ("+kv.Value.Count+" samples)");
            }
        }
        catch(Exception e){report.Add("FAIL "+e);}
        File.WriteAllLines(Folder+"/report.txt",report);EditorApplication.isPlaying=false;
    }
}
