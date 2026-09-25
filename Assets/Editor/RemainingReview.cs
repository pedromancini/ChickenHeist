using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class RemainingReview
{
    const string Key="RemainingReview",Folder="output/remaining-review";
    static readonly List<string> results=new List<string>();static bool failed;static int phase;static float next;
    static HeistGameManager game;static HouseholdEconomy economy;static GameCheckpoint checkpoint;static GameCheckpointData baseline;
    static FarmAnimalMotion bird,cow;static Vector3 cameraPosition;static Quaternion cameraRotation;
    static RemainingReview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false) && (t==LogType.Error || t==LogType.Exception)){Check(false,m);EditorApplication.isPlaying=false;}};}
    public static void Run()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var e=Object.FindAnyObjectByType<HouseholdEconomy>();e.editorTestSavePath=Path.GetFullPath("Temp/remaining-"+Guid.NewGuid().ToString("N")+".json");HouseholdEconomy.SaveAccount(e.editorTestSavePath,new HouseholdAccount());
        if(!Application.isBatchMode){var view=EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));view.Show();view.Focus();}
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){results.Clear();failed=false;phase=0;Application.runInBackground=true;next=Time.realtimeSinceStartup+3;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);EditorApplication.Exit(failed?1:0);}
    }
    static void Check(bool value,string message){failed|=!value;results.Add((value?"PASS ":"FAIL ")+message);File.WriteAllLines(Folder+"/checks.txt",results);}
    static void Capture(string name,Vector3 from,Vector3 target)
    {
        var go=new GameObject("Remaining review eye");var eye=go.AddComponent<Camera>();eye.CopyFrom(Camera.main);eye.transform.position=from;eye.transform.LookAt(target);eye.fieldOfView=55;eye.cullingMask=~0;
        var rt=new RenderTexture(1400,900,24);eye.targetTexture=rt;eye.Render();var old=RenderTexture.active;RenderTexture.active=rt;
        var texture=new Texture2D(1400,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1400,900),0,0);texture.Apply();File.WriteAllBytes(Folder+"/"+name+".png",texture.EncodeToPNG());
        RenderTexture.active=old;eye.targetTexture=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(rt);Object.DestroyImmediate(go);
    }
    static void Logic()
    {
        Check(StoryDialogue.openingFiles.Length==12 && StoryDialogue.declineFiles.Length==12,"Dialogue has 24 separately named voice cues");
        Check(Resources.Load<Texture2D>("Cinematics/opening")!=null && Resources.Load<Texture2D>("Cinematics/decline")!=null,"Both cinematic illustrations are integrated");
        var preferences=new GamePreferences();
        Check(preferences.frameLimit==144 && !preferences.vsync,"New settings default to 144 FPS without refresh-rate cap");
        preferences.frameLimit=60;preferences.vsync=true;preferences.UpgradePerformance();
        Check(preferences.frameLimit==144 && !preferences.vsync,"Existing settings migrate once to requested 144 FPS target");
        preferences.frameLimit=120;preferences.vsync=true;preferences.UpgradePerformance();
        Check(preferences.frameLimit==120 && preferences.vsync,"Later player frame-rate choice survives migration");
        var account=new HouseholdAccount{boards=6,flock=3};int balance=account.balance;
        for(int level=1;level<=3;level++)Check(account.Repair() && account.CoopLevel==level,"Upgrade advances exactly one level "+level);
        Check(account.boards==0 && account.balance==balance && !account.Repair(),"Upgrade consumes 1+2+3 kits once and has a final level");
        account=new HouseholdAccount{boards=0,repairs=1,flock=4};Check(!account.Repair() && account.repairs==1 && account.flock==4,"Missing materials preserve acquired level and birds");
        string legacy="{\"version\":1,\"day\":2,\"balance\":95,\"feed\":0,\"boards\":3,\"flock\":2,\"meals\":0,\"repairs\":2,\"truckCages\":1,\"debts\":[],\"ledger\":[]}";
        account=JsonUtility.FromJson<HouseholdAccount>(legacy);Check(account.IsValid() && account.CoopLevel==2 && account.boards==3,"Legacy repairs migrate without charging acquired upgrades");
        account=new HouseholdAccount();account.RestUntilMorning();Check(!account.pendingVision && !account.newsUnread,"Peaceful night produces no crime scene or news");
        account.RegisterRaid("Fazenda A",2,false);account.RegisterRaid("Fazenda B",1,false);account.RestUntilMorning();
        Check(account.pendingVision && account.newsUnread && account.news.Count==2 && account.pendingRaids.Count==0,"Multiple raids produce one pending vision and corresponding news");
        account=JsonUtility.FromJson<HouseholdAccount>(JsonUtility.ToJson(account));Check(account.pendingVision,"Interrupted transition persists pending vision");
        account.pendingVision=false;account.newsUnread=false;account.RestUntilMorning();Check(!account.pendingVision && !account.newsUnread && account.news.Count==2,"Processed crimes do not replay on a peaceful night");
        account.RegisterRaid("Fazenda A",1,false);account.RestUntilMorning();Check(account.newsUnread && account.pendingVision && account.news.Count==3,"New raid creates a fresh notification after older news was read");
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try
        {
            if(phase==0)
            {
                Logic();game=HeistGameManager.Instance;economy=HouseholdEconomy.Instance;checkpoint=Object.FindAnyObjectByType<GameCheckpoint>();GameMenu.Instance.Resume();baseline=checkpoint.Capture();
                game.player.GetComponent<PlayerMovement>().enabled=false;game.player.GetComponentInChildren<PlayerLook>().enabled=false;
                bird=Object.FindObjectsByType<FarmAnimalMotion>().First(a=>!a.cow);cow=Object.FindObjectsByType<FarmAnimalMotion>().First(a=>a.cow);
                var light=new GameObject("Review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.2f;light.transform.rotation=Quaternion.Euler(40,15,0);
                Check(Object.FindObjectsByType<InteractableChicken>().All(b=>b.GetComponent<FarmAnimalMotion>()!=null),"Every farm chicken has animation");
                Check(Object.FindObjectsByType<SimpleAnimalWander>().All(b=>b.GetComponent<FarmAnimalMotion>()!=null),"Every cow has animation");
                next=Time.realtimeSinceStartup+.2f;phase++;return;
            }
            if(phase==1)
            {
                foreach(var animal in new[]{bird,cow})
                {
                    var filter=animal.GetComponentInChildren<MeshFilter>();var original=Resources.Load<Mesh>("AnimalMotion/"+filter.sharedMesh.name.Replace("(Clone)","")).vertices;
                    animal.Pose(1,animal.cow?0:1,animal.cow?0:1);var moved=filter.sharedMesh.vertices;
                    Check(moved.Where((v,i)=>Vector3.Distance(v,original[i])>.001f).Count()>5,(animal.cow?"Cow":"Chicken")+" articulation moves mesh vertices");
                    Capture(animal.cow?"cow-stride":"chicken-peck-flap",animal.transform.position+new Vector3(2,1.8f,2.5f),animal.transform.position+Vector3.up*(animal.cow?1:.65f));
                }
                for(int level=0;level<=3;level++)
                {
                    var account=new HouseholdAccount{repairs=level,flock=3};Check(economy.RestoreAccount(account),"Persist home level "+level);
                    Check(economy.repairStages.Where((s,i)=>s.activeSelf!=(i<level)).Count()==0,"Construction matches account level "+level);
                    economy.home.GetComponentInChildren<CoopUpgradeVisibility>().SendMessage("LateUpdate");
                    var home=economy.home;Capture("coop-level-"+level,home.TransformPoint(new Vector3(20,5,-11)),home.TransformPoint(new Vector3(12,1,-3)));
                }
                cameraPosition=Camera.main.transform.localPosition;cameraRotation=Camera.main.transform.localRotation;
                GameAudioMix.Voice=.23f;Check(StoryDirector.Instance.Begin(false),"Opening starts");Check(Mathf.Abs(StoryDirector.Instance.GetComponent<AudioSource>().volume-.23f)<.001f,"Opening applies voice volume before playback");next=Time.realtimeSinceStartup+5f;phase++;return;
            }
            if(phase==2)
            {
                if(!Application.isBatchMode && !openingCaptured){ScreenCapture.CaptureScreenshot(Folder+"/opening-ui.png");openingCaptured=true;next=Time.realtimeSinceStartup+.3f;return;}
                Check(StoryDirector.Active && GameMenu.BlocksInput,"Opening blocks gameplay inputs");Check(Mathf.Abs(StoryDirector.Instance.GetComponent<AudioSource>().volume-.23f)<.001f,"Cutscene retains configured voice volume across frames");GameAudioMix.Voice=1;
                Capture("opening-shot",Camera.main.transform.position,Camera.main.transform.position+Camera.main.transform.forward*15);
                StoryDirector.Instance.Complete();Check(economy.Account.introSeen && !StoryDirector.Active,"Skipping opening records completion");
                Check(Vector3.Distance(Camera.main.transform.localPosition,cameraPosition)<.001f && Quaternion.Angle(Camera.main.transform.localRotation,cameraRotation)<.01f,"Cutscene restores player camera");
                economy.Commit(a=>{a.RegisterRaid("Fazenda de teste",1,false);a.RestUntilMorning();return true;},"Test");
                Check(StoryDirector.Instance.Begin(true),"Crime vision starts from pending event");next=Time.realtimeSinceStartup+5f;phase++;return;
            }
            if(phase==3)
            {
                if(!Application.isBatchMode && !declineCaptured){ScreenCapture.CaptureScreenshot(Folder+"/decline-ui.png");declineCaptured=true;next=Time.realtimeSinceStartup+.3f;return;}
                Capture("decline-shot",Camera.main.transform.position,Camera.main.transform.position+Camera.main.transform.forward*15);
                StoryDirector.Instance.Complete();Check(!economy.Account.pendingVision && economy.Account.newsUnread,"Completing vision preserves unread news and clears only the scene event");
                foreach(var key in new[]{"engine","starter","start","stall","step","wood","door","sleep","wake","chicken","cow","spray"})
                {
                    var clip=GameAudioMix.Instance.Clip(key);var samples=new float[clip.samples];clip.GetData(samples,0);Check(samples.All(float.IsFinite) && samples.Max(x=>Mathf.Abs(x))<.96f && samples.Any(x=>Mathf.Abs(x)>.01f),"Audio signal finite and below full scale: "+key);
                }
                File.WriteAllLines(Folder+"/dialogue-recordings.txt",StoryDialogue.openingFiles.Concat(StoryDialogue.declineFiles).Select(id=>(Resources.Load<AudioClip>("Dialogue/"+id)!=null?"PRESENT ":"MISSING ")+id));
                for(int i=0;i<30;i++)GameAudioMix.Instance.Play("step",game.player.position);
                Check(GameAudioMix.Instance.ActiveEffects<=12,"Effect pool limits simultaneous sounds");
                Check(checkpoint.Restore(baseline,out _),"Baseline checkpoint restores after upgrade and story tests");
                elapsedStart=Time.realtimeSinceStartup;frameStart=Time.frameCount;birdStart=bird.GaitPhase;cowStart=cow.GaitPhase;
                bird.GetComponent<InteractableChicken>().sleeping=false;
                timing=game.gameObject.AddComponent<FrameTimingReview>();timing.Begin();
                next=Time.realtimeSinceStartup+20;phase++;return;
            }
            if(phase==4)
            {
                File.WriteAllText(Folder+"/frame-timings.txt",timing.Report());
                var motions=Object.FindObjectsByType<FarmAnimalMotion>();
                Check(motions.All(a=>float.IsFinite(a.transform.position.x) && float.IsFinite(a.transform.position.y) && float.IsFinite(a.GaitPhase)),"All animated animals retain finite positions and gait after six seconds");
                float fps=(Time.frameCount-frameStart)/Mathf.Max(.001f,Time.realtimeSinceStartup-elapsedStart);
                File.WriteAllText(Folder+"/animation-runtime.txt","Animals: "+motions.Length+"\nEditor average FPS over review window: "+fps.ToString("F1")+"\nThis is an editor smoke measurement, not a release performance certification.");
                Capture("chicken-live",bird.transform.position+new Vector3(.9f,.9f,1.4f),bird.transform.position+Vector3.up*.55f);
                Capture("cow-live",cow.transform.position+new Vector3(2,1.6f,2.6f),cow.transform.position+Vector3.up);
                foreach(var animal in new[]{bird,cow})
                {
                    float gait=animal.GaitPhase;var motion=animal.GetComponentInChildren<MeshFilter>().sharedMesh.vertices;
                    GameMenu.Instance.Pause();animal.SendMessage("LateUpdate");
                    Check(animal.GaitPhase==gait && animal.GetComponentInChildren<MeshFilter>().sharedMesh.vertices.SequenceEqual(motion),"Pause preserves animated mesh: "+(animal.cow?"cow":"chicken"));GameMenu.Instance.Resume();
                }
                EditorApplication.isPlaying=false;return;
            }
        }
        catch(Exception e){Check(false,e.ToString());EditorApplication.isPlaying=false;}
    }
    static float elapsedStart,birdStart,cowStart;static int frameStart;static bool openingCaptured,declineCaptured;
    static FrameTimingReview timing;
}


