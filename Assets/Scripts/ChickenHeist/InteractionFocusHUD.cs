using UnityEngine;

public class InteractionFocusHUD : MonoBehaviour
{
    Component focus;
    void Update()
    {
        focus=GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || OldPickupTruck.IsDriving || ChickenCoopLockpick.Active!=null?null:WorldInteraction.Focus;
    }
    void OnGUI()
    {
        if(GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || OldPickupTruck.IsDriving || ChickenCoopLockpick.Active!=null)return;
        GUI.skin=UITheme.Skin;
        string label=focus is InteractableChicken?"Pegar galinha":focus is RuralGate gate?(gate.IsOpen?"Fechar portão":"Abrir portão"):
            focus is ChickenCoopLockpick coop?(coop.IsOpen?"Fechar galinheiro":"Abrir trinco"):focus is HomeDoor door?(door.opened?"Fechar porta":"Abrir porta"):
            focus is HomeCoopGate coopGate?(coopGate.IsOpen?"Fechar portinhola":"Abrir portinhola"):focus is HomeDoorbell?"Tocar campainha":focus is HomeNextNight?"Descansar":focus is HomePhoneDock || focus is ReceivedTabletDock?"Consultar tablet":focus is OldPickupTruck?"Guardar galinha":focus is VillageMarket?"Conversar com vendedor":focus is ExtractionZone?"Concluir entrega":"";
        if(label.Length>0)UITheme.KeyPrompt(Screen.width*.5f,Screen.height*.62f,"E",label);
        var game=HeistGameManager.Instance;var economy=HouseholdEconomy.Instance;
        if(game!=null && economy!=null && !game.MissionActive)
        {
            // Objective card, top-left (the mission card takes this place during a mission).
            float s=UITheme.Scale,w=UITheme.Size(380);var body=new GUIStyle(UITheme.Style("body")){fontSize=UITheme.Size(16)};
            string objective=economy.Account.CampaignObjective;float h=body.CalcHeight(new GUIContent(objective),w-28*s)+UITheme.Size(40);
            var r=new Rect(UITheme.Size(20),UITheme.Size(20),w,h);UITheme.Panel(r,UITheme.PanelSoft);
            GUI.Label(new Rect(r.x+14*s,r.y+8*s,w-28*s,20*s),"OBJETIVO  ·  DIA "+economy.Account.day,UITheme.Style("caption"));
            GUI.Label(new Rect(r.x+14*s,r.y+26*s,w-28*s,h-30*s),objective,body);
        }
    }
}
