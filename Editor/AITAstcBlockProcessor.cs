// -----------------------------------------------------------------------
// <copyright file="AITAstcBlockProcessor.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - ASTC 블록 에스컬레이션 (build-time importer override)
// </copyright>
// -----------------------------------------------------------------------
//
// 빌드 직전, 대상 Texture2D / SpriteAtlas 의 임포터 설정을 일시적으로
//   WebGL 플랫폼 오버라이드 format=ASTC_NxN + maxTextureSize 캡
// 으로 바꿔 reimport 하여, Unity 가 더 작은 ASTC 텍스처를 .data 에 굽도록 한다.
// 빌드 종료(성공/실패 무관) 후 원본 임포트 설정으로 원상 복원한다.
//
// 효과: ASTC_12x12 는 ASTC_4x4(8bpp) 대비 ~2.25×(0.89bpp) 압축률. .data on-wire 크기 감소.
//   WebGL 플랫폼 오버라이드(overridden=true/format/maxTextureSize)를 직접 기록하여
//   빌드 시 해당 포맷이 그대로 사용된다. Texture2D 의 비압축(Uncompressed) 에셋은
//   RGBA32 팽창 위험이 있으므로 skip.
//   SpriteAtlas 는 WebGL 플랫폼 설정(GetPlatformSettings("WebGL"))에 동일 적용 후 repack.
//   폰트/SDF/TextMeshPro 에셋은 텍스트 품질 보호를 위해 내장 휴리스틱으로 항상 제외.
//
// 비파괴: 각 에셋의 원본 .meta(텍스처) / .spriteatlasv2(아틀라스) 를 <path>.aitastcbak 로 백업,
//   빌드 후 원본을 그대로 복원한다. 빌드가 비정상 종료되어 복원이 누락돼도, 다음 에디터 로드 시
//   안전망(SafetyNetRestore)이 잔존 백업을 자동 복원한다.
//   (crunch 기능의 .aittexbak / .ait-texcrunch-active 와 상호 간섭 없음)
//
// 통합: AITConvertCore.BuildWebGL 가 BuildPipeline.BuildPlayer 직전에 ApplyForBuild,
//   try/finally 의 finally 에서 RestoreForBuild 를 호출한다.
//
// ⚠ 비용: ASTC reimport 는 무겁다(에셋 수에 비례). 빌드 시 apply + 복원으로 2회 reimport 가
//   발생한다. 기본은 자동 ON이며, 대용량 ASTC 텍스처 프로젝트에서 다운로드/.data 를 실감한다.
//   필요시 AIT Configuration에서 비활성화할 수 있다.
//   ASTC 서브타겟 전용 — DXT 서브타겟 프로젝트에서는 crunch 를 사용할 것.

