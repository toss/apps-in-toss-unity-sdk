using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using AppsInToss;
using AppsInToss.Editor;
using AppsInToss.Editor.ErrorTracker;
using AppsInToss.Editor.IssueReport;

namespace AppsInToss.Editor.Menu
{
    /// <summary>
    /// 배포 종류 — 콘솔 QR 테스트 환경으로의 배포 목적을 구분한다.
    /// </summary>
    /// <remarks>
    /// `ait deploy` CLI는 플래그와 무관하게 항상 테스트(QR) 환경에 배포하며, 실제 출시는
    /// 콘솔 심사/출시 신청으로만 가능하다. 두 값은 배포 자체의 동작 차이(빌드 방식·memo 접두사·
    /// 성공 창의 콘솔 안내 노출 여부)만 가른다 — CLI 호출 자체는 동일하다.
    /// </remarks>
    internal enum DeployKind
    {
        Test,
        Production
    }

    /// <summary>
    /// 빌드/배포 실행 로직 (Deploy (Test/Production), Build &amp; Package).
    /// AppsInTossMenu의 [MenuItem] 진입점에서 위임 받아 실행됩니다.
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class AITDeployManager
    {
        // ait deploy -m/--memo 플래그의 CLI 측 최대 길이.
        internal const int MaxMemoLength = 1000;

        // 재진입 가드 — RunBuildAndPackage / RunDeploy 가 await 대기 중일 때 중복 클릭 차단.
        private static bool _buildEntryInProgress;

        // 빌드 소요 시간 측정 (StartServer의 buildStopwatch와 독립)
        private static Stopwatch _buildStopwatch = new Stopwatch();

        // ==================== Deploy (Test) / Deploy (Production) ====================

        /// <summary>
        /// Deploy (Test) / Deploy (Production) 메뉴의 실제 실행 로직.
        /// AppsInTossMenu.DeployTest()/DeployProduction() [MenuItem] 에서 위임됩니다.
        /// </summary>
        /// <remarks>
        /// `ait deploy`는 플래그와 무관하게 항상 콘솔 QR 테스트 환경에 배포한다(CLI에 릴리즈
        /// 기능 없음). 두 메뉴는 그래서 배포 자체의 목적지가 아니라 빌드 방식(증분/클린)과
        /// memo 접두사, 성공 창의 콘솔 안내 노출 여부만 가른다.
        /// </remarks>
        internal static async void RunDeploy(DeployKind kind)
        {
            if (_buildEntryInProgress)
            {
                AITLog.Warning("AIT: 이미 빌드/배포 준비가 진행 중입니다.", sentryCapture: false);
                return;
            }
            _buildEntryInProgress = true;
            string profileName = ProfileNameFor(kind);
            try
            {
                var config = UnityUtil.GetEditorConf();
                if (!PathValidator.ValidateSettingsForPackage(config)) return;

                // Configuration Window 미flush 변경분을 빌드 진입 전 디스크에 강제 기록 (유실 방지)
                AssetDatabase.SaveAssets();

                // 리로드를 유발할 수 있는 컴파일/업데이트가 끝난 뒤 빌드 진입.
                if (!await AITEditorIdleWaiter.WaitAsync()) return;

                // 빌드 전 배포 키 사전 체크 (fail-fast)
                string deploymentKey = AITCredentialsUtil.GetDeploymentKey();
                if (string.IsNullOrWhiteSpace(deploymentKey))
                {
                    AITLog.Error("AIT: 배포 키가 설정되지 않았습니다.", sentryCapture: false);
                    AITPlatformHelper.ShowInfoDialog("오류", "배포 키가 설정되지 않았습니다.\n\nApps in Toss > Configuration에서 배포 키를 입력해주세요.", "확인");
                    return;
                }

                // Production은 현행 Publish와 동일하게 클린 빌드, Test는 반복 속도를 위해 증분 빌드 +
                // 빠른 빌드(IL2CPP Debug + Code Generation OptimizeSize + 에셋 최적화 검사 스킵).
                var (cleanBuild, fastBuild) = GetBuildFlags(kind);
                string il2cppMode = fastBuild ? "Debug/OptimizeSize" : "기본";

                Debug.Log($"AIT: {profileName} 빌드 시작 (cleanBuild={cleanBuild}, fastBuild={fastBuild}, IL2CPP={il2cppMode})...");
                _buildStopwatch.Restart();

                // DoExportAsync 병렬 경로로 전환 — pnpm install이 WebGL 빌드 시간에 숨겨진다
                // (StartServer의 동일 패턴 참조). onComplete를 TaskCompletionSource로 감싸 await함으로써
                // 이 async void 메서드의 try/finally 재진입 가드가 배포 흐름 완료/실패까지 유지되도록 한다.
                var tcs = new TaskCompletionSource<AITConvertCore.AITExportError>();

                AITConvertCore.DoExportAsync(
                    buildWebGL: true,
                    doPackaging: true,
                    cleanBuild: cleanBuild,
                    profile: config.productionProfile,
                    profileName: profileName,
                    onComplete: (result) => tcs.TrySetResult(result),
                    onProgress: (phase, progress, status) =>
                    {
                        // DisplayCancelableProgressBar로 취소 가능한 진행률 표시 (AITDeployManager/StartServer와 동일 패턴)
                        bool cancelled = EditorUtility.DisplayCancelableProgressBar(
                            $"Apps in Toss - {profileName}",
                            status,
                            progress
                        );

                        if (cancelled)
                        {
                            AITConvertCore.CancelBuild();
                        }
                    },
                    skipGraniteBuild: false,
                    fastBuild: fastBuild
                );

                var result = await tcs.Task;
                _buildStopwatch.Stop();
                EditorUtility.ClearProgressBar();

                if (result == AITConvertCore.AITExportError.CANCELLED)
                {
                    Debug.Log("AIT: 빌드가 사용자에 의해 취소되었습니다.");
                    AITPlatformHelper.ShowInfoDialog("취소됨", "빌드가 취소되었습니다.", "확인");
                    return;
                }

                if (result != AITConvertCore.AITExportError.SUCCEED)
                {
                    ShowBuildFailedDialog(result, profileName);
                    return;
                }

                Debug.Log($"AIT: 빌드 완료 (소요 시간: {_buildStopwatch.Elapsed.TotalSeconds:F1}초)");

                // 배포 실행 (성공 시에만)
                ExecuteDeploy(kind);
            }
            catch (Exception e)
            {
                // async void 의 미처리 예외는 SynchronizationContext 로 터져 Editor 전체에
                // 영향을 주므로 여기서 삼키고 사용자에게 다이얼로그로 알린다.
                AITLog.Error($"AIT: {profileName} 중 예외: {e.Message}", sentryCapture: true);
                AITPlatformHelper.ShowInfoDialog("오류", $"배포 중 오류가 발생했습니다.\n\n{e.Message}", "확인");
            }
            finally
            {
                // 정상 종료 시 128행에서 이미 ClearProgressBar가 호출되지만, DoExportAsync가
                // onComplete를 끝내 호출하지 못해 await가 영영 완료되지 않는 경로(예: 동기 구간
                // 예외로 tcs가 set되지 않는 경우)에서도 진행률 바가 남지 않도록 finally에서도 정리한다
                // (StartServer의 catch 블록과 동일한 방어 — AppsInTossMenu.cs 참조).
                EditorUtility.ClearProgressBar();
                _buildEntryInProgress = false;
            }
        }

        private static string ProfileNameFor(DeployKind kind) =>
            kind == DeployKind.Production ? "Deploy (Production)" : "Deploy (Test)";

        /// <summary>
        /// DeployKind별 빌드 플래그 매트릭스.
        /// Production: 클린 빌드(cleanBuild=true) + 기존 IL2CPP 설정(fastBuild=false) — 현행 Publish와 동일.
        /// Test: 증분 빌드(cleanBuild=false) + 빠른 빌드(fastBuild=true) — IL2CPP Debug + Code Generation
        /// OptimizeSize + 에셋 최적화 검사 스킵으로 반복 배포 속도 개선 (Dev Server와 동일 레버).
        /// </summary>
        internal static (bool cleanBuild, bool fastBuild) GetBuildFlags(DeployKind kind)
        {
            return kind switch
            {
                DeployKind.Production => (cleanBuild: true, fastBuild: false),
                DeployKind.Test => (cleanBuild: false, fastBuild: true),
                // 안전한 기본값: 향후 DeployKind가 추가되어도 미인지 값이 자동으로 빠른 빌드
                // (IL2CPP Debug/OptimizeSize)를 받는 일이 없도록 명시적으로 실패시킨다.
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "정의되지 않은 DeployKind"),
            };
        }

