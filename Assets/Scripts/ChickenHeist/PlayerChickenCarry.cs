using UnityEngine;

// Uses the protagonist's real skeleton, so the same hands and bird are visible
// in first person, shadows and mirrors. The backpack remains authoritative.
[DefaultExecutionOrder(350)]
public class PlayerChickenCarry : MonoBehaviour
{
    public bool HasVisual => visual != null;
    public bool IsLifting => visual != null && Time.time < liftStarted + LiftDuration;
    public Vector3 BirdPosition => visual != null ? visual.transform.position : transform.position;
    public float HandError { get; private set; }
    const float LiftDuration = .85f;
    Transform player, eyes;
    BackpackInventory pack;
    RuralCharacterAnimator animator;
    HandheldPhone phone;
    GameObject visual;
    Vector3 liftOrigin;
    float liftStarted, poseWeight;
    readonly Transform[] bones = new Transform[6];
    readonly Quaternion[] original = new Quaternion[6];
    bool posed;

    void Awake()
    {
        player=transform;pack=GetComponent<BackpackInventory>();
        eyes=GetComponentInChildren<Camera>()?.transform;
        animator=GetComponentInChildren<RuralCharacterAnimator>();
        phone=GetComponentInChildren<HandheldPhone>();
        var names=new[]{"UpperArmL","ForearmL","HandL","UpperArmR","ForearmR","HandR"};
        if(animator!=null)foreach(var bone in animator.GetComponentsInChildren<Transform>())
            for(int i=0;i<names.Length;i++)if(bone.name==names[i])bones[i]=bone;
    }

    public void Lift(InteractableChicken bird)
    {
        if(bird==null)return;
        CreateVisual(bird.gameObject);
        liftOrigin=bird.transform.position+Vector3.up*.2f;
        liftStarted=Time.time;
        animator?.Pickup();
    }

    void CreateVisual(GameObject source)
    {
        ClearVisual();
        visual=new GameObject("Galinha no colo - visual");
        visual.transform.SetParent(player,false);
        visual.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        // Copy geometry only: no AI, pickup scripts, triggers or checkpoint IDs.
        foreach(var filter in source.GetComponentsInChildren<MeshFilter>(true))
        {
            var renderer=filter.GetComponent<MeshRenderer>();
            if(renderer==null || filter.sharedMesh==null)continue;
            var part=new GameObject(filter.name);
            part.transform.SetParent(visual.transform,false);
            var relative=source.transform.worldToLocalMatrix*filter.transform.localToWorldMatrix;
            part.transform.localPosition=relative.GetColumn(3);
            part.transform.localRotation=relative.rotation;
            part.transform.localScale=relative.lossyScale;
            part.AddComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
            part.AddComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
        }
        var renderers=visual.GetComponentsInChildren<Renderer>();
        if(renderers.Length==0){ClearVisual();return;}
        var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
        foreach(Transform part in visual.transform)part.position-=bounds.center;
        visual.transform.localScale=Vector3.one*(.38f/Mathf.Max(.01f,bounds.size.y));
    }

    void RestoreArms()
    {
        if(!posed)return;
        for(int i=0;i<bones.Length;i++)if(bones[i]!=null)bones[i].localRotation=original[i];
        posed=false;
    }
    void Update(){RestoreArms();}

    void LateUpdate()
    {
        if(pack==null || eyes==null)return;
        if(OldPickupTruck.IsDriving)
        {
            ClearVisual();
            for(int i=0;i<bones.Length;i++)if(bones[i]==null)return;
            for(int i=0;i<bones.Length;i++)original[i]=bones[i].localRotation;
            posed=true;poseWeight=Mathf.MoveTowards(poseWeight,1,Time.deltaTime*5);
            var truck=OldPickupTruck.Instance;
            for(int side=0;side<2;side++)Reach(side*3,truck.steeringWheel.position+truck.transform.right*(side==0?-.13f:.13f),side==0?-1:1);
            return;
        }
        if(pack.chickensCarried==0){ClearVisual();poseWeight=0;return;}
        if(visual==null)
        {
            var flock=FindFirstObjectByType<HomeFlockView>();
            if(flock?.chickenPrefab!=null){CreateVisual(flock.chickenPrefab);liftStarted=Time.time-LiftDuration;}
        }
        if(visual==null)return;
        bool stowed=ProtagonistPhone.IsOpen || phone!=null && phone.IsBusy || ChickenCoopLockpick.Active!=null;
        visual.SetActive(!stowed);
        if(stowed){poseWeight=0;return;}
        float t=Mathf.SmoothStep(0,1,Mathf.Clamp01((Time.time-liftStarted)/LiftDuration));
        var movement=GetComponent<PlayerMovement>();
        float bob=movement!=null && movement.estaMovendo?Mathf.Sin(Time.time*(movement.estaSprinting?12:8))*.014f:Mathf.Sin(Time.time*2)*.003f;
        var target=player.position+Vector3.up*(eyes.localPosition.y-.34f+bob)+player.forward*.43f;
        visual.transform.position=Vector3.Lerp(liftOrigin,target,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.10f);
        visual.transform.rotation=player.rotation*Quaternion.Euler(0,78,Mathf.Sin(Time.time*2)*2);
        poseWeight=Mathf.MoveTowards(poseWeight,1,Time.deltaTime*5);
        for(int i=0;i<bones.Length;i++)if(bones[i]==null)return;
        for(int i=0;i<bones.Length;i++)original[i]=bones[i].localRotation;
        posed=true;
        HandError=0;
        for(int side=0;side<2;side++)
        {
            var wrist=visual.transform.position+player.right*(side==0?-.13f:.13f)-Vector3.up*.075f;
            Reach(side*3,wrist,side==0?-1:1);
            HandError=Mathf.Max(HandError,Vector3.Distance(bones[side*3+2].position,wrist));
        }
    }

    void Reach(int index,Vector3 target,float side)
    {
        var upper=bones[index];var lower=bones[index+1];var hand=bones[index+2];
        float a=Vector3.Distance(upper.position,lower.position),b=Vector3.Distance(lower.position,hand.position);
        Vector3 axis=target-upper.position;
        float distance=Mathf.Clamp(axis.magnitude,.02f,a+b-.005f);axis.Normalize();
        Vector3 bend=Vector3.ProjectOnPlane(player.right*side-Vector3.up*.6f,axis).normalized;
        float along=(a*a-b*b+distance*distance)/(2*distance);
        Vector3 elbow=upper.position+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(lower.position-upper.position,elbow-upper.position)*upper.rotation,poseWeight);
        lower.rotation=Quaternion.Slerp(lower.rotation,Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation,poseWeight);
    }
    public void ClearVisual(){if(visual!=null){visual.SetActive(false);Destroy(visual);visual=null;}}
    void OnDisable(){RestoreArms();ClearVisual();}
}
