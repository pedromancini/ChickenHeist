using System;

using UnityEngine;

[DefaultExecutionOrder(450)]

public class StoryDirector : MonoBehaviour

{

    public static StoryDirector Instance {get;private set;}

    public static bool Active=>Instance!=null && Instance.running;

    public bool running {get;private set;}

    public static readonly string[] Opening=VisitorOpeningDialogue.Text;

    public static readonly string[] Decline=StoryDialogue.declineText;

    string[] Texts=>vision?Decline:Opening;

    string[] Speakers=>vision?StoryDialogue.declineSpeakers:VisitorOpeningDialogue.Speakers;

    string[] Files=>vision?StoryDialogue.declineFiles:StoryDialogue.openingFiles;

    Texture2D illustration;float lineClock;int line; VisitorCinematicStage openingStage; DeclineCinematicStage declineStage;

    public int CurrentLine=>line;
    public bool Paused {get;private set;}
    bool replay,freshOpening;float skipHeld;CursorLockMode cursorBeforePause;bool cursorVisibleBeforePause;
    public float Elapsed=>clock;

    public float LineDuration=>Duration;
    float Duration=>voice.clip!=null?voice.clip.length+.8f:vision?Mathf.Max(2.5f,Texts[line].Split(' ').Length*.36f+1.2f):VisitorOpeningDialogue.Durations[line];

    void StartLine(){voice.Stop();voice.volume=Mathf.Clamp01(GameAudioMix.Voice);var file=vision?Files[line]:VisitorOpeningDialogue.VoiceFile(line);voice.clip=string.IsNullOrEmpty(file)?null:Resources.Load<AudioClip>("Dialogue/"+file);if(voice.clip!=null)voice.Play();}

    Camera eye;Vector3 position;Quaternion rotation;float fov,clock;bool vision;int shot=-1;Action finished;

    AudioSource voice;Light sceneLight;

    GameObject ruins;Material ruinMaterial;

    readonly System.Collections.Generic.Dictionary<Renderer,Material[]> surfaces=new System.Collections.Generic.Dictionary<Renderer,Material[]>();

    void Awake(){Instance=this;voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;}
    void Start(){if(HouseholdEconomy.Instance?.home!=null)ReceivedTabletDock.Ensure(HouseholdEconomy.Instance.home);}
    public bool ReplayOpening(){if(!Begin(false))return false;replay=true;freshOpening=false;return true;}
    public void SetPaused(bool value)
    {
        if(!running || Paused==value)return;Paused=value;
        if(value){cursorBeforePause=Cursor.lockState;cursorVisibleBeforePause=Cursor.visible;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;voice.Pause();}
        else{Cursor.lockState=cursorBeforePause;Cursor.visible=cursorVisibleBeforePause;voice.UnPause();}
        openingStage?.SetPaused(value);
    }

    public bool Begin(bool decline,Action onDone=null)

    {

        if(running || Camera.main==null || HouseholdEconomy.Instance?.home==null)return false;
        if(!decline && Resources.Load<GameObject>("Cinematics/VisitorStage")==null){Debug.LogError("VisitorStage prefab is missing; opening was not marked complete.");return false;}

        vision=decline;finished=onDone;eye=Camera.main;position=eye.transform.localPosition;rotation=eye.transform.localRotation;fov=eye.fieldOfView;
        replay=false;freshOpening=!decline && !HouseholdEconomy.Instance.Account.introSeen;Paused=false;skipHeld=0;

        clock=0;lineClock=0;line=0;shot=-1;running=true;illustration=Resources.Load<Texture2D>("Cinematics/"+(vision?"decline":"opening"));StartLine();

        sceneLight=new GameObject("Luz da lembranca").AddComponent<Light>();sceneLight.type=LightType.Point;sceneLight.range=14;sceneLight.intensity=vision?.45f:1.3f;sceneLight.color=vision?new Color(.5f,.65f,1):new Color(1,.73f,.46f);

        try
        {
            ProtagonistPhone.Instance?.SetOpen(false);
            if(vision){BuildRuins();declineStage=DeclineCinematicStage.Create(HouseholdEconomy.Instance.home);}else openingStage=VisitorCinematicStage.Create(HouseholdEconomy.Instance.home,eye);
        }
        catch(Exception error){Restore();finished=null;Debug.LogException(error);return false;}

        return true;

    }

