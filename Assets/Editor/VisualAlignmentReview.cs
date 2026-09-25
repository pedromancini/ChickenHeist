using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Diagnostic only: measures how characters, animals and props sit on the ground,
// looks for broken materials/skins and renders contact sheets for manual review.
// Run: Unity.exe -projectPath . -executeMethod VisualAlignmentReview.Begin
[InitializeOnLoad]
public static class VisualAlignmentReview
{
    const string Key="VisualAlignmentReview",Folder="output/visual-alignment-review";
    const int TileW=360,TileH=480,Columns=6;
    static readonly List<string> report=new List<string>();
    static int phase;static float next;static bool failed;
    static readonly List<Texture2D> cinematicFrames=new List<Texture2D>();
    static int cinematicShots;

    static VisualAlignmentReview()
    {
        EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false) && t==LogType.Exception)Line("EXCEPTION "+m+"\n"+s);};
    }
    public static void Begin()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/visual-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount());SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;report.Clear();next=Time.realtimeSinceStartup+4;Application.runInBackground=true;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);EditorApplication.Exit(failed?1:0);}
    }
    static void Line(string text){report.Add(text);}
    static void Flush(){Directory.CreateDirectory(Folder);File.WriteAllLines(Folder+"/report.txt",report);}

    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try
        {
            if(phase==0){GameMenu.Instance.Resume();next=Time.realtimeSinceStartup+10;phase++;return;}
            if(phase==1){AuditWorld();phase++;next=Time.realtimeSinceStartup+1;return;}
            if(phase==2){StoryDirector.Instance.Begin(false);cinematicShots=0;phase++;next=Time.realtimeSinceStartup+2;return;}
            if(phase==3)
            {
                if(StoryDirector.Active && cinematicShots<24){cinematicFrames.Add(Grab(Camera.main,TileW*2,TileH*3/2));cinematicShots++;next=Time.realtimeSinceStartup+4;return;}
                if(StoryDirector.Active)StoryDirector.Instance.Complete();
                Sheet("cinematic-opening",cinematicFrames,TileW*2,TileH*3/2,3);cinematicFrames.Clear();
                Line("Opening frames captured: "+cinematicShots);Flush();
                StoryDirector.Instance.Begin(true);cinematicShots=0;phase++;next=Time.realtimeSinceStartup+2;return;
            }
            if(phase==4)
            {
                if(StoryDirector.Active && cinematicShots<18){cinematicFrames.Add(Grab(Camera.main,TileW*2,TileH*3/2));cinematicShots++;next=Time.realtimeSinceStartup+4;return;}
                if(StoryDirector.Active)StoryDirector.Instance.Complete();
                Sheet("cinematic-decline",cinematicFrames,TileW*2,TileH*3/2,3);cinematicFrames.Clear();
                Line("Decline frames captured: "+cinematicShots);Flush();
                EditorApplication.isPlaying=false;
            }
        }
        catch(Exception e){failed=true;Line("FAIL "+e);Flush();EditorApplication.isPlaying=false;}
    }

    // ---------- measurements ----------
    class Subject{public Transform root;public string kind;public Bounds body;public float gap,height,width;public List<string> issues=new List<string>();}

    static void AuditWorld()
    {
        var boost=Lighting();
        try
        {
            var subjects=Characters().Concat(Animals()).ToList();
            var owned=new HashSet<Transform>(subjects.Select(s=>s.root));
            Line("== Characters and animals ("+subjects.Count+")");
            foreach(var s in subjects)
                Line((s.issues.Count>0?"FLAG ":"ok   ")+s.kind+" | "+Name(s.root)+" | gap="+s.gap.ToString("0.00")+" h="+s.height.ToString("0.00")+" w="+s.width.ToString("0.00")+(s.issues.Count>0?" | "+string.Join("; ",s.issues):""));
            var people=subjects.Where(s=>s.kind!="animal").ToList();
            var animalsFlagged=subjects.Where(s=>s.kind=="animal" && s.issues.Count>0).Take(18).ToList();
            var animalSamples=subjects.Where(s=>s.kind=="animal").GroupBy(s=>s.root.name.Split(' ')[0]).Select(g=>g.First());
            Portraits("characters",people);
            Portraits("characters-eye-level",people.Where(p=>p.height>p.width).ToList(),true);
            Portraits("animals",animalsFlagged.Concat(animalSamples).Distinct().ToList());
            AuditMaterials();
            var props=Props(owned);
            Portraits("props",props.Take(36).ToList());
        }
        finally{boost();Flush();}
    }

    static IEnumerable<Subject> Characters()
    {
        var roots=new HashSet<Transform>();
        foreach(var smr in Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None))
        {
            if(!smr.gameObject.activeInHierarchy)continue;
            var root=smr.GetComponentInParent<CharacterController>()?.transform??smr.GetComponentInParent<Animation>()?.transform??smr.GetComponentInParent<Animator>()?.transform??smr.transform.parent??smr.transform;
            if(root.GetComponentInParent<InteractableChicken>()!=null || root.GetComponentInParent<SimpleAnimalWander>()!=null)continue;
            roots.Add(root);
        }
        foreach(var root in roots)
        {
            var s=Measure(root,"person");
            foreach(var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if(smr.sharedMesh==null)s.issues.Add("skin without mesh "+smr.name);
                else if(smr.bones.Any(b=>b==null))s.issues.Add("missing bones "+smr.name);
                if(smr.rootBone==null && smr.bones.Length>0)s.issues.Add("no rootBone "+smr.name);
            }
            var clips=root.GetComponentInChildren<Animation>();var animator=root.GetComponentInChildren<Animator>();
            if(clips!=null && !clips.isPlaying)s.issues.Add("Animation idle (bind pose?)");
            if(clips==null && animator!=null && (animator.runtimeAnimatorController==null || animator.avatar==null || !animator.avatar.isValid))s.issues.Add("Animator without controller/avatar");
            if(clips==null && animator==null)s.issues.Add("no animation component");
            if(s.width>s.height*.75f)s.issues.Add("arms spread (T-pose?)");
            if(s.height<1.35f || s.height>2.15f)s.issues.Add("odd height");
            if(Mathf.Abs(s.gap)>.06f)s.issues.Add(s.gap>0?"floating":"sunk");
            yield return s;
        }
    }
    static IEnumerable<Subject> Animals()
    {
        var roots=Object.FindObjectsByType<InteractableChicken>(FindObjectsSortMode.None).Select(c=>c.transform)
            .Concat(Object.FindObjectsByType<SimpleAnimalWander>(FindObjectsSortMode.None).Select(c=>c.transform)).Distinct();
        foreach(var root in roots)
        {
            if(!root.gameObject.activeInHierarchy || root.GetComponentsInChildren<Renderer>().Length==0)continue;
            if(root.GetComponentInParent<PlayerMovement>()!=null)continue;
            var s=Measure(root,"animal");
            if(Mathf.Abs(s.gap)>.06f)s.issues.Add(s.gap>0?"floating":"sunk");
            yield return s;
        }
    }
    static Subject Measure(Transform root,string kind)
    {
        var s=new Subject{root=root,kind=kind};
        var points=new List<Vector3>();
        foreach(var r in root.GetComponentsInChildren<Renderer>())
        {
            if(!r.enabled || r is ParticleSystemRenderer)continue;
            if(r is SkinnedMeshRenderer smr && smr.sharedMesh!=null)
            {
                var baked=new Mesh();smr.BakeMesh(baked,true);var m=smr.transform.localToWorldMatrix;
                foreach(var v in baked.vertices)points.Add(m.MultiplyPoint3x4(v));Object.DestroyImmediate(baked);
            }
            else if(r is MeshRenderer){var mf=r.GetComponent<MeshFilter>();if(mf?.sharedMesh!=null && mf.sharedMesh.isReadable){var m=r.transform.localToWorldMatrix;foreach(var v in mf.sharedMesh.vertices)points.Add(m.MultiplyPoint3x4(v));}else{points.Add(r.bounds.min);points.Add(r.bounds.max);}}
        }
        if(points.Count==0){s.issues.Add("no visible geometry");return s;}
        var b=new Bounds(points[0],Vector3.zero);foreach(var p in points)b.Encapsulate(p);
        s.body=b;s.height=b.size.y;s.width=Mathf.Max(b.size.x,b.size.z);
        // Ground directly below the lowest vertices, ignoring the subject's own colliders.
        var sole=points.Where(p=>p.y<b.min.y+.03f).Aggregate(Vector3.zero,(a,p)=>a+p)/Mathf.Max(1,points.Count(p=>p.y<b.min.y+.03f));
        s.gap=Ground(new Vector3(sole.x,b.max.y+.2f,sole.z),root,out var hit)?b.min.y-hit.y:float.NaN;
        if(float.IsNaN(s.gap))s.issues.Add("no ground below");
        return s;
    }
    static bool Ground(Vector3 from,Transform ignore,out Vector3 point)
    {
        point=default;
        foreach(var h in Physics.RaycastAll(from,Vector3.down,80,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
        {
            if(ignore!=null && (h.transform.IsChildOf(ignore) || ignore.IsChildOf(h.transform) && h.collider is CharacterController))continue;
            if(h.collider is CharacterController)continue;
            point=h.point;return true;
        }
        return false;
    }

    static void AuditMaterials()
    {
        Line("== Materials");
        var seen=new HashSet<Material>();int issues=0;
        foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if(!r.enabled || !r.gameObject.activeInHierarchy)continue;
            foreach(var mat in r.sharedMaterials)
            {
                if(mat==null){Line("FLAG null material slot | "+Name(r.transform));issues++;continue;}
                if(!seen.Add(mat))continue;
                var sh=mat.shader;string problem=null;
                if(sh==null || sh.name.Contains("InternalErrorShader"))problem="error shader (pink)";
                else if(!sh.isSupported)problem="unsupported shader "+sh.name;
                else if(sh.name=="Standard" || sh.name.StartsWith("Legacy Shaders") || sh.name.StartsWith("Mobile/"))problem="built-in shader in URP: "+sh.name;
                else if(mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap")==null && mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex")!=null)problem="texture lost on URP conversion";
                if(problem!=null){Line("FLAG material "+mat.name+" | "+problem+" | e.g. "+Name(r.transform));issues++;}
            }
        }
        Line("Materials checked: "+seen.Count+" issues: "+issues);
    }

    static List<Subject> Props(HashSet<Transform> owned)
    {
        Line("== Props (floating or buried)");
        var terrainGo=GameObject.Find("Terreno Ondulado Low Poly");var terrain=terrainGo!=null?terrainGo.GetComponent<Collider>():null;
        var tops=new Dictionary<Transform,Bounds>();
        var boundsCache=new Dictionary<Transform,Bounds?>();
        Bounds? Combined(Transform t)
        {
            if(boundsCache.TryGetValue(t,out var c))return c;
            Bounds? result=null;
            foreach(var r in t.GetComponentsInChildren<Renderer>()){if(!r.enabled || r is ParticleSystemRenderer)continue;if(result==null)result=r.bounds;else{var x=result.Value;x.Encapsulate(r.bounds);result=x;}}
            boundsCache[t]=result;return result;
        }
        foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if(!r.enabled || !r.gameObject.activeInHierarchy || terrainGo!=null && r.transform.IsChildOf(terrainGo.transform))continue;
            if(owned.Any(o=>r.transform.IsChildOf(o)) || r.GetComponentInParent<Camera>()!=null || r.GetComponentInParent<PlayerMovement>()!=null)continue;
            var top=r.transform;
            while(top.parent!=null && top.parent.parent!=null && top.parent.GetComponent<FarmLayoutInfo>()==null){var pb=Combined(top.parent);if(pb==null || pb.Value.size.magnitude>14)break;top=top.parent;}
            var b=Combined(top);if(b==null || b.Value.size.magnitude<.15f || b.Value.size.magnitude>14)continue;
            tops[top]=b.Value;
        }
        var list=new List<(Subject s,float score)>();
        foreach(var kv in tops)
        {
            var b=kv.Value;var s=new Subject{root=kv.Key,kind="prop",body=b,height=b.size.y,width=Mathf.Max(b.size.x,b.size.z)};
            float terrainY=float.NaN;
            if(terrain!=null && terrain.Raycast(new Ray(new Vector3(b.center.x,b.max.y+200,b.center.z),Vector3.down),out var th,500))terrainY=th.point.y;
            if(Ground(new Vector3(b.center.x,b.max.y+.5f,b.center.z),kv.Key,out var g))s.gap=b.min.y-g.y;else s.gap=float.NaN;
            float score=0;
            if(!float.IsNaN(terrainY) && b.max.y<terrainY-.02f){s.issues.Add("fully under terrain");score=5+terrainY-b.max.y;}
            else if(!float.IsNaN(terrainY) && b.min.y<terrainY-Mathf.Max(.35f,b.size.y*.45f) && b.size.y<4){s.issues.Add("mostly buried "+(terrainY-b.min.y).ToString("0.00")+"m");score=2+terrainY-b.min.y;}
            if(float.IsNaN(s.gap) && !float.IsNaN(terrainY) && b.min.y>terrainY+.2f){s.issues.Add("floating (nothing below)");score=Mathf.Max(score,1+b.min.y-terrainY);}
            else if(s.gap>.15f && s.gap<6){s.issues.Add("floating "+s.gap.ToString("0.00")+"m");score=Mathf.Max(score,s.gap);}
            if(s.issues.Count>0)list.Add((s,score));
        }
        var ordered=list.OrderByDescending(x=>x.score).Select(x=>x.s).ToList();
        foreach(var s in ordered)Line("FLAG prop | "+Name(s.root)+" | "+string.Join("; ",s.issues)+" | size="+s.body.size.ToString("0.0"));
        Line("Prop roots checked: "+tops.Count+" flagged: "+ordered.Count);
        Probe("Tree_02(Clone)",6);Probe("CircleTree_Autumn(Clone)",4);
        return ordered;
    }

    // Lists every collider and visible surface stacked under a tree base, to explain ground/collider mismatches.
    static void Probe(string name,int count)
    {
        Line("== Probe under "+name);
        var trees=Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name==name && t.GetComponentInChildren<Renderer>()!=null).Take(count);
        var surfaces=Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r=>r.enabled && r.gameObject.activeInHierarchy && r.bounds.size.x>6 && r.bounds.size.z>6).ToArray();
        foreach(var tree in trees)
        {
            var b=tree.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).Aggregate((a,x)=>{a.Encapsulate(x);return a;});
            var p=new Vector3(b.center.x,b.min.y,b.center.z);
            var hits=Physics.RaycastAll(p+Vector3.up*60,Vector3.down,200,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.distance).Select(h=>h.collider.name+"@"+h.point.y.ToString("0.00")+(h.collider.isTrigger?"(trigger)":""));
            var under=surfaces.Where(r=>r.bounds.min.x<=p.x && r.bounds.max.x>=p.x && r.bounds.min.z<=p.z && r.bounds.max.z>=p.z && r.bounds.min.y<p.y+1 && r.bounds.max.y>p.y-8)
                .Select(r=>Name(r.transform)+" ["+string.Join(",",r.sharedMaterials.Where(m=>m!=null).Select(m=>m.name))+"] top="+r.bounds.max.y.ToString("0.00")+" collider="+(r.GetComponent<Collider>()!=null));
            Line("tree base y="+p.y.ToString("0.00")+" at "+p.ToString("0.0")+" | hits: "+string.Join(" ; ",hits));
            foreach(var u in under)Line("    surface "+u);
        }
    }

    // ---------- rendering ----------
    static Action Lighting()
    {
        bool fog=RenderSettings.fog;var ambient=RenderSettings.ambientLight;var mode=RenderSettings.ambientMode;
        RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.56f,.6f);
        var light=new GameObject("Visual review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(38,-30,0);
        return ()=>{RenderSettings.fog=fog;RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=mode;Object.Destroy(light.gameObject);};
    }
    static void Portraits(string name,List<Subject> subjects,bool eyeLevel=false)
    {
        var frames=new List<Texture2D>();var index=new List<string>();
        var go=new GameObject("Visual review camera");var eye=go.AddComponent<Camera>();
        if(Camera.main!=null)eye.CopyFrom(Camera.main);
        eye.enabled=false;eye.fieldOfView=32;eye.nearClipPlane=.03f;eye.farClipPlane=400;eye.cullingMask=~0;eye.clearFlags=CameraClearFlags.SolidColor;eye.backgroundColor=new Color(.42f,.55f,.66f);
        foreach(var s in subjects)
        {
            var b=s.body;float size=Mathf.Max(b.size.y,s.width*.9f,.35f);
            var forward=s.root.forward;forward.y=0;if(forward.sqrMagnitude<.01f)forward=Vector3.forward;
            var dir=Quaternion.Euler(0,32,0)*forward.normalized;
            bool lying=s.kind=="person" && s.height<s.width;
            if(lying)dir=s.root.right;
            float distance=size*.62f/Mathf.Tan(eye.fieldOfView*.5f*Mathf.Deg2Rad);
            var target=b.center;var position=target+dir*distance+Vector3.up*size*(lying?.9f:.12f);
            // Eye level: what a standing player sees from conversation distance, nothing hidden.
            if(eyeLevel){position=new Vector3(b.center.x,b.min.y+1.62f,b.center.z)+dir*2.6f;target=b.center+Vector3.up*b.extents.y*.35f;}
            eye.transform.position=position;eye.transform.LookAt(target);
            var hidden=eyeLevel?new List<Renderer>():Occluders(position,b,s.root);
            frames.Add(Grab(eye,TileW,TileH));
            foreach(var r in hidden)r.enabled=true;
            index.Add(frames.Count.ToString("00")+" "+Name(s.root)+(s.issues.Count>0?" | "+string.Join("; ",s.issues):""));
        }
        Object.Destroy(go);
        Sheet(name,frames,TileW,TileH,Columns);
        File.WriteAllLines(Folder+"/"+name+"-index.txt",index);
    }
    static List<Renderer> Occluders(Vector3 eye,Bounds body,Transform subject)
    {
        var hidden=new List<Renderer>();
        foreach(var corner in new[]{body.center,body.center+Vector3.up*body.extents.y*.8f,body.center-Vector3.up*body.extents.y*.8f})
        {
            var dir=corner-eye;
            foreach(var h in Physics.RaycastAll(eye,dir.normalized,dir.magnitude,~0,QueryTriggerInteraction.Collide))
            {
                if(h.transform.IsChildOf(subject))continue;
                var owner=h.collider.GetComponentInParent<Rigidbody>()?.transform??h.transform;
                foreach(var r in owner.GetComponentsInChildren<Renderer>().Concat(h.transform.GetComponents<Renderer>()))
                    if(r.enabled && !r.transform.IsChildOf(subject) && r.bounds.size.magnitude<60){r.enabled=false;hidden.Add(r);}
            }
        }
        return hidden;
    }
    static Texture2D Grab(Camera eye,int w,int h)
    {
        var rt=RenderTexture.GetTemporary(w,h,24);var previous=eye.targetTexture;eye.targetTexture=rt;eye.Render();eye.targetTexture=previous;
        var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);tex.Apply();
        RenderTexture.active=old;RenderTexture.ReleaseTemporary(rt);return tex;
    }
    static void Sheet(string name,List<Texture2D> frames,int w,int h,int columns)
    {
        const int PerSheet=24;
        for(int page=0;page*PerSheet<frames.Count;page++)
        {
            var chunk=frames.Skip(page*PerSheet).Take(PerSheet).ToList();
            int cols=Mathf.Min(columns,chunk.Count),rows=(chunk.Count+cols-1)/cols;
            var sheet=new Texture2D(cols*w+(cols-1)*4,rows*h+(rows-1)*4,TextureFormat.RGB24,false);
            var fill=Enumerable.Repeat(new Color32(20,20,20,255),sheet.width*sheet.height).ToArray();sheet.SetPixels32(fill);
            for(int i=0;i<chunk.Count;i++){int c=i%cols,r=rows-1-i/cols;sheet.SetPixels(c*(w+4),r*(h+4),w,h,chunk[i].GetPixels());}
            sheet.Apply();File.WriteAllBytes(Folder+"/"+name+"-"+(page+1)+".png",sheet.EncodeToPNG());Object.Destroy(sheet);
        }
        foreach(var f in frames)Object.Destroy(f);
    }
    static string Name(Transform t){var names=new List<string>();for(var x=t;x!=null && names.Count<4;x=x.parent)names.Insert(0,x.name);return string.Join("/",names);}
}
