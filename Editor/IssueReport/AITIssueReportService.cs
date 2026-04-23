using System;
using System.Collections.Generic;
using AppsInToss.Editor.ErrorTracker;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor.IssueReport
{
    /// <summary>
    /// 이슈 제보를 Sentry User Feedback 경로로 전송하는 서비스.
    /// - Manual: 새 message event + user_report 두 아이템을 한 envelope에 묶어 전송
    /// - BuildFailure with LinkedEventId: 기존 에러 이벤트에 user_report만 추가 연결
    /// - BuildFailure without LinkedEventId: Manual fallback (경고 후 새 이벤트 생성)
    /// 전송은 <see cref="AITSentryTransport.SendEnvelope"/> 경로를 재사용합니다.
    /// </summary>
    internal static class AITIssueReportService
    {
        // 로그 버퍼에서 breadcrumb으로 전환할 최대 개수 (에러/경고/인포 버킷별 상한).
        // 총 최대 60개로 유지하여 envelope 크기가 과도해지지 않도록 함.
        private const int MaxErrorBreadcrumbs = 30;
        private const int MaxWarningBreadcrumbs = 15;
        private const int MaxInfoBreadcrumbs = 15;

        internal struct SubmitRequest
        {
            public AITIssueReportContext Context;
            public string Title;
            public string Body;
            public string Email;
            /// <summary>Task 7a 스크린샷 연결은 보류. 현재 값은 무시됩니다.</summary>
            public bool IncludeScreenshot;
            public string LinkedEventId;
        }

        internal struct SendResult
        {
            public bool Success;
            public string ErrorMessage;
        }

        /// <summary>
        /// 요청을 비동기 전송합니다. DSN이 설정되어 있지 않거나 envelope 빌드 중 예외가
        /// 발생하면 다음 에디터 프레임에서 실패 콜백을 호출합니다.
        /// </summary>
        /// <remarks>
        /// Unity 메인 스레드에서만 호출해야 합니다. 내부적으로 <c>Application.unityVersion</c>,
        /// <c>Application.platform</c>, <c>SystemInfo.deviceName</c> 등 메인 스레드 전용 API를
        /// 참조합니다. 백그라운드 스레드에서 호출하면 <c>UnityException</c>이 발생할 수 있습니다.
        /// </remarks>
        internal static void SendAsync(SubmitRequest request, Action<SendResult> onComplete)
        {
            string dsn = AITEditorErrorTracker.GetDsn();
            if (string.IsNullOrEmpty(dsn))
            {
                EditorApplication.delayCall += () => onComplete?.Invoke(new SendResult
                {
                    Success = false,
                    ErrorMessage = "DSN이 설정되지 않았습니다",
                });
                return;
            }

            string envelope;
            try
            {
                envelope = BuildEnvelopeForTest(request, dsn);
            }
            catch (Exception e)
            {
                EditorApplication.delayCall += () => onComplete?.Invoke(new SendResult
                {
                    Success = false,
                    ErrorMessage = $"전송 준비 중 오류: {e.Message}",
                });
                return;
            }

            AITSentryTransport.SendEnvelope(envelope, result =>
            {
                onComplete?.Invoke(new SendResult
                {
                    Success = result.Success,
                    ErrorMessage = result.ErrorMessage,
                });
            });
        }

        /// <summary>
        /// 테스트 가시성을 위해 envelope 문자열만 빌드합니다 (전송은 수행하지 않음).
        /// <see cref="SendAsync"/>의 envelope 구성과 동일합니다.
        /// </summary>
        internal static string BuildEnvelopeForTest(SubmitRequest request, string dsn)
        {
            string email = string.IsNullOrWhiteSpace(request.Email)
                ? BuildAutoEmail()
                : request.Email;
            string name = Environment.UserName;

            // BuildFailure + LinkedEventId: 기존 에러 이벤트에 user_report만 붙임
            if (request.Context == AITIssueReportContext.BuildFailure
                && !string.IsNullOrEmpty(request.LinkedEventId))
            {
                return AITSentryEnvelope.BuildStandaloneUserReportEnvelope(
                    dsn: dsn,
                    eventId: request.LinkedEventId,
                    email: email,
                    name: name,
                    comments: $"{request.Title}\n\n{request.Body}");
            }

            // Fallback 경고: BuildFailure 였는데 연결할 event가 없으면 Manual처럼 새 이벤트 생성
            if (request.Context == AITIssueReportContext.BuildFailure)
            {
                Debug.LogWarning("[AIT] 이전 에러 이벤트를 찾지 못해 새 이벤트로 전송합니다.");
            }

            string feedbackSource = request.Context == AITIssueReportContext.BuildFailure
                ? "build_failure_dialog"
                : "manual_menu";

            var tags = new Dictionary<string, string>
            {
                { "feedback.source", feedbackSource },
                { "ait.sdk_version", AITVersion.Version },
                { "ait.unity_version", Application.unityVersion },
                { "editor.platform", Application.platform.ToString() },
            };

            var breadcrumbs = BuildBreadcrumbsFromRecentLogs();

            string messageEventEnvelope = AITSentryEnvelope.BuildMessageEventEnvelope(
                dsn: dsn,
                message: request.Title,
                level: "info",
                tags: tags,
                breadcrumbs: breadcrumbs,
                release: null,
                environment: null,
                eventId: out string eventId);

            string userReport = AITSentryEnvelope.BuildUserReportItem(
                eventId: eventId,
                email: email,
                name: name,
                comments: request.Body);

            return messageEventEnvelope + "\n" + userReport;
        }

        private static List<AITSentryEnvelope.Breadcrumb> BuildBreadcrumbsFromRecentLogs()
        {
            var logs = AITErrorReporter.GetRecentLogs(
                maxErrors: MaxErrorBreadcrumbs,
                maxWarnings: MaxWarningBreadcrumbs,
                maxInfo: MaxInfoBreadcrumbs);

            if (logs == null || logs.Count == 0)
                return null;

            var result = new List<AITSentryEnvelope.Breadcrumb>(logs.Count);
            for (int i = 0; i < logs.Count; i++)
            {
                var log = logs[i];
                result.Add(new AITSentryEnvelope.Breadcrumb
                {
                    Timestamp = log.Timestamp.ToUniversalTime(),
                    Category = "console",
                    Message = log.Message,
                    Level = MapLogTypeToSentryLevel(log.Type),
                });
            }
            result.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
            return result;
        }

        private static string BuildAutoEmail()
        {
            string user = System.Text.RegularExpressions.Regex.Replace(
                Environment.UserName ?? "unknown", @"[^a-zA-Z0-9\-_.]", "-");
            string host = System.Text.RegularExpressions.Regex.Replace(
                SystemInfo.deviceName ?? "unity", @"[^a-zA-Z0-9\-_.]", "-");
            if (string.IsNullOrEmpty(user)) user = "unknown";
            if (string.IsNullOrEmpty(host)) host = "unity";
            return $"{user}@{host}";
        }

        private static string MapLogTypeToSentryLevel(LogType type)
        {
            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                    return "error";
                case LogType.Warning:
                    return "warning";
                default:
                    return "info";
            }
        }
    }
}
