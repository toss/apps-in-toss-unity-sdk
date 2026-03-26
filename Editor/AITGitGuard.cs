using UnityEngine;
using UnityEditor;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    // ============================================================
    // 데이터 모델
    // ============================================================

    public enum GitGuardIssueType { MissingGitignore, TrackedFile }

    public class GitGuardIssue
    {
        public GitGuardIssueType type;
        public string label;
        public string description;
        public bool isSelected = true;
        // MissingGitignore용
        public List<string> missingPatterns;
        // TrackedFile용
        public string path;
        public bool isDirectory;
    }

    public class GitGuardFixResult
    {
        public string label;
        public bool success;
        public string message;
        /// <summary>실패 시 사용자가 터미널에 붙여넣을 수 있는 명령어 (없으면 null)</summary>
        public string command;
    }

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
            // SDK가 빌드 시 자동 복사하는 템플릿 (원본: Packages/*/WebGLTemplates/)
            "Assets/WebGLTemplates/",
            // 빌드 시 자동 생성되는 빌드 정보
            "Assets/StreamingAssets/build_info",
            "Assets/StreamingAssets/build_info.meta",
        };

        /// <summary>
        /// tracked 여부를 감지하여 자동 untrack할 대상 목록
        /// </summary>
        private static readonly (string path, bool isDirectory)[] TRACKED_CHECK_TARGETS = new[]
        {
            ("Assets/AppsInToss/Editor/AITCredentials.asset", false),
            ("ait-build/", true),
            ("webgl/", true),
            ("Assets/WebGLTemplates/", true),
            ("Assets/StreamingAssets/build_info", false),
            ("Assets/StreamingAssets/build_info.meta", false),
        };

        private const string PREFS_KEY_GITIGNORE_CHECKED = "AIT_GitIgnore_Checked_v5";

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

            var issues = DetectAllIssues();
            if (issues.Count == 0)
            {
                return;
            }

            if (AITPlatformHelper.IsNonInteractive)
            {
                // CI/batch mode — 자동 실행 (DetectAllIssues()는 isSelected=true 기본값으로 생성)
                Debug.Log($"[AIT/CI] GitGuard: {issues.Count}개 이슈 자동 수정");
                var results = ExecuteBatchFix(issues);
                foreach (var r in results)
                {
                    if (r.success)
                        Debug.Log($"[AIT/CI] ✅ {r.label}");
                    else
                        Debug.LogError($"[AIT/CI] ❌ {r.label}: {r.message}");
                }
            }
            else
            {
                AITGitGuardWindow.Show(issues);
            }
        }

        // ============================================================
        // 감지 로직
        // ============================================================

        /// <summary>
        /// 모든 이슈를 감지하여 리스트로 반환
        /// </summary>
        public static List<GitGuardIssue> DetectAllIssues()
        {
            var issues = new List<GitGuardIssue>();

            // 1. .gitignore 누락 패턴 감지
            var missingPatterns = DetectMissingGitignorePatterns();
            if (missingPatterns.Count > 0)
            {
                issues.Add(new GitGuardIssue
                {
                    type = GitGuardIssueType.MissingGitignore,
                    label = $".gitignore 보호 패턴 추가 ({missingPatterns.Count}개)",
                    description = string.Join("\n", missingPatterns.ConvertAll(p => $"• {p}")),
                    missingPatterns = missingPatterns,
                });
            }

            // 2. tracked 파일/디렉토리 감지
            string projectRoot = GetProjectRoot();
            foreach (var (path, isDirectory) in TRACKED_CHECK_TARGETS)
            {
                string fullPath = Path.Combine(projectRoot, path.TrimEnd('/'));
                bool exists = isDirectory ? Directory.Exists(fullPath) : File.Exists(fullPath);
                if (!exists) continue;

                var result = RunGit($"ls-files --error-unmatch \"{path.TrimEnd('/')}\"");
                if (result == null || result.Value.exitCode != 0) continue;

                bool isCredentials = path.Contains("AITCredentials");
                issues.Add(new GitGuardIssue
                {
                    type = GitGuardIssueType.TrackedFile,
                    label = isCredentials
                        ? "⚠️ AITCredentials.asset 추적 해제 (배포 키 노출)"
                        : $"{path} 추적 해제",
                    description = isCredentials
                        ? "배포 키가 저장소에 노출될 수 있습니다."
                        : ".gitignore에 패턴이 있더라도 이미 tracked된 파일은 무시되지 않습니다.",
                    path = path,
                    isDirectory = isDirectory,
                });
            }

            return issues;
        }

        /// <summary>
        /// .gitignore에서 누락된 패턴 목록 반환
        /// </summary>
        private static List<string> DetectMissingGitignorePatterns()
        {
            string projectRoot = GetProjectRoot();
            string gitignorePath = Path.Combine(projectRoot, ".gitignore");
            string existingContent = File.Exists(gitignorePath) ? File.ReadAllText(gitignorePath) : "";

            var missing = new List<string>();
            foreach (string pattern in GITIGNORE_PATTERNS)
            {
                string patternKey = pattern.TrimStart('*', '/');
                if (!ContainsPattern(existingContent, patternKey))
                {
                    missing.Add(pattern);
                }
            }
            return missing;
        }

        // ============================================================
        // 일괄 수정 로직
        // ============================================================

        /// <summary>
        /// 선택된 이슈들을 일괄 처리하고 결과를 반환
        /// </summary>
        public static List<GitGuardFixResult> ExecuteBatchFix(List<GitGuardIssue> selected)
        {
            var results = new List<GitGuardFixResult>();
            bool anyStaged = false;

            foreach (var issue in selected)
            {
                if (!issue.isSelected) continue;

                if (issue.type == GitGuardIssueType.MissingGitignore)
                {
                    var result = FixMissingGitignore(issue.missingPatterns);
                    results.Add(result);
                    if (result.success) anyStaged = true;
                }
                else if (issue.type == GitGuardIssueType.TrackedFile)
                {
                    var result = FixTrackedFile(issue.path, issue.isDirectory);
                    results.Add(result);
                    if (result.success) anyStaged = true;
                }
            }

            // 변경사항이 있으면 커밋 안내
            if (anyStaged)
            {
                if (AITPlatformHelper.IsNonInteractive)
                {
                    // CI/batch mode — 커밋은 사용자에게 맡김
                    Debug.LogWarning("[AIT] GitGuard: .gitignore 및 untrack 변경이 staged되었습니다. 필요 시 직접 커밋해주세요: git commit -m \"정리: Git 보호 설정\"");
                }
                else
                {
                    var summaryParts = new List<string>();
                    foreach (var r in results)
                    {
                        if (r.success) summaryParts.Add(r.label);
                    }
                    string summary = string.Join(", ", summaryParts);
                    if (summary.Length > 60)
                    {
                        // 마지막 ", " 경계에서 잘라 한국어 문자 중간 잘림 방지
                        int cutoff = summary.LastIndexOf(", ", 57, System.StringComparison.Ordinal);
                        summary = (cutoff > 0 ? summary.Substring(0, cutoff) : summary.Substring(0, 57)) + " ...";
                    }

                    var commitResult = RunGit($"commit --quiet -m \"정리: {summary} (AIT SDK 자동)\"", 300000);
                    if (commitResult == null || commitResult.Value.exitCode != 0)
                    {
                        string commitError = commitResult != null
                            ? (commitResult.Value.stderr.Trim() + " " + commitResult.Value.stdout.Trim()).Trim()
                            : "Git 프로세스를 시작할 수 없거나 타임아웃이 발생했습니다.";
                        Debug.LogError($"[AIT] 자동 커밋 실패: {commitError}");
                        results.Add(new GitGuardFixResult
                        {
                            label = "자동 커밋",
                            success = false,
                            message = commitError,
                            command = "git commit -m \"정리: Git 보호 설정\"",
                        });
                    }
                    else
                    {
                        Debug.Log("[AIT] GitGuard 자동 커밋 완료");
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// .gitignore에 누락된 패턴 추가 + git add
        /// </summary>
        private static GitGuardFixResult FixMissingGitignore(List<string> missingPatterns)
        {
            string label = ".gitignore 보호 패턴 추가";
            try
            {
                string projectRoot = GetProjectRoot();
                string gitignorePath = Path.Combine(projectRoot, ".gitignore");

                string patternsToAdd = "\n# Apps in Toss SDK - 자동 보호 패턴\n";
                foreach (string pattern in missingPatterns)
                {
                    patternsToAdd += pattern + "\n";
                }
                File.AppendAllText(gitignorePath, patternsToAdd);

                var addResult = RunGit("add .gitignore", 5000);
                if (addResult == null || addResult.Value.exitCode != 0)
                {
                    string err = addResult != null ? addResult.Value.stderr.Trim() : "Git 실행 실패";
                    return new GitGuardFixResult
                    {
                        label = label,
                        success = false,
                        message = $"패턴은 .gitignore에 추가되었으나 git add 실패: {err}",
                        command = "git add .gitignore",
                    };
                }

                Debug.Log("[AIT] .gitignore에 보호 패턴 추가됨: " + string.Join(", ", missingPatterns));
                return new GitGuardFixResult { label = label, success = true };
            }
            catch (System.Exception ex)
            {
                return new GitGuardFixResult { label = label, success = false, message = ex.Message };
            }
        }

        /// <summary>
        /// tracked 파일/디렉토리를 git rm --cached
        /// </summary>
        private static GitGuardFixResult FixTrackedFile(string path, bool isDirectory)
        {
            string label = $"{path} 추적 해제";
            string args = isDirectory
                ? $"rm -r --cached --quiet \"{path}\""
                : $"rm --cached --quiet \"{path}\"";

            string cmd = isDirectory ? $"git rm -r --cached {path}" : $"git rm --cached {path}";

            var result = RunGit(args, 300000);
            if (result == null)
            {
                return new GitGuardFixResult { label = label, success = false, message = "Git 명령 실행 실패", command = cmd };
            }

            if (result.Value.exitCode == 0)
            {
                Debug.Log($"[AIT] Git 추적 해제 완료: {path}");
                return new GitGuardFixResult { label = label, success = true };
            }
            else
            {
                return new GitGuardFixResult { label = label, success = false, message = result.Value.stderr.Trim(), command = cmd };
            }
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

        /// <summary>
        /// 문자열에 특정 패턴이 포함되어 있는지 확인
        /// </summary>
        private static bool ContainsPattern(string content, string pattern)
        {
            string[] lines = content.Split('\n');
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                    continue;

                if (trimmed.Contains(pattern))
                    return true;

                // 상위 디렉토리가 이미 무시되고 있는지 확인
                {
                    string trimmedLine = trimmed.TrimEnd('/');
                    string patternWithoutSlash = pattern.TrimEnd('/');
                    if (patternWithoutSlash.StartsWith(trimmedLine) &&
                        (patternWithoutSlash.Length == trimmedLine.Length ||
                         patternWithoutSlash[trimmedLine.Length] == '/'))
                    {
                        return true;
                    }
                }
            }
            return false;
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
