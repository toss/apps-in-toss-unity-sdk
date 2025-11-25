using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 패키지 매니저 및 Node.js 검색/설치 통합 유틸리티
    /// 모든 npm/pnpm/node 관련 로직을 중앙화하여 관리
    /// </summary>
    public static class AITPackageManagerHelper
    {
        /// <summary>
        /// 표준 실행 파일 경로 (macOS/Linux 기준)
        /// Windows의 경우 환경변수 PATH에서 찾음
        /// </summary>
        private static readonly string[] StandardPaths = new string[]
        {
            "/opt/homebrew/bin",     // Apple Silicon Mac (Homebrew)
            "/usr/local/bin",        // Intel Mac (Homebrew)
            "/usr/bin"               // System default
        };

        /// <summary>
        /// npm 실행 환경에서 node 경로 찾기
        /// npm이 실행되는 환경에서는 node도 반드시 있어야 하므로, npm을 통해 node 찾기
        /// </summary>
        /// <param name="npmPath">npm 실행 파일 경로</param>
        /// <param name="verbose">상세 로그 출력 여부</param>
        /// <returns>node 실행 파일 절대 경로 또는 null</returns>
        private static string FindNodeFromNpm(string npmPath, bool verbose = true)
        {
            string npmDir = Path.GetDirectoryName(npmPath);

            if (verbose) Debug.Log($"[Package Manager] Finding node from npm path: {npmPath}");

            // 1. npm과 같은 디렉토리에서 node 찾기 (가장 확실한 방법)
            string nodePath = Path.Combine(npmDir, "node");
            if (verbose) Debug.Log($"[Package Manager] [1/4] Checking npm directory: {nodePath}");

            if (File.Exists(nodePath))
            {
                if (verbose) Debug.Log($"[Package Manager] ✓ node found in npm directory: {nodePath}");
                return nodePath;
            }
            else
            {
                if (verbose) Debug.Log($"[Package Manager] ✗ node not found in npm directory");
            }

            // 2. npm config get prefix 명령으로 prefix 경로 알아내기
            try
            {
                if (verbose) Debug.Log($"[Package Manager] [2/4] Trying 'npm config get prefix'...");

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-l -c \"export PATH='{npmDir}:$PATH' && '{npmPath}' config get prefix\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string prefix = process.StandardOutput.ReadToEnd().Trim();
                string stderr = process.StandardError.ReadToEnd().Trim();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(prefix))
                {
                    if (verbose) Debug.Log($"[Package Manager]   npm prefix: {prefix}");

                    // prefix/bin/node 경로 확인
                    string prefixNodePath = Path.Combine(prefix, "bin", "node");
                    if (verbose) Debug.Log($"[Package Manager]   Checking: {prefixNodePath}");

                    if (File.Exists(prefixNodePath))
                    {
                        if (verbose) Debug.Log($"[Package Manager] ✓ node found via npm prefix: {prefixNodePath}");
                        return prefixNodePath;
                    }
                    else
                    {
                        if (verbose) Debug.Log($"[Package Manager] ✗ node not found at prefix/bin/node");
                    }
                }
                else
                {
                    if (verbose) Debug.Log($"[Package Manager] ✗ npm config get prefix failed (exit: {process.ExitCode})");
                    if (verbose && !string.IsNullOrEmpty(stderr)) Debug.Log($"[Package Manager]   stderr: {stderr}");
                }
            }
            catch (Exception e)
            {
                if (verbose) Debug.LogWarning($"[Package Manager] ✗ npm config get prefix exception: {e.Message}");
            }

            // 3. npm 실행 환경에서 type -P node (함수가 아닌 실제 실행 파일만)
            try
            {
                if (verbose) Debug.Log($"[Package Manager] [3/4] Trying 'type -P node' in npm environment...");

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-l -c \"export PATH='{npmDir}:$PATH' && type -P node\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                string stderr = process.StandardError.ReadToEnd().Trim();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    if (verbose) Debug.Log($"[Package Manager]   type -P node output: {output}");

                    if (File.Exists(output))
                    {
                        if (verbose) Debug.Log($"[Package Manager] ✓ node found via type -P in npm environment: {output}");
                        return output;
                    }
                    else
                    {
                        if (verbose) Debug.Log($"[Package Manager] ✗ type -P returned path but file doesn't exist: {output}");
                    }
                }
                else
                {
                    if (verbose) Debug.Log($"[Package Manager] ✗ type -P node failed (exit: {process.ExitCode})");
                    if (verbose && !string.IsNullOrEmpty(stderr)) Debug.Log($"[Package Manager]   stderr: {stderr}");
                }
            }
            catch (Exception e)
            {
                if (verbose) Debug.LogWarning($"[Package Manager] ✗ type -P node exception: {e.Message}");
            }

            // 4. 일반적인 경로에서 찾기
            if (verbose) Debug.Log($"[Package Manager] [4/4] Checking standard paths...");
            string generalNodePath = FindExecutable("node", verbose: verbose);
            if (!string.IsNullOrEmpty(generalNodePath))
            {
                if (verbose) Debug.Log($"[Package Manager] ✓ node found in standard paths: {generalNodePath}");
                return generalNodePath;
            }
            else
            {
                if (verbose) Debug.Log($"[Package Manager] ✗ node not found in standard paths");
            }

            if (verbose) Debug.LogWarning("[Package Manager] ✗ Could not find node in any location (tried 4 methods)");
            return null;
        }

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
                Debug.LogError("[Package Manager] ✗ 내장 Node.js 다운로드 실패");
                Debug.LogError("[Package Manager]   Tools~/NodeJS/ 디렉토리 확인 필요");
                return null;
            }

            // npm 경로에서 node bin 디렉토리 추출
            string embeddedNodeDir = System.IO.Path.GetDirectoryName(embeddedNpm);
            string nodePath = System.IO.Path.Combine(embeddedNodeDir, "node");

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
            string pnpmPath = System.IO.Path.Combine(embeddedNodeDir, "pnpm");

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
        /// npm 찾기 (시스템 npm → Embedded Node.js)
        /// </summary>
        /// <param name="verbose">상세 로그 출력 여부</param>
        /// <returns>npm 실행 파일 경로</returns>
        public static string FindNpm(bool verbose = true)
        {
            // 1. 시스템 npm 찾기
            string npmPath = FindExecutable("npm", verbose);
            if (!string.IsNullOrEmpty(npmPath))
            {
                if (verbose) Debug.Log($"[npm] 시스템 npm 사용: {npmPath}");
                return npmPath;
            }

            // 2. Embedded portable Node.js 사용 (자동 다운로드)
            if (verbose) Debug.LogWarning("[npm] 시스템 npm 없음. Embedded Node.js 확인...");
            string embeddedNpm = AITNodeJSDownloader.FindEmbeddedNpm(autoDownload: true);
            if (!string.IsNullOrEmpty(embeddedNpm))
            {
                if (verbose) Debug.Log($"[npm] Embedded npm 사용: {embeddedNpm}");
                return embeddedNpm;
            }

            // 3. 둘 다 없으면 에러
            if (verbose) Debug.LogError("[npm] npm을 찾을 수 없습니다. Node.js가 설치되어 있는지 확인하세요.");
            return null;
        }

        /// <summary>
        /// 실행 파일 경로 찾기 (표준 경로 + which 명령)
        /// </summary>
        /// <param name="executableName">실행 파일 이름 (예: "pnpm", "npm", "node")</param>
        /// <param name="verbose">상세 로그 출력 여부</param>
        /// <returns>실행 파일 절대 경로 또는 null</returns>
        public static string FindExecutable(string executableName, bool verbose = true)
        {
            if (verbose) Debug.Log($"[Package Manager]   '{executableName}' 검색 중...");

            // 1. 표준 경로에서 찾기
            if (verbose) Debug.Log($"[Package Manager]   표준 경로 검색...");
            foreach (string dir in StandardPaths)
            {
                string fullPath = Path.Combine(dir, executableName);
                if (verbose) Debug.Log($"[Package Manager]     확인: {fullPath}");
                if (File.Exists(fullPath))
                {
                    if (verbose) Debug.Log($"[Package Manager]   ✓ 발견: {fullPath}");
                    return fullPath;
                }
            }
            if (verbose) Debug.Log($"[Package Manager]   표준 경로에서 찾지 못함");

            // 2. type -P 명령으로 찾기 (PATH 환경변수 고려, nvm/함수 무시)
            // type -P는 which와 달리 함수가 아닌 실제 실행 파일 경로만 반환
            if (verbose) Debug.Log($"[Package Manager]   type -P {executableName} 실행...");
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-l -c \"type -P {executableName}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                string error = process.StandardError.ReadToEnd().Trim();
                process.WaitForExit();

                if (verbose) Debug.Log($"[Package Manager]   명령어: /bin/bash -l -c \"type -P {executableName}\"");
                if (verbose) Debug.Log($"[Package Manager]   종료 코드: {process.ExitCode}");
                if (verbose && !string.IsNullOrEmpty(output)) Debug.Log($"[Package Manager]   출력: {output}");
                if (verbose && !string.IsNullOrEmpty(error)) Debug.Log($"[Package Manager]   에러: {error}");

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output))
                {
                    if (verbose) Debug.Log($"[Package Manager]   ✓ 발견: {output}");
                    return output;
                }

                if (verbose) Debug.Log($"[Package Manager]   ✗ '{executableName}' 찾을 수 없음");
            }
            catch (Exception e)
            {
                if (verbose) Debug.LogWarning($"[Package Manager]   ✗ type -P {executableName} 실행 실패: {e.Message}");
            }

            return null;
        }

        /// <summary>
        /// npm을 사용하여 pnpm 로컬 설치 (ait-build 내부)
        /// </summary>
        /// <param name="buildPath">빌드 디렉토리 경로</param>
        /// <param name="npmPath">npm 실행 파일 경로</param>
        /// <param name="verbose">상세 로그 출력 여부</param>
        /// <returns>설치 성공 여부</returns>
        private static bool InstallPnpmLocally(string buildPath, string npmPath, bool verbose = true)
        {
            try
            {
                // buildPath 디렉토리 생성 (없으면)
                if (!Directory.Exists(buildPath))
                {
                    Directory.CreateDirectory(buildPath);
                }

                // npm 디렉토리 경로 구하기 (npm 실행 파일이 있는 bin 디렉토리)
                string npmBinPath = Path.GetDirectoryName(npmPath);
                string pathEnv = $"{npmBinPath}:/usr/local/bin:/usr/bin:/bin";

                if (verbose) Debug.Log($"[Package Manager] pnpm 로컬 설치 시작: npm install pnpm");
                if (verbose) Debug.Log($"[Package Manager]   빌드 경로: {buildPath}");
                if (verbose) Debug.Log($"[Package Manager]   npm 경로: {npmPath}");

                // Progress bar 표시
                EditorUtility.DisplayProgressBar("pnpm 설치", "pnpm을 로컬에 설치하고 있습니다...", 0.5f);

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-l -c \"export PATH='{pathEnv}' && cd '{buildPath}' && '{npmPath}' install pnpm\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit();

                EditorUtility.ClearProgressBar();

                if (process.ExitCode == 0)
                {
                    if (verbose) Debug.Log($"[Package Manager] ✓ pnpm 설치 성공\n{output}");
                    return true;
                }
                else
                {
                    if (verbose) Debug.LogWarning($"[Package Manager] ✗ pnpm 설치 실패 (exit code: {process.ExitCode})\nStderr: {error}");
                    return false;
                }
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                if (verbose) Debug.LogError($"[Package Manager] pnpm 설치 중 예외 발생: {e.Message}");
                return false;
            }
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
                        if (verbose) Debug.Log("[Package Manager] pnpm이 없습니다. npm install -g pnpm 실행...");

                        // npm install -g pnpm 실행 (동기)
                        bool pnpmInstallSuccess = ExecutePackageManagerCommand(
                            packageManagerPath,
                            "install -g pnpm",
                            buildPath,
                            async: false,  // 글로벌 설치는 항상 동기
                            verbose: verbose,
                            showProgressBar: showProgressBar
                        );

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

                    // package.json 복사 (제공된 경우)
                    if (!string.IsNullOrEmpty(packageJsonTemplatePath) && File.Exists(packageJsonTemplatePath))
                    {
                        if (!Directory.Exists(buildPath))
                        {
                            Directory.CreateDirectory(buildPath);
                        }

                        string packageJsonDest = Path.Combine(buildPath, "package.json");
                        File.Copy(packageJsonTemplatePath, packageJsonDest, true);
                        if (verbose) Debug.Log($"[Package Manager] package.json 복사: {packageJsonTemplatePath} → {packageJsonDest}");
                    }

                    // install 먼저 실행 (동기 모드로 강제)
                    bool installSuccess = ExecutePackageManagerCommand(
                        packageManagerPath,
                        "install",
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
        /// 패키지 매니저 명령 실제 실행 (내부 헬퍼)
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
                string pmName = Path.GetFileName(packageManagerPath);
                string pmDir = Path.GetDirectoryName(packageManagerPath);

                // PATH 환경변수 설정 (내장 Node.js bin 경로 우선)
                string pathEnv;
                if (!string.IsNullOrEmpty(_embeddedNodeBinPath))
                {
                    // 내장 Node.js bin 경로를 맨 앞에 추가 (최우선)
                    pathEnv = $"{_embeddedNodeBinPath}:{pmDir}:/usr/local/bin:/usr/bin:/bin:/opt/homebrew/bin";
                }
                else
                {
                    pathEnv = $"{pmDir}:/usr/local/bin:/usr/bin:/bin:/opt/homebrew/bin";
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

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = $"-l -c \"export PATH='{pathEnv}' && cd '{buildPath}' && '{packageManagerPath}' {command} {(async ? "--loglevel=error" : "")}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                if (async)
                {
                    // 비동기 모드: 백그라운드에서 실행
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            Debug.Log($"[{pmName}] {e.Data}");
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data) && !e.Data.Contains("npm WARN") && !e.Data.Contains("pnpm WARN"))
                        {
                            Debug.LogWarning($"[{pmName}] {e.Data}");
                        }
                    };

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    // 비동기 대기
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        process.WaitForExit();

                        if (process.ExitCode == 0)
                        {
                            UnityEngine.Debug.Log($"[{pmName}] ✓ 백그라운드 {command} 완료");
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning($"[{pmName}] 백그라운드 {command} 실패 (exit code: {process.ExitCode})");
                        }
                    });

                    return true; // 비동기는 항상 true 반환 (실제 결과는 나중에)
                }
                else
                {
                    // 동기 모드: 결과 대기
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (showProgressBar)
                    {
                        EditorUtility.ClearProgressBar();
                    }

                    if (process.ExitCode == 0)
                    {
                        if (verbose) Debug.Log($"[{pmName}] ✓ {command} 성공\n{output}");
                        return true;
                    }
                    else
                    {
                        if (verbose) Debug.LogWarning($"[{pmName}] ✗ {command} 실패 (exit code: {process.ExitCode})\nStderr: {error}");
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
