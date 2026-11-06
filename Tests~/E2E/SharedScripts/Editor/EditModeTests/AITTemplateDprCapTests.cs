// -----------------------------------------------------------------------
// AITTemplateDprCapTests.cs - index.html auto devicePixelRatio 정책(상한 3, RAM 부족 기기 2) 정적 검증
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITTemplateDprCapTests
{
    private static string ReadTemplate()
    {
        Assert.IsTrue(
            AITPackagePathResolver.TryResolveFile(
                "WebGLTemplates/AITTemplate/index.html",
                out string path,
                typeof(AITConvertCore)),
            "WebGLTemplates/AITTemplate/index.html 경로를 찾지 못했습니다.");
        return File.ReadAllText(path);
    }

    [Test]
    public void AutoDpr_IsCappedAtThree_FixedValueBypassesCap()
    {
        string html = ReadTemplate();
        StringAssert.Contains("var AIT_AUTO_DPR_CAP = 3;", html);
        StringAssert.Contains("Math.min(computeAutoDevicePixelRatio(), AIT_AUTO_DPR_CAP)", html,
            "auto 모드는 기기 성능 계산 결과에 상한 3 을 적용해야 한다");
        int fixedIdx = html.IndexOf("if (aitDevicePixelRatioSetting > 0)", System.StringComparison.Ordinal);
        int capIdx = html.IndexOf("Math.min(computeAutoDevicePixelRatio()", System.StringComparison.Ordinal);
        Assert.Greater(fixedIdx, -1);
        Assert.Less(fixedIdx, capIdx, "고정값 분기는 상한 적용보다 먼저 반환해야 한다(명시 값 존중)");
    }

    [Test]
    public void AutoDpr_LowRamDevice_GetsDprTwo_OthersKeepNative()
    {
        string html = ReadTemplate();
        int begin = html.IndexOf("/* AIT-AUTO-DPR:BEGIN */", System.StringComparison.Ordinal);
        int end = html.IndexOf("/* AIT-AUTO-DPR:END */", System.StringComparison.Ordinal);
        Assert.Greater(begin, -1);
        Assert.Greater(end, begin);
        string block = html.Substring(begin, end - begin);
        StringAssert.Contains("window.AITMemory.lowRamDevice", block, "RAM 부족 판별은 ait-mem.js 한 곳에서 한다");
        StringAssert.Contains("Math.min(baseRatio, 2)", block, "low-RAM 기기는 DPR 2 로 제한해야 한다");
        StringAssert.DoesNotContain("deviceMemory", block, "deviceMemory 직접 분기는 ait-mem.js 의 판별로 일원화됐다");
        StringAssert.DoesNotContain("screenPixels", block, "iOS 화면 픽셀 휴리스틱은 제거됐다");
        Assert.AreEqual(1, CountOccurrences(block, "Math.min(baseRatio, 2)"), "고사양 기기를 2 로 묶는 폴백이 남아 있으면 안 된다");
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0;
        int idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }
}
