using UnityEngine;

// Keep the view in front of the shoulder line, inside the controller's radius. Seated, the eyes stay 10 cm in front
// of the neck, as on a real head (directly above it, looking down shows the top of the neck).
// Crouched, the whole body kneels: the view then follows the head, so it never ends up inside the body that holds
// the chicken. While picking something up (the body bends to the ground) the view holds still. Detection keeps using the crouch
// profile (FarmerStateMachine, SecurityCamera), so stealth is unchanged.
// Runs after ProtagonistArticulation (250) and before PlayerChickenCarry (350), which places the carried bird below
// the view.
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
        // the view height PlayerMovement gives the controller (computed here too, so a lowered view never stays
        // lowered on frames where the movement code is paused: tablet, market, menus)
        if(!OldPickupTruck.IsDriving && controller!=null)position.y=controller.height-.35f;
        string state=driver!=null?driver.CurrentState:"";
        // where the view sits relative to the head when standing still: learnt once, standing
        if(!calibrated && head!=null && !movement.estaAgachado && !OldPickupTruck.IsDriving && state=="Idle" && Mathf.Abs(Mathf.DeltaAngle(0,eyes.localEulerAngles.x))<8)
        {eyeInHead=head.InverseTransformPoint(eyes.parent.TransformPoint(position));calibrated=true;}
        bool whole=calibrated && !OldPickupTruck.IsDriving && movement.estaAgachado;
        follow=Mathf.MoveTowards(follow,whole?1:0,Time.deltaTime*4);
        if(follow>0)
        {
            var target=eyes.parent.InverseTransformPoint(head.TransformPoint(eyeInHead));
            if(state!="Pickup" || followed==Vector3.zero)followed=followed==Vector3.zero?target:Vector3.Lerp(followed,target,1-Mathf.Exp(-14*Time.deltaTime));
            var head2=followed;
            // kneeling moves the head forward: keep the view as far ahead as standing (inside the controller), and
            // short of any wall in front of it
            head2.z=Mathf.Min(head2.z,.24f);head2.x=Mathf.Clamp(head2.x,-.08f,.08f);
            var from=eyes.parent.TransformPoint(new Vector3(0,head2.y,0));var to=eyes.parent.TransformPoint(head2);
            if(Physics.SphereCast(from,.06f,(to-from).normalized,out var wall,Vector3.Distance(from,to),~0,QueryTriggerInteraction.Ignore) && !wall.transform.IsChildOf(movement.transform))
                head2=Vector3.Lerp(new Vector3(0,head2.y,0),head2,Mathf.Clamp01((wall.distance-.04f)/Mathf.Max(.001f,Vector3.Distance(from,to))));
            position=Vector3.Lerp(position,head2,Mathf.SmoothStep(0,1,follow));
            // the controller is only crouch-high: keep the view under whatever is above it
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
