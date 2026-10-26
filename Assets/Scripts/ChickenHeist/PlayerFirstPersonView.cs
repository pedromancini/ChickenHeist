using UnityEngine;

[DefaultExecutionOrder(300)]
public sealed class PlayerFirstPersonView : MonoBehaviour
{
    Transform eyes,head;PlayerMovement movement;RuralCharacterAnimator driver;CharacterController controller;
    Vector3 eyeInHead,followed;bool calibrated;float follow;
    void Start()
    {
        driver=GetComponent<RuralCharacterAnimator>();
        foreach(var t in GetComponentsInChildren<Transform>(true))if(t.name=="Head"){head=t;break;}
    }
    void LateUpdate()
    {
        if(StoryDirector.Active || ChickenCoopLockpick.Active!=null || ChickenCoopLockpick.ClosedFrame==Time.frameCount)return;
        if(movement==null){movement=GetComponentInParent<PlayerMovement>();if(movement!=null)controller=movement.GetComponent<CharacterController>();}
        if(eyes==null)eyes=movement!=null?movement.GetComponentInChildren<Camera>()?.transform:null;
        if(eyes==null)return;
        var position=eyes.localPosition;position.z=OldPickupTruck.IsDriving?.10f:.22f;
        if(!OldPickupTruck.IsDriving && controller!=null)position.y=controller.height-.35f;
        string state=driver!=null?driver.CurrentState:"";
        if(!calibrated && head!=null && !movement.estaAgachado && !OldPickupTruck.IsDriving && state=="Idle" && Mathf.Abs(Mathf.DeltaAngle(0,eyes.localEulerAngles.x))<8)
        {eyeInHead=head.InverseTransformPoint(eyes.parent.TransformPoint(position));calibrated=true;}
        bool whole=calibrated && !OldPickupTruck.IsDriving && movement.estaAgachado;
        follow=Mathf.MoveTowards(follow,whole?1:0,Time.deltaTime*4);
        if(follow>0)
        {
            var target=eyes.parent.InverseTransformPoint(head.TransformPoint(eyeInHead));
            if(state!="Pickup" || followed==Vector3.zero)followed=followed==Vector3.zero?target:Vector3.Lerp(followed,target,1-Mathf.Exp(-14*Time.deltaTime));
            var head2=followed;
            head2.z=Mathf.Min(head2.z,.24f);head2.x=Mathf.Clamp(head2.x,-.08f,.08f);
            var from=eyes.parent.TransformPoint(new Vector3(0,head2.y,0));var to=eyes.parent.TransformPoint(head2);
            if(Physics.SphereCast(from,.06f,(to-from).normalized,out var wall,Vector3.Distance(from,to),~0,QueryTriggerInteraction.Ignore) && !wall.transform.IsChildOf(movement.transform))
                head2=Vector3.Lerp(new Vector3(0,head2.y,0),head2,Mathf.Clamp01((wall.distance-.04f)/Mathf.Max(.001f,Vector3.Distance(from,to))));
            position=Vector3.Lerp(position,head2,Mathf.SmoothStep(0,1,follow));
            if(controller!=null && position.y>controller.height-.12f)
            {
                var top=eyes.parent.TransformPoint(new Vector3(0,controller.height-.15f,0));float room=position.y-(controller.height-.15f)+.12f;
                if(Physics.SphereCast(top,.08f,Vector3.up,out var hit,room,~0,QueryTriggerInteraction.Ignore) && !hit.transform.IsChildOf(movement.transform))
                    position.y=Mathf.Min(position.y,controller.height-.15f+hit.distance-.04f);
            }
        }
        else followed=Vector3.zero;
        eyes.localPosition=position;
    }
}
