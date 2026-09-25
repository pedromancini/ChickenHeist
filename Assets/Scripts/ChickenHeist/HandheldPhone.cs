using UnityEngine;

// Serialized compatibility only: the tablet opens directly as a screen overlay.
public class HandheldPhone : MonoBehaviour
{
    public Transform eyes,handset,upperArm,forearm,hand;
    public float Progress=>0;
    public bool ScreenReady=>ProtagonistPhone.IsOpen;
    public bool IsBusy=>false;
    public Vector3 PalmContact=>hand!=null?hand.position:transform.position;
    public Vector3 PhoneContact=>PalmContact;
    public Vector3 GripPoint=>PalmContact;
    public Rect ScreenRect=>ProtagonistPhone.TabletRect(Screen.width,Screen.height);
    void Awake(){Hide();}
    void OnEnable(){Hide();}
    void OnDisable(){Hide();}
    public void Advance(float delta){Hide();}
    void Hide(){if(handset!=null)handset.gameObject.SetActive(false);}
}
