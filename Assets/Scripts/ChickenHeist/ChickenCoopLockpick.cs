using System.Collections;
using UnityEngine;

[DefaultExecutionOrder(-90)]
public class ChickenCoopLockpick : MonoBehaviour
{
    public static ChickenCoopLockpick Active { get; private set; }
    public static int ClosedFrame { get; private set; }=-1;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics(){Active=null;ClosedFrame=-1;}
    public Transform door;
    public Transform lockAnchor;
    public Transform scareChicken;
    public float interactionDistance = 3.8f;
    public float pickSpeed = 0.72f;
    public float sweetSpotWidth = 0.12f;
    public float EffectiveSweetSpotWidth => Mathf.Min(.24f,sweetSpotWidth*(HouseholdEconomy.Instance?.Account.professionalEquipped==true?1.75f:1f));
    public float EffectivePickSpeed => pickSpeed*(HouseholdEconomy.Instance?.Account.professionalEquipped==true ? .75f : 1f);

    private Transform player;
    private PlayerMovement playerMovement;
    private bool challengeActive;
    private bool doorOpened;
    bool movementWasEnabled;
    public bool IsOpen => doorOpened;
    public bool ChallengeActive => challengeActive;
    public void RestoreOpen(bool open)
    {
        ReleaseMovement();doorOpened=open;
        if(door!=null){foreach(var r in door.GetComponentsInChildren<Renderer>())r.enabled=!open;foreach(var c in door.GetComponentsInChildren<Collider>())c.enabled=!open;}
    }
    private float pickPosition = 0.5f;
    private float sweetSpot;
    private int mistakes;
    private float messageUntil;
    public Vector3 InteractionPoint => lockAnchor!=null?lockAnchor.position:door!=null?door.position:transform.position;
    void Awake()
    {
        if(lockAnchor==null)lockAnchor=transform.Find("Cadeado do Galinheiro");
        CoopPadlockModel.Apply(lockAnchor);
    }
    public bool CanReachLock()
    {
        var game=HeistGameManager.Instance;var camera=Camera.main;
        if(game==null || game.player==null || camera==null || doorOpened)return false;
        Vector3 delta=InteractionPoint-camera.transform.position;
        if(delta.magnitude>interactionDistance || Vector3.Dot(camera.transform.forward,delta.normalized)<.5f)return false;
        var hits=Physics.RaycastAll(camera.transform.position,delta.normalized,delta.magnitude,~0,QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
        foreach(var hit in hits)
            if(!hit.transform.IsChildOf(game.player))return hit.transform.IsChildOf(transform);
        return true;
    }
    public bool TryBeginChallenge()
    {
        var game=HeistGameManager.Instance;
        if(GameMenu.BlocksInput || ProtagonistPhone.IsOpen || VillageMarket.IsOpen || game==null || game.missionEnded || Active!=null || !CanReachLock())return false;
        if(!game.IsMissionTarget(this))
        {game.ShowMessage(game.MissionActive?"Este cadeado pertence a outra fazenda.":"Inicie a missao desta fazenda nas fotos do celular.",4);return false;}
        player=game.player;playerMovement=player.GetComponent<PlayerMovement>();
        BeginChallenge();return true;
    }
    void ReleaseMovement()
    {
        if(challengeActive && playerMovement!=null)playerMovement.enabled=movementWasEnabled;
        if(Active==this){Active=null;ClosedFrame=Time.frameCount;}
        challengeActive=false;
    }
    void OnDisable(){ReleaseMovement();}

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
        if(challengeActive && HeistGameManager.Instance?.IsMissionTarget(this)!=true){EndChallenge();return;}
        if(GameMenu.BlocksInput)return;
        if (ProtagonistPhone.IsOpen || VillageMarket.IsOpen || HeistGameManager.Instance?.missionEnded==true) return;
        if (doorOpened || player == null)
            return;

        if (!challengeActive && Input.GetKeyDown(KeyCode.E))
        {
            TryBeginChallenge();
            return;
        }

        if (!challengeActive)
            return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            EndChallenge();
            return;
        }

