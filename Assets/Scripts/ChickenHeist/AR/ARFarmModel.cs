using UnityEngine;

public sealed class ARFarmModel : MonoBehaviour
{
    [System.Serializable]
    public struct Sweep{public Transform head;public float arc,speed,startYaw;}
    public string farmName,kind;
    public int chickens,cameras,guards;
    public float patrolSpeed=1.8f,patrolLength;
    public Transform scaled,route,cones,birds,gate,token,labels;
    public Vector3[] patrol;
    public Sweep[] sweeps;
    public string[] labelTexts;
    public Vector3[] labelPositions;
    public float[] labelSizes;
    float walked;
    void Start()
    {
        if(labels==null || labelTexts==null)return;
        var font=Resources.Load<Font>("UIFonts/Barlow-SemiBold");var shader=Resources.Load<Shader>("Cinematics/CinematicPrint");
        if(font==null || shader==null)return;
        for(int i=0;i<labelTexts.Length;i++)
        {
            var go=new GameObject("Rotulo - "+labelTexts[i]);go.transform.SetParent(labels,false);go.transform.localPosition=labelPositions[i];
            var text=go.AddComponent<TextMesh>();text.font=font;text.text=labelTexts[i];text.fontSize=64;text.anchor=TextAnchor.LowerCenter;text.alignment=TextAlignment.Center;
            text.characterSize=labelSizes[i]/6.4f;text.color=Color.white;
            font.RequestCharactersInTexture(text.text,text.fontSize);
            var material=new Material(shader);material.SetColor("_BaseColor",new Color(1,.97f,.9f));material.SetTexture("_BaseMap",font.material.mainTexture);
            go.GetComponent<Renderer>().sharedMaterial=material;
        }
    }

    public void Show(string layer,bool on)
    {
        var t=layer=="Ronda"?route:layer=="Cameras"?cones:layer=="Galinhas"?birds:layer=="Entrada"?gate:null;
        if(t!=null)t.gameObject.SetActive(on);
    }
    void Update()
    {
        if(token!=null && patrol!=null && patrol.Length>1 && patrolLength>0)
        {
            walked=Mathf.Repeat(walked+Time.deltaTime*patrolSpeed*4,patrolLength);
            float left=walked;
            for(int i=0;i<patrol.Length;i++)
            {
                Vector3 a=patrol[i],b=patrol[(i+1)%patrol.Length];float d=Vector3.Distance(a,b);
                if(left<=d || i==patrol.Length-1)
                {
                    float u=d>0?Mathf.Clamp01(left/d):0;token.localPosition=Vector3.Lerp(a,b,u);
                    var dir=b-a;dir.y=0;if(dir.sqrMagnitude>1e-4f)token.localRotation=Quaternion.LookRotation(dir);
                    break;
                }
                left-=d;
            }
        }
        if(sweeps!=null)foreach(var s in sweeps)
        {
            if(s.head==null)continue;
            float yaw=s.startYaw+Mathf.Sin(Time.time*s.speed*Mathf.Deg2Rad*2.2f)*s.arc*.5f;
            s.head.localRotation=Quaternion.Euler(0,yaw,0);
        }
        var cam=Camera.main;
        if(labels!=null && cam!=null)foreach(Transform l in labels)
        {
            var away=l.position-cam.transform.position;away.y=0;
            if(away.sqrMagnitude>1e-6f)l.rotation=Quaternion.LookRotation(away);
        }
    }
}
