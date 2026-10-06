using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Replaces the protagonist body with native Elias (Tools/SourceArt/ProtagonistV2/build_elias_native.py):
// real human proportions, cap and stubble, bones named like ProtagonistRig so gameplay code is unchanged.
// Every protagonist state is re-baked for the new skeleton: captured takes for locomotion/talk/breathing,
// and the authored action poses (carry, drive, lockpick...) retargeted from the previous rig through Humanoid.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod EliasNativeInstall.Install
public static class EliasNativeInstall
{
    const string Folder="Assets/ChickenHeistGenerated/Characters/EliasNative/";
    const string Model=Folder+"Elias_Rigged.fbx",HumanoidCopy=Folder+"EliasHumanoid.fbx";
    const string OldFolder="Assets/ChickenHeistGenerated/Characters/ProtagonistV2/",OldHumanoid=OldFolder+"Retarget/ProtagonistHumanoid.fbx";
    const string Takes="Assets/ThirdParty/HumanBasicMotions/";
    const string ModelChild="Protagonist_Rigged(Clone)";
    static readonly Dictionary<string,string> Captured=new Dictionary<string,string>
    {
        {"Idle","Idle01"},{"Breathe","Idle02"},{"Talk","Talk01"},{"Walk","Walk01_Forward"},{"Run","Run01_Forward"},{"Jump","Jump01"},{"Fall","Fall01"},
        {"WalkBackward","Walk01_Backward"},{"WalkLeft","Walk01_Left"},{"WalkRight","Walk01_Right"},{"WalkForwardLeft","Walk01_ForwardLeft"},{"WalkForwardRight","Walk01_ForwardRight"},
        {"WalkBackwardLeft","Walk01_BackwardLeft"},{"WalkBackwardRight","Walk01_BackwardRight"},
        {"RunBackward","Run01_Backward"},{"RunLeft","Run01_Left"},{"RunRight","Run01_Right"},{"RunForwardLeft","Run01_ForwardLeft"},{"RunForwardRight","Run01_ForwardRight"},
        {"RunBackwardLeft","Run01_BackwardLeft"},{"RunBackwardRight","Run01_BackwardRight"},
    };
    static readonly string[] Authored={"CrouchIdle","Crouch","Pickup","Trade","Drive","Ignite","Lockpick","Carry"};
    static readonly string[] OneShot={"Pickup","Trade","Jump"};
    static float newStanding;