        pickPosition += Input.GetAxisRaw("Horizontal") * EffectivePickSpeed * Time.deltaTime;
        pickPosition = Mathf.Clamp01(pickPosition);

        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.E))
            TryPick();
    }

    private void BeginChallenge()
    {
        if(challengeActive || Active!=null)return;
        Active=this;
        challengeActive = true;
        mistakes = 0;
        pickPosition = 0.5f;
        sweetSpot = Random.Range(0.15f, 0.85f);
        if (playerMovement != null)
        {
            movementWasEnabled=playerMovement.enabled;playerMovement.enabled=false;
            playerMovement.estaMovendo=false;playerMovement.estaSprinting=false;playerMovement.nivelRuido=0;
        }
        ShowMessage("Lockpick: mova com A/D e pressione ESPACO no ponto certo.", 3f);
    }

    private void TryPick()
    {
        if (Mathf.Abs(pickPosition - sweetSpot) <= EffectiveSweetSpotWidth)
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
        ReleaseMovement();
        if (door != null)
        {
            foreach(var renderer in door.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            foreach(var collider in door.GetComponentsInChildren<Collider>())collider.enabled=false;
        }
        ShowMessage("Cadeado aberto. Entre devagar no galinheiro.", 3f);
    }

    private void EndChallenge()
    {
        ReleaseMovement();
        ShowMessage("Lockpick interrompido.", 1.5f);
    }

    private IEnumerator ChickenJumpscare()
    {
        InteractableChicken chicken = scareChicken.GetComponent<InteractableChicken>();
        bool wasSleeping=chicken!=null && chicken.sleeping;
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
            chicken.sleeping = wasSleeping;
    }

    private void ShowMessage(string message, float duration)
    {
        messageUntil = Time.time + duration;
        if (HeistGameManager.Instance != null)
            HeistGameManager.Instance.ShowMessage(message, duration);
    }

    private void OnGUI()
    {
        if(GameMenu.IsOpen || DeveloperConsole.IsOpen || BackpackPanel.IsOpen || ProtagonistPhone.IsOpen || VillageMarket.IsOpen)return;
        if (!challengeActive)
        {
            if(Active==null && CanReachLock())
            {
                string text=HeistGameManager.Instance.IsMissionTarget(this)?"E  |  Abrir cadeado":"Missao desta fazenda necessaria no celular";
                var style=new GUIStyle(GUI.skin.box){fontSize=16,wordWrap=true};
                float w=Mathf.Min(420,Screen.width-32);
                GUI.Box(new Rect((Screen.width-w)*.5f,Screen.height*.8f,w,48),text,style);
            }
            return;
        }

        float width = Mathf.Min(560f, Screen.width * 0.72f);
        float left = (Screen.width - width) * 0.5f;
        float top = Screen.height * 0.72f;
        GUI.Box(new Rect(left, top - 52f, width, 112f), HouseholdEconomy.Instance?.Account.professionalEquipped==true?"LOCKPICK PROFISSIONAL":"LOCKPICK BASICO");
        GUI.Label(new Rect(left + 18f, top - 24f, width - 36f, 22f), "A/D move o pino    ESPACO tenta abrir    ESC sai");

        GUI.Box(new Rect(left + 18f, top + 7f, width - 36f, 18f), string.Empty);
        float minimum=Mathf.Clamp01(sweetSpot-EffectiveSweetSpotWidth),maximum=Mathf.Clamp01(sweetSpot+EffectiveSweetSpotWidth);
        float sweetLeft = left + 18f + (width - 36f) * minimum;
        float sweetWidth = (width - 36f) * (maximum-minimum);
        Color previous = GUI.color;
        GUI.color = new Color(0.35f, 0.8f, 0.38f, 1f);
        GUI.DrawTexture(new Rect(sweetLeft, top + 7f, sweetWidth, 18f), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.75f, 0.2f, 1f);
        GUI.DrawTexture(new Rect(left + 18f + (width - 36f) * pickPosition - 3f, top + 2f, 6f, 28f), Texture2D.whiteTexture);
        GUI.color = previous;
        GUI.Label(new Rect(left + 18f, top + 32f, width - 36f, 22f), "Erros: " + mistakes + "   Um erro assusta as galinhas e aumenta o alerta.");
    }
}

