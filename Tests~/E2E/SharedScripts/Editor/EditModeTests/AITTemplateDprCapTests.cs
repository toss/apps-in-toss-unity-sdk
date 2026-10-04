// -----------------------------------------------------------------------
// AITTemplateDprCapTests.cs - index.html auto devicePixelRatio 상한(2) 정적 검증
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
    public void AutoDpr_IsCappedAtTwo_FixedValueBypassesCap()
    {
        string html = ReadTemplate();
        StringAssert.Contains("var AIT_AUTO_DPR_CAP = 2;", html);
        StringAssert.Contains("Math.min(computeAutoDevicePixelRatio(), AIT_AUTO_DPR_CAP)", html,
            "auto 모드는 기기 성능 계산 결과에 상한 2 를 적용해야 한다");
        int fixedIdx = html.IndexOf("if (aitDevicePixelRatioSetting > 0)", System.StringComparison.Ordinal);
        int capIdx = html.IndexOf("Math.min(computeAutoDevicePixelRatio()", System.StringComparison.Ordinal);
        Assert.Greater(fixedIdx, -1);
        Assert.Less(fixedIdx, capIdx, "고정값 분기는 상한 적용보다 먼저 반환해야 한다(명시 값 존중)");
    }
}
