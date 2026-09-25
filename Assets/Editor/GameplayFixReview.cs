using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class GameplayFixReview
{
    const string Key="GameplayFixReview",Folder="output/gameplay-fix-review";
    static readonly List<string> results=new List<string>();static bool failed;static int phase;static float next;
    static HeistGameManager game;static VehicleImpactReaction reaction;static Vector3 before;static Quaternion look;
    static GameplayFixReview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void InstallAndRun()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        if(!File.Exists(Folder+"/scene-before-fixes.unity"))File.Copy(RuralWorldReview.WorldScene,Folder+"/scene-before-fixes.unity");
        var roads=Object.FindObjectsByType<RuralRoadSpan>(FindObjectsSortMode.None);
        var forest=GameObject.Find("Floresta Low Poly");int cleared=0;
        if(forest!=null)foreach(var plant in forest.GetComponentsInChildren<Transform>().Where(t=>t.GetComponent<RuralTreeRoots>()!=null || t.name.Contains("(Clone)") && (t.name.ToLowerInvariant().Contains("tree") || t.name.ToLowerInvariant().Contains("bush"))))
        {
            var bounds=ProceduralFarmGenerator.VisualBounds(plant.gameObject);
            float radius=Mathf.Min(4,Mathf.Max(bounds.extents.x,bounds.extents.z));
            bool overlaps=roads.Any(road=>{
                Vector3 a=road.start,b=road.end,p=bounds.center;a.y=b.y=p.y=0;var d=b-a;
                float t=d.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude):0;
                return Vector3.Distance(p,a+d*t)<road.width*.5f+radius+.8f;
            });
            if(overlaps){plant.gameObject.SetActive(false);cleared++;}
        }
        File.WriteAllText(Folder+"/vegetation.txt",cleared+" vegetation objects disabled along road corridors; originals preserved in scene.");
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        var account=Object.FindAnyObjectByType<HouseholdEconomy>();account.editorTestSavePath=Path.GetFullPath("Temp/fixes-"+Guid.NewGuid().ToString("N")+".json");HouseholdEconomy.SaveAccount(account.editorTestSavePath,new HouseholdAccount());
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=-1;next=Time.realtimeSinceStartup+4;results.Clear();failed=false;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);EditorApplication.Exit(failed?1:0);}
    }
    static void Check(bool ok,string text){failed|=!ok;results.Add((ok?"PASS ":"FAIL ")+text);File.WriteAllLines(Folder+"/checks.txt",results);}
    static void Position(Vector3 p){var cc=game.player.GetComponent<CharacterController>();cc.enabled=false;game.player.position=p;cc.enabled=true;Physics.SyncTransforms();}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try
        {
            if(phase==-1){game=HeistGameManager.Instance;GameMenu.Instance.Resume();phase=0;next=Time.realtimeSinceStartup+.2f;return;}
            if(phase==0)
            {
                game=HeistGameManager.Instance;
                var movement=game.player.GetComponent<PlayerMovement>();var cc=game.player.GetComponent<CharacterController>();cc.Move(Vector3.down*.2f);
                Check(movement.TryJump(),"Jump starts from grounded controller");Check(movement.VerticalSpeed>0,"Jump creates upward speed");Check(!movement.TryJump(),"No double jump");movement.RestorePosture(false);
                foreach(var gate in Object.FindObjectsByType<RuralGate>())
                {
                    var p=gate.transform.position;var q=gate.transform.rotation;gate.RestoreOpen(true);
                    Check(Mathf.Abs(gate.transform.position.y-p.y)<.01f && gate.GetComponentsInChildren<Renderer>().All(r=>r.enabled),"Gate swings visibly above ground: "+gate.name);
                    gate.RestoreOpen(false);Check(Vector3.Distance(p,gate.transform.position)<.001f && Quaternion.Angle(q,gate.transform.rotation)<.01f,"Gate closes back to original pose");
                }
                var coop=Object.FindAnyObjectByType<ChickenCoopLockpick>();coop.RestoreOpen(true);Check(coop.door.GetComponentsInChildren<Renderer>().All(r=>r.enabled),"Opened coop door stays visible");coop.RestoreOpen(false);Check(!coop.IsOpen && coop.door.GetComponentsInChildren<Collider>().All(c=>c.enabled),"Coop closes with collisions");
                var economy=HouseholdEconomy.Instance;var flock=economy.home.GetComponentInChildren<HomeFlockView>();
                game.backpack.TryAddChicken();int count=economy.Account.flock;
                Position(flock.transform.position+Vector3.back*25);game.CompleteMission();Check(economy.Account.flock==count && game.backpack.chickensCarried==1,"Remote delivery rejected without consuming bird");
                Position(flock.transform.position+Vector3.back*3);game.CompleteMission();Check(economy.Account.flock==count+1 && game.backpack.chickensCarried==0,"Delivery succeeds beside coop exactly once");
                var walker=Object.FindAnyObjectByType<RoadsideWalker>();reaction=walker.gameObject.AddComponent<VehicleImpactReaction>();Check(!reaction.Hit(Vector3.forward),"Slow contact does not knock down NPC");Check(reaction.Hit(Vector3.forward*6) && reaction.Down,"Vehicle impact knocks down NPC");
                var nav=MissionNavigation.Instance;nav.Route.Clear();nav.Route.Add(Vector3.zero);nav.Route.Add(Vector3.forward*30);Check(nav.DistanceToRoute(Vector3.forward*12)<.01f,"Walking along route does not require recalculation");Check(nav.DistanceToRoute(Vector3.right*15)>14,"Off-route movement is detected");nav.Clear();
                var truck=OldPickupTruck.Instance;before=game.player.position;look=Camera.main.transform.rotation;truck.cageLids.CloseFullCage(0);
                next=Time.realtimeSinceStartup+1.6f;phase++;return;
            }
            if(phase==1)
            {
                Check((game.player.position-before).magnitude<.2f,"Lid closure does not drag player around vehicle");Check(Quaternion.Angle(look,Camera.main.transform.rotation)<.1f,"Lid closure does not force camera angle");
                Check(TruckCageLids.Active==null && game.player.GetComponent<PlayerMovement>().enabled,"Lid closure returns control");
                next=Time.realtimeSinceStartup+4;phase++;return;
            }
            if(phase==2){Check(!reaction.Down && reaction.GetComponent<RoadsideWalker>().enabled,"NPC recovers after vehicle impact");EditorApplication.isPlaying=false;}
        }
        catch(Exception e){Check(false,e.ToString());EditorApplication.isPlaying=false;}
    }
}

