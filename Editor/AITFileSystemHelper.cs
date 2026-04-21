using System;
using System.IO;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 파일/디렉토리 정리(clean-up)용 공통 유틸리티.
    /// 삭제 실패가 치명적이지 않은 맥락에서 사용합니다.
    /// <para>
    /// 모든 예외를 흡수하여 `finally` 블록에서 호출되어도 원래 예외를 마스킹하지 않습니다.
    /// 실패 시 경고 로그는 `AITLog.Warning`으로 남기되 Sentry 전송은 억제합니다.
    /// </para>
    /// </summary>
    internal static class AITFileSystemHelper
    {
        private const string DefaultLogPrefix = "[AIT]";

        /// <summary>
        /// 파일을 삭제합니다. 존재하지 않으면 조용히 true를 반환합니다.
        /// Unity `.meta` 파일이 있으면 함께 삭제합니다.
        /// 실패 시 경고 로그를 남기고 false를 반환합니다 (Sentry 전송 없음).
        /// </summary>
        /// <param name="path">삭제할 파일 경로</param>
        /// <param name="logOnFailure">실패 시 경고 로그를 남길지 여부</param>
        /// <param name="logPrefix">로그 메시지 접두사 (예: "[NodeJS]"). 기본값은 "[AIT]"</param>
        /// <returns>삭제 성공 또는 파일 부재 시 true, 실패 시 false</returns>
        public static bool SafeDelete(string path, bool logOnFailure = true, string logPrefix = DefaultLogPrefix)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            try
            {
                if (!File.Exists(path))
                    return true;

                ClearReadOnlyAttribute(path);
                File.Delete(path);

                // Unity가 생성한 .meta 파일도 함께 정리 (남아있으면 고아 meta로 경고 유발)
                string metaPath = path + ".meta";
                if (File.Exists(metaPath))
                {
                    try
                    {
                        ClearReadOnlyAttribute(metaPath);
                        File.Delete(metaPath);
                    }
                    catch (Exception ex)
                    {
                        // cleanup 경로이므로 어떤 예외든 흡수
                        if (logOnFailure)
                            AITLog.Warning(
                                $"{logPrefix} .meta 파일 삭제 실패: {metaPath} ({ex.GetType().Name}: {ex.Message})",
                                sentryCapture: false);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                // cleanup 경로이므로 어떤 예외든 흡수: finally 블록에서 호출되어도 원래 예외를 마스킹하지 않음
                if (logOnFailure)
                    AITLog.Warning(
                        $"{logPrefix} 파일 삭제 실패: {path} ({ex.GetType().Name}: {ex.Message})",
                        sentryCapture: false);
                return false;
            }
        }

        /// <summary>
        /// 디렉토리를 재귀 삭제합니다. 존재하지 않으면 조용히 true를 반환합니다.
        /// 내부 파일의 읽기 전용 속성을 먼저 해제한 후 <c>Directory.Delete(recursive:true)</c>로 삭제합니다 (Windows 대응).
        /// <para>
        /// 동작 특성: 오직 <c>FileAttributes.ReadOnly</c>만 해제하며, Hidden/Archive 등
        /// 다른 속성은 그대로 둡니다. 또한 첫 번째 회복 불가 실패에서 전체 연산이 중단되므로
        /// partial cleanup(일부 파일만 삭제)은 수행하지 않습니다.
        /// </para>
        /// <para>
        /// 실패 시 경고 로그를 남기고 false를 반환합니다 (Sentry 전송 없음).
        /// </para>
        /// <para>
        /// 디렉토리에 대한 `.meta` 파일은 호출자가 처리합니다. 이 메서드는 디렉토리 내부만 정리합니다.
        /// </para>
        /// </summary>
        /// <param name="path">삭제할 디렉토리 경로</param>
        /// <param name="logOnFailure">실패 시 경고 로그를 남길지 여부</param>
        /// <param name="logPrefix">로그 메시지 접두사 (예: "[NodeJS]"). 기본값은 "[AIT]"</param>
        /// <returns>삭제 성공 또는 디렉토리 부재 시 true, 실패 시 false</returns>
        public static bool SafeDeleteDirectory(string path, bool logOnFailure = true, string logPrefix = DefaultLogPrefix)
        {
            if (string.IsNullOrEmpty(path))
                return true;

            try
            {
                if (!Directory.Exists(path))
                    return true;

                ClearReadOnlyAttributesRecursive(path);
                Directory.Delete(path, recursive: true);
                return true;
            }
            catch (Exception ex)
            {
                // cleanup 경로이므로 어떤 예외든 흡수: finally 블록에서 호출되어도 원래 예외를 마스킹하지 않음
                if (logOnFailure)
                    AITLog.Warning(
                        $"{logPrefix} 디렉토리 삭제 실패: {path} ({ex.GetType().Name}: {ex.Message})",
                        sentryCapture: false);
                return false;
            }
        }

        private static void ClearReadOnlyAttribute(string filePath)
        {
            try
            {
                var attributes = File.GetAttributes(filePath);
                if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
                }
            }
            catch (Exception)
            {
                // 의도적으로 무음 처리: 속성 해제 실패는 후속 File.Delete/Directory.Delete 호출에서
                // 동일한 근본 원인으로 다시 드러나며, 그쪽에서 사용자에게 경고 로그가 노출됨.
                // 여기서 이중으로 로깅하면 소음만 늘어남.
            }
        }

        private static void ClearReadOnlyAttributesRecursive(string dirPath)
        {
            try
            {
                foreach (string file in Directory.GetFiles(dirPath, "*", SearchOption.AllDirectories))
                {
                    ClearReadOnlyAttribute(file);
                }
            }
            catch (Exception)
            {
                // 의도적으로 무음 처리: 열람 실패 시 후속 Directory.Delete가 동일한 오류로 실패하며
                // SafeDeleteDirectory의 catch 블록이 경고 로그를 남긴다.
            }
        }
    }
}
