using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using AppsInToss;
using AppsInToss.Editor;

/// <summary>
/// E2E 테스트용 빌드 스크립트 - SDK API를 직접 호출
/// </summary>
public class E2EBuildRunner
{
    /// <summary>SharedScripts 패키지 내 비임포트 원본 폰트 디렉토리 이름(Runtime/ 바로 아래, "~" 접미 —
    /// Unity가 임포트하지 않음). AITFontSubsetProcessor.ToolDirName 과 동일한 명명 관용구.</summary>
    private const string FontsSourceDirName = "Fonts~";

    /// <summary>비임포트 원본에서 복사해 각 프로젝트 Assets/Resources/Fonts/ 에 배치할 폰트 파일명
    /// (UIBuilder.cs 의 Resources.Load("Fonts/NotoSansKR-Regular") 상대 경로가 이 이름에 의존한다).</summary>
    private const string NotoSansKrFileName = "NotoSansKR-Regular.otf";

    [MenuItem("E2E/Build with SDK")]
    public static void BuildWithSDK()
    {
        Debug.Log("========================================");
        Debug.Log("E2E Build with Apps in Toss SDK");
        Debug.Log("========================================");

        // 폰트 원본은 패키지 비임포트 폴더(Runtime/Fonts~/)에 있어 "패키지 Runtime/Resources/ 는
        // 무조건 빌드 포함" 규칙을 벗어난다(15MB+ 폰트가 서브셋 불가능하게 모든 .data 에 실리는 문제
        // 해결) — 각 프로젝트 Assets/Resources/Fonts/ 로 복사해 (a) UIBuilder.cs:69 의
        // Resources.Load("Fonts/NotoSansKR-Regular") 경로를 유지하면서 (b) Assets/ 하위이므로
        // fontSubset 레버 사정권에 들어오게 한다. 다른 러너(HeavyBuildRunner/DeployProbeBuildRunner)도
        // 이 폰트를 별도 용도로 복사하지만 이 훅에 의존하지 않고 각자 원본에서 직접 해석한다.
        EnsureFontsCopiedToResources();

        // 포트 충돌 방지: Profiler 자동연결 비활성화
        // Unity WebGL 빌드 시 websockify가 포트를 사용하는데 (6000.x: 35020, 2021-2022: 54998),
        // 같은 머신에서 여러 Unity 버전이 동시 빌드하면 충돌 발생
        // 참고: connectProfiler=false로 프로파일러 포트(54998)는 방지되지만,
        // 빌드 파이프라인의 websockify는 여전히 실행됨 (CI에서 -logFile 파일 기록으로 대응)
        EditorUserBuildSettings.connectProfiler = false;
        Debug.Log("✓ Profiler autoconnect disabled (prevents port conflict)");

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

        // E2ETestBridge.jslib는 SharedScripts 패키지의 Plugins/ 폴더에 포함되어 있음
        // UPM 패키지로 자동 로드되므로 별도 복사 불필요
        // Note: Assets/Plugins에 중복 복사하면 "Plugin used from several locations" 오류 발생
        Debug.Log("✓ E2ETestBridge.jslib available via SharedScripts package");

        // Build Settings에 Scene 추가
        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(scenePath, true)
        };
        EditorBuildSettings.scenes = scenes;
        Debug.Log("✓ Scene added to Build Settings");

        // 배포 프로브 픽스처 훅: DeployProbeBuildRunner가 env var로 프로브 씬 경로를 넘기면
        // index 1로 추가한다(위 단일 원소 덮어쓰기 로직은 그대로 두고 최소 개입으로 append).
        string probeScenePath = System.Environment.GetEnvironmentVariable("AIT_DEPLOY_PROBE_SCENE_PATH");
        if (!string.IsNullOrEmpty(probeScenePath) && File.Exists(probeScenePath))
        {
            EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
            {
                scenes[0],
                new EditorBuildSettingsScene(probeScenePath, true)
            };
            Debug.Log($"✓ Deploy probe scene added to Build Settings (index 1): {probeScenePath}");
        }

        // 2. SDK 설정 구성
        Debug.Log("[2/5] Configuring Apps in Toss SDK...");
        var config = UnityUtil.GetEditorConf();
        config.appName = "unity-sdk-sample";
        config.displayName = "Unity SDK Sample";
        config.version = "1.0.0";
        config.description = "E2E test for Apps in Toss Unity SDK";
        config.iconUrl = "https://via.placeholder.com/512"; // 테스트용 아이콘
        config.primaryColor = "#1E88E5";
        // Unity 버전별 고유 포트 사용 (동시 실행 시 충돌 방지)
        // 환경 변수가 설정된 경우 해당 값 사용 (CI 제어용)
        int portOffset = GetPortOffsetForUnityVersion();
        config.graniteHost = GetEnvString("AIT_GRANITE_HOST", "0.0.0.0");
        config.granitePort = GetEnvInt("AIT_GRANITE_PORT", 8081 + portOffset);
        config.viteHost = GetEnvString("AIT_VITE_HOST", "localhost");
        config.vitePort = GetEnvInt("AIT_VITE_PORT", 5173 + portOffset);
        Debug.Log($"✓ Server config: Granite={config.graniteHost}:{config.granitePort}, Vite={config.viteHost}:{config.vitePort}");
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("✓ SDK config updated");

