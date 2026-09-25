using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CoopPressureTests
{
    const string Key="CoopPressureTests",Folder="output/coop-pressure-review";
    static readonly List<string> results=new List<string>();static bool failed;static int phase,noiseCount;static float next;
    static HeistGameManager game;static ChickenCoopLockpick coop;static InteractableChicken bird;static GameCheckpoint checkpoint;static GameCheckpointData baseline;
    static Vector3 birdPosition;static Quaternion birdRotation;static Transform birdParent;static int birdSibling,birdCount,inventory;
    static bool enabledBefore;static bool[] colliderBefore;static float nearBefore;
    static CoopPressureTests()
    {
        EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=Changed;
        Application.logMessageReceived+=(m,s,t)=>{if(SessionState.GetBool(Key,false) && (t==LogType.Error || t==LogType.Exception)){Check(false,m);EditorApplication.isPlaying=false;}};
    }
    public static void Run()
    {
        Directory.CreateDirectory(Folder);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        PrepareWingMesh();
        var economy=Object.FindAnyObjectByType<HouseholdEconomy>();economy.editorTestSavePath=Path.GetFullPath("Temp/coop-pressure-"+Guid.NewGuid().ToString("N")+".json");
        HouseholdEconomy.SaveAccount(economy.editorTestSavePath,new HouseholdAccount());
        if(!Application.isBatchMode){var view=EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));view.Show();view.Focus();}
        SessionState.SetBool(Key,true);EditorApplication.isPlaying=true;
    }
    static void PrepareWingMesh()
    {
        var source=Object.FindObjectsByType<InteractableChicken>().First().GetComponentInChildren<MeshFilter>().sharedMesh;
        var mesh=new Mesh{name="Scare chicken readable mesh",vertices=source.vertices,normals=source.normals,uv=source.uv,colors=source.colors,subMeshCount=source.subMeshCount};
        for(int i=0;i<source.subMeshCount;i++)mesh.SetTriangles(source.GetTriangles(i),i);
        mesh.RecalculateBounds();
        const string path="Assets/Resources/ScareChickenMesh.asset";
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);}
        AssetDatabase.SaveAssets();
    }
    static void Changed(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Key,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode){phase=0;failed=false;results.Clear();next=Time.realtimeSinceStartup+3;Application.runInBackground=true;}
        if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);EditorApplication.Exit(failed?1:0);}
    }
    static void Check(bool value,string message){failed|=!value;results.Add((value?"PASS ":"FAIL ")+message);File.WriteAllLines(Folder+"/checks.txt",results);}
    public static void SolvePiece(ChickenCoopLockpick target,float error=0)
    {
        int completed=target.PinsSet;
        for(int i=0;i<1000 && target.PinsSet==completed;i++)
        {
            var latch=target.Latch;float delta=latch.RequiredPressure+error-latch.Pressure;
            target.Manipulate(.02f,latch.NextPiece,Mathf.Clamp(delta/(.38f*.02f),-1,1),Mathf.Abs(delta)<.01f);
        }
    }
    static void Logic()
    {
        for(int seed=0;seed<12;seed++)foreach(int fps in new[]{30,60,144})foreach(bool professional in new[]{false,true})
        {
            var latch=new CoopPressureLatch(seed);bool slipped=false;float dt=1f/fps;
            for(int step=0;step<fps*25 && latch.Completed<3;step++)
            {
                float delta=latch.RequiredPressure-latch.Pressure;
                slipped|=latch.Step(dt,latch.NextPiece,Mathf.Clamp(delta/(.38f*dt),-1,1),Mathf.Abs(delta)<.02f,professional)<0;
            }
            Check(latch.Completed==3 && !slipped,"Solvable without random failure: farm "+seed+" at "+fps+" fps, professional="+professional);
        }
        var wrong=new CoopPressureLatch(2);bool failedOnce=false;
        for(int i=0;i<100;i++)failedOnce|=wrong.Step(.02f,(wrong.NextPiece+1)%3,0,true,true)<0;
        Check(failedOnce && wrong.Completed==0,"Professional tool cannot force a trapped piece");
        var idle=new CoopPressureLatch(7);for(int i=0;i<500;i++)idle.Step(.02f,idle.NextPiece,1,false,true);
        Check(idle.Completed==0 && idle.Stress==0,"Pressure alone never opens the latch or causes unavoidable failure");
        idle.Step(.05f,(idle.NextPiece+1)%3,0,true,false);float stress=idle.Stress;idle.Step(.05f,idle.NextPiece,0,false,false);
        Check(idle.Stress<stress,"Releasing force cools friction before a slip");
    }
    static void RecordBird()
    {
        bird=coop.scareChicken.GetComponent<InteractableChicken>();bird.sleeping=true;
        birdPosition=bird.transform.localPosition;birdRotation=bird.transform.localRotation;birdParent=bird.transform.parent;birdSibling=bird.transform.GetSiblingIndex();
        enabledBefore=bird.enabled;colliderBefore=bird.GetComponentsInChildren<Collider>().Select(c=>c.enabled).ToArray();
        birdCount=Object.FindObjectsByType<InteractableChicken>().Length;inventory=game.backpack.chickensCarried;nearBefore=Camera.main.nearClipPlane;
    }
    static void Restored(string reason)
    {
        Check(!coop.Scare.IsActive && ChickenScare.Active==null,reason+": scare released");
        Check(bird.transform.parent==birdParent && bird.transform.GetSiblingIndex()==birdSibling && Vector3.Distance(bird.transform.localPosition,birdPosition)<.001f && Quaternion.Angle(bird.transform.localRotation,birdRotation)<.01f,reason+": original pose and hierarchy restored");
        Check(bird.enabled==enabledBefore && bird.GetComponentsInChildren<Collider>().Select(c=>c.enabled).SequenceEqual(colliderBefore),reason+": interaction and collision restored");
        Check(Object.FindObjectsByType<InteractableChicken>().Length==birdCount && game.backpack.chickensCarried==inventory,reason+": no missing or duplicated bird");
        Check(Mathf.Approximately(Camera.main.nearClipPlane,nearBefore),reason+": camera restored");
    }
    static void Capture(string name,int width,int height)
    {
        var eye=Camera.main;var rt=new RenderTexture(width,height,24);var oldTarget=eye.targetTexture;float oldAspect=eye.aspect;eye.targetTexture=rt;eye.aspect=(float)width/height;eye.Render();
        var old=RenderTexture.active;RenderTexture.active=rt;var png=new Texture2D(width,height,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,width,height),0,0);png.Apply();File.WriteAllBytes(Folder+"/"+name+".png",png.EncodeToPNG());
        eye.targetTexture=oldTarget;eye.aspect=oldAspect;RenderTexture.active=old;Object.DestroyImmediate(png);Object.DestroyImmediate(rt);
    }
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false) || !EditorApplication.isPlaying || Time.realtimeSinceStartup<next)return;
        try
        {
            if(phase==0)
            {
                Logic();game=HeistGameManager.Instance;GameMenu.Instance.Resume();checkpoint=Object.FindAnyObjectByType<GameCheckpoint>();
                Check(game.StartMission(0),"Mission starts");coop=Object.FindObjectsByType<ChickenCoopLockpick>().First(c=>game.IsMissionTarget(c));
                baseline=checkpoint.Capture();game.player.GetComponentInChildren<PlayerLook>().enabled=false;
                var cc=game.player.GetComponent<CharacterController>();cc.enabled=false;game.player.position=coop.InteractionPoint-coop.transform.forward*1.4f-Vector3.up*1.2f;cc.enabled=true;
                Camera.main.transform.LookAt(coop.InteractionPoint);Physics.SyncTransforms();
                NoiseEmitter.NoiseEmitted+=(source,p,m)=>{if(source==NoiseSource.CoopLockpickFail)noiseCount++;};noiseCount=0;
                next=Time.realtimeSinceStartup+.2f;phase++;return;
            }
            if(phase==1)
            {
                viewPosition=Camera.main.transform.localPosition;viewRotation=Camera.main.transform.localRotation;viewFov=Camera.main.fieldOfView;viewNear=Camera.main.nearClipPlane;
                Check(coop.TryBeginChallenge(),"Actual entrance starts pressure interaction");
                Check(!checkpoint.Save(out _),"Saving is blocked while operating the latch");
                RecordBird();
                next=Time.realtimeSinceStartup+.3f;phase++;return;
            }
            if(phase==2)
            {
                if(!viewCaptured){Check(coop.GetComponent<CoopLatchPresentation>().CloseViewActive && coop.GetComponent<CoopLatchPresentation>().PiecesInFrame,"Close view frames all three physical pieces");ScreenCapture.CaptureScreenshot(Folder+"/pressure-ui.png");viewCaptured=true;next=Time.realtimeSinceStartup+.2f;return;}
                int wrong=(coop.Latch.NextPiece+1)%3;
                for(int i=0;i<100 && !coop.Scare.IsActive;i++)coop.Manipulate(.02f,wrong,0,true);
                Check(noiseCount==1 && !coop.IsOpen && coop.Scare.IsActive,"Sustained wrong force makes noise and triggers one actual chicken scare");
                Check(!bird.TrySteal() && game.backpack.chickensCarried==inventory,"Scared bird cannot be picked up");
                Check(!coop.Scare.Begin(bird.transform,Camera.main),"Simultaneous scares are rejected");
                Check(!checkpoint.Save(out _),"Saving is blocked during the scare");
                phase++;return;
            }
            if(phase==3)
            {
                if(coop.Scare.Elapsed<.32f)return;
                Capture("scare-16x9",1600,900);Capture("scare-4x3",1200,900);Capture("scare-wide",1800,750);
                GameMenu.Instance.Pause();birdPositionPaused=bird.transform.position;next=Time.realtimeSinceStartup+.25f;phase++;return;
            }
            if(phase==4)
            {
                Check(Vector3.Distance(bird.transform.position,birdPositionPaused)<.0001f,"Pause freezes the scare");GameMenu.Instance.Resume();next=Time.realtimeSinceStartup+1;phase++;return;
            }
            if(phase==5)
            {
                Restored("Normal completion");
                int wrong=(coop.Latch.NextPiece+1)%3;for(int i=0;i<50;i++)coop.Manipulate(.02f,wrong,0,true);
                Check(noiseCount==2 && !coop.Scare.IsActive,"Cooldown prevents repeated immediate scares");
                for(int i=0;i<3;i++){SolvePiece(coop);Check(coop.IsOpen==(i==2),"Real interaction requires all pieces: "+(i+1));}
                Check(coop.door.GetComponentsInChildren<Collider>().All(c=>!c.enabled),"Released latch opens physical entrance");
                Check(Vector3.Distance(Camera.main.transform.localPosition,viewPosition)<.001f && Quaternion.Angle(Camera.main.transform.localRotation,viewRotation)<.01f && Mathf.Approximately(Camera.main.fieldOfView,viewFov) && Mathf.Approximately(Camera.main.nearClipPlane,viewNear),"Opening restores camera pose, field of view and near plane");nearBefore=Camera.main.nearClipPlane;
                coop.RestoreOpen(false);Check(coop.Scare.Begin(bird.transform,Camera.main),"Scare can start for cancellation test");coop.CancelInteraction();Restored("Cancellation");
                coop.Scare.Begin(bird.transform,Camera.main);coop.enabled=false;Restored("Component disabled");coop.enabled=true;
                coop.Scare.Begin(bird.transform,Camera.main);Check(checkpoint.Restore(baseline,out _),"Checkpoint restore cancels scare before applying saved bird positions");
                Check(ChickenScare.Active==null && !coop.Scare.IsActive,"Loading leaves no active scare");
                Check(Object.FindObjectsByType<InteractableChicken>().Length==birdCount && game.backpack.chickensCarried==inventory,"Loading preserves animal count and inventory");
                EditorApplication.isPlaying=false;return;
            }
        }
        catch(Exception e){Check(false,e.ToString());EditorApplication.isPlaying=false;}
    }
    static Vector3 birdPositionPaused;
    static Vector3 viewPosition;static Quaternion viewRotation;static float viewFov,viewNear;static bool viewCaptured;
}
