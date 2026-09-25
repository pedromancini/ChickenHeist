using UnityEngine;

public class RuralCharacterAnimator : MonoBehaviour
{
    public PlayerMovement movement;
    public Animation clips;
    public FarmerSleepSystem farmer;
    public RoadsideWalker walker;
    public Transform gestureBone;
    public bool articulatedPlayer;
    public string CurrentState=>state;
    Vector3 previousPosition;
    void Start(){previousPosition=transform.position;}
    float gestureUntil;
    string gestureState="Trade";
    string state;
    public void Gesture(){PlayGesture("Trade");}
    public void Pickup(){PlayGesture("Pickup");}
    void PlayGesture(string name)
    {
        if(clips==null || clips[name]==null)return;
        gestureState=name;gestureUntil=Time.time+clips[name].length;
        clips[name].time=0;clips.CrossFade(name,.10f);state=name;
    }
    public static string PlayerState(bool moving,bool crouching,bool sprinting,bool airborne,float verticalSpeed)
    {
        if(airborne)return verticalSpeed>0?"Jump":"Fall";
        if(crouching)return moving?"Crouch":"CrouchIdle";
        return moving?(sprinting?"Run":"Walk"):"Idle";
    }
    void Update()
    {
        if(clips==null)return;
        string next=Time.time<gestureUntil?gestureState:movement==null?"Idle":PlayerState(
            movement.estaMovendo,movement.estaAgachado,movement.estaSprinting,
            movement.enabled && movement.IsAirborne,movement.VerticalSpeed);
        if(Time.time>=gestureUntil && walker!=null)next=walker.IsWalking?"Walk":"Idle";
        if(articulatedPlayer && movement!=null)
        {
            if(OldPickupTruck.IsDriving)next=OldPickupTruck.Instance.ignition.Active?"Ignite":"Drive";
            else if(ChickenCoopLockpick.Active!=null)next="Lockpick";
            else if(Time.time>=gestureUntil)
            {
                if(next=="Idle" && movement.GetComponent<BackpackInventory>()?.chickensCarried>0)next="Carry";
                else if(next=="Walk" || next=="Run")next=DirectionalState(next,movement.MoveInput);
            }
        }
        if(Time.time>=gestureUntil && farmer!=null)
        {
            float travelled=(transform.position-previousPosition).magnitude;
            var behaviour=farmer.GetComponent<FarmerStateMachine>();
            next=behaviour!=null && behaviour.Activity==FarmerActivity.Sleeping?"Sleep":travelled>Time.deltaTime*.2f?(behaviour!=null && behaviour.Activity==FarmerActivity.Chasing?"Run":"Walk"):"Idle";
        }
        previousPosition=transform.position;
        if(clips[next]==null)next="Idle";
        if(state!=next){clips.CrossFade(next,.18f);state=next;}
        if(movement!=null && (next=="Walk" || next=="Crouch"))
            clips[next].speed=!articulatedPlayer && movement.MoveInput.y<-.1f?-1:1;
        if(movement!=null && (next.StartsWith("Walk") || next.StartsWith("Run") || next=="Crouch"))
        {
            float normal=movement.estaAgachado?movement.velocidadeAgachado:movement.estaSprinting?movement.velocidadeSprint:movement.velocidadeNormal;
            clips[next].speed=Mathf.Sign(clips[next].speed)*movement.CurrentMoveSpeed/Mathf.Max(.1f,normal);
        }
    }
    public static string DirectionalState(string gait,Vector2 input)
    {
        if(Mathf.Abs(input.x)<.25f)return input.y<-.1f?gait+"Backward":gait;
        string side=input.x<0?"Left":"Right";
        return gait+(input.y>.25f?"Forward":input.y<-.25f?"Backward":"")+side;
    }
}
