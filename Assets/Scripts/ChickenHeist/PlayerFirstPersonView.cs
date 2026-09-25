using UnityEngine;

// Keep the view in front of the shoulder line, inside the controller's radius.
[DefaultExecutionOrder(450)]
public sealed class PlayerFirstPersonView : MonoBehaviour
{
    Transform eyes;
    void LateUpdate()
    {
        if(StoryDirector.Active || ChickenCoopLockpick.Active!=null || ChickenCoopLockpick.ClosedFrame==Time.frameCount)return;
        if(eyes==null)eyes=GetComponentInParent<PlayerMovement>()?.GetComponentInChildren<Camera>()?.transform;
        if(eyes==null)return;
        var position=eyes.localPosition;position.z=OldPickupTruck.IsDriving?0:.22f;eyes.localPosition=position;
    }
}
