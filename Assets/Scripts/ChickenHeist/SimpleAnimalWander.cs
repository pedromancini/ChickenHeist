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

    private void Start()
    {
        origin = transform.position;
        PickTarget();
    }

    private void Update()
    {
        if (Time.time >= nextPickTime)
            PickTarget();

        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
        Vector3 direction = target - transform.position;
        if (direction.sqrMagnitude > 0.05f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 5f * Time.deltaTime);

        if (Random.value < noiseChancePerSecond * Time.deltaTime)
            NoiseEmitter.EmitGlobal(occasionalNoise, transform.position);
    }

    private void PickTarget()
    {
        nextPickTime = Time.time + Random.Range(3f, 8f);
        Vector2 offset = Random.insideUnitCircle * radius;
        target = origin + new Vector3(offset.x, 0f, offset.y);
    }
}
