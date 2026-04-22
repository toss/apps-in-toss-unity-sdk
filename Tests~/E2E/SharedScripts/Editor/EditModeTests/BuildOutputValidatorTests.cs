// -----------------------------------------------------------------------
// BuildOutputValidatorTests.cs - EditMode Build 산출물 분류 테스트
// Level 0: DetectFileType 패턴 매칭 검증
// -----------------------------------------------------------------------

using NUnit.Framework;

[TestFixture]
public class BuildOutputValidatorTests
{
    // =====================================================
    // 기본 파일 타입 분류
    // =====================================================

    [TestCase("build.loader.js", "loader")]
    [TestCase("build.wasm", "wasm")]
    [TestCase("build.data", "data")]
    [TestCase("build.framework.js", "framework")]
    [TestCase("build.symbols.json", "symbols")]
    public void DetectFileType_UncompressedArtifacts_Classified(string fileName, string expectedType)
    {
        Assert.AreEqual(expectedType, BuildOutputValidator.DetectFileType(fileName));
    }

    // =====================================================
    // 압축 변형 (.br, .gz, .unityweb) — Contains 매칭으로 모두 허용 타입
    // =====================================================

    [TestCase("build.wasm.br", "wasm")]
    [TestCase("build.wasm.gz", "wasm")]
    [TestCase("build.wasm.unityweb", "wasm")]
    [TestCase("build.data.br", "data")]
    [TestCase("build.data.unityweb", "data")]
    [TestCase("build.framework.js.br", "framework")]
    [TestCase("build.framework.js.unityweb", "framework")]
    [TestCase("build.symbols.json.br", "symbols")]
    [TestCase("build.symbols.json.gz", "symbols")]
    [TestCase("build.symbols.json.unityweb", "symbols")]
    public void DetectFileType_CompressedArtifacts_Classified(string fileName, string expectedType)
    {
        Assert.AreEqual(expectedType, BuildOutputValidator.DetectFileType(fileName));
    }

    // =====================================================
    // 알 수 없는 파일은 "other"
    // =====================================================

    [TestCase("index.html")]
    [TestCase("README.txt")]
    [TestCase("random.file")]
    public void DetectFileType_UnknownFiles_ReturnsOther(string fileName)
    {
        Assert.AreEqual("other", BuildOutputValidator.DetectFileType(fileName));
    }
}
