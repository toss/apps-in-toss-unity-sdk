using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 크로스 플랫폼 프로세스 트리 관리자
    /// Windows: Job Objects 사용 - 프로세스 그룹을 관리하고 Unity 종료 시 자동 정리
    /// Unix: setsid + kill -PGID 사용 - 프로세스 그룹 전체 종료
    /// </summary>
    public class AITProcessTreeManager : IDisposable
    {
        private Process managedProcess;
        private int processGroupId;
        private bool disposed;

#if UNITY_EDITOR_WIN
        private IntPtr jobHandle = IntPtr.Zero;

        #region Windows P/Invoke

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateJobObject(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr hJob, JobObjectInfoType infoType,
            IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool TerminateJobObject(IntPtr hJob, uint uExitCode);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private enum JobObjectInfoType
        {
            ExtendedLimitInformation = 9
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x2000;

        #endregion
#endif

        /// <summary>
        /// 관리 중인 프로세스 ID
        /// </summary>
        public int ProcessId => managedProcess?.Id ?? 0;

        /// <summary>
        /// 프로세스가 종료되었는지 확인
        /// </summary>
        public bool HasExited
        {
            get
            {
                if (managedProcess == null) return true;
                try
                {
                    return managedProcess.HasExited;
                }
                catch
                {
                    return true;
                }
            }
        }

        /// <summary>
        /// 관리 중인 Process 객체 반환
        /// </summary>
        public Process Process => managedProcess;

        /// <summary>
        /// 프로세스 트리 관리자로 프로세스 시작
        /// </summary>
        /// <param name="startInfo">프로세스 시작 정보</param>
        /// <returns>시작된 프로세스</returns>
        public Process StartProcess(ProcessStartInfo startInfo)
        {
            if (managedProcess != null && !HasExited)
            {
                throw new InvalidOperationException("프로세스가 이미 실행 중입니다. 먼저 KillProcessTree()를 호출하세요.");
            }

#if UNITY_EDITOR_WIN
            // Windows: Job Object 생성 및 설정
            CreateWindowsJobObject();
#endif

            managedProcess = new Process { StartInfo = startInfo };
            managedProcess.Start();
            processGroupId = managedProcess.Id;

#if UNITY_EDITOR_WIN
            // Windows: 프로세스를 Job Object에 할당
            AssignToJobObject();
#endif

            return managedProcess;
        }

#if UNITY_EDITOR_WIN
        /// <summary>
        /// Windows Job Object 생성
        /// </summary>
        private void CreateWindowsJobObject()
        {
            jobHandle = CreateJobObject(IntPtr.Zero, null);
            if (jobHandle == IntPtr.Zero)
            {
                Debug.LogWarning("[AITProcessTreeManager] Job Object 생성 실패");
                return;
            }

            // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE 설정
            var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
            {
                BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
                {
                    LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                }
            };

            int infoSize = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
            IntPtr infoPtr = Marshal.AllocHGlobal(infoSize);
            try
            {
                Marshal.StructureToPtr(info, infoPtr, false);
                if (!SetInformationJobObject(jobHandle, JobObjectInfoType.ExtendedLimitInformation, infoPtr, (uint)infoSize))
                {
                    Debug.LogWarning("[AITProcessTreeManager] Job Object 설정 실패");
                }
            }
            finally
            {
                Marshal.FreeHGlobal(infoPtr);
            }
        }

        /// <summary>
        /// 프로세스를 Job Object에 할당
        /// </summary>
        private void AssignToJobObject()
        {
            if (jobHandle == IntPtr.Zero || managedProcess == null)
                return;

            if (!AssignProcessToJobObject(jobHandle, managedProcess.Handle))
            {
                Debug.LogWarning("[AITProcessTreeManager] 프로세스를 Job Object에 할당 실패");
            }
        }
#endif

        /// <summary>
        /// 프로세스 트리 전체 종료
        /// </summary>
        public void KillProcessTree()
        {
            if (disposed) return;

#if UNITY_EDITOR_WIN
            KillProcessTreeWindows();
#else
            KillProcessTreeUnix();
#endif
            CleanupProcess();
        }

#if UNITY_EDITOR_WIN
        /// <summary>
        /// Windows: Job Object 또는 taskkill /T로 프로세스 트리 종료
        /// </summary>
        private void KillProcessTreeWindows()
        {
            // 1차: Job Object로 종료
            if (jobHandle != IntPtr.Zero)
            {
                try
                {
                    TerminateJobObject(jobHandle, 0);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[AITProcessTreeManager] Job Object 종료 실패: {ex.Message}");
                }
            }

            // 2차: taskkill /T로 백업 종료 (Job Object 할당 실패한 경우)
            if (processGroupId > 0)
            {
                try
                {
                    var process = Process.GetProcessById(processGroupId);
                    if (!process.HasExited)
                    {
                        // taskkill /F /T /PID: 프로세스와 모든 자식 강제 종료
                        AITPlatformHelper.ExecuteCommand(
                            $"taskkill /F /T /PID {processGroupId}",
                            null, null, timeoutMs: 5000, verbose: false
                        );
                    }
                }
                catch
                {
                    // 프로세스가 이미 종료됨
                }
            }

            // Job Handle 정리
            if (jobHandle != IntPtr.Zero)
            {
                CloseHandle(jobHandle);
                jobHandle = IntPtr.Zero;
            }
        }
#else
        /// <summary>
        /// Unix: 자식 프로세스를 재귀적으로 찾아서 종료
        /// macOS에는 setsid가 없으므로 pgrep/pkill 방식 사용
        /// </summary>
        private void KillProcessTreeUnix()
        {
            if (processGroupId <= 0) return;

            try
            {
                // 방법 1: pkill -P로 자식 프로세스 종료 (재귀적으로)
                // pkill -TERM -P <PID>: 해당 PID의 직접 자식들에게 SIGTERM
                // 여러 번 실행하여 손자 프로세스까지 처리
                for (int i = 0; i < 3; i++)
                {
                    AITPlatformHelper.ExecuteCommand(
                        $"pkill -TERM -P {processGroupId} 2>/dev/null",
                        null, null, timeoutMs: 1000, verbose: false
                    );
                }

                // 잠시 대기 후 SIGKILL
                System.Threading.Thread.Sleep(300);

                for (int i = 0; i < 3; i++)
                {
                    AITPlatformHelper.ExecuteCommand(
                        $"pkill -9 -P {processGroupId} 2>/dev/null",
                        null, null, timeoutMs: 1000, verbose: false
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AITProcessTreeManager] 자식 프로세스 종료 실패: {ex.Message}");
            }

            // 부모 프로세스도 종료
            if (managedProcess != null && !managedProcess.HasExited)
            {
                try
                {
                    managedProcess.Kill();
                    managedProcess.WaitForExit(2000);
                }
                catch
                {
                    // 무시
                }
            }
        }
#endif

        /// <summary>
        /// 프로세스 참조 정리
        /// </summary>
        private void CleanupProcess()
        {
            if (managedProcess != null)
            {
                try
                {
                    managedProcess.Dispose();
                }
                catch
                {
                    // 무시
                }
                managedProcess = null;
            }
            processGroupId = 0;
        }

        /// <summary>
        /// 저장된 PID로 프로세스 트리 관리자 복원 (도메인 리로드 후)
        /// </summary>
        /// <param name="pid">저장된 프로세스 ID</param>
        /// <returns>복원 성공 여부</returns>
        public bool RestoreFromPid(int pid)
        {
            if (pid <= 0) return false;

            try
            {
                var process = Process.GetProcessById(pid);
                if (process.HasExited)
                {
                    return false;
                }

                managedProcess = process;
                processGroupId = pid;

#if UNITY_EDITOR_WIN
                // Windows: Job Object는 복원 불가 - taskkill /T로 대체
                // 새 Job Object를 생성해서 기존 프로세스에 할당할 수 없음
                // (프로세스는 한 번만 Job에 할당 가능)
                jobHandle = IntPtr.Zero;
#endif

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 리소스 정리
        /// </summary>
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            KillProcessTree();
        }
    }
}
