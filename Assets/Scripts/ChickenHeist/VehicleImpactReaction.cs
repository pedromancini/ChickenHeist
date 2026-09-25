using System.Collections;
using UnityEngine;

public class VehicleImpactReaction : MonoBehaviour
{
    public bool Down {get;private set;}
    CharacterController capsule;float standingHeight;Vector3 standingCenter;
    float cooldown;Transform visual;Quaternion rest;Behaviour[] drivers;bool[] enabledBefore;
    public bool Hit(Vector3 velocity)
    {
        if(Down || Time.time<cooldown || velocity.magnitude<2.5f || GameMenu.BlocksInput)return false;
        var animator=GetComponentInChildren<RuralCharacterAnimator>();if(animator==null)return false;
        visual=animator.transform;rest=visual.localRotation;
        drivers=new Behaviour[]{GetComponent<RoadsideWalker>(),GetComponent<FarmerStateMachine>(),animator,animator.clips,visual.GetComponent<NPCFootContact>()};
        enabledBefore=new bool[drivers.Length];for(int i=0;i<drivers.Length;i++)if(drivers[i]!=null){enabledBefore[i]=drivers[i].enabled;drivers[i].enabled=false;}
        capsule=GetComponent<CharacterController>();if(capsule!=null){standingHeight=capsule.height;standingCenter=capsule.center;capsule.height=capsule.radius*2;capsule.center=Vector3.up*capsule.radius;}
        Down=true;cooldown=Time.time+7;StartCoroutine(Fall(velocity));return true;
    }
    IEnumerator Fall(Vector3 velocity)
    {
        var controller=GetComponent<CharacterController>();var direction=velocity.normalized;direction.y=0;
        var fall=Quaternion.AngleAxis(82,visual.parent!=null?visual.parent.InverseTransformDirection(Vector3.Cross(Vector3.up,direction)):Vector3.right)*rest;
        for(float t=0;t<4;t+=Time.deltaTime)
        {
            if(GameMenu.IsOpen){yield return null;continue;}
            float weight=t<.4f?Mathf.SmoothStep(0,1,t/.4f):t<3?1:1-Mathf.SmoothStep(0,1,t-3);
            visual.localRotation=Quaternion.Slerp(rest,fall,weight);
            if(controller!=null && controller.enabled && t<.5f)controller.Move((direction*Mathf.Min(velocity.magnitude,7)*(1-t*2)+Vector3.down*2)*Time.deltaTime);
            yield return null;
        }
        Restore();
    }
    void Restore(){if(capsule!=null){capsule.height=standingHeight;capsule.center=standingCenter;capsule=null;}if(visual!=null)visual.localRotation=rest;if(drivers!=null)for(int i=0;i<drivers.Length;i++)if(drivers[i]!=null)drivers[i].enabled=enabledBefore[i];drivers=null;Down=false;}
    void OnDisable(){StopAllCoroutines();Restore();}
}
