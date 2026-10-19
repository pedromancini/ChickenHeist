using System.IO;
using System.Linq;
using UnityEditor;

// Which project assets the game actually uses: everything the build scenes and the Resources folders depend on.
// Writes output/usage-audit/unused.txt (unused assets, largest first, with folder totals) for clean-ups.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod UsageAudit.Run
public static class UsageAudit
{
    public static void Run()
    {
        var roots=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToList();
        roots.AddRange(AssetDatabase.GetAllAssetPaths().Where(p=>p.StartsWith("Assets/") && p.Contains("/Resources/") && !AssetDatabase.IsValidFolder(p)));
        var used=new System.Collections.Generic.HashSet<string>(AssetDatabase.GetDependencies(roots.ToArray(),true));
        var unused=AssetDatabase.GetAllAssetPaths()
            .Where(p=>p.StartsWith("Assets/") && !AssetDatabase.IsValidFolder(p) && !used.Contains(p) && !p.Contains("/Editor/") && !p.EndsWith(".cs") && !p.EndsWith(".asmdef") && File.Exists(p))
            .Select(p=>(path:p,size:new FileInfo(p).Length)).OrderByDescending(x=>x.size).ToList();
        Directory.CreateDirectory("output/usage-audit");
        var folders=unused.GroupBy(x=>string.Join("/",x.path.Split('/').Take(3))).Select(g=>(folder:g.Key,size:g.Sum(x=>x.size),count:g.Count())).OrderByDescending(x=>x.size);
        var lines=new System.Collections.Generic.List<string>{"used assets: "+used.Count+", unused: "+unused.Count+" ("+(unused.Sum(x=>x.size)/1048576.0).ToString("0")+" MB)","","by folder:"};
        lines.AddRange(folders.Select(f=>(f.size/1048576.0).ToString("0.0").PadLeft(8)+" MB  "+f.count.ToString().PadLeft(5)+"  "+f.folder));
        lines.Add("");lines.Add("files:");
        lines.AddRange(unused.Select(x=>(x.size/1048576.0).ToString("0.00").PadLeft(9)+" MB  "+x.path));
        File.WriteAllLines("output/usage-audit/unused.txt",lines);
        File.WriteAllLines("output/usage-audit/used.txt",used.OrderBy(p=>p));
    }
}
