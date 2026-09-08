using System;
using System.IO;
using UnityEngine;

public class HouseholdEconomy : MonoBehaviour
{
    public static bool ReviewSession=>Array.IndexOf(Environment.GetCommandLineArgs(),"--review-session")>=0;
    static readonly string reviewId=Guid.NewGuid().ToString("N");
    public static HouseholdEconomy Instance { get; private set; }
    public HouseholdAccount Account {get;private set;}=new HouseholdAccount();
    public string Message {get;private set;}="Nao posso perder este lugar.";
    public Transform home;
    public GameObject[] repairStages;
    public bool Ready {get;private set;}
    int baseBackpackCapacity;
    public bool AtHome => home!=null && HeistGameManager.Instance!=null && HeistGameManager.Instance.player!=null
        && Vector3.Distance(home.position,HeistGameManager.Instance.player.position)<24;
    #if UNITY_EDITOR
    [HideInInspector] public string editorTestSavePath;
    #endif
    string SavePath
    {
        get
        {
            #if UNITY_EDITOR
            if(!string.IsNullOrEmpty(editorTestSavePath))return editorTestSavePath;
            #endif
            if(ReviewSession)return Path.Combine(Application.temporaryCachePath,"ChickenHeistReview",reviewId,"account.json");
            return Path.Combine(Application.persistentDataPath,"chicken-heist-household-v1.json");
        }
    }
    void Awake()
    {
        Instance=this;
        try
        {
            if(File.Exists(SavePath))
            {
                Account=JsonUtility.FromJson<HouseholdAccount>(File.ReadAllText(SavePath));
                if(Account==null || !Account.IsValid())throw new InvalidDataException("Invalid household save");
            }
            Ready=true;
        }
        catch(Exception e){Account=new HouseholdAccount();Ready=false;Message="Falha ao ler o progresso. Arquivo preservado.";Debug.LogException(e);}
    }
    void Start(){baseBackpackCapacity=HeistGameManager.Instance?.backpack?.capacity??8;ApplyUpgrades();}
    void OnDestroy(){if(Instance==this)Instance=null;}
    public bool Commit(Func<HouseholdAccount,bool> action,string success)
    {
        if(!Ready)return false;
        var next=JsonUtility.FromJson<HouseholdAccount>(JsonUtility.ToJson(Account));
        if(!action(next)){Message="Operacao indisponivel. Confira saldo, estoque e quantidade.";return false;}
        try
        {
            SaveAccount(SavePath,next);
            Account=next;Message=success;ApplyUpgrades();return true;
        }
        catch(Exception e){Message="Nao foi possivel salvar. Nenhum valor foi descontado.";Debug.LogException(e);return false;}
    }
    public bool CompleteHeist(int chickens)=>Commit(a=>{a.ReturnFromHeist(chickens);return true;},"As galinhas chegaram ao sitio.");
    public bool DepositChickens(int chickens,bool settleNight,bool unloadTruck=false)=>Commit(a=>
    {
        if(chickens<1)return false;
        if(unloadTruck)a.truckChickens=0;
        if(settleNight)a.ReturnFromHeist(chickens);
        else{a.flock+=chickens;a.Record("Entrega adicional: +"+chickens+" galinhas");}
        RecordRaid(a,chickens);
        return true;
    },"As galinhas chegaram ao sitio.");
    static void RecordRaid(HouseholdAccount account,int chickens)
    {
        var game=HeistGameManager.Instance;
        if(game==null || !game.MissionActive)return;
        bool emptied=true;
        foreach(var bird in UnityEngine.Object.FindObjectsByType<InteractableChicken>())
            if(game.IsMissionTarget(bird)){emptied=false;break;}
        account.RegisterRaid(game.MissionName,chickens,emptied);
    }
    public bool RestUntilMorning()=>Commit(a=>{a.RestUntilMorning();return true;},"Uma nova manha. Confira as noticias no celular.");
    public bool ReadNews()=>!Account.newsUnread || Commit(a=>{a.newsUnread=false;return true;},"Noticiario atualizado.");
    public static void SaveAccount(string path,HouseholdAccount account)
    {
        if(account==null || !account.IsValid())throw new InvalidDataException("Invalid household account");
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path+".tmp",JsonUtility.ToJson(account,true));
        if(File.Exists(path))File.Replace(path+".tmp",path,path+".bak");else File.Move(path+".tmp",path);
    }
    public void Buy(int id)=>Commit(a=>a.Buy(id),id>=2?"Equipamento recebido na mochila.":"Compra recebida no estoque do sitio.");
    public bool EquipLockpick(bool professional)=>Commit(a=>a.EquipLockpick(professional),professional?"Lockpick profissional equipado.":"Lockpick basico equipado.");
    public bool UsePaint()=>Commit(a=>a.UsePaint(),"Tinta aplicada.");
    public string CheckpointPath => SavePath+".checkpoint.json";
    public bool RestoreAccount(HouseholdAccount saved)
    {
        if(saved==null || !saved.IsValid())return false;
        try{SaveAccount(SavePath,saved);Account=saved;Ready=true;ApplyUpgrades();return true;}
        catch(Exception e){Message="Nao foi possivel restaurar o progresso.";Debug.LogWarning(e.Message);return false;}
    }
    public void Pay(int id)=>Commit(a=>a.Pay(id),"Conta quitada. Uma preocupacao a menos.");
    public void UseFeed(){if(AtHome)Commit(a=>a.Feed(),"Comedouro abastecido.");else Message="Volte ao sitio para abastecer o comedouro.";}
    public void Repair(){if(AtHome)Commit(a=>a.Repair(),"Mais um remendo. Ainda da para continuar.");else Message="Volte ao sitio para reparar o galinheiro.";}
    public void Sell(){if(AtHome)Commit(a=>a.Sell(),"Venda registrada na cooperativa.");else Message="A retirada acontece no seu sitio.";}
    public bool SellAtMarket(VillageMarket market,int quantity,bool fromBackpack)
    {
        if(market==null || !market.PlayerInRange || quantity<1)return false;
        var pack=HeistGameManager.Instance?.backpack;
        if(fromBackpack && (pack==null || quantity>pack.chickensCarried))return false;
        bool sold=Commit(a=>
        {
            if(!fromBackpack)return a.Sell(quantity);
            if(quantity>(int.MaxValue-a.balance)/45)return false;
            a.balance+=quantity*45;a.Record("+ R$ "+quantity*45+" | Entrega de "+quantity+" galinha(s) na venda");RecordRaid(a,quantity);return true;
        },"Negocio fechado. O dinheiro entrou na conta.");
        if(sold && fromBackpack)pack.RemoveChickens(quantity);
        return sold;
    }
    void ApplyUpgrades()
    {
        FarmSecurityProgression.Apply();
        var pack=HeistGameManager.Instance?.backpack;
        if(pack!=null && baseBackpackCapacity>0)pack.capacity=baseBackpackCapacity+(Account.backpackUpgrade?3:0);
        if(repairStages!=null)for(int i=0;i<repairStages.Length;i++)if(repairStages[i]!=null)repairStages[i].SetActive(i<Account.repairs);
    }
}
