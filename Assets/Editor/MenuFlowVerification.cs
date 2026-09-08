using UnityEditor;
using UnityEditor.SceneManagement;

public static class MenuFlowVerification
{
    public static void BuildOnly()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        ChickenHeistPlayerBuild.Build();EditorApplication.Exit(0);
    }
    public static void RunBatch()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        SessionState.SetBool("Protagonist.Batch",true);
        PlayableVillageTests.Run();
    }
}
