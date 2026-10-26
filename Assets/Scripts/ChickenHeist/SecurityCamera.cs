using UnityEngine;

public class SecurityCamera : MonoBehaviour
{
    public Transform player;
    public float viewDistance = 14f;
    public float viewAngle = 42f;
    public float rotationArc = 80f;
    public float rotationSpeed = 32f;
    public float detectionCooldown = 1.5f;

    private float startYaw;
    private float nextDetectionTime;
    private float disabledUntil;
    readonly RaycastHit[] visibilityHits=new RaycastHit[64];
    public Transform scanHead;
    public const float DetectSeconds=.7f,ForgetSeconds=1.6f,Dwell=1.4f;
    public bool Detecting { get; private set; }
    public bool Sees { get; private set; }
    public float Suspicion { get; private set; }
    float phase;
    public bool Operational => enabled && FarmSecurityProgression.Installed && PaintSecondsRemaining<=0 &&
        HeistGameManager.Instance?.missionEnded==false && HeistGameManager.Instance.IsMissionTarget(this);
    public Vector3 Eye => (scanHead!=null?scanHead:transform).TransformPoint(new Vector3(0,0,.31f));
    Renderer paintRenderer;
    MaterialPropertyBlock originalPaint,coatedPaint;
    bool showingPaint;
    public const float PaintDuration=60f;
    public float PaintSecondsRemaining => Mathf.Clamp(disabledUntil-Time.time,0,PaintDuration);
    public void RestorePaint(float seconds){disabledUntil=Time.time+Mathf.Clamp(seconds,0,PaintDuration);}
    void Awake()
    {
        if(GetComponent<SecurityEquipmentPresentation>()==null)gameObject.AddComponent<SecurityEquipmentPresentation>();
        if(GetComponent<SecurityEquipmentAudio>()==null)gameObject.AddComponent<SecurityEquipmentAudio>();
        paintRenderer=GetComponent<Renderer>();
        if(paintRenderer==null)return;
        originalPaint=new MaterialPropertyBlock();coatedPaint=new MaterialPropertyBlock();
        paintRenderer.GetPropertyBlock(originalPaint);paintRenderer.GetPropertyBlock(coatedPaint);
        coatedPaint.SetColor("_BaseColor",new Color(.38f,.19f,.14f));coatedPaint.SetColor("_Color",new Color(.38f,.19f,.14f));
    }
    void LateUpdate()
    {
        bool coated=PaintSecondsRemaining>0;
        if(paintRenderer!=null && showingPaint!=coated){paintRenderer.SetPropertyBlock(coated?coatedPaint:originalPaint);showingPaint=coated;}
    }
    public Vector3 PaintTarget=>scanHead!=null?scanHead.position:transform.position;
    public const float PaintReach=3.3f,PaintAim=28;
    public enum PaintBlock{None,Unavailable,TooFar,NotAimed,Obstructed}
    public PaintBlock PaintCheck()
    {
        var game=HeistGameManager.Instance;var eye=Camera.main;
        if(!FarmSecurityProgression.Installed || game==null || game.missionEnded || !game.IsMissionTarget(this) || eye==null || PaintSecondsRemaining>0)return PaintBlock.Unavailable;
        Vector3 delta=PaintTarget-eye.transform.position;
        if(delta.magnitude>PaintReach)return PaintBlock.TooFar;
        if(Vector3.Angle(eye.transform.forward,delta)>PaintAim)return PaintBlock.NotAimed;
        var hits=Physics.RaycastAll(eye.transform.position,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var hit in hits)
            if(!hit.transform.IsChildOf(game.player))return hit.transform.IsChildOf(transform)?PaintBlock.None:PaintBlock.Obstructed;
        return PaintBlock.None;
    }
    public bool CanPaintNow()=>PaintCheck()==PaintBlock.None;
    public bool TryPaint()
    {
        if(GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || !CanPaintNow())return false;
        var economy=HouseholdEconomy.Instance;
        if(economy==null || economy.Account.paintUses<1)
        {HeistGameManager.Instance.ShowMessage("Sem tinta. Compre uma lata na loja.",3);return false;}
        if(!economy.UsePaint()){HeistGameManager.Instance.ShowMessage(economy.Message,3);return false;}
        RestorePaint(PaintDuration);
        var view=Camera.main.transform;SprayEffect.Play(view.position+view.right*.16f-view.up*.17f+view.forward*.32f,Eye);
        HeistGameManager.Instance.ShowMessage("Lente coberta por 60 s. Tinta: "+economy.Account.paintUses+" usos restantes.",3);return true;
    }

