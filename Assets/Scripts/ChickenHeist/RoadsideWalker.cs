using System.Collections.Generic;
using UnityEngine;

// Residents stroll along the road shoulders on errands of their own: each picks a place a good walk away within its
// neighbourhood (never one it has just been to), walks there along the pedestrian graph, lingers a while looking at
// whatever is there (a farm across the fence, the road) and sets off somewhere else. On the way the body sways a
// little off the line and the pace drifts, so nobody marches straight back and forth on one stretch. `route` is only
// the spawn hint authored by VillagerPopulation.
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
    float waitUntil,blockedTime,vertical,pace=1,lookYaw,seed;bool looking;
    readonly List<int> path=new List<int>();int step;
    readonly Queue<int> recent=new Queue<int>();

    static readonly List<RoadsideWalker> all=new List<RoadsideWalker>();
    void Awake(){controller=GetComponent<CharacterController>();}
    void OnEnable(){all.Add(this);}
    void OnDisable(){all.Remove(this);}
    void Start()
    {
        home=transform.position;
        var net=WalkNetwork.Get(this);
        current=net.Nearest(transform.position);target=current;
        pace=Random.Range(.85f,1.15f);seed=Random.Range(0,100f);
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
            if(target==current && !NextStep(net)){Plan(net);return;}
            // the path is the road's shoulder line shifted sideways by a slowly wandering amount (up to about two metres
            // either way), so nobody walks the same straight line
            Vector3 goal=Wander(net,target);
            Vector3 delta=goal-before;delta.y=0;
            if(delta.magnitude<1.0f){previous=current;current=target;if(!NextStep(net))Linger();}
            else
            {
                // a slow sideways drift off the shoulder line, and a pace that wanders a little
                Vector3 across=Vector3.Cross(Vector3.up,delta.normalized);
                float sway=(Mathf.PerlinNoise(seed,Time.time*.11f)-.5f)*.5f;
                Vector3 aim=delta+across*sway;
                transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.LookRotation(aim),Time.deltaTime*3);
                // Walk where the body faces, so corners are rounded instead of snapped.
                Vector3 heading=Vector3.Slerp(transform.forward,aim.normalized,.5f);heading.y=0;
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
                float drift=.88f+.24f*Mathf.PerlinNoise(seed+17,Time.time*.05f);
                controller.Move(heading.normalized*speed*pace*drift*Time.deltaTime);
            }
        }
        if(controller.isGrounded && vertical<0)vertical=-2;
        vertical+=Physics.gravity.y*Time.deltaTime;
        controller.Move(Vector3.up*vertical*Time.deltaTime);
        Vector3 travelled=transform.position-before;travelled.y=0;
        DistanceWalked+=travelled.magnitude;IsWalking=travelled.magnitude>.1f*Time.deltaTime;
        if(!giveWay && Time.time>=waitUntil && !IsWalking)blockedTime+=Time.deltaTime;else blockedTime=0;
        // blocked for good: give this errand up and pick another from here
        if(blockedTime>1.5f){blockedTime=0;Reversals++;target=current;path.Clear();step=0;Pause(1,2.5f);}
    }
    void OnControllerColliderHit(ControllerColliderHit hit){if(hit.normal.y<.6f)LastObstacle=hit.collider.name+" @"+hit.point.ToString("0");}
    // The node shifted across the road by the wander amount, pulled back towards the line where the ground is not
    // free (a fence, a tree, a wall at the side).
    Vector3 Wander(WalkNetwork net,int node)
    {
        if(wanderNode==node)return wanderPoint;
        wanderNode=node;
        Vector3 from=net.nodes[current],to=net.nodes[node];Vector3 dir=to-from;dir.y=0;
        if(dir.sqrMagnitude<.01f){wanderPoint=to;return to;}
        Vector3 across=Vector3.Cross(Vector3.up,dir.normalized);
        float offset=(Mathf.PerlinNoise(seed+41,DistanceWalked*.035f)-.5f)*4.4f;
        for(int k=0;k<4;k++,offset*=.5f)
        {
            Vector3 p=to+across*offset;
            if(Physics.Raycast(p+Vector3.up*2,Vector3.down,out var hit,5,~0,QueryTriggerInteraction.Ignore) && hit.normal.y>.7f && hit.collider.GetComponentInParent<CharacterController>()==null
                && !Physics.CheckCapsule(hit.point+Vector3.up*.5f,hit.point+Vector3.up*1.4f,.3f,~0,QueryTriggerInteraction.Ignore))
            {wanderPoint=hit.point;return wanderPoint;}
        }
        wanderPoint=to;return to;
    }
    int wanderNode=-1;Vector3 wanderPoint;
    bool NextStep(WalkNetwork net)
    {
        if(step>=path.Count)return false;
        target=path[step++];
        // now and then someone stops on the way to look around
        if(Random.value<.05f){Pause(2,5,true);return true;}
        // the odd pause at a junction, to look up and down the road
        if(net.links[current].Count>2 && Random.value<.15f)Pause(1,2.5f,true);
        return true;
    }
    // A new errand: somewhere 30 to 240 m away along the paths, inside the neighbourhood, not near one of the last few
    // places, and preferably not straight back the way it came.
    void Plan(WalkNetwork net)
    {
        var parent=new Dictionary<int,int>{{current,-1}};var hops=new Dictionary<int,int>{{current,0}};var queue=new Queue<int>();queue.Enqueue(current);
        while(queue.Count>0)
        {
            int n=queue.Dequeue();if(hops[n]>=40)continue;
            foreach(int m in net.links[n])
            {
                if(parent.ContainsKey(m) || (net.nodes[m]-home).sqrMagnitude>roamRadius*roamRadius)continue;
                parent[m]=n;hops[m]=hops[n]+1;queue.Enqueue(m);
            }
        }
        var choices=new List<int>();var fallback=new List<int>();
        foreach(var kv in hops)
        {
            if(kv.Value<5)continue;
            bool fresh=true;foreach(int r in recent)if((net.nodes[r]-net.nodes[kv.Key]).sqrMagnitude<20*20){fresh=false;break;}
            if(!fresh)continue;
            int first=kv.Key;while(parent[first]!=current)first=parent[first];
            (first==previous?fallback:choices).Add(kv.Key);
        }
        if(choices.Count==0)choices=fallback;
        if(choices.Count==0){Pause(2,5,true);recent.Clear();return;}
        int goal=choices[Random.Range(0,choices.Count)];
        path.Clear();for(int n=goal;n!=current;n=parent[n])path.Add(n);path.Reverse();step=0;
        recent.Enqueue(goal);while(recent.Count>3)recent.Dequeue();
        NextStep(net);
    }
    // At the end of an errand: stand a while, facing the nearest farm if one is close, else somewhere along the road.
    void Linger()
    {
        Pause(3,9,true);
        FarmLayoutInfo nearest=null;float best=40*40;
        foreach(var farm in Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None))
        {var c=new Vector3(farm.lot.center.x,transform.position.y,farm.lot.center.y);float d=(c-transform.position).sqrMagnitude;if(d<best){best=d;nearest=farm;}}
        if(nearest!=null){var to=new Vector3(nearest.lot.center.x-transform.position.x,0,nearest.lot.center.y-transform.position.z);if(to.sqrMagnitude>.01f)lookYaw=Quaternion.LookRotation(to).eulerAngles.y+Random.Range(-25f,25f);}
        target=current;path.Clear();step=0;
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
