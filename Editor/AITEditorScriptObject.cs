using UnityEngine;
using UnityEditor;

namespace AppsInToss
{
    /// <summary>
    /// 빌드 프로필 설정
    /// Dev Server (개발용, 빌드 속도 우선)와 Production (배포용, 최적화 우선)으로 구분
    /// </summary>
    [System.Serializable]
    public class AITBuildProfile
    {
        [Header("런타임 설정")]
        [Tooltip("디버그 콘솔 활성화 (개발/테스트 목적)")]
        public bool enableDebugConsole = false;

        [Header("빌드 설정")]
        [Tooltip("Development Build 활성화 (빌드 속도 향상, 디버깅 편의)")]
        public bool developmentBuild = false;

        [Tooltip("LZ4 압축으로 빌드 속도 향상")]
        public bool enableLZ4Compression = true;

        [Tooltip("압축 포맷: -1 = 자동, 0 = Disabled, 1 = Gzip, 2 = Brotli")]
        public int compressionFormat = -1;

        [Tooltip("Managed Stripping Level: -1 = 자동 (High), 1 = Minimal, 2 = Low, 3 = Medium, 4 = High (0 = 레거시 Disabled, Minimal로 처리)")]
        public int managedStrippingLevel = -1;

        [Tooltip("디버그 심볼을 외부 파일로 분리 (빌드 크기 감소)")]
        public bool debugSymbolsExternal = true;

        /// <summary>
        /// 프로필의 얕은 복사본 생성 (모든 필드가 값 타입이므로 안전)
        /// </summary>
        public AITBuildProfile Clone() => (AITBuildProfile)MemberwiseClone();

        /// <summary>
        /// Dev Server 기본 프로필 생성 (빌드 속도 우선)
        /// </summary>
        public static AITBuildProfile CreateDevServerProfile()
        {
            return new AITBuildProfile
            {
                enableDebugConsole = true,
                developmentBuild = true,
                enableLZ4Compression = true,
                compressionFormat = 0,  // Disabled - 빌드 속도 우선
                managedStrippingLevel = -1,  // 자동 (High) - 실측상 Minimal보다 cold 빌드가 더 빠름(87.4s→42.6~47.4s,
                                              // SampleUnityProject-2022.3/Unity 2022.3.62f2/batchmode).
                                              // High 스트리핑이 IL2CPP로 넘어가는 관리 코드량을 줄여 UnityLinker가
                                              // 몇 초 더 쓰는 대신 IL2CPP 변환·컴파일과 직렬 emscripten 링크가 크게 줄어듦.
                                              // warm 빌드(무변경/1줄 수정)는 Minimal과 동등. 또한 Production·Deploy for
                                              // Online Test가 이미 High이므로 통일하면 스트리핑 관련 문제가 배포 단계가
                                              // 아니라 로컬 Dev Server 단계에서 가장 먼저 드러남.
                debugSymbolsExternal = false
            };
        }

        /// <summary>
        /// Production 기본 프로필 생성 (최적화 우선)
        /// Build & Package, Deploy for Online Test, Deploy Release Candidate에서 공통으로 사용
        /// </summary>
        public static AITBuildProfile CreateProductionProfile()
        {
            return new AITBuildProfile
            {
                enableDebugConsole = false,
                developmentBuild = false,
                enableLZ4Compression = true,
                compressionFormat = -1,  // 자동 (Brotli)
                managedStrippingLevel = -1,  // 자동 (High)
                debugSymbolsExternal = true
            };
        }

