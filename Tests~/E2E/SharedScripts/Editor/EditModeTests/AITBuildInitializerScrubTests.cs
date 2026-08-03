// -----------------------------------------------------------------------
// AITBuildInitializerScrubTests.cs - ScrubTemplatePreprocessorHazards 단위 테스트
// Level 0: BuildConfig~ 하위 비-소스 산출물(node_modules/.npm-cache/dist) 제거 검증
//   - Unity WebGL 템플릿 전처리기(Preprocess.js) 크래시 방지 (Sentry SDK-137)
//   - 소스 파일/폴더는 보존, 위험 폴더만 제거, 부재 시 무해(no-op)
// -----------------------------------------------------------------------

using System.IO;
using NUnit.Framework;
using AppsInToss.Editor; // AITBuildInitializer (internal — AssemblyInfo InternalsVisibleTo 경유)

[TestFixture]
public class AITBuildInitializerScrubTests
{
    private string _buildConfigDir;

    [SetUp]
    public void Setup()
    {
        _buildConfigDir = Path.Combine(Path.GetTempPath(), "ait-scrub-tests-" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_buildConfigDir);
    }

    [TearDown]
    public void Cleanup()
    {
        if (_buildConfigDir != null && Directory.Exists(_buildConfigDir))
            Directory.Delete(_buildConfigDir, recursive: true);
    }

    private void MakeDir(string name)
    {
        Directory.CreateDirectory(Path.Combine(_buildConfigDir, name));
    }

    private void MakeFile(string relativePath, string content = "x")
    {
        string full = Path.Combine(_buildConfigDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full));
        File.WriteAllText(full, content);
    }

    [Test]
    public void Scrub_RemovesHazardFolders_PreservesSourceFiles()
    {
        // 위험 폴더(전처리기가 들어가면 크래시) + 그 안의 파일
        MakeFile("node_modules/.pnpm/pkg/lib/Generate.js", "#endif");
        MakeFile(".npm-cache/tarball.tgz");
        MakeFile("dist/web/index.js", "console.log(1)");
        // 보존되어야 하는 소스
        MakeFile("package.json", "{}");
        MakeFile("vite.config.ts", "export default {}");

        AITBuildInitializer.ScrubTemplatePreprocessorHazards(_buildConfigDir);

        Assert.IsFalse(Directory.Exists(Path.Combine(_buildConfigDir, "node_modules")), "node_modules는 제거되어야 한다");
        Assert.IsFalse(Directory.Exists(Path.Combine(_buildConfigDir, ".npm-cache")), ".npm-cache는 제거되어야 한다");
        Assert.IsFalse(Directory.Exists(Path.Combine(_buildConfigDir, "dist")), "dist는 제거되어야 한다");

        Assert.IsTrue(File.Exists(Path.Combine(_buildConfigDir, "package.json")), "소스 package.json은 보존되어야 한다");
        Assert.IsTrue(File.Exists(Path.Combine(_buildConfigDir, "vite.config.ts")), "소스 vite.config.ts는 보존되어야 한다");
    }

    [Test]
    public void Scrub_NoHazardFolders_IsNoOp()
    {
        MakeFile("package.json", "{}");

        // 위험 폴더가 없을 때 예외 없이 통과해야 한다
        Assert.DoesNotThrow(() => AITBuildInitializer.ScrubTemplatePreprocessorHazards(_buildConfigDir));
        Assert.IsTrue(File.Exists(Path.Combine(_buildConfigDir, "package.json")));
    }

    [Test]
    public void Scrub_NonexistentPath_IsNoOp()
    {
        string missing = Path.Combine(_buildConfigDir, "does-not-exist");
        Assert.DoesNotThrow(() => AITBuildInitializer.ScrubTemplatePreprocessorHazards(missing));
    }

    [Test]
    public void Scrub_NullOrEmptyPath_IsNoOp()
    {
        Assert.DoesNotThrow(() => AITBuildInitializer.ScrubTemplatePreprocessorHazards(null));
        Assert.DoesNotThrow(() => AITBuildInitializer.ScrubTemplatePreprocessorHazards(string.Empty));
    }
}
