using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ReleaseBuild
{
    public static void Windows(){EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);ChickenHeistPlayerBuild.Build();}

    [MenuItem("Chicken Heist/Build macOS Player")]
    public static void Mac()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        var settings=Type.GetType("UnityEditor.OSXStandalone.UserBuildSettings, UnityEditor.OSXStandalone.Extensions");
        if(settings==null)throw new InvalidOperationException("Mac Build Support is not installed for this Unity version.");
        var arch=settings.GetProperty("architecture");
        arch?.SetValue(null,Enum.Parse(arch.PropertyType,"x64ARM64"));
        const string folder="Builds/macOS";
        Directory.CreateDirectory(folder);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes=new[]{RuralWorldReview.WorldScene},
            locationPathName=folder+"/ChickenHeist.app",
            target=BuildTarget.StandaloneOSX,
            options=BuildOptions.None
        });
        var summary=report.summary;
        Directory.CreateDirectory("output/playable-review");
        File.WriteAllText("output/playable-review/mac-build.txt","Result: "+summary.result+"\nErrors: "+summary.totalErrors+"\nBytes: "+summary.totalSize+"\nArchitecture: "+(arch!=null?arch.GetValue(null):"?"));
        if(summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("macOS build failed. See Editor.log.");
        Debug.Log("CHICKEN HEIST MAC BUILD SUCCEEDED: "+Path.GetFullPath(folder+"/ChickenHeist.app"));
    }
}
