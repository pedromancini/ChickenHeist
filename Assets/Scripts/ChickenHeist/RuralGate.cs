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
        Toggle(game.player.position);
    }
    // Opened or shut by someone standing at `from`: it swings away from them, or into the farm when an alarm wire runs
    // across the way in just outside it (TrapSystem), so the leaf never sweeps through the wire.
    public void Toggle(Vector3 from)
    {
        Ensure();opened=!opened;hinge.SetOpen(opened,false,TrapSystem.Guards(this)?(Vector3?)null:from);
        GameAudioMix.Instance?.PlayRecorded("gate_metal",hinge.InteractionPoint,.4f);NoiseEmitter.EmitGlobal(NoiseSource.FloorCreak,hinge.InteractionPoint);
        MissionNavigation.Instance?.Refresh();
    }
}
