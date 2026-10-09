using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class ProtagonistActionReview
{
    const string Key="ProtagonistV2.ActionReview",Folder="output/articulation-review";
    static readonly List<string> results=new List<string>();
    static int phase,errors;static float next;static bool passed;
    static HeistGameManager game;static ProtagonistArticulation rig;static Camera eyes;
    static ChickenCoopLockpick coop;static InteractableChicken bird;
    static Quaternion fingerBefore;static Transform finger;
    static ProtagonistActionReview()
    {
        EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false) && (t==LogType.Exception || t==LogType.Error)){errors++;results.Add("ERROR "+m);}};
    }
    public static void RunBatch()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        Directory.CreateDirectory(Folder);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();
        economy.editorTestSavePath=Path.GetFullPath("Temp/articulation-review-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount());
        SessionState.SetBool(Key,true);SessionState.SetBool(Key+".batch",true);EditorApplication.isPlaying=true;
    }
    public static void RunRegression()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        SessionState.SetBool("Protagonist.Batch",true);PlayableVillageTests.Run();
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;errors=0;results.Clear();next=Time.realtimeSinceStartup+3;}
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
            if(SessionState.GetBool(Key+".batch",false))EditorApplication.delayCall+=()=>EditorApplication.Exit(passed?0:1);
        }
    }
    static void Check(bool ok,string message){results.Add((ok?"PASS ":"FAIL ")+message);}
    static void Position(Vector3 point)
    {var cc=game.player.GetComponent<CharacterController>();cc.enabled=false;game.player.position=point;cc.enabled=true;Physics.SyncTransforms();}
    static void Capture(string name)
    {
        var driver=rig.GetComponent<RuralCharacterAnimator>();
        results.Add("INFO "+name+" clip="+driver.CurrentState+" root="+rig.transform.localPosition+" bones="+string.Join("; ",rig.GetComponentsInChildren<Transform>().Where(t=>new[]{"Hips","Head","UpperArmR","HandR"}.Contains(t.name)).Select(t=>t.name+":"+game.player.InverseTransformPoint(t.position))));
        var go=new GameObject("Articulation review camera");var camera=go.AddComponent<Camera>();
        camera.transform.position=game.player.position+game.player.forward*2.3f+game.player.right*1.6f+Vector3.up*1.7f;
        camera.transform.LookAt(game.player.position+Vector3.up*.95f);camera.nearClipPlane=.02f;camera.farClipPlane=80;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.18f);
        var texture=new RenderTexture(960,720,24);texture.Create();
        void Save(Camera c,string suffix)
        {
            var old=c.targetTexture;c.targetTexture=texture;c.Render();var active=RenderTexture.active;RenderTexture.active=texture;
            var png=new Texture2D(960,720,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,960,720),0,0);png.Apply();
            File.WriteAllBytes(Folder+"/"+name+suffix+".png",png.EncodeToPNG());Object.DestroyImmediate(png);RenderTexture.active=active;c.targetTexture=old;
        }
        var layers=rig.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>t.gameObject.layer);
        foreach(var t in layers.Keys)t.gameObject.layer=28;
        camera.cullingMask=1<<28;
        var lightObj=new GameObject("Review light");var light=lightObj.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.cullingMask=1<<28;lightObj.transform.rotation=Quaternion.Euler(40,-30,0);
        Save(camera,"-body");foreach(var t in layers)t.Key.gameObject.layer=t.Value;Object.DestroyImmediate(lightObj);
        Save(eyes,"-eyes");
        // What the player sees when looking down at the hands, and close-ups of each hand on the body the player sees
        // (the first-person copy while the hands are busy, otherwise the full body).
        Transform Bone(string n)=>rig.GetComponentsInChildren<Transform>(true).First(t=>t.name==n);
        var hl=Bone("HandL");var hr=Bone("HandR");var mid=(hl.position+hr.position)*.5f;
        var eyeRot=eyes.transform.rotation;eyes.transform.rotation=Quaternion.LookRotation(mid-eyes.transform.position,game.player.up);
        Save(eyes,"-eyes-hands");
        // the same view split into what is body and what is world
        var eyeMask=eyes.cullingMask;eyes.cullingMask=eyeMask&(1<<30);var eyeClear=eyes.clearFlags;var eyeBg=eyes.backgroundColor;
        eyes.clearFlags=CameraClearFlags.SolidColor;eyes.backgroundColor=new Color(.9f,.2f,.8f);Save(eyes,"-eyes-hands-bodyonly");
        eyes.cullingMask=eyeMask&~(1<<30);eyes.clearFlags=eyeClear;eyes.backgroundColor=eyeBg;Save(eyes,"-eyes-hands-nobody");
        eyes.cullingMask=eyeMask;
        // a natural glance down (35 degrees below the horizon), like the player looking at the wheel
        eyes.transform.rotation=Quaternion.AngleAxis(35,game.player.right)*Quaternion.LookRotation(game.player.forward,game.player.up);Save(eyes,"-eyes-glance");
        eyes.transform.rotation=eyeRot;
        var fp=rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.name=="Corpo em primeira pessoa");
        var shown=fp!=null && fp.enabled?new[]{fp.transform}:rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.gameObject.layer==31).Select(r=>r.transform).ToArray();
        var saved=shown.ToDictionary(t=>t,t=>t.gameObject.layer);foreach(var t in shown)t.gameObject.layer=28;
        var lightObj2=new GameObject("Review light");var light2=lightObj2.AddComponent<Light>();light2.type=LightType.Directional;light2.intensity=2;light2.cullingMask=1<<28;lightObj2.transform.rotation=Quaternion.Euler(40,-30,0);
        foreach(var (hand,label) in new[]{(hl,"L"),(hr,"R")})
        {
            var palm=hand.position;
            foreach(var (dir,tag) in new[]{(game.player.up*.6f-game.player.forward*.15f+game.player.right*(label=="L"?-.15f:.15f),"top"),(-game.player.forward*.45f+game.player.right*(label=="L"?-.35f:.35f)+game.player.up*.1f,"side")})
            {camera.transform.position=palm+dir.normalized*.32f;camera.transform.LookAt(palm+(Bone(label=="L"?"Middle2L":"Middle2R").position-palm)*.5f,game.player.up);camera.fieldOfView=40;Save(camera,"-hand"+label+"-"+tag);}
        }
        foreach(var t in saved)t.Key.gameObject.layer=t.Value;
        // The wheel grip seen from outside: full body and steering wheel only, from the dashboard and from the left.
        if(name=="wheel" && OldPickupTruck.Instance!=null)
        {
            var wheel=OldPickupTruck.Instance.steeringWheel;
            foreach(var sd in new[]{"L","R"})
            {
                var hb=Bone("Hand"+sd);var grip=new HandGripPose(hb,rig.transform);
                Vector3 W(Vector3 d)=>wheel.InverseTransformDirection(d);
                string F3(Vector3 v)=>v.ToString("F2");
                var centre=OldPickupTruck.Instance.SteeringGrip(sd=="L"?-1:1);
                results.Add("INFO grip "+sd+": wrist "+F3(wheel.InverseTransformPoint(hb.position))+" anchor "+F3(wheel.InverseTransformPoint(PowerGrip.AnchorWorld(grip,OldPickupTruck.SteeringRimRadius)))+" rim centre "+F3(wheel.InverseTransformPoint(centre))
                    +" palm faces "+F3(W(hb.TransformDirection(grip.PalmLocal)))+" knuckles "+F3(W(hb.TransformDirection(grip.FingersLocal)))
                    +" | flex "+string.Join(" ",new[]{"Index","Middle","Little","Thumb"}.Select(f=>f+":"+string.Join("/",Enumerable.Range(1,3).Select(k=>{var b=Bone(f+k+sd);return Quaternion.Angle(b.localRotation,Quaternion.identity).ToString("F0");}))))
                    +" frame "+Time.frameCount);
            }
            var steering=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).FirstOrDefault(r=>r.name=="Steering" && Vector3.Distance(r.bounds.center,wheel.position)<.1f);
            var body=rig.GetComponentsInChildren<Renderer>(true).Where(r=>r.gameObject.layer==31 || r.name.StartsWith("Protagonist")).Select(r=>r.transform).ToList();
            if(steering!=null)body.Add(steering.transform);
            var keep=body.ToDictionary(t=>t,t=>t.gameObject.layer);foreach(var t in body)t.gameObject.layer=28;
            var fpSkin=rig.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.name=="Corpo em primeira pessoa");bool fpOn=fpSkin!=null && fpSkin.enabled;if(fpSkin!=null)fpSkin.enabled=false;
            foreach(var (pos,look,tag) in new[]{(wheel.position+wheel.forward*.55f+wheel.up*.08f,wheel.position,"grip-front"),(wheel.position-wheel.right*.42f+wheel.forward*.12f+wheel.up*.05f,OldPickupTruck.Instance.SteeringGrip(-1),"grip-left"),(wheel.position+wheel.right*.42f+wheel.forward*.12f+wheel.up*.05f,OldPickupTruck.Instance.SteeringGrip(1),"grip-right")})
            {camera.transform.position=pos;camera.transform.LookAt(look,wheel.up);camera.fieldOfView=tag=="grip-front"?50:34;Save(camera,"-"+tag);}
            if(fpSkin!=null)fpSkin.enabled=fpOn;foreach(var t in keep)t.Key.gameObject.layer=t.Value;
        }
        Object.DestroyImmediate(lightObj2);
        texture.Release();Object.DestroyImmediate(texture);Object.DestroyImmediate(go);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try
        {
            if(phase==0)
            {
                game=HeistGameManager.Instance;GameMenu.Instance.SendMessage("Resume",SendMessageOptions.DontRequireReceiver);
                rig=game.player.GetComponentInChildren<ProtagonistArticulation>();eyes=game.player.GetComponentInChildren<Camera>();
                foreach(var look in game.player.GetComponentsInChildren<PlayerLook>())look.enabled=false;
                game.player.GetComponent<PlayerMovement>().enabled=false;
                Check(rig!=null,"new protagonist articulation installed");
                Check(rig.GetComponentsInChildren<Transform>().Count(t=>new[]{"Index","Middle","Ring","Little","Thumb"}.Any(p=>t.name.StartsWith(p)))>=18,"weighted finger joints (native Elias: thumb, index and ring, three joints each)");
                finger=rig.GetComponentsInChildren<Transform>().Single(t=>t.name=="Index2R");fingerBefore=finger.localRotation;
                eyes.transform.localRotation=Quaternion.Euler(20,60,0);
            }
            else if(phase==1)
            {
                Check(rig.HeadYaw>40 && rig.HeadYaw<=65 && rig.HeadPitch>10,"head follows right/down within anatomical limits");Capture("look-right");
                eyes.transform.localRotation=Quaternion.Euler(-30,-70,0);
            }
            else if(phase==2)
            {
                Check(rig.HeadYaw< -40 && rig.HeadPitch< -15,"head follows left/up without accumulating rotations");Capture("look-left");
                eyes.transform.localRotation=Quaternion.identity;game.backpack.RestoreCount(0);
                Position(OldPickupTruck.Instance.seat.position-OldPickupTruck.Instance.transform.right);
                Check(OldPickupTruck.Instance.EnterDriver(),"enter truck");
            }
            else if(phase==3)
            {
                Check(rig.ActionState=="Drive","driving pose");
                Check(game.player.GetComponent<PlayerChickenCarry>().HandError<.12f,"wheel hand contact error "+game.player.GetComponent<PlayerChickenCarry>().HandError);
                Check(Quaternion.Angle(fingerBefore,finger.localRotation)>8,"real finger geometry curls on wheel");Capture("wheel");
                OldPickupTruck.Instance.ignition.Begin();
            }
            else if(phase==4)
            {
                Check(rig.ActionState=="Ignite","ignition controls right arm");Check(rig.ContactError<.12f,"key hand contact error "+rig.ContactError);Capture("ignition");
                OldPickupTruck.Instance.ForceExit(OldPickupTruck.Instance.transform.position+OldPickupTruck.Instance.transform.right*2);
                bird=Object.FindObjectsByType<InteractableChicken>().First(b=>b.coop!=null);coop=bird.coop;
                var names=ProtagonistPhone.Instance.farmNames;game.StartMission(Array.IndexOf(names,bird.GetComponentInParent<FarmLayoutInfo>().identity));
                Vector3 stand=coop.InteractionPoint-coop.transform.forward*.65f;
                if(Physics.Raycast(stand+Vector3.up*.15f,Vector3.down,out var ground,5,~0,QueryTriggerInteraction.Ignore))stand.y=ground.point.y+.02f;
                else stand.y=coop.InteractionPoint.y-1.1f;
                Position(stand);
                game.player.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(coop.InteractionPoint-game.player.position,Vector3.up));
                coop.SendMessage("BeginChallenge");
            }
            else if(phase==5)
            {
                Check(rig.ActionState=="Lockpick","lockpick action pose");Check(rig.ContactError<.12f,"lock hand contact error "+rig.ContactError);Capture("lockpick");
                coop.SendMessage("EndChallenge");game.backpack.RestoreCount(1);game.player.GetComponent<PlayerChickenCarry>().Lift(bird);
            }
            else if(phase==6)
            {
                var carry=game.player.GetComponent<PlayerChickenCarry>();Check(carry.HasVisual && !carry.IsLifting,"pickup transitions into holding bird");
                Check(carry.HandError<.12f,"bird hand contact error "+carry.HandError);Capture("carry");game.backpack.RestoreCount(0);
            }
            else if(phase==7)
            {
                Check(!game.player.GetComponent<PlayerChickenCarry>().HasVisual,"release removes carried bird");
                Check(rig.ActionState=="Idle","action exits to idle");
                Check(errors==0,"no runtime exceptions");
                passed=!results.Any(r=>r.StartsWith("FAIL") || r.StartsWith("ERROR"));File.WriteAllLines(Folder+"/checks.txt",results);
                EditorApplication.isPlaying=false;return;
            }
            phase++;next=Time.realtimeSinceStartup+2;
        }
        catch(Exception e){results.Add("ERROR "+e);File.WriteAllLines(Folder+"/checks.txt",results);passed=false;EditorApplication.isPlaying=false;}
    }
}
