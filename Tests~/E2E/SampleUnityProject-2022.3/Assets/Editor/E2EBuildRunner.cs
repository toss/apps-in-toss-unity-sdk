using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using AppsInToss;

/// <summary>
/// E2E 테스트용 빌드 스크립트 - SDK API를 직접 호출
/// </summary>
public class E2EBuildRunner
{
    [MenuItem("E2E/Build with SDK")]
    public static void BuildWithSDK()
    {
        Debug.Log("========================================");
        Debug.Log("E2E Build with Apps in Toss SDK");
        Debug.Log("========================================");

        // 1. 씬 생성 및 설정
        Debug.Log("[1/5] Creating and setting up benchmark scene...");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        SetupBenchmarkScene();

        // 씬 저장
        string scenePath = "Assets/Scenes/BenchmarkScene.unity";
        if (!Directory.Exists("Assets/Scenes"))
        {
            Directory.CreateDirectory("Assets/Scenes");
        }
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"✓ Scene saved to: {scenePath}");

        // Build Settings에 Scene 추가
        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(scenePath, true)
        };
        EditorBuildSettings.scenes = scenes;
        Debug.Log("✓ Scene added to Build Settings");

        // 2. SDK 설정 구성
        Debug.Log("[2/5] Configuring Apps in Toss SDK...");
        var config = UnityUtil.GetEditorConf();
        config.appName = "e2e-benchmark-test";
        config.displayName = "E2E Benchmark Test";
        config.version = "1.0.0";
        config.description = "E2E test for Apps in Toss Unity SDK";
        config.iconUrl = "https://via.placeholder.com/512"; // 테스트용 아이콘
        config.primaryColor = "#1E88E5";
        config.isProduction = false;
        config.enableOptimization = true;
        config.localPort = 4173;
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("✓ SDK config updated");

        // 3. SDK의 Init 호출
        Debug.Log("[3/5] Initializing SDK...");
        AITConvertCore.Init();
        Debug.Log("✓ SDK initialized");

        // 4. SDK의 빌드 & 패키징 실행
        Debug.Log("[4/5] Building WebGL and packaging with SDK...");
        var result = AITConvertCore.DoExport(buildWebGL: true, doPackaging: true);

        if (result == AITConvertCore.AITExportError.SUCCEED)
        {
            Debug.Log("✓ SDK build succeeded!");
            Debug.Log("[5/5] Build artifacts should be in ait-build/dist/");
            Debug.Log("========================================");
            Debug.Log("E2E Build Complete - SUCCESS");
            Debug.Log("========================================");
        }
        else
        {
            string errorMessage = AITConvertCore.GetErrorMessage(result);
            Debug.LogError($"✗ SDK build failed: {errorMessage}");
            Debug.LogError("========================================");
            Debug.LogError("E2E Build Complete - FAILED");
            Debug.LogError("========================================");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// 커맨드라인에서 호출 가능한 메서드 (기존 benchmark.sh 호환)
    /// </summary>
    public static void CommandLineBuild()
    {
        BuildWithSDK();
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
}
