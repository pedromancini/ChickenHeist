using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class FarmerCombatReview
{
    const string Key="FarmerCombatReview",Folder="output/farmer-combat-review";
    static readonly List<string> results=new List<string>();
    static HeistGameManager game;static FarmerStateMachine farmer;static FarmerResidence home;static PlayerHealth health;
    static int phase;static float next,deadline;static bool failed;static Vector3 before,previousPosition;static float previousTime;
    static GameCheckpointData baseline;static GameObject wall;
    static FarmerCombatReview()
    {
        EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false) && (t==LogType.Error || t==LogType.Exception)){Check(false,m);EditorApplication.isPlaying=false;}};
    }
    public static void Begin()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/combat-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount());SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;failed=false;results.Clear();next=Time.realtimeSinceStartup+3;Application.runInBackground=true;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);EditorApplication.Exit(failed?1:0);}
    }
    static void Check(bool value,string message){failed|=!value;results.Add((value?"PASS ":"FAIL ")+message);File.WriteAllLines(Folder+"/checks.txt",results);}
    static void Position(Transform target,Vector3 position){var cc=target.GetComponent<CharacterController>();if(cc!=null)cc.enabled=false;target.position=position;if(cc!=null)cc.enabled=true;Physics.SyncTransforms();}
    static void Capture(string name,Vector3 position,Vector3 target)
    {
        var go=new GameObject("Combat review camera");var eye=go.AddComponent<Camera>();eye.CopyFrom(Camera.main);eye.transform.position=position;eye.transform.LookAt(target);eye.fieldOfView=55;eye.nearClipPlane=.04f;eye.cullingMask=~0;
        var texture=new RenderTexture(1500,1000,24);eye.targetTexture=texture;eye.Render();var old=RenderTexture.active;RenderTexture.active=texture;
        var png=new Texture2D(1500,1000,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1500,1000),0,0);png.Apply();File.WriteAllBytes(Folder+"/"+name+".png",png.EncodeToPNG());
        RenderTexture.active=old;eye.targetTexture=null;Object.DestroyImmediate(png);Object.DestroyImmediate(texture);Object.DestroyImmediate(go);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try
        {
            if(phase==0)
            {
                game=HeistGameManager.Instance;GameMenu.Instance.Resume();health=game.player.GetComponent<PlayerHealth>();
                game.player.GetComponent<PlayerMovement>().enabled=false;game.player.GetComponentInChildren<PlayerLook>().enabled=false;
                foreach(var ai in Object.FindObjectsByType<FarmerStateMachine>())
                {
                    var residence=ai.GetComponent<FarmerResidence>();
                    Check(residence!=null && residence.AtBed && ai.Activity==FarmerActivity.Sleeping,ai.name+" starts in bed inside house");
                    var path=new NavMeshPath();bool found=NavMesh.SamplePosition(residence.bedPosition.position,out var start,1,NavMesh.AllAreas) &&
                        NavMesh.SamplePosition(residence.outsideDoor.position,out var end,1,NavMesh.AllAreas) && NavMesh.CalculatePath(start.position,end.position,NavMesh.AllAreas,path);
                    Check(found && path.status==NavMeshPathStatus.PathComplete,ai.name+" has navigation through the authored doorway");
                }
                Check(game.StartMission(0),"Mission starts");farmer=game.farmerSleep.GetComponent<FarmerStateMachine>();home=farmer.GetComponent<FarmerResidence>();before=farmer.transform.position;
                baseline=Object.FindAnyObjectByType<GameCheckpoint>().Capture();
                var light=new GameObject("Combat review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1;light.transform.rotation=Quaternion.Euler(40,15,0);
                Capture("sleeping",home.bedPosition.position+new Vector3(.85f,1.8f,1),home.bedPosition.position+new Vector3(0,.7f,1.1f));
                next=Time.realtimeSinceStartup+2;phase++;return;
            }
            if(phase==1)
            {
                Check(Vector3.Distance(farmer.transform.position,before)<.05f,"Deep sleep keeps farmer inside without movement");
                var sleep=farmer.sleepSystem;float initial=sleep.CurrentSleep;
                for(int i=0;i<100;i++)NoiseEmitter.EmitGlobal(NoiseSource.VehicleEngine,farmer.transform.position+Vector3.forward*5,2);
                Check(Mathf.Approximately(initial,sleep.CurrentSleep) && sleep.State==FarmerAwakeState.DeepSleep,"Engine arrival and repeated ignition cannot wake sleeping farmer");
                sleep.RestoreSleep(64);Check(sleep.State==FarmerAwakeState.DeepSleep,"Farmer stays asleep below 65");
                sleep.RestoreSleep(initial);sleep.AddNoise(40);Check(sleep.State==FarmerAwakeState.DeepSleep,"Former early wake noise remains below new threshold");
                sleep.AddNoise(70);farmer.Hear(home.outsideDoor.position+Vector3.back*6);
                next=Time.realtimeSinceStartup+1.3f;phase++;return;
            }
            if(phase==2)
            {
                Check(farmer.Activity==FarmerActivity.Waking,"Crossing 65 starts a visible wake-up before movement");
                Check(!farmer.HasVisualContact,"Noise does not give the player's position through walls");
                Capture("waking",home.insideDoor.position+new Vector3(.7f,1.6f,.4f),farmer.transform.position+Vector3.up);
                previousPosition=farmer.transform.position;previousTime=Time.realtimeSinceStartup;deadline=previousTime+20;phase++;return;
            }
            if(phase==3)
            {
                float elapsed=Time.realtimeSinceStartup-previousTime;
                if(Vector3.Distance(farmer.transform.position,previousPosition)>farmer.chaseSpeed*elapsed+.25f)Check(false,"Farmer position jumped during exit");
                previousPosition=farmer.transform.position;previousTime=Time.realtimeSinceStartup;
                if((farmer.Activity==FarmerActivity.Waking || farmer.Activity==FarmerActivity.Leaving) && Time.realtimeSinceStartup<deadline)return;
                Check(farmer.Activity==FarmerActivity.Investigating && Vector3.Distance(farmer.transform.position,home.outsideDoor.position)<1.5f,"Farmer exits physically through doorway: "+farmer.Activity+" at "+farmer.transform.position);
                Capture("outside-with-shotgun",home.outsideDoor.position+new Vector3(2,1.6f,-3),farmer.transform.position+Vector3.up);
                health.TakeDamage(45);Check(health.Injured && Mathf.Approximately(health.Current,55),"Damage creates injury at low health");
                var movement=game.player.GetComponent<PlayerMovement>();movement.estaAgachado=true;movement.ApplyTrapSlow(2);
                Check(Mathf.Abs(movement.CurrentMoveSpeed-movement.velocidadeAgachado*.35f*health.SpeedMultiplier)<.01f,"Injury composes with crouch and trap slowdown");movement.estaAgachado=false;
                var checkpoint=Object.FindAnyObjectByType<GameCheckpoint>();var save=checkpoint.Capture();health.Restore(100);farmer.Restore(null);
                Check(checkpoint.Restore(save,out _) && Mathf.Approximately(health.Current,55) && farmer.Activity==save.farmers.First(s=>s.id==farmer.GetComponent<PersistentWorldId>()?.value || s.id.Contains(farmer.name)).combat.activity,"Checkpoint restores health and farmer activity");
                save.hasHealth=false;foreach(var s in save.farmers)s.combat=null;
                Check(checkpoint.Restore(save,out _) && health.Current==100,"Legacy checkpoint migrates with full health");
                farmer.enabled=false;Position(farmer.transform,new Vector3(0,500,0));farmer.transform.rotation=Quaternion.identity;Position(game.player,new Vector3(0,500,4));
                farmer.Restore(new FarmerCombatSave{activity=FarmerActivity.Aiming,ammo=2,actionRemaining=1,lastKnown=game.player.position});
                next=Time.realtimeSinceStartup+3;phase++;return;
            }
            if(phase==4)
            {
                var gun=farmer.GetComponent<FarmerShotgun>();
                Check(farmer.CanSeePlayer(),"Unobstructed target is visible");
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,501.5f,2);wall.transform.localScale=new Vector3(4,4,.3f);Physics.SyncTransforms();
                Check(!farmer.CanSeePlayer(),"Opaque cover prevents tracking");gun.Fire(game.player.position+Vector3.up*1.35f);
                Check(health.Current==100 && gun.LastShotDamage==0,"Shotgun pellets stop at cover");Object.DestroyImmediate(wall);Physics.SyncTransforms();
                for(int i=0;i<3 && health.Current>50;i++)gun.Fire(game.player.position+Vector3.up*1.35f);
                Check(health.Current<100,"Unblocked shotgun deals damage");
                GameMenu.Instance.Pause();float oldHealth=health.Current;int shots=gun.ShotsFired;gun.Fire(game.player.position+Vector3.up);health.TakeDamage(10);
                Check(health.Current==oldHealth && gun.ShotsFired==shots,"Pause blocks shots and damage");GameMenu.Instance.Resume();
                Capture("shotgun-aim",farmer.transform.position+new Vector3(1.5f,1.5f,2.5f),farmer.transform.position+new Vector3(0,1.15f,.2f));
                next=Time.realtimeSinceStartup+.1f;phase++;return;
            }
            if(phase==5)
            {
                health.TakeDamage(200);Check(health.Dead && game.missionEnded,"Lethal damage enters existing defeat flow");
                GameMenu.Instance.Resume();Check(Object.FindAnyObjectByType<GameCheckpoint>().Restore(baseline,out _) && !health.Dead && !game.missionEnded,"Checkpoint resumes after defeat");
                health.Restore(50);health.RecoverAfterRest();Check(health.Current==100,"Rest recovery removes injury without permanent cost");
                EditorApplication.isPlaying=false;return;
            }
        }
        catch(Exception e){Check(false,e.ToString());EditorApplication.isPlaying=false;}
    }
}