    public static void Install()
    {
        var log=new List<string>();
        Import(Model,ModelImporterAnimationType.Legacy);Import(HumanoidCopy,ModelImporterAnimationType.Human);
        foreach(var take in Captured.Values)Import(Takes+"HumanM@"+take+".fbx",ModelImporterAnimationType.Human);
        var avatar=Avatar(HumanoidCopy);var oldAvatar=Avatar(OldHumanoid);
        if(avatar==null || oldAvatar==null)throw new InvalidOperationException("Humanoid avatars missing");
        var clips=new Dictionary<string,AnimationClip>();
        foreach(var kv in Captured)clips[kv.Key]=Bake(kv.Key,avatar,(target,t)=>Take(kv.Value).SampleAnimation(target,t),Take(kv.Value).length);
        foreach(string state in Authored)
        {
            var old=AssetDatabase.LoadAssetAtPath<AnimationClip>(OldFolder+state+".anim");
            // Source is always the previous rig from its own FBX (the prefab now holds native Elias).
            var source=new GameObject("old rig");
            var oldModel=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(OldFolder+"Protagonist_Rigged.fbx"),source.transform);oldModel.name=ModelChild;
            foreach(var a in oldModel.GetComponentsInChildren<Animation>())Object.DestroyImmediate(a);foreach(var a in oldModel.GetComponentsInChildren<Animator>())Object.DestroyImmediate(a);
            try
            {
                var sourceRoot=source.GetComponentsInChildren<Transform>().First(t=>t.name=="ProtagonistRig").parent;
                var oldHips=sourceRoot.GetComponentsInChildren<Transform>().First(t=>t.name=="Hips");
                var reader=new HumanPoseHandler(oldAvatar,sourceRoot);var pose=new HumanPose();
                // Humanoid transfer keeps limb angles but not the pelvis drop of sitting and crouching poses:
                // the pelvis follows the source, scaled by the ratio of standing pelvis heights.
                AssetDatabase.LoadAssetAtPath<AnimationClip>(OldFolder+"Idle.anim").SampleAnimation(source,0);
                float oldStanding=sourceRoot.InverseTransformPoint(oldHips.position).y;
                clips[state]=Bake(state,avatar,(target,t)=>
                {
                    old.SampleAnimation(source,t);reader.GetHumanPose(ref pose);
                    var writer=new HumanPoseHandler(avatar,target.transform);writer.SetHumanPose(ref pose);
                    var hips=target.GetComponentsInChildren<Transform>().First(b=>b.name=="Hips");
                    if(newStanding<=0){Take("Idle01").SampleAnimation(target,0);newStanding=target.transform.InverseTransformPoint(hips.position).y;writer.SetHumanPose(ref pose);}
                    var rel=sourceRoot.InverseTransformPoint(oldHips.position)*(newStanding/oldStanding);
                    hips.position=target.transform.TransformPoint(rel);
                },old.length);
                newStanding=0;
            }
            finally{Object.DestroyImmediate(source);}
        }
        log.Add("Baked "+clips.Count+" Elias clips");
        var material=EliasMaterial();
        void Apply(GameObject root,string where)
        {
            foreach(var body in root.GetComponentsInChildren<Animation>(true).Where(a=>a.transform.Find(ModelChild)!=null).ToArray())
            {
                Swap(body,clips,material);log.Add("Elias body swapped: "+where+"/"+body.name);
            }
        }
        foreach(var path in new[]{OldFolder+"Protagonist.prefab","Assets/Resources/Cinematics/OpeningStage.prefab","Assets/Resources/Cinematics/VisitorStage.prefab"})
        {var root=PrefabUtility.LoadPrefabContents(path);try{Apply(root,Path.GetFileName(path));PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
        const string backup="output/scene-backups/ChickenHeistRuralWorld-before-elias-native.unity";
        if(!File.Exists(backup))File.Copy(RuralWorldReview.WorldScene,backup);
        var scene=EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        foreach(var go in scene.GetRootGameObjects())Apply(go,"scene");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("output/elias-native");File.WriteAllLines("output/elias-native/install.txt",log);
        Debug.Log("ELIAS NATIVE INSTALLED\n"+string.Join("\n",log));
    }

    // Cinematic actors are seen in third person only: drop the first-person copy and keep the full body visible.
    public static void CleanStages()
    {
        foreach(var path in new[]{"Assets/Resources/Cinematics/OpeningStage.prefab","Assets/Resources/Cinematics/VisitorStage.prefab"})
        {
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach(var fp in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Corpo em primeira pessoa" || t.name=="Pernas em primeira pessoa").ToArray())Object.DestroyImmediate(fp.gameObject);
                foreach(var arms in root.GetComponentsInChildren<FirstPersonArms>(true))Object.DestroyImmediate(arms);
                foreach(var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.name=="ProtagonistBody" || s.name.StartsWith("ProtagonistHead")))skin.gameObject.layer=0;
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        Debug.Log("ELIAS STAGES CLEANED");
    }

    // First-person copy of the full body: only the head, neck and top of the chest (the volume around the
    // camera) are removed, so looking down shows torso, arms and legs like a full-body first-person game.
    static void FirstPersonBody(GameObject character,SkinnedMeshRenderer full)
    {
        var old=character.transform.Find("Corpo em primeira pessoa");if(old!=null)Object.DestroyImmediate(old.gameObject);
        var source=full.sharedMesh;var baked=new Mesh();full.BakeMesh(baked,true);
        var points=baked.vertices.Select(v=>character.transform.InverseTransformPoint(full.transform.TransformPoint(v))).ToArray();Object.DestroyImmediate(baked);
        int head=Array.FindIndex(full.bones,b=>b.name=="Head"),neck=Array.FindIndex(full.bones,b=>b.name=="Neck");
        var weights=source.boneWeights;
        // Elias v3 ships the head as its own mesh and closes the neck on the body: nothing is cut, so the collar stays whole.
        bool cutHead=!File.Exists(Folder+"EliasV3_Palette.png");
        bool Hidden(int i)
        {
            if(!cutHead)return false;
            // Only the head (and the cap on it) is removed: shoulders, collar and neck stay closed, so looking
            // down never reveals an open garment; the camera near plane handles the rest.
            var w=weights[i];float headWeight=0;
            if(w.boneIndex0==head)headWeight+=w.weight0;if(w.boneIndex1==head)headWeight+=w.weight1;
            return headWeight>.4f;
        }
        // Two-sided copy of the body material: the cut at the neckline must not read as a hole.
        string twoSidedPath=Folder+"EliasFirstPerson.mat";var twoSided=AssetDatabase.LoadAssetAtPath<Material>(twoSidedPath);
        if(twoSided==null){twoSided=new Material(full.sharedMaterial){name="Elias first person"};AssetDatabase.CreateAsset(twoSided,twoSidedPath);}
        twoSided.CopyPropertiesFromMaterial(full.sharedMaterial);twoSided.SetFloat("_Cull",0);twoSided.doubleSidedGI=true;EditorUtility.SetDirty(twoSided);
        SkinnedMeshRenderer Copy(string objectName,string meshName,string path,Func<int,bool> hidden,float capAbove=float.PositiveInfinity)
        {
            var stale=character.transform.Find(objectName);if(stale!=null)Object.DestroyImmediate(stale.gameObject);
            var copy=new Mesh{name=meshName,indexFormat=source.indexFormat};
            var vertices=source.vertices.ToList();var normals=source.normals.ToList();var uvs=source.uv.ToList();var boneWeights=weights.ToList();
            var kept=new List<int>[source.subMeshCount];
            for(int sub=0;sub<source.subMeshCount;sub++)
            {var tri=source.GetTriangles(sub);var keep=new List<int>();for(int i=0;i<tri.Length;i+=3)if(!hidden(tri[i]) && !hidden(tri[i+1]) && !hidden(tri[i+2]))keep.AddRange(new[]{tri[i],tri[i+1],tri[i+2]});kept[sub]=keep;}
            if(!float.IsPositiveInfinity(capAbove))
            {
                // Close the openings left by the cut (the waist, seen from straight above) with a fan per boundary
                // loop; vertices are welded by position because flat shading splits them per face.
                var all=kept.SelectMany(k=>k).ToList();
                var key=new Dictionary<Vector3Int,int>();int Weld(int i){var p=Vector3Int.RoundToInt(vertices[i]*10000);if(!key.TryGetValue(p,out int w)){w=i;key[p]=i;}return w;}
                var edges=new Dictionary<(int,int),int>();
                for(int i=0;i<all.Count;i+=3)for(int e=0;e<3;e++){int x=Weld(all[i+e]),y=Weld(all[i+(e+1)%3]);var k=x<y?(x,y):(y,x);edges[k]=edges.TryGetValue(k,out int n)?n+1:1;}
                var next=new Dictionary<int,List<int>>();
                foreach(var kv in edges.Where(kv=>kv.Value==1)){var (x,y)=kv.Key;if(!next.ContainsKey(x))next[x]=new List<int>();if(!next.ContainsKey(y))next[y]=new List<int>();next[x].Add(y);next[y].Add(x);}
                var seen=new HashSet<int>();
                foreach(int start in next.Keys.ToList())
                {
                    if(seen.Contains(start))continue;
                    var loop=new List<int>();int prev=-1,cur=start;
                    while(cur>=0 && !seen.Contains(cur))
                    {
                        seen.Add(cur);loop.Add(cur);int nxt=-1;
                        foreach(int n in next[cur])if(n!=prev && !seen.Contains(n)){nxt=n;break;}
                        prev=cur;cur=nxt;
                    }
                    if(loop.Count<6)continue;
                    // Height is read in character space (the mesh space may carry the FBX axis conversion).
                    if(loop.Average(i=>points[i].y)<capAbove)continue;
                    var centre=loop.Aggregate(Vector3.zero,(acc,i)=>acc+vertices[i])/loop.Count;
                    int c=vertices.Count;vertices.Add(centre);normals.Add(full.transform.InverseTransformDirection(character.transform.up).normalized);uvs.Add(uvs[loop[0]]);
                    boneWeights.Add(boneWeights[loop[0]]);
                    for(int i=0;i<loop.Count;i++){kept[0].AddRange(new[]{c,loop[i],loop[(i+1)%loop.Count]});}
                }
            }
            copy.SetVertices(vertices);copy.SetNormals(normals);copy.SetUVs(0,uvs);copy.boneWeights=boneWeights.ToArray();copy.bindposes=source.bindposes;copy.subMeshCount=source.subMeshCount;
            for(int sub=0;sub<source.subMeshCount;sub++)copy.SetTriangles(kept[sub],sub);
            copy.RecalculateBounds();
            var asset=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(asset==null){AssetDatabase.CreateAsset(copy,path);asset=copy;}
            else{asset.Clear();asset.vertices=copy.vertices;asset.normals=copy.normals;asset.uv=copy.uv;asset.boneWeights=copy.boneWeights;asset.bindposes=copy.bindposes;asset.subMeshCount=copy.subMeshCount;for(int sub=0;sub<copy.subMeshCount;sub++)asset.SetTriangles(copy.GetTriangles(sub),sub);asset.RecalculateBounds();EditorUtility.SetDirty(asset);Object.DestroyImmediate(copy);}
            var go=new GameObject(objectName);go.layer=30;go.transform.SetParent(character.transform,false);
            go.transform.SetPositionAndRotation(full.transform.position,full.transform.rotation);go.transform.localScale=full.transform.lossyScale;
            var renderer=go.AddComponent<SkinnedMeshRenderer>();renderer.sharedMesh=asset;renderer.bones=full.bones;renderer.rootBone=full.rootBone;renderer.sharedMaterial=twoSided;
            renderer.updateWhenOffscreen=true;renderer.shadowCastingMode=ShadowCastingMode.Off;
            return renderer;
        }
        var whole=Copy("Corpo em primeira pessoa","First person body",Folder+"FirstPersonBody.asset",Hidden);
        // Legs-only copy: walking, running and standing show hips, legs and boots; the swinging arms would
        // otherwise pop in at the screen edge as loose hands (FirstPersonArms switches between the two).
        var legBones=new HashSet<int>(Enumerable.Range(0,full.bones.Length).Where(i=>{var n=full.bones[i].name;return n=="Hips" || n.StartsWith("Thigh") || n.StartsWith("Shin") || n.StartsWith("Foot") || n.StartsWith("Toe");}));
        bool NotLeg(int i){var w=weights[i];float l=(legBones.Contains(w.boneIndex0)?w.weight0:0)+(legBones.Contains(w.boneIndex1)?w.weight1:0)+(legBones.Contains(w.boneIndex2)?w.weight2:0)+(legBones.Contains(w.boneIndex3)?w.weight3:0);return l<.5f;}
        var legs=Copy("Pernas em primeira pessoa","First person legs",Folder+"FirstPersonLegs.asset",NotLeg,.75f);
        var arms=character.GetComponent<FirstPersonArms>();if(arms==null)arms=character.AddComponent<FirstPersonArms>();
        arms.full=whole;arms.legs=legs;legs.enabled=false;
        full.gameObject.layer=31;
    }
    public static void RebuildFirstPerson()
    {
        void Apply(GameObject root)
        {
            foreach(var a in root.GetComponentsInChildren<Animation>(true).Where(a=>a.transform.Find(ModelChild)!=null && a.transform.Find("Corpo em primeira pessoa")!=null))
            {a.GetClip("Idle").SampleAnimation(a.gameObject,0);FirstPersonBody(a.gameObject,a.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s=>s.name=="ProtagonistBody"));}
        }
        var prefabPath=OldFolder+"Protagonist.prefab";var prefab=PrefabUtility.LoadPrefabContents(prefabPath);
        try{Apply(prefab);PrefabUtility.SaveAsPrefabAsset(prefab,prefabPath);}finally{PrefabUtility.UnloadPrefabContents(prefab);}
        var scene=EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);foreach(var go in scene.GetRootGameObjects())Apply(go);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("FIRST PERSON REBUILT");
    }

