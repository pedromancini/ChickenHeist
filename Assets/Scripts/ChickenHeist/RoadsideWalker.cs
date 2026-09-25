using System.Collections.Generic;
using UnityEngine;

// Residents stroll along the road shoulders: they pick a new branch at every junction,
// never turn straight back unless the road ends, pause now and then to look around and
// stay within a comfortable distance of where they live. `route` is only the spawn hint
// authored by VillagerPopulation.
[RequireComponent(typeof(CharacterController))]
public class RoadsideWalker : MonoBehaviour
{
    public Vector3[] route;
    public float speed=1.35f;
    public float roamRadius=180;
    public bool IsWalking {get;private set;}
    public float DistanceWalked {get;private set;}
    public int Reversals {get;private set;}
    public string LastObstacle {get;private set;}="";
    CharacterController controller;
    Vector3 home;int current=-1,previous=-1,target=-1;
    float waitUntil,blockedTime,vertical,pace=1,lookYaw;bool looking;

    static readonly List<RoadsideWalker> all=new List<RoadsideWalker>();
    void Awake(){controller=GetComponent<CharacterController>();}
    void OnEnable(){all.Add(this);}
    void OnDisable(){all.Remove(this);}
    void Start()
    {
        home=transform.position;
        var net=WalkNetwork.Get(this);
        current=net.Nearest(transform.position);target=current;
        pace=Random.Range(.85f,1.15f);
        waitUntil=Time.time+Random.Range(0,4f);
    }
    void Update()
    {
        IsWalking=false;
        var net=WalkNetwork.Shared;if(net==null || current<0)return;
        Vector3 before=transform.position;
        var player=HeistGameManager.Instance?.player;
        // Only stop when practically touching the player; otherwise walk around them.
        bool giveWay=player!=null && (player.position-before).sqrMagnitude<.8f*.8f;
        if(Time.time<waitUntil || giveWay)
        {
            if(looking)transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.Euler(0,lookYaw,0),Time.deltaTime*1.5f);
        }
        else
        {
            looking=false;
            Vector3 delta=net.nodes[target]-before;delta.y=0;
            if(delta.magnitude<.5f)Arrive(net);
            else
            {
                transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(delta),Time.deltaTime*4);
                // Walk where the body faces, so corners are rounded instead of snapped.
                Vector3 heading=Vector3.Slerp(transform.forward,delta.normalized,.5f);heading.y=0;
                // Oncoming pedestrians keep to their right instead of bumping and turning back.
                foreach(var other in all)
                {
                    if(other==this)continue;Vector3 gap=other.transform.position-before;gap.y=0;
                    if(gap.sqrMagnitude<2.6f*2.6f && Vector3.Dot(gap,heading)>0)heading+=transform.right*.9f*(1-gap.magnitude/2.6f);
                }
                if(player!=null)
                {
                    Vector3 gap=player.position-before;gap.y=0;
                    if(gap.sqrMagnitude<2.4f*2.4f && Vector3.Dot(gap,heading)>0)heading+=Vector3.Cross(Vector3.up,heading).normalized*1.1f*(1-gap.magnitude/2.4f);
                }
                controller.Move(heading.normalized*speed*pace*Time.deltaTime);
            }
        }
        if(controller.isGrounded && vertical<0)vertical=-2;
        vertical+=Physics.gravity.y*Time.deltaTime;
        controller.Move(Vector3.up*vertical*Time.deltaTime);
        Vector3 travelled=transform.position-before;travelled.y=0;
        DistanceWalked+=travelled.magnitude;IsWalking=travelled.magnitude>.1f*Time.deltaTime;
        if(!giveWay && Time.time>=waitUntil && !IsWalking)blockedTime+=Time.deltaTime;else blockedTime=0;
        if(blockedTime>1.5f){blockedTime=0;Reversals++;int back=target;target=previous>=0?previous:current;previous=back;Pause(1,2.5f);}
    }
    void OnControllerColliderHit(ControllerColliderHit hit){if(hit.normal.y<.6f)LastObstacle=hit.collider.name+" @"+hit.point.ToString("0");}
    void Arrive(WalkNetwork net)
    {
        previous=current;current=target;
        var options=new List<int>();
        foreach(int n in net.links[current])if(n!=previous && (net.nodes[n]-home).sqrMagnitude<roamRadius*roamRadius)options.Add(n);
        if(options.Count==0)
        {
            // Dead end or edge of the neighbourhood: stop, look around, head back.
            target=previous>=0?previous:current;Pause(2,5,true);return;
        }
        // Prefer continuing roughly straight; junctions are where people change their mind.
        Vector3 heading=net.nodes[current]-net.nodes[previous>=0?previous:current];
        options.Sort((a,b)=>Vector3.Dot(net.nodes[b]-net.nodes[current],heading).CompareTo(Vector3.Dot(net.nodes[a]-net.nodes[current],heading)));
        bool junction=net.links[current].Count>2;
        target=junction?options[Random.Range(0,options.Count)]:options[0];
        if(junction && Random.value<.35f)Pause(1.5f,4,true);
        else if(Random.value<.04f)Pause(3,7,true);
    }
    void Pause(float min,float max,bool look=false)
    {
        waitUntil=Time.time+Random.Range(min,max);looking=look;
        lookYaw=transform.eulerAngles.y+Random.Range(-110f,110f);
    }
}

