// -----------------------------------------------------------------------
// BuildFileSelectionTests.cs - EditMode Build 파일 선별 테스트
// Level 0: FindFileInBuild의 패턴 매칭 및 중복 파일 최신 선택 로직 검증
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.IO;
using AppsInToss.Editor;

[TestFixture]
public class BuildFileSelectionTests
{
    private string tempDir;

    [SetUp]
    public void Setup()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "ait-test-build-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }

    // =====================================================
    // 단일 매치 — 정확한 파일명 반환
    // =====================================================

    [Test]
    public void FindFileInBuild_SingleMatch_ReturnsFileName()
    {
        string filePath = Path.Combine(tempDir, "build.loader.js");
        File.WriteAllText(filePath, "// loader");

        string result = AITBuildValidator.FindFileInBuild(tempDir, "*.loader.js");

        Assert.AreEqual("build.loader.js", result);
    }

    // =====================================================
    // 다중 매치 — 최신 파일 선택
    // Build 선별 복사의 핵심 변경을 검증:
    // 이전 코드는 파일시스템 순서로 반환했지만,
    // 현재 코드는 LastWriteTime 기준 최신 파일을 선택함
    // =====================================================

    [Test]
    public void FindFileInBuild_MultipleMatches_SelectsNewestFile()
    {
        // 오래된 파일 (이전 빌드 잔여물)
        string oldFile = Path.Combine(tempDir, "old_hash.loader.js");
        File.WriteAllText(oldFile, "// old loader");
        File.SetLastWriteTime(oldFile, new DateTime(2025, 1, 1, 0, 0, 0));

        // 최신 파일 (현재 빌드)
        string newFile = Path.Combine(tempDir, "new_hash.loader.js");
        File.WriteAllText(newFile, "// new loader");
        File.SetLastWriteTime(newFile, new DateTime(2026, 2, 1, 0, 0, 0));

        string result = AITBuildValidator.FindFileInBuild(tempDir, "*.loader.js");

        Assert.AreEqual("new_hash.loader.js", result,
            "FindFileInBuild should select the newest file when multiple matches exist");
    }

    // =====================================================
    // 매치 없음 — 빈 문자열 반환
    // =====================================================

    [Test]
    public void FindFileInBuild_NoMatch_ReturnsEmpty()
    {
        // Build 폴더에 관련 없는 파일만 존재
        File.WriteAllText(Path.Combine(tempDir, "unrelated.txt"), "nothing");

        string result = AITBuildValidator.FindFileInBuild(tempDir, "*.loader.js");

        Assert.AreEqual("", result);
    }

    // =====================================================
    // 압축 확장자 (.br, .gz) 매치
    // Unity 압축 설정에 따라 .wasm.br, .wasm.gz 파일이 생성됨
    // =====================================================

    [TestCase("build.wasm.br", "*.wasm*")]
    [TestCase("build.wasm.gz", "*.wasm*")]
    [TestCase("build.data.unityweb", "*.data*")]
    [TestCase("build.framework.js.br", "*.framework.js*")]
    public void FindFileInBuild_CompressedExtensions_Matches(string fileName, string pattern)
    {
        File.WriteAllText(Path.Combine(tempDir, fileName), "compressed");

        string result = AITBuildValidator.FindFileInBuild(tempDir, pattern);

        Assert.AreEqual(fileName, result,
            $"Pattern '{pattern}' should match compressed file '{fileName}'");
    }

    // =====================================================
    // 존재하지 않는 경로 — 빈 문자열 반환
    // =====================================================

    [Test]
    public void FindFileInBuild_NonExistentPath_ReturnsEmpty()
    {
        string fakePath = Path.Combine(tempDir, "nonexistent");

        string result = AITBuildValidator.FindFileInBuild(fakePath, "*.loader.js");

        Assert.AreEqual("", result);
    }

    // =====================================================
    // 다중 매치 3개 이상 — 가장 최신 파일 선택
    // 이전 빌드가 여러 번 쌓인 경우를 시뮬레이션
    // =====================================================

    [Test]
    public void FindFileInBuild_ThreeMatches_SelectsNewest()
    {
        // 가장 오래된 파일
        string file1 = Path.Combine(tempDir, "aaa.data");
        File.WriteAllText(file1, "data1");
        File.SetLastWriteTime(file1, new DateTime(2025, 1, 1));

        // 중간 파일
        string file2 = Path.Combine(tempDir, "bbb.data");
        File.WriteAllText(file2, "data2");
        File.SetLastWriteTime(file2, new DateTime(2025, 6, 1));

        // 최신 파일
        string file3 = Path.Combine(tempDir, "ccc.data");
        File.WriteAllText(file3, "data3");
        File.SetLastWriteTime(file3, new DateTime(2026, 2, 1));

        string result = AITBuildValidator.FindFileInBuild(tempDir, "*.data*");

        Assert.AreEqual("ccc.data", result,
            "FindFileInBuild should select the newest among 3+ matching files");
    }
}
