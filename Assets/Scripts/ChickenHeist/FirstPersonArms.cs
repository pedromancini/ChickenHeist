using UnityEngine;

// First-person body: while standing, walking or running the swinging arms only reach the screen edge as loose
// hands, so the legs-only copy is shown; when the hands do something (carry, lockpick, drive, ignition,
// phone, picking up a bird, trading) the full copy with arms is shown instead.
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
        if(full==null || legs==null)return;
        bool hands=HandsInView;
        if(full.enabled!=hands)full.enabled=hands;
        if(legs.enabled==hands)legs.enabled=!hands;
    }
}
