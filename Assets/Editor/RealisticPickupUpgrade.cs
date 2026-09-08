using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class RealisticPickupUpgrade
{
    const string Folder="Assets/ChickenHeistGenerated/Truck";
    public static void RunBatch()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        const string path=Folder+"/FarmPickup.fbx";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        importer.bakeAxisConversion=true;importer.globalScale=1;importer.importAnimation=false;importer.SaveAndReimport();
        var truck=Object.FindFirstObjectByType<OldPickupTruck>();
        if(truck==null)throw new Exception("Existing truck with purchased-cage slots required.");
        foreach(Transform child in truck.transform.Cast<Transform>().ToArray())
            if(!truck.cages.Contains(child.gameObject) && child!=truck.seat && child!=truck.cargoPoint)Object.DestroyImmediate(child.gameObject);
        foreach(var collider in truck.GetComponents<Collider>())Object.DestroyImmediate(collider);
        var oldPhysics=truck.GetComponent<PickupVehiclePhysics>();if(oldPhysics!=null)Object.DestroyImmediate(oldPhysics);
        var body=truck.GetComponent<Rigidbody>();if(body==null)body=truck.gameObject.AddComponent<Rigidbody>();
        body.mass=1200;body.linearDamping=.04f;body.angularDamping=.65f;body.interpolation=RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        model.name="Carroceria modelada";model.transform.SetParent(truck.transform,false);
        var imported=model.GetComponentsInChildren<Transform>();
        Vector3 front=imported.Where(t=>t.name=="Wheel_FL" || t.name=="Wheel_FR").Aggregate(Vector3.zero,(sum,t)=>sum+truck.transform.InverseTransformPoint(t.position))*.5f;
        Vector3 rear=imported.Where(t=>t.name=="Wheel_RL" || t.name=="Wheel_RR").Aggregate(Vector3.zero,(sum,t)=>sum+truck.transform.InverseTransformPoint(t.position))*.5f;
        model.transform.localRotation=Quaternion.FromToRotation(front-rear,Vector3.forward)*model.transform.localRotation;
        if(truck.transform.InverseTransformPoint(imported.Single(t=>t.name=="Wheel_FL").position).x>0)
            model.transform.localScale=new Vector3(-model.transform.localScale.x,model.transform.localScale.y,model.transform.localScale.z);
        foreach(var renderer in model.GetComponentsInChildren<Renderer>())
        {
            renderer.sharedMaterials=renderer.sharedMaterials.Select(source=>
            {
                string name=source.name.Split('.')[0];string destination=Folder+"/"+name+".mat";
                var m=AssetDatabase.LoadAssetAtPath<Material>(destination);
                if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,destination);}
                Color color=source.color;
                if(name=="FadedSage")color=new Color(.38f,.46f,.37f);
                if(name=="Rubber")color=new Color(.028f,.030f,.027f);
                m.color=color;m.SetFloat("_Metallic",name=="WornSteel"?.65f:name=="Headlamp"?.2f:.06f);
                m.SetFloat("_Smoothness",name=="WornSteel"?.4f:.22f);
                if(name=="WindowGlass")
                {
                    m.color=new Color(.37f,.49f,.50f,.16f);m.SetFloat("_Surface",1);m.SetFloat("_ZWrite",0);
                    m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_Cull",0);
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.renderQueue=3000;m.SetOverrideTag("RenderType","Transparent");
                }
                EditorUtility.SetDirty(m);return m;
            }).ToArray();
            if(renderer.name=="Glass")renderer.shadowCastingMode=ShadowCastingMode.Off;
        }
        var bounds=ProceduralFarmGenerator.VisualBounds(model);
        if(bounds.size.y<1.5f || bounds.size.y>2.4f || bounds.size.z<4 || bounds.size.z>5.2f)
            throw new Exception("Imported vehicle scale is incorrect: "+bounds);
        Box(truck.transform,"Chassi - colisao",new Vector3(0,.59f,0),new Vector3(1.62f,.26f,4.35f));
        Box(truck.transform,"Cabine - colisao",new Vector3(0,1.26f,.28f),new Vector3(1.56f,1.13f,1.13f));
        Box(truck.transform,"Motor - colisao",new Vector3(0,.92f,1.62f),new Vector3(1.65f,.31f,1.04f));
        Box(truck.transform,"Cacamba - colisao",new Vector3(0,.98f,-1.23f),new Vector3(1.72f,.46f,1.96f));
        var physics=truck.gameObject.AddComponent<PickupVehiclePhysics>();truck.vehicle=physics;
        physics.axles=new WheelCollider[4];physics.wheelModels=new Transform[4];
        string[] names={"Wheel_FL","Wheel_FR","Wheel_RL","Wheel_RR"};
        var transforms=model.GetComponentsInChildren<Transform>();
        for(int i=0;i<4;i++)
        {
            physics.wheelModels[i]=transforms.Single(t=>t.name==names[i]);
            var expected=new Vector3(i%2==0?-.835f:.835f,.38f,i<2?1.40f:-1.25f);
            var measured=truck.transform.InverseTransformPoint(physics.wheelModels[i].position);
            if(Vector3.Distance(expected,measured)>.025f)throw new Exception("Visual wheel alignment mismatch "+names[i]+": "+measured+" expected "+expected);
            var axle=new GameObject(names[i]+" suspension");axle.transform.SetParent(truck.transform,false);
            axle.transform.localPosition=new Vector3(i%2==0?-.835f:.835f,.50f,i<2?1.40f:-1.25f);
            var w=axle.AddComponent<WheelCollider>();w.mass=28;w.radius=.38f;w.suspensionDistance=.24f;
            w.wheelDampingRate=.5f;w.forceAppPointDistance=.18f;
            w.suspensionSpring=new JointSpring{spring=i<2?32000:30000,damper=i<2?3800:3600,targetPosition=.5f};
            w.forwardFriction=new WheelFrictionCurve{extremumSlip=.35f,extremumValue=1,asymptoteSlip=.8f,asymptoteValue=.65f,stiffness=1.2f};
            w.sidewaysFriction=new WheelFrictionCurve{extremumSlip=.22f,extremumValue=1,asymptoteSlip=.55f,asymptoteValue=.75f,stiffness=1.15f};
            physics.axles[i]=w;
        }
        truck.wheels=physics.wheelModels;truck.steeringWheel=transforms.Single(t=>t.name=="Steering");
        truck.seat.localPosition=new Vector3(-.40f,-.10f,0);
        truck.cargoPoint.localPosition=new Vector3(0,.95f,-2.45f);
        for(int i=0;i<4;i++)truck.cages[i].transform.localPosition=new Vector3(i%2==0?-.41f:.41f,.80f,i<2?-.73f:-1.64f);
        var home=Object.FindFirstObjectByType<HouseholdEconomy>().home;
        truck.transform.SetPositionAndRotation(home.position+new Vector3(-3,.10f,-8),Quaternion.Euler(0,180,0));
        PrefabUtility.SaveAsPrefabAsset(truck.gameObject,Folder+"/OldPickupTruck.prefab");
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();SessionState.SetBool("Protagonist.Batch",true);PlayableVillageTests.Run();
    }
    static void Box(Transform parent,string name,Vector3 center,Vector3 size)
    {
        var obj=new GameObject(name);obj.transform.SetParent(parent,false);
        var box=obj.AddComponent<BoxCollider>();box.center=center;box.size=size;
    }
}
