using UnityEngine;

public class ExtractionZone : MonoBehaviour
{
    private bool playerInside;

    private void Update()
    {
        if (playerInside && Input.GetKeyDown(KeyCode.E) && HeistGameManager.Instance != null)
        {
            if (HeistGameManager.Instance.backpack.chickensCarried > 0)
                HeistGameManager.Instance.CompleteMission();
            else
                HeistGameManager.Instance.ShowMessage("Voce ainda nao roubou nenhuma galinha.", 2f);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInside = true;
        HeistGameManager.Instance.ShowMessage("Caminhonete: pressione E para fugir.", 2f);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInside = false;
    }
}
