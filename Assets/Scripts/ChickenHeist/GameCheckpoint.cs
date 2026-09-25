using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WorldObjectSave
{
    public string id;
    public Vector3 position;
    public Quaternion rotation;
    public bool active;
    public float value;
    public FarmerCombatSave combat;
}

[Serializable]
public class GameCheckpointData
{
    public int version=1,missionFarm=-1,carried;
    public string scene,savedAt;
    public Vector3 position;
    public float yaw,pitch;
    public bool nightSettled,crouched;
    public bool hasTruck;
    public Vector3 truckPosition;
    public float truckYaw;
    public bool hasTruckRotation;
    public Quaternion truckRotation=Quaternion.identity;
    public HouseholdAccount account;
    public List<WorldObjectSave> birds=new List<WorldObjectSave>();
    public List<WorldObjectSave> coops=new List<WorldObjectSave>();
    public List<WorldObjectSave> farmers=new List<WorldObjectSave>();
    public List<WorldObjectSave> gates=new List<WorldObjectSave>();
    public List<WorldObjectSave> doors=new List<WorldObjectSave>();
    public List<WorldObjectSave> cameras=new List<WorldObjectSave>();
    public List<WorldObjectSave> traps=new List<WorldObjectSave>();
    public float trapSlowRemaining;
    public bool hasHealth;
    public float health;
    public bool Valid => version==1 && account!=null && account.IsValid() && carried>=0 && carried<=11 && missionFarm>=-1 &&
        (!hasHealth || float.IsFinite(health) && health>0 && health<=PlayerHealth.Maximum) &&
        !string.IsNullOrEmpty(scene) && birds!=null && coops!=null && farmers!=null && gates!=null && doors!=null &&
        float.IsFinite(position.x) && float.IsFinite(position.y) && float.IsFinite(position.z) && float.IsFinite(yaw) && float.IsFinite(pitch) &&
        (!hasTruck || float.IsFinite(truckPosition.x) && float.IsFinite(truckPosition.y) && float.IsFinite(truckPosition.z) && float.IsFinite(truckYaw)) &&
        (!hasTruckRotation || float.IsFinite(truckRotation.x) && float.IsFinite(truckRotation.y) && float.IsFinite(truckRotation.z) && float.IsFinite(truckRotation.w) && Quaternion.Dot(truckRotation,truckRotation)>.9f && Quaternion.Dot(truckRotation,truckRotation)<1.1f);
}

