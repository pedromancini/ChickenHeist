using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Diagnostic: the pickup's steering wheel in its own space: rim radius, rim tube thickness, which side faces the
// driver seat, and where SteeringGrip puts the hands.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod SteeringWheelProbe.Run
public static class SteeringWheelProbe
{
    public static void Run()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var truck=Object.FindAnyObjectByType<OldPickupTruck>();var wheel=truck.steeringWheel;var log=new List<string>();
        log.Add("wheel "+wheel.name+" lossyScale "+wheel.lossyScale.ToString("F3")+" children "+string.Join(", ",wheel.GetComponentsInChildren<Transform>().Select(t=>t.name)));
        var pts=new List<Vector3>();
        foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Where(r=>Vector3.Distance(r.bounds.center,wheel.position)<.5f))
            log.Add("renderer near the wheel: "+r.name+" parent "+(r.transform.parent!=null?r.transform.parent.name:"-")+" child of wheel "+r.transform.IsChildOf(wheel)+" bounds size "+r.bounds.size.ToString("F3")+" centre offset "+wheel.InverseTransformPoint(r.bounds.center).ToString("F3"));
        var meshes=wheel.GetComponentsInChildren<MeshFilter>().ToList();
        if(meshes.Count==0)meshes=Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Where(m=>m.GetComponent<Renderer>()!=null && Vector3.Distance(m.GetComponent<Renderer>().bounds.center,wheel.position)<.08f && m.GetComponent<Renderer>().bounds.size.magnitude<.8f).ToList();
        foreach(var mf in meshes)
        {
            var m=mf.sharedMesh;log.Add("mesh "+mf.name+" verts "+m.vertexCount+" local bounds "+m.bounds);
            foreach(var v in m.vertices)pts.Add(wheel.InverseTransformPoint(mf.transform.TransformPoint(v)));
        }
        // radial distance in the wheel's local XY plane (the wheel turns about local Z)
        var radial=pts.Select(p=>new Vector2(p.x,p.y).magnitude).ToList();
        float outer=radial.Max();
        var rim=pts.Where(p=>new Vector2(p.x,p.y).magnitude>outer*.75f).ToList();
        float rimIn=rim.Min(p=>new Vector2(p.x,p.y).magnitude),zMin=rim.Min(p=>p.z),zMax=rim.Max(p=>p.z);
        log.Add($"rim (local units): outer radius {outer:F3}, inner radius {rimIn:F3}, centre radius {(outer+rimIn)/2:F3}, tube radial half-width {(outer-rimIn)/2:F3}, z {zMin:F3}..{zMax:F3} (half-depth {(zMax-zMin)/2:F3}), centre z {(zMin+zMax)/2:F3}");
        float s=wheel.lossyScale.x;
        log.Add($"rim (metres): centre radius {(outer+rimIn)/2*s:F3}, tube half-width {(outer-rimIn)/2*s:F3}, tube half-depth {(zMax-zMin)/2*wheel.lossyScale.z:F3}");
        log.Add("SteeringGrip(-1) local "+wheel.InverseTransformPoint(truck.SteeringGrip(-1)).ToString("F3")+" (+1) local "+wheel.InverseTransformPoint(truck.SteeringGrip(1)).ToString("F3"));
        var seat=truck.seat;var toSeat=wheel.InverseTransformDirection(seat.position-wheel.position).normalized;
        log.Add("seat direction in wheel space "+toSeat.ToString("F2")+" (wheel forward points "+(toSeat.z<0?"away from":"towards")+" the driver)");
        log.Add("wheel forward world "+wheel.forward.ToString("F2")+" up "+wheel.up.ToString("F2")+" right "+wheel.right.ToString("F2")+"; truck forward "+truck.transform.forward.ToString("F2"));
        Directory.CreateDirectory("output/wheel-grip");File.WriteAllLines("output/wheel-grip/wheel.txt",log);
        Debug.Log("STEERING WHEEL PROBE\n"+string.Join("\n",log));
    }
}
