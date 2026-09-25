using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Records the actual rest and sampled rig coordinates before skinning the
// supplied mesh, so binding does not depend on guessed bone proportions.
public static class HoodedVisitorRigReview
{
    public static void Inspect()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Cinematics/VisitorStage.prefab");
        var stage=Object.Instantiate(prefab).GetComponent<VisitorCinematicStage>();
        var actor=stage.visitor;
        actor.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        var report=new StringBuilder();
        Record(report,actor,"prefab rest");
        var animation=actor.GetComponentInChildren<Animation>();
        animation.GetClip("Idle").SampleAnimation(animation.gameObject,0);
        Record(report,actor,"idle zero");
        animation.GetClip("Walk").SampleAnimation(animation.gameObject,.25f);
        Record(report,actor,"walk quarter second");
        Directory.CreateDirectory("output/hooded-rig");
        File.WriteAllText("output/hooded-rig/bone-coordinates.txt",report.ToString());
        Object.DestroyImmediate(stage.gameObject);
    }
    static void Record(StringBuilder report,Transform actor,string label)
    {
        report.AppendLine(label);
        foreach(var bone in actor.GetComponentsInChildren<Transform>(true))
        {
            if(bone.name.Contains("Index") || bone.name.Contains("Middle") || bone.name.Contains("Ring") || bone.name.Contains("Pinky") || bone.name.Contains("Thumb"))continue;
            report.AppendLine(bone.name+" position="+actor.InverseTransformPoint(bone.position).ToString("F4")+" rotation="+(Quaternion.Inverse(actor.rotation)*bone.rotation).ToString("F4"));
        }
    }
}
