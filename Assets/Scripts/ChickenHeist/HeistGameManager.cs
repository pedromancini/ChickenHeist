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

    public bool StartMission(int index)
    {
        var phone=ProtagonistPhone.Instance;
        if(MissionActive || missionEnded || backpack==null || backpack.chickensCarried>0 || (!restoringSession && (HouseholdEconomy.Instance?.Account.truckChickens??0)>0) || phone==null ||
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
                statusMessage = "Mochila cheia. Volte ao ponto de retorno.";
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

    public void CompleteMission()
    {
        if (missionEnded) return;
        if(backpack==null)return;
        int cargo=HouseholdEconomy.Instance?.Account.truckChickens??0;
        if(cargo>0 && OldPickupTruck.Instance?.AtHome!=true){ShowMessage("Traga a caminhonete ao sitio para entregar as galinhas.",4);return;}
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
        backpack.RemoveChickens(backpack.chickensCarried);
        nightSettled=true;
        missionEnded = false;
        missionWon = true;
        EndActiveMission();
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
        restPrepared=true;EndActiveMission();return true;
    }
}
