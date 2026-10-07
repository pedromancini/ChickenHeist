using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Diagnostic: renders the protagonist's house (interior, front door) and worn coop from fixed viewpoints in
// play mode, with the game's own night lighting. Run: Unity.exe -batchmode -projectPath . -executeMethod HomeVisualReview.Begin
[InitializeOnLoad]
public static class HomeVisualReview
{
    const string Key="HomeVisualReview",Folder="output/home-review";
    static readonly List<string> report=new List<string>();
    static int phase;static float next;
    // home-local (camera position, look target)
    static readonly (string name,Vector3 eye,Vector3 look)[] Views=
    {
        ("interior-entrada",new Vector3(-3.2f,2.25f,2.7f),new Vector3(-3.2f,1.2f,6.6f)),
        ("interior-cozinha",new Vector3(-1.2f,2.2f,3.0f),new Vector3(-6.5f,1.1f,5.6f)),
        ("interior-quarto",new Vector3(-6.9f,2.2f,3.0f),new Vector3(-.5f,1.0f,5.8f)),
        ("interior-porta",new Vector3(-3.6f,2.2f,6.4f),new Vector3(-3.2f,1.4f,2.4f)),
        ("fachada-porta",new Vector3(-3.0f,1.75f,-1.2f),new Vector3(-3.22f,1.6f,2.3f)),
        ("fachada",new Vector3(-1f,3.2f,-9f),new Vector3(-3f,1.6f,3f)),
        ("campainha-botao",new Vector3(-2.05f,1.45f,1.75f),new Vector3(-2.17f,1.38f,2.25f)),
        ("campainha-sino",new Vector3(-1.85f,2.35f,1.6f),new Vector3(-1.98f,2.5f,2.25f)),
        ("porta-aberta",new Vector3(-1.2f,2.0f,-.6f),new Vector3(-3.6f,1.5f,1.8f)),
        ("galinheiro-frente",new Vector3(11.0f,1.9f,-8.5f),new Vector3(12f,.7f,-3f)),
        ("galinheiro-portinhola",new Vector3(10.4f,1.6f,-6.2f),new Vector3(10.6f,.6f,-5.2f)),
        ("galinheiro-dentro",new Vector3(14.6f,1.7f,-4.9f),new Vector3(11f,.5f,-2.4f)),
        ("galinheiro-cima",new Vector3(12f,7.5f,-6f),new Vector3(12f,0f,-3f)),
    };
    static HomeVisualReview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void Begin()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/home-review-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount{introSeen=true,flock=int.Parse(Environment.GetEnvironmentVariable("HOME_REVIEW_FLOCK")??"0")});
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;report.Clear();next=Time.realtimeSinceStartup+4;Application.runInBackground=true;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try
        {
            if(phase==0){GameMenu.Instance.Resume();next=Time.realtimeSinceStartup+5;phase++;return;}
            var home=HouseholdEconomy.Instance.home;
            // hide the player so the body never blocks a view
            var player=HeistGameManager.Instance.player;foreach(var r in player.GetComponentsInChildren<Renderer>())r.forceRenderingOff=true;
            var main=Camera.main;
            if(Environment.GetEnvironmentVariable("HOME_REVIEW_DOOR_OPEN")=="1")home.GetComponentInChildren<HomeDoor>().RestoreOpen(true);
            foreach(var v in Views)
            {
                var go=new GameObject("review cam");var cam=go.AddComponent<Camera>();cam.CopyFrom(main);cam.cullingMask=main.cullingMask;
                cam.transform.position=home.TransformPoint(v.eye);cam.transform.LookAt(home.TransformPoint(v.look));cam.fieldOfView=70;cam.nearClipPlane=.05f;
                var tex=new RenderTexture(960,600,24);cam.targetTexture=tex;cam.Render();RenderTexture.active=tex;
                var png=new Texture2D(960,600,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,960,600),0,0);png.Apply();RenderTexture.active=null;
                File.WriteAllBytes(Folder+"/"+v.name+".png",png.EncodeToPNG());Object.DestroyImmediate(png);cam.targetTexture=null;tex.Release();Object.DestroyImmediate(go);
            }
            foreach(var t in home.GetComponentsInChildren<Transform>(true).Where(t=>t.parent==home))report.Add("child "+t.name+" active="+t.gameObject.activeSelf+" local="+t.localPosition);
            var coop=home.Find("Galinheiro gasto - Ultimo Recurso");
            if(coop!=null)foreach(Transform t in coop)if(!t.name.StartsWith("Tela fina") && !t.name.StartsWith("Arame"))report.Add("  coop/"+t.name+" active="+t.gameObject.activeSelf+" local="+t.localPosition);
        }
        catch(Exception e){report.Add("FAIL "+e);}
        File.WriteAllLines(Folder+"/report.txt",report);EditorApplication.isPlaying=false;
    }
}
