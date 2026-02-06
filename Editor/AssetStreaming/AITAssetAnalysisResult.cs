using System;
using System.Collections.Generic;

namespace AppsInToss.Editor.AssetStreaming
{
    /// <summary>
    /// 에셋 타입 분류
    /// </summary>
    public enum AssetType
    {
        Texture,
        Audio,
        Mesh,
        Video,
        Font,
        Data,
        Other
    }

    /// <summary>
    /// 추천 수준
    /// </summary>
    public enum RecommendationLevel
    {
        HighlyRecommended,
        Recommended,
        Optional
    }

    /// <summary>
    /// 개별 에셋 분석 결과
    /// </summary>
    [Serializable]
    public class AITAnalyzedAsset
    {
        public string assetPath;
        public string guid;
        public AssetType assetType;
        public long fileSizeBytes;
        public long estimatedBuildContribution;
        public RecommendationLevel recommendation;
        public bool isInResources;
        public bool isAlreadyAddressable;
    }

    /// <summary>
    /// 전체 분석 보고서
    /// </summary>
    [Serializable]
    public class AITAssetAnalysisReport
    {
        public List<AITAnalyzedAsset> assets = new List<AITAnalyzedAsset>();
        public long totalProjectSizeBytes;
        public long totalEstimatedSavingsBytes;
        public bool addressablesInstalled;
        public DateTime analyzedAt;

        public int HighlyRecommendedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < assets.Count; i++)
                    if (assets[i].recommendation == RecommendationLevel.HighlyRecommended)
                        count++;
                return count;
            }
        }

        public int RecommendedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < assets.Count; i++)
                    if (assets[i].recommendation == RecommendationLevel.Recommended)
                        count++;
                return count;
            }
        }

        public int OptionalCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < assets.Count; i++)
                    if (assets[i].recommendation == RecommendationLevel.Optional)
                        count++;
                return count;
            }
        }
    }

    /// <summary>
    /// 빠른 분석 요약 (pre-build check용)
    /// </summary>
    [Serializable]
    public class AITQuickAnalysisSummary
    {
        public int totalAssetCount;
        public long totalEstimatedSavingsBytes;
        public int highlyRecommendedCount;
        public bool addressablesInstalled;
    }
}
