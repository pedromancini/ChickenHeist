using UnityEngine;
// A gate or coop door that swings 95 degrees about its hinge side.
//  - It swings away from whoever opens it (pushed open, as a real gate); restored from a save, or opened with nobody
//    near, it swings into its farm. Before, every gate swung the same way, so on some farms it opened outwards into
//    the player.
//  - While it moves it lets people through: its colliders come back only once it has stopped and nobody stands where
//    it now is, so it never shoves or drags the player along.
public class HingedBarrier : MonoBehaviour
{
    Vector3 closedPosition,pivot,interactionLocal,inward;Quaternion closedRotation;float angle,target,side=1;bool ready;
    Transform moving;Collider[] colliders;
    public Vector3 InteractionPoint {get{Initialize();return transform.TransformPoint(interactionLocal);}}
    void Initialize()
    {
        if(ready)return;ready=true;
        // Rotate above the fitting scale; rotating the mesh below a non-uniform
        // scale shears it and its collider. Keep the hierarchy/save IDs intact.
        moving=transform;
        if(GetComponent<RuralGate>()!=null && transform.parent!=null &&
            transform.parent.name==gameObject.name+" - Encaixe" && transform.parent.childCount==1)
            moving=transform.parent;
        closedPosition=moving.position;closedRotation=moving.rotation;
        var renderers=GetComponentsInChildren<Renderer>(true);var bounds=new Bounds(transform.position,Vector3.zero);
        if(renderers.Length>0){bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);}
        interactionLocal=transform.InverseTransformPoint(bounds.center);
        pivot=bounds.size.x>bounds.size.z?new Vector3(bounds.min.x,bounds.min.y,bounds.center.z):new Vector3(bounds.center.x,bounds.min.y,bounds.min.z);
        colliders=moving.GetComponentsInChildren<Collider>(true);
        // into the farm: towards the centre of the farm it belongs to (or away from the world's origin road)
        var farm=GetComponentInParent<FarmLayoutInfo>();
        inward=farm!=null?new Vector3(farm.lot.center.x,pivot.y,farm.lot.center.y)-bounds.center:-bounds.center;inward.y=0;
        side=SwingSide(bounds.center+inward);
    }
    // +1 or -1: the swing direction that carries the gate's free side towards `towards`.
    float SwingSide(Vector3 towards)
    {
        var arm=InteractionPoint-pivot;arm.y=0;
        // a positive turn about up moves the free side along up x arm
        var sweep=Vector3.Cross(Vector3.up,arm);var to=towards-pivot;to.y=0;
        return Vector3.Dot(sweep,to)>=0?1:-1;
    }
    public void SetOpen(bool open,bool immediate)=>SetOpen(open,immediate,null);
    // opener: who pushes it open (the gate swings away from them)
    public void SetOpen(bool open,bool immediate,Vector3? opener)
    {
        Initialize();
        if(open && angle==0)side=opener.HasValue?-SwingSide(opener.Value):SwingSide(InteractionPoint+inward);
        target=open?95*side:0;
        if(immediate){angle=target;Apply();}
    }
    void Update()
    {
        if(!ready || GameMenu.BlocksInput)return;
        angle=Mathf.MoveTowards(angle,target,125*Time.deltaTime);Apply();
        bool still=Mathf.Approximately(angle,target);
        foreach(var c in colliders)
        {
            if(c==null)continue;
            if(!still){c.enabled=false;continue;}
            if(!c.enabled && !Blocking(c))c.enabled=true;
        }
    }
    // somebody stands where the stopped gate is: wait for them to step out before it is solid again
    static bool Blocking(Collider c)
    {
        var b=c.bounds;
        foreach(var hit in Physics.OverlapBox(b.center,b.extents,Quaternion.identity,~0,QueryTriggerInteraction.Ignore))
            if(hit!=c && hit is CharacterController)return true;
        return false;
    }
    void Apply(){var turn=Quaternion.AngleAxis(angle,Vector3.up);moving.SetPositionAndRotation(pivot+turn*(closedPosition-pivot),turn*closedRotation);}
}
