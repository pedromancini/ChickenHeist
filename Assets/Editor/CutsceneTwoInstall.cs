using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Cutscene 2 ("O dia seguinte", DeclineCinematicStage): the cast on its stage prefab. Joana and Osvaldo (the couple who
// were robbed) are already there; this adds Bento, the neighbour who puts the camera up, and Elias for the last scene,
// and gives the three villagers a "Fala" shape: the pack's heads have no jaw and the mouth is painted, so the shape
// drops the chin, as the visitor's mask does, for the mouth to move with the line.
public static class CutsceneTwoInstall
{
    const string Prefab="Assets/Resources/Cinematics/DeclineStage.prefab",Meshes="Assets/ChickenHeistGenerated/Characters/Villagers/Speech";
    [MenuItem("Chicken Heist/Cinematics/Install Cutscene Two Cast")]
    public static void Install()
    {
        Directory.CreateDirectory(Meshes);
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var stage=root.GetComponent<DeclineCinematicStage>();
            if(stage.bento!=null)Object.DestroyImmediate(stage.bento.gameObject);
            var bento=VillagerNPCFactory.Create(root.transform,"Bento","peasant_3","flannel");
            Object.DestroyImmediate(bento.GetComponent<RuralCharacterAnimator>());Object.DestroyImmediate(bento.GetComponent<NPCFootContact>());
            stage.bento=bento.transform;
            if(stage.elias!=null)Object.DestroyImmediate(stage.elias.gameObject);
            var elias=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistInstaller.PrefabPath),root.transform).transform;elias.name="Elias - ator cinematografico";
            elias.localPosition=Vector3.zero;elias.localRotation=Quaternion.identity;
            foreach(var script in elias.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(script);
            foreach(var collider in elias.GetComponentsInChildren<Collider>(true))Object.DestroyImmediate(collider);
            foreach(var node in elias.GetComponentsInChildren<Transform>(true)){node.gameObject.layer=0;if(node.name=="Corpo em primeira pessoa" || node.name.Contains("Celular"))node.gameObject.SetActive(false);}
            foreach(var renderer in elias.GetComponentsInChildren<Renderer>()){renderer.enabled=true;renderer.forceRenderingOff=false;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;}
            foreach(var skin in elias.GetComponentsInChildren<SkinnedMeshRenderer>())skin.updateWhenOffscreen=true;
            foreach(var animation in elias.GetComponentsInChildren<Animation>()){animation.playAutomatically=false;animation.enabled=false;}
            stage.elias=elias;
            foreach(var actor in new[]{stage.osvaldo,stage.joana,stage.bento})Debug.Log("CUTSCENE TWO speech shape "+actor.name+": "+SpeechShape(actor));
            stage.bedSurface=BedSurface();Debug.Log("CUTSCENE TWO bed surface "+stage.bedSurface.ToString("0.000"));
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
    }
    public static void InstallBatch(){Install();EditorApplication.Exit(0);}
    // The blanket's height where the tablet lies (DeclineCinematicStage.TabletOnBed from the bed's middle), in the house's
    // frame: the bed's meshes are probed through colliders of their exact shape, made for the query and removed (the
    // world scene is opened for it and not saved).
    static float BedSurface()
    {
        var scene=UnityEditor.SceneManagement.EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var home=Object.FindAnyObjectByType<HouseholdEconomy>().home;
        var bed=home.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Prop_Bed_01");if(bed==null)return 0;
        var renderers=bed.GetComponentsInChildren<Renderer>();var box=renderers[0].bounds;foreach(var r in renderers)box.Encapsulate(r.bounds);
        var at=home.TransformPoint(home.InverseTransformPoint(box.center)+DeclineCinematicStage.TabletOnBed);
        var probes=bed.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh!=null).Select(f=>{var c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;return c;}).ToList();
        Physics.SyncTransforms();float top=float.NegativeInfinity;
        foreach(var hit in Physics.RaycastAll(at+Vector3.up*2,Vector3.down,4,~0,QueryTriggerInteraction.Ignore))if(hit.collider is MeshCollider m && probes.Contains(m))top=Mathf.Max(top,hit.point.y);
        foreach(var probe in probes)Object.DestroyImmediate(probe);
        return float.IsNegativeInfinity(top)?0:home.InverseTransformPoint(new Vector3(at.x,top,at.z)).y;
    }
    // A copy of the body mesh with a "Fala" shape: the vertices the head carries, in the lower front of the face, drop
    // and draw back a little, most at the chin; the painted mouth stretches with them.
    static string SpeechShape(Transform actor)
    {
        var skin=actor.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s=>s.bones.Any(b=>b!=null && b.name=="Head"));
        var source=skin.sharedMesh;if(source.GetBlendShapeIndex("Fala")>=0)source=AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GetAssetPath(source))??source;
        int head=System.Array.FindIndex(skin.bones,b=>b!=null && b.name=="Head");
        var mesh=Object.Instantiate(source);mesh.name=source.name.Replace(" (Fala)","")+" (Fala)";
        if(mesh.GetBlendShapeIndex("Fala")>=0){var keep=mesh;mesh=Object.Instantiate(source);mesh.ClearBlendShapes();Object.DestroyImmediate(keep);}
        var vertices=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes[head];
        // the head's vertices in the head bone's own space
        var local=new Vector3[vertices.Length];var carried=new float[vertices.Length];
        for(int i=0;i<vertices.Length;i++)
        {
            var w=weights[i];float h=(w.boneIndex0==head?w.weight0:0)+(w.boneIndex1==head?w.weight1:0)+(w.boneIndex2==head?w.weight2:0)+(w.boneIndex3==head?w.weight3:0);
            carried[i]=h;local[i]=bind.MultiplyPoint3x4(vertices[i]);
        }
        var face=Enumerable.Range(0,vertices.Length).Where(i=>carried[i]>.5f).ToArray();
        if(face.Length<20)return "no head vertices";
        // the head's axes in its own space: up is where the head's vertices reach furthest from the neck, front is where
        // the face sticks out (the nose and brow), taken across the head's middle
        var centre=face.Aggregate(Vector3.zero,(s,i)=>s+local[i])/face.Length;
        var top=face.OrderByDescending(i=>(local[i]-centre).sqrMagnitude).Take(face.Length/20).Aggregate(Vector3.zero,(s,i)=>s+local[i])/(face.Length/20);
        // the bind pose head bone sits at the neck: up from the bone's origin to the crown
        Vector3 up=(top-Vector3.zero).normalized;
        if(Vector3.Dot(up,centre)<0)up=-up;
        // front: of the horizontal directions, the one along which the head's vertices reach furthest out
        Vector3 axisA=Vector3.Cross(up,Mathf.Abs(up.x)<.9f?Vector3.right:Vector3.forward).normalized,axisB=Vector3.Cross(up,axisA);
        Vector3 front=axisA;float best=-1;
        for(int k=0;k<16;k++)
        {
            float a=k*Mathf.PI/8;var d=axisA*Mathf.Cos(a)+axisB*Mathf.Sin(a);
            float reach=face.Max(i=>Vector3.Dot(local[i]-centre,d));if(reach>best){best=reach;front=d;}
        }
        float low=face.Min(i=>Vector3.Dot(local[i],up)),high=face.Max(i=>Vector3.Dot(local[i],up));float size=high-low;
        float frontMost=face.Max(i=>Vector3.Dot(local[i]-centre,front));
        var deltas=new Vector3[vertices.Length];int moved=0;
        var toMesh=bind.inverse;
        foreach(int i in face)
        {
            float height=(Vector3.Dot(local[i],up)-low)/size;float forward=Vector3.Dot(local[i]-centre,front)/frontMost;
            // the jaw: the lowest third of the head, its front half; the chin moves most
            if(height>.36f || forward<.15f)continue;
            float w=Mathf.Clamp01((.36f-height)/.22f)*Mathf.Clamp01((forward-.15f)/.35f)*carried[i];
            var shift=(-up*.11f-front*.03f)*size*w;
            deltas[i]=toMesh.MultiplyVector(shift);moved++;
        }
        mesh.AddBlendShapeFrame("Fala",100,deltas,null,null);
        string path=Meshes+"/"+actor.name+"_Fala.asset";
        if(AssetDatabase.LoadAssetAtPath<Mesh>(path)!=null)AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mesh,path);skin.sharedMesh=mesh;
        return moved+" vertices on the jaw, head "+size.ToString("0.000")+" units";
    }
}
