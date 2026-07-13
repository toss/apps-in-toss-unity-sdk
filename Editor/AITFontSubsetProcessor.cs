// -----------------------------------------------------------------------
// <copyright file="AITFontSubsetProcessor.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Font CJK Subset (build-time source subsetter)
// </copyright>
// -----------------------------------------------------------------------
//
// 빌드 직전, 크고 빌드에 포함될 가능성이 있는 .ttf/.otf 소스를 "보존할 유니코드 범위"만 남기도록
// subset 하여 Unity 가 .data 에 굽는 폰트 데이터를 급감시킨다(CJK 전체 폰트는 5~15MB → subset 후 ~0.1MB).
// 빌드 종료(성공/실패 무관) 후 원본 폰트로 원상 복원한다.
//
// 효과: Unity 의 동적 폰트는 소스 .ttf 전체를 빌드에 포함시켜 런타임에 임의 글자를 래스터화한다.
//   필요한 범위(예: 한글 음절 + ASCII)만 남기면 .data 의 폰트 바이트가 사라져 초기 다운로드/TTI 급감.
//
// ─── zero-config(Auto 모드) 철학 ───
//   모든 성능 최적화 레버는 개발자의 깊은 이해 없이 자동 적용된다: 기본 ON + 자동 안전장치.
//   폰트 subset 은 본래 lossy(보존 범위에 없는 글자는 □)지만, 다음 자동 안전장치로 정확성을 보존한다.
//     (1) 자동 대상 탐지: ≥1MB 이고 빌드에 포함될 가능성이 있는 폰트만(활성 씬 의존성 + Resources/ + TMP 소스).
//     (2) 전 프로젝트 사용 문자 스캔(AITFontUsedCharScanner): 씬/프리팹/asset/C#/로컬라이제이션 텍스트.
//     (3) ★ 블록 완성 규칙(AITFontUnicodeBlocks): 등장한 문자체계의 유니코드 블록 전체를 보존.
//         → "가" 한 글자만 써도 한글 음절 전체가 살아남아 동적 닉네임/채팅이 □ 가 되지 않는다.
//     (4) Han 예외: 한자 블록(2만+자)은 전체 대신 [감지 한자 + KS X 1001 상용 한자 4,888자 패드]만.
//     (5) 항시 베이스라인: ASCII+Latin-1+한글+CJK 기호+전각은 스캔 결과와 무관하게 항상 포함.
//     (6) 빌드 드롭 리포트: 폰트별 원본→subset 크기, 보존 블록, 감지 문자체계 요약.
//     (7) opt-out: fontSubset=0(완전 no-op). override: 수동 대상/범위 지정 시 Auto 스캔 생략.
//
//   레버 상태(tri-state): fontSubset = -1(자동, 기본) | 0(비활성) | 1(자동, 명시적 ON).
//     -1/1 → Auto 동작, 0 → 완전 no-op. 수동 필드(targetPaths/unicodeRanges)는 Auto 의 override.
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
    /// 빌드 단계 폰트 CJK subset 처리기. <see cref="AITEditorScriptObject.fontSubset"/>
    /// tri-state(-1=자동/0=비활성/1=자동 명시) 설정에 따라 동작한다.
    /// 런타임 컴포넌트는 없다(빌드 산출물만 작아질 뿐, 런타임 렌더 경로 동일).
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

        /// <summary>크기 자동 탐지 하한(바이트). 이보다 작은 폰트는 절감 효과가 미미해 대상에서 제외.</summary>
        private const long AutoTargetMinBytes = 1L * 1024 * 1024; // 1MB

        /// <summary>
        /// 빌드 직전 호출: tri-state 설정에 따라 Auto 모드(또는 수동 override)로 대상 폰트를 subset 한다.
        ///
        /// - fontSubset == 0: 완전 no-op.
        /// - fontSubset == -1/1: Auto 동작. fontSubsetTargetPaths 비면 자동 대상 탐지, 지정 시 수동 대상.
        ///   fontSubsetUnicodeRanges 비면 Auto 스캔으로 범위 결정, 지정 시 수동 범위(스캔 생략).
        /// </summary>
        /// <param name="config">프로젝트 에디터 설정. null·비활성 시 no-op.</param>
        /// <returns>복원에 사용할 핸들(항상 non-null).</returns>
        public static FontHandle ApplyForBuild(AITEditorScriptObject config)
        {
            var handle = new FontHandle();
            if (config == null)
            {
                return handle;
            }

            // tri-state: 0 = 완전 비활성(no-op), -1/1 = Auto 동작.
            bool enabled = config.fontSubset >= 0
                ? config.fontSubset == 1
                : AITDefaultSettings.GetDefaultFontSubset();
            if (!enabled)
            {
                if (config.fontSubset == 0)
                {
                    Debug.Log("[AIT-FontSubset] 폰트 subset 비활성(설정) → 풀 폰트로 빌드합니다.");
                }

                return handle;
            }

            // ── 안전 정보 수집: TMP fallback 소스 / Dynamic atlas 소스 폰트(리플렉션, TMP 미설치 안전) ──
            //   fallback 소스: '임의 언어' 글자를 렌더하려고 존재하는 폰트라 subset 하면 tofu 위험 → 자동 제외.
            //   Dynamic atlas 소스: 런타임에 소스에서 글자를 즉석 래스터화 → 보존 범위 밖 '동적' 글자 tofu 위험 → 경고.
            CollectTmpFontSafetyInfo(out var fallbackSourcePaths, out var dynamicAtlasSourcePaths);
            var excludeSet = BuildNormalizedPathSet(SplitList(config.fontSubsetExcludeTargetPaths));

            // ── 대상 폰트 결정: 수동 지정(override)이 있으면 그대로, 없으면 자동 탐지 ──
            string[] targets = SplitList(config.fontSubsetTargetPaths);
            bool manualTargets = targets != null && targets.Length > 0;
            if (!manualTargets)
            {
                targets = DetectAutoTargets();

                // 자동 탐지 대상에서 TMP fallback 소스 폰트를 제외(임의 언어 fallback 글자 tofu 방지).
                //   수동 지정(fontSubsetTargetPaths)은 개발자의 명시 의사이므로 이 자동 제외를 적용하지 않는다.
                targets = FilterOutPaths(targets, fallbackSourcePaths, "TMP fallback 소스(임의 언어 렌더) — 자동 제외");

                if (targets.Length == 0)
                {
                    Debug.Log($"[AIT-FontSubset] 자동 탐지된 subset 대상 폰트가 없습니다(≥{AutoTargetMinBytes / 1048576}MB & 빌드 포함 가능·비-fallback 폰트 없음) → no-op.");
                    return handle;
                }

                Debug.Log($"[AIT-FontSubset] 자동 탐지: subset 대상 폰트 {targets.Length}개.");
            }

            // ── escape hatch: fontSubsetExcludeTargetPaths 는 auto/manual 무관하게 항상 적용(안전 우선) ──
            targets = FilterOutPaths(targets, excludeSet, "fontSubsetExcludeTargetPaths 지정 — 제외");
            if (targets.Length == 0)
            {
                Debug.Log("[AIT-FontSubset] 제외 필터 적용 후 남은 대상 폰트가 없습니다 → no-op.");
                return handle;
            }

            // ── 보존 범위 결정: 수동 범위(override)가 있으면 그대로, 없으면 Auto 스캔으로 도출 ──
            string ranges = (config.fontSubsetUnicodeRanges ?? string.Empty).Trim();
            bool manualRanges = !string.IsNullOrEmpty(ranges);
            System.Collections.Generic.List<AITFontUnicodeBlocks.Block> preservedBlocks = null;
            int detectedCount = 0;
            int hanPadCount = 0;
            if (!manualRanges)
            {
                var detected = AITFontUsedCharScanner.ScanProject();
                detectedCount = detected.Count;
                var hanPad = AITFontUsedCharScanner.GetKsx1001Han();
                hanPadCount = hanPad.Count;
                ranges = AITFontUsedCharScanner.BuildPreservedRanges(detected, hanPad, out preservedBlocks);

                if (string.IsNullOrEmpty(ranges))
                {
                    Debug.LogWarning("[AIT-FontSubset] Auto 스캔 결과 보존 범위가 비어 있어 건너뜁니다(풀 폰트로 빌드).");
                    return handle;
                }
            }
            else
            {
                Debug.Log("[AIT-FontSubset] 수동 보존 범위 지정(override) → Auto 스캔 생략.");
            }

            // ── (additive) fontSubsetExtraRanges: auto/manual 무관하게 항상 union(override 아님) ──
            //   스캔이 놓칠 수 있는 '외부에서 동적 로드하는 다른 언어'를 개발자가 보강하는 안전 필드.
            string extraRanges = (config.fontSubsetExtraRanges ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(extraRanges))
            {
                ranges = string.IsNullOrEmpty(ranges) ? extraRanges : ranges + "," + extraRanges;
                Debug.Log($"[AIT-FontSubset] 추가 보존 범위(union) 적용: {extraRanges}");
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

                    // Dynamic atlas TMP 폰트 소스면 경고 — 런타임 즉석 래스터화 글자 중 보존 범위 밖은 □.
                    if (dynamicAtlasSourcePaths.Contains(assetPath))
                    {
                        Debug.LogWarning($"[AIT-FontSubset]   ⚠ {Path.GetFileName(assetPath)}: Dynamic atlas TMP 폰트 소스입니다. " +
                            "런타임에 소스 폰트에서 글자를 즉석 래스터화하므로, 프로젝트에 등장하지 않는 '외부/동적' 언어 글자는 " +
                            "보존 범위 밖이라 □(tofu)가 될 수 있습니다. 외부 언어 UGC 가 있으면 fontSubsetExtraRanges 로 보강하거나 " +
                            "fontSubsetExcludeTargetPaths 로 이 폰트를 제외하세요.");
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
                LogDropReport(manualTargets, manualRanges, detectedCount, hanPadCount, preservedBlocks);
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

        // ─────────────────────────── 자동 대상 탐지 ───────────────────────────

        /// <summary>
        /// 빌드에 포함될 가능성이 있는 대형(≥1MB) .ttf/.otf 폰트를 보수적으로 자동 탐지한다.
        /// 후보 출처:
        ///   - EditorBuildSettings 활성 씬들의 AssetDatabase.GetDependencies(재귀)
        ///   - Resources/ 하위(런타임 동적 로드 경로)
        ///   - TMP_FontAsset 의 소스 폰트(있으면 — 리플렉션으로 TMP 하드 의존 회피)
        /// 탐지 실패/예외 시 빈 배열을 반환한다(no-op + 로그).
        /// </summary>
        private static string[] DetectAutoTargets()
        {
            var candidates = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                // 1) 활성 씬 + 의존성(재귀).
                var scenePaths = new System.Collections.Generic.List<string>();
                foreach (var scene in EditorBuildSettings.scenes)
                {
                    if (scene != null && scene.enabled && !string.IsNullOrEmpty(scene.path))
                    {
                        scenePaths.Add(scene.path);
                    }
                }

                if (scenePaths.Count > 0)
                {
                    foreach (var dep in AssetDatabase.GetDependencies(scenePaths.ToArray(), recursive: true))
                    {
                        AddIfFontCandidate(dep, candidates);
                    }
                }

                // 2) Resources/ 하위 폰트(런타임 동적 로드 가능 경로).
                foreach (var guid in AssetDatabase.FindAssets("t:Font"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path) && path.Replace('\\', '/').Contains("/Resources/"))
                    {
                        AddIfFontCandidate(path, candidates);
                    }
                }

                // 3) TMP_FontAsset 소스 폰트(리플렉션 — TMP 미설치 환경에서도 안전).
                CollectTmpSourceFonts(candidates);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-FontSubset] 자동 대상 탐지 예외(빈 목록으로 계속): {e.Message}");
                return Array.Empty<string>();
            }

            var result = new System.Collections.Generic.List<string>();
            foreach (var path in candidates)
            {
                result.Add(path);
            }

            return result.ToArray();
        }

        /// <summary>경로가 ≥1MB 인 .ttf/.otf 폰트 에셋이면 후보 집합에 추가한다.</summary>
        private static void AddIfFontCandidate(string assetPath, System.Collections.Generic.HashSet<string> sink)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return;
            }

            string norm = assetPath.Replace('\\', '/');
            if (!norm.StartsWith("Assets/"))
            {
                return;
            }

            string ext = Path.GetExtension(norm).ToLowerInvariant();
            if (ext != ".ttf" && ext != ".otf")
            {
                return;
            }

            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string full = Path.Combine(projectRoot, norm);
                if (File.Exists(full) && new FileInfo(full).Length >= AutoTargetMinBytes)
                {
                    sink.Add(norm);
                }
            }
            catch
            {
                // 파일 접근 실패는 무시(후보 제외)
            }
        }

        /// <summary>
        /// TMP_FontAsset 의 소스 폰트(sourceFontFile)를 리플렉션으로 수집한다.
        /// TextMeshPro 미설치/타입 미발견 시 조용히 건너뛴다(하드 의존 회피).
        /// </summary>
        private static void CollectTmpSourceFonts(System.Collections.Generic.HashSet<string> sink)
        {
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset == null)
                    {
                        continue;
                    }

                    var prop = asset.GetType().GetProperty("sourceFontFile");
                    var source = prop?.GetValue(asset) as UnityEngine.Object;
                    if (source != null)
                    {
                        AddIfFontCandidate(AssetDatabase.GetAssetPath(source), sink);
                    }
                }
            }
            catch
            {
                // TMP 미설치 등 — 무시
            }
        }

        // ─────────────────────────── TMP 안전 정보 / 경로 필터 ───────────────────────────

        /// <summary>
        /// TMP 폰트의 안전 정보를 리플렉션으로 수집한다(TMP 미설치/타입 미발견 시 빈 집합).
        ///   - fallbackSourcePaths: TMP fallback(폰트별 fallbackFontAssetTable + 전역 TMP_Settings.fallbackFontAssets)
        ///     의 소스 .ttf/.otf 경로. '임의 언어' 렌더 목적이라 자동 subset 대상에서 제외한다.
        ///   - dynamicAtlasSourcePaths: atlasPopulationMode==Dynamic 인 TMP 폰트의 소스 경로. 런타임 즉석
        ///     래스터화라 보존 범위 밖 '동적' 글자가 tofu 위험 → 경고 대상.
        /// 경로는 정규화(forward-slash)되며 대소문자 무시 비교용 집합으로 반환한다.
        /// </summary>
        private static void CollectTmpFontSafetyInfo(
            out System.Collections.Generic.HashSet<string> fallbackSourcePaths,
            out System.Collections.Generic.HashSet<string> dynamicAtlasSourcePaths)
        {
            fallbackSourcePaths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            dynamicAtlasSourcePaths = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                Type fontAssetType = null;
                foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path))
                    {
                        continue;
                    }

                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset == null)
                    {
                        continue;
                    }

                    if (fontAssetType == null)
                    {
                        fontAssetType = asset.GetType();
                    }

                    // atlasPopulationMode == Dynamic → 이 폰트의 소스는 경고 대상.
                    var modeProp = asset.GetType().GetProperty("atlasPopulationMode");
                    var mode = modeProp?.GetValue(asset);
                    if (mode != null && string.Equals(mode.ToString(), "Dynamic", StringComparison.Ordinal))
                    {
                        AddTmpSourcePath(asset, dynamicAtlasSourcePaths);
                    }

                    // 폰트별 fallback 체인의 소스들 → fallback 제외 대상.
                    CollectFallbackTableSources(asset, fallbackSourcePaths);
                }

                // 전역 fallback(TMP_Settings.fallbackFontAssets).
                if (fontAssetType != null)
                {
                    CollectGlobalFallbackSources(fontAssetType, fallbackSourcePaths);
                }
            }
            catch
            {
                // TMP 미설치/리플렉션 실패 — 빈 집합으로 안전 진행.
            }
        }

        /// <summary>TMP 폰트 에셋의 sourceFontFile 경로를 정규화(forward-slash)해 집합에 추가.</summary>
        private static void AddTmpSourcePath(UnityEngine.Object tmpFontAsset, System.Collections.Generic.HashSet<string> sink)
        {
            try
            {
                var prop = tmpFontAsset.GetType().GetProperty("sourceFontFile");
                var source = prop?.GetValue(tmpFontAsset) as UnityEngine.Object;
                if (source == null)
                {
                    return;
                }

                string p = AssetDatabase.GetAssetPath(source);
                if (!string.IsNullOrEmpty(p))
                {
                    sink.Add(p.Replace('\\', '/'));
                }
            }
            catch
            {
                // 무시
            }
        }

        /// <summary>TMP 폰트의 fallbackFontAssetTable(List&lt;TMP_FontAsset&gt;) 각 원소의 소스 경로를 수집.</summary>
        private static void CollectFallbackTableSources(UnityEngine.Object tmpFontAsset, System.Collections.Generic.HashSet<string> sink)
        {
            try
            {
                var t = tmpFontAsset.GetType();
                object table = t.GetField("fallbackFontAssetTable")?.GetValue(tmpFontAsset)
                    ?? t.GetProperty("fallbackFontAssetTable")?.GetValue(tmpFontAsset);
                if (table is System.Collections.IEnumerable list)
                {
                    foreach (var item in list)
                    {
                        if (item is UnityEngine.Object fb && fb != null)
                        {
                            AddTmpSourcePath(fb, sink);
                        }
                    }
                }
            }
            catch
            {
                // 무시
            }
        }

        /// <summary>전역 TMP_Settings.fallbackFontAssets 의 소스 경로를 수집(리플렉션).</summary>
        private static void CollectGlobalFallbackSources(Type fontAssetType, System.Collections.Generic.HashSet<string> sink)
        {
            try
            {
                var settingsType = fontAssetType.Assembly.GetType("TMPro.TMP_Settings");
                var prop = settingsType?.GetProperty(
                    "fallbackFontAssets",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop?.GetValue(null) is System.Collections.IEnumerable list)
                {
                    foreach (var item in list)
                    {
                        if (item is UnityEngine.Object fb && fb != null)
                        {
                            AddTmpSourcePath(fb, sink);
                        }
                    }
                }
            }
            catch
            {
                // 무시
            }
        }

        /// <summary>경로 목록을 정규화(trim, forward-slash)해 대소문자 무시 집합으로. null/빈 입력은 빈 집합.</summary>
        private static System.Collections.Generic.HashSet<string> BuildNormalizedPathSet(string[] paths)
        {
            var set = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (paths != null)
            {
                foreach (var p in paths)
                {
                    string n = (p ?? string.Empty).Trim().Replace('\\', '/');
                    if (n.Length > 0)
                    {
                        set.Add(n);
                    }
                }
            }

            return set;
        }

        /// <summary>targets 에서 excludeSet 에 속한 경로를 제거하고 로그를 남긴다. excludeSet 이 비면 원본 그대로.</summary>
        private static string[] FilterOutPaths(string[] targets, System.Collections.Generic.HashSet<string> excludeSet, string reasonLabel)
        {
            if (targets == null || targets.Length == 0 || excludeSet == null || excludeSet.Count == 0)
            {
                return targets ?? Array.Empty<string>();
            }

            var kept = new System.Collections.Generic.List<string>(targets.Length);
            foreach (var raw in targets)
            {
                string n = (raw ?? string.Empty).Trim().Replace('\\', '/');
                if (n.Length == 0)
                {
                    continue;
                }

                if (excludeSet.Contains(n))
                {
                    Debug.Log($"[AIT-FontSubset]   대상 제외({reasonLabel}): {n}");
                    continue;
                }

                kept.Add(raw);
            }

            return kept.ToArray();
        }

        // ─────────────────────────── 드롭 리포트 ───────────────────────────

        /// <summary>
        /// 빌드 로그에 subset 드롭 리포트를 출력한다(자동 안전장치 가시화).
        /// 보존 블록 목록(이름+범위), 감지된 문자체계 요약, 수동 override 안내를 포함한다.
        /// </summary>
        private static void LogDropReport(
            bool manualTargets,
            bool manualRanges,
            int detectedCodepoints,
            int hanPadCount,
            System.Collections.Generic.List<AITFontUnicodeBlocks.Block> preservedBlocks)
        {
            try
            {
                string mode = manualRanges ? "수동 범위(override)" : "Auto 스캔";
                Debug.Log($"[AIT-FontSubset] ── 드롭 리포트 ── (대상: {(manualTargets ? "수동 지정" : "자동 탐지")}, 범위: {mode})");

                if (manualRanges)
                {
                    Debug.Log("[AIT-FontSubset]   수동 보존 범위가 적용되었습니다. 동적 텍스트 글자가 누락되지 않도록 범위를 직접 검토하세요.");
                    return;
                }

                Debug.Log($"[AIT-FontSubset]   스캔 감지 문자(비ASCII 고유): {detectedCodepoints}개, 한자 패드(KS X 1001): {hanPadCount}자");

                if (preservedBlocks != null && preservedBlocks.Count > 0)
                {
                    Debug.Log($"[AIT-FontSubset]   보존 블록(블록 완성 규칙으로 전체 보존) {preservedBlocks.Count}개:");
                    foreach (var b in preservedBlocks)
                    {
                        Debug.Log($"[AIT-FontSubset]     - {b.Name} ({AITFontUnicodeBlocks.FormatCodepoint(b.Start)}-{AITFontUnicodeBlocks.FormatCodepoint(b.End)})");
                    }
                }
                else
                {
                    Debug.Log("[AIT-FontSubset]   보존 블록 없음(베이스라인 + 감지 한자만 보존).");
                }

                Debug.Log("[AIT-FontSubset]   프로젝트에 등장한 문자체계는 (블록 완성으로) 동적 텍스트도 □ 가 되지 않습니다.");
                Debug.Log("[AIT-FontSubset]   ⚠ 외부/서버에서 '프로젝트에 없는 다른 언어'를 동적 로드한다면 fontSubsetExtraRanges 에 그 범위를 추가하세요(union).");
                Debug.Log("[AIT-FontSubset]   특정 폰트를 subset 에서 빼려면 fontSubsetExcludeTargetPaths, 수동 범위/대상은 fontSubsetUnicodeRanges/fontSubsetTargetPaths 를 쓰세요.");
                Debug.Log("[AIT-FontSubset]   (TMP fallback 소스 폰트는 자동 제외, Dynamic atlas 소스 폰트는 위 로그에 ⚠ 로 표시됩니다.)");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-FontSubset] 드롭 리포트 출력 예외(무시): {e.Message}");
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
