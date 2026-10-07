using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(-550)]
public class BackpackPanel : MonoBehaviour
{
    public static BackpackPanel Instance { get; private set; }
    public static bool IsOpen => Instance!=null && Instance.open;
    public static bool BlocksInput => IsOpen || closedFrame==Time.frameCount;
    static int closedFrame=-1;
    bool open,oldAudio,oldCursor;
    float oldTime;
    CursorLockMode oldLock;
    string feedback=""; int selected,context=-1; bool pendingSpray;
    GUIStyle heading,label,small,button,slotName,slotEmpty,badge;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Instance=null;closedFrame=-1;}
    void Awake(){Instance=this;}
    public bool Open()
    {
        if(open || GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen ||
            HeistGameManager.Instance==null || HeistGameManager.Instance.missionEnded ||
            FindObjectsByType<ChickenCoopLockpick>().Any(c=>c.ChallengeActive))return false;
        oldTime=Time.timeScale;oldAudio=AudioListener.pause;oldCursor=Cursor.visible;oldLock=Cursor.lockState;
        open=true;feedback="";Time.timeScale=0;AudioListener.pause=true;
        Cursor.lockState=CursorLockMode.None;Cursor.visible=true;return true;
    }
    public void Close()
    {
        if(!open)return;
        open=false;closedFrame=Time.frameCount;
        if(!GameMenu.IsOpen){Time.timeScale=oldTime;AudioListener.pause=oldAudio;Cursor.lockState=oldLock;Cursor.visible=oldCursor;}
    }
    void OnDisable(){Close();if(Instance==this)Instance=null;}
    void Update()
    {
        if(open && GameMenu.IsOpen){Close();return;}
        if(pendingSpray && !GameMenu.BlocksInput){pendingSpray=false;Spray();}
        if(Input.GetKeyDown(KeyCode.B)){if(open)Close();else Open();}
        if(open && Input.GetKeyDown(KeyCode.Escape))Close();
        if(!GameMenu.BlocksInput && !OldPickupTruck.IsDriving && !ProtagonistPhone.IsOpen && !VillageMarket.IsOpen && Input.GetMouseButtonDown(1) && selected==2)Spray();
    }
    void LateUpdate(){if(open){Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}}
    public static bool Spray()
    {
        var game=HeistGameManager.Instance;
        if(GameMenu.BlocksInput || OldPickupTruck.IsDriving || game==null || game.missionEnded || !game.MissionActive || ProtagonistPhone.IsOpen || VillageMarket.IsOpen ||
            FindObjectsByType<ChickenCoopLockpick>().Any(c=>c.ChallengeActive))return false;
        if(game.backpack.chickensCarried>0){game.ShowMessage("Guarde a galinha na gaiola para liberar as maos.",2);return false;}
        var target=FindObjectsByType<SecurityCamera>().Where(c=>c.CanPaintNow())
            .OrderBy(c=>(c.transform.position-Camera.main.transform.position).sqrMagnitude).FirstOrDefault();
        if(target==null){game.ShowMessage("Nenhuma camera ao alcance da tinta.",2);return false;}
        return target.TryPaint();
    }
    public bool Equip(bool professional)
    {
        var economy=HouseholdEconomy.Instance;
        if(economy==null)return false;
        bool success=economy.EquipLockpick(professional);feedback=economy.Message;return success;
    }
    void OnGUI()
    {
        if(!open || GameMenu.IsOpen || HouseholdEconomy.Instance==null)return;
        GUI.skin=UITheme.Skin;
        // Laid out in a fixed 690x546 space scaled by GUI.matrix, so sizes here are absolute.
        if(label==null)
        {
            label=new GUIStyle(UITheme.Skin.label){font=UITheme.Body,fontSize=18,wordWrap=true,normal={textColor=UITheme.Ink}};
            heading=new GUIStyle(label){font=UITheme.TitleFont,fontSize=30};
            small=new GUIStyle(label){fontSize=14,normal={textColor=UITheme.Muted}};
            button=new GUIStyle(UITheme.Skin.button){fontSize=16,padding=new RectOffset(12,12,8,8)};
            slotName=new GUIStyle(label){font=UITheme.BodyBold,fontSize=16,alignment=TextAnchor.MiddleLeft,wordWrap=true};
            slotEmpty=new GUIStyle(slotName){font=UITheme.Body,normal={textColor=UITheme.Faint}};
            badge=new GUIStyle(UITheme.Style("key")){fontSize=15};
        }
        var account=HouseholdEconomy.Instance.Account;
        Matrix4x4 matrix=GUI.matrix;Color color=GUI.color;int depth=GUI.depth;GUI.depth=-90;
        float scale=Mathf.Min(UITheme.Scale,Screen.width/750f,Screen.height/600f);
        GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-690*scale)*.5f,(Screen.height-546*scale)*.5f),Quaternion.identity,Vector3.one*scale);
        UITheme.Panel(new Rect(0,0,690,546),UITheme.PanelFill,UITheme.Edge,16);
        GUI.Label(new Rect(26,16,420,44),"Mochila",heading);
        if(GUI.Button(new Rect(556,22,108,38),"Fechar",button))Close();
        GUI.Label(new Rect(26,62,630,28),"Ferramentas e consumíveis  ·  R$ "+account.balance+",00",new GUIStyle(small){normal={textColor=UITheme.Accent}});
        string[] names={"Lockpick básico","Lockpick profissional","Spray","Ração","Reparos"};
        int[] quantities={1,account.professionalLockpick?1:0,account.paintUses,account.feed,account.boards};
        int slots=account.backpackUpgrade?9:6;
        for(int i=0;i<9;i++)
        {
            var rect=new Rect(26+(i%3)*216,108+(i/3)*92,200,80);
            bool owned=i<5 && quantities[i]>0,locked=i>=slots,isSelected=selected==i && owned;
            UITheme.Panel(rect,locked?new Color(.05f,.045f,.04f,.9f):isSelected?new Color(.24f,.17f,.08f,.98f):new Color(.12f,.105f,.088f,.96f),
                isSelected?UITheme.Accent:locked?new Color(.2f,.18f,.15f,.6f):new Color(.32f,.26f,.18f,.9f),10);
            if(owned)
            {
                GUI.Label(new Rect(rect.x+14,rect.y+6,rect.width-70,rect.height-12),names[i],slotName);
                GUI.Label(new Rect(rect.xMax-50,rect.y+(rect.height-28)*.5f,38,28),"×"+quantities[i],badge);
            }
            else GUI.Label(new Rect(rect.x+14,rect.y,rect.width-28,rect.height),locked?"Bloqueado":"Vazio",new GUIStyle(slotEmpty){alignment=TextAnchor.MiddleCenter});
            if(i<slots && owned && Event.current.type==EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                selected=i;if(Event.current.button==1)context=i;else context=-1;Event.current.Use();
            }
        }
        GUI.Label(new Rect(26,386,638,48),"Clique para selecionar. Botão direito no slot para usar.\nUma galinha por vez, no colo — fora da mochila.",small);
        if(context>=0)
        {
            if(GUI.Button(new Rect(26,444,220,42),"Usar item selecionado",UITheme.Style("primary")))UseSelected();
            if(GUI.Button(new Rect(258,444,130,42),"Cancelar",button))context=-1;
        }
        GUI.Label(new Rect(26,494,638,44),feedback,small);
        GUI.enabled=true;GUI.matrix=matrix;GUI.color=color;GUI.depth=depth;
    }
    public bool UseSelected()
    {
        var economy=HouseholdEconomy.Instance;
        if(economy==null)return false;
        context=-1;
        if(selected==0 || selected==1){bool result=Equip(selected==1);if(result)Close();return result;}
        if(selected==2){if(economy.Account.paintUses<1)return false;Close();pendingSpray=true;return true;}
        if(selected==3)economy.UseFeed();else if(selected==4)economy.Repair();
        feedback=economy.Message;return selected==3 || selected==4;
    }
    void ToolRow(float y,string name,string description,bool equipped,bool owned,bool professional)
    {
        GUI.Label(new Rect(26,y,460,28),name,label);GUI.Label(new Rect(26,y+31,465,30),description,small);
        GUI.enabled=owned && !equipped && HouseholdEconomy.Instance.Ready;
        if(GUI.Button(new Rect(520,y+6,144,40),equipped?"Equipado":owned?"Equipar":"Não adquirido",button))Equip(professional);
        GUI.enabled=true;
    }
    static void Fill(Rect rect,Color color){var old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;}
}
