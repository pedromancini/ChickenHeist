using UnityEngine;

[DefaultExecutionOrder(-120)]
public class VillageMarket : MonoBehaviour
{
    public static bool IsOpen {get;private set;}
    public static int ClosedFrame {get;private set;}=-1;
    public Transform counter;
    public RuralCharacterAnimator merchant;
    public float range=3.2f;
    public bool PlayerInRange => counter!=null && HeistGameManager.Instance?.player!=null
        && Vector3.Distance(HeistGameManager.Instance.player.position+Vector3.up,counter.position)<range;
    bool ownsPanel;
    int quantity=1,tab;
    bool backpack=true;
    string feedback="Pago R$ 45 por galinha. Sem perguntas.";
    CursorLockMode previousLock;bool previousCursor;
    void Update()
    {
        if(GameMenu.BlocksInput)return;
        if(ownsPanel)
        {
            if(!PlayerInRange || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))Close();
            return;
        }
        if(ProtagonistPhone.IsOpen || !PlayerInRange || HeistGameManager.Instance.missionEnded)return;
        if(WorldInteraction.Pressed(this))Open();
    }
    public void Open()
    {
        if(IsOpen || ProtagonistPhone.IsOpen || !PlayerInRange)return;
        ownsPanel=IsOpen=true;quantity=1;tab=0;
        backpack=(HeistGameManager.Instance.backpack.chickensCarried>0);
        previousLock=Cursor.lockState;previousCursor=Cursor.visible;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
    }
    public void Close()
    {
        if(!ownsPanel)return;
        ownsPanel=IsOpen=false;ClosedFrame=Time.frameCount;
        Cursor.lockState=previousLock;Cursor.visible=previousCursor;
    }
    void OnDisable(){Close();}
    public bool Sell(int count,bool carried)
    {
        var economy=HouseholdEconomy.Instance;
        if(economy==null)return false;
        bool sold=economy.SellAtMarket(this,count,carried);
        feedback=sold?"Recebido: R$ "+count*45+". Volte quando tiver mais.":economy.Message;
        if(sold)merchant?.Gesture();return sold;
    }
    void OnGUI()
    {
        // The "E" prompt for the counter comes from InteractionFocusHUD; this only draws the open panel.
        if(GameMenu.IsOpen || !ownsPanel)return;
        var economy=HouseholdEconomy.Instance;if(economy==null)return;
        GUI.skin=UITheme.Skin;
        var label=UITheme.Style("body");var caption=UITheme.Style("caption");var button=UITheme.Skin.button;var tabStyle=UITheme.Style("tab");var primary=UITheme.Style("primary");
        float w=Mathf.Min(UITheme.Size(640),Screen.width-UITheme.Size(40)),h=Mathf.Min(UITheme.Size(600),Screen.height-UITheme.Size(40));
        var panel=new Rect((Screen.width-w)*.5f,(Screen.height-h)*.5f,w,h);
        UITheme.Panel(panel,UITheme.PanelFill,UITheme.Edge,16);
        float pad=UITheme.Size(28);
        GUILayout.BeginArea(new Rect(panel.x+pad,panel.y+UITheme.Size(22),panel.width-pad*2,panel.height-UITheme.Size(44)));
        GUILayout.BeginHorizontal();
        GUILayout.BeginVertical();
        GUILayout.Label("Entreposto da Mata",UITheme.Style("heading"));
        GUILayout.Label("Seu Anselmo  ·  compra sem perguntas",UITheme.Style("subtitle"));
        GUILayout.EndVertical();GUILayout.FlexibleSpace();
        if(GUILayout.Button("Fechar",button,GUILayout.Width(UITheme.Size(100))))Close();GUILayout.EndHorizontal();
        GUILayout.Space(UITheme.Size(6));
        GUILayout.Label("SALDO  R$ "+economy.Account.balance+",00",new GUIStyle(caption){normal={textColor=UITheme.Accent}});
        GUILayout.Space(UITheme.Size(10));
        tab=GUILayout.Toolbar(tab,new[]{"Vender galinhas","Suprimentos"},tabStyle,GUILayout.Height(UITheme.Size(44)));GUILayout.Space(UITheme.Size(18));
        if(tab==0)
        {
            GUILayout.Label("DE ONDE SAEM AS GALINHAS",caption);
            int selected=GUILayout.Toolbar(backpack?0:1,new[]{"No colo","Retirada no sítio"},tabStyle,GUILayout.Height(UITheme.Size(40)));
            if(backpack!=(selected==0)){backpack=selected==0;quantity=1;}
            int available=backpack?HeistGameManager.Instance.backpack.chickensCarried:economy.Account.flock;
            GUILayout.Space(UITheme.Size(8));
            GUILayout.Label("Disponíveis: "+available+"  ·  R$ 45 por galinha",label);
            quantity=Mathf.Clamp(quantity,1,Mathf.Max(1,available));
            GUILayout.Space(UITheme.Size(6));
            GUILayout.BeginHorizontal();
            float cell=UITheme.Size(48);
            if(GUILayout.Button("−",tabStyle,GUILayout.Width(cell),GUILayout.Height(cell)))quantity=Mathf.Max(1,quantity-1);
            GUILayout.Label(quantity.ToString(),new GUIStyle(UITheme.Style("heading")){alignment=TextAnchor.MiddleCenter,fontSize=UITheme.Size(30)},GUILayout.Width(UITheme.Size(70)),GUILayout.Height(cell));
            if(GUILayout.Button("+",tabStyle,GUILayout.Width(cell),GUILayout.Height(cell)))quantity=Mathf.Min(Mathf.Max(1,available),quantity+1);
            GUILayout.Space(UITheme.Size(10));
            if(GUILayout.Button("Todas",tabStyle,GUILayout.Width(UITheme.Size(100)),GUILayout.Height(cell)))quantity=Mathf.Max(1,available);
            GUILayout.FlexibleSpace();GUILayout.EndHorizontal();GUILayout.Space(UITheme.Size(14));
            GUI.enabled=available>0 && economy.Ready;
            if(GUILayout.Button("Confirmar venda  ·  R$ "+quantity*45,primary,GUILayout.Height(UITheme.Size(50))))Sell(quantity,backpack);
            GUI.enabled=true;
        }
        else
        {
            for(int i=0;i<HouseholdAccount.ProductCount;i++)
            {
                if(i==2)continue;
                GUILayout.BeginHorizontal();
                GUILayout.Label(HouseholdAccount.ProductName(i),new GUIStyle(label){alignment=TextAnchor.MiddleLeft},GUILayout.Height(UITheme.Size(44)));GUILayout.FlexibleSpace();
                GUILayout.Label("R$ "+HouseholdAccount.ProductPrice(i),new GUIStyle(UITheme.Style("hud")){alignment=TextAnchor.MiddleRight,normal={textColor=UITheme.Accent}},GUILayout.Height(UITheme.Size(44)));GUILayout.Space(UITheme.Size(12));
                GUI.enabled=economy.Ready && economy.Account.CanBuy(i);
                if(GUILayout.Button("Comprar",button,GUILayout.Width(UITheme.Size(120)),GUILayout.Height(UITheme.Size(44)))){economy.Buy(i);feedback=economy.Message;merchant?.Gesture();}
                GUI.enabled=true;
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.FlexibleSpace();GUILayout.Label(feedback,UITheme.Style("small"));
        GUILayout.EndArea();
    }
}
