using UnityEngine;

public class ChickenHeistHUD : MonoBehaviour
{
    public GUISkin skin;

    private void OnGUI()
    {
        HeistGameManager game = HeistGameManager.Instance;
        if (game == null)
            return;

        GUI.skin = skin;
        GUILayout.BeginArea(new Rect(18f, 18f, 460f, 215f), GUI.skin.box);
        GUILayout.Label("CHICKEN HEIST - MUNDO ABERTO");

        if (game.farmerSleep != null)
        {
            GUILayout.Label("Fazendeiro mais alerta: " + Mathf.RoundToInt(game.farmerSleep.CurrentSleep) + "% - " + game.farmerSleep.State);
            Rect sleepRect = GUILayoutUtility.GetRect(400f, 18f);
            GUI.Box(sleepRect, string.Empty);
            float width = sleepRect.width * Mathf.Clamp01(game.farmerSleep.CurrentSleep / 100f);
            GUI.Box(new Rect(sleepRect.x, sleepRect.y, width, sleepRect.height), string.Empty);
        }

        if (game.backpack != null)
            GUILayout.Label("Mochila: " + game.backpack.chickensCarried + "/" + game.backpack.capacity + " galinhas");

        int farmCount = game.farmers == null ? 0 : game.farmers.Length;
        GUILayout.Label("Fazendas no vale: " + farmCount);
        GUILayout.Label("Galinhas no vale: " + game.chickensRemaining);
        GUILayout.Label(game.statusMessage);
        GUILayout.Label("WASD mover | Mouse olhar | C agachar | Shift correr | E interagir | F spray camera | R reiniciar");
        GUILayout.EndArea();

        if (game.missionEnded)
        {
            string title = game.missionWon ? "FUGA CONCLUIDA" : "ASSALTO FRACASSOU";
            GUI.Box(new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.5f - 60f, 360f, 120f),
                title + "\n" + game.statusMessage);
        }
    }
}
