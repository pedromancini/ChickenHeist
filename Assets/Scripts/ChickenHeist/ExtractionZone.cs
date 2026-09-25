using UnityEngine;

public class ExtractionZone : MonoBehaviour
{
    public string returnPrompt = "Ponto de retorno: pressione E para encerrar a noite.";
    public bool PlayerInside
    {
        get
        {
            var player=HeistGameManager.Instance?.player;
            var zone=GetComponent<BoxCollider>();
            if(player==null || zone==null || !zone.enabled)return false;
            var point=transform.InverseTransformPoint(player.position+Vector3.up)-zone.center;
            var half=zone.size*.5f;
            return Mathf.Abs(point.x)<=half.x && Mathf.Abs(point.y)<=half.y && Mathf.Abs(point.z)<=half.z;
        }
    }

    public bool TryReturn()
    {
        if(GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen ||
            ChickenCoopLockpick.Active!=null || ChickenCoopLockpick.ClosedFrame==Time.frameCount ||
            OldPickupTruck.IsDriving || !PlayerInside)return false;
        var game=HeistGameManager.Instance;
        if(game==null || game.backpack==null)return false;
        if(game.MissionActive || game.backpack.chickensCarried>0 || (HouseholdEconomy.Instance?.Account.truckChickens??0)>0)
            game.CompleteMission();
        else game.ShowMessage("Voce ainda nao roubou nenhuma galinha.",2f);
        return true;
    }

    private void Update()
    {
        var game=HeistGameManager.Instance;var flock=HouseholdEconomy.Instance?.home?.GetComponentInChildren<HomeFlockView>();
        if(Input.GetKeyDown(KeyCode.G) && !GameMenu.BlocksInput && !ProtagonistPhone.IsOpen && !VillageMarket.IsOpen && ChickenCoopLockpick.Active==null && !OldPickupTruck.IsDriving && flock!=null && game?.player!=null && game.CanDeliverHere)game.CompleteMission();
        if(WorldInteraction.Pressed(this))TryReturn();
    }

    void OnGUI()
    {
        var game=HeistGameManager.Instance;
        if(GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || OldPickupTruck.IsDriving || game==null || !game.CanDeliverHere)return;
        GUI.Box(new Rect(Screen.width*.5f-200,Screen.height*.72f,400,32),"G | Entregar galinhas no galinheiro");
    }
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if(PlayerInside && HeistGameManager.Instance!=null && (HeistGameManager.Instance.MissionActive || HeistGameManager.Instance.backpack.chickensCarried>0))
            HeistGameManager.Instance.ShowMessage("Retorno ao sitio.", 2f);
    }

}
