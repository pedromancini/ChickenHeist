using UnityEngine;
public class RuralGate : MonoBehaviour
{
    bool opened;HingedBarrier hinge;
    public bool IsOpen=>opened;
    public Vector3 InteractionPoint {get{Ensure();return hinge.InteractionPoint;}}
    void Awake(){Ensure();}
    void Ensure(){if(hinge==null){hinge=GetComponent<HingedBarrier>();if(hinge==null)hinge=gameObject.AddComponent<HingedBarrier>();hinge.SetOpen(opened,true);}}
    public void RestoreOpen(bool value){Ensure();opened=value;hinge.SetOpen(value,true);}
    void Update()
    {
        if(GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen)return;
        var game=HeistGameManager.Instance;if(game?.player==null || !WorldInteraction.Pressed(this))return;
        Ensure();if(Vector3.Distance(game.player.position+Vector3.up,hinge.InteractionPoint)>3.2f)return;
        opened=!opened;hinge.SetOpen(opened,false);NoiseEmitter.EmitGlobal(NoiseSource.FloorCreak,hinge.InteractionPoint);
        MissionNavigation.Instance?.Refresh();
    }
}
