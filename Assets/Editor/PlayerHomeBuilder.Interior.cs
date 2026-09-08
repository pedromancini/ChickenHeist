using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static partial class PlayerHomeBuilder
{
    const string InteriorName="Interior - Uma vida por reconstruir";
    const string FurniturePath="Assets/Grimnir_Farm_Assets/Grimnir_Low-Poly-Farm-Assets_Vol1_1.0/Models/FBX Format (Unity)/";

    [MenuItem("Chicken Heist/Build Protagonist Interior and Phone")]
    public static void BuildInterior()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var home=GameObject.Find("Casa do Protagonista - Sitio do Recomeco");
        if(home==null)throw new System.InvalidOperationException("Abra ChickenHeistRuralWorld primeiro.");
        var scene=SceneManager.GetActiveScene();Directory.CreateDirectory(Output);
        EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BeforeHomeInterior.unity"),true);
        var old=home.transform.Find(InteriorName);if(old!=null)Object.DestroyImmediate(old.gameObject);
        var previousHouse=home.transform.Find("Casa de madeira envelhecida");
        if(previousHouse!=null)Object.DestroyImmediate(previousHouse.gameObject);
        var house=Place("House",new Vector3(-3,0,3),new Vector3(10,7,9),home.transform);
        house.name="Casa de madeira envelhecida";
        house.transform.RotateAround(ProceduralFarmGenerator.VisualBounds(house).center,Vector3.up,180);
        Vector3 center=home.transform.TransformPoint(new Vector3(-3,0,3));
        house.transform.position=center+(house.transform.position-center)*1.25f;house.transform.localScale*=1.25f;
        foreach(var mf in house.GetComponentsInChildren<MeshFilter>())
        {
            mf.sharedMesh=HomeMeshOpening.Subtract(mf,home.transform,new Bounds(new Vector3(-3.22f,1.72f,2.28f),new Vector3(1.50f,2.34f,.65f)));
            var mc=mf.GetComponent<MeshCollider>();if(mc!=null)mc.sharedMesh=mf.sharedMesh;
        }
        var interior=new GameObject(InteriorName);interior.transform.SetParent(home.transform,false);var root=interior.transform;
        var wood=WornMaterial("Madeira lavada pelo tempo",new Color(.30f,.27f,.23f));
        var trim=WornMaterial("Rodape verde descascado",new Color(.20f,.28f,.24f));
        var plaster=WornMaterial("Reboco antigo",new Color(.62f,.64f,.57f));
        var cream=WornMaterial("Papel guardado",new Color(.78f,.77f,.66f));
        var metal=WornMaterial("Ferro do fogao",new Color(.10f,.115f,.12f));
        var cloth=WornMaterial("Tecido vermelho desbotado",new Color(.37f,.15f,.15f));
        Part(root,"Base do assoalho",new Vector3(-3.22f,.56f,4.56f),new Vector3(8.45f,.1f,4.65f),wood);
        for(int i=0;i<24;i++)
        {
            var grain=WornMaterial("Tabua usada "+i,Color.Lerp(new Color(.30f,.27f,.22f),new Color(.44f,.39f,.30f),(i*7%11)/11f));
            Part(root,"Assoalho de tabuas",new Vector3(-7.27f+i*.352f,.616f,4.57f),new Vector3(.346f,.016f,4.59f),grain);
        }
        Part(root,"Forro baixo",new Vector3(-3.22f,3.30f,4.57f),new Vector3(8.5f,.12f,4.68f),plaster);
        Wall(root,new Vector3(-7.48f,1.92f,4.57f),new Vector3(.12f,2.65f,4.68f),plaster,trim);
        Wall(root,new Vector3(1.02f,1.92f,4.57f),new Vector3(.12f,2.65f,4.68f),plaster,trim);
        Wall(root,new Vector3(-3.22f,1.92f,6.88f),new Vector3(8.5f,2.65f,.12f),plaster,trim);
        Wall(root,new Vector3(-5.74f,1.92f,2.37f),new Vector3(3.5f,2.65f,.10f),plaster,trim);
        Wall(root,new Vector3(-.69f,1.92f,2.37f),new Vector3(3.45f,2.65f,.10f),plaster,trim);
        Part(root,"Verga da entrada",new Vector3(-3.22f,3.08f,2.31f),new Vector3(1.52f,.35f,.16f),wood);
        for(int i=0;i<4;i++)Part(root,"Viga exposta",new Vector3(-6.6f+i*2.2f,3.19f,4.58f),new Vector3(.13f,.15f,4.6f),wood);
        Part(root,"Divisoria do canto de dormir",new Vector3(-1.65f,1.9f,5.91f),new Vector3(.10f,2.6f,1.8f),plaster);
        Part(root,"Remendo no reboco",new Vector3(-4.7f,1.15f,6.809f),new Vector3(.8f,.5f,.015f),wood);
        Furniture("Prop_Bed_01",new Vector3(-.35f,.63f,5.58f),new Vector3(1.35f,1.05f,2.1f),root,0);
        Furniture("Prop_Cupboard_01",new Vector3(.32f,.63f,3.49f),new Vector3(1.0f,1.7f,.62f),root,90);
        var table=Furniture("Prop_Wooden_Table_02",new Vector3(-5.6f,.63f,3.7f),new Vector3(1.6f,.78f,1.05f),root,0);
        Furniture("Prop_Wooden_Chair_01",new Vector3(-4.33f,.63f,3.7f),new Vector3(.6f,1.05f,.65f),root,90);
        Furniture("Prop_Shelf_01",new Vector3(-6.78f,.63f,5.9f),new Vector3(.9f,1.7f,.5f),root,90);
        Furniture("Prop_Cupboard_02",new Vector3(-5.6f,.63f,6.40f),new Vector3(1.5f,.91f,.62f),root,0);
        var chair=Place("RockingChair",new Vector3(-2.35f,.63f,6.12f),new Vector3(.8f,1.1f,.85f),root);
        chair.name="A mesma cadeira, tantos anos depois";
        Place("Bucket",new Vector3(-6.7f,.63f,4.55f),new Vector3(.34f,.37f,.34f),root).name="Balde sob a goteira";
        Part(root,"Fogao antigo",new Vector3(-3.98f,1.08f,6.38f),new Vector3(.8f,.9f,.65f),metal);
        Part(root,"Porta do forno",new Vector3(-3.98f,1.06f,6.043f),new Vector3(.58f,.44f,.018f),wood);
        Part(root,"Puxador do forno",new Vector3(-3.98f,1.23f,6.0f),new Vector3(.40f,.035f,.06f),metal);
        for(int i=0;i<2;i++)
        {
            var hob=GameObject.CreatePrimitive(PrimitiveType.Cylinder);hob.name="Boca do fogao";hob.transform.SetParent(root,false);
            hob.transform.localPosition=new Vector3(-4.2f+i*.40f,1.545f,6.38f);hob.transform.localScale=new Vector3(.27f,.018f,.27f);hob.GetComponent<Renderer>().sharedMaterial=wood;
        }
        Part(root,"Pano de prato remendado",new Vector3(-5.50f,1.21f,6.065f),new Vector3(.30f,.45f,.018f),cloth);
        Part(root,"Tapete puido",new Vector3(-3.18f,.638f,3.12f),new Vector3(1.4f,.01f,1.0f),cloth);
        float deskY=ProceduralFarmGenerator.VisualBounds(table).max.y-home.transform.position.y+.018f;
        for(int i=0;i<3;i++)
        {
            var paper=Part(root,"Conta atrasada",new Vector3(-5.82f+i*.18f,deskY+i*.004f,3.65f),new Vector3(.22f,.003f,.31f),cream);
            paper.transform.localRotation=Quaternion.Euler(0,i*12-9,0);
            for(int line=0;line<4;line++)Part(root,"Linha da cobranca",new Vector3(-5.70f+i*.1f,deskY+.017f,3.56f+line*.037f),new Vector3(.13f,.002f,.006f),line==0?cloth:wood);
        }
        var phone=Part(root,"Celular antigo - tela rachada",new Vector3(-5.22f,deskY+.018f,3.7f),new Vector3(.12f,.023f,.23f),metal);
        phone.AddComponent<HomePhoneDock>();
        var glow=WornMaterial("Tela do celular",new Color(.26f,.43f,.42f));glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(.11f,.24f,.22f));
        Part(root,"Tela",new Vector3(-5.22f,deskY+.031f,3.7f),new Vector3(.102f,.003f,.191f),glow);
        Beam(root,"Vidro rachado",new Vector3(-5.26f,deskY+.034f,3.67f),new Vector3(-5.19f,deskY+.034f,3.77f),.002f,cream);
        Part(root,"Quadro - antes das dividas",new Vector3(-2.8f,2.2f,6.80f),new Vector3(.55f,.42f,.04f),wood);
        var photoMaterial=WornMaterial("Lembranca do sitio",new Color(.45f,.54f,.39f));
        Part(root,"Retrato guardado",new Vector3(-2.8f,2.2f,6.772f),new Vector3(.46f,.33f,.012f),photoMaterial);
        string memoryPath="Assets/ChickenHeistGenerated/PlayerHome/lembranca-do-sitio.png";
        if(!File.Exists(memoryPath))File.Copy(Output+"/home-front.png",memoryPath);
        AssetDatabase.ImportAsset(memoryPath);photoMaterial.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(memoryPath));
        for(int side=0;side<2;side++)
        {
            float x=side==0?-5.75f:-.65f;
            Part(root,"Moldura interna da janela",new Vector3(x,1.85f,2.465f),new Vector3(1.15f,1.18f,.085f),wood);
            for(int i=0;i<8;i++)Part(root,"Veneziana fechada",new Vector3(x,1.37f+i*.135f,2.518f),new Vector3(1.0f,.122f,.025f),trim);
            Part(root,"Peitoril gasto",new Vector3(x,1.22f,2.53f),new Vector3(1.24f,.065f,.24f),wood);
        }
        var ceramic=WornMaterial("Ceramica lascada",new Color(.49f,.60f,.59f));
        var pot=GameObject.CreatePrimitive(PrimitiveType.Cylinder);pot.name="Panela da ultima refeicao";pot.transform.SetParent(root,false);
        pot.transform.localPosition=new Vector3(-4.2f,1.63f,6.38f);pot.transform.localScale=new Vector3(.23f,.09f,.23f);pot.GetComponent<Renderer>().sharedMaterial=ceramic;
        Part(root,"Tampa da panela",new Vector3(-4.2f,1.724f,6.38f),new Vector3(.235f,.023f,.235f),metal);
        Part(root,"Alca da panela",new Vector3(-4.2f,1.75f,6.38f),new Vector3(.08f,.035f,.035f),wood);
        var pillow=Part(root,"Travesseiro antigo",new Vector3(-.35f,1.27f,6.15f),new Vector3(.76f,.12f,.36f),cream);
        for(int i=0;i<3;i++)Part(root,"Costura do cobertor",new Vector3(-.45f+i*.09f,1.243f,5.25f),new Vector3(.025f,.012f,.13f),cream);
        Part(root,"Saco de mantimentos dobrado",new Vector3(-6.75f,1.15f,5.9f),new Vector3(.24f,.21f,.20f),cream);
        // Small wall patches and uneven seams suggest maintenance deferred, not random debris.
        for(int i=0;i<5;i++)Part(root,"Marca de umidade",new Vector3(-7.41f,.80f+i*.12f,5.15f),new Vector3(.015f,.085f,.65f-i*.07f),wood);
        BuildHomeDoor(root,wood,metal);
        HomeLamp(root,new Vector3(-4.8f,2.82f,4.4f),new Color(1,.82f,.60f),3.8f);
        HomeLamp(root,new Vector3(-.4f,2.75f,5.4f),new Color(.79f,.85f,1),1.8f);
        var porch=home.transform.Find("Lampada fraca da varanda");if(porch!=null)porch.localPosition=new Vector3(-3.2f,2.82f,.65f);
        var manager=Object.FindFirstObjectByType<HeistGameManager>();
        if(Camera.main!=null && Camera.main.transform.IsChildOf(manager.player))Camera.main.transform.localPosition=new Vector3(0,1.65f,0);
        var economy=manager.GetComponent<HouseholdEconomy>()??manager.gameObject.AddComponent<HouseholdEconomy>();economy.home=home.transform;
        var mobile=manager.GetComponent<ProtagonistPhone>()??manager.gameObject.AddComponent<ProtagonistPhone>();mobile.economy=economy;
        BuildPhonePhotos(mobile);
        var coop=home.transform.Find("Galinheiro gasto - Ultimo Recurso");
        var stages=home.transform.Find("Reparos comprados");if(stages!=null)Object.DestroyImmediate(stages.gameObject);
        var repairRoot=new GameObject("Reparos comprados");repairRoot.transform.SetParent(home.transform,false);
        economy.repairStages=new GameObject[3];
        for(int i=0;i<3;i++)
        {
            economy.repairStages[i]=Part(repairRoot.transform,"Reparo "+(i+1),coop.localPosition+new Vector3(-.8f+i*.7f,1.20f,.17f),new Vector3(.18f,.95f,.065f),wood);
            economy.repairStages[i].SetActive(false);
        }
        ArrangeFurnitureLayout(root);
        BuildHomeFixtures(root);
        RuralWorldReview.PersistGeneratedAssets(scene);
        PrefabUtility.SaveAsPrefabAsset(home,"Assets/ChickenHeistGenerated/PlayerHome/ProtagonistHome.prefab");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        CaptureInterior();
        Debug.Log("HOME INTERIOR: scene and phone saved.");
    }
    static void Wall(Transform root,Vector3 p,Vector3 size,Material wall,Material trim)
    {
        Part(root,"Parede interna",p,size,wall);
        Part(root,"Rodape desgastado",new Vector3(p.x,.75f,p.z),new Vector3(size.x+.018f,.28f,size.z+.018f),trim);
    }
    static GameObject Furniture(string name,Vector3 position,Vector3 limits,Transform parent,float yaw)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(FurniturePath+name+".fbx");
        if(source==null)throw new System.InvalidOperationException("Missing furniture: "+name);
        var obj=Object.Instantiate(source,parent);obj.name=name;
        obj.transform.RotateAround(ProceduralFarmGenerator.VisualBounds(obj).center,Vector3.up,yaw);
        Bounds b=ProceduralFarmGenerator.VisualBounds(obj);
        obj.transform.localScale*=Mathf.Min(limits.x/b.size.x,limits.y/b.size.y,limits.z/b.size.z);
        b=ProceduralFarmGenerator.VisualBounds(obj);Vector3 p=parent.TransformPoint(position);
        obj.transform.position+=new Vector3(p.x-b.center.x,p.y-b.min.y,p.z-b.center.z);
        foreach(var r in obj.GetComponentsInChildren<Renderer>())
        {
            var mats=r.sharedMaterials;
            for(int i=0;i<mats.Length;i++)
            {
                var m=mats[i];var replacement=WornMaterial(name+" - usado",m!=null && m.HasProperty("_Color")?m.GetColor("_Color"):Color.white);
                Texture tex=m!=null && m.HasProperty("_MainTex")?m.GetTexture("_MainTex"):null;
                if(tex==null && m!=null && m.HasProperty("_BaseMap"))tex=m.GetTexture("_BaseMap");
                replacement.SetTexture("_BaseMap",tex);mats[i]=replacement;
            }
            r.sharedMaterials=mats;
        }
        var collider=obj.AddComponent<BoxCollider>();b=ProceduralFarmGenerator.VisualBounds(obj);
        collider.center=obj.transform.InverseTransformPoint(b.center);
        collider.size=new Vector3(b.size.x/Mathf.Abs(obj.transform.lossyScale.x),b.size.y/Mathf.Abs(obj.transform.lossyScale.y),b.size.z/Mathf.Abs(obj.transform.lossyScale.z));
        // Imported models can have rotated axes; keep collision in an unrotated bounds holder.
        Object.DestroyImmediate(collider);var boundsObject=new GameObject("Colisao do movel");boundsObject.transform.SetParent(parent,false);boundsObject.transform.position=b.center;
        boundsObject.AddComponent<BoxCollider>().size=b.size;boundsObject.transform.SetParent(obj.transform,true);
        return obj;
    }
    public static void PreviewFurniture()
    {
        foreach(string asset in new[]{"Prop_Wooden_Table_02","Prop_Wooden_Table_03"})
        {
            var root=new GameObject("Furniture review");root.transform.position=new Vector3(3000,0,3000);
            try {Furniture(asset,Vector3.zero,new Vector3(1.6f,.78f,1.05f),root.transform,0);Capture(root,Output+"/"+asset+".png",new Vector3(2,1.5f,-2),Vector3.up*.35f,55);}
            finally{Object.DestroyImmediate(root);}
        }
        HomeInteriorTests.Run();
    }
    public static void FinishInterior()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var home=GameObject.Find("Casa do Protagonista - Sitio do Recomeco");
        var root=home.transform.Find(InteriorName);var scene=SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BeforeInteriorPolish.unity"),true);
        var oldTable=root.Find("Prop_Wooden_Table_01");
        if(oldTable!=null)
        {
            float top=ProceduralFarmGenerator.VisualBounds(oldTable.gameObject).max.y;
            Object.DestroyImmediate(oldTable.gameObject);
            var table=Furniture("Prop_Wooden_Table_02",new Vector3(-5.6f,.63f,3.7f),new Vector3(1.6f,.78f,1.05f),root,0);
            float delta=ProceduralFarmGenerator.VisualBounds(table).max.y-top;
            foreach(Transform item in root)
                if(item.name=="Conta atrasada" || item.name=="Linha da cobranca" || item.name=="Celular antigo - tela rachada" || item.name=="Tela" || item.name=="Vidro rachado")item.localPosition+=Vector3.up*delta;
        }
        var player=GameObject.FindGameObjectWithTag("Player");
        foreach(string path in Directory.GetFiles("Assets/ChickenHeistGenerated/PlayerHome/Recon","*.png"))
        {
            var importer=(TextureImporter)AssetImporter.GetAtPath(path.Replace('\\','/'));
            if(importer.npotScale!=TextureImporterNPOTScale.None){importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();}
        }
        if(Camera.main!=null && Camera.main.transform.IsChildOf(player.transform))Camera.main.transform.localPosition=new Vector3(0,1.65f,0);
        RuralWorldReview.PersistGeneratedAssets(scene);
        PrefabUtility.SaveAsPrefabAsset(home,"Assets/ChickenHeistGenerated/PlayerHome/ProtagonistHome.prefab");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        CaptureInterior();HomeInteriorTests.ProbePassage();HomeInteriorTests.Run();
    }
    static void HomeLamp(Transform parent,Vector3 p,Color color,float intensity)
    {
        var lamp=new GameObject("Luz de casa");lamp.transform.SetParent(parent,false);lamp.transform.localPosition=p;
        var light=lamp.AddComponent<Light>();light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=6;light.shadows=LightShadows.Soft;
        var shade=WornMaterial("Luminaria esmaltada",new Color(.23f,.32f,.27f));
        Part(parent,"Fio da lampada",new Vector3(p.x,3.02f,p.z),new Vector3(.013f,.37f,.013f),shade);
        Part(parent,"Cupula",p+Vector3.up*.10f,new Vector3(.32f,.10f,.32f),shade);
    }
    static void BuildHomeDoor(Transform root,Material wood,Material metal)
    {
        var doorObject=new GameObject("Porta de casa - E");doorObject.transform.SetParent(root,false);doorObject.transform.localPosition=new Vector3(-3.22f,.61f,2.28f);
        var hinge=new GameObject("Dobradica");hinge.transform.SetParent(doorObject.transform,false);hinge.transform.localPosition=new Vector3(-.74f,0,0);
        var slab=Part(hinge.transform,"Porta de madeira",new Vector3(.74f,1.12f,0),new Vector3(1.46f,2.24f,.075f),wood);
        for(int i=0;i<5;i++)Part(hinge.transform,"Junta da tabua",new Vector3(.11f+i*.29f,1.12f,-.042f),new Vector3(.012f,2.17f,.009f),metal);
        Part(hinge.transform,"Macaneta",new Vector3(1.30f,1.06f,-.095f),new Vector3(.12f,.04f,.12f),metal);
        var door=doorObject.AddComponent<HomeDoor>();door.hinge=hinge.transform;door.doorCollider=slab.GetComponent<Collider>();
        foreach(var col in hinge.GetComponentsInChildren<Collider>())if(col!=door.doorCollider)Object.DestroyImmediate(col);
    }
    static void BuildPhonePhotos(ProtagonistPhone phone)
    {
        var farms=Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None);
        System.Array.Sort(farms,(a,b)=>string.CompareOrdinal(a.identity,b.identity));
        string folder="Assets/ChickenHeistGenerated/PlayerHome/Recon";Directory.CreateDirectory(folder);
        phone.farmPhotos=new Texture2D[farms.Length];phone.farmNames=new string[farms.Length];phone.farmPositions=new Vector3[farms.Length];
        for(int i=0;i<farms.Length;i++)
        {
            string path=folder+"/farm-"+i.ToString("00")+".png";
            Vector3 gate=farms[i].entrance;Vector3 middle=new Vector3(farms[i].lot.center.x,gate.y,farms[i].lot.center.y);
            Vector3 outward=(gate-middle).normalized;if(outward.sqrMagnitude<.1f)outward=Vector3.back;
            Vector3 eye=gate+outward*22+Vector3.up*15;
            Capture(farms[i].gameObject,path,eye-farms[i].transform.position,middle-farms[i].transform.position+Vector3.up*2);
            AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.maxTextureSize=1024;importer.mipmapEnabled=false;importer.npotScale=TextureImporterNPOTScale.None;importer.SaveAndReimport();
            phone.farmPhotos[i]=AssetDatabase.LoadAssetAtPath<Texture2D>(path);phone.farmNames[i]=farms[i].identity;phone.farmPositions[i]=gate;
        }
    }
    [MenuItem("Chicken Heist/Review Protagonist Interior")]
    public static void CaptureInterior()
    {
        var home=GameObject.Find("Casa do Protagonista - Sitio do Recomeco");
        var door=home.GetComponentInChildren<HomeDoor>();var old=door.hinge.localRotation;door.hinge.localRotation=Quaternion.Euler(0,100,0);
        try
        {
            Capture(home,Output+"/interior-kitchen.png",new Vector3(-2.8f,2.15f,3.9f),new Vector3(-5.5f,1.5f,5.4f),78);
            Capture(home,Output+"/interior-desk.png",new Vector3(-3.65f,1.95f,4.9f),new Vector3(-5.55f,1.3f,3.6f),62);
            Capture(home,Output+"/interior-bedroom.png",new Vector3(-2.4f,2.05f,3.3f),new Vector3(-.05f,1.3f,5.8f),70);
            Capture(home,Output+"/interior-door.png",new Vector3(-3.22f,2.15f,-1.3f),new Vector3(-3.22f,1.65f,4.6f),60);
        }
        finally{door.hinge.localRotation=old;}
    }
    [MenuItem("Chicken Heist/Inspect Protagonist Interior")]
    public static void InspectInterior()
    {
        var root=GameObject.Find("Casa do Protagonista - Sitio do Recomeco");
        var house=root.transform.Find("Casa de madeira envelhecida");
        var lines=new List<string>();
        lines.Add("House local position="+house.localPosition+" bounds="+ProceduralFarmGenerator.VisualBounds(house.gameObject));
        foreach(var mf in house.GetComponentsInChildren<MeshFilter>())
        {
            var xyz=new List<string>();
            foreach(var vertex in mf.sharedMesh.vertices) {
                var p=root.transform.InverseTransformPoint(mf.transform.TransformPoint(vertex));
                xyz.Add(p.x.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+","+p.y.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)+","+p.z.ToString("F3",System.Globalization.CultureInfo.InvariantCulture));
            }
            File.WriteAllLines(Output+"/house-vertices.csv",xyz);
            lines.Add(mf.name+" | vertices="+mf.sharedMesh.vertexCount+" | bounds="+ProceduralFarmGenerator.VisualBounds(mf.gameObject));
            var counts=new SortedDictionary<float,int>();
            foreach(var v in mf.sharedMesh.vertices)
            {
                float z=Mathf.Round(root.transform.InverseTransformPoint(mf.transform.TransformPoint(v)).z*10)/10;
                if(!counts.ContainsKey(z))counts[z]=0;counts[z]++;
            }
            foreach(var pair in counts) if(pair.Value>15)lines.Add("Z plane="+pair.Key+" vertices="+pair.Value);
        }
        foreach(var collider in house.GetComponentsInChildren<Collider>())lines.Add("Collider="+collider.name+" "+collider.GetType().Name+" "+collider.bounds);
        Directory.CreateDirectory(Output);File.WriteAllLines(Output+"/interior-inspect.txt",lines);
        Capture(root,Output+"/door-before.png",new Vector3(-3,1.9f,-7),new Vector3(-3,1.6f,3));
        Capture(root,Output+"/inside-before.png",new Vector3(-3,2,2),new Vector3(-2,1.7f,5));
    }
}
