using UnityEngine;

public class RuralGate : MonoBehaviour
{
    private bool opened;
    public bool IsOpen=>opened;
    public void RestoreOpen(bool value)
    {
        if(opened!=value)transform.position+=Vector3.down*(value?1.6f:-1.6f);
        opened=value;foreach(var c in GetComponentsInChildren<Collider>())c.enabled=!value;
    }
    private void Update()
    {
        if(GameMenu.BlocksInput)return;
        var game = HeistGameManager.Instance;
        if (ProtagonistPhone.IsOpen || opened || game == null || game.player == null || !Input.GetKeyDown(KeyCode.E)) return;
        if (Vector3.Distance(game.player.position, transform.position) > 3.2f) return;
        opened = true;
        foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        transform.position += Vector3.down * 1.6f;
        NoiseEmitter.EmitGlobal(NoiseSource.FloorCreak, transform.position);
    }
}
