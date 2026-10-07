using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Diagnostic sweep: missing scripts, missing/pink materials, empty meshes, silent audio sources in the scene and
// prefabs; then a play session that visits the home, the coop, the market road and a farm and records every
// warning and error the game logs. Run: Unity.exe -batchmode -projectPath . -executeMethod ProjectHealthScan.Begin
[InitializeOnLoad]
public static class ProjectHealthScan
{
    const string Key="ProjectHealthScan",Folder="output/health";
    static readonly List<string> report=new List<string>();
    static readonly Dictionary<string,int> logged=new Dictionary<string,int>();
    static int phase;static float next;
    static ProjectHealthScan()
    {
        EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;
        Application.logMessageReceived+=(m,s,t)=>
        {
            if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || t==LogType.Log)return;
            string key=t+": "+m.Split('\n')[0];logged[key]=logged.TryGetValue(key,out int n)?n+1:1;
        };
    }
    public static void Begin()
    {
        Directory.CreateDirectory(Folder);report.Clear();logged.Clear();
        var scene=EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        int missing=0,pink=0,noMat=0,emptyMesh=0,silent=0;
        void Inspect(GameObject root,string where)
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))
            {
                var go=t.gameObject;string path=where+":"+AnimationUtility.CalculateTransformPath(t,root.transform);
                int m=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);if(m>0){missing+=m;report.Add("MISSING SCRIPT x"+m+" "+path);}
                var r=go.GetComponent<Renderer>();
                if(r!=null && !(r is ParticleSystemRenderer))
                {
                    if(r.sharedMaterials.Length==0 || r.sharedMaterials.Any(x=>x==null)){noMat++;if(noMat<=40)report.Add("NULL MATERIAL "+path);}
                    else if(r.sharedMaterials.Any(x=>x.shader==null || x.shader.name.Contains("InternalErrorShader") || !x.shader.isSupported)){pink++;report.Add("ERROR SHADER "+path);}
                }
                var mf=go.GetComponent<MeshFilter>();if(mf!=null && go.GetComponent<MeshRenderer>()!=null && (mf.sharedMesh==null || mf.sharedMesh.vertexCount==0)){emptyMesh++;if(emptyMesh<=40)report.Add("EMPTY MESH "+path);}
                var sm=go.GetComponent<SkinnedMeshRenderer>();if(sm!=null && sm.sharedMesh==null){emptyMesh++;report.Add("EMPTY SKIN "+path);}
                var a=go.GetComponent<AudioSource>();if(a!=null && a.playOnAwake && a.clip==null){silent++;report.Add("PLAY-ON-AWAKE AUDIO WITHOUT CLIP "+path);}
            }
        }
        foreach(var root in scene.GetRootGameObjects())Inspect(root,"scene");
        int prefabs=0;
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/ChickenHeistGenerated","Assets/Resources","Assets/Prefabs"}))
        {var path=AssetDatabase.GUIDToAssetPath(guid);var go=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(go==null)continue;prefabs++;Inspect(go,path);}
        report.Insert(0,"STATIC: missing scripts "+missing+", null materials "+noMat+", error shaders "+pink+", empty meshes "+emptyMesh+", silent play-on-awake audio "+silent+" (scene + "+prefabs+" prefabs)");
        File.WriteAllLines(Folder+"/static.txt",report); // play mode reloads the domain and clears this list
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/health-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount{introSeen=true,flock=5});
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;next=Time.realtimeSinceStartup+4;Application.runInBackground=true;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try
        {
            var game=HeistGameManager.Instance;
            if(phase==0){GameMenu.Instance.Resume();next=Time.realtimeSinceStartup+3;phase++;return;}
            var home=HouseholdEconomy.Instance.home;
            Vector3[] stops={home.TransformPoint(new Vector3(-3.2f,.8f,4.5f)),home.TransformPoint(new Vector3(-3.2f,.2f,-1.5f)),home.TransformPoint(new Vector3(11,.2f,-6.5f)),
                home.TransformPoint(new Vector3(12f,.2f,-3f)),home.TransformPoint(new Vector3(-3f,.2f,-9f))};
            var farm=Object.FindObjectsByType<FarmLayoutInfo>().FirstOrDefault();
            if(phase<=stops.Length)
            {
                var p=phase<stops.Length?stops[phase-1+0]:(farm!=null?farm.transform.position+Vector3.up*.5f:stops[0]);
                var cc=game.player.GetComponent<CharacterController>();cc.enabled=false;game.player.position=p;cc.enabled=true;
                if(phase==3)HomeCoopGate.Instance?.Toggle();if(phase==2)HomeDoorbell.Instance?.Ring();
                phase++;next=Time.realtimeSinceStartup+5;return;
            }
            report.Add("RUNTIME: "+logged.Count+" distinct warnings/errors during a "+(phase*5)+" s visit");
            foreach(var kv in logged.OrderByDescending(k=>k.Value))report.Add("  x"+kv.Value+" "+kv.Key);
        }
        catch(Exception e){report.Add("FAIL "+e);}
        File.WriteAllLines(Folder+"/report.txt",report);EditorApplication.isPlaying=false;
    }
}
