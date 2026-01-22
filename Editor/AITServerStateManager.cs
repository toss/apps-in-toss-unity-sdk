using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 서버 타입
    /// </summary>
    public enum ServerType
    {
        Dev,
        Prod
    }

    /// <summary>
    /// 서버 상태
    /// </summary>
    public enum ServerState
    {
        /// <summary>서버가 실행 중이지 않음</summary>
        NotRunning,
        /// <summary>서버가 시작 중 (프로세스는 있지만 포트가 아직 열리지 않음)</summary>
        Starting,
        /// <summary>서버가 실행 중 (프로세스 존재 + 포트 열림)</summary>
        Running
    }

    /// <summary>
    /// 서버 상태를 캐싱하고 실제 상태와 동기화하는 관리자
    /// MenuItem 검증에서는 캐시된 상태를 반환하고,
    /// 주기적으로 실제 상태를 검증하여 캐시를 갱신
    /// </summary>
    public class AITServerStateManager
    {
        // 캐시 유효 시간 (MenuItem 검증이 빈번하므로 적절한 간격 필요)
        private const double CACHE_VALIDITY_SECONDS = 2.0;

        // EditorPrefs 키
        private readonly string pidPrefKey;
        private readonly string portPrefKey;

        // 캐시된 상태
        private ServerState cachedState = ServerState.NotRunning;
        private double lastValidationTime = 0;
        private int cachedPort = 0;
        private int cachedPid = 0;

        // 프로세스 관리자
        private AITProcessTreeManager processManager;

        // 서버 타입
        private readonly ServerType serverType;

        /// <summary>
        /// 현재 캐시된 포트
        /// </summary>
        public int Port => cachedPort;

        /// <summary>
        /// 현재 캐시된 PID
        /// </summary>
        public int Pid => cachedPid;

        /// <summary>
        /// 프로세스 관리자
        /// </summary>
        public AITProcessTreeManager ProcessManager => processManager;

        /// <summary>
        /// 서버 상태 관리자 생성
        /// </summary>
        /// <param name="type">서버 타입 (Dev 또는 Prod)</param>
        public AITServerStateManager(ServerType type)
        {
            serverType = type;

            // EditorPrefs 키 설정
            string prefix = type == ServerType.Dev ? "AIT_DevServer" : "AIT_ProdServer";
            pidPrefKey = $"{prefix}PID";
            portPrefKey = $"{prefix}Port";
        }

        /// <summary>
        /// MenuItem 검증용 - 캐시된 상태 반환 (빠름)
        /// 캐시가 만료되었으면 갱신
        /// </summary>
        public ServerState GetCachedState()
        {
            if (IsCacheExpired())
            {
                ValidateState();
            }
            return cachedState;
        }

        /// <summary>
        /// 캐시가 만료되었는지 확인
        /// </summary>
        private bool IsCacheExpired()
        {
            return EditorApplication.timeSinceStartup - lastValidationTime > CACHE_VALIDITY_SECONDS;
        }

        /// <summary>
        /// 실제 상태 확인 및 캐시 갱신 (동기)
        /// 서버 시작/중지 등 액션 전에 호출
        /// </summary>
        /// <remarks>
        /// 상태 판단 우선순위:
        /// 1. 포트가 열려있으면 → Running (서버가 실제로 응답 중)
        /// 2. 프로세스 관리자가 살아있으면 → Starting (아직 포트 대기 중)
        /// 3. 둘 다 없으면 → NotRunning
        ///
        /// 참고: Unix에서 bash -l -c "..." 로 실행 시 bash 프로세스는 종료되고
        /// 자식 프로세스(npm/node)만 남을 수 있어 PID 기반 확인이 불안정함.
        /// 따라서 포트 기반 확인을 우선함.
        /// </remarks>
        public ServerState ValidateState()
        {
            // EditorPrefs에서 저장된 PID/Port 로드
            int savedPid = EditorPrefs.GetInt(pidPrefKey, 0);
            int savedPort = EditorPrefs.GetInt(portPrefKey, 0);

            // 포트 사용 확인 (가장 신뢰할 수 있는 지표)
            bool portInUse = savedPort > 0 && IsPortInUse(savedPort);

            // 프로세스 관리자 존재 여부 확인 (메모리 내 참조)
            bool hasProcessManager = processManager != null && !processManager.HasExited;

            // PID 기반 프로세스 확인 (폴백)
            bool processAlive = IsProcessAlive(savedPid);

            // 상태 결정: 포트 우선 판단
            if (portInUse)
            {
                // 포트가 열려있으면 서버가 실행 중
                cachedState = ServerState.Running;
                cachedPid = savedPid;
                cachedPort = savedPort;

                // 프로세스 관리자 복원 시도 (없는 경우, PID가 유효하면)
                if (processManager == null && processAlive)
                {
                    RestoreProcessManager(savedPid);
                }
            }
            else if (hasProcessManager || processAlive)
            {
                // 프로세스는 있지만 포트가 아직 열리지 않음 → Starting
                cachedState = ServerState.Starting;
                cachedPid = savedPid;
                cachedPort = 0;

                // 프로세스 관리자 복원 (없는 경우)
                if (processManager == null && processAlive)
                {
                    RestoreProcessManager(savedPid);
                }
            }
            else
            {
                // 프로세스도 없고 포트도 없음 → NotRunning
                cachedState = ServerState.NotRunning;
                ClearPersistedState();
            }

            lastValidationTime = EditorApplication.timeSinceStartup;
            return cachedState;
        }

        /// <summary>
        /// 서버 시작 시 호출 - 상태를 Starting으로 설정하고 프로세스 관리자 저장
        /// </summary>
        /// <param name="manager">시작된 프로세스의 관리자</param>
        /// <param name="expectedPort">예상 포트 (서버가 열 포트)</param>
        public void OnServerStarting(AITProcessTreeManager manager, int expectedPort)
        {
            processManager = manager;
            cachedPid = manager.ProcessId;
            cachedPort = expectedPort;
            cachedState = ServerState.Starting;
            lastValidationTime = EditorApplication.timeSinceStartup;

            // EditorPrefs에 저장
            EditorPrefs.SetInt(pidPrefKey, cachedPid);
            EditorPrefs.SetInt(portPrefKey, expectedPort);
        }

        /// <summary>
        /// 서버가 성공적으로 시작됨 - 상태를 Running으로 전환
        /// </summary>
        /// <param name="actualPort">실제 열린 포트</param>
        public void OnServerStarted(int actualPort)
        {
            cachedPort = actualPort;
            cachedState = ServerState.Running;
            lastValidationTime = EditorApplication.timeSinceStartup;

            // EditorPrefs 업데이트
            EditorPrefs.SetInt(portPrefKey, actualPort);

            string serverName = serverType == ServerType.Dev ? "Dev" : "Production";
            Debug.Log($"[AIT] {serverName} 서버 프로세스 복원됨 (PID: {cachedPid}, Port: {actualPort})");
        }

        /// <summary>
        /// 서버 시작 실패
        /// </summary>
        public void OnServerFailed()
        {
            processManager = null;
            cachedPid = 0;
            cachedPort = 0;
            cachedState = ServerState.NotRunning;
            lastValidationTime = EditorApplication.timeSinceStartup;

            ClearPersistedState();
        }

        /// <summary>
        /// 서버 중지 시 호출
        /// </summary>
        public void OnServerStopped()
        {
            // 프로세스 트리 종료
            if (processManager != null)
            {
                try
                {
                    processManager.KillProcessTree();
                }
                catch
                {
                    // 무시
                }
                processManager = null;
            }

            cachedPid = 0;
            cachedPort = 0;
            cachedState = ServerState.NotRunning;
            lastValidationTime = EditorApplication.timeSinceStartup;

            ClearPersistedState();
        }

        /// <summary>
        /// 프로세스 트리 종료 (서버 중지용)
        /// </summary>
        public void KillProcessTree()
        {
            if (processManager != null)
            {
                try
                {
                    processManager.KillProcessTree();
                }
                catch
                {
                    // 무시
                }
                processManager = null;
            }
        }

        /// <summary>
        /// 프로세스 관리자 복원
        /// </summary>
        private void RestoreProcessManager(int pid)
        {
            if (pid <= 0) return;

            processManager = new AITProcessTreeManager();
            if (!processManager.RestoreFromPid(pid))
            {
                processManager = null;
            }
        }

        /// <summary>
        /// 영속화된 상태 정리
        /// </summary>
        private void ClearPersistedState()
        {
            processManager = null;
            cachedPid = 0;
            cachedPort = 0;

            EditorPrefs.DeleteKey(pidPrefKey);
            EditorPrefs.DeleteKey(portPrefKey);
        }

        /// <summary>
        /// 프로세스가 살아있는지 확인
        /// </summary>
        private static bool IsProcessAlive(int pid)
        {
            if (pid <= 0) return false;
            try
            {
                var process = Process.GetProcessById(pid);
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 포트가 사용 중인지 확인 (열 수 없으면 사용 중)
        /// </summary>
        private static bool IsPortInUse(int port)
        {
            if (port <= 0) return false;

            TcpListener listener = null;
            try
            {
                // Loopback 주소로 확인
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return false; // 포트 사용 가능 = 사용 중이 아님
            }
            catch (SocketException)
            {
                return true; // 포트 사용 불가 = 사용 중
            }
            finally
            {
                listener?.Stop();
            }
        }
    }
}
