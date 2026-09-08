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
    GUIStyle label,title,button;
    void Update()
    {
        if(GameMenu.BlocksInput)return;
        if(ownsPanel)
        {
            if(!PlayerInRange || Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab))Close();
            return;
        }
        if(ProtagonistPhone.IsOpen || !PlayerInRange || HeistGameManager.Instance.missionEnded)return;
        if(Input.GetKeyDown(KeyCode.E))Open();
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
        if(GameMenu.IsOpen)return;
        if(!ownsPanel)
        {
            if(PlayerInRange && !ProtagonistPhone.IsOpen)
                GUI.Box(new Rect(Screen.width*.5f-135,Screen.height*.75f,270,34),"E  |  Conversar com o comerciante");
            return;
        }
        var economy=HouseholdEconomy.Instance;if(economy==null)return;
        if(label==null)
        {
            label=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true};
            title=new GUIStyle(label){fontSize=26,fontStyle=FontStyle.Bold};
            button=new GUIStyle(GUI.skin.button){fontSize=17,wordWrap=true,padding=new RectOffset(12,12,12,12)};
        }
        Matrix4x4 previous=GUI.matrix;
        float scale=Mathf.Min(1f,Screen.width/690f,Screen.height/650f);
        GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-620*scale)*.5f,(Screen.height-580*scale)*.5f),Quaternion.identity,Vector3.one*scale);
        Color previousColor=GUI.color;
        GUI.color=new Color(.045f,.06f,.052f,.97f);
        GUI.DrawTexture(new Rect(0,0,620,580),Texture2D.whiteTexture);
        GUI.color=previousColor;
        GUI.Box(new Rect(0,0,620,580),GUIContent.none);
        GUILayout.BeginArea(new Rect(24,20,572,540));
        GUILayout.BeginHorizontal();GUILayout.Label("ENTREPOSTO DA MATA",title);GUILayout.FlexibleSpace();
        if(GUILayout.Button("Fechar",button,GUILayout.Width(90)))Close();GUILayout.EndHorizontal();
        GUILayout.Label("Seu Anselmo   |   Saldo: R$ "+economy.Account.balance,label);
        tab=GUILayout.Toolbar(tab,new[]{"Vender galinhas","Suprimentos"},button);GUILayout.Space(18);
        if(tab==0)
        {
            int selected=GUILayout.Toolbar(backpack?0:1,new[]{"Na mochila","Retirada no sitio"},button);
            if(backpack!=(selected==0)){backpack=selected==0;quantity=1;}
            int available=backpack?HeistGameManager.Instance.backpack.chickensCarried:economy.Account.flock;
            GUILayout.Label("Disponiveis: "+available+"   |   R$ 45 por galinha",label);
            quantity=Mathf.Clamp(quantity,1,Mathf.Max(1,available));
            GUILayout.BeginHorizontal();
            if(GUILayout.Button("-",button,GUILayout.Width(50)))quantity=Mathf.Max(1,quantity-1);
            GUILayout.Label(quantity.ToString(),title,GUILayout.Width(60));
            if(GUILayout.Button("+",button,GUILayout.Width(50)))quantity=Mathf.Min(Mathf.Max(1,available),quantity+1);
            if(GUILayout.Button("Todas",button))quantity=Mathf.Max(1,available);
            GUILayout.EndHorizontal();GUILayout.Space(12);
            GUI.enabled=available>0 && economy.Ready;
            if(GUILayout.Button("Confirmar venda  |  R$ "+quantity*45,button))Sell(quantity,backpack);
            GUI.enabled=true;
        }
        else
        {
            for(int i=0;i<HouseholdAccount.ProductCount;i++)
            {
                GUI.enabled=economy.Ready && economy.Account.CanBuy(i);
                if(GUILayout.Button("Comprar "+HouseholdAccount.ProductName(i)+"  |  R$ "+HouseholdAccount.ProductPrice(i),button)){economy.Buy(i);feedback=economy.Message;merchant?.Gesture();}
            }
            GUI.enabled=true;
        }
        GUILayout.Space(22);GUILayout.Label(feedback,label);
        GUILayout.EndArea();GUI.matrix=previous;
    }
}
