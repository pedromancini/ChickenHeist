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
}
