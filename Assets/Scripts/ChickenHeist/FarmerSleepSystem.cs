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
    public const float WakeThreshold=65;
    bool visualContact;
    public void SetVisualContact(bool visible){visualContact=visible;UpdateState();}

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
        if((position-transform.position).sqrMagnitude<.01f)return;
        float distance = Vector3.Distance(transform.position, position);
        if (distance > hearingRange)
            return;

        // Familiar vehicle noise cannot wake a sleeping farmer by itself.
        if(source==NoiseSource.VehicleEngine && CurrentSleep<WakeThreshold)return;

        float distanceFactor = Mathf.Lerp(1f, 0.25f, distance / hearingRange);
        if(multiplier>0 && !GameMenu.BlocksInput && HeistGameManager.Instance?.IsMissionFarmer(this)==true)
            GetComponent<FarmerStateMachine>()?.Hear(position);
        AddNoise((float)source * multiplier * distanceFactor);
    }

    public void AddNoise(float amount)
    {
        if(GameMenu.IsOpen || HeistGameManager.Instance?.IsMissionFarmer(this)!=true)return;
        CurrentSleep = Mathf.Clamp(CurrentSleep + amount * lighterSleepMultiplier * .7f, minimumSleep, 100f);
        UpdateState();
    }

    public void RestoreSleep(float value)
    {
        CurrentSleep=Mathf.Clamp(value,minimumSleep,100);visualContact=false;UpdateState();
    }

    private void UpdateState()
    {
        FarmerAwakeState previous = State;

        if(visualContact)State=FarmerAwakeState.Chase;
        else if (CurrentSleep < WakeThreshold) State = FarmerAwakeState.DeepSleep;
        else if (CurrentSleep < 78f) State = FarmerAwakeState.Restless;
        else if (CurrentSleep < 90f) State = FarmerAwakeState.HalfAlert;
        else if (CurrentSleep < 100f) State = FarmerAwakeState.Searching;
        else State = FarmerAwakeState.Searching;

        if (previous != State && HeistGameManager.Instance?.IsMissionFarmer(this)==true)
            HeistGameManager.Instance.ShowMessage(GetStateMessage(), 3f);
    }

    public string GetStateMessage()
    {
        switch (State)
        {
            case FarmerAwakeState.DeepSleep: return "Sono profundo: o fazendeiro nao reage a ruidos pequenos.";
            case FarmerAwakeState.Restless: return "O fazendeiro acordou com o barulho e esta se levantando.";
            case FarmerAwakeState.HalfAlert: return "Alerta parcial: ele pegou a lanterna.";
            case FarmerAwakeState.Searching: return "Procurando: ele esta patrulhando a casa.";
            case FarmerAwakeState.Chase: return "Ele te viu! Procure cobertura antes do disparo.";
            default: return string.Empty;
        }
    }
}

