using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Mixamo (or any humanoid FBX) -> Elias. Drop the downloads into E:\Jogo3D\Animacoes named after the game state
// (Walk.fbx, CrouchIdle.fbx... see ANIMACOES_MIXAMO.md; common Mixamo names are recognised too). Each file is
// imported as Humanoid, retargeted through Elias' humanoid avatar and baked into the legacy clip of that state,
// overwritten in place so the player, the cinematic stages and the first-person body pick it up without rewiring.
// The previous clip is kept once in EliasNative/Backup. A strip of four frames per clip goes to output/mixamo.
// Install: Unity.exe -batchmode -quit -projectPath . -executeMethod MixamoInstall.Run
// Trial (bakes to a scratch folder, game untouched): ... -executeMethod MixamoInstall.Trial -mixamoSource <folder>
public static class MixamoInstall
{
    const string Imported="Assets/ThirdParty/Mixamo/",Trial_="Assets/ThirdParty/Mixamo/_trial/",Backup=EliasNativeInstall.Folder+"Backup/",Out="output/mixamo";
    // Game state, loops, accepted file names (lower case, letters only). The first name is the one to use.
    static readonly (string state,bool loop,string[] names)[] States=
    {
        ("Idle",true,new[]{"idle"}),
        ("Breathe",true,new[]{"breathe","breathingidle"}),
        ("Talk",true,new[]{"talk","talking"}),
        ("Walk",true,new[]{"walk","walking","standardwalk"}),
        ("Run",true,new[]{"run","running","standardrun"}),
        ("WalkBackward",true,new[]{"walkbackward","walkingbackwards","walkingbackward","walkbackwards"}),
        ("WalkLeft",true,new[]{"walkleft","leftstrafewalking","leftstrafewalk"}),
        ("WalkRight",true,new[]{"walkright","rightstrafewalking","rightstrafewalk"}),
        ("RunBackward",true,new[]{"runbackward","runningbackward","runningbackwards","runbackwards"}),
        ("RunLeft",true,new[]{"runleft","leftstrafe","leftstraferunning"}),
        ("RunRight",true,new[]{"runright","rightstrafe","rightstraferunning"}),
        ("Crouch",true,new[]{"crouch","crouchwalk","crouchedwalking","crouchwalking","sneakwalk"}),
        ("CrouchIdle",true,new[]{"crouchidle","crouchingidle"}),
        ("Jump",false,new[]{"jump","jumping"}),
        ("Fall",true,new[]{"fall","fallingidle","falling"}),
        ("Pickup",false,new[]{"pickup","pickingup","pickingupobject"}),
        ("Carry",true,new[]{"carry","carrying","carryidle","boxidle"}),
        ("Lockpick",true,new[]{"lockpick","kneeling","kneelingidle"}),
        ("Drive",true,new[]{"drive","driving"}),
        ("Ignite",true,new[]{"ignite"}),
        ("Trade",false,new[]{"trade","giving","handing"}),
    };
    // A state without its own file borrows another one (the hands are placed by code on top of the body pose).
    static readonly Dictionary<string,string> Borrow=new Dictionary<string,string>{{"Ignite","Drive"}};
    // One-shot gestures the game plays in a fixed time: keep only the dip (pelvis goes down and comes back up)
    // and retime it to this length. Mixamo's "Picking Up" is 9.6 s, the game lifts the chicken in 0.85 s.
    static readonly Dictionary<string,float> Dip=new Dictionary<string,float>{{"Pickup",1.1f}};

    public static void Run()=>Install(Arg("-mixamoSource")??Path.GetFullPath("../Animacoes"),false);
    public static void Trial()=>Install(Arg("-mixamoSource")??Path.GetFullPath("../Animacoes"),true);
    static string Arg(string name){var a=Environment.GetCommandLineArgs();int i=Array.IndexOf(a,name);return i>=0 && i+1<a.Length?a[i+1]:null;}
    static string Key(string file)=>new string(Path.GetFileNameWithoutExtension(file).ToLowerInvariant().Where(char.IsLetter).ToArray());