        // 3. SDK의 Init 호출
        Debug.Log("[3/5] Initializing SDK...");
        AITConvertCore.Init();
        Debug.Log("✓ SDK initialized");

        // 4. SDK의 빌드 & 패키징 실행
        Debug.Log("[4/5] Building WebGL and packaging with SDK...");

        // Library/Bee/는 CI에서 SDK/asmdef/jslib 변경이 감지될 때만 삭제됨 (stale ref.dll 방지)
        // 로컬 환경에서는 SDK 재생성 후 캐시 문제 발생 시 Library/Bee/ 수동 삭제 필요
        // cleanBuild: false로 설정하여 Unity 측 증분 빌드 캐시 재사용
        // 전체 Library 삭제가 필요한 경우 workflow_dispatch에서 clean_library=true로 실행
        // E2E 테스트에서는 프로덕션 환경을 시뮬레이션하기 위해 productionProfile 사용
        var result = AITConvertCore.DoExport(
            buildWebGL: true,
            doPackaging: true,
            cleanBuild: false,
            profile: config.productionProfile,
            profileName: "E2E Build"
        );

        if (result == AITConvertCore.AITExportError.SUCCEED)
        {
            Debug.Log("✓ SDK build succeeded!");

            // 빌드 산출물 검증 (Level 1 - 기존 Playwright Tests 1, 3, 4 대체)
            Debug.Log("[5/6] Validating build output...");
            string projectPath = Path.GetDirectoryName(Application.dataPath);
            var validation = BuildOutputValidator.ValidateAll(projectPath);
            string jsonPath = Path.Combine(projectPath, "ait-build", "build-validation.json");
            File.WriteAllText(jsonPath, JsonUtility.ToJson(validation, true));
            Debug.Log($"✓ Build validation written to: {jsonPath}");

            if (!validation.passed)
            {
                // 검증 에러 메시지에 ait-build 경로 등 AIT 키워드가 섞여 들어가면 SDK 에러 트래커가
                // 캡처(IsAitRelated 통과)하므로 sentryCapture:false로 차단. E2E 검증 실패는 CI exit
                // code 2로 검출되며 Sentry 대상이 아니다.
                foreach (var err in validation.errors)
                    AITLog.Error($"[Validation] {err}", sentryCapture: false);
                Debug.LogError("========================================");
                Debug.LogError("E2E Build Complete - VALIDATION FAILED");
                Debug.LogError("========================================");
                EditorApplication.Exit(2);
                return;
            }

            foreach (var warn in validation.warnings)
                Debug.LogWarning($"[Validation] {warn}");

            Debug.Log($"✓ Build validation passed (size: {validation.buildSizeMB:F2} MB, files: {validation.fileCount}, compression: {validation.compressionFormat})");
            Debug.Log("[6/6] Build artifacts verified in ait-build/dist/");
            Debug.Log("========================================");
            Debug.Log("E2E Build Complete - SUCCESS");
            Debug.Log("========================================");
        }
        else
        {
            string errorMessage = AITConvertCore.GetErrorMessage(result);
            // errorMessage에는 "ait-build" 등 AIT 키워드가 포함되어 SDK 에러 트래커가 이 콘솔
            // 로그를 캡처(SDK-10P cascade)한다. E2E 빌드 실패는 CI exit code 1로 검출되므로
            // Sentry 캡처 대상이 아니다 — sentryCapture:false로 차단.
            AITLog.Error($"✗ SDK build failed: {errorMessage}", sentryCapture: false);
            Debug.LogError("========================================");
            Debug.LogError("E2E Build Complete - FAILED");
            Debug.LogError("========================================");
            EditorApplication.Exit(1);
        }
    }

    /// <summary>
    /// Unity 버전에 따른 포트 오프셋 반환 (동시 실행 시 충돌 방지)
    /// 2021.3 → 0, 2022.3 → 1, 6000.0 → 2, 6000.2 → 3, 6000.3 → 4
    /// </summary>
    private static int GetPortOffsetForUnityVersion()
    {
#if UNITY_6000_3_OR_NEWER
        return 4;
#elif UNITY_6000_2_OR_NEWER
        return 3;
#elif UNITY_6000_0_OR_NEWER
        return 2;
#elif UNITY_2022_3_OR_NEWER
        return 1;
#else
        return 0;
#endif
    }

    /// <summary>
    /// 환경 변수에서 int 값 읽기 (없으면 기본값 반환)
    /// </summary>
    private static int GetEnvInt(string name, int defaultValue)
    {
        string value = System.Environment.GetEnvironmentVariable(name);
        if (!string.IsNullOrEmpty(value) && int.TryParse(value, out int result))
        {
            return result;
        }
        return defaultValue;
    }

    /// <summary>
    /// 환경 변수에서 string 값 읽기 (없으면 기본값 반환)
    /// </summary>
    private static string GetEnvString(string name, string defaultValue)
    {
        string value = System.Environment.GetEnvironmentVariable(name);
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    /// <summary>
    /// 패키지 비임포트 원본(Runtime/Fonts~/NotoSansKR-Regular.otf)을 이 프로젝트의
    /// Assets/Resources/Fonts/ 로 복사한다. UPM/embedded 설치 모두에서 실경로를 해석하기 위해
    /// AITFontSubsetProcessor.ResolveToolSourceDir 와 동일한 관용구(PackageInfo.FindForAssembly
    /// resolvedPath 우선 → CallerFilePath 폴백)를 사용한다. 내용이 실제로 다를 때만 File.Copy 해
    /// 불필요한 재임포트/증분 캐시 무효화를 피한다(Editor/AITWebGLBuilder.cs 의
    /// VersionInfoAssetPath 와 동일 사유).
    ///
    /// 해석 실패(원본을 못 찾음) 시에도 빌드를 중단하지 않는다 — UIBuilder.DefaultFont 에
    /// LegacyRuntime.ttf 폴백이 있어 빌드 자체는 성공하지만 한글이 tofu 로 렌더링되므로 경고 로그를
    /// 눈에 띄게 남긴다.
    /// </summary>
    private static void EnsureFontsCopiedToResources()
    {
        try
        {
            string srcPath = ResolveFontsSourcePath();
            if (string.IsNullOrEmpty(srcPath) || !File.Exists(srcPath))
            {
                Debug.LogWarning(
                    "⚠ [EnsureFontsCopiedToResources] 원본 폰트를 찾지 못했습니다(Runtime/Fonts~/" +
                    $"{NotoSansKrFileName}). Assets/Resources/Fonts/ 로 복사를 건너뜁니다 — " +
                    "UIBuilder.DefaultFont 가 LegacyRuntime.ttf 로 폴백해 빌드는 계속되지만 한글이 " +
                    "깨져(tofu) 보일 수 있습니다.");
                return;
            }

            string destDir = Path.Combine("Assets", "Resources", "Fonts");
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            string destPath = Path.Combine(destDir, NotoSansKrFileName);
            if (!FilesAreIdentical(srcPath, destPath))
            {
                File.Copy(srcPath, destPath, overwrite: true);
            }

            string assetPath = "Assets/Resources/Fonts/" + NotoSansKrFileName;
            AssetDatabase.ImportAsset(
                assetPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            Debug.Log($"✓ 폰트 복사 완료: {srcPath} → {assetPath}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning(
                $"⚠ [EnsureFontsCopiedToResources] 폰트 복사 중 예외(빌드는 계속 진행, LegacyRuntime.ttf 폴백 가능): {e}");
        }
    }

    /// <summary>SharedScripts 패키지에 동봉된 원본 폰트 소스 경로. UPM/embedded 설치 모두 해석
    /// (AITFontSubsetProcessor.ResolveToolSourceDir 와 동일 관용구).</summary>
    private static string ResolveFontsSourcePath()
    {
        try
        {
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(E2EBuildRunner).Assembly);
            if (pkg != null && !string.IsNullOrEmpty(pkg.resolvedPath))
            {
                return Path.Combine(pkg.resolvedPath, "Runtime", FontsSourceDirName, NotoSansKrFileName);
            }
        }
        catch
        {
            // PackageInfo 미해석(Assets 내 임베드 개발) → 소스 파일 위치 폴백.
        }

        string here = CallerDir();
        return string.IsNullOrEmpty(here)
            ? null
            : Path.GetFullPath(Path.Combine(here, "..", "Runtime", FontsSourceDirName, NotoSansKrFileName));
    }

    private static string CallerDir([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
        => string.IsNullOrEmpty(thisFile) ? null : Path.GetDirectoryName(thisFile);

    /// <summary>두 파일의 길이+바이트가 완전히 같은지 비교한다(불필요한 재임포트/증분 캐시 무효화 방지).</summary>
    private static bool FilesAreIdentical(string pathA, string pathB)
    {
        if (!File.Exists(pathB))
        {
            return false;
        }

        var infoA = new FileInfo(pathA);
        var infoB = new FileInfo(pathB);
        if (infoA.Length != infoB.Length)
        {
            return false;
        }

        const int bufferSize = 1024 * 1024;
        using (var streamA = File.OpenRead(pathA))
        using (var streamB = File.OpenRead(pathB))
        {
            var bufferA = new byte[bufferSize];
            var bufferB = new byte[bufferSize];
            int readA;
            while ((readA = streamA.Read(bufferA, 0, bufferSize)) > 0)
            {
                int readB = streamB.Read(bufferB, 0, readA);
                if (readB != readA)
                {
                    return false;
                }

                for (int i = 0; i < readA; i++)
                {
                    if (bufferA[i] != bufferB[i])
                    {
                        return false;
                    }
                }
            }
        }

        return true;
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
