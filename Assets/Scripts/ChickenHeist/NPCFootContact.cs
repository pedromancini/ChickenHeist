using UnityEngine;

[DefaultExecutionOrder(200)]
public class NPCFootContact : MonoBehaviour
{
    public Transform leftFoot,rightFoot;
    public Vector3 leftSole,rightSole;
    float height;
    public float LowestSole=>Mathf.Min(leftFoot.TransformPoint(leftSole).y,rightFoot.TransformPoint(rightSole).y);
    void Start(){height=transform.localPosition.y;}
    void LateUpdate()
    {
        if(leftFoot==null || rightFoot==null || transform.parent==null)return;
        float target=transform.parent.TransformPoint(Vector3.up*height).y;
        if(transform.parent.GetComponent<CharacterController>()!=null)target-=.04f;
        transform.position+=Vector3.up*Mathf.Clamp(target-LowestSole,-.6f,.6f);
    }
}
