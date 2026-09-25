using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// One low-poly family for every character (Medieval People pack, present-day rural palettes).
// Elias keeps ProtagonistRig: only the skinned meshes are rebuilt from
// Tools/SourceArt/ProtagonistV2/generated/lowpoly_elias.json (build_lowpoly_elias.py).
// Run: Unity.exe -projectPath . -executeMethod UniformCastUpgrade.Install
public static class UniformCastUpgrade
{
    const string EliasJson="Tools/SourceArt/ProtagonistV2/generated/lowpoly_elias.json";
    const string ProtagonistFolder="Assets/ChickenHeistGenerated/Characters/ProtagonistV2";
    static readonly string[] Stages={"Assets/Resources/Cinematics/OpeningStage.prefab","Assets/Resources/Cinematics/VisitorStage.prefab"};
    // Three farmer types in rotation: khaki shirt and jeans, red flannel, blue shirt and vest.
    static readonly (string model,string palette)[] Farmers={("peasant_5","farmerKhaki"),("rich_citizzens_1","flannel"),("city_dwellers_1",null)};
    static readonly Dictionary<int,(string model,string palette)> Residents=new Dictionary<int,(string,string)>
    {
        {1,("peasant_1",null)},{2,("rich_citizzens_1","flannel")},{3,("peasant_2","dress")},{4,("peasant_5","farmerKhaki")},{5,("city_dwellers_1",null)},
        {6,("peasant_4",null)},{7,("peasant_5",null)},{8,("peasant_6","dress")},{9,("rich_citizzens_1",null)},{10,("peasant_2",null)}
    };

    [Serializable] class EliasVertex{public float[] p;public string[] bones;public float[] weights;}
    [Serializable] class EliasBone{public string name;public float[] head;}
    [Serializable] class EliasData{public string model;public EliasVertex[] vertices;public int[] triangles;public float[] uvs;public EliasBone[] bones;}

