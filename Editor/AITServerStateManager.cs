using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using UnityEditor;
using UnityEngine;
using AppsInToss.Editor.Menu;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 서버 타입
    /// </summary>
    /// <remarks>
    /// Production Server 지원이 제거되며 Dev 단일 값만 남았습니다(샌드박스 앱 테스트 불가로
    /// 존재 이유 상실 — SDK 3.0.0부터). enum 자체를 남긴 이유는 <see cref="AITServerStateManager"/>
    /// 생성자·<c>ServerType type</c> 매개변수를 받는 기존 호출부들을 그대로 유지해 diff를 최소화하기
    /// 위함입니다(전체 사용처 grep 결과, 완전 제거 시 다수 시그니처를 함께 바꿔야 해 diff가 커짐).
    /// </remarks>
    public enum ServerType
    {
        Dev
    }

    /// <summary>
    /// 서버 상태
    /// </summary>
    public enum ServerState
    {
        /// <summary>서버가 실행 중이지 않음</summary>
        NotRunning,
        /// <summary>서버가 실행 중 (포트 열림)</summary>
        Running
    }

    /// <summary>
    /// 서버 상태를 캐싱하고 실제 상태와 동기화하는 관리자
    /// MenuItem 검증에서는 캐시된 상태를 반환하고,
    /// 주기적으로 실제 상태를 검증하여 캐시를 갱신
    /// </summary>
    public class AITServerStateManager
    {
        // 기본 캐시 유효 시간 (짧게 설정하여 실시간에 가깝게 반영)
        private const double CACHE_VALIDITY_SECONDS = 0.1;

        // 서버 시작 직후 캐시 연장 시간 (서버 안정화 대기)
        // OnServerStarted 호출 후 이 시간 동안은 포트 확인을 건너뛰고 캐시된 상태 유지
        private const double STARTUP_GRACE_PERIOD_SECONDS = 5.0;

        // EditorPrefs 키
        private readonly string pidPrefKey;
        private readonly string portPrefKey;

        // 캐시된 상태
        private ServerState cachedState = ServerState.NotRunning;
        private double lastValidationTime = 0;
        private double serverStartedTime = 0;  // OnServerStarted 호출 시간 (grace period용)
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
        /// <param name="type">서버 타입 (현재 Dev만 존재 — Production Server 제거됨)</param>
        public AITServerStateManager(ServerType type)
        {
            serverType = type;

            // EditorPrefs 키 리터럴 "AIT_DevServer"는 업그레이드 시 기존 실행 중 서버를 계속
            // 추적해야 하므로 절대 변경하지 않는다.
            pidPrefKey = "AIT_DevServerPID";
            portPrefKey = "AIT_DevServerPort";
        }

        // 구버전(Production Server 지원 시절) EditorPrefs 키 — 1회성 마이그레이션 전용.
        private const string LegacyProdPidPrefKey = "AIT_ProdServerPID";
        private const string LegacyProdPortPrefKey = "AIT_ProdServerPort";

        /// <summary>
        /// 구 "AIT_ProdServer*" EditorPrefs 키가 남아 있으면(Production Server 지원 시절 상태)
        /// 기록된 프로세스를 기존 kill 로직(PID 우선 종료 → 포트 백업 종료)으로 정리 시도한 뒤,
        /// 성공 여부와 무관하게 키를 삭제하는 1회성 마이그레이션입니다. 절대 throw하지 않습니다.
        /// </summary>
        public static void MigrateLegacyProdServerState()
        {
            try
            {
                if (!EditorPrefs.HasKey(LegacyProdPidPrefKey) && !EditorPrefs.HasKey(LegacyProdPortPrefKey))
                {
                    return;
                }

                int pid = EditorPrefs.GetInt(LegacyProdPidPrefKey, 0);
                int port = EditorPrefs.GetInt(LegacyProdPortPrefKey, 0);

                if (IsProcessAlive(pid))
                {
                    try
                    {
                        Process.GetProcessById(pid).Kill();
                    }
                    catch
                    {
                        // 이미 종료되었거나 접근 불가 - 포트 기반 백업 종료로 이어감
                    }
                }

                // 백업: 포트에서 실행 중인 프로세스도 종료 (StopServer와 동일한 기존 kill 로직)
                PortResolver.KillProcessOnPort(port);
            }
            catch (Exception e)
            {
                Debug.Log($"[AIT] 레거시 Production Server 프로세스 정리 중 오류 (무시됨): {e.Message}");
            }
            finally
            {
                EditorPrefs.DeleteKey(LegacyProdPidPrefKey);
                EditorPrefs.DeleteKey(LegacyProdPortPrefKey);
            }
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
            double timeSinceLastValidation = EditorApplication.timeSinceStartup - lastValidationTime;

            // 서버 시작 직후 grace period 동안은 캐시를 더 오래 유지
            // 이 기간에는 포트 확인을 건너뛰고 OnServerStarted에서 설정한 상태를 신뢰
            if (cachedState == ServerState.Running && serverStartedTime > 0)
            {
                double timeSinceStartup = EditorApplication.timeSinceStartup - serverStartedTime;
                if (timeSinceStartup < STARTUP_GRACE_PERIOD_SECONDS)
                {
                    // Grace period 내에서는 1초 간격으로만 검증
                    return timeSinceLastValidation > 1.0;
                }
            }

            return timeSinceLastValidation > CACHE_VALIDITY_SECONDS;
        }

        /// <summary>
        /// 실제 상태 확인 및 캐시 갱신 (동기)
        /// 서버 시작/중지 등 액션 전에 호출
        /// </summary>
        /// <remarks>
        /// 상태 판단: 포트가 열려있으면 Running, 아니면 NotRunning
        /// 포트 기반 확인만 사용하여 단순하고 신뢰할 수 있는 상태 관리
        /// </remarks>
        public ServerState ValidateState()
        {
            // EditorPrefs에서 저장된 PID/Port 로드
            int savedPid = EditorPrefs.GetInt(pidPrefKey, 0);
            int savedPort = EditorPrefs.GetInt(portPrefKey, 0);

            // EditorPrefs가 비어 있으면(직전 검증에서 방금 지워졌거나, cfprefsd flush 지연 등)
            // 도메인 리로드 전까지는 in-memory 캐시 값으로 폴백한다. 인스턴스가 살아있는 동안은
            // SetExpectedPortAndProcess/OnServerStarted가 남긴 값이 EditorPrefs보다 항상 최신이거나
            // 같으므로 폴백해도 안전하다.
            int pid = savedPid > 0 ? savedPid : cachedPid;
            int port = savedPort > 0 ? savedPort : cachedPort;

            // 포트 사용 확인 (가장 신뢰할 수 있는 지표)
            bool portInUse = port > 0 && IsPortInUse(port);

            // 상태 결정: 포트만으로 판단
            if (portInUse)
            {
                // 포트가 열려있으면 서버가 실행 중
                cachedState = ServerState.Running;
                cachedPid = pid;
                cachedPort = port;

                // EditorPrefs가 비어 있어 in-memory 값으로 복구된 경우, 다음 도메인 리로드에
                // 대비해 다시 기록해 둔다.
                if (savedPort <= 0 && port > 0)
                {
                    EditorPrefs.SetInt(portPrefKey, port);
                }
                if (savedPid <= 0 && pid > 0)
                {
                    EditorPrefs.SetInt(pidPrefKey, pid);
                }

                // 프로세스 관리자 복원 시도 (없는 경우, PID가 유효하면)
                if (processManager == null && IsProcessAlive(pid))
                {
                    RestoreProcessManager(pid);
                }
            }
            else
            {
                cachedState = ServerState.NotRunning;

                // 정리할 상태가 애초에 없으면(신규 세션 등) 조용히 넘어간다 — 매 도메인 리로드마다
                // 무의미한 "정리" 로그가 찍히는 것을 방지.
                bool hasTrackedState = savedPid > 0 || savedPort > 0 || cachedPid > 0 || cachedPort > 0 || processManager != null;

                // 포트가 아직 열리지 않았더라도, 추적 중인 PID가 살아있다면(예: 서버가 막 시작되어
                // 아직 포트를 열기 전인 순간) 영속 상태를 지우지 않는다. 다음 검증에서 포트가 열리면
                // 자동으로 Running으로 복구된다. PID가 죽었거나 애초에 없을 때만 정리한다.
                if (hasTrackedState && ShouldClearPersistedState(portInUse, pid, IsProcessAlive))
                {
                    ClearPersistedState($"포트 {port} 미사용 + PID {pid} 비활성 (ValidateState)");
                }
            }

            lastValidationTime = EditorApplication.timeSinceStartup;
            return cachedState;
        }

        /// <summary>
        /// 영속화된(EditorPrefs) 서버 상태를 지워야 하는지 판단하는 순수 함수.
        /// 포트가 열려 있으면(portInUse) 절대 지우지 않고, PID가 없거나(&lt;= 0) 이미 죽었을 때만 지운다.
        /// PID가 살아있다면(서버 프로세스가 시작되어 아직 포트를 열기 전인 순간 등) 일시적으로
        /// portInUse가 false여도 상태를 보존해 다음 검증에서 자동 복구되도록 한다.
        /// </summary>
        /// <param name="portInUse">현재 포트가 사용 중인지 여부</param>
        /// <param name="pid">추적 중인 프로세스 PID (저장값 또는 캐시값)</param>
        /// <param name="isAlive">PID 생존 여부를 확인하는 함수 (테스트 시 대체 가능)</param>
        internal static bool ShouldClearPersistedState(bool portInUse, int pid, Func<int, bool> isAlive)
        {
            if (portInUse) return false;
            if (pid <= 0) return true;
            return !(isAlive?.Invoke(pid) ?? false);
        }

        /// <summary>
        /// 서버 시작 전 호출 - 예상 포트와 프로세스 관리자 저장
        /// 상태는 변경하지 않음 (포트가 열린 후에 Running으로 전환)
        /// </summary>
        /// <param name="manager">시작된 프로세스의 관리자</param>
        /// <param name="expectedPort">예상 포트 (서버가 열 포트)</param>
        public void SetExpectedPortAndProcess(AITProcessTreeManager manager, int expectedPort)
        {
            processManager = manager;
            cachedPid = manager.ProcessId;
            cachedPort = expectedPort;
            // 상태는 변경하지 않음 - 포트가 열린 후에 Running으로 전환됨
            lastValidationTime = EditorApplication.timeSinceStartup;

            // EditorPrefs에 저장 (포트 기반 복원용)
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
            serverStartedTime = EditorApplication.timeSinceStartup;  // Grace period 시작

            // EditorPrefs 업데이트
            EditorPrefs.SetInt(portPrefKey, actualPort);
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
            serverStartedTime = 0;  // Grace period 초기화

            ClearPersistedState("서버 시작 실패 (OnServerFailed)");
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
            serverStartedTime = 0;  // Grace period 초기화

            ClearPersistedState("사용자 요청에 의한 서버 중지 (OnServerStopped)");
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
        /// <param name="reason">정리 사유 (로그 추적용) — 무로그 삭제로 원인 추적이 불가했던 문제 대응</param>
        private void ClearPersistedState(string reason)
        {
            Debug.Log($"[AIT] Dev 서버 영속 상태(EditorPrefs) 정리 — 사유: {reason}");

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
        /// Granite는 0.0.0.0에 바인딩하므로 Any와 Loopback 모두 확인
        /// </summary>
        private static bool IsPortInUse(int port)
        {
            if (port <= 0) return false;

            // 먼저 Any (0.0.0.0) 주소로 확인
            // Granite가 0.0.0.0에 바인딩하므로 이것이 더 정확
            TcpListener listener = null;
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                listener.Stop();
            }
            catch (SocketException)
            {
                return true; // 포트 사용 불가 = 사용 중
            }
            finally
            {
                listener?.Stop();
            }

            // 추가로 Loopback (127.0.0.1)도 확인
            // 다른 프로세스가 127.0.0.1에만 바인딩한 경우를 위해
            try
            {
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
