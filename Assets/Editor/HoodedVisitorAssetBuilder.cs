using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Converts the supplied static GLB into a Unity-native, reusable visual.  The
// cinematic currently uses this full-body visual alongside its actor rig.
// Full-body skinning must be verified separately from prop IK.
public static class HoodedVisitorAssetBuilder
{
    public static void BuildAndReview(){Build();VisitorCinematicReview.Review();}
    public const string Source="Assets/ChickenHeistGenerated/Characters/HoodedVisitor/HoodedVisitor.obj";
    public const string Prefab="Assets/Resources/Cinematics/HoodedVisitorVisual.prefab";
    const string Texture="Assets/ChickenHeistGenerated/Characters/HoodedVisitor/hooded_visitor_0.png";
    const string MaterialPath="Assets/Resources/Cinematics/HoodedVisitorVisual.mat";

    [MenuItem("Chicken Heist/Cinematics/Build Hooded Visitor Asset")]
    public static void Build()
    {
        if(!File.Exists(Source))throw new FileNotFoundException("Converted supplied visitor mesh is missing",Source);
        AssetDatabase.ImportAsset(Source,ImportAssetOptions.ForceUpdate);
        var importer=(ModelImporter)AssetImporter.GetAtPath(Source);
        if(!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(Source);
        if(source==null)throw new System.InvalidOperationException("Unity could not import the supplied hooded visitor mesh.");
        var material=AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if(material==null)
        {
            material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Tecido e rosto - visitante fornecido"};
            AssetDatabase.CreateAsset(material,MaterialPath);
        }
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Texture));
        // Preserve authored colors. Coordinate conversion belongs in the
        // source converter, not in a tint that masks incorrect texture UVs.
        material.SetColor("_BaseColor",Color.white);
        material.SetFloat("_Metallic",0);material.SetFloat("_Smoothness",.08f);material.SetFloat("_Cull",0);
        var root=new GameObject("Visitante encapuzado - modelo fornecido");
        var model=Object.Instantiate(source,root.transform);
        model.name="Malha do visitante fornecida";
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            renderer.sharedMaterial=material;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows=true;
            renderer.gameObject.layer=0;
        }
        PrefabUtility.SaveAsPrefabAsset(root,Prefab);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        var renderers=prefab.GetComponentsInChildren<Renderer>(true);
        var bounds=new Bounds();bool initialized=false;
        foreach(var renderer in renderers){if(!initialized){bounds=renderer.bounds;initialized=true;}else bounds.Encapsulate(renderer.bounds);}
        Debug.Log("Hooded visitor asset built. renderers="+renderers.Length+" bounds="+bounds.size+" source="+Source);
    }

    [MenuItem("Chicken Heist/Cinematics/Preview Hooded Visitor Asset")]
    public static void Preview()
    {
        Build();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        if(prefab==null)throw new System.InvalidOperationException("Build the hooded visitor asset first.");
        var actor=Object.Instantiate(prefab);actor.transform.localScale=Vector3.one*1.7f;
        var renderers=actor.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
        foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        var cameraNode=new GameObject("Preview camera");var camera=cameraNode.AddComponent<Camera>();camera.backgroundColor=new Color(.035f,.045f,.07f);camera.clearFlags=CameraClearFlags.SolidColor;camera.fieldOfView=42;
        var distance=Mathf.Max(bounds.extents.y*2.7f,bounds.extents.x*2.2f);camera.transform.position=bounds.center+new Vector3(0,.02f,-distance);camera.transform.LookAt(bounds.center+Vector3.up*.03f);
        var lightNode=new GameObject("Preview key");var light=lightNode.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.color=new Color(1,.76f,.51f);light.transform.rotation=Quaternion.Euler(38,-24,0);
        var target=new RenderTexture(960,960,24);camera.targetTexture=target;camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(960,960,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,960,960),0,0);image.Apply();Directory.CreateDirectory("output");File.WriteAllBytes("output/hooded-visitor-preview.png",image.EncodeToPNG());RenderTexture.active=old;
        Object.DestroyImmediate(image);Object.DestroyImmediate(target);Object.DestroyImmediate(cameraNode);Object.DestroyImmediate(lightNode);Object.DestroyImmediate(actor);AssetDatabase.Refresh();
    }
}
