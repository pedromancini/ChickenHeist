using UnityEngine;

[DefaultExecutionOrder(470)]
public sealed class FirstPersonFist : MonoBehaviour
{
    PlayerPunch punch;Transform fist;Camera eye;
    static readonly Vector3 Hidden=new Vector3(.24f,-.46f,.22f),Cocked=new Vector3(.20f,-.27f,.22f),Out=new Vector3(.09f,-.12f,.44f);
    static readonly Vector3 HiddenFrom=new Vector3(.34f,-.80f,0),CockedFrom=new Vector3(.32f,-.55f,-.05f),OutFrom=new Vector3(.25f,-.38f,.06f);
    const float HiddenRoll=-80,CockedRoll=-70,OutRoll=-20,Scale=1.15f;
    public Transform View=>fist;
    public bool Showing=>fist!=null && fist.gameObject.activeSelf;
    void Awake(){punch=GetComponent<PlayerPunch>();}
    void LateUpdate()
    {
        if(eye==null)eye=Camera.main;
        if(fist==null)
        {
            var prefab=Resources.Load<GameObject>("FirstPerson/PunchFist");if(prefab==null || eye==null)return;
            fist=Instantiate(prefab).transform;fist.name="Punho em primeira pessoa";fist.localScale=Vector3.one*Scale;fist.gameObject.SetActive(false);
        }
        bool show=punch!=null && punch.Punching && eye!=null;
        if(fist.gameObject.activeSelf!=show)fist.gameObject.SetActive(show);
        if(!show)return;
        float t=punch.Progress,cock=Ease(t/.22f),drive=punch.Extension,back=Mathf.Clamp01((t-.58f)/.42f);
        Vector3 at,from;float roll;
        if(t<.22f){at=Vector3.Lerp(Hidden,Cocked,cock);from=Vector3.Lerp(HiddenFrom,CockedFrom,cock);roll=Mathf.Lerp(HiddenRoll,CockedRoll,cock);}
        else if(t<.58f){at=Vector3.Lerp(Cocked,Out,drive);from=Vector3.Lerp(CockedFrom,OutFrom,drive);roll=Mathf.Lerp(CockedRoll,OutRoll,drive);}
        else
        {
            float b=Ease(back);at=Vector3.Lerp(Vector3.Lerp(Out,Cocked,b),Hidden,b*b);from=Vector3.Lerp(Vector3.Lerp(OutFrom,CockedFrom,b),HiddenFrom,b*b);roll=Mathf.Lerp(OutRoll,HiddenRoll,b);
        }
        var view=eye.transform;
        Vector3 world=view.TransformPoint(at),forward=(world-view.TransformPoint(from)).normalized;
        var rotation=Quaternion.LookRotation(forward,view.up);
        fist.SetPositionAndRotation(world,rotation*Quaternion.AngleAxis(roll,Vector3.forward));
    }
    void OnDestroy(){if(fist!=null)Destroy(fist.gameObject);}
    static float Ease(float x){x=Mathf.Clamp01(x);return x*x*(3-2*x);}
}
