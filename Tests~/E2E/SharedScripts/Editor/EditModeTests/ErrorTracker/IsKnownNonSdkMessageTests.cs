// ---------------------------------------------------------------------------
// IsKnownNonSdkMessageTests.cs - IsKnownNonSdkMessage 단위 테스트
// IsAitRelated를 통과한 메시지 중 Unity 내부/사용자 프로젝트 패턴 필터링 검증.
// ---------------------------------------------------------------------------

using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

[TestFixture]
[Category("Unit")]
public class IsKnownNonSdkMessageTests
{
    #region Null/Empty Tests

    [Test]
    public void NullMessage_ReturnsFalse()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(null));
    }

    [Test]
    public void EmptyMessage_ReturnsFalse()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(""));
    }

    #endregion

    #region SDK 보호: AIT 키워드 포함 시 절대 필터링 안 함

    [Test]
    public void AitPrefix_NeverFiltered()
    {
        // [AIT] 접두사가 있는 메시지는 다른 패턴이 매칭되어도 필터링되면 안 됨
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("[AIT] GfxDevice renderer is null"));
    }

    [Test]
    public void AitWarnPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("[AITWarn] some warning"));
    }

    [Test]
    public void AppsInTossKeyword_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("AppsInToss build error matches more than one built-in atlases"));
    }

    [Test]
    public void AppsInTossPackageId_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("apps-in-toss config exceeds previous array size"));
    }

    [Test]
    public void AitNpmRunner_NeverFiltered()
    {
        // AitKeywords의 다른 키워드도 Unity 내부 패턴과 섞여도 보호되어야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITNpmRunner] pnpm install failed: matches more than one built-in atlases"));
    }

    [Test]
    public void AitConvertCore_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AITConvertCore.DoExport: GfxDevice renderer is null"));
    }

    [Test]
    public void AitPackageBuilder_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AITPackageBuilder: Localization-String-Tables-Shared missing"));
    }

    [Test]
    public void AitNodeJS_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AITNodeJS download: exceeds previous array size"));
    }

    [Test]
    public void AitColonPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: Ignoring locale en-US"));
    }

    [Test]
    public void AitBuildKeyword_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "ait-build step failed: Cannot read BuildLayout header"));
    }

    #endregion

    #region Unity 내부 경고 패턴

    [Test]
    public void GfxDeviceRendererNull_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("GfxDevice renderer is null"));
    }

    [Test]
    public void IgnoringLocale_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("Ignoring locale en-US because it is not supported"));
    }

    [Test]
    public void UnableToLoadBuildReport_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("Unable to load build report at Library/LastBuild.buildreport"));
    }

    [Test]
    public void CannotReadBuildLayout_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("Cannot read BuildLayout header"));
    }

    [Test]
    public void ServicesCoreTag_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("[ServicesCore] Initialization error"));
    }

    [Test]
    public void ProfileValueReferenceEmpty_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("ProfileValueReference: GetValue called with empty id"));
    }

    [Test]
    public void Editor32BitPlugins_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("The Editor does not support 32-bit plugins"));
    }

    #endregion

    #region 사용자 프로젝트 에셋 문제

    [Test]
    public void SpriteAtlasDuplicate_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Sprite Foo matches more than one built-in atlases"));
    }

    [Test]
    public void ScriptMissingInUserAssets_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Script attached to 'GameObject' in Assets/Scenes/Main.unity is missing or no valid script"));
    }

    [Test]
    public void ScriptMissingWithoutAssets_ReturnsFalse()
    {
        // "Script attached to" + "is missing"이지만 Assets/ 경로가 없으면 필터링 안 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Script attached to 'GameObject' is missing or no valid script"));
    }

    #endregion

    #region Unity 패키지 내부

    [Test]
    public void LocalizationStringTables_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Localization-String-Tables-Shared something"));
    }

    [Test]
    public void GraphInUnityPackage_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Warning in Graph at Packages/com.unity.visualscripting/foo"));
    }

    #endregion

    #region 사용자 프로젝트 직렬화

    [Test]
    public void AssemblyCSharpSerialization_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Fields serialized in [Assembly-CSharp] MyClass can't be serialized"));
    }

    [Test]
    public void FieldsSerializedInSdkAssembly_NotFiltered()
    {
        // SDK 어셈블리(AppsInTossSDKEditor 등)의 직렬화 경고는 보호되어야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Fields serialized in [AppsInTossSDKEditor] AITConfig changed"));
    }

    #endregion

    #region 외부 패키지

    [Test]
    public void MetaButFolderMissing_ReturnsTrue()
    {
        // Unity가 괄호를 붙이는 버전
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Foo.meta) exists but its folder doesn't"));
    }

    [Test]
    public void MetaButFolderMissing_WithoutParen_ReturnsTrue()
    {
        // Unity가 괄호 없이 출력하는 버전 — 패턴은 두 경우 모두 매칭
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Foo.meta exists but its folder doesn't"));
    }

    #endregion

    #region Unity URP 내부

    [Test]
    public void ExceedsPreviousArraySize_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Index 5 exceeds previous array size 3"));
    }

    #endregion

    #region SDK 관련 메시지는 통과 (negative cases)

    [Test]
    public void GenericSdkErrorWithoutPattern_ReturnsFalse()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("Random SDK error"));
    }

    [Test]
    public void EmptyAitNothingMatches_ReturnsFalse()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage("Some unrelated message"));
    }

    #endregion
}
