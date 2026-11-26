// -----------------------------------------------------------------------
// AITBannerBoundsTests.cs - 배너 범위(AITBannerBounds) 계산 순수 함수 검증
// - GameArea는 Camera.rect의 가로 범위(x/width)만 쓰고 세로는 건드리지 않는다
//   (카메라를 세로로 줄여 배너 자리를 비우는 게임에서 배너가 게임 안으로 밀려 들어오지 않도록)
// - Custom은 정규화 rect를 0~1로 보정하고 가로/세로 가장자리를 모두 적용한다
// - 기존(범위 미지정) Show 경로와 default 값은 항상 Screen = 자동 감지 없음
// -----------------------------------------------------------------------

using AppsInToss;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
[Category("Unit")]
public class AITBannerBoundsTests
{
    private const float Tolerance = 0.0001f;

    #region 기존 경로 = Screen (자동 감지 없음)

    [Test]
    public void DefaultBounds_IsScreen()
    {
        Assert.AreEqual(AITBannerBoundsKind.Screen, default(AITBannerBounds).Kind);
        Assert.AreEqual(AITBannerBoundsKind.Screen, AITBannerBounds.Screen.Kind);
    }

    [Test]
    public void LegacyShowBounds_IsScreen()
    {
        // 기존 Show(adGroupId, position, theme, ...) 오버로드가 쓰는 범위
        Assert.AreEqual(AITBannerBoundsKind.Screen, AITBannerAd.LegacyBounds.Kind);
    }

    [Test]
    public void Resolve_Screen_IgnoresLetterboxedCamera()
    {
        // 카메라가 가운데 50%로 줄어 있어도 Screen은 영역을 만들지 않는다
        int edges = AITBannerBoundsMath.Resolve(
            AITBannerBoundsKind.Screen, default, true, new Rect(0.25f, 0f, 0.5f, 1f), out var area);
        Assert.AreEqual(0, edges);
        Assert.AreEqual(default(Rect), area);
    }

    [Test]
    public void Factories_ProduceExpectedKinds()
    {
        Assert.AreEqual(AITBannerBoundsKind.GameArea, AITBannerBounds.GameArea.Kind);
        Assert.AreEqual(AITBannerBoundsKind.CustomRect, AITBannerBounds.Custom(new Rect(0.1f, 0.1f, 0.5f, 0.5f)).Kind);
        Assert.AreEqual(AITBannerBoundsKind.CustomRectTransform, AITBannerBounds.Custom((RectTransform)null).Kind);
    }

    #endregion

    #region GameArea (Camera.rect 가로만)

    [Test]
    public void Resolve_GameArea_NoCamera_ReturnsNoEdges()
    {
        Assert.AreEqual(0, AITBannerBoundsMath.Resolve(AITBannerBoundsKind.GameArea, default, false, default, out _));
    }

    [Test]
    public void Resolve_GameArea_FullCameraRect_ReturnsNoEdges()
    {
        Assert.AreEqual(0, AITBannerBoundsMath.Resolve(
            AITBannerBoundsKind.GameArea, default, true, new Rect(0f, 0f, 1f, 1f), out _));
    }

    [Test]
    public void Resolve_GameArea_CenteredPillarbox_UsesHorizontalEdgesOnly()
    {
        int edges = AITBannerBoundsMath.Resolve(
            AITBannerBoundsKind.GameArea, default, true, new Rect(0.25f, 0f, 0.5f, 1f), out var area);
        Assert.AreEqual(AITBannerBoundsMath.EdgesHorizontal, edges);
        Assert.AreEqual(0.25f, area.x, Tolerance);
        Assert.AreEqual(0.5f, area.width, Tolerance);
        Assert.AreEqual(0f, area.y, Tolerance);
        Assert.AreEqual(1f, area.height, Tolerance);
    }

    [Test]
    public void Resolve_GameArea_VerticalOnlyShrink_ReturnsNoEdges()
    {
        // 배너 자리를 비우려고 카메라를 세로로만 줄인 게임 — 가로는 전체라 배너는 화면 가장자리 그대로
        Assert.AreEqual(0, AITBannerBoundsMath.Resolve(
            AITBannerBoundsKind.GameArea, default, true, new Rect(0f, 0.15f, 1f, 0.85f), out _));
    }

    [Test]
    public void Resolve_GameArea_PillarboxWithVerticalShrink_IgnoresVertical()
    {
        Assert.IsTrue(AITBannerBoundsMath.TryHorizontalFromCameraRect(new Rect(0.2f, 0.3f, 0.6f, 0.5f), out var area));
        Assert.AreEqual(0.2f, area.x, Tolerance);
        Assert.AreEqual(0.6f, area.width, Tolerance);
        Assert.AreEqual(0f, area.y, Tolerance);
        Assert.AreEqual(1f, area.height, Tolerance);
    }

    [Test]
    public void TryHorizontalFromCameraRect_AlmostFull_ReturnsFalse()
    {
        Assert.IsFalse(AITBannerBoundsMath.TryHorizontalFromCameraRect(new Rect(0.0001f, 0f, 0.9998f, 1f), out _));
    }

