using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// The first-person fist (Resources/FirstPerson/PunchFist): a right fist, forearm and rolled shirt sleeve made to be
// seen from 20-50 cm, low poly with flat faces in Elias' skin and shirt colours. The body's own arms, posed by IK that
// close, read as lumps (the full-body hand's fingers collapse when it closes), so the punch shows this instead.
// Space: +Z the way the fist points (knuckles), +Y the back of the hand, +X towards the little finger; the origin is
// the middle of the hand, the forearm runs back along -Z.
public static class PunchFistBuilder
{
    const string Folder="Assets/Resources/FirstPerson";
    [MenuItem("Chicken Heist/First Person/Build Punch Fist")]
    public static void Build()
    {
        Directory.CreateDirectory(Folder);
        var skin=new List<Vector3>();var sleeve=new List<Vector3>();
        // the hand: back and palm, its corners cut
        Block(skin,new Vector3(0,0,-.012f),new Vector3(.084f,.058f,.072f),.013f,Quaternion.identity);
        // the four fingers rolled in: the first bones make the knuckle row in front, the second ones fold down under it
        float[] across={-.030f,-.010f,.010f,.029f};float[] reach={.0f,.003f,.0f,-.008f};float[] thick={.020f,.020f,.019f,.016f};
        for(int i=0;i<4;i++)
        {
            Block(skin,new Vector3(across[i],.008f,.034f+reach[i]),new Vector3(thick[i],.027f,.030f),.006f,Quaternion.Euler(-8,0,0));
            Block(skin,new Vector3(across[i],-.018f,.043f+reach[i]),new Vector3(thick[i]*.94f,.026f,.020f),.006f,Quaternion.Euler(12,0,0));
        }
        // the thumb laid across the front of the first two fingers, below the knuckles
        Block(skin,new Vector3(-.016f,-.026f,.046f),new Vector3(.046f,.017f,.020f),.006f,Quaternion.Euler(0,-14,-8));
        Block(skin,new Vector3(-.040f,-.020f,.012f),new Vector3(.020f,.022f,.040f),.007f,Quaternion.Euler(0,-20,0));
        // wrist and forearm, widening to the elbow
        Loft(skin,new[]{new Ring(-.044f,.064f,.050f),new Ring(-.075f,.060f,.050f),new Ring(-.20f,.074f,.064f),new Ring(-.31f,.084f,.074f)});
        // the rolled sleeve: a thick cuff, then the sleeve running on out of view
        Loft(sleeve,new[]{new Ring(-.30f,.090f,.080f),new Ring(-.31f,.106f,.096f),new Ring(-.37f,.108f,.098f),new Ring(-.38f,.098f,.090f),new Ring(-.70f,.104f,.096f)});
        var mesh=new Mesh{name="Punho em primeira pessoa"};
        var vertices=new List<Vector3>(skin);vertices.AddRange(sleeve);
        mesh.SetVertices(vertices);mesh.subMeshCount=2;
        mesh.SetTriangles(Indices(0,skin.Count),0);mesh.SetTriangles(Indices(skin.Count,sleeve.Count),1);
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        Save(mesh,Folder+"/PunchFistMesh.asset");
        var skinMaterial=Material(Folder+"/PunchFistSkin.mat",new Color(.77f,.565f,.41f));
        var sleeveMaterial=Material(Folder+"/PunchFistSleeve.mat",new Color(.61f,.63f,.47f));
        var root=new GameObject("PunchFist");var filter=root.AddComponent<MeshFilter>();filter.sharedMesh=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/PunchFistMesh.asset");
        var renderer=root.AddComponent<MeshRenderer>();renderer.sharedMaterials=new[]{skinMaterial,sleeveMaterial};
        renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
        PrefabUtility.SaveAsPrefabAsset(root,Folder+"/PunchFist.prefab");Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        Debug.Log("PUNCH FIST BUILT: "+vertices.Count+" vertices");
    }
    public static void BuildBatch(){Build();EditorApplication.Exit(0);}
    struct Ring{public float z,w,h;public Ring(float z,float w,float h){this.z=z;this.w=w;this.h=h;}}
    // a box with its edges cut (an octagon section, its two ends cut in as well), every face flat
    static void Block(List<Vector3> tris,Vector3 centre,Vector3 size,float cut,Quaternion rotation)
    {
        float z0=-size.z*.5f,z1=size.z*.5f;
        var rings=new[]{Section(z0,size.x-2*cut,size.y-2*cut,cut*.5f),Section(z0+cut,size.x,size.y,cut),Section(z1-cut,size.x,size.y,cut),Section(z1,size.x-2*cut,size.y-2*cut,cut*.5f)};
        Emit(tris,rings,true,p=>centre+rotation*p);
    }
    static void Loft(List<Vector3> tris,Ring[] rings)
    {
        var sections=new Vector3[rings.Length][];
        for(int i=0;i<rings.Length;i++)sections[i]=Section(rings[i].z,rings[i].w,rings[i].h,Mathf.Min(rings[i].w,rings[i].h)*.29f);
        System.Array.Reverse(sections);   // from the elbow to the wrist: the faces point out
        Emit(tris,sections,true,p=>p);
    }
    // eight points round a rectangle w x h with its corners cut by `cut`, at depth z
    static Vector3[] Section(float z,float w,float h,float cut)
    {
        float x=w*.5f,y=h*.5f;
        return new[]{new Vector3(-x+cut,y,z),new Vector3(x-cut,y,z),new Vector3(x,y-cut,z),new Vector3(x,-y+cut,z),
                     new Vector3(x-cut,-y,z),new Vector3(-x+cut,-y,z),new Vector3(-x,-y+cut,z),new Vector3(-x,y-cut,z)};
    }
    static void Emit(List<Vector3> tris,Vector3[][] rings,bool caps,System.Func<Vector3,Vector3> place)
    {
        for(int r=0;r<rings.Length-1;r++)
            for(int i=0;i<8;i++)
            {
                Vector3 a=rings[r][i],b=rings[r][(i+1)%8],c=rings[r+1][(i+1)%8],d=rings[r+1][i];
                Quad(tris,place(a),place(d),place(c),place(b));
            }
        if(!caps)return;
        var first=rings[0];var last=rings[rings.Length-1];
        for(int i=1;i<7;i++){tris.Add(place(first[0]));tris.Add(place(first[i]));tris.Add(place(first[i+1]));}
        for(int i=1;i<7;i++){tris.Add(place(last[0]));tris.Add(place(last[i+1]));tris.Add(place(last[i]));}
    }
    static void Quad(List<Vector3> tris,Vector3 a,Vector3 b,Vector3 c,Vector3 d){tris.Add(a);tris.Add(b);tris.Add(c);tris.Add(a);tris.Add(c);tris.Add(d);}
    static int[] Indices(int start,int count){var list=new int[count];for(int i=0;i<count;i++)list[i]=start+i;return list;}
    static void Save(Object asset,string path){var old=AssetDatabase.LoadAssetAtPath<Object>(path);if(old!=null){EditorUtility.CopySerialized(asset,old);}else AssetDatabase.CreateAsset(asset,path);}
    static Material Material(string path,Color color)
    {
        var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.SetColor("_BaseColor",color);material.SetFloat("_Smoothness",.18f);material.SetFloat("_Metallic",0);EditorUtility.SetDirty(material);
        return material;
    }
}
