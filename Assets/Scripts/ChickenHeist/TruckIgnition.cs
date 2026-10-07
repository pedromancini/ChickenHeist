using UnityEngine;

[DefaultExecutionOrder(110)]
public class TruckIgnition : MonoBehaviour
{
    public bool EngineRunning {get;private set;}
    public bool Active {get;private set;}
    public int Hits {get;private set;}
    public float CursorPosition {get;private set;}
    public float Target {get;private set;}
    float phase,cooldown;
    OldPickupTruck truck;
    void Awake(){truck=GetComponent<OldPickupTruck>();}
    public bool Begin()
    {
        if(!truck.driving || EngineRunning || Active || Time.time<cooldown || GameMenu.BlocksInput)return false;
        Active=true;Hits=0;phase=0;CursorPosition=0;Target=Random.Range(.25f,.75f);return true;
    }
    public bool Confirm()
    {
        if(!Active || !truck.driving)return false;
        if(Mathf.Abs(CursorPosition-Target)>.085f)
        {
            Active=false;EngineRunning=false;Hits=0;cooldown=Time.time+1;
            NoiseEmitter.EmitGlobal(NoiseSource.VehicleEngine,transform.position,2f);
            HeistGameManager.Instance.ShowMessage("O motor engasgou e nao ligou. E para tentar novamente.",3);return false;
        }
        Hits++;
        if(Hits==2){EngineRunning=true;Active=false;HeistGameManager.Instance.ShowMessage("Motor ligado.",2);}
        else{Target=Random.Range(.2f,.8f);phase=0;CursorPosition=0;}
        return true;
    }
    public void StopEngine(){EngineRunning=false;Active=false;Hits=0;}
    void Update()
    {
        if(!truck.driving){StopEngine();return;}
        if(GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen)return;
        if(!EngineRunning && !Active && Input.GetKeyDown(KeyCode.E)){Begin();return;}
        if(!Active)return;
        phase+=Time.deltaTime*(1.1f+Hits*.25f);CursorPosition=Mathf.PingPong(phase,1);
        if(Input.GetKeyDown(KeyCode.Escape)){Active=false;return;}
        if(Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))Confirm();
    }
    void OnGUI()
    {
        if(!truck.driving || GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || EngineRunning)return;
        GUI.skin=UITheme.Skin;
        if(!Active){UITheme.HintBar(Screen.height*.62f,"Motor desligado","E","Dar partida","F","Sair");return;}
        float s=UITheme.Scale,w=UITheme.Size(480),h=UITheme.Size(128),x=(Screen.width-w)*.5f,y=Screen.height*.58f;
        UITheme.Panel(new Rect(x,y,w,h));
        GUI.Label(new Rect(x+16*s,y+10*s,w-32*s,24*s),"PARTIDA",UITheme.Style("caption"));
        GUI.Label(new Rect(x+16*s,y+10*s,w-32*s,24*s),"Acertos "+Hits+" / 2",new GUIStyle(UITheme.Style("hud")){alignment=TextAnchor.UpperRight});
        var bar=new Rect(x+16*s,y+42*s,w-32*s,26*s);UITheme.Panel(bar,new Color(.02f,.02f,.015f,.9f),UITheme.Edge,6);
        UITheme.Panel(new Rect(bar.x+(Target-.085f)*bar.width,bar.y+3*s,.17f*bar.width,bar.height-6*s),new Color(.36f,.62f,.30f),new Color(.58f,.85f,.45f),4);
        UITheme.Panel(new Rect(bar.x+CursorPosition*bar.width-3*s,bar.y-6*s,6*s,bar.height+12*s),UITheme.Accent,new Color(1,.9f,.6f),3);
        GUI.Label(new Rect(x+16*s,y+78*s,w-32*s,40*s),"<b>Espaço</b> quando a agulha estiver na faixa verde  ·  errar afoga o motor  ·  <b>Esc</b> cancela",new GUIStyle(UITheme.Style("small")){alignment=TextAnchor.MiddleCenter});
    }
}
