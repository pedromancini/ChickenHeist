using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class VisitorCinematicStage : MonoBehaviour
{
    public Transform elias,visitor;
    public int Shot {get;private set;}
    public Transform Tablet=>props.tablet;
    public Vector3 BillCentre=>props.NoteCentre;
    public Vector3 EliasHand=>first.rightHand.position;
    public float GripError=>Mathf.Max(first.ContactError,second.ContactError);
    public VisitorCinematicActor EliasActor=>first;
    public VisitorCinematicActor VisitorActor=>second;
    public HomeDoor Door=>door;
    Transform home;Camera eye;VisitorCinematicActor first,second;VisitorCinematicProps props;
    HomeDoor door;Quaternion doorRotation;bool doorEnabled;
    Renderer[] hidden;bool[] visibility;
    readonly Dictionary<Light,float> lightLevels=new Dictionary<Light,float>();
    Color ambient,background;AmbientMode ambientMode;float ambientIntensity;bool fog;
    CameraClearFlags clearFlags;float near,aspect;Rect viewport;bool released,initialized;
    int previousLine=-1,knockIndex,stepIndex;float previousLocal;
    AudioSource effects,eliasSteps,visitorSteps,room;AudioClip knock,creak,night;
    readonly System.Collections.Generic.HashSet<int> playedSounds=new System.Collections.Generic.HashSet<int>();
    Vector3 desk=new Vector3(-5.80f,1.43f,3.05f);
    readonly Vector3 atDesk=new Vector3(-5.8f,.63f,3.70f),atDoor=new Vector3(-3.06f,.63f,2.62f),atPorch=new Vector3(-2.96f,.63f,1.20f);
    readonly Vector3 aside=new Vector3(-2.30f,.63f,3.20f);
    readonly Vector3 atKnock=new Vector3(-3.01f,.63f,1.76f),atWait=new Vector3(-2.86f,.63f,.83f),arrival=new Vector3(-3.3f,0,-4.3f),departure=new Vector3(-3.8f,0,-4.6f);
    public static VisitorCinematicStage Create(Transform home,Camera camera)
    {
        var prefab=Resources.Load<GameObject>("Cinematics/VisitorStage");
        if(prefab==null)return null;
        var stage=Instantiate(prefab,home).GetComponent<VisitorCinematicStage>();
        try{stage.Initialize(home,camera);return stage;}
        catch{stage.Release();throw;}
    }
    void Initialize(Transform location,Camera camera)
    {
        home=location;eye=camera;first=new VisitorCinematicActor(elias);second=new VisitorCinematicActor(visitor);
        door=home.GetComponentInChildren<HomeDoor>();
        if(door!=null){doorRotation=door.hinge.localRotation;doorEnabled=door.enabled;door.enabled=false;door.hinge.localRotation=Quaternion.identity;}
        var player=HeistGameManager.Instance.player;
        hidden=player.GetComponentsInChildren<Renderer>(true).Concat(home.GetComponentsInChildren<Renderer>().Where(r=>r.name=="Celular antigo - tela rachada" || r.name=="Tela" || r.name=="Vidro rachado" || r.name=="Conta atrasada" || r.name=="Linha da cobranca")).ToArray();
        visibility=hidden.Select(r=>r.forceRenderingOff).ToArray();foreach(var r in hidden)r.forceRenderingOff=true;
        ambient=RenderSettings.ambientLight;ambientMode=RenderSettings.ambientMode;ambientIntensity=RenderSettings.ambientIntensity;fog=RenderSettings.fog;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.15f,.19f,.29f);RenderSettings.ambientIntensity=1;RenderSettings.fog=false;
        foreach(var light in FindObjectsByType<Light>())if(light.type==LightType.Directional || light.transform.IsChildOf(home))
        {lightLevels[light]=light.intensity;light.intensity=light.type==LightType.Directional?.16f:light.intensity*.35f;}
        clearFlags=eye.clearFlags;background=eye.backgroundColor;near=eye.nearClipPlane;viewport=eye.rect;aspect=eye.aspect;
        initialized=true;
        eye.clearFlags=CameraClearFlags.SolidColor;eye.backgroundColor=new Color(.025f,.037f,.065f);eye.nearClipPlane=.035f;
        eye.rect=new Rect(0,.18f,1,.74f);eye.ResetAspect();
        var table=home.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Prop_Wooden_Table_02");
        if(table!=null){var renderers=table.GetComponentsInChildren<Renderer>();if(renderers.Length>0){var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);desk=home.InverseTransformPoint(new Vector3(b.center.x,b.max.y+.025f,b.center.z));}}
        props=new VisitorCinematicProps(transform,home,second.head,visitor,desk);
        LightAt("Abajur da mesa",new Vector3(-5.8f,2.62f,3.5f),new Color(1,.69f,.36f),2.2f,5);
        LightAt("Lampada sobre a porta",new Vector3(-3.8f,2.95f,2.5f),new Color(1,.77f,.48f),1.35f,5);
        LightAt("Luar na varanda",new Vector3(-1.2f,3.7f,-.6f),new Color(.38f,.55f,1),1.5f,7);
        LightAt("Retorno suave da cozinha",new Vector3(-2.2f,2.7f,4.5f),new Color(.60f,.68f,1),.75f,5);
        LightAt("Lampada da varanda",new Vector3(-3.0f,2.65f,1.85f),new Color(1,.80f,.55f),1.1f,3.2f);
        effects=Source("Madeira e objetos",1);effects.transform.position=World(new Vector3(-3.2f,1.7f,2.28f));
        eliasSteps=Source("Passos de Elias",1);visitorSteps=Source("Passos do visitante",1);room=Source("Insetos ao longe",0);
        knock=SoundLibrary.Or("knock",()=>Sound("Batidas na madeira",.18f,145,31));creak=SoundLibrary.Or("door_creak",()=>Sound("Dobradica gasta",.8f,320,13));
        night=SoundLibrary.Or("night_ambience",NightSound);room.clip=night;room.loop=true;room.volume=SoundLibrary.Load("night_ambience")!=null?.12f:.025f;room.GetComponent<AudioBusGain>().ambient=true;room.Play();
    }
    AudioSource Source(string name,float spatial)
    {var node=new GameObject(name);node.transform.SetParent(transform,false);var source=node.AddComponent<AudioSource>();node.AddComponent<AudioBusGain>();source.playOnAwake=false;source.spatialBlend=spatial;source.minDistance=1.5f;source.maxDistance=16;source.rolloffMode=AudioRolloffMode.Linear;return source;}
    public void SetPaused(bool value){foreach(var source in new[]{effects,eliasSteps,visitorSteps,room})if(source!=null){if(value)source.Pause();else source.UnPause();}}
    void LightAt(string name,Vector3 p,Color color,float intensity,float range)
    {var l=new GameObject(name).AddComponent<Light>();l.transform.SetParent(transform,false);l.transform.position=home.TransformPoint(p);l.type=LightType.Point;l.color=color;l.intensity=intensity;l.range=range;l.shadows=name=="Lampada sobre a porta"?LightShadows.Soft:LightShadows.None;}
    static float Ease(float t)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
    Vector3 World(Vector3 p)=>home.TransformPoint(p);
    public void Evaluate(Camera camera,int line,float localTime,float duration,float total)
    {
        if(released)return;
        float p=Ease(localTime/duration);bool change=line!=previousLine;
        if(change){previousLine=line;knockIndex=0;stepIndex=0;previousLocal=0;}
        Vector3 ep=atDoor,vp=atPorch;float eyaw=180,vyaw=0;bool emove=false,vmove=false,vback=false,eback=false;
        float edistance=0,vdistance=0,ew=1,vw=1;Vector3 vfrom=vp,vto=vp;
        if(line==0){ep=atDesk;vfrom=arrival;vto=atKnock;vp=Vector3.Lerp(arrival,atKnock,p);vmove=p<.99f;}
        if(line==1)vp=atKnock;
        if(line==2){float back=Ease((localTime-.3f)/1.3f);vfrom=atKnock;vto=atWait;vp=Vector3.Lerp(atKnock,atWait,back);vmove=back>0 && back<1;vback=true;}
        if(line>=3 && line<=5)vp=atWait;
        if(line==6){float step=Ease((localTime-.2f)/1.3f);vfrom=atWait;vto=atPorch;vp=Vector3.Lerp(atWait,atPorch,step);vmove=step>0 && step<1;}
        if(line==5){float back=Ease((localTime-.8f)/1.3f);ep=Vector3.Lerp(atDoor,aside,back);emove=back>0 && back<1;eback=true;}
        if(line==6){float step=Ease((localTime-.4f)/1.2f);ep=Vector3.Lerp(aside,atDoor,step);emove=step>0 && step<1;}
        if(line==1){float travel=Ease((localTime-1.2f)/(duration-1.2f));ep=Vector3.Lerp(atDesk,new Vector3(-4,.63f,3.9f),travel);eyaw=Mathf.Lerp(180,120,travel);emove=travel>0 && travel<1;}
        if(line==2){ep=Vector3.Lerp(new Vector3(-4,.63f,3.9f),atDoor,Ease(localTime/1.6f));eyaw=Mathf.Lerp(120,180,p);emove=localTime<1.6f;}
        if(line==28){float move=Ease((localTime-.85f)/(duration-.85f));vfrom=atPorch;vto=departure;vp=Vector3.Lerp(atPorch,departure,move);vyaw=Mathf.Lerp(0,188,Ease(localTime/.85f));vmove=localTime>.85f && move<1;}
        if(line>=29)
        {
            vp=departure;visitor.gameObject.SetActive(false);ep=atDesk;
            if(line==29)
            {
                if(localTime<CloseGrab)
                {
                    float go=Ease(localTime/CloseGrab);var round=AroundDoor();var grab=Closing(DoorOpen);
                    float first=Vector3.Distance(atDoor,round),route=first+Vector3.Distance(round,grab),along=go*route;
                    ep=along<first?Vector3.Lerp(atDoor,round,along/first):Vector3.Lerp(round,grab,(along-first)/Mathf.Max(.01f,route-first));
                    var heading=along<first?round-atDoor:grab-round;float walkYaw=Mathf.Atan2(heading.x,heading.z)*Mathf.Rad2Deg;
                    eyaw=go<.85f?Mathf.LerpAngle(180,walkYaw,Ease(localTime/.4f)):Mathf.LerpAngle(walkYaw,180+DoorOpen,Ease((go-.85f)/.15f));emove=go<1;
                }
                else if(localTime<CloseEnd){float a=DoorAngle(29,localTime);ep=Closing(a);eyaw=180+a;emove=localTime>ClosePull && Mathf.Abs(a)>.5f;}
                else
                {
                    float move=Ease((localTime-CloseEnd-.3f)/(duration-CloseEnd-.3f));var from=Closing(0);ep=Vector3.Lerp(from,atDesk,move);
                    var way=atDesk-from;float walkYaw=Mathf.Atan2(way.x,way.z)*Mathf.Rad2Deg;
                    eyaw=move<.8f?Mathf.LerpAngle(180,walkYaw,Ease((localTime-CloseEnd)/.5f)):Mathf.LerpAngle(walkYaw,180,Ease((move-.8f)/.2f));emove=move>0 && move<1;
                }
            }
        }
        else visitor.gameObject.SetActive(true);
        ep=Ground(ep);vp=Ground(vp);
        if(vfrom!=vto)vdistance=Vector3.Distance(vfrom,vp);
        if(line==1)edistance=Vector3.Distance(atDesk,ep);
        if(line==2)edistance=Vector3.Distance(atDesk,new Vector3(-4,.63f,3.9f))+Vector3.Distance(new Vector3(-4,.63f,3.9f),ep);
        if(line==29)edistance=localTime<CloseGrab?PathWalked(ep):localTime<CloseEnd?Vector3.Distance(Closing(DoorOpen),ep):Vector3.Distance(Closing(0),ep);
        if(line==5)edistance=Vector3.Distance(atDoor,ep);
        if(line==6 && ep!=atDoor)edistance=Vector3.Distance(aside,ep);
        ew=Mathf.Clamp01(edistance*6)*Mathf.Clamp01(Vector3.Distance(ep,line==1?new Vector3(-4,.63f,3.9f):line==2 || line==6?atDoor:line==5?aside:line==29 && localTime<CloseGrab?Closing(DoorOpen):atDesk)*6);
        vw=Mathf.Clamp01(vdistance*5)*Mathf.Clamp01(Vector3.Distance(vp,Ground(vto))*5);
        if(door!=null){float angle=line<5?0:line==5?Mathf.Lerp(0,DoorOpen,Ease((localTime-.5f)/(duration-.8f))):line<29?DoorOpen:line==29?DoorAngle(29,localTime):0;door.hinge.localRotation=Quaternion.Euler(0,angle,0);}
        Vector3 egaze=line==0 || line>=29?desk:vp+Vector3.up*1.58f;
        Vector3 vgaze=line==6?desk:ep+Vector3.up*1.57f;
        if(line==22)vgaze=Vector3.Lerp(vgaze,vp+new Vector3(3,1.5f,-1),Ease((p-.30f)/.12f)*(1-Ease((p-.63f)/.12f)));
        if(line==0)egaze=home.InverseTransformPoint(props.NoteCentre);
        first.Pose(ep,eyaw,total,emove,VisitorOpeningDialogue.Speakers[line]=="Elias",World(egaze),line==0?12:line>=29?6:line==14?-3:2,edistance,ew,eback);
        second.Pose(vp,vyaw,total,vmove,VisitorOpeningDialogue.Speakers[line]=="Visitante",World(vgaze),line==1?4:0,vdistance,vw,vback);
        string said=VisitorOpeningDialogue.Text[line];
        bool jump=change || Mathf.Abs(localTime-previousLocal)>.1f;
        first.Speak(VisitorOpeningDialogue.Speakers[line]=="Elias"?VisitorCinematicActor.Spoken(said,localTime,duration):0,jump);
        second.Speak(VisitorOpeningDialogue.Speakers[line]=="Visitante"?VisitorCinematicActor.Spoken(said,localTime,duration):0,jump);
        if(line==0)
        {
            Vector3 across=Vector3.ProjectOnPlane(elias.forward,home.up).normalized;
            first.Flat(true,props.NoteContact+props.NoteUp*.012f,-props.NoteUp,Quaternion.AngleAxis(-18,home.up)*across,.95f);
            first.Flat(false,props.DeskBeside+props.NoteUp*.012f,-props.NoteUp,Quaternion.AngleAxis(18,home.up)*across,.95f);
        }
        if(line==1 && door!=null)
        {
            float hit=0;foreach(float t in KnockTimes)hit=Mathf.Max(hit,Mathf.Clamp01(1-Mathf.Abs(localTime-t)/.13f));
            float raise=Ease((localTime-.25f)/.4f)*(1-Ease((localTime-3.25f)/.6f));
            var knuckle=door.hinge.InverseTransformPoint(World(vp+new Vector3(.17f,0,0)));knuckle.y=1.36f;knuckle.z=-.042f-(1-hit)*.065f;
            second.Knock(true,door.hinge.TransformPoint(knuckle),door.hinge.forward,raise);
        }
        if(line==5 && door!=null)first.Bar(false,door.hinge.TransformPoint(new Vector3(1.30f,1.06f,.10f)),door.hinge.right,-home.up,.018f,Ease(localTime/.45f)*(1-Ease((localTime-.95f)/.4f)));
        props.tablet.gameObject.SetActive(false);
        if(line==29 && door!=null)first.Bar(false,door.hinge.TransformPoint(new Vector3(1.30f,1.06f,.10f)),door.hinge.right,-home.up,.018f,Ease((localTime-.9f)/.35f)*(1-Ease((localTime-CloseEnd+.15f)/.3f)));
        first.Twist();second.Twist();
        Direct(camera,line,p,ep,vp);
        SoundEvents(line,localTime,emove,vmove,change,edistance,vdistance);
        previousLocal=localTime;
    }
    public const float CloseGrab=2.1f,ClosePull=2.4f,CloseEnd=4.4f;
    public const float DoorOpen=HomeDoor.OpenAngle;
    Vector3 AroundDoor()
    {
        if(door==null)return atDoor;
        var edge=home.InverseTransformPoint(door.hinge.parent.TransformPoint(door.hinge.localPosition+Quaternion.Euler(0,DoorOpen,0)*new Vector3(1.46f,0,0)));
        var p=edge+new Vector3(.25f,0,.45f);p.y=atDoor.y;return p;
    }
    float PathWalked(Vector3 at)
    {
        var round=AroundDoor();float first=Vector3.Distance(atDoor,round);
        return Vector3.Distance(atDoor,at)<=first+.01f && Vector3.Distance(at,round)>.02f?Vector3.Distance(atDoor,at):first+Vector3.Distance(round,at);
    }
    float DoorAngle(int line,float t)=>line==29?Mathf.Lerp(DoorOpen,0,Ease((t-ClosePull)/(CloseEnd-ClosePull-.15f))):DoorOpen;
    Vector3 Closing(float angle)
    {
        if(door==null)return atDoor;
        var turn=Quaternion.Euler(0,angle,0);
        var lever=home.InverseTransformPoint(door.hinge.parent.TransformPoint(door.hinge.localPosition+turn*new Vector3(1.30f,0,.10f)));
        var inside=home.InverseTransformDirection(door.hinge.parent.TransformDirection(turn*Vector3.forward));inside.y=0;inside.Normalize();
        var left=Vector3.Cross(-inside,Vector3.up);
        var p=lever+inside*.42f-left*.17f;p.y=atDoor.y;return p;
    }
    static readonly float[] KnockTimes={.75f,1.09f,1.43f,2.8f};
    const float EliasEye=1.63f;
    void Direct(Camera camera,int line,float p,Vector3 ep,Vector3 vp)
    {
        Vector3 from,target;float fov=46;
        if(line==0){Shot=0;from=Vector3.Lerp(new Vector3(-4.90f,2.55f,2.80f),new Vector3(-5.05f,2.42f,2.92f),p);target=desk+new Vector3(0,.32f,.22f);fov=50;}
        else if(line==1){Shot=1;from=new Vector3(-1.40f,2.32f,.30f)+Vector3.forward*p*.16f;target=new Vector3(-3.13f,1.80f,2.05f);fov=48;}
        else if(line==3){Shot=11;from=new Vector3(-2.42f,1.95f,1.86f)+Vector3.back*p*.05f;target=vp+new Vector3(0,1.47f,0);fov=44;}
        else if(line==4){Shot=2;from=ep+new Vector3(.42f,EliasEye+.12f,.70f)+Vector3.back*p*.05f;target=ep+new Vector3(0,EliasEye-.25f,-.45f);fov=50;}
        else if(line==2 || line==7 || line==26)
        {Shot=2;from=line<5?new Vector3(-1.80f,2.12f,3.40f):new Vector3(-2.80f,2.10f,1.88f);target=ep+new Vector3(0,EliasEye-.05f,-.05f);fov=48;from+=Vector3.forward*p*.04f;}
        else if(line==5){Shot=12;from=Vector3.Lerp(new Vector3(-2.62f,2.26f,4.80f),new Vector3(-2.66f,2.24f,4.66f),p);target=new Vector3(-3.12f,1.56f,2.30f);fov=42;}
        else if(line==10 || line==11 || line==15 || line==16 || line==18 || line==23 || line==24 || line==25)
        {Shot=5;from=new Vector3(-2.66f,2.46f,3.12f)+new Vector3(-.04f,0,.05f)*p;target=vp+new Vector3(-.02f,1.35f,.30f);fov=44;}
        else if(VisitorOpeningDialogue.Speakers[line]=="Elias")
        {Shot=10;from=Vector3.Lerp(new Vector3(-2.80f,0,1.88f),new Vector3(-2.68f,0,1.97f),p)+Vector3.up*(ep.y+EliasEye-.28f);target=ep+Vector3.up*(EliasEye-.04f);fov=45;}
        else if(line==28){Shot=6;from=Vector3.Lerp(new Vector3(-2.66f,2.42f,3.12f),new Vector3(-2.74f,2.36f,3.00f),p);target=vp+Vector3.up*1.2f;fov=48;}
        else if(line==29){Shot=7;from=new Vector3(-2.62f,2.30f,4.85f)+Vector3.left*p*.35f;target=ep+Vector3.up*(EliasEye-.35f);fov=52;}
        else if(line==30){Shot=8;from=Vector3.Lerp(desk+new Vector3(.95f,1.45f,.95f),desk+new Vector3(.80f,1.25f,.85f),p);target=desk+new Vector3(.18f,.30f,.10f);fov=50;}
        else{Shot=9;from=new Vector3(-2.74f,2.05f,2.00f)+Vector3.back*p*.03f;target=vp+new Vector3(.02f,1.44f,0);fov=line==14 || line==20?38:43;}
        camera.transform.SetPositionAndRotation(World(from),Quaternion.LookRotation(World(target)-World(from),home.up));camera.fieldOfView=fov;
    }
    Vector3 Ground(Vector3 position)
    {
        Vector3 origin=World(position)+home.up*.25f;
        var hits=Physics.RaycastAll(origin,-home.up,1.15f,~0,QueryTriggerInteraction.Ignore);
        float nearest=float.PositiveInfinity;
        foreach(var hit in hits)if(hit.distance<nearest && Vector3.Dot(hit.normal,home.up)>.65f && !hit.transform.IsChildOf(HeistGameManager.Instance.player))nearest=hit.distance;
        if(!float.IsPositiveInfinity(nearest))position=home.InverseTransformPoint(origin-home.up*nearest);
        return position;
    }
    void SoundEvents(int line,float time,bool walking,bool visitorWalking,bool changed,float edistance,float vdistance)
    {
        eliasSteps.transform.position=elias.position;visitorSteps.transform.position=visitor.position;
        if(line==1)
        {
            if(previousLocal<.05f && time>=.05f)HomeDoorbell.Instance?.Ring();
            while(knockIndex<KnockTimes.Length && time>=KnockTimes[knockIndex]){effects.PlayOneShot(knock,knockIndex==3?.8f:.6f);knockIndex++;}
        }
        if(line==5 && previousLocal<.5f && time>=.5f || line==29 && previousLocal<ClosePull && time>=ClosePull)effects.PlayOneShot(creak,.18f);
        if(line==29 && previousLocal<CloseEnd-.15f && time>=CloseEnd-.15f)effects.PlayOneShot(knock,.35f);
        if(line==0 && previousLocal<.85f && time>=.85f)eliasSteps.PlayOneShot(knock,.045f);
        if(line==30 && previousLocal<1.6f && time>=1.6f)eliasSteps.PlayOneShot(knock,.10f);
        if(line==28 && previousLocal<5.7f && time>=5.7f)GameAudioMix.Instance?.Play("door",World(new Vector3(-3.8f,0,-4.6f)),.15f);
    }
    static AudioClip NightSound()
    {
        const int rate=22050;var data=new float[rate*6];
        for(int i=0;i<data.Length;i++){float t=(float)i/rate;float pulse=Mathf.Pow(Mathf.Max(0,Mathf.Sin(t*Mathf.PI*8)),12);float window=Mathf.Pow(Mathf.Sin(t*Mathf.PI/6),2);data[i]=Mathf.Sin(t*2*Mathf.PI*3100)*pulse*window*.12f;}
        var clip=AudioClip.Create("Insetos sintetizados distantes",data.Length,1,rate,false);clip.SetData(data,0);return clip;
    }
    static AudioClip Sound(string name,float duration,float frequency,int seed)
    {
        const int rate=22050;var data=new float[(int)(rate*duration)];var random=new System.Random(seed);
        for(int i=0;i<data.Length;i++){float t=(float)i/rate;float envelope=Mathf.Min(1,t*450)*Mathf.Exp(-t*(duration>.5f?4:32));data[i]=envelope*(Mathf.Sin(t*frequency*6.283f)*.55f+Mathf.Sin(t*frequency*10.7f)*.18f+((float)random.NextDouble()*2-1)*.22f);}
        var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);return clip;
    }
    public void Release(){Restore();gameObject.SetActive(false);Destroy(gameObject);}
    void Restore()
    {
        if(released || !initialized)return;released=true;
        if(hidden!=null)for(int i=0;i<hidden.Length;i++)if(hidden[i]!=null)hidden[i].forceRenderingOff=visibility[i];
        if(door!=null){door.hinge.localRotation=doorRotation;door.enabled=doorEnabled;}
        foreach(var pair in lightLevels)if(pair.Key!=null)pair.Key.intensity=pair.Value;
        RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambient;RenderSettings.ambientIntensity=ambientIntensity;RenderSettings.fog=fog;
        if(eye!=null){eye.clearFlags=clearFlags;eye.backgroundColor=background;eye.nearClipPlane=near;eye.rect=viewport;eye.aspect=aspect;}
        foreach(var source in new[]{effects,eliasSteps,visitorSteps,room})if(source!=null)source.Stop();props?.Dispose();
        foreach(var clip in new[]{knock,creak,night})SoundLibrary.Release(clip);
    }
    void OnDestroy(){Restore();}
}
