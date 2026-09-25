using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class GripHandsUpgrade
{
 public static void RunBatch()
 {
  EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
  const string source="Assets/ChickenHeistGenerated/Hands/GripHand.fbx";
  AssetDatabase.ImportAsset(source,ImportAssetOptions.ForceSynchronousImport);
  var importer=(ModelImporter)AssetImporter.GetAtPath(source);importer.isReadable=true;importer.importBlendShapes=true;importer.importAnimation=false;importer.SaveAndReimport();
  var obj=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(source));
  var ts=obj.GetComponentsInChildren<Transform>();
  Vector3 origin=ts.Single(t=>t.name=="Wrist").position;
  Vector3 x=(ts.Single(t=>t.name=="Across").position-origin).normalized,y=(ts.Single(t=>t.name=="Fingers").position-origin).normalized,z=(ts.Single(t=>t.name=="Palm").position-origin).normalized;
  Vector3 Convert(Vector3 p)=>new Vector3(Vector3.Dot(p,x),Vector3.Dot(p,y),Vector3.Dot(p,z));
  var renderer=obj.GetComponentInChildren<SkinnedMeshRenderer>();var sourceMesh=renderer.sharedMesh;
  var mesh=new Mesh{name="GripHands canonical"};
  mesh.vertices=sourceMesh.vertices.Select(p=>Convert(renderer.transform.TransformPoint(p)-origin)).ToArray();mesh.triangles=sourceMesh.triangles;
  // Correct winding after FBX coordinate conversion, using imported normals.
  var tri=mesh.triangles;var v=mesh.vertices;var normal=Convert(renderer.transform.TransformDirection(sourceMesh.normals[tri[0]]));
  if(Vector3.Dot(Vector3.Cross(v[tri[1]]-v[tri[0]],v[tri[2]]-v[tri[0]]),normal)<0){for(int i=0;i<tri.Length;i+=3)(tri[i+1],tri[i+2])=(tri[i+2],tri[i+1]);mesh.triangles=tri;}
  mesh.RecalculateNormals();
  for(int i=0;i<sourceMesh.blendShapeCount;i++)
  {
   var delta=new Vector3[v.Length];sourceMesh.GetBlendShapeFrameVertices(i,0,delta,null,null);
   for(int j=0;j<delta.Length;j++)delta[j]=Convert(renderer.transform.TransformVector(delta[j]));
   mesh.AddBlendShapeFrame(sourceMesh.GetBlendShapeName(i).Split('.').Last(),100,delta,new Vector3[v.Length],new Vector3[v.Length]);
  }
  mesh.RecalculateBounds();
  const string path="Assets/Resources/GripHands.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
  if(existing==null)AssetDatabase.CreateAsset(mesh,path);else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);}
  Object.DestroyImmediate(obj);AssetDatabase.SaveAssets();
  SessionState.SetBool("Protagonist.Batch",true);PlayableVillageTests.Run();
 }
}
