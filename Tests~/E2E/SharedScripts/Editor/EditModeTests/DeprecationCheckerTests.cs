// -----------------------------------------------------------------------
// DeprecationCheckerTests.cs - EditMode SDK Deprecation 검증 테스트
// Level 0: 버전 비교 로직 및 git tag 파싱 로직을 검증
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.Reflection;
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

    private MethodInfo GetParseLatestTagMethod()
    {
        var method = checkerType.GetMethod("ParseLatestTag",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "ParseLatestTag should exist as a private static method");
        return method;
    }

    [Test]
    public void ParseLatestTag_SingleTag_ReturnsCorrectTag()
    {
        var method = GetParseLatestTagMethod();
        string input = "abc123def456\trefs/tags/release/v2.0.0\n";
        string result = (string)method.Invoke(null, new object[] { input });
        Assert.AreEqual("release/v2.0.0", result);
    }

    [Test]
    public void ParseLatestTag_MultipleTags_ReturnsHighestVersion()
    {
        var method = GetParseLatestTagMethod();
        string input =
            "aaa\trefs/tags/release/v2.0.0\n" +
            "bbb\trefs/tags/release/v2.0.3\n" +
            "ccc\trefs/tags/release/v2.0.1\n" +
            "ddd\trefs/tags/release/v2.0.5\n" +
            "eee\trefs/tags/release/v2.0.2\n";
        string result = (string)method.Invoke(null, new object[] { input });
        Assert.AreEqual("release/v2.0.5", result);
    }

    [Test]
    public void ParseLatestTag_WithAnnotatedTagDerefs_IgnoresThem()
    {
        var method = GetParseLatestTagMethod();
        string input =
            "aaa\trefs/tags/release/v2.0.5\n" +
            "bbb\trefs/tags/release/v2.0.5^{}\n" +
            "ccc\trefs/tags/release/v2.1.0\n" +
            "ddd\trefs/tags/release/v2.1.0^{}\n";
        string result = (string)method.Invoke(null, new object[] { input });
        Assert.AreEqual("release/v2.1.0", result);
    }

    [Test]
    public void ParseLatestTag_EmptyOutput_ReturnsNull()
    {
        var method = GetParseLatestTagMethod();
        string result = (string)method.Invoke(null, new object[] { "" });
        Assert.IsNull(result);
    }

    [Test]
    public void ParseLatestTag_InvalidLines_ReturnsNull()
    {
        var method = GetParseLatestTagMethod();
        string input = "not-a-valid-line\nanother-invalid\n";
        string result = (string)method.Invoke(null, new object[] { input });
        Assert.IsNull(result);
    }

    [Test]
    public void ParseLatestTag_MixedValidAndInvalid_ReturnsHighestValid()
    {
        var method = GetParseLatestTagMethod();
        string input =
            "aaa\trefs/tags/release/v2.0.0\n" +
            "invalid-line\n" +
            "bbb\trefs/tags/not-a-release/v9.9.9\n" +
            "ccc\trefs/tags/release/v2.1.0\n";
        string result = (string)method.Invoke(null, new object[] { input });
        Assert.AreEqual("release/v2.1.0", result);
    }

    [Test]
    public void ParseLatestTag_HigherMinorAndPatch_ComparesCorrectly()
    {
        var method = GetParseLatestTagMethod();
        string input =
            "aaa\trefs/tags/release/v2.0.9\n" +
            "bbb\trefs/tags/release/v2.1.0\n" +
            "ccc\trefs/tags/release/v2.0.10\n";
        string result = (string)method.Invoke(null, new object[] { input });
        Assert.AreEqual("release/v2.1.0", result);
    }

    // =====================================================
    // DeprecationThreshold: 상수 검증
    // =====================================================

    [Test]
    public void DeprecationThreshold_Is_v2_0_0()
    {
        var field = checkerType.GetField("DeprecationThreshold",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "DeprecationThreshold field should exist");

        var threshold = (Version)field.GetValue(null);
        Assert.AreEqual(new Version(2, 0, 0), threshold,
            "Deprecation threshold should be 2.0.0");
    }

    [Test]
    public void FallbackUpgradeTag_IsValid()
    {
        var field = checkerType.GetField("FALLBACK_UPGRADE_TAG",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(field, "FALLBACK_UPGRADE_TAG field should exist");

        string tag = (string)field.GetValue(null);
        Assert.IsTrue(tag.StartsWith("release/v2."),
            $"Fallback tag should start with 'release/v2.', got: {tag}");
    }

    // =====================================================
    // IsDeprecated: 현재 SDK 버전 기반 동작 확인
    // =====================================================

    [Test]
    public void IsDeprecated_CurrentSDK_ReturnsExpectedValue()
    {
        // 현재 SDK 버전을 기준으로 IsDeprecated가 올바르게 동작하는지 확인
        string currentVersion = AppsInToss.AITVersion.Version;
        bool isDeprecated = AITDeprecationChecker.IsDeprecated();

        if (currentVersion == "unknown" || string.IsNullOrEmpty(currentVersion))
        {
            Assert.IsFalse(isDeprecated,
                "IsDeprecated should return false for unknown version");
        }
        else
        {
            var version = new Version(currentVersion);
            var threshold = new Version(2, 0, 0);
            bool expected = version < threshold;
            Assert.AreEqual(expected, isDeprecated,
                $"IsDeprecated should return {expected} for SDK v{currentVersion}");
        }
    }

    // =====================================================
    // Version 비교 로직 직접 검증
    // =====================================================

    [TestCase("1.0.0", true)]
    [TestCase("1.5.0", true)]
    [TestCase("1.14.1", true)]
    [TestCase("1.99.99", true)]
    [TestCase("2.0.0", false)]
    [TestCase("2.0.1", false)]
    [TestCase("2.1.0", false)]
    [TestCase("3.0.0", false)]
    public void VersionComparison_MatchesExpected(string versionStr, bool expectedDeprecated)
    {
        var version = new Version(versionStr);
        var threshold = new Version(2, 0, 0);
        bool result = version < threshold;
        Assert.AreEqual(expectedDeprecated, result,
            $"Version {versionStr} should {(expectedDeprecated ? "" : "not ")}be deprecated");
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
}
