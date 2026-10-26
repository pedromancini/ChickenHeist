using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class DeclineCinematicStage : MonoBehaviour
{
    public Transform osvaldo,joana,bento,elias;
    public float bedSurface;
    public static readonly Vector3 TabletOnBed=new Vector3(-.22f,0,-.1f);
    public const int Lines=17;
    public static int SceneOf(int line)=>line<9?1:line<13?2:3;
    public int Scene {get;private set;}
    public string Farm {get;private set;}
    public string Staging {get;private set;}
    public string BedStaging {get;private set;}
    static IEnumerable<Transform> Ancestors(Transform t){for(var x=t;x!=null;x=x.parent)yield return x;}
    public int Shot {get;private set;}=-1;
    public ChickenCoopLockpick Coop=>coop;
    public Light Lantern=>lantern;
    public Transform Tablet=>tablet;
    public Light Headlights=>headlights;
    Transform home;FarmLayoutInfo farm;ChickenCoopLockpick coop;bool coopWasOpen;
    Villager os,jo,be;VisitorCinematicActor el;
    Light lantern,sun,tabletGlow,headlights,moon;Transform lanternBody,tablet;GameObject props;
    AudioSource effects,engine,bed;AudioClip creak,rooster,cans,engineClip,tap,morning;
    readonly List<Light> dimmed=new List<Light>();readonly List<Renderer> hiddenPlayer=new List<Renderer>();
    Color ambient,fogColor;AmbientMode ambientMode;float ambientIntensity,fogDensity;bool fog;FogMode fogMode;Rect viewport;
    Camera eye;int lastLine=-1;float lastTime;
    Vector3 latch,entry,outward,across,gate,inward,gateAcross,bedTop,window,toWindow;
    public static DeclineCinematicStage Create(Transform home)
    {
        var prefab=Resources.Load<GameObject>("Cinematics/DeclineStage");
        if(prefab==null)return null;
        var stage=Instantiate(prefab,home).GetComponent<DeclineCinematicStage>();stage.Initialize(home);return stage;
    }
    void Initialize(Transform h)
    {
        home=h;eye=Camera.main;
        foreach(var c in GetComponentsInChildren<Collider>(true))Destroy(c);
        foreach(var a in GetComponentsInChildren<Animation>(true))a.enabled=false;
        os=new Villager(osvaldo);jo=new Villager(joana);be=new Villager(bento);el=new VisitorCinematicActor(elias);
        var account=HouseholdEconomy.Instance.Account;
        string robbed=account.news!=null && account.news.Count>0?account.news[0].farm:null;
        var farms=FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None).OrderBy(f=>f.layoutIndex).ToArray();
        farm=farms.FirstOrDefault(f=>f.identity==robbed)??farms.FirstOrDefault();Farm=farm!=null?farm.identity:"";
        if(farm!=null)
        {
            var centre=new Vector3(farm.lot.center.x,farm.entrance.y,farm.lot.center.y);
            coop=FindObjectsByType<ChickenCoopLockpick>(FindObjectsSortMode.None).OrderBy(c=>Flat(c.transform.position-centre).sqrMagnitude).FirstOrDefault();
            gate=Ground(farm.entrance);inward=Flat(centre-gate).normalized;gateAcross=Vector3.Cross(Vector3.up,inward);
        }
        if(coop!=null)
        {
            coopWasOpen=coop.IsOpen;coop.SetDoorOpen(true,true);
            latch=coop.InteractionPoint;var leaf=BoundsOf(coop.door!=null?coop.door:coop.transform);
            var normal=leaf.size.x<leaf.size.z?Vector3.right:Vector3.forward;
            var group=coop.transform.parent;
            while(group!=null && group.parent!=null && BoundsOf(group).size.magnitude<leaf.size.magnitude*1.5f)group=group.parent;
            var run=group!=null && BoundsOf(group).size.magnitude<40?BoundsOf(group):new Bounds(new Vector3(farm.lot.center.x,latch.y,farm.lot.center.y),Vector3.one*20);
            run.extents=new Vector3(run.extents.x,50,run.extents.z);
            bool plusInside=run.Contains(latch+normal*1.5f),minusInside=run.Contains(latch-normal*1.5f);
            var faces=new[]{(Vector3.right,run.max.x-latch.x),(Vector3.left,latch.x-run.min.x),(Vector3.forward,run.max.z-latch.z),(Vector3.back,latch.z-run.min.z)};
            var nearest=faces.OrderBy(f=>f.Item2).First();
            outward=nearest.Item1;entry=latch+outward*nearest.Item2;entry.y=latch.y;
            Staging="entry "+entry.ToString("0.00")+" door "+(coop.door!=null?coop.door.name:"-")+" leaf "+leaf.center.ToString("0.00")+" size "+leaf.size.ToString("0.00")+" | group "+(group!=null?group.name:"-")+" bounds "+run.center.ToString("0.00")+" size "+run.size.ToString("0.00")+" | +inside "+plusInside+" -inside "+minusInside+" | parents "+string.Join(" < ",Ancestors(coop.transform).Select(t=>t.name+" "+BoundsOf(t).size.ToString("0.0")));
            across=Vector3.Cross(Vector3.up,outward);
            if(entry==Vector3.zero)entry=latch;
        }
        var bedNode=home.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Prop_Bed_01");
        var bedBox=bedNode!=null?BoundsOf(bedNode):new Bounds(home.TransformPoint(new Vector3(.2f,.9f,5.7f)),new Vector3(1.3f,1,2));
        var onBed=home.TransformPoint(home.InverseTransformPoint(bedBox.center)+TabletOnBed);
        BedStaging="bed "+home.InverseTransformPoint(bedBox.center).ToString("0.00")+" size "+bedBox.size.ToString("0.00");
        bedTop=onBed;bedTop.y=bedSurface>0?home.TransformPoint(new Vector3(0,bedSurface,0)).y:bedBox.min.y+.6f;
        var sleep=FindAnyObjectByType<HomeNextNight>();Vector3 bedAt=bedBox.center;
        var windows=new[]{home.TransformPoint(new Vector3(-5.75f,1.85f,2.465f)),home.TransformPoint(new Vector3(-.65f,1.85f,2.465f))};
        window=windows.OrderBy(w=>(w-bedAt).sqrMagnitude).First();toWindow=Flat(window-bedTop).normalized;
        FarmSecurityProgression.HiddenForStory=true;FarmSecurityProgression.Apply();Props();Sounds();
        var player=FindAnyObjectByType<PlayerMovement>();
        if(player!=null)foreach(var r in player.GetComponentsInChildren<Renderer>(true))if(!r.forceRenderingOff){r.forceRenderingOff=true;hiddenPlayer.Add(r);}
        ambient=RenderSettings.ambientLight;ambientMode=RenderSettings.ambientMode;ambientIntensity=RenderSettings.ambientIntensity;
        fog=RenderSettings.fog;fogColor=RenderSettings.fogColor;fogDensity=RenderSettings.fogDensity;fogMode=RenderSettings.fogMode;
        if(eye!=null){viewport=eye.rect;eye.rect=new Rect(0,.18f,1,.74f);eye.ResetAspect();}
    }
    void Props()
    {
        props=new GameObject("Cena 2 - cenario");props.transform.SetParent(transform,false);
        var feather=Mat(new Color(.58f,.56f,.52f));var rubber=Mat(new Color(.10f,.085f,.07f));var metal=Mat(new Color(.45f,.46f,.44f));
        var wood=Mat(new Color(.36f,.27f,.18f));var tin=Mat(new Color(.62f,.60f,.55f));var plastic=Mat(new Color(.86f,.86f,.84f));
        var random=new System.Random(7);
        for(int i=0;i<16;i++)
        {
            float d=.3f+(float)random.NextDouble()*1.8f,s=((float)random.NextDouble()-.5f)*1.8f;
            var at=Ground(entry+outward*(d-.6f)+across*s)+Vector3.up*.012f;
            Part("Pena",at,new Vector3(.02f,.004f,.07f),feather,Quaternion.Euler(0,(float)random.NextDouble()*360,(float)random.NextDouble()*25));
        }
        var tracks=TrackCentre;
        for(int w=-1;w<=1;w+=2)for(int k=0;k<12;k++)
        {
            var at=Ground(tracks+across*(k*.3f-1.65f)+outward*w*.75f)+Vector3.up*.004f;
            Part("Marca de pneu careca",at,new Vector3(.3f,.006f,.19f),rubber,Quaternion.LookRotation(outward));
        }
        lanternBody=new GameObject("Lampiao").transform;lanternBody.SetParent(props.transform,false);
        Part("Corpo do lampiao",Vector3.zero,new Vector3(.11f,.17f,.11f),metal,Quaternion.identity,lanternBody,true);
        Part("Vidro aceso",Vector3.zero,new Vector3(.08f,.11f,.08f),Glow(new Color(1,.72f,.35f)),Quaternion.identity,lanternBody,true);
        lantern=new GameObject("Luz do lampiao").AddComponent<Light>();lantern.transform.SetParent(lanternBody,false);
        lantern.type=LightType.Point;lantern.color=new Color(1,.70f,.38f);lantern.range=7;lantern.intensity=2.4f;lantern.shadows=LightShadows.Soft;
        var pole=gate+gateAcross*2.4f-inward*.3f;
        Part("Poste novo",pole+Vector3.up*1.4f,new Vector3(.12f,2.8f,.12f),wood,Quaternion.identity);
        var cam=Part("Camera nova",pole+Vector3.up*2.55f-inward*.18f,new Vector3(.14f,.12f,.26f),plastic,Quaternion.LookRotation(-inward-gateAcross*.4f));
        Part("Lente",cam.transform.position+cam.transform.forward*.15f,new Vector3(.07f,.07f,.05f),rubber,cam.transform.rotation);
        var ladderFoot=Ground(pole+inward*.75f+gateAcross*.15f);var ladderTop=pole+Vector3.up*2.35f+inward*.08f;
        var up=(ladderTop-ladderFoot).normalized;
        for(int r=-1;r<=1;r+=2)Part("Banzo da escada",(ladderFoot+ladderTop)*.5f+gateAcross*r*.2f,new Vector3(.05f,(ladderTop-ladderFoot).magnitude,.05f),wood,Quaternion.FromToRotation(Vector3.up,up));
        for(int k=1;k<8;k++)Part("Degrau",Vector3.Lerp(ladderFoot,ladderTop,k/8f),new Vector3(.035f,.035f,.42f),wood,Quaternion.LookRotation(gateAcross,up));
        var a=Ground(gate-gateAcross*1.7f)+Vector3.up*.45f;var b=Ground(gate+gateAcross*1.7f)+Vector3.up*.45f;
        Part("Arame novo",(a+b)*.5f,new Vector3(.008f,.008f,(b-a).magnitude),metal,Quaternion.LookRotation(b-a));
        for(int k=1;k<7;k++){var at=Vector3.Lerp(a,b,k/7f)+Vector3.down*.07f;Part("Lata no arame",at,new Vector3(.07f,.1f,.07f),tin,Quaternion.Euler(0,k*40,k%2*8));}
        tablet=new GameObject("Tablet na cama").transform;tablet.SetParent(props.transform,false);
        tablet.position=bedTop+Vector3.up*.012f;tablet.rotation=home.rotation*Quaternion.Euler(0,-90,0);
        Part("Tablet",Vector3.zero,new Vector3(.19f,.012f,.28f),rubber,Quaternion.identity,tablet,true);
        var screen=Part("Tela acesa",Vector3.up*.007f,new Vector3(.17f,.002f,.25f),Glow(new Color(.55f,.78f,1)),Quaternion.identity,tablet,true);
        var shown=screen.GetComponent<Renderer>().sharedMaterial;shown.SetTexture("_BaseMap",Message());shown.SetTexture("_EmissionMap",shown.GetTexture("_BaseMap"));shown.SetColor("_EmissionColor",Color.white*1.4f);shown.SetColor("_BaseColor",Color.white);
        tabletGlow=new GameObject("Luz da tela").AddComponent<Light>();tabletGlow.transform.SetParent(tablet,false);tabletGlow.transform.localPosition=Vector3.up*.4f;
        tabletGlow.type=LightType.Point;tabletGlow.color=new Color(.55f,.75f,1);tabletGlow.range=3.4f;tabletGlow.intensity=2.2f;
        moon=new GameObject("Luar pela veneziana").AddComponent<Light>();moon.transform.SetParent(props.transform,false);
        moon.type=LightType.Point;moon.color=new Color(.45f,.55f,.85f);moon.range=6;moon.intensity=0;moon.transform.position=window+Vector3.Cross(Vector3.up,toWindow)*.0f-toWindow*.6f+Vector3.down*.2f;
        headlights=new GameObject("Farois passando").AddComponent<Light>();headlights.transform.SetParent(props.transform,false);
        headlights.type=LightType.Spot;headlights.color=new Color(1,.93f,.78f);headlights.range=10;headlights.spotAngle=38;headlights.intensity=0;headlights.shadows=LightShadows.Soft;
        headlights.cookie=Slats();
        sun=new GameObject("Sol da manha").AddComponent<Light>();sun.transform.SetParent(props.transform,false);
        sun.type=LightType.Directional;sun.color=new Color(1,.78f,.55f);sun.intensity=0;sun.shadows=LightShadows.Soft;
        sun.transform.rotation=Quaternion.LookRotation(Vector3.down*.32f+inward+gateAcross*.6f);
    }
    Vector3 TrackCentre=>entry+outward*3.4f+across*2.4f;
    const float Floor=.63f;
    Texture2D Message()
    {
        var t=new Texture2D(96,140,TextureFormat.RGBA32,false){filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp};
        var px=new Color32[96*140];for(int i=0;i<px.Length;i++)px[i]=new Color32(18,26,40,255);
        void Box(int x0,int y0,int x1,int y1,Color32 c){for(int y=y0;y<y1;y++)for(int x=x0;x<x1;x++)px[y*96+x]=c;}
        Box(0,124,96,140,new Color32(40,70,110,255));Box(8,128,40,134,new Color32(200,220,240,255));
        Box(8,70,80,108,new Color32(210,225,240,255));
        for(int l=0;l<3;l++)Box(14,98-l*10,l==2?52:72,102-l*10,new Color32(40,55,75,255));
        Box(8,60,30,64,new Color32(120,140,170,255));
        t.SetPixels32(px);t.Apply();textures.Add(t);return t;
    }
    readonly List<Texture2D> textures=new List<Texture2D>();
    Texture2D Slats()
    {
        var t=new Texture2D(64,64,TextureFormat.RGBA32,false){wrapMode=TextureWrapMode.Clamp};var px=new Color[64*64];
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            float band=Mathf.Repeat(y/8f,1)<.42f?1:0;float edge=Mathf.Clamp01(Mathf.Min(Mathf.Min(x,63-x),Mathf.Min(y,63-y))/10f);
            float v=band*edge;px[y*64+x]=new Color(v,v,v,v);
        }
        t.SetPixels(px);t.Apply();textures.Add(t);return t;
    }
    void Sounds()
    {
        effects=Source("Efeitos da cena",1);engine=Source("Motor passando",1);bed=Source("Fundo da cena",0);bed.loop=true;
        creak=SoundLibrary.Load("door_creak");rooster=SoundLibrary.Load("rooster_morning");cans=SoundLibrary.Load("wire_rattle");
        engineClip=SoundLibrary.Load("engine");tap=SoundLibrary.Load("latch_click");morning=SoundLibrary.Load("day_ambience");
        engine.clip=engineClip;engine.loop=true;engine.minDistance=4;engine.maxDistance=60;
    }
    AudioSource Source(string label,float spatial)
    {
        var node=new GameObject(label);node.transform.SetParent(transform,false);var s=node.AddComponent<AudioSource>();
        s.playOnAwake=false;s.spatialBlend=spatial;s.minDistance=2;s.maxDistance=40;s.rolloffMode=AudioRolloffMode.Linear;return s;
    }
    public void Evaluate(Camera camera,int line,float time,float duration,float total)
    {
        eye=camera;int scene=SceneOf(line);
        if(scene!=Scene){Scene=scene;ApplyLight(scene);}
        bool fresh=line!=lastLine;float step=fresh?1:Mathf.Clamp(time-lastTime,0,.25f);lastLine=line;lastTime=time;float p=Mathf.Clamp01(time/Mathf.Max(.01f,duration));Villager.Step=step;
        string said=line<StoryDirector.Instance.LineCount?StoryDirector.Instance.LineText(line):"";
        string who=StoryDialogue.declineSpeakers[Mathf.Min(line,StoryDialogue.declineSpeakers.Length-1)];
        float mouth=VisitorCinematicActor.Spoken(said,time,duration);
        osvaldo.gameObject.SetActive(scene==2 || scene==1 && line>=2);joana.gameObject.SetActive(scene<3 && line>=1);bento.gameObject.SetActive(scene==2);elias.gameObject.SetActive(scene==3);
        lanternBody.gameObject.SetActive(scene==1);
        Vector3 from,target;float fov;
        if(scene==1)
        {
            var ground=Ground(entry);
            Vector3 osAt=Ground(latch+outward*.85f+across*.35f),joFrom=Ground(entry+outward*6f+across*2.2f),joAt=Ground(entry+outward*.4f-across*.7f);
            if(line>=6){osAt=Ground(TrackCentre-across*.6f-outward*.15f);joAt=Ground(TrackCentre-across*1.3f-outward*1.1f);}
            else if(line>=3){osAt=Ground(entry+outward*.3f+across*.55f);}
            float walk=line==1?Ease(Mathf.Clamp01(time/(duration*.75f))):1;var jo0=line==0?joFrom:line==1?Vector3.Lerp(joFrom,joAt,walk):joAt;
            Vector3 osLook=line<=2?latch-Vector3.up*.25f:line==4?latch-outward*1.2f+Vector3.up*.4f:line==6?Ground(TrackCentre):line==8?gate+Vector3.up*1.5f:jo.Head;
            Vector3 joLook=line<=1?latch:line==6?Ground(TrackCentre):os.Head;
            float osYaw=line<=2?Yaw(osLook-osAt):line==6?Yaw(Ground(TrackCentre)-osAt):Yaw(jo.Head-osAt);
            os.Pose(osAt,osYaw,total,who=="Osvaldo",osLook,who=="Osvaldo"?mouth:0,line==6?18:0);
            jo.Pose(jo0,line==1 && walk<1?Yaw(joAt-joFrom):Yaw(joLook-jo0),total+1.1f,who=="Joana",joLook,who=="Joana"?mouth:0,0,line==1 && walk<1,Vector3.Distance(joFrom,jo0));
            var hand=jo.Hand;lanternBody.position=line==6?Ground(TrackCentre)+Vector3.up*.45f-across*.4f:hand+Vector3.down*.16f;lanternBody.rotation=Quaternion.identity;
            if(line==0){lanternBody.position=entry+outward*7+Vector3.up*50;}
            if(fresh && line<=2 && creak!=null){effects.transform.position=latch;effects.PlayOneShot(creak,.55f);}
            switch(line)
            {
                case 0:from=ground+outward*3.4f+across*2.2f+Vector3.up*(1.05f+p*.12f);target=Vector3.Lerp(ground,latch,.55f)+Vector3.up*.2f;fov=50;break;
                case 1:from=ground+outward*7f-across*3.6f+Vector3.up*2.1f;target=Vector3.Lerp(entry,jo0,.5f)+Vector3.up*.4f;fov=48;break;
                case 2:from=jo.Head+outward*.9f-across*.55f+Vector3.up*.05f;target=Vector3.Lerp(latch,os.Head,.6f)-Vector3.up*.15f;fov=44;break;
                case 6:{var rut=Ground(TrackCentre);from=rut-across*.2f+outward*1.9f+Vector3.up*.55f;target=Vector3.Lerp(rut+Vector3.up*.05f,os.Head,.5f);fov=50;break;}
                case 5:{var mid=(os.Head+jo.Head)*.5f;var axis=Flat(jo.Head-os.Head).normalized;var side=Vector3.Cross(Vector3.up,axis);if(Vector3.Dot(side,outward)<0)side=-side;from=mid+side*3.0f+Vector3.down*.15f;target=mid-Vector3.up*.25f;fov=40;break;}
                default:{bool joana=who=="Joana";CloseUp(joana?jo:os,joana?os:jo,outward,p,out from,out target);fov=34;break;}
            }
        }
        else if(scene==2)
        {
            var pole=gate+gateAcross*2.4f-inward*.3f;
            Vector3 beAt=Ground(pole+inward*1.15f-gateAcross*.15f),osAt=Ground(gate+inward*2.4f-gateAcross*.4f),joAt=Ground(gate+inward*2.5f+gateAcross*.45f);
            be.Pose(beAt,Yaw(line==9?pole-beAt:os.Head-beAt),total,who=="Bento",line==9?pole+Vector3.up*2.5f:os.Head,who=="Bento"?mouth:0);
            os.Pose(osAt,Yaw(be.Head-osAt),total,who=="Osvaldo",line==12?gate+Vector3.up*1.4f-inward*6:be.Head,who=="Osvaldo"?mouth:0);
            jo.Pose(joAt,Yaw(line==11?os.Head-joAt:be.Head-joAt),total+1.1f,who=="Joana",line==11?os.Head:be.Head,who=="Joana"?mouth:0);
            if(fresh && line==9){if(rooster!=null){effects.transform.position=gate+inward*25+gateAcross*10;effects.PlayOneShot(rooster,.6f);}}
            if(fresh && line==10 && cans!=null){effects.transform.position=gate;effects.PlayOneShot(cans,.45f);}
            switch(line)
            {
                case 9:from=gate-inward*4.6f+gateAcross*4.2f+Vector3.up*(1.3f+p*1.1f);target=pole+Vector3.up*(1.2f+p*.6f)+inward*.8f;fov=48;break;
                case 10:from=be.Head+Flat(osAt-beAt).normalized*1.6f-gateAcross*.55f+Vector3.down*.05f;target=be.Head+Vector3.down*.18f;fov=40;break;
                default:{bool joana=who=="Joana";CloseUp(joana?jo:os,be,joana?gateAcross:-gateAcross,p,out from,out target);fov=34;break;}
            }
        }
        else
        {
            var tabLocal=home.InverseTransformPoint(tablet.position);var stand=home.TransformPoint(new Vector3(tabLocal.x-.95f,Floor,tabLocal.z-.15f));
            float drive=line==15?Mathf.Clamp01((time-.6f)/(duration-1.2f)):line>15?1:0;
            float turn=line==15?Ease(Mathf.Clamp01(drive*2.2f)):line>15?1:0;
            var facing=Flat(Vector3.Lerp(tablet.position-stand,window-stand,line>=15?.75f:0)).normalized;
            el.Pose(transform.InverseTransformPoint(stand),Yaw(facing)-transform.eulerAngles.y,total,false,who=="Elias",Vector3.Lerp(tablet.position,window,turn));
            el.Speak(who=="Elias"?mouth:0,fresh);
            bool down=line>=15 || (line==14 && p>.75f);
            if(line==14)el.Flat(true,tablet.position+Vector3.up*.012f,Vector3.down,Flat(tablet.position-stand).normalized,Ease(Mathf.Clamp01((p-.55f)/.2f))*(1-Ease(Mathf.Clamp01((p-.85f)/.15f))));
            el.Twist();
            tablet.localRotation=Quaternion.identity;tablet.rotation=Quaternion.LookRotation(Vector3.Cross(Vector3.up,toWindow),Vector3.up)*(down?Quaternion.Euler(0,0,180):Quaternion.identity);
            if(down)tablet.position=bedTop+Vector3.up*.012f+toWindow*.25f;
            tabletGlow.intensity=down?0:2.2f;
            var road=window+Flat(window-home.TransformPoint(new Vector3(-3,0,6))).normalized*6;var along=Vector3.Cross(Vector3.up,toWindow);
            var car=road+along*Mathf.Lerp(-14,14,drive)+Vector3.down*.6f;
            var face=elias.GetComponentsInChildren<Transform>().First(t=>t.name=="Head").position;
            headlights.transform.position=window-toWindow*.25f;
            headlights.transform.rotation=Quaternion.LookRotation((face+along*Mathf.Lerp(1.4f,-1.4f,drive)+Vector3.down*.1f-headlights.transform.position).normalized);
            headlights.spotAngle=28;headlights.range=7;headlights.intensity=line==15?Mathf.Sin(drive*Mathf.PI)*6:0;
            engine.transform.position=car;
            if(line==15 && engineClip!=null){if(!engine.isPlaying)engine.Play();engine.volume=Mathf.Sin(drive*Mathf.PI)*.55f*GameAudioMix.Effects;engine.pitch=.75f;}
            else if(engine.isPlaying)engine.Stop();
            if(fresh && line==15 && tap!=null){effects.transform.position=tablet.position;effects.PlayOneShot(tap,.5f);}
            var head=elias.GetComponentsInChildren<Transform>().First(t=>t.name=="Head").position;var side=Vector3.Cross(Vector3.up,toWindow);
            Vector3 H(float x,float y,float z)=>home.TransformPoint(new Vector3(x,y,z));
            var tab=home.InverseTransformPoint(tablet.position);
            switch(line)
            {
                case 13:from=H(tab.x-.75f,tab.y+.55f,tab.z-.7f);target=tablet.position+Vector3.up*.02f;fov=36;break;
                case 14:from=H(tab.x-.1f,1.55f,tab.z-1.75f);target=Vector3.Lerp(tablet.position,head,.65f);fov=46;break;
                default:
                {
                    var look=Flat(window-head).normalized;var cheek=Vector3.Cross(Vector3.up,look);if(Vector3.Dot(cheek,home.TransformDirection(Vector3.left))<0)cheek=-cheek;
                    float near=line==15?Mathf.Lerp(1.05f,.85f,p):.8f;
                    from=head+(look*.55f+cheek*.85f).normalized*near+Vector3.up*.06f;target=head+Vector3.up*.08f+look*.05f;fov=30;break;
                }
            }
            from=InsideRoom(from);
            if(line==16){RenderSettings.ambientLight=Color.Lerp(new Color(.07f,.08f,.11f),Color.black,Ease(p));moon.intensity=.9f*(1-Ease(p));}
        }
        Shot=line;
        if(who=="Joana" || who=="Osvaldo" || who=="Bento" || who=="Elias")
        {
            var face=HeadOf(who);
            foreach(float swing in new[]{0f,15,-15,30,-30,45,-45,60,-60})
            {
                var turned=target+Quaternion.AngleAxis(swing,Vector3.up)*(from-target);
                if(scene==3)turned=InsideRoom(turned);
                if(!Physics.Linecast(turned,face,~0,QueryTriggerInteraction.Ignore)){from=turned;break;}
            }
        }
        camera.transform.SetPositionAndRotation(from,Quaternion.LookRotation(target-from,Vector3.up));camera.fieldOfView=fov;
        if(bed.isPlaying)bed.volume=(Scene==2?.35f:.0f)*GameAudioMix.Ambience;
    }
    void CloseUp(Villager speaker,Villager listener,Vector3 sideHint,float p,out Vector3 from,out Vector3 target)
    {
        var axis=Flat(listener.Head-speaker.Head).normalized;var side=Vector3.Cross(Vector3.up,axis);if(Vector3.Dot(side,sideHint)<0)side=-side;
        from=speaker.Head+axis*(1.6f-p*.08f)+side*.98f+Vector3.down*.03f;target=speaker.Head+Vector3.down*.07f+side*.08f;
    }
    void ApplyLight(int scene)
    {
        foreach(var l in dimmed)if(l!=null)l.enabled=true;dimmed.Clear();
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;
        lantern.enabled=scene==1;sun.intensity=scene==2?1.25f:0;tabletGlow.enabled=scene==3;headlights.enabled=scene==3;moon.intensity=scene==3?.9f:0;
        bed.Stop();
        if(scene==1)
        {
            RenderSettings.ambientLight=new Color(.10f,.13f,.21f);RenderSettings.fogColor=new Color(.20f,.25f,.33f);RenderSettings.fogDensity=.045f;
        }
        else if(scene==2)
        {
            RenderSettings.ambientLight=new Color(.44f,.42f,.40f);RenderSettings.fogColor=new Color(.78f,.66f,.54f);RenderSettings.fogDensity=.014f;
            foreach(var l in FindObjectsByType<Light>(FindObjectsSortMode.None))if(l.enabled && l!=sun && l.type==LightType.Directional && !l.transform.IsChildOf(transform)){l.enabled=false;dimmed.Add(l);}
            if(morning!=null){bed.clip=morning;bed.Play();}
        }
        else
        {
            RenderSettings.ambientLight=new Color(.07f,.08f,.11f);RenderSettings.fogColor=new Color(.02f,.025f,.035f);RenderSettings.fogDensity=.01f;
            foreach(var l in home.GetComponentsInChildren<Light>())if(l.enabled && !l.transform.IsChildOf(transform)){l.enabled=false;dimmed.Add(l);}
        }
    }
    public void Release()
    {
        foreach(var l in dimmed)if(l!=null)l.enabled=true;dimmed.Clear();
        foreach(var r in hiddenPlayer)if(r!=null)r.forceRenderingOff=false;hiddenPlayer.Clear();FarmSecurityProgression.HiddenForStory=false;FarmSecurityProgression.Apply();
        RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=ambientMode;RenderSettings.ambientIntensity=ambientIntensity;
        RenderSettings.fog=fog;RenderSettings.fogColor=fogColor;RenderSettings.fogDensity=fogDensity;RenderSettings.fogMode=fogMode;
        if(eye!=null){eye.rect=viewport;eye.ResetAspect();}
        if(coop!=null && !coopWasOpen)coop.SetDoorOpen(false,true);coop=null;
        gameObject.SetActive(false);Destroy(gameObject);
    }
    Vector3 InsideRoom(Vector3 at){var l=home.InverseTransformPoint(at);l.x=Mathf.Clamp(l.x,-7.3f,.85f);l.z=Mathf.Clamp(l.z,2.55f,6.75f);l.y=Mathf.Clamp(l.y,.9f,2.6f);return home.TransformPoint(l);}
    public static void OverShoulder(Camera camera,Transform listenerHead,Transform speakerHead,Vector3 preferredSide,float dolly)
    {
        Vector3 axis=speakerHead.position-listenerHead.position;axis.y=0;axis.Normalize();
        Vector3 side=Vector3.Cross(Vector3.up,axis);
        Vector3 middle=(speakerHead.position+listenerHead.position)*.5f;
        if(Vector3.Dot(side,preferredSide-middle)<0)side=-side;
        Vector3 from=listenerHead.position-axis*(1.45f-dolly*.15f)+side*.85f+Vector3.up*.05f;
        camera.transform.position=from;camera.transform.LookAt(speakerHead.position-Vector3.up*.1f);camera.fieldOfView=36;
    }
    static float Ease(float x){x=Mathf.Clamp01(x);return x*x*(3-2*x);}
    static Vector3 Flat(Vector3 v){v.y=0;return v;}
    static float Yaw(Vector3 v)=>Mathf.Atan2(v.x,v.z)*Mathf.Rad2Deg;
    Vector3 Ground(Vector3 p)
    {
        var hits=Physics.RaycastAll(p+Vector3.up*3,Vector3.down,8,~0,QueryTriggerInteraction.Ignore);float best=float.NegativeInfinity;Vector3 at=p;
        foreach(var h in hits)if(h.point.y<p.y+.9f && h.point.y>best && Vector3.Dot(h.normal,Vector3.up)>.6f && !h.transform.IsChildOf(transform) && (HeistGameManager.Instance==null || !h.transform.IsChildOf(HeistGameManager.Instance.player))){best=h.point.y;at=h.point;}
        return at;
    }
    static Bounds BoundsOf(Transform t)
    {
        var rs=t.GetComponentsInChildren<Renderer>();if(rs.Length==0)return new Bounds(t.position,Vector3.one);
        var b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);return b;
    }
    GameObject Part(string name,Vector3 at,Vector3 size,Material material,Quaternion rotation,Transform parent=null,bool local=false)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent!=null?parent:props.transform,false);
        if(local){go.transform.localPosition=at;go.transform.localRotation=rotation;}else go.transform.SetPositionAndRotation(at,rotation);
        go.transform.localScale=size;var r=go.GetComponent<Renderer>();r.sharedMaterial=material;return go;
    }
    readonly List<Material> materials=new List<Material>();
    Material Mat(Color c){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.12f);materials.Add(m);return m;}
    Material Glow(Color c){var m=Mat(c);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*2.2f);return m;}
    void OnDestroy(){foreach(var m in materials)if(m!=null)Destroy(m);foreach(var t in textures)if(t!=null)Destroy(t);}

    sealed class Villager
    {
        readonly Transform root,head,hand;readonly GameObject animated;readonly AnimationClip idle,walk,talk;
        readonly List<(SkinnedMeshRenderer skin,int index)> jaws=new List<(SkinnedMeshRenderer,int)>();float mouth;
        public Vector3 Head=>head.position;public Vector3 Hand=>hand!=null?hand.position:head.position+Vector3.down*.9f;
        public float JawWeight=>jaws.Count>0?jaws[0].skin.GetBlendShapeWeight(jaws[0].index):0;
        public static float Step;
        public Villager(Transform actor)
        {
            root=actor;var anim=actor.GetComponentInChildren<Animation>();animated=anim!=null?anim.gameObject:actor.gameObject;
            idle=anim?.GetClip("Idle");walk=anim?.GetClip("Walk");talk=anim?.GetClip("Trade");
            var all=actor.GetComponentsInChildren<Transform>(true);head=all.First(t=>t.name=="Head");hand=all.FirstOrDefault(t=>t.name=="Hand_R" || t.name=="HandR");
            foreach(var s in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)){int k=s.sharedMesh!=null?s.sharedMesh.GetBlendShapeIndex("Fala"):-1;if(k>=0)jaws.Add((s,k));}
        }
        public void Pose(Vector3 at,float yaw,float time,bool speaking,Vector3 gaze,float open,float bow=0,bool walking=false,float distance=0)
        {
            var clip=walking && walk!=null?walk:speaking && talk!=null?talk:idle;
            if(clip!=null)clip.SampleAnimation(animated,walking?Mathf.Repeat(distance/1.3f,1)*clip.length:Mathf.Repeat(time,Mathf.Max(.1f,clip.length)));
            root.SetPositionAndRotation(at,Quaternion.Euler(0,yaw,0));
            var local=root.InverseTransformDirection(gaze-head.position);
            float turn=Mathf.Clamp(Mathf.Atan2(local.x,local.z)*Mathf.Rad2Deg,-35,35),pitch=Mathf.Clamp(-Mathf.Atan2(local.y,new Vector2(local.x,local.z).magnitude)*Mathf.Rad2Deg+bow,-20,30);
            head.rotation=Quaternion.AngleAxis(turn*.8f,root.up)*Quaternion.AngleAxis(pitch*.7f,root.right)*head.rotation;
            mouth=Mathf.Lerp(mouth,Mathf.Clamp01(open),1-Mathf.Exp(-Step*22));
            foreach(var (skin,index) in jaws)skin.SetBlendShapeWeight(index,mouth*100);
        }
    }
    public float JawOf(string who)
    {
        var actor=who=="Osvaldo"?osvaldo:who=="Joana"?joana:who=="Bento"?bento:who=="Elias"?elias:null;
        if(actor==null || !actor.gameObject.activeInHierarchy)return 0;
        return who=="Osvaldo"?os.JawWeight:who=="Joana"?jo.JawWeight:who=="Bento"?be.JawWeight:el.JawWeight;
    }
    public Vector3 HeadOf(string who)=>who=="Osvaldo"?os.Head:who=="Joana"?jo.Head:who=="Bento"?be.Head:elias.GetComponentsInChildren<Transform>().First(t=>t.name=="Head").position;
}
