using UnityEngine;

public class HeistGameManager : MonoBehaviour
{
    public static HeistGameManager Instance { get; private set; }

    public FarmerSleepSystem farmerSleep;
    public FarmerSleepSystem[] farmers;
    public BackpackInventory backpack;
    public Transform player;
    public int chickensRemaining;
    public int targetChickens = 3;
    public bool missionEnded { get; private set; }
    public bool missionWon { get; private set; }
    public string statusMessage = "Roube galinhas e volte ao ponto de retorno.";

    private float messageUntil;
    bool nightSettled;
    bool restPrepared;
    bool restoringSession;
    public bool MissionActive { get; private set; }
    public int MissionFarm { get; private set; } = -1;
    public string MissionName { get; private set; } = "";
    public bool NightSettled => nightSettled;
    public bool HasMessage => Time.time < messageUntil;

    public string MissionStartBlocker
    {
        get
        {
            if(MissionActive)return "Retorne ao sitio para concluir a missao atual.";
            if(missionEnded)return "Carregue o jogo ou inicie um novo jogo apos a captura.";
            if(backpack==null)return "Aguarde o personagem ficar pronto.";
            if(backpack.chickensCarried>0)return "Entregue ou venda as galinhas no colo antes de iniciar.";
            if(!restoringSession && (HouseholdEconomy.Instance?.Account.truckChickens??0)>0)
                return "Entregue as galinhas da caminhonete no ponto de retorno do sitio antes de iniciar.";
            return "";
        }
    }

    public bool StartMission(int index)
    {
        var phone=ProtagonistPhone.Instance;
        if(MissionStartBlocker.Length>0){ShowMessage(MissionStartBlocker,5);return false;}
        if(phone==null ||
            index<0 || index>=phone.farmNames.Length || index>=phone.farmPositions.Length)return false;
        FarmLayoutInfo target=null;
        foreach(var farm in FindObjectsByType<FarmLayoutInfo>())
            if(farm.identity==phone.farmNames[index]){target=farm;break;}
        var owner=target!=null?target.GetComponentInChildren<FarmerSleepSystem>():null;
        if(owner==null)return false;
        MissionActive=true;MissionFarm=index;MissionName=target.identity;farmerSleep=owner;
        missionWon=false;nightSettled=false;phone.selectedFarm=index;
        owner.RestoreSleep(owner.startingSleep);
        ShowMessage("Missao iniciada: "+MissionName+". Retorne ao sitio com as galinhas.",5);
        MissionNavigation.Instance?.Refresh();
        return true;
    }
    public bool IsMissionFarmer(FarmerSleepSystem farmer)=>MissionActive && farmer==farmerSleep;
    public bool IsMissionTarget(Component item)
    {
        var farm=item.GetComponentInParent<FarmLayoutInfo>();
        return MissionActive && farm!=null && farm.identity==MissionName;
    }
    public void EndActiveMission()
    {
        MissionNavigation.Instance?.Clear();
        MissionActive=false;MissionFarm=-1;MissionName="";
        if(farmerSleep!=null)farmerSleep.RestoreSleep(farmerSleep.minimumSleep);
        farmerSleep=null;
        if(ProtagonistPhone.Instance!=null)ProtagonistPhone.Instance.selectedFarm=-1;
    }
    public void RestoreSession(int farmIndex,bool settled,bool failed)
    {
        restPrepared=false;
        EndActiveMission();missionEnded=false;
        int carried=backpack.chickensCarried;backpack.RestoreCount(0);
        restoringSession=true;if(farmIndex>=0)StartMission(farmIndex);restoringSession=false;
        backpack.RestoreCount(carried);nightSettled=settled;missionEnded=failed;
        if(failed)EndActiveMission();
    }

    private void Awake()
    {
        Instance = this;
        if(GetComponent<InteractionFocusHUD>()==null)gameObject.AddComponent<InteractionFocusHUD>();
        if(GetComponent<MissionNavigation>()==null)gameObject.AddComponent<MissionNavigation>();
        if(player!=null && player.GetComponent<PlayerHealth>()==null)player.gameObject.AddComponent<PlayerHealth>();
    }

