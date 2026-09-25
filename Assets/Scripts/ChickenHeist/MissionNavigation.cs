using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Uses the same authored road spans that paint the terrain. Every connection
// is checked against the live collision world, including gates and fences.
public class MissionNavigation : MonoBehaviour
{
    public static MissionNavigation Instance {get;private set;}
    public readonly List<Vector3> Route=new List<Vector3>();
    public string Status {get;private set;}="";
    public Vector3 Destination {get;private set;}
    public string Diagnostic {get;private set;}="";
    public string LastObstacle {get;private set;}="";
    readonly List<Vector3> nodes=new List<Vector3>();
    readonly List<List<int>> links=new List<List<int>>();
    RuralRoadSpan[] roads;
    FarmLayoutInfo[] farms;
    readonly HashSet<Collider> terrain=new HashSet<Collider>();
    readonly RaycastHit[] groundHits=new RaycastHit[64];
    readonly RaycastHit[] passageHits=new RaycastHit[64];
    readonly Collider[] overlapHits=new Collider[64];
    int mission=-1;
    bool driving;
    public bool ReturningHome {get;private set;}
    public void ToggleDestination(){ReturningHome=!ReturningHome;Route.Clear();Refresh();}
    Vector3 lastStart;
    float nextRefresh;
    Coroutine search;
    HeistGameManager Game=>HeistGameManager.Instance;
    void Awake(){Instance=this;}
    void Start(){BuildNetwork();}
    void OnDestroy(){if(Instance==this)Instance=null;}
    int Node(Vector3 p)
    {
        p.y=0;
        for(int i=0;i<nodes.Count;i++)if(new Vector2(nodes[i].x-p.x,nodes[i].z-p.z).sqrMagnitude<.01f)return i;
        nodes.Add(GroundPoint(p));links.Add(new List<int>());return nodes.Count-1;
    }
    void Link(int a,int b){if(a==b)return;if(!links[a].Contains(b))links[a].Add(b);if(!links[b].Contains(a))links[b].Add(a);}
    public void BuildNetwork()
    {
        roads=FindObjectsByType<RuralRoadSpan>();farms=FindObjectsByType<FarmLayoutInfo>();nodes.Clear();links.Clear();
        terrain.Clear();foreach(var surface in RuralTreeRoots.TerrainSurfaces())terrain.Add(surface);
        foreach(var road in roads)
        {
            var points=new List<float>{0,1};Vector3 d=road.end-road.start;
            int count=Mathf.CeilToInt(d.magnitude/5);
            for(int i=1;i<count;i++)points.Add((float)i/count);
            foreach(var other in roads)
            {
                if(other==road)continue;Vector3 e=other.end-other.start,q=other.start-road.start;
                float cross=d.x*e.z-d.z*e.x;
                if(Mathf.Abs(cross)>.0001f)
                {
                    float t=(q.x*e.z-q.z*e.x)/cross,u=(q.x*d.z-q.z*d.x)/cross;
                    if(t>=0 && t<=1 && u>=0 && u<=1)points.Add(t);
                }
                foreach(var p in new[]{other.start,other.end})
                {
                    float t=Mathf.Clamp01(Vector3.Dot(p-road.start,d)/d.sqrMagnitude);
                    if(Vector3.Distance(p,road.start+d*t)<.15f)points.Add(t);
                }
            }
            points.Sort();var previous=new[]{-1,-1,-1};
            Vector3 side=Vector3.Cross(Vector3.up,d.normalized)*Mathf.Min(road.width*.48f,2.5f);
            foreach(float t in points)
            {
                var current=new int[3];
                for(int lane=0;lane<3;lane++)
                {current[lane]=Node(road.start+d*t+side*(lane-1));if(previous[lane]>=0)Link(previous[lane],current[lane]);}
                Link(current[0],current[1]);Link(current[1],current[2]);previous=current;
            }
        }
        // Small gaps between road ends and farm access lanes are genuine junctions
        // only if collision clearance succeeds during route calculation.
        for(int i=0;i<nodes.Count;i++)for(int j=i+1;j<nodes.Count;j++)
            if((nodes[i]-nodes[j]).sqrMagnitude<36)Link(i,j);
    }
    void Update()
    {
        if(Game==null || Game.player==null)return;
        if(!Game.MissionActive || Game.missionEnded){Clear();return;}
        if(GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen)return;
        if(Input.GetKeyDown(KeyCode.N)){ToggleDestination();return;}
        if(search==null && Route.Count>1)Status=(driving?"De carro | ":"A pe | ")+Mathf.RoundToInt(RemainingDistance(Game.player.position))+" m";
        if(mission==Game.MissionFarm && Vector3.Distance(Game.player.position,Destination)<4){if(search!=null){StopCoroutine(search);search=null;}Route.Clear();Status=ReturningHome?"Acesso ao sitio | G perto do galinheiro":"Entrada da fazenda";return;}
        bool changed=mission!=Game.MissionFarm || driving!=OldPickupTruck.IsDriving;
        if(changed)Route.Clear();
        if(search!=null && !changed)return;
        if(changed || Time.time>=nextRefresh && (Route.Count<2 || DistanceToRoute(Game.player.position)>(driving?12:7)))Refresh();
    }
    public float RemainingDistance(Vector3 point)
    {
        float nearest=float.PositiveInfinity,remaining=0,tail=0;
        for(int i=Route.Count-1;i>0;i--){Vector3 a=Route[i-1],b=Route[i],d=b-a;float t=d.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(point-a,d)/d.sqrMagnitude):0;
            float distance=Vector3.Distance(point,a+d*t);if(distance<nearest){nearest=distance;remaining=tail+Vector3.Distance(a+d*t,b);}tail+=d.magnitude;}
        return remaining;
    }
    public float DistanceToRoute(Vector3 point)
    {
        float nearest=float.PositiveInfinity;point.y=0;
        for(int i=1;i<Route.Count;i++)
        {
            Vector3 a=Route[i-1],b=Route[i];a.y=b.y=0;var d=b-a;
            float t=d.sqrMagnitude>0?Mathf.Clamp01(Vector3.Dot(point-a,d)/d.sqrMagnitude):0;
            nearest=Mathf.Min(nearest,Vector3.Distance(point,a+d*t));
        }
        return nearest;
    }
    public void Clear()
    {
        if(search!=null){StopCoroutine(search);search=null;}mission=-1;ReturningHome=false;Route.Clear();Status="";
    }
    public void Refresh()
    {
        if(Game==null || !Game.MissionActive){Clear();return;}
        if(roads==null)BuildNetwork();
        if(search!=null)StopCoroutine(search);
        mission=Game.MissionFarm;driving=OldPickupTruck.IsDriving;lastStart=GroundPoint(Game.player.position);
        nextRefresh=Time.time+5;Status="Calculando caminho...";
        FarmLayoutInfo target=null;foreach(var farm in farms)if(farm.identity==Game.MissionName){target=farm;break;}
        if(target==null){Status="Destino indisponivel";return;}
        Destination=GroundPoint(ReturningHome && HouseholdEconomy.Instance?.home!=null?HouseholdEconomy.Instance.home.TransformPoint(new Vector3(-3,0,-15)):target.entrance+Vector3.back*3);
        if(Vector3.Distance(lastStart,Destination)<4){Status=ReturningHome?"Acesso ao sitio | G perto do galinheiro":"Entrada da fazenda";search=null;return;}
        search=StartCoroutine(CalculateRoute(lastStart,Destination,driving));
    }
    public bool ClearPassage(Vector3 a,Vector3 b,bool vehicle)
    {
        int steps=Mathf.CeilToInt(Vector3.Distance(a,b));var previous=a;
        for(int i=1;i<=steps;i++)
        {
            var point=GroundPoint(Vector3.Lerp(a,b,(float)i/steps));
            if(point.y-previous.y>(vehicle?.5f:.35f) || previous.y-point.y>(vehicle?.5f:1f)){LastObstacle="Desnivel sem passagem";return false;}
            if(!ClearSegment(previous,point,vehicle))return false;previous=point;
        }
        return true;
    }
    Vector3 GroundPoint(Vector3 p)
    {
        int count=Physics.RaycastNonAlloc(p+Vector3.up*2,Vector3.down,groundHits,5,~0,QueryTriggerInteraction.Ignore);
        float nearest=float.PositiveInfinity;Vector3 result=p;
        for(int i=0;i<count;i++)
        {
            var hit=groundHits[i];
            if(Game?.player!=null && hit.transform.IsChildOf(Game.player) || hit.transform.GetComponentInParent<OldPickupTruck>()!=null)continue;
            if(hit.transform.GetComponentInParent<FarmerSleepSystem>()!=null || hit.transform.GetComponentInParent<InteractableChicken>()!=null || hit.transform.GetComponentInParent<SimpleAnimalWander>()!=null)continue;
            if(hit.point.y>p.y+.75f)continue;
            if(hit.normal.y<.65f || hit.distance>=nearest)continue;
            result.y=hit.point.y;nearest=hit.distance;
        }
        return result;
    }
    bool ClearSegment(Vector3 a,Vector3 b,bool vehicle)
    {
        LastObstacle="";
        var delta=b-a;if(delta.sqrMagnitude<.001f)return true;
        float radius=vehicle?.98f:.36f;
        Vector3 low=a+Vector3.up*(radius+.20f),high=a+Vector3.up*(vehicle?1.2f:1.65f);
        int hitCount=Physics.CapsuleCastNonAlloc(low,high,radius,delta.normalized,passageHits,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
        if(hitCount==passageHits.Length){LastObstacle="Passagem congestionada";return false;}
        for(int i=0;i<hitCount;i++)
        {
            var c=passageHits[i].collider;
            if(terrain.Contains(c))continue; // Elevation is checked along the path above.
            if(c.bounds.max.y<Mathf.Min(a.y,b.y)+.18f)continue;
            if(Game?.player!=null && c.transform.IsChildOf(Game.player))continue;
            if(c.GetComponentInParent<OldPickupTruck>()!=null)continue;
            if(c.GetComponentInParent<SimpleAnimalWander>()!=null || c.GetComponentInParent<InteractableChicken>()!=null || c.GetComponentInParent<FarmerSleepSystem>()!=null)continue;
            LastObstacle=c.name;return false;
        }
        // Capsule casts don't report initial overlaps.
        int overlapCount=Physics.OverlapCapsuleNonAlloc(low,high,radius,overlapHits,~0,QueryTriggerInteraction.Ignore);
        if(overlapCount==overlapHits.Length){LastObstacle="Passagem congestionada";return false;}
        for(int i=0;i<overlapCount;i++)
        {
            var c=overlapHits[i];
            if(terrain.Contains(c))continue;
            if(c.bounds.max.y<a.y+.18f || Game?.player!=null && c.transform.IsChildOf(Game.player) || c.GetComponentInParent<OldPickupTruck>()!=null)continue;
            if(c.GetComponentInParent<SimpleAnimalWander>()!=null || c.GetComponentInParent<InteractableChicken>()!=null || c.GetComponentInParent<FarmerSleepSystem>()!=null)continue;
            LastObstacle=c.name;return false;
        }
        return true;
    }
    public IEnumerator CalculateRoute(Vector3 start,Vector3 end,bool vehicle)
    {
        Status="Calculando caminho...";
        int n=nodes.Count;var costs=new float[n];var previous=new int[n];var done=new bool[n];var endLinks=new bool[n];
        var heap=new List<KeyValuePair<int,float>>();
        int starts=0,ends=0;var diagnostic=new System.Text.StringBuilder();
        for(int i=0;i<n;i++)
        {
            costs[i]=float.PositiveInfinity;previous[i]=-1;
            float ds=Vector3.Distance(start,nodes[i]),de=Vector3.Distance(end,nodes[i]);
            if(ds<=35){if(ClearPassage(start,nodes[i],vehicle)){costs[i]=ds;Push(heap,i,ds);starts++;}else if(diagnostic.Length<1500)diagnostic.AppendLine("Start to "+nodes[i]+" blocked by "+LastObstacle);}
            if(de<=15 && ClearPassage(nodes[i],end,vehicle)){endLinks[i]=true;ends++;}
            if(i%40==0)yield return null;
        }
        int best=-1;float bestCost=float.PositiveInfinity;int budget=0;
        while(heap.Count>0)
        {
            var item=Pop(heap);int current=item.Key;if(done[current])continue;
            if(item.Value>=bestCost)break;done[current]=true;
            float finish=item.Value+Vector3.Distance(nodes[current],end);
            if(endLinks[current] && finish<bestCost){best=current;bestCost=finish;}
            foreach(int adjacent in links[current])
            {
                float cost=costs[current]+Vector3.Distance(nodes[current],nodes[adjacent]);
                if(done[adjacent] || cost>=costs[adjacent])continue;
                if(!ClearPassage(nodes[current],nodes[adjacent],vehicle))
                {if(diagnostic.Length<9000)diagnostic.AppendLine("Edge "+nodes[current]+" to "+nodes[adjacent]+" blocked by "+LastObstacle);continue;}
                costs[adjacent]=cost;previous[adjacent]=current;Push(heap,adjacent,cost);
            }
            if(++budget%25==0)yield return null;
        }
        Route.Clear();
        Diagnostic="nodes="+n+" starts="+starts+" ends="+ends+" visited="+budget+" origin="+start+" destination="+end+"\n"+diagnostic;
        if(best<0){Status=vehicle?"Sem rota para carro. Procure uma via livre.":"Sem caminho livre ate a entrada.";search=null;yield break;}
        Route.Add(end);for(int i=best;i>=0;i=previous[i])Route.Add(nodes[i]);Route.Add(start);Route.Reverse();
        nextRefresh=Time.time+5;
        Status=(vehicle?"De carro · ":"A pe · ")+Mathf.RoundToInt(bestCost)+" m";search=null;
    }
    static void Push(List<KeyValuePair<int,float>> heap,int key,float value)
    {
        heap.Add(new KeyValuePair<int,float>(key,value));int i=heap.Count-1;
        while(i>0){int p=(i-1)/2;if(heap[p].Value<=value)break;heap[i]=heap[p];i=p;}heap[i]=new KeyValuePair<int,float>(key,value);
    }
    static KeyValuePair<int,float> Pop(List<KeyValuePair<int,float>> heap)
    {
        var result=heap[0];var last=heap[heap.Count-1];heap.RemoveAt(heap.Count-1);if(heap.Count==0)return result;
        int i=0;while(i*2+1<heap.Count){int c=i*2+1;if(c+1<heap.Count && heap[c+1].Value<heap[c].Value)c++;if(heap[c].Value>=last.Value)break;heap[i]=heap[c];i=c;}heap[i]=last;return result;
    }
    void OnGUI()
    {
        if(Game==null || !Game.MissionActive || Game.missionEnded || GameMenu.IsOpen || ChickenCoopLockpick.Active!=null || ChickenScare.Active!=null || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || roads==null)return;
        float size=Mathf.Min(230,Screen.width*.25f);Rect area=new Rect(Screen.width-size-24,Screen.height-size-76,size,size);
        GUI.Box(new Rect(area.x-5,area.y-27,size+10,size+70),"");
        GUI.Label(new Rect(area.x,area.y-25,size,24),ReturningHome?"N | VOLTAR A FAZENDA":"N | ROTA PARA O SITIO");
        mapSize=size;GUI.BeginGroup(area);Color previousColor=GUI.color;GUI.color=new Color(.06f,.10f,.10f,.94f);GUI.DrawTexture(new Rect(0,0,size,size),Texture2D.whiteTexture);GUI.color=previousColor;
        Vector3 center=Game.player.position;float scale=size/(driving?190f:120f);
        Vector2 Map(Vector3 p)=>new Vector2(size*.5f+(p.x-center.x)*scale,size*.5f-(p.z-center.z)*scale);
        foreach(var farm in farms)
        {
            var r=farm.lot;Vector2 a=Map(new Vector3(r.xMin,0,r.yMax)),b=Map(new Vector3(r.xMax,0,r.yMin));
            GUI.color=new Color(.25f,.34f,.28f,.6f);GUI.DrawTexture(new Rect(a.x,a.y,b.x-a.x,b.y-a.y),Texture2D.whiteTexture);
        }
        GUI.color=previousColor;
        foreach(var road in roads)Line(Map(road.start),Map(road.end),new Color(.55f,.51f,.40f),Mathf.Max(1,road.width*scale));
        for(int i=1;i<Route.Count;i++)Line(Map(Route[i-1]),Map(Route[i]),new Color(1,.73f,.22f),3);
        Vector2 marker=Map(Destination);GUI.color=new Color(1,.73f,.22f);GUI.DrawTexture(new Rect(marker.x-4,marker.y-4,8,8),Texture2D.whiteTexture);GUI.color=previousColor;
        Vector2 origin=new Vector2(size*.5f,size*.5f),forward=new Vector2(Game.player.forward.x,-Game.player.forward.z)*10;
        Line(origin-forward*.5f,origin+forward,Color.white,3);
        GUI.EndGroup();GUI.Label(new Rect(area.x,area.yMax+3,size,42),Status);
    }
    static float mapSize;
    static void Line(Vector2 a,Vector2 b,Color color,float width)
    {
        // Rasterize in the group's coordinates: rotating GUI.matrix also rotates its clip.
        var d=b-a;float lo=0,hi=1;
        bool Clip(float p,float q){if(Mathf.Abs(p)<.00001f)return q>=0;float t=q/p;if(p<0){if(t>hi)return false;lo=Mathf.Max(lo,t);}else{if(t<lo)return false;hi=Mathf.Min(hi,t);}return true;}
        if(!Clip(-d.x,a.x) || !Clip(d.x,mapSize-a.x) || !Clip(-d.y,a.y) || !Clip(d.y,mapSize-a.y))return;
        b=a+d*hi;a+=d*lo;float distance=Vector2.Distance(a,b);int steps=Mathf.Max(1,Mathf.CeilToInt(distance/2));
        var previous=GUI.color;GUI.color=color;width=Mathf.Clamp(width,2,20);
        for(int i=0;i<=steps;i++){var p=Vector2.Lerp(a,b,(float)i/steps);float x=Mathf.Max(0,p.x-width/2),y=Mathf.Max(0,p.y-width/2);GUI.DrawTexture(new Rect(x,y,Mathf.Min(mapSize,p.x+width/2)-x,Mathf.Min(mapSize,p.y+width/2)-y),Texture2D.whiteTexture);}
        GUI.color=previous;
    }
}
