using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor.ErrorTracker
{
    /// <summary>
    /// Error Tracker 메인 오케스트레이터.
    /// 에러 캡처, 세션 관리, 텔레메트리 전송을 총괄합니다.
    /// </summary>
    [InitializeOnLoad]
    internal static class AITEditorErrorTracker
    {
        #region Constants

        // 빈 문자열이면 에러 트래킹이 비활성화됩니다.
        // DSN의 public key는 클라이언트 측에서 사용하도록 설계되어 있으며 (Sentry 공식 문서 참조),
        // Sentry 측 inbound filter와 rate limiting으로 보호됩니다.
        private const string DEFAULT_DSN = "https://af6caf8b80107bc41edf37baff728a5d@o89496.ingest.us.sentry.io/4511182309359616";
        private const string RELEASE_PREFIX = "apps-in-toss.unity";
        private const string ENVIRONMENT = "editor";
        private const int MAX_BREADCRUMBS = 20;
        private const int MAX_DEDUP_ENTRIES = 200;
        private static readonly TimeSpan DedupWindow = TimeSpan.FromMinutes(1);

        #endregion

        #region AIT Keywords

        private static readonly string[] AitKeywords =
        {
            "[AIT",
            "AIT:",
            "AppsInToss",
            "apps-in-toss",
            "ait-build",
            "AITConvertCore",
            "AITPackageBuilder",
            "AITNodeJS",
            "AITNpmRunner"
        };

        // Dev/Production Server 프로세스에서 리디렉션된 로그의 prefix 패턴.
        // 이 로그는 granite dev 프로세스의 stdout/stderr를 Unity Console로 전달한 것이며,
        // SDK 자체 에러가 아니므로 Sentry 캡처에서 제외해야 합니다.
        private static readonly string[] ServerLogPrefixes =
        {
            "[Dev Server]",
            "[Production Server]"
        };

        // 100% SDK와 무관한 Unity 내부/사용자 프로젝트 메시지 패턴.
        // IsAitRelated를 통과한 메시지 중에서도 이 패턴이 매칭되면 캡처 대상에서 제외.
        //
        // 새 노이즈 패턴 추가 워크플로우:
        //   1. Sentry에서 해당 이슈가 SDK 변경 없이 재현되는지 확인 (사용자 프로젝트/Unity 내부)
        //   2. Sentry에서 해당 이슈를 ignored 처리
        //   3. 여기 NonSdkMessagePatterns에 메시지의 불변 핵심 문구를 부분 문자열로 추가
        //      (Unity 버전/환경 차이에 민감한 부분은 피할 것)
        //      단, 단일 부분 문자열이 SDK 자체 로그와 충돌할 위험이 있으면
        //      IsKnownNonSdkMessage 내에 composite AND 조건(예: "GUID [" && "conflicts with:")을 추가
        //   4. IsKnownNonSdkMessageTests.cs에 positive/negative 테스트 추가
        //      (특히 AIT 키워드가 섞여도 필터링되지 않는지 negative 케이스 필수)
        private static readonly string[] NonSdkMessagePatterns =
        {
            // Unity 내부 경고
            "GfxDevice renderer is null",
            "Ignoring locale ",
            "Unable to load build report at Library/",
            "Cannot read BuildLayout header",
            "[ServicesCore]",
            "ProfileValueReference: GetValue called with empty id",
            "The Editor does not support 32-bit plugins",
            // Unity 라이프사이클 경고 — 사용자 MonoBehaviour의 Awake/OnValidate에서 발생
            "SendMessage cannot be called during Awake, CheckConsistency, or OnValidate",
            // Unity Animator — 사용자 프로젝트의 레거시 클립 사용
            "Legacy AnimationClips are not allowed in Animator Controllers",
            // ↑ 동일 경고의 첫 줄 변형 — Unity 출력 포맷("cannot be used in the State \"X\".")과 밀착시키려
            // 따옴표를 포함해 좁힘 (SDK-KV/KT)
            "cannot be used in the State \"",
            // Unity 빌드 — 미컴파일 코드 변경 상태에서 빌드 시도 시 Unity가 직접 출력 (SDK-NE)
            "You are building a player, but you have uncompiled code changes",
            // Unity 빌드 — 사용자가 Development 옵션 없이 ConnectWithProfiler를 설정해 발생하는 Unity 표준 예외 (SDK-T3)
            // 사용자 BuildPlayerOptions 구성 문제이며 SDK 버그 아님. Unity 버전/언어와 무관하게 영문 메시지 본문이 동일.
            "Non-development build cannot allow auto-connecting the profiler",

            // 사용자 프로젝트 에셋 문제
            "matches more than one built-in atlases",
            "Warnings during import of AudioClip",
            // FMOD/오디오 — 사용자 에셋 import/포맷 문제
            "Cannot create FMOD::Sound instance for clip",
            "Failed getting load state of FSB for audio clip",
            "Cannot load audio data for audio clip",
            // Animator — 사용자 컨트롤러 설정 누락
            "doesn't have an Exit Time or any condition",

            // Unity 패키지 내부
            "Localization-String-Tables-",
            "Warning in Graph at Packages/com.unity",

            // 사용자 프로젝트 직렬화 ([Assembly-CSharp] 한정 — SDK 어셈블리의 직렬화 경고는 보호)
            "Fields serialized in [Assembly-CSharp]",
            // [Assembly-CSharp] 타입의 player/editor 직렬화 mismatch (AdSwitcher 등 사용자 코드)
            "Type '[Assembly-CSharp]",
            // 사용자 게임 코드의 player script 컴파일 실패 (스택 없이 메시지만 도착)
            "Failed to compile player scripts",
            // 사용자 코드의 사용되지 않은 필드 경고 — Unity 컴파일러가 직접 출력하는 CS0414
            "warning CS0414",

            // 외부 패키지 (Unity 버전별 괄호 유무에 관계없이 매칭되도록 핵심 문구만 추출)
            "exists but its folder",

            // 위 "exists but its folder"의 에셋 변형 — 사용자가 Unity 외부에서 자산을 이동/삭제해
            // .meta만 고아로 남은 경우 Unity 에디터가 직접 출력하는 표준 경고.
            // 예: "A meta data file (.meta) exists but its asset 'Assets/.../Foo.cs' can't be found.
            //      When moving or deleting files outside of Unity, please ensure that the corresponding
            //      .meta file is moved or deleted along with it."
            // 자산 경로(Assets/ 또는 Packages/...)가 가변이라 불변 핵심 문구만 추출.
            // SDK는 이 문구를 출력하지 않으므로(grep 확인) AitKeywords 보호 가드와 충돌 없음.
            // Sentry APPS-IN-TOSS-UNITY-SDK-ZS, APPS-IN-TOSS-UNITY-SDK-ZQ.
            "exists but its asset",

            // 외부 UPM 패키지(immutable 폴더)의 에셋에 .meta 파일이 없을 때 Unity 에디터가 직접 출력하는 표준 경고.
            // 외부 서드파티 패키지(예: com.lupidan.apple-signin-unity)가 .meta를 누락한 채 배포되어 발생.
            // 예: "Asset 'Packages/com.lupidan.apple-signin-unity/AppleAuthSampleProject/ProjectSettings/...'
            //      has no meta file, but it's in an immutable folder. The asset will be ignored."
            // Sentry APPS-IN-TOSS-UNITY-SDK-10D, 10E, 10F, 10G, 10H, 10J, 10K.
            // immutable 폴더(외부 패키지)의 누락 .meta는 사용자가 조치 불가한 Unity 자체 노이즈이며,
            // 에셋 경로/패키지명이 가변이므로 불변 핵심 문구만 추출. SDK는 이 문구를 출력하지 않으므로(AitKeywords 미포함) 보호 가드와 충돌 없음.
            "has no meta file, but it's in an immutable folder",

            // Unity URP 내부
            "exceeds previous array size",

            // 사용자 Addressable 설정
            "does not have any associated AddressableAssetGroupSchemas",
            // 사용자 Addressables 콘텐츠 빌드 실패 — Unity Addressables 시스템 자체 오류 (SDK-H2)
            "Addressable content build failure",

            // 사용자 Unity 설치에 WebGL 모듈 미설치 — SDK-DD
            "Build target 'WebGL' not supported",

            // Unity AssetImporter 내부 워커/메인 에러 — SDK 외부 출처 (Unity 자체 에셋 임포트 경고).
            // 워커 prefix가 있는 변형: "[Worker2] Import Error Code:(4)" (SDK-RC/RD/RE)
            // prefix 없이 UnityWarning으로 래핑된 변형: "UnityWarning: Import Error Code:(4)" (SDK-V7)
            // prefix 유무·워커 번호·코드 숫자가 모두 가변이므로 공통 핵심 문구 "Import Error Code:(" 로 일반화.
            // SDK는 이 문자열을 출력하지 않으므로(AitKeywords에 없음) 보호 가드와 충돌 없음.
            "Import Error Code:(",

            // 서브프로세스 실행 브레드크럼 — SDK가 아닌 외부(Unity Collab/CCD/사용자 도구)가 출력
            // 예: "Exec> git show -s --pretty=%D HEAD", "Exec> git log -1 --pretty=format:%h"
            // Sentry APPS-IN-TOSS-UNITY-SDK-NX, APPS-IN-TOSS-UNITY-SDK-NW
            "Exec> git ",

            // Unity 엔진 자체 경고 — WebGL은 IL2CPP "Method Name, File Name, Line Number" 스택트레이스 옵션 미지원 (SDK-8B)
            "IL2CPP stack traces is not supported on WebGL",

            // 사용자 게임 코드(외부 IAP 모듈)가 출력하는 진단. SDK 코드에 'Toss IAP' 문자열은 없음.
            // 사용자 환경(상품 구성 누락, 백엔드 연결 실패) 원인이며 SDK 분기로 해결 불가.
            // Sentry APPS-IN-TOSS-UNITY-SDK-CY.
            "Toss IAP: Initialize failed or no products",

            // 사용자 프로젝트 .meta 파일 GUID 손상 — Unity 자체 노이즈
            // 예: "The .meta file Assets/.../foo.png.meta does not have a valid GUID..."
            //     "The GUID inside 'Assets/.../foo.png.meta' cannot be extracted by the YAML Parser..."
            // Sentry APPS-IN-TOSS-UNITY-SDK-R4, APPS-IN-TOSS-UNITY-SDK-R3
            // 두 번째 패턴은 작은따옴표를 포함시켜 .meta 경로 형식의 메시지에만 매칭되도록 좁힘
            // (다른 YAML 에셋 파싱 오류 메시지와 충돌 방지).
            "does not have a valid GUID",
            "' cannot be extracted by the YAML Parser",

            // pnpm stdout/stderr 패스스루 노이즈 — Unity가 외부 pnpm 프로세스 출력을 래핑한 라인.
            // "[pnpm] 출력:"(SDK-HA, SDK-R6)은 stdout, "[pnpm] 오류:"(SDK-VF, SDK-VA)는 stderr를
            // Unity가 UnityWarning으로 래핑한 것. 둘 다 SDK 외부(pnpm 프로세스) 출처.
            // AitKeywords에 "[pnpm]"이 없어 SDK 보호 가드는 우회되며,
            // SDK 자체 로그는 "[AIT...]" prefix와 함께 출력되므로 보호된다.
            "[pnpm] 출력:",
            "[pnpm] 오류:",

            // 사용자 코드 using 중복 — Unity 컴파일러가 직접 출력하는 CS0105.
            // 예: "warning CS0105: The using directive for 'AppsInToss' appeared previously in this namespace"
            // Sentry APPS-IN-TOSS-UNITY-SDK-SW.
            // SDK는 컴파일러 경고 메시지를 직접 출력하지 않으므로 안전.
            "warning CS0105",

            // 사용자 MonoBehaviour 라이프사이클 위반 — Unity 엔진이 OnValidate/animation event 등에서
            // 즉시 파괴를 시도할 때 직접 출력. 사용자 게임 코드 호출 흐름.
            // Sentry APPS-IN-TOSS-UNITY-SDK-T0.
            "Destroying GameObjects immediately is not permitted during",

            // Unity IL2CPP / Bee 빌드 시스템이 직접 출력하는 컴파일 단위별 빌드 실패.
            // .o 해시 파일명이 매번 달라 Sentry에서 동일 빌드 실패가 수십 개 별도 이슈로 grouping 됨.
            // 예: "Building Library/Bee/artifacts/WebGL/GameAssembly/release_WebGL_wasm/abcdef.o failed with output:"
            //     "Building Library/Bee/artifacts/WebGL/ManagedStripped failed with output:"
            //     "Building Library/Bee/artifacts/WebGL/build/debug_WebGL_wasm/build.js failed with output:"
            // Sentry APPS-IN-TOSS-UNITY-SDK-SA~SV, T7, TV 등 다수.
            // 실 SDK 빌드 실패는 "[AIT] WebGL 빌드가 실패했습니다." (SDK-8E)로 별도 캡처되므로 안전.
            "Building Library/Bee/artifacts/WebGL/",

            // git 호출 trace의 cmd wrapper 변형 — 기존 "Exec> git " 패턴이 잡지 못하는 형태.
            // 예: "Exec> cmd /c \"git\" show -s --pretty=%D HEAD", "Exec> cmd /c \"git\" log -1 ..."
            // Sentry APPS-IN-TOSS-UNITY-SDK-TE, APPS-IN-TOSS-UNITY-SDK-TF.
            "Exec> cmd /c \"git\"",

            // Unity Addressables / ScriptableBuildPipeline이 직접 출력하는 빌드 실패 메시지.
            // 사용자 프로젝트의 Addressables 그룹/스키마 설정 문제이며 SDK 영역 아님.
            // 예: "SBP ErrorError" (SDK-H4), "BuildFailedException: Failed to build Addressables content..." (SDK-S4)
            // Addressables는 동일 메시지를 다양한 prefix로 반복 출력하므로 핵심 토큰만 매칭.
            "SBP ErrorError",
            "Failed to build Addressables content",
            // Cannot read BuildLayout의 변형 — "BuildLayout has not open for a file" (SDK-EX)
            "BuildLayout has not open",

            // Unity 오디오 시스템 — 빌드/플레이 중 오디오 장치 전환 발생 시 출력 (SDK-TW)
            "Default audio device was changed",

            // Unity emscripten 압축 단계 직접 출력 — SDK 코드가 띄우는 메시지가 아님 (SDK-RV)
            // 예: "Building webgl/Build/204ccce7cc46e2cd9bd7212e664b4738.data.unityweb failed with output:"
            // 실 SDK 빌드 실패는 "[AIT] WebGL 빌드가 실패했습니다." (SDK-8E)로 별도 캡처되므로 안전.
            "Building webgl/Build/",

            // 사용자 프로젝트가 사용하는 다른 WebGL 템플릿(Fill 등)이 출력하는 진단. SDK 영역 아님.
            // 예: "[WebGL] unity-webview.js source not found: /Users/.../Assets/WebGLTemplates/Fill/TemplateData/unity-webview.js"
            //     "UnityWarning: [WebGL] unity-webview.js source not found: /Users/.../unity-webview.js"
            // Sentry APPS-IN-TOSS-UNITY-SDK-VJ ("UnityWarning: " prefix 변형 포함 — 부분 문자열 매칭).
            // SDK는 "[WebGL]" prefix를 출력하지 않으므로(grep 확인) AitKeywords 보호 가드와 충돌 없음.
            "[WebGL] unity-webview.js source not found",

            // Unity Addressables linker 누락 — 사용자 프로젝트의 Addressables 그룹 설정 문제. SDK 영역 아님.
            // 예: "BuildFailedException: Missing Addressables linker file. ..."
            // Sentry APPS-IN-TOSS-UNITY-SDK-QE.
            // SDK는 "Addressables linker" 문자열을 출력하지 않으므로(grep 확인) 안전.
            "Missing Addressables linker",

            // Unity AssetDatabase가 import 중 SaveAssets 호출 시 직접 출력 — 사용자 코드/플러그인이 import 중에 SaveAssets를 호출.
            // 예: "Calls to \"AssetDatabase.SaveAssets\" are restricted during asset importing."
            // Sentry APPS-IN-TOSS-UNITY-SDK-P4.
            // SDK는 이 문자열을 출력하지 않으므로(grep 확인) 안전.
            "\"AssetDatabase.SaveAssets\" are restricted during asset importing",

            // Unity AssetDatabase Refresh 루프 중 발생 — 사용자 프로젝트의 import 충돌. Unity 자체 진단.
            // 예: "The asset at ProjectSettings/ProjectSettings.asset has been scheduled for reimport during the Refresh loop ..."
            // Sentry APPS-IN-TOSS-UNITY-SDK-P6.
            // SDK는 이 문자열을 출력하지 않으므로(grep 확인) 안전.
            "scheduled for reimport during the Refresh loop",

            // Unity PackageManager가 immutable 패키지 변경 감지 시 직접 출력 — SDK 자동 업데이트 또는 사용자 변경.
            // 예: "The following asset(s) located in immutable packages were unexpectedly altered. ..."
            // Sentry APPS-IN-TOSS-UNITY-SDK-CH.
            // 기존 LogType.Warning 가드는 별도로 유지되며(line 432-436), 이 패턴은 Error/Exception LogType 변형도 흡수.
            // SDK 자체 코드는 이 메시지를 출력하지 않으며(주석으로만 참조) Unity 엔진이 직접 출력.
            "immutable packages were unexpectedly altered",

            // Unity AssetDatabase가 존재하지 않는 검색 폴더로 FindAssets 호출 시 직접 출력하는 엔진 경고.
            // 예: "AssetDatabase.FindAssets: Folder not found: 'Assets/Foo'"
            // SDK 자체 로그 접두사([AIT 등)가 없는 Unity 패키지 탐색 노이즈이며 사용자 프로젝트의
            // 폴더 구성/검색 경로 문제에 해당. SDK는 FindAssets를 호출하긴 하지만(AITBuildOptimizationScanner)
            // 이 경고 문자열을 직접 출력하지 않고 Unity 엔진이 출력하므로 AitKeywords 보호 가드와 충돌 없음.
            // Sentry APPS-IN-TOSS-UNITY-SDK-ZZ.
            "AssetDatabase.FindAssets: Folder not found",
        };

        // DetermineErrorSource에서 메시지를 SDK로 분류하는 추가 패턴.
        // 스택트레이스로 출처 판별이 안 될 때, AitKeywords 및 "Sentry:" prefix와 함께 검사됩니다.
        // AitKeywords와 중복되는 키워드는 제외 — drift 방지.
        private static readonly string[] SdkMessagePatterns =
        {
            "[Validation]",
            "[pnpm]",
            "webgl/Build/",
        };

        // 외부(샘플/사용자 게임 코드)에서 AIT prefix를 사용하지만 SDK가 출력하지 않는 메시지.
        // AitKeywords 가드보다 먼저 매칭되어 SDK 보호 가드를 우회하고 노이즈로 분류된다.
        // 새 prefix 추가 시: SDK 코드에서 grep으로 해당 문자열이 출력되지 않음을 반드시 확인.
        // 대상 Sentry 이슈:
        //   - SDK-D2: [AIT Login][src=AIT_MOCK_OR_TIMEOUT] ...
        //   - SDK-D3: [AIT Login] InitSession failed: FORBIDDEN_ORIGIN
        //   - SDK-CF: [Toss Firebase] 게임로그인 실패: ... (사용자 게임 백엔드 통합 레이어)
        //   - SDK-PK/PJ/PF/PC/PB/QA/Q9/Q8/Q7/Q6/Q5/Q4: Assets/FTR_AppsInToss/... CS#### 사용자 코드 경고
        //     (Unity 컴파일러가 사용자 프로젝트 파일 경로 prefix로 출력 — SDK 자체 코드는 Runtime/ 또는 Editor/ 하위)
        //   - SDK-S1/SX: <color=Yellow>AITPromotion</color>: ... (사용자 게임 프로모션 로직 로그)
        //     SDK 어디에도 "AITPromotion" 문자열이 출력되지 않으며 (grep 확인),
        //     AitKeywords의 "[AIT"/"AIT:"와도 매칭되지 않으므로 ExternalAitPrefixes로 안전하게 분류.
        //     "UnityWarning:" prefix가 덧붙은 변형(SDK-S1 재발)도 IndexOf 부분 매칭으로 동일하게 드롭.
        private static readonly string[] ExternalAitPrefixes =
        {
            "[AIT Login]",
            "[Toss Firebase]",
            "Assets\\FTR_AppsInToss\\",
            "Assets/FTR_AppsInToss/",
            "AITPromotion</color>",
            // SDK-ZV: [AppsInTossIAPManager] IAPGetPendingOrders: null (앱 버전 미지원 등) — 사용자/샘플 IAP wrapper 클래스 로그.
            // SDK는 IAPGetPendingOrders API는 제공하지만 "[AppsInTossIAPManager]" prefix는 출력하지 않으며(grep 확인),
            // "AppsInToss"가 "IAPManager"와 붙어 단어 경계가 깨져 AitKeywords 가드에도 안 걸리므로 ExternalAitPrefixes로 분류.
            "[AppsInTossIAPManager]",
        };

        #endregion

        #region Session State

        private static string _sessionId;
        private static DateTime _sessionStarted;
        private static int _sessionErrorCount;
        private static bool _sessionInitSent;

        #endregion

        #region Dedup State

        private static readonly Dictionary<int, DateTime> _recentErrors = new Dictionary<int, DateTime>();

        // CaptureBuildError/AITLog에서 로그 핸들러의 Sentry 캡처를 억제하기 위한 카운터
        // 중첩 호출을 지원하며, Interlocked으로 스레드 안전성 보장
        private static volatile int _suppressLogCaptureCount;

        #endregion

        #region Breadcrumbs

        private static readonly Queue<AITSentryEnvelope.Breadcrumb> _breadcrumbs =
            new Queue<AITSentryEnvelope.Breadcrumb>();

        #endregion

        #region Last Event ID

        private static volatile string _lastEventId;

        /// <summary>
        /// 가장 최근에 빌드·전송된 에러 이벤트의 event_id (32자 소문자 hex).
        /// 주의: 실제 Sentry 전송 성공 여부와 무관하게, 엔벨로프가 빌드되어 Transport 큐에 넣어진 시점에 할당됩니다.
        /// 네트워크 실패·rate-limit·드롭 등으로 전송되지 않았을 수 있지만, user_report는 동일 event_id로 별도 전송되면
        /// Sentry 서버에서 관계가 복원됩니다. consent 미동의·DSN 미설정·dedup 캐시 적중 시에는 null이 유지됩니다.
        /// </summary>
        internal static string LastEventId => _lastEventId;

        #endregion

        #region Static Constructor

        static AITEditorErrorTracker()
        {
            string dsn = GetDsn();
            if (string.IsNullOrEmpty(dsn))
                return;

            // 데이터 전송 전에 사용자에게 고지
            AITErrorTrackerConsent.ShowNoticeIfNeeded();

            if (!AITErrorTrackerConsent.IsEnabled())
                return;

            AITSentryTransport.SetDsn(dsn);
            StartSession();

            // 도메인 리로드 시 핸들러 중복 등록 방지
            EditorApplication.quitting -= OnQuitting;
            EditorApplication.quitting += OnQuitting;
            Application.logMessageReceived -= OnLogMessageReceived;
            Application.logMessageReceived += OnLogMessageReceived;
        }

        #endregion

        #region Public API

        /// <summary>
        /// DSN이 설정되어 있는지 확인합니다. 사용자의 opt-out 상태는 반영하지 않습니다.
        /// </summary>
        internal static bool IsDsnConfigured => !string.IsNullOrEmpty(DEFAULT_DSN);

        /// <summary>
        /// 현재 설정된 Sentry DSN을 반환합니다.
        /// </summary>
        internal static string GetDsn()
        {
            return DEFAULT_DSN;
        }

        /// <summary>
        /// 릴리즈 문자열을 반환합니다. (예: "apps-in-toss.unity@2.4.1")
        /// </summary>
        internal static string GetRelease()
        {
            return $"{RELEASE_PREFIX}@{AITVersion.Version}";
        }

        #endregion

        #region Session Management

        private static void StartSession()
        {
            _sessionId = Guid.NewGuid().ToString("N");
            _sessionStarted = DateTime.UtcNow;
            _sessionErrorCount = 0;
            _sessionInitSent = false;
            _breadcrumbs.Clear();
            _recentErrors.Clear();

            if (!AITErrorTrackerConsent.IsEnabled())
                return;

            string dsn = GetDsn();
            if (string.IsNullOrEmpty(dsn))
                return;

            string envelope = AITSentryEnvelope.BuildSessionEnvelope(
                dsn: dsn,
                sessionId: _sessionId,
                distinctId: AITErrorTrackerConsent.GetDistinctId(),
                status: "ok",
                isInit: true,
                started: _sessionStarted,
                errorCount: 0,
                duration: 0,
                release: GetRelease(),
                environment: ENVIRONMENT
            );

            AITSentryTransport.SendEnvelope(envelope);
            _sessionInitSent = true;
        }

        private static void EndSession(string status)
        {
            if (!AITErrorTrackerConsent.IsEnabled())
                return;

            if (!_sessionInitSent)
                return;

            string dsn = GetDsn();
            if (string.IsNullOrEmpty(dsn))
                return;

            double duration = (DateTime.UtcNow - _sessionStarted).TotalSeconds;

            string envelope = AITSentryEnvelope.BuildSessionEnvelope(
                dsn: dsn,
                sessionId: _sessionId,
                distinctId: AITErrorTrackerConsent.GetDistinctId(),
                status: status,
                isInit: false,
                started: _sessionStarted,
                errorCount: _sessionErrorCount,
                duration: duration,
                release: GetRelease(),
                environment: ENVIRONMENT
            );

            AITSentryTransport.SendEnvelope(envelope);
            AITSentryTransport.FlushSync();
        }

        private static void OnQuitting()
        {
            EndSession("exited");
        }

        #endregion

        #region Error Capture

        private static void OnLogMessageReceived(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Warning)
                return;

            // CaptureBuildError 또는 AITLog에서 억제된 로그의 이중 전송 방지
            if (_suppressLogCaptureCount > 0)
                return;

            // EditMode 테스트가 SUT를 호출하며 발생시키는 의도된 LogWarning/LogError는 Sentry에서 제외
            // (테스트는 invalid input을 일부러 주입하므로 그 로그는 프로덕션 에러가 아님)
            if (IsInvokedFromTestRunner(stackTrace))
                return;

            // Dev/Production Server 프로세스에서 리디렉션된 로그는 Sentry에서 제외
            // (granite dev의 stdout/stderr가 Debug.Log/LogError/LogWarning으로 전달된 것)
            if (IsServerRedirectedLog(message))
                return;

            if (!IsAitRelated(message, stackTrace))
                return;

            // 확실한 사용자 프로젝트/Unity 내부 메시지는 IsAitRelated를 통과해도 제외
            if (IsKnownNonSdkMessage(message))
                return;

            // Unity PackageManager가 Git 패키지 업데이트 시 발생시키는 immutable 패키지 경고는 무시
            // (SDK 자동 업데이트 과정에서 정상적으로 발생할 수 있는 경고)
            if (type == LogType.Warning && message != null
                && message.IndexOf("immutable packages", StringComparison.OrdinalIgnoreCase) >= 0)
                return;

            // Strict error_source 게이트: 출처가 SDK로 확정되지 않은 메시지는 전송하지 않음.
            if (ShouldDropAsNonSdkSource(message, stackTrace))
                return;

            string level;
            string exceptionType;

            if (type == LogType.Exception)
            {
                exceptionType = ExtractExceptionType(message);
                level = "error";
            }
            else if (type == LogType.Error)
            {
                exceptionType = "UnityError";
                level = "error";
            }
            else
            {
                exceptionType = "UnityWarning";
                level = "warning";
            }

            CaptureError(exceptionType, message, stackTrace, level);
        }

        /// <summary>
        /// 에러를 캡처하여 Sentry로 전송합니다.
        /// </summary>
        internal static void CaptureError(
            string exceptionType,
            string message,
            string stackTrace,
            string level = "error",
            Dictionary<string, string> extraTags = null,
            string[] fingerprint = null)
        {
            if (!AITErrorTrackerConsent.IsEnabled())
                return;

            string dsn = GetDsn();
            if (string.IsNullOrEmpty(dsn))
                return;

            // Dedup check
            int dedupKey = GetDedupKey(exceptionType, message);
            DateTime now = DateTime.UtcNow;
            CleanupExpiredDedups(now);

            if (_recentErrors.ContainsKey(dedupKey))
                return;

            // 딕셔너리 크기 제한 — 만료 정리 후에도 초과 시 전체 리셋
            if (_recentErrors.Count >= MAX_DEDUP_ENTRIES)
                _recentErrors.Clear();

            _recentErrors[dedupKey] = now;
            _sessionErrorCount++;

            // Build tags
            var tags = new Dictionary<string, string>
            {
                { "sdk_version", AITVersion.Version },
                { "unity_version", Application.unityVersion },
                { "os", SystemInfo.operatingSystem },
                { "editor_platform", Application.platform.ToString() },
                { "error_source", DetermineErrorSource(stackTrace, message) }
            };

            if (extraTags != null)
            {
                foreach (var kvp in extraTags)
                {
                    tags[kvp.Key] = kvp.Value;
                }
            }

            // Truncate message
            string truncatedMessage = message;
            if (truncatedMessage != null && truncatedMessage.Length > 1000)
            {
                truncatedMessage = truncatedMessage.Substring(0, 1000);
            }

            // Build and send envelope
            string envelope = AITSentryEnvelope.BuildErrorEventEnvelope(
                dsn: dsn,
                exceptionType: exceptionType,
                exceptionValue: truncatedMessage,
                stackTrace: stackTrace,
                eventId: out string capturedEventId,
                level: level,
                tags: tags,
                breadcrumbs: _breadcrumbs.Count > 0 ? new List<AITSentryEnvelope.Breadcrumb>(_breadcrumbs) : null,
                fingerprint: fingerprint,
                release: GetRelease(),
                environment: ENVIRONMENT
            );

            _lastEventId = capturedEventId;
            AITSentryTransport.SendEnvelope(envelope);
        }

        /// <summary>
        /// 빌드 에러를 Sentry에 캡처하고, Console에 에러 로그를 출력합니다.
        /// 로그 핸들러의 이중 캡처를 내부적으로 방지하므로 호출자가 suppress를 관리할 필요 없습니다.
        /// </summary>
        internal static void CaptureBuildError(
            AITConvertCore.AITExportError errorCode,
            string logMessage,
            string profileName = null)
        {
            if (errorCode == AITConvertCore.AITExportError.SUCCEED ||
                errorCode == AITConvertCore.AITExportError.CANCELLED)
                return;

            BeginSuppressLogCapture();
            try
            {
                CaptureBuildErrorInternal(errorCode, profileName);
                UnityEngine.Debug.LogError(logMessage);
            }
            finally
            {
                EndSuppressLogCapture();
            }
        }

        /// <summary>
        /// 로그 핸들러의 Sentry 캡처를 일시 억제합니다.
        /// 반드시 EndSuppressLogCapture()와 쌍으로 사용해야 합니다.
        /// </summary>
        internal static void BeginSuppressLogCapture()
        {
            System.Threading.Interlocked.Increment(ref _suppressLogCaptureCount);
        }

        /// <summary>
        /// BeginSuppressLogCapture 후 로그 억제를 해제합니다.
        /// </summary>
        internal static void EndSuppressLogCapture()
        {
            // 원자적 underflow 방지: 0 이하로 내려가지 않도록 CAS 루프
            int current;
            do
            {
                current = _suppressLogCaptureCount;
                if (current <= 0)
                    return;
            }
            while (System.Threading.Interlocked.CompareExchange(
                ref _suppressLogCaptureCount, current - 1, current) != current);
        }

        private static void CaptureBuildErrorInternal(
            AITConvertCore.AITExportError errorCode,
            string profileName)
        {
            string errorMessage = AITConvertCore.GetErrorMessage(errorCode);
            string exceptionType = $"AITBuildError.{errorCode}";

            var extraTags = new Dictionary<string, string>
            {
                { "error_code", errorCode.ToString() },
                { "error_code_int", ((int)errorCode).ToString() },
                { "error_source", "sdk" }
            };

            if (!string.IsNullOrEmpty(profileName))
            {
                extraTags["build_profile"] = profileName;
            }

            var fingerprint = new[] { "{{ default }}", errorCode.ToString() };

            CaptureError(
                exceptionType: exceptionType,
                message: errorMessage,
                stackTrace: null,
                level: "error",
                extraTags: extraTags,
                fingerprint: fingerprint
            );
        }

        #endregion

        #region Breadcrumbs

        /// <summary>
        /// 브레드크럼을 추가합니다. 최대 개수 초과 시 가장 오래된 항목을 제거합니다.
        /// </summary>
        internal static void AddBreadcrumb(string category, string message, string level = "info")
        {
            if (_breadcrumbs.Count >= MAX_BREADCRUMBS)
            {
                _breadcrumbs.Dequeue();
            }

            _breadcrumbs.Enqueue(new AITSentryEnvelope.Breadcrumb
            {
                Timestamp = DateTime.UtcNow,
                Category = category,
                Message = message,
                Level = level
            });
        }

        #endregion

        #region Private Helpers

        private static bool IsServerRedirectedLog(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            for (int i = 0; i < ServerLogPrefixes.Length; i++)
            {
                if (message.StartsWith(ServerLogPrefixes[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsAitRelated(string message, string stackTrace)
        {
            for (int i = 0; i < AitKeywords.Length; i++)
            {
                string keyword = AitKeywords[i];

                // 메시지 본문은 단어 경계 정책으로 매칭 — "Portrait:" 안의 "ait:"처럼
                // 일반 영어 단어 일부와 case-insensitive substring 충돌하는 거짓양성을 차단.
                // 스택트레이스는 namespace prefix(예: "AppsInToss.Editor.Foo") 매칭이 자연스럽고
                // 거짓양성 가능성이 낮으므로 기존 substring 동작 유지.
                if (message != null && KeywordMatchesAtBoundary(message, keyword))
                    return true;

                if (stackTrace != null && stackTrace.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 메시지가 SDK 자체 로그임을 식별할 수 있는 키워드를 포함하는지 검사합니다.
        /// <see cref="IsKnownNonSdkMessage"/>의 SDK 보호 가드 및 <see cref="DetermineErrorSource"/>의
        /// 메시지 기반 분류에서 단일 source로 재사용됩니다.
        ///
        /// <para>
        /// "AppsInToss"/"ait-build"처럼 사용자 프로젝트 경로(예: <c>Assets/FTR_AppsInToss/...</c>)에
        /// 부분 문자열로 들어갈 수 있는 키워드는 단어 경계를 요구하여 거짓 양성을 차단합니다.
        /// 단어 경계: 키워드 직전/직후가 letter 또는 digit이면 SDK 키워드로 보지 않습니다.
        /// (점/슬래시/공백/괄호 등 식별자 구분자만 허용)
        /// </para>
        /// </summary>
        private static bool MessageContainsSdkKeyword(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            // AitKeywords를 그대로 재사용하여 IsAitRelated와 가드의 키워드 set drift를 방지.
            // 식별자형 키워드("AppsInToss"/"ait-build" 등)는 양쪽 단어 경계를 요구하고,
            // prefix형 키워드("[AIT", "AIT:")는 키워드 자체가 letter로 시작하는 경우에 한해
            // 왼쪽 단어 경계만 요구한다. 후자는 "Portrait:" 안의 "ait:"처럼 일반 영어 단어
            // 일부와 case-insensitive 충돌하는 거짓양성을 차단하기 위함이다 (Sentry T6 회귀 방지).
            for (int i = 0; i < AitKeywords.Length; i++)
            {
                if (KeywordMatchesAtBoundary(message, AitKeywords[i]))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// AitKeywords의 단일 키워드가 메시지에 단어 경계 정책에 맞게 등장하는지 검사한다.
        /// 식별자형 키워드는 양쪽 경계, prefix형 키워드(첫 문자가 비식별자)는 substring 매치,
        /// 첫 문자가 letter인 prefix형 키워드("AIT:")는 왼쪽 경계만 요구한다.
        /// </summary>
        private static bool KeywordMatchesAtBoundary(string text, string keyword)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(keyword))
                return false;

            if (IsIdentifierToken(keyword))
                return ContainsKeywordAtBoundary(text, keyword);

            // prefix형 키워드: 키워드 첫 문자가 비식별자(예: '[')면 자연스럽게 왼쪽 경계가 형성되어
            // substring 매치로 충분. letter로 시작하면("AIT:") 왼쪽 경계 검사로 좁힘.
            if (!IsBoundaryWordChar(keyword[0]))
                return text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;

            int searchFrom = 0;
            while (searchFrom <= text.Length - keyword.Length)
            {
                int idx = text.IndexOf(keyword, searchFrom, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    return false;

                bool leftOk = idx == 0 || !IsBoundaryWordChar(text[idx - 1]);
                if (leftOk)
                    return true;

                searchFrom = idx + 1;
            }
            return false;
        }

        // 키워드의 모든 문자가 식별자 문자(letter/digit/underscore/하이픈)로 이루어졌는지.
        // 'apps-in-toss'/'ait-build'처럼 하이픈을 포함한 키워드도 단일 토큰으로 본다.
        private static bool IsIdentifierToken(string keyword)
        {
            for (int i = 0; i < keyword.Length; i++)
            {
                char c = keyword[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    return false;
            }
            return true;
        }

        // 식별자형 키워드가 message에 단어 경계로 등장하는지 검사한다.
        // 단어 경계: 키워드 직전/직후가 letter/digit/underscore가 아닌 위치.
        // 예) "AppsInToss"는 "AppsInToss.Editor"에 매치, "FTR_AppsInToss"엔 미매치.
        private static bool ContainsKeywordAtBoundary(string message, string keyword)
        {
            if (string.IsNullOrEmpty(message) || string.IsNullOrEmpty(keyword))
                return false;

            int searchFrom = 0;
            while (searchFrom <= message.Length - keyword.Length)
            {
                int idx = message.IndexOf(keyword, searchFrom, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                    return false;

                bool leftOk = idx == 0 || !IsBoundaryWordChar(message[idx - 1]);
                int after = idx + keyword.Length;
                bool rightOk = after >= message.Length || !IsBoundaryWordChar(message[after]);

                if (leftOk && rightOk)
                    return true;

                searchFrom = idx + 1;
            }

            return false;
        }

        private static bool IsBoundaryWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
        }

        // Unity EditMode 테스트 러너 실행을 감지하는 스택트레이스 마커.
        // NUnit 프레임워크 호출부 또는 Unity가 주입한 TestTools/TestRunner 프레임이
        // 스택에 포함되면 테스트 실행 컨텍스트로 간주합니다.
        // 메시지 본문이 아닌 stackTrace 인자만 검사하여 사용자 프로젝트에 'NUnit' 문자열이
        // 포함된 로그가 잘못 필터링되는 것을 방지합니다.
        private static readonly string[] TestRunnerStackMarkers =
        {
            "NUnit.Framework.",
            "UnityEngine.TestRunner.",
            "UnityEditor.TestTools."
        };

        /// <summary>
        /// 호출 스택이 Unity 테스트 러너 내부에서 비롯된 것인지 판별합니다.
        /// true일 경우 해당 로그는 Sentry 캡처 대상에서 제외됩니다.
        /// </summary>
        internal static bool IsInvokedFromTestRunner(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
                return false;

            for (int i = 0; i < TestRunnerStackMarkers.Length; i++)
            {
                if (stackTrace.IndexOf(TestRunnerStackMarkers[i], StringComparison.Ordinal) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 메시지가 확실히 SDK와 무관한 Unity 내부/사용자 프로젝트 패턴인지 판별합니다.
        /// AIT 키워드(<see cref="AitKeywords"/>)가 포함되면 절대 필터링하지 않습니다.
        /// 단, <see cref="ExternalAitPrefixes"/>는 SDK가 출력하지 않는 외부 코드 prefix로
        /// AitKeywords 가드보다 먼저 매칭되어 노이즈로 드롭됩니다.
        /// </summary>
        internal static bool IsKnownNonSdkMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            // 외부 코드가 사용하는 AIT prefix는 SDK 가드를 우회하여 먼저 드롭한다.
            // SDK 코드는 이 prefix를 출력하지 않음이 보장되므로 안전.
            for (int i = 0; i < ExternalAitPrefixes.Length; i++)
            {
                if (message.IndexOf(ExternalAitPrefixes[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            // Sentry 전송 모듈 자체의 실패를 다시 Sentry로 보내면 self-loop이 발생한다.
            // 현재 main에서는 source 단에서 sentryCapture:false로 차단하지만(AITSentryTransport),
            // 이전 SDK 버전이 만든 envelope이 후속 빌드에서 흘러올 수 있어 cascade 필터도 함께 유지.
            // 4xx(인증/페이로드)와 5xx(서비스 장애) 모두 SDK 코드로 분기할 정보가 아니므로 동일 처리.
            // Sentry APPS-IN-TOSS-UNITY-SDK-T4 — [AITSentryTransport] Sentry 전송 실패 (HTTP 503)
            if (message.IndexOf("[AITSentryTransport] Sentry 전송 실패 (HTTP ", StringComparison.Ordinal) >= 0)
                return true;

            // 동기 전송(에디터 종료 시 FlushSync) 실패도 self-loop 위험. 동일 정책으로 차단.
            if (message.IndexOf("[AITSentryTransport] 동기 전송 실패", StringComparison.Ordinal) >= 0)
                return true;

            // AITSentryTransport 자체의 네트워크 오류(ConnectionError) — 사용자 환경 일시 장애.
            // Transport가 스스로의 출력을 다시 Sentry로 보내면 캐스케이드 위험이 있고,
            // 실제로 SubmitResult.Fail로 호출자에게 결과가 전달되므로 가시성도 유지됨.
            // Sentry APPS-IN-TOSS-UNITY-SDK-CZ, APPS-IN-TOSS-UNITY-SDK-KA, APPS-IN-TOSS-UNITY-SDK-RR.
            // UnityWebRequest.error 텍스트(예: "Connection refused", "Unknown Error", "Request timeout",
            // "Unable to read data")가 suffix로 붙는 다양한 변형이 동일 패턴으로 모두 매칭된다.
            if (message.IndexOf("[AITSentryTransport] 네트워크 오류", StringComparison.Ordinal) >= 0)
                return true;

            // 외부 정책 파일 fetch의 일시적 네트워크 오류 — SDK가 외부 호스트에 띄운 메시지이지만
            // SubmitResult/콘솔로 사용자 가시성은 유지되고, 재시도 시 자연 회복되는 케이스.
            // Sentry APPS-IN-TOSS-UNITY-SDK-M9.
            if (message.IndexOf("[AIT] sdk-policy.json fetch 실패", StringComparison.Ordinal) >= 0)
                return true;

            // Vite dev 서버 포트 polling 타임아웃 — 단순 폴링 종료 알림이며 실제로는 곧 브라우저가 열림.
            // SDK 흐름상 fatal하지 않고 사용자에게 안내 후 진행.
            // Sentry APPS-IN-TOSS-UNITY-SDK-QN.
            if (message.IndexOf("[AIT] Vite 포트 5173 대기 타임아웃", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 환경 포트 점유 / 외부 프로세스 비정상 종료 — actionable 가이드는 콘솔에 이미 출력.
            // Sentry APPS-IN-TOSS-UNITY-SDK-KP, APPS-IN-TOSS-UNITY-SDK-Q3.
            if (message.IndexOf("AIT: Dev 서버 시작 실패", StringComparison.Ordinal) >= 0
                || message.IndexOf("AIT: Production 서버 시작 실패", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 git 환경 문제(Author identity 미설정, git 프로세스 실패 등) — SDK 자동 커밋 보조 흐름.
            // 실패 시 SDK는 계속 진행하며 사용자가 수동 커밋 가능.
            // Sentry APPS-IN-TOSS-UNITY-SDK-SK, APPS-IN-TOSS-UNITY-SDK-TZ.
            if (message.IndexOf("[AIT] 자동 커밋 실패", StringComparison.Ordinal) >= 0)
                return true;

            // git 명령 타임아웃 — 짧은 타임아웃(5초)은 #591에서 source 차단(AITLog sentryCapture:false).
            // 긴 타임아웃(예: 300초 commit) 변형도 동일하게 사용자 환경 응답 지연이므로 차단.
            // Sentry APPS-IN-TOSS-UNITY-SDK-TY, QC.
            if (message.IndexOf("[AIT] Git 명령 타임아웃", StringComparison.Ordinal) >= 0)
                return true;

            // Unity AssetDatabase가 출력하는 GUID 충돌 경고. 사용자가 SDK를 UPM이 아닌
            // Assets/ 하위로 import한 환경에서 동일 자산이 Packages/와 Assets/ 양쪽에 존재하면
            // Unity 엔진이 자동으로 새 GUID를 부여하며 이 경고를 출력한다.
            // 형식:
            //   "GUID [<hash>] for asset '<assetPath>' conflicts with:
            //     '<otherAssetPath>' (current owner)
            //   Assigning a new guid."
            // composite AND 조건으로 다른 GUID 진단 메시지와 거짓양성 충돌을 방지한다.
            // SDK 패키지 경로가 message에 들어가 AitKeywords 가드가 발동되므로 가드보다 먼저 매칭.
            // [AIT] prefix가 붙은 SDK 자체 로그는 절대 필터링하지 않으므로 별도 가드.
            // Sentry APPS-IN-TOSS-UNITY-SDK-BQ.
            if (message.IndexOf("GUID [", StringComparison.Ordinal) >= 0
                && message.IndexOf("] for asset '", StringComparison.Ordinal) >= 0
                && message.IndexOf("' conflicts with:", StringComparison.Ordinal) >= 0
                && !message.StartsWith("[AIT]", StringComparison.Ordinal))
                return true;

            // 사용자가 직접 WebGL 빌드를 취소한 정보성 경고 — SDK의 의도된 동작 경로.
            // 신규 SDK 버전은 #591에서 origin(AITLog sentryCapture:false)으로 차단되지만,
            // 이전 버전 사용자 빌드에서는 여전히 Sentry로 도달하므로 message-filter로도 흡수.
            // Sentry APPS-IN-TOSS-UNITY-SDK-TX.
            if (message.IndexOf("사용자에 의해 WebGL 빌드가 취소", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 프로젝트(Assets/) 하위 .cs 파일의 CS0029 암묵 변환 컴파일 에러 (SDK-T2).
            // Unity 컴파일러가 사용자 코드의 타입 변환 실패를 보고할 때 출력하는 포맷:
            //   "Assets/.../Foo.cs(L,C): error CS0029: Cannot implicitly convert type 'X' to 'Y'"
            // 메시지에 SDK 타입명(예: 'AppsInToss.IapProductListItem')이 들어가 AitKeywords 가드가
            // 발동되므로, SDK 가드보다 먼저 매칭해 노이즈로 드롭한다.
            // SDK 자체 코드의 컴파일 에러는 'Packages/com.toss.apps-in-toss/...' 또는
            // 'Library/PackageCache/...' 경로로 출력되어 'Assets/' prefix가 붙지 않으므로 안전.
            if (message.IndexOf("error CS0029", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 식별자 미발견 컴파일 에러 (CS0103) — Unity 컴파일러가 직접 출력.
            // 예: "Assets\Scripts\AppsInTossCompatibilityChecker.cs(31,9): error CS0103:
            //       The name 'CheckIncompatibleComponents' does not exist in the current context"
            // Sentry APPS-IN-TOSS-UNITY-SDK-CR, APPS-IN-TOSS-UNITY-SDK-MN.
            // 사용자 식별자가 가변이라 일반화된 형태로 좁힌다(Assets/ 경로 + .cs(L,C) 패턴).
            // SDK 자체 코드는 Packages/ 또는 Library/PackageCache/ 경로로 출력되어 안전.
            if (message.IndexOf("error CS0103", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 멤버 미정의 컴파일 에러 (CS0117) — Unity 컴파일러가 직접 출력.
            // 예: "Assets\98_Tools\BuildTool\Editor\BuildToolEditorWindow.cs(484,43): error CS0117:
            //       'AppsInTossMenu' does not contain a definition for 'Package'"
            // Sentry APPS-IN-TOSS-UNITY-SDK-80.
            // SDK 타입명을 잘못 참조한 경우라도 사용자 코드가 잘못 사용한 것이며 SDK 분기로 해결 불가.
            if (message.IndexOf("error CS0117", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 async/await 미사용 경고 (CS1998) — Unity 컴파일러가 직접 출력.
            // 예: "Assets\Scripts\1. System\AppsInToss\TossManager.cs(260,43): warning CS1998:
            //       This async method lacks 'await' operators and will run synchronously. ..."
            // Sentry APPS-IN-TOSS-UNITY-SDK-Z6.
            // 사용자 폴더명에 'AppsInToss'가 포함돼 SDK 키워드 가드가 발동하므로 가드보다 먼저 매칭한다(Assets/ 경로 + .cs(L,C)).
            // SDK 자체 코드는 Packages/ 또는 Library/PackageCache/ 경로로 출력되어 Assets/ 가드와 충돌 없음.
            if (message.IndexOf("warning CS1998", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 'AppsInToss' 미발견 컴파일 에러 — SDK 미설치 또는 asmdef 참조 누락.
            // 예: "Assets/.../Foo.cs(L,C): error CS0246: The type or namespace name 'AppsInToss' could not be found ..."
            // 메시지에 'AppsInToss' 토큰이 들어가 SDK 키워드 가드가 발동하므로 가드보다 먼저 매칭한다.
            // Sentry APPS-IN-TOSS-UNITY-SDK-C3, APPS-IN-TOSS-UNITY-SDK-M7 등.
            // CS0246 단독은 SDK 빌드 메시지와 충돌 위험이 있어 "'AppsInToss'"와 합성 AND + Assets/ 경로 가드로 좁힌다.
            if (message.IndexOf("error CS0246", StringComparison.Ordinal) >= 0
                && message.IndexOf("'AppsInToss'", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 'AppsInToss' using 중복(CS0105) — Unity 컴파일러가 직접 출력.
            // 예: "Assets/.../Foo.cs(L,C): warning CS0105: The using directive for 'AppsInToss' appeared previously in this namespace"
            // Sentry APPS-IN-TOSS-UNITY-SDK-SW.
            if (message.IndexOf("warning CS0105", StringComparison.Ordinal) >= 0
                && message.IndexOf("'AppsInToss'", StringComparison.Ordinal) >= 0
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자 코드의 SDK 타입 인자 오용(CS1503) — Unity 컴파일러가 직접 출력.
            // 예: "Assets\Scripts\Manager\TossManager.cs(192,91): error CS1503: Argument 1: cannot convert from 'AppsInToss.GetUserKeyForGameResult' to 'string'"
            // 예(List 제네릭 변형): "Assets/.../RemoteShopInfo.cs(312,68): error CS1503: Argument 1: cannot convert from 'System.Collections.Generic.List<Studio.Common.Trident.Billing.ProductInfo>' to 'System.Collections.Generic.List<AppsInToss.IapProductListItem>'"
            // Sentry APPS-IN-TOSS-UNITY-SDK-VM/PV/PW/DA/WW/WV.
            // 메시지에 'AppsInToss.*' 타입명이 들어가 SDK 키워드 가드가 발동하므로 가드보다 먼저 매칭한다.
            // namespace prefix는 작은따옴표 직후('AppsInToss.) 또는 List<> 등 제네릭 인자 직후(<AppsInToss.) 두 형태를 모두 허용.
            // 두 변형 모두 점(.)을 포함해 단독 토큰 'AppsInToss'만 있는 SDK 빌드 메시지와는 충돌 없음.
            if (message.IndexOf("error CS1503", StringComparison.Ordinal) >= 0
                && (message.IndexOf("'AppsInToss.", StringComparison.Ordinal) >= 0
                    || message.IndexOf("<AppsInToss.", StringComparison.Ordinal) >= 0)
                && (message.IndexOf("Assets/", StringComparison.Ordinal) >= 0
                    || message.IndexOf("Assets\\", StringComparison.Ordinal) >= 0)
                && message.IndexOf(".cs(", StringComparison.Ordinal) >= 0)
                return true;

            // Unity AssetDatabase가 Library/ 경로의 SDK 외부 캐시 로딩 실패 시 직접 출력.
            // 예: "Unknown error occurred while loading 'Library/AppsInToss/AITBuildSession.asset'."
            // 메시지에 'AppsInToss'/'AIT' 토큰이 들어가 SDK 키워드 가드가 발동하므로 가드보다 먼저 매칭한다.
            // Sentry APPS-IN-TOSS-UNITY-SDK-RT.
            if (message.IndexOf("Unknown error occurred while loading 'Library/", StringComparison.Ordinal) >= 0)
                return true;

            // AITAsyncCommandRunner의 Windows powershell 실행 실패 — 사용자 환경(PATH/실행 정책) 원인.
            // 예: "[AIT Async] 명령 실행 예외: System.ComponentModel.Win32Exception (0x80004005):
            //       ApplicationName='powershell.exe', CommandLine='-ExecutionPolicy Bypass ...'"
            // [AIT Async] prefix가 SDK 키워드 가드("[AIT")에 막히므로 가드보다 먼저 매칭한다.
            // 사용자 환경 문제이며 SDK 코드 분기로 해결 불가. 합성 AND로 일반 [AIT Async] 메시지와 충돌 방지.
            // Sentry APPS-IN-TOSS-UNITY-SDK-VE, APPS-IN-TOSS-UNITY-SDK-VC.
            if (message.IndexOf("[AIT Async] 명령 실행 예외", StringComparison.Ordinal) >= 0
                && message.IndexOf("Win32Exception", StringComparison.Ordinal) >= 0
                && message.IndexOf("ApplicationName='powershell.exe'", StringComparison.Ordinal) >= 0)
                return true;

            // AppsInTossMenu의 'ait deploy' 실패 경로가 redirect한 stdout/stderr 본문 중
            // pnpm/npm progress bar의 ANSI escape 시퀀스(\x1b[?25l, \x1b[?25h 등 커서 hide/show)만 들어간 라인.
            // 예: "AIT: [stdout] \x1b[?25l│", "AIT: [stdout] \x1b[?25h"
            // 신규 SDK는 deploy 경로를 sentryCapture: false로 차단(D10a)했지만, 구버전 SDK 사용자는 여전히
            // 동일 메시지를 다수 fingerprint(VK/BD/T5)로 전송한다. 컨텐츠 기반 backstop.
            // 'AIT: [stdout]'/'AIT: [stderr]' prefix는 SDK 키워드 가드("AIT:")에 막히므로 가드보다 먼저 매칭.
            // ANSI escape "[?25" + "AIT: [std" 합성으로 좁혀 일반 stdout/stderr 진단 메시지와 충돌 방지.
            // Sentry APPS-IN-TOSS-UNITY-SDK-VK/BD/T5.
            if (message.IndexOf("AIT: [std", StringComparison.Ordinal) >= 0
                && message.IndexOf("[?25", StringComparison.Ordinal) >= 0)
                return true;

            // 사용자가 WebGL 빌드를 직접 취소한 정상 액션 — 에러가 아닌 의도된 사용자 동작 노이즈.
            // 예: "[AIT] 사용자에 의해 WebGL 빌드가 취소되었습니다." (AITConvertCore.cs, AITLog.Warning)
            //     "[AIT] 빌드가 취소되었습니다." (AITConvertCore.cs, Debug.LogWarning 변형)
            // 신규 SDK는 취소 경로를 sentryCapture: false로 차단했지만, 구버전 SDK 사용자는 여전히
            // 동일 Warning을 전송한다. 컨텐츠 기반 backstop.
            // "[AIT]" prefix가 SDK 키워드 가드("[AIT")에 막히므로 가드보다 먼저 매칭한다.
            // "[AIT" + "빌드가 취소되었습니다" 합성으로 좁혀 일반 빌드 실패 메시지와 충돌 방지.
            // Sentry APPS-IN-TOSS-UNITY-SDK-TX.
            if (message.IndexOf("[AIT", StringComparison.Ordinal) >= 0
                && message.IndexOf("빌드가 취소되었습니다", StringComparison.Ordinal) >= 0)
                return true;

            // Unity Package Manager가 사용자 환경(네트워크/Git 인증/SSL 등) 문제로 Git 패키지 추가/제거 실패 시 직접 출력.
            // 예: "[Package Manager Window] Error adding/removing packages: https://github.com/toss/apps-in-toss-unity-sdk.git #release/v2.4.3."
            // URL에 'apps-in-toss' 토큰이 단어 경계로 들어가 SDK 키워드 가드가 발동하므로 가드보다 먼저 매칭한다.
            // "[Package Manager Window]" + "Error adding/removing packages" 합성으로 일반 PM 메시지와 충돌 방지.
            // Sentry APPS-IN-TOSS-UNITY-SDK-QQ, APPS-IN-TOSS-UNITY-SDK-7Y.
            if (message.IndexOf("[Package Manager Window]", StringComparison.Ordinal) >= 0
                && message.IndexOf("Error adding/removing packages", StringComparison.Ordinal) >= 0)
                return true;

            // Unity Package Manager가 의존성 해석 실패 시 직접 출력 — 사용자 환경 manifest.json 충돌 또는 네트워크 장애.
            // 예: "An error occurred while resolving packages:\n  Project has invalid dependencies: com.example.foo"
            // 본문에 SDK 패키지 경로(예: com.toss.apps-in-toss)가 들어가면 SDK 키워드 가드가 발동하므로 가드보다 먼저 매칭한다.
            // "An error occurred while resolving packages" + "Project has invalid dependencies" 합성으로 좁힌다.
            // Sentry APPS-IN-TOSS-UNITY-SDK-V1.
            if (message.IndexOf("An error occurred while resolving packages", StringComparison.Ordinal) >= 0
                && message.IndexOf("Project has invalid dependencies", StringComparison.Ordinal) >= 0)
                return true;

            // AppsInTossMenu deploy 경로의 pnpm/ait CLI가 잘못된 인자로 호출됐을 때 redirect한 syntax error.
            // 예: "AIT: [stdout] Unknown Syntax Error: Not enough arguments to option --api-key."
            // "AIT: [stdout]" prefix가 SDK 키워드 가드("AIT:")에 막히므로 가드보다 먼저 매칭한다.
            // "AIT: [std" + "Unknown Syntax Error: Not enough arguments" 합성으로 일반 stdout과 충돌 방지.
            // 사용자가 deployment key를 입력하지 않은 환경 문제 — SDK 분기로 해결 불가.
            // Sentry APPS-IN-TOSS-UNITY-SDK-TS.
            if (message.IndexOf("AIT: [std", StringComparison.Ordinal) >= 0
                && message.IndexOf("Unknown Syntax Error: Not enough arguments", StringComparison.Ordinal) >= 0)
                return true;

            // Windows shell이 인식 못하는 명령어 호출 시 redirect한 stderr — 사용자 환경 PATH 누락 또는 인코딩 깨짐.
            // 예: "AIT: [stderr] '<corrupted-bytes>' is not recognized as an internal or external command,"
            // "AIT: [stderr]" prefix가 SDK 키워드 가드("AIT:")에 막히므로 가드보다 먼저 매칭한다.
            // "AIT: [std" + "is not recognized as an internal or external command" 합성으로 좁힌다.
            // Sentry APPS-IN-TOSS-UNITY-SDK-RG.
            if (message.IndexOf("AIT: [std", StringComparison.Ordinal) >= 0
                && message.IndexOf("is not recognized as an internal or external command", StringComparison.Ordinal) >= 0)
                return true;

            // SDK 자체 로그는 절대 필터링하지 않음 — AitKeywords 전체를 가드로 사용
            if (MessageContainsSdkKeyword(message))
                return false;

            for (int i = 0; i < NonSdkMessagePatterns.Length; i++)
            {
                if (message.IndexOf(NonSdkMessagePatterns[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            // "Script attached to ... is missing" 패턴은 Assets/ 경로가 포함된 경우에만 사용자 프로젝트로 분류.
            // "in Assets/..." 직접 경로 형태(SDK-H1/GY), "in scene 'Assets/...'" 변형(SDK-H0/GX/GW) 모두 매칭한다.
            if (message.IndexOf("Script attached to", StringComparison.Ordinal) >= 0
                && message.IndexOf("is missing", StringComparison.Ordinal) >= 0
                && message.IndexOf("Assets/", StringComparison.Ordinal) >= 0)
                return true;

            return false;
        }

        /// <summary>
        /// strict error_source 게이트 판정. DetermineErrorSource() != "sdk"이면 true(드롭).
        /// 필터 체인의 다른 단계(LogType, _suppressLogCaptureCount, IsAitRelated, IsKnownNonSdkMessage,
        /// immutable packages)를 모두 통과한 메시지에 대해 마지막으로 출처를 strict 검사한다.
        /// 분류 우선순위는 DetermineErrorSource를 따른다: 스택트레이스 우선, 그 다음 메시지 키워드(AIT/Sentry/SdkMessagePatterns).
        /// </summary>
        internal static bool ShouldDropAsNonSdkSource(string message, string stackTrace)
        {
            // 인수 순서 주의: DetermineErrorSource는 (stackTrace, message) 순.
            return DetermineErrorSource(stackTrace, message) != "sdk";
        }

        private static string ExtractExceptionType(string message)
        {
            // Unity exception format: "ExceptionType: message"
            if (string.IsNullOrEmpty(message))
                return "Exception";

            int colonIndex = message.IndexOf(':');
            if (colonIndex > 0 && colonIndex < 100)
            {
                string candidate = message.Substring(0, colonIndex).Trim();
                // Verify it looks like a type name (no spaces)
                if (candidate.IndexOf(' ') < 0 && candidate.Length > 0)
                    return candidate;
            }

            return "Exception";
        }

        private const string SdkPackagePath = AITVersion.PackageAssetPath + "/";
        private const string SdkPackageCachePath = "Library/PackageCache/" + AITVersion.PackageName + "@";
        private const string SdkPackageCachePathNoVersion = "Library/PackageCache/" + AITVersion.PackageName + "/";
        private const string LegacySdkPackagePath = AITVersion.LegacyPackageAssetPath + "/";
        private const string LegacySdkPackageCachePath = "Library/PackageCache/" + AITVersion.LegacyPackageName + "@";
        private const string LegacySdkPackageCachePathNoVersion = "Library/PackageCache/" + AITVersion.LegacyPackageName + "/";
        private const string UserProjectPathPrefix = "Assets/";

        /// <summary>
        /// 스택트레이스와 메시지를 분석하여 에러의 출처를 결정합니다.
        /// "에러가 throw된 위치" 기준으로 판별합니다 (최상위 프레임 우선).
        /// 누가 호출했는지(trigger)가 아닌 어디서 발생했는지(origin)를 반환합니다.
        /// </summary>
        internal static string DetermineErrorSource(string stackTrace, string message)
        {
            if (!string.IsNullOrEmpty(stackTrace))
            {
                var frames = AITSentryEnvelope.ParseStackTrace(stackTrace);
                if (frames.Count > 0)
                {
                    // ParseStackTrace는 Sentry 컨벤션(oldest first)으로 reverse되어 있으므로,
                    // 최상위 프레임(호출 스택 최상단)은 리스트의 마지막 요소
                    for (int i = frames.Count - 1; i >= 0; i--)
                    {
                        string filename = frames[i].Filename;
                        if (string.IsNullOrEmpty(filename))
                            continue;

                        if (filename.StartsWith(SdkPackagePath, StringComparison.Ordinal) ||
                            filename.StartsWith(SdkPackageCachePath, StringComparison.Ordinal) ||
                            filename.StartsWith(SdkPackageCachePathNoVersion, StringComparison.Ordinal) ||
                            filename.StartsWith(LegacySdkPackagePath, StringComparison.Ordinal) ||
                            filename.StartsWith(LegacySdkPackageCachePath, StringComparison.Ordinal) ||
                            filename.StartsWith(LegacySdkPackageCachePathNoVersion, StringComparison.Ordinal))
                            return "sdk";

                        if (filename.StartsWith(UserProjectPathPrefix, StringComparison.Ordinal))
                            return "user_project";
                    }
                }
            }

            // 스택트레이스로 판별 불가한 경우, 메시지의 SDK 키워드로 분류
            // AitKeywords의 "[AIT", "AIT:", "AppsInToss", "apps-in-toss", "AITNpmRunner" 등 —
            // IsKnownNonSdkMessage의 SDK 보호 가드와 동일한 source를 사용
            if (MessageContainsSdkKeyword(message))
                return "sdk";

            // 메시지 내 SDK 관련 추가 패턴
            if (!string.IsNullOrEmpty(message))
            {
                // Sentry transport 자체 에러
                if (message.StartsWith("Sentry:", StringComparison.Ordinal))
                    return "sdk";

                // SDK 빌드 파이프라인 관련 추가 패턴
                for (int i = 0; i < SdkMessagePatterns.Length; i++)
                {
                    if (message.IndexOf(SdkMessagePatterns[i], StringComparison.Ordinal) >= 0)
                        return "sdk";
                }
            }

            return "unknown";
        }

        // 세션 내 중복 검출용 — GetHashCode()는 Mono 런타임에서 프로세스 내 결정적이며,
        // CoreCLR 전환 시 프로세스 간 비결정적이 되지만, 세션 스코프이므로 문제 없음
        private static int GetDedupKey(string exceptionType, string message)
        {
            string truncated = message != null && message.Length > 100
                ? message.Substring(0, 100)
                : message ?? "";

            unchecked
            {
                int hash = 17;
                hash = hash * 31 + (exceptionType ?? "").GetHashCode();
                hash = hash * 31 + truncated.GetHashCode();
                return hash;
            }
        }

        private static readonly List<int> _expiredKeyBuffer = new List<int>();

        private static void CleanupExpiredDedups(DateTime now)
        {
            _expiredKeyBuffer.Clear();
            var expiredKeys = _expiredKeyBuffer;
            foreach (var kvp in _recentErrors)
            {
                if (now - kvp.Value > DedupWindow)
                {
                    expiredKeys.Add(kvp.Key);
                }
            }

            for (int i = 0; i < expiredKeys.Count; i++)
            {
                _recentErrors.Remove(expiredKeys[i]);
            }
        }

        #endregion
    }
}
