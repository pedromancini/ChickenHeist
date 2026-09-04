using System.Collections;
using UnityEngine;

public class ChickenCoopLockpick : MonoBehaviour
{
    public Transform door;
    public Transform scareChicken;
    public float interactionDistance = 3.8f;
    public float pickSpeed = 0.72f;
    public float sweetSpotWidth = 0.12f;

    private Transform player;
    private PlayerMovement playerMovement;
    private bool challengeActive;
    private bool doorOpened;
    private float pickPosition = 0.5f;
    private float sweetSpot;
    private int mistakes;
    private float messageUntil;

    private void Start()
    {
        if (HeistGameManager.Instance != null)
        {
            player = HeistGameManager.Instance.player;
            playerMovement = player != null ? player.GetComponent<PlayerMovement>() : null;
        }
        sweetSpot = Random.Range(0.18f, 0.82f);
    }

    private void Update()
    {
        if (doorOpened || player == null)
            return;

        if (!challengeActive && Vector3.Distance(player.position, transform.position) <= interactionDistance && Input.GetKeyDown(KeyCode.E))
            BeginChallenge();

        if (!challengeActive)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            EndChallenge();
            return;
        }

        pickPosition += Input.GetAxisRaw("Horizontal") * pickSpeed * Time.deltaTime;
        pickPosition = Mathf.Clamp01(pickPosition);

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
            TryPick();
    }

    private void BeginChallenge()
    {
        challengeActive = true;
        mistakes = 0;
        pickPosition = 0.5f;
        sweetSpot = Random.Range(0.15f, 0.85f);
        if (playerMovement != null)
            playerMovement.enabled = false;
        ShowMessage("Lockpick: mova com A/D e pressione ESPACO no ponto certo.", 3f);
    }

    private void TryPick()
    {
        if (Mathf.Abs(pickPosition - sweetSpot) <= sweetSpotWidth)
        {
            OpenDoor();
            return;
        }

        mistakes++;
        sweetSpot = Random.Range(0.12f, 0.88f);
        pickPosition = 0.5f;
        NoiseEmitter.EmitGlobal(NoiseSource.CoopLockpickFail, transform.position);
        ShowMessage("A galinha ouviu o cadeado...", 2f);
        if (mistakes == 1 && scareChicken != null)
            StartCoroutine(ChickenJumpscare());
    }

    private void OpenDoor()
    {
        doorOpened = true;
        challengeActive = false;
        if (playerMovement != null)
            playerMovement.enabled = true;
        if (door != null)
            door.gameObject.SetActive(false);
        ShowMessage("Cadeado aberto. Entre devagar no galinheiro.", 3f);
    }

    private void EndChallenge()
    {
        challengeActive = false;
        if (playerMovement != null)
            playerMovement.enabled = true;
        ShowMessage("Lockpick interrompido.", 1.5f);
    }

    private IEnumerator ChickenJumpscare()
    {
        InteractableChicken chicken = scareChicken.GetComponent<InteractableChicken>();
        if (chicken != null)
            chicken.sleeping = true;

        Transform originalParent = scareChicken.parent;
        Vector3 originalPosition = scareChicken.position;
        Quaternion originalRotation = scareChicken.rotation;
        scareChicken.SetParent(null, true);

        Vector3 facePosition = player.position + player.forward * 0.9f + Vector3.up * 1.15f;
        float duration = 0.26f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float bounce = Mathf.Sin(t * Mathf.PI);
            scareChicken.position = Vector3.Lerp(originalPosition, facePosition, t) + Vector3.up * bounce * 0.35f;
            scareChicken.LookAt(player.position + Vector3.up * 0.8f);
            yield return null;
        }

        yield return new WaitForSeconds(0.22f);
        scareChicken.SetParent(originalParent, true);
        scareChicken.position = originalPosition;
        scareChicken.rotation = originalRotation;
        if (chicken != null)
            chicken.sleeping = false;
    }

    private void ShowMessage(string message, float duration)
    {
        messageUntil = Time.time + duration;
        if (HeistGameManager.Instance != null)
            HeistGameManager.Instance.ShowMessage(message, duration);
    }

    private void OnGUI()
    {
        if (!challengeActive)
            return;

        float width = Mathf.Min(560f, Screen.width * 0.72f);
        float left = (Screen.width - width) * 0.5f;
        float top = Screen.height * 0.72f;
        GUI.Box(new Rect(left, top - 52f, width, 112f), "LOCKPICK DO GALINHEIRO");
        GUI.Label(new Rect(left + 18f, top - 24f, width - 36f, 22f), "A/D move o pino    ESPACO tenta abrir    ESC sai");

        GUI.Box(new Rect(left + 18f, top + 7f, width - 36f, 18f), string.Empty);
        float sweetLeft = left + 18f + (width - 36f) * (sweetSpot - sweetSpotWidth);
        float sweetWidth = (width - 36f) * sweetSpotWidth * 2f;
        Color previous = GUI.color;
        GUI.color = new Color(0.35f, 0.8f, 0.38f, 1f);
        GUI.Box(new Rect(sweetLeft, top + 7f, sweetWidth, 18f), string.Empty);
        GUI.color = new Color(1f, 0.75f, 0.2f, 1f);
        GUI.Box(new Rect(left + 18f + (width - 36f) * pickPosition - 3f, top + 2f, 6f, 28f), string.Empty);
        GUI.color = previous;
        GUI.Label(new Rect(left + 18f, top + 32f, width - 36f, 22f), "Erros: " + mistakes + "   Um erro assusta as galinhas e aumenta o alerta.");
    }
}