// Pedestrian graph over both shoulders of the roads near residents, built once and shared.
public class WalkNetwork
{
    public static WalkNetwork Shared {get;private set;}
    public readonly List<Vector3> nodes=new List<Vector3>();
    public readonly List<List<int>> links=new List<List<int>>();
    const float Spacing=6,Reach=260;
    Collider[] surfaces;
    public static WalkNetwork Get(RoadsideWalker any)
    {
        if(Shared!=null)return Shared;
        Shared=new WalkNetwork();Shared.Build(Object.FindObjectsByType<RoadsideWalker>(FindObjectsSortMode.None));
        return Shared;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){Shared=null;}
    void Build(RoadsideWalker[] walkers)
    {
        surfaces=RuralTreeRoots.TerrainSurfaces();var terrain=new HashSet<Collider>(surfaces);
        foreach(var road in Object.FindObjectsByType<RuralRoadSpan>(FindObjectsSortMode.None))
        {
            if(road.width<2.5f || road.GetComponentInParent<FarmLayoutInfo>()!=null)continue;
            Vector3 d=road.end-road.start;float length=d.magnitude;if(length<1)continue;
            bool near=false;foreach(var w in walkers)if(DistanceToSegment(w.transform.position,road.start,road.end)<Reach){near=true;break;}
            if(!near)continue;
            Vector3 side=Vector3.Cross(Vector3.up,d/length)*(road.width*.5f-.55f);
            int count=Mathf.Max(1,Mathf.CeilToInt(length/Spacing));
            foreach(float s in new[]{-1f,1f})
            {
                int last=-1;
                for(int i=0;i<=count;i++)
                {
                    // Slide towards the crown of the road when a tree or fence sits on the shoulder.
                    Vector3 axis=road.start+d*i/count;int node=-1;
                    foreach(float inset in new[]{1f,.6f,.25f,0f})if(Clear(axis+side*s*inset,terrain)){node=Add(axis+side*s*inset);break;}
                    if(node<0)continue;
                    if(last>=0 && (nodes[node]-nodes[last]).magnitude<Spacing*2.2f)Link(last,node);last=node;
                }
            }
        }
        // Junctions and crossings: join shoulders that meet within a few metres when the gap is walkable.
        for(int i=0;i<nodes.Count;i++)for(int j=i+1;j<nodes.Count;j++)
        {
            Vector3 a=nodes[i],b=nodes[j];a.y=b.y=0;float gap=(a-b).magnitude;
            if(gap<4.5f && !links[i].Contains(j) && Clear((nodes[i]+nodes[j])*.5f,terrain))Link(i,j);
        }
    }
    int Add(Vector3 p){nodes.Add(p);links.Add(new List<int>());return nodes.Count-1;}
    void Link(int a,int b){if(a==b)return;if(!links[a].Contains(b))links[a].Add(b);if(!links[b].Contains(a))links[b].Add(a);}
    bool Clear(Vector3 p,HashSet<Collider> terrain)
    {
        if(!RuralTreeRoots.SurfaceHeight(surfaces,p,out float y))return false;
        p.y=y;
        foreach(var hit in Physics.OverlapCapsule(p+Vector3.up*.45f,p+Vector3.up*1.5f,.35f,~0,QueryTriggerInteraction.Ignore))
            if(!terrain.Contains(hit) && hit.GetComponentInParent<RoadsideWalker>()==null && hit.GetComponentInParent<CharacterController>()==null)return false;
        return true;
    }
    int[] component;int[] componentSize;
    // Nearest node on a stretch big enough to stroll along; tiny islands left by obstacles are skipped.
    public int Nearest(Vector3 p)
    {
        if(component==null)Components();
        int best=-1;float bestDistance=float.MaxValue;
        for(int i=0;i<nodes.Count;i++){float d=(nodes[i]-p).sqrMagnitude;if(d<bestDistance && componentSize[component[i]]>=25){bestDistance=d;best=i;}}
        return best;
    }
    void Components()
    {
        component=new int[nodes.Count];for(int i=0;i<component.Length;i++)component[i]=-1;var sizes=new List<int>();
        for(int i=0;i<nodes.Count;i++)
        {
            if(component[i]>=0)continue;int id=sizes.Count,size=0;var stack=new Stack<int>();stack.Push(i);component[i]=id;
            while(stack.Count>0){int n=stack.Pop();size++;foreach(int m in links[n])if(component[m]<0){component[m]=id;stack.Push(m);}}
            sizes.Add(size);
        }
        componentSize=sizes.ToArray();
    }
    static float DistanceToSegment(Vector3 p,Vector3 a,Vector3 b){p.y=a.y=b.y=0;Vector3 d=b-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(1e-4f,d.sqrMagnitude));return Vector3.Distance(p,a+d*t);}
}
