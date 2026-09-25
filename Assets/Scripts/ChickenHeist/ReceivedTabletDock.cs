using System.Linq;
using UnityEngine;

// Recreated from introSeen after loading. It is scenery, not a second inventory item.
public sealed class ReceivedTabletDock : MonoBehaviour
{
    VisitorCinematicProps props;Transform visual;Renderer[] replaced;bool[] previous;
    public Vector3 InteractionPoint=>props!=null?props.tablet.position:transform.position;
    public bool Available=>HouseholdEconomy.Instance?.Account.introSeen==true && !StoryDirector.Active;
    public static ReceivedTabletDock Ensure(Transform home)
    {
        var found=home.GetComponentInChildren<ReceivedTabletDock>();if(found!=null)return found;
        var node=new GameObject("Tablet entregue - mesa de Elias");node.transform.SetParent(home,false);return node.AddComponent<ReceivedTabletDock>();
    }
    void Start()
    {
        var home=transform.parent;Vector3 desk=new Vector3(-5.8f,1.43f,3.05f);
        var table=home.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Prop_Wooden_Table_02");
        if(table!=null){var b=ProceduralFarmGenerator.VisualBounds(table.gameObject);desk=home.InverseTransformPoint(new Vector3(b.center.x,b.max.y+.025f,b.center.z));}
        visual=new GameObject("Aparelho permanente").transform;visual.SetParent(transform,false);
        props=new VisitorCinematicProps(visual,home,null,null,desk);
        props.tablet.localPosition=desk+new Vector3(.19f,.04f,.05f);props.tablet.localRotation=Quaternion.Euler(90,180,5);props.tablet.gameObject.SetActive(true);
        var collider=props.tablet.gameObject.AddComponent<BoxCollider>();collider.size=new Vector3(.49f,.31f,.03f);
        replaced=home.GetComponentsInChildren<Renderer>(true).Where(r=>!r.transform.IsChildOf(transform) && (r.name=="Celular antigo - tela rachada" || r.name=="Tela" || r.name=="Vidro rachado")).ToArray();
        previous=replaced.Select(r=>r.forceRenderingOff).ToArray();Refresh();
    }
    void Update()
    {
        Refresh();if(!Available || GameMenu.BlocksInput || ProtagonistPhone.IsOpen)return;
        if(WorldInteraction.Pressed(this))ProtagonistPhone.Instance?.SetOpen(true);
    }
    void Refresh()
    {
        if(visual==null || props==null || replaced==null)return;visual.gameObject.SetActive(Available);
        if(StoryDirector.Active)return;
        for(int i=0;i<replaced.Length;i++)if(replaced[i]!=null)replaced[i].forceRenderingOff=Available || previous[i];
    }
    void OnDestroy(){props?.Dispose();if(replaced!=null)for(int i=0;i<replaced.Length;i++)if(replaced[i]!=null)replaced[i].forceRenderingOff=previous[i];}
}
