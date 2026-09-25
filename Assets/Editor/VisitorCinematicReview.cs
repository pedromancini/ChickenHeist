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
        stage.visitor=VillagerNPCFactory.Create(root.transform,"Visitante","peasant_2").transform;
        foreach(var actor in new[]{stage.elias,stage.visitor})
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
                Check(stage.visitor.GetComponentsInChildren<SkinnedMeshRenderer>().Any(s=>s.name=="Pele articulada do visitante" && s.bones.Length==15),"Supplied visitor has full-body skinning");
                var supplied=stage.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Visitante encapuzado c12cb2ea fornecido");
                Check(supplied!=null && supplied.GetComponentsInChildren<Renderer>(true).Length>0,"Visitor uses supplied c12cb2ea hooded character mesh with a visible renderer");
                Check(stage.GetComponentsInChildren<Light>().Count(l=>l.shadows!=UnityEngine.LightShadows.None)<=1,"Only key light casts additional shadows");
                float total=0;
                for(int line=0;line<VisitorOpeningDialogue.Text.Length;line++)
                {
                    SetLine(line,.5f);float duration=VisitorOpeningDialogue.Durations[line];
                    stage.Evaluate(camera,line,duration*.5f,duration,total+duration*.5f);
                    Check(!float.IsNaN(camera.transform.position.x) && !float.IsNaN(stage.EliasHand.x),"Finite camera and actor pose at beat "+line);
                    if(new[]{0,1,2,5,8,9,12,14,19,24,28,29,30}.Contains(line))Capture("shot-"+line.ToString("00"));
                    if(line==25)Check(stage.GripError<.16f,"Hands contact tablet edges within tolerance: "+stage.GripError);
                    total+=duration;
                }
                stage.Evaluate(camera,28,.8f,6.5f,120);var before=stage.visitor.position;
                stage.Evaluate(camera,28,4.5f,6.5f,124);Check(Vector3.Distance(before,stage.visitor.position)>2,"Visitor physically walks away");
                stage.Evaluate(camera,29,.3f,8.5f,125);before=stage.elias.position;
                stage.Evaluate(camera,29,7.8f,8.5f,132);Check(Vector3.Distance(before,stage.elias.position)>2,"Elias physically returns to table");
                stage.Evaluate(camera,29,1.0f,8.5f,126);before=stage.elias.position;
                stage.Evaluate(camera,29,2.2f,8.5f,127);Check(Vector3.Distance(before,stage.elias.position)<.01f,"Elias stays at door throughout closing action");
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
