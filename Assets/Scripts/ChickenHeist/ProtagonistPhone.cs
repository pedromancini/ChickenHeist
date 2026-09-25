using UnityEngine;

[DefaultExecutionOrder(-100)]
public class ProtagonistPhone : MonoBehaviour
{
    public static bool IsOpen {get;private set;}
    public static int LastClosedFrame {get;private set;}=-1;
    public static ProtagonistPhone Instance {get;private set;}
    public HouseholdEconomy economy;
    public Texture2D[] farmPhotos;
    public string[] farmNames;
    public Vector3[] farmPositions;
    public int selectedFarm=-1;
    int tab,photoIndex,confirmProduct=-1;
    Vector2 scroll;
    CursorLockMode oldLock; bool oldCursor;
    GUIStyle title,body,small,button,heading;
    Texture2D roundedOuter,roundedScreen;
    readonly Color ink=new Color(.14f,.18f,.19f),green=new Color(.16f,.37f,.3f);
    public int ReviewTab {set {tab=value;scroll=Vector2.zero;}}
    void Awake(){Instance=this;IsOpen=false;}
    void Update()
    {
        if(GameMenu.BlocksInput || VillageMarket.IsOpen || VillageMarket.ClosedFrame==Time.frameCount)return;
        if(Input.GetKeyDown(KeyCode.Tab))
        {
            // Lockpick owns movement while active; do not steal its input focus.
            var movement=HeistGameManager.Instance?.player?.GetComponent<PlayerMovement>();
            if(IsOpen || movement==null || movement.enabled)SetOpen(!IsOpen);
        }
        if(IsOpen && Input.GetKeyDown(KeyCode.Escape))SetOpen(false);
    }
    public void SetOpen(bool open)
    {
        if(IsOpen==open)return;
        if(open){oldLock=Cursor.lockState;oldCursor=Cursor.visible;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
            if(economy!=null && economy.Account.newsUnread){tab=4;scroll=Vector2.zero;economy.ReadNews();}}
        else {Cursor.lockState=oldLock;Cursor.visible=oldCursor;confirmProduct=-1;LastClosedFrame=Time.frameCount;}
        IsOpen=open;
    }
    void OnDisable(){SetOpen(false);if(Instance==this)Instance=null;}
    void OnGUI()
    {
        if(GameMenu.IsOpen || StoryDirector.Active || HomeNextNight.IsResting)return;
        if(!IsOpen && economy!=null && economy.Account.newsUnread)
        {
            var notice=new GUIStyle(GUI.skin.box){fontSize=17,wordWrap=true};
            GUI.Box(new Rect(Screen.width-350,90,330,66),"VALE NOTICIAS\nAssalto na regiao. TAB para ler.",notice);
        }
        
        if(IsOpen && economy!=null && economy.Ready)DrawScreen(economy.Account,economy.Message);
    }
    public void DrawScreen(HouseholdAccount account,string message,Rect? phoneRect=null)
    {
        if(title==null)
        {
            body=new GUIStyle(GUI.skin.label){fontSize=16,wordWrap=true,padding=new RectOffset(0,0,4,4)};body.normal.textColor=ink;
            small=new GUIStyle(body){fontSize=13};
            title=new GUIStyle(body){fontSize=30,fontStyle=FontStyle.Bold};
            heading=new GUIStyle(body){fontSize=19,fontStyle=FontStyle.Bold};
            button=new GUIStyle(GUI.skin.button){fontSize=15,wordWrap=true,padding=new RectOffset(12,12,10,10)};
            button.normal.background=button.hover.background=button.active.background=button.focused.background=Texture2D.whiteTexture;
            button.normal.textColor=button.hover.textColor=button.active.textColor=button.focused.textColor=Color.white;
        }
        Matrix4x4 previous=GUI.matrix;Color old=GUI.color,oldBackground=GUI.backgroundColor;int depth=GUI.depth;GUI.depth=-100;
        GUI.backgroundColor=green;
        
        Rect frame=phoneRect??TabletRect(Screen.width,Screen.height);
        GUI.matrix=Matrix4x4.TRS(new Vector3(frame.x,frame.y),Quaternion.identity,new Vector3(frame.width/1180f,frame.height/800f,1));
        EnsureRoundedTextures();
        GUI.DrawTexture(new Rect(0,0,1180,800),roundedOuter,ScaleMode.StretchToFill);
        GUI.DrawTexture(new Rect(14,14,1152,772),roundedScreen,ScaleMode.StretchToFill);
        Fill(new Rect(6,376,4,48),new Color(.25f,.27f,.27f));
        GUILayout.BeginArea(new Rect(48,36,1084,716));
        GUILayout.BeginHorizontal();GUILayout.Label("VALE TABLET   /   DIA "+account.day,small);GUILayout.FlexibleSpace();
        if(GUILayout.Button("Fechar",button,GUILayout.Width(96)))SetOpen(false);GUILayout.EndHorizontal();
        GUILayout.Label(new[]{"Meu banco","Minhas contas","Fazendas","Cooperativa","Vale Noticias"}[tab],title);
        GUILayout.Label("Sitio do Recomeco",small);GUILayout.Space(8);
        GUILayout.BeginHorizontal();
        string[] tabs={"Banco","Dividas","Fotos","Loja","Jornal"};
        for(int i=0;i<tabs.Length;i++){GUI.backgroundColor=i==tab?green:new Color(.42f,.46f,.45f);if(GUILayout.Button(tabs[i],button)){tab=i;scroll=Vector2.zero;confirmProduct=-1;if(i==4)economy?.ReadNews();}}
        GUI.backgroundColor=green;GUILayout.EndHorizontal();GUILayout.Space(12);
        scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(488));
        if(tab==0)
        {
            GUILayout.Label("SALDO DISPONIVEL",small);GUILayout.Label("R$ "+account.balance+",00",title);
            GUILayout.Label(account.flock+" galinhas no sitio  |  "+account.meals+" porcoes no comedouro",body);
            GUI.enabled=economy!=null && economy.AtHome && account.flock>0;
            if(GUILayout.Button("Vender 1 galinha  + R$ 45",button))economy.Sell();GUI.enabled=true;
            GUILayout.Space(16);GUILayout.Label("Extrato",heading);
            foreach(string entry in account.ledger)GUILayout.Label(entry,small);
        }
        else if(tab==1)
        {
            int total=0;foreach(var d in account.debts)total+=d.amount;
            GUILayout.Label("Falta pagar: R$ "+total+",00",heading);GUILayout.Space(8);
            for(int i=0;i<account.debts.Count;i++)
            {
                var debt=account.debts[i];GUILayout.Label(debt.label,heading);
                GUILayout.Label(debt.amount==0?"QUITADA":"R$ "+debt.amount+"  |  "+(account.day>debt.dueDay?"VENCIDA":"Vence dia "+debt.dueDay),body);
                GUI.enabled=economy!=null && debt.amount>0 && account.balance>=debt.amount;
                if(GUILayout.Button("Pagar conta",button))economy.Pay(i);GUI.enabled=true;GUILayout.Space(12);
            }
        }
        else if(tab==2)
        {
            int count=farmPhotos==null?0:farmPhotos.Length;
            if(count==0)GUILayout.Label("Nenhuma foto no aparelho.",body);
            else
            {
                photoIndex=Mathf.Clamp(photoIndex,0,count-1);
                Rect rect=GUILayoutUtility.GetRect(1000,300,GUILayout.ExpandWidth(true));
                if(farmPhotos[photoIndex]!=null)GUI.DrawTexture(rect,farmPhotos[photoIndex],ScaleMode.ScaleToFit);
                GUILayout.Label(farmNames[photoIndex],heading);GUILayout.Label("Foto de reconhecimento  |  "+(photoIndex+1)+" / "+count,small);
                GUILayout.BeginHorizontal();if(GUILayout.Button("Anterior",button))photoIndex=(photoIndex+count-1)%count;
                if(GUILayout.Button("Proxima",button))photoIndex=(photoIndex+1)%count;GUILayout.EndHorizontal();
                var game=HeistGameManager.Instance;
                bool active=game!=null && game.MissionActive;
                GUI.enabled=game!=null && game.MissionStartBlocker.Length==0;
                if(GUILayout.Button(active && game.MissionFarm==photoIndex?"Missao em andamento":"Iniciar missao",button))
                {
                    if(game.StartMission(photoIndex))SetOpen(false);
                }
                GUI.enabled=true;
                GUILayout.Label(game!=null?game.MissionStartBlocker:"",small);
            }
        }
        else if(tab==4)
        {
            GUILayout.Label("O JORNAL DA NOSSA REGIAO",small);GUILayout.Space(12);
            if(account.news==null || account.news.Count==0)
                GUILayout.Label("Manha tranquila no vale. Nenhum assalto foi noticiado.",body);
            else foreach(var report in account.news)
            {
                GUILayout.Label("DIA "+report.day+"  |  SEGURANCA RURAL",small);
                GUILayout.Label(report.Headline,heading);GUILayout.Space(8);
                GUILayout.Label(report.Story,body);GUILayout.Space(16);
                GUILayout.Label("Protecao em conjunto",heading);
                GUILayout.Label("As fazendas do vale agora contam com cameras e armadilhas. A vizinhanca esta mais atenta.",body);
                GUILayout.Space(24);
            }
        }
        else
        {
            GUILayout.Label("R$ "+account.balance+",00 disponiveis",heading);
            for(int i=0;i<HouseholdAccount.ProductCount;i++)
            {
                GUILayout.Label(HouseholdAccount.ProductName(i),heading);GUILayout.Label("R$ "+HouseholdAccount.ProductPrice(i)+",00",small);
                if(i==3)GUILayout.Label("Maior tolerancia de pressao | Equipamento permanente",small);
                if(i==4)GUILayout.Label("3 aplicacoes  |  60 s por camera",small);
                if(i==5)GUILayout.Label("2 galinhas por gaiola | "+account.truckCages+"/4 instaladas | Maximo 8 galinhas",small);
                GUI.enabled=economy!=null && economy.Ready && account.CanBuy(i);
                if(GUILayout.Button(account.OwnsUniqueProduct(i)?"Adquirido":confirmProduct==i?"Confirmar compra":"Comprar",button))
                {if(confirmProduct==i){economy.Buy(i);confirmProduct=-1;}else confirmProduct=i;}GUI.enabled=true;GUILayout.Space(8);
            }
            GUILayout.Space(8);GUILayout.Label("Estoque do sitio",heading);
            GUILayout.Label("Racao: "+account.feed+"  |  Kits de tabuas e pregos: "+account.boards,small);
            GUILayout.Label("Galinheiro — nivel "+account.CoopLevel+"/3",heading);
            GUILayout.Label(account.CoopBenefit,small);
            if(account.CoopLevel<3)
            {
                int required=account.NextCoopBoards;
                GUILayout.Label("Proximo: "+(account.CoopLevel==0?"estrutura reforcada":account.CoopLevel==1?"cobertura e tela":"ninhos e comedouro"),small);
                GUILayout.Label("Kits de tabuas e pregos: "+account.boards+"/"+required+" — faltam "+Mathf.Max(0,required-account.boards),small);
                GUILayout.Label("Beneficio: "+(account.FeedPortions+1)+" porcoes por racao e R$ "+(account.EggIncome+2)+" por galinha alimentada.",small);
            }
            GUI.enabled=economy!=null && economy.AtHome;
            if(GUILayout.Button("Abastecer comedouro",button))economy.UseFeed();
            GUI.enabled=economy!=null && economy.AtHome && account.CoopLevel<3 && account.boards>=account.NextCoopBoards;
            if(GUILayout.Button(account.CoopLevel==3?"Galinheiro completo":"Aplicar melhoria",button))economy.Repair();GUI.enabled=true;
        }
        GUILayout.EndScrollView();GUILayout.Space(8);GUILayout.Label(message,small);GUILayout.EndArea();
        Fill(new Rect(540,775,100,4),new Color(.6f,.61f,.6f));
        GUI.matrix=previous;GUI.color=old;GUI.backgroundColor=oldBackground;GUI.depth=depth;GUI.enabled=true;
    }
    public static Rect TabletRect(float width,float height)
    {
        float scale=Mathf.Min(width*.90f/1180f,height*.92f/800f);
        return new Rect((width-1180*scale)*.5f,(height-800*scale)*.5f,1180*scale,800*scale);
    }
    void EnsureRoundedTextures()
    {
        if(roundedOuter!=null)return;
        roundedOuter=RoundedTexture(1180,800,28,new Color(.035f,.043f,.045f));
        roundedScreen=RoundedTexture(1152,772,22,new Color(.94f,.95f,.93f));
    }
    static Texture2D RoundedTexture(int width,int height,int radius,Color color)
    {
        var texture=new Texture2D(width,height,TextureFormat.RGBA32,false,true);texture.name="Tablet UI rounded surface";
        var pixels=new Color[width*height];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            float dx=Mathf.Max(radius-x,0,x-(width-radius-1));float dy=Mathf.Max(radius-y,0,y-(height-radius-1));
            bool inside=dx*dx+dy*dy<=radius*radius;pixels[y*width+x]=inside?color:new Color(0,0,0,0);
        }
        texture.SetPixels(pixels);texture.Apply(false,true);return texture;
    }
    static void Fill(Rect r,Color c){Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
}

