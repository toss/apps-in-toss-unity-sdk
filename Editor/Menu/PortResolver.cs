using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
                if (AITPlatformHelper.IsWindows)
                {
                    KillListeningProcessesWindows(port);
                    return;
                }

                // Unix: lsof + kill.
                // NOTE: lsof -ti (필터 없음)는 LISTEN뿐 아니라 해당 포트로의 클라이언트 연결(ESTABLISHED 등)도
                // 함께 잡아 종료할 수 있다. Windows 분기는 netstat 파싱으로 LISTEN 소유자만 골라 종료하지만,
                // Unix 분기는 기존 동작(및 macOS/Linux 바이트 동일성 요구사항)을 유지하기 위해 그대로 둔다.
                AITPlatformHelper.ExecuteCommand($"lsof -ti :{port} | xargs kill -9 2>/dev/null", null, null, timeoutMs: 2000, verbose: false);
            }
            catch
            {
                // 무시
            }
        }

        // %SystemRoot% 같은 환경변수 문자열은 UseShellExecute=false(CreateProcess) 경로에서는 확장되지 않는다.
        // 실제 System32 경로를 조립하고, 실패 시 CreateProcess 검색 순서에 System32가 포함되어 있으므로
        // 바로 실행 파일 이름으로 폴백한다.
        internal static System.Diagnostics.ProcessStartInfo CreateNetstatStartInfo()
        {
            string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string exePath = string.IsNullOrEmpty(systemDir) ? null : Path.Combine(systemDir, "netstat.exe");

            return new System.Diagnostics.ProcessStartInfo
            {
                FileName = (exePath != null && File.Exists(exePath)) ? exePath : "netstat.exe",
                Arguments = "-ano",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }

        /// <summary>
        /// netstat -ano 출력에서 지정 포트를 LISTEN 중인 PID 목록을 찾는다 (자기 프로세스 제외).
        /// </summary>
        internal static List<int> FindListeningPidsWindows(int port, int excludedPid)
        {
            try
            {
                var run = AITProcessExecutor.Run(CreateNetstatStartInfo(), 3000);
                if (run.TimedOut || run.ExitCode != 0) return new List<int>();
                return ParseListeningPids(run.StdOut, port, excludedPid);
            }
            catch
            {
                return new List<int>();
            }
        }

        /// <summary>
        /// netstat -ano 출력을 파싱해 지정 포트를 LISTEN 중인 PID 목록을 반환한다 (순수 함수, 로케일 무관).
        /// TCP 행 + 로컬 포트 정확 일치 + 원격 포트 0(=LISTEN, 로케일별 상태 문자열 "LISTENING"/"ABHÖREN"/"수신 대기"
        /// 등을 직접 비교하지 않아도 됨) + PID 4 초과(0/4는 시스템 프로세스) + 자기 PID 제외 + 중복 제거.
        /// </summary>
        internal static List<int> ParseListeningPids(string output, int port, int excludedPid)
        {
            var pids = new List<int>();
            if (string.IsNullOrEmpty(output) || port <= 0) return pids;

            foreach (var line in output.Split('\n'))
            {
                // Split(null, RemoveEmptyEntries)는 char.IsWhiteSpace 기준으로 나누므로 CRLF의 '\r'도 구분자로
                // 소비된다. 아래 TrimEnd('\r')는 이 동작에 기대지 않으려는 방어 코드다.
                var tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 4 || !string.Equals(tokens[0], "TCP", StringComparison.OrdinalIgnoreCase)) continue;

                if (!TryParseEndpointPort(tokens[1], out int localPort) || localPort != port) continue;
                if (!TryParseEndpointPort(tokens[2], out int foreignPort) || foreignPort != 0) continue;

                string pidToken = tokens[tokens.Length - 1].TrimEnd('\r');
                if (!int.TryParse(pidToken, NumberStyles.None, CultureInfo.InvariantCulture, out int pid)) continue;

                if (pid <= 4 || pid == excludedPid || pids.Contains(pid)) continue;
                pids.Add(pid);
            }

            return pids;
        }

        /// <summary>
        /// netstat 로컬/원격 주소 열("호스트:포트")에서 포트만 추출한다.
        /// IPv6 주소는 "[::]:8081", "[fe80::1%12]:8081"처럼 콜론을 포함하므로 마지막 ':' 기준으로 분리한다.
        /// </summary>
        private static bool TryParseEndpointPort(string endpoint, out int port)
        {
            port = 0;
            if (string.IsNullOrEmpty(endpoint)) return false;

            int separatorIndex = endpoint.LastIndexOf(':');
            if (separatorIndex < 0) return false;

            return int.TryParse(endpoint.Substring(separatorIndex + 1), NumberStyles.None, CultureInfo.InvariantCulture, out port);
        }

        /// <summary>
        /// 지정 포트를 LISTEN 중인 프로세스 중 Node 계열(node/pnpm/npm)만 강제 종료한다.
        /// PID 재사용으로 무관한 프로세스를 잘못 종료하는 사고를 막기 위한 방어 기준으로,
        /// <see cref="AITServerStateManager"/>가 저장된 PID를 kill할 때 쓰는 기준과 동일하다.
        /// </summary>
        private static void KillListeningProcessesWindows(int port)
        {
            int selfPid;
            using (var self = System.Diagnostics.Process.GetCurrentProcess())
            {
                selfPid = self.Id;
            }

            foreach (int pid in FindListeningPidsWindows(port, selfPid))
            {
                try
                {
                    using (var process = System.Diagnostics.Process.GetProcessById(pid))
                    {
                        if (!AITBuildSessionRecovery.IsNodeLikeProcessName(process.ProcessName)) continue;

                        process.Kill();
                        process.WaitForExit(1000);
                    }
                }
                catch
                {
                    // 이미 종료되었거나 권한 부족 — best-effort이므로 무시.
                }
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
        /// 서버 포트 설정 해석 및 충돌 검사.
        /// skipGranitePortScan이 true면 granite 포트 가용성 검사를 건너뛴다
        /// (web-framework 3.x vite 단독 모드 — granite 포트를 열지 않으므로, 8081 계열이
        /// 다른 프로세스에 점유되어 있어도 서버 기동을 차단하면 안 된다).
        /// </summary>
        internal static bool TryResolveServerPorts(
            AITEditorScriptObject config,
            out string graniteHost, out int granitePort,
            out string viteHost, out int vitePort,
            bool skipGranitePortScan = false)
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
            if (!skipGranitePortScan && !IsPortAvailable(granitePort))
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
