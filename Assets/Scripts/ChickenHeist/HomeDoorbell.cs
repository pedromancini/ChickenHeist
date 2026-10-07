using UnityEngine;

// Old electric doorbell beside Elias' front door: the buzzer stutters on tired wiring and the porch bulb dips
// while it rings. The player rings it with E; the visitor rings it in the opening before knocking.
public class HomeDoorbell : MonoBehaviour
{
    public static HomeDoorbell Instance {get;private set;}
    public Transform button,chime,hammer;
    Vector3 buttonRest;bool rested;
    public Light porchLight;
    float ringUntil,baseIntensity=-1,cooldown;
    public bool Ringing=>Time.time<ringUntil;
    void Awake(){Instance=this;}
    void OnDestroy(){if(Instance==this)Instance=null;}
    public Vector3 InteractionPoint=>button!=null?button.position:transform.position;
    public void Ring()
    {
        if(Time.time<cooldown)return;
        cooldown=Time.time+1.4f;ringUntil=Time.time+1.15f;
        GameAudioMix.Instance?.Play("bell",chime!=null?chime.position:transform.position,.38f);
    }
    void Update()
    {
        if(!GameMenu.BlocksInput && !ProtagonistPhone.IsOpen && !VillageMarket.IsOpen && WorldInteraction.Pressed(this))Ring();
        // the button sinks while held down; the striker hammers the gong
        if(button!=null){if(!rested){buttonRest=button.localPosition;rested=true;}button.localPosition=buttonRest+(Ringing?new Vector3(0,0,.004f):Vector3.zero);}
        if(hammer!=null)hammer.localRotation=Ringing?Quaternion.Euler(0,0,Mathf.Sin(Time.time*2*Mathf.PI*31)>0?11:-3):Quaternion.identity;
        if(porchLight!=null)
        {
            if(baseIntensity<0)baseIntensity=porchLight.intensity;
            porchLight.intensity=Ringing?baseIntensity*(.45f+.35f*Mathf.PerlinNoise(Time.time*22,.3f)):baseIntensity;
        }
    }
}
