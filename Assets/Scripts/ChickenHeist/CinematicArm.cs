using UnityEngine;

// Shoulder, elbow and wrist of a cinematic actor (ProtagonistRig bone names) posed the way a real arm moves:
//  - the elbow is a hinge: it only bends, about the axis it has in the bind pose (vertical in the T-pose, so the
//    forearm folds forwards), and the upper arm turns to aim it
//  - the elbow goes where the wrist stays straightest, preferring down and out to the side; it never rises above the
//    shoulder line on its own or folds into the body
//  - the forearm takes the hand's roll (pronation and supination: the radius turns over the ulna); the wrist itself
//    keeps 15 degrees of it at most
//  - the wrist bends at most 70 degrees towards the palm, 55 towards the back of the hand, 20 towards the thumb and
//    30 towards the little finger; a grip that needs more is left short instead of breaking the wrist
// Bind-pose frames come from the skin's bind poses, so whatever the clip did to the bones never skews them.
public sealed class CinematicArm
{
    public struct Plan{public Quaternion upper,fore,hand;public float swivel,flexion,deviation,roll,forearmRoll,cost;}
    public readonly Transform upper,fore,hand;
    readonly Transform body,chest;readonly bool right;
    readonly Vector3 upperAxis,foreAxis,hinge,palmar,radial;
    readonly Quaternion foreBind,handBind;
    readonly float supination;
    float swivel=float.NaN;
    public float Flexion{get;private set;}
    public float Deviation{get;private set;}
    public float Roll{get;private set;}
    public float ForearmRoll{get;private set;}

    public CinematicArm(Transform body,Transform chest,Transform upper,Transform fore,Transform hand,SkinnedMeshRenderer skin,bool right)
    {
        this.body=body;this.chest=chest;this.upper=upper;this.fore=fore;this.hand=hand;this.right=right;
        Matrix4x4 Bind(Transform t)
        {
            int i=System.Array.IndexOf(skin.bones,t);
            if(i<0)throw new System.InvalidOperationException("Bone not skinned: "+t.name);
            return skin.sharedMesh.bindposes[i].inverse;
        }
        var mu=Bind(upper);var mf=Bind(fore);var mh=Bind(hand);
        Quaternion ru=mu.rotation,rf=mf.rotation,rh=mh.rotation;
        foreBind=Quaternion.Inverse(ru)*rf;handBind=Quaternion.Inverse(rf)*rh;
        upperAxis=fore.localPosition.normalized;foreAxis=hand.localPosition.normalized;
        Vector3 up=skin.transform.InverseTransformDirection(body.up).normalized,forward=skin.transform.InverseTransformDirection(body.forward).normalized;
        Vector3 forearm=((Vector3)mh.GetColumn(3)-(Vector3)mf.GetColumn(3)).normalized;
        // bending moves the forearm towards the front: hinge x forearm = forward
        hinge=Vector3.ProjectOnPlane(Quaternion.Inverse(ru)*Vector3.Cross(forearm,forward),upperAxis).normalized;
        // the palms face down in the bind pose and the thumbs point forwards
        palmar=Vector3.ProjectOnPlane(Quaternion.Inverse(rf)*-up,foreAxis).normalized;
        radial=Vector3.ProjectOnPlane(Vector3.ProjectOnPlane(Quaternion.Inverse(rf)*forward,foreAxis),palmar).normalized;
        supination=Vector3.Dot(Quaternion.AngleAxis(90,foreAxis)*palmar,radial)>0?1:-1;
    }