    public static void Install()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play mode first.");
        Directory.CreateDirectory("output/scene-backups");
        const string backup="output/scene-backups/ChickenHeistRuralWorld-before-uniform-cast.unity";
        if(!File.Exists(backup))File.Copy(RuralWorldReview.WorldScene,backup);
        var scene=EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var log=new List<string>();
        var elias=BuildElias(log);
        // Protagonist prefab, the player in the scene and both cinematic stages share the same meshes.
        var prefab=PrefabUtility.LoadPrefabContents(ProtagonistFolder+"/Protagonist.prefab");
        try{ApplyElias(prefab,elias,true,log);PrefabUtility.SaveAsPrefabAsset(prefab,ProtagonistFolder+"/Protagonist.prefab");}
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
        var player=Object.FindAnyObjectByType<HeistGameManager>().player.GetComponentInChildren<RuralCharacterAnimator>().gameObject;
        ApplyElias(player,elias,true,log);
        foreach(var path in Stages)
        {
            var stage=PrefabUtility.LoadPrefabContents(path);
            try{ApplyElias(stage,elias,false,log);PrefabUtility.SaveAsPrefabAsset(stage,path);}
            finally{PrefabUtility.UnloadPrefabContents(stage);}
        }
        ReplaceFarmers(log);ReplaceMerchant(log);ReplaceResidents(log);ReplaceDeclineActors(log);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("output/uniform-cast");File.WriteAllLines("output/uniform-cast/install.txt",log);
        Debug.Log("UNIFORM CAST INSTALLED\n"+string.Join("\n",log));
    }

    // ---------- Elias ----------
    class EliasMeshes{public Mesh body,head;public Material material;}
    static EliasMeshes BuildElias(List<string> log)
    {
        var data=JsonUtility.FromJson<EliasData>(File.ReadAllText(EliasJson));
        var template=AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistFolder+"/Protagonist.prefab").GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s=>s.name=="ProtagonistBody");
        var bindposes=template.sharedMesh.bindposes;var boneNames=template.bones.Select(b=>b.name).ToArray();
        // Blender -> mesh space: fit the axis conversion from the rig's bone heads (bindposes hold them in mesh space).
        var pairs=data.bones.Where(b=>Array.IndexOf(boneNames,b.name)>=0).Select(b=>(blender:V(b.head),unity:(Vector3)bindposes[Array.IndexOf(boneNames,b.name)].inverse.GetColumn(3))).ToArray();
        var fit=FitAxes(pairs);
        log.Add($"Elias from {data.model}: axis fit error {fit.error:0.0000} scale {fit.scale:0.000} mirrored {fit.mirrored}");
        if(fit.error>.01f)throw new InvalidOperationException("Could not match Blender and Unity rig spaces.");
        var body=new MeshParts();var head=new MeshParts();
        for(int t=0;t<data.triangles.Length;t+=3)
        {
            int[] ids={data.triangles[t],data.triangles[t+1],data.triangles[t+2]};
            bool isHead=ids.All(i=>data.vertices[i].bones.Length>0 && data.vertices[i].bones[0]=="Head");
            var target=isHead?head:body;
            var order=fit.mirrored?new[]{0,2,1}:new[]{0,1,2};
            foreach(int k in order)
            {
                var v=data.vertices[ids[k]];
                target.positions.Add(fit.Apply(V(v.p)));
                target.uvs.Add(new Vector2(data.uvs[(t+k)*2],data.uvs[(t+k)*2+1]));
                var w=new BoneWeight();
                for(int b=0;b<v.bones.Length && b<4;b++)
                {
                    int index=Array.IndexOf(boneNames,v.bones[b]);if(index<0)throw new InvalidOperationException("Unknown bone "+v.bones[b]);
                    if(b==0){w.boneIndex0=index;w.weight0=v.weights[b];}if(b==1){w.boneIndex1=index;w.weight1=v.weights[b];}
                    if(b==2){w.boneIndex2=index;w.weight2=v.weights[b];}if(b==3){w.boneIndex3=index;w.weight3=v.weights[b];}
                }
                target.weights.Add(w);
            }
        }
        var meshes=new EliasMeshes{body=body.Save(ProtagonistFolder+"/EliasLowPolyBody.asset","Elias low poly body",bindposes),head=head.Save(ProtagonistFolder+"/EliasLowPolyHead.asset","Elias low poly head",bindposes),material=CastPalette.Material("elias")};
        log.Add($"Elias meshes: body {meshes.body.triangles.Length/3} tris, head {meshes.head.triangles.Length/3} tris");
        return meshes;
    }
    class MeshParts
    {
        public List<Vector3> positions=new List<Vector3>();public List<Vector2> uvs=new List<Vector2>();public List<BoneWeight> weights=new List<BoneWeight>();
        public Mesh Save(string path,string name,Matrix4x4[] bindposes)
        {
            // Unshared vertices per triangle: flat facets like the rest of the cast.
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            var mesh=existing??new Mesh();mesh.Clear();mesh.name=name;
            mesh.SetVertices(positions);mesh.SetUVs(0,uvs);mesh.SetTriangles(Enumerable.Range(0,positions.Count).ToArray(),0);
            mesh.boneWeights=weights.ToArray();mesh.bindposes=bindposes;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();
            if(existing==null)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
            return mesh;
        }
    }
    static void ApplyElias(GameObject root,EliasMeshes elias,bool rebuildFirstPerson,List<string> log)
    {
        int count=0;
        foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if(skin.name=="ProtagonistBody"){skin.sharedMesh=elias.body;skin.sharedMaterials=new[]{elias.material};count++;}
            else if(skin.name.StartsWith("ProtagonistHead")){skin.sharedMesh=elias.head;skin.sharedMaterials=new[]{elias.material};count++;}
            // The inner shirt only covered gaps in the previous model's open collar and sleeves.
            else if(skin.name=="Camisa fechada - interior")skin.enabled=false;
            else if(skin.name=="Corpo em primeira pessoa" && !rebuildFirstPerson)skin.sharedMaterials=new[]{elias.material};
        }
        foreach(var body in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.name=="ProtagonistBody").ToArray())
        {
            if(!rebuildFirstPerson)continue;
            var character=body.GetComponentInParent<RuralCharacterAnimator>(true)?.gameObject??root;
            typeof(TabletBodyUpgrade).GetMethod("CreateFirstPersonBody",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{character,body});
        }
        log.Add($"Elias applied to {root.name}: {count} renderers");
    }
    struct AxisFit{public Matrix4x4 rotation;public float scale,error;public Vector3 offset;public bool mirrored;public Vector3 Apply(Vector3 p)=>rotation.MultiplyVector(p)*scale+offset;}
    static AxisFit FitAxes((Vector3 blender,Vector3 unity)[] pairs)
    {
        var best=new AxisFit{error=float.MaxValue};
        Vector3 cb=pairs.Aggregate(Vector3.zero,(a,p)=>a+p.blender)/pairs.Length,cu=pairs.Aggregate(Vector3.zero,(a,p)=>a+p.unity)/pairs.Length;
        int[][] perms={new[]{0,1,2},new[]{0,2,1},new[]{1,0,2},new[]{1,2,0},new[]{2,0,1},new[]{2,1,0}};
        foreach(var perm in perms)for(int signs=0;signs<8;signs++)
        {
            var m=Matrix4x4.zero;m.m33=1;
            for(int r=0;r<3;r++)m[r,perm[r]]=(signs>>r&1)==1?-1:1;
            float num=0,den=0;
            foreach(var p in pairs){var a=m.MultiplyVector(p.blender-cb);num+=Vector3.Dot(a,p.unity-cu);den+=a.sqrMagnitude;}
            float s=num/Mathf.Max(1e-6f,den);if(s<=0)continue;
            float err=pairs.Average(p=>(m.MultiplyVector(p.blender-cb)*s+cu-p.unity).magnitude)/s;
            if(err<best.error)best=new AxisFit{rotation=m,scale=s,error=err,offset=cu-m.MultiplyVector(cb)*s,mirrored=m.determinant<0};
        }
        return best;
    }
    static Vector3 V(float[] a)=>new Vector3(a[0],a[1],a[2]);

    // ---------- NPCs ----------
    static GameObject Recreate(Transform old,string name,(string model,string palette) cast,bool keepDrivers=true)
    {
        var parent=old.parent;var position=old.localPosition;var rotation=old.localRotation;int sibling=old.GetSiblingIndex();
        var oldDriver=old.GetComponent<RuralCharacterAnimator>();
        var created=VillagerNPCFactory.Create(parent,name,cast.model,cast.palette);
        created.transform.SetSiblingIndex(sibling);created.transform.localPosition=position;created.transform.localRotation=rotation;
        var driver=created.GetComponent<RuralCharacterAnimator>();
        if(oldDriver!=null && keepDrivers){driver.farmer=oldDriver.farmer;driver.walker=oldDriver.walker;driver.movement=oldDriver.movement;}
        Object.DestroyImmediate(old.gameObject);
        return created;
    }
    static void ReplaceFarmers(List<string> log)
    {
        var farmers=Object.FindObjectsByType<FarmerSleepSystem>(FindObjectsSortMode.None).OrderBy(f=>f.name).ToArray();
        for(int i=0;i<farmers.Length;i++)
        {
            var old=farmers[i].transform.Find("Visual Villager NPC");if(old==null)throw new InvalidOperationException("Farmer visual missing: "+farmers[i].name);
            var cast=Farmers[i%Farmers.Length];var visual=Recreate(old,"Visual Villager NPC",cast);
            visual.GetComponent<RuralCharacterAnimator>().farmer=farmers[i];
            log.Add($"{farmers[i].name}: {cast.model} {cast.palette??"base"}");
        }
    }
    static void ReplaceMerchant(List<string> log)
    {
        var market=Object.FindFirstObjectByType<VillageMarket>();var old=market.merchant.transform;
        var capsule=old.GetComponent<CapsuleCollider>();float height=capsule!=null?capsule.height:1.8f,radius=capsule!=null?capsule.radius:.25f;
        var merchant=Recreate(old,"Seu Anselmo - Comerciante",("peasant_1","shop"));
        var c=merchant.AddComponent<CapsuleCollider>();c.height=height;c.radius=radius;c.center=Vector3.up*height*.5f;
        market.merchant=merchant.GetComponent<RuralCharacterAnimator>();
        PrefabUtility.SaveAsPrefabAsset(market.gameObject,"Assets/ChickenHeistGenerated/PlayerHome/VendaDoVale.prefab");
        log.Add("Seu Anselmo: peasant_1 shop");
    }
    static void ReplaceResidents(List<string> log)
    {
        var crowd=GameObject.Find("Moradores das estradas");
        foreach(var walker in crowd.GetComponentsInChildren<RoadsideWalker>())
        {
            int number=int.Parse(walker.name.Replace("Morador ",""));var cast=Residents[number];
            var old=walker.GetComponentInChildren<RuralCharacterAnimator>().transform;
            var visual=Recreate(old,"Visual "+cast.model,cast);visual.GetComponent<RuralCharacterAnimator>().walker=walker;
            log.Add($"{walker.name}: {cast.model} {cast.palette??"base"}");
        }
    }
    static void ReplaceDeclineActors(List<string> log)
    {
        const string path="Assets/Resources/Cinematics/DeclineStage.prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var stage=root.GetComponent<DeclineCinematicStage>();
            stage.osvaldo=Actor(stage.osvaldo,"Osvaldo",("peasant_5","farmerKhaki")).transform;
            stage.joana=Actor(stage.joana,"Joana",("peasant_2","dress")).transform;
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        log.Add("Decline stage: Osvaldo peasant_5 farmerKhaki, Joana peasant_2 dress");
    }
    static GameObject Actor(Transform old,string name,(string model,string palette) cast)
    {
        // Cinematic actors are posed by their stage, not by gameplay drivers.
        var actor=Recreate(old,name,cast,false);
        Object.DestroyImmediate(actor.GetComponent<RuralCharacterAnimator>());Object.DestroyImmediate(actor.GetComponent<NPCFootContact>());
        return actor;
    }
}
