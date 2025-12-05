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

        // 포트 충돌 방지: Profiler 자동연결 비활성화
        // Unity WebGL 빌드 시 websockify가 포트 54998을 사용하는데,
        // 같은 머신에서 여러 Unity 버전이 동시 빌드하면 충돌 발생
        EditorUserBuildSettings.connectProfiler = false;
        Debug.Log("✓ Profiler autoconnect disabled (prevents port 54998 conflict)");

        // 1. 씬 생성 및 설정
        Debug.Log("[1/5] Creating and setting up benchmark scene...");
        string scenePath = "Assets/Scenes/BenchmarkScene.unity";
        if (!Directory.Exists("Assets/Scenes"))
        {
            Directory.CreateDirectory("Assets/Scenes");
        }

        // 기존 씬 파일 삭제
        if (File.Exists(scenePath))
        {
            File.Delete(scenePath);
            string metaPath = scenePath + ".meta";
            if (File.Exists(metaPath))
            {
                File.Delete(metaPath);
            }
            AssetDatabase.Refresh();
        }

        // Unity 6 호환성: EmptyScene으로 생성하여 직렬화 문제 방지
        // DefaultGameObjects는 Unity 6에서 WebGL 빌드 시 Scene 데이터 손상을 유발할 수 있음
#if UNITY_6000_0_OR_NEWER
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 기본 카메라와 라이트를 수동으로 추가
        CreateDefaultSceneObjects();
