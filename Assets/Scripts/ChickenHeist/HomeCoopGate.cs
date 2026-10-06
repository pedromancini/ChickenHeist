using UnityEngine;

// Gate of Elias' worn coop: a small plank door on a tired hinge. E opens/closes it; a delivery opens it,
// lets the birds through and closes it again unless the player had opened it by hand.
public class HomeCoopGate : MonoBehaviour
{
    public static HomeCoopGate Instance {get;private set;}
    public float openAngle=-100;
    bool open,autoOpened;float angle,autoCloseAt;
    public bool IsOpen=>open;
    public bool FullyOpen=>open && Mathf.Abs(angle-openAngle)<8;
    public Vector3 InteractionPoint=>transform.TransformPoint(new Vector3(.5f,.6f,0));
    void Awake(){Instance=this;transform.localRotation=Quaternion.identity;}
    void OnDestroy(){if(Instance==this)Instance=null;}
    void Update()
    {
        if(!GameMenu.BlocksInput && !ProtagonistPhone.IsOpen && !VillageMarket.IsOpen && WorldInteraction.Pressed(this)){autoOpened=false;Set(!open);}
        if(autoOpened && open && Time.time>=autoCloseAt){autoOpened=false;Set(false);}
        if(GameMenu.BlocksInput)return;
        // A worn hinge: quick start, slow finish, a small bounce when it shuts.
        float target=open?openAngle:0;
        angle=Mathf.MoveTowards(angle,target,Time.deltaTime*(Mathf.Abs(target-angle)>25?140:60));
        float settle=!open && Mathf.Abs(angle)<.5f?Mathf.Sin(Time.time*30)*Mathf.Max(0,closeBounce-Time.time)*6:0;
        transform.localRotation=Quaternion.Euler(0,angle+settle,0);
    }
    float closeBounce;
    public void Toggle(){autoOpened=false;Set(!open);}
    void Set(bool value)
    {
        if(open==value)return;
        open=value;if(!open)closeBounce=Time.time+.35f+Mathf.Abs(angle)/60f;
        GameAudioMix.Instance?.Play("creak",InteractionPoint,.32f);
    }
    // Opens for a delivery and shuts behind the birds (only if it was closed before).
    public void OpenForDelivery(float hold)
    {
        if(open){if(autoOpened)autoCloseAt=Mathf.Max(autoCloseAt,Time.time+hold);return;}
        autoOpened=true;autoCloseAt=Time.time+hold;Set(true);
    }
}
