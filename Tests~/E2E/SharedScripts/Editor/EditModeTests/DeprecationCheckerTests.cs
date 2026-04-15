// -----------------------------------------------------------------------
// DeprecationCheckerTests.cs - EditMode SDK Deprecation 검증 테스트
// Level 0: 버전 비교 로직, git tag 파싱 로직, 동적 최소 버전 폴백을 검증
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.Reflection;
using System.Text.RegularExpressions;
using AppsInToss.Editor;

[TestFixture]
public class DeprecationCheckerTests
{
    private Type checkerType;

    [SetUp]
    public void Setup()
    {
        checkerType = typeof(AITDeprecationChecker);
        Assert.IsNotNull(checkerType, "AITDeprecationChecker type should exist");
        AITDeprecationChecker.ResetCache();
    }

    [TearDown]
    public void TearDown()
    {
        AITDeprecationChecker.ResetCache();
    }

    // =====================================================
    // IsDeprecated: 공개 API 존재 확인
    // =====================================================

    [Test]
    public void IsDeprecated_MethodExists()
    {
        var method = checkerType.GetMethod("IsDeprecated",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "IsDeprecated() should be a public static method");
        Assert.AreEqual(typeof(bool), method.ReturnType);
    }

    [Test]
    public void BlockIfDeprecated_MethodExists()
    {
        var method = checkerType.GetMethod("BlockIfDeprecated",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "BlockIfDeprecated() should be a public static method");
        Assert.AreEqual(typeof(bool), method.ReturnType);
    }

    [Test]
    public void UpgradeToLatest_MethodExists()
    {
        var method = checkerType.GetMethod("UpgradeToLatest",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "UpgradeToLatest() should be a public static method");
    }

