using UnityEditor;
using UnityEditor.SceneManagement;

// Runs the playable village suite in batch mode and exits without building the player.
// Run: Unity.exe -batchmode -projectPath . -executeMethod RegressionWithoutBuild.Run
[InitializeOnLoad]
public static class RegressionWithoutBuild
{
    const string Key="RegressionWithoutBuild";
    static RegressionWithoutBuild()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Key,false))return;
            SessionState.SetBool(Key,false);
            EditorApplication.delayCall+=()=>EditorApplication.Exit(SessionState.GetBool("ChickenHeist.PlayableVillageTests.passed",false)?0:1);
        };
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        SessionState.SetBool("ChickenHeist.PlayableVillageTests.build",false);SessionState.SetBool(Key,true);
        PlayableVillageTests.Run();
    }
}
