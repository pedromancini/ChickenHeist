using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(200)]
public class TruckCageLids : MonoBehaviour
{
    public static TruckCageLids Active {get;private set;}
    public bool IsAnimating=>Active==this;
    public bool HandsOnLid {get;private set;}
    public Vector3 LeftHand {get;private set;}
    public Vector3 RightHand {get;private set;}
    Transform[] lids=new Transform[4];
    OldPickupTruck truck;
    PlayerMovement movement;
    bool wasEnabled;
    PlayerLook look;
    bool lookEnabled;
    int closing=-1;
    void Awake()
    {
        truck=GetComponent<OldPickupTruck>();
        for(int i=0;i<4;i++)
        {
            var existing=truck.cages[i].transform.Find("Tampa removivel");
            if(existing!=null){lids[i]=existing;continue;}
            var lid=new GameObject("Tampa removivel").transform;lid.SetParent(truck.cages[i].transform,false);lid.localPosition=Vector3.up*.655f;lids[i]=lid;
            var material=truck.cages[i].GetComponentInChildren<Renderer>(true).sharedMaterial;
            for(int n=0;n<5;n++)Bar(lid,new Vector3(-.36f+n*.18f,0,0),new Vector3(.023f,.026f,.79f),material);
            for(int side=-1;side<=1;side+=2)Bar(lid,new Vector3(0,0,side*.38f),new Vector3(.76f,.03f,.028f),material);
        }
    }
    static void Bar(Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var bar=GameObject.CreatePrimitive(PrimitiveType.Cube);bar.name="Grade da tampa";bar.transform.SetParent(parent,false);bar.transform.localPosition=position;bar.transform.localScale=scale;
        Destroy(bar.GetComponent<Collider>());bar.GetComponent<Renderer>().sharedMaterial=material;
    }
    void Update()
    {
        int count=HouseholdEconomy.Instance?.Account.truckChickens??0;
        for(int i=0;i<4;i++)if(i!=closing)lids[i].gameObject.SetActive(count>=(i+1)*2);
    }
    public bool Closed(int cage)=>lids[cage].gameObject.activeSelf && closing!=cage;
    public void CloseFullCage(int cage)
    {
        if(Active!=null || cage<0 || cage>=4)return;
        StartCoroutine(Place(cage));
    }
    IEnumerator Place(int cage)
    {
        Active=this;closing=cage;
        var player=HeistGameManager.Instance.player;movement=player.GetComponent<PlayerMovement>();wasEnabled=movement.enabled;movement.enabled=false;
        look=player.GetComponentInChildren<PlayerLook>();lookEnabled=look.enabled;
        int side=cage%2==0?-1:1;
        movement.estaMovendo=false;
        var lid=lids[cage];lid.gameObject.SetActive(true);HandsOnLid=false;
        var end=truck.cages[cage].transform.TransformPoint(Vector3.up*.655f);
        var start=end+truck.transform.right*side*.22f+Vector3.up*.12f;
        for(float t=0;t<1;t+=Time.deltaTime/1.1f)
        {
            if(GameMenu.IsOpen){yield return null;continue;}
            lid.position=Vector3.Lerp(start,end,Mathf.SmoothStep(0,1,t));
            var near=lid.position+truck.transform.right*side*.34f;
            LeftHand=near-player.right*.19f;RightHand=near+player.right*.19f;
            yield return null;
        }
        lid.localPosition=Vector3.up*.655f;HandsOnLid=false;closing=-1;
        movement.enabled=wasEnabled;look.enabled=lookEnabled;Active=null;
        HeistGameManager.Instance.ShowMessage("Gaiola cheia. Tampa colocada.",2);
    }
    public void CancelPlacement()
    {
        if(Active!=this)return;
        StopAllCoroutines();Active=null;HandsOnLid=false;
        if(closing>=0)lids[closing].localPosition=Vector3.up*.655f;
        closing=-1;if(movement!=null){movement.enabled=wasEnabled;movement.estaMovendo=false;}
        if(look!=null)look.enabled=lookEnabled;
    }
    void OnDisable(){CancelPlacement();}
}
