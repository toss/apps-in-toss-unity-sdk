// -----------------------------------------------------------------------
// BuildMarkerTests.cs - 빌드 마커 파일 생성/읽기 회귀 테스트
// Level 0: WriteBuildMarker가 부모 디렉토리 부재 시에도 성공하는지 검증 (Sentry SDK-DC)
//          + ShouldForceCleanBuild가 마커 부재 시 정확한 Warning을 남기는지 검증 (Sentry SDK-AA)
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.TestTools;
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

    // =====================================================
    // Sentry SDK-AA 회귀 테스트: 마커 누락 시 경고 + Clean build
    // =====================================================

    // 분류기가 메시지 본문으로 매칭하므로 문자열을 고정해서 회귀를 막는다.
    private const string EXPECTED_MISSING_MARKER_WARNING =
        "[AIT] 빌드 마커가 없습니다. Clean build를 수행합니다.";

    private static bool InvokeShouldForceCleanBuild(string outputPath, bool cleanBuild)
    {
        MethodInfo m = typeof(AITConvertCore).GetMethod(
            "ShouldForceCleanBuild",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(m, "ShouldForceCleanBuild 메서드를 찾을 수 없습니다.");
        return (bool)m.Invoke(null, new object[] { outputPath, cleanBuild });
    }

    [Test]
    public void ShouldForceCleanBuild_ReturnsTrueAndWarns_WhenMarkerIsMissing()
    {
        // 빌드 산출 디렉토리는 존재하지만 마커 파일은 없는 상태
        string outputPath = Path.Combine(tempDir, "webgl");
        Directory.CreateDirectory(outputPath);
        string markerPath = Path.Combine(outputPath, AITConvertCore.BUILD_MARKER_FILENAME);
        Assert.IsFalse(File.Exists(markerPath), "Precondition: marker should be absent");

        LogAssert.Expect(LogType.Warning, EXPECTED_MISSING_MARKER_WARNING);

        bool shouldClean = InvokeShouldForceCleanBuild(outputPath, false);

        Assert.IsTrue(shouldClean,
            "마커가 없으면 ShouldForceCleanBuild는 true를 반환해야 합니다.");
    }
}
