// -----------------------------------------------------------------------
// DevServerCommandResolverTests.cs
// Level 0: DevServerCommandResolver — dev 서버 커맨드 해석 검증
// (2.x: web-framework granite bin 직접 실행 / 3.x: vite / 해석 실패: 기존 명령 폴백)
// -----------------------------------------------------------------------

using System;
using System.IO;
using NUnit.Framework;
using AppsInToss.Editor.Menu;

[TestFixture]
public class DevServerCommandResolverTests
{
    private string tempDir;

    [SetUp]
    public void SetUp()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "ait-test-devcmd-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private void WriteBuildPackageJson(string webFrameworkVersion)
    {
        File.WriteAllText(Path.Combine(tempDir, "package.json"),
            "{\"dependencies\":{\"@apps-in-toss/web-framework\":\"" + webFrameworkVersion + "\"}}");
    }

    private void WriteWebFrameworkPackage(string binJson, string binFileToCreate = null)
    {
        string pkgDir = Path.Combine(tempDir, DevServerCommandResolver.WebFrameworkPackagePath);
        Directory.CreateDirectory(pkgDir);
        File.WriteAllText(Path.Combine(pkgDir, "package.json"),
            "{\"name\":\"@apps-in-toss/web-framework\"" + (binJson != null ? ",\"bin\":" + binJson : "") + "}");
        if (binFileToCreate != null)
        {
            string binPath = Path.Combine(pkgDir, binFileToCreate);
            Directory.CreateDirectory(Path.GetDirectoryName(binPath));
            File.WriteAllText(binPath, "// bin");
        }
    }

    [Test]
    public void Resolve_2x_WithGraniteBin_ReturnsNodeDirectInvocation()
    {
        WriteBuildPackageJson("2.10.7");
        WriteWebFrameworkPackage("{\"ait\":\"./ait.js\",\"granite\":\"./bin.js\"}", "bin.js");

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, out bool viteOnly);

