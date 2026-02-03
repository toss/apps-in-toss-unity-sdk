using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// SDK 패키지 자동 업데이트 체크
    /// Unity Editor 시작 시 git 원격 커밋을 확인하여, 새로운 커밋이 있으면 사용자에게 알림을 표시합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class AITAutoUpdater
    {
        private const string SESSION_KEY = "AIT_AutoUpdate_Checked_v1";
        private const string DAILY_CHECK_KEY_PREFIX = "AIT_AutoUpdate_LastCheckDate_";
        private const int GIT_TIMEOUT_MS = 10000;

        /// <summary>
        /// 프로젝트별 고유 키 생성 (프로젝트 경로 해시 사용)
        /// </summary>
        private static string GetDailyCheckKey()
        {
            string projectPath = Application.dataPath;
            int hash = projectPath.GetHashCode();
            return $"{DAILY_CHECK_KEY_PREFIX}{hash:X8}";
        }

        static AITAutoUpdater()
        {
            EditorApplication.delayCall += OnEditorReady;
        }

        private static void OnEditorReady()
        {
            // Play 모드 진입 시 스킵
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            // CI 환경 스킵
            if (Application.isBatchMode)
            {
                return;
            }

            // 세션당 1회만 실행
            if (SessionState.GetBool(SESSION_KEY, false))
            {
                return;
            }

            SessionState.SetBool(SESSION_KEY, true);

            // 하루에 1번만 체크
            if (!ShouldCheckToday())
            {
                return;
            }

            try
            {
                CheckForUpdate();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] SDK 자동 업데이트 체크 중 예외 발생 (무시됨): {e.Message}");
            }
        }

        /// <summary>
        /// 오늘 이미 체크했는지 확인
        /// </summary>
        private static bool ShouldCheckToday()
        {
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            string key = GetDailyCheckKey();
            string lastCheckDate = EditorPrefs.GetString(key, "");

            if (lastCheckDate == today)
            {
                return false;
            }

            EditorPrefs.SetString(key, today);
            return true;
        }

        private static void CheckForUpdate(bool isManualCheck = false)
        {
            // 1. 현재 패키지 정보 수집
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                "Packages/im.toss.apps-in-toss-unity-sdk"
            );

            if (packageInfo == null)
            {
                if (isManualCheck)
                {
                    Debug.Log("[AIT] SDK 패키지를 찾을 수 없습니다.");
                }
                return;
            }

            if (packageInfo.source != UnityEditor.PackageManager.PackageSource.Git)
            {
                if (isManualCheck)
                {
                    Debug.Log("[AIT] Git 패키지가 아니므로 자동 업데이트 체크를 건너뜁니다.");
                }
                return;
            }

            // packageId에서 git URL과 fragment 파싱
            // 형식: im.toss.apps-in-toss-unity-sdk@https://github.com/...#fragment
            string packageId = packageInfo.packageId;
            if (!TryParseGitPackageId(packageId, out string gitUrl, out string fragment))
            {
                if (isManualCheck)
                {
                    Debug.Log("[AIT] 패키지 ID 파싱에 실패했습니다.");
                }
                return;
            }

            // fragment가 없으면 스킵
            if (string.IsNullOrEmpty(fragment))
            {
                Debug.Log("[AIT] Git fragment(브랜치/태그)가 지정되지 않아 자동 업데이트 체크를 건너뜁니다.");
                return;
            }

            // fragment가 40자 hex(커밋 SHA 직접 지정)이면 스킵
            if (Regex.IsMatch(fragment, "^[0-9a-fA-F]{40}$"))
            {
                Debug.Log("[AIT] 커밋 SHA가 직접 지정되어 있어 자동 업데이트 체크를 건너뜁니다.");
                return;
            }

            // 2. 설치된 커밋 해시 가져오기
            string installedHash = GetInstalledCommitHash(packageInfo);
            if (string.IsNullOrEmpty(installedHash))
            {
                if (isManualCheck)
                {
                    Debug.Log("[AIT] 설치된 커밋 해시를 확인할 수 없습니다.");
                }
                return;
            }

            Debug.Log("[AIT] SDK 자동 업데이트 체크 중...");

            // 3. 백그라운드 스레드에서 원격 커밋 해시 확인
            string capturedGitUrl = gitUrl;
            string capturedFragment = fragment;
            string capturedInstalledHash = installedHash;
            string capturedPackageId = packageId;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string remoteHash = GetRemoteCommitHash(capturedGitUrl, capturedFragment);

                    EditorApplication.delayCall += () =>
                    {
                        try
                        {
                            OnRemoteHashResolved(
                                remoteHash,
                                capturedInstalledHash,
                                capturedPackageId,
                                capturedGitUrl,
                                capturedFragment
                            );
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning(
                                $"[AIT] SDK 업데이트 적용 중 예외 발생 (무시됨): {e.Message}"
                            );
                        }
                    };
                }
                catch (Exception e)
                {
                    EditorApplication.delayCall += () =>
                    {
                        Debug.LogWarning(
                            $"[AIT] SDK 원격 버전 확인 실패 (무시됨): {e.Message}"
                        );
                    };
                }
            });
        }

        /// <summary>
        /// packageId에서 git URL과 fragment를 파싱
        /// </summary>
        private static bool TryParseGitPackageId(
            string packageId,
            out string gitUrl,
            out string fragment
        )
        {
            gitUrl = null;
            fragment = null;

            // packageId 형식: name@url#fragment 또는 name@url
            int atIndex = packageId.IndexOf('@');
            if (atIndex < 0)
            {
                return false;
            }

            string urlPart = packageId.Substring(atIndex + 1);

            int hashIndex = urlPart.LastIndexOf('#');
            if (hashIndex >= 0)
            {
                gitUrl = urlPart.Substring(0, hashIndex);
                fragment = urlPart.Substring(hashIndex + 1);

                // ?path= 파라미터가 fragment에 포함된 경우 제거
                int queryIndex = fragment.IndexOf('?');
                if (queryIndex >= 0)
                {
                    fragment = fragment.Substring(0, queryIndex);
                }
            }
            else
            {
                // ?path= 파라미터가 URL에 포함된 경우 제거
                int queryIndex = urlPart.IndexOf('?');
                if (queryIndex >= 0)
                {
                    gitUrl = urlPart.Substring(0, queryIndex);
                }
                else
                {
                    gitUrl = urlPart;
                }
                fragment = null;
            }

            return !string.IsNullOrEmpty(gitUrl);
        }

        /// <summary>
        /// 설치된 패키지의 커밋 해시를 가져옴
        /// </summary>
        private static string GetInstalledCommitHash(
            UnityEditor.PackageManager.PackageInfo packageInfo
        )
        {
#if UNITY_2022_1_OR_NEWER
            if (packageInfo.git != null && !string.IsNullOrEmpty(packageInfo.git.hash))
            {
                return packageInfo.git.hash;
            }
#endif
            // Unity 2021.3 폴백: packages-lock.json 파싱
            return GetHashFromPackagesLock(packageInfo.name);
        }

        /// <summary>
        /// packages-lock.json에서 패키지의 hash 필드를 추출
        /// </summary>
        private static string GetHashFromPackagesLock(string packageName)
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string lockFilePath = Path.Combine(projectRoot, "Packages", "packages-lock.json");

                if (!File.Exists(lockFilePath))
                {
                    return null;
                }

                string content = File.ReadAllText(lockFilePath);

                // 간단한 문자열 파싱으로 hash 추출 (JSON 라이브러리 의존성 없이)
                // "패키지명": { ... "hash": "abc123" ... }
                int pkgIndex = content.IndexOf($"\"{packageName}\"");
                if (pkgIndex < 0)
                {
                    return null;
                }

                // 패키지 블록 내에서 hash 필드 찾기
                int hashIndex = content.IndexOf("\"hash\"", pkgIndex);
                if (hashIndex < 0)
                {
                    return null;
                }

                // 다음 패키지 블록 시작 전인지 확인 (안전 범위 제한)
                int nextPkgIndex = content.IndexOf("\n    }", pkgIndex);
                if (nextPkgIndex >= 0 && hashIndex > nextPkgIndex)
                {
                    return null;
                }

                // "hash": "값" 에서 값 추출
                int colonIndex = content.IndexOf(':', hashIndex);
                if (colonIndex < 0)
                {
                    return null;
                }

                int quoteStart = content.IndexOf('"', colonIndex + 1);
                if (quoteStart < 0)
                {
                    return null;
                }

                int quoteEnd = content.IndexOf('"', quoteStart + 1);
                if (quoteEnd < 0)
                {
                    return null;
                }

                string hash = content.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                return string.IsNullOrEmpty(hash) ? null : hash;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// git ls-remote로 원격 커밋 해시를 가져옴
        /// </summary>
        private static string GetRemoteCommitHash(string gitUrl, string fragment)
        {
            // annotated tag의 실제 커밋 해시 시도 (^{})
            string hash = RunGitLsRemote(gitUrl, $"refs/tags/{fragment}^{{}}");
            if (!string.IsNullOrEmpty(hash))
            {
                return hash;
            }

            // 일반 태그 시도
            hash = RunGitLsRemote(gitUrl, $"refs/tags/{fragment}");
            if (!string.IsNullOrEmpty(hash))
            {
                return hash;
            }

            // 브랜치 시도
            hash = RunGitLsRemote(gitUrl, $"refs/heads/{fragment}");
            if (!string.IsNullOrEmpty(hash))
            {
                return hash;
            }

            return null;
        }

        /// <summary>
        /// git ls-remote 실행하여 refSpec에 대한 커밋 해시 반환
        /// </summary>
        private static string RunGitLsRemote(string gitUrl, string refSpec)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git",
                    Arguments = $"ls-remote \"{gitUrl}\" \"{refSpec}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                // 인증 프롬프트 차단 (백그라운드 실행 시 무한 대기 방지)
                psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

                using (var process = Process.Start(psi))
                {
                    // stderr를 비동기로 읽어 파이프 버퍼 데드락 방지
                    // (stdout은 ls-remote 출력이 1줄이므로 ReadToEnd 안전)
                    process.BeginErrorReadLine();

                    string output = process.StandardOutput.ReadToEnd();

                    if (!process.WaitForExit(GIT_TIMEOUT_MS))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception)
                        {
                            // 무시
                        }
                        return null;
                    }

                    if (process.ExitCode != 0 || string.IsNullOrEmpty(output))
                    {
                        return null;
                    }

                    // 출력 형식: "해시\t참조"
                    string trimmed = output.Trim();
                    int tabIndex = trimmed.IndexOf('\t');
                    if (tabIndex > 0)
                    {
                        return trimmed.Substring(0, tabIndex);
                    }

                    return null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 원격 해시 결과 처리 (메인 스레드에서 호출)
        /// </summary>
        private static void OnRemoteHashResolved(
            string remoteHash,
            string installedHash,
            string packageId,
            string gitUrl,
            string fragment
        )
        {
            if (string.IsNullOrEmpty(remoteHash))
            {
                Debug.LogWarning("[AIT] SDK 원격 버전을 확인할 수 없습니다. 네트워크 연결을 확인하세요.");
                return;
            }

            string shortInstalled = installedHash.Length > 7
                ? installedHash.Substring(0, 7)
                : installedHash;
            string shortRemote = remoteHash.Length > 7
                ? remoteHash.Substring(0, 7)
                : remoteHash;

            if (string.Equals(remoteHash, installedHash, StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log("[AIT] SDK가 최신 상태입니다.");
                return;
            }

            Debug.Log($"[AIT] SDK 업데이트 발견: {shortInstalled} → {shortRemote}");

            // 커밋 시간 정보 가져오기 (GitHub API)
            string installedTime = GetCommitTime(gitUrl, installedHash);
            string remoteTime = GetCommitTime(gitUrl, remoteHash);

            // 다이얼로그 메시지 구성
            string installedInfo = string.IsNullOrEmpty(installedTime)
                ? shortInstalled
                : $"{shortInstalled} ({installedTime})";
            string remoteInfo = string.IsNullOrEmpty(remoteTime)
                ? shortRemote
                : $"{shortRemote} ({remoteTime})";

            // 사용자에게 업데이트 확인 다이얼로그 표시
            bool shouldUpdate = EditorUtility.DisplayDialog(
                "Apps in Toss SDK 업데이트",
                $"새로운 SDK 버전이 있습니다.\n\n" +
                $"브랜치: {fragment}\n\n" +
                $"현재: {installedInfo}\n" +
                $"최신: {remoteInfo}\n\n" +
                $"지금 업데이트하시겠습니까?",
                "업데이트",
                "나중에"
            );

            if (shouldUpdate)
            {
                Debug.Log("[AIT] SDK 업데이트를 적용합니다...");

                // 기존 packageId에서 이름 부분 추출하여 동일한 URL+fragment로 Add 호출
                int atIndex = packageId.IndexOf('@');
                if (atIndex < 0)
                {
                    return;
                }

                string addUrl = packageId.Substring(atIndex + 1);
                UnityEditor.PackageManager.Client.Add(addUrl);
            }
            else
            {
                Debug.Log("[AIT] SDK 업데이트를 건너뛰었습니다.");
            }
        }

        /// <summary>
        /// GitHub API를 통해 커밋 시간 가져오기
        /// </summary>
        private static string GetCommitTime(string gitUrl, string commitHash)
        {
            try
            {
                // git URL에서 owner/repo 추출
                // 예: https://github.com/toss/apps-in-toss-unity-sdk.git -> toss/apps-in-toss-unity-sdk
                var match = Regex.Match(gitUrl, @"github\.com[/:]([^/]+)/([^/]+?)(?:\.git)?$");
                if (!match.Success)
                {
                    return null;
                }

                string owner = match.Groups[1].Value;
                string repo = match.Groups[2].Value;

                // GitHub API 호출
                string apiUrl = $"https://api.github.com/repos/{owner}/{repo}/commits/{commitHash}";

                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.Add("User-Agent", "Unity-AIT-SDK");
                    client.Timeout = TimeSpan.FromSeconds(5);

                    var response = client.GetStringAsync(apiUrl).Result;

                    // 간단한 JSON 파싱으로 committer.date 추출
                    // "committer":{"name":"...","email":"...","date":"2026-02-02T12:34:56Z"}
                    var dateMatch = Regex.Match(response, @"""committer""[^}]*""date""\s*:\s*""([^""]+)""");
                    if (dateMatch.Success)
                    {
                        string isoDate = dateMatch.Groups[1].Value;
                        if (DateTime.TryParse(isoDate, out DateTime dt))
                        {
                            return dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                        }
                    }
                }
            }
            catch (Exception)
            {
                // API 호출 실패 시 무시
            }

            return null;
        }

        /// <summary>
        /// 수동으로 업데이트 체크 강제 실행 (디버그용)
        /// </summary>
        [MenuItem("AIT/Debug/Force Auto-Update Check")]
        public static void ForceAutoUpdateCheck()
        {
            // 세션 상태 초기화 후 체크 실행 (수동 체크는 daily 제한 무시)
            SessionState.SetBool(SESSION_KEY, false);

            try
            {
                CheckForUpdate(isManualCheck: true);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] SDK 자동 업데이트 체크 중 예외 발생: {e.Message}");
            }
        }

        /// <summary>
        /// 일일 체크 상태 초기화 (디버그용)
        /// </summary>
        [MenuItem("AIT/Debug/Reset Daily Update Check")]
        public static void ResetDailyCheck()
        {
            EditorPrefs.DeleteKey(GetDailyCheckKey());
            SessionState.SetBool(SESSION_KEY, false);
            Debug.Log("[AIT] 일일 업데이트 체크 상태가 초기화되었습니다.");
        }
    }
}
