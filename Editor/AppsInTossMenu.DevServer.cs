using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using AppsInToss.Editor;
using AppsInToss.Editor.IssueReport;
using AppsInToss.Editor.Menu;

namespace AppsInToss
{
    public partial class AppsInTossMenu
    {
        // ==================== Helper Methods ====================

        private static void RestartServer(ServerType type, bool serverOnly)
        {
            StopServer(type);
            // 프로세스 종료 대기 후 서버 재시작 (메인 스레드 블로킹 방지)
            double startTime = EditorApplication.timeSinceStartup;
            void WaitAndRestart()
            {
                if (EditorApplication.timeSinceStartup - startTime < 0.5) return;
                EditorApplication.update -= WaitAndRestart;
                if (serverOnly)
                    StartServerOnly(type);
                else
                    StartServer(type);
            }
            EditorApplication.update += WaitAndRestart;
        }

        private static void RestartDevServer() => RestartServer(ServerType.Dev, serverOnly: false);
        private static void RestartDevServerOnly() => RestartServer(ServerType.Dev, serverOnly: true);

        // ==================== 서버 타입별 헬퍼 ====================
        // Production Server 제거로 ServerType은 Dev 단일 값만 남았지만, 아래 헬퍼들의
        // ServerType 매개변수는 호출부(StartServer/StartServerOnly/LaunchServerProcess/StopServer 등)
        // 시그니처를 그대로 유지해 diff를 최소화하기 위해 남겨두었습니다.

        private static AITServerStateManager GetServerState(ServerType type) => devServerState;

        private static string GetServerLabel(ServerType type) => "Dev";

        private static AITBuildProfile GetBuildProfile(AITEditorScriptObject config, ServerType type) =>
            config.devServerProfile;

        private static string GetProfileName(ServerType type) => "Dev Server";

        /// <summary>
        /// 서버 시작 전 공통 검증: 이미 실행 중인지 확인
        /// </summary>
        /// <returns>검증 통과 시 config, 실패 시 null</returns>
        private static AITEditorScriptObject ValidateAndSwitchServer(ServerType type)
        {
            var stateManager = GetServerState(type);
            string label = GetServerLabel(type);

            // 실제 상태 검증
            var currentState = stateManager.ValidateState();
            if (currentState == ServerState.Running)
            {
                Debug.LogWarning($"AIT: {label} 서버가 이미 실행 중입니다.");
                return null;
            }

            var config = UnityUtil.GetEditorConf();
            if (!PathValidator.ValidateSettings(config))
            {
                return null;
            }

            return config;
        }

        // ==================== 통합 서버 메서드 ====================

