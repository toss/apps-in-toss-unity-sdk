using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// WebGL 빌드를 위한 에디터 스크립트
/// </summary>
public class WebGLBuilder : EditorWindow
{
    private string buildPath = "WebGLBuild";
    private bool developmentBuild = false;
    private bool autoRunPlayer = false;

    [MenuItem("Build/WebGL Build Window")]
    public static void ShowWindow()
    {
        GetWindow<WebGLBuilder>("WebGL Builder");
    }

    void OnGUI()
    {
        GUILayout.Label("WebGL Build Settings", EditorStyles.boldLabel);

        EditorGUILayout.Space();

        buildPath = EditorGUILayout.TextField("Build Path:", buildPath);

        EditorGUILayout.Space();

        developmentBuild = EditorGUILayout.Toggle("Development Build", developmentBuild);
        autoRunPlayer = EditorGUILayout.Toggle("Auto Run Player", autoRunPlayer);

        EditorGUILayout.Space();

        if (GUILayout.Button("Build WebGL", GUILayout.Height(40)))
        {
            BuildWebGL();
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Build and Run", GUILayout.Height(40)))
        {
            BuildAndRun();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "빌드 전에 Build Settings에서 씬이 추가되어 있는지 확인하세요.\n\n" +
            "권장 설정:\n" +
            "- Player Settings > Publishing Settings > Compression Format: Gzip\n" +
            "- Player Settings > Publishing Settings > Memory Size: 256MB\n" +
            "- Player Settings > Other Settings > Managed Stripping Level: Medium",
            MessageType.Info
        );
    }

    void BuildWebGL()
    {
        // 빌드 경로 설정
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), buildPath);

        // 빌드 옵션 설정
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = GetScenePaths();
        buildPlayerOptions.locationPathName = fullPath;
        buildPlayerOptions.target = BuildTarget.WebGL;

        if (developmentBuild)
        {
            buildPlayerOptions.options = BuildOptions.Development;
        }
        else
        {
            buildPlayerOptions.options = BuildOptions.None;
        }

        // WebGL 플랫폼 체크
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
        {
            if (!EditorUtility.DisplayDialog(
                "Platform Switch Required",
                "Current platform is not WebGL. Switch to WebGL platform?",
                "Switch",
                "Cancel"))
            {
                return;
            }
        }

        Debug.Log($"Starting WebGL build to: {fullPath}");

        // 빌드 실행
        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"Build succeeded: {report.summary.totalSize} bytes");

            // 탐색기에서 폴더 열기
            EditorUtility.RevealInFinder(fullPath);
        }
        else
        {
            Debug.LogError($"Build failed: {report.summary.result}");
        }
    }

    void BuildAndRun()
    {
        // 빌드 경로 설정
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), buildPath);

        // 빌드 옵션 설정
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = GetScenePaths();
        buildPlayerOptions.locationPathName = fullPath;
        buildPlayerOptions.target = BuildTarget.WebGL;
        buildPlayerOptions.options = BuildOptions.AutoRunPlayer;

        if (developmentBuild)
        {
            buildPlayerOptions.options |= BuildOptions.Development;
        }

        Debug.Log($"Starting WebGL build and run to: {fullPath}");

        // 빌드 실행
        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"Build succeeded: {report.summary.totalSize} bytes");
        }
        else
        {
            Debug.LogError($"Build failed: {report.summary.result}");
        }
    }

    string[] GetScenePaths()
    {
        // Build Settings에 추가된 씬들 가져오기
        var scenes = EditorBuildSettings.scenes;

        if (scenes.Length == 0)
        {
            Debug.LogWarning("No scenes in Build Settings. Adding current scene...");

            // 현재 씬 추가
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(currentScene.path))
            {
                return new string[] { currentScene.path };
            }
            else
            {
                Debug.LogError("No active scene found!");
                return new string[0];
            }
        }

        // 활성화된 씬만 필터링
        string[] scenePaths = new string[scenes.Length];
        for (int i = 0; i < scenes.Length; i++)
        {
            scenePaths[i] = scenes[i].path;
        }

        return scenePaths;
    }

    [MenuItem("Build/Quick WebGL Build")]
    public static void QuickBuild()
    {
        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), "WebGLBuild");

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = GetActiveScenePaths();
        buildPlayerOptions.locationPathName = fullPath;
        buildPlayerOptions.target = BuildTarget.WebGL;
        buildPlayerOptions.options = BuildOptions.None;

        Debug.Log($"Quick build to: {fullPath}");

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"Quick build succeeded: {report.summary.totalSize} bytes");
            EditorUtility.RevealInFinder(fullPath);
        }
        else
        {
            Debug.LogError($"Quick build failed: {report.summary.result}");
        }
    }

    static string[] GetActiveScenePaths()
    {
        var scenes = EditorBuildSettings.scenes;

        if (scenes.Length == 0)
        {
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (!string.IsNullOrEmpty(currentScene.path))
            {
                return new string[] { currentScene.path };
            }
            return new string[0];
        }

        string[] scenePaths = new string[scenes.Length];
        for (int i = 0; i < scenes.Length; i++)
        {
            scenePaths[i] = scenes[i].path;
        }

        return scenePaths;
    }
}
