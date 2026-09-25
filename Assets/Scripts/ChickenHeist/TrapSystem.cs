using UnityEngine;

public class TrapSystem : MonoBehaviour
{
    public float slowDuration=2.5f;
    public bool triggered;
    Transform wire;
    Material steel,wood;
    Renderer[] legacyRenderers;
    Collider[] legacyColliders;
    void Awake()
    {
        if(GetComponent<SecurityEquipmentAudio>()==null)gameObject.AddComponent<SecurityEquipmentAudio>();
        legacyRenderers=GetComponentsInChildren<Renderer>();legacyColliders=GetComponentsInChildren<Collider>();
        foreach(var r in legacyRenderers)r.enabled=false;
        foreach(var c in legacyColliders)c.enabled=false;
        var old=GetComponent<Renderer>();if(old!=null)old.enabled=false;
        foreach(var c in GetComponents<Collider>()){c.enabled=false;Destroy(c);}
        transform.localScale=Vector3.one;
        steel=SecurityEquipmentVisual.Material("Tripwire galvanized steel",new Color(.48f,.51f,.47f));
        wood=SecurityEquipmentVisual.Material("Tripwire stakes",new Color(.27f,.20f,.12f));
        for(int sign=-1;sign<=1;sign+=2)
        {
            SecurityEquipmentVisual.Rod(transform,"Wire stake",new Vector3(sign*1.2f,0,0),new Vector3(sign*1.2f,.48f,0),.065f,wood);
            SecurityEquipmentVisual.Part(transform,"Rattle can",PrimitiveType.Cylinder,new Vector3(sign*1.08f,.23f,0),new Vector3(.13f,.12f,.13f),steel);
        }
        wire=new GameObject("Tensioned wire").transform;wire.SetParent(transform,false);
        SecurityEquipmentVisual.Rod(wire,"Steel tripwire",new Vector3(-1.2f,.28f,0),new Vector3(1.2f,.28f,0),.012f,steel);
        var trigger=gameObject.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.center=new Vector3(0,.28f,0);trigger.size=new Vector3(2.4f,.08f,.06f);
    }
    void LateUpdate()
    {
        foreach(var r in legacyRenderers)if(r!=null)r.enabled=false;
        foreach(var c in legacyColliders)if(c!=null)c.enabled=false;
        var old=GetComponent<Renderer>();if(old!=null)old.enabled=false;
        if(wire!=null)wire.localPosition=Vector3.down*(triggered?.24f:0);
    }
    public bool TryTrigger(PlayerMovement movement)
    {
        if(movement==null || !FarmSecurityProgression.Installed || GameMenu.BlocksInput ||
            HeistGameManager.Instance?.IsMissionTarget(this)!=true || triggered)return false;
        triggered=true;movement.ApplyTrapSlow(slowDuration);
        NoiseEmitter.EmitGlobal(NoiseSource.TrapTriggered,transform.position);
        HeistGameManager.Instance.ShowMessage("Fio acionado! As latas alertaram o fazendeiro.",3);
        return true;
    }
    void OnTriggerEnter(Collider other){TryTrigger(other.GetComponentInParent<PlayerMovement>());}
    void OnDestroy(){if(steel!=null)Destroy(steel);if(wood!=null)Destroy(wood);}
}