        /// <summary>
        /// 서버 시작 (빌드 & 패키징 수행 후 granite dev 실행)
        /// DoExportAsync 기반 — BuildConfig 복사 + pnpm install을 WebGL 빌드와 병렬 실행해
        /// install 시간을 빌드 시간에 숨긴다 (참고: AITDeployManager.RunBuildAndPackage).
        /// </summary>
        private static async void StartServer(ServerType type)
        {
            if (_devServerBuildInProgress)
            {
                Debug.LogWarning("AIT: 이미 Dev 서버 빌드가 진행 중입니다.");
                return;
            }

            var config = ValidateAndSwitchServer(type);
            if (config == null) return;

            // WaitAsync 대기 중(수십 초) 재클릭을 여기서부터 차단 — AssetDatabase.SaveAssets()
            // 이후 도메인 리로드가 끼어들 수 있는 구간이므로 가드를 진입 시점에 세운다.
            _devServerBuildInProgress = true;

            try
            {
                // Configuration Window 변경이 디스크에 flush되기 전 상태일 수 있다.
                // 빌드 중 도메인 리로드 또는 Editor 강제 종료 시 유실을 방지하기 위해 강제 flush.
                AssetDatabase.SaveAssets();

                // 리로드를 유발할 수 있는 컴파일/업데이트가 끝난 뒤 빌드 진입.
                if (!await AITEditorIdleWaiter.WaitAsync())
                {
                    _devServerBuildInProgress = false;
                    return;
                }

                string profileName = GetProfileName(type);
                var profile = GetBuildProfile(config, type);

                // Dev Server는 granite build(production build)를 스킵하여 시작 속도 개선
                bool skipGraniteBuild = (type == ServerType.Dev);
                // 빠른 빌드(Dev Server): IL2CPP Debug 구성 + Code Generation OptimizeSize +
                // 에셋 최적화 검사 스킵으로 반복 루프 속도 개선
                bool fastBuild = (type == ServerType.Dev);

                Debug.Log($"AIT: 빌드 & 패키징 수행 중 (증분 빌드, {profileName} 프로필{(skipGraniteBuild ? ", granite build 스킵" : "")})...");
                buildStopwatch.Restart();

                AITConvertCore.DoExportAsync(
                    buildWebGL: true,
                    doPackaging: true,
                    cleanBuild: false,
                    profile: profile,
                    profileName: profileName,
                    onComplete: (result) =>
                    {
                        buildStopwatch.Stop();
                        EditorUtility.ClearProgressBar();
                        _devServerBuildInProgress = false;

                        if (result == AITConvertCore.AITExportError.CANCELLED)
                        {
                            Debug.Log("AIT: 빌드가 사용자에 의해 취소되었습니다.");
                            AITPlatformHelper.ShowInfoDialog("취소됨", "빌드가 취소되었습니다.", "확인");
                            return;
                        }

                        if (result != AITConvertCore.AITExportError.SUCCEED)
                        {
                            AITDeployManager.ShowBuildFailedDialog(result, profileName);
                            return;
                        }

                        Debug.Log($"AIT: 빌드 & 패키징 완료 (소요 시간: {buildStopwatch.Elapsed.TotalSeconds:F1}초)");

                        string buildPath = PathValidator.GetBuildTemplatePath();
                        string npmPath = PathValidator.FindNpmPath();
                        if (!PathValidator.ValidateNpmPath(npmPath))
                        {
                            return;
                        }

                        // Note: EnsureNodeModules 호출 제거 - PackageWebGLBuild에서 이미 pnpm install 실행됨

                        // config를 여기서 새로 조회 — StartServer 진입 시점 config는 수십 초 뒤인
                        // 이 시점까지 유효하다는 보장이 없다 (재임포트/도메인 리로드 시 destroyed 참조화).
                        LaunchServerProcess(type, UnityUtil.GetEditorConf(), buildPath, npmPath, openBrowser: true);
                    },
                    onProgress: (phase, progress, status) =>
                    {
                        // DisplayCancelableProgressBar로 취소 가능한 진행률 표시 (AITDeployManager와 동일 패턴)
                        bool cancelled = EditorUtility.DisplayCancelableProgressBar(
                            "Apps in Toss - Dev Server 빌드",
                            status,
                            progress
                        );

                        if (cancelled)
                        {
                            AITConvertCore.CancelBuild();
                        }
                    },
                    skipGraniteBuild: skipGraniteBuild,
                    fastBuild: fastBuild
                );
            }
            catch (Exception e)
            {
                // DoExportAsync 진입 전(BeginBuild 세션 파일 기록, PlayerSettingsSnapshot.Capture 등)
                // 동기 구간에서 예외가 나면 onComplete가 절대 호출되지 않는다 — 여기서 가드를 풀지
                // 않으면 도메인 리로드 전까지 Start Server 메뉴가 영구 비활성화된다.
                _devServerBuildInProgress = false;
                EditorUtility.ClearProgressBar();
                AITLog.Error($"AIT: Dev 서버 빌드 준비 중 예외: {e.Message}", sentryCapture: true);
                AITPlatformHelper.ShowInfoDialog("오류", $"빌드 준비 중 오류가 발생했습니다.\n\n{e.Message}", "확인");
            }
        }

        /// <summary>
        /// 서버 시작 (서버만, 빌드 없음)
        /// 기존 빌드 결과물을 사용하여 granite dev 서버만 재시작
        /// </summary>
        private static void StartServerOnly(ServerType type)
        {
            var config = ValidateAndSwitchServer(type);
            if (config == null) return;

            string buildPath = PathValidator.GetBuildTemplatePath();

            // 빌드 결과물이 있는지 확인
            if (!Directory.Exists(buildPath))
            {
                AITLog.Error($"AIT: 빌드 결과물이 없습니다. 먼저 AIT > Advanced > Build & Package를 실행하세요. ({buildPath})", sentryCapture: false);
                AITPlatformHelper.ShowInfoDialog("빌드 필요", "빌드 결과물이 없습니다.\n먼저 AIT > Advanced > Build & Package를 실행하세요.", "확인");
                return;
            }

            string npmPath = PathValidator.FindNpmPath();
            if (!PathValidator.ValidateNpmPath(npmPath))
            {
                return;
            }

            if (!PathValidator.EnsureNodeModules(buildPath, npmPath))
            {
                return;
            }

            LaunchServerProcess(type, config, buildPath, npmPath, openBrowser: false);
        }

