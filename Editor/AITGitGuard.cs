using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// SDK 관련 파일들이 Git에 커밋되지 않도록 보호하는 클래스
    /// Editor 시작 시 자동으로 .gitignore를 체크하고, 필요시 패턴을 추가합니다.
    /// Git이 설치되지 않았거나 Git 저장소가 아닌 경우 안전하게 스킵합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class AITGitGuard
    {
        /// <summary>
        /// .gitignore에 추가할 패턴 목록
        /// </summary>
        private static readonly string[] GITIGNORE_PATTERNS = new[]
        {
            // 민감 정보
            "**/AITCredentials.asset",
            "**/AITCredentials.asset.meta",
            // 빌드 산출물
            "ait-build/",
            "webgl/",
        };

        /// <summary>
        /// tracked 여부를 감지하여 자동 untrack할 대상 목록
        /// </summary>
        private static readonly (string path, bool isDirectory)[] TRACKED_CHECK_TARGETS = new[]
        {
            ("Assets/AppsInToss/Editor/AITCredentials.asset", false),
            ("ait-build/", true),
            ("webgl/", true),
        };

        private const string PREFS_KEY_GITIGNORE_CHECKED = "AIT_GitIgnore_Checked_v3";

        static AITGitGuard()
        {
            // Editor가 완전히 로드된 후에 체크 실행
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            // 이미 체크했는지 확인 (세션당 1회)
            if (SessionState.GetBool(PREFS_KEY_GITIGNORE_CHECKED, false))
            {
                return;
            }

            SessionState.SetBool(PREFS_KEY_GITIGNORE_CHECKED, true);

            // Git 저장소인지 확인
            if (!IsGitRepository())
            {
                return;
            }

            // .gitignore 체크 및 필요시 패턴 추가
            CheckAndUpdateGitIgnore();

            // 이미 tracked된 파일/디렉토리인지 체크
            CheckTrackedTargets();
        }

        // ============================================================
        // Git 유틸리티
        // ============================================================

        /// <summary>
        /// git 명령어를 실행하고 결과를 반환
        /// </summary>
        /// <returns>성공 시 (exitCode, stdout, stderr), 실행 불가 시 null</returns>
        private static (int exitCode, string stdout, string stderr)? RunGit(string arguments, int timeoutMs = 5000)
        {
            string projectRoot = GetProjectRoot();

            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = arguments,
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                // Unity Editor 프로세스의 PATH에는 Homebrew 등의 경로가 없어
                // git이 gpg 등 외부 도구를 찾지 못하는 문제 방지
                psi.EnvironmentVariables["PATH"] = AITPlatformHelper.BuildPathEnv();

                using (Process process = Process.Start(psi))
                {
                    // stdout/stderr를 모두 비동기로 읽어 데드락 완전 방지
                    // ReadToEnd()는 프로세스 종료까지 블로킹하므로 WaitForExit 타임아웃이 무효화됨
                    // ReadToEndAsync()는 백그라운드에서 읽기 시작하므로 WaitForExit가 실제 타임아웃으로 동작
                    var stdoutTask = process.StandardOutput.ReadToEndAsync();
                    var stderrTask = process.StandardError.ReadToEndAsync();

                    if (!process.WaitForExit(timeoutMs))
                    {
                        try { process.Kill(); } catch (System.Exception) { }
                        try { System.Threading.Tasks.Task.WaitAll(stdoutTask, stderrTask); } catch (System.Exception) { }
                        Debug.LogWarning($"[AIT] Git 명령 타임아웃 ({timeoutMs / 1000}초): git {arguments}");
                        return null;
                    }

                    return (process.ExitCode, stdoutTask.Result, stderrTask.Result);
                }
            }
            catch (System.Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 현재 프로젝트가 Git 저장소이고 git CLI를 사용할 수 있는지 확인
        /// Unity Version Control 등 다른 VCS를 사용하는 경우 false 반환
        /// </summary>
        private static bool IsGitRepository()
        {
            string projectRoot = GetProjectRoot();
            string gitDir = Path.Combine(projectRoot, ".git");
            if (!Directory.Exists(gitDir) && !File.Exists(gitDir))
            {
                return false;
            }

            // git CLI 사용 가능 여부 확인
            var result = RunGit("rev-parse --git-dir", 3000);
            return result.HasValue && result.Value.exitCode == 0;
        }

        /// <summary>
        /// Unity 프로젝트 루트 경로 반환
        /// </summary>
        private static string GetProjectRoot()
        {
            // Application.dataPath는 Assets 폴더를 가리킴
            return Directory.GetParent(Application.dataPath).FullName;
        }

        // ============================================================
        // .gitignore 관리
        // ============================================================

        /// <summary>
        /// .gitignore 파일을 체크하고 필요시 패턴 추가
        /// </summary>
        private static void CheckAndUpdateGitIgnore()
        {
            string projectRoot = GetProjectRoot();
            string gitignorePath = Path.Combine(projectRoot, ".gitignore");

            // 누락된 패턴 수집
            System.Collections.Generic.List<string> missingPatterns = new System.Collections.Generic.List<string>();
            string existingContent = "";

            if (File.Exists(gitignorePath))
            {
                existingContent = File.ReadAllText(gitignorePath);
            }

            foreach (string pattern in GITIGNORE_PATTERNS)
            {
                // 패턴의 핵심 부분 추출 (예: "**/AITCredentials.asset" → "AITCredentials.asset")
                string patternKey = pattern.TrimStart('*', '/');
                if (!ContainsPattern(existingContent, patternKey))
                {
                    missingPatterns.Add(pattern);
                }
            }

            // 모든 패턴이 있으면 종료
            if (missingPatterns.Count == 0)
            {
                return;
            }

            // 다이얼로그 메시지 생성
            string patternList = "";
            foreach (string pattern in missingPatterns)
            {
                patternList += $"• {pattern}\n";
            }

            // 사용자에게 확인 후 패턴 추가 (CI에서는 자동 승인 - 보안 기능이므로)
            bool shouldAdd = AITPlatformHelper.ShowConfirmDialog(
                "Apps in Toss SDK - .gitignore 설정",
                "민감 정보와 빌드 결과물이 Git에 커밋되지 않도록 .gitignore에 패턴을 추가합니다.\n\n" +
                "추가될 패턴:\n" +
                patternList + "\n" +
                "계속하시겠습니까?",
                "추가",
                "나중에",
                autoApprove: true
            );

            if (shouldAdd)
            {
                AddPatternsToGitIgnore(gitignorePath, missingPatterns);
            }
        }

        /// <summary>
        /// 문자열에 특정 패턴이 포함되어 있는지 확인
        /// </summary>
        private static bool ContainsPattern(string content, string pattern)
        {
            // 다양한 형태의 패턴 체크 (예: AITCredentials.asset, **/AITCredentials.asset, */AITCredentials.asset 등)
            string[] lines = content.Split('\n');
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                // 정확한 패턴 매치
                if (trimmed.Contains(pattern))
                    return true;

                // 디렉토리 패턴의 경우, 상위 디렉토리가 이미 무시되고 있는지 확인
                // 예: "Assets/WebGLTemplates/"가 있으면 "Assets/WebGLTemplates/AITTemplate/"도 포함됨
                if (pattern.EndsWith("/"))
                {
                    string trimmedPattern = trimmed.TrimEnd('/');
                    string patternWithoutSlash = pattern.TrimEnd('/');
                    if (patternWithoutSlash.StartsWith(trimmedPattern + "/") ||
                        patternWithoutSlash.StartsWith(trimmedPattern))
                    {
                        // trimmed가 pattern의 상위 디렉토리인지 확인
                        if (patternWithoutSlash.StartsWith(trimmedPattern) &&
                            (patternWithoutSlash.Length == trimmedPattern.Length ||
                             patternWithoutSlash[trimmedPattern.Length] == '/'))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// .gitignore에 패턴 추가
        /// </summary>
        private static void AddPatternsToGitIgnore(string gitignorePath, System.Collections.Generic.List<string> missingPatterns)
        {
            try
            {
                string patternsToAdd = "\n# Apps in Toss SDK - 자동 보호 패턴\n";

                foreach (string pattern in missingPatterns)
                {
                    patternsToAdd += pattern + "\n";
                }

                File.AppendAllText(gitignorePath, patternsToAdd);

                Debug.Log("[AIT] .gitignore에 보호 패턴이 추가되었습니다: " + string.Join(", ", missingPatterns));

                // .gitignore 변경을 즉시 커밋하여 완결된 단위로 만듦
                var addResult = RunGit("add .gitignore", 5000);
                if (addResult == null || addResult.Value.exitCode != 0)
                {
                    string addError = addResult != null
                        ? addResult.Value.stderr.Trim()
                        : "Git 프로세스를 시작할 수 없거나 타임아웃이 발생했습니다.";
                    Debug.LogError($"[AIT] git add .gitignore 실패: {addError}");
                    AITPlatformHelper.ShowInfoDialog(
                        "자동 커밋 실패",
                        ".gitignore에 패턴은 추가되었으나 git add에 실패했습니다.\n\n" +
                        $"원인: {addError}\n\n" +
                        "수동으로 커밋해주세요:\n" +
                        "   git add .gitignore && git commit -m \"정리: .gitignore 보호 패턴 추가\"",
                        "확인"
                    );
                    return;
                }

                var commitResult = RunGit("commit --quiet -m \"정리: .gitignore 보호 패턴 추가 (AIT SDK 자동)\"", 30000);
                if (commitResult != null && commitResult.Value.exitCode == 0)
                {
                    Debug.Log("[AIT] .gitignore 변경 자동 커밋 완료");
                    AITPlatformHelper.ShowInfoDialog(
                        "완료",
                        ".gitignore에 보호 패턴이 추가되었습니다.\n\n" +
                        "민감 정보와 빌드 결과물이 Git에 커밋되지 않습니다.",
                        "확인"
                    );
                }
                else
                {
                    string commitError = commitResult != null
                        ? (commitResult.Value.stderr.Trim() + " " + commitResult.Value.stdout.Trim()).Trim()
                        : "Git 프로세스를 시작할 수 없거나 타임아웃이 발생했습니다.";
                    Debug.LogError($"[AIT] .gitignore 자동 커밋 실패: {commitError}");
                    AITPlatformHelper.ShowInfoDialog(
                        "자동 커밋 실패",
                        ".gitignore에 패턴은 추가되었으나 자동 커밋에 실패했습니다.\n\n" +
                        $"원인: {commitError}\n\n" +
                        "수동으로 커밋해주세요:\n" +
                        "   git add .gitignore && git commit -m \"정리: .gitignore 보호 패턴 추가\"",
                        "확인"
                    );
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AIT] .gitignore 업데이트 실패: {ex.Message}");

                string patternList = "";
                foreach (string pattern in missingPatterns)
                {
                    patternList += $"• {pattern}\n";
                }

                AITPlatformHelper.ShowInfoDialog(
                    "오류",
                    $".gitignore 업데이트에 실패했습니다.\n\n" +
                    $"수동으로 다음 패턴을 추가해주세요:\n" +
                    patternList + "\n" +
                    $"오류: {ex.Message}",
                    "확인"
                );
            }
        }

        // ============================================================
        // Tracked 파일/디렉토리 감지 및 자동 untrack
        // ============================================================

        /// <summary>
        /// TRACKED_CHECK_TARGETS에 정의된 모든 대상의 tracked 여부를 확인
        /// </summary>
        private static void CheckTrackedTargets()
        {
            string projectRoot = GetProjectRoot();

            foreach (var (path, isDirectory) in TRACKED_CHECK_TARGETS)
            {
                // 대상이 존재하는지 먼저 확인
                string fullPath = Path.Combine(projectRoot, path.TrimEnd('/'));
                bool exists = isDirectory ? Directory.Exists(fullPath) : File.Exists(fullPath);
                if (!exists)
                {
                    continue;
                }

                // git ls-files --error-unmatch로 tracked 여부만 확인 (출력량 무관)
                var result = RunGit($"ls-files --error-unmatch \"{path.TrimEnd('/')}\"");
                if (result == null || result.Value.exitCode != 0)
                {
                    continue;
                }

                // tracked 상태 — 사용자에게 untrack 제안
                bool isCredentials = path.Contains("AITCredentials");
                string title = isCredentials
                    ? "⚠️ 경고: 배포 키가 Git에 노출되어 있습니다!"
                    : $"⚠️ 경고: {path} 가 Git에 포함되어 있습니다";
                string message = isCredentials
                    ? "AITCredentials.asset 파일이 이미 Git에 커밋되어 있습니다.\n\n" +
                      "배포 키가 저장소에 노출될 수 있으므로, Git에서 제거하는 것을 권장합니다.\n\n" +
                      "지금 자동으로 해제하시겠습니까?"
                    : $"{path} 가 Git에 커밋되어 있습니다.\n\n" +
                      ".gitignore에 패턴이 있더라도 이미 tracked된 파일은 무시되지 않습니다.\n" +
                      "저장소 크기가 불필요하게 커지므로 Git 추적을 해제하는 것을 권장합니다.\n\n" +
                      "지금 자동으로 해제하시겠습니까?";

                bool shouldFix = AITPlatformHelper.ShowConfirmDialog(
                    title,
                    message,
                    "자동 해제",
                    "나중에",
                    autoApprove: false
                );

                if (shouldFix)
                {
                    ExecuteGitUntrack(path, isDirectory);
                }
            }
        }

        /// <summary>
        /// git rm --cached 를 실행하여 파일/디렉토리의 Git 추적을 해제
        /// 로컬 파일은 삭제하지 않고 Git index에서만 제거
        /// </summary>
        private static void ExecuteGitUntrack(string path, bool isDirectory)
        {
            string args = isDirectory
                ? $"rm -r --cached --quiet \"{path}\""
                : $"rm --cached --quiet \"{path}\"";

            try
            {
                EditorUtility.DisplayProgressBar("Git 추적 해제", $"{path} 처리 중...", 0.3f);

                var result = RunGit(args, 300000);

                if (result == null)
                {
                    Debug.LogError("[AIT] Git 명령 실행 실패");
                    ShowManualUntrackHelp(path, isDirectory);
                    return;
                }

                if (result.Value.exitCode == 0)
                {
                    Debug.Log($"[AIT] Git 추적 해제 완료: {path}");

                    EditorUtility.DisplayProgressBar("Git 추적 해제", "커밋 중...", 0.7f);

                    // 자동 커밋 (staged 변경사항만 — git rm --cached)
                    // .gitignore는 이미 CheckAndUpdateGitIgnore 단계에서 커밋 완료
                    var commitResult = RunGit($"commit --quiet -m \"정리: {path} Git 추적 해제 (AIT SDK 자동)\"", 300000);
                    if (commitResult != null && commitResult.Value.exitCode == 0)
                    {
                        Debug.Log($"[AIT] 자동 커밋 완료: untrack {path}");
                        AITPlatformHelper.ShowInfoDialog(
                            "완료",
                            $"{path} 의 Git 추적이 해제되고 자동 커밋되었습니다.\n\n" +
                            "(로컬 파일은 삭제되지 않습니다)",
                            "확인"
                        );
                    }
                    else
                    {
                        string commitError = commitResult != null
                            ? (commitResult.Value.stderr.Trim() + " " + commitResult.Value.stdout.Trim()).Trim()
                            : "Git 프로세스를 시작할 수 없거나 타임아웃이 발생했습니다.";
                        Debug.LogError($"[AIT] untrack 자동 커밋 실패: {commitError}");
                        AITPlatformHelper.ShowInfoDialog(
                            "자동 커밋 실패",
                            $"{path} 의 Git 추적은 해제되었으나 자동 커밋에 실패했습니다.\n\n" +
                            $"원인: {commitError}\n\n" +
                            "수동으로 커밋해주세요:\n" +
                            $"   git commit -m \"정리: {path} Git 추적 해제\"\n\n" +
                            "(로컬 파일은 삭제되지 않습니다)",
                            "확인"
                        );
                    }
                }
                else
                {
                    Debug.LogError($"[AIT] Git 추적 해제 실패: {result.Value.stderr}");
                    ShowManualUntrackHelp(path, isDirectory);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>
        /// 자동 untrack 실패 시 수동 해제 안내
        /// </summary>
        private static void ShowManualUntrackHelp(string path, bool isDirectory)
        {
            string command = isDirectory
                ? $"git rm -r --cached {path}"
                : $"git rm --cached {path}";

            GUIUtility.systemCopyBuffer = command;

            AITPlatformHelper.ShowInfoDialog(
                "수동 해제 필요",
                $"자동 해제에 실패했습니다.\n\n" +
                "터미널에서 다음 명령어를 실행해주세요:\n" +
                $"   {command}\n\n" +
                "(명령어가 클립보드에 복사되었습니다)",
                "확인"
            );
        }

        /// <summary>
        /// .gitignore 체크 세션 상태 초기화 (통합 리셋용)
        /// </summary>
        public static void ResetSessionState()
        {
            SessionState.SetBool(PREFS_KEY_GITIGNORE_CHECKED, false);
        }
    }
}
