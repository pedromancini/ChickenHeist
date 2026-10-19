using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// A farmer's alarm wire across the approach to the coop: two painted steel stakes driven into the ground, a thin
// galvanized wire with a little sag at shin height between them, and tin cans hanging off it on strings. Walking
// into it slows Elias and the cans rattle (the farmer hears it). Each farm's wires are laid out from its coop door
// on the first frame: the first across the path two metres out, the next further out and to one side, both square
// to the way in; a stake that would land in a fence or a wall slides in towards the middle.
public class TrapSystem : MonoBehaviour
{
    public float slowDuration=2.5f;
    public bool triggered;
    public const float WireHeight=.22f;
    Transform hanging;
    readonly List<Material> materials=new List<Material>();
    Renderer[] legacyRenderers;
    Collider[] legacyColliders;
    BoxCollider trigger;
    public float Span {get;private set;}
    public Vector3 StakeLeft {get;private set;}
    public Vector3 StakeRight {get;private set;}
    Material Mat(string name,Color color){var m=SecurityEquipmentVisual.Material(name,color);m.SetFloat("_Smoothness",.25f);materials.Add(m);return m;}
    void Awake()
    {
        if(GetComponent<SecurityEquipmentAudio>()==null)gameObject.AddComponent<SecurityEquipmentAudio>();
        // the older plate traps this object was built as: their meshes go for good (the security progression switches
        // every renderer under a trap on and off)
        foreach(var r in GetComponentsInChildren<Renderer>(true)){var f=r.GetComponent<MeshFilter>();DestroyImmediate(r);if(f!=null)DestroyImmediate(f);}
        foreach(var c in GetComponentsInChildren<Collider>(true))DestroyImmediate(c);
        legacyRenderers=new Renderer[0];legacyColliders=new Collider[0];
        transform.localScale=Vector3.one;
        trigger=gameObject.AddComponent<BoxCollider>();trigger.isTrigger=true;
        Layout();Build();
    }
    // Where this wire goes: its order among the farm's wires and the farm's coop door decide it.
    void Layout()
    {
        var farm=GetComponentInParent<FarmLayoutInfo>();var coop=farm!=null?farm.GetComponentInChildren<ChickenCoopLockpick>():null;
        float half=1.1f;Vector3 center=transform.position,across=Vector3.right;
        if(coop!=null)
        {
            var wires=farm.GetComponentsInChildren<TrapSystem>(true).OrderBy(t=>t.name).ToList();int index=Mathf.Max(0,wires.IndexOf(this));
            Vector3 outward=-coop.transform.forward;outward.y=0;outward.Normalize();across=Vector3.Cross(Vector3.up,outward);
            float side=(Mathf.Abs(farm.lot.center.x*7+farm.lot.center.y*3)%2)<1?1:-1;
            float distance=index==0?2.1f:4.4f+(index-1)*2.2f;float shift=index==0?0:side*(index%2==1?.9f:-.9f);
            half=index==0?1.15f:1.4f;
            if(index>0)across=Quaternion.AngleAxis(side*12,Vector3.up)*across;
            center=coop.InteractionPoint+outward*distance+across*shift;
        }
        Vector3 a=Ground(center-across*half),b=Ground(center+across*half);
        // stakes clear of fences, posts and walls: slide in towards the middle until free (never shorter than a metre)
        for(int k=0;k<8 && Blocked(a) && Vector3.Distance(a,b)>1.0f;k++)a=Ground(Vector3.MoveTowards(a,b,.12f));
        for(int k=0;k<8 && Blocked(b) && Vector3.Distance(a,b)>1.0f;k++)b=Ground(Vector3.MoveTowards(b,a,.12f));
        StakeLeft=a;StakeRight=b;Span=Vector3.Distance(new Vector3(a.x,0,a.z),new Vector3(b.x,0,b.z));
        Vector3 line=b-a;line.y=0;
        transform.SetPositionAndRotation((a+b)*.5f,Quaternion.LookRotation(Vector3.Cross(line.normalized,Vector3.up)));
    }
    static Vector3 Ground(Vector3 p)
    {
        float best=float.PositiveInfinity;Vector3 found=p;
        foreach(var hit in Physics.RaycastAll(p+Vector3.up*2,Vector3.down,6,~0,QueryTriggerInteraction.Ignore))
        {
            if(hit.normal.y<.6f || hit.collider.GetComponentInParent<CharacterController>()!=null)continue;
            if(hit.distance<best){best=hit.distance;found=hit.point;}
        }
        return found;
    }
    static bool Blocked(Vector3 p)
    {
        foreach(var c in Physics.OverlapCapsule(p+Vector3.up*.15f,p+Vector3.up*.6f,.07f,~0,QueryTriggerInteraction.Ignore))
            if(c.GetComponentInParent<CharacterController>()==null && !(c is TerrainCollider) && c.bounds.max.y>p.y+.1f)return true;
        return false;
    }
    void Build()
    {
        var paint=Mat("Tripwire stake - green paint",new Color(.16f,.27f,.17f));
        var steel=Mat("Tripwire galvanized steel",new Color(.58f,.60f,.58f));
        var tin=Mat("Tripwire tin can",new Color(.66f,.66f,.62f));
        var string_=Mat("Tripwire string",new Color(.62f,.55f,.40f));
        Vector3 la=transform.InverseTransformPoint(StakeLeft),lb=transform.InverseTransformPoint(StakeRight);
        foreach(var p in new[]{la,lb})
        {
            // steel stake: driven in a few centimetres, a flat top cap, the wire tied a little below it
            SecurityEquipmentVisual.Rod(transform,"Steel stake",p+Vector3.down*.06f,p+Vector3.up*.34f,.028f,paint);
            SecurityEquipmentVisual.Part(transform,"Stake cap",PrimitiveType.Cylinder,p+Vector3.up*.345f,new Vector3(.045f,.008f,.045f),paint);
            SecurityEquipmentVisual.Part(transform,"Wire knot",PrimitiveType.Sphere,p+Vector3.up*WireHeight,Vector3.one*.032f,steel);
            // a little bare earth where it went in
            SecurityEquipmentVisual.Part(transform,"Disturbed earth",PrimitiveType.Cylinder,p+Vector3.up*.004f,new Vector3(.16f,.004f,.16f),Mat("Tripwire earth",new Color(.24f,.18f,.12f)));
        }
        hanging=new GameObject("Wire and cans").transform;hanging.SetParent(transform,false);
        // the wire sags a little between the stakes
        const int Segments=10;Vector3 previous=Sag(la,lb,0);
        for(int i=1;i<=Segments;i++){var next=Sag(la,lb,(float)i/Segments);SecurityEquipmentVisual.Rod(hanging,"Steel tripwire",previous,next,.007f,steel);previous=next;}
        // three cans on strings, a pair near one stake and one near the other
        var random=new System.Random(Mathf.RoundToInt(transform.position.x*13+transform.position.z*7));
        foreach(float t in new[]{.18f,.27f,.78f})
        {
            Vector3 on=Sag(la,lb,t),can=on+Vector3.down*.085f;
            SecurityEquipmentVisual.Rod(hanging,"Can string",on,can+Vector3.up*.045f,.004f,string_);
            var body=SecurityEquipmentVisual.Part(hanging,"Tin can",PrimitiveType.Cylinder,can,new Vector3(.062f,.045f,.062f),tin,
                Quaternion.Euler((float)random.NextDouble()*16-8,(float)random.NextDouble()*360,(float)random.NextDouble()*16-8));
            SecurityEquipmentVisual.Part(body.transform,"Can rim",PrimitiveType.Cylinder,Vector3.up*1.0f,new Vector3(1.06f,.06f,1.06f),steel);
        }
        trigger.center=(la+lb)*.5f+Vector3.up*WireHeight;trigger.size=new Vector3(Span,.34f,.14f);
    }
    static Vector3 Sag(Vector3 a,Vector3 b,float t)=>Vector3.Lerp(a,b,t)+Vector3.up*(WireHeight-.035f*4*t*(1-t));
    void LateUpdate()
    {
        foreach(var r in legacyRenderers)if(r!=null)r.enabled=false;
        foreach(var c in legacyColliders)if(c!=null)c.enabled=false;
        var old=GetComponent<Renderer>();if(old!=null)old.enabled=false;
        // tripped: the wire comes off the knots and lies on the ground with its cans
        if(hanging!=null)hanging.localPosition=Vector3.down*(triggered?WireHeight-.03f:0);
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
    void OnDestroy(){foreach(var m in materials)if(m!=null)Destroy(m);}
}
