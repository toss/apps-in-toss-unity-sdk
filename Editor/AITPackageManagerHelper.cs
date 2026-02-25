using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 패키지 매니저 및 Node.js 검색/설치 통합 유틸리티
    /// 모든 npm/pnpm/node 관련 로직을 중앙화하여 관리
    /// 크로스 플랫폼 지원 (Windows, macOS, Linux)
    /// </summary>
    public static class AITPackageManagerHelper
    {
        /// <summary>
        /// pnpm 버전 (내장 Node.js에 설치할 버전)
        /// 최신 버전 강제 방지를 위해 고정
        /// </summary>
        public const string PNPM_VERSION = "10.28.0";

        /// <summary>
        /// 표준 실행 파일 경로 (플랫폼별)
        /// </summary>
        private static string[] StandardPaths => AITPlatformHelper.StandardBinPaths;

        // 마지막으로 탐지한 내장 Node.js 경로 (ExecutePackageManagerCommand에서 PATH 설정용)
        private static string _embeddedNodeBinPath = null;

        /// <summary>
        /// 내장 Node.js bin 경로 가져오기 (PATH 환경변수 설정용)
        /// </summary>
        public static string GetEmbeddedNodeBinPath()
        {
            return _embeddedNodeBinPath;
        }

        /// <summary>
        /// 패키지 매니저 찾기 (환경 통제를 위해 무조건 내장 Node.js + pnpm 사용)
        /// 1. 내장 Node.js 다운로드 → 2. pnpm 설치 확인/자동설치 → 3. pnpm 경로 반환
        /// </summary>
        /// <param name="buildPath">빌드 디렉토리 경로 (로컬 pnpm 확인용)</param>
        /// <param name="verbose">상세 로그 출력 여부</param>
        /// <returns>pnpm 실행 파일 경로</returns>
        public static string FindPackageManager(string buildPath = null, bool verbose = true)
        {
            // 백그라운드 설치 진행 중이면 대기
            if (AITPackageInitializer.IsInstalling)
            {
                if (verbose) Debug.Log("[Package Manager] 백그라운드 설치 감지. 대기합니다...");
                AITPackageInitializer.WaitForInstallation();
            }

            if (verbose) Debug.Log("[Package Manager] === 패키지 매니저 탐지 시작 (내장 Node.js + pnpm 강제 사용) ===");

            // 1. 내장 Node.js 다운로드 (환경 통제)
            if (verbose) Debug.Log("[Package Manager] [1/2] 내장 Node.js 다운로드...");
            string embeddedNpm = AITNodeJSDownloader.FindEmbeddedNpm(autoDownload: true);

            if (string.IsNullOrEmpty(embeddedNpm))
            {
                LogInstallationFailure("Package Manager");
                return null;
            }

            // npm 경로에서 node bin 디렉토리 추출
            string embeddedNodeDir = System.IO.Path.GetDirectoryName(embeddedNpm);
            // Windows에서는 node.exe, macOS/Linux에서는 node
            string nodeExe = AITPlatformHelper.IsWindows ? "node.exe" : "node";
            string nodePath = System.IO.Path.Combine(embeddedNodeDir, nodeExe);

            if (!File.Exists(nodePath))
            {
                Debug.LogError($"[Package Manager] ✗ 내장 Node.js 실행 파일 없음: {nodePath}");
                return null;
            }

            // 내장 Node.js bin 경로 저장 (ExecutePackageManagerCommand에서 PATH 설정용)
            _embeddedNodeBinPath = embeddedNodeDir;

            if (verbose) Debug.Log($"[Package Manager] ✓ 내장 Node.js: {nodePath}");
            if (verbose) Debug.Log($"[Package Manager] ✓ 내장 npm: {embeddedNpm}");

            // 2. pnpm 확인 및 설치
            if (verbose) Debug.Log("[Package Manager] [2/2] pnpm 확인...");

            // pnpm 경로 (내장 Node.js bin 디렉토리 내)
            // Windows에서는 pnpm.cmd, macOS/Linux에서는 pnpm
            string pnpmExe = AITPlatformHelper.IsWindows ? "pnpm.cmd" : "pnpm";
            string pnpmPath = System.IO.Path.Combine(embeddedNodeDir, pnpmExe);

            if (File.Exists(pnpmPath))
            {
                if (verbose) Debug.Log($"[Package Manager] ✓ pnpm 발견: {pnpmPath}");
                if (verbose) Debug.Log("[Package Manager] === 최종 선택: pnpm (내장) ===");
                return pnpmPath;
            }

            // pnpm 미설치: npm을 반환 (RunPackageCommand에서 pnpm 자동 설치)
            if (verbose) Debug.Log("[Package Manager] pnpm 미설치. npm을 통해 자동 설치 예정...");
            if (verbose) Debug.Log("[Package Manager] === 임시 반환: npm (pnpm 자동 설치 후 pnpm 사용) ===");
            return embeddedNpm;
        }

        /// <summary>
        /// 실행 파일 경로 찾기 (크로스 플랫폼)
        /// </summary>
        /// <param name="executableName">실행 파일 이름 (예: "pnpm", "npm", "node")</param>
        /// <param name="verbose">상세 로그 출력 여부</param>
        /// <returns>실행 파일 절대 경로 또는 null</returns>
        public static string FindExecutable(string executableName, bool verbose = true)
        {
            // AITPlatformHelper에 위임 (크로스 플랫폼 지원)
            return AITPlatformHelper.FindExecutable(executableName, null, verbose);
        }

        /// <summary>
        /// 플랫폼별 Node.js 설치 경로 문자열 반환
        /// </summary>
        public static string GetNodejsPathDescription()
        {
            return AITPlatformHelper.IsWindows
                ? "%LOCALAPPDATA%\\ait-unity-sdk\\nodejs\\"
                : "~/.ait-unity-sdk/nodejs/";
        }

        /// <summary>
        /// pnpm/Node.js 설치 실패 에러를 로그에 출력
        /// </summary>
        public static void LogInstallationFailure(string tag)
        {
            string nodejsPath = GetNodejsPathDescription();
            Debug.LogError($"[{tag}] ========================================");
            Debug.LogError($"[{tag}] ✗ pnpm을 설치할 수 없습니다.");
            Debug.LogError($"[{tag}] ========================================");
            Debug.LogError($"[{tag}] ");
            Debug.LogError($"[{tag}] 내장 Node.js 다운로드 또는 pnpm 설치에 실패했습니다.");
            Debug.LogError($"[{tag}] ");
            Debug.LogError($"[{tag}] 해결 방법:");
            Debug.LogError($"[{tag}]   1. 네트워크 연결을 확인하세요.");
            Debug.LogError($"[{tag}]   2. {nodejsPath} 폴더를 삭제 후 Unity를 재시작하세요.");
            Debug.LogError($"[{tag}]   3. 방화벽/프록시가 nodejs.org를 차단하고 있는지 확인하세요.");
            Debug.LogError($"[{tag}] ========================================");
        }

        /// <summary>
        /// pnpm 설치 실패 다이얼로그 표시
        /// </summary>
        public static void ShowInstallationFailureDialog()
        {
            string nodejsPath = GetNodejsPathDescription();
            AITPlatformHelper.ShowInfoDialog(
                "빌드 실패",
                "pnpm을 찾을 수 없습니다.\n\n" +
                "해결 방법:\n" +
                "1. 네트워크 연결을 확인하세요.\n" +
                $"2. {nodejsPath} 폴더를 삭제 후 Unity를 재시작하세요.\n" +
                "3. 방화벽/프록시가 nodejs.org를 차단하고 있는지 확인하세요.",
                "확인");
        }

        /// <summary>
        /// ait-build 경로 가져오기
        /// </summary>
        /// <returns>ait-build 디렉토리 절대 경로</returns>
        public static string GetBuildPath()
        {
            string projectPath = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectPath, "ait-build");
        }

        /// <summary>
        /// 통합 패키지 매니저 명령 실행 (node/pnpm/npm/node_modules 자동 체크 및 설치)
        ///
        /// 내부 동작:
        /// 1. node 존재 확인 → 없으면 embedded Node.js 설치
        /// 2. pnpm/npm 경로 찾기
        /// 3. node_modules 확인 → 없으면 자동 install
        /// 4. 명령 실행 (예: "install", "run build", "run deploy")
        ///
        /// 사용 예:
        /// RunPackageCommand("install") → pnpm install 또는 npm install
        /// RunPackageCommand("run build") → pnpm run build 또는 npm run build
        /// </summary>
        /// <param name="command">실행할 명령 (예: "install", "run build")</param>
        /// <param name="buildPath">빌드 디렉토리 경로 (null이면 자동으로 ait-build 사용)</param>
        /// <param name="packageJsonTemplatePath">package.json 템플릿 경로 (node_modules 없을 때 복사)</param>
        /// <param name="async">비동기 실행 여부</param>
        /// <param name="verbose">상세 로그 출력 여부</param>
        /// <param name="showProgressBar">Progress bar 표시 여부 (동기 모드에서만)</param>
        /// <returns>성공 여부 (비동기 모드는 항상 true)</returns>
        public static bool RunPackageCommand(
            string command,
            string buildPath = null,
            string packageJsonTemplatePath = null,
            bool async = false,
            bool verbose = true,
            bool showProgressBar = false)
        {
            try
            {
                // buildPath 기본값
                if (string.IsNullOrEmpty(buildPath))
                {
                    buildPath = GetBuildPath();
                }

                if (verbose) Debug.Log($"[Package Manager] 명령 실행 준비: {command}");

                // 1. 패키지 매니저 찾기 (pnpm 우선, npm fallback)
                string packageManagerPath = FindPackageManager(buildPath, verbose);
                if (string.IsNullOrEmpty(packageManagerPath))
                {
                    Debug.LogError("[Package Manager] npm 또는 pnpm을 찾을 수 없습니다.");
                    return false;
                }

                string pmName = Path.GetFileName(packageManagerPath);
                if (verbose) Debug.Log($"[Package Manager] 사용할 패키지 매니저: {pmName} ({packageManagerPath})");

                // 2. pnpm 확인 및 자동 설치 (npm인 경우 pnpm을 글로벌 설치)
                if (pmName == "npm")
                {
                    if (verbose) Debug.Log("[Package Manager] npm 감지. pnpm을 글로벌 설치합니다...");

                    // 내장 Node.js bin 디렉토리에 pnpm 글로벌 설치
                    string npmDir = Path.GetDirectoryName(packageManagerPath);
                    string pnpmGlobalPath = Path.Combine(npmDir, "pnpm");

                    if (!File.Exists(pnpmGlobalPath))
                    {
                        if (verbose) Debug.Log($"[Package Manager] pnpm이 없습니다. npm install -g pnpm@{PNPM_VERSION} 실행...");

                        bool pnpmInstallSuccess = AITNpmRunner.InstallPnpmWithNpm(packageManagerPath, npmDir, PNPM_VERSION);

                        if (!pnpmInstallSuccess)
                        {
                            Debug.LogError("[Package Manager] pnpm 글로벌 설치 실패");
                            return false;
                        }

                        if (verbose) Debug.Log("[Package Manager] ✓ pnpm 글로벌 설치 완료");

                        // pnpm으로 전환
                        if (File.Exists(pnpmGlobalPath))
                        {
                            packageManagerPath = pnpmGlobalPath;
                            pmName = "pnpm";
                            if (verbose) Debug.Log($"[Package Manager] ✓ pnpm으로 전환: {pnpmGlobalPath}");
                        }
                        else
                        {
                            Debug.LogError($"[Package Manager] pnpm 설치 후에도 파일이 없습니다: {pnpmGlobalPath}");
                            return false;
                        }
                    }
                    else
                    {
                        // pnpm이 이미 있으면 전환
                        packageManagerPath = pnpmGlobalPath;
                        pmName = "pnpm";
                        if (verbose) Debug.Log($"[Package Manager] ✓ 기존 pnpm 사용: {pnpmGlobalPath}");
                    }
                }

                // 3. node_modules 확인 및 자동 설치
                string nodeModulesPath = Path.Combine(buildPath, "node_modules");
                if (!Directory.Exists(nodeModulesPath))
                {
                    if (verbose) Debug.Log($"[Package Manager] node_modules가 없습니다. 먼저 dependencies를 설치합니다...");

                    // package.json 및 pnpm-lock.yaml 복사 (제공된 경우)
                    if (!string.IsNullOrEmpty(packageJsonTemplatePath) && File.Exists(packageJsonTemplatePath))
                    {
                        if (!Directory.Exists(buildPath))
                        {
                            Directory.CreateDirectory(buildPath);
                        }

                        string packageJsonDest = Path.Combine(buildPath, "package.json");
                        File.Copy(packageJsonTemplatePath, packageJsonDest, true);
                        if (verbose) Debug.Log($"[Package Manager] package.json 복사: {packageJsonTemplatePath} → {packageJsonDest}");

                        // pnpm-lock.yaml도 복사 (빠른 설치를 위해)
                        string lockfilePath = Path.Combine(Path.GetDirectoryName(packageJsonTemplatePath), "pnpm-lock.yaml");
                        if (File.Exists(lockfilePath))
                        {
                            string lockfileDest = Path.Combine(buildPath, "pnpm-lock.yaml");
                            File.Copy(lockfilePath, lockfileDest, true);
                            if (verbose) Debug.Log($"[Package Manager] pnpm-lock.yaml 복사: {lockfilePath} → {lockfileDest}");
                        }
                    }

                    // install 먼저 실행 (동기 모드로 강제, lockfile이 있으면 --frozen-lockfile 사용)
                    string lockfileInBuild = Path.Combine(buildPath, "pnpm-lock.yaml");
                    string installCommand = File.Exists(lockfileInBuild) ? "install --frozen-lockfile" : "install";
                    bool installSuccess = ExecutePackageManagerCommand(
                        packageManagerPath,
                        installCommand,
                        buildPath,
                        async: false,  // install은 항상 동기로
                        verbose: verbose,
                        showProgressBar: showProgressBar
                    );

                    if (!installSuccess)
                    {
                        Debug.LogError("[Package Manager] dependencies 설치 실패. 명령을 실행할 수 없습니다.");
                        return false;
                    }

                    if (verbose) Debug.Log("[Package Manager] ✓ dependencies 설치 완료");
                }

                // 4. 실제 명령 실행 (install이 아닌 경우에만)
                if (command != "install")
                {
                    return ExecutePackageManagerCommand(
                        packageManagerPath,
                        command,
                        buildPath,
                        async: async,
                        verbose: verbose,
                        showProgressBar: showProgressBar
                    );
                }

                return true;
            }
            catch (Exception e)
            {
                if (verbose) Debug.LogError($"[Package Manager] 명령 실행 중 예외 발생: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 패키지 매니저 명령 실제 실행 (내부 헬퍼 - 크로스 플랫폼)
        /// </summary>
        private static bool ExecutePackageManagerCommand(
            string packageManagerPath,
            string command,
            string buildPath,
            bool async = false,
            bool verbose = true,
            bool showProgressBar = false)
        {
            try
            {
                // buildPath 디렉토리 생성
                if (!Directory.Exists(buildPath))
                {
                    Directory.CreateDirectory(buildPath);
                }

                // 패키지 매니저 종류 확인
                string pmName = Path.GetFileNameWithoutExtension(packageManagerPath);
                string pmDir = Path.GetDirectoryName(packageManagerPath);

                // 추가 PATH 경로 수집
                var additionalPaths = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(_embeddedNodeBinPath))
                {
                    additionalPaths.Add(_embeddedNodeBinPath);
                }
                if (!string.IsNullOrEmpty(pmDir))
                {
                    additionalPaths.Add(pmDir);
                }

                if (verbose) Debug.Log($"[{pmName}] {(async ? "백그라운드" : "동기")} 명령 실행: {command}");

                // Progress bar 표시 (동기 모드에서만)
                if (showProgressBar && !async)
                {
                    EditorUtility.DisplayProgressBar(
                        $"{pmName} {command}",
                        $"{pmName} {command} 실행 중...",
                        0.5f
                    );
                }

                // 크로스 플랫폼 명령 구성
                string fullCommand = $"\"{packageManagerPath}\" {command}";
                if (async)
                {
                    fullCommand += " --loglevel=error";
                }

                if (async)
                {
                    // 비동기 모드: 백그라운드에서 실행
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        var result = AITPlatformHelper.ExecuteCommand(
                            fullCommand,
                            buildPath,
                            additionalPaths.ToArray(),
                            verbose: verbose
                        );

                        if (result.Success)
                        {
                            UnityEngine.Debug.Log($"[{pmName}] ✓ 백그라운드 {command} 완료");
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning($"[{pmName}] 백그라운드 {command} 실패 (exit code: {result.ExitCode})\nStderr: {result.Error}");
                        }
                    });

                    return true; // 비동기는 항상 true 반환 (실제 결과는 나중에)
                }
                else
                {
                    // 동기 모드: 결과 대기
                    var result = AITPlatformHelper.ExecuteCommand(
                        fullCommand,
                        buildPath,
                        additionalPaths.ToArray(),
                        verbose: verbose
                    );

                    if (showProgressBar)
                    {
                        EditorUtility.ClearProgressBar();
                    }

                    if (result.Success)
                    {
                        if (verbose) Debug.Log($"[{pmName}] ✓ {command} 성공");
                        return true;
                    }
                    else
                    {
                        if (verbose) Debug.LogWarning($"[{pmName}] ✗ {command} 실패 (exit code: {result.ExitCode})\nStderr: {result.Error}");
                        return false;
                    }
                }
            }
            catch (Exception e)
            {
                if (showProgressBar && !async)
                {
                    EditorUtility.ClearProgressBar();
                }
                if (verbose) Debug.LogError($"[Package Manager] 명령 실행 중 예외 발생: {e.Message}");
                return false;
            }
        }
    }
}
