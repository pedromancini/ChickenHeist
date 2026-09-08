using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class PlayerHomeBuilder
{
    [MenuItem("Chicken Heist/Build Playable Village Market")]
    public static void BuildMarket()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var home=GameObject.Find("Casa do Protagonista - Sitio do Recomeco");
        var game=Object.FindFirstObjectByType<HeistGameManager>();
        if(home==null || game==null)throw new System.InvalidOperationException("Open the rural scene with the protagonist home.");
        var scene=SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BeforeVillageMarket.unity"),true);
        var old=GameObject.Find("Venda do Vale");if(old!=null)Object.DestroyImmediate(old);
        var marketRoot=new GameObject("Venda do Vale");marketRoot.transform.position=FindHiddenMarketSite(home.transform);
        var root=marketRoot.transform;
        Physics.SyncTransforms();var surfaces=RuralTreeRoots.TerrainSurfaces();
        float ground=0;
        foreach(var p in new[]{new Vector3(-6,0,-6),new Vector3(6,0,-6),new Vector3(-6,0,6),new Vector3(6,0,6)})
            if(RuralTreeRoots.SurfaceHeight(surfaces,root.TransformPoint(p),out float y))ground=Mathf.Max(ground,y);
        root.position=new Vector3(root.position.x,ground,root.position.z);
        var wood=WornMaterial("Madeira da venda",new Color(.19f,.17f,.14f));
        var cream=WornMaterial("Letreiro pintado",new Color(.76f,.78f,.65f));
        var dark=WornMaterial("Ferro e letras",new Color(.07f,.095f,.085f));
        var foundation=WornMaterial("Pedra da fundacao",new Color(.34f,.35f,.31f));
        Part(root,"Fundacao",new Vector3(0,-.08f,0),new Vector3(12,.18f,10),foundation);
        var storeSource=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Prefabs/Bld_StoreBuilding_01.prefab");
        var store=Object.Instantiate(storeSource,root);store.name="Armazem rural";
        store.transform.RotateAround(ProceduralFarmGenerator.VisualBounds(store).center,Vector3.up,180);
        FitMarketProp(store,root,new Vector3(0,.02f,1),new Vector3(10,6,8));
        ConvertMarketMaterials(store);
        var collider=store.AddComponent<BoxCollider>();Bounds b=ProceduralFarmGenerator.VisualBounds(store);
        collider.center=store.transform.InverseTransformPoint(b.center);collider.size=new Vector3(b.size.x/store.transform.lossyScale.x,b.size.y/store.transform.lossyScale.y,b.size.z/store.transform.lossyScale.z);
        Part(root,"Balcao de atendimento",new Vector3(0,.56f,-5.15f),new Vector3(3.2f,1.10f,.70f),wood);
        Part(root,"Tampo do balcao",new Vector3(0,1.135f,-5.15f),new Vector3(3.3f,.08f,.82f),wood);
        for(int i=0;i<8;i++)Part(root,"Tabua da frente do balcao",new Vector3(-1.39f+i*.40f,.53f,-5.513f),new Vector3(.37f,1.04f,.03f),wood);
        DressHiddenMarket(root,wood,dark);
        var till=Part(root,"Caixa registradora antiga",new Vector3(.99f,1.29f,-5.05f),new Vector3(.38f,.24f,.36f),dark);
        for(int i=0;i<3;i++)Part(root,"Tecla da registradora",new Vector3(.90f+i*.07f,1.419f,-5.11f),new Vector3(.04f,.016f,.055f),cream);
        Place("Box",new Vector3(-2.7f,0,-4.5f),new Vector3(.85f,.8f,.8f),root).name="Caixa de suprimentos";
        Place("Barell",new Vector3(2.7f,0,-4.5f),new Vector3(.75f,1.1f,.75f),root).name="Barril da venda";
        var npc=VillagerNPCFactory.Create(root,"Seu Anselmo - Comerciante","Blacksmith");npc.transform.localPosition=new Vector3(0,.03f,-4.10f);npc.transform.localRotation=Quaternion.Euler(0,180,0);
        var npcCollider=npc.AddComponent<CapsuleCollider>();npcCollider.height=1.8f;npcCollider.radius=.25f;npcCollider.center=Vector3.up*.9f;
        var service=new GameObject("Ponto de atendimento");service.transform.SetParent(root,false);service.transform.localPosition=new Vector3(0,1,-6.0f);
        var market=marketRoot.AddComponent<VillageMarket>();market.counter=service.transform;market.merchant=npc.GetComponent<RuralCharacterAnimator>();
        var lamp=new GameObject("Luz da venda");lamp.transform.SetParent(root,false);lamp.transform.localPosition=new Vector3(0,2.45f,-5.1f);
        var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.66f,.32f);light.intensity=1.4f;light.range=6;
        BuildHiddenMarketTrail(root,home.transform);
        var roads=GameObject.Find(RuralRoadSurface.RootName);if(roads!=null)Object.DestroyImmediate(roads);
        RuralRoadSurface.Build(Object.FindObjectsByType<RuralRoadSpan>(FindObjectsSortMode.None),AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Materials/ground/Ground048_1K-JPG_Color.jpg"));
        var existingBody=game.player.Find("Protagonista - Corpo animado");if(existingBody!=null)Object.DestroyImmediate(existingBody.gameObject);
        var oldRenderer=game.player.GetComponent<MeshRenderer>();if(oldRenderer!=null)oldRenderer.enabled=false;
        var playerBody=RuralCharacterBuilder.Create(game.player,"Protagonista - Corpo animado",true);playerBody.transform.localPosition=new Vector3(0,0,-.12f);
        var interior=home.transform.Find(InteriorName);var bed=interior.Find("Prop_Bed_01");
        var coop=home.transform.Find("Galinheiro gasto - Ultimo Recurso");
        var flock=coop.GetComponent<HomeFlockView>()??coop.gameObject.AddComponent<HomeFlockView>();
        flock.chickenPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChickenHeistGenerated/Animals/Chicken.prefab");
        if(bed.GetComponent<HomeNextNight>()==null)bed.gameObject.AddComponent<HomeNextNight>();
        var extraction=Object.FindFirstObjectByType<ExtractionZone>();
        extraction.transform.position=home.transform.position+new Vector3(-3,.05f,-9);
        extraction.returnPrompt="E | Entregar as galinhas ao sitio";
        var controller=game.player.GetComponent<CharacterController>();controller.enabled=false;
        game.player.position=home.transform.position+new Vector3(-3.22f,.72f,3.6f);game.player.rotation=Quaternion.Euler(0,180,0);controller.enabled=true;
        var settings=new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if(!settings.Exists(s=>s.path==scene.path))settings.Add(new EditorBuildSettingsScene(scene.path,true));
        else foreach(var entry in settings)if(entry.path==scene.path)entry.enabled=true;
        EditorBuildSettings.scenes=settings.ToArray();
        RuralWorldReview.PersistGeneratedAssets(scene);
        PrefabUtility.SaveAsPrefabAsset(marketRoot,"Assets/ChickenHeistGenerated/PlayerHome/VendaDoVale.prefab");
        PrefabUtility.SaveAsPrefabAsset(home,"Assets/ChickenHeistGenerated/PlayerHome/ProtagonistHome.prefab");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("output/playable-review");
        Capture(marketRoot,"output/playable-review/market-front.png",new Vector3(8,4,-13),new Vector3(0,1.8f,-2),65);
        Capture(npc,"output/playable-review/merchant.png",new Vector3(2,1.3f,-2.5f),new Vector3(0,1.05f,0),52);
        Capture(home,"output/playable-review/home-to-market.png",new Vector3(15,14,-27),new Vector3(12,0,-5),65);
        Debug.Log("PLAYABLE MARKET SAVED: trader, supplies, protagonist and five animation clips.");
    }
    static void FitMarketProp(GameObject obj,Transform parent,Vector3 position,Vector3 limit)
    {
        Bounds b=ProceduralFarmGenerator.VisualBounds(obj);obj.transform.localScale*=Mathf.Min(limit.x/b.size.x,limit.y/b.size.y,limit.z/b.size.z);
        b=ProceduralFarmGenerator.VisualBounds(obj);Vector3 target=parent.TransformPoint(position);obj.transform.position+=new Vector3(target.x-b.center.x,target.y-b.min.y,target.z-b.center.z);
    }
    static void ConvertMarketMaterials(GameObject obj)
    {
        foreach(var renderer in obj.GetComponentsInChildren<Renderer>())
        {
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++)
            {
                var source=materials[i];var replacement=WornMaterial(source.name+" - Venda URP",new Color(.38f,.40f,.36f));
                Texture texture=null;
                foreach(string property in new[]{"_BaseMap","_MainTex"})if(source.HasProperty(property) && source.GetTexture(property)!=null){texture=source.GetTexture(property);break;}
                replacement.SetTexture("_BaseMap",texture);materials[i]=replacement;
            }
            renderer.sharedMaterials=materials;
        }
    }
    static void Sign(Transform root,string words,Vector3 position,float size,Color color)
    {
        var go=new GameObject(words);go.transform.SetParent(root,false);go.transform.localPosition=position;
        var text=go.AddComponent<TextMesh>();text.text=words;text.fontSize=64;text.characterSize=size;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=color;
        Bounds bounds=go.GetComponent<Renderer>().bounds;
        go.transform.localScale=Vector3.one*Mathf.Min(3.05f/Mathf.Max(.01f,bounds.size.x),(size>.1f?.22f:.095f)/Mathf.Max(.01f,bounds.size.y));
    }
}
