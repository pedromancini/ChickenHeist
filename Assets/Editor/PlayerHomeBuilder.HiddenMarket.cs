using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static partial class PlayerHomeBuilder
{
    static Vector3 FindHiddenMarketSite(Transform home)
    {
        var surfaces=RuralTreeRoots.TerrainSurfaces();
        var farms=Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None);
        var trees=Object.FindObjectsByType<RuralTreeRoots>(FindObjectsSortMode.None);
        Vector3 entrance=home.position+new Vector3(-3,0,-15),best=Vector3.zero;
        float bestScore=float.NegativeInfinity;
        for(int i=0;i<72;i++)
        {
            float angle=i*Mathf.PI*2/72;
            Vector3 candidate=entrance+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(240+(i%3)*40);
            bool valid=true;float low=float.PositiveInfinity,high=float.NegativeInfinity;
            foreach(var p in new[]{new Vector3(-9,0,-10),new Vector3(9,0,-10),new Vector3(-9,0,10),new Vector3(9,0,10)})
            {
                if(!RuralTreeRoots.SurfaceHeight(surfaces,candidate+p,out float y)){valid=false;break;}
                low=Mathf.Min(low,y);high=Mathf.Max(high,y);
            }
            if(!valid || high-low>1.2f)continue;
            foreach(var farm in farms)
            {
                Rect lot=farm.lot;lot.xMin-=22;lot.xMax+=22;lot.yMin-=22;lot.yMax+=22;
                for(int step=1;step<=40;step++)
                {
                    Vector3 p=Vector3.Lerp(entrance,candidate,step/40f);
                    if(lot.Contains(new Vector2(p.x,p.z))){valid=false;break;}
                }
                if(!valid)break;
            }
            if(!valid)continue;
            float score=-(high-low)*20;
            foreach(var tree in trees)if((tree.transform.position-candidate).sqrMagnitude<2500)score++;
            if(score>bestScore){bestScore=score;best=new Vector3(candidate.x,high,candidate.z);}
        }
        if(float.IsNegativeInfinity(bestScore))throw new System.InvalidOperationException("No isolated, accessible market site found. Scene backup preserved.");
        return best;
    }

    static void DressHiddenMarket(Transform root,Material wood,Material metal)
    {
        var tarp=WornMaterial("Lona velha verde musgo",new Color(.12f,.20f,.15f));
        for(int i=0;i<3;i++)
        {
            var sheet=Part(root,"Cobertura remendada",new Vector3(-1.3f+i*1.3f,2.53f,-4.9f),new Vector3(1.38f,.07f,2.2f),tarp);
            sheet.transform.localRotation=Quaternion.Euler(5,0,i==1?1:-1);
        }
        foreach(float x in new[]{-2.1f,2.1f})Part(root,"Esteio gasto",new Vector3(x,1.2f,-5.6f),new Vector3(.12f,2.4f,.12f),wood);
        for(int i=0;i<5;i++)
        {
            var board=Part(root,"Tabua vedando fachada",new Vector3(-3.6f+i*1.8f,1.55f,-3.05f),new Vector3(1.68f,.21f,.09f),wood);
            board.transform.localRotation=Quaternion.Euler(0,0,i%2==0?8:-6);
        }
        for(int i=0;i<4;i++)Place("Box",new Vector3(-3.4f+(i%2)*.83f,(i/2)*.73f,-3.9f),new Vector3(.78f,.72f,.72f),root).name="Carga sem identificacao";
        Part(root,"Lampiao - protecao",new Vector3(0,2.33f,-5.4f),new Vector3(.24f,.1f,.24f),metal);
        var glass=WornMaterial("Vidro ambar do lampiao",new Color(.85f,.49f,.16f));
        glass.EnableKeyword("_EMISSION");glass.SetColor("_EmissionColor",new Color(1,.48f,.10f)*1.4f);
        Part(root,"Lampiao - vidro",new Vector3(0,2.19f,-5.4f),new Vector3(.11f,.22f,.11f),glass);
    }

    static void BuildHiddenMarketTrail(Transform root,Transform home)
    {
        Vector3 start=home.position+new Vector3(-3,0,-15),end=root.position+new Vector3(0,0,-12);
        Vector3 side=Vector3.Cross((end-start).normalized,Vector3.up);
        Vector3 exit=home.position+new Vector3(end.x>start.x?32:-32,0,-30);
        var points=new List<Vector3>{start,start+Vector3.back*15,exit};
        for(int i=1;i<8;i++)points.Add(Vector3.Lerp(exit,end,i/8f)+side*Mathf.Sin(i*.9f)*6);
        points.Add(end);points.Add(root.position+new Vector3(0,0,-6.4f));
        var surfaces=RuralTreeRoots.TerrainSurfaces();
        for(int i=0;i<points.Count;i++)
        {
            Vector3 p=points[i];
            if(!RuralTreeRoots.SurfaceHeight(surfaces,p,out float y))throw new System.InvalidOperationException("Trail leaves terrain.");
            points[i]=new Vector3(p.x,y,p.z);
            if(i>0)AddHomePath(root,root.InverseTransformPoint(points[i-1]),root.InverseTransformPoint(points[i]),2.1f);
        }
        int relocated=0;
        foreach(var tree in Object.FindObjectsByType<RuralTreeRoots>(FindObjectsSortMode.None))
        {
            Vector3 p=tree.transform.position;bool blocked=Mathf.Abs(p.x-root.position.x)<9 && Mathf.Abs(p.z-root.position.z)<10;
            for(int i=1;i<points.Count && !blocked;i++)blocked=HorizontalSegmentDistance(p,points[i-1],points[i])<3.5f;
            if(!blocked)continue;
            Vector3 away=(p-root.position);away.y=0;if(away.sqrMagnitude<1)away=Vector3.right;
            Vector3 replacement=p+side*9+away.normalized*12;
            tree.transform.position=replacement;
            if(!tree.Ground(surfaces,out _))throw new System.InvalidOperationException("Cannot ground relocated tree.");
            relocated++;
        }
        Physics.SyncTransforms();
        string[] species={"CircleTree_Summer","Env_Tree_01","Tree_01"};
        for(int i=0;i<28;i++)
        {
            float angle=i*2.399963f;
            Vector3 p=root.position+new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(13+(i%4)*3.2f);
            bool clear=true;
            for(int j=1;j<points.Count;j++)if(HorizontalSegmentDistance(p,points[j-1],points[j])<6)clear=false;
            if(!clear || !RuralTreeRoots.SurfaceHeight(surfaces,p,out float y))continue;
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChickenHeistGenerated/Nature/Trees/"+species[i%3]+".prefab");
            var tree=Object.Instantiate(source,root);tree.name="Mata de ocultacao "+i;
            tree.transform.position=new Vector3(p.x,y,p.z);
            Bounds bounds=ProceduralFarmGenerator.VisualBounds(tree);
            tree.transform.localScale*=(6+(i%3)*1.3f)/Mathf.Max(.1f,bounds.size.y);
            tree.GetComponent<RuralTreeRoots>().Ground(surfaces,out _);
        }
        Physics.SyncTransforms();
        Directory.CreateDirectory("output/playable-review");
        GradeHiddenTrail(points);
        RuralTreeRoots.GroundAll();
        File.WriteAllText("output/playable-review/hidden-market-location.txt","Home distance: "+Vector3.Distance(home.position,root.position)+"m\nPosition: "+root.position+"\nTrees relocated, not deleted: "+relocated);
    }
    static void GradeHiddenTrail(List<Vector3> points)
    {
        // A narrow graded track eases the steep transition around the home plateau.
        for(int pass=0;pass<2;pass++)
        for(int k=1;k<points.Count;k++)
        {
            int i=pass==0?k:points.Count-1-k,j=pass==0?i-1:i+1;
            Vector3 p=points[i],q=points[j],flat=p-q;flat.y=0;
            p.y=Mathf.Clamp(p.y,q.y-flat.magnitude*.32f,q.y+flat.magnitude*.32f);points[i]=p;
        }
        var terrain=GameObject.Find("Terreno Ondulado Low Poly");
        var filter=terrain.GetComponent<MeshFilter>();
        var mesh=Object.Instantiate(filter.sharedMesh);mesh.name="Terreno rural - trilha do entreposto";
        var vertices=mesh.vertices;
        for(int v=0;v<vertices.Length;v++)
        {
            Vector3 p=terrain.transform.TransformPoint(vertices[v]);float nearest=10,height=p.y;
            for(int i=1;i<points.Count;i++)
            {
                Vector3 a=points[i-1],b=points[i],ab=b-a,ap=p-a;ab.y=ap.y=0;
                float t=Mathf.Clamp01(Vector3.Dot(ap,ab)/Mathf.Max(.001f,ab.sqrMagnitude));
                Vector3 d=ap-ab*t;float distance=d.magnitude;
                if(distance<nearest){nearest=distance;height=Mathf.Lerp(a.y,b.y,t);}
            }
            if(nearest>=10)continue;
            float blend=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(3.5f,10,nearest));
            p.y=Mathf.Lerp(p.y,height,blend);vertices[v]=terrain.transform.InverseTransformPoint(p);
        }
        mesh.vertices=vertices;mesh.RecalculateNormals();mesh.RecalculateBounds();filter.sharedMesh=mesh;
        var collider=terrain.GetComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=mesh;
        Physics.SyncTransforms();
    }
    static float HorizontalSegmentDistance(Vector3 p,Vector3 a,Vector3 b)
    {
        p.y=a.y=b.y=0;Vector3 ab=b-a;
        return Vector3.Distance(p,a+ab*Mathf.Clamp01(Vector3.Dot(p-a,ab)/Mathf.Max(.001f,ab.sqrMagnitude)));
    }
}
