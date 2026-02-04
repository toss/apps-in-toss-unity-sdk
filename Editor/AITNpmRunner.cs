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
            Debug.LogError("[AIT] ========================================");
            Debug.LogError("[AIT] ✗ pnpm을 설치할 수 없습니다.");
            Debug.LogError("[AIT] ========================================");
            Debug.LogError("[AIT] ");
            Debug.LogError("[AIT] 내장 Node.js 다운로드 또는 pnpm 설치에 실패했습니다.");
            Debug.LogError("[AIT] ");
            Debug.LogError("[AIT] 해결 방법:");
            Debug.LogError("[AIT]   1. 네트워크 연결을 확인하세요.");
            Debug.LogError(AITPlatformHelper.IsWindows
                ? "[AIT]   2. %LOCALAPPDATA%\\ait-unity-sdk\\nodejs\\ 폴더를 삭제 후 Unity를 재시작하세요."
                : "[AIT]   2. ~/.ait-unity-sdk/nodejs/ 폴더를 삭제 후 Unity를 재시작하세요.");
            Debug.LogError("[AIT]   3. 방화벽/프록시가 nodejs.org를 차단하고 있는지 확인하세요.");
            Debug.LogError("[AIT] ========================================");
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
                Debug.LogWarning($"[AIT] pnpm 버전 확인 실패: {ex.Message}");
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
                Debug.LogError($"[AIT] pnpm 설치 실패: {result.Error}");
                return false;
            }
            return true;
        }

        /// <summary>
        /// npm 명령 실행 (캐시 사용)
        /// </summary>
        internal static AITConvertCore.AITExportError RunNpmCommandWithCache(
            string workingDirectory,
            string npmPath,
            string arguments,
            string cachePath,
            string progressTitle)
        {
            string npmDir = Path.GetDirectoryName(npmPath);

            // node 실행 파일 경로 찾기 (pnpm이 node를 찾을 수 있도록)
            string nodePath = AITPackageManagerHelper.FindExecutable("node", verbose: false);
            string nodeDir = "";

            if (!string.IsNullOrEmpty(nodePath))
            {
                nodeDir = Path.GetDirectoryName(nodePath);
            }
            else
            {
                // node를 찾지 못한 경우, npmPath가 embedded Node.js인지 확인
                string nodeExeName = AITPlatformHelper.GetExecutableName("node");
                string possibleNodePath = Path.Combine(npmDir, nodeExeName);
                if (File.Exists(possibleNodePath))
                {
                    nodePath = possibleNodePath;
                    nodeDir = npmDir;
                    Debug.Log($"[Package Manager] Embedded node 발견: {nodePath}");
                }
            }

            // 패키지 매니저 이름 추출 (pnpm 또는 npm)
            string pmName = Path.GetFileNameWithoutExtension(npmPath);

            // --store-dir는 install 명령어에만 적용 (run build에는 적용하지 않음)
            // install, install --frozen-lockfile 등 모든 install 명령어에 적용
            bool isInstallCommand = arguments.TrimStart().StartsWith("install");
            string fullArguments = isInstallCommand
                ? $"{arguments} --store-dir \"{cachePath}\""
                : arguments;

            // 추가 PATH 경로 수집
            var additionalPaths = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(npmDir)) additionalPaths.Add(npmDir);
            if (!string.IsNullOrEmpty(nodeDir) && nodeDir != npmDir) additionalPaths.Add(nodeDir);

            Debug.Log($"[{pmName}] 명령 실행 준비:");
            Debug.Log($"[{pmName}]   작업 디렉토리: {workingDirectory}");
            Debug.Log($"[{pmName}]   {pmName} 경로: {npmPath}");
            Debug.Log($"[{pmName}]   node 경로: {nodePath ?? "찾을 수 없음"}");
            Debug.Log($"[{pmName}]   명령: {pmName} {arguments}");
            Debug.Log($"[{pmName}]   캐시 경로: {cachePath}");

            try
            {
                Debug.Log($"[{pmName}] 프로세스 시작...");

                // 크로스 플랫폼 명령 구성
                string command = $"\"{npmPath}\" {fullArguments}";

                // 프로세스 완료를 대기하되, 진행 상황을 업데이트
                int maxWaitSeconds = 300; // 5분

                // EditorUtility.DisplayProgressBar와 함께 명령 실행
                EditorUtility.DisplayProgressBar("Apps in Toss", $"{progressTitle} (시작 중...)", 0);

                var result = AITPlatformHelper.ExecuteCommand(
                    command,
                    workingDirectory,
                    additionalPaths.ToArray(),
                    timeoutMs: maxWaitSeconds * 1000,
                    verbose: true
                );

                EditorUtility.ClearProgressBar();

                if (!result.Success)
                {
                    Debug.LogError($"[{pmName}] 명령 실패 (Exit Code: {result.ExitCode}): {pmName} {arguments}");
                    if (!string.IsNullOrEmpty(result.Output))
                    {
                        Debug.LogError($"[{pmName}] 출력:\n{result.Output}");
                    }
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        Debug.LogError($"[{pmName}] 오류:\n{result.Error}");
                    }
                    return AITConvertCore.AITExportError.BUILD_WEBGL_FAILED;
                }

                Debug.Log($"[{pmName}] ✓ 명령 성공 완료: {pmName} {arguments}");
                return AITConvertCore.AITExportError.SUCCEED;
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                Debug.LogError($"[{pmName}] 명령 실행 오류: {e.Message}");
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
            CancellationToken cancellationToken = default)
        {
            string npmDir = Path.GetDirectoryName(npmPath);

            // node 실행 파일 경로 찾기
            string nodePath = AITPackageManagerHelper.FindExecutable("node", verbose: false);
            string nodeDir = "";

            if (!string.IsNullOrEmpty(nodePath))
            {
                nodeDir = Path.GetDirectoryName(nodePath);
            }
            else
            {
                string nodeExeName = AITPlatformHelper.GetExecutableName("node");
                string possibleNodePath = Path.Combine(npmDir, nodeExeName);
                if (File.Exists(possibleNodePath))
                {
                    nodePath = possibleNodePath;
                    nodeDir = npmDir;
                }
            }

            // 패키지 매니저 이름 추출
            string pmName = Path.GetFileNameWithoutExtension(npmPath);

            // install 명령에만 --store-dir 적용
            bool isInstallCommand = arguments.TrimStart().StartsWith("install");
            string fullArguments = isInstallCommand
                ? $"{arguments} --store-dir \"{cachePath}\""
                : arguments;

            // PATH 경로 수집
            var additionalPaths = new List<string>();
            if (!string.IsNullOrEmpty(npmDir)) additionalPaths.Add(npmDir);
            if (!string.IsNullOrEmpty(nodeDir) && nodeDir != npmDir) additionalPaths.Add(nodeDir);

            Debug.Log($"[{pmName}] 비동기 명령 실행:");
            Debug.Log($"[{pmName}]   명령: {pmName} {arguments}");

            // 전체 명령 구성
            string command = $"\"{npmPath}\" {fullArguments}";

            // 비동기 실행
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
                        Debug.Log($"[{pmName}] ✓ 비동기 명령 성공: {pmName} {arguments}");
                        onComplete?.Invoke(AITConvertCore.AITExportError.SUCCEED);
                    }
                    else
                    {
                        Debug.LogError($"[{pmName}] 비동기 명령 실패 (Exit Code: {result.ExitCode}): {pmName} {arguments}");
                        if (!string.IsNullOrEmpty(result.Error))
                        {
                            Debug.LogError($"[{pmName}] 오류:\n{result.Error}");
                        }
                        onComplete?.Invoke(AITConvertCore.AITExportError.FAIL_NPM_BUILD);
                    }
                },
                onOutputReceived: onOutputReceived,
                timeoutMs: 300000 // 5분
            );

            // 현재 작업 등록 (취소용)
            AITConvertCore.SetCurrentAsyncTask(task);

            return task;
        }
    }
}