        /// <summary>
        /// 서버 프로세스 실행 공통 로직 (포트 해석 → 프로세스 시작)
        /// </summary>
        private static void LaunchServerProcess(
            ServerType type, AITEditorScriptObject config,
            string buildPath, string npmPath, bool openBrowser)
        {
            var stateManager = GetServerState(type);
            string label = GetServerLabel(type);
            string logPrefix = GetProfileName(type);
            string suffix = openBrowser ? "" : " (서버만)";

            // vite 단독 모드(web-framework 3.x) 여부 — granite 포트를 열지 않으므로
            // 포트 가용성 검사와 Granite 관련 로그를 건너뛴다
            bool viteOnly = DevServerCommandResolver.IsViteOnly(buildPath);

            // devtools(mock SDK) 활성화 여부 — config.devtools.enabled + viteOnly + 설치 확인을
            // 모두 통과해야 활성화되며, 실패해도 throw하지 않고 reason과 함께 비활성으로 계속 진행한다
            bool devtoolsOn = DevtoolsSupport.ShouldEnable(config, buildPath, viteOnly, out string devtoolsReason);
            if (!devtoolsOn)
            {
                Debug.LogWarning($"AIT: Devtools 비활성화 — {devtoolsReason}");
            }

            // 서버 포트 해석 및 충돌 검사
            if (!PortResolver.TryResolveServerPorts(config,
                out string graniteHost, out int granitePort,
                out string viteHost, out int vitePort,
                skipGranitePortScan: viteOnly))
            {
                return;
            }

            Debug.Log($"AIT: {label} 서버 시작 중{suffix}... ({buildPath})");
            if (!viteOnly)
                Debug.Log($"AIT:   Granite: {graniteHost}:{granitePort}");
            Debug.Log($"AIT:   Vite: {viteHost}:{vitePort}");
            Debug.Log($"AIT:   Devtools: {(devtoolsOn ? "활성화" : "비활성화")}");

            // 캡처용 로컬 변수
            int finalVitePort = vitePort;
            int finalGranitePort = granitePort;

            try
            {
                // 환경 변수로 Vite 설정 전달 (granite.config.ts, vite.config.ts에서 사용)
                var envVars = new Dictionary<string, string>
                {
                    { "AIT_GRANITE_HOST", graniteHost },
                    { "AIT_GRANITE_PORT", finalGranitePort.ToString() },
                    { "AIT_VITE_HOST", viteHost },
                    { "AIT_VITE_PORT", finalVitePort.ToString() }
                };
                DevtoolsSupport.AddEnvVars(envVars, config, devtoolsOn);

                // web-framework 버전에 맞는 dev 서버 커맨드 해석
                // (2.x: granite bin 파일을 node로 직접 실행 — .bin/granite 이름 충돌 우회,
                //  3.x: vite bin을 node로 직접 실행 — resolve된 경우 pnpm CLI 기동 자체를 생략)
                string devCommand = DevServerCommandResolver.Resolve(buildPath, finalVitePort, out viteOnly, out string directExecutablePath);
                int expectedPort = viteOnly ? finalVitePort : finalGranitePort;

                // directExecutablePath가 있으면 pnpm을 거치지 않고 node를 직접 실행 (5b), 없으면 기존처럼 pnpm 경유
                string executablePath = directExecutablePath ?? npmPath;
                string executableLabel = directExecutablePath != null ? "node" : "pnpm";
                Debug.Log($"AIT:   dev 커맨드: {executableLabel} {devCommand}");

                var processManager = new AITProcessTreeManager();

                // 포트와 프로세스 관리자 저장 (상태는 변경하지 않음)
                stateManager.SetExpectedPortAndProcess(processManager, expectedPort);

                StartServerProcessWithPortDetection(
                    processManager,
                    buildPath, executablePath, devCommand, logPrefix, envVars, expectedPort,
                    onServerStarted: (detectedPort) =>
                    {
                        // 감지된 포트를 저장하여 ValidateState에서 올바르게 확인할 수 있도록 함
                        stateManager.OnServerStarted(detectedPort);
                        Debug.Log($"AIT: {label} 서버가 시작되었습니다{suffix}");
                        if (!viteOnly)
                            Debug.Log($"AIT:   Granite (Metro): http://{graniteHost}:{finalGranitePort}");
                        Debug.Log($"AIT:   Vite: http://{viteHost}:{finalVitePort}");
                        if (openBrowser)
                            AITBrowserLauncher.OpenBrowser(finalVitePort);
                    },
                    onServerFailed: (reason) =>
                    {
                        Debug.LogError($"AIT: {label} 서버 시작 실패 - {reason}");
                        AITPlatformHelper.ShowInfoDialog($"{label} 서버 시작 실패", reason, "확인");
                        stateManager.OnServerFailed();
                    }
                );
            }
            catch (Exception e)
            {
                Debug.LogError($"AIT: {label} 서버 시작 실패: {e}");
                AITPlatformHelper.ShowInfoDialog("오류", $"{label} 서버 시작 실패:\n{e.Message}", "확인");
                stateManager.OnServerFailed();
            }
        }

