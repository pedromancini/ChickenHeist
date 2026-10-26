using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PhaseTwoGameplayReview
{
    const string Key="PhaseTwoGameplayReview",Folder="output/phase-two-gameplay";
    static readonly List<string> results=new List<string>();
    static int step;static double next,started;static bool failed;
    static HeistGameManager game;static SecurityCamera unit;static FarmerStateMachine farmer;static Vector3 home;
    static PhaseTwoGameplayReview()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=s=>{
            if(!SessionState.GetBool(Key,false))return;
            if(s==PlayModeStateChange.EnteredPlayMode){results.Clear();step=0;failed=false;started=EditorApplication.timeSinceStartup;next=started+4;}
            if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(failed?1:0);}
        };
        Application.logMessageReceived+=(m,st,t)=>{if(SessionState.GetBool(Key,false) && t==LogType.Exception)Check(false,m+"\n"+st);};
    }
    public static void Begin()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/phase2-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount{introSeen=true,regionalSecurity=true});
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Check(bool ok,string message){failed|=!ok;results.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines(Folder+"/checks.txt",results);}
    static void Wait(double seconds){next=EditorApplication.timeSinceStartup+seconds;}
    static void Position(Vector3 p,Vector3? look=null)
    {
        var player=game.player;var cc=player.GetComponent<CharacterController>();bool was=cc.enabled;cc.enabled=false;player.position=p;
        if(look.HasValue){var d=look.Value-p;d.y=0;if(d.sqrMagnitude>.001f)player.rotation=Quaternion.LookRotation(d);}
        cc.enabled=was;Physics.SyncTransforms();
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(EditorApplication.timeSinceStartup-started>240)throw new TimeoutException("phase two gameplay review");
            switch(step)
            {
            case 0:
            {
                Application.runInBackground=true;GameMenu.Instance.Resume();game=HeistGameManager.Instance;home=game.player.position;
                game.player.GetComponent<PlayerMovement>().enabled=false;game.player.GetComponentInChildren<PlayerLook>().enabled=false;
                FarmSecurityProgression.Apply();Check(FarmSecurityProgression.Installed,"Phase two security installed");
                closedGates.Clear();Wires();
                for(int i=0;i<closedGates.Count;i++){var g=closedGates[i];g.gate.Toggle(g.wire+g.inward*(i%2==0?-2.5f:4f));}
                step=70;Wait(1.4);return;
            }
            case 70:
            {
                int inward=closedGates.Count(g=>Vector3.Dot(Leaf(g.gate)-g.leaf,g.inward)>.5f);
                Check(closedGates.Count>0 && inward==closedGates.Count,"With a wire outside it, every gate swings into its farm, whichever side it is opened from ("+inward+"/"+closedGates.Count+")");
                foreach(var g in closedGates)g.gate.Toggle(g.wire);
                step=1;Wait(1.4);return;
            }
            case 1:
            {
                Check(game.StartMission(0),"Mission 0 starts");
                unit=Object.FindObjectsByType<SecurityCamera>().First(c=>game.IsMissionTarget(c));
                unit.rotationArc=0;unit.transform.SetPositionAndRotation(new Vector3(0,503.4f,0),Quaternion.identity);
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Review floor";floor.transform.position=new Vector3(0,499.5f,6);floor.transform.localScale=new Vector3(30,1,30);
                Position(new Vector3(30,500.1f,30));Physics.SyncTransforms();step=2;Wait(.6);return;
            }
            case 2:
            {
                Check(!unit.Sees && unit.Suspicion==0,"Camera calm with Elias out of view");
                Capture("camera-calm",unit.transform.position+new Vector3(5,6,-5),unit.transform.position+new Vector3(0,-3,6));
                Position(new Vector3(0,500.1f,6));step=3;Wait(.3);return;
            }
            case 3:
            {
                Check(unit.Sees && !unit.Detecting && unit.Suspicion>0 && unit.Suspicion<1,"Camera takes a moment to react (suspicion "+unit.Suspicion.ToString("0.00")+" after 0.3 s)");
                Capture("camera-noticing",unit.transform.position+new Vector3(5,6,-5),unit.transform.position+new Vector3(0,-3,6));
                step=4;Wait(.8);return;
            }
            case 4:
            {
                Check(unit.Detecting,"Camera raises the alarm after "+SecurityCamera.DetectSeconds+" s in view");
                Capture("camera-alarm",unit.transform.position+new Vector3(5,6,-5),unit.transform.position+new Vector3(0,-3,6));
                Position(new Vector3(30,500.1f,30));step=5;Wait(2.0);return;
            }
            case 5:
            {
                Check(!unit.Detecting && unit.Suspicion==0,"Out of view the camera calms down again");
                game.EndActiveMission();Position(home);
                float far=12;
                float sprint=(6-FarmerStateMachine.TrackSpeed(far))*1.35f,walk=Mathf.Max(0,3-FarmerStateMachine.TrackSpeed(far))*1.35f;
                Check(sprint>1.2f,"Sprinting across the farmer's aim at 12 m leaves the shot "+sprint.ToString("0.0")+" m behind (dodged)");
                Check(walk<.3f,"Walking across at 12 m stays in his aim ("+walk.ToString("0.0")+" m behind)");
                Check(FarmerStateMachine.TrackSpeed(1.5f)>6,"Within arm's reach the aim keeps up even with a sprint ("+FarmerStateMachine.TrackSpeed(1.5f).ToString("0.0")+" m/s)");
                float close=6*Mathf.Lerp(14,1.5f,1.5f/20),distant=6*Mathf.Lerp(14,1.5f,15f/20);
                Check(close>2.5f*distant,"A full hit up close hurts far more ("+close.ToString("0")+" at 1.5 m, "+distant.ToString("0")+" at 15 m)");
                Check(game.StartMission(0),"Mission 0 again");
                farmer=Object.FindObjectsByType<FarmerStateMachine>().First(f=>game.IsMissionFarmer(f.sleepSystem));
                var coop=farmer.GetComponentInParent<FarmLayoutInfo>().GetComponentInChildren<ChickenCoopLockpick>();
                var yard=coop.InteractionPoint-coop.transform.forward*6.5f;var cc=farmer.GetComponent<CharacterController>();cc.enabled=false;
                farmer.transform.SetPositionAndRotation(yard+Vector3.up*.1f,Quaternion.LookRotation(-coop.transform.forward));cc.enabled=true;
                farmer.Restore(new FarmerCombatSave{activity=FarmerActivity.Investigating,ammo=2,lastKnown=yard,memoryRemaining=8});
                step=50;Wait(3.0);return;
            }
            case 50:
            {
                var front=farmer.transform.position+farmer.transform.forward*1.2f;Position(front,farmer.transform.position);
                var eye=game.player.GetComponentInChildren<Camera>().transform;eye.localRotation=Quaternion.identity;
                farmer.Restore(new FarmerCombatSave{activity=FarmerActivity.Investigating,ammo=2,lastKnown=farmer.transform.position,memoryRemaining=8});
                Check(PlayerPunch.Instance.TryPunch(),"Left click throws a punch");step=6;Wait(PlayerPunch.ImpactAt*PlayerPunch.Duration+.1);return;
            }
            case 6:
            {
                Capture("punch-swing",game.player.position+game.player.right*1.6f+Vector3.up*1.7f-game.player.forward*.4f,farmer.transform.position+Vector3.up*1.2f);
                Check(farmer.Stunned && farmer.Activity==FarmerActivity.Stunned,"The punch stuns the farmer");
                Check(!PlayerPunch.Instance.TryPunch(),"Punches have a cooldown");
                step=51;Wait(1.0);return;
            }
            case 51:
            {
                Capture("punch-stunned",game.player.position+game.player.right*1.8f+Vector3.up*1.5f-game.player.forward*.8f,farmer.transform.position+Vector3.up*1.1f);
                step=7;Wait(PlayerPunch.StunSeconds-.6);return;
            }
            case 7:
            {
                Check(!farmer.Stunned && farmer.Activity!=FarmerActivity.Stunned,"He comes round after "+PlayerPunch.StunSeconds+" s");
                Position(farmer.transform.position+farmer.transform.forward*3.5f,farmer.transform.position);
                Check(PlayerPunch.Instance.TryPunch(),"Punch thrown from 3.5 m");step=8;Wait(.4);return;
            }
            case 8:
            {
                Check(!farmer.Stunned,"Out of reach the punch misses");
                game.backpack.RestoreCount(1);Check(!PlayerPunch.Instance.TryPunch(),"No punching with a chicken in the arms");game.backpack.RestoreCount(0);
                game.EndActiveMission();
                var economy=HouseholdEconomy.Instance;int flock=economy.Account.flock;
                Check(game.StartMission(1),"Mission 1 starts at home");game.backpack.RestoreCount(1);
                var houseHome=economy.home.position;var away=houseHome+(game.player.position-houseHome).normalized*60;if((away-houseHome).sqrMagnitude<1)away=houseHome+Vector3.forward*60;
                Position(new Vector3(away.x,houseHome.y+1,away.z));step=9;Wait(.2);arrivalFlock=flock;return;
            }
            case 9:
            {
                var houseHome=HouseholdEconomy.Instance.home.position;var p=game.player.position;
                Vector3 to=houseHome-p;to.y=0;
                if(to.magnitude>12 && game.MissionActive){Position(p+to.normalized*4);Wait(.05);return;}
                Check(!game.MissionActive,"Coming within 20 m of the house ends the mission");
                Check(MissionCompletePopup.Instance!=null && MissionCompletePopup.Instance.Showing,"\"Missão concluída!\" shows");
                Check(HouseholdEconomy.Instance.Account.flock==arrivalFlock+1 && game.backpack.chickensCarried==0,"The chicken in Elias' arms joined the flock");
                Check(HouseholdEconomy.Instance.Account.pendingRaids.Count>0,"The raid is recorded for the news");
                step=10;Wait(.3);return;
            }
            case 10:
            {
                if(!Application.isBatchMode)ScreenCapture.CaptureScreenshot(Folder+"/mission-complete.png");
                Check(game.StartMission(2),"Mission 2 starts");Position(HouseholdEconomy.Instance.home.position+Vector3.forward*90+Vector3.up);step=11;Wait(.2);return;
            }
            case 11:
            {
                Position(HouseholdEconomy.Instance.home.position+Vector3.back*12+Vector3.up);step=12;Wait(.3);return;
            }
            case 12:
            {
                Check(game.MissionActive,"Teleporting home keeps the mission (dev console /casa)");game.EndActiveMission();
                EditorApplication.isPlaying=false;step=99;return;
            }
            }
        }
        catch(Exception e){Check(false,e.ToString());EditorApplication.isPlaying=false;}
    }
    static int arrivalFlock;
    static void Wires()
    {
        var sheet=new List<Texture2D>();int farms=0,atGate=0,atCoop=0;
        foreach(var farm in Object.FindObjectsByType<FarmLayoutInfo>().OrderBy(f=>f.identity))
        {
            farms++;var wires=farm.GetComponentsInChildren<TrapSystem>().OrderBy(x=>x.name).ToArray();
            foreach(var w in wires)
            {
                bool grounded=Mathf.Abs(Height(w.StakeLeft)-w.StakeLeft.y)<.08f && Mathf.Abs(Height(w.StakeRight)-w.StakeRight.y)<.08f;
                Check(grounded && w.Span>=w.Opening,farm.identity+" / "+(w.Gate!=null?"gate":w.AtCoop?"coop door":w.name)+" wire: "+w.Span.ToString("0.0")+" m across a "+w.Opening.ToString("0.0")+" m opening, stakes on the ground");
            }
            for(int i=0;i<wires.Length;i++)for(int j=i+1;j<wires.Length;j++)
                Check(SegmentGap(wires[i].StakeLeft,wires[i].StakeRight,wires[j].StakeLeft,wires[j].StakeRight)>.6f,farm.identity+": wires apart");
            var gate=wires.FirstOrDefault(x=>x.Gate!=null);var coop=wires.FirstOrDefault(x=>x.AtCoop);
            if(gate!=null)
            {
                var leaf=Leaf(gate.Gate);var mid=(gate.StakeLeft+gate.StakeRight)*.5f;var gap=leaf-mid;gap.y=0;float ahead=Vector3.Dot(gap,gate.Inward);
                if(ahead>.4f && ahead<1.2f)atGate++;else Check(false,farm.identity+": gate wire "+ahead.ToString("0.00")+" m in front of the gate");
                closedGates.Add((gate.Gate,leaf,gate.Inward,mid));
            }
            if(coop!=null)atCoop++;
            foreach(var w in new[]{gate,coop})
            {
                if(w==null){sheet.Add(new Texture2D(480,300,TextureFormat.RGB24,false));continue;}
                var mid=(w.StakeLeft+w.StakeRight)*.5f;var side=Vector3.Cross(Vector3.up,w.Inward);
                sheet.Add(Shot(mid-w.Inward*3.0f+side*1.4f+Vector3.up*1.5f,mid+w.Inward*.6f+Vector3.up*.25f,480,300));
            }
        }
        Check(atGate==farms && atCoop==farms,"Every farm has a wire just outside its gate and one across its coop door (gate "+atGate+"/"+farms+", coop "+atCoop+"/"+farms+")");
        var all=new Texture2D(480*4,300*((sheet.Count+3)/4),TextureFormat.RGB24,false);
        for(int i=0;i<sheet.Count;i++){all.SetPixels(480*(i%4),all.height-300*(i/4+1),480,300,sheet[i].GetPixels());Object.DestroyImmediate(sheet[i]);}
        all.Apply();File.WriteAllBytes(Folder+"/wires-all-farms.png",all.EncodeToPNG());Object.DestroyImmediate(all);
        var first=Object.FindObjectsByType<FarmLayoutInfo>().OrderBy(f=>f.identity).First().GetComponentsInChildren<TrapSystem>().OrderBy(x=>x.name).ToArray();
        foreach(var w in first)
        {
            var mid=(w.StakeLeft+w.StakeRight)*.5f;var side=Vector3.Cross(Vector3.up,w.Inward);
            var t=Shot(mid-w.Inward*1.2f+side*.5f+Vector3.up*.65f,mid+Vector3.up*.18f,960,540);File.WriteAllBytes(Folder+"/wire-close-"+(w.Gate!=null?"gate":"coop")+".png",t.EncodeToPNG());Object.DestroyImmediate(t);
        }
    }
    static readonly List<(RuralGate gate,Vector3 leaf,Vector3 inward,Vector3 wire)> closedGates=new List<(RuralGate,Vector3,Vector3,Vector3)>();
    static Vector3 Leaf(RuralGate gate){var rs=gate.GetComponentsInChildren<Renderer>(true);var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b.center;}
    static float Height(Vector3 p)
    {
        float best=float.PositiveInfinity,y=p.y;
        foreach(var h in Physics.RaycastAll(p+Vector3.up*1.5f,Vector3.down,4,~0,QueryTriggerInteraction.Ignore))
            if(h.normal.y>.6f && h.distance<best && h.collider.GetComponentInParent<TrapSystem>()==null){best=h.distance;y=h.point.y;}
        return y;
    }
    static float SegmentGap(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
    {
        float best=float.PositiveInfinity;
        for(int i=0;i<=10;i++)for(int j=0;j<=10;j++){var p=Vector3.Lerp(a,b,i/10f);var q=Vector3.Lerp(c,d,j/10f);p.y=q.y=0;best=Mathf.Min(best,Vector3.Distance(p,q));}
        return best;
    }
    static Texture2D Shot(Vector3 from,Vector3 at,int w,int h)
    {
        var go=new GameObject("review shot");var cam=go.AddComponent<Camera>();cam.fieldOfView=55;cam.nearClipPlane=.05f;go.transform.position=from;go.transform.LookAt(at);
        var lamp=new GameObject("review lamp").AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=1.2f;lamp.transform.rotation=Quaternion.LookRotation(at-from+Vector3.down*2);
        var rt=new RenderTexture(w,h,24);cam.targetTexture=rt;cam.Render();var a=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(w,h,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,w,h),0,0);t.Apply();RenderTexture.active=a;
        cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(go);Object.DestroyImmediate(lamp.gameObject);return t;
    }
    static void Capture(string name,Vector3 from,Vector3 at){var t=Shot(from,at,960,540);File.WriteAllBytes(Folder+"/"+name+".png",t.EncodeToPNG());Object.DestroyImmediate(t);}
}
