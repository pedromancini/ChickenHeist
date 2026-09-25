using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Retargets Human Basic Motions (motion capture) onto every Medieval People model through Mecanim
// Humanoid, then bakes the result into the legacy clips the NPC factory already references
// (Characters/Villagers/<model>-<state>.anim). Existing Animation components keep their clip GUIDs,
// so residents, farmers, the merchant and cinematic actors all switch to captured motion at once.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod MocapBake.Run
public static class MocapBake
{
    const string Source="Assets/ThirdParty/HumanBasicMotions/",Models="Assets/ImportedMedievalPeople/fbx/people_unity/",Folder="Assets/ChickenHeistGenerated/Characters/Villagers/";
    // State -> motion capture take. Trade is a short spoken gesture; Sleep is the calmest breathing idle.
    static readonly (string state,string take,bool loop)[] States={("Idle","HumanM@Idle01",true),("Walk","HumanM@Walk01_Forward",true),("Run","HumanM@Run01_Forward",true),("Trade","HumanM@Talk01",false),("Sleep","HumanM@Idle02",true)};
    public static void Run()
    {
        var log=new List<string>();
        foreach(var take in States.Select(s=>s.take).Distinct())Humanoid(Source+take+".fbx",true);
        foreach(var file in Directory.GetFiles(Folder,"*-Idle.anim"))
        {
            string model=Path.GetFileName(file).Replace("-Idle.anim","");
            string path=Models+model+".fbx";if(!File.Exists(path))continue;
            if(!Humanoid(path,false)){log.Add("SKIP "+model+": humanoid avatar could not be built");continue;}
            foreach(var s in States)log.Add(Bake(model,path,s.state,s.take,s.loop));
        }
        log.AddRange(BakeProtagonist());
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("output/mocap");File.WriteAllLines("output/mocap/bake.txt",log);
        Debug.Log("MOCAP BAKE\n"+string.Join("\n",log));
    }
    // Elias keeps ProtagonistRig. A humanoid copy of its FBX lets Mecanim retarget captured takes
    // onto that rig; results are saved next to the protagonist's other legacy clips.
    const string ProtagonistFolder="Assets/ChickenHeistGenerated/Characters/ProtagonistV2/";
    static readonly (string state,string take,bool loop)[] ProtagonistStates={("Talk","HumanM@Talk01",true),("Breathe","HumanM@Idle02",true)};
    static IEnumerable<string> BakeProtagonist()
    {
        const string model=ProtagonistFolder+"Retarget/ProtagonistHumanoid.fbx";
        if(!Humanoid(model,false)){yield return "SKIP protagonist: humanoid avatar could not be built";yield break;}
        var avatar=AssetDatabase.LoadAllAssetsAtPath(model).OfType<Avatar>().First();
        foreach(var s in ProtagonistStates)
        {
            var prefab=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistFolder+"Protagonist.prefab"));
            try
            {
                var holder=prefab.GetComponentInChildren<Animation>().transform;
                var rig=prefab.GetComponentsInChildren<Transform>().First(t=>t.name=="ProtagonistRig");
                var animated=rig.parent.gameObject;
                var animator=animated.GetComponent<Animator>();if(animator==null)animator=animated.AddComponent<Animator>();animator.avatar=avatar;animator.applyRootMotion=false;
                var clip=AssetDatabase.LoadAllAssetsAtPath(Source+s.take+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
                var bones=rig.GetComponentsInChildren<Transform>().Where(t=>t!=rig).ToArray();
                var hips=animator.GetBoneTransform(HumanBodyBones.Hips);Vector3 hipRest=hips.localPosition;float startY=0;
                var curves=bones.ToDictionary(b=>b,b=>Enumerable.Range(0,4).Select(_=>new AnimationCurve()).ToArray());
                var hipCurves=Enumerable.Range(0,3).Select(_=>new AnimationCurve()).ToArray();
                int frames=Mathf.Max(2,Mathf.RoundToInt(clip.length*30));
                for(int f=0;f<=frames;f++)
                {
                    float t=clip.length*f/frames;clip.SampleAnimation(animated,t);
                    foreach(var b in bones){var q=b.localRotation;var c=curves[b];c[0].AddKey(t,q.x);c[1].AddKey(t,q.y);c[2].AddKey(t,q.z);c[3].AddKey(t,q.w);}
                    var p=hips.localPosition;if(f==0)startY=p.y;
                    hipCurves[0].AddKey(t,hipRest.x);hipCurves[1].AddKey(t,hipRest.y+(p.y-startY));hipCurves[2].AddKey(t,hipRest.z);
                }
                var baked=new AnimationClip{name=s.state,legacy=true,wrapMode=s.loop?WrapMode.Loop:WrapMode.Once,frameRate=30};
                foreach(var kv in curves){string path=AnimationUtility.CalculateTransformPath(kv.Key,holder);for(int c=0;c<4;c++)baked.SetCurve(path,typeof(Transform),"localRotation."+"xyzw"[c],kv.Value[c]);}
                string hipPath=AnimationUtility.CalculateTransformPath(hips,holder);for(int c=0;c<3;c++)baked.SetCurve(hipPath,typeof(Transform),"localPosition."+"xyz"[c],hipCurves[c]);
                baked.EnsureQuaternionContinuity();
                string target=ProtagonistFolder+s.state+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(target);
                if(existing!=null){EditorUtility.CopySerialized(baked,existing);existing.name=s.state;EditorUtility.SetDirty(existing);Object.DestroyImmediate(baked);}
                else AssetDatabase.CreateAsset(baked,target);
            }
            finally{Object.DestroyImmediate(prefab);}
            yield return "protagonist "+s.state+" <- "+s.take;
        }
    }

    // Registers the protagonist's captured clips on every Animation that plays ProtagonistRig.
    public static void InstallProtagonistClips()
    {
        var clips=ProtagonistStates.Select(s=>AssetDatabase.LoadAssetAtPath<AnimationClip>(ProtagonistFolder+s.state+".anim")).ToArray();
        void Add(GameObject root)
        {
            foreach(var animation in root.GetComponentsInChildren<Animation>(true))
            {
                if(animation.GetClip("Idle")==null || !animation.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="ProtagonistRig"))continue;
                foreach(var clip in clips){if(animation.GetClip(clip.name)!=null)animation.RemoveClip(clip.name);animation.AddClip(clip,clip.name);}
            }
        }
        foreach(var path in new[]{ProtagonistFolder+"Protagonist.prefab","Assets/Resources/Cinematics/OpeningStage.prefab","Assets/Resources/Cinematics/VisitorStage.prefab"})
        {var root=PrefabUtility.LoadPrefabContents(path);try{Add(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
        var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        foreach(var go in scene.GetRootGameObjects())Add(go);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        Debug.Log("PROTAGONIST CLIPS INSTALLED");
    }

    static bool Humanoid(string path,bool clips)
    {
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        if(importer.animationType!=ModelImporterAnimationType.Human || importer.avatarSetup!=ModelImporterAvatarSetup.CreateFromThisModel)
        {importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.SaveAndReimport();}
        var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        return avatar!=null && avatar.isHuman && avatar.isValid;
    }
    static string Bake(string model,string modelPath,string state,string take,bool loop)
    {
        var clip=AssetDatabase.LoadAllAssetsAtPath(Source+take+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
        var avatar=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().First();
        // Same hierarchy as VillagerNPCFactory: root / <model> / bones.
        var root=new GameObject("bake");var visual=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),root.transform);visual.name=model;
        try
        {
            var animator=visual.GetComponent<Animator>();if(animator==null)animator=visual.AddComponent<Animator>();animator.avatar=avatar;animator.applyRootMotion=false;
            var bones=visual.GetComponentsInChildren<Transform>().Where(t=>t!=visual.transform).ToArray();
            var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
            float length=clip.length;int frames=Mathf.Max(2,Mathf.RoundToInt(length*30));
            var curves=bones.ToDictionary(b=>b,b=>Enumerable.Range(0,4).Select(_=>new AnimationCurve()).ToArray());
            var hipCurves=Enumerable.Range(0,3).Select(_=>new AnimationCurve()).ToArray();
            Vector3 hipRest=hips.localPosition;float hipStartY=0;
            for(int f=0;f<=frames;f++)
            {
                float t=length*f/frames;
                clip.SampleAnimation(visual,t);
                foreach(var b in bones){var q=b.localRotation;var c=curves[b];c[0].AddKey(t,q.x);c[1].AddKey(t,q.y);c[2].AddKey(t,q.z);c[3].AddKey(t,q.w);}
                // In-place: keep the vertical bob and sway, drop forward travel.
                var p=hips.localPosition;if(f==0)hipStartY=p.y;
                hipCurves[0].AddKey(t,hipRest.x);hipCurves[1].AddKey(t,hipRest.y+(p.y-hipStartY));hipCurves[2].AddKey(t,hipRest.z);
            }
            var baked=new AnimationClip{name=state,legacy=true,wrapMode=loop?WrapMode.Loop:WrapMode.Once,frameRate=30};
            foreach(var kv in curves)
            {
                string bonePath=AnimationUtility.CalculateTransformPath(kv.Key,root.transform);
                for(int c=0;c<4;c++)baked.SetCurve(bonePath,typeof(Transform),"localRotation."+"xyzw"[c],kv.Value[c]);
            }
            string hipPath=AnimationUtility.CalculateTransformPath(hips,root.transform);
            for(int c=0;c<3;c++)baked.SetCurve(hipPath,typeof(Transform),"localPosition."+"xyz"[c],hipCurves[c]);
            baked.EnsureQuaternionContinuity();
            string path=Folder+model+"-"+state+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if(existing!=null){EditorUtility.CopySerialized(baked,existing);existing.name=state;EditorUtility.SetDirty(existing);Object.DestroyImmediate(baked);}
            else AssetDatabase.CreateAsset(baked,path);
            return $"{model} {state} <- {take} ({length:0.00}s, {bones.Length} bones)";
        }
        finally{Object.DestroyImmediate(root);}
    }
}
