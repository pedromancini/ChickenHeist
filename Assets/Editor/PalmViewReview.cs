using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class PalmViewReview
{
    const string Key="PalmViewReview",Folder="output/palm-view",Samples="Tools/PalmSamples/";
    static int phase;static double next;static bool failed;
    static Light sun;static float shadow;static bool fog;static Camera main;

    static PalmViewReview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void Review()
    {
        Directory.CreateDirectory(Folder);File.WriteAllText(Folder+"/checks.txt","");
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/palm-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount{introSeen=true,regionalSecurity=true});
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;failed=false;next=EditorApplication.timeSinceStartup+5;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);PalmFarmView.StillImage=null;PalmFarmView.SkipCamera=false;EditorApplication.Exit(failed?1:0);}
    }
    static void Check(bool ok,string message){failed|=!ok;File.AppendAllText(Folder+"/checks.txt",(ok?"PASS ":"FAIL ")+message+"\n");}
    static void Note(string message)=>File.AppendAllText(Folder+"/checks.txt","     "+message+"\n");

    static Texture2D Image(string name){var t=new Texture2D(2,2,TextureFormat.RGBA32,false);t.LoadImage(File.ReadAllBytes(Samples+name+".png"));return t;}
    static float LandmarkError(string name)
    {
        var expected=File.ReadAllLines(Samples+name+".txt").Where(l=>l.Trim().Length>0)
            .Select(l=>{var p=l.Split(' ');return new Vector2(float.Parse(p[0],System.Globalization.CultureInfo.InvariantCulture),float.Parse(p[1],System.Globalization.CultureInfo.InvariantCulture));}).ToArray();
        var points=PalmFarmView.Current.Tracker.Points;
        return Enumerable.Range(0,21).Average(i=>Vector2.Distance(points[i],expected[i]));
    }
    static Vector3 CameraUp(){var v=PalmFarmView.Current;return v.View.transform.InverseTransformDirection(v.Model.transform.up);}
    static void Capture(string name)
    {
        var camera=PalmFarmView.Current.View;int h=720,w=Mathf.RoundToInt(h*camera.aspect);
        var rt=new RenderTexture(w,h,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var t=new Texture2D(w,h,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,w,h),0,0);t.Apply();
        File.WriteAllBytes(Folder+"/"+name+".png",t.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(t);
    }
    static void Wait(double seconds,int nextPhase){phase=nextPhase;next=EditorApplication.timeSinceStartup+seconds;}

    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(phase==0){GameMenu.Instance.Resume();Wait(1,1);return;}
            if(phase==1)
            {
                main=Camera.main;sun=RenderSettings.sun;fog=RenderSettings.fog;
                shadow=((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).shadowDistance;
                ProtagonistPhone.Instance.SetOpen(true);
                PalmFarmView.StillImage=Image("palma_para_cima");PalmFarmView.Open(0);
                Check(PalmFarmView.Active && ProtagonistPhone.IsOpen,"Ver em 3D opens over the tablet");
                Check(Camera.main==null || !Camera.main.enabled,"The game camera is paused while the view is open");
                Wait(2.5,2);return;
            }
            if(phase==2)
            {
                var view=PalmFarmView.Current;var tracker=view.Tracker;
                Check(tracker.Found && tracker.Presence>.9f,"Palm held up is found (presence "+tracker.Presence.ToString("0.000")+")");
                float error=LandmarkError("palma_para_cima");
                Check(error<5,"Hand points match the reference within 5 px (mean "+error.ToString("0.0")+" px)");
                Check(view.Showing,"The farm appears on the palm");
                var up=CameraUp();
                Check(up.y>.3f,"The farm stands up from a palm held face up (up "+up.ToString("0.00")+")");
                Check(tracker.PalmWidth>.045f && tracker.PalmWidth<.12f,"Palm width is a real hand size ("+(tracker.PalmWidth*100).ToString("0.0")+" cm)");
                var size=view.Model.transform.localScale.x;
                Check(size>tracker.PalmWidth*1.3f && size<tracker.PalmWidth*1.8f,"The island is sized to the palm ("+(size*100).ToString("0.0")+" cm across)");
                Check(view.Model.cones.gameObject.activeSelf,"Phase two gear is shown once the valley installed it");
                int wires=view.Model.cones.GetComponentsInChildren<Transform>().Count(t=>t.name=="Arame com latas");
                Check(view.Model.cameras>0 || wires>0,"The farm shows its cameras and wires ("+view.Model.cameras+" cameras, "+wires+" wires)");
                Check(view.Model.farmName+" - "+view.Model.kind==ProtagonistPhone.Instance.farmNames[0],"The model is the farm from the photo");
                Check(view.Model.GetComponentsInChildren<Renderer>(true).All(r=>r.gameObject.layer==PalmFarmView.Layer),"Every piece renders on the view layer");
                var pixels=Image("palma_para_cima");var colours=pixels.GetPixels32();var clock=Stopwatch.StartNew();
                for(int i=0;i<10;i++)tracker.Process(colours,pixels.width,pixels.height,false,false);
                Note("tracking "+(clock.Elapsed.TotalMilliseconds/10).ToString("0.0")+" ms per frame");
                Capture("palma_para_cima");
                PalmFarmView.Close();PalmFarmView.StillImage=Image("palma_de_frente");PalmFarmView.Open(3);
                Wait(2.5,3);return;
            }
            if(phase==3)
            {
                var view=PalmFarmView.Current;var tracker=view.Tracker;
                Check(tracker.Found,"Palm facing the camera is found");
                float error=LandmarkError("palma_de_frente");
                Check(error<4,"Hand points match the reference within 4 px (mean "+error.ToString("0.0")+" px)");
                var up=CameraUp();
                Check(up.z<-.3f,"The farm faces the camera from a palm facing it (up "+up.ToString("0.00")+")");
                Capture("palma_de_frente");
                typeof(PalmFarmView).GetMethod("LoadModel",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,new object[]{view.Farm+1});
                Check(view.Farm==4 && view.Model.farmName+" - "+view.Model.kind==ProtagonistPhone.Instance.farmNames[4],"Switching farm loads the next one");
                Check(ProtagonistPhone.Instance.PhotoIndex==4,"The tablet follows the farm being viewed");
                PalmFarmView.Close();
                Check(!PalmFarmView.Active && ProtagonistPhone.IsOpen,"Closing goes back to the tablet");
                Check(main!=null && main.enabled && RenderSettings.sun==sun && RenderSettings.fog==fog,"The game camera, sun and fog come back");
                Check(Mathf.Approximately(((UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline).shadowDistance,shadow),"Shadow distance is restored");
                var blank=new Texture2D(640,480);blank.SetPixels32(Enumerable.Repeat(new Color32(120,118,112,255),640*480).ToArray());blank.Apply();
                PalmFarmView.StillImage=blank;PalmFarmView.Open(1);
                Wait(1.5,4);return;
            }
            if(phase==4)
            {
                var view=PalmFarmView.Current;
                Check(!view.Tracker.Found && !view.Showing,"No hand, no farm");
                PalmFarmView.Close();
                PalmFarmView.StillImage=null;PalmFarmView.SkipCamera=true;
                HouseholdEconomy.Instance.Commit(a=>{a.regionalSecurity=false;return true;},"Review before phase two");
                PalmFarmView.Open(2);
                Wait(1.5,5);return;
            }
            if(phase==5)
            {
                var view=PalmFarmView.Current;
                Check(view.FreeView && view.Showing,"Without a camera the farm turns on its own");
                Check(!view.Model.cones.gameObject.activeSelf,"Before phase two the farm has no cameras or wires");
                Capture("sem_camera");
                ProtagonistPhone.Instance.SetOpen(false);
                Check(!PalmFarmView.Active && !ProtagonistPhone.IsOpen,"Closing the tablet closes the view");
                Check(main.enabled,"The game camera is back after the tablet closes");
                EditorApplication.isPlaying=false;phase=99;
            }
        }
        catch(Exception e){Check(false,"Review error: "+e);EditorApplication.isPlaying=false;phase=99;}
    }
}
