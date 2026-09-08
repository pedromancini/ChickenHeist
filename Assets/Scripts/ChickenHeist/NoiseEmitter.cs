using System;
using UnityEngine;

public class NoiseEmitter : MonoBehaviour
{
    public static event Action<NoiseSource, Vector3, float> NoiseEmitted;

    public NoiseSource source = NoiseSource.FloorCreak;
    public float multiplier = 1f;
    public float cooldown = 0.5f;

    private float nextAllowedTime;

    public void Emit()
    {
        Emit(source, transform.position, multiplier);
    }

    public void Emit(NoiseSource noiseSource, Vector3 position, float noiseMultiplier = 1f)
    {
        if (Time.time < nextAllowedTime)
            return;

        nextAllowedTime = Time.time + cooldown;
        EmitGlobal(noiseSource, position, noiseMultiplier);
    }

    public static void EmitGlobal(NoiseSource noiseSource, Vector3 position, float noiseMultiplier = 1f)
    {
        NoiseEmitted?.Invoke(noiseSource, position, noiseMultiplier);
    }

    public static void EmitAnimalActivity(NoiseSource noiseSource, Vector3 position)
    {
        var player = HeistGameManager.Instance?.player;
        var movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        // Familiar animal activity is harmless; noisy nearby movement provokes an alarm.
        bool disturbed = movement != null && movement.estaMovendo && movement.nivelRuido > .2f
            && Vector3.SqrMagnitude(player.position - position) < 36f;
        EmitGlobal(noiseSource, position, disturbed ? 1f : 0f);
    }
}
