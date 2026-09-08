using UnityEngine;

public class SimpleAnimalWander : MonoBehaviour
{
    public float radius = 5f;
    public float speed = 0.5f;
    public NoiseSource occasionalNoise = NoiseSource.CowMoo;
    public float noiseChancePerSecond = 0.025f;

    private Vector3 origin;
    private Vector3 target;
    private float nextPickTime;
    private FarmAnimalBoundary boundary;

    private void Start()
    {
        boundary = GetComponent<FarmAnimalBoundary>();
        origin = transform.position;
        PickTarget();
    }

    private void Update()
    {
        if (Time.time >= nextPickTime)
            PickTarget();

        Vector3 nextPosition = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        if (boundary == null || boundary.Allows(nextPosition)) transform.position = nextPosition;
        else PickTarget();
        Vector3 direction = target - transform.position;
        if (direction.sqrMagnitude > 0.05f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 5f * Time.deltaTime);

        if (Random.value < noiseChancePerSecond * Time.deltaTime)
            NoiseEmitter.EmitAnimalActivity(occasionalNoise, transform.position);
    }

    private void PickTarget()
    {
        nextPickTime = Time.time + Random.Range(3f, 8f);
        if (boundary != null) { target = boundary.PickTarget(transform.position, radius); return; }
        Vector2 offset = Random.insideUnitCircle * radius;
        target = origin + new Vector3(offset.x, 0f, offset.y);
    }
}
