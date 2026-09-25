using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

// Lists every world material with its colour, texture and how many renderers use it.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod WorldPaletteAudit.Run
public static class WorldPaletteAudit
{
    public static void Run()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var usage=new Dictionary<Material,(int count,string example)>();
        foreach(var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if(r.GetComponentInParent<RuralCharacterAnimator>()!=null)continue;
            foreach(var m in r.sharedMaterials){if(m==null)continue;usage[m]=usage.TryGetValue(m,out var u)?(u.count+1,u.example):(1,Path(r.transform));}
        }
        var lines=new List<string>();
        foreach(var kv in usage.OrderByDescending(k=>k.Value.count))
        {
            var m=kv.Key;var c=m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):m.HasProperty("_Color")?m.color:Color.white;
            Color.RGBToHSV(c,out float h,out float s,out float v);
            var tex=m.HasProperty("_BaseMap")?m.GetTexture("_BaseMap"):m.mainTexture;
            lines.Add($"{kv.Value.count,6} | {m.name} | {AssetDatabase.GetAssetPath(m)} | rgb({c.r:0.00},{c.g:0.00},{c.b:0.00}) s={s:0.00} v={v:0.00} | tex={(tex!=null?tex.name+" "+tex.width+"x"+tex.height:"-")} | e.g. {kv.Value.example}");
        }
        Directory.CreateDirectory("output/map-review");File.WriteAllLines("output/map-review/materials.txt",lines);
    }
    static string Path(Transform t){var n=new List<string>();for(var x=t;x!=null && n.Count<3;x=x.parent)n.Insert(0,x.name);return string.Join("/",n);}
}
