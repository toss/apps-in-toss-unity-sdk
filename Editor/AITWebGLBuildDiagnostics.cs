using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AppsInToss
{
    /// <summary>
    /// Unity WebGL 빌드 결과 해석 헬퍼(#36으로 AITConvertCore에서 분리). BuildResult→AIT 에러 코드
    /// 매핑, 빌드 실패 요약 문자열 구성, 무진단 실패 대비 에디터 로그 꼬리 읽기를 담당하는 순수/준순수
    /// 유틸리티입니다. 동작은 분리 전과 동일합니다(behavior-preserving).
    ///
    /// 주의: 이름이 비슷한 <see cref="Editor.AITBuildDiagnostics"/>(AppsInToss.Editor)와는 별개입니다 —
    /// 그쪽은 외부 명령 실패의 exit code/stderr를 Sentry extra로 싣는 상태 버퍼이고, 이 클래스는
    /// 무상태 결과 해석/요약 함수 모음입니다. 공개 에러 메시지 변환(GetErrorMessage 등)과
    /// AITExportError 열거형은 공개 계약이라 AITConvertCore에 남습니다.
    /// </summary>
    internal static class AITWebGLBuildDiagnostics
    {
        /// <summary>
        /// Unity의 BuildResult를 AIT 내부 에러 코드로 매핑합니다.
        /// Cancelled는 사용자 의사이므로 SDK 결함 보고 대상이 아니며 CANCELLED로 매핑됩니다.
        /// </summary>
        internal static AITConvertCore.AITExportError MapBuildResultToExportError(
            UnityEditor.Build.Reporting.BuildResult result)
        {
            switch (result)
            {
                case UnityEditor.Build.Reporting.BuildResult.Succeeded:
                    return AITConvertCore.AITExportError.SUCCEED;
                case UnityEditor.Build.Reporting.BuildResult.Cancelled:
                    return AITConvertCore.AITExportError.CANCELLED;
                default:
                    return AITConvertCore.AITExportError.BUILD_WEBGL_FAILED;
            }
        }

        // WebGL 빌드 실패 진단에서 에러/경고에 부여하는 '개별' 캡(슬롯).
        // 과거에는 단일 maxMessages(10)를 에러·경고가 공유해, 에러가 10개를 채우면 경고가
        // 한 줄도 안 보이고 생략 카운트도 심각도 구분 없이 합산돼 모호했다(이 PR이 해소하는 갭).
        // 캡을 분리해 에러를 우선 보장하고 경고에도 별도 예산을 준다.
        internal const int FailureSummaryMaxErrors = 10;
        internal const int FailureSummaryMaxWarnings = 5;

        /// <summary>
        /// WebGL 빌드 실패 진단 요약 문자열을 구성합니다. 에러/경고 캡을 분리(에러 우선)하여
        /// 한쪽이 다른 쪽의 슬롯을 잠식하지 않게 하고, 생략 개수를 심각도별로 분리 표기합니다.
        /// <paramref name="messages"/>에 에러/경고가 하나도 없을 때(예: Bee/IL2CPP의 .o 컴파일
        /// 실패가 BuildReport.steps 메시지로 잡히지 않는 무진단 실패)에 한해
        /// <paramref name="logTail"/>(에디터 로그 꼬리)을 첨부해 단서를 남깁니다.
        /// 순수 함수로 분리되어 EditMode에서 BuildReport 없이 검증 가능합니다.
        /// </summary>
        internal static string BuildFailureSummary(
            string resultLabel,
            int summaryTotalErrors,
            int summaryTotalWarnings,
            List<(LogType type, string content)> messages,
            string logTail,
            int maxErrors = FailureSummaryMaxErrors,
            int maxWarnings = FailureSummaryMaxWarnings)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[AIT] WebGL 빌드가 실패했습니다.");
            sb.AppendLine($"  결과: {resultLabel}");
            sb.AppendLine($"  총 에러: {summaryTotalErrors}, 총 경고: {summaryTotalWarnings}");

            int shownErrors = 0, omittedErrors = 0;
            int shownWarnings = 0, omittedWarnings = 0;

            if (messages != null)
            {
                // 에러(Error/Exception/Assert) 먼저 출력 — 실패 원인 우선.
                foreach (var m in messages)
                {
                    if (m.type == LogType.Error || m.type == LogType.Exception || m.type == LogType.Assert)
                    {
                        if (shownErrors < maxErrors)
                        {
                            sb.AppendLine($"  [{m.type}] {m.content}");
                            shownErrors++;
                        }
                        else
                        {
                            omittedErrors++;
                        }
                    }
                }

                // 경고는 에러 캡과 무관한 별도 예산으로 출력(에러가 경고 슬롯을 잠식하지 않음).
                foreach (var m in messages)
                {
                    if (m.type == LogType.Warning)
                    {
                        if (shownWarnings < maxWarnings)
                        {
                            sb.AppendLine($"  [{m.type}] {m.content}");
                            shownWarnings++;
                        }
                        else
                        {
                            omittedWarnings++;
                        }
                    }
                }
            }

            if (omittedErrors > 0 || omittedWarnings > 0)
            {
                sb.AppendLine($"  ... 생략: 에러 {omittedErrors}개, 경고 {omittedWarnings}개");
            }

            // step 메시지가 하나도 없을 때만 로그 꼬리를 첨부(Bee/IL2CPP .o 무진단 실패 대비).
            bool hadAnyStepMessage =
                shownErrors > 0 || shownWarnings > 0 || omittedErrors > 0 || omittedWarnings > 0;
            if (!hadAnyStepMessage && !string.IsNullOrEmpty(logTail))
            {
                sb.AppendLine("  [진단] BuildReport.steps에 메시지가 없습니다. 에디터 로그 꼬리:");
                sb.Append(logTail);
            }

            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 에디터 로그 파일의 마지막 <paramref name="maxBytes"/>바이트(꼬리)를 읽어 반환합니다.
        /// Bee/IL2CPP의 .o 컴파일 실패처럼 BuildReport.steps에 잡히지 않는 무진단 실패의 단서를
        /// 남기기 위한 보조 진단 경로입니다. 파일이 에디터에 의해 열려 있을 수 있으므로 공유 읽기
        /// 모드로 접근하고, 어떤 예외도 삼켜 <c>null</c>을 반환합니다(진단 보조라 실패가 빌드 흐름을
        /// 막으면 안 됨). UTF-8 다바이트 경계가 잘릴 수 있으나 진단용으로 허용합니다.
        /// </summary>
        internal static string ReadEditorLogTail(string logPath, int maxBytes = 4000)
        {
            try
            {
                if (string.IsNullOrEmpty(logPath) || !File.Exists(logPath))
                    return null;

                using (var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long len = fs.Length;
                    if (len <= 0)
                        return null;

                    int toRead = (int)Math.Min(len, (long)Math.Max(0, maxBytes));
                    if (toRead <= 0)
                        return null;

                    fs.Seek(-toRead, SeekOrigin.End);
                    var buffer = new byte[toRead];
                    int offset = 0;
                    while (offset < toRead)
                    {
                        int read = fs.Read(buffer, offset, toRead - offset);
                        if (read <= 0)
                            break;
                        offset += read;
                    }

                    return System.Text.Encoding.UTF8.GetString(buffer, 0, offset);
                }
            }
            catch
            {
                return null;
            }
        }
    }
}
