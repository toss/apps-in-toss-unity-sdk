// -----------------------------------------------------------------------
// SentryReleaseResolverTests.cs - EditMode AITSentryReleaseResolver 단위 테스트
// Level 0: prerelease SDK 빌드 시 Sentry environment/release 자동 분리 로직 검증
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss;

[TestFixture]
public class SentryReleaseResolverTests
{
    // =====================================================
    // IsPrerelease: prerelease 버전 (true 기대)
    // =====================================================

    [TestCase("3.0.0-beta.9d42c0b")]
    [TestCase("3.0.0-rc.1")]
    [TestCase("3.0.0-alpha")]
    [TestCase("1.0.0-next.5")]
    [TestCase("3.0.0-beta.9d42c0b+20240101")]   // prerelease + build metadata → prerelease
    public void IsPrerelease_PrereleaseVersions_ReturnsTrue(string version)
    {
        Assert.IsTrue(
            AITSentryReleaseResolver.IsPrerelease(version),
            $"'{version}' should be classified as prerelease");
    }

    // =====================================================
    // IsPrerelease: stable 버전 (false 기대)
    // =====================================================

    [TestCase("2.6.1")]
    [TestCase("3.0.0")]
    [TestCase("10.20.30")]
    [TestCase("3.0.0-")]                   // 빈 trailing hyphen → SemVer상 prerelease 식별자 없음 → stable
    [TestCase("2.6.1+build")]              // build metadata만 → stable
    [TestCase("2.6.1+build-20240101")]     // build metadata 내 hyphen은 무시 → stable
    public void IsPrerelease_StableVersions_ReturnsFalse(string version)
    {
        Assert.IsFalse(
            AITSentryReleaseResolver.IsPrerelease(version),
            $"'{version}' should be classified as stable");
    }

    // =====================================================
    // IsPrerelease: 누락/unknown/공백 (false 기대)
    // =====================================================

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("unknown")]
    public void IsPrerelease_MissingOrUnknown_ReturnsFalse(string version)
    {
        Assert.IsFalse(
            AITSentryReleaseResolver.IsPrerelease(version),
            $"'{version}' should not be treated as prerelease");
    }

    // =====================================================
    // ResolveEnvironment: 자동 파생 (override 없음)
    // =====================================================

    [Test]
    public void ResolveEnvironment_Prerelease_NoOverride_ReturnsBeta()
    {
        Assert.AreEqual(
            "beta",
            AITSentryReleaseResolver.ResolveEnvironment("3.0.0-beta.9d42c0b", null));
    }

    [TestCase("2.6.1")]
    [TestCase("3.0.0")]
    public void ResolveEnvironment_Stable_NoOverride_ReturnsNull(string version)
    {
        // stable은 null → Sentry 기본값 "production" 유지 (동작 불변)
        Assert.IsNull(AITSentryReleaseResolver.ResolveEnvironment(version, null));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("unknown")]
    [TestCase("3.0.0-")]   // prerelease 식별자가 비어 있어 stable로 취급 → environment 미설정
    public void ResolveEnvironment_UnknownVersion_NoOverride_ReturnsNull(string version)
    {
        Assert.IsNull(AITSentryReleaseResolver.ResolveEnvironment(version, null));
    }

    // =====================================================
    // ResolveEnvironment: 명시 override 우선
    // =====================================================

    [TestCase("3.0.0-beta.9d42c0b")]   // prerelease여도 override가 이김
    [TestCase("2.6.1")]
    [TestCase("unknown")]
    [TestCase(null)]
    public void ResolveEnvironment_ExplicitOverride_Wins(string version)
    {
        Assert.AreEqual(
            "staging",
            AITSentryReleaseResolver.ResolveEnvironment(version, "staging"));
    }

    // =====================================================
    // ResolveRelease: 자동 파생 (override 없음)
    // =====================================================

    [Test]
    public void ResolveRelease_Prerelease_NoOverride_PrefixesVersion()
    {
        Assert.AreEqual(
            "apps-in-toss.unity@3.0.0-beta.9d42c0b",
            AITSentryReleaseResolver.ResolveRelease("3.0.0-beta.9d42c0b", null));
    }

    [Test]
    public void ResolveRelease_Stable_NoOverride_PrefixesVersion()
    {
        Assert.AreEqual(
            "apps-in-toss.unity@2.6.1",
            AITSentryReleaseResolver.ResolveRelease("2.6.1", null));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("unknown")]
    public void ResolveRelease_UnknownVersion_NoOverride_ReturnsNull(string version)
    {
        Assert.IsNull(AITSentryReleaseResolver.ResolveRelease(version, null));
    }

    [Test]
    public void ResolveRelease_TrailingHyphenVersion_PassesThroughFaithfully()
    {
        // "3.0.0-"은 stable로 분류(environment 미설정)되지만, release는 실제 버전 문자열을 그대로 반영한다.
        Assert.AreEqual(
            "apps-in-toss.unity@3.0.0-",
            AITSentryReleaseResolver.ResolveRelease("3.0.0-", null));
    }

    // =====================================================
    // ResolveRelease: 명시 override 우선
    // =====================================================

    [TestCase("3.0.0-beta.9d42c0b")]
    [TestCase("2.6.1")]
    [TestCase("unknown")]
    [TestCase(null)]
    public void ResolveRelease_ExplicitOverride_Wins(string version)
    {
        // 버전을 알 수 없어도(override가 있으면) override가 이김
        Assert.AreEqual(
            "my-app@1.2.3",
            AITSentryReleaseResolver.ResolveRelease(version, "my-app@1.2.3"));
    }
}
