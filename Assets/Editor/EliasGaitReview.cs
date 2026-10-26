using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class EliasGaitReview
{
    const string Folder="output/elias-gait";
    static readonly string[] Sheets={"Walk","WalkBackward"};
    static readonly string[] Measured={"Walk","WalkBackward","WalkLeft","WalkRight","WalkForwardLeft","WalkForwardRight","WalkBackwardLeft","WalkBackwardRight",
        "Run","RunBackward","RunLeft","RunRight","RunForwardLeft","RunForwardRight","RunBackwardLeft","RunBackwardRight"};
    const float MaxRoll=12,MaxYaw=20,MaxTwist=15,MaxApart=25,MaxDrift=3;const int MaxImbalance=5;
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var log=new List<string>();var checks=new List<string>();
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChickenHeistGenerated/Characters/ProtagonistV2/Protagonist.prefab");
        var root=(GameObject)Object.Instantiate(prefab);
        try
        {
            foreach(var b in root.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
            var body=root.GetComponentsInChildren<Animation>(true).First(a=>a.transform.Find("Protagonist_Rigged(Clone)")!=null);
            foreach(var r in body.GetComponentsInChildren<Renderer>(true)){r.gameObject.layer=0;r.enabled=!r.name.Contains("em primeira pessoa");if(r is SkinnedMeshRenderer sk){sk.forceMatrixRecalculationPerRender=true;sk.updateWhenOffscreen=true;}}
            var lightGo=new GameObject("light");var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;lightGo.transform.rotation=Quaternion.Euler(40,-30,0);
            RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.5f,.55f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.localScale=new Vector3(3,.004f,3);
            var floorMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));floorMat.color=new Color(.62f,.58f,.50f);floor.GetComponent<Renderer>().sharedMaterial=floorMat;
            var camGo=new GameObject("cam");var cam=camGo.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.2f,.22f,.25f);
            cam.orthographic=true;cam.orthographicSize=.5f;cam.nearClipPlane=.05f;cam.farClipPlane=20;
            var tex=new RenderTexture(240,300,24);cam.targetTexture=tex;
            Transform Bone(string n)=>body.GetComponentsInChildren<Transform>(true).First(t=>t.name==n);
            body.GetClip("Idle").SampleAnimation(body.gameObject,0);
            float floorY=Mathf.Min(Bone("ToeL").position.y,Bone("ToeR").position.y);
            var restAlong=body.transform.InverseTransformPoint(Bone("ToeL").position)-body.transform.InverseTransformPoint(Bone("FootL").position);
            flatPitch=Mathf.Atan2(-restAlong.y,new Vector2(restAlong.x,restAlong.z).magnitude)*Mathf.Rad2Deg;
            floor.transform.position=new Vector3(0,body.transform.position.y-.002f,0);
            cam.Render();
            foreach(var name in Measured)
            {
                var clip=body.GetClip(name);if(clip==null){log.Add(name+": missing");continue;}
                var result=Gait(name,clip,body.gameObject,body.transform,Bone,floorY);log.AddRange(result.lines);
                bool sheet=Sheets.Contains(name);
                if(sheet || !result.ok)checks.Add((result.ok?"PASS ":"FAIL ")+result.summary);
                if(!sheet)continue;
                var tiles=new List<Texture2D>();
                for(int row=0;row<2;row++)for(int f=0;f<8;f++)
                {
                    clip.SampleAnimation(body.gameObject,clip.length*f/8);
                    var hips=Bone("Hips");Vector3 c=new Vector3(hips.position.x,body.transform.position.y+.42f,hips.position.z);
                    var view=row==0?body.transform.right:body.transform.forward;
                    cam.transform.position=c+view*6;cam.transform.LookAt(c);cam.Render();
                    RenderTexture.active=tex;var t2=new Texture2D(240,300,TextureFormat.RGB24,false);t2.ReadPixels(new Rect(0,0,240,300),0,0);t2.Apply();RenderTexture.active=null;tiles.Add(t2);
                }
                var image=new Texture2D(240*8,600,TextureFormat.RGB24,false);
                for(int i=0;i<tiles.Count;i++){image.SetPixels((i%8)*240,(1-i/8)*300,240,300,tiles[i].GetPixels());Object.DestroyImmediate(tiles[i]);}
                image.Apply();File.WriteAllBytes(Folder+"/"+name+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
            }
            int bad=Measured.Count(n=>body.GetClip(n)!=null && !Gait(n,body.GetClip(n),body.gameObject,body.transform,Bone,floorY).ok);
            checks.Add((bad==0?"PASS ":"FAIL ")+$"Every directional walk and run keeps both feet flat, steady and alike ({Measured.Length-bad}/{Measured.Length} clips)");
            checks.AddRange(CutsceneWalk("Elias",root.transform));
        }
        finally{Object.DestroyImmediate(root);}
        var stage=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Cinematics/VisitorStage.prefab"));
        try{foreach(var b in stage.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;checks.AddRange(CutsceneWalk("Visitor",stage.GetComponent<VisitorCinematicStage>().visitor));}
        finally{Object.DestroyImmediate(stage);}
        File.WriteAllLines(Folder+"/gait.txt",log);File.WriteAllLines(Folder+"/checks.txt",checks);
        Debug.Log("ELIAS GAIT\n"+string.Join("\n",checks));
    }
    static IEnumerable<string> CutsceneWalk(string who,Transform actorRoot)
    {
        var animation=actorRoot.GetComponentInChildren<Animation>(true);
        Transform Bone(string n)=>actorRoot.GetComponentsInChildren<Transform>(true).First(t=>t.name==n);
        actorRoot.position=Vector3.zero;actorRoot.rotation=Quaternion.identity;
        var stand=animation.GetClip("Breathe")??animation.GetClip("Idle");stand.SampleAnimation(animation.gameObject,0);
        var toes=new[]{Bone("ToeL"),Bone("ToeR")};var ankles=new[]{Bone("FootL"),Bone("FootR")};
        float floorRel=Mathf.Min(toes[0].position.y,toes[1].position.y)-actorRoot.position.y,ankleFlat=ankles[0].position.y-Mathf.Min(toes[0].position.y,toes[1].position.y);
        var actor=new VisitorCinematicActor(actorRoot);var results=new List<string>();
        foreach(bool backward in new[]{false,true})
        {
            float slide=0,planted=0,low=float.PositiveInfinity,high=float.NegativeInfinity;Vector3 last=Vector3.zero;int lastFoot=-1;
            for(int k=0;k<=300;k++)
            {
                float d=k*.01f;var position=new Vector3(0,0,backward?-d:d);
                actor.Pose(position,0,0,true,false,position+new Vector3(0,1.6f,4),0,d,1,backward);
                float floorAt=actorRoot.position.y+floorRel;
                var lowest=new float[2];var level=new bool[2];
                for(int f=0;f<2;f++){float toe=toes[f].position.y-floorAt,heel=ankles[f].position.y-floorAt-ankleFlat;lowest[f]=Mathf.Min(toe,heel);level[f]=Mathf.Abs(toe-heel)<.015f;}
                int foot=lowest[0]<lowest[1]?0:1;float h=lowest[foot];bool down=h<.02f && level[foot];
                if(k>=20){low=Mathf.Min(low,h);high=Mathf.Max(high,h);}
                if(down && foot==lastFoot){var step=toes[foot].position-last;step.y=0;slide+=step.magnitude;planted+=.01f;}
                lastFoot=down?foot:-1;last=toes[foot].position;
            }
            float perMetre=planted>0?slide/planted*100:999;
            bool ok=perMetre<=12 && low>=-.025f && high<=.025f;
            results.Add((ok?"PASS ":"FAIL ")+$"{who} walking {(backward?"backwards":"forwards")} in the cutscene: the foot on the floor slides {perMetre:0} cm per metre walked, the lower foot stays {low*100:+0.0;-0.0}..{high*100:+0.0;-0.0} cm from the floor");
        }
        return results;
    }
    static float flatPitch=25;
    static float stride;
    static (List<string> lines,bool ok,string summary) Gait(string name,AnimationClip clip,GameObject sample,Transform frame,System.Func<string,Transform> bone,float floorY)
    {
        var lines=new List<string>{$"{name} ({clip.length:0.00} s)","  t     | L ankle  toe  yaw  roll | R ankle  toe  yaw  roll | planted"};
        const int n=24;var toes=new Vector3[2,n+1];var heights=new float[2,n+1];var yaws=new float[2,n+1];var rolls=new float[2,n+1];var flat=new bool[2,n+1];
        for(int i=0;i<=n;i++)
        {
            clip.SampleAnimation(sample,clip.length*i/n);
            string row=$"  {clip.length*i/n:0.00}  ";
            for(int s=0;s<2;s++)
            {
                string side=s==0?"L":"R";
                var foot=bone("Foot"+side);var toe=bone("Toe"+side);
                Vector3 ankle=frame.InverseTransformPoint(foot.position),tip=frame.InverseTransformPoint(toe.position);
                var along=tip-ankle;along.y=0;
                float yaw=Mathf.Atan2(along.x,along.z)*Mathf.Rad2Deg;
                var footDir=(tip-ankle).normalized;var width=Vector3.ProjectOnPlane(frame.InverseTransformDirection(foot.rotation*Vector3.right),footDir).normalized;
                float roll=Mathf.Asin(Mathf.Clamp(width.y,-1,1))*Mathf.Rad2Deg;
                float ah=(ankle.y-floorY)*100,th=(tip.y-floorY)*100;
                row+=$"| {ah,6:0.0} {th,5:0.0} {yaw,5:0} {roll,5:0} ";
                toes[s,i]=tip;heights[s,i]=Mathf.Min(ah,th);yaws[s,i]=yaw;rolls[s,i]=roll;
                float pitch=Mathf.Atan2(-(tip.y-ankle.y),along.magnitude)*Mathf.Rad2Deg;flat[s,i]=Mathf.Abs(pitch-flatPitch)<12;
            }
            lines.Add(row+"| "+(heights[0,i]<heights[1,i]?"L":"R"));
        }
        bool ok=true;var planted=new int[2];var worstRoll=new float[2];var twist=new float[2];var heading=new float[2];var drift=new float[2];
        for(int s=0;s<2;s++)
        {
            int o=1-s;var down=new List<float>();
            for(int i=0;i<n;i++)
            {
                if(!(heights[s,i]<heights[o,i]))continue;
                planted[s]++;worstRoll[s]=Mathf.Max(worstRoll[s],Mathf.Abs(rolls[s,i]));
                if(flat[s,i])down.Add(yaws[s,i]);
            }
            heading[s]=down.Count>0?Mathf.Atan2(down.Sum(y=>Mathf.Sin(y*Mathf.Deg2Rad)),down.Sum(y=>Mathf.Cos(y*Mathf.Deg2Rad)))*Mathf.Rad2Deg:0;
            twist[s]=down.Count>0?down.Max(y=>Mathf.Abs(Mathf.DeltaAngle(heading[s],y))):0;
            for(int i=0;i<n;)
            {
                if(!(heights[s,i]<heights[o,i])){i++;continue;}
                int j=i;float path=0;while(j<n && heights[s,j+1]<heights[o,j+1]){var d=toes[s,j+1]-toes[s,j];d.y=0;path+=d.magnitude;j++;}
                var net=toes[s,j]-toes[s,i];net.y=0;drift[s]+=path-net.magnitude;i=j+1;
            }
            ok&=worstRoll[s]<=MaxRoll && twist[s]<=MaxTwist && drift[s]*100<=MaxDrift;
            lines.Add($"  {(s==0?"left ":"right")}: planted {planted[s]}/{n}, roll up to {worstRoll[s]:0} deg, stands pointing {heading[s]:0} deg and turns {twist[s]:0} deg, sliding off a straight line {drift[s]*100:0.0} cm");
        }
        stride=VisitorCinematicActor.Stride(clip,sample,frame,bone("ToeL"),bone("ToeR")).stride;
        lines.Add($"  covers {stride:0.00} m per cycle ({stride/clip.length:0.00} m/s at normal speed)");
        bool straight=!name.EndsWith("Left") && !name.EndsWith("Right");
        float apart=Mathf.Abs(Mathf.DeltaAngle(heading[0],heading[1]));
        ok&=Mathf.Abs(planted[0]-planted[1])<=MaxImbalance && apart<=MaxApart && (!straight || Mathf.Max(Mathf.Abs(heading[0]),Mathf.Abs(heading[1]))<=MaxYaw);
        string summary=$"{name}: each foot carries the body (left {planted[0]}/{n}, right {planted[1]}/{n}), flat (roll {worstRoll[0]:0}/{worstRoll[1]:0} deg), "+
            $"pointing {heading[0]:0}/{heading[1]:0} deg{(straight?" (ahead)":"")} without turning while down ({twist[0]:0}/{twist[1]:0} deg), no skating ({drift[0]*100:0.0}/{drift[1]*100:0.0} cm)";
        return (lines,ok,summary);
    }
}
