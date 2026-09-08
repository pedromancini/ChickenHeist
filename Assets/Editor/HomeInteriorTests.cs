using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class HomeInteriorTests
{
    const string Key="ChickenHeist.HomeTest";
    static readonly List<string> checks=new List<string>();
    static float started,lastTick;
    static int phase,errors;
    static bool walking;
    static CharacterController controller;
    static Transform home;
    static void InitializeRuntime()
    {
        started=lastTick=Time.realtimeSinceStartup;phase=0;errors=0;walking=false;checks.Clear();
        home=GameObject.Find("Casa do Protagonista - Sitio do Recomeco").transform;
        var player=HeistGameManager.Instance.player;controller=player.GetComponent<CharacterController>();
        controller.enabled=false;player.position=home.TransformPoint(new Vector3(-3.22f,.65f,.8f));player.rotation=Quaternion.identity;controller.enabled=true;
        Camera.main.transform.localRotation=Quaternion.identity;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        home.GetComponentInChildren<HomeDoor>().opened=true;Application.runInBackground=true;
    }
    static HomeInteriorTests()
    {
        EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=Tick;
        Application.logMessageReceived+=(message,stack,type)=>{if(SessionState.GetBool(Key,false) && (type==LogType.Error || type==LogType.Exception))errors++;};
    }
    [MenuItem("Chicken Heist/Test Home and Phone")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        Checks();EditorSceneManager.SaveOpenScenes();
        foreach(var look in Object.FindObjectsByType<PlayerLook>(FindObjectsSortMode.None))look.enabled=false;
        foreach(var move in Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None))move.enabled=false;
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    public static void BuildAndRun()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        PlayerHomeBuilder.BuildInterior();Run();
    }
    public static void ProbePassage()
    {
        var root=GameObject.Find("Casa do Protagonista - Sitio do Recomeco").transform;
        var door=root.GetComponentInChildren<HomeDoor>();var rotation=door.hinge.localRotation;
        var probe=new GameObject("Temporary standing clearance probe");
        var cc=probe.AddComponent<CharacterController>();cc.height=2;cc.radius=.35f;cc.center=Vector3.up;
        cc.enabled=false;probe.transform.position=root.TransformPoint(new Vector3(-3.22f,.65f,.8f));cc.enabled=true;
        var report=new List<string>();
        try
        {
            door.hinge.localRotation=Quaternion.Euler(0,100,0);Physics.SyncTransforms();
            for(int i=0;i<100;i++)cc.Move(Vector3.forward*.04f);
            report.Add("After movement: "+root.InverseTransformPoint(probe.transform.position));
            for(float z=.8f;z<4.1f;z+=.2f)
            {
                Vector3 feet=root.TransformPoint(new Vector3(-3.22f,.65f,z));
                foreach(var obstacle in Physics.OverlapCapsule(feet+Vector3.up*.36f,feet+Vector3.up*1.64f,.34f,~0,QueryTriggerInteraction.Ignore))
                    if(obstacle!=cc && obstacle.bounds.max.y>feet.y+.1f)report.Add("z="+z+" obstacle="+obstacle.name+" bounds="+obstacle.bounds);
            }
            report.Add(root.InverseTransformPoint(probe.transform.position).z>4?"PASS standing passage":"FAIL standing passage");
            File.WriteAllLines("output/player-home/passage-probe.txt",report);
        }
        finally{door.hinge.localRotation=rotation;Object.DestroyImmediate(probe);Physics.SyncTransforms();}
    }
    static void Expect(bool result,string label){checks.Add((result?"PASS ":"FAIL ")+label);}
    static void Checks()
    {
        checks.Clear();
        var a=new HouseholdAccount();Expect(a.IsValid(),"Initial account valid");
        Expect(!a.Buy(-1) && a.balance==95,"Invalid product does not debit");
        Expect(a.Buy(0) && a.balance==70 && a.feed==1,"Feed purchase debits and adds stock");
        Expect(!a.Pay(0) && a.balance==70,"Insufficient balance cannot pay debt");
        Expect(!a.Buy(2) && a.balance==70,"Insufficient balance cannot buy upgrade");
        Expect(!a.Feed() && a.feed==1,"Cannot waste feed without chickens");
        a.ReturnFromHeist(3);Expect(a.day==1 && a.flock==3,"Return transfers chickens; only sleeping advances day");
        Expect(a.Feed() && a.feed==0 && a.meals==5,"Feeding consumes inventory");
        Expect(!a.Feed(),"Cannot feed twice without stock");
        a.ReturnFromHeist(1);Expect(a.balance==88 && a.meals==2 && a.flock==4,"Fed chickens yield egg revenue");
        Expect(a.Sell() && a.balance==133 && a.flock==3,"Sale removes chicken and adds funds");
        Expect(a.Buy(2) && a.balance==43 && a.backpackUpgrade,"Backpack upgrade purchase");
        a.balance=400;Expect(!a.Buy(2) && a.balance==400,"One-time upgrade cannot be charged twice");
        Expect(a.Pay(0) && a.balance==240 && a.debts[0].amount==0,"Pay debt once");
        Expect(!a.Pay(0) && a.balance==240,"Paid debt cannot be charged twice");
        Expect(!a.Pay(99),"Unknown debt rejected");
        Expect(a.Buy(1) && a.Repair() && a.repairs==1 && a.boards==0,"Repair consumes purchased kit");
        a.boards=5;a.Repair();a.Repair();Expect(!a.Repair() && a.boards==3,"Repair stages capped at three");
        string serialized=JsonUtility.ToJson(a);var loaded=JsonUtility.FromJson<HouseholdAccount>(serialized);
        Expect(loaded.IsValid() && loaded.balance==a.balance && loaded.backpackUpgrade && loaded.ledger.Count==a.ledger.Count,"Save JSON round trip retains state");
        string testPath=Path.GetFullPath("Temp/household-test-"+System.Guid.NewGuid().ToString("N")+".json");
        try
        {
            HouseholdEconomy.SaveAccount(testPath,a);a.balance=123;HouseholdEconomy.SaveAccount(testPath,a);
            var disk=JsonUtility.FromJson<HouseholdAccount>(File.ReadAllText(testPath));
            Expect(disk.balance==123 && File.Exists(testPath+".bak"),"Atomic save replaces file and retains backup");
            a.balance=-1;bool rejected=false;
            try{HouseholdEconomy.SaveAccount(testPath,a);}catch(InvalidDataException){rejected=true;}
            Expect(rejected && JsonUtility.FromJson<HouseholdAccount>(File.ReadAllText(testPath)).balance==123,"Invalid state cannot overwrite valid save");
        }
        finally{foreach(string suffix in new[]{"",".bak",".tmp"})if(File.Exists(testPath+suffix))File.Delete(testPath+suffix);}
        var phone=Object.FindFirstObjectByType<ProtagonistPhone>();
        Expect(phone!=null && phone.farmPhotos.Length==12,"Twelve recon photos wired");
        var unique=new HashSet<Texture2D>();foreach(var photo in phone.farmPhotos)if(photo!=null)unique.Add(photo);
        Expect(unique.Count==12,"Recon photos are unique assets");
        Expect(Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None).Length==12,"All target farms preserved");
        File.WriteAllLines("output/player-home/economy-tests.txt",checks);
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)
        {
            InitializeRuntime();
        }
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key,false);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
            Debug.Log("HOME TEST COMPLETE: output/player-home/*tests.txt and phone-*.png");
        }
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || phase==99)return;
        if(controller==null){if(HeistGameManager.Instance==null)return;InitializeRuntime();}
        float now=Time.realtimeSinceStartup;
        float elapsed=now-started,delta=Mathf.Min(.1f,now-lastTick);lastTick=now;
        if(elapsed>1 && phase==0)
        {
            if(Quaternion.Angle(home.GetComponentInChildren<HomeDoor>().hinge.localRotation,Quaternion.Euler(0,100,0))>1 && elapsed<8)return;
            // Fixed movement increments isolate doorway clearance from editor frame rate.
            for(int step=0;step<100;step++)controller.Move(Vector3.forward*.04f);
            walking=true;started=now-4;
            checks.Add("Player local position="+home.InverseTransformPoint(controller.transform.position)+" height="+controller.height+" center="+controller.center+" scale="+controller.transform.lossyScale);
            for(float z=.8f;z<4.1f;z+=.25f)
            {
                Vector3 feet=home.TransformPoint(new Vector3(-3.22f,.65f,z));
                foreach(var obstacle in Physics.OverlapCapsule(feet+Vector3.up*.36f,feet+Vector3.up*1.64f,.34f,~0,QueryTriggerInteraction.Ignore))
                    if(obstacle!=controller && obstacle.bounds.max.y>feet.y+.1f)checks.Add("Passage z="+z+" obstacle="+obstacle.name+" bounds="+obstacle.bounds);
            }
            Expect(walking && home.InverseTransformPoint(controller.transform.position).z>2.9f,"CharacterController walks through front door standing");
            var phone=ProtagonistPhone.Instance;phone.SetOpen(true);phone.ReviewTab=0;phase++;
        }
        if(elapsed>5 && phase==1){ScreenCapture.CaptureScreenshot("output/player-home/phone-bank.png");phase++;}
        if(elapsed>6 && phase==2){ProtagonistPhone.Instance.ReviewTab=1;phase++;}
        if(elapsed>7 && phase==3){ScreenCapture.CaptureScreenshot("output/player-home/phone-debts.png");phase++;}
        if(elapsed>8 && phase==4){ProtagonistPhone.Instance.ReviewTab=2;phase++;}
        if(elapsed>9 && phase==5){ScreenCapture.CaptureScreenshot("output/player-home/phone-photos.png");phase++;}
        if(elapsed>10 && phase==6){ProtagonistPhone.Instance.ReviewTab=3;phase++;}
        if(elapsed>11 && phase==7){ScreenCapture.CaptureScreenshot("output/player-home/phone-shop.png");phase++;}
        if(elapsed>13 && phase==8)
        {
            ProtagonistPhone.Instance.SetOpen(false);Expect(!ProtagonistPhone.IsOpen,"Phone closes and releases input");
            Expect(errors==0,"Runtime errors="+errors);File.WriteAllLines("output/player-home/runtime-tests.txt",checks);
            phase=99;controller=null;EditorApplication.isPlaying=false;
        }
    }
}