        /// <summary>
        /// 서버 중지
        /// </summary>
        private static void StopServer(ServerType type)
        {
            var stateManager = GetServerState(type);
            string label = GetServerLabel(type);

            // 백업: 포트에서 실행 중인 프로세스도 종료 (혹시 남아있는 경우)
            int port = stateManager?.Port ?? 0;
            if (port > 0)
            {
                PortResolver.KillProcessOnPort(port);
            }

            // 상태 관리자에 중지 알림
            stateManager?.OnServerStopped();

            Debug.Log($"AIT: {label} 서버가 중지되었습니다.");
        }

        // 서버 시작 타임아웃 (30초)
        private const double SERVER_START_TIMEOUT_SECONDS = 30.0;

        // 포트 직접 확인 폴백 시작 시간 (5초 후부터)
        // stdout 파싱이 실패해도 포트가 열려있으면 성공으로 처리
        private const double PORT_FALLBACK_CHECK_START_SECONDS = 5.0;

        /// <summary>
        /// 서버 프로세스 시작 (동적 포트 감지 포함) - 크로스 플랫폼
        /// AITProcessTreeManager를 사용하여 프로세스 트리 전체를 관리
        /// </summary>
        /// <param name="manager">프로세스 트리 관리자</param>
        /// <param name="npmPath">실행할 바이너리의 절대 경로. pnpm(기존 exec 경로) 또는 node(직접 실행 경로 — 5b)</param>
        /// <param name="envVars">환경 변수 (AIT_GRANITE_HOST, AIT_GRANITE_PORT, AIT_VITE_PORT 등)</param>
        /// <param name="expectedPort">예상 포트 (타임아웃 시 확인용)</param>
        /// <param name="onServerStarted">서버가 성공적으로 시작되면 호출되는 콜백 (메인 스레드에서 실행)</param>
        /// <param name="onServerFailed">서버 시작에 실패하면 호출되는 콜백 (메인 스레드에서 실행)</param>
        private static void StartServerProcessWithPortDetection(
            AITProcessTreeManager manager,
            string buildPath,
            string npmPath,
            string npmCommand,
            string logPrefix,
            Dictionary<string, string> envVars,
            int expectedPort,
            Action<int> onServerStarted,
            Action<string> onServerFailed = null)
        {
            string npmDir = Path.GetDirectoryName(npmPath);
            string pathEnv = AITPlatformHelper.BuildPathEnv(npmDir);

            ProcessStartInfo startInfo;

            if (AITPlatformHelper.IsWindows)
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"\"{npmPath}\" {npmCommand}\"",
                    WorkingDirectory = buildPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };
                startInfo.EnvironmentVariables["PATH"] = pathEnv;

