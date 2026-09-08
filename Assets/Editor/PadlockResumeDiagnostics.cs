using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class PadlockResumeDiagnostics {
 public static void Run(){
 EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
 var lines=new System.Collections.Generic.List<string>();
 var prefab=Resources.Load<GameObject>("CoopPadlock");
 lines.Add("mesh bounds "+prefab.GetComponent<MeshFilter>().sharedMesh.bounds);
 foreach(var c in Object.FindObjectsByType<ChickenCoopLockpick>()){
 var a=c.lockAnchor; var r=a.GetComponent<Renderer>();
 lines.Add(c.name+" anchor "+a.position+" scale "+a.lossyScale+" bounds "+r.bounds+" enabled "+r.enabled+" static "+a.gameObject.isStatic);
 }
 File.WriteAllLines("output/padlock-diagnostics.txt",lines); EditorApplication.Exit(0);
 }
}
