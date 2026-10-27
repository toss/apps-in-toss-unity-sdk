// -----------------------------------------------------------------------
// DevServerCommandResolverTests.cs
// Level 0: DevServerCommandResolver — dev 서버 커맨드 해석 검증
// (vite bin 직접 실행 / 해석 실패: 기존 pnpm exec 명령 폴백)
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
    public void Resolve_3x_ReturnsViteCommand()
    {
        WriteBuildPackageJson("3.0.0-rc.0");
        // node_modules/vite 자체가 없으므로 bin 직접 실행은 해석되지 않고 기존 pnpm exec 명령으로 폴백해야 함

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, out string directExecutablePath);

        Assert.AreEqual("exec -- vite --host --port 5174", cmd);
        Assert.IsNull(directExecutablePath);
    }

    [Test]
    public void Resolve_3xBetaVersion_ReturnsViteCommand()
    {
        WriteBuildPackageJson("^3.0.0-beta.283cb36");

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5175, out string directExecutablePath);

        Assert.AreEqual("exec -- vite --host --port 5175", cmd);
        Assert.IsNull(directExecutablePath);
    }

    [Test]
    public void Resolve_NoBuildPackageJson_FallsBackToPnpmExec()
    {
        // node_modules/vite 없음 → bin 해석 실패 → pnpm exec 폴백
        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, out string directExecutablePath);

        Assert.IsNull(directExecutablePath);
        Assert.AreEqual("exec -- vite --host --port 5174", cmd);
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
        // IsSafeBinRelPath allowlist 대표 케이스만 커버
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

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, fakeNode, out string directExecutablePath);

        Assert.AreEqual("node_modules/vite/bin/vite.js --host --port 5174", cmd);
        Assert.AreEqual(fakeNode, directExecutablePath);
    }

    [Test]
    public void Resolve_3x_NodeUnavailable_FallsBackToPnpmExec()
    {
        WriteBuildPackageJson("3.0.0-rc.0");
        WriteVitePackage("{\"vite\":\"bin/vite.js\"}", "bin/vite.js");
        string missingNode = Path.Combine(tempDir, "no-such-node");

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, missingNode, out string directExecutablePath);

        Assert.AreEqual("exec -- vite --host --port 5174", cmd);
        Assert.IsNull(directExecutablePath);
    }

    [Test]
    public void Resolve_3x_NodeAvailableButViteBinUnresolvable_FallsBackToPnpmExec()
    {
        WriteBuildPackageJson("3.0.0-rc.0");
        // node_modules/vite 자체가 없음 — node는 있지만 bin 해석이 실패하는 경우
        string fakeNode = CreateFakeNodeExecutable();

        string cmd = DevServerCommandResolver.Resolve(tempDir, 5174, fakeNode, out string directExecutablePath);

        Assert.AreEqual("exec -- vite --host --port 5174", cmd);
        Assert.IsNull(directExecutablePath);
    }
}
