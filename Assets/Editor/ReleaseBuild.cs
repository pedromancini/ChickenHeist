using UnityEditor.SceneManagement;

// Batch entry point: opens the rural world and builds the Windows player.
// Run: Unity.exe -batchmode -quit -projectPath . -executeMethod ReleaseBuild.Windows
public static class ReleaseBuild
{
    public static void Windows(){EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);ChickenHeistPlayerBuild.Build();}
}
