using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
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
    /// 빌드/배포 실행 로직 (Publish, Build &amp; Package, Deploy).
    /// AppsInTossMenu의 [MenuItem] 진입점에서 위임 받아 실행됩니다.
    /// internal 멤버는 Editor/AssemblyInfo.cs 의 InternalsVisibleTo 를 통해 테스트 어셈블리에서 접근됩니다.
    /// </summary>
    internal static class AITDeployManager
    {
        // 재진입 가드 — RunBuildAndPackage / RunPublish 가 await 대기 중일 때 중복 클릭 차단.
        private static bool _buildEntryInProgress;

        // 빌드 소요 시간 측정 (StartServer의 buildStopwatch와 독립)
        private static Stopwatch _buildStopwatch = new Stopwatch();

        // ==================== Publish ====================

        /// <summary>
        /// Publish 메뉴의 실제 실행 로직.
        /// AppsInTossMenu.Publish() [MenuItem] 에서 위임됩니다.
        /// </summary>
        internal static async void RunPublish()
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

                string projectPath = UnityUtil.GetProjectPath();
                string aitBuildPath = Path.Combine(projectPath, "ait-build");
                string distPath = Path.Combine(aitBuildPath, "dist");

                // 기존 빌드가 있는지 확인
                bool hasExistingBuild = Directory.Exists(distPath) &&
                                        Directory.GetFiles(distPath, "*", SearchOption.AllDirectories).Length > 0;

                bool shouldRebuild = true;

                if (hasExistingBuild)
                {
                    // 기존 빌드가 있으면 사용자에게 선택권 부여 (CI에서는 재빌드)
                    int choice = AITPlatformHelper.ShowComplexDialog(
                        "Publish",
                        "기존 빌드가 존재합니다.\n\n" +
                        "코드나 에셋을 변경했다면 다시 빌드하는 것을 권장합니다.",
                        "다시 빌드 후 배포",  // 0: Alt (권장)
                        "취소",               // 1: Cancel
                        "기존 빌드로 배포",   // 2: Other
                        defaultChoice: 0      // CI에서는 재빌드
                    );

                    if (choice == 1)
                    {
                        Debug.Log("AIT: Publish 취소됨");
                        return;
                    }

                    shouldRebuild = (choice == 0);
                }

                if (shouldRebuild)
                {
                    // 클린 빌드 후 배포 (Production 프로필 사용)
                    Debug.Log("AIT: 클린 빌드 & 배포 시작...");
                    _buildStopwatch.Restart();

                    var result = AITConvertCore.DoExport(
                        buildWebGL: true,
                        doPackaging: true,
                        cleanBuild: true,
                        profile: config.productionProfile,
                        profileName: "Publish"
                    );
                    _buildStopwatch.Stop();

                    if (result != AITConvertCore.AITExportError.SUCCEED)
                    {
                        ShowBuildFailedDialog(result, "Publish");
                        return;
                    }

                    Debug.Log($"AIT: 클린 빌드 완료 (소요 시간: {_buildStopwatch.Elapsed.TotalSeconds:F1}초)");
                }
                else
                {
                    Debug.Log("AIT: 기존 빌드를 사용하여 배포합니다.");
                }

                // 배포 실행
                ExecuteDeploy();
            }
            catch (Exception e)
            {
                // async void 의 미처리 예외는 SynchronizationContext 로 터져 Editor 전체에
                // 영향을 주므로 여기서 삼키고 사용자에게 다이얼로그로 알린다.
                AITLog.Error($"AIT: Publish 중 예외: {e.Message}", sentryCapture: true);
                AITPlatformHelper.ShowInfoDialog("오류", $"배포 중 오류가 발생했습니다.\n\n{e.Message}", "확인");
            }
            finally
            {
                _buildEntryInProgress = false;
            }
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
        private static void ExecuteDeploy()
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

            bool confirmed = AITPlatformHelper.ShowConfirmDialog(
                "배포 확인",
                $"Apps in Toss에 배포하시겠습니까?\n\n프로젝트: {config.appName}\n버전: {config.version}",
                "배포",
                "취소",
                autoApprove: true
            );

            if (!confirmed) return;

            Debug.Log("AIT: Apps in Toss 배포 시작...");

            try
            {
                string npmDir = Path.GetDirectoryName(npmPath);

                // pnpm run deploy를 사용하여 로컬 node_modules/.bin/ait 사용
                string pnpmName = AITPlatformHelper.IsWindows ? "pnpm.cmd" : "pnpm";
                string pnpmPath = Path.Combine(npmDir, pnpmName);

                // pnpm exec ait deploy --api-key "KEY" 형태로 직접 실행
                string command = $"\"{pnpmPath}\" exec ait deploy --api-key \"{deploymentKey}\"";
                var result = AITPlatformHelper.ExecuteCommand(
                    command,
                    buildPath,
                    new[] { npmDir },
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
                            DeploySuccessWindow.Show(deployUrl);
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
