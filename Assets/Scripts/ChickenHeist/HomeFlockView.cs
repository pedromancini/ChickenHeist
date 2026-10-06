using System.Collections.Generic;
using UnityEngine;

public class HomeFlockView : MonoBehaviour
{
    public GameObject chickenPrefab;
    public Vector3 DeliveryPoint=>transform.TransformPoint(new Vector3(-1,0,-2.7f));
    // Coop layout (local to the coop root, see PlayerHomeBuilder.WornCoop): wire run 6 x 4.4 m, gate in the front
    // fence between x -1.5 and -0.4; birds keep clear of the trough, the bucket and the fence.
    static readonly Rect Run=new Rect(-2.7f,-1.95f,5.4f,3.7f);
    static readonly Rect[] Obstacles={new Rect(1.3f,-1.1f,1.3f,.6f),new Rect(-2.5f,.15f,.5f,.5f)};
    public Vector3 GateInside=>transform.TransformPoint(new Vector3(-.95f,0,-1.55f));
    public Vector3 GateOutside=>transform.TransformPoint(new Vector3(-.95f,0,-2.75f));
    readonly List<GameObject> birds=new List<GameObject>();
    readonly Dictionary<Transform,(Vector3 target,float wait)> wander=new Dictionary<Transform,(Vector3,float)>();
    readonly HashSet<Transform> arriving=new HashSet<Transform>();
    int visible=-1;
    bool delivering;Vector3 deliveryOrigin;

    // Birds go in at the gate (any distance up to 2.5 m from the entrance) or straight from inside the run.
    public bool InsideRun(Vector3 world){var p=transform.InverseTransformPoint(world);return p.x>-3 && p.x<3 && p.z>-2.2f && p.z<2.2f;}
    public bool CanReceive(Vector3 playerPosition)=>Vector3.Distance(playerPosition,DeliveryPoint)<=2.5f || InsideRun(playerPosition);
    public void AnimateDelivery(Vector3 origin)
    {
        delivering=true;deliveryOrigin=origin;
        if(!InsideRun(origin))HomeCoopGate.Instance?.OpenForDelivery(3.2f);
    }
    System.Collections.IEnumerator Transfer(Transform bird,Vector3 target,float delay)
    {
        arriving.Add(bird);
        bird.position=deliveryOrigin;
        yield return new WaitForSeconds(delay+(InsideRun(deliveryOrigin)?0:.45f));
        if(bird==null)yield break;
        // From the hands (or the truck) down to the gate, through it, then into the run.
        var path=new List<Vector3>{bird.position};
        if(!InsideRun(deliveryOrigin)){path.Add(GateOutside);path.Add(GateInside);}
        path.Add(target);
        for(int leg=1;leg<path.Count;leg++)
        {
            Vector3 start=path[leg-1],end=path[leg];float duration=Mathf.Clamp(Vector3.Distance(start,end)/1.6f,.25f,1.1f);
            Face(bird,end-start);
            for(float t=0;t<1;t+=Time.deltaTime/duration)
            {
                if(bird==null)yield break;
                bird.position=Vector3.Lerp(start,end,Mathf.SmoothStep(0,1,t))+Vector3.up*Mathf.Sin(t*Mathf.PI)*(leg==1?.18f:.06f);
                yield return null;
            }
        }
        if(bird==null)yield break;
        bird.position=target;arriving.Remove(bird);wander[bird]=(target,Random.Range(.5f,2.5f));
    }
    static void Face(Transform bird,Vector3 direction){direction.y=0;if(direction.sqrMagnitude>.0004f)bird.rotation=Quaternion.LookRotation(direction);}
    Vector3 RandomSpot()
    {
        for(int attempt=0;attempt<20;attempt++)
        {
            var p=new Vector2(Random.Range(Run.xMin,Run.xMax),Random.Range(Run.yMin,Run.yMax));bool blocked=false;
            foreach(var o in Obstacles)if(o.Contains(p))blocked=true;
            if(!blocked)return transform.TransformPoint(new Vector3(p.x,0,p.y));
        }
        return transform.TransformPoint(new Vector3(0,0,-.8f));
    }
    void Wander()
    {
        foreach(var bird in birds)
        {
            if(bird==null || arriving.Contains(bird.transform))continue;
            var t=bird.transform;
            if(!wander.TryGetValue(t,out var state))state=(t.position,Random.Range(.5f,3f));
            Vector3 flat=new Vector3(state.target.x-t.position.x,0,state.target.z-t.position.z);
            if(flat.magnitude<.05f)
            {
                state.wait-=Time.deltaTime;
                if(state.wait<=0){var next=RandomSpot();next.y=t.position.y;state=(next,Random.Range(1.5f,5f));
                    if(Vector3.Distance(next,t.position)>2.2f)state.target=Vector3.Lerp(t.position,next,2.2f/Vector3.Distance(next,t.position));}
            }
            else
            {
                Vector3 step=Vector3.ClampMagnitude(flat,Time.deltaTime*.34f);
                t.position+=step;
                t.rotation=Quaternion.Slerp(t.rotation,Quaternion.LookRotation(flat),Time.deltaTime*6);
            }
            wander[t]=state;
        }
    }
    void Update()
    {
        if(GameMenu.BlocksInput)return;
        Wander();
        int desired=Mathf.Min(12,HouseholdEconomy.Instance?.Account.flock??0);
        if(desired==visible || chickenPrefab==null)return;
        while(birds.Count>desired){var last=birds[birds.Count-1];if(last!=null){wander.Remove(last.transform);arriving.Remove(last.transform);Destroy(last);}birds.RemoveAt(birds.Count-1);}
        int previous=birds.Count;visible=desired;
        for(int i=previous;i<desired;i++)
        {
            Vector3 position=i<8?new Vector3(-2.1f+(i%4)*1.38f,.03f,-1.5f+(i/4)*.78f):new Vector3(i%2==0?-2.2f:2.2f,.03f,.5f+(i-8)/2*.82f);
            var bird=Instantiate(chickenPrefab,transform);bird.name="Galinha do sitio "+(i+1);
            Bounds bounds=ProceduralFarmGenerator.VisualBounds(bird);bird.transform.localScale*=.44f/Mathf.Max(.01f,bounds.size.y);
            bird.transform.localRotation=Quaternion.Euler(0,i*73,0);bounds=ProceduralFarmGenerator.VisualBounds(bird);
            Vector3 target=transform.TransformPoint(position);bird.transform.position+=new Vector3(target.x-bounds.center.x,target.y-bounds.min.y,target.z-bounds.center.z);
            foreach(var collider in bird.GetComponentsInChildren<Collider>())Destroy(collider);
            if(bird.GetComponent<FarmAnimalMotion>()==null)bird.AddComponent<FarmAnimalMotion>();
            birds.Add(bird);
            if(delivering)StartCoroutine(Transfer(bird.transform,bird.transform.position,(i-previous)*.18f));
        }
        delivering=false;
    }
}
