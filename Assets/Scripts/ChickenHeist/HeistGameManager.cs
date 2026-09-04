using UnityEngine;

public class HeistGameManager : MonoBehaviour
{
    public static HeistGameManager Instance { get; private set; }

    public FarmerSleepSystem farmerSleep;
    public FarmerSleepSystem[] farmers;
    public BackpackInventory backpack;
    public Transform player;
    public int chickensRemaining;
    public int targetChickens = 3;
    public bool missionEnded { get; private set; }
    public bool missionWon { get; private set; }
    public string statusMessage = "Roube galinhas e volte para a caminhonete.";

    private float messageUntil;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        UpdatePrimaryFarmer();

        if (!missionEnded && farmerSleep != null && farmerSleep.CurrentSleep >= 100f)
        {
            statusMessage = farmerSleep.name + " acordou. Fuja!";
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        if (Time.time > messageUntil && missionEnded == false && backpack != null)
        {
            if (backpack.IsFull)
                statusMessage = "Mochila cheia. Volte para a caminhonete.";
            else if (backpack.chickensCarried >= targetChickens)
                statusMessage = "Voce ja tem o suficiente. Extrair agora seria inteligente.";
        }
    }

    private void UpdatePrimaryFarmer()
    {
        if (farmers == null || farmers.Length == 0)
            return;

        FarmerSleepSystem mostDangerous = farmers[0];
        for (int i = 1; i < farmers.Length; i++)
        {
            if (farmers[i] != null && farmers[i].CurrentSleep > mostDangerous.CurrentSleep)
                mostDangerous = farmers[i];
        }

        farmerSleep = mostDangerous;
    }

    public void RegisterChicken()
    {
        chickensRemaining++;
    }

    public void ChickenStolen()
    {
        chickensRemaining = Mathf.Max(0, chickensRemaining - 1);
        ShowMessage("Galinha roubada. O sono do fazendeiro ficou mais leve.", 3f);
    }

    public void ShowMessage(string message, float duration = 2f)
    {
        statusMessage = message;
        messageUntil = Time.time + duration;
    }

    public void CompleteMission()
    {
        missionEnded = true;
        missionWon = true;
        statusMessage = "Missao concluida. Voce escapou com " + backpack.chickensCarried + " galinhas.";
    }

    public void FailMission(string reason)
    {
        missionEnded = true;
        missionWon = false;
        statusMessage = reason + " Pressione R para tentar outra fazenda.";
    }
}
