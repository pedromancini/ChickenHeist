using UnityEngine;

public class ExtractionZone : MonoBehaviour
{
    public string returnPrompt = "Ponto de retorno: pressione E para encerrar a noite.";
    private bool playerInside;

    private void Update()
    {
        if(GameMenu.BlocksInput)return;
        if (!ProtagonistPhone.IsOpen && playerInside && Input.GetKeyDown(KeyCode.E) && HeistGameManager.Instance != null)
        {
            if (HeistGameManager.Instance.MissionActive || HeistGameManager.Instance.backpack.chickensCarried > 0)
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
        if(HeistGameManager.Instance.MissionActive || HeistGameManager.Instance.backpack.chickensCarried>0)
            HeistGameManager.Instance.ShowMessage("Retorno ao sitio.", 2f);
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
            playerInside = false;
    }
}
