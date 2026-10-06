using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

// Elias' house after years of debt: damp and peeling plaster, cracks, a ceiling stain over the leak bucket,
// sprung floorboards, cobwebs, dirty dishes, faded furniture, a yellowed fridge, dim warm bulbs (one flickering);
// an old doorbell at the front door; the coop gate becomes a working hinged gate (HomeCoopGate).
// Idempotent: everything it adds lives under "Desgaste - *" objects and is rebuilt on each run.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod HomeWornUpgrade.Install
public static class HomeWornUpgrade
{
    const string HomeName="Casa do Protagonista - Sitio do Recomeco",InteriorName="Interior - Uma vida por reconstruir";
    const string MaterialFolder="Assets/ChickenHeistGenerated/PlayerHome/Worn";
    static readonly List<string> log=new List<string>();
    static readonly Dictionary<string,Material> made=new Dictionary<string,Material>();

    public static void Install()
    {
        log.Clear();made.Clear();
        var scene=EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        const string backup="output/scene-backups/ChickenHeistRuralWorld-before-home-worn.unity";
        Directory.CreateDirectory("output/scene-backups");if(!File.Exists(backup))File.Copy(RuralWorldReview.WorldScene,backup);
        Directory.CreateDirectory(MaterialFolder);
        var home=GameObject.Find(HomeName)?.transform;if(home==null)throw new System.InvalidOperationException("Home not found");
        var interior=home.Find(InteriorName);if(interior==null)throw new System.InvalidOperationException("Interior not found");
        log.Add("home rotation "+home.eulerAngles+" position "+home.position);
        WearInterior(home,interior);
        Lights(interior);
        Doorbell(home);
        CoopGate(home);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        PrefabUtility.SaveAsPrefabAsset(home.gameObject,"Assets/ChickenHeistGenerated/PlayerHome/ProtagonistHome.prefab");
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory("output/home-review");File.WriteAllLines("output/home-review/worn-install.txt",log);
        Debug.Log("HOME WORN INSTALLED\n"+string.Join("\n",log));
    }

