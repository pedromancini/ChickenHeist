using UnityEngine;

public class HomeDoor : MonoBehaviour
{
    public Transform hinge;
    public Collider doorCollider;
    public bool opened;
    float angle;
    public void RestoreOpen(bool value)
    {
        opened=value;angle=value?100:0;
        hinge.localRotation=Quaternion.Euler(0,angle,0);doorCollider.enabled=true;
    }
    void Update()
    {
        if(GameMenu.BlocksInput)return;
        var game=HeistGameManager.Instance;
        if(game==null || game.player==null)return;
        if(!ProtagonistPhone.IsOpen && Vector3.Distance(game.player.position,transform.position)<2.6f && Input.GetKeyDown(KeyCode.E))
        {
            Vector3 p=transform.InverseTransformPoint(game.player.position);
            if(opened && Mathf.Abs(p.x)<1.15f && Mathf.Abs(p.z)<.8f)
                game.ShowMessage("Afaste-se da passagem para fechar a porta.");
            else opened=!opened;
        }
        angle=Mathf.MoveTowards(angle,opened?100:0,Time.deltaTime*150);
        hinge.localRotation=Quaternion.Euler(0,angle,0);
        doorCollider.enabled=Mathf.Abs(angle-(opened?100:0))<.1f;
    }
}
