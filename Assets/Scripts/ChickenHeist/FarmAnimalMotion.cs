using UnityEngine;

[DefaultExecutionOrder(250)]
public class FarmAnimalMotion : MonoBehaviour
{
    public bool cow,carried;
    public float Flap {get;set;}
    public float GaitPhase {get;private set;}
    public bool Pecking {get;private set;}
    MeshFilter filter;Mesh original,mesh;Vector3[] rest,posed;Bounds bounds;
    float[] legWeights,headWeights,wingWeights;int[] legGroups;bool[] leftSide;
    readonly Vector3[] legOffsets=new Vector3[4];Vector3 headPivot,leftHinge,rightHinge;
    Vector3 previous;float phase,idleClock,seed,flap;
    float gaitWeight;
    float nextPose;bool distantRest;float lastPose;
    InteractableChicken chicken;
    float nextGround,bottomOffset;readonly RaycastHit[] groundHits=new RaycastHit[24];
    void Start()
    {
        chicken=GetComponent<InteractableChicken>();filter=GetComponentInChildren<MeshFilter>();previous=transform.position;
        if(filter==null)return;original=filter.sharedMesh;
        var readable=Resources.Load<Mesh>("AnimalMotion/"+original.name.Replace("(Clone)",""));
        if(readable==null && original.isReadable)readable=original;
        if(readable==null)return;
        mesh=Instantiate(readable);mesh.MarkDynamic();filter.sharedMesh=mesh;rest=readable.vertices;posed=new Vector3[rest.Length];bounds=readable.bounds;
        legWeights=new float[rest.Length];headWeights=new float[rest.Length];wingWeights=new float[rest.Length];legGroups=new int[rest.Length];leftSide=new bool[rest.Length];
        headPivot=new Vector3(bounds.center.x,bounds.min.y+bounds.size.y*.60f,bounds.min.z+bounds.size.z*.66f);
        leftHinge=new Vector3(bounds.center.x-bounds.size.x*.24f,bounds.min.y+bounds.size.y*.55f,bounds.center.z);
        rightHinge=new Vector3(bounds.center.x+bounds.size.x*.24f,bounds.min.y+bounds.size.y*.55f,bounds.center.z);
        for(int i=0;i<rest.Length;i++)
        {
            var p=rest[i];var n=new Vector3((p.x-bounds.center.x)/bounds.size.x,(p.y-bounds.min.y)/bounds.size.y,(p.z-bounds.min.z)/bounds.size.z);
            leftSide[i]=n.x<0;legGroups[i]=(leftSide[i]?1:0)+(cow && n.z<.5f?2:0);
            legWeights[i]=Mathf.Clamp01(((cow?.53f:.30f)-n.y)/(cow?.18f:.12f));
            headWeights[i]=Mathf.Clamp01((n.y-.56f)/.2f)*Mathf.Clamp01((n.z-.55f)/.18f);
            wingWeights[i]=Mathf.Clamp01((Mathf.Abs(n.x)-.26f)/.18f)*Mathf.Clamp01((n.y-.26f)/.1f)*Mathf.Clamp01((.7f-n.y)/.16f)*Mathf.Clamp01((.77f-n.z)/.18f);
        }
        seed=Mathf.Abs(transform.position.x*.73f+transform.position.z*.37f)%19;phase=seed;idleClock=seed;
        bottomOffset=filter.GetComponent<Renderer>().bounds.min.y-transform.position.y;
        lastPose=Time.time;
    }
    void LateUpdate()
    {
        if(mesh==null)return;
        if(GameMenu.BlocksInput){previous=transform.position;return;}
        var viewer=Camera.main;float distance=viewer!=null?Vector3.SqrMagnitude(viewer.transform.position-transform.position):0;
        if(!carried && distance>80*80)
        {
            if(!distantRest){Pose(0,0,0);distantRest=true;}
            previous=transform.position;lastPose=Time.time;return;
        }
        distantRest=false;
        if(Time.time<nextPose)return;
        nextPose=Time.time+(distance>30*30?1f/15:1f/30);
        float delta=Mathf.Clamp(Time.time-lastPose,.001f,.15f);lastPose=Time.time;
        if(!carried && Time.time>=nextGround)
        {
            nextGround=Time.time+.25f;
            int count=Physics.RaycastNonAlloc(transform.position+Vector3.up*.4f,Vector3.down,groundHits,1.8f,~0,QueryTriggerInteraction.Ignore);
            float ground=float.NegativeInfinity;
            for(int i=0;i<count;i++)
            {
                var hit=groundHits[i];if(hit.transform.IsChildOf(transform) || hit.normal.y<.65f || hit.transform.GetComponentInParent<InteractableChicken>()!=null || hit.transform.GetComponentInParent<SimpleAnimalWander>()!=null)continue;
                if(hit.point.y<=transform.position.y+.2f)ground=Mathf.Max(ground,hit.point.y);
            }
            if(float.IsFinite(ground))transform.position=new Vector3(transform.position.x,ground-bottomOffset+.008f,transform.position.z);
        }
        float moved=Vector3.Distance(new Vector3(previous.x,0,previous.z),new Vector3(transform.position.x,0,transform.position.z));previous=transform.position;
        float speed=moved/delta;bool moving=!carried && speed>.035f && moved<1;
        phase+=moving?moved/(cow?1.1f:.34f)*Mathf.PI*2:0;GaitPhase=phase;
        idleClock+=delta;Pecking=!cow && !carried && !moving && chicken?.sleeping!=true && idleClock%7<1.3f;
        flap=Mathf.MoveTowards(flap,carried?1:Flap,delta*4);Flap=Mathf.MoveTowards(Flap,0,delta);
        gaitWeight=Mathf.MoveTowards(gaitWeight,moving?1:0,delta*5);
        Pose(gaitWeight,Pecking?Mathf.Sin(idleClock%7/1.3f*Mathf.PI):0,flap);
    }
    public void Pose(float walking,float peck,float wings)
    {
        if(mesh==null)return;
        for(int group=0;group<4;group++)
        {
            float legPhase=phase+((group&1)!=0?Mathf.PI:0)+(group>=2?Mathf.PI:0);
            // During stance the foot travels backward at body speed; lift belongs to the return stroke.
            float cycle=Mathf.Repeat(legPhase/(Mathf.PI*2),1);
            float stride=cycle<.6f?Mathf.Lerp(.5f,-.5f,cycle/.6f):Mathf.Lerp(-.5f,.5f,(cycle-.6f)/.4f);
            float lift=cycle<.6f?0:Mathf.Sin((cycle-.6f)/.4f*Mathf.PI);
            legOffsets[group]=new Vector3(0,lift*bounds.size.y*(cow?.065f:.085f)*walking,stride*bounds.size.z*(cow?.19f:.14f)*walking);
        }
        var headRotation=Quaternion.Euler(peck*62,0,0);
        float angle=(35+25*Mathf.Sin(Time.time*45+seed))*wings;
        var leftRotation=Quaternion.AngleAxis(-angle,Vector3.forward);var rightRotation=Quaternion.AngleAxis(angle,Vector3.forward);
        for(int i=0;i<rest.Length;i++)
        {
            Vector3 p=rest[i]+legOffsets[legGroups[i]]*legWeights[i];
            if(!cow)
            {
                if(peck!=0 && headWeights[i]>0)p=Vector3.Lerp(p,headPivot+headRotation*(p-headPivot),headWeights[i]);
                if(wings!=0 && wingWeights[i]>0){var hinge=leftSide[i]?leftHinge:rightHinge;var rotation=leftSide[i]?leftRotation:rightRotation;p=Vector3.Lerp(p,hinge+rotation*(p-hinge),wingWeights[i]);}
            }
            posed[i]=p;
        }
        mesh.vertices=posed;mesh.RecalculateNormals();mesh.RecalculateBounds();
    }
    void OnDestroy(){if(filter!=null && original!=null)filter.sharedMesh=original;if(mesh!=null)Destroy(mesh);}
}
