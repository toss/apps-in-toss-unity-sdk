// -----------------------------------------------------------------------
// UIBuilderDragThresholdTests.cs - 화면 밀도에 따른 드래그 임계값 환산 검증
// Level 0: UIBuilder.ScaledDragThreshold 를 Unity 런타임 없이 검증한다.
//
// 배경: EventSystem.pixelDragThreshold는 탭과 드래그를 가르는 이동 거리다. 기본값 10은
//   스크린 픽셀 기준이라 화면 밀도가 높을수록 물리 거리가 짧아진다 — WebGL에서 스크린
//   픽셀은 캔버스 프레임버퍼 픽셀이고 이는 CSS 픽셀의 devicePixelRatio배다. 배율 2인
//   기기에서는 1.5mm가 안 되어 가만히 눌러도 드래그로 판정된다.
//
//   그래서 기본값을 CSS 기준 밀도(96dpi)에서의 물리 거리로 보고 밀도에 비례해
//   환산한다. WebGL의 Screen.dpi는 devicePixelRatio * 96이므로 결과는 배율과
//   무관하게 항상 10 CSS 픽셀이 된다.
// -----------------------------------------------------------------------

using NUnit.Framework;

[TestFixture]
public class UIBuilderDragThresholdTests
{
    private const int UnityDefaultThreshold = 10;

    // =====================================================
    // 밀도별 환산
    // =====================================================

    [Test]
    public void ScaledDragThreshold_CssBaselineDensity_LeavesThresholdUnchanged()
    {
        // devicePixelRatio 1 — 데스크톱 브라우저, 그리고 MOBILE_EMULATION이 아닌
        // Playwright 프로필이 여기 해당한다. 이 경로는 기본값이 유지돼야 한다.
        Assert.AreEqual(
            UnityDefaultThreshold,
            UIBuilder.ScaledDragThreshold(UnityDefaultThreshold, 96f));
    }

    [Test]
    public void ScaledDragThreshold_DoubleDensity_YieldsTenCssPixels()
    {
        // devicePixelRatio 2 → 20 프레임버퍼 픽셀 = 10 CSS 픽셀.
        // 실기기(iPhone 15 Pro)와 CI의 MOBILE_EMULATION leg(iPhone 8 프로필) 둘 다
        // 템플릿의 getOptimalDevicePixelRatio가 배율을 2로 깎아 Screen.dpi가 192가 된다.
        Assert.AreEqual(20, UIBuilder.ScaledDragThreshold(UnityDefaultThreshold, 192f));
    }

    [Test]
    public void ScaledDragThreshold_TripleDensity_YieldsTenCssPixels()
    {
        // devicePixelRatio 3 → 30 프레임버퍼 픽셀 = 역시 10 CSS 픽셀.
        // 배율이 달라져도 물리 거리가 일정하게 유지되는지 확인한다.
        Assert.AreEqual(30, UIBuilder.ScaledDragThreshold(UnityDefaultThreshold, 288f));
    }

    // =====================================================
    // 임계값을 낮추지 않는다
    // =====================================================

    [Test]
    public void ScaledDragThreshold_UnknownDensity_FallsBackToBase()
    {
        // Screen.dpi가 0을 반환하는 환경. 임의로 키우면 스크롤이 둔해지므로 기본값을 유지한다.
        Assert.AreEqual(UnityDefaultThreshold, UIBuilder.ScaledDragThreshold(UnityDefaultThreshold, 0f));
        Assert.AreEqual(UnityDefaultThreshold, UIBuilder.ScaledDragThreshold(UnityDefaultThreshold, -1f));
    }

    [Test]
    public void ScaledDragThreshold_BelowBaselineDensity_DoesNotShrinkThreshold()
    {
        // 기준보다 낮은 밀도에서 임계값을 줄이면 스크롤 도중 원치 않는 클릭이 발생한다.
        Assert.AreEqual(UnityDefaultThreshold, UIBuilder.ScaledDragThreshold(UnityDefaultThreshold, 48f));
    }

    // =====================================================
    // 경계
    // =====================================================

    [Test]
    public void ScaledDragThreshold_FractionalRatio_RoundsToNearest()
    {
        // devicePixelRatio 1.5 → 15. 나눗셈이 정수로 떨어지는 경우.
        Assert.AreEqual(15, UIBuilder.ScaledDragThreshold(UnityDefaultThreshold, 144f));

        // 아래 두 케이스는 소수부가 0.5 양쪽에 하나씩 놓이도록 골랐다.
        // 반올림을 올림이나 버림으로 바꾸면 둘 중 하나가 반드시 깨진다.
        // 11.458... → 11 (올림이면 12)
        Assert.AreEqual(11, UIBuilder.ScaledDragThreshold(UnityDefaultThreshold, 110f));
        // 13.541... → 14 (버림이면 13)
        Assert.AreEqual(14, UIBuilder.ScaledDragThreshold(UnityDefaultThreshold, 130f));
    }
}
