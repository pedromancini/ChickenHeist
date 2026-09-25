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
    public bool Detecting { get; private set; }
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
    public bool CanPaintNow()
    {
        var game=HeistGameManager.Instance;var eye=Camera.main;
        if(!FarmSecurityProgression.Installed || game==null || game.missionEnded || !game.IsMissionTarget(this) || eye==null || PaintSecondsRemaining>0)return false;
        Vector3 delta=transform.position-eye.transform.position;
        if(delta.sqrMagnitude>3f*3f || Vector3.Dot(eye.transform.forward,delta.normalized)<.86f)return false;
        var hits=Physics.RaycastAll(eye.transform.position,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        // Geometry behind the camera's visible surface must not occlude its lens.
        foreach(var hit in hits)
            if(!hit.transform.IsChildOf(game.player))return hit.transform.IsChildOf(transform);
        return true;
    }
    public bool TryPaint()
    {
        if(GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || !CanPaintNow())return false;
        var economy=HouseholdEconomy.Instance;
        if(economy==null || economy.Account.paintUses<1)
        {HeistGameManager.Instance.ShowMessage("Sem tinta. Compre uma lata na loja.",3);return false;}
        if(!economy.UsePaint()){HeistGameManager.Instance.ShowMessage(economy.Message,3);return false;}
        RestorePaint(PaintDuration);
        HeistGameManager.Instance.ShowMessage("Lente coberta por 60 s. Tinta: "+economy.Account.paintUses+" usos restantes.",3);return true;
    }

    private void Start()
    {
        startYaw = transform.eulerAngles.y;
    }

    private void Update()
    {
        Detecting=false;
        if(!Operational || GameMenu.BlocksInput)return;

        float yaw = startYaw + Mathf.Sin(Time.time * rotationSpeed * Mathf.Deg2Rad) * rotationArc;
        (scanHead!=null?scanHead:transform).rotation = Quaternion.Euler(0f, yaw, 0f);

        Detecting=player!=null && CanSeePlayer();
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

    // Footprint of the same line-of-sight test at standing torso height.
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
