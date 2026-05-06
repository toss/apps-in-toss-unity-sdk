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
            // Animator State에 legacy AnimationClip 할당 — Unity가 메시지 첫 줄만 캡처하는 변형 (SDK-KV/KT)
            "cannot be used in the State",

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

            // Unity URP 내부
            "exceeds previous array size",

            // 사용자 Addressable 설정
            "does not have any associated AddressableAssetGroupSchemas",

            // 사용자 Unity 설치에 WebGL 모듈 미설치 — SDK-DD
            "Build target 'WebGL' not supported",
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
        private static readonly string[] ExternalAitPrefixes =
        {
            "[AIT Login]",
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
                if (message != null && message.IndexOf(AitKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                if (stackTrace != null && stackTrace.IndexOf(AitKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
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
            // 식별자형 키워드(영숫자/언더스코어/하이픈)는 단어 경계를 요구해
            // 사용자 경로(예: Assets/FTR_AppsInToss/...)에 부분 문자열로 들어가도
            // SDK 가드가 잘못 발동하지 않게 한다. prefix형 키워드("[AIT", "AIT:")는
            // 비식별자 문자를 포함하므로 substring 매치한다 ("[AITWarn]"처럼
            // 다른 토큰의 시작 prefix로 사용되어야 함).
            for (int i = 0; i < AitKeywords.Length; i++)
            {
                string keyword = AitKeywords[i];
                bool match = IsIdentifierToken(keyword)
                    ? ContainsKeywordAtBoundary(message, keyword)
                    : message.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
                if (match)
                    return true;
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

            // SDK 자체 로그는 절대 필터링하지 않음 — AitKeywords 전체를 가드로 사용
            if (MessageContainsSdkKeyword(message))
                return false;

            for (int i = 0; i < NonSdkMessagePatterns.Length; i++)
            {
                if (message.IndexOf(NonSdkMessagePatterns[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            // "Script attached to ... is missing" 패턴은 Assets/ 경로가 포함된 경우에만 사용자 프로젝트로 분류
            if (message.IndexOf("Script attached to", StringComparison.Ordinal) >= 0
                && message.IndexOf("is missing", StringComparison.Ordinal) >= 0
                && message.IndexOf("Assets/", StringComparison.Ordinal) >= 0)
                return true;

            // "GUID [...] ... conflicts with:" 패턴 — 사용자 프로젝트에 동일 GUID 에셋 잔존 (SDK-BQ)
            // AITTemplate 경로가 포함되더라도 SDK가 제공한 파일을 사용자가 복사해둔 경우이므로 필터 대상
            if (message.IndexOf("GUID [", StringComparison.Ordinal) >= 0
                && message.IndexOf("conflicts with:", StringComparison.Ordinal) >= 0)
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
