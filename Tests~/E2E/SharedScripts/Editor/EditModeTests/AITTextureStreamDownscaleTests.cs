// -----------------------------------------------------------------------
// AITTextureStreamDownscaleTests.cs - 스트림 사본 다운스케일 순수 로직 검증
// Level 0: 균일 배율 차원 산출(ComputeDownscaledDims) / 영역 평균 리샘플(BoxDownsamplePremultiplied)
//
// 핵심 불변식:
//   1) 균일 배율 — 두 축에 동일 factor → 종횡비 & 스프라이트시트 서브-rect UV 비율 보존.
//   2) 캡 이하/무축소는 no-op(작은 텍스처 불변).
//   3) 알파 프리멀티플라이 평균 — 완전 투명 texel 이 경계 색을 오염시키지 않음(dark-fringe 방지).
// -----------------------------------------------------------------------

using NUnit.Framework;
using UnityEngine;
using AppsInToss.Editor;

[TestFixture]
public class AITTextureStreamDownscaleTests
{
    // ─────────────────────── ComputeDownscaledDims ───────────────────────

    [Test]
    public void ComputeDownscaledDims_MaxDimAtOrBelowCap_NoDownscale()
    {
        Assert.IsFalse(AITLargeTextureExternalizer.ComputeDownscaledDims(2048, 1024, 2048, out _, out _),
            "최대변이 캡과 같으면 축소하지 않아야 한다.");
        Assert.IsFalse(AITLargeTextureExternalizer.ComputeDownscaledDims(1000, 500, 2048, out _, out _),
            "캡보다 작으면 축소하지 않아야 한다.");
    }

    [Test]
    public void ComputeDownscaledDims_Square_HalvesCleanly()
    {
        Assert.IsTrue(AITLargeTextureExternalizer.ComputeDownscaledDims(4096, 4096, 2048, out int nw, out int nh));
        Assert.AreEqual(2048, nw);
        Assert.AreEqual(2048, nh);
    }

    [Test]
    public void ComputeDownscaledDims_Landscape_CapsLargerDim_PreservesAspect()
    {
        // 4096x2048, 캡 2048 → factor 0.5 → 2048x1024 (두 축 동일 factor).
        Assert.IsTrue(AITLargeTextureExternalizer.ComputeDownscaledDims(4096, 2048, 2048, out int nw, out int nh));
        Assert.AreEqual(2048, nw);
        Assert.AreEqual(1024, nh);
    }

    [Test]
    public void ComputeDownscaledDims_Portrait_HeightIsMaxDim()
    {
        // 2000x3000, 캡 2048 → factor 2048/3000 → 1365x2048.
        Assert.IsTrue(AITLargeTextureExternalizer.ComputeDownscaledDims(2000, 3000, 2048, out int nw, out int nh));
        Assert.AreEqual(2048, nh, "최대변(height)이 캡에 맞아야 한다.");
        Assert.AreEqual(1365, nw); // round(2000 * 2048/3000) = round(1365.33) = 1365
    }

    [Test]
    public void ComputeDownscaledDims_NonSquare_UniformFactor_BothAxesSameScale()
    {
        // 균일 배율 불변식: nw/w ≈ nh/h (반올림 오차 이내). 이게 스프라이트시트 UV 정합의 근거.
        int w = 3000, h = 2000, cap = 2048;
        Assert.IsTrue(AITLargeTextureExternalizer.ComputeDownscaledDims(w, h, cap, out int nw, out int nh));
        double fx = (double)nw / w;
        double fy = (double)nh / h;
        Assert.That(System.Math.Abs(fx - fy), Is.LessThan(0.001),
            $"두 축 배율이 동일해야 한다(균일 배율). fx={fx}, fy={fy}");
        // 최대변이 캡에 클램프됐는지.
        Assert.AreEqual(cap, System.Math.Max(nw, nh));
    }

    [Test]
    public void ComputeDownscaledDims_CapBelowMinimum_NoDownscale()
    {
        // MinDownscaleSize(16) 미만 캡은 사고 방지로 무시.
        Assert.IsFalse(AITLargeTextureExternalizer.ComputeDownscaledDims(4096, 4096, 8, out _, out _));
    }

