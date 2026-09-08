using UnityEngine;

public class RuralCharacterAnimator : MonoBehaviour
{
    public PlayerMovement movement;
    public Animation clips;
    public FarmerSleepSystem farmer;
    public RoadsideWalker walker;
    public Transform gestureBone;
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
        if(Time.time>=gestureUntil && farmer!=null)
        {
            float travelled=(transform.position-previousPosition).magnitude;
            next=farmer.State<FarmerAwakeState.HalfAlert?"Sleep":travelled>Time.deltaTime*.2f?(farmer.State==FarmerAwakeState.Chase?"Run":"Walk"):"Idle";
        }
        previousPosition=transform.position;
        if(clips[next]==null)next="Idle";
        if(state!=next){clips.CrossFade(next,.18f);state=next;}
        if(movement!=null && (next=="Walk" || next=="Crouch"))
            clips[next].speed=movement.MoveInput.y<-.1f?-1:1;
    }
}
