using UnityEngine;

// Calibrate the contact surface from the actual skinned hand in its bind pose.
public sealed class HandGripPose
{
    readonly Vector3 contact;
    readonly Quaternion frame;
    readonly Transform hand;
    public HandGripPose(Transform hand, Transform character, bool authored=false, bool wheel=false)
    {
        this.hand=hand;
        bool articulated=character.GetComponent<ProtagonistArticulation>()!=null;
        if(articulated)authored=false;
        if(authored)
        {
            contact=new Vector3(0,(wheel?.075f:.065f)/hand.lossyScale.y,.018f/hand.lossyScale.z);
            frame=Quaternion.identity;return;
        }
        foreach(var skin in character.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            int bone=System.Array.IndexOf(skin.bones,hand);
            if(bone<0)continue;
            var mesh=skin.sharedMesh;var bind=mesh.bindposes[bone];
            var vertices=mesh.vertices;var weights=mesh.boneWeights;
            Vector3 center=Vector3.zero;float sum=0;
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];
                float weight=(w.boneIndex0==bone?w.weight0:0)+(w.boneIndex1==bone?w.weight1:0)+
                    (w.boneIndex2==bone?w.weight2:0)+(w.boneIndex3==bone?w.weight3:0);
                if(weight<.5f)continue;
                center+=bind.MultiplyPoint3x4(vertices[i])*weight;sum+=weight;
            }
            if(sum<=0)continue;
            center/=sum;
            var palm=bind.MultiplyVector(skin.transform.InverseTransformDirection(articulated?-character.up:-character.forward)).normalized;
            var fingers=Vector3.ProjectOnPlane(center,palm).normalized;
            float surface=Vector3.Dot(center,palm);
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];
                if((w.boneIndex0==bone && w.weight0>.5f) || (w.boneIndex1==bone && w.weight1>.5f) ||
                    (w.boneIndex2==bone && w.weight2>.5f) || (w.boneIndex3==bone && w.weight3>.5f))
                    surface=Mathf.Max(surface,Vector3.Dot(bind.MultiplyPoint3x4(vertices[i]),palm));
            }
            contact=center+palm*(surface-Vector3.Dot(center,palm));
            frame=Quaternion.LookRotation(palm,fingers);
            return;
        }
        frame=Quaternion.identity;contact=Vector3.up*(.06f/Mathf.Max(.001f,hand.lossyScale.y));
    }
    public Quaternion Rotation(Vector3 palmNormal,Vector3 fingers)=>Quaternion.LookRotation(palmNormal,fingers)*Quaternion.Inverse(frame);
    public Vector3 Wrist(Vector3 surface,Quaternion rotation)=>surface-rotation*Vector3.Scale(contact,hand.lossyScale);
    public Vector3 Contact=>hand.TransformPoint(contact);
}
