using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object=UnityEngine.Object;

// Explicit opt-in only. HouseholdEconomy routes these sessions away from user saves.
public class ReleaseAuditRunner : MonoBehaviour
{
    RenderTexture auditTarget;
    void LateUpdate(){if(done || Camera.main==null)return;if(auditTarget==null)auditTarget=new RenderTexture(1920,1080,24);var camera=Camera.main;var previous=camera.targetTexture;camera.targetTexture=auditTarget;camera.Render();camera.targetTexture=previous;}
    string folder;bool done;float deadline;readonly List<string> checks=new List<string>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch(){if(HouseholdEconomy.ReviewSession && Array.IndexOf(Environment.GetCommandLineArgs(),"--release-audit")>=0 && FindAnyObjectByType<ReleaseAuditRunner>()==null)new GameObject("Isolated release audit").AddComponent<ReleaseAuditRunner>();}
    void Awake(){DontDestroyOnLoad(gameObject);Application.runInBackground=true;folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../../ChickenHeist/output/release-audit"));Directory.CreateDirectory(folder);deadline=Time.realtimeSinceStartup+650;Application.logMessageReceived+=Log;}
    void OnDestroy(){Application.logMessageReceived-=Log;}
    void Log(string message,string stack,LogType type){if(type==LogType.Exception || type==LogType.Error){Check(false,message);Finish();}}
    void Update(){if(!done && Time.realtimeSinceStartup>deadline){Check(false,"Audit timeout");Finish();}}
    void Check(bool ok,string message){checks.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines(Path.Combine(folder,"checks.txt"),checks);}
    void Finish(){if(done)return;done=true;Application.Quit(checks.Any(s=>s.StartsWith("FAIL"))?1:0);}
    IEnumerator Shot(string name){yield return new WaitForEndOfFrame();var old=RenderTexture.active;RenderTexture.active=auditTarget;var image=new Texture2D(1920,1080,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1920,1080),0,0);image.Apply();RenderTexture.active=old;File.WriteAllBytes(Path.Combine(folder,name+".png"),image.EncodeToPNG());Destroy(image);}
    void Position(Vector3 p){var player=HeistGameManager.Instance.player;var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.position=p;Physics.SyncTransforms();}
    IEnumerator RouteReady(){float end=Time.realtimeSinceStartup+45;while(MissionNavigation.Instance.Status=="Calculando caminho..." && Time.realtimeSinceStartup<end)yield return null;}
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(3);
        Screen.SetResolution(1920,1080,false);yield return new WaitForSecondsRealtime(1);
        Check(!GameMenu.Instance.HasSave,"Clean account begins without checkpoint");
        GameMenu.Instance.SendMessage("BeginGame");
        yield return new WaitForSecondsRealtime(5);
        Check(StoryDirector.Active,"First game through menu starts opening");
        yield return Shot("opening");StoryDirector.Instance.Complete();yield return null;
        var game=HeistGameManager.Instance;var economy=HouseholdEconomy.Instance;var player=game.player;
        player.GetComponent<PlayerMovement>().enabled=false;player.GetComponentInChildren<PlayerLook>().enabled=false;
        var checkpoint=FindAnyObjectByType<GameCheckpoint>();
        Check(checkpoint.Save(out _),"Checkpoint saved before failure injection");
        using(var file=new FileStream(economy.CheckpointPath,FileMode.Open,FileAccess.Read,FileShare.None))Check(!checkpoint.Save(out _),"Locked checkpoint rejects save without reporting success");
        Check(checkpoint.Save(out _),"Checkpoint retry succeeds after releasing file lock");
        var origin=player.position;var eye=Camera.main;
        foreach(var door in FindObjectsByType<HomeDoor>())door.RestoreOpen(true);
        var farms=FindObjectsByType<FarmLayoutInfo>().OrderBy(f=>f.layoutIndex).ToArray();
        foreach(var farm in farms)
        {
            game.EndActiveMission();Position(farm.entrance+Vector3.back*3);
            int index=Array.IndexOf(ProtagonistPhone.Instance.farmNames,farm.identity);Check(game.StartMission(index),"Start "+farm.identity);
            MissionNavigation.Instance.ToggleDestination();yield return RouteReady();
            Check(MissionNavigation.Instance.Route.Count>1,"Return route from "+farm.identity+": "+MissionNavigation.Instance.Status);
            eye.transform.position=new Vector3(farm.lot.center.x,Mathf.Max(player.position.y,0)+38,farm.lot.yMin-15);eye.transform.LookAt(new Vector3(farm.lot.center.x,player.position.y,farm.lot.center.y));
            yield return Shot("farm-"+farm.layoutIndex);
        }
        game.EndActiveMission();Position(origin);eye.transform.localPosition=new Vector3(0,1.65f,0);eye.transform.localRotation=Quaternion.identity;
        var nav=MissionNavigation.Instance;game.StartMission(0);yield return RouteReady();
        var timing=game.gameObject.AddComponent<FrameTimingReview>();timing.Begin();
        var route=nav.Route.ToArray();float until=Time.realtimeSinceStartup+20;int corner=1;
        while(Time.realtimeSinceStartup<until){if(route.Length>1){var goal=route[Mathf.Min(corner,route.Length-1)];player.position=Vector3.MoveTowards(player.position,goal,5*Time.deltaTime);if(Vector3.Distance(player.position,goal)<.1f)corner=Mathf.Min(corner+1,route.Length-1);}yield return null;}
        File.WriteAllText(Path.Combine(folder,"timing-travel.txt"),timing.Report().Replace("Editor measurement, not standalone certification.","Standalone with forced offscreen 1080p camera rendering; hidden window, scripted traversal. Diagnostic only, not interactive FPS certification."));
        Check(Screen.width==1920 && Screen.height==1080,"Performance sample at 1920x1080");
        game.EndActiveMission();Position(origin);
        var actor=FindAnyObjectByType<RoadsideWalker>();var truck=OldPickupTruck.Instance;
        if(actor!=null){var reaction=actor.GetComponent<VehicleImpactReaction>()??actor.gameObject.AddComponent<VehicleImpactReaction>();Check(reaction.Hit(Vector3.forward*5),"Forward impact knocks pedestrian down");yield return new WaitForSeconds(7.2f);Check(!reaction.Down,"Pedestrian recovers from forward impact");Check(reaction.Hit(Vector3.back*5),"Reverse impact knocks pedestrian down");yield return new WaitForSeconds(4.2f);Check(!reaction.Down,"Pedestrian recovers from reverse impact");}
        economy.Commit(a=>{a.flock=0;return true;},"Test");yield return null;
        var flock=game.DeliveryFlock;Position(flock.transform.position+Vector3.back*2);game.backpack.RestoreCount(1);game.CompleteMission();Check(game.backpack.chickensCarried==0 && economy.Account.flock==1,"Delivery commits one chicken");yield return new WaitForSeconds(.3f);eye.transform.LookAt(flock.transform.position);yield return Shot("delivery");
        economy.Commit(a=>{a.repairs=3;foreach(var debt in a.debts)debt.amount=0;return true;},"Test");Check(economy.Account.CampaignComplete && economy.Account.campaignCelebrated,"Campaign completion recorded after repairs and debts");
        Position(origin);StoryDirector.Instance.Begin(true);yield return new WaitForSeconds(2);yield return Shot("decline");StoryDirector.Instance.Complete();
        Check(economy.Account.declineSeen,"Completed vision persisted");Finish();
    }
}

