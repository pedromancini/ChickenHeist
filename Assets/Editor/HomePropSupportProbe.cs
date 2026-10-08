using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Diagnostic: small props in the protagonist's home that have nothing under them (floating cups, plates,
// bottles), plus the porch floor height under the doorbell button and the door handle height for reference.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod HomePropSupportProbe.Run
public static class HomePropSupportProbe
{
    public static void Run()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var home=GameObject.Find("Casa do Protagonista - Sitio do Recomeco").transform;
        Physics.SyncTransforms();
        var log=new List<string>();
        Vector3 Local(Vector3 p)=>home.InverseTransformPoint(p);
        // Props up to 40 cm across: cast down from just under their base and report the gap to the first surface.
        foreach(var r in home.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled && r.gameObject.activeInHierarchy).OrderBy(r=>r.name))
        {
            var b=r.bounds;
            if(b.size.x>.4f || b.size.z>.4f || b.size.y>.5f)continue;
            var from=new Vector3(b.center.x,b.min.y-.002f,b.center.z);
            float gap=float.PositiveInfinity;string below="nothing";
            foreach(var hit in Physics.RaycastAll(from,Vector3.down,3f,~0,QueryTriggerInteraction.Ignore).OrderBy(h=>h.distance))
            {if(hit.collider.transform==r.transform)continue;gap=hit.distance;below=hit.collider.name;break;}
            // Also accept a renderer directly under it (props often have no colliders).
            foreach(var other in home.GetComponentsInChildren<Renderer>(true))
            {
                if(other==r || !other.enabled)continue;var o=other.bounds;
                if(from.x<o.min.x || from.x>o.max.x || from.z<o.min.z || from.z>o.max.z || o.max.y>from.y+.004f)continue;
                float g=from.y-o.max.y;if(g<gap){gap=g;below=other.name+" (renderer)";}
            }
            if(gap>.02f)log.Add("FLOATING "+r.transform.parent.name+"/"+r.name+" base "+Local(b.min).ToString("F3")+" gap "+(float.IsInfinity(gap)?"inf":gap.ToString("F3"))+" above "+below);
        }
        var bell=home.GetComponentInChildren<HomeDoorbell>();
        if(bell!=null && bell.button!=null)
        {
            var p=bell.button.position;
            foreach(var hit in Physics.RaycastAll(p+home.forward*-.6f,Vector3.down,4f).OrderBy(h=>h.distance).Take(1))
                log.Add("doorbell button local "+Local(p).ToString("F3")+" porch floor local y "+Local(hit.point).y.ToString("F3")+" ("+hit.collider.name+") -> height above porch "+(p.y-hit.point.y).ToString("F3"));
        }
        foreach(var r in home.GetComponentsInChildren<Renderer>().Where(r=>r.name.ToLowerInvariant().Contains("macaneta")||r.name.ToLowerInvariant().Contains("puxador")).Take(4))
            log.Add("handle "+r.transform.parent.name+"/"+r.name+" local "+Local(r.bounds.center).ToString("F3"));
        // Furniture tops near the floating props, for re-seating them.
        foreach(var r in home.GetComponentsInChildren<Renderer>().Where(r=>{var c=Local(r.bounds.center);return c.x<-4f && c.x>-7.6f && c.z>2.5f && c.z<6.9f && r.bounds.size.x>.3f && r.bounds.size.z>.3f;}).OrderBy(r=>r.name))
            log.Add("surface "+r.transform.parent.name+"/"+r.name+" min "+Local(r.bounds.min).ToString("F3")+" max "+Local(r.bounds.max).ToString("F3"));
        Directory.CreateDirectory("output/home-review");
        File.WriteAllLines("output/home-review/prop-support.txt",log);
        Debug.Log("HOME PROP SUPPORT PROBE\n"+string.Join("\n",log));
        EditorApplication.Exit(0);
    }
}
