using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class RemainingUpgrade
{
    public static void Install()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        Directory.CreateDirectory("Assets/Resources/AnimalMotion");Directory.CreateDirectory("output/remaining-review");AssetDatabase.Refresh();
        foreach(var bird in Object.FindObjectsByType<InteractableChicken>()){Prepare(bird.gameObject,false);}
        foreach(var cow in Object.FindObjectsByType<SimpleAnimalWander>()){Prepare(cow.gameObject,true);}
        var home=Object.FindAnyObjectByType<HouseholdEconomy>();Coop(home);Wood(home.home);
        RuralWorldReview.PersistGeneratedAssets(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        RemainingReview.Run();
    }
    static void Prepare(GameObject root,bool cow)
    {
        var motion=root.GetComponent<FarmAnimalMotion>();if(motion==null)motion=root.AddComponent<FarmAnimalMotion>();motion.cow=cow;
        var filter=root.GetComponentInChildren<MeshFilter>();if(filter==null)return;var source=filter.sharedMesh;
        string path="Assets/Resources/AnimalMotion/"+source.name+".asset";
        if(AssetDatabase.LoadAssetAtPath<Mesh>(path)==null)
        {
            var mesh=new Mesh{name=source.name,vertices=source.vertices,normals=source.normals,uv=source.uv,colors=source.colors,subMeshCount=source.subMeshCount};
            for(int i=0;i<source.subMeshCount;i++)mesh.SetTriangles(source.GetTriangles(i),i);mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
        }
    }
    static GameObject Part(Transform root,string name,Vector3 position,Vector3 size,Material mat)
    {return SecurityEquipmentVisual.Part(root,name,PrimitiveType.Cube,position,size,mat);}
    static void Coop(HouseholdEconomy economy)
    {
        var coop=economy.home.Find("Galinheiro gasto - Ultimo Recurso");if(coop==null)throw new System.Exception("Home coop not found");
        if(coop.GetComponent<CoopUpgradeVisibility>()==null)coop.gameObject.AddComponent<CoopUpgradeVisibility>();
        var existing=coop.Find("Melhorias permanentes");if(existing!=null)return;
        var parent=new GameObject("Melhorias permanentes").transform;parent.SetParent(coop,false);
        var wood=SecurityEquipmentVisual.Material("Madeira nova do galinheiro",new Color(.43f,.29f,.15f));
        var steel=SecurityEquipmentVisual.Material("Tela galvanizada nova",new Color(.37f,.43f,.45f));var straw=SecurityEquipmentVisual.Material("Palha dos ninhos novos",new Color(.64f,.50f,.22f));
        var old=economy.repairStages??new GameObject[0];var stages=new GameObject[3];
        for(int i=0;i<3;i++){stages[i]=new GameObject("Nivel "+(i+1));stages[i].transform.SetParent(parent,false);}
        // Preserve existing repair pieces under their matching level.
        for(int i=0;i<old.Length;i++)if(old[i]!=null && i<3)old[i].transform.SetParent(stages[i].transform,true);
        for(int side=-1;side<=1;side+=2)
        {
            Part(stages[0].transform,"Mourao reforcado",new Vector3(side*3,.68f,-2.2f),new Vector3(.16f,1.36f,.16f),wood);
            Part(stages[0].transform,"Travessa nova",new Vector3(side*3,1.2f,0),new Vector3(.10f,.10f,4.4f),wood);
            var roof=Part(stages[1].transform,"Cobertura nova",new Vector3(side*.79f,1.98f,1),new Vector3(1.65f,.05f,2.05f),steel);roof.transform.localRotation=Quaternion.Euler(0,0,-side*8);
            for(int wire=0;wire<12;wire++)SecurityEquipmentVisual.Rod(stages[1].transform,"Tela lateral nova",new Vector3(side*3,wire*.1f,-2.2f),new Vector3(side*3,wire*.1f,2.2f),.012f,steel);
        }
        for(int n=0;n<3;n++)
        {
            float x=-.95f+n*.9f;Part(stages[2].transform,"Base do ninho",new Vector3(x,.86f,1.1f),new Vector3(.75f,.08f,.65f),wood);
            Part(stages[2].transform,"Palha limpa",new Vector3(x,.92f,1.1f),new Vector3(.62f,.045f,.50f),straw);
            Part(stages[2].transform,"Divisoria do ninho",new Vector3(x-.38f,1.05f,1.1f),new Vector3(.06f,.35f,.70f),wood);
        }
        Part(stages[2].transform,"Comedouro ampliado",new Vector3(1.9f,.19f,-.85f),new Vector3(1.3f,.12f,.5f),steel);
        economy.repairStages=stages;
    }
    static void Wood(Transform root)
    {
        var house=root.Find("Casa de madeira envelhecida");
        if(house!=null)
        {
            var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/MarpaStudio/Built-In/Materials/House.mat");
            foreach(var renderer in house.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Casa - atlas e relevo restaurados",enableInstancing=true};
                    material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MarpaStudio/Textures/House_Albedo.png"));
                    material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/MarpaStudio/Textures/House_Normal.png"));
                    material.SetFloat("_BumpScale",.65f);material.EnableKeyword("_NORMALMAP");material.SetFloat("_Smoothness",.12f);
                    if(source!=null){material.SetTexture("_OcclusionMap",source.GetTexture("_OcclusionMap"));material.SetFloat("_OcclusionStrength",.6f);material.EnableKeyword("_OCCLUSIONMAP");}
                    materials[i]=material;
                }
                renderer.sharedMaterials=materials;
                var filter=renderer.GetComponent<MeshFilter>();if(filter!=null && filter.sharedMesh!=null)filter.sharedMesh.RecalculateTangents();
            }
        }
        const string path="Assets/Resources/HomeWoodGrain.asset";
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if(texture==null)
        {
            texture=new Texture2D(128,256,TextureFormat.RGB24,true){name="Home wood grain",wrapMode=TextureWrapMode.Repeat};
            for(int y=0;y<256;y++)for(int x=0;x<128;x++)
            {
                float warp=Mathf.PerlinNoise(x*.018f,y*.006f)*6;float grain=Mathf.Sin(x*.7f+warp)*.06f+Mathf.PerlinNoise(x*.14f,y*.024f)*.15f;
                float tone=.72f+grain;texture.SetPixel(x,y,new Color(tone,tone*.91f,tone*.78f));
            }
            texture.Apply();AssetDatabase.CreateAsset(texture,path);
        }
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            if(house!=null && renderer.transform.IsChildOf(house))continue;
            if(!renderer.name.ToLowerInvariant().Contains("tabua") && !renderer.name.ToLowerInvariant().Contains("madeira") && !renderer.name.ToLowerInvariant().Contains("assoalho"))continue;
            var mats=renderer.sharedMaterials;
            for(int i=0;i<mats.Length;i++)if(mats[i]!=null && mats[i].HasProperty("_BaseMap") && !mats[i].name.EndsWith(" - veios"))
            {mats[i]=Object.Instantiate(mats[i]);mats[i].name+=" - veios";mats[i].SetTexture("_BaseMap",texture);mats[i].SetTextureScale("_BaseMap",new Vector2(1,2));}
            renderer.sharedMaterials=mats;
        }
    }
}
