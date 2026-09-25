using System.Linq;
using UnityEngine;

// Deterministically sample existing rigged animation, then layer gaze and hand IK.
// Every evaluation begins from the clip, so replay/skip/shot changes cannot accumulate rotations.
public sealed class VisitorCinematicActor
{
    public readonly Transform root,head,spine,leftHand,rightHand;
    readonly Transform leftArm,leftElbow,rightArm,rightElbow;
    readonly Animation animation;readonly AnimationClip idle,walk,talk;
    readonly Transform[] bones;readonly Quaternion[] rest,walkPose,talkPose;
    float talkWeight;
    readonly Vector3[] handContact=new Vector3[2];readonly Quaternion[] handFrame=new Quaternion[2];
    readonly Transform[][] fingers=new Transform[2][];readonly Quaternion[][] fingerRest=new Quaternion[2][];
    public float ContactError {get;private set;}
    public VisitorCinematicActor(Transform actor)
    {
        root=actor;animation=actor.GetComponentInChildren<Animation>();
        foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())skin.forceMatrixRecalculationPerRender=true;
        if(animation==null)throw new System.InvalidOperationException("Cinematic actor requires Animation: "+actor.name);
        idle=Clip("Breathe","Idle");walk=animation.GetClip("Walk");animation.enabled=false;
        // Captured conversation take: "Talk" on ProtagonistRig, "Trade" (Talk01) on the Medieval People cast.
        talk=actor.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="ProtagonistRig")?Clip("Talk"):Clip("Talk","Trade");
        if(idle==null || walk==null)throw new System.InvalidOperationException("Missing cinematic Idle/Walk clips");
        head=Bone("Head");spine=Bone("Chest","Spine_02","Spine");
        leftArm=Bone("UpperArmL","Upperarm_L");leftElbow=Bone("ForearmL","Lowerarm_L");leftHand=Bone("HandL","Hand_L");
        rightArm=Bone("UpperArmR","Upperarm_R");rightElbow=Bone("ForearmR","Lowerarm_R");rightHand=Bone("HandR","Hand_R");
        bones=root.GetComponentsInChildren<Transform>(true);rest=bones.Select(b=>b.localRotation).ToArray();walkPose=new Quaternion[bones.Length];talkPose=new Quaternion[bones.Length];
        idle.SampleAnimation(animation.gameObject,0);
        for(int i=0;i<2;i++)
        {
            var hand=i==0?leftHand:rightHand;
            var tip=hand.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.StartsWith("Index1") || t.name.StartsWith("Index_01"));
            Vector3 along=tip!=null?(tip.position-hand.position).normalized:-root.up;
            handContact[i]=hand.InverseTransformVector(along*.055f);
            handFrame[i]=Quaternion.LookRotation(hand.InverseTransformDirection(-root.forward),hand.InverseTransformDirection(along));
            fingers[i]=hand.GetComponentsInChildren<Transform>().Where(t=>t!=hand && (t.name.StartsWith("Index") || t.name.StartsWith("Middle") || t.name.StartsWith("Ring") || t.name.StartsWith("Pinky") || t.name.StartsWith("Little") || t.name.StartsWith("Thumb"))).ToArray();
            fingerRest[i]=fingers[i].Select(t=>t.localRotation).ToArray();
        }
    }
    AnimationClip Clip(params string[] names){foreach(var n in names){var c=animation.GetClip(n);if(c!=null)return c;}return null;}
    Transform Bone(params string[] names)=>root.GetComponentsInChildren<Transform>(true).First(t=>names.Contains(t.name));
    public void Pose(Vector3 position,float yaw,float time,bool moving,bool speaking,Vector3 gaze,float lean=0,float distance=0,float walkWeight=1)
    {
        for(int i=0;i<bones.Length;i++)bones[i].localRotation=rest[i];
        if(moving)
        {
            walk.SampleAnimation(animation.gameObject,Mathf.Repeat(distance/1.05f,1)*walk.length);
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
        ContactError=0;
        root.localPosition=position;root.localRotation=Quaternion.Euler(0,yaw,0);
        spine.rotation=Quaternion.AngleAxis(lean,root.right)*spine.rotation;
        Vector3 delta=root.InverseTransformDirection(gaze-head.position);
        float turn=Mathf.Clamp(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,-28,28);
        // Downward look is limited so the cap brim never hides the face in close-ups.
        float pitch=Mathf.Clamp(-Mathf.Atan2(delta.y,new Vector2(delta.x,delta.z).magnitude)*Mathf.Rad2Deg,-16,12);
        head.rotation=Quaternion.AngleAxis(turn*.7f,root.up)*Quaternion.AngleAxis(pitch*.65f,root.right)*head.rotation;
    }
    public void Grip(bool right,Vector3 surface,Vector3 normal,Vector3 along,float weight=1)
    {
        int i=right?1:0;var hand=right?rightHand:leftHand;
        Quaternion desired=Quaternion.LookRotation(normal,along)*Quaternion.Inverse(handFrame[i]);
        Vector3 wrist=surface-desired*Vector3.Scale(handContact[i],hand.lossyScale);
        Reach(right,wrist,weight);hand.rotation=Quaternion.Slerp(hand.rotation,desired,weight);
        ContactError=Mathf.Max(ContactError,Vector3.Distance(hand.TransformPoint(handContact[i]),surface)*weight);
        foreach(var finger in fingers[i])
        {
            int j=System.Array.IndexOf(fingers[i],finger);
            Vector3 axis=finger.parent.InverseTransformDirection(Vector3.Cross(along,normal));
            float curl=finger.name.StartsWith("Thumb")?13: finger.name.Contains("1")?12:24;
            finger.localRotation=Quaternion.AngleAxis(curl*weight,axis)*fingerRest[i][j];
        }
    }
    public void Reach(bool right,Vector3 target,float weight=1)
    {
        Transform upper=right?rightArm:leftArm,elbow=right?rightElbow:leftElbow,hand=right?rightHand:leftHand;
        target=Vector3.Lerp(hand.position,target,Mathf.Clamp01(weight));
        float a=Vector3.Distance(upper.position,elbow.position),b=Vector3.Distance(elbow.position,hand.position);
        Vector3 delta=target-upper.position;float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.001f,a+b-.002f);
        Vector3 direction=delta.normalized;
        Vector3 pole=Vector3.ProjectOnPlane(root.right*(right?1:-1)*.6f-root.up*.6f+root.forward*.2f,direction).normalized;
        float along=(a*a-b*b+distance*distance)/(2*distance);
        Vector3 bend=upper.position+direction*along+pole*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        upper.rotation=Quaternion.FromToRotation(elbow.position-upper.position,bend-upper.position)*upper.rotation;
        elbow.rotation=Quaternion.FromToRotation(hand.position-elbow.position,upper.position+direction*distance-elbow.position)*elbow.rotation;
    }
}
