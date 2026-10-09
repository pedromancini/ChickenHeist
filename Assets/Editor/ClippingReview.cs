using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Clipping audit: how far animated bodies go into the scene's geometry.
// 1. Elias in each action at its real spot (picking up a chicken, carrying, crouching, driving, ignition, trading at
//    the market): first-person and outside views, plus the vertices of the body that end up inside colliders.
// 2. Residents, farmers, the merchant and the farm animals during 45 s of free play, sampled every half second.
// Penetration: a vertex is inside a collider when a tiny overlap finds it (primitives, convex meshes) or when rays
// from it hit the back of the same mesh collider in two directions (closed meshes). Depth: shortest way out along the
// six axes. Report: output/clipping-review/report.txt.
// Run: Unity.exe -batchmode -projectPath . -executeMethod ClippingReview.Begin
[InitializeOnLoad]
public static class ClippingReview
{
    const string Key="ClippingReview",Folder="output/clipping-review";
    static readonly List<string> log=new List<string>();
    static int step;static float next,sampleUntil,nextSample;static HeistGameManager game;static Camera eyes;
    static RuralCharacterAnimator driver;static InteractableChicken bird;static int shots;
    static readonly Dictionary<string,(float depth,string where,string time)> worst=new Dictionary<string,(float,string,string)>();
    static readonly HashSet<string> captured=new HashSet<string>();
    static ClippingReview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void Begin()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);Directory.CreateDirectory(Folder);
        foreach(var f in Directory.GetFiles(Folder,"*.png"))File.Delete(f);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();
        economy.editorTestSavePath=System.IO.Path.GetFullPath("Temp/clipping-review-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount{balance=300});
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){step=0;log.Clear();worst.Clear();captured.Clear();shots=0;next=Time.realtimeSinceStartup+3;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.delayCall+=()=>EditorApplication.Exit(0);}
    }

    // ---------------------------------------------------------------- penetration
    static readonly Vector3[] Axes={Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back};
    static readonly Collider[] buffer=new Collider[16];
    struct Hit{public Vector3 point;public Collider collider;public float depth;public string part;}
    static bool Ignored(Collider c,Transform self)=>c==null || c.isTrigger || c is CharacterController || (self!=null && c.transform.IsChildOf(self));
    static float Depth(Vector3 p,Collider c)
    {
        float best=float.PositiveInfinity;
        foreach(var d in Axes)if(c.Raycast(new Ray(p+d*3,-d),out var h,3.01f))best=Mathf.Min(best,3-h.distance);
        return best;
    }
    static Collider Inside(Vector3 p,Transform self)
    {
        int n=Physics.OverlapSphereNonAlloc(p,.0015f,buffer,~0,QueryTriggerInteraction.Ignore);
        for(int k=0;k<n;k++)if(!Ignored(buffer[k],self) && (!(buffer[k] is MeshCollider mc) || mc.convex))return buffer[k];
        Collider first=null;int agree=0;
        foreach(var d in new[]{Vector3.up,Vector3.forward,Vector3.right})
        {
            if(!Physics.Raycast(p,d,out var h,4,~0,QueryTriggerInteraction.Ignore) || Ignored(h.collider,self) || Vector3.Dot(h.normal,d)<=0)continue;
            if(first==null)first=h.collider;if(h.collider==first)agree++;
        }
        return agree>=2?first:null;
    }
    static List<Hit> Penetrations(Renderer r,Transform self,int stride,float minDepth)
    {
        var hits=new List<Hit>();Vector3[] verts;BoneWeight[] weights=null;Transform[] bones=null;
        if(r is SkinnedMeshRenderer sk && sk.sharedMesh!=null)
        {var baked=new Mesh();sk.BakeMesh(baked,true);verts=baked.vertices;Object.DestroyImmediate(baked);weights=sk.sharedMesh.boneWeights;bones=sk.bones;}
        else{var mf=r.GetComponent<MeshFilter>();if(mf==null || mf.sharedMesh==null || !mf.sharedMesh.isReadable)return hits;verts=mf.sharedMesh.vertices;}
        bool back=Physics.queriesHitBackfaces;Physics.queriesHitBackfaces=true;
        try
        {
            for(int i=0;i<verts.Length;i+=stride)
            {
                var p=r.transform.TransformPoint(verts[i]);var c=Inside(p,self);if(c==null)continue;
                float depth=Depth(p,c);if(float.IsInfinity(depth) || depth<minDepth)continue;
                string part=weights!=null && bones!=null && weights.Length>i && bones[weights[i].boneIndex0]!=null?bones[weights[i].boneIndex0].name:r.name;
                hits.Add(new Hit{point=p,collider=c,depth=depth,part=part});
            }
        }
        finally{Physics.queriesHitBackfaces=back;}
        return hits;
    }
    static string Describe(IEnumerable<Hit> hits,int top=8)
    {
        var groups=hits.GroupBy(h=>(h.collider!=null?PathOf(h.collider.transform,3):"?")+" <- "+Simplify(h.part))
            .Select(g=>(g.Key,max:g.Max(h=>h.depth),count:g.Count())).OrderByDescending(g=>g.max).Take(top);
        return string.Join("\n",groups.Select(g=>$"    {g.max*100,5:F1} cm  x{g.count,-4} {g.Key}"));
    }
    static string Simplify(string bone)=>bone.TrimEnd('L','R','1','2','3');
    static string PathOf(Transform t,int depth){var s=t.name;var p=t.parent;for(int i=1;i<depth && p!=null;i++,p=p.parent)s=p.name+"/"+s;return s;}

    // ---------------------------------------------------------------- rendering
    static void Save(Camera c,string name,int mask=-1)
    {
        var rt=new RenderTexture(960,600,24);var old=c.targetTexture;int oldMask=c.cullingMask;if(mask!=-1)c.cullingMask=mask;
        c.targetTexture=rt;c.Render();var active=RenderTexture.active;RenderTexture.active=rt;
        var tex=new Texture2D(960,600,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,960,600),0,0);tex.Apply();
        File.WriteAllBytes(Folder+"/"+name+".png",tex.EncodeToPNG());RenderTexture.active=active;c.targetTexture=old;c.cullingMask=oldMask;
        Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);
    }
    // Outside view of the scene with Elias' full body (layer 31) shown and the first-person copy (layer 30) hidden.
    // Around the subject, the first viewpoint with a clear line to it (posts and walls otherwise fill the frame).
    static Vector3 Clear(Vector3 look,Vector3 preferred)
    {
        float dist=preferred.magnitude;var flat=Vector3.ProjectOnPlane(preferred,Vector3.up);float up=preferred.y;
        for(int a=0;a<24;a++)
        {
            var dir=Quaternion.AngleAxis((a%2==0?1:-1)*(a/2)*15,Vector3.up)*flat;var from=look+dir+Vector3.up*up;
            var hits=Physics.RaycastAll(look,(from-look).normalized,dist,~0,QueryTriggerInteraction.Ignore);
            if(hits.All(h=>h.transform.IsChildOf(game.player)))return from;
        }
        return look+preferred;
    }
    static void Outside(string name,Vector3 from,Vector3 look,float fov=45)
    {
        var go=new GameObject("clip cam");var cam=go.AddComponent<Camera>();cam.CopyFrom(eyes);cam.fieldOfView=fov;cam.nearClipPlane=.02f;
        go.transform.position=from;go.transform.LookAt(look);
        var bodies=game.player.GetComponentsInChildren<Renderer>(true).Where(r=>r.gameObject.layer==31).ToList();
        var modes=bodies.ToDictionary(r=>r,r=>r.shadowCastingMode);foreach(var r in bodies)if(r.shadowCastingMode==ShadowCastingMode.ShadowsOnly)r.shadowCastingMode=ShadowCastingMode.On;
        Save(cam,name,(~0 & ~(1<<30))|(1<<31));
        foreach(var kv in modes)kv.Key.shadowCastingMode=kv.Value;Object.DestroyImmediate(go);
    }
    static void Measure(string name)
    {
        var self=game.player;var hits=new List<Hit>();
        foreach(var r in self.GetComponentsInChildren<SkinnedMeshRenderer>(true).Where(r=>r.gameObject.layer==31 || r.enabled))hits.AddRange(Penetrations(r,self,2,.01f));
        var fp=self.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r=>r.name=="Corpo em primeira pessoa");
        log.Add($"{name}: state {driver.CurrentState}, first-person copy {(fp!=null && fp.enabled?"shown":"hidden")}, {hits.Count} vertices more than 1 cm inside geometry"+(hits.Count>0?" (max "+(hits.Max(h=>h.depth)*100).ToString("F1")+" cm)":""));
        if(hits.Count>0)log.Add(Describe(hits));
    }
    static void Position(Vector3 p,Vector3 face)
    {
        // stand on whatever is under the spot (the coop floor is raised above the ground)
        foreach(var h in Physics.RaycastAll(p+Vector3.up*1.2f,Vector3.down,3,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            if(!h.transform.IsChildOf(game.player) && h.collider.GetComponentInParent<InteractableChicken>()==null){p.y=h.point.y;break;}
        var cc=game.player.GetComponent<CharacterController>();p.y+=cc.skinWidth;cc.enabled=false;game.player.position=p;
        face.y=0;if(face.sqrMagnitude>1e-4f)game.player.rotation=Quaternion.LookRotation(face);cc.enabled=true;Physics.SyncTransforms();
    }

    // ---------------------------------------------------------------- free-play sampling
    static IEnumerable<(string label,Transform root,Renderer[] renderers)> Actors()
    {
        foreach(var w in Object.FindObjectsByType<RoadsideWalker>(FindObjectsSortMode.None))yield return ("morador "+w.name,w.transform,w.GetComponentsInChildren<Renderer>());
        foreach(var f in Object.FindObjectsByType<FarmerStateMachine>(FindObjectsSortMode.None))yield return ("fazendeiro "+(f.GetComponentInParent<FarmLayoutInfo>()?.identity??f.name),f.transform,f.GetComponentsInChildren<Renderer>());
        var market=Object.FindAnyObjectByType<VillageMarket>();if(market!=null && market.merchant!=null)yield return ("comerciante",market.merchant.transform,market.merchant.GetComponentsInChildren<Renderer>());
        foreach(var a in Object.FindObjectsByType<SimpleAnimalWander>(FindObjectsSortMode.None))yield return ("vaca "+(a.GetComponentInParent<FarmLayoutInfo>()?.identity??"?"),a.transform,a.GetComponentsInChildren<Renderer>());
        foreach(var c in Object.FindObjectsByType<InteractableChicken>(FindObjectsSortMode.None))yield return ("galinha "+(c.GetComponentInParent<FarmLayoutInfo>()?.identity??"sitio"),c.transform,c.GetComponentsInChildren<Renderer>());
    }
    static void Sample()
    {
        foreach(var (label,root,renderers) in Actors())
        {
            if(root==null || !root.gameObject.activeInHierarchy)continue;
            var hits=new List<Hit>();
            foreach(var r in renderers.Where(r=>r!=null && r.enabled && r.gameObject.activeInHierarchy))hits.AddRange(Penetrations(r,root,label.StartsWith("galinha")?3:5,.03f));
            // the ground under feet and hooves is expected to be touched: only count it past 5 cm
            hits=hits.Where(h=>!(IsGround(h.collider) && h.depth<.05f) && !(h.depth>.4f && h.collider.bounds.Contains(root.position+Vector3.up*.5f))).ToList();
            if(hits.Count==0)continue;
            var top=hits.OrderByDescending(h=>h.depth).First();
            string key=label;string where=PathOf(top.collider.transform,3)+" <- "+Simplify(top.part)+" ("+hits.Count+" vertices)";
            if(!worst.TryGetValue(key,out var w) || top.depth>w.depth)worst[key]=(top.depth,where,Time.time.ToString("F1")+" s at "+root.position.ToString("F1"));
            if(top.depth>.08f && shots<14 && captured.Add(label.Split(' ')[0]+top.collider.name))
            {
                shots++;var look=top.point;var away=(root.position-look);away.y=0;
                var dir=(Quaternion.AngleAxis(35,Vector3.up)*(away.sqrMagnitude>.01f?away.normalized:Vector3.forward)*-1f+Vector3.up*.45f).normalized;
                Outside("npc-"+shots.ToString("00")+"-"+label.Split(' ')[0],look+dir*(label.StartsWith("galinha")?1.2f:3.2f),look,40);
                log.Add($"  image npc-{shots:00}: {label}: {top.depth*100:F1} cm into {where}");
            }
        }
    }
    static bool IsGround(Collider c){var n=c.name.ToLowerInvariant();return n.Contains("terreno") || n.Contains("terrain") || n.Contains("chao") || n.Contains("estrada") || n.Contains("road") || n.Contains("assoalho") || n.Contains("piso") || n.Contains("ground") || c is TerrainCollider;}

    // ---------------------------------------------------------------- steps
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try
        {
            float wait=1.5f;
            switch(step)
            {
                case 0:
                    game=HeistGameManager.Instance;GameMenu.Instance.SendMessage("Resume",SendMessageOptions.DontRequireReceiver);
                    eyes=game.player.GetComponentInChildren<Camera>();driver=game.player.GetComponentsInChildren<RuralCharacterAnimator>().First(d=>d.GetComponent<ProtagonistArticulation>()!=null);
                    foreach(var l in game.player.GetComponentsInChildren<PlayerLook>())l.enabled=false;
                    game.player.GetComponent<PlayerMovement>().enabled=false;
                    bird=Object.FindObjectsByType<InteractableChicken>().Where(b=>b.coop!=null).OrderBy(b=>b.GetComponentInParent<FarmLayoutInfo>().layoutIndex).First();
                    game.StartMission(Array.IndexOf(ProtagonistPhone.Instance.farmNames,bird.GetComponentInParent<FarmLayoutInfo>().identity));
                    bird.coop.RestoreOpen(true);
                    {
                        // a spot about a metre from the bird where a standing body fits (as the controller would allow)
                        var cc0=game.player.GetComponent<CharacterController>();Vector3 spot=bird.transform.position-Vector3.forward*1.1f;
                        for(int a=0;a<16;a++)
                        {
                            var dir=Quaternion.Euler(0,a*22.5f,0)*Vector3.back;var p=bird.transform.position+dir*1.0f;
                            if(!Physics.CheckCapsule(p+Vector3.up*(cc0.radius+.12f),p+Vector3.up*(1.7f-cc0.radius),cc0.radius+.05f,~0,QueryTriggerInteraction.Ignore)){spot=p;break;}
                        }
                        Position(spot,bird.transform.position-spot);
                    }
                    eyes.transform.localRotation=Quaternion.Euler(38,0,0);
                    break;
                case 1:
                    log.Add("== Elias");
                    Check(bird.TrySteal(),"pickup starts");wait=.28f;break;
                case 2: Measure("pickup 25%");Save(eyes,"elias-pickup-1-eyes");Outside("elias-pickup-1-out",Clear(game.player.position+Vector3.up*.7f,game.player.right*1.7f+game.player.forward*.9f+Vector3.up*.4f),game.player.position+Vector3.up*.7f);wait=.27f;break;
                case 3: Measure("pickup 50%");Save(eyes,"elias-pickup-2-eyes");Outside("elias-pickup-2-out",Clear(game.player.position+Vector3.up*.7f,game.player.right*1.7f+game.player.forward*.9f+Vector3.up*.4f),game.player.position+Vector3.up*.7f);wait=.27f;break;
                case 4: Measure("pickup 80%");Save(eyes,"elias-pickup-3-eyes");Outside("elias-pickup-3-out",Clear(game.player.position+Vector3.up*.7f,game.player.right*1.7f+game.player.forward*.9f+Vector3.up*.4f),game.player.position+Vector3.up*.7f);wait=1.2f;break;
                case 5:
                    eyes.transform.localRotation=Quaternion.Euler(20,0,0);
                    Measure("carry");Save(eyes,"elias-carry-eyes");Outside("elias-carry-out",Clear(game.player.position+Vector3.up*1f,game.player.right*1.6f+game.player.forward*1.4f+Vector3.up*.3f),game.player.position+Vector3.up*1f);
                    {var mv0=game.player.GetComponent<PlayerMovement>();mv0.RestorePosture(true);
                     // as in play: the crouched controller and the view height the movement code gives it
                     var cc1=game.player.GetComponent<CharacterController>();cc1.height=mv0.alturaAgachado;cc1.center=Vector3.up*(cc1.height*.5f);
                     eyes.transform.localPosition=new Vector3(0,cc1.height-.35f,0);}
                    eyes.transform.localRotation=Quaternion.Euler(10,0,0);wait=1.5f;break;
                case 6:
                    Measure("carry crouched");Save(eyes,"elias-carry-crouch-eyes");
                    {var headB=game.player.GetComponentsInChildren<Transform>().First(b=>b.name=="Head");
                     log.Add($"  crouched view {game.player.InverseTransformPoint(eyes.transform.position).y*100:F0} cm high, head {game.player.InverseTransformPoint(headB.position).y*100:F0} cm, view {game.player.InverseTransformPoint(eyes.transform.position).z*100:F0} cm in front");}Outside("elias-carry-crouch-out",game.player.position+game.player.right*1.6f+game.player.forward*1.4f+Vector3.up*1.1f,game.player.position+Vector3.up*.7f);
                    game.backpack.RestoreCount(0);var mv=game.player.GetComponent<PlayerMovement>();mv.estaMovendo=true;wait=1.2f;break;
                case 7:
                    Measure("crouch walk");Outside("elias-crouch-walk-out",game.player.position+game.player.right*1.8f+game.player.forward*1.2f+Vector3.up*1.1f,game.player.position+Vector3.up*.6f);
                    var m2=game.player.GetComponent<PlayerMovement>();m2.estaMovendo=false;m2.RestorePosture(false);
                    {var cc2=game.player.GetComponent<CharacterController>();cc2.height=m2.alturaEmPe;cc2.center=Vector3.up*(cc2.height*.5f);}
                    game.EndActiveMission();
                    var truck=OldPickupTruck.Instance;Position(truck.seat.position-truck.transform.right,truck.transform.forward);
                    eyes.transform.localRotation=Quaternion.identity;Check(truck.EnterDriver(),"enter truck");wait=2f;break;
                case 8:
                {
                    var t=OldPickupTruck.Instance;
                    // the cab collision box contains the driver by design: measure against the visible parts instead
                    var shells=t.GetComponentsInChildren<Collider>().Where(c=>c.enabled && !c.isTrigger).ToList();foreach(var c in shells)c.enabled=false;
                    var temp=new List<MeshCollider>();
                    foreach(var mr in t.GetComponentsInChildren<MeshRenderer>())
                    {
                        var mf=mr.GetComponent<MeshFilter>();if(mf==null || mf.sharedMesh==null || mr.name=="Steering")continue;
                        if(Vector3.Distance(mr.bounds.ClosestPoint(t.seat.position),t.seat.position)>1.6f)continue;
                        var mc=mr.gameObject.AddComponent<MeshCollider>();mc.sharedMesh=mf.sharedMesh;temp.Add(mc);
                    }
                    Physics.SyncTransforms();
                    Measure("driving (against the visible truck parts)");
                    foreach(var mc in temp)Object.DestroyImmediate(mc);foreach(var c in shells)c.enabled=true;Physics.SyncTransforms();
                    eyes.transform.localRotation=Quaternion.Euler(40,0,0);Save(eyes,"elias-drive-eyes-down");
                    eyes.transform.localRotation=Quaternion.Euler(25,-70,0);Save(eyes,"elias-drive-eyes-door");
                    eyes.transform.localRotation=Quaternion.Euler(25,70,0);Save(eyes,"elias-drive-eyes-right");
                    eyes.transform.localRotation=Quaternion.identity;
                    Vector3 side=Vector3.ProjectOnPlane(t.seat.position-t.transform.position,Vector3.up).normalized;
                    Vector3 fwd=Vector3.ProjectOnPlane(t.steeringWheel.position-t.seat.position,Vector3.up).normalized;
                    // the driver in the cab with the roof, door and windows hidden: from above the door and from the front
                    var chest=game.player.GetComponentsInChildren<Transform>().First(b=>b.name=="Chest").position;
                    var hidden=t.GetComponentsInChildren<Renderer>().Where(r=>r.enabled && (r.bounds.min.y>chest.y-.05f || r.name.ToLowerInvariant().Contains("porta") || r.name.ToLowerInvariant().Contains("door") || r.name.ToLowerInvariant().Contains("vidro") || r.name.ToLowerInvariant().Contains("glass") || r.name.ToLowerInvariant().Contains("window"))).ToList();
                    foreach(var r in hidden)r.enabled=false;
                    Outside("elias-drive-cab-side",chest+side*1.5f+Vector3.up*.7f+fwd*.2f,chest-Vector3.up*.25f,55);
                    Outside("elias-drive-cab-top",chest+Vector3.up*1.6f+side*.35f-fwd*.15f,chest-Vector3.up*.3f+fwd*.25f,60);
                    Outside("elias-drive-cab-front",chest+fwd*1.4f+Vector3.up*.5f+side*.25f,chest-Vector3.up*.3f,55);
                    foreach(var r in hidden)r.enabled=true;
                    log.Add("  (cab views: "+hidden.Count+" truck parts hidden: roof, door, windows)");
                    t.ignition.Begin();wait=1.2f;break;
                }
                case 9:
                {
                    var t=OldPickupTruck.Instance;
                    var shells2=t.GetComponentsInChildren<Collider>().Where(c=>c.enabled && !c.isTrigger).ToList();foreach(var c in shells2)c.enabled=false;
                    var temp2=new List<MeshCollider>();
                    foreach(var mr in t.GetComponentsInChildren<MeshRenderer>())
                    {
                        var mf=mr.GetComponent<MeshFilter>();if(mf==null || mf.sharedMesh==null || mr.name=="Steering")continue;
                        if(Vector3.Distance(mr.bounds.ClosestPoint(t.seat.position),t.seat.position)>1.6f)continue;
                        var mc=mr.gameObject.AddComponent<MeshCollider>();mc.sharedMesh=mf.sharedMesh;temp2.Add(mc);
                    }
                    Physics.SyncTransforms();Measure("ignition (against the visible truck parts)");
                    foreach(var mc in temp2)Object.DestroyImmediate(mc);foreach(var c in shells2)c.enabled=true;Physics.SyncTransforms();
                    eyes.transform.localRotation=Quaternion.Euler(35,15,0);Save(eyes,"elias-ignition-eyes");eyes.transform.localRotation=Quaternion.identity;
                    t.ForceExit(t.transform.position+t.transform.right*2.4f);
                    var market=Object.FindAnyObjectByType<VillageMarket>();
                    if(market!=null && market.counter!=null)
                    {
                        // stand on the open side of the counter, facing it
                        Vector3 best=market.counter.position;float bestScore=-1;
                        foreach(var d in new[]{market.counter.forward,-market.counter.forward,market.counter.right,-market.counter.right})
                        {
                            var p=market.counter.position+d*.75f;p.y=market.counter.position.y;
                            if(Physics.Raycast(p+Vector3.up*1.5f,Vector3.down,out var g,4,~0,QueryTriggerInteraction.Ignore))p.y=g.point.y;
                            bool free=!Physics.CheckCapsule(p+Vector3.up*.4f,p+Vector3.up*1.5f,.3f,~0,QueryTriggerInteraction.Ignore);
                            float score=(free?10:0)-Vector3.Distance(p,market.merchant!=null?market.merchant.transform.position:p)*-1f;
                            if(free && score>bestScore){bestScore=score;best=p;}
                        }
                        Position(best+Vector3.up*.02f,market.counter.position-best);
                        driver.Gesture();
                    }
                    wait=1.1f;break;
                }
                case 10:
                {
                    Measure("trade (market)");eyes.transform.localRotation=Quaternion.Euler(25,0,0);Save(eyes,"elias-trade-eyes");eyes.transform.localRotation=Quaternion.identity;
                    Outside("elias-trade-out",game.player.position+game.player.right*1.5f-game.player.forward*.6f+Vector3.up*1.4f,game.player.position+game.player.forward*.5f+Vector3.up*1f);
                    // back home, free play for the residents, farmers and animals
                    Position(HouseholdEconomy.Instance.home.position+HouseholdEconomy.Instance.home.forward*-6,Vector3.forward);
                    log.Add("== residents, farmers, merchant and animals (45 s of free play, worst moment of each)");
                    var cowRadii=Object.FindObjectsByType<SimpleAnimalWander>(FindObjectsSortMode.None).Select(c=>c.GetComponent<FarmAnimalBoundary>()).Where(b=>b!=null).Select(b=>b.radius).ToList();
                    if(cowRadii.Count>0)log.Add($"  cow spacing radius {cowRadii.Min():F2}..{cowRadii.Max():F2} m");
                    sampleUntil=Time.time+45;nextSample=0;wait=.1f;break;
                }
                case 11:
                    if(Time.time<sampleUntil)
                    {
                        if(Time.time>=nextSample){Sample();nextSample=Time.time+.5f;}
                        next=Time.realtimeSinceStartup+.05f;return;
                    }
                    foreach(var kv in worst.OrderByDescending(k=>k.Value.depth))log.Add($"  {kv.Value.depth*100,5:F1} cm  {kv.Key}: {kv.Value.where} ({kv.Value.time})");
                    if(worst.Count==0)log.Add("  nothing more than 3 cm inside geometry");
                    File.WriteAllLines(Folder+"/report.txt",log);EditorApplication.isPlaying=false;return;
            }
            step++;next=Time.realtimeSinceStartup+wait;
        }
        catch(Exception e){log.Add("ERROR "+e);File.WriteAllLines(Folder+"/report.txt",log);EditorApplication.isPlaying=false;}
    }
    static void Check(bool ok,string what){if(!ok)log.Add("FAIL "+what);}
}
