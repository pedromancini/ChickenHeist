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
            Check(a.truckCages==1 && a.TruckCapacity==2 && a.balance==500,"new game includes one free cage for two birds");
            for(int i=1;i<4;i++)Check(a.Buy(5) && a.truckCages==i+1 && a.balance==500-i*100,"cage purchase installs two places for 100: "+(i+1));
            Check(a.TruckCapacity==8 && !a.Buy(5),"four cages cap the vehicle at eight birds");
            Check(!new HouseholdAccount{truckCages=5}.IsValid() && !new HouseholdAccount{truckCages=1,truckChickens=3}.IsValid(),"invalid capacity and cargo rejected");
            economy.RestoreAccount(a);game.EndActiveMission();game.backpack.RestoreCount(0);Check(game.StartMission(0),"transport mission starts");
            var cc=game.player.GetComponent<CharacterController>();cc.enabled=false;game.player.position=truck.cargoPoint.position-Vector3.up;cc.enabled=true;
            game.backpack.RestoreCount(1);
            Check(truck.LoadOne() && game.backpack.chickensCarried==0 && economy.Account.truckChickens==1,"loading transfers one bird without duplication");
            Check(truck.UnloadOne() && game.backpack.chickensCarried==1 && economy.Account.truckChickens==0,"unloading returns same bird to player");
            truck.LoadOne();
            Check(!game.PrepareNextNight(),"cannot sleep while cargo remains in truck");
            var saved=checkpoint.Capture();economy.RestoreAccount(new HouseholdAccount());
            Check(checkpoint.Restore(saved,out _) && economy.Account.truckChickens==1 && game.MissionActive,"save restores cages cargo and active mission together");
            // Truck deliveries need the player at the coop entrance and the cargo within 7 m of it.
            var entrance=economy.home.GetComponentInChildren<HomeFlockView>().DeliveryPoint;
            var parked=truck.transform.position;
            truck.transform.position+=entrance+(truck.cargoPoint.position-truck.transform.position).normalized*-4-truck.cargoPoint.position;
            cc.enabled=false;game.player.position=entrance+Vector3.up*.1f;cc.enabled=true;Physics.SyncTransforms();
            game.CompleteMission();Check(economy.Account.truckChickens==0 && economy.Account.flock==1,"home delivery empties truck into home stock");
            game.CompleteMission();Check(economy.Account.flock==1,"repeated delivery cannot duplicate truck cargo");
            truck.transform.position=parked;Physics.SyncTransforms();
            cc.enabled=false;game.player.position=truck.seat.position-truck.transform.right*1.5f;cc.enabled=true;
            Check(truck.EnterDriver() && !cc.enabled && !movement.enabled,"driver mounts with walking disabled");
            Check(!checkpoint.Save(out _),"saving asks driver to park and exit");
                        Check(!truck.ignition.EngineRunning,"entering driver seat leaves engine off");
            truck.Drive(1,0,false,.02f);truck.vehicle.PhysicsStep(.02f);
            Check(truck.vehicle.axles[2].motorTorque==0,"accelerator cannot bypass ignition challenge");
            Check(truck.ignition.Begin(),"ignition challenge starts");
            var flags=System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance;
            typeof(TruckIgnition).GetField("<CursorPosition>k__BackingField",flags).SetValue(truck.ignition,1f);
            Check(!truck.ignition.Confirm() && !truck.ignition.EngineRunning,"mistimed ignition keeps engine off");
            typeof(TruckIgnition).GetField("cooldown",flags).SetValue(truck.ignition,0f);
            truck.ignition.Begin();
            for(int i=0;i<2;i++){typeof(TruckIgnition).GetField("<CursorPosition>k__BackingField",flags).SetValue(truck.ignition,truck.ignition.Target);Check(truck.ignition.Confirm(),"timed ignition contact "+i);}
            Check(truck.ignition.EngineRunning,"two contacts start engine");
            results.AddRange(PickupPhysicsTests.Run(truck));
            truck.ForceExit(game.player.position);Check(cc.enabled,"exit restores character controller");
        }
        finally{if(truck!=null && truck.driving)truck.ForceExit(game.player.position);checkpoint.Restore(baseline,out _);movement.enabled=oldMovement;}
        return results;
    }
}
