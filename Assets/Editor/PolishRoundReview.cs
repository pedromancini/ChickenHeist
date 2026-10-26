using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PolishRoundReview
{
    const string Key="PolishRoundReview",Folder="output/polish-round";
    static readonly List<string> results=new List<string>();
    static int step,farm;static double next,started;static bool failed;
    static TrapSystem gateWire,coopWire;static int trial,coopLevel;static readonly List<(int parts,int torn)> coopParts=new List<(int,int)>();
    static Vector3 Middle(TrapSystem w)=>(w.StakeLeft+w.StakeRight)*.5f;
    static HeistGameManager game;static int paintable,tested;static SecurityCamera sprayed;static int punchFrame;static OldPickupTruck truck;static Quaternion truckRotation;static Vector3 truckPosition;
    static PolishRoundReview()
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
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=System.IO.Path.GetFullPath("Temp/polish-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount{introSeen=true,regionalSecurity=true,paintUses=99});
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Check(bool ok,string message){failed|=!ok;results.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines(Folder+"/checks.txt",results);}
    static void Note(string message){results.Add("     "+message);File.WriteAllLines(Folder+"/checks.txt",results);}
    static void Wait(double seconds){next=EditorApplication.timeSinceStartup+seconds;}
    static void Position(Vector3 p,Vector3 look)
    {
        var player=game.player;var cc=player.GetComponent<CharacterController>();bool was=cc.enabled;cc.enabled=false;player.position=p;
        var d=look-p;d.y=0;if(d.sqrMagnitude>.001f)player.rotation=Quaternion.LookRotation(d);cc.enabled=was;Physics.SyncTransforms();
    }
    static Vector3 Ground(Vector3 p,float below)
    {
        float best=float.PositiveInfinity;Vector3 found=p;
        foreach(var hit in Physics.RaycastAll(p,Vector3.down,below,~0,QueryTriggerInteraction.Ignore))
            if(hit.normal.y>.6f && hit.distance<best && !hit.transform.IsChildOf(game.player)){best=hit.distance;found=hit.point;}
        return found;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(EditorApplication.timeSinceStartup-started>300)throw new TimeoutException("polish round review");
            switch(step)
            {
            case 0:
                Application.runInBackground=true;GameMenu.Instance.Resume();game=HeistGameManager.Instance;
                game.player.GetComponent<PlayerMovement>().enabled=false;game.player.GetComponentInChildren<PlayerLook>().enabled=false;
                FarmSecurityProgression.Apply();farm=0;paintable=tested=0;step=1;Wait(.3);return;
            case 1:
                if(farm>=ProtagonistPhone.Instance.farmNames.Length)
                {
                    Check(tested>0 && paintable==tested,"Spray reaches every camera, from in front and from under it: "+paintable+"/"+tested+" stands");
                    game.EndActiveMission();step=2;Wait(.1);return;
                }
                game.EndActiveMission();game.StartMission(farm);
                foreach(var unit in Object.FindObjectsByType<SecurityCamera>().Where(c=>game.IsMissionTarget(c)))CameraStands(unit);
                farm++;Wait(.05);return;
            case 2:
            {
                game.StartMission(0);sprayed=Object.FindObjectsByType<SecurityCamera>().First(c=>game.IsMissionTarget(c));sprayed.RestorePaint(0);
                var head=sprayed.scanHead;var forward=head.forward;forward.y=0;forward.Normalize();
                Position(Ground(head.position+forward*1.3f,6)+Vector3.up*.05f,head.position);
                var eye=game.player.GetComponentInChildren<Camera>().transform;eye.LookAt(head.position);Physics.SyncTransforms();
                int before=HouseholdEconomy.Instance.Account.paintUses;
                Check(BackpackPanel.Spray() && sprayed.PaintSecondsRemaining>59 && HouseholdEconomy.Instance.Account.paintUses==before-1,"Spraying from the backpack paints the lens and uses one charge");
                Check(Resources.Load<Material>("Effects/SprayMist")!=null && GameObject.Find("Jato de tinta")!=null,"The spray jet plays with its material from Resources (kept in builds)");
                step=3;Wait(.33);return;
            }
            case 3:
            {
                var head=sprayed.scanHead;var view=game.player.GetComponentInChildren<Camera>().transform;
                var side=Vector3.Cross(Vector3.up,(head.position-view.position).normalized);
                Capture("spray-jet",(view.position+head.position)*.5f+side*1.6f+Vector3.up*.2f,(view.position+head.position)*.5f);
                Capture("spray-first-person",view.position,head.position);
                Lockpick();
                game.EndActiveMission();
                var yard=HouseholdEconomy.Instance.home.position-HouseholdEconomy.Instance.home.forward*14;
                Position(Ground(yard+Vector3.up*3,8)+Vector3.up*.05f,yard+HouseholdEconomy.Instance.home.right*10);
                game.player.GetComponentInChildren<Camera>().transform.localRotation=Quaternion.identity;
                punchFrame=0;step=4;Wait(.3);return;
            }
            case 4:
            {
                if(punchFrame>=3){step=5;Wait(.5);return;}
                Check(PlayerPunch.Instance.TryPunch(),"Punch "+(punchFrame+1)+" thrown");step=40;Wait(new[]{.08,.25,.38}[punchFrame]);return;
            }
            case 40:
            {
                var p=game.player;string label=""+(char)('a'+punchFrame);
                var body=p.GetComponentInChildren<ProtagonistArticulation>().transform;FrameCapture.Shot(Folder+"/punch-side-"+label+".png",p.position+body.right*1.9f+body.forward*1.2f+Vector3.up*1.55f,p.position+body.forward*.4f+Vector3.up*1.35f,45);
                FrameCapture.Through(Folder+"/punch-eyes-"+label+".png",game.player.GetComponentInChildren<Camera>());{var cam=game.player.GetComponentInChildren<Camera>().transform;var bones=game.player.GetComponentsInChildren<Transform>();string Where(string n){var t=bones.FirstOrDefault(x=>x.name==n);return t==null?"?":cam.InverseTransformPoint(t.position).ToString("F2");}Note("frame "+label+": in eye space  head "+Where("Head")+"  shoulderR "+Where("UpperArmR")+"  handR "+Where("HandR")+"  shoulderL "+Where("UpperArmL")+"  handL "+Where("HandL")+"  fov "+cam.GetComponent<Camera>().fieldOfView+" near "+cam.GetComponent<Camera>().nearClipPlane);}
                punchFrame++;step=4;Wait(.9);return;
            }
            case 5:
            {
                var move=game.player.GetComponent<PlayerMovement>();move.estaAgachado=false;move.estaSprinting=false;
                float walk=move.SpeedFor(true);move.estaSprinting=true;float air=move.SpeedFor(false);float run=move.SpeedFor(true);
                Check(Mathf.Approximately(air,walk) && run>walk*1.5f,"Shift pressed in mid-air keeps the takeoff speed ("+air.ToString("0.0")+" m/s, walking "+walk.ToString("0.0")+", running on the ground "+run.ToString("0.0")+")");
                move.estaSprinting=false;
                truck=OldPickupTruck.Instance;truckRotation=truck.transform.rotation;truckPosition=truck.transform.position;
                Position(truck.seat.position-truck.transform.right*1.6f,truck.seat.position);
                Check(truck.EnterDriver(),"Elias gets in the truck");
                var body=truck.GetComponent<Rigidbody>();if(body!=null){body.isKinematic=true;}
                truck.transform.rotation=Quaternion.Euler(9,truck.transform.eulerAngles.y,14);
                step=6;Wait(.2);return;
            }
            case 6:
            {
                var eye=game.player.GetComponentInChildren<Camera>().transform;
                Check(Vector3.Angle(game.player.up,Vector3.up)>8,"Driving on the slope Elias leans with the truck ("+Vector3.Angle(game.player.up,Vector3.up).ToString("0")+" deg)");
                eye.localRotation=Quaternion.Euler(12,70,0);var looked=eye.forward;looked.y=0;
                Capture("truck-exit-before",eye.position,eye.position+eye.forward);
                truck.ForceExit(truck.seat.position-truck.transform.right*1.7f+Vector3.up*.3f);Physics.SyncTransforms();
                var forward=game.player.forward;
                Check(Vector3.Angle(game.player.up,Vector3.up)<.5f && Mathf.Abs(eye.forward.y)<.01f && Mathf.Abs(eye.right.y)<.01f,"Out of the truck he stands upright with the view level (tilt "+Vector3.Angle(game.player.up,Vector3.up).ToString("0.0")+" deg, view pitch "+(Mathf.Asin(eye.forward.y)*Mathf.Rad2Deg).ToString("0.0")+", roll "+(Mathf.Asin(eye.right.y)*Mathf.Rad2Deg).ToString("0.0")+")");
                Check(Vector3.Angle(forward,looked)<2,"He faces where he was looking from the seat ("+Vector3.Angle(forward,looked).ToString("0.0")+" deg off)");
                Capture("truck-exit-after",eye.position,eye.position+eye.forward);
                truck.transform.SetPositionAndRotation(truckPosition,truckRotation);var body=truck.GetComponent<Rigidbody>();if(body!=null)body.isKinematic=false;
                step=7;Wait(.3);return;
            }
            case 7:
            {
                game.EndActiveMission();Check(game.StartMission(0),"Mission 0 starts for the wires");
                var wires=Object.FindObjectsByType<TrapSystem>(FindObjectsSortMode.None).Where(t=>game.IsMissionTarget(t)).ToList();
                gateWire=wires.FirstOrDefault(t=>t.Gate!=null);coopWire=wires.FirstOrDefault(t=>t.AtCoop);
                Check(gateWire!=null && coopWire!=null,"The farm has a wire at its gate and one at its coop door");
                if(gateWire.Gate.IsOpen==false)gateWire.Gate.Toggle(Middle(gateWire)-gateWire.Inward*3);
                trial=0;step=8;Wait(1.2);return;
            }
            case 8:
            {
                if(trial>0){var w=trial-1<2?gateWire:coopWire;bool walked=(trial-1)%2==0;Check(walked?w.triggered:!w.triggered,(walked?"Walking into":"Jumping over")+" the "+(w==gateWire?"gate":"coop door")+" wire "+(walked?"trips it":"clears it"));}
                if(trial>=4)
                {
                    gateWire.triggered=false;truck=OldPickupTruck.Instance;truckRotation=truck.transform.rotation;truckPosition=truck.transform.position;
                    truck.transform.SetPositionAndRotation(Middle(gateWire)-gateWire.Inward*5+Vector3.up*.4f,Quaternion.LookRotation(gateWire.Inward));
                    Position(truck.seat.position-truck.transform.right*1.6f,truck.seat.position);Check(truck.EnterDriver(),"Elias drives up to the gate");
                    var body=truck.GetComponent<Rigidbody>();if(body!=null)body.isKinematic=true;
                    truck.transform.position=Middle(gateWire)+Vector3.up*.4f;if(body!=null)body.position=truck.transform.position;Physics.SyncTransforms();
                    step=9;Wait(.4);return;
                }
                {
                    var w=trial<2?gateWire:coopWire;bool walk=trial%2==0;w.triggered=false;game.player.GetComponent<PlayerMovement>().RestoreTrapSlow(0);
                    var coop=Object.FindObjectsByType<ChickenCoopLockpick>(FindObjectsSortMode.None).First(c=>game.IsMissionTarget(c));coop.RestoreOpen(w==coopWire && !walk);
                    var mid=Middle(w);Position(Ground(mid-w.Inward*(walk?2f:1.3f)+Vector3.up*2,4)+Vector3.up*.03f,mid+w.Inward);
                    var mover=game.player.gameObject.AddComponent<ReviewMover>();var move=game.player.GetComponent<PlayerMovement>();
                    mover.velocity=w.Inward*move.velocidadeNormal+Vector3.up*(walk?-1:Mathf.Sqrt(move.alturaDoSalto*-2f*move.gravidade));mover.gravity=-move.gravidade;mover.until=Time.time+(walk?1.4f:1.0f);
                    if(!walk)FrameCapture.Shot(Folder+"/wire-jump-"+(w==gateWire?"gate":"coop")+".png",mid+Vector3.Cross(Vector3.up,w.Inward)*3.8f-w.Inward*1.4f+Vector3.up*1.6f,mid+w.Inward*.3f+Vector3.up*.9f,60,.36f);
                    trial++;Wait(walk?1.8:1.4);return;
                }
            }
            case 9:
            {
                Check(gateWire.triggered,"Driving the truck through the gate snaps the wire");
                truck.ForceExit(truck.seat.position-truck.transform.right*1.7f+Vector3.up*.3f);
                truck.transform.SetPositionAndRotation(truckPosition,truckRotation);var body=truck.GetComponent<Rigidbody>();if(body!=null)body.isKinematic=false;
                gateWire.triggered=false;coopWire.triggered=false;game.EndActiveMission();
                coopLevel=0;coopParts.Clear();step=10;Wait(.2);return;
            }
            case 10:
            {
                if(coopLevel>3)
                {
                    Check(coopParts.Zip(coopParts.Skip(1),(a,b)=>b.parts>a.parts).All(x=>x),"Each repair level adds pieces to the coop you can see (visible pieces by level: "+string.Join(", ",coopParts.Select(c=>c.parts))+")");
                    Check(coopParts[0].torn>0 && coopParts[1].torn>0 && coopParts[2].torn==0 && coopParts[3].torn==0,"The torn roof sheets go when the new roof is on (level 2)");
                    EditorApplication.isPlaying=false;step=99;return;
                }
                HouseholdEconomy.Instance.RestoreAccount(new HouseholdAccount{introSeen=true,regionalSecurity=true,paintUses=99,repairs=coopLevel});
                step=11;Wait(.3);return;
            }
            case 11:
            {
                var coop=HouseholdEconomy.Instance.home.Find("Galinheiro gasto - Ultimo Recurso");
                var extra=coop.Find("Melhorias permanentes");
                int parts=extra!=null?extra.GetComponentsInChildren<Renderer>().Count(r=>r.enabled):0;
                int torn=coop.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Chapa ondulada rasgada"));
                coopParts.Add((parts,torn));
                Capture("coop-level-"+coopLevel,coop.TransformPoint(new Vector3(4.6f,2.7f,-5.6f)),coop.TransformPoint(new Vector3(0,.9f,0)));
                coopLevel++;step=10;Wait(.1);return;
            }
            }
        }
        catch(Exception e){Check(false,e.ToString());EditorApplication.isPlaying=false;}
    }
    static void Lockpick()
    {
        int notFirst=0;
        for(int seed=0;seed<12;seed++)
        {
            var latch=new CoopPressureLatch(seed);int first=latch.NextPiece;
            for(int i=0;i<2000 && latch.Completed==0;i++){float delta=latch.RequiredPressure-latch.Pressure;latch.Step(.02f,latch.NextPiece,Mathf.Clamp(delta/(.38f*.02f),-1,1),Mathf.Abs(delta)<.01f,false);}
            bool right=latch.Completed==1 && latch.Released(first) && Enumerable.Range(0,3).Count(p=>latch.Released(p))==1;
            if(first!=0)notFirst++;
            Check(right,"Lock "+seed+": the first piece freed is piece "+(first+1)+" and only its dot lights");
        }
        Check(notFirst>0,"Some locks free another piece than the first one first ("+notFirst+"/12): the dots follow the pieces, not the count");
        var coop=Object.FindAnyObjectByType<ChickenCoopLockpick>();var solved=new CoopPressureLatch(5);int piece=solved.NextPiece;
        for(int i=0;i<2000 && solved.Completed==0;i++){float delta=solved.RequiredPressure-solved.Pressure;solved.Step(.02f,solved.NextPiece,Mathf.Clamp(delta/(.38f*.02f),-1,1),Mathf.Abs(delta)<.01f,false);}
        typeof(ChickenCoopLockpick).GetProperty("Latch").SetValue(coop,solved);
        Check(Enumerable.Range(0,3).All(p=>coop.PinReleased(p)==(p==piece)),"The coop panel lights dot "+(piece+1)+" for piece "+(piece+1));
        coop.RestoreOpen(false);
    }
    static void Capture(string name,Vector3 from,Vector3 at)
    {
        var go=new GameObject("review shot");var cam=go.AddComponent<Camera>();cam.fieldOfView=55;cam.nearClipPlane=.03f;go.transform.position=from;go.transform.LookAt(at);
        var rt=new RenderTexture(960,540,24);cam.targetTexture=rt;cam.Render();var a=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(960,540,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,960,540),0,0);t.Apply();RenderTexture.active=a;
        File.WriteAllBytes(Folder+"/"+name+".png",t.EncodeToPNG());cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(t);Object.DestroyImmediate(go);
    }
    static void CameraStands(SecurityCamera unit)
    {
        unit.RestorePaint(0);
        var head=unit.scanHead!=null?unit.scanHead:unit.transform;var forward=head.forward;forward.y=0;forward.Normalize();
        var eye=game.player.GetComponentInChildren<Camera>().transform;
        foreach(var (label,offset) in new[]{("in front",forward*1.3f),("under",forward*.25f)})
        {
            var ground=Ground(head.position+offset,6);Position(ground+Vector3.up*.05f,head.position);
            eye.LookAt(head.position);Physics.SyncTransforms();
            bool ok=unit.CanPaintNow();tested++;if(ok)paintable++;
            if(!ok)
            {
                var delta=head.position-eye.position;var first=Physics.RaycastAll(eye.position,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore)
                    .Where(h=>!h.transform.IsChildOf(game.player)).OrderBy(h=>h.distance).Select(h=>h.collider.name).FirstOrDefault()??"nothing";
                Note(unit.GetComponentInParent<FarmLayoutInfo>().identity+" / "+unit.name+" "+label+": cannot paint (head "+delta.magnitude.ToString("0.0")+" m away, first in the way: "+first+")");
            }
        }
    }
}
