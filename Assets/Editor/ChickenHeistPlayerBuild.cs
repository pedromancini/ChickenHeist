using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class ChickenHeistPlayerBuild
{
    [MenuItem("Chicken Heist/Build Windows Player")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play mode before building.");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!=RuralWorldReview.WorldScene)
            throw new InvalidOperationException("Open the rural world scene before building.");
        var economy=UnityEngine.Object.FindAnyObjectByType<HouseholdEconomy>();
        var game=UnityEngine.Object.FindAnyObjectByType<HeistGameManager>();
        if(economy==null || !string.IsNullOrEmpty(economy.editorTestSavePath) || game?.player==null
            || !game.player.GetComponent<PlayerMovement>().enabled
            || !game.player.GetComponentInChildren<PlayerLook>().enabled)
            throw new InvalidOperationException("Build aborted: scene still contains test overrides or missing player controls.");
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        const string folder="Builds/Windows";
        Directory.CreateDirectory(folder);
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes=new[]{RuralWorldReview.WorldScene},
            locationPathName=folder+"/ChickenHeist.exe",
            target=BuildTarget.StandaloneWindows64,
            options=BuildOptions.None
        });
        var summary=report.summary;
        Directory.CreateDirectory("output/playable-review");
        File.WriteAllText("output/playable-review/windows-build.txt",
            "Result: "+summary.result+"\nErrors: "+summary.totalErrors+"\nWarnings: "+summary.totalWarnings+
            "\nDuration: "+summary.totalTime+"\nBytes: "+summary.totalSize+"\nExecutable: "+Path.GetFullPath(folder+"/ChickenHeist.exe"));
        if(summary.result!=BuildResult.Succeeded)
            throw new InvalidOperationException("Windows build failed. See the build report and Editor.log.");
        Debug.Log("CHICKEN HEIST WINDOWS BUILD SUCCEEDED: "+Path.GetFullPath(folder+"/ChickenHeist.exe"));
    }
}
