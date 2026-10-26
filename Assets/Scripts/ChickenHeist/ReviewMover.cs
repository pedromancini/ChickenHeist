using UnityEngine;

public sealed class ReviewMover : MonoBehaviour
{
    public Vector3 velocity;public float gravity;public float until;CharacterController controller;
    void Start(){controller=GetComponent<CharacterController>();}
    void Update()
    {
        if(Time.time>until){Destroy(this);return;}
        velocity.y-=gravity*Time.deltaTime;var step=velocity*Time.deltaTime;
        if(controller!=null && controller.enabled){controller.Move(step);if(controller.isGrounded && velocity.y<0)velocity.y=-1;}
        else{var body=GetComponent<Rigidbody>();if(body!=null && body.isKinematic)body.MovePosition(body.position+step);else transform.position+=step;}
    }
}
