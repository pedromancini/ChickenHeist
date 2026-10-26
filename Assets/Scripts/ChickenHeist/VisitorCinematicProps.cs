using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class VisitorCinematicProps
{
    public readonly Transform tablet;
    readonly List<Object> owned=new List<Object>();readonly Material screen;readonly Transform root;
    readonly Texture2D[] photos;int shown=-1;
    Transform note;Vector3 deskBeside;
    public VisitorCinematicProps(Transform root,Transform home,Transform head,Transform visitor,Vector3 desk)
    {
        this.root=root;
        var dark=Material("Borracha gasta",new Color(.035f,.042f,.045f));
        if(head!=null)
        {
        var cloth=Material("Lona escura do visitante",new Color(.075f,.090f,.110f));
        cloth.SetFloat("_Cull",0);
        var leather=Material("Costura do capuz",new Color(.13f,.14f,.15f));
        bool suppliedCharacter=ProvidedCharacter(visitor);
        if(!suppliedCharacter)
        {
            var hood=new GameObject("Capuz de lona - abertura do rosto").transform;
            hood.SetParent(root,false);hood.position=head.position;hood.rotation=visitor.rotation;
            hood.SetParent(head,true);
            hood.localScale=new Vector3(1/head.lossyScale.x,1/head.lossyScale.y,1/head.lossyScale.z);
            Hood(hood,cloth);
            FaceWrap(hood,cloth);
            var seams=new[]{new Vector3(-.19f,-.17f,.15f),new Vector3(.19f,-.17f,.15f)};
            foreach(var p in seams)Part(hood,"Cordao do capuz",PrimitiveType.Capsule,p,new Vector3(.013f,.105f,.013f),leather);
            Cloak(visitor,cloth);
        }

        var paper=Material("Papel amarelado",new Color(.76f,.72f,.59f));
        note=Part(root,"Aviso final de cobranca",PrimitiveType.Cube,desk+new Vector3(-.23f,.01f,.12f),new Vector3(.31f,.006f,.38f),paper);
        note.localRotation=Quaternion.Euler(0,-8,0);
        var printed=Resources.Load<Texture2D>("Cinematics/ContaDeEnergia");
        if(printed!=null)
        {
            var print=Material("Conta de energia impressa",Color.white);print.SetTexture("_BaseMap",printed);
            var face=Part(note,"Conta de energia",PrimitiveType.Quad,new Vector3(0,.6f,0),Vector3.one,print);face.localRotation=Quaternion.Euler(90,0,180);
        }
        deskBeside=desk+new Vector3(.0f,.007f,.27f);
        var calculator=Part(root,"Calculadora de mesa",PrimitiveType.Cube,desk+new Vector3(.23f,.025f,.10f),new Vector3(.16f,.035f,.23f),dark);
        for(int row=0;row<4;row++)for(int col=0;col<3;col++)Part(root,"Tecla",PrimitiveType.Cube,desk+new Vector3(.185f+col*.045f,.05f,.10f+row*.033f),new Vector3(.031f,.012f,.021f),paper);
        var calcScreen=Material("LCD da calculadora",new Color(.30f,.36f,.27f));
        Part(root,"Visor da calculadora",PrimitiveType.Cube,desk+new Vector3(.23f,.047f,.03f),new Vector3(.125f,.007f,.06f),calcScreen);
        }
        tablet=new GameObject("Tablet recebido do visitante").transform;tablet.SetParent(root,false);
        Part(tablet,"Carcaca",PrimitiveType.Cube,Vector3.zero,new Vector3(.49f,.31f,.028f),dark);
        var rim=Material("Aro gasto do tablet",new Color(.12f,.14f,.15f));
        Part(tablet,"Aro",PrimitiveType.Cube,new Vector3(0,0,-.017f),new Vector3(.465f,.287f,.01f),rim);
        screen=Material("Fotos reais das fazendas",Color.white,true);
        Part(tablet,"Tela das fazendas",PrimitiveType.Quad,new Vector3(0,0,-.024f),new Vector3(.428f,.247f,1),screen);
        Part(tablet,"Faixa da proposta",PrimitiveType.Cube,new Vector3(0,-.091f,-.028f),new Vector3(.428f,.067f,.002f),dark);
        var offer=new GameObject("Proposta na tela").AddComponent<TextMesh>();offer.transform.SetParent(tablet,false);offer.transform.localPosition=new Vector3(-.19f,-.067f,-.031f);
        offer.text="R$ 45 / AVE VIVA\nENTREGA: MERCADO CLANDESTINO";offer.fontSize=48;offer.characterSize=.0031f;offer.color=new Color(.81f,.88f,.76f);offer.anchor=TextAnchor.UpperLeft;
        DepthText(offer);
        Part(tablet,"Camera frontal",PrimitiveType.Sphere,new Vector3(0,.139f,-.024f),new Vector3(.009f,.009f,.003f),dark);
        var scratch=Material("Trinca fina do vidro",new Color(.57f,.61f,.63f));
        var crack=Part(tablet,"Trinca no canto",PrimitiveType.Cube,new Vector3(.188f,.095f,-.026f),new Vector3(.002f,.054f,.001f),scratch);crack.localRotation=Quaternion.Euler(0,0,-27);
        photos=Resources.LoadAll<Texture2D>("Cinematics/Recon");if(photos.Length==0)photos=ProtagonistPhone.Instance?.farmPhotos;
        ShowPhoto(0);tablet.gameObject.SetActive(false);
    }
    public Vector3 NoteContact=>note.TransformPoint(new Vector3(-.25f,0,.3f));
    public Vector3 NoteCentre=>note.position;
    public Vector3 DeskBeside=>root.TransformPoint(deskBeside);
    public Vector3 NoteUp=>note.up;
    Material Material(string name,Color color,bool unlit=false)
    {
        var shader=unlit?Resources.Load<Shader>("Cinematics/CinematicScreen"):Shader.Find("Universal Render Pipeline/Lit");
        if(shader==null)throw new System.InvalidOperationException("Missing cinematic shader: "+(unlit?"CinematicScreen":"URP Lit"));
        var material=new Material(shader);
        material.name=name;material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.12f);owned.Add(material);return material;
    }
    static bool ProvidedCharacter(Transform visitor)=>visitor.GetComponentsInChildren<SkinnedMeshRenderer>(true).Any(s=>s.name=="VisitorBody");
    void DepthText(TextMesh text)
    {
        text.font.RequestCharactersInTexture(text.text,text.fontSize);
        var material=new Material(Resources.Load<Shader>("Cinematics/CinematicPrint"));owned.Add(material);
        material.SetColor("_BaseColor",text.color);material.SetTexture("_BaseMap",text.font.material.mainTexture);
        text.GetComponent<Renderer>().sharedMaterial=material;
    }
    Transform Part(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;
        var collider=go.GetComponent<Collider>();if(collider!=null){collider.enabled=false;Object.Destroy(collider);}
        go.GetComponent<Renderer>().sharedMaterial=material;return go.transform;
    }
    void Hood(Transform parent,Material material)
    {
        const int sides=18;var vertices=new List<Vector3>();var indices=new List<int>();
        var rings=new[]{new Vector4(-.10f,.24f,.215f,0),new Vector4(.06f,.225f,.215f,0),new Vector4(.25f,.19f,.19f,0),new Vector4(.32f,.045f,.10f,0)};
        for(int r=0;r<rings.Length;r++)for(int j=0;j<=sides;j++)
        {
            float angle=Mathf.Lerp(30,330,(float)j/sides)*Mathf.Deg2Rad;
            vertices.Add(new Vector3(Mathf.Sin(angle)*rings[r].y,rings[r].x,Mathf.Cos(angle)*rings[r].z-.035f));
        }
        for(int r=0;r<rings.Length-1;r++)for(int j=0;j<sides;j++)
        {int a=r*(sides+1)+j,b=a+sides+1;indices.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
        var mesh=new Mesh{name="Capuz aberto com caimento"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
        var filter=parent.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=mesh;parent.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    void FaceWrap(Transform parent,Material material)
    {
        var node=new GameObject("Mascara de tecido sob o capuz").transform;
        node.SetParent(parent,false);node.localPosition=new Vector3(0,.035f,.188f);
        var mesh=new Mesh{name="Mascara de tecido afunilada"};
        mesh.SetVertices(new[]{new Vector3(-.090f,.070f,0),new Vector3(.090f,.070f,0),new Vector3(.065f,-.060f,.010f),new Vector3(-.065f,-.060f,.010f)});
        mesh.SetTriangles(new[]{0,1,2,0,2,3,2,1,0,3,2,0},0);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
        node.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;node.gameObject.AddComponent<MeshRenderer>().sharedMaterial=material;
    }
    void Cloak(Transform actor,Material material)
    {
        const int sides=20;var vertices=new List<Vector3>();var indices=new List<int>();
        var rings=new[]{new Vector3(.72f,.30f,.22f),new Vector3(1.15f,.33f,.235f),new Vector3(1.43f,.32f,.19f),new Vector3(1.55f,.14f,.13f)};
        for(int r=0;r<rings.Length;r++)for(int j=0;j<sides;j++)
        {float a=j*2*Mathf.PI/sides;float fold=1+Mathf.Cos(a*7)*.035f;vertices.Add(new Vector3(Mathf.Sin(a)*rings[r].y*fold,rings[r].x,Mathf.Cos(a)*rings[r].z*fold));}
        for(int r=0;r<rings.Length-1;r++)for(int j=0;j<sides;j++)
        {int a=r*sides+j,b=r*sides+(j+1)%sides;indices.AddRange(new[]{a,a+sides,b,b,a+sides,b+sides});}
        var mesh=new Mesh{name="Sobrecapa de lona"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.RecalculateBounds();owned.Add(mesh);
        var node=new GameObject("Sobrecapa escura");node.transform.SetParent(actor,false);
        var bones=actor.GetComponentsInChildren<Transform>();
        var hips=bones.First(t=>t.name=="Hips" || t.name=="Pelvis" || t.name=="Spine_01");
        var chest=bones.First(t=>t.name=="Spine_02" || t.name=="Chest");
        mesh.bindposes=new[]{hips.worldToLocalMatrix*node.transform.localToWorldMatrix,chest.worldToLocalMatrix*node.transform.localToWorldMatrix};
        mesh.boneWeights=vertices.Select(v=>{float w=Mathf.InverseLerp(.95f,1.5f,v.y);return new BoneWeight{boneIndex0=0,weight0=1-w,boneIndex1=1,weight1=w};}).ToArray();
        var skin=node.AddComponent<SkinnedMeshRenderer>();skin.sharedMesh=mesh;skin.sharedMaterial=material;skin.bones=new[]{hips,chest};skin.rootBone=hips;skin.updateWhenOffscreen=true;skin.forceMatrixRecalculationPerRender=true;skin.localBounds=new Bounds(Vector3.up,Vector3.one*3);
        Sleeve(actor,"UpperArmL","Upperarm_L","ForearmL","Lowerarm_L",material,"Manga esquerda - capa");
        Sleeve(actor,"ForearmL","Lowerarm_L","HandL","Hand_L",material,"Punho esquerdo - capa");
        Sleeve(actor,"UpperArmR","Upperarm_R","ForearmR","Lowerarm_R",material,"Manga direita - capa");
        Sleeve(actor,"ForearmR","Lowerarm_R","HandR","Hand_R",material,"Punho direito - capa");
    }
    void Sleeve(Transform actor,string firstName,string firstAlternate,string secondName,string secondAlternate,Material material,string name)
    {
        var bones=actor.GetComponentsInChildren<Transform>(true);
        var first=bones.FirstOrDefault(t=>t.name==firstName || t.name==firstAlternate);
        var second=bones.FirstOrDefault(t=>t.name==secondName || t.name==secondAlternate);
        if(first==null || second==null)return;
        Vector3 direction=first.InverseTransformPoint(second.position);
        float length=direction.magnitude;
        if(length<.01f)return;
        var sleeve=Part(first,name,PrimitiveType.Cylinder,direction*.50f,new Vector3(.16f,length*.50f,.16f),material);
        sleeve.localRotation=Quaternion.FromToRotation(Vector3.up,direction.normalized);
    }
    public void ShowPhoto(int index)
    {
        if(photos==null || photos.Length==0)return;index=Mathf.Clamp(index,0,photos.Length-1);
        if(index==shown)return;shown=index;screen.SetTexture("_BaseMap",photos[index]);
    }
    public void Dispose(){foreach(var item in owned)if(item!=null)Object.Destroy(item);owned.Clear();}
}
