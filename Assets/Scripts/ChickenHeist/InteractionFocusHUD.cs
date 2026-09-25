using UnityEngine;

public class InteractionFocusHUD : MonoBehaviour
{
    GUIStyle text;
    Component focus;
    void Update()
    {
        focus=GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || OldPickupTruck.IsDriving || ChickenCoopLockpick.Active!=null?null:WorldInteraction.Focus;
    }
    void OnGUI()
    {
        if(GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || OldPickupTruck.IsDriving || ChickenCoopLockpick.Active!=null)return;
        if(text==null)text=new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=16,wordWrap=true};
        string label=focus is InteractableChicken?"Pegar galinha":focus is RuralGate gate?(gate.IsOpen?"Fechar portao":"Abrir portao"):
            focus is ChickenCoopLockpick coop?(coop.IsOpen?"Fechar galinheiro":"Abrir trinco"):focus is HomeDoor door?(door.opened?"Fechar porta":"Abrir porta"):
            focus is HomeNextNight?"Descansar":focus is HomePhoneDock || focus is ReceivedTabletDock?"Consultar tablet":focus is OldPickupTruck?"Guardar galinha":focus is VillageMarket?"Conversar com vendedor":focus is ExtractionZone?"Concluir entrega":"";
        float width=Mathf.Min(390,Screen.width-32);
        if(label.Length>0){GUI.Box(new Rect((Screen.width-width)/2,Screen.height*.60f,width,38),"");GUI.Label(new Rect((Screen.width-width)/2,Screen.height*.60f,width,38),"E | "+label,text);}
        var game=HeistGameManager.Instance;var economy=HouseholdEconomy.Instance;
        if(game!=null && economy!=null && !game.MissionActive)
        {GUI.Box(new Rect(16,20,width,62),"");GUI.Label(new Rect(24,23,width-16,56),economy.Account.CampaignObjective,text);}
    }
}
