using System.Collections.Generic;
using UnityEngine;

// Purpose-built glove geometry with authored finger curls. Original arm bones remain authoritative.
[DefaultExecutionOrder(400)]
public class CharacterGripHands : MonoBehaviour
{
    public enum Pose { None, Phone, Wheel }
    readonly Transform[] hands=new Transform[2];
    readonly SkinnedMeshRenderer[] gloves=new SkinnedMeshRenderer[2];
    readonly Transform[] cuffs=new Transform[2];
    readonly Pose[] poses=new Pose[2];
    SkinnedMeshRenderer body;Mesh original,masked;Material material;
    public static CharacterGripHands Attach(Transform character)
    {
        var value=character.GetComponent<CharacterGripHands>();return value!=null?value:character.gameObject.AddComponent<CharacterGripHands>();
    }
    public void SetPose(Transform hand,Pose pose)
    {
        for(int i=0;i<2;i++)if(hands[i]==hand)poses[i]=pose;
    }
    void Awake()
    {
        // The new protagonist uses its own weighted finger geometry.
        if(GetComponent<ProtagonistArticulation>()!=null){enabled=false;return;}
        foreach(var bone in GetComponentsInChildren<Transform>()){if(bone.name=="HandL")hands[0]=bone;if(bone.name=="HandR")hands[1]=bone;}
        foreach(var skin in GetComponentsInChildren<SkinnedMeshRenderer>())if(System.Array.IndexOf(skin.bones,hands[0])>=0){body=skin;break;}
        var mesh=Resources.Load<Mesh>("GripHands");if(mesh==null || body==null)return;
        original=body.sharedMesh;masked=Instantiate(original);masked.name="Character arms with grip hand openings";
        material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=new Color(.48f,.34f,.21f);material.SetFloat("_Smoothness",.15f);material.SetFloat("_Cull",0);
        for(int i=0;i<2;i++)
        {
            var obj=new GameObject(i==0?"Luva esquerda articulada":"Luva direita articulada");obj.transform.SetParent(hands[i],false);
            obj.transform.localScale=new Vector3((i==1?-1:1)/hands[i].lossyScale.x,1/hands[i].lossyScale.y,1/hands[i].lossyScale.z);
            gloves[i]=obj.AddComponent<SkinnedMeshRenderer>();gloves[i].sharedMesh=mesh;gloves[i].sharedMaterial=material;gloves[i].updateWhenOffscreen=true;gloves[i].localBounds=new Bounds(new Vector3(0,.06f,0),Vector3.one*.4f);obj.SetActive(false);
            var cuff=new GameObject("Antebraco e punho da luva");
            cuff.transform.SetParent(transform,false);cuff.AddComponent<MeshFilter>().sharedMesh=ForearmMesh();cuff.AddComponent<MeshRenderer>().sharedMaterial=material;cuffs[i]=cuff.transform;cuff.SetActive(false);
        }
    }
    int previousMask=-1;
    void LateUpdate()
    {
        if(body==null || gloves[0]==null)return;
        bool phone=GetComponent<HandheldPhone>()?.IsBusy==true;
        for(int i=0;i<2;i++)
        {
            bool active=poses[i]==Pose.Phone?phone:poses[i]==Pose.Wheel && OldPickupTruck.IsDriving;
            gloves[i].gameObject.SetActive(active);
            cuffs[i].gameObject.SetActive(active);
            if(active)
            {
                var direction=(hands[i].position-hands[i].parent.position).normalized;
                cuffs[i].position=(hands[i].position+hands[i].parent.position)*.5f;
                cuffs[i].rotation=Quaternion.FromToRotation(Vector3.up,direction);
                cuffs[i].localScale=new Vector3(1,Vector3.Distance(hands[i].position,hands[i].parent.position)+.045f,1);
            }
            var mesh=gloves[i].sharedMesh;
            for(int shape=0;shape<mesh.blendShapeCount;shape++)gloves[i].SetBlendShapeWeight(shape,Mathf.MoveTowards(gloves[i].GetBlendShapeWeight(shape),active && mesh.GetBlendShapeName(shape)==poses[i].ToString()?100:0,Time.unscaledDeltaTime*450));
            if(!active)poses[i]=Pose.None;
        }
        int mask=(gloves[0].gameObject.activeSelf?1:0)|(gloves[1].gameObject.activeSelf?2:0);
        if(mask==previousMask)return;previousMask=mask;
        if(mask==0){body.sharedMesh=original;return;}
        int left=System.Array.IndexOf(body.bones,hands[0]),right=System.Array.IndexOf(body.bones,hands[1]);
        int leftArm=System.Array.IndexOf(body.bones,hands[0].parent),rightArm=System.Array.IndexOf(body.bones,hands[1].parent);var weights=original.boneWeights;
        bool Removed(int v)
        {
            var w=weights[v];float sum=0;
            void Add(int bone,float value){if((mask&1)!=0 && (bone==left || bone==leftArm) || (mask&2)!=0 && (bone==right || bone==rightArm))sum+=value;}
            Add(w.boneIndex0,w.weight0);Add(w.boneIndex1,w.weight1);Add(w.boneIndex2,w.weight2);Add(w.boneIndex3,w.weight3);return sum>.05f;
        }
        for(int sub=0;sub<original.subMeshCount;sub++)
        {
            var keep=new List<int>();var triangles=original.GetTriangles(sub);
            for(int t=0;t<triangles.Length;t+=3)if(!Removed(triangles[t]) && !Removed(triangles[t+1]) && !Removed(triangles[t+2])){keep.Add(triangles[t]);keep.Add(triangles[t+1]);keep.Add(triangles[t+2]);}
            masked.SetTriangles(keep,sub);
        }
        body.sharedMesh=masked;
    }
    static Mesh ForearmMesh()
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();
        for(int ring=0;ring<4;ring++)for(int j=0;j<12;j++)
        {
            float t=ring/3f,a=j*Mathf.PI/6,r=Mathf.Lerp(.044f,.026f,t);
            vertices.Add(new Vector3(Mathf.Cos(a)*r,t-.5f,Mathf.Sin(a)*r*.85f));
        }
        for(int ring=0;ring<3;ring++)for(int j=0;j<12;j++)
        {int a=ring*12+j,b=ring*12+(j+1)%12;triangles.AddRange(new[]{a,b,a+12,b,b+12,a+12});}
        var mesh=new Mesh{name="Tapered forearm glove"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    void OnDestroy(){if(body!=null && original!=null)body.sharedMesh=original;if(masked!=null)Destroy(masked);if(material!=null)Destroy(material);foreach(var cuff in cuffs)if(cuff!=null)Destroy(cuff.GetComponent<MeshFilter>().sharedMesh);}
}
