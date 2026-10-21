using UnityEngine;

// Review helper (play-mode reviews add it; editor-assembly components cannot be put on scene objects): moves whatever
// it is put on (the player's controller, or the truck held kinematic) at a set velocity under gravity,
// frame by frame, so triggers along the way fire as in play; it removes itself when its time is up.
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
