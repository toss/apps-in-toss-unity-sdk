// -----------------------------------------------------------------------
// <copyright file="AITTextureCrunchProcessor.cs" company="Toss">
//     Copyright (c) Toss. All rights reserved.
//     Apps in Toss Unity SDK - Texture Crunch (build-time importer override)
// </copyright>
// -----------------------------------------------------------------------
//
// 빌드 직전, 대상 Texture2D / SpriteAtlas 의 임포터 설정을 일시적으로
//   (1) maxTextureSize 캡  (2) crunch 압축 + 품질 하향
// 으로 바꿔 reimport 하여, Unity 가 더 작은 텍스처를 .data 에 굽도록 한다.
// 빌드 종료(성공/실패 무관) 후 원본 임포트 설정으로 원상 복원한다.
//
// 효과: crunch 는 DXT 위에 적용되는 강력한 코덱(4~8x). 다운로드(on-wire) 와 .data 를 실감.
//   WebGL 플랫폼 오버라이드를 "생성"하면 format 이 RGBA32(비압축)로 리셋돼 오히려 팽창하므로,
//   오버라이드를 만들지 않고 기본 임포터 설정(textureCompression/crunchedCompression/compressionQuality)
//   과 maxTextureSize 만 조정한다(빌드는 WebGL fallback 으로 기본 format=DXT 를 쓰고 거기에 crunch).
//   SpriteAtlas 는 DefaultTexturePlatform(마스터) 설정만 조정 후 WebGL 타깃으로 repack.
//
// 비파괴: 각 에셋의 원본 .meta(텍스처) / .spriteatlasv2(아틀라스) 를 <path>.aittexbak 로 백업,
//   빌드 후 원본을 그대로 복원한다. 빌드가 비정상 종료되어 복원이 누락돼도, 다음 에디터 로드 시
//   안전망(SafetyNetRestore)이 잔존 백업을 자동 복원한다.
//
// 통합: AITConvertCore.BuildWebGL 가 BuildPipeline.BuildPlayer 직전에 ApplyForBuild,
//   try/finally 의 finally 에서 RestoreForBuild 를 호출한다(오디오 스트리밍과 동일 패턴).
//
// ⚠ 비용: crunch reimport 는 무겁다(에셋 수에 비례). 빌드 시 apply + 복원으로 2회 reimport 가
//   발생하므로, 명시적 opt-in 으로 기본 비활성이다. 대용량 텍스처가 많은 프로젝트의 다운로드
//   최적화용이다.

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

namespace AppsInToss.Editor
{
    /// <summary>
    /// 빌드 단계 텍스처 crunch 처리기. <see cref="AITEditorScriptObject.enableTextureCrunch"/>
    /// 설정에 따라 동작한다. 런타임 컴포넌트는 없다(빌드 산출물만 작아질 뿐, 런타임 동작 동일).
    /// </summary>
    [InitializeOnLoad]
    public static class AITTextureCrunchProcessor
    {
        /// <summary>원본 .meta / .spriteatlasv2 를 보관하는 백업 접미사.</summary>
        private const string BackupSuffix = ".aittexbak";

        /// <summary>apply 가 진행 중임을 표시하는 마커(Unity 가 무시하는 '.' 접두 숨김 파일).</summary>
        private const string MarkerRelative = "Assets/.ait-texcrunch-active";

        /// <summary>한 번의 crunch 적용 결과 핸들. finally 에서 정확한 복원에 사용.</summary>
        public sealed class CrunchHandle
        {
            /// <summary>이번 빌드에서 crunch 가 실제로 수행되었는지.</summary>
            public bool Active;

            /// <summary>처리된 텍스처 개수.</summary>
            public int TextureCount;

            /// <summary>처리된 SpriteAtlas 개수.</summary>
            public int AtlasCount;
        }

        static AITTextureCrunchProcessor()
        {
            EditorApplication.delayCall += SafetyNetRestore;
        }

        /// <summary>
        /// 빌드 직전 호출: 설정이 켜져 있으면 대상 텍스처/아틀라스를 crunch 로 reimport 한다.
        /// </summary>
        /// <param name="config">프로젝트 에디터 설정. null 이거나 기능이 꺼져 있으면 no-op.</param>
        /// <returns>복원에 사용할 핸들(항상 non-null).</returns>
        public static CrunchHandle ApplyForBuild(AITEditorScriptObject config)
        {
            var handle = new CrunchHandle();
            if (config == null || !config.enableTextureCrunch)
            {
                return handle;
            }

            try
            {
                int maxSize = config.textureCrunchMaxSize;          // 0 = 캡 안 함
                int quality = Mathf.Clamp(config.textureCrunchQuality, 0, 100);
                string[] dirs = SplitDirs(config.textureCrunchDirs); // null = 전체 Assets
                bool doAtlas = config.textureCrunchAtlas;
                int atlasMax = config.textureCrunchAtlasMaxSize;     // 0 = 캡 안 함

                CreateMarker();

                int tex = ApplyTexture2D(maxSize, quality, dirs);
                int atl = doAtlas ? ApplySpriteAtlases(atlasMax, quality, dirs) : 0;

                AssetDatabase.Refresh();

                handle.Active = (tex + atl) > 0;
                handle.TextureCount = tex;
                handle.AtlasCount = atl;
                if (!handle.Active)
                {
                    // 대상 0건 → 마커 제거(복원할 것 없음).
                    RemoveMarker();
                }

                Debug.Log($"[AIT-TextureCrunch] ✓ 텍스처 {tex}개 + 아틀라스 {atl}개 crunch(q={quality}, maxSize≤{(maxSize > 0 ? maxSize.ToString() : "원본")}) 적용.");
                return handle;
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT-TextureCrunch] 적용 예외 → 복원 후 건너뜀: {e}");
                RestoreAllBackups();
                RemoveMarker();
                AssetDatabase.Refresh();
                return new CrunchHandle();
            }
        }

