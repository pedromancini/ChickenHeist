using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public sealed class ARHeistPlanner : MonoBehaviour
{
    public ARSession session;
    public ARRaycastManager raycasts;
    public ARPlaneManager planes;
    public ARCameraBackground background;
    public Camera view;
    public GameObject[] farms;
    public Font font;
    public static int StartFarm;

    int current;ARFarmModel model;bool placed,fallback;float scale=1,yaw;
    readonly List<ARRaycastHit> hits=new List<ARRaycastHit>();
    readonly string[] layerNames={"Ronda","Cameras","Galinhas","Entrada"};
    readonly Dictionary<string,bool> layers=new Dictionary<string,bool>{{"Ronda",true},{"Cameras",true},{"Galinhas",true},{"Entrada",true}};
    readonly Dictionary<string,Image> toggles=new Dictionary<string,Image>();
    Text title,info,hint;
    float pinch;Vector2 lastPointer;
    static readonly Color Ink=new Color(.96f,.93f,.86f),Panel=new Color(.08f,.09f,.11f,.82f),Accent=new Color(.96f,.71f,.32f),Off=new Color(.30f,.31f,.34f,.9f);

    IEnumerator Start()
    {
        current=Mathf.Clamp(StartFarm,0,farms.Length-1);
        BuildUi();
        if(!Application.isEditor && session!=null)
        {
            if(ARSession.state==ARSessionState.None || ARSession.state==ARSessionState.CheckingAvailability)yield return ARSession.CheckAvailability();
            if(ARSession.state==ARSessionState.NeedsInstall)yield return ARSession.Install();
        }
        fallback=Application.isEditor || session==null || ARSession.state==ARSessionState.Unsupported;
        if(fallback)
        {
            if(session!=null)session.enabled=false;if(background!=null)background.enabled=false;if(planes!=null)planes.enabled=false;
            view.clearFlags=CameraClearFlags.SolidColor;view.backgroundColor=new Color(.13f,.15f,.18f);
            view.transform.SetPositionAndRotation(new Vector3(0,.55f,-.75f),Quaternion.Euler(34,0,0));
            Place(Vector3.zero,0);
        }
        Refresh();
    }

    void Update()
    {
        if(!fallback && !placed && ARSession.state==ARSessionState.SessionTracking && planes!=null && planes.trackables.count>0)hint.text="Toque na mesa para colocar a fazenda";
        if(Input.touchCount==1)
        {
            var t=Input.GetTouch(0);
            if(t.phase==TouchPhase.Began)
            {
                if(OverUi(t.fingerId))return;
                lastPointer=t.position;
                if(!fallback && (!placed || t.tapCount>=2) && raycasts.Raycast(t.position,hits,TrackableType.PlaneWithinPolygon))
                {Place(hits[0].pose.position,Yaw());return;}
            }
            else if(t.phase==TouchPhase.Moved && placed && !OverUi(t.fingerId)){yaw-=(t.position.x-lastPointer.x)*.25f;lastPointer=t.position;}
        }
        else if(Input.touchCount==2 && placed)
        {
            var a=Input.GetTouch(0);var b=Input.GetTouch(1);float d=Vector2.Distance(a.position,b.position);
            if(a.phase==TouchPhase.Began || b.phase==TouchPhase.Began || pinch<=0)pinch=d;
            else{scale=Mathf.Clamp(scale*d/Mathf.Max(1,pinch),.3f,3f);pinch=d;}
        }
        else pinch=0;
        if(Input.touchCount==0 && placed)
        {
            if(Input.GetMouseButtonDown(0))lastPointer=Input.mousePosition;
            if(Input.GetMouseButton(0) && !OverUi(-1)){yaw-=(Input.mousePosition.x-lastPointer.x)*.25f;lastPointer=Input.mousePosition;}
            scale=Mathf.Clamp(scale*(1+Input.mouseScrollDelta.y*.1f),.3f,3f);
        }
        if(model!=null){model.transform.localScale=Vector3.one*scale;model.transform.rotation=Quaternion.Euler(0,yaw,0);}
    }
    static bool OverUi(int id)=>EventSystem.current!=null && EventSystem.current.IsPointerOverGameObject(id);
    float Yaw(){var f=view.transform.forward;f.y=0;return f.sqrMagnitude>1e-4f?Quaternion.LookRotation(f).eulerAngles.y:0;}

    void Place(Vector3 at,float facing)
    {
        if(model!=null)Destroy(model.gameObject);
        model=Instantiate(farms[current]).GetComponent<ARFarmModel>();
        model.transform.position=at;yaw=facing;placed=true;
        foreach(var l in layerNames)model.Show(l,layers[l]);
        if(!fallback)SetPlanesVisible(false);
        Refresh();
    }
    void Switch(int step)
    {
        current=(current+step+farms.Length)%farms.Length;
        if(placed)Place(model.transform.position,yaw);else Refresh();
    }
    void Toggle(string layer)
    {
        layers[layer]=!layers[layer];
        if(model!=null)model.Show(layer,layers[layer]);
        Refresh();
    }
    void Reposition()
    {
        if(fallback){yaw=0;scale=1;return;}
        if(model!=null)Destroy(model.gameObject);
        model=null;placed=false;SetPlanesVisible(true);Refresh();
    }
    void SetPlanesVisible(bool on)
    {
        if(planes==null)return;
        foreach(var p in planes.trackables)foreach(var r in p.GetComponentsInChildren<Renderer>())r.enabled=on;
        planes.planePrefab.GetComponentInChildren<Renderer>(true).enabled=on;
    }
    void Refresh()
    {
        var m=farms[current].GetComponent<ARFarmModel>();
        title.text=m.farmName;
        info.text=$"{m.kind}   ·   {m.chickens} galinhas   ·   {m.cameras} {(m.cameras==1?"câmera":"câmeras")}   ·   {m.guards} {(m.guards==1?"fazendeiro":"fazendeiros")} de ronda";
        hint.text=fallback?"Arraste para girar a maquete · roda do mouse aproxima"
            :placed?"1 dedo gira · 2 dedos aproximam · toque duplo na mesa muda de lugar"
            :"Aponte para uma mesa e mova o celular devagar";
        foreach(var kv in toggles)kv.Value.color=layers[kv.Key]?Accent:Off;
    }

    void BuildUi()
    {
        if(EventSystem.current==null){var es=new GameObject("EventSystem");es.AddComponent<EventSystem>();es.AddComponent<StandaloneInputModule>();}
        var canvas=new GameObject("Interface do planejamento").AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=10;
        var scaler=canvas.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        canvas.gameObject.AddComponent<GraphicRaycaster>();
        var top=Box(canvas.transform,"Topo",new Vector2(0,1),new Vector2(1,1),new Vector2(0,-150),new Vector2(0,0),Panel);
        Label(top,"PLANEJAMENTO DO ROUBO",22,Accent,new Vector2(0,1),new Vector2(1,1),new Vector2(40,-46),new Vector2(-40,-12),TextAnchor.MiddleLeft);
        title=Label(top,"",44,Ink,new Vector2(0,0),new Vector2(1,1),new Vector2(40,44),new Vector2(-40,-44),TextAnchor.MiddleLeft);
        info=Label(top,"",24,new Color(.80f,.82f,.85f),new Vector2(0,0),new Vector2(1,0),new Vector2(40,8),new Vector2(-40,46),TextAnchor.MiddleLeft);
        var bottom=Box(canvas.transform,"Base",new Vector2(0,0),new Vector2(1,0),new Vector2(0,0),new Vector2(0,150),Panel);
        hint=Label(canvas.transform,"",28,Ink,new Vector2(0,0),new Vector2(1,0),new Vector2(40,160),new Vector2(-40,214),TextAnchor.MiddleCenter);
        hint.gameObject.AddComponent<Shadow>().effectColor=new Color(0,0,0,.8f);
        float x=30;
        Button(bottom,"◀  Fazenda",ref x,230,Accent,()=>Switch(-1));
        Button(bottom,"Fazenda  ▶",ref x,230,Accent,()=>Switch(1));
        x+=30;
        foreach(var l in layerNames){string name=l;toggles[l]=Button(bottom,l=="Cameras"?"Câmeras":l,ref x,190,Accent,()=>Toggle(name));}
        x+=30;
        Button(bottom,"Reposicionar",ref x,230,Off,Reposition);
    }
    RectTransform Box(Transform parent,string name,Vector2 min,Vector2 max,Vector2 offMin,Vector2 offMax,Color color)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);
        var rt=(RectTransform)go.transform;rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=offMin;rt.offsetMax=offMax;go.GetComponent<Image>().color=color;return rt;
    }
    Text Label(Transform parent,string text,int size,Color color,Vector2 min,Vector2 max,Vector2 offMin,Vector2 offMax,TextAnchor anchor)
    {
        var go=new GameObject("Texto",typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);
        var rt=(RectTransform)go.transform;rt.anchorMin=min;rt.anchorMax=max;rt.offsetMin=offMin;rt.offsetMax=offMax;
        var t=go.GetComponent<Text>();t.font=font;t.fontSize=size;t.color=color;t.alignment=anchor;t.text=text;t.horizontalOverflow=HorizontalWrapMode.Overflow;t.raycastTarget=false;return t;
    }
    Image Button(Transform parent,string text,ref float x,float width,Color color,UnityEngine.Events.UnityAction click)
    {
        var rt=Box(parent,text,new Vector2(0,.5f),new Vector2(0,.5f),new Vector2(x,-45),new Vector2(x+width,45),color);x+=width+16;
        var b=rt.gameObject.AddComponent<Button>();b.onClick.AddListener(click);
        Label(rt,text,28,new Color(.08f,.09f,.11f),Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero,TextAnchor.MiddleCenter);
        return rt.GetComponent<Image>();
    }
}
