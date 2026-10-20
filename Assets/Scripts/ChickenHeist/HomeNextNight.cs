using UnityEngine;
using UnityEngine.SceneManagement;

public class HomeNextNight : MonoBehaviour
{
    public static bool IsResting {get;private set;}
    // set when a night's sleep reloads the world, so the morning starts with the rooster (GameAudioMix)
    public static bool JustWoke;
    public static HomeNextNight Instance {get;private set;}
    void Awake(){Instance=this;}
    float fade;
    public float distance=1.7f;
    bool prepared;
    // the same reach as the "E  Descansar" prompt (WorldInteraction): where the prompt shows, E sleeps
    bool Near=>HeistGameManager.Instance?.player!=null && Vector3.Distance(transform.position,HeistGameManager.Instance.player.position+Vector3.up*.5f)<distance+.5f;
    void Update()
    {
        if(WorldInteraction.Pressed(this))TryRest();
    }
    // From the bed, or from anywhere when the day's end prompt sends Elias to sleep (SleepPrompt).
    public bool RestNow()=>TryRest(true);
    public bool TryRest()=>TryRest(false);
    bool TryRest(bool anywhere)
    {
        if(GameMenu.BlocksInput || !anywhere && !Near || ProtagonistPhone.IsOpen || VillageMarket.IsOpen)return false;
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
        JustWoke=true;SceneManager.LoadScene(SceneManager.GetActiveScene().path);
    }
    void OnDestroy(){IsResting=false;if(Instance==this)Instance=null;}
    void OnGUI()
    {
        if(IsResting)
        {
            var old=GUI.color;GUI.color=new Color(0,0,0,fade);
            GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=old;
            if(fade>.9f){GUI.skin=UITheme.Skin;GUI.Label(new Rect(0,Screen.height*.46f,Screen.width,UITheme.Size(60)),"Na manhã seguinte...",new GUIStyle(UITheme.Style("heading")){alignment=TextAnchor.MiddleCenter,fontSize=UITheme.Size(34)});}
            return;
        }
        // The "E  Descansar" prompt comes from InteractionFocusHUD, like every other interaction.
    }
}
