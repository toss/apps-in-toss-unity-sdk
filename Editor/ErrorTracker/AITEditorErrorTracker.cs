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

            // Dev/Production Server 프로세스에서 리디렉션된 로그는 Sentry에서 제외
            // (granite dev의 stdout/stderr가 Debug.Log/LogError/LogWarning으로 전달된 것)
            if (IsServerRedirectedLog(message))
                return;

            if (!IsAitRelated(message, stackTrace))
                return;

            // Unity PackageManager가 Git 패키지 업데이트 시 발생시키는 immutable 패키지 경고는 무시
            // (SDK 자동 업데이트 과정에서 정상적으로 발생할 수 있는 경고)
            if (type == LogType.Warning && message != null
                && message.IndexOf("immutable packages", StringComparison.OrdinalIgnoreCase) >= 0)
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
                level: level,
                tags: tags,
                breadcrumbs: _breadcrumbs.Count > 0 ? new List<AITSentryEnvelope.Breadcrumb>(_breadcrumbs) : null,
                fingerprint: fingerprint,
                release: GetRelease(),
                environment: ENVIRONMENT
            );

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

        private const string SdkPackagePath = "Packages/im.toss.apps-in-toss-unity-sdk/";
        private const string SdkPackageCachePath = "Library/PackageCache/im.toss.apps-in-toss-unity-sdk@";
        private const string SdkPackageCachePathNoVersion = "Library/PackageCache/im.toss.apps-in-toss-unity-sdk/";
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
                            filename.StartsWith(SdkPackageCachePathNoVersion, StringComparison.Ordinal))
                            return "sdk";

                        if (filename.StartsWith(UserProjectPathPrefix, StringComparison.Ordinal))
                            return "user_project";
                    }
                }
            }

            // 스택트레이스가 없거나 판별 불가한 경우, 메시지의 [AIT] 접두사로 판단
            if (!string.IsNullOrEmpty(message) && message.StartsWith("[AIT]", StringComparison.Ordinal))
                return "sdk";

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
