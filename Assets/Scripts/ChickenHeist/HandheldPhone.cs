using UnityEngine;

[DefaultExecutionOrder(300)]
public class HandheldPhone : MonoBehaviour
{
    public Transform eyes, handset, upperArm, forearm, hand;
    public float Progress { get; private set; }
    public bool ScreenReady => Progress > .98f;
    Quaternion upperBase,forearmBase,handBase;
    Vector3 handScale;
    bool posed;
    public bool IsBusy => Progress>0;
    public Vector3 GripPoint => handset.TransformPoint(new Vector3(.044f,-.052f,.047f));
    public Rect ScreenRect
    {
        get
        {
            var camera=eyes.GetComponent<Camera>();
            Vector3 a=camera.WorldToScreenPoint(handset.TransformPoint(new Vector3(-.044f,.088f,-.0065f)));
            Vector3 b=camera.WorldToScreenPoint(handset.TransformPoint(new Vector3(.044f,-.088f,-.0065f)));
            return new Rect(a.x,Screen.height-a.y,b.x-a.x,a.y-b.y);
        }
    }
    void RestorePose()
    {
        if(!posed)return;
        upperArm.localRotation=upperBase;forearm.localRotation=forearmBase;hand.localRotation=handBase;hand.localScale=handScale;posed=false;
    }
    void Update(){RestorePose();}
    public void Advance(float delta)
    {
        Progress=Mathf.MoveTowards(Progress,ProtagonistPhone.IsOpen?1:0,delta/.8f);
        handset.gameObject.SetActive(Progress>0);
        if(Progress<=0)return;
        upperBase=upperArm.localRotation;forearmBase=forearm.localRotation;handBase=hand.localRotation;handScale=hand.localScale;posed=true;
        float t=Progress*Progress*Progress*(Progress*(Progress*6-15)+10);
        var camera=eyes.GetComponent<Camera>();
        float depth=.176f/(2*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f)*.78f)+.0065f;
        Vector3 start=new Vector3(.23f,-.65f,.16f),control=new Vector3(.17f,-.15f,.28f),end=new Vector3(0,-.005f,depth);
        handset.position=eyes.TransformPoint((1-t)*(1-t)*start+2*(1-t)*t*control+t*t*end);
        handset.rotation=eyes.rotation*Quaternion.Euler(Mathf.Lerp(60,0,t),Mathf.Lerp(-22,0,t),Mathf.Lerp(15,0,t));
        // A two-bone reach keeps the existing character's hand attached to the handset.
        Vector3 wrist=GripPoint;
        float a=Vector3.Distance(upperArm.position,forearm.position),b=Vector3.Distance(forearm.position,hand.position);
        Vector3 axis=wrist-upperArm.position;float distance=Mathf.Clamp(axis.magnitude,.02f,a+b-.005f);axis.Normalize();
        Vector3 bend=Vector3.ProjectOnPlane(eyes.right+Vector3.down,axis).normalized;
        float along=(a*a-b*b+distance*distance)/(2*distance);
        Vector3 elbow=upperArm.position+axis*along+bend*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
        upperArm.rotation=Quaternion.Slerp(upperArm.rotation,Quaternion.FromToRotation(forearm.position-upperArm.position,elbow-upperArm.position)*upperArm.rotation,t);
        forearm.rotation=Quaternion.Slerp(forearm.rotation,Quaternion.FromToRotation(hand.position-forearm.position,wrist-forearm.position)*forearm.rotation,t);
        var gripRotation=Quaternion.AngleAxis(90,handset.up)*Quaternion.FromToRotation(hand.up,handset.up)*hand.rotation;
        hand.rotation=Quaternion.Slerp(hand.rotation,gripRotation,t);
        hand.localScale=Vector3.Lerp(handScale,handScale*.62f,t);
    }
    void LateUpdate(){if(eyes!=null && handset!=null && hand!=null)Advance(Time.unscaledDeltaTime);}
    void OnDisable(){RestorePose();Progress=0;if(handset!=null)handset.gameObject.SetActive(false);}
}
