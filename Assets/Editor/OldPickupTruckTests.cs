using System.Collections.Generic;
using UnityEngine;

public static class OldPickupTruckTests
{
    public static List<string> Run()
    {
        var results=new List<string>();void Check(bool ok,string label)=>results.Add((ok?"PASS ":"FAIL ")+"TRUCK "+label);
        var checkpoint=GameMenu.Instance.GetComponent<GameCheckpoint>();var baseline=checkpoint.Capture();
        var game=HeistGameManager.Instance;var economy=HouseholdEconomy.Instance;var truck=OldPickupTruck.Instance;
        var movement=game.player.GetComponent<PlayerMovement>();bool oldMovement=movement.enabled;
        try
        {
            Check(truck!=null,"old pickup exists in playable home");if(truck==null)return results;
            var a=new HouseholdAccount{balance=500};
            Check(a.TruckCapacity==0,"truck has no free cages");
            for(int i=0;i<4;i++)Check(a.Buy(5) && a.truckCages==i+1 && a.balance==500-(i+1)*100,"cage purchase installs two places for 100: "+(i+1));
            Check(a.TruckCapacity==8 && !a.Buy(5),"four cages cap the vehicle at eight birds");
            Check(!new HouseholdAccount{truckCages=5}.IsValid() && !new HouseholdAccount{truckCages=1,truckChickens=3}.IsValid(),"invalid capacity and cargo rejected");
            economy.RestoreAccount(a);game.EndActiveMission();game.backpack.RestoreCount(0);Check(game.StartMission(0),"transport mission starts");
            var cc=game.player.GetComponent<CharacterController>();cc.enabled=false;game.player.position=truck.cargoPoint.position-Vector3.up;cc.enabled=true;
            game.backpack.RestoreCount(2);
            Check(truck.LoadOne() && game.backpack.chickensCarried==1 && economy.Account.truckChickens==1,"loading transfers one bird without duplication");
            Check(truck.UnloadOne() && game.backpack.chickensCarried==2 && economy.Account.truckChickens==0,"unloading returns same bird to player");
            truck.LoadOne();truck.LoadOne();
            Check(!game.PrepareNextNight(),"cannot sleep while cargo remains in truck");
            var saved=checkpoint.Capture();economy.RestoreAccount(new HouseholdAccount());
            Check(checkpoint.Restore(saved,out _) && economy.Account.truckChickens==2 && game.MissionActive,"save restores cages cargo and active mission together");
            game.CompleteMission();Check(economy.Account.truckChickens==0 && economy.Account.flock==2,"home delivery empties truck into home stock");
            game.CompleteMission();Check(economy.Account.flock==2,"repeated delivery cannot duplicate truck cargo");
            cc.enabled=false;game.player.position=truck.seat.position-truck.transform.right*1.5f;cc.enabled=true;
            Check(truck.EnterDriver() && !cc.enabled && !movement.enabled,"driver mounts with walking disabled");
            Check(!checkpoint.Save(out _),"saving asks driver to park and exit");
            results.AddRange(PickupPhysicsTests.Run(truck));
            truck.ForceExit(game.player.position);Check(cc.enabled,"exit restores character controller");
        }
        finally{if(truck!=null && truck.driving)truck.ForceExit(game.player.position);checkpoint.Restore(baseline,out _);movement.enabled=oldMovement;}
        return results;
    }
}