    void BuildRuins()

    {

        var home=HouseholdEconomy.Instance.home;if(home==null)return;

        ruinMaterial=SecurityEquipmentVisual.Material("Madeira abandonada da visao",new Color(.12f,.10f,.085f));

        foreach(var renderer in home.GetComponentsInChildren<MeshRenderer>())

        {

            if(renderer.name.Contains("Tabua") || renderer.name.Contains("Casa") || renderer.name.Contains("Mourao"))

            {surfaces[renderer]=renderer.sharedMaterials;var mats=new Material[renderer.sharedMaterials.Length];for(int i=0;i<mats.Length;i++)mats[i]=ruinMaterial;renderer.sharedMaterials=mats;}

        }

        ruins=new GameObject("Ruinas imaginadas — temporarias");ruins.transform.SetParent(home,false);

        for(int i=0;i<28;i++)

        {

            float x=Mathf.Sin(i*13.7f)*6,z=-3-Mathf.Abs(Mathf.Cos(i*7.3f))*3;

            SecurityEquipmentVisual.Part(ruins.transform,"Tabua desabada",PrimitiveType.Cube,new Vector3(x,.10f+(i%4)*.055f,z),new Vector3(.18f,.08f,1.4f+(i%3)*.4f),ruinMaterial,Quaternion.Euler(i%3*7,i*37,0));

        }

        for(int i=0;i<7;i++)SecurityEquipmentVisual.Part(ruins.transform,"Escora quebrada",PrimitiveType.Cube,new Vector3(-5+i*1.6f,1.2f,-3),new Vector3(.12f,2.5f,.12f),ruinMaterial,Quaternion.Euler(0,0,i%2==0?32:-27));

    }

    void Update()

    {

        if(!running || GameMenu.IsOpen)return;
        if(Input.GetKeyDown(KeyCode.Escape)){SetPaused(!Paused);return;}
        skipHeld=Input.GetKey(KeyCode.Space)?skipHeld+Time.unscaledDeltaTime:0;
        if(skipHeld>=.9f){Complete();return;}
        if(Paused)return;

        clock+=Time.unscaledDeltaTime;lineClock+=Time.unscaledDeltaTime;

        if(lineClock>=Duration){lineClock=0;if(++line>=Texts.Length){line=Texts.Length-1;Complete();return;}StartLine();}


    }

    void LateUpdate()

    {

        if(!running || eye==null || Paused)return;

        var home=HouseholdEconomy.Instance?.home;if(home==null){Complete();return;}

        if(declineStage!=null){declineStage.Evaluate(eye,line,lineClock,Duration,clock);voice.volume=Mathf.Clamp01(GameAudioMix.Voice);return;}

        if(openingStage!=null){openingStage.Evaluate(eye,line,lineClock,Duration,clock);voice.volume=Mathf.Clamp01(GameAudioMix.Voice);return;}

        int index=line==0?0:line==Texts.Length-1?2:1;float t=Mathf.Clamp01(lineClock/Duration);

        Vector3[] places={new Vector3(-16,5,-20),new Vector3(15,2,-8),new Vector3(-4,2.3f,-5)};

        Vector3[] targets={new Vector3(0,1.5f,0),new Vector3(12,1,-3),new Vector3(0,1.2f,1)};

        eye.transform.position=home.TransformPoint(places[index]+Vector3.right*(t-.5f)*2);eye.transform.LookAt(home.TransformPoint(targets[index]));eye.fieldOfView=vision?48:55;

        sceneLight.transform.position=home.TransformPoint(targets[index]+Vector3.up*3);voice.volume=Mathf.Clamp01(GameAudioMix.Voice);

        shot=index;

    }

    public void Complete()

    {

        if(!running)return;

        bool saved=replay || HouseholdEconomy.Instance.Commit(a=>{if(vision){a.pendingVision=false;a.declineSeen=true;}else a.introSeen=true;return true;},vision?"Uma nova manha. Leia as noticias no tablet.":"O tablet ficou com voce. TAB para examinar as fazendas antes de decidir.");

        if(!saved)return;

        bool wasOpening=!vision;Restore();
        if(freshOpening)
        {
            var player=HeistGameManager.Instance.player;var controller=player.GetComponent<CharacterController>();bool active=controller!=null && controller.enabled;if(controller!=null)controller.enabled=false;
            player.SetPositionAndRotation(HouseholdEconomy.Instance.home.TransformPoint(new Vector3(-4.72f,.65f,3.78f)),HouseholdEconomy.Instance.home.rotation*Quaternion.Euler(0,235,0));
            player.GetComponentInChildren<PlayerLook>()?.RestorePitch(20);if(controller!=null)controller.enabled=active;Physics.SyncTransforms();
        }
        var callback=finished;finished=null;callback?.Invoke();
        if(wasOpening)HeistGameManager.Instance?.ShowMessage("TAB  |  Examine as fotos no tablet. Escolha uma fazenda quando estiver pronto.",9);

    }

