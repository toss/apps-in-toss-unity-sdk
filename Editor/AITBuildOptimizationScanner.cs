using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace AppsInToss.Editor
{
    public enum OptimizationIssueType
    {
        TextureCompression,
        AudioCompression,
        IL2CPP
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
            issues.Add(CheckIL2CPP());

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

                if (!platformSettings.overridden || IsUncompressedTextureFormat(platformSettings.format))
                {
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

                var sampleSettings = importer.GetOverrideSampleSettings("WebGL");

                // WebGL 오버라이드가 없거나 PCM인 경우
                if (!importer.ContainsSampleSettingsOverride("WebGL") ||
                    sampleSettings.compressionFormat == AudioCompressionFormat.PCM)
                {
                    issue.assetPaths.Add(path);
                }
            }

            if (issue.assetPaths.Count > 0)
            {
                issue.status = OptimizationStatus.Issue;
                issue.description = $"{issue.assetPaths.Count}개 오디오가 비압축(PCM) 포맷 사용 중";
            }
            else
            {
                issue.status = OptimizationStatus.AlreadyOptimal;
                issue.description = "모든 오디오가 최적화됨";
                issue.isSelected = false;
            }

            return issue;
        }

        private static OptimizationIssue CheckIL2CPP()
        {
            return new OptimizationIssue
            {
                type = OptimizationIssueType.IL2CPP,
                status = OptimizationStatus.AlreadyOptimal,
                label = "IL2CPP 빌드",
                description = "SDK가 자동으로 IL2CPP를 설정합니다",
                isSelected = false
            };
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
                AssetDatabase.StartAssetEditing();

                var format = GetRecommendedTextureFormat();
                int fixed_ = 0;

                for (int i = 0; i < assetPaths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar(
                        "텍스처 최적화 적용 중",
                        $"{assetPaths[i]} ({i + 1}/{assetPaths.Count})",
                        (float)i / assetPaths.Count);

                    var importer = AssetImporter.GetAtPath(assetPaths[i]) as TextureImporter;
                    if (importer == null) continue;

                    var platformSettings = importer.GetPlatformTextureSettings("WebGL");
                    platformSettings.overridden = true;
                    platformSettings.format = format;
                    importer.SetPlatformTextureSettings(platformSettings);
                    importer.SaveAndReimport();
                    fixed_++;
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
                AssetDatabase.StopAssetEditing();
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
                AssetDatabase.StartAssetEditing();

                int fixed_ = 0;

                for (int i = 0; i < assetPaths.Count; i++)
                {
                    EditorUtility.DisplayProgressBar(
                        "오디오 최적화 적용 중",
                        $"{assetPaths[i]} ({i + 1}/{assetPaths.Count})",
                        (float)i / assetPaths.Count);

                    var importer = AssetImporter.GetAtPath(assetPaths[i]) as AudioImporter;
                    if (importer == null) continue;

                    var sampleSettings = importer.GetOverrideSampleSettings("WebGL");
                    sampleSettings.compressionFormat = AudioCompressionFormat.Vorbis;
                    sampleSettings.quality = 0.5f;
                    importer.SetOverrideSampleSettings("WebGL", sampleSettings);
                    importer.SaveAndReimport();
                    fixed_++;
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
                AssetDatabase.StopAssetEditing();
                EditorUtility.ClearProgressBar();
            }

            return result;
        }

        private static bool IsUncompressedTextureFormat(TextureImporterFormat format)
        {
            return format == TextureImporterFormat.RGBA32 ||
                   format == TextureImporterFormat.ARGB32 ||
                   format == TextureImporterFormat.RGB24 ||
                   format == TextureImporterFormat.Alpha8 ||
                   format == TextureImporterFormat.RGBA16;
        }

        private static TextureImporterFormat GetRecommendedTextureFormat()
        {
#if UNITY_2023_3_OR_NEWER
            return TextureImporterFormat.ASTC_6x6;
#else
            return TextureImporterFormat.ETC2_RGBA8;
#endif
        }

        private static string GetRecommendedTextureFormatName()
        {
#if UNITY_2023_3_OR_NEWER
            return "ASTC 6x6";
#else
            return "ETC2";
#endif
        }
    }
}
