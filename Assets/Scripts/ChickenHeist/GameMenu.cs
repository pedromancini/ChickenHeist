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
    public static bool BlocksInput => IsOpen || HomeNextNight.IsResting || closedFrame==Time.frameCount || DeveloperConsole.BlocksInput || BackpackPanel.BlocksInput;
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
    Texture2D normal,hover,selected;
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
            {played=true;SetPage(Page.Play);checkpoint.Save(out feedback);}
            else{feedback=HouseholdEconomy.Instance.Message;SetPage(Page.Main);}
        }
        else if(played)SetPage(Page.Play);
        RefreshSaveSummary();
    }
    void Update()
    {
        if(confirmUntil>0 && Time.realtimeSinceStartup>=confirmUntil)RevertVideo();
        if(DeveloperConsole.BlocksInput || BackpackPanel.BlocksInput)return;
        if(page==Page.Loading)return;
        if(!Input.GetKeyDown(KeyCode.Escape))return;
        if(page==Page.Play)
        {
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
        if(normal!=null)Destroy(normal);if(hover!=null)Destroy(hover);if(selected!=null)Destroy(selected);
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
        played=true;SetPage(Page.Play);SaveProgress();
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
        if(title!=null)return;
        normal=Pixel(new Color(.16f,.19f,.18f));hover=Pixel(new Color(.26f,.32f,.28f));selected=Pixel(new Color(.31f,.43f,.32f));
        label=new GUIStyle(GUI.skin.label){fontSize=19,wordWrap=true};label.normal.textColor=new Color(.92f,.94f,.91f);
        small=new GUIStyle(label){fontSize=15};title=new GUIStyle(label){fontSize=38,fontStyle=FontStyle.Bold};
        button=new GUIStyle(GUI.skin.button){fontSize=19,alignment=TextAnchor.MiddleLeft,padding=new RectOffset(18,18,12,12),margin=new RectOffset(0,0,0,10)};
        button.normal.background=normal;button.hover.background=button.active.background=hover;button.focused.background=selected;
        button.normal.textColor=button.hover.textColor=button.active.textColor=button.focused.textColor=Color.white;
        tabStyle=new GUIStyle(button){fontSize=17,alignment=TextAnchor.MiddleCenter};
        tabStyle.onNormal.background=tabStyle.onHover.background=tabStyle.onActive.background=selected;
        tabStyle.onNormal.textColor=tabStyle.onHover.textColor=tabStyle.onActive.textColor=Color.white;
        toggle=new GUIStyle(GUI.skin.toggle){fontSize=19,padding=new RectOffset(26,0,0,0)};toggle.normal.textColor=toggle.onNormal.textColor=Color.white;
    }
    static Texture2D Pixel(Color c){var t=new Texture2D(1,1);t.SetPixel(0,0,c);t.Apply();return t;}
    bool Action(string text)=>GUILayout.Button(text,button,GUILayout.MinHeight(48));
    void OnGUI()
    {
        if(!IsOpen || settings==null)return;
        Styles();GUI.depth=-1000;
        var previous=GUI.matrix;var color=GUI.color;
        GUI.color=new Color(.025f,.035f,.031f,menuBackdrop?.30f:.64f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=color;
        float scale=Mathf.Min(1,Screen.height/820f,Screen.width/1040f);
        float height=Screen.height/scale;
        GUI.matrix=Matrix4x4.Scale(new Vector3(scale,scale,1));
        float panelWidth=page==Page.Settings?720:440;
        GUI.color=new Color(.04f,.055f,.047f,.93f);GUI.DrawTexture(new Rect(0,0,panelWidth+100,height),Texture2D.whiteTexture);GUI.color=color;
        GUILayout.BeginArea(new Rect(50,44,panelWidth,height-82));
        GUILayout.Label("CHICKEN HEIST",title);GUILayout.Space(10);
        GUILayout.Label(page==Page.Main?"Sitio do Recomeco":page==Page.Pause?"Jogo pausado":page==Page.Settings?"Configuracoes":page==Page.Failure?"Assalto interrompido":"",label);
        GUILayout.Space(28);
        if(page==Page.Main)
        {
            if(played && !HeistGameManager.Instance.missionEnded && Action("Voltar ao jogo"))Resume();
            GUI.enabled=HasSave;if(Action("Continuar jogo salvo"))RequestLoad();GUI.enabled=true;
            if(HasSave){GUILayout.Label(saveSummary,small);GUILayout.Space(18);}
            if(Action(HasSave?"Novo jogo":"Iniciar jogo"))BeginGame();
            if(Action("Configuracoes"))OpenSettings();
            if(Action("Sair")){returnPage=Page.Main;SetPage(Page.Quit);}
        }
        else if(page==Page.Pause)
        {
            if(Action("Continuar"))Resume();
            if(Action("Salvar jogo"))SaveProgress();
            GUI.enabled=HasSave;if(Action("Carregar jogo"))RequestLoad();GUI.enabled=true;
            if(Action("Configuracoes"))OpenSettings();
            if(Action("Menu principal"))SetPage(Page.Main);
            if(Action("Sair do jogo")){returnPage=Page.Pause;SetPage(Page.Quit);}
        }
        else if(page==Page.Settings)DrawSettings();
        else if(page==Page.NewGame)
        {
            GUILayout.Label("Iniciar uma nova historia? O progresso atual sera substituido. Uma copia do save anterior sera mantida.",label);GUILayout.Space(24);
            if(Action("Iniciar nova historia")){pendingFresh=true;Reload();}
            if(Action("Voltar"))SetPage(Page.Main);
        }
        else if(page==Page.Quit)
        {
            GUILayout.Label("Sair de Chicken Heist?",label);GUILayout.Space(24);
            if(played && !HeistGameManager.Instance.missionEnded && Action("Salvar e sair")){if(SaveProgress())Quit();}
            if(Action("Sair sem salvar"))Quit();
            if(Action("Voltar"))SetPage(returnPage);
        }
        else if(page==Page.Failure)
        {
            GUI.enabled=HasSave;if(Action("Carregar ultimo save"))LoadProgress();GUI.enabled=true;
            if(Action("Menu principal"))SetPage(Page.Main);
        }
        else if(page==Page.LoadConfirm)
        {
            GUILayout.Label("Carregar este progresso? Alteracoes posteriores ao save serao descartadas.",label);GUILayout.Space(18);
            GUILayout.Label(saveSummary,small);GUILayout.Space(24);
            if(Action("Carregar save"))LoadProgress();
            if(Action("Voltar"))SetPage(returnPage);
        }
        else if(page==Page.Loading)
        {
            GUILayout.Label("Carregando o vale...",label);GUILayout.Space(24);
            var bar=GUILayoutUtility.GetRect(200,5,GUILayout.ExpandWidth(true));
            GUI.DrawTexture(bar,normal);
            GUI.DrawTexture(new Rect(bar.x,bar.y,bar.width*Mathf.Clamp01((loading?.progress??.02f)/.9f),bar.height),selected);
        }
        GUILayout.FlexibleSpace();GUILayout.Label(feedback,small);GUILayout.EndArea();GUI.matrix=previous;
    }
    void DrawSettings()
    {
        GUI.enabled=confirmUntil<=0;
        int newTab=GUILayout.Toolbar(settingsTab,new[]{"Video","Audio","Controles"},tabStyle,GUILayout.Height(46));
        if(newTab!=settingsTab){settingsTab=newTab;scroll=Vector2.zero;}
        GUILayout.Space(18);
        scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(390));
        if(settingsTab==0)
        {
            var resolutions=Screen.resolutions.Select(r=>new Vector2Int(r.width,r.height)).Where(r=>r.x>=800 && r.y>=600).Distinct().ToList();
            var current=new Vector2Int(settings.width,settings.height);if(!resolutions.Contains(current))resolutions.Add(current);
            GUILayout.Label("Resolucao",label);GUILayout.BeginHorizontal();
            int index=resolutions.IndexOf(current);
            if(GUILayout.Button("<",tabStyle,GUILayout.Width(48)))current=resolutions[(index+resolutions.Count-1)%resolutions.Count];
            GUILayout.Label(current.x+" x "+current.y,label);
            if(GUILayout.Button(">",tabStyle,GUILayout.Width(48)))current=resolutions[(index+1)%resolutions.Count];
            settings.width=current.x;settings.height=current.y;GUILayout.EndHorizontal();
            settings.fullscreen=GUILayout.Toggle(settings.fullscreen,"Tela cheia",toggle);GUILayout.Space(12);
            GUILayout.Label("Qualidade grafica",label);
            settings.quality=GUILayout.SelectionGrid(settings.quality,new[]{"Baixa","Media","Alta"},3,tabStyle);GUILayout.Space(12);
            settings.vsync=GUILayout.Toggle(settings.vsync,"Sincronizacao vertical",toggle);
            GUILayout.Label("Limite de quadros",label);
            int[] caps={30,60,120,144,-1};int cap=System.Array.IndexOf(caps,settings.frameLimit);
            GUI.enabled=confirmUntil<=0 && !settings.vsync;int next=GUILayout.Toolbar(Mathf.Max(0,cap),new[]{"30","60","120","144","Livre"},tabStyle);GUI.enabled=confirmUntil<=0;
            settings.frameLimit=caps[next];
            Slider("Campo de visao",ref settings.fov,50,90);
        }
        else if(settingsTab==1)Slider("Volume geral",ref settings.volume,0,1,true);
        else
        {
            Slider("Sensibilidade do mouse",ref settings.sensitivity,20,300);
            settings.invertY=GUILayout.Toggle(settings.invertY,"Inverter eixo vertical",toggle);GUILayout.Space(20);
            GUILayout.Label("Movimento   WASD\nCorrer   Shift\nAgachar   C\nPular   Espaco\nInteragir   E\nCelular   Tab\nMochila   B\nTinta na camera   F\nPausa   Esc",small);
        }
        GUILayout.EndScrollView();GUI.enabled=true;GUILayout.Space(14);
        if(confirmUntil>0)
        {
            GUILayout.Label("Manter o modo de video? "+Mathf.CeilToInt(confirmUntil-Time.realtimeSinceStartup)+" s",label);
            if(Action("Manter")){confirmUntil=0;applied=settings.Copy();applied.Persist();feedback="Configuracoes salvas.";}
            if(Action("Reverter"))RevertVideo();
        }
        else
        {
            GUILayout.BeginHorizontal();if(Action("Aplicar"))ApplySettings();GUILayout.Space(12);
            if(Action("Restaurar padrao")){settings=GamePreferences.Defaults();feedback="Padrao selecionado. Clique em Aplicar para confirmar.";}
            GUILayout.Space(12);if(Action("Voltar"))LeaveSettings();GUILayout.EndHorizontal();
        }
    }
    void Slider(string name,ref float value,float min,float max,bool percent=false)
    {
        GUILayout.Label(name+"   "+Mathf.RoundToInt(percent?value*100:value)+(percent?"%":""),label);
        value=GUILayout.HorizontalSlider(value,min,max,GUILayout.Height(24));GUILayout.Space(8);
    }
}
