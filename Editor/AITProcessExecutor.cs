using System.Diagnostics;
using System.Threading.Tasks;

namespace AppsInToss.Editor
{
    /// <summary>
    /// "프로세스 생성 → 타임아웃 대기 → 출력 캡처"의 공통 동기 실행 패턴을 캡슐화한다.
    /// 비동기 리더(ReadToEndAsync) + WaitForExit(타임아웃) + Kill + 2단계 drain의
    /// 까다로운 데드락/타임아웃 처리를 한 곳에 모은다.
    ///
    /// 공유 대상: <see cref="AITGitGuard"/>(git 직접 실행), <see cref="AITPlatformHelper"/>(셸 래핑).
    /// 각 호출부는 자신만의 ProcessStartInfo를 만들어 넘기고, 반환된 raw 결과를 자신의
    /// 결과 타입/로깅/ANSI 처리로 매핑한다.
    ///
    /// <para>
    /// AITAsyncCommandRunner는 취소 가능 + 라이브 스트리밍(OutputDataReceived 이벤트) 모델이라
    /// 이 동기 실행기와 의도적으로 분리한다 — 여기에 접으면 취소 기능을 잃는다.
    /// </para>
    /// </summary>
    internal static class AITProcessExecutor
    {
        /// <summary>프로세스 실행 raw 결과. <see cref="TimedOut"/>가 true면 나머지 필드는 신뢰 불가.</summary>
        internal struct Result
        {
            /// <summary>타임아웃으로 프로세스를 강제 종료했는지 여부.</summary>
            public bool TimedOut;
            public int ExitCode;
            public string StdOut;
            public string StdErr;
        }

        /// <summary>
        /// 주어진 <paramref name="startInfo"/>로 프로세스를 시작하고 stdout/stderr를 비동기로 읽어
        /// 데드락을 방지하면서 <paramref name="timeoutMs"/>까지 종료를 기다린다. 타임아웃 시
        /// Kill 후 리더를 상한 있는 best-effort로 drain하고 <c>TimedOut=true</c>를 반환한다.
        ///
        /// 프로세스 시작 실패(바이너리 부재 등)는 예외를 전파한다 — 호출부가 처리한다.
        /// startInfo는 RedirectStandardOutput/RedirectStandardError가 true여야 한다.
        /// </summary>
        internal static Result Run(ProcessStartInfo startInfo, int timeoutMs)
        {
            using (var process = new Process { StartInfo = startInfo })
            {
                process.Start();

                // stdout/stderr를 모두 비동기로 읽어 데드락 방지.
                // ReadToEnd()는 프로세스 종료까지 블로킹하므로 WaitForExit 타임아웃이 무효화됨.
                // ReadToEndAsync()는 백그라운드에서 읽기 시작하므로 WaitForExit가 실제 타임아웃으로 동작.
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();

                if (!process.WaitForExit(timeoutMs))
                {
                    try { process.Kill(); }
                    catch { /* 이미 종료/권한 부족 등 — 무시 */ }

                    // 리더를 drain하되 상한을 둔다: 셸이 장수명 자식(예: sleep)을 남기면
                    // 그 자식이 파이프 쓰기단을 계속 잡고 있어 EOF가 안 와 무한 대기할 수 있다.
                    // 타임아웃 경로에선 출력을 어차피 버리므로 500ms만 시도한다.
                    try { Task.WaitAll(new Task[] { stdoutTask, stderrTask }, 500); }
                    catch { /* 읽기 실패 무시 */ }

                    return new Result
                    {
                        TimedOut = true,
                        ExitCode = -1,
                        StdOut = TryGetResult(stdoutTask),
                        StdErr = TryGetResult(stderrTask)
                    };
                }

                // 타임아웃 강제(WaitForExit(int)) + 리더 drain 확정(WaitForExit())의 2단계 패턴.
                // 타임아웃 오버로드 WaitForExit(int)은 stdout/stderr 비동기 리더 배수(drain)를
                // 보장하지 않음. 파라미터 없는 오버로드를 한 번 더 호출해 리더 완료를 확정함.
                // (이 시점에서 프로세스는 이미 exit 상태이므로 리더가 EOF로 곧 완료됨.
                //  둘 중 한 호출만 남기지 말 것 — 하나만 있으면 hang 또는 drain 누락 발생.)
                process.WaitForExit();

                // 이 시점에서 두 리더 Task는 완료 상태이므로 동기 접근에 데드락 위험 없음.
                // .Result 대신 GetAwaiter().GetResult()를 사용해 예외 래핑(AggregateException)을 피함.
                return new Result
                {
                    TimedOut = false,
                    ExitCode = process.ExitCode,
                    StdOut = stdoutTask.GetAwaiter().GetResult(),
                    StdErr = stderrTask.GetAwaiter().GetResult()
                };
            }
        }

        /// <summary>
        /// 완료된 리더 Task에서 결과를 논블로킹으로 꺼낸다. 아직 미완료(상한 drain 초과)이거나
        /// faulted면 빈 문자열 — .Result 접근으로 다시 블로킹하지 않는다.
        /// </summary>
        private static string TryGetResult(Task<string> task)
        {
            try
            {
                if (task.IsCompleted && !task.IsFaulted && !task.IsCanceled)
                {
                    return task.Result;
                }
            }
            catch { /* 무시 */ }
            return string.Empty;
        }
    }
}
