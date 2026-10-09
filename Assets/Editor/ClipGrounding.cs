using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

// Elias' clips come from different sources (motion capture, Mixamo, authored poses) and do not all put the feet at
// the same height: once retargeted, the walks sank up to 11 cm into the floor mid-step and the crouches 13-15 cm. For
// every grounded state, the pelvis is moved up or down key by key so that the lowest point of the body in each frame
// sits at the lowest point of the standing Idle; runs are only raised, so their flight phase stays.
// Seated and airborne states are left alone. Run after EliasNativeInstall.Install and MixamoInstall.Run (MixamoInstall
// calls it). Report: output/mixamo/grounding.txt.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod ClipGrounding.Run
// (add -groundingStats to only measure every clip, without changing anything: output/mixamo/grounding-stats.txt)
public static class ClipGrounding
{
    static readonly string[] Skip={"Drive","Ignite","Jump","Fall"};
    public static void Run()
    {
        var log=new List<string>();
        var root=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChickenHeistGenerated/Characters/ProtagonistV2/Protagonist.prefab"));
        try
        {
            foreach(var b in root.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
            var body=root.GetComponentsInChildren<Animation>(true).First(a=>a.transform.Find("Protagonist_Rigged(Clone)")!=null);
            var skins=body.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(s=>s.name.StartsWith("ProtagonistBody")).ToArray();
            var hips=body.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Hips");
            string hipPath=AnimationUtility.CalculateTransformPath(hips,body.transform);
            var baked=new Mesh();
            float Lowest(AnimationClip clip,float t)
            {
                clip.SampleAnimation(body.gameObject,t);float low=float.PositiveInfinity;
                foreach(var s in skins){s.BakeMesh(baked,true);foreach(var v in baked.vertices)low=Mathf.Min(low,body.transform.InverseTransformPoint(s.transform.TransformPoint(v)).y);}
                return low;
            }
            float LowestOver(AnimationClip clip){float low=float.PositiveInfinity;int n=Mathf.Max(2,Mathf.CeilToInt(clip.length*15));for(int i=0;i<=n;i++)low=Mathf.Min(low,Lowest(clip,clip.length*i/n));return low;}
            var idle=body.GetClip("Idle");float reference=LowestOver(idle);
            if(System.Array.IndexOf(System.Environment.GetCommandLineArgs(),"-groundingStats")>=0)
            {
                foreach(AnimationState st in body)
                {
                    var c=st.clip;if(c==null)continue;int n=Mathf.Max(2,Mathf.CeilToInt(c.length*15));
                    var lows=Enumerable.Range(0,n+1).Select(i=>Lowest(c,c.length*i/n)-reference).OrderBy(x=>x).ToArray();
                    log.Add($"{st.name,-18} per-frame lowest point: min {lows[0]*100:+0.0;-0.0}  20% {lows[lows.Length/5]*100:+0.0;-0.0}  median {lows[lows.Length/2]*100:+0.0;-0.0}  max {lows[lows.Length-1]*100:+0.0;-0.0} cm");
                }
                Directory.CreateDirectory("output/mixamo");File.WriteAllLines("output/mixamo/grounding-stats.txt",log);Object.DestroyImmediate(baked);return;
            }
            log.Add($"reference: Idle lowest point {reference*100:F1} cm");
            // Per frame: one foot always stands on the floor, so the lowest point of every frame goes to the standing
            // level (the pelvis curve moves by that amount, smoothed over three frames). Runs only come up where they
            // sink: their flight phase, with both feet up, stays.
            foreach(AnimationState state in body)
            {
                var clip=state.clip;if(clip==null || clip==idle || Skip.Contains(state.name))continue;
                bool raiseOnly=state.name.StartsWith("Run");
                var bindings=AnimationUtility.GetCurveBindings(clip).Where(b=>b.path==hipPath && b.propertyName.StartsWith("m_LocalPosition")).ToList();
                if(bindings.Count<3){log.Add($"{state.name,-18} no pelvis position curve: left as is");continue;}
                var curves=bindings.ToDictionary(b=>b,b=>AnimationUtility.GetEditorCurve(clip,b));
                var times=curves.Values.First().keys.Select(k=>k.time).ToArray();
                var before=times.Select(t=>Lowest(clip,t)-reference).ToArray();
                var delta=before.Select(l=>raiseOnly?Mathf.Max(0,-l):-l).ToArray();
                var smooth=new float[delta.Length];
                for(int i=0;i<delta.Length;i++){float sum=delta[i]*2;int n=2;if(i>0){sum+=delta[i-1];n++;}if(i<delta.Length-1){sum+=delta[i+1];n++;}smooth[i]=sum/n;}
                if(smooth.All(d=>Mathf.Abs(d)<.004f)){log.Add($"{state.name,-18} ok (lowest point {before.Min()*100:+0.0;-0.0}..{before.Max()*100:+0.0;-0.0} cm)");continue;}
                foreach(var binding in bindings)
                {
                    var keys=curves[binding].keys;
                    for(int k=0;k<keys.Length && k<times.Length;k++)
                    {
                        clip.SampleAnimation(body.gameObject,times[k]);var local=hips.parent.InverseTransformVector(body.transform.up*smooth[k]);
                        keys[k].value+=binding.propertyName.EndsWith(".x")?local.x:binding.propertyName.EndsWith(".y")?local.y:local.z;
                    }
                    curves[binding].keys=keys;
                }
                foreach(var kv in curves)AnimationUtility.SetEditorCurve(clip,kv.Key,kv.Value);
                EditorUtility.SetDirty(clip);
                var after=times.Select(t=>Lowest(clip,t)-reference).ToArray();
                log.Add($"{state.name,-18} lowest point per frame {before.Min()*100:+0.0;-0.0}..{before.Max()*100:+0.0;-0.0} cm -> {after.Min()*100:+0.0;-0.0}..{after.Max()*100:+0.0;-0.0} cm"+(raiseOnly?" (run: flight kept)":""));
            }
            // eye height of the crouches, for the first-person camera
            var head=body.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Head");
            foreach(var name in new[]{"Idle","CrouchIdle","Crouch"})
            {
                var c=body.GetClip(name);if(c==null)continue;c.SampleAnimation(body.gameObject,c.length*.25f);
                log.Add($"{name,-18} head pivot {body.transform.InverseTransformPoint(head.position).y*100:F0} cm above the feet");
                var old=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ChickenHeistGenerated/Characters/EliasNative/Backup/"+name+".anim");
                if(old!=null){old.SampleAnimation(body.gameObject,old.length*.25f);log.Add($"{name,-18} before Mixamo: head pivot {body.transform.InverseTransformPoint(head.position).y*100:F0} cm (lowest point {(Lowest(old,old.length*.25f)-reference)*100:+0;-0} cm)");}
            }
            Object.DestroyImmediate(baked);
        }
        finally{Object.DestroyImmediate(root);}
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("output/mixamo");File.WriteAllLines("output/mixamo/grounding.txt",log);
        Debug.Log("CLIP GROUNDING\n"+string.Join("\n",log));
    }
}
