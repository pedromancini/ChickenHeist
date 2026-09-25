using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class RestFailureAudit
{
    const string Key="RestFailureAudit";static int phase,day;static float next;static bool failed;
    static RestFailureAudit(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;}
    public static void Begin(){EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);var e=Object.FindAnyObjectByType<HouseholdEconomy>();e.editorTestSavePath=Path.GetFullPath("Temp/rest-audit-"+Guid.NewGuid()+".json");HouseholdEconomy.SaveAccount(e.editorTestSavePath,new HouseholdAccount());File.WriteAllText("output/rest-failure-checks.txt","");SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;}
    static void Changed(PlayModeStateChange s){if(!SessionState.GetBool(Key,false))return;if(s==PlayModeStateChange.EnteredPlayMode){next=Time.realtimeSinceStartup+4;phase=0;failed=false;}if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorApplication.Exit(failed?1:0);}}
    static void Check(bool ok,string msg){failed|=!ok;File.AppendAllText("output/rest-failure-checks.txt",(ok?"PASS ":"FAIL ")+msg+"\n");}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try{
            if(phase==0){GameMenu.Instance.Resume();next=Time.realtimeSinceStartup+.2f;phase++;return;}
            var economy=HouseholdEconomy.Instance;var game=HeistGameManager.Instance;
            if(phase==1){var bed=Object.FindAnyObjectByType<HomeNextNight>();var cc=game.player.GetComponent<CharacterController>();cc.enabled=false;game.player.position=bed.transform.position-Vector3.up;game.player.GetComponent<PlayerMovement>().enabled=false;
                var checkpoint=Object.FindAnyObjectByType<GameCheckpoint>();Check(checkpoint.Save(out _),"Initial checkpoint saved");day=economy.Account.day;
                using(var file=new FileStream(economy.CheckpointPath,FileMode.Open,FileAccess.Read,FileShare.None)){
                    Check(!bed.TryRest() && !HomeNextNight.IsResting,"Failed checkpoint prevents rest transition");
                    int advanced=economy.Account.day;Check(advanced==day+1,"Morning prepared once before blocked save");
                    Check(!bed.TryRest() && economy.Account.day==advanced,"Repeated failed save does not advance another day");
                }
                Check(bed.TryRest() && HomeNextNight.IsResting && economy.Account.day==day+1,"Unlocked save resumes same prepared morning");
                Check(GameCheckpoint.TryRead(economy.CheckpointPath,out var data,out _) && data.account.day==day+1,"Retry writes correct day into checkpoint");
                // Stop the transition in this isolated Editor fixture, before loading the production scene account path.
                bed.StopAllCoroutines();Object.Destroy(bed);EditorApplication.isPlaying=false;
            }
        }catch(Exception e){Check(false,e.ToString());EditorApplication.isPlaying=false;}
    }
}