        /// <summary>
        /// Deploy for Online Test 전용 오버라이드 프로필 생성.
        /// baseProfile(보통 productionProfile)의 나머지 필드는 그대로 복사한 "새 인스턴스"에
        /// 압축 포맷/외부 디버그 심볼은 빌드 가속용 값으로, 디버그 콘솔은 실기기 디버깅용으로
        /// 덮어쓴다. Managed Stripping Level은 실측상 base(Production=High) 유지가 오히려
        /// 더 빠르므로 오버라이드하지 않는다(아래 참고).
        /// </summary>
        /// <remarks>
        /// baseProfile 인스턴스는 절대 변형하지 않는다 — config.productionProfile은 ScriptableObject로
        /// 영속화되므로 여기서 원본 필드를 바꾸면 사용자가 Configuration에서 설정한 Production 값이
        /// 디스크(AITConfig.asset)에 그대로 오염된다. 그래서 Clone() 뒤 필드를 대입하는 대신, object
        /// initializer로 완전히 새 인스턴스를 만들어 baseProfile을 읽기 전용으로만 사용한다.
        ///
        /// 오버라이드 값의 근거(저장값 컨벤션): AITBuildInitializer.ConvertToCompressionFormat 주석 —
        /// compressionFormat 1=Gzip. Production(-1=자동)은 Brotli로 변환되므로 Test만 Gzip으로 갈라진다.
        /// managedStrippingLevel은 한때 1(Minimal)로 가속을 노렸으나, 실측(SampleUnityProject-2022.3)에서
        /// High 스트리핑이 IL2CPP 변환·직렬 링크량을 줄여 cold 빌드가 오히려 ~15초 더 빨랐다(123→108s,
        /// UnityLinker 자체는 5→12s로 늘지만 상쇄됨) — "Minimal이 빌드 속도 우선"이라는 가정이 뒤집혀
        /// base 값을 그대로 물려받도록 되돌렸다. 부수 효과로 Production과 동일 스트리핑이 되어
        /// 배포 충실성(Test와 Production 간 산출물 차이) 격차도 줄어든다.
        /// </remarks>
        internal static AITBuildProfile CreateTestDeployProfile(AITBuildProfile baseProfile)
        {
            if (baseProfile == null)
                baseProfile = CreateProductionProfile();

            return new AITBuildProfile
            {
                enableDebugConsole = true,  // Deploy for Online Test는 실기기 디버깅 용도라 콘솔 활성화(Production은 base=false 유지). 템플릿 주입만 결정하므로 빌드 시간·캐시 영향 없음
                developmentBuild = baseProfile.developmentBuild,
                enableLZ4Compression = baseProfile.enableLZ4Compression,
                compressionFormat = 1,  // Gzip — Deploy for Online Test 가속: 압축 시간 단축(Production은 Brotli 유지)
                managedStrippingLevel = baseProfile.managedStrippingLevel,  // base(Production=High) 유지 — 실측상 High 스트리핑이 IL2CPP 변환·링크량을 줄여 cold 빌드가 Minimal보다 ~15초 빠르고(123→108s), Production과 동일 스트리핑이라 배포 충실성도 확보
                debugSymbolsExternal = false  // Embedded — Deploy for Online Test 가속: 외부 심볼 생성으로 늘어나는 emscripten 링크 시간 제거(Production은 base 값 유지)
            };
        }
    }

    /// <summary>
    /// 권한 설정 구성
    /// 문서: https://developers-apps-in-toss.toss.im/bedrock/reference/framework/권한/permission.html
    /// </summary>
    [System.Serializable]
    public class AITPermissionConfig
    {
        [Header("Clipboard")]
        [Tooltip("클립보드 읽기 권한")]
        public bool clipboardRead = false;

        [Tooltip("클립보드 쓰기 권한")]
        public bool clipboardWrite = false;

        [Header("Contacts")]
        [Tooltip("연락처 읽기 권한 (read only)")]
        public bool contacts = false;

        [Header("Photos")]
        [Tooltip("사진 앨범 읽기 권한 (read only)")]
        public bool photos = false;

        [Header("Camera")]
        [Tooltip("카메라 접근 권한 (access only)")]
        public bool camera = false;

        [Header("Geolocation")]
        [Tooltip("위치 정보 접근 권한 (access only)")]
        public bool geolocation = false;
    }

    /// <summary>
    /// Devtools(@apps-in-toss/devtools) 설정 — Dev Server 브라우저에서 SDK API를 Mock으로 동작시키는
    /// vite unplugin/패널/MCP 옵션. 빌드 산출물에 영향을 주지 않으므로(server-only) 빌드 프로필이 아닌
    /// config 직속으로 둔다.
    ///
    /// 주의(zero-fill 함정): Unity는 중첩 [Serializable] 클래스를 역직렬화할 때 생성자/필드 초기화식을
    /// 실행하지 않고 필드를 zero-fill한다. devtools 블록이 없는 구버전 AITConfig.asset을 로드하면
    /// 이 클래스가 통째로 zero-fill되므로, enabled/panel을 긍정형 필드로 두면 false로 굳어 devtools가
    /// 조용히 꺼진다. 그래서 직렬화 필드는 부정형(disableMock/hidePanel)으로 두어 zero-fill(false)이
    /// 곧 "기본 활성"이 되게 하고, 공개 API(enabled/panel)는 이를 반전한 프로퍼티로 노출한다.
    /// </summary>
    [System.Serializable]
    public class AITDevtoolsSettings
    {
        [SerializeField]
        [Tooltip("Dev Server에서 devtools mock(브라우저 로컬 SDK 모의 동작)을 사용하지 않습니다. 기본값 false(=mock 사용) — zero-fill 안전")]
        private bool disableMock = false;

