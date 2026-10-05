// -----------------------------------------------------------------------
// AITLowMemoryTierTests.cs - EditMode 저메모리 tier C# 정책 테스트
// Level 0: AITLowMemoryTier 의 tier→정책 매핑(mip 제한, 스트리밍 동시성 상한)과
//          BeforeSplashScreen 부트스트랩 애너테이션 검증 (순수 정적 함수, 빌드 불필요)
// -----------------------------------------------------------------------

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;

[TestFixture]
public class AITLowMemoryTierTests
{
    [TestCase(0, 0)]
    [TestCase(1, 0)]
    [TestCase(2, 1)]
    [TestCase(3, 1)]
    public void MipmapLimitFor_OnlyTier2Lowers(int tier, int expected)
    {
        Assert.AreEqual(expected, AITLowMemoryTier.MipmapLimitFor(tier));
    }

    [Test]
    public void StreamingConcurrencyCapFor_Tier0IsUnlimited_Tier1PlusIsOne()
    {
        Assert.AreEqual(AITLowMemoryTier.Unlimited, AITLowMemoryTier.StreamingConcurrencyCapFor(0));
        Assert.AreEqual(1, AITLowMemoryTier.StreamingConcurrencyCapFor(1));
        Assert.AreEqual(1, AITLowMemoryTier.StreamingConcurrencyCapFor(2));
    }

    [Test]
    public void ReadTier_OutsideWebGL_IsZero()
    {
        // 에디터/EditMode 에서는 jslib 가 없어 브릿지가 0 을 돌려준다.
        Assert.AreEqual(0, AITLowMemoryTier.ReadTier());
    }

    [Test]
    public void ClampStreamingConcurrency_Tier0_KeepsRequested()
    {
        // EditMode 의 tier 는 0 이다.
        Assert.AreEqual(4, AITLowMemoryTier.ClampStreamingConcurrency(4));
        Assert.AreEqual(0, AITLowMemoryTier.ClampStreamingConcurrency(0), "0 이하(미지정)는 건드리지 않는다");
    }

    [Test]
    public void Apply_Tier0_DoesNotTouchQualitySettings()
    {
#if UNITY_2022_2_OR_NEWER
        int before = QualitySettings.globalTextureMipmapLimit;
        AITLowMemoryTier.Apply(0);
        Assert.AreEqual(before, QualitySettings.globalTextureMipmapLimit);
#else
        int before = QualitySettings.masterTextureLimit;
        AITLowMemoryTier.Apply(0);
        Assert.AreEqual(before, QualitySettings.masterTextureLimit);
#endif
    }

    [Test]
    public void Bootstrap_IsAnnotatedBeforeSplashScreen()
    {
        var m = typeof(AITLowMemoryTier).GetMethod("Bootstrap", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(m, "Bootstrap 메서드가 있어야 한다");
        var attr = m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
        Assert.IsNotNull(attr, "RuntimeInitializeOnLoadMethod 애너테이션이 있어야 한다");
        Assert.AreEqual(RuntimeInitializeLoadType.BeforeSplashScreen, attr.loadType,
            "첫 씬 텍스처 업로드 전에 mip 제한이 걸려야 하므로 BeforeSplashScreen 이어야 한다");
    }
}
