using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Diagnostic: pelvis height of the previous and native Elias clips (sitting/crouching retarget check).
public static class EliasPelvisProbe
{
    public static void Run()
    {
        var lines=new System.Collections.Generic.List<string>();
        foreach(var (label,modelPath,clipFolder) in new[]{("old","Assets/ChickenHeistGenerated/Characters/ProtagonistV2/Protagonist_Rigged.fbx","Assets/ChickenHeistGenerated/Characters/ProtagonistV2/"),("new","Assets/ChickenHeistGenerated/Characters/EliasNative/Elias_Rigged.fbx","Assets/ChickenHeistGenerated/Characters/EliasNative/")})
        {
            var holder=new GameObject("probe");var model=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath),holder.transform);model.name="Protagonist_Rigged(Clone)";
            foreach(var a in model.GetComponentsInChildren<Animation>())Object.DestroyImmediate(a);foreach(var a in model.GetComponentsInChildren<Animator>())Object.DestroyImmediate(a);
            var hips=model.GetComponentsInChildren<Transform>().First(t=>t.name=="Hips");var foot=model.GetComponentsInChildren<Transform>().First(t=>t.name=="FootL");
            foreach(var state in new[]{"Idle","Drive","CrouchIdle","Carry"})
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipFolder+state+".anim");if(clip==null){lines.Add(label+" "+state+" missing");continue;}
                clip.SampleAnimation(holder,0);
                var curves=AnimationUtility.GetCurveBindings(clip).Where(b=>b.propertyName.StartsWith("m_LocalPosition")).Select(b=>b.path.Split('/').Last()+"."+b.propertyName).Distinct().Take(6);
                lines.Add($"{label} {state}: hips {holder.transform.InverseTransformPoint(hips.position).y:0.00} foot {holder.transform.InverseTransformPoint(foot.position).y:0.00} posCurves [{string.Join(",",curves)}]");
            }
            Object.DestroyImmediate(holder);
        }
        File.WriteAllLines("output/elias-native/pelvis-probe.txt",lines);
    }
}
