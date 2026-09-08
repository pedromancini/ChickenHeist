using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class RuralCharacterBuilder
{
    const string Folder="Assets/ChickenHeistGenerated/Characters";
    static readonly string[] Names={"Hips","Spine","Head","ArmL","ForearmL","HandL","ArmR","ForearmR","HandR","LegL","ShinL","FootL","LegR","ShinR","FootR"};
    static readonly int[] Parents={-1,0,1,1,3,4,1,6,7,0,9,10,0,12,13};
    static readonly Vector3[] Pivots={new Vector3(0,.92f,0),new Vector3(0,1.16f,0),new Vector3(0,1.54f,0),
        new Vector3(-.24f,1.43f,0),new Vector3(-.275f,1.13f,0),new Vector3(-.28f,.86f,0),
        new Vector3(.24f,1.43f,0),new Vector3(.275f,1.13f,0),new Vector3(.28f,.86f,0),
        new Vector3(-.11f,.90f,0),new Vector3(-.11f,.49f,0),new Vector3(-.11f,.10f,.03f),
        new Vector3(.11f,.90f,0),new Vector3(.11f,.49f,0),new Vector3(.11f,.10f,.03f)};
    public static GameObject Create(Transform parent,string name,bool firstPerson)
    {
        if(firstPerson && AssetDatabase.LoadAssetAtPath<GameObject>(ProtagonistInstaller.PrefabPath)!=null)
            return ProtagonistInstaller.Create(parent,name);
        if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/ChickenHeistGenerated","Characters");
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/LowPolyHuman2.obj");
        if(source==null)throw new System.InvalidOperationException("Local low-poly character is missing.");
        var root=new GameObject(name);root.transform.SetParent(parent,false);
        var bones=new Transform[Names.Length];
        for(int i=0;i<bones.Length;i++)
        {
            bones[i]=new GameObject(Names[i]).transform;
            bones[i].SetParent(Parents[i]<0?root.transform:bones[Parents[i]],false);
            bones[i].localPosition=Pivots[i]-(Parents[i]<0?Vector3.zero:Pivots[Parents[i]]);
        }
        var body=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name+" - roupa de trabalho"};body.color=firstPerson?new Color(.24f,.30f,.26f):new Color(.43f,.25f,.18f);
        var trousers=new Material(body){name="Calca de sarja",color=new Color(.19f,.22f,.23f)};
        var skin=new Material(body){name="Pele",color=new Color(.62f,.40f,.27f)};
        var boots=new Material(body){name="Botas gastas",color=new Color(.13f,.105f,.08f)};
        foreach(var m in new[]{body,trousers,skin,boots})m.SetFloat("_Smoothness",.08f);
        var sourceFilter=source.GetComponentInChildren<MeshFilter>();var mesh=sourceFilter.sharedMesh;
        Bounds bounds=mesh.bounds;float scale=1.78f/bounds.size.y;
        Vector3[] original=mesh.vertices;var vertices=new Vector3[original.Length];
        for(int i=0;i<vertices.Length;i++)
        {
            Vector3 p=original[i];p-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);p*=scale;
            vertices[i]=Quaternion.Euler(0,180,0)*p;
        }
        var groups=new List<Vector3>[bones.Length];for(int i=0;i<groups.Length;i++)groups[i]=new List<Vector3>();
        var triangles=mesh.triangles;
        for(int t=0;t<triangles.Length;t+=3)
        {
            Vector3 center=(vertices[triangles[t]]+vertices[triangles[t+1]]+vertices[triangles[t+2]])/3;
            int bone=Region(center);
            for(int v=0;v<3;v++)groups[bone].Add(vertices[triangles[t+v]]-Pivots[bone]);
        }
        for(int i=0;i<groups.Length;i++)
        {
            if(groups[i].Count==0)continue;
            var part=new GameObject("Mesh "+Names[i]);part.transform.SetParent(bones[i],false);
            var section=new Mesh{name="LowPolyHuman2 - "+Names[i]};section.SetVertices(groups[i]);
            var indices=new int[groups[i].Count];for(int j=0;j<indices.Length;j++)indices[j]=j;
            section.SetTriangles(indices,0);section.RecalculateNormals();section.RecalculateBounds();
            part.AddComponent<MeshFilter>().sharedMesh=section;
            var renderer=part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial=i==2 || i==5 || i==8?skin:i==11 || i==14?boots:i>=9 || i==0?trousers:body;
            if(firstPerson && i==2)renderer.shadowCastingMode=ShadowCastingMode.ShadowsOnly;
        }
        // Small facial features and a work cap distinguish the person from the unpainted source.
        foreach(float x in new[]{-.046f,.046f})Detail(bones[2],"Olho",new Vector3(x,.105f,.075f),new Vector3(.017f,.013f,.018f),boots,firstPerson);
        Detail(bones[2],"Aba do bone",new Vector3(0,.19f,.10f),new Vector3(.23f,.025f,.13f),body,firstPerson);
        Detail(bones[2],"Bone de trabalho",new Vector3(0,.21f,.025f),new Vector3(.215f,.055f,.18f),body,firstPerson);
        var animation=root.AddComponent<Animation>();animation.playAutomatically=true;
        foreach(string state in new[]{"Idle","Walk","Run","Crouch","Trade"})
        {
            var clip=MakeClip(state,bones,root.transform);animation.AddClip(clip,state);
            if(state=="Idle")animation.clip=clip;
        }
        var driver=root.AddComponent<RuralCharacterAnimator>();driver.clips=animation;
        if(firstPerson)driver.movement=parent.GetComponent<PlayerMovement>();
        return root;
    }
    static int Region(Vector3 p)
    {
        if(p.y>1.50f)return 2;
        if(Mathf.Abs(p.x)>.195f && p.y>.72f)
        {int side=p.x<0?3:6;return side+(p.y>1.16f?0:p.y>.89f?1:2);}
        if(p.y<.87f){int side=p.x<0?9:12;return side+(p.y>.49f?0:p.y>.15f?1:2);}
        return p.y>1.03f?1:0;
    }
    static void Detail(Transform parent,string name,Vector3 position,Vector3 size,Material material,bool hide)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;
        Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=material;
        if(hide)go.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.ShadowsOnly;
    }
    static AnimationClip MakeClip(string state,Transform[] bones,Transform root)
    {
        float duration=state=="Run"?.62f:state=="Walk"?1.05f:state=="Crouch"?1.4f:state=="Trade"?1.1f:3.4f;
        var clip=new AnimationClip{name=state,legacy=true,wrapMode=state=="Trade"?WrapMode.Once:WrapMode.Loop};
        for(int i=0;i<bones.Length;i++)
        {
            string path=AnimationUtility.CalculateTransformPath(bones[i],root);
            var keys=new Keyframe[9];
            for(int k=0;k<keys.Length;k++)
            {
                float phase=k/8f,angle=0,sine=Mathf.Sin(phase*Mathf.PI*2);
                if(state=="Idle" && (i==1 || i==2))angle=sine*(i==1?.8f:2f);
                if(state=="Walk" || state=="Run" || state=="Crouch")
                {
                    float amount=state=="Run"?37:state=="Crouch"?15:24;
                    if(i==9)angle=sine*amount;
                    if(i==12)angle=-sine*amount;
                    if(i==10)angle=Mathf.Max(0,-sine)*amount*.8f;
                    if(i==13)angle=Mathf.Max(0,sine)*amount*.8f;
                    if(i==3)angle=-sine*amount*.7f;
                    if(i==6)angle=sine*amount*.7f;
                    if(i==4 || i==7)angle=state=="Run"?-55:-15;
                    if(state=="Crouch")
                    {if(i==0)angle=13;if(i==9 || i==12)angle-=25;if(i==10 || i==13)angle+=42;}
                }
                if(state=="Trade")
                {
                    float reach=Mathf.Sin(Mathf.PI*phase);
                    if(i==6)angle=-65*reach;if(i==7)angle=-28*reach;if(i==2)angle=8*reach;
                }
                keys[k]=new Keyframe(phase*duration,angle);
            }
            clip.SetCurve(path,typeof(Transform),"localEulerAnglesRaw.x",new AnimationCurve(keys));
        }
        string assetPath=Folder+"/"+state+".anim";
        var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(assetPath);
        if(existing!=null){EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);return existing;}
        AssetDatabase.CreateAsset(clip,assetPath);return clip;
    }
}