    static void Install(string source,bool trial)
    {
        var log=new List<string>{"source "+source+(trial?" (trial: game clips untouched)":"")};
        Directory.CreateDirectory(Out);
        if(!Directory.Exists(source)){log.Add("FAIL source folder not found");Write(log);return;}
        string target=trial?Trial_:Imported;Directory.CreateDirectory(target);
        var found=new Dictionary<string,string>();
        foreach(var file in Directory.GetFiles(source,"*.fbx"))
        {
            var match=States.FirstOrDefault(s=>s.names.Contains(Key(file)));
            if(match.state==null){log.Add("SKIP "+Path.GetFileName(file)+": name not recognised (rename it to one of the states)");continue;}
            if(found.ContainsKey(match.state)){log.Add("SKIP "+Path.GetFileName(file)+": "+match.state+" already comes from "+Path.GetFileName(found[match.state]));continue;}
            string asset=target+match.state+".fbx";
            if(!File.Exists(asset) || File.GetLastWriteTimeUtc(asset)<File.GetLastWriteTimeUtc(file) || new FileInfo(asset).Length!=new FileInfo(file).Length)File.Copy(file,asset,true);
            found[match.state]=file;
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var elias=EliasNativeInstall.Avatar(EliasNativeInstall.HumanoidCopy);
        if(elias==null){log.Add("FAIL Elias humanoid avatar missing or invalid");Write(log);return;}
        var baked=new Dictionary<string,AnimationClip>();
        foreach(var s in States)
        {
            string from=found.ContainsKey(s.state)?s.state:Borrow.TryGetValue(s.state,out var other) && found.ContainsKey(other)?other:null;
            if(from==null)continue;
            string asset=target+from+".fbx";
            var clip=Humanoid(asset,s.loop,out string problem);
            if(clip==null){log.Add("FAIL "+s.state+": "+problem);continue;}
            string output=trial?Trial_+s.state+".anim":null;
            if(!trial)KeepOriginal(s.state);
            float begin=0,end=clip.length,length=clip.length;string note="";
            if(Dip.TryGetValue(s.state,out float fit)){(begin,end)=DipWindow(clip,elias);length=fit;note=$" [cut {begin:0.00}-{end:0.00}s, retimed to {fit:0.00}s]";}
            var pose=InPlace(clip);float b=begin,e=end,l=length;
            baked[s.state]=EliasNativeInstall.Bake(s.state,elias,(model,t)=>pose(model,b+(e-b)*t/l),length,output,s.loop);
            log.Add("OK "+s.state+" <- "+Path.GetFileName(found[from])+$" ({length:0.00}s, {(s.loop?"loop":"once")})"+note+(from!=s.state?" [borrowed from "+from+"]":""));
        }
        var missing=States.Where(s=>!baked.ContainsKey(s.state)).Select(s=>s.state).ToList();
        if(missing.Count>0)log.Add("kept current clip for: "+string.Join(", ",missing));
        AssetDatabase.SaveAssets();
        Sheets(baked,log);
        // every clip, captured or from Mixamo, with the feet on the floor (output/mixamo/grounding.txt)
        if(!trial){ClipGrounding.Run();log.Add("feet grounded: output/mixamo/grounding.txt");}
        Write(log);
    }
    static void Write(List<string> log){File.WriteAllLines(Out+"/install.txt",log);Debug.Log("MIXAMO INSTALL\n"+string.Join("\n",log));}

    // Humanoid import with the root baked into the pose at its original height and heading, so sitting and
    // crouching keep their pelvis drop; InPlace removes any travel left in the take.
    static AnimationClip Humanoid(string path,bool loop,out string problem)
    {
        problem=null;
        var importer=(ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;importer.importAnimation=true;
        importer.SaveAndReimport();
        var clips=importer.defaultClipAnimations;
        if(clips.Length==0){problem="no animation in the file";return null;}
        foreach(var c in clips)
        {
            c.loopTime=loop;c.loopPose=false;
            c.lockRootRotation=true;c.keepOriginalOrientation=true;
            c.lockRootHeightY=true;c.keepOriginalPositionY=true;c.heightFromFeet=false;
            c.lockRootPositionXZ=true;c.keepOriginalPositionXZ=true;
        }
        importer.clipAnimations=clips;importer.SaveAndReimport();
        var avatar=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Avatar>().FirstOrDefault();
        if(avatar==null || !avatar.isHuman || !avatar.isValid){problem="Unity could not map a humanoid skeleton in this file";return null;}
        var result=AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c=>!c.name.StartsWith("__preview__"));
        if(result==null)problem="no clip after import";
        return result;
    }

    // Samples the take on Elias and cancels net travel: the pelvis starts over Elias' own and any drift between the
    // first and last frame is removed linearly, keeping sway and bob. Height is left as captured.
    static Action<GameObject,float> InPlace(AnimationClip clip)
    {
        Vector3 rest=Vector3.zero,start=Vector3.zero,drift=Vector3.zero;bool ready=false;
        return (model,t)=>
        {
            var hips=model.GetComponentsInChildren<Transform>().First(b=>b.name=="Hips");
            Vector3 Local()=>model.transform.InverseTransformPoint(hips.position);
            if(!ready)
            {
                rest=Local();
                clip.SampleAnimation(model,clip.length);var end=Local();
                clip.SampleAnimation(model,0);start=Local();
                drift=end-start;drift.y=0;ready=true;
            }
            clip.SampleAnimation(model,t);
            var p=Local()-drift*(clip.length>0?t/clip.length:0);
            p.x-=start.x-rest.x;p.z-=start.z-rest.z;
            hips.position=model.transform.TransformPoint(p);
        };
    }

    // From the last standing frame before the lowest pelvis point to the first standing frame after it.
    static (float,float) DipWindow(AnimationClip clip,Avatar avatar)
    {
        var model=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(EliasNativeInstall.Model));
        try
        {
            var animator=model.GetComponent<Animator>();if(animator==null)animator=model.AddComponent<Animator>();animator.avatar=avatar;
            var hips=model.GetComponentsInChildren<Transform>().First(b=>b.name=="Hips");
            int n=Mathf.Max(2,Mathf.RoundToInt(clip.length*30));var y=new float[n+1];
            for(int i=0;i<=n;i++){clip.SampleAnimation(model,clip.length*i/n);y[i]=model.transform.InverseTransformPoint(hips.position).y;}
            int low=Array.IndexOf(y,y.Min());float stand=y[0]-.03f;
            int a=low;while(a>0 && y[a]<stand)a--;
            int b=low;while(b<n && y[b]<stand)b++;
            return (clip.length*a/n,clip.length*Mathf.Min(n,b+4)/n);
        }
        finally{Object.DestroyImmediate(model);}
    }

