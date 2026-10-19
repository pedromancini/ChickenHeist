using UnityEngine;

// Elias punches with the left mouse button: a right jab (ProtagonistArticulation drives the arm, ProtagonistFingers
// closes the fist). A punch that lands on an awake farmer within reach stuns him for a few seconds (no moving, no
// shooting). Getting that close is the risk: the farmer's shots are tighter and hit harder up close (FarmerShotgun).
[DisallowMultipleComponent]
public sealed class PlayerPunch : MonoBehaviour
{
    public static PlayerPunch Instance {get;private set;}
    public const float Reach=1.8f,Cooldown=.75f,StunSeconds=4.5f,Duration=.45f,ImpactAt=.42f;
    public int Hits {get;private set;}
    public int Swings {get;private set;}
    float started=-10;bool landed;
    // 0 → 1 → 0 over a punch: the arm pulls back a little, snaps out, and comes back
    public float Extension
    {
        get
        {
            float t=(Time.time-started)/Duration;if(t<0 || t>=1)return 0;
            if(t<.2f)return -.25f*Mathf.SmoothStep(0,1,t/.2f);
            if(t<.45f)return Mathf.Lerp(-.25f,1,Mathf.SmoothStep(0,1,(t-.2f)/.25f));
            return 1-Mathf.SmoothStep(0,1,(t-.45f)/.55f);
        }
    }
    public bool Punching=>Time.time-started<Duration;
    // how closed the fist is (it closes as the punch starts and opens as the arm comes back)
    public float Fist=>Punching?Mathf.Clamp01(Mathf.Min((Time.time-started)/.08f,(Duration-(Time.time-started))/.1f)):0;
    void Awake(){Instance=this;}
    void OnDestroy(){if(Instance==this)Instance=null;}
    bool Blocked
    {
        get
        {
            var game=HeistGameManager.Instance;var bag=GetComponent<BackpackInventory>();
            return GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || OldPickupTruck.IsDriving || DeveloperConsole.IsOpen ||
                ChickenCoopLockpick.Active!=null || TruckCageLids.Active!=null || ChickenScare.Active!=null || game==null || game.missionEnded ||
                (bag!=null && bag.chickensCarried>0) || GetComponent<PlayerHealth>()?.Dead==true;
        }
    }
    void Update()
    {
        // the click that locks the cursor again (PlayerLook) is not a punch
        if(Input.GetMouseButtonDown(0) && Cursor.lockState==CursorLockMode.Locked)TryPunch();
        if(Punching && !landed && Time.time-started>=Duration*ImpactAt){landed=true;Land();}
    }
    public bool TryPunch()
    {
        if(Blocked || Time.time-started<Cooldown)return false;
        started=Time.time;landed=false;Swings++;return true;
    }
    // The farmer in front within reach: nearest, inside a 60 degree cone of where Elias looks.
    public FarmerStateMachine Target()
    {
        var eye=Camera.main!=null?Camera.main.transform:transform;
        FarmerStateMachine best=null;float bestDistance=Reach;
        foreach(var farmer in FindObjectsByType<FarmerStateMachine>(FindObjectsSortMode.None))
        {
            Vector3 delta=farmer.transform.position-transform.position;delta.y=0;float d=delta.magnitude;
            Vector3 look=eye.forward;look.y=0;
            if(d>bestDistance || d>.05f && Vector3.Angle(look,delta)>30)continue;
            best=farmer;bestDistance=d;
        }
        return best;
    }
    void Land()
    {
        var farmer=Target();
        if(farmer==null || !farmer.Stun(StunSeconds))return;
        Hits++;
        GameAudioMix.Instance?.Play("wood",farmer.transform.position+Vector3.up*1.4f,.55f);
        HeistGameManager.Instance?.ShowMessage("Acertou! O fazendeiro ficou atordoado. Saia daí!",2.5f);
    }
}