public class GameCheckpoint : MonoBehaviour
{
    // IDs are captured before pickups destroy objects and change sibling indices.
    readonly Dictionary<string,InteractableChicken> birds=new Dictionary<string,InteractableChicken>();
    readonly Dictionary<string,ChickenCoopLockpick> coops=new Dictionary<string,ChickenCoopLockpick>();
    readonly Dictionary<string,FarmerSleepSystem> farmers=new Dictionary<string,FarmerSleepSystem>();
    readonly Dictionary<string,RuralGate> gates=new Dictionary<string,RuralGate>();
    readonly Dictionary<string,HomeDoor> doors=new Dictionary<string,HomeDoor>();
    readonly Dictionary<string,SecurityCamera> cameras=new Dictionary<string,SecurityCamera>();
    readonly Dictionary<string,TrapSystem> traps=new Dictionary<string,TrapSystem>();
    readonly Dictionary<string,string> legacyIds=new Dictionary<string,string>();
    void Awake()
    {
        Index(birds);Index(coops);Index(farmers);Index(gates);Index(doors);Index(cameras);Index(traps);
    }
    void Index<T>(Dictionary<string,T> map) where T:Component
    {
        foreach(var item in FindObjectsByType<T>())
        {
            var id=item.GetComponent<PersistentWorldId>();string key=id!=null && !string.IsNullOrEmpty(id.value)?id.value:Id(item.transform);
            map.Add(key,item);
            if(id!=null && !string.IsNullOrEmpty(id.legacyPath))legacyIds[id.legacyPath]=key;
        }
    }
    static string Id(Transform item)
    {
        string id="";
        while(item!=null){id="/"+item.GetSiblingIndex()+":"+item.name+id;item=item.parent;}
        return id;
    }
    public static bool TryRead(string path,out GameCheckpointData data,out string error)
    {
        data=null;error="";
        try
        {
            if(!File.Exists(path)){error="Nenhum jogo salvo ainda.";return false;}
            data=JsonUtility.FromJson<GameCheckpointData>(File.ReadAllText(path));
            if(data==null || !data.Valid)throw new InvalidDataException("Arquivo de progresso invalido.");
            return true;
        }
        catch(Exception e){data=null;error="Nao foi possivel carregar: "+e.Message;return false;}
    }
    public bool Save(out string message)
    {
        var game=HeistGameManager.Instance;var economy=HouseholdEconomy.Instance;
        if(ChickenScare.Active!=null){message="Aguarde a galinha voltar antes de salvar.";return false;}
        if(OldPickupTruck.IsDriving){message="Estacione e saia da caminhonete antes de salvar.";return false;}
        if(game==null || economy==null || !economy.Ready || game.missionEnded || TruckCageLids.Active!=null || OldPickupTruck.IsDriving || coops.Values.Any(c=>c!=null && c.ChallengeActive))
        {message="Conclua a interacao antes de salvar.";return false;}
        try
        {
            var data=Capture();string path=economy.CheckpointPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path+".tmp",JsonUtility.ToJson(data,true));
            if(File.Exists(path))File.Replace(path+".tmp",path,path+".bak");else File.Move(path+".tmp",path);
            message="Jogo salvo.";return true;
        }
        catch(Exception e){message="Nao foi possivel salvar: "+e.Message;return false;}
    }
    public GameCheckpointData Capture()
    {
        var game=HeistGameManager.Instance;
        var data=new GameCheckpointData{scene=game.gameObject.scene.path,savedAt=DateTime.Now.ToString("g"),
            account=JsonUtility.FromJson<HouseholdAccount>(JsonUtility.ToJson(HouseholdEconomy.Instance.Account)),
            position=game.player.position,yaw=game.player.eulerAngles.y,pitch=game.player.GetComponentInChildren<PlayerLook>().Pitch,
            carried=game.backpack.chickensCarried,missionFarm=game.MissionFarm,nightSettled=game.NightSettled,
            crouched=game.player.GetComponent<PlayerMovement>().estaAgachado};
        if(OldPickupTruck.Instance!=null){data.hasTruck=true;data.truckPosition=OldPickupTruck.Instance.vehicle.Body.position;data.truckYaw=OldPickupTruck.Instance.transform.eulerAngles.y;data.hasTruckRotation=true;data.truckRotation=OldPickupTruck.Instance.vehicle.Body.rotation;}
        foreach(var pair in birds)data.birds.Add(State(pair.Key,pair.Value,pair.Value!=null && pair.Value.gameObject.activeSelf));
        foreach(var pair in coops)data.coops.Add(State(pair.Key,pair.Value,pair.Value.IsOpen));
        foreach(var pair in farmers){var s=State(pair.Key,pair.Value,true);s.value=pair.Value.CurrentSleep;s.combat=pair.Value.GetComponent<FarmerStateMachine>()?.Capture();data.farmers.Add(s);}
        foreach(var pair in gates)data.gates.Add(State(pair.Key,pair.Value,pair.Value.IsOpen));
        foreach(var pair in doors)data.doors.Add(State(pair.Key,pair.Value,pair.Value.opened));
        foreach(var pair in cameras){var s=State(pair.Key,pair.Value,true);s.value=pair.Value.PaintSecondsRemaining;data.cameras.Add(s);}
        foreach(var pair in traps)data.traps.Add(State(pair.Key,pair.Value,pair.Value.triggered));
        data.trapSlowRemaining=game.player.GetComponent<PlayerMovement>().TrapSlowRemaining;
        data.hasHealth=true;data.health=game.player.GetComponent<PlayerHealth>()?.Current??PlayerHealth.Maximum;
        return data;
    }
    static WorldObjectSave State(string id,Component obj,bool active)=>new WorldObjectSave{id=id,active=active,
        position=obj!=null?obj.transform.position:Vector3.zero,rotation=obj!=null?obj.transform.rotation:Quaternion.identity};
    public bool Restore(GameCheckpointData data,out string message)
    {
        message="Progresso carregado.";
        var game=HeistGameManager.Instance;
        // Migrate legacy hierarchy IDs on a copy; never modify the user's source save.
        if(data!=null){data=JsonUtility.FromJson<GameCheckpointData>(JsonUtility.ToJson(data));
            foreach(var list in new[]{data.birds,data.coops,data.farmers,data.gates,data.doors,data.cameras,data.traps})
                if(list!=null)foreach(var state in list)if(state!=null && state.id!=null && legacyIds.TryGetValue(state.id,out var stable))state.id=stable;
        }
        // Reject a changed world before touching either the account or the scene.
        if(data==null || !data.Valid || data.scene!=game.gameObject.scene.path ||
            !Matches(data.birds,birds) || !Matches(data.coops,coops) || !Matches(data.farmers,farmers) ||
            !Matches(data.gates,gates) || !Matches(data.doors,doors) ||
            data.farmers.Any(s=>s.combat!=null && !s.combat.Valid) ||
            (data.cameras!=null && data.cameras.Count>0 && (!Matches(data.cameras,cameras) || data.cameras.Any(c=>!float.IsFinite(c.value) || c.value<0 || c.value>SecurityCamera.PaintDuration))) ||
            (data.traps!=null && data.traps.Count>0 && !Matches(data.traps,traps)) ||
            !float.IsFinite(data.trapSlowRemaining) || data.trapSlowRemaining<0 || data.trapSlowRemaining>30 ||
            data.missionFarm>=ProtagonistPhone.Instance.farmNames.Length)
        {message="Este save nao corresponde ao mundo atual. O arquivo foi preservado.";return false;}
        var restoredAccount=JsonUtility.FromJson<HouseholdAccount>(JsonUtility.ToJson(data.account));
        if(data.carried>1){restoredAccount.flock+=data.carried-1;restoredAccount.Record("Galinhas do save antigo transferidas ao sitio: "+(data.carried-1));}
        if(!HouseholdEconomy.Instance.RestoreAccount(restoredAccount)){message=HouseholdEconomy.Instance.Message;return false;}
        foreach(var coop in coops.Values)coop.CancelInteraction();
        if(data.hasTruck && OldPickupTruck.Instance!=null)OldPickupTruck.Instance.RestorePose(data.truckPosition,data.hasTruckRotation?data.truckRotation:Quaternion.Euler(0,data.truckYaw,0));
        var cc=game.player.GetComponent<CharacterController>();cc.enabled=false;
        game.player.position=data.position;game.player.rotation=Quaternion.Euler(0,data.yaw,0);
        game.player.GetComponent<PlayerMovement>().RestorePosture(data.crouched);cc.enabled=true;
        game.player.GetComponentInChildren<PlayerLook>().RestorePitch(data.pitch);
        game.backpack.RestoreCount(data.carried);game.RestoreSession(data.missionFarm,data.nightSettled,false);
        foreach(var s in data.birds)
        {
            var bird=birds[s.id];bird.gameObject.SetActive(s.active);
            if(s.active)bird.transform.SetPositionAndRotation(s.position,s.rotation);
        }
        game.chickensRemaining=data.birds.Count(s=>s.active);
        foreach(var s in data.coops)coops[s.id].RestoreOpen(s.active);
        foreach(var s in data.farmers)
        {
            var farmer=farmers[s.id];var controller=farmer.GetComponent<CharacterController>();
            controller.enabled=false;farmer.transform.SetPositionAndRotation(s.position,s.rotation);controller.enabled=true;
            farmer.RestoreSleep(s.value);
            farmer.GetComponent<FarmerStateMachine>()?.Restore(s.combat);
        }
        foreach(var s in data.gates)gates[s.id].RestoreOpen(s.active);
        foreach(var s in data.doors)doors[s.id].RestoreOpen(s.active);
        foreach(var camera in cameras.Values)camera.RestorePaint(0);
        if(data.cameras!=null)foreach(var s in data.cameras)cameras[s.id].RestorePaint(s.value);
        foreach(var trap in traps.Values)trap.triggered=false;
        if(data.traps!=null)foreach(var s in data.traps)traps[s.id].triggered=s.active;
        game.player.GetComponent<PlayerMovement>().RestoreTrapSlow(data.trapSlowRemaining);
        game.player.GetComponent<PlayerHealth>()?.Restore(data.hasHealth?data.health:PlayerHealth.Maximum);
        Physics.SyncTransforms();return true;
    }
    static bool Matches<T>(List<WorldObjectSave> states,Dictionary<string,T> map) where T:Component =>
        states.Count==map.Count && states.All(s=>s!=null && s.id!=null && map.ContainsKey(s.id)) && states.Select(s=>s.id).Distinct().Count()==states.Count;
}
