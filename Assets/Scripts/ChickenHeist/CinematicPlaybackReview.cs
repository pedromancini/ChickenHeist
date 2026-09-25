using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

// Opt-in player QA; never runs in ordinary games and requires the isolated save flag.
public sealed class CinematicPlaybackReview : MonoBehaviour
{
    string folder;readonly List<string> report=new List<string>();readonly HashSet<int> captures=new HashSet<int>();bool failed;int frames;float longest;
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
                yield return new WaitForSecondsRealtime(Mathf.Min(.7f,director.LineDuration*.25f));yield return new WaitForEndOfFrame();Capture("full-"+line.ToString("00"));captures.Add(line);
            }
            yield return null;
        }
        Check(!StoryDirector.Active,"Whole timeline completed naturally");Check(captures.Count==VisitorOpeningDialogue.Text.Length,"Every beat rendered with actual GUI: "+captures.Count);
        yield return new WaitForSecondsRealtime(.3f);yield return new WaitForEndOfFrame();Capture("gameplay-after");
        Check(economy.Account.balance==balance && economy.Account.flock==flock,"Opening preserves money and bird count");
        var dock=FindAnyObjectByType<ReceivedTabletDock>();Check(dock!=null && dock.Available,"Delivered tablet remains interactive");
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
        report.Add("Observed frames: "+frames+"; longest frame including image capture overhead: "+longest.ToString("F3")+"s. Not a performance benchmark.");File.WriteAllLines(Path.Combine(folder,"playback-checks.txt"),report);
        Application.Quit(failed?1:0);
    }
    void Capture(string name)
    {var texture=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(folder,name+".png"),texture.EncodeToPNG());Destroy(texture);}
}
