// -----------------------------------------------------------------------
// BuildMarkerTests.cs - 빌드 마커 파일 생성/읽기 회귀 테스트
// Level 0: WriteBuildMarker가 부모 디렉토리 부재 시에도 성공하는지 검증 (Sentry SDK-DC)
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using AppsInToss;

[TestFixture]
public class BuildMarkerTests
{
    private string tempDir;
    private MethodInfo writeMarkerMethod;
    private MethodInfo readMarkerMethod;
    private Type buildInfoType;

    [SetUp]
    public void Setup()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "ait-test-marker-" + Guid.NewGuid().ToString("N").Substring(0, 8));

        var convertType = typeof(AITConvertCore);
        buildInfoType = convertType.Assembly.GetType("AppsInToss.AITBuildInfo");
        Assert.IsNotNull(buildInfoType, "AITBuildInfo type should exist in assembly");

        writeMarkerMethod = convertType.GetMethod("WriteBuildMarker",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(writeMarkerMethod, "WriteBuildMarker method should exist");

        readMarkerMethod = convertType.GetMethod("ReadBuildMarker",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(readMarkerMethod, "ReadBuildMarker method should exist");
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempDir))
        {
            Directory.Delete(tempDir, true);
        }
    }

    private object MakeBuildInfo()
    {
        var info = Activator.CreateInstance(buildInfoType);
        buildInfoType.GetField("sdkVersion").SetValue(info, "test-version");
        buildInfoType.GetField("buildTime").SetValue(info, "2026-01-01T00:00:00Z");
        buildInfoType.GetField("compressionFormat").SetValue(info, 0);
        buildInfoType.GetField("decompressionFallback").SetValue(info, false);
        buildInfoType.GetField("profileName").SetValue(info, "Production");
        buildInfoType.GetField("unityVersion").SetValue(info, "2021.3.45f2");
        return info;
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

        var buildInfo = MakeBuildInfo();

        // DirectoryNotFoundException이 발생하지 않아야 함
        Assert.DoesNotThrow(() =>
        {
            writeMarkerMethod.Invoke(null, new object[] { missingOutputPath, buildInfo });
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

        var buildInfo = MakeBuildInfo();
        writeMarkerMethod.Invoke(null, new object[] { existingOutputPath, buildInfo });

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
        var written = MakeBuildInfo();

        writeMarkerMethod.Invoke(null, new object[] { outputPath, written });

        object read = readMarkerMethod.Invoke(null, new object[] { outputPath });
        Assert.IsNotNull(read, "ReadBuildMarker should return a value after WriteBuildMarker");

        Assert.AreEqual("test-version",
            buildInfoType.GetField("sdkVersion").GetValue(read),
            "sdkVersion should round-trip");
        Assert.AreEqual("Production",
            buildInfoType.GetField("profileName").GetValue(read),
            "profileName should round-trip");
    }

    // =====================================================
    // 멱등성: 같은 경로에 두 번 써도 마지막 값이 남음
    // =====================================================

    [Test]
    public void WriteBuildMarker_IsIdempotent_WhenCalledTwice()
    {
        string outputPath = Path.Combine(tempDir, "webgl");

        var first = MakeBuildInfo();
        writeMarkerMethod.Invoke(null, new object[] { outputPath, first });

        var second = MakeBuildInfo();
        buildInfoType.GetField("sdkVersion").SetValue(second, "second-version");
        writeMarkerMethod.Invoke(null, new object[] { outputPath, second });

        object read = readMarkerMethod.Invoke(null, new object[] { outputPath });
        Assert.AreEqual("second-version",
            buildInfoType.GetField("sdkVersion").GetValue(read),
            "Second WriteBuildMarker should overwrite the first");
    }
}
