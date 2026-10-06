using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Play-mode checks for Elias' coop: the gate blocks when shut, opens/closes with its hinge animation, a delivery at
// the entrance opens it, walks the bird through and shuts it again, a delivery from inside the run works too,
// and the flock never leaves the run. Run: Unity.exe -batchmode -projectPath . -executeMethod HomeCoopReview.Begin
[InitializeOnLoad]
public static class HomeCoopReview
{
    const string Key="HomeCoopReview",Folder="output/home-review/coop";
    static readonly List<string> results=new List<string>();
    static int phase;static float next;static bool failed;
    static HeistGameManager game;static HomeFlockView flock;static HomeCoopGate gate;static Transform coop;static int flockBefore;
    static HomeCoopReview()
    {
        EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false) && (t==LogType.Exception || t==LogType.Error)){results.Add("ERROR "+m);failed=true;}};
    }
    public static void Begin()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/coop-review-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount{introSeen=true,flock=4});SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;failed=false;results.Clear();next=Time.realtimeSinceStartup+4;Application.runInBackground=true;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(failed?1:0);}
    }
    static void Check(bool ok,string text){results.Add((ok?"PASS ":"FAIL ")+text);if(!ok)failed=true;File.WriteAllLines(Folder+"/checks.txt",results);}
    static void Position(Vector3 p){var cc=game.player.GetComponent<CharacterController>();cc.enabled=false;game.player.position=p;cc.enabled=true;Physics.SyncTransforms();}
    static bool GapBlocked()
    {
        Physics.SyncTransforms();var c=coop.TransformPoint(new Vector3(-.95f,.7f,-2.2f));
        return Physics.OverlapBox(c,new Vector3(.3f,.4f,.08f),coop.rotation).Any(x=>x.transform.IsChildOf(gate.transform));
    }
    static IEnumerable<Transform> Birds()=>flock.GetComponentsInChildren<FarmAnimalMotion>().Where(b=>b.name.StartsWith("Galinha do sitio")).Select(b=>b.transform);
    static bool AllInside()=>Birds().All(b=>flock.InsideRun(b.position));
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try
        {
            switch(phase)
            {
                case 0:
                    GameMenu.Instance.Resume();game=HeistGameManager.Instance;flock=game.DeliveryFlock;gate=HomeCoopGate.Instance;
                    Check(flock!=null && gate!=null,"coop flock view and gate present");coop=flock.transform;
                    Check(!gate.IsOpen && GapBlocked(),"gate starts closed and blocks the gap");
                    Capture("1-fechada");gate.Toggle();next=Time.realtimeSinceStartup+.35f;phase++;return;
                case 1:
                    Capture("2-abrindo");next=Time.realtimeSinceStartup+1.2f;phase++;return;
                case 2:
                    Check(gate.FullyOpen && !GapBlocked(),"gate swings open and frees the gap");Capture("3-aberta");
                    gate.Toggle();next=Time.realtimeSinceStartup+1.6f;phase++;return;
                case 3:
                    Check(!gate.IsOpen && GapBlocked(),"gate closes again");
                    flockBefore=HouseholdEconomy.Instance.Account.flock;
                    Position(flock.DeliveryPoint+Vector3.up*.1f);game.backpack.RestoreCount(1);game.CompleteMission();
                    Check(HouseholdEconomy.Instance.Account.flock==flockBefore+1 && game.backpack.chickensCarried==0,"delivery at the entrance commits the bird");
                    next=Time.realtimeSinceStartup+.6f;phase++;return;
                case 4:
                    Check(gate.IsOpen,"delivery opens the gate for the bird");Capture("4-entrega-passando");next=Time.realtimeSinceStartup+4.2f;phase++;return;
                case 5:
                    Check(!gate.IsOpen,"gate shuts behind the delivered bird");Check(AllInside(),"delivered bird is inside the run");Capture("5-entregue");
                    flockBefore=HouseholdEconomy.Instance.Account.flock;
                    Position(coop.TransformPoint(new Vector3(1.2f,.1f,-.9f)));game.backpack.RestoreCount(1);
                    Check(game.CanDeliverHere,"delivery is offered from inside the run");game.CompleteMission();
                    Check(HouseholdEconomy.Instance.Account.flock==flockBefore+1,"delivery from inside the run commits the bird");
                    Check(!gate.IsOpen,"inside delivery keeps the gate shut");
                    Position(coop.TransformPoint(new Vector3(6,.1f,-6)));game.backpack.RestoreCount(1);
                    Check(!game.CanDeliverHere,"no delivery away from the coop");game.backpack.RestoreCount(0);
                    next=Time.realtimeSinceStartup+6;phase++;return;
                case 6:
                    Check(AllInside(),"flock wanders but stays inside the run ("+Birds().Count()+" birds)");
                    gate.Toggle();next=Time.realtimeSinceStartup+6;phase++;return;
                case 7:
                    Check(AllInside(),"flock stays inside with the gate open");gate.Toggle();
                    var bell=HomeDoorbell.Instance;Check(bell!=null,"doorbell installed at the front door");
                    if(bell!=null){bell.Ring();Check(bell.Ringing,"doorbell rings");}
                    File.WriteAllLines(Folder+"/checks.txt",results);EditorApplication.isPlaying=false;return;
            }
        }
        catch(Exception e){Check(false,e.ToString());EditorApplication.isPlaying=false;}
    }
    static void Capture(string name)
    {
        var main=Camera.main;var go=new GameObject("coop review cam");var cam=go.AddComponent<Camera>();cam.CopyFrom(main);
        cam.transform.position=coop.TransformPoint(new Vector3(-.3f,1.55f,-4.6f));cam.transform.LookAt(coop.TransformPoint(new Vector3(-.95f,.55f,-2.0f)));cam.fieldOfView=60;cam.nearClipPlane=.05f;
        foreach(var r in game.player.GetComponentsInChildren<Renderer>())r.forceRenderingOff=true;
        var tex=new RenderTexture(960,600,24);cam.targetTexture=tex;cam.Render();RenderTexture.active=tex;
        var png=new Texture2D(960,600,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,960,600),0,0);png.Apply();RenderTexture.active=null;
        File.WriteAllBytes(Folder+"/"+name+".png",png.EncodeToPNG());Object.DestroyImmediate(png);cam.targetTexture=null;tex.Release();Object.DestroyImmediate(go);
        foreach(var r in game.player.GetComponentsInChildren<Renderer>())r.forceRenderingOff=false;
    }
}
