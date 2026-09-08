using UnityEngine;

public class ChickenHeistHUD : MonoBehaviour
{
    public GUISkin skin;

    GUIStyle label,caption;
    void OnGUI()
    {
        var game=HeistGameManager.Instance;
        if(game==null || GameMenu.IsOpen || ProtagonistPhone.IsOpen || VillageMarket.IsOpen)return;
        if(label==null)
        {
            label=new GUIStyle(GUI.skin.label){fontSize=18,wordWrap=true};label.normal.textColor=Color.white;
            caption=new GUIStyle(label){fontSize=14};
        }
        float width=Mathf.Min(330,Screen.width-48);
        if(game.MissionActive)
        {
            GUI.Label(new Rect(24,24,width,48),game.MissionName,label);
            var farmer=game.farmerSleep;
            if(farmer!=null)
            {
                string[] states={"Sono profundo","Sono agitado","Alerta parcial","Procurando","Perseguicao"};
                GUI.Label(new Rect(24,76,width,25),states[(int)farmer.State],caption);
                Draw(new Rect(24,105,width,5),new Color(.15f,.17f,.16f,.8f));
                Draw(new Rect(24,105,width*farmer.CurrentSleep/100,5),Color.Lerp(new Color(.64f,.76f,.58f),new Color(.9f,.33f,.24f),farmer.CurrentSleep/100));
            }
            var phone=ProtagonistPhone.Instance;
            if(phone!=null && game.MissionFarm>=0 && game.MissionFarm<phone.farmPositions.Length)
                GUI.Label(new Rect(24,120,width,28),Mathf.RoundToInt(Vector3.Distance(game.player.position,phone.farmPositions[game.MissionFarm]))+" m",caption);
        }
        if(game.backpack!=null && (game.MissionActive || game.backpack.chickensCarried>0))
            GUI.Label(new Rect(24,Screen.height-50,width,28),"Mochila  "+game.backpack.chickensCarried+" / "+game.backpack.capacity,caption);
        if(game.HasMessage)
            GUI.Label(new Rect(Screen.width*.25f,Screen.height-110,Screen.width*.5f,65),game.statusMessage,caption);
    }
    static void Draw(Rect rect,Color color){var previous=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=previous;}
}
