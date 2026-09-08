using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OldPickupTruckBuilder
{
    const string Folder="Assets/ChickenHeistGenerated/Truck";
    static Material paint,rust,metal,rubber,seat,light,wood;
    static GameObject Part(Transform root,string name,Vector3 p,Vector3 size,Material mat,PrimitiveType shape=PrimitiveType.Cube)
    {
        var o=GameObject.CreatePrimitive(shape);o.name=name;o.transform.SetParent(root,false);o.transform.localPosition=p;o.transform.localScale=size;
        o.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(o.GetComponent<Collider>());return o;
    }
    static Material Mat(string name,Color color,float metallic=0)
    {
        var path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.color=color;m.SetFloat("_Metallic",metallic);m.SetFloat("_Smoothness",.15f);return m;
    }
    public static void RunBatch()
    {RealisticPickupUpgrade.RunBatch();}
    static void BuildLegacy()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/ChickenHeistGenerated","Truck");
        paint=Mat("Verde desbotado",new Color(.22f,.32f,.27f),.25f);rust=Mat("Ferrugem",new Color(.31f,.12f,.045f));
        metal=Mat("Aco gasto",new Color(.24f,.25f,.23f),.65f);rubber=Mat("Pneus velhos",new Color(.035f,.036f,.03f));
        seat=Mat("Banco rasgado",new Color(.15f,.085f,.042f));light=Mat("Farol amarelado",new Color(.78f,.70f,.43f));wood=Mat("Tabuas remendadas",new Color(.26f,.18f,.085f));
        var existing=Object.FindFirstObjectByType<OldPickupTruck>();if(existing!=null)Object.DestroyImmediate(existing.gameObject);
        var root=new GameObject("Caminhonete velha do protagonista");
        var truck=root.AddComponent<OldPickupTruck>();var cc=root.AddComponent<CharacterController>();cc.center=new Vector3(0,.9f,0);cc.height=1.8f;cc.radius=.78f;cc.slopeLimit=42;cc.stepOffset=.2f;
        Part(root.transform,"Chassi enferrujado",new Vector3(0,.49f,-.1f),new Vector3(1.48f,.18f,3.9f),rust);
        Part(root.transform,"Assoalho da cacamba",new Vector3(0,.88f,-1.05f),new Vector3(1.68f,.1f,1.92f),metal);
        for(int side=-1;side<=1;side+=2)
        {
            Part(root.transform,"Lateral amassada da cacamba",new Vector3(side*.85f,1.08f,-1.05f),new Vector3(.10f,.4f,1.95f),paint);
            Part(root.transform,"Porta remendada",new Vector3(side*.84f,1.03f,.65f),new Vector3(.10f,.68f,1.2f),paint);
            Part(root.transform,"Macaneta",new Vector3(side*.91f,1.28f,.35f),new Vector3(.05f,.04f,.2f),metal);
            var patch=Part(root.transform,"Chapa de ferrugem na porta",new Vector3(side*.899f,.88f,.7f),new Vector3(.006f,.24f,.5f),rust);patch.transform.localRotation=Quaternion.Euler(side*8,0,0);
            Part(root.transform,"Retrovisor torto",new Vector3(side*1.03f,1.60f,1.12f),new Vector3(.2f,.17f,.06f),metal).transform.localRotation=Quaternion.Euler(0,side*12,side*8);
            Part(root.transform,"Coluna traseira",new Vector3(side*.79f,1.72f,.08f),new Vector3(.09f,.77f,.1f),paint);
            Part(root.transform,"Coluna do para-brisa",new Vector3(side*.79f,1.74f,1.10f),new Vector3(.08f,.8f,.08f),paint).transform.localRotation=Quaternion.Euler(-12,0,0);
        }
        Part(root.transform,"Teto com remendo",new Vector3(0,2.11f,.57f),new Vector3(1.73f,.09f,1.22f),paint);
        Part(root.transform,"Remendo de teto",new Vector3(.35f,2.165f,.4f),new Vector3(.55f,.015f,.45f),rust);
        Part(root.transform,"Capo desalinhado",new Vector3(0,1.10f,1.55f),new Vector3(1.68f,.24f,.83f),paint).transform.localRotation=Quaternion.Euler(-4,0,2);
        Part(root.transform,"Painel velho",new Vector3(0,1.41f,1.03f),new Vector3(1.5f,.16f,.2f),rubber);
        Part(root.transform,"Banco gasto",new Vector3(0,.83f,.55f),new Vector3(1.35f,.2f,.65f),seat);
        Part(root.transform,"Encosto rasgado",new Vector3(0,1.15f,.24f),new Vector3(1.35f,.58f,.13f),seat);
        Part(root.transform,"Espuma exposta",new Vector3(-.4f,1.15f,.313f),new Vector3(.24f,.2f,.01f),light);
        Part(root.transform,"Grade frontal",new Vector3(0,.86f,2.01f),new Vector3(1.5f,.27f,.06f),rubber);
        for(int i=0;i<6;i++)Part(root.transform,"Lamina da grade",new Vector3(-.5f+i*.2f,.87f,2.05f),new Vector3(.035f,.23f,.025f),metal);
        for(int side=-1;side<=1;side+=2)
        {
            var lamp=Part(root.transform,side<0?"Farol inteiro":"Farol rachado",new Vector3(side*.65f,1.02f,2.04f),new Vector3(.23f,.05f,.23f),side<0?light:rubber,PrimitiveType.Cylinder);lamp.transform.localRotation=Quaternion.Euler(90,0,0);
            Part(root.transform,"Lanterna traseira",new Vector3(side*.75f,1.03f,-2.06f),new Vector3(.12f,.12f,.04f),rust);
        }
        Part(root.transform,"Para-choque torto",new Vector3(0,.61f,2.1f),new Vector3(1.88f,.13f,.14f),metal).transform.localRotation=Quaternion.Euler(0,0,4);
        Part(root.transform,"Tabua no lugar da tampa",new Vector3(0,1.08f,-2.04f),new Vector3(1.66f,.26f,.075f),wood);
        var wheels=new List<Transform>();
        for(int side=-1;side<=1;side+=2)foreach(float z in new[]{-1.32f,1.33f})
        {
            var tire=Part(root.transform,"Pneu gasto",new Vector3(side*.86f,.40f,z),new Vector3(.77f,.17f,.77f),rubber,PrimitiveType.Cylinder);
            tire.transform.localRotation=Quaternion.Euler(0,0,90);wheels.Add(tire.transform);
            var hub=Part(root.transform,"Roda de aco oxidada",new Vector3(side*1.035f,.40f,z),new Vector3(.41f,.018f,.41f),metal,PrimitiveType.Cylinder);hub.transform.localRotation=Quaternion.Euler(0,0,90);
        }
        truck.wheels=wheels.ToArray();
        var wheel=Part(root.transform,"Volante gasto",new Vector3(-.43f,1.40f,.98f),new Vector3(.34f,.026f,.34f),rubber,PrimitiveType.Cylinder);wheel.transform.localRotation=Quaternion.Euler(65,0,0);truck.steeringWheel=wheel.transform;
        var driver=new GameObject("Posicao do motorista");driver.transform.SetParent(root.transform,false);driver.transform.localPosition=new Vector3(-.4f,.18f,.48f);truck.seat=driver.transform;
        var cargo=new GameObject("Acesso a cacamba");cargo.transform.SetParent(root.transform,false);cargo.transform.localPosition=new Vector3(0,.95f,-2.25f);truck.cargoPoint=cargo.transform;
        truck.cages=new GameObject[4];truck.birds=new GameObject[8];
        var prefab=Object.FindFirstObjectByType<HomeFlockView>().chickenPrefab;
        for(int cage=0;cage<4;cage++)
        {
            var box=new GameObject("Gaiola "+(cage+1)+" - duas galinhas");box.transform.SetParent(root.transform,false);
            box.transform.localPosition=new Vector3(cage%2==0?-.42f:.42f,.96f,cage<2?-.54f:-1.43f);truck.cages[cage]=box;
            Part(box.transform,"Base",Vector3.zero,new Vector3(.76f,.045f,.79f),wood);
            for(int n=0;n<5;n++)for(int side=-1;side<=1;side+=2)
            {
                Part(box.transform,"Barra vertical",new Vector3(-.36f+n*.18f,.32f,side*.38f),new Vector3(.016f,.63f,.016f),metal);
                Part(box.transform,"Barra lateral",new Vector3(side*.36f,.32f,-.38f+n*.19f),new Vector3(.016f,.63f,.016f),metal);
            }
            for(int side=-1;side<=1;side+=2)
            {
                Part(box.transform,"Travessa superior",new Vector3(0,.64f,side*.38f),new Vector3(.75f,.018f,.018f),metal);
                Part(box.transform,"Travessa lateral",new Vector3(side*.36f,.64f,0),new Vector3(.018f,.018f,.78f),metal);
            }
            for(int j=0;j<2;j++)
            {
                var bird=Object.Instantiate(prefab,box.transform);bird.name="Galinha transportada "+(cage*2+j+1);
                foreach(var behavior in bird.GetComponentsInChildren<MonoBehaviour>())Object.DestroyImmediate(behavior);
                foreach(var collider in bird.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
                var bounds=ProceduralFarmGenerator.VisualBounds(bird);bird.transform.localScale*=.32f/bounds.size.y;
                bird.transform.localRotation=Quaternion.Euler(0,j==0?65:-65,0);bounds=ProceduralFarmGenerator.VisualBounds(bird);
                var target=box.transform.TransformPoint(new Vector3(j==0?-.17f:.17f,.04f,0));bird.transform.position+=new Vector3(target.x-bounds.center.x,target.y-bounds.min.y,target.z-bounds.center.z);
                truck.birds[cage*2+j]=bird;
            }
        }
        var home=Object.FindFirstObjectByType<HouseholdEconomy>().home;
        root.transform.position=home.position+new Vector3(-3f,.05f,-8);root.transform.rotation=Quaternion.Euler(0,180,0);
        PrefabUtility.SaveAsPrefabAsset(root,Folder+"/OldPickupTruck.prefab");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        SessionState.SetBool("Protagonist.Batch",true);PlayableVillageTests.Run();
    }
}
