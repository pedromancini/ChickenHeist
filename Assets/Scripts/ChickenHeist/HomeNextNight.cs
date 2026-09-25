using UnityEngine;
using UnityEngine.SceneManagement;

public class HomeNextNight : MonoBehaviour
{
    public static bool IsResting {get;private set;}
    float fade;
    public float distance=1.7f;
    bool prepared;
    bool Near=>HeistGameManager.Instance?.player!=null && Vector3.Distance(transform.position,HeistGameManager.Instance.player.position+Vector3.up)<distance;
    void Update()
    {
        if(WorldInteraction.Pressed(this))TryRest();
    }
    public bool TryRest()
    {
        if(GameMenu.BlocksInput || !Near || ProtagonistPhone.IsOpen || VillageMarket.IsOpen)return false;
        if(HeistGameManager.Instance.backpack.chickensCarried>0 || (HouseholdEconomy.Instance?.Account.truckChickens??0)>0){HeistGameManager.Instance.ShowMessage("Entregue ou venda as galinhas no colo e da caminhonete antes de dormir.");return false;}
        if(prepared || HeistGameManager.Instance.PrepareNextNight())
        {
            prepared=true;
            var checkpoint=Object.FindAnyObjectByType<GameCheckpoint>();
            if(checkpoint==null || !checkpoint.Save(out _)){HeistGameManager.Instance.ShowMessage("Falha ao salvar. Pressione E para tentar novamente antes de descansar.",6);return false;}
            StartCoroutine(Rest());return true;
        }
        return false;
    }
    System.Collections.IEnumerator Rest()
    {
        IsResting=true;
        if(HouseholdEconomy.Instance.Account.pendingVision)
        {
            StoryDirector.Instance.Begin(true);
            while(StoryDirector.Active)yield return null;
            var checkpoint=Object.FindAnyObjectByType<GameCheckpoint>();
            if(checkpoint==null || !checkpoint.Save(out _)){IsResting=false;HeistGameManager.Instance.ShowMessage("Falha ao salvar. Pressione E para tentar novamente.",6);yield break;}
        }
        while(fade<1){fade=Mathf.Min(1,fade+Time.unscaledDeltaTime);yield return null;}
        yield return new WaitForSecondsRealtime(.65f);
        SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }
    void OnDestroy(){IsResting=false;}
    void OnGUI()
    {
        if(IsResting)
        {
            var old=GUI.color;GUI.color=new Color(0,0,0,fade);
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=old;
            if(fade>.9f)GUI.Label(new Rect(Screen.width*.5f-150,Screen.height*.5f,300,40),"Na manha seguinte...",new GUIStyle(GUI.skin.label){alignment=TextAnchor.MiddleCenter,fontSize=24});
            return;
        }
        if(GameMenu.IsOpen)return;
        if(Near && !ProtagonistPhone.IsOpen && !VillageMarket.IsOpen)
            GUI.Box(new Rect(Screen.width*.5f-170,Screen.height*.75f,340,34),"E  |  Descansar e preparar outra saida");
    }
}
