using System;
using System.Diagnostics;
using System.Linq;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// Editor 로드 시 pending AITBuildSession 을 감지해 PlayerSettings 복원과
    /// 자식 프로세스 정리를 수행. 한 번 실행되고 세션 파일은 항상 삭제된다.
    /// </summary>
    [InitializeOnLoad]
    internal static class AITBuildSessionRecovery
    {
        static AITBuildSessionRecovery()
        {
            // Editor 가 도메인을 완전히 준비한 뒤 실행하도록 한 프레임 지연.
            EditorApplication.delayCall += OnLoad;
        }

        private static void OnLoad()
        {
            if (!AITBuildSession.TryLoadPendingSession(out var session)) return;

            try
            {
                if (AITBuildSession.IsStale(session))
                {
                    Debug.Log(
                        $"[AIT] Stale build session 발견 및 제거 " +
                        $"(sessionId={Short(session.sessionId)}).");
                    return;
                }

                try { session.playerSettings.Restore(); }
                catch (Exception ex)
                {
                    AITLog.Error($"[AIT] 세션 복원 중 PlayerSettings 복원 실패: {ex.Message}",
                        sentryCapture: true);
                }

                int killed = session.childPids.Count(TryKillIfRunning);

                Debug.Log(
                    $"[AIT] 이전 빌드가 중단되어 상태를 복구했습니다. " +
                    $"entrypoint={session.entrypoint}, stage={session.stage}, " +
                    $"PlayerSettings 복원, 자식 프로세스 {killed}개 정리.");
            }
            catch (Exception ex)
            {
                AITLog.Error($"[AIT] 세션 복구 훅 예외: {ex.Message}", sentryCapture: true);
            }
            finally
            {
                // 성공/실패/stale 모두 세션 파일 삭제 — 무한 루프 방지.
                AITBuildSession.EndBuild();
            }
        }

        private static bool TryKillIfRunning(int pid)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                string name = (p.ProcessName ?? string.Empty).ToLowerInvariant();
                // PID 재사용 오검출 방어 — name 이 pnpm/node/npm 계열일 때만 kill.
                bool matchesBuildProcess =
                    name.Contains("node") || name.Contains("pnpm") || name.Contains("npm");
                if (!matchesBuildProcess)
                {
                    Debug.Log($"[AIT] PID {pid} 은 빌드 프로세스가 아님 (name={name}). Skip.");
                    return false;
                }
                p.Kill();
                return true;
            }
            catch (ArgumentException)
            {
                // 이미 종료됨 — 정상 경로, 로그 없음.
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIT] PID {pid} 종료 실패: {ex.Message}. 수동 확인 필요.");
                return false;
            }
        }

        private static string Short(string id)
            => string.IsNullOrEmpty(id) ? "?" : id.Substring(0, System.Math.Min(8, id.Length));
    }
}