using System;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 빌드 단계 ASTC 블록 에스컬레이션 처리기. <see cref="AITEditorScriptObject.astcBlockEscalation"/>
    /// 설정에 따라 동작한다. 런타임 컴포넌트는 없다(빌드 산출물만 작아질 뿐, 런타임 동작 동일).
    /// ASTC 서브타겟 전용 — DXT 서브타겟이면 skip.
    /// </summary>
    [InitializeOnLoad]
    public static class AITAstcBlockProcessor
    {
        /// <summary>원본 .meta / .spriteatlasv2 를 보관하는 백업 접미사.</summary>
        private const string BackupSuffix = ".aitastcbak";

        /// <summary>apply 가 진행 중임을 표시하는 마커(Unity 가 무시하는 '.' 접두 숨김 파일).</summary>
        private const string MarkerRelative = "Assets/.ait-astcblock-active";

        /// <summary>후보 스캔 상한. 이 수를 넘으면 스캔을 중단하고 "N+개" 로 표기한다.</summary>
        private const int CandidateScanLimit = 2000;

        /// <summary>한 번의 ASTC 블록 적용 결과 핸들. finally 에서 정확한 복원에 사용.</summary>
        public sealed class AstcBlockHandle
        {
            /// <summary>이번 빌드에서 ASTC 블록 에스컬레이션이 실제로 수행되었는지.</summary>
            public bool Active;

            /// <summary>처리된 텍스처 개수.</summary>
            public int TextureCount;

            /// <summary>처리된 SpriteAtlas 개수.</summary>
            public int AtlasCount;
        }

        static AITAstcBlockProcessor()
        {
            EditorApplication.delayCall += SafetyNetRestore;
        }

        /// <summary>
        /// 빌드 직전 호출: 설정이 켜져 있으면 대상 텍스처/아틀라스를 ASTC_NxN 으로 reimport 한다.
        /// 설정이 꺼져 있으면 ASTC 서브타겟일 때만 후보 스캔 후 절감 가능 여부를 Debug.Log 1줄로 안내한다.
        /// </summary>
        /// <param name="config">프로젝트 에디터 설정. null 이거나 기능이 꺼져 있으면 no-op(단, 후보 안내는 수행).</param>
        /// <returns>복원에 사용할 핸들(항상 non-null).</returns>
        public static AstcBlockHandle ApplyForBuild(AITEditorScriptObject config)
        {
            var handle = new AstcBlockHandle();

            bool enabled = EffectiveEnabled(config);
            if (config == null || !enabled)
            {
                // ASTC 비활성화 — ASTC 서브타겟일 때만 후보 스캔 후 절감 가능 여부를 1줄 안내(후보 0이면 침묵).
                if (config != null)
                {
                    ReportCandidateHint(config);
                }

                return handle;
            }

            // 서브타겟 게이트: ASTC 전용, DXT 서브타겟이면 경고 후 skip.
#if UNITY_2022_3_OR_NEWER
            if (EditorUserBuildSettings.webGLBuildSubtarget != WebGLTextureSubtarget.ASTC)
            {
                Debug.LogWarning("[AIT-AstcBlock] WebGL Texture Compression이 DXT(기본) 서브타겟입니다. " +
                    "ASTC 블록 에스컬레이션은 ASTC 서브타겟 전용이며 DXT 서브타겟에서는 동작하지 않습니다. " +
                    "DXT 환경에서는 crunch 를 사용하세요.");
                return handle;
            }
#else
            // Unity 2021.3: WebGLTextureSubtarget API 미지원 — 서브타겟 감지 불가.
            Debug.LogWarning("[AIT-AstcBlock] Unity 2021.3에서는 WebGL 서브타겟 감지가 지원되지 않습니다. " +
                "ASTC 블록 에스컬레이션을 건너뜁니다. Unity 2022.3 이상에서 사용하세요.");
            return handle;
#endif

            try
            {
                int blockSize = config.astcBlockSize;
                int maxCap = config.astcBlockMaxSize;       // 0 = 캡 안 함
                bool doAtlas = config.astcBlockAtlas;
                string[] dirs = SplitDirs(config.astcBlockDirs);           // null = 전체 Assets
                string[] excludeDirs = SplitDirs(config.astcBlockExcludeDirs);
                var targetFormat = BlockFormatFor(blockSize);

                CreateMarker();

                int tex = ApplyTexture2D(targetFormat, maxCap, dirs, excludeDirs);
                int atl = doAtlas ? ApplySpriteAtlases(targetFormat, maxCap, dirs, excludeDirs) : 0;

                AssetDatabase.Refresh();

                handle.Active = (tex + atl) > 0;
                handle.TextureCount = tex;
                handle.AtlasCount = atl;

                if (!handle.Active)
                {
                    // 대상 0건 → 마커 제거(복원할 것 없음).
                    RemoveMarker();
                }

                string capStr = maxCap > 0 ? maxCap.ToString() : "원본";
                Debug.Log($"[AIT-AstcBlock] ✓ 텍스처 {tex}개 + 아틀라스 {atl}개 ASTC_{blockSize}x{blockSize} 적용(maxSize 캡={capStr}){(config.astcBlockEscalation < 0 ? " (자동)" : "")}.");
                return handle;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT-AstcBlock] 적용 예외 → 복원 후 건너뜀: {e}");
                RestoreAllBackups();
                RemoveMarker();
                AssetDatabase.Refresh();
                return new AstcBlockHandle();
            }
        }

        /// <summary>빌드 종료 후(성공/실패 무관) 호출: 원본 임포트 설정으로 복원한다.</summary>
        public static void RestoreForBuild(AstcBlockHandle handle)
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
                Debug.Log($"[AIT-AstcBlock] 복원 완료: {restored}개 에셋 원본 임포트 설정 원상.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT-AstcBlock] 복원 예외: {e}");
            }
        }

        /// <summary>에디터 로드 시 안전망. 마커가 잔존하면(=이전 빌드가 복원 전에 종료) 백업을 자동 복원.</summary>
        private static void SafetyNetRestore()
        {
            try
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string markerFull = Path.Combine(projectRoot, MarkerRelative);
                if (!File.Exists(markerFull))
                {
                    return; // 공통 경로: 잔존물 없음(빠른 반환)
                }

                int restored = RestoreAllBackups();
                RemoveMarker();
                if (restored > 0)
                {
                    AssetDatabase.Refresh();
                    Debug.LogWarning($"[AIT-AstcBlock] 안전망: 이전 빌드 잔존 백업 {restored}개를 복원했습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-AstcBlock] 안전망 복원 중 예외(무시): {e}");
            }
        }

        // ─────────────────────────── 후보 스캔 안내 ───────────────────────────

        /// <summary>
        /// ASTC 비활성화 빌드에서 절감 후보 텍스처 수를 빠르게 세어
        /// ASTC 서브타겟일 때만, 후보가 1개 이상이면 Debug.Log 1줄로 재활성화 안내를 출력한다.
        /// 스캔 비용을 제한하기 위해 <see cref="CandidateScanLimit"/>개를 초과하면 중단.
        /// </summary>
        private static void ReportCandidateHint(AITEditorScriptObject config)
        {
            try
            {
#if UNITY_2022_3_OR_NEWER
                // ASTC 서브타겟이 아닌 경우 안내 불필요(crunch 대상).
                if (EditorUserBuildSettings.webGLBuildSubtarget != WebGLTextureSubtarget.ASTC)
                {
                    return;
                }
#else
                // Unity 2021.3: 서브타겟 감지 불가 — 안내 생략.
                return;
#endif

                string[] dirs = SplitDirs(config.astcBlockDirs);
                string[] excludeDirs = SplitDirs(config.astcBlockExcludeDirs);
                var targetFormat = BlockFormatFor(config.astcBlockSize);
                int maxCap = config.astcBlockMaxSize;

                var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });

                int candidates = 0;
                int scanned = 0;
                bool hitLimit = false;

                foreach (var g in guids)
                {
                    if (scanned >= CandidateScanLimit)
                    {
                        hitLimit = true;
                        break;
                    }

                    string path = AssetDatabase.GUIDToAssetPath(g);
                    if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
                    {
                        continue;
                    }

                    if (dirs != null && !UnderAny(path, dirs))
                    {
                        continue;
                    }

                    if (IsExcludedPath(path, excludeDirs))
                    {
                        continue;
                    }

                    var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (ti == null)
                    {
                        continue;
                    }

                    if (ti.textureCompression == TextureImporterCompression.Uncompressed)
                    {
                        continue;
                    }

                    var masterPs = ti.GetPlatformTextureSettings("DefaultTexturePlatform");
                    var webglPs = ti.GetPlatformTextureSettings("WebGL");
                    bool isOverridden = webglPs.overridden;
                    int curMax = (isOverridden && webglPs.maxTextureSize > 0)
                        ? webglPs.maxTextureSize
                        : (masterPs.maxTextureSize > 0 ? masterPs.maxTextureSize : 2048);
                    int targetMax = ResolveTargetMaxSize(curMax, maxCap);

                    if (WouldChange(isOverridden, webglPs.format, curMax, targetFormat, targetMax))
                    {
                        candidates++;
                    }

                    scanned++;
                }

                if (candidates > 0)
                {
                    string countStr = hitLimit ? $"{CandidateScanLimit}+개" : $"{candidates}개";
                    Debug.Log($"[AIT] ASTC 블록 에스컬레이션 비활성화됨: 후보 {countStr} 존재. " +
                        "AIT Configuration에서 자동/활성화로 되돌리면 .data 크기를 줄일 수 있습니다" +
                        "(품질 트레이드오프 있음 — lossy).");
                }
            }
            catch (Exception e)
            {
                // 후보 스캔 실패는 빌드 차단이 아님(안내용).
                Debug.LogWarning($"[AIT-AstcBlock] 후보 스캔 중 예외(무시): {e.Message}");
            }
        }

        // ─────────────────────────── Texture2D ───────────────────────────

        private static int ApplyTexture2D(TextureImporterFormat targetFormat, int maxCap, string[] dirs, string[] excludeDirs)
        {
            int n = 0;
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;

            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
                {
                    continue;
                }

                if (dirs != null && !UnderAny(path, dirs))
                {
                    continue;
                }

                if (IsExcludedPath(path, excludeDirs))
                {
                    continue;
                }

                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                {
                    continue;
                }

                // 비압축(RGBA32 등) 텍스처는 ASTC 강제 시 오히려 팽창할 수 있으므로 skip.
                if (ti.textureCompression == TextureImporterCompression.Uncompressed)
                {
                    continue;
                }

                var masterPs = ti.GetPlatformTextureSettings("DefaultTexturePlatform");
                var ps = ti.GetPlatformTextureSettings("WebGL");
                bool wasOverridden = ps.overridden;

                // 현재 maxTextureSize 해석(스파이크와 동일):
                //   WebGL 오버라이드가 있고 >0 이면 그 값, 아니면 DefaultTexturePlatform 마스터 >0 이면 그 값, 아니면 2048.
                int curMax = (wasOverridden && ps.maxTextureSize > 0)
                    ? ps.maxTextureSize
                    : (masterPs.maxTextureSize > 0 ? masterPs.maxTextureSize : 2048);

                // 현재 WebGL 오버라이드 format 이 비압축 계열이면 skip(팽창 방지).
                if (wasOverridden && IsUncompressedFormat(ps.format))
                {
                    continue;
                }

                int targetMax = ResolveTargetMaxSize(curMax, maxCap);

                // 이미 목표 상태이면 skip(재진입 비용 회피).
                if (!WouldChange(wasOverridden, ps.format, curMax, targetFormat, targetMax))
                {
                    continue;
                }

                // 원본 .meta 백업(verbatim 복원용).
                if (!BackupAssetMeta(path, projectRoot))
                {
                    continue;
                }

                ps.overridden = true;
                ps.format = targetFormat;
                ps.maxTextureSize = targetMax;
                ti.SetPlatformTextureSettings(ps);
                ti.SaveAndReimport();
                n++;
            }

            return n;
        }

        // ─────────────────────────── SpriteAtlas ───────────────────────────

        private static int ApplySpriteAtlases(TextureImporterFormat targetFormat, int maxCap, string[] dirs, string[] excludeDirs)
        {
            int n = 0;
            var guids = AssetDatabase.FindAssets("t:SpriteAtlas", new[] { "Assets" });
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;

            foreach (var g in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(g);
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/"))
                {
                    continue;
                }

                if (dirs != null && !UnderAny(path, dirs))
                {
                    continue;
                }

                if (IsExcludedPath(path, excludeDirs))
                {
                    continue;
                }

                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                if (atlas == null)
                {
                    continue;
                }

                // SpriteAtlas 의 WebGL 플랫폼 설정에 오버라이드 적용(Texture2D 경로와 동일 구조).
                var masterPs = atlas.GetPlatformSettings("DefaultTexturePlatform");
                var ws = atlas.GetPlatformSettings("WebGL");
                bool wasOverridden = ws.overridden;

                // 현재 maxTextureSize 해석: 기존 WebGL 오버라이드(overridden=true, >0)가 있으면 빌드는 그것을
                // 우선하므로 그 값을, 없으면 DefaultTexturePlatform 마스터(>0), 둘 다 없으면 2048.
                // 마스터 기준으로만 해석하면 더 작은 오버라이드 캡(예: 512)을 마스터(4096)로 부풀려
                // 덮어써 아틀라스가 최대 64× 비대해진다(#8 plat-override-ignored).
                int curMax = (wasOverridden && ws.maxTextureSize > 0)
                    ? ws.maxTextureSize
                    : (masterPs.maxTextureSize > 0 ? masterPs.maxTextureSize : 2048);

                // 기존 WebGL 오버라이드 format 이 비압축 계열이면 ASTC 강제 시 팽창 → skip.
                if (wasOverridden && IsUncompressedFormat(ws.format))
                {
                    continue;
                }

                int targetMax = ResolveTargetMaxSize(curMax, maxCap);

                // 이미 목표 상태이면 skip(재진입 비용 회피).
                if (!WouldChange(wasOverridden, ws.format, curMax, targetFormat, targetMax))
                {
                    continue;
                }

                // 아틀라스 에셋 파일 자체를 백업(플랫폼 설정이 .spriteatlasv2 안에 직렬화됨).
                if (!BackupAssetFile(path, projectRoot))
                {
                    continue;
                }

                ws.overridden = true;
                ws.format = targetFormat;
                ws.maxTextureSize = targetMax;
                atlas.SetPlatformSettings(ws);
                EditorUtility.SetDirty(atlas);
                n++;
            }

            if (n > 0)
            {
                AssetDatabase.SaveAssets();
                try
                {
                    SpriteAtlasUtility.PackAllAtlases(BuildTarget.WebGL);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-AstcBlock]   PackAllAtlases 경고: {e.Message}");
                }
            }

            return n;
        }

        // ─────────────────────────── 백업/복원 ───────────────────────────

        /// <summary>에셋의 .meta 파일을 <path>.meta.aitastcbak 로 백업.</summary>
        private static bool BackupAssetMeta(string assetPath, string projectRoot)
        {
            try
            {
                string metaFull = Path.Combine(projectRoot, assetPath + ".meta");
                if (!File.Exists(metaFull))
                {
                    return false;
                }

                string bak = metaFull + BackupSuffix;
                if (!File.Exists(bak))
                {
                    File.Copy(metaFull, bak, true);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-AstcBlock] .meta 백업 실패({assetPath}): {e.Message}");
                return false;
            }
        }

        /// <summary>에셋 본체 파일을 <path>.aitastcbak 로 백업(SpriteAtlas 용).</summary>
        private static bool BackupAssetFile(string assetPath, string projectRoot)
        {
            try
            {
                string full = Path.Combine(projectRoot, assetPath);
                if (!File.Exists(full))
                {
                    return false;
                }

                string bak = full + BackupSuffix;
                if (!File.Exists(bak))
                {
                    File.Copy(full, bak, true);
                }

                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-AstcBlock] 에셋 백업 실패({assetPath}): {e.Message}");
                return false;
            }
        }

        /// <summary>Assets 트리의 모든 *.aitastcbak 를 원본으로 되돌리고 백업을 삭제한다. 복원 개수 반환.</summary>
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
                // bak = "<original>.aitastcbak" → original 은 .meta(텍스처) 또는 .spriteatlasv2(아틀라스).
                string original = bak.Substring(0, bak.Length - BackupSuffix.Length);
                try
                {
                    File.Copy(bak, original, true);
                    File.Delete(bak);

                    // reimport 대상 에셋 경로 산출: .meta 면 본체, 아니면 그 파일 자체.
                    string assetFull = original.EndsWith(".meta")
                        ? original.Substring(0, original.Length - ".meta".Length)
                        : original;
                    string rel = AbsoluteToProjectRelative(assetFull, projectRoot);
                    if (!string.IsNullOrEmpty(rel))
                    {
                        AssetDatabase.ImportAsset(rel, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    }

                    restored++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[AIT-AstcBlock] 백업 복원 실패({bak}): {e.Message}");
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
                // 마커 생성 실패는 치명적이지 않음(안전망 가속용일 뿐).
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

        private static string AbsoluteToProjectRelative(string absolute, string projectRoot)
        {
            string norm = absolute.Replace('\\', '/');
            string root = projectRoot.Replace('\\', '/').TrimEnd('/') + "/";
            return norm.StartsWith(root) ? norm.Substring(root.Length) : null;
        }

        // ─────────────────────────── 순수 내부 헬퍼 (테스트용, 에셋 DB 비의존) ───────────────────────────

        /// <summary>
        /// ASTC 블록 에스컬레이션 실효 활성 여부를 반환한다(tri-state 해석).
        /// null → false, astcBlockEscalation >= 0 → ==1, &lt;0 → GetDefaultAstcBlockEscalation().
        /// </summary>
        internal static bool EffectiveEnabled(AITEditorScriptObject config)
        {
            if (config == null) return false;
            return config.astcBlockEscalation >= 0
                ? config.astcBlockEscalation == 1
                : AITDefaultSettings.GetDefaultAstcBlockEscalation();
        }

        /// <summary>
        /// ASTC 블록 크기(4/5/6/8/10/12)를 <see cref="TextureImporterFormat"/> 으로 변환한다.
        /// 그 외 값은 ASTC_12x12 로 폴백.
        /// </summary>
        internal static TextureImporterFormat BlockFormatFor(int block)
        {
            switch (block)
            {
                case 4:  return TextureImporterFormat.ASTC_4x4;
                case 5:  return TextureImporterFormat.ASTC_5x5;
                case 6:  return TextureImporterFormat.ASTC_6x6;
                case 8:  return TextureImporterFormat.ASTC_8x8;
                case 10: return TextureImporterFormat.ASTC_10x10;
                case 12: return TextureImporterFormat.ASTC_12x12;
                default: return TextureImporterFormat.ASTC_12x12;
            }
        }

        /// <summary>
        /// 경로가 제외 대상인지 판별한다.
        /// 내장 휴리스틱: 소문자 경로에 "/font", " sdf", "textmesh" 포함.
        /// 추가: <paramref name="excludeDirs"/> 접두 일치(대소문자 무시).
        /// </summary>
        internal static bool IsExcludedPath(string assetPath, string[] excludeDirs)
        {
            // 사용자 제외 폴더 접두 일치 확인.
            if (excludeDirs != null)
            {
                foreach (var d in excludeDirs)
                {
                    var t = d.Trim().TrimEnd('/');
                    if (string.IsNullOrEmpty(t))
                    {
                        continue;
                    }

                    if (assetPath.StartsWith(t + "/", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(assetPath, t, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }

            // 내장 휴리스틱: 폰트/SDF/TextMeshPro (TMP 아틀라스 명명 규약 포함).
            // 경로 세그먼트에 "font" 가 포함된 디렉터리, 파일명에 " sdf", "textmesh" 포함 여부를 검사한다.
            var low = assetPath.ToLowerInvariant();
            // " sdf", "textmesh" 는 전체 경로 문자열에서 검사.
            if (low.Contains(" sdf") || low.Contains("textmesh")) return true;
            // "font" 는 슬래시로 나뉜 각 경로 세그먼트가 해당 단어를 포함하는지 검사(예: "8.Fonts" 포함).
            var segments = low.Split('/');
            for (int i = 0; i < segments.Length - 1; i++) // 파일명(마지막 세그먼트) 제외, 디렉터리만 검사.
            {
                if (segments[i].Contains("font")) return true;
            }
            return false;
        }

        /// <summary>
        /// 목표 maxTextureSize 를 결정한다.
        /// <paramref name="cap"/> &lt;= 0 이면 캡 없음(currentMax 그대로).
        /// 그 외 <paramref name="currentMax"/> 와 <paramref name="cap"/> 중 작은 값.
        /// </summary>
        internal static int ResolveTargetMaxSize(int currentMax, int cap)
        {
            if (cap <= 0)
            {
                return currentMax;
            }

            return Math.Min(currentMax, cap);
        }

        /// <summary>
        /// 현재 상태가 이미 목표 상태인지 판별한다.
        /// overridden=true + format==targetFormat + max==targetMax 이면 변경 불필요.
        /// </summary>
        internal static bool WouldChange(bool overridden, TextureImporterFormat currentFormat, int currentMax, TextureImporterFormat targetFormat, int targetMax)
        {
            if (!overridden)
            {
                return true;
            }

            if (currentFormat != targetFormat)
            {
                return true;
            }

            if (currentMax != targetMax)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 비압축 계열 format 인지 판별한다.
        /// RGBA32/ARGB32/RGB24/Alpha8/R8/R16/RGBAHalf/RGBAFloat/RGB48/RGBA64 등 비압축 포맷.
        /// </summary>
        internal static bool IsUncompressedFormat(TextureImporterFormat f)
        {
            switch (f)
            {
                case TextureImporterFormat.RGBA32:
                case TextureImporterFormat.ARGB32:
                case TextureImporterFormat.RGB24:
                case TextureImporterFormat.Alpha8:
                case TextureImporterFormat.R8:
                case TextureImporterFormat.R16:
                case TextureImporterFormat.RGBAHalf:
                case TextureImporterFormat.RGBAFloat:
                case TextureImporterFormat.RGB48:
                case TextureImporterFormat.RGBA64:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>쉼표 구분 문자열을 배열로 분리. null/빈 문자열이면 null 반환.</summary>
        internal static string[] SplitDirs(string v)
            => string.IsNullOrEmpty(v) ? null : v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        /// <summary>경로가 dirs 중 어느 하나의 하위에 있는지 확인.</summary>
        internal static bool UnderAny(string path, string[] dirs)
        {
            foreach (var d in dirs)
            {
                var t = d.Trim().TrimEnd('/');
                if (path.StartsWith(t + "/") || path == t)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
