using System.Linq;
using UnityEngine;

// Cinematic actor (Elias and the hooded visitor, both on ProtagonistRig bone names with fingers): samples the
// captured clips deterministically, then layers gaze and hand poses on top. Every evaluation begins from the clip,
// so replay, skip and shot changes cannot accumulate rotations. Arms move like real ones (CinematicArm: the elbow is
// a hinge, the forearm takes the hand's roll, the wrist bends within its range) and the hands take the shape of what
// they hold (CinematicHand): flat on a desk, a fist knocking, the index on a key, folded over the tablet's edge.
public sealed class VisitorCinematicActor
{
    public readonly Transform root,head,spine,leftHand,rightHand;
    readonly Animation animation;readonly AnimationClip idle,walk,walkBack,talk;
    readonly Transform[] bones;readonly Quaternion[] rest,walkPose,talkPose;
    float talkWeight;
    readonly CinematicArm[] arms=new CinematicArm[2];readonly CinematicHand[] hands=new CinematicHand[2];
    readonly HandGripPose[] grips=new HandGripPose[2];readonly Vector3[] knuckles=new Vector3[2];
    readonly Vector3[][] knuckleEach=new Vector3[2][];
    public float ContactError {get;private set;}
    // Largest wrist bend this frame against its limit (1: at the limit of the joint), for the review.
    public float WristLoad {get;private set;}
    public CinematicArm Arm(bool right)=>arms[right?1:0];
    public CinematicHand Hand(bool right)=>hands[right?1:0];
    // World directions out of the palm and from the wrist to the knuckles, for the review's close-ups.
    public Vector3 Palm(bool right){int i=right?1:0;return hands[i].hand.TransformDirection(grips[i].PalmLocal).normalized;}
    public Vector3 Fingers(bool right){int i=right?1:0;return hands[i].hand.TransformDirection(grips[i].FingersLocal).normalized;}
    public VisitorCinematicActor(Transform actor)
    {
        root=actor;animation=actor.GetComponentInChildren<Animation>();
        foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())skin.forceMatrixRecalculationPerRender=true;
        if(animation==null)throw new System.InvalidOperationException("Cinematic actor requires Animation: "+actor.name);
        idle=Clip("Breathe","Idle");walk=animation.GetClip("Walk");walkBack=Clip("WalkBackward","Walk");talk=Clip("Talk");animation.enabled=false;
        if(idle==null || walk==null)throw new System.InvalidOperationException("Missing cinematic Idle/Walk clips");
        head=Bone("Head");spine=Bone("Chest");leftHand=Bone("HandL");rightHand=Bone("HandR");
        bones=root.GetComponentsInChildren<Transform>(true);rest=bones.Select(b=>b.localRotation).ToArray();walkPose=new Quaternion[bones.Length];talkPose=new Quaternion[bones.Length];
        var body=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s=>System.Array.IndexOf(s.bones,leftHand)>=0);
        for(int i=0;i<2;i++)
        {
            string side=i==0?"L":"R";var hand=i==0?leftHand:rightHand;
            arms[i]=new CinematicArm(root,spine,Bone("UpperArm"+side),Bone("Forearm"+side),hand,body,i==1);
            hands[i]=new CinematicHand(hand,body,root);
            grips[i]=new HandGripPose(hand,root,palmDown:true);
            var sum=Vector3.zero;int n=0;var each=new System.Collections.Generic.List<Vector3>();
            foreach(var f in new[]{"Index1","Middle1","Ring1","Little1"}){var b=hand.Find(f+side);if(b!=null){sum+=b.localPosition;each.Add(b.localPosition);n++;}}
            knuckleEach[i]=each.ToArray();
            knuckles[i]=n>0?sum/n:grips[i].FingersLocal*.08f;
        }
    }
    AnimationClip Clip(params string[] names){foreach(var n in names){var c=animation.GetClip(n);if(c!=null)return c;}return null;}
    Transform Bone(params string[] names)=>root.GetComponentsInChildren<Transform>(true).First(t=>names.Contains(t.name));
    // backward: stepping back (the backward walk take, shorter strides)
    public void Pose(Vector3 position,float yaw,float time,bool moving,bool speaking,Vector3 gaze,float lean=0,float distance=0,float walkWeight=1,bool backward=false)
    {
        for(int i=0;i<bones.Length;i++)bones[i].localRotation=rest[i];
        if(moving)
        {
            var steps=backward?walkBack:walk;
            steps.SampleAnimation(animation.gameObject,Mathf.Repeat(distance/(backward?.8f:1.05f),1)*steps.length);
            for(int i=0;i<bones.Length;i++)walkPose[i]=bones[i].localRotation;
        }
        talkWeight=Mathf.MoveTowards(talkWeight,speaking && !moving && talk!=null?1:0,Time.deltaTime*2.2f);
        if(talkWeight>0)
        {
            talk.SampleAnimation(animation.gameObject,Mathf.Repeat(time,talk.length));
            for(int i=0;i<bones.Length;i++)talkPose[i]=bones[i].localRotation;
        }
        idle.SampleAnimation(animation.gameObject,Mathf.Repeat(time,Mathf.Max(.1f,idle.length)));
        if(talkWeight>0){float w=Mathf.SmoothStep(0,1,talkWeight);for(int i=0;i<bones.Length;i++)bones[i].localRotation=Quaternion.Slerp(bones[i].localRotation,talkPose[i],w);}
        if(moving)for(int i=0;i<bones.Length;i++)bones[i].localRotation=Quaternion.Slerp(bones[i].localRotation,walkPose[i],walkWeight);
        ContactError=0;WristLoad=0;
        root.localPosition=position;root.localRotation=Quaternion.Euler(0,yaw,0);
        spine.rotation=Quaternion.AngleAxis(lean,root.right)*spine.rotation;
        Vector3 delta=root.InverseTransformDirection(gaze-head.position);
        float turn=Mathf.Clamp(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,-28,28);
        // Downward look is limited so the hood or cap brim never hides the face in close-ups.
        float pitch=Mathf.Clamp(-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg,-16,12);
        head.rotation=Quaternion.AngleAxis(turn*.7f,root.up)*Quaternion.AngleAxis(pitch*.65f,root.right)*head.rotation;
    }

    // Palm flat on a surface: `into` points from the palm into the surface, `along` where the fingers point.
    public void Flat(bool right,Vector3 point,Vector3 into,Vector3 along,float weight)
    {
        int i=right?1:0;if(weight<=0)return;
        hands[i].Shape(CinematicHand.Flat,weight);
        Place(i,grips[i].Rotation(into,along),grips[i].ContactLocal,point,weight);
    }
    // A thin plate (the tablet) held by its side edge the way a book is: the edge in the palm, the thumb on the face
    // towards the holder, the fingers reaching through past the far face and folding over it. The hand may turn about
    // the edge by up to 35 degrees either way to whatever keeps the wrist straightest for where the arm is.
    // edge: on the edge, halfway through the plate; inward: from the edge towards the plate's centre; near: the plate's
    // normal on the holder's side.
    public void Edge(bool right,Vector3 edge,Vector3 inward,Vector3 near,float thickness,float weight)
    {
        int i=right?1:0;if(weight<=0)return;var hand=hands[i].hand;
        hands[i].Shape(CinematicHand.Edge,weight,aimThumb:true);
        inward=inward.normalized;near=Vector3.ProjectOnPlane(near,inward).normalized;
        // the knuckles go 1 cm past the far face, so the fingers have something to fold over
        float reach=Vector3.Dot(knuckles[i]-grips[i].ContactLocal,grips[i].FingersLocal)*hand.lossyScale.x;
        Vector3 contact=edge-inward*.004f+near*(reach-thickness*.5f-.012f),local=grips[i].ContactLocal;
        // every knuckle stays at least 1 cm past the far face whatever the turn, or the fingers would fold into the
        // plate (or round the near face): turns that break this cost more than any wrist bend
        float Knuckles(CinematicArm.Plan plan)
        {
            float cost=0;
            foreach(var k in knuckleEach[i])
            {
                float depth=Vector3.Dot(contact+plan.hand*Vector3.Scale(k-local,hand.lossyScale)-edge,-near);
                cost+=Mathf.Pow(Mathf.Max(0,thickness*.5f+.01f-depth)/.004f,2);
            }
            return cost;
        }
        Place(i,grips[i].Rotation(inward,-near),local,contact,weight,inward,-35,35,Knuckles);
        hands[i].AimThumb(edge+inward*.035f+near*(thickness*.5f+.008f),weight);
    }
    // A closed fist knocking: the knuckles on `point`, `into` pointing into the door, palm down.
    public void Knock(bool right,Vector3 point,Vector3 into,float weight)
    {
        int i=right?1:0;if(weight<=0)return;var hand=hands[i].hand;
        hands[i].Shape(CinematicHand.Fist,weight,aimThumb:true);
        var front=knuckles[i]+grips[i].FingersLocal*(.012f/Mathf.Max(1e-4f,hand.lossyScale.x));
        Place(i,grips[i].Rotation(Vector3.ProjectOnPlane(-root.up,into),into),front,point,weight);
        // the thumb lies across the middle phalanges of the index and middle fingers
        var palm=hand.TransformDirection(grips[i].PalmLocal).normalized;
        hands[i].AimThumb((hands[i].Middle(1)+hands[i].Middle(2))*.5f+palm*.012f,weight);
    }
    // The index finger's tip on `point` (a key, a screen), the finger pointing between `along` and the line from the
    // shoulder (a pointing finger continues the arm; the wrist does not fold to aim it), the others curled.
    public void Point(bool right,Vector3 point,Vector3 along,float weight)
    {
        int i=right?1:0;if(weight<=0)return;var hand=hands[i].hand;
        along=(along.normalized+(point-arms[i].upper.position).normalized).normalized;
        hands[i].Shape(CinematicHand.Point,weight,aimThumb:true);
        var palm=hand.TransformDirection(grips[i].PalmLocal).normalized;
        hands[i].AimThumb(hands[i].Middle(2)+palm*.012f,weight);
        var tip=hand.InverseTransformPoint(hands[i].Tip(1));
        Place(i,grips[i].Rotation(Vector3.ProjectOnPlane(-root.up,along),along),tip,point,weight);
    }
    // Closed around a bar (a door lever) whose centre line runs along `axis`; `palmFacing`: the way the palm faces.
    public void Bar(bool right,Vector3 centre,Vector3 axis,Vector3 palmFacing,float radius,float weight)
    {
        int i=right?1:0;if(weight<=0)return;
        hands[i].Shape(CinematicHand.Bar,weight,aimThumb:true);
        Vector3 fingers=Vector3.Cross(axis,palmFacing);if(Vector3.Dot(fingers,centre-root.position)<0)fingers=-fingers;
        Place(i,grips[i].Rotation(palmFacing,fingers),PowerGrip.Anchor(grips[i],radius),centre,weight);
        var palm=hands[i].hand.TransformDirection(grips[i].PalmLocal).normalized;
        hands[i].AimThumb(centre+palm*(radius+.012f)-fingers*.01f,weight);
    }
    // Wrist to a point, the hand as the clip holds it.
    public void Reach(bool right,Vector3 target,float weight)
    {
        int i=right?1:0;if(weight<=0)return;
        arms[i].Apply(arms[i].Solve(target,null),weight);
    }
    // Arm solved so the hand-local point `local` lands on `point` with the hand turned as close to `rotation` as the
    // wrist allows (when the wrist reaches its limit the hand turns less and the wrist target follows). With a free
    // axis, the hand may also turn about it (through `point`) by `from` to `to` degrees: the arm takes the turn that
    // keeps the wrist straightest.
    void Place(int i,Quaternion rotation,Vector3 local,Vector3 point,float weight,Vector3 free=default,float from=0,float to=0,System.Func<CinematicArm.Plan,float> extra=null)
    {
        var arm=arms[i];Vector3 scaled=Vector3.Scale(local,arm.hand.lossyScale);
        CinematicArm.Plan Reach(Quaternion r)
        {
            var p=arm.Solve(point-r*scaled,r);
            for(int it=0;it<2;it++)p=arm.Solve(point-p.hand*scaled,r);
            return p;
        }
        CinematicArm.Plan plan;
        if(free==Vector3.zero)plan=Reach(rotation);
        else
        {
            float best=from;plan=default;plan.cost=float.PositiveInfinity;
            void Try(float a){a=Mathf.Clamp(a,from,to);var p=Reach(Quaternion.AngleAxis(a,free)*rotation);if(extra!=null)p.cost+=extra(p);if(p.cost<plan.cost){plan=p;best=a;}}
            for(int k=0;k<=6;k++)Try(Mathf.Lerp(from,to,k/6f));
            float step=(to-from)/12;Try(best-step);Try(best+step);
        }
        arm.Apply(plan,weight);
        ContactError=Mathf.Max(ContactError,Vector3.Distance(arm.hand.TransformPoint(local),point)*weight);
        float load=Mathf.Sqrt(Mathf.Pow(arm.Flexion/(arm.Flexion>=0?70:55),2)+Mathf.Pow(arm.Deviation/(arm.Deviation>=0?20:30),2));
        WristLoad=Mathf.Max(WristLoad,load);
    }
}
