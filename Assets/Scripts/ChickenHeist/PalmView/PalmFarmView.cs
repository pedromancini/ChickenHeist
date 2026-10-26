using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class PalmFarmView:MonoBehaviour
{
    public const int Layer=30;
    const float WebcamFov=62,Lift=.012f,Spread=1.5f,FingerRadius=.0075f,FingerDepth=.007f;
    static readonly int[,] Bones={{1,2},{2,3},{3,4},{5,6},{6,7},{7,8},{9,10},{10,11},{11,12},{13,14},{14,15},{15,16},{17,18},{18,19},{19,20}};
    public static PalmFarmView Current{get;private set;}
    public static bool Active=>Current!=null;
    public static int ClosedFrame{get;private set;}=-1;
    public static Texture2D StillImage;
    public static bool SkipCamera;

    public int Farm{get;private set;}
    public ARFarmModel Model=>model;
    public PalmTracker Tracker=>tracker;
    public bool Showing=>modelRoot!=null && modelRoot.gameObject.activeSelf && shown>.99f;
    public bool FreeView=>free;
    public Camera View=>view;
    public Rect Controls=>controls;

    ARFarmModel[] prefabs;string[] names;ARFarmModel model;Transform modelRoot;
    Camera view;Transform backdrop;Material backdropMaterial,occluderMaterial;Light sun;
    WebCamTexture webcam;Texture2D still;Color32[] pixels;bool mirror,flip,free,denied,starting=true,restored;
    PalmTracker tracker;
    readonly Transform[] occluders=new Transform[15];
    Vector3 position;Quaternion rotation=Quaternion.identity;float size,shown,lostFor,yaw=-30,tilt=28;bool hasPose;Vector2 lastMouse;
    readonly List<(Light light,bool on)> lights=new List<(Light,bool)>();
    readonly List<(Camera camera,bool on)> cameras=new List<(Camera,bool)>();
    readonly List<Material> materials=new List<Material>();
    AmbientMode ambientMode;Color ambientLight;bool fog;Light oldSun;UniversalRenderPipelineAsset pipeline;float shadowDistance;
    readonly Dictionary<string,bool> layers=new Dictionary<string,bool>{{"Ronda",true},{"Galinhas",true},{"Entrada",true},{"Cameras",true}};
    Rect controls;

    public static void Open(int farm)
    {
        if(Current!=null)return;
        Current=new GameObject("Fazenda na palma da mao").AddComponent<PalmFarmView>();
        Current.Begin(farm);
    }
    public static void Close()
    {
        var view=Current;if(view==null)return;
        Current=null;ClosedFrame=Time.frameCount;view.Restore();Destroy(view.gameObject);
    }

    void Begin(int farm)
    {
        transform.position=new Vector3(0,-2000,0);
        prefabs=Resources.LoadAll<ARFarmModel>("PalmFarms").OrderBy(p=>p.name).ToArray();
        names=ProtagonistPhone.Instance!=null && ProtagonistPhone.Instance.farmNames!=null && ProtagonistPhone.Instance.farmNames.Length>0
            ?ProtagonistPhone.Instance.farmNames:prefabs.Select(p=>p.farmName+" - "+p.kind).ToArray();
        foreach(var l in FindObjectsByType<Light>(FindObjectsSortMode.None)){lights.Add((l,l.enabled));l.enabled=false;}
        foreach(var c in Camera.allCameras){cameras.Add((c,c.enabled));c.enabled=false;}
        ambientMode=RenderSettings.ambientMode;ambientLight=RenderSettings.ambientLight;fog=RenderSettings.fog;oldSun=RenderSettings.sun;
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.50f,.50f,.54f);RenderSettings.fog=false;
        pipeline=GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if(pipeline!=null){shadowDistance=pipeline.shadowDistance;pipeline.shadowDistance=1.5f;}

        view=new GameObject("Camera da visualizacao").AddComponent<Camera>();view.transform.SetParent(transform,false);
        view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.10f,.095f,.085f);view.cullingMask=1<<Layer;
        view.nearClipPlane=.01f;view.farClipPlane=30;view.depth=60;view.fieldOfView=40;
        var data=view.GetUniversalAdditionalCameraData();data.renderPostProcessing=false;data.renderShadows=true;
        sun=new GameObject("Luz da visualizacao").AddComponent<Light>();sun.transform.SetParent(view.transform,false);
        sun.type=LightType.Directional;sun.transform.localRotation=Quaternion.Euler(48,-32,0);sun.intensity=1.3f;sun.color=new Color(1,.96f,.9f);
        sun.shadows=LightShadows.Soft;sun.cullingMask=1<<Layer;RenderSettings.sun=sun;

        backdrop=GameObject.CreatePrimitive(PrimitiveType.Quad).transform;Destroy(backdrop.GetComponent<Collider>());
        backdrop.name="Imagem da camera";backdrop.SetParent(view.transform,false);backdrop.gameObject.layer=Layer;backdrop.gameObject.SetActive(false);
        backdropMaterial=Keep(new Material(Resources.Load<Shader>("HandTracking/PalmViewBackground")));
        var br=backdrop.GetComponent<MeshRenderer>();br.sharedMaterial=backdropMaterial;br.shadowCastingMode=ShadowCastingMode.Off;br.receiveShadows=false;
        occluderMaterial=Keep(new Material(Resources.Load<Shader>("HandTracking/PalmViewOccluder")));
        for(int i=0;i<occluders.Length;i++)
        {
            var o=GameObject.CreatePrimitive(PrimitiveType.Capsule);Destroy(o.GetComponent<Collider>());o.name="Dedo";o.layer=Layer;
            o.transform.SetParent(view.transform,false);var r=o.GetComponent<MeshRenderer>();r.sharedMaterial=occluderMaterial;r.shadowCastingMode=ShadowCastingMode.Off;
            o.SetActive(false);occluders[i]=o.transform;
        }
        tracker=new PalmTracker();
        LoadModel(farm);
    }

    Material Keep(Material m){materials.Add(m);return m;}

    IEnumerator Start()
    {
        if(StillImage!=null)
        {
            still=StillImage;pixels=still.GetPixels32();Show(still,false,false);starting=false;yield break;
        }
        if(SkipCamera){free=true;starting=false;yield break;}
        if(!Application.HasUserAuthorization(UserAuthorization.WebCam))yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        var devices=WebCamTexture.devices;
        if(devices.Length==0 || !Application.HasUserAuthorization(UserAuthorization.WebCam)){denied=devices.Length>0;free=true;starting=false;yield break;}
        webcam=new WebCamTexture(devices[0].name,1280,720,30);webcam.Play();
        for(float wait=0;webcam.width<32 && wait<8;wait+=Time.unscaledDeltaTime)yield return null;
        starting=false;
        if(webcam.width<32){webcam.Stop();free=true;yield break;}
        mirror=true;flip=webcam.videoVerticallyMirrored;
        Show(webcam,mirror,flip);
    }

    void Show(Texture image,bool mirrored,bool flipped)
    {
        backdropMaterial.mainTexture=image;
        backdropMaterial.mainTextureScale=new Vector2(mirrored?-1:1,flipped?-1:1);
        backdropMaterial.mainTextureOffset=new Vector2(mirrored?1:0,flipped?1:0);
        backdropMaterial.SetFloat("_Linearize",QualitySettings.activeColorSpace==ColorSpace.Linear && !image.isDataSRGB?1:0);
        backdrop.gameObject.SetActive(true);
    }

    void LoadModel(int farm)
    {
        if(modelRoot!=null)Destroy(modelRoot.gameObject);
        int count=names.Length;Farm=(farm%count+count)%count;
        var prefab=prefabs.FirstOrDefault(p=>p.farmName+" - "+p.kind==names[Farm])??prefabs[Mathf.Min(Farm,prefabs.Length-1)];
        model=Instantiate(prefab,transform);modelRoot=model.transform;
        foreach(var t in modelRoot.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;
        AddTraps(names[Farm]);
        foreach(var t in model.cones.GetComponentsInChildren<Transform>(true))t.gameObject.layer=Layer;
        foreach(var kv in layers)model.Show(kv.Key,kv.Value);
        if(!FarmSecurityProgression.Visible)model.cones.gameObject.SetActive(false);
        shown=0;modelRoot.localScale=Vector3.zero;
        if(ProtagonistPhone.Instance!=null)ProtagonistPhone.Instance.PhotoIndex=Farm;
    }

    void AddTraps(string identity)
    {
        var farm=FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None).FirstOrDefault(f=>f.identity==identity);
        if(farm==null)return;
        var wood=Keep(SecurityEquipmentVisual.Material("Estaca da maquete",new Color(.34f,.25f,.16f)));
        var wire=Keep(SecurityEquipmentVisual.Material("Arame da maquete",new Color(.80f,.80f,.76f)));
        var tin=Keep(SecurityEquipmentVisual.Material("Lata da maquete",new Color(.62f,.30f,.18f)));
        foreach(var trap in farm.GetComponentsInChildren<TrapSystem>(true))
        {
            if(trap.Span<.2f)continue;
            Vector3 a=trap.StakeLeft-model.origin,b=trap.StakeRight-model.origin;
            var group=new GameObject("Arame com latas").transform;group.SetParent(model.cones,false);
            foreach(var p in new[]{a,b})SecurityEquipmentVisual.Part(group,"Estaca",PrimitiveType.Cube,p+Vector3.up*.9f,new Vector3(.5f,1.8f,.5f),wood);
            SecurityEquipmentVisual.Rod(group,"Arame",a+Vector3.up*1.2f,b+Vector3.up*1.2f,.25f,wire);
            for(int i=1;i<=3;i++)SecurityEquipmentVisual.Part(group,"Lata",PrimitiveType.Cylinder,Vector3.Lerp(a,b,i/4f)+Vector3.up*.75f,new Vector3(.55f,.4f,.55f),tin);
        }
    }

    void Update()
    {
        if(Current!=this)return;
        if(Input.GetKeyDown(KeyCode.Escape)){Close();return;}
        if(Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow))LoadModel(Farm+1);
        if(Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow))LoadModel(Farm-1);
        int w=0,h=0;
        if(webcam!=null && webcam.isPlaying && webcam.width>=32)
        {
            w=webcam.width;h=webcam.height;
            if(webcam.didUpdateThisFrame)
            {
                if(pixels==null || pixels.Length!=w*h)pixels=new Color32[w*h];
                webcam.GetPixels32(pixels);tracker.Process(pixels,w,h,mirror,flip);
            }
        }
        else if(still!=null){w=still.width;h=still.height;tracker.Process(pixels,w,h,false,false);}
        Frame(w,h);
        if(free)Spin();else Follow(w,h);
        Occlude(w,h);
    }

    float Focal(int width)=>width*.5f/Mathf.Tan(WebcamFov*.5f*Mathf.Deg2Rad);

    void Frame(int w,int h)
    {
        if(w==0){view.fieldOfView=40;return;}
        float f=Focal(w),visible=view.aspect>=(float)w/h?w/view.aspect:h;
        view.fieldOfView=2*Mathf.Atan(visible*.5f/f)*Mathf.Rad2Deg;
        const float distance=20;
        backdrop.localPosition=new Vector3(0,0,distance);backdrop.localRotation=Quaternion.identity;backdrop.localScale=new Vector3(distance*w/f,distance*h/f,1);
    }

    Vector3 PalmCentre(int w,int h)
    {
        float f=Focal(w),z=f/tracker.PixelsPerMetre;var c=tracker.PalmPoint;
        return new Vector3((c.x-w*.5f)/f*z,(c.y-h*.5f)/f*z,z);
    }

    void Follow(int w,int h)
    {
        float dt=Mathf.Max(Time.unscaledDeltaTime,1e-3f);
        bool found=w>0 && tracker.Found && tracker.PixelsPerMetre>1;
        if(found)
        {
            var targetRotation=Quaternion.LookRotation(tracker.Forward,tracker.Normal);
            var targetPosition=PalmCentre(w,h)+tracker.Normal*Lift;
            float targetSize=Mathf.Clamp(tracker.PalmWidth,.05f,.11f)*Spread;
            if(!hasPose || lostFor>.6f){position=targetPosition;rotation=targetRotation;size=targetSize;hasPose=true;}
            else
            {
                float speed=(targetPosition-position).magnitude/dt;
                position=Vector3.Lerp(position,targetPosition,1-Mathf.Exp(-dt*Mathf.Lerp(9,30,speed/.5f)));
                rotation=Quaternion.Slerp(rotation,targetRotation,1-Mathf.Exp(-dt*12));
                size=Mathf.Lerp(size,targetSize,1-Mathf.Exp(-dt*5));
            }
            lostFor=0;
        }
        else lostFor+=dt;
        float target=found || lostFor<.25f?1:0;
        shown=Mathf.MoveTowards(shown,target,dt*(target>shown?2.4f:4f));
        modelRoot.gameObject.SetActive(hasPose && shown>.001f);
        if(!hasPose)return;
        modelRoot.SetPositionAndRotation(view.transform.TransformPoint(position),view.transform.rotation*rotation);
        modelRoot.localScale=Vector3.one*size*Pop(shown);
    }

    void Spin()
    {
        float dt=Mathf.Max(Time.unscaledDeltaTime,1e-3f);
        var mouse=(Vector2)Input.mousePosition;
        bool overControls=controls.Contains(new Vector2(mouse.x,Screen.height-mouse.y));
        if(Input.GetMouseButtonDown(0))lastMouse=mouse;
        if(Input.GetMouseButton(0) && !overControls){yaw-=(mouse.x-lastMouse.x)*.3f;tilt=Mathf.Clamp(tilt+(mouse.y-lastMouse.y)*.2f,5,70);}
        else yaw+=dt*8;
        lastMouse=mouse;
        shown=Mathf.MoveTowards(shown,1,dt*2.4f);
        modelRoot.gameObject.SetActive(true);
        modelRoot.SetPositionAndRotation(view.transform.TransformPoint(new Vector3(0,-.035f,.42f)),view.transform.rotation*Quaternion.Euler(-tilt,0,0)*Quaternion.Euler(0,yaw,0));
        modelRoot.localScale=Vector3.one*.24f*Pop(shown);
    }

    static float Pop(float t){t=Mathf.Clamp01(t);float u=t-1;return 1+2.70158f*u*u*u+1.70158f*u*u;}

    void Occlude(int w,int h)
    {
        bool on=!free && w>0 && tracker.Found && hasPose && tracker.PixelsPerMetre>1;
        for(int i=0;i<occluders.Length;i++)
        {
            occluders[i].gameObject.SetActive(on);
            if(!on)continue;
            var centre=PalmCentre(w,h);
            var back=Vector3.forward*FingerDepth;
            Vector3 a=centre+tracker.Joints[Bones[i,0]]-tracker.PalmJoint+back,b=centre+tracker.Joints[Bones[i,1]]-tracker.PalmJoint+back;
            float length=Vector3.Distance(a,b);
            occluders[i].localPosition=(a+b)*.5f;
            occluders[i].localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
            occluders[i].localScale=new Vector3(FingerRadius*2,(length+FingerRadius*2)*.5f,FingerRadius*2);
        }
    }

    string Hint()
    {
        if(free)return denied?"Sem permissão para usar a câmera. Arraste para girar a maquete.":"Nenhuma câmera disponível. Arraste para girar a maquete.";
        if(starting)return "Ligando a câmera...";
        if(!tracker.Found)return "Mostre a palma da mão aberta para a câmera";
        return "Gire a mão para ver a fazenda de outros ângulos";
    }

    void OnGUI()
    {
        if(Current!=this || model==null)return;
        GUI.skin=UITheme.Skin;GUI.depth=-200;float s=UITheme.Scale;
        bool security=FarmSecurityProgression.Visible;
        var head=new Rect(UITheme.Size(24),UITheme.Size(20),UITheme.Size(500),UITheme.Size(178));UITheme.Panel(head);controls=head;
        float pad=18*s,inner=head.width-pad*2;
        GUI.Label(new Rect(head.x+pad,head.y+12*s,inner,20*s),"VISUALIZAÇÃO 3D  ·  FAZENDA "+(Farm+1)+" DE "+names.Length,new GUIStyle(UITheme.Style("caption")){normal={textColor=UITheme.Accent}});
        var tab=new GUIStyle(UITheme.Style("tab")){fontSize=UITheme.Size(14),padding=new RectOffset(4,4,2,2)};var lit=new GUIStyle(UITheme.Style("primary")){fontSize=UITheme.Size(14),padding=new RectOffset(4,4,2,2)};
        float arrow=UITheme.Size(40);
        if(GUI.Button(new Rect(head.xMax-pad-arrow*2-6*s,head.y+38*s,arrow,arrow),"‹",tab))LoadModel(Farm-1);
        if(GUI.Button(new Rect(head.xMax-pad-arrow,head.y+38*s,arrow,arrow),"›",tab))LoadModel(Farm+1);
        GUI.Label(new Rect(head.x+pad,head.y+32*s,inner-arrow*2-16*s,46*s),model.farmName,new GUIStyle(UITheme.Style("title")){fontSize=UITheme.Size(32)});
        string detail=model.kind+"  ·  "+model.chickens+" galinhas  ·  "+(model.guards==1?"1 fazendeiro de ronda":model.guards+" fazendeiros de ronda");
        if(security)detail+="  ·  "+(model.cameras==1?"1 câmera":model.cameras+" câmeras");
        GUI.Label(new Rect(head.x+pad,head.y+84*s,inner,24*s),detail,new GUIStyle(UITheme.Style("small")){wordWrap=false});
        var keys=new List<string>{"Ronda","Galinhas","Entrada"};if(security)keys.Add("Cameras");
        float gap=8*s,chip=(inner-gap*3)/4,x=head.x+pad;
        foreach(var key in keys)
        {
            bool on=layers[key];
            if(GUI.Button(new Rect(x,head.y+122*s,chip,34*s),key=="Cameras"?"Segurança":key,on?lit:tab)){layers[key]=!on;model.Show(key,!on);}
            x+=chip+gap;
        }
        UITheme.HintBar(Screen.height-UITheme.Size(76),Hint(),"A / D","Trocar fazenda","Esc","Voltar ao tablet");
    }

    void Restore()
    {
        if(restored)return;restored=true;
        foreach(var (light,on) in lights)if(light!=null)light.enabled=on;
        foreach(var (camera,on) in cameras)if(camera!=null)camera.enabled=on;
        RenderSettings.ambientMode=ambientMode;RenderSettings.ambientLight=ambientLight;RenderSettings.fog=fog;RenderSettings.sun=oldSun;
        if(pipeline!=null)pipeline.shadowDistance=shadowDistance;
        if(webcam!=null){webcam.Stop();Destroy(webcam);}
        tracker?.Dispose();tracker=null;
        foreach(var m in materials)if(m!=null)Destroy(m);
    }

    void OnDestroy()
    {
        if(Current==this){Current=null;ClosedFrame=Time.frameCount;}
        Restore();
    }
}