        [SerializeField]
        [Tooltip("devtools 플로팅 패널(Mock 상태 제어 UI)을 숨깁니다. 기본값 false(=패널 표시) — zero-fill 안전")]
        private bool hidePanel = false;

        [Tooltip("AI 에이전트가 제어할 수 있는 MCP 엔드포인트를 엽니다")]
        public bool mcp = false;

        /// <summary>
        /// Dev Server에서 devtools mock(브라우저 로컬 SDK 모의 동작) 사용 여부.
        /// 직렬화 필드 disableMock을 반전해 노출한다(zero-fill = 기본 true).
        /// </summary>
        public bool enabled
        {
            get => !disableMock;
            set => disableMock = !value;
        }

        /// <summary>
        /// devtools 플로팅 패널(Mock 상태 제어 UI) 표시 여부.
        /// 직렬화 필드 hidePanel을 반전해 노출한다(zero-fill = 기본 true).
        /// </summary>
        public bool panel
        {
            get => !hidePanel;
            set => hidePanel = !value;
        }
    }

    /// <summary>
    /// Apps in Toss Editor 설정 오브젝트
    /// </summary>
    [System.Serializable]
    public class AITEditorScriptObject : ScriptableObject
    {
        [Header("앱 기본 정보")]
        public string appName = "";
        public string displayName = "";
        public string version = "0.0.1";
        public string description = "";

        [Header("브랜드 설정")]
        public string primaryColor = "#3182F6";
        public string iconUrl = "";

        [Header("WebView 설정")]
        [Tooltip("브릿지 색상 모드. 게임앱은 'inverted' (다크모드), 일반앱은 'basic'")]
        public int bridgeColorMode = 0; // 0=inverted (게임 기본), 1=basic

        [Tooltip("WebView 타입. 게임앱은 'game' (투명배경), 일반앱은 'partner' (흰색배경)")]
        public int webViewType = 0; // 0=game, 1=partner

        [Tooltip("상단 네비게이션 바 투명 배경. game 타입에서 풀스크린(노치/상단 영역까지 그리기)에 필요")]
        public bool navigationBarTransparentBackground = true; // 기본 ON (game 풀스크린 복구)

        [Tooltip("네비게이션 바 테마. 0=기본(미지정), 1=light, 2=dark")]
        public int navigationBarTheme = 0; // 0=기본(미지정), 1=light, 2=dark

        [Tooltip("인라인 미디어 재생 허용")]
        public bool allowsInlineMediaPlayback = false;

        [Tooltip("미디어 재생 시 사용자 액션 필요")]
        public bool mediaPlaybackRequiresUserAction = false;

        [Header("서버 설정")]
        [Tooltip("Granite (Metro) 서버 호스트. 기본값: 0.0.0.0")]
        public string graniteHost = "0.0.0.0";

        [Tooltip("Granite (Metro) 서버 포트. 기본값: 8081")]
        public int granitePort = 8081;

        [Tooltip("Vite 서버 호스트. 기본값: localhost")]
        public string viteHost = "localhost";

        [Tooltip("Vite 서버 포트. 기본값: 5173")]
        public int vitePort = 5173;

        [Header("빌드 출력 설정")]
        [Tooltip("granite build 출력 디렉토리. 기본값: dist")]
        public string outdir = "dist";

        [Header("빌드 프로필")]
        [Tooltip("Dev Server 실행 시 적용되는 빌드 설정 (빌드 속도 우선)")]
        public AITBuildProfile devServerProfile = AITBuildProfile.CreateDevServerProfile();

        [Tooltip("Production 빌드 시 적용되는 빌드 설정 (Build & Package, Deploy for Online Test, Deploy Release Candidate)")]
        public AITBuildProfile productionProfile = AITBuildProfile.CreateProductionProfile();

