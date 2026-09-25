using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Follows every resident for a while and reports whether they roam naturally instead of pacing.
// Run: Unity.exe -projectPath . -executeMethod WalkerBehaviourReview.Begin
[InitializeOnLoad]
public static class WalkerBehaviourReview
{
    const string Key="WalkerBehaviourReview",Folder="output/walker-review";const float Duration=90;
    static float start,nextSample;static bool failed;
    static readonly Dictionary<RoadsideWalker,List<Vector3>> trails=new Dictionary<RoadsideWalker,List<Vector3>>();
    static readonly Dictionary<RoadsideWalker,int> pauses=new Dictionary<RoadsideWalker,int>();
    static readonly Dictionary<RoadsideWalker,bool> wasWalking=new Dictionary<RoadsideWalker,bool>();
    static readonly List<string> lines=new List<string>();
    static WalkerBehaviourReview()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=s=>{
            if(!SessionState.GetBool(Key,false))return;
            if(s==PlayModeStateChange.EnteredPlayMode){start=Time.realtimeSinceStartup+4;nextSample=0;trails.Clear();pauses.Clear();wasWalking.Clear();lines.Clear();Application.runInBackground=true;}
            if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(failed?1:0);}
        };
        Application.logMessageReceived+=(m,st,t)=>{if(SessionState.GetBool(Key,false) && t==LogType.Exception){failed=true;lines.Add("EXCEPTION "+m+"\n"+st);}};
    }
    public static void Begin()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/walkers-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount());SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<start)return;
        if(trails.Count==0)
        {
            GameMenu.Instance.Resume();
            // Keep the player out of everyone's way.
            var player=HeistGameManager.Instance.player;var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.position+=Vector3.up*400;
            player.GetComponent<PlayerMovement>().enabled=false;
            foreach(var w in Object.FindObjectsByType<RoadsideWalker>(FindObjectsSortMode.None)){trails[w]=new List<Vector3>();pauses[w]=0;wasWalking[w]=true;}
            lines.Add("Network nodes: "+(WalkNetwork.Shared?.nodes.Count??0));
        }
        if(Time.realtimeSinceStartup>=nextSample)
        {
            nextSample=Time.realtimeSinceStartup+.5f;
            foreach(var w in trails.Keys){trails[w].Add(w.transform.position);if(wasWalking[w] && !w.IsWalking)pauses[w]++;wasWalking[w]=w.IsWalking;}
        }
        if(Time.realtimeSinceStartup<start+Duration)return;
        int pacing=0;
        foreach(var kv in trails.OrderBy(k=>k.Key.name))
        {
            var t=kv.Value;var home=t[0];
            float walked=kv.Key.DistanceWalked,far=t.Max(p=>Vector3.Distance(p,home));
            var min=t.Aggregate(t[0],Vector3.Min);var max=t.Aggregate(t[0],Vector3.Max);float area=(max.x-min.x)*(max.z-min.z);
            // Pacing = coming back through the same few cells over and over.
            int cells=t.Select(p=>(Mathf.RoundToInt(p.x/4),Mathf.RoundToInt(p.z/4))).Distinct().Count();
            bool pace=walked>20 && cells<walked/4*.45f;if(pace)pacing++;
            lines.Add($"{kv.Key.name}: walked {walked:0}m, farthest {far:0}m from start, area {area:0}m2, distinct 4m cells {cells}, pauses {pauses[kv.Key]}, reversals {kv.Key.Reversals}, last obstacle {kv.Key.LastObstacle}{(pace?"  <- repetitive":"")}");
        }
        failed|=pacing>trails.Count/3 || trails.Keys.Any(w=>w.DistanceWalked<15);
        lines.Add((failed?"FAIL":"PASS")+" residents roam: repetitive="+pacing+"/"+trails.Count);
        Capture();File.WriteAllLines(Folder+"/report.txt",lines);EditorApplication.isPlaying=false;
    }
    // Top view of each trail over the scene.
    static void Capture()
    {
        var all=trails.Values.SelectMany(p=>p).ToList();var min=all.Aggregate(all[0],Vector3.Min);var max=all.Aggregate(all[0],Vector3.Max);
        var center=(min+max)*.5f;float size=Mathf.Max(max.x-min.x,max.z-min.z)*.6f+20;
        var go=new GameObject("Walker review camera");var eye=go.AddComponent<Camera>();eye.orthographic=true;eye.orthographicSize=size;
        eye.transform.position=center+Vector3.up*300;eye.transform.rotation=Quaternion.Euler(90,0,0);eye.farClipPlane=800;eye.clearFlags=CameraClearFlags.SolidColor;eye.backgroundColor=Color.gray;
        RenderSettings.fog=false;RenderSettings.ambientLight=new Color(.6f,.6f,.6f);
        var rt=new RenderTexture(1600,1600,24);eye.targetTexture=rt;eye.Render();RenderTexture.active=rt;
        var tex=new Texture2D(1600,1600,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1600),0,0);
        Color[] colors={Color.red,Color.yellow,Color.cyan,Color.magenta,Color.white,new Color(1,.5f,0),Color.green,Color.blue,Color.black,new Color(.6f,0,1)};
        int index=0;
        foreach(var t in trails.OrderBy(k=>k.Key.name).Select(k=>k.Value))
        {
            var c=colors[index++%colors.Length];
            foreach(var p in t){var s=eye.WorldToViewportPoint(p);int x=(int)(s.x*1600),y=(int)(s.y*1600);for(int dx=-3;dx<=3;dx++)for(int dy=-3;dy<=3;dy++)if(x+dx>=0 && x+dx<1600 && y+dy>=0 && y+dy<1600)tex.SetPixel(x+dx,y+dy,c);}
        }
        tex.Apply();File.WriteAllBytes(Folder+"/trails.png",tex.EncodeToPNG());RenderTexture.active=null;Object.DestroyImmediate(go);
    }
}
