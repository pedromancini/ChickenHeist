using UnityEngine;
public class HingedBarrier : MonoBehaviour
{
    Vector3 closedPosition,pivot,interactionLocal;Quaternion closedRotation;float angle,target;bool ready;
    Transform moving;
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
    }
    public void SetOpen(bool open,bool immediate){Initialize();target=open?95:0;if(immediate){angle=target;Apply();}}
    void Update(){if(!ready || GameMenu.BlocksInput)return;angle=Mathf.MoveTowards(angle,target,125*Time.deltaTime);Apply();}
    void Apply(){var turn=Quaternion.AngleAxis(angle,Vector3.up);moving.SetPositionAndRotation(pivot+turn*(closedPosition-pivot),turn*closedRotation);}
}
