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
    /// </summary>
    [InitializeOnLoad]
    public static class AITCredentialsGuard
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
        };

        private const string PREFS_KEY_GITIGNORE_CHECKED = "AIT_GitIgnore_Checked_v2";

        static AITCredentialsGuard()
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

            // 이미 tracked된 파일인지 체크
            CheckIfCredentialsTracked();
        }

        /// <summary>
        /// 현재 프로젝트가 Git 저장소인지 확인
        /// </summary>
        private static bool IsGitRepository()
        {
            string projectRoot = GetProjectRoot();
            string gitDir = Path.Combine(projectRoot, ".git");
            return Directory.Exists(gitDir);
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

                AITPlatformHelper.ShowInfoDialog(
                    "완료",
                    ".gitignore에 보호 패턴이 추가되었습니다.\n\n" +
                    "민감 정보와 빌드 결과물이 Git에 커밋되지 않습니다.",
                    "확인"
                );
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

        /// <summary>
        /// AITCredentials.asset이 이미 Git에서 tracked되고 있는지 확인
        /// </summary>
        private static void CheckIfCredentialsTracked()
        {
            string projectRoot = GetProjectRoot();
            string credentialsPath = "Assets/AppsInToss/Editor/AITCredentials.asset";
            string fullPath = Path.Combine(projectRoot, credentialsPath);

            // 파일이 존재하지 않으면 체크 불필요
            if (!File.Exists(fullPath))
            {
                return;
            }

            try
            {
                // git ls-files로 tracked 여부 확인
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"ls-files --error-unmatch \"{credentialsPath}\"",
                    WorkingDirectory = projectRoot,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    process.WaitForExit(5000);

                    // exit code 0 = tracked, 1 = not tracked
                    if (process.ExitCode == 0)
                    {
                        ShowTrackedWarning();
                    }
                }
            }
            catch (System.Exception)
            {
                // git 명령어 실패 시 무시 (git이 설치되지 않았을 수 있음)
            }
        }

        /// <summary>
        /// AITCredentials.asset이 이미 tracked된 경우 경고 표시
        /// </summary>
        private static void ShowTrackedWarning()
        {
            // CI에서는 경고 로그만 출력하고 클립보드 복사는 스킵
            bool shouldFix = AITPlatformHelper.ShowConfirmDialog(
                "⚠️ 경고: 배포 키가 Git에 노출되어 있습니다!",
                "AITCredentials.asset 파일이 이미 Git에 커밋되어 있습니다.\n\n" +
                "배포 키가 저장소에 노출될 수 있으므로, Git에서 제거하는 것을 권장합니다.\n\n" +
                "제거 방법:\n" +
                "1. 터미널에서 다음 명령어 실행:\n" +
                "   git rm --cached Assets/AppsInToss/Editor/AITCredentials.asset\n" +
                "2. 변경사항 커밋\n\n" +
                "클립보드에 명령어를 복사하시겠습니까?",
                "명령어 복사",
                "나중에",
                autoApprove: false  // CI에서는 자동 스킵 (클립보드 복사 무의미)
            );

            if (shouldFix)
            {
                GUIUtility.systemCopyBuffer = "git rm --cached Assets/AppsInToss/Editor/AITCredentials.asset";
                Debug.Log("[AIT] Git untrack 명령어가 클립보드에 복사되었습니다.");
            }
        }
    }
}
