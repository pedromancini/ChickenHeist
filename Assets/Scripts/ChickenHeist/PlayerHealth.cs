using UnityEngine;

[DisallowMultipleComponent]
public class PlayerHealth : MonoBehaviour
{
    public const float Maximum=100;
    public float Current {get;private set;}=Maximum;
    public bool Injured=>Current>0 && Current<60;
    public bool Dead=>Current<=0;
    public float SpeedMultiplier=>Dead?0:Injured?.72f*(.92f+.08f*Mathf.Sin(Time.time*9)):1;
    float damageTime=-10;
    CapsuleCollider damageTarget;
    void Awake()
    {
        var target=new GameObject("Corpo atingivel");target.transform.SetParent(transform,false);
        damageTarget=target.AddComponent<CapsuleCollider>();damageTarget.isTrigger=true;damageTarget.radius=.28f;damageTarget.height=1.6f;damageTarget.center=Vector3.up*1.05f;
    }
    void Update(){bool crouched=GetComponent<PlayerMovement>()?.estaAgachado==true;damageTarget.height=crouched?.85f:1.6f;damageTarget.center=Vector3.up*(crouched?.55f:1.05f);}
    public bool TakeDamage(float amount)
    {
        var game=HeistGameManager.Instance;
        if(!float.IsFinite(amount) || amount<=0 || Dead || GameMenu.BlocksInput || game==null || !game.MissionActive || game.missionEnded)return false;
        Current=Mathf.Max(0,Current-amount);damageTime=Time.time;
        if(Dead)game.FailMission("Voce morreu. Carregue seu ultimo save ou inicie uma nova partida.");
        else game.ShowMessage(Injured?"Voce foi ferido e esta mancando. Procure cobertura!":"Voce foi atingido! Procure cobertura.",3);
        return true;
    }
    public void Restore(float health){Current=Mathf.Clamp(health,0,Maximum);damageTime=-10;}
    public void RecoverAfterRest(){Restore(Maximum);}
    void OnGUI()
    {
        var game=HeistGameManager.Instance;
        if(game==null || GameMenu.IsOpen || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || !game.MissionActive && Current>=Maximum)return;
        GUI.skin=UITheme.Skin;Color old=GUI.color;float s=UITheme.Scale;
        var card=ChickenHeistHUD.MissionCard;var r=new Rect(card.x,card.yMax+UITheme.Size(8),UITheme.Size(260),UITheme.Size(50));UITheme.Panel(r,UITheme.PanelSoft);
        GUI.Label(new Rect(r.x+14*s,r.y+6*s,r.width-28*s,20*s),"Vida",UITheme.Style("hudSmall"));
        GUI.Label(new Rect(r.x+14*s,r.y+6*s,r.width-28*s,20*s),Injured?"Ferido — mancando":Mathf.CeilToInt(Current)+" / 100",
            new GUIStyle(UITheme.Style("hudSmall")){alignment=TextAnchor.UpperRight,normal={textColor=Injured?UITheme.Danger:UITheme.Muted}});
        UITheme.Meter(new Rect(r.x+14*s,r.y+31*s,r.width-28*s,8*s),Current/Maximum,Injured?UITheme.Danger:UITheme.Good);
        float flash=Mathf.Clamp01(1-(Time.time-damageTime)/.7f)*.35f;
        if(flash>0){GUI.color=new Color(.8f,.02f,.01f,flash);GUI.DrawTexture(new Rect(0,0,Screen.width,12),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(0,Screen.height-12,Screen.width,12),Texture2D.whiteTexture);GUI.color=old;}
    }
}
