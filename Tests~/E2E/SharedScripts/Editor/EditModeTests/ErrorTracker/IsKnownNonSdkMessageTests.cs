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
    public void WorkerImportError_RealisticVariants_ReturnsTrue(string message)
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-RC/RD/RE — Unity AssetImporter 내부 워커 에러.
        // 워커 번호(2/3/4)가 가변이지만 "] Import Error Code:(" 부분 문자열로 모두 매칭.
        // SDK 코드에는 "Worker"/"Import Error Code" 문자열이 존재하지 않음 (grep으로 확인).
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(message));
    }

    [Test]
    public void WorkerImportError_WithAitPrefix_NeverFiltered()
    {
        // AitKeywords 가드 회귀 방지: [AIT] prefix가 붙으면 SDK 자체 로그로 간주되어 필터링되지 않아야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AIT] [Worker2] Import Error Code:(4)"));
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

    #region 외부 서비스 일시적 장애 (Sentry transport 5xx)

    [Test]
    public void SentryTransport_Http503_ReturnsTrue()
    {
        // Sentry APPS-IN-TOSS-UNITY-SDK-T4 — Sentry 서버의 일시적 503 응답.
        // SDK 자체 로그이지만 원인이 외부 서비스 transient 장애이므로 노이즈로 드롭.
        Assert.IsTrue(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] Sentry 전송 실패 (HTTP 503)"));
    }

    [Test]
    public void SentryTransport_Http500_ReturnsTrue()
    {
        // 5xx 일반화 — 500/502/504 등도 동일한 외부 transient 장애로 분류
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
    public void SentryTransport_Http400_NotFiltered()
    {
        // 4xx(인증/페이로드 오류)는 SDK가 알아야 하는 진짜 에러이므로 5xx 패턴에 매칭되지 않아야 함
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] Sentry 전송 실패 (HTTP 400)"));
    }

    [Test]
    public void SentryTransport_Http401_NotFiltered()
    {
        // DSN 오설정 등 — SDK 측 수정이 필요한 진짜 에러
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] Sentry 전송 실패 (HTTP 401)"));
    }

    [Test]
    public void SentryTransport_NetworkError_NotFiltered()
    {
        // 다른 transport 로그(네트워크 오류, 동기 전송 실패 등)는 영향 없음
        Assert.IsFalse(AITEditorErrorTracker.IsKnownNonSdkMessage(
            "[AITSentryTransport] 네트워크 오류: Connection refused"));
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
