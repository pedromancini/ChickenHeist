using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

// Runs only with two explicit review flags, using the isolated household account.
public class MenuVisualReview : MonoBehaviour
{
    readonly List<string> results=new List<string>();
    string output;
    float deadline;
    bool finished;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch()
    {
        if(!HouseholdEconomy.ReviewSession || Array.IndexOf(Environment.GetCommandLineArgs(),"--menu-review")<0 || FindAnyObjectByType<MenuVisualReview>()!=null)return;
        new GameObject("Menu visual review runner").AddComponent<MenuVisualReview>();
    }
    void Awake()
    {
        DontDestroyOnLoad(gameObject);Application.runInBackground=true;
        output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../output/menu-review"));
        Directory.CreateDirectory(output);deadline=Time.realtimeSinceStartup+180;
        Application.logMessageReceived+=Log;
    }
    void Log(string message,string stack,LogType type)
    {
        if(type==LogType.Exception || type==LogType.Error)results.Add("FAIL runtime: "+message);
    }
    void Update(){if(!finished && Time.realtimeSinceStartup>deadline){results.Add("FAIL review timed out");Finish();}}
    void OnDestroy(){Application.logMessageReceived-=Log;}
    void Check(bool condition,string message){results.Add((condition?"PASS ":"FAIL ")+message);}
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(target,value);
    IEnumerator Shot(string name)
    {
        yield return new WaitForSecondsRealtime(.3f);
        yield return new WaitForEndOfFrame();
        var texture=ScreenCapture.CaptureScreenshotAsTexture();
        File.WriteAllBytes(Path.Combine(output,name+".png"),texture.EncodeToPNG());Destroy(texture);
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2);
        var menu=GameMenu.Instance;
        Check(menu!=null && GameMenu.IsOpen && Time.timeScale==0,"Standalone starts in a paused main menu");
        Check(!menu.HasSave,"Review account is isolated from real saves");
        yield return Shot("01-main-1280");
        menu.SendMessage("OpenSettings");yield return Shot("02-video-1280");
        Set(menu,"settingsTab",2);yield return Shot("03-controls-1280");
        Set(menu,"settingsTab",1);yield return Shot("04-audio-1280");
        Screen.SetResolution(800,600,false);yield return new WaitForSecondsRealtime(1);
        Set(menu,"settingsTab",0);yield return Shot("05-video-800");
        Screen.SetResolution(1280,800,false);yield return new WaitForSecondsRealtime(1);
        var draft=GamePreferences.Defaults();draft.width=800;draft.height=600;draft.fullscreen=false;
        Set(menu,"settings",draft);menu.SendMessage("ApplySettings");
        yield return new WaitForSecondsRealtime(1);yield return Shot("06-video-confirm");
        // Exercise the real deadline branch without waiting fifteen seconds for each review.
        Set(menu,"confirmUntil",Time.realtimeSinceStartup+.1f);yield return new WaitForSecondsRealtime(1);
        Check(Screen.width==1280 && Screen.height==800,"Unconfirmed video change restores original resolution");
        menu.SendMessage("LeaveSettings");menu.SendMessage("BeginGame");
        yield return new WaitForSecondsRealtime(1);
        Check(!GameMenu.IsOpen && !HeistGameManager.Instance.MissionActive,"Start game enters quiet free exploration");
        yield return Shot("07-exploration");
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--truck-review")>=0)
        {
            var checkpoint=menu.GetComponent<GameCheckpoint>();var baseline=checkpoint.Capture();
            var game=HeistGameManager.Instance;var economy=HouseholdEconomy.Instance;var truck=OldPickupTruck.Instance;
            var movement=game.player.GetComponent<PlayerMovement>();var look=game.player.GetComponentInChildren<PlayerLook>();
            var cc=game.player.GetComponent<CharacterController>();
            economy.RestoreAccount(new HouseholdAccount{balance=500});game.EndActiveMission();game.backpack.RestoreCount(0);
            cc.enabled=false;game.player.position=truck.transform.position+new Vector3(-3.8f,0,-4.8f);movement.RestorePosture(false);cc.enabled=true;
            movement.enabled=false;look.enabled=false;Camera.main.transform.LookAt(truck.transform.position+Vector3.up);
            yield return Shot("truck-01-old-pickup");
            for(int i=0;i<4;i++)economy.Buy(5);
            Check(economy.Account.truckCages==4 && economy.Account.TruckCapacity==8,"Shop installs four two-bird cages for 400");
            Check(game.StartMission(0),"Truck review starts a mission");game.backpack.RestoreCount(8);
            cc.enabled=false;game.player.position=truck.cargoPoint.position-Vector3.up;cc.enabled=true;
            for(int i=0;i<8;i++)Check(truck.LoadOne(),"Load bird "+(i+1)+" into truck");
            cc.enabled=false;game.player.position=truck.transform.position+new Vector3(3.5f,.2f,4.5f);cc.enabled=true;
            Camera.main.transform.LookAt(truck.transform.position+Vector3.up*1.1f);yield return Shot("truck-02-full-cages");
            cc.enabled=false;game.player.position=truck.seat.position-truck.transform.right*1.5f;movement.RestorePosture(false);cc.enabled=true;
            Camera.main.transform.localRotation=Quaternion.identity;
            Check(truck.EnterDriver(),"Driver enters cabin with loaded cages");yield return new WaitForSecondsRealtime(.6f);
            yield return Shot("truck-03-driving-view");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--physics-review")>=0)
            {
                var vehicle=truck.vehicle;vehicle.ExternalControl=true;Vector3 start=vehicle.Body.position;
                truck.Drive(1,0,false,.02f);yield return new WaitForSecondsRealtime(2.5f);
                Check(vehicle.SignedSpeed>1 && Vector3.Distance(start,vehicle.Body.position)>3,"Standalone wheel physics accelerates loaded pickup");
                Check(Vector3.Dot(truck.transform.up,Vector3.up)>.8f && vehicle.GroundedWheels>=2,"Loaded pickup remains stable on home departure road");
                yield return Shot("truck-04-road-suspension");
                truck.Drive(0,0,true,.02f);yield return new WaitForSecondsRealtime(2);
                Check(vehicle.Body.linearVelocity.magnitude<.6f,"Standalone brakes stop loaded pickup");
                vehicle.ExternalControl=false;
            }
            Check(truck.ExitDriver(),"Parked truck has safe exit beside cabin");
            game.CompleteMission();Check(economy.Account.flock==8 && economy.Account.truckChickens==0,"All eight transported chickens delivered once");
            checkpoint.Restore(baseline,out _);movement.enabled=true;look.enabled=true;
        }
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--progression-review")>=0)
        {
            var checkpoint=menu.GetComponent<GameCheckpoint>();var baseline=checkpoint.Capture();
            var game=HeistGameManager.Instance;var economy=HouseholdEconomy.Instance;
            economy.RestoreAccount(new HouseholdAccount());game.EndActiveMission();game.backpack.RestoreCount(0);
            Check(!FarmSecurityProgression.Installed,"Fresh region has no installed defenses");
            Check(game.StartMission(0),"First robbery starts without security");
            InteractableChicken bird=null;
            foreach(var candidate in FindObjectsByType<InteractableChicken>())if(game.IsMissionTarget(candidate)){bird=candidate;break;}
            bird.coop.RestoreOpen(true);
            var movement=game.player.GetComponent<PlayerMovement>();var look=game.player.GetComponentInChildren<PlayerLook>();
            var cc=game.player.GetComponent<CharacterController>();cc.enabled=false;
            game.player.position=bird.transform.position-Vector3.forward*1.1f;
            game.player.rotation=Quaternion.identity;movement.RestorePosture(false);cc.enabled=true;
            movement.enabled=false;look.enabled=false;Camera.main.transform.LookAt(bird.transform.position+Vector3.up*.22f);
            yield return Shot("progression-01-before-pickup");
            Check(bird.TrySteal(),"Real chicken pickup succeeds");
            yield return Shot("progression-02-lifting");
            yield return new WaitForSecondsRealtime(.8f);
            Camera.main.transform.localRotation=Quaternion.identity;
            yield return Shot("progression-03-in-arms");
            var carry=game.player.GetComponent<PlayerChickenCarry>();
            Check(carry.HasVisual && !carry.IsLifting,"Chicken remains visibly carried after pickup finishes");
            Check(carry.HandError<.18f,"Both wrists reach the carried chicken");
            var driver=game.player.GetComponentInChildren<RuralCharacterAnimator>();
            movement.estaMovendo=true;yield return new WaitForSecondsRealtime(.3f);
            Check(driver.CurrentState=="Walk" && carry.HandError<.18f,"Walking animation keeps both hands supporting chicken");
            movement.estaSprinting=true;yield return new WaitForSecondsRealtime(.3f);
            Check(driver.CurrentState=="Run" && carry.HasVisual,"Running retains carried chicken");
            movement.RestorePosture(true);yield return new WaitForSecondsRealtime(.4f);
            Check(driver.CurrentState=="CrouchIdle" && carry.HasVisual,"Crouching retains carried chicken");
            yield return Shot("progression-03b-crouched-carry");
            movement.RestorePosture(false);
            game.CompleteMission();
            yield return null;Check(!carry.HasVisual,"Delivery removes carried presentation");
            Check(!FarmSecurityProgression.Installed,"Delivery does not install defenses before sleeping");
            Check(game.PrepareNextNight() && FarmSecurityProgression.Installed,"Sleep installs regional protection");
            ProtagonistPhone.Instance.SetOpen(true);yield return new WaitForSecondsRealtime(1);
            yield return Shot("progression-04-newspaper");
            Check(economy.Account.news.Count==1,"Newspaper reports the actual robbery");
            ProtagonistPhone.Instance.SetOpen(false);checkpoint.Restore(baseline,out _);
            movement.enabled=true;look.enabled=true;
        }
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--padlock-review")>=0)
        {
            var checkpoint=menu.GetComponent<GameCheckpoint>();var baseline=checkpoint.Capture();
            var game=HeistGameManager.Instance;
            var coops=FindObjectsByType<ChickenCoopLockpick>();var coop=coops[0];
            var movement=game.player.GetComponent<PlayerMovement>();var look=game.player.GetComponentInChildren<PlayerLook>();
            var controller=game.player.GetComponent<CharacterController>();controller.enabled=false;
            game.player.position=coop.InteractionPoint-coop.transform.forward*.85f-Vector3.up*1.4f;
            movement.RestorePosture(false);controller.enabled=true;movement.enabled=false;look.enabled=false;
            Camera.main.transform.LookAt(coop.InteractionPoint);
            yield return Shot("padlock-01-model");
            int index=Array.IndexOf(ProtagonistPhone.Instance.farmNames,coop.GetComponentInParent<FarmLayoutInfo>().identity);
            Check(game.StartMission(index),"Padlock mission starts from matching phone entry");
            yield return Shot("padlock-02-prompt");
            Check(coop.TryBeginChallenge(),"Padlock begins through actual interaction entry point");
            yield return Shot("padlock-03-active");
            coop.SendMessage("EndChallenge");checkpoint.Restore(baseline,out _);movement.enabled=true;look.enabled=true;
        }
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--equipment-review")>=0)
        {
            var checkpoint=menu.GetComponent<GameCheckpoint>();var baseline=checkpoint.Capture();
            yield return null;
            var pack=BackpackPanel.Instance;
            Check(pack.Open(),"Backpack opens in exploration");yield return Shot("equipment-01-basic");
            HouseholdEconomy.Instance.Commit(a=>{a.balance=500;return true;},"Review account");
            HouseholdEconomy.Instance.Buy(3);HouseholdEconomy.Instance.Buy(4);pack.Equip(true);
            yield return Shot("equipment-02-equipped");
            Screen.SetResolution(800,600,false);yield return new WaitForSecondsRealtime(1);
            yield return Shot("equipment-03-small");
            pack.Close();Screen.SetResolution(1280,800,false);yield return new WaitForSecondsRealtime(1);
            var shop=ProtagonistPhone.Instance;shop.ReviewTab=3;shop.SetOpen(true);
            yield return new WaitForSecondsRealtime(1);Set(shop,"scroll",new Vector2(0,310));
            yield return Shot("equipment-04-phone-shop");shop.SetOpen(false);
            Check(checkpoint.Restore(baseline,out _),"Equipment review restores original account");
        }
        if(Array.IndexOf(Environment.GetCommandLineArgs(),"--dev-review")>=0)
        {
            var checkpoint=menu.GetComponent<GameCheckpoint>();var baseline=checkpoint.Capture();
            var dev=DeveloperConsole.Instance;
            Check(dev.Open() && dev.ExecuteCommand("/dev"),"Developer chat opens and displays help");
            Set(dev,"input","/fase 1");yield return Shot("dev-01-chat-1280");
            Screen.SetResolution(800,600,false);yield return new WaitForSecondsRealtime(1);
            yield return Shot("dev-02-chat-800");
            Screen.SetResolution(1280,800,false);yield return new WaitForSecondsRealtime(1);
            Check(dev.ExecuteCommand("/fase 1"),"Developer command starts phase one");dev.Close();
            yield return Shot("dev-03-phase-1");
            Check(dev.Open() && dev.ExecuteCommand("/fase 2"),"Developer command switches to phase two");
            dev.Close();yield return Shot("dev-04-phase-2");
            dev.ExecuteCommand("/dev");Check(checkpoint.Restore(baseline,out _),"Developer review restores baseline");
        }
        var phone=ProtagonistPhone.Instance;phone.ReviewTab=2;phone.SetOpen(true);
        yield return new WaitForSecondsRealtime(1);yield return Shot("08-phone-missions");
        Check(HeistGameManager.Instance.StartMission(0),"A farm from phone starts a mission");phone.SetOpen(false);
        yield return new WaitForSecondsRealtime(1);yield return Shot("09-mission-hud");
        menu.Pause();Check(menu.SaveProgress(),"Pause saves active mission");
        yield return Shot("10-pause-saved");
        GameCheckpoint.TryRead(HouseholdEconomy.Instance.CheckpointPath,out var saved,out _);
        var oldGame=HeistGameManager.Instance;
        oldGame.EndActiveMission();
        menu.SendMessage("RequestLoad");yield return Shot("11-load-confirm");
        menu.LoadProgress();yield return Shot("12-loading");
        while(HeistGameManager.Instance==null || HeistGameManager.Instance==oldGame || GameMenu.IsOpen)
            yield return null;
        yield return new WaitForSecondsRealtime(.8f);
        var restored=HeistGameManager.Instance;
        Check(restored.MissionActive && restored.MissionFarm==saved.missionFarm,"Async scene reload resumes saved farm mission");
        Check(Vector2.Distance(new Vector2(restored.player.position.x,restored.player.position.z),new Vector2(saved.position.x,saved.position.z))<.1f,"Async reload restores player location");
        Check(restored.backpack.chickensCarried==saved.carried,"Async reload restores backpack");
        yield return Shot("13-loaded-mission");
        restored.CompleteMission();Check(!restored.MissionActive,"Return home clears active mission");
        menu=GameMenu.Instance;menu.Pause();menu.SaveProgress();menu.SendMessage("SetPage",Enum.Parse(typeof(GameMenu).GetNestedType("Page",BindingFlags.NonPublic),"Main"));
        yield return Shot("14-main-save-summary");
        Finish();
    }
    void Finish()
    {
        if(finished)return;finished=true;File.WriteAllLines(Path.Combine(output,"review-results.txt"),results);
        Application.Quit(results.Exists(r=>r.StartsWith("FAIL"))?1:0);
    }
}
