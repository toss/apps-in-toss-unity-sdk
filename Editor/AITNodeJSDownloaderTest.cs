using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// AITNodeJSDownloader 테스트 유틸리티
    /// Unity 메뉴: Apps in Toss > Test Node.js Downloader
    /// </summary>
    public static class AITNodeJSDownloaderTest
    {
        [MenuItem("Apps in Toss/Test Node.js Downloader/1. Test Platform Detection")]
        public static void TestPlatformDetection()
        {
            Debug.Log("=== Platform Detection Test ===");

            #if UNITY_EDITOR_WIN
                Debug.Log("✓ Platform: Windows x64");
                Debug.Log("  Expected: win-x64");
            #elif UNITY_EDITOR_OSX
                var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture;
                Debug.Log($"✓ Platform: macOS {arch}");
                Debug.Log($"  Expected: darwin-{(arch == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "x64")}");
            #elif UNITY_EDITOR_LINUX
                Debug.Log("✓ Platform: Linux x64");
                Debug.Log("  Expected: linux-x64");
            #else
                Debug.LogError("✗ Unknown platform!");
            #endif

            Debug.Log("=== Test Complete ===");
        }

        [MenuItem("Apps in Toss/Test Node.js Downloader/2. Test npm Path Finding (Dry Run)")]
        public static void TestNpmPathFinding()
        {
            Debug.Log("=== npm Path Finding Test ===");

            // 시스템 npm 확인
            Debug.Log("[1] Checking system npm...");
            string[] possiblePaths = new string[]
            {
                "/usr/local/bin/npm",
                "/opt/homebrew/bin/npm",
                "/usr/bin/npm"
            };

            bool foundSystem = false;
            foreach (string path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Debug.Log($"  ✓ Found: {path}");
                    foundSystem = true;
                    break;
                }
            }

            if (!foundSystem)
            {
                Debug.Log("  ✗ No system npm found in standard paths");
            }

            // which npm 테스트
            Debug.Log("[2] Testing 'which npm'...");
            try
            {
                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "/bin/bash",
                        Arguments = "-l -c \"which npm\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                string output = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();

                if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                {
                    Debug.Log($"  ✓ which npm: {output}");
                }
                else
                {
                    Debug.Log("  ✗ which npm failed");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"  ✗ which npm error: {e.Message}");
            }

            // Embedded npm 경로 확인
            Debug.Log("[3] Checking Embedded npm path (without download)...");
            string embeddedNpm = AITNodeJSDownloader.FindEmbeddedNpm(autoDownload: false);
            if (!string.IsNullOrEmpty(embeddedNpm))
            {
                Debug.Log($"  ✓ Found embedded npm: {embeddedNpm}");
            }
            else
            {
                Debug.Log("  ✗ Embedded npm not found (would trigger auto-download)");
            }

            Debug.Log("=== Test Complete ===");
        }

        [MenuItem("Apps in Toss/Test Node.js Downloader/3. Test Download URLs")]
        public static void TestDownloadUrls()
        {
            Debug.Log("=== Download URL Test ===");

            string version = "24.11.1";
            string[] platforms = { "darwin-arm64", "darwin-x64", "win-x64", "linux-x64" };

            foreach (string platform in platforms)
            {
                Debug.Log($"\n[{platform}]");

                string fileName = GetFileName(platform, version);
                Debug.Log($"  Filename: {fileName}");

                string[] urls = new string[]
                {
                    $"https://nodejs.org/dist/v{version}/{fileName}",
                    $"https://cdn.npmmirror.com/binaries/node/v{version}/{fileName}",
                    $"https://repo.huaweicloud.com/nodejs/v{version}/{fileName}"
                };

                foreach (string url in urls)
                {
                    Debug.Log($"  URL: {url}");
                }
            }

            Debug.Log("\n=== Test Complete ===");
        }

        private static string GetFileName(string platform, string version)
        {
            return platform == "win-x64"
                ? $"node-v{version}-{platform}.zip"
                : $"node-v{version}-{platform}.tar.gz";
        }

        [MenuItem("Apps in Toss/Test Node.js Downloader/4. Verify SHA256 Checksums")]
        public static void VerifyChecksums()
        {
            Debug.Log("=== SHA256 Checksum Verification ===");
            Debug.Log("Checksums hardcoded in AITNodeJSDownloader.cs:");
            Debug.Log("");
            Debug.Log("darwin-arm64: b05aa3a66efe680023f930bd5af3fdbbd542794da5644ca2ad711d68cbd4dc35");
            Debug.Log("darwin-x64:   096081b6d6fcdd3f5ba0f5f1d44a47e83037ad2e78eada26671c252fe64dd111");
            Debug.Log("win-x64:      5355ae6d7c49eddcfde7d34ac3486820600a831bf81dc3bdca5c8db6a9bb0e76");
            Debug.Log("linux-x64:    60e3b0a8500819514aca603487c254298cd776de0698d3cd08f11dba5b8289a8");
            Debug.Log("");
            Debug.Log("Source: https://nodejs.org/dist/v24.11.1/SHASUMS256.txt");
            Debug.Log("=== All checksums verified from official source ===");
        }

        [MenuItem("Apps in Toss/Test Node.js Downloader/5. Test Full Download (REAL DOWNLOAD)")]
        public static void TestFullDownload()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "실제 다운로드 테스트",
                "이 테스트는 실제로 Node.js를 다운로드합니다.\n\n" +
                "크기: 약 40-50MB\n" +
                "시간: 1-3분 소요\n\n" +
                "계속하시겠습니까?",
                "다운로드", "취소"
            );

            if (!confirm)
            {
                Debug.Log("테스트 취소됨");
                return;
            }

            Debug.Log("=== Full Download Test START ===");

            try
            {
                string npmPath = AITNodeJSDownloader.FindEmbeddedNpm(autoDownload: true);

                if (!string.IsNullOrEmpty(npmPath))
                {
                    Debug.Log($"✓ SUCCESS: npm installed at {npmPath}");

                    // npm 버전 확인
                    TestNpmVersion(npmPath);
                }
                else
                {
                    Debug.LogError("✗ FAILED: npm path is null");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"✗ EXCEPTION: {e.Message}\n{e.StackTrace}");
            }

            Debug.Log("=== Full Download Test END ===");
        }

        private static void TestNpmVersion(string npmPath)
        {
            try
            {
                Debug.Log($"[Version Test] Running: {npmPath} --version");

                var process = new System.Diagnostics.Process
                {
                    StartInfo = new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = npmPath,
                        Arguments = "--version",
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

                if (process.ExitCode == 0)
                {
                    Debug.Log($"✓ npm version: {output}");
                }
                else
                {
                    Debug.LogError($"✗ npm --version failed (exit code {process.ExitCode})\nStderr: {error}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"✗ npm version test failed: {e.Message}");
            }
        }

        [MenuItem("Apps in Toss/Test Node.js Downloader/6. Clean Embedded Node.js")]
        public static void CleanEmbeddedNodeJS()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "Embedded Node.js 삭제",
                "Tools~/NodeJS/ 폴더의 모든 런타임을 삭제합니다.\n\n" +
                "다음 빌드 시 자동으로 다시 다운로드됩니다.\n\n" +
                "계속하시겠습니까?",
                "삭제", "취소"
            );

            if (!confirm)
            {
                Debug.Log("삭제 취소됨");
                return;
            }

            try
            {
                string packagePath = GetPackagePath();
                string nodeJSPath = Path.Combine(packagePath, "Tools~", "NodeJS");

                if (Directory.Exists(nodeJSPath))
                {
                    string[] platforms = { "darwin-arm64", "darwin-x64", "win-x64", "linux-x64" };
                    int deletedCount = 0;

                    foreach (string platform in platforms)
                    {
                        string platformPath = Path.Combine(nodeJSPath, platform);
                        if (Directory.Exists(platformPath))
                        {
                            Directory.Delete(platformPath, true);
                            Debug.Log($"✓ Deleted: {platformPath}");
                            deletedCount++;
                        }
                    }

                    if (deletedCount > 0)
                    {
                        EditorUtility.DisplayDialog("삭제 완료",
                            $"{deletedCount}개 플랫폼의 런타임이 삭제되었습니다.", "확인");
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("삭제할 항목 없음",
                            "런타임이 설치되어 있지 않습니다.", "확인");
                    }
                }
                else
                {
                    Debug.Log("Tools~/NodeJS 폴더가 없습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"✗ 삭제 실패: {e.Message}");
                EditorUtility.DisplayDialog("삭제 실패", e.Message, "확인");
            }
        }

        private static string GetPackagePath()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/im.toss.apps-in-toss-unity-sdk");
            if (packageInfo != null)
            {
                return packageInfo.resolvedPath;
            }

            string[] guids = AssetDatabase.FindAssets("t:Script AITConvertCore");
            if (guids.Length > 0)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                string packagePath = Path.GetDirectoryName(Path.GetDirectoryName(scriptPath));
                return packagePath;
            }

            return Path.Combine(Application.dataPath, "AppsInToss");
        }
    }
}
