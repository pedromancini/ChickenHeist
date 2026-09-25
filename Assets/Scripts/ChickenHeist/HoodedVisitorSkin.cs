using System;
using System.Linq;
using UnityEngine;

// Bind the supplied A-pose mesh to the cinematic skeleton. Source joints are
// expressed in the normalized model's metres; target rotations come from the
// actual idle pose so the authored arms do not have to be cut away.
public static class HoodedVisitorSkin
{
    public static Mesh Bind(Transform actor,MeshFilter filter)
    {
        string[] names={"Pelvis","Spine_02","Head","Upperarm_L","Lowerarm_L","Hand_L","Upperarm_R","Lowerarm_R","Hand_R","Thigh_L","Calf_L","Foot_L","Thigh_R","Calf_R","Foot_R"};
        var all=actor.GetComponentsInChildren<Transform>(true);
        var bones=names.Select(n=>all.First(t=>t.name==n)).ToArray();
        Vector3[] joints={new Vector3(0,.83f,0),new Vector3(0,1.16f,0),new Vector3(0,1.48f,0),
            new Vector3(-.22f,1.33f,0),new Vector3(-.43f,1.20f,0),new Vector3(-.63f,1.08f,0),
            new Vector3(.22f,1.33f,0),new Vector3(.43f,1.20f,0),new Vector3(.63f,1.08f,0),
            new Vector3(-.11f,.81f,0),new Vector3(-.15f,.46f,0),new Vector3(-.18f,.12f,0),
            new Vector3(.11f,.81f,0),new Vector3(.15f,.46f,0),new Vector3(.18f,.12f,0)};
        int[] next={1,2,-1,4,5,-1,7,8,-1,10,11,-1,13,14,-1};
        var mesh=UnityEngine.Object.Instantiate(filter.sharedMesh);
        mesh.name="Visitante fornecido - corpo articulado";
        var toActor=actor.worldToLocalMatrix*filter.transform.localToWorldMatrix;
        var vertices=mesh.vertices.Select(toActor.MultiplyPoint3x4).ToArray();
        var normals=mesh.normals.Select(n=>toActor.MultiplyVector(n).normalized).ToArray();
        var bind=new Matrix4x4[bones.Length];
        for(int i=0;i<bones.Length;i++)
        {
            var rotation=Quaternion.Inverse(actor.rotation)*bones[i].rotation;
            if(next[i]>=0)
            {
                var targetDirection=actor.InverseTransformVector(bones[next[i]].position-bones[i].position);
                rotation=Quaternion.FromToRotation(targetDirection,joints[next[i]]-joints[i])*rotation;
            }
            else if(i==5 || i==8)
            {
                // Match the palm frame used by VisitorCinematicActor. Using
                // the forearm direction here leaves the imported palms
                // twisted relative to the hand IK contact plane.
                var hand=bones[i];
                var index=hand.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name.StartsWith("Index1") || t.name.StartsWith("Index_01"));
                var along=index!=null?(index.position-hand.position).normalized:-actor.up;
                var localFrame=Quaternion.LookRotation(hand.InverseTransformDirection(-actor.forward),hand.InverseTransformDirection(along));
                var sourceAlong=(joints[i]-joints[i-1]).normalized;
                rotation=Quaternion.LookRotation(-Vector3.forward,sourceAlong)*Quaternion.Inverse(localFrame);
            }
            bind[i]=Matrix4x4.TRS(joints[i],rotation,bones[i].lossyScale).inverse;
        }
        var weights=new BoneWeight[vertices.Length];
        for(int i=0;i<vertices.Length;i++)
        {
            var p=vertices[i];float x=Mathf.Abs(p.x);
            int a,b;float mix;
            if(p.y>1.42f){a=1;b=2;mix=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.40f,1.50f,p.y));}
            else if(x>.21f && p.y>.95f)
            {
                int upper=p.x<0?3:6;
                if(x<.29f){a=1;b=upper;mix=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.21f,.29f,x));}
                else if(x<.53f){a=upper;b=upper+1;mix=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.37f,.48f,x));}
                else{a=upper+1;b=upper+2;mix=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.58f,.66f,x));}
            }
            else if(p.y<.84f)
            {
                int thigh=p.x<0?9:12;
                if(p.y>.70f){a=thigh;b=0;mix=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.70f,.84f,p.y));}
                else if(p.y>.25f){a=thigh+1;b=thigh;mix=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.38f,.54f,p.y));}
                else{a=thigh+2;b=thigh+1;mix=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.24f,p.y));}
            }
            else{a=0;b=1;mix=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.84f,1.20f,p.y));}
            weights[i]=new BoneWeight{boneIndex0=a,weight0=1-mix,boneIndex1=b,weight1=mix};
        }
        mesh.vertices=vertices;mesh.normals=normals;mesh.boneWeights=weights;mesh.bindposes=bind;mesh.RecalculateBounds();
        var original=filter.GetComponent<MeshRenderer>();
        var node=new GameObject("Pele articulada do visitante");node.transform.SetParent(actor,false);
        var skin=node.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.sharedMaterials=original.sharedMaterials;skin.bones=bones;skin.rootBone=bones[0];skin.updateWhenOffscreen=true;skin.forceMatrixRecalculationPerRender=true;skin.localBounds=new Bounds(Vector3.up,Vector3.one*4);
        original.enabled=false;
        return mesh;
    }
}
