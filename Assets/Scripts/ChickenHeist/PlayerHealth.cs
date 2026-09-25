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
        Color old=GUI.color;float width=Mathf.Min(220,Screen.width*.28f);
        GUI.color=new Color(.12f,.08f,.07f,.85f);GUI.DrawTexture(new Rect(24,160,width,7),Texture2D.whiteTexture);
        GUI.color=Injured?new Color(.95f,.31f,.22f):new Color(.62f,.81f,.61f);GUI.DrawTexture(new Rect(24,160,width*Current/Maximum,7),Texture2D.whiteTexture);
        GUI.color=old;GUI.Label(new Rect(24,172,310,32),"Vida "+Mathf.CeilToInt(Current)+" / 100"+(Injured?"  ·  Ferido — mancando":""));
        float flash=Mathf.Clamp01(1-(Time.time-damageTime)/.7f)*.35f;
        if(flash>0){GUI.color=new Color(.8f,.02f,.01f,flash);GUI.DrawTexture(new Rect(0,0,Screen.width,12),Texture2D.whiteTexture);GUI.DrawTexture(new Rect(0,Screen.height-12,Screen.width,12),Texture2D.whiteTexture);GUI.color=old;}
    }
}
