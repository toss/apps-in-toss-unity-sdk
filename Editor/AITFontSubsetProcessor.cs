// -----------------------------------------------------------------------
// <copyright file="AITFontSubsetProcessor.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Font CJK Subset (build-time source subsetter)
// </copyright>
// -----------------------------------------------------------------------
//
// 빌드 직전, 지정한 .ttf/.otf 소스를 "보존할 유니코드 범위"만 남기도록 subset 하여
// Unity 가 .data 에 굽는 폰트 데이터를 급감시킨다(CJK 전체 폰트는 5~15MB → subset 후 ~0.1MB).
// 빌드 종료(성공/실패 무관) 후 원본 폰트로 원상 복원한다.
//
// 효과: Unity 의 동적 폰트는 소스 .ttf 전체를 빌드에 포함시켜 런타임에 임의 글자를 래스터화한다.
//   필요한 범위(예: 한글 음절 + ASCII)만 남기면 .data 의 폰트 바이트가 사라져 초기 다운로드/TTI 급감.
//
// ⚠ 동적 텍스트 리스크(이 기능만의 고유 위험): subset 에 포함되지 않은 글자(희귀 한자, 이모지,
//   런타임 동적 텍스트)는 □(누락 글리프)로 렌더된다. 따라서 본 기능은
//     (1) 대상 폰트 경로(fontSubsetTargetPaths) 를 명시적으로 지정해야만 동작하고(비우면 no-op),
//     (2) 보존 범위(fontSubsetUnicodeRanges) 도 명시 필요,
//   두 화이트리스트가 모두 채워졌을 때만 적용된다(프로젝트 전체 폰트 자동 스캔 금지).
//
// 도구: harfbuzz(hb-subset, wasm) 를 래핑한 `subset-font`(npm). Google Fonts 와 동일 코덱.
//   SDK 내장 Node.js(AITNodeJSDownloader)로 실행하며, 최초 1회 ~/.ait-unity-sdk/font-subset/ 에
//   subset-font 를 설치한다(node 다운로드와 동일한 on-demand 패턴). 네트워크/도구 준비 실패 시
//   경고만 남기고 subset 을 건너뛴다(빌드는 풀 폰트로 계속 진행 — graceful degradation).
//
// 비파괴: 소스 원본은 <src>.aitfontbak 로 백업, 빌드 종료 시 원상 복원한다. 빌드가 비정상
//   종료되어 복원이 누락돼도 다음 에디터 로드 시 안전망(SafetyNetRestore)이 자동 복원한다.
//
// 통합: AITConvertCore.BuildWebGL 가 BuildPipeline.BuildPlayer 직전에 ApplyForBuild,
//   try/finally 의 finally 에서 RestoreForBuild 를 호출한다(오디오 스트리밍과 동일 패턴).

