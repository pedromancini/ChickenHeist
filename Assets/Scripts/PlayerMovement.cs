using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Velocidades")]
    public float velocidadeNormal = 3f;
    public float velocidadeSprint = 6f;
    public float velocidadeAgachado = 1.5f;
    public float DeveloperSpeedMultiplier { get; private set; } = 1f;
    public float CurrentMoveSpeed => (estaAgachado?velocidadeAgachado:estaSprinting?velocidadeSprint:velocidadeNormal)*DeveloperSpeedMultiplier;
    public bool SetDeveloperSpeed(int multiplier)
    {
        if(multiplier<1 || multiplier>20)return false;
        DeveloperSpeedMultiplier=multiplier;return true;
    }

    [Header("Pulo e Gravidade")]
    public float alturaDoSalto = 1.2f;
    public float gravidade = -9.81f;

    [Header("Agachar")]
    public float alturaEmPe = 2f;
    public float alturaAgachado = 1f;
    public float velocidadeAgachar = 8f;

    private CharacterController controller;
    private Transform eyes;
    private Vector3 velocidadeVertical;
    private float proximoPassoBarulhento = 0f;

    [HideInInspector] public bool estaSprinting = false;
    [HideInInspector] public bool estaAgachado = false;
    [HideInInspector] public bool estaMovendo = false;
    [HideInInspector] public float nivelRuido = 0f;
    public bool IsAirborne => controller != null && !controller.isGrounded;
    public float VerticalSpeed => velocidadeVertical.y;
    public Vector2 MoveInput { get; private set; }
    public void RestorePosture(bool crouched)
    {
        if(controller==null)controller=GetComponent<CharacterController>();
        if(eyes==null)eyes=GetComponentInChildren<Camera>().transform;
        estaAgachado=crouched;estaMovendo=false;estaSprinting=false;MoveInput=Vector2.zero;
        velocidadeVertical=Vector3.zero;nivelRuido=0;
        controller.height=crouched?alturaAgachado:alturaEmPe;
        controller.center=Vector3.up*controller.height*.5f;
        eyes.localPosition=new Vector3(0,controller.height-.35f,0);
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();
        var camera=GetComponentInChildren<Camera>();eyes=camera!=null?camera.transform:null;
    }

    void Update()
    {
        if(GameMenu.BlocksInput){estaMovendo=false;estaSprinting=false;MoveInput=Vector2.zero;return;}
        if (ProtagonistPhone.IsOpen || VillageMarket.IsOpen || HeistGameManager.Instance?.missionEnded==true) { estaMovendo=false; estaSprinting=false; nivelRuido=0; HandleGravity(); return; }
        HandleMovement();
        HandleCrouch();
        HandleGravity();
        CalcularRuido();
    }

    void HandleMovement()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        MoveInput = Vector2.ClampMagnitude(new Vector2(x,z),1);
        estaMovendo = (x != 0 || z != 0);
        estaSprinting = Input.GetKey(KeyCode.LeftShift) && !estaAgachado && estaMovendo;
        Vector3 movimento = transform.right * x + transform.forward * z;
        movimento=Vector3.ClampMagnitude(movimento,1);
        controller.Move(movimento * CurrentMoveSpeed * Time.deltaTime);
    }

    void HandleCrouch()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            bool blocked=false;
            if(estaAgachado)foreach(var hit in Physics.OverlapCapsule(transform.position+Vector3.up*.36f,transform.position+Vector3.up*(alturaEmPe-.36f),.34f,~0,QueryTriggerInteraction.Ignore))
                if(hit!=controller && hit.bounds.max.y>transform.position.y+.15f)blocked=true;
            if(!blocked)estaAgachado = !estaAgachado;
        }
        float alturaAlvo = estaAgachado ? alturaAgachado : alturaEmPe;
        controller.height = Mathf.Lerp(controller.height, alturaAlvo, velocidadeAgachar * Time.deltaTime);
        controller.center=Vector3.up*(controller.height*.5f);
        if(eyes!=null)eyes.localPosition=new Vector3(0,controller.height-.35f,0);
    }

    void HandleGravity()
    {
        if (controller.isGrounded && velocidadeVertical.y < 0)
            velocidadeVertical.y = -2f;
        if (!ProtagonistPhone.IsOpen && !VillageMarket.IsOpen && HeistGameManager.Instance?.missionEnded!=true && Input.GetButtonDown("Jump") && controller.isGrounded && !estaAgachado)
            velocidadeVertical.y = Mathf.Sqrt(alturaDoSalto * -2f * gravidade);
        velocidadeVertical.y += gravidade * Time.deltaTime;
        controller.Move(velocidadeVertical * Time.deltaTime);
    }

    void CalcularRuido()
    {
        if (!estaMovendo) nivelRuido = 0f;
        else if (estaAgachado) nivelRuido = 0.2f;
        else if (estaSprinting) nivelRuido = 1.0f;
        else nivelRuido = 0.5f;

        if (controller.isGrounded && estaMovendo && Time.time >= proximoPassoBarulhento)
        {
            float intervalo = estaSprinting ? 0.28f : estaAgachado ? 0.85f : 0.5f;
            proximoPassoBarulhento = Time.time + intervalo;
            NoiseEmitter.EmitGlobal(NoiseSource.Footstep, transform.position, nivelRuido);
        }
    }
}
