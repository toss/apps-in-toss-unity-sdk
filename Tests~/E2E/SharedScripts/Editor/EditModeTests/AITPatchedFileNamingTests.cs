// -----------------------------------------------------------------------
// AITPatchedFileNamingTests.cs - 패치 산출물 파일명 규약(".aitpN") 순수 로직 검증
//   · GetPatchedName: 역할 확장자 앞에 접미사 삽입, 멱등, 버전 교체, 디렉터리 접두 보존
//   · Unity 파일 글롭 패턴(*.framework.js.br 등)이 패치 이름에도 그대로 맞는지
//   · RewriteReferences: 이름 경계 준수, 긴 이름 우선, 멱등
//   · RenameInDirectory: 실제 파일 이동과 재호출 안전성
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using AppsInToss.Editor;

[TestFixture]
[Category("Unit")]
public class AITPatchedFileNamingTests
{
    private string _tempDir;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ait-patchname-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (_tempDir != null && Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [TestCase("abc123.framework.js.br", "abc123.aitp1.framework.js.br")]
    [TestCase("abc123.framework.js", "abc123.aitp1.framework.js")]
    [TestCase("abc123.loader.js", "abc123.aitp1.loader.js")]
    [TestCase("abc123.data.br", "abc123.aitp1.data.br")]
    [TestCase("abc123.data.gz", "abc123.aitp1.data.gz")]
    [TestCase("abc123.wasm.br", "abc123.aitp1.wasm.br")]
    [TestCase("abc123.symbols.json.br", "abc123.aitp1.symbols.json.br")]
    [TestCase("abc123.framework.js.unityweb", "abc123.aitp1.framework.js.unityweb")]
    // 스템에 점이 있어도 역할 확장자 앞에 끼운다
    [TestCase("My.Game.framework.js", "My.Game.aitp1.framework.js")]
    // 스템 안의 ".data" 가 역할 확장자보다 앞서 있어도 가장 뒤의 역할 확장자를 쓴다
    [TestCase("abc.data.framework.js.br", "abc.data.aitp1.framework.js.br")]
    // ".data" 로 시작하지만 다른 단어(".database")는 역할 확장자가 아니다
    [TestCase("x.database.wasm", "x.database.aitp1.wasm")]
    // 디렉터리/URL 접두 보존
    [TestCase("Build/abc123.framework.js.br", "Build/abc123.aitp1.framework.js.br")]
    [TestCase("Build\\abc123.data.br", "Build\\abc123.aitp1.data.br")]
    // 알 수 없는 이름: 첫 점 앞, 점이 없으면 끝
    [TestCase("foo.bar.txt", "foo.aitp1.bar.txt")]
    [TestCase("noext", "noext.aitp1")]
    public void GetPatchedName_InsertsSuffixBeforeRoleExtension(string input, string expected)
    {
        Assert.AreEqual(expected, AITPatchedFileNaming.GetPatchedName(input, 1));
    }

    [Test]
    public void GetPatchedName_DefaultVersion_IsPatchSetVersion()
    {
        Assert.AreEqual(
            "h.aitp" + AITPatchedFileNaming.PatchSetVersion + ".data.br",
            AITPatchedFileNaming.GetPatchedName("h.data.br"));
        Assert.Greater(AITPatchedFileNaming.PatchSetVersion, 0);
    }

    [Test]
    public void GetPatchedName_IsIdempotent_AndReplacesOlderVersion()
    {
        string once = AITPatchedFileNaming.GetPatchedName("h.framework.js.br", 2);
        Assert.AreEqual("h.aitp2.framework.js.br", once);
        Assert.AreEqual(once, AITPatchedFileNaming.GetPatchedName(once, 2), "같은 버전으로 다시 적용해도 같아야 한다");
        Assert.AreEqual("h.aitp3.framework.js.br", AITPatchedFileNaming.GetPatchedName(once, 3), "다른 버전이면 접미사를 교체한다");
    }

    [TestCase("abc.framework.js.br")]
    [TestCase("abc.loader.js")]
    [TestCase("abc.data.br")]
    [TestCase("abc.wasm.br")]
    [TestCase("abc.symbols.json.br")]
    public void GetPatchedName_StillMatchesUnityBuildGlobPatterns(string original)
    {
        // AITBuildValidator.GetFilePatterns 의 패턴은 "*.<역할><압축확장자>" 라서, 접미사가 역할 확장자 '앞'에 있으면 그대로 맞는다.
        string patched = AITPatchedFileNaming.GetPatchedName(original, 1);
        string tail = original.Substring(original.IndexOf('.'));
        StringAssert.EndsWith(tail, patched, "역할·압축 확장자 꼬리는 그대로여야 한다(글롭 패턴 호환)");
        StringAssert.Contains(".aitp1.", patched);
    }

    [Test]
    public void GetPatchedName_Throws_OnInvalidArguments()
    {
        Assert.Throws<ArgumentException>(() => AITPatchedFileNaming.GetPatchedName(""));
        Assert.Throws<ArgumentException>(() => AITPatchedFileNaming.GetPatchedName(null));
        Assert.Throws<ArgumentOutOfRangeException>(() => AITPatchedFileNaming.GetPatchedName("a.data", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => AITPatchedFileNaming.GetPatchedName("a.data", -1));
    }

    [Test]
    public void GetOriginalName_RoundTrips()
    {
        foreach (string original in new[] { "h.framework.js.br", "h.data.br", "Build/h.loader.js", "noext" })
        {
            string patched = AITPatchedFileNaming.GetPatchedName(original, 4);
            Assert.AreEqual(original, AITPatchedFileNaming.GetOriginalName(patched), original);
        }
        Assert.AreEqual("h.data.br", AITPatchedFileNaming.GetOriginalName("h.data.br"), "접미사 없는 이름은 그대로");
    }

    [Test]
    public void TryParsePatchVersion_ParsesVersion_OrFails()
    {
        Assert.IsTrue(AITPatchedFileNaming.TryParsePatchVersion("h.aitp12.framework.js.br", out int v));
        Assert.AreEqual(12, v);
        Assert.IsTrue(AITPatchedFileNaming.TryParsePatchVersion("Build/h.aitp1.data", out v));
        Assert.AreEqual(1, v);

        Assert.IsFalse(AITPatchedFileNaming.TryParsePatchVersion("h.framework.js.br", out _));
        Assert.IsFalse(AITPatchedFileNaming.TryParsePatchVersion("h.aitp.framework.js", out _), "숫자 없는 접미사는 아니다");
        Assert.IsFalse(AITPatchedFileNaming.TryParsePatchVersion("h.aitpx1.data", out _));
        Assert.IsFalse(AITPatchedFileNaming.TryParsePatchVersion("h.aitp0.data", out _), "버전 0 은 무효");
        Assert.IsFalse(AITPatchedFileNaming.TryParsePatchVersion("", out _));
        Assert.IsFalse(AITPatchedFileNaming.TryParsePatchVersion(null, out _));

        Assert.IsTrue(AITPatchedFileNaming.IsPatched("h.aitp1.loader.js"));
        Assert.IsFalse(AITPatchedFileNaming.IsPatched("h.loader.js"));
    }

    [Test]
    public void RewriteReferences_ReplacesWithinHtmlAndConfig()
    {
        var renames = new Dictionary<string, string>
        {
            { "h1.framework.js.br", "h1.aitp1.framework.js.br" },
            { "h2.loader.js", "h2.aitp1.loader.js" },
        };
        string html =
            "<link rel=\"preload\" href=\"Build/h1.framework.js.br\">\n" +
            "<script src=\"Build/h2.loader.js\"></script>\n" +
            "var config = { frameworkUrl: \"Build/h1.framework.js.br\", dataUrl: \"Build/h3.data.br\" };\n";

        string rewritten = AITPatchedFileNaming.RewriteReferences(html, renames);

        StringAssert.Contains("href=\"Build/h1.aitp1.framework.js.br\"", rewritten);
        StringAssert.Contains("src=\"Build/h2.aitp1.loader.js\"", rewritten);
        StringAssert.Contains("frameworkUrl: \"Build/h1.aitp1.framework.js.br\"", rewritten);
        StringAssert.Contains("dataUrl: \"Build/h3.data.br\"", rewritten, "rename 대상이 아닌 파일은 그대로");
        Assert.AreEqual(rewritten, AITPatchedFileNaming.RewriteReferences(rewritten, renames), "이미 바뀐 텍스트에 다시 적용해도 변하지 않는다(멱등)");
    }

    [Test]
    public void RewriteReferences_RespectsNameBoundaries()
    {
        var renames = new Dictionary<string, string> { { "a.data.br", "a.aitp1.data.br" } };

        // 다른 이름의 일부(앞에 파일명 문자가 더 있음)는 건드리지 않는다
        Assert.AreEqual("Build/xa.data.br", AITPatchedFileNaming.RewriteReferences("Build/xa.data.br", renames));
        // 더 긴 이름의 접두도 건드리지 않는다
        Assert.AreEqual("Build/a.data.br.map", AITPatchedFileNaming.RewriteReferences("Build/a.data.br.map", renames));
        // 따옴표/슬래시/공백 경계는 치환
        Assert.AreEqual("\"a.aitp1.data.br\"", AITPatchedFileNaming.RewriteReferences("\"a.data.br\"", renames));
        Assert.AreEqual("x a.aitp1.data.br y", AITPatchedFileNaming.RewriteReferences("x a.data.br y", renames));
    }

    [Test]
    public void RewriteReferences_LongerNameWinsWhenOneIsPrefixOfAnother()
    {
        var renames = new Dictionary<string, string>
        {
            { "a.data", "a.aitp1.data" },
            { "a.data.br", "a.aitp1.data.br" },
        };
        string rewritten = AITPatchedFileNaming.RewriteReferences("[a.data.br] [a.data]", renames);
        Assert.AreEqual("[a.aitp1.data.br] [a.aitp1.data]", rewritten);
    }

    [Test]
    public void RewriteReferences_NullOrEmptyInputs_AreNoOps()
    {
        var renames = new Dictionary<string, string> { { "a.data", "a.aitp1.data" } };
        Assert.IsNull(AITPatchedFileNaming.RewriteReferences(null, renames));
        Assert.AreEqual("", AITPatchedFileNaming.RewriteReferences("", renames));
        Assert.AreEqual("a.data", AITPatchedFileNaming.RewriteReferences("a.data", null));
        Assert.AreEqual("a.data", AITPatchedFileNaming.RewriteReferences("a.data", new Dictionary<string, string>()));
    }

    [Test]
    public void RewriteReferencesInFile_WritesOnlyWhenChanged()
    {
        string path = Path.Combine(_tempDir, "index.html");
        File.WriteAllText(path, "<script src=\"Build/h.loader.js\"></script>");
        var renames = new Dictionary<string, string> { { "h.loader.js", "h.aitp1.loader.js" } };

        Assert.IsTrue(AITPatchedFileNaming.RewriteReferencesInFile(path, renames));
        Assert.AreEqual("<script src=\"Build/h.aitp1.loader.js\"></script>", File.ReadAllText(path));
        Assert.IsFalse(AITPatchedFileNaming.RewriteReferencesInFile(path, renames), "두 번째는 변경 없음");
        Assert.IsFalse(AITPatchedFileNaming.RewriteReferencesInFile(Path.Combine(_tempDir, "missing.html"), renames));
    }

    [Test]
    public void RenameInDirectory_MovesFile_AndIsSafeToRepeat()
    {
        string src = Path.Combine(_tempDir, "h.framework.js.br");
        File.WriteAllText(src, "payload");

        Assert.IsTrue(AITPatchedFileNaming.RenameInDirectory(_tempDir, "h.framework.js.br", out string newName, 1));
        Assert.AreEqual("h.aitp1.framework.js.br", newName);
        Assert.IsFalse(File.Exists(src), "원본 이름은 사라져야 한다");
        Assert.AreEqual("payload", File.ReadAllText(Path.Combine(_tempDir, newName)));

        // 원본이 이미 없으니 재호출은 false, newName 은 입력 그대로
        Assert.IsFalse(AITPatchedFileNaming.RenameInDirectory(_tempDir, "h.framework.js.br", out string again, 1));
        Assert.AreEqual("h.framework.js.br", again);

        // 이미 패치 이름이면 변화 없음
        Assert.IsFalse(AITPatchedFileNaming.RenameInDirectory(_tempDir, newName, out string same, 1));
        Assert.AreEqual(newName, same);
        Assert.IsTrue(File.Exists(Path.Combine(_tempDir, newName)));
    }

    [Test]
    public void RenameInDirectory_OverwritesStaleTarget()
    {
        File.WriteAllText(Path.Combine(_tempDir, "h.data.br"), "new");
        File.WriteAllText(Path.Combine(_tempDir, "h.aitp1.data.br"), "stale");

        Assert.IsTrue(AITPatchedFileNaming.RenameInDirectory(_tempDir, "h.data.br", out string newName, 1));
        Assert.AreEqual("new", File.ReadAllText(Path.Combine(_tempDir, newName)));
    }
}
