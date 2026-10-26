using UnityEngine;

public sealed class ArmTwist
{
    public const float Share=.8f;
    readonly Transform fore,hand,twist;
    readonly Quaternion foreBind,handBind,twistBind;
    readonly Vector3 axis;
    ArmTwist(Transform hand,Transform twist,Quaternion foreBind,Quaternion handBind,Quaternion twistBind)
    {fore=hand.parent;this.hand=hand;this.twist=twist;this.foreBind=foreBind;this.handBind=handBind;this.twistBind=twistBind;axis=hand.localPosition.normalized;}
    public static ArmTwist Find(Transform hand,SkinnedMeshRenderer skin)
    {
        if(hand==null || skin==null || hand.parent==null)return null;
        var fore=hand.parent;var twist=fore.Find("ForearmTwist"+hand.name.Substring(hand.name.Length-1));
        int u=System.Array.IndexOf(skin.bones,fore.parent),f=System.Array.IndexOf(skin.bones,fore),h=System.Array.IndexOf(skin.bones,hand),t=twist!=null?System.Array.IndexOf(skin.bones,twist):-1;
        if(u<0 || f<0 || h<0 || t<0)return null;
        var poses=skin.sharedMesh.bindposes;var foreBind=poses[f].inverse.rotation;
        return new ArmTwist(hand,twist,Quaternion.Inverse(poses[u].inverse.rotation)*foreBind,Quaternion.Inverse(foreBind)*poses[h].inverse.rotation,Quaternion.Inverse(foreBind)*poses[t].inverse.rotation);
    }
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
