using UnityEngine;

public class HomePhoneDock : MonoBehaviour
{
    void Update()
    {
        if(GameMenu.BlocksInput)return;
        if(ProtagonistPhone.IsOpen || ProtagonistPhone.Instance==null || Camera.main==null)return;
        Vector3 delta=transform.position-Camera.main.transform.position;
        if(delta.magnitude<2 && Vector3.Dot(delta.normalized,Camera.main.transform.forward)>.75f && Input.GetKeyDown(KeyCode.E))
            ProtagonistPhone.Instance.SetOpen(true);
    }
}
