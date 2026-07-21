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
    [TestCase("build.data.gz", "data")]
    [TestCase("build.data.unityweb", "data")]
    [TestCase("build.framework.js.br", "framework")]
    [TestCase("build.framework.js.gz", "framework")]
    [TestCase("build.framework.js.unityweb", "framework")]
    [TestCase("build.symbols.json.br", "symbols")]
    [TestCase("build.symbols.json.gz", "symbols")]
    [TestCase("build.symbols.json.unityweb", "symbols")]
    public void DetectFileType_CompressedArtifacts_Classified(string fileName, string expectedType)
    {
        Assert.AreEqual(expectedType, BuildOutputValidator.DetectFileType(fileName));
    }

    // =====================================================
    // loader는 EndsWith 매칭이므로 압축 변형은 "other"
    // 다른 타입은 Contains 매칭이라는 비대칭성을 고정
    // =====================================================

    [TestCase("build.loader.js.br")]
    [TestCase("build.loader.js.gz")]
    [TestCase("build.loader.js.unityweb")]
    public void DetectFileType_CompressedLoader_ReturnsOther(string fileName)
    {
        Assert.AreEqual("other", BuildOutputValidator.DetectFileType(fileName));
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

    // =====================================================
    // Contains 매칭 semantics 고정
    // .symbols.json 부분 문자열이 있으면 "symbols"로 분류됨 (Unity가 생성하지 않는 파일명)
    // =====================================================

    [Test]
    public void DetectFileType_SubstringMatch_DocumentsContainsSemantics()
    {
        Assert.AreEqual("symbols", BuildOutputValidator.DetectFileType("notes.symbols.json.backup"));
    }

    // =====================================================
    // APPS-IN-TOSS-UNITY-SDK-11Z 회귀 가드:
    // ValidateAll이 *.framework.js 누락을 에러로 보고해야 한다.
    // 기존 코드는 hasLoader/hasWasm/hasData만 검증하고 hasFramework를 누락했다.
    // 이 테스트는 framework.js가 없는 Build/ 폴더에서 ValidateAll이
    // "Missing .framework.js" 에러를 포함한 결과를 반환하는지 고정한다.
    // =====================================================

    [Test]
    public void ValidateAll_MissingFrameworkJs_ReturnsError()
    {
        // 임시 프로젝트 구조 생성
        string tempDir = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "ait-test-validator-" + System.Guid.NewGuid().ToString("N").Substring(0, 8));

        try
        {
            // ait-build/dist/web/Build/ 구조 생성 (framework.js 제외)
            string aitBuildPath = System.IO.Path.Combine(tempDir, "ait-build");
            string distWebPath = System.IO.Path.Combine(aitBuildPath, "dist", "web");
            string distBuildPath = System.IO.Path.Combine(distWebPath, "Build");
            System.IO.Directory.CreateDirectory(distBuildPath);

            // 필수 설정 파일
            System.IO.File.WriteAllText(System.IO.Path.Combine(aitBuildPath, "package.json"), "{}");
            // node_modules/ 디렉토리를 먼저 만든 뒤 .keep을 써야 한다 —
            // File.WriteAllText는 상위 디렉토리를 만들지 않으므로 순서가 뒤바뀌면
            // DirectoryNotFoundException으로 테스트가 셋업 단계에서 throw된다.
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(aitBuildPath, "node_modules"));
            System.IO.File.WriteAllText(System.IO.Path.Combine(aitBuildPath, "node_modules", ".keep"), "");
            System.IO.File.WriteAllText(System.IO.Path.Combine(distWebPath, "index.html"), "<html></html>");

            // loader, data, wasm은 있지만 framework.js는 의도적으로 생성 안 함
            System.IO.File.WriteAllText(System.IO.Path.Combine(distBuildPath, "build.loader.js"), "loader");
            System.IO.File.WriteAllText(System.IO.Path.Combine(distBuildPath, "build.data"), "data");
            System.IO.File.WriteAllText(System.IO.Path.Combine(distBuildPath, "build.wasm"), "wasm");
            // build.framework.js 의도적으로 누락

            var result = BuildOutputValidator.ValidateAll(tempDir);

            Assert.IsFalse(result.passed,
                "framework.js가 없으면 ValidateAll은 실패해야 한다");

            bool hasFrameworkError = System.Array.Exists(
                result.errors,
                e => e.Contains("framework.js"));
            Assert.IsTrue(hasFrameworkError,
                "ValidateAll 에러 목록에 'framework.js' 누락 에러가 포함되어야 한다. " +
                "실제 에러: " + string.Join(", ", result.errors));
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, true);
        }
    }
}
