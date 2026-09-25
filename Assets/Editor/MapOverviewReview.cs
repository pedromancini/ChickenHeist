using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Edit-mode captures for judging the map: whole-world top view, labelled landmarks and
// eye-level views along the roads. Lighting is lifted so layout reads clearly.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod MapOverviewReview.Capture
public static class MapOverviewReview
{
    const string Folder="output/map-review";
    public static void Capture()
    {
        Directory.CreateDirectory(Folder);
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.56f,.6f);
        var sun=new GameObject("Map review sun").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.2f;sun.transform.rotation=Quaternion.Euler(50,-30,0);
        var lines=new List<string>();
        var renderers=Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>r.enabled && r.gameObject.activeInHierarchy).ToArray();
        var terrain=GameObject.Find("Terreno Ondulado Low Poly").GetComponent<Renderer>().bounds;
        lines.Add("World bounds: "+terrain.size.ToString("0"));
        var farms=Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None).OrderBy(f=>f.layoutIndex).ToArray();
        foreach(var f in farms)lines.Add($"Farm {f.layoutIndex} {f.identity} lot {f.lot}");
        var home=HouseholdEconomy.Instance!=null?HouseholdEconomy.Instance.home:GameObject.Find("Casa do Protagonista - Sitio do Recomeco").transform;
        var market=Object.FindFirstObjectByType<VillageMarket>();
        lines.Add("Home "+home.position.ToString("0")+" market "+market.transform.position.ToString("0"));
        var roads=Object.FindObjectsByType<RuralRoadSpan>(FindObjectsSortMode.None);
        lines.Add("Road spans "+roads.Length+" total length "+roads.Sum(r=>Vector3.Distance(r.start,r.end)).ToString("0")+" m");
        // Density of props per 100x100 m cell, to find empty stretches.
        var cells=new Dictionary<(int,int),int>();
        foreach(var r in renderers){var p=r.bounds.center;var k=(Mathf.FloorToInt(p.x/100),Mathf.FloorToInt(p.z/100));cells[k]=cells.TryGetValue(k,out int c)?c+1:1;}
        int total=0,empty=0;
        for(int x=Mathf.FloorToInt(terrain.min.x/100);x<=Mathf.FloorToInt(terrain.max.x/100);x++)for(int z=Mathf.FloorToInt(terrain.min.z/100);z<=Mathf.FloorToInt(terrain.max.z/100);z++)
        {total++;if(!cells.TryGetValue((x,z),out int c) || c<25)empty++;}
        lines.Add($"100 m cells: {total}, nearly empty (<25 renderers): {empty}");
        var cam=new GameObject("Map review camera").AddComponent<Camera>();UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam).renderPostProcessing=true;cam.farClipPlane=4000;cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.45f,.6f,.75f);
        // Whole-world top view.
        cam.orthographic=true;cam.orthographicSize=Mathf.Max(terrain.extents.x,terrain.extents.z)*1.02f;
        cam.transform.position=terrain.center+Vector3.up*600;cam.transform.rotation=Quaternion.Euler(90,0,0);
        Save(cam,"world-top",2000,2000,farms.Select(f=>(new Vector3(f.lot.center.x,0,f.lot.center.y),Color.red)).Append((home.position,Color.yellow)).Append((market.transform.position,Color.cyan)).ToArray());
        // Oblique overview.
        cam.orthographic=false;cam.fieldOfView=50;
        cam.transform.position=terrain.center+new Vector3(0,420,-terrain.extents.z*1.15f);cam.transform.LookAt(terrain.center);
        Save(cam,"world-oblique",2000,1200,null);
        // Eye level: from home, near market, and every few hundred metres along the longest roads.
        cam.fieldOfView=62;int index=0;
        void Eye(Vector3 at,Vector3 forward,string label)
        {
            if(Physics.Raycast(at+Vector3.up*200,Vector3.down,out var hit,400))at.y=hit.point.y;
            cam.transform.position=at+Vector3.up*1.7f;cam.transform.rotation=Quaternion.LookRotation(new Vector3(forward.x,-.05f,forward.z));
            Save(cam,$"eye-{index++:00}-{label}",1280,720,null);lines.Add($"eye-{index-1:00} {label} at {at:0}");
        }
        Eye(home.position+home.forward*12,-home.forward,"home-front");
        Eye(market.transform.position+market.transform.forward*15,-market.transform.forward,"market");
        foreach(var road in roads.OrderByDescending(r=>Vector3.Distance(r.start,r.end)).Take(8))
            Eye(Vector3.Lerp(road.start,road.end,.5f),road.end-road.start,"road");
        foreach(var f in farms.Take(3))Eye(f.entrance+(f.entrance-new Vector3(f.lot.center.x,0,f.lot.center.y)).normalized*10,new Vector3(f.lot.center.x,0,f.lot.center.y)-f.entrance,"farm-"+f.layoutIndex);
        File.WriteAllLines(Folder+"/map-facts.txt",lines);
        Object.DestroyImmediate(cam.gameObject);Object.DestroyImmediate(sun.gameObject);
    }
    static void Save(Camera cam,string name,int w,int h,(Vector3 p,Color c)[] marks)
    {
        var rt=new RenderTexture(w,h,24);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;
        var tex=new Texture2D(w,h,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,w,h),0,0);
        if(marks!=null)foreach(var m in marks){var v=cam.WorldToViewportPoint(m.p);int x=(int)(v.x*w),y=(int)(v.y*h);for(int dx=-9;dx<=9;dx++)for(int dy=-9;dy<=9;dy++)if(Mathf.Abs(dx)+Mathf.Abs(dy)<11 && x+dx>=0 && x+dx<w && y+dy>=0 && y+dy<h)tex.SetPixel(x+dx,y+dy,m.c);}
        tex.Apply();File.WriteAllBytes($"{Folder}/{name}.png",tex.EncodeToPNG());
        RenderTexture.active=null;cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);
    }
}
