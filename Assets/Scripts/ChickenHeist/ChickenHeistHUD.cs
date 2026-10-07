using UnityEngine;

public class ChickenHeistHUD : MonoBehaviour
{
    public GUISkin skin;
    static readonly string[] States={"Sono profundo","Sono agitado","Alerta parcial","Procurando","Perseguição"};
    float messageAlpha;string shownMessage="";

    // Top-left mission card (farm, farmer's sleep, distance), bottom-left carried birds, bottom-centre status toast.
    public static Rect MissionCard=>new Rect(UITheme.Size(20),UITheme.Size(20),UITheme.Size(330),UITheme.Size(112));
    void OnGUI()
    {
        var game=HeistGameManager.Instance;
        if(game==null || GameMenu.IsOpen || ProtagonistPhone.IsOpen || VillageMarket.IsOpen)return;
        GUI.skin=UITheme.Skin;
        float s=UITheme.Scale;
        if(game.MissionActive)
        {
            var card=MissionCard;UITheme.Panel(card,UITheme.PanelSoft);
            float x=card.x+14*s,w=card.width-28*s;
            GUI.Label(new Rect(x,card.y+8*s,w,22*s),"MISSÃO",UITheme.Style("caption"));
            GUI.Label(new Rect(x,card.y+24*s,w,28*s),game.MissionName,new GUIStyle(UITheme.Style("hud")){fontSize=UITheme.Size(19)});
            var farmer=game.farmerSleep;
            if(farmer!=null)
            {
                float sleep=farmer.CurrentSleep/100f;
                var stateColor=Color.Lerp(UITheme.Good,UITheme.Danger,sleep);
                GUI.Label(new Rect(x,card.y+54*s,w,22*s),States[(int)farmer.State],new GUIStyle(UITheme.Style("hudSmall")){normal={textColor=Color.Lerp(UITheme.Ink,stateColor,.6f)}});
                UITheme.Meter(new Rect(x,card.y+78*s,w,8*s),sleep,stateColor);
            }
            var phone=ProtagonistPhone.Instance;
            if(phone!=null && game.MissionFarm>=0 && game.MissionFarm<phone.farmPositions.Length)
                GUI.Label(new Rect(x,card.y+54*s,w,22*s),Mathf.RoundToInt(Vector3.Distance(game.player.position,phone.farmPositions[game.MissionFarm]))+" m",
                    new GUIStyle(UITheme.Style("hudSmall")){alignment=TextAnchor.UpperRight});
        }
        if(game.backpack!=null && (game.MissionActive || game.backpack.chickensCarried>0))
        {
            var r=new Rect(UITheme.Size(20),Screen.height-UITheme.Size(64),UITheme.Size(176),UITheme.Size(44));UITheme.Panel(r,UITheme.PanelSoft);
            GUI.Label(new Rect(r.x+14*s,r.y,r.width-28*s,r.height),"No colo",new GUIStyle(UITheme.Style("hudSmall")){alignment=TextAnchor.MiddleLeft});
            GUI.Label(new Rect(r.x+14*s,r.y,r.width-28*s,r.height),game.backpack.chickensCarried+" / "+game.backpack.capacity,
                new GUIStyle(UITheme.Style("hud")){alignment=TextAnchor.MiddleRight,normal={textColor=game.backpack.IsFull?UITheme.Accent:UITheme.Ink}});
        }
        // Messages fade in and out instead of popping.
        if(game.HasMessage){shownMessage=game.statusMessage;messageAlpha=Mathf.MoveTowards(messageAlpha,1,Time.unscaledDeltaTime*6);}
        else messageAlpha=Mathf.MoveTowards(messageAlpha,0,Time.unscaledDeltaTime*3);
        if(messageAlpha>0)UITheme.Toast(shownMessage,messageAlpha);
    }
}
