using UnityEngine;

public class SecurityCamera : MonoBehaviour
{
    public Transform player;
    public float viewDistance = 14f;
    public float viewAngle = 42f;
    public float rotationArc = 80f;
    public float rotationSpeed = 32f;
    public float detectionCooldown = 1.5f;
    public float disabledDuration = 6f;

    private float startYaw;
    private float nextDetectionTime;
    private float disabledUntil;

    private void Start()
    {
        startYaw = transform.eulerAngles.y;
        CreateVisionCone();
    }

    private void Update()
    {
        if (Time.time < disabledUntil)
            return;

        float yaw = startYaw + Mathf.Sin(Time.time * rotationSpeed * Mathf.Deg2Rad) * rotationArc;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (player != null && CanSeePlayer() && Time.time >= nextDetectionTime)
        {
            nextDetectionTime = Time.time + detectionCooldown;
            NoiseEmitter.EmitGlobal(NoiseSource.CameraDetected, player.position);
            HeistGameManager.Instance.ShowMessage("Camera te viu. O fazendeiro ouviu o alerta.", 2f);
        }

        if (player != null && Input.GetKeyDown(KeyCode.F) && Vector3.Distance(player.position, transform.position) < 3f)
        {
            disabledUntil = Time.time + disabledDuration;
            HeistGameManager.Instance.ShowMessage("Camera desativada por alguns segundos.", 2f);
        }
    }

    private bool CanSeePlayer()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        if (toPlayer.magnitude > viewDistance)
            return false;

        if (Vector3.Angle(transform.forward, toPlayer.normalized) > viewAngle)
            return false;

        Vector3 eye = transform.position + Vector3.up * 0.6f;
        Vector3 target = player.position + Vector3.up;
        if (Physics.Linecast(eye, target, out RaycastHit hit))
            return hit.transform == player || hit.transform.IsChildOf(player);

        return true;
    }

    private void CreateVisionCone()
    {
        GameObject cone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cone.name = "VisionCone";
        cone.transform.SetParent(transform, false);
        cone.transform.localPosition = new Vector3(0f, -0.45f, viewDistance * 0.5f);
        cone.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        cone.transform.localScale = new Vector3(viewDistance * 0.28f, 0.03f, viewDistance * 0.5f);
        Destroy(cone.GetComponent<Collider>());

        Renderer renderer = cone.GetComponent<Renderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.color = new Color(1f, 0.84f, 0.1f, 0.18f);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_AlphaClip", 0f);
        renderer.material = material;
    }
}
