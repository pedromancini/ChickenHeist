using System.Linq;
using UnityEngine;

public sealed class DeclineCinematicStage : MonoBehaviour
{
    public Transform osvaldo,joana;
    Transform home;OpeningCinematicStage remembered;
    public static DeclineCinematicStage Create(Transform home)
    {
        var prefab=Resources.Load<GameObject>("Cinematics/DeclineStage");
        if(prefab==null)return null;
        var stage=Instantiate(prefab,home).GetComponent<DeclineCinematicStage>();stage.home=home;
        stage.remembered=OpeningCinematicStage.Create(home);
        stage.remembered.elias.gameObject.SetActive(false);stage.remembered.lia.gameObject.SetActive(false);
        foreach(var animation in stage.GetComponentsInChildren<Animation>())animation.enabled=false;
        return stage;
    }
    public void Evaluate(Camera camera,int line,float time,float duration,float total)
    {
        bool aftermath=line<6;
        osvaldo.gameObject.SetActive(aftermath);joana.gameObject.SetActive(aftermath);
        remembered.elias.gameObject.SetActive(!aftermath);remembered.lia.gameObject.SetActive(!aftermath);
        if(!aftermath)
        {
            remembered.Evaluate(camera,line%2==0?7:8,time,duration,total);
            return;
        }
        foreach(var actor in new[]{osvaldo,joana})
        {
            var location=actor.localPosition;var rotation=actor.localRotation;var anim=actor.GetComponent<Animation>();
            bool speaking=(line%2==0?joana:osvaldo)==actor;
            // Captured takes: the speaker talks (Trade = Talk01), the listener breathes (Sleep = Idle02).
            var take=speaking && anim.GetClip("Trade")!=null?anim.GetClip("Trade"):anim.GetClip("Sleep")??anim.GetClip("Idle");
            take.SampleAnimation(actor.gameObject,Mathf.Repeat(total+(actor==joana?1.1f:0),Mathf.Max(.1f,take.length)));actor.localPosition=location;actor.localRotation=rotation;
            var head=actor.GetComponentsInChildren<Transform>().First(t=>t.name=="Head");
            var other=actor==joana?osvaldo:joana;
            float turn=Mathf.Clamp(Vector3.SignedAngle(actor.forward,other.position-actor.position,Vector3.up),-18,18);
            float envelope=Mathf.Sin(Mathf.Clamp01(time/duration)*Mathf.PI);
            head.rotation=Quaternion.AngleAxis(turn*.45f,Vector3.up)*Quaternion.AngleAxis(4+envelope*2,actor.right)*head.rotation;
            var spine=actor.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Spine" || t.name=="Spine_02");
            
        }
        float dolly=Mathf.SmoothStep(0,1,Mathf.Clamp01(time/duration));
        if(line==0)
        {
            camera.transform.position=home.TransformPoint(new Vector3(6,2.7f,-11)+Vector3.forward*dolly*.4f);camera.transform.LookAt(home.TransformPoint(new Vector3(10,1,-5)));camera.fieldOfView=48;
            return;
        }
        var speaker=line%2==0?joana:osvaldo;var listener=speaker==joana?osvaldo:joana;
        OverShoulder(camera,Head(listener),Head(speaker),home.TransformPoint(new Vector3(10,1.8f,-10)),dolly);
    }
    static Transform Head(Transform actor)=>actor.GetComponentsInChildren<Transform>().First(t=>t.name=="Head");
    // Over-the-shoulder coverage: the listener's shoulder frames one side, the speaker stays whole,
    // and every angle keeps to the side of the conversation axis nearest the authored camera spot.
    public static void OverShoulder(Camera camera,Transform listenerHead,Transform speakerHead,Vector3 preferredSide,float dolly)
    {
        Vector3 axis=speakerHead.position-listenerHead.position;axis.y=0;axis.Normalize();
        Vector3 side=Vector3.Cross(Vector3.up,axis);
        Vector3 middle=(speakerHead.position+listenerHead.position)*.5f;
        if(Vector3.Dot(side,preferredSide-middle)<0)side=-side;
        Vector3 from=listenerHead.position-axis*(1.45f-dolly*.15f)+side*.85f+Vector3.up*.05f;
        camera.transform.position=from;camera.transform.LookAt(speakerHead.position-Vector3.up*.1f);camera.fieldOfView=36;
    }
    public void Release(){if(remembered!=null)remembered.Release();gameObject.SetActive(false);Destroy(gameObject);}
    void OnDestroy(){if(remembered!=null)remembered.Release();}
}
