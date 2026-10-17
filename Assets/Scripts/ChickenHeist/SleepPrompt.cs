using UnityEngine;

// After the opening conversation: a box on screen tells Elias to sleep, since the visitor leaves the tablet by the door
// in the morning (TabletDeliveryBox). "Dormir até amanhã" goes straight to bed (HomeNextNight); "Agora não" closes it
// and the objective card keeps the reminder.
public sealed class SleepPrompt : MonoBehaviour
{
    public static bool IsOpen {get;private set;}
    // the frame it was closed (an Escape that closes it must not also open the pause menu)
    public static int ClosedFrame {get;private set;}=-1;
    static SleepPrompt instance;
    CursorLockMode oldLock;bool oldCursor;
    void Awake(){instance=this;IsOpen=false;}
    void OnDestroy(){if(instance==this){instance=null;IsOpen=false;}}
    public static void Show()
    {
        if(instance==null || IsOpen)return;
        instance.oldLock=Cursor.lockState;instance.oldCursor=Cursor.visible;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;IsOpen=true;
    }
    public static void Close()
    {
        if(instance==null || !IsOpen)return;
        IsOpen=false;ClosedFrame=Time.frameCount;Cursor.lockState=instance.oldLock;Cursor.visible=instance.oldCursor;
    }
    // Reviews that go on playing after a fresh opening: close the box and hand the tablet over as the morning would.
    public static void SkipForReview()
    {
        Close();var economy=HouseholdEconomy.Instance;
        if(economy!=null && economy.Account.tabletWaiting)economy.Commit(a=>{a.tabletWaiting=false;return true;},"Revisao: tablet entregue");
    }
    void Update(){if(IsOpen && Input.GetKeyDown(KeyCode.Escape))Close();}
    void OnGUI()
    {
        if(!IsOpen || GameMenu.IsOpen)return;
        GUI.skin=UITheme.Skin;int depth=GUI.depth;GUI.depth=-150;
        var old=GUI.color;GUI.color=new Color(0,0,0,.55f);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=old;
        float w=UITheme.Size(560),h=UITheme.Size(300);var r=new Rect((Screen.width-w)/2,(Screen.height-h)/2,w,h);
        UITheme.Panel(r,UITheme.PanelFill,UITheme.Accent);
        float pad=UITheme.Size(26);
        GUI.Label(new Rect(r.x+pad,r.y+pad,r.width-2*pad,UITheme.Size(40)),"Fim do dia",new GUIStyle(UITheme.Style("title")){fontSize=UITheme.Size(30)});
        var body=new GUIStyle(UITheme.Style("body")){fontSize=UITheme.Size(18),wordWrap=true};
        GUI.Label(new Rect(r.x+pad,r.y+pad+UITheme.Size(50),r.width-2*pad,UITheme.Size(120)),
            "O visitante foi embora. Amanhã cedo ele deixa o tablet numa caixa, do lado da porta.\n\nVá dormir para o dia virar.",body);
        float bw=(r.width-3*pad)/2,bh=UITheme.Size(48),by=r.yMax-pad-bh;
        if(GUI.Button(new Rect(r.x+pad,by,bw,bh),"Dormir até amanhã",UITheme.Style("primary"))){Close();HomeNextNight.Instance?.RestNow();}
        if(GUI.Button(new Rect(r.x+2*pad+bw,by,bw,bh),"Agora não"))Close();
        GUI.depth=depth;
    }
}
