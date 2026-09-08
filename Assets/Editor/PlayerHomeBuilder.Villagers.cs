using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static partial class PlayerHomeBuilder
{
    public static void CaptureVillagerPopulation()
    {
        Directory.CreateDirectory("output/villager-review");
        foreach(var walker in Object.FindObjectsByType<RoadsideWalker>(FindObjectsSortMode.None))
        {
            var driver=walker.GetComponentInChildren<RuralCharacterAnimator>();
            if(!Application.isPlaying)driver.clips["Walk"].clip.SampleAnimation(driver.gameObject,.2f);
            Capture(walker.gameObject,"output/villager-review/"+walker.name+"-walk.png",new Vector3(2,1.4f,3),new Vector3(0,.9f,0),45);
            if(!Application.isPlaying)driver.clips["Idle"].clip.SampleAnimation(driver.gameObject,0);
        }
        var market=Object.FindFirstObjectByType<VillageMarket>();
        Capture(market.merchant.gameObject,"output/villager-review/merchant-final.png",new Vector3(2,1.3f,-2.5f),new Vector3(0,1.05f,0),52);
    }
    public static void InspectVillagers()
    {
        Directory.CreateDirectory("output/villager-review");
        var lines=new List<string>();
        foreach(string name in new[]{"Blacksmith","Hunter","Child","peasant_1","peasant_2","peasant_3","peasant_4","peasant_5","peasant_6","city_dwellers_1","city_dwellers_2"})
        {
            string path="Assets/ImportedVillagerNPC/Villager NPC Free/FBX/Characters/"+name+".fbx";
            if(name.StartsWith("peasant") || name.StartsWith("city"))path="Assets/ImportedMedievalPeople/fbx/people_unity/"+name+".fbx";
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            lines.Add("MODEL "+name);
            foreach(var asset in AssetDatabase.LoadAllAssetsAtPath(path))
                if(asset is AnimationClip clip)lines.Add("CLIP "+clip.name+" duration="+clip.length);
            var instance=Object.Instantiate(source);
            try
            {
                foreach(var t in instance.GetComponentsInChildren<Transform>())lines.Add(AnimationUtility.CalculateTransformPath(t,instance.transform)+" p="+t.localPosition+" r="+t.localEulerAngles);
                foreach(var r in instance.GetComponentsInChildren<SkinnedMeshRenderer>())lines.Add("SKIN "+r.name+" bones="+r.bones.Length+" vertices="+r.sharedMesh.vertexCount);
                FitMarketProp(instance,instance.transform,new Vector3(0,0,0),new Vector3(3,1.8f,3));
                foreach(var r in instance.GetComponentsInChildren<Renderer>())
                {
                    var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(name.StartsWith("peasant") || name.StartsWith("city")?"Assets/ImportedMedievalPeople/texture/people_texture_map.png":"Assets/ImportedVillagerNPC/Villager NPC Free/Texture/Villagers_Texture.png"));
                    r.sharedMaterial=material;
                }
                Capture(instance,"output/villager-review/"+name+".png",new Vector3(2,1.5f,3),new Vector3(0,.9f,0),45);
            }
            finally{Object.DestroyImmediate(instance);}
        }
        File.WriteAllLines("output/villager-review/import-inspection.txt",lines);
    }
}
