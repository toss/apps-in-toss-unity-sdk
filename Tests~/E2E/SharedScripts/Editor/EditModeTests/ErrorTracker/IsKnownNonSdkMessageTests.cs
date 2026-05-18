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

    #region 외부 AIT prefix (SDK가 출력하지 않는 prefix) — 가드 우회 드롭

    [Test]
    public void ExternalAitLoginPrefix_AitMockOrTimeout_ReturnsTrue()
    {
        // Sentry SDK-D2 — 외부 코드가 [AIT Login] prefix로 출력한 fallback 경고.
        // SDK 코드에는 "[AIT Login]" 문자열이 존재하지 않으므로 노이즈로 드롭한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT Login][src=AIT_MOCK_OR_TIMEOUT] status=RanToCompletion, len=0 → device fallback"));
    }

    [Test]
    public void ExternalAitLoginPrefix_ForbiddenOrigin_ReturnsTrue()
    {
        // Sentry SDK-D3
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT Login] InitSession failed: FORBIDDEN_ORIGIN"));
    }

    [Test]
    public void RegularAitPrefix_NotMatchingExternal_StillProtected()
    {
        // "[AIT Login]"이 아닌 일반 [AIT] prefix는 SDK 보호 가드로 필터링되지 않아야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] InitSession started"));
    }

    [Test]
    public void ExternalTossFirebasePrefix_GameLoginFailure_ReturnsTrue()
    {
        // Sentry SDK-CF — 사용자 게임 백엔드의 [Toss Firebase] prefix.
        // SDK 코드에는 "[Toss Firebase]"/"게임로그인" 문자열이 존재하지 않음.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[Toss Firebase] 게임로그인 실패: Object reference not set to an instance of an object"));
    }

    [Test]
    public void ExternalFtrAppsInTossPath_BackslashPrefix_ReturnsTrue()
    {
        // Sentry SDK-PK/PJ/PF/PC/PB — Windows 경로 변형. 사용자 프로젝트 경로 prefix는
        // ExternalAitPrefixes를 통해 SDK 보호 가드보다 먼저 매칭되어 노이즈로 드롭된다.
        // CS0414/NonSdkMessagePatterns에 의존하지 않는 입력으로 ExternalAitPrefixes 코드 경로를 직접 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets\\FTR_AppsInToss\\Optimization\\Platform\\PlatformMemoryManager.cs(19,36): some unrelated diagnostic"));
    }

    [Test]
    public void ExternalFtrAppsInTossPath_ForwardSlashPrefix_ReturnsTrue()
    {
        // Sentry SDK-QA/Q9/Q8/Q7/Q6/Q5/Q4 — POSIX 경로 변형. NonSdkMessagePatterns 키워드 없이도
        // ExternalAitPrefixes만으로 노이즈 분류되어야 함.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/FTR_AppsInToss/Optimization/Platform/PlatformOptimizer.cs(17,35): some unrelated diagnostic"));
    }

    [Test]
    public void ExternalFtrAppsInTossPath_WithAitKeyword_StillFiltered()
    {
        // ExternalAitPrefixes는 AitKeywords 가드보다 먼저 매칭되어야 한다.
        // 사용자 코드에 AppsInToss 식별자가 섞여도 FTR_AppsInToss 경로 prefix가 우선해 드롭된다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/FTR_AppsInToss/Foo.cs(1,1): warning CS0123: AppsInToss reference unresolved"));
    }

    [Test]
    public void SdkAssetsPath_NotFtrAppsInToss_StillProtected()
    {
        // SDK 자체 경로(Runtime/, Editor/) 또는 일반 Assets/ 경로는
        // FTR_AppsInToss prefix와 무관하므로 SDK 보호 가드가 정상 동작해야 한다.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Assets/Other/Foo.cs error: AppsInToss runtime issue"));
    }

    [Test]
    public void ExternalAitPromotionPrefix_CaptureEntryPointSkipped_ReturnsTrue()
    {
        // Sentry SDK-S1 — 사용자 게임의 프로모션 로직이 <color=Yellow>AITPromotion</color> tag로 출력하는 로그.
        // SDK 코드 어디에도 "AITPromotion" 문자열이 존재하지 않음 (grep 확인).
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[05:58:01:496] <color=Yellow>AITPromotion</color>: CaptureEntryPoint skipped on non-WebGL"));
    }

    [Test]
    public void ExternalAitPromotionPrefix_SkipNotPromotionEntry_ReturnsTrue()
    {
        // Sentry SDK-SX — 동일 prefix를 사용하는 또 다른 분기 출력 (rebirthCount 조건).
        // 단일 ExternalAitPrefixes 패턴 "AITPromotion</color>"이 두 변형 모두 매칭함을 검증.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[02:00:21:536] <color=Yellow>AITPromotion</color>: Skip: not a promotion entry (rebirthCount=1)"));
    }

    [Test]
    public void AitPromotionPrefix_WithAitKeyword_StillFiltered()
    {
        // ExternalAitPrefixes는 AitKeywords 가드보다 먼저 매칭되므로,
        // 동일 메시지에 SDK 키워드가 섞여도 외부 prefix가 우선해 드롭된다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "<color=Yellow>AITPromotion</color>: AppsInToss helper called"));
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
    public void AitColonPrefix_MidSentence_LeadingNonWordBoundary_NeverFiltered()
    {
        // "AIT:" prefix는 줄 시작이 아니어도 토큰 경계(공백 등) 직후이면 SDK 자체 로그로 보호되어야 한다.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[WARN] AIT: Ignoring locale en-US"));
    }

    [Test]
    public void PortraitColonSubstring_NotMatchingAitColon_FilteredAsNoise()
    {
        // 회귀 가드: "Portrait:" 내부의 "ait:" substring은 "AIT:"와 case-insensitive로
        // 충돌하지만 왼쪽이 letter라 단어 경계 정책에 의해 SDK 키워드로 인정하지 않는다.
        // 따라서 SDK 보호 가드가 발동하지 않고 NonSdkMessagePatterns로 정상 필터된다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "SendMessage cannot be called during Awake, CheckConsistency, or OnValidate (ViewPortrait: OnFoo)"));
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
    // Sentry APPS-IN-TOSS-UNITY-SDK-RM / RN — "UnityWarning: " prefix가 붙은 실측 변형.
    [TestCase(
        "UnityWarning: Sprite object_03_07 matches more than one built-in atlases. Default to use the first available atlas.",
        TestName = "SpriteAtlas_UnityWarningPrefix_ObjectName_ReturnsTrue")]
    [TestCase(
        "UnityWarning: Sprite background_theme1_3 matches more than one built-in atlases. Default to use the first available atlas.",
        TestName = "SpriteAtlas_UnityWarningPrefix_UnderscoredName_ReturnsTrue")]
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

    [TestCase(
        "Script attached to '_iOSUpdate' in scene 'Assets/Scenes/Main.unity' is missing or no valid script is attached.",
        TestName = "ScriptMissingInUserScene_iOSUpdate_ReturnsTrue")]
    [TestCase(
        "Script attached to 'Image_Popup' in scene 'Assets/Scenes/Main.unity' is missing or no valid script is attached.",
        TestName = "ScriptMissingInUserScene_ImagePopup_ReturnsTrue")]
    [TestCase(
        "Script attached to '_AdManager' in scene 'Assets/Scenes/Main.unity' is missing or no valid script is attached.",
        TestName = "ScriptMissingInUserScene_AdManager_ReturnsTrue")]
    public void ScriptMissingInUserScene_RealisticVariants_ReturnsTrue(string message)
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-H0/GX/GW — 사용자 씬의 missing script 경고.
        // "in scene 'Assets/...'" 형태도 기존 composite 가드(Script attached to + is missing + Assets/)가 매칭한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(message));
    }

    [Test]
    public void ScriptMissingInUserScene_WithAitPrefix_NeverFiltered()
    {
        // composite missing-script 가드의 세 조건(Script attached to + is missing + Assets/)을 모두 만족하지만,
        // "[AIT" 키워드가 MessageContainsSdkKeyword 가드에 먼저 매칭되어 composite 가드 도달 전에 return false 한다.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Script attached to '_iOSUpdate' in scene 'Assets/Scenes/Main.unity' is missing or no valid script is attached."));
    }

    [Test]
    public void ScriptMissingInUserPrefab_LasercahrgingFixture_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-H1 실측 메시지.
        // "Script attached to" + "is missing" + "Assets/" composite 조건으로 매칭됨.
        // 필터가 향후 anchored/regex로 tightening되어도 이 실측 메시지를 놓치지 않도록 fixture로 박아둠.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Script attached to '_NotUse_Effetc_Lasercharging' in asset 'Assets/Resources/Effect/_NotUse_Effetc_Lasercharging.prefab' is missing or no valid script is attached."));
    }

    [Test]
    public void ScriptMissingInUserPrefab_HitMissileFixture_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-GY 실측 메시지.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Script attached to '_NotUse_HitMissile_P10' in asset 'Assets/Resources/Effect/_NotUse_HitMissile_P10.prefab' is missing or no valid script is attached."));
    }

    [Test]
    public void ScriptMissingInUserPrefab_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙으면 SDK 자체 로그로 간주되어 필터링되지 않아야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Script attached to 'Foo' in asset 'Assets/Resources/Foo.prefab' is missing or no valid script is attached."));
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

    [Test]
    public void FmodSoundCreateError_ReturnsTrue()
    {
        // Sentry NM/NK/N9/CJ 등 — 사용자 프로젝트의 FMOD 오디오 에셋 문제
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Error: Cannot create FMOD::Sound instance for clip \"button_tick\" (FMOD error: Couldn't perform seek operation. ...)"));
    }

    [Test]
    public void FmodSoundCreateError_WithAitPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Cannot create FMOD::Sound instance for clip \"foo\""));
    }

    [Test]
    public void FmodFsbLoadStateError_ReturnsTrue()
    {
        // Sentry CX/N5/N7/N8 — FSB 로드 실패
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Failed getting load state of FSB for audio clip \"bensound-ukulele\""));
    }

    [Test]
    public void AudioClipLoadError_ReturnsTrue()
    {
        // Sentry P2/P3 — 오디오 데이터 로드 실패
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Error: Cannot load audio data for audio clip \"button_tick\""));
    }

    [Test]
    public void AnimatorTransitionMissingExitTime_ReturnsTrue()
    {
        // Sentry NY — 사용자 Animator 컨트롤러 설정 누락
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Asset 'Machine': Transition 'Spin -> Exit' in state 'Spin' doesn't have an Exit Time or any condition, transition will be ignored"));
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

    [Test]
    public void AssemblyCSharpTypeMismatch_ReturnsTrue()
    {
        // Sentry NG/NF — 사용자 코드 타입의 player/editor 직렬화 mismatch
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Type '[Assembly-CSharp]AdSwitcher' has an extra field 'adGoogleAdPlacementManager' of type 'AdGoogleAdPlacementManager' in the player and thus can't be serialized"));
    }

    [Test]
    public void SdkAssemblyTypeMismatch_NeverFiltered()
    {
        // SDK 어셈블리의 동일 패턴은 필터링되지 않아야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Type '[AppsInTossSDKEditor]AITConfig' has an extra field 'foo'"));
    }

    [Test]
    public void FailedToCompilePlayerScripts_ReturnsTrue()
    {
        // Sentry H3 — 사용자 게임 코드 컴파일 실패 (스택 없이 메시지만 도착)
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Failed to compile player scripts"));
    }

    [Test]
    public void FailedToCompilePlayerScripts_WithAitPrefix_NeverFiltered()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Failed to compile player scripts"));
    }

    [Test]
    public void ExecGitTrace_ShowHead_ReturnsTrue()
    {
        // Sentry NX — Unity Editor가 외부 git 명령 실행 시 stdout으로 출력하는 trace.
        // SDK 코드 어디에도 "Exec>" 문자열이 없음을 grep으로 확인하여 안전하게 노이즈로 분류.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Exec> git show -s --pretty=%D HEAD"));
    }

    [Test]
    public void ExecGitTrace_LogShortSha_ReturnsTrue()
    {
        // Sentry NW
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Exec> git log -1 --pretty=format:%h"));
    }

    [Test]
    public void ExecGitTrace_WithAitPrefix_NeverFiltered()
    {
        // SDK 보호 가드 회귀 방지: AIT prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Exec> git show -s --pretty=%D HEAD"));
    }

    [Test]
    public void ExecWithoutGit_ReturnsFalse()
    {
        // "Exec> " 단독은 너무 일반적이라 매칭 안 함 — git 호출 trace에 한정
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Exec> npm install"));
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
    public void ExecGitBreadcrumb_GitShow_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-NX — 외부 도구의 git 서브프로세스 실행 브레드크럼
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Exec> git show -s --pretty=%D HEAD"));
    }

    [Test]
    public void ExecGitBreadcrumb_GitLog_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-NW
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Exec> git log -1 --pretty=format:%h"));
    }

    [Test]
    public void ExecGitBreadcrumb_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: SDK가 [AIT] prefix와 함께 동일 메시지를 출력했다면 필터되면 안 됨
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Exec> git show -s --pretty=%D HEAD"));
    }

    [Test]
    public void AddressableContentBuildFailure_ReturnsTrue()
    {
        // Sentry SDK-H2 — Unity Addressables 콘텐츠 빌드 실패 (SDK와 무관한 사용자 프로젝트 문제)
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "UnityError: Addressable content build failure (duration : 0:00:16.61)"));
    }

    [Test]
    public void AddressableContentBuildFailure_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙으면 SDK 자체 로그로 간주
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Addressable content build failure (duration : 0:00:16.61)"));
    }

    [TestCase(
        "[Worker2] Import Error Code:(4)",
        TestName = "WorkerImportError_Worker2_ReturnsTrue")]
    [TestCase(
        "[Worker3] Import Error Code:(4)",
        TestName = "WorkerImportError_Worker3_ReturnsTrue")]
    [TestCase(
        "[Worker4] Import Error Code:(4)",
        TestName = "WorkerImportError_Worker4_ReturnsTrue")]
    // Sentry APPS-IN-TOSS-UNITY-SDK-V7 — 워커 prefix 없이 UnityWarning으로 래핑된 변형
    [TestCase(
        "UnityWarning: Import Error Code:(4)",
        TestName = "WorkerImportError_UnityWarningWrapped_ReturnsTrue")]
    public void WorkerImportError_RealisticVariants_ReturnsTrue(string message)
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-RC/RD/RE/V7 — Unity AssetImporter 내부/메인 에셋 임포트 에러.
        // 워커 prefix 유무·워커 번호·코드 숫자가 가변이지만 "Import Error Code:(" 부분 문자열로 모두 매칭.
        // SDK 코드에는 "Import Error Code" 문자열이 존재하지 않음 (grep으로 확인).
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(message));
    }

    [Test]
    public void WorkerImportError_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙으면 SDK 자체 로그로 간주되어 필터링되지 않아야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] [Worker2] Import Error Code:(4)"));
        // UnityWarning 래핑 변형에 대한 가드 회귀 방지
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Import Error Code:(4)"));
    }

    [Test]
    public void Il2CppStackTracesNotSupportedOnWebGL_ReturnsTrue()
    {
        // Sentry SDK-8B — Unity 엔진 자체 경고. WebGL 빌드는 IL2CPP의 "Method Name, File Name, Line Number"
        // 스택트레이스 옵션을 지원하지 않으므로 PlayerSettings에 해당 옵션이 켜져 있을 때 Unity가 직접 출력.
        // SDK 코드와 무관하므로 노이즈로 분류.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "The \"Method Name, File Name, and Line Number\" option for IL2CPP stack traces is not supported on WebGL."));
    }

    [Test]
    public void Il2CppStackTracesNotSupportedOnWebGL_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주되어야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] The \"Method Name, File Name, and Line Number\" option for IL2CPP stack traces is not supported on WebGL."));
    }

    [Test]
    public void MetaFileInvalidGuid_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-R4 — 사용자 Assets/ 경로의 .meta 파일 GUID 손상
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "The .meta file Assets/Art/Images/Skin0/specialblock_fire_5.png.meta does not have a valid GUID and its corresponding Asset file will be ignored. If this file is not malformed, please add a GUID, or delete the .meta file and it will be recreated correctly"));
    }

    [Test]
    public void MetaFileInvalidGuid_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] The .meta file Assets/Foo/bar.png.meta does not have a valid GUID"));
    }

    [Test]
    public void GuidInsideMetaCannotBeExtracted_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-R3 — YAML Parser GUID 추출 실패 경고
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "The GUID inside 'Assets/Art/Images/Skin0/specialblock_fire_5.png.meta' cannot be extracted by the YAML Parser. Attempting to extract it via string matching instead. Please verify the file does not contain unexpected data."));
    }

    [Test]
    public void GuidInsideMetaCannotBeExtracted_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] The GUID inside 'Assets/Foo.png.meta' cannot be extracted by the YAML Parser"));
    }

    #endregion

    #region 사용자 코드 CS0029 암묵 변환 컴파일 에러 (SDK-T2)

    [Test]
    public void UserCodeCS0029_IapProductListItem_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-T2 실측 메시지 — 사용자 IAP 매니저 코드가 SDK 반환 배열을
        // List<T>에 암묵 변환하려다 발생한 컴파일 에러. SDK 타입명('AppsInToss.IapProductListItem')이
        // 포함되어 AitKeywords 가드를 발동시키지만, composite AND 가드가 그보다 먼저 매칭되어 노이즈로 드롭됨.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/02_Scripts/02_Manager/IAP/IAPServiceToss.cs(24,29): error CS0029: Cannot implicitly convert type 'AppsInToss.IapProductListItem[]' to 'System.Collections.Generic.List<AppsInToss.IapProductListItem>'"));
    }

    [Test]
    public void UserCodeCS0029_OtherUserPath_ReturnsTrue()
    {
        // CS0029 + Assets/ + .cs( 조합은 SDK 타입 참조 여부와 무관하게 사용자 코드 컴파일 에러로 분류.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/Scripts/GameLogic.cs(42,10): error CS0029: Cannot implicitly convert type 'int' to 'string'"));
    }

    [Test]
    public void Cs0029WithoutAssetsPrefix_AitProtected_ReturnsFalse()
    {
        // 'Assets/' 경로가 없으면 composite AND 가드의 한 조건이 빠지므로 매칭되지 않고,
        // AitKeywords 가드가 정상 동작해 SDK 자체 로그로 간주됨 (필터링 안 함).
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] error CS0029 reported from SDK build pipeline"));
    }

    [Test]
    public void SdkPackageCS0029_ReturnsFalse()
    {
        // SDK 자체 .cs 파일의 컴파일 에러는 'Packages/com.toss.apps-in-toss/...' 경로로 출력되어
        // 'Assets/' prefix가 붙지 않으므로 composite AND 가드가 매칭되지 않아야 함 (SDK 보호).
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Packages/com.toss.apps-in-toss/Runtime/Foo.cs(10,5): error CS0029: Cannot implicitly convert type 'int' to 'string'"));
    }

    [Test]
    public void OtherCSErrorInUserAssets_ReturnsFalse()
    {
        // 가드가 등록되지 않은 다른 CS 에러 코드(예: CS0535)는 composite 가드가 매칭되지 않아 통과해야 함.
        // 본 SDK는 CS0029/CS0103/CS0105/CS0117/CS0246/CS1503 한정으로 가드.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/Scripts/GameLogic.cs(42,10): error CS0535: 'X' does not implement interface member 'Y'"));
    }

    #endregion

    #region 사용자 코드 CS0103/CS0117 컴파일 에러 (SDK-CR, SDK-MN, SDK-80)

    [Test]
    public void UserCodeCS0103_AssetsBackslashPath_ReturnsTrue()
    {
        // Sentry SDK-CR — Windows 경로 + 사용자 식별자 미발견.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets\\Scripts\\AppsInTossCompatibilityChecker.cs(31,9): error CS0103: The name 'CheckIncompatibleComponents' does not exist in the current context"));
    }

    [Test]
    public void UserCodeCS0103_AssetsForwardSlashPath_ReturnsTrue()
    {
        // Sentry SDK-MN POSIX 변형.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/AppsInToss/01_Script/CreateObstacle.cs(22,54): error CS0103: The name 'touchPos' does not exist in the current context"));
    }

    [Test]
    public void UserCodeCS0117_AppsInTossMenu_ReturnsTrue()
    {
        // Sentry SDK-80 — 사용자 BuildTool 코드가 SDK 타입의 존재하지 않는 멤버 참조.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets\\98_Tools\\BuildTool\\Editor\\BuildToolEditorWindow.cs(484,43): error CS0117: 'AppsInTossMenu' does not contain a definition for 'Package'"));
    }

    [Test]
    public void Cs0103WithoutAssetsPrefix_AitProtected_ReturnsFalse()
    {
        // SDK 자체 진단 라인(Assets/ 경로 미포함)은 [AIT] prefix로 보호됨.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] diag: error CS0103 reported in SDK fallback path"));
    }

    [Test]
    public void Cs0103_SdkPackagePath_NotFiltered()
    {
        // SDK 자체 .cs의 CS0103은 Packages/com.toss.apps-in-toss 경로로 출력되어 Assets/ prefix 미포함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Packages/com.toss.apps-in-toss/Editor/Foo.cs(10,5): error CS0103: The name 'bar' does not exist in the current context"));
    }

    [Test]
    public void Cs0117_SdkPackagePath_NotFiltered()
    {
        // CS0117도 동일 — SDK 자체 코드 보호.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Packages/com.toss.apps-in-toss/Editor/Foo.cs(10,5): error CS0117: 'Bar' does not contain a definition for 'baz'"));
    }

    #endregion

    #region pnpm stdout 패스스루 (SDK-HA, SDK-R6)

    [Test]
    public void PnpmStdoutPassthrough_TrailingAnchor_ReturnsTrue()
    {
        // Sentry SDK-HA — 본문 없는 "[pnpm] 출력:" stdout passthrough
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("[pnpm] 출력:"));
    }

    [Test]
    public void PnpmStdoutPassthrough_LeadingAnchor_ReturnsTrue()
    {
        // Sentry SDK-R6 — "[pnpm] 출력:"으로 시작하는 패스스루 라인 (후행 본문이 있어도 노이즈)
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[pnpm] 출력: WARN deprecated some message"));
    }

    [Test]
    public void PnpmStdoutPassthrough_WithAitPrefix_NeverFiltered()
    {
        // SDK가 직접 출력한 "[AIT...]" prefix가 붙으면 SDK 보호 가드로 필터링되지 않아야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] [pnpm] 출력: build failed"));
    }

    #endregion

    #region Sentry transport 자기참조 노이즈 (4xx/5xx + 네트워크 + 동기)

    [Test]
    public void SentryTransport_Http503_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-T4 — Sentry 서버의 일시적 503 응답.
        // Transport 자체의 실패를 다시 Sentry로 보내면 self-loop이 발생하므로 드롭.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] Sentry 전송 실패 (HTTP 503)"));
    }

    [Test]
    public void SentryTransport_Http500_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] Sentry 전송 실패 (HTTP 500)"));
    }

    [Test]
    public void SentryTransport_Http502_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] Sentry 전송 실패 (HTTP 502)"));
    }

    [Test]
    public void SentryTransport_Http504_ReturnsTrue()
    {
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] Sentry 전송 실패 (HTTP 504)"));
    }

    [Test]
    public void SentryTransport_Http400_ReturnsTrue()
    {
        // 4xx도 self-loop 방지 정책에 따라 동일하게 차단. SDK 분기 정보가 없고
        // SubmitResult.Fail로 호출자에게 결과가 전달되므로 가시성 손실 없음.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] Sentry 전송 실패 (HTTP 400)"));
    }

    [Test]
    public void SentryTransport_Http401_ReturnsTrue()
    {
        // DSN 오설정도 사용자 가시성은 콘솔에 유지되므로 self-loop 방지를 우선.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] Sentry 전송 실패 (HTTP 401)"));
    }

    [Test]
    public void SentryTransport_SyncSendFailure_ReturnsTrue()
    {
        // 에디터 종료 FlushSync 경로의 예외도 self-loop 방지를 위해 차단.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] 동기 전송 실패: System.Net.WebException: ..."));
    }

    [Test]
    public void SentryTransport_NetworkError_ReturnsTrue()
    {
        // Sentry SDK-CZ, KA — ConnectionError 등 transport 네트워크 일시 장애는 SDK 가드 우회로 드롭.
        // Transport가 자기 출력을 다시 Sentry로 보내면 캐스케이드 위험.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] 네트워크 오류: Connection refused"));
    }

    [Test]
    public void SentryTransport_NetworkErrorUnknown_ReturnsTrue()
    {
        // Sentry SDK-CZ
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] 네트워크 오류: Unknown Error"));
    }

    [Test]
    public void SentryTransport_NetworkErrorTimeout_ReturnsTrue()
    {
        // Sentry SDK-KA
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] 네트워크 오류: Request timeout"));
    }

    [Test]
    public void SentryTransport_OtherDiagnostic_NotFiltered()
    {
        // Transport의 다른 진단 메시지(예: 큐 가득 참, 동기 전송 등)는 영향받지 않아야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] 큐가 가득 차서 가장 오래된 이벤트를 버립니다"));
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

    #region 사용자 게임 코드 IAP 진단 (SDK-CY)

    [Test]
    public void TossIap_InitializeFailed_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-CY — 사용자 게임 코드(외부 IAP 모듈) 진단.
        // SDK 코드에 'Toss IAP' 문자열은 없음.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Toss IAP: Initialize failed or no products"));
    }

    #endregion

    #region 사용자 코드 컴파일러 경고/에러 (SDK-SW, SDK-T0, SDK-C3/M7)

    [Test]
    public void UsingDirectiveAppearedPreviously_AppsInToss_ReturnsTrue()
    {
        // Sentry SDK-SW — 사용자 코드의 using AppsInToss; 중복 (CS0105).
        // Unity 컴파일러가 직접 출력하므로 SDK 외부 메시지로 드롭한다.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets\\Script\\AITIAPManager.cs(11,7): warning CS0105: The using directive for 'AppsInToss' appeared previously in this namespace"));
    }

    [Test]
    public void UsingDirectiveAppearedPreviously_AitKeywordProtected()
    {
        // SDK 자체 로그가 "[AIT ...] warning CS0105 ..." 형태로 캡처될 가능성 보호.
        // AitKeywords 가드로 필터링되지 않아야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] internal: warning CS0105 collision"));
    }

    [Test]
    public void DestroyingGameObjectsImmediately_ReturnsTrue()
    {
        // Sentry SDK-T0 — 사용자 MonoBehaviour의 OnValidate/animation event 등에서 즉시 파괴 시도.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Destroying GameObjects immediately is not permitted during physics trigger/contact, animation event callbacks, rendering callbacks or OnValidate. You must use Destroy instead."));
    }

    [Test]
    public void DestroyingGameObjects_AitKeywordProtected()
    {
        // SDK가 동일 문구를 진단/리포트로 출력하더라도 [AIT] prefix가 붙으면 보호되어야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] diag: Destroying GameObjects immediately is not permitted during teardown"));
    }

    #endregion

    #region 'AppsInToss' 식별자 미발견 컴파일 에러 (SDK-C3, SDK-M7, SDK-PV)

    [Test]
    public void UserCode_AppsInTossNamespaceMissing_ReturnsTrue()
    {
        // Sentry SDK-C3 — 사용자 스크립트가 AppsInToss namespace를 import하지 못함.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets\\Script\\Tutorial_Howtoplay.cs(4,7): error CS0246: The type or namespace name 'AppsInToss' could not be found (are you missing a using directive or an assembly reference?)"));
    }

    [Test]
    public void UserCode_AppsInTossNamespaceMissing_PosixPath_ReturnsTrue()
    {
        // Sentry SDK-M7 — POSIX 경로 변형
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/CommonScripts/AdManager.cs(3,7): error CS0246: The type or namespace name 'AppsInToss' could not be found (are you missing a using directive or an assembly reference?)"));
    }

    [Test]
    public void Cs0246_WithoutAppsInTossToken_NotFiltered()
    {
        // CS0246 단독은 SDK 빌드 메시지/다른 namespace 미발견과 충돌할 수 있으므로
        // 'AppsInToss' 식별자가 동반될 때만 노이즈로 분류한다.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/Foo.cs(1,1): error CS0246: The type or namespace name 'SomethingElse' could not be found"));
    }

    [Test]
    public void Cs0246_AppsInTossToken_WithAitPrefix_StillProtected()
    {
        // SDK 보호 가드: [AIT] prefix가 붙은 동일 메시지는 SDK 자체 로그로 간주.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] diag: error CS0246: The type or namespace name 'AppsInToss' could not be found in fallback build"));
    }

    #endregion

    #region SDK 타입 인자 오용 컴파일 에러 CS1503 (SDK-VM, SDK-PV, SDK-PW, SDK-DA)

    [Test]
    public void UserCode_Cs1503_AppsInTossTypeMisuse_ReturnsTrue()
    {
        // Sentry SDK-VM — TossManager.cs에서 GetUserKeyForGameResult를 string으로 잘못 사용.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets\\Scripts\\Manager\\TossManager.cs(192,91): error CS1503: Argument 1: cannot convert from 'AppsInToss.GetUserKeyForGameResult' to 'string'"));
    }

    [Test]
    public void UserCode_Cs1503_AppsInTossTypeMisuse_PosixPath_ReturnsTrue()
    {
        // Sentry SDK-PV — POSIX 경로 변형 (IapProductListItem 잘못 사용).
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/02.Script/Platform/AIT/AITBridge.IAP.cs(17,26): error CS1503: Argument 1: cannot convert from 'AppsInToss.IapProductListItem' to 'string'"));
    }

    [Test]
    public void UserCode_Cs1503_AitException_ReturnsTrue()
    {
        // Sentry SDK-PW — modules 경로의 사용자 코드, AITException 잘못 사용.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/modules/unity-trident/Runtime/FeatureService/Billing/Billing.cs(398,42): error CS1503: Argument 1: cannot convert from 'AppsInToss.AITException' to 'string'"));
    }

    [Test]
    public void Cs1503_WithoutAppsInTossNamespace_NotFiltered()
    {
        // CS1503 단독은 SDK와 무관한 컴파일 에러도 잡을 수 있으므로 'AppsInToss.' namespace prefix가
        // 동반될 때만 노이즈로 분류. 'AppsInToss.' 점(.) 위치가 중요 (단독 토큰 'AppsInToss'는 통과).
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Assets/Foo.cs(1,1): error CS1503: Argument 1: cannot convert from 'System.Int32' to 'string'"));
    }

    [Test]
    public void Cs1503_AppsInTossTypeMisuse_WithAitPrefix_StillProtected()
    {
        // SDK 보호 가드: 합성 가드(error CS1503 + 'AppsInToss.' + Assets/ + .cs()를 모두 만족하지 않으면
        // 다음 단계인 SDK 키워드 가드에서 [AIT] prefix로 보호된다. 여기서는 Assets/와 .cs( 없이
        // SDK가 진단/리포트로 같은 토큰을 출력하는 케이스를 검증.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] diag: error CS1503 fallback: 'AppsInToss.Result' conversion mismatch detected"));
    }

    #endregion

    #region 'ait deploy' stdout/stderr ANSI escape 노이즈 (SDK-VK, SDK-BD, SDK-T5)

    [Test]
    public void DeployStdout_AnsiCursorHide_ReturnsTrue()
    {
        // Sentry SDK-VK/BD/T5 — pnpm progress bar의 커서 hide(\x1b[?25l) escape가 stdout에 새는 경우.
        // 구버전 SDK 사용자가 deploy 실행 시 다수의 별도 fingerprint로 캡처되던 메시지.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: [stdout] \x1b[?25l│"));
    }

    [Test]
    public void DeployStderr_AnsiCursorShow_ReturnsTrue()
    {
        // 동일 패턴의 stderr 변형 — 커서 show(\x1b[?25h) escape 포함.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: [stderr] \x1b[?25h│"));
    }

    [Test]
    public void DeployStdout_WithoutAnsiEscape_NotFiltered()
    {
        // 실제 진단 가치가 있는 stdout 메시지는 보호되어야 한다 — ANSI escape "[?25" 미포함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: [stdout] Building project... done in 12.3s"));
    }

    [Test]
    public void DeployStdout_AnsiEscape_WithAitPrefix_StillProtected()
    {
        // SDK 보호 가드: [AIT...] prefix가 붙은 동일 패턴은 SDK 자체 진단 로그로 간주.
        // 합성 가드의 "AIT: [std" 토큰을 우회하기 위해 [AIT] prefix가 먼저 와야 보호 가능.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] deploy diag: stdout snapshot contains \x1b[?25l progress bar fragment"));
    }

    #endregion

    #region AITAsyncCommandRunner Windows powershell 실행 실패 (SDK-VE, SDK-VC)

    [Test]
    public void AsyncCommand_Win32Exception_PowershellMissing_ReturnsTrue()
    {
        // Sentry SDK-VE/VC — 사용자 Windows 환경에서 powershell.exe 실행 실패. PATH/실행 정책 문제.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT Async] 명령 실행 예외: System.ComponentModel.Win32Exception (0x80004005): " +
            "ApplicationName='powershell.exe', CommandLine='-ExecutionPolicy Bypass -NoProfile -NoLogo -Command \"...\"'"));
    }

    [Test]
    public void AsyncCommand_OtherException_NotFiltered()
    {
        // Win32Exception이 아닌 일반 [AIT Async] 예외는 SDK 디버깅에 가치가 있으므로 보호.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT Async] 명령 실행 예외: System.InvalidOperationException: pipe closed"));
    }

    [Test]
    public void AsyncCommand_Win32Exception_NonPowershell_NotFiltered()
    {
        // powershell.exe 외 다른 ApplicationName의 Win32Exception은 패턴 좁히기 위해 통과.
        // (현재는 pnpm/git/node 등은 별도 진단 경로가 있어 SDK 가치 보존)
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT Async] 명령 실행 예외: System.ComponentModel.Win32Exception (0x80004005): " +
            "ApplicationName='node.exe', CommandLine='...'"));
    }

    #endregion

    #region 외부 WebGL 템플릿 (SDK-VJ)

    [Test]
    public void ExternalWebGLTemplate_UnityWebviewSourceNotFound_ReturnsTrue()
    {
        // Sentry SDK-VJ — 사용자 프로젝트가 사용하는 다른 WebGL 템플릿(Fill)이 출력한 진단.
        // SDK는 "[WebGL]" prefix를 사용하지 않음.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[WebGL] unity-webview.js source not found: " +
            "/Users/ad03148361/jenkins_workspace/attack-web/Assets/WebGLTemplates/Fill/TemplateData/unity-webview.js"));
    }

    [Test]
    public void WebGL_OtherWarning_NotFiltered()
    {
        // 다른 "[WebGL]" prefix 메시지는 패턴이 좁혀 있어 통과 (unity-webview source 한정).
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[WebGL] unity-webview.js compiled successfully"));
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

    #region IL2CPP/Bee 빌드 단위별 실패 (SDK-SA~SV, T7, TV)

    [Test]
    public void BuildLibraryBee_ObjFailed_ReturnsTrue()
    {
        // Sentry SDK-SV: 매번 다른 해시 파일명 — "Building Library/Bee/artifacts/WebGL/" 부분 문자열로 일괄 매칭
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Building Library/Bee/artifacts/WebGL/GameAssembly/master_WebGL_wasm/uqx36jn5evd9.o failed with output:"));
    }

    [Test]
    public void BuildLibraryBee_ReleaseObjFailed_ReturnsTrue()
    {
        // Sentry SDK-SQ
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Building Library/Bee/artifacts/WebGL/GameAssembly/release_WebGL_wasm/287iqgly6k3x.o failed with output:"));
    }

    [Test]
    public void BuildLibraryBee_ManagedStripped_ReturnsTrue()
    {
        // Sentry SDK-T7
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Building Library/Bee/artifacts/WebGL/ManagedStripped failed with output:"));
    }

    [Test]
    public void BuildLibraryBee_BuildJs_ReturnsTrue()
    {
        // Sentry SDK-TV
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Building Library/Bee/artifacts/WebGL/build/debug_WebGL_wasm/build.js failed with output:"));
    }

    [Test]
    public void BuildLibraryBee_WithAitPrefix_StillProtected()
    {
        // SDK 보호 가드: SDK가 동일 prefix로 출력하는 가상의 케이스도 필터링 안 되어야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Building Library/Bee/artifacts/WebGL/GameAssembly/release_WebGL_wasm/xxx.o failed"));
    }

    #endregion

    #region git wrapper trace (SDK-TE, SDK-TF)

    [Test]
    public void ExecCmdGit_ShowVariant_ReturnsTrue()
    {
        // Sentry SDK-TF: Unity Collab/CCD 등이 cmd 래핑으로 git 호출
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Exec> cmd /c \"git\" show -s --pretty=%D HEAD"));
    }

    [Test]
    public void ExecCmdGit_LogVariant_ReturnsTrue()
    {
        // Sentry SDK-TE
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Exec> cmd /c \"git\" log -1 --pretty=format:%h"));
    }

    [Test]
    public void ExecCmdGit_WithAitPrefix_StillProtected()
    {
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Exec> cmd /c \"git\" show -s --pretty=%D HEAD"));
    }

    #endregion

    #region 카테고리 D 노이즈 (Addressables / Vite / git / 정책 fetch / Unity 자체)

    [Test]
    public void Addressables_SbpError_ReturnsTrue()
    {
        // Sentry SDK-H4: "SBP ErrorError"는 ScriptableBuildPipeline 출력 자체.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage("SBP ErrorError"));
    }

    [Test]
    public void Addressables_FailedToBuildContent_ReturnsTrue()
    {
        // Sentry SDK-S4
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "BuildFailedException: BuildFailedException: Failed to build Addressables content, content not included in Player Build. \"SBP ErrorError\""));
    }

    [Test]
    public void BuildLayout_HasNotOpen_ReturnsTrue()
    {
        // Sentry SDK-EX
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Cannot read BuildLayout header, BuildLayout has not open for a file"));
    }

    [Test]
    public void DefaultAudioDevice_Changed_ReturnsTrue()
    {
        // Sentry SDK-TW
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Default audio device was changed, but the audio system failed to initialize it. Attempting to reset sound system."));
    }

    [Test]
    public void EmscriptenBuild_WebGlBuildFailed_ReturnsTrue()
    {
        // Sentry SDK-RV
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Building webgl/Build/204ccce7cc46e2cd9bd7212e664b4738.data.unityweb failed with output:"));
    }

    [Test]
    public void UnityAssetDb_LibraryLoad_ReturnsTrue()
    {
        // Sentry SDK-RT
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "Unknown error occurred while loading 'Library/AppsInToss/AITBuildSession.asset'."));
    }

    [Test]
    public void SdkPolicyFetchFailed_ReturnsTrue()
    {
        // Sentry SDK-M9
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] sdk-policy.json fetch 실패: System.Net.WebException: The operation has timed out."));
    }

    [Test]
    public void VitePortTimeout_ReturnsTrue()
    {
        // Sentry SDK-QN
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Vite 포트 5173 대기 타임아웃 (15초), 브라우저를 엽니다"));
    }

    [Test]
    public void DevServerStartFailed_PortBusy_ReturnsTrue()
    {
        // Sentry SDK-KP
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: Dev 서버 시작 실패 - 포트가 이미 사용 중입니다. 다른 서버가 실행 중인지 확인하세요."));
    }

    [Test]
    public void ProductionServerStartFailed_ReturnsTrue()
    {
        // Sentry SDK-Q3
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "AIT: Production 서버 시작 실패 - 프로세스가 비정상 종료되었습니다 (Exit Code: 1)"));
    }

    [Test]
    public void AutoCommit_AuthorIdentity_ReturnsTrue()
    {
        // Sentry SDK-SK
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] 자동 커밋 실패: Author identity unknown"));
    }

    [Test]
    public void AutoCommit_GitProcessTimeout_ReturnsTrue()
    {
        // Sentry SDK-TZ
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] 자동 커밋 실패: Git 프로세스를 시작할 수 없거나 타임아웃이 발생했습니다."));
    }

    [Test]
    public void GitCommandTimeout_300Seconds_ReturnsTrue()
    {
        // Sentry SDK-TY (300초 변형) — 5초 케이스(SDK-QC)는 #591에서 source 차단됨.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] Git 명령 타임아웃 (300초): git commit --quiet -m \"정리: .gitignore 보호 패턴 추가\""));
    }

    [Test]
    public void SdkPolicyFetch_NotMatching_GenericMessage_NotFiltered()
    {
        // negative — 단순 'sdk-policy' 문자열만 포함된 다른 메시지는 영향받지 않아야 함.
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] sdk-policy.json 적용 완료"));
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
