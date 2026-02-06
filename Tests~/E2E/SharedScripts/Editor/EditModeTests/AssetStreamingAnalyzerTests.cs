using NUnit.Framework;
using AppsInToss.Editor.AssetStreaming;

[TestFixture]
public class AssetStreamingAnalyzerTests
{
    [SetUp]
    public void Setup()
    {
        AITAssetAnalyzer.InvalidateCache();
    }

    [Test]
    public void Analyze_ReturnsNonNullReport()
    {
        var report = AITAssetAnalyzer.Analyze(forceRefresh: true);
        Assert.IsNotNull(report, "Analyze() should return a non-null report");
        Assert.IsNotNull(report.assets, "Report assets list should not be null");
    }

    [Test]
    public void Analyze_ExcludesEditorAssets()
    {
        var report = AITAssetAnalyzer.Analyze(forceRefresh: true);
        foreach (var asset in report.assets)
        {
            Assert.IsFalse(
                asset.assetPath.Contains("/Editor/"),
                $"Editor asset should be excluded: {asset.assetPath}");
        }
    }

    [Test]
    public void Analyze_ExcludesPackagesAssets()
    {
        var report = AITAssetAnalyzer.Analyze(forceRefresh: true);
        foreach (var asset in report.assets)
        {
            Assert.IsFalse(
                asset.assetPath.StartsWith("Packages/"),
                $"Packages asset should be excluded: {asset.assetPath}");
        }
    }

    [Test]
    public void Analyze_IncludesResourcesAssets()
    {
        // Resources/ 에셋이 있는 경우에만 isInResources 플래그가 올바른지 확인
        var report = AITAssetAnalyzer.Analyze(forceRefresh: true);
        foreach (var asset in report.assets)
        {
            if (asset.assetPath.Contains("/Resources/"))
            {
                Assert.IsTrue(asset.isInResources,
                    $"Resources asset should have isInResources=true: {asset.assetPath}");
            }
        }
    }

    [TestCase("Assets/Textures/Big.png", true)]
    [TestCase("Packages/com.unity.ugui/Texture.png", false)]
    [TestCase("Assets/Editor/EditorTex.png", false)]
    [TestCase("Assets/AppsInToss/Config.asset", false)]
    [TestCase("Assets/Resources/Audio.wav", true)]
    [TestCase("Assets/Scenes/MyScene.unity", true)]
    public void ShouldIncludeAsset_FiltersCorrectly(string path, bool expected)
    {
        bool result = AITAssetAnalyzer.ShouldIncludeAsset(path);
        Assert.AreEqual(expected, result, $"ShouldIncludeAsset(\"{path}\") should be {expected}");
    }

    [TestCase("Assets/Resources/Audio.wav", true)]
    [TestCase("Assets/Art/Resources/Tex.png", true)]
    [TestCase("Assets/Art/Textures/Tex.png", false)]
    public void IsInResourcesFolder_DetectsCorrectly(string path, bool expected)
    {
        bool result = AITAssetAnalyzer.IsInResourcesFolder(path);
        Assert.AreEqual(expected, result, $"IsInResourcesFolder(\"{path}\") should be {expected}");
    }

    [Test]
    public void Analyze_DetectsAssetTypes()
    {
        var report = AITAssetAnalyzer.Analyze(forceRefresh: true);
        // 타입이 올바르게 분류되는지 확인 (에셋이 있는 경우)
        foreach (var asset in report.assets)
        {
            Assert.IsTrue(
                System.Enum.IsDefined(typeof(AssetType), asset.assetType),
                $"Asset type should be valid enum: {asset.assetType} for {asset.assetPath}");
        }
    }

    [Test]
    public void Config_DefaultThresholds_AreReasonable()
    {
        // 기본 빌드 크기 임계값이 합리적인 범위인지 확인
        float threshold = AITAssetStreamingConfig.BuildSizeThresholdMB;
        Assert.GreaterOrEqual(threshold, 10f, "Build size threshold should be at least 10MB");
        Assert.LessOrEqual(threshold, 200f, "Build size threshold should be at most 200MB");

        // 최소 에셋 크기가 합리적인 범위인지 확인
        int minSize = AITAssetStreamingConfig.MinAssetSizeKB;
        Assert.GreaterOrEqual(minSize, 64, "Min asset size should be at least 64KB");
        Assert.LessOrEqual(minSize, 1024, "Min asset size should be at most 1024KB");
    }

    [Test]
    public void QuickAnalyze_ReturnsTotals()
    {
        var summary = AITAssetAnalyzer.QuickAnalyze();
        Assert.IsNotNull(summary, "QuickAnalyze() should return a non-null summary");
        Assert.GreaterOrEqual(summary.totalAssetCount, 0, "Total asset count should be >= 0");
        Assert.GreaterOrEqual(summary.totalEstimatedSavingsBytes, 0, "Total estimated savings should be >= 0");
    }

    [TestCase(3 * 1024 * 1024L, RecommendationLevel.HighlyRecommended)]
    [TestCase(2 * 1024 * 1024L + 1, RecommendationLevel.HighlyRecommended)]
    [TestCase(1 * 1024 * 1024L, RecommendationLevel.Recommended)]
    [TestCase(512 * 1024L + 1, RecommendationLevel.Recommended)]
    [TestCase(256 * 1024L, RecommendationLevel.Optional)]
    [TestCase(100 * 1024L, RecommendationLevel.Optional)]
    public void RecommendationLevel_Classification(long sizeBytes, RecommendationLevel expected)
    {
        var result = AITAssetAnalyzer.ClassifyRecommendation(sizeBytes);
        Assert.AreEqual(expected, result,
            $"ClassifyRecommendation({sizeBytes}) should be {expected} but was {result}");
    }
}
