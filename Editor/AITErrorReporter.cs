using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 이슈 제보용 컨텍스트 수집 유틸리티 - 콘솔 로그 버퍼와 빌드 리포트 포맷팅을 제공합니다.
    /// 에디터 로그를 순환 버퍼에 저장하고, 이슈 제보 시 breadcrumb 또는 본문으로 변환할 수 있습니다.
    /// </summary>
    [InitializeOnLoad]
    public static class AITErrorReporter
    {
        // 로그 타입별 최대 저장 개수
        internal const int MAX_ERROR_LOGS = 50;
        internal const int MAX_WARNING_LOGS = 30;
        internal const int MAX_INFO_LOGS = 20;

        /// <summary>
        /// 로그 엔트리 저장용 구조체
        /// </summary>
        internal readonly struct LogEntry
        {
            public readonly string Message;
            public readonly string StackTrace;
            public readonly LogType Type;
            public readonly DateTime Timestamp;

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
        /// 빌드 리포트 저장 (AITWebGLBuilder.BuildWebGL에서 호출)
        /// </summary>
        public static void SetBuildReport(BuildReport report)
        {
            lastBuildReport = report;
        }

        /// <summary>
        /// BuildReport에서 에러 정보 추출
        /// </summary>
        internal static string FormatBuildErrors()
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
        /// 단일 로그 엔트리 포맷팅
        /// </summary>
        internal static string FormatLogEntry(LogEntry entry)
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
        /// 최근 로그 스냅샷을 반환합니다. 각 버킷은 에러 → 경고 → 인포 순으로 이어지며,
        /// 각 버킷 내부에서는 오래된 순으로 정렬됩니다.
        /// 반환 리스트는 호출 시점의 스냅샷이므로 이후 버퍼 변경에 영향받지 않습니다.
        /// 각 인자가 0이면 해당 타입 로그는 결과에서 제외됩니다.
        /// </summary>
        internal static IReadOnlyList<LogEntry> GetRecentLogs(
            int maxErrors = MAX_ERROR_LOGS,
            int maxWarnings = MAX_WARNING_LOGS,
            int maxInfo = MAX_INFO_LOGS)
        {
            var result = new List<LogEntry>(maxErrors + maxWarnings + maxInfo);
            TakeTail(errorLogs, maxErrors, result);
            TakeTail(warningLogs, maxWarnings, result);
            TakeTail(infoLogs, maxInfo, result);
            return result;
        }

        private static void TakeTail(List<LogEntry> src, int count, List<LogEntry> dest)
        {
            int start = Math.Max(0, src.Count - count);
            for (int i = start; i < src.Count; i++)
            {
                dest.Add(src[i]);
            }
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