        [Header("WebGL 최적화 설정")]
        [Tooltip("WebGL 초기 힙 메모리 크기(MB). 런타임에 필요 시 자동 확장됩니다. -1 = 자동 (Unity 버전별 권장값)")]
        public int memorySize = -1;

        [Tooltip("-1 = 자동, 0 = false, 1 = true")]
        public int threadsSupport = -1;

        [Tooltip("-1 = 자동, 0 = false, 1 = true")]
        public int dataCaching = -1;

        public bool nameFilesAsHashes = true;

        [Header("렌더링 품질 설정")]
        [Tooltip("devicePixelRatio 설정: -1 = auto (기기 성능에 따라 자동 결정), 1/2/3 = 고정값. 높을수록 고품질이지만 GPU 부하 증가")]
        public int devicePixelRatio = -1;

        [Header("IL2CPP/Stripping 설정")]
        public bool stripEngineCode = true;

        [Tooltip("-1 = 자동 (Release)")]
        public int il2cppConfiguration = -1;

        [Header("Unity 6 전용 설정")]
        [Tooltip("-1 = 자동 (HighPerformance)")]
        public int powerPreference = -1;

        public bool wasmStreaming = true;

        [Header("고급 설정 (주의: 변경 시 호환성 문제 발생 가능)")]
        [Tooltip("-1 = 자동 (FullWithStacktrace, Sentry 경고 방지)")]
        public int exceptionSupport = -1;

        [Tooltip("-1 = 자동 (false), Unity Pro 라이선스 필요")]
        public int showUnityLogo = -1;

        [Tooltip("-1 = 자동 (true)")]
        public int decompressionFallback = -1;

        [Tooltip("-1 = 자동 (false)")]
        public int runInBackground = -1;

        [Tooltip("-1 = 자동 (false, Unity 6+)")]
        public int webAssemblyArithmeticExceptions = -1;

        [Header("빌드 전 검사 설정")]
        [Tooltip("빌드 전 에셋 최적화 검사를 활성화합니다")]
        public bool enableBuildOptimizationCheck = true;

        [Header("계측 설정")]
        [Tooltip("first-interactive 계측: -1 = 자동 (활성), 0 = 비활성, 1 = 활성")]
        public int firstInteractiveLog = -1;

        [Header("스토리지 설정")]
        [Tooltip("PlayerPrefs 영속화 (앱인토스 Storage): -1 = 자동 (활성), 0 = 비활성, 1 = 활성")]
        public int playerPrefsPersistence = -1;

        [Header("권한 설정")]
        public AITPermissionConfig permissionConfig = new AITPermissionConfig();

        [Header("Devtools 설정")]
        public AITDevtoolsSettings devtools = new AITDevtoolsSettings();

        /// <summary>
        /// 아이콘 URL 유효성 검사
        /// </summary>
        public bool IsIconUrlValid()
        {
            return !string.IsNullOrWhiteSpace(iconUrl) &&
                   (iconUrl.StartsWith("http://") || iconUrl.StartsWith("https://"));
        }

