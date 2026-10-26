using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public sealed class LayoutReview : MonoBehaviour
{
    string folder;readonly List<string> report=new List<string>();bool failed;
    static readonly Vector2Int[] Sizes={new Vector2Int(1024,768),new Vector2Int(1280,800),new Vector2Int(1920,1080),new Vector2Int(1680,720)};
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--layout-review")<0 || !HouseholdEconomy.ReviewSession)return;
        var go=new GameObject("Layout review");DontDestroyOnLoad(go);go.AddComponent<LayoutReview>();
    }
    void Check(bool value,string text){failed|=!value;report.Add((value?"PASS ":"FAIL ")+text);File.WriteAllLines(Path.Combine(folder,"checks.txt"),report);}
    IEnumerator Capture(string name)
    {
        yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();
        var t=ScreenCapture.CaptureScreenshotAsTexture();File.WriteAllBytes(Path.Combine(folder,name+".png"),t.EncodeToPNG());Destroy(t);
    }
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
    bool MessageClear(out string what)
    {
        var toast=UITheme.LastToast;var hit=UITheme.Occupied.FirstOrDefault(o=>o.Overlaps(toast));
        what="message "+toast+(hit.width>0?" over "+hit:"");return hit.width<=0;
    }
    IEnumerator Start()
    {
        folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../output/layout-review"));Directory.CreateDirectory(folder);Application.runInBackground=true;
        Application.logMessageReceived+=(message,stack,type)=>{if(type==LogType.Exception)Check(false,message+"\n"+stack);};
        yield return new WaitForSecondsRealtime(3);
        GameMenu.Instance.Resume();yield return null;
        var economy=HouseholdEconomy.Instance;economy.RestoreAccount(new HouseholdAccount{introSeen=true,balance=420,feed=1,boards=1,repairs=1,paintUses=3});
        var game=HeistGameManager.Instance;var director=StoryDirector.Instance;
        int longest=Enumerable.Range(0,VisitorOpeningDialogue.Text.Length).OrderByDescending(i=>VisitorOpeningDialogue.Text[i].Length).First();
        foreach(var size in Sizes)
        {
            Screen.SetResolution(size.x,size.y,false);yield return new WaitForSecondsRealtime(1.2f);
            string at=Screen.width+"x"+Screen.height;
            game.ShowMessage("Galinhas entregues no sítio. Durma para começar um novo dia.",8);yield return new WaitForSecondsRealtime(.3f);
            Check(director.ReplayOpening(),"Opening replays at "+at);
            Set(director,"line",longest);Set(director,"lineClock",1f);
            yield return Capture("subtitle-long-"+at);
            Check(UITheme.LastToastFrame<Time.frameCount-2,"No message from play over the cutscene at "+at);
            Check(StoryDirector.SubtitleSpare>=0,"The longest line of the opening ("+VisitorOpeningDialogue.Text[longest].Length+" letters) fits on the screen at "+at+" ("+StoryDirector.SubtitleSpare.ToString("0")+" px to spare)");
            Set(director,"line",0);Set(director,"lineClock",1f);
            yield return Capture("subtitle-title-"+at);
            director.Complete();yield return new WaitForSecondsRealtime(.3f);
            if(SleepPrompt.IsOpen)SleepPrompt.SkipForReview();
            var truck=OldPickupTruck.Instance;var player=game.player;var cc=player.GetComponent<CharacterController>();
            cc.enabled=false;player.position=truck.seat.position-truck.transform.right*1.6f;cc.enabled=true;yield return null;
            Check(truck.EnterDriver(),"Elias takes the wheel at "+at);yield return new WaitForSecondsRealtime(.4f);
            game.ShowMessage("O motor pegou! Segure W para acelerar, S para frear e dar ré.",4);
            yield return Capture("drive-"+at);
            Check(MessageClear(out var drive),"Driving: the message keeps clear of the driving keys at "+at+" ("+drive+")");
            truck.ForceExit(truck.seat.position-truck.transform.right*1.8f+Vector3.up*.2f);yield return new WaitForSecondsRealtime(.3f);
            game.StartMission(0);yield return new WaitForSecondsRealtime(.4f);
            game.ShowMessage("Fio acionado! As latas alertaram o fazendeiro. Ele vai sair para ver o que foi; saia da vista dele.",4);
            yield return Capture("mission-"+at);
            Check(MessageClear(out var mission),"Mission: the message keeps clear of the mission card and prompts at "+at+" ("+mission+")");
            game.EndActiveMission();yield return new WaitForSecondsRealtime(.3f);
            var phone=ProtagonistPhone.Instance;phone.ReviewTab=3;phone.SetOpen(true);
            yield return Capture("shop-tablet-"+at);phone.SetOpen(false);yield return null;
            var market=FindAnyObjectByType<VillageMarket>();
            if(market!=null && market.counter!=null)
            {
                cc.enabled=false;player.position=market.counter.position-market.counter.forward*1.6f-Vector3.up;cc.enabled=true;yield return null;
                market.Open();Set(market,"tab",1);yield return Capture("shop-market-"+at);market.Close();yield return null;
            }
        }
        Screen.SetResolution(1280,800,false);yield return new WaitForSecondsRealtime(.5f);
        Application.Quit(failed?1:0);
    }
}