    // Arm pose that puts the wrist on `wrist` with the hand turned towards `handRotation` (world; null keeps the
    // hand as the clip holds it relative to the forearm). Nothing is moved until Apply.
    public Plan Solve(Vector3 wrist,Quaternion? handRotation)
    {
        Vector3 S=upper.position;float a=Vector3.Distance(S,fore.position),b=Vector3.Distance(fore.position,hand.position);
        Vector3 d=wrist-S;Vector3 dir=d.sqrMagnitude>1e-8f?d.normalized:-body.up;
        float dist=Mathf.Clamp(d.magnitude,Mathf.Abs(a-b)+.002f,(a+b)*.998f);
        float along=(a*a-b*b+dist*dist)/(2*dist),r=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        Vector3 C=S+dir*along,W=S+dir*dist;
        Vector3 e1=Vector3.ProjectOnPlane(-body.up,dir);if(e1.sqrMagnitude<1e-6f)e1=Vector3.ProjectOnPlane(-body.forward,dir);e1.Normalize();
        Vector3 e2=Vector3.Cross(dir,e1);
        Vector3 side=body.right*(right?1:-1);
        Vector3 natural=(-body.up+side*.55f-body.forward*.25f).normalized;
        Quaternion handLocal=Quaternion.Inverse(fore.rotation)*hand.rotation;
        Vector3 Elbow(float phi)=>C+(e1*Mathf.Cos(phi*Mathf.Deg2Rad)+e2*Mathf.Sin(phi*Mathf.Deg2Rad))*r;
        float Cost(float phi)
        {
            Vector3 E=Elbow(phi),o=r>1e-4f?(E-C)/r:e1;
            float cost=3*(1-Vector3.Dot(o,natural));
            // the elbow stays out of the body and below the shoulder line
            float lateral=Vector3.Dot(E-chest.position,side);if(lateral<.14f)cost+=Sq((.14f-lateral)/.03f);
            float rise=Vector3.Dot(E-S,body.up);if(rise>.02f)cost+=Sq((rise-.02f)/.05f);
            if(handRotation.HasValue)
            {
                Arm(E,W,out _,out var rf);Wrist(rf,handRotation.Value,out float swing,out float roll);
                cost+=Sq(Mathf.Max(0,swing-20)/10)+Sq(Mathf.Max(0,Mathf.Abs(roll)-110)/20)+Mathf.Abs(roll)/90;
            }
            if(!float.IsNaN(swivel))cost+=Sq(Mathf.DeltaAngle(phi,swivel)/30);
            return cost;
        }
        float best=0,bestCost=float.PositiveInfinity;
        for(float phi=-180;phi<180;phi+=7.5f){float c=Cost(phi);if(c<bestCost){bestCost=c;best=phi;}}
        for(float phi=best-6;phi<=best+6;phi+=1.5f){float c=Cost(phi);if(c<bestCost){bestCost=c;best=phi;}}
        var plan=new Plan{swivel=best,cost=bestCost};
        Arm(Elbow(best),W,out plan.upper,out var fore0);
        if(!handRotation.HasValue){plan.fore=fore0;plan.hand=fore0*handLocal;return plan;}
        // the forearm takes the roll; the wrist keeps a little of it
        Wrist(fore0,handRotation.Value,out _,out float need);
        float keep=Mathf.Clamp(need*.25f,-15,15);plan.forearmRoll=Mathf.Clamp(need-keep,-40,170);
        plan.fore=fore0*Quaternion.AngleAxis(plan.forearmRoll*supination,foreAxis);
        var delta=Quaternion.Inverse(plan.fore)*handRotation.Value*Quaternion.Inverse(handBind);
        Decompose(delta,foreAxis,out var twist,out var swingQ);
        plan.roll=Mathf.Clamp(Angle(twist,foreAxis)*supination,-15,15);
        Vector3 v=swingQ*foreAxis;
        float flex=Mathf.Atan2(Vector3.Dot(v,palmar),Vector3.Dot(v,foreAxis))*Mathf.Rad2Deg;
        float dev=Mathf.Atan2(Vector3.Dot(v,radial),Vector3.Dot(v,foreAxis))*Mathf.Rad2Deg;
        float e=Mathf.Sqrt(Sq(flex/(flex>=0?70:55))+Sq(dev/(dev>=0?20:30)));
        if(e>1){flex/=e;dev/=e;}
        var bent=Quaternion.FromToRotation(foreAxis,(foreAxis+palmar*Mathf.Tan(flex*Mathf.Deg2Rad)+radial*Mathf.Tan(dev*Mathf.Deg2Rad)).normalized);
        plan.hand=plan.fore*bent*Quaternion.AngleAxis(plan.roll*supination,foreAxis)*handBind;
        plan.flexion=flex;plan.deviation=dev;
        return plan;
    }

    // Blends from the clip pose to the plan.
    public void Apply(Plan plan,float weight)
    {
        if(weight<=0)return;weight=Mathf.Clamp01(weight);
        Quaternion f0=fore.rotation,h0=hand.rotation;
        upper.rotation=Quaternion.Slerp(upper.rotation,plan.upper,weight);
        fore.rotation=Quaternion.Slerp(f0,plan.fore,weight);
        hand.rotation=Quaternion.Slerp(h0,plan.hand,weight);
        swivel=plan.swivel;
        Flexion=plan.flexion*weight;Deviation=plan.deviation*weight;Roll=plan.roll*weight;ForearmRoll=plan.forearmRoll*weight;
    }

    // Upper arm aimed at the elbow with its hinge across the arm's plane, forearm bent about that hinge to the wrist.
    void Arm(Vector3 E,Vector3 W,out Quaternion ru,out Quaternion rf)
    {
        Vector3 S=upper.position,u=(E-S).normalized,f=(W-E).normalized,n=Vector3.Cross(u,f);
        if(n.sqrMagnitude<1e-6f)n=upper.rotation*hinge;
        n=Vector3.ProjectOnPlane(n,u).normalized;
        ru=Quaternion.LookRotation(u,n)*Quaternion.Inverse(Quaternion.LookRotation(upperAxis,hinge));
        rf=ru*foreBind;rf=Quaternion.FromToRotation(rf*foreAxis,f)*rf;
    }
    // How far a hand rotation is from straight on this forearm: swing (degrees off the forearm's line) and roll
    // (degrees about it, positive towards supination).
    void Wrist(Quaternion rf,Quaternion target,out float swing,out float roll)
    {
        var delta=Quaternion.Inverse(rf)*target*Quaternion.Inverse(handBind);
        Decompose(delta,foreAxis,out var twist,out var swingQ);
        swing=Vector3.Angle(foreAxis,swingQ*foreAxis);roll=Angle(twist,foreAxis)*supination;
    }
    static void Decompose(Quaternion q,Vector3 axis,out Quaternion twist,out Quaternion swing)
    {
        var p=Vector3.Project(new Vector3(q.x,q.y,q.z),axis);
        twist=new Quaternion(p.x,p.y,p.z,q.w);float m=Mathf.Sqrt(twist.x*twist.x+twist.y*twist.y+twist.z*twist.z+twist.w*twist.w);
        twist=m<1e-6f?Quaternion.identity:new Quaternion(twist.x/m,twist.y/m,twist.z/m,twist.w/m);
        swing=q*Quaternion.Inverse(twist);
    }
    static float Angle(Quaternion twist,Vector3 axis)
    {
        float s=Vector3.Dot(new Vector3(twist.x,twist.y,twist.z),axis);
        return Mathf.DeltaAngle(0,2*Mathf.Atan2(s,twist.w)*Mathf.Rad2Deg);
    }
    static float Sq(float x)=>x*x;
}
