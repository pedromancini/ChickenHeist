using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class GateScareRegression
{
    const string Key="GateScareRegression";
    const string Output="output/gate-scare-regression.txt";
    static int phase;static double next;static bool failed;
    static ChickenCoopLockpick coop;
    static GateScareRegression(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void Run()
    {
        Directory.CreateDirectory("output");File.WriteAllText(Output,"");
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();
        economy.editorTestSavePath=Path.GetFullPath("Temp/gate-scare-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount());
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;failed=false;next=EditorApplication.timeSinceStartup+4;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(failed?1:0);}
    }
    static void Check(bool value,string label){failed|=!value;File.AppendAllText(Output,(value?"PASS ":"FAIL ")+label+"\n");}
    static void Field(string name,object value)=>typeof(ChickenCoopLockpick).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(coop,value);
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || EditorApplication.timeSinceStartup<next)return;
        try
        {
            if(phase==0)
            {
                GameMenu.Instance.Resume();next=EditorApplication.timeSinceStartup+.2;phase=1;return;
            }
            if(phase==1)
            {
                var gates=Object.FindObjectsByType<RuralGate>();Check(gates.Length>0,"Scene contains rural gates");
                foreach(var gate in gates)
                {
                    gate.RestoreOpen(false);var mesh=gate.GetComponentInChildren<MeshFilter>();
                    var bounds=mesh.sharedMesh.bounds;
                    Vector3 a=bounds.min,b=bounds.max;
                    float length=Vector3.Distance(mesh.transform.TransformPoint(a),mesh.transform.TransformPoint(b));
                    Vector3 closed=gate.transform.position;Quaternion rotation=gate.transform.rotation;
                    for(int i=0;i<3;i++)
                    {
                        gate.RestoreOpen(true);
                        Check(Mathf.Abs(Vector3.Distance(mesh.transform.TransformPoint(a),mesh.transform.TransformPoint(b))-length)<.002f,"Open gate preserves mesh dimensions: "+gate.transform.position);
                        Check(Vector3.Distance(gate.InteractionPoint,gate.GetComponentInChildren<Renderer>().bounds.center)<.01f,"Interaction follows open gate: "+gate.transform.position);
                        gate.RestoreOpen(false);
                    }
                    Check(Vector3.Distance(closed,gate.transform.position)<.002f && Quaternion.Angle(rotation,gate.transform.rotation)<.02f,"Repeated close restores original placement: "+gate.transform.position);
                }
                var game=HeistGameManager.Instance;Check(game.StartMission(0),"Start mission for scare regression");
                coop=Object.FindObjectsByType<ChickenCoopLockpick>().First(c=>game.IsMissionTarget(c));
                coop.RestoreOpen(false);
                typeof(ChickenCoopLockpick).GetMethod("BeginChallenge",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(coop,null);
                Field("mistakes",2);Field("nextScare",-1f);
                // Exercise the missing-reference fallback against live birds in the scene.
                coop.scareChicken=null;
                int wrong=(coop.Latch.NextPiece+1)%3;
                for(int i=0;i<200 && !coop.Scare.IsActive && coop.ChallengeActive;i++)coop.Manipulate(.02f,wrong,0,true);
                Check(coop.scareChicken!=null && coop.Scare.IsActive,"Third mistake finds a live bird and starts scare");
                Check(coop.ChallengeActive,"Third mistake does not cancel scare in triggering frame");
                next=EditorApplication.timeSinceStartup+.35;phase=2;return;
            }
            if(phase==2)
            {
                Check(coop.Scare.IsActive && coop.Scare.Elapsed>.1f,"Scare remains visible across frames");
                ScreenCapture.CaptureScreenshot("output/gate-scare-regression.png");
                next=EditorApplication.timeSinceStartup+1.2;phase=3;return;
            }
            if(phase==3)
            {
                Check(!coop.Scare.IsActive && ChickenScare.Active==null,"Scare completes naturally");
                Check(!coop.ChallengeActive && ChickenCoopLockpick.Active==null,"Jam releases interaction after scare");
                Check(coop.scareChicken.gameObject.activeInHierarchy && coop.scareChicken.GetComponent<InteractableChicken>().enabled,"Bird restored after delayed jam");
                EditorApplication.isPlaying=false;phase=4;
            }
        }
        catch(Exception e){Check(false,e.ToString());EditorApplication.isPlaying=false;}
    }
}
