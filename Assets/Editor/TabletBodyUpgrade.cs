using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class TabletBodyUpgrade
{
    const string Folder="Assets/ChickenHeistGenerated/Characters/ProtagonistV2";
    public static void Rebuild(){ProtagonistInstaller.Install();Install();}
    public static void Install()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var scene=EditorSceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene,AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BeforeTabletBodyFix.unity"),true);
        var path=Folder+"/Protagonist.prefab";
        var prefab=PrefabUtility.LoadPrefabContents(path);
        try{CloseShirt(prefab);PrefabUtility.SaveAsPrefabAsset(prefab,path);}
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        var player=Object.FindAnyObjectByType<HeistGameManager>().player;
        CloseShirt(player.GetComponentInChildren<RuralCharacterAnimator>().gameObject);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        TabletBodyReview.Begin();
    }
    public static void Regression()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        SessionState.SetBool("Protagonist.Batch",true);PlayableVillageTests.Run();
    }
    static void CloseShirt(GameObject character)
    {
        var previous=character.transform.Find("Camisa fechada - interior");if(previous!=null)Object.DestroyImmediate(previous.gameObject);
        var skin=character.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s=>s.name=="ProtagonistBody");
        if(character.GetComponent<PlayerFirstPersonView>()==null)character.AddComponent<PlayerFirstPersonView>();
        CreateFirstPersonBody(character,skin);
        var vertices=new List<Vector3>();var normals=new List<Vector3>();var indices=new List<int>();var weights=new List<BoneWeight>();
        int Bone(string name)=>Array.FindIndex(skin.bones,b=>b.name==name);
        int hips=Bone("Hips"),spine=Bone("Spine"),chest=Bone("Chest");
        Vector3 BoundPoint(Vector3 p)=>skin.sharedMesh.bindposes[hips].inverse.MultiplyPoint3x4(skin.bones[hips].InverseTransformPoint(character.transform.TransformPoint(p)));
        // A closed inner shirt follows the same skeleton, behind the textured outer shirt.
        var rings=new[]{new Vector4(.76f,.13f,.08f,0),new Vector4(.80f,.14f,.085f,0),new Vector4(.84f,.145f,.09f,0),new Vector4(.88f,.145f,.09f,0),new Vector4(.91f,.14f,.085f,0)};
        for(int ring=0;ring<rings.Length;ring++)for(int j=0;j<32;j++)
        {
            float a=j*Mathf.PI/16;var r=rings[ring];
            var p=new Vector3(Mathf.Cos(a)*r.y,r.x,Mathf.Sin(a)*r.z);
            vertices.Add(BoundPoint(p));
            weights.Add(new BoneWeight{boneIndex0=hips,weight0=1});
        }
        for(int r=0;r<rings.Length-1;r++)for(int j=0;j<32;j++)
        {int a=r*32+j,b=r*32+(j+1)%32;indices.AddRange(new[]{a,a+32,b,b,a+32,b+32});}
        int cap=vertices.Count;vertices.Add(BoundPoint(new Vector3(0,.91f,0)));weights.Add(new BoneWeight{boneIndex0=hips,weight0=1});
        for(int j=0;j<32;j++)indices.AddRange(new[]{cap,128+(j+1)%32,128+j});
        cap=vertices.Count;vertices.Add(BoundPoint(new Vector3(0,.79f,0)));weights.Add(new BoneWeight{boneIndex0=hips,weight0=1});
        for(int j=0;j<32;j++)indices.AddRange(new[]{cap,j,(j+1)%32});
        // Dark fabric behind each cuff prevents a view through the loose sleeve.
        foreach(string side in new[]{"L","R"})
        {
            int arm=Bone("UpperArm"+side);var upper=skin.bones[arm];var elbow=skin.bones[Bone("Forearm"+side)];
            var axis=(elbow.position-upper.position).normalized;
            var across=Vector3.Cross(axis,character.transform.forward).normalized;
            if(across.sqrMagnitude<.01f)across=Vector3.Cross(axis,character.transform.up).normalized;
            var up=Vector3.Cross(axis,across).normalized;var center=elbow.position-axis*.035f;
            int start=vertices.Count;
            for(int j=0;j<24;j++)
            {
                float angle=j*Mathf.PI/12;var p=center+(across*Mathf.Cos(angle)+up*Mathf.Sin(angle))*.075f;
                vertices.Add(skin.sharedMesh.bindposes[arm].inverse.MultiplyPoint3x4(upper.InverseTransformPoint(p)));
                weights.Add(new BoneWeight{boneIndex0=arm,weight0=1});
            }
            for(int j=1;j<23;j++)indices.AddRange(new[]{start,start+j,start+j+1});
        }
        var mesh=new Mesh{name="Closed inner shirt"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.boneWeights=weights.ToArray();mesh.bindposes=skin.sharedMesh.bindposes;mesh.RecalculateNormals();mesh.RecalculateBounds();
        string meshPath=Folder+"/ClosedShirt.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if(existing==null)AssetDatabase.CreateAsset(mesh,meshPath);else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);mesh=existing;}
        string matPath=Folder+"/ClosedShirt.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,matPath);}
        mat.SetColor("_BaseColor",new Color(.34f,.40f,.34f));mat.SetFloat("_Smoothness",0);mat.SetFloat("_Cull",0);
        var obj=new GameObject("Camisa fechada - interior");obj.layer=30;obj.transform.SetParent(character.transform,false);
        obj.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);obj.transform.localScale=skin.transform.lossyScale;
        var renderer=obj.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=mesh;renderer.bones=skin.bones;renderer.rootBone=skin.rootBone;renderer.sharedMaterial=mat;renderer.updateWhenOffscreen=true;renderer.shadowCastingMode=ShadowCastingMode.Off;
        var phone=character.GetComponent<HandheldPhone>();if(phone?.handset!=null)phone.handset.gameObject.SetActive(false);
    }
    static void CreateFirstPersonBody(GameObject character,SkinnedMeshRenderer full)
    {
        var old=character.transform.Find("Corpo em primeira pessoa");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var source=full.sharedMesh;var vertices=source.vertices;
        var points=vertices.Select(v=>character.transform.InverseTransformPoint(full.transform.TransformPoint(v))).ToArray();
        // Rebuild standard vertex streams rather than copying FBX GPU buffer metadata.
        var copy=new Mesh{name="First person arms and lower body",indexFormat=source.indexFormat};
        copy.vertices=source.vertices;copy.normals=source.normals;copy.tangents=source.tangents;
        copy.uv=source.uv;copy.boneWeights=source.boneWeights;copy.bindposes=source.bindposes;copy.subMeshCount=source.subMeshCount;
        bool Hidden(int i)=>points[i].y>.90f && Mathf.Abs(points[i].x)<.29f;
        for(int sub=0;sub<copy.subMeshCount;sub++)
        {
            var tri=source.GetTriangles(sub);var keep=new List<int>();
            for(int i=0;i<tri.Length;i+=3)if(!Hidden(tri[i]) && !Hidden(tri[i+1]) && !Hidden(tri[i+2]))keep.AddRange(new[]{tri[i],tri[i+1],tri[i+2]});
            copy.SetTriangles(keep,sub);
        }
        string path=Folder+"/FirstPersonBody.asset";var asset=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(asset==null)AssetDatabase.CreateAsset(copy,path);else{asset.Clear(false);EditorUtility.CopySerialized(copy,asset);Object.DestroyImmediate(copy);copy=asset;}
        var go=new GameObject("Corpo em primeira pessoa");go.layer=30;go.transform.SetParent(character.transform,false);
        go.transform.SetPositionAndRotation(full.transform.position,full.transform.rotation);go.transform.localScale=full.transform.lossyScale;
        var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=copy;renderer.bones=full.bones;renderer.rootBone=full.rootBone;renderer.sharedMaterials=full.sharedMaterials;renderer.updateWhenOffscreen=true;renderer.shadowCastingMode=ShadowCastingMode.Off;
        full.gameObject.layer=31;
    }
}
