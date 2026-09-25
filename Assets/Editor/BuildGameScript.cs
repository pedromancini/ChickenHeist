using UnityEngine;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;

public class BuildGameScript
{
    [MenuItem("Tools/Build Windows Game (.exe)")]
    public static void BuildWindowsGame()
    {
        string buildFolderPath = "E:/Jogo3D/Build";
        if (!Directory.Exists(buildFolderPath))
        {
            Directory.CreateDirectory(buildFolderPath);
        }

        string exePath = Path.Combine(buildFolderPath, "ChickenHeist.exe");

        string[] scenes = new string[]
        {
            "Assets/Scenes/ChickenHeistRuralWorld.unity"
        };

        Debug.Log($"[BUILD] Iniciando a compilação do jogo para Windows 64-bit em: {exePath}");

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = scenes;
        buildPlayerOptions.locationPathName = exePath;
        buildPlayerOptions.target = BuildTarget.StandaloneWindows64;
        buildPlayerOptions.options = BuildOptions.None;

        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[BUILD_SUCCESS] Build de Chicken Heist concluída com SUCESSO!");
            Debug.Log($"[BUILD_SUCCESS] Tamanho Total: {summary.totalSize / (1024f * 1024f):F2} MB");
            Debug.Log($"[BUILD_SUCCESS] Local do Executável: {exePath}");

            try
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{exePath.Replace('/', '\\')}\"");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[BUILD] Não foi possível abrir o Explorer automaticamente: {ex.Message}");
            }
        }
        else if (summary.result == BuildResult.Failed)
        {
            Debug.LogError($"[BUILD_FAILED] A compilação FALHOU com {summary.totalErrors} erros. Verifique o console da Unity.");
        }
    }
}