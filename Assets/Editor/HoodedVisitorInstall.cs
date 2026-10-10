using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// The hooded visitor of the opening cinematic, rigged offline by Tools/SourceArt/HoodedVisitor/build_visitor.py on
// Elias' bone names (fingers included). Its clips are baked from the same captured takes as Elias' through its own
// humanoid avatar (with a backward step: he backs away from the door after knocking), and it replaces, in VisitorStage.prefab, the villager skeleton the static mesh used to be glued to
// at run time (weights cut by height, no fingers: the arms broke and the hands stayed open like starfish).
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod HoodedVisitorInstall.Install
public static class HoodedVisitorInstall
{
    const string Folder="Assets/ChickenHeistGenerated/Characters/HoodedVisitor/";
    const string Model=Folder+"Visitor_Rigged.fbx",Humanoid=Folder+"VisitorHumanoid.fbx",Clips=Folder+"Clips/";
    const string Stage="Assets/Resources/Cinematics/VisitorStage.prefab",MaterialPath="Assets/Resources/Cinematics/HoodedVisitorVisual.mat";
    const string Texture=Folder+"hooded_visitor_0.png",Takes="Assets/ThirdParty/HumanBasicMotions/";
    internal const string Child="Visitor_Rigged(Clone)",Rig="VisitorRig",Body="VisitorBody";
    static readonly Dictionary<string,string> States=new Dictionary<string,string>{{"Idle","Idle01"},{"Breathe","Idle02"},{"Talk","Talk01"},{"Walk","Walk01_Forward"},{"WalkBackward","Walk01_Backward"}};

    public static void Install()
    {
        var log=new List<string>();
        Import(Model,ModelImporterAnimationType.Legacy);Import(Humanoid,ModelImporterAnimationType.Human);
        var avatar=EliasNativeInstall.Avatar(Humanoid);
        if(avatar==null)throw new InvalidOperationException("Visitor humanoid avatar missing or invalid: "+Humanoid);
        if(!AssetDatabase.IsValidFolder(Clips.TrimEnd('/')))AssetDatabase.CreateFolder(Folder.TrimEnd('/'),"Clips");
        foreach(var kv in States)
        {
            AssetDatabase.ImportAsset(Takes+"HumanM@"+kv.Value+".fbx",ImportAssetOptions.ForceSynchronousImport);
            var take=AssetDatabase.LoadAllAssetsAtPath(Takes+"HumanM@"+kv.Value+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));
            var clip=EliasNativeInstall.Bake(kv.Key,avatar,(target,t)=>take.SampleAnimation(target,t),take.length,Clips+kv.Key+".anim",true,Model,Child,Rig);
            log.Add("baked "+kv.Key+" <- "+kv.Value+" ("+clip.length.ToString("0.00")+" s)");
        }
        AssetDatabase.SaveAssets();
        var root=PrefabUtility.LoadPrefabContents(Stage);
        try{Build(root.GetComponent<VisitorCinematicStage>().visitor);PrefabUtility.SaveAsPrefabAsset(root,Stage);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
        log.Add("visitor rebuilt in "+Stage);
        Directory.CreateDirectory("output/visitor-opening");File.WriteAllLines("output/visitor-opening/visitor-install.txt",log);
        Debug.Log("HOODED VISITOR INSTALLED\n"+string.Join("\n",log));
    }

