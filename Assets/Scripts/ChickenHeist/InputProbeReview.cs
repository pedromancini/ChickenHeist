using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Runs only with --review-session --input-probe: starts free play, then logs every Space press with the keys held
// and whether a jump followed. Keystrokes come from outside (Tools/send_jump_keys.ps1), through the real OS input.
public class InputProbeReview : MonoBehaviour
{
    readonly List<string> lines=new List<string>();
    string output;PlayerMovement movement;float watchUntil;bool pending,shiftAtPress,sprintAtPress,groundedAtPress;float startY,peak;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch()
    {
        if(!HouseholdEconomy.ReviewSession || Array.IndexOf(Environment.GetCommandLineArgs(),"--input-probe")<0 || FindAnyObjectByType<InputProbeReview>()!=null)return;
        new GameObject("Input probe").AddComponent<InputProbeReview>();
    }
    void Awake()
    {
        DontDestroyOnLoad(gameObject);Application.runInBackground=true;
        output=Path.GetFullPath(Path.Combine(Application.dataPath,"../../../output/input-probe"));Directory.CreateDirectory(output);
    }
    IEnumerator Start()
    {
        yield return new WaitForSecondsRealtime(2);
        var menu=GameMenu.Instance;var old=menu;menu.SendMessage("BeginGame");
        float deadline=Time.realtimeSinceStartup+30;
        while((GameMenu.Instance==null || GameMenu.Instance==old || !StoryDirector.Active) && Time.realtimeSinceStartup<deadline)yield return null;
        if(StoryDirector.Active)StoryDirector.Instance.Complete();
        yield return new WaitForSecondsRealtime(1.5f);
        movement=HeistGameManager.Instance.player.GetComponent<PlayerMovement>();
        File.WriteAllText(Path.Combine(output,"ready.txt"),"ready");
        lines.Add("ready at "+Time.realtimeSinceStartup.ToString("F1"));
        yield return new WaitForSecondsRealtime(40);
        File.WriteAllLines(Path.Combine(output,"presses.txt"),lines);Application.Quit(0);
    }
    void LateUpdate()
    {
        if(movement==null)return;
        var p=movement.transform.position.y;
        if(Input.GetKeyDown(KeyCode.Space))
        {
            if(pending)Report();
            pending=true;watchUntil=Time.time+.5f;startY=p;peak=p;
            shiftAtPress=Input.GetKey(KeyCode.LeftShift);sprintAtPress=movement.estaSprinting;groundedAtPress=!movement.IsAirborne;
        }
        if(pending){peak=Mathf.Max(peak,p);if(Time.time>=watchUntil)Report();}
    }
    void Report()
    {
        pending=false;
        lines.Add("space: shift "+shiftAtPress+" W "+Input.GetKey(KeyCode.W)+" sprinting "+sprintAtPress+" grounded "+groundedAtPress+" -> rise "+(peak-startY).ToString("F2")+" m "+(peak-startY>.3f?"JUMPED":"NO JUMP"));
    }
}