        /// <summary>빌드 종료 후(성공/실패 무관) 호출: 원본 임포트 설정으로 복원한다.</summary>
        public static void RestoreForBuild(CrunchHandle handle)
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
                Debug.Log($"[AIT-TextureCrunch] 복원 완료: {restored}개 에셋 원본 임포트 설정 원상.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AIT-TextureCrunch] 복원 예외: {e}");
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
                    Debug.LogWarning($"[AIT-TextureCrunch] 안전망: 이전 빌드 잔존 백업 {restored}개를 복원했습니다.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[AIT-TextureCrunch] 안전망 복원 중 예외(무시): {e}");
            }
        }

        // ─────────────────────────── Texture2D ───────────────────────────
        private static int ApplyTexture2D(int maxSize, int quality, string[] dirs)
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

                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti == null)
                {
                    continue;
                }

                bool wouldChange =
                    (maxSize > 0 && ti.maxTextureSize > maxSize)
                    || ti.textureCompression == TextureImporterCompression.Uncompressed
                    || !ti.crunchedCompression
                    || ti.compressionQuality != quality;
                if (!wouldChange)
                {
                    continue; // 이미 목표 상태(예: 재진입) → 백업/리임포트 비용 회피
                }

                // 원본 .meta 백업(verbatim 복원용).
                if (!BackupAssetMeta(path, projectRoot))
                {
                    continue;
                }

                if (maxSize > 0 && ti.maxTextureSize > maxSize)
                {
                    ti.maxTextureSize = maxSize;
                }

                if (ti.textureCompression == TextureImporterCompression.Uncompressed)
                {
                    ti.textureCompression = TextureImporterCompression.Compressed; // crunch 전제
                }

                ti.crunchedCompression = true;
                ti.compressionQuality = quality;
                ti.SaveAndReimport();
                n++;
            }

            return n;
        }

        // ─────────────────────────── SpriteAtlas ───────────────────────────
        private static int ApplySpriteAtlases(int atlasMax, int quality, string[] dirs)
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

                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(path);
                if (atlas == null)
                {
                    continue;
                }

                // 아틀라스 에셋 파일 자체를 백업(플랫폼 설정이 .spriteatlasv2 안에 직렬화됨).
                if (!BackupAssetFile(path, projectRoot))
                {
                    continue;
                }

                // DefaultTexturePlatform(마스터). format 필드는 보존(=null/Automatic 유지).
                var def = atlas.GetPlatformSettings("DefaultTexturePlatform");
                if (atlasMax > 0 && def.maxTextureSize > atlasMax)
                {
                    def.maxTextureSize = atlasMax;
                }

                if (def.textureCompression == TextureImporterCompression.Uncompressed)
                {
                    def.textureCompression = TextureImporterCompression.Compressed;
                }

                def.crunchedCompression = true;
                def.compressionQuality = quality;
                atlas.SetPlatformSettings(def);
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
                    Debug.LogWarning($"[AIT-TextureCrunch]   PackAllAtlases 경고: {e.Message}");
                }
            }

            return n;
        }

        // ─────────────────────────── 백업/복원 ───────────────────────────

        /// <summary>에셋의 .meta 파일을 <path>.meta.aittexbak 로 백업.</summary>
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
                Debug.LogWarning($"[AIT-TextureCrunch] .meta 백업 실패({assetPath}): {e.Message}");
                return false;
            }
        }

        /// <summary>에셋 본체 파일을 <path>.aittexbak 로 백업(SpriteAtlas 용).</summary>
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
                Debug.LogWarning($"[AIT-TextureCrunch] 에셋 백업 실패({assetPath}): {e.Message}");
                return false;
            }
        }

        /// <summary>Assets 트리의 모든 *.aittexbak 를 원본으로 되돌리고 백업을 삭제한다. 복원 개수 반환.</summary>
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
                // bak = "<original>.aittexbak" → original 은 .meta(텍스처) 또는 .spriteatlasv2(아틀라스).
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
                    Debug.LogWarning($"[AIT-TextureCrunch] 백업 복원 실패({bak}): {e.Message}");
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
