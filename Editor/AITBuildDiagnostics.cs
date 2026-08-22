using System;
using System.Text;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 빌드 실패 시 마지막 외부 명령(pnpm install / granite build)의 exit code + stderr 말미를 보관해,
    /// 상위 단일 Sentry 캡처(<see cref="ErrorTracker.AITEditorErrorTracker.CaptureBuildError"/>)에
    /// 진단 컨텍스트(extra)로 첨부하기 위한 프로세스-로컬 버퍼.
    ///
    /// 배경: 빌드 실패 cascade(SDK-R5/10P/10Q)는 runner 레벨에서 모두 sentryCapture:false로 Console에만
    /// 남기고, 단일 구조화 이벤트는 errorCode 기반 fingerprint로 상위에서 캡처한다. 그 결과 Sentry 메시지에는
    /// GetErrorMessage(errorCode)의 일반 해결책 텍스트만 담겨 pnpm/granite의 실제 실패 원인(exit code, stderr
    /// 말미)이 빠져 triage가 불가능했다(계획 §5). 이 버퍼는 fingerprint를 errorCode로 유지(= 이슈 1개)하면서
    /// extra로만 원인 진단을 실어 "1 이슈 + triage 가능"을 동시에 만족시킨다.
    ///
    /// 정책:
    ///  - 외부 명령이 최종 실패하면 <see cref="RecordFailure"/>로 마지막 1건만 보관(중간 폴백 단계는 덮어씀).
    ///  - 외부 명령이 성공하면 <see cref="ClearOnSuccess"/>로 직전 폴백 단계의 stale 기록을 비운다.
    ///  - 상위 캡처가 <see cref="ConsumeForCapture"/>로 읽고 즉시 비운다(소비 후 무관한 후속 캡처에 재첨부 방지).
    ///  - stderr은 말미 일부만, 사용자 홈 경로는 &lt;home&gt;으로 마스킹, 도메인 리로드/세션 교체 stale은 신선도 창으로 차단.
    /// </summary>
    internal static class AITBuildDiagnostics
    {
        private const int MaxStderrTailChars = 2000;
        private const int MaxStageChars = 300;

        // 동일 빌드 흐름 내 실패→상위 캡처는 보통 수 초 내 발생한다. 도메인 리로드나 세션 교체로 남은 stale
        // 기록을 무관한 후속 빌드의 캡처에 첨부하지 않도록 신선도 창을 둔다.
        private static readonly TimeSpan StaleThreshold = TimeSpan.FromMinutes(30);

        // 신선도 창 판정용 시계. 프로덕션은 UtcNow, 테스트는 가짜 시각을 주입해 stale 경로를 검증한다.
        internal static Func<DateTime> UtcNowProvider = () => DateTime.UtcNow;

        private static readonly object _gate = new object();
        private static bool _has;
        private static string _stage;
        private static int _exitCode;
        private static string _stderrTail;
        private static DateTime _recordedAtUtc;

        /// <summary>
        /// 외부 명령(pnpm/granite)이 실패했을 때 stage 라벨 + exit code + stderr 말미를 기록한다.
        /// stderr이 비어 있으면 stdout 말미로 폴백한다. 마지막 1건만 유지하므로 중간 폴백 단계는 덮어쓰여
        /// 최종적으로 가장 마지막(=터미널) 실패가 남는다.
        /// </summary>
        internal static void RecordFailure(string stage, int exitCode, string stderr, string stdout = null)
        {
            string raw = !string.IsNullOrEmpty(stderr) ? stderr : stdout;
            string tail = BuildTail(raw);
            string maskedStage = CapStage(MaskHome(stage));
            lock (_gate)
            {
                _has = true;
                _stage = maskedStage;
                _exitCode = exitCode;
                _stderrTail = tail;
                _recordedAtUtc = UtcNowProvider();
            }
        }

        /// <summary>
        /// 외부 명령이 최종 성공하면 직전 폴백 단계의 실패 기록을 비운다. 복구된 단계의 stderr이 이후 무관한
        /// 빌드 실패 캡처에 잘못 첨부되는 것을 방지한다.
        /// </summary>
        internal static void ClearOnSuccess()
        {
            lock (_gate)
            {
                _has = false;
                _stage = null;
                _stderrTail = null;
                _exitCode = 0;
                _recordedAtUtc = default;
            }
        }

        /// <summary>
        /// 상위 빌드 실패 캡처가 호출. 신선한 진단이 있으면 사람이 읽을 수 있는 블록(stage/exit_code/stderr_tail)을
        /// 반환하고 버퍼를 비운다. 진단이 없거나 신선도 창을 벗어났으면 <c>null</c>.
        /// </summary>
        internal static string ConsumeForCapture()
        {
            lock (_gate)
            {
                if (!_has)
                    return null;

                bool fresh = (UtcNowProvider() - _recordedAtUtc) < StaleThreshold;
                string block = null;
                if (fresh)
                {
                    var sb = new StringBuilder();
                    sb.Append("stage: ").Append(string.IsNullOrEmpty(_stage) ? "(unknown)" : _stage).Append('\n');
                    sb.Append("exit_code: ").Append(_exitCode);
                    if (!string.IsNullOrEmpty(_stderrTail))
                        sb.Append('\n').Append("stderr_tail:\n").Append(_stderrTail);
                    block = sb.ToString();
                }

                // 신선하든 stale이든 소비했으면 비운다(소비-once).
                _has = false;
                _stage = null;
                _stderrTail = null;
                _exitCode = 0;
                _recordedAtUtc = default;
                return block;
            }
        }

        private static string BuildTail(string s)
        {
            if (string.IsNullOrEmpty(s))
                return null;
            string masked = MaskHome(s.Trim());
            if (masked.Length <= MaxStderrTailChars)
                return masked;
            return "…(truncated)\n" + masked.Substring(masked.Length - MaxStderrTailChars);
        }

        private static string CapStage(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            return s.Length <= MaxStageChars ? s : s.Substring(0, MaxStageChars) + "…";
        }

        // 사용자 홈 경로를 <home>으로 마스킹해 Sentry extra의 PII를 최소화한다.
        private static string MaskHome(string s)
        {
            if (string.IsNullOrEmpty(s))
                return s;
            try
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(home))
                    s = s.Replace(home, "<home>");
            }
            catch
            {
                // 환경 조회 실패는 무시 — 마스킹은 best-effort.
            }
            return s;
        }
    }
}
