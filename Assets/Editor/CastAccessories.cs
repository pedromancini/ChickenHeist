using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Hats that break up the repeated Medieval People silhouettes (Tools/SourceArt/build_cast_accessories.py).
// Each hat uses the wearer's own atlas material, is measured onto the top of that character's skull in
// the idle pose and follows the Head bone.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod CastAccessories.Apply
public static class CastAccessories
{
    const string Source="Assets/ChickenHeistGenerated/Characters/Villagers/CastAccessories.fbx";
    const string Tag="Acessorio - ";
    // model -> hat (null: none). Farmers by type; residents by name.
    static readonly Dictionary<string,string> ByModel=new Dictionary<string,string>{{"peasant_5","StrawHat"},{"rich_citizzens_1","Cap"}};
    static readonly Dictionary<string,string> Residents=new Dictionary<string,string>{{"Morador 1","StrawHat"},{"Morador 2","Cap"},{"Morador 4","StrawHat"},{"Morador 9","Cap"}};
    public static void Apply()
    {
        var log=new List<string>();
        var scene=EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        foreach(var farmer in Object.FindObjectsByType<FarmerSleepSystem>(FindObjectsSortMode.None))
        {
            var visual=farmer.transform.Find("Visual Villager NPC");if(visual==null)continue;
            string model=visual.GetChild(0).name;if(ByModel.TryGetValue(model,out var hat))log.Add(farmer.name+": "+Wear(visual,hat));
        }
        foreach(var walker in Object.FindObjectsByType<RoadsideWalker>(FindObjectsSortMode.None))
            if(Residents.TryGetValue(walker.name,out var hat))log.Add(walker.name+": "+Wear(walker.GetComponentInChildren<RuralCharacterAnimator>().transform,hat));
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        const string decline="Assets/Resources/Cinematics/DeclineStage.prefab";
        var root=PrefabUtility.LoadPrefabContents(decline);
        try{var stage=root.GetComponent<DeclineCinematicStage>();log.Add("Osvaldo: "+Wear(stage.osvaldo,"StrawHat"));PrefabUtility.SaveAsPrefabAsset(root,decline);}
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();Directory.CreateDirectory("output/uniform-cast");File.WriteAllLines("output/uniform-cast/accessories.txt",log);
    }
    static string Wear(Transform character,string hatName)
    {
        var head=character.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Head");
        foreach(var old in head.Cast<Transform>().Where(t=>t.name.StartsWith(Tag)).ToArray())Object.DestroyImmediate(old.gameObject);
        var animation=character.GetComponent<Animation>();var idle=animation!=null?animation.GetClip("Idle"):null;
        if(idle!=null)idle.SampleAnimation(character.gameObject,0);
        var skin=character.GetComponentsInChildren<SkinnedMeshRenderer>().First();
        int headIndex=System.Array.IndexOf(skin.bones,head);
        var baked=new Mesh();skin.BakeMesh(baked,true);var weights=skin.sharedMesh.boneWeights;var verts=baked.vertices;
        var points=Enumerable.Range(0,verts.Length).Where(i=>weights[i].boneIndex0==headIndex && weights[i].weight0>.5f)
            .Select(i=>character.InverseTransformPoint(skin.transform.TransformPoint(verts[i]))).ToList();
        Object.DestroyImmediate(baked);
        if(points.Count==0)return "no head vertices";
        float top=points.Max(p=>p.y);var crown=points.Where(p=>p.y>top-.06f).ToList();
        var center=new Vector3(crown.Average(p=>p.x),top,crown.Average(p=>p.z));
        // Keep the importer's unit scale and axis rotation of the Blender object.
        var part=AssetDatabase.LoadAssetAtPath<GameObject>(Source).transform.Find(hatName);
        var go=Object.Instantiate(part.gameObject);go.name=Tag+(hatName=="Cap"?"bone":"chapeu de palha");
        go.GetComponent<MeshRenderer>().sharedMaterial=skin.sharedMaterial;
        // A hat sits down over the skull: the crown top rests a little below the highest scalp vertex.
        go.transform.SetPositionAndRotation(character.TransformPoint(center-Vector3.up*(hatName=="Cap"?.035f:.045f)),character.rotation*part.localRotation);
        go.transform.localScale=part.localScale;
        go.transform.SetParent(head,true);
        return hatName+" at "+center.ToString("0.00");
    }
}
