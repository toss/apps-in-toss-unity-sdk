using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 에러 리포트 유틸리티 - GitHub Issue로 빌드 에러를 쉽게 리포트
    /// </summary>
    [InitializeOnLoad]
    public static class AITErrorReporter
    {
        private const string GITHUB_REPO = "toss/apps-in-toss-unity-sdk";

        // 로그 타입별 최대 저장 개수
        private const int MAX_ERROR_LOGS = 50;
        private const int MAX_WARNING_LOGS = 30;
        private const int MAX_INFO_LOGS = 20;

        // GitHub URL 길이 제한 (브라우저/서버 호환성을 위해 보수적으로 설정)
        private const int MAX_URL_LENGTH = 2000;

        /// <summary>
        /// 로그 엔트리 저장용 구조체
        /// </summary>
        private struct LogEntry
        {
            public string Message;
            public string StackTrace;
            public LogType Type;
            public DateTime Timestamp;

            public LogEntry(string message, string stackTrace, LogType type)
            {
                Message = message;
                StackTrace = stackTrace;
                Type = type;
                Timestamp = DateTime.Now;
            }
        }

        // 타입별 로그 저장 (순환 버퍼)
        private static readonly List<LogEntry> errorLogs = new List<LogEntry>();
        private static readonly List<LogEntry> warningLogs = new List<LogEntry>();
        private static readonly List<LogEntry> infoLogs = new List<LogEntry>();

        // 마지막 빌드 리포트 저장
        private static BuildReport lastBuildReport;

        static AITErrorReporter()
        {
            Application.logMessageReceived += CaptureLog;
        }

        /// <summary>
        /// 로그 캡처 (타입별 분류 저장)
        /// </summary>
        private static void CaptureLog(string message, string stackTrace, LogType type)
        {
            var entry = new LogEntry(message, stackTrace, type);

            switch (type)
            {
                case LogType.Error:
                case LogType.Exception:
                    AddToBuffer(errorLogs, entry, MAX_ERROR_LOGS);
                    break;
                case LogType.Warning:
                    AddToBuffer(warningLogs, entry, MAX_WARNING_LOGS);
                    break;
                default: // Log, Assert
                    AddToBuffer(infoLogs, entry, MAX_INFO_LOGS);
                    break;
            }
        }

        /// <summary>
        /// 순환 버퍼에 로그 추가
        /// </summary>
        private static void AddToBuffer(List<LogEntry> buffer, LogEntry entry, int maxSize)
        {
            if (buffer.Count >= maxSize)
            {
                buffer.RemoveAt(0);
            }
            buffer.Add(entry);
        }

        /// <summary>
        /// 빌드 리포트 저장 (AITConvertCore.BuildWebGL에서 호출)
        /// </summary>
        public static void SetBuildReport(BuildReport report)
        {
            lastBuildReport = report;
        }

        /// <summary>
        /// GitHub Issue URL 생성 및 브라우저에서 열기
        /// </summary>
        public static void OpenIssueInBrowser(AITConvertCore.AITExportError errorCode, string profileName = null)
        {
            string issueUrl = GenerateIssueUrl(errorCode, profileName);
            Application.OpenURL(issueUrl);
            Debug.Log($"[AIT] GitHub Issue 페이지를 열었습니다: {issueUrl}");
        }

        /// <summary>
        /// GitHub Issue URL 생성
        /// </summary>
        private static string GenerateIssueUrl(AITConvertCore.AITExportError errorCode, string profileName)
        {
            string title = $"[빌드 에러] {errorCode}";

            // 초기 body 생성 (제한 없이)
            string body = GenerateIssueBody(errorCode, profileName);
            string url = BuildIssueUrl(title, body);

            // 인코딩된 URL이 제한 초과시 body를 점진적으로 줄임
            int maxBodyChars = body.Length;
            while (url.Length > MAX_URL_LENGTH && maxBodyChars > 100)
            {
                maxBodyChars = maxBodyChars * 2 / 3; // 33%씩 감소
                body = GenerateIssueBody(errorCode, profileName, maxLength: maxBodyChars);
                url = BuildIssueUrl(title, body);
            }

            // 그래도 초과시 최소 body로 대체
            if (url.Length > MAX_URL_LENGTH)
            {
                body = $"## 에러 정보\n- 에러 코드: `{errorCode}`\n\n환경 정보와 에러 내용은 Issue에 직접 작성해주세요.";
                url = BuildIssueUrl(title, body);
            }

            return url;
        }

        /// <summary>
        /// GitHub Issue URL 빌드 헬퍼
        /// </summary>
        private static string BuildIssueUrl(string title, string body)
        {
            string encodedTitle = Uri.EscapeDataString(title);
            string encodedBody = Uri.EscapeDataString(body);
            return $"https://github.com/{GITHUB_REPO}/issues/new?title={encodedTitle}&body={encodedBody}&labels=bug";
        }

        /// <summary>
        /// Issue body 생성
        /// </summary>
        private static string GenerateIssueBody(AITConvertCore.AITExportError errorCode, string profileName, int maxLength = 0)
        {
            var sb = new StringBuilder();
            var config = UnityUtil.GetEditorConf();

            // 환경 정보
            sb.AppendLine("## 환경 정보");
            sb.AppendLine($"- **SDK 버전**: {AITVersion.FullVersion}");
            sb.AppendLine($"- **Unity 버전**: {Application.unityVersion}");
            sb.AppendLine($"- **에디터 플랫폼**: {SystemInfo.operatingSystem}");
            if (!string.IsNullOrEmpty(profileName))
            {
                sb.AppendLine($"- **빌드 프로필**: {profileName}");
            }
            sb.AppendLine();

            // 에러 정보
            sb.AppendLine("## 에러 정보");
            sb.AppendLine($"- **에러 코드**: `{errorCode}`");
            sb.AppendLine($"- **에러 메시지**: {AITConvertCore.GetErrorMessage(errorCode)}");
            sb.AppendLine();

            // 앱 설정
            if (config != null)
            {
                sb.AppendLine("## 앱 설정");
                if (!string.IsNullOrEmpty(config.appName))
                {
                    sb.AppendLine($"- **앱 ID**: {config.appName}");
                }
                if (!string.IsNullOrEmpty(config.version))
                {
                    sb.AppendLine($"- **버전**: {config.version}");
                }
                sb.AppendLine();
            }

            // 빌드 에러 (BuildReport)
            string buildErrors = FormatBuildErrors();
            if (!string.IsNullOrEmpty(buildErrors))
            {
                sb.AppendLine("## 빌드 에러 (BuildReport)");
                sb.AppendLine("```");
                sb.AppendLine(buildErrors);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            // 콘솔 로그 (길이 제한 고려)
            int remainingLength = maxLength > 0 ? maxLength - sb.Length - 200 : int.MaxValue;
            string consoleLogs = FormatLogsForIssue(remainingLength);
            if (!string.IsNullOrEmpty(consoleLogs))
            {
                sb.AppendLine("## 최근 콘솔 로그");
                sb.AppendLine("```");
                sb.AppendLine(consoleLogs);
                sb.AppendLine("```");
                sb.AppendLine();
            }

            // 추가 컨텍스트
            sb.AppendLine("## 추가 컨텍스트");
            sb.AppendLine("<!-- 여기에 추가 정보를 입력해주세요 -->");
            sb.AppendLine();

            return sb.ToString();
        }

        /// <summary>
        /// BuildReport에서 에러 정보 추출
        /// </summary>
        private static string FormatBuildErrors()
        {
            if (lastBuildReport == null)
            {
                return null;
            }

            var sb = new StringBuilder();

            if (lastBuildReport.summary.result != BuildResult.Succeeded)
            {
                sb.AppendLine($"Build failed with {lastBuildReport.summary.totalErrors} error(s), {lastBuildReport.summary.totalWarnings} warning(s)");

                // 빌드 스텝의 메시지 수집
                foreach (var step in lastBuildReport.steps)
                {
                    foreach (var message in step.messages)
                    {
                        if (message.type == LogType.Error || message.type == LogType.Exception)
                        {
                            sb.AppendLine(message.content);
                        }
                    }
                }
            }

            return sb.Length > 0 ? sb.ToString().TrimEnd() : null;
        }

        /// <summary>
        /// 콘솔 로그 포맷팅 (URL 길이에 맞춰 조정)
        /// </summary>
        private static string FormatLogsForIssue(int remainingChars)
        {
            var sb = new StringBuilder();
            int currentLength = 0;

            // 1. Error 로그 우선 (모두 포함 시도)
            foreach (var log in errorLogs)
            {
                string formatted = FormatLogEntry(log);
                if (remainingChars > 0 && currentLength + formatted.Length > remainingChars)
                {
                    sb.AppendLine("... (로그가 너무 길어 일부 생략됨)");
                    break;
                }
                sb.AppendLine(formatted);
                currentLength += formatted.Length;
            }

            // 2. Warning 로그 (남은 공간에 맞춰)
            if (remainingChars <= 0 || currentLength < remainingChars - 100)
            {
                foreach (var log in warningLogs)
                {
                    string formatted = FormatLogEntry(log);
                    if (remainingChars > 0 && currentLength + formatted.Length > remainingChars)
                    {
                        break;
                    }
                    sb.AppendLine(formatted);
                    currentLength += formatted.Length;
                }
            }

            // 3. Info 로그 (남은 공간에 맞춰)
            if (remainingChars <= 0 || currentLength < remainingChars - 100)
            {
                foreach (var log in infoLogs)
                {
                    string formatted = FormatLogEntry(log);
                    if (remainingChars > 0 && currentLength + formatted.Length > remainingChars)
                    {
                        break;
                    }
                    sb.AppendLine(formatted);
                    currentLength += formatted.Length;
                }
            }

            return sb.Length > 0 ? sb.ToString().TrimEnd() : null;
        }

        /// <summary>
        /// 단일 로그 엔트리 포맷팅
        /// </summary>
        private static string FormatLogEntry(LogEntry entry)
        {
            string typeTag = entry.Type switch
            {
                LogType.Error => "[Error]",
                LogType.Exception => "[Exception]",
                LogType.Warning => "[Warning]",
                LogType.Assert => "[Assert]",
                _ => "[Log]"
            };

            // 메시지가 너무 길면 잘라내기
            string message = entry.Message;
            if (message.Length > 500)
            {
                message = message.Substring(0, 500) + "...";
            }

            // 스택 트레이스 첫 줄만 포함 (있는 경우)
            if (!string.IsNullOrEmpty(entry.StackTrace) &&
                (entry.Type == LogType.Error || entry.Type == LogType.Exception))
            {
                string[] stackLines = entry.StackTrace.Split('\n');
                if (stackLines.Length > 0 && !string.IsNullOrWhiteSpace(stackLines[0]))
                {
                    return $"{typeTag} {message}\n  at {stackLines[0].Trim()}";
                }
            }

            return $"{typeTag} {message}";
        }

        /// <summary>
        /// 로그 버퍼 초기화 (테스트용)
        /// </summary>
        public static void ClearLogs()
        {
            errorLogs.Clear();
            warningLogs.Clear();
            infoLogs.Clear();
            lastBuildReport = null;
        }
    }
}
