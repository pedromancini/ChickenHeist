using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class VisitorCinematicReview
{
    const string Key="VisitorCinematicReview",Folder="output/visitor-opening";
    static int phase;static double next,start;static bool failed;
    static Vector3 cameraPosition;static Quaternion cameraRotation,doorRotation;static float cameraFov,near;
    static int balance,flock;static HomeDoor door;static bool doorEnabled;
    static VisitorCinematicReview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    [MenuItem("Chicken Heist/Cinematics/Install Visitor Opening")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
        Directory.CreateDirectory(Folder);
        var root=new GameObject("VisitorStage");var stage=root.AddComponent<VisitorCinematicStage>();
        var protagonist=AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistInstaller.PrefabPath);
        stage.elias=Object.Instantiate(protagonist,root.transform).transform;
        // the hooded visitor, rigged on Elias' bone names (HoodedVisitorInstall)
        stage.visitor=new GameObject("Visitante").transform;stage.visitor.SetParent(root.transform,false);HoodedVisitorInstall.Build(stage.visitor);
        foreach(var actor in new[]{stage.elias})
        {
            actor.localPosition=Vector3.zero;actor.localRotation=Quaternion.identity;
            foreach(var script in actor.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(script);
            foreach(var collider in actor.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            foreach(var node in actor.GetComponentsInChildren<Transform>(true))
            {node.gameObject.layer=0;if(node.name=="Corpo em primeira pessoa" || node.name.Contains("Celular"))node.gameObject.SetActive(false);}
            foreach(var renderer in actor.GetComponentsInChildren<Renderer>()){renderer.enabled=true;renderer.forceRenderingOff=false;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;}
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())skin.updateWhenOffscreen=true;
            foreach(var animation in actor.GetComponentsInChildren<Animation>()){animation.playAutomatically=false;animation.enabled=false;}
        }
        stage.elias.name="Elias - ator cinematografico";stage.visitor.name="Visitante - identidade desconhecida";
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/Cinematics/VisitorStage.prefab");Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
    }
    public static void InstallAndReview(){Install();Recon();Review();}
    static void Recon()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);Directory.CreateDirectory("Assets/Resources/Cinematics/Recon");
        var farms=Object.FindObjectsByType<FarmLayoutInfo>().OrderBy(f=>f.identity).Take(2).ToArray();
        var cameraNode=new GameObject("Recon photo camera");var camera=cameraNode.AddComponent<Camera>();camera.CopyFrom(Camera.main);camera.cullingMask=~(1<<31);camera.rect=new Rect(0,0,1,1);camera.fieldOfView=58;
        var target=new RenderTexture(960,540,24);camera.targetTexture=target;
        try
        {
            for(int i=0;i<farms.Length;i++)
            {
                var farm=farms[i];Vector3 center=new Vector3(farm.lot.center.x,farm.entrance.y,farm.lot.center.y);Vector3 outward=(farm.entrance-center).normalized;
                Vector3 p=farm.entrance+outward*5+Vector3.Cross(Vector3.up,outward)*1.6f;
                if(Physics.Raycast(p+Vector3.up*20,Vector3.down,out var hit,40))p.y=hit.point.y;
                camera.transform.position=p+Vector3.up*1.65f;camera.transform.LookAt(farm.entrance-outward*4+Vector3.up*1.25f);camera.aspect=960f/540;
                camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var texture=new Texture2D(960,540,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,960,540),0,0);texture.Apply();
                File.WriteAllBytes("Assets/Resources/Cinematics/Recon/farm-"+i.ToString("00")+".png",texture.EncodeToPNG());Object.DestroyImmediate(texture);RenderTexture.active=old;
            }
        }
        finally{camera.targetTexture=null;Object.DestroyImmediate(target);Object.DestroyImmediate(cameraNode);}
        AssetDatabase.Refresh();
    }
    public static void Review()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/checks.txt","");
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/visitor-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount());
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;failed=false;start=EditorApplication.timeSinceStartup;next=start+4;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(failed?1:0);}
    }
    static void Check(bool ok,string message){failed|=!ok;File.AppendAllText(Folder+"/checks.txt",(ok?"PASS ":"FAIL ")+message+"\n");}
    static void SetLine(int line,float fraction)
    {
        var director=StoryDirector.Instance;var type=typeof(StoryDirector);
        type.GetField("line",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(director,line);
        type.GetField("lineClock",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(director,VisitorOpeningDialogue.Durations[line]*fraction);
        type.GetMethod("StartLine",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(director,null);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(EditorApplication.timeSinceStartup-start>200)throw new TimeoutException("Visitor review timeout");
            if(phase==0){GameMenu.Instance.Resume();next=EditorApplication.timeSinceStartup+.3;phase=1;return;}
            if(phase==1)
            {
                var camera=Camera.main;cameraPosition=camera.transform.localPosition;cameraRotation=camera.transform.localRotation;cameraFov=camera.fieldOfView;near=camera.nearClipPlane;
                door=HouseholdEconomy.Instance.home.GetComponentInChildren<HomeDoor>();doorRotation=door.hinge.localRotation;doorEnabled=door.enabled;
                balance=HouseholdEconomy.Instance.Account.balance;flock=HouseholdEconomy.Instance.Account.flock;
                HouseholdEconomy.Instance.Commit(a=>{a.introSeen=true;return true;},"Isolated replay validation");
                Check(VisitorOpeningDialogue.Text.Length==VisitorOpeningDialogue.Speakers.Length && VisitorOpeningDialogue.Durations.Length==VisitorOpeningDialogue.Text.Length,"All spoken and silent beats have timings and speakers");
                Check(StoryDirector.Instance.Begin(false),"New 3D visitor opening starts");
                Check(GameMenu.BlocksInput,"Cinematic owns gameplay input");
                StoryDirector.Instance.SetPaused(true);Check(StoryDirector.Instance.Paused,"Pause has explicit state");StoryDirector.Instance.SetPaused(false);
                phase=2;next=EditorApplication.timeSinceStartup+.3;return;
            }
            if(phase==2)
            {
                var stage=Object.FindAnyObjectByType<VisitorCinematicStage>();var camera=Camera.main;
                Check(stage!=null,"Visitor prefab instantiated with two rigged actors");
                var visitorBody=stage.visitor.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s=>s.name=="VisitorBody");
                Check(visitorBody!=null && visitorBody.enabled && visitorBody.bones.Count(b=>b!=null)>=50 && new[]{"HandL","Index3L","Little3R","Thumb3R","ForearmR"}.All(n=>visitorBody.bones.Any(b=>b!=null && b.name==n)),
                    "Hooded visitor is the supplied 6d4aedaf model rigged with arms, wrists and fingers: "+(visitorBody!=null?visitorBody.bones.Length+" bones":"missing"));
                Check(visitorBody!=null && visitorBody.sharedMaterial!=null && visitorBody.sharedMaterial.GetTexture("_BaseMap")!=null,"Hooded visitor keeps its painted texture");
                File.WriteAllText(Folder+"/wrists.txt","beat: wrist load (1 = at the joint's limit) and contact error, Elias | visitor\n");
                Check(stage.GetComponentsInChildren<Light>().Count(l=>l.shadows!=UnityEngine.LightShadows.None)<=1,"Only key light casts additional shadows");
                {
                    var keys=new[]{"bell","box_open","cage_lid","camera_alert","chicken","chicken_pickup","chicken_scared","chicken_walk","cow","creak","door","door_creak","engine","gate_metal","knock","latch_click","latch_scrape","latch_slip","night_ambience","porch_step","rooster_morning","shotgun_blast","shotgun_reload","sleep","spray","start","stall","starter","step","truck_door","wake","wire_rattle","wood"};
                    var missing=keys.Where(k=>SoundLibrary.Load(k)==null).ToArray();
                    Check(missing.Length==0,"Recorded sound effects load in place of the synthesized ones: "+(keys.Length-missing.Length)+"/"+keys.Length+(missing.Length>0?" missing "+string.Join(",",missing):""));
                }
                float total=0;
                for(int line=0;line<VisitorOpeningDialogue.Text.Length;line++)
                {
                    SetLine(line,.5f);float duration=VisitorOpeningDialogue.Durations[line];
                    stage.Evaluate(camera,line,duration*.5f,duration,total+duration*.5f);
                    Check(!float.IsNaN(camera.transform.position.x) && !float.IsNaN(stage.EliasHand.x),"Finite camera and actor pose at beat "+line);
                    if(new[]{0,1,2,5,8,9,12,14,19,24,28,29,30}.Contains(line))Capture("shot-"+line.ToString("00"));
                    Wrists(stage,"beat "+line);
                    total+=duration;
                }
                // storyboard: every beat at its start, middle and end, as the player sees it (output/visitor-opening/story)
                Directory.CreateDirectory(Folder+"/story");float clock=0;
                for(int line=0;line<VisitorOpeningDialogue.Text.Length;line++)
                {
                    float duration=VisitorOpeningDialogue.Durations[line];
                    foreach(var (k,f) in new[]{("a",.08f),("b",.5f),("c",.92f)})
                    {SetLine(line,f);stage.Evaluate(camera,line,duration*f,duration,clock+duration*f);Capture("story/beat-"+line.ToString("00")+"-"+k);}
                    clock+=duration;
                }
                // the moments the hands are busy, up close: leaning on the desk, knocking, the lever, shutting the door
                foreach(var (line,at,visitorSide,name) in new[]{(0,.85f,false,"desk"),(1,1.43f,true,"knock"),(5,.9f,false,"lever"),(29,3.2f,false,"door-close")})
                {
                    SetLine(line,at/VisitorOpeningDialogue.Durations[line]);stage.Evaluate(camera,line,at,VisitorOpeningDialogue.Durations[line],200+line+at);
                    if(!visitorSide)Capture("moment-"+name);
                    HandCloseUps(visitorSide?stage.VisitorActor:stage.EliasActor,"hands-"+name+(visitorSide?"-visitor":"-elias"));
                    Wrists(stage,name+" (beat "+line+" at "+at+" s)");
                    if(name=="knock")Check(stage.VisitorActor.ContactError<.03f,"Knuckles meet the door on the knock: "+stage.VisitorActor.ContactError.ToString("F3"));
                    // the hand at work, as the shot shows it: on screen, not hidden behind the actor's own trunk
                    foreach(bool right in name=="desk"?new[]{true,false}:new[]{name=="knock"})
                    {
                        var actor=visitorSide?stage.VisitorActor:stage.EliasActor;
                        Check(HandInShot(camera,actor,right,out var where),(visitorSide?"The visitor's ":"Elias' ")+(right?"right":"left")+" hand shows in the shot at "+name+": "+where);
                    }
                    Check(stage.EliasActor.WristLoad<=1.001f && stage.VisitorActor.WristLoad<=1.001f,"Wrists within their range at "+name);
                }
                // the door swings into the house: it must clear Elias, who pulls it open and steps out of its way
                if(stage.Door!=null)
                {
                    float clearance=float.PositiveInfinity;var hinge=stage.Door.hinge;
                    for(float t=0;t<=VisitorOpeningDialogue.Durations[5];t+=.1f)
                    {
                        SetLine(5,t/VisitorOpeningDialogue.Durations[5]);stage.Evaluate(camera,5,t,VisitorOpeningDialogue.Durations[5],300+t);
                        if(Quaternion.Angle(hinge.localRotation,Quaternion.identity)<4)continue;
                        Vector3 a=hinge.position,b=hinge.TransformPoint(new Vector3(1.47f,0,0));Vector3 v=stage.elias.position;
                        Vector2 A=new Vector2(a.x,a.z),B=new Vector2(b.x,b.z),V=new Vector2(v.x,v.z);
                        float u=Mathf.Clamp01(Vector2.Dot(V-A,B-A)/(B-A).sqrMagnitude);clearance=Mathf.Min(clearance,Vector2.Distance(A+(B-A)*u,V));
                    }
                    Check(clearance>.28f,"The door swinging into the house clears Elias: "+clearance.ToString("F2")+" m from his centre");
                }
                // the opening as the shot shows it: Elias keeps his distance from the camera while he steps aside (he used
                // to back into it, his shirt filling the picture), and the visitor then shows in the doorway beside him
                {
                    float d5=VisitorOpeningDialogue.Durations[5],closest=float.PositiveInfinity;
                    for(float t=0;t<=d5;t+=.1f){SetLine(5,t/d5);stage.Evaluate(camera,5,t,d5,320+t);closest=Mathf.Min(closest,Vector3.Distance(camera.transform.position,stage.EliasActor.head.position));}
                    Check(closest>1.2f,"Elias stays clear of the camera while he opens the door: at least "+closest.ToString("F2")+" m from it");
                    foreach(var at in new[]{4.4f,5.3f})
                    {
                        SetLine(5,at/d5);stage.Evaluate(camera,5,at,d5,330+at);
                        float aspect=camera.aspect;camera.aspect=1280f/(720*camera.rect.height);
                        var face=camera.WorldToViewportPoint(stage.VisitorActor.head.position);var chest=camera.WorldToViewportPoint(stage.EliasActor.spine.position);
                        float tan=Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad);camera.aspect=aspect;
                        // apart by more than his half shoulder width (0.25 m at his depth) and half a head (0.12 m at the visitor's)
                        float apart=Mathf.Abs(face.x-chest.x),need=.25f/(2*chest.z*tan*1280f/(720*camera.rect.height))+.12f/(2*face.z*tan*1280f/(720*camera.rect.height));
                        bool inFrame=face.z>chest.z && face.x>.05f && face.x<.95f && face.y>.05f && face.y<.95f;
                        Check(inFrame && apart>need,"The opening door shows the visitor beside Elias at "+at+" s: his face at ("+face.x.ToString("0.00")+", "+face.y.ToString("0.00")+"), "+apart.ToString("0.00")+" of the width from Elias' chest (needs "+need.ToString("0.00")+")");
                    }
                    Capture("moment-door-open");
                }
                stage.Evaluate(camera,28,.8f,6.5f,120);var before=stage.visitor.position;
                stage.Evaluate(camera,28,4.5f,6.5f,124);Check(Vector3.Distance(before,stage.visitor.position)>2,"Visitor physically walks away");
                stage.Evaluate(camera,29,.3f,8.5f,125);before=stage.elias.position;
                stage.Evaluate(camera,29,7.8f,8.5f,132);Check(Vector3.Distance(before,stage.elias.position)>2,"Elias physically returns to table");
                // shutting the door: his hand stays on the inner lever while he pushes it shut, and he ends inside
                SetLine(29,.38f);stage.Evaluate(camera,29,3.2f,8.5f,127);
                Check(stage.EliasActor.ContactError<.05f,"Elias' hand stays on the inner lever while he pushes the door shut: "+stage.EliasActor.ContactError.ToString("F3"));
                // the lever in front of his left shoulder (x < 0 in his own space), not across his body
                foreach(var at in new[]{2.6f,3.4f,4.2f})
                {
                    SetLine(29,at/8.5f);stage.Evaluate(camera,29,at,8.5f,127+at);var lever=stage.elias.InverseTransformPoint(stage.Door.hinge.TransformPoint(new Vector3(1.30f,1.06f,.10f)));
                    Check(lever.x<-.06f && lever.z>.2f,"Door lever in front of Elias' left shoulder at "+at+" s (his space: x "+lever.x.ToString("F2")+", z "+lever.z.ToString("F2")+")");
                }
                SetLine(29,.54f);stage.Evaluate(camera,29,4.6f,8.5f,128);
                Check(Quaternion.Angle(stage.Door.hinge.localRotation,Quaternion.identity)<1 && Vector3.Dot(stage.elias.position-stage.Door.hinge.position,stage.Door.hinge.parent.forward)>.3f,"Door shut with Elias inside the house");
                // and he never steps out of the house to do it: from the doorway, round the open door, pushing it shut
                {
                    float least=float.PositiveInfinity;
                    for(float t=0;t<=8.5f;t+=.1f){SetLine(29,t/8.5f);stage.Evaluate(camera,29,t,8.5f,140+t);least=Mathf.Min(least,Vector3.Dot(stage.elias.position-stage.Door.hinge.position,stage.Door.hinge.parent.forward));}
                    Check(least>.05f,"Elias shuts the door from inside, never stepping out onto the porch: at least "+least.ToString("F2")+" m inside the door line all along");
                }
                foreach(var at in new[]{.8f,1.6f,2.4f,3.2f,4.0f,4.8f}){SetLine(29,at/8.5f);stage.Evaluate(camera,29,at,8.5f,130+at);Capture("door-close-"+at.ToString("0.0"));}
                // the visitor's close-up (and Elias' reverse): how much of the frame the door and its frame take
                {
                    float d8=VisitorOpeningDialogue.Durations[8];SetLine(8,.5f);stage.Evaluate(camera,8,d8*.5f,d8,150);
                    var leaf=stage.Door.hinge.GetComponentsInChildren<Renderer>().ToList();
                    var frame=HouseholdEconomy.Instance.home.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Batente completo da entrada").SelectMany(t=>t.GetComponentsInChildren<Renderer>()).Except(leaf).ToList();
                    float doorShare=Share(camera,leaf),frameShare=Share(camera,frame);
                    Check(doorShare+frameShare<.06f,"The visitor's close-up keeps the door and the doorway out of the shot: door "+(doorShare*100).ToString("0.0")+"%, frame "+(frameShare*100).ToString("0.0")+"% of the picture");
                    Capture("framing-visitor-close-up");
                    // his mouth, as that shot shows it: the mask's chin drops visibly at full voice
                    Mouth(stage.visitor,camera,"visitor","framing-visitor-mouth");
                    MouthAlong(stage,camera,8,stage.visitor,"visitor");
                    float d9=VisitorOpeningDialogue.Durations[9];SetLine(9,.5f);stage.Evaluate(camera,9,d9*.5f,d9,151);
                    Mouth(stage.elias,camera,"Elias","framing-elias-mouth");
                }
                // the bill as Elias sees it, from his eyes (it must read upright to him)
                {
                    SetLine(0,.5f);stage.Evaluate(camera,0,3,6,3);
                    var go=new GameObject("pov");var cam=go.AddComponent<Camera>();cam.CopyFrom(camera);cam.rect=new Rect(0,0,1,1);cam.fieldOfView=38;cam.nearClipPlane=.05f;
                    cam.transform.position=stage.EliasActor.head.position+stage.EliasActor.root.forward*.08f+Vector3.up*.05f;cam.transform.LookAt(stage.BillCentre,stage.EliasActor.root.forward);
                    var t=new RenderTexture(900,700,24);cam.targetTexture=t;cam.Render();var a=RenderTexture.active;RenderTexture.active=t;var tex=new Texture2D(900,700,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,900,700),0,0);tex.Apply();RenderTexture.active=a;
                    File.WriteAllBytes(Folder+"/bill-pov.png",tex.EncodeToPNG());cam.targetTexture=null;Object.DestroyImmediate(t);Object.DestroyImmediate(tex);Object.DestroyImmediate(go);
                }
                // bodies whole, at the knock, through the door shutting and while talking (output/visitor-opening/body)
                Directory.CreateDirectory(Folder+"/body");
                foreach(var (line,at,visitorSide) in new[]{(0,.85f,false),(0,2.5f,false),(1,.75f,true),(1,1.43f,true),(1,2.8f,true),(29,.8f,false),(29,1.6f,false),(29,2.4f,false),(29,3.2f,false),(22,2f,true),(21,2f,false),(12,1.5f,true),(25,3f,false)})
                {
                    float duration=VisitorOpeningDialogue.Durations[line];SetLine(line,at/duration);stage.Evaluate(camera,line,at,duration,500+line+at);
                    BodyViews(visitorSide?stage.VisitorActor:stage.EliasActor,"body/"+line.ToString("00")+"-"+at.ToString("0.00")+(visitorSide?"-visitor":"-elias"));
                }
                // the trunk while talking: the chest's travel over a whole line (the captured takes rocked it back and forth)
                foreach(var (beat,actor,name) in new[]{(21,stage.EliasActor,"Elias"),(22,stage.VisitorActor,"visitor"),(12,stage.VisitorActor,"visitor (beat 12)")})
                {
                    float duration=VisitorOpeningDialogue.Durations[beat];var chest=new System.Collections.Generic.List<Vector3>();var tilt=new System.Collections.Generic.List<Quaternion>();
                    for(float at=.2f;at<duration-.1f;at+=.1f)
                    {SetLine(beat,at/duration);stage.Evaluate(camera,beat,at,duration,600+beat+at);chest.Add(actor.root.InverseTransformPoint(actor.spine.position));tilt.Add(Quaternion.Inverse(actor.root.rotation)*actor.spine.rotation);}
                    var mean=chest.Aggregate(Vector3.zero,(a,b)=>a+b)/chest.Count;float travel=chest.Max(c=>Vector3.Distance(c,mean))*2;float turn=tilt.Max(q=>tilt.Max(r=>Quaternion.Angle(q,r)));
                    Check(travel<.03f && turn<5,"Chest steady while "+name+" talks: travels "+(travel*100).ToString("0.0")+" cm, turns "+turn.ToString("0.0")+" deg");
                }
                // the arms while talking: how far each hand wanders in the actor's own space over a whole line
                foreach(var (beat,actor,name) in new[]{(21,stage.EliasActor,"Elias"),(22,stage.VisitorActor,"visitor"),(18,stage.VisitorActor,"visitor (beat 18)")})
                {
                    float duration=VisitorOpeningDialogue.Durations[beat];var left=new System.Collections.Generic.List<Vector3>();var right=new System.Collections.Generic.List<Vector3>();
                    for(float at=.2f;at<duration-.1f;at+=.1f)
                    {SetLine(beat,at/duration);stage.Evaluate(camera,beat,at,duration,800+beat+at);left.Add(actor.root.InverseTransformPoint(actor.leftHand.position));right.Add(actor.root.InverseTransformPoint(actor.rightHand.position));}
                    float Spread(System.Collections.Generic.List<Vector3> l){var m=l.Aggregate(Vector3.zero,(a,b)=>a+b)/l.Count;return l.Max(c=>Vector3.Distance(c,m))*2;}
                    float wander=Mathf.Max(Spread(left),Spread(right));
                    Check(wander<.08f,"Arms steady while "+name+" talks: hands wander "+(wander*100).ToString("0.0")+" cm (arm sway "+VisitorCinematicActor.ArmSway+")");
                }
                // mouths: open while speaking, shut while listening
                SetLine(14,.5f);stage.Evaluate(camera,14,VisitorOpeningDialogue.Durations[14]*.5f,VisitorOpeningDialogue.Durations[14],60);
                Check(stage.EliasActor.HasJaw && stage.VisitorActor.HasJaw,"Both actors have a jaw shape for speech");
                // lip movement in the cutscene itself: Elias in his close-up while he talks (beat 21), the visitor's mask in
                // his (beat 22); the listener's mouth stays shut. A strip of the speaker's face goes to mouth-<beat>.png.
                foreach(var (beat,speaker,listener,name) in new[]{(21,stage.EliasActor,stage.VisitorActor,"Elias"),(22,stage.VisitorActor,stage.EliasActor,"visitor")})
                {
                    float duration=VisitorOpeningDialogue.Durations[beat];var weights=new System.Collections.Generic.List<float>();float listening=0;
                    var strip=new Texture2D(8*240,240,TextureFormat.RGB24,false);
                    for(int k=0;k<8;k++)
                    {
                        float at=.4f+(duration-1.1f)*k/7f;SetLine(beat,at/duration);stage.Evaluate(camera,beat,at,duration,400+beat+at);
                        weights.Add(speaker.JawWeight);listening=Mathf.Max(listening,listener.JawWeight);
                        Face(speaker,strip,k);
                    }
                    strip.Apply();File.WriteAllBytes(Folder+"/mouth-"+name+".png",strip.EncodeToPNG());Object.DestroyImmediate(strip);
                    Check(weights.Max()>35 && weights.Min()<15 && listening<1,"Mouth moves while "+name+" talks (jaw "+string.Join(" ",weights.Select(w=>w.ToString("0")))+"), listener shut ("+listening.ToString("0")+")");
                }
                StoryDirector.Instance.Complete();CheckRestored("Completion");
                phase=3;next=EditorApplication.timeSinceStartup+.3;return;
            }
            if(phase==3)
            {
                foreach(int beat in new[]{0,9,24,30})
                {
                    Check(StoryDirector.Instance.Begin(false),"Replay for skip at beat "+beat);
                    SetLine(beat,.5f);var stage=Object.FindAnyObjectByType<VisitorCinematicStage>();stage.Evaluate(Camera.main,beat,VisitorOpeningDialogue.Durations[beat]*.5f,VisitorOpeningDialogue.Durations[beat],50);
                    StoryDirector.Instance.Complete();CheckRestored("Skip "+beat);
                }
                Check(HouseholdEconomy.Instance.Account.introSeen,"Completion flag persists");
                Check(HouseholdEconomy.Instance.Account.balance==balance && HouseholdEconomy.Instance.Account.flock==flock && !HeistGameManager.Instance.MissionActive,"Viewing/skip does not accept a heist, grant money or change birds");
                // Drive the final beat through the actual Update completion path.
                Check(StoryDirector.Instance.Begin(false),"Natural ending starts");SetLine(30,.98f);
                phase=4;next=EditorApplication.timeSinceStartup+.5;return;
            }
            if(phase==4){CheckRestored("Natural ending");Check(Object.FindAnyObjectByType<VisitorCinematicStage>()==null,"No temporary stage remains");Check(HouseholdEconomy.Instance.Account.TabletReady,"Received tablet stays with Elias outside the cinematic");DeliveryFlow();phase=5;EditorApplication.isPlaying=false;}
        }
        catch(Exception e){Check(false,e.ToString());EditorApplication.isPlaying=false;}
    }
    // The story after the conversation: the visitor only promises the tablet; a box on screen sends Elias to bed; the
    // next morning a box waits by the door, and opening it hands the tablet over (no tablet between the actors' hands).
    static void DeliveryFlow()
    {
        var economy=HouseholdEconomy.Instance;
        economy.Commit(a=>{a.introSeen=false;a.tabletWaiting=false;return true;},"Fresh opening for the delivery flow");
        Check(StoryDirector.Instance.Begin(false),"Fresh opening starts");StoryDirector.Instance.Complete();
        Check(economy.Account.introSeen && economy.Account.tabletWaiting && !economy.Account.TabletReady,"After the conversation the tablet is only promised");
        Check(SleepPrompt.IsOpen && GameMenu.BlocksInput,"A box on screen tells Elias to go to sleep");
        var box=Object.FindAnyObjectByType<TabletDeliveryBox>(FindObjectsInactive.Include);
        Check(box!=null && !box.Available && !economy.Account.TabletReady,"No box at the door and no tablet before the night passes");
        Check(economy.Account.CampaignObjective.StartsWith("Durma"),"Objective says to sleep: "+economy.Account.CampaignObjective);
        SleepPrompt.Close();Check(!GameMenu.BlocksInput,"Closing the box gives the controls back");
        economy.Commit(a=>{a.RestUntilMorning();return true;},"Next morning");
        Check(box.Available,"Next morning the box waits by the door");
        Check(economy.Account.CampaignObjective.Contains("caixa"),"Objective points to the box: "+economy.Account.CampaignObjective);
        box.SendMessage("Update");
        Check(box.Shown,"The box is on the porch: "+economy.home.InverseTransformPoint(box.InteractionPoint).ToString("F2")+" (house space)");
        var door=economy.home.GetComponentInChildren<HomeDoor>();var spot=box.InteractionPoint;
        var go=new GameObject("box view");var cam=go.AddComponent<Camera>();cam.CopyFrom(Camera.main);cam.rect=new Rect(0,0,1,1);cam.fieldOfView=50;
        cam.transform.position=spot-door.hinge.parent.forward*2.2f+Vector3.up*1.1f+door.hinge.parent.right*.6f;cam.transform.LookAt(spot+door.hinge.parent.right*-.5f+Vector3.up*.4f);
        var t=new RenderTexture(1280,720,24);cam.targetTexture=t;cam.Render();var a=RenderTexture.active;RenderTexture.active=t;var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();RenderTexture.active=a;
        File.WriteAllBytes(Folder+"/delivery-box.png",tex.EncodeToPNG());cam.targetTexture=null;Object.DestroyImmediate(t);Object.DestroyImmediate(tex);Object.DestroyImmediate(go);
        Check(box.Open() && economy.Account.TabletReady && !box.Available,"Opening the box hands the tablet over: it stays with Elias and Tab opens it");
        ProtagonistPhone.Instance.SetOpen(true);Check(ProtagonistPhone.IsOpen,"Tab opens the delivered tablet");ProtagonistPhone.Instance.SetOpen(false);
    }
    static void CheckRestored(string label)
    {
        var camera=Camera.main;
        Check(!StoryDirector.Active && !GameMenu.BlocksInput,label+": controls released");
        Check(Vector3.Distance(camera.transform.localPosition,cameraPosition)<.01f && Quaternion.Angle(camera.transform.localRotation,cameraRotation)<.1f && Mathf.Abs(camera.fieldOfView-cameraFov)<.01f && Mathf.Abs(camera.nearClipPlane-near)<.001f,label+": player camera restored");
        Check(Quaternion.Angle(door.hinge.localRotation,doorRotation)<.1f && door.enabled==doorEnabled,label+": original door restored");
    }
    // Held like a book: thumbs on the face towards the holder (near), fingers past the far face, nothing inside.
    // the speaker's face up close, lit from the front, into one cell of a strip
    static void Face(VisitorCinematicActor actor,Texture2D strip,int cell)
    {
        var head=actor.head;var go=new GameObject("face close-up");var cam=go.AddComponent<Camera>();cam.CopyFrom(Camera.main);cam.rect=new Rect(0,0,1,1);cam.fieldOfView=24;cam.nearClipPlane=.02f;
        var lamp=new GameObject("face lamp").AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=1.2f;lamp.shadows=LightShadows.None;
        var look=head.position+actor.root.forward*.06f+Vector3.up*.02f;cam.transform.position=look+actor.root.forward*.55f+Vector3.up*.03f;cam.transform.LookAt(look);
        lamp.transform.rotation=Quaternion.LookRotation(-actor.root.forward+Vector3.down*.4f);
        var target=new RenderTexture(240,240,24);cam.targetTexture=target;cam.Render();var active=RenderTexture.active;RenderTexture.active=target;
        strip.ReadPixels(new Rect(0,0,240,240),cell*240,0);RenderTexture.active=active;cam.targetTexture=null;
        Object.DestroyImmediate(target);Object.DestroyImmediate(go);Object.DestroyImmediate(lamp.gameObject);
    }
    static void Wrists(VisitorCinematicStage stage,string label)
    {
        var e=stage.EliasActor;var v=stage.VisitorActor;
        string Arm(VisitorCinematicActor a,bool right){var arm=a.Arm(right);return (right?"R":"L")+$" flex {arm.Flexion:0} dev {arm.Deviation:0} roll {arm.Roll:0} forearm {arm.ForearmRoll:0}";}
        File.AppendAllText(Folder+"/wrists.txt",$"{label}: Elias load {e.WristLoad:0.00} contact {e.ContactError:0.000} [{Arm(e,false)} | {Arm(e,true)}]  visitor load {v.WristLoad:0.00} contact {v.ContactError:0.000} [{Arm(v,false)} | {Arm(v,true)}]\n");
    }
    // Both hands of an actor up close, each from the back of the hand, the thumb side and the palm side, lit by a lamp
    // at the camera (the night scene is too dark to judge a grip): wrist, fingers and what they hold in one sheet.
    // The whole actor alone (everything else culled), from the front, both sides and the back, lit evenly: arms against
    // the body (an arm sunk into the chest), the clothes, the stance.
    static void BodyViews(VisitorCinematicActor actor,string name)
    {
        const int Layer=31;var renderers=actor.root.GetComponentsInChildren<Renderer>(true);var layers=renderers.Select(r=>r.gameObject.layer).ToArray();
        foreach(var r in renderers)r.gameObject.layer=Layer;
        var go=new GameObject("body view");var cam=go.AddComponent<Camera>();cam.rect=new Rect(0,0,1,1);cam.fieldOfView=30;cam.nearClipPlane=.05f;
        cam.cullingMask=1<<Layer;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.42f,.45f,.5f);
        var lamp=new GameObject("body lamp").AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=1.3f;lamp.shadows=LightShadows.None;lamp.cullingMask=1<<Layer;
        var target=new RenderTexture(400,560,24);cam.targetTexture=target;var sheet=new Texture2D(1600,560,TextureFormat.RGB24,false);
        var look=actor.spine.position+Vector3.up*.05f;
        for(int v=0;v<4;v++)
        {
            var dir=Quaternion.AngleAxis(v*90-15,Vector3.up)*actor.root.forward;cam.transform.position=look+dir*2.3f+Vector3.up*.25f;cam.transform.LookAt(look+Vector3.down*.15f);
            lamp.transform.rotation=Quaternion.LookRotation(-dir+Vector3.down*.4f);
            cam.Render();var active=RenderTexture.active;RenderTexture.active=target;sheet.ReadPixels(new Rect(0,0,400,560),v*400,0);RenderTexture.active=active;
        }
        sheet.Apply();File.WriteAllBytes(Folder+"/"+name+".png",sheet.EncodeToPNG());cam.targetTexture=null;
        for(int i=0;i<renderers.Length;i++)renderers[i].gameObject.layer=layers[i];
        Object.DestroyImmediate(target);Object.DestroyImmediate(sheet);Object.DestroyImmediate(go);Object.DestroyImmediate(lamp.gameObject);
    }
    static void HandCloseUps(VisitorCinematicActor actor,string name)
    {
        var main=Camera.main;var go=new GameObject("hand close-up");var cam=go.AddComponent<Camera>();cam.CopyFrom(main);cam.rect=new Rect(0,0,1,1);cam.fieldOfView=34;cam.nearClipPlane=.02f;
        var lamp=new GameObject("close-up lamp").AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=1.5f;lamp.shadows=LightShadows.None;
        var target=new RenderTexture(480,400,24);cam.targetTexture=target;var sheet=new Texture2D(1440,800,TextureFormat.RGB24,false);
        for(int row=0;row<2;row++)
        {
            bool right=row==1;var hand=right?actor.rightHand:actor.leftHand;
            Vector3 palm=actor.Palm(right),fingers=actor.Fingers(right),thumb=Vector3.Cross(palm,fingers)*(right?-1:1);
            var middle=hand.Find("Middle2"+(right?"R":"L"));var look=Vector3.Lerp(hand.position,middle!=null?middle.position:hand.position,.35f);
            var views=new[]{-palm+fingers*.15f,thumb-palm*.25f,palm+fingers*.15f};
            for(int v=0;v<3;v++)
            {
                var dir=views[v].normalized;cam.transform.position=look+dir*.36f;cam.transform.LookAt(look,Vector3.Cross(dir,Vector3.Cross(Vector3.up,dir)).sqrMagnitude>1e-4f?Vector3.up:fingers);
                lamp.transform.rotation=Quaternion.LookRotation(-dir+Vector3.down*.3f);
                cam.Render();var active=RenderTexture.active;RenderTexture.active=target;sheet.ReadPixels(new Rect(0,0,480,400),v*480,(1-row)*400);RenderTexture.active=active;
            }
        }
        sheet.Apply();File.WriteAllBytes(Folder+"/"+name+".png",sheet.EncodeToPNG());cam.targetTexture=null;
        Object.DestroyImmediate(target);Object.DestroyImmediate(sheet);Object.DestroyImmediate(go);Object.DestroyImmediate(lamp.gameObject);
    }
    // Share of the picture (the shot's letterboxed viewport) the given renderers cover, as drawn from that camera on
    // their own (anything in front of them is not counted out: a conservative measure).
    static float Share(Camera camera,List<Renderer> marked)
    {
        if(marked.Count==0)return 0;
        const int Layer=29;var layers=marked.ToDictionary(r=>r,r=>r.gameObject.layer);var materials=marked.ToDictionary(r=>r,r=>r.sharedMaterials);
        // flat white whatever the light: a door in shadow at night must count as much as a lit one
        var white=new Material(Shader.Find("Universal Render Pipeline/Unlit"));white.SetColor("_BaseColor",Color.white);
        foreach(var r in marked){r.gameObject.layer=Layer;r.sharedMaterials=r.sharedMaterials.Select(_=>white).ToArray();}
        var go=new GameObject("framing probe");var cam=go.AddComponent<Camera>();cam.CopyFrom(camera);
        cam.cullingMask=1<<Layer;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=Color.black;
        var target=new RenderTexture(1280,720,24);cam.targetTexture=target;cam.aspect=1280f/(720*camera.rect.height);cam.Render();
        var active=RenderTexture.active;RenderTexture.active=target;var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();RenderTexture.active=active;
        int y0=Mathf.RoundToInt(720*camera.rect.y),y1=Mathf.RoundToInt(720*(camera.rect.y+camera.rect.height));int hit=0,all=0;var px=tex.GetPixels32();
        for(int y=y0;y<y1;y++)for(int x=0;x<1280;x++){var c=px[y*1280+x];all++;if(c.r+c.g+c.b>24)hit++;}
        foreach(var kv in layers)if(kv.Key!=null){kv.Key.gameObject.layer=kv.Value;kv.Key.sharedMaterials=materials[kv.Key];}
        Object.DestroyImmediate(tex);Object.DestroyImmediate(target);Object.DestroyImmediate(go);Object.DestroyImmediate(white);
        return all>0?(float)hit/all:0;
    }
    // A speaking head's jaw ("Fala") shut and fully open, through the shot's camera: how far the vertex that moves most
    // travels on a 1280x720 picture, and the two frames side by side.
    static void Mouth(Transform actor,Camera camera,string who,string capture)
    {
        var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>().Where(x=>x.sharedMesh!=null && x.sharedMesh.GetBlendShapeIndex("Fala")>=0).ToList();
        if(skins.Count==0){Check(false,who+" has no speech shape");return;}
        float most=0;var weights=skins.Select(x=>x.GetBlendShapeWeight(x.sharedMesh.GetBlendShapeIndex("Fala"))).ToArray();
        float aspect=camera.aspect;camera.aspect=1280f/(720*camera.rect.height);
        foreach(var skin in skins)
        {
            int k=skin.sharedMesh.GetBlendShapeIndex("Fala");var shut=new Mesh();var open=new Mesh();
            skin.SetBlendShapeWeight(k,0);skin.BakeMesh(shut,true);skin.SetBlendShapeWeight(k,100);skin.BakeMesh(open,true);
            var a=shut.vertices;var b=open.vertices;
            for(int i=0;i<a.Length;i++)
            {
                var pa=camera.WorldToViewportPoint(skin.transform.TransformPoint(a[i]));var pb=camera.WorldToViewportPoint(skin.transform.TransformPoint(b[i]));
                if(pa.z<=0 || pa.x<0 || pa.x>1 || pa.y<0 || pa.y>1)continue;
                most=Mathf.Max(most,new Vector2((pa.x-pb.x)*1280,(pa.y-pb.y)*720*camera.rect.height).magnitude);
            }
            Object.DestroyImmediate(shut);Object.DestroyImmediate(open);
        }
        camera.aspect=aspect;
        foreach(var skin in skins)skin.SetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("Fala"),0);Capture(capture+"-shut");
        foreach(var skin in skins)skin.SetBlendShapeWeight(skin.sharedMesh.GetBlendShapeIndex("Fala"),100);Capture(capture+"-open");
        for(int i=0;i<skins.Count;i++)skins[i].SetBlendShapeWeight(skins[i].sharedMesh.GetBlendShapeIndex("Fala"),weights[i]);
        Check(most>=10,"The "+who+"'s mouth moves visibly in his close-up: at full voice the jaw moves "+most.ToString("0")+" px on a 1280x720 picture");
    }
    // Is the hand on screen in the shot, and clear of the actor's own trunk? The trunk is taken as a 0.42 m wide column
    // round the chest bone, from 0.45 m below it to 0.15 m above; a hand behind it (farther from the camera) is hidden.
    static bool HandInShot(Camera camera,VisitorCinematicActor actor,bool right,out string where)
    {
        float aspect=camera.aspect;camera.aspect=1280f/(720*camera.rect.height);
        var hand=right?actor.rightHand:actor.leftHand;var v=camera.WorldToViewportPoint(hand.position);var c=camera.WorldToViewportPoint(actor.spine.position);
        float tan=Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad),perX=1/(2*Mathf.Max(.05f,c.z)*tan*camera.aspect),perY=1/(2*Mathf.Max(.05f,c.z)*tan);
        bool inFrame=v.z>0 && v.x>.04f && v.x<.96f && v.y>.04f && v.y<.96f;
        bool behind=v.z>c.z+.04f && Mathf.Abs(v.x-c.x)<.21f*perX && v.y<c.y+.15f*perY && v.y>c.y-.45f*perY;
        camera.aspect=aspect;
        where="at ("+v.x.ToString("0.00")+", "+v.y.ToString("0.00")+") of the picture, "+(!inFrame?"out of frame":behind?"behind his body":"in view");
        return inFrame && !behind;
    }
    // The mouth through a whole spoken line, frame by frame as the cinematic plays it: how far the chin (the vertex the
    // jaw moves most) travels on a 1280x720 picture between its highest and lowest point.
    static void MouthAlong(VisitorCinematicStage stage,Camera camera,int line,Transform actor,string who)
    {
        var skin=actor.GetComponentsInChildren<SkinnedMeshRenderer>().FirstOrDefault(x=>x.sharedMesh!=null && x.sharedMesh.GetBlendShapeIndex("Fala")>=0);
        if(skin==null){Check(false,who+" has no speech shape");return;}
        int k=skin.sharedMesh.GetBlendShapeIndex("Fala");float keep=skin.GetBlendShapeWeight(k);
        var shut=new Mesh();var open=new Mesh();skin.SetBlendShapeWeight(k,0);skin.BakeMesh(shut,true);skin.SetBlendShapeWeight(k,100);skin.BakeMesh(open,true);skin.SetBlendShapeWeight(k,keep);
        var a=shut.vertices;var b=open.vertices;int chin=0;float most=-1;for(int i=0;i<a.Length;i++){float d=(b[i]-a[i]).sqrMagnitude;if(d>most){most=d;chin=i;}}
        float d0=VisitorOpeningDialogue.Durations[line],low=float.PositiveInfinity,high=float.NegativeInfinity;var baked=new Mesh();int frames=0;
        float aspect=camera.aspect;
        for(float t=.1f;t<d0-.05f;t+=.1f)
        {
            SetLine(line,t/d0);stage.Evaluate(camera,line,t,d0,400+t);camera.aspect=1280f/(720*camera.rect.height);
            skin.BakeMesh(baked,true);var y=camera.WorldToViewportPoint(skin.transform.TransformPoint(baked.vertices[chin])).y*720*camera.rect.height;
            low=Mathf.Min(low,y);high=Mathf.Max(high,y);frames++;
        }
        camera.aspect=aspect;Object.DestroyImmediate(shut);Object.DestroyImmediate(open);Object.DestroyImmediate(baked);
        Check(high-low>=8,"The "+who+"'s mouth keeps moving through line "+line+" as it plays: the chin travels "+(high-low).ToString("0")+" px over "+frames+" frames");
    }
    static void Capture(string name)
    {
        var camera=Camera.main;var old=camera.targetTexture;var active=RenderTexture.active;float aspect=camera.aspect;
        var target=new RenderTexture(1280,720,24);camera.targetTexture=target;camera.aspect=1280f/(720*camera.rect.height);camera.Render();RenderTexture.active=target;
        var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
        File.WriteAllBytes(Folder+"/"+name+".png",texture.EncodeToPNG());camera.targetTexture=old;camera.aspect=aspect;RenderTexture.active=active;
        Object.DestroyImmediate(target);Object.DestroyImmediate(texture);
    }
    public static void Build()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        if(Resources.Load<GameObject>("Cinematics/VisitorStage")==null)throw new InvalidOperationException("Install visitor stage first");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{RuralWorldReview.WorldScene},locationPathName="E:/Jogo3D/Build/ChickenHeist.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
        File.WriteAllText(Folder+"/build.txt",report.summary.result+"\nErrors: "+report.summary.totalErrors+"\nDuration: "+report.summary.totalTime);
        EditorApplication.Exit(report.summary.result==BuildResult.Succeeded?0:1);
    }
}
