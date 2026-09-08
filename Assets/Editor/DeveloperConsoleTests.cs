using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class DeveloperConsoleTests
{
    public static List<string> Run()
    {
        var results=new List<string>();
        void Check(bool value,string message)=>results.Add((value?"PASS ":"FAIL ")+"DEV "+message);
        var console=DeveloperConsole.Instance;
        var game=HeistGameManager.Instance;
        var checkpoint=GameMenu.Instance.GetComponent<GameCheckpoint>();
        var baseline=checkpoint.Capture();
        float time=Time.timeScale;bool audio=AudioListener.pause;
        try
        {
            Check(console!=null,"console installed automatically");
            Check(console.Open() && Time.timeScale==0 && GameMenu.BlocksInput,"chat pauses world and blocks gameplay inputs");
            Check(console.ExecuteCommand(" /DEV ") && console.HelpVisible,"help command tolerates case and whitespace");
            var movement=game.player.GetComponent<PlayerMovement>();
            float walk=movement.velocidadeNormal,run=movement.velocidadeSprint,crouch=movement.velocidadeAgachado;
            Check(console.ExecuteCommand("/speed 5") && movement.DeveloperSpeedMultiplier==5,"speed command applies five-times multiplier");
            movement.estaAgachado=false;movement.estaSprinting=false;
            Check(Mathf.Approximately(movement.CurrentMoveSpeed,walk*5),"speed scales actual walking rate");
            movement.estaSprinting=true;Check(Mathf.Approximately(movement.CurrentMoveSpeed,run*5),"speed scales sprinting rate");
            movement.estaAgachado=true;Check(Mathf.Approximately(movement.CurrentMoveSpeed,crouch*5),"speed scales crouching rate");
            foreach(string invalid in new[]{"/speed 0","/speed -1","/speed 21","/speed 2.5","/speed NaN","/speed","/speed 5 extra"})
                Check(!console.ExecuteCommand(invalid) && movement.DeveloperSpeedMultiplier==5,"invalid speed leaves multiplier unchanged: "+invalid);
            Check(console.ExecuteCommand("/speed 20") && movement.DeveloperSpeedMultiplier==20,"maximum developer speed accepted");
            Check(console.ExecuteCommand("/speed 1") && movement.velocidadeNormal==walk && movement.velocidadeSprint==run && movement.velocidadeAgachado==crouch,"normal speed restored without modifying original tuning");
            movement.estaAgachado=false;movement.estaSprinting=false;
            Check(!console.ExecuteCommand("/fase lixo") && !console.ExecuteCommand("/fase 0") && !console.ExecuteCommand("/fase 999"),"invalid phases rejected");
            Check(!game.MissionActive,"invalid phase leaves mission untouched");
            var phone=ProtagonistPhone.Instance;
            for(int i=0;i<phone.farmNames.Length;i++)
            {
                Check(console.ExecuteCommand("/fase "+(i+1)) && game.MissionFarm==i && game.MissionName==phone.farmNames[i],"phase "+(i+1)+" starts matching phone mission");
                var delta=game.player.position-phone.farmPositions[i];delta.y=0;
                Check(delta.magnitude<13,"phase "+(i+1)+" lands near entrance");
            }
            Check(console.ExecuteCommand("/sono 100") && game.farmerSleep.CurrentSleep==100,"wake farmer");
            Check(console.ExecuteCommand("/sono 0") && game.farmerSleep.CurrentSleep==game.farmerSleep.minimumSleep,"reset farmer alert");
            Check(!console.ExecuteCommand("/sono -1") && !console.ExecuteCommand("/sono 101"),"invalid alert rejected");
            Check(console.ExecuteCommand("/abrir") && Object.FindObjectsByType<ChickenCoopLockpick>().Where(c=>game.IsMissionTarget(c)).All(c=>c.IsOpen),"open active mission coop");
            Check(console.ExecuteCommand("/fechar") && Object.FindObjectsByType<ChickenCoopLockpick>().Where(c=>game.IsMissionTarget(c)).All(c=>!c.IsOpen),"close active mission coop");
            Check(console.ExecuteCommand("/mochila 3") && game.backpack.chickensCarried==3,"test inventory set");
            int phase=game.MissionFarm;
            Check(!console.ExecuteCommand("/fase 1") && game.MissionFarm==phase && game.backpack.chickensCarried==3,"phase switch preserves carried loot");
            Check(!console.ExecuteCommand("/mochila 999") && game.backpack.chickensCarried==3,"inventory capacity enforced");
            Check(console.ExecuteCommand("/mercado") && Object.FindAnyObjectByType<VillageMarket>().PlayerInRange,"market teleport reaches trading range");
            Check(console.ExecuteCommand("/casa") && HouseholdEconomy.Instance.AtHome && game.MissionFarm==phase,"home teleport preserves mission");
            Check(console.ExecuteCommand("/cancelar") && !game.MissionActive && game.backpack.chickensCarried==3,"cancel preserves inventory without paying out");
            Check(!console.ExecuteCommand("/abrir") && !console.ExecuteCommand("/sono 50"),"mission-only commands guarded");
            Check(console.ExecuteCommand("/mochila 0") && console.ExecuteCommand("/salvar"),"clear inventory and save");
            Check(!console.ExecuteCommand("/qualquer") && !console.ExecuteCommand("/casa extra"),"unknown commands and extra arguments rejected");
            console.Close();
            Check(Time.timeScale==time && AudioListener.pause==audio && DeveloperConsole.BlocksInput,"closing restores time and audio but protects same-frame input");
            GameMenu.Instance.Pause();Check(!console.Open() && !console.ExecuteCommand("/fase 1"),"main menu owns input over console");GameMenu.Instance.Resume();
            Check(console.ExecuteCommand("/dev") && !console.HelpVisible,"help can be hidden");
        }
        finally
        {
            console.Close();GameMenu.Instance.Resume();
            game.player.GetComponent<PlayerMovement>().SetDeveloperSpeed(1);
            Check(checkpoint.Restore(baseline,out _),"test restores original world and isolated account");
        }
        return results;
    }
}
