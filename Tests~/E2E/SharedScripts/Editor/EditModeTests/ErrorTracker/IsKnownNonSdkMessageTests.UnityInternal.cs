// IsKnownNonSdkMessageTests.UnityInternal.cs - Unity 엔진·패키지 자체 경고
using NUnit.Framework;
using AppsInToss.Editor.ErrorTracker;

public partial class IsKnownNonSdkMessageTests
{
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
    public void UnableToLoadBuildReport_AddressablesBuildLayout_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-RP — Unity Addressables가 빌드 리포트 파일이 없을 때 출력하는 표준 경고.
        // Library/com.unity.addressables/BuildReports/ 하위 timestamped 파일명까지 매칭되는지 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Unable to load build report at Library/com.unity.addressables/BuildReports/buildlayout_2026.05.12.07.23.02.json."));
    }

    [Test]
    public void UnableToLoadBuildReport_WithAitPrefix_NeverFiltered()
    {
        // SDK 보호 가드: AIT prefix가 붙은 동일 본문은 SDK 자체 로그로 보호되어야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Unable to load build report at Library/com.unity.addressables/BuildReports/foo.json"));
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
    public void SendMessageDuringAwake_OnRectTransformDimensionsChange_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-T6 — 사용자 컴포넌트(ViewPortrait)의
        // OnRectTransformDimensionsChange에서 발생한 라이프사이클 변형.
        //
        // 회귀 가드: "Portrait:" 안의 "ait:"가 AitKeywords의 "AIT:"와 case-insensitive로
        // substring 충돌하던 거짓양성을 단어 경계 정책으로 차단했음을 검증한다.
        // 거짓양성이 차단되면 SDK 보호 가드가 발동하지 않고 NonSdkMessagePatterns의
        // "SendMessage cannot be called during Awake, CheckConsistency, or OnValidate" 패턴이
        // 정상 매칭되어 노이즈로 드롭된다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "SendMessage cannot be called during Awake, CheckConsistency, or OnValidate (ViewPortrait: OnRectTransformDimensionsChange)"));
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
    public void LegacyAnimationClipCannotBeUsedInState_Finger_ReturnsTrue()
    {
        // Sentry KV — Unity가 메시지 첫 줄만 캡처하는 변형 ("Legacy AnimationClips are not allowed..." 후행 문구 없음)
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "The legacy Animation Clip \"finger\" cannot be used in the State \"finger\"."));
    }

    [Test]
    public void LegacyAnimationClipCannotBeUsedInState_StartSound_ReturnsTrue()
    {
        // Sentry KT
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "The legacy Animation Clip \"start_sound\" cannot be used in the State \"start_sound\"."));
    }

    [Test]
    public void LegacyAnimationClipCannotBeUsedInState_WithAitPrefix_NeverFiltered()
    {
        // SDK 보호 가드 회귀 방지: AIT prefix가 붙으면 SDK 자체 로그로 간주되어야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] The legacy Animation Clip \"foo\" cannot be used in the State \"foo\"."));
    }

    [Test]
    public void UncompiledCodeChangesBuild_ReturnsTrue()
    {
        // Sentry SDK-NE — Unity가 미컴파일 변경 상태에서 빌드 시도 시 출력하는 자체 경고
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "You are building a player, but you have uncompiled code changes. This means that any post-processing or importing code you may have changed will not affect this player build."));
    }

    [Test]
    public void UncompiledCodeChangesBuild_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙으면 SDK 자체 로그로 간주되어야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] You are building a player, but you have uncompiled code changes."));
    }

    [Test]
    public void NonDevelopmentBuildAutoConnectingProfiler_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-T3 — 사용자가 Development 옵션 없이 ConnectWithProfiler를 설정해
        // Unity가 직접 던지는 ArgumentException. 사용자 BuildPlayerOptions 구성 문제이며 SDK 버그 아님.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityError: 변환 중 오류가 발생했습니다: System.ArgumentException: Non-development build cannot allow auto-connecting the profiler. Either add the Development build option,"));
    }

    [Test]
    public void NonDevelopmentBuildAutoConnectingProfiler_PlainException_ReturnsTrue()
    {
        // Unity가 영문 환경 또는 다른 호출 경로에서 ArgumentException 본문만 그대로 출력하는 변형.
        // "Non-development build cannot allow auto-connecting the profiler" 부분 문자열이 핵심 불변 문구.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "ArgumentException: Non-development build cannot allow auto-connecting the profiler. Either add the Development build option, or unset ConnectWithProfiler."));
    }

    [Test]
    public void NonDevelopmentBuildAutoConnectingProfiler_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙으면 SDK 자체 로그로 간주되어야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Non-development build cannot allow auto-connecting the profiler"));
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

    #region Unity AssetDatabase.FindAssets 폴더 미발견 경고 (SDK-ZZ)

    [Test]
    public void AssetDatabaseFindAssetsFolderNotFound_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-ZZ — Unity AssetDatabase가 존재하지 않는 검색 폴더로
        // FindAssets 호출 시 직접 출력하는 엔진 경고. SDK 로그 접두사 없는 Unity 패키지 탐색 노이즈.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AssetDatabase.FindAssets: Folder not found: 'Assets/NonExistentFolder'"));
    }

    [Test]
    public void AssetDatabaseFindAssetsFolderNotFound_BareMessage_ReturnsTrue()
    {
        // 경로 suffix 없이 핵심 문구만 도착하는 변형도 동일하게 드롭.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AssetDatabase.FindAssets: Folder not found"));
    }

    [Test]
    public void AssetDatabaseFindAssetsFolderNotFound_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주되어야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] AssetDatabase.FindAssets: Folder not found: 'Assets/Foo'"));
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

    #region Unity AssetDatabase GUID 충돌 (SDK-BQ)

    [Test]
    public void GuidConflict_SdkAssetImported_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-BQ — 사용자가 SDK를 UPM이 아닌 Assets/ 하위로 import.
        // Unity 엔진이 출력하는 GUID 충돌 경고로, SDK 코드에서 차단 불가.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "GUID [ab2202024c4c0485e9a366792ed7605d] for asset 'Assets/WebGLTemplates/AITTemplate/TemplateData/unity-logo-dark.png' conflicts with:\n  'Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates/AITTemplate/TemplateData/unity-logo-dark.png' (current owner)\nAssigning a new guid.\n"));
    }

    [Test]
    public void GuidConflict_StyleCss_ReturnsTrue()
    {
        // BQ의 다른 변형 — style.css에서도 동일 패턴
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "GUID [d097f2670df9e41ff90dd9bda7da052c] for asset 'Assets/WebGLTemplates/AITTemplate/TemplateData/style.css' conflicts with:\n  'Packages/im.toss.apps-in-toss-unity-sdk/WebGLTemplates/AITTemplate/TemplateData/style.css' (current owner)\nAssigning a new guid.\n"));
    }

    [Test]
    public void GuidDiagnostic_WithoutConflict_NotFiltered()
    {
        // composite AND 가드: "GUID ["만 있고 "conflicts with:"는 없는 다른 메시지는 통과
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "GUID [abc123] computed for asset; nothing else to report."));
    }

    #endregion

    #region Unity Addressables / AssetDatabase 노이즈 (SDK-QE, SDK-P4, SDK-P6, SDK-CH)

    [Test]
    public void Addressables_MissingLinker_ReturnsTrue()
    {
        // Sentry SDK-QE — 사용자 프로젝트의 Addressables 그룹/링커 설정 누락. SDK 영역 아님.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "BuildFailedException: Missing Addressables linker file. " +
            "Please ensure the linker file is present in your project."));
    }

    [Test]
    public void AssetDatabase_SaveAssetsRestricted_ReturnsTrue()
    {
        // Sentry SDK-P4 — 사용자 코드/플러그인이 import 중에 SaveAssets 호출.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Calls to \"AssetDatabase.SaveAssets\" are restricted during asset importing."));
    }

    [Test]
    public void AssetDatabase_ScheduledForReimport_ReturnsTrue()
    {
        // Sentry SDK-P6 — Refresh 루프 중 import 충돌. Unity 자체 진단.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "The asset at ProjectSettings/ProjectSettings.asset has been scheduled for reimport " +
            "during the Refresh loop and will be reimported."));
    }

    [Test]
    public void ImmutablePackages_UnexpectedlyAltered_ReturnsTrue()
    {
        // Sentry SDK-CH — Unity PackageManager가 immutable 패키지 변경 감지 시 직접 출력.
        // LogType.Warning 가드(line 432-436)와 별개로 Error/Exception LogType 변형도 흡수.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "The following asset(s) located in immutable packages were unexpectedly altered. " +
            "These assets should be reverted to their original state."));
    }

    [Test]
    public void Addressables_GenericMessage_NotFiltered()
    {
        // "Missing Addressables linker"가 없는 일반 Addressables 메시지는 통과.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Addressables: catalog cache miss"));
    }

    [Test]
    public void AssetDatabase_GenericRestriction_NotFiltered()
    {
        // "SaveAssets" 토큰이 없으면 통과.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Calls to AssetDatabase.Refresh are restricted during asset importing."));
    }

    #endregion

    #region immutable 폴더 meta 파일 누락 경고 (SDK-10K, 10J, 10H, 10G, 10F, APPS-IN-TOSS-UNITY-SDK-12K)

    [Test]
    public void ImmutableFolder_MissingMeta_Img_ReturnsTrue()
    {
        // Sentry SDK-10K — apple-signin-unity의 Img 폴더 meta 누락.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Asset Packages/com.lupidan.apple-signin-unity/Img has no meta file, but it's in an immutable folder. The asset will be ignored."));
    }

    [Test]
    public void ImmutableFolder_MissingMeta_ProjectSettings_ReturnsTrue()
    {
        // Sentry SDK-10J, 10H, 10G, 10F — AppleAuthSampleProject/ProjectSettings 경로 변형.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Asset Packages/com.lupidan.apple-signin-unity/AppleAuthSampleProject/ProjectSettings has no meta file, but it's in an immutable folder. The asset will be ignored."));
    }

    [Test]
    public void ImmutableFolder_MissingMeta_UnityWarningPrefix_ReturnsTrue()
    {
        // "UnityWarning: " prefix가 덧붙은 변형도 부분 문자열 매칭으로 동일하게 드롭.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityWarning: Asset Packages/com.foo.bar/Baz has no meta file, but it's in an immutable folder."));
    }

    [Test]
    public void ImmutableFolder_MissingMeta_AitPrefix_NotFiltered()
    {
        // AIT prefix가 붙은 SDK 자체 로그는 보호 — immutable 폴더 문구가 없는 SDK 메시지는 매칭 안 됨.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] 패키지 meta 파일을 검증합니다."));
    }

    [Test]
    public void ImmutableFolder_MissingMeta_SdkPluginsClaudeMd_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-12K — SDK 자체 immutable 패키지 폴더(im.toss.apps-in-toss-unity-sdk)
        // 내 파일에 .meta가 없을 때 Unity 에디터가 직접 출력하는 표준 경고. 메시지에 SDK 패키지 경로
        // (apps-in-toss)가 들어가 SDK 키워드 가드가 발동하므로, 가드보다 먼저 매칭하는 전용 분기로 드롭한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityError: Asset Packages/im.toss.apps-in-toss-unity-sdk/Runtime/SDK/Plugins/CLAUDE.md has no meta file, but it's in an immutable folder. The asset will be ignored."));
    }

    [Test]
    public void ImmutableFolder_MissingMeta_SdkPluginsClaudeMd_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: "[AIT" prefix가 붙은 동일 본문은 SDK 자체 로그로 간주되어
        // immutable 폴더 전용 분기(!StartsWith("[AIT"))에서 제외되고 키워드 가드로 보호되어야 한다.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Asset Packages/im.toss.apps-in-toss-unity-sdk/Runtime/SDK/Plugins/CLAUDE.md has no meta file, but it's in an immutable folder."));
    }

    #endregion
}
