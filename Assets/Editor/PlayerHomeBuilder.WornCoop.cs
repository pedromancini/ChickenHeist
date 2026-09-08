using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

public static partial class PlayerHomeBuilder
{
    private static void BuildWornCoop(Transform home)
    {
        foreach (string name in new[]{"Pequeno galinheiro do jogador","Comedouro quase vazio","Galinheiro gasto - Ultimo Recurso"})
        {
            var previous=home.Find(name);
            if(previous!=null) Object.DestroyImmediate(previous.gameObject);
        }
        // The service walk goes beside the run, never through its shelter.
        Vector3 origin=home.position;
        foreach(var path in home.GetComponentsInChildren<RuralRoadSpan>())
        {
            if(Mathf.Abs(path.start.x-origin.x-10)<.1f) path.start.x=origin.x+6;
            if(Mathf.Abs(path.end.x-origin.x-10)<.1f) path.end.x=origin.x+6;
        }
        bool hasCoopAccess=false;
        foreach(var path in home.GetComponentsInChildren<RuralRoadSpan>())
            if(path.name=="Acesso ao galinheiro gasto") hasCoopAccess=true;
        if(!hasCoopAccess)
        {
            var access=new GameObject("Acesso ao galinheiro gasto");access.transform.SetParent(home,false);
            var span=access.AddComponent<RuralRoadSpan>();
            span.start=home.TransformPoint(new Vector3(6,0,-6));span.end=home.TransformPoint(new Vector3(11,0,-5.7f));span.width=1.3f;
        }
        var coop=new GameObject("Galinheiro gasto - Ultimo Recurso");
        coop.transform.SetParent(home,false);coop.transform.localPosition=new Vector3(12,0,-3);
        var root=coop.transform;
        var wood=new Material[4];
        wood[0]=WornMaterial("Madeira cinza desgastada",new Color(.28f,.25f,.19f));
        wood[1]=WornMaterial("Madeira exposta",new Color(.34f,.28f,.19f));
        wood[2]=WornMaterial("Tabua umida escurecida",new Color(.17f,.18f,.14f));
        wood[3]=WornMaterial("Remendo de madeira antiga",new Color(.39f,.35f,.25f));
        var rust=WornMaterial("Chapa velha oxidada",new Color(.32f,.23f,.16f));
        var metal=WornMaterial("Arame oxidado",new Color(.20f,.21f,.17f));
        var straw=WornMaterial("Palha seca restante",new Color(.47f,.40f,.21f));
        var dirt=WornMaterial("Chao pisoteado do galinheiro",new Color(.19f,.17f,.115f));
        Part(root,"Chao de terra e palha",new Vector3(0,.015f,0),new Vector3(5.9f,.025f,4.4f),dirt);
        // A raised sleeping box at the back leaves a walkable run in front.
        for(int side=-1;side<=1;side+=2)
        for(int end=-1;end<=1;end+=2)
            Part(root,"Pe do abrigo",new Vector3(side*1.28f,.38f,1+end*.65f),new Vector3(.13f,.76f,.13f),wood[2]);
        Part(root,"Assoalho do abrigo",new Vector3(0,.72f,1),new Vector3(2.8f,.12f,1.6f),wood[0]);
        for(int i=0;i<12;i++)
        {
            float x=-1.32f+i*.24f;
            Part(root,"Tabua do fundo",new Vector3(x,1.31f,1.77f),new Vector3(.225f,1.1f+(i%3)*.02f,.07f),wood[i%4]);
            if(i>=4 && i<=6) continue;
            var plank=Part(root,"Tabua remendada da frente",new Vector3(x,1.23f,.23f),new Vector3(.225f,.97f-(i%4)*.025f,.07f),wood[(i+1)%4]);
            if(i==9) plank.transform.localRotation=Quaternion.Euler(0,0,-4);
        }
        for(int side=-1;side<=1;side+=2)
        for(int i=0;i<7;i++)
            Part(root,"Tabua lateral do abrigo",new Vector3(side*1.42f,1.28f,.29f+i*.235f),new Vector3(.07f,1.08f,.22f),wood[(i+2)%4]);
        Beam(root,"Remendo diagonal",new Vector3(.4f,.92f,.16f),new Vector3(1.30f,1.55f,.16f),.1f,wood[3]);
        Beam(root,"Rampa de acesso",new Vector3(-.13f,.73f,.17f),new Vector3(-.13f,.06f,-1.25f),.10f,wood[1],.55f);
        for(int i=0;i<6;i++)
            Part(root,"Travessa da rampa",new Vector3(-.13f,.16f+i*.105f,-1.05f+i*.22f),new Vector3(.59f,.045f,.055f),wood[2]);
        for(int i=0;i<4;i++) CorrugatedPanel(root,i,rust);
        Beam(root,"Sarrafao exposto do telhado",new Vector3(-1.55f,1.9f,.02f),new Vector3(1.55f,1.9f,.02f),.09f,wood[2]);
        // Low, patched wire run: visibly fragile, without the target farms' secure roof enclosure.
        Vector3[] posts={new Vector3(-3,0,-2.2f),new Vector3(3,0,-2.2f),new Vector3(-3,0,2.2f),new Vector3(3,0,2.2f),new Vector3(-1.5f,0,-2.2f),new Vector3(-.4f,0,-2.2f)};
        for(int i=0;i<posts.Length;i++)
        {
            Vector3 lean=i==1 ? new Vector3(-.09f,0,.03f):Vector3.zero;
            Beam(root,"Mourao gasto",posts[i],posts[i]+Vector3.up*1.22f+lean,.10f,wood[i%4]);
        }
        WirePanel(root,new Vector3(-3,0,2.2f),new Vector3(3,0,2.2f),metal);
        WirePanel(root,new Vector3(-3,0,-2.2f),new Vector3(-3,0,2.2f),metal);
        WirePanel(root,new Vector3(3,0,-2.2f),new Vector3(3,0,2.2f),metal);
        WirePanel(root,new Vector3(-3,0,-2.2f),new Vector3(-1.5f,0,-2.2f),metal);
        WirePanel(root,new Vector3(-.4f,0,-2.2f),new Vector3(3,0,-2.2f),metal);
        Beam(root,"Travessa de conserto da tela",new Vector3(.3f,.43f,-2.23f),new Vector3(1.55f,.8f,-2.23f),.08f,wood[3]);
        var gate=new GameObject("Portinhola aberta - dobradica gasta");gate.transform.SetParent(root,false);
        gate.transform.localPosition=new Vector3(-1.5f,0,-2.2f);gate.transform.localRotation=Quaternion.Euler(0,-65,0);
        for(int i=0;i<4;i++) Part(gate.transform,"Tabua da portinhola",new Vector3(.1f+i*.28f,.51f,0),new Vector3(.11f,1.02f,.07f),wood[i]);
        Beam(gate.transform,"Travessa da portinhola",new Vector3(0,.25f,0),new Vector3(1,.8f,0),.08f,wood[2]);
        // Empty nests and a nearly empty trough establish the starting hardship without adding stealable animals.
        for(int i=0;i<2;i++)
        {
            var nest=Place("Box",new Vector3(i*.7f-.5f,.8f,1.25f),new Vector3(.58f,.28f,.52f),root);
            nest.name="Ninho vazio";
            for(int j=0;j<8;j++)
            {
                var stalk=Part(root,"Palha no ninho",new Vector3(i*.7f-.5f+(j%3)*.09f,.82f,1.1f+(j/3)*.10f),new Vector3(.23f,.016f,.022f),straw);
                stalk.transform.localRotation=Quaternion.Euler(0,j*31,0);
            }
        }
        var feeder=Place("FoodTrough",new Vector3(1.95f,0,-.8f),new Vector3(1.1f,.36f,.45f),root);feeder.name="Comedouro com pouca racao";
        for(int i=0;i<9;i++)
            Part(root,"Restos de racao",new Vector3(1.65f+(i%5)*.095f,.14f,-.87f+(i/5)*.08f),new Vector3(.035f,.025f,.04f),straw);
        Place("Bucket",new Vector3(-2.25f,0,.4f),new Vector3(.42f,.4f,.42f),root).name="Balde de agua gasto";
        for(int i=0;i<4;i++)
        {
            var plank=Part(root,"Tabua quebrada guardada",new Vector3(3.45f+i*.12f,.08f+i*.025f,.6f),new Vector3(.13f,.06f,1.15f+i*.1f),wood[i]);
            plank.transform.localRotation=Quaternion.Euler(0,i*9,0);
        }
    }

