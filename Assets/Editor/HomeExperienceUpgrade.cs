using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class HomeExperienceUpgrade
{
    const string Folder="Assets/ChickenHeistGenerated/PlayerHome";
    public static int KeepSingleBird(GameObject bird)
    {
        var alternatives=bird.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name.StartsWith("alt")).OrderBy(r=>r.name).ToArray();
        if(alternatives.Length<2)return 0;
        for(int i=1;i<alternatives.Length;i++)Object.DestroyImmediate(alternatives[i].gameObject);
        return alternatives.Length-1;
    }
    static Material Material(string name,Color color,string shader="Universal Render Pipeline/Lit")
    {
        string path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(mat,path);}
        mat.color=color;if(mat.HasProperty("_Smoothness"))mat.SetFloat("_Smoothness",.1f);return mat;
    }
    static GameObject Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
        go.transform.localPosition=position;go.transform.localScale=scale;
        Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;return go;
    }
    public static void DressPlayer(GameObject root)
    {
        if(root.GetComponent<HandheldPhone>()!=null)return;
        var bones=root.GetComponentsInChildren<Transform>();
        var chest=bones.Single(t=>t.name=="Chest");
        var cloth=Material("Forro da camisa",new Color(.27f,.28f,.24f));
        var lining=GameObject.CreatePrimitive(PrimitiveType.Sphere);lining.name="Forro fechado da camisa";
        lining.transform.SetParent(root.transform,false);lining.transform.localPosition=new Vector3(0,1.035f,0);
        lining.transform.localScale=new Vector3(.30f,.57f,.19f);lining.transform.SetParent(chest,true);lining.layer=30;
        Object.DestroyImmediate(lining.GetComponent<Collider>());lining.GetComponent<Renderer>().sharedMaterial=cloth;
        lining.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
        // The garment's reverse faces remain opaque when viewed down through its collar.
        var skin=root.GetComponentsInChildren<SkinnedMeshRenderer>().First(r=>r.name=="ProtagonistBody");
        skin.sharedMaterial.SetFloat("_Cull",0);
        var phoneRoot=new GameObject("Celular na mao");phoneRoot.transform.SetParent(root.transform,false);
        var phone=phoneRoot.transform;
        var modernPhone=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/ModernPhone/ModernPhone.fbx");
        if(modernPhone!=null)
        {
            var model=(GameObject)PrefabUtility.InstantiatePrefab(modernPhone,phone);
            model.name="Aparelho moderno arredondado";
            model.transform.localPosition=Vector3.zero;
            // The FBX exporter already maps the display toward the first-person camera.
            model.transform.localRotation=Quaternion.identity;
            model.transform.localScale=Vector3.one;
            AssignPhoneMaterials(model);
        }
        else
        {
            // Fallback only for a fresh import before the generated FBX is available.
            var shell=Material("Celular gasto",new Color(.025f,.029f,.026f));
            Box(phone,"Carcaca provisoria",Vector3.zero,new Vector3(.088f,.176f,.012f),shell);
        }
        var driver=root.AddComponent<HandheldPhone>();driver.handset=phone;
        driver.upperArm=bones.Single(t=>t.name=="UpperArmR");driver.forearm=bones.Single(t=>t.name=="ForearmR");driver.hand=bones.Single(t=>t.name=="HandR");
        phoneRoot.SetActive(false);
    }
    static void AssignPhoneMaterials(GameObject model)
    {
        var graphite=Material("Celular moderno - grafite",new Color(.045f,.052f,.057f));
        graphite.SetFloat("_Metallic",.6f);graphite.SetFloat("_Smoothness",.7f);
        var back=Material("Celular moderno - traseira",new Color(.065f,.10f,.11f));
        back.SetFloat("_Metallic",.15f);back.SetFloat("_Smoothness",.7f);
        var screen=Material("Celular moderno - tela",new Color(.012f,.020f,.024f));
        screen.SetFloat("_Metallic",.1f);screen.SetFloat("_Smoothness",.9f);
        var lens=Material("Celular moderno - lentes",new Color(.01f,.015f,.018f));
        lens.SetFloat("_Metallic",.7f);lens.SetFloat("_Smoothness",.9f);
        foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
        {
            string n=renderer.name.ToLowerInvariant();
            renderer.sharedMaterial=n.Contains("display")||n.Contains("selfie")||n.Contains("lensglass")?screen:
                n.Contains("backglass")?back:n.Contains("lensrim")||n.Contains("button")?graphite:graphite;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
    }
    public static void Apply()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play mode first");
        var scene=EditorSceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BeforeHomeExperience.unity"),true);
        int removed=0;
        foreach(string name in new[]{"Chicken","Hen"})
        {
            string path="Assets/ChickenHeistGenerated/Animals/"+name+".prefab";
            if(!File.Exists(path))continue;
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try{removed+=KeepSingleBird(prefab);PrefabUtility.SaveAsPrefabAsset(prefab,path);}
            finally{PrefabUtility.UnloadPrefabContents(prefab);}
        }
        foreach(var bird in Object.FindObjectsByType<InteractableChicken>(FindObjectsSortMode.None))removed+=KeepSingleBird(bird.gameObject);
        var home=GameObject.Find("Casa do Protagonista - Sitio do Recomeco");
        var interior=home.transform.Find("Interior - Uma vida por reconstruir");
        var old=interior.Find("Espelho rachado");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var mirror=new GameObject("Espelho rachado");mirror.transform.SetParent(interior,false);
        mirror.transform.localPosition=new Vector3(-2.40f,1.94f,6.77f);mirror.transform.localRotation=Quaternion.Euler(0,180,0);
        var wood=Material("Moldura espelho usada",new Color(.22f,.18f,.13f));
        Box(mirror.transform,"Fundo",new Vector3(0,0,-.014f),new Vector3(.82f,1.36f,.055f),wood);
        foreach(float side in new[]{-1f,1f})
        {
            Box(mirror.transform,"Moldura lateral",new Vector3(side*.40f,0,.025f),new Vector3(.047f,1.37f,.05f),wood);
            Box(mirror.transform,"Moldura horizontal",new Vector3(0,side*.66f,.025f),new Vector3(.80f,.048f,.05f),wood);
        }
        var surface=GameObject.CreatePrimitive(PrimitiveType.Quad);surface.name="Vidro refletivo rachado";surface.transform.SetParent(mirror.transform,false);
        surface.transform.localPosition=new Vector3(0,0,.024f);surface.transform.localScale=new Vector3(.75f,1.25f,1);
        Object.DestroyImmediate(surface.GetComponent<Collider>());
        surface.GetComponent<Renderer>().sharedMaterial=Material("Espelho rachado",Color.white,"ChickenHeist/CrackedMirror");
        var reflection=mirror.AddComponent<CrackedHomeMirror>();reflection.surface=surface.GetComponent<Renderer>();reflection.viewer=Camera.main;
        PrefabUtility.SaveAsPrefabAsset(home,Folder+"/ProtagonistHome.prefab");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("output/home-experience");File.WriteAllText("output/home-experience/install.txt","Removed alternate chicken meshes: "+removed);
    }
    public static void RunBatch(){ProtagonistInstaller.Install();Apply();SessionState.SetBool("Protagonist.Batch",true);PlayableVillageTests.Run();}
}