                // Windows: 환경 변수 직접 설정
                if (envVars != null)
                {
                    foreach (var kv in envVars)
                    {
                        startInfo.EnvironmentVariables[kv.Key] = kv.Value;
                    }
                }
            }
            else
            {
                string escapedPathEnv = AITPlatformHelper.EscapeForBashDoubleQuotes(pathEnv);
                string escapedBuildPath = AITPlatformHelper.EscapeForBashDoubleQuotes(buildPath);
                string escapedNpmPath = AITPlatformHelper.EscapeForBashDoubleQuotes(npmPath);
                startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/bash",
                    // 로그인 셸(-l) 미사용: PATH는 위에서 BuildPathEnv로 명시 구성하고 실행 파일도
                    // 절대경로로 넘기므로 셸 프로파일 소싱 없이도 자급자족한다 (기동 비용 절감).
                    Arguments = $"-c \"export PATH=\\\"{escapedPathEnv}\\\" && cd \\\"{escapedBuildPath}\\\" && \\\"{escapedNpmPath}\\\" {npmCommand}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8,
                    StandardErrorEncoding = System.Text.Encoding.UTF8
                };

                // 환경변수는 ProcessStartInfo.EnvironmentVariables로 설정
                // bash -c "..." 안에서 export 할당 시 JSON 등의 큰따옴표가
                // 바깥 큰따옴표와 충돌하므로, 셸 명령에서는 export하지 않음
                if (envVars != null)
                {
                    foreach (var kv in envVars)
                    {
                        startInfo.EnvironmentVariables[kv.Key] = kv.Value;
                    }
                }
            }

            // pnpm이 비대화형(no TTY) 환경임을 인식하도록 CI=true 설정
            // (없으면 node_modules purge 확인 프롬프트에서 ERR_PNPM_ABORTED_REMOVE_MODULES_DIR_NO_TTY 발생)
            // AITPlatformHelper.CreateProcessStartInfo / AITAsyncCommandRunner와 동일한 컨벤션
            startInfo.EnvironmentVariables["CI"] = "true";

            // 스레드 안전한 상태 플래그 (Interlocked 사용)
            // OutputDataReceived는 ThreadPool 스레드에서 호출되므로 원자적 연산 필요
            int serverStartedFlag = 0;  // 0 = false, 1 = true
            int serverFailedFlag = 0;   // 0 = false, 1 = true
            object failureReasonLock = new object();
            string failureReason = null;
            double startTime = EditorApplication.timeSinceStartup;

            // AITProcessTreeManager를 통해 프로세스 시작 (프로세스 그룹 관리)
            var process = manager.StartProcess(startInfo);

            // 타임아웃 체크 및 포트 폴백 확인을 위한 EditorApplication.update 콜백
            EditorApplication.CallbackFunction timeoutCheck = null;
            timeoutCheck = () =>
            {
                // 이미 성공 또는 실패한 경우 콜백 제거 (원자적 읽기)
                if (Interlocked.CompareExchange(ref serverStartedFlag, 0, 0) == 1 ||
                    Interlocked.CompareExchange(ref serverFailedFlag, 0, 0) == 1)
                {
                    EditorApplication.update -= timeoutCheck;
                    return;
                }

                double elapsed = EditorApplication.timeSinceStartup - startTime;

                // 폴백 체크: stdout 파싱이 실패해도 포트가 열려있으면 성공으로 처리
                // (일부 환경에서 stdout 버퍼링으로 인해 포트 정보가 지연될 수 있음)
                if (elapsed > PORT_FALLBACK_CHECK_START_SECONDS && expectedPort > 0)
                {
                    if (!PortResolver.IsPortAvailable(expectedPort))
                    {
                        // 포트가 사용 중 = 서버가 시작됨
                        if (Interlocked.CompareExchange(ref serverStartedFlag, 1, 0) == 0)
                        {
                            EditorApplication.update -= timeoutCheck;
                            Debug.Log($"[{logPrefix}] 포트 {expectedPort} 감지됨 (stdout 폴백)");
                            onServerStarted?.Invoke(expectedPort);
                            return;
                        }
                    }
                }

                // 타임아웃 체크
                if (elapsed > SERVER_START_TIMEOUT_SECONDS)
                {
                    // 원자적으로 실패 플래그 설정 (중복 호출 방지)
                    if (Interlocked.CompareExchange(ref serverFailedFlag, 1, 0) == 0)
                    {
                        EditorApplication.update -= timeoutCheck;

                        // 프로세스 종료 시도
                        try
                        {
                            manager.KillProcessTree();
                        }
                        catch
                        {
                            // 무시
                        }

                        string reason = $"서버 시작 타임아웃 ({SERVER_START_TIMEOUT_SECONDS}초)";
                        Debug.LogError($"[{logPrefix}] {reason}");
                        onServerFailed?.Invoke(reason);
                    }
                }
            };
            EditorApplication.update += timeoutCheck;

            // 프로세스 종료 감지
            process.EnableRaisingEvents = true;
            process.Exited += (sender, args) =>
            {
                // 서버가 아직 시작되지 않았는데 프로세스가 종료된 경우 = 실패
                // 원자적으로 플래그 확인 및 설정
                if (Interlocked.CompareExchange(ref serverStartedFlag, 0, 0) == 0 &&
                    Interlocked.CompareExchange(ref serverFailedFlag, 1, 0) == 0)
                {
                    int exitCode = process.ExitCode;
                    string reason;
                    lock (failureReasonLock)
                    {
                        reason = failureReason ?? $"프로세스가 비정상 종료되었습니다 (Exit Code: {exitCode})";
                    }

                    // 스레드 안전한 메인 스레드 큐를 통해 콜백 실행
                    MainThreadDispatcher.Enqueue(() =>
                    {
                        EditorApplication.update -= timeoutCheck;
                        Debug.LogError($"[{logPrefix}] 서버 시작 실패: {reason}");
                        onServerFailed?.Invoke(reason);
                    });
                }
            };

            process.OutputDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    string cleanOutput = Regex.Replace(args.Data, @"\x1B\[[0-9;]*[mGKH]", "");

                    // 에러 패턴 감지 (stdout에도 에러가 출력될 수 있음)
                    if (PathValidator.IsErrorOutput(cleanOutput))
                    {
                        Debug.LogError($"[{logPrefix}] {cleanOutput}");

                        // 포트 충돌 에러 감지
                        if (PortResolver.IsPortConflictError(cleanOutput))
                        {
                            lock (failureReasonLock)
                            {
                                failureReason = "포트가 이미 사용 중입니다. 다른 서버가 실행 중인지 확인하세요.";
                            }
                        }
                    }
                    else
                    {
                        Debug.Log($"[{logPrefix}] {args.Data}");
                    }

                    // 서버 시작 성공 감지 (포트 감지)
                    // IPv4: localhost:PORT, 0.0.0.0:PORT, 127.0.0.1:PORT
                    // IPv6: [::1]:PORT, [::]:PORT
                    // 원자적으로 플래그 확인 (ThreadPool 스레드에서 호출됨)
                    if (Interlocked.CompareExchange(ref serverStartedFlag, 0, 0) == 0 &&
                        Interlocked.CompareExchange(ref serverFailedFlag, 0, 0) == 0)
                    {
                        var portMatch = Regex.Match(cleanOutput, @"(?:localhost|0\.0\.0\.0|127\.0\.0\.1|\[::1?\]):(\d+)");
                        if (portMatch.Success)
                        {
                            int port = int.Parse(portMatch.Groups[1].Value);

                            // 원자적으로 성공 플래그 설정 (중복 호출 방지)
                            if (Interlocked.CompareExchange(ref serverStartedFlag, 1, 0) == 0)
                            {
                                // 스레드 안전한 메인 스레드 큐를 통해 콜백 실행
                                MainThreadDispatcher.Enqueue(() =>
                                {
                                    EditorApplication.update -= timeoutCheck;
                                    onServerStarted?.Invoke(port);
                                });
                            }
                        }
                    }
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (args.Data != null)
                {
                    string cleanOutput = Regex.Replace(args.Data, @"\x1B\[[0-9;]*[mGKH]", "");

                    // stderr 출력을 실제 에러와 경고로 분류
                    if (PathValidator.IsErrorOutput(cleanOutput))
                        Debug.LogError($"[{logPrefix}] {cleanOutput}");
                    else
                        Debug.LogWarning($"[{logPrefix}] {cleanOutput}");

                    // 포트 충돌 에러 감지
                    if (PortResolver.IsPortConflictError(cleanOutput))
                    {
                        lock (failureReasonLock)
                        {
                            failureReason = "포트가 이미 사용 중입니다. 다른 서버가 실행 중인지 확인하세요.";
                        }
                    }
                }
            };

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }
    }
}