    // Empties the actor and puts the rigged visitor in it, with its clips on an Animation component at the actor
    // (the clip paths start at the model: Visitor_Rigged(Clone)/VisitorRig/Root/Hips/...).
    public static void Build(Transform actor)
    {
        for(int i=actor.childCount-1;i>=0;i--)Object.DestroyImmediate(actor.GetChild(i).gameObject);
        foreach(var c in actor.GetComponents<Component>().Where(c=>!(c is Transform)).ToArray())Object.DestroyImmediate(c);
        var model=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model),actor);
        model.name=Child;model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one;
        foreach(var a in model.GetComponentsInChildren<Animator>(true))Object.DestroyImmediate(a);
        var material=VisitorMaterial();
        foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            skin.sharedMaterial=material;skin.updateWhenOffscreen=true;skin.shadowCastingMode=ShadowCastingMode.On;skin.receiveShadows=true;
        }
        foreach(var node in actor.GetComponentsInChildren<Transform>(true))node.gameObject.layer=0;
        var animation=actor.gameObject.AddComponent<Animation>();animation.playAutomatically=false;
        foreach(var state in States.Keys)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips+state+".anim");
            if(clip==null)throw new InvalidOperationException("Visitor clip missing: "+state+" (run HoodedVisitorInstall.Install)");
            animation.AddClip(clip,state);
        }
        animation.clip=animation.GetClip("Idle");animation.GetClip("Idle").SampleAnimation(actor.gameObject,0);animation.enabled=false;
    }

    // Four frames of each clip, front and side, from the stage prefab: output/visitor-opening/visitor-<clip>.png.
    // Run: Unity.exe -batchmode -quit -projectPath . -executeMethod HoodedVisitorInstall.Preview
    public static void Preview()
    {
        UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,UnityEditor.SceneManagement.NewSceneMode.Single);
        var stage=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Stage));
        foreach(var b in stage.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
        var actor=stage.GetComponent<VisitorCinematicStage>().visitor;stage.GetComponent<VisitorCinematicStage>().elias.gameObject.SetActive(false);
        actor.position=Vector3.zero;actor.rotation=Quaternion.identity;
        foreach(var r in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true)){r.forceMatrixRecalculationPerRender=true;r.updateWhenOffscreen=true;}
        var light=new GameObject("light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(40,-30,0);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.5f,.55f);
        var cam=new GameObject("cam").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.42f,.44f,.48f);
        // -visitorView <centre height>,<half height>: a closer look (e.g. 1.25,0.42 for the shoulders)
        var args=Environment.GetCommandLineArgs();int at=Array.IndexOf(args,"-visitorView");
        float centreY=.9f,size=1f;if(at>=0 && at+1<args.Length){var v=args[at+1].Split(',');centreY=float.Parse(v[0],System.Globalization.CultureInfo.InvariantCulture);size=float.Parse(v[1],System.Globalization.CultureInfo.InvariantCulture);}
        cam.orthographic=true;cam.orthographicSize=size;cam.nearClipPlane=.05f;cam.farClipPlane=20;
        var rt=new RenderTexture(300,400,24);cam.targetTexture=rt;cam.Render();
        var animation=actor.GetComponent<Animation>();
        foreach(AnimationState state in animation)
        {
            var sheet=new Texture2D(1200,400,TextureFormat.RGB24,false);
            for(int i=0;i<4;i++)
            {
                state.clip.SampleAnimation(actor.gameObject,state.clip.length*i/4f);
                var center=Vector3.up*centreY;cam.transform.position=center+Quaternion.Euler(0,i%2==0?0:120,0)*Vector3.forward*4;cam.transform.LookAt(center);
                cam.Render();RenderTexture.active=rt;sheet.ReadPixels(new Rect(0,0,300,400),i*300,0);RenderTexture.active=null;
            }
            sheet.Apply();Directory.CreateDirectory("output/visitor-opening");File.WriteAllBytes("output/visitor-opening/visitor-"+state.name+(at>=0?"-close":"")+".png",sheet.EncodeToPNG());Object.DestroyImmediate(sheet);
        }
        // lowest point of the body in each clip, against the floor (the stage stands the actor on it)
        var skin=actor.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name==Body);var baked=new Mesh();var lines=new List<string>();
        foreach(AnimationState state in animation)
        {
            float low=float.PositiveInfinity,high=float.NegativeInfinity;
            for(int i=0;i<=8;i++){state.clip.SampleAnimation(actor.gameObject,state.clip.length*i/8f);skin.BakeMesh(baked,true);foreach(var v in baked.vertices){float y=skin.transform.TransformPoint(v).y;low=Mathf.Min(low,y);high=Mathf.Max(high,y);}}
            lines.Add($"{state.name}: lowest {low*100:0.0} cm, highest {high*100:0.0} cm");
        }
        File.WriteAllLines("output/visitor-opening/visitor-heights.txt",lines);Debug.Log("VISITOR PREVIEW\n"+string.Join("\n",lines));
        cam.targetTexture=null;rt.Release();
    }

    static Material VisitorMaterial()
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Tecido e rosto - visitante fornecido"};AssetDatabase.CreateAsset(material,MaterialPath);}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Texture));material.SetColor("_BaseColor",Color.white);
        material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.08f);
        // the rigged surface is closed: back faces are never seen
        material.SetFloat("_Cull",2);
        EditorUtility.SetDirty(material);return material;
    }

    static void Import(string path,ModelImporterAnimationType type)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);bool dirty=false;
        if(importer==null)throw new FileNotFoundException("Run build_visitor.py with VISITOR_EXPORT=1 first",path);
        if(importer.animationType!=type){importer.animationType=type;dirty=true;}
        if(type==ModelImporterAnimationType.Human && importer.avatarSetup!=ModelImporterAvatarSetup.CreateFromThisModel){importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;dirty=true;}
        if(path==Model && (importer.importAnimation || !importer.isReadable)){importer.importAnimation=false;importer.isReadable=true;dirty=true;}
        if(importer.materialImportMode!=ModelImporterMaterialImportMode.None){importer.materialImportMode=ModelImporterMaterialImportMode.None;dirty=true;}
        if(dirty)importer.SaveAndReimport();
    }
}
