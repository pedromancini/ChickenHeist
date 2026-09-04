using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Velocidades")]
    public float velocidadeNormal = 3f;
    public float velocidadeSprint = 6f;
    public float velocidadeAgachado = 1.5f;

    [Header("Pulo e Gravidade")]
    public float alturaDoSalto = 1.2f;
    public float gravidade = -9.81f;

    [Header("Agachar")]
    public float alturaEmPe = 2f;
    public float alturaAgachado = 1f;
    public float velocidadeAgachar = 8f;

    private CharacterController controller;
    private Vector3 velocidadeVertical;
    private float proximoPassoBarulhento = 0f;

    [HideInInspector] public bool estaSprinting = false;
    [HideInInspector] public bool estaAgachado = false;
    [HideInInspector] public bool estaMovendo = false;
    [HideInInspector] public float nivelRuido = 0f;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        HandleMovement();
        HandleCrouch();
        HandleGravity();
        CalcularRuido();
    }

    void HandleMovement()
    {
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        estaMovendo = (x != 0 || z != 0);
        estaSprinting = Input.GetKey(KeyCode.LeftShift) && !estaAgachado && estaMovendo;
        float velocidade = estaAgachado ? velocidadeAgachado :
                           estaSprinting ? velocidadeSprint : velocidadeNormal;
        Vector3 movimento = transform.right * x + transform.forward * z;
        controller.Move(movimento * velocidade * Time.deltaTime);
    }

    void HandleCrouch()
    {
        if (Input.GetKeyDown(KeyCode.C))
            estaAgachado = !estaAgachado;
        float alturaAlvo = estaAgachado ? alturaAgachado : alturaEmPe;
        controller.height = Mathf.Lerp(controller.height, alturaAlvo, velocidadeAgachar * Time.deltaTime);
    }

    void HandleGravity()
    {
        if (controller.isGrounded && velocidadeVertical.y < 0)
            velocidadeVertical.y = -2f;
        if (Input.GetButtonDown("Jump") && controller.isGrounded && !estaAgachado)
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
