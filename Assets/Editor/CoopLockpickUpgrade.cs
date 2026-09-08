using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CoopLockpickUpgrade
{
    public static void RunBatch()
    {
        const string folder="Assets/ChickenHeistGenerated/Padlock";
        const string path=folder+"/CoopPadlock.fbx";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        importer.globalScale=1;importer.bakeAxisConversion=true;importer.SaveAndReimport();
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var source=model.GetComponentInChildren<MeshFilter>();
        var materials=model.GetComponentInChildren<MeshRenderer>().sharedMaterials.Select(m=>
        {
            string name=m.name;string materialPath=folder+"/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,materialPath);}
            mat.color=name.Contains("Keyway")?new Color(.018f,.021f,.022f):name.Contains("Steel")?new Color(.46f,.53f,.58f):name.Contains("Wear")?new Color(.68f,.49f,.23f):new Color(.53f,.34f,.12f);
            mat.SetFloat("_Metallic",name.Contains("Keyway")?0:.75f);mat.SetFloat("_Smoothness",.55f);EditorUtility.SetDirty(mat);return mat;
        }).ToArray();
        Directory.CreateDirectory("Assets/Resources");
        var root=new GameObject("CoopPadlock");var mesh=Object.Instantiate(source.sharedMesh);
        var vertices=mesh.vertices;
        for(int i=0;i<vertices.Length;i++)vertices[i]=source.transform.TransformPoint(vertices[i]);
        mesh.vertices=vertices;mesh.RecalculateBounds();
        var bounds=mesh.bounds;
        for(int i=0;i<vertices.Length;i++){
            var v=vertices[i]-bounds.center;
            vertices[i]=new Vector3(v.x/bounds.size.x,v.y/bounds.size.y,v.z/bounds.size.z);
        }
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();
        const string meshPath="Assets/ChickenHeistGenerated/Padlock/CoopPadlockMesh.asset";
        var savedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(savedMesh==null){AssetDatabase.CreateAsset(mesh,meshPath);savedMesh=mesh;}
        else{EditorUtility.CopySerialized(mesh,savedMesh);Object.DestroyImmediate(mesh);}
        root.AddComponent<MeshFilter>().sharedMesh=savedMesh;
        root.AddComponent<MeshRenderer>().sharedMaterials=materials;
        PrefabUtility.SaveAsPrefabAsset(root,"Assets/Resources/CoopPadlock.prefab");Object.DestroyImmediate(root);
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        foreach(var coop in Object.FindObjectsByType<ChickenCoopLockpick>())
        {
            coop.lockAnchor=coop.transform.Find("Cadeado do Galinheiro");CoopPadlockModel.Apply(coop.lockAnchor);EditorUtility.SetDirty(coop);
        }
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        SessionState.SetBool("Protagonist.Batch",true);PlayableVillageTests.Run();
    }
}