        Assert.IsFalse(viteOnly);
        Assert.AreEqual("exec -- node node_modules/@apps-in-toss/web-framework/bin.js dev", cmd);
    }

    [Test]
    public void Resolve_3x_ReturnsViteCommand()
    {
        WriteBuildPackageJson("3.0.0-rc.0");
        // 3.x는 granite bin이 없으므로 web-framework 패키지 유무와 무관하게 vite 경로

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, out bool viteOnly);

        Assert.IsTrue(viteOnly);
        Assert.AreEqual("exec -- vite --host --port 5174", cmd);
    }

    [Test]
    public void Resolve_3xBetaVersion_ReturnsViteCommand()
    {
        WriteBuildPackageJson("^3.0.0-beta.283cb36");

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5175, out bool viteOnly);

        Assert.IsTrue(viteOnly);
        Assert.AreEqual("exec -- vite --host --port 5175", cmd);
    }

    [Test]
    public void Resolve_2x_WebFrameworkPackageMissing_FallsBackToLegacyCommand()
    {
        WriteBuildPackageJson("2.10.7");
        // node_modules/@apps-in-toss/web-framework 자체가 없음

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, out bool viteOnly);

        Assert.IsFalse(viteOnly);
        Assert.AreEqual(DevServerCommandResolver.LegacyGraniteCommand, cmd);
    }

    [Test]
    public void Resolve_2x_NoGraniteBinDeclared_FallsBackToLegacyCommand()
    {
        WriteBuildPackageJson("2.10.7");
        WriteWebFrameworkPackage("{\"ait\":\"./bin/ait.js\"}");

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, out bool viteOnly);

        Assert.AreEqual(DevServerCommandResolver.LegacyGraniteCommand, cmd);
    }

    [Test]
    public void Resolve_2x_BinFileMissingOnDisk_FallsBackToLegacyCommand()
    {
        WriteBuildPackageJson("2.10.7");
        WriteWebFrameworkPackage("{\"granite\":\"./bin.js\"}", binFileToCreate: null);

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, out bool viteOnly);

        Assert.AreEqual(DevServerCommandResolver.LegacyGraniteCommand, cmd);
    }

    [Test]
    public void Resolve_NoBuildPackageJson_FallsBackToLegacyCommand()
    {
        // package.json 없음 → GetWebFrameworkMajor는 2 반환 → bin 해석도 실패 → 폴백

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, out bool viteOnly);

        Assert.IsFalse(viteOnly);
        Assert.AreEqual(DevServerCommandResolver.LegacyGraniteCommand, cmd);
    }

    [Test]
    public void ResolveGraniteBinRelPath_NestedBinPath_ReturnsForwardSlashRelPath()
    {
        WriteBuildPackageJson("2.10.7");
        WriteWebFrameworkPackage("{\"granite\":\"./dist/cli/bin.js\"}", "dist/cli/bin.js");

        string rel = DevServerCommandResolver.ResolveGraniteBinRelPath(tempDir);

        Assert.AreEqual("node_modules/@apps-in-toss/web-framework/dist/cli/bin.js", rel);
    }

    // allowlist(영숫자·._-/) 밖의 문자·경로 탈출·절대 경로는 모두 거부되어야 한다.
    // 셸 무인용 삽입 컨텍스트이므로 bash(공백 " ' $ ` ; & | < > 개행)와
    // cmd.exe(% ^ & | < >) 메타문자를 실사용 인젝션 형태로 직접 커버한다.
    [TestCase("./bin with space.js")]
    [TestCase("../escape.js")]
    [TestCase("bin\\win.js")]
    [TestCase("bin\"quote.js")]
    [TestCase("bin'quote.js")]
    [TestCase("bin$var.js")]
    [TestCase("bin`tick.js")]
    [TestCase("bin.js;rm -rf x")]
    [TestCase("bin.js&&curl x")]
    [TestCase("bin.js|sh")]
    [TestCase("bin.js<input")]
    [TestCase("bin.js>output")]
    [TestCase("bin%PATH%.js")]
    [TestCase("bin^caret.js")]
    [TestCase("bin\ttab.js")]
    [TestCase("bin\nnewline.js")]
    [TestCase("/abs/path/bin.js")]
    public void ResolveGraniteBinRelPath_UnsafeBinPath_ReturnsNull(string unsafeBin)
    {
        WriteBuildPackageJson("2.10.7");
        // JSON 문자열로 안전하게 직렬화 (역슬래시·따옴표·제어문자 이스케이프)
        string escaped = unsafeBin.Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("\t", "\\t").Replace("\n", "\\n");
        WriteWebFrameworkPackage("{\"granite\":\"" + escaped + "\"}");

        Assert.IsNull(DevServerCommandResolver.ResolveGraniteBinRelPath(tempDir));
    }

    [Test]
    public void ResolveGraniteBinRelPath_AllowlistChars_DigitsDashUnderscore_Resolves()
    {
        WriteBuildPackageJson("2.10.7");
        WriteWebFrameworkPackage("{\"granite\":\"./dist/bin-2.x_v1.js\"}", "dist/bin-2.x_v1.js");

        Assert.AreEqual(
            "node_modules/@apps-in-toss/web-framework/dist/bin-2.x_v1.js",
            DevServerCommandResolver.ResolveGraniteBinRelPath(tempDir));
    }

    [Test]
    public void IsViteOnly_3x_ReturnsTrue_2x_ReturnsFalse()
    {
        WriteBuildPackageJson("3.0.0-rc.0");
        Assert.IsTrue(DevServerCommandResolver.IsViteOnly(tempDir),
            "3.x는 granite 포트를 사용하지 않으므로 vite 단독 모드여야 함 (granite 포트 스캔 생략 근거)");

        WriteBuildPackageJson("2.10.7");
        Assert.IsFalse(DevServerCommandResolver.IsViteOnly(tempDir));
    }

    [Test]
    public void ResolveGraniteBinRelPath_MalformedPackageJson_ReturnsNull()
    {
        WriteBuildPackageJson("2.10.7");
        string pkgDir = Path.Combine(tempDir, DevServerCommandResolver.WebFrameworkPackagePath);
        Directory.CreateDirectory(pkgDir);
        File.WriteAllText(Path.Combine(pkgDir, "package.json"), "{ not valid json");

        Assert.IsNull(DevServerCommandResolver.ResolveGraniteBinRelPath(tempDir));
    }
}
