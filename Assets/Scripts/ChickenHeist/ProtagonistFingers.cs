using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(450)]
public sealed class ProtagonistFingers : MonoBehaviour
{
    sealed class Finger {public Transform bone;public Quaternion rest,previous;public Vector3 axis;public string name;public bool right;public float curl;}
    readonly List<Finger> fingers=new List<Finger>();
    ProtagonistArticulation action;
    HandheldPhone phone;
    bool posed;
    void Awake()
    {
        action=GetComponent<ProtagonistArticulation>();phone=GetComponent<HandheldPhone>();
        foreach(var bone in GetComponentsInChildren<Transform>())
            foreach(string name in new[]{"Thumb","Index","Middle","Ring","Little"})
                if(bone.name.StartsWith(name) && bone.name.Length==name.Length+2)
                    fingers.Add(new Finger{bone=bone,rest=bone.localRotation,name=name,right=bone.name.EndsWith("R")});
        foreach(var finger in fingers)
        {
            finger.axis=Vector3.right;
            foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                int index=System.Array.IndexOf(skin.bones,finger.bone);if(index<0)continue;
                var bind=skin.sharedMesh.bindposes[index];
                int parentIndex=System.Array.IndexOf(skin.bones,finger.bone.parent);
                if(parentIndex>=0)finger.rest=(skin.sharedMesh.bindposes[parentIndex]*bind.inverse).rotation;
                Vector3 along=bind.inverse.MultiplyVector(Vector3.up).normalized;
                Vector3 palm=skin.transform.InverseTransformDirection(-transform.up).normalized;
                finger.axis=bind.MultiplyVector(Vector3.Cross(along,palm)).normalized;
                break;
            }
        }
    }
    void Restore(){if(!posed)return;foreach(var f in fingers)f.bone.localRotation=f.previous;posed=false;}
    void Update(){Restore();}
    void LateUpdate()
    {
        if(action==null)return;
        foreach(var f in fingers)
        {
            f.previous=f.bone.localRotation;
            string state=phone!=null && phone.IsBusy && f.right?"Phone":action.ActionState;
            float angle=state=="Drive"?38:state=="Carry"?18:state=="Phone"?10:state=="Lockpick" || state=="Ignite"?24:0;
            if(f.name=="Thumb")angle*=.45f;
            if((state=="Lockpick" || state=="Ignite") && f.name=="Index")angle=18+Mathf.Sin(Time.time*5)*4;
            f.curl=Mathf.MoveTowards(f.curl,angle,Time.deltaTime*140);
            if(angle>0 || f.curl>.1f)f.bone.localRotation=Quaternion.Slerp(f.previous,f.rest*Quaternion.AngleAxis(f.curl,f.axis),Mathf.Clamp01(f.curl/12));
            if(state=="Phone" && f.name=="Thumb")
            {
                Vector3 direction=phone.handset.up+phone.handset.forward*.15f;
                f.bone.rotation=Quaternion.Slerp(f.bone.rotation,Quaternion.FromToRotation(f.bone.up,direction)*f.bone.rotation,phone.Progress);
            }
        }
        posed=true;
    }
    void OnDisable(){Restore();}
}