    // The playable body keeps the view 22 cm in front of the shoulder line (PlayerFirstPersonView); an earlier
    // scene rebuild dropped the component, leaving the camera straight above the neck.
    static void EnsureFirstPersonView(GameObject body)
    {
        if(body.GetComponentInParent<PlayerMovement>()!=null && body.GetComponent<PlayerFirstPersonView>()==null)body.AddComponent<PlayerFirstPersonView>();
    }
    public static void RestoreFirstPersonView()
    {
        var scene=EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);int added=0;
        foreach(var go in scene.GetRootGameObjects())
            foreach(var a in go.GetComponentsInChildren<Animation>(true).Where(a=>a.transform.Find(ModelChild)!=null))
            {bool had=a.GetComponent<PlayerFirstPersonView>()!=null;EnsureFirstPersonView(a.gameObject);if(!had && a.GetComponent<PlayerFirstPersonView>()!=null)added++;}
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Debug.Log("FIRST PERSON VIEW RESTORED "+added);
    }

    // Serialized bone references on the protagonist components point at the new skeleton by name.
    static void Rebind(GameObject body)
    {
        Transform Bone(string name)=>body.GetComponentsInChildren<Transform>(true).First(t=>t.name==name);
        var driver=body.GetComponent<RuralCharacterAnimator>();if(driver!=null)driver.gestureBone=Bone("ForearmR");
        var phone=body.GetComponent<HandheldPhone>();
        if(phone!=null){phone.upperArm=Bone("UpperArmR");phone.forearm=Bone("ForearmR");phone.hand=Bone("HandR");}
    }
    public static void RebindAll()
    {
        void Apply(GameObject root){foreach(var a in root.GetComponentsInChildren<Animation>(true).Where(a=>a.transform.Find(ModelChild)!=null))Rebind(a.gameObject);}
        foreach(var path in new[]{OldFolder+"Protagonist.prefab","Assets/Resources/Cinematics/OpeningStage.prefab","Assets/Resources/Cinematics/VisitorStage.prefab"})
        {var root=PrefabUtility.LoadPrefabContents(path);try{Apply(root);PrefabUtility.SaveAsPrefabAsset(root,path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
        var scene=EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);foreach(var go in scene.GetRootGameObjects())Apply(go);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Debug.Log("ELIAS REBOUND");
    }

    // Elias v3 (Tools/SourceArt/ProtagonistV3) ships its own flat palette; earlier builds use the cast atlas.
    static Material EliasMaterial()
    {
        const string palette=Folder+"EliasV3_Palette.png",path=Folder+"EliasV3.mat";
        if(!File.Exists(palette))return CastPalette.Material("eliasNative");
        AssetDatabase.ImportAsset(palette,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(palette);
        if(importer.filterMode!=FilterMode.Point || importer.mipmapEnabled || importer.textureCompression!=TextureImporterCompression.Uncompressed)
        {importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();}
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Elias v3"};AssetDatabase.CreateAsset(material,path);}
        material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(palette));material.SetFloat("_Smoothness",.07f);
        EditorUtility.SetDirty(material);return material;
    }

    static void Import(string path,ModelImporterAnimationType type)
    {
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);bool dirty=false;
        if(importer.animationType!=type){importer.animationType=type;dirty=true;}
        if(type==ModelImporterAnimationType.Human && importer.avatarSetup!=ModelImporterAvatarSetup.CreateFromThisModel){importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;dirty=true;}
        if(path==Model && (importer.importAnimation || !importer.isReadable)){importer.importAnimation=false;importer.isReadable=true;dirty=true;}
        if(dirty)importer.SaveAndReimport();
    }
    static Avatar Avatar(string path){var a=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();return a!=null && a.isHuman && a.isValid?a:null;}
    static AnimationClip Take(string take)=>AssetDatabase.LoadAllAssetsAtPath(Takes+"HumanM@"+take+".fbx").OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));

    // Bakes one state onto the Elias hierarchy used in game: holder / Protagonist_Rigged(Clone) / ProtagonistRig / ...
    static AnimationClip Bake(string state,Avatar avatar,Action<GameObject,float> poseAt,float length)
    {
        var holder=new GameObject("bake");
        var model=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model),holder.transform);model.name=ModelChild;
        try
        {
            var animator=model.GetComponent<Animator>();if(animator==null)animator=model.AddComponent<Animator>();animator.avatar=avatar;animator.applyRootMotion=false;
            var rig=model.transform.Find("ProtagonistRig");var bones=rig.GetComponentsInChildren<Transform>().Where(t=>t!=rig).ToArray();
            var hips=bones.First(b=>b.name=="Hips");
            int frames=Mathf.Max(2,Mathf.RoundToInt(length*30));
            var rot=bones.ToDictionary(b=>b,b=>Enumerable.Range(0,4).Select(_=>new AnimationCurve()).ToArray());
            var pos=Enumerable.Range(0,3).Select(_=>new AnimationCurve()).ToArray();
            // Humanoid posing may also move the non-human Root bone above the pelvis; record it too.
            var root=hips.parent!=rig?hips.parent:null;var rootPos=Enumerable.Range(0,3).Select(_=>new AnimationCurve()).ToArray();
            for(int f=0;f<=frames;f++)
            {
                float t=length*f/frames;poseAt(model,t);
                foreach(var b in bones){var q=b.localRotation;var c=rot[b];c[0].AddKey(t,q.x);c[1].AddKey(t,q.y);c[2].AddKey(t,q.z);c[3].AddKey(t,q.w);}
                var p=hips.localPosition;pos[0].AddKey(t,p.x);pos[1].AddKey(t,p.y);pos[2].AddKey(t,p.z);
                if(root!=null){var r=root.localPosition;rootPos[0].AddKey(t,r.x);rootPos[1].AddKey(t,r.y);rootPos[2].AddKey(t,r.z);}
            }
            bool once=OneShot.Contains(state);
            var clip=new AnimationClip{name=state,legacy=true,frameRate=30,wrapMode=once?WrapMode.ClampForever:WrapMode.Loop};
            foreach(var kv in rot){string path=AnimationUtility.CalculateTransformPath(kv.Key,holder.transform);for(int c=0;c<4;c++)clip.SetCurve(path,typeof(Transform),"localRotation."+"xyzw"[c],kv.Value[c]);}
            string hipPath=AnimationUtility.CalculateTransformPath(hips,holder.transform);for(int c=0;c<3;c++)clip.SetCurve(hipPath,typeof(Transform),"localPosition."+"xyz"[c],pos[c]);
            if(root!=null){string rootPath=AnimationUtility.CalculateTransformPath(root,holder.transform);for(int c=0;c<3;c++)clip.SetCurve(rootPath,typeof(Transform),"localPosition."+"xyz"[c],rootPos[c]);}
            clip.EnsureQuaternionContinuity();
            string asset=Folder+state+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(asset);
            if(existing!=null){EditorUtility.CopySerialized(clip,existing);existing.name=state;EditorUtility.SetDirty(existing);Object.DestroyImmediate(clip);return existing;}
            AssetDatabase.CreateAsset(clip,asset);return clip;
        }
        finally{Object.DestroyImmediate(holder);}
    }

    static void Swap(Animation body,Dictionary<string,AnimationClip> clips,Material material)
    {
        var old=body.transform.Find(ModelChild);
        var layers=old.GetComponentsInChildren<SkinnedMeshRenderer>(true).ToDictionary(s=>s.name,s=>(s.gameObject.layer,s.shadowCastingMode,s.enabled));
        var local=(old.localPosition,old.localRotation,old.localScale);int sibling=old.GetSiblingIndex();
        Object.DestroyImmediate(old.gameObject);
        var model=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Model),body.transform);model.name=ModelChild;
        model.transform.SetSiblingIndex(sibling);model.transform.localPosition=local.localPosition;model.transform.localRotation=local.localRotation;model.transform.localScale=Vector3.one;
        foreach(var a in model.GetComponentsInChildren<Animator>())Object.DestroyImmediate(a);
        foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            skin.sharedMaterial=material;skin.updateWhenOffscreen=true;
            if(layers.TryGetValue(skin.name,out var l)){skin.gameObject.layer=l.layer;skin.shadowCastingMode=l.shadowCastingMode;skin.enabled=l.enabled;}
        }
        foreach(var state in clips.Keys){if(body.GetClip(state)!=null)body.RemoveClip(state);body.AddClip(clips[state],state);}
        body.clip=clips["Idle"];
        Rebind(body.gameObject);
        EnsureFirstPersonView(body.gameObject);
        clips["Idle"].SampleAnimation(body.gameObject,0);
        // First-person arms and legs are cut from the new body, torso hidden under the camera.
        var full=model.GetComponentsInChildren<SkinnedMeshRenderer>().First(s=>s.name=="ProtagonistBody");
        var shirt=body.transform.Find("Camisa fechada - interior");if(shirt!=null)Object.DestroyImmediate(shirt.gameObject);
        FirstPersonBody(body.gameObject,full);
        var firstPerson=body.transform.Find("Corpo em primeira pessoa");
        // CreateFirstPersonBody moves the full body to layer 31 (hidden from the gameplay camera); keep the head there too.
        foreach(var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.name.StartsWith("ProtagonistHead"))skin.gameObject.layer=31;
    }
}
