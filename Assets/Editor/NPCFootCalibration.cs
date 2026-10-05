using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Calibrates NPCFootContact heel/toe contact points from each character's skinned mesh (bind pose):
// the lowest vertices under each boot, farthest apart, stored in foot-bone space. Meshes are not readable
// in the player build, so this runs in the editor and the points are serialized on the component.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod NPCFootCalibration.Run
public static class NPCFootCalibration
{
    public static void Run()
    {
        var log=new List<string>();
        var scene=EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        foreach(var root in scene.GetRootGameObjects())
            foreach(var feet in root.GetComponentsInChildren<NPCFootContact>(true))log.Add(Calibrate(feet));
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(asset==null || asset.GetComponentInChildren<NPCFootContact>(true)==null)continue;
            var contents=PrefabUtility.LoadPrefabContents(path);
            try{foreach(var feet in contents.GetComponentsInChildren<NPCFootContact>(true))log.Add(path+": "+Calibrate(feet));PrefabUtility.SaveAsPrefabAsset(contents,path);}
            finally{PrefabUtility.UnloadPrefabContents(contents);}
        }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("output/character-ground-audit");File.WriteAllLines("output/character-ground-audit/calibration.txt",log);
        Debug.Log("NPC FEET CALIBRATED "+log.Count+"\n"+string.Join("\n",log));
    }

    static string Calibrate(NPCFootContact feet)
    {
        if(feet.leftFoot==null || feet.rightFoot==null)return feet.name+": no feet";
        var skin=feet.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(s=>s.bones.Contains(feet.leftFoot) && s.bones.Contains(feet.rightFoot));
        if(skin==null || skin.sharedMesh==null)return feet.name+": no skinned mesh with both feet";
        feet.leftContacts=Points(skin,feet.leftFoot,feet.rightFoot,out float l);feet.rightContacts=Points(skin,feet.rightFoot,feet.leftFoot,out float r);
        EditorUtility.SetDirty(feet);
        return feet.name+": heel-toe "+l.ToString("0.000")+" / "+r.ToString("0.000")+" m";
    }

    static Vector3[] Points(SkinnedMeshRenderer skin,Transform foot,Transform otherFoot,out float span)
    {
        var mesh=skin.sharedMesh;var bones=skin.bones;var bind=mesh.bindposes;
        int footIndex=System.Array.IndexOf(bones,foot);
        var set=new HashSet<int>(Enumerable.Range(0,bones.Length).Where(i=>bones[i]!=null && bones[i].IsChildOf(foot)));
        int hips=System.Array.FindIndex(bones,b=>b!=null && (b.name=="Pelvis" || b.name=="Hips" || b.name=="hips"));
        Vector3 footPos=bind[footIndex].inverse.MultiplyPoint3x4(Vector3.zero);
        Vector3 hipPos=hips>=0?bind[hips].inverse.MultiplyPoint3x4(Vector3.zero):footPos+Vector3.up;
        // Vertical of the bind pose: from between the feet up to the pelvis (one leg alone tilts with the stance).
        Vector3 otherPos=bind[System.Array.IndexOf(bones,otherFoot)].inverse.MultiplyPoint3x4(Vector3.zero);
        Vector3 up=(hipPos-(footPos+otherPos)*.5f).normalized;
        var vertices=mesh.vertices;var weights=mesh.boneWeights;var candidates=new List<Vector3>();
        float W(BoneWeight w)=>(set.Contains(w.boneIndex0)?w.weight0:0)+(set.Contains(w.boneIndex1)?w.weight1:0)+(set.Contains(w.boneIndex2)?w.weight2:0)+(set.Contains(w.boneIndex3)?w.weight3:0);
        for(int i=0;i<vertices.Length;i++)if(W(weights[i])>=.5f)candidates.Add(vertices[i]);
        span=0;
        if(candidates.Count==0)return new Vector3[0];
        float low=candidates.Min(v=>Vector3.Dot(v,up));
        float legLength=(hipPos-footPos).magnitude;
        var soles=candidates.Where(v=>Vector3.Dot(v,up)<=low+legLength*.025f).ToList();
        Vector3 a=soles[0],b=soles[0];
        foreach(var p in soles)foreach(var q in soles)if((p-q).sqrMagnitude>(a-b).sqrMagnitude){a=p;b=q;}
        span=(a-b).magnitude*skin.transform.lossyScale.x;
        // Project both points onto the sole plane (lowest height) so neither sits above the boot's bottom.
        Vector3 Flat(Vector3 v)=>v-up*(Vector3.Dot(v,up)-low);
        return new[]{bind[footIndex].MultiplyPoint3x4(Flat(a)),bind[footIndex].MultiplyPoint3x4(Flat(b))};
    }
}
