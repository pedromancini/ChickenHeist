using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TrapSystem : MonoBehaviour
{
    public float slowDuration=2.5f;
    public bool triggered;
    public const float WireHeight=.22f,CatchHeight=.45f;
    Transform hanging;
    readonly List<Material> materials=new List<Material>();
    readonly List<Mesh> meshes=new List<Mesh>();
    BoxCollider trigger;
    static readonly List<TrapSystem> all=new List<TrapSystem>();
    public float Span {get;private set;}
    public Vector3 StakeLeft {get;private set;}
    public Vector3 StakeRight {get;private set;}
    public Vector3 Inward {get;private set;}
    public RuralGate Gate {get;private set;}
    public bool AtCoop {get;private set;}
    public float Opening {get;private set;}
    public static bool Guards(RuralGate gate)=>gate!=null && FarmSecurityProgression.Installed && all.Any(t=>t!=null && t.Gate==gate);
    Material Mat(string name,Color color){var m=SecurityEquipmentVisual.Material(name,color);m.SetFloat("_Smoothness",.2f);materials.Add(m);return m;}
    void Awake()
    {
        if(GetComponent<SecurityEquipmentAudio>()==null)gameObject.AddComponent<SecurityEquipmentAudio>();
        foreach(var r in GetComponentsInChildren<Renderer>(true)){var f=r.GetComponent<MeshFilter>();DestroyImmediate(r);if(f!=null)DestroyImmediate(f);}
        foreach(var c in GetComponentsInChildren<Collider>(true))DestroyImmediate(c);
        for(int i=transform.childCount-1;i>=0;i--)DestroyImmediate(transform.GetChild(i).gameObject);
        transform.localScale=Vector3.one;
        trigger=gameObject.AddComponent<BoxCollider>();trigger.isTrigger=true;
        Layout();Build();
    }
    void OnEnable(){if(!all.Contains(this))all.Add(this);}
    void OnDisable(){all.Remove(this);}
    void Layout()
    {
        var farm=GetComponentInParent<FarmLayoutInfo>();
        float half=1.1f;Vector3 center=transform.position,across=Vector3.right,inward=Vector3.forward;
        if(farm!=null)
        {
            var wires=farm.GetComponentsInChildren<TrapSystem>(true).OrderBy(t=>t.name).ToList();int index=Mathf.Max(0,wires.IndexOf(this));
            var lot=new Vector3(farm.lot.center.x,0,farm.lot.center.y);
            if(index==0 && Entrance(farm,out var gate,out var middle,out var along,out float width))
            {
                Gate=gate;Opening=width;across=along;inward=Square(along,lot-middle);center=middle-inward*.7f;half=width*.5f+.3f;
            }
            else if(CoopDoor(farm,out var door,out var doorAlong,out float doorWidth,out var outward))
            {
                AtCoop=true;Opening=doorWidth;across=doorAlong;inward=-outward;center=door+outward*(1.55f+Mathf.Max(0,index-1)*1.2f);half=doorWidth*.5f+.4f;
            }
        }
        Inward=inward;
        Vector3 a=Ground(center-across*half),b=Ground(center+across*half);
        for(int k=0;k<8 && Blocked(a) && Vector3.Distance(a,b)>1.0f;k++)a=Ground(Vector3.MoveTowards(a,b,.12f));
        for(int k=0;k<8 && Blocked(b) && Vector3.Distance(a,b)>1.0f;k++)b=Ground(Vector3.MoveTowards(b,a,.12f));
        StakeLeft=a;StakeRight=b;Span=Vector3.Distance(new Vector3(a.x,0,a.z),new Vector3(b.x,0,b.z));
        transform.SetPositionAndRotation((a+b)*.5f,Quaternion.LookRotation(inward,Vector3.up));
    }
    static Vector3 Square(Vector3 along,Vector3 towards){var n=Vector3.Cross(Vector3.up,along).normalized;return Vector3.Dot(n,towards)>=0?n:-n;}
    static bool Entrance(FarmLayoutInfo farm,out RuralGate gate,out Vector3 middle,out Vector3 along,out float width)
    {
        gate=farm.GetComponentsInChildren<RuralGate>(true).OrderBy(g=>Vector3.Distance(Flat(Leaf(g.transform,out _,out _)),Flat(farm.entrance))).FirstOrDefault();
        middle=Vector3.zero;along=Vector3.right;width=0;
        if(gate==null)return false;
        var centre=Leaf(gate.transform,out along,out width);middle=Ground(centre);
        return width>.5f;
    }
    static bool CoopDoor(FarmLayoutInfo farm,out Vector3 middle,out Vector3 along,out float width,out Vector3 outward)
    {
        var coop=farm.GetComponentInChildren<ChickenCoopLockpick>(true);middle=Vector3.zero;along=Vector3.right;width=0;outward=Vector3.back;
        if(coop==null)return false;
        outward=-coop.transform.forward;outward.y=0;outward.Normalize();
        if(coop.door!=null){var centre=Leaf(coop.door,out along,out width);middle=centre;}
        else{middle=coop.InteractionPoint;along=Vector3.Cross(Vector3.up,outward);width=1.1f;}
        width=Mathf.Clamp(width,.8f,2.4f);
        middle=Ground(new Vector3(middle.x,middle.y+.3f,middle.z)+outward*.35f)-outward*.35f;
        return true;
    }
    static Vector3 Leaf(Transform panel,out Vector3 along,out float width)
    {
        along=Vector3.right;width=0;Vector3 centre=panel.position;float best=-1;
        foreach(var f in panel.GetComponentsInChildren<MeshFilter>(true))
        {
            if(f.sharedMesh==null)continue;var b=f.sharedMesh.bounds;var t=f.transform;
            Vector3 x=t.TransformVector(new Vector3(b.size.x,0,0)),z=t.TransformVector(new Vector3(0,0,b.size.z));x.y=0;z.y=0;
            float size=Mathf.Max(x.magnitude,z.magnitude);if(size<=best)continue;
            best=size;along=(x.magnitude>=z.magnitude?x:z).normalized;width=size;centre=t.TransformPoint(b.center);
        }
        return centre;
    }
    static Vector3 Flat(Vector3 p)=>new Vector3(p.x,0,p.z);
    static Vector3 Ground(Vector3 p)
    {
        float best=float.PositiveInfinity;Vector3 found=p;
        foreach(var hit in Physics.RaycastAll(p+Vector3.up*2,Vector3.down,6,~0,QueryTriggerInteraction.Ignore))
        {
            if(hit.normal.y<.6f || hit.collider.GetComponentInParent<CharacterController>()!=null || hit.collider.GetComponentInParent<HingedBarrier>()!=null)continue;
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
        var wood=Mat("Tripwire stake - weathered wood",new Color(.34f,.25f,.16f));
        var steel=Mat("Tripwire galvanized steel",new Color(.64f,.66f,.64f));
        var twine=Mat("Tripwire string",new Color(.66f,.58f,.42f));
        var earth=Mat("Tripwire earth",new Color(.26f,.19f,.12f));
        var cans=new[]{Mat("Tripwire can - tin",new Color(.72f,.72f,.68f)),Mat("Tripwire can - rust",new Color(.50f,.29f,.15f)),Mat("Tripwire can - faded red",new Color(.60f,.21f,.17f))};
        Vector3 la=transform.InverseTransformPoint(StakeLeft),lb=transform.InverseTransformPoint(StakeRight);
        var random=new System.Random(Mathf.RoundToInt(transform.position.x*13+transform.position.z*7));
        float R(float a,float b)=>a+(float)random.NextDouble()*(b-a);
        float floorTop=float.NegativeInfinity,floorLow=float.PositiveInfinity;
        for(int i=0;i<=6;i++){float y=transform.InverseTransformPoint(Ground(Vector3.Lerp(StakeLeft,StakeRight,i/6f))).y;floorTop=Mathf.Max(floorTop,y);floorLow=Mathf.Min(floorLow,y);}
        wireAt=floorTop+WireHeight;
        foreach(var p in new[]{la,lb})
        {
            float tall=wireAt+.26f-p.y;
            Piece("Estaca de madeira",Prism(4,.034f,tall+.06f,.05f),wood,p+Vector3.down*.06f,Quaternion.Euler(R(-4,4),R(0,90),R(-4,4)));
            Piece("Terra remexida",Prism(7,.13f,.025f,0),earth,p+Vector3.down*.012f,Quaternion.Euler(0,R(0,60),0));
            for(int w=0;w<2;w++)Piece("Volta do arame",Prism(6,.041f,.008f,0),steel,new Vector3(p.x,wireAt-.012f+w*.014f,p.z),Quaternion.Euler(R(-6,6),R(0,60),R(-6,6)));
        }
        hanging=new GameObject("Arame e latas").transform;hanging.SetParent(transform,false);
        const int Segments=8;Vector3 previous=Sag(la,lb,0);
        for(int i=1;i<=Segments;i++){var next=Sag(la,lb,(float)i/Segments);Wire("Arame galvanizado",previous,next,.006f,steel,hanging);previous=next;}
        int count=Mathf.Clamp(Mathf.RoundToInt(Span/1.1f),2,5);
        for(int c=0;c<count;c++)
        {
            float t=(c+.5f)/count+R(-.06f,.06f);Vector3 on=Sag(la,lb,t);float drop=R(.025f,.045f);
            Wire("Barbante",on,on+Vector3.down*drop,.003f,twine,hanging);
            var can=Piece("Lata",Prism(8,.035f,.105f,0),cans[(c+random.Next(3))%3],on+Vector3.down*(drop+.105f),Quaternion.Euler(R(-9,9),R(0,45),R(-9,9)),hanging);
            can.transform.localScale=new Vector3(R(.88f,1f),1,R(.92f,1.04f));
        }
        float bottom=floorLow-.05f,top=floorTop+CatchHeight;
        trigger.center=new Vector3(0,(bottom+top)*.5f,0);trigger.size=new Vector3(Span+.12f,top-bottom,.3f);
    }
    float wireAt=WireHeight;
    Vector3 Sag(Vector3 a,Vector3 b,float t){var p=Vector3.Lerp(a,b,t);p.y=wireAt-.012f*4*t*(1-t);return p;}
    GameObject Piece(string name,Mesh mesh,Material material,Vector3 local,Quaternion rotation,Transform parent=null)
    {
        var go=new GameObject(name);go.transform.SetParent(parent??transform,false);go.transform.localPosition=local;go.transform.localRotation=rotation;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
        return go;
    }
    void Wire(string name,Vector3 a,Vector3 b,float thickness,Material material,Transform parent)
    {var go=Piece(name,Prism(4,thickness*.5f,1,0),material,a,Quaternion.FromToRotation(Vector3.up,b-a),parent);go.transform.localScale=new Vector3(1,Vector3.Distance(a,b),1);}
    readonly Dictionary<(int,float,float,float),Mesh> shapes=new Dictionary<(int,float,float,float),Mesh>();
    Mesh Prism(int sides,float radius,float height,float tip)
    {
        if(shapes.TryGetValue((sides,radius,height,tip),out var known))return known;
        var v=new List<Vector3>();var tri=new List<int>();
        void Face(params Vector3[] p){int o=v.Count;v.AddRange(p);for(int k=1;k+1<p.Length;k++){tri.Add(o);tri.Add(o+k);tri.Add(o+k+1);}}
        Vector3 Corner(int i,float y){float a=(i+.5f)*Mathf.PI*2/sides;return new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius);}
        for(int i=0;i<sides;i++)
        {
            int j=(i+1)%sides;
            Face(Corner(i,0),Corner(i,height),Corner(j,height),Corner(j,0));
            Face(Corner(j,0),Vector3.zero,Corner(i,0));
            if(tip>0)Face(Corner(i,height),Vector3.up*(height+tip),Corner(j,height));else Face(Corner(i,height),Vector3.up*height,Corner(j,height));
        }
        var mesh=new Mesh{name="Tripwire piece"};mesh.SetVertices(v);mesh.SetTriangles(tri,0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
        shapes[(sides,radius,height,tip)]=mesh;return mesh;
    }
    void LateUpdate()
    {
        if(hanging!=null)hanging.localPosition=Vector3.down*(triggered?WireHeight-.03f:0);
    }
    public bool TryTrigger(PlayerMovement movement)=>TryTrigger(movement,false);
    bool TryTrigger(PlayerMovement movement,bool truck)
    {
        if((movement==null && !truck) || !FarmSecurityProgression.Installed || GameMenu.BlocksInput ||
            HeistGameManager.Instance?.IsMissionTarget(this)!=true || triggered)return false;
        triggered=true;if(movement!=null)movement.ApplyTrapSlow(slowDuration);
        NoiseEmitter.EmitGlobal(NoiseSource.TrapTriggered,transform.position);
        HeistGameManager.Instance.ShowMessage(truck?"A caminhonete arrebentou o fio! As latas alertaram o fazendeiro.":"Fio acionado! As latas alertaram o fazendeiro.",3);
        return true;
    }
    void OnTriggerEnter(Collider other){TryTrigger(other.GetComponentInParent<PlayerMovement>());}
    void FixedUpdate()
    {
        var truck=OldPickupTruck.Instance;
        if(triggered || !OldPickupTruck.IsDriving || truck==null || trigger==null || !trigger.enabled || (truck.transform.position-transform.position).sqrMagnitude>100)return;
        foreach(var c in Physics.OverlapBox(transform.TransformPoint(trigger.center+Vector3.up*.45f),new Vector3(trigger.size.x*.5f,.7f,trigger.size.z*.5f),transform.rotation,~0,QueryTriggerInteraction.Ignore))
            if(c.GetComponentInParent<OldPickupTruck>()==truck){TryTrigger(null,true);return;}
    }
    void OnDestroy(){foreach(var m in materials)if(m!=null)Destroy(m);foreach(var m in meshes)if(m!=null)Destroy(m);}
}
