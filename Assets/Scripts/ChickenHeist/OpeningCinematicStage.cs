using System.Linq;
using UnityEngine;

// Temporary actors; no gameplay actor, inventory or save is moved by the scene.
public sealed class OpeningCinematicStage : MonoBehaviour
{
    public Transform elias,lia;
    Transform home; Actor first,second; Light key,fill;
    Renderer[] playerRenderers; bool[] playerVisibility;
    Vector3 cameraFrom,cameraTarget;
    public int Shot {get;private set;}
    public Vector3 EliasHand=>first.hand.position;
    sealed class Actor
    {
        public Transform root,head,spine,arm,forearm,hand; public Animation animation;
        public AnimationClip idle,walk,talk; public Vector3 origin;float talkWeight;
        Transform[] bones;Quaternion[] talkPose;
        public Actor(Transform r)
        {
            root=r;origin=r.localPosition;animation=r.GetComponentInChildren<Animation>();
            idle=Clip("Breathe","Idle");walk=animation.GetClip("Walk");animation.enabled=false;
            bool protagonist=r.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="ProtagonistRig");
            talk=protagonist?Clip("Talk"):Clip("Talk","Trade");
            bones=animation.GetComponentsInChildren<Transform>(true);talkPose=new Quaternion[bones.Length];
            var all=r.GetComponentsInChildren<Transform>();
            head=all.First(t=>t.name=="Head");
            spine=all.First(t=>t.name=="Spine" || t.name=="Spine_02");
            arm=all.First(t=>t.name=="UpperArmR" || t.name=="Upperarm_R");
            forearm=all.First(t=>t.name=="ForearmR" || t.name=="Lowerarm_R");
            hand=all.First(t=>t.name=="HandR" || t.name=="Hand_R");
        }
        AnimationClip Clip(params string[] names){foreach(var n in names){var c=animation.GetClip(n);if(c!=null)return c;}return null;}
        // Standing in place: captured breathing for the listener, captured conversation for the speaker.
        public void Pose(float time,float lineTime,bool speaking,int line,bool departing)
        {
            talkWeight=Mathf.MoveTowards(talkWeight,speaking && talk!=null?1:0,Time.deltaTime*2.2f);
            if(talkWeight>0){talk.SampleAnimation(animation.gameObject,Mathf.Repeat(time,talk.length));for(int i=0;i<bones.Length;i++)talkPose[i]=bones[i].localRotation;}
            idle.SampleAnimation(animation.gameObject,Mathf.Repeat(time,Mathf.Max(.1f,idle.length)));
            if(talkWeight>0){float w=Mathf.SmoothStep(0,1,talkWeight);for(int i=0;i<bones.Length;i++)bones[i].localRotation=Quaternion.Slerp(bones[i].localRotation,talkPose[i],w);}
        }
    }
    public static OpeningCinematicStage Create(Transform home)
    {
        var prefab=Resources.Load<GameObject>("Cinematics/OpeningStage");
        if(prefab==null)return null;
        var stage=Instantiate(prefab,home).GetComponent<OpeningCinematicStage>();stage.Initialize(home);return stage;
    }
    void Initialize(Transform h)
    {
        home=h;elias.localPosition+=Vector3.left*5;lia.localPosition+=Vector3.left*5;first=new Actor(elias);second=new Actor(lia);
        var player=FindAnyObjectByType<PlayerMovement>();
        playerRenderers=player!=null?player.GetComponentsInChildren<Renderer>(true):new Renderer[0];
        playerVisibility=playerRenderers.Select(r=>r.forceRenderingOff).ToArray();
        foreach(var r in playerRenderers)r.forceRenderingOff=true;
        key=NewLight("Luz quente da conversa",new Vector3(-9,4,-10),new Color(1,.80f,.60f),3.4f);
        fill=NewLight("Luz suave da varanda",new Vector3(-5,3,-7),new Color(.70f,.80f,1),1.8f);
    }
    Light NewLight(string label,Vector3 at,Color color,float intensity)
    {
        var go=new GameObject(label);go.transform.SetParent(transform,false);go.transform.localPosition=at;
        var l=go.AddComponent<Light>();l.type=LightType.Point;l.range=12;l.color=color;l.intensity=intensity;return l;
    }
    public void Evaluate(Camera camera,int line,float localTime,float duration,float total)
    {
        // Elias speaks on odd lines, Lia on even ones (see Shot selection below).
        first.Pose(total,localTime,line%2==1,line,false);second.Pose(total+1.3f,localTime,line%2==0 && line>0,line,false);
        elias.localPosition=first.origin;lia.localPosition=second.origin;
        Shot=line==0?0:line==3 && localTime>duration*.55f?3:line==11?4:line%2==0?1:2;
        Vector3 from,target;
        if(Shot==0){from=new Vector3(-6.2f,2.5f,-12.5f);target=new Vector3(-3,1.15f,-5.5f);}
        else if(Shot==1){from=new Vector3(-4.7f,1.65f,-8.2f);target=home.InverseTransformPoint(lia.position)+Vector3.up*1.4f;}
        else if(Shot==2){from=new Vector3(-.9f,1.7f,-8.4f);target=home.InverseTransformPoint(elias.position)+Vector3.up*1.42f;}
        else if(Shot==3){from=new Vector3(6,2.3f,-10);target=new Vector3(11,1.25f,-4);}
        else {from=new Vector3(-6.2f,2.3f,-10.5f);target=new Vector3(-3.6f,1.2f,-6);}
        if(Shot!=3){from+=Vector3.left*5;if(Shot==0 || Shot==4)target+=Vector3.left*5;}
        float progress=Mathf.SmoothStep(0,1,Mathf.Clamp01(localTime/duration));
        from+=new Vector3(progress*.25f,0,progress*.35f);
        // Cut on speaker changes, then a gentle dolly; never fly through the set.
        cameraFrom=from;cameraTarget=target;
        if(Shot==1 || Shot==2)
        {
            // Authored spots sat between the actors and cut the listener in half; frame over the listener's shoulder instead.
            var speaker=Shot==1?second:first;var listener=Shot==1?first:second;
            DeclineCinematicStage.OverShoulder(camera,listener.head,speaker.head,home.TransformPoint(new Vector3(-7.8f,1.7f,-8.3f)),progress);
            return;
        }
        camera.transform.position=home.TransformPoint(cameraFrom);camera.transform.LookAt(home.TransformPoint(cameraTarget));
        camera.fieldOfView=Shot==0?48:Shot==3?45:Shot==4?48:40;
    }
    public void Release()
    {
        if(playerRenderers!=null)for(int i=0;i<playerRenderers.Length;i++)if(playerRenderers[i]!=null)playerRenderers[i].forceRenderingOff=playerVisibility[i];
        playerRenderers=null;gameObject.SetActive(false);Destroy(gameObject);
    }
    void OnDestroy(){if(playerRenderers!=null)for(int i=0;i<playerRenderers.Length;i++)if(playerRenderers[i]!=null)playerRenderers[i].forceRenderingOff=playerVisibility[i];}
}


