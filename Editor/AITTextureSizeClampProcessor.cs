// -----------------------------------------------------------------------
// <copyright file="AITTextureSizeClampProcessor.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Texture Size Clamp (build-time importer override, crunch-decoupled)
// </copyright>
// -----------------------------------------------------------------------
//
// 빌드 직전, 대상 Texture2D 의 임포터 설정 중 maxTextureSize "만" 일시적으로 캡(상한)으로
//   낮춰 reimport 하여, Unity 가 더 작은 텍셀의 텍스처를 .data 에 굽게 한다.
//   빌드 종료(성공/실패 무관) 후 원본 임포트 설정으로 원상 복원한다.
//
// crunch 와의 차이(분리된 이유):
//   AITTextureCrunchProcessor 는 maxTextureSize 캡을 crunch(=DXT 강제 + crunchedCompression=true +
//   textureCompression/compressionQuality 변경)와 한 게이트(textureCrunch)에 묶어 둔다.
//   즉 "크기만 줄이고 싶은데 DXT-crunch 는 원치 않는" 경우(이미 ASTC 로 굽거나, crunch reimport
//   비용/화질 변화를 피하고 싶은 경우)에는 쓸 수 없다. 본 처리기는 format/compression/crunch 를
//   일절 건드리지 않고 maxTextureSize 한 필드만 override 하므로, 빌드 파이프라인이 정하는 기본
//   format(WebGL fallback = ASTC/DXT)을 그대로 유지한 채 텍셀 수만 줄인다.
//
// 효과: 부팅 텍스처를 예) 1024 로 캡하면 2048→1024 는 텍셀 1/4. 압축 포맷(ASTC/DXT)은 텍셀당
//   고정 비트레이트이므로 압축 payload 도 ~1/4, on-wire(brotli) 도 감소하고 GPU 업로드 tail 도 일부
//   완화된다. 단, 표시 해상도가 절반으로 떨어지는 lossy 변경(시각 품질 저하)이므로 기본 비활성이다.
//
// 비파괴: 각 텍스처의 원본 .meta 를 <path>.meta.aittexclampbak 로 백업, 빌드 후 원본을 그대로
//   복원한다. 빌드가 비정상 종료되어 복원이 누락돼도, 다음 에디터 로드 시 안전망(SafetyNetRestore)이
//   잔존 백업을 자동 복원한다.
//
// 통합: AITConvertCore.BuildWebGL 가 BuildPipeline.BuildPlayer 직전에 ApplyForBuild,
//   try/finally 의 finally 에서 RestoreForBuild 를 호출한다(텍스처 crunch 와 동일 패턴).
//   적용 순서는 crunch 직후(=crunch 가 같은 텍스처에 maxSize 를 더 낮게 캡했다면 그 값이 우선이도록
//   wouldChange 게이트가 더 큰 쪽으로만 내린다). 복원은 적용 역순.
//
// ⚠ 비용: maxTextureSize 변경은 reimport 를 유발한다(crunch 처럼 codec 재인코딩까지는 아니지만
//   대상 수에 비례). apply + 복원으로 2회 reimport 가 발생하므로 명시적 opt-in 으로 기본 비활성이다.

