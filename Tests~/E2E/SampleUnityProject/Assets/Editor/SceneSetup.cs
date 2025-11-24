using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 벤치마크 씬을 자동으로 설정하는 에디터 스크립트
/// </summary>
public class SceneSetup : EditorWindow
{
    [MenuItem("Tools/Setup Benchmark Scene")]
    public static void SetupBenchmarkScene()
    {
        if (!EditorUtility.DisplayDialog(
            "Setup Benchmark Scene",
            "이 작업은 현재 씬에 벤치마크 오브젝트들을 추가합니다.\n계속하시겠습니까?",
            "확인",
            "취소"))
        {
            return;
        }

        // 1. 카메라 설정
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

        // 카메라 컨트롤러 추가
        if (mainCamera.GetComponent<CameraController>() == null)
        {
            mainCamera.gameObject.AddComponent<CameraController>();
        }

        Debug.Log("✓ Camera setup complete");

        // 2. Directional Light 설정
        Light directionalLight = FindObjectOfType<Light>();
        if (directionalLight == null || directionalLight.type != LightType.Directional)
        {
            GameObject lightObj = new GameObject("Directional Light");
            directionalLight = lightObj.AddComponent<Light>();
            directionalLight.type = LightType.Directional;
        }

        directionalLight.transform.position = new Vector3(0, 3, 0);
        directionalLight.transform.rotation = Quaternion.Euler(50, -30, 0);
        directionalLight.color = new Color(1f, 0.956f, 0.839f);
        directionalLight.intensity = 1f;
        directionalLight.shadows = LightShadows.Soft;

        Debug.Log("✓ Light setup complete");

        // 3. Ground Plane 생성
        GameObject ground = GameObject.Find("Ground");
        if (ground == null)
        {
            ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
        }

        ground.transform.position = Vector3.zero;
        ground.transform.localScale = new Vector3(5, 1, 5); // Plane은 10x10이므로 5배하면 50x50

        Debug.Log("✓ Ground setup complete");

        // 4. BenchmarkManager 생성
        GameObject benchmarkManager = GameObject.Find("BenchmarkManager");
        if (benchmarkManager == null)
        {
            benchmarkManager = new GameObject("BenchmarkManager");
        }

        // AutoBenchmarkRunner 추가 (가장 먼저)
        if (benchmarkManager.GetComponent<AutoBenchmarkRunner>() == null)
        {
            var autoRunner = benchmarkManager.AddComponent<AutoBenchmarkRunner>();
            autoRunner.autoRunOnStart = true;
            autoRunner.quitAfterComplete = true;
            autoRunner.baselineTestDuration = 10f;
            autoRunner.physicsTestDuration = 15f;
            autoRunner.renderingTestDuration = 15f;
            autoRunner.combinedTestDuration = 20f;
            autoRunner.physicsTestObjectCount = 50;
            autoRunner.renderingTestGridSize = 10;
        }

        // 모든 벤치마크 스크립트 추가
        if (benchmarkManager.GetComponent<PerformanceBenchmark>() == null)
        {
            var perfBenchmark = benchmarkManager.AddComponent<PerformanceBenchmark>();
            perfBenchmark.showStats = true;
        }

        if (benchmarkManager.GetComponent<PhysicsStressTest>() == null)
        {
            var physicsTest = benchmarkManager.AddComponent<PhysicsStressTest>();
            physicsTest.objectsPerWave = 10;
            physicsTest.spawnInterval = 0.5f;
            physicsTest.maxObjects = 50;
            physicsTest.autoStart = false; // AutoBenchmarkRunner가 제어
        }

        if (benchmarkManager.GetComponent<RenderingBenchmark>() == null)
        {
            var renderingBenchmark = benchmarkManager.AddComponent<RenderingBenchmark>();
            renderingBenchmark.gridSize = 10;
            renderingBenchmark.spacing = 2f;
            renderingBenchmark.animateObjects = true;
            renderingBenchmark.enabled = false; // AutoBenchmarkRunner가 활성화
        }

        if (benchmarkManager.GetComponent<ObjectSpawner>() == null)
        {
            benchmarkManager.AddComponent<ObjectSpawner>();
        }

        if (benchmarkManager.GetComponent<TextureGenerator>() == null)
        {
            var textureGen = benchmarkManager.AddComponent<TextureGenerator>();
            textureGen.enabled = false; // 기본적으로 비활성화
        }

        Debug.Log("✓ BenchmarkManager setup complete (with AutoBenchmarkRunner)");

        // 씬 저장
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        EditorUtility.DisplayDialog(
            "Setup Complete",
            "벤치마크 씬 설정이 완료되었습니다!\n\n" +
            "추가된 오브젝트:\n" +
            "- Main Camera (CameraController)\n" +
            "- Directional Light\n" +
            "- Ground Plane\n" +
            "- BenchmarkManager (모든 벤치마크 스크립트)\n\n" +
            "Play 버튼을 눌러 테스트하세요!",
            "확인");

        Debug.Log("========================================");
        Debug.Log("Benchmark Scene Setup Complete!");
        Debug.Log("========================================");
    }

    [MenuItem("Tools/Quick Setup New Scene")]
    public static void QuickSetupNewScene()
    {
        // 새 씬 생성
        var newScene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        // 씬 설정 실행
        SetupBenchmarkScene();
    }
}
