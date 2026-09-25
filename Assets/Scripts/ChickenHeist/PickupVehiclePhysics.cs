using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PickupVehiclePhysics : MonoBehaviour
{
    public WheelCollider[] axles;
    public Transform[] wheelModels;
    public Rigidbody Body {get;private set;}
    public float SignedSpeed=>Body!=null?Vector3.Dot(Body.linearVelocity,transform.forward):0;
    public float SteeringAngle {get;private set;}
    public int GroundedWheels {get;private set;}
    public bool ExternalControl {get;set;}
    public float maxSpeed=14, motorTorque=780, brakeTorque=2300;
    Quaternion[] wheelRotations;
    float throttle,steer;
    bool brake,occupied;
    public void SetInput(float acceleration,float steering,bool stop,bool driver)
    {throttle=Mathf.Clamp(acceleration,-1,1);steer=Mathf.Clamp(steering,-1,1);brake=stop;occupied=driver;}
    void Awake()
    {
        Body=GetComponent<Rigidbody>();Body.centerOfMass=new Vector3(0,.52f,-.12f);
        Body.maxAngularVelocity=3;Body.solverIterations=12;Body.solverVelocityIterations=6;
        wheelRotations=new Quaternion[wheelModels.Length];
        for(int i=0;i<wheelModels.Length;i++)wheelRotations[i]=Quaternion.Inverse(transform.rotation)*wheelModels[i].rotation;
        foreach(var wheel in axles)wheel.ConfigureVehicleSubsteps(5,12,16);
    }
    readonly Collider[] impactHits=new Collider[32];
    void FixedUpdate(){PhysicsStep(Time.fixedDeltaTime);CheckPedestrians();}
    void CheckPedestrians()
    {
        if(Body==null || GameMenu.BlocksInput || Body.linearVelocity.magnitude<2.5f)return;
        Vector3 center=transform.position+Vector3.up*.8f;
        int count=Physics.OverlapCapsuleNonAlloc(center-transform.forward*2.1f,center+transform.forward*2.1f+Body.linearVelocity*Time.fixedDeltaTime,1.05f,impactHits,~0,QueryTriggerInteraction.Ignore);
        for(int i=0;i<count;i++)
        {
            var c=impactHits[i];if(c.transform.IsChildOf(transform))continue;
            Component person=c.GetComponentInParent<RoadsideWalker>();if(person==null)person=c.GetComponentInParent<FarmerStateMachine>();
            if(person==null)continue;
            var reaction=person.GetComponent<VehicleImpactReaction>();if(reaction==null)reaction=person.gameObject.AddComponent<VehicleImpactReaction>();
            Vector3 target=person.transform.position+Vector3.up*.7f,delta=target-center;bool blocked=false;
            foreach(var hit in Physics.RaycastAll(center,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(person.transform)){blocked=true;break;}
            if(!blocked)reaction.Hit(Body.linearVelocity);
        }
    }
    public void PhysicsStep(float dt)
    {
        if(Body==null || axles.Length!=4)return;
        float speed=SignedSpeed,absolute=Mathf.Abs(speed);
        bool controls=occupied && !GameMenu.BlocksInput && !ProtagonistPhone.IsOpen && !VillageMarket.IsOpen;
        float gas=controls?throttle:0,turn=controls?steer:0;
        bool reversing=gas*speed<-.5f;
        float stopping=!controls?brakeTorque*2:brake?brakeTorque:reversing?brakeTorque*Mathf.Abs(gas):Mathf.Abs(gas)<.05f?65:0;
        float maxSteer=Mathf.Lerp(33,13,Mathf.InverseLerp(2,maxSpeed,absolute));
        SteeringAngle=Mathf.MoveTowards(SteeringAngle,turn*maxSteer,90*dt);
        float inner=0,outer=0;
        if(Mathf.Abs(SteeringAngle)>.01f)
        {
            float radius=2.65f/Mathf.Tan(Mathf.Abs(SteeringAngle)*Mathf.Deg2Rad);
            inner=Mathf.Atan(2.65f/Mathf.Max(1,radius-.835f))*Mathf.Rad2Deg*Mathf.Sign(SteeringAngle);
            outer=Mathf.Atan(2.65f/(radius+.835f))*Mathf.Rad2Deg*Mathf.Sign(SteeringAngle);
        }
        axles[0].steerAngle=SteeringAngle>0?outer:inner;axles[1].steerAngle=SteeringAngle>0?inner:outer;
        GroundedWheels=0;
        for(int i=0;i<axles.Length;i++)
        {
            var wheel=axles[i];if(wheel.isGrounded)GroundedWheels++;
            float limit=gas>=0?maxSpeed:4.5f;
            float power=controls && !brake && !reversing && absolute<limit?gas*motorTorque*Mathf.Lerp(1,.3f,absolute/limit):0;
            wheel.motorTorque=i>=2?power:0;wheel.brakeTorque=stopping;
        }
        AntiRoll(axles[0],axles[1],3800);AntiRoll(axles[2],axles[3],3200);
        if(GroundedWheels>0 && absolute>.1f)
            Body.AddForce(-Body.linearVelocity.normalized*(75+absolute*absolute*1.8f));
        Body.mass=1200+(HouseholdEconomy.Instance?.Account.truckCages??0)*7+(HouseholdEconomy.Instance?.Account.truckChickens??0)*2;
    }
    void AntiRoll(WheelCollider left,WheelCollider right,float stiffness)
    {
        bool l=left.GetGroundHit(out var lh),r=right.GetGroundHit(out var rh);
        float lt=l?Mathf.Clamp01((-left.transform.InverseTransformPoint(lh.point).y-left.radius)/left.suspensionDistance):1;
        float rt=r?Mathf.Clamp01((-right.transform.InverseTransformPoint(rh.point).y-right.radius)/right.suspensionDistance):1;
        float force=(lt-rt)*stiffness;
        if(l)Body.AddForceAtPosition(left.transform.up*-force,left.transform.position);
        if(r)Body.AddForceAtPosition(right.transform.up*force,right.transform.position);
    }
    void LateUpdate()
    {
        for(int i=0;i<axles.Length;i++)
        {
            axles[i].GetWorldPose(out var p,out var q);
            wheelModels[i].SetPositionAndRotation(p,q*wheelRotations[i]);
        }
    }
    public void ResetPose(Vector3 position,Quaternion rotation)
    {
        Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;
        Body.position=position;Body.rotation=rotation;transform.SetPositionAndRotation(position,rotation);
        throttle=steer=0;brake=true;SteeringAngle=0;
        foreach(var wheel in axles){wheel.motorTorque=0;wheel.brakeTorque=brakeTorque*2;wheel.steerAngle=0;}
        Body.WakeUp();Physics.SyncTransforms();
    }
}
