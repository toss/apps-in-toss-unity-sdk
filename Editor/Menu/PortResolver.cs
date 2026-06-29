using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor.Menu
{
    /// <summary>
    /// 서버 포트 해석 및 충돌 처리 유틸리티.
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class PortResolver
    {
        /// <summary>
        /// 포트 충돌 에러인지 판단
        /// </summary>
        internal static bool IsPortConflictError(string output)
        {
            if (string.IsNullOrEmpty(output)) return false;

            string lower = output.ToLowerInvariant();
            return lower.Contains("eaddrinuse") ||
                   lower.Contains("port is already in use") ||
                   lower.Contains("address already in use");
        }

        /// <summary>
        /// 지정된 포트를 점유 중인 프로세스를 강제 종료
        /// </summary>
        internal static void KillProcessOnPort(int port)
        {
            if (port <= 0) return;

            try
            {
                string command;

                if (AITPlatformHelper.IsWindows)
                {
                    // Windows: netstat + taskkill
                    command = $"for /f \"tokens=5\" %a in ('netstat -aon ^| findstr :{port}') do taskkill /PID %a /F 2>nul";
                }
                else
                {
                    // Unix: lsof + kill
                    command = $"lsof -ti :{port} | xargs kill -9 2>/dev/null";
                }

                AITPlatformHelper.ExecuteCommand(command, null, null, timeoutMs: 2000, verbose: false);
            }
            catch
            {
                // 무시
            }
        }

        /// <summary>
        /// 포트가 사용 가능한지 확인 (0.0.0.0과 127.0.0.1 모두 체크)
        /// </summary>
        internal static bool IsPortAvailable(int port)
        {
            // granite/vite는 0.0.0.0에 바인딩하므로 Any로 체크해야 함
            try
            {
                var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, port);
                listener.Start();
                listener.Stop();
            }
            catch
            {
                return false;
            }

            // 추가로 Loopback도 체크 (다른 프로세스가 127.0.0.1에만 바인딩한 경우)
            try
            {
                var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
            }
            catch
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Vite 포트 폴링 결정.
        /// </summary>
        internal enum VitePollDecision
        {
            /// <summary>다음 update 콜백까지 대기.</summary>
            Wait,
            /// <summary>이번 호출에서 포트 상태를 다시 확인하고 결정.</summary>
            CheckPort,
            /// <summary>포트가 준비됨 — 브라우저 열기.</summary>
            Ready,
            /// <summary>최대 대기 시간 초과 — fallback으로 브라우저 열기.</summary>
            Timeout,
        }

        /// <summary>
        /// Vite 포트 대기 폴링의 결정을 시간 진행만으로 계산하는 순수 함수.
        /// 결과가 <see cref="VitePollDecision.CheckPort"/>이면 호출자가 포트 상태를 확인한 뒤
        /// 사용 중이면 <see cref="VitePollDecision.Ready"/>로, 아니면 <see cref="VitePollDecision.Wait"/>로 진행해야 한다.
        /// 시간 우선 순위: 타임아웃이 polling interval보다 우선 — 인터벌이 매우 길더라도 타임아웃은 보장된다.
        /// </summary>
        internal static VitePollDecision EvaluateVitePollDecision(
            double elapsedSeconds,
            double lastCheckSeconds,
            double maxWaitSeconds,
            double checkIntervalSeconds)
        {
            // 타임아웃이 우선 — checkInterval이 maxWait보다 큰 경우에도 타임아웃은 발생해야 한다.
            if (elapsedSeconds > maxWaitSeconds)
                return VitePollDecision.Timeout;

            if (elapsedSeconds - lastCheckSeconds < checkIntervalSeconds)
                return VitePollDecision.Wait;

            return VitePollDecision.CheckPort;
        }

        /// <summary>
        /// Vite 포트 대기의 기본 타임아웃 (초). 콜드 스타트나 무거운 사용자 환경에서
        /// 15초가 부족해 fallback 경고가 발생하던 사례를 흡수하기 위해 충분히 크게 설정.
        /// </summary>
        internal const double DefaultViteWaitMaxSeconds = 60.0;

        /// <summary>
        /// Vite 포트 폴링 체크 간격 (초). TCP bind/unbind 부하 방지.
        /// </summary>
        internal const double DefaultViteWaitIntervalSeconds = 0.5;

        /// <summary>
        /// Vite 포트가 열릴 때까지 대기한 후 브라우저를 엽니다.
        /// Granite 포트가 먼저 감지되지만 Vite는 아직 준비되지 않았을 수 있으므로
        /// EditorApplication.update 폴링으로 최대 <see cref="DefaultViteWaitMaxSeconds"/>초 대기합니다.
        /// </summary>
        internal static void WaitForPortAndOpenBrowser(int port, string url)
        {
            // 이미 포트가 열려있으면 즉시 열기
            if (!IsPortAvailable(port))
            {
                Debug.Log($"[AIT] Vite 포트 {port} 준비 완료, 브라우저 열기");
                Application.OpenURL(url);
                return;
            }

            Debug.Log($"[AIT] Vite 포트 {port} 대기 중...");
            double startTime = EditorApplication.timeSinceStartup;
            double lastCheckTime = 0;
            const double maxWaitSeconds = DefaultViteWaitMaxSeconds;
            const double checkIntervalSeconds = DefaultViteWaitIntervalSeconds;

            void PollVitePort()
            {
                double elapsed = EditorApplication.timeSinceStartup - startTime;
                var decision = EvaluateVitePollDecision(elapsed, lastCheckTime, maxWaitSeconds, checkIntervalSeconds);

                if (decision == VitePollDecision.Wait)
                    return;

                if (decision == VitePollDecision.Timeout)
                {
                    EditorApplication.update -= PollVitePort;
                    // 정상 흐름의 timeout fallback이므로 Sentry 전송 억제
                    AITLog.Warning($"[AIT] Vite 포트 {port} 대기 타임아웃 ({maxWaitSeconds}초), 브라우저를 엽니다", sentryCapture: false);
                    Application.OpenURL(url);
                    return;
                }

                // CheckPort
                lastCheckTime = elapsed;
                if (!IsPortAvailable(port))
                {
                    EditorApplication.update -= PollVitePort;
                    Debug.Log($"[AIT] Vite 포트 {port} 준비 완료 ({elapsed:F1}초 대기), 브라우저 열기");
                    Application.OpenURL(url);
                }
            }

            EditorApplication.update += PollVitePort;
        }

        /// <summary>
        /// 사용 가능한 포트 찾기 (시작 포트부터 최대 10개 시도)
        /// </summary>
        internal static int FindAvailablePort(int startPort, int maxAttempts = 10)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                int port = startPort + i;
                if (IsPortAvailable(port))
                {
                    return port;
                }
                Debug.Log($"[AIT] 포트 {port}가 사용 중, 다음 포트 시도...");
            }
            return -1; // 사용 가능한 포트 없음
        }

        /// <summary>
        /// 서버 포트 설정 해석 및 충돌 검사
        /// </summary>
        internal static bool TryResolveServerPorts(
            AITEditorScriptObject config,
            out string graniteHost, out int granitePort,
            out string viteHost, out int vitePort)
        {
            graniteHost = !string.IsNullOrEmpty(config.graniteHost) ? config.graniteHost : "0.0.0.0";
            granitePort = config.granitePort > 0 ? config.granitePort : 8081;
            viteHost = !string.IsNullOrEmpty(config.viteHost) ? config.viteHost : "localhost";
            vitePort = config.vitePort > 0 ? config.vitePort : 5173;

            // Vite 포트 충돌 확인 및 자동 탐지
            if (!IsPortAvailable(vitePort))
            {
                int availablePort = FindAvailablePort(vitePort);
                if (availablePort > 0)
                {
                    Debug.Log($"[AIT] 포트 {vitePort}가 사용 중, {availablePort} 사용");
                    vitePort = availablePort;
                }
                else
                {
                    AITLog.Error($"[AIT] 사용 가능한 포트를 찾을 수 없습니다 (시도: {vitePort}-{vitePort+9})", sentryCapture: false);
                    AITPlatformHelper.ShowInfoDialog("포트 오류", $"포트 {vitePort} 및 인근 포트가 모두 사용 중입니다.\n다른 포트를 Configuration에서 설정하거나, 사용 중인 프로세스를 종료하세요.", "확인");
                    return false;
                }
            }

            // Granite 포트 충돌 확인 및 자동 탐지
            if (!IsPortAvailable(granitePort))
            {
                int availablePort = FindAvailablePort(granitePort);
                if (availablePort > 0)
                {
                    Debug.Log($"[AIT] Granite 포트 {granitePort}가 사용 중, {availablePort} 사용");
                    granitePort = availablePort;
                }
                else
                {
                    AITLog.Error($"[AIT] 사용 가능한 Granite 포트를 찾을 수 없습니다 (시도: {granitePort}-{granitePort+9})", sentryCapture: false);
                    AITPlatformHelper.ShowInfoDialog("포트 오류", $"Granite 포트 {granitePort} 및 인근 포트가 모두 사용 중입니다.\n다른 포트를 Configuration에서 설정하거나, 사용 중인 프로세스를 종료하세요.", "확인");
                    return false;
                }
            }

            return true;
        }
    }
}
