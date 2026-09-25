using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Play-mode views through the real gameplay camera (post-processing, fog, night lighting),
// plus the same views with lifted light to judge colour.
// Run: Unity.exe -projectPath . -executeMethod MapPlayReview.Begin
[InitializeOnLoad]
public static class MapPlayReview
{
    const string Key="MapPlayReview",Folder="output/map-review/play";
    static float next;static int step;static List<(Vector3 at,Vector3 forward,string label)> views;
    static MapPlayReview()
    {
        EditorApplication.update+=Tick;
        EditorApplication.playModeStateChanged+=s=>{
            if(!SessionState.GetBool(Key,false))return;
            if(s==PlayModeStateChange.EnteredPlayMode){next=Time.realtimeSinceStartup+5;step=-1;Application.runInBackground=true;}
            if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}
        };
    }
    public static void Begin()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/mapplay-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount());SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        var eye=Camera.main;
        if(step<0)
        {
            GameMenu.Instance.Resume();
            var player=HeistGameManager.Instance.player;player.GetComponent<PlayerMovement>().enabled=false;player.GetComponentInChildren<PlayerLook>().enabled=false;
            eye.transform.SetParent(null,true);
            var home=HouseholdEconomy.Instance.home;var market=Object.FindFirstObjectByType<VillageMarket>().transform;
            views=new List<(Vector3,Vector3,string)>{(home.position+home.forward*14,-home.forward,"home"),(market.position+market.forward*16,-market.forward,"market")};
            foreach(var road in Object.FindObjectsByType<RuralRoadSpan>(FindObjectsSortMode.None).OrderByDescending(r=>Vector3.Distance(r.start,r.end)).Take(5))views.Add((Vector3.Lerp(road.start,road.end,.5f),road.end-road.start,"road"));
            foreach(var f in Object.FindObjectsByType<FarmLayoutInfo>(FindObjectsSortMode.None).OrderBy(f=>f.layoutIndex).Take(3)){var c=new Vector3(f.lot.center.x,0,f.lot.center.y);views.Add((f.entrance+(f.entrance-c).normalized*10,c-f.entrance,"farm-"+f.layoutIndex));}
            var data=UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(eye);
            var stack=UnityEngine.Rendering.VolumeManager.instance.stack;var grade=stack?.GetComponent<UnityEngine.Rendering.Universal.ColorAdjustments>();
            File.WriteAllLines(Folder+"/diagnostics.txt",new[]{"camera "+eye.name+" post="+data.renderPostProcessing+" mask="+data.volumeLayerMask.value+" trigger="+(data.volumeTrigger?data.volumeTrigger.name:"-")+" type="+data.renderType,
                "volumes "+Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None).Length+" saturation="+(grade!=null?grade.saturation.value.ToString():"null")+" active="+(grade!=null && grade.IsActive()),
                "pipeline "+UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.name+" quality "+QualitySettings.GetQualityLevel()});
            step=0;next=Time.realtimeSinceStartup+1;return;
        }
        if(step<views.Count*2)
        {
            var v=views[step/2];bool lifted=step%2==1;var at=v.at;
            if(Physics.Raycast(at+Vector3.up*200,Vector3.down,out var hit,400))at.y=hit.point.y;
            eye.transform.position=at+Vector3.up*1.7f;eye.transform.rotation=Quaternion.LookRotation(new Vector3(v.forward.x,-.04f,v.forward.z));
            var fog=RenderSettings.fog;var ambient=RenderSettings.ambientLight;var mode=RenderSettings.ambientMode;Light sun=null;
            if(lifted){RenderSettings.fog=false;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.56f,.6f);sun=new GameObject("lift").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=1.1f;sun.transform.rotation=Quaternion.Euler(45,-30,0);}
            var rt=new RenderTexture(1280,720,24);var old=eye.targetTexture;eye.targetTexture=rt;eye.Render();eye.targetTexture=old;RenderTexture.active=rt;
            var tex=new Texture2D(1280,720,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,720),0,0);tex.Apply();
            File.WriteAllBytes($"{Folder}/{step/2:00}-{v.label}-{(lifted?"lifted":"night")}.png",tex.EncodeToPNG());RenderTexture.active=null;Object.Destroy(rt);Object.Destroy(tex);
            if(lifted){RenderSettings.fog=fog;RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=mode;Object.Destroy(sun.gameObject);}
            step++;next=Time.realtimeSinceStartup+.6f;return;
        }
        EditorApplication.isPlaying=false;
    }
}
