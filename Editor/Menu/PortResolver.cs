using UnityEditor;
using UnityEngine;
using AppsInToss.Editor;

namespace AppsInToss.Editor.Menu
{
    /// <summary>
    /// 서버 포트 해석 및 충돌 처리 유틸리티
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
        /// Vite 포트가 열릴 때까지 대기한 후 브라우저를 엽니다.
        /// Granite 포트가 먼저 감지되지만 Vite는 아직 준비되지 않았을 수 있으므로
        /// EditorApplication.update 폴링으로 최대 15초 대기합니다.
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
            const double maxWaitSeconds = 15.0;
            const double checkIntervalSeconds = 0.5;

            void PollVitePort()
            {
                double elapsed = EditorApplication.timeSinceStartup - startTime;

                // TCP bind/unbind 부하 방지: 0.5초 간격으로 체크
                if (elapsed - lastCheckTime < checkIntervalSeconds)
                    return;
                lastCheckTime = elapsed;

                if (!IsPortAvailable(port))
                {
                    // 포트가 사용 중 = Vite 서버 준비됨
                    EditorApplication.update -= PollVitePort;
                    Debug.Log($"[AIT] Vite 포트 {port} 준비 완료 ({elapsed:F1}초 대기), 브라우저 열기");
                    Application.OpenURL(url);
                    return;
                }

                if (elapsed > maxWaitSeconds)
                {
                    // 타임아웃 — 그래도 브라우저 열기 (사용자가 직접 새로고침 가능)
                    EditorApplication.update -= PollVitePort;
                    Debug.LogWarning($"[AIT] Vite 포트 {port} 대기 타임아웃 ({maxWaitSeconds}초), 브라우저를 엽니다");
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
