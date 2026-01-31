using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// SDK 패키지 자동 업데이트 체크
    /// Unity Editor 시작 시 git 원격 커밋을 확인하여, 새로운 커밋이 있으면 자동으로 업데이트합니다.
    /// </summary>
    [InitializeOnLoad]
    public static class AITAutoUpdater
    {
        private const string SESSION_KEY = "AIT_AutoUpdate_Checked_v1";
        private const int GIT_TIMEOUT_MS = 10000;

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

            try
            {
                CheckForUpdate();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] SDK 자동 업데이트 체크 중 예외 발생 (무시됨): {e.Message}");
            }
        }

        private static void CheckForUpdate()
        {
            // 1. 현재 패키지 정보 수집
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                "Packages/im.toss.apps-in-toss-unity-sdk"
            );

            if (packageInfo == null)
            {
                return;
            }

            if (packageInfo.source != UnityEditor.PackageManager.PackageSource.Git)
            {
                return;
            }

            // packageId에서 git URL과 fragment 파싱
            // 형식: im.toss.apps-in-toss-unity-sdk@https://github.com/...#fragment
            string packageId = packageInfo.packageId;
            if (!TryParseGitPackageId(packageId, out string gitUrl, out string fragment))
            {
                return;
            }

            // fragment가 없으면 스킵
            if (string.IsNullOrEmpty(fragment))
            {
                return;
            }

            // fragment가 40자 hex(커밋 SHA 직접 지정)이면 스킵
            if (Regex.IsMatch(fragment, "^[0-9a-fA-F]{40}$"))
            {
                return;
            }

            // 2. 설치된 커밋 해시 가져오기
            string installedHash = GetInstalledCommitHash(packageInfo);
            if (string.IsNullOrEmpty(installedHash))
            {
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

                using (var process = Process.Start(psi))
                {
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

        /// <summary>
        /// 수동으로 업데이트 체크 강제 실행 (디버그용)
        /// </summary>
        [MenuItem("AIT/Debug/Force Auto-Update Check")]
        public static void ForceAutoUpdateCheck()
        {
            // 세션 상태 초기화 후 체크 실행
            SessionState.SetBool(SESSION_KEY, false);

            try
            {
                CheckForUpdate();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT] SDK 자동 업데이트 체크 중 예외 발생: {e.Message}");
            }
        }
    }
}