    [Test]
    public void DrawDeprecationBanner_MethodExists()
    {
        var method = checkerType.GetMethod("DrawDeprecationBanner",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(method, "DrawDeprecationBanner() should be a public static method");
    }

    // =====================================================
    // ParseLatestTag: git ls-remote 출력 파싱 검증
    // =====================================================

    [Test]
    public void ParseLatestTag_SingleTag_ReturnsCorrectTag()
    {
        string input = "abc123def456\trefs/tags/release/v2.0.0\n";
        string result = AITDeprecationChecker.ParseLatestTag(input, new Version(2, 0, 0));
        Assert.AreEqual("release/v2.0.0", result);
    }

    [Test]
    public void ParseLatestTag_MultipleTags_ReturnsHighestVersion()
    {
        string input =
            "aaa\trefs/tags/release/v2.0.0\n" +
            "bbb\trefs/tags/release/v2.0.3\n" +
            "ccc\trefs/tags/release/v2.0.1\n" +
            "ddd\trefs/tags/release/v2.0.5\n" +
            "eee\trefs/tags/release/v2.0.2\n";
        string result = AITDeprecationChecker.ParseLatestTag(input, new Version(2, 0, 0));
        Assert.AreEqual("release/v2.0.5", result);
    }

    [Test]
    public void ParseLatestTag_WithAnnotatedTagDerefs_IgnoresThem()
    {
        string input =
            "aaa\trefs/tags/release/v2.0.5\n" +
            "bbb\trefs/tags/release/v2.0.5^{}\n" +
            "ccc\trefs/tags/release/v2.1.0\n" +
            "ddd\trefs/tags/release/v2.1.0^{}\n";
        string result = AITDeprecationChecker.ParseLatestTag(input, new Version(2, 0, 0));
        Assert.AreEqual("release/v2.1.0", result);
    }

    [Test]
    public void ParseLatestTag_EmptyOutput_ReturnsNull()
    {
        string result = AITDeprecationChecker.ParseLatestTag("", new Version(2, 0, 0));
        Assert.IsNull(result);
    }

    [Test]
    public void ParseLatestTag_InvalidLines_ReturnsNull()
    {
        string input = "not-a-valid-line\nanother-invalid\n";
        string result = AITDeprecationChecker.ParseLatestTag(input, new Version(2, 0, 0));
        Assert.IsNull(result);
    }

    [Test]
    public void ParseLatestTag_MixedValidAndInvalid_ReturnsHighestValid()
    {
        string input =
            "aaa\trefs/tags/release/v2.0.0\n" +
            "invalid-line\n" +
            "bbb\trefs/tags/not-a-release/v9.9.9\n" +
            "ccc\trefs/tags/release/v2.1.0\n";
        string result = AITDeprecationChecker.ParseLatestTag(input, new Version(2, 0, 0));
        Assert.AreEqual("release/v2.1.0", result);
    }

    [Test]
    public void ParseLatestTag_HigherMinorAndPatch_ComparesCorrectly()
    {
        string input =
            "aaa\trefs/tags/release/v2.0.9\n" +
            "bbb\trefs/tags/release/v2.1.0\n" +
            "ccc\trefs/tags/release/v2.0.10\n";
        string result = AITDeprecationChecker.ParseLatestTag(input, new Version(2, 0, 0));
        Assert.AreEqual("release/v2.1.0", result);
    }

    // =====================================================
    // ParseLatestTag: minVersion 필터링 검증
    // =====================================================

    [Test]
    public void ParseLatestTag_FiltersOutTagsBelowMinVersion()
    {
        string input =
            "aaa\trefs/tags/release/v2.0.0\n" +
            "bbb\trefs/tags/release/v2.3.0\n" +
            "ccc\trefs/tags/release/v2.4.0\n" +
            "ddd\trefs/tags/release/v2.4.1\n" +
            "eee\trefs/tags/release/v2.5.0\n";
        string result = AITDeprecationChecker.ParseLatestTag(input, new Version(2, 4, 1));
        Assert.AreEqual("release/v2.5.0", result);
    }

    [Test]
    public void ParseLatestTag_AllTagsBelowMinVersion_ReturnsNull()
    {
        string input =
            "aaa\trefs/tags/release/v2.0.0\n" +
            "bbb\trefs/tags/release/v2.4.1\n" +
            "ccc\trefs/tags/release/v2.5.0\n";
        string result = AITDeprecationChecker.ParseLatestTag(input, new Version(3, 0, 0));
        Assert.IsNull(result);
    }

    [Test]
    public void ParseLatestTag_IncludesV1Tags_WhenMinVersionIsLow()
    {
        string input =
            "aaa\trefs/tags/release/v1.5.0\n" +
            "bbb\trefs/tags/release/v2.0.0\n" +
            "ccc\trefs/tags/release/v2.4.1\n";
        string result = AITDeprecationChecker.ParseLatestTag(input, new Version(1, 0, 0));
        Assert.AreEqual("release/v2.4.1", result);
    }

    // =====================================================
    // ParseMinVersionFromJson: JSON 파싱 검증
    // =====================================================

    [Test]
    public void ParseMinVersionFromJson_ValidJson_ReturnsVersion()
    {
        string json = "{\"schemaVersion\":1,\"minVersion\":\"2.4.1\"}";
        var result = AITDeprecationChecker.ParseMinVersionFromJson(json);
        Assert.AreEqual(new Version(2, 4, 1), result);
    }

    [Test]
    public void ParseMinVersionFromJson_EmptyJson_ReturnsNull()
    {
        var result = AITDeprecationChecker.ParseMinVersionFromJson("");
        Assert.IsNull(result);
    }

    [Test]
    public void ParseMinVersionFromJson_NullJson_ReturnsNull()
    {
        var result = AITDeprecationChecker.ParseMinVersionFromJson(null);
        Assert.IsNull(result);
    }

    [Test]
    public void ParseMinVersionFromJson_MissingMinVersion_ReturnsNull()
    {
        string json = "{\"schemaVersion\":1,\"otherField\":\"value\"}";
        var result = AITDeprecationChecker.ParseMinVersionFromJson(json);
        Assert.IsNull(result);
    }

    [Test]
    public void ParseMinVersionFromJson_MalformedJson_ReturnsNull()
    {
        var result = AITDeprecationChecker.ParseMinVersionFromJson("not json at all");
        Assert.IsNull(result);
    }

    [Test]
    public void ParseMinVersionFromJson_InvalidVersionString_ReturnsNull()
    {
        string json = "{\"schemaVersion\":1,\"minVersion\":\"not-a-version\"}";
        var result = AITDeprecationChecker.ParseMinVersionFromJson(json);
        Assert.IsNull(result);
    }

    [Test]
    public void ParseMinVersionFromJson_TwoPartVersion_NormalizesToThreePart()
    {
        // "2.4"는 "2.4.0"으로 정규화되어야 합니다 (System.Version에서 2.4 < 2.4.0이므로)
        string json = "{\"schemaVersion\":1,\"minVersion\":\"2.4\"}";
        var result = AITDeprecationChecker.ParseMinVersionFromJson(json);
        Assert.AreEqual(new Version(2, 4, 0), result);
    }

    [Test]
    public void ParseMinVersionFromJson_FourPartVersion_NormalizesToThreePart()
    {
        // 4-part 버전(예: "2.4.1.0")은 3-part(2.4.1)로 정규화됩니다.
        // System.Version에서 2.4.1 < 2.4.1.0이므로 일관된 비교를 위해 필요합니다.
        string json = "{\"schemaVersion\":1,\"minVersion\":\"2.4.1.0\"}";
        var result = AITDeprecationChecker.ParseMinVersionFromJson(json);
        Assert.AreEqual(new Version(2, 4, 1), result);
    }

    [Test]
    public void ParseMinVersionFromJson_UnreasonablyHighVersion_ReturnsNull()
    {
        // MaxReasonableMinVersion(10.0.0) 초과 시 거부
        string json = "{\"schemaVersion\":1,\"minVersion\":\"11.0.0\"}";
        var result = AITDeprecationChecker.ParseMinVersionFromJson(json);
        Assert.IsNull(result, "Unreasonably high minVersion should be rejected");
    }

    [Test]
    public void ParseMinVersionFromJson_ExactMaxReasonableVersion_ReturnsVersion()
    {
        // MaxReasonableMinVersion(10.0.0)과 동일한 값은 허용 (> 비교이므로)
        string json = "{\"schemaVersion\":1,\"minVersion\":\"10.0.0\"}";
        var result = AITDeprecationChecker.ParseMinVersionFromJson(json);
        Assert.AreEqual(new Version(10, 0, 0), result,
            "minVersion equal to MaxReasonableMinVersion should be allowed");
    }

    [Test]
    public void ParseMinVersionFromJson_ReasonableHighVersion_ReturnsVersion()
    {
        // MaxReasonableMinVersion(10.0.0) 이하는 허용
        string json = "{\"schemaVersion\":1,\"minVersion\":\"9.0.0\"}";
        var result = AITDeprecationChecker.ParseMinVersionFromJson(json);
        Assert.AreEqual(new Version(9, 0, 0), result);
    }

    [Test]
    public void ParseMinVersionFromJson_WithSchemaVersion1_ReturnsVersion()
    {
        string json = "{\"schemaVersion\":1,\"minVersion\":\"2.4.1\"}";
        var result = AITDeprecationChecker.ParseMinVersionFromJson(json);
        Assert.AreEqual(new Version(2, 4, 1), result);
    }

    [Test]
    public void ParseMinVersionFromJson_UnsupportedSchemaVersion_ReturnsNull()
    {
        string json = "{\"schemaVersion\":2,\"minVersion\":\"2.4.1\"}";
        var result = AITDeprecationChecker.ParseMinVersionFromJson(json);
        Assert.IsNull(result, "Unsupported schemaVersion should be rejected");
    }

    [Test]
    public void ParseMinVersionFromJson_MissingSchemaVersion_ReturnsNull()
    {
        // schemaVersion이 없으면 기본값 0이므로 검증 실패
        string json = "{\"minVersion\":\"2.4.1\"}";
        var result = AITDeprecationChecker.ParseMinVersionFromJson(json);
        Assert.IsNull(result, "Missing schemaVersion should be rejected");
    }

    [Test]
    public void ParseMinVersionFromJson_ExtraUnknownFields_IgnoredAndReturnsVersion()
    {
        // JsonUtility.FromJson은 알 수 없는 필드를 무시합니다
        string json = "{\"schemaVersion\":1,\"minVersion\":\"2.4.1\",\"newField\":\"value\",\"anotherField\":42}";
        var result = AITDeprecationChecker.ParseMinVersionFromJson(json);
        Assert.AreEqual(new Version(2, 4, 1), result);
    }

    // =====================================================
    // FallbackDeprecationThreshold: 상수 검증
    // =====================================================

    [Test]
    public void FallbackDeprecationThreshold_Is_v2_4_1()
    {
        var field = checkerType.GetField("FallbackDeprecationThreshold",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "FallbackDeprecationThreshold field should exist");

        var threshold = (Version)field.GetValue(null);
        Assert.AreEqual(new Version(2, 4, 1), threshold,
            "Fallback deprecation threshold should be 2.4.1");
    }

    [Test]
    public void FallbackUpgradeTag_IsValid()
    {
        var field = checkerType.GetField("FALLBACK_UPGRADE_TAG",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "FALLBACK_UPGRADE_TAG field should exist");

        string tag = (string)field.GetValue(null);
        Assert.IsTrue(tag.StartsWith("release/v"),
            $"Fallback tag should start with 'release/v', got: {tag}");
    }

    [Test]
    public void FallbackUpgradeTag_IsAtLeastFallbackThreshold()
    {
        // 불변 조건: FALLBACK_UPGRADE_TAG의 버전은 FallbackDeprecationThreshold 이상이어야 합니다.
        // 그렇지 않으면 폴백 태그로 업그레이드해도 여전히 deprecated 상태가 됩니다.
        var tagField = checkerType.GetField("FALLBACK_UPGRADE_TAG",
            BindingFlags.NonPublic | BindingFlags.Static);
        var thresholdField = checkerType.GetField("FallbackDeprecationThreshold",
            BindingFlags.NonPublic | BindingFlags.Static);

        string tag = (string)tagField.GetValue(null);
        var threshold = (Version)thresholdField.GetValue(null);

        var match = Regex.Match(tag, @"release/v(\d+\.\d+\.\d+)$");
        Assert.IsTrue(match.Success, $"FALLBACK_UPGRADE_TAG should match version pattern: {tag}");

        var tagVersion = new Version(match.Groups[1].Value);
        Assert.IsTrue(tagVersion >= threshold,
            $"FALLBACK_UPGRADE_TAG version ({tagVersion}) must be >= FallbackDeprecationThreshold ({threshold})");
    }

    // =====================================================
    // 동적 최소 버전: GetMinVersionCached 검증
    // =====================================================

    [Test]
    public void GetMinVersionCached_WithTestOverride_ReturnsOverriddenVersion()
    {
        var testVersion = new Version(3, 0, 0);
        AITDeprecationChecker.SetMinVersionForTesting(testVersion);
        var result = AITDeprecationChecker.GetMinVersionCached();
        Assert.AreEqual(testVersion, result);
    }

    [Test]
    public void GetMinVersionCached_AfterReset_ThenSetNew_ReturnsNewValue()
    {
        // 캐시 리셋 후 새로운 값을 설정하면 새 값이 반환되는지 확인
        // (네트워크 호출 없이 캐시 리셋 동작만 검증)
        AITDeprecationChecker.SetMinVersionForTesting(new Version(9, 9, 9));
        Assert.AreEqual(new Version(9, 9, 9), AITDeprecationChecker.GetMinVersionCached());

        AITDeprecationChecker.ResetCache();
        AITDeprecationChecker.SetMinVersionForTesting(new Version(3, 0, 0));
        Assert.AreEqual(new Version(3, 0, 0), AITDeprecationChecker.GetMinVersionCached(),
            "After reset + set, should return newly set version");
    }

    [Test]
    public void SetMinVersionForTesting_AffectsIsDeprecated()
    {
        string currentVersion = AppsInToss.AITVersion.Version;
        if (currentVersion == "unknown" || string.IsNullOrEmpty(currentVersion))
        {
            Assert.Inconclusive("SDK version is unknown — cannot verify IsDeprecated behavior");
            return;
        }

        // Set a very high min version so current SDK is deprecated
        AITDeprecationChecker.SetMinVersionForTesting(new Version(99, 0, 0));
        Assert.IsTrue(AITDeprecationChecker.IsDeprecated(),
            "SDK should be deprecated when minVersion is 99.0.0");
    }

    [Test]
    public void SetMinVersionForTesting_LowVersion_NotDeprecated()
    {
        string currentVersion = AppsInToss.AITVersion.Version;
        if (currentVersion == "unknown" || string.IsNullOrEmpty(currentVersion))
        {
            Assert.Inconclusive("SDK version is unknown — cannot verify IsDeprecated behavior");
            return;
        }

        // Set a very low min version so current SDK is not deprecated
        AITDeprecationChecker.SetMinVersionForTesting(new Version(0, 0, 1));
        Assert.IsFalse(AITDeprecationChecker.IsDeprecated(),
            "SDK should not be deprecated when minVersion is 0.0.1");
    }

    // =====================================================
    // IsDeprecated: 현재 SDK 버전 기반 동작 확인
    // =====================================================

    [Test]
    public void IsDeprecated_CurrentSDK_WithKnownMinVersion_ReturnsExpectedValue()
    {
        // 고정된 minVersion으로 설정하여 네트워크 의존성 없이 테스트
        string currentVersion = AppsInToss.AITVersion.Version;
        if (currentVersion == "unknown" || string.IsNullOrEmpty(currentVersion))
        {
            Assert.Inconclusive("SDK version is unknown — cannot verify IsDeprecated behavior");
            return;
        }

        var testMinVersion = new Version(2, 4, 1);
        AITDeprecationChecker.SetMinVersionForTesting(testMinVersion);

        var version = new Version(currentVersion);
        bool expected = version < testMinVersion;
        bool isDeprecated = AITDeprecationChecker.IsDeprecated();
        Assert.AreEqual(expected, isDeprecated,
            $"IsDeprecated should return {expected} for SDK v{currentVersion} (minVersion: {testMinVersion})");
    }

    // =====================================================
    // Version 비교 로직 직접 검증
    // =====================================================

    [TestCase("1.0.0", true)]
    [TestCase("1.5.0", true)]
    [TestCase("1.14.1", true)]
    [TestCase("2.0.0", true)]
    [TestCase("2.4.0", true)]
    [TestCase("2.4.1", false)]
    [TestCase("2.4.2", false)]
    [TestCase("2.5.0", false)]
    [TestCase("3.0.0", false)]
    public void VersionComparison_AgainstMinVersion_MatchesExpected(string versionStr, bool expectedDeprecated)
    {
        var version = new Version(versionStr);
        var threshold = new Version(2, 4, 1);
        bool result = version < threshold;
        Assert.AreEqual(expectedDeprecated, result,
            $"Version {versionStr} should {(expectedDeprecated ? "" : "not ")}be deprecated (minVersion: 2.4.1)");
    }

    // =====================================================
    // 지속적 다이얼로그 메서드 존재 검증
    // =====================================================

    [Test]
    public void ShowDeprecationDialogPersistent_MethodExists()
    {
        var method = checkerType.GetMethod("ShowDeprecationDialogPersistent",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method,
            "ShowDeprecationDialogPersistent should exist for persistent dialog behavior");
    }

    // =====================================================
    // ResetCache / SetMinVersionForTesting: 내부 API 존재 확인
    // =====================================================

    [Test]
    public void ResetCache_MethodExists()
    {
        var method = checkerType.GetMethod("ResetCache",
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(method, "ResetCache() should exist");
    }

    [Test]
    public void SetMinVersionForTesting_MethodExists()
    {
        var method = checkerType.GetMethod("SetMinVersionForTesting",
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(method, "SetMinVersionForTesting() should exist");
    }
}
