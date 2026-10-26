using UnityEngine;

public sealed class MissionCompletePopup : MonoBehaviour
{
    public static MissionCompletePopup Instance {get;private set;}
    public bool Showing=>Time.unscaledTime<until;
    public string Farm {get;private set;}="";
    public int Chickens {get;private set;}
    float until,shownAt;
    void Awake(){Instance=this;}
    void OnDestroy(){if(Instance==this)Instance=null;}
    public void Show(string farm,int chickens){Farm=farm;Chickens=chickens;shownAt=Time.unscaledTime;until=shownAt+5;}
    void OnGUI()
    {
        if(!Showing || GameMenu.IsOpen || StoryDirector.Active)return;
        GUI.skin=UITheme.Skin;int depth=GUI.depth;GUI.depth=-120;Color old=GUI.color;
        float age=Time.unscaledTime-shownAt,fade=Mathf.Clamp01(age/.25f)*Mathf.Clamp01((until-Time.unscaledTime)/.5f);
        GUI.color=new Color(1,1,1,fade);
        float w=UITheme.Size(460),h=UITheme.Size(150);var r=new Rect((Screen.width-w)/2,Screen.height*.22f,w,h);
        UITheme.Panel(r,UITheme.PanelFill,UITheme.Good);
        GUI.Label(new Rect(r.x,r.y+UITheme.Size(18),r.width,UITheme.Size(44)),"Missão concluída!",new GUIStyle(UITheme.Style("title")){alignment=TextAnchor.MiddleCenter,fontSize=UITheme.Size(32)});
        string detail=(Farm.Length>0?Farm+"\n":"")+(Chickens>0?Chickens+(Chickens==1?" galinha trazida":" galinhas trazidas")+" para o sítio.":"Você voltou ao sítio sem galinhas.");
        GUI.Label(new Rect(r.x+UITheme.Size(16),r.y+UITheme.Size(68),r.width-UITheme.Size(32),UITheme.Size(70)),detail,
            new GUIStyle(UITheme.Style("body")){alignment=TextAnchor.UpperCenter,fontSize=UITheme.Size(17),wordWrap=true});
        GUI.color=old;GUI.depth=depth;
    }
}
