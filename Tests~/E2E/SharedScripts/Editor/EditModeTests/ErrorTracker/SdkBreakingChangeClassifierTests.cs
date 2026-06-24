// ---------------------------------------------------------------------------
// SdkBreakingChangeClassifierTests.cs - IsSdkBreakingChangeCompileError 단위 테스트
// 사용자 프로젝트가 SDK API 변경(브레이킹 체인지)으로 컴파일에 실패한 'error CS####'(AppsInToss
// 참조)만 sdk_breaking_change 버킷으로 분류되는지 검증한다. 경고(warning CS)·사용자 자체 오류·
// SDK 내부 경로 컴파일 에러·SDK 자체 로그([AIT)는 분류 대상이 아니어야 한다.
// ---------------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

[TestFixture]
[Category("Unit")]
public class SdkBreakingChangeClassifierTests
{
    // ---- positive: SDK 심볼(AppsInToss)을 참조하는 컴파일 '에러' ----

    [Test]
    public void CS0117_MemberMissingOnAppsInTossMenu_IsBreakingChange()
    {
        // Sentry SDK-80: 'AppsInTossMenu' does not contain a definition for 'Package'
        Assert.IsTrue(AITEditorErrorTracker.IsSdkBreakingChangeCompileError(
            @"Assets\98_Tools\BuildTool\Editor\BuildToolEditorWindow.cs(484,43): error CS0117: 'AppsInTossMenu' does not contain a definition for 'Package'"));
    }

    [Test]
    public void CS0246_AppsInTossTypeNotFound_IsBreakingChange()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsSdkBreakingChangeCompileError(
            "Assets/Scripts/Foo.cs(12,9): error CS0246: The type or namespace name 'AppsInToss' could not be found (are you missing a using directive or an assembly reference?)"));
    }

    [Test]
    public void CS1503_AppsInTossArgumentConversion_IsBreakingChange()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsSdkBreakingChangeCompileError(
            "Assets/Scripts/Manager/TossManager.cs(192,91): error CS1503: Argument 1: cannot convert from 'AppsInToss.GetUserKeyForGameResult' to 'string'"));
    }

    [Test]
    public void PathStripped_ErrorCsReferencingAppsInToss_IsBreakingChange()
    {
        // Sentry로 파일 경로 prefix 없이 컴파일러 진단 본문만 도달하는 변형도 흡수해야 한다.
        Assert.IsTrue(AITEditorErrorTracker.IsSdkBreakingChangeCompileError(
            "error CS0246: The type or namespace name 'AppsInToss' could not be found"));
    }

    // ---- negative: 경고 / 사용자 자체 오류 / SDK 내부 / SDK 로그 ----

    [Test]
    public void UserErrorWithoutAppsInToss_IsNotBreakingChange()
    {
        // AppsInToss를 참조하지 않는 순수 사용자 오류는 SDK 브레이킹 체인지가 아니다.
        Assert.IsFalse(AITEditorErrorTracker.IsSdkBreakingChangeCompileError(
            "Assets/Scripts/Foo.cs(3,5): error CS0103: The name 'fooBarBaz' does not exist in the current context"));
    }

    [Test]
    public void WarningReferencingAppsInToss_IsNotBreakingChange()
    {
        // CS1998 경고는 컴파일을 깨뜨리지 않으므로(=호환성 깨짐 아님) error CS 매칭에 걸리지 않아야 한다.
        Assert.IsFalse(AITEditorErrorTracker.IsSdkBreakingChangeCompileError(
            @"Assets\Scripts\1. System\AppsInToss\TossManager.cs(260,43): warning CS1998: This async method lacks 'await' operators and will run synchronously."));
    }

    [Test]
    public void SdkPackagePathCompileError_IsNotBreakingChange()
    {
        // SDK 패키지 경로(im.toss.apps-in-toss)의 컴파일 에러는 실 SDK 버그 → 일반 경로(error_source=sdk)로.
        Assert.IsFalse(AITEditorErrorTracker.IsSdkBreakingChangeCompileError(
            "Packages/im.toss.apps-in-toss-unity-sdk/Editor/AITConvertCore.cs(42,9): error CS0103: The name 'AppsInToss' does not exist in the current context"));
    }

    [Test]
    public void LegacySdkPackageCachePathCompileError_IsNotBreakingChange()
    {
        // 레거시 패키지명(com.toss.apps-in-toss)도 SDK 내부 경로이므로 제외.
        Assert.IsFalse(AITEditorErrorTracker.IsSdkBreakingChangeCompileError(
            "Library/PackageCache/com.toss.apps-in-toss-unity-sdk@1.0.0/Editor/Foo.cs(1,1): error CS0117: 'AppsInToss' does not contain a definition for 'X'"));
    }

    [Test]
    public void AitOwnLogLine_IsNotBreakingChange()
    {
        // SDK 자체 로그([AIT prefix)는 분류 대상이 아니다(실 SDK 로그를 가리지 않도록).
        Assert.IsFalse(AITEditorErrorTracker.IsSdkBreakingChangeCompileError(
            "[AIT] error CS0117 AppsInToss something"));
    }

    [Test]
    public void NullOrEmpty_IsNotBreakingChange()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsSdkBreakingChangeCompileError(null));
        Assert.IsFalse(AITEditorErrorTracker.IsSdkBreakingChangeCompileError(string.Empty));
    }
}
