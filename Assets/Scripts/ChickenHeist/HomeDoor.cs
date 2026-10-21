using UnityEngine;

public class HomeDoor : MonoBehaviour
{
    // The front door opens into the house (negative turn), as front doors do; past 76 degrees it would meet the wall
    // beside it. It used to open out across the porch. The opening cinematic uses the same swing.
    public const float OpenAngle=-76f;
    public Transform hinge;
    public Collider doorCollider;
    public bool opened;
    float angle;
    public void RestoreOpen(bool value)
    {
        opened=value;angle=value?OpenAngle:0;
        hinge.localRotation=Quaternion.Euler(0,angle,0);doorCollider.enabled=true;
    }
    void Update()
    {
        if(GameMenu.BlocksInput)return;
        var game=HeistGameManager.Instance;
        if(game==null || game.player==null)return;
        if(!ProtagonistPhone.IsOpen && Vector3.Distance(game.player.position,transform.position)<2.6f && WorldInteraction.Pressed(this))
        {
            Vector3 p=transform.InverseTransformPoint(game.player.position);
            if(opened && Mathf.Abs(p.x)<1.15f && Mathf.Abs(p.z)<.8f)
                game.ShowMessage("Afaste-se da passagem para fechar a porta.");
            else opened=!opened;
        }
        angle=Mathf.MoveTowards(angle,opened?OpenAngle:0,Time.deltaTime*150);
        hinge.localRotation=Quaternion.Euler(0,angle,0);
        doorCollider.enabled=Mathf.Abs(angle-(opened?OpenAngle:0))<.1f;
    }
}
