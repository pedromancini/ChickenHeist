using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

public sealed class AudioReview : MonoBehaviour
{
    string folder;bool done;float deadline;readonly List<string> checks=new List<string>();
    readonly float[] left=new float[1024],right=new float[1024];
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch(){if(HouseholdEconomy.ReviewSession && Array.IndexOf(Environment.GetCommandLineArgs(),"--audio-review")>=0 && FindAnyObjectByType<AudioReview>()==null)new GameObject("Isolated audio review").AddComponent<AudioReview>();}
    void Awake(){DontDestroyOnLoad(gameObject);Application.runInBackground=true;folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../ChickenHeist/output/audio-review"));Directory.CreateDirectory(folder);deadline=Time.realtimeSinceStartup+240;Application.logMessageReceived+=Log;}
    void OnDestroy(){Application.logMessageReceived-=Log;}
    void Log(string message,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error){Check(false,message);Finish();}}
    void Update(){if(!done && Time.realtimeSinceStartup>deadline){Check(false,"Audio review timeout");Finish();}}
    void Check(bool ok,string message){checks.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines(Path.Combine(folder,"checks.txt"),checks);}
    void Note(string message){checks.Add("     "+message);File.WriteAllLines(Path.Combine(folder,"checks.txt"),checks);}
    void Finish(){if(done)return;done=true;Application.Quit(checks.Any(s=>s.StartsWith("FAIL"))?1:0);}
    IEnumerator Measure(float seconds,float[] result)
    {
        double sum=0;long count=0;float end=Time.realtimeSinceStartup+seconds;
        while(Time.realtimeSinceStartup<end)
        {
            AudioListener.GetOutputData(left,0);AudioListener.GetOutputData(right,1);
            for(int i=0;i<left.Length;i++){sum+=left[i]*left[i]+right[i]*right[i];count+=2;}
            yield return null;
        }
        result[0]=20*Mathf.Log10(Mathf.Sqrt((float)(sum/Math.Max(1,count)))+1e-6f);
    }
    static string Playing()
    {
        var names=FindObjectsByType<AudioSource>().Where(s=>s.isPlaying && s.clip!=null).Select(s=>s.clip.name+(s.loop?" (loop)":"")).OrderBy(n=>n).ToArray();
        return names.Length+" sources: "+string.Join(", ",names);
    }
    static bool Sounding(string clip)=>FindObjectsByType<AudioSource>().Any(s=>s.isPlaying && s.clip!=null && s.clip.name==clip);
    static void Place(Transform player,Vector3 at)
    {
        var cc=player.GetComponent<CharacterController>();bool was=cc.enabled;cc.enabled=false;
        if(Physics.Raycast(at+Vector3.up*3,Vector3.down,out var hit,8,~0,QueryTriggerInteraction.Ignore))at=hit.point+Vector3.up*.05f;
        player.position=at;Physics.SyncTransforms();cc.enabled=was;
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(3);
        GameMenu.Instance.SendMessage("BeginGame");yield return new WaitForSecondsRealtime(4);
        StoryDirector.Instance.Complete();yield return null;SleepPrompt.SkipForReview();yield return new WaitForSecondsRealtime(1.5f);
        var player=HeistGameManager.Instance.player;var r=new float[1];
        Note("volumes: master "+AudioListener.volume.ToString("0.00")+", effects "+GameAudioMix.Effects.ToString("0.00")+", ambience "+GameAudioMix.Ambience.ToString("0.00"));
        var bell=HomeDoorbell.Instance;
        Check(bell!=null,"The doorbell exists");
        if(bell!=null)
        {
            var away=bell.InteractionPoint-bell.transform.forward*1.4f;Place(player,away);
            yield return new WaitForSecondsRealtime(1.2f);yield return Measure(1,r);float quiet=r[0];Note("near the bell, quiet: "+quiet.ToString("0.0")+" dB; "+Playing());
            bell.Ring();yield return null;bool sounding=Sounding("bell");Note("ringing: "+Playing());
            yield return Measure(.45f,r);float ring=r[0];
            Check(sounding && ring>quiet+6,"The doorbell is heard: "+ring.ToString("0.0")+" dB against "+quiet.ToString("0.0")+" dB quiet");
        }
        var truck=OldPickupTruck.Instance;
        Check(truck!=null,"The truck exists");
        if(truck!=null)
        {
            var seat=truck.seat;
            Place(player,seat.position-truck.transform.right*1.3f);yield return new WaitForSecondsRealtime(.6f);
            Check(truck.EnterDriver(),"Elias gets in the truck");yield return new WaitForSecondsRealtime(1.2f);
            yield return Measure(1,r);float quiet=r[0];Note("in the cab, engine off: "+quiet.ToString("0.0")+" dB; "+Playing());
            Check(truck.ignition.Begin(),"The ignition starts");yield return null;yield return null;bool cranking=Sounding("starter");Note("cranking: "+Playing());
            yield return Measure(.8f,r);float crank=r[0];
            Check(cranking && crank>quiet+6,"The starter is heard: "+crank.ToString("0.0")+" dB against "+quiet.ToString("0.0")+" dB");
            var target=typeof(TruckIgnition).GetProperty("Target");
            for(int hit=0;hit<2;hit++){target.SetValue(truck.ignition,truck.ignition.CursorPosition);truck.ignition.Confirm();yield return null;}
            yield return null;yield return null;bool caught=Sounding("start");Note("catching: "+Playing());
            yield return Measure(.8f,r);float start=r[0];
            Check(truck.ignition.EngineRunning && caught && start>quiet+6,"The engine catching is heard: "+start.ToString("0.0")+" dB");
            yield return new WaitForSecondsRealtime(1.5f);yield return Measure(1,r);float idle=r[0];Note("idling: "+Playing());
            Check(Sounding("engine") && idle>quiet+6,"The engine idling is heard: "+idle.ToString("0.0")+" dB");
            truck.ignition.StopEngine();yield return new WaitForSecondsRealtime(2);truck.ExitDriver();yield return null;
        }
        var coop=FindObjectsByType<ChickenCoopLockpick>().OrderBy(c=>(c.transform.position-HouseholdEconomy.Instance.home.position).sqrMagnitude).FirstOrDefault();
        if(coop!=null)
        {
            Place(player,coop.transform.position+coop.transform.forward*3);
            int busiest=0;for(int s=0;s<8;s++){yield return new WaitForSecondsRealtime(1);busiest=Mathf.Max(busiest,GameAudioMix.Instance.ActiveEffects);}
            Note("by the home coop: "+Playing());
            Check(busiest<10,"Effect slots stay free for new sounds by the coop: at most "+busiest+" of 12 in use");
            Check(!Sounding("chicken_walk"),"No hens-scratching loop by the coops");
        }
        {
            float alive=0,total=0;var kinds=new HashSet<string>();float end=Time.realtimeSinceStartup+45;
            while(Time.realtimeSinceStartup<end)
            {
                var life=FindObjectsByType<AudioSource>().Where(x=>x.isPlaying && x.clip!=null && x.clip.name.StartsWith("night_") && x.clip.name!="night_ambience").ToArray();
                foreach(var x in life)kinds.Add(x.clip.name);
                if(life.Length>0)alive+=Time.unscaledDeltaTime;total+=Time.unscaledDeltaTime;yield return null;
            }
            Note("night life over 45 s: "+string.Join(", ",kinds)+"; sounding "+(100*alive/total).ToString("0")+"% of the time");
            Check(kinds.Count>=1 && alive/total>.08f && alive/total<.75f,"The night has life with silences between: "+(100*alive/total).ToString("0")+"% of the time, "+kinds.Count+" kinds");
            Check(FindObjectsByType<AudioSource>().Any(x=>x.isPlaying && x.clip!=null && x.clip.name=="night_ambience"),"The night breeze bed plays");
        }
        Finish();
    }
}
