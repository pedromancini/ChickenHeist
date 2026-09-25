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
        if(!Active){GUI.Box(new Rect(Screen.width*.5f-230,Screen.height*.62f,460,48),"MOTOR DESLIGADO | E iniciar partida | F sair");return;}
        float x=Screen.width*.5f-230,y=Screen.height*.6f;
        GUI.Box(new Rect(x,y,460,100),"PARTIDA | ESPACO na faixa verde | Acertos "+Hits+"/2");
        GUI.Box(new Rect(x+20,y+40,420,20),"");var c=GUI.color;GUI.color=Color.green;
        GUI.DrawTexture(new Rect(x+20+(Target-.085f)*420,y+40,.17f*420,20),Texture2D.whiteTexture);
        GUI.color=Color.yellow;GUI.DrawTexture(new Rect(x+20+CursorPosition*420-3,y+35,6,30),Texture2D.whiteTexture);GUI.color=c;
        GUI.Label(new Rect(x+20,y+73,420,24),"Errou: o motor nao liga. ESC cancela.");
    }
}
