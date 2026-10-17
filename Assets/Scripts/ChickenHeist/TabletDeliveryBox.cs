using UnityEngine;

// The visitor's promise kept: the morning after the opening the doorbell rings, and a cardboard box waits on the porch
// beside the front door with the tablet inside. Opening it (E) hands the tablet over: the account stops waiting for it
// and Elias keeps it with him (Tab opens it). The tablet never passes between the actors' hands.
public sealed class TabletDeliveryBox : MonoBehaviour
{
    GameObject box;bool rang;float ringAt=-1;
    public static TabletDeliveryBox Ensure(Transform home)
    {
        var found=home.GetComponentInChildren<TabletDeliveryBox>(true);if(found!=null)return found;
        var node=new GameObject("Caixa do tablet - varanda");node.transform.SetParent(home,false);return node.AddComponent<TabletDeliveryBox>();
    }
    public bool Available=>HouseholdEconomy.Instance!=null && HouseholdEconomy.Instance.Account.TabletAtDoor && !StoryDirector.Active;
    public bool Shown=>box!=null && box.activeInHierarchy;
    public Vector3 InteractionPoint=>box!=null?box.transform.position+Vector3.up*.2f:transform.position;
    void Start()
    {
        var home=transform.parent;var door=home.GetComponentInChildren<HomeDoor>();
        // on the porch beside the latch side of the front door (the door swings out over the hinge side)
        Vector3 spot=door!=null?door.hinge.parent.TransformPoint(door.hinge.localPosition+new Vector3(2.05f,0,-.55f)):home.TransformPoint(new Vector3(-2.2f,.63f,1.9f));
        // on the porch boards: the nearest floor under it (the ground lies further down, under the porch)
        float nearest=float.PositiveInfinity;
        foreach(var hit in Physics.RaycastAll(spot+Vector3.up*1.2f,Vector3.down,2.5f,~0,QueryTriggerInteraction.Ignore))
            if(hit.distance<nearest && Vector3.Dot(hit.normal,Vector3.up)>.7f && !hit.transform.IsChildOf(HeistGameManager.Instance.player)){nearest=hit.distance;spot.y=hit.point.y;}
        box=new GameObject("Caixa de papelao");box.transform.SetParent(transform,false);
        box.transform.position=spot;box.transform.rotation=(door!=null?door.hinge.parent.rotation:home.rotation)*Quaternion.Euler(0,-14,0);
        var cardboard=SecurityEquipmentVisual.Material("Papelao",new Color(.60f,.43f,.26f));
        var tape=SecurityEquipmentVisual.Material("Fita adesiva",new Color(.74f,.66f,.48f));
        var label=SecurityEquipmentVisual.Material("Etiqueta",new Color(.92f,.90f,.84f));
        SecurityEquipmentVisual.Part(box.transform,"Caixa",PrimitiveType.Cube,new Vector3(0,.17f,0),new Vector3(.52f,.34f,.38f),cardboard);
        SecurityEquipmentVisual.Part(box.transform,"Fita",PrimitiveType.Cube,new Vector3(0,.341f,0),new Vector3(.53f,.004f,.07f),tape);
        SecurityEquipmentVisual.Part(box.transform,"Fita lateral",PrimitiveType.Cube,new Vector3(.262f,.29f,0),new Vector3(.004f,.10f,.07f),tape);
        SecurityEquipmentVisual.Part(box.transform,"Fita lateral",PrimitiveType.Cube,new Vector3(-.262f,.29f,0),new Vector3(.004f,.10f,.07f),tape);
        SecurityEquipmentVisual.Part(box.transform,"Etiqueta",PrimitiveType.Cube,new Vector3(.10f,.2f,-.191f),new Vector3(.16f,.10f,.003f),label);
        var solid=box.AddComponent<BoxCollider>();solid.center=new Vector3(0,.17f,0);solid.size=new Vector3(.52f,.34f,.38f);
        box.SetActive(Available);
    }
    void Update()
    {
        if(box==null)return;
        bool visible=Available;if(box.activeSelf!=visible)box.SetActive(visible);
        if(!visible)return;
        // the morning delivery: the old bell stutters a moment after the day starts
        if(!rang)
        {
            if(ringAt<0)ringAt=Time.time+1.5f;
            if(Time.time>=ringAt && !GameMenu.IsOpen){rang=true;HomeDoorbell.Instance?.Ring();HeistGameManager.Instance?.ShowMessage("A campainha tocou. Tem uma caixa do lado da porta.",8);}
        }
        if(!GameMenu.BlocksInput && !ProtagonistPhone.IsOpen && WorldInteraction.Pressed(this))Open();
    }
    public bool Open()
    {
        if(!Available)return false;
        if(!HouseholdEconomy.Instance.Commit(a=>{a.tabletWaiting=false;return true;},"Dentro da caixa estava o tablet do visitante."))return false;
        box.SetActive(false);
        var checkpoint=FindAnyObjectByType<GameCheckpoint>();checkpoint?.Save(out _);
        HeistGameManager.Instance?.ShowMessage("TAB  |  O tablet estava na caixa e agora fica com voce. Examine as fazendas antes de decidir.",9);
        return true;
    }
}
