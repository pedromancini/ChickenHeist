using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class TabletBodyReview
{
    const string Key="TabletBodyReview",Folder="output/tablet-body-review";
    static int phase;static float next;static readonly List<string> results=new List<string>();
    static HeistGameManager game;static Camera camera;static PlayerMovement movement;
    static bool passed;static GameObject ground;
    static TabletBodyReview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void Visible()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        view.Show();view.Focus();Begin();
    }
    public static void Begin()
    {
        if(!Application.isBatchMode){var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();}
        Directory.CreateDirectory(Folder);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/tablet-body-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount());
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;results.Clear();next=Time.realtimeSinceStartup+3;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);EditorApplication.delayCall+=()=>EditorApplication.Exit(passed?0:1);}
    }
    static void Check(bool ok,string label){results.Add((ok?"PASS ":"FAIL ")+label);}
    static void Capture(string name)
    {
        var rt=new RenderTexture(1280,800,24);rt.Create();var target=camera.targetTexture;var active=RenderTexture.active;
        camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var png=new Texture2D(1280,800,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1280,800),0,0);png.Apply();File.WriteAllBytes(Folder+"/"+name+".png",png.EncodeToPNG());
        camera.targetTexture=target;RenderTexture.active=active;Object.DestroyImmediate(png);rt.Release();Object.DestroyImmediate(rt);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try
        {
            if(phase==0)
            {
                game=HeistGameManager.Instance;GameMenu.Instance.Resume();camera=game.player.GetComponentInChildren<Camera>();movement=game.player.GetComponent<PlayerMovement>();
                movement.enabled=false;camera.GetComponent<PlayerLook>().enabled=false;
                game.player.GetComponent<CharacterController>().enabled=false;game.player.position=new Vector3(0,500,0);game.player.rotation=Quaternion.identity;
                ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.transform.position=new Vector3(0,499.98f,0);ground.transform.localScale=Vector3.one*5;
                var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=new Color(.34f,.37f,.39f);ground.GetComponent<Renderer>().material=mat;
                var go=new GameObject("Review light");var light=go.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;go.transform.rotation=Quaternion.Euler(40,-30,0);
                RenderSettings.fog=false;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.23f,.27f);
                movement.RestorePosture(false);camera.transform.localRotation=Quaternion.Euler(80,0,0);
                Check(game.player.GetComponentInChildren<RuralCharacterAnimator>().transform.Find("Camisa fechada - interior")!=null,"closed shirt installed");
            }
            else if(phase==1)
            {
                Capture("standing-down");
                var body=game.player.GetComponentInChildren<RuralCharacterAnimator>();
                var geometry=new List<string>{"camera "+camera.transform.position+" root "+body.transform.position+" scale "+body.transform.lossyScale};
                foreach(var r in game.player.GetComponentsInChildren<Renderer>())
                {
                    geometry.Add(r.name+" enabled="+r.enabled+" layer="+r.gameObject.layer+" bounds="+r.bounds+" scale="+r.transform.lossyScale);
                    if(r is SkinnedMeshRenderer sk){geometry.Add("source "+sk.sharedMesh.bounds);var baked=new Mesh();sk.BakeMesh(baked);geometry.Add("baked "+baked.bounds);Object.DestroyImmediate(baked);}
                }
                File.WriteAllLines(Folder+"/geometry.txt",geometry);
                var liner=body.transform.Find("Camisa fechada - interior").gameObject;
                liner.SetActive(false);Capture("standing-without-liner");liner.SetActive(true);
                movement.RestorePosture(true);camera.transform.localRotation=Quaternion.Euler(80,0,0);
            }
            else if(phase==2)
            {
                Capture("crouching-down");movement.RestorePosture(false);
                camera.transform.localRotation=Quaternion.Euler(45,0,0);game.backpack.RestoreCount(1);
                var bird=Object.FindAnyObjectByType<InteractableChicken>();game.player.GetComponent<PlayerChickenCarry>().Lift(bird);
            }
            else if(phase==3)
            {
                var carry=game.player.GetComponent<PlayerChickenCarry>();
                Check(carry.HasVisual && !carry.IsLifting,"bird carried after pickup");Check(carry.WristBend<10,"neutral wrist angle "+carry.WristBend);Check(carry.HandError<.12f,"carry contact "+carry.HandError);
                Capture("carry-down");camera.transform.localRotation=Quaternion.Euler(15,0,0);
            }
            else if(phase==4)
            {
                Capture("carry-forward");ProtagonistPhone.Instance.SetOpen(true);ProtagonistPhone.Instance.ReviewTab=2;
                Check(game.player.GetComponentInChildren<HandheldPhone>().ScreenReady,"tablet immediately ready");
                foreach(var size in new[]{new Vector2(1920,1080),new Vector2(1280,720),new Vector2(1024,768),new Vector2(2560,1080)})
                {var r=ProtagonistPhone.TabletRect(size.x,size.y);Check(r.width>r.height && r.x>=0 && r.y>=0 && r.xMax<=size.x && r.yMax<=size.y,"tablet layout "+size);}
            }
            else if(phase==5)
            {
                var phone=game.player.GetComponentInChildren<HandheldPhone>();Check(!phone.IsBusy && !phone.handset.gameObject.activeSelf,"no held device or draw animation");
                Check(!game.player.GetComponent<PlayerChickenCarry>().GetComponentsInChildren<Transform>().Any(t=>t.name=="Galinha no colo - visual" && t.gameObject.activeSelf),"bird hidden while using tablet");
                ScreenCapture.CaptureScreenshot(Folder+"/tablet.png");
            }
            else if(phase==6){ProtagonistPhone.Instance.SetOpen(false);}
            else if(phase==7)
            {
                Check(game.player.GetComponent<PlayerChickenCarry>().HasVisual,"carried bird retained after tablet closes");Capture("carry-restored");
                passed=!results.Any(s=>s.StartsWith("FAIL"));File.WriteAllLines(Folder+"/checks.txt",results);EditorApplication.isPlaying=false;return;
            }
            phase++;next=Time.realtimeSinceStartup+1.6f;
        }
        catch(Exception e){results.Add("FAIL "+e);File.WriteAllLines(Folder+"/checks.txt",results);passed=false;EditorApplication.isPlaying=false;}
    }
}