    static Material Mat(string name,Color color,float smooth=.04f)
    {
        if(made.TryGetValue(name,out var m))return m;
        string path=MaterialFolder+"/"+name+".mat";m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(m);made[name]=m;return m;
    }
    static GameObject Piece(Transform parent,string name,Vector3 p,Vector3 size,Material material,Vector3 euler=default,PrimitiveType type=PrimitiveType.Cube)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;go.transform.localRotation=Quaternion.Euler(euler);
        var r=go.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;return go;
    }
    static void Line(Transform parent,string name,Vector3 a,Vector3 b,float thickness,Material material)
    {
        var go=Piece(parent,name,(a+b)*.5f,new Vector3(thickness,thickness,Vector3.Distance(a,b)),material);go.transform.localRotation=Quaternion.LookRotation(b-a);
    }
    static Transform Fresh(Transform parent,string name){var old=parent.Find(name);if(old!=null)Object.DestroyImmediate(old.gameObject);var t=new GameObject(name).transform;t.SetParent(parent,false);return t;}

    // ---------------------------------------------------------------- interior (home-local, see PlayerHomeBuilder.Interior)
    static void WearInterior(Transform home,Transform interior)
    {
        var root=Fresh(interior,"Desgaste - interior");var rnd=new System.Random(41);float R(float a,float b)=>a+(float)rnd.NextDouble()*(b-a);
        var damp=Mat("Mancha de umidade",new Color(.41f,.39f,.31f));
        var dampDark=Mat("Umidade escura",new Color(.35f,.33f,.26f));
        var streak=Mat("Escorrido de goteira",new Color(.38f,.33f,.22f));
        var lath=Mat("Ripa exposta",new Color(.33f,.25f,.16f));
        var hole=Mat("Buraco no reboco",new Color(.11f,.09f,.07f));
        var crack=Mat("Rachadura",new Color(.13f,.12f,.10f));
        var gap=Mat("Fresta no assoalho",new Color(.035f,.03f,.025f));
        var dirt=Mat("Terra trazida das botas",new Color(.22f,.17f,.11f));
        var web=Mat("Teia de aranha",new Color(.66f,.66f,.62f),.2f);
        var card=Mat("Papelao umido",new Color(.42f,.33f,.21f));
        var plate=Mat("Louca lascada",new Color(.70f,.68f,.60f),.25f);
        var bottleGreen=Mat("Garrafa verde",new Color(.15f,.27f,.17f),.6f);
        var bottleBrown=Mat("Garrafa ambar",new Color(.30f,.17f,.07f),.6f);
        var paper=Mat("Jornal velho",new Color(.62f,.60f,.50f));
        var rust=Mat("Ferrugem",new Color(.36f,.19f,.09f));
        const float floor=.625f,ceiling=3.236f,back=6.812f,left=-7.412f,right=.952f,front=2.428f;
        // Organic stain: overlapping, slightly rotated patches (halo + core) on a wall plane.
        void Blot(string name,Vector3 c,bool alongX,float w,float h,Material halo,Material core,int count=7)
        {
            for(int i=0;i<count;i++)
            {
                bool inner=i>=count-2;float sw=w*R(.35f,.7f)*(inner?.6f:1),sh=h*R(.35f,.7f)*(inner?.6f:1);
                var offset=new Vector3(alongX?R(-w,w)*.32f:0,R(-h,h)*.3f,alongX?0:R(-w,w)*.32f);
                float depth=inner?.004f:.002f;
                var p=c+offset+(alongX?new Vector3(0,0,c.z>4?-depth:depth):new Vector3(c.x<-3?depth:-depth,0,0));
                var size=alongX?new Vector3(sw,sh,.008f):new Vector3(.008f,sh,sw);
                var euler=alongX?new Vector3(0,0,R(-12,12)):new Vector3(R(-12,12),0,0);
                Piece(root,name,p,size,inner?core:halo,euler);
            }
        }
        // damp along the base of the walls and around the leak corner
        Blot("Umidade no pe da parede",new Vector3(-5.4f,1.0f,back),true,2.6f,.75f,damp,dampDark,9);
        Blot("Umidade no canto da goteira",new Vector3(-6.9f,1.35f,back),true,1.0f,1.3f,damp,dampDark,7);
        Blot("Umidade na parede lateral",new Vector3(left,1.15f,4.5f),false,2.0f,1.0f,damp,dampDark,8);
        Blot("Umidade na parede do quarto",new Vector3(right,1.0f,3.4f),false,1.4f,.6f,damp,dampDark,6);
        Blot("Umidade sob a janela",new Vector3(-1.1f,1.0f,front),true,1.2f,.5f,damp,dampDark,5);
        Blot("Mancha de gordura atras do fogao",new Vector3(-3.98f,1.85f,back),true,.85f,.55f,dampDark,Mat("Fuligem",new Color(.27f,.25f,.20f)),5);
        // water streaks running down from the ceiling leak
        for(int i=0;i<5;i++)Piece(root,"Escorrido da goteira",new Vector3(-7.0f+i*.17f,ceiling-.55f-i*.05f,back-.002f),new Vector3(.06f+i%2*.03f,1.1f-i*.12f,.012f),streak);
        // peeled plaster with laths and bare boards behind
        var chunk=Mat("Caco de reboco",new Color(.52f,.50f,.42f));
        void Peel(string name,Vector3 c,bool alongX,float w,float h)
        {
            float sign=alongX?(c.z>4?-1:1):(c.x<-3?1:-1);
            Vector3 At(float u,float v,float d)=>c+(alongX?new Vector3(u,v,sign*d):new Vector3(sign*d,v,u));
            Vector3 Size(float a,float b,float d)=>alongX?new Vector3(a,b,d):new Vector3(d,b,a);
            Vector3 Tilt(float t)=>alongX?new Vector3(0,0,t):new Vector3(t,0,0);
            // ragged hole: a few rotated dark patches
            for(int i=0;i<4;i++)Piece(root,name,At(R(-w,w)*.2f,R(-h,h)*.2f,.001f),Size(w*R(.55f,.85f),h*R(.5f,.8f),.008f),hole,Tilt(R(-18,18)));
            // laths of uneven length and spacing behind the plaster
            float y=-h*.38f;
            while(y<h*.38f){Piece(root,name+" - ripa",At(R(-w,w)*.08f,y,.005f),Size(w*R(.45f,.85f),.04f,.008f),lath,Tilt(R(-3,3)));y+=R(.075f,.12f);}
            // broken plaster rim and the chunks that fell to the floor
            for(int i=0;i<5;i++)Piece(root,name+" - borda",At(R(-w,w)*.45f,R(-h,h)*.45f,.006f),Size(R(.06f,.16f),R(.04f,.1f),.014f),chunk,Tilt(R(0,90)));
            Vector3 below=At(0,0,0);below.y=floor+.012f;
            for(int i=0;i<6;i++)Piece(root,"Caco de reboco no chao",below+(alongX?new Vector3(R(-w,w)*.5f,0,sign*R(.05f,.35f)):new Vector3(sign*R(.05f,.35f),0,R(-w,w)*.5f)),new Vector3(R(.03f,.09f),R(.012f,.025f),R(.03f,.08f)),chunk,new Vector3(0,R(0,90),0));
        }
        Peel("Reboco caido acima da mesa",new Vector3(-5.9f,2.35f,back),true,.85f,.55f);
        Peel("Reboco caido perto da porta",new Vector3(left,2.25f,3.2f),false,.7f,.45f);
        Peel("Reboco caido no quarto",new Vector3(.3f,2.55f,back),true,.6f,.4f);
        // cracks: zig-zag lines on plaster
        void Crack(Vector3 start,Vector3 dir,int steps,bool alongX)
        {
            var p=start;
            for(int i=0;i<steps;i++)
            {
                var jitter=alongX?new Vector3(R(-.12f,.12f),0,0):new Vector3(0,0,R(-.12f,.12f));
                var q=p+dir*R(.14f,.24f)+jitter;Line(root,"Rachadura",p,q,.011f,crack);p=q;
            }
        }
        Crack(new Vector3(-2.6f,3.2f,back-.004f),Vector3.down,7,true);
        Crack(new Vector3(-3.95f,3.1f,front+.004f),Vector3.down+Vector3.left*.4f,5,true);
        Crack(new Vector3(right-.004f,3.2f,5.6f),Vector3.down,6,false);
        Crack(new Vector3(left+.004f,1.0f,6.2f),Vector3.up,5,false);
        // ceiling stain right above the leak bucket, a sagging board beside it
        Piece(root,"Mancha da goteira no forro",new Vector3(-6.7f,ceiling-.003f,4.55f),new Vector3(1.25f,.012f,1.0f),streak);
        Piece(root,"Centro escuro da goteira",new Vector3(-6.7f,ceiling-.006f,4.55f),new Vector3(.55f,.012f,.45f),dampDark);
        Piece(root,"Tabua do forro cedendo",new Vector3(-5.6f,ceiling-.05f,4.0f),new Vector3(.24f,.03f,1.6f),lath,new Vector3(4,0,3));
        // floor: open gaps, a sprung board, mud from the yard
        float[] gaps={-6.92f,-6.22f,-5.17f,-4.47f,-2.37f,-1.32f,-.27f};
        foreach(float x in gaps)Piece(root,"Fresta no assoalho",new Vector3(x,floor-.004f,R(3.0f,6.1f)),new Vector3(.03f,.012f,R(.6f,1.5f)),gap);
        Piece(root,"Tabua solta levantada",new Vector3(-3.55f,floor+.02f,4.8f),new Vector3(.33f,.035f,1.1f),lath,new Vector3(-3,0,2));
        Piece(root,"Buraco da tabua solta",new Vector3(-3.55f,floor-.003f,4.8f),new Vector3(.29f,.012f,1.05f),gap);
        for(int i=0;i<6;i++)Piece(root,"Terra trazida das botas",new Vector3(-3.2f+R(-.6f,.6f),floor+.003f,2.7f+i*.32f),new Vector3(R(.2f,.45f),.006f,R(.15f,.3f)),dirt,new Vector3(0,R(0,90),0));
        // cobwebs in the ceiling corners: a few radial threads and rings
        void Web(Vector3 corner,Vector3 a,Vector3 b)
        {
            for(int i=0;i<=4;i++){var d=Vector3.Slerp(a,b,i/4f);Line(root,"Teia",corner,corner+d*.48f,.006f,web);}
            for(int ring=1;ring<=3;ring++)for(int i=0;i<4;i++){var d1=Vector3.Slerp(a,b,i/4f);var d2=Vector3.Slerp(a,b,(i+1)/4f);Line(root,"Teia",corner+d1*.15f*ring,corner+d2*.15f*ring,.005f,web);}
        }
        Web(new Vector3(left+.01f,ceiling-.01f,back-.01f),Vector3.right,Vector3.down);
        Web(new Vector3(right-.01f,ceiling-.01f,back-.01f),Vector3.left,Vector3.down);
        Web(new Vector3(left+.01f,ceiling-.01f,front+.01f),Vector3.forward,Vector3.down);
        Web(new Vector3(-1.7f,ceiling-.01f,back-.01f),Vector3.left,Vector3.down);
        // clutter: boxes against the bedroom wall, bottles, dirty dishes on the stove, old newspaper on the floor
        for(int i=0;i<3;i++)Piece(root,"Caixa de papelao",new Vector3(.5f-i*.05f,floor+.21f+i*.38f,2.85f+i*.04f),new Vector3(.62f-i*.08f,.42f-i*.04f,.48f-i*.06f),card,new Vector3(0,i*9-6,0));
        Piece(root,"Caixa de papelao aberta",new Vector3(-.15f,floor+.15f,2.9f),new Vector3(.5f,.3f,.4f),card,new Vector3(0,22,0));
        for(int i=0;i<4;i++){var b=Piece(root,"Garrafa vazia",new Vector3(-6.95f+i*.11f,floor+.13f,6.55f-(i%2)*.1f),new Vector3(.07f,.13f,.07f),i%2==0?bottleGreen:bottleBrown,default,PrimitiveType.Cylinder);if(i==3)b.transform.localRotation=Quaternion.Euler(90,30,0);}
        for(int i=0;i<4;i++)Piece(root,"Prato sujo empilhado",new Vector3(-4.32f,1.565f+i*.018f,6.38f),new Vector3(.24f,.008f,.24f),plate,default,PrimitiveType.Cylinder);
        Piece(root,"Caneca lascada",new Vector3(-5.05f,1.42f,3.95f),new Vector3(.08f,.05f,.08f),plate,default,PrimitiveType.Cylinder);
        for(int i=0;i<3;i++)Piece(root,"Jornal velho no chao",new Vector3(-2.2f+i*.18f,floor+.004f+i*.002f,6.0f-i*.12f),new Vector3(.42f,.004f,.55f),paper,new Vector3(0,i*27-15,0));
        // the fridge: yellowed enamel with rust at the hinges and the base
        bool Fridge(Transform t){for(var x=t;x!=null && x!=interior;x=x.parent)if(x.name.ToLowerInvariant().Contains("geladeira"))return true;return false;}
        foreach(var r in interior.GetComponentsInChildren<Renderer>(true).Where(r=>Fridge(r.transform) && (r.name.StartsWith("Gabinete") || r.name.StartsWith("Porta d")) && !r.transform.IsChildOf(root)))
        {r.sharedMaterial=Mat("Esmalte amarelado da geladeira",new Color(.62f,.58f,.43f),.18f);log.Add("fridge part "+r.name);}
        foreach(var r in interior.GetComponentsInChildren<Renderer>(true).Where(r=>Fridge(r.transform) && !r.transform.IsChildOf(root)))
        {
            string n=r.name;
            if(n.StartsWith("Pe nivelador") || n.StartsWith("Aleta") || n.StartsWith("Rodape ventilado"))r.sharedMaterial=Mat("Metal escuro da geladeira",new Color(.13f,.13f,.12f),.3f);
            else if(n.StartsWith("Emblema"))r.sharedMaterial=Mat("Cromo opaco",new Color(.55f,.55f,.52f),.5f);
            else if(n.StartsWith("Esmalte lascado"))r.sharedMaterial=Mat("Lasca de esmalte",new Color(.19f,.16f,.12f));
            else if(n.StartsWith("Borracha"))r.sharedMaterial=Mat("Borracha ressecada",new Color(.08f,.08f,.08f));
        }
        var fridge=interior.GetComponentsInChildren<Renderer>(true).Where(r=>Fridge(r.transform) && !r.transform.IsChildOf(root)).Select(r=>r.bounds).DefaultIfEmpty().Aggregate((a,c)=>{a.Encapsulate(c);return a;});
        if(fridge.size!=Vector3.zero)
        {
            var c=home.InverseTransformPoint(fridge.center);var e=home.InverseTransformVector(fridge.extents);e=new Vector3(Mathf.Abs(e.x),Mathf.Abs(e.y),Mathf.Abs(e.z));
            float faceZ=c.z-e.z-.004f;
            Piece(root,"Ferrugem na base da geladeira",new Vector3(c.x,c.y-e.y+.09f,faceZ),new Vector3(e.x*1.7f,.1f,.01f),rust);
            Piece(root,"Ferrugem na dobradica",new Vector3(c.x+e.x*.85f,c.y+e.y*.6f,faceZ),new Vector3(.06f,.16f,.01f),rust);
            Piece(root,"Mancha na porta da geladeira",new Vector3(c.x-e.x*.3f,c.y,faceZ),new Vector3(.18f,.1f,.008f),damp);
            log.Add("fridge front at "+faceZ+" centre "+c);
        }
        // faded, grimy furniture: tint the pack materials (shared copies, the originals stay untouched)
        var cache=new Dictionary<Material,Material>();int tinted=0;
        foreach(var r in interior.GetComponentsInChildren<Renderer>(true))
        {
            if(r.transform.IsChildOf(root))continue;
            var top=r.transform;while(top.parent!=null && top.parent!=interior)top=top.parent;
            if(!(top.name.StartsWith("Prop_") || top.name=="RockingChair" || top.name=="Bucket"))continue;
            var mats=r.sharedMaterials;
            for(int i=0;i<mats.Length;i++)
            {
                var m=mats[i];if(m==null || m.name.StartsWith("Gasto - "))continue; // already faded on an earlier run
                if(!cache.TryGetValue(m,out var worn))
                {
                    string path=MaterialFolder+"/Gasto - "+m.name.Replace("/","_")+".mat";worn=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(worn==null){worn=new Material(m){name="Gasto - "+m.name};AssetDatabase.CreateAsset(worn,path);}
                    else worn.CopyPropertiesFromMaterial(m);
                    var col=m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):m.color;float grey=col.r*.3f+col.g*.59f+col.b*.11f;
                    var faded=Color.Lerp(new Color(grey,grey,grey),col,.45f)*.68f;faded.a=col.a;
                    if(worn.HasProperty("_BaseColor"))worn.SetColor("_BaseColor",faded);else worn.color=faded;
                    if(worn.HasProperty("_Smoothness"))worn.SetFloat("_Smoothness",.05f);
                    EditorUtility.SetDirty(worn);cache[m]=worn;
                }
                mats[i]=cache[m];
            }
            r.sharedMaterials=mats;tinted++;
        }
        var grime=Mat("Reboco encardido",new Color(.47f,.45f,.37f),.02f);var ceilingMat=Mat("Forro encardido",new Color(.42f,.39f,.32f),.02f);int walls=0;
        foreach(var r in interior.GetComponentsInChildren<Renderer>(true))
        {
            if(r.transform.IsChildOf(root) || r.sharedMaterial==null)continue;
            string n=r.sharedMaterial.name;
            if(n.StartsWith("Reboco antigo") || n=="Reboco encardido"){r.sharedMaterial=r.name.StartsWith("Forro")?ceilingMat:grime;walls++;}
        }
        log.Add("worn interior pieces "+root.childCount+", furniture renderers faded "+tinted+", plaster surfaces "+walls);
    }

    static void Lights(Transform interior)
    {
        var lights=interior.GetComponentsInChildren<Light>(true).Where(l=>l.name=="Luz de casa").ToArray();
        for(int i=0;i<lights.Length;i++)
        {
            var l=lights[i];l.color=new Color(1f,.69f,.40f);l.intensity=.72f;l.range=5.2f;
            var flicker=l.GetComponent<HomeLightFlicker>();
            if(i==0){if(flicker==null)flicker=l.gameObject.AddComponent<HomeLightFlicker>();flicker.baseIntensity=l.intensity;}
            else if(flicker!=null)Object.DestroyImmediate(flicker);
            log.Add("light "+i+" intensity "+l.intensity.ToString("0.00")+(i==0?" (flicker)":""));
        }
    }

    // ---------------------------------------------------------------- doorbell beside the front door
    static void Doorbell(Transform home)
    {
        var root=Fresh(home,"Desgaste - campainha velha");
        Vector3 Wall(Vector3 local,out Vector3 normal)
        {
            Vector3 from=home.TransformPoint(new Vector3(local.x,local.y,.6f));normal=-home.forward;
            foreach(var hit in Physics.RaycastAll(from,home.forward,3f).OrderBy(h=>h.distance))
            {if(hit.collider.transform.IsChildOf(root))continue;normal=hit.normal;return hit.point;}
            return home.TransformPoint(new Vector3(local.x,local.y,1.955f));
        }
        var plateMat=Mat("Baquelite amarelada",new Color(.66f,.60f,.44f),.3f);
        var buttonMat=Mat("Botao de latao gasto",new Color(.42f,.33f,.16f),.45f);
        var boxMat=Mat("Caixa da campainha oxidada",new Color(.30f,.24f,.17f),.2f);
        var wireMat=Mat("Fio velho da campainha",new Color(.09f,.08f,.07f));
        var tape=Mat("Fita isolante",new Color(.05f,.05f,.06f),.3f);
        var at=Wall(new Vector3(-2.29f,1.42f,0),out var n);
        root.position=at+n*.006f;root.rotation=Quaternion.LookRotation(-n,Vector3.up);
        Piece(root,"Espelho da campainha",Vector3.zero,new Vector3(.075f,.115f,.014f),plateMat);
        var button=Piece(root,"Botao da campainha",new Vector3(0,.012f,-.012f),new Vector3(.03f,.012f,.03f),buttonMat,new Vector3(90,0,0),PrimitiveType.Cylinder);
        Piece(root,"Fita isolante no fio",new Vector3(0,.075f,-.004f),new Vector3(.03f,.025f,.012f),tape);
        var chimeAt=Wall(new Vector3(-2.75f,2.62f,0),out var n2);
        var chime=Piece(root,"Caixa da campainha",root.InverseTransformPoint(chimeAt+n2*.03f),new Vector3(.12f,.09f,.05f),boxMat);
        Piece(root,"Sino enferrujado",chime.transform.localPosition+new Vector3(0,-.07f,-.01f),new Vector3(.08f,.04f,.08f),Mat("Sino enferrujado",new Color(.38f,.24f,.12f),.35f),new Vector3(0,0,0),PrimitiveType.Sphere);
        Line(root,"Fio exposto da campainha",new Vector3(0,.06f,.0f),chime.transform.localPosition+new Vector3(.0f,-.03f,.02f),.006f,wireMat);
        var bell=root.gameObject.AddComponent<HomeDoorbell>();bell.button=button.transform;bell.chime=chime.transform;
        bell.porchLight=home.Find("Lampada fraca da varanda")?.GetComponentInChildren<Light>();
        log.Add("doorbell at "+home.InverseTransformPoint(root.position)+" chime "+home.InverseTransformPoint(chimeAt)+" porch light "+(bell.porchLight!=null));
    }

    // ---------------------------------------------------------------- coop gate
    static void CoopGate(Transform home)
    {
        var coop=home.Find("Galinheiro gasto - Ultimo Recurso");if(coop==null){log.Add("coop not found");return;}
        var gate=coop.Find("Portinhola aberta - dobradica gasta")??coop.Find("Portinhola do galinheiro");
        if(gate==null){log.Add("coop gate not found");return;}
        gate.name="Portinhola do galinheiro";gate.localRotation=Quaternion.identity;
        if(gate.GetComponent<HomeCoopGate>()==null)gate.gameObject.AddComponent<HomeCoopGate>();
        // a latch block on the post, so the closed gate reads as shut
        var latch=gate.Find("Tramela de madeira");if(latch!=null)Object.DestroyImmediate(latch.gameObject);
        var wood=Mat("Madeira da tramela",new Color(.30f,.25f,.18f));
        var t=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;t.name="Tramela de madeira";Object.DestroyImmediate(t.GetComponent<Collider>());
        t.SetParent(gate,false);t.localPosition=new Vector3(1.04f,.62f,-.04f);t.localScale=new Vector3(.16f,.05f,.04f);t.GetComponent<Renderer>().sharedMaterial=wood;
        int colliders=gate.GetComponentsInChildren<Collider>().Length;
        log.Add("coop gate ready at "+coop.InverseTransformPoint(gate.position)+" colliders "+colliders);
    }
}
