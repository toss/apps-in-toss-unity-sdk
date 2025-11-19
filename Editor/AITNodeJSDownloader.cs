using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// Node.js 런타임 자동 다운로드 및 관리 유틸리티
    /// </summary>
    public static class AITNodeJSDownloader
    {
        private const string NODE_VERSION = "24.11.1";

        // SHA256 체크섬 (Node.js 공식 SHASUMS256.txt에서 검증됨)
        // 출처: https://nodejs.org/dist/v24.11.1/SHASUMS256.txt
        private static readonly Dictionary<string, string> Checksums = new Dictionary<string, string>
        {
            ["darwin-arm64"] = "b05aa3a66efe680023f930bd5af3fdbbd542794da5644ca2ad711d68cbd4dc35",
            ["darwin-x64"] = "096081b6d6fcdd3f5ba0f5f1d44a47e83037ad2e78eada26671c252fe64dd111",
            ["win-x64"] = "5355ae6d7c49eddcfde7d34ac3486820600a831bf81dc3bdca5c8db6a9bb0e76",
            ["linux-x64"] = "60e3b0a8500819514aca603487c254298cd776de0698d3cd08f11dba5b8289a8"
        };

        /// <summary>
        /// Embedded Node.js 경로 찾기 (없으면 자동 다운로드)
        /// </summary>
        public static string FindEmbeddedNpm(bool autoDownload = true)
        {
            string platform = GetPlatformFolder();
            if (string.IsNullOrEmpty(platform))
            {
                Debug.LogError("[NodeJS] 지원하지 않는 플랫폼입니다.");
                return null;
            }

            string packagePath = GetPackagePath();
            string nodePath = Path.Combine(packagePath, "Tools~", "NodeJS", platform);
            string npmPath = GetNpmExecutablePath(nodePath);

            // 이미 존재하면 반환
            if (File.Exists(npmPath))
            {
                Debug.Log($"[NodeJS] Embedded npm 발견: {npmPath}");
                return npmPath;
            }

            // 자동 다운로드
            if (autoDownload)
            {
                Debug.Log($"[NodeJS] Embedded Node.js를 찾을 수 없습니다. 다운로드를 시작합니다...");

                bool download = EditorUtility.DisplayDialog(
                    "Node.js 자동 다운로드",
                    $"Apps in Toss SDK는 빌드를 위해 Node.js {NODE_VERSION} (LTS)가 필요합니다.\n\n" +
                    "SDK에 포함된 portable Node.js를 자동으로 다운로드하시겠습니까?\n\n" +
                    $"다운로드 크기: 약 40-50MB\n" +
                    $"설치 위치: {nodePath}",
                    "다운로드", "취소"
                );

                if (download)
                {
                    DownloadNodeJS(platform, nodePath);

                    // 다운로드 후 재확인
                    if (File.Exists(npmPath))
                    {
                        return npmPath;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Node.js 다운로드 및 설치
        /// </summary>
        private static void DownloadNodeJS(string platform, string targetPath)
        {
            string fileName = GetNodeJSFileName(platform);
            string tempFile = Path.Combine(Application.temporaryCachePath, fileName);

            try
            {
                // 디렉토리 생성
                Directory.CreateDirectory(targetPath);

                // 다운로드 URL 목록 (폴백)
                string[] downloadUrls = GetDownloadUrls(platform, fileName);

                EditorUtility.DisplayProgressBar("Node.js 다운로드",
                    "Node.js 런타임을 다운로드하고 있습니다...", 0.1f);

                // 폴백을 사용하여 다운로드
                bool downloaded = false;
                Exception lastException = null;

                foreach (string url in downloadUrls)
                {
                    try
                    {
                        Debug.Log($"[NodeJS] 다운로드 시도: {url}");
                        DownloadFile(url, tempFile);
                        downloaded = true;
                        break;
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[NodeJS] 다운로드 실패 ({url}): {e.Message}");
                        lastException = e;
                    }
                }

                if (!downloaded)
                {
                    throw new Exception($"모든 다운로드 소스가 실패했습니다: {lastException?.Message}");
                }

                EditorUtility.DisplayProgressBar("Node.js 다운로드",
                    "파일 무결성 검증 중 (SHA256)...", 0.85f);

                // 체크섬 검증 (필수 - 보안)
                if (!Checksums.ContainsKey(platform) || string.IsNullOrEmpty(Checksums[platform]))
                {
                    throw new Exception($"플랫폼 '{platform}'의 체크섬 정보가 없습니다. SDK 업데이트가 필요합니다.");
                }

                if (!VerifyChecksum(tempFile, Checksums[platform]))
                {
                    // 보안상 중요: 체크섬 불일치 시 파일 즉시 삭제
                    File.Delete(tempFile);
                    throw new Exception(
                        "보안 경고: 다운로드한 파일의 체크섬이 일치하지 않습니다.\n\n" +
                        "파일이 변조되었거나 손상되었을 수 있습니다.\n" +
                        "다운로드를 중단합니다.\n\n" +
                        "문제가 지속되면 다음을 시도하세요:\n" +
                        "1. 인터넷 연결 확인\n" +
                        "2. 방화벽/프록시 설정 확인\n" +
                        "3. 직접 Node.js 설치: https://nodejs.org"
                    );
                }

                EditorUtility.DisplayProgressBar("Node.js 다운로드",
                    "압축 해제 중...", 0.90f);

                // 압축 해제
                ExtractNodeJS(tempFile, targetPath, platform);

                // 실행 권한 부여 (macOS/Linux)
                #if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
                    SetExecutablePermissions(targetPath);
                #endif

                EditorUtility.DisplayProgressBar("Node.js 다운로드",
                    "완료!", 1.0f);

                Debug.Log($"[NodeJS] 다운로드 및 설치 완료: {targetPath}");

                EditorUtility.DisplayDialog("다운로드 완료",
                    $"Node.js {NODE_VERSION}이(가) 성공적으로 설치되었습니다.\n\n" +
                    $"위치: {targetPath}",
                    "확인");
            }
            catch (Exception e)
            {
                Debug.LogError($"[NodeJS] 다운로드 실패: {e.Message}");

                EditorUtility.DisplayDialog("다운로드 실패",
                    $"Node.js 다운로드에 실패했습니다:\n\n{e.Message}\n\n" +
                    "수동으로 Node.js를 설치하시거나, 나중에 다시 시도해주세요.\n\n" +
                    "공식 사이트: https://nodejs.org",
                    "확인");

                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                // 임시 파일 삭제
                if (File.Exists(tempFile))
                {
                    try { File.Delete(tempFile); }
                    catch { /* ignore */ }
                }
            }
        }

        /// <summary>
        /// 파일 다운로드 (진행률 표시)
        /// </summary>
        private static void DownloadFile(string url, string targetPath)
        {
            using (var webClient = new WebClient())
            {
                // 진행률 이벤트
                webClient.DownloadProgressChanged += (sender, e) => {
                    float progress = 0.1f + (e.ProgressPercentage / 100f * 0.75f);
                    EditorUtility.DisplayProgressBar("Node.js 다운로드",
                        $"다운로드 중... {e.ProgressPercentage}% ({e.BytesReceived / 1024 / 1024}MB / {e.TotalBytesToReceive / 1024 / 1024}MB)",
                        progress);
                };

                // 동기적 다운로드 (Editor 전용)
                webClient.DownloadFile(url, targetPath);
            }
        }

        /// <summary>
        /// SHA256 체크섬 검증
        /// </summary>
        private static bool VerifyChecksum(string filePath, string expectedHash)
        {
            Debug.Log($"[NodeJS] SHA256 체크섬 검증 시작...");
            Debug.Log($"[NodeJS] 파일: {Path.GetFileName(filePath)}");
            Debug.Log($"[NodeJS] 기대값: {expectedHash}");

            using (var sha256 = SHA256.Create())
            {
                using (var stream = File.OpenRead(filePath))
                {
                    byte[] hash = sha256.ComputeHash(stream);
                    string actualHash = BitConverter.ToString(hash).Replace("-", "").ToLower();

                    Debug.Log($"[NodeJS] 실제값: {actualHash}");

                    if (actualHash != expectedHash.ToLower())
                    {
                        Debug.LogError(
                            $"[NodeJS] ⚠️ 보안 경고: 체크섬 불일치 감지!\n" +
                            $"파일: {Path.GetFileName(filePath)}\n" +
                            $"기대값 (공식): {expectedHash}\n" +
                            $"실제값 (다운로드): {actualHash}\n" +
                            $"파일이 변조되었거나 다운로드 중 손상되었을 수 있습니다."
                        );
                        return false;
                    }

                    Debug.Log($"[NodeJS] ✓ 체크섬 검증 성공! 파일 무결성 확인됨.");
                    return true;
                }
            }
        }

        /// <summary>
        /// 압축 파일 해제
        /// </summary>
        private static void ExtractNodeJS(string archivePath, string targetPath, string platform)
        {
            #if UNITY_EDITOR_WIN
                // Windows: ZIP 압축 해제
                System.IO.Compression.ZipFile.ExtractToDirectory(archivePath, targetPath);

                // node-v24.11.1-win-x64 폴더 내용을 상위로 이동
                string extractedFolder = Path.Combine(targetPath, $"node-v{NODE_VERSION}-win-x64");
                if (Directory.Exists(extractedFolder))
                {
                    foreach (string file in Directory.GetFiles(extractedFolder, "*", SearchOption.AllDirectories))
                    {
                        string relativePath = file.Substring(extractedFolder.Length + 1);
                        string destPath = Path.Combine(targetPath, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(destPath));
                        File.Move(file, destPath);
                    }
                    Directory.Delete(extractedFolder, true);
                }
            #else
                // macOS/Linux: tar.gz 압축 해제
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "/usr/bin/tar",
                        Arguments = $"-xzf \"{archivePath}\" -C \"{targetPath}\" --strip-components=1",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    throw new Exception($"압축 해제 실패 (exit code {process.ExitCode}):\n{stderr}");
                }
            #endif
        }

        /// <summary>
        /// 실행 권한 부여 (macOS/Linux)
        /// </summary>
        private static void SetExecutablePermissions(string nodePath)
        {
            string[] executables = new string[]
            {
                Path.Combine(nodePath, "bin", "node"),
                Path.Combine(nodePath, "bin", "npm"),
                Path.Combine(nodePath, "bin", "npx")
            };

            foreach (string exe in executables)
            {
                if (File.Exists(exe))
                {
                    try
                    {
                        var process = Process.Start(new ProcessStartInfo
                        {
                            FileName = "/bin/chmod",
                            Arguments = $"+x \"{exe}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                        process?.WaitForExit();

                        Debug.Log($"[NodeJS] 실행 권한 부여: {exe}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[NodeJS] 실행 권한 부여 실패 ({exe}): {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 다운로드 URL 목록 (폴백)
        /// </summary>
        private static string[] GetDownloadUrls(string platform, string fileName)
        {
            return new string[]
            {
                // 1순위: Node.js 공식 사이트
                $"https://nodejs.org/dist/v{NODE_VERSION}/{fileName}",

                // 2순위: npmmirror CDN (빠름)
                $"https://cdn.npmmirror.com/binaries/node/v{NODE_VERSION}/{fileName}",

                // 3순위: Huawei Cloud Mirror
                $"https://repo.huaweicloud.com/nodejs/v{NODE_VERSION}/{fileName}",
            };
        }

        /// <summary>
        /// 플랫폼별 파일 이름
        /// </summary>
        private static string GetNodeJSFileName(string platform)
        {
            switch (platform)
            {
                case "darwin-arm64":
                case "darwin-x64":
                case "linux-x64":
                    return $"node-v{NODE_VERSION}-{platform}.tar.gz";
                case "win-x64":
                    return $"node-v{NODE_VERSION}-{platform}.zip";
                default:
                    throw new NotSupportedException($"지원하지 않는 플랫폼: {platform}");
            }
        }

        /// <summary>
        /// 현재 플랫폼 폴더 이름
        /// </summary>
        private static string GetPlatformFolder()
        {
            #if UNITY_EDITOR_WIN
                return "win-x64";
            #elif UNITY_EDITOR_OSX
                // macOS 아키텍처 감지
                return System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
                       == System.Runtime.InteropServices.Architecture.Arm64
                       ? "darwin-arm64"
                       : "darwin-x64";
            #elif UNITY_EDITOR_LINUX
                return "linux-x64";
            #else
                return null;
            #endif
        }

        /// <summary>
        /// npm 실행 파일 경로
        /// </summary>
        private static string GetNpmExecutablePath(string nodePath)
        {
            #if UNITY_EDITOR_WIN
                return Path.Combine(nodePath, "npm.cmd");
            #else
                return Path.Combine(nodePath, "bin", "npm");
            #endif
        }

        /// <summary>
        /// Unity Package 경로
        /// </summary>
        private static string GetPackagePath()
        {
            // PackageManager API로 경로 찾기
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/im.toss.apps-in-toss-unity-sdk");
            if (packageInfo != null)
            {
                return packageInfo.resolvedPath;
            }

            // 폴백: Assets 내부에 있을 경우
            string[] guids = AssetDatabase.FindAssets("t:Script AITConvertCore");
            if (guids.Length > 0)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                // Assets/AppsInToss/Editor/AITConvertCore.cs → Assets/AppsInToss
                string packagePath = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath));
                return packagePath;
            }

            // 최종 폴백
            return Path.Combine(Application.dataPath, "AppsInToss");
        }
    }
}
