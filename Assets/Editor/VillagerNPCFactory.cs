using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class VillagerNPCFactory
{
    const string Folder="Assets/ChickenHeistGenerated/Characters/Villagers";
    public static GameObject Create(Transform parent,string name,string model)
    {
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/ChickenHeistGenerated/Characters","Villagers");
        bool medieval=model.StartsWith("peasant") || model.StartsWith("city");
        string path=medieval?"Assets/ImportedMedievalPeople/fbx/people_unity/"+model+".fbx":"Assets/ImportedVillagerNPC/Villager NPC Free/FBX/Characters/"+model+".fbx";
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if(source==null)throw new System.InvalidOperationException("Missing NPC model: "+path);
        var root=new GameObject(name);root.transform.SetParent(parent,false);
        var visual=Object.Instantiate(source,root.transform);visual.name=model;
        foreach(var animator in visual.GetComponentsInChildren<Animator>())Object.DestroyImmediate(animator);
        foreach(var animation in visual.GetComponentsInChildren<Animation>())Object.DestroyImmediate(animation);
        Bounds bounds=ProceduralFarmGenerator.VisualBounds(visual);
        visual.transform.localScale*=1.78f/Mathf.Max(.01f,bounds.size.y);
        bounds=ProceduralFarmGenerator.VisualBounds(visual);
        visual.transform.position+=root.transform.position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
        var materialPath=Folder+(medieval?"/MedievalAtlas.mat":"/VillagerAtlas.mat");
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null)
        {
            material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=medieval?"Medieval atlas URP":"Villager atlas URP"};
            material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(medieval?"Assets/ImportedMedievalPeople/texture/people_texture_map.png":"Assets/ImportedVillagerNPC/Villager NPC Free/Texture/Villagers_Texture.png"));
            material.SetFloat("_Smoothness",.07f);AssetDatabase.CreateAsset(material,materialPath);
        }
        foreach(var renderer in visual.GetComponentsInChildren<Renderer>())renderer.sharedMaterials=Enumerable.Repeat(material,renderer.sharedMaterials.Length).ToArray();
        var transforms=visual.GetComponentsInChildren<Transform>();
        string[] boneNames=medieval?new[]{"Spine_02","Head","Upperarm_L","Lowerarm_L","Upperarm_R","Lowerarm_R","Thigh_L","Calf_L","Thigh_R","Calf_R"}:
            new[]{"spine.002","spine.006","upper_arm.L","forearm.L","upper_arm.R","forearm.R","thigh.L","shin.L","thigh.R","shin.R"};
        var bones=boneNames.Select(n=>transforms.First(t=>t.name==n)).ToArray();
        for(int i=2;i<=4;i+=2)
        {
            Vector3 along=bones[i+1].position-bones[i].position;
            Vector3 relaxed=-root.transform.up+root.transform.right*(Vector3.Dot(along,root.transform.right)>0?.10f:-.10f);
            bones[i].rotation=Quaternion.FromToRotation(along,relaxed)*bones[i].rotation;
        }
        var animationComponent=root.AddComponent<Animation>();animationComponent.playAutomatically=true;
        foreach(string state in new[]{"Idle","Walk","Run","Trade","Sleep"})
        {
            var clip=Clip(root.transform,bones,model,state);animationComponent.AddClip(clip,state);
            if(state=="Idle")animationComponent.clip=clip;
        }
        var driver=root.AddComponent<RuralCharacterAnimator>();driver.clips=animationComponent;driver.gestureBone=bones[4];
        var feet=root.AddComponent<NPCFootContact>();
        feet.leftFoot=transforms.First(t=>t.name==(medieval?"Foot_L":"foot.L"));
        feet.rightFoot=transforms.First(t=>t.name==(medieval?"Foot_R":"foot.R"));
        float low=float.PositiveInfinity;
        foreach(var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var baked=new Mesh();skin.BakeMesh(baked);
            foreach(var vertex in baked.vertices)low=Mathf.Min(low,skin.transform.TransformPoint(vertex).y);
            Object.DestroyImmediate(baked);
        }
        feet.leftSole=feet.leftFoot.InverseTransformPoint(new Vector3(feet.leftFoot.position.x,low,feet.leftFoot.position.z));
        feet.rightSole=feet.rightFoot.InverseTransformPoint(new Vector3(feet.rightFoot.position.x,low,feet.rightFoot.position.z));
        return root;
    }
    static AnimationClip Clip(Transform root,Transform[] bones,string model,string state)
    {
        float duration=state=="Walk"?1.1f:state=="Run"?.65f:state=="Trade"?1.05f:3.2f;
        var clip=new AnimationClip{name=state,legacy=true,wrapMode=state=="Trade"?WrapMode.Once:WrapMode.Loop};
        for(int i=0;i<bones.Length;i++)
        {
            var bone=bones[i];Quaternion rest=bone.localRotation;
            Vector3 axis=bone.parent.InverseTransformDirection(root.right);
            var curves=new AnimationCurve[4];for(int c=0;c<4;c++)curves[c]=new AnimationCurve();
            for(int k=0;k<=16;k++)
            {
                float t=k/16f,sine=Mathf.Sin(t*Mathf.PI*2),angle=0;
                if(state=="Idle" || state=="Sleep"){if(i==0)angle=sine*.8f;if(i==1)angle=state=="Sleep"?18+sine*2:sine*2;}
                if(state=="Trade"){if(i==4)angle=-55*Mathf.Sin(t*Mathf.PI);if(i==5)angle=-20*Mathf.Sin(t*Mathf.PI);if(i==1)angle=6*Mathf.Sin(t*Mathf.PI);}
                if(state=="Walk" || state=="Run")
                {
                    float amount=state=="Run"?32:21;
                    if(i==6)angle=sine*amount;if(i==8)angle=-sine*amount;
                    if(i==7)angle=Mathf.Max(0,-sine)*amount*.65f;if(i==9)angle=Mathf.Max(0,sine)*amount*.65f;
                    if(i==2)angle=-sine*amount*.65f;if(i==4)angle=sine*amount*.65f;
                    if(i==3 || i==5)angle=state=="Run"?-25:-8;
                }
                Quaternion q=Quaternion.AngleAxis(angle,axis)*rest;
                curves[0].AddKey(t*duration,q.x);curves[1].AddKey(t*duration,q.y);curves[2].AddKey(t*duration,q.z);curves[3].AddKey(t*duration,q.w);
            }
            for(int c=0;c<4;c++)clip.SetCurve(AnimationUtility.CalculateTransformPath(bone,root),typeof(Transform),"localRotation."+"xyzw"[c],curves[c]);
        }
        clip.EnsureQuaternionContinuity();
        string path=Folder+"/"+model+"-"+state+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if(existing!=null){EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);return existing;}
        AssetDatabase.CreateAsset(clip,path);return clip;
    }
}
