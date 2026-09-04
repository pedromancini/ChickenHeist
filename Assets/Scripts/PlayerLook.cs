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

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * sensibilidade * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * sensibilidade * Time.deltaTime;

        rotacaoX -= mouseY;
        rotacaoX = Mathf.Clamp(rotacaoX, limiteVerticalCima, limiteVerticalBaixo);
        transform.localRotation = Quaternion.Euler(rotacaoX, 0f, 0f);

        corpoDojogador.Rotate(Vector3.up * mouseX);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}