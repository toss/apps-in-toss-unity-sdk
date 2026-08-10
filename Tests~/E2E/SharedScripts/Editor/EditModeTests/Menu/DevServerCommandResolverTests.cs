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

    private void WriteVitePackage(string binJson, string binFileToCreate = null)
    {
        string pkgDir = Path.Combine(tempDir, DevServerCommandResolver.VitePackagePath);
        Directory.CreateDirectory(pkgDir);
        File.WriteAllText(Path.Combine(pkgDir, "package.json"),
            "{\"name\":\"vite\"" + (binJson != null ? ",\"bin\":" + binJson : "") + "}");
        if (binFileToCreate != null)
        {
            string binPath = Path.Combine(pkgDir, binFileToCreate);
            Directory.CreateDirectory(Path.GetDirectoryName(binPath));
            File.WriteAllText(binPath, "// bin");
        }
    }

    // Resolve()의 node 직접 실행 분기(5b)를 결정론적으로 테스트하기 위한 가짜 node 실행 파일.
    // 실제 node 바이너리일 필요는 없음 — Resolve()는 File.Exists만 확인한다.
    private string CreateFakeNodeExecutable()
    {
        string nodePath = Path.Combine(tempDir, "fake-node");
        File.WriteAllText(nodePath, "#!/bin/sh\n");
        return nodePath;
    }

    [Test]
    public void Resolve_2x_WithGraniteBin_ReturnsNodeDirectInvocation()
    {
        WriteBuildPackageJson("2.10.7");
        WriteWebFrameworkPackage("{\"ait\":\"./ait.js\",\"granite\":\"./bin.js\"}", "bin.js");

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, out bool viteOnly, out string directExecutablePath);

        Assert.IsFalse(viteOnly);
        Assert.AreEqual("exec -- node node_modules/@apps-in-toss/web-framework/bin.js dev", cmd);
        Assert.IsNull(directExecutablePath, "granite 경로는 항상 pnpm exec 경유 (5b는 vite 전용)");
    }

    [Test]
    public void Resolve_3x_ReturnsViteCommand()
    {
        WriteBuildPackageJson("3.0.0-rc.0");
        // 3.x는 granite bin이 없으므로 web-framework 패키지 유무와 무관하게 vite 경로
        // node_modules/vite 자체가 없으므로 bin 직접 실행은 해석되지 않고 기존 pnpm exec 명령으로 폴백해야 함

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, out bool viteOnly, out string directExecutablePath);

        Assert.IsTrue(viteOnly);
        Assert.AreEqual("exec -- vite --host --port 5174", cmd);
        Assert.IsNull(directExecutablePath);
    }

    [Test]
    public void Resolve_3xBetaVersion_ReturnsViteCommand()
    {
        WriteBuildPackageJson("^3.0.0-beta.283cb36");

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5175, out bool viteOnly, out string directExecutablePath);

        Assert.IsTrue(viteOnly);
        Assert.AreEqual("exec -- vite --host --port 5175", cmd);
        Assert.IsNull(directExecutablePath);
    }

    [Test]
    public void Resolve_2x_WebFrameworkPackageMissing_FallsBackToLegacyCommand()
    {
        WriteBuildPackageJson("2.10.7");
        // node_modules/@apps-in-toss/web-framework 자체가 없음

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, out bool viteOnly, out string directExecutablePath);

        Assert.IsFalse(viteOnly);
        Assert.AreEqual(DevServerCommandResolver.LegacyGraniteCommand, cmd);
        Assert.IsNull(directExecutablePath);
    }

    [Test]
    public void Resolve_2x_NoGraniteBinDeclared_FallsBackToLegacyCommand()
    {
        WriteBuildPackageJson("2.10.7");
        WriteWebFrameworkPackage("{\"ait\":\"./bin/ait.js\"}");

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, out bool viteOnly, out string directExecutablePath);

        Assert.AreEqual(DevServerCommandResolver.LegacyGraniteCommand, cmd);
        Assert.IsNull(directExecutablePath);
    }

    [Test]
    public void Resolve_2x_BinFileMissingOnDisk_FallsBackToLegacyCommand()
    {
        WriteBuildPackageJson("2.10.7");
        WriteWebFrameworkPackage("{\"granite\":\"./bin.js\"}", binFileToCreate: null);

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, out bool viteOnly, out string directExecutablePath);

        Assert.AreEqual(DevServerCommandResolver.LegacyGraniteCommand, cmd);
        Assert.IsNull(directExecutablePath);
    }

    [Test]
    public void Resolve_NoBuildPackageJson_FallsBackToLegacyCommand()
    {
        // package.json 없음 → GetWebFrameworkMajor는 2 반환 → bin 해석도 실패 → 폴백

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, out bool viteOnly, out string directExecutablePath);

        Assert.IsFalse(viteOnly);
        Assert.IsNull(directExecutablePath);
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

    // ==================== ResolveViteBinRelPath (5b: vite bin 직접 실행) ====================

    [Test]
    public void ResolveViteBinRelPath_ObjectBinForm_ReturnsRelPath()
    {
        WriteVitePackage("{\"vite\":\"bin/vite.js\"}", "bin/vite.js");

        Assert.AreEqual("node_modules/vite/bin/vite.js", DevServerCommandResolver.ResolveViteBinRelPath(tempDir));
    }

    [Test]
    public void ResolveViteBinRelPath_StringBinForm_ReturnsRelPath()
    {
        // 일부 단일 커맨드 패키지는 "bin"을 객체가 아닌 문자열로 선언한다
        WriteVitePackage("\"bin/vite.js\"", "bin/vite.js");

        Assert.AreEqual("node_modules/vite/bin/vite.js", DevServerCommandResolver.ResolveViteBinRelPath(tempDir));
    }

    [Test]
    public void ResolveViteBinRelPath_SingleEntryDifferentKey_ReturnsRelPath()
    {
        // 스코프가 다른 배포본 등 "vite" 키가 아니어도 엔트리가 하나뿐이면 그 값을 사용
        WriteVitePackage("{\"vite-cli\":\"bin/vite.js\"}", "bin/vite.js");

        Assert.AreEqual("node_modules/vite/bin/vite.js", DevServerCommandResolver.ResolveViteBinRelPath(tempDir));
    }

    [Test]
    public void ResolveViteBinRelPath_MultiEntryWithoutViteKey_ReturnsNull()
    {
        // 엔트리가 여러 개인데 "vite" 키가 없으면 어느 것이 진입점인지 알 수 없어 폴백
        WriteVitePackage("{\"a\":\"a.js\",\"b\":\"b.js\"}", null);

        Assert.IsNull(DevServerCommandResolver.ResolveViteBinRelPath(tempDir));
    }

    [Test]
    public void ResolveViteBinRelPath_VitePackageMissing_ReturnsNull()
    {
        // node_modules/vite 자체가 없음
        Assert.IsNull(DevServerCommandResolver.ResolveViteBinRelPath(tempDir));
    }

    [Test]
    public void ResolveViteBinRelPath_BinFileMissingOnDisk_ReturnsNull()
    {
        WriteVitePackage("{\"vite\":\"bin/vite.js\"}", binFileToCreate: null);

        Assert.IsNull(DevServerCommandResolver.ResolveViteBinRelPath(tempDir));
    }

    [Test]
    public void ResolveViteBinRelPath_UnsafeBinPath_ReturnsNull()
    {
        // ResolveGraniteBinRelPath와 동일한 IsSafeBinRelPath allowlist를 공유하므로 대표 케이스만 커버
        WriteVitePackage("{\"vite\":\"bin with space.js\"}", null);

        Assert.IsNull(DevServerCommandResolver.ResolveViteBinRelPath(tempDir));
    }

    // ==================== Resolve() 노드 직접 실행 분기 (5b) ====================

    [Test]
    public void Resolve_3x_ViteBinAndNodeAvailable_ReturnsDirectNodeInvocation()
    {
        WriteBuildPackageJson("3.0.0-rc.0");
        WriteVitePackage("{\"vite\":\"bin/vite.js\"}", "bin/vite.js");
        string fakeNode = CreateFakeNodeExecutable();

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, fakeNode, out bool viteOnly, out string directExecutablePath);

        Assert.IsTrue(viteOnly);
        Assert.AreEqual("node_modules/vite/bin/vite.js --host --port 5174", cmd);
        Assert.AreEqual(fakeNode, directExecutablePath);
    }

    [Test]
    public void Resolve_3x_NodeUnavailable_FallsBackToPnpmExec()
    {
        WriteBuildPackageJson("3.0.0-rc.0");
        WriteVitePackage("{\"vite\":\"bin/vite.js\"}", "bin/vite.js");
        string missingNode = Path.Combine(tempDir, "no-such-node");

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, missingNode, out bool viteOnly, out string directExecutablePath);

        Assert.IsTrue(viteOnly);
        Assert.AreEqual("exec -- vite --host --port 5174", cmd);
        Assert.IsNull(directExecutablePath);
    }

    [Test]
    public void Resolve_3x_NodeAvailableButViteBinUnresolvable_FallsBackToPnpmExec()
    {
        WriteBuildPackageJson("3.0.0-rc.0");
        // node_modules/vite 자체가 없음 — node는 있지만 bin 해석이 실패하는 경우
        string fakeNode = CreateFakeNodeExecutable();

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, fakeNode, out bool viteOnly, out string directExecutablePath);

        Assert.IsTrue(viteOnly);
        Assert.AreEqual("exec -- vite --host --port 5174", cmd);
        Assert.IsNull(directExecutablePath);
    }
}
