using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

// Opt-in player QA; never runs in ordinary games and requires the isolated save flag. Runs after the director, so what
// it reads each frame is what that frame shows.
[DefaultExecutionOrder(10100)]
public sealed class CinematicPlaybackReview : MonoBehaviour
{
    string folder;readonly List<string> report=new List<string>();readonly HashSet<int> captures=new HashSet<int>();bool failed;int frames;float longest;
    // the moments that depend on the frame, as the player sees them: the hands at work (IK), the door, its side
    static readonly (int line,float at,string name)[] Moments={(0,.85f,"desk"),(5,.6f,"lever"),(5,5.0f,"door-open"),(29,3.2f,"door-close"),(29,5.0f,"door-shut")};
    readonly HashSet<string> taken=new HashSet<string>();VisitorCinematicStage stage;
    static readonly FieldInfo LineClock=typeof(StoryDirector).GetField("lineClock",BindingFlags.Instance|BindingFlags.NonPublic);
    // per spoken line, how far the speaker's jaw opened and the listener's (from 0.3 s in, once the last speaker's
    // mouth has closed)
    readonly float[] speakerJaw=new float[64],listenerJaw=new float[64];
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        var args=Environment.GetCommandLineArgs();if(Array.IndexOf(args,"--cinematic-review")<0 || !HouseholdEconomy.ReviewSession)return;
        new GameObject("Isolated cinematic playback QA").AddComponent<CinematicPlaybackReview>();
    }
    void Check(bool value,string text){failed|=!value;report.Add((value?"PASS ":"FAIL ")+text);File.WriteAllLines(Path.Combine(folder,"playback-checks.txt"),report);}
    IEnumerator Start()
    {
        folder=Path.Combine(Application.dataPath,"../CinematicReview");Directory.CreateDirectory(folder);Application.runInBackground=true;
        Application.logMessageReceived+=(message,stack,type)=>{if(type==LogType.Exception || type==LogType.Error)Check(false,message+"\n"+stack);};
        yield return new WaitForSecondsRealtime(3);
        GameMenu.Instance.Resume();yield return null;
        var economy=HouseholdEconomy.Instance;int balance=economy.Account.balance,flock=economy.Account.flock;
        var director=StoryDirector.Instance;Check(director.Begin(false),"Full opening starts with fresh isolated save");
        bool pauseTest=false;float deadline=Time.realtimeSinceStartup+200;
        while(StoryDirector.Active && Time.realtimeSinceStartup<deadline)
        {
            frames++;longest=Mathf.Max(longest,Time.unscaledDeltaTime);
            int line=director.CurrentLine;
            if(line==10 && !pauseTest)
            {
                pauseTest=true;director.SetPaused(true);float elapsed=director.Elapsed;var position=Camera.main.transform.position;
                yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();Capture("paused");
                Check(Mathf.Abs(director.Elapsed-elapsed)<.001f && Vector3.Distance(position,Camera.main.transform.position)<.001f,"Pause freezes the timeline and camera");director.SetPaused(false);
            }
            if(!captures.Contains(line))
            {
                float until=Time.realtimeSinceStartup+Mathf.Min(.7f,director.LineDuration*.25f);
                while(Time.realtimeSinceStartup<until){yield return TakeMoments(director);yield return null;}
                yield return new WaitForEndOfFrame();Capture("full-"+line.ToString("00"));captures.Add(line);
            }
            yield return TakeMoments(director);
            yield return null;
        }
        Check(!StoryDirector.Active,"Whole timeline completed naturally");Check(captures.Count==VisitorOpeningDialogue.Text.Length,"Every beat rendered with actual GUI: "+captures.Count);
        Check(taken.Count==Moments.Length,"Every hand, door and mouth moment rendered as played: "+taken.Count+"/"+Moments.Length);
        {
            int spoken=0,moving=0,quiet=0;
            for(int i=0;i<VisitorOpeningDialogue.Speakers.Length;i++){if(VisitorOpeningDialogue.Speakers[i]=="")continue;spoken++;if(speakerJaw[i]>=30)moving++;if(listenerJaw[i]<=3)quiet++;}
            Check(moving==spoken,"Whoever speaks moves the mouth through the line as it plays: "+moving+"/"+spoken+" lines");
            Check(quiet==spoken,"Whoever listens keeps the mouth shut: "+quiet+"/"+spoken+" lines");
        }
        yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Capture("gameplay-after");
        Check(economy.Account.balance==balance && economy.Account.flock==flock,"Opening preserves money and bird count");
        Check(SleepPrompt.IsOpen && economy.Account.tabletWaiting,"The tablet is only promised; a box sends Elias to bed");SleepPrompt.SkipForReview();yield return null;
        Check(economy.Account.TabletReady,"Delivered tablet stays with Elias");
        Check(Camera.main.rect==new Rect(0,0,1,1),"Gameplay viewport restored after opening");
        var phone=ProtagonistPhone.Instance;
        for(int attempt=0;attempt<2;attempt++)
        {
            phone.SetOpen(true);yield return null;yield return new WaitForEndOfFrame();Capture("tablet-open-"+attempt);
            Check(ProtagonistPhone.IsOpen && !StoryDirector.Active && !GameMenu.IsOpen,"Tablet opens after cinematic");
            phone.SetOpen(false);yield return null;
            Check(!ProtagonistPhone.IsOpen && !GameMenu.BlocksInput && Camera.main.rect==new Rect(0,0,1,1),"Tablet closes and gameplay is available");
        }
        Check(Vector3.Distance(HeistGameManager.Instance.player.position,economy.home.TransformPoint(new Vector3(-4.72f,.65f,3.78f)))<.4f,"New game resumes beside the table");
        foreach(var size in new[]{new Vector2Int(1024,768),new Vector2Int(1680,720)})
        {
            Screen.SetResolution(size.x,size.y,false);yield return new WaitForSecondsRealtime(.5f);
            var player=HeistGameManager.Instance.player;var playerPosition=player.position;
            Check(director.ReplayOpening(),"Replay starts at "+size);
            typeof(StoryDirector).GetField("line",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(director,22);
            typeof(StoryDirector).GetField("lineClock",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(director,1f);
            yield return new WaitForSecondsRealtime(.2f);yield return new WaitForEndOfFrame();Capture("subtitles-"+size.x+"x"+size.y);
            director.Complete();yield return null;Check(Vector3.Distance(playerPosition,player.position)<.05f,"Replay preserves player position");
        }
        // cutscene 2 ("O dia seguinte") in real time, as it comes the first night after a raid: every line rendered with
        // its subtitle, played to its end, and the world put back
        {
            var farms=FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None);System.Array.Sort(farms,(x,y)=>x.layoutIndex.CompareTo(y.layoutIndex));
            string robbed=farms[Mathf.Min(2,farms.Length-1)].identity;Screen.SetResolution(1280,800,false);yield return new WaitForSecondsRealtime(.5f);
            economy.Commit(a=>{a.news=new List<FarmRaidNews>{new FarmRaidNews{farm=robbed,chickens=4,day=a.day}};a.pendingVision=true;return true;},"Review raid");
            var ambient=RenderSettings.ambientLight;var view=Camera.main.rect;
            Check(director.Begin(true),"Cutscene 2 starts after a raid");
            var seen=new HashSet<int>();float end=Time.realtimeSinceStartup+120;var stage=FindAnyObjectByType<DeclineCinematicStage>();
            Check(stage!=null && stage.Farm==robbed,"Cutscene 2 plays at the raided farm: "+(stage!=null?stage.Farm:"no stage"));
            Check(director.LineText(4)=="Quatro.","Osvaldo counts the birds taken: "+director.LineText(4));
            while(StoryDirector.Active && Time.realtimeSinceStartup<end)
            {
                int line=director.CurrentLine;
                if(!seen.Contains(line) && (float)LineClock.GetValue(director)>director.LineDuration*.45f){seen.Add(line);yield return new WaitForEndOfFrame();Capture("vision-"+line.ToString("00"));}
                yield return null;
            }
            Check(!StoryDirector.Active && seen.Count==StoryDialogue.declineText.Length,"Cutscene 2 plays through, every line on screen: "+seen.Count+"/"+StoryDialogue.declineText.Length);
            yield return null;
            Check(FindAnyObjectByType<DeclineCinematicStage>()==null && RenderSettings.ambientLight==ambient && Camera.main.rect==view,"After cutscene 2 the world, its light and the view are back");
        }
        report.Add("Observed frames: "+frames+"; longest frame including image capture overhead: "+longest.ToString("F3")+"s. Not a performance benchmark.");File.WriteAllLines(Path.Combine(folder,"playback-checks.txt"),report);
        Application.Quit(failed?1:0);
    }
    void Capture(string name)
    {var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());Destroy(texture);}
    void LateUpdate()
    {
        if(!StoryDirector.Active || StoryDirector.Instance.CurrentLine>=VisitorOpeningDialogue.Speakers.Length)return;
        if(stage==null)stage=FindAnyObjectByType<VisitorCinematicStage>();
        if(stage==null || stage.EliasActor==null || stage.VisitorActor==null)return;
        int line=StoryDirector.Instance.CurrentLine;string who=VisitorOpeningDialogue.Speakers[line];
        if(who=="" || (float)LineClock.GetValue(StoryDirector.Instance)<.3f)return;
        var speaker=who=="Elias"?stage.EliasActor:stage.VisitorActor;var listener=who=="Elias"?stage.VisitorActor:stage.EliasActor;
        speakerJaw[line]=Mathf.Max(speakerJaw[line],speaker.JawWeight);listenerJaw[line]=Mathf.Max(listenerJaw[line],listener.JawWeight);
    }
    IEnumerator TakeMoments(StoryDirector director)
    {
        if(!StoryDirector.Active)yield break;
        int line=director.CurrentLine;float time=(float)LineClock.GetValue(director);
        foreach(var m in Moments)
        {
            if(m.line!=line || time<m.at || taken.Contains(m.name))continue;
            taken.Add(m.name);yield return new WaitForEndOfFrame();Capture("moment-"+m.name);Moment(m.name);
        }
    }
    void Moment(string name)
    {
        if(stage==null)stage=FindAnyObjectByType<VisitorCinematicStage>();
        var camera=Camera.main;var elias=stage.EliasActor;var door=stage.Door;
        float angle=door!=null?Quaternion.Angle(door.hinge.localRotation,Quaternion.identity):0;
        float inside=door!=null?Vector3.Dot(stage.elias.position-door.hinge.position,door.hinge.parent.forward):1;
        if(name=="desk")Check(InShot(camera,elias,true) && InShot(camera,elias,false),"Both of Elias' hands show on the desk");
        if(name=="lever")Check(elias.ContactError<.05f && InShot(camera,elias,false),"Elias' left hand on the inner lever and in the shot as he opens the door: "+elias.ContactError.ToString("F3"));
        if(name=="door-open")
        {
            var face=camera.WorldToViewportPoint(stage.VisitorActor.head.position);var chest=camera.WorldToViewportPoint(elias.spine.position);
            Check(angle>60 && face.z>0 && face.x>.05f && face.x<.95f && face.y>.05f && face.y<.95f && Mathf.Abs(face.x-chest.x)>.08f,
                "The door opens into the house and the visitor shows in the doorway beside Elias: "+angle.ToString("0")+" deg, his face at ("+face.x.ToString("0.00")+", "+face.y.ToString("0.00")+")");
        }
        if(name=="door-close")Check(elias.ContactError<.05f && inside>.05f,"Elias pushes the door shut from inside, his hand on the inner lever: "+elias.ContactError.ToString("F3")+", "+inside.ToString("F2")+" m inside");
        if(name=="door-shut")Check(angle<1 && inside>.3f,"The door ends shut with Elias inside: "+angle.ToString("0.0")+" deg, "+inside.ToString("F2")+" m inside");
    }
    // on screen, and not hidden behind the actor's trunk (a 0.42 m column round the chest, as in the editor review)
    static bool InShot(Camera camera,VisitorCinematicActor actor,bool right)
    {
        var v=camera.WorldToViewportPoint((right?actor.rightHand:actor.leftHand).position);var c=camera.WorldToViewportPoint(actor.spine.position);
        float tan=Mathf.Tan(camera.fieldOfView*.5f*Mathf.Deg2Rad),perX=1/(2*Mathf.Max(.05f,c.z)*tan*camera.aspect),perY=1/(2*Mathf.Max(.05f,c.z)*tan);
        bool behind=v.z>c.z+.04f && Mathf.Abs(v.x-c.x)<.21f*perX && v.y<c.y+.15f*perY && v.y>c.y-.45f*perY;
        return v.z>0 && v.x>.04f && v.x<.96f && v.y>.04f && v.y<.96f && !behind;
    }
}
