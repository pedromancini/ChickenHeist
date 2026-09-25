using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PhaseTwoReview
{
    const string Key="PhaseTwoReview",Folder="output/phase-two-review";
    static readonly List<string> results=new List<string>();
    static int phase,routeFarm;static float next,deadline;
    static HeistGameManager game;static Camera eye;static SecurityCamera unit;static TrapSystem wire;
    static GameCheckpointData baseline;static bool passed;static Vector3 home;
    static GameObject blockedDestination;
    public static void Visible()
    {
        var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();Begin();
    }
    static PhaseTwoReview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;
        Application.logMessageReceived+=(message,stack,type)=>{if(SessionState.GetBool(Key,false) && (type==LogType.Exception || type==LogType.Error))
            {Check(false,message);passed=false;EditorApplication.isPlaying=false;}};}
    public static void Begin()
    {
        Directory.CreateDirectory(Folder);
        SessionState.SetBool(Key+".GUI",false);
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var account=Object.FindAnyObjectByType<HouseholdEconomy>();
        account.editorTestSavePath=Path.GetFullPath("Temp/phase-two-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(account.editorTestSavePath,new HouseholdAccount());
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){results.Clear();phase=0;next=Time.realtimeSinceStartup+3;passed=false;}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
            EditorApplication.Exit(passed?0:1);
        }
    }
    static void Check(bool value,string message){results.Add((value?"PASS ":"FAIL ")+message);File.WriteAllLines(Folder+"/checks.txt",results);}
    static void Position(Vector3 p)
    {var cc=game.player.GetComponent<CharacterController>();cc.enabled=false;game.player.position=p;Physics.SyncTransforms();}
    static void Capture(string name,Vector3 position,Vector3 target)
    {
        var go=new GameObject("Review camera");var camera=go.AddComponent<Camera>();camera.CopyFrom(eye);camera.transform.position=position;camera.transform.LookAt(target);
        camera.cullingMask=~0;camera.fieldOfView=48;camera.nearClipPlane=.03f;
        var rt=new RenderTexture(1600,1000,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;
        var texture=new Texture2D(1600,1000,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,1000),0,0);texture.Apply();
        File.WriteAllBytes(Folder+"/"+name+".png",texture.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;
        Object.DestroyImmediate(texture);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try
        {
            if(phase==0)
            {
                Application.runInBackground=true;
                game=HeistGameManager.Instance;GameMenu.Instance.Resume();eye=game.player.GetComponentInChildren<Camera>();home=game.player.position;
                game.player.GetComponent<PlayerMovement>().enabled=false;eye.GetComponent<PlayerLook>().enabled=false;
                foreach(var door in Object.FindObjectsByType<HomeDoor>())door.RestoreOpen(true);
                Physics.SyncTransforms();
                Check(Object.FindObjectsByType<FarmLayoutInfo>().All(f=>f.GetComponentsInChildren<SecurityCamera>().Length>0 && f.GetComponentsInChildren<TrapSystem>().Length>0),"Every farm has authored cameras and wires");
                Check(!FarmSecurityProgression.Installed,"Phase one starts without security");
                var account=HouseholdEconomy.Instance.Account;account.RestUntilMorning();Check(!account.regionalSecurity,"Sleeping without crime does not install security");
                account.RegisterRaid(ProtagonistPhone.Instance.farmNames[0],1,false);Check(!account.regionalSecurity,"Raid alone does not install security");
                account.RestUntilMorning();account.pendingVision=false;FarmSecurityProgression.Apply();Check(FarmSecurityProgression.Installed,"Raid then sleep installs phase two");
                Check(game.StartMission(0),"Tablet mission action starts mission");
                unit=Object.FindObjectsByType<SecurityCamera>().First(c=>game.IsMissionTarget(c));wire=Object.FindObjectsByType<TrapSystem>().First(c=>game.IsMissionTarget(c));
                baseline=Object.FindAnyObjectByType<GameCheckpoint>().Capture();
                deadline=Time.realtimeSinceStartup+90;
            }
            else if(phase==1)
            {
                if(MissionNavigation.Instance.Status=="Calculando caminho..." && Time.realtimeSinceStartup<deadline)return;
                if(!Application.isBatchMode && !SessionState.GetBool(Key+".GUI",false))
                {ScreenCapture.CaptureScreenshot(Folder+"/mission-minimap.png");SessionState.SetBool(Key+".GUI",true);next=Time.realtimeSinceStartup+2;return;}
                Check(MissionNavigation.Instance.Route.Count>1,"Home to farm route: "+MissionNavigation.Instance.Status);
                File.WriteAllText(Folder+"/route-diagnostic.txt",MissionNavigation.Instance.Diagnostic);
                var navigation=MissionNavigation.Instance;
                if(navigation.Route.Count>2){var routeBefore=navigation.Route.ToArray();Position(Vector3.Lerp(routeBefore[0],routeBefore[1],.5f));navigation.SendMessage("Update");Check(navigation.Route.SequenceEqual(routeBefore),"Following the route preserves its path instead of clearing it");Position(home);}
                var gate=Object.FindObjectsByType<RuralGate>().First();gate.RestoreOpen(false);var center=gate.GetComponent<HingedBarrier>().InteractionPoint;
                Capture("gate-closed",center+new Vector3(4,3,6),center);gate.RestoreOpen(true);Capture("gate-open",center+new Vector3(4,3,6),center);gate.RestoreOpen(false);
                Check(MissionNavigation.Instance.Route.Zip(MissionNavigation.Instance.Route.Skip(1),(a,b)=>MissionNavigation.Instance.ClearPassage(a,b,false)).All(v=>v),"Every walking route segment has collision clearance");
                var light=new GameObject("Review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.8f;light.transform.rotation=Quaternion.Euler(40,-25,0);
                Capture("camera-close",unit.Eye+unit.scanHead.forward*.9f+unit.scanHead.right*.5f+Vector3.up*.16f,unit.transform.position);
                Capture("camera-field",unit.transform.position+Vector3.up*8+Vector3.back*8,unit.transform.position+Vector3.forward*4);
                Capture("tripwire",wire.transform.position+new Vector3(1.8f,1.1f,-2),wire.transform.position+Vector3.up*.22f);
                // Deterministic visibility fixture above the scene; real physics wall.
                unit.rotationArc=0;unit.transform.position=new Vector3(0,503.4f,0);unit.scanHead.rotation=Quaternion.identity;
                Position(new Vector3(0,500,6));Physics.SyncTransforms();
                Check(unit.CanSeePoint(new Vector3(0,501,6)),"Unobstructed target visible");
                Check(!unit.CanSeePoint(new Vector3(8,501,0)),"Outside field angle rejected");
                Check(!unit.CanSeePoint(new Vector3(0,501,20)),"Outside range rejected");
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,502,3);wall.transform.localScale=new Vector3(6,5,.4f);Physics.SyncTransforms();
                Check(!unit.CanSeePoint(new Vector3(0,501,6)),"Wall blocks actual camera detection");
                Check(unit.ClearRange(Vector3.forward,501)<3.3f,"Field footprint stops at wall");Object.DestroyImmediate(wall);
                unit.RestorePaint(30);Check(!unit.Operational,"Spray disables detection");
                Check(wire.TryTrigger(game.player.GetComponent<PlayerMovement>()),"Wire emits noise and slows player once");
                Check(!wire.TryTrigger(game.player.GetComponent<PlayerMovement>()),"Triggered wire cannot fire twice");
                var move=game.player.GetComponent<PlayerMovement>();move.estaAgachado=true;
                Check(Mathf.Abs(move.CurrentMoveSpeed-move.velocidadeAgachado*.35f)<.001f,"Trap slowdown composes with crouch");move.estaAgachado=false;
                var checkpoint=Object.FindAnyObjectByType<GameCheckpoint>();var data=checkpoint.Capture();wire.triggered=false;unit.RestorePaint(0);move.RestoreTrapSlow(0);
                Check(checkpoint.Restore(data,out var message),"Checkpoint round trip: "+message);
                Check(wire.triggered && unit.PaintSecondsRemaining>25 && move.TrapSlowRemaining>0,"Checkpoint restores wire, paint and slowdown");
                data.traps=null;data.trapSlowRemaining=0;
                Check(checkpoint.Restore(data,out message) && !wire.triggered,"Legacy save without traps migrates safely");
                checkpoint.Restore(baseline,out _);game.EndActiveMission();Check(MissionNavigation.Instance.Route.Count==0,"Ending mission clears route");
                Position(home);routeFarm=0;phase=2;next=Time.realtimeSinceStartup+.2f;return;
            }
            else if(phase==2)
            {
                if(routeFarm>=ProtagonistPhone.Instance.farmNames.Length){phase=4;return;}
                game.EndActiveMission();game.StartMission(routeFarm);deadline=Time.realtimeSinceStartup+90;phase=3;return;
            }
            else if(phase==3)
            {
                var nav=MissionNavigation.Instance;
                if(nav.Status=="Calculando caminho..." && Time.realtimeSinceStartup<deadline)return;
                Check(nav.Route.Count>1,"Farm "+routeFarm+" route from home: "+nav.Status);
                routeFarm++;phase=2;return;
            }
            else if(phase==4)
            {
                // Width-sensitive passage fixture, independent of scene geometry.
                var nav=MissionNavigation.Instance;
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(.9f,502,4);wall.transform.localScale=new Vector3(.2f,4,5);Physics.SyncTransforms();
                Check(nav.ClearPassage(new Vector3(0,500,0),new Vector3(0,500,8),false),"Walking clearance fits narrow lane");
                Check(!nav.ClearPassage(new Vector3(0,500,0),new Vector3(0,500,8),true),"Car route rejects lane too narrow for truck");
                Object.DestroyImmediate(wall);
                nav.enabled=false;
                var road=Object.FindObjectsByType<RuralRoadSpan>().Where(r=>Vector3.Distance(r.start,r.end)>100).OrderBy(r=>Vector3.Distance(home,r.start)).First();
                game.EndActiveMission();game.StartMission(0);nav.Clear();nav.enabled=false;
                var farm=Object.FindObjectsByType<FarmLayoutInfo>().First(f=>f.identity==game.MissionName);
                nav.StartCoroutine(nav.CalculateRoute(road.start,farm.entrance+Vector3.back*3,true));deadline=Time.realtimeSinceStartup+90;
            }
            else if(phase==5)
            {
                var nav=MissionNavigation.Instance;if(nav.Status=="Calculando caminho..." && Time.realtimeSinceStartup<deadline)return;
                Check(nav.Route.Count>1,"Driving route from home access: "+nav.Status);
                Check(nav.Route.Count>1 && nav.Route.Zip(nav.Route.Skip(1),(a,b)=>nav.ClearPassage(a,b,true)).All(v=>v),"Every driving segment clears truck width");
                File.WriteAllText(Folder+"/vehicle-route-diagnostic.txt",nav.Diagnostic);
                var farm=Object.FindObjectsByType<FarmLayoutInfo>().First(f=>f.identity==game.MissionName);var end=farm.entrance+Vector3.back*3;
                blockedDestination=GameObject.CreatePrimitive(PrimitiveType.Cube);blockedDestination.transform.position=end+Vector3.up;blockedDestination.transform.localScale=new Vector3(5,4,5);Physics.SyncTransforms();
                nav.StartCoroutine(nav.CalculateRoute(end+Vector3.back*15,end,false));deadline=Time.realtimeSinceStartup+90;
            }
            else if(phase==6)
            {
                var nav=MissionNavigation.Instance;if(nav.Status=="Calculando caminho..." && Time.realtimeSinceStartup<deadline)return;
                Check(nav.Route.Count==0 && nav.Status.StartsWith("Sem"),"Blocked destination reports no path and draws no false route");
                Object.DestroyImmediate(blockedDestination);
                passed=!results.Any(s=>s.StartsWith("FAIL"));File.WriteAllLines(Folder+"/checks.txt",results);EditorApplication.isPlaying=false;return;
            }
            phase++;next=Time.realtimeSinceStartup+1;
        }
        catch(Exception e){Check(false,e.ToString());passed=false;EditorApplication.isPlaying=false;}
    }
}
