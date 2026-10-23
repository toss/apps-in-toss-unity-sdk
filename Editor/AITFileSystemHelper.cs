using System;
using System.IO;
using System.Threading;

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

        // Windows 파일 잠금(AV 스캔, 직전 프로세스 핸들, pnpm symlink resolve race) 회복용
        // 지수 백오프 — pnpm node_modules 트리는 보통 200~400ms 안에 안정화됨.
        // 합계 약 750ms로 사용자가 인지 못할 수준이며, 회복 불가 케이스(권한 부족 등)에는
        // 마지막 시도에서 동일 예외가 다시 던져져 호출자에게 결과가 전달됨.
        private static readonly int[] DeleteRetryDelaysMs = { 50, 100, 200, 400 };

        // 원자적 디렉토리 이동(Directory.Move) 회복용 지수 백오프.
        // Windows AV/Defender가 추출 직후 staging dir 내 파일 핸들을 점유하면 rename이
        // IOException("Access denied")으로 실패하는데, 보통 수백 ms 안에 핸들이 풀린다.
        // 삭제(DeleteRetryDelaysMs)보다 다소 길게 잡는다 — 이동 실패는 Node.js 설치 자체를
        // 무산시키는 치명적 경로라 회복 가치가 더 크고, 1회성 설치라 합계 ~1.85s도 수용 가능.
        private static readonly int[] MoveRetryDelaysMs = { 100, 250, 500, 1000 };

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
        /// 재시도: Windows의 일시적 잠금(AV 스캔, 직전 빌드 프로세스 핸들 미해제, pnpm symlink resolve race)에
        /// 회복 가능하도록 지수 백오프 재시도를 수행합니다. ReparsePoint(symlink/junction)는 link 자체만
        /// 제거하고 target은 따라가지 않으므로 store 외부에 영향이 없습니다.
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

            if (!Directory.Exists(path))
                return true;

            Exception lastException = null;
            int totalAttempts = DeleteRetryDelaysMs.Length + 1; // 즉시 1회 + 백오프 횟수

            for (int attempt = 0; attempt < totalAttempts; attempt++)
            {
                try
                {
                    DeleteDirectoryRecursive(path);
                    return true;
                }
                catch (DirectoryNotFoundException)
                {
                    // 동시 정리 등으로 이미 사라진 경우 — 성공으로 처리
                    return true;
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException || ex is IOException)
                {
                    lastException = ex;
                    // 마지막 시도는 sleep 없이 catch만 (백오프 배열 길이를 그대로 사용)
                    if (attempt < DeleteRetryDelaysMs.Length)
                    {
                        try { Thread.Sleep(DeleteRetryDelaysMs[attempt]); }
                        catch (ThreadInterruptedException) { break; }
                    }
                }
                catch (Exception ex)
                {
                    // 회복 가능성이 낮은 다른 예외는 즉시 중단
                    lastException = ex;
                    break;
                }
            }

            // cleanup 경로이므로 어떤 예외든 흡수: finally 블록에서 호출되어도 원래 예외를 마스킹하지 않음
            if (logOnFailure && lastException != null)
                AITLog.Warning(
                    $"{logPrefix} 디렉토리 삭제 실패: {path} ({lastException.GetType().Name}: {lastException.Message})",
                    sentryCapture: false);
            return false;
        }

        /// <summary>
        /// 디렉토리를 최종 경로로 원자적 이동(rename)합니다. Windows의 일시적 잠금
        /// (AV/Defender가 추출 직후 staging dir 내 파일 핸들을 점유하는 경우 등)으로 인한
        /// <see cref="IOException"/>에 대해 지수 백오프로 재시도합니다.
        /// <para>
        /// 동시 설치 경합 대응: 매 시도 전에 <paramref name="targetPath"/> 존재를 확인하여,
        /// 다른 프로세스가 먼저(또는 재시도 도중) 설치를 완료하면(race winner) 이동을 건너뛰고
        /// <c>false</c>를 반환합니다. 이 경우 <paramref name="stagingPath"/> 정리는 호출자가
        /// 담당합니다(이 메서드는 stagingPath를 삭제하지 않음).
        /// </para>
        /// <para>
        /// 삭제 유틸(<see cref="SafeDeleteDirectory"/>)과 달리 이동 실패는 치명적이므로
        /// (Node.js 설치 자체가 무산됨) 전체 재시도 윈도우를 소진해도 실패하면 마지막
        /// <see cref="IOException"/>을 다시 던집니다 — 호출자가 결과를 인지하고 사용자에게
        /// 안내/정리하도록. 실패 직전 경고 로그 1회를 남기되 Sentry 전송은 억제합니다.
        /// </para>
        /// </summary>
        /// <param name="stagingPath">이동할 원본(스테이징) 경로</param>
        /// <param name="targetPath">이동 대상(최종) 경로</param>
        /// <param name="logPrefix">로그 메시지 접두사 (예: "[NodeJS]"). 기본값은 "[AIT]"</param>
        /// <returns>이동을 수행했으면 true, 다른 프로세스가 이미 target을 완성해 이동을 건너뛰었으면 false</returns>
        /// <exception cref="IOException">전체 재시도 윈도우를 소진해도 이동에 실패한 경우</exception>
        public static bool MoveDirectoryWithRetry(string stagingPath, string targetPath, string logPrefix = DefaultLogPrefix)
        {
            return MoveDirectoryWithRetryCore(
                stagingPath,
                targetPath,
                logPrefix,
                move: () => Directory.Move(stagingPath, targetPath),
                targetExists: () => Directory.Exists(targetPath),
                sleep: ms => Thread.Sleep(ms));
        }

        /// <summary>
        /// <see cref="MoveDirectoryWithRetry"/>의 테스트 가능한 코어. 파일시스템·시간 의존성을
        /// 델리게이트로 주입받아 재시도/백오프/경합(race winner)/소진 흐름만 결정적으로 검증할 수 있다.
        /// 프로덕션 호출은 <see cref="MoveDirectoryWithRetry"/>가 실제 <see cref="Directory"/>/
        /// <see cref="Thread.Sleep(int)"/>를 주입한다.
        /// </summary>
        /// <param name="move">이동 시도(성공 시 정상 반환, 일시적 실패 시 <see cref="IOException"/>)</param>
        /// <param name="targetExists">대상 경로 존재 여부(다른 프로세스의 동시 설치 감지)</param>
        /// <param name="sleep">백오프 대기(ms) — 테스트에서는 실제 대기 없이 호출 인자만 기록 가능</param>
        internal static bool MoveDirectoryWithRetryCore(
            string stagingPath,
            string targetPath,
            string logPrefix,
            Action move,
            Func<bool> targetExists,
            Action<int> sleep)
        {
            int totalAttempts = MoveRetryDelaysMs.Length + 1; // 즉시 1회 + 백오프 횟수
            IOException lastException = null;

            for (int attempt = 0; attempt < totalAttempts; attempt++)
            {
                // 다른 프로세스가 먼저(또는 재시도 도중) 설치를 완료했으면 이동 불필요 — race winner.
                if (targetExists())
                    return false;

                try
                {
                    move();
                    return true;
                }
                catch (IOException ex)
                {
                    // targetPath가 아직 없는 상태의 IOException = staging 측 핸들 점유(주로 AV 스캔)
                    // 가능성이 높다. 다음 반복 진입 시 targetExists()로 race winner도 함께 재확인된다.
                    lastException = ex;
                    if (attempt < MoveRetryDelaysMs.Length)
                    {
                        try { sleep(MoveRetryDelaysMs[attempt]); }
                        catch (ThreadInterruptedException) { break; }
                    }
                }
            }

            // 재시도 도중 race winner가 target을 완성했을 수 있으니 마지막으로 한 번 더 확인.
            if (targetExists())
                return false;

            // 전체 재시도 소진 — 이동 실패는 치명적이므로 호출자에게 마지막 예외를 전파.
            AITLog.Warning(
                $"{logPrefix} 디렉토리 이동 재시도 {totalAttempts}회 모두 실패: {stagingPath} → {targetPath} " +
                $"({lastException?.GetType().Name}: {lastException?.Message})",
                sentryCapture: false);
            throw lastException ?? new IOException($"디렉토리 이동 실패: {stagingPath} → {targetPath}");
        }

        /// <summary>
        /// 재귀 삭제 본체. ReadOnly 속성 해제 후 ReparsePoint(symlink/junction)는 link만 제거,
        /// 일반 디렉토리는 자식 파일·서브디렉토리를 먼저 정리하고 본체를 삭제합니다.
        /// 단순히 <c>Directory.Delete(recursive:true)</c>를 호출하면 symlink target까지 따라가
        /// pnpm의 .pnpm 스토어 밖 파일에 영향을 줄 수 있어 수동 재귀를 사용합니다.
        /// </summary>
        private static void DeleteDirectoryRecursive(string dirPath)
        {
            var info = new DirectoryInfo(dirPath);
            if (!info.Exists)
                return;

            // ReparsePoint(symlink/junction)는 link 본체만 제거하고 target은 따라가지 않는다.
            if ((info.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                try { info.Attributes = FileAttributes.Normal; }
                catch { /* 권한 부족 등은 다음 Delete에서 다시 드러남 */ }
                info.Delete();
                return;
            }

            foreach (var file in info.GetFiles())
            {
                if ((file.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    try { file.Attributes = FileAttributes.Normal; }
                    catch { /* 다음 Delete에서 동일 원인이 다시 드러남 */ }
                }
                file.Delete();
            }

            foreach (var sub in info.GetDirectories())
            {
                DeleteDirectoryRecursive(sub.FullName);
            }

            info.Delete(recursive: false);
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

    }
}
