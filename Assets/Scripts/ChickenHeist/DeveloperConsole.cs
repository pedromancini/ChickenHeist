using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-600)]
public class DeveloperConsole : MonoBehaviour
{
    public static DeveloperConsole Instance { get; private set; }
    public static bool IsOpen => Instance!=null && Instance.open;
    public static bool BlocksInput => IsOpen || closedFrame==Time.frameCount;
    static int closedFrame=-1;
    public bool HelpVisible { get; private set; }
    bool open,focus;
    int openedFrame,historyIndex;
    string input="",draft="",notice="";
    float noticeUntil,previousTime;
    bool previousAudio,previousCursor;
    CursorLockMode previousLock;
    readonly List<string> output=new List<string>();
    readonly List<string> history=new List<string>();
    Vector2 scroll;
    GUIStyle text,heading,field;
    const string Help="/fase N   Inicia e vai ate a fazenda N\n/fases   Lista numeros e nomes\n/speed N   Velocidade x1 a x20 (1 normal)\n/casa   Volta ao sitio\n/mercado   Vai ao entreposto\n/sono N   Define alerta (0 a 100)\n/abrir   Abre o galinheiro da missao\n/fechar   Fecha o galinheiro da missao\n/mochila N   Define galinhas de teste\n/cancelar   Encerra a missao, sem entrega\n/pos   Mostra a posicao atual\n/salvar   Grava o progresso atual\n/limpar   Limpa o historico do chat\n/dev   Mostra ou oculta esta janela";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Instance=null;closedFrame=-1;}
    void Awake(){Instance=this;}
    bool InteractionBusy()=>ProtagonistPhone.IsOpen || VillageMarket.IsOpen || BackpackPanel.BlocksInput ||
        FindObjectsByType<ChickenCoopLockpick>().Any(c=>c.ChallengeActive);
    public bool Open()
    {
        if(open || GameMenu.IsOpen || HeistGameManager.Instance==null || HeistGameManager.Instance.missionEnded || InteractionBusy())return false;
        previousTime=Time.timeScale;previousAudio=AudioListener.pause;
        previousLock=Cursor.lockState;previousCursor=Cursor.visible;
        open=true;openedFrame=Time.frameCount;focus=true;input="";draft="";historyIndex=history.Count;
        Time.timeScale=0;AudioListener.pause=true;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        return true;
    }
    public void Close()
    {
        if(!open)return;
        open=false;closedFrame=Time.frameCount;
        if(!GameMenu.IsOpen)
        {Time.timeScale=previousTime;AudioListener.pause=previousAudio;Cursor.lockState=previousLock;Cursor.visible=previousCursor;}
    }
    void OnDisable(){Close();if(Instance==this)Instance=null;}
    void Update()
    {
        if(open && GameMenu.IsOpen){Close();return;}
        if(!open && Input.GetKeyDown(KeyCode.T))Open();
        if(open && Input.GetKeyDown(KeyCode.Escape))Close();
    }
    void LateUpdate(){if(open){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
    void Reply(string value)
    {
        output.Add(value);if(output.Count>60)output.RemoveAt(0);
        scroll.y=float.MaxValue;notice=value;noticeUntil=Time.unscaledTime+5;
    }
    public bool ExecuteCommand(string raw)
    {
        var game=HeistGameManager.Instance;
        if(GameMenu.IsOpen || game==null || game.player==null || game.missionEnded || InteractionBusy())
        {Reply("Entre no jogo e conclua a interacao antes de usar comandos.");return false;}
        string[] args=(raw??"").Trim().Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
        if(args.Length==0)return false;
        string command=args[0].ToLowerInvariant();
        int number=0;
        bool numeric=command=="/fase" || command=="/sono" || command=="/mochila" || command=="/speed";
        if((numeric && (args.Length!=2 || !int.TryParse(args[1],out number))) || (!numeric && args.Length!=1))
        {Reply("Argumentos invalidos. Consulte /dev.");return false;}
        switch(command)
        {
            case "/dev":case "/ajuda":HelpVisible=!HelpVisible;Reply(HelpVisible?"Painel DEV ativado.":"Painel DEV oculto.");return true;
            case "/limpar":output.Clear();notice="";return true;
            case "/fases":
                var names=ProtagonistPhone.Instance?.farmNames;
                if(names==null){Reply("Fazendas indisponiveis.");return false;}
                Reply(string.Join("\n",names.Select((name,index)=>(index+1)+" - "+name)));return true;
            case "/fase":return StartPhase(number);
            case "/speed":
                var movement=game.player.GetComponent<PlayerMovement>();
                if(movement==null || !movement.SetDeveloperSpeed(number)){Reply("Use /speed de 1 a 20. /speed 1 volta ao normal.");return false;}
                Reply("Velocidade DEV: "+number+"x. Temporaria; /speed 1 restaura o normal.");return true;
            case "/casa":
                var home=HouseholdEconomy.Instance?.home;
                return home!=null && Travel(home.position+Vector3.back*12,home.position,"No sitio. A missao e a mochila foram mantidas.");
            case "/mercado":
                var market=FindAnyObjectByType<VillageMarket>();
                if(market?.counter==null){Reply("Mercado indisponivel.");return false;}
                return Travel(market.counter.position-market.counter.forward*2.5f,market.counter.position,"No entreposto.");
            case "/sono":
                if(!game.MissionActive || game.farmerSleep==null){Reply("Inicie uma fase antes de ajustar o alerta.");return false;}
                if(number<0 || number>100){Reply("Use /sono de 0 a 100. Zero restaura o sono minimo.");return false;}
                game.farmerSleep.RestoreSleep(number);Reply("Alerta: "+game.farmerSleep.CurrentSleep.ToString("0")+"%.");return true;
            case "/abrir":case "/fechar":
                var coops=FindObjectsByType<ChickenCoopLockpick>().Where(c=>game.IsMissionTarget(c)).ToArray();
                if(coops.Length==0){Reply("Inicie uma fase com galinheiro primeiro.");return false;}
                foreach(var coop in coops)coop.RestoreOpen(command=="/abrir");
                Reply(command=="/abrir"?"Galinheiro aberto.":"Galinheiro fechado.");return true;
            case "/mochila":
                if(number<0 || number>game.backpack.capacity){Reply("Use /mochila de 0 a "+game.backpack.capacity+".");return false;}
                game.backpack.RestoreCount(number);Reply("Mochila de teste: "+number+". Entregar ou vender afeta seu progresso.");return true;
            case "/cancelar":game.EndActiveMission();Reply("Missao cancelada, sem entrega. Mochila preservada.");return true;
            case "/pos":Reply("Posicao: "+game.player.position.ToString("F2"));return true;
            case "/salvar":
                bool saved=GameMenu.Instance!=null && GameMenu.Instance.SaveProgress();
                Reply(saved?"Progresso salvo, incluindo alteracoes DEV.":"Nao foi possivel salvar o progresso.");return saved;
            default:Reply("Comando desconhecido: "+command+". Consulte /dev.");return false;
        }
    }
    bool StartPhase(int number)
    {
        var game=HeistGameManager.Instance;var phone=ProtagonistPhone.Instance;
        if(phone?.farmNames==null || number<1 || number>phone.farmNames.Length || phone.farmPositions==null || number>phone.farmPositions.Length)
        {Reply("Fase invalida. Use /fases para listar.");return false;}
        if(game.backpack.chickensCarried>0){Reply("Esvazie a mochila antes: entregue as galinhas ou use /mochila 0.");return false;}
        var farm=FindObjectsByType<FarmLayoutInfo>().FirstOrDefault(f=>f.identity==phone.farmNames[number-1]);
        if(farm==null || farm.GetComponentInChildren<FarmerSleepSystem>()==null){Reply("Esta fazenda nao tem missao configurada.");return false;}
        Vector3 middle=new Vector3(farm.lot.center.x,farm.entrance.y,farm.lot.center.y);
        Vector3 outward=(farm.entrance-middle).normalized;
        if(outward.sqrMagnitude<.1f)outward=Vector3.back;
        if(!FindLanding(farm.entrance+outward*5,out var landing)){Reply("Nao encontrei chao livre na entrada. Nenhuma missao foi alterada.");return false;}
        game.EndActiveMission();
        if(!game.StartMission(number-1)){Reply("Nao foi possivel iniciar a missao.");return false;}
        Teleport(landing,middle);
        Reply("Fase "+number+": "+game.MissionName+". Posicionado na entrada.");return true;
    }
    bool Travel(Vector3 destination,Vector3 lookAt,string message)
    {
        if(!FindLanding(destination,out var landing)){Reply("Nao encontrei chao livre neste destino.");return false;}
        Teleport(landing,lookAt);Reply(message);return true;
    }
    static bool FindLanding(Vector3 destination,out Vector3 landing)
    {
        var player=HeistGameManager.Instance.player;
        var controller=player.GetComponent<CharacterController>();
        float radius=controller.radius*Mathf.Max(Mathf.Abs(player.lossyScale.x),Mathf.Abs(player.lossyScale.z));
        float height=Mathf.Max(radius*2,player.GetComponent<PlayerMovement>().alturaEmPe*Mathf.Abs(player.lossyScale.y));
        Physics.SyncTransforms();
        for(int ring=0;ring<=4;ring++)for(int step=0;step<(ring==0?1:12);step++)
        {
            float angle=step*Mathf.PI/6;
            var probe=destination+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*ring*1.5f;
            var hits=Physics.RaycastAll(probe+Vector3.up*60,Vector3.down,160,~0,QueryTriggerInteraction.Ignore);
            foreach(var hit in hits.OrderByDescending(h=>h.point.y))
            {
                if(hit.transform.IsChildOf(player) || Vector3.Angle(hit.normal,Vector3.up)>controller.slopeLimit)continue;
                var feet=hit.point+Vector3.up*.08f;
                bool blocked=Physics.OverlapCapsule(feet+Vector3.up*radius,feet+Vector3.up*(height-radius),radius,~0,QueryTriggerInteraction.Ignore)
                    .Any(c=>!c.transform.IsChildOf(player));
                if(!blocked){landing=feet;return true;}
            }
        }
        landing=default;return false;
    }
    static void Teleport(Vector3 position,Vector3 lookAt)
    {
        var player=HeistGameManager.Instance.player;var controller=player.GetComponent<CharacterController>();
        bool wasEnabled=controller.enabled;controller.enabled=false;
        player.position=position;var direction=lookAt-position;direction.y=0;
        if(direction.sqrMagnitude>.01f)player.rotation=Quaternion.LookRotation(direction);
        player.GetComponent<PlayerMovement>().RestorePosture(false);
        player.GetComponentInChildren<PlayerLook>()?.RestorePitch(0);
        controller.enabled=wasEnabled;Physics.SyncTransforms();
    }
    void Submit()
    {
        string command=input.Trim();if(command.Length==0){Close();return;}
        if(history.Count==0 || history[history.Count-1]!=command)history.Add(command);
        if(history.Count>40)history.RemoveAt(0);
        Reply("> "+command);ExecuteCommand(command);Close();
    }
    void OnGUI()
    {
        if(GameMenu.IsOpen || (!open && !HelpVisible && Time.unscaledTime>=noticeUntil))return;
        if(text==null)
        {
            text=new GUIStyle(GUI.skin.label){fontSize=14,wordWrap=true,normal={textColor=new Color(.9f,.94f,.91f)}};
            heading=new GUIStyle(text){fontSize=18,fontStyle=FontStyle.Bold};
            field=new GUIStyle(GUI.skin.textField){fontSize=17,padding=new RectOffset(10,10,8,8)};
        }
        var matrix=GUI.matrix;var color=GUI.color;int depth=GUI.depth;GUI.depth=-100;
        float scale=Mathf.Min(1,Screen.width/800f,Screen.height/650f);
        GUI.matrix=Matrix4x4.Scale(Vector3.one*scale);
        float width=Screen.width/scale,height=Screen.height/scale;
        if(HelpVisible)
        {
            float y=Mathf.Max(12,open?Mathf.Min((height-386)*.5f,height-612):(height-386)*.5f);
            Panel(new Rect(16,y,355,386));
            GUI.Label(new Rect(30,y+10,325,28),"DEV / COMANDOS",heading);
            GUI.Label(new Rect(30,y+44,325,292),Help,text);
            GUI.Label(new Rect(30,y+340,325,42),"T abre o chat. Comandos alteram o jogo atual; salvar grava as alteracoes.",text);
        }
        if(open)
        {
            float w=Mathf.Min(710,width-32),y=height-210;
            Panel(new Rect(16,y,w,194));
            GUI.Label(new Rect(30,y+8,w-28,26),"CONSOLE DEV  /  JOGO PAUSADO",heading);
            GUILayout.BeginArea(new Rect(30,y+38,w-28,102));
            scroll=GUILayout.BeginScrollView(scroll);
            foreach(string line in output)GUILayout.Label(line,text);
            GUILayout.EndScrollView();GUILayout.EndArea();
            Event e=Event.current;
            if(Time.frameCount==openedFrame && e.type==EventType.KeyDown)e.Use();
            if(e.type==EventType.KeyDown && (e.keyCode==KeyCode.Return || e.keyCode==KeyCode.KeypadEnter)){e.Use();Submit();}
            else if(e.type==EventType.KeyDown && (e.keyCode==KeyCode.UpArrow || e.keyCode==KeyCode.DownArrow))
            {
                if(historyIndex==history.Count)draft=input;
                historyIndex=Mathf.Clamp(historyIndex+(e.keyCode==KeyCode.UpArrow?-1:1),0,history.Count);
                input=historyIndex==history.Count?draft:history[historyIndex];focus=true;e.Use();
            }
            GUI.SetNextControlName("DevInput");input=GUI.TextField(new Rect(30,y+148,w-28,36),input,180,field);
            if(focus && Time.frameCount>openedFrame){GUI.FocusControl("DevInput");focus=false;}
        }
        else if(Time.unscaledTime<noticeUntil && notice.Length>0)
        {
            float w=Mathf.Min(710,width-32);Panel(new Rect(16,height-98,w,82));
            GUI.Label(new Rect(30,height-90,w-28,66),notice.Split('\n')[0],text);
        }
        GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;
    }
    static void Panel(Rect rect)
    {
        var color=GUI.color;GUI.color=new Color(.035f,.05f,.043f,.96f);
        GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=color;
    }
}
