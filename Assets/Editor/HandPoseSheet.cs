using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Diagnostic: both hands of the protagonist prefab in several clips (first frame), lit from both sides so dark
// areas are geometry rather than shadow, with each thumb bone's direction relative to the hand logged.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod HandPoseSheet.Run
public static class HandPoseSheet
{
    const string Folder="output/hand-poses";
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=(GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChickenHeistGenerated/Characters/ProtagonistV2/Protagonist.prefab"));
        foreach(var b in root.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
        var body=root.GetComponentsInChildren<Animation>(true).First(a=>a.transform.Find("Protagonist_Rigged(Clone)")!=null);
        foreach(var r in body.GetComponentsInChildren<Renderer>(true)){r.gameObject.layer=0;r.enabled=!r.name.Contains("em primeira pessoa");if(r is SkinnedMeshRenderer sk){sk.forceMatrixRecalculationPerRender=true;sk.updateWhenOffscreen=true;}}
        foreach(var rot in new[]{Quaternion.Euler(40,-30,0),Quaternion.Euler(-30,150,0)})
        {var l=new GameObject("light").AddComponent<Light>();l.type=LightType.Directional;l.intensity=1.1f;l.transform.rotation=rot;}
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.55f,.58f);
        var cam=new GameObject("cam").AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.2f,.22f,.25f);
        cam.fieldOfView=30;cam.nearClipPlane=.01f;cam.farClipPlane=10;
        var rt=new RenderTexture(400,400,24);cam.targetTexture=rt;
        Transform Bone(string n)=>body.GetComponentsInChildren<Transform>(true).First(t=>t.name==n);
        var log=new List<string>();
        var clips=new[]{"Idle","Drive","Carry","Pickup","Lockpick","CrouchIdle","Walk"};
        var views=new[]{("top",new Vector3(0,1,-.15f)),("front",new Vector3(0,.2f,1)),("outer",new Vector3(1,.3f,0)),("below",new Vector3(0,-1,.2f))};
        var sheet=new Texture2D(400*views.Length*2,400*clips.Length,TextureFormat.RGB24,false);
        for(int c=0;c<clips.Length;c++)
        {
            var clip=body.GetClip(clips[c]);if(clip==null){log.Add(clips[c]+": missing");continue;}
            clip.SampleAnimation(body.gameObject,clip.length*.3f);
            for(int s=0;s<2;s++)
            {
                string side=s==0?"L":"R";var hand=Bone("Hand"+side);var mid=Bone("Middle1"+side);
                var t1=Bone("Thumb1"+side);var t2=Bone("Thumb2"+side);var t3=Bone("Thumb3"+side);var idx=Bone("Index1"+side);
                Vector3 handDir=(mid.position-hand.position).normalized;
                log.Add($"{clips[c]} {side}: thumb1->2 vs hand {Vector3.Angle(t2.position-t1.position,handDir):F0} deg, thumb2->3 vs thumb1->2 {Vector3.Angle(t3.position-t2.position,t2.position-t1.position):F0} deg, thumb tip to index base {Vector3.Distance(t3.position,idx.position):F3} m");
                var centre=(hand.position+mid.position)*.5f;
                for(int v=0;v<views.Length;v++)
                {
                    var dir=root.transform.TransformDirection(new Vector3(views[v].Item2.x*(s==0?-1:1),views[v].Item2.y,views[v].Item2.z)).normalized;
                    cam.transform.position=centre+dir*.42f;cam.transform.LookAt(centre,Vector3.up);
                    cam.Render();RenderTexture.active=rt;sheet.ReadPixels(new Rect(0,0,400,400),(s*views.Length+v)*400,(clips.Length-1-c)*400);RenderTexture.active=null;
                }
            }
        }
        sheet.Apply();File.WriteAllBytes(Folder+"/hands.png",sheet.EncodeToPNG());
        File.WriteAllLines(Folder+"/hands.txt",log);cam.targetTexture=null;rt.Release();
        Debug.Log("HAND POSE SHEET\n"+string.Join("\n",log));
    }
}