    private void Update()
    {
        if(GameMenu.IsOpen)return;

        if (MissionActive && !missionEnded && farmerSleep != null && farmerSleep.CurrentSleep >= 100f)
        {
            statusMessage = farmerSleep.name + " acordou. Fuja!";
        }

        if (MissionActive && Time.time > messageUntil && missionEnded == false && backpack != null)
        {
            if (backpack.IsFull)
                statusMessage = "Galinha no colo. Leve-a a uma gaiola ou ao sitio.";
            else if (backpack.chickensCarried >= targetChickens)
                statusMessage = "Voce ja tem o suficiente. Extrair agora seria inteligente.";
        }
    }

    public void RegisterChicken()
    {
        chickensRemaining++;
    }

    public void ChickenStolen()
    {
        player.GetComponentInChildren<RuralCharacterAnimator>()?.Pickup();
        chickensRemaining = Mathf.Max(0, chickensRemaining - 1);
        ShowMessage("Galinha roubada. O sono do fazendeiro ficou mais leve.", 3f);
    }

    public void ShowMessage(string message, float duration = 2f)
    {
        statusMessage = message;
        messageUntil = Time.time + duration;
    }

    public bool CanDeliverHere => player!=null && DeliveryFlock!=null && Vector3.Distance(player.position,DeliveryFlock.DeliveryPoint)<=2.5f;
    public HomeFlockView DeliveryFlock => HouseholdEconomy.Instance?.home?.GetComponentInChildren<HomeFlockView>();
    public void CompleteMission()
    {
        if (missionEnded) return;
        if(backpack==null)return;
        var home=HouseholdEconomy.Instance?.home;
        var flock=home!=null?home.GetComponentInChildren<HomeFlockView>():null;
        if(player==null || flock==null || Vector3.Distance(player.position,flock.DeliveryPoint)>2.5f){ShowMessage("Leve as galinhas ate a entrada do seu galinheiro para entregar (G).",4);return;}
        int cargo=HouseholdEconomy.Instance?.Account.truckChickens??0;
        if(cargo>0 && (OldPickupTruck.Instance==null || Vector3.Distance(OldPickupTruck.Instance.cargoPoint.position,flock.DeliveryPoint)>7))
        {if(backpack.chickensCarried==0){ShowMessage("Traga a caminhonete para perto do galinheiro.",4);return;}cargo=0;}
        int total=backpack.chickensCarried+cargo;
        if(total<1)
        {
            if(!MissionActive)return;
            if(!nightSettled && HouseholdEconomy.Instance!=null && !HouseholdEconomy.Instance.CompleteHeist(0))return;
            nightSettled=true;EndActiveMission();ShowMessage("Missao encerrada. Voce voltou ao sitio.",4);return;
        }
        if (HouseholdEconomy.Instance!=null && !HouseholdEconomy.Instance.DepositChickens(total,!nightSettled,cargo>0))
        { ShowMessage("Nao foi possivel salvar a entrega. Tente novamente.",4); return; }
        int delivered=total;
        flock.AnimateDelivery(player.position+player.forward*.5f+Vector3.up*.6f);
        backpack.RemoveChickens(backpack.chickensCarried);
        nightSettled=true;
        missionEnded = false;
        missionWon = true;
        if((HouseholdEconomy.Instance?.Account.truckChickens??0)==0)EndActiveMission();
        ShowMessage("Entrega concluida: "+delivered+" galinhas no sitio. A venda do vale esta aberta.",7);
    }

    public void FailMission(string reason)
    {
        missionEnded = true;
        missionWon = false;
        EndActiveMission();
        statusMessage = reason;
        GameMenu.Instance?.ShowFailure(reason);
    }
    public bool PrepareNextNight()
    {
        if(backpack==null || backpack.chickensCarried>0 || (HouseholdEconomy.Instance?.Account.truckChickens??0)>0 || restPrepared)return false;
        if(!nightSettled && HouseholdEconomy.Instance!=null && !HouseholdEconomy.Instance.CompleteHeist(0))return false;
        nightSettled=true;
        if(HouseholdEconomy.Instance==null || !HouseholdEconomy.Instance.RestUntilMorning())return false;
        player.GetComponent<PlayerHealth>()?.RecoverAfterRest();
        restPrepared=true;EndActiveMission();return true;
    }
}

