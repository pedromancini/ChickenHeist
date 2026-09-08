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
    Renderer paintRenderer;
    MaterialPropertyBlock originalPaint,coatedPaint;
    bool showingPaint;
    public const float PaintDuration=60f;
    public float PaintSecondsRemaining => Mathf.Clamp(disabledUntil-Time.time,0,PaintDuration);
    public void RestorePaint(float seconds){disabledUntil=Time.time+Mathf.Clamp(seconds,0,PaintDuration);}
    void Awake()
    {
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
        if(!FarmSecurityProgression.Installed || GameMenu.BlocksInput || HeistGameManager.Instance?.IsMissionTarget(this)!=true)return;
        if (Time.time < disabledUntil)
            return;

        float yaw = startYaw + Mathf.Sin(Time.time * rotationSpeed * Mathf.Deg2Rad) * rotationArc;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (player != null && CanSeePlayer() && Time.time >= nextDetectionTime)
        {
            nextDetectionTime = Time.time + detectionCooldown;
            NoiseEmitter.EmitGlobal(NoiseSource.CameraDetected, player.position);
            HeistGameManager.Instance.ShowMessage("Camera te viu. O fazendeiro ouviu o alerta.", 2f);
        }

    }

    private bool CanSeePlayer()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.magnitude > viewDistance)
            return false;

        if (Vector3.Angle(transform.forward, toPlayer.normalized) > viewAngle)
            return false;

        Vector3 eye = transform.position + Vector3.up * 0.6f;
        Vector3 target = player.position + Vector3.up;
        if (Physics.Linecast(eye, target, out RaycastHit hit))
            return hit.transform == player || hit.transform.IsChildOf(player);

        return true;
    }

}