        // ==================== Build & Package ====================

        /// <summary>
        /// Build &amp; Package 메뉴의 실제 실행 로직.
        /// AppsInTossMenu.BuildAndPackage() [MenuItem] 에서 위임됩니다.
        /// </summary>
        internal static async void RunBuildAndPackage()
        {
            if (_buildEntryInProgress)
            {
                AITLog.Warning("AIT: 이미 빌드/배포 준비가 진행 중입니다.", sentryCapture: false);
                return;
            }
            _buildEntryInProgress = true;
            try
            {
                var config = UnityUtil.GetEditorConf();
                if (!PathValidator.ValidateSettingsForPackage(config)) return;

                // Configuration Window에서 방금 입력한 변경이 디스크에 flush되기 전 상태일 수 있다.
                // 빌드 중 도메인 리로드 또는 Editor 강제 종료 시 유실을 방지하기 위해 강제 flush.
                AssetDatabase.SaveAssets();

                // 리로드를 유발할 수 있는 컴파일/업데이트가 끝난 뒤 빌드 진입.
                if (!await AITEditorIdleWaiter.WaitAsync()) return;

                Debug.Log("AIT: 전체 빌드 & 패키징 시작...");
                _buildStopwatch.Restart();

                // Build & Package 메뉴는 productionProfile 사용
                AITConvertCore.DoExportAsync(
                    buildWebGL: true,
                    doPackaging: true,
                    cleanBuild: false,
                    profile: config.productionProfile,
                    profileName: "Build & Package",
                    onComplete: (result) =>
                    {
                        _buildStopwatch.Stop();
                        EditorUtility.ClearProgressBar();

                        if (result == AITConvertCore.AITExportError.SUCCEED)
                        {
                            Debug.Log($"AIT: 전체 프로세스 완료! (총 소요 시간: {_buildStopwatch.Elapsed.TotalSeconds:F1}초)");
                            AITPlatformHelper.ShowInfoDialog("성공", $"빌드 & 패키징이 완료되었습니다!\n\n총 소요 시간: {_buildStopwatch.Elapsed.TotalSeconds:F1}초", "확인");
                        }
                        else if (result == AITConvertCore.AITExportError.CANCELLED)
                        {
                            Debug.Log("AIT: 빌드가 사용자에 의해 취소되었습니다.");
                            AITPlatformHelper.ShowInfoDialog("취소됨", "빌드가 취소되었습니다.", "확인");
                        }
                        else
                        {
                            ShowBuildFailedDialog(result, "Build & Package");
                        }
                    },
                    onProgress: (phase, progress, status) =>
                    {
                        // DisplayCancelableProgressBar로 취소 가능한 진행률 표시
                        string phaseText = GetPhaseText(phase);

                        bool cancelled = EditorUtility.DisplayCancelableProgressBar(
                            $"Apps in Toss - {phaseText}",
                            status,
                            progress
                        );

                        if (cancelled)
                        {
                            AITConvertCore.CancelBuild();
                        }
                    }
                );
            }
            catch (Exception e)
            {
                AITLog.Error($"AIT: Build & Package 중 예외: {e.Message}", sentryCapture: true);
                AITPlatformHelper.ShowInfoDialog("오류", $"빌드 준비 중 오류가 발생했습니다.\n\n{e.Message}", "확인");
            }
            finally
            {
                // 재진입 가드 해제 — DoExportAsync 는 fire-and-forget 이므로 여기 도달 시점에는
                // 실제 빌드는 이미 시작되었다 (중복 실제 빌드는 AITConvertCore 내부의 별도
                // 상태로 차단된다). 이 플래그는 "WaitAsync 대기 중 중복 클릭" 만 차단.
                _buildEntryInProgress = false;
            }
        }

