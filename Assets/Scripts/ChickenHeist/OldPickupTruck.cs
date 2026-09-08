using UnityEngine;

[DefaultExecutionOrder(100)]
public class OldPickupTruck : MonoBehaviour
{
    public static OldPickupTruck Instance {get;private set;}
    public static bool IsDriving=>Instance!=null && Instance.driving;
    public Transform seat, steeringWheel, cargoPoint;
    public Transform[] wheels;
    public GameObject[] cages, birds;
    public bool driving {get;private set;}
    public float Speed=>vehicle!=null?vehicle.SignedSpeed:0;
    public bool AtHome=>HouseholdEconomy.Instance?.home!=null && Vector3.Distance(transform.position,HouseholdEconomy.Instance.home.position)<26;
    public bool NearCargo=>Player!=null && Vector3.Distance(Player.position+Vector3.up,cargoPoint.position)<2.5f;
    Transform Player=>HeistGameManager.Instance?.player;
    public PickupVehiclePhysics vehicle;
    float nextNoise;
    Quaternion wheelBase;
    bool movementEnabled;
    void Awake(){Instance=this;vehicle=GetComponent<PickupVehiclePhysics>();if(steeringWheel!=null)wheelBase=steeringWheel.localRotation;}
    void OnDestroy(){if(Instance==this)Instance=null;}
    void Update()
    {
        var economy=HouseholdEconomy.Instance;if(economy==null || Player==null)return;
        for(int i=0;i<cages.Length;i++)cages[i].SetActive(i<economy.Account.truckCages);
        for(int i=0;i<birds.Length;i++)birds[i].SetActive(i<economy.Account.truckChickens);
        if(GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen)return;
        if(driving)
        {
            if(Input.GetKeyDown(KeyCode.F)){ExitDriver();return;}
            if(!vehicle.ExternalControl)Drive(Input.GetAxisRaw("Vertical"),Input.GetAxisRaw("Horizontal"),Input.GetKey(KeyCode.Space),Time.deltaTime);
            if(Mathf.Abs(Speed)>1 && Time.time>nextNoise)
            {nextNoise=Time.time+1.2f;NoiseEmitter.EmitGlobal(NoiseSource.TrapTriggered,transform.position,.35f);}
        }
        else
        {
            if(Input.GetKeyDown(KeyCode.F) && Vector3.Distance(Player.position,seat.position)<3)EnterDriver();
            if(NearCargo)
            {
                if(Input.GetKeyDown(KeyCode.E))LoadOne();
                if(Input.GetKeyDown(KeyCode.R))UnloadOne();
                if(Input.GetKeyDown(KeyCode.G) && AtHome)HeistGameManager.Instance.CompleteMission();
            }
        }
    }
    public void Drive(float throttle,float turn,bool brake,float dt)
    {
        if(!driving)return;
        vehicle.SetInput(throttle,turn,brake,true);
    }
    void LateUpdate()
    {
        if(driving && Player!=null)
        {
            Player.position=seat.position;Player.rotation=transform.rotation;
            if(steeringWheel!=null)steeringWheel.localRotation=wheelBase*Quaternion.AngleAxis(-vehicle.SteeringAngle*3,Vector3.forward);
        }
    }
    public bool EnterDriver()
    {
        if(driving || GameMenu.BlocksInput || ChickenCoopLockpick.Active!=null || Player==null || Vector3.Distance(Player.position,seat.position)>3)return false;
        if(HeistGameManager.Instance.backpack.chickensCarried>0)
        {HeistGameManager.Instance.ShowMessage("Guarde as galinhas nas gaiolas antes de dirigir.",4);return false;}
        var movement=Player.GetComponent<PlayerMovement>();movementEnabled=movement.enabled;
        movement.RestorePosture(false);movement.enabled=false;Player.GetComponent<CharacterController>().enabled=false;
        driving=true;vehicle.SetInput(0,0,true,true);return true;
    }
    public bool ExitDriver()
    {
        if(!driving)return false;
        if(vehicle.Body.linearVelocity.magnitude>.6f){HeistGameManager.Instance.ShowMessage("Pare a caminhonete antes de sair. ESPACO freia.");return false;}
        for(int side=-1;side<=1;side+=2)
        {
            var point=transform.position+transform.right*side*1.7f+transform.forward*.5f;
            if(!Physics.Raycast(point+Vector3.up*2,Vector3.down,out var ground,5,~0,QueryTriggerInteraction.Ignore))continue;
            point=ground.point+Vector3.up*.08f;
            bool blocked=false;
            foreach(var hit in Physics.OverlapCapsule(point+Vector3.up*.4f,point+Vector3.up*1.5f,.3f,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(Player) && hit.bounds.max.y>point.y+.15f){blocked=true;break;}
            if(blocked)continue;
            ForceExit(point);return true;
        }
        HeistGameManager.Instance.ShowMessage("Sem espaco para sair. Estacione em outro lugar.");return false;
    }
    public void ForceExit(Vector3 position)
    {
        if(Player==null)return;
        driving=false;vehicle.SetInput(0,0,true,false);Player.position=position;
        Player.GetComponent<CharacterController>().enabled=true;Player.GetComponent<PlayerMovement>().enabled=movementEnabled;
    }
    public bool LoadOne()
    {
        if(driving || !NearCargo || GameMenu.BlocksInput)return false;
        var pack=HeistGameManager.Instance.backpack;var economy=HouseholdEconomy.Instance;
        if(pack.chickensCarried<1)return false;
        if(economy.Account.truckChickens>=economy.Account.TruckCapacity)
        {HeistGameManager.Instance.ShowMessage("Sem vaga nas gaiolas. Compre gaiolas na loja: 2 galinhas por R$ 100.",4);return false;}
        if(!economy.Commit(a=>{a.truckChickens++;return true;},"Galinha colocada na gaiola."))return false;
        pack.RemoveChickens(1);Player.GetComponentInChildren<RuralCharacterAnimator>()?.Gesture();
        HeistGameManager.Instance.ShowMessage("Na caminhonete: "+economy.Account.truckChickens+" / "+economy.Account.TruckCapacity);return true;
    }
    public bool UnloadOne()
    {
        if(driving || !NearCargo || GameMenu.BlocksInput)return false;
        var pack=HeistGameManager.Instance.backpack;var economy=HouseholdEconomy.Instance;
        if(pack.IsFull || economy.Account.truckChickens<1)return false;
        if(!economy.Commit(a=>{a.truckChickens--;return true;},"Galinha retirada da gaiola."))return false;
        pack.TryAddChicken();Player.GetComponentInChildren<RuralCharacterAnimator>()?.Pickup();return true;
    }
    public void RestorePosition(Vector3 position,float yaw)
    {RestorePose(position,Quaternion.Euler(0,yaw,0));}
    public void RestorePose(Vector3 position,Quaternion rotation)
    {
        if(driving)ForceExit(Player.position);
        vehicle.ResetPose(position,rotation);
    }
    void OnGUI()
    {
        if(GameMenu.IsOpen || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || Player==null)return;
        string prompt=driving?"W/S acelerar e re  |  A/D virar  |  ESPACO frear  |  F sair\n"+Mathf.Abs(Speed*3.6f).ToString("0")+" km/h":
            NearCargo?"E colocar galinha  |  R retirar"+(AtHome?"  |  G entregar no sitio":"")+"\nGaiolas: "+HouseholdEconomy.Instance.Account.truckCages+"/4  |  Galinhas: "+HouseholdEconomy.Instance.Account.truckChickens+"/"+HouseholdEconomy.Instance.Account.TruckCapacity:
            Vector3.Distance(Player.position,seat.position)<3?"F  |  Dirigir a velha caminhonete":"";
        if(prompt.Length>0)GUI.Box(new Rect(Screen.width*.5f-270,Screen.height*.82f,540,60),prompt,new GUIStyle(GUI.skin.box){fontSize=16,wordWrap=true});
    }
}
