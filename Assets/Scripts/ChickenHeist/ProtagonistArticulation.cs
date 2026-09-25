using System.Collections.Generic;
using UnityEngine;

// Additive look and object contact run after clip sampling and before phone/carry IK.
[DefaultExecutionOrder(250)]
public sealed class ProtagonistArticulation : MonoBehaviour
{
    public Transform eyes;
    public float HeadYaw {get;private set;}
    public float HeadPitch {get;private set;}
    public float ContactError {get;private set;}
    public string ActionState {get;private set;}="Idle";
    Transform player,head,neck;
    PlayerMovement movement;
    HandheldPhone phone;
    readonly Transform[] arms=new Transform[6];
    readonly Dictionary<Transform,Quaternion> saved=new Dictionary<Transform,Quaternion>();
    readonly HandGripPose[] grip=new HandGripPose[2];
    GameObject pick,key;
    Material metal;
    float weight,bodyYaw,yawVelocity;
    bool initialized;
    Vector3 standingPosition,standingScale;
    bool focusingLock;float beforeLockPitch;
    void Start()
    {
        player=GetComponentInParent<PlayerMovement>()?.transform;
        if(player==null){enabled=false;return;}
        movement=player.GetComponent<PlayerMovement>();phone=GetComponent<HandheldPhone>();
        if(eyes==null)eyes=player.GetComponentInChildren<Camera>()?.transform;
        var names=new[]{"UpperArmL","ForearmL","HandL","UpperArmR","ForearmR","HandR"};
        foreach(var b in GetComponentsInChildren<Transform>())
        {
            if(b.name=="Head")head=b;if(b.name=="Neck")neck=b;
            for(int i=0;i<names.Length;i++)if(b.name==names[i])arms[i]=b;
        }
        if(head==null || neck==null || System.Array.Exists(arms,b=>b==null)){enabled=false;return;}
        bodyYaw=player.eulerAngles.y;
        standingPosition=transform.localPosition;standingScale=transform.localScale;
        for(int i=0;i<2;i++)grip[i]=new HandGripPose(arms[i*3+2],transform);
        metal=new Material(Shader.Find("Universal Render Pipeline/Lit"));metal.color=new Color(.28f,.3f,.32f);metal.SetFloat("_Metallic",.8f);
        pick=Tool("Gazua",new Vector3(.005f,.005f,.11f));
        key=Tool("Chave de partida",new Vector3(.018f,.008f,.045f));
        initialized=true;
    }
    GameObject Tool(string label,Vector3 scale)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=label;Destroy(obj.GetComponent<Collider>());
        obj.transform.SetParent(transform,false);obj.transform.localScale=scale;obj.GetComponent<Renderer>().sharedMaterial=metal;obj.SetActive(false);return obj;
    }
    void Restore()
    {
        foreach(var p in saved)if(p.Key!=null)p.Key.localRotation=p.Value;
        saved.Clear();
    }
    void Remember(Transform bone){if(!saved.ContainsKey(bone))saved.Add(bone,bone.localRotation);}
    void Update()
    {
        Restore();
        if(!initialized || GameMenu.BlocksInput)return;
        var coop=ChickenCoopLockpick.Active;
        if(coop!=null)
        {
            // Approach through CharacterController collisions, keeping feet on the ground.
            Vector3 delta=coop.InteractionPoint-player.position;delta.y=0;
            float step=Mathf.Min(Mathf.Max(0,delta.magnitude-.43f),Time.deltaTime*1.5f);
            var cc=player.GetComponent<CharacterController>();
            if(cc!=null && cc.enabled && step>0)cc.Move(delta.normalized*step+Vector3.down*Time.deltaTime);
            if(delta.sqrMagnitude>.01f)player.rotation=Quaternion.Slerp(player.rotation,Quaternion.LookRotation(delta),1-Mathf.Exp(-8*Time.deltaTime));
        }
    }
    void LateUpdate()
    {
        if(!initialized || eyes==null)return;
        float dt=GameMenu.BlocksInput?0:Time.deltaTime;
        var coop=ChickenCoopLockpick.Active;var truck=OldPickupTruck.Instance;
        if(coop!=null)
        {
            if(!focusingLock){beforeLockPitch=eyes.GetComponent<PlayerLook>()?.Pitch??eyes.localEulerAngles.x;focusingLock=true;}
            Vector3 aim=player.InverseTransformDirection(coop.InteractionPoint-eyes.position);
            eyes.localRotation=Quaternion.Slerp(eyes.localRotation,Quaternion.LookRotation(aim),1-Mathf.Exp(-8*dt));
        }
        else if(focusingLock){eyes.GetComponent<PlayerLook>()?.RestorePitch(beforeLockPitch);focusingLock=false;}
        bool ignition=OldPickupTruck.IsDriving && truck.ignition.Active;
        bool driving=OldPickupTruck.IsDriving;
        float target=player.eulerAngles.y;
        if(driving || coop!=null)bodyYaw=target;
        else if(movement.estaMovendo || Mathf.Abs(Mathf.DeltaAngle(bodyYaw,target))>45)
            bodyYaw=Mathf.SmoothDampAngle(bodyYaw,target,ref yawVelocity,.16f,360,dt);
        transform.rotation=driving?player.rotation:Quaternion.Euler(0,bodyYaw,0);
        transform.localPosition=Vector3.Lerp(transform.localPosition,standingPosition+(driving?Vector3.up*.50f:Vector3.zero),1-Mathf.Exp(-10*dt));
        transform.localScale=Vector3.Lerp(transform.localScale,standingScale*(driving?.90f:1),1-Mathf.Exp(-10*dt));
        Vector3 direction=coop!=null?coop.InteractionPoint-head.position:eyes.forward;
        var local=transform.InverseTransformDirection(direction.normalized);
        HeadYaw=Mathf.LerpAngle(HeadYaw,Mathf.Clamp(Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg,-65,65),1-Mathf.Exp(-12*dt));
        HeadPitch=Mathf.LerpAngle(HeadPitch,Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y,-1,1))*Mathf.Rad2Deg,-40,55),1-Mathf.Exp(-12*dt));
        Remember(neck);Remember(head);
        neck.rotation=Quaternion.AngleAxis(HeadYaw*.3f,transform.up)*Quaternion.AngleAxis(HeadPitch*.3f,transform.right)*neck.rotation;
        head.rotation=Quaternion.AngleAxis(HeadYaw*.7f,transform.up)*Quaternion.AngleAxis(HeadPitch*.7f,transform.right)*head.rotation;
        ActionState=coop!=null?"Lockpick":ignition?"Ignite":driving?"Drive":phone!=null && phone.IsBusy?"Phone":player.GetComponent<BackpackInventory>()?.chickensCarried>0?"Carry":"Idle";
        weight=Mathf.MoveTowards(weight,coop!=null || ignition?1:0,dt*4);
        pick.SetActive(coop!=null);key.SetActive(ignition);ContactError=0;
        if(coop!=null)
        {
            var point=coop.InteractionPoint;
            for(int side=0;side<2;side++)
            {
                float motion=side==1?(coop.PickPosition-.5f)*.025f:Mathf.Sin(Time.time*4)*.003f;
                Vector3 contact=point+player.right*(side==0?-.055f:.025f)+player.up*(-.025f+motion)-player.forward*.035f;
                Reach(side,contact, -player.forward,player.up,weight);
            }
            pick.transform.position=point-player.forward*.055f+player.right*(coop.PickPosition-.5f)*.018f;
            pick.transform.rotation=player.rotation*Quaternion.Euler(0,(coop.PickPosition-.5f)*22,0);
        }
        else if(ignition && !(phone!=null && phone.IsBusy))
        {
            Vector3 contact=truck.steeringWheel.position+truck.transform.right*.16f-truck.transform.up*.13f+truck.transform.forward*.035f;
            var rotation=Quaternion.AngleAxis(Mathf.Sin(truck.ignition.CursorPosition*Mathf.PI)*35,truck.transform.forward);
            Reach(1,contact,-truck.transform.forward,rotation*truck.transform.up,weight);
            key.transform.SetPositionAndRotation(contact+truck.transform.forward*.025f,truck.transform.rotation);
        }
    }
    void Reach(int side,Vector3 contact,Vector3 normal,Vector3 fingers,float blend)
    {
        int i=side*3;var u=arms[i];var l=arms[i+1];var h=arms[i+2];Remember(u);Remember(l);Remember(h);
        var rotation=grip[side].Rotation(normal,fingers);var target=grip[side].Wrist(contact,rotation);
        float a=Vector3.Distance(u.position,l.position),b=Vector3.Distance(l.position,h.position);
        Vector3 axis=target-u.position;float d=Mathf.Clamp(axis.magnitude,.025f,a+b-.002f);axis.Normalize();
        Vector3 bend=Vector3.ProjectOnPlane(player.right*(side==0?-1:1)-player.up*.7f,axis).normalized;
        if(bend.sqrMagnitude<.001f)bend=Vector3.Cross(axis,player.forward).normalized;
        float along=(a*a-b*b+d*d)/(2*d);Vector3 elbow=u.position+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        u.rotation=Quaternion.Slerp(u.rotation,Quaternion.FromToRotation(l.position-u.position,elbow-u.position)*u.rotation,blend);
        l.rotation=Quaternion.Slerp(l.rotation,Quaternion.FromToRotation(h.position-l.position,target-l.position)*l.rotation,blend);
        h.rotation=Quaternion.Slerp(h.rotation,rotation,blend);ContactError=Mathf.Max(ContactError,Vector3.Distance(grip[side].Contact,contact));
    }
    void OnDisable(){Restore();if(pick!=null)pick.SetActive(false);if(key!=null)key.SetActive(false);}
    void OnDestroy(){if(metal!=null)Destroy(metal);}
}
