using UnityEngine;

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
            return (action!=null && action.ActionState!="Idle" && action.ActionState!="Punch") || state=="Pickup" || state=="Trade";
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
