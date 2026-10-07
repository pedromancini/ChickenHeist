using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Diagnostic: geometry around the protagonist's front door (door leaf, hinge, frame, wall faces, porch items),
// the door's swept volume and every renderer that intersects another one it should not touch.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod HomeGeometryProbe.Run
public static class HomeGeometryProbe
{
    public static void Run()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var home=GameObject.Find("Casa do Protagonista - Sitio do Recomeco").transform;
        var log=new List<string>();
        string L(Bounds b)=>"min "+home.InverseTransformPoint(b.min).ToString("F2")+" max "+home.InverseTransformPoint(b.max).ToString("F2");
        var door=home.GetComponentInChildren<HomeDoor>();
        log.Add("HomeDoor on "+door.name+" hinge "+door.hinge.name+" hingeLocal "+home.InverseTransformPoint(door.hinge.position).ToString("F2")+" hingeRot "+door.hinge.localEulerAngles);
        foreach(var r in door.hinge.GetComponentsInChildren<Renderer>())log.Add("  leaf part "+r.name+" "+L(r.bounds));
        // swept volume of the door leaf from 0 to 100 degrees
        var rot=door.hinge.localRotation;var swept=new Bounds();bool first=true;
        for(int a=0;a<=100;a+=10)
        {
            door.hinge.localRotation=Quaternion.Euler(0,a,0);
            foreach(var r in door.hinge.GetComponentsInChildren<Renderer>()){if(first){swept=r.bounds;first=false;}else swept.Encapsulate(r.bounds);}
        }
        door.hinge.localRotation=rot;
        log.Add("door swept "+L(swept));
        var region=new Bounds(home.TransformPoint(new Vector3(-3.2f,1.6f,1.6f)),new Vector3(5,3.5f,2.6f));
        foreach(var r in home.GetComponentsInChildren<Renderer>().Where(r=>region.Intersects(r.bounds) && !r.transform.IsChildOf(door.hinge)).OrderBy(r=>r.name))
            log.Add("near door: "+r.transform.parent.name+"/"+r.name+" "+L(r.bounds)+(swept.Intersects(r.bounds)?"  <-- inside door sweep":""));
        // exterior wall surface right of the door at bell height, by raycast from the porch
        foreach(float x in new[]{-4.6f,-4.2f,-2.3f,-2.0f,-1.7f})
            foreach(float y in new[]{1.4f,2.6f})
            {
                var from=home.TransformPoint(new Vector3(x,y,.4f));
                var hits=Physics.RaycastAll(from,home.forward,3).OrderBy(h=>h.distance).Select(h=>h.collider.name+"@z"+home.InverseTransformPoint(h.point).z.ToString("F3")).Take(3);
                log.Add("ray x="+x+" y="+y+": "+string.Join(", ",hits));
            }
        // ---- overlap scan ----
        float Vol(Bounds b)=>Mathf.Max(1e-6f,b.size.x*b.size.y*b.size.z);
        float Share(Bounds a,Bounds b)
        {
            var mn=Vector3.Max(a.min,b.min);var mx=Vector3.Min(a.max,b.max);var d=mx-mn;
            if(d.x<=0 || d.y<=0 || d.z<=0)return 0;return d.x*d.y*d.z/Mathf.Min(Vol(a),Vol(b));
        }
        string[] flat={"Umidade","Escorrido","Mancha","Rachadura","Teia","Fresta","Terra","Reboco caido","Centro escuro","Buraco","Caco de reboco no chao"};
        string[] structural={"Parede interna","Rodape","Assoalho","Base do assoalho","Forro baixo","Viga exposta","Divisoria","Remendo no reboco","Moldura","Peitoril","Veneziana","Verga"};
        var interior=home.Find("Interior - Uma vida por reconstruir");var worn=interior.Find("Desgaste - interior");
        var mine=worn.GetComponentsInChildren<Renderer>().Where(r=>!flat.Any(f=>r.name.StartsWith(f))).ToList();
        var others=interior.GetComponentsInChildren<Renderer>().Where(r=>!r.transform.IsChildOf(worn) && !structural.Any(f=>r.name.StartsWith(f))).ToList();
        int clashes=0;
        foreach(var m in mine)foreach(var o in others)
        {float sh=Share(m.bounds,o.bounds);if(sh>.15f){clashes++;log.Add("CLASH worn "+m.name+" x "+o.transform.parent.name+"/"+o.name+" share "+sh.ToString("F2")+" at "+L(m.bounds));}}
        var porch=new List<Renderer>();var bellRoot=home.Find("Desgaste - campainha velha");if(bellRoot!=null)porch.AddRange(bellRoot.GetComponentsInChildren<Renderer>());
        var lantern=home.Find("Lanterna da varanda");if(lantern!=null)porch.AddRange(lantern.GetComponentsInChildren<Renderer>());
        foreach(var r in porch)
        {
            // door leaf at every 10 degrees
            for(int a=0;a<=100;a+=10)
            {
                door.hinge.localRotation=Quaternion.Euler(0,a,0);
                foreach(var leaf in door.hinge.GetComponentsInChildren<Renderer>())if(leaf.bounds.Intersects(r.bounds)){clashes++;log.Add("CLASH door leaf at "+a+" deg x "+r.transform.parent.name+"/"+r.name);a=200;break;}
            }
            door.hinge.localRotation=rot;
        }
        var coop=home.Find("Galinheiro gasto - Ultimo Recurso");var gate=coop!=null?coop.Find("Portinhola do galinheiro"):null;
        if(gate!=null)
        {
            var parts=coop.GetComponentsInChildren<Renderer>().Where(r=>!r.transform.IsChildOf(gate) && !r.name.StartsWith("Chao") && !r.name.StartsWith("Galinha")).ToList();
            var gateRot=gate.localRotation;var hitParts=new HashSet<string>();
            for(int a=0;a>=-90;a-=8)
            {
                gate.localRotation=Quaternion.Euler(0,a,0);
                foreach(var g in gate.GetComponentsInChildren<Renderer>())foreach(var p in parts)
                    if(Share(g.bounds,p.bounds)>.05f && hitParts.Add(p.name+" @"+a))log.Add("CLASH coop gate at "+a+" deg x "+p.name+" "+L(p.bounds));
            }
            gate.localRotation=gateRot;
            // exact check with the colliders (oriented boxes), which the bounding boxes above only approximate
            var gateCols=gate.GetComponentsInChildren<Collider>();var partCols=coop.GetComponentsInChildren<Collider>().Where(c=>!c.transform.IsChildOf(gate) && !c.name.StartsWith("Chao")).ToArray();
            int exact=0;
            for(int a=0;a>=-88;a-=8)
            {
                gate.localRotation=Quaternion.Euler(0,a,0);Physics.SyncTransforms();
                foreach(var g in gateCols)foreach(var p in partCols)
                    if(Physics.ComputePenetration(g,g.transform.position,g.transform.rotation,p,p.transform.position,p.transform.rotation,out var dir,out float depth) && depth>.004f)
                    {exact++;log.Add("EXACT coop gate at "+a+" deg: "+g.name+" into "+p.name+" by "+(depth*100).ToString("F1")+" cm");}
            }
            gate.localRotation=gateRot;Physics.SyncTransforms();
            log.Add("coop gate exact penetrations "+exact+" (bounding-box flags "+hitParts.Count+")");clashes+=exact;
        }
        log.Add("TOTAL CLASHES "+clashes);
        Directory.CreateDirectory("output/home-review");File.WriteAllLines("output/home-review/door-probe.txt",log);
        Debug.Log("HOME GEOMETRY PROBE\n"+string.Join("\n",log));
    }
}
