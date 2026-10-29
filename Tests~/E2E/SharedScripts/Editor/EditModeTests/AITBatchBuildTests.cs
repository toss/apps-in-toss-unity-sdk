// -----------------------------------------------------------------------
// AITBatchBuildTests.cs - batchmode 빌드 진입점(AITBatchBuild)의 순수 로직 테스트
// Level 0: 인자 파싱, 결과 파일 경로, 결과 JSON 스키마, .ait 탐색 결과 → success/error 매핑
//   (DoExport 자체는 부르지 않는다. 실제 빌드는 -executeMethod 실행으로 검증)
// -----------------------------------------------------------------------

using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using AppsInToss;
using AppsInToss.Editor;

[TestFixture]
public class AITBatchBuildTests
{
    private string projectDir;

    [SetUp]
    public void Setup()
    {
        projectDir = Path.Combine(
            Path.GetTempPath(),
            "ait-test-batch-build-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(projectDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(projectDir))
            Directory.Delete(projectDir, true);
    }

    // ---- 인자 파싱 ----

    [Test]
    public void GetArgValue_ReturnsValueAfterName()
    {
        var args = new[] { "Unity", "-batchmode", "-aitResultPath", "out/r.json", "-quit" };
        Assert.AreEqual("out/r.json", AITBatchBuild.GetArgValue(args, "-aitResultPath"));
    }

    [Test]
    public void GetArgValue_NameIsLastArgument_ReturnsNull()
    {
        var args = new[] { "Unity", "-aitResultPath" };
        Assert.IsNull(AITBatchBuild.GetArgValue(args, "-aitResultPath"));
    }

    [Test]
    public void GetArgValue_Missing_ReturnsNull()
    {
        Assert.IsNull(AITBatchBuild.GetArgValue(new[] { "Unity" }, "-aitResultPath"));
        Assert.IsNull(AITBatchBuild.GetArgValue(null, "-aitResultPath"));
    }

    [Test]
    public void HasFlag_DetectsExactMatchOnly()
    {
        Assert.IsTrue(AITBatchBuild.HasFlag(new[] { "Unity", "-aitCleanBuild" }, "-aitCleanBuild"));
        Assert.IsFalse(AITBatchBuild.HasFlag(new[] { "Unity", "-aitCleanBuildX" }, "-aitCleanBuild"));
        Assert.IsFalse(AITBatchBuild.HasFlag(null, "-aitCleanBuild"));
    }

    // ---- 결과 파일 경로 ----

    [Test]
    public void ResolveResultPath_Default_IsUnderAitBuild()
    {
        string path = AITBatchBuild.ResolveResultPath(projectDir, new[] { "Unity" });
        Assert.AreEqual(Path.Combine(projectDir, "ait-build", "ait-build-result.json"), path);
    }

    [Test]
    public void ResolveResultPath_RelativeArg_IsResolvedAgainstProject()
    {
        string path = AITBatchBuild.ResolveResultPath(projectDir, new[] { "-aitResultPath", "out/r.json" });
        Assert.AreEqual(Path.GetFullPath(Path.Combine(projectDir, "out", "r.json")), path);
    }

    [Test]
    public void ResolveResultPath_AbsoluteArg_IsUsedAsIs()
    {
        string abs = Path.Combine(Path.GetTempPath(), "ait-batch-abs", "r.json");
        string path = AITBatchBuild.ResolveResultPath(projectDir, new[] { "-aitResultPath", abs });
        Assert.AreEqual(Path.GetFullPath(abs), path);
    }

    [Test]
    public void ResolveResultPath_InvalidArg_FallsBackToDefault()
    {
        string path = AITBatchBuild.ResolveResultPath(projectDir, new[] { "-aitResultPath", "out/\0bad.json" });
        Assert.AreEqual(Path.Combine(projectDir, "ait-build", "ait-build-result.json"), path);
    }

    [Test]
    public void ToProjectRelativePath_InsideProject_UsesForwardSlashes()
    {
        string full = Path.Combine(projectDir, "ait-build", "app.ait");
        Assert.AreEqual("ait-build/app.ait", AITBatchBuild.ToProjectRelativePath(projectDir, full));
    }

    [Test]
    public void ToProjectRelativePath_OutsideProject_ReturnsInput()
    {
        string outside = Path.Combine(Path.GetTempPath(), "somewhere-else", "app.ait");
        Assert.AreEqual(outside, AITBatchBuild.ToProjectRelativePath(projectDir, outside));
    }

    // ---- 결과 만들기 ----

    [Test]
    public void CreateResult_Succeed_WithAitInBuildRoot_ReportsPathAndSize()
    {
        string buildDir = Path.Combine(projectDir, "ait-build");
        Directory.CreateDirectory(buildDir);
        File.WriteAllBytes(Path.Combine(buildDir, "app.ait"), new byte[123]);

        var result = AITBatchBuild.CreateResult(AITConvertCore.AITExportError.SUCCEED, projectDir, 4567, "9.9.9");

        Assert.IsTrue(result.success);
        Assert.AreEqual("ait-build/app.ait", result.aitPath);
        Assert.AreEqual(123L, result.sizeBytes);
        Assert.AreEqual(4567L, result.durationMs);
        Assert.AreEqual("9.9.9", result.sdkVersion);
        Assert.AreEqual("", result.errorCode);
        Assert.AreEqual("", result.error);
    }

    [Test]
    public void CreateResult_Succeed_WithAitOnlyInDist_FallsBackToDist()
    {
        string distDir = Path.Combine(projectDir, "ait-build", "dist");
        Directory.CreateDirectory(distDir);
        File.WriteAllBytes(Path.Combine(distDir, "app.ait"), new byte[7]);

        var result = AITBatchBuild.CreateResult(AITConvertCore.AITExportError.SUCCEED, projectDir, 1, "1.0.0");

        Assert.IsTrue(result.success);
        Assert.AreEqual("ait-build/dist/app.ait", result.aitPath);
        Assert.AreEqual(7L, result.sizeBytes);
    }

    [Test]
    public void CreateResult_Succeed_WithoutAitFile_IsReportedAsAitFileMissing()
    {
        Directory.CreateDirectory(Path.Combine(projectDir, "ait-build"));

        var result = AITBatchBuild.CreateResult(AITConvertCore.AITExportError.SUCCEED, projectDir, 1, "1.0.0");

        Assert.IsFalse(result.success);
        Assert.AreEqual("AIT_FILE_MISSING", result.errorCode);
        Assert.IsNotEmpty(result.error);
        Assert.AreEqual("", result.aitPath);
        Assert.AreEqual(0L, result.sizeBytes);
    }

    [Test]
    public void CreateResult_ExportError_CarriesErrorCodeAndCause()
    {
        var result = AITBatchBuild.CreateResult(AITConvertCore.AITExportError.FAIL_NPM_BUILD, projectDir, 10, "1.0.0");

        Assert.IsFalse(result.success);
        Assert.AreEqual("FAIL_NPM_BUILD", result.errorCode);
        Assert.AreEqual(AITConvertCore.GetErrorCause(AITConvertCore.AITExportError.FAIL_NPM_BUILD), result.error);
    }

    [Test]
    public void CreateExceptionResult_ReportsTypeAndMessage()
    {
        var result = AITBatchBuild.CreateExceptionResult(new InvalidOperationException("boom"), 5, "1.0.0");

        Assert.IsFalse(result.success);
        Assert.AreEqual("BUILD_WEBGL_FAILED", result.errorCode);
        Assert.AreEqual("InvalidOperationException: boom", result.error);
    }

    // ---- JSON 스키마 ----

    [Test]
    public void ToJson_ContainsDocumentedFields_AndRoundTrips()
    {
        var original = AITBatchBuild.CreateFailure("INVALID_APP_CONFIG", "줄바꿈\n포함 \"따옴표\"", 321, "3.8.0");

        string json = original.ToJson();
        foreach (string key in new[] { "schemaVersion", "success", "aitPath", "sizeBytes", "errorCode", "error", "durationMs", "sdkVersion" })
            StringAssert.Contains("\"" + key + "\"", json);

        var restored = JsonUtility.FromJson<AITBatchBuildResult>(json);
        Assert.AreEqual(AITBatchBuild.ResultSchemaVersion, restored.schemaVersion);
        Assert.IsFalse(restored.success);
        Assert.AreEqual(original.errorCode, restored.errorCode);
        Assert.AreEqual(original.error, restored.error);
        Assert.AreEqual(321L, restored.durationMs);
        Assert.AreEqual("3.8.0", restored.sdkVersion);
    }
}
