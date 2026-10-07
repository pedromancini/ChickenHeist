using UnityEngine;

[DefaultExecutionOrder(-90)]
public class ChickenCoopLockpick : MonoBehaviour
{
    public static ChickenCoopLockpick Active {get;private set;}
    public static int ClosedFrame {get;private set;}=-1;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Active=null;ClosedFrame=-1;}
    public Transform door,lockAnchor,scareChicken;
    public float interactionDistance=3.8f;
    public float pickSpeed=.72f,sweetSpotWidth=.12f;
    public float EffectiveSweetSpotWidth=>Latch.Tolerance(Professional);
    public float EffectivePickSpeed=>pickSpeed*(Professional?.75f:1);
    bool Professional=>HouseholdEconomy.Instance?.Account.professionalEquipped==true;
    public bool IsOpen {get;private set;}
    public bool ChallengeActive {get;private set;}
    public int PinsSet=>IsOpen?3:Latch.Completed;
    public const int RequiredPins=3;
    public float PickPosition=>Latch.Pressure;
    public CoopPressureLatch Latch {get;private set;}=new CoopPressureLatch(0);
    public ChickenScare Scare {get;private set;}
    public Vector3 InteractionPoint=>lockAnchor!=null?lockAnchor.position:door!=null?door.position:transform.position;
    Transform player;PlayerMovement movement;bool movementWasEnabled;
    int mistakes,selected;float jamUntil,nextScare;
    bool jamPending;
    CoopLatchPresentation presentation;
    void Awake()
    {
        if(lockAnchor==null)lockAnchor=transform.Find("Cadeado do Galinheiro");
        Scare=gameObject.AddComponent<ChickenScare>();
        presentation=gameObject.AddComponent<CoopLatchPresentation>();presentation.owner=this;
    }
    void Start(){player=HeistGameManager.Instance?.player;movement=player!=null?player.GetComponent<PlayerMovement>():null;}
    int Seed=>GetComponentInParent<FarmLayoutInfo>()?.layoutIndex??0;
    public bool CanReachLock()
    {
        var game=HeistGameManager.Instance;var camera=Camera.main;
        if(game==null || game.player==null || camera==null)return false;
        Vector3 delta=InteractionPoint-camera.transform.position;
        if(delta.magnitude>interactionDistance || Vector3.Dot(camera.transform.forward,delta.normalized)<.5f)return false;
        var hits=Physics.RaycastAll(camera.transform.position,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var hit in hits)if(!hit.transform.IsChildOf(game.player))return hit.transform.IsChildOf(transform);
        return true;
    }
    public bool TryBeginChallenge()
    {
        var game=HeistGameManager.Instance;
        if(IsOpen || GameMenu.BlocksInput || OldPickupTruck.IsDriving || Time.time<jamUntil || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || game==null || game.missionEnded || Active!=null || !CanReachLock())return false;
        if(!game.IsMissionTarget(this)){game.ShowMessage("Escolha esta fazenda nas fotos do tablet.",3);return false;}
        player=game.player;movement=player.GetComponent<PlayerMovement>();BeginChallenge();return true;
    }
    void BeginChallenge()
    {
        if(ChallengeActive || Active!=null)return;
        Active=this;ChallengeActive=true;mistakes=0;selected=0;jamPending=false;Latch=new CoopPressureLatch(Seed);
        presentation?.BeginView();
        if(movement!=null){movementWasEnabled=movement.enabled;movement.enabled=false;movement.estaMovendo=false;movement.estaSprinting=false;movement.nivelRuido=0;}
        HeistGameManager.Instance?.ShowMessage("",0);
    }
    public void CancelInteraction()
    {
        Scare?.Cancel();
        presentation?.EndView();
        if(ChallengeActive && movement!=null)movement.enabled=movementWasEnabled;
        if(Active==this){Active=null;ClosedFrame=Time.frameCount;}
        ChallengeActive=false;
        jamPending=false;
    }
    void EndChallenge(){CancelInteraction();HeistGameManager.Instance?.ShowMessage("Trava interrompida.",1.5f);}
    void OnDisable(){CancelInteraction();}
    public void RestoreOpen(bool open){SetDoorOpen(open,true);}
    public void SetDoorOpen(bool open,bool immediate=false)
    {
        CancelInteraction();IsOpen=open;Latch=new CoopPressureLatch(Seed);
        if(door!=null){var hinge=door.GetComponent<HingedBarrier>();if(hinge==null)hinge=door.gameObject.AddComponent<HingedBarrier>();hinge.SetOpen(open,immediate);foreach(var r in door.GetComponentsInChildren<Renderer>())r.enabled=true;foreach(var c in door.GetComponentsInChildren<Collider>())c.enabled=true;}
    }
    void Update()
    {
        var game=HeistGameManager.Instance;
        if(ChallengeActive && (game==null || game.missionEnded || !game.IsMissionTarget(this))){CancelInteraction();return;}
        if(GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || game?.missionEnded==true)return;
        if(player==null)return;
        if(jamPending){if(Scare?.IsActive!=true)Jam();return;}
        if(IsOpen){if(WorldInteraction.Pressed(this) && CanReachLock())SetDoorOpen(false);return;}
        if(!ChallengeActive){if(WorldInteraction.Pressed(this))TryBeginChallenge();return;}
        if(Input.GetKeyDown(KeyCode.Escape)){EndChallenge();return;}
        if(Input.GetKeyDown(KeyCode.Alpha1))selected=0;
        if(Input.GetKeyDown(KeyCode.Alpha2))selected=1;
        if(Input.GetKeyDown(KeyCode.Alpha3))selected=2;
        float axis=(Input.GetKey(KeyCode.W)?1:0)-(Input.GetKey(KeyCode.S)?1:0);
        Manipulate(Time.deltaTime,selected,axis,Input.GetKey(KeyCode.Space));
    }
    public void Manipulate(float dt,int piece,float pressureAxis,bool pull)
    {
        if(!ChallengeActive || jamPending || GameMenu.BlocksInput || Scare?.IsActive==true)return;
        int result=Latch.Step(dt,piece,pressureAxis,pull,Professional);
        presentation?.SetFriction(pull && !Latch.CanSlide(Professional)?Latch.Stress:0);
        if(result>0)
        {
            presentation?.Click(false);
            if(PinsSet==3){SetDoorOpen(true);HeistGameManager.Instance?.ShowMessage("Trinco liberado. Entre devagar.",3);}
        }
        if(result<0)
        {
            mistakes++;presentation?.Click(true);NoiseEmitter.EmitGlobal(NoiseSource.CoopLockpickFail,InteractionPoint);
            HeistGameManager.Instance?.ShowMessage("O trinco bateu! Alivie a pressao antes de forcar.",3);
            if(Time.time>=nextScare && TryScare())nextScare=Time.time+18;
            if(mistakes>=3){if(Scare?.IsActive==true)jamPending=true;else Jam();}
        }
    }
    bool TryScare()
    {
        if(scareChicken==null || !scareChicken.gameObject.activeInHierarchy)
        {
            scareChicken=null;
            var farm=GetComponentInParent<FarmLayoutInfo>();
            if(farm!=null)foreach(var candidate in farm.GetComponentsInChildren<InteractableChicken>())
                if(candidate.coop==this && candidate.gameObject.activeInHierarchy){scareChicken=candidate.transform;break;}
        }
        return Scare!=null && scareChicken!=null && Scare.Begin(scareChicken,Camera.main);
    }
    void Jam(){jamUntil=Time.time+4;CancelInteraction();HeistGameManager.Instance?.ShowMessage("Trinco emperrado. Espere 4 segundos.",4);}
    void OnGUI()
    {
        if(GameMenu.IsOpen || DeveloperConsole.IsOpen || BackpackPanel.IsOpen || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || Scare?.IsActive==true)return;
        if(!ChallengeActive)
        {
            if(Active==null && CanReachLock()){GUI.skin=UITheme.Skin;UITheme.KeyPrompt(Screen.width*.5f,Screen.height*.78f,"E","Inspecionar trinco");}
            return;
        }
        int depth=GUI.depth;GUI.depth=-100;Color oldColor=GUI.color;GUI.skin=UITheme.Skin;
        float width=Mathf.Min(UITheme.Size(700),Screen.width-32);float x=(Screen.width-width)*.5f,y=Screen.height-UITheme.Size(170);
        UITheme.Panel(new Rect(x,y-UITheme.Size(10),width,UITheme.Size(160)));
        var label=new GUIStyle(UITheme.Style("hud")){alignment=TextAnchor.MiddleCenter,wordWrap=true};
        string state=Latch.Stress>.65f?"RANGENDO — solte ESPAÇO":Latch.CanSlide(Professional)?"CEDENDO — mantenha a pressão":Latch.HasSlack?"COM FOLGA — ajuste a pressão":"PRESA — experimente outra peça";
        float s=UITheme.Scale;
        GUI.Label(new Rect(x+10,y,width-20,28*s),"PEÇA "+(Latch.Selected+1)+" / 3    •    "+state,new GUIStyle(label){normal={textColor=Latch.Stress>.65f?UITheme.Danger:UITheme.Ink}});
        UITheme.Meter(new Rect(x+24*s,y+34*s,width-48*s,10*s),Latch.Pressure,Color.Lerp(UITheme.Accent,UITheme.Danger,Latch.Stress));
        for(int pin=0;pin<3;pin++)UITheme.Panel(new Rect(Screen.width*.5f-40*s+pin*28*s,y+52*s,20*s,20*s),pin<PinsSet?UITheme.Good:new Color(.12f,.10f,.08f,.95f),pin==Latch.Selected?UITheme.Accent:UITheme.Edge,5);
        GUI.Label(new Rect(x+10,y+74*s,width-20,22*s),"Pressão "+Mathf.RoundToInt(Latch.Pressure*100)+"%   ·   "+PinsSet+" de 3 peças liberadas",new GUIStyle(UITheme.Style("hudSmall")){alignment=TextAnchor.MiddleCenter});
        var keys=new[]{"1 2 3","Peça","W/S","Pressão","Espaço","Deslizar","Esc","Sair"};
        var keyStyle=UITheme.Style("key");var text=new GUIStyle(UITheme.Style("hudSmall")){alignment=TextAnchor.MiddleLeft,normal={textColor=UITheme.Ink}};
        float total=0;for(int i=0;i<keys.Length;i+=2)total+=keyStyle.CalcSize(new GUIContent(keys[i])).x+12*s+8*s+text.CalcSize(new GUIContent(keys[i+1])).x+18*s;
        float cx=Screen.width*.5f-total*.5f,by=y+100*s;
        for(int i=0;i<keys.Length;i+=2){float kw=keyStyle.CalcSize(new GUIContent(keys[i])).x+12*s;GUI.Label(new Rect(cx,by+6*s,kw,26*s),keys[i],keyStyle);cx+=kw+8*s;float tw=text.CalcSize(new GUIContent(keys[i+1])).x;GUI.Label(new Rect(cx,by,tw+4,40*s),keys[i+1],text);cx+=tw+18*s;}
        GUI.color=oldColor;GUI.depth=depth;
    }
}
