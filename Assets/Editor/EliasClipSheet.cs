using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Diagnostic: samples every clip of the protagonist body (Protagonist.prefab) at four moments and renders
// side and three-quarter views, so each animation can be checked on the current Elias model.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod EliasClipSheet.Run
public static class EliasClipSheet
{
    const string Folder="output/elias-v3/clips";
    public static void Run()
    {
        Directory.CreateDirectory(Folder);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ChickenHeistGenerated/Characters/ProtagonistV2/Protagonist.prefab");
        var root=(GameObject)Object.Instantiate(prefab);
        foreach(var b in root.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
        var body=root.GetComponentsInChildren<Animation>(true).First(a=>a.transform.Find("Protagonist_Rigged(Clone)")!=null);
        foreach(var r in body.GetComponentsInChildren<Renderer>(true)){r.gameObject.layer=0;r.enabled=!r.name.Contains("em primeira pessoa");if(r is SkinnedMeshRenderer sk){sk.forceMatrixRecalculationPerRender=true;sk.updateWhenOffscreen=true;}}
        var lightGo=new GameObject("light");var light=lightGo.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.3f;lightGo.transform.rotation=Quaternion.Euler(45,-35,0);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.45f,.45f,.5f);
        var camGo=new GameObject("cam");var cam=camGo.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.2f,.22f,.25f);
        cam.orthographic=true;cam.orthographicSize=1.05f;cam.nearClipPlane=.05f;cam.farClipPlane=20;
        var tex=new RenderTexture(300,400,24);cam.targetTexture=tex;
        var names=body.Cast<AnimationState>().Select(s=>s.name).OrderBy(n=>n).ToList();
        {
            var thigh=body.GetComponentsInChildren<Transform>(true).First(t=>t.name=="ThighL");
            var skin=body.GetComponentsInChildren<SkinnedMeshRenderer>(true).First(s=>s.name=="ProtagonistBody");
            var walk=body.GetClip("Walk");
            walk.SampleAnimation(body.gameObject,0);var a0=thigh.localRotation;
            walk.SampleAnimation(body.gameObject,.4f);var a1=thigh.localRotation;
            Debug.Log("PROBE thigh angle between samples "+Quaternion.Angle(a0,a1)+" | skin bone is hierarchy bone: "+skin.bones.Contains(thigh)+
                " | skin root "+(skin.rootBone!=null?skin.rootBone.name:"none")+" | skin enabled "+skin.enabled+" | body path "+AnimationUtility.CalculateTransformPath(thigh,body.transform)+
                " | skins: "+string.Join(", ",body.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(x=>x.name+"("+x.bones.Length+","+(x.bones.Length>0 && x.bones[0]!=null?x.bones[0].root.name:"null")+")")));
        }
        var log=new List<string>();
        foreach(var name in names)
        {
            var clip=body.GetClip(name);
            var tiles=new List<Texture2D>();
            foreach(float f in new[]{0f,.25f,.5f,.75f})
            {
                clip.SampleAnimation(body.gameObject,clip.length*f);
                var hips=body.GetComponentsInChildren<Transform>().First(t=>t.name=="Hips");
                Vector3 c=new Vector3(hips.position.x,body.transform.position.y+.95f,hips.position.z);
                foreach(var view in new[]{body.transform.right,(body.transform.forward+body.transform.right*.8f).normalized})
                {
                    cam.transform.position=c+view*6;cam.transform.LookAt(c);cam.Render();
                    RenderTexture.active=tex;var t2=new Texture2D(300,400,TextureFormat.RGB24,false);t2.ReadPixels(new Rect(0,0,300,400),0,0);t2.Apply();RenderTexture.active=null;tiles.Add(t2);
                }
            }
            var sheet=new Texture2D(300*tiles.Count,400,TextureFormat.RGB24,false);
            for(int i=0;i<tiles.Count;i++){sheet.SetPixels(i*300,0,300,400,tiles[i].GetPixels());Object.DestroyImmediate(tiles[i]);}
            sheet.Apply();File.WriteAllBytes(Folder+"/"+name+".png",sheet.EncodeToPNG());Object.DestroyImmediate(sheet);
            log.Add(name+" length "+clip.length.ToString("0.00")+" wrap "+clip.wrapMode);
        }
        File.WriteAllLines(Folder+"/clips.txt",log);
        Debug.Log("ELIAS CLIP SHEET "+names.Count);
    }
}
