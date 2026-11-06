// -----------------------------------------------------------------------
// AITDecompressionFallbackWarningTests.cs - decompressionFallback=1 빌드 경고(AITBuildValidator) 순수 로직 검증
// Level 0: 빌드 산출물 파일명 목록만으로 .unityweb(Decompression Fallback 형식) 여부를 판정한다.
// -----------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
public class AITDecompressionFallbackWarningTests
{
    [Test]
    public void NullOrEmpty_ReturnsNull()
    {
        Assert.IsNull(AITBuildValidator.BuildDecompressionFallbackWarning(null));
        Assert.IsNull(AITBuildValidator.BuildDecompressionFallbackWarning(new string[0]));
    }

    [Test]
    public void BrotliOrGzipOrPlainBuild_ReturnsNull()
    {
        var files = new[] { "/b/Build/x.wasm.br", "/b/Build/x.data.br", "/b/Build/x.framework.js.gz", "/b/Build/x.loader.js", "/b/index.html" };
        Assert.IsNull(AITBuildValidator.BuildDecompressionFallbackWarning(files));
    }

    [Test]
    public void UnityWebFiles_ProduceWarningListingBaseNamesOnly()
    {
        var files = new[] { "/b/Build/x.wasm.unityweb", "/b/Build/x.DATA.UNITYWEB", "/b/Build/x.loader.js" };
        string w = AITBuildValidator.BuildDecompressionFallbackWarning(files);
        Assert.IsNotNull(w);
        StringAssert.Contains("Decompression Fallback", w);
        StringAssert.Contains("x.wasm.unityweb", w);
        StringAssert.Contains("x.DATA.UNITYWEB", w);
        StringAssert.DoesNotContain("/b/Build", w, "절대 경로는 로그에 남기지 않는다");
        StringAssert.DoesNotContain("x.loader.js", w);
        StringAssert.Contains("decompressionFallback=0", w, "해결 방법이 메시지에 있어야 한다");
    }
}
