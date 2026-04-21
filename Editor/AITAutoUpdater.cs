using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
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
                Debug.LogWarning($"[AIT] SDK 자동 업데이트 체크 중 예외 발생 (무시됨): {e}");
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
            var packageInfo = AITPackagePathResolver.FindSDKPackageInfo();

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

            bool capturedIsManualCheck = isManualCheck;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string remoteHash = GetRemoteCommitHash(capturedGitUrl, capturedFragment);

                    EditorApplication.delayCall += async () =>
                    {
                        try
                        {
                            await OnRemoteHashResolved(
                                remoteHash,
                                capturedInstalledHash,
                                capturedPackageId,
                                capturedGitUrl,
                                capturedFragment,
                                capturedIsManualCheck
                            );
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning(
                                $"[AIT] SDK 업데이트 적용 중 예외 발생 (무시됨): {e}"
                            );
                        }
                    };
                }
                catch (Exception e)
                {
                    EditorApplication.delayCall += () =>
                    {
                        Debug.LogWarning(
                            $"[AIT] SDK 원격 버전 확인 실패 (무시됨): {e}"
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
            return AITVersion.GetHashFromPackagesLock(packageInfo.name);
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
        private static async Task OnRemoteHashResolved(
            string remoteHash,
            string installedHash,
            string packageId,
            string gitUrl,
            string fragment,
            bool isManualCheck = false
        )
        {
            if (string.IsNullOrEmpty(remoteHash))
            {
                Debug.LogWarning("[AIT] SDK 원격 버전을 확인할 수 없습니다. 네트워크 연결을 확인하세요.");
                if (isManualCheck)
                {
                    EditorUtility.DisplayDialog(
                        "Apps in Toss SDK",
                        "원격 버전을 확인할 수 없습니다.\n네트워크 연결을 확인하세요.",
                        "확인"
                    );
                }
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
                if (isManualCheck)
                {
                    EditorUtility.DisplayDialog(
                        "Apps in Toss SDK",
                        "SDK가 최신 상태입니다.",
                        "확인"
                    );
                }
                return;
            }

            Debug.Log($"[AIT] SDK 업데이트 발견: {shortInstalled} → {shortRemote}");

            // 커밋 시간 정보 가져오기 (GitHub API)
            string installedTime = await GetCommitTime(gitUrl, installedHash);
            string remoteTime = await GetCommitTime(gitUrl, remoteHash);

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

                // 기존 packageId에서 이름과 URL 부분 추출
                int atIndex = packageId.IndexOf('@');
                if (atIndex < 0)
                {
                    return;
                }

                string packageName = packageId.Substring(0, atIndex);
                string addUrl = packageId.Substring(atIndex + 1);

                // manifest.json 직접 수정 방식 사용
                // PackageManager.Client.Add()는 기존 immutable 패키지를 교체하는 과정에서
                // 'immutable packages were unexpectedly altered' 경고를 유발할 수 있음.
                // manifest.json을 직접 수정하면 Unity가 패키지를 처음부터 다시 resolve하여
                // 이 경고를 방지할 수 있음.
                if (!UpdateManifestAndResolve(packageName, addUrl))
                {
                    // manifest.json 수정 실패 시 기존 방식으로 폴백
                    Debug.LogWarning("[AIT] manifest.json 수정에 실패하여 기존 방식으로 업데이트합니다.");
                    UnityEditor.PackageManager.Client.Add(addUrl);
                }
            }
            else
            {
                Debug.Log("[AIT] SDK 업데이트를 건너뛰었습니다.");
            }
        }

        /// <summary>
        /// GitHub API를 통해 커밋 시간 가져오기
        /// </summary>
        private static async Task<string> GetCommitTime(string gitUrl, string commitHash)
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

                    // await 사용: 메인 스레드에서 .Result로 블로킹하면
                    // Unity SynchronizationContext와 상호작용해 데드락 가능.
                    string response = await client.GetStringAsync(apiUrl);

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
        /// 수동으로 업데이트 체크 (메뉴)
        /// </summary>
        [MenuItem("AIT/Check for Updates...", false, 301)]
        public static void MenuCheckForUpdates()
        {
            ForceAutoUpdateCheck();
        }

        /// <summary>
        /// 수동으로 업데이트 체크 강제 실행 (디버그용)
        /// </summary>
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
                Debug.LogWarning($"[AIT] SDK 자동 업데이트 체크 중 예외 발생: {e}");
            }
        }

        /// <summary>
        /// 일일 체크 상태 초기화 (디버그용)
        /// </summary>
        public static void ResetDailyCheck()
        {
            EditorPrefs.DeleteKey(GetDailyCheckKey());
            SessionState.SetBool(SESSION_KEY, false);
            Debug.Log("[AIT] 일일 업데이트 체크 상태가 초기화되었습니다.");
        }

        /// <summary>
        /// manifest.json을 직접 수정하여 패키지 업데이트를 트리거합니다.
        /// PackageManager.Client.Add()와 달리 immutable 패키지 변경 경고가 발생하지 않습니다.
        /// </summary>
        private static bool UpdateManifestAndResolve(string packageName, string newUrl)
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string manifestPath = Path.Combine(projectRoot, "Packages", "manifest.json");

                if (!File.Exists(manifestPath))
                {
                    Debug.LogWarning("[AIT] manifest.json을 찾을 수 없습니다.");
                    return false;
                }

                var utf8NoBom = new System.Text.UTF8Encoding(false);
                string content = File.ReadAllText(manifestPath, utf8NoBom);

                // manifest.json에서 패키지 항목 교체
                // 형식: "패키지이름": "git URL"
                string escapedName = Regex.Escape(packageName);
                var pattern = new Regex(
                    $@"(""{escapedName}""\s*:\s*"")([^""]*)("")",
                    RegexOptions.None
                );

                // JSON 값 내에서 안전하도록 이스케이프 (일반적인 git URL에는 해당하지 않지만 방어적 처리)
                string escapedUrl = newUrl
                    .Replace("\\", "\\\\")
                    .Replace("\"", "\\\"")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\r")
                    .Replace("\t", "\\t");

                bool matched = false;
                string newContent = pattern.Replace(content, m =>
                {
                    matched = true;
                    return m.Groups[1].Value + escapedUrl + m.Groups[3].Value;
                }, 1);

                if (!matched)
                {
                    Debug.LogWarning($"[AIT] manifest.json에서 패키지 '{packageName}'을 찾을 수 없습니다.");
                    return false;
                }

                if (newContent != content)
                {
                    File.WriteAllText(manifestPath, newContent, utf8NoBom);
                    Debug.Log("[AIT] manifest.json을 업데이트했습니다. 패키지를 다시 resolve합니다...");
                    UnityEditor.PackageManager.Client.Resolve();
                }
                else
                {
                    // 브랜치/태그 기반 참조는 URL이 동일하지만 원격 커밋이 변경된 경우.
                    // Client.Resolve()만으로는 캐시된 패키지를 re-fetch하지 않을 수 있으므로
                    // 이 경우에는 manifest.json 방식의 이점이 없음 — false 반환하여
                    // 폴백(Client.Add)을 사용하도록 함.
                    Debug.Log("[AIT] manifest.json URL이 동일합니다. 기존 방식으로 업데이트합니다.");
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] manifest.json 업데이트 중 오류 발생: {e.Message}");
                return false;
            }
        }
    }
}
