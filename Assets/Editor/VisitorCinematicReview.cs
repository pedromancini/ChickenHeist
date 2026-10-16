using System;
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
                    "Hooded visitor is the supplied c12cb2ea model rigged with arms, wrists and fingers: "+(visitorBody!=null?visitorBody.bones.Length+" bones":"missing"));
                Check(visitorBody!=null && visitorBody.sharedMaterial!=null && visitorBody.sharedMaterial.GetTexture("_BaseMap")!=null,"Hooded visitor keeps its painted texture");
                File.WriteAllText(Folder+"/wrists.txt","beat: wrist load (1 = at the joint's limit) and contact error, Elias | visitor\n");
                Check(stage.GetComponentsInChildren<Light>().Count(l=>l.shadows!=UnityEngine.LightShadows.None)<=1,"Only key light casts additional shadows");
                float total=0;
                for(int line=0;line<VisitorOpeningDialogue.Text.Length;line++)
                {
                    SetLine(line,.5f);float duration=VisitorOpeningDialogue.Durations[line];
                    stage.Evaluate(camera,line,duration*.5f,duration,total+duration*.5f);
                    Check(!float.IsNaN(camera.transform.position.x) && !float.IsNaN(stage.EliasHand.x),"Finite camera and actor pose at beat "+line);
                    if(new[]{0,1,2,5,8,9,12,14,19,24,28,29,30}.Contains(line))Capture("shot-"+line.ToString("00"));
                    if(line==25)Check(stage.GripError<.16f,"Hands contact tablet edges within tolerance: "+stage.GripError);
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
                // the moments the hands are busy, up close: typing, knocking, the lever, holding and passing the tablet
                foreach(var (line,at,visitorSide,name) in new[]{(0,.85f,false,"desk"),(1,1.43f,true,"knock"),(5,.9f,false,"lever"),(9,2.4f,true,"tablet-shown"),(12,1.5f,true,"tablet-held"),
                    (19,1.4f,false,"pointing"),(24,1.2f,false,"handover"),(24,1.2f,true,"handover"),(25,3f,false,"tablet-read"),(29,1.6f,false,"door-close")})
                {
                    SetLine(line,at/VisitorOpeningDialogue.Durations[line]);stage.Evaluate(camera,line,at,VisitorOpeningDialogue.Durations[line],200+line+at);
                    if(!visitorSide)Capture("moment-"+name);
                    HandCloseUps(visitorSide?stage.VisitorActor:stage.EliasActor,"hands-"+name+(visitorSide?"-visitor":"-elias"));
                    Wrists(stage,name+" (beat "+line+" at "+at+" s)");
                    if(name=="knock")Check(stage.VisitorActor.ContactError<.03f,"Knuckles meet the door on the knock: "+stage.VisitorActor.ContactError.ToString("F3"));
                    if(name=="tablet-held")Check(stage.VisitorActor.ContactError<.03f,"Visitor's palms on the tablet edges: "+stage.VisitorActor.ContactError.ToString("F3"));
                    if(name=="tablet-held" || name=="tablet-read")TabletFingers(stage,name=="tablet-held"?stage.VisitorActor:stage.EliasActor,name,name=="tablet-held"?stage.Tablet.forward:-stage.Tablet.forward);
                    if(name=="tablet-held" || name=="tablet-read" || name=="handover")
                    {
                        if(!visitorSide || name!="handover")TabletViews(stage,"tablet-"+name);
                        var (n,d)=InTablet(stage,visitorSide?stage.VisitorActor:stage.EliasActor);
                        foreach(bool right in new[]{false,true})
                        {
                            var who=visitorSide?stage.VisitorActor:stage.EliasActor;float g=who.Hand(right).PalmGap(who.Plate,out var deep);
                            File.AppendAllText(Folder+"/wrists.txt",name+(visitorSide?" visitor ":" Elias ")+(right?"R":"L")+" palm gap "+(g*1000).ToString("0.0")+" mm at tablet-local "+stage.Tablet.InverseTransformPoint(deep).ToString("F3")+" hand at "+stage.Tablet.InverseTransformPoint(who.Hand(right).hand.position).ToString("F3")+" trace "+who.Trace+"\n");
                        }
                        Check(d<.0015f,"No skin inside the tablet at "+name+(visitorSide?" (visitor)":" (Elias)")+": "+n+" vertices, "+(d*1000).ToString("0.0")+" mm deep (skin contact under 1.5 mm) "+inTabletBones);
                    }
                    if(name=="tablet-read")Check(stage.EliasActor.ContactError<.03f,"Elias' palms on the tablet edges: "+stage.EliasActor.ContactError.ToString("F3"));
                    Check(stage.EliasActor.WristLoad<=1.001f && stage.VisitorActor.WristLoad<=1.001f,"Wrists within their range at "+name);
                }
                // every beat the tablet is in someone's hands: no finger or palm through its case
                {
                    int worst=0;float deepest=0;string where="";
                    foreach(int line in new[]{10,12,14,16,18,20,22,23,24,25,26,27,28})
                    {
                        float duration=VisitorOpeningDialogue.Durations[line];
                        foreach(float f in new[]{.2f,.6f,.95f})
                        {
                            SetLine(line,f);stage.Evaluate(camera,line,duration*f,duration,700+line+f);
                            foreach(var actor in new[]{stage.EliasActor,stage.VisitorActor})
                            {var (n,d)=InTablet(stage,actor);if(n>worst || d>deepest){where="beat "+line+" at "+f.ToString("0.00")+(actor==stage.VisitorActor?" visitor":" Elias");}worst=Mathf.Max(worst,n);deepest=Mathf.Max(deepest,d);}
                        }
                    }
                    Check(deepest<.0015f,"Hands never through the tablet while it is held (skin contact under 1.5 mm): worst "+worst+" vertices, "+(deepest*1000).ToString("0.0")+" mm deep ("+where+")");
                }
                // the door swings out through the porch: it must clear the visitor standing there
                if(stage.Door!=null)
                {
                    float clearance=float.PositiveInfinity;var hinge=stage.Door.hinge;
                    for(float t=0;t<=VisitorOpeningDialogue.Durations[5];t+=.1f)
                    {
                        SetLine(5,t/VisitorOpeningDialogue.Durations[5]);stage.Evaluate(camera,5,t,VisitorOpeningDialogue.Durations[5],300+t);
                        Vector3 a=hinge.position,b=hinge.TransformPoint(new Vector3(1.47f,0,0));Vector3 v=stage.visitor.position;
                        Vector2 A=new Vector2(a.x,a.z),B=new Vector2(b.x,b.z),V=new Vector2(v.x,v.z);
                        float u=Mathf.Clamp01(Vector2.Dot(V-A,B-A)/(B-A).sqrMagnitude);clearance=Mathf.Min(clearance,Vector2.Distance(A+(B-A)*u,V));
                    }
                    Check(clearance>.28f,"Opening door clears the visitor on the porch: "+clearance.ToString("F2")+" m from his centre");
                }
                stage.Evaluate(camera,28,.8f,6.5f,120);var before=stage.visitor.position;
                stage.Evaluate(camera,28,4.5f,6.5f,124);Check(Vector3.Distance(before,stage.visitor.position)>2,"Visitor physically walks away");
                stage.Evaluate(camera,29,.3f,8.5f,125);before=stage.elias.position;
                stage.Evaluate(camera,29,7.8f,8.5f,132);Check(Vector3.Distance(before,stage.elias.position)>2,"Elias physically returns to table");
                // shutting the door: his hand stays on the inner lever while he walks back in pulling it, and he ends inside
                SetLine(29,.3f);stage.Evaluate(camera,29,2.4f,8.5f,127);
                Check(stage.EliasActor.ContactError<.05f,"Elias' hand stays on the inner lever while he pulls the door shut: "+stage.EliasActor.ContactError.ToString("F3"));
                // the lever in front of his left shoulder (x < 0 in his own space), not across his body
                foreach(var at in new[]{1.6f,2.4f,3.0f})
                {
                    SetLine(29,at/8.5f);stage.Evaluate(camera,29,at,8.5f,127+at);var lever=stage.elias.InverseTransformPoint(stage.Door.hinge.TransformPoint(new Vector3(1.30f,1.06f,.10f)));
                    Check(lever.x<-.06f && lever.z>.2f,"Door lever in front of Elias' left shoulder at "+at+" s (his space: x "+lever.x.ToString("F2")+", z "+lever.z.ToString("F2")+")");
                }
                SetLine(29,.42f);stage.Evaluate(camera,29,3.6f,8.5f,128);
                Check(Quaternion.Angle(stage.Door.hinge.localRotation,Quaternion.identity)<1 && Vector3.Dot(stage.elias.position-stage.Door.hinge.position,stage.Door.hinge.parent.forward)>.3f,"Door shut with Elias inside the house");
                foreach(var at in new[]{.6f,1.4f,2.0f,2.6f,3.2f,3.8f}){SetLine(29,at/8.5f);stage.Evaluate(camera,29,at,8.5f,130+at);Capture("door-close-"+at.ToString("0.0"));}
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
                foreach(var (beat,actor,name) in new[]{(21,stage.EliasActor,"Elias"),(22,stage.VisitorActor,"visitor"),(12,stage.VisitorActor,"visitor holding the tablet")})
                {
                    float duration=VisitorOpeningDialogue.Durations[beat];var chest=new System.Collections.Generic.List<Vector3>();var tilt=new System.Collections.Generic.List<Quaternion>();
                    for(float at=.2f;at<duration-.1f;at+=.1f)
                    {SetLine(beat,at/duration);stage.Evaluate(camera,beat,at,duration,600+beat+at);chest.Add(actor.root.InverseTransformPoint(actor.spine.position));tilt.Add(Quaternion.Inverse(actor.root.rotation)*actor.spine.rotation);}
                    var mean=chest.Aggregate(Vector3.zero,(a,b)=>a+b)/chest.Count;float travel=chest.Max(c=>Vector3.Distance(c,mean))*2;float turn=tilt.Max(q=>tilt.Max(r=>Quaternion.Angle(q,r)));
                    Check(travel<.03f && turn<5,"Chest steady while "+name+" talks: travels "+(travel*100).ToString("0.0")+" cm, turns "+turn.ToString("0.0")+" deg");
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
            if(phase==4){CheckRestored("Natural ending");Check(Object.FindAnyObjectByType<VisitorCinematicStage>()==null,"No temporary stage remains");Check(Object.FindAnyObjectByType<ReceivedTabletDock>()?.Available==true,"Received tablet persists outside cinematic");phase=5;EditorApplication.isPlaying=false;}
        }
        catch(Exception e){Check(false,e.ToString());EditorApplication.isPlaying=false;}
    }
    static void CheckRestored(string label)
    {
        var camera=Camera.main;
        Check(!StoryDirector.Active && !GameMenu.BlocksInput,label+": controls released");
        Check(Vector3.Distance(camera.transform.localPosition,cameraPosition)<.01f && Quaternion.Angle(camera.transform.localRotation,cameraRotation)<.1f && Mathf.Abs(camera.fieldOfView-cameraFov)<.01f && Mathf.Abs(camera.nearClipPlane-near)<.001f,label+": player camera restored");
        Check(Quaternion.Angle(door.hinge.localRotation,doorRotation)<.1f && door.enabled==doorEnabled,label+": original door restored");
    }
    // Held like a book: thumbs on the face towards the holder (near), fingers past the far face, nothing inside.
    // Skin of the actor inside the tablet's case (deeper than half a millimetre): how many vertices and how deep.
    static string inTabletBones="";
    static (int count,float depth) InTablet(VisitorCinematicStage stage,VisitorCinematicActor actor)
    {
        var box=stage.Tablet.worldToLocalMatrix;var half=new Vector3(.245f,.155f,.014f);int count=0;float depth=0;
        var byBone=new System.Collections.Generic.Dictionary<string,int>();
        foreach(var skin in actor.root.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var mesh=skin.sharedMesh;if(mesh==null || !mesh.isReadable)continue;
            var vertices=mesh.vertices;var weights=mesh.boneWeights;var poses=mesh.bindposes;var bones=skin.bones;
            var m=new Matrix4x4[bones.Length];for(int i=0;i<bones.Length;i++)m[i]=bones[i]!=null?bones[i].localToWorldMatrix*poses[i]:Matrix4x4.zero;
            for(int v=0;v<vertices.Length;v++)
            {
                var w=weights[v];var x=vertices[v];
                var p=m[w.boneIndex0].MultiplyPoint3x4(x)*w.weight0+m[w.boneIndex1].MultiplyPoint3x4(x)*w.weight1+m[w.boneIndex2].MultiplyPoint3x4(x)*w.weight2+m[w.boneIndex3].MultiplyPoint3x4(x)*w.weight3;
                float d=CinematicHand.BoxDistance(box,half,p);
                if(d<-.0005f){count++;depth=Mathf.Max(depth,-d);var bn=bones[w.boneIndex0]!=null?bones[w.boneIndex0].name:"?";byBone[bn]=(byBone.TryGetValue(bn,out var c)?c:0)+1;}
            }
        }
        inTabletBones=string.Join(" ",byBone.OrderByDescending(kv=>kv.Value).Select(kv=>kv.Key+":"+kv.Value));
        return (count,depth);
    }
    static void TabletFingers(VisitorCinematicStage stage,VisitorCinematicActor actor,string label,Vector3 near)
    {
        var tablet=stage.Tablet;Vector3 screen=near;string line=label+":";bool ok=true;
        for(int side=0;side<2;side++)
        {
            var hand=actor.Hand(side==1);
            for(int f=0;f<5;f++)
            {
                var local=tablet.InverseTransformPoint(hand.Tip(f));float face=Vector3.Dot(hand.Tip(f)-tablet.position,screen);
                bool inside=Mathf.Abs(local.x)<.245f && Mathf.Abs(local.y)<.155f && Mathf.Abs(face)<.014f;
                // a finger tip may rest on the edge itself (within the thickness), never on the holder's face
                bool right=f==0?face>0:face<.014f;ok&=right && !inside;
                float knuckle=Vector3.Dot(hand.Knuckle(f)-tablet.position,screen);
                line+=$" {(side==0?"L":"R")}{CinematicHand.Names[f][0]} {face*100:+0.0;-0.0}cm(k{knuckle*100:+0.0;-0.0}){(inside?" INSIDE":"")}";
            }
        }
        File.AppendAllText(Folder+"/wrists.txt",line+"\n");
        Check(ok,"Thumbs on the holder's side of the tablet, fingers over the far side, none inside ("+label+")");
    }
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
    // The tablet in someone's hands from four sides (front, back, above, below its edge), lit evenly: how the hands sit on it.
    static void TabletViews(VisitorCinematicStage stage,string name)
    {
        var t=stage.Tablet;var go=new GameObject("tablet view");var cam=go.AddComponent<Camera>();cam.CopyFrom(Camera.main);cam.rect=new Rect(0,0,1,1);cam.fieldOfView=40;cam.nearClipPlane=.02f;
        var lamp=new GameObject("tablet lamp").AddComponent<Light>();lamp.type=LightType.Directional;lamp.intensity=1.4f;lamp.shadows=LightShadows.None;
        var target=new RenderTexture(480,360,24);cam.targetTexture=target;var sheet=new Texture2D(1920,360,TextureFormat.RGB24,false);
        var dirs=new[]{-t.forward,t.forward,t.up*.9f-t.forward*.4f,-t.up*.9f+t.forward*.4f};
        for(int v=0;v<4;v++)
        {
            var d=dirs[v].normalized;cam.transform.position=t.position+d*.75f;cam.transform.LookAt(t.position,Mathf.Abs(Vector3.Dot(d,t.up))>.8f?t.forward:t.up);
            lamp.transform.rotation=Quaternion.LookRotation(-d+Vector3.down*.3f);
            cam.Render();var active=RenderTexture.active;RenderTexture.active=target;sheet.ReadPixels(new Rect(0,0,480,360),v*480,0);RenderTexture.active=active;
        }
        sheet.Apply();File.WriteAllBytes(Folder+"/"+name+".png",sheet.EncodeToPNG());cam.targetTexture=null;
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
