using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class ProtagonistInstaller
{
    const string Folder="Assets/ChickenHeistGenerated/Characters/Protagonist";
    const string Model=Folder+"/Protagonist_Rigged.fbx";
    public const string PrefabPath=Folder+"/Protagonist.prefab";
    const string Output="output/protagonist-review";
    static readonly string[] States={"Idle","Walk","Run","CrouchIdle","Crouch","Jump","Fall","Pickup","Trade"};
    public static GameObject Create(Transform parent,string name)
    {
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath),parent);
        root.name=name;root.transform.localPosition=new Vector3(0,0,-.22f);
        root.GetComponent<RuralCharacterAnimator>().movement=parent.GetComponent<PlayerMovement>();
        root.GetComponent<HandheldPhone>().eyes=parent.GetComponentInChildren<Camera>().transform;
        var camera=parent.GetComponentInChildren<Camera>();if(camera!=null)camera.cullingMask &= ~(1<<31);
        return root;
    }
    [MenuItem("Chicken Heist/Install New Protagonist")]
    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
        Directory.CreateDirectory(Output);
        AssetDatabase.ImportAsset(Model,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(Model);
        importer.animationType=ModelImporterAnimationType.Legacy;importer.importAnimation=true;
        importer.animationCompression=ModelImporterAnimationCompression.Off;
        importer.SaveAndReimport();
        var clips=importer.defaultClipAnimations;
        foreach(var c in clips)
        {
            c.name=c.name.Split('|').Last();
            c.loopTime=c.name!="Pickup" && c.name!="Trade" && c.name!="Jump";
            c.wrapMode=c.loopTime?WrapMode.Loop:WrapMode.ClampForever;
        }
        importer.clipAnimations=clips;importer.SaveAndReimport();
        var root=new GameObject("Protagonista - Corpo animado");
        try
        {
            var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model),root.transform);
            foreach(var a in source.GetComponentsInChildren<Animation>())Object.DestroyImmediate(a);
            foreach(var a in source.GetComponentsInChildren<Animator>())Object.DestroyImmediate(a);
            var material=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Protagonist.mat");
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,Folder+"/Protagonist.mat");}
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Protagonist_BaseColor.png");
            if(texture==null)throw new Exception("Protagonist texture import failed.");
            material.SetTexture("_BaseMap",texture);
            material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.13f);
            foreach(var skin in source.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                skin.sharedMaterial=material;skin.updateWhenOffscreen=true;
                if(skin.name.StartsWith("ProtagonistHead")){skin.shadowCastingMode=ShadowCastingMode.On;skin.gameObject.layer=31;}
            }
            var animation=root.AddComponent<Animation>();
            foreach(string state in States)
            {
                var clip=AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Single(c=>c.name==state);
                // Imported curves are relative to the FBX root, which lives one level below this wrapper.
                var copy=Object.Instantiate(clip);copy.name=state;copy.legacy=true;
                foreach(var binding in AnimationUtility.GetCurveBindings(copy))
                {
                    var curve=AnimationUtility.GetEditorCurve(copy,binding);
                    AnimationUtility.SetEditorCurve(copy,binding,null);
                    var target=binding;target.path=source.name+(binding.path.Length>0?"/"+binding.path:"");
                    AnimationUtility.SetEditorCurve(copy,target,curve);
                }
                string path=Folder+"/"+state+".anim";
                var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(existing!=null){EditorUtility.CopySerialized(copy,existing);Object.DestroyImmediate(copy);copy=existing;}
                else AssetDatabase.CreateAsset(copy,path);
                animation.AddClip(copy,state);if(state=="Idle")animation.clip=copy;
            }
            var driver=root.AddComponent<RuralCharacterAnimator>();driver.clips=animation;
            driver.gestureBone=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="ForearmR");
            animation["Idle"].clip.SampleAnimation(root,0);
            HomeExperienceUpgrade.DressPlayer(root);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
        }
        finally{Object.DestroyImmediate(root);}
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!=RuralWorldReview.WorldScene)scene=EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BeforeNewProtagonist.unity"),true);
        var game=Object.FindFirstObjectByType<HeistGameManager>();
        var old=game.player.Find("Protagonista - Corpo animado");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var body=Create(game.player,"Protagonista - Corpo animado");
        var primitive=game.player.GetComponent<MeshRenderer>();if(primitive!=null)primitive.enabled=false;
        game.player.GetComponent<PlayerMovement>().alturaAgachado=1.4f;
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        VerifyAndCapture();
        Debug.Log("PROTAGONIST INSTALLED: original texture, skinned mesh and nine gameplay clips.");
    }
    static void VerifyAndCapture()
    {
        var results=new List<string>();
        var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        root.transform.position=new Vector3(0,1000,0);
        var driver=root.GetComponent<RuralCharacterAnimator>();
        foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            if(skin.sharedMaterial.GetTexture("_BaseMap")==null)throw new Exception("Missing protagonist paint");
        results.Add("PASS Original base-color texture is assigned to body and head");
        var head=root.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s=>s.name.StartsWith("ProtagonistHead"));
        if(head.gameObject.layer!=31)throw new Exception("Head blocks first-person camera.");
        results.Add("PASS Head hidden from first-person rendering, shadow preserved");head.shadowCastingMode=ShadowCastingMode.On;
        var camObj=new GameObject("Protagonist review camera");var cam=camObj.AddComponent<Camera>();
        cam.transform.position=root.transform.position+new Vector3(2,1.8f,4);
        cam.transform.LookAt(root.transform.position+Vector3.up*.88f);cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.2f,.24f,.25f);
        cam.orthographic=true;cam.orthographicSize=1.1f;cam.farClipPlane=20;
        var lightObj=new GameObject("Review light");var light=lightObj.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;
        lightObj.transform.rotation=Quaternion.Euler(35,-145,0);
        var target=new RenderTexture(720,720,24);target.Create();cam.targetTexture=target;
        bool fog=RenderSettings.fog;RenderSettings.fog=false;
        try
        {
            foreach(var name in States)
            {
                var clip=driver.clips[name].clip;
                if(clip.length<=0 || AnimationUtility.GetCurveBindings(clip).Length<30)throw new Exception("Missing curves: "+name);
                clip.SampleAnimation(root,clip.length*.15f);var a=driver.gestureBone.localRotation;
                clip.SampleAnimation(root,clip.length*.60f);var b=driver.gestureBone.localRotation;
                if((name=="Walk" || name=="Run" || name=="Pickup") && Quaternion.Angle(a,b)<2)throw new Exception("Arm is not animated: "+name);
                results.Add("PASS "+name+": imported skeletal curves and duration "+clip.length);
                clip.SampleAnimation(root,clip.length*.25f);
                cam.Render();var old=RenderTexture.active;RenderTexture.active=target;
                var image=new Texture2D(720,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,720,720),0,0);image.Apply();
                File.WriteAllBytes(Output+"/"+name+"-unity.png",image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=old;
            }
            head.shadowCastingMode=ShadowCastingMode.On;
            cam.cullingMask &= ~(1<<31);
            cam.orthographic=false;cam.fieldOfView=70;cam.nearClipPlane=.03f;
            foreach(string pose in new[]{"Idle","CrouchIdle","Pickup"})
            {
                var clip=driver.clips[pose].clip;clip.SampleAnimation(root,clip.length*.5f);
                cam.transform.position=root.transform.position+new Vector3(0,pose=="CrouchIdle"?1.05f:1.65f,.30f);
                cam.transform.rotation=Quaternion.Euler(55,0,0);cam.Render();
                var old=RenderTexture.active;RenderTexture.active=target;
                var image=new Texture2D(720,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,720,720),0,0);image.Apply();
                File.WriteAllBytes(Output+"/"+pose+"-first-person.png",image.EncodeToPNG());Object.DestroyImmediate(image);RenderTexture.active=old;
            }
            if(RuralCharacterAnimator.PlayerState(false,true,false,false,0)!="CrouchIdle" ||
               RuralCharacterAnimator.PlayerState(true,true,true,false,0)!="Crouch" ||
               RuralCharacterAnimator.PlayerState(true,false,true,false,0)!="Run" ||
               RuralCharacterAnimator.PlayerState(false,false,false,true,2)!="Jump" ||
               RuralCharacterAnimator.PlayerState(false,false,false,true,-2)!="Fall")throw new Exception("Invalid player state selection");
            results.Add("PASS Crouch idle, crouch walk, sprint, jump and fall state selection");
            File.WriteAllLines(Output+"/integration-tests.txt",results);
        }
        finally{RenderSettings.fog=fog;cam.targetTexture=null;target.Release();Object.DestroyImmediate(target);Object.DestroyImmediate(camObj);Object.DestroyImmediate(lightObj);Object.DestroyImmediate(root);}
    }
    public static void RunBatch()
    {
        Install();SessionState.SetBool("Protagonist.Batch",true);PlayableVillageTests.Run();
    }
    [InitializeOnLoadMethod]
    static void HookBatch()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("Protagonist.Batch",false))return;
            SessionState.SetBool("Protagonist.Batch",false);
            EditorApplication.delayCall+=()=>
            {
                try
                {
                    if(!SessionState.GetBool("ChickenHeist.PlayableVillageTests.passed",false))throw new Exception("Gameplay tests failed");
                    ChickenHeistPlayerBuild.Build();EditorApplication.Exit(0);
                }
                catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
            };
        };
    }
}