        // ==================== Deploy ====================

        /// <summary>
        /// 실제 배포 CLI 호출 로직.
        /// </summary>
        private static void ExecuteDeploy(DeployKind kind)
        {
            var config = UnityUtil.GetEditorConf();
            if (!PathValidator.ValidateSettingsForPackage(config)) return;

            // AITCredentials에서 배포 키 로드
            string deploymentKey = AITCredentialsUtil.GetDeploymentKey();
            if (string.IsNullOrWhiteSpace(deploymentKey))
            {
                AITLog.Error("AIT: 배포 키가 설정되지 않았습니다.", sentryCapture: false);
                AITPlatformHelper.ShowInfoDialog("오류", "배포 키가 설정되지 않았습니다.\n\nApps in Toss > Configuration에서 배포 키를 입력해주세요.", "확인");
                return;
            }

            string buildPath = PathValidator.GetBuildTemplatePath();

            // npm 경로 찾기
            string npmPath = PathValidator.FindNpmPath();
            if (string.IsNullOrEmpty(npmPath))
            {
                AITLog.Error("AIT: npm을 찾을 수 없습니다. Node.js가 설치되어 있는지 확인하세요.", sentryCapture: false);
                AITPlatformHelper.ShowInfoDialog("오류", "npm을 찾을 수 없습니다.\n\nNode.js가 설치되어 있는지 확인하세요.", "확인");
                return;
            }

            string memo = BuildDeployMemo(kind, config.appName, config.version);
            string profileName = ProfileNameFor(kind);

            // Deploy (Test)는 빠른 빌드(IL2CPP Debug + Code Generation OptimizeSize) 산출물이라
            // 런타임 성능이 실제 출시 빌드와 다르다 — QR로 성능을 재는 테스터가 오해하지 않도록 고지.
            string fastBuildNotice = kind == DeployKind.Test
                ? "\n\n⚠ Deploy (Test)는 빠른 빌드(IL2CPP Debug/OptimizeSize)로 생성되어 런타임 성능이 실제 출시 빌드와 다릅니다."
                : "";

            bool confirmed = AITPlatformHelper.ShowConfirmDialog(
                "배포 확인",
                $"Apps in Toss에 배포하시겠습니까? ({profileName})\n\n프로젝트: {config.appName}\n버전: {config.version}\nMemo: {memo}{fastBuildNotice}",
                "배포",
                "취소",
                autoApprove: true
            );

            if (!confirmed) return;

            Debug.Log($"AIT: Apps in Toss 배포 시작... ({profileName})");

            try
            {
                string npmDir = Path.GetDirectoryName(npmPath);

                // pnpm run deploy를 사용하여 로컬 node_modules/.bin/ait 사용
                string pnpmName = AITPlatformHelper.IsWindows ? "pnpm.cmd" : "pnpm";
                string pnpmPath = Path.Combine(npmDir, pnpmName);

                // pnpm exec ait deploy --api-key "KEY" -m "MEMO" 형태로 직접 실행
                // additionalPaths는 BuildAdditionalPaths로 구성한다(npmDir 단독 전달 금지).
                // node_modules/.bin이 PATH에서 빠지면 Windows에서 'ait' is not recognized로 배포가 실패한다
                // (build 경로 RunNpmCommandWithCache와 동일한 PATH 구성). Sentry APPS-IN-TOSS-UNITY-SDK-12J.
                // memo는 BuildDeployMemo가 이미 셸 인용을 깨는 문자를 제거한 상태다(SanitizeMemo).
                // EscapeMemoForShell은 그 위의 심층 방어층 — 이 명령 문자열 전체가 이후
                // bash -l -c "..."로 한 번 더 감싸이므로(AITPlatformHelper.CreateProcessStartInfo)
                // 이스케이프에만 의존하면 층이 중첩되어 원본에 없던 백슬래시가 memo에 남는다.
                string escapedMemo = EscapeMemoForShell(memo);
                string command = $"\"{pnpmPath}\" exec ait deploy --api-key \"{deploymentKey}\" -m \"{escapedMemo}\"";
                var additionalPaths = AITNpmRunner.BuildAdditionalPaths(npmPath, buildPath);
                var result = AITPlatformHelper.ExecuteCommand(
                    command,
                    buildPath,
                    additionalPaths.ToArray(),
                    timeoutMs: 300000,
                    verbose: true
                );

                if (!result.Success)
                {
                    if (result.ExitCode == -1)
                    {
                        // 사용자 환경(네트워크/원격 응답 지연) 원인 — 다이얼로그로 가시화하며 Sentry는 차단.
                        AITLog.Error("AIT: 배포 타임아웃 (5분 초과)", sentryCapture: false);
                        AITPlatformHelper.ShowInfoDialog("타임아웃", "배포 시간이 초과되었습니다.", "확인");
                    }
                    else
                    {
                        // 'ait deploy' CLI exit != 0 — 원인은 인증 실패(401/403), 서버 오류,
                        // 네트워크, 사용자 환경 등 사용자에게 actionable한 외부 요인. 다이얼로그에서
                        // 인증/서버 분기로 가이드를 보여주므로 Sentry로 흘리면 stdout/stderr 변형이
                        // 다수의 별도 fingerprint(SDK-B9, SDK-RF cascade)를 만든다.
                        string errorDetail = ExtractDeployErrorMessage(result.Output, result.Error);
                        AITLog.Error($"AIT: 배포 실패 (Exit Code: {result.ExitCode})", sentryCapture: false);
                        if (!string.IsNullOrEmpty(result.Output))
                            AITLog.Error($"AIT: [stdout] {result.Output}", sentryCapture: false);
                        if (!string.IsNullOrEmpty(result.Error))
                            AITLog.Error($"AIT: [stderr] {result.Error}", sentryCapture: false);

                        bool isAuthError = !string.IsNullOrEmpty(errorDetail) &&
                            (errorDetail.Contains("403") || errorDetail.Contains("401") ||
                             errorDetail.Contains("Forbidden") || errorDetail.Contains("Unauthorized"));

                        string shortReason = isAuthError ? "인증 실패" : "서버 오류";
                        string cause = !string.IsNullOrEmpty(errorDetail)
                            ? errorDetail
                            : "배포 서버로부터 오류 응답을 받았습니다. Console 로그에서 상세 내용을 확인해주세요.";

                        string title = $"배포 실패 ({shortReason})";
                        string dialogMessage =
                            "앱인토스 미니앱 배포에 실패했습니다.\n\n" +
                            $"{cause}";

                        if (isAuthError)
                        {
                            dialogMessage += "\n\n다음 항목을 확인해주세요:";
                            dialogMessage += "\n• 배포 키가 올바른지 확인 (Apps in Toss 콘솔 > 워크스페이스 > 키 관리)";
                            dialogMessage += "\n• 앱 이름(appName)이 콘솔에 등록된 이름과 일치하는지 확인";
                            dialogMessage += $"\n  현재 설정된 appName: {config.appName}";
                            dialogMessage += "\n• 배포 키가 해당 앱의 워크스페이스에서 발급되었는지 확인";
                        }

                        dialogMessage += "\n\n문제를 공유하려면 'Issue 신고'를 눌러주세요.";

                        int choice = AITPlatformHelper.ShowComplexDialog(
                            title,
                            dialogMessage,
                            "확인",
                            "Issue 신고",
                            null,
                            defaultChoice: 0
                        );
                        if (choice == 1)
                        {
                            AITIssueReportWindow.Open(
                                AITIssueReportContext.BuildFailure,
                                linkedEventId: AITEditorErrorTracker.LastEventId,
                                prefilledTitle: title);
                        }
                    }
                }
                else
                {
                    Debug.Log("AIT: 배포 완료!");
                    string deployUrl = ExtractDeployUrl(result.Output);
                    if (!string.IsNullOrEmpty(deployUrl))
                    {
                        Debug.Log($"AIT: 배포 URL: {deployUrl}");
                        if (!AITPlatformHelper.IsNonInteractive)
                        {
                            DeploySuccessWindow.Show(deployUrl, kind);
                        }
                    }
                    else
                    {
                        AITPlatformHelper.ShowInfoDialog("성공", "Apps in Toss에 배포되었습니다!", "확인");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"AIT: 배포 오류: {e}");
                AITPlatformHelper.ShowInfoDialog("오류", $"배포 오류:\n{e.Message}", "확인");
            }
        }

        // ==================== 유틸리티 ====================

        private static string GetPhaseText(AITConvertCore.BuildPhase phase)
        {
            switch (phase)
            {
                case AITConvertCore.BuildPhase.Preparing: return "준비 중";
                case AITConvertCore.BuildPhase.WebGLBuild: return "WebGL 빌드";
                case AITConvertCore.BuildPhase.CopyingFiles: return "파일 복사";
                case AITConvertCore.BuildPhase.PnpmInstall: return "pnpm install";
                case AITConvertCore.BuildPhase.GraniteBuild: return "granite build";
                case AITConvertCore.BuildPhase.Complete: return "완료";
                default: return "빌드 중";
            }
        }

        /// <summary>
        /// 배포 memo 자동 생성: "[Test] {appName} v{version} · Unity SDK {AITVersion.Version}"
        /// (Production은 [Production] 접두사). 셸 인용을 깨는 문자를 소스에서 무해화한 뒤
        /// CLI의 -m/--memo 최대 길이(1000자)에 맞춰 잘라낸다(무해화는 길이를 늘리지 않으므로
        /// 절단 후 재팽창이 없다).
        /// </summary>
        internal static string BuildDeployMemo(DeployKind kind, string appName, string version)
        {
            string prefix = kind == DeployKind.Production ? "[Production]" : "[Test]";
            string memo = SanitizeMemo($"{prefix} {appName} v{version} · Unity SDK {AITVersion.Version}");
            return memo.Length > MaxMemoLength ? memo.Substring(0, MaxMemoLength) : memo;
        }

        /// <summary>
        /// memo에서 셸 인용 경계를 깨는 문자를 소스 단계에서 제거한다.
        /// </summary>
        /// <remarks>
        /// memo는 콘솔 배포 이력에 표시되는 정보성 라벨이라 원문 문자 보존이 필요 없다. 반면 명령
        /// 조립 경로는 이스케이프가 중첩된다 — 여기서 만든 문자열이 -m "..."에 들어간 뒤
        /// AITPlatformHelper.CreateProcessStartInfo가 macOS/Linux에서 명령 전체를 bash -l -c "..."로
        /// 한 번 더 이스케이프하고, 그 결과를 .NET이 argv로 파싱하면서 백슬래시 축약 규칙이 다시
        /// 적용된다. 그래서 이스케이프 층을 더 쌓으면 원본에 없던 백슬래시가 최종 memo에 남고,
        /// Windows(-Command 문자열)에서는 큰따옴표가 인자 경계를 깬다.
        /// \ " ` $ 4종은 작은따옴표로 치환하고, 개행 등 제어 문자는 제거한다.
        /// </remarks>
        internal static string SanitizeMemo(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (c == '\\' || c == '"' || c == '$' || c == '`')
                {
                    sb.Append('\'');
                    continue;
                }
                if (char.IsControl(c)) continue;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// memo 문자열을 bash -l -c "..." 로 조립되는 명령의 -m "..." 인자 안에 안전하게
        /// 삽입할 수 있도록 이스케이프한다. 백슬래시·큰따옴표·달러 기호·백틱을 백슬래시로 이스케이프.
        /// <see cref="SanitizeMemo"/>가 이미 4종을 제거하므로 현재 memo 소스(BuildDeployMemo)에서는
        /// 도달하지 않는 심층 방어층이다 — memo 소스가 늘어날 때를 대비해 유지한다.
        /// </summary>
        internal static string EscapeMemoForShell(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;

            var sb = new StringBuilder(value.Length);
            foreach (char c in value)
            {
                if (c == '\\' || c == '"' || c == '$' || c == '`')
                {
                    sb.Append('\\');
                }
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 배포 출력에서 intoss-private:// URL 추출
        /// </summary>
        private static string ExtractDeployUrl(string output)
        {
            if (string.IsNullOrEmpty(output))
            {
                return null;
            }

            // intoss-private://... URL 패턴 찾기
            // (ANSI 코드는 AITPlatformHelper.ExecuteCommand에서 이미 제거됨)
            var match = Regex.Match(output, @"intoss-private://[^\s\│\│]+");
            if (match.Success)
            {
                return match.Value.TrimEnd('│', ' ', '\r', '\n');
            }

            return null;
        }

        /// <summary>
        /// 배포 에러 메시지에서 사용자에게 보여줄 핵심 내용 추출
        /// </summary>
        private static string ExtractDeployErrorMessage(string stdout, string stderr)
        {
            // stderr와 stdout 합치기
            string combined = $"{stdout}\n{stderr}".Trim();

            if (string.IsNullOrEmpty(combined))
            {
                return null;
            }

            // 일반적인 에러 패턴 감지
            var lines = combined.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var errorLines = new List<string>();

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                // (ANSI 코드는 AITPlatformHelper.ExecuteCommand에서 이미 제거됨)

                // 에러 관련 라인 수집
                if (trimmed.StartsWith("Error:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("error:", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("ERR!", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.Contains("ENOENT") ||
                    trimmed.Contains("EACCES") ||
                    trimmed.Contains("401") ||
                    trimmed.Contains("403") ||
                    trimmed.Contains("404") ||
                    trimmed.Contains("500") ||
                    trimmed.Contains("Unauthorized") ||
                    trimmed.Contains("Forbidden") ||
                    trimmed.Contains("Not Found") ||
                    trimmed.Contains("failed") && trimmed.Contains("deploy"))
                {
                    errorLines.Add(trimmed);
                }
            }

            if (errorLines.Count > 0)
            {
                // 최대 3줄까지만 표시
                int maxLines = Math.Min(errorLines.Count, 3);
                return string.Join("\n", errorLines.GetRange(0, maxLines));
            }

            // 에러 패턴을 못 찾았으면 마지막 몇 줄 반환
            if (lines.Length > 0)
            {
                int startIndex = Math.Max(0, lines.Length - 3);
                var lastLines = new List<string>();
                for (int i = startIndex; i < lines.Length; i++)
                {
                    string trimmed = Regex.Replace(lines[i].Trim(), @"\x1B\[[0-9;]*[mGKH]", "");
                    if (!string.IsNullOrEmpty(trimmed))
                    {
                        lastLines.Add(trimmed);
                    }
                }
                if (lastLines.Count > 0)
                {
                    return string.Join("\n", lastLines);
                }
            }

            return null;
        }

        /// <summary>
        /// 빌드 실패 시 사용자에게 에러 다이얼로그를 표시하고 Issue 신고 옵션을 제공
        /// </summary>
        internal static void ShowBuildFailedDialog(AITConvertCore.AITExportError result, string callerName)
        {
            string shortReason = AITConvertCore.GetErrorShortReason(result);
            string cause = AITConvertCore.GetErrorCause(result);
            string title = $"빌드 실패 ({shortReason})";
            string message =
                "앱인토스 미니앱 빌드에 실패했습니다.\n\n" +
                $"{cause}\n\n" +
                "문제를 공유하려면 'Issue 신고'를 눌러주세요.";

            // 자동 에러 전송 — Sentry에 빌드 에러 캡처 + Console에 로그 출력 (이중 캡처 방지 내장)
            AppsInToss.Editor.ErrorTracker.AITEditorErrorTracker.CaptureBuildError(result, $"AIT: 빌드 실패: {result}", callerName);

            int choice = AITPlatformHelper.ShowComplexDialog(
                title,
                message,
                "확인",
                "Issue 신고",
                null,
                defaultChoice: 0
            );
            if (choice == 1)
            {
                AITIssueReportWindow.Open(
                    AITIssueReportContext.BuildFailure,
                    linkedEventId: AITEditorErrorTracker.LastEventId,
                    prefilledTitle: title);
            }
        }
    }
}
