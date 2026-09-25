using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CoopInteractionTests
{
    public static List<string> Run()
    {
        var results=new List<string>();
        void Check(bool value,string message)=>results.Add((value?"PASS ":"FAIL ")+"PADLOCK "+message);
        var game=HeistGameManager.Instance;var checkpoint=GameMenu.Instance.GetComponent<GameCheckpoint>();
        var baseline=checkpoint.Capture();var movement=game.player.GetComponent<PlayerMovement>();bool oldEnabled=movement.enabled;
        var coops=Object.FindObjectsByType<ChickenCoopLockpick>();GameObject wall=null;
        try
        {
            foreach(var coop in coops)
            {
                game.EndActiveMission();game.backpack.RestoreCount(0);coop.RestoreOpen(false);
                Check(coop.lockAnchor!=null && coop.lockAnchor.GetComponent<MeshFilter>().sharedMesh.vertexCount>100,"mesh replaces cube at "+coop.GetComponentInParent<FarmLayoutInfo>().identity);
                var controller=game.player.GetComponent<CharacterController>();controller.enabled=false;
                game.player.position=coop.InteractionPoint-coop.transform.forward*1.4f-Vector3.up*1.2f;
                movement.RestorePosture(false);movement.enabled=true;controller.enabled=true;
                Camera.main.transform.LookAt(coop.InteractionPoint);Physics.SyncTransforms();
                Check(coop.CanReachLock(),"padlock reachable at actual door");
                Check(!coop.TryBeginChallenge() && !coop.ChallengeActive,"free exploration explains required mission without starting it");
                int index=System.Array.IndexOf(ProtagonistPhone.Instance.farmNames,coop.GetComponentInParent<FarmLayoutInfo>().identity);
                Check(game.StartMission(index),"phone mission selects padlock owner");
                var returnZone=Object.FindFirstObjectByType<ExtractionZone>();
                returnZone.SendMessage("OnTriggerEnter",controller);
                Check(!returnZone.PlayerInside && !returnZone.TryReturn() && game.MissionActive,"stale return trigger cannot end mission at farm");
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.position=Vector3.Lerp(Camera.main.transform.position,coop.InteractionPoint,.5f);wall.transform.localScale=Vector3.one*.5f;
                Physics.SyncTransforms();Check(!coop.TryBeginChallenge(),"solid obstacle prevents interaction through walls");
                Object.DestroyImmediate(wall);wall=null;Physics.SyncTransforms();
                Check(coop.TryBeginChallenge() && ChickenCoopLockpick.Active==coop && !movement.enabled,"real entry method starts challenge and captures movement");
                Check(!returnZone.TryReturn() && game.MissionActive,"lockpick input cannot end mission");
                Check(!BackpackPanel.Instance.Open() && !DeveloperConsole.Instance.Open(),"inventory and dev chat cannot steal challenge");
                coop.SendMessage("EndChallenge");Check(!coop.ChallengeActive && ChickenCoopLockpick.Active==null && movement.enabled,"cancel releases character movement");
            }
        }
        finally
        {
            if(wall!=null)Object.DestroyImmediate(wall);
            foreach(var coop in coops)coop.RestoreOpen(coop.IsOpen);
            checkpoint.Restore(baseline,out _);movement.enabled=oldEnabled;
        }
        return results;
    }
}
