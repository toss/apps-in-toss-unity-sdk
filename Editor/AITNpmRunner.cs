using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// npm/pnpm 실행 관련 유틸리티
    /// </summary>
    internal static class AITNpmRunner
    {
        /// <summary>
        /// 패키지 매니저 경로 찾기
        /// </summary>
        internal static string FindNpmPath()
        {
            // AITPackageManagerHelper를 사용한 통합 패키지 매니저 검색
            string buildPath = AITPackageManagerHelper.GetBuildPath();
            return AITPackageManagerHelper.FindPackageManager(buildPath, verbose: true);
        }

        /// <summary>
        /// pnpm 경로를 찾는 함수 (내장 Node.js 사용 시 자동 설치 포함)
        /// </summary>
        internal static string FindPnpmPath()
        {
            string requiredVersion = AITPackageManagerHelper.PNPM_VERSION;

            // 1. 내장 Node.js bin 디렉토리에서 pnpm 찾기
            string embeddedNodeBinPath = AITPackageManagerHelper.GetEmbeddedNodeBinPath();
            if (!string.IsNullOrEmpty(embeddedNodeBinPath))
            {
                string pnpmInEmbedded = Path.Combine(embeddedNodeBinPath, AITPlatformHelper.GetExecutableName("pnpm"));
                string npmPath = Path.Combine(embeddedNodeBinPath, AITPlatformHelper.GetExecutableName("npm"));

                if (File.Exists(pnpmInEmbedded))
                {
                    // 버전 확인
                    string installedVersion = GetPnpmVersion(pnpmInEmbedded, embeddedNodeBinPath);
                    if (!string.IsNullOrEmpty(installedVersion))
                    {
                        if (installedVersion == requiredVersion)
                        {
                            Debug.Log($"[AIT] ✓ 내장 pnpm v{installedVersion} 발견 (요구 버전과 일치)");
                            return pnpmInEmbedded;
                        }
                        else
                        {
                            Debug.Log($"[AIT] 내장 pnpm v{installedVersion} 발견 (요구 버전: v{requiredVersion})");
                            Debug.Log($"[AIT] pnpm을 v{requiredVersion}으로 업데이트합니다...");

                            // 버전이 다르면 재설치
                            if (File.Exists(npmPath) && InstallPnpmWithNpm(npmPath, embeddedNodeBinPath, requiredVersion))
                            {
                                Debug.Log($"[AIT] ✓ pnpm v{requiredVersion} 설치 완료");
                                return pnpmInEmbedded;
                            }
                        }
                    }
                    else
                    {
                        // 버전 확인 실패 시 그냥 사용
                        Debug.Log($"[AIT] ✓ 내장 pnpm 발견 (버전 확인 불가): {pnpmInEmbedded}");
                        return pnpmInEmbedded;
                    }
                }

                // 2. pnpm이 없으면 npm으로 글로벌 설치
                Debug.Log($"[AIT] 내장 pnpm이 없습니다. npm install -g pnpm@{requiredVersion} 실행 중...");

                if (File.Exists(npmPath))
                {
                    if (InstallPnpmWithNpm(npmPath, embeddedNodeBinPath, requiredVersion))
                    {
                        Debug.Log("[AIT] ✓ pnpm 글로벌 설치 완료");

                        // 설치 후 pnpm 경로 다시 확인
                        if (File.Exists(pnpmInEmbedded))
                        {
                            return pnpmInEmbedded;
                        }
                    }
                }
            }

            // 3. 내장 Node.js/pnpm 설치 모두 실패 - 상세 에러 메시지
            AITPackageManagerHelper.LogInstallationFailure("AIT");
            return null;
        }

        /// <summary>
        /// pnpm 버전 확인
        /// </summary>
        internal static string GetPnpmVersion(string pnpmPath, string workingDir)
        {
            try
            {
                // pnpmPath의 디렉토리를 additionalPaths로 전달 (workingDir이 아닌 실행파일 디렉토리)
                string pnpmDir = Path.GetDirectoryName(pnpmPath);
                var result = AITPlatformHelper.ExecuteCommand(
                    $"\"{pnpmPath}\" --version",
                    workingDir,
                    new[] { pnpmDir },
                    timeoutMs: 10000,
                    verbose: false
                );

                if (result.Success && !string.IsNullOrEmpty(result.Output))
                {
                    return result.Output.Trim();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIT] pnpm 버전 확인 실패: {ex}");
            }
            return null;
        }

        /// <summary>
        /// npm을 사용해 pnpm 설치
        /// </summary>
        internal static bool InstallPnpmWithNpm(string npmPath, string workingDir, string version)
        {
            // npmPath의 디렉토리를 additionalPaths로 전달 (workingDir이 아닌 실행파일 디렉토리)
            string npmDir = Path.GetDirectoryName(npmPath);
            string command = $"\"{npmPath}\" install -g pnpm@{version}";
            var result = AITPlatformHelper.ExecuteCommand(
                command,
                workingDir,
                new[] { npmDir },
                timeoutMs: 120000, // 2분
                verbose: true
            );

            if (!result.Success)
            {
                // 사용자 환경(공백 미인용 PATH, PowerShell 정책, 네트워크 차단 등)에서 발생하는
                // npm 호출 실패 — 콘솔에는 진단 메시지를 남기되 Sentry로 전송하지 않는다.
                // result.Error에 전체 명령줄/PowerShell 호출 본문이 들어가 fingerprint가 폭주하던
                // 케이스(Sentry SDK-TQ/TM/TR 등) 방지.
                AITLog.Error($"[AIT] pnpm 설치 실패: {result.Error}", sentryCapture: false);
                return false;
            }
            return true;
        }

        /// <summary>
        /// pnpm 실행에 필요한 추가 PATH 경로 목록 구성
        /// (node_modules/.bin + npmPath 디렉토리 + 내장 node 실행 파일 디렉토리)
        /// </summary>
        internal static List<string> BuildAdditionalPaths(string npmPath, string workingDirectory = null)
        {
            var paths = new List<string>();

            // node_modules/.bin을 최우선으로 추가 (pnpm exec로 CLI 직접 호출 시 필요)
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                string nodeModulesBin = Path.Combine(workingDirectory, "node_modules", ".bin");
                if (Directory.Exists(nodeModulesBin))
                {
                    paths.Add(nodeModulesBin);
                }
            }

            string npmDir = Path.GetDirectoryName(npmPath);
            if (!string.IsNullOrEmpty(npmDir)) paths.Add(npmDir);

            string embeddedBinPath = AITPackageManagerHelper.GetEmbeddedNodeBinPath();
            if (!string.IsNullOrEmpty(embeddedBinPath) && embeddedBinPath != npmDir)
            {
                paths.Add(embeddedBinPath);
            }

            return paths;
        }

        /// <summary>
        /// install 명령어에 --store-dir 적용한 최종 arguments 구성
        /// </summary>
        internal static string BuildFullArguments(string arguments, string cachePath)
        {
            bool isInstallCommand = arguments.TrimStart().StartsWith("install");
            return isInstallCommand
                ? $"{arguments} --store-dir \"{cachePath}\""
                : arguments;
        }

        /// <summary>
        /// npm 명령 실행 (캐시 사용, 취소 가능한 프로그레스바)
        /// process.WaitForExit() 대신 HasExited 폴링으로 UI 응답성 유지
        /// </summary>
        internal static AITConvertCore.AITExportError RunNpmCommandWithCache(
            string workingDirectory,
            string npmPath,
            string arguments,
            string cachePath,
            string progressTitle,
            Dictionary<string, string> additionalEnvVars = null)
        {
            string pmName = Path.GetFileNameWithoutExtension(npmPath);
            string fullArguments = BuildFullArguments(arguments, cachePath);
            var additionalPaths = BuildAdditionalPaths(npmPath, workingDirectory);

            Debug.Log($"[{pmName}] 명령 실행 준비:");
            Debug.Log($"[{pmName}]   작업 디렉토리: {workingDirectory}");
            Debug.Log($"[{pmName}]   {pmName} 경로: {npmPath}");
            Debug.Log($"[{pmName}]   명령: {pmName} {arguments}");
            Debug.Log($"[{pmName}]   캐시 경로: {cachePath}");

            try
            {
                Debug.Log($"[{pmName}] 프로세스 시작...");

                string command = $"\"{npmPath}\" {fullArguments}";
                int maxWaitMs = 300000; // 5분

                var processInfo = AITPlatformHelper.CreateProcessStartInfo(
                    command, workingDirectory, additionalPaths.ToArray(), additionalEnvVars);

                Debug.Log($"[Platform] 명령 실행: {command}");
                Debug.Log($"[Platform] 셸: {processInfo.FileName} {processInfo.Arguments}");
                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    Debug.Log($"[Platform] 작업 디렉토리: {workingDirectory}");
                }

                using (var process = new System.Diagnostics.Process { StartInfo = processInfo })
                {
                    var outputBuilder = new System.Text.StringBuilder();
                    var errorBuilder = new System.Text.StringBuilder();
                    // OutputDataReceived 콜백에서 쓰이고 메인 스레드에서 읽힘
                    // string 참조 대입은 atomic이므로 프로그레스바 표시 용도로 안전
                    string lastOutputLine = "";

                    // 비동기 출력 수신 (실시간 로그 + 프로그레스바 표시용)
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            string line = AITPlatformHelper.StripAnsiCodes(e.Data);
                            outputBuilder.AppendLine(line);
                            lastOutputLine = line;
                        }
                    };
                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (e.Data != null)
                        {
                            string line = AITPlatformHelper.StripAnsiCodes(e.Data);
                            errorBuilder.AppendLine(line);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // HasExited 폴링 — UI 응답성 유지 + Cancel 가능
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    bool cancelled = false;

                    while (!process.HasExited)
                    {
                        // 타임아웃 체크
                        if (stopwatch.ElapsedMilliseconds > maxWaitMs)
                        {
                            process.Kill();
                            process.WaitForExit(5000); // 출력 버퍼 플러시 (최대 5초)
                            EditorUtility.ClearProgressBar();
                            string timeoutOutput = outputBuilder.ToString();
                            string timeoutError = errorBuilder.ToString();
                            // npm/pnpm 타임아웃 — lockfile drift, 네트워크 지연, AV scan, registry
                            // 정책 등 사용자 환경 원인. 호출자가 FAIL_NPM_BUILD로 결과 인지 후
                            // 상위에서 컨텍스트 있는 메시지로 보고하므로 Console 진단만 남기고
                            // Sentry는 차단 (exit != 0 동기 실패 분기와 동일 정책 — SDK-HA/R6/VF/VA cascade 흡수).
                            AITLog.Error($"[{pmName}] 명령 시간 초과 ({maxWaitMs / 1000}초): {pmName} {arguments}", sentryCapture: false);
                            if (!string.IsNullOrEmpty(timeoutOutput))
                                AITLog.Error($"[{pmName}] 출력:\n{timeoutOutput.Trim()}", sentryCapture: false);
                            if (!string.IsNullOrEmpty(timeoutError))
                                AITLog.Error($"[{pmName}] 오류:\n{timeoutError.Trim()}", sentryCapture: false);
                            // 상위 단일 빌드 실패 캡처(CaptureBuildError)에 exit/stderr 진단을 실어줄 수 있도록 기록 (§5).
                            AITBuildDiagnostics.RecordFailure(
                                $"{pmName} {arguments} (timeout {maxWaitMs / 1000}s)", -1, timeoutError, timeoutOutput);
                            return AITConvertCore.AITExportError.FAIL_NPM_BUILD;
                        }

                        // 취소 가능한 프로그레스바 표시
                        float elapsed = (float)stopwatch.ElapsedMilliseconds / maxWaitMs;
                        string statusText = string.IsNullOrEmpty(lastOutputLine)
                            ? $"{progressTitle}"
                            : $"{progressTitle}\n{lastOutputLine}";
                        cancelled = EditorUtility.DisplayCancelableProgressBar("Apps in Toss", statusText, elapsed);

                        if (cancelled)
                        {
                            Debug.Log($"[{pmName}] 사용자가 취소했습니다: {pmName} {arguments}");
                            process.Kill();
                            process.WaitForExit(5000); // 프로세스 종료 대기 (최대 5초)
                            break;
                        }

                        // 짧은 대기 (UI 응답성 유지)
                        Thread.Sleep(200);
                    }

                    EditorUtility.ClearProgressBar();

                    if (cancelled)
                    {
                        return AITConvertCore.AITExportError.CANCELLED;
                    }

                    // 출력 버퍼 플러시 대기 (프로세스는 이미 종료됨)
                    process.WaitForExit(5000);

                    string output = AITPlatformHelper.StripAnsiCodes(outputBuilder.ToString());
                    string error = AITPlatformHelper.StripAnsiCodes(errorBuilder.ToString());
                    bool success = process.ExitCode == 0;

                    if (success)
                    {
                        // 명령이 성공하면 직전 폴백 단계의 실패 진단을 비운다 (복구된 단계 stderr 오첨부 방지, §5).
                        AITBuildDiagnostics.ClearOnSuccess();
                        Debug.Log($"[Platform] ✓ 명령 성공 (Exit Code: {process.ExitCode})");
                        if (!string.IsNullOrEmpty(output))
                        {
                            Debug.Log($"[Platform] 출력:\n{output.Trim()}");
                        }
                    }
                    else
                    {
                        // npm/pnpm exit != 0 — lockfile drift, 네트워크, AV scan, registry 정책 등
                        // 사용자 환경 원인. 호출자는 FAIL_NPM_BUILD로 결과 인지 후 상위에서
                        // 컨텍스트 있는 메시지로 보고하므로 여기서는 Console 진단만 남기고
                        // Sentry는 차단 (SDK-HA/HB/R6/PM cascade 흡수).
                        AITLog.Error($"[{pmName}] 명령 실패 (Exit Code: {process.ExitCode}): {pmName} {arguments}", sentryCapture: false);
                        if (!string.IsNullOrEmpty(output))
                        {
                            AITLog.Error($"[{pmName}] 출력:\n{output}", sentryCapture: false);
                        }
                        if (!string.IsNullOrEmpty(error))
                        {
                            AITLog.Error($"[{pmName}] 오류:\n{error}", sentryCapture: false);
                        }
                        // 상위 단일 빌드 실패 캡처(CaptureBuildError)에 exit/stderr 진단을 실어줄 수 있도록 기록 (§5).
                        AITBuildDiagnostics.RecordFailure(
                            $"{pmName} {arguments}", process.ExitCode, error, output);
                        return AITConvertCore.AITExportError.FAIL_NPM_BUILD;
                    }
                }

                Debug.Log($"[{pmName}] ✓ 명령 성공 완료: {pmName} {arguments}");
                return AITConvertCore.AITExportError.SUCCEED;
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                // 동기 npm 실행 예외 — 호출자가 NODE_NOT_FOUND로 인지 후 상위 가이드 출력.
                // 예외 본문에 명령줄/path가 들어가 fingerprint가 폭주하므로 Sentry 차단.
                AITLog.Error($"[{pmName}] 명령 실행 오류: {e}", sentryCapture: false);
                // 상위 캡처(NODE_NOT_FOUND)에 예외 요지를 진단으로 실어줄 수 있도록 기록 (§5).
                AITBuildDiagnostics.RecordFailure($"{pmName} {arguments} (exception)", -1, e.Message);
                return AITConvertCore.AITExportError.NODE_NOT_FOUND;
            }
        }

        /// <summary>
        /// npm 명령 비동기 실행 (non-blocking)
        /// </summary>
        /// <param name="workingDirectory">작업 디렉토리</param>
        /// <param name="npmPath">npm/pnpm 실행 파일 경로</param>
        /// <param name="arguments">명령 인자</param>
        /// <param name="cachePath">캐시 경로</param>
        /// <param name="onComplete">완료 콜백</param>
        /// <param name="onOutputReceived">출력 수신 콜백 (선택)</param>
        /// <param name="cancellationToken">취소 토큰 (선택)</param>
        /// <returns>비동기 명령 작업</returns>
        internal static AITAsyncCommandRunner.CommandTask RunNpmCommandWithCacheAsync(
            string workingDirectory,
            string npmPath,
            string arguments,
            string cachePath,
            Action<AITConvertCore.AITExportError> onComplete,
            Action<string> onOutputReceived = null,
            CancellationToken cancellationToken = default,
            Dictionary<string, string> additionalEnvVars = null)
        {
            string pmName = Path.GetFileNameWithoutExtension(npmPath);
            string fullArguments = BuildFullArguments(arguments, cachePath);
            var additionalPaths = BuildAdditionalPaths(npmPath, workingDirectory);

            Debug.Log($"[{pmName}] 비동기 명령 실행:");
            Debug.Log($"[{pmName}]   명령: {pmName} {arguments}");

            string command = $"\"{npmPath}\" {fullArguments}";

            var task = AITAsyncCommandRunner.RunAsync(
                command: command,
                workingDirectory: workingDirectory,
                additionalPaths: additionalPaths.ToArray(),
                onComplete: (result) =>
                {
                    // 취소된 경우
                    if (result.ExitCode == -1 && AITConvertCore.IsCancelled())
                    {
                        Debug.Log($"[{pmName}] 명령이 취소되었습니다: {pmName} {arguments}");
                        onComplete?.Invoke(AITConvertCore.AITExportError.CANCELLED);
                        return;
                    }

                    if (result.Success)
                    {
                        // 성공 시 직전 폴백 단계 실패 진단을 비운다 (§5).
                        AITBuildDiagnostics.ClearOnSuccess();
                        Debug.Log($"[{pmName}] ✓ 비동기 명령 성공: {pmName} {arguments}");
                        onComplete?.Invoke(AITConvertCore.AITExportError.SUCCEED);
                    }
                    else
                    {
                        // 비동기 변형 — 동기 경로와 동일하게 사용자 환경 원인이며
                        // onComplete가 상위로 결과 전파. Sentry는 차단.
                        AITLog.Warning($"[{pmName}] 비동기 명령 실패 (Exit Code: {result.ExitCode}): {pmName} {arguments}", sentryCapture: false);
                        if (!string.IsNullOrEmpty(result.Error))
                        {
                            AITLog.Warning($"[{pmName}] 오류:\n{result.Error}", sentryCapture: false);
                        }
                        // 상위 단일 빌드 실패 캡처에 exit/stderr 진단을 실어줄 수 있도록 기록 (§5).
                        AITBuildDiagnostics.RecordFailure(
                            $"{pmName} {arguments} (async)", result.ExitCode, result.Error, result.Output);
                        onComplete?.Invoke(AITConvertCore.AITExportError.FAIL_NPM_BUILD);
                    }
                },
                onOutputReceived: onOutputReceived,
                timeoutMs: 300000, // 5분
                additionalEnvVars: additionalEnvVars
            );

            // 현재 작업 등록 (취소용)
            AITConvertCore.SetCurrentAsyncTask(task);

            return task;
        }
    }
}
