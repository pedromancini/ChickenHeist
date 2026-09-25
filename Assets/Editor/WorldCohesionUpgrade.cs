using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

// Pulls the five art packs of the rural world towards one muted palette and gives the countryside
// some structure: open pastures between farms, woods where trees already cluster, weathered fences
// and dirt-coloured roads. Nothing is deleted: thinned trees are only deactivated, so hierarchy
// paths used by checkpoints stay the same.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod WorldCohesionUpgrade.Install
public static class WorldCohesionUpgrade
{
    const string Folder="Assets/ChickenHeistGenerated/World/Cohesion";
    public static void Install()
    {
        const string backup="output/scene-backups/ChickenHeistRuralWorld-before-world-cohesion.unity";
        Directory.CreateDirectory("output/scene-backups");
        if(!File.Exists(backup))File.Copy(RuralWorldReview.WorldScene,backup);
        var scene=EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/ChickenHeistGenerated/World","Cohesion");
        var log=new List<string>();
        Grade(log);MuteMaterials(log);MuteAtlases(log);Fences(log);Roads(log);Pastures(log);PorchLights(log);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("output/map-review");File.WriteAllLines("output/map-review/cohesion-install.txt",log);
    }

    // One global grade for every pack: less saturation, a little contrast, slightly warm.
    static void Grade(List<string> log)
    {
        string path=Folder+"/RuralNightGrade.asset";
        var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,path);}
        if(!profile.TryGet(out ColorAdjustments color))color=profile.Add<ColorAdjustments>(true);
        // Volume components must live inside the profile asset, or the saved profile comes back empty.
        if(!AssetDatabase.Contains(color)){color.name="ColorAdjustments";color.hideFlags=HideFlags.HideInInspector|HideFlags.HideInHierarchy;AssetDatabase.AddObjectToAsset(color,profile);}
        color.saturation.Override(-22);color.contrast.Override(10);color.colorFilter.Override(new Color(1,.965f,.92f));
        color.postExposure.Override(.25f);
        // Filmic finish for the night: neutral tonemapping keeps lamp highlights, bloom lets windows and
        // lanterns glow, a light vignette pulls the eye to the centre of the frame.
        T Component<T>() where T:VolumeComponent
        {
            if(!profile.TryGet(out T c))c=profile.Add<T>(true);
            if(!AssetDatabase.Contains(c)){c.name=typeof(T).Name;c.hideFlags=HideFlags.HideInInspector|HideFlags.HideInHierarchy;AssetDatabase.AddObjectToAsset(c,profile);}
            return c;
        }
        var tone=Component<Tonemapping>();tone.mode.Override(TonemappingMode.Neutral);
        var bloom=Component<Bloom>();bloom.threshold.Override(1.05f);bloom.intensity.Override(.6f);bloom.scatter.Override(.65f);bloom.tint.Override(new Color(1,.9f,.78f));
        var vignette=Component<Vignette>();vignette.intensity.Override(.24f);vignette.smoothness.Override(.45f);
        EditorUtility.SetDirty(profile);
        var go=GameObject.Find("Grade de cor rural")??new GameObject("Grade de cor rural");
        var volume=go.GetComponent<Volume>()??go.AddComponent<Volume>();volume.isGlobal=true;volume.priority=0;volume.sharedProfile=profile;
        int cameras=0;
        foreach(var cam in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {var data=cam.GetUniversalAdditionalCameraData();if(data!=null){data.renderPostProcessing=true;EditorUtility.SetDirty(data);cameras++;}}
        log.Add("Global grade: saturation -22, contrast +10, warm filter, exposure +0.25, neutral tonemapping, bloom, vignette; post-processing on "+cameras+" cameras");
    }

    // Untextured pack colours that read as neon at night.
    static void MuteMaterials(List<string> log)
    {
        // Vegetation materials are shared assets: mute them only once.
        const string marker=Folder+"/.vegetation-muted";
        bool alreadyMuted=File.Exists(marker);
        var seen=new HashSet<Material>();
        var forest=GameObject.Find("Floresta Low Poly");
        foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            // Vegetation only: gameplay signals (security lights, traps, padlocks) keep their colours.
            if(alreadyMuted || forest==null || !r.transform.IsChildOf(forest.transform))continue;
            foreach(var m in r.sharedMaterials)
            {
                if(m==null || !seen.Add(m) || !m.HasProperty("_BaseColor"))continue;
                var tex=m.GetTexture("_BaseMap");if(tex!=null)continue;
                string path=AssetDatabase.GetAssetPath(m);
                // Materials inside FBX files are read-only; the Quaternius barns get project copies below.
                if(path.EndsWith(".fbx",System.StringComparison.OrdinalIgnoreCase))continue;
                var c=m.GetColor("_BaseColor");Color.RGBToHSV(c,out float h,out float s,out float v);
                if(s<.45f || v<.4f)continue;
                float ns=s*.68f,nv=v*.82f;var muted=Color.HSVToRGB(h,ns,nv);muted.a=c.a;
                m.SetColor("_BaseColor",muted);EditorUtility.SetDirty(m);
                log.Add($"Muted {m.name} ({path}) s {s:0.00}->{ns:0.00} v {v:0.00}->{nv:0.00}");
            }
        }
        if(!alreadyMuted)File.WriteAllText(marker,"vegetation muted by WorldCohesionUpgrade");
        // Quaternius barns: bright red and pure white come from FBX-embedded materials.
        var copies=new Dictionary<Material,Material>();
        foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            var mats=r.sharedMaterials;bool changed=false;
            for(int i=0;i<mats.Length;i++)
            {
                var m=mats[i];if(m==null)continue;string path=AssetDatabase.GetAssetPath(m);
                if(!path.StartsWith("Assets/Quaternius_Farm_Buildings"))continue;
                if(!copies.TryGetValue(m,out var copy))
                {
                    var c=m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):m.color;Color.RGBToHSV(c,out float h,out float s,out float v);
                    // Pure white trim becomes off-white; greys and black roofs stay; reds and browns age.
                    var target=s<.1f?(v>.85f?new Color(.74f,.71f,.64f):c):Color.HSVToRGB(h,s*.72f,v*.78f);
                    copy=new Material(m){name=m.name+" - envelhecido"};copy.SetColor("_BaseColor",target);if(copy.HasProperty("_Smoothness"))copy.SetFloat("_Smoothness",.08f);
                    string copyPath=AssetDatabase.GenerateUniqueAssetPath(Folder+"/Quaternius "+m.name+".mat");AssetDatabase.CreateAsset(copy,copyPath);
                    copies[m]=copy;log.Add($"Barn {m.name}: {c} -> {target}");
                }
                mats[i]=copies[m];changed=true;
            }
            if(changed)r.sharedMaterials=mats;
        }
    }

    // Pack atlases carry the loudest colours (royal-blue roofs, candy-red silos, magenta trees).
    // Muted copies replace them; the originals stay untouched.
    static readonly string[] Atlases={"Assets/Pandazole_Ultimate_Pack/Pandazole Farm Ranch Pack/Textures/PandaMat.png","Assets/Prefabs/Environment/FreePack/FBX/ColorAtlas.png"};
    static void MuteAtlases(List<string> log)
    {
        foreach(var source in Atlases)
        {
            var original=AssetDatabase.LoadAssetAtPath<Texture2D>(source);if(original==null){log.Add("Missing atlas "+source);continue;}
            string target=Folder+"/"+Path.GetFileNameWithoutExtension(source)+" - rural.png";
            if(!File.Exists(target))
            {
                var tex=new Texture2D(2,2);tex.LoadImage(File.ReadAllBytes(source));var px=tex.GetPixels();
                for(int i=0;i<px.Length;i++){Color.RGBToHSV(px[i],out float h,out float sat,out float v);var c=Color.HSVToRGB(h,sat*.62f,v*.9f);c.a=px[i].a;px[i]=c;}
                tex.SetPixels(px);File.WriteAllBytes(target,tex.EncodeToPNG());Object.DestroyImmediate(tex);AssetDatabase.ImportAsset(target);
                var src=(TextureImporter)AssetImporter.GetAtPath(source);var dst=(TextureImporter)AssetImporter.GetAtPath(target);
                dst.filterMode=src.filterMode;dst.mipmapEnabled=src.mipmapEnabled;dst.textureCompression=src.textureCompression;dst.SaveAndReimport();
            }
            var muted=AssetDatabase.LoadAssetAtPath<Texture2D>(target);int count=0;
            foreach(var m in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).SelectMany(r=>r.sharedMaterials).Distinct())
            {
                if(m==null)continue;
                foreach(var name in m.GetTexturePropertyNames())if(m.GetTexture(name)==original){m.SetTexture(name,muted);EditorUtility.SetDirty(m);count++;}
            }
            log.Add($"Atlas {Path.GetFileName(source)} muted in {count} material slots");
        }
    }

    // Farm fences: weathered wood instead of bright white pickets.
    static void Fences(List<string> log)
    {
        var copies=new Dictionary<Material,Material>();int count=0;
        foreach(var holder in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Where(t=>t.name.EndsWith(" - Encaixe")))
            foreach(var r in holder.GetComponentsInChildren<Renderer>())
            {
                var mats=r.sharedMaterials;
                for(int i=0;i<mats.Length;i++)
                {
                    var m=mats[i];if(m==null)continue;
                    if(!copies.TryGetValue(m,out var copy))
                    {
                        // Already repainted fences keep their material; it is only reconfigured.
                        bool repainted=AssetDatabase.GetAssetPath(m).StartsWith(Folder);
                        string path=repainted?AssetDatabase.GetAssetPath(m):Folder+"/Cerca de madeira envelhecida "+copies.Count+".mat";
                        copy=AssetDatabase.LoadAssetAtPath<Material>(path);
                        Texture texture=m.GetTexturePropertyNames().Select(n=>m.GetTexture(n)).FirstOrDefault(t=>t!=null);
                        if(copy==null){copy=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(copy,path);}
                        // Pack shaders ignore _BaseColor, so the fence always uses URP Lit with the same texture.
                        copy.shader=Shader.Find("Universal Render Pipeline/Lit");copy.name="Cerca de madeira envelhecida";
                        copy.SetTexture("_BaseMap",texture);copy.SetColor("_BaseColor",texture!=null?new Color(.60f,.50f,.40f):new Color(.46f,.38f,.30f));
                        copy.SetFloat("_Smoothness",.05f);EditorUtility.SetDirty(copy);copies[m]=copy;
                    }
                    mats[i]=copies[m];
                }
                r.sharedMaterials=mats;count++;
            }
        log.Add("Fence renderers repainted: "+count+" using "+copies.Count+" materials");
    }

    static void Roads(List<string> log)
    {
        foreach(var m in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).SelectMany(r=>r.sharedMaterials).Distinct())
        {
            if(m==null || !m.name.StartsWith("Terra batida") || !m.HasProperty("_BaseColor"))continue;
            m.SetColor("_BaseColor",new Color(1f,.88f,.72f));EditorUtility.SetDirty(m);log.Add("Road tinted towards dry earth: "+m.name);
        }
    }

    // Scattered trees standing alone in open ground become pasture; clusters, road edges and farm surroundings stay.
    static void Pastures(List<string> log)
    {
        var forest=GameObject.Find("Floresta Low Poly");if(forest==null){log.Add("No forest root found");return;}
        const string marker="Pastos abertos - WorldCohesionUpgrade";
        if(forest.transform.Find(marker)!=null){log.Add("Pastures already opened; skipped");return;}
        new GameObject(marker).transform.SetParent(forest.transform,false);
        var trees=forest.transform.Cast<Transform>().Where(t=>t.gameObject.activeSelf && t.name.Contains("Tree")).ToList();
        var roads=Object.FindObjectsByType<RuralRoadSpan>(FindObjectsSortMode.None);
        var farms=Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None);
        var home=GameObject.Find("Casa do Protagonista - Sitio do Recomeco");
        var positions=trees.Select(t=>t.position).ToArray();
        var rng=new System.Random(20260924);int hidden=0;
        for(int i=0;i<trees.Count;i++)
        {
            var p=positions[i];
            int neighbours=0;for(int j=0;j<positions.Length && neighbours<4;j++)if(j!=i && (positions[j]-p).sqrMagnitude<14*14)neighbours++;
            if(neighbours>=3)continue;                                   // part of a wood
            if(roads.Any(r=>Segment(p,r.start,r.end)<14))continue;        // tree line along the road
            if(farms.Any(f=>Expand(f.lot,18).Contains(new Vector2(p.x,p.z))))continue;
            if(home!=null && (home.transform.position-p).sqrMagnitude<60*60)continue;
            if(rng.NextDouble()<.65){trees[i].gameObject.SetActive(false);hidden++;}
        }
        log.Add($"Pastures: {hidden} of {trees.Count} isolated trees deactivated");
    }
    // Warm practical light at every inhabited door: amber against the cold moonlight is what gives the
    // night depth. No shadows (cost), a small emissive lantern so bloom has something to catch.
    static void PorchLights(List<string> log)
    {
        var glass=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Lanterna acesa.mat");
        if(glass==null)
        {
            glass=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Lanterna acesa"};
            glass.SetColor("_BaseColor",new Color(1,.78f,.5f));glass.EnableKeyword("_EMISSION");glass.SetColor("_EmissionColor",new Color(1,.62f,.3f)*6f);
            glass.globalIlluminationFlags=MaterialGlobalIlluminationFlags.None;AssetDatabase.CreateAsset(glass,Folder+"/Lanterna acesa.mat");
        }
        int count=0;
        void Lantern(Transform anchor,Vector3 position,Vector3 facing,float intensity)
        {
            const string name="Lanterna da varanda";
            var old=anchor.Find(name);if(old!=null)Object.DestroyImmediate(old.gameObject);
            var root=new GameObject(name);root.transform.SetParent(anchor,true);root.transform.position=position;
            if(facing.sqrMagnitude>.01f)root.transform.rotation=Quaternion.LookRotation(new Vector3(facing.x,0,facing.z));
            var body=GameObject.CreatePrimitive(PrimitiveType.Cube);body.name="Vidro aceso";Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform,false);body.transform.localScale=new Vector3(.14f,.2f,.14f);
            var renderer=body.GetComponent<MeshRenderer>();renderer.sharedMaterial=glass;renderer.shadowCastingMode=ShadowCastingMode.Off;
            var light=root.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(1,.7f,.42f);light.intensity=intensity;light.range=9;light.shadows=LightShadows.None;
            count++;
        }
        foreach(var home in Object.FindObjectsByType<FarmerResidence>(FindObjectsSortMode.None))
        {
            if(home.outsideDoor==null || home.insideDoor==null)continue;
            var outward=home.outsideDoor.position-home.insideDoor.position;outward.y=0;
            Lantern(home.transform,home.outsideDoor.position-outward.normalized*.35f+Vector3.up*2.35f+Vector3.Cross(Vector3.up,outward.normalized)*.75f,outward,2.4f);
        }
        var protagonistHome=GameObject.Find("Casa do Protagonista - Sitio do Recomeco");
        var door=protagonistHome!=null?protagonistHome.GetComponentInChildren<HomeDoor>():null;
        if(door!=null)Lantern(protagonistHome.transform,door.hinge.position+Vector3.up*1.2f-door.hinge.forward*.4f,-door.hinge.forward,2f);
        var market=Object.FindFirstObjectByType<VillageMarket>();
        if(market!=null)Lantern(market.transform,market.counter.position+Vector3.up*1.9f-market.counter.forward*.2f,market.counter.forward,2.6f);
        log.Add("Porch lanterns: "+count);
    }
    static Rect Expand(Rect r,float m)=>new Rect(r.xMin-m,r.yMin-m,r.width+2*m,r.height+2*m);
    static float Segment(Vector3 p,Vector3 a,Vector3 b){p.y=a.y=b.y=0;var d=b-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,d)/Mathf.Max(1e-4f,d.sqrMagnitude));return Vector3.Distance(p,a+d*t);}
}
