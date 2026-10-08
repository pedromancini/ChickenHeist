using UnityEngine;

// First-person body: nothing is drawn while standing, walking or running (no legs when looking down, no loose
// hands at the screen edge); when the hands do something (carry, lockpick, drive, ignition, phone, picking up
// a bird, trading) the upper-body copy with arms is shown. `legs` is a leftover from older installs.
[DefaultExecutionOrder(460)]
public sealed class FirstPersonArms : MonoBehaviour
{
    public Renderer full,legs;
    ProtagonistArticulation action;
    RuralCharacterAnimator driver;
    void Awake(){action=GetComponent<ProtagonistArticulation>();driver=GetComponent<RuralCharacterAnimator>();}
    public bool HandsInView
    {
        get
        {
            string state=driver!=null?driver.CurrentState:"";
            return (action!=null && action.ActionState!="Idle") || state=="Pickup" || state=="Trade";
        }
    }
    void LateUpdate()
    {
        if(full==null)return;
        bool hands=HandsInView;
        if(full.enabled!=hands)full.enabled=hands;
        if(legs!=null && legs.enabled)legs.enabled=false;
    }
}