    [Test]
    public void ComputeDownscaledDims_ResultAlwaysStrictlySmaller()
    {
        // 축소가 성립하면 두 축 모두 원본 미만이어야 한다.
        Assert.IsTrue(AITLargeTextureExternalizer.ComputeDownscaledDims(2049, 2049, 2048, out int nw, out int nh));
        Assert.Less(nw, 2049);
        Assert.Less(nh, 2049);
        Assert.GreaterOrEqual(nw, 1);
        Assert.GreaterOrEqual(nh, 1);
    }

    [Test]
    public void ComputeDownscaledDims_InvalidDims_NoDownscale()
    {
        Assert.IsFalse(AITLargeTextureExternalizer.ComputeDownscaledDims(0, 100, 2048, out _, out _));
        Assert.IsFalse(AITLargeTextureExternalizer.ComputeDownscaledDims(100, 0, 2048, out _, out _));
    }

    // ─────────────────────── BoxDownsamplePremultiplied ───────────────────────

    [Test]
    public void BoxDownsample_2x2Opaque_To1x1_AveragesColors()
    {
        var src = new Color32[]
        {
            new Color32(0, 0, 0, 255),
            new Color32(255, 0, 0, 255),
            new Color32(0, 255, 0, 255),
            new Color32(255, 255, 255, 255),
        };
        var dst = AITLargeTextureExternalizer.BoxDownsamplePremultiplied(src, 2, 2, 1, 1);
        Assert.AreEqual(1, dst.Length);
        // 알파 전부 255 → 프리멀티플라이가 단순 평균과 동일.
        Assert.AreEqual((0 + 255 + 0 + 255) / 4, dst[0].r); // 127
        Assert.AreEqual((0 + 0 + 255 + 255) / 4, dst[0].g); // 127
        Assert.AreEqual((0 + 0 + 0 + 255) / 4, dst[0].b);   // 63
        Assert.AreEqual(255, dst[0].a);
    }

    [Test]
    public void BoxDownsample_AlphaWeighted_TransparentTexelDoesNotPolluteColor()
    {
        // 불투명 빨강 + 완전 투명(파랑) → 프리멀티플라이 평균이면 색은 빨강만 반영(dark/파랑 fringe 없음),
        // 알파는 단순 평균(127).
        var src = new Color32[]
        {
            new Color32(255, 0, 0, 255), // 불투명 빨강
            new Color32(0, 0, 255, 0),   // 완전 투명 파랑
        };
        var dst = AITLargeTextureExternalizer.BoxDownsamplePremultiplied(src, 2, 1, 1, 1);
        Assert.AreEqual(255, dst[0].r, "불투명 빨강만 색에 기여해야 한다.");
        Assert.AreEqual(0, dst[0].g);
        Assert.AreEqual(0, dst[0].b, "완전 투명 파랑은 색을 오염시키면 안 된다(dark-fringe 방지).");
        Assert.AreEqual(127, dst[0].a, "알파는 단순 평균 (255+0)/2 = 127.");
    }

    [Test]
    public void BoxDownsample_FullyTransparent_YieldsZeroColorZeroAlpha()
    {
        var src = new Color32[]
        {
            new Color32(10, 20, 30, 0),
            new Color32(40, 50, 60, 0),
        };
        var dst = AITLargeTextureExternalizer.BoxDownsamplePremultiplied(src, 2, 1, 1, 1);
        Assert.AreEqual(0, dst[0].a);
        Assert.AreEqual(0, dst[0].r, "완전 투명 → 색 무의미(0).");
        Assert.AreEqual(0, dst[0].g);
        Assert.AreEqual(0, dst[0].b);
    }

    [Test]
    public void BoxDownsample_PreservesDimensions_ProducesExpectedLength()
    {
        // 4x4 → 2x2: 각 dst 픽셀이 2x2 소스 블록 평균. 길이/기본 정합만 확인.
        var src = new Color32[16];
        for (int i = 0; i < 16; i++)
        {
            src[i] = new Color32(100, 100, 100, 255);
        }

        var dst = AITLargeTextureExternalizer.BoxDownsamplePremultiplied(src, 4, 4, 2, 2);
        Assert.AreEqual(4, dst.Length);
        foreach (var c in dst)
        {
            Assert.AreEqual(100, c.r);
            Assert.AreEqual(255, c.a);
        }
    }
}
