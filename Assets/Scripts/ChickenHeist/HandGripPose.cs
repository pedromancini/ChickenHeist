using UnityEngine;

public sealed class HandGripPose
{
    readonly Vector3 contact;
    readonly Quaternion frame;
    readonly Transform hand;
    public Vector3 PalmLocal {get;private set;}=Vector3.forward;
    public Vector3 FingersLocal {get;private set;}=Vector3.up;
    public float SurfaceLevel {get;private set;}
    public Transform Hand=>hand;
    public HandGripPose(Transform hand, Transform character, bool authored=false, bool wheel=false, bool? palmDown=null, bool fingersFromKnuckles=false)
    {
        this.hand=hand;
        bool articulated=palmDown ?? character.GetComponent<ProtagonistArticulation>()!=null;
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
            if(fingersFromKnuckles)
            {
                float xx=0,xy=0,xz=0,yy=0,yz=0,zz=0;int n=0;
                for(int i=0;i<vertices.Length;i++)
                {
                    var w=weights[i];if(!(w.boneIndex0==bone && w.weight0>.5f))continue;
                    var d=bind.MultiplyPoint3x4(vertices[i])-center;xx+=d.x*d.x;xy+=d.x*d.y;xz+=d.x*d.z;yy+=d.y*d.y;yz+=d.y*d.z;zz+=d.z*d.z;n++;
                }
                if(n>20)
                {
                    float tr=xx+yy+zz;var v=palm;
                    for(int it=0;it<60;it++)
                    {
                        var c=new Vector3(xx*v.x+xy*v.y+xz*v.z,xy*v.x+yy*v.y+yz*v.z,xz*v.x+yz*v.y+zz*v.z);
                        v=(v*tr-c).normalized;
                    }
                    if(Vector3.Dot(v,palm)<0)v=-v;palm=v;
                }
            }
            var fingers=Vector3.ProjectOnPlane(center,palm).normalized;
            if(fingersFromKnuckles)
            {
                string side=hand.name.Length>0?hand.name.Substring(hand.name.Length-1):"";Vector3 sumK=Vector3.zero;int n=0;
                foreach(var f in new[]{"Index1","Middle1","Ring1","Little1"}){var b=hand.Find(f+side);if(b!=null){sumK+=b.localPosition;n++;}}
                if(n>0)fingers=Vector3.ProjectOnPlane(sumK/n,palm).normalized;
            }
            var lateral=Vector3.Cross(palm,fingers).normalized;
            Vector3 knuckles=Vector3.zero;int found=0;float halfWidth=0;
            string suffix=hand.name.Length>0?hand.name.Substring(hand.name.Length-1):"";
            foreach(var f in new[]{"Index1","Middle1","Ring1","Little1"}){var b=hand.Find(f+suffix);if(b!=null){knuckles+=b.localPosition;found++;}}
            if(found>0)
            {
                knuckles/=found;
                foreach(var f in new[]{"Index1","Little1"}){var b=hand.Find(f+suffix);if(b!=null)halfWidth=Mathf.Max(halfWidth,Mathf.Abs(Vector3.Dot(b.localPosition-knuckles,lateral)));}
            }
            float reach=found>0?Vector3.Dot(knuckles,fingers):Vector3.Dot(center,fingers)*1.6f;
            float midLateral=found>0?Vector3.Dot(knuckles,lateral):Vector3.Dot(center,lateral);
            if(halfWidth<=0)halfWidth=Mathf.Abs(reach)*.35f;
            float surface=float.NegativeInfinity;Vector3 strip=Vector3.zero;int count=0;
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];
                if(!((w.boneIndex0==bone && w.weight0>.5f) || (w.boneIndex1==bone && w.weight1>.5f) ||
                    (w.boneIndex2==bone && w.weight2>.5f) || (w.boneIndex3==bone && w.weight3>.5f)))continue;
                var p=bind.MultiplyPoint3x4(vertices[i]);float along=Vector3.Dot(p,fingers);
                if(along<reach*.45f || along>reach*1.02f || Mathf.Abs(Vector3.Dot(p,lateral)-midLateral)>halfWidth*.6f)continue;
                surface=Mathf.Max(surface,Vector3.Dot(p,palm));strip+=p;count++;
            }
            if(count==0)
            {
                surface=Vector3.Dot(center,palm);
                for(int i=0;i<vertices.Length;i++)
                {
                    var w=weights[i];
                    if((w.boneIndex0==bone && w.weight0>.5f) || (w.boneIndex1==bone && w.weight1>.5f) ||
                        (w.boneIndex2==bone && w.weight2>.5f) || (w.boneIndex3==bone && w.weight3>.5f))
                        surface=Mathf.Max(surface,Vector3.Dot(bind.MultiplyPoint3x4(vertices[i]),palm));
                }
                strip=center;
            }
            else strip/=count;
            contact=strip+palm*(surface-Vector3.Dot(strip,palm));
            frame=Quaternion.LookRotation(palm,fingers);
            PalmLocal=palm;FingersLocal=fingers;SurfaceLevel=surface;
            return;
        }
        frame=Quaternion.identity;contact=Vector3.up*(.06f/Mathf.Max(.001f,hand.lossyScale.y));
    }
    public Quaternion Rotation(Vector3 palmNormal,Vector3 fingers)=>Quaternion.LookRotation(palmNormal,fingers)*Quaternion.Inverse(frame);
    public Vector3 Wrist(Vector3 surface,Quaternion rotation)=>surface-rotation*Vector3.Scale(contact,hand.lossyScale);
    public Vector3 Contact=>hand.TransformPoint(contact);
    public Vector3 ContactLocal=>contact;
}
