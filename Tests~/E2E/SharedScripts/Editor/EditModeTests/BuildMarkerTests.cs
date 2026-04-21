// -----------------------------------------------------------------------
// BuildMarkerTests.cs - 빌드 마커 파일 생성/읽기 회귀 테스트
// Level 0: WriteBuildMarker가 부모 디렉토리 부재 시에도 성공하는지 검증 (Sentry SDK-DC)
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.IO;
using AppsInToss;

[TestFixture]
public class BuildMarkerTests
{
    private string tempDir;

    [SetUp]
    public void Setup()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "ait-test-marker-" + Guid.NewGuid().ToString("N").Substring(0, 8));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }

    private static AITBuildInfo MakeBuildInfo(string sdkVersion = "test-version")
    {
        return new AITBuildInfo
        {
            sdkVersion = sdkVersion,
            buildTime = "2026-01-01T00:00:00Z",
            compressionFormat = 0,
            decompressionFallback = false,
            profileName = "Production",
            unityVersion = "2021.3.45f2"
        };
    }

    // =====================================================
    // Sentry SDK-DC 회귀 테스트: 부모 디렉토리가 없어도 마커 생성 성공
    // =====================================================

    [Test]
    public void WriteBuildMarker_CreatesParentDirectory_WhenMissing()
    {
        string missingOutputPath = Path.Combine(tempDir, "webgl");
        Assert.IsFalse(Directory.Exists(missingOutputPath),
            "Precondition: webgl/ should not exist");

        // DirectoryNotFoundException이 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            AITConvertCore.WriteBuildMarker(missingOutputPath, MakeBuildInfo());
        }, "WriteBuildMarker should not throw when parent directory is missing");

        string markerPath = Path.Combine(missingOutputPath, AITConvertCore.BUILD_MARKER_FILENAME);
        Assert.IsTrue(File.Exists(markerPath),
            "Marker file should be written at the expected path");
    }

    // =====================================================
    // 기존 디렉토리가 있을 때도 정상 동작
    // =====================================================

    [Test]
    public void WriteBuildMarker_Succeeds_WhenParentExists()
    {
        string existingOutputPath = Path.Combine(tempDir, "webgl");
        Directory.CreateDirectory(existingOutputPath);

        AITConvertCore.WriteBuildMarker(existingOutputPath, MakeBuildInfo());

        string markerPath = Path.Combine(existingOutputPath, AITConvertCore.BUILD_MARKER_FILENAME);
        Assert.IsTrue(File.Exists(markerPath),
            "Marker file should be written when parent directory exists");
    }

    // =====================================================
    // 쓰기 후 ReadBuildMarker로 왕복 검증
    // =====================================================

    [Test]
    public void WriteBuildMarker_RoundTripsThroughReadBuildMarker()
    {
        string outputPath = Path.Combine(tempDir, "webgl");

        AITConvertCore.WriteBuildMarker(outputPath, MakeBuildInfo());

        AITBuildInfo read = AITConvertCore.ReadBuildMarker(outputPath);
        Assert.IsNotNull(read, "ReadBuildMarker should return a value after WriteBuildMarker");
        Assert.AreEqual("test-version", read.sdkVersion, "sdkVersion should round-trip");
        Assert.AreEqual("Production", read.profileName, "profileName should round-trip");
    }

    // =====================================================
    // 멱등성: 같은 경로에 두 번 써도 마지막 값이 남음
    // =====================================================

    [Test]
    public void WriteBuildMarker_IsIdempotent_WhenCalledTwice()
    {
        string outputPath = Path.Combine(tempDir, "webgl");

        AITConvertCore.WriteBuildMarker(outputPath, MakeBuildInfo("first-version"));
        AITConvertCore.WriteBuildMarker(outputPath, MakeBuildInfo("second-version"));

        AITBuildInfo read = AITConvertCore.ReadBuildMarker(outputPath);
        Assert.IsNotNull(read);
        Assert.AreEqual("second-version", read.sdkVersion,
            "Second WriteBuildMarker should overwrite the first");
    }
}
