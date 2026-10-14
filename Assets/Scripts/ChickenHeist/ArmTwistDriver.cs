using UnityEngine;

// Keeps both forearm twists in step with the hands after every animation and grip of the frame.
[DefaultExecutionOrder(900)]
public sealed class ArmTwistDriver : MonoBehaviour
{
    ArmTwist[] arms;
    void Start()
    {
        var bones=GetComponentsInChildren<Transform>(true);
        Transform Bone(string n){foreach(var b in bones)if(b.name==n)return b;return null;}
        var left=Bone("HandL");SkinnedMeshRenderer skin=null;
        foreach(var s in GetComponentsInChildren<SkinnedMeshRenderer>(true))if(System.Array.IndexOf(s.bones,left)>=0){skin=s;break;}
        arms=new[]{ArmTwist.Find(left,skin),ArmTwist.Find(Bone("HandR"),skin)};
    }
    void LateUpdate(){if(arms==null)return;foreach(var a in arms)a?.Apply();}
}
