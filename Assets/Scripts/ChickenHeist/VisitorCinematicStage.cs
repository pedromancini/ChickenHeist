using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

// Real-time scene in the player's house. Actors/props are temporary; gameplay
// transforms, economy, door state, rendering and camera settings are restored.
public sealed class VisitorCinematicStage : MonoBehaviour
{
    public Transform elias,visitor;
    public int Shot {get;private set;}
    public Transform Tablet=>props.tablet;
    public Vector3 EliasHand=>first.rightHand.position;
    public float GripError=>Mathf.Max(first.ContactError,second.ContactError);
    Transform home;Camera eye;VisitorCinematicActor first,second;VisitorCinematicProps props;
    HomeDoor door;Quaternion doorRotation;bool doorEnabled;
    Renderer[] hidden;bool[] visibility;
    readonly Dictionary<Light,float> lightLevels=new Dictionary<Light,float>();
    Color ambient,background;AmbientMode ambientMode;float ambientIntensity;bool fog;
    CameraClearFlags clearFlags;float near,aspect;Rect viewport;bool released,initialized;
    int previousLine=-1,knockIndex,stepIndex;float previousLocal;
    AudioSource effects,eliasSteps,visitorSteps,room;AudioClip knock,footstep,creak,night;
    readonly System.Collections.Generic.HashSet<int> playedSounds=new System.Collections.Generic.HashSet<int>();
    readonly Vector3 closeDoorPosition=new Vector3(-3.52f,.63f,2.53f);
    Vector3 desk=new Vector3(-5.80f,1.43f,3.05f);
    readonly Vector3 atDesk=new Vector3(-5.8f,.63f,3.70f),atDoor=new Vector3(-3.06f,.63f,2.75f),atPorch=new Vector3(-2.96f,.63f,1.55f);
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
        effects=Source("Madeira e objetos",1);effects.transform.position=World(new Vector3(-3.2f,1.7f,2.28f));
        eliasSteps=Source("Passos de Elias",1);visitorSteps=Source("Passos do visitante",1);room=Source("Insetos ao longe",0);
        knock=Sound("Batidas na madeira",.18f,145,31);footstep=Sound("Passo na varanda",.15f,80,79);creak=Sound("Dobradica gasta",.8f,320,13);
        night=NightSound();room.clip=night;room.loop=true;room.volume=.025f;room.GetComponent<AudioBusGain>().ambient=true;room.Play();
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
        Vector3 ep=atDoor,vp=atPorch;float eyaw=180,vyaw=0;bool emove=false,vmove=false;
        float edistance=0,vdistance=0,ew=1,vw=1;
        if(line==0){ep=atDesk;vp=Vector3.Lerp(new Vector3(-3.3f,0,-4.3f),atPorch,p);vmove=p<.99f;}
        if(line==1){float travel=Ease((localTime-1.2f)/(duration-1.2f));ep=Vector3.Lerp(atDesk,new Vector3(-4,.63f,3.9f),travel);eyaw=Mathf.Lerp(180,120,travel);emove=travel>0 && travel<1;}
        if(line==2){ep=Vector3.Lerp(new Vector3(-4,.63f,3.9f),atDoor,Ease(localTime/1.6f));eyaw=Mathf.Lerp(120,180,p);emove=localTime<1.6f;}
        if(line==28){float move=Ease((localTime-.85f)/(duration-.85f));vp=Vector3.Lerp(atPorch,new Vector3(-3.8f,0,-4.6f),move);vyaw=Mathf.Lerp(0,188,Ease(localTime/.85f));vmove=localTime>.85f && move<1;}
        if(line>=29)
        {
            vp=new Vector3(-3.8f,0,-4.6f);visitor.gameObject.SetActive(false);ep=atDesk;
            if(line==29)
            {
                if(localTime<.85f){ep=Vector3.Lerp(atDoor,closeDoorPosition,Ease(localTime/.85f));eyaw=Mathf.LerpAngle(180,215,Ease(localTime/.85f));emove=true;}
                else if(localTime<3.2f){ep=closeDoorPosition;eyaw=Mathf.LerpAngle(215,292,Ease((localTime-2.4f)/.8f));}
                else{float move=Ease((localTime-3.2f)/(duration-3.2f));ep=Vector3.Lerp(closeDoorPosition,atDesk,move);eyaw=Mathf.LerpAngle(292,180,Ease((move-.8f)/.2f));emove=move<1;}
            }
        }
        else visitor.gameObject.SetActive(true);
        ep=Ground(ep);vp=Ground(vp);
        if(line==0)vdistance=Vector3.Distance(new Vector3(-3.3f,0,-4.3f),vp);
        if(line==1)edistance=Vector3.Distance(atDesk,ep);
        if(line==2)edistance=Vector3.Distance(atDesk,new Vector3(-4,.63f,3.9f))+Vector3.Distance(new Vector3(-4,.63f,3.9f),ep);
        if(line==28)vdistance=Vector3.Distance(atPorch,vp);
        if(line==29)edistance=localTime<.85f?Vector3.Distance(atDoor,ep):Vector3.Distance(atDoor,closeDoorPosition)+Vector3.Distance(closeDoorPosition,ep);
        ew=Mathf.Clamp01(edistance*6)*Mathf.Clamp01(Vector3.Distance(ep,line==1?new Vector3(-4,.63f,3.9f):line==2?atDoor:atDesk)*6);
        vw=Mathf.Clamp01(vdistance*5)*Mathf.Clamp01(Vector3.Distance(vp,line==0?atPorch:new Vector3(-3.8f,0,-4.6f))*5);
        if(door!=null){float angle=line<5?0:line==5?Mathf.Lerp(0,76,Ease((localTime-.5f)/(duration-.8f))):line<29?76:line==29?Mathf.Lerp(76,0,Ease((localTime-1.05f)/1.1f)):0;door.hinge.localRotation=Quaternion.Euler(0,angle,0);}
        Vector3 tabletPosition=new Vector3(-3.04f,1.73f,2.22f);
        Vector3 egaze=line==0 || line>=29?desk:line>=9 && line<=20 || line==23 || line==24?tabletPosition:vp+Vector3.up*1.58f;
        Vector3 vgaze=line==6?desk:line>=9 && line<=11?tabletPosition:ep+Vector3.up*1.57f;
        if(line==22)vgaze=Vector3.Lerp(vgaze,vp+new Vector3(3,1.5f,-1),Ease((p-.30f)/.12f)*(1-Ease((p-.63f)/.12f)));
        first.Pose(ep,eyaw,total,emove,VisitorOpeningDialogue.Speakers[line]=="Elias",World(egaze),line==0?12:line>=29?6:line==14?-3:2,edistance,ew);
        second.Pose(vp,vyaw,total,vmove,VisitorOpeningDialogue.Speakers[line]=="Visitante",World(vgaze),line==1?4:0,vdistance,vw);
        props.Paper(line==0?localTime:6);
        if(line>=3 && line<9)
        {
            float gesture=Mathf.Sin(p*Mathf.PI)*.7f;
            if(VisitorOpeningDialogue.Speakers[line]=="Elias")first.Reach(true,elias.TransformPoint(new Vector3(.26f,1.12f,.27f)),gesture);
            else second.Reach(true,visitor.TransformPoint(new Vector3(.25f,1.12f,.26f)),gesture);
        }
        if(line==0)
        {
            first.Reach(true,props.KeyContact+home.up*(.025f+Mathf.Abs(localTime-.85f)*.02f),localTime<1.5f?.95f:.35f);
            first.Reach(false,props.NoteContact,.95f);
        }
        if(line==1)
        {
            float hit=0;foreach(float t in new[]{.2f,.54f,.88f,2.25f})hit=Mathf.Max(hit,Mathf.Clamp01(1-Mathf.Abs(localTime-t)/.13f));
            second.Reach(true,World(new Vector3(-3.12f,2.05f,2.20f-hit*.07f)),.95f);
        }
        if(line==5 && door!=null)first.Reach(true,door.hinge.TransformPoint(new Vector3(Mathf.Lerp(1.3f,.35f,p),1.06f,-.09f)),Ease(localTime/.4f)*(1-Ease((p-.8f)/.2f)));
        props.tablet.gameObject.SetActive(line>=9);
        if(line>=9 && line<24)
        {
            float reveal=line==9?p:1;
            tabletPosition=Vector3.Lerp(vp+new Vector3(-.2f,.80f,.13f),tabletPosition,reveal);
            props.tablet.position=World(tabletPosition);props.tablet.rotation=home.rotation*Quaternion.Euler(62,180,5);
            second.Grip(true,props.tablet.TransformPoint(new Vector3(-.23f,.035f,0)),props.tablet.right,-props.tablet.up);
            second.Grip(false,props.tablet.TransformPoint(new Vector3(.23f,.035f,0)),-props.tablet.right,-props.tablet.up);
            if(line==15 || line==19)first.Reach(true,props.tablet.TransformPoint(new Vector3(.02f,.035f,-.036f)),Mathf.Sin(p*Mathf.PI));
        }
        if(line>=24)
        {
            Vector3 carried=ep+home.InverseTransformDirection(elias.forward)*.40f+Vector3.up*1.02f;
            tabletPosition=line==24?Vector3.Lerp(tabletPosition,carried,p):line==30?Vector3.Lerp(carried,desk+new Vector3(.19f,.04f,.05f),Ease(localTime/1.6f)):carried;
            props.tablet.position=World(tabletPosition);props.tablet.rotation=line==30?home.rotation*Quaternion.Euler(Mathf.Lerp(62,90,Ease(localTime/1.6f)),180,5):elias.rotation*Quaternion.Euler(62,0,5);
            float grip=line==30?1-Ease((localTime-.8f)/1.3f):1;
            first.Grip(true,props.tablet.TransformPoint(new Vector3(.23f,-.04f,0)),-props.tablet.right,props.tablet.up,line==29 && localTime<2.5f?0:grip);
            first.Grip(false,props.tablet.TransformPoint(new Vector3(-.23f,-.04f,0)),props.tablet.right,props.tablet.up,grip);
            if(line==24)second.Reach(true,props.tablet.TransformPoint(new Vector3(-.18f,.10f,0)),1-p);
        }
        if(line==29 && localTime<2.5f && door!=null)first.Reach(true,door.hinge.TransformPoint(new Vector3(.35f,1.06f,-.09f)),Ease(localTime/.8f)*(1-Ease((localTime-2.15f)/.35f)));
        props.ShowPhoto(line>=19?1:0);
        Direct(camera,line,p,ep,vp);
        SoundEvents(line,localTime,emove,vmove,change,edistance,vdistance);
        previousLocal=localTime;
    }
    void Direct(Camera camera,int line,float p,Vector3 ep,Vector3 vp)
    {
        Vector3 from,target;float fov=46;
        // All dialogue coverage remains on the east side of the Elias/visitor axis.
        if(line==0){Shot=0;from=Vector3.Lerp(new Vector3(-4.62f,2.65f,4.35f),new Vector3(-4.92f,2.24f,4.04f),p);target=desk+new Vector3(0,.16f,.23f);fov=49;}
        else if(line==1){Shot=1;from=new Vector3(-1.40f,2.32f,.30f)+Vector3.forward*p*.16f;target=new Vector3(-3.13f,1.80f,2.05f);fov=48;}
        // The door is still closed while the visitor answers, so this line is covered from the porch.
        else if(line==3){Shot=11;from=new Vector3(-1.62f,2.12f,.62f)+Vector3.forward*p*.05f;target=vp+new Vector3(0,1.45f,0);fov=44;}
        else if(line==2 || line==4 || line==7 || line==26)
        {Shot=2;from=line<5?new Vector3(-1.80f,2.12f,3.40f):new Vector3(-2.80f,2.10f,1.88f);target=ep+new Vector3(0,1.40f,-.05f);fov=48;from+=Vector3.forward*p*.04f;}
        else if(line==9 || line==19)
        {Shot=3;from=home.InverseTransformPoint(props.tablet.position)+new Vector3(.23f,.70f,.38f);target=home.InverseTransformPoint(props.tablet.position);fov=46;}
        else if(line==14){Shot=4;from=new Vector3(-2.82f,2.12f,1.88f);target=ep+Vector3.up*1.43f;fov=43;}
        else if(line>=10 && line<=12 || line>=15 && line<=18 || line==24 || line==25 || line==5)
        {Shot=5;from=new Vector3(-2.45f,2.30f,3.02f)+new Vector3(-.06f,0,.1f)*p;target=new Vector3(-3.04f,1.94f,2.20f);fov=70;}
        else if(line>=20 && line<=23)
        {Shot=10;from=Vector3.Lerp(new Vector3(-2.80f,2.10f,1.88f),new Vector3(-2.68f,2.14f,1.97f),((line-20)+p)/4f);target=ep+Vector3.up*1.43f;fov=45;}
        else if(line==28){Shot=6;from=Vector3.Lerp(new Vector3(-2.52f,2.17f,2.75f),new Vector3(-2.56f,2.27f,2.35f),p);target=vp+Vector3.up*1.2f;fov=54;}
        else if(line==29){Shot=7;from=new Vector3(-2.60f,2.40f,4.95f)+Vector3.left*p*.35f;target=ep+Vector3.up*1.14f;fov=57;}
        else if(line==30){Shot=8;from=Vector3.Lerp(desk+new Vector3(.7f,1.14f,.67f),desk+new Vector3(.53f,.82f,.53f),p);target=desk+new Vector3(.04f,.02f,.025f);fov=47;}
        else{Shot=9;from=new Vector3(-2.54f,2.29f,2.38f)+Vector3.back*p*.06f;target=vp+new Vector3(0,1.52f,.01f);fov=line==13?37:43;}
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
            float[] times={.2f,.54f,.88f,2.25f};
            while(knockIndex<times.Length && time>=times[knockIndex]){effects.PlayOneShot(knock,knockIndex==3?.8f:.6f);knockIndex++;}
        }
        if(walking && edistance>.06f && playedSounds.Add(line*1000+(int)(edistance/.525f)))eliasSteps.PlayOneShot(footstep,.18f);
        if(visitorWalking && vdistance>.06f && playedSounds.Add(line*1000+500+(int)(vdistance/.525f)))visitorSteps.PlayOneShot(footstep,.16f);
        if(line==5 && previousLocal<.5f && time>=.5f || line==29 && previousLocal<1.05f && time>=1.05f)effects.PlayOneShot(creak,.18f);
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
        foreach(var clip in new[]{knock,footstep,creak,night})if(clip!=null)Destroy(clip);
    }
    void OnDestroy(){Restore();}
}
