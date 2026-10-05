using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Diagnostic for Elias v3 (Tools/SourceArt/ProtagonistV3): first-person body seen from the player camera,
// and a line-up of Elias beside the cast (merchant, farmers, walkers) under the same light.
// Run: Unity.exe -batchmode -projectPath . -executeMethod EliasV3Review.Begin
[InitializeOnLoad]
public static class EliasV3Review
{
    const string Key="EliasV3Review",Folder="output/elias-v3";
    static readonly List<string> report=new List<string>();
    static int phase;static float next;static bool failed;

    static EliasV3Review()
    {
        EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false) && (t==LogType.Exception || t==LogType.Error)){report.Add("ERROR "+m);failed=true;}};
    }
    public static void Begin()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/elias-v3-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount{introSeen=true});SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;report.Clear();failed=false;next=Time.realtimeSinceStartup+4;Application.runInBackground=true;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(failed?1:0);}
    }
    static void Check(bool ok,string text){report.Add((ok?"PASS ":"FAIL ")+text);if(!ok)failed=true;}

    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try
        {
            if(phase==0){GameMenu.Instance.Resume();next=Time.realtimeSinceStartup+6;phase++;return;}
            if(phase==1){FirstPerson();phase++;next=Time.realtimeSinceStartup+1;return;}
            if(phase==2){LineUp();File.WriteAllLines(Folder+"/review.txt",report);EditorApplication.isPlaying=false;}
        }
        catch(Exception e){failed=true;report.Add("FAIL "+e);File.WriteAllLines(Folder+"/review.txt",report);EditorApplication.isPlaying=false;}
    }

    static Transform Player=>HeistGameManager.Instance.player;

    static void FirstPerson()
    {
        var eyes=Player.GetComponentInChildren<Camera>();
        var fp=Player.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s=>s.name=="Corpo em primeira pessoa");
        Check(fp!=null,"first-person body present");
        if(fp!=null)
        {
            int tris=fp.sharedMesh.triangles.Length/3;
            Check(fp.enabled && fp.gameObject.activeInHierarchy && fp.gameObject.layer==30,"first-person body enabled on layer 30 (tris "+tris+", bones "+fp.bones.Length+")");
            Check((eyes.cullingMask & (1<<30))!=0 && (eyes.cullingMask & (1<<31))==0,"player camera renders layer 30 and hides layer 31");
            Check(tris>1000,"first-person body keeps torso, arms and legs");
            Check(fp.sharedMaterial!=null && fp.sharedMaterial.mainTexture!=null,"first-person material has the palette ("+(fp.sharedMaterial!=null && fp.sharedMaterial.mainTexture!=null?fp.sharedMaterial.mainTexture.name:"none")+")");
        }
        foreach(var skin in Player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            report.Add("INFO skin "+skin.name+" layer "+skin.gameObject.layer+" material "+(skin.sharedMaterial!=null?skin.sharedMaterial.name:"none")+" verts "+skin.sharedMesh.vertexCount);
        var head=Player.GetComponentsInChildren<Transform>().First(t=>t.name=="Head");
        report.Add("INFO eye "+Player.InverseTransformPoint(eyes.transform.position)+" head bone "+Player.InverseTransformPoint(head.position));
        var old=eyes.transform.localRotation;
        foreach(var (name,euler) in new[]{("fp-down",new Vector3(70,0,0)),("fp-down-right",new Vector3(50,35,0)),("fp-ahead",Vector3.zero)})
        {eyes.transform.localRotation=Quaternion.Euler(euler);Save(eyes,name);}
        eyes.transform.localRotation=old;
    }

    static void LineUp()
    {
        var player=Player;
        var cast=new List<Transform>();
        string[] wanted={"Seu Anselmo","Morador 2","Morador 4","Morador 6","Morador 8"};
        foreach(var w in wanted)
        {
            var t=Object.FindObjectsByType<Transform>().FirstOrDefault(x=>x.name.StartsWith(w) && x.GetComponentInChildren<SkinnedMeshRenderer>()!=null);
            if(t!=null)cast.Add(t);else report.Add("INFO not found "+w);
        }
        var body=player.GetComponentsInChildren<Animation>().First(a=>a.transform.Find("Protagonist_Rigged(Clone)")!=null).transform;
        var moved=new List<(Transform t,Vector3 p,Quaternion r)>();
        var layers=new Dictionary<GameObject,int>();
        SkinnedMeshRenderer[] Skins(Transform root)=>root.GetComponentsInChildren<SkinnedMeshRenderer>().Where(r=>r.name!="Corpo em primeira pessoa").ToArray();
        Bounds Measure(Transform root){var skins=Skins(root);var b=skins[0].bounds;foreach(var r in skins)b.Encapsulate(r.bounds);return b;}
        void Layer(Transform root){foreach(var r in Skins(root)){layers[r.gameObject]=r.gameObject.layer;r.gameObject.layer=28;}}
        Layer(body);
        var eb=Measure(body);var side=body.right;
        var heights=new List<string>{"Elias "+eb.size.y.ToString("0.00")+"m"};
        for(int i=0;i<cast.Count;i++)
        {
            var t=cast[i];moved.Add((t,t.position,t.rotation));t.rotation=body.rotation;
            foreach(var a in t.GetComponentsInChildren<Animator>())a.Update(0);
            var nb=Measure(t);var target=new Vector3(eb.center.x,eb.min.y,eb.center.z)+side*(.9f*(i+1));
            t.position+=target-new Vector3(nb.center.x,nb.min.y,nb.center.z);Layer(t);
            heights.Add(t.name+" "+nb.size.y.ToString("0.00")+"m");
        }
        var go=new GameObject("Line-up camera");var camera=go.AddComponent<Camera>();
        Vector3 centre=new Vector3(eb.center.x,eb.min.y,eb.center.z)+side*(.9f*cast.Count/2f)+Vector3.up*1.0f;
        camera.transform.position=centre+body.forward*10f;camera.transform.LookAt(centre);
        camera.orthographic=true;camera.orthographicSize=1.15f;camera.nearClipPlane=.1f;camera.farClipPlane=30;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.18f);camera.cullingMask=1<<28;
        var lightObj=new GameObject("Line-up light");var light=lightObj.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.cullingMask=1<<28;lightObj.transform.rotation=Quaternion.LookRotation(-camera.transform.forward+Vector3.down*.8f);
        Save(camera,"lineup",1800,800);
        report.Add("INFO heights (skinned bounds): "+string.Join(" | ",heights));
        Object.DestroyImmediate(go);Object.DestroyImmediate(lightObj);
        foreach(var kv in layers)if(kv.Key!=null)kv.Key.layer=kv.Value;
        foreach(var m in moved){m.t.position=m.p;m.t.rotation=m.r;}
    }
    static float GroundBelow(Transform t)=>Physics.Raycast(t.position+Vector3.up*2,Vector3.down,out var hit,10)?hit.point.y:t.position.y;

    static void Save(Camera c,string name,int w=960,int h=720)
    {
        var texture=new RenderTexture(w,h,24);texture.Create();var old=c.targetTexture;c.targetTexture=texture;c.Render();
        var active=RenderTexture.active;RenderTexture.active=texture;var png=new Texture2D(w,h,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,w,h),0,0);png.Apply();
        File.WriteAllBytes(Folder+"/"+name+".png",png.EncodeToPNG());Object.DestroyImmediate(png);RenderTexture.active=active;c.targetTexture=old;texture.Release();Object.DestroyImmediate(texture);
    }
}
