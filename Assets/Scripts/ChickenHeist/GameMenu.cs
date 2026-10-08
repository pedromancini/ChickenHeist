using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-500)]
public class GameMenu : MonoBehaviour
{
    public static GameMenu Instance { get; private set; }
    public static bool IsOpen => Instance!=null && Instance.page!=Page.Play;
    public static bool BlocksInput => IsOpen || StoryDirector.Active || TruckCageLids.Active!=null || HomeNextNight.IsResting || closedFrame==Time.frameCount || DeveloperConsole.BlocksInput || BackpackPanel.BlocksInput;
    static int closedFrame=-1;
    static bool played,pendingFresh;
    static GameCheckpointData pendingLoad;
    enum Page { Play, Main, Pause, Settings, NewGame, Quit, Failure, LoadConfirm, Loading }
    Page page=Page.Play,returnPage=Page.Main;
    GameCheckpoint checkpoint;
    GamePreferences settings,applied,videoBefore;
    float confirmUntil;
    string feedback="";
    string saveSummary="";
    AsyncOperation loading;
    int settingsTab;
    Vector2 scroll;
    bool menuBackdrop;
    Vector3 cameraLocalPosition;
    Quaternion cameraLocalRotation;
    GUIStyle title,label,small,button,tabStyle,toggle;
    public bool HasSave => HouseholdEconomy.Instance!=null && File.Exists(HouseholdEconomy.Instance.CheckpointPath);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Instance=null;played=false;pendingFresh=false;pendingLoad=null;closedFrame=-1;}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register(){SceneManager.sceneLoaded-=SceneLoaded;SceneManager.sceneLoaded+=SceneLoaded;}
    static void SceneLoaded(Scene scene,LoadSceneMode mode)
    {
        if(FindAnyObjectByType<HeistGameManager>()==null || Instance!=null)return;
        new GameObject("Menu e progresso").AddComponent<GameMenu>();
    }
    void Awake()
    {
        Instance=this;checkpoint=gameObject.AddComponent<GameCheckpoint>();
        gameObject.AddComponent<DeveloperConsole>();
        gameObject.AddComponent<BackpackPanel>();
        gameObject.AddComponent<StoryDirector>();gameObject.AddComponent<GameAudioMix>();
        if(!Application.isBatchMode)page=Page.Main;
    }
    IEnumerator Start()
    {
        settings=GamePreferences.Load();applied=settings.Copy();
        if(!Application.isBatchMode){settings.Apply(!HouseholdEconomy.ReviewSession);SetPage(Page.Main);}
        yield return null;
        if(pendingLoad!=null)
        {
            RestoreMenuCamera();
            var data=pendingLoad;pendingLoad=null;
            if(checkpoint.Restore(data,out feedback)){played=true;SetPage(Page.Play);}else SetPage(Page.Main);
        }
        else if(pendingFresh)
        {
            pendingFresh=false;
            if(HouseholdEconomy.Instance.RestoreAccount(new HouseholdAccount()))
            {played=true;SetPage(Page.Play);checkpoint.Save(out feedback);StoryDirector.Instance.Begin(false,()=>checkpoint.Save(out feedback));}
            else{feedback=HouseholdEconomy.Instance.Message;SetPage(Page.Main);}
        }
        else if(played)SetPage(Page.Play);
        if(!IsOpen && !StoryDirector.Active && HouseholdEconomy.Instance.Account.pendingVision)StoryDirector.Instance.Begin(true,()=>checkpoint.Save(out feedback));
        RefreshSaveSummary();
    }
    void Update()
    {
        if(confirmUntil>0 && Time.realtimeSinceStartup>=confirmUntil)RevertVideo();
        if(StoryDirector.Active || DeveloperConsole.BlocksInput || BackpackPanel.BlocksInput)return;
        if(page==Page.Loading)return;
        if(!Input.GetKeyDown(KeyCode.Escape))return;
        if(page==Page.Play)
        {
            if(OldPickupTruck.Instance?.ignition?.Active==true)return;
            if(ProtagonistPhone.IsOpen || VillageMarket.IsOpen || ProtagonistPhone.LastClosedFrame==Time.frameCount || VillageMarket.ClosedFrame==Time.frameCount)return;
            if(FindObjectsByType<ChickenCoopLockpick>().Any(c=>c.ChallengeActive))return;
            Pause();
        }
        else if(page==Page.Pause)Resume();
        else if(page==Page.Settings){if(confirmUntil>0)RevertVideo();else LeaveSettings();}
        else if(page==Page.NewGame || page==Page.Quit || page==Page.LoadConfirm)SetPage(returnPage);
    }
    void LateUpdate(){if(IsOpen){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
    void OnDestroy()
    {
        if(Instance!=this)return;
        Time.timeScale=1;AudioListener.pause=false;Instance=null;
    }
    void SetPage(Page next)
    {
        bool backdrop=next==Page.Main || next==Page.NewGame || next==Page.Failure ||
            ((next==Page.Settings || next==Page.Quit || next==Page.LoadConfirm) && returnPage==Page.Main);
        var camera=Camera.main;
        if(backdrop && !menuBackdrop && camera!=null && HouseholdEconomy.Instance?.home!=null)
        {
            cameraLocalPosition=camera.transform.localPosition;cameraLocalRotation=camera.transform.localRotation;menuBackdrop=true;
            var site=HouseholdEconomy.Instance.home;
            camera.transform.position=site.position+new Vector3(-16,8,-24);
            camera.transform.LookAt(site.position+Vector3.up*2.5f);
            camera.transform.LookAt(site.position+Vector3.up*2.5f-camera.transform.right*11f);
        }
        else if(!backdrop)RestoreMenuCamera();
        page=next;Time.timeScale=next==Page.Play?1:0;AudioListener.pause=next!=Page.Play;
        Cursor.lockState=next==Page.Play?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=next!=Page.Play;
        if(next==Page.Play)closedFrame=Time.frameCount;
    }
    void RestoreMenuCamera()
    {
        if(!menuBackdrop)return;
        if(Camera.main!=null){Camera.main.transform.localPosition=cameraLocalPosition;Camera.main.transform.localRotation=cameraLocalRotation;}
        menuBackdrop=false;
    }
    public void Pause(){feedback="";SetPage(Page.Pause);}
    public void Resume(){SetPage(Page.Play);}
    public void ShowFailure(string reason){feedback=reason;SetPage(Page.Failure);}
    public bool SaveProgress(){bool success=checkpoint.Save(out feedback);if(success)RefreshSaveSummary();return success;}
    void RefreshSaveSummary()
    {
        saveSummary="";
        if(HouseholdEconomy.Instance==null || !HasSave)return;
        if(GameCheckpoint.TryRead(HouseholdEconomy.Instance.CheckpointPath,out var data,out var error))
        {
            string mission=data.missionFarm>=0?"Missao em andamento":"No sitio";
            var phone=ProtagonistPhone.Instance;
            if(data.missionFarm>=0 && phone!=null && data.missionFarm<phone.farmNames.Length)mission=phone.farmNames[data.missionFarm];
            saveSummary="Dia "+data.account.day+"   |   R$ "+data.account.balance+"\n"+mission+"\n"+data.savedAt;
        }
        else saveSummary=error;
    }
    void RequestLoad()
    {
        if(played && !HeistGameManager.Instance.missionEnded){returnPage=page;SetPage(Page.LoadConfirm);}
        else LoadProgress();
    }
    public void LoadProgress()
    {
        if(page==Page.Loading)return;
        if(!GameCheckpoint.TryRead(HouseholdEconomy.Instance.CheckpointPath,out var data,out feedback))return;
        if(data.scene!=SceneManager.GetActiveScene().path){feedback="Este save pertence a outro mundo.";return;}
        pendingLoad=data;Reload();
    }
    void Reload()
    {
        if(page==Page.Loading)return;
        SetPage(Page.Loading);feedback="";
        StartCoroutine(LoadScene());
    }
    IEnumerator LoadScene()
    {
        // Present the loading frame before scene deserialization starts.
        yield return new WaitForEndOfFrame();
        loading=SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().path);
        while(!loading.isDone)yield return null;
    }
    void BeginGame()
    {
        if(HasSave)
        {returnPage=Page.Main;SetPage(Page.NewGame);return;}
        pendingFresh=true;Reload();
    }
    void OpenSettings(){returnPage=page;settings=applied.Copy();feedback="";scroll=Vector2.zero;SetPage(Page.Settings);}
    void LeaveSettings(){settings=applied.Copy();SetPage(returnPage);}
    void ApplySettings()
    {
        bool display=settings.width!=applied.width || settings.height!=applied.height || settings.fullscreen!=applied.fullscreen;
        videoBefore=applied.Copy();settings.Apply(display);
        if(display)confirmUntil=Time.realtimeSinceStartup+15;
        else{applied=settings.Copy();applied.Persist();feedback="Configuracoes salvas.";}
    }
    void RevertVideo(){videoBefore.Apply(true);settings=videoBefore.Copy();confirmUntil=0;feedback="Modo de video anterior restaurado.";}
    void Quit(){Application.Quit();}
    void Styles()
    {
        // Rebuilt from the shared theme whenever the theme rebuilds (resolution change).
        if(title!=null && label.font==UITheme.Body && label.fontSize==UITheme.Style("body").fontSize)return;
        var skin=UITheme.Skin;
        title=new GUIStyle(UITheme.Style("title")){fontSize=UITheme.Size(46)};
        label=new GUIStyle(UITheme.Style("body"));
        small=new GUIStyle(UITheme.Style("small"));
        button=new GUIStyle(skin.button){alignment=TextAnchor.MiddleLeft,fontSize=UITheme.Size(19),padding=new RectOffset(UITheme.Size(20),UITheme.Size(20),UITheme.Size(12),UITheme.Size(12)),margin=new RectOffset(0,0,0,UITheme.Size(10))};
        primary=new GUIStyle(UITheme.Style("primary")){alignment=TextAnchor.MiddleLeft,fontSize=UITheme.Size(20),padding=button.padding,margin=button.margin};
        tabStyle=new GUIStyle(UITheme.Style("tab"));
        toggle=new GUIStyle(skin.toggle);
    }
    GUIStyle primary;
    float contentHeight;
    bool Action(string text,bool main=false)=>GUILayout.Button(text,main?primary:button,GUILayout.MinHeight(UITheme.Size(50)));
    void OnGUI()
    {
        if(!IsOpen || settings==null)return;
        GUI.skin=UITheme.Skin;Styles();GUI.depth=-1000;
        var color=GUI.color;
        // Darken the world behind the menu (lighter on the title screen, where the valley is the backdrop).
        GUI.color=new Color(.02f,.018f,.015f,menuBackdrop?.35f:.66f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=color;
        float panelWidth=Mathf.Min(UITheme.Size(page==Page.Settings?760:470),Screen.width-UITheme.Size(160));
        // Settings use the full height; the other pages fit their measured content (from the previous repaint), centred vertically.
        float maxHeight=Screen.height-UITheme.Size(96);
        float height=page==Page.Settings || contentHeight<=0?maxHeight:Mathf.Min(maxHeight,contentHeight+UITheme.Size(52));
        var panel=new Rect(UITheme.Size(48),page==Page.Settings?UITheme.Size(48):(Screen.height-height)*.5f,panelWidth+UITheme.Size(64),height);
        UITheme.Panel(panel,UITheme.PanelFill,UITheme.Edge,16);
        GUILayout.BeginArea(new Rect(panel.x+UITheme.Size(32),panel.y+UITheme.Size(26),panelWidth,panel.height-UITheme.Size(52)));
        GUILayout.Label("Chicken Heist",title);
        GUILayout.Label(page==Page.Main?"Sítio do Recomeço":page==Page.Pause?"Jogo pausado":page==Page.Settings?"Configurações":page==Page.Failure?"Assalto interrompido":
            page==Page.NewGame?"Nova história":page==Page.Quit?"Sair":page==Page.LoadConfirm?"Carregar":page==Page.Loading?"Carregando":"",UITheme.Style("subtitle"));
        GUILayout.Space(UITheme.Size(26));
        if(page==Page.Main)
        {
            bool canResume=played && !HeistGameManager.Instance.missionEnded;
            if(canResume && Action("Voltar ao jogo",true))Resume();
            GUI.enabled=HasSave;if(Action("Continuar jogo salvo",!canResume && HasSave))RequestLoad();GUI.enabled=true;
            if(HasSave){GUILayout.Label(saveSummary,small);GUILayout.Space(UITheme.Size(14));}
            if(Action(HasSave?"Novo jogo":"Iniciar jogo",!HasSave && !canResume))BeginGame();
            if(Action("Configurações"))OpenSettings();
            if(Action("Sair")){returnPage=Page.Main;SetPage(Page.Quit);}
        }
        else if(page==Page.Pause)
        {
            if(Action("Continuar",true))Resume();
            if(HouseholdEconomy.Instance.Account.introSeen && Action("Rever abertura")){Resume();StoryDirector.Instance.ReplayOpening();}
            if(Action("Salvar jogo"))SaveProgress();
            GUI.enabled=HasSave;if(Action("Carregar jogo"))RequestLoad();GUI.enabled=true;
            if(Action("Configurações"))OpenSettings();
            if(Action("Menu principal"))SetPage(Page.Main);
            if(Action("Sair do jogo")){returnPage=Page.Pause;SetPage(Page.Quit);}
        }
        else if(page==Page.Settings)DrawSettings();
        else if(page==Page.NewGame)
        {
            GUILayout.Label("Iniciar uma nova história? O progresso atual será substituído. Uma cópia do save anterior será mantida.",label);GUILayout.Space(UITheme.Size(24));
            if(Action("Iniciar nova história",true)){pendingFresh=true;Reload();}
            if(Action("Voltar"))SetPage(Page.Main);
        }
        else if(page==Page.Quit)
        {
            GUILayout.Label("Sair de Chicken Heist?",label);GUILayout.Space(UITheme.Size(24));
            if(played && !HeistGameManager.Instance.missionEnded && Action("Salvar e sair",true)){if(SaveProgress())Quit();}
            if(Action("Sair sem salvar"))Quit();
            if(Action("Voltar"))SetPage(returnPage);
        }
        else if(page==Page.Failure)
        {
            GUI.enabled=HasSave;if(Action("Carregar último save",true))LoadProgress();GUI.enabled=true;
            if(Action("Menu principal"))SetPage(Page.Main);
        }
        else if(page==Page.LoadConfirm)
        {
            GUILayout.Label("Carregar este progresso? Alterações posteriores ao save serão descartadas.",label);GUILayout.Space(UITheme.Size(18));
            GUILayout.Label(saveSummary,small);GUILayout.Space(UITheme.Size(24));
            if(Action("Carregar save",true))LoadProgress();
            if(Action("Voltar"))SetPage(returnPage);
        }
        else if(page==Page.Loading)
        {
            GUILayout.Label("Carregando o vale...",label);GUILayout.Space(UITheme.Size(24));
            var bar=GUILayoutUtility.GetRect(200,UITheme.Size(10),GUILayout.ExpandWidth(true));
            UITheme.Meter(bar,Mathf.Clamp01((loading?.progress??.02f)/.9f),UITheme.Accent);
        }
        if(feedback.Length>0){GUILayout.Space(UITheme.Size(8));GUILayout.Label(feedback,small);}
        if(Event.current.type==EventType.Repaint){float measured=GUILayoutUtility.GetLastRect().yMax;if(Mathf.Abs(measured-contentHeight)>1){contentHeight=measured;}}
        GUILayout.FlexibleSpace();GUILayout.EndArea();
    }
    void DrawSettings()
    {
        GUI.enabled=confirmUntil<=0;
        int newTab=GUILayout.Toolbar(settingsTab,new[]{"Vídeo","Áudio","Controles"},tabStyle,GUILayout.Height(UITheme.Size(46)));
        if(newTab!=settingsTab){settingsTab=newTab;scroll=Vector2.zero;}
        GUILayout.Space(UITheme.Size(18));
        scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(Screen.height-UITheme.Size(410)));
        if(settingsTab==0)
        {
            var resolutions=Screen.resolutions.Select(r=>new Vector2Int(r.width,r.height)).Where(r=>r.x>=800 && r.y>=600).Distinct().ToList();
            var current=new Vector2Int(settings.width,settings.height);if(!resolutions.Contains(current))resolutions.Add(current);
            GUILayout.Label("Resolução",UITheme.Style("caption"));GUILayout.BeginHorizontal();
            int index=resolutions.IndexOf(current);
            if(GUILayout.Button("<",tabStyle,GUILayout.Width(UITheme.Size(48))))current=resolutions[(index+resolutions.Count-1)%resolutions.Count];
            GUILayout.Label(current.x+" × "+current.y,new GUIStyle(label){alignment=TextAnchor.MiddleCenter},GUILayout.Width(UITheme.Size(170)),GUILayout.Height(UITheme.Size(44)));
            if(GUILayout.Button(">",tabStyle,GUILayout.Width(UITheme.Size(48))))current=resolutions[(index+1)%resolutions.Count];
            settings.width=current.x;settings.height=current.y;GUILayout.FlexibleSpace();GUILayout.EndHorizontal();
            settings.fullscreen=UITheme.Check(settings.fullscreen,"Tela cheia");GUILayout.Space(UITheme.Size(12));
            GUILayout.Label("Qualidade gráfica",UITheme.Style("caption"));
            settings.quality=GUILayout.SelectionGrid(settings.quality,new[]{"Baixa","Média","Alta"},3,tabStyle);GUILayout.Space(UITheme.Size(12));
            settings.vsync=UITheme.Check(settings.vsync,"Sincronização vertical");GUILayout.Space(UITheme.Size(8));
            GUILayout.Label("Limite de quadros",UITheme.Style("caption"));
            int[] caps={30,60,120,144,-1};int cap=System.Array.IndexOf(caps,settings.frameLimit);
            GUI.enabled=confirmUntil<=0 && !settings.vsync;int next=GUILayout.Toolbar(Mathf.Max(0,cap),new[]{"30","60","120","144","Livre"},tabStyle);GUI.enabled=confirmUntil<=0;
            settings.frameLimit=caps[next];GUILayout.Space(UITheme.Size(8));
            Slider("Campo de visão",ref settings.fov,50,90);
        }
        else if(settingsTab==1)
        {
            Slider("Volume geral",ref settings.volume,0,1,true);
            Slider("Efeitos",ref settings.effectsVolume,0,1,true);
            Slider("Ambiente e animais",ref settings.ambienceVolume,0,1,true);
            Slider("Narração",ref settings.voiceVolume,0,1,true);
        }
        else
        {
            Slider("Sensibilidade do mouse",ref settings.sensitivity,20,300);
            settings.invertY=UITheme.Check(settings.invertY,"Inverter eixo vertical");
            settings.sprintToggle=UITheme.Check(settings.sprintToggle,"Shift liga/desliga a corrida (sem segurar)");GUILayout.Space(UITheme.Size(16));
            GUILayout.Label("Comandos",UITheme.Style("caption"));
            string[,] keys={{"W A S D","Andar"},{"Shift","Correr (segurar ou alternar)"},{"C","Agachar"},{"Espaço","Pular"},{"E","Interagir"},{"Tab","Tablet"},{"B","Mochila de itens"},
                {"Botão direito","Usar spray selecionado"},{"Esc","Pausa"},{"F","Dirigir / sair da caminhonete"},{"E / R","Carga: colocar / retirar"},{"G","Soltar galinhas no galinheiro"},{"N","Alternar rota fazenda / sítio"}};
            var keyStyle=UITheme.Style("key");
            for(int i=0;i<keys.GetLength(0);i++)
            {
                GUILayout.BeginHorizontal(GUILayout.Height(UITheme.Size(34)));
                GUILayout.Label(keys[i,0],keyStyle,GUILayout.Width(UITheme.Size(130)),GUILayout.Height(UITheme.Size(28)));GUILayout.Space(UITheme.Size(14));
                GUILayout.Label(keys[i,1],new GUIStyle(label){alignment=TextAnchor.MiddleLeft},GUILayout.Height(UITheme.Size(28)));
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.EndScrollView();GUI.enabled=true;GUILayout.Space(UITheme.Size(14));
        if(confirmUntil>0)
        {
            GUILayout.Label("Manter o modo de vídeo? "+Mathf.CeilToInt(confirmUntil-Time.realtimeSinceStartup)+" s",label);
            if(Action("Manter",true)){confirmUntil=0;applied=settings.Copy();applied.Persist();feedback="Configuracoes salvas.";}
            if(Action("Reverter"))RevertVideo();
        }
        else
        {
            GUILayout.BeginHorizontal();if(Action("Aplicar",true))ApplySettings();GUILayout.Space(UITheme.Size(12));
            if(Action("Restaurar padrão")){settings=GamePreferences.Defaults();feedback="Padrao selecionado. Clique em Aplicar para confirmar.";}
            GUILayout.Space(UITheme.Size(12));if(Action("Voltar"))LeaveSettings();GUILayout.EndHorizontal();
        }
    }
    void Slider(string name,ref float value,float min,float max,bool percent=false)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(name,label);GUILayout.FlexibleSpace();
        GUILayout.Label(Mathf.RoundToInt(percent?value*100:value)+(percent?"%":""),new GUIStyle(label){alignment=TextAnchor.MiddleRight,normal={textColor=UITheme.Accent}});
        GUILayout.EndHorizontal();
        value=GUILayout.HorizontalSlider(value,min,max);GUILayout.Space(UITheme.Size(10));
    }
}
