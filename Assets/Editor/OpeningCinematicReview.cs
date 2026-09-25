using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class OpeningCinematicReview
{
    const string Key="OpeningCinematicReview",Folder="output/opening-cinematic-review";
    static List<string> report=new List<string>();static int lastLine=-1;static float start;static bool active,failed;
    static Vector3 originalPosition;static Quaternion originalRotation;
    static Vector3 previousHand;static bool motion;static int captures;static bool decline;
    static OpeningCinematicReview(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void InstallAndRun()
    {
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("Assets/Resources/Cinematics");
        var stage=new GameObject("OpeningStage");var component=stage.AddComponent<OpeningCinematicStage>();
        var elias=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChickenHeistGenerated/Characters/ProtagonistV2/Protagonist.prefab"),stage.transform);
        elias.name="Elias";
        foreach(var b in elias.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(b);
        foreach(var t in elias.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.layer=0;
            if(t.name=="Corpo em primeira pessoa" || t.name.Contains("Celular"))t.gameObject.SetActive(false);
        }
        foreach(var r in elias.GetComponentsInChildren<Renderer>()){r.enabled=true;r.forceRenderingOff=false;r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.On;}
        elias.transform.localPosition=new Vector3(-4.0f,0,-5.7f);elias.transform.localRotation=Quaternion.Euler(0,110,0);
        var lia=VillagerNPCFactory.Create(stage.transform,"Lia","peasant_4");
        foreach(var b in lia.GetComponentsInChildren<MonoBehaviour>(true))Object.DestroyImmediate(b);
        lia.transform.localPosition=new Vector3(-2,0,-5.4f);lia.transform.localRotation=Quaternion.Euler(0,-110,0);
        component.elias=elias.transform;component.lia=lia.transform;
        PrefabUtility.SaveAsPrefabAsset(stage,"Assets/Resources/Cinematics/OpeningStage.prefab");Object.DestroyImmediate(stage);
        var decline=new GameObject("DeclineStage");var dc=decline.AddComponent<DeclineCinematicStage>();
        var husband=VillagerNPCFactory.Create(decline.transform,"Osvaldo","peasant_3");
        var wife=VillagerNPCFactory.Create(decline.transform,"Joana","city_dwellers_2");
        foreach(var actor in new[]{husband,wife})foreach(var driver in actor.GetComponentsInChildren<MonoBehaviour>())Object.DestroyImmediate(driver);
        husband.transform.localPosition=new Vector3(9,0,-7.5f);husband.transform.localRotation=Quaternion.Euler(0,160,0);
        wife.transform.localPosition=new Vector3(11,0,-7.5f);wife.transform.localRotation=Quaternion.Euler(0,200,0);
        dc.osvaldo=husband.transform;dc.joana=wife.transform;
        PrefabUtility.SaveAsPrefabAsset(decline,"Assets/Resources/Cinematics/DeclineStage.prefab");Object.DestroyImmediate(decline);
        AssetDatabase.SaveAssets();AssetDatabase.Refresh();
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/cinematic-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount());
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){start=Time.realtimeSinceStartup;active=false;failed=false;lastLine=-1;captures=0;motion=false;decline=false;report.Clear();Application.runInBackground=true;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);EditorApplication.Exit(failed?1:0);}
    }
    static void Check(bool value,string label){report.Add((value?"PASS ":"FAIL ")+label);failed|=!value;File.WriteAllLines(Folder+"/checks.txt",report);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying)return;
        try
        {
            if(!active)
            {
                if(Time.realtimeSinceStartup-start<4 || StoryDirector.Instance==null)return;
                GameMenu.Instance.Resume();
                originalPosition=Camera.main.transform.localPosition;originalRotation=Camera.main.transform.localRotation;
                Check(StoryDirector.Instance.Begin(false),"Opening starts with isolated save");active=true;return;
            }
            var director=StoryDirector.Instance;var stage=Object.FindAnyObjectByType<OpeningCinematicStage>();
            if(stage!=null || Object.FindAnyObjectByType<DeclineCinematicStage>()!=null)
            {
                if(stage!=null && previousHand!=Vector3.zero && Vector3.Distance(previousHand,stage.EliasHand)>.0005f)motion=true;
                if(stage!=null)previousHand=stage.EliasHand;
                if(lastLine!=director.CurrentLine)
                {
                    lastLine=director.CurrentLine;
                    Check(director.GetComponent<AudioSource>().clip!=null,"Voice present at line "+lastLine);
                    Capture((decline?"decline-":"line-")+lastLine.ToString("00"));captures++;
                }
            }
            if(!StoryDirector.Active)
            {
                Check(captures==12,"All twelve lines played to completion: "+(decline?"decline":"opening"));Check(!motion,"Actors remain in relaxed pose without arm swings");
                Check(stage==null && Object.FindAnyObjectByType<DeclineCinematicStage>()==null,"Temporary actors removed");Check(HouseholdEconomy.Instance.Account.introSeen,"Completion saved");
                Check(Vector3.Distance(Camera.main.transform.localPosition,originalPosition)<.01f,"Player camera position restored");
                Check(Quaternion.Angle(Camera.main.transform.localRotation,originalRotation)<.1f,"Player camera rotation restored");
                Check(director.Begin(false),"Can replay for skip test");director.Complete();Check(!StoryDirector.Active,"Skip releases scene");
                if(!decline){decline=true;captures=0;lastLine=-1;previousHand=Vector3.zero;motion=false;HouseholdEconomy.Instance.Commit(a=>{a.pendingVision=true;return true;},"Review");Check(director.Begin(true),"Decline scene starts");return;}
                Check(!HouseholdEconomy.Instance.Account.pendingVision,"Decline event cleared");
                EditorApplication.isPlaying=false;
            }
            if(Time.realtimeSinceStartup-start>240){Check(false,"Review timed out");EditorApplication.isPlaying=false;}
        }
        catch(Exception ex){Check(false,ex.ToString());EditorApplication.isPlaying=false;}
    }
    static void Capture(string name)
    {
        var camera=Camera.main;var old=camera.targetTexture;var active=RenderTexture.active;
        var target=new RenderTexture(1280,720,24);camera.targetTexture=target;camera.Render();RenderTexture.active=target;
        var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();
        File.WriteAllBytes(Folder+"/"+name+".png",texture.EncodeToPNG());camera.targetTexture=old;RenderTexture.active=active;
        Object.DestroyImmediate(target);Object.DestroyImmediate(texture);
    }
}

