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
    public void SpriteAtlasDuplicate_FullSentryMessage_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-DW에서 실제 관찰된 원문 fixture.
        // 현재 필터는 IndexOf 부분 매칭이라 후행 문구 유무는 결과에 영향 없지만,
        // 실제 입력 예시를 테스트로 박아두면 향후 필터가 anchored/regex로 바뀔 때
        // 이 실측 메시지를 놓치지 않도록 방어한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Sprite object_19_01 matches more than one built-in atlases. Default to use the first available atlas."));
    }

    // 필터가 향후 anchored/regex로 tightening되어도 실제 Unity 출력 변형을 놓치지 않도록
    // 구조적으로 서로 다른 리스크 프로파일의 변형만 선별해 검증 (단순 이름 swap은 제외).
    [TestCase(
        "Sprite object_04_02 matches more than one built-in atlases. Default to use the first available atlas.",
        TestName = "SpriteAtlas_StandardLayout_ReturnsTrue")]
    [TestCase(
        "[Warn]  Sprite cloud matches more than one built-in atlases.",
        TestName = "SpriteAtlas_WithPrefixAndWhitespace_ReturnsTrue")]
    [TestCase(
        "Sprite 'player idle frame' matches more than one built-in atlases (fallback applied).",
        TestName = "SpriteAtlas_QuotedNameAndTrailingParen_ReturnsTrue")]
    public void SpriteAtlasDuplicate_RealisticVariants_ReturnsTrue(string message)
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(message));
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
    public void FieldsSerializedInSdkAssembly_NeverFiltered()
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

    #region 사용자 환경 문제 (WebGL 모듈 미설치, GUID 충돌)

    [Test]
    public void BuildTargetWebGLNotSupported_ReturnsTrue()
    {
        // 사용자 Unity 설치에 WebGL 모듈이 없어서 발생 — SDK-DD
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Build target 'WebGL' not supported"));
    }

    [Test]
    public void BuildTargetWebGLNotSupported_FullMessage_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Build target 'WebGL' not supported. Please install it via Unity Hub."));
    }

    [Test]
    public void GuidConflict_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-BQ에서 실제 관찰된 메시지 형태를 반영한 fixture.
        // 사용자 프로젝트에 동일 GUID의 에셋이 남아있어 AITTemplate 파일과 충돌.
        // AITTemplate 경로가 메시지에 포함되어도 AitKeywords(`[AIT`, `AIT:` 등) 중 어느 것의 부분 문자열도 아니므로
        // SDK 보호 가드가 발동하지 않아 일반 필터 흐름으로 내려가 정상 필터링됨.
        // 필터가 향후 anchored/regex로 tightening되더라도 이 실측 경로를 놓치지 않도록 fixture로 박아둠.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "GUID [abc123def456] for asset 'Assets/WebGLTemplates/AITTemplate/TemplateData/diagnostics.css' conflicts with: 'Assets/OldCopy/diagnostics.css'"));
    }

    [Test]
    public void GuidConflict_WithoutAitTemplate_ReturnsTrue()
    {
        // AITTemplate 경로가 없어도 일반 GUID 충돌 메시지라면 사용자 프로젝트 문제
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "GUID [deadbeef] for asset 'Assets/Foo/bar.png' conflicts with: 'Assets/Baz/bar.png'"));
    }

    [Test]
    public void GuidWithoutConflictsWith_ReturnsFalse()
    {
        // "GUID ["만 있고 "conflicts with:"가 없으면 composite AND의 한쪽만 충족되어 필터링 안 됨
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "GUID [abc123] for asset 'Assets/Foo/bar.png' updated"));
    }

    [Test]
    public void ConflictsWithoutGuid_ReturnsFalse()
    {
        // "conflicts with:"만 있고 "GUID ["가 없으면 composite AND의 한쪽만 충족되어 필터링 안 됨
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Package version conflicts with: older version"));
    }

    [Test]
    public void BuildTargetWebGLNotSupported_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주되어야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Build target 'WebGL' not supported"));
    }

    [Test]
    public void GuidConflict_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙은 GUID 충돌 메시지는 SDK가 직접 출력한 것으로 간주되어야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] GUID [abc123] for asset 'Assets/Foo/bar.png' conflicts with: 'Assets/Baz/bar.png'"));
    }

    #endregion

    #region 신규 추가 패턴 (PR2 strict source gate)

    [Test]
    public void SendMessageDuringAwake_ReturnsTrue()
    {
        // Sentry KQ/KD/KC/KB — Unity 자체 경고
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "SendMessage cannot be called during Awake, CheckConsistency, or OnValidate."));
    }

    [Test]
    public void SendMessageDuringAwake_WithAitPrefix_NeverFiltered()
    {
        // SDK 보호 가드: AIT prefix 붙으면 필터 안 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] SendMessage cannot be called during Awake, CheckConsistency, or OnValidate."));
    }

    [Test]
    public void LegacyAnimationClips_ReturnsTrue()
    {
        // Sentry KT/KV — Unity 자체 경고
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Legacy AnimationClips are not allowed in Animator Controllers"));
    }

    [Test]
    public void LegacyAnimationClips_WithAitPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Legacy AnimationClips are not allowed in Animator Controllers"));
    }

    [Test]
    public void AddressableGroupSchemasMissing_ReturnsTrue()
    {
        // Sentry KR — 사용자 프로젝트의 Addressable 설정 문제
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Group 'Default Local Group' does not have any associated AddressableAssetGroupSchemas"));
    }

    [Test]
    public void AddressableGroupSchemasMissing_WithAitPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Group 'Foo' does not have any associated AddressableAssetGroupSchemas"));
    }

    [Test]
    public void AudioClipImportWarning_ReturnsTrue()
    {
        // Sentry GT/GV — 사용자 프로젝트 에셋 import 경고
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Warnings during import of AudioClip Assets/Sounds/bgm.wav"));
    }

    [Test]
    public void AudioClipImportWarning_WithAitPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Warnings during import of AudioClip Assets/Sounds/bgm.wav"));
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
