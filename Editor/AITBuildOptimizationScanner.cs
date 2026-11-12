using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace AppsInToss.Editor
{
    public enum OptimizationIssueType
    {
        TextureCompression,
        AudioCompression
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

            // 가드가 꺼져 있으면(Off) DXT/BC·아틀라스 검사는 건너뛴다. 감지 로직은 AITTextureFormatGuard 와 공유.
            var guardMode = AITTextureFormatGuard.ResolveCurrentMode(UnityUtil.GetEditorConf(), out _);
            bool checkIncompatible = guardMode != AITTextureFormatGuardMode.Off;

            // 경고만 모드에서는 서브타깃을 바꾸지 않는다. Generic이면 Automatic 포맷 텍스처가 Player Settings의
            // 텍스처 압축(DXT 등)으로 구워지므로 권장 문구에 알려 준다. 자동 변환은 빌드 직전에 ASTC로 바꾼다.
            var webglSubtarget = EditorUserBuildSettings.webGLBuildSubtarget;
            if (guardMode == AITTextureFormatGuardMode.WarnOnly && !AITTextureFormatGuard.IsMobileCompatibleSubtarget(webglSubtarget))
            {
                issue.recommendation += $" (WebGL 텍스처 서브타깃이 {webglSubtarget}라 Automatic 포맷 텍스처는 Player Settings의 DXT 등 비호환 압축을 따를 수 있습니다)";
            }

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
                else if (checkIncompatible && AITTextureFormatGuard.InspectIncompatible(path) != null)
                {
                    // DXT/BC 오버라이드 → 모바일 WebView 비호환 (옵트아웃 레이블 에셋 제외)
                    issue.assetPaths.Add(path);
                }
            }

            if (checkIncompatible)
            {
                // SpriteAtlas(v1/v2)의 DXT/BC 오버라이드
                foreach (string guid in AssetDatabase.FindAssets("t:SpriteAtlas", new[] { "Assets" }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AITTextureFormatGuard.InspectIncompatible(path) != null)
                        issue.assetPaths.Add(path);
                }
            }

            if (issue.assetPaths.Count > 0)
            {
                issue.status = OptimizationStatus.Issue;
                issue.description = $"{issue.assetPaths.Count}개 텍스처/아틀라스가 WebGL 압축 미적용 또는 모바일 비호환(DXT/BC)";
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

                // WebGL 오버라이드가 없거나 비압축/경량압축 포맷인 경우
                if (!importer.ContainsSampleSettingsOverride("WebGL") ||
                    sampleSettings.compressionFormat == AudioCompressionFormat.PCM ||
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
                        // 아틀라스·DXT/BC 오버라이드는 가드의 변환 매핑을 그대로 쓴다
                        if (AITTextureFormatGuard.ConvertAsset(assetPaths[i]))
                        {
                            fixed_++;
                            continue;
                        }

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

                // SpriteAtlas(v1) 변경은 저장해야 reimport 로 되돌려지지 않는다
                AssetDatabase.SaveAssets();

                // 일괄 reimport
                for (int i = 0; i < assetPaths.Count; i++)
                {
                    AssetDatabase.WriteImportSettingsIfDirty(assetPaths[i]);
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

                        var sampleSettings = importer.GetOverrideSampleSettings("WebGL");
                        sampleSettings.compressionFormat = AudioCompressionFormat.Vorbis;
                        sampleSettings.quality = 0.5f;
                        importer.SetOverrideSampleSettings("WebGL", sampleSettings);
                        fixed_++;
                    }
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                // SpriteAtlas(v1) 변경은 저장해야 reimport 로 되돌려지지 않는다
                AssetDatabase.SaveAssets();

                // 일괄 reimport
                for (int i = 0; i < assetPaths.Count; i++)
                {
                    AssetDatabase.WriteImportSettingsIfDirty(assetPaths[i]);
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
            return AITTextureFormatGuard.GetRecommendedTextureFormat();
        }

        private static string GetRecommendedTextureFormatName()
        {
            return AITTextureFormatGuard.GetRecommendedTextureFormatName();
        }
    }
}
