using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace AppsInToss.Editor.AssetStreaming
{
    /// <summary>
    /// 에셋 정적 분석 엔진
    /// AssetDatabase를 스캔하여 Addressable 변환 후보 에셋을 분석
    /// </summary>
    public static class AITAssetAnalyzer
    {
        private static AITAssetAnalysisReport cachedReport;
        private static DateTime cacheExpiry;
        private const double CacheTTLMinutes = 5.0;

        /// <summary>
        /// 2MB 초과: HighlyRecommended
        /// </summary>
        private const long HighlyRecommendedThreshold = 2 * 1024 * 1024;

        /// <summary>
        /// 512KB 초과: Recommended
        /// </summary>
        private const long RecommendedThreshold = 512 * 1024;

        /// <summary>
        /// 분석 대상 에셋 타입별 검색 필터
        /// </summary>
        private static readonly (string filter, AssetType type)[] AssetTypeFilters = new[]
        {
            ("t:Texture2D", AssetType.Texture),
            ("t:AudioClip", AssetType.Audio),
            ("t:Mesh", AssetType.Mesh),
            ("t:VideoClip", AssetType.Video),
            ("t:Font", AssetType.Font),
        };

        /// <summary>
        /// 전체 분석 실행
        /// </summary>
        /// <param name="forceRefresh">캐시 무시 여부</param>
        /// <returns>분석 보고서</returns>
        public static AITAssetAnalysisReport Analyze(bool forceRefresh = false)
        {
            if (!forceRefresh && cachedReport != null && DateTime.Now < cacheExpiry)
            {
                return cachedReport;
            }

            var report = new AITAssetAnalysisReport
            {
                analyzedAt = DateTime.Now,
                addressablesInstalled = IsAddressablesInstalled()
            };

            long minSizeBytes = AITAssetStreamingConfig.MinAssetSizeBytes;

            foreach (var (filter, assetType) in AssetTypeFilters)
            {
                string[] guids = AssetDatabase.FindAssets(filter);
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!ShouldIncludeAsset(path))
                        continue;

                    long fileSize = GetFileSize(path);
                    if (fileSize < minSizeBytes)
                        continue;

                    long estimatedContribution = EstimateBuildContribution(path, assetType, fileSize);
                    var asset = new AITAnalyzedAsset
                    {
                        assetPath = path,
                        guid = guid,
                        assetType = assetType,
                        fileSizeBytes = fileSize,
                        estimatedBuildContribution = estimatedContribution,
                        recommendation = ClassifyRecommendation(estimatedContribution),
                        isInResources = IsInResourcesFolder(path),
                        isAlreadyAddressable = report.addressablesInstalled && IsAssetAddressable(guid)
                    };

                    report.assets.Add(asset);
                    report.totalProjectSizeBytes += fileSize;
                    if (!asset.isAlreadyAddressable)
                    {
                        report.totalEstimatedSavingsBytes += estimatedContribution;
                    }
                }
            }

            // 크기 내림차순 정렬
            report.assets.Sort((a, b) => b.estimatedBuildContribution.CompareTo(a.estimatedBuildContribution));

            cachedReport = report;
            cacheExpiry = DateTime.Now.AddMinutes(CacheTTLMinutes);

            return report;
        }

        /// <summary>
        /// 빠른 요약 분석 (pre-build check용, 총합만 계산)
        /// </summary>
        public static AITQuickAnalysisSummary QuickAnalyze()
        {
            var summary = new AITQuickAnalysisSummary
            {
                addressablesInstalled = IsAddressablesInstalled()
            };

            long minSizeBytes = AITAssetStreamingConfig.MinAssetSizeBytes;

            foreach (var (filter, assetType) in AssetTypeFilters)
            {
                string[] guids = AssetDatabase.FindAssets(filter);
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!ShouldIncludeAsset(path))
                        continue;

                    long fileSize = GetFileSize(path);
                    if (fileSize < minSizeBytes)
                        continue;

                    long estimatedContribution = EstimateBuildContribution(path, assetType, fileSize);
                    summary.totalAssetCount++;
                    summary.totalEstimatedSavingsBytes += estimatedContribution;

                    if (estimatedContribution > HighlyRecommendedThreshold)
                        summary.highlyRecommendedCount++;
                }
            }

            return summary;
        }

        /// <summary>
        /// 캐시 무효화
        /// </summary>
        public static void InvalidateCache()
        {
            cachedReport = null;
        }

        /// <summary>
        /// 에셋 포함 여부 필터링
        /// </summary>
        internal static bool ShouldIncludeAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // Assets/ 로 시작하지 않으면 제외 (Packages/ 등)
            if (!path.StartsWith("Assets/", StringComparison.Ordinal))
                return false;

            // Editor/ 하위 제외
            if (path.Contains("/Editor/") || path.StartsWith("Assets/Editor/", StringComparison.Ordinal))
                return false;

            // AppsInToss/ 하위 제외 (SDK 설정 파일)
            if (path.Contains("/AppsInToss/") || path.StartsWith("Assets/AppsInToss/", StringComparison.Ordinal))
                return false;

            return true;
        }

        /// <summary>
        /// Resources/ 폴더 내 에셋인지 확인
        /// </summary>
        internal static bool IsInResourcesFolder(string path)
        {
            return path.Contains("/Resources/") || path.StartsWith("Assets/Resources/", StringComparison.Ordinal);
        }

        /// <summary>
        /// 추천 수준 분류
        /// </summary>
        internal static RecommendationLevel ClassifyRecommendation(long estimatedBuildContribution)
        {
            if (estimatedBuildContribution > HighlyRecommendedThreshold)
                return RecommendationLevel.HighlyRecommended;
            if (estimatedBuildContribution > RecommendedThreshold)
                return RecommendationLevel.Recommended;
            return RecommendationLevel.Optional;
        }

        /// <summary>
        /// 파일 크기 조회
        /// </summary>
        private static long GetFileSize(string assetPath)
        {
            try
            {
                string fullPath = Path.GetFullPath(assetPath);
                if (File.Exists(fullPath))
                    return new FileInfo(fullPath).Length;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIT] Failed to get file size for '{assetPath}': {ex.Message}");
            }
            return 0;
        }

        /// <summary>
        /// 빌드 기여도 추정
        /// TextureImporter/AudioImporter 설정을 참고하여 WebGL 빌드 시 크기 추정
        /// </summary>
        private static long EstimateBuildContribution(string path, AssetType type, long fileSize)
        {
            try
            {
                AssetImporter importer = AssetImporter.GetAtPath(path);

                switch (type)
                {
                    case AssetType.Texture:
                        if (importer is TextureImporter texImporter)
                        {
                            // WebGL 플랫폼 설정 확인
                            var webglSettings = texImporter.GetPlatformTextureSettings("WebGL");
                            if (webglSettings.overridden)
                            {
                                // 대략적 추정: maxTextureSize와 format 기반
                                int maxSize = webglSettings.maxTextureSize;
                                return EstimateCompressedTextureSize(maxSize, webglSettings.format);
                            }

                            // 기본 설정 사용 시 원본 크기의 약 25% (ASTC/ETC2 압축 가정)
                            return fileSize / 4;
                        }
                        return fileSize / 4;

                    case AssetType.Audio:
                        if (importer is AudioImporter audioImporter)
                        {
                            var webglSettings = audioImporter.GetOverrideSampleSettings("WebGL");
                            if (webglSettings.quality > 0)
                            {
                                // Vorbis 압축 시 원본의 약 10-20%
                                return (long)(fileSize * webglSettings.quality * 0.2f);
                            }
                        }
                        // 기본: 원본의 약 15%
                        return (long)(fileSize * 0.15f);

                    case AssetType.Video:
                        // 비디오는 거의 그대로 포함
                        return fileSize;

                    case AssetType.Mesh:
                        // 메시는 약 80% 유지
                        return (long)(fileSize * 0.8f);

                    case AssetType.Font:
                        // 폰트는 거의 그대로 포함
                        return fileSize;

                    default:
                        return fileSize;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIT] Failed to estimate build contribution for '{path}': {ex.Message}");
                return fileSize;
            }
        }

        /// <summary>
        /// 압축 텍스처 크기 추정
        /// </summary>
        private static long EstimateCompressedTextureSize(int maxSize, TextureImporterFormat format)
        {
            long pixels = (long)maxSize * maxSize;
            float bitsPerPixel;

            switch (format)
            {
                case TextureImporterFormat.ASTC_4x4:
                    bitsPerPixel = 8f;
                    break;
                case TextureImporterFormat.ASTC_6x6:
                    bitsPerPixel = 3.56f;
                    break;
                case TextureImporterFormat.ASTC_8x8:
                    bitsPerPixel = 2f;
                    break;
                case TextureImporterFormat.ETC2_RGBA8:
                    bitsPerPixel = 8f;
                    break;
                case TextureImporterFormat.ETC2_RGB4:
                    bitsPerPixel = 4f;
                    break;
                case TextureImporterFormat.DXT5:
                    bitsPerPixel = 8f;
                    break;
                case TextureImporterFormat.DXT1:
                    bitsPerPixel = 4f;
                    break;
                default:
                    bitsPerPixel = 8f;
                    break;
            }

            return (long)(pixels * bitsPerPixel / 8);
        }

        /// <summary>
        /// Addressables 패키지 설치 여부 확인
        /// </summary>
        private static bool IsAddressablesInstalled()
        {
#if AIT_ADDRESSABLES_INSTALLED
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// 에셋이 이미 Addressable로 마킹되어 있는지 확인
        /// </summary>
        private static bool IsAssetAddressable(string guid)
        {
#if AIT_ADDRESSABLES_INSTALLED
            try
            {
                var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
                if (settings == null) return false;
                var entry = settings.FindAssetEntry(guid);
                return entry != null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AIT] Failed to check addressable status for guid '{guid}': {ex.Message}");
                return false;
            }
#else
            return false;
#endif
        }
    }
}