    static void KeepOriginal(string state)
    {
        string current=EliasNativeInstall.Folder+state+".anim",kept=Backup+state+".anim";
        if(!File.Exists(current) || File.Exists(kept))return;
        if(!AssetDatabase.IsValidFolder(Backup.TrimEnd('/')))AssetDatabase.CreateFolder(EliasNativeInstall.Folder.TrimEnd('/'),"Backup");
        AssetDatabase.CopyAsset(current,kept);
    }

    // Four frames per clip on the protagonist prefab, side view, so each result can be checked at a glance.
    static void Sheets(Dictionary<string,AnimationClip> clips,List<string> log)
    {
        if(clips.Count==0)return;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChickenHeistGenerated/Characters/ProtagonistV2/Protagonist.prefab"));
        foreach(var b in root.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
        var body=root.GetComponentsInChildren<Animation>(true).First(a=>a.transform.Find("Protagonist_Rigged(Clone)")!=null);
        foreach(var r in body.GetComponentsInChildren<Renderer>(true)){r.gameObject.layer=0;r.enabled=!r.name.Contains("em primeira pessoa");if(r is SkinnedMeshRenderer sk){sk.forceMatrixRecalculationPerRender=true;sk.updateWhenOffscreen=true;}}
        var light=new GameObject("light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.transform.rotation=Quaternion.Euler(45,-35,0);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.45f,.5f);
        var cam=new GameObject("cam").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.2f,.22f,.25f);
        cam.orthographic=true;cam.orthographicSize=1.05f;cam.nearClipPlane=.05f;cam.farClipPlane=20;
        var rt=new RenderTexture(300,400,24);cam.targetTexture=rt;
        var floor=GameObject.CreatePrimitive(PrimitiveType.Quad);floor.transform.rotation=Quaternion.Euler(90,0,0);floor.transform.localScale=Vector3.one*4;
        cam.Render();// warm-up: the first render after loading can miss the material
        foreach(var kv in clips)
        {
            // How far the new clip is from the game's clip of that state (degrees, mean over bones and 8 moments).
            var current=AssetDatabase.LoadAssetAtPath<AnimationClip>(EliasNativeInstall.Folder+kv.Key+".anim");
            if(current!=null && current!=kv.Value)
            {
                var rig=body.GetComponentsInChildren<Transform>(true).First(t=>t.name=="ProtagonistRig");var bones=rig.GetComponentsInChildren<Transform>(true).Where(t=>t!=rig).ToArray();float sum=0;int n=0;
                for(int i=0;i<8;i++)
                {
                    var a=new Quaternion[bones.Length];current.SampleAnimation(body.gameObject,current.length*i/8f);for(int b=0;b<bones.Length;b++)a[b]=bones[b].localRotation;
                    kv.Value.SampleAnimation(body.gameObject,kv.Value.length*i/8f);for(int b=0;b<bones.Length;b++){sum+=Quaternion.Angle(a[b],bones[b].localRotation);n++;}
                }
                log.Add(kv.Key+": differs from the current game clip by "+(sum/Mathf.Max(1,n)).ToString("F1")+" degrees on average ("+bones.Length+" bones)");
            }
            var sheet=new Texture2D(1200,400,TextureFormat.RGB24,false);
            for(int i=0;i<4;i++)
            {
                kv.Value.SampleAnimation(body.gameObject,kv.Value.length*i/4f);
                var center=root.transform.position+Vector3.up*.95f;
                cam.transform.position=center+Quaternion.Euler(0,i%2==0?90:135,0)*Vector3.forward*4;cam.transform.LookAt(center);
                cam.Render();RenderTexture.active=rt;sheet.ReadPixels(new Rect(0,0,300,400),i*300,0);RenderTexture.active=null;
            }
            sheet.Apply();File.WriteAllBytes(Out+"/"+kv.Key+".png",sheet.EncodeToPNG());Object.DestroyImmediate(sheet);
        }
        cam.targetTexture=null;rt.Release();
        log.Add("frame strips: "+Out+"/<state>.png");
    }
}
