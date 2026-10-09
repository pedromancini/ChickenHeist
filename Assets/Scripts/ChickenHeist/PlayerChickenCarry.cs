using UnityEngine;

// Uses the protagonist's real skeleton, so the same hands and bird are visible
// in first person, shadows and mirrors. The backpack remains authoritative.
[DefaultExecutionOrder(350)]
public class PlayerChickenCarry : MonoBehaviour
{
    public bool HasVisual => visual != null;
    public bool IsLifting => visual != null && Time.time < liftStarted + liftLength + LiftBlend;
    public Vector3 BirdPosition => visual != null ? visual.transform.position : transform.position;
    public float HandError { get; private set; }
    public float WristBend {get;private set;}
    // Pickup: the bird waits where it was until the hands of the Pickup clip come down to it, rides up between them
    // while the body straightens, then moves to the carry spot as the arm IK blends in over LiftBlend.
    const float LiftDuration = .85f, LiftBlend = .28f;
    float liftLength = LiftDuration; bool grabbed, followHands; Vector3 grabFrom; float grabTime;
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
    readonly HandGripPose[] grips=new HandGripPose[2];
    readonly HandGripPose[] wheelGrips=new HandGripPose[2];
    readonly Quaternion[] wristBind=new Quaternion[2];

    void Awake()
    {
        player=transform;pack=GetComponent<BackpackInventory>();
        eyes=GetComponentInChildren<Camera>()?.transform;
        animator=GetComponentInChildren<RuralCharacterAnimator>();
        phone=GetComponentInChildren<HandheldPhone>();
        var names=new[]{"UpperArmL","ForearmL","HandL","UpperArmR","ForearmR","HandR"};
        if(animator!=null)foreach(var bone in animator.GetComponentsInChildren<Transform>())
            for(int i=0;i<names.Length;i++)if(bone.name==names[i])bones[i]=bone;
        for(int side=0;side<2;side++)
        {
            var hand=bones[side*3+2];if(hand==null)continue;wristBind[side]=hand.localRotation;
            foreach(var skin in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int h=System.Array.IndexOf(skin.bones,hand),f=System.Array.IndexOf(skin.bones,hand.parent);
                if(h>=0 && f>=0){wristBind[side]=(skin.sharedMesh.bindposes[f]*skin.sharedMesh.bindposes[h].inverse).rotation;break;}
            }
        }
    }

    public void Lift(InteractableChicken bird)
    {
        if(bird==null)return;
        LiftFrom(bird.gameObject,bird.transform.position+Vector3.up*.12f);
    }
    // Shared by the farm pickup and unloading a bird from the truck cages (origin: where the bird is taken from).
    // fromGround: bend down with the Pickup clip and take the bird from between the hands; otherwise (a raised cage)
    // reach forward with the Trade gesture and bring the bird straight to the arms.
    public void LiftFrom(GameObject source,Vector3 origin,bool fromGround=true)
    {
        CreateVisual(source);
        liftOrigin=origin;grabbed=false;
        liftStarted=Time.time;
        bool articulated=animator!=null && animator.GetComponent<ProtagonistArticulation>()!=null;
        followHands=fromGround && articulated;
        if(fromGround)animator?.Pickup();else animator?.Gesture();
        float clip=animator!=null?animator.Length("Pickup"):0;
        liftLength=followHands && clip>.3f?clip:LiftDuration;
        if(visual!=null)visual.transform.position=liftOrigin;
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
        visual.AddComponent<FarmAnimalMotion>().carried=true;
    }

    void RestoreArms()
    {
        if(!posed)return;
        for(int i=0;i<bones.Length;i++)if(bones[i]!=null && !(i>=3 && phone!=null && phone.IsBusy))bones[i].localRotation=original[i];
        posed=false;
    }
    void Update(){RestoreArms();}

