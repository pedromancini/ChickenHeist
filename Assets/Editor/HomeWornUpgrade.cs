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
        for(int i=0;i<4;i++){var b=Piece(root,"Garrafa vazia",new Vector3(-6.95f+i*.11f,i==3?floor+.035f:floor+.13f,6.55f-(i%2)*.1f),new Vector3(.07f,.13f,.07f),i%2==0?bottleGreen:bottleBrown,default,PrimitiveType.Cylinder);if(i==3)b.transform.localRotation=Quaternion.Euler(90,30,0);}
        for(int i=0;i<4;i++)Piece(root,"Prato sujo empilhado",new Vector3(-4.32f,1.54f+i*.018f,6.38f),new Vector3(.24f,.008f,.24f),plate,default,PrimitiveType.Cylinder);
        // on the table top (1.41), clear of the phone
        Piece(root,"Caneca lascada",new Vector3(-5.75f,1.46f,3.22f),new Vector3(.08f,.05f,.08f),plate,default,PrimitiveType.Cylinder);
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
        // The wear never moves: static batching folds ~230 small pieces into a handful of draw calls.
        foreach(Transform t in root.GetComponentsInChildren<Transform>(true))GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.BatchingStatic|StaticEditorFlags.ContributeGI);
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
    // Everything stays right of the door frame (frame post ends at x -2.36; the leaf hinges on the left at x -3.96
    // and swings out over the porch), so nothing sits in the door's path.
    static void Doorbell(Transform home)
    {
        var root=Fresh(home,"Desgaste - campainha velha");
        float wallZ=2.253f;
        foreach(var hit in Physics.RaycastAll(home.TransformPoint(new Vector3(-2.1f,1.6f,.4f)),home.forward,3f).OrderBy(h=>h.distance))
        {if(hit.collider.transform.IsChildOf(root))continue;wallZ=home.InverseTransformPoint(hit.point).z;break;}
        root.localPosition=new Vector3(0,0,0);root.localRotation=Quaternion.identity;
        // home-local helpers: a point on the wall face, offset out toward the porch by d
        Vector3 W(float x,float y,float d)=>new Vector3(x,y,wallZ-d);
        var brass=Mat("Latao envelhecido",new Color(.46f,.36f,.18f),.45f);
        var brassDark=Mat("Latao escurecido",new Color(.27f,.21f,.11f),.35f);
        var ivory=Mat("Baquelite creme",new Color(.74f,.68f,.52f),.4f);
        var screw=Mat("Parafuso oxidado",new Color(.16f,.14f,.12f),.3f);
        var baseWood=Mat("Base de madeira da campainha",new Color(.24f,.17f,.11f));
        var coil=Mat("Bobina preta",new Color(.06f,.06f,.06f),.35f);
        var gong=Mat("Cupula de latao gasta",new Color(.52f,.40f,.20f),.55f);
        var wire=Mat("Fio velho da campainha",new Color(.09f,.08f,.07f));
        var clip=Mat("Grampo de fio",new Color(.55f,.53f,.48f),.2f);
        var tape=Mat("Fita isolante",new Color(.05f,.05f,.06f),.3f);
        // push button: stadium-shaped brass plate, dark bezel, ivory button, two screws
        float bx=-2.17f,by=1.98f; // 1.39 m above the porch floor (home-local y 0.59)
        var station=new GameObject("Botao da campainha - espelho").transform;station.SetParent(root,false);station.localPosition=W(bx,by,0);
        Piece(station,"Espelho de latao",new Vector3(0,0,-.006f),new Vector3(.07f,.09f,.012f),brass);
        Piece(station,"Ponta do espelho",new Vector3(0,.045f,-.006f),new Vector3(.07f,.006f,.07f),brass,new Vector3(90,0,0),PrimitiveType.Cylinder);
        Piece(station,"Ponta do espelho",new Vector3(0,-.045f,-.006f),new Vector3(.07f,.006f,.07f),brass,new Vector3(90,0,0),PrimitiveType.Cylinder);
        Piece(station,"Aro do botao",new Vector3(0,0,-.014f),new Vector3(.042f,.004f,.042f),brassDark,new Vector3(90,0,0),PrimitiveType.Cylinder);
        var button=Piece(station,"Botao de baquelite",new Vector3(0,0,-.02f),new Vector3(.026f,.007f,.026f),ivory,new Vector3(90,0,0),PrimitiveType.Cylinder);
        Piece(station,"Parafuso",new Vector3(0,.052f,-.013f),new Vector3(.008f,.002f,.008f),screw,new Vector3(90,0,0),PrimitiveType.Cylinder);
        Piece(station,"Parafuso",new Vector3(0,-.052f,-.013f),new Vector3(.008f,.002f,.008f),screw,new Vector3(90,0,0),PrimitiveType.Cylinder);
        // bell unit high on the wall, right of the frame: wooden base, coil, brass gong, striker
        float gx=-1.98f,gy=2.62f;
        var bell=new GameObject("Campainha de sino").transform;bell.SetParent(root,false);bell.localPosition=W(gx,gy,0);
        Piece(bell,"Base de madeira",new Vector3(0,0,-.011f),new Vector3(.15f,.21f,.022f),baseWood);
        Piece(bell,"Parafuso da base",new Vector3(.055f,.085f,-.023f),new Vector3(.01f,.002f,.01f),screw,new Vector3(90,0,0),PrimitiveType.Cylinder);
        Piece(bell,"Parafuso da base",new Vector3(-.055f,-.085f,-.023f),new Vector3(.01f,.002f,.01f),screw,new Vector3(90,0,0),PrimitiveType.Cylinder);
        Piece(bell,"Bobina do eletroima",new Vector3(0,-.055f,-.042f),new Vector3(.075f,.05f,.04f),coil);
        Piece(bell,"Bobina enrolada",new Vector3(-.018f,-.055f,-.065f),new Vector3(.024f,.022f,.024f),Mat("Cobre oxidado",new Color(.36f,.20f,.10f),.4f),new Vector3(0,0,90),PrimitiveType.Cylinder);
        Piece(bell,"Bobina enrolada",new Vector3(.018f,-.055f,-.065f),new Vector3(.024f,.022f,.024f),Mat("Cobre oxidado",new Color(.36f,.20f,.10f),.4f),new Vector3(0,0,90),PrimitiveType.Cylinder);
        Piece(bell,"Haste da cupula",new Vector3(0,.04f,-.03f),new Vector3(.012f,.016f,.012f),screw,new Vector3(90,0,0),PrimitiveType.Cylinder);
        Piece(bell,"Cupula de latao",new Vector3(0,.04f,-.05f),new Vector3(.12f,.12f,.045f),gong,default,PrimitiveType.Sphere);
        var hammer=new GameObject("Martelo da campainha").transform;hammer.SetParent(bell,false);hammer.localPosition=new Vector3(0,-.03f,-.075f);
        Piece(hammer,"Haste do martelo",new Vector3(0,.03f,0),new Vector3(.006f,.03f,.006f),screw);
        Piece(hammer,"Bola do martelo",new Vector3(0,.062f,0),new Vector3(.018f,.018f,.018f),brassDark,default,PrimitiveType.Sphere);
        // two old wires clipped to the wall, from the coil down to the button (all right of the frame)
        for(int k=0;k<2;k++)
        {
            float o=k*.008f;
            Line(root,"Fio velho da campainha",W(gx-.03f+o,gy-.1f,.005f),W(-2.1f+o,gy-.25f,.005f),.005f,wire);
            Line(root,"Fio velho da campainha",W(-2.1f+o,gy-.25f,.005f),W(-2.1f+o,by+.12f,.005f),.005f,wire);
            Line(root,"Fio velho da campainha",W(-2.1f+o,by+.12f,.005f),W(bx+o*.5f,by+.055f,.005f),.005f,wire);
        }
        for(float y=gy-.4f;y>by+.2f;y-=.28f)Piece(root,"Grampo de fio",W(-2.096f,y,.008f),new Vector3(.022f,.01f,.008f),clip);
        Piece(root,"Fita isolante na emenda",W(-2.096f,by+.3f,.009f),new Vector3(.024f,.03f,.012f),tape);
        var component=root.gameObject.AddComponent<HomeDoorbell>();component.button=button.transform;component.chime=bell;component.hammer=hammer;
        component.porchLight=home.Find("Lampada fraca da varanda")?.GetComponentInChildren<Light>();
        // The porch lantern hung where the opening door passes (37 cm from the hinge): move it past the leaf's sweep.
        var lantern=home.Find("Lanterna da varanda");
        if(lantern!=null){lantern.localPosition=new Vector3(-4.56f,lantern.localPosition.y,wallZ-.075f);log.Add("porch lantern moved to "+lantern.localPosition);}
        log.Add("doorbell button at "+W(bx,by,0)+" bell at "+W(gx,gy,0)+" wall z "+wallZ.ToString("F3"));
    }

    // ---------------------------------------------------------------- coop gate
    static void CoopGate(Transform home)
    {
        var coop=home.Find("Galinheiro gasto - Ultimo Recurso");if(coop==null){log.Add("coop not found");return;}
        var gate=coop.Find("Portinhola aberta - dobradica gasta")??coop.Find("Portinhola do galinheiro");
        if(gate==null){log.Add("coop gate not found");return;}
        // Hinge just off the face of the post (not its centre) and an 88 degree swing: the leaf clears the post and the wire.
        gate.name="Portinhola do galinheiro";gate.localRotation=Quaternion.identity;
        var hinge=gate.GetComponent<HomeCoopGate>();if(hinge==null)hinge=gate.gameObject.AddComponent<HomeCoopGate>();hinge.openAngle=-88;
        // Rebuilt leaf with clearances: the opening runs between post faces at x -1.45 and -0.45 (coop space);
        // the hinge sits 2 cm off the post face and every part stays inside the leaf.
        gate.localPosition=new Vector3(-1.43f,0,-2.2f);
        var woods=gate.GetComponentsInChildren<Renderer>(true).Select(r=>r.sharedMaterial).Where(m=>m!=null && !m.name.StartsWith("Madeira da tramela") && !m.name.StartsWith("Ferragem")).Distinct().ToList();
        if(woods.Count==0)woods.Add(Mat("Madeira da portinhola",new Color(.30f,.26f,.19f)));
        foreach(Transform child in gate.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
        var iron=Mat("Ferragem enferrujada",new Color(.20f,.14f,.10f),.25f);var latchWood=Mat("Madeira da tramela",new Color(.30f,.25f,.18f));
        GameObject Solid(string name,Vector3 p,Vector3 size,Material m,Vector3 euler=default)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(gate,false);go.transform.localPosition=p;go.transform.localScale=size;go.transform.localRotation=Quaternion.Euler(euler);go.GetComponent<Renderer>().sharedMaterial=m;return go;}
        float[] xs={.12f,.37f,.62f,.86f};float[] hs={1.0f,.96f,1.02f,.94f};
        for(int i=0;i<4;i++)Solid("Tabua da portinhola",new Vector3(xs[i],.08f+hs[i]*.5f,0),new Vector3(.22f,hs[i],.035f),woods[i%woods.Count],new Vector3(0,0,i==2?1.5f:0));
        Solid("Travessa de cima",new Vector3(.49f,.86f,-.032f),new Vector3(.9f,.09f,.03f),woods[(1)%woods.Count]);
        Solid("Travessa de baixo",new Vector3(.49f,.24f,-.032f),new Vector3(.9f,.09f,.03f),woods[(2)%woods.Count]);
        var a=new Vector3(.14f,.3f,-.032f);var b=new Vector3(.84f,.8f,-.032f);
        var brace=Solid("Travessa diagonal",(a+b)*.5f,new Vector3(.08f,Vector3.Distance(a,b),.028f),woods[(3)%woods.Count]);brace.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
        foreach(float y in new[]{.24f,.86f})
        {var strap=Solid("Tira de dobradica",new Vector3(.17f,y,-.05f),new Vector3(.3f,.035f,.006f),iron);Object.DestroyImmediate(strap.GetComponent<Collider>());}
        var latch=Solid("Tramela de madeira",new Vector3(1.0f,.58f,-.055f),new Vector3(.14f,.045f,.03f),latchWood,new Vector3(0,0,8));Object.DestroyImmediate(latch.GetComponent<Collider>());
        var nail=Solid("Prego da tramela",new Vector3(.95f,.58f,-.072f),new Vector3(.012f,.012f,.01f),iron);Object.DestroyImmediate(nail.GetComponent<Collider>());
        int colliders=gate.GetComponentsInChildren<Collider>().Length;
        log.Add("coop gate ready at "+coop.InverseTransformPoint(gate.position)+" colliders "+colliders);
    }
}