using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 빌드 단계 텍스처 maxTextureSize 캡(crunch 비결합) 처리기.
    /// <see cref="AITEditorScriptObject.textureSizeClamp"/> 설정에 따라 동작한다.
    /// format/compression/crunch 는 건드리지 않고 maxTextureSize 한 필드만 override 한다.
    /// 런타임 컴포넌트는 없다(빌드 산출물만 작아질 뿐, 런타임 동작 동일).
    /// </summary>
    [InitializeOnLoad]
    public static class AITTextureSizeClampProcessor
    {
        /// <summary>원본 .meta 를 보관하는 백업 접미사.</summary>
        private const string BackupSuffix = ".aittexclampbak";

        /// <summary>apply 가 진행 중임을 표시하는 마커(Unity 가 무시하는 '.' 접두 숨김 파일).</summary>
        private const string MarkerRelative = "Assets/.ait-texclamp-active";

        /// <summary>캡 최소 하한. 너무 작은 값으로 인한 사고 방지(NPOT/타일 등). 16 미만은 무시.</summary>
        private const int MinClampSize = 16;

        /// <summary>한 번의 클램프 적용 결과 핸들. finally 에서 정확한 복원에 사용.</summary>
        public sealed class ClampHandle
        {
            /// <summary>이번 빌드에서 클램프가 실제로 수행되었는지.</summary>
            public bool Active;

            /// <summary>처리된 텍스처 개수.</summary>
            public int TextureCount;
        }

        static AITTextureSizeClampProcessor()
        {
            EditorApplication.delayCall += SafetyNetRestore;
        }

        /// <summary>
        /// 빌드 직전 호출: 설정이 켜져 있으면 대상 텍스처의 maxTextureSize 만 캡으로 낮춰 reimport 한다.
        /// </summary>
        /// <param name="config">프로젝트 에디터 설정. null 이거나 기능이 꺼져 있으면 no-op.</param>
        /// <returns>복원에 사용할 핸들(항상 non-null).</returns>
        public static ClampHandle ApplyForBuild(AITEditorScriptObject config)
        {
            var handle = new ClampHandle();

            // tri-state 게이트: -1 = 자동(AITDefaultSettings 기본값), 0 = 비활성, 1 = 활성.
            bool enabled = config != null && (config.textureSizeClamp >= 0
                ? config.textureSizeClamp == 1
                : AITDefaultSettings.GetDefaultTextureSizeClamp());
            if (!enabled)
            {
                return handle;
            }

            try
            {
                int clampMax = config.textureClampMaxSize;       // 0 이하 또는 하한 미만 = no-op
                if (clampMax < MinClampSize)
                {
                    Debug.LogWarning($"[AIT-TextureSizeClamp] textureClampMaxSize({clampMax}) < 하한({MinClampSize}) → 건너뜀(no-op).");
                    return handle;
                }

                long minBytes = config.textureClampMinBytes > 0 ? config.textureClampMinBytes : 0; // 0 = 크기 필터 없음
                string[] dirs = SplitDirs(config.textureClampDirs);              // null = 전체 Assets
                string[] excludeDirs = SplitDirs(config.textureClampExcludeDirs); // 사용자 escape hatch

                CreateMarker();

                int tex = ApplyTexture2D(clampMax, minBytes, dirs, excludeDirs);

                AssetDatabase.Refresh();

                handle.Active = tex > 0;
                handle.TextureCount = tex;
                if (!handle.Active)
                {
                    // 대상 0건 → 마커 제거(복원할 것 없음).
                    RemoveMarker();
                }

                Debug.Log($"[AIT-TextureSizeClamp] ✓ 텍스처 {tex}개 maxTextureSize≤{clampMax} 캡(format/crunch 불변) 적용{(config.textureSizeClamp < 0 ? " (자동)" : "")}.");
                return handle;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT-TextureSizeClamp] 적용 예외 → 복원 후 건너뜀: {e}");
                RestoreAllBackups();
                RemoveMarker();
                AssetDatabase.Refresh();
                return new ClampHandle();
            }
        }

        /// <summary>빌드 종료 후(성공/실패 무관) 호출: 원본 임포트 설정으로 복원한다.</summary>
        public static void RestoreForBuild(ClampHandle handle)
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
                Debug.Log($"[AIT-TextureSizeClamp] 복원 완료: {restored}개 텍스처 원본 임포트 설정 원상.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT-TextureSizeClamp] 복원 예외: {e}");
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
                    Debug.LogWarning($"[AIT-TextureSizeClamp] 안전망: 이전 빌드 잔존 백업 {restored}개를 복원했습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-TextureSizeClamp] 안전망 복원 중 예외(무시): {e}");
            }
        }

        // ─────────────────────────── Texture2D ───────────────────────────
        private static int ApplyTexture2D(int clampMax, long minBytes, string[] dirs, string[] excludeDirs)
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

                // 다른 처리기의 StreamingAssets 사본은 건드리지 않음(자기 외부화본 보호).
                if (path.IndexOf("/ait-stream-", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                if (dirs != null && !UnderAny(path, dirs))
                {
                    continue;
                }

                if (excludeDirs != null && UnderAny(path, excludeDirs))
                {
                    continue; // 사용자 escape hatch
                }

                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                {
                    continue;
                }

                // maxTextureSize 가 이미 캡 이하면 줄일 것이 없음(재진입/이미 작은 텍스처) → 비용 회피.
                // 이렇게 "더 큰 쪽만 내리는" 게이트라 crunch 가 이미 더 작게 캡했어도 그 값을 덮어쓰지 않는다.
                if (ti.maxTextureSize <= clampMax)
                {
                    continue;
                }

                // 바이트 필터(선택): 소스 파일이 minBytes 미만이면 제외(작은 아이콘 보호).
                if (minBytes > 0)
                {
                    string srcFull = Path.Combine(projectRoot, path);
                    if (!File.Exists(srcFull) || new FileInfo(srcFull).Length < minBytes)
                    {
                        continue;
                    }
                }

                // 원본 .meta 백업(verbatim 복원용).
                if (!BackupAssetMeta(path, projectRoot))
                {
                    continue;
                }

                // maxTextureSize 한 필드만 override. format/compression/crunch/sRGB/sprite 설정은 보존.
                ti.maxTextureSize = clampMax;
                ti.SaveAndReimport();
                n++;
            }

            return n;
        }

        // ─────────────────────────── 백업/복원 ───────────────────────────

        /// <summary>에셋의 .meta 파일을 <path>.meta.aittexclampbak 로 백업.</summary>
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
                Debug.LogWarning($"[AIT-TextureSizeClamp] .meta 백업 실패({assetPath}): {e.Message}");
                return false;
            }
        }

        /// <summary>Assets 트리의 모든 *.aittexclampbak 를 원본으로 되돌리고 백업을 삭제한다. 복원 개수 반환.</summary>
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
                // bak = "<original>.meta.aittexclampbak" → original 은 텍스처의 .meta.
                string original = bak.Substring(0, bak.Length - BackupSuffix.Length);
                try
                {
                    File.Copy(bak, original, true);
                    File.Delete(bak);

                    // reimport 대상 에셋 경로 산출: original 은 .meta 이므로 본체 경로로 환원.
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
                    Debug.LogWarning($"[AIT-TextureSizeClamp] 백업 복원 실패({bak}): {e.Message}");
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

        private static string[] SplitDirs(string v)
            => string.IsNullOrEmpty(v) ? null : v.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        private static bool UnderAny(string path, string[] dirs)
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
