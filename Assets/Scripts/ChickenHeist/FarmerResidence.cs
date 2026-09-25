using UnityEngine;

public class FarmerResidence : MonoBehaviour
{
    public Transform bedPosition,insideDoor,outsideDoor;
    public Transform doorHinge;
    public Collider doorCollider;
    public bool DoorOpen {get;private set;}
    float angle;
    public bool AtBed=>bedPosition!=null && Vector3.Distance(transform.position,bedPosition.position)<.35f;
    public void SetDoor(bool open,bool immediate=false)
    {DoorOpen=open;if(immediate){angle=open?105:0;Apply();}}
    public bool DoorReady=>DoorOpen && angle>100;
    void Apply(){if(doorHinge!=null)doorHinge.localRotation=Quaternion.Euler(0,angle,0);}
    void Update(){if(GameMenu.BlocksInput)return;angle=Mathf.MoveTowards(angle,DoorOpen?105:0,100*Time.deltaTime);Apply();}
}