    private void Start()
    {
        startYaw = transform.eulerAngles.y;
        phase=Mathf.Abs(transform.position.x*.37f+transform.position.z*.73f)%10;
    }
    public float ScanYaw(float time)
    {
        float sweep=2*rotationArc/Mathf.Max(1,rotationSpeed),cycle=2*(sweep+Dwell),t=Mathf.Repeat(time+phase,cycle),s;
        if(t<Dwell)s=0;else if(t<Dwell+sweep)s=Mathf.SmoothStep(0,1,(t-Dwell)/sweep);else if(t<2*Dwell+sweep)s=1;else s=1-Mathf.SmoothStep(0,1,(t-2*Dwell-sweep)/sweep);
        return startYaw+Mathf.Lerp(-rotationArc,rotationArc,s);
    }

    private void Update()
    {
        if(!Operational){Detecting=false;Sees=false;Suspicion=0;return;}
        if(GameMenu.BlocksInput)return;

        (scanHead!=null?scanHead:transform).rotation = Quaternion.Euler(0f, ScanYaw(Time.time), 0f);

        Sees=player!=null && CanSeePlayer();
        Suspicion=Mathf.MoveTowards(Suspicion,Sees?1:0,Time.deltaTime/(Sees?DetectSeconds:ForgetSeconds));
        Detecting=Sees && Suspicion>=1;
        if (Detecting && Time.time >= nextDetectionTime)
        {
            nextDetectionTime = Time.time + detectionCooldown;
            GetComponent<SecurityEquipmentAudio>()?.Alert();
            NoiseEmitter.EmitGlobal(NoiseSource.CameraDetected, player.position);
            HeistGameManager.Instance.ShowMessage("Camera te viu. O fazendeiro ouviu o alerta.", 2f);
        }

    }

    private bool CanSeePlayer()
    {
        return CanSeePoint(player.position+Vector3.up*(player.GetComponent<PlayerMovement>()?.estaAgachado==true?.5f:1f));
    }

    public bool CanSeePoint(Vector3 target)
    {
        Vector3 toPlayer = target - Eye;
        toPlayer.y = 0f;

        if (toPlayer.magnitude > viewDistance)
            return false;

        if (Vector3.Angle((scanHead!=null?scanHead:transform).forward, toPlayer.normalized) > viewAngle)
            return false;

        return Unoccluded(target) && toPlayer.magnitude<=ClearRange(toPlayer.normalized,target.y)+.01f;
    }

    public bool Unoccluded(Vector3 target)
    {
        var delta=target-Eye;
        int count=Physics.RaycastNonAlloc(Eye,delta.normalized,visibilityHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
        if(count==visibilityHits.Length)return false;
        for(int i=0;i<count;i++)
            if(!visibilityHits[i].transform.IsChildOf(transform) && (player==null || !visibilityHits[i].transform.IsChildOf(player)))return false;
        return true;
    }

    public float ClearRange(Vector3 direction,float targetHeight)
    {
        Vector3 origin=Eye;origin.y=targetHeight;
        float previous=0;
        for(float d=.3f;d<viewDistance+.3f;d+=.3f)
        {
            float end=Mathf.Min(d,viewDistance);
            if(!Unoccluded(origin+direction*end))
            {
                for(int j=0;j<5;j++){float mid=(previous+end)*.5f;if(Unoccluded(origin+direction*mid))previous=mid;else end=mid;}
                return previous;
            }
            previous=end;
        }
        return viewDistance;
    }

}
