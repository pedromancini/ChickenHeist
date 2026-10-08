using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Diagnostic: where key bones sit relative to the first-person camera for the backed-up clip of a state and
// the installed one (EliasNative/Backup/<state>.anim vs EliasNative/<state>.anim), at the clip's first frame.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod PoseCompareProbe.Run
public static class PoseCompareProbe
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChickenHeistGenerated/Characters/ProtagonistV2/Protagonist.prefab"));
        var body=root.GetComponentsInChildren<Animation>(true).First(a=>a.transform.Find("Protagonist_Rigged(Clone)")!=null);
        Transform Bone(string n)=>body.GetComponentsInChildren<Transform>(true).First(t=>t.name==n);
        var eye=new Vector3(0,1.65f,.22f);
        var log=new List<string>{"positions relative to the eye point (x right, y up, z forward), metres"};
        foreach(var state in new[]{"Drive","Lockpick","Carry","CrouchIdle","Pickup"})
        {
            foreach(var (label,path) in new[]{("old",EliasNativeInstall.Folder+"Backup/"+state+".anim"),("new",EliasNativeInstall.Folder+state+".anim")})
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);if(clip==null){log.Add(state+" "+label+": missing");continue;}
                clip.SampleAnimation(body.gameObject,0);
                string P(string n)=>(body.transform.InverseTransformPoint(Bone(n).position)-eye).ToString("F2");
                log.Add($"{state} {label}: hips {P("Hips")} head {P("Head")} shoulderL {P("UpperArmL")} shoulderR {P("UpperArmR")} handL {P("HandL")} handR {P("HandR")}");
            }
        }
        Directory.CreateDirectory("output/mixamo");File.WriteAllLines("output/mixamo/pose-compare.txt",log);
    }
}
