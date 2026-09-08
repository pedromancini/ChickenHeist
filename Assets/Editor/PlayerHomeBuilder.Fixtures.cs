using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static partial class PlayerHomeBuilder
{
    [MenuItem("Chicken Heist/Finish Home Fixtures")]
    public static void FinishFixtures()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var home = GameObject.Find("Casa do Protagonista - Sitio do Recomeco");
        if (home == null) throw new System.InvalidOperationException("Open the rural world first.");
        var interior = home.transform.Find(InteriorName);
        if (interior == null) throw new System.InvalidOperationException("Build the home interior first.");
        var scene = SceneManager.GetActiveScene();
        Directory.CreateDirectory(Output);
        EditorSceneManager.SaveScene(scene, AssetDatabase.GenerateUniqueAssetPath("Assets/Scenes/BeforeHomeFixtures.unity"), true);
        BuildHomeFixtures(interior);
        var coop = home.transform.Find("Galinheiro gasto - Ultimo Recurso");
        if (coop != null)
        {
            // Replace only the four roof sheets, retaining the existing run and its props.
            foreach (var filter in coop.GetComponentsInChildren<MeshFilter>())
            {
                if (!filter.name.StartsWith("Chapa ondulada rasgada") || filter.sharedMesh.name.EndsWith(" - solid")) continue;
                filter.sharedMesh = SolidRoofSheet(filter.sharedMesh);
                var collider = filter.GetComponent<MeshCollider>();
                if (collider != null) collider.sharedMesh = filter.sharedMesh;
            }
        }
        RuralWorldReview.PersistGeneratedAssets(scene);
        SaveRefrigeratorPrefab(interior.Find("Geladeira antiga"));
        PrefabUtility.SaveAsPrefabAsset(home, "Assets/ChickenHeistGenerated/PlayerHome/ProtagonistHome.prefab");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Capture(home, Output + "/fixtures-kitchen.png", new Vector3(-5.6f,2.15f,3.65f), new Vector3(-3.9f,1.55f,6.4f), 68);
        Capture(home, Output + "/fixtures-door-inside.png", new Vector3(-4.8f,1.9f,4.05f), new Vector3(-3.22f,1.8f,2.28f), 78);
        Capture(home, Output + "/fixtures-door-outside.png", new Vector3(-4.85f,1.9f,.9f), new Vector3(-3.22f,1.8f,2.28f), 78);
        if (coop != null) Capture(coop.gameObject, Output + "/fixtures-coop-ceiling.png", new Vector3(0,1.18f,.65f), new Vector3(.1f,2.1f,1.55f), 94);
        AuditFurniture(interior);
        HomeInteriorTests.ProbePassage();
        AuditFixtures(interior, coop);
        Debug.Log("HOME FIXTURES SAVED: refrigerator, solid entrance returns and roof undersides.");
    }

    static void BuildHomeFixtures(Transform interior)
    {
        foreach (string name in new[] { "Geladeira antiga", "Batente completo da entrada" })
        {
            var old = interior.Find(name);
            if (old != null) Object.DestroyImmediate(old.gameObject);
        }
        var enamel = WornMaterial("Esmalte verde claro envelhecido", new Color(.55f,.64f,.57f));
        enamel.SetFloat("_Smoothness",.24f);
        var seal = WornMaterial("Borracha ressecada", new Color(.10f,.12f,.11f));
        var steel = WornMaterial("Puxador de aluminio fosco", new Color(.48f,.51f,.49f));
        var rust = WornMaterial("Oxidacao nas quinas", new Color(.27f,.20f,.15f));
        var fridge = new GameObject("Geladeira antiga").transform;
        fridge.SetParent(interior, false);
        fridge.localPosition = new Vector3(-3.52f,.63f,6.40f);
        Part(fridge, "Gabinete esmaltado", new Vector3(0,.87f,.035f), new Vector3(.72f,1.58f,.66f), enamel);
        Part(fridge, "Borracha da porta", new Vector3(0,.91f,-.304f), new Vector3(.685f,1.46f,.024f), seal);
        Part(fridge, "Porta da geladeira", new Vector3(0,.71f,-.345f), new Vector3(.67f,1.04f,.070f), enamel);
        Part(fridge, "Porta do congelador", new Vector3(0,1.45f,-.345f), new Vector3(.67f,.405f,.070f), enamel);
        Part(fridge, "Puxador inferior", new Vector3(-.23f,1.00f,-.416f), new Vector3(.035f,.29f,.04f), steel);
        Part(fridge, "Puxador superior", new Vector3(-.23f,1.44f,-.416f), new Vector3(.035f,.19f,.04f), steel);
        foreach (float y in new[] {.88f,1.12f,1.37f,1.51f})
            Part(fridge, "Suporte do puxador", new Vector3(-.23f,y,-.389f), new Vector3(.035f,.023f,.046f), steel);
        Part(fridge, "Rodape ventilado", new Vector3(0,.13f,-.32f), new Vector3(.65f,.10f,.04f), seal);
        for (int i=0;i<9;i++)
            Part(fridge, "Aleta de ventilacao", new Vector3(-.27f+i*.0675f,.13f,-.348f), new Vector3(.02f,.065f,.012f), steel);
        for (int x=-1;x<=1;x+=2) for (int z=-1;z<=1;z+=2)
            Part(fridge, "Pe nivelador", new Vector3(x*.27f,.045f,z*.25f), new Vector3(.075f,.09f,.075f), seal);
        for (int i=0;i<6;i++)
            Part(fridge, "Esmalte lascado na base", new Vector3(-.29f+i*.065f,.20f+(i%2)*.012f,-.381f), new Vector3(.025f+i%3*.013f,.012f,.003f), rust);
        foreach (float y in new[] {.33f,1.19f,1.59f})
            Part(fridge,"Dobradica da geladeira",new Vector3(.345f,y,-.305f),new Vector3(.035f,.055f,.09f),steel);
        Part(fridge,"Emblema antigo",new Vector3(.19f,1.55f,-.382f),new Vector3(.12f,.028f,.008f),steel);
        foreach (string name in new[]{"Gabinete esmaltado","Porta da geladeira","Porta do congelador"})
        {
            var panel=fridge.Find(name);
            panel.GetComponent<MeshFilter>().sharedMesh=ChamferedFixtureBox(panel.localScale,name=="Gabinete esmaltado"?.022f:.012f);
            panel.localScale=Vector3.one;
        }
        // Only the cabinet needs collision; small decorative pieces cannot snag the player.
        foreach (var collider in fridge.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
        var body = fridge.gameObject.AddComponent<BoxCollider>();
        body.center = new Vector3(0,.83f,-.03f); body.size = new Vector3(.72f,1.66f,.79f);
        var picture=interior.Find("Quadro - antes das dividas");
        var photo=interior.Find("Retrato guardado");
        if(picture!=null && photo!=null)
        {
            Vector3 delta=new Vector3(-2.45f,2.35f,picture.localPosition.z)-picture.localPosition;
            picture.localPosition+=delta;photo.localPosition+=delta;
        }
        var hinge=interior.GetComponentInChildren<HomeDoor>().hinge;
        var oldHandle=hinge.Find("Macaneta interna");
        if(oldHandle!=null)Object.DestroyImmediate(oldHandle.gameObject);
        var handle=Part(hinge,"Macaneta interna",new Vector3(1.30f,1.06f,.095f),new Vector3(.12f,.04f,.12f),steel);
        Object.DestroyImmediate(handle.GetComponent<Collider>());

        var frame = new GameObject("Batente completo da entrada").transform;
        frame.SetParent(interior, false);
        var wood = WornMaterial("Batente de madeira usada", new Color(.28f,.245f,.19f));
        // Returns cap the wall opening, while stops overlap the closed leaf behind its swing.
        for (int side=-1;side<=1;side+=2)
        {
            Part(frame, "Ombreira macica", new Vector3(-3.22f+side*.80f,1.73f,2.29f), new Vector3(.12f,2.34f,.68f), wood);
            Part(frame, "Encosto lateral da folha", new Vector3(-3.22f+side*.727f,1.73f,2.337f), new Vector3(.026f,2.24f,.025f), wood);
        }
        Part(frame, "Fechamento superior do vao", new Vector3(-3.22f,2.94f,2.29f), new Vector3(1.72f,.18f,.68f), wood);
        Part(frame, "Encosto superior da folha", new Vector3(-3.22f,2.847f,2.337f), new Vector3(1.48f,.026f,.025f), wood);
        Part(frame, "Soleira de madeira", new Vector3(-3.22f,.597f,2.29f), new Vector3(1.72f,.055f,.68f), wood);
    }

    static Mesh SolidRoofSheet(Mesh source)
    {
        var top = source.vertices;
        var original = source.triangles;
        var vertices = new List<Vector3>(top);
        foreach (var v in top) vertices.Add(v-Vector3.up*.028f);
        var triangles = new List<int>(original);
        var edges = new Dictionary<long, Vector2Int>();
        var counts = new Dictionary<long, int>();
        for (int i=0;i<original.Length;i+=3)
        {
            triangles.Add(original[i]+top.Length);
            triangles.Add(original[i+2]+top.Length);
            triangles.Add(original[i+1]+top.Length);
            for (int j=0;j<3;j++)
            {
                int a=original[i+j], b=original[i+(j+1)%3];
                long key=((long)Mathf.Min(a,b)<<32)|(uint)Mathf.Max(a,b);
                if (!counts.ContainsKey(key)) { counts[key]=0; edges[key]=new Vector2Int(a,b); }
                counts[key]++;
            }
        }
        // Close every boundary, including the deliberately torn part of the old sheet.
        foreach (var pair in edges)
        {
            if (counts[pair.Key]!=1) continue;
            int a=pair.Value.x,b=pair.Value.y,n=top.Length;
            triangles.AddRange(new[]{b,a,a+n,b,a+n,b+n});
        }
        var mesh = new Mesh { name=source.name+" - solid" };
        mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh ChamferedFixtureBox(Vector3 size,float bevel)
    {
        var vertices=new List<Vector3>();var indices=new List<int>();
        Vector3 h=size*.5f,inner=h-Vector3.one*bevel;
        System.Action<Vector3[]> face=points=>
        {
            Vector3 center=Vector3.zero;foreach(var p in points)center+=p;
            if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),center)<0)System.Array.Reverse(points);
            int first=vertices.Count;vertices.AddRange(points);
            for(int i=1;i<points.Length-1;i++)indices.AddRange(new[]{first,first+i,first+i+1});
        };
        // Six inset faces, twelve bevel strips and eight corner triangles keep the mesh closed.
        for(int axis=0;axis<3;axis++)for(int sign=-1;sign<=1;sign+=2)
        {
            int u=(axis+1)%3,v=(axis+2)%3;
            var points=new Vector3[4];int k=0;
            foreach(var corner in new[]{new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(-1,1)})
            {Vector3 p=Vector3.zero;p[axis]=sign*h[axis];p[u]=corner.x*inner[u];p[v]=corner.y*inner[v];points[k++]=p;}
            face(points);
        }
        for(int a=0;a<3;a++)for(int b=a+1;b<3;b++)for(int sa=-1;sa<=1;sa+=2)for(int sb=-1;sb<=1;sb+=2)
        {
            int c=3-a-b;
            Vector3 p=Vector3.zero;p[a]=sa*h[a];p[b]=sb*inner[b];p[c]=-inner[c];
            Vector3 q=p;q[c]=inner[c];Vector3 r=q;r[a]=sa*inner[a];r[b]=sb*h[b];
            Vector3 s=r;s[c]=-inner[c];face(new[]{p,q,r,s});
        }
        for(int x=-1;x<=1;x+=2)for(int y=-1;y<=1;y+=2)for(int z=-1;z<=1;z+=2)
            face(new[]{new Vector3(x*h.x,y*inner.y,z*inner.z),new Vector3(x*inner.x,y*h.y,z*inner.z),new Vector3(x*inner.x,y*inner.y,z*h.z)});
        var mesh=new Mesh{name="Painel low poly chanfrado"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);
        mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }

    static void SaveRefrigeratorPrefab(Transform fridge)
    {
        const string folder="Assets/ChickenHeistGenerated/PlayerHome/Props";
        if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/ChickenHeistGenerated/PlayerHome","Props");
        var instance=Object.Instantiate(fridge.gameObject);
        try
        {
            instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            PrefabUtility.SaveAsPrefabAsset(instance,folder+"/GeladeiraAntiga.prefab");
        }
        finally{Object.DestroyImmediate(instance);}
    }

    static void AuditFixtures(Transform interior, Transform coop)
    {
        var lines = new List<string>();
        Bounds fridge = LocalBounds(interior,interior.Find("Geladeira antiga"));
        bool inside = fridge.min.x>-4.31f && fridge.max.z<6.82f && fridge.min.y>=.629f && fridge.max.y<3.24f;
        lines.Add((inside?"PASS":"FAIL")+" refrigerator grounded, behind circulation, clear of stove and walls: "+fridge);
        Physics.SyncTransforms();
        int roofHits=0,roofSamples=0;
        if (coop!=null) foreach (var filter in coop.GetComponentsInChildren<MeshFilter>())
        {
            if (!filter.name.StartsWith("Chapa ondulada rasgada")) continue;
            var collider=filter.GetComponent<MeshCollider>();
            Vector3 p=collider.bounds.center-Vector3.up*.5f;
            roofSamples++;
            if(collider.Raycast(new Ray(p,Vector3.up),out var hit,2))roofHits++;
        }
        lines.Add((roofSamples==4 && roofHits==4?"PASS":"FAIL")+" roof underside upward rays: "+roofHits+"/"+roofSamples);
        var door=interior.GetComponentInChildren<HomeDoor>();
        Quaternion saved=door.hinge.localRotation;
        door.hinge.localRotation=Quaternion.identity;Physics.SyncTransforms();
        int leaks=0;
        foreach (float x in new[]{-3.975f,-3.96f,-3.94f,-3.22f,-2.50f,-2.48f,-2.46f})
        foreach (float y in new[]{.62f,1f,2f,2.845f,2.875f})
        {
            var p=interior.TransformPoint(new Vector3(x,y,1.8f));
            if(!Physics.Raycast(p,Vector3.forward,.95f,~0,QueryTriggerInteraction.Ignore))leaks++;
        }
        door.hinge.localRotation=saved;Physics.SyncTransforms();
        lines.Add((leaks==0?"PASS":"FAIL")+" closed entrance coverage: "+leaks+" leaks");
        File.WriteAllLines(Output+"/fixtures-audit.txt",lines);
    }
}
