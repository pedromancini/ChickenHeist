using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(450)]
public sealed class ProtagonistFingers : MonoBehaviour
{
    sealed class Finger {public Transform bone;public Quaternion rest,previous;public Vector3 axis,along,palm,spreadAxis;public string name;public bool right;public float curl,spread;}
    readonly List<Finger> fingers=new List<Finger>();
    ProtagonistArticulation action;
    HandheldPhone phone;
    bool posed;
    // Hands on the steering wheel close around the rim (PowerGrip) instead of the flat state curl.
    readonly float[] wheelGrip=new float[2];readonly HandGripPose[] grips=new HandGripPose[2];readonly Transform[] hands=new Transform[2];
    void Awake()
    {
        action=GetComponent<ProtagonistArticulation>();phone=GetComponent<HandheldPhone>();
        foreach(var bone in GetComponentsInChildren<Transform>()){if(bone.name=="HandL")hands[0]=bone;if(bone.name=="HandR")hands[1]=bone;}
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
                finger.along=along;finger.palm=palm;finger.spreadAxis=bind.MultiplyVector(palm).normalized;
                break;
            }
        }
        // A gripping hand closes its fingers together: the knuckle of each finger turns toward the middle finger
        // (the protagonist model is built with the fingers spread wide).
        foreach(var finger in fingers)
        {
            if(finger.name=="Thumb" || finger.name=="Middle" || finger.bone.name[finger.name.Length]!='1' || finger.along==Vector3.zero)continue;
            var middle=fingers.Find(f=>f.name=="Middle" && f.right==finger.right && f.bone.name[f.name.Length]=='1');
            if(middle==null || middle.along==Vector3.zero)continue;
            float angle=Vector3.SignedAngle(Vector3.ProjectOnPlane(finger.along,finger.palm),Vector3.ProjectOnPlane(middle.along,finger.palm),finger.palm);
            finger.spread=Mathf.Clamp(angle*.8f,-25,25);
        }
    }
    void Restore(){if(!posed)return;foreach(var f in fingers)f.bone.localRotation=f.previous;posed=false;}
    void Update(){Restore();}
    void LateUpdate()
    {
        if(action==null)return;
        var truck=OldPickupTruck.IsDriving?OldPickupTruck.Instance:null;
        for(int side=0;side<2;side++)
        {
            bool onWheel=truck!=null && truck.steeringWheel!=null && hands[side]!=null && (side==0 || !(phone!=null && phone.IsBusy) && truck.ignition?.Active!=true);
            wheelGrip[side]=Mathf.MoveTowards(wheelGrip[side],onWheel?1:0,Time.deltaTime*5);
        }
        foreach(var f in fingers)f.previous=f.bone.localRotation;
        foreach(var f in fingers)
        {
            if(wheelGrip[f.right?1:0]>0){f.curl=0;continue;}
            string state=phone!=null && phone.IsBusy && f.right?"Phone":action.ActionState;
            float angle=state=="Drive"?38:state=="Carry"?28:state=="Phone"?10:state=="Lockpick" || state=="Ignite"?24:0;
            if(f.name=="Thumb")angle*=.45f;
            if((state=="Lockpick" || state=="Ignite") && f.name=="Index")angle=18+Mathf.Sin(Time.time*5)*4;
            f.curl=Mathf.MoveTowards(f.curl,angle,Time.deltaTime*140);
            float weight=Mathf.Clamp01(f.curl/12);
            if(angle>0 || f.curl>.1f)f.bone.localRotation=Quaternion.Slerp(f.previous,f.rest*Quaternion.AngleAxis(f.spread*weight,f.spreadAxis)*Quaternion.AngleAxis(f.curl,f.axis),weight);
            if(state=="Phone" && f.name=="Thumb")
            {
                Vector3 direction=phone.handset.up+phone.handset.forward*.15f;
                f.bone.rotation=Quaternion.Slerp(f.bone.rotation,Quaternion.FromToRotation(f.bone.up,direction)*f.bone.rotation,phone.Progress);
            }
        }
        for(int side=0;side<2;side++)
        {
            if(wheelGrip[side]<=0 || truck==null)continue;
            if(grips[side]==null)grips[side]=new HandGripPose(hands[side],transform);
            bool right=side==1;
            foreach(var f in fingers)if(f.right==right)f.bone.localRotation=f.rest;
            var wheel=truck.steeringWheel;
            PowerGrip.Close(grips[side],truck.SteeringGrip(right?1:-1),wheel.up,OldPickupTruck.SteeringRimRadius,-wheel.forward);
            foreach(var f in fingers)if(f.right==right)f.bone.localRotation=Quaternion.Slerp(f.previous,f.bone.localRotation,wheelGrip[side]);
        }
        posed=true;
    }
    void OnDisable(){Restore();}
}
