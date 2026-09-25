using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class AuditIntegrationReview
{
    const string Key="AuditIntegrationReview";
    static float next;static bool failed;static int phase;
    static readonly System.Collections.Generic.List<string> checks=new System.Collections.Generic.List<string>();
    static AuditIntegrationReview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void Begin(){EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);var e=Object.FindAnyObjectByType<HouseholdEconomy>();e.editorTestSavePath=Path.GetFullPath("Temp/audit-"+Guid.NewGuid()+".json");HouseholdEconomy.SaveAccount(e.editorTestSavePath,new HouseholdAccount());SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    static void Changed(PlayModeStateChange state){if(!SessionState.GetBool(Key,false))return;if(state==PlayModeStateChange.EnteredPlayMode){next=Time.realtimeSinceStartup+4;phase=0;failed=false;checks.Clear();}if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(failed?1:0);}}
    static void Check(bool value,string text){failed|=!value;checks.Add((value?"PASS ":"FAIL ")+text);File.WriteAllLines("output/audit-integration-checks.txt",checks);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try{
            if(phase++==0){GameMenu.Instance.Resume();next=Time.realtimeSinceStartup+.2f;return;}
            var game=HeistGameManager.Instance;var checkpoint=Object.FindAnyObjectByType<GameCheckpoint>();
            var account=new HouseholdAccount{balance=1000};Check(!account.CanBuy(2) && !account.Buy(2) && account.balance==1000,"Unused backpack upgrade cannot charge money");
            account.declineSeen=true;account.RegisterRaid("Test",1,false);account.RestUntilMorning();Check(!account.pendingVision && account.newsUnread,"Seen vision is not repeated; news remains available");
            var data=checkpoint.Capture();Check(data.birds.All(b=>!b.id.StartsWith("/")),"Checkpoint captures persistent IDs");
            Check(checkpoint.Restore(data,out var msg),"Persistent save round trip: "+msg);
            var ids=Object.FindObjectsByType<PersistentWorldId>();
            foreach(var list in new[]{data.birds,data.coops,data.farmers,data.gates,data.doors,data.cameras,data.traps})foreach(var state in list)state.id=ids.First(i=>i.value==state.id).legacyPath;
            Check(checkpoint.Restore(data,out msg),"Legacy hierarchy save migration: "+msg);
            var nav=MissionNavigation.Instance;nav.Route.Clear();nav.Route.Add(Vector3.zero);nav.Route.Add(Vector3.right*10);nav.Route.Add(Vector3.right*10+Vector3.forward*10);
            Check(Mathf.Abs(nav.RemainingDistance(Vector3.right*5)-15)<.01f && Mathf.Abs(nav.RemainingDistance(Vector3.right*10+Vector3.forward*5)-5)<.01f,"Remaining route distance follows player progress");nav.Clear();
            var coop=Object.FindAnyObjectByType<ChickenCoopLockpick>();coop.RestoreOpen(false);var rotation=coop.door.rotation;coop.SetDoorOpen(true);
            Check(coop.IsOpen && Quaternion.Angle(rotation,coop.door.rotation)<.01f,"Gameplay coop opening starts animation without snapping");coop.RestoreOpen(false);
            var bird=Object.FindObjectsByType<InteractableChicken>().First();Check(game.StartMission(Array.IndexOf(ProtagonistPhone.Instance.farmNames,bird.GetComponentInParent<FarmLayoutInfo>().identity)),"Start target mission");
            bird.coop.RestoreOpen(true);var cc=game.player.GetComponent<CharacterController>();cc.enabled=false;game.player.position=bird.transform.position+Vector3.back*1.5f;cc.enabled=true;
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=(game.player.position+bird.transform.position)*.5f+Vector3.up;wall.transform.localScale=new Vector3(2,3,.2f);Physics.SyncTransforms();
            Check(!bird.TrySteal() && game.backpack.chickensCarried==0,"Solid obstacle prevents chicken pickup");Object.Destroy(wall);game.EndActiveMission();
            EditorApplication.isPlaying=false;
        }catch(Exception e){Check(false,e.ToString());EditorApplication.isPlaying=false;}
    }
}
