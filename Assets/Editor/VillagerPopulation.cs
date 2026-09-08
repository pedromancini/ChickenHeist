using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class VillagerPopulation
{
    [MenuItem("Chicken Heist/Apply Villagers and Populate Roads")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=EditorSceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BeforeVillagerPopulation.unity"),true);
        int index=0;
        foreach(var farmer in Object.FindObjectsByType<FarmerSleepSystem>(FindObjectsSortMode.None))
        {
            var old=farmer.transform.Find("Visual Villager NPC");if(old!=null)Object.DestroyImmediate(old.gameObject);
            foreach(var renderer in farmer.GetComponentsInChildren<Renderer>())renderer.enabled=false;
            var visual=VillagerNPCFactory.Create(farmer.transform,"Visual Villager NPC",index++%2==0?"Hunter":"Blacksmith");
            visual.GetComponent<RuralCharacterAnimator>().farmer=farmer;
        }
        var market=Object.FindFirstObjectByType<VillageMarket>();
        Vector3 localPosition=market.merchant.transform.localPosition;Quaternion localRotation=market.merchant.transform.localRotation;
        Object.DestroyImmediate(market.merchant.gameObject);
        var merchant=VillagerNPCFactory.Create(market.transform,"Seu Anselmo - Comerciante","Blacksmith");
        merchant.transform.localPosition=localPosition;merchant.transform.localRotation=localRotation;
        var capsule=merchant.AddComponent<CapsuleCollider>();capsule.height=1.8f;capsule.radius=.25f;capsule.center=Vector3.up*.9f;
        market.merchant=merchant.GetComponent<RuralCharacterAnimator>();
        var oldCrowd=GameObject.Find("Moradores das estradas");if(oldCrowd!=null)Object.DestroyImmediate(oldCrowd);
        var crowd=new GameObject("Moradores das estradas");
        var surfaces=RuralTreeRoots.TerrainSurfaces();var used=new List<Vector3>();
        string[] models={"peasant_1","Hunter","peasant_2","Blacksmith","peasant_3","peasant_4","peasant_5","peasant_6","city_dwellers_1","city_dwellers_2"};
        var home=HouseholdEconomy.Instance!=null?HouseholdEconomy.Instance.home:GameObject.Find("Casa do Protagonista - Sitio do Recomeco").transform;
        var spans=Object.FindObjectsByType<RuralRoadSpan>(FindObjectsSortMode.None).Where(s=>s.GetComponentInParent<FarmLayoutInfo>()==null && s.GetComponentInParent<VillageMarket>()==null && s.width>=2.5f).OrderBy(s=>(s.start-home.position).sqrMagnitude);
        foreach(var span in spans)
        {
            Vector3 a=span.start,b=span.end;float length=Vector3.Distance(a,b);if(length<24)continue;
            for(float center=.2f;center<.85f && used.Count<models.Length;center+=.25f)
            {
                Vector3 mid=Vector3.Lerp(a,b,center);
                if(used.Any(p=>(p-mid).sqrMagnitude<1600) || (mid-market.transform.position).sqrMagnitude<3600)continue;
                float half=Mathf.Min(.12f,12/length);var route=new Vector3[4];bool valid=true;
                for(int j=0;j<4;j++)
                {
                    Vector3 p=Vector3.Lerp(a,b,center-half+j*half*2/3);
                    if(!RuralTreeRoots.SurfaceHeight(surfaces,p,out float y)){valid=false;break;}
                    route[j]=new Vector3(p.x,y+.06f,p.z);
                }
                if(!valid || !ClearRoute(route,surfaces))continue;
                var person=new GameObject("Morador "+(used.Count+1));person.transform.SetParent(crowd.transform,false);person.transform.position=route[0];
                var cc=person.AddComponent<CharacterController>();cc.height=1.8f;cc.radius=.28f;cc.center=Vector3.up*.9f;cc.stepOffset=.3f;
                var walker=person.AddComponent<RoadsideWalker>();walker.route=route;walker.speed=1.1f+(used.Count%4)*.12f;
                var visual=VillagerNPCFactory.Create(person.transform,"Visual "+models[used.Count],models[used.Count]);visual.GetComponent<RuralCharacterAnimator>().walker=walker;
                used.Add(mid);
            }
            if(used.Count==models.Length)break;
        }
        if(used.Count<models.Length)throw new System.InvalidOperationException("Only "+used.Count+" safe street routes found; review before releasing.");
        RuralWorldReview.PersistGeneratedAssets(scene);
        PrefabUtility.SaveAsPrefabAsset(market.gameObject,"Assets/ChickenHeistGenerated/PlayerHome/VendaDoVale.prefab");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        PlayerHomeBuilder.CaptureVillagerPopulation();
    }
    static bool ClearRoute(Vector3[] route,Collider[] terrain)
    {
        Physics.SyncTransforms();
        for(int i=1;i<route.Length;i++)
        {
            int count=Mathf.CeilToInt(Vector3.Distance(route[i-1],route[i]));float lastY=route[i-1].y;
            for(int j=0;j<=count;j++)
            {
                Vector3 p=Vector3.Lerp(route[i-1],route[i],j/(float)count);
                if(!RuralTreeRoots.SurfaceHeight(terrain,p,out float y) || Mathf.Abs(y-lastY)>.35f)return false;
                lastY=y;p.y=y+.06f;
                foreach(var hit in Physics.OverlapCapsule(p+Vector3.up*.35f,p+Vector3.up*1.45f,.4f,~0,QueryTriggerInteraction.Ignore))if(!terrain.Contains(hit))return false;
            }
        }
        return true;
    }
}
