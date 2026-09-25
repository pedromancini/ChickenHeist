using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class PhaseTwoUpgrade
{
    static PhaseTwoUpgrade()
    {
        EditorApplication.playModeStateChanged+=state=>
        {
            if(state!=PlayModeStateChange.EnteredEditMode || !SessionState.GetBool("PhaseTwoRegression",false))return;
            SessionState.SetBool("PhaseTwoRegression",false);
            EditorApplication.delayCall+=()=>
            {
                string file="output/playable-review/playable-tests.txt";
                bool passed=File.Exists(file) && !File.ReadAllText(file).Contains("FAIL") && !File.ReadAllText(file).Contains("ERROR");
                // Windows build is explicitly deferred until the end of the full plan.
                EditorApplication.Exit(passed?0:1);
            };
        };
    }
    public static void Regression()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        SessionState.SetBool("PhaseTwoRegression",true);
        PlayableVillageTests.Run();
    }
    public static void Install()
    {
        EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
        foreach(var farm in Object.FindObjectsByType<FarmLayoutInfo>())ProceduralFarmGenerator.CreatePhaseTwoWires(farm);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        PhaseTwoReview.Begin();
    }
}