using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 빌드 단계 폰트 CJK subset 처리기. <see cref="AITEditorScriptObject.enableFontSubset"/>
    /// 설정에 따라 동작한다. 런타임 컴포넌트는 없다(빌드 산출물만 작아질 뿐, 런타임 렌더 경로 동일).
    /// </summary>
    [InitializeOnLoad]
    public static class AITFontSubsetProcessor
    {
        /// <summary>치환 전 원본 폰트를 보관하는 백업 접미사.</summary>
        private const string BackupSuffix = ".aitfontbak";

        /// <summary>SDK 패키지에 동봉된 subset 러너 디렉토리(Unity 미임포트: '~' 접미사).</summary>
        private const string ToolDirName = "FontSubset~";

        /// <summary>러너 스크립트 파일명.</summary>
        private const string RunnerName = "subset-font-runner.mjs";

        /// <summary>apply 가 진행 중임을 표시하는 마커(Unity 가 무시하는 '.' 접두 숨김 파일).</summary>
        private const string MarkerRelative = "Assets/.ait-fontsubset-active";

        /// <summary>한 번의 subset 결과 핸들. finally 에서 정확한 복원에 사용.</summary>
        public sealed class FontHandle
        {
            /// <summary>이번 빌드에서 subset 이 실제로 수행되었는지.</summary>
            public bool Active;

            /// <summary>subset 된 폰트 개수.</summary>
            public int Count;
        }

        static AITFontSubsetProcessor()
        {
            EditorApplication.delayCall += SafetyNetRestore;
        }

        /// <summary>
        /// 빌드 직전 호출: 설정이 켜져 있고 대상/범위가 지정돼 있으면 대상 폰트를 subset 한다.
        /// </summary>
        /// <param name="config">프로젝트 에디터 설정. null·비활성·화이트리스트 미지정 시 no-op.</param>
        /// <returns>복원에 사용할 핸들(항상 non-null).</returns>
        public static FontHandle ApplyForBuild(AITEditorScriptObject config)
        {
            var handle = new FontHandle();
            if (config == null || !config.enableFontSubset)
            {
                return handle;
            }

            string ranges = (config.fontSubsetUnicodeRanges ?? string.Empty).Trim();
            string[] targets = SplitList(config.fontSubsetTargetPaths);

            // 화이트리스트 이중 안전망: 대상/범위 중 하나라도 비면 동적-텍스트 리스크 때문에 적용하지 않는다.
            if (targets == null || targets.Length == 0)
            {
                Debug.Log("[AIT-FontSubset] 대상 폰트 경로가 비어 있어 건너뜁니다(fontSubsetTargetPaths). 동적 텍스트 안전을 위해 명시적 지정이 필요합니다.");
                return handle;
            }

            if (string.IsNullOrEmpty(ranges))
            {
                Debug.LogWarning("[AIT-FontSubset] 보존 유니코드 범위가 비어 있어 건너뜁니다(fontSubsetUnicodeRanges).");
                return handle;
            }

            string node, runner;
            if (!EnsureTool(out node, out runner))
            {
                Debug.LogWarning("[AIT-FontSubset] subset 도구 준비 실패 → subset 을 건너뜁니다(풀 폰트로 빌드 계속).");
                return handle;
            }

            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                CreateMarker();

                int n = 0;
                long savedBytes = 0;
                foreach (var rawTarget in targets)
                {
                    string assetPath = rawTarget.Trim().Replace('\\', '/');
                    if (string.IsNullOrEmpty(assetPath))
                    {
                        continue;
                    }

                    if (!assetPath.StartsWith("Assets/"))
                    {
                        Debug.LogWarning($"[AIT-FontSubset]   대상 제외(Assets/ 밖): {assetPath}");
                        continue;
                    }

                    string ext = Path.GetExtension(assetPath).ToLowerInvariant();
                    if (ext != ".ttf" && ext != ".otf")
                    {
                        Debug.LogWarning($"[AIT-FontSubset]   대상 제외(.ttf/.otf 아님): {assetPath}");
                        continue;
                    }

                    string srcFull = Path.Combine(projectRoot, assetPath);
                    if (!File.Exists(srcFull))
                    {
                        Debug.LogWarning($"[AIT-FontSubset]   대상 없음: {assetPath}");
                        continue;
                    }

                    long before = new FileInfo(srcFull).Length;
                    string tmpOut = srcFull + ".subset.tmp";

                    if (!RunSubset(node, runner, srcFull, tmpOut, ranges, out string err))
                    {
                        Debug.LogWarning($"[AIT-FontSubset]   subset 실패({Path.GetFileName(assetPath)}): {err} → 이 폰트는 원본 유지");
                        SafeDelete(tmpOut);
                        continue;
                    }

                    if (!File.Exists(tmpOut) || new FileInfo(tmpOut).Length <= 0)
                    {
                        Debug.LogWarning($"[AIT-FontSubset]   subset 산출물 비정상 → 원본 유지: {assetPath}");
                        SafeDelete(tmpOut);
                        continue;
                    }

                    // 백업 후 소스 치환 + reimport(폰트 reimport 는 스크립트 컴파일 무유발 → 도메인 리로드 없음).
                    string bak = srcFull + BackupSuffix;
                    if (!File.Exists(bak))
                    {
                        File.Copy(srcFull, bak, true);
                    }

                    File.Copy(tmpOut, srcFull, true);
                    SafeDelete(tmpOut);
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

                    long after = new FileInfo(srcFull).Length;
                    savedBytes += Math.Max(0, before - after);
                    n++;
                    Debug.Log($"[AIT-FontSubset]   subset {Path.GetFileName(assetPath)}: {before / 1024}KB → {after / 1024}KB");
                }

                AssetDatabase.Refresh();
                handle.Active = n > 0;
                handle.Count = n;
                if (!handle.Active)
                {
                    RemoveMarker();
                }

                Debug.Log($"[AIT-FontSubset] ✓ {n}개 폰트 subset, 소스 {savedBytes / 1048576f:0.0}MB 절감(초기 .data 제거).");
                return handle;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT-FontSubset] 적용 예외 → 복원 후 건너뜀: {e}");
                RestoreAllBackups();
                RemoveMarker();
                AssetDatabase.Refresh();
                return new FontHandle();
            }
        }

        /// <summary>빌드 종료 후(성공/실패 무관) 호출: 원본 폰트로 복원한다.</summary>
        public static void RestoreForBuild(FontHandle handle)
        {
            if (handle == null || !handle.Active)
            {
                return;
            }

            try
            {
                int restored = RestoreAllBackups();
                RemoveMarker();
                AssetDatabase.Refresh();
                Debug.Log($"[AIT-FontSubset] 복원 완료: {restored}개 폰트 원상.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT-FontSubset] 복원 예외: {e}");
            }
        }

        /// <summary>에디터 로드 시 안전망. 마커가 잔존하면(=이전 빌드가 복원 전 종료) 백업을 자동 복원.</summary>
        private static void SafetyNetRestore()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                if (!File.Exists(Path.Combine(projectRoot, MarkerRelative)))
                {
                    return; // 공통 경로: 잔존물 없음(빠른 반환)
                }

                int restored = RestoreAllBackups();
                RemoveMarker();
                if (restored > 0)
                {
                    AssetDatabase.Refresh();
                    Debug.LogWarning($"[AIT-FontSubset] 안전망: 이전 빌드 잔존 폰트 백업 {restored}개를 복원했습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-FontSubset] 안전망 복원 중 예외(무시): {e}");
            }
        }

        // ─────────────────────────── 도구 준비/실행 ───────────────────────────

        /// <summary>내장 Node.js + subset-font 를 준비한다. node 실행경로와 러너 경로를 반환.</summary>
        private static bool EnsureTool(out string nodeExe, out string runnerPath)
        {
            nodeExe = null;
            runnerPath = null;
            try
            {
                string npm = AITNodeJSDownloader.FindEmbeddedNpm(autoDownload: true);
                if (string.IsNullOrEmpty(npm))
                {
                    return false;
                }

                string nodeBin = Path.GetDirectoryName(npm);
                string node = Path.Combine(nodeBin, AITPlatformHelper.IsWindows ? "node.exe" : "node");
                if (!File.Exists(node))
                {
                    return false;
                }

                string srcDir = ResolveToolSourceDir();
                if (string.IsNullOrEmpty(srcDir) || !File.Exists(Path.Combine(srcDir, RunnerName)))
                {
                    Debug.LogWarning($"[AIT-FontSubset] 러너 소스 디렉토리를 찾지 못했습니다: '{srcDir}'");
                    return false;
                }

                string homeTool = GetHomeToolDir();
                Directory.CreateDirectory(homeTool);

                // 러너/매니페스트는 항상 최신본으로 동기화(SDK 업데이트 반영).
                File.Copy(Path.Combine(srcDir, RunnerName), Path.Combine(homeTool, RunnerName), true);
                File.Copy(Path.Combine(srcDir, "package.json"), Path.Combine(homeTool, "package.json"), true);

                // subset-font 미설치 시 1회 설치(on-demand, node 다운로드와 동일 철학).
                string installed = Path.Combine(homeTool, "node_modules", "subset-font", "package.json");
                if (!File.Exists(installed))
                {
                    Debug.Log("[AIT-FontSubset] subset-font 최초 설치 중(내장 npm)...");
                    if (!RunNpmInstall(npm, nodeBin, homeTool) || !File.Exists(installed))
                    {
                        Debug.LogWarning("[AIT-FontSubset] subset-font 설치 실패.");
                        return false;
                    }
                }

                nodeExe = node;
                runnerPath = Path.Combine(homeTool, RunnerName);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-FontSubset] 도구 준비 예외: {e.Message}");
                return false;
            }
        }

        private static bool RunNpmInstall(string npm, string nodeBin, string cwd)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    WorkingDirectory = cwd,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                if (AITPlatformHelper.IsWindows)
                {
                    psi.FileName = "cmd.exe";
                    psi.Arguments = $"/c \"\"{npm}\" install --no-audit --no-fund --loglevel=error\"";
                }
                else
                {
                    psi.FileName = npm;
                    psi.Arguments = "install --no-audit --no-fund --loglevel=error";
                }

                PrependPath(psi, nodeBin);
                return RunProcess(psi, 180000, out _, out _);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-FontSubset] npm install 예외: {e.Message}");
                return false;
            }
        }

        private static bool RunSubset(string node, string runner, string inAbs, string outAbs, string ranges, out string err)
        {
            err = null;
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = node,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(runner),
                };
                psi.ArgumentList.Add(runner);
                psi.ArgumentList.Add(inAbs);
                psi.ArgumentList.Add(outAbs);
                psi.ArgumentList.Add(ranges);
                PrependPath(psi, Path.GetDirectoryName(node));

                if (!RunProcess(psi, 120000, out string stdout, out string stderr))
                {
                    err = string.IsNullOrEmpty(stderr) ? stdout : stderr;
                    if (string.IsNullOrEmpty(err))
                    {
                        err = "러너 비정상 종료";
                    }

                    return false;
                }

                // 러너 stdout 마지막 JSON 라인의 ok 판정.
                return stdout != null && stdout.Contains("\"ok\":true");
            }
            catch (Exception e)
            {
                err = e.Message;
                return false;
            }
        }

        private static bool RunProcess(ProcessStartInfo psi, int timeoutMs, out string stdout, out string stderr)
        {
            stdout = null;
            stderr = null;
            using (var p = new Process { StartInfo = psi })
            {
                p.Start();
                stdout = p.StandardOutput.ReadToEnd();
                stderr = p.StandardError.ReadToEnd();
                if (!p.WaitForExit(timeoutMs))
                {
                    try { p.Kill(); } catch { /* 무시 */ }
                    return false;
                }

                return p.ExitCode == 0;
            }
        }

        private static void PrependPath(ProcessStartInfo psi, string dir)
        {
            if (string.IsNullOrEmpty(dir))
            {
                return;
            }

            string sep = AITPlatformHelper.IsWindows ? ";" : ":";
            string existing = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            psi.EnvironmentVariables["PATH"] = dir + sep + existing;
        }

        /// <summary>~/.ait-unity-sdk/font-subset (Windows: %LOCALAPPDATA%\ait-unity-sdk\font-subset).</summary>
        private static string GetHomeToolDir()
        {
            string basePath = AITPlatformHelper.IsWindows
                ? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(basePath, ".ait-unity-sdk", "font-subset");
        }

        /// <summary>SDK 패키지에 동봉된 러너 소스 디렉토리. UPM/embedded 설치 모두 해석.</summary>
        private static string ResolveToolSourceDir()
        {
            try
            {
                var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(AITFontSubsetProcessor).Assembly);
                if (pkg != null && !string.IsNullOrEmpty(pkg.resolvedPath))
                {
                    return Path.Combine(pkg.resolvedPath, "Editor", ToolDirName);
                }
            }
            catch
            {
                // PackageInfo 미해석(Assets 내 임베드 개발) → 소스 파일 위치 폴백
            }

            string here = CallerDir();
            return string.IsNullOrEmpty(here) ? null : Path.Combine(here, ToolDirName);
        }

        // ─────────────────────────── 백업/복원 ───────────────────────────

        /// <summary>Assets 트리의 모든 *.aitfontbak 를 원본으로 되돌리고 백업을 삭제한다. 복원 개수 반환.</summary>
        private static int RestoreAllBackups()
        {
            int restored = 0;
            string assetsPath = Application.dataPath;
            string projectRoot = Directory.GetParent(assetsPath).FullName;
            string[] backups;
            try
            {
                backups = Directory.GetFiles(assetsPath, "*" + BackupSuffix, SearchOption.AllDirectories);
            }
            catch
            {
                return 0;
            }

            foreach (var bak in backups)
            {
                string srcFull = bak.Substring(0, bak.Length - BackupSuffix.Length);
                try
                {
                    File.Copy(bak, srcFull, true);
                    File.Delete(bak);

                    string rel = AbsoluteToProjectRelative(srcFull, projectRoot);
                    if (!string.IsNullOrEmpty(rel))
                    {
                        AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    }

                    restored++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-FontSubset] 백업 복원 실패({bak}): {e.Message}");
                }
            }

            return restored;
        }

        private static void CreateMarker()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                File.WriteAllText(Path.Combine(projectRoot, MarkerRelative), "active");
            }
            catch
            {
                // 마커 생성 실패는 치명적이지 않음(안전망 가속용)
            }
        }

        private static void RemoveMarker()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string m = Path.Combine(projectRoot, MarkerRelative);
                if (File.Exists(m))
                {
                    File.Delete(m);
                }
            }
            catch
            {
                // 무시
            }
        }

        private static void SafeDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // 무시
            }
        }

        private static string CallerDir([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
            => string.IsNullOrEmpty(thisFile) ? null : Path.GetDirectoryName(thisFile);

        private static string AbsoluteToProjectRelative(string absolute, string projectRoot)
        {
            string norm = absolute.Replace('\\', '/');
            string root = projectRoot.Replace('\\', '/').TrimEnd('/') + "/";
            return norm.StartsWith(root) ? norm.Substring(root.Length) : null;
        }

        private static string[] SplitList(string v)
            => string.IsNullOrEmpty(v) ? null : v.Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
