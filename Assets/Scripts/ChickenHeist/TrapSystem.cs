using UnityEngine;

public class TrapSystem : MonoBehaviour
{
    public float slowDuration = 2.5f;
    public bool triggered;

    private void OnTriggerEnter(Collider other)
    {
        if (!FarmSecurityProgression.Installed || GameMenu.BlocksInput || HeistGameManager.Instance?.IsMissionTarget(this)!=true || triggered || !other.CompareTag("Player"))
            return;

        triggered = true;
        NoiseEmitter.EmitGlobal(NoiseSource.TrapTriggered, transform.position);

        PlayerMovement movement = other.GetComponent<PlayerMovement>();
        if (movement != null)
            StartCoroutine(SlowPlayer(movement));

        if (HeistGameManager.Instance != null)
            HeistGameManager.Instance.ShowMessage("Armadilha acionada. Barulho alto.", 3f);
    }

    private System.Collections.IEnumerator SlowPlayer(PlayerMovement movement)
    {
        float normal = movement.velocidadeNormal;
        float sprint = movement.velocidadeSprint;
        movement.velocidadeNormal *= 0.35f;
        movement.velocidadeSprint *= 0.35f;
        yield return new WaitForSeconds(slowDuration);
        movement.velocidadeNormal = normal;
        movement.velocidadeSprint = sprint;
    }
}
