using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace AppsInToss.Editor
{
    public enum OptimizationIssueType
    {
        TextureCompression,
        AudioCompression,
        ReadWriteEnabled
    }

    public enum OptimizationStatus
    {
        Issue,
        AlreadyOptimal
    }

    public class OptimizationIssue
    {
        public OptimizationIssueType type;
        public OptimizationStatus status;
        public string label;
        public string description;
        public string recommendation;
        public bool isSelected = true;
        public List<string> assetPaths = new List<string>();
    }

    public class OptimizationFixResult
    {
        public OptimizationIssueType type;
        public string label;
        public bool success;
        public int fixedCount;
        public string message;
    }

    /// <summary>
    /// 빌드 전 에셋 최적화 상태를 스캔하고 자동 수정하는 유틸리티
    /// </summary>
    public static class AITBuildOptimizationScanner
    {
        /// <summary>
        /// 프로젝트 에셋을 스캔하여 최적화 이슈 목록을 반환
        /// </summary>
        public static List<OptimizationIssue> Scan()
        {
            var issues = new List<OptimizationIssue>();

            issues.Add(ScanTextures());
            issues.Add(ScanAudio());
            issues.Add(ScanReadable());

            return issues;
        }

        /// <summary>
        /// 선택된 이슈들에 대해 자동 수정을 적용
        /// </summary>
        public static List<OptimizationFixResult> ApplyFixes(List<OptimizationIssue> issues)
        {
            var results = new List<OptimizationFixResult>();

            foreach (var issue in issues)
            {
                if (!issue.isSelected || issue.status == OptimizationStatus.AlreadyOptimal)
                    continue;

                switch (issue.type)
                {
                    case OptimizationIssueType.TextureCompression:
                        results.Add(FixTextures(issue.assetPaths));
                        break;
                    case OptimizationIssueType.AudioCompression:
                        results.Add(FixAudio(issue.assetPaths));
                        break;
                    case OptimizationIssueType.ReadWriteEnabled:
                        results.Add(FixReadable(issue.assetPaths));
                        break;
                }
            }

            return results;
        }

        private static OptimizationIssue ScanTextures()
        {
            var issue = new OptimizationIssue
            {
                type = OptimizationIssueType.TextureCompression,
                label = "텍스처 압축",
                recommendation = GetRecommendedTextureFormatName() + " 압축 적용 권장"
            };

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                var platformSettings = importer.GetPlatformTextureSettings("WebGL");

                if (!platformSettings.overridden)
                {
                    // WebGL 플랫폼 오버라이드 없음 → 최적화 대상
                    issue.assetPaths.Add(path);
                }
                else if (platformSettings.format != TextureImporterFormat.Automatic &&
                         IsUncompressedTextureFormat(platformSettings.format))
                {
                    // 오버라이드 있지만 비압축 포맷 → 최적화 대상
                    // Automatic은 Unity가 플랫폼별 최적 포맷을 자동 선택하므로 제외
                    issue.assetPaths.Add(path);
                }
            }

            if (issue.assetPaths.Count > 0)
            {
                issue.status = OptimizationStatus.Issue;
                issue.description = $"{issue.assetPaths.Count}개 텍스처가 WebGL 압축 미적용";
            }
            else
            {
                issue.status = OptimizationStatus.AlreadyOptimal;
                issue.description = "모든 텍스처가 최적화됨";
                issue.isSelected = false;
            }

            return issue;
        }

        private static OptimizationIssue ScanAudio()
        {
            var issue = new OptimizationIssue
            {
                type = OptimizationIssueType.AudioCompression,
                label = "오디오 압축",
                recommendation = "Vorbis 압축 적용 권장"
            };

            string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                if (importer == null) continue;

                // WebGL 은 per-platform 오디오 오버라이드 미지원(ContainsSampleSettingsOverride("WebGL")
                // 은 항상 false) → WebGL 빌드가 ship 하는 유효 설정은 base(defaultSampleSettings)다.
                // 과거 "오버라이드 부재 = 이슈" 판정은 이 때문에 전 클립을 오탐했다.
                var sampleSettings = importer.ContainsSampleSettingsOverride("WebGL")
                    ? importer.GetOverrideSampleSettings("WebGL")
                    : importer.defaultSampleSettings;

                // 유효 포맷이 비압축/경량압축인 경우만 이슈
                if (sampleSettings.compressionFormat == AudioCompressionFormat.PCM ||
                    sampleSettings.compressionFormat == AudioCompressionFormat.ADPCM)
                {
                    issue.assetPaths.Add(path);
                }
            }

            if (issue.assetPaths.Count > 0)
            {
                issue.status = OptimizationStatus.Issue;
                issue.description = $"{issue.assetPaths.Count}개 오디오가 비압축/경량압축(PCM/ADPCM) 포맷 사용 중";
            }
            else
            {
                issue.status = OptimizationStatus.AlreadyOptimal;
                issue.description = "모든 오디오가 최적화됨";
                issue.isSelected = false;
            }

            return issue;
        }

        private static OptimizationFixResult FixTextures(List<string> assetPaths)
        {
            var result = new OptimizationFixResult
            {
                type = OptimizationIssueType.TextureCompression,
                label = "텍스처 압축"
            };

            try
            {
                var format = GetRecommendedTextureFormat();
                int fixed_ = 0;

                // 설정 변경 단계 (reimport 억제)
                AssetDatabase.StartAssetEditing();
                try
                {
                    for (int i = 0; i < assetPaths.Count; i++)
                    {
                        var importer = AssetImporter.GetAtPath(assetPaths[i]) as TextureImporter;
                        if (importer == null) continue;

                        var platformSettings = importer.GetPlatformTextureSettings("WebGL");
                        platformSettings.overridden = true;
                        platformSettings.format = format;
                        importer.SetPlatformTextureSettings(platformSettings);
                        fixed_++;
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                // 일괄 reimport
                for (int i = 0; i < assetPaths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar(
                        "텍스처 reimport 중",
                        $"{assetPaths[i]} ({i + 1}/{assetPaths.Count})",
                        (float)(i + 1) / assetPaths.Count);
                    AssetDatabase.ImportAsset(assetPaths[i]);
                }

                result.success = true;
                result.fixedCount = fixed_;
                result.message = $"{fixed_}개 텍스처에 {GetRecommendedTextureFormatName()} 압축 적용 완료";
            }
            catch (System.Exception e)
            {
                result.success = false;
                result.message = $"텍스처 수정 중 오류: {e.Message}";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return result;
        }

        private static OptimizationFixResult FixAudio(List<string> assetPaths)
        {
            var result = new OptimizationFixResult
            {
                type = OptimizationIssueType.AudioCompression,
                label = "오디오 압축"
            };

            try
            {
                int fixed_ = 0;

                // 설정 변경 단계 (reimport 억제)
                AssetDatabase.StartAssetEditing();
                try
                {
                    for (int i = 0; i < assetPaths.Count; i++)
                    {
                        var importer = AssetImporter.GetAtPath(assetPaths[i]) as AudioImporter;
                        if (importer == null) continue;

                        // WebGL 은 per-platform 오디오 오버라이드 미지원 → base 를 변경해야 실제로 적용됨
                        // (기존 SetOverrideSampleSettings("WebGL") 경로는 항상 no-op 였다).
                        var sampleSettings = importer.defaultSampleSettings;
                        sampleSettings.compressionFormat = AudioCompressionFormat.Vorbis;
                        sampleSettings.quality = 0.5f;
                        importer.defaultSampleSettings = sampleSettings;
                        fixed_++;
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                // 일괄 reimport
                for (int i = 0; i < assetPaths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar(
                        "오디오 reimport 중",
                        $"{assetPaths[i]} ({i + 1}/{assetPaths.Count})",
                        (float)(i + 1) / assetPaths.Count);
                    AssetDatabase.ImportAsset(assetPaths[i]);
                }

                result.success = true;
                result.fixedCount = fixed_;
                result.message = $"{fixed_}개 오디오에 Vorbis 압축 적용 완료";
            }
            catch (System.Exception e)
            {
                result.success = false;
                result.message = $"오디오 수정 중 오류: {e.Message}";
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return result;
        }

        // Read/Write 이슈 판정 임계치: 텍스처 CPU 사본 합계(MB). 모델은 1개라도 있으면 이슈.
        private const double ReadableTextureMbThreshold = 8.0;

        /// <summary>
        /// isReadable 텍스처가 wasm heap 에 남기는 CPU 사본 크기 추정(바이트).
        /// 비압축 4 B/px, 밉맵이 있으면 ×4/3.
        /// </summary>
        internal static long EstimateTextureCpuBytes(int w, int h, bool mips)
        {
            return (long)(w * (long)h * 4 * (mips ? 4.0 / 3.0 : 1.0));
        }

        /// <summary>
        /// 최장변이 maxSize 를 넘으면 비율을 유지한 채 maxSize 로 줄인다(각 변 최소 1).
        /// </summary>
        internal static void ClampToMaxTextureSize(ref int w, ref int h, int maxSize)
        {
            if (maxSize <= 0) return;
            int longest = Mathf.Max(w, h);
            if (longest <= maxSize) return;
            double scale = (double)maxSize / longest;
            w = Mathf.Max(1, (int)System.Math.Round(w * scale));
            h = Mathf.Max(1, (int)System.Math.Round(h * scale));
        }

        // GetSourceTextureWidthAndHeight 는 버전에 따라 공개 여부가 달라(2021.3 호환) 리플렉션으로 호출한다.
        private static readonly System.Reflection.MethodInfo s_getSourceSize =
            typeof(TextureImporter).GetMethod(
                "GetSourceTextureWidthAndHeight",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic,
                null,
                new[] { typeof(int).MakeByRefType(), typeof(int).MakeByRefType() },
                null);

        private static bool TryGetSourceSize(TextureImporter importer, out int w, out int h)
        {
            w = 0;
            h = 0;
            if (s_getSourceSize == null) return false;
            try
            {
                var args = new object[] { 0, 0 };
                s_getSourceSize.Invoke(importer, args);
                w = (int)args[0];
                h = (int)args[1];
                return w > 0 && h > 0;
            }
            catch (System.Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Read/Write 활성 텍스처·모델 스캔. WebGL 에서 isReadable 에셋은 GPU 사본 외에
        /// wasm heap 에 CPU 사본을 따로 유지한다. 런타임 reader 가 있을 수 있어 기본 선택하지 않는다.
        /// </summary>
        internal static OptimizationIssue ScanReadable()
        {
            var issue = new OptimizationIssue
            {
                type = OptimizationIssueType.ReadWriteEnabled,
                label = "Read/Write 활성 에셋",
                recommendation = "Read/Write 해제 권장 (런타임에서 접근하는 에셋은 제외)",
                isSelected = false
            };

            int textureCount = 0;
            int modelCount = 0;
            long textureBytes = 0;

            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null || !importer.isReadable) continue;

                issue.assetPaths.Add(path);
                textureCount++;

                if (TryGetSourceSize(importer, out int w, out int h))
                {
                    var platformSettings = importer.GetPlatformTextureSettings("WebGL");
                    int maxSize = platformSettings.overridden ? platformSettings.maxTextureSize : importer.maxTextureSize;
                    ClampToMaxTextureSize(ref w, ref h, maxSize);
                    textureBytes += EstimateTextureCpuBytes(w, h, importer.mipmapEnabled);
                }
            }

            // 모델은 메시를 로드하지 않고 경로만 센다(크기 추정 없음).
            foreach (string guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null || !importer.isReadable) continue;

                issue.assetPaths.Add(path);
                modelCount++;
            }

            double textureMb = textureBytes / 1e6; // 십진 MB (2048² 밉맵 텍스처 ≈ 22 MB)
            if (textureMb >= ReadableTextureMbThreshold || modelCount >= 1)
            {
                issue.status = OptimizationStatus.Issue;
                issue.description = $"{textureCount}개 텍스처(CPU 사본 약 {textureMb:F0} MB) · {modelCount}개 모델이 Read/Write 활성 — " +
                    "wasm heap 에 CPU 사본이 남습니다. 런타임에서 GetPixels/ReadPixels 대상/Mesh.vertices 등을 쓰지 않는 에셋만 해제하세요.";
            }
            else
            {
                issue.status = OptimizationStatus.AlreadyOptimal;
                issue.description = "Read/Write 활성 에셋의 CPU 사본이 작음";
            }

            return issue;
        }

        private static OptimizationFixResult FixReadable(List<string> assetPaths)
        {
            var result = new OptimizationFixResult
            {
                type = OptimizationIssueType.ReadWriteEnabled,
                label = "Read/Write 활성 에셋"
            };

            try
            {
                int fixed_ = 0;

                AssetDatabase.StartAssetEditing();
                try
                {
                    for (int i = 0; i < assetPaths.Count; i++)
                    {
                        var importer = AssetImporter.GetAtPath(assetPaths[i]);
                        var textureImporter = importer as TextureImporter;
                        var modelImporter = importer as ModelImporter;

                        if (textureImporter != null && textureImporter.isReadable)
                        {
                            textureImporter.isReadable = false;
                            textureImporter.SaveAndReimport();
                            fixed_++;
                        }
                        else if (modelImporter != null && modelImporter.isReadable)
                        {
                            modelImporter.isReadable = false;
                            modelImporter.SaveAndReimport();
                            fixed_++;
                        }
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                result.success = true;
                result.fixedCount = fixed_;
                result.message = $"{fixed_}개 에셋의 Read/Write 해제 완료";
            }
            catch (System.Exception e)
            {
                result.success = false;
                result.message = $"Read/Write 해제 중 오류: {e.Message}";
            }

            return result;
        }

        private static bool IsUncompressedTextureFormat(TextureImporterFormat format)
        {
            return format == TextureImporterFormat.RGBA32 ||
                   format == TextureImporterFormat.ARGB32 ||
                   format == TextureImporterFormat.RGB24 ||
                   format == TextureImporterFormat.Alpha8 ||
                   format == TextureImporterFormat.RGBA16 ||
                   format == TextureImporterFormat.R8 ||
                   format == TextureImporterFormat.R16 ||
                   format == TextureImporterFormat.RG16 ||
                   format == TextureImporterFormat.RGB48 ||
                   format == TextureImporterFormat.RGBA64;
        }

        private static TextureImporterFormat GetRecommendedTextureFormat()
        {
#if UNITY_6000_0_OR_NEWER
            return TextureImporterFormat.ASTC_6x6;
#else
            return TextureImporterFormat.ETC2_RGBA8;
#endif
        }

        private static string GetRecommendedTextureFormatName()
        {
#if UNITY_6000_0_OR_NEWER
            return "ASTC 6x6";
#else
            return "ETC2";
#endif
        }
    }
}
