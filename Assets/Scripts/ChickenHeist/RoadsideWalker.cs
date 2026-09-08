using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class RoadsideWalker : MonoBehaviour
{
    public Vector3[] route;
    public float speed=1.35f;
    public bool IsWalking {get;private set;}
    public float DistanceWalked {get;private set;}
    CharacterController controller;
    int target=1,direction=1;
    float waitUntil,blockedTime,vertical;
    void Awake(){controller=GetComponent<CharacterController>();}
    void Update()
    {
        IsWalking=false;
        if(route==null || route.Length<2)return;
        Vector3 before=transform.position;
        Vector3 delta=route[target]-before;delta.y=0;
        if(delta.magnitude<.45f)
        {
            target+=direction;
            if(target>=route.Length){direction=-1;target=route.Length-2;waitUntil=Time.time+Random.Range(1,3f);}
            else if(target<0){direction=1;target=1;waitUntil=Time.time+Random.Range(1,3f);}
        }
        var player=HeistGameManager.Instance?.player;
        bool giveWay=player!=null && (player.position-before).sqrMagnitude<2.8f;
        if(Time.time>=waitUntil && !giveWay)
        {
            delta=route[target]-before;delta.y=0;
            if(delta.sqrMagnitude>.02f)
            {
                transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(delta),Time.deltaTime*5);
                controller.Move(delta.normalized*speed*Time.deltaTime);
            }
        }
        if(controller.isGrounded && vertical<0)vertical=-2;
        vertical+=Physics.gravity.y*Time.deltaTime;
        controller.Move(Vector3.up*vertical*Time.deltaTime);
        Vector3 travelled=transform.position-before;travelled.y=0;
        DistanceWalked+=travelled.magnitude;IsWalking=travelled.magnitude>.1f*Time.deltaTime;
        if(!giveWay && Time.time>=waitUntil && !IsWalking)blockedTime+=Time.deltaTime;else blockedTime=0;
        if(blockedTime>2){direction=-direction;target=Mathf.Clamp(target+direction,0,route.Length-1);waitUntil=Time.time+1;blockedTime=0;}
    }
}
