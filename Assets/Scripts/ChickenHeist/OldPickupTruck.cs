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
    public TruckIgnition ignition;
    public TruckCageLids cageLids;
    void Start(){cageLids=GetComponent<TruckCageLids>();if(cageLids==null)cageLids=gameObject.AddComponent<TruckCageLids>();}
    float nextNoise;
    Quaternion wheelBase;
    public Vector3 SteeringGrip(int side)=>steeringWheel.TransformPoint(new Vector3(side<0?-.175f:.175f,0,0));
    bool movementEnabled;
    void Awake(){Instance=this;vehicle=GetComponent<PickupVehiclePhysics>();ignition=GetComponent<TruckIgnition>();if(ignition==null)ignition=gameObject.AddComponent<TruckIgnition>();if(steeringWheel!=null)wheelBase=steeringWheel.localRotation;}
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
            {nextNoise=Time.time+1.2f;NoiseEmitter.EmitGlobal(NoiseSource.VehicleEngine,transform.position,1f);}
        }
        else
        {
            if(Input.GetKeyDown(KeyCode.F) && Vector3.Distance(Player.position,seat.position)<3)EnterDriver();
            if(NearCargo)
            {
                if(WorldInteraction.Pressed(this))LoadOne();
                if(Input.GetKeyDown(KeyCode.R))UnloadOne();
                if(Input.GetKeyDown(KeyCode.G) && AtHome)HeistGameManager.Instance.CompleteMission();
            }
        }
    }
    public void Drive(float throttle,float turn,bool brake,float dt)
    {
        if(!driving)return;
        vehicle.SetInput(ignition.EngineRunning?throttle:0,turn,brake || !ignition.EngineRunning,true);
    }
    void LateUpdate()
    {
        if(driving && Player!=null)
        {
            Player.position=seat.position;Player.rotation=transform.rotation;
            if(steeringWheel!=null)steeringWheel.localRotation=wheelBase*Quaternion.AngleAxis(-vehicle.SteeringAngle*1.5f,Vector3.forward);
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
        driving=false;ignition.StopEngine();vehicle.SetInput(0,0,true,false);Player.position=position;
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
        if(economy.Account.truckChickens%2==0)cageLids.CloseFullCage(economy.Account.truckChickens/2-1);
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
        cageLids?.CancelPlacement();
        if(driving)ForceExit(Player.position);
        vehicle.ResetPose(position,rotation);
    }
    void OnGUI()
    {
        if(GameMenu.IsOpen || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || Player==null)return;
        GUI.skin=UITheme.Skin;float y=Screen.height-UITheme.Size(150);var account=HouseholdEconomy.Instance.Account;
        if(driving)
        {
            UITheme.HintBar(y,null,"W/S","Acelerar e ré","A/D","Virar","Espaço","Frear","F","Sair");
            var speed=new Rect(Screen.width-UITheme.Size(170),Screen.height-UITheme.Size(110),UITheme.Size(150),UITheme.Size(90));UITheme.Panel(speed,UITheme.PanelSoft);
            GUI.Label(new Rect(speed.x,speed.y+UITheme.Size(6),speed.width,UITheme.Size(50)),Mathf.Abs(Speed*3.6f).ToString("0"),new GUIStyle(UITheme.Style("title")){alignment=TextAnchor.MiddleCenter,fontSize=UITheme.Size(42)});
            GUI.Label(new Rect(speed.x,speed.y+UITheme.Size(56),speed.width,UITheme.Size(24)),"km/h",new GUIStyle(UITheme.Style("caption")){alignment=TextAnchor.MiddleCenter});
        }
        else if(NearCargo)
        {
            string info="Gaiolas "+account.truckCages+"/4   ·   Galinhas na carroceria "+account.truckChickens+"/"+account.TruckCapacity;
            if(HeistGameManager.Instance.CanDeliverHere)UITheme.HintBar(y,info,"E","Colocar galinha","R","Retirar","G","Soltar no galinheiro");
            else UITheme.HintBar(y,info,"E","Colocar galinha","R","Retirar");
        }
        else if(Vector3.Distance(Player.position,seat.position)<3)UITheme.KeyPrompt(Screen.width*.5f,y,"F","Dirigir a velha caminhonete");
    }
}
