using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Diagnostic: where each sleeping farmer lies (bed, roof above, house nearby) and whether the Sleep clip
// actually drives the farmer's skeleton. Run: Unity.exe -batchmode -projectPath . -executeMethod FarmerSleepAudit.Begin
[InitializeOnLoad]
public static class FarmerSleepAudit
{
    const string Key="FarmerSleepAudit",Folder="output/farmer-sleep-audit";
    static readonly List<string> report=new List<string>();
    static int phase;static float next;

    static FarmerSleepAudit(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void Begin()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/farmer-audit-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount{introSeen=true});SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;report.Clear();next=Time.realtimeSinceStartup+4;Application.runInBackground=true;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(0);}
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try
        {
            if(phase==0){GameMenu.Instance.Resume();next=Time.realtimeSinceStartup+6;phase++;return;}
            Audit();
        }
        catch(Exception e){report.Add("FAIL "+e);}
        File.WriteAllLines(Folder+"/report.txt",report);EditorApplication.isPlaying=false;
    }
    static string Path_(Transform t){var s=t.name;while(t.parent!=null){t=t.parent;s=t.name+"/"+s;}return s;}
    static void Audit()
    {
        int shot=0;
        foreach(var home in Object.FindObjectsByType<FarmerResidence>())
        {
            var farmer=home.GetComponent<FarmerStateMachine>()??home.GetComponentInParent<FarmerStateMachine>()??home.GetComponentInChildren<FarmerStateMachine>();
            var body=home.transform;
            string roof=Physics.Raycast(body.position+Vector3.up*.6f,Vector3.up,out var hit,12)?hit.collider.name+" @"+(hit.point.y-body.position.y).ToString("0.0")+"m":"open sky";
            report.Add(Path_(home.transform)+" | activity="+(farmer!=null?farmer.Activity.ToString():"?")+" atBed="+home.AtBed+
                " | pos="+body.position+" bed="+(home.bedPosition!=null?Path_(home.bedPosition)+" "+home.bedPosition.position:"none")+" | above: "+roof);
            var bones=home.GetComponentsInChildren<Transform>();
            Transform B(params string[] n)=>bones.FirstOrDefault(t=>n.Contains(t.name));
            var hl=B("Hand_L","HandL");var hr=B("Hand_R","HandR");var pel=B("Pelvis","Hips");var hd=B("Head");
            if(hl!=null && hr!=null && pel!=null && hd!=null)
                report.Add("   pose: hand span "+Vector3.Distance(hl.position,hr.position).ToString("0.00")+"m, pelvis height "+(pel.position.y-body.position.y).ToString("0.00")+"m, head height "+(hd.position.y-body.position.y).ToString("0.00")+"m, body length "+Vector3.Distance(hd.position,pel.position).ToString("0.00")+"m");
            var anim=home.GetComponentInChildren<Animation>();
            if(anim!=null)
            {
                var clip=anim.GetClip("Sleep");
                if(clip==null)report.Add("   no Sleep clip; clips: "+string.Join(",",anim.Cast<AnimationState>().Select(s=>s.name)));
                else
                {
                    var paths=AnimationUtility.GetCurveBindings(clip).Select(b=>b.path).Distinct().ToList();
                    int resolved=paths.Count(p=>p.Length==0 || anim.transform.Find(p)!=null);
                    report.Add("   Sleep clip '"+clip.name+"' length "+clip.length+" curves on "+paths.Count+" paths, resolved "+resolved+"; sample missing: "+string.Join(", ",paths.Where(p=>p.Length>0 && anim.transform.Find(p)==null).Take(3)));
                    report.Add("   playing: "+string.Join(",",anim.Cast<AnimationState>().Where(s=>anim.IsPlaying(s.name)).Select(s=>s.name+" w"+s.weight.ToString("0.00"))));
                }
            }
            else
            {
                var animator=home.GetComponentInChildren<Animator>();
                report.Add("   no legacy Animation; Animator="+(animator!=null?(animator.runtimeAnimatorController!=null?animator.runtimeAnimatorController.name:"no controller"):"none"));
            }
            if(shot<3)
            {
                var go=new GameObject("audit cam");var cam=go.AddComponent<Camera>();
                var hipsT=home.GetComponentsInChildren<Transform>().FirstOrDefault(t=>t.name=="Pelvis" || t.name=="Hips")??body;
                cam.transform.position=new Vector3(hipsT.position.x,body.position.y+2.45f,hipsT.position.z);cam.transform.rotation=Quaternion.Euler(90,body.eulerAngles.y,0);cam.fieldOfView=80;cam.nearClipPlane=.05f;
                var tex=new RenderTexture(800,600,24);cam.targetTexture=tex;cam.Render();RenderTexture.active=tex;
                var png=new Texture2D(800,600,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,800,600),0,0);png.Apply();
                File.WriteAllBytes(Folder+"/farmer-"+shot+".png",png.EncodeToPNG());RenderTexture.active=null;cam.targetTexture=null;
                Object.DestroyImmediate(png);tex.Release();Object.DestroyImmediate(go);shot++;
            }
        }
    }
}