        /// <summary>
        /// 앱 ID 유효성 검사
        /// </summary>
        public bool IsAppNameValid()
        {
            if (string.IsNullOrWhiteSpace(appName))
                return false;

            // 영문, 숫자, 하이픈만 허용
            foreach (char c in appName)
            {
                if (!char.IsLetterOrDigit(c) && c != '-')
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 버전 형식 검사 (x.y.z)
        /// </summary>
        public bool IsVersionValid()
        {
            if (string.IsNullOrWhiteSpace(version))
                return false;

            string[] parts = version.Split('.');
            if (parts.Length != 3)
                return false;

            foreach (string part in parts)
            {
                if (!int.TryParse(part, out _))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 배포 준비 완료 여부 (기본 설정만 체크, deploymentKey는 AITCredentials에서 별도 확인)
        /// </summary>
        public bool IsReadyForDeploy()
        {
            return IsIconUrlValid() &&
                   IsAppNameValid() &&
                   IsVersionValid();
        }

        /// <summary>
        /// bridgeColorMode 문자열 반환
        /// </summary>
        public string GetBridgeColorModeString()
        {
            return bridgeColorMode == 0 ? "inverted" : "basic";
        }

        /// <summary>
        /// webViewProps.type 문자열 반환
        /// </summary>
        public string GetWebViewTypeString()
        {
            switch (webViewType)
            {
                case 0: return "game";
                case 1: return "partner";
                default: return "game";
            }
        }

        /// <summary>
        /// navigationBar 옵션을 granite.config.ts/apps-in-toss.config.ts 형식의 TS 객체 리터럴로 반환.
        /// webViewType=game일 때만 transparentBackground/theme를 emit하고(하위호환), partner면 빈 객체를
        /// 반환해 기존 동작과 USER_CONFIG의 navigationBar를 보존한다.
        /// 형식 예: { transparentBackground: true, theme: 'dark' }
        /// </summary>
        public string GetNavigationBarJson()
        {
            // partner(비게임)는 동작 불변 — 빈 객체로 emit (USER_CONFIG의 navigationBar 보존)
            if (webViewType != 0)
                return "{}";

            var parts = new System.Collections.Generic.List<string>
            {
                "transparentBackground: " + navigationBarTransparentBackground.ToString().ToLower()
            };
            if (navigationBarTheme == 1)
                parts.Add("theme: 'light'");
            else if (navigationBarTheme == 2)
                parts.Add("theme: 'dark'");

            return "{ " + string.Join(", ", parts) + " }";
        }

        /// <summary>
        /// permissionConfig를 granite.config.ts 형식의 JSON 배열로 변환
        /// 형식: [{ name: 'geolocation', access: 'access' }, ...]
        /// </summary>
        public string GetPermissionsJson()
        {
            if (permissionConfig == null)
                return "[]";

            var objects = new System.Collections.Generic.List<string>();

            // Clipboard
            if (permissionConfig.clipboardRead)
                objects.Add("{ name: 'clipboard', access: 'read' }");
            if (permissionConfig.clipboardWrite)
                objects.Add("{ name: 'clipboard', access: 'write' }");

            // Contacts (read only)
            if (permissionConfig.contacts)
                objects.Add("{ name: 'contacts', access: 'read' }");

            // Photos (read only)
            if (permissionConfig.photos)
                objects.Add("{ name: 'photos', access: 'read' }");

            // Camera (access only)
            if (permissionConfig.camera)
                objects.Add("{ name: 'camera', access: 'access' }");

            // Geolocation (access only)
            if (permissionConfig.geolocation)
                objects.Add("{ name: 'geolocation', access: 'access' }");

            return "[" + string.Join(", ", objects) + "]";
        }
    }

    /// <summary>
    /// Unity 버전별 기본 설정값을 제공하는 클래스
    /// 출처: apps-in-toss-unity-docs/Design/UnityVersion.md
    /// </summary>
    public static class AITDefaultSettings
    {
        /// <summary>
        /// 버전별 기본 메모리 크기 (MB)
        /// - Unity 2021.3: 256MB (UnityVersion.md:439)
        /// - Unity 2022.3: 512MB (UnityVersion.md:430)
        /// - Unity 6/2023.3: 1024MB (UnityVersion.md:392)
        /// - Unity 2024.2: 1536MB (UnityVersion.md:415)
        /// </summary>
        public static int GetDefaultMemorySize()
        {
#if UNITY_2024_2_OR_NEWER
            return 1536;  // AI 모델용 대용량 메모리
#elif UNITY_2023_3_OR_NEWER
            return 1024;  // Unity 6
#elif UNITY_2022_3_OR_NEWER
            return 512;   // Unity 2022.3
#else
            return 256;   // Unity 2021.3 (호환성 우선)
#endif
        }

        /// <summary>
        /// 버전별 기본 스레딩 지원 여부
        /// - Unity 2021.3/2022.3: false (UnityVersion.md:432 "브라우저 호환성")
        /// - Unity 6+: true (UnityVersion.md:394 "향상된 멀티스레딩")
        /// </summary>
        public static bool GetDefaultThreadsSupport()
        {
            // WebGL 멀티스레딩은 COOP/COEP 헤더가 필요하여 배포 환경에 따라 문제 발생 가능
            // 필요한 경우 사용자가 직접 활성화하도록 기본값은 false
            return false;
        }

        /// <summary>
        /// 기본 데이터 캐싱 여부
        /// 베타 기능 미공개 상태라 전 버전 비활성화 — 플랫폼(WebView) 캐시 정책 검증 완료 후
        /// 공개 시 Unity 6+ 기본 활성화(UnityVersion.md:401) 재검토
        /// </summary>
        public static bool GetDefaultDataCaching()
        {
            return false;
        }

        /// <summary>
        /// 기본 압축 포맷: Brotli
        /// decompressionFallback이 활성화되어 있으므로 모든 Unity 버전에서 Brotli 사용 가능
        /// </summary>
        public static WebGLCompressionFormat GetDefaultCompressionFormat()
        {
            return WebGLCompressionFormat.Brotli;
        }

        /// <summary>
        /// 기본 Managed Stripping Level
        /// 출처: StartupOptimization.md:89
        /// </summary>
        public static ManagedStrippingLevel GetDefaultManagedStrippingLevel()
        {
            return ManagedStrippingLevel.High;
        }

        /// <summary>
        /// 기본 IL2CPP 컴파일러 설정
        /// 출처: StartupOptimization.md:85
        /// </summary>
        public static Il2CppCompilerConfiguration GetDefaultIl2CppConfiguration()
        {
            return Il2CppCompilerConfiguration.Release;
        }

#if UNITY_2023_3_OR_NEWER
        /// <summary>
        /// Unity 6 전용: 기본 전력 설정
        /// 출처: UnityVersion.md:396
        /// </summary>
        public static WebGLPowerPreference GetDefaultPowerPreference()
        {
            return WebGLPowerPreference.HighPerformance;
        }
#endif

        /// <summary>
        /// 기본 예외 처리 모드
        /// 출처: UnityVersion.md:393, 431
        /// </summary>
        public static WebGLExceptionSupport GetDefaultExceptionSupport()
        {
            // Sentry/에러 추적 SDK가 stack trace를 캡처하려면 FullWithStacktrace 필요.
            // Unity 기본값(ExplicitlyThrownExceptionsOnly)을 올려서 Sentry의 런타임 경고 제거.
            return WebGLExceptionSupport.FullWithStacktrace;
        }

        /// <summary>
        /// 기본 Unity 로고 표시 여부
        /// Unity Pro 라이선스가 있으면 false, 없으면 true (필수)
        /// </summary>
        public static bool GetDefaultShowUnityLogo()
        {
            // Unity Pro가 아니면 로고 표시 필수
            return !UnityEditorInternal.InternalEditorUtility.HasPro();
        }

        /// <summary>
        /// 기본 Decompression Fallback
        /// 출처: StartupOptimization.md:93
        /// </summary>
        public static bool GetDefaultDecompressionFallback()
        {
            return true;
        }

        /// <summary>
        /// 기본 Run In Background
        /// 모바일 환경에서는 false 권장
        /// </summary>
        public static bool GetDefaultRunInBackground()
        {
            return false;
        }

#if UNITY_2023_3_OR_NEWER && !UNITY_6000_0_OR_NEWER
        /// <summary>
        /// Unity 2023.3 전용: 기본 WASM 산술 예외 처리
        /// Unity 6000에서는 이 설정이 제거됨
        /// 출처: UnityVersion.md:397
        /// </summary>
        public static bool GetDefaultWebAssemblyArithmeticExceptions()
        {
            return false;
        }
#endif

        /// <summary>
        /// first-interactive 계측 기본 활성 여부.
        /// 픽셀 불변·세션당 1회 단일 이벤트·호스트 로딩 지표 표준화에 해당하므로 기본 ON.
        /// </summary>
        public static bool GetDefaultFirstInteractiveLog()
        {
            return true;
        }

        /// <summary>
        /// PlayerPrefs 영속화(앱인토스 Storage) 기본 활성 여부.
        /// IndexedDB 영속성이 보장되지 않는 웹뷰 환경에서 게임 코드 수정 없이
        /// PlayerPrefs 데이터를 보호하기 위해 기본 ON.
        /// </summary>
        public static bool GetDefaultPlayerPrefsPersistence()
        {
            return true;
        }

        /// <summary>
        /// 현재 Unity 버전의 버전 그룹 이름 반환
        /// </summary>
        public static string GetUnityVersionGroup()
        {
#if UNITY_2024_2_OR_NEWER
            return "Unity 2024.2+";
#elif UNITY_2023_3_OR_NEWER
            return "Unity 6 (2023.3+)";
#elif UNITY_2022_3_OR_NEWER
            return "Unity 2022.3 LTS";
#else
            return "Unity 2021.3 LTS";
#endif
        }
    }
}
