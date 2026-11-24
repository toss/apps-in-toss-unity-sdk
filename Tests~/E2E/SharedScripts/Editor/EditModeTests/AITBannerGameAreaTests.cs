// -----------------------------------------------------------------------
// AITBannerGameAreaTests.cs - 배너 게임 렌더 영역(레터박스) 정규화 순수 함수 검증
// Camera.rect(좌하단 원점)/화면 좌표 사각형을 좌상단 원점 정규화 영역으로 바꾸고,
// 전체 화면·무효 값은 "영역 없음"(기존 전체 폭 동작)으로 처리하는지 확인한다.
// -----------------------------------------------------------------------

using AppsInToss;
using NUnit.Framework;
using UnityEngine;

[TestFixture]
[Category("Unit")]
public class AITBannerGameAreaTests
{
    private const float Tolerance = 0.0001f;

    #region TryFromCameraRect

    [Test]
    public void TryFromCameraRect_FullRect_ReturnsFalse()
    {
        Assert.IsFalse(AITBannerGameArea.TryFromCameraRect(new Rect(0f, 0f, 1f, 1f), out _));
    }

    [Test]
    public void TryFromCameraRect_AlmostFullRect_ReturnsFalse()
    {
        Assert.IsFalse(AITBannerGameArea.TryFromCameraRect(new Rect(0.0001f, 0f, 0.9998f, 1f), out _));
    }

    [Test]
    public void TryFromCameraRect_CenteredPillarbox_ReturnsCenteredArea()
    {
        // 가로 50% 가운데 — 세로는 전체
        Assert.IsTrue(AITBannerGameArea.TryFromCameraRect(new Rect(0.25f, 0f, 0.5f, 1f), out var area));
        Assert.AreEqual(0.25f, area.x, Tolerance);
        Assert.AreEqual(0f, area.y, Tolerance);
        Assert.AreEqual(0.5f, area.width, Tolerance);
        Assert.AreEqual(1f, area.height, Tolerance);
    }

    [Test]
    public void TryFromCameraRect_Letterbox_FlipsYToTopLeftOrigin()
    {
        // Camera.rect 하단 20% 띠 + 상단 10% 띠 → 좌상단 기준 y는 0.1
        Assert.IsTrue(AITBannerGameArea.TryFromCameraRect(new Rect(0f, 0.2f, 1f, 0.7f), out var area));
        Assert.AreEqual(0f, area.x, Tolerance);
        Assert.AreEqual(0.1f, area.y, Tolerance);
        Assert.AreEqual(1f, area.width, Tolerance);
        Assert.AreEqual(0.7f, area.height, Tolerance);
    }

    [Test]
    public void TryFromCameraRect_ZeroSize_ReturnsFalse()
    {
        Assert.IsFalse(AITBannerGameArea.TryFromCameraRect(new Rect(0.5f, 0.5f, 0f, 0f), out _));
    }

    #endregion

    #region TryNormalize

    [Test]
    public void TryNormalize_OutOfRange_IsClampedToUnitSquare()
    {
        Assert.IsTrue(AITBannerGameArea.TryNormalize(new Rect(-0.2f, 0.1f, 0.7f, 2f), out var area));
        Assert.AreEqual(0f, area.xMin, Tolerance);
        Assert.AreEqual(0.5f, area.xMax, Tolerance);
        Assert.AreEqual(0.1f, area.yMin, Tolerance);
        Assert.AreEqual(1f, area.yMax, Tolerance);
    }

    [Test]
    public void TryNormalize_NegativeOrZeroWidth_ReturnsFalse()
    {
        Assert.IsFalse(AITBannerGameArea.TryNormalize(new Rect(0.3f, 0f, 0f, 1f), out _));
        Assert.IsFalse(AITBannerGameArea.TryNormalize(new Rect(0.3f, 0f, 0.4f, 0f), out _));
    }

    [Test]
    public void TryNormalize_NaN_ReturnsFalse()
    {
        Assert.IsFalse(AITBannerGameArea.TryNormalize(new Rect(float.NaN, 0f, 0.5f, 1f), out _));
    }

    [Test]
    public void TryNormalize_FullScreen_ReturnsFalse()
    {
        Assert.IsFalse(AITBannerGameArea.TryNormalize(new Rect(0f, 0f, 1f, 1f), out _));
    }

    #endregion

    #region TryFromScreenCorners

    [Test]
    public void TryFromScreenCorners_CenteredColumn_MapsToNormalizedTopLeft()
    {
        // 1600x900 화면, 가운데 506.25px(9:16) 폭 전체 높이
        float w = 900f * 9f / 16f;
        float left = (1600f - w) * 0.5f;
        Assert.IsTrue(AITBannerGameArea.TryFromScreenCorners(
            new Vector2(left, 0f), new Vector2(left + w, 900f), 1600f, 900f, out var area));
        Assert.AreEqual(left / 1600f, area.x, Tolerance);
        Assert.AreEqual(0f, area.y, Tolerance);
        Assert.AreEqual(w / 1600f, area.width, Tolerance);
        Assert.AreEqual(1f, area.height, Tolerance);
    }

    [Test]
    public void TryFromScreenCorners_SwappedCorners_GiveSameResult()
    {
        Assert.IsTrue(AITBannerGameArea.TryFromScreenCorners(
            new Vector2(100f, 100f), new Vector2(500f, 700f), 1000f, 1000f, out var a));
        Assert.IsTrue(AITBannerGameArea.TryFromScreenCorners(
            new Vector2(500f, 700f), new Vector2(100f, 100f), 1000f, 1000f, out var b));
        Assert.IsTrue(AITBannerGameArea.ApproximatelyEqual(a, b));
        // 화면 상단에서 300px 아래(= 위쪽 띠 30%)에서 시작
        Assert.AreEqual(0.3f, a.y, Tolerance);
    }

    [Test]
    public void TryFromScreenCorners_InvalidScreenSize_ReturnsFalse()
    {
        Assert.IsFalse(AITBannerGameArea.TryFromScreenCorners(Vector2.zero, Vector2.one, 0f, 0f, out _));
    }

    [Test]
    public void TryFromScreenCorners_WholeScreen_ReturnsFalse()
    {
        Assert.IsFalse(AITBannerGameArea.TryFromScreenCorners(
            Vector2.zero, new Vector2(1000f, 800f), 1000f, 800f, out _));
    }

    #endregion

    #region ApproximatelyEqual

    [Test]
    public void ApproximatelyEqual_WithinEpsilon_ReturnsTrue()
    {
        Assert.IsTrue(AITBannerGameArea.ApproximatelyEqual(
            new Rect(0.25f, 0f, 0.5f, 1f), new Rect(0.25f + 0.0001f, 0f, 0.5f, 1f)));
    }

    [Test]
    public void ApproximatelyEqual_BeyondEpsilon_ReturnsFalse()
    {
        Assert.IsFalse(AITBannerGameArea.ApproximatelyEqual(
            new Rect(0.25f, 0f, 0.5f, 1f), new Rect(0.26f, 0f, 0.5f, 1f)));
    }

    #endregion
}