#else
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
#endif
        SetupBenchmarkScene();

        // 씬 저장
        EditorSceneManager.SaveScene(scene, scenePath);
        Debug.Log($"✓ Scene saved to: {scenePath}");

        // E2ETestBridge.jslib 복사 (SharedScripts의 Plugins~를 Assets/Plugins로)
        // Note: Plugins~ 폴더는 Unity에서 무시되므로 중복 참조 문제 없음
        string packagePath = Path.GetFullPath("Packages/im.toss.sdk-test-scripts");
        string sharedPluginsPath = Path.Combine(packagePath, "../SharedScripts/Plugins~/E2ETestBridge.jslib");
        string assetsPluginsPath = "Assets/Plugins/E2ETestBridge.jslib";
        string assetsPluginsMetaPath = "Assets/Plugins/E2ETestBridge.jslib.meta";

        if (File.Exists(sharedPluginsPath))
        {
            if (!Directory.Exists("Assets/Plugins"))
            {
                Directory.CreateDirectory("Assets/Plugins");
            }

            // 기존 구버전 jslib 파일 삭제 (E2EBridge.jslib)
            string oldJslibPath = "Assets/Plugins/E2EBridge.jslib";
            string oldJslibMetaPath = "Assets/Plugins/E2EBridge.jslib.meta";
            if (File.Exists(oldJslibPath))
            {
                File.Delete(oldJslibPath);
                Debug.Log($"✓ Deleted old E2EBridge.jslib");
            }
            if (File.Exists(oldJslibMetaPath))
            {
                File.Delete(oldJslibMetaPath);
            }

            // jslib 파일 복사
            File.Copy(sharedPluginsPath, assetsPluginsPath, true);

            // Unity가 인식할 수 있도록 .meta 파일 생성 (WebGL 플랫폼용)
            string metaContent = @"fileFormatVersion: 2
guid: a1b2c3d4e5f6789012345678abcdef00
PluginImporter:
  externalObjects: {}
  serializedVersion: 2
  iconMap: {}
  executionOrder: {}
  defineConstraints: []
  isPreloaded: 0
  isOverridable: 0
  isExplicitlyReferenced: 0
  validateReferences: 1
  platformData:
  - first:
      Any:
    second:
      enabled: 0
      settings: {}
  - first:
      Editor: Editor
    second:
      enabled: 0
      settings:
        DefaultValueInitialized: true
  - first:
      WebGL: WebGL
    second:
      enabled: 1
      settings: {}
  userData:
  assetBundleName:
  assetBundleVariant:
";
            File.WriteAllText(assetsPluginsMetaPath, metaContent);
            AssetDatabase.Refresh();
            Debug.Log($"✓ E2ETestBridge.jslib copied to Assets/Plugins");
        }
        else
        {
            Debug.LogError($"✗ E2ETestBridge.jslib not found at {sharedPluginsPath}");
            Debug.LogError("RuntimeAPITester requires E2ETestBridge.jslib to work properly.");
            EditorApplication.Exit(1);
            return;
        }

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
        config.appName = "unity-sdk-sample";
        config.displayName = "Unity SDK Sample";
        config.version = "1.0.0";
        config.description = "E2E test for Apps in Toss Unity SDK";
        config.iconUrl = "https://via.placeholder.com/512"; // 테스트용 아이콘
        config.primaryColor = "#1E88E5";
        config.isProduction = true;  // 프로덕션 환경 시뮬레이션
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

        // Unity 2021.3에서 Bee 빌드 캐시 문제로 인한 무한 루프 방지
        // Library/Bee 폴더를 삭제하여 완전한 클린 빌드 수행
        string beePath = System.IO.Path.Combine(Application.dataPath, "../Library/Bee");
        if (System.IO.Directory.Exists(beePath))
        {
            Debug.Log("[AIT] Bee 빌드 캐시 삭제 중... (빌드 루프 방지)");
            System.IO.Directory.Delete(beePath, true);
        }

        var result = AITConvertCore.DoExport(buildWebGL: true, doPackaging: true, cleanBuild: true);

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

    /// <summary>
    /// E2E 테스트 파이프라인에서 호출하는 메서드
    /// </summary>
    public static void PerformE2EBuild()
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

        // CameraController는 E2EBootstrapper에서 런타임에 추가됨

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

        // BenchmarkManager - 빈 GameObject만 생성
        // 모든 스크립트 컴포넌트는 E2EBootstrapper에서 런타임에 추가됨
        // 이렇게 하면 Unity 6에서 Scene 직렬화 시 스크립트 참조가 누락되는 문제를 방지
        GameObject benchmarkManager = GameObject.Find("BenchmarkManager");
        if (benchmarkManager == null)
        {
            benchmarkManager = new GameObject("BenchmarkManager");
        }

        // E2EBootstrapperHelper 추가 (RuntimeInitializeOnLoadMethod의 대안)
        // WebGL에서 RuntimeInitializeOnLoadMethod가 작동하지 않을 경우를 대비
        if (benchmarkManager.GetComponent<E2EBootstrapperHelper>() == null)
        {
            benchmarkManager.AddComponent<E2EBootstrapperHelper>();
            Debug.Log("Added E2EBootstrapperHelper to BenchmarkManager");
        }

        Debug.Log("Benchmark scene setup complete (scripts will be added at runtime by E2EBootstrapper)");
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

#if UNITY_6000_0_OR_NEWER
    /// <summary>
    /// Unity 6 호환성: EmptyScene에 기본 오브젝트 생성
    /// DefaultGameObjects 대신 수동으로 생성하여 직렬화 문제 방지
    /// </summary>
    private static void CreateDefaultSceneObjects()
    {
        // Main Camera 생성
        GameObject cameraObj = new GameObject("Main Camera");
        Camera mainCamera = cameraObj.AddComponent<Camera>();
        cameraObj.AddComponent<AudioListener>();
        cameraObj.tag = "MainCamera";
        mainCamera.clearFlags = CameraClearFlags.Skybox;
        cameraObj.transform.position = new Vector3(0, 1, -10);
        Debug.Log("✓ Main Camera created for Unity 6");

        // Directional Light 생성
        GameObject lightObj = new GameObject("Directional Light");
        Light directionalLight = lightObj.AddComponent<Light>();
        directionalLight.type = LightType.Directional;
        directionalLight.color = new Color(1f, 0.956f, 0.839f); // Warm white
        directionalLight.intensity = 1f;
        lightObj.transform.position = new Vector3(0, 3, 0);
        lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);
        Debug.Log("✓ Directional Light created for Unity 6");
    }
#endif
}
