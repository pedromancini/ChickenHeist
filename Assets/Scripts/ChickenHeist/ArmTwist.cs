using UnityEngine;

// Forearm twist of a ProtagonistRig body (Elias, the hooded visitor). The hand turns (pronation, supination) by the
// radius rolling over the ulna: the roll grows along the forearm, nothing at the elbow, all of it at the wrist. With
// the forearm as one bone the whole roll landed at the wrist and the skin knotted there. ForearmTwistL/R (a bone from
// mid-forearm to the wrist, child of the forearm, built by build_elias_v4.py / build_visitor.py) carries a growing
// share of the forearm's skin towards the wrist; this turns it by most of the hand's roll about the forearm, measured
// against the bind pose. Clips also roll the forearm bone itself from the elbow (that knots the elbow): that roll is
// handed over to the hand first, leaving the hand exactly where and how it was. Run it after everything that poses
// the arm (ArmTwistDriver in play, the cinematic actor).
public sealed class ArmTwist
{
    // share of the hand's roll the twist bone takes; the wrist's own blend band carries the rest
    public const float Share=.8f;
    readonly Transform fore,hand,twist;
    readonly Quaternion foreBind,handBind,twistBind;
    readonly Vector3 axis;
    ArmTwist(Transform hand,Transform twist,Quaternion foreBind,Quaternion handBind,Quaternion twistBind)
    {fore=hand.parent;this.hand=hand;this.twist=twist;this.foreBind=foreBind;this.handBind=handBind;this.twistBind=twistBind;axis=hand.localPosition.normalized;}
    // null when the body has no twist bone on that arm (older rigs)
    public static ArmTwist Find(Transform hand,SkinnedMeshRenderer skin)
    {
        if(hand==null || skin==null || hand.parent==null)return null;
        var fore=hand.parent;var twist=fore.Find("ForearmTwist"+hand.name.Substring(hand.name.Length-1));
        int u=System.Array.IndexOf(skin.bones,fore.parent),f=System.Array.IndexOf(skin.bones,fore),h=System.Array.IndexOf(skin.bones,hand),t=twist!=null?System.Array.IndexOf(skin.bones,twist):-1;
        if(u<0 || f<0 || h<0 || t<0)return null;
        var poses=skin.sharedMesh.bindposes;var foreBind=poses[f].inverse.rotation;
        return new ArmTwist(hand,twist,Quaternion.Inverse(poses[u].inverse.rotation)*foreBind,Quaternion.Inverse(foreBind)*poses[h].inverse.rotation,Quaternion.Inverse(foreBind)*poses[t].inverse.rotation);
    }
    // Roll of the hand about the forearm, in degrees, against the bind pose (what the twist bone follows).
    public float HandRoll
    {
        get
        {
            var delta=hand.localRotation*Quaternion.Inverse(handBind);
            float s=Vector3.Dot(new Vector3(delta.x,delta.y,delta.z),axis);
            return Mathf.DeltaAngle(0,2*Mathf.Atan2(s,delta.w)*Mathf.Rad2Deg);
        }
    }
    public void Apply()
    {
        // the forearm bone keeps only its swing from the bind pose (the elbow's bend); its roll goes to the hand
        var delta=fore.localRotation*Quaternion.Inverse(foreBind);var a=foreBind*axis;
        var p=Vector3.Project(new Vector3(delta.x,delta.y,delta.z),a);
        if(p.sqrMagnitude>1e-8f)
        {
            var roll=new Quaternion(p.x,p.y,p.z,delta.w);float m=Mathf.Sqrt(roll.x*roll.x+roll.y*roll.y+roll.z*roll.z+roll.w*roll.w);
            if(m>1e-6f)
            {
                roll=new Quaternion(roll.x/m,roll.y/m,roll.z/m,roll.w/m);
                var held=hand.rotation;fore.localRotation=delta*Quaternion.Inverse(roll)*foreBind;hand.rotation=held;
            }
        }
        twist.localRotation=Quaternion.AngleAxis(HandRoll*Share,axis)*twistBind;
    }
}
