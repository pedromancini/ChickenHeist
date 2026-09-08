using UnityEngine;

public class PlayerLook : MonoBehaviour
{
    [Header("Sensibilidade")]
    public float sensibilidade = 100f;

    [Header("Limite Vertical")]
    public float limiteVerticalCima = -80f;
    public float limiteVerticalBaixo = 80f;

    public Transform corpoDojogador;

    private float rotacaoX = 0f;
    float driverYaw;
    public bool invertY;
    public float Pitch=>rotacaoX;
    public void RestorePitch(float pitch){driverYaw=0;rotacaoX=pitch>180?pitch-360:pitch;transform.localRotation=Quaternion.Euler(rotacaoX,0,0);}

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if(GameMenu.BlocksInput || ChickenCoopLockpick.Active!=null || ChickenCoopLockpick.ClosedFrame==Time.frameCount)return;
        if (ProtagonistPhone.IsOpen || VillageMarket.IsOpen || HeistGameManager.Instance?.missionEnded==true || ProtagonistPhone.LastClosedFrame==Time.frameCount || VillageMarket.ClosedFrame==Time.frameCount) return;
        if(Cursor.lockState!=CursorLockMode.Locked)
        {
            if(Input.GetMouseButtonDown(0)){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
            return;
        }
        float mouseX = Input.GetAxis("Mouse X") * sensibilidade * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * sensibilidade * Time.deltaTime;

        rotacaoX -= mouseY*(invertY?-1:1);
        rotacaoX = Mathf.Clamp(rotacaoX, limiteVerticalCima, limiteVerticalBaixo);
        if(OldPickupTruck.IsDriving)
        {
            driverYaw=Mathf.Clamp(driverYaw+mouseX,-100,100);
            transform.localRotation=Quaternion.Euler(rotacaoX,driverYaw,0);
        }
        else
        {
            driverYaw=0;transform.localRotation = Quaternion.Euler(rotacaoX, 0f, 0f);
            corpoDojogador.Rotate(Vector3.up * mouseX);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
