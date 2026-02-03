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
        private const string NODE_VERSION = "24.13.0";

        // SHA256 체크섬 (Node.js 공식 SHASUMS256.txt에서 검증됨)
        // 출처: https://nodejs.org/dist/v24.13.0/SHASUMS256.txt
        private static readonly Dictionary<string, string> Checksums = new Dictionary<string, string>
        {
            ["darwin-arm64"] = "d595961e563fcae057d4a0fb992f175a54d97fcc4a14dc2d474d92ddeea3b9f8",
            ["darwin-x64"] = "6f03c1b48ddbe1b129a6f8038be08e0899f05f17185b4d3e4350180ab669a7f3",
            ["win-x64"] = "ca2742695be8de44027d71b3f53a4bdb36009b95575fe1ae6f7f0b5ce091cb88",
            ["linux-x64"] = "6223aad1a81f9d1e7b682c59d12e2de233f7b4c37475cd40d1c89c42b737ffa8"
        };

        // 예상 파일 크기 (bytes) - 불완전한 다운로드 감지용
        private static readonly Dictionary<string, long> ExpectedFileSizes = new Dictionary<string, long>
        {
            ["darwin-arm64"] = 44_000_000,  // ~44MB
            ["darwin-x64"] = 47_000_000,    // ~47MB
            ["win-x64"] = 32_000_000,       // ~32MB
            ["linux-x64"] = 48_000_000      // ~48MB
        };

        private const int MAX_DOWNLOAD_RETRIES = 3;

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

            string nodePath = GetNodeInstallPath(platform);
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
                Debug.Log($"[NodeJS] Node.js {NODE_VERSION} 자동 다운로드를 시작합니다. (약 40-50MB)");
                Debug.Log($"[NodeJS] 설치 위치: {nodePath}");

                DownloadNodeJS(platform, nodePath);

                // 다운로드 후 재확인
                if (File.Exists(npmPath))
                {
                    return npmPath;
                }
            }

            return null;
        }

        /// <summary>
        /// Node.js 설치 경로 (시스템 공용 위치)
        /// macOS/Linux: ~/.ait-unity-sdk/nodejs/v{VERSION}/{platform}/
        /// Windows: %LOCALAPPDATA%\ait-unity-sdk\nodejs\v{VERSION}\{platform}\
        /// </summary>
        private static string GetNodeInstallPath(string platform)
        {
            string basePath;
            #if UNITY_EDITOR_WIN
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            #else
                basePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            #endif

            return Path.Combine(basePath, ".ait-unity-sdk", "nodejs", $"v{NODE_VERSION}", platform);
        }

        /// <summary>
        /// Node.js 다운로드 및 설치
        /// atomic 설치: 임시 디렉토리에 먼저 설치 후 최종 경로로 이동하여
        /// 여러 Unity 인스턴스가 동시에 실행될 때 race condition 방지
        /// </summary>
        private static void DownloadNodeJS(string platform, string targetPath)
        {
            string fileName = GetNodeJSFileName(platform);
            // 여러 Unity 버전이 동시 실행될 때 race condition 방지를 위해 고유 ID 추가
            string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);
            string tempDir = Path.Combine(Path.GetTempPath(), $"AppsInTossSDK-{uniqueId}");
            Directory.CreateDirectory(tempDir);
            string tempFile = Path.Combine(tempDir, fileName);

            // atomic 설치를 위한 임시 설치 경로 (최종 경로의 sibling)
            string targetParent = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetParent))
            {
                Directory.CreateDirectory(targetParent);
            }
            string stagingPath = targetPath + $"-installing-{uniqueId}";

            try
            {
                // 스테이징 디렉토리 생성
                Directory.CreateDirectory(stagingPath);

                // 다운로드 URL 목록 (폴백)
                string[] downloadUrls = GetDownloadUrls(platform, fileName);

                EditorUtility.DisplayProgressBar("Node.js 다운로드",
                    "Node.js 런타임을 다운로드하고 있습니다...", 0.1f);

                // 재시도 + 폴백을 사용하여 다운로드
                bool downloaded = false;
                Exception lastException = null;
                int totalAttempts = 0;

                for (int retry = 0; retry < MAX_DOWNLOAD_RETRIES && !downloaded; retry++)
                {
                    if (retry > 0)
                    {
                        Debug.Log($"[NodeJS] === 재시도 {retry}/{MAX_DOWNLOAD_RETRIES - 1} ===");
                        // 재시도 전 잠시 대기 (exponential backoff)
                        System.Threading.Thread.Sleep(1000 * retry);
                    }

                    foreach (string url in downloadUrls)
                    {
                        totalAttempts++;
                        try
                        {
                            Debug.Log($"[NodeJS] 다운로드 시도 #{totalAttempts}: {url}");
                            EditorUtility.DisplayProgressBar("Node.js 다운로드",
                                $"다운로드 시도 #{totalAttempts}: {new Uri(url).Host}...", 0.1f);

                            DownloadFile(url, tempFile, platform);
                            downloaded = true;
                            Debug.Log($"[NodeJS] ✓ 다운로드 성공: {url}");
                            break;
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[NodeJS] 다운로드 실패 ({url}): {e.Message}");
                            lastException = e;

                            // 다운로드 실패 시 임시 파일 삭제
                            if (File.Exists(tempFile))
                            {
                                try { File.Delete(tempFile); } catch { /* ignore */ }
                            }
                        }
                    }
                }

                if (!downloaded)
                {
                    throw new Exception(
                        $"Node.js 다운로드 실패 (총 {totalAttempts}회 시도)\n\n" +
                        $"마지막 오류: {lastException?.Message}\n\n" +
                        "가능한 원인:\n" +
                        "- 네트워크 연결 불안정\n" +
                        "- 방화벽/프록시 차단\n" +
                        "- 디스크 공간 부족\n\n" +
                        "해결 방법:\n" +
                        "1. 네트워크 연결 확인 후 다시 시도\n" +
                        "2. 직접 Node.js 설치: https://nodejs.org"
                    );
                }

                EditorUtility.DisplayProgressBar("Node.js 다운로드",
                    "파일 무결성 검증 중 (SHA256)...", 0.85f);

                // 체크섬 검증 (필수 - 보안) - 실패 시 재시도
                if (!Checksums.ContainsKey(platform) || string.IsNullOrEmpty(Checksums[platform]))
                {
                    throw new Exception($"플랫폼 '{platform}'의 체크섬 정보가 없습니다. SDK 업데이트가 필요합니다.");
                }

                bool checksumValid = VerifyChecksum(tempFile, Checksums[platform]);
                if (!checksumValid)
                {
                    // 체크섬 실패 시 다른 미러에서 재시도
                    Debug.LogWarning("[NodeJS] 체크섬 불일치! 다른 미러에서 재다운로드를 시도합니다...");
                    File.Delete(tempFile);

                    // 나머지 미러들로 재시도
                    for (int mirrorIdx = 1; mirrorIdx < downloadUrls.Length && !checksumValid; mirrorIdx++)
                    {
                        string mirrorUrl = downloadUrls[mirrorIdx];
                        try
                        {
                            Debug.Log($"[NodeJS] 대체 미러 시도 ({mirrorIdx}/{downloadUrls.Length - 1}): {mirrorUrl}");
                            EditorUtility.DisplayProgressBar("Node.js 다운로드",
                                $"체크섬 실패 - 대체 미러에서 재다운로드 중...", 0.2f);

                            DownloadFile(mirrorUrl, tempFile, platform);
                            checksumValid = VerifyChecksum(tempFile, Checksums[platform]);

                            if (checksumValid)
                            {
                                Debug.Log($"[NodeJS] ✓ 대체 미러에서 다운로드 성공!");
                            }
                            else
                            {
                                Debug.LogWarning($"[NodeJS] 대체 미러도 체크섬 실패: {mirrorUrl}");
                                File.Delete(tempFile);
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[NodeJS] 대체 미러 다운로드 실패: {e.Message}");
                            if (File.Exists(tempFile)) File.Delete(tempFile);
                        }
                    }
                }

                if (!checksumValid)
                {
                    throw new Exception(
                        "보안 경고: 모든 다운로드 소스에서 체크섬 검증에 실패했습니다.\n\n" +
                        "가능한 원인:\n" +
                        "- 네트워크 연결 불안정으로 파일이 손상됨\n" +
                        "- 프록시/방화벽이 파일을 변조함\n" +
                        "- 다운로드 서버 문제\n\n" +
                        "해결 방법:\n" +
                        "1. 네트워크 연결 확인 후 다시 시도\n" +
                        "2. VPN/프록시 비활성화 후 시도\n" +
                        "3. 직접 Node.js 설치: https://nodejs.org"
                    );
                }

                EditorUtility.DisplayProgressBar("Node.js 다운로드",
                    "압축 해제 중...", 0.90f);

                // 스테이징 경로에 압축 해제
                ExtractNodeJS(tempFile, stagingPath, platform);

                // 실행 권한 부여 (macOS/Linux)
                #if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
                    SetExecutablePermissions(stagingPath);
                #endif

                EditorUtility.DisplayProgressBar("Node.js 다운로드",
                    "Node.js 설치 완료! pnpm 설치 중...", 0.95f);

                // pnpm 자동 설치 (스테이징 경로에서)
                bool pnpmInstalled = InstallPnpm(stagingPath);

                // atomic 이동: 스테이징 → 최종 경로
                // 다른 프로세스가 먼저 설치를 완료했을 수 있으므로 확인
                if (Directory.Exists(targetPath))
                {
                    // 이미 설치됨 - 스테이징 삭제
                    Debug.Log($"[NodeJS] 다른 프로세스가 이미 설치를 완료함. 스테이징 삭제: {stagingPath}");
                    try { Directory.Delete(stagingPath, true); } catch { /* ignore */ }
                }
                else
                {
                    try
                    {
                        Directory.Move(stagingPath, targetPath);
                        Debug.Log($"[NodeJS] Node.js 설치 완료 (atomic move): {targetPath}");
                    }
                    catch (IOException)
                    {
                        // Move 실패 = 다른 프로세스가 동시에 이동 성공
                        if (Directory.Exists(targetPath))
                        {
                            Debug.Log($"[NodeJS] 다른 프로세스가 동시에 설치 완료함. 스테이징 삭제.");
                            try { Directory.Delete(stagingPath, true); } catch { /* ignore */ }
                        }
                        else
                        {
                            throw;
                        }
                    }
                }

                Debug.Log($"[NodeJS] Node.js 다운로드 및 설치 완료: {targetPath}");

                EditorUtility.DisplayProgressBar("Node.js 다운로드",
                    "완료!", 1.0f);

                // 로그만 남김 (다이얼로그 없음)
                if (pnpmInstalled)
                {
                    Debug.Log($"[NodeJS] ✓ Node.js {NODE_VERSION} 및 pnpm 설치 완료!");
                }
                else
                {
                    Debug.LogWarning($"[NodeJS] ✓ Node.js {NODE_VERSION} 설치 완료. pnpm 설치 실패 (빌드 시 재시도됨)");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[NodeJS] 다운로드 실패: {e.Message}");

                // CI/배치 모드에서는 다이얼로그 스킵
                AITPlatformHelper.ShowInfoDialog("다운로드 실패",
                    $"Node.js 다운로드에 실패했습니다:\n\n{e.Message}\n\n" +
                    "수동으로 Node.js를 설치하시거나, 나중에 다시 시도해주세요.\n\n" +
                    "공식 사이트: https://nodejs.org",
                    "확인");

                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();

                // 임시 디렉토리 전체 삭제 (고유 ID로 생성했으므로 안전)
                if (Directory.Exists(tempDir))
                {
                    try { Directory.Delete(tempDir, true); }
                    catch { /* ignore */ }
                }

                // 실패 시 스테이징 디렉토리 정리
                if (Directory.Exists(stagingPath))
                {
                    try { Directory.Delete(stagingPath, true); }
                    catch { /* ignore */ }
                }
            }
        }

        /// <summary>
        /// 파일 다운로드 (진행률 표시, 파일 크기 검증 포함)
        /// </summary>
        private static void DownloadFile(string url, string targetPath, string platform)
        {
            // 기존 파일 삭제
            if (File.Exists(targetPath))
            {
                File.Delete(targetPath);
            }

            using (var webClient = new WebClient())
            {
                // 타임아웃 방지를 위한 User-Agent 설정
                webClient.Headers.Add("User-Agent", "AppsInTossSDK/1.0");

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

            // 다운로드 완료 후 파일 크기 검증
            ValidateFileSize(targetPath, platform);
        }

        /// <summary>
        /// 다운로드한 파일 크기 검증 (불완전한 다운로드 감지)
        /// </summary>
        private static void ValidateFileSize(string filePath, string platform)
        {
            if (!File.Exists(filePath))
            {
                throw new Exception("다운로드한 파일이 존재하지 않습니다.");
            }

            var fileInfo = new FileInfo(filePath);
            long actualSize = fileInfo.Length;

            Debug.Log($"[NodeJS] 다운로드 파일 크기: {actualSize / 1024 / 1024}MB ({actualSize:N0} bytes)");

            // 최소 예상 크기 검증
            if (ExpectedFileSizes.TryGetValue(platform, out long minExpectedSize))
            {
                if (actualSize < minExpectedSize)
                {
                    throw new Exception(
                        $"다운로드가 불완전합니다.\n" +
                        $"예상 크기: {minExpectedSize / 1024 / 1024}MB 이상\n" +
                        $"실제 크기: {actualSize / 1024 / 1024}MB ({actualSize:N0} bytes)\n" +
                        $"네트워크 연결을 확인하고 다시 시도해주세요."
                    );
                }
            }

            // 파일이 비어있는지 확인
            if (actualSize == 0)
            {
                throw new Exception("다운로드한 파일이 비어있습니다. 네트워크 연결을 확인해주세요.");
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
                // Windows: PowerShell을 사용한 ZIP 추출 (긴 경로 문제 우회)
                ExtractWithPowerShell(archivePath, targetPath);
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

        #if UNITY_EDITOR_WIN
        /// <summary>
        /// PowerShell을 사용한 Windows ZIP 추출 (긴 경로 문제 우회)
        /// Windows MAX_PATH 260자 제한을 피하기 위해 짧은 임시 경로에 먼저 추출 후 복사
        /// </summary>
        private static void ExtractWithPowerShell(string archivePath, string targetPath)
        {
            // 짧은 임시 경로 생성 (권한 문제 시 폴백)
            string shortTempDir = CreateShortTempDirectory();

            Debug.Log($"[NodeJS] 임시 경로 사용: {shortTempDir}");

            try
            {
                // 1. PowerShell Expand-Archive로 짧은 경로에 추출
                Debug.Log("[NodeJS] PowerShell로 ZIP 압축 해제 중...");
                string extractCmd = $"Expand-Archive -Path '{archivePath}' -DestinationPath '{shortTempDir}' -Force";

                var extractResult = AITPlatformHelper.ExecuteCommand(extractCmd, timeoutMs: 300000, verbose: true);
                if (!extractResult.Success)
                {
                    throw new Exception($"ZIP 추출 실패: {extractResult.Error}");
                }

                // 추출된 폴더 경로 (node-v{NODE_VERSION}-win-x64)
                string extractedFolder = Path.Combine(shortTempDir, $"node-v{NODE_VERSION}-win-x64");

                if (!Directory.Exists(extractedFolder))
                {
                    // 추출된 내용 확인
                    string[] contents = Directory.GetDirectories(shortTempDir);
                    string contentList = contents.Length > 0 ? string.Join(", ", contents) : "(비어있음)";
                    throw new Exception($"추출된 폴더를 찾을 수 없습니다: {extractedFolder}\n추출된 내용: {contentList}");
                }

                // 2. Copy-Item으로 타겟 경로로 복사
                // PowerShell의 Copy-Item은 긴 경로를 더 잘 처리함
                Debug.Log($"[NodeJS] 파일 복사 중: {extractedFolder} → {targetPath}");
                string copyCmd = $"Copy-Item -Path '{extractedFolder}\\*' -Destination '{targetPath}' -Recurse -Force";

                var copyResult = AITPlatformHelper.ExecuteCommand(copyCmd, timeoutMs: 300000, verbose: true);
                if (!copyResult.Success)
                {
                    throw new Exception($"파일 복사 실패: {copyResult.Error}");
                }

                Debug.Log("[NodeJS] ✓ ZIP 추출 및 파일 복사 완료");
            }
            finally
            {
                // 임시 폴더 정리
                if (Directory.Exists(shortTempDir))
                {
                    try
                    {
                        Debug.Log($"[NodeJS] 임시 폴더 정리: {shortTempDir}");
                        // PowerShell로 삭제 (긴 경로도 처리 가능)
                        AITPlatformHelper.ExecuteCommand(
                            $"Remove-Item -Path '{shortTempDir}' -Recurse -Force -ErrorAction SilentlyContinue",
                            timeoutMs: 60000,
                            verbose: false
                        );
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"[NodeJS] 임시 폴더 삭제 실패 (무시됨): {e.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 짧은 임시 디렉토리 생성 (권한 문제 시 폴백)
        /// 1순위: 드라이브 루트 (예: C:\AIT-XXXX) - 가장 짧은 경로
        /// 2순위: 표준 임시 디렉토리 (예: C:\Users\...\Temp\AIT-XXXX) - 권한 보장
        /// </summary>
        private static string CreateShortTempDirectory()
        {
            string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8);

            // 1순위: 드라이브 루트에 생성 시도 (가장 짧은 경로)
            string driveRoot = Path.GetPathRoot(Path.GetTempPath()) ?? "C:\\";
            string shortPath = Path.Combine(driveRoot, $"AIT-{uniqueId}");

            try
            {
                Directory.CreateDirectory(shortPath);
                Debug.Log($"[NodeJS] 드라이브 루트에 임시 폴더 생성 성공: {shortPath}");
                return shortPath;
            }
            catch (UnauthorizedAccessException)
            {
                Debug.LogWarning($"[NodeJS] 드라이브 루트 접근 권한 없음 ({driveRoot}), 표준 임시 디렉토리로 폴백");
            }
            catch (IOException ex)
            {
                Debug.LogWarning($"[NodeJS] 드라이브 루트 접근 실패 ({ex.Message}), 표준 임시 디렉토리로 폴백");
            }

            // 2순위: 표준 임시 디렉토리 사용 (권한 보장)
            string fallbackPath = Path.Combine(Path.GetTempPath(), $"AIT-{uniqueId}");
            Directory.CreateDirectory(fallbackPath);
            Debug.Log($"[NodeJS] 표준 임시 디렉토리 사용: {fallbackPath}");
            return fallbackPath;
        }
        #endif

        /// <summary>
        /// pnpm 설치 (npm install -g pnpm)
        /// </summary>
        /// <param name="nodePath">Node.js 설치 경로</param>
        /// <returns>설치 성공 여부</returns>
        private static bool InstallPnpm(string nodePath)
        {
            Debug.Log("[NodeJS] pnpm 설치 시작...");

            try
            {
                string npmPath = GetNpmExecutablePath(nodePath);
                if (!File.Exists(npmPath))
                {
                    Debug.LogError($"[NodeJS] npm을 찾을 수 없습니다: {npmPath}");
                    return false;
                }

                // npm 실행을 위한 PATH 설정
                string binPath = AITPlatformHelper.IsWindows ? nodePath : Path.Combine(nodePath, "bin");

                // npm install -g pnpm 실행
                string command = $"\"{npmPath}\" install -g pnpm";
                var result = AITPlatformHelper.ExecuteCommand(
                    command,
                    workingDirectory: nodePath,
                    additionalPaths: new[] { binPath },
                    timeoutMs: 120000,  // 2분 타임아웃
                    verbose: true
                );

                if (result.Success)
                {
                    Debug.Log("[NodeJS] ✓ pnpm 설치 완료!");

                    // pnpm 실행 권한 부여 (Unix)
                    if (!AITPlatformHelper.IsWindows)
                    {
                        string pnpmPath = Path.Combine(nodePath, "bin", "pnpm");
                        if (File.Exists(pnpmPath))
                        {
                            AITPlatformHelper.SetExecutablePermission(pnpmPath, verbose: true);
                        }
                    }

                    return true;
                }
                else
                {
                    Debug.LogWarning($"[NodeJS] pnpm 설치 실패 (Exit Code: {result.ExitCode})");
                    if (!string.IsNullOrEmpty(result.Error))
                    {
                        Debug.LogWarning($"[NodeJS] 에러: {result.Error}");
                    }
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NodeJS] pnpm 설치 중 예외 발생: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 실행 권한 부여 (Unix 전용, Windows에서는 무시)
        /// </summary>
        private static void SetExecutablePermissions(string nodePath)
        {
            // Windows에서는 실행 권한이 필요 없음
            if (AITPlatformHelper.IsWindows)
            {
                return;
            }

            string[] executables = new string[]
            {
                Path.Combine(nodePath, "bin", "node"),
                Path.Combine(nodePath, "bin", "npm"),
                Path.Combine(nodePath, "bin", "npx")
            };

            foreach (string exe in executables)
            {
                AITPlatformHelper.SetExecutablePermission(exe, verbose: true);
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
        /// npm 실행 파일 경로 (크로스 플랫폼)
        /// </summary>
        private static string GetNpmExecutablePath(string nodePath)
        {
            if (AITPlatformHelper.IsWindows)
            {
                return Path.Combine(nodePath, "npm.cmd");
            }
            else
            {
                return Path.Combine(nodePath, "bin", "npm");
            }
        }

    }
}
