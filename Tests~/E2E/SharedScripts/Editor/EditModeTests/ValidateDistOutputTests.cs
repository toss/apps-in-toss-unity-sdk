// -----------------------------------------------------------------------
// ValidateDistOutputTests.cs - ValidateDistOutput 커버리지 테스트
// Level 0: .ait 파일 존재 여부에 따른 ValidateDistOutput 동작 고정
//   (Sentry APPS-IN-TOSS-UNITY-SDK-10X 참조 — 원 증상 미해결, 커버리지 추가 목적)
//
// 원 증상: ait build 가 exit 0 으로 완료되었으나 .ait 파일이 dist/ 에 생성되지
//          않아 "[AIT] ✗ .ait 파일이 생성되지 않았습니다!" 에러 발생.
//          bundle.ios.js 는 dist/ 에 존재하나 .ait 패키징 단계가 실패한 시나리오.
//          → ait build CLI 동작이므로 EditMode 재현 불가.
//            ValidateDistOutput 자체의 판별 로직을 회귀 테스트로 고정.
// -----------------------------------------------------------------------

using NUnit.Framework;
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;
using AppsInToss.Editor;
using AppsInToss;

[TestFixture]
public class ValidateDistOutputTests
{
    private string tempDir;

    [SetUp]
    public void Setup()
    {
        tempDir = Path.Combine(
            Path.GetTempPath(),
            "ait-test-validate-dist-" + Guid.NewGuid().ToString("N").Substring(0, 8));
        Directory.CreateDirectory(tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
    }

    // =====================================================
    // .ait 파일 없음 → AIT_FILE_MISSING
    // 원 이슈 시나리오: dist/ 에 bundle.ios.js 만 있고 .ait 파일이 없는 경우
    // =====================================================

    [Test]
    public void ValidateDistOutput_NoAitFile_NoDistFolder_ReturnsAitFileMissing()
    {
        // buildProjectPath 에 아무 파일도 없는 경우
        // .ait 파일이 없으면 ValidateDistOutput 이 의도적으로 LogError 를 남기므로 기대 등록
        // (Unity Test Runner 는 미선언 LogError 를 자동으로 테스트 실패 처리함)
        LogAssert.Expect(LogType.Error, new Regex(@"\.ait 파일이 생성되지 않았습니다"));
        var result = AITBuildValidator.ValidateDistOutput(tempDir);
        Assert.AreEqual(AITConvertCore.AITExportError.AIT_FILE_MISSING, result);
    }

    [Test]
    public void ValidateDistOutput_DistFolderExists_ButOnlyNonAitFiles_ReturnsAitFileMissing()
    {
        // 원 이슈 시나리오: dist/ 에 bundle.ios.js 는 있으나 .ait 파일이 없음
        string distPath = Path.Combine(tempDir, "dist");
        Directory.CreateDirectory(distPath);
        File.WriteAllText(Path.Combine(distPath, "bundle.ios.js"), "// js bundle");
        File.WriteAllText(Path.Combine(distPath, "bundle.android.js"), "// js bundle");

        // .ait 파일이 없으면 ValidateDistOutput 이 의도적으로 LogError 를 남기므로 기대 등록
        LogAssert.Expect(LogType.Error, new Regex(@"\.ait 파일이 생성되지 않았습니다"));
        var result = AITBuildValidator.ValidateDistOutput(tempDir);
        Assert.AreEqual(AITConvertCore.AITExportError.AIT_FILE_MISSING, result,
            "dist/ 에 .ait 파일이 없으면 AIT_FILE_MISSING 을 반환해야 한다");
    }

    [Test]
    public void ValidateDistOutput_EmptyDistFolder_ReturnsAitFileMissing()
    {
        string distPath = Path.Combine(tempDir, "dist");
        Directory.CreateDirectory(distPath);

        // .ait 파일이 없으면 ValidateDistOutput 이 의도적으로 LogError 를 남기므로 기대 등록
        LogAssert.Expect(LogType.Error, new Regex(@"\.ait 파일이 생성되지 않았습니다"));
        var result = AITBuildValidator.ValidateDistOutput(tempDir);
        Assert.AreEqual(AITConvertCore.AITExportError.AIT_FILE_MISSING, result);
    }

    // =====================================================
    // .ait 파일이 빌드 루트에 있음 → SUCCEED
    // =====================================================

    [Test]
    public void ValidateDistOutput_AitFileInBuildRoot_ReturnsSucceed()
    {
        // ait build CLI 버전에 따라 빌드 루트에 .ait 파일이 생성될 수 있음
        File.WriteAllText(Path.Combine(tempDir, "app.ait"), "ait-content");

        var result = AITBuildValidator.ValidateDistOutput(tempDir);
        Assert.AreEqual(AITConvertCore.AITExportError.SUCCEED, result,
            "빌드 루트에 .ait 파일이 있으면 SUCCEED 를 반환해야 한다");
    }

    // =====================================================
    // .ait 파일이 dist/ 에 있음 → SUCCEED
    // =====================================================

    [Test]
    public void ValidateDistOutput_AitFileInDistFolder_ReturnsSucceed()
    {
        // 일반 케이스: dist/ 에 .ait 파일이 생성된 경우
        string distPath = Path.Combine(tempDir, "dist");
        Directory.CreateDirectory(distPath);
        File.WriteAllText(Path.Combine(distPath, "app.ait"), "ait-content");

        var result = AITBuildValidator.ValidateDistOutput(tempDir);
        Assert.AreEqual(AITConvertCore.AITExportError.SUCCEED, result,
            "dist/ 에 .ait 파일이 있으면 SUCCEED 를 반환해야 한다");
    }

    [Test]
    public void ValidateDistOutput_AitFileInDistFolder_WithOtherFiles_ReturnsSucceed()
    {
        // dist/ 에 .ait 파일과 다른 파일들이 함께 있는 경우
        string distPath = Path.Combine(tempDir, "dist");
        Directory.CreateDirectory(distPath);
        File.WriteAllText(Path.Combine(distPath, "app.ait"), "ait-content");
        File.WriteAllText(Path.Combine(distPath, "bundle.ios.js"), "// js");

        var result = AITBuildValidator.ValidateDistOutput(tempDir);
        Assert.AreEqual(AITConvertCore.AITExportError.SUCCEED, result);
    }

    // =====================================================
    // 빌드 루트 .ait 파일이 dist/ 탐색보다 우선됨
    // =====================================================

    [Test]
    public void ValidateDistOutput_AitFileInRoot_DistFolderAlsoExists_ReturnsSucceed()
    {
        // 빌드 루트에 .ait 파일이 있고 dist/ 도 존재하는 경우
        File.WriteAllText(Path.Combine(tempDir, "app.ait"), "ait-content");
        string distPath = Path.Combine(tempDir, "dist");
        Directory.CreateDirectory(distPath);
        // dist/ 에는 .ait 파일 없음

        var result = AITBuildValidator.ValidateDistOutput(tempDir);
        Assert.AreEqual(AITConvertCore.AITExportError.SUCCEED, result,
            "빌드 루트에 .ait 파일이 있으면 dist/ 탐색 없이 SUCCEED 를 반환해야 한다");
    }
}