    void LateUpdate()
    {
        if(pack==null || eyes==null)return;
        if(ChickenCoopLockpick.Active!=null && animator?.GetComponent<ProtagonistArticulation>()!=null)
        {if(visual!=null)visual.SetActive(false);poseWeight=0;return;}
        if(TruckCageLids.Active?.HandsOnLid==true)
        {
            for(int i=0;i<bones.Length;i++){if(bones[i]==null)return;original[i]=bones[i].localRotation;}
            posed=true;poseWeight=1;
            HandError=0;
            for(int side=0;side<2;side++)
            {
                if(grips[side]==null)grips[side]=new HandGripPose(bones[side*3+2],animator.transform);
                var surface=(side==0?TruckCageLids.Active.LeftHand:TruckCageLids.Active.RightHand)-Vector3.up*.015f;
                var rotation=grips[side].Rotation(Vector3.up,player.forward);
                Reach(side*3,grips[side].Wrist(surface,rotation),side==0?-1:1);
                bones[side*3+2].rotation=rotation;
                HandError=Mathf.Max(HandError,Vector3.Distance(grips[side].Contact,surface));
            }
            return;
        }
        if(OldPickupTruck.IsDriving)
        {
            ClearVisual();
            for(int i=0;i<bones.Length;i++)if(bones[i]==null)return;
            for(int i=0;i<bones.Length;i++)original[i]=bones[i].localRotation;
            posed=true;poseWeight=Mathf.MoveTowards(poseWeight,1,Time.deltaTime*5);
            var truck=OldPickupTruck.Instance;
            HandError=0;
            for(int side=0;side<2;side++)
            {
                if(side==1 && phone!=null && phone.IsBusy)continue;
                if(side==1 && truck.ignition.Active && animator.GetComponent<ProtagonistArticulation>()!=null)continue;
                if(wheelGrips[side]==null)wheelGrips[side]=new HandGripPose(bones[side*3+2],animator.transform,true,true);
                CharacterGripHands.Attach(animator.transform).SetPose(bones[side*3+2],CharacterGripHands.Pose.Wheel);
                // The rim runs through the closed hand (ProtagonistFingers closes it): palm towards the wheel's
                // centre, knuckles towards the dashboard (wheel forward points away from the driver), thumb up.
                var wheel=truck.steeringWheel;var inward=wheel.right*(side==0?1:-1);
                var centre=truck.SteeringGrip(side==0?-1:1);
                var rotation=wheelGrips[side].Rotation(inward,(wheel.forward*.92f+inward*.38f).normalized);
                Reach(side*3,PowerGrip.Wrist(wheelGrips[side],rotation,centre,OldPickupTruck.SteeringRimRadius),side==0?-1:1);
                bones[side*3+2].rotation=Quaternion.Slerp(bones[side*3+2].rotation,rotation,poseWeight);
                HandError=Mathf.Max(HandError,Vector3.Distance(PowerGrip.AnchorWorld(wheelGrips[side],OldPickupTruck.SteeringRimRadius),centre));
            }
            return;
        }
        if(pack.chickensCarried==0){ClearVisual();poseWeight=0;return;}
        if(visual==null)
        {
            var flock=FindFirstObjectByType<HomeFlockView>();
            if(flock?.chickenPrefab!=null){CreateVisual(flock.chickenPrefab);liftStarted=Time.time-liftLength-LiftBlend;}
        }
        if(visual==null)return;
        bool stowed=ProtagonistPhone.IsOpen || phone!=null && phone.IsBusy || ChickenCoopLockpick.Active!=null;
        visual.SetActive(!stowed);
        if(stowed){poseWeight=0;return;}
        var movement=GetComponent<PlayerMovement>();
        float bob=movement!=null && movement.estaMovendo?Mathf.Sin(Time.time*(movement.estaSprinting?12:8))*.014f:Mathf.Sin(Time.time*2)*.003f;
        bool articulated=animator!=null && animator.GetComponent<ProtagonistArticulation>()!=null;
        var target=player.position+Vector3.up*(eyes.localPosition.y-(articulated?.32f:.34f)+bob)+player.forward*(articulated?.47f:.43f);
        visual.transform.rotation=player.rotation*Quaternion.Euler(0,78,Mathf.Sin(Time.time*2)*2);
        float elapsed=Time.time-liftStarted;
        if(followHands && elapsed<liftLength+LiftBlend && bones[2]!=null && bones[5]!=null)
        {
            // Hands of the Pickup clip: the bird is taken when they come down to it (or half way through the clip)
            // and stays between the palms while the body straightens; no arm IK until the clip ends.
            for(int side=0;side<2;side++)if(grips[side]==null)grips[side]=new HandGripPose(bones[side*3+2],animator.transform);
            var hands=(grips[0].Contact+grips[1].Contact)*.5f-Vector3.up*.03f;
            if(elapsed<liftLength)
            {
                // the bird is drawn into the hands while they come down (from a fifth of the clip to just before
                // the lowest point), so it never jumps; then it rides between the palms
                if(!grabbed && elapsed>liftLength*.2f){grabbed=true;grabFrom=visual.transform.position;grabTime=Time.time;}
                visual.transform.position=grabbed?Vector3.Lerp(grabFrom,hands,Mathf.SmoothStep(0,1,(Time.time-grabTime)/(liftLength*.28f))):liftOrigin;
                poseWeight=0;HandError=0;return;
            }
            float b=Mathf.SmoothStep(0,1,(elapsed-liftLength)/LiftBlend);
            visual.transform.position=Vector3.Lerp(hands,target,b);
            poseWeight=b;
        }
        else
        {
            float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/liftLength));
            visual.transform.position=Vector3.Lerp(liftOrigin,target,t)+Vector3.up*(articulated?0:Mathf.Sin(t*Mathf.PI)*.10f);
            poseWeight=Mathf.MoveTowards(poseWeight,1,Time.deltaTime*5);
        }
        for(int i=0;i<bones.Length;i++)if(bones[i]==null)return;
        for(int i=0;i<bones.Length;i++)original[i]=bones[i].localRotation;
        posed=true;
        HandError=0;
        for(int side=0;side<2;side++)
        {
            var wrist=visual.transform.position+player.right*(side==0?-.14f:.14f)-Vector3.up*.035f;
            if(animator.GetComponent<ProtagonistArticulation>()!=null)
            {
                if(grips[side]==null)grips[side]=new HandGripPose(bones[side*3+2],animator.transform);
                // Keep the wrist neutral and put the grip roll into the forearm.
                var hand=bones[side*3+2];var forearm=bones[side*3+1];
                for(int iteration=0;iteration<2;iteration++)
                {
                    hand.localRotation=wristBind[side];
                    var direction=(hand.position-forearm.position).normalized;
                    var normal=(player.right*(side==0?1:-1)+player.up*.25f).normalized;
                    var desired=grips[side].Rotation(normal,direction);
                    Reach(side*3,grips[side].Wrist(wrist,desired),side==0?-1:1);
                    hand.localRotation=wristBind[side];
                    direction=(hand.position-forearm.position).normalized;
                    desired=grips[side].Rotation(normal,direction);
                    var delta=desired*Quaternion.Inverse(hand.rotation);
                    var vector=Vector3.Project(new Vector3(delta.x,delta.y,delta.z),direction);
                    var twist=new Quaternion(vector.x,vector.y,vector.z,delta.w);
                    if(Quaternion.Dot(twist,twist)>.00001f)forearm.rotation=Quaternion.Slerp(Quaternion.identity,twist.normalized,poseWeight)*forearm.rotation;
                    hand.localRotation=wristBind[side];
                }
                WristBend=Quaternion.Angle(hand.localRotation,wristBind[side]);
                HandError=Mathf.Max(HandError,Vector3.Distance(grips[side].Contact,wrist));
            }
            else
            {
                Reach(side*3,wrist,side==0?-1:1);
                HandError=Mathf.Max(HandError,Vector3.Distance(bones[side*3+2].position,wrist));
            }
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
