using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PlayableVillageTests
{
    const string Key="ChickenHeist.PlayableVillageTests";
    const string Output="output/playable-review";
    static readonly List<string> results=new List<string>();
    static float next;
    static int phase,errors;
    static InteractableChicken first,second;
    static ChickenCoopLockpick coop;
    static VillageMarket market;
    static HeistGameManager game;
    static HouseholdEconomy economy;
    static Quaternion handBefore;
    static PlayableVillageTests()
    {
        EditorApplication.playModeStateChanged+=Changed;
        EditorApplication.update+=Tick;
        Application.logMessageReceived+=(message,stack,type)=>
        {if(SessionState.GetBool(Key,false) && (type==LogType.Error || type==LogType.Exception)){errors++;results.Add("ERROR "+message);}};
    }
    [MenuItem("Chicken Heist/Test Playable Village")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Directory.CreateDirectory(Output);EditorSceneManager.SaveOpenScenes();
        var account=Object.FindFirstObjectByType<HouseholdEconomy>();
        string path=Path.GetFullPath("Temp/playable-market-test-"+System.Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(path,new HouseholdAccount());
        account.editorTestSavePath=path;
        SessionState.SetString(Key+".path",path);
        foreach(var look in Object.FindObjectsByType<PlayerLook>(FindObjectsSortMode.None))look.enabled=false;
        foreach(var move in Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))move.enabled=false;
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            results.Clear();phase=-1;errors=0;next=Time.realtimeSinceStartup+2;
            Application.runInBackground=true;
        }
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);
            EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
            Debug.Log("PLAYABLE VILLAGE TESTS FINISHED: "+Output+"/playable-tests.txt");
            bool build=SessionState.GetBool(Key+".build",false);
            SessionState.SetBool(Key+".build",false);
            if(build && SessionState.GetBool(Key+".passed",false))
                EditorApplication.delayCall+=ChickenHeistPlayerBuild.Build;
        }
    }
    static void Expect(bool value,string message){results.Add((value?"PASS ":"FAIL ")+message);}
    static void Position(Vector3 p)
    {
        var cc=game.player.GetComponent<CharacterController>();cc.enabled=false;game.player.position=p;cc.enabled=true;
        game.player.GetComponent<PlayerMovement>().enabled=false;Physics.SyncTransforms();
    }
    static void SetPick(float value)
    {
        if(value>.1f){CoopPressureTests.SolvePiece(coop);return;}
        int wrong=(coop.Latch.NextPiece+1)%3;
        for(int i=0;i<42;i++)coop.Manipulate(.02f,wrong,0,true);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.isPaused || Time.realtimeSinceStartup<next)return;
        try
        {
            if(phase==-1)
            {
                results.AddRange(DeveloperConsoleTests.Run());next=Time.realtimeSinceStartup+.1f;
            }
            else if(phase==0)
            {
                game=HeistGameManager.Instance;economy=HouseholdEconomy.Instance;market=Object.FindFirstObjectByType<VillageMarket>();
                string isolatedPath=SessionState.GetString(Key+".path","");
                if(string.IsNullOrEmpty(isolatedPath) || economy.editorTestSavePath!=isolatedPath)
                    throw new System.InvalidOperationException("Aborting before transactions: isolated save override is missing.");
                Expect(economy.Ready && economy.Account.balance==95,"Isolated test account starts at R$95");
                Expect(Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None).Length==12,"Twelve farms preserved");
                results.AddRange(HiddenMarketTests.Check(market,economy.home));
                results.AddRange(NPCIntegrationTests.Begin());
                first=Object.FindObjectsByType<InteractableChicken>(FindObjectsSortMode.None).First(c=>c.coop!=null);
                coop=first.coop;
                Expect(!game.MissionActive,"Free exploration starts without an active heist");
                float beforeMission=game.farmers.Sum(f=>f.CurrentSleep);
                foreach(var farmer in game.farmers)farmer.AddNoise(35);
                Expect(Mathf.Approximately(beforeMission,game.farmers.Sum(f=>f.CurrentSleep)),"Farmers ignore heist alarms before phone mission starts");
                var phone=ProtagonistPhone.Instance;
                int farmIndex=System.Array.IndexOf(phone.farmNames,first.GetComponentInParent<FarmLayoutInfo>().identity);
                Expect(game.StartMission(farmIndex),"Phone farm selection starts its owner's mission");
                Expect(game.IsMissionTarget(first) && game.IsMissionFarmer(first.GetComponentInParent<FarmLayoutInfo>().GetComponentInChildren<FarmerSleepSystem>()),"Mission uses selected farm, not the most alert farmer in the valley");
                Expect(!game.StartMission(farmIndex),"A second mission cannot replace an active mission");
                var other=game.farmers.First(f=>!game.IsMissionFarmer(f));float otherAlert=other.CurrentSleep;other.AddNoise(50);
                Expect(Mathf.Approximately(other.CurrentSleep,otherAlert),"Other farmers stay outside the active mission");
                second=Object.FindObjectsByType<InteractableChicken>(FindObjectsSortMode.None).First(c=>c!=first && c.coop==coop);
                bool independent=first.gameObject!=second.gameObject && !second.transform.IsChildOf(first.transform);
                Expect(independent,"Chicken pickups have independent object roots");
                if(!independent)throw new System.InvalidOperationException("Chicken interaction is duplicated or nested on another chicken.");
                float peacefulAlert=game.farmers.Sum(f=>f.CurrentSleep);
                foreach(var animal in Object.FindObjectsByType<InteractableChicken>(FindObjectsSortMode.None))
                    NoiseEmitter.EmitAnimalActivity(NoiseSource.ChickenCluck,animal.transform.position);
                Expect(Mathf.Approximately(peacefulAlert,game.farmers.Sum(f=>f.CurrentSleep)),"Distant ambient animals do not wake farmers");
                Position(first.transform.position+Vector3.back*.6f);
                var movement=game.player.GetComponent<PlayerMovement>();
                movement.estaMovendo=true;movement.nivelRuido=.5f;
                NoiseEmitter.EmitAnimalActivity(NoiseSource.ChickenCluck,first.transform.position);
                Expect(game.farmers.Sum(f=>f.CurrentSleep)>peacefulAlert,"Nearby noisy movement disturbs animals and alerts farmer");
                movement.estaMovendo=false;movement.nivelRuido=0;
                float quietAlert=game.farmers.Sum(f=>f.CurrentSleep);
                NoiseEmitter.EmitAnimalActivity(NoiseSource.ChickenCluck,first.transform.position);
                Expect(Mathf.Approximately(quietAlert,game.farmers.Sum(f=>f.CurrentSleep)),"Standing quietly does not cause repeated animal alarms");
                Expect(!first.TrySteal() && game.backpack.chickensCarried==0,"Locked coop prevents theft");
                Position(coop.InteractionPoint-coop.transform.forward*1.5f-Vector3.up*1.2f);
                Camera.main.transform.LookAt(coop.InteractionPoint);
                Expect(coop.TryBeginChallenge() && coop.ChallengeActive,"Looking at the physical padlock starts the actual interaction");
                float alert=game.farmers.Sum(f=>f.CurrentSleep);
                SetPick(0);
                Expect(!coop.IsOpen,"Failed lockpick does not open gate");
                Expect(game.farmers.Sum(f=>f.CurrentSleep)>alert,"Lockpick mistake raises farmer alert");
                next=Time.realtimeSinceStartup+1.1f;
            }
            else if(phase==1)
            {
                if(!coop.IsOpen)
                {
                    for(int pin=0;pin<3;pin++)SetPick(.5f);Expect(coop.IsOpen,"Correct lockpick unlocks coop");
                    if(coop.IsOpen){next=Time.realtimeSinceStartup+.1f;return;}
                }
                Expect(coop.gameObject.activeInHierarchy,"Lockpick remains alive to finish jumpscare cleanup");
                Position(first.transform.position+Vector3.back*.6f);
                Expect(first.TrySteal() && game.backpack.chickensCarried==1,"Actual chicken pickup adds exactly one bird");
                Expect(game.player.GetComponentInChildren<RuralCharacterAnimator>().CurrentState=="Pickup","Chicken pickup triggers protagonist reach animation");
                Expect(!second.TrySteal(),"One interaction cannot collect multiple birds in a frame");
                Expect(GameMenu.Instance.SaveProgress(),"Checkpoint saves during an active mission");
                Expect(GameCheckpoint.TryRead(economy.CheckpointPath,out var checkpoint,out var saveError),"Checkpoint reads after atomic write: "+saveError);
                Expect(checkpoint!=null && checkpoint.missionFarm==game.MissionFarm && checkpoint.carried==1 && checkpoint.coops.Any(c=>c.active),"Save preserves mission, carried chicken and opened coop");
                int activeFarm=game.MissionFarm;game.EndActiveMission();game.backpack.RestoreCount(0);
                Expect(GameMenu.Instance.GetComponent<GameCheckpoint>().Restore(checkpoint,out _),"Active mission checkpoint restores successfully");
                Expect(game.MissionActive && game.MissionFarm==activeFarm && game.backpack.chickensCarried==1 && coop.IsOpen,"Reload resumes selected mission with its inventory and unlocked coop");
                next=Time.realtimeSinceStartup+.4f;
            }
            else if(phase==2)
            {
                Position(second.transform.position+Vector3.back*.6f);
                Expect(!second.TrySteal() && game.backpack.chickensCarried==1,"A second chicken cannot be carried even on a later frame");
                Position(market.counter.position+Vector3.back*.8f-Vector3.up);
                game.player.rotation=Quaternion.identity;
                Camera.main.transform.localRotation=Quaternion.identity;
                market.Open();Expect(VillageMarket.IsOpen,"Merchant interaction opens trade panel");
                Expect(market.Sell(1,true) && economy.Account.balance==140 && game.backpack.chickensCarried==0,"Backpack sale credits R$45 and removes one bird");
                Expect(!market.Sell(99,true) && !market.Sell(-1,true) && !market.Sell(0,true) && economy.Account.balance==140,"Invalid sale quantities cannot credit money");
                Expect(!market.Sell(1,true),"empty hands cannot sell another bird");
                market.Close();Position(second.transform.position+Vector3.back*.6f);Expect(second.TrySteal(),"after selling, player can pick up another chicken");Position(market.counter.position+Vector3.back*.8f-Vector3.up);market.Open();
                Expect(market.Sell(1,true) && !market.Sell(1,true) && economy.Account.balance==185,"Empty backpack cannot be sold twice");
                handBefore=market.merchant.gestureBone.localRotation;
                ScreenCapture.CaptureScreenshot(Output+"/trade-ui.png");
                next=Time.realtimeSinceStartup+.35f;
            }
            else if(phase==3)
            {
                Expect(Quaternion.Angle(handBefore,market.merchant.gestureBone.localRotation)>5,"Merchant trade animation moves the arm");
                economy.Buy(0);Expect(economy.Account.balance==160 && economy.Account.feed==1,"Supply purchase debits account and adds feed");
                int capacity=game.backpack.capacity;economy.Buy(2);
                Expect(economy.Account.balance==70 && game.backpack.capacity==1 && economy.Account.backpackUpgrade,"tool backpack upgrade does not increase chicken carrying limit");
                economy.Buy(2);Expect(economy.Account.balance==70,"Upgrade cannot be purchased twice");
                economy.Pay(0);Expect(economy.Account.balance==70 && economy.Account.debts[0].amount==160,"Insufficient funds cannot pay a debt");
                market.Close();Expect(!VillageMarket.IsOpen,"Trade panel closes cleanly");
                // Delivery happens at the coop entrance (HomeFlockView.DeliveryPoint, 2.5 m rule since 10/09).
                Position(economy.home.GetComponentInChildren<HomeFlockView>().DeliveryPoint+Vector3.up*.1f);
                game.backpack.TryAddChicken();game.CompleteMission();game.backpack.TryAddChicken();game.CompleteMission();game.backpack.TryAddChicken();
                game.CompleteMission();
                Expect(game.backpack.chickensCarried==0 && economy.Account.flock==3 && economy.Account.day==1,"Home delivery transfers stock without advancing day before sleep");
                Expect(!game.MissionActive && game.MissionFarm==-1,"Returning home clears mission and farmer HUD state");
                game.CompleteMission();Expect(economy.Account.day==1 && economy.Account.flock==3,"Repeated delivery cannot duplicate chickens or days");
                economy.UseFeed();Expect(economy.Account.feed==0 && economy.Account.meals==5,"Home feeding consumes purchased feed");
                Expect(!market.Sell(1,false),"Remote sale outside merchant range is rejected");
                next=Time.realtimeSinceStartup+.5f;
            }
            else if(phase==4)
            {
                var flock=Object.FindFirstObjectByType<HomeFlockView>();
                Expect(flock!=null && flock.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Galinha do sitio "))==3,"Delivered chickens appear in home coop");
                Position(market.counter.position+Vector3.back*.8f-Vector3.up);
                Expect(market.Sell(1,false) && economy.Account.flock==2 && economy.Account.balance==115,"Merchant collection debits home flock and pays account");
                Expect(game.PrepareNextNight() && economy.Account.day==2,"Rest after delivery does not settle the same night twice");
                var disk=JsonUtility.FromJson<HouseholdAccount>(File.ReadAllText(SessionState.GetString(Key+".path","")));
                Expect(disk.IsValid() && disk.balance==115 && disk.flock==2,"Transactions persist to isolated save file");
                Expect(GameMenu.Instance.SaveProgress(),"Manual save persists updated account and world");
                Expect(File.Exists(economy.CheckpointPath+".bak"),"Replacing checkpoint keeps previous save backup");
                GameCheckpoint.TryRead(economy.CheckpointPath,out var saved,out var error);
                Expect(saved!=null && saved.account.balance==115 && saved.carried==0 && saved.missionFarm==-1 && saved.birds.Count(b=>b.active)==94,"Checkpoint excludes stolen birds and contains settled finances");
                var checkpointSystem=GameMenu.Instance.GetComponent<GameCheckpoint>();
                Vector3 savedPosition=game.player.position;
                game.backpack.TryAddChicken();Position(savedPosition+Vector3.right*3);
                first.gameObject.SetActive(true);
                Expect(checkpointSystem.Restore(saved,out var restoreMessage),"Checkpoint restores world and account: "+restoreMessage);
                Expect(game.backpack.chickensCarried==0 && !first.gameObject.activeSelf && Vector3.Distance(savedPosition,game.player.position)<.01f,"Load restores position, removes stale carried loot and keeps stolen birds absent");
                var restoredMovement=game.player.GetComponent<PlayerMovement>();
                restoredMovement.RestorePosture(true);
                var crouchSave=checkpointSystem.Capture();restoredMovement.RestorePosture(false);
                Expect(checkpointSystem.Restore(crouchSave,out _) && restoredMovement.estaAgachado &&
                    Mathf.Approximately(game.player.GetComponent<CharacterController>().height,restoredMovement.alturaAgachado),"Save restores crouched collider and eye height in low passages");
                restoredMovement.RestorePosture(false);
                saved.version=999;
                Expect(!checkpointSystem.Restore(saved,out _) && economy.Account.balance==115,"Incompatible save is rejected before changing finances");
                GameMenu.Instance.Pause();Expect(GameMenu.IsOpen && Time.timeScale==0,"Pause menu freezes simulation");
                GameMenu.Instance.Resume();Expect(!GameMenu.IsOpen && Time.timeScale==1,"Resume restores simulation");
                var preferences=new GamePreferences{quality=0,sensitivity=137,volume=.4f,fov=72};preferences.Apply(false);
                var pipeline=QualitySettings.renderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                Expect(pipeline!=null && Mathf.Approximately(pipeline.renderScale,.75f) && pipeline.msaaSampleCount==1,"Low quality changes actual render resolution and antialiasing");
                Expect(Mathf.Approximately(game.player.GetComponentInChildren<PlayerLook>().sensibilidade,137) && Mathf.Approximately(Camera.main.fieldOfView,72) && Mathf.Approximately(AudioListener.volume,.4f),"Settings apply sensitivity, field of view and volume to gameplay");
                preferences.quality=2;preferences.Apply(false);
                Expect(Mathf.Approximately(pipeline.renderScale,1) && pipeline.msaaSampleCount==4 && pipeline.shadowDistance==85,"High quality applies distinct rendering and shadows");
                var body=game.player.GetComponentInChildren<RuralCharacterAnimator>();
                foreach(string name in new[]{"Idle","Walk","Run","Crouch","Trade"})Expect(body.clips[name]!=null,"Character animation exists: "+name);
                game.player.GetComponent<PlayerMovement>().estaMovendo=true;
                next=Time.realtimeSinceStartup+.4f;
            }
            else if(phase==5)
            {
                Expect(game.player.GetComponentInChildren<RuralCharacterAnimator>().clips.IsPlaying("Walk"),"Player locomotion selects walk animation");
                game.player.GetComponent<PlayerMovement>().estaSprinting=true;
                next=Time.realtimeSinceStartup+.3f;
            }
            else if(phase==6)
            {
                Expect(game.player.GetComponentInChildren<RuralCharacterAnimator>().clips.IsPlaying("Run"),"Player sprint selects run animation");
                Expect(errors==0,"No runtime errors during gameplay test");
                results.AddRange(NPCIntegrationTests.Finish());
                HomeExperienceTests.Begin();next=Time.realtimeSinceStartup+1.05f;
            }
            else if(phase==7)
            {
                HomeExperienceTests.ClosePhone();next=Time.realtimeSinceStartup+.25f;
            }
            else if(phase==8)
            {
                HomeExperienceTests.StowPhone();next=Time.realtimeSinceStartup+1.05f;
            }
            else
            {
                results.AddRange(HomeExperienceTests.Finish());
                results.AddRange(CoopInteractionTests.Run());
                results.AddRange(FarmProgressionTests.Run());
                results.AddRange(OldPickupTruckTests.Run());
                results.AddRange(BackpackEquipmentTests.Run());
                Expect(errors==0,"No runtime errors during phone and mirror review");
                Finish();return;
            }
            phase++;
        }
        catch(System.Exception e){results.Add("FAIL exception: "+e);Finish();}
    }
    static void Finish()
    {
        File.WriteAllLines(Output+"/playable-tests.txt",results);
        SessionState.SetBool(Key+".passed",!results.Any(line=>line.StartsWith("FAIL") || line.StartsWith("ERROR")));
        EditorApplication.isPlaying=false;
    }
}
