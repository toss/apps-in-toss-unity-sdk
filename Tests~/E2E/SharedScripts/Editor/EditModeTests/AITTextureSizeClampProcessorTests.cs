// -----------------------------------------------------------------------
// AITTextureSizeClampProcessorTests.cs - 텍스처 size clamp 순수 로직 검증
// Level 0: AssetDatabase 비의존 순수 헬퍼 함수 EditMode 테스트
//
// 핵심 불변식(#5 plat-override-ignored 회귀 가드):
//   빌드가 실제로 ship 하는 maxTextureSize 는 WebGL 오버라이드가 있으면 그 값,
//   없으면 base. clamp 게이트/적용은 이 "effective" 값을 기준으로 동작해야 한다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class AITTextureSizeClampProcessorTests
{
    // 1) WebGL 오버라이드가 있고 maxTextureSize>0 이면 그 값이 effective(빌드가 우선).
    [Test]
    public void ResolveEffectiveMaxSize_OverriddenWithPositive_ReturnsOverride()
    {
        Assert.AreEqual(2048,
            AITTextureSizeClampProcessor.ResolveEffectiveMaxSize(true, 2048, 1024),
            "WebGL 오버라이드(overridden=true, >0)가 있으면 빌드는 그 값을 ship 하므로 effective=오버라이드 여야 합니다.");
    }

    // 2) 오버라이드가 base 보다 작아도 그대로 반환(빌드가 더 작은 오버라이드를 ship).
    [Test]
    public void ResolveEffectiveMaxSize_OverrideSmallerThanBase_ReturnsOverride()
    {
        Assert.AreEqual(512,
            AITTextureSizeClampProcessor.ResolveEffectiveMaxSize(true, 512, 4096),
            "오버라이드가 base 보다 작아도 빌드는 오버라이드를 ship 하므로 effective=오버라이드(512) 여야 합니다.");
    }

    // 3) overridden=true 지만 maxTextureSize==0(미설정)이면 base 로 폴백.
    [Test]
    public void ResolveEffectiveMaxSize_OverriddenButZero_FallsBackToBase()
    {
        Assert.AreEqual(1024,
            AITTextureSizeClampProcessor.ResolveEffectiveMaxSize(true, 0, 1024),
            "오버라이드가 설정돼 있어도 maxTextureSize==0 이면 의미 있는 값이 아니므로 base 로 폴백해야 합니다.");
    }

    // 4) 오버라이드가 없으면 base 가 effective.
    [Test]
    public void ResolveEffectiveMaxSize_NotOverridden_ReturnsBase()
    {
        Assert.AreEqual(4096,
            AITTextureSizeClampProcessor.ResolveEffectiveMaxSize(false, 2048, 4096),
            "오버라이드가 없으면 빌드는 base 를 ship 하므로 effective=base 여야 합니다(오버라이드 인자 무시).");
    }

    // 5) 게이트 시나리오: base 1024(≤cap) 인데 오버라이드 2048(>cap) → effective 2048 로 clamp 대상.
    //    이게 #5 의 핵심 — base 만 봤다면 skip 되어 오버라이드 2048 이 그대로 ship 됐을 것.
    [Test]
    public void ResolveEffectiveMaxSize_BaseUnderCapButOverrideOver_ExposesOverride()
    {
        const int cap = 1024;
        int effective = AITTextureSizeClampProcessor.ResolveEffectiveMaxSize(true, 2048, 1024);
        Assert.Greater(effective, cap,
            "base(1024)는 cap 이하라 skip 됐겠지만 오버라이드(2048)는 cap 초과 → effective 가 cap 을 넘어 clamp 대상이어야 합니다(#5).");
    }
}
