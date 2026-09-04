using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class FarmerStateMachine : MonoBehaviour
{
    public FarmerSleepSystem sleepSystem;
    public Transform player;
    public Transform[] patrolPoints;
    public float patrolSpeed = 2.1f;
    public float chaseSpeed = 4.5f;
    public float catchDistance = 1.8f;

    private CharacterController controller;
    private int patrolIndex;
    private Vector3 velocity;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (sleepSystem == null || player == null || HeistGameManager.Instance == null || HeistGameManager.Instance.missionEnded)
            return;

        if (sleepSystem.State == FarmerAwakeState.Chase)
        {
            MoveToward(player.position, chaseSpeed);

            if (Vector3.Distance(transform.position, player.position) <= catchDistance)
                HeistGameManager.Instance.FailMission("Voce foi pego pelo fazendeiro.");
        }
        else if (sleepSystem.State == FarmerAwakeState.Searching && patrolPoints != null && patrolPoints.Length > 0)
        {
            Transform target = patrolPoints[patrolIndex];
            MoveToward(target.position, patrolSpeed);

            if (Vector3.Distance(transform.position, target.position) < 1.2f)
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
        }
        else
        {
            ApplyGravity();
        }
    }

    private void MoveToward(Vector3 target, float speed)
    {
        Vector3 flatTarget = new Vector3(target.x, transform.position.y, target.z);
        Vector3 direction = flatTarget - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion lookRotation = Quaternion.LookRotation(direction.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, 8f * Time.deltaTime);
            controller.Move(direction.normalized * speed * Time.deltaTime);
        }

        ApplyGravity();
    }

    private void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0f)
            velocity.y = -1f;

        velocity.y += Physics.gravity.y * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}
