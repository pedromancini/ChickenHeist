using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Reproduces reported play problems in the editor: E at the bed, a farmer waking up (captures around him), a punch
// seen from Elias' eyes. Output: output/playtest-probe.
// Run: Unity.exe -batchmode -projectPath . -executeMethod PlaytestProbe.Begin
[InitializeOnLoad]
public static class PlaytestProbe
{
    const string Key="PlaytestProbe",Folder="output/playtest-probe";
    static readonly List<string> lines=new List<string>();static int step;static double next,started;
    static int bedOffset;static HeistGameManager game;static FarmerStateMachine farmer;
    static PlaytestProbe()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=s=>{
            if(!SessionState.GetBool(Key,false))return;
            if(s==PlayModeStateChange.EnteredPlayMode){lines.Clear();step=0;started=EditorApplication.timeSinceStartup;next=started+4;}
            if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}
        };
        Application.logMessageReceived+=(m,st,t)=>{if(SessionState.GetBool(Key,false) && (t==LogType.Exception || t==LogType.Error))Log("LOG "+t+": "+m);};
    }
    public static void Begin()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=System.IO.Path.GetFullPath("Temp/probe-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount{introSeen=true,regionalSecurity=true});
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Log(string s){lines.Add(s);File.WriteAllLines(Folder+"/probe.txt",lines);}
    static void Wait(double s){next=EditorApplication.timeSinceStartup+s;}
    static void Position(Vector3 p,Vector3 look)
    {
        var player=game.player;var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.position=p;var d=look-p;d.y=0;if(d.sqrMagnitude>.001f)player.rotation=Quaternion.LookRotation(d);cc.enabled=true;Physics.SyncTransforms();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(EditorApplication.timeSinceStartup-started>120)throw new TimeoutException("probe");
            if(step==0)
            {
                Application.runInBackground=true;GameMenu.Instance.Resume();game=HeistGameManager.Instance;
                var bed=Object.FindAnyObjectByType<HomeNextNight>();
                Log("bed "+bed.name+" at "+bed.transform.position.ToString("F2")+" distance "+bed.distance+" colliders on it: "+string.Join(",",bed.GetComponentsInChildren<Collider>().Select(c=>c.GetType().Name+(c.enabled?"":"(off)"))));
                // stand where a player would: in front of it at arm's length, looking at it
                bedOffset=0;step=20;Wait(.1);return;
            }
            if(step==20 || step==21)
            {
                var bed=Object.FindAnyObjectByType<HomeNextNight>();var offsets=new[]{new Vector3(-1,0,0),new Vector3(0,0,1),new Vector3(0,0,-1),new Vector3(.8f,0,.8f),new Vector3(-.8f,0,-.8f)};
                if(step==21)
                {
                    var focus=WorldInteraction.Focus;float near=Vector3.Distance(bed.transform.position,game.player.position+Vector3.up);
                    Log("offset "+offsets[bedOffset]+" focus="+(focus!=null?focus.GetType().Name+" "+focus.name:"none")+" near="+near.ToString("F2")+" blocksInput="+GameMenu.BlocksInput);if(focus==bed)Log("  prompt shown and bed in reach: "+(bool)typeof(HomeNextNight).GetProperty("Near",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(bed));
                    bedOffset++;if(bedOffset>=offsets.Length){step=1;Wait(.2);return;}
                }
                var p=bed.transform.position+offsets[bedOffset]*1.1f;p.y=game.player.position.y;
                if(Physics.Raycast(p+Vector3.up*2,Vector3.down,out var hit,5,~0,QueryTriggerInteraction.Ignore))Position(hit.point+Vector3.up*.05f,bed.transform.position);
                game.player.GetComponentInChildren<Camera>().transform.LookAt(bed.transform.position);
                step=21;Wait(.15);return;
            }
            if(step==1)
            {
                // a farmer waking up, out in his yard
                Check(game.StartMission(0),"mission");farmer=Object.FindObjectsByType<FarmerStateMachine>().First(f=>game.IsMissionFarmer(f.sleepSystem));
                farmer.sleepSystem.RestoreSleep(100);Log("farmer "+farmer.name+" activity "+farmer.Activity);
                var p=farmer.transform.position;Position(p+Vector3.forward*30,p);
                step=2;Wait(6);return;
            }
            if(step==2)
            {
                Log("farmer activity "+farmer.Activity+" at "+farmer.transform.position.ToString("F1"));
                var p=farmer.transform.position;
                for(int i=0;i<4;i++){var d=Quaternion.Euler(0,i*90,0)*Vector3.forward;Shot("farmer-awake-"+i,p+d*3.2f+Vector3.up*1.6f,p+Vector3.up*1f);}
                // white things near him: every renderer within 2.5 m that is bright
                foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if(!r.enabled || !r.gameObject.activeInHierarchy || Vector3.Distance(r.bounds.center,p)>2.5f || r.transform.IsChildOf(farmer.transform) && r.name.Contains("Escopeta"))continue;
                    var m=r.sharedMaterial;var c=m!=null && m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):Color.black;
                    if(c.r+c.g+c.b>2.0f)Log("bright near farmer: "+Path(r.transform)+" "+c+" mat "+(m!=null?m.name:"none"));
                }
                foreach(var l in farmer.GetComponentsInChildren<Light>())Log("light on farmer: "+Path(l.transform)+" intensity "+l.intensity);var lantern=GameObject.Find("Lanterna da varanda");Log("lanterns under farmers: "+Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Count(l=>l.name=="Lanterna da varanda" && l.GetComponentInParent<FarmerStateMachine>()!=null));
                step=3;Wait(.2);return;
            }
            if(step==3)
            {
                // a punch from Elias' own eyes
                game.EndActiveMission();var yard=HouseholdEconomy.Instance.home.position+HouseholdEconomy.Instance.home.forward*-14;Position(yard+Vector3.up,yard+Vector3.forward*10);game.player.GetComponentInChildren<Camera>().transform.localRotation=Quaternion.identity;
                var punch=game.player.GetComponent<PlayerPunch>();Log("PlayerPunch on player: "+(punch!=null)+" instance "+(PlayerPunch.Instance!=null)+" cursor "+Cursor.lockState);
                Log("TryPunch: "+(punch!=null && punch.TryPunch()));step=4;Wait(.19);return;
            }
            if(step==4){var cam=Camera.main;Log("main camera "+cam.name+" punching "+PlayerPunch.Instance.Punching+" ext "+PlayerPunch.Instance.Extension.ToString("F2"));Grab("punch-first-person",cam);EditorApplication.isPlaying=false;step=99;}
        }
        catch(Exception e){Log("EXC "+e);EditorApplication.isPlaying=false;}
    }
    static void Check(bool ok,string what){Log((ok?"ok ":"FAILED ")+what);}
    static string Path(Transform t){var s=t.name;while(t.parent!=null){t=t.parent;s=t.name+"/"+s;}return s;}
    static void Grab(string name,Camera cam)
    {
        var rt=new RenderTexture(1280,720,24);var old=cam.targetTexture;cam.targetTexture=rt;cam.Render();var a=RenderTexture.active;RenderTexture.active=rt;
        var t=new Texture2D(1280,720,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,1280,720),0,0);t.Apply();RenderTexture.active=a;cam.targetTexture=old;
        File.WriteAllBytes(Folder+"/"+name+".png",t.EncodeToPNG());Object.DestroyImmediate(rt);Object.DestroyImmediate(t);
    }
    static void Shot(string name,Vector3 from,Vector3 at)
    {
        var go=new GameObject("probe cam");var cam=go.AddComponent<Camera>();cam.fieldOfView=50;cam.nearClipPlane=.05f;go.transform.position=from;go.transform.LookAt(at);
        Grab(name,cam);Object.DestroyImmediate(go);
    }
}
