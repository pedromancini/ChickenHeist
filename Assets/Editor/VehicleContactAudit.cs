using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class VehicleContactAudit
{
    const string Key="VehicleContactAudit";static int phase;static float next;static bool failed;
    static OldPickupTruck truck;static RoadsideWalker person;static VehicleImpactReaction reaction;static Vector3 origin;static GameObject wall;
    static readonly List<string> checks=new List<string>();
    static VehicleContactAudit(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void Begin(){EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);var e=Object.FindAnyObjectByType<HouseholdEconomy>();e.editorTestSavePath=Path.GetFullPath("Temp/vehicle-audit-"+Guid.NewGuid()+".json");HouseholdEconomy.SaveAccount(e.editorTestSavePath,new HouseholdAccount());SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    static void Changed(PlayModeStateChange s){if(!SessionState.GetBool(Key,false))return;if(s==PlayModeStateChange.EnteredPlayMode){next=Time.realtimeSinceStartup+4;phase=0;failed=false;checks.Clear();}if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(failed?1:0);}}
    static void Check(bool ok,string message){failed|=!ok;checks.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines("output/vehicle-contact-checks.txt",checks);}
    static void Person(Vector3 p){var cc=person.GetComponent<CharacterController>();cc.enabled=false;person.transform.position=p;cc.enabled=true;Physics.SyncTransforms();}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try{
            if(phase==0){GameMenu.Instance.Resume();next=Time.realtimeSinceStartup+.2f;phase++;return;}
            if(phase==1){truck=OldPickupTruck.Instance;person=Object.FindAnyObjectByType<RoadsideWalker>();person.enabled=false;reaction=person.GetComponent<VehicleImpactReaction>()??person.gameObject.AddComponent<VehicleImpactReaction>();origin=HouseholdEconomy.Instance.home.position+new Vector3(-3,.1f,-20);truck.RestorePose(origin,Quaternion.identity);Person(origin+Vector3.forward*2.6f);truck.vehicle.Body.linearVelocity=Vector3.forward*5;truck.vehicle.SendMessage("CheckPedestrians");Check(reaction.Down,"Actual vehicle proximity query detects front pedestrian");truck.vehicle.Body.linearVelocity=Vector3.zero;next=Time.realtimeSinceStartup+7.2f;phase++;return;}
            if(phase==2){Check(!reaction.Down,"Front contact recovery completes");truck.RestorePose(origin,Quaternion.identity);Person(origin-Vector3.forward*2.6f);truck.vehicle.Body.linearVelocity=Vector3.back*5;truck.vehicle.SendMessage("CheckPedestrians");Check(reaction.Down,"Actual vehicle proximity query detects reverse pedestrian");truck.vehicle.Body.linearVelocity=Vector3.zero;next=Time.realtimeSinceStartup+7.2f;phase++;return;}
            if(phase==3){truck.RestorePose(origin,Quaternion.identity);Person(origin+Vector3.right*2.5f);truck.vehicle.Body.linearVelocity=Vector3.forward*5;truck.vehicle.SendMessage("CheckPedestrians");Check(!reaction.Down,"Passing outside body width does not knock pedestrian down");Person(origin+Vector3.forward*2.6f);wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=origin+Vector3.forward*1.5f+Vector3.up;wall.transform.localScale=new Vector3(3,3,.2f);Physics.SyncTransforms();truck.vehicle.SendMessage("CheckPedestrians");Check(!reaction.Down,"Solid wall prevents vehicle knockdown through barrier");truck.vehicle.Body.linearVelocity=Vector3.zero;Object.Destroy(wall);Person(origin+Vector3.right*10);truck.vehicle.ExternalControl=true;truck.vehicle.SetInput(1,0,false,true);next=Time.realtimeSinceStartup+3;phase++;return;}
            if(phase==4){Check(Vector3.Distance(truck.transform.position,origin)>1 && truck.vehicle.Body.linearVelocity.magnitude>.5f,"Wheel physics accelerates truck along road");truck.vehicle.SetInput(0,0,true,true);next=Time.realtimeSinceStartup+3;phase++;return;}
            Check(truck.vehicle.Body.linearVelocity.magnitude<.8f,"Brake stops truck");EditorApplication.isPlaying=false;
        }catch(Exception e){Check(false,e.ToString());EditorApplication.isPlaying=false;}
    }
}