    void Restore()

    {

        if(Paused)SetPaused(false);running=false;voice.Stop();if(declineStage!=null){declineStage.Release();declineStage=null;}if(openingStage!=null){openingStage.Release();openingStage=null;}

        if(eye!=null){eye.transform.localPosition=position;eye.transform.localRotation=rotation;eye.fieldOfView=fov;}

        if(sceneLight!=null)Destroy(sceneLight.gameObject);

        foreach(var pair in surfaces)if(pair.Key!=null)pair.Key.sharedMaterials=pair.Value;surfaces.Clear();

        if(ruins!=null)Destroy(ruins);if(ruinMaterial!=null)Destroy(ruinMaterial);

    }

    void OnDestroy(){if(running)Restore();if(Instance==this)Instance=null;}

    void OnGUI()

    {

        if(!running)return;

        int depth=GUI.depth;GUI.depth=-200;Color color=GUI.color;

        if(vision && declineStage==null && illustration!=null && line>0 && line<Texts.Length-1)

        {

            float zoom=1.02f+Mathf.Clamp01(clock/65)*.04f;

            GUI.color=Color.white;

            GUI.color=Color.black;GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;

            GUI.BeginGroup(new Rect(0,Screen.height*.1f,Screen.width,Screen.height*.68f));

            GUI.DrawTexture(new Rect(-Screen.width*(zoom-1)*.5f,-Screen.height*.68f*(zoom-1)*.5f,Screen.width*zoom,Screen.height*.68f*zoom),illustration,ScaleMode.ScaleToFit);

            GUI.EndGroup();

        }

        GUI.color=vision?new Color(.08f,.04f,.03f,.30f):new Color(0,0,0,.1f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);

        GUI.color=Color.black;GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height*(vision?.10f:.08f)),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(0,Screen.height*(vision?.78f:.82f),Screen.width,Screen.height*(vision?.22f:.18f)),Texture2D.whiteTexture);

        GUI.color=Color.white;var style=new GUIStyle(GUI.skin.label){fontSize=Mathf.Clamp(Screen.height/35,16,30),wordWrap=true,alignment=TextAnchor.MiddleCenter};

        if(!string.IsNullOrEmpty(Texts[line]))GUI.Label(new Rect(Screen.width*.1f,Screen.height*(vision?.8f:.835f),Screen.width*.8f,Screen.height*.12f),Speakers[line].ToUpperInvariant()+"\n"+Texts[line],style);
        if(!vision && line==0)GUI.Label(new Rect(Screen.width*.08f,Screen.height*.16f,Screen.width*.5f,60),"UMA ENTREGA",new GUIStyle(style){alignment=TextAnchor.MiddleLeft,fontSize=28});

        GUI.Label(new Rect(Screen.width-410,16,390,30),"ESC — pausar  |  Segure ESPAÇO — pular",new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleRight,fontSize=14});
        if(skipHeld>0){GUI.color=new Color(.85f,.72f,.48f);GUI.DrawTexture(new Rect(Screen.width-200,46,180*Mathf.Clamp01(skipHeld/.9f),3),Texture2D.whiteTexture);GUI.color=Color.white;}
        if(Paused)
        {
            GUI.color=new Color(0,0,0,.65f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;
            GUI.Label(new Rect(0,Screen.height*.32f,Screen.width,45),"CENA PAUSADA",style);
            if(GUI.Button(new Rect((Screen.width-240)/2,Screen.height*.46f,240,42),"Continuar"))SetPaused(false);
            if(GUI.Button(new Rect((Screen.width-240)/2,Screen.height*.46f+54,240,42),"Pular cena"))Complete();
        }
        GUI.color=color;GUI.depth=depth;

    }

}
