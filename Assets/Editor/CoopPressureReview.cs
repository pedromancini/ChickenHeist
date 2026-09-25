using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class CoopPressureReview
{
    public static void Inspect()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        Directory.CreateDirectory("output/coop-pressure-review");
        var bird=Object.FindObjectsByType<InteractableChicken>().First();
        var mesh=bird.GetComponentInChildren<MeshFilter>();
        File.WriteAllLines("output/coop-pressure-review/vertices.csv",mesh.sharedMesh.vertices.Select((v,i)=>{
            var p=bird.transform.InverseTransformPoint(mesh.transform.TransformPoint(v));var c=mesh.sharedMesh.colors[i];
            return FormattableString.Invariant($"{p.x},{p.y},{p.z},{c.r},{c.g},{c.b}");}));
        File.WriteAllText("output/coop-pressure-review/bird.txt",string.Join("\n",bird.GetComponentsInChildren<Transform>().Select(t=>t.name+" "+t.localPosition+" "+t.localScale)));
        var go=new GameObject("Review");var camera=go.AddComponent<Camera>();camera.CopyFrom(Camera.main);
        camera.transform.position=bird.transform.TransformPoint(new Vector3(1.6f,1.2f,2));camera.transform.LookAt(bird.transform.TransformPoint(new Vector3(0,.65f,0)));camera.cullingMask=~0;
        var light=new GameObject("Review light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(45,15,0);
        var rt=new RenderTexture(1200,1000,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
        var png=new Texture2D(1200,1000,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,1200,1000),0,0);png.Apply();File.WriteAllBytes("output/coop-pressure-review/bird.png",png.EncodeToPNG());
        EditorApplication.Exit(0);
    }
}
