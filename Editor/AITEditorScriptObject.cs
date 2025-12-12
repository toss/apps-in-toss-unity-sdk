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
        [Tooltip("Mock 브릿지 사용 (로컬 테스트용, 네이티브 API 없이 동작)")]
        public bool enableMockBridge = false;

        [Tooltip("디버그 콘솔 활성화 (개발/테스트 목적)")]
        public bool enableDebugConsole = false;

        [Header("빌드 설정")]
        [Tooltip("Development Build 활성화 (빌드 속도 향상, 디버깅 편의)")]
        public bool developmentBuild = false;

        [Tooltip("LZ4 압축으로 빌드 속도 향상")]
        public bool enableLZ4Compression = true;

        [Tooltip("압축 포맷: -1 = 자동, 0 = Disabled, 1 = Gzip, 2 = Brotli")]
        public int compressionFormat = -1;

        [Tooltip("Managed Stripping Level: -1 = 자동 (High), 0 = Disabled, 1 = Minimal, 2 = Low, 3 = Medium, 4 = High")]
        public int managedStrippingLevel = -1;

        [Tooltip("디버그 심볼을 외부 파일로 분리 (빌드 크기 감소)")]
        public bool debugSymbolsExternal = true;

        /// <summary>
        /// Dev Server 기본 프로필 생성 (빌드 속도 우선)
        /// </summary>
        public static AITBuildProfile CreateDevServerProfile()
        {
            return new AITBuildProfile
            {
                enableMockBridge = true,
                enableDebugConsole = true,
                developmentBuild = true,
                enableLZ4Compression = true,
                compressionFormat = 0,  // Disabled - 빌드 속도 우선
                managedStrippingLevel = 1,  // Minimal - 빌드 속도 우선
                debugSymbolsExternal = false
            };
        }

        /// <summary>
        /// Production 기본 프로필 생성 (최적화 우선)
        /// Prod Server, Build & Package, Publish에서 공통으로 사용
        /// </summary>
        public static AITBuildProfile CreateProductionProfile()
        {
            return new AITBuildProfile
            {
                enableMockBridge = false,
                enableDebugConsole = false,
                developmentBuild = false,
                enableLZ4Compression = true,
                compressionFormat = -1,  // 자동 (Brotli)
                managedStrippingLevel = -1,  // 자동 (High)
                debugSymbolsExternal = true
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

        [Tooltip("WebView 타입. 게임앱은 'game' (투명배경), 일반앱은 'partner' (흰색배경), 외부연동은 'external'")]
        public int webViewType = 0; // 0=game, 1=partner, 2=external

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

        [Tooltip("Production 빌드 시 적용되는 빌드 설정 (Prod Server, Build & Package, Publish)")]
        public AITBuildProfile productionProfile = AITBuildProfile.CreateProductionProfile();

        [Header("WebGL 최적화 설정")]
        [Tooltip("-1 = 자동 (Unity 버전별 권장값)")]
        public int memorySize = -1;

        [Tooltip("-1 = 자동, 0 = false, 1 = true")]
        public int threadsSupport = -1;

        [Tooltip("-1 = 자동, 0 = false, 1 = true")]
        public int dataCaching = -1;

        public bool nameFilesAsHashes = true;

        [Header("IL2CPP/Stripping 설정")]
        public bool stripEngineCode = true;

        [Tooltip("-1 = 자동 (Release)")]
        public int il2cppConfiguration = -1;

        [Header("Unity 6 전용 설정")]
        [Tooltip("-1 = 자동 (HighPerformance)")]
        public int powerPreference = -1;

        public bool wasmStreaming = true;

        [Header("고급 설정 (주의: 변경 시 호환성 문제 발생 가능)")]
        [Tooltip("-1 = 자동 (ExplicitlyThrownExceptionsOnly)")]
        public int exceptionSupport = -1;

        [Tooltip("-1 = 자동 (false), Unity Pro 라이선스 필요")]
        public int showUnityLogo = -1;

        [Tooltip("-1 = 자동 (true)")]
        public int decompressionFallback = -1;

        [Tooltip("-1 = 자동 (false)")]
        public int runInBackground = -1;

        [Tooltip("-1 = 자동 (false, Unity 6+)")]
        public int webAssemblyArithmeticExceptions = -1;

        [Header("디버그 설정")]
        [Tooltip("프로덕션 빌드에서 디버그 콘솔을 활성화합니다. 개발/테스트 목적으로 사용하세요.")]
        public bool enableDebugConsole = false;

        [Header("권한 설정")]
        public AITPermissionConfig permissionConfig = new AITPermissionConfig();

        [Header("플러그인 설정")]
        public string[] plugins = new string[] { };

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
                case 2: return "external";
                default: return "game";
            }
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
        /// 버전별 기본 데이터 캐싱 여부
        /// - Unity 2021.3/2022.3: false
        /// - Unity 6+: true (UnityVersion.md:401)
        /// </summary>
        public static bool GetDefaultDataCaching()
        {
#if UNITY_2023_3_OR_NEWER
            return true;  // Unity 6에서만 활성화 권장
#else
            return false;
#endif
        }

        /// <summary>
        /// 버전별 기본 압축 포맷
        /// - Unity 2021.3: Gzip (UnityVersion.md:438)
        /// - Unity 2022.3+: Brotli (UnityVersion.md:391, 429)
        /// </summary>
        public static WebGLCompressionFormat GetDefaultCompressionFormat()
        {
#if UNITY_2022_3_OR_NEWER
            return WebGLCompressionFormat.Brotli;
#else
            return WebGLCompressionFormat.Gzip; // 2021.3 호환성
#endif
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
            return WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
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