    [Test]
    public void TryHorizontalFromCameraRect_ZeroWidthOrNaN_ReturnsFalse()
    {
        Assert.IsFalse(AITBannerBoundsMath.TryHorizontalFromCameraRect(new Rect(0.5f, 0f, 0f, 1f), out _));
        Assert.IsFalse(AITBannerBoundsMath.TryHorizontalFromCameraRect(new Rect(float.NaN, 0f, 0.5f, 1f), out _));
    }

    #endregion

    #region Custom (정규화 rect, 가로/세로 모두)

    [Test]
    public void Resolve_CustomRect_AppliesAllEdges()
    {
        int edges = AITBannerBoundsMath.Resolve(
            AITBannerBoundsKind.CustomRect, new Rect(0.1f, 0.2f, 0.5f, 0.6f), false, default, out var area);
        Assert.AreEqual(AITBannerBoundsMath.EdgesAll, edges);
        Assert.AreEqual(0.1f, area.x, Tolerance);
        Assert.AreEqual(0.2f, area.y, Tolerance);
        Assert.AreEqual(0.5f, area.width, Tolerance);
        Assert.AreEqual(0.6f, area.height, Tolerance);
    }

    [Test]
    public void Resolve_CustomRect_FullOrInvalid_ReturnsNoEdges()
    {
        Assert.AreEqual(0, AITBannerBoundsMath.Resolve(AITBannerBoundsKind.CustomRect, new Rect(0f, 0f, 1f, 1f), false, default, out _));
        Assert.AreEqual(0, AITBannerBoundsMath.Resolve(AITBannerBoundsKind.CustomRect, new Rect(0.3f, 0f, 0f, 1f), false, default, out _));
        Assert.AreEqual(0, AITBannerBoundsMath.Resolve(AITBannerBoundsKind.CustomRectTransform, default, false, default, out _));
    }

    [Test]
    public void TryNormalize_OutOfRange_IsClampedToUnitSquare()
    {
        Assert.IsTrue(AITBannerBoundsMath.TryNormalize(new Rect(-0.2f, 0.1f, 0.7f, 2f), out var area));
        Assert.AreEqual(0f, area.xMin, Tolerance);
        Assert.AreEqual(0.5f, area.xMax, Tolerance);
        Assert.AreEqual(0.1f, area.yMin, Tolerance);
        Assert.AreEqual(1f, area.yMax, Tolerance);
    }

    [Test]
    public void TryNormalize_NaN_ReturnsFalse()
    {
        Assert.IsFalse(AITBannerBoundsMath.TryNormalize(new Rect(float.NaN, 0f, 0.5f, 1f), out _));
    }

    #endregion

    #region TryFromScreenCorners (RectTransform 투영)

    [Test]
    public void TryFromScreenCorners_CenteredColumn_MapsToNormalizedTopLeft()
    {
        // 1600x900 화면, 가운데 9:16 폭 전체 높이
        float w = 900f * 9f / 16f;
        float left = (1600f - w) * 0.5f;
        Assert.IsTrue(AITBannerBoundsMath.TryFromScreenCorners(
            new Vector2(left, 0f), new Vector2(left + w, 900f), 1600f, 900f, out var area));
        Assert.AreEqual(left / 1600f, area.x, Tolerance);
        Assert.AreEqual(0f, area.y, Tolerance);
        Assert.AreEqual(w / 1600f, area.width, Tolerance);
        Assert.AreEqual(1f, area.height, Tolerance);
    }

    [Test]
    public void TryFromScreenCorners_SwappedCorners_GiveSameResult()
    {
        Assert.IsTrue(AITBannerBoundsMath.TryFromScreenCorners(
            new Vector2(100f, 100f), new Vector2(500f, 700f), 1000f, 1000f, out var a));
        Assert.IsTrue(AITBannerBoundsMath.TryFromScreenCorners(
            new Vector2(500f, 700f), new Vector2(100f, 100f), 1000f, 1000f, out var b));
        Assert.IsTrue(AITBannerBoundsMath.ApproximatelyEqual(a, b));
        // 화면 상단에서 300px 아래(= 위쪽 띠 30%)에서 시작
        Assert.AreEqual(0.3f, a.y, Tolerance);
    }

    [Test]
    public void TryFromScreenCorners_InvalidScreenSizeOrWholeScreen_ReturnsFalse()
    {
        Assert.IsFalse(AITBannerBoundsMath.TryFromScreenCorners(Vector2.zero, Vector2.one, 0f, 0f, out _));
        Assert.IsFalse(AITBannerBoundsMath.TryFromScreenCorners(Vector2.zero, new Vector2(1000f, 800f), 1000f, 800f, out _));
    }

    #endregion

    #region ApproximatelyEqual

    [Test]
    public void ApproximatelyEqual_WithinAndBeyondEpsilon()
    {
        Assert.IsTrue(AITBannerBoundsMath.ApproximatelyEqual(
            new Rect(0.25f, 0f, 0.5f, 1f), new Rect(0.25f + 0.0001f, 0f, 0.5f, 1f)));
        Assert.IsFalse(AITBannerBoundsMath.ApproximatelyEqual(
            new Rect(0.25f, 0f, 0.5f, 1f), new Rect(0.26f, 0f, 0.5f, 1f)));
    }

    #endregion
}
