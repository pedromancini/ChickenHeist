using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Diagnostic: plays the protagonist through idle, walk, strafe, back-pedal, run and crouch-walk with every
// runtime system active (state selection, head look, fingers, grip, ground contact). Keyboard input is replaced
// by the same PlayerMovement fields it would set; captures a 3/4 view and the player's own view.
// Run: Unity.exe -batchmode -projectPath . -executeMethod EliasRuntimeReview.Begin
[InitializeOnLoad]
public static class EliasRuntimeReview
{
    const string Key="EliasRuntimeReview",Folder="output/elias-v3/runtime";
    static readonly List<string> report=new List<string>();
    static readonly (string name,Vector2 input,bool sprint,bool crouch)[] Scenarios=
    {
        ("idle",Vector2.zero,false,false),("walk",new Vector2(0,1),false,false),("strafe-left",new Vector2(-1,0),false,false),
        ("back",new Vector2(0,-1),false,false),("run",new Vector2(0,1),true,false),("crouch-walk",new Vector2(0,1),false,true),("crouch-idle",Vector2.zero,false,true),
    };
    static int phase,scenario,frame;static float next;
    static PlayerMovement movement;static Transform player;static Camera eyes;static RuralCharacterAnimator driver;

    static EliasRuntimeReview()
    {
        EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false) && (t==LogType.Exception || t==LogType.Error))report.Add("ERROR "+m);};
    }
    public static void Begin()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/elias-runtime-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount{introSeen=true});SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;scenario=0;frame=0;report.Clear();next=Time.realtimeSinceStartup+4;Application.runInBackground=true;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}
    }
    static void SetInput(Vector2 input,bool sprint,bool crouch)
    {
        typeof(PlayerMovement).GetProperty("MoveInput").SetValue(movement,input);
        movement.estaMovendo=input!=Vector2.zero;movement.estaSprinting=sprint;
        var f=typeof(PlayerMovement).GetField("estaAgachado",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);f?.SetValue(movement,crouch);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying)return;
        try
        {
            if(phase==0 && Time.realtimeSinceStartup>=next)
            {
                GameMenu.Instance.Resume();player=HeistGameManager.Instance.player;movement=player.GetComponent<PlayerMovement>();
                eyes=player.GetComponentInChildren<Camera>();driver=player.GetComponentInChildren<RuralCharacterAnimator>();
                movement.enabled=false;
                // Out in the yard, away from walls, so the third-person camera sees the whole body.
                var home=HouseholdEconomy.Instance.home;var cc=player.GetComponent<CharacterController>();cc.enabled=false;
                player.position=home.position+home.forward*7+Vector3.up*.3f;cc.enabled=true;
                phase=1;next=Time.realtimeSinceStartup+2;return;
            }
            if(phase==2 && Time.realtimeSinceStartup>=next)
            {
                var arms=player.GetComponentInChildren<FirstPersonArms>();var rig=player.GetComponentInChildren<ProtagonistArticulation>();
                report.Add((arms!=null && arms.full.enabled && (arms.legs==null || !arms.legs.enabled)?"PASS":"FAIL")+" carrying a bird shows arms (state "+rig.ActionState+")");
                Capture("carry-0");player.GetComponent<BackpackInventory>().RestoreCount(0);
                File.WriteAllLines(Folder+"/report.txt",report);EditorApplication.isPlaying=false;return;
            }
            if(phase!=1)return;
            var s=Scenarios[scenario];
            SetInput(s.input,s.sprint,s.crouch);
            if(s.input!=Vector2.zero)
            {
                float speed=s.crouch?movement.velocidadeAgachado:s.sprint?movement.velocidadeSprint:movement.velocidadeNormal;
                player.GetComponent<CharacterController>().Move((player.right*s.input.x+player.forward*s.input.y)*speed*Time.deltaTime+Vector3.down*.05f);
            }
            if(Time.realtimeSinceStartup<next)return;
            Capture(s.name+"-"+frame);
            report.Add(s.name+" frame "+frame+": state="+driver.CurrentState+" eyes="+player.InverseTransformPoint(eyes.transform.position));
            frame++;next=Time.realtimeSinceStartup+.23f;
            if(frame==1)next=Time.realtimeSinceStartup+.23f;
            if(frame>=4)
            {
                frame=0;scenario++;next=Time.realtimeSinceStartup+1.2f;
                if(scenario>=Scenarios.Length)
                {
                    var arms=player.GetComponentInChildren<FirstPersonArms>();
                    report.Add((arms!=null && !arms.full.enabled && (arms.legs==null || !arms.legs.enabled)?"PASS":"FAIL")+" walking/idle shows no first-person body (no legs)");
                    SetInput(Vector2.zero,false,false);player.GetComponent<BackpackInventory>().RestoreCount(1);
                    phase=2;next=Time.realtimeSinceStartup+.8f;
                }
            }
        }
        catch(Exception e){report.Add("FAIL "+e);File.WriteAllLines(Folder+"/report.txt",report);EditorApplication.isPlaying=false;}
    }
    static void Capture(string name)
    {
        var body=player.GetComponentsInChildren<Animation>().First(a=>a.transform.Find("Protagonist_Rigged(Clone)")!=null);
        var layers=new Dictionary<GameObject,int>();
        foreach(var r in body.GetComponentsInChildren<Renderer>(true)){if(r.name.Contains("em primeira pessoa"))continue;layers[r.gameObject]=r.gameObject.layer;r.gameObject.layer=28;}
        var go=new GameObject("review cam");var cam=go.AddComponent<Camera>();
        cam.transform.position=player.position+player.forward*2.6f+player.right*1.8f+Vector3.up*1.5f;cam.transform.LookAt(player.position+Vector3.up*.9f);
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.18f,.2f,.23f);cam.cullingMask=1<<28;cam.fieldOfView=45;
        var lightGo=new GameObject("review light");var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.cullingMask=1<<28;lightGo.transform.rotation=Quaternion.Euler(40,-30,0);
        Save(cam,name+"-body");
        foreach(var kv in layers)kv.Key.layer=kv.Value;
        // the first-person copy alone, seen from outside
        var fp=body.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(r=>r.name=="Corpo em primeira pessoa");
        int fpLayer=fp.gameObject.layer;fp.gameObject.layer=28;Save(cam,name+"-fpbody");fp.gameObject.layer=fpLayer;
        if(frame==0)report.Add(name+" fp: enabled="+fp.enabled+" active="+fp.gameObject.activeInHierarchy+" bounds="+fp.bounds.center+" size "+fp.bounds.size+" player="+player.position+" rootBone="+(fp.rootBone!=null?fp.rootBone.name:"none")+" forceOff="+fp.forceRenderingOff);
        Object.DestroyImmediate(lightGo);Object.DestroyImmediate(go);
        var old=eyes.transform.localRotation;eyes.transform.localRotation=Quaternion.Euler(55,0,0);Save(eyes,name+"-eyes");eyes.transform.localRotation=Quaternion.Euler(82,0,0);Save(eyes,name+"-down");eyes.transform.localRotation=old;
    }
    static void Save(Camera c,string name)
    {
        var texture=new RenderTexture(480,360,24);texture.Create();var old=c.targetTexture;c.targetTexture=texture;c.Render();
        var active=RenderTexture.active;RenderTexture.active=texture;var png=new Texture2D(480,360,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,480,360),0,0);png.Apply();
        File.WriteAllBytes(Folder+"/"+name+".png",png.EncodeToPNG());Object.DestroyImmediate(png);RenderTexture.active=active;c.targetTexture=old;texture.Release();Object.DestroyImmediate(texture);
    }
}
