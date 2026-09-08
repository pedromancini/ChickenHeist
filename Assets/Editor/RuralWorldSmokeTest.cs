using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RuralWorldSmokeTest
{
    private const string Key = "ChickenHeist.RuralSmoke";
    private static readonly Dictionary<FarmAnimalBoundary, Vector3> starts = new Dictionary<FarmAnimalBoundary, Vector3>();
    private static int errors;
    private static int violations;
    private static float started;

    static RuralWorldSmokeTest()
    {
        EditorApplication.playModeStateChanged += Changed;
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, type) =>
        {
            if (SessionState.GetBool(Key, false) && (type == LogType.Exception || type == LogType.Error)) errors++;
        };
    }

    [MenuItem("Chicken Heist/Test Rural Animal Movement")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorSceneManager.SaveOpenScenes();
        // Keep the automated test from capturing the user's cursor.
        foreach (var look in Object.FindObjectsByType<PlayerLook>(FindObjectsSortMode.None)) look.enabled = false;
        foreach (var movement in Object.FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None)) movement.enabled = false;
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static void Changed(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            started = Time.realtimeSinceStartup;
            starts.Clear();
            errors = violations = 0;
            foreach (var animal in Object.FindObjectsByType<FarmAnimalBoundary>(FindObjectsSortMode.None))
                starts[animal] = animal.transform.position;
            Application.runInBackground = true;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            EditorSceneManager.OpenScene(RuralWorldReview.WorldScene);
            Debug.Log("Rural movement smoke test finished. See output/world-review/play-audit.txt");
        }
    }

    private static void Tick()
    {
        if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying || starts.Count == 0) return;
        int moved = 0;
        var animals = Object.FindObjectsByType<FarmAnimalBoundary>(FindObjectsSortMode.None);
        foreach (var animal in animals)
        {
            Vector3 p = animal.transform.position;
            if (!animal.Allows(p)) violations++;
            if (starts.TryGetValue(animal, out Vector3 start) && Vector3.Distance(start, p) > 0.1f) moved++;
        }
        if (Time.realtimeSinceStartup - started < 15f) return;
        File.WriteAllLines("output/world-review/play-audit.txt", new[] {
            "Duration: 15 seconds in Unity Play Mode (player input disabled for automated test).",
            "Animals=" + animals.Length + " | moved=" + moved + " | boundary/overlap violations=" + violations + " | errors=" + errors,
            animals.Length == 132 && moved > 0 && violations == 0 && errors == 0 ? "PASS" : "FAIL"
        });
        starts.Clear();
        EditorApplication.isPlaying = false;
    }
}
