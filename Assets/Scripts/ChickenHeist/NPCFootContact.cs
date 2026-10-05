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
    }
}
