using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

/// <summary>
/// 커맨드라인에서 벤치마크를 자동 실행하기 위한 빌드 스크립트
/// </summary>
public class BuildAndRunBenchmark
{
    [MenuItem("Build/Build Standalone and Run Benchmark")]
    public static void BuildStandaloneBenchmark()
    {
        // 씬 설정
        SetupBenchmarkScene();

        // 빌드 경로
        string buildPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds/Benchmark");
        if (!Directory.Exists(buildPath))
        {
            Directory.CreateDirectory(buildPath);
        }

        string executablePath;
#if UNITY_EDITOR_OSX
        executablePath = Path.Combine(buildPath, "UnityBenchmark.app");
#elif UNITY_EDITOR_WIN
        executablePath = Path.Combine(buildPath, "UnityBenchmark.exe");
#else
        executablePath = Path.Combine(buildPath, "UnityBenchmark");
#endif

        // 빌드 옵션
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = new[] { EditorSceneManager.GetActiveScene().path };
        buildPlayerOptions.locationPathName = executablePath;
        buildPlayerOptions.target = BuildTarget.StandaloneOSX;
        buildPlayerOptions.options = BuildOptions.None;

        Debug.Log($"Building benchmark to: {executablePath}");

        // 빌드 실행
        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"Build succeeded: {report.summary.totalSize} bytes");
            Debug.Log($"Build location: {executablePath}");
            Debug.Log("You can now run the benchmark executable.");
        }
        else
        {
            Debug.LogError($"Build failed: {report.summary.result}");
        }
    }

    [MenuItem("Build/Setup and Save Benchmark Scene")]
    public static void SetupAndSaveBenchmarkScene()
    {
        SetupBenchmarkScene();

        string scenePath = "Assets/Scenes/BenchmarkScene.unity";
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), scenePath);

        Debug.Log($"Benchmark scene saved to: {scenePath}");
    }

    private static void SetupBenchmarkScene()
    {
        // 카메라 설정
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObj = new GameObject("Main Camera");
            mainCamera = cameraObj.AddComponent<Camera>();
            cameraObj.AddComponent<AudioListener>();
            cameraObj.tag = "MainCamera";
        }
        mainCamera.transform.position = new Vector3(0, 8, -20);
        mainCamera.transform.rotation = Quaternion.Euler(30, 0, 0);

        if (mainCamera.GetComponent<CameraController>() == null)
        {
            mainCamera.gameObject.AddComponent<CameraController>();
        }

        // Directional Light
        Light directionalLight = FindLight();
        if (directionalLight == null)
        {
            GameObject lightObj = new GameObject("Directional Light");
            directionalLight = lightObj.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
        }
        directionalLight.transform.position = new Vector3(0, 3, 0);
        directionalLight.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Ground
        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
        }
        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(5, 1, 5);

        // BenchmarkManager
        GameObject benchmarkManager = GameObject.Find("BenchmarkManager");
        if (benchmarkManager == null)
        {
            benchmarkManager = new GameObject("BenchmarkManager");
        }

        // AutoBenchmarkRunner 추가
        if (benchmarkManager.GetComponent<AutoBenchmarkRunner>() == null)
        {
            var autoRunner = benchmarkManager.AddComponent<AutoBenchmarkRunner>();
            autoRunner.autoRunOnStart = true;
            autoRunner.quitAfterComplete = true;
        }

        if (benchmarkManager.GetComponent<PerformanceBenchmark>() == null)
        {
            benchmarkManager.AddComponent<PerformanceBenchmark>();
        }

        if (benchmarkManager.GetComponent<PhysicsStressTest>() == null)
        {
            var physicsTest = benchmarkManager.AddComponent<PhysicsStressTest>();
            physicsTest.autoStart = false;
        }

        if (benchmarkManager.GetComponent<RenderingBenchmark>() == null)
        {
            var renderingBenchmark = benchmarkManager.AddComponent<RenderingBenchmark>();
            renderingBenchmark.enabled = false;
        }

        Debug.Log("Benchmark scene setup complete");
    }

    private static Light FindLight()
    {
#if UNITY_2023_1_OR_NEWER
        Light[] lights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
#else
        Light[] lights = Object.FindObjectsOfType<Light>();
#endif
        foreach (Light light in lights)
        {
            if (light.type == LightType.Directional)
            {
                return light;
            }
        }
        return null;
    }

    // 커맨드라인에서 호출 가능한 static 메서드
    public static void CommandLineBuild()
    {
        Debug.Log("========================================");
        Debug.Log("Command Line Build Started");
        Debug.Log("========================================");

        // 1. 씬 생성 및 설정
        Debug.Log("[1/4] Creating and setting up benchmark scene...");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        SetupBenchmarkScene();

        // 씬 저장
        string scenePath = "Assets/Scenes/BenchmarkScene.unity";
        if (!System.IO.Directory.Exists("Assets/Scenes"))
        {
            System.IO.Directory.CreateDirectory("Assets/Scenes");
        }
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"✓ Scene saved to: {scenePath}");

        // 2. WebGL 템플릿 설정
        Debug.Log("[2/4] Configuring WebGL template...");
        SetWebGLTemplate();

        // 3. Player Settings 최적화
        Debug.Log("[3/4] Optimizing Player Settings...");
        OptimizePlayerSettings();

        // 4. WebGL 빌드
        Debug.Log("[4/4] Building WebGL...");
        BuildWebGL();

        Debug.Log("========================================");
        Debug.Log("Command Line Build Complete");
        Debug.Log("========================================");
    }

    private static void SetWebGLTemplate()
    {
        // WebGL 템플릿을 BenchmarkTemplate으로 설정
        PlayerSettings.WebGL.template = "PROJECT:BenchmarkTemplate";
        Debug.Log("✓ WebGL Template set to: BenchmarkTemplate");
    }

    private static void OptimizePlayerSettings()
    {
        // WebGL 최적화 설정
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.memorySize = 256;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.dataCaching = true;

        // 일반 설정
        PlayerSettings.stripEngineCode = true;
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.WebGL, ManagedStrippingLevel.Medium);

        Debug.Log("✓ Player Settings optimized for WebGL");
    }

    private static void BuildWebGL()
    {
        string buildPath = Path.Combine(Directory.GetCurrentDirectory(), "WebGLBuild");

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = new[] { "Assets/Scenes/BenchmarkScene.unity" };
        buildPlayerOptions.locationPathName = buildPath;
        buildPlayerOptions.target = BuildTarget.WebGL;
        buildPlayerOptions.options = BuildOptions.None;

        Debug.Log($"Building WebGL to: {buildPath}");

        var report = BuildPipeline.BuildPlayer(buildPlayerOptions);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"WebGL build succeeded: {report.summary.totalSize} bytes");
        }
        else
        {
            Debug.LogError($"WebGL build failed: {report.summary.result}");
        }
    }
}
