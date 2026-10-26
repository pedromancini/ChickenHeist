using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class NpcGaitProbe
{
    const string Models="Assets/ImportedMedievalPeople/fbx/people_unity/",Clips="Assets/ChickenHeistGenerated/Characters/Villagers/";
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var log=new List<string>();
        foreach(var found in Directory.GetFiles(Clips,"*-Walk.anim"))
        {
            var path=found.Replace(Path.DirectorySeparatorChar,'/');
            string model=Path.GetFileName(path).Replace("-Walk.anim","");var fbx=Models+model+".fbx";if(!File.Exists(fbx))continue;
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips+model+"-Idle.anim");
            var root=new GameObject("bake");var visual=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(fbx),root.transform);visual.name=model;
            try
            {
                var animator=visual.GetComponent<Animator>();if(animator==null || animator.avatar==null || !animator.avatar.isHuman){log.Add(model+": no humanoid avatar");continue;}
                Transform B(HumanBodyBones b)=>animator.GetBoneTransform(b);
                var feet=new[]{B(HumanBodyBones.LeftFoot),B(HumanBodyBones.RightFoot)};var toes=new[]{B(HumanBodyBones.LeftToes),B(HumanBodyBones.RightToes)};
                if(idle!=null)idle.SampleAnimation(root,0);
                Vector3 Tip(int s)=>toes[s]!=null?toes[s].position:feet[s].position+feet[s].forward*.12f;
                var restForward=new Vector3[2];var restSide=new Vector3[2];
                for(int s=0;s<2;s++){var f=Tip(s)-feet[s].position;f.y=0;restForward[s]=feet[s].InverseTransformDirection(f.normalized);restSide[s]=feet[s].InverseTransformDirection(Vector3.Cross(Vector3.up,f.normalized));}
                float floor=Mathf.Min(Tip(0).y,Tip(1).y);
                const int n=24;var low=new float[2,n];var roll=new float[2,n];var yaw=new float[2,n];
                for(int i=0;i<n;i++)
                {
                    clip.SampleAnimation(root,clip.length*i/n);
                    for(int s=0;s<2;s++)
                    {
                        var fwd=feet[s].TransformDirection(restForward[s]);var side=feet[s].TransformDirection(restSide[s]);
                        low[s,i]=Mathf.Min(Tip(s).y,feet[s].position.y)-floor;
                        roll[s,i]=Mathf.Asin(Mathf.Clamp(Vector3.ProjectOnPlane(side,fwd).normalized.y,-1,1))*Mathf.Rad2Deg;
                        var flat=fwd;flat.y=0;yaw[s,i]=Vector3.SignedAngle(root.transform.forward,flat,Vector3.up);
                    }
                }
                var line=model+":";
                for(int s=0;s<2;s++)
                {
                    var down=Enumerable.Range(0,n).Where(i=>low[s,i]<low[1-s,i]).ToList();
                    float worstRoll=down.Count>0?down.Max(i=>Mathf.Abs(roll[s,i])):0;
                    float meanYaw=down.Count>0?down.Average(i=>yaw[s,i]):0;
                    line+=$"  {(s==0?"left":"right")} down {down.Count}/{n}, roll up to {worstRoll:0} deg, pointing {meanYaw:0} deg";
                }
                log.Add(line);
            }
            finally{Object.DestroyImmediate(root);}
        }
        Directory.CreateDirectory("output");File.WriteAllLines("output/npc-gait.txt",log);Debug.Log("NPC GAIT\n"+string.Join("\n",log));
    }
}
