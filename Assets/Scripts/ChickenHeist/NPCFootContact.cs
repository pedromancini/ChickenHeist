using UnityEngine;

[DefaultExecutionOrder(200)]
public class NPCFootContact : MonoBehaviour
{
    public Transform leftFoot,rightFoot;
    public Vector3 leftSole,rightSole;
    // Heel and toe under each boot, in foot-bone space (calibrated in the editor from the skinned mesh by
    // NPCFootCalibration). When present, the lowest of the four touches the ground, so toes and heels no
    // longer dip into the road during the walk cycle.
    public Vector3[] leftContacts,rightContacts;
    float height,floor;
    bool Calibrated=>leftContacts!=null && leftContacts.Length>0 && rightContacts!=null && rightContacts.Length>0;
    public float LowestSole
    {
        get
        {
            if(!Calibrated)return Mathf.Min(leftFoot.TransformPoint(leftSole).y,rightFoot.TransformPoint(rightSole).y);
            float low=float.PositiveInfinity;
            foreach(var p in leftContacts)low=Mathf.Min(low,leftFoot.TransformPoint(p).y);
            foreach(var p in rightContacts)low=Mathf.Min(low,rightFoot.TransformPoint(p).y);
            return low;
        }
    }
    void Start()
    {
        height=transform.localPosition.y;
        // Without a CharacterController nothing settles the root on the floor: measure the floor once
        // (small corrections only, so a stair or a second storey is never mistaken for it).
        if(!Calibrated || transform.parent==null || transform.parent.GetComponent<CharacterController>()!=null)return;
        Vector3 origin=transform.parent.TransformPoint(Vector3.up*height);
        foreach(var hit in Physics.RaycastAll(origin+Vector3.up*.6f,Vector3.down,1.5f,~0,QueryTriggerInteraction.Ignore))
        {
            if(hit.collider.transform.IsChildOf(transform))continue;
            float gap=hit.point.y-origin.y;
            if(Mathf.Abs(gap)<=.15f && (floor==0 || gap>floor))floor=gap;
        }
    }
    void LateUpdate()
    {
        if(leftFoot==null || rightFoot==null || transform.parent==null)return;
        float target=transform.parent.TransformPoint(Vector3.up*height).y;
        // A grounded CharacterController rests skinWidth above the ground: calibrated soles go down to the
        // ground itself; the legacy single sole keeps its original 4 cm allowance.
        var controller=transform.parent.GetComponent<CharacterController>();
        if(controller!=null)target-=Calibrated?controller.skinWidth:.04f;
        else target+=floor;
        transform.position+=Vector3.up*Mathf.Clamp(target-LowestSole,-.6f,.6f);
        // On a slope the lowest sole rests on the ground under the body, so the uphill foot dips into the hill:
        // bend that leg (thigh and shin) until its heel and toe are back on the ground under them.
        if(!Calibrated || sleeper!=null && sleeper.CurrentState=="Sleep")return;
        Plant(leftFoot,leftContacts);Plant(rightFoot,rightContacts);
    }
    RuralCharacterAnimator sleeper;
    void Awake(){sleeper=GetComponentInParent<RuralCharacterAnimator>();if(sleeper==null)sleeper=GetComponentInChildren<RuralCharacterAnimator>();}
    static readonly RaycastHit[] hits=new RaycastHit[8];
    bool Ground(Vector3 p,out float y)
    {
        y=float.NegativeInfinity;var root=transform.parent!=null?transform.parent:transform;float best=float.PositiveInfinity;
        int n=Physics.RaycastNonAlloc(p+Vector3.up*.35f,Vector3.down,hits,.7f,~0,QueryTriggerInteraction.Ignore);
        for(int i=0;i<n;i++)if(!hits[i].transform.IsChildOf(root) && hits[i].distance<best){best=hits[i].distance;y=hits[i].point.y;}
        return best<float.PositiveInfinity;
    }
    void Plant(Transform foot,Vector3[] contacts)
    {
        float lift=0;
        foreach(var c in contacts){var p=foot.TransformPoint(c);if(Ground(p,out float g))lift=Mathf.Max(lift,g-p.y);}
        if(lift<=.008f)return;lift=Mathf.Min(lift,.3f);
        var knee=foot.parent;var hip=knee!=null?knee.parent:null;if(hip==null)return;
        var footRotation=foot.rotation;var target=foot.position+Vector3.up*lift;
        float a=Vector3.Distance(hip.position,knee.position),b=Vector3.Distance(knee.position,foot.position);
        Vector3 reach=target-hip.position;float d=Mathf.Clamp(reach.magnitude,Mathf.Abs(a-b)+.001f,a+b-.001f);Vector3 axis=reach.normalized;
        Vector3 bend=Vector3.ProjectOnPlane(knee.position-hip.position,axis);
        if(bend.sqrMagnitude<1e-6f)bend=Vector3.ProjectOnPlane(transform.forward,axis);bend.Normalize();
        float along=(a*a-b*b+d*d)/(2*d);Vector3 kneeAt=hip.position+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        hip.rotation=Quaternion.FromToRotation(knee.position-hip.position,kneeAt-hip.position)*hip.rotation;
        knee.rotation=Quaternion.FromToRotation(foot.position-knee.position,target-knee.position)*knee.rotation;
        foot.rotation=footRotation;
    }
}
