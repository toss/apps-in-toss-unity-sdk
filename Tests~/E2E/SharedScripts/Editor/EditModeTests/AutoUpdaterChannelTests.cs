// -----------------------------------------------------------------------
// AutoUpdaterChannelTests.cs - EditMode AITAutoUpdater 채널 분류 테스트
// Level 0: stable-only 자동 업데이트 정책을 위한 fragment(ref) prerelease 판정 검증
// -----------------------------------------------------------------------

using System.Reflection;
using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class AutoUpdaterChannelTests
{
    // =====================================================
    // IsPrereleaseChannel: 메서드 존재 확인
    // =====================================================

    [Test]
    public void IsPrereleaseChannel_MethodExists()
    {
        var method = typeof(AITAutoUpdater).GetMethod("IsPrereleaseChannel",
            BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);
        Assert.IsNotNull(method, "IsPrereleaseChannel() should exist as a static method");
        Assert.AreEqual(typeof(bool), method.ReturnType);
    }

    // =====================================================
    // STABLE 채널 (false 기대) — 자동 업데이트 대상
    // =====================================================

    [TestCase("main")]
    [TestCase("master")]
    [TestCase("release/v2.6.1")]
    [TestCase("release/v3.0.0")]   // stable major 릴리즈 태그: '-' 없음
    [TestCase("release/v10.20.30")]
    [TestCase("2.6.1")]            // bare stable 버전
    [TestCase("develop")]
    [TestCase("development")]      // 'dev' 뒤에 'e' → 토큰 경계 불충족 → stable
    [TestCase("feature/login")]
    [TestCase("hotfix-123")]
    public void IsPrereleaseChannel_StableRefs_ReturnsFalse(string fragment)
    {
        Assert.IsFalse(AITAutoUpdater.IsPrereleaseChannel(fragment),
            $"'{fragment}' should be classified as STABLE (auto-update eligible)");
    }

    // =====================================================
    // PRERELEASE 채널 (true 기대) — 자동 업데이트 제외
    // =====================================================

    [TestCase("beta")]
    [TestCase("alpha")]
    [TestCase("rc")]
    [TestCase("rc1")]
    [TestCase("canary")]
    [TestCase("nightly")]
    [TestCase("next")]
    [TestCase("preview")]
    [TestCase("3.0.0-beta.9d42c0b")]
    [TestCase("release/v3.0.0-beta")]
    [TestCase("release/v3.0.0-rc.1")]
    [TestCase("release/v3.0.0-beta.9d42c0b")]
    [TestCase("feature/preview-x")]
    [TestCase("dev.2")]
    [TestCase("x_canary")]
    public void IsPrereleaseChannel_PrereleaseRefs_ReturnsTrue(string fragment)
    {
        Assert.IsTrue(AITAutoUpdater.IsPrereleaseChannel(fragment),
            $"'{fragment}' should be classified as PRERELEASE (auto-update excluded)");
    }

    // =====================================================
    // 경계 조건: null / 빈 문자열
    // =====================================================

    [Test]
    public void IsPrereleaseChannel_Null_ReturnsFalse()
    {
        Assert.IsFalse(AITAutoUpdater.IsPrereleaseChannel(null),
            "null fragment should be treated as non-prerelease (stable path handles empty separately)");
    }

    [Test]
    public void IsPrereleaseChannel_Empty_ReturnsFalse()
    {
        Assert.IsFalse(AITAutoUpdater.IsPrereleaseChannel(string.Empty),
            "empty fragment should be treated as non-prerelease");
    }

    // =====================================================
    // 대소문자 무시 확인
    // =====================================================

    [TestCase("BETA")]
    [TestCase("Beta")]
    [TestCase("RELEASE/v3.0.0-BETA")]
    public void IsPrereleaseChannel_CaseInsensitive_ReturnsTrue(string fragment)
    {
        Assert.IsTrue(AITAutoUpdater.IsPrereleaseChannel(fragment),
            $"'{fragment}' should match prerelease markers case-insensitively");
    }
}
