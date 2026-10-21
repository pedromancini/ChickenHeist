using System.Collections.Generic;
using System.IO;
using UnityEngine;

// Review captures that have to show the frame as drawn: rendered from LateUpdate after every pose (clip sampling, the
// arms' IK in ProtagonistArticulation, the fingers) has been applied. A capture taken from the editor's update loop
// lands between frames, after the poses were reset for the next one.
[DefaultExecutionOrder(10000)]
public sealed class FrameCapture : MonoBehaviour
{
    struct Request{public string path;public Vector3 from,at;public float fov,when;public Camera camera;}
    static FrameCapture instance;
    readonly List<Request> requests=new List<Request>();
    public static int Pending=>instance!=null?instance.requests.Count:0;
    static FrameCapture Instance{get{if(instance==null){var go=new GameObject("Capturas de revisao");instance=go.AddComponent<FrameCapture>();}return instance;}}
    // a camera at `from` looking at `at`, `after` seconds from now (0: this frame)
    public static void Shot(string path,Vector3 from,Vector3 at,float fov=50,float after=0)=>Instance.requests.Add(new Request{path=path,from=from,at=at,fov=fov,when=Time.time+after});
    // what a scene camera sees (the player's eyes)
    public static void Through(string path,Camera camera)=>Instance.requests.Add(new Request{path=path,camera=camera,when=Time.time});
    void LateUpdate()
    {
        for(int i=0;i<requests.Count;)
        {
            var r=requests[i];if(r.when>Time.time){i++;continue;}requests.RemoveAt(i);
            Camera cam=r.camera;GameObject temp=null;
            if(cam==null){temp=new GameObject("captura");cam=temp.AddComponent<Camera>();cam.fieldOfView=r.fov;cam.nearClipPlane=.03f;temp.transform.position=r.from;temp.transform.LookAt(r.at);}
            var rt=new RenderTexture(960,540,24);var old=cam.targetTexture;Rect rect=cam.rect;cam.targetTexture=rt;cam.rect=new Rect(0,0,1,1);cam.Render();
            var active=RenderTexture.active;RenderTexture.active=rt;var t=new Texture2D(960,540,TextureFormat.RGB24,false);t.ReadPixels(new Rect(0,0,960,540),0,0);t.Apply();RenderTexture.active=active;
            cam.targetTexture=old;cam.rect=rect;Directory.CreateDirectory(Path.GetDirectoryName(r.path));File.WriteAllBytes(r.path,t.EncodeToPNG());
            Destroy(rt);Destroy(t);if(temp!=null)Destroy(temp);
        }
    }
}
