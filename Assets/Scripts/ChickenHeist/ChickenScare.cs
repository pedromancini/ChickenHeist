using UnityEngine;

[DefaultExecutionOrder(300)]
public class ChickenScare : MonoBehaviour
{
    public static ChickenScare Active {get;private set;}
    public bool IsActive=>bird!=null;
    public float Elapsed=>elapsed;
    Transform bird;Vector3 originalPosition,originalScale;Quaternion originalRotation;
    Behaviour[] behaviours;bool[] enabledStates;Collider[] colliders;bool[] collisionStates;
    Camera eye;float oldNear,elapsed;Light fill;AudioSource voice;AudioClip cry;
    ChickenWingMotion wings;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatic(){Active=null;}
    public bool Begin(Transform target,Camera camera)
    {
        if(Active!=null || target==null || !target.gameObject.activeInHierarchy || camera==null)return false;
        bird=target;eye=camera;Active=this;elapsed=0;
        // Keep parent and sibling index intact: checkpoint IDs remain stable.
        originalPosition=bird.localPosition;originalRotation=bird.localRotation;originalScale=bird.localScale;
        behaviours=bird.GetComponentsInChildren<Behaviour>();enabledStates=new bool[behaviours.Length];
        for(int i=0;i<behaviours.Length;i++){enabledStates[i]=behaviours[i].enabled;behaviours[i].enabled=false;}
        colliders=bird.GetComponentsInChildren<Collider>();collisionStates=new bool[colliders.Length];
        for(int i=0;i<colliders.Length;i++){collisionStates[i]=colliders[i].enabled;colliders[i].enabled=false;}
        wings=bird.gameObject.AddComponent<ChickenWingMotion>();
        oldNear=eye.nearClipPlane;eye.nearClipPlane=.025f;
        fill=new GameObject("Luz breve do susto").AddComponent<Light>();fill.type=LightType.Point;fill.range=2;fill.intensity=.65f;fill.color=new Color(1,.82f,.64f);
        if(voice==null){var node=new GameObject("Grito da galinha");node.transform.SetParent(transform,false);voice=node.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;cry=CreateCry();}
        voice.PlayOneShot(cry,.45f);return true;
    }
    void LateUpdate()
    {
        if(bird==null){if(Active==this)Cancel();return;}
        if(eye==null || !bird.gameObject.activeInHierarchy || HeistGameManager.Instance?.missionEnded==true){Cancel();return;}
        if(GameMenu.BlocksInput)return;
        elapsed+=Time.deltaTime;
        float approach=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/.28f));
        float retreat=Mathf.SmoothStep(0,1,Mathf.Clamp01((elapsed-.64f)/.30f));
        float blend=approach*(1-retreat);
        Quaternion facing=Quaternion.LookRotation(-eye.transform.forward,eye.transform.up)*Quaternion.Euler(0,8,0);
        Vector3 faceLocal=new Vector3(0,.94f,.295f);
        // Fit the recognizable head to vertical FOV, while keeping the beak beyond the near plane.
        float distance=Mathf.Max(.145f,.12f/Mathf.Tan(eye.fieldOfView*.5f*Mathf.Deg2Rad));
        Vector3 target=eye.transform.position+eye.transform.forward*distance-facing*Vector3.Scale(faceLocal,bird.lossyScale);
        Vector3 start=bird.parent!=null?bird.parent.TransformPoint(originalPosition):originalPosition;
        bird.position=Vector3.Lerp(start,target,blend)+eye.transform.up*(Mathf.Sin(approach*Mathf.PI)*.22f*(1-retreat));
        Quaternion startRotation=bird.parent!=null?bird.parent.rotation*originalRotation:originalRotation;
        bird.rotation=Quaternion.Slerp(startRotation,facing,blend);
        wings.Pose(elapsed,blend);fill.transform.position=eye.transform.position+eye.transform.up*.18f;
        if(elapsed>=.94f)Cancel();
    }
    public void Cancel()
    {
        if(wings!=null){wings.Restore();Destroy(wings);wings=null;}
        if(bird!=null){bird.localPosition=originalPosition;bird.localRotation=originalRotation;bird.localScale=originalScale;}
        if(behaviours!=null)for(int i=0;i<behaviours.Length;i++)if(behaviours[i]!=null)behaviours[i].enabled=enabledStates[i];
        if(colliders!=null)for(int i=0;i<colliders.Length;i++)if(colliders[i]!=null)colliders[i].enabled=collisionStates[i];
        if(eye!=null)eye.nearClipPlane=oldNear;
        if(fill!=null)Destroy(fill.gameObject);if(voice!=null)voice.Stop();
        bird=null;eye=null;behaviours=null;colliders=null;
        if(Active==this)Active=null;
    }
    void OnDisable(){Cancel();}
    void OnDestroy(){Cancel();if(cry!=null)Destroy(cry);}
    // Original provisional vocal synthesis; final listening/mix pass is still required.
    static AudioClip CreateCry()
    {
        const int rate=22050;var samples=new float[(int)(rate*.7f)];var random=new System.Random(39);float phase=0;
        for(int i=0;i<samples.Length;i++){float t=(float)i/rate;phase+=2*Mathf.PI*(760-380*t+120*Mathf.Sin(t*36))/rate;
            float envelope=Mathf.Min(1,t*80)*Mathf.Exp(-t*4);samples[i]=envelope*(Mathf.Sin(phase)*.5f+Mathf.Sin(phase*2.03f)*.2f+((float)random.NextDouble()*2-1)*.12f);}
        var clip=AudioClip.Create("Provisional chicken cry",samples.Length,1,rate,false);clip.SetData(samples,0);return clip;
    }
}

// Articulates the existing wing region; source mesh and animal count are untouched.
public class ChickenWingMotion : MonoBehaviour
{
    MeshFilter filter;Mesh source,working;Vector3[] vertices,result;
    void Awake()
    {
        filter=GetComponentInChildren<MeshFilter>();if(filter==null)return;
        source=filter.sharedMesh;var readable=source.isReadable?source:Resources.Load<Mesh>("ScareChickenMesh");
        if(readable==null)return;
        working=Instantiate(readable);vertices=readable.vertices;result=new Vector3[vertices.Length];filter.sharedMesh=working;
    }
    public void Pose(float time,float strength)
    {
        if(working==null)return;
        float flap=(35+Mathf.Sin(time*55)*30)*strength;
        for(int i=0;i<vertices.Length;i++)
        {
            Vector3 p=transform.InverseTransformPoint(filter.transform.TransformPoint(vertices[i]));
            float weight=Mathf.InverseLerp(.14f,.23f,Mathf.Abs(p.x))*Mathf.Clamp01((p.y-.27f)/.12f)*Mathf.Clamp01((.73f-p.y)/.15f)*Mathf.Clamp01((.10f-p.z)/.15f)*Mathf.Clamp01((p.z+.48f)/.15f);
            float side=Mathf.Sign(p.x);Vector3 pivot=new Vector3(side*.13f,.57f,-.10f);
            Vector3 moved=Vector3.Lerp(p,pivot+Quaternion.AngleAxis(side*flap,Vector3.forward)*(p-pivot),weight);
            result[i]=filter.transform.InverseTransformPoint(transform.TransformPoint(moved));
        }
        working.vertices=result;working.RecalculateNormals();working.RecalculateBounds();
    }
    public void Restore(){if(filter!=null && source!=null)filter.sharedMesh=source;if(working!=null)Destroy(working);working=null;}
    void OnDestroy(){Restore();}
}
