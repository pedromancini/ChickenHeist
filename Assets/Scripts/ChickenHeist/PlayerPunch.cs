using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerPunch : MonoBehaviour
{
    public static PlayerPunch Instance {get;private set;}
    public const float Reach=2.0f,Cooldown=.7f,StunSeconds=4.5f,Duration=.5f,ImpactAt=.38f;
    public int Hits {get;private set;}
    public int Swings {get;private set;}
    float started=-10,kickUntil=-10;bool landed;
    AudioSource voice;AudioClip swing,thud;
    static readonly Vector3 Rest=new Vector3(.24f,-.34f,.26f),Guard=new Vector3(.15f,-.17f,.22f),Drawn=new Vector3(.17f,-.21f,.19f),
        Jab=new Vector3(.03f,-.06f,.40f),LeftGuard=new Vector3(-.14f,-.12f,.23f),LeftRest=new Vector3(-.22f,-.34f,.24f);
    float T=>(Time.time-started)/Duration;
    public float Progress=>T;
    public bool Punching=>Time.time-started<Duration;
    public float Extension
    {
        get
        {
            float t=T;if(t<0 || t>=1)return 0;
            if(t<.22f)return 0;if(t<.42f)return Ease((t-.22f)/.2f);if(t<.58f)return 1;return 1-Ease((t-.58f)/.42f);
        }
    }
    public float ArmWeight{get{float t=T;if(t<0 || t>=1)return 0;return Mathf.Clamp01(Mathf.Min(t/.12f,(1-t)/.16f));}}
    public float Fist=>Punching?Mathf.Clamp01(Mathf.Min(T/.1f,(1-T)/.12f)):0;
    public Vector3 RightFist
    {
        get
        {
            float t=T;if(t<0 || t>=1)return Rest;
            if(t<.22f)return Vector3.Lerp(Rest,Drawn,Ease(t/.22f));
            if(t<.58f)return Vector3.Lerp(Drawn,Jab,Extension);
            return Vector3.Lerp(Guard,Jab,Extension);
        }
    }
    public Vector3 RightBase{get{float t=T;if(t<0 || t>=1)return Rest;if(t<.22f)return Vector3.Lerp(Rest,Drawn,Ease(t/.22f));return t<.58f?Drawn:Guard;}}
    public Vector3 JabAim=>Jab;
    public Vector3 LeftFist=>Vector3.Lerp(LeftRest,LeftGuard,Mathf.Clamp01(ArmWeight*1.4f));
    public float Twist=>Extension;
    public float Kick=>Mathf.Clamp01((kickUntil-Time.time)/.18f);
    static float Ease(float x){x=Mathf.Clamp01(x);return 1-(1-x)*(1-x);}
    void Awake()
    {
        Instance=this;if(GetComponent<FirstPersonFist>()==null)gameObject.AddComponent<FirstPersonFist>();
        var node=new GameObject("Som do soco");node.transform.SetParent(transform,false);node.AddComponent<AudioBusGain>();
        voice=node.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;voice.volume=.5f;
        swing=SoundLibrary.Or("punch_swing",()=>Synth(true));thud=SoundLibrary.Or("punch_hit",()=>Synth(false));
    }
    void OnDestroy(){if(Instance==this)Instance=null;SoundLibrary.Release(swing);SoundLibrary.Release(thud);}
    bool Blocked
    {
        get
        {
            var game=HeistGameManager.Instance;var bag=GetComponent<BackpackInventory>();
            return GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || OldPickupTruck.IsDriving || DeveloperConsole.IsOpen || BackpackPanel.BlocksInput ||
                ChickenCoopLockpick.Active!=null || TruckCageLids.Active!=null || ChickenScare.Active!=null || game==null || game.missionEnded ||
                (bag!=null && bag.chickensCarried>0) || GetComponent<PlayerHealth>()?.Dead==true;
        }
    }
    void Update()
    {
        if(Input.GetMouseButtonDown(0) && Cursor.lockState==CursorLockMode.Locked)TryPunch();
        if(Punching && !landed && T>=ImpactAt){landed=true;Land();}
    }
    public bool TryPunch()
    {
        if(Blocked || Time.time-started<Cooldown)return false;
        started=Time.time;landed=false;Swings++;
        if(voice!=null && swing!=null){voice.pitch=Random.Range(.92f,1.08f);voice.PlayOneShot(swing,.55f);}
        return true;
    }
    public FarmerStateMachine Target()
    {
        var eye=Camera.main!=null?Camera.main.transform:transform;
        FarmerStateMachine best=null;float bestDistance=Reach;
        foreach(var farmer in FindObjectsByType<FarmerStateMachine>(FindObjectsSortMode.None))
        {
            Vector3 delta=farmer.transform.position-transform.position;delta.y=0;float d=delta.magnitude;
            Vector3 look=eye.forward;look.y=0;
            if(d>bestDistance || d>.05f && Vector3.Angle(look,delta)>35)continue;
            best=farmer;bestDistance=d;
        }
        return best;
    }
    void Land()
    {
        var farmer=Target();
        Vector3 push=farmer!=null?farmer.transform.position-transform.position:Vector3.zero;push.y=0;
        if(farmer==null || !farmer.Stun(StunSeconds,push.normalized*.45f))return;
        Hits++;kickUntil=Time.time+.18f;
        if(voice!=null && thud!=null){voice.pitch=Random.Range(.95f,1.05f);voice.PlayOneShot(thud,.8f);}
        NoiseEmitter.EmitGlobal(NoiseSource.FloorCreak,farmer.transform.position);
        HeistGameManager.Instance?.ShowMessage("Acertou! O fazendeiro ficou atordoado. Saia daí!",2.5f);
    }
    static AudioClip Synth(bool whoosh)
    {
        const int rate=22050;float length=whoosh?.24f:.2f;var data=new float[(int)(rate*length)];var random=new System.Random(whoosh?11:23);float low=0;
        for(int i=0;i<data.Length;i++)
        {
            float t=(float)i/rate,noise=(float)random.NextDouble()*2-1;
            if(whoosh){float env=Mathf.Sin(Mathf.PI*t/length);float k=Mathf.Lerp(.05f,.35f,env);low+=(noise-low)*k;data[i]=low*env*.9f;}
            else{low+=(noise-low)*.18f;data[i]=(Mathf.Sin(2*Mathf.PI*85*t)*.8f+low*.6f)*Mathf.Exp(-t*22);}
        }
        var clip=AudioClip.Create(whoosh?"Punch swing (synth)":"Punch hit (synth)",data.Length,1,rate,false);clip.SetData(data,0);return clip;
    }
}
