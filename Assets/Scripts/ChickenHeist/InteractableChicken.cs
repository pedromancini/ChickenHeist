using UnityEngine;

public class InteractableChicken : MonoBehaviour
{
    public float interactionDistance = 2.4f;
    public bool sleeping;
    public float cluckChancePerSecond = 0.08f;
    public ChickenCoopLockpick coop;
    private FarmAnimalBoundary boundary;

    private Transform player;
    private Vector3 wanderTarget;
    private float nextWanderTime;
    static int pickupFrame=-1;

    private void Start()
    {
        boundary = GetComponent<FarmAnimalBoundary>();
        if (HeistGameManager.Instance != null)
        {
            player = HeistGameManager.Instance.player;
            HeistGameManager.Instance.RegisterChicken();
        }

        PickWanderTarget();
    }

    private void Update()
    {
        if(GameMenu.BlocksInput)return;
        if (!ProtagonistPhone.IsOpen && !VillageMarket.IsOpen && HeistGameManager.Instance?.missionEnded!=true && player != null && WorldInteraction.Pressed(this) && IsPlayerClose())
            TrySteal();

        if (sleeping)
            return;

        if (Time.time >= nextWanderTime)
            PickWanderTarget();

        Vector3 nextPosition = Vector3.MoveTowards(transform.position, wanderTarget, 0.7f * Time.deltaTime);
        if (boundary == null || boundary.Allows(nextPosition)) transform.position = nextPosition;
        else PickWanderTarget();
        Vector3 direction = wanderTarget - transform.position;
        if (direction.sqrMagnitude > 0.05f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 6f * Time.deltaTime);

        if (Random.value < cluckChancePerSecond * Time.deltaTime)
            NoiseEmitter.EmitAnimalActivity(NoiseSource.ChickenCluck, transform.position);
    }

    private bool IsPlayerClose()
    {
        if(player==null || (coop!=null && !coop.IsOpen) || Vector3.Distance(player.position,transform.position)>interactionDistance)return false;
        Vector3 origin=player.position+Vector3.up*.9f,target=transform.position+Vector3.up*.2f;
        foreach(var hit in Physics.RaycastAll(origin,(target-origin).normalized,Vector3.Distance(origin,target),~0,QueryTriggerInteraction.Ignore))
            if(!hit.transform.IsChildOf(player) && !hit.transform.IsChildOf(transform))return false;
        return true;
    }

    public bool TrySteal()
    {
        if(!isActiveAndEnabled || ChickenScare.Active!=null || ChickenCoopLockpick.Active!=null || ChickenCoopLockpick.ClosedFrame==Time.frameCount || GameMenu.BlocksInput || HeistGameManager.Instance?.IsMissionTarget(this)!=true)return false;
        if(pickupFrame==Time.frameCount || player==null || !IsPlayerClose() || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || HeistGameManager.Instance?.missionEnded==true)return false;
        BackpackInventory backpack = player.GetComponent<BackpackInventory>();
        if (backpack == null || backpack.IsFull)
        {
            HeistGameManager.Instance.ShowMessage("Voce ja carrega uma galinha. Coloque-a na gaiola antes de pegar outra.", 2f);
            return false;
        }

        if (backpack.TryAddChicken())
        {
            pickupFrame=Time.frameCount;
            if (!sleeping)
                NoiseEmitter.EmitGlobal(NoiseSource.ChickenCluck, transform.position);

            player.GetComponent<PlayerChickenCarry>()?.Lift(this);
            HeistGameManager.Instance.ChickenStolen();
            gameObject.SetActive(false);
            return true;
        }
        return false;
    }

    private void PickWanderTarget()
    {
        nextWanderTime = Time.time + Random.Range(2f, 6f);
        if (boundary != null) { wanderTarget = boundary.PickTarget(transform.position, 3.5f); return; }
        Vector2 offset = Random.insideUnitCircle * 3.5f;
        wanderTarget = new Vector3(transform.position.x + offset.x, transform.position.y, transform.position.z + offset.y);
    }
}
