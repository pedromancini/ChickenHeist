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
    GUIStyle title,body,small,caption,button,primary,tabStyle,heading,amount,card,rowTitle,rowText;
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
            GUI.skin=UITheme.Skin;float s=UITheme.Scale;
            var r=new Rect(Screen.width-UITheme.Size(360),UITheme.Size(20),UITheme.Size(340),UITheme.Size(76));UITheme.Panel(r,UITheme.PanelFill,UITheme.Accent);
            GUI.Label(new Rect(r.x+14*s,r.y+8*s,r.width-28*s,20*s),"VALE NOTÍCIAS",new GUIStyle(UITheme.Style("caption")){normal={textColor=UITheme.Accent}});
            GUI.Label(new Rect(r.x+14*s,r.y+28*s,r.width-80*s,40*s),"Assalto na região.",new GUIStyle(UITheme.Style("hud")){alignment=TextAnchor.MiddleLeft});
            GUI.Label(new Rect(r.xMax-58*s,r.y+34*s,44*s,28*s),"Tab",UITheme.Style("key"));
        }
        
        if(IsOpen && economy!=null && economy.Ready)DrawScreen(economy.Account,economy.Message);
    }
    public void DrawScreen(HouseholdAccount account,string message,Rect? phoneRect=null)
    {
        GUI.skin=UITheme.Skin;
        // The tablet is laid out in a fixed 1180x800 space and scaled by GUI.matrix, so its sizes are absolute.
        if(title==null)
        {
            body=new GUIStyle(UITheme.Skin.label){font=UITheme.Body,fontSize=17,wordWrap=true,padding=new RectOffset(0,0,3,3),normal={textColor=UITheme.Ink}};
            small=new GUIStyle(body){fontSize=14,normal={textColor=UITheme.Muted}};
            caption=new GUIStyle(body){font=UITheme.BodyBold,fontSize=14,normal={textColor=new Color(.66f,.60f,.50f)}};
            title=new GUIStyle(body){font=UITheme.TitleFont,fontSize=36,wordWrap=false,normal={textColor=UITheme.Ink}};
            heading=new GUIStyle(body){font=UITheme.TitleFont,fontSize=21,normal={textColor=UITheme.Ink}};
            amount=new GUIStyle(title){fontSize=46,normal={textColor=UITheme.Accent}};
            rowTitle=new GUIStyle(heading){wordWrap=false};rowText=new GUIStyle(small){wordWrap=false};
            var pad=new RectOffset(16,16,10,10);var margin=new RectOffset(0,0,4,6);
            button=new GUIStyle(UITheme.Style("tab")){fontSize=16,padding=pad,margin=margin};
            primary=new GUIStyle(UITheme.Style("primary")){fontSize=16,padding=pad,margin=margin};
            tabStyle=new GUIStyle(UITheme.Style("tab")){fontSize=16,padding=new RectOffset(18,18,9,9),margin=new RectOffset(0,8,0,0)};
            card=new GUIStyle{border=new RectOffset(13,13,13,13),padding=new RectOffset(20,20,14,16),margin=new RectOffset(0,0,0,12),
                normal={background=UITheme.Rounded(new Color(.13f,.115f,.095f,.96f),new Color(.32f,.26f,.18f,.9f),12)}};
        }
        Matrix4x4 previous=GUI.matrix;Color old=GUI.color,oldBackground=GUI.backgroundColor;int depth=GUI.depth;GUI.depth=-100;

        Rect frame=phoneRect??TabletRect(Screen.width,Screen.height);
        GUI.matrix=Matrix4x4.TRS(new Vector3(frame.x,frame.y),Quaternion.identity,new Vector3(frame.width/1180f,frame.height/800f,1));
        UITheme.Panel(new Rect(0,0,1180,800),new Color(.03f,.028f,.025f,1),new Color(.2f,.17f,.13f,1),28);
        UITheme.Panel(new Rect(14,14,1152,772),new Color(.075f,.066f,.055f,1),new Color(0,0,0,0),22);
        Fill(new Rect(6,376,4,48),new Color(.22f,.2f,.17f));
        GUILayout.BeginArea(new Rect(48,32,1084,724));
        GUILayout.BeginHorizontal();
        GUILayout.Label("VALE TABLET  ·  DIA "+account.day,caption);GUILayout.FlexibleSpace();
        GUILayout.Label("R$ "+account.balance+",00",new GUIStyle(caption){normal={textColor=UITheme.Accent}});GUILayout.Space(16);
        if(GUILayout.Button("Fechar",button,GUILayout.Width(100)))SetOpen(false);GUILayout.EndHorizontal();
        GUILayout.Label(new[]{"Meu banco","Minhas contas","Fazendas","Cooperativa","Vale Notícias"}[tab],title);
        GUILayout.Label("Sítio do Recomeço",new GUIStyle(small){normal={textColor=UITheme.Accent}});GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        string[] tabs={"Banco","Dívidas","Fotos","Loja","Jornal"};
        for(int i=0;i<tabs.Length;i++)
        {
            string text=tabs[i]+(i==4 && account.newsUnread?"  •":"");
            if(GUILayout.Toggle(i==tab,text,tabStyle,GUILayout.MinWidth(120)) && i!=tab){tab=i;scroll=Vector2.zero;confirmProduct=-1;if(i==4)economy?.ReadNews();}
        }
        GUILayout.FlexibleSpace();GUILayout.EndHorizontal();GUILayout.Space(14);
        scroll=GUILayout.BeginScrollView(scroll,GUILayout.Height(482));
        if(tab==0)
        {
            GUILayout.BeginVertical(card);
            GUILayout.Label("SALDO DISPONÍVEL",caption);GUILayout.Label("R$ "+account.balance+",00",amount);
            GUILayout.Label(account.flock+" galinhas no sítio  ·  "+account.meals+" porções no comedouro",body);
            GUI.enabled=economy!=null && economy.AtHome && account.flock>0;
            if(GUILayout.Button("Vender 1 galinha  ·  + R$ 45",primary,GUILayout.Width(300)))economy.Sell();GUI.enabled=true;
            if(economy!=null && !economy.AtHome)GUILayout.Label("Vendas só podem ser feitas no sítio.",small);
            GUILayout.EndVertical();
            GUILayout.BeginVertical(card);
            GUILayout.Label("Extrato",heading);GUILayout.Space(4);
            if(account.ledger.Count==0)GUILayout.Label("Nenhuma movimentação ainda.",small);
            foreach(string entry in account.ledger)GUILayout.Label(entry,small);
            GUILayout.EndVertical();
        }
        else if(tab==1)
        {
            int total=0;foreach(var d in account.debts)total+=d.amount;
            GUILayout.BeginVertical(card);
            GUILayout.Label("FALTA PAGAR",caption);GUILayout.Label("R$ "+total+",00",new GUIStyle(amount){normal={textColor=total>0?UITheme.Accent:UITheme.Good}});
            GUILayout.EndVertical();
            for(int i=0;i<account.debts.Count;i++)
            {
                var debt=account.debts[i];bool overdue=debt.amount>0 && account.day>debt.dueDay;
                GUILayout.BeginVertical(card);
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                GUILayout.Label(debt.label,rowTitle);
                GUILayout.Label(debt.amount==0?"QUITADA":"R$ "+debt.amount+",00  ·  "+(overdue?"VENCIDA":"vence no dia "+debt.dueDay),
                    new GUIStyle(body){wordWrap=false,normal={textColor=debt.amount==0?UITheme.Good:overdue?UITheme.Danger:UITheme.Ink}});
                GUILayout.EndVertical();GUILayout.FlexibleSpace();
                GUI.enabled=economy!=null && debt.amount>0 && account.balance>=debt.amount;
                if(debt.amount>0 && GUILayout.Button("Pagar conta",primary,GUILayout.Width(180),GUILayout.Height(46)))economy.Pay(i);GUI.enabled=true;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
        }
        else if(tab==2)
        {
            int count=farmPhotos==null?0:farmPhotos.Length;
            if(count==0)GUILayout.Label("Nenhuma foto no aparelho.",body);
            else
            {
                photoIndex=Mathf.Clamp(photoIndex,0,count-1);
                GUILayout.BeginVertical(card);
                Rect rect=GUILayoutUtility.GetRect(1000,300,GUILayout.ExpandWidth(true));
                if(farmPhotos[photoIndex]!=null)GUI.DrawTexture(rect,farmPhotos[photoIndex],ScaleMode.ScaleToFit);
                GUILayout.Space(8);
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                GUILayout.Label(farmNames[photoIndex],rowTitle);GUILayout.Label("Foto de reconhecimento  ·  "+(photoIndex+1)+" / "+count,rowText);
                GUILayout.EndVertical();GUILayout.FlexibleSpace();
                if(GUILayout.Button("‹  Anterior",button,GUILayout.Width(140)))photoIndex=(photoIndex+count-1)%count;
                if(GUILayout.Button("Próxima  ›",button,GUILayout.Width(140)))photoIndex=(photoIndex+1)%count;
                GUILayout.EndHorizontal();
                var game=HeistGameManager.Instance;
                bool active=game!=null && game.MissionActive;
                GUI.enabled=game!=null && game.MissionStartBlocker.Length==0;
                if(GUILayout.Button(active && game.MissionFarm==photoIndex?"Missão em andamento":"Iniciar missão",primary,GUILayout.Height(48)))
                {
                    if(game.StartMission(photoIndex))SetOpen(false);
                }
                GUI.enabled=true;
                if(game!=null && game.MissionStartBlocker.Length>0)GUILayout.Label(game.MissionStartBlocker,new GUIStyle(small){normal={textColor=UITheme.Danger}});
                GUILayout.EndVertical();
            }
        }
        else if(tab==4)
        {
            GUILayout.Label("O JORNAL DA NOSSA REGIÃO",caption);GUILayout.Space(8);
            if(account.news==null || account.news.Count==0)
            {
                GUILayout.BeginVertical(card);GUILayout.Label("Manhã tranquila no vale. Nenhum assalto foi noticiado.",body);GUILayout.EndVertical();
            }
            else foreach(var report in account.news)
            {
                GUILayout.BeginVertical(card);
                GUILayout.Label("DIA "+report.day+"  ·  SEGURANÇA RURAL",new GUIStyle(caption){normal={textColor=UITheme.Danger}});
                GUILayout.Label(report.Headline,new GUIStyle(heading){fontSize=25});GUILayout.Space(6);
                GUILayout.Label(report.Story,body);GUILayout.Space(12);
                GUILayout.Label("Proteção em conjunto",heading);
                GUILayout.Label("As fazendas do vale agora contam com câmeras e armadilhas. A vizinhança está mais atenta.",small);
                GUILayout.EndVertical();
            }
        }
        else
        {
            GUILayout.Label("R$ "+account.balance+",00 disponíveis",new GUIStyle(heading){normal={textColor=UITheme.Accent}});GUILayout.Space(6);
            for(int i=0;i<HouseholdAccount.ProductCount;i++)
            {
                GUILayout.BeginVertical(card);
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
                GUILayout.Label(HouseholdAccount.ProductName(i),rowTitle);
                GUILayout.Label("R$ "+HouseholdAccount.ProductPrice(i)+",00",new GUIStyle(body){font=UITheme.BodyBold,wordWrap=false,normal={textColor=UITheme.Accent}});
                if(i==3)GUILayout.Label("Maior tolerância de pressão  ·  equipamento permanente",rowText);
                if(i==4)GUILayout.Label("3 aplicações  ·  60 s por câmera",rowText);
                if(i==5)GUILayout.Label("2 galinhas por gaiola  ·  "+account.truckCages+"/4 instaladas  ·  máximo 8 galinhas",rowText);
                GUILayout.EndVertical();GUILayout.FlexibleSpace();
                GUI.enabled=economy!=null && economy.Ready && account.CanBuy(i);
                bool owned=account.OwnsUniqueProduct(i);
                if(GUILayout.Button(owned?"Adquirido":confirmProduct==i?"Confirmar compra":"Comprar",confirmProduct==i?primary:button,GUILayout.Width(200),GUILayout.Height(46)))
                {if(confirmProduct==i){economy.Buy(i);confirmProduct=-1;}else confirmProduct=i;}GUI.enabled=true;
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
            }
            GUILayout.BeginVertical(card);
            GUILayout.Label("Estoque do sítio",heading);
            GUILayout.Label("Ração: "+account.feed+"  ·  Kits de tábuas e pregos: "+account.boards,small);
            GUILayout.Space(8);
            GUILayout.Label("Galinheiro — nível "+account.CoopLevel+"/3",heading);
            var level=GUILayoutUtility.GetRect(200,8,GUILayout.Width(320));UITheme.Meter(level,account.CoopLevel/3f,UITheme.Good);GUILayout.Space(6);
            GUILayout.Label(account.CoopBenefit,small);
            if(account.CoopLevel<3)
            {
                int required=account.NextCoopBoards;
                GUILayout.Label("Próximo: "+(account.CoopLevel==0?"estrutura reforçada":account.CoopLevel==1?"cobertura e tela":"ninhos e comedouro"),small);
                GUILayout.Label("Kits de tábuas e pregos: "+account.boards+"/"+required+" — faltam "+Mathf.Max(0,required-account.boards),small);
                GUILayout.Label("Benefício: "+(account.FeedPortions+1)+" porções por ração e R$ "+(account.EggIncome+2)+" por galinha alimentada.",small);
            }
            GUILayout.Space(6);GUILayout.BeginHorizontal();
            GUI.enabled=economy!=null && economy.AtHome;
            if(GUILayout.Button("Abastecer comedouro",button,GUILayout.Width(240)))economy.UseFeed();
            GUI.enabled=economy!=null && economy.AtHome && account.CoopLevel<3 && account.boards>=account.NextCoopBoards;
            if(GUILayout.Button(account.CoopLevel==3?"Galinheiro completo":"Aplicar melhoria",primary,GUILayout.Width(240)))economy.Repair();GUI.enabled=true;
            GUILayout.FlexibleSpace();GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
        GUILayout.EndScrollView();GUILayout.Space(6);GUILayout.Label(message,small);GUILayout.EndArea();
        Fill(new Rect(540,775,100,4),new Color(.38f,.33f,.26f));
        GUI.matrix=previous;GUI.color=old;GUI.backgroundColor=oldBackground;GUI.depth=depth;GUI.enabled=true;
    }
    public static Rect TabletRect(float width,float height)
    {
        float scale=Mathf.Min(width*.90f/1180f,height*.92f/800f);
        return new Rect((width-1180*scale)*.5f,(height-800*scale)*.5f,1180*scale,800*scale);
    }
    static void Fill(Rect r,Color c){Color old=GUI.color;GUI.color=c;GUI.DrawTexture(r,Texture2D.whiteTexture);GUI.color=old;}
}
