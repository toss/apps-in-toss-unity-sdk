using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor.ErrorTracker
{
    /// <summary>
    /// Error Tracker 메인 오케스트레이터.
    /// 에러 캡처, 세션 관리, 텔레메트리 전송을 총괄합니다.
    /// </summary>
    [InitializeOnLoad]
    internal static partial class AITEditorErrorTracker
    {
        #region Constants

        // 빈 문자열이면 에러 트래킹이 비활성화됩니다.
        // DSN의 public key는 클라이언트 측에서 사용하도록 설계되어 있으며 (Sentry 공식 문서 참조),
        // Sentry 측 inbound filter와 rate limiting으로 보호됩니다.
        private const string DEFAULT_DSN = "https://af6caf8b80107bc41edf37baff728a5d@o89496.ingest.us.sentry.io/4511182309359616";

        // 에디터 트래커 DSN을 빌드/CI 환경에서 재정의할 수 있는 환경변수 이름.
        // 미설정(혹은 공백)이면 DEFAULT_DSN으로 폴백하므로 기본 동작은 변하지 않는다(순수 추가·가역적).
        // 런타임(플레이어) Sentry DSN을 주입하는 SENTRY_DSN(AITSentryDsnInjector)과는 의도적으로
        // 분리한다: 에디터 트래커는 SDK 자체 Sentry 프로젝트로, 런타임은 게임 개발자의 프로젝트로
        // 향하므로 같은 변수를 공유하면 에디터 텔레메트리가 게임 런타임 프로젝트로 오라우팅된다.
        private const string DSN_ENV_VAR = "AIT_EDITOR_SENTRY_DSN";
        private const string RELEASE_PREFIX = "apps-in-toss.unity";
        private const string ENVIRONMENT = "editor";
        private const int MAX_BREADCRUMBS = 20;
        private const int MAX_DEDUP_ENTRIES = 200;
        private static readonly TimeSpan DedupWindow = TimeSpan.FromMinutes(1);

        #endregion

        // AIT Keywords 영역은 AITEditorErrorTracker.NoiseFilter.cs로 옮겼다.

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
        /// 환경변수 override(<see cref="DSN_ENV_VAR"/>)가 설정되어 있으면 그 값을 기준으로 판정합니다.
        /// </summary>
        internal static bool IsDsnConfigured => !string.IsNullOrEmpty(GetDsn());

        /// <summary>
        /// 현재 설정된 Sentry DSN을 반환합니다.
        /// 환경변수 <see cref="DSN_ENV_VAR"/>가 비어있지 않게 설정되어 있으면 그 값을, 아니면 DEFAULT_DSN을 반환합니다.
        /// </summary>
        internal static string GetDsn()
        {
            return ResolveEditorDsn(Environment.GetEnvironmentVariable(DSN_ENV_VAR));
        }

        /// <summary>
        /// 환경변수 override 적용 규칙(순수 함수 — 프로세스 env 없이 테스트 가능).
        /// override가 null/공백이면 DEFAULT_DSN으로 폴백하고, 그 외에는 trim한 override 값을 사용합니다.
        /// </summary>
        internal static string ResolveEditorDsn(string overrideValue)
        {
            return string.IsNullOrWhiteSpace(overrideValue) ? DEFAULT_DSN : overrideValue.Trim();
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

            // SDK 브레이킹 체인지 추적: 사용자 프로젝트가 SDK API 변경으로 컴파일에 실패한 경우,
            // IsKnownNonSdkMessage가 이 컴파일 에러를 노이즈로 드롭하기 전에 가로채 별도 버킷
            // (error_source=sdk_breaking_change, exceptionType=SdkBreakingChange)으로 캡처해
            // 릴리즈가 제휴사 프로젝트 컴파일을 깨뜨린 영향을 추적한다. 에디터 전용 트래커라
            // 볼륨은 개발자 세션 수로 제한되고 dedup/fingerprint로 추가 제한되며, distinct
            // exceptionType/fingerprint라 기존에 ignored 처리된 SDK 자체 이슈를 재오픈하지 않는다.
            if (type == LogType.Error && IsSdkBreakingChangeCompileError(message))
            {
                CaptureSdkBreakingChange(message, stackTrace);
                return;
            }

            // SDK 자체 패키지 경로의 컴파일 에러(error CS#### in im.toss/com.toss.apps-in-toss)는
            // UPM 임포트/재컴파일 중 심볼 정의 어셈블리가 컴파일되기 전 일시적으로 발생할 수 있다
            // (transient 임포트 아티팩트, 0-user 노이즈 — SDK-133/130/131/12Z/12P/12W/12V/12T/12S).
            // 즉시 캡처하면 노이즈가 새므로 defer+confirm 게이트로 보내, 성공적인 도메인 리로드를
            // 거치지 못한(= 컴파일이 끝내 실패한 실 회귀) 경우에만 캡처한다(#42). 일반 경로보다 먼저
            // 가로채야 하므로 ShouldDropAsNonSdkSource/CaptureError 직전인 이 위치에 둔다.
            if (type == LogType.Error && AITSdkSelfCompileGuard.IsSdkSelfCompileError(message))
            {
                AITSdkSelfCompileGuard.Defer(message, stackTrace);
                return;
            }

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

            // 가변 토큰(경로/해시/버전/숫자/GUID)을 정규화한 안정적 fingerprint를 부여해
            // 동일 root cause가 Sentry에서 여러 이슈로 쪼개지는 fingerprint explosion을 방지한다.
            // 가변 토큰이 없는 메시지는 null을 반환하므로 기본 그룹화 동작은 그대로 유지된다.
            string[] fingerprint = BuildNormalizedFingerprint(exceptionType, message);

            CaptureError(exceptionType, message, stackTrace, level, fingerprint: fingerprint);
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
            string[] fingerprint = null,
            Dictionary<string, string> extra = null)
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
                extra: extra,
                breadcrumbs: _breadcrumbs.Count > 0 ? new List<AITSentryEnvelope.Breadcrumb>(_breadcrumbs) : null,
                fingerprint: fingerprint,
                release: GetRelease(),
                environment: ENVIRONMENT
            );

            _lastEventId = capturedEventId;
            AITSentryTransport.SendEnvelope(envelope);
        }

        /// <summary>
        /// 사용자 프로젝트 코드가 SDK API 변경(브레이킹 체인지)으로 컴파일에 실패했음을 나타내는
        /// C# 컴파일러 '에러'인지 판정합니다. SDK 심볼("AppsInToss")을 참조하는 "error CS####"만
        /// 매칭하며, 경고("warning CS")는 호환성 깨짐이 아니므로 제외합니다(기존 드롭 동작 유지).
        /// SDK 자체 로그("[AIT" prefix)와 SDK 패키지 경로(im.toss/com.toss.apps-in-toss)의 컴파일
        /// 에러는 실제 SDK 버그이므로 이 분류에서 제외하고 일반 경로(error_source=sdk)로 흘려보냅니다.
        ///
        /// <para>
        /// 매칭 시 <see cref="OnLogMessageReceived"/>가 <see cref="IsKnownNonSdkMessage"/>의 드롭
        /// (이 컴파일 에러들은 기존에 SDK-80/C3/M7 등으로 ignored 처리되어 드롭됨)보다 먼저 가로채,
        /// 별도 버킷(error_source=sdk_breaking_change)으로 캡처합니다.
        /// </para>
        /// </summary>
        internal static bool IsSdkBreakingChangeCompileError(string message)
        {
            if (string.IsNullOrEmpty(message))
                return false;

            // SDK 자체 로그/패키지 경로의 컴파일 에러는 실 SDK 버그이므로 별도 분류로 흘려보낸다.
            if (message.StartsWith("[AIT", StringComparison.Ordinal))
                return false;
            if (message.IndexOf("im.toss.apps-in-toss", StringComparison.Ordinal) >= 0
                || message.IndexOf("com.toss.apps-in-toss", StringComparison.Ordinal) >= 0)
                return false;

            // C# 컴파일러 '에러'(error CS####)이면서 SDK 심볼(AppsInToss)을 참조해야 한다.
            // 경고(warning CS####)는 호환성 깨짐이 아니므로 제외(기존 드롭 동작 유지).
            return message.IndexOf("error CS", StringComparison.Ordinal) >= 0
                && message.IndexOf("AppsInToss", StringComparison.Ordinal) >= 0;
        }

        /// <summary>
        /// SDK 브레이킹 체인지로 인한 사용자 컴파일 에러를 별도 버킷으로 캡처합니다.
        /// exceptionType "SdkBreakingChange" + error_source 태그 "sdk_breaking_change"로,
        /// 기존에 ignored 처리된 SDK 자체 이슈와 fingerprint가 겹치지 않게 분리합니다
        /// (<see cref="BuildNormalizedFingerprint"/>가 exceptionType을 fingerprint에 포함).
        /// </summary>
        private static void CaptureSdkBreakingChange(string message, string stackTrace)
        {
            var tags = new Dictionary<string, string>
            {
                { "error_source", "sdk_breaking_change" }
            };
            string[] fingerprint = BuildNormalizedFingerprint("SdkBreakingChange", message);
            CaptureError(
                exceptionType: "SdkBreakingChange",
                message: message,
                stackTrace: stackTrace,
                level: "error",
                extraTags: tags,
                fingerprint: fingerprint);
        }

        /// <summary>
        /// defer+confirm 게이트(<see cref="AITSdkSelfCompileGuard"/>)가 "성공적인 도메인 리로드로
        /// 해소되지 못한 = 컴파일이 끝내 실패한 실 회귀"로 확정한 SDK 자체 컴파일 에러를 캡처합니다(#42).
        /// 기존 일반 경로와 동일하게 exceptionType "UnityError" + 정규화 fingerprint로 캡처해 그룹화를
        /// 보존하되, transient 게이트를 통과(성공 리로드로 사라지지 않음)했음을 태그로 남겨 triage를 돕습니다.
        /// </summary>
        internal static void CaptureConfirmedSdkSelfCompileError(string message, string stackTrace)
        {
            var tags = new Dictionary<string, string>
            {
                { "compile_error_confirmed", "persisted_no_reload" }
            };
            string[] fingerprint = BuildNormalizedFingerprint("UnityError", message);
            CaptureError(
                exceptionType: "UnityError",
                message: message,
                stackTrace: stackTrace,
                level: "error",
                extraTags: tags,
                fingerprint: fingerprint);
        }

        // === fingerprint explosion 방지용 정규화 규칙 ===
        // 메시지에 박히는 가변 토큰(파일 경로, 해시, 버전, 숫자, GUID, glob 파일명)을 placeholder로 치환해
        // 같은 root cause의 변형들이 단일 Sentry 이슈로 묶이도록 한다. 에러 경로에서만 호출되므로(핫패스 아님)
        // Compiled 정규식을 정적으로 캐시한다. 치환 순서 중요: 긴 토큰(경로/GUID/버전)을 숫자보다 먼저 소비.
        private static readonly Regex _fpGuid =
            new Regex(@"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}", RegexOptions.Compiled);
        private static readonly Regex _fpWinPath =
            new Regex(@"[A-Za-z]:\\[^\s""'<>|]+", RegexOptions.Compiled);
        private static readonly Regex _fpUnixPath =
            new Regex(@"/(?:[\w.\-]+/)+[\w.\-]+", RegexOptions.Compiled);
        private static readonly Regex _fpHexHash =
            new Regex(@"\b[0-9a-fA-F]{16,}\b", RegexOptions.Compiled);
        private static readonly Regex _fpVersion =
            new Regex(@"\bv?\d+\.\d+(?:\.\d+)+(?:[\-+][0-9A-Za-z.\-]+)?\b", RegexOptions.Compiled);
        private static readonly Regex _fpGlobFile =
            new Regex(@"\*\.[A-Za-z][\w.]*", RegexOptions.Compiled);
        private static readonly Regex _fpNumber =
            new Regex(@"\d+", RegexOptions.Compiled);
        private static readonly Regex _fpCollapseFiles =
            new Regex(@"<file>(?:[,\s]+<file>)+", RegexOptions.Compiled);
        private static readonly Regex _fpWhitespace =
            new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>
        /// 로그 캡처 이벤트의 메시지에서 가변 토큰을 정규화해 안정적인 fingerprint를 생성합니다.
        /// 가변 토큰이 하나도 없으면(정규화 결과가 원본과 동일) <c>null</c>을 반환하여 Sentry 기본 그룹화를 그대로 둡니다.
        /// 반환 형식: <c>{ "ait-log", exceptionType, 정규화된 메시지 }</c> — "{{ default }}"를 포함하지 않으므로
        /// 가변 텍스트가 그룹화에 다시 끼어들지 않고 동일 root cause가 단일 이슈로 묶입니다.
        /// (빌드 에러 경로의 <see cref="CaptureBuildErrorInternal"/>는 errorCode 기반 fingerprint를 별도로 사용.)
        /// </summary>
        internal static string[] BuildNormalizedFingerprint(string exceptionType, string message)
        {
            if (string.IsNullOrEmpty(message))
                return null;

            string normalized = message;
            normalized = _fpGuid.Replace(normalized, "<guid>");
            normalized = _fpWinPath.Replace(normalized, "<path>");
            normalized = _fpUnixPath.Replace(normalized, "<path>");
            normalized = _fpHexHash.Replace(normalized, "<hash>");
            normalized = _fpVersion.Replace(normalized, "<ver>");
            normalized = _fpGlobFile.Replace(normalized, "<file>");
            normalized = _fpNumber.Replace(normalized, "<n>");

            // 가변 토큰이 전혀 치환되지 않았다면 fingerprint를 덮어쓰지 않고 기본 그룹화를 유지(기존 동작 불변).
            if (string.Equals(normalized, message, StringComparison.Ordinal))
                return null;

            // "<file>, <file>, <file>"처럼 나열된 누락 파일 목록을 하나로 접어 조합 폭발(예: SDK-ZM)을 방지.
            normalized = _fpCollapseFiles.Replace(normalized, "<file>");
            // 줄바꿈/연속 공백 정리로 동일 메시지의 미세 변형까지 같은 키로 수렴.
            normalized = _fpWhitespace.Replace(normalized, " ").Trim();

            // 정규화 결과가 과도하게 길면 상한을 둔다(긴 경로/스택 혼입 방지).
            if (normalized.Length > 200)
                normalized = normalized.Substring(0, 200);

            return new[]
            {
                "ait-log",
                string.IsNullOrEmpty(exceptionType) ? "unknown" : exceptionType,
                normalized
            };
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

            // pnpm/granite 등 외부 명령이 남긴 마지막 실패 진단(exit code + stderr 말미)을 extra로 첨부한다 (§5).
            // fingerprint는 여전히 errorCode 기반이므로 그룹화(이슈 1개)는 유지되고, 진단만 이벤트에 실린다.
            // extra(인덱싱되지 않는 자유 컨텍스트)에 담으므로 tag 길이/cardinality 제약과 무관하다.
            // 소비-once 정책상, 동일 세션에서 같은 errorCode가 CaptureError의 dedup으로 드롭되는 후속 캡처는
            // 진단도 함께 비워진다. 드롭된 이벤트엔 어차피 첨부할 곳이 없고, 먼저 전송된 대표 이벤트가 진단을
            // 이미 실어 보냈으므로 triage에는 영향이 없다(이슈당 대표 1건 + 진단 보존).
            Dictionary<string, string> extra = null;
            string buildDiag = AITBuildDiagnostics.ConsumeForCapture();
            if (!string.IsNullOrEmpty(buildDiag))
            {
                extra = new Dictionary<string, string>
                {
                    { "build_command_diagnostics", buildDiag }
                };
            }

            var fingerprint = new[] { "{{ default }}", errorCode.ToString() };

            CaptureError(
                exceptionType: exceptionType,
                message: errorMessage,
                stackTrace: null,
                level: "error",
                extraTags: extraTags,
                fingerprint: fingerprint,
                extra: extra
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

        // IsKnownNonSdkMessage 메서드는 AITEditorErrorTracker.NoiseFilter.cs로 옮겼다.

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