    private static Material WornMaterial(string name,Color color)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.name=name;m.color=color;m.SetFloat("_Smoothness",.04f);return m;
    }

    private static GameObject Part(Transform parent,string name,Vector3 p,Vector3 size,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
        go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=material;return go;
    }

    private static void Beam(Transform parent,string name,Vector3 a,Vector3 b,float thickness,Material material,float width=0)
    {
        var go=Part(parent,name,(a+b)*.5f,new Vector3(width>0 ? width:thickness,thickness,Vector3.Distance(a,b)),material);
        go.transform.localRotation=Quaternion.LookRotation(b-a);
    }

    private static void WirePanel(Transform parent,Vector3 a,Vector3 b,Material material)
    {
        float length=Vector3.Distance(a,b);int count=Mathf.CeilToInt(length/.25f);
        for(int i=0;i<=count;i++)
        {
            Vector3 p=Vector3.Lerp(a,b,i/(float)count);
            Beam(parent,"Tela fina desgastada",p+Vector3.up*.07f,p+Vector3.up*(1.12f-.08f*Mathf.Sin(i*.7f)),.012f,material);
        }
        for(int i=0;i<6;i++) Beam(parent,"Arame da tela",a+Vector3.up*(.1f+i*.19f),b+Vector3.up*(.1f+i*.19f),.012f,material);
    }

    private static void CorrugatedPanel(Transform parent,int index,Material material)
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();
        const int columns=12;
        for(int row=0;row<3;row++)
        for(int col=0;col<=columns;col++)
        {
            float x=-1.65f+index*.82f+col*.82f/columns;
            float z=-.02f+row*1.05f;
            if(index==3 && row==0) z+=col>6 ? .20f+(col%3)*.09f:0;
            float y=1.91f+z*.12f+(col%2)*.022f;
            if(index==2) y-=Mathf.Sin(col*Mathf.PI/columns)*.10f;
            vertices.Add(new Vector3(x,y,z));
        }
        for(int row=0;row<2;row++)
        for(int col=0;col<columns;col++)
        {
            if(index==1 && row==1 && col>8) continue;
            int a=row*(columns+1)+col;
            triangles.AddRange(new[]{a,a+columns+1,a+1,a+1,a+columns+1,a+columns+2});
        }
        var mesh=new Mesh {name="Chapa ondulada rasgada "+index};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        var surface=mesh;mesh=SolidRoofSheet(surface);Object.DestroyImmediate(surface);
        var go=new GameObject(mesh.name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial=material;go.AddComponent<MeshCollider>().sharedMesh=mesh;
    }
}
