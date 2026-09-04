using UnityEngine;

public class InteractableChicken : MonoBehaviour
{
    public float interactionDistance = 2.4f;
    public bool sleeping;
    public float cluckChancePerSecond = 0.08f;

    private Transform player;
    private Vector3 wanderTarget;
    private float nextWanderTime;

    private void Start()
    {
        if (HeistGameManager.Instance != null)
        {
            player = HeistGameManager.Instance.player;
            HeistGameManager.Instance.RegisterChicken();
        }

        PickWanderTarget();
    }

    private void Update()
    {
        if (player != null && Input.GetKeyDown(KeyCode.E) && IsPlayerClose())
            TrySteal();

        if (sleeping)
            return;

        if (Time.time >= nextWanderTime)
            PickWanderTarget();

        transform.position = Vector3.MoveTowards(transform.position, wanderTarget, 0.7f * Time.deltaTime);
        Vector3 direction = wanderTarget - transform.position;
        if (direction.sqrMagnitude > 0.05f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 6f * Time.deltaTime);

        if (Random.value < cluckChancePerSecond * Time.deltaTime)
            NoiseEmitter.EmitGlobal(NoiseSource.ChickenCluck, transform.position);
    }

    private bool IsPlayerClose()
    {
        return Vector3.Distance(player.position, transform.position) <= interactionDistance;
    }

    private void TrySteal()
    {
        BackpackInventory backpack = player.GetComponent<BackpackInventory>();
        if (backpack == null || backpack.IsFull)
        {
            HeistGameManager.Instance.ShowMessage("Mochila cheia. Hora de ir embora.", 2f);
            return;
        }

        if (backpack.TryAddChicken())
        {
            if (!sleeping)
                NoiseEmitter.EmitGlobal(NoiseSource.ChickenCluck, transform.position);

            HeistGameManager.Instance.ChickenStolen();
            Destroy(gameObject);
        }
    }

    private void PickWanderTarget()
    {
        nextWanderTime = Time.time + Random.Range(2f, 6f);
        Vector2 offset = Random.insideUnitCircle * 3.5f;
        wanderTarget = new Vector3(transform.position.x + offset.x, transform.position.y, transform.position.z + offset.y);
    }
}
