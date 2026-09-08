using UnityEngine;

public enum FarmerAwakeState
{
    DeepSleep,
    Restless,
    HalfAlert,
    Searching,
    Chase
}

public class FarmerSleepSystem : MonoBehaviour
{
    [Range(0f, 100f)] public float startingSleep = 12f;
    public float minimumSleep = 6f;
    public float quietDecayPerSecond = 1.2f;
    public float lighterSleepMultiplier = 1f;
    public float hearingRange = 65f;
    public Transform player;

    public float CurrentSleep { get; private set; }
    public FarmerAwakeState State { get; private set; }

    private void OnEnable()
    {
        NoiseEmitter.NoiseEmitted += OnNoiseEmitted;
    }

    private void OnDisable()
    {
        NoiseEmitter.NoiseEmitted -= OnNoiseEmitted;
    }

    private void Start()
    {
        // Large procedural lots must keep their own coop inside hearing range.
        var farm = GetComponentInParent<FarmLayoutInfo>();
        if (farm != null)
            foreach (var coop in farm.GetComponentsInChildren<ChickenCoopLockpick>())
                hearingRange = Mathf.Max(hearingRange, Vector3.Distance(transform.position, coop.transform.position) + 18f);
        CurrentSleep = startingSleep;
        UpdateState();
    }

    private void Update()
    {
        if(GameMenu.IsOpen || HeistGameManager.Instance?.IsMissionFarmer(this)!=true)return;
        if (CurrentSleep < 100f)
        {
            CurrentSleep = Mathf.Max(minimumSleep, CurrentSleep - quietDecayPerSecond * Time.deltaTime);
            UpdateState();
        }
    }

    private void OnNoiseEmitted(NoiseSource source, Vector3 position, float multiplier)
    {
        float distance = Vector3.Distance(transform.position, position);
        if (distance > hearingRange)
            return;

        float distanceFactor = Mathf.Lerp(1f, 0.25f, distance / hearingRange);
        AddNoise((float)source * multiplier * distanceFactor);
    }

    public void AddNoise(float amount)
    {
        if(GameMenu.IsOpen || HeistGameManager.Instance?.IsMissionFarmer(this)!=true)return;
        CurrentSleep = Mathf.Clamp(CurrentSleep + amount * lighterSleepMultiplier, minimumSleep, 100f);
        UpdateState();
    }

    public void RestoreSleep(float value)
    {
        CurrentSleep=Mathf.Clamp(value,minimumSleep,100);UpdateState();
    }

    private void UpdateState()
    {
        FarmerAwakeState previous = State;

        if (CurrentSleep < 30f) State = FarmerAwakeState.DeepSleep;
        else if (CurrentSleep < 60f) State = FarmerAwakeState.Restless;
        else if (CurrentSleep < 80f) State = FarmerAwakeState.HalfAlert;
        else if (CurrentSleep < 100f) State = FarmerAwakeState.Searching;
        else State = FarmerAwakeState.Chase;

        if (previous != State && HeistGameManager.Instance?.IsMissionFarmer(this)==true)
            HeistGameManager.Instance.ShowMessage(GetStateMessage(), 3f);
    }

    public string GetStateMessage()
    {
        switch (State)
        {
            case FarmerAwakeState.DeepSleep: return "Sono profundo: o fazendeiro nao reage a ruidos pequenos.";
            case FarmerAwakeState.Restless: return "O fazendeiro se mexe na cama.";
            case FarmerAwakeState.HalfAlert: return "Alerta parcial: ele pegou a lanterna.";
            case FarmerAwakeState.Searching: return "Procurando: ele esta patrulhando a casa.";
            case FarmerAwakeState.Chase: return "Perseguicao: corre.";
            default: return string.Empty;
        }
    }
}
